using System.Collections.Generic;
using UnityEngine;

// Plays effects on request and takes them back when they are done.
//
// Plain C# with no MonoBehaviour of its own: whoever owns it decides when it
// ticks, and it can be driven from a test without entering play mode. The
// MonoBehaviour that does own it is GameManager, which is also the only place
// that knows which factories exist.
//
// Instances are pooled per id. Effects are the textbook case for it - many
// short lived objects, all identical within an id - and building a
// ParticleSystem is expensive enough that spawning a fresh one per hit shows up
// as a hitch.
public sealed class EffectService
{
  private const float NaturalScale = 1.0f;

  // Bookkeeping for one running effect. A class rather than a struct because it
  // is mutated in place every tick, and a list of structs would need writing
  // back through the index each time.
  private sealed class Active
  {
    public int Handle;
    public EffectInstance Instance;
    public float Remaining;
    // Set once nothing more will be emitted, either because the duration ran
    // out or because there was never a scheduled end to begin with. From that
    // point the only thing left to wait on is the last particle dying.
    public bool Stopping;
  }

  private readonly IEffectFactory[] factories;
  private readonly Transform root;
  private readonly Dictionary<EffectId, Stack<EffectInstance>> pool = new();
  private readonly List<Active> active = new();

  // Never reused, so a handle kept past the end of its effect can never be
  // mistaken for a live one. Starts at 1 because 0 is the invalid handle.
  private int nextHandle = 1;

  public EffectService(IEffectFactory[] factories, Transform root)
  {
    this.factories = factories ?? System.Array.Empty<IEffectFactory>();
    this.root = root;
  }

  public int ActiveCount => active.Count;

  public EffectHandle Play(EffectRequest request)
  {
    EffectInstance instance = Acquire(request.Id);

    if (instance == null) return EffectHandle.None;

    Place(instance, request);

    instance.gameObject.SetActive(true);
    instance.Play();

    Active record = new()
    {
      Handle = nextHandle++,
      Instance = instance,
      Remaining = request.Duration,
      // With no duration asked for there is nothing to schedule, so the effect
      // is already in its "waiting to die" state. A looping effect never leaves
      // it and runs until Stop; a one shot leaves it when its particles expire.
      // Both fall out of the same check in Tick.
      Stopping = request.Duration <= EffectRequest.UntilStopped,
    };

    active.Add(record);

    return new EffectHandle(record.Handle);
  }

  // Ends the effect gracefully: no more particles are emitted, and what is
  // already in the air plays out. The instance is recycled once it does.
  public void Stop(EffectHandle handle)
  {
    Active record = Find(handle);

    if (record == null) return;

    record.Instance.StopEmitting();
    record.Stopping = true;
    record.Remaining = 0.0f;
  }

  // Ends it this frame, particles and all. For teleports and scene resets,
  // where letting the old smoke drift out would look like a bug.
  public void Cancel(EffectHandle handle)
  {
    Active record = Find(handle);

    if (record == null) return;

    active.Remove(record);
    Release(record.Instance);
  }

  public bool IsPlaying(EffectHandle handle) => Find(handle) != null;

  public void Tick(float deltaTime)
  {
    // Backwards, because finished effects are removed as they are found.
    for (int index = active.Count - 1; index >= 0; index--)
    {
      Active record = active[index];

      // The instance can be destroyed underneath us by a scene unload or by a
      // hand edit in the hierarchy. Drop the record rather than nurse it.
      if (record.Instance == null)
      {
        active.RemoveAt(index);
        continue;
      }

      if (!record.Stopping)
      {
        record.Remaining -= deltaTime;

        if (record.Remaining <= 0.0f)
        {
          record.Instance.StopEmitting();
          record.Stopping = true;
        }
      }

      if (!record.Stopping || record.Instance.IsAlive) continue;

      active.RemoveAt(index);
      Release(record.Instance);
    }
  }

  // Everything down at once, with nothing left drifting. The pool survives it:
  // the instances are recycled, not destroyed.
  public void Clear()
  {
    for (int index = 0; index < active.Count; index++)
    {
      if (active[index].Instance != null) Release(active[index].Instance);
    }

    active.Clear();
  }

  void Place(EffectInstance instance, EffectRequest request)
  {
    Transform host = instance.transform;

    host.SetParent(request.Parent != null ? request.Parent : root, false);
    // World space, after parenting. A caller passing a parent is saying where
    // the effect should follow, not what its coordinates mean.
    host.position = request.Position;
    host.rotation = request.Rotation;
    // A default constructed request has a scale of zero, which would render
    // nothing at all and look like the effect never played.
    host.localScale = Vector3.one * (request.Scale > 0.0f ? request.Scale : NaturalScale);
  }

  EffectInstance Acquire(EffectId id)
  {
    Stack<EffectInstance> stack = PoolFor(id);

    while (stack.Count > 0)
    {
      EffectInstance pooled = stack.Pop();

      // Destroyed while parked. Keep drawing until the pool yields a live one
      // or runs dry, rather than failing the spawn over a stale entry.
      if (pooled != null) return pooled;
    }

    IEffectFactory factory = FactoryFor(id);

    if (factory == null)
    {
      Debug.LogError($"No factory is registered for effect {id}, so it cannot be played.");
      return null;
    }

    EffectInstance created = factory.Create(id, root);

    if (created == null) Debug.LogError($"The factory registered for effect {id} refused to build it.");

    return created;
  }

  void Release(EffectInstance instance)
  {
    instance.Clear();
    // Off the caller's parent before it is parked, or a pooled instance is
    // destroyed along with whatever object it happened to be riding.
    instance.transform.SetParent(root, false);
    instance.gameObject.SetActive(false);

    PoolFor(instance.Id).Push(instance);
  }

  Stack<EffectInstance> PoolFor(EffectId id)
  {
    if (pool.TryGetValue(id, out Stack<EffectInstance> stack)) return stack;

    stack = new Stack<EffectInstance>();
    pool[id] = stack;

    return stack;
  }

  // Linear, and that is fine: there is one factory per family of effect, so the
  // list is a handful of entries and this only runs on a pool miss.
  IEffectFactory FactoryFor(EffectId id)
  {
    for (int index = 0; index < factories.Length; index++)
    {
      if (factories[index].Supports(id)) return factories[index];
    }

    return null;
  }

  Active Find(EffectHandle handle)
  {
    if (!handle.IsValid) return null;

    for (int index = 0; index < active.Count; index++)
    {
      if (active[index].Handle == handle.Value) return active[index];
    }

    return null;
  }
}
