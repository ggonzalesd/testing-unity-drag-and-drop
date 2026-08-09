// Every effect the game can ask for, named by what it is for rather than by the
// system that happens to draw it. A caller says what it wants to see; which
// factory builds it, and out of what, is not its problem.
//
// One flat enum rather than one per family. A single id is something a spawner,
// an inspector field and a save file can all refer to without a second enum
// alongside it saying which enum the first one belongs to.
public enum EffectId
{
  SmokeGusty,
  SmokeSurge,
  SmokeDrift,
  Fire,
  Confetti,
  SparkBurst,
}
