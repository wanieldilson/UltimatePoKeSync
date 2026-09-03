using System.Text;
using PKHeX.Core;
using UltimatePoKeSync.Contracts;
using Xunit;

namespace UltimatePoKeSync.SoulSilverOpponent.Tests;

public sealed class SoulSilverOpponentScannerTests
{
    [Fact]
    public async Task ReadsAValidatedTrainerPartyThroughTheDocumentedLayout()
    {
        var memory = new FakeMemory();
        uint root = 0x02010000;
        uint manager = 0x02080000;
        uint party = manager + SoulSilverMemoryMap.PrimaryTrainerPartyOffset;
        memory.PutHeader("IPGE", "POKEMON SS", revision: 0);
        memory.PutUInt32(SoulSilverMemoryMap.RootPointerAddress, root);
        memory.PutUInt32(root + SoulSilverMemoryMap.TrainerManagerPointerOffset, manager);
        memory.PutPokemon(party, CreateGastly());

        OpponentScan result = await new SoulSilverOpponentScanner(memory)
            .ScanAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ScanState.Ready, result.State);
        OpponentRoster roster = Assert.Single(result.Rosters);
        OpponentPokemon pokemon = Assert.Single(roster.Members);
        Assert.Equal("GASTLY", pokemon.SpeciesName);
        Assert.Equal(13, pokemon.Level);
        Assert.Equal(28, pokemon.CurrentHp);
        Assert.Contains(pokemon.Moves, move => move.Name == "Lick");
    }

    [Fact]
    public async Task FallsBackToTheWildPokemonLocation()
    {
        var memory = new FakeMemory();
        uint root = 0x02010000;
        memory.PutHeader("IPGE", "POKEMON SS", revision: 0);
        memory.PutUInt32(SoulSilverMemoryMap.RootPointerAddress, root);
        memory.PutPokemon(root + SoulSilverMemoryMap.WildPokemonOffset, CreateGastly());

        OpponentScan result = await new SoulSilverOpponentScanner(memory)
            .ScanAsync(TestContext.Current.CancellationToken);

        OpponentRoster roster = Assert.Single(result.Rosters);
        Assert.Equal("Wild opponent", roster.Label);
        Assert.Equal("GASTLY", Assert.Single(roster.Members).SpeciesName);
    }

    [Fact]
    public async Task RefusesAValidPokemonUnderTheWrongGameCode()
    {
        var memory = new FakeMemory();
        memory.PutHeader("IPKE", "POKEMON HG", revision: 0);
        memory.PutUInt32(SoulSilverMemoryMap.RootPointerAddress, 0x02010000);

        OpponentScan result = await new SoulSilverOpponentScanner(memory)
            .ScanAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ScanState.UnsupportedGame, result.State);
        Assert.Empty(result.Rosters);
    }

    [Fact]
    public async Task DoesNotPrintRandomBytesAsAnOpponent()
    {
        var memory = new FakeMemory();
        uint root = 0x02010000;
        memory.PutHeader("IPGE", "POKEMON SS", revision: 0);
        memory.PutUInt32(SoulSilverMemoryMap.RootPointerAddress, root);
        memory.PutBytes(root + SoulSilverMemoryMap.WildPokemonOffset,
            [.. Enumerable.Range(0, SoulSilverMemoryMap.SlotSize).Select(i => (byte)(i * 37))]);

        OpponentScan result = await new SoulSilverOpponentScanner(memory)
            .ScanAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ScanState.Ready, result.State);
        Assert.Empty(result.Rosters);
    }

    [Fact]
    public void CommandLineRejectsAnIntervalThatWouldHammerTheStub()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(
            () => CommandLineOptions.Parse(["--interval", "20"]));

        Assert.Contains("250", error.Message, StringComparison.Ordinal);
    }

    private static PK4 CreateGastly()
    {
        var pokemon = new PK4
        {
            Species = 92,
            PID = 0x12345678,
            TID16 = 123,
            SID16 = 456,
            CurrentLevel = 13,
            Stat_Level = 13,
            Stat_HPCurrent = 28,
            Stat_HPMax = 31,
            Ability = 26,
            Move1 = 122,
            Move1_PP = 30,
            Nickname = "GASTLY",
            OriginalTrainerName = "MORTY",
        };
        pokemon.RefreshChecksum();
        return pokemon;
    }

    private sealed class FakeMemory : IEmulatorMemoryReader
    {
        private const uint BaseAddress = 0x02000000;
        private readonly byte[] _data = new byte[4 * 1024 * 1024];

        public bool CanRead => true;

        public Task<byte[]?> ReadMemoryAsync(
            uint address,
            int length,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (address < BaseAddress || address + length > BaseAddress + _data.Length)
            {
                return Task.FromResult<byte[]?>(null);
            }

            return Task.FromResult<byte[]?>(
                _data.AsSpan((int)(address - BaseAddress), length).ToArray());
        }

        public void PutHeader(string code, string title, byte revision)
        {
            var header = new byte[SoulSilverMemoryMap.HeaderLength];
            Encoding.ASCII.GetBytes(title).CopyTo(header, 0);
            Encoding.ASCII.GetBytes(code).CopyTo(header, 0x0C);
            header[0x1E] = revision;
            PutBytes(SoulSilverMemoryMap.CartridgeHeader, header);
        }

        public void PutUInt32(uint address, uint value) => PutBytes(address, BitConverter.GetBytes(value));

        public void PutPokemon(uint address, PK4 pokemon)
        {
            byte[] data = pokemon.Data.ToArray();
            PokeCrypto.Encrypt45(data);
            PutBytes(address, data);
        }

        public void PutBytes(uint address, byte[] value) =>
            value.CopyTo(_data, (int)(address - BaseAddress));
    }
}
