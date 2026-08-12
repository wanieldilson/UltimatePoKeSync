using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.GameData.Learnsets;

/// <summary>
/// Every generation PKHeX answers for, behind one source each. See D-043.
/// </summary>
/// <remarks>
/// One place to add a generation, so nothing above has to be told there is a new one. What
/// is not here is not supported, and the layers above find that out by asking rather than by
/// reading garbage.
/// </remarks>
public static class PKHeXSources
{
    public static IMoveLearnSource Learnsets { get; } = new CompositeMoveLearnSource(
        PKHeXGen3MoveLearnSource.Instance,
        PKHeXGen5MoveLearnSource.Instance);

    public static IEvolutionSource Evolutions { get; } = new CompositeEvolutionSource(
        PKHeXGen3EvolutionSource.Instance,
        PKHeXGen5EvolutionSource.Instance);
}
