using UnityEngine;

// A confetti cannon: one burst of paper that goes up, tumbles, and comes down.
//
// The only effect here drawn as real quads rather than billboards. It has to
// be: a billboard always faces the camera, so it can spin flat but never turn
// edge on, and edge on is exactly what makes a piece of paper read as paper
// rather than as a coloured dot. Tumbling on three axes is the entire effect.
public sealed class ConfettiCatalogue : IEffectCatalogue
{
  // Just long enough for the burst to be released. Nothing is emitted after it;
  // the effect lasts as long as the pieces do, not as long as this.
  private const float BurstSeconds = 0.2f;

  // Flat, saturated, and far enough apart in hue that no two read as the same
  // colour at the size these are drawn.
  //
  // Split across two sets for a reason that is not aesthetic: the random start
  // colour is picked along a Gradient, a Gradient holds eight colour keys, and
  // stepping between flat colours instead of blending through them costs two
  // keys each. Four per layer is the ceiling, so eight colours means two
  // layers. The alternative was a smooth gradient, which hands out every hue in
  // between and turns crisp paper into a muddy rainbow.
  private static readonly Color[] Warm =
  {
    new(0.95f, 0.26f, 0.31f),
    new(0.99f, 0.76f, 0.18f),
    new(0.98f, 0.45f, 0.6f),
    new(0.99f, 0.99f, 0.99f),
  };

  private static readonly Color[] Cool =
  {
    new(0.35f, 0.78f, 0.42f),
    new(0.25f, 0.6f, 0.95f),
    new(0.78f, 0.4f, 0.9f),
    new(0.3f, 0.86f, 0.83f),
  };

  public bool Contains(EffectId id) => id == EffectId.Confetti;

  public ParticleProfile[] LayersFor(EffectId id) =>
    Contains(id) ? new[] { Pieces("Warm", Warm), Pieces("Cool", Cool) } : null;

  static ParticleProfile Pieces(string layerName, Color[] palette) => new()
  {
    LayerName = layerName,
    Sprite = ParticleSprite.Confetti,
    Draw = ParticleDraw.Mesh,
    Looping = false,
    Duration = BurstSeconds,
    // No rate at all. A rate cannot put a hundred and ten pieces in the air in
    // one frame, and anything less than that does not read as a cannon.
    RateOverTime = 0.0f,
    BurstCount = 110,
    // A wide spread on purpose, here and in the size and speed below. Identical
    // pieces fall as a sheet however many there are; the chaos comes from them
    // disagreeing with each other, not from the count.
    LifetimeMin = 2.4f,
    LifetimeMax = 4.8f,
    SizeMin = 0.09f,
    SizeMax = 0.19f,
    SizeStart = 1.0f,
    SizeGrowth = 1.0f,
    SizeEnd = 0.0f,
    // Almost at the very end. Paper does not shrink as it falls, so the
    // collapse is squeezed into the last tenth of its life, where it reads as
    // the piece being gone rather than as it deflating on the way down.
    SizePeak = 0.92f,
    // Balanced against the gravity below rather than picked on its own: these
    // two decide the apex, and an apex above the camera means the audience
    // watches the confetti leave rather than fall past them.
    SpeedMin = 2.4f,
    SpeedMax = 6.5f,
    // Well under real gravity. There is no air drag here, so a realistic pull
    // would have the paper drop like gravel; a light one stands in for the
    // drag that would otherwise slow it.
    GravityModifier = 0.32f,
    Spawn = ParticleSpawn.Cone,
    ConeAngle = 55.0f,
    SpawnRadius = 0.12f,
    RotationSpeed = 340.0f,
    Tumbles = true,
    // Pushed hard. This reads as paper catching the air, and it is the single
    // biggest source of the mess: without it the pieces fall on parallel tracks
    // however many of them there are and however wide the cone.
    NoiseStrength = 0.95f,
    NoiseFrequency = 0.9f,
    Palette = palette,
    // Paper does not change colour on the way down.
    Ramp = null,
    MaxParticles = 120,
  };
}
