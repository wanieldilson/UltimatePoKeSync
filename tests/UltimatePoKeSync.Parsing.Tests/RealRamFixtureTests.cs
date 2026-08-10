using System.Text.Json;
using UltimatePoKeSync.Contracts;
using Xunit;

namespace UltimatePoKeSync.Parsing.Tests;

/// <summary>
/// Tests against bytes captured from a real console, not from fixtures we built ourselves.
/// </summary>
/// <remarks>
/// Hand-built fixtures only prove the parser agrees with PKHeX's writer. These prove it
/// agrees with what the game actually puts in memory, which is the claim that matters and
/// the one that would break silently on a PKHeX upgrade.
/// </remarks>
public sealed class RealRamFixtureTests
{
    private readonly Gen3PartyParser _parser = new();

    [Fact]
    public void ItalianEmerald_StarterIsReadCorrectlyFromRealRam()
    {
        // Captured 2026-08-10 from Pokémon Emerald (Italy) running in mGBA 0.10.5,
        // shortly after receiving the starter. See D-017.
        PartySnapshot party = _parser.Parse(LoadFixture("emerald-it-treecko.json"));

        Assert.Empty(party.RejectedSlots);
        PokemonSnapshot treecko = Assert.Single(party.Members);

        Assert.Equal(252, treecko.SpeciesId);
        Assert.Equal("TREECKO", treecko.SpeciesName);
        Assert.Equal(5, treecko.Level);
        Assert.Equal(PokemonType.Grass, treecko.PrimaryType);
        Assert.Equal(PokemonType.None, treecko.SecondaryType);
        Assert.Equal("Overgrow", treecko.AbilityName);
        Assert.Equal(new StatBlock(40, 45, 35, 65, 55, 70), treecko.BaseStats);
        Assert.Equal(new StatBlock(19, 14, 24, 29, 0, 25), treecko.IndividualValues);
        Assert.False(treecko.IsEgg);

        Assert.Equal(["Pound", "Leer"], treecko.Moves.Where(m => !m.IsEmpty).Select(m => m.Name));
    }

    [Fact]
    public void ItalianEmerald_LeftoverBytesBeyondThePartyCountAreNotRead()
    {
        // Real RAM does not zero the unused slots: in this capture only slot 0 holds the
        // party member, yet none of the other five is all zeroes. Slots past the declared
        // count must be ignored entirely. See D-019.
        RawPartySnapshot raw = LoadFixture("emerald-it-treecko.json");

        Assert.Equal(1, raw.PartyCount);
        Assert.Equal(6, raw.SlotCapacity);
        Assert.Contains(
            Enumerable.Range(1, 5),
            slot => !raw.GetSlot(slot).Span.TrimStart((byte)0).IsEmpty);

        Assert.Single(_parser.Parse(raw).Members);
    }

    private static RawPartySnapshot LoadFixture(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
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
            DateTimeOffset.UtcNow,
            1);
    }
}
