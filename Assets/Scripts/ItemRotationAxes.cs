using UnityEngine;

// Axis a rotation key turns a held item around. World axes stay put whatever the
// item does, so the same key always turns it the same way on screen. Local axes
// ride the item, so the same key always turns the same edge of the item whatever
// its orientation.
public enum ItemRotationAxis
{
  WorldRight,
  WorldUp,
  WorldForward,
  LocalRight,
  LocalUp,
  LocalForward,
}

// Per item override for the axes the player's rotation keys work on. A plate, a
// stick and a tray each have a different edge worth turning around, and the
// controller cannot guess which from the collider.
//
// Optional: an item without one falls back to the controller's defaults. Purely
// descriptive, so it needs no Rigidbody of its own and sits happily next to an
// ItemCarrier, which is opt-in on a different pair of fields.
public class ItemRotationAxes : MonoBehaviour
{
  [SerializeField]
  [Tooltip("Axis A/D turns around.")]
  private ItemRotationAxis Yaw = ItemRotationAxis.WorldUp;
  [SerializeField]
  [Tooltip("Axis W/S turns around.")]
  private ItemRotationAxis Pitch = ItemRotationAxis.LocalRight;
  [SerializeField]
  [Tooltip("Axis Q/E turns around.")]
  private ItemRotationAxis Roll = ItemRotationAxis.LocalForward;

  public ItemRotationAxis YawAxis => Yaw;
  public ItemRotationAxis PitchAxis => Pitch;
  public ItemRotationAxis RollAxis => Roll;

  // World space direction of an axis for a given orientation. Local axes are
  // resolved against the orientation the hold is commanding rather than against
  // the Rigidbody's: the body always lags behind, so reading it would let the
  // axis drift while a key is held and bend the turn as it goes.
  public static Vector3 Resolve(ItemRotationAxis axis, Quaternion rotation) => axis switch
  {
    ItemRotationAxis.WorldRight => Vector3.right,
    ItemRotationAxis.WorldUp => Vector3.up,
    ItemRotationAxis.WorldForward => Vector3.forward,
    ItemRotationAxis.LocalRight => rotation * Vector3.right,
    ItemRotationAxis.LocalUp => rotation * Vector3.up,
    _ => rotation * Vector3.forward,
  };
}
