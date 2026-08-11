using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;
using Xunit;

namespace UltimatePoKeSync.GameData.Learnsets.Tests;

public sealed class PKHeXGen3MoveLearnSourceTests
{
    private const int Zubat = 41;
    private const int Treecko = 252;
    private const int Charizard = 6;

    private static readonly GameIdentity Emerald =
        new("BPEE", "POKEMON EMER", 0, PokemonGeneration.Gen3);

    private static readonly GameIdentity ItalianEmerald =
        new("BPEI", "POKEMON EMER", 0, PokemonGeneration.Gen3);

    private static readonly GameIdentity FireRed =
        new("BPRE", "POKEMON FIRE", 0, PokemonGeneration.Gen3);

    private static readonly GameIdentity RubySapphire =
        new("AXVE", "POKEMON RUBY", 0, PokemonGeneration.Gen3);

    private readonly PKHeXGen3MoveLearnSource _moves = PKHeXGen3MoveLearnSource.Instance;

    [Fact]
    public void Supports_CoversEveryGen3GameTheParserCovers()
    {
        Assert.All(
            new[]
            {
                "BPEE", "BPEF", "BPED", "BPES", "BPEI",
                "BPRE", "BPGE", "AXVE", "AXPE",

                // Italian releases, confirmed to use the same addresses as their USA
                // counterparts by tools/check-gen3-addresses.py. See D-034.
                "BPRI", "BPGI", "AXVI", "AXPI",
            },
            code => Assert.True(
                _moves.Supports(new GameIdentity(code, "", 0, PokemonGeneration.Gen3)),
                code));

        Assert.False(_moves.Supports(new GameIdentity("BPEJ", "", 0, PokemonGeneration.Gen3)));
        Assert.False(_moves.Supports(new GameIdentity("BPEE", "", 0, PokemonGeneration.Gen4)));
    }

    /// <summary>
    /// The reason this source is keyed by game. Zubat learns Supersonic at 6 and Astonish
    /// at 11 in Ruby, Sapphire and Emerald, and exactly the other way round in FireRed and
    /// LeafGreen. A learnset merged across the generation has to pick one and be wrong
    /// somewhere. See D-027.
    /// </summary>
    [Fact]
    public void LevelUpMoves_DisagreeBetweenGamesOfTheSameGeneration()
    {
        Assert.Equal(6, LevelOf(Emerald, Zubat, "Supersonic"));
        Assert.Equal(11, LevelOf(Emerald, Zubat, "Astonish"));
        Assert.Equal(6, LevelOf(RubySapphire, Zubat, "Supersonic"));

        Assert.Equal(11, LevelOf(FireRed, Zubat, "Supersonic"));
        Assert.Equal(6, LevelOf(FireRed, Zubat, "Astonish"));
    }

    /// <summary>
    /// Tutors diverge far harder than levels do, which is why the same keying matters for
    /// them. See D-030.
    /// </summary>
    [Fact]
    public void TutorMoves_DifferSharplyBetweenEmeraldAndFireRed()
    {
        int emerald = Count(Emerald, Charizard, MoveLearnMethod.Tutor);
        int fireRed = Count(FireRed, Charizard, MoveLearnMethod.Tutor);

        Assert.True(emerald > 10, $"Emerald tutors for Charizard: {emerald}");
        Assert.True(fireRed < emerald, $"FireRed {fireRed} should trail Emerald {emerald}");
    }

    [Fact]
    public void MachineMoves_AreReportedAndCarryNoLevel()
    {
        IReadOnlyList<LearnableMove> machines =
        [
            .. _moves.FindLearnableMoves(Emerald, Charizard, 50)
                .Where(move => move.Method == MoveLearnMethod.Machine),
        ];

        Assert.NotEmpty(machines);
        Assert.All(machines, move => Assert.Null(move.LearnedAtLevel));

        // Fire Blast is TM38 and Charizard learns it; it is not in its level-up table.
        Assert.Contains(machines, move => move.Move.Name == "Fire Blast");
    }

    [Fact]
    public void EveryMoveIsReportedOnceUnderItsEarliestSource()
    {
        IReadOnlyList<LearnableMove> all = _moves.FindLearnableMoves(Emerald, Charizard, 60);

        Assert.Equal(
            all.Select(move => move.Move.ReferenceId).Distinct().Count(),
            all.Count);

        // Level-up first, then machines, then tutors: the order a player meets them in.
        Assert.Equal(
            all.Select(move => move.Method).OrderBy(method => method),
            all.Select(move => move.Method));
    }

    [Fact]
    public void LevelUpMoves_StopAtTheGivenLevel()
    {
        IReadOnlyList<LearnableMove> atEleven =
        [
            .. _moves.FindLearnableMoves(Emerald, Treecko, 11)
                .Where(move => move.Method == MoveLearnMethod.LevelUp),
        ];

        Assert.Equal(
            ["pound", "leer", "absorb", "quickattack"],
            atEleven.Select(move => move.Move.ReferenceId));
        Assert.Equal([1, 1, 6, 11], atEleven.Select(move => move.LearnedAtLevel));

        Assert.DoesNotContain(
            LevelUpOnly(Emerald, Treecko, 15),
            move => move.Move.ReferenceId == "pursuit");
        Assert.Contains(
            LevelUpOnly(Emerald, Treecko, 16),
            move => move.Move.ReferenceId == "pursuit");
    }

    [Fact]
    public void EveryWesternEmeraldIsTheSameGame()
    {
        Assert.Equal(
            _moves.FindLearnableMoves(Emerald, Treecko, 40),
            _moves.FindLearnableMoves(ItalianEmerald, Treecko, 40));
    }

    [Fact]
    public void SpeciesOutsideGen3_ReturnNothing()
    {
        Assert.Empty(_moves.FindLearnableMoves(Emerald, 387, 50));
        Assert.Empty(_moves.FindLearnableMoves(Emerald, 0, 50));
    }

    [Fact]
    public void AnUnsupportedGameOrAnImpossibleLevel_IsRejected()
    {
        Assert.Throws<NotSupportedException>(() => _moves.FindLearnableMoves(
            new GameIdentity("BPEJ", "", 0, PokemonGeneration.Gen3),
            Treecko,
            10));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _moves.FindLearnableMoves(Emerald, Treecko, 101));
    }

    private IReadOnlyList<LearnableMove> LevelUpOnly(GameIdentity game, int species, int level) =>
        [.. _moves.FindLearnableMoves(game, species, level)
            .Where(move => move.Method == MoveLearnMethod.LevelUp)];

    private int Count(GameIdentity game, int species, MoveLearnMethod method) =>
        _moves.FindLearnableMoves(game, species, 100).Count(move => move.Method == method);

    private int? LevelOf(GameIdentity game, int species, string moveName) =>
        _moves.FindLearnableMoves(game, species, 100)
            .First(move => move.Move.Name == moveName && move.Method == MoveLearnMethod.LevelUp)
            .LearnedAtLevel;
}
