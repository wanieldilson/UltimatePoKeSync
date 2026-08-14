using System.Collections.ObjectModel;
using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.GameData.Learnsets;

/// <summary>
/// The main-story acquisition timeline for Pokémon Emerald. Wild species and levels are a
/// pinned snapshot of PKHeX's Emerald table; the story order is curated here, because a
/// legality table says where a Pokémon exists and not when the player can reach it.
/// </summary>
/// <remarks>
/// <para>
/// The two halves are not equally solid, and the difference matters. The encounters are
/// PKHeX's and a test checks every one of them. The order is written from knowledge of the
/// game with nothing to check it against, so wherever it was uncertain the place was filed
/// behind the badge that certainly precedes it. Late costs a suggestion; early sends somebody
/// to a route they cannot open. See D-057.
/// </para>
/// <para>
/// Grass and Surf only. Fishing is left out: it needs a rod, which is an item this app cannot
/// see, and it would have tripled the pool with Tentacool and Magikarp. Ruby and Sapphire
/// share this region and are not mapped, because their encounters differ and nobody has run
/// one against this catalog.
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
    private static readonly Lazy<HoennEncounterCatalog> LazyInstance = new(() => new());

    private readonly IReadOnlyList<StoryMilestone> _milestones;
    private readonly IReadOnlyList<EncounterCandidate> _encounters;

    private HoennEncounterCatalog()
    {
        StoryMilestone route101 = Milestone("route-101", "Route 101", 10, 0,
            "After receiving your first Pokémon in Littleroot Town.");
        StoryMilestone route103 = Milestone("route-103", "Route 103", 20, 0,
            "After reaching Oldale Town.");
        StoryMilestone route102 = Milestone("route-102", "Route 102", 30, 0,
            "Heading west from Oldale Town toward Petalburg City.");
        StoryMilestone route104 = Milestone("route-104", "Route 104", 40, 0,
            "North of Petalburg City, on the way to Petalburg Woods.");
        StoryMilestone woods = Milestone("petalburg-woods", "Petalburg Woods", 50, 0,
            "Crossing Petalburg Woods toward Rustboro City.");
        StoryMilestone route116 = Milestone("route-116", "Route 116", 60, 1,
            "East of Rustboro City, after the Stone Badge.", 1);
        StoryMilestone rusturf = Milestone("rusturf", "Rusturf Tunnel", 70, 1,
            "Inside Rusturf Tunnel, during the Devon Goods errand.", 1);
        StoryMilestone dewford = Milestone("dewford", "Dewford Town", 80, 1,
            "After sailing to Dewford Town with Mr. Briney.", 1);
        StoryMilestone granite = Milestone("granite-cave", "Granite Cave", 90, 1,
            "Inside Granite Cave, reached from Dewford Town.", 1);
        StoryMilestone route109 = Milestone("route-109", "Route 109", 100, 2,
            "The beach at Slateport, after the Knuckle Badge.", 2);
        StoryMilestone route110 = Milestone("route-110", "Route 110", 110, 2,
            "North of Slateport City toward Mauville.", 2);
        StoryMilestone route117 = Milestone("route-117", "Route 117", 120, 3,
            "West of Mauville City, after the Dynamo Badge.", 3);
        StoryMilestone route111 = Milestone("route-111", "Route 111", 130, 3,
            "North of Mauville, at the edge of the desert.", 3);
        StoryMilestone route112 = Milestone("route-112", "Route 112", 140, 3,
            "On the slopes below Mt. Chimney.", 3);
        StoryMilestone jagged = Milestone("jagged-pass", "Jagged Pass", 150, 3,
            "Descending Jagged Pass toward Lavaridge Town.", 3);
        StoryMilestone route113 = Milestone("route-113", "Route 113", 160, 3,
            "In the ash fall north of Mt. Chimney.", 3);
        StoryMilestone route114 = Milestone("route-114", "Route 114", 170, 3,
            "West of Fallarbor Town.", 3);
        StoryMilestone meteor = Milestone("meteor-falls", "Meteor Falls", 180, 3,
            "Inside Meteor Falls, off Route 114.", 3);
        StoryMilestone route115 = Milestone("route-115", "Route 115", 190, 3,
            "The coast north of Rustboro City.", 3);
        StoryMilestone fiery = Milestone("fiery-path", "Fiery Path", 200, 4,
            "Through the Fiery Path, once Strength can move its boulders.", 4);
        StoryMilestone newMauville = Milestone("new-mauville", "New Mauville", 210, 4,
            "Inside New Mauville, after Wattson hands over the Basement Key.", 4);
        StoryMilestone route118 = Milestone("route-118", "Route 118", 220, 5,
            "East of Mauville, across the water Surf opens after the Balance Badge.", 5);
        StoryMilestone route119 = Milestone("route-119", "Route 119", 230, 5,
            "The tall grass on the way to the Weather Institute.", 5);
        StoryMilestone route120 = Milestone("route-120", "Route 120", 240, 6,
            "North of Fortree City, after the Feather Badge.", 6);
        StoryMilestone route121 = Milestone("route-121", "Route 121", 250, 6,
            "West of Lilycove City.", 6);
        StoryMilestone safari = Milestone("safari-zone", "Safari Zone", 260, 6,
            "Paying the entry fee at the Safari Zone on Route 121.", 6);
        StoryMilestone route122 = Milestone("route-122", "Route 122", 270, 6,
            "The water south of Route 121, below Mt. Pyre.", 6);
        StoryMilestone pyre = Milestone("mt-pyre", "Mt. Pyre", 280, 6,
            "Climbing Mt. Pyre.", 6);
        StoryMilestone route123 = Milestone("route-123", "Route 123", 290, 6,
            "East of Mt. Pyre.", 6);
        StoryMilestone magma = Milestone("magma-hideout", "Magma Hideout", 300, 6,
            "Following Team Magma into their hideout on Jagged Pass.", 6);
        StoryMilestone route124 = Milestone("route-124", "Route 124", 310, 6,
            "The sea east of Lilycove, on the way to Mossdeep.", 6);
        StoryMilestone route125 = Milestone("route-125", "Route 125", 320, 6,
            "The sea around Shoal Cave, north of Mossdeep.", 6);
        StoryMilestone shoal = Milestone("shoal-cave", "Shoal Cave", 330, 6,
            "Inside Shoal Cave, reached by Surf from Route 125.", 6);
        StoryMilestone route127 = Milestone("route-127", "Route 127", 340, 7,
            "The open sea south of Mossdeep, after the Mind Badge.", 7);
        StoryMilestone route126 = Milestone("route-126", "Route 126", 350, 7,
            "The sea around Sootopolis City.", 7);
        StoryMilestone underwater = Milestone("underwater", "Underwater", 360, 7,
            "Diving from Route 124 or Route 126, once Dive is available.", 7);
        StoryMilestone seafloor = Milestone("seafloor-cavern", "Seafloor Cavern", 370, 7,
            "Inside Seafloor Cavern, during the Team Aqua chase.", 7);
        StoryMilestone ship = Milestone("abandoned-ship", "Abandoned Ship", 380, 7,
            "Exploring the Abandoned Ship on Route 108.", 7);
        StoryMilestone route128 = Milestone("route-128", "Route 128", 390, 8,
            "The sea east of Sootopolis, after the Rain Badge.", 8);
        StoryMilestone route129 = Milestone("route-129", "Route 129", 400, 8,
            "The deep water on the way south.", 8);
        StoryMilestone route130 = Milestone("route-130", "Route 130", 410, 8,
            "The open sea near Pacifidlog Town.", 8);
        StoryMilestone route131 = Milestone("route-131", "Route 131", 420, 8,
            "The water below the Sky Pillar.", 8);
        StoryMilestone pacifidlog = Milestone("pacifidlog", "Pacifidlog Town", 430, 8,
            "The water around Pacifidlog Town.", 8);
        StoryMilestone skyPillar = Milestone("sky-pillar", "Sky Pillar", 440, 8,
            "Climbing the Sky Pillar.", 8);
        StoryMilestone route132 = Milestone("route-132", "Route 132", 450, 8,
            "The sea west of Ever Grande City.", 8);
        StoryMilestone route133 = Milestone("route-133", "Route 133", 460, 8,
            "Further along the sea toward Ever Grande.", 8);
        StoryMilestone route134 = Milestone("route-134", "Route 134", 470, 8,
            "The last stretch of sea before Ever Grande City.", 8);
        StoryMilestone everGrande = Milestone("ever-grande", "Ever Grande City", 480, 8,
            "The shore below Victory Road.", 8);
        StoryMilestone victoryRoad = Milestone("victory-road", "Victory Road", 490, 8,
            "Inside Victory Road, after all eight Badges.", 8);

        _milestones = Array.AsReadOnly<StoryMilestone>(
        [
            route101, route103, route102, route104, woods, route116, rusturf, dewford, granite,
            route109, route110, route117, route111, route112, jagged, route113, route114,
            meteor, route115, fiery, newMauville, route118, route119, route120, route121,
            safari, route122, pyre, route123, magma, route124, route125, shoal, route127,
            route126, underwater, seafloor, ship, route128, route129, route130, route131,
            pacifidlog, skyPillar, route132, route133, route134, everGrande, victoryRoad,
        ]);

        var result = new List<EncounterCandidate>();
        AddWild(result, ship, "Abandoned Ship", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(73, "Tentacruel", 30, 35));
        AddWild(result, dewford, "Dewford Town", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, everGrande, "Ever Grande City", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, fiery, "Fiery Path", EncounterMethod.Cave,
            W(66, "Machop", 15, 16), W(88, "Grimer", 14, 14), W(109, "Koffing", 15, 16), W(218, "Slugma", 15, 15), W(322, "Numel", 15, 16), W(324, "Torkoal", 14, 16));
        AddWild(result, granite, "Granite Cave", EncounterMethod.Cave,
            W(41, "Zubat", 7, 11), W(63, "Abra", 8, 10), W(74, "Geodude", 6, 9), W(296, "Makuhita", 6, 11), W(302, "Sableye", 9, 12), W(304, "Aron", 7, 12));
        AddWild(result, jagged, "Jagged Pass", EncounterMethod.Grass,
            W(66, "Machop", 20, 22), W(322, "Numel", 20, 22), W(325, "Spoink", 20, 22));
        AddWild(result, route121, "Lilycove City", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, magma, "Magma Hideout", EncounterMethod.Cave,
            W(74, "Geodude", 27, 30), W(75, "Graveler", 30, 33), W(324, "Torkoal", 28, 30));
        AddWild(result, meteor, "Meteor Falls", EncounterMethod.Cave,
            W(41, "Zubat", 14, 20), W(42, "Golbat", 33, 40), W(338, "Solrock", 14, 39), W(371, "Bagon", 25, 35));
        AddWild(result, meteor, "Meteor Falls", EncounterMethod.Surf,
            W(41, "Zubat", 5, 35), W(42, "Golbat", 30, 35), W(338, "Solrock", 5, 35));
        AddWild(result, route127, "Mossdeep City", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, pyre, "Mt. Pyre", EncounterMethod.Cave,
            W(37, "Vulpix", 25, 29), W(278, "Wingull", 26, 28), W(353, "Shuppet", 22, 30), W(355, "Duskull", 25, 30), W(358, "Chimecho", 28, 28));
        AddWild(result, newMauville, "New Mauville", EncounterMethod.Cave,
            W(81, "Magnemite", 22, 26), W(82, "Magneton", 26, 26), W(100, "Voltorb", 22, 26), W(101, "Electrode", 26, 26));
        AddWild(result, pacifidlog, "Pacifidlog Town", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, route104, "Petalburg City", EncounterMethod.Surf,
            W(183, "Marill", 5, 35));
        AddWild(result, woods, "Petalburg Woods", EncounterMethod.Grass,
            W(261, "Poochyena", 5, 6), W(265, "Wurmple", 5, 6), W(266, "Silcoon", 5, 5), W(268, "Cascoon", 5, 5), W(276, "Taillow", 5, 6), W(285, "Shroomish", 5, 6), W(287, "Slakoth", 5, 6));
        AddWild(result, route101, "Route 101", EncounterMethod.Grass,
            W(261, "Poochyena", 2, 3), W(263, "Zigzagoon", 2, 3), W(265, "Wurmple", 2, 3));
        AddWild(result, route102, "Route 102", EncounterMethod.Grass,
            W(261, "Poochyena", 3, 4), W(263, "Zigzagoon", 3, 4), W(265, "Wurmple", 3, 4), W(270, "Lotad", 3, 4), W(273, "Seedot", 3, 3), W(280, "Ralts", 4, 4));
        AddWild(result, route102, "Route 102", EncounterMethod.Surf,
            W(118, "Goldeen", 20, 30), W(183, "Marill", 5, 35));
        AddWild(result, route103, "Route 103", EncounterMethod.Grass,
            W(261, "Poochyena", 2, 4), W(263, "Zigzagoon", 3, 4), W(278, "Wingull", 2, 4));
        AddWild(result, route103, "Route 103", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, route104, "Route 104", EncounterMethod.Grass,
            W(183, "Marill", 4, 5), W(261, "Poochyena", 4, 5), W(265, "Wurmple", 4, 4), W(276, "Taillow", 4, 5), W(278, "Wingull", 3, 5));
        AddWild(result, route104, "Route 104", EncounterMethod.Surf,
            W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, route118, "Route 105", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, dewford, "Route 106", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, route109, "Route 107", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, ship, "Route 108", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, route109, "Route 109", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, route110, "Route 110", EncounterMethod.Grass,
            W(43, "Oddish", 13, 13), W(261, "Poochyena", 12, 12), W(278, "Wingull", 12, 12), W(309, "Electrike", 12, 13), W(311, "Plusle", 12, 13), W(312, "Minun", 13, 13), W(316, "Gulpin", 12, 13));
        AddWild(result, route110, "Route 110", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, route111, "Route 111", EncounterMethod.Grass,
            W(27, "Sandshrew", 19, 21), W(328, "Trapinch", 19, 21), W(331, "Cacnea", 20, 22), W(343, "Baltoy", 19, 21));
        AddWild(result, route111, "Route 111", EncounterMethod.Surf,
            W(118, "Goldeen", 20, 30), W(183, "Marill", 5, 35));
        AddWild(result, route112, "Route 112", EncounterMethod.Grass,
            W(183, "Marill", 14, 16), W(322, "Numel", 14, 16));
        AddWild(result, route113, "Route 113", EncounterMethod.Grass,
            W(218, "Slugma", 14, 16), W(227, "Skarmory", 16, 16), W(327, "Spinda", 14, 16));
        AddWild(result, route114, "Route 114", EncounterMethod.Grass,
            W(270, "Lotad", 15, 16), W(271, "Lombre", 16, 18), W(274, "Nuzleaf", 15, 15), W(333, "Swablu", 15, 17), W(336, "Seviper", 15, 17));
        AddWild(result, route114, "Route 114", EncounterMethod.Surf,
            W(118, "Goldeen", 20, 30), W(183, "Marill", 5, 35));
        AddWild(result, route115, "Route 115", EncounterMethod.Grass,
            W(39, "Jigglypuff", 24, 25), W(276, "Taillow", 23, 25), W(277, "Swellow", 25, 25), W(278, "Wingull", 24, 26), W(333, "Swablu", 23, 25));
        AddWild(result, route115, "Route 115", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, route116, "Route 116", EncounterMethod.Grass,
            W(63, "Abra", 7, 7), W(261, "Poochyena", 6, 8), W(276, "Taillow", 6, 8), W(290, "Nincada", 6, 7), W(293, "Whismur", 6, 6), W(300, "Skitty", 7, 8));
        AddWild(result, route117, "Route 117", EncounterMethod.Grass,
            W(43, "Oddish", 13, 14), W(183, "Marill", 13, 13), W(261, "Poochyena", 13, 14), W(273, "Seedot", 13, 13), W(313, "Volbeat", 13, 13), W(314, "Illumise", 13, 14));
        AddWild(result, route117, "Route 117", EncounterMethod.Surf,
            W(118, "Goldeen", 20, 30), W(183, "Marill", 5, 35));
        AddWild(result, route118, "Route 118", EncounterMethod.Grass,
            W(263, "Zigzagoon", 24, 26), W(264, "Linoone", 26, 26), W(278, "Wingull", 25, 27), W(309, "Electrike", 24, 26), W(310, "Manectric", 26, 26), W(352, "Kecleon", 25, 25));
        AddWild(result, route118, "Route 118", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, route119, "Route 119", EncounterMethod.Grass,
            W(43, "Oddish", 24, 27), W(263, "Zigzagoon", 25, 27), W(264, "Linoone", 25, 27), W(352, "Kecleon", 25, 25), W(357, "Tropius", 25, 27));
        AddWild(result, route119, "Route 119", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, route120, "Route 120", EncounterMethod.Grass,
            W(43, "Oddish", 25, 27), W(183, "Marill", 25, 27), W(261, "Poochyena", 25, 25), W(262, "Mightyena", 25, 27), W(273, "Seedot", 25, 25), W(352, "Kecleon", 25, 25), W(359, "Absol", 25, 27));
        AddWild(result, route120, "Route 120", EncounterMethod.Surf,
            W(118, "Goldeen", 20, 30), W(183, "Marill", 5, 35));
        AddWild(result, route121, "Route 121", EncounterMethod.Grass,
            W(43, "Oddish", 26, 28), W(44, "Gloom", 28, 28), W(261, "Poochyena", 26, 26), W(262, "Mightyena", 26, 28), W(278, "Wingull", 26, 28), W(352, "Kecleon", 25, 25), W(353, "Shuppet", 26, 28));
        AddWild(result, route121, "Route 121", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, route122, "Route 122", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, route123, "Route 123", EncounterMethod.Grass,
            W(43, "Oddish", 26, 28), W(44, "Gloom", 28, 28), W(261, "Poochyena", 26, 26), W(262, "Mightyena", 26, 28), W(278, "Wingull", 26, 28), W(352, "Kecleon", 25, 25), W(353, "Shuppet", 26, 28));
        AddWild(result, route123, "Route 123", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, route124, "Route 124", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, route125, "Route 125", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, route126, "Route 126", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, route127, "Route 127", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, route128, "Route 128", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, route129, "Route 129", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30), W(321, "Wailord", 25, 30));
        AddWild(result, route130, "Route 130", EncounterMethod.Grass,
            W(360, "Wynaut", 5, 50));
        AddWild(result, route130, "Route 130", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, route131, "Route 131", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, route132, "Route 132", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, route133, "Route 133", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, route134, "Route 134", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, rusturf, "Rusturf Tunnel", EncounterMethod.Cave,
            W(293, "Whismur", 5, 8));
        AddWild(result, safari, "Safari Zone (RSE)", EncounterMethod.Grass,
            W(25, "Pikachu", 25, 27), W(43, "Oddish", 25, 29), W(44, "Gloom", 25, 31), W(84, "Doduo", 25, 29), W(85, "Dodrio", 29, 31), W(111, "Rhyhorn", 27, 29), W(127, "Pinsir", 27, 29), W(163, "Hoothoot", 35, 35), W(165, "Ledyba", 33, 33), W(167, "Spinarak", 33, 33), W(177, "Natu", 25, 29), W(178, "Xatu", 29, 31), W(179, "Mareep", 34, 36), W(190, "Aipom", 33, 35), W(191, "Sunkern", 33, 35), W(202, "Wobbuffet", 27, 29), W(203, "Girafarig", 25, 27), W(204, "Pineco", 34, 34), W(207, "Gligar", 37, 40), W(209, "Snubbull", 34, 34), W(214, "Heracross", 27, 29), W(216, "Teddiursa", 34, 36), W(228, "Houndour", 36, 39), W(231, "Phanpy", 27, 29), W(234, "Stantler", 36, 39), W(241, "Miltank", 37, 40));
        AddWild(result, safari, "Safari Zone (RSE)", EncounterMethod.Surf,
            W(54, "Psyduck", 20, 35), W(55, "Golduck", 25, 40), W(183, "Marill", 25, 35), W(194, "Wooper", 25, 30), W(195, "Quagsire", 35, 40));
        AddWild(result, seafloor, "Seafloor Cavern", EncounterMethod.Cave,
            W(41, "Zubat", 28, 35), W(42, "Golbat", 33, 36));
        AddWild(result, seafloor, "Seafloor Cavern", EncounterMethod.Surf,
            W(41, "Zubat", 5, 35), W(42, "Golbat", 30, 35), W(72, "Tentacool", 5, 35));
        AddWild(result, shoal, "Shoal Cave", EncounterMethod.Cave,
            W(41, "Zubat", 26, 32), W(42, "Golbat", 30, 32), W(361, "Snorunt", 26, 30), W(363, "Spheal", 26, 32));
        AddWild(result, shoal, "Shoal Cave", EncounterMethod.Surf,
            W(41, "Zubat", 5, 35), W(72, "Tentacool", 5, 35), W(363, "Spheal", 25, 35));
        AddWild(result, skyPillar, "Sky Pillar", EncounterMethod.Cave,
            W(42, "Golbat", 34, 35), W(302, "Sableye", 33, 34), W(334, "Altaria", 38, 39), W(344, "Claydol", 36, 38), W(354, "Banette", 37, 38));
        AddWild(result, route109, "Slateport City", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 35), W(278, "Wingull", 10, 30), W(279, "Pelipper", 25, 30));
        AddWild(result, route126, "Sootopolis City", EncounterMethod.Surf,
            W(129, "Magikarp", 5, 35));
        AddWild(result, underwater, "Underwater (Route 124)", EncounterMethod.Surf,
            W(170, "Chinchou", 20, 30), W(366, "Clamperl", 20, 35), W(369, "Relicanth", 30, 35));
        AddWild(result, underwater, "Underwater (Route 126)", EncounterMethod.Surf,
            W(170, "Chinchou", 20, 30), W(366, "Clamperl", 20, 35), W(369, "Relicanth", 30, 35));
        AddWild(result, victoryRoad, "Victory Road (RSE)", EncounterMethod.Cave,
            W(41, "Zubat", 36, 36), W(42, "Golbat", 38, 44), W(293, "Whismur", 36, 36), W(294, "Loudred", 40, 40), W(296, "Makuhita", 36, 36), W(297, "Hariyama", 38, 42), W(302, "Sableye", 40, 44), W(303, "Mawile", 38, 44), W(304, "Aron", 36, 36), W(305, "Lairon", 40, 44));
        AddWild(result, victoryRoad, "Victory Road (RSE)", EncounterMethod.Surf,
            W(42, "Golbat", 25, 40));

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

    public static HoennEncounterCatalog Instance => LazyInstance.Value;

    public string SourceName =>
        "PKHeX.Core 26.7.7 encounter snapshot + curated Pokémon Emerald story timeline";

    public bool Supports(GameIdentity game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return game.Generation == PokemonGeneration.Gen3 && IsEmerald(game.GameCode);
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

    /// <summary>Every language of Emerald. Ruby and Sapphire are a different table.</summary>
    private static bool IsEmerald(string gameCode) => gameCode is
        "BPEE" or "BPEF" or "BPED" or "BPES" or "BPEI" or "BPEJ";

    private void EnsureSupported(GameIdentity game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (!Supports(game))
        {
            throw new NotSupportedException(
                $"No Pokémon Emerald encounter timeline is available for {game}.");
        }
    }

    private readonly record struct WildEncounter(
        int SpeciesId,
        string SpeciesName,
        int MinimumLevel,
        int MaximumLevel);
}
