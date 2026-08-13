using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.Analysis;

public sealed record PokemonRoleAnalysis(
    PokemonSnapshot Member,
    PokemonRole Role,
    int PhysicalMoveCount,
    int SpecialMoveCount,
    int UtilityMoveCount,
    int PhysicalOffenseScore,
    int SpecialOffenseScore,
    double BulkScore,
    /// <summary>
    /// The species these scores describe. It is the final form when the evolution line is
    /// unambiguous, otherwise the member itself. Nature and EV advice use the same target,
    /// so one recommendation never mixes two species' stats. See D-052.
    /// </summary>
    int JudgedSpeciesId,
    string JudgedSpeciesName,
    StatBlock JudgedBaseStats,
    PokemonType JudgedPrimaryType,
    PokemonType JudgedSecondaryType)
{
    public bool IsJudgedAsEvolution => JudgedSpeciesId != Member.SpeciesId;
}

public enum PokemonRole
{
    PhysicalAttacker = 0,
    SpecialAttacker = 1,
    MixedAttacker = 2,
    PhysicalWall = 3,
    SpecialWall = 4,
    MixedWall = 5,
    Support = 6,
}
