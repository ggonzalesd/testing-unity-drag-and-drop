using UnityEngine;

// Builds every effect that is made of particles, whichever family it belongs
// to. One factory rather than one per family, because the families differ only
// in their data: they all end up in the same builder with the same materials.
//
// Composed of catalogues rather than deriving from a base class, so a new
// family is a new IEffectCatalogue and one entry at the composition root, with
// nothing here to change.
public sealed class ParticleEffectFactory : IEffectFactory
{
  private readonly IEffectCatalogue[] catalogues;
  private readonly ParticleMaterialTable materials;

  public ParticleEffectFactory(IEffectCatalogue[] catalogues, ParticleMaterialTable materials)
  {
    this.catalogues = catalogues ?? System.Array.Empty<IEffectCatalogue>();
    this.materials = materials;
  }

  public bool Supports(EffectId id) => CatalogueFor(id) != null;

  public EffectInstance Create(EffectId id, Transform parent)
  {
    IEffectCatalogue catalogue = CatalogueFor(id);

    if (catalogue == null) return null;

    ParticleProfile[] layers = catalogue.LayersFor(id);

    if (layers == null || layers.Length == 0)
    {
      Debug.LogError($"Effect {id} is claimed by {catalogue.GetType().Name} but has no layers, so there is nothing to build.");
      return null;
    }

    GameObject root = ParticleEffectBuilder.Build(id.ToString(), layers, materials, parent);

    EffectInstance instance = root.AddComponent<EffectInstance>();

    // Collected from the hierarchy rather than returned by the builder, so the
    // layer count stays the builder's business. An effect that grows a layer
    // needs no change here.
    instance.Bind(id, root.GetComponentsInChildren<ParticleSystem>());

    return instance;
  }

  // Linear, and that is fine: there is one catalogue per family, so the list is
  // a handful of entries and this only runs on a pool miss.
  IEffectCatalogue CatalogueFor(EffectId id)
  {
    for (int index = 0; index < catalogues.Length; index++)
    {
      if (catalogues[index].Contains(id)) return catalogues[index];
    }

    return null;
  }
}
