using System.Text;
using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.Session;

/// <summary>
/// A compact key over the fields that actually matter for analysis.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately excludes the volatile fields: current PP, current HP and any other battle
/// state. Those change constantly during play — a single battle turn moves PP — and none
/// of them changes a single recommendation about EVs, nature, moves or items.
/// </para>
/// <para>
/// Without this, the analysis would be recomputed several times per second while the
/// player fights, all to produce identical output.
/// </para>
/// <para>
/// A string rather than a hash: at 15 updates per second the cost is irrelevant, and a
/// hash collision would silently swallow a real party change, which is precisely the
/// failure mode this whole layer exists to avoid.
/// </para>
/// </remarks>
internal static class PartySignature
{
    public static string Compute(PartySnapshot party)
    {
        var builder = new StringBuilder(party.Members.Count * 64);
        builder.Append(party.Game.GameCode).Append('|');

        foreach (PokemonSnapshot mon in party.Members)
        {
            builder
                .Append(mon.SlotIndex).Append(':')
                .Append(mon.SpeciesId).Append(':')
                .Append(mon.PersonalityValue).Append(':')
                .Append(mon.Level).Append(':')
                .Append(mon.NatureId).Append(':')
                .Append(mon.AbilityId).Append(':')
                .Append(mon.HeldItemId).Append(':')
                .Append(mon.IsEgg ? '1' : '0').Append(':');

            Append(builder, mon.IndividualValues);
            Append(builder, mon.EffortValues);

            foreach (MoveSlot move in mon.Moves)
            {
                builder.Append(move.MoveId).Append(',');
            }

            builder.Append('|');
        }

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, StatBlock stats) => builder
        .Append(stats.Hp).Append('.')
        .Append(stats.Attack).Append('.')
        .Append(stats.Defense).Append('.')
        .Append(stats.SpecialAttack).Append('.')
        .Append(stats.SpecialDefense).Append('.')
        .Append(stats.Speed).Append(':');
}
