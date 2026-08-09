// One family of effects and the layers each of them is made of. Pure data with
// no Unity object in sight, so a family can be read top to bottom as a look
// rather than as a sequence of calls into Shuriken.
//
// Split out from the factory so that adding a family means writing a catalogue
// and registering it, with nothing in the factory or the service to touch.
public interface IEffectCatalogue
{
  bool Contains(EffectId id);

  // Draw order is list order: the first layer ends up behind the rest. Returns
  // null for an id it does not own.
  ParticleProfile[] LayersFor(EffectId id);
}
