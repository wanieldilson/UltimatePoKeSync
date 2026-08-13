using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.GameData;

/// <summary>
/// The base stats of any species in a game, including ones nobody is carrying.
/// </summary>
/// <remarks>
/// <para>
/// A party member arrives with its own base stats already read, so this exists for the
/// species a snapshot cannot supply: what the Pokémon is going to become. Advice about
/// nature and effort values outlives the current form, because a nature is fixed at capture
/// and effort values carry through an evolution, so it has to be aimed at the Pokémon the
/// player will be battling with rather than the one in front of them. See D-052.
/// </para>
/// <para>
/// Behind an interface for the same reason as <see cref="IMoveLearnSource"/> and
/// <see cref="IEvolutionSource"/>: the data is PKHeX's, and Analysis must not depend on
/// PKHeX. See D-007.
/// </para>
/// </remarks>
public interface ISpeciesBaseStatsSource
{
    string SourceName { get; }

    bool Supports(GameIdentity game);

    /// <summary>
    /// The species' base stats in that game, or null when the source does not know it.
    /// Null rather than a zeroed block, because a Pokémon with no stats does not exist and
    /// silently returning one would be a wrong answer wearing the shape of a right one.
    /// </summary>
    StatBlock? FindBaseStats(GameIdentity game, int speciesId);

    /// <summary>
    /// The battle-relevant identity of the species, including its types. Competitive move
    /// advice targets an unambiguous final evolution, so STAB has to be judged against that
    /// species too rather than against the unevolved member in the snapshot.
    /// </summary>
    SpeciesBattleProfile? FindProfile(GameIdentity game, int speciesId);
}

/// <summary>
/// Stable battle facts about a species that advice may target even when it is not in the
/// party. The name comes from the evolution source because it also describes the route.
/// </summary>
public sealed record SpeciesBattleProfile(
    PokemonType PrimaryType,
    PokemonType SecondaryType,
    StatBlock BaseStats);

/// <summary>
/// The per-game data the analysis layer needs, gathered so a composition root names them
/// once. One more source is one more property here, not a new parameter at every call site.
/// </summary>
public sealed record GameDataSources(
    IMoveLearnSource Learnsets,
    IEvolutionSource Evolutions,
    ISpeciesBaseStatsSource BaseStats);
