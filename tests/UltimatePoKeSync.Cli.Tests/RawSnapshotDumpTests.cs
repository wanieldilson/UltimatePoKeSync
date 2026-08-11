using UltimatePoKeSync.Contracts;
using Xunit;

namespace UltimatePoKeSync.Cli.Tests;

/// <summary>
/// The dump format is how a capture becomes a fixture, and how a bug becomes reproducible
/// without the reporter's save file. A round trip that loses a field would be found much
/// later and much more expensively.
/// </summary>
public sealed class RawSnapshotDumpTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"upks-dump-{Guid.NewGuid():N}");

    [Fact]
    public void WhatIsWrittenIsWhatComesBack()
    {
        byte[] data = [.. Enumerable.Range(0, 600).Select(i => (byte)(i * 7 % 251))];
        var original = new RawPartySnapshot(
            new GameIdentity("BPEI", "POKEMON EMER", 0, PokemonGeneration.Gen3),
            3,
            data,
            100,
            DateTimeOffset.UnixEpoch,
            42);

        string path = RawSnapshotDump.Write(original, _directory);
        RawPartySnapshot read = RawSnapshotDump.Read(path);

        Assert.Equal(original.Game.GameCode, read.Game.GameCode);
        Assert.Equal(original.Game.Title, read.Game.Title);
        Assert.Equal(original.Game.Revision, read.Game.Revision);
        Assert.Equal(original.Game.Generation, read.Game.Generation);
        Assert.Equal(original.PartyCount, read.PartyCount);
        Assert.Equal(original.SlotSize, read.SlotSize);
        Assert.Equal(data, read.PartyData.ToArray());
    }

    [Fact]
    public void TheFileIsNamedAfterTheGameAndTheSequence()
    {
        var snapshot = new RawPartySnapshot(
            new GameIdentity("BPRI", "POKEMON FIRE", 0, PokemonGeneration.Gen3),
            1,
            new byte[600],
            100,
            DateTimeOffset.UnixEpoch,
            7);

        string path = RawSnapshotDump.Write(snapshot, _directory);

        Assert.Equal("bpri-seq0007.json", Path.GetFileName(path));
    }

    [Fact]
    public void AFileThatIsNotASnapshotIsRefused()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "rubbish.json");
        File.WriteAllText(path, "{\"something\": \"else\"}");

        Assert.ThrowsAny<Exception>(() => RawSnapshotDump.Read(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
