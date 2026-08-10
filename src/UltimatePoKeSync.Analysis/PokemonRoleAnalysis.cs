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
    double BulkScore);

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
