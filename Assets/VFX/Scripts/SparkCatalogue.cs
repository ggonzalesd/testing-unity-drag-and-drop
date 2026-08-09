using UnityEngine;

// Celebration sparks: a firework going off rather than metal being ground.
//
// Two layers doing two different jobs. The comets carry the shape of the burst,
// and they carry it with a trail rather than by being stretched: a stretched
// sprite smears the drawing itself, which turns a four pointed sparkle into a
// blurred lozenge and throws away the reason for using it. A trail leaves the
// head intact and draws the streak as its own ribbon behind it.
//
// The twinkles are the punctuation. Big five pointed stars, few enough to be
// looked at one at a time, spinning where the comets are already pointing.
public sealed class SparkCatalogue : IEffectCatalogue
{
  private const float BurstSeconds = 0.15f;

  public bool Contains(EffectId id) => id == EffectId.SparkBurst;

  public ParticleProfile[] LayersFor(EffectId id) => Contains(id) ? new[] { Dust(), Comets(), Twinkles() } : null;

  // The filler, drawn behind the other two. Small, plentiful and spread over a
  // much wider speed range than anything else here, which is what gives the
  // burst depth: the slow ones are still near the centre while the fast ones
  // are already at the edge, so the eye reads a volume rather than a ring.
  //
  // Nothing in this layer is meant to be looked at individually. It is the
  // reason the gaps between the comets do not read as empty.
  static ParticleProfile Dust() => new()
  {
    LayerName = "Dust",
    Sprite = ParticleSprite.Spark,
    Draw = ParticleDraw.Billboard,
    Looping = false,
    Duration = BurstSeconds,
    RateOverTime = 0.0f,
    BurstCount = 110,
    LifetimeMin = 0.35f,
    LifetimeMax = 0.9f,
    SizeMin = 0.05f,
    SizeMax = 0.1f,
    SizeStart = 0.8f,
    SizeGrowth = 1.0f,
    SizeEnd = 0.0f,
    SizePeak = 0.4f,
    // Deliberately enormous as a range. This one number is what turns a shell
    // of sparks into a filled ball.
    SpeedMin = 1.5f,
    SpeedMax = 11.0f,
    GravityModifier = 0.8f,
    Spawn = ParticleSpawn.Sphere,
    SpawnRadius = 0.05f,
    RotationSpeed = 180.0f,
    NoiseStrength = 0.0f,
    Palette = new[] { new Color(1.0f, 0.96f, 0.82f) },
    Ramp = new GradientColorKey[]
    {
      new(Color.white, 0.0f),
      new(new Color(1.0f, 0.7f, 0.2f), 0.5f),
      new(new Color(0.9f, 0.25f, 0.04f), 1.0f),
    },
    MaxParticles = 130,
  };

  static ParticleProfile Comets() => new()
  {
    LayerName = "Comets",
    Sprite = ParticleSprite.Spark,
    Draw = ParticleDraw.Billboard,
    Looping = false,
    Duration = BurstSeconds,
    RateOverTime = 0.0f,
    BurstCount = 60,
    LifetimeMin = 0.8f,
    LifetimeMax = 1.4f,
    // Several times the old size. At the previous scale the sprite was a dot
    // and the shape you picked out was invisible.
    SizeMin = 0.18f,
    SizeMax = 0.3f,
    SizeStart = 0.5f,
    SizeGrowth = 1.0f,
    SizeEnd = 0.0f,
    SizePeak = 0.3f,
    SpeedMin = 5.0f,
    SpeedMax = 9.0f,
    // Heavy, and deliberately so. Sparks that arc over and fall are a firework;
    // sparks that fly straight are an explosion.
    GravityModifier = 0.9f,
    // Every direction at once. A cone would give the burst a front, and a
    // firework has none.
    Spawn = ParticleSpawn.Sphere,
    SpawnRadius = 0.05f,
    // Slow. The sprite has points, so its angle is visible, and spinning it
    // fast turns the head into a flicker instead of a shape.
    RotationSpeed = 60.0f,
    NoiseStrength = 0.0f,
    Palette = new[] { new Color(1.0f, 0.98f, 0.88f) },
    // White hot into gold into a dull red. Cooling is what makes a spark look
    // like it is burning out rather than being switched off. The trail inherits
    // this, so the ribbon cools along with the head that drew it.
    Ramp = new GradientColorKey[]
    {
      new(Color.white, 0.0f),
      new(new Color(1.0f, 0.8f, 0.25f), 0.35f),
      new(new Color(1.0f, 0.35f, 0.05f), 0.75f),
      new(new Color(0.75f, 0.1f, 0.03f), 1.0f),
    },
    // Not all of them. Every comet trailing reads as a solid ball of ribbon;
    // two in three leaves gaps for the eye to find the individual sparks in.
    TrailRatio = 0.65f,
    // Well under the particle's own life, so the ribbon is a streak behind a
    // moving head rather than a record of its whole flight.
    TrailLifetime = 0.28f,
    TrailWidth = 0.05f,
    MaxParticles = 80,
  };

  static ParticleProfile Twinkles() => new()
  {
    LayerName = "Twinkles",
    Sprite = ParticleSprite.Star,
    Draw = ParticleDraw.Billboard,
    Looping = false,
    Duration = BurstSeconds,
    RateOverTime = 0.0f,
    // Far fewer than the comets. These are the ones you actually look at, and a
    // screen full of them is glitter, not a firework.
    BurstCount = 22,
    LifetimeMin = 0.6f,
    LifetimeMax = 1.1f,
    SizeMin = 0.32f,
    SizeMax = 0.5f,
    // Grows in from small, which reads as the flash of the star igniting.
    SizeStart = 0.35f,
    SizeGrowth = 1.0f,
    SizeEnd = 0.0f,
    SizePeak = 0.35f,
    // Slower than the comets, so they stay near the middle of the burst where
    // the comets are already pointing.
    SpeedMin = 2.5f,
    SpeedMax = 5.0f,
    GravityModifier = 0.6f,
    Spawn = ParticleSpawn.Sphere,
    SpawnRadius = 0.05f,
    RotationSpeed = 110.0f,
    NoiseStrength = 0.0f,
    Palette = new[] { new Color(1.0f, 0.97f, 0.85f) },
    Ramp = new GradientColorKey[]
    {
      new(Color.white, 0.0f),
      new(new Color(1.0f, 0.9f, 0.5f), 0.45f),
      new(new Color(1.0f, 0.6f, 0.15f), 1.0f),
    },
    MaxParticles = 40,
  };
}
