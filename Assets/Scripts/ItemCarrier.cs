using UnityEngine;

// An item that carries one other item. Needs a pivot and a capture volume
// assigned: an item without both is simply not a carrier, which is what keeps
// the behaviour opt-in per object.
[RequireComponent(typeof(Rigidbody))]
public class ItemCarrier : MonoBehaviour
{
  private const int OverlapBufferSize = 8;

  // A damping ratio of 1 is critical damping: damper = 2 * sqrt(spring).
  private const float CriticalDampingFactor = 2.0f;

  [SerializeField]
  [Tooltip("Where the carried item is placed. Its rotation becomes the item's rotation.")]
  private Transform Pivot;
  [SerializeField]
  [Tooltip("Volume that captures items. Only its shape is read, so it does not have to be a trigger.")]
  private Collider CaptureVolume;

  [SerializeField]
  [Tooltip("Gentle pull that keeps the item centred after it lands. It is not meant to carry the weight: the surface underneath does that. 0 places the item and then leaves it alone.")]
  private float PlaceSpring = 20.0f;
  [SerializeField]
  [Range(0.0f, 2.0f)]
  [Tooltip("1 is critically damped: settles without bouncing.")]
  private float PlaceDamping = 1.0f;

  private readonly Collider[] overlaps = new Collider[OverlapBufferSize];

  private Rigidbody self;
  private Rigidbody carried;

  private bool IsConfigured => Pivot != null && CaptureVolume != null;

  void Awake() => self = GetComponent<Rigidbody>();

  void FixedUpdate()
  {
    if (!IsConfigured) return;

    int count = Overlap();

    // The item stays a normal dynamic body, so it leaves on its own: rolling
    // out of the volume or being picked up is all it takes. Nothing to undo.
    if (carried != null && (PlayerSpotController.IsHeld(carried) || !IsPresent(carried, count)))
    {
      Debug.Log($"DROP {carried.name}");
      carried = null;
    }

    if (carried == null)
    {
      Rigidbody item = FindItem(count);
      if (item == null) return;

      Place(item);
      carried = item;
    }

    ApplyPlaceForce();
  }

  void Place(Rigidbody body)
  {
    Debug.Log($"CARRY {body.name}");

    // A physics level teleport, not a follow: the item is put down once and is
    // free from that point on. Its momentum is dropped so it settles instead of
    // shooting off in whatever direction it arrived from.
    body.position = Pivot.position;
    body.rotation = Pivot.rotation;
    body.linearVelocity = Vector3.zero;
    body.angularVelocity = Vector3.zero;
  }

  // Weak on purpose. It only nudges the item back over the pivot, so anything
  // pushing it harder than this wins and the item slides off.
  void ApplyPlaceForce()
  {
    if (PlaceSpring <= 0.0f) return;

    float damper = CriticalDampingFactor * Mathf.Sqrt(PlaceSpring) * PlaceDamping;

    // Gravity is left alone: the item is meant to rest on the carrier's surface,
    // not to hang off the pivot. Cancelling it here is what would read as glue.
    Vector3 acceleration = (Pivot.position - carried.worldCenterOfMass) * PlaceSpring - carried.linearVelocity * damper;

    carried.AddForce(acceleration, ForceMode.Acceleration);
  }

  Rigidbody FindItem(int count)
  {
    for (int index = 0; index < count; index++)
    {
      Collider collider = overlaps[index];
      if (collider == null) continue;

      Rigidbody body = collider.attachedRigidbody;

      if (body == null) continue;
      // The volume sits on the carrier, so it always overlaps it.
      if (body == self) continue;
      if (!collider.CompareTag(ItemTags.Item)) continue;
      // Kinematic covers scenery.
      if (body.isKinematic) continue;
      // Never take an item out of the player's hand.
      if (PlayerSpotController.IsHeld(body)) continue;

      return body;
    }

    return null;
  }

  bool IsPresent(Rigidbody body, int count)
  {
    for (int index = 0; index < count; index++)
    {
      Collider collider = overlaps[index];

      if (collider != null && collider.attachedRigidbody == body) return true;
    }

    return false;
  }

  int Overlap()
  {
    ResolveVolume(out Vector3 center, out Vector3 halfExtents, out Quaternion orientation);

    return Physics.OverlapBoxNonAlloc(center, halfExtents, overlaps, orientation, ~0, QueryTriggerInteraction.Ignore);
  }

  void ResolveVolume(out Vector3 center, out Vector3 halfExtents, out Quaternion orientation)
  {
    if (CaptureVolume is BoxCollider box)
    {
      Transform volume = box.transform;
      Vector3 scale = volume.lossyScale;

      center = volume.TransformPoint(box.center);
      halfExtents = Vector3.Scale(box.size * 0.5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
      orientation = volume.rotation;
      return;
    }

    // Any other shape falls back to its world axis aligned box, which is a
    // superset of the real volume.
    Bounds bounds = CaptureVolume.bounds;

    center = bounds.center;
    halfExtents = bounds.extents;
    orientation = Quaternion.identity;
  }

  void OnDrawGizmos()
  {
    if (!IsConfigured) return;

    ResolveVolume(out Vector3 center, out Vector3 halfExtents, out Quaternion orientation);

    Gizmos.color = carried != null ? Color.yellow : Color.green;

    Matrix4x4 previousMatrix = Gizmos.matrix;
    Gizmos.matrix = Matrix4x4.TRS(center, orientation, Vector3.one);
    Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2.0f);
    Gizmos.matrix = previousMatrix;

    Gizmos.DrawWireSphere(Pivot.position, 0.05f);
  }
}
