using UnityEngine;

// The live object a factory produced, wrapped so the service can start, stop and
// recycle it without knowing it is made of ParticleSystems.
//
// The three operations are the whole contract, and they are deliberately not
// "start" and "stop": closing the tap and wiping the effect out are different
// acts, and an effect that is asked to end should trail off rather than blink.
public sealed class EffectInstance : MonoBehaviour
{
  private ParticleSystem[] systems;

  public EffectId Id { get; private set; }

  // True while anything is still emitting or any particle is still on screen.
  // The service waits on this rather than on the requested duration, so the
  // instance is not recycled out from under smoke that is still visible.
  public bool IsAlive
  {
    get
    {
      if (systems == null) return false;

      for (int index = 0; index < systems.Length; index++)
      {
        if (systems[index] != null && systems[index].IsAlive(true)) return true;
      }

      return false;
    }
  }

  public void Bind(EffectId id, ParticleSystem[] particleSystems)
  {
    Id = id;
    systems = particleSystems;
  }

  public void Play()
  {
    if (systems == null) return;

    for (int index = 0; index < systems.Length; index++)
    {
      if (systems[index] != null) systems[index].Play(true);
    }
  }

  // Closes the tap and leaves what is already in the air to finish.
  public void StopEmitting()
  {
    Stop(ParticleSystemStopBehavior.StopEmitting);
  }

  // Wipes it out this frame. Used when recycling: the systems simulate in world
  // space, so anything left alive would still be hanging at the old position
  // when the instance is played again somewhere else.
  public void Clear()
  {
    Stop(ParticleSystemStopBehavior.StopEmittingAndClear);
  }

  void Stop(ParticleSystemStopBehavior behavior)
  {
    if (systems == null) return;

    for (int index = 0; index < systems.Length; index++)
    {
      if (systems[index] != null) systems[index].Stop(true, behavior);
    }
  }
}
