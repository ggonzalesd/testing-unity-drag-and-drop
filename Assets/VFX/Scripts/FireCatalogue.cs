using UnityEngine;

// Cartoon fire: three flame layers nested inside each other, plus embers.
//
// The layering is the whole technique, and it is the one that suits a cut out
// material best. Real fire is drawn with additive transparency, which this
// shader deliberately does not do; a cel painted flame instead shows its heat
// as bands of flat colour. Each band is its own layer, smaller, faster and
// shorter lived than the one behind it, so the core is always inside the body
// and the body inside the outer edge without anything having to sort them per
// particle.
//
// Every layer is born white hot and cools on its own clock. That is the second
// half of the technique and it is what puts the colour where it belongs: red is
// not a property of the outer layer, it is what any particle turns into if it
// lives long enough, and only the outer layer lives that long. So the base of
// the fire comes out white, the middle orange and the tips red, from four
// layers that all start at the same colour.
public sealed class FireCatalogue : IEffectCatalogue
{
  private const float CycleSeconds = 1.0f;

  // Barely tinted. The heat is carried by the ramps below; if these were
  // already coloured, every particle would start at its band's colour and the
  // white hot base would be gone.
  private static readonly Color[] WhiteHot = { new(1.0f, 0.99f, 0.95f) };

  public bool Contains(EffectId id) => id == EffectId.Fire;

  public ParticleProfile[] LayersFor(EffectId id)
  {
    if (!Contains(id)) return null;

    return new[] { Outer(), Body(), Core(), Embers() };
  }

  // Widest and coolest, drawn behind everything else. Its taper is what gives
  // the fire its outline, so it is the layer worth tuning first.
  static ParticleProfile Outer() => new()
  {
    LayerName = "Outer",
    Sprite = ParticleSprite.Circle,
    Draw = ParticleDraw.Billboard,
    Looping = true,
    Duration = CycleSeconds,
    RateOverTime = 30.0f,
    LifetimeMin = 0.6f,
    LifetimeMax = 0.95f,
    SizeMin = 0.5f,
    SizeMax = 0.72f,
    SizeStart = 0.75f,
    SizeGrowth = 1.1f,
    SizeEnd = 0.0f,
    // Early, unlike smoke. A flame is at its widest near the base and spends
    // most of its life narrowing, which is what makes the tip look like a tip.
    SizePeak = 0.3f,
    SpeedMin = 1.2f,
    SpeedMax = 1.8f,
    GravityModifier = -0.4f,
    Spawn = ParticleSpawn.Cone,
    ConeAngle = 14.0f,
    SpawnRadius = 0.16f,
    // A circle has no orientation to see, so spinning it would be work for
    // nothing. Flicker comes from the noise instead.
    RotationSpeed = 0.0f,
    // High frequency on purpose. Smoke wants slow curl; fire wants a flicker
    // fast enough to read as combustion.
    NoiseStrength = 0.35f,
    NoiseFrequency = 1.2f,
    Palette = WhiteHot,
    // The longest lived layer, so it is the only one that gets all the way to
    // red. Holding white for the first fifth is what stops the whole fire from
    // turning red the instant it leaves the ground.
    Ramp = new GradientColorKey[]
    {
      new(Color.white, 0.0f),
      new(new Color(1.0f, 0.9f, 0.5f), 0.2f),
      new(new Color(1.0f, 0.45f, 0.05f), 0.55f),
      new(new Color(0.9f, 0.08f, 0.02f), 1.0f),
    },
    MaxParticles = 60,
  };

  static ParticleProfile Body() => new()
  {
    LayerName = "Body",
    Sprite = ParticleSprite.Circle,
    Draw = ParticleDraw.Billboard,
    Looping = true,
    Duration = CycleSeconds,
    RateOverTime = 34.0f,
    LifetimeMin = 0.45f,
    LifetimeMax = 0.7f,
    SizeMin = 0.34f,
    SizeMax = 0.5f,
    SizeStart = 0.8f,
    SizeGrowth = 1.05f,
    SizeEnd = 0.0f,
    SizePeak = 0.3f,
    // Faster and shorter lived than the outer layer, which is what keeps it
    // inside it: it never has time to reach the same height.
    SpeedMin = 1.5f,
    SpeedMax = 2.2f,
    GravityModifier = -0.5f,
    Spawn = ParticleSpawn.Cone,
    ConeAngle = 11.0f,
    SpawnRadius = 0.11f,
    RotationSpeed = 0.0f,
    NoiseStrength = 0.4f,
    NoiseFrequency = 1.5f,
    Palette = WhiteHot,
    // Shorter lived than the outer layer, so it never reaches the same red.
    Ramp = new GradientColorKey[]
    {
      new(Color.white, 0.0f),
      new(new Color(1.0f, 0.96f, 0.62f), 0.3f),
      new(new Color(1.0f, 0.62f, 0.06f), 0.68f),
      new(new Color(1.0f, 0.2f, 0.02f), 1.0f),
    },
    MaxParticles = 55,
  };

  static ParticleProfile Core() => new()
  {
    LayerName = "Core",
    Sprite = ParticleSprite.Circle,
    Draw = ParticleDraw.Billboard,
    Looping = true,
    Duration = CycleSeconds,
    RateOverTime = 26.0f,
    LifetimeMin = 0.3f,
    LifetimeMax = 0.5f,
    SizeMin = 0.2f,
    SizeMax = 0.3f,
    SizeStart = 0.85f,
    SizeGrowth = 1.0f,
    SizeEnd = 0.0f,
    SizePeak = 0.25f,
    SpeedMin = 1.8f,
    SpeedMax = 2.6f,
    GravityModifier = -0.6f,
    Spawn = ParticleSpawn.Cone,
    ConeAngle = 8.0f,
    SpawnRadius = 0.07f,
    RotationSpeed = 0.0f,
    NoiseStrength = 0.45f,
    NoiseFrequency = 1.8f,
    Palette = WhiteHot,
    // Dies too young to cool past yellow, which is exactly why the middle of
    // the fire stays bright.
    Ramp = new GradientColorKey[]
    {
      new(Color.white, 0.0f),
      new(new Color(1.0f, 1.0f, 0.9f), 0.5f),
      new(new Color(1.0f, 0.88f, 0.3f), 1.0f),
    },
    MaxParticles = 40,
  };

  // The one layer that is not a flat band of flame. Sparse and erratic, and the
  // only part of the fire that leaves the silhouette, which is what stops the
  // whole thing from reading as a static painted shape.
  static ParticleProfile Embers() => new()
  {
    LayerName = "Embers",
    Sprite = ParticleSprite.Spark,
    Draw = ParticleDraw.Billboard,
    Looping = true,
    Duration = CycleSeconds,
    RateOverTime = 7.0f,
    LifetimeMin = 0.9f,
    LifetimeMax = 1.6f,
    SizeMin = 0.05f,
    SizeMax = 0.09f,
    SizeStart = 1.0f,
    SizeGrowth = 1.0f,
    SizeEnd = 0.0f,
    SizePeak = 0.8f,
    SpeedMin = 1.6f,
    SpeedMax = 2.8f,
    GravityModifier = -0.35f,
    Spawn = ParticleSpawn.Cone,
    ConeAngle = 24.0f,
    SpawnRadius = 0.1f,
    // Worth spinning here: unlike the circle, the spark sprite has points, so
    // its orientation is visible.
    RotationSpeed = 120.0f,
    NoiseStrength = 0.9f,
    NoiseFrequency = 1.6f,
    Palette = WhiteHot,
    Ramp = new GradientColorKey[]
    {
      new(Color.white, 0.0f),
      new(new Color(1.0f, 0.92f, 0.55f), 0.3f),
      new(new Color(1.0f, 0.4f, 0.05f), 1.0f),
    },
    MaxParticles = 30,
  };
}
