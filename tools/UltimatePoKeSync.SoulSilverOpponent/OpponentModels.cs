namespace UltimatePoKeSync.SoulSilverOpponent;

internal enum ScanState
{
    Ready,
    EmulatorUnavailable,
    UnsupportedGame,
    InvalidRootPointer,
}

internal sealed record OpponentMove(
    int Id,
    string Name,
    int CurrentPp,
    int MaximumPp);

internal sealed record OpponentPokemon(
    int Slot,
    ushort SpeciesId,
    string SpeciesName,
    string Nickname,
    int Level,
    int CurrentHp,
    int MaximumHp,
    string Status,
    string Nature,
    string Ability,
    string HeldItem,
    uint PersonalityValue,
    IReadOnlyList<OpponentMove> Moves);

internal sealed record OpponentRoster(
    string Label,
    uint Address,
    IReadOnlyList<OpponentPokemon> Members);

internal sealed record OpponentScan(
    ScanState State,
    string GameCode,
    string GameTitle,
    int Revision,
    uint RootAddress,
    IReadOnlyList<OpponentRoster> Rosters,
    IReadOnlyList<string> Diagnostics)
{
    public string Signature => string.Join(
        '|',
        Rosters.SelectMany(roster => roster.Members.Select(member =>
            $"{roster.Label}:{member.Slot}:{member.PersonalityValue}:{member.Level}:"
            + $"{member.CurrentHp}:{member.MaximumHp}:{member.Status}:{member.HeldItem}:"
            + string.Join(',', member.Moves.Select(move => $"{move.Id}.{move.CurrentPp}")))));
}
