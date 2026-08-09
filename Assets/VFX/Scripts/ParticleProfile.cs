using UnityEngine;

// Which drawing a layer stamps. An enum rather than a Material reference,
// because a profile is data that describes a look and has no business holding
// an asset: the factory owns the table that turns one of these into a material.
public enum ParticleSprite
{
  Circle,
  Confetti,
  // Four pointed sparkle.
  Spark,
  // Five pointed star, rounded.
  Star,
  // No drawing at all, just white. What a trail wants: the texture would
  // otherwise be repeated along its length, and a row of little stars is not a
  // streak.
  Trail,
}

// Where particles are born.
public enum ParticleSpawn
{
  // Directed. Angle opens the mouth, radius sets how wide the base is.
  Cone,
  // Every direction at once, which is what a burst wants. Angle is ignored.
  Sphere,
}

// How a particle is put on screen.
public enum ParticleDraw
{
  // Flat quad facing the camera.
  Billboard,
  // Billboard smeared along its own velocity. Turns a dot into a streak, which
  // is the whole read of a spark.
  Stretched,
  // A real quad in the world, so it can tumble on all three axes. Costs more
  // than a billboard and is only worth it when the tumble is the point.
  Mesh,
}

// Everything that separates one layer of an effect from another, with no
// reference to a ParticleSystem. Plain data, so a look can be read, diffed and
// tuned without touching the code that talks to Unity.
//
// An effect is a stack of these rather than a single one. Smoke reads as smoke
// because coarse puffs and fine specks move at different speeds through the
// same space; fire reads as fire because three differently coloured layers sit
// inside each other. One layer trying to do both jobs just looks like a blur.
public sealed class ParticleProfile
{
  public string LayerName;
  public ParticleSprite Sprite;
  public ParticleDraw Draw;
  // Metres of streak per metre per second of speed. Only read when stretched.
  public float StretchScale;

  public bool Looping;
  public float Duration;

  public float RateOverTime;
  // How far the emission drops at the bottom of its cycle, as a fraction of the
  // rate above. 0 holds steady; 0.8 nearly stops between surges. The rate is
  // then the peak rather than the average, so raising this thins the effect.
  public float PulseDepth;
  // Particles released in one go at the start of each cycle. This is what makes
  // a burst a burst: a rate cannot produce a hundred particles in one frame.
  public int BurstCount;

  public float LifetimeMin;
  public float LifetimeMax;

  public float SizeMin;
  public float SizeMax;
  // The size curve as three numbers, all multipliers on the start size.
  // Fraction at birth, the multiplier reached at the peak, and the fraction
  // left at death. Ending at 0 is how anything vanishes here: the material is
  // cut out, so there is no half transparent pixel to fade through.
  public float SizeStart;
  public float SizeGrowth;
  public float SizeEnd;
  // Where in the particle's life the peak sits. Late for smoke, so it holds its
  // size and the shrink reads as going away; very late for confetti, which
  // should keep its size until it is gone.
  public float SizePeak;

  public float SpeedMin;
  public float SpeedMax;
  public float GravityModifier;
  // Constant push in world space. Wind, essentially: what bends an effect over
  // instead of merely making it wobble.
  public Vector3 Drift;

  public ParticleSpawn Spawn;
  public float ConeAngle;
  public float SpawnRadius;

  // Degrees per second, applied in both directions so the effect does not read
  // as one object turning.
  public float RotationSpeed;
  // Tumble on all three axes instead of spinning flat. Needs a mesh draw: a
  // billboard has no third axis to turn on.
  public bool Tumbles;

  public float NoiseStrength;
  public float NoiseFrequency;

  // Colours a particle can be born as, picked at random per particle. One entry
  // means they are all the same colour.
  public Color[] Palette;
  // Multiplied over the particle's life, so it is a shading of whatever the
  // palette gave it rather than a replacement. Fewer than two entries means no
  // change at all.
  //
  // Carries its own key times rather than being spaced evenly, because where a
  // colour change sits matters more than which colours it passes through: a
  // flame that reaches red a third of the way through its life reads as embers,
  // and the same three colours held white until halfway read as fire.
  public GradientColorKey[] Ramp;

  // Ribbon left behind each particle. Ratio is the fraction of particles that
  // get one, from 0 for none to 1 for all. Width is in world units and is
  // tapered to nothing along the ribbon: with a cut out material there is no
  // alpha to fade a trail out with, so narrowing it is the only exit.
  public float TrailRatio;
  public float TrailLifetime;
  public float TrailWidth;

  public int MaxParticles;
}
