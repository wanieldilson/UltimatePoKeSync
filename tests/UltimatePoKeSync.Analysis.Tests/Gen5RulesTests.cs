using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;
using Xunit;

namespace UltimatePoKeSync.Analysis.Tests;

/// <summary>
/// Gen 5 battle rules, and above all the one that is not Gen 3 with a different number on
/// it: the physical/special split moved from the type to the move. See D-041.
/// </summary>
public sealed class Gen5RulesTests
{
    private static readonly Gen5Rules Rules = Gen5Rules.Instance;
    private static readonly Gen3Rules Older = Gen3Rules.Instance;

    /// <summary>
    /// Bite is the cleanest case. In Gen 3 it is special because Dark was a special type; in
    /// Gen 5 it is physical because Bite is. A parser that copied the older rule would send
    /// every Dark and Ghost attacker off the wrong stat.
    /// </summary>
    [Theory]
    [InlineData(44, PokemonType.Dark, MoveCategory.Physical)]        // Bite
    [InlineData(247, PokemonType.Ghost, MoveCategory.Special)]       // Shadow Ball
    [InlineData(89, PokemonType.Ground, MoveCategory.Physical)]      // Earthquake
    [InlineData(57, PokemonType.Water, MoveCategory.Special)]        // Surf
    [InlineData(453, PokemonType.Fighting, MoveCategory.Physical)]   // Drain Punch is not
    [InlineData(14, PokemonType.Normal, MoveCategory.Status)]        // Swords Dance
    public void EachMoveCarriesItsOwnCategory(int moveId, PokemonType type, MoveCategory expected) =>
        Assert.Equal(expected, Rules.GetMoveCategory(moveId, type));

    [Fact]
    public void TheSplitDisagreesWithGenThreeExactlyWhereItShould()
    {
        // Same move, same type, different generation, different answer.
        Assert.Equal(MoveCategory.Special, Older.GetMoveCategory(44, PokemonType.Dark));
        Assert.Equal(MoveCategory.Physical, Rules.GetMoveCategory(44, PokemonType.Dark));

        // And where nothing changed, nothing changed.
        Assert.Equal(MoveCategory.Physical, Older.GetMoveCategory(89, PokemonType.Ground));
        Assert.Equal(MoveCategory.Physical, Rules.GetMoveCategory(89, PokemonType.Ground));
    }

    /// <summary>
    /// Base power is per generation too, and reading today's numbers would have been the
    /// easy mistake: Tackle is 50 in Black and 40 now.
    /// </summary>
    [Theory]
    [InlineData(33, 50)]    // Tackle: 35 in Gen 3, 50 in Gen 5, 40 today
    [InlineData(57, 95)]    // Surf
    [InlineData(89, 100)]   // Earthquake
    [InlineData(372, 50)]   // Assurance: 50 in Gen 5, 60 today
    [InlineData(14, 0)]     // Swords Dance
    public void BasePowerIsTheGenerationsOwn(int moveId, int expected) =>
        Assert.Equal(expected, Rules.GetMoveBasePower(moveId));

    [Fact]
    public void TackleHitsHarderInBlackThanItDidInEmerald()
    {
        Assert.Equal(35, Older.GetMoveBasePower(33));
        Assert.Equal(50, Rules.GetMoveBasePower(33));
    }

    /// <summary>
    /// An empty move slot has no type, and the team analyser walks all four slots. Asking
    /// about it must give a boring answer rather than an exception, which is how this was
    /// found, by running the console against the real party.
    /// </summary>
    [Fact]
    public void AnEmptyMoveSlotIsNotAnError()
    {
        Assert.Equal(MoveCategory.Status, Rules.GetMoveCategory(0, PokemonType.None));
        Assert.Equal(MoveCategory.Status, Older.GetMoveCategory(0, PokemonType.None));
    }

    [Fact]
    public void MovesBeyondTheGenerationAreNotInvented()
    {
        // Fusion Bolt is the last Gen 5 move; 560 is Gen 6 and must read as nothing.
        Assert.NotEqual(0, Rules.GetMoveBasePower(559));
        Assert.Equal(0, Rules.GetMoveBasePower(560));
        Assert.False(Rules.CanProvideSuperEffectiveCoverage(560));
    }

    [Fact]
    public void FixedDamageMovesCoverNothing()
    {
        // Night Shade and Seismic Toss deal damage without caring about the matchup.
        Assert.False(Rules.CanProvideSuperEffectiveCoverage(101));
        Assert.False(Rules.CanProvideSuperEffectiveCoverage(69));

        // Final Gambit is the Gen 5 addition to that family.
        Assert.False(Rules.CanProvideSuperEffectiveCoverage(515));

        Assert.True(Rules.CanProvideSuperEffectiveCoverage(57));
    }

    /// <summary>
    /// The chart did not change between the two generations: Fairy and Steel's lost
    /// resistances both arrived in Gen 6, so the two files we ship must agree everywhere.
    /// A difference would mean one of them was edited by hand.
    /// </summary>
    [Fact]
    public void TheTypeChartMatchesGenThreeEverywhere()
    {
        Assert.Equal(Older.TypeChart.Types, Rules.TypeChart.Types);

        foreach (PokemonType attacking in Rules.TypeChart.Types)
        {
            foreach (PokemonType defending in Rules.TypeChart.Types)
            {
                Assert.Equal(
                    Older.TypeChart.GetMultiplier(attacking, defending),
                    Rules.TypeChart.GetMultiplier(attacking, defending));
            }
        }
    }

    [Fact]
    public void SteelStillResistsGhostAndDark()
    {
        // The Gen 6 change that this generation predates. Worth pinning, because getting it
        // wrong would quietly flatter every Steel type on the team.
        Assert.Equal(0.5, Rules.TypeChart.GetMultiplier(PokemonType.Ghost, PokemonType.Steel));
        Assert.Equal(0.5, Rules.TypeChart.GetMultiplier(PokemonType.Dark, PokemonType.Steel));
    }

    /// <summary>Abilities that became immunities in Gen 5 and were not before.</summary>
    [Theory]
    [InlineData(Gen5AbilityIds.LightningRod, PokemonType.Electric, 0)]
    [InlineData(Gen5AbilityIds.StormDrain, PokemonType.Water, 0)]
    [InlineData(Gen5AbilityIds.SapSipper, PokemonType.Grass, 0)]
    [InlineData(Gen5AbilityIds.MotorDrive, PokemonType.Electric, 0)]
    [InlineData(Gen5AbilityIds.Levitate, PokemonType.Ground, 0)]
    public void AnAbilityCanRemoveAWeaknessEntirely(int abilityId, PokemonType attacking, double expected) =>
        Assert.Equal(
            expected,
            Rules.GetDefensiveMultiplier(attacking, PokemonType.Normal, PokemonType.None, abilityId));

    [Fact]
    public void DrySkinTradesAWaterImmunityForTakingMoreFromFire()
    {
        Assert.Equal(
            0,
            Rules.GetDefensiveMultiplier(
                PokemonType.Water, PokemonType.Normal, PokemonType.None, Gen5AbilityIds.DrySkin));

        Assert.Equal(
            1.25,
            Rules.GetDefensiveMultiplier(
                PokemonType.Fire, PokemonType.Normal, PokemonType.None, Gen5AbilityIds.DrySkin));
    }

    [Fact]
    public void FilterOnlyBluntsWhatIsAlreadySuperEffective()
    {
        // Fighting hits Normal for double; Filter takes a quarter off that.
        Assert.Equal(
            1.5,
            Rules.GetDefensiveMultiplier(
                PokemonType.Fighting, PokemonType.Normal, PokemonType.None, Gen5AbilityIds.Filter));

        // A neutral hit is untouched.
        Assert.Equal(
            1,
            Rules.GetDefensiveMultiplier(
                PokemonType.Water, PokemonType.Normal, PokemonType.None, Gen5AbilityIds.Filter));
    }

    [Fact]
    public void TheStatFormulaAgreesWithGenThree()
    {
        var baseStats = new StatBlock(45, 45, 55, 45, 55, 63);   // Snivy
        var perfect = new StatBlock(31, 31, 31, 31, 31, 31);
        var none = new StatBlock(0, 0, 0, 0, 0, 0);

        Assert.Equal(
            Older.CalculateStats(50, baseStats, perfect, none, 0),
            Rules.CalculateStats(50, baseStats, perfect, none, 0));
    }

    [Fact]
    public void TheResolverKnowsGenFive()
    {
        IGenerationRules? rules = GenerationRulesResolver.Default.Resolve(PokemonGeneration.Gen5);

        Assert.NotNull(rules);
        Assert.Equal(PokemonGeneration.Gen5, rules.Generation);
    }
}
