using UnityEngine;

// Builds the effects of one family. The service holds no knowledge of smoke,
// sparks or confetti: it asks whichever factory claims the id, so a new family
// is a new class and one line at the composition root, not an edit to the
// service itself.
public interface IEffectFactory
{
  bool Supports(EffectId id);

  // Only called when the pool has nothing to hand back, so this is free to be
  // as expensive as building the effect properly requires. The instance comes
  // back stopped, parented under the given root, and ready to be positioned.
  EffectInstance Create(EffectId id, Transform parent);
}
