using UnityEngine;

// The one place a sprite name becomes an asset. Kept out of the profiles so
// that a look can be written as data without reaching for the project window.
//
// An asset rather than a block of fields on the GameManager, and that is not a
// style choice. A scene that Unity already has open is not re-read when the
// file changes underneath it, so every sprite added here used to mean a stale
// scene and a set of null materials that render magenta. An asset is reimported
// the moment it changes, so the scene holds one reference that never has to
// move again and new sprites land without touching it.
//
// A class with named fields rather than an array of pairs: the sprites are a
// closed set, and named fields mean the inspector cannot be left with a missing
// entry or two rows claiming the same sprite.
[CreateAssetMenu(fileName = "ParticleMaterials", menuName = "VFX/Particle Material Table")]
public sealed class ParticleMaterialTable : ScriptableObject
{
  [SerializeField]
  [Tooltip("Soft round blob. Used by smoke and by the flame bands.")]
  private Material Circle;
  [SerializeField]
  [Tooltip("Rounded rectangle drawn as a real quad, so it can turn edge on.")]
  private Material Confetti;
  [SerializeField]
  [Tooltip("Four pointed sparkle. Used by embers and by the firework heads.")]
  private Material Spark;
  [SerializeField]
  [Tooltip("Five pointed star. Used by the firework twinkles.")]
  private Material Star;
  [SerializeField]
  [Tooltip("Plain white, no texture. Used for particle trails, which repeat their texture along their length.")]
  private Material Trail;

  public Material For(ParticleSprite sprite) => sprite switch
  {
    ParticleSprite.Confetti => Confetti,
    ParticleSprite.Spark => Spark,
    ParticleSprite.Star => Star,
    ParticleSprite.Trail => Trail,
    _ => Circle,
  };
}
