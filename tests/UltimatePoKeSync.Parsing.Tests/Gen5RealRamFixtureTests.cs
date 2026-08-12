using System.Text.Json;
using UltimatePoKeSync.Contracts;
using Xunit;

namespace UltimatePoKeSync.Parsing.Tests;

/// <summary>
/// The Gen 5 parser against bytes taken out of a real Pokémon Black, not a constructed
/// fixture: the Italian cartridge of D-040, read through melonDS's GDB stub, with the Snivy
/// that was actually in the party at the time.
/// </summary>
/// <remarks>
/// A parser can be made to pass tests written from its own assumptions. This one has to
/// agree with a game that was running.
/// </remarks>
public sealed class Gen5RealRamFixtureTests
{
    private readonly Gen5PartyParser _parser = new();

    [Fact]
    public void TheSnivyThatWasInThePartyComesBackWhole()
    {
        PokemonSnapshot snivy = Assert.Single(_parser.Parse(LoadFixture()).Members);

        Assert.Equal(495, snivy.SpeciesId);
        Assert.Equal("Snivy", snivy.SpeciesName);
        Assert.Equal(6, snivy.Level);
        Assert.Equal(0xFB3938B6, snivy.PersonalityValue);

        // Grass and nothing else, from the Black/White personal table.
        Assert.Equal(PokemonType.Grass, snivy.PrimaryType);
        Assert.Equal(PokemonType.None, snivy.SecondaryType);
        Assert.False(snivy.IsDualType);

        // It was hurt when the memory was read: 17 of 22.
        Assert.Equal(17, snivy.CurrentHp);
        Assert.Equal(22, snivy.CurrentStats.Hp);
        Assert.False(snivy.IsFainted);
        Assert.Equal(StatusCondition.None, snivy.Status);
        Assert.False(snivy.IsEgg);
    }

    /// <summary>
    /// Nature is the field that would have been silently wrong had the Gen 3 parser been
    /// copied: there it is derived from the personality value, here it is stored.
    /// </summary>
    [Fact]
    public void NatureIsReadRatherThanDerivedFromThePersonalityValue()
    {
        PokemonSnapshot snivy = Assert.Single(_parser.Parse(LoadFixture()).Members);

        Assert.InRange(snivy.NatureId, 0, 24);
        Assert.False(string.IsNullOrWhiteSpace(snivy.NatureName));
        Assert.NotEqual("?", snivy.NatureName);
    }

    [Fact]
    public void ItsMovesAreNamedAndTyped()
    {
        PokemonSnapshot snivy = Assert.Single(_parser.Parse(LoadFixture()).Members);
        MoveSlot[] known = [.. snivy.Moves.Where(move => !move.IsEmpty)];

        Assert.NotEmpty(known);
        Assert.All(known, move => Assert.NotEqual("?", move.Name));
        Assert.All(known, move => Assert.InRange(move.CurrentPp, 0, move.MaxPp));

        // A Snivy this early knows Tackle.
        Assert.Contains(known, move => move.Name == "Tackle");
    }

    [Fact]
    public void TheFiveEmptySlotsAreNotMistakenForPokemon()
    {
        PartySnapshot party = _parser.Parse(LoadFixture());

        Assert.Single(party.Members);
        Assert.Empty(party.RejectedSlots);
        Assert.Equal(1, party.Count);
    }

    /// <summary>
    /// The same bytes read as Gen 3 must produce nothing rather than nonsense: a parser that
    /// accepts another generation's data is how a wrong memory map goes unnoticed.
    /// </summary>
    [Fact]
    public void TheGen3ParserRefusesAGen5Game()
    {
        RawPartySnapshot raw = LoadFixture();

        Assert.False(new Gen3PartyParser().CanParse(raw.Game));
        Assert.True(_parser.CanParse(raw.Game));
    }

    /// <summary>
    /// Registering the parser is the step that is easy to forget: everything can work and
    /// the app still show nothing, because the resolver never hands the bytes over.
    /// </summary>
    [Fact]
    public void TheResolverPicksTheGen5ParserForAGen5Game()
    {
        RawPartySnapshot raw = LoadFixture();

        IPartyParser? chosen = PartyParserResolver.CreateDefault().Resolve(raw.Game);

        Assert.IsType<Gen5PartyParser>(chosen);
        Assert.Single(chosen.Parse(raw).Members);
    }

    private static RawPartySnapshot LoadFixture()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "black-it-snivy.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;

        var game = new GameIdentity(
            root.GetProperty("gameCode").GetString()!,
            root.GetProperty("title").GetString()!,
            root.GetProperty("revision").GetInt32(),
            (PokemonGeneration)root.GetProperty("generation").GetInt32());

        return new RawPartySnapshot(
            game,
            root.GetProperty("partyCount").GetInt32(),
            Convert.FromBase64String(root.GetProperty("data").GetString()!),
            root.GetProperty("slotSize").GetInt32(),
            DateTimeOffset.UnixEpoch,
            1);
    }
}
