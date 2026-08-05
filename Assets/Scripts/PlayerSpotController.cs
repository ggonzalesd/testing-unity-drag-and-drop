using System;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class PlayerSpotController : MonoBehaviour
{
  [SerializeField]
  [Range(0.0f, 1.0f)]
  private float InterpolateMomentum = 0.50f;
  [SerializeField]
  [Range(0.0f, 100.0f)]
  private float MomentumMultiplier = 50.0f;

  [SerializeField]
  [Range(0.0f, 20.0f)]
  private float PickDistance = 2.5f;
  [SerializeField]
  [Range(0.0f, 20.0f)]
  private float MoveDistance = 2.5f;

  [SerializeField]
  private float RotationSpeed = 190.0f;

  private Rigidbody target;
  private Vector3 momentum = Vector3.zero;
  private Vector3 targetPivot = Vector3.zero;

  // Start is called once before the first execution of Update after the MonoBehaviour is created
  // void Start()
  // {
  // }

  // Update is called once per frame
  void Update()
  {
    PickTarget();
    ReleaseTarget();

    HandleTarget();
  }

  // ongui
  void OnGUI()
  {
    if (target == null) return;

    // Draw Momentum Vector
    Vector3 screenMomentum = Camera.main.WorldToScreenPoint(target.position + momentum * 2.0f);
    Vector3 screenPosition = Camera.main.WorldToScreenPoint(target.position);
    Debug.DrawLine(screenPosition, screenMomentum, Color.red);
  }

  void OnDrawGizmos()
  {
    // Draw Target Pick Distance
    if (target != null)
    {
      Gizmos.color = Color.blue;
      Gizmos.DrawWireSphere(transform.position, PickDistance);
    }

    if (target == null) return;

    // Draw Target Position
    Gizmos.color = Color.green;
    Gizmos.DrawWireSphere(target.position, 0.3f);

    // Draw Target Momentum
    Gizmos.color = Color.red;
    Gizmos.DrawLine(target.position, target.position + momentum);

    // Draw Distance from Player to Target
    Gizmos.color = Color.yellow;
    Gizmos.DrawLine(transform.position, target.position);

    // Draw Target Pivot Point
    Gizmos.color = Color.cyan;
    Gizmos.DrawWireSphere(target.position + targetPivot, 0.1f);

    // Draw Text Distance in center of line
    Vector3 midPoint = (transform.position + target.position) / 2.0f;
    Handles.Label(midPoint, $"Distance: {Vector3.Distance(transform.position, target.position):F2}");
  }

  void HandleTarget()
  {
    if (target == null) return;

    Camera cam = Camera.main;

    Plane plane = new(-cam.transform.forward, transform.position);

    Vector2 mouse = Mouse.current.position.ReadValue();
    Ray ray = cam.ScreenPointToRay(mouse);

    // Debug.Log($"Mouse: {mouse.x}, {mouse.y}");

    if (plane.Raycast(ray, out float distance))
    {
      // Debug.Log($"Distance: {distance}");
      Vector3 worldPosition = ray.GetPoint(distance);

      var clampedPosition = Vector3.ClampMagnitude(worldPosition - transform.position, MoveDistance) + transform.position;

      var resolverTargetPosition = target.position + targetPivot; // Adjust the target position based on the pivot point
      momentum = Vector3.Lerp(momentum, (clampedPosition - resolverTargetPosition) * MomentumMultiplier,
        Mathf.Exp(-InterpolateMomentum * Time.deltaTime) // Decay factor for interpolation, ensuring smooth transition
      );

      var targetPositionWithPivot = clampedPosition - targetPivot; // Adjust the target position based on the pivot point

      target.MovePosition(targetPositionWithPivot);
    }

    // Rotate the target to face the camera's forward direction if A W S D keys are pressed
    // 1. Obtener el input de las teclas
    float horizontalInput = Keyboard.current.dKey.isPressed ? 1.0f : Keyboard.current.aKey.isPressed ? -1.0f : 0.0f;
    float verticalInput = Keyboard.current.wKey.isPressed ? 1.0f : Keyboard.current.sKey.isPressed ? -1.0f : 0.0f;

    // 2. Definir la velocidad de giro (grados por segundo)

    // 3. Crear el delta de rotación usando los ejes de la cámara
    // El input horizontal (A/D) rota alrededor del eje VERTICAL de la cámara (Up)
    // El input vertical (W/S) rota alrededor del eje HORIZONTAL de la cámara (Right)
    Vector3 camUp = cam.transform.up;
    Vector3 camRight = cam.transform.right;

    // 4. Calcular los Quaternions de transformación para este frame
    Quaternion horizontalRotation = Quaternion.AngleAxis(-horizontalInput * RotationSpeed * Time.deltaTime, camUp);
    Quaternion verticalRotation = Quaternion.AngleAxis(verticalInput * RotationSpeed * Time.deltaTime, camRight); // Negativo para que 'W' rote hacia arriba

    // 5. Combinar las transformaciones
    Quaternion deltaRotation = horizontalRotation * verticalRotation;

    // 6. Aplicar la transformación directamente al Quaternion actual del target
    target.transform.rotation = deltaRotation * target.transform.rotation;
  }

  void ReleaseTarget()
  {
    if (!Mouse.current.leftButton.wasReleasedThisFrame) return;

    if (target != null)
    {
      Debug.Log($"RELEASE {target.name}");
      target.useGravity = true;
      target.freezeRotation = false;
      target.linearVelocity = momentum;
      target = null;
      targetPivot = Vector3.zero;
    }
  }

  void PickTarget()
  {
    // Check if the left mouse button was pressed this frame
    if (!Mouse.current.leftButton.wasPressedThisFrame) return;
    // Check if the mouse is within the pick distance from the player
    if (Vector3.Distance(transform.position, Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue())) > PickDistance) return;

    Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

    if (Physics.Raycast(ray, out RaycastHit hit))
    {
      Debug.Log($"Hit {hit.collider.name}");
      Debug.Log($"Hit {hit.collider.tag}");
      // Tag "Item"
      if (hit.collider.CompareTag("Item"))
      {
        Debug.Log($"PICK {hit.collider.name}");
        target = hit.collider.GetComponent<Rigidbody>();

        var vec = Camera.main.transform.forward;
        vec.y = 0.0f; // Keep the y component zero to maintain horizontal orientation
        var currentTargetRotation = Quaternion.LookRotation(vec.normalized, Vector3.up);
        target.MoveRotation(currentTargetRotation);

        target.useGravity = false;
        target.freezeRotation = true;

        target.angularVelocity = Vector3.zero;
        momentum = target.linearVelocity;
        targetPivot = hit.point - target.position; // Calculate the pivot point relative to the target's position
      }
    }
  }
}
