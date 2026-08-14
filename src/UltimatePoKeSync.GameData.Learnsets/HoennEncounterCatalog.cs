using System.Collections.ObjectModel;
using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.GameData.Learnsets;

/// <summary>Which Hoenn game a catalog answers for.</summary>
public enum HoennVersion
{
    Ruby = 0,
    Sapphire = 1,
    Emerald = 2,
}

/// <summary>
/// The main-story acquisition timeline for Hoenn: Ruby, Sapphire and Emerald. Wild species
/// and levels are a pinned snapshot of PKHeX's table for that version; the story order is
/// curated here, because a legality table says where a Pokémon exists and not when the player
/// can reach it.
/// </summary>
/// <remarks>
/// <para>
/// The two halves are not equally solid. The encounters are PKHeX's and a test checks every
/// one. The order is written from knowledge of the game with nothing to check it against, so
/// wherever it was uncertain the place was filed behind the badge that certainly precedes it:
/// late costs a suggestion, early sends somebody to a route they cannot open. See D-057.
/// </para>
/// <para>
/// The three versions share the region and therefore this timeline, but not their tables.
/// Emerald holds 312 wild entries against Ruby and Sapphire's 287, and the two of those differ
/// from each other in 38 places, so unlike Black and White (D-055) this cannot be a handful of
/// conditionals. Each version gets its own emitted list and they share everything else. See
/// D-058.
/// </para>
/// <para>
/// Grass and Surf only. Fishing needs a rod, which is an item this app cannot see, and it
/// would have filled the pool with Tentacool and Magikarp.
/// </para>
/// <para>Places PKHeX knows and this catalog will not suggest:</para>
/// <list type="bullet">
/// <item>Altering Cave (E): an event-only cave that holds Zubat unless a distribution changed it.</item>
/// <item>Artisan Cave: post-game, inside the Battle Frontier.</item>
/// <item>Cave of Origin: story-locked, and empty by the time the player can walk it.</item>
/// <item>Desert Underpass: post-game, opened by the Fossil Maniac after the League.</item>
/// <item>Mirage Tower: appears and vanishes with the day, which no snapshot can verify.</item>
/// </list>
/// </remarks>
public sealed class HoennEncounterCatalog : IEncounterCatalog
{
    private static readonly Lazy<HoennEncounterCatalog> LazyRuby =
        new(() => new(HoennVersion.Ruby));

    private static readonly Lazy<HoennEncounterCatalog> LazySapphire =
        new(() => new(HoennVersion.Sapphire));

    private static readonly Lazy<HoennEncounterCatalog> LazyEmerald =
        new(() => new(HoennVersion.Emerald));

    private readonly HoennVersion _version;
    private readonly IReadOnlyList<StoryMilestone> _milestones;
    private readonly IReadOnlyList<EncounterCandidate> _encounters;

    private HoennEncounterCatalog(HoennVersion version)
    {
        _version = version;

        StoryMilestone[] timeline =
        [
            Milestone("route-101", "Route 101", 10, 0,
                "After receiving your first Pokémon in Littleroot Town."),
            Milestone("route-103", "Route 103", 20, 0,
                "After reaching Oldale Town."),
            Milestone("route-102", "Route 102", 30, 0,
                "Heading west from Oldale Town toward Petalburg City."),
            Milestone("route-104", "Route 104", 40, 0,
                "North of Petalburg City, on the way to Petalburg Woods."),
            Milestone("petalburg-woods", "Petalburg Woods", 50, 0,
                "Crossing Petalburg Woods toward Rustboro City."),
            Milestone("route-116", "Route 116", 60, 1,
                "East of Rustboro City, after the Stone Badge.", 1),
            Milestone("rusturf", "Rusturf Tunnel", 70, 1,
                "Inside Rusturf Tunnel, during the Devon Goods errand.", 1),
            Milestone("dewford", "Dewford Town", 80, 1,
                "After sailing to Dewford Town with Mr. Briney.", 1),
            Milestone("granite-cave", "Granite Cave", 90, 1,
                "Inside Granite Cave, reached from Dewford Town.", 1),
            Milestone("route-109", "Route 109", 100, 2,
                "The beach at Slateport, after the Knuckle Badge.", 2),
            Milestone("route-110", "Route 110", 110, 2,
                "North of Slateport City toward Mauville.", 2),
            Milestone("route-117", "Route 117", 120, 3,
                "West of Mauville City, after the Dynamo Badge.", 3),
            Milestone("route-111", "Route 111", 130, 3,
                "North of Mauville, at the edge of the desert.", 3),
            Milestone("route-112", "Route 112", 140, 3,
                "On the slopes below Mt. Chimney.", 3),
            Milestone("jagged-pass", "Jagged Pass", 150, 3,
                "Descending Jagged Pass toward Lavaridge Town.", 3),
            Milestone("route-113", "Route 113", 160, 3,
                "In the ash fall north of Mt. Chimney.", 3),
            Milestone("route-114", "Route 114", 170, 3,
                "West of Fallarbor Town.", 3),
            Milestone("meteor-falls", "Meteor Falls", 180, 3,
                "Inside Meteor Falls, off Route 114.", 3),
            Milestone("route-115", "Route 115", 190, 3,
                "The coast north of Rustboro City.", 3),
            Milestone("fiery-path", "Fiery Path", 200, 4,
                "Through the Fiery Path, once Strength can move its boulders.", 4),
            Milestone("new-mauville", "New Mauville", 210, 4,
                "Inside New Mauville, after Wattson hands over the Basement Key.", 4),
            Milestone("route-118", "Route 118", 220, 5,
                "East of Mauville, across the water Surf opens after the Balance Badge.", 5),
            Milestone("route-119", "Route 119", 230, 5,
                "The tall grass on the way to the Weather Institute.", 5),
            Milestone("route-120", "Route 120", 240, 6,
                "North of Fortree City, after the Feather Badge.", 6),
            Milestone("route-121", "Route 121", 250, 6,
                "West of Lilycove City.", 6),
            Milestone("safari-zone", "Safari Zone", 260, 6,
                "Paying the entry fee at the Safari Zone on Route 121.", 6),
            Milestone("route-122", "Route 122", 270, 6,
                "The water south of Route 121, below Mt. Pyre.", 6),
            Milestone("mt-pyre", "Mt. Pyre", 280, 6,
                "Climbing Mt. Pyre.", 6),
            Milestone("route-123", "Route 123", 290, 6,
                "East of Mt. Pyre.", 6),
            Milestone("magma-hideout", "Magma Hideout", 300, 6,
                "Following Team Magma into their hideout on Jagged Pass.", 6),
            Milestone("route-124", "Route 124", 310, 6,
                "The sea east of Lilycove, on the way to Mossdeep.", 6),
            Milestone("route-125", "Route 125", 320, 6,
                "The sea around Shoal Cave, north of Mossdeep.", 6),
            Milestone("shoal-cave", "Shoal Cave", 330, 6,
                "Inside Shoal Cave, reached by Surf from Route 125.", 6),
            Milestone("route-127", "Route 127", 340, 7,
                "The open sea south of Mossdeep, after the Mind Badge.", 7),
            Milestone("route-126", "Route 126", 350, 7,
                "The sea around Sootopolis City.", 7),
            Milestone("underwater", "Underwater", 360, 7,
                "Diving from Route 124 or Route 126, once Dive is available.", 7),
            Milestone("seafloor-cavern", "Seafloor Cavern", 370, 7,
                "Inside Seafloor Cavern, during the Team Aqua chase.", 7),
            Milestone("abandoned-ship", "Abandoned Ship", 380, 7,
                "Exploring the Abandoned Ship on Route 108.", 7),
            Milestone("route-128", "Route 128", 390, 8,
                "The sea east of Sootopolis, after the Rain Badge.", 8),
            Milestone("route-129", "Route 129", 400, 8,
                "The deep water on the way south.", 8),
            Milestone("route-130", "Route 130", 410, 8,
                "The open sea near Pacifidlog Town.", 8),
            Milestone("route-131", "Route 131", 420, 8,
                "The water below the Sky Pillar.", 8),
            Milestone("pacifidlog", "Pacifidlog Town", 430, 8,
                "The water around Pacifidlog Town.", 8),
            Milestone("sky-pillar", "Sky Pillar", 440, 8,
                "Climbing the Sky Pillar.", 8),
            Milestone("route-132", "Route 132", 450, 8,
                "The sea west of Ever Grande City.", 8),
            Milestone("route-133", "Route 133", 460, 8,
                "Further along the sea toward Ever Grande.", 8),
            Milestone("route-134", "Route 134", 470, 8,
                "The last stretch of sea before Ever Grande City.", 8),
            Milestone("ever-grande", "Ever Grande City", 480, 8,
                "The shore below Victory Road.", 8),
            Milestone("victory-road", "Victory Road", 490, 8,
                "Inside Victory Road, after all eight Badges.", 8),
        ];

        _milestones = Array.AsReadOnly(timeline);
        Dictionary<string, StoryMilestone> at = timeline.ToDictionary(milestone => milestone.Id);

        var result = new List<EncounterCandidate>();
        switch (version)
        {
            case HoennVersion.Ruby:
                AddRuby(result, at);
                break;
            case HoennVersion.Sapphire:
                AddSapphire(result, at);
                break;
            default:
                AddEmerald(result, at);
                break;
        }

        _encounters = new ReadOnlyCollection<EncounterCandidate>(
        [
            .. result
                .GroupBy(encounter => encounter.SpeciesId)
                .Select(group => group
                    .OrderBy(encounter => encounter.EarliestMilestone.Order)
                    .ThenBy(encounter => encounter.Location, StringComparer.Ordinal)
                    .First())
                .OrderBy(encounter => encounter.EarliestMilestone.Order)
                .ThenBy(encounter => encounter.SpeciesId),
        ]);
    }

    public static HoennEncounterCatalog Ruby => LazyRuby.Value;

    public static HoennEncounterCatalog Sapphire => LazySapphire.Value;

    public static HoennEncounterCatalog Emerald => LazyEmerald.Value;

    public string SourceName =>
        $"PKHeX.Core 26.7.7 encounter snapshot + curated Pokémon {_version} story timeline";

    public bool Supports(GameIdentity game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return game.Generation == PokemonGeneration.Gen3 && Codes().Contains(game.GameCode);
    }

    public IReadOnlyList<StoryMilestone> FindMilestones(GameIdentity game)
    {
        EnsureSupported(game);
        return _milestones;
    }

    public IReadOnlyList<EncounterCandidate> FindEncounters(GameIdentity game)
    {
        EnsureSupported(game);
        return _encounters;
    }

    public StoryMilestone FindConservativeMilestone(GameIdentity game, int badgeCount)
    {
        EnsureSupported(game);
        if (badgeCount is < 0 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(badgeCount));
        }

        return badgeCount == 0
            ? _milestones[0]
            : _milestones
                .Where(milestone => milestone.GuaranteedWhenBadgesAtLeast is int required &&
                    required <= badgeCount)
                .MaxBy(milestone => milestone.Order)!;
    }

    /// <summary>Every language of that one game. The three do not share a table.</summary>
    private string[] Codes() => _version switch
    {
        HoennVersion.Ruby => ["AXVE", "AXVF", "AXVD", "AXVS", "AXVI", "AXVJ"],
        HoennVersion.Sapphire => ["AXPE", "AXPF", "AXPD", "AXPS", "AXPI", "AXPJ"],
        _ => ["BPEE", "BPEF", "BPED", "BPES", "BPEI", "BPEJ"],
    };

    private static StoryMilestone Milestone(
        string id,
        string name,
        int order,
        int badgeCount,
        string reachedWhen,
        int? guaranteedWhenBadgesAtLeast = null) =>
        new(id, name, order, badgeCount, reachedWhen, guaranteedWhenBadgesAtLeast);

    private static void AddWild(
        List<EncounterCandidate> target,
        StoryMilestone milestone,
        string location,
        EncounterMethod method,
        params WildEncounter[] encounters)
    {
        foreach (WildEncounter encounter in encounters)
        {
            target.Add(new EncounterCandidate(
                encounter.SpeciesId,
                encounter.SpeciesName,
                milestone,
                location,
                method,
                encounter.MinimumLevel,
                encounter.MaximumLevel));
        }
    }

    private static WildEncounter W(int speciesId, string speciesName, int minimumLevel, int maximumLevel) =>
        new(speciesId, speciesName, minimumLevel, maximumLevel);

    private void EnsureSupported(GameIdentity game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (!Supports(game))
        {
            throw new NotSupportedException(
                $"No Pokémon {_version} encounter timeline is available for {game}.");
        }
    }

    /// <summary>Emerald's wild table, emitted from PKHeX rather than typed by hand.</summary>
    private static void AddEmerald(
        List<EncounterCandidate> result,
        IReadOnlyDictionary<string, StoryMilestone> at)
    {
        AddWild(result, at["abandoned-ship"], "Abandoned Ship", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(73, "Tentacruel", 30, 35));
        AddWild(result, at["dewford"], "Dewford Town", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["ever-grande"], "Ever Grande City", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["fiery-path"], "Fiery Path", EncounterMethod.Cave,
            W(66, "Machop", 15, 16), W(88, "Grimer", 14, 14), W(109, "Koffing", 15, 16), W(218, "Slugma", 15, 15), W(322, "Numel", 15, 16), W(324, "Torkoal", 14, 16));
        AddWild(result, at["granite-cave"], "Granite Cave", EncounterMethod.Cave,
            W(41, "Zubat", 7, 11), W(63, "Abra", 8, 10), W(74, "Geodude", 6, 9), W(296, "Makuhita", 6, 11), W(302, "Sableye", 9, 12), W(304, "Aron", 7, 12));
        AddWild(result, at["jagged-pass"], "Jagged Pass", EncounterMethod.Grass,
            W(66, "Machop", 20, 22), W(322, "Numel", 20, 22), W(325, "Spoink", 20, 22));
        AddWild(result, at["route-121"], "Lilycove City", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["magma-hideout"], "Magma Hideout", EncounterMethod.Cave,
            W(74, "Geodude", 27, 30), W(75, "Graveler", 30, 33), W(324, "Torkoal", 28, 30));
        AddWild(result, at["meteor-falls"], "Meteor Falls", EncounterMethod.Cave,
            W(41, "Zubat", 14, 20), W(42, "Golbat", 33, 40), W(338, "Solrock", 14, 39), W(371, "Bagon", 25, 35));
        AddWild(result, at["meteor-falls"], "Meteor Falls", EncounterMethod.Surf,
            W(41, "Zubat", 5, 35), W(42, "Golbat", 30, 35), W(338, "Solrock", 5, 35));
        AddWild(result, at["route-127"], "Mossdeep City", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["mt-pyre"], "Mt. Pyre", EncounterMethod.Cave,
            W(37, "Vulpix", 25, 29), W(278, "Wingull", 26, 28), W(353, "Shuppet", 22, 30), W(355, "Duskull", 25, 30), W(358, "Chimecho", 28, 28));
        AddWild(result, at["new-mauville"], "New Mauville", EncounterMethod.Cave,
            W(81, "Magnemite", 22, 26), W(82, "Magneton", 26, 26), W(100, "Voltorb", 22, 26), W(101, "Electrode", 26, 26));
        AddWild(result, at["pacifidlog"], "Pacifidlog Town", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-104"], "Petalburg City", EncounterMethod.Surf,
            W(183, "Marill", 5, 35));
        AddWild(result, at["petalburg-woods"], "Petalburg Woods", EncounterMethod.Grass,
            W(261, "Poochyena", 5, 6), W(265, "Wurmple", 5, 6), W(266, "Silcoon", 5, 5), W(268, "Cascoon", 5, 5), W(276, "Taillow", 5, 6), W(285, "Shroomish", 5, 6), W(287, "Slakoth", 5, 6));
        AddWild(result, at["route-101"], "Route 101", EncounterMethod.Grass,
            W(261, "Poochyena", 2, 3), W(263, "Zigzagoon", 2, 3), W(265, "Wurmple", 2, 3));
        AddWild(result, at["route-102"], "Route 102", EncounterMethod.Grass,
            W(261, "Poochyena", 3, 4), W(263, "Zigzagoon", 3, 4), W(265, "Wurmple", 3, 4), W(270, "Lotad", 3, 4), W(273, "Seedot", 3, 3), W(280, "Ralts", 4, 4));
        AddWild(result, at["route-102"], "Route 102", EncounterMethod.Surf,
            W(118, "Goldeen", 20, 30), W(183, "Marill", 5, 35));
        AddWild(result, at["route-103"], "Route 103", EncounterMethod.Grass,
            W(261, "Poochyena", 2, 4), W(263, "Zigzagoon", 3, 4), W(278, "Wingull", 2, 4));
        AddWild(result, at["route-103"], "Route 103", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-104"], "Route 104", EncounterMethod.Grass,
            W(183, "Marill", 4, 5), W(261, "Poochyena", 4, 5), W(265, "Wurmple", 4, 4), W(276, "Taillow", 4, 5), W(278, "Wingull", 3, 5));
        AddWild(result, at["route-104"], "Route 104", EncounterMethod.Surf,
            W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-118"], "Route 105", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["dewford"], "Route 106", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-109"], "Route 107", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["abandoned-ship"], "Route 108", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-109"], "Route 109", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-110"], "Route 110", EncounterMethod.Grass,
            W(43, "Oddish", 13, 13), W(261, "Poochyena", 12, 12), W(278, "Wingull", 12, 12), W(309, "Electrike", 12, 13), W(311, "Plusle", 12, 13), W(312, "Minun", 13, 13), W(316, "Gulpin", 12, 13));
        AddWild(result, at["route-110"], "Route 110", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-111"], "Route 111", EncounterMethod.Grass,
            W(27, "Sandshrew", 19, 21), W(328, "Trapinch", 19, 21), W(331, "Cacnea", 20, 22), W(343, "Baltoy", 19, 21));
        AddWild(result, at["route-111"], "Route 111", EncounterMethod.Surf,
            W(118, "Goldeen", 20, 30), W(183, "Marill", 5, 35));
        AddWild(result, at["route-112"], "Route 112", EncounterMethod.Grass,
            W(183, "Marill", 14, 16), W(322, "Numel", 14, 16));
        AddWild(result, at["route-113"], "Route 113", EncounterMethod.Grass,
            W(218, "Slugma", 14, 16), W(227, "Skarmory", 16, 16), W(327, "Spinda", 14, 16));
        AddWild(result, at["route-114"], "Route 114", EncounterMethod.Grass,
            W(270, "Lotad", 15, 16), W(271, "Lombre", 16, 18), W(274, "Nuzleaf", 15, 15), W(333, "Swablu", 15, 17), W(336, "Seviper", 15, 17));
        AddWild(result, at["route-114"], "Route 114", EncounterMethod.Surf,
            W(118, "Goldeen", 20, 30), W(183, "Marill", 5, 35));
        AddWild(result, at["route-115"], "Route 115", EncounterMethod.Grass,
            W(39, "Jigglypuff", 24, 25), W(276, "Taillow", 23, 25), W(277, "Swellow", 25, 25), W(278, "Wingull", 24, 26), W(333, "Swablu", 23, 25));
        AddWild(result, at["route-115"], "Route 115", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-116"], "Route 116", EncounterMethod.Grass,
            W(63, "Abra", 7, 7), W(261, "Poochyena", 6, 8), W(276, "Taillow", 6, 8), W(290, "Nincada", 6, 7), W(293, "Whismur", 6, 6), W(300, "Skitty", 7, 8));
        AddWild(result, at["route-117"], "Route 117", EncounterMethod.Grass,
            W(43, "Oddish", 13, 14), W(183, "Marill", 13, 13), W(261, "Poochyena", 13, 14), W(273, "Seedot", 13, 13), W(313, "Volbeat", 13, 13), W(314, "Illumise", 13, 14));
        AddWild(result, at["route-117"], "Route 117", EncounterMethod.Surf,
            W(118, "Goldeen", 20, 30), W(183, "Marill", 5, 35));
        AddWild(result, at["route-118"], "Route 118", EncounterMethod.Grass,
            W(263, "Zigzagoon", 24, 26), W(264, "Linoone", 26, 26), W(278, "Wingull", 25, 27), W(309, "Electrike", 24, 26), W(310, "Manectric", 26, 26), W(352, "Kecleon", 25, 25));
        AddWild(result, at["route-118"], "Route 118", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-119"], "Route 119", EncounterMethod.Grass,
            W(43, "Oddish", 24, 27), W(263, "Zigzagoon", 25, 27), W(264, "Linoone", 25, 27), W(352, "Kecleon", 25, 25), W(357, "Tropius", 25, 27));
        AddWild(result, at["route-119"], "Route 119", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-120"], "Route 120", EncounterMethod.Grass,
            W(43, "Oddish", 25, 27), W(183, "Marill", 25, 27), W(261, "Poochyena", 25, 25), W(262, "Mightyena", 25, 27), W(273, "Seedot", 25, 25), W(352, "Kecleon", 25, 25), W(359, "Absol", 25, 27));
        AddWild(result, at["route-120"], "Route 120", EncounterMethod.Surf,
            W(118, "Goldeen", 20, 30), W(183, "Marill", 5, 35));
        AddWild(result, at["route-121"], "Route 121", EncounterMethod.Grass,
            W(43, "Oddish", 26, 28), W(44, "Gloom", 28, 28), W(261, "Poochyena", 26, 26), W(262, "Mightyena", 26, 28), W(278, "Wingull", 26, 28), W(352, "Kecleon", 25, 25), W(353, "Shuppet", 26, 28));
        AddWild(result, at["route-121"], "Route 121", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-122"], "Route 122", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-123"], "Route 123", EncounterMethod.Grass,
            W(43, "Oddish", 26, 28), W(44, "Gloom", 28, 28), W(261, "Poochyena", 26, 26), W(262, "Mightyena", 26, 28), W(278, "Wingull", 26, 28), W(352, "Kecleon", 25, 25), W(353, "Shuppet", 26, 28));
        AddWild(result, at["route-123"], "Route 123", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-124"], "Route 124", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-125"], "Route 125", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-126"], "Route 126", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-127"], "Route 127", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-128"], "Route 128", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-129"], "Route 129", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30), W(321, "Wailord", 25, 30));
        AddWild(result, at["route-130"], "Route 130", EncounterMethod.Grass,
            W(360, "Wynaut", 5, 50));
        AddWild(result, at["route-130"], "Route 130", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-131"], "Route 131", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-132"], "Route 132", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-133"], "Route 133", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-134"], "Route 134", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["rusturf"], "Rusturf Tunnel", EncounterMethod.Cave,
            W(293, "Whismur", 5, 8));
        AddWild(result, at["safari-zone"], "Safari Zone (RSE)", EncounterMethod.Grass,
            W(25, "Pikachu", 25, 27), W(43, "Oddish", 25, 29), W(44, "Gloom", 25, 31), W(84, "Doduo", 25, 29), W(85, "Dodrio", 29, 31), W(111, "Rhyhorn", 27, 29), W(127, "Pinsir", 27, 29), W(163, "Hoothoot", 35, 35), W(165, "Ledyba", 33, 33), W(167, "Spinarak", 33, 33), W(177, "Natu", 25, 29), W(178, "Xatu", 29, 31), W(179, "Mareep", 34, 36), W(190, "Aipom", 33, 35), W(191, "Sunkern", 33, 35), W(202, "Wobbuffet", 27, 29), W(203, "Girafarig", 25, 27), W(204, "Pineco", 34, 34), W(207, "Gligar", 37, 40), W(209, "Snubbull", 34, 34), W(214, "Heracross", 27, 29), W(216, "Teddiursa", 34, 36), W(228, "Houndour", 36, 39), W(231, "Phanpy", 27, 29), W(234, "Stantler", 36, 39), W(241, "Miltank", 37, 40));
        AddWild(result, at["safari-zone"], "Safari Zone (RSE)", EncounterMethod.Surf,
            W(54, "Psyduck", 20, 35), W(55, "Golduck", 25, 40), W(183, "Marill", 25, 35), W(194, "Wooper", 25, 30), W(195, "Quagsire", 35, 40));
        AddWild(result, at["seafloor-cavern"], "Seafloor Cavern", EncounterMethod.Cave,
            W(41, "Zubat", 28, 35), W(42, "Golbat", 33, 36));
        AddWild(result, at["seafloor-cavern"], "Seafloor Cavern", EncounterMethod.Surf,
            W(41, "Zubat", 5, 35), W(42, "Golbat", 30, 35), W(72, "Tentacool", 5, 35));
        AddWild(result, at["shoal-cave"], "Shoal Cave", EncounterMethod.Cave,
            W(41, "Zubat", 26, 32), W(42, "Golbat", 30, 32), W(361, "Snorunt", 26, 30), W(363, "Spheal", 26, 32));
        AddWild(result, at["shoal-cave"], "Shoal Cave", EncounterMethod.Surf,
            W(41, "Zubat", 5, 35), W(72, "Tentacool", 5, 35), W(363, "Spheal", 25, 35));
        AddWild(result, at["sky-pillar"], "Sky Pillar", EncounterMethod.Cave,
            W(42, "Golbat", 34, 35), W(302, "Sableye", 33, 34), W(334, "Altaria", 38, 39), W(344, "Claydol", 36, 38), W(354, "Banette", 37, 38));
        AddWild(result, at["route-109"], "Slateport City", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-126"], "Sootopolis City", EncounterMethod.Surf,
            W(129, "Magikarp", 5, 35));
        AddWild(result, at["underwater"], "Underwater (Route 124)", EncounterMethod.Surf,
            W(170, "Chinchou", 20, 30), W(366, "Clamperl", 20, 35), W(369, "Relicanth", 30, 35));
        AddWild(result, at["underwater"], "Underwater (Route 126)", EncounterMethod.Surf,
            W(170, "Chinchou", 20, 30), W(366, "Clamperl", 20, 35), W(369, "Relicanth", 30, 35));
        AddWild(result, at["victory-road"], "Victory Road (RSE)", EncounterMethod.Cave,
            W(41, "Zubat", 36, 36), W(42, "Golbat", 38, 44), W(293, "Whismur", 36, 36), W(294, "Loudred", 40, 40), W(296, "Makuhita", 36, 36), W(297, "Hariyama", 38, 42), W(302, "Sableye", 40, 44), W(303, "Mawile", 38, 44), W(304, "Aron", 36, 36), W(305, "Lairon", 40, 44));
        AddWild(result, at["victory-road"], "Victory Road (RSE)", EncounterMethod.Surf,
            W(42, "Golbat", 25, 40));
    }

    /// <summary>Ruby's wild table, emitted from PKHeX rather than typed by hand.</summary>
    private static void AddRuby(
        List<EncounterCandidate> result,
        IReadOnlyDictionary<string, StoryMilestone> at)
    {
        AddWild(result, at["abandoned-ship"], "Abandoned Ship", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(73, "Tentacruel", 30, 35));
        AddWild(result, at["dewford"], "Dewford Town", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["ever-grande"], "Ever Grande City", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["fiery-path"], "Fiery Path", EncounterMethod.Cave,
            W(66, "Machop", 15, 16), W(88, "Grimer", 14, 14), W(109, "Koffing", 15, 16), W(218, "Slugma", 15, 15), W(322, "Numel", 15, 16), W(324, "Torkoal", 14, 16));
        AddWild(result, at["granite-cave"], "Granite Cave", EncounterMethod.Cave,
            W(41, "Zubat", 7, 11), W(63, "Abra", 8, 10), W(74, "Geodude", 6, 9), W(296, "Makuhita", 6, 11), W(303, "Mawile", 9, 12), W(304, "Aron", 7, 12));
        AddWild(result, at["jagged-pass"], "Jagged Pass", EncounterMethod.Grass,
            W(66, "Machop", 18, 20), W(322, "Numel", 18, 20), W(325, "Spoink", 18, 20));
        AddWild(result, at["route-121"], "Lilycove City", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["meteor-falls"], "Meteor Falls", EncounterMethod.Cave,
            W(41, "Zubat", 14, 20), W(42, "Golbat", 33, 40), W(338, "Solrock", 14, 39), W(371, "Bagon", 25, 35));
        AddWild(result, at["meteor-falls"], "Meteor Falls", EncounterMethod.Surf,
            W(41, "Zubat", 5, 35), W(42, "Golbat", 30, 35), W(338, "Solrock", 5, 35));
        AddWild(result, at["route-127"], "Mossdeep City", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["mt-pyre"], "Mt. Pyre", EncounterMethod.Cave,
            W(37, "Vulpix", 25, 29), W(278, "Wingull", 26, 28), W(307, "Meditite", 27, 29), W(353, "Shuppet", 25, 30), W(355, "Duskull", 22, 30), W(358, "Chimecho", 28, 28));
        AddWild(result, at["new-mauville"], "New Mauville", EncounterMethod.Cave,
            W(81, "Magnemite", 22, 26), W(82, "Magneton", 26, 26), W(100, "Voltorb", 22, 26), W(101, "Electrode", 26, 26));
        AddWild(result, at["pacifidlog"], "Pacifidlog Town", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-104"], "Petalburg City", EncounterMethod.Surf,
            W(183, "Marill", 5, 35));
        AddWild(result, at["petalburg-woods"], "Petalburg Woods", EncounterMethod.Grass,
            W(263, "Zigzagoon", 5, 6), W(265, "Wurmple", 5, 6), W(266, "Silcoon", 5, 5), W(268, "Cascoon", 5, 5), W(276, "Taillow", 5, 6), W(285, "Shroomish", 5, 6), W(287, "Slakoth", 5, 6));
        AddWild(result, at["route-101"], "Route 101", EncounterMethod.Grass,
            W(261, "Poochyena", 2, 3), W(263, "Zigzagoon", 2, 3), W(265, "Wurmple", 2, 3));
        AddWild(result, at["route-102"], "Route 102", EncounterMethod.Grass,
            W(261, "Poochyena", 3, 4), W(263, "Zigzagoon", 3, 4), W(265, "Wurmple", 3, 4), W(273, "Seedot", 3, 4), W(280, "Ralts", 4, 4), W(283, "Surskit", 3, 3));
        AddWild(result, at["route-102"], "Route 102", EncounterMethod.Surf,
            W(183, "Marill", 5, 35), W(283, "Surskit", 20, 30));
        AddWild(result, at["route-103"], "Route 103", EncounterMethod.Grass,
            W(261, "Poochyena", 2, 4), W(263, "Zigzagoon", 2, 4), W(278, "Wingull", 2, 4));
        AddWild(result, at["route-103"], "Route 103", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-104"], "Route 104", EncounterMethod.Grass,
            W(263, "Zigzagoon", 4, 5), W(265, "Wurmple", 4, 5), W(276, "Taillow", 4, 5), W(278, "Wingull", 3, 5));
        AddWild(result, at["route-104"], "Route 104", EncounterMethod.Surf,
            W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-118"], "Route 105", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["dewford"], "Route 106", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-109"], "Route 107", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["abandoned-ship"], "Route 108", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-109"], "Route 109", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-110"], "Route 110", EncounterMethod.Grass,
            W(43, "Oddish", 13, 13), W(263, "Zigzagoon", 12, 12), W(278, "Wingull", 12, 12), W(309, "Electrike", 12, 13), W(311, "Plusle", 12, 13), W(312, "Minun", 13, 13), W(316, "Gulpin", 12, 13));
        AddWild(result, at["route-110"], "Route 110", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-111"], "Route 111", EncounterMethod.Grass,
            W(27, "Sandshrew", 19, 21), W(328, "Trapinch", 19, 21), W(331, "Cacnea", 19, 21), W(343, "Baltoy", 20, 22));
        AddWild(result, at["route-111"], "Route 111", EncounterMethod.Surf,
            W(183, "Marill", 5, 35), W(283, "Surskit", 20, 30));
        AddWild(result, at["route-112"], "Route 112", EncounterMethod.Grass,
            W(66, "Machop", 14, 16), W(322, "Numel", 14, 16));
        AddWild(result, at["route-113"], "Route 113", EncounterMethod.Grass,
            W(27, "Sandshrew", 14, 16), W(227, "Skarmory", 16, 16), W(327, "Spinda", 14, 16));
        AddWild(result, at["route-114"], "Route 114", EncounterMethod.Grass,
            W(273, "Seedot", 15, 16), W(274, "Nuzleaf", 16, 18), W(283, "Surskit", 15, 15), W(333, "Swablu", 15, 17), W(335, "Zangoose", 15, 17));
        AddWild(result, at["route-114"], "Route 114", EncounterMethod.Surf,
            W(183, "Marill", 5, 35), W(283, "Surskit", 20, 30));
        AddWild(result, at["route-115"], "Route 115", EncounterMethod.Grass,
            W(39, "Jigglypuff", 24, 25), W(276, "Taillow", 23, 25), W(277, "Swellow", 25, 25), W(278, "Wingull", 24, 26), W(333, "Swablu", 23, 25));
        AddWild(result, at["route-115"], "Route 115", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-116"], "Route 116", EncounterMethod.Grass,
            W(263, "Zigzagoon", 6, 8), W(276, "Taillow", 6, 8), W(290, "Nincada", 6, 7), W(293, "Whismur", 6, 7), W(300, "Skitty", 7, 8));
        AddWild(result, at["route-117"], "Route 117", EncounterMethod.Grass,
            W(43, "Oddish", 13, 13), W(183, "Marill", 13, 13), W(263, "Zigzagoon", 13, 14), W(283, "Surskit", 13, 13), W(313, "Volbeat", 13, 13), W(314, "Illumise", 13, 14), W(315, "Roselia", 13, 14));
        AddWild(result, at["route-117"], "Route 117", EncounterMethod.Surf,
            W(183, "Marill", 5, 35), W(283, "Surskit", 20, 30));
        AddWild(result, at["route-118"], "Route 118", EncounterMethod.Grass,
            W(263, "Zigzagoon", 24, 26), W(264, "Linoone", 26, 26), W(278, "Wingull", 25, 27), W(309, "Electrike", 24, 26), W(310, "Manectric", 26, 26), W(352, "Kecleon", 25, 25));
        AddWild(result, at["route-118"], "Route 118", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-119"], "Route 119", EncounterMethod.Grass,
            W(43, "Oddish", 24, 27), W(263, "Zigzagoon", 25, 27), W(264, "Linoone", 25, 27), W(352, "Kecleon", 25, 25), W(357, "Tropius", 25, 27));
        AddWild(result, at["route-119"], "Route 119", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-120"], "Route 120", EncounterMethod.Grass,
            W(43, "Oddish", 25, 27), W(183, "Marill", 25, 27), W(263, "Zigzagoon", 25, 25), W(264, "Linoone", 25, 27), W(283, "Surskit", 25, 25), W(352, "Kecleon", 25, 25), W(359, "Absol", 25, 27));
        AddWild(result, at["route-120"], "Route 120", EncounterMethod.Surf,
            W(183, "Marill", 5, 35), W(283, "Surskit", 20, 30));
        AddWild(result, at["route-121"], "Route 121", EncounterMethod.Grass,
            W(43, "Oddish", 26, 28), W(44, "Gloom", 28, 28), W(263, "Zigzagoon", 26, 26), W(264, "Linoone", 26, 28), W(278, "Wingull", 26, 28), W(352, "Kecleon", 25, 25), W(355, "Duskull", 26, 28));
        AddWild(result, at["route-121"], "Route 121", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-122"], "Route 122", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-123"], "Route 123", EncounterMethod.Grass,
            W(43, "Oddish", 26, 28), W(44, "Gloom", 28, 28), W(263, "Zigzagoon", 26, 26), W(264, "Linoone", 26, 28), W(278, "Wingull", 26, 28), W(352, "Kecleon", 25, 25), W(355, "Duskull", 26, 28));
        AddWild(result, at["route-123"], "Route 123", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-124"], "Route 124", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-125"], "Route 125", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-126"], "Route 126", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-127"], "Route 127", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-128"], "Route 128", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-129"], "Route 129", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30), W(321, "Wailord", 35, 40));
        AddWild(result, at["route-130"], "Route 130", EncounterMethod.Grass,
            W(360, "Wynaut", 5, 50));
        AddWild(result, at["route-130"], "Route 130", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-131"], "Route 131", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-132"], "Route 132", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-133"], "Route 133", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-134"], "Route 134", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["rusturf"], "Rusturf Tunnel", EncounterMethod.Cave,
            W(293, "Whismur", 5, 8));
        AddWild(result, at["safari-zone"], "Safari Zone (RSE)", EncounterMethod.Grass,
            W(25, "Pikachu", 25, 27), W(43, "Oddish", 25, 29), W(44, "Gloom", 25, 31), W(84, "Doduo", 25, 29), W(85, "Dodrio", 29, 31), W(111, "Rhyhorn", 27, 29), W(127, "Pinsir", 27, 29), W(177, "Natu", 25, 29), W(178, "Xatu", 29, 31), W(202, "Wobbuffet", 27, 29), W(203, "Girafarig", 25, 27), W(214, "Heracross", 27, 29), W(231, "Phanpy", 27, 29));
        AddWild(result, at["safari-zone"], "Safari Zone (RSE)", EncounterMethod.Surf,
            W(54, "Psyduck", 20, 35), W(55, "Golduck", 25, 40));
        AddWild(result, at["seafloor-cavern"], "Seafloor Cavern", EncounterMethod.Cave,
            W(41, "Zubat", 28, 35), W(42, "Golbat", 33, 36));
        AddWild(result, at["seafloor-cavern"], "Seafloor Cavern", EncounterMethod.Surf,
            W(41, "Zubat", 5, 35), W(42, "Golbat", 30, 35), W(72, "Tentacool", 5, 35));
        AddWild(result, at["shoal-cave"], "Shoal Cave", EncounterMethod.Cave,
            W(41, "Zubat", 26, 32), W(42, "Golbat", 30, 32), W(361, "Snorunt", 26, 30), W(363, "Spheal", 26, 32));
        AddWild(result, at["shoal-cave"], "Shoal Cave", EncounterMethod.Surf,
            W(41, "Zubat", 5, 35), W(72, "Tentacool", 5, 35), W(363, "Spheal", 25, 35));
        AddWild(result, at["sky-pillar"], "Sky Pillar", EncounterMethod.Cave,
            W(42, "Golbat", 48, 56), W(303, "Mawile", 48, 56), W(334, "Altaria", 54, 60), W(344, "Claydol", 47, 56), W(356, "Dusclops", 48, 56));
        AddWild(result, at["route-109"], "Slateport City", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-126"], "Sootopolis City", EncounterMethod.Surf,
            W(129, "Magikarp", 5, 35));
        AddWild(result, at["underwater"], "Underwater (Route 124)", EncounterMethod.Surf,
            W(170, "Chinchou", 20, 30), W(366, "Clamperl", 20, 35), W(369, "Relicanth", 30, 35));
        AddWild(result, at["underwater"], "Underwater (Route 126)", EncounterMethod.Surf,
            W(170, "Chinchou", 20, 30), W(366, "Clamperl", 20, 35), W(369, "Relicanth", 30, 35));
        AddWild(result, at["victory-road"], "Victory Road (RSE)", EncounterMethod.Cave,
            W(41, "Zubat", 36, 36), W(42, "Golbat", 38, 44), W(293, "Whismur", 36, 36), W(294, "Loudred", 40, 40), W(296, "Makuhita", 36, 36), W(297, "Hariyama", 38, 42), W(303, "Mawile", 40, 44), W(304, "Aron", 36, 36), W(305, "Lairon", 40, 44), W(307, "Meditite", 38, 38), W(308, "Medicham", 40, 44));
        AddWild(result, at["victory-road"], "Victory Road (RSE)", EncounterMethod.Surf,
            W(42, "Golbat", 25, 40));
    }

    /// <summary>Sapphire's wild table, emitted from PKHeX rather than typed by hand.</summary>
    private static void AddSapphire(
        List<EncounterCandidate> result,
        IReadOnlyDictionary<string, StoryMilestone> at)
    {
        AddWild(result, at["abandoned-ship"], "Abandoned Ship", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(73, "Tentacruel", 30, 35));
        AddWild(result, at["dewford"], "Dewford Town", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["ever-grande"], "Ever Grande City", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["fiery-path"], "Fiery Path", EncounterMethod.Cave,
            W(66, "Machop", 15, 16), W(88, "Grimer", 15, 16), W(109, "Koffing", 14, 14), W(218, "Slugma", 15, 15), W(322, "Numel", 15, 16), W(324, "Torkoal", 14, 16));
        AddWild(result, at["granite-cave"], "Granite Cave", EncounterMethod.Cave,
            W(41, "Zubat", 7, 11), W(63, "Abra", 8, 10), W(74, "Geodude", 6, 9), W(296, "Makuhita", 6, 11), W(302, "Sableye", 9, 12), W(304, "Aron", 7, 12));
        AddWild(result, at["jagged-pass"], "Jagged Pass", EncounterMethod.Grass,
            W(66, "Machop", 20, 22), W(322, "Numel", 20, 22), W(325, "Spoink", 20, 22));
        AddWild(result, at["route-121"], "Lilycove City", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["meteor-falls"], "Meteor Falls", EncounterMethod.Cave,
            W(41, "Zubat", 14, 20), W(42, "Golbat", 33, 40), W(337, "Lunatone", 14, 39), W(371, "Bagon", 25, 35));
        AddWild(result, at["meteor-falls"], "Meteor Falls", EncounterMethod.Surf,
            W(41, "Zubat", 5, 35), W(42, "Golbat", 30, 35), W(337, "Lunatone", 5, 35));
        AddWild(result, at["route-127"], "Mossdeep City", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["mt-pyre"], "Mt. Pyre", EncounterMethod.Cave,
            W(37, "Vulpix", 25, 29), W(278, "Wingull", 26, 28), W(307, "Meditite", 27, 29), W(353, "Shuppet", 22, 30), W(355, "Duskull", 25, 30), W(358, "Chimecho", 28, 28));
        AddWild(result, at["new-mauville"], "New Mauville", EncounterMethod.Cave,
            W(81, "Magnemite", 22, 26), W(82, "Magneton", 26, 26), W(100, "Voltorb", 22, 26), W(101, "Electrode", 26, 26));
        AddWild(result, at["pacifidlog"], "Pacifidlog Town", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-104"], "Petalburg City", EncounterMethod.Surf,
            W(183, "Marill", 5, 35));
        AddWild(result, at["petalburg-woods"], "Petalburg Woods", EncounterMethod.Grass,
            W(263, "Zigzagoon", 5, 6), W(265, "Wurmple", 5, 6), W(266, "Silcoon", 5, 5), W(268, "Cascoon", 5, 5), W(276, "Taillow", 5, 6), W(285, "Shroomish", 5, 6), W(287, "Slakoth", 5, 6));
        AddWild(result, at["route-101"], "Route 101", EncounterMethod.Grass,
            W(261, "Poochyena", 2, 3), W(263, "Zigzagoon", 2, 3), W(265, "Wurmple", 2, 3));
        AddWild(result, at["route-102"], "Route 102", EncounterMethod.Grass,
            W(261, "Poochyena", 3, 4), W(263, "Zigzagoon", 3, 4), W(265, "Wurmple", 3, 4), W(270, "Lotad", 3, 4), W(280, "Ralts", 4, 4), W(283, "Surskit", 3, 3));
        AddWild(result, at["route-102"], "Route 102", EncounterMethod.Surf,
            W(183, "Marill", 5, 35), W(283, "Surskit", 20, 30));
        AddWild(result, at["route-103"], "Route 103", EncounterMethod.Grass,
            W(261, "Poochyena", 2, 4), W(263, "Zigzagoon", 2, 4), W(278, "Wingull", 2, 4));
        AddWild(result, at["route-103"], "Route 103", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-104"], "Route 104", EncounterMethod.Grass,
            W(263, "Zigzagoon", 4, 5), W(265, "Wurmple", 4, 5), W(276, "Taillow", 4, 5), W(278, "Wingull", 3, 5));
        AddWild(result, at["route-104"], "Route 104", EncounterMethod.Surf,
            W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-118"], "Route 105", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["dewford"], "Route 106", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-109"], "Route 107", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["abandoned-ship"], "Route 108", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-109"], "Route 109", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-110"], "Route 110", EncounterMethod.Grass,
            W(43, "Oddish", 13, 13), W(263, "Zigzagoon", 12, 12), W(278, "Wingull", 12, 12), W(309, "Electrike", 12, 13), W(311, "Plusle", 13, 13), W(312, "Minun", 12, 13), W(316, "Gulpin", 12, 13));
        AddWild(result, at["route-110"], "Route 110", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-111"], "Route 111", EncounterMethod.Grass,
            W(27, "Sandshrew", 19, 21), W(328, "Trapinch", 19, 21), W(331, "Cacnea", 19, 21), W(343, "Baltoy", 20, 22));
        AddWild(result, at["route-111"], "Route 111", EncounterMethod.Surf,
            W(183, "Marill", 5, 35), W(283, "Surskit", 20, 30));
        AddWild(result, at["route-112"], "Route 112", EncounterMethod.Grass,
            W(66, "Machop", 14, 16), W(322, "Numel", 14, 16));
        AddWild(result, at["route-113"], "Route 113", EncounterMethod.Grass,
            W(27, "Sandshrew", 14, 16), W(227, "Skarmory", 16, 16), W(327, "Spinda", 14, 16));
        AddWild(result, at["route-114"], "Route 114", EncounterMethod.Grass,
            W(270, "Lotad", 15, 16), W(271, "Lombre", 16, 18), W(283, "Surskit", 15, 15), W(333, "Swablu", 15, 17), W(336, "Seviper", 15, 17));
        AddWild(result, at["route-114"], "Route 114", EncounterMethod.Surf,
            W(183, "Marill", 5, 35), W(283, "Surskit", 20, 30));
        AddWild(result, at["route-115"], "Route 115", EncounterMethod.Grass,
            W(39, "Jigglypuff", 24, 25), W(276, "Taillow", 23, 25), W(277, "Swellow", 25, 25), W(278, "Wingull", 24, 26), W(333, "Swablu", 23, 25));
        AddWild(result, at["route-115"], "Route 115", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-116"], "Route 116", EncounterMethod.Grass,
            W(263, "Zigzagoon", 6, 8), W(276, "Taillow", 6, 8), W(290, "Nincada", 6, 7), W(293, "Whismur", 6, 7), W(300, "Skitty", 7, 8));
        AddWild(result, at["route-117"], "Route 117", EncounterMethod.Grass,
            W(43, "Oddish", 13, 13), W(183, "Marill", 13, 13), W(263, "Zigzagoon", 13, 14), W(283, "Surskit", 13, 13), W(313, "Volbeat", 13, 14), W(314, "Illumise", 13, 13), W(315, "Roselia", 13, 14));
        AddWild(result, at["route-117"], "Route 117", EncounterMethod.Surf,
            W(183, "Marill", 5, 35), W(283, "Surskit", 20, 30));
        AddWild(result, at["route-118"], "Route 118", EncounterMethod.Grass,
            W(263, "Zigzagoon", 24, 26), W(264, "Linoone", 26, 26), W(278, "Wingull", 25, 27), W(309, "Electrike", 24, 26), W(310, "Manectric", 26, 26), W(352, "Kecleon", 25, 25));
        AddWild(result, at["route-118"], "Route 118", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-119"], "Route 119", EncounterMethod.Grass,
            W(43, "Oddish", 24, 27), W(263, "Zigzagoon", 25, 27), W(264, "Linoone", 25, 27), W(352, "Kecleon", 25, 25), W(357, "Tropius", 25, 27));
        AddWild(result, at["route-119"], "Route 119", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-120"], "Route 120", EncounterMethod.Grass,
            W(43, "Oddish", 25, 27), W(183, "Marill", 25, 27), W(263, "Zigzagoon", 25, 25), W(264, "Linoone", 25, 27), W(283, "Surskit", 25, 25), W(352, "Kecleon", 25, 25), W(359, "Absol", 25, 27));
        AddWild(result, at["route-120"], "Route 120", EncounterMethod.Surf,
            W(183, "Marill", 5, 35), W(283, "Surskit", 20, 30));
        AddWild(result, at["route-121"], "Route 121", EncounterMethod.Grass,
            W(43, "Oddish", 26, 28), W(44, "Gloom", 28, 28), W(263, "Zigzagoon", 26, 26), W(264, "Linoone", 26, 28), W(278, "Wingull", 26, 28), W(352, "Kecleon", 25, 25), W(353, "Shuppet", 26, 28));
        AddWild(result, at["route-121"], "Route 121", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-122"], "Route 122", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-123"], "Route 123", EncounterMethod.Grass,
            W(43, "Oddish", 26, 28), W(44, "Gloom", 28, 28), W(263, "Zigzagoon", 26, 26), W(264, "Linoone", 26, 28), W(278, "Wingull", 26, 28), W(352, "Kecleon", 25, 25), W(353, "Shuppet", 26, 28));
        AddWild(result, at["route-123"], "Route 123", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-124"], "Route 124", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-125"], "Route 125", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-126"], "Route 126", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-127"], "Route 127", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-128"], "Route 128", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-129"], "Route 129", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30), W(321, "Wailord", 25, 30));
        AddWild(result, at["route-130"], "Route 130", EncounterMethod.Grass,
            W(360, "Wynaut", 5, 50));
        AddWild(result, at["route-130"], "Route 130", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-131"], "Route 131", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-132"], "Route 132", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-133"], "Route 133", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-134"], "Route 134", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["rusturf"], "Rusturf Tunnel", EncounterMethod.Cave,
            W(293, "Whismur", 5, 8));
        AddWild(result, at["safari-zone"], "Safari Zone (RSE)", EncounterMethod.Grass,
            W(25, "Pikachu", 25, 27), W(43, "Oddish", 25, 29), W(44, "Gloom", 25, 31), W(84, "Doduo", 25, 29), W(85, "Dodrio", 29, 31), W(111, "Rhyhorn", 27, 29), W(127, "Pinsir", 27, 29), W(177, "Natu", 25, 29), W(178, "Xatu", 29, 31), W(202, "Wobbuffet", 27, 29), W(203, "Girafarig", 25, 27), W(214, "Heracross", 27, 29), W(231, "Phanpy", 27, 29));
        AddWild(result, at["safari-zone"], "Safari Zone (RSE)", EncounterMethod.Surf,
            W(54, "Psyduck", 20, 35), W(55, "Golduck", 25, 40));
        AddWild(result, at["seafloor-cavern"], "Seafloor Cavern", EncounterMethod.Cave,
            W(41, "Zubat", 28, 35), W(42, "Golbat", 33, 36));
        AddWild(result, at["seafloor-cavern"], "Seafloor Cavern", EncounterMethod.Surf,
            W(41, "Zubat", 5, 35), W(42, "Golbat", 30, 35), W(72, "Tentacool", 5, 35));
        AddWild(result, at["shoal-cave"], "Shoal Cave", EncounterMethod.Cave,
            W(41, "Zubat", 26, 32), W(42, "Golbat", 30, 32), W(361, "Snorunt", 26, 30), W(363, "Spheal", 26, 32));
        AddWild(result, at["shoal-cave"], "Shoal Cave", EncounterMethod.Surf,
            W(41, "Zubat", 5, 35), W(72, "Tentacool", 5, 35), W(363, "Spheal", 25, 35));
        AddWild(result, at["sky-pillar"], "Sky Pillar", EncounterMethod.Cave,
            W(42, "Golbat", 48, 56), W(302, "Sableye", 48, 56), W(334, "Altaria", 54, 60), W(344, "Claydol", 47, 56), W(354, "Banette", 48, 56));
        AddWild(result, at["route-109"], "Slateport City", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, at["route-126"], "Sootopolis City", EncounterMethod.Surf,
            W(129, "Magikarp", 5, 35));
        AddWild(result, at["underwater"], "Underwater (Route 124)", EncounterMethod.Surf,
            W(170, "Chinchou", 20, 30), W(366, "Clamperl", 20, 35), W(369, "Relicanth", 30, 35));
        AddWild(result, at["underwater"], "Underwater (Route 126)", EncounterMethod.Surf,
            W(170, "Chinchou", 20, 30), W(366, "Clamperl", 20, 35), W(369, "Relicanth", 30, 35));
        AddWild(result, at["victory-road"], "Victory Road (RSE)", EncounterMethod.Cave,
            W(41, "Zubat", 36, 36), W(42, "Golbat", 38, 44), W(293, "Whismur", 36, 36), W(294, "Loudred", 40, 40), W(296, "Makuhita", 36, 36), W(297, "Hariyama", 38, 42), W(302, "Sableye", 40, 44), W(304, "Aron", 36, 36), W(305, "Lairon", 40, 44), W(307, "Meditite", 38, 38), W(308, "Medicham", 40, 44));
        AddWild(result, at["victory-road"], "Victory Road (RSE)", EncounterMethod.Surf,
            W(42, "Golbat", 25, 40));
    }

    private readonly record struct WildEncounter(
        int SpeciesId,
        string SpeciesName,
        int MinimumLevel,
        int MaximumLevel);
}
