// Receipt for a running effect, so a caller that started one can stop it later.
//
// A number rather than a reference to the instance, for one reason: instances
// are pooled. Hand out the object and a caller that keeps it a second too long
// is driving whatever effect got recycled into its place. A number that is
// never reused goes stale instead, and a stale handle simply matches nothing.
public readonly struct EffectHandle
{
  public static readonly EffectHandle None = new(0);

  public readonly int Value;

  public EffectHandle(int value) => Value = value;

  public bool IsValid => Value != 0;
}
