namespace UltimatePoKeSync.Contracts;

/// <summary>A conservative story-progress reading from live emulator memory.</summary>
public sealed record DetectedStoryProgress(
    int BadgeCount,
    int? MapId,
    string Evidence);

/// <summary>
/// Reads only progress facts whose memory layout has been verified for this exact ROM.
/// Returning null activates the manual selector; a guessed milestone must never unlock a
/// future encounter.
/// </summary>
public interface IStoryProgressReader
{
    bool Supports(GameIdentity game);

    Task<DetectedStoryProgress?> ReadAsync(
        GameIdentity game,
        CancellationToken cancellationToken = default);
}
