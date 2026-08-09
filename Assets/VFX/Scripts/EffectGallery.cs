using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Spawns one of every effect in a row so they can be judged side by side rather
// than one at a time from memory. A scratch tool for picking a look, not part of
// the game.
//
// It goes through the service like anything else would, which is the point:
// whatever the gallery cannot express, a real caller will not be able to either.
public class EffectGallery : MonoBehaviour
{
  [SerializeField]
  [Tooltip("Metres between effects. Too tight and neighbouring clouds overlap and get judged as one.")]
  private float Spacing = 2.5f;
  [SerializeField]
  [Tooltip("Seconds before a finished effect is played again. Looping ones never finish, so this only affects the one shots. 0 plays each effect once.")]
  private float ReplayInterval = 2.0f;

  private EffectHandle[] handles;
  private float replayTimer;

  // Cached, not a property: Enum.GetValues allocates a fresh array on every
  // call, and this is read from Update and from the gizmo pass.
  private static readonly EffectId[] Effects = (EffectId[])Enum.GetValues(typeof(EffectId));

  void Start()
  {
    if (GameManager.Instance == null)
    {
      Debug.LogError($"{nameof(EffectGallery)} needs a {nameof(GameManager)} in the scene to play anything.");
      return;
    }

    handles = new EffectHandle[Effects.Length];

    PlayAll();
  }

  void Update()
  {
    if (handles == null) return;
    if (ReplayInterval <= 0.0f) return;

    replayTimer += Time.deltaTime;

    if (replayTimer < ReplayInterval) return;

    replayTimer = 0.0f;

    // Only the ones that have actually ended. Restarting a looping column every
    // interval would chop it off at the knees.
    EffectService service = GameManager.Instance.Effects;

    for (int index = 0; index < handles.Length; index++)
    {
      if (service.IsPlaying(handles[index])) continue;

      handles[index] = Play(index);
    }
  }

  void PlayAll()
  {
    for (int index = 0; index < handles.Length; index++)
    {
      handles[index] = Play(index);
    }
  }

  EffectHandle Play(int index) => GameManager.Instance.Effects.Play(
    EffectRequest.At(Effects[index], transform.position + SlotPosition(index, Effects.Length)));

  // Centred on the gallery, so the row grows outwards from wherever it is placed
  // instead of always running off to one side.
  Vector3 SlotPosition(int index, int count) => Vector3.right * ((index - (count - 1) * 0.5f) * Spacing);

  void OnDrawGizmos()
  {
    for (int index = 0; index < Effects.Length; index++)
    {
      Vector3 slot = transform.position + SlotPosition(index, Effects.Length);

      Gizmos.color = Color.cyan;
      Gizmos.DrawWireSphere(slot, 0.08f);

#if UNITY_EDITOR
      Handles.Label(slot + Vector3.up * 0.2f, Effects[index].ToString());
#endif
    }
  }
}
