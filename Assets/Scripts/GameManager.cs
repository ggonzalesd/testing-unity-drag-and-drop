using UnityEngine;

// Composition root. The one place that knows which factories exist and what
// assets they need, so everything downstream asks for an effect by id and stays
// ignorant of how it gets built.
//
// A singleton because effects are requested from collisions, input and physics
// callbacks, none of which have anywhere sensible to hold a reference. Scene
// scoped rather than DontDestroyOnLoad: the effects it owns are made of scene
// objects, and carrying them across a load would leave them pointing at a scene
// that no longer exists.
public class GameManager : MonoBehaviour
{
  [SerializeField]
  [Tooltip("Asset holding one material per sprite the effects draw with. Every particle effect needs it; without it there is nothing to render.")]
  private ParticleMaterialTable ParticleMaterials;

  public static GameManager Instance { get; private set; }

  public EffectService Effects { get; private set; }

  void Awake()
  {
    if (Instance != null && Instance != this)
    {
      // Two managers means two pools and two sets of live effects, with the
      // second silently winning every lookup. Losing the duplicate outright is
      // less confusing than half the game talking to each.
      Debug.LogWarning($"A second {nameof(GameManager)} was found on {name} and has been removed.");
      Destroy(gameObject);
      return;
    }

    Instance = this;

    // Loud, because the failure otherwise shows up much later as effects that
    // all render magenta with nothing in the console to say why.
    if (ParticleMaterials == null)
    {
      Debug.LogError($"{nameof(GameManager)} has no particle material table assigned, so every particle effect will render magenta. Assign Assets/VFX/ParticleMaterials.");
    }

    Effects = new EffectService(BuildFactories(), BuildEffectRoot());
  }

  void Update() => Effects?.Tick(Time.deltaTime);

  void OnDestroy()
  {
    // Only when this really is the live one. A duplicate destroying itself in
    // Awake must not blank the reference the survivor just took.
    if (Instance != this) return;

    Effects?.Clear();
    Effects = null;
    Instance = null;
  }

  // The catalogue list is the registry of what the game can play. Adding a
  // family of effects ends here, with one more entry.
  IEffectFactory[] BuildFactories() => new IEffectFactory[]
  {
    new ParticleEffectFactory(
      new IEffectCatalogue[]
      {
        new SmokeCatalogue(),
        new FireCatalogue(),
        new ConfettiCatalogue(),
        new SparkCatalogue(),
      },
      ParticleMaterials),
  };

  // Pooled and unparented effects live under here rather than loose in the
  // scene, so the hierarchy shows what is running instead of filling up with
  // orphans nobody can trace back.
  Transform BuildEffectRoot()
  {
    GameObject effects = new("Effects");

    effects.transform.SetParent(transform, false);

    return effects.transform;
  }
}
