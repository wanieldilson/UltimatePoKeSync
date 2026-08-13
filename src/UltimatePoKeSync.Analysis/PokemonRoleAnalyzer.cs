using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.Analysis;

/// <summary>Infers a broad, explainable role from base stats and the live moveset.</summary>
/// <remarks>
/// <para>
/// The stats are the ones the Pokémon will end up with, not the ones it has. A nature is
/// fixed at capture and effort values carry through an evolution, so the advice that follows
/// from a role outlives the current form and has to be aimed at the final one. A Snivy and a
/// Serperior are not the same recommendation. See D-052.
/// </para>
/// <para>
/// The moveset is still the live one, because it is evidence about how the player is using
/// the Pokémon. It is weak evidence early on, when the game chose the moves, which is why it
/// is worth less than the gap between two base stats rather than more.
/// </para>
/// </remarks>
public sealed class PokemonRoleAnalyzer
{
    /// <summary>
    /// What one damaging move says about which stat a Pokémon is meant to use. Small on
    /// purpose: a level 6 starter knows one Normal attack because the game gave it one, and
    /// letting that outweigh the species itself turned every Snivy into a physical attacker
    /// and recommended it Adamant. See D-052.
    /// </summary>
    private const int MoveEvidenceWeight = 3;

    private const double OffensiveBiasThreshold = 1.15;
    private const double WallBulkThreshold = 90;
    private const double WallDefenseBiasThreshold = 1.25;

    /// <summary>Enough for the longest chain in the generations supported, with room spare.</summary>
    private const int MaximumEvolutionDepth = 4;

    private readonly IGenerationRulesResolver _rulesResolver;
    private readonly IEvolutionSource? _evolutions;
    private readonly ISpeciesBaseStatsSource? _baseStats;

    /// <summary>Judges on the Pokémon as it is, for a caller with no game data to hand.</summary>
    public PokemonRoleAnalyzer()
        : this(GenerationRulesResolver.Default, null, null)
    {
    }

    public PokemonRoleAnalyzer(GameDataSources sources)
        : this(GenerationRulesResolver.Default, sources)
    {
    }

    public PokemonRoleAnalyzer(IGenerationRulesResolver rulesResolver, GameDataSources sources)
        : this(rulesResolver, (sources ?? throw new ArgumentNullException(nameof(sources))).Evolutions,
            sources.BaseStats)
    {
    }

    public PokemonRoleAnalyzer(
        IGenerationRulesResolver rulesResolver,
        IEvolutionSource? evolutions,
        ISpeciesBaseStatsSource? baseStats)
    {
        ArgumentNullException.ThrowIfNull(rulesResolver);
        _rulesResolver = rulesResolver;
        _evolutions = evolutions;
        _baseStats = baseStats;
    }

    public PokemonRoleAnalysis Analyze(PokemonSnapshot member, GameIdentity game)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(game);

        IGenerationRules rules = _rulesResolver.Resolve(game.Generation)
            ?? throw new NotSupportedException(
                $"No analysis rules are available for {game.Generation}.");

        int physicalMoves = 0;
        int specialMoves = 0;
        int utilityMoves = 0;

        foreach (MoveSlot move in member.Moves.Where(move => move.MoveId > 0))
        {
            MoveCategory category = rules.GetMoveCategory(move.MoveId, move.Type);
            if (category == MoveCategory.Status)
            {
                utilityMoves++;
            }
            else if (category == MoveCategory.Physical)
            {
                physicalMoves++;
            }
            else
            {
                specialMoves++;
            }
        }

        (int judgedSpeciesId, string judgedSpeciesName, StatBlock stats,
            PokemonType primaryType, PokemonType secondaryType) = FinalForm(member, game);

        int physicalScore = stats.Attack + (physicalMoves * MoveEvidenceWeight);
        int specialScore = stats.SpecialAttack + (specialMoves * MoveEvidenceWeight);
        double bulkScore = (stats.Hp + stats.Defense + stats.SpecialDefense) / 3.0;
        int scalingMoves = physicalMoves + specialMoves;

        PokemonRole role = InferRole(
            stats,
            physicalScore,
            specialScore,
            bulkScore,
            scalingMoves,
            utilityMoves);

        return new PokemonRoleAnalysis(
            member,
            role,
            physicalMoves,
            specialMoves,
            utilityMoves,
            physicalScore,
            specialScore,
            bulkScore,
            judgedSpeciesId,
            judgedSpeciesName,
            stats,
            primaryType,
            secondaryType);
    }

    /// <summary>
    /// The stats of what this Pokémon becomes, and its name when that is not the Pokémon
    /// itself. Falls back to the member's own stats whenever the answer is not certain.
    /// </summary>
    /// <remarks>
    /// The walk stops at a branch. Eevee, Wurmple and Tyrogue become several different things
    /// with different stats, and picking one of them would be inventing the player's future
    /// rather than reading it. It does not stop at a trade or a stone: those change whether
    /// the evolution happens, not what it would be, and the offensive character of a line
    /// rarely flips across one step. The chain is followed at most
    /// <see cref="MaximumEvolutionDepth"/> times, so a table that ever pointed at itself
    /// cannot hang the analysis.
    /// </remarks>
    private (int SpeciesId, string SpeciesName, StatBlock Stats,
        PokemonType PrimaryType, PokemonType SecondaryType) FinalForm(
        PokemonSnapshot member,
        GameIdentity game)
    {
        if (_evolutions is null || _baseStats is null ||
            !_evolutions.Supports(game) || !_baseStats.Supports(game))
        {
            return Current(member);
        }

        int speciesId = member.SpeciesId;
        string name = member.SpeciesName;
        bool evolved = false;

        for (int step = 0; step < MaximumEvolutionDepth; step++)
        {
            EvolutionStep[] destinations =
            [
                .. _evolutions
                    .FindEvolutions(game, speciesId)
                    // Shedinja appears beside Ninjask; it does not replace Nincada.
                    .Where(next => !next.IsByproduct && IsEligible(member, next))
                    // One destination can have more than one route. In Gen 5, Feebas can
                    // reach Milotic through Beauty or a Prism Scale trade. That is one
                    // certain species, not a branch.
                    .GroupBy(next => next.IntoSpeciesId)
                    .Select(routes => routes.First()),
            ];

            if (destinations.Length == 0)
            {
                break;
            }

            if (destinations.Length > 1)
            {
                // A branch anywhere in the line makes the whole destination uncertain.
                // Stopping at the intermediate form would still invent a target that is
                // neither the member nor what it will eventually become.
                return Current(member);
            }

            speciesId = destinations[0].IntoSpeciesId;
            name = destinations[0].IntoSpeciesName;
            evolved = true;
        }

        if (!evolved)
        {
            return Current(member);
        }

        SpeciesBattleProfile? profile = _baseStats.FindProfile(game, speciesId);
        return profile is not null
            ? (speciesId, name, profile.BaseStats, profile.PrimaryType, profile.SecondaryType)
            : Current(member);
    }

    private static (int SpeciesId, string SpeciesName, StatBlock Stats,
        PokemonType PrimaryType, PokemonType SecondaryType) Current(PokemonSnapshot member) =>
        (member.SpeciesId, member.SpeciesName, member.BaseStats,
            member.PrimaryType, member.SecondaryType);

    private static bool IsEligible(PokemonSnapshot member, EvolutionStep evolution) =>
        evolution.RequiredGender is null || evolution.RequiredGender == member.Gender;

    private static PokemonRole InferRole(
        StatBlock stats,
        int physicalScore,
        int specialScore,
        double bulkScore,
        int scalingMoves,
        int utilityMoves)
    {
        if (utilityMoves >= 2 && bulkScore >= WallBulkThreshold &&
            bulkScore >= Math.Max(stats.Attack, stats.SpecialAttack) * 0.9)
        {
            if (stats.Defense >= stats.SpecialDefense * WallDefenseBiasThreshold)
            {
                return PokemonRole.PhysicalWall;
            }

            if (stats.SpecialDefense >= stats.Defense * WallDefenseBiasThreshold)
            {
                return PokemonRole.SpecialWall;
            }

            return PokemonRole.MixedWall;
        }

        if (scalingMoves == 0 || (utilityMoves >= 2 && scalingMoves <= 1))
        {
            return PokemonRole.Support;
        }

        if (physicalScore >= specialScore * OffensiveBiasThreshold)
        {
            return PokemonRole.PhysicalAttacker;
        }

        if (specialScore >= physicalScore * OffensiveBiasThreshold)
        {
            return PokemonRole.SpecialAttacker;
        }

        return PokemonRole.MixedAttacker;
    }
}
