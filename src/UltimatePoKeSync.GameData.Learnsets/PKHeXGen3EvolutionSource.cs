using PKHeX.Core;
using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.GameData.Learnsets;

/// <summary>
/// The Gen 3 evolution table, read from PKHeX and turned into sentences a player can act on.
/// </summary>
/// <remarks>
/// <para>
/// PKHeX keeps one forward table per generation, so the Gen 3 one answers for all thirteen
/// game codes: no Gen 3 release differs here, unlike learnsets (D-027) and party addresses
/// (D-035). It still takes a <see cref="GameIdentity"/>, because that stops being true the
/// moment a later generation is added — Gen 7's Sun and Moon disagree about Rockruff.
/// </para>
/// <para>
/// The table's Argument column means a different thing for each trigger: an item index for
/// UseItem and TradeHeldItem, a beauty value for Feebas, and a copy of the level everywhere
/// else. Reading it as one kind of number is how a Fire Stone becomes level 95.
/// </para>
/// </remarks>
public sealed class PKHeXGen3EvolutionSource : IEvolutionSource
{
    private const int FirstGen3Species = 1;
    private const int LastGen3Species = 386;

    private static readonly Lazy<PKHeXGen3EvolutionSource> LazyInstance = new(() => new());

    private readonly GameStrings _strings = GameInfo.GetStrings("en");
    private readonly string[] _gen3Items;

    public PKHeXGen3EvolutionSource() =>
        // Item indices in the evolution table are Gen 3 ones. The current-generation item
        // list turns Thunder Stone into Damp Mulch, which is exactly the kind of confident
        // nonsense D-025 exists to keep out.
        _gen3Items = _strings.GetItemStrings(EntityContext.Gen3, GameVersion.E);

    public static PKHeXGen3EvolutionSource Instance => LazyInstance.Value;

    public string SourceName => "PKHeX.Core";

    public bool Supports(GameIdentity game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return game.Generation == PokemonGeneration.Gen3;
    }

    public IReadOnlyList<EvolutionStep> FindEvolutions(GameIdentity game, int speciesId)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (!Supports(game))
        {
            throw new NotSupportedException($"No Gen 3 evolution data is available for {game}.");
        }

        if (speciesId is < FirstGen3Species or > LastGen3Species)
        {
            return [];
        }

        ReadOnlyMemory<EvolutionMethod> forward =
            EvolutionTree.Evolves3.Forward.GetForward((ushort)speciesId, 0);

        var steps = new List<EvolutionStep>(forward.Length);
        foreach (EvolutionMethod method in forward.Span)
        {
            if (method.Species is < FirstGen3Species or > LastGen3Species)
            {
                continue;
            }

            steps.Add(Describe(method));
        }

        return steps;
    }

    private EvolutionStep Describe(EvolutionMethod method)
    {
        string into = SpeciesName(method.Species);
        (EvolutionTrigger trigger, string requirement) = method.Method switch
        {
            EvolutionType.LevelUp =>
                (EvolutionTrigger.Level, $"at Lv.{method.Level}"),

            EvolutionType.UseItem =>
                (EvolutionTrigger.Item, $"with a {ItemName(method.Argument)}"),

            EvolutionType.Trade =>
                (EvolutionTrigger.Trade, "when traded"),
            EvolutionType.TradeHeldItem =>
                (EvolutionTrigger.Trade, $"when traded holding a {ItemName(method.Argument)}"),

            EvolutionType.LevelUpFriendship =>
                (EvolutionTrigger.Friendship, "on levelling up with high friendship"),
            EvolutionType.LevelUpFriendshipMorning =>
                (EvolutionTrigger.Friendship, "on levelling up with high friendship, in the morning"),
            EvolutionType.LevelUpFriendshipNight =>
                (EvolutionTrigger.Friendship, "on levelling up with high friendship, at night"),

            // Tyrogue splits three ways on the stats it happens to have at level 20.
            EvolutionType.LevelUpATK =>
                (EvolutionTrigger.Condition, $"at Lv.{method.Level}, with Attack above Defense"),
            EvolutionType.LevelUpDEF =>
                (EvolutionTrigger.Condition, $"at Lv.{method.Level}, with Defense above Attack"),
            EvolutionType.LevelUpAeqD =>
                (EvolutionTrigger.Condition, $"at Lv.{method.Level}, with Attack equal to Defense"),

            // Wurmple's fork is decided by the personality value, before it ever hatched.
            EvolutionType.LevelUpECl5 or EvolutionType.LevelUpECgeq5 =>
                (EvolutionTrigger.Condition, $"at Lv.{method.Level}, if its personality says so"),

            // Ninjask is a plain level evolution; Shedinja is an extra creature on the side,
            // and the one that can fail to appear.
            EvolutionType.LevelUpNinjask =>
                (EvolutionTrigger.Level, $"at Lv.{method.Level}"),
            EvolutionType.LevelUpShedinja =>
                (EvolutionTrigger.Condition,
                    $"at Lv.{method.Level}, as a second one, with a free party slot and a spare Poké Ball"),

            EvolutionType.LevelUpBeauty =>
                (EvolutionTrigger.Condition, "on levelling up with very high beauty"),

            _ => (EvolutionTrigger.Condition, "under a condition this app does not know"),
        };

        // A level is recorded only where reaching it is genuinely enough. Elsewhere the
        // column holds a copy of the argument or nothing at all, and promising a level that
        // is not sufficient on its own is worse than promising nothing. Shedinja is the
        // exception among the level triggers: Lv.20 is when it can appear, not when it will.
        bool certainAtLevel = method.Method is
            EvolutionType.LevelUp or
            EvolutionType.LevelUpNinjask or
            EvolutionType.LevelUpATK or EvolutionType.LevelUpDEF or EvolutionType.LevelUpAeqD or
            EvolutionType.LevelUpECl5 or EvolutionType.LevelUpECgeq5;
        int? level = certainAtLevel && method.Level > 0 ? method.Level : null;

        return new EvolutionStep(method.Species, into, trigger, level, requirement);
    }

    private string SpeciesName(ushort species) =>
        species < _strings.Species.Count ? _strings.Species[species] : $"#{species}";

    private string ItemName(ushort item) =>
        item < _gen3Items.Length ? _gen3Items[item] : $"item #{item}";
}
