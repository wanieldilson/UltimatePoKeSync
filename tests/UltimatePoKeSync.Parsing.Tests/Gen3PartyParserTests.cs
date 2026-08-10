using UltimatePoKeSync.Contracts;
using Xunit;

namespace UltimatePoKeSync.Parsing.Tests;

public sealed class Gen3PartyParserTests
{
    private readonly Gen3PartyParser _parser = new();

    [Fact]
    public void Parse_DecryptsRawBytesUnaided()
    {
        RawPartySnapshot raw = Gen3TestData.ToRawSnapshot(Gen3TestData.CreateGyarados());

        PartySnapshot party = _parser.Parse(raw);

        PokemonSnapshot mon = Assert.Single(party.Members);
        Assert.Equal(130, mon.SpeciesId);
        Assert.Equal("GYARADOS", mon.SpeciesName);
        Assert.Equal(55, mon.Level);
        Assert.Empty(party.RejectedSlots);
    }

    [Fact]
    public void Parse_ReadsModernTypeIndicesNotGen3Internal()
    {
        // The trap: Gen 3 internal type IDs have "???" at index 9, so Fire is 10 and Water
        // is 11. PKHeX normalises them to the modern scheme (Water = 10). If that
        // assumption ever broke, every type analysis would be silently wrong, so it has to
        // be pinned by a test. See D-014.
        RawPartySnapshot raw = Gen3TestData.ToRawSnapshot(Gen3TestData.CreateGyarados());

        PokemonSnapshot mon = Assert.Single(_parser.Parse(raw).Members);

        Assert.Equal(PokemonType.Water, mon.PrimaryType);
        Assert.Equal(PokemonType.Flying, mon.SecondaryType);
    }

    [Fact]
    public void Parse_MonoTypeNormalisesSecondaryToNone()
    {
        // In the game data a mono-type repeats its type twice. Letting that through would
        // double that type's weight in defensive calculations. See D-015.
        RawPartySnapshot raw = Gen3TestData.ToRawSnapshot(Gen3TestData.CreatePikachu());

        PokemonSnapshot mon = Assert.Single(_parser.Parse(raw).Members);

        Assert.Equal(PokemonType.Electric, mon.PrimaryType);
        Assert.Equal(PokemonType.None, mon.SecondaryType);
        Assert.False(mon.IsDualType);
    }

    [Fact]
    public void Parse_ReadsIvsEvsNatureItemAndMoves()
    {
        RawPartySnapshot raw = Gen3TestData.ToRawSnapshot(Gen3TestData.CreateGyarados());

        PokemonSnapshot mon = Assert.Single(_parser.Parse(raw).Members);

        Assert.Equal(new StatBlock(31, 31, 20, 5, 22, 30), mon.IndividualValues);
        Assert.Equal(new StatBlock(4, 252, 0, 0, 0, 252), mon.EffortValues);
        Assert.Equal(508, mon.TotalEffortValues);
        Assert.Equal("Ganlon Berry", mon.HeldItemName);
        Assert.Equal("Intimidate", mon.AbilityName);

        // Gen 3 nature is derived from the PID rather than stored, so all we check here is
        // that it resolved to a valid name.
        Assert.NotEqual("?", mon.NatureName);

        Assert.Equal(4, mon.Moves.Count);
        Assert.Equal("Hyper Beam", mon.Moves[0].Name);
        Assert.Equal(PokemonType.Ground, mon.Moves[1].Type);
        Assert.All(mon.Moves, m => Assert.False(m.IsEmpty));
    }

    [Fact]
    public void Parse_JunkBytesAreRejectedNotInterpreted()
    {
        // The worst possible failure is showing an invented Pokémon. It must be rejected
        // with a reason, not translated into something plausible. See D-008.
        RawPartySnapshot raw = Gen3TestData.WithJunkInFirstSlot();

        PartySnapshot party = _parser.Parse(raw);

        Assert.Empty(party.Members);
        RejectedSlot rejected = Assert.Single(party.RejectedSlots);
        Assert.Equal(0, rejected.SlotIndex);
    }

    [Fact]
    public void Parse_EmptySlotsBeyondTheCountAreNotReportedAsErrors()
    {
        // With one Pokémon in the party the remaining five slots are zeroes: normal, not a
        // problem.
        RawPartySnapshot raw = Gen3TestData.ToRawSnapshot(Gen3TestData.CreatePikachu());

        PartySnapshot party = _parser.Parse(raw);

        Assert.Single(party.Members);
        Assert.Empty(party.RejectedSlots);
    }

    [Fact]
    public void Parse_EmptySlotWithinTheCountIsReported()
    {
        // The count declares three Pokémon but we find one: a sign of an inconsistent read.
        RawPartySnapshot raw = Gen3TestData.ToRawSnapshot(
            Gen3TestData.Emerald, declaredCount: 3, Gen3TestData.CreatePikachu());

        PartySnapshot party = _parser.Parse(raw);

        Assert.Single(party.Members);
        Assert.Equal(2, party.RejectedSlots.Count);
    }

    [Fact]
    public void Parse_FullPartyReadsEverySlot()
    {
        RawPartySnapshot raw = Gen3TestData.ToRawSnapshot(
            Gen3TestData.CreateGyarados(),
            Gen3TestData.CreatePikachu(),
            Gen3TestData.CreateGyarados(),
            Gen3TestData.CreatePikachu(),
            Gen3TestData.CreateGyarados(),
            Gen3TestData.CreatePikachu());

        PartySnapshot party = _parser.Parse(raw);

        Assert.Equal(6, party.Count);
        Assert.Empty(party.RejectedSlots);
        Assert.Equal([0, 1, 2, 3, 4, 5], party.Members.Select(m => m.SlotIndex));
    }

    [Theory]
    [InlineData("BPEE", true)]  // Emerald USA
    [InlineData("BPRE", true)]  // FireRed
    [InlineData("BPGE", true)]  // LeafGreen
    [InlineData("AXVE", true)]  // Ruby
    [InlineData("AXPE", true)]  // Sapphire
    [InlineData("BPEJ", false)] // Japanese Emerald: different addresses, not mapped yet
    [InlineData("XXXX", false)]
    public void CanParse_AcceptsOnlyMappedGames(string gameCode, bool expected)
    {
        var game = new GameIdentity(gameCode, "T", 0, PokemonGeneration.Gen3);

        Assert.Equal(expected, _parser.CanParse(game));
    }

    [Fact]
    public void CanParse_RejectsOtherGenerations()
    {
        var game = new GameIdentity("BPEE", "T", 0, PokemonGeneration.Gen4);

        Assert.False(_parser.CanParse(game));
    }

    [Fact]
    public void Parse_UnknownGameReturnsARejectionNotAnException()
    {
        var game = new GameIdentity("XXXX", "?", 0, PokemonGeneration.Gen3);
        RawPartySnapshot raw = Gen3TestData.ToRawSnapshot(game, 1, Gen3TestData.CreatePikachu());

        PartySnapshot party = _parser.Parse(raw);

        Assert.Empty(party.Members);
        Assert.Single(party.RejectedSlots);
    }
}
