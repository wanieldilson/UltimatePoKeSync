using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.Analysis;

/// <summary>Infers a broad, explainable role from base stats and the live moveset.</summary>
public sealed class PokemonRoleAnalyzer
{
    private const int MoveEvidenceWeight = 10;
    private const double OffensiveBiasThreshold = 1.15;
    private const double WallBulkThreshold = 90;
    private const double WallDefenseBiasThreshold = 1.25;

    private readonly IGenerationRulesResolver _rulesResolver;

    public PokemonRoleAnalyzer()
        : this(GenerationRulesResolver.Default)
    {
    }

    public PokemonRoleAnalyzer(IGenerationRulesResolver rulesResolver)
    {
        ArgumentNullException.ThrowIfNull(rulesResolver);
        _rulesResolver = rulesResolver;
    }

    public PokemonRoleAnalysis Analyze(PokemonSnapshot member, PokemonGeneration generation)
    {
        ArgumentNullException.ThrowIfNull(member);

        IGenerationRules rules = _rulesResolver.Resolve(generation)
            ?? throw new NotSupportedException($"No analysis rules are available for {generation}.");

        int physicalMoves = 0;
        int specialMoves = 0;
        int utilityMoves = 0;

        foreach (MoveSlot move in member.Moves.Where(move => move.MoveId > 0))
        {
            MoveCategory category = rules.GetMoveCategory(move.MoveId, move.Type);
            if (category == MoveCategory.Status || !rules.CanProvideSuperEffectiveCoverage(move.MoveId))
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

        int physicalScore = member.BaseStats.Attack + (physicalMoves * MoveEvidenceWeight);
        int specialScore = member.BaseStats.SpecialAttack + (specialMoves * MoveEvidenceWeight);
        double bulkScore =
            (member.BaseStats.Hp + member.BaseStats.Defense + member.BaseStats.SpecialDefense) / 3.0;
        int scalingMoves = physicalMoves + specialMoves;

        PokemonRole role = InferRole(
            member,
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
            bulkScore);
    }

    private static PokemonRole InferRole(
        PokemonSnapshot member,
        int physicalScore,
        int specialScore,
        double bulkScore,
        int scalingMoves,
        int utilityMoves)
    {
        if (utilityMoves >= 2 && bulkScore >= WallBulkThreshold &&
            bulkScore >= Math.Max(member.BaseStats.Attack, member.BaseStats.SpecialAttack) * 0.9)
        {
            if (member.BaseStats.Defense >= member.BaseStats.SpecialDefense * WallDefenseBiasThreshold)
            {
                return PokemonRole.PhysicalWall;
            }

            if (member.BaseStats.SpecialDefense >= member.BaseStats.Defense * WallDefenseBiasThreshold)
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
