using UnityEngine;

// Turns a stack of ParticleProfiles into configured ParticleSystems.
//
// Built in code rather than authored as prefabs because a ParticleSystem
// serialises into hundreds of lines of YAML per variant, which is unreadable in
// a diff and impossible to keep consistent across a dozen looks. The factory
// calls this, so a variant is defined exactly once.
public static class ParticleEffectBuilder
{
  // A Gradient holds at most eight colour keys, and a palette needs two of them
  // per entry to step between colours instead of blending through them. More
  // colours than this means splitting the effect across another layer.
  private const int MaxPaletteColours = 4;

  // Half a key's width either side of a boundary, so the two colours meet in a
  // step rather than a ramp. Small enough that the blended sliver is narrower
  // than the sampling can land on in practice.
  private const float PaletteStep = 0.002f;

  private const float ExpansionSnap = 1.2f;

  // Built once and shared. Every confetti piece in the game is the same quad,
  // and Unity only needs it to have positions, UVs and a winding.
  private static Mesh quad;

  // One GameObject per effect, one child system per layer. Layers are separate
  // systems rather than sub emitters because they differ in almost every
  // module, and a sub emitter inherits far too much of its parent for that.
  //
  // Returns the root rather than the systems: the caller moves, parents and
  // scales the effect as one object, and can still reach the systems through it.
  public static GameObject Build(string name, ParticleProfile[] layers, ParticleMaterialTable materials, Transform parent)
  {
    GameObject root = new(name);

    root.transform.SetParent(parent, false);
    root.transform.localPosition = Vector3.zero;

    // Without this the first lookup below throws, and a NullReferenceException
    // out of a builder says nothing about a table that was never assigned. The
    // effect is still returned, empty, so one missing asset does not take the
    // rest of the scene down with it.
    if (materials == null)
    {
      Debug.LogError($"No particle material table was given, so {name} cannot be built. Assign one on the {nameof(GameManager)}.");
      return root;
    }

    Material trailMaterial = materials.For(ParticleSprite.Trail);

    for (int index = 0; index < layers.Length; index++)
    {
      ParticleProfile layer = layers[index];
      Material material = materials.For(layer.Sprite);

      // Checked separately from the sprite, because a trail is a second draw
      // with a second material and would otherwise go magenta on its own with
      // the particle it belongs to looking perfectly fine.
      if (layer.TrailRatio > 0.0f && trailMaterial == null)
      {
        Debug.LogError($"No material is assigned for the {ParticleSprite.Trail} sprite, so the trails on layer {layer.LayerName} of {name} will render magenta. Assign it on the particle material table.");
      }

      // Worth shouting about. A null material is drawn in magenta, exactly like
      // a shader that failed to compile, so the obvious reading of the screen
      // sends you off looking for a compile error that was never there.
      if (material == null)
      {
        Debug.LogError($"No material is assigned for the {layer.Sprite} sprite, so layer {layer.LayerName} of {name} will render magenta. Assign it on the {nameof(GameManager)}.");
      }

      BuildLayer(layer, material, trailMaterial, root.transform, index);
    }

    return root;
  }

  static void BuildLayer(ParticleProfile profile, Material material, Material trailMaterial, Transform parent, int order)
  {
    GameObject host = new(profile.LayerName);

    host.transform.SetParent(parent, false);
    host.transform.localPosition = Vector3.zero;

    ParticleSystem system = host.AddComponent<ParticleSystem>();

    // AddComponent starts the system playing straight away, and duration is one
    // of the fields Unity refuses to change while it runs. Clearing rather than
    // merely pausing matters too: the check is against particles still being
    // alive, not just against the emitter.
    system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

    // Every module is fetched, mutated and dropped. They are structs wrapping a
    // pointer to the native system, so writing to the local copy writes through.
    Configure(system, profile);
    ConfigureRenderer(system.GetComponent<ParticleSystemRenderer>(), profile, material, trailMaterial, order);

    // Deliberately left stopped. Whoever owns the effect decides when it starts,
    // which is the difference between a pooled instance waiting to be used and
    // one quietly emitting into a corner of the scene.
  }

  static void Configure(ParticleSystem system, ParticleProfile profile)
  {
    ParticleSystem.MainModule main = system.main;

    main.duration = profile.Duration;
    main.loop = profile.Looping;
    main.startLifetime = new ParticleSystem.MinMaxCurve(profile.LifetimeMin, profile.LifetimeMax);
    main.startSize = new ParticleSystem.MinMaxCurve(profile.SizeMin, profile.SizeMax);
    main.startSpeed = new ParticleSystem.MinMaxCurve(profile.SpeedMin, profile.SpeedMax);
    main.startColor = StartColour(profile);
    main.gravityModifier = profile.GravityModifier;
    main.maxParticles = profile.MaxParticles;
    // World space: an effect is left behind by whatever emitted it. Local space
    // drags the whole thing along with the emitter and looks glued to it.
    main.simulationSpace = ParticleSystemSimulationSpace.World;
    // Off, or a pooled instance would restart itself the moment it is enabled,
    // emitting a frame at wherever it was last used.
    main.playOnAwake = false;
    // Hierarchy rather than Local, so scaling the effect root scales the shape
    // and the particles with it.
    main.scalingMode = ParticleSystemScalingMode.Hierarchy;

    ConfigureStartRotation(main, profile);
    ConfigureEmission(system, profile);
    ConfigureShape(system, profile);
    ConfigureSize(system, profile);
    ConfigureColour(system, profile);
    ConfigureRotation(system, profile);
    ConfigureVelocity(system, profile);
    ConfigureNoise(system, profile);
    ConfigureTrails(system, profile);
  }

  static void ConfigureTrails(ParticleSystem system, ParticleProfile profile)
  {
    ParticleSystem.TrailModule trails = system.trails;

    trails.enabled = profile.TrailRatio > 0.0f;

    if (!trails.enabled) return;

    trails.mode = ParticleSystemTrailMode.PerParticle;
    trails.ratio = profile.TrailRatio;
    trails.lifetime = new ParticleSystem.MinMaxCurve(profile.TrailLifetime);
    // Vertices are laid down by distance travelled, not by time, so a fast
    // particle does not get a coarser ribbon than a slow one.
    trails.minVertexDistance = 0.05f;
    // The head is already drawn as a particle. Without this the ribbon carries
    // on after the spark is gone and reads as a stray line.
    trails.dieWithParticles = true;
    // Width comes from the profile in world units instead of being scaled by
    // the particle, so a big sparkle does not drag a fat band behind it.
    trails.sizeAffectsWidth = false;
    trails.widthOverTrail = new ParticleSystem.MinMaxCurve(profile.TrailWidth, Taper());
    // The ribbon should be the same colour as the spark that laid it, including
    // the cooling it goes through.
    trails.inheritParticleColor = true;
  }

  // Full width at one end, nothing at the other. This is the only exit a trail
  // has: the material is cut out, so fading its alpha would make it vanish in
  // one step instead of thinning out.
  static AnimationCurve Taper() => new(
    new Keyframe(0.0f, 1.0f, 0.0f, 0.0f),
    new Keyframe(1.0f, 0.0f, -1.4f, 0.0f));

  // Randomised, and that is not decoration: without it every particle is the
  // same stamp at the same angle, and a stack of them reads as one repeated
  // drawing. Radians, not degrees.
  static void ConfigureStartRotation(ParticleSystem.MainModule main, ParticleProfile profile)
  {
    ParticleSystem.MinMaxCurve turn = new(0.0f, Mathf.PI * 2.0f);

    if (!profile.Tumbles)
    {
      main.startRotation = turn;
      return;
    }

    main.startRotation3D = true;
    main.startRotationX = turn;
    main.startRotationY = turn;
    main.startRotationZ = turn;
  }

  static void ConfigureEmission(ParticleSystem system, ParticleProfile profile)
  {
    ParticleSystem.EmissionModule emission = system.emission;

    if (profile.BurstCount > 0)
    {
      // At time zero of the cycle. On a looping system this repeats every
      // duration; on a one shot it is the whole effect.
      emission.SetBursts(new[] { new ParticleSystem.Burst(0.0f, profile.BurstCount) });
    }

    if (profile.PulseDepth <= 0.0f)
    {
      emission.rateOverTime = profile.RateOverTime;
      return;
    }

    emission.rateOverTime = new ParticleSystem.MinMaxCurve(profile.RateOverTime, PulseCurve(profile.PulseDepth));
  }

  // A curve on rateOverTime is sampled across the system's duration and repeats
  // with the loop, so the wave has to end on the value it started at or every
  // cycle boundary shows up as a jolt in the emission.
  //
  // Keys are left with flat tangents, which is what turns the alternating
  // trough and peak into a smooth swell rather than a triangle wave.
  static AnimationCurve PulseCurve(float depth)
  {
    // Swells per loop. Two over a four second duration puts a surge every two
    // seconds, which is slow enough to read as the fire catching and fast
    // enough that you do not wait around for it.
    const int PulsesPerCycle = 2;

    float trough = Mathf.Clamp01(1.0f - depth);

    AnimationCurve curve = new();

    int steps = PulsesPerCycle * 2;

    for (int step = 0; step <= steps; step++)
    {
      curve.AddKey(new Keyframe(step / (float)steps, step % 2 == 0 ? trough : 1.0f));
    }

    return curve;
  }

  static void ConfigureShape(ParticleSystem system, ParticleProfile profile)
  {
    ParticleSystem.ShapeModule shape = system.shape;

    shape.enabled = true;
    shape.shapeType = profile.Spawn == ParticleSpawn.Sphere
      ? ParticleSystemShapeType.Sphere
      : ParticleSystemShapeType.Cone;
    shape.angle = profile.ConeAngle;
    shape.radius = profile.SpawnRadius;
    // Emitting from the whole volume rather than the rim, so the effect has a
    // filled centre instead of a visible hole.
    shape.radiusThickness = 1.0f;
  }

  static void ConfigureSize(ParticleSystem system, ParticleProfile profile)
  {
    ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;

    size.enabled = true;

    float peak = Mathf.Clamp(profile.SizePeak, 0.05f, 0.95f);

    // Tangents follow the slope of each segment rather than being written per
    // profile, so a peak moved early or late keeps its shape instead of needing
    // the curve retuned by hand. The snap factor overshoots the linear slope
    // slightly, which is how a real puff behaves: it pushes into still air
    // hardest at the moment it is released.
    float rise = (profile.SizeGrowth - profile.SizeStart) / peak * ExpansionSnap;
    float fall = (profile.SizeEnd - profile.SizeGrowth) / (1.0f - peak);

    AnimationCurve curve = new(
      new Keyframe(0.0f, profile.SizeStart, 0.0f, rise),
      new Keyframe(peak, profile.SizeGrowth, 0.0f, 0.0f),
      new Keyframe(1.0f, profile.SizeEnd, fall, 0.0f));

    size.size = new ParticleSystem.MinMaxCurve(1.0f, curve);
  }

  // The colour a particle is born as. More than one entry and Unity picks at
  // random along a gradient, so the gradient is built as steps: a smooth one
  // would hand out every hue in between, which turns four flat paper colours
  // into a muddy rainbow.
  static ParticleSystem.MinMaxGradient StartColour(ParticleProfile profile)
  {
    Color[] palette = profile.Palette;

    if (palette == null || palette.Length == 0) return new ParticleSystem.MinMaxGradient(Color.white);
    if (palette.Length == 1) return new ParticleSystem.MinMaxGradient(palette[0]);

    int count = Mathf.Min(palette.Length, MaxPaletteColours);

    if (palette.Length > MaxPaletteColours)
    {
      Debug.LogWarning($"Layer {profile.LayerName} lists {palette.Length} palette colours; only the first {MaxPaletteColours} fit in a gradient. Split the extras into another layer.");
    }

    GradientColorKey[] colours = new GradientColorKey[count * 2];

    for (int index = 0; index < count; index++)
    {
      float start = index / (float)count;
      float end = (index + 1) / (float)count;

      colours[index * 2] = new GradientColorKey(palette[index], index == 0 ? 0.0f : start + PaletteStep);
      colours[index * 2 + 1] = new GradientColorKey(palette[index], index == count - 1 ? 1.0f : end - PaletteStep);
    }

    Gradient gradient = new();

    gradient.SetKeys(colours, OpaqueAlpha());

    return new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };
  }

  // Multiplied over the particle's life, so it shades whatever the palette gave
  // it rather than replacing it.
  static void ConfigureColour(ParticleSystem system, ParticleProfile profile)
  {
    if (profile.Ramp == null || profile.Ramp.Length < 2) return;

    ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;

    colour.enabled = true;

    Gradient gradient = new();

    gradient.SetKeys(profile.Ramp, OpaqueAlpha());

    colour.color = new ParticleSystem.MinMaxGradient(gradient);
  }

  // Alpha stays pinned at full for the whole life. The material discards
  // anything under its cutoff, so every intermediate value would be a cliff
  // rather than a ramp: the particle would blink out the instant the curve
  // crossed the threshold. Disappearing is the size curve's job.
  static GradientAlphaKey[] OpaqueAlpha() => new[]
  {
    new GradientAlphaKey(1.0f, 0.0f),
    new GradientAlphaKey(1.0f, 1.0f),
  };

  static void ConfigureRotation(ParticleSystem system, ParticleProfile profile)
  {
    ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;

    rotation.enabled = profile.RotationSpeed > 0.0f;

    if (!rotation.enabled) return;

    float speed = profile.RotationSpeed * Mathf.Deg2Rad;

    // Symmetric range so roughly half turn each way. All one way and the whole
    // effect reads as a single rotating object.
    ParticleSystem.MinMaxCurve turn = new(-speed, speed);

    if (!profile.Tumbles)
    {
      rotation.z = turn;
      return;
    }

    rotation.separateAxes = true;
    rotation.x = turn;
    rotation.y = turn;
    rotation.z = turn;
  }

  static void ConfigureVelocity(ParticleSystem system, ParticleProfile profile)
  {
    ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;

    velocity.enabled = profile.Drift != Vector3.zero;

    if (!velocity.enabled) return;

    // Ramps in rather than applying flat: a particle leaving the vent has not
    // caught the wind yet, and starting it already drifting shears the base of
    // the effect off sideways.
    AnimationCurve rampIn = new(
      new Keyframe(0.0f, 0.0f, 0.0f, 0.0f),
      new Keyframe(1.0f, 1.0f, 1.2f, 1.2f));

    // World space, because wind does not care which way the emitter is facing.
    velocity.space = ParticleSystemSimulationSpace.World;
    velocity.x = new ParticleSystem.MinMaxCurve(profile.Drift.x, rampIn);
    velocity.y = new ParticleSystem.MinMaxCurve(profile.Drift.y, rampIn);
    velocity.z = new ParticleSystem.MinMaxCurve(profile.Drift.z, rampIn);
  }

  static void ConfigureNoise(ParticleSystem system, ParticleProfile profile)
  {
    ParticleSystem.NoiseModule noise = system.noise;

    noise.enabled = profile.NoiseStrength > 0.0f;

    if (!noise.enabled) return;

    noise.strength = profile.NoiseStrength;
    noise.frequency = profile.NoiseFrequency;
    noise.scrollSpeed = 0.3f;
    // 1D would push every particle the same way at a given point in the field,
    // which curls the effect as one sheet.
    noise.quality = ParticleSystemNoiseQuality.Medium;
    noise.damping = true;
  }

  static void ConfigureRenderer(ParticleSystemRenderer renderer, ParticleProfile profile, Material material, Material trailMaterial, int order)
  {
    // Its own material, and it has to be. A trail repeats whatever texture it
    // is given along its length, so handing it the particle's sprite draws a
    // row of little stars instead of a streak.
    if (profile.TrailRatio > 0.0f) renderer.trailMaterial = trailMaterial;

    // Layers share a queue, so the only thing separating them is this. First
    // layer at the back, each one after it in front.
    renderer.sortingOrder = order;
    renderer.sharedMaterial = material;
    // Back to front within the system, or overlapping quads resolve in whatever
    // order they were spawned and the effect flickers as it turns.
    renderer.sortMode = ParticleSystemSortMode.Distance;
    // Unlit particles casting or receiving shadows is a contradiction, and in
    // HDRP it costs a full extra pass per system.
    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    renderer.receiveShadows = false;

    switch (profile.Draw)
    {
      case ParticleDraw.Stretched:
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = profile.StretchScale;
        // The sprite is already round, so it needs no stretching of its own on
        // top of what its velocity gives it.
        renderer.lengthScale = 1.0f;
        // Camera motion must not lengthen the streak, or the whole burst grows
        // a tail every time the player turns.
        renderer.cameraVelocityScale = 0.0f;
        break;

      case ParticleDraw.Mesh:
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = Quad();
        // The particle's own rotation is the orientation here, not the camera.
        // That is the whole reason for a mesh: a billboard would keep turning
        // to face the viewer and never show the quad edge on.
        //
        // The back of that quad has to be drawn too, or half the confetti
        // blinks out mid tumble. That comes from the material, which culls
        // nothing, and not from anything settable here.
        renderer.alignment = ParticleSystemRenderSpace.Local;
        break;

      default:
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        // Half the particles get mirrored on each axis, which multiplies one
        // sprite into four silhouettes for free.
        renderer.flip = new Vector3(0.5f, 0.5f, 0.0f);
        break;
    }
  }

  // Two triangles, unit sized and centred, wound so the front face points at
  // negative Z the way Unity's own quad does.
  static Mesh Quad()
  {
    if (quad != null) return quad;

    quad = new Mesh { name = "Particle Quad" };

    quad.SetVertices(new[]
    {
      new Vector3(-0.5f, -0.5f, 0.0f),
      new Vector3(0.5f, -0.5f, 0.0f),
      new Vector3(0.5f, 0.5f, 0.0f),
      new Vector3(-0.5f, 0.5f, 0.0f),
    });

    quad.SetUVs(0, new[]
    {
      new Vector2(0.0f, 0.0f),
      new Vector2(1.0f, 0.0f),
      new Vector2(1.0f, 1.0f),
      new Vector2(0.0f, 1.0f),
    });

    quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
    quad.RecalculateNormals();
    quad.RecalculateBounds();

    return quad;
  }
}
