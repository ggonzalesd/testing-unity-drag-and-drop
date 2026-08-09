using UnityEngine;

// What to play, where, and for how long.
//
// A struct passed by value, so a caller can build one inline at a collision or
// an input event without allocating, and so nothing can hold on to a request
// and mutate an effect that is already running.
//
// Built through the chained helpers rather than a constructor with six
// arguments: most calls set two of these fields and a positional constructor
// would make every one of them spell out defaults it does not care about.
public struct EffectRequest
{
  // A duration of zero or less means the effect runs until something stops it.
  // Looping effects then never end on their own; one shots still finish when
  // their last particle dies.
  public const float UntilStopped = 0.0f;

  private const float NaturalScale = 1.0f;

  public EffectId Id;
  public Vector3 Position;
  public Quaternion Rotation;
  // Null leaves the effect standing where it was spawned. Set it and the effect
  // rides the transform, which is what smoke coming off a moving object needs.
  public Transform Parent;
  public float Duration;
  public float Scale;

  public static EffectRequest At(EffectId id, Vector3 position) => new()
  {
    Id = id,
    Position = position,
    Rotation = Quaternion.identity,
    Duration = UntilStopped,
    Scale = NaturalScale,
  };

  // Each of these returns a modified copy, which is what makes chaining safe on
  // a struct: nothing here mutates a request another caller might be holding.
  public EffectRequest For(float seconds)
  {
    Duration = seconds;
    return this;
  }

  public EffectRequest Facing(Quaternion rotation)
  {
    Rotation = rotation;
    return this;
  }

  // Zero has no direction to look along, and LookRotation warns on it, so it is
  // treated as no rotation at all.
  public EffectRequest Facing(Vector3 direction) =>
    Facing(direction == Vector3.zero ? Quaternion.identity : Quaternion.LookRotation(direction));

  public EffectRequest On(Transform parent)
  {
    Parent = parent;
    return this;
  }

  public EffectRequest Scaled(float scale)
  {
    Scale = scale;
    return this;
  }
}
