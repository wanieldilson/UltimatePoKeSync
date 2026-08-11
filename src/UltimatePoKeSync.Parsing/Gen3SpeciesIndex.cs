using PKHeX.Core;

namespace UltimatePoKeSync.Parsing;

/// <summary>
/// Converts a national dex number to the index Gen 3 uses internally.
/// </summary>
/// <remarks>
/// The two disagree from Hoenn onwards, and not by a constant. Treecko is national 252 and
/// internal 277, which looks like a simple offset of 25 and is not: Pelipper is national
/// 279 and internal 310, while its neighbours sit elsewhere again. A rule invented from the
/// first few species matches for a while and then quietly picks the wrong sprite, so the
/// conversion comes from PKHeX's table rather than from arithmetic. See D-033.
/// </remarks>
public static class Gen3SpeciesIndex
{
    /// <summary>The game's own index, or 0 when the species is not a Gen 3 one.</summary>
    public static int ToInternal(int nationalSpeciesId) =>
        nationalSpeciesId is < 1 or > 386 ? 0 : SpeciesConverter.GetInternal3((ushort)nationalSpeciesId);
}
