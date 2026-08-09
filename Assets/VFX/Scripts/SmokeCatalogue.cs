using UnityEngine;

// The smoke columns. All three are the same column with one thing changed, so
// they are tuned against each other rather than in isolation.
public sealed class SmokeCatalogue : IEffectCatalogue
{
  // How much smaller, faster and busier the speck layer is than the body it
  // rides on. Derived rather than written out per variant, so retuning a column
  // carries its specks along instead of leaving them behind.
  private const float MicroRate = 2.5f;
  private const float MicroLifetime = 0.6f;
  private const float MicroSize = 0.18f;
  private const float MicroSpeed = 1.9f;
  private const float MicroSpread = 1.6f;
  private const float MicroTurbulence = 1.8f;

  // Every column loops for four seconds. It doubles as the period of the pulse
  // on the variant that has one.
  private const float CycleSeconds = 4.0f;

  // Drifts slightly darker as it thins, so a cloud reads as cooling rather than
  // as simply dissolving.
  private static readonly GradientColorKey[] Cooling =
  {
    new(Color.white, 0.0f),
    new(new Color(0.85f, 0.85f, 0.85f), 1.0f),
  };

  public bool Contains(EffectId id) =>
    id == EffectId.SmokeGusty || id == EffectId.SmokeSurge || id == EffectId.SmokeDrift;

  public ParticleProfile[] LayersFor(EffectId id)
  {
    if (!Contains(id)) return null;

    ParticleProfile body = BodyFor(id);

    return new[] { body, MicroOf(body) };
  }

  static ParticleProfile BodyFor(EffectId id) => id switch
  {
    // Ragged and blown sideways. The same column, bad weather.
    EffectId.SmokeGusty => new ParticleProfile
    {
      LayerName = "Body",
      Sprite = ParticleSprite.Circle,
      Draw = ParticleDraw.Billboard,
      Looping = true,
      Duration = CycleSeconds,
      RateOverTime = 16.0f,
      LifetimeMin = 2.2f,
      LifetimeMax = 3.4f,
      SizeMin = 0.28f,
      SizeMax = 0.45f,
      SizeStart = 0.5f,
      SizeGrowth = 2.8f,
      SizeEnd = 0.0f,
      SizePeak = 0.7f,
      SpeedMin = 0.4f,
      SpeedMax = 0.7f,
      GravityModifier = -0.09f,
      // Sideways push that beats the rise, so the column leans instead of
      // standing up and merely shivering. With the noise cut back, this is what
      // carries the variant's character on its own.
      Drift = new Vector3(1.1f, 0.0f, 0.3f),
      Spawn = ParticleSpawn.Cone,
      ConeAngle = 16.0f,
      SpawnRadius = 0.08f,
      RotationSpeed = 16.0f,
      NoiseStrength = 0.5f,
      NoiseFrequency = 0.4f,
      Palette = new[] { new Color(0.7f, 0.71f, 0.75f) },
      Ramp = Cooling,
      MaxParticles = 85,
    },
    // Breathes: the emission swells and drops instead of holding steady, so the
    // column arrives in slugs. What a fire that keeps catching actually does.
    EffectId.SmokeSurge => new ParticleProfile
    {
      LayerName = "Body",
      Sprite = ParticleSprite.Circle,
      Draw = ParticleDraw.Billboard,
      Looping = true,
      Duration = CycleSeconds,
      RateOverTime = 26.0f,
      PulseDepth = 0.8f,
      LifetimeMin = 2.2f,
      LifetimeMax = 3.4f,
      SizeMin = 0.38f,
      SizeMax = 0.62f,
      SizeStart = 0.5f,
      SizeGrowth = 3.2f,
      SizeEnd = 0.0f,
      SizePeak = 0.7f,
      SpeedMin = 0.5f,
      SpeedMax = 0.85f,
      GravityModifier = -0.12f,
      Spawn = ParticleSpawn.Cone,
      ConeAngle = 26.0f,
      SpawnRadius = 0.16f,
      RotationSpeed = 10.0f,
      NoiseStrength = 0.22f,
      NoiseFrequency = 0.22f,
      Palette = new[] { new Color(0.66f, 0.67f, 0.7f) },
      Ramp = Cooling,
      MaxParticles = 130,
    },
    // Slow and pale. Smouldering embers or steam rather than a fire: it barely
    // climbs, hangs around, and sits light against a dark background.
    _ => new ParticleProfile
    {
      LayerName = "Body",
      Sprite = ParticleSprite.Circle,
      Draw = ParticleDraw.Billboard,
      Looping = true,
      Duration = CycleSeconds,
      // Far lower than the others, and it has to be. What fills a column is
      // rate times lifetime, and a particle here lives twice as long.
      RateOverTime = 9.0f,
      LifetimeMin = 4.0f,
      LifetimeMax = 6.0f,
      SizeMin = 0.42f,
      SizeMax = 0.68f,
      SizeStart = 0.5f,
      // The long lifetime stretches the same growth over more seconds, so it
      // never looks like it inflates.
      SizeGrowth = 3.8f,
      SizeEnd = 0.0f,
      SizePeak = 0.7f,
      // The point of the variant: it barely climbs. Any faster and it reads as
      // a fire again instead of something smouldering.
      SpeedMin = 0.18f,
      SpeedMax = 0.35f,
      // Nearly neutral. Strong buoyancy would accelerate the rise over that
      // long lifetime and undo the slow start.
      GravityModifier = -0.04f,
      Spawn = ParticleSpawn.Cone,
      // Wider than the others, which thins it further: the same few particles
      // spread over more volume instead of stacking along one axis.
      ConeAngle = 34.0f,
      SpawnRadius = 0.28f,
      RotationSpeed = 6.0f,
      NoiseStrength = 0.16f,
      NoiseFrequency = 0.16f,
      Palette = new[] { new Color(0.88f, 0.89f, 0.92f) },
      Ramp = Cooling,
      MaxParticles = 80,
    },
  };

  // Fine specks riding the same column: the fragments a fire throws off that
  // are too small to hold a shape. They stop a stack of large puffs from
  // reading as one soft mass, and they carry the sense of speed - the body is
  // slow enough that on its own the column looks like it is barely moving.
  static ParticleProfile MicroOf(ParticleProfile body) => new()
  {
    LayerName = "Micro",
    Sprite = body.Sprite,
    Draw = body.Draw,
    Looping = body.Looping,
    Duration = body.Duration,
    RateOverTime = body.RateOverTime * MicroRate,
    // Surges with the body rather than against it. Out of phase, the specks
    // fill the gaps and the swell disappears.
    PulseDepth = body.PulseDepth,
    LifetimeMin = body.LifetimeMin * MicroLifetime,
    LifetimeMax = body.LifetimeMax * MicroLifetime,
    SizeMin = body.SizeMin * MicroSize,
    SizeMax = body.SizeMax * MicroSize,
    SizeStart = body.SizeStart,
    // Barely grows. A speck that swells stops being a speck.
    SizeGrowth = 1.3f,
    SizeEnd = 0.0f,
    SizePeak = body.SizePeak,
    SpeedMin = body.SpeedMin * MicroSpeed,
    SpeedMax = body.SpeedMax * MicroSpeed,
    // Lighter than the body, so they keep rising after the mass has stalled.
    GravityModifier = body.GravityModifier * 1.4f,
    Drift = body.Drift * 1.3f,
    Spawn = body.Spawn,
    ConeAngle = body.ConeAngle * MicroSpread,
    // A tighter throat than the body, so they leave from inside it.
    SpawnRadius = body.SpawnRadius * 0.5f,
    RotationSpeed = body.RotationSpeed * 3.0f,
    NoiseStrength = body.NoiseStrength * MicroTurbulence,
    NoiseFrequency = body.NoiseFrequency * 2.0f,
    // A step lighter than the body so they stay legible against it. Cut out
    // rendering gives no transparency to separate them with, so the only
    // separation available is value.
    Palette = new[] { Brighten(body.Palette[0], 0.18f) },
    Ramp = body.Ramp,
    MaxParticles = body.MaxParticles * 2,
  };

  static Color Brighten(Color color, float amount) => new(
    Mathf.Clamp01(color.r + amount),
    Mathf.Clamp01(color.g + amount),
    Mathf.Clamp01(color.b + amount));
}
