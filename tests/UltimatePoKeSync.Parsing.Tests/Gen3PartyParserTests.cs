using UltimatePoKeSync.Contracts;
using Xunit;

namespace UltimatePoKeSync.Parsing.Tests;

public sealed class Gen3PartyParserTests
{
    private readonly Gen3PartyParser _parser = new();

    [Fact]
    public void Parse_DecifraIByteGrezziSenzaAiuto()
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
    public void Parse_LeggeTipiConIndiciModerniNonQuelliInterniGen3()
    {
        // Il tranello: in Gen 3 gli ID interni dei tipi hanno "???" all'indice 9, quindi
        // Fuoco e' 10 e Acqua 11. PKHeX li normalizza allo schema moderno (Acqua = 10).
        // Se questa assunzione cadesse, ogni analisi di tipo sarebbe sbagliata in modo
        // silenzioso, quindi va inchiodata da un test.
        RawPartySnapshot raw = Gen3TestData.ToRawSnapshot(Gen3TestData.CreateGyarados());

        PokemonSnapshot mon = Assert.Single(_parser.Parse(raw).Members);

        Assert.Equal(PokemonType.Water, mon.PrimaryType);
        Assert.Equal(PokemonType.Flying, mon.SecondaryType);
    }

    [Fact]
    public void Parse_MonoTipoNormalizzaIlSecondoTipoANone()
    {
        // Nei dati di gioco un mono-tipo ha il tipo ripetuto due volte. Lasciarlo passare
        // cosi' raddoppierebbe il peso di quel tipo nei calcoli difensivi.
        RawPartySnapshot raw = Gen3TestData.ToRawSnapshot(Gen3TestData.CreatePikachu());

        PokemonSnapshot mon = Assert.Single(_parser.Parse(raw).Members);

        Assert.Equal(PokemonType.Electric, mon.PrimaryType);
        Assert.Equal(PokemonType.None, mon.SecondaryType);
        Assert.False(mon.IsDualType);
    }

    [Fact]
    public void Parse_LeggeIvEvNaturaOggettoEMosse()
    {
        RawPartySnapshot raw = Gen3TestData.ToRawSnapshot(Gen3TestData.CreateGyarados());

        PokemonSnapshot mon = Assert.Single(_parser.Parse(raw).Members);

        Assert.Equal(new StatBlock(31, 31, 20, 5, 22, 30), mon.IndividualValues);
        Assert.Equal(new StatBlock(4, 252, 0, 0, 0, 252), mon.EffortValues);
        Assert.Equal(508, mon.TotalEffortValues);
        Assert.Equal("Ganlon Berry", mon.HeldItemName);
        Assert.Equal("Intimidate", mon.AbilityName);

        // La natura in Gen 3 e' derivata dal PID, non memorizzata: qui verifichiamo solo
        // che sia stata risolta in un nome valido.
        Assert.NotEqual("?", mon.NatureName);

        Assert.Equal(4, mon.Moves.Count);
        Assert.Equal("Hyper Beam", mon.Moves[0].Name);
        Assert.Equal(PokemonType.Ground, mon.Moves[1].Type);
        Assert.All(mon.Moves, m => Assert.False(m.IsEmpty));
    }

    [Fact]
    public void Parse_ByteSpazzaturaVengonoScartatiNonInterpretati()
    {
        // Il fallimento peggiore possibile e' mostrare un Pokemon inventato. Deve essere
        // scartato con un motivo, non tradotto in qualcosa di plausibile. Vedi D-008.
        RawPartySnapshot raw = Gen3TestData.WithJunkInFirstSlot();

        PartySnapshot party = _parser.Parse(raw);

        Assert.Empty(party.Members);
        RejectedSlot rejected = Assert.Single(party.RejectedSlots);
        Assert.Equal(0, rejected.SlotIndex);
    }

    [Fact]
    public void Parse_SlotVuotiOltreIlConteggioNonSonoSegnalatiComeErrori()
    {
        // Con 1 Pokemon in squadra, i 5 slot rimanenti sono zeri: normale, non un problema.
        RawPartySnapshot raw = Gen3TestData.ToRawSnapshot(Gen3TestData.CreatePikachu());

        PartySnapshot party = _parser.Parse(raw);

        Assert.Single(party.Members);
        Assert.Empty(party.RejectedSlots);
    }

    [Fact]
    public void Parse_SlotVuotoMaConteggiatoVieneSegnalato()
    {
        // Il conteggio dichiara 3 Pokemon ma ne troviamo 1: sintomo di lettura incoerente.
        RawPartySnapshot raw = Gen3TestData.ToRawSnapshot(
            Gen3TestData.Emerald, declaredCount: 3, Gen3TestData.CreatePikachu());

        PartySnapshot party = _parser.Parse(raw);

        Assert.Single(party.Members);
        Assert.Equal(2, party.RejectedSlots.Count);
    }

    [Fact]
    public void Parse_SquadraCompletaLeggeTuttiGliSlot()
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
    [InlineData("BPEE", true)]  // Emerald
    [InlineData("BPRE", true)]  // FireRed
    [InlineData("BPGE", true)]  // LeafGreen
    [InlineData("AXVE", true)]  // Ruby
    [InlineData("AXPE", true)]  // Sapphire
    [InlineData("BPEJ", false)] // Emerald giapponese: indirizzi diversi, non ancora mappata
    [InlineData("XXXX", false)]
    public void CanParse_AccettaSoloIGiochiMappati(string gameCode, bool expected)
    {
        var game = new GameIdentity(gameCode, "T", 0, PokemonGeneration.Gen3);

        Assert.Equal(expected, _parser.CanParse(game));
    }

    [Fact]
    public void CanParse_RifiutaLeAltreGenerazioni()
    {
        var game = new GameIdentity("BPEE", "T", 0, PokemonGeneration.Gen4);

        Assert.False(_parser.CanParse(game));
    }

    [Fact]
    public void Parse_GiocoSconosciutoRestituisceUnRifiutoNonUnaEccezione()
    {
        var game = new GameIdentity("XXXX", "?", 0, PokemonGeneration.Gen3);
        RawPartySnapshot raw = Gen3TestData.ToRawSnapshot(game, 1, Gen3TestData.CreatePikachu());

        PartySnapshot party = _parser.Parse(raw);

        Assert.Empty(party.Members);
        Assert.Single(party.RejectedSlots);
    }
}
