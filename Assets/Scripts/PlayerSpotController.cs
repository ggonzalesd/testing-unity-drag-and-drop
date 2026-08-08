using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PlayerSpotController : MonoBehaviour
{
  public enum RotationPivot
  {
    HitPoint,
    Center,
    CenterOfMass,
  }

  private const string ItemTag = "Item";

  // A damping ratio of 1 is critical damping: damper = 2 * sqrt(spring).
  private const float CriticalDampingFactor = 2.0f;

  [Header("Reach")]
  [SerializeField]
  [Range(0.0f, 20.0f)]
  private float PickDistance = 2.5f;
  [SerializeField]
  [Range(0.0f, 20.0f)]
  private float MoveDistance = 2.5f;
  [SerializeField]
  [Tooltip("Drops the item when the grab point falls this far behind the anchor. 0 disables it.")]
  private float BreakDistance = 4.0f;

  [Header("Hold")]
  [SerializeField]
  [Tooltip("Stiffness of the spring pulling the grab point towards the anchor.")]
  private float HoldSpring = 120.0f;
  [SerializeField]
  [Range(0.0f, 2.0f)]
  [Tooltip("1 is critically damped: fastest approach without overshoot.")]
  private float HoldDamping = 1.0f;
  [SerializeField]
  [Tooltip("Caps the spring pull only. The damper is never scaled down, so a saturated spring cannot leave the hold undamped.")]
  private float MaxHoldAcceleration = 250.0f;
  [SerializeField]
  [Tooltip("Cancels the sag the spring would otherwise show under gravity.")]
  private bool CompensateGravity = true;
  [SerializeField]
  [Range(0.0f, 0.5f)]
  [Tooltip("Smooths pointer noise before it reaches the physics solver.")]
  private float AnchorSmoothTime = 0.02f;

  [Header("Rotation")]
  [SerializeField]
  private float RotationSpeed = 190.0f;
  [SerializeField]
  private float TorqueSpring = 200.0f;
  [SerializeField]
  [Range(0.0f, 2.0f)]
  private float TorqueDamping = 1.0f;
  [SerializeField]
  private float MaxHoldAngularAcceleration = 400.0f;
  [SerializeField]
  private RotationPivot PivotMode = RotationPivot.HitPoint;

  [Header("Release")]
  [SerializeField]
  [Range(0.0f, 30.0f)]
  [Tooltip("How quickly the throw velocity tracks the item's actual velocity.")]
  private float MomentumSmoothing = 15.0f;
  [SerializeField]
  [Range(0.0f, 5.0f)]
  private float ThrowMultiplier = 1.0f;

  [Header("Solver (held item only)")]
  [SerializeField]
  [Range(1, 30)]
  private int HeldSolverIterations = 12;
  [SerializeField]
  [Range(1, 30)]
  private int HeldSolverVelocityIterations = 4;
  [SerializeField]
  [Range(0.0f, 1.0f)]
  [Tooltip("Raises the smallest inertia axis to this fraction of the largest while held. A 4x0.25x1 stick has 16x less inertia around its long axis, which turns any residual torque into a spin. 0 disables it.")]
  private float MinInertiaRatio = 0.25f;

  private Camera mainCamera;
  private Rigidbody target;
  private Vector3 momentum = Vector3.zero;

  // Pivot offset in the target's local frame, so it follows every rotation
  // instead of going stale the moment the item turns.
  private Vector3 pivotLocal = Vector3.zero;

  // Orientation the hold aims for. The Rigidbody reaches it through torque, so
  // it lags on purpose whenever something resists.
  private Quaternion heldRotation = Quaternion.identity;

  // Rate the commanded orientation is turning at. The torque damper measures
  // against this instead of against zero, or it would brake the very rotation
  // the player is asking for.
  private Vector3 heldAngularVelocity = Vector3.zero;

  // Smoothed anchor: raw pointer noise would reach the solver as impulses and
  // shake anything resting on the item.
  private Vector3 anchor = Vector3.zero;
  private Vector3 anchorVelocity = Vector3.zero;

  // Input is sampled in Update (edge events would be missed in FixedUpdate)
  // and consumed in FixedUpdate, where the Rigidbody is actually driven.
  private Vector2 rotationInput = Vector2.zero;
  private Vector2 pointerPosition = Vector2.zero;
  private bool levelRequested;

  private bool targetFrozeRotation;
  private bool targetIsKinematic;
  private RigidbodyInterpolation targetInterpolation;
  private CollisionDetectionMode targetCollisionDetection;
  private int targetSolverIterations;
  private int targetSolverVelocityIterations;
  private bool targetAutomaticInertiaTensor;
  private Vector3 targetInertiaTensor;

  private Camera MainCamera => mainCamera != null ? mainCamera : mainCamera = Camera.main;

  // Where the grab point actually is right now. Uses the Rigidbody's real
  // rotation, not the commanded one, because forces act on the real pose.
  private Vector3 PivotWorld => target.position + target.rotation * pivotLocal;

  // Lever arm of the grab point. Measured from the center of mass, because that
  // is what linearVelocity refers to and what angularVelocity turns around.
  private Vector3 PivotLever => PivotWorld - target.worldCenterOfMass;

  void Update()
  {
    SampleInput();

    PickTarget();
    ReleaseTarget();
  }

  void FixedUpdate()
  {
    HandleTarget();
  }

  void OnDrawGizmos()
  {
    // Draw Target Pick Distance
    Gizmos.color = Color.blue;
    Gizmos.DrawWireSphere(transform.position, PickDistance);

    if (target == null) return;

    // Draw Target Position
    Gizmos.color = Color.green;
    Gizmos.DrawWireSphere(target.position, 0.3f);

    // Draw Throw Momentum
    Gizmos.color = Color.red;
    Gizmos.DrawLine(target.position, target.position + momentum);

    // Draw Distance from Player to the grab point, which is what the reach
    // limits actually constrain
    Vector3 pivot = PivotWorld;
    Gizmos.color = Color.yellow;
    Gizmos.DrawLine(transform.position, pivot);

    // Draw the pivot and the spring pulling it towards the anchor
    Gizmos.color = Color.cyan;
    Gizmos.DrawWireSphere(pivot, 0.1f);
    Gizmos.DrawLine(target.position, pivot);

    Gizmos.color = Color.magenta;
    Gizmos.DrawWireSphere(anchor, 0.05f);
    Gizmos.DrawLine(pivot, anchor);

#if UNITY_EDITOR
    // Draw Text Distance in center of line
    Vector3 midPoint = (transform.position + pivot) / 2.0f;
    Handles.Label(midPoint, $"Distance: {Vector3.Distance(transform.position, pivot):F2}");
#endif
  }

  void SampleInput()
  {
    Keyboard keyboard = Keyboard.current;
    Mouse mouse = Mouse.current;

    // Horizontal (A/D) rotates around the camera's Up axis,
    // vertical (W/S) around the camera's Right axis.
    rotationInput = keyboard == null
      ? Vector2.zero
      : new Vector2(
        (keyboard.dKey.isPressed ? -1.0f : 0.0f) - (keyboard.aKey.isPressed ? -1.0f : 0.0f),
        (keyboard.wKey.isPressed ? -1.0f : 0.0f) - (keyboard.sKey.isPressed ? -1.0f : 0.0f));

    if (mouse == null) return;

    pointerPosition = mouse.position.ReadValue();

    // Latched: Update can run several times between physics steps, so the edge
    // would be lost if FixedUpdate simply polled it.
    levelRequested |= mouse.rightButton.wasPressedThisFrame;
  }

  void HandleTarget()
  {
    if (target == null) return;

    Camera cam = MainCamera;
    if (cam == null) return;

    float deltaTime = Time.fixedDeltaTime;

    UpdateHeldRotation(cam, deltaTime);

    anchor = Vector3.SmoothDamp(anchor, ResolveAnchor(cam), ref anchorVelocity, AnchorSmoothTime, Mathf.Infinity, deltaTime);

    // The item is a plain dynamic body now, so it can get stuck behind geometry
    // while the spring keeps winding up. Let go instead.
    if (BreakDistance > 0.0f && Vector3.Distance(anchor, PivotWorld) > BreakDistance)
    {
      Debug.Log($"BREAK {target.name}");
      DropTarget();
      return;
    }

    ApplyHoldForce(deltaTime);
    ApplyHoldTorque(deltaTime);

    // The body carries a real velocity now, so the throw is just a smoothed
    // reading of it rather than a separately integrated guess.
    momentum = Vector3.Lerp(momentum, target.linearVelocity,
      1.0f - Mathf.Exp(-MomentumSmoothing * deltaTime) // Frame-rate independent decay factor
    );
  }

  void UpdateHeldRotation(Camera cam, float deltaTime)
  {
    if (levelRequested)
    {
      levelRequested = false;
      heldRotation = LevelRotation(heldRotation);
    }

    // Both axes are levelled: A/D yaws around world up, W/S pitches around the
    // camera's right flattened onto XZ. Using the camera's own tilted up/right
    // would leak the view pitch into the item as roll and tip it over.
    Vector3 pitchAxis = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up);
    pitchAxis = pitchAxis.sqrMagnitude > Mathf.Epsilon ? pitchAxis.normalized : Vector3.right;

    Quaternion horizontalRotation = Quaternion.AngleAxis(-rotationInput.x * RotationSpeed * deltaTime, Vector3.up);
    Quaternion verticalRotation = Quaternion.AngleAxis(rotationInput.y * RotationSpeed * deltaTime, pitchAxis);

    heldRotation = horizontalRotation * verticalRotation * heldRotation;

    // Same two axis rates the rotation above just applied, kept as a world
    // angular velocity so the torque damper can subtract it out.
    heldAngularVelocity =
      Vector3.up * (-rotationInput.x * RotationSpeed * Mathf.Deg2Rad)
      + pitchAxis * (rotationInput.y * RotationSpeed * Mathf.Deg2Rad);
  }

  // Drops pitch and roll and keeps only the yaw, so the item stands upright
  // again. It only retargets the spring, which then glides the item there
  // instead of snapping it and kicking whatever is stacked on top.
  static Quaternion LevelRotation(Quaternion rotation)
  {
    Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);

    // The item's forward can point straight up or down, where the projection
    // collapses. Its up axis is horizontal in exactly that case.
    if (forward.sqrMagnitude <= Mathf.Epsilon) forward = Vector3.ProjectOnPlane(rotation * Vector3.up, Vector3.up);
    if (forward.sqrMagnitude <= Mathf.Epsilon) return Quaternion.identity;

    return Quaternion.LookRotation(forward.normalized, Vector3.up);
  }

  // Pointer projected onto the plane through the player, clamped to reach.
  // Falls back to holding the grab point where it already is.
  Vector3 ResolveAnchor(Camera cam)
  {
    Plane plane = new(-cam.transform.forward, transform.position);
    Ray ray = cam.ScreenPointToRay(pointerPosition);

    if (!plane.Raycast(ray, out float distance)) return PivotWorld;

    Vector3 worldPosition = ray.GetPoint(distance);

    return Vector3.ClampMagnitude(worldPosition - transform.position, MoveDistance) + transform.position;
  }

  void ApplyHoldForce(float deltaTime)
  {
    Vector3 pivot = PivotWorld;
    Vector3 lever = PivotLever;

    // Velocity of the grab point itself, not of the body center: the lever arm
    // contributes through the angular term.
    Vector3 pivotVelocity = target.linearVelocity + Vector3.Cross(target.angularVelocity, lever);

    // The anchor is what the grab point is supposed to ride along with, so the
    // damper measures the velocity relative to it. Damping the absolute velocity
    // instead makes the damper fight every fast drag: the item lags, the spring
    // winds up, and it snaps back the moment the pointer settles.
    Vector3 relativeVelocity = pivotVelocity - anchorVelocity;

    float damper = StableDamper(HoldSpring, HoldDamping, deltaTime);

    // Only the spring is capped. Scaling the damper down along with it would
    // strip the hold of damping exactly during the fast moves that saturate it.
    Vector3 acceleration =
      Vector3.ClampMagnitude((anchor - pivot) * HoldSpring, MaxHoldAcceleration)
      - relativeVelocity * damper;

    // A force at a lever arm also spins the body, so the grab point accelerates
    // by far more than force/mass: for the 4x0.25x1 stick held at its tip the
    // point responds as if it weighed a quarter of the body. Dividing by the
    // effective mass along the pull cancels that, which is what keeps the
    // tuning stable for long items instead of ringing at the solver rate.
    float inverseEffectiveMass = InverseEffectiveMass(lever, acceleration);
    Vector3 force = inverseEffectiveMass > Mathf.Epsilon ? acceleration / inverseEffectiveMass : Vector3.zero;

    // Applying at the pivot yields the torque that makes the item hang from the
    // grab point instead of from its center.
    target.AddForceAtPosition(force, pivot, ForceMode.Force);

    // Gravity pulls at the center of mass and produces no torque there, so its
    // counterweight has to be applied the same way. Folding it into the pivot
    // force would invent a torque the orientation spring then has to fight.
    if (CompensateGravity && target.useGravity) target.AddForce(-Physics.gravity, ForceMode.Acceleration);
  }

  void ApplyHoldTorque(float deltaTime)
  {
    float damper = StableDamper(TorqueSpring, TorqueDamping, deltaTime);

    Quaternion delta = heldRotation * Quaternion.Inverse(target.rotation);

    // The negated quaternion is the same rotation taken the long way around.
    if (delta.w < 0.0f) delta = new Quaternion(-delta.x, -delta.y, -delta.z, -delta.w);

    delta.ToAngleAxis(out float angle, out Vector3 axis);

    Vector3 spring = Vector3.zero;

    if (angle > Mathf.Epsilon && !float.IsInfinity(axis.sqrMagnitude))
    {
      spring = axis.normalized * (angle * Mathf.Deg2Rad * TorqueSpring);
    }

    // Damped against the commanded turn rate, not against zero, so holding a
    // rotation key does not run the spring and the damper against each other.
    Vector3 torque =
      Vector3.ClampMagnitude(spring, MaxHoldAngularAcceleration)
      - (target.angularVelocity - heldAngularVelocity) * damper;

    // Acceleration mode already divides by the inertia tensor, so this control
    // path needs no effective-mass correction of its own.
    target.AddTorque(torque, ForceMode.Acceleration);
  }

  // Critical damping for the given stiffness, held below the rate at which an
  // explicit step would overshoot. Past damper * dt = 1 the correction flips the
  // velocity's sign every step, which reads as a high frequency tremble rather
  // than as damping.
  static float StableDamper(float spring, float damping, float deltaTime)
  {
    float damper = CriticalDampingFactor * Mathf.Sqrt(spring) * damping;

    return deltaTime > Mathf.Epsilon ? Mathf.Min(damper, 1.0f / deltaTime) : damper;
  }

  // Inverse of the mass the grab point presents along a direction:
  // 1/m + (r x n)^T * I^-1 * (r x n), the same term a point constraint solves for.
  float InverseEffectiveMass(Vector3 lever, Vector3 direction)
  {
    float inverseMass = target.mass > Mathf.Epsilon ? 1.0f / target.mass : 0.0f;

    if (direction.sqrMagnitude <= Mathf.Epsilon) return inverseMass;

    Vector3 leverCrossDirection = Vector3.Cross(lever, direction.normalized);

    return inverseMass + Vector3.Dot(leverCrossDirection, InverseInertiaTimes(leverCrossDirection));
  }

  // I^-1 * v in world space. The tensor is diagonal in its own principal frame,
  // so the vector is rotated into that frame, scaled, and rotated back.
  Vector3 InverseInertiaTimes(Vector3 worldVector)
  {
    Quaternion principal = target.rotation * target.inertiaTensorRotation;
    Vector3 local = Quaternion.Inverse(principal) * worldVector;
    Vector3 inertia = target.inertiaTensor;

    local = new Vector3(
      inertia.x > Mathf.Epsilon ? local.x / inertia.x : 0.0f,
      inertia.y > Mathf.Epsilon ? local.y / inertia.y : 0.0f,
      inertia.z > Mathf.Epsilon ? local.z / inertia.z : 0.0f);

    return principal * local;
  }

  void ReleaseTarget()
  {
    if (target == null) return;
    if (Mouse.current == null || !Mouse.current.leftButton.wasReleasedThisFrame) return;

    Debug.Log($"RELEASE {target.name}");
    DropTarget();
  }

  void DropTarget()
  {
    target.freezeRotation = targetFrozeRotation;
    target.isKinematic = targetIsKinematic;
    target.interpolation = targetInterpolation;
    target.collisionDetectionMode = targetCollisionDetection;
    target.solverIterations = targetSolverIterations;
    target.solverVelocityIterations = targetSolverVelocityIterations;

    // Order matters: assigning the tensor turns the automatic one off, so the
    // flag has to be restored after the value it overrides.
    target.inertiaTensor = targetInertiaTensor;
    target.automaticInertiaTensor = targetAutomaticInertiaTensor;

    if (!target.isKinematic) target.linearVelocity = momentum * ThrowMultiplier;

    target = null;
    pivotLocal = Vector3.zero;
    heldRotation = Quaternion.identity;
    heldAngularVelocity = Vector3.zero;
    anchorVelocity = Vector3.zero;
    levelRequested = false;
  }

  void PickTarget()
  {
    if (target != null) return;
    // Check if the left mouse button was pressed this frame
    if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

    Camera cam = MainCamera;
    if (cam == null) return;

    Ray ray = cam.ScreenPointToRay(pointerPosition);

    if (!Physics.Raycast(ray, out RaycastHit hit)) return;
    if (!hit.collider.CompareTag(ItemTag)) return;

    // Reach is measured against the surface actually hit.
    if (Vector3.Distance(transform.position, hit.point) > PickDistance) return;

    Rigidbody body = hit.collider.attachedRigidbody;
    if (body == null) return;

    Debug.Log($"PICK {hit.collider.name}");

    target = body;
    targetFrozeRotation = body.freezeRotation;
    targetIsKinematic = body.isKinematic;
    targetInterpolation = body.interpolation;
    targetCollisionDetection = body.collisionDetectionMode;
    targetSolverIterations = body.solverIterations;
    targetSolverVelocityIterations = body.solverVelocityIterations;
    targetAutomaticInertiaTensor = body.automaticInertiaTensor;
    targetInertiaTensor = body.inertiaTensor;

    pivotLocal = ResolvePivotLocal(hit);

    // The item keeps whatever orientation it had; the hold takes it over as-is.
    heldRotation = body.rotation;
    heldAngularVelocity = Vector3.zero;

    // Gravity stays untouched: the spring carries the weight, and anything
    // stacked on top needs a support that pushes back like a real body.
    body.isKinematic = false;
    // Rotation constraints would swallow the hold torque.
    body.freezeRotation = false;
    body.interpolation = RigidbodyInterpolation.Interpolate;
    // Speculative contacts, not sweeps: a body driven by a spring reverses often
    // enough that CCD sweeps keep re-resolving the same contact and buzz.
    body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    body.solverIterations = HeldSolverIterations;
    body.solverVelocityIterations = HeldSolverVelocityIterations;

    ThickenInertiaTensor(body);

    anchor = PivotWorld;
    anchorVelocity = Vector3.zero;
    momentum = body.linearVelocity;
    levelRequested = false;
  }

  // A thin item has a far smaller inertia around its long axis than around the
  // other two, so the same leftover torque spins it many times faster there.
  // Raising the floor while it is held evens the axes out; the real tensor comes
  // back on release.
  void ThickenInertiaTensor(Rigidbody body)
  {
    if (MinInertiaRatio <= 0.0f) return;

    Vector3 inertia = body.inertiaTensor;
    float floor = Mathf.Max(inertia.x, inertia.y, inertia.z) * MinInertiaRatio;

    body.inertiaTensor = Vector3.Max(inertia, Vector3.one * floor);
  }

  Vector3 ResolvePivotLocal(RaycastHit hit)
  {
    Quaternion inverseRotation = Quaternion.Inverse(target.rotation);

    return PivotMode switch
    {
      RotationPivot.HitPoint => inverseRotation * (hit.point - target.position),
      RotationPivot.CenterOfMass => inverseRotation * (target.worldCenterOfMass - target.position),
      _ => Vector3.zero,
    };
  }
}
