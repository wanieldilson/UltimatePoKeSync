using System.Collections.ObjectModel;
using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.GameData.Learnsets;

/// <summary>Which of the sequels a catalog answers for.</summary>
public enum UnovaSequel
{
    BlackTwo = 0,
    WhiteTwo = 1,
}

/// <summary>
/// The main-story acquisition timeline for Pokémon Black 2 and White 2.
/// </summary>
/// <remarks>
/// <para>
/// Unlike every other catalog here, the checkpoints are badges and nothing else: nine of them,
/// one per Gym. Unova is rebuilt in these two games, the Gym order is different, and half the
/// western region that Black and White open early is reached near the end. That is the part of
/// this project I know least well, and a route-level timeline written from a shaky memory would
/// be precise and wrong. Coarse and true was the better trade. See D-060.
/// </para>
/// <para>
/// Each place therefore sits at the badge count by which it is certainly reachable, and
/// anything uncertain sits at the last checkpoint before the League. The cost is that a whole
/// stretch of the map opens at once rather than route by route.
/// </para>
/// <para>
/// Grass and Surf. Fishing is out for the reason it is out of Hoenn. Swarms are out too:
/// in these two games they are the post-game rustling grass, and they put a Lv.40-55
/// Sudowoodo on Route 20, which is a starting-area route. See D-062.
/// </para>
/// <para>Places PKHeX knows and this catalog will not suggest:</para>
/// <list type="bullet">
/// <item>Guidance Chamber: post-game legendary chambers.</item>
/// <item>Hidden Grotto: its contents rotate on a timer nothing here can read.</item>
/// <item>Iceberg Chamber: post-game legendary chambers.</item>
/// <item>Iron Chamber: post-game legendary chambers.</item>
/// <item>Marvelous Bridge: post-game.</item>
/// <item>Nature Preserve: post-game, and reached only through an event.</item>
/// <item>P2 Laboratory: post-game.</item>
/// <item>Rock Peak Chamber: post-game legendary chambers.</item>
/// <item>Victory Road (B/W): the old Victory Road, which is not on this game's path.</item>
/// </list>
/// </remarks>
public sealed class UnovaSequelEncounterCatalog : IEncounterCatalog
{
    private static readonly Lazy<UnovaSequelEncounterCatalog> LazyBlackTwo =
        new(() => new(UnovaSequel.BlackTwo));

    private static readonly Lazy<UnovaSequelEncounterCatalog> LazyWhiteTwo =
        new(() => new(UnovaSequel.WhiteTwo));

    private readonly UnovaSequel _version;
    private readonly IReadOnlyList<StoryMilestone> _milestones;
    private readonly IReadOnlyList<EncounterCandidate> _encounters;

    private UnovaSequelEncounterCatalog(UnovaSequel version)
    {
        _version = version;

        StoryMilestone[] timeline =
        [
            Milestone("start", "Before the first Gym", 10, 0,
                "After leaving Aspertia City with your first Pokémon."),
            Milestone("basic", "After the Basic Badge", 20, 1,
                "After beating Cheren in Aspertia City.", 1),
            Milestone("toxic", "After the Toxic Badge", 30, 2,
                "After beating Roxie in Virbank City.", 2),
            Milestone("insect", "After the Insect Badge", 40, 3,
                "After beating Burgh in Castelia City.", 3),
            Milestone("bolt", "After the Bolt Badge", 50, 4,
                "After beating Elesa in Nimbasa City.", 4),
            Milestone("quake", "After the Quake Badge", 60, 5,
                "After beating Clay in Driftveil City.", 5),
            Milestone("jet", "After the Jet Badge", 70, 6,
                "After beating Skyla in Mistralton City.", 6),
            Milestone("legend", "After the Legend Badge", 80, 7,
                "After beating Drayden in Opelucid City.", 7),
            Milestone("wave", "After the Wave Badge", 90, 8,
                "After beating Marlon in Humilau City.", 8),
        ];

        _milestones = Array.AsReadOnly(timeline);
        Dictionary<string, StoryMilestone> at = timeline.ToDictionary(milestone => milestone.Id);

        var result = new List<EncounterCandidate>();
        if (version == UnovaSequel.BlackTwo)
        {
            AddBlackTwo(result, at);
        }
        else
        {
            AddWhiteTwo(result, at);
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

    public static UnovaSequelEncounterCatalog BlackTwo => LazyBlackTwo.Value;

    public static UnovaSequelEncounterCatalog WhiteTwo => LazyWhiteTwo.Value;

    public string SourceName =>
        $"PKHeX.Core 26.7.7 encounter snapshot + curated Pokémon {Label} story timeline";

    public bool Supports(GameIdentity game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return game.Generation == PokemonGeneration.Gen5 && Codes().Contains(game.GameCode);
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

    private string Label => _version == UnovaSequel.BlackTwo ? "Black 2" : "White 2";

    private string[] Codes() => _version == UnovaSequel.BlackTwo
        ? ["IREO", "IREE", "IREJ", "IREI", "IRES", "IREF", "IRED", "IREK"]
        : ["IRDO", "IRDE", "IRDJ", "IRDI", "IRDS", "IRDF", "IRDD", "IRDK"];

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
                $"No Pokémon {Label} encounter timeline is available for {game}.");
        }
    }

    /// <summary>Black 2's wild table, emitted from PKHeX rather than typed by hand.</summary>
    private static void AddBlackTwo(
        List<EncounterCandidate> result,
        IReadOnlyDictionary<string, StoryMilestone> at)
    {
        AddWild(result, at["legend"], "Abundant Shrine", EncounterMethod.Grass,
            W(37, "Vulpix", 34, 37), W(38, "Ninetales", 36, 36), W(55, "Golduck", 34, 38), W(183, "Marill", 34, 38), W(333, "Swablu", 33, 33), W(334, "Altaria", 36, 40), W(436, "Bronzor", 32, 32), W(437, "Bronzong", 36, 36), W(531, "Audino", 33, 36), W(546, "Cottonee", 33, 39), W(547, "Whimsicott", 36, 36), W(587, "Emolga", 34, 34));
        AddWild(result, at["legend"], "Abundant Shrine", EncounterMethod.Surf,
            W(183, "Marill", 25, 40), W(184, "Azumarill", 30, 40), W(550, "Basculin", 25, 40));
        AddWild(result, at["start"], "Aspertia City", EncounterMethod.Surf,
            W(550, "Basculin", 5, 15));
        AddWild(result, at["toxic"], "Castelia City", EncounterMethod.Grass,
            W(19, "Rattata", 15, 17), W(133, "Eevee", 18, 19), W(427, "Buneary", 15, 17), W(428, "Lopunny", 18, 18), W(519, "Pidove", 15, 17), W(531, "Audino", 15, 18), W(546, "Cottonee", 15, 18), W(547, "Whimsicott", 18, 18));
        AddWild(result, at["toxic"], "Castelia Sewers", EncounterMethod.Cave,
            W(19, "Rattata", 14, 17), W(41, "Zubat", 14, 17), W(88, "Grimer", 15, 17));
        AddWild(result, at["toxic"], "Castelia Sewers", EncounterMethod.Surf,
            W(88, "Grimer", 5, 20), W(89, "Muk", 5, 20));
        AddWild(result, at["jet"], "Celestial Tower", EncounterMethod.Cave,
            W(42, "Golbat", 31, 33), W(605, "Elgyem", 30, 33), W(607, "Litwick", 27, 33));
        AddWild(result, at["quake"], "Chargestone Cave", EncounterMethod.Cave,
            W(299, "Nosepass", 27, 30), W(525, "Boldore", 25, 28), W(529, "Drilbur", 25, 31), W(595, "Joltik", 25, 31), W(597, "Ferroseed", 26, 31), W(599, "Klink", 26, 31), W(602, "Tynamo", 28, 31));
        AddWild(result, at["quake"], "Clay Tunnel", EncounterMethod.Cave,
            W(95, "Onix", 54, 57), W(208, "Steelix", 57, 57), W(299, "Nosepass", 55, 57), W(305, "Lairon", 55, 57), W(525, "Boldore", 54, 56), W(527, "Woobat", 54, 54), W(530, "Excadrill", 54, 57), W(632, "Durant", 54, 54));
        AddWild(result, at["quake"], "Clay Tunnel", EncounterMethod.Surf,
            W(550, "Basculin", 45, 60));
        AddWild(result, at["insect"], "Desert Resort", EncounterMethod.Grass,
            W(27, "Sandshrew", 19, 20), W(328, "Trapinch", 21, 21), W(551, "Sandile", 18, 19), W(554, "Darumaka", 18, 19), W(556, "Maractus", 19, 19), W(557, "Dwebble", 19, 21), W(559, "Scraggy", 19, 19), W(561, "Sigilyph", 19, 19));
        AddWild(result, at["wave"], "Dragonspiral Tower", EncounterMethod.Cave,
            W(520, "Tranquill", 55, 66), W(521, "Unfezant", 57, 58), W(531, "Audino", 55, 58), W(583, "Vanillish", 55, 65), W(584, "Vanilluxe", 57, 58), W(586, "Sawsbuck", 55, 66), W(587, "Emolga", 56, 56), W(614, "Beartic", 57, 66), W(619, "Mienfoo", 66, 66), W(620, "Mienshao", 55, 66), W(621, "Druddigon", 56, 66), W(623, "Golurk", 55, 58));
        AddWild(result, at["wave"], "Dragonspiral Tower", EncounterMethod.Surf,
            W(550, "Basculin", 45, 60));
        AddWild(result, at["wave"], "Dreamyard", EncounterMethod.Grass,
            W(20, "Raticate", 57, 67), W(39, "Jigglypuff", 57, 65), W(40, "Wigglytuff", 59, 59), W(42, "Golbat", 57, 67), W(169, "Crobat", 59, 59), W(206, "Dunsparce", 57, 57), W(505, "Watchog", 56, 67), W(510, "Liepard", 56, 67), W(517, "Munna", 57, 66), W(518, "Musharna", 59, 59), W(531, "Audino", 56, 59));
        AddWild(result, at["quake"], "Driftveil Drawbridge", EncounterMethod.Grass,
            W(580, "Ducklett", 23, 26));
        AddWild(result, at["start"], "Floccesy Ranch", EncounterMethod.Grass,
            W(54, "Psyduck", 5, 5), W(179, "Mareep", 5, 5), W(206, "Dunsparce", 5, 5), W(298, "Azurill", 5, 5), W(447, "Riolu", 5, 7), W(504, "Patrat", 5, 5), W(506, "Lillipup", 4, 7), W(519, "Pidove", 7, 7), W(531, "Audino", 4, 7));
        AddWild(result, at["start"], "Floccesy Ranch", EncounterMethod.Surf,
            W(183, "Marill", 5, 15), W(184, "Azumarill", 5, 15), W(298, "Azurill", 5, 15), W(550, "Basculin", 5, 15));
        AddWild(result, at["wave"], "Giant Chasm", EncounterMethod.Cave,
            W(35, "Clefairy", 44, 52), W(36, "Clefable", 47, 47), W(114, "Tangela", 44, 50), W(132, "Ditto", 45, 52), W(215, "Sneasel", 44, 44), W(221, "Piloswine", 44, 51), W(225, "Delibird", 44, 49), W(279, "Pelipper", 45, 50), W(337, "Lunatone", 45, 51), W(338, "Solrock", 46, 51), W(375, "Metang", 45, 52), W(376, "Metagross", 47, 47), W(465, "Tangrowth", 47, 47), W(473, "Mamoswine", 47, 47), W(530, "Excadrill", 44, 47), W(531, "Audino", 44, 47), W(583, "Vanillish", 45, 52), W(584, "Vanilluxe", 47, 47));
        AddWild(result, at["wave"], "Giant Chasm", EncounterMethod.Surf,
            W(86, "Seel", 35, 50), W(87, "Dewgong", 40, 50), W(550, "Basculin", 35, 50));
        AddWild(result, at["wave"], "Humilau City", EncounterMethod.Surf,
            W(120, "Staryu", 30, 45), W(121, "Starmie", 35, 45), W(222, "Corsola", 35, 45), W(550, "Basculin", 35, 45), W(592, "Frillish", 30, 45), W(593, "Jellicent", 35, 45));
        AddWild(result, at["wave"], "Icirrus City", EncounterMethod.Grass,
            W(453, "Croagunk", 55, 56), W(536, "Palpitoad", 54, 57), W(588, "Karrablast", 57, 57), W(616, "Shelmet", 54, 54), W(618, "Stunfisk", 55, 56));
        AddWild(result, at["wave"], "Icirrus City", EncounterMethod.Surf,
            W(537, "Seismitoad", 50, 60), W(618, "Stunfisk", 45, 60));
        AddWild(result, at["bolt"], "Lostlorn Forest", EncounterMethod.Cave,
            W(214, "Heracross", 24, 26), W(315, "Roselia", 23, 26), W(407, "Roserade", 24, 24), W(415, "Combee", 22, 24), W(416, "Vespiquen", 24, 24), W(511, "Pansage", 22, 22), W(513, "Pansear", 22, 22), W(515, "Panpour", 22, 22), W(531, "Audino", 21, 23), W(541, "Swadloon", 21, 26), W(542, "Leavanny", 24, 24), W(543, "Venipede", 21, 21), W(544, "Whirlipede", 23, 23), W(546, "Cottonee", 21, 26), W(547, "Whimsicott", 24, 24), W(587, "Emolga", 22, 22));
        AddWild(result, at["bolt"], "Lostlorn Forest", EncounterMethod.Surf,
            W(418, "Buizel", 10, 30), W(419, "Floatzel", 10, 30), W(550, "Basculin", 10, 30));
        AddWild(result, at["quake"], "Mistralton Cave", EncounterMethod.Cave,
            W(304, "Aron", 29, 30), W(525, "Boldore", 27, 28), W(527, "Woobat", 27, 28), W(529, "Drilbur", 27, 30), W(610, "Axew", 29, 30));
        AddWild(result, at["wave"], "Moor of Icirrus", EncounterMethod.Grass,
            W(453, "Croagunk", 55, 56), W(536, "Palpitoad", 54, 57), W(588, "Karrablast", 57, 57), W(616, "Shelmet", 54, 54), W(618, "Stunfisk", 55, 56));
        AddWild(result, at["wave"], "Moor of Icirrus", EncounterMethod.Surf,
            W(537, "Seismitoad", 50, 60), W(618, "Stunfisk", 45, 60));
        AddWild(result, at["wave"], "Pinwheel Forest", EncounterMethod.Cave,
            W(193, "Yanma", 55, 63), W(288, "Vigoroth", 55, 65), W(289, "Slaking", 57, 57), W(454, "Toxicroak", 55, 63), W(469, "Yanmega", 57, 57), W(511, "Pansage", 55, 55), W(513, "Pansear", 55, 55), W(515, "Panpour", 55, 55), W(531, "Audino", 54, 64), W(533, "Gurdurr", 54, 64), W(536, "Palpitoad", 54, 65), W(537, "Seismitoad", 57, 57), W(538, "Throh", 57, 57), W(539, "Sawk", 55, 65), W(541, "Swadloon", 54, 64), W(544, "Whirlipede", 55, 65), W(545, "Scolipede", 57, 57), W(546, "Cottonee", 54, 65), W(547, "Whimsicott", 57, 57));
        AddWild(result, at["wave"], "Pinwheel Forest", EncounterMethod.Surf,
            W(183, "Marill", 45, 60), W(184, "Azumarill", 50, 60), W(550, "Basculin", 45, 60));
        AddWild(result, at["insect"], "Relic Castle", EncounterMethod.Cave,
            W(27, "Sandshrew", 19, 20), W(28, "Sandslash", 28, 29), W(343, "Baltoy", 27, 30), W(551, "Sandile", 18, 28), W(552, "Krokorok", 29, 30), W(562, "Yamask", 18, 30));
        AddWild(result, at["toxic"], "Relic Passage", EncounterMethod.Cave,
            W(19, "Rattata", 17, 17), W(20, "Raticate", 29, 29), W(95, "Onix", 16, 30), W(524, "Roggenrola", 16, 18), W(525, "Boldore", 27, 30), W(527, "Woobat", 16, 30), W(529, "Drilbur", 16, 30), W(532, "Timburr", 17, 18), W(533, "Gurdurr", 28, 30));
        AddWild(result, at["toxic"], "Relic Passage", EncounterMethod.Surf,
            W(183, "Marill", 10, 30), W(184, "Azumarill", 10, 30), W(550, "Basculin", 10, 30));
        AddWild(result, at["legend"], "Reversal Mountain", EncounterMethod.Cave,
            W(227, "Skarmory", 34, 38), W(325, "Spoink", 31, 31), W(326, "Grumpig", 33, 37), W(328, "Trapinch", 32, 34), W(329, "Vibrava", 38, 38), W(426, "Drifblim", 32, 37), W(451, "Skorupi", 31, 37), W(525, "Boldore", 32, 35), W(527, "Woobat", 32, 35), W(530, "Excadrill", 32, 35), W(531, "Audino", 31, 34));
        AddWild(result, at["wave"], "Route 1", EncounterMethod.Grass,
            W(39, "Jigglypuff", 57, 65), W(40, "Wigglytuff", 59, 59), W(206, "Dunsparce", 57, 57), W(505, "Watchog", 56, 67), W(507, "Herdier", 56, 67), W(508, "Stoutland", 59, 59), W(531, "Audino", 56, 59), W(560, "Scrafty", 66, 67));
        AddWild(result, at["wave"], "Route 1", EncounterMethod.Surf,
            W(550, "Basculin", 45, 60));
        AddWild(result, at["legend"], "Route 11", EncounterMethod.Grass,
            W(55, "Golduck", 36, 40), W(183, "Marill", 36, 40), W(184, "Azumarill", 39, 39), W(207, "Gligar", 37, 43), W(335, "Zangoose", 38, 42), W(336, "Seviper", 38, 42), W(472, "Gliscor", 39, 39), W(531, "Audino", 36, 39), W(587, "Emolga", 37, 37), W(588, "Karrablast", 36, 40), W(591, "Amoonguss", 37, 41), W(616, "Shelmet", 36, 43));
        AddWild(result, at["legend"], "Route 11", EncounterMethod.Surf,
            W(418, "Buizel", 25, 40), W(419, "Floatzel", 30, 40), W(550, "Basculin", 25, 40));
        AddWild(result, at["legend"], "Route 12", EncounterMethod.Grass,
            W(206, "Dunsparce", 36, 36), W(214, "Heracross", 36, 42), W(315, "Roselia", 35, 41), W(407, "Roserade", 38, 38), W(415, "Combee", 35, 39), W(416, "Vespiquen", 38, 38), W(520, "Tranquill", 36, 42), W(521, "Unfezant", 38, 38), W(531, "Audino", 35, 37), W(540, "Sewaddle", 36, 42), W(542, "Leavanny", 38, 38), W(587, "Emolga", 36, 36));
        AddWild(result, at["legend"], "Route 13", EncounterMethod.Grass,
            W(114, "Tangela", 34, 41), W(279, "Pelipper", 34, 41), W(337, "Lunatone", 36, 40), W(338, "Solrock", 36, 40), W(359, "Absol", 35, 41), W(426, "Drifblim", 34, 39), W(465, "Tangrowth", 37, 37), W(531, "Audino", 34, 37), W(587, "Emolga", 35, 35));
        AddWild(result, at["legend"], "Route 13", EncounterMethod.Surf,
            W(120, "Staryu", 25, 40), W(121, "Starmie", 30, 40), W(550, "Basculin", 30, 40), W(592, "Frillish", 25, 40), W(593, "Jellicent", 25, 40));
        AddWild(result, at["legend"], "Route 14", EncounterMethod.Grass,
            W(55, "Golduck", 34, 39), W(333, "Swablu", 33, 33), W(334, "Altaria", 36, 40), W(359, "Absol", 34, 40), W(426, "Drifblim", 34, 39), W(531, "Audino", 33, 36), W(587, "Emolga", 34, 34), W(619, "Mienfoo", 33, 39));
        AddWild(result, at["legend"], "Route 14", EncounterMethod.Surf,
            W(418, "Buizel", 15, 40), W(419, "Floatzel", 25, 40), W(550, "Basculin", 15, 40));
        AddWild(result, at["wave"], "Route 15", EncounterMethod.Grass,
            W(28, "Sandslash", 54, 64), W(207, "Gligar", 55, 65), W(247, "Pupitar", 55, 65), W(248, "Tyranitar", 57, 57), W(472, "Gliscor", 57, 57), W(531, "Audino", 54, 57), W(538, "Throh", 57, 57), W(539, "Sawk", 55, 65), W(560, "Scrafty", 55, 63), W(587, "Emolga", 55, 55));
        AddWild(result, at["bolt"], "Route 16", EncounterMethod.Grass,
            W(510, "Liepard", 22, 26), W(531, "Audino", 21, 24), W(568, "Trubbish", 21, 25), W(572, "Minccino", 21, 26), W(573, "Cinccino", 24, 24), W(574, "Gothita", 21, 26), W(587, "Emolga", 22, 22));
        AddWild(result, at["wave"], "Route 17", EncounterMethod.Surf,
            W(592, "Frillish", 45, 60), W(593, "Jellicent", 50, 60), W(594, "Alomomola", 45, 60));
        AddWild(result, at["wave"], "Route 18", EncounterMethod.Grass,
            W(206, "Dunsparce", 57, 57), W(357, "Tropius", 58, 66), W(455, "Carnivine", 58, 66), W(505, "Watchog", 56, 64), W(531, "Audino", 56, 59), W(538, "Throh", 59, 59), W(539, "Sawk", 57, 67), W(558, "Crustle", 57, 65), W(560, "Scrafty", 57, 66));
        AddWild(result, at["wave"], "Route 18", EncounterMethod.Surf,
            W(592, "Frillish", 45, 60), W(593, "Jellicent", 50, 60), W(594, "Alomomola", 45, 60));
        AddWild(result, at["start"], "Route 19", EncounterMethod.Grass,
            W(504, "Patrat", 2, 4), W(509, "Purrloin", 2, 4));
        AddWild(result, at["start"], "Route 19", EncounterMethod.Surf,
            W(550, "Basculin", 5, 15));
        AddWild(result, at["wave"], "Route 2", EncounterMethod.Grass,
            W(39, "Jigglypuff", 57, 57), W(40, "Wigglytuff", 59, 59), W(108, "Lickitung", 58, 58), W(206, "Dunsparce", 57, 57), W(463, "Lickilicky", 59, 59), W(505, "Watchog", 56, 59), W(507, "Herdier", 56, 59), W(508, "Stoutland", 59, 59), W(510, "Liepard", 57, 58), W(531, "Audino", 56, 59));
        AddWild(result, at["start"], "Route 20", EncounterMethod.Grass,
            W(191, "Sunkern", 2, 11), W(206, "Dunsparce", 3, 3), W(504, "Patrat", 3, 10), W(509, "Purrloin", 3, 11), W(519, "Pidove", 2, 10), W(531, "Audino", 2, 4), W(540, "Sewaddle", 2, 11), W(543, "Venipede", 10, 10));
        AddWild(result, at["start"], "Route 20", EncounterMethod.Surf,
            W(183, "Marill", 5, 15), W(184, "Azumarill", 5, 15), W(298, "Azurill", 5, 15), W(550, "Basculin", 5, 15));
        AddWild(result, at["wave"], "Route 21", EncounterMethod.Surf,
            W(223, "Remoraid", 35, 45), W(226, "Mantine", 35, 45), W(458, "Mantyke", 30, 45), W(592, "Frillish", 30, 45), W(593, "Jellicent", 30, 45), W(594, "Alomomola", 30, 45));
        AddWild(result, at["wave"], "Route 22", EncounterMethod.Grass,
            W(55, "Golduck", 40, 45), W(183, "Marill", 40, 47), W(184, "Azumarill", 42, 42), W(225, "Delibird", 39, 44), W(279, "Pelipper", 40, 45), W(337, "Lunatone", 41, 46), W(338, "Solrock", 41, 46), W(531, "Audino", 39, 42), W(587, "Emolga", 40, 40), W(591, "Amoonguss", 39, 44), W(619, "Mienfoo", 39, 47));
        AddWild(result, at["wave"], "Route 22", EncounterMethod.Surf,
            W(183, "Marill", 15, 40), W(184, "Azumarill", 25, 45), W(550, "Basculin", 15, 45));
        AddWild(result, at["wave"], "Route 23", EncounterMethod.Grass,
            W(55, "Golduck", 50, 55), W(207, "Gligar", 49, 54), W(472, "Gliscor", 51, 51), W(531, "Audino", 48, 51), W(538, "Throh", 51, 51), W(539, "Sawk", 48, 54), W(587, "Emolga", 49, 49), W(591, "Amoonguss", 49, 54), W(619, "Mienfoo", 48, 48), W(620, "Mienshao", 53, 53), W(626, "Bouffalant", 49, 56), W(629, "Vullaby", 47, 52));
        AddWild(result, at["wave"], "Route 23", EncounterMethod.Surf,
            W(418, "Buizel", 40, 55), W(419, "Floatzel", 45, 55), W(550, "Basculin", 40, 55));
        AddWild(result, at["wave"], "Route 3", EncounterMethod.Grass,
            W(193, "Yanma", 56, 64), W(469, "Yanmega", 58, 58), W(505, "Watchog", 55, 63), W(507, "Herdier", 57, 65), W(508, "Stoutland", 58, 58), W(509, "Purrloin", 57, 65), W(520, "Tranquill", 55, 65), W(521, "Unfezant", 58, 58), W(523, "Zebstrika", 56, 66), W(531, "Audino", 55, 58));
        AddWild(result, at["wave"], "Route 3", EncounterMethod.Surf,
            W(341, "Corphish", 45, 60), W(342, "Crawdaunt", 50, 60), W(550, "Basculin", 45, 60));
        AddWild(result, at["insect"], "Route 4", EncounterMethod.Grass,
            W(551, "Sandile", 14, 17), W(554, "Darumaka", 14, 17), W(559, "Scraggy", 17, 17), W(568, "Trubbish", 14, 17));
        AddWild(result, at["insect"], "Route 4", EncounterMethod.Surf,
            W(592, "Frillish", 5, 15), W(593, "Jellicent", 5, 20), W(594, "Alomomola", 5, 20));
        AddWild(result, at["bolt"], "Route 5", EncounterMethod.Grass,
            W(510, "Liepard", 22, 26), W(531, "Audino", 21, 24), W(568, "Trubbish", 21, 25), W(572, "Minccino", 21, 26), W(573, "Cinccino", 24, 24), W(574, "Gothita", 21, 26), W(587, "Emolga", 22, 22));
        AddWild(result, at["quake"], "Route 6", EncounterMethod.Grass,
            W(183, "Marill", 25, 28), W(184, "Azumarill", 26, 26), W(206, "Dunsparce", 25, 25), W(351, "Castform", 26, 26), W(520, "Tranquill", 26, 29), W(521, "Unfezant", 26, 26), W(531, "Audino", 23, 25), W(541, "Swadloon", 26, 29), W(542, "Leavanny", 26, 26), W(585, "Deerling", 23, 28), W(587, "Emolga", 24, 25), W(588, "Karrablast", 23, 26), W(590, "Foongus", 26, 29), W(616, "Shelmet", 23, 29));
        AddWild(result, at["quake"], "Route 6", EncounterMethod.Surf,
            W(183, "Marill", 10, 30), W(184, "Azumarill", 10, 30), W(550, "Basculin", 10, 30));
        AddWild(result, at["jet"], "Route 7", EncounterMethod.Grass,
            W(335, "Zangoose", 32, 35), W(336, "Seviper", 32, 35), W(505, "Watchog", 31, 36), W(520, "Tranquill", 30, 35), W(521, "Unfezant", 33, 33), W(523, "Zebstrika", 31, 36), W(531, "Audino", 30, 33), W(585, "Deerling", 30, 33), W(587, "Emolga", 31, 31), W(590, "Foongus", 33, 36), W(613, "Cubchoo", 30, 36));
        AddWild(result, at["wave"], "Route 8", EncounterMethod.Grass,
            W(453, "Croagunk", 55, 56), W(536, "Palpitoad", 54, 57), W(588, "Karrablast", 57, 57), W(616, "Shelmet", 54, 54), W(618, "Stunfisk", 55, 56));
        AddWild(result, at["wave"], "Route 8", EncounterMethod.Surf,
            W(536, "Palpitoad", 45, 60), W(537, "Seismitoad", 50, 60), W(618, "Stunfisk", 45, 60));
        AddWild(result, at["wave"], "Route 9", EncounterMethod.Grass,
            W(89, "Muk", 40, 44), W(510, "Liepard", 40, 44), W(531, "Audino", 37, 40), W(569, "Garbodor", 38, 44), W(572, "Minccino", 37, 43), W(573, "Cinccino", 40, 40), W(575, "Gothorita", 37, 43), W(576, "Gothitelle", 40, 40), W(587, "Emolga", 38, 38), W(624, "Pawniard", 38, 44));
        AddWild(result, at["wave"], "Seaside Cave", EncounterMethod.Cave,
            W(55, "Golduck", 34, 40), W(86, "Seel", 35, 35), W(213, "Shuckle", 41, 41), W(525, "Boldore", 35, 42), W(527, "Woobat", 34, 41), W(530, "Excadrill", 34, 42), W(602, "Tynamo", 37, 37), W(603, "Eelektrik", 42, 42));
        AddWild(result, at["wave"], "Seaside Cave", EncounterMethod.Surf,
            W(86, "Seel", 25, 40), W(87, "Dewgong", 30, 40), W(592, "Frillish", 25, 40), W(593, "Jellicent", 25, 40));
        AddWild(result, at["legend"], "Strange House", EncounterMethod.Cave,
            W(20, "Raticate", 32, 33), W(42, "Golbat", 32, 33), W(354, "Banette", 32, 34), W(574, "Gothita", 31, 31), W(575, "Gothorita", 33, 34), W(607, "Litwick", 31, 33));
        AddWild(result, at["wave"], "Striaton City", EncounterMethod.Surf,
            W(341, "Corphish", 45, 60), W(342, "Crawdaunt", 50, 60), W(550, "Basculin", 45, 60));
        AddWild(result, at["wave"], "Twist Mountain", EncounterMethod.Cave,
            W(95, "Onix", 54, 57), W(208, "Steelix", 57, 57), W(525, "Boldore", 54, 55), W(527, "Woobat", 54, 54), W(530, "Excadrill", 54, 57), W(533, "Gurdurr", 55, 55), W(614, "Beartic", 54, 56), W(615, "Cryogonal", 56, 57), W(631, "Heatmor", 55, 56), W(632, "Durant", 56, 57));
        AddWild(result, at["legend"], "Undella Bay", EncounterMethod.Surf,
            W(223, "Remoraid", 30, 40), W(226, "Mantine", 30, 40), W(320, "Wailmer", 25, 40), W(321, "Wailord", 30, 40), W(363, "Spheal", 25, 40), W(364, "Sealeo", 25, 40), W(365, "Walrein", 30, 40), W(458, "Mantyke", 25, 40), W(592, "Frillish", 25, 40), W(593, "Jellicent", 25, 40));
        AddWild(result, at["legend"], "Undella Town", EncounterMethod.Surf,
            W(120, "Staryu", 25, 40), W(121, "Starmie", 30, 40), W(550, "Basculin", 30, 40), W(592, "Frillish", 25, 40), W(593, "Jellicent", 25, 40));
        AddWild(result, at["quake"], "Underground Ruins", EncounterMethod.Cave,
            W(95, "Onix", 54, 57), W(208, "Steelix", 57, 57), W(299, "Nosepass", 55, 57), W(305, "Lairon", 55, 57), W(525, "Boldore", 54, 56), W(527, "Woobat", 54, 54), W(530, "Excadrill", 54, 57), W(632, "Durant", 54, 54));
        AddWild(result, at["wave"], "Victory Road (B2/W2)", EncounterMethod.Cave,
            W(95, "Onix", 41, 50), W(206, "Dunsparce", 49, 49), W(315, "Roselia", 48, 55), W(334, "Altaria", 49, 55), W(354, "Banette", 47, 50), W(407, "Roserade", 50, 50), W(520, "Tranquill", 47, 55), W(521, "Unfezant", 50, 50), W(525, "Boldore", 41, 50), W(530, "Excadrill", 41, 50), W(531, "Audino", 47, 50), W(533, "Gurdurr", 47, 55), W(538, "Throh", 48, 55), W(539, "Sawk", 50, 50), W(546, "Cottonee", 47, 55), W(547, "Whimsicott", 50, 50), W(621, "Druddigon", 47, 50), W(623, "Golurk", 48, 50), W(634, "Zweilous", 49, 50));
        AddWild(result, at["wave"], "Victory Road (B2/W2)", EncounterMethod.Surf,
            W(183, "Marill", 35, 60), W(184, "Azumarill", 40, 70), W(418, "Buizel", 35, 50), W(419, "Floatzel", 40, 50), W(550, "Basculin", 35, 70));
        AddWild(result, at["legend"], "Village Bridge", EncounterMethod.Grass,
            W(55, "Golduck", 36, 42), W(183, "Marill", 36, 42), W(184, "Azumarill", 39, 39), W(206, "Dunsparce", 37, 37), W(335, "Zangoose", 37, 43), W(336, "Seviper", 37, 43), W(531, "Audino", 36, 39), W(587, "Emolga", 37, 37));
        AddWild(result, at["legend"], "Village Bridge", EncounterMethod.Surf,
            W(131, "Lapras", 30, 40), W(183, "Marill", 25, 40), W(184, "Azumarill", 30, 40), W(550, "Basculin", 25, 40));
        AddWild(result, at["basic"], "Virbank City", EncounterMethod.Surf,
            W(592, "Frillish", 5, 15), W(593, "Jellicent", 5, 15), W(594, "Alomomola", 5, 15));
        AddWild(result, at["basic"], "Virbank Complex", EncounterMethod.Grass,
            W(58, "Growlithe", 11, 14), W(81, "Magnemite", 10, 13), W(109, "Koffing", 10, 14), W(240, "Magby", 10, 13), W(504, "Patrat", 10, 14), W(519, "Pidove", 10, 13), W(531, "Audino", 10, 13));
        AddWild(result, at["basic"], "Virbank Complex", EncounterMethod.Surf,
            W(592, "Frillish", 5, 15), W(593, "Jellicent", 5, 15), W(594, "Alomomola", 5, 15));
        AddWild(result, at["wave"], "Wellspring Cave", EncounterMethod.Cave,
            W(525, "Boldore", 55, 58), W(527, "Woobat", 55, 58), W(530, "Excadrill", 55, 58));
        AddWild(result, at["wave"], "Wellspring Cave", EncounterMethod.Surf,
            W(550, "Basculin", 45, 60));
    }

    /// <summary>White 2's wild table, emitted from PKHeX rather than typed by hand.</summary>
    private static void AddWhiteTwo(
        List<EncounterCandidate> result,
        IReadOnlyDictionary<string, StoryMilestone> at)
    {
        AddWild(result, at["legend"], "Abundant Shrine", EncounterMethod.Grass,
            W(37, "Vulpix", 34, 37), W(38, "Ninetales", 36, 36), W(55, "Golduck", 34, 38), W(183, "Marill", 34, 38), W(184, "Azumarill", 36, 36), W(333, "Swablu", 33, 33), W(334, "Altaria", 36, 40), W(436, "Bronzor", 32, 32), W(437, "Bronzong", 36, 36), W(531, "Audino", 33, 36), W(548, "Petilil", 33, 39), W(549, "Lilligant", 36, 36), W(587, "Emolga", 34, 34));
        AddWild(result, at["legend"], "Abundant Shrine", EncounterMethod.Surf,
            W(183, "Marill", 25, 40), W(184, "Azumarill", 30, 40), W(550, "Basculin", 25, 40));
        AddWild(result, at["start"], "Aspertia City", EncounterMethod.Surf,
            W(550, "Basculin", 5, 15));
        AddWild(result, at["toxic"], "Castelia City", EncounterMethod.Grass,
            W(19, "Rattata", 15, 17), W(133, "Eevee", 18, 19), W(300, "Skitty", 15, 17), W(301, "Delcatty", 18, 18), W(519, "Pidove", 15, 17), W(531, "Audino", 15, 18), W(548, "Petilil", 15, 18), W(549, "Lilligant", 18, 18));
        AddWild(result, at["toxic"], "Castelia Sewers", EncounterMethod.Cave,
            W(19, "Rattata", 14, 17), W(41, "Zubat", 14, 17), W(88, "Grimer", 15, 17));
        AddWild(result, at["toxic"], "Castelia Sewers", EncounterMethod.Surf,
            W(88, "Grimer", 5, 20), W(89, "Muk", 5, 20));
        AddWild(result, at["jet"], "Celestial Tower", EncounterMethod.Cave,
            W(42, "Golbat", 31, 33), W(605, "Elgyem", 30, 33), W(607, "Litwick", 27, 33));
        AddWild(result, at["quake"], "Chargestone Cave", EncounterMethod.Cave,
            W(299, "Nosepass", 27, 30), W(525, "Boldore", 25, 28), W(529, "Drilbur", 25, 31), W(595, "Joltik", 25, 31), W(597, "Ferroseed", 26, 31), W(599, "Klink", 26, 31), W(602, "Tynamo", 28, 31));
        AddWild(result, at["quake"], "Clay Tunnel", EncounterMethod.Cave,
            W(95, "Onix", 54, 57), W(208, "Steelix", 57, 57), W(299, "Nosepass", 55, 57), W(305, "Lairon", 55, 57), W(525, "Boldore", 54, 56), W(527, "Woobat", 54, 54), W(530, "Excadrill", 54, 57), W(632, "Durant", 54, 54));
        AddWild(result, at["quake"], "Clay Tunnel", EncounterMethod.Surf,
            W(550, "Basculin", 45, 60));
        AddWild(result, at["insect"], "Desert Resort", EncounterMethod.Grass,
            W(27, "Sandshrew", 19, 20), W(328, "Trapinch", 21, 21), W(551, "Sandile", 18, 19), W(554, "Darumaka", 18, 19), W(556, "Maractus", 19, 19), W(557, "Dwebble", 19, 21), W(559, "Scraggy", 19, 19), W(561, "Sigilyph", 19, 19));
        AddWild(result, at["wave"], "Dragonspiral Tower", EncounterMethod.Cave,
            W(520, "Tranquill", 55, 66), W(521, "Unfezant", 57, 58), W(531, "Audino", 55, 58), W(583, "Vanillish", 55, 65), W(584, "Vanilluxe", 57, 58), W(586, "Sawsbuck", 55, 66), W(587, "Emolga", 56, 56), W(614, "Beartic", 57, 66), W(619, "Mienfoo", 66, 66), W(620, "Mienshao", 55, 66), W(621, "Druddigon", 56, 66), W(623, "Golurk", 55, 58));
        AddWild(result, at["wave"], "Dragonspiral Tower", EncounterMethod.Surf,
            W(550, "Basculin", 45, 60));
        AddWild(result, at["wave"], "Dreamyard", EncounterMethod.Grass,
            W(20, "Raticate", 57, 67), W(39, "Jigglypuff", 57, 65), W(40, "Wigglytuff", 59, 59), W(42, "Golbat", 57, 67), W(169, "Crobat", 59, 59), W(206, "Dunsparce", 57, 57), W(505, "Watchog", 56, 67), W(510, "Liepard", 56, 67), W(517, "Munna", 57, 66), W(518, "Musharna", 59, 59), W(531, "Audino", 56, 59));
        AddWild(result, at["quake"], "Driftveil Drawbridge", EncounterMethod.Grass,
            W(580, "Ducklett", 23, 26));
        AddWild(result, at["start"], "Floccesy Ranch", EncounterMethod.Grass,
            W(54, "Psyduck", 5, 5), W(179, "Mareep", 5, 5), W(206, "Dunsparce", 5, 5), W(298, "Azurill", 5, 5), W(447, "Riolu", 5, 7), W(504, "Patrat", 5, 5), W(506, "Lillipup", 4, 7), W(519, "Pidove", 7, 7), W(531, "Audino", 4, 7));
        AddWild(result, at["start"], "Floccesy Ranch", EncounterMethod.Surf,
            W(183, "Marill", 5, 15), W(184, "Azumarill", 5, 15), W(298, "Azurill", 5, 15), W(550, "Basculin", 5, 15));
        AddWild(result, at["wave"], "Giant Chasm", EncounterMethod.Cave,
            W(35, "Clefairy", 44, 52), W(36, "Clefable", 47, 47), W(114, "Tangela", 44, 50), W(132, "Ditto", 45, 52), W(215, "Sneasel", 44, 44), W(221, "Piloswine", 44, 51), W(225, "Delibird", 44, 49), W(279, "Pelipper", 45, 50), W(337, "Lunatone", 45, 51), W(338, "Solrock", 46, 51), W(375, "Metang", 45, 52), W(376, "Metagross", 47, 47), W(465, "Tangrowth", 47, 47), W(473, "Mamoswine", 47, 47), W(530, "Excadrill", 44, 47), W(531, "Audino", 44, 47), W(583, "Vanillish", 45, 52), W(584, "Vanilluxe", 47, 47));
        AddWild(result, at["wave"], "Giant Chasm", EncounterMethod.Surf,
            W(86, "Seel", 35, 50), W(87, "Dewgong", 40, 50), W(550, "Basculin", 35, 50));
        AddWild(result, at["wave"], "Humilau City", EncounterMethod.Surf,
            W(120, "Staryu", 30, 45), W(121, "Starmie", 35, 45), W(222, "Corsola", 35, 45), W(550, "Basculin", 35, 45), W(592, "Frillish", 30, 45), W(593, "Jellicent", 35, 45));
        AddWild(result, at["wave"], "Icirrus City", EncounterMethod.Grass,
            W(453, "Croagunk", 55, 56), W(536, "Palpitoad", 54, 57), W(588, "Karrablast", 54, 54), W(616, "Shelmet", 57, 57), W(618, "Stunfisk", 55, 56));
        AddWild(result, at["wave"], "Icirrus City", EncounterMethod.Surf,
            W(537, "Seismitoad", 50, 60), W(618, "Stunfisk", 45, 60));
        AddWild(result, at["bolt"], "Lostlorn Forest", EncounterMethod.Cave,
            W(127, "Pinsir", 24, 26), W(315, "Roselia", 23, 26), W(407, "Roserade", 24, 24), W(415, "Combee", 22, 24), W(416, "Vespiquen", 24, 24), W(511, "Pansage", 22, 22), W(513, "Pansear", 22, 22), W(515, "Panpour", 22, 22), W(531, "Audino", 21, 23), W(541, "Swadloon", 21, 26), W(542, "Leavanny", 24, 24), W(543, "Venipede", 21, 21), W(544, "Whirlipede", 23, 23), W(548, "Petilil", 21, 26), W(549, "Lilligant", 24, 24), W(587, "Emolga", 22, 22));
        AddWild(result, at["bolt"], "Lostlorn Forest", EncounterMethod.Surf,
            W(418, "Buizel", 10, 30), W(419, "Floatzel", 10, 30), W(550, "Basculin", 10, 30));
        AddWild(result, at["quake"], "Mistralton Cave", EncounterMethod.Cave,
            W(304, "Aron", 29, 30), W(525, "Boldore", 27, 28), W(527, "Woobat", 27, 28), W(529, "Drilbur", 27, 30), W(610, "Axew", 29, 30));
        AddWild(result, at["wave"], "Moor of Icirrus", EncounterMethod.Grass,
            W(453, "Croagunk", 55, 56), W(536, "Palpitoad", 54, 57), W(588, "Karrablast", 54, 54), W(616, "Shelmet", 57, 57), W(618, "Stunfisk", 55, 56));
        AddWild(result, at["wave"], "Moor of Icirrus", EncounterMethod.Surf,
            W(537, "Seismitoad", 50, 60), W(618, "Stunfisk", 45, 60));
        AddWild(result, at["wave"], "Pinwheel Forest", EncounterMethod.Cave,
            W(193, "Yanma", 55, 63), W(288, "Vigoroth", 55, 65), W(289, "Slaking", 57, 57), W(454, "Toxicroak", 55, 63), W(469, "Yanmega", 57, 57), W(511, "Pansage", 55, 55), W(513, "Pansear", 55, 55), W(515, "Panpour", 55, 55), W(531, "Audino", 54, 57), W(533, "Gurdurr", 54, 64), W(536, "Palpitoad", 54, 65), W(537, "Seismitoad", 57, 57), W(538, "Throh", 55, 65), W(539, "Sawk", 57, 57), W(541, "Swadloon", 54, 64), W(544, "Whirlipede", 55, 65), W(545, "Scolipede", 57, 57), W(548, "Petilil", 54, 65), W(549, "Lilligant", 57, 57));
        AddWild(result, at["wave"], "Pinwheel Forest", EncounterMethod.Surf,
            W(183, "Marill", 45, 60), W(184, "Azumarill", 50, 60), W(550, "Basculin", 45, 60));
        AddWild(result, at["insect"], "Relic Castle", EncounterMethod.Cave,
            W(27, "Sandshrew", 19, 20), W(28, "Sandslash", 28, 29), W(343, "Baltoy", 27, 30), W(551, "Sandile", 18, 28), W(552, "Krokorok", 29, 30), W(562, "Yamask", 18, 30));
        AddWild(result, at["toxic"], "Relic Passage", EncounterMethod.Cave,
            W(19, "Rattata", 17, 17), W(20, "Raticate", 29, 29), W(95, "Onix", 16, 30), W(524, "Roggenrola", 16, 18), W(525, "Boldore", 27, 30), W(527, "Woobat", 16, 30), W(529, "Drilbur", 16, 30), W(532, "Timburr", 17, 18), W(533, "Gurdurr", 28, 30));
        AddWild(result, at["toxic"], "Relic Passage", EncounterMethod.Surf,
            W(183, "Marill", 10, 30), W(184, "Azumarill", 10, 30), W(550, "Basculin", 10, 30));
        AddWild(result, at["legend"], "Reversal Mountain", EncounterMethod.Cave,
            W(227, "Skarmory", 34, 38), W(322, "Numel", 31, 31), W(323, "Camerupt", 33, 37), W(328, "Trapinch", 32, 34), W(329, "Vibrava", 38, 38), W(426, "Drifblim", 32, 37), W(451, "Skorupi", 31, 37), W(525, "Boldore", 32, 35), W(527, "Woobat", 32, 35), W(530, "Excadrill", 32, 35), W(531, "Audino", 31, 34));
        AddWild(result, at["wave"], "Route 1", EncounterMethod.Grass,
            W(39, "Jigglypuff", 57, 65), W(40, "Wigglytuff", 59, 59), W(206, "Dunsparce", 57, 57), W(505, "Watchog", 56, 67), W(507, "Herdier", 56, 67), W(508, "Stoutland", 59, 59), W(531, "Audino", 56, 59), W(560, "Scrafty", 66, 67));
        AddWild(result, at["wave"], "Route 1", EncounterMethod.Surf,
            W(550, "Basculin", 45, 60));
        AddWild(result, at["legend"], "Route 11", EncounterMethod.Grass,
            W(55, "Golduck", 36, 40), W(183, "Marill", 36, 40), W(184, "Azumarill", 39, 39), W(207, "Gligar", 37, 43), W(335, "Zangoose", 38, 42), W(336, "Seviper", 38, 42), W(472, "Gliscor", 39, 39), W(531, "Audino", 36, 39), W(587, "Emolga", 37, 37), W(588, "Karrablast", 36, 43), W(591, "Amoonguss", 37, 41), W(616, "Shelmet", 36, 40));
        AddWild(result, at["legend"], "Route 11", EncounterMethod.Surf,
            W(418, "Buizel", 25, 40), W(419, "Floatzel", 30, 40), W(550, "Basculin", 25, 40));
        AddWild(result, at["legend"], "Route 12", EncounterMethod.Grass,
            W(127, "Pinsir", 36, 42), W(206, "Dunsparce", 36, 36), W(315, "Roselia", 35, 41), W(407, "Roserade", 38, 38), W(415, "Combee", 35, 39), W(416, "Vespiquen", 38, 38), W(520, "Tranquill", 36, 42), W(521, "Unfezant", 38, 38), W(531, "Audino", 35, 37), W(540, "Sewaddle", 36, 42), W(542, "Leavanny", 38, 38), W(587, "Emolga", 36, 36));
        AddWild(result, at["legend"], "Route 13", EncounterMethod.Grass,
            W(114, "Tangela", 34, 41), W(279, "Pelipper", 34, 41), W(337, "Lunatone", 36, 40), W(338, "Solrock", 36, 40), W(359, "Absol", 35, 41), W(426, "Drifblim", 34, 39), W(465, "Tangrowth", 37, 37), W(531, "Audino", 34, 37), W(587, "Emolga", 35, 35));
        AddWild(result, at["legend"], "Route 13", EncounterMethod.Surf,
            W(120, "Staryu", 25, 40), W(121, "Starmie", 30, 40), W(550, "Basculin", 30, 40), W(592, "Frillish", 25, 40), W(593, "Jellicent", 25, 40));
        AddWild(result, at["legend"], "Route 14", EncounterMethod.Grass,
            W(55, "Golduck", 34, 39), W(333, "Swablu", 33, 33), W(334, "Altaria", 36, 40), W(359, "Absol", 34, 40), W(426, "Drifblim", 34, 39), W(531, "Audino", 33, 36), W(587, "Emolga", 34, 34), W(619, "Mienfoo", 33, 39));
        AddWild(result, at["legend"], "Route 14", EncounterMethod.Surf,
            W(418, "Buizel", 25, 40), W(419, "Floatzel", 30, 40), W(550, "Basculin", 25, 40));
        AddWild(result, at["wave"], "Route 15", EncounterMethod.Grass,
            W(28, "Sandslash", 54, 64), W(207, "Gligar", 55, 65), W(247, "Pupitar", 55, 65), W(248, "Tyranitar", 57, 57), W(472, "Gliscor", 57, 57), W(531, "Audino", 54, 57), W(538, "Throh", 55, 65), W(539, "Sawk", 57, 57), W(560, "Scrafty", 55, 63), W(587, "Emolga", 55, 55));
        AddWild(result, at["bolt"], "Route 16", EncounterMethod.Grass,
            W(510, "Liepard", 22, 26), W(531, "Audino", 21, 24), W(568, "Trubbish", 21, 26), W(572, "Minccino", 21, 25), W(573, "Cinccino", 24, 24), W(577, "Solosis", 21, 26), W(587, "Emolga", 22, 22));
        AddWild(result, at["wave"], "Route 17", EncounterMethod.Surf,
            W(592, "Frillish", 45, 60), W(593, "Jellicent", 50, 60), W(594, "Alomomola", 45, 60));
        AddWild(result, at["wave"], "Route 18", EncounterMethod.Grass,
            W(206, "Dunsparce", 57, 57), W(357, "Tropius", 58, 66), W(455, "Carnivine", 58, 66), W(505, "Watchog", 56, 64), W(531, "Audino", 56, 59), W(538, "Throh", 57, 67), W(539, "Sawk", 59, 59), W(558, "Crustle", 57, 65), W(560, "Scrafty", 57, 66));
        AddWild(result, at["wave"], "Route 18", EncounterMethod.Surf,
            W(592, "Frillish", 45, 60), W(593, "Jellicent", 50, 60), W(594, "Alomomola", 45, 60));
        AddWild(result, at["start"], "Route 19", EncounterMethod.Grass,
            W(504, "Patrat", 2, 4), W(509, "Purrloin", 2, 4));
        AddWild(result, at["start"], "Route 19", EncounterMethod.Surf,
            W(550, "Basculin", 5, 15));
        AddWild(result, at["wave"], "Route 2", EncounterMethod.Grass,
            W(39, "Jigglypuff", 57, 57), W(40, "Wigglytuff", 59, 59), W(108, "Lickitung", 58, 58), W(206, "Dunsparce", 57, 57), W(463, "Lickilicky", 59, 59), W(505, "Watchog", 56, 59), W(507, "Herdier", 56, 59), W(508, "Stoutland", 59, 59), W(510, "Liepard", 57, 58), W(531, "Audino", 56, 59));
        AddWild(result, at["start"], "Route 20", EncounterMethod.Grass,
            W(191, "Sunkern", 2, 11), W(206, "Dunsparce", 3, 3), W(504, "Patrat", 3, 10), W(509, "Purrloin", 3, 11), W(519, "Pidove", 2, 10), W(531, "Audino", 2, 4), W(540, "Sewaddle", 2, 11), W(543, "Venipede", 10, 10));
        AddWild(result, at["start"], "Route 20", EncounterMethod.Surf,
            W(183, "Marill", 5, 15), W(184, "Azumarill", 5, 15), W(298, "Azurill", 5, 15), W(550, "Basculin", 5, 15));
        AddWild(result, at["wave"], "Route 21", EncounterMethod.Surf,
            W(223, "Remoraid", 35, 45), W(226, "Mantine", 35, 45), W(458, "Mantyke", 30, 45), W(592, "Frillish", 30, 45), W(593, "Jellicent", 30, 45), W(594, "Alomomola", 30, 45));
        AddWild(result, at["wave"], "Route 22", EncounterMethod.Grass,
            W(55, "Golduck", 40, 45), W(183, "Marill", 40, 47), W(184, "Azumarill", 42, 42), W(225, "Delibird", 39, 44), W(279, "Pelipper", 40, 45), W(337, "Lunatone", 41, 46), W(338, "Solrock", 41, 46), W(531, "Audino", 39, 42), W(587, "Emolga", 40, 40), W(591, "Amoonguss", 39, 44), W(619, "Mienfoo", 39, 47));
        AddWild(result, at["wave"], "Route 22", EncounterMethod.Surf,
            W(183, "Marill", 15, 40), W(184, "Azumarill", 25, 45), W(550, "Basculin", 15, 45));
        AddWild(result, at["wave"], "Route 23", EncounterMethod.Grass,
            W(55, "Golduck", 50, 55), W(207, "Gligar", 49, 54), W(472, "Gliscor", 51, 51), W(531, "Audino", 48, 51), W(538, "Throh", 48, 54), W(539, "Sawk", 51, 51), W(587, "Emolga", 49, 49), W(591, "Amoonguss", 49, 54), W(619, "Mienfoo", 48, 48), W(620, "Mienshao", 53, 53), W(626, "Bouffalant", 49, 56), W(627, "Rufflet", 47, 52));
        AddWild(result, at["wave"], "Route 23", EncounterMethod.Surf,
            W(418, "Buizel", 40, 55), W(419, "Floatzel", 45, 55), W(550, "Basculin", 40, 55));
        AddWild(result, at["wave"], "Route 3", EncounterMethod.Grass,
            W(193, "Yanma", 56, 64), W(469, "Yanmega", 58, 58), W(505, "Watchog", 55, 63), W(507, "Herdier", 57, 65), W(508, "Stoutland", 58, 58), W(509, "Purrloin", 57, 65), W(520, "Tranquill", 55, 65), W(521, "Unfezant", 58, 58), W(523, "Zebstrika", 56, 66), W(531, "Audino", 55, 58));
        AddWild(result, at["wave"], "Route 3", EncounterMethod.Surf,
            W(341, "Corphish", 45, 60), W(342, "Crawdaunt", 50, 60), W(550, "Basculin", 45, 60));
        AddWild(result, at["insect"], "Route 4", EncounterMethod.Grass,
            W(551, "Sandile", 14, 17), W(554, "Darumaka", 14, 17), W(559, "Scraggy", 17, 17), W(572, "Minccino", 14, 17));
        AddWild(result, at["insect"], "Route 4", EncounterMethod.Surf,
            W(592, "Frillish", 5, 15), W(593, "Jellicent", 5, 20), W(594, "Alomomola", 5, 20));
        AddWild(result, at["bolt"], "Route 5", EncounterMethod.Grass,
            W(510, "Liepard", 22, 26), W(531, "Audino", 21, 24), W(568, "Trubbish", 21, 26), W(572, "Minccino", 21, 25), W(573, "Cinccino", 24, 24), W(577, "Solosis", 21, 26), W(587, "Emolga", 22, 22));
        AddWild(result, at["quake"], "Route 6", EncounterMethod.Grass,
            W(183, "Marill", 25, 28), W(184, "Azumarill", 26, 26), W(206, "Dunsparce", 25, 25), W(351, "Castform", 26, 26), W(520, "Tranquill", 26, 29), W(521, "Unfezant", 26, 26), W(531, "Audino", 23, 25), W(541, "Swadloon", 26, 29), W(542, "Leavanny", 26, 26), W(585, "Deerling", 23, 28), W(587, "Emolga", 24, 25), W(588, "Karrablast", 23, 29), W(590, "Foongus", 26, 29), W(616, "Shelmet", 23, 26));
        AddWild(result, at["quake"], "Route 6", EncounterMethod.Surf,
            W(183, "Marill", 10, 35), W(184, "Azumarill", 10, 35), W(550, "Basculin", 10, 35));
        AddWild(result, at["jet"], "Route 7", EncounterMethod.Grass,
            W(335, "Zangoose", 32, 35), W(336, "Seviper", 32, 35), W(505, "Watchog", 31, 36), W(520, "Tranquill", 30, 35), W(521, "Unfezant", 33, 33), W(523, "Zebstrika", 31, 36), W(531, "Audino", 30, 33), W(585, "Deerling", 30, 33), W(587, "Emolga", 31, 31), W(590, "Foongus", 33, 36), W(613, "Cubchoo", 30, 36));
        AddWild(result, at["wave"], "Route 8", EncounterMethod.Grass,
            W(453, "Croagunk", 55, 56), W(536, "Palpitoad", 54, 57), W(588, "Karrablast", 54, 54), W(616, "Shelmet", 57, 57), W(618, "Stunfisk", 55, 56));
        AddWild(result, at["wave"], "Route 8", EncounterMethod.Surf,
            W(536, "Palpitoad", 45, 60), W(537, "Seismitoad", 50, 60), W(618, "Stunfisk", 45, 60));
        AddWild(result, at["wave"], "Route 9", EncounterMethod.Grass,
            W(89, "Muk", 40, 44), W(510, "Liepard", 40, 44), W(531, "Audino", 37, 40), W(569, "Garbodor", 38, 44), W(572, "Minccino", 37, 43), W(573, "Cinccino", 40, 40), W(578, "Duosion", 37, 43), W(579, "Reuniclus", 40, 40), W(587, "Emolga", 38, 38), W(624, "Pawniard", 38, 44));
        AddWild(result, at["wave"], "Seaside Cave", EncounterMethod.Cave,
            W(55, "Golduck", 34, 40), W(86, "Seel", 35, 35), W(213, "Shuckle", 41, 41), W(525, "Boldore", 35, 42), W(527, "Woobat", 34, 41), W(530, "Excadrill", 34, 42), W(602, "Tynamo", 37, 37), W(603, "Eelektrik", 42, 42));
        AddWild(result, at["wave"], "Seaside Cave", EncounterMethod.Surf,
            W(86, "Seel", 25, 40), W(87, "Dewgong", 30, 40), W(592, "Frillish", 25, 40), W(593, "Jellicent", 25, 40));
        AddWild(result, at["legend"], "Strange House", EncounterMethod.Cave,
            W(20, "Raticate", 32, 33), W(42, "Golbat", 32, 33), W(354, "Banette", 32, 34), W(577, "Solosis", 31, 31), W(578, "Duosion", 33, 34), W(607, "Litwick", 31, 33));
        AddWild(result, at["wave"], "Striaton City", EncounterMethod.Surf,
            W(341, "Corphish", 45, 60), W(342, "Crawdaunt", 50, 60), W(550, "Basculin", 45, 60));
        AddWild(result, at["wave"], "Twist Mountain", EncounterMethod.Cave,
            W(95, "Onix", 54, 57), W(208, "Steelix", 57, 57), W(525, "Boldore", 54, 55), W(527, "Woobat", 54, 54), W(530, "Excadrill", 54, 57), W(533, "Gurdurr", 55, 55), W(614, "Beartic", 54, 56), W(615, "Cryogonal", 56, 57), W(631, "Heatmor", 55, 56), W(632, "Durant", 56, 57));
        AddWild(result, at["legend"], "Undella Bay", EncounterMethod.Surf,
            W(223, "Remoraid", 30, 40), W(226, "Mantine", 30, 40), W(320, "Wailmer", 25, 40), W(321, "Wailord", 30, 40), W(363, "Spheal", 25, 40), W(364, "Sealeo", 25, 40), W(365, "Walrein", 30, 40), W(458, "Mantyke", 25, 40), W(592, "Frillish", 25, 40), W(593, "Jellicent", 25, 40));
        AddWild(result, at["legend"], "Undella Town", EncounterMethod.Surf,
            W(120, "Staryu", 25, 40), W(121, "Starmie", 30, 40), W(550, "Basculin", 30, 40), W(592, "Frillish", 25, 40), W(593, "Jellicent", 25, 40));
        AddWild(result, at["quake"], "Underground Ruins", EncounterMethod.Cave,
            W(95, "Onix", 54, 57), W(208, "Steelix", 57, 57), W(299, "Nosepass", 55, 57), W(305, "Lairon", 55, 57), W(525, "Boldore", 54, 56), W(527, "Woobat", 54, 54), W(530, "Excadrill", 54, 57), W(632, "Durant", 54, 54));
        AddWild(result, at["wave"], "Victory Road (B2/W2)", EncounterMethod.Cave,
            W(95, "Onix", 41, 50), W(206, "Dunsparce", 49, 49), W(315, "Roselia", 48, 55), W(334, "Altaria", 49, 55), W(354, "Banette", 47, 50), W(407, "Roserade", 50, 50), W(520, "Tranquill", 47, 55), W(521, "Unfezant", 50, 50), W(525, "Boldore", 41, 50), W(530, "Excadrill", 41, 50), W(531, "Audino", 47, 50), W(533, "Gurdurr", 47, 55), W(538, "Throh", 48, 55), W(539, "Sawk", 50, 50), W(548, "Petilil", 47, 55), W(549, "Lilligant", 50, 50), W(621, "Druddigon", 47, 50), W(623, "Golurk", 48, 50), W(634, "Zweilous", 49, 50));
        AddWild(result, at["wave"], "Victory Road (B2/W2)", EncounterMethod.Surf,
            W(183, "Marill", 35, 50), W(184, "Azumarill", 40, 50), W(418, "Buizel", 35, 50), W(419, "Floatzel", 40, 50), W(550, "Basculin", 35, 50));
        AddWild(result, at["legend"], "Village Bridge", EncounterMethod.Grass,
            W(55, "Golduck", 36, 42), W(183, "Marill", 36, 42), W(184, "Azumarill", 39, 39), W(206, "Dunsparce", 37, 37), W(335, "Zangoose", 37, 43), W(336, "Seviper", 37, 43), W(531, "Audino", 36, 39), W(587, "Emolga", 37, 37));
        AddWild(result, at["legend"], "Village Bridge", EncounterMethod.Surf,
            W(131, "Lapras", 30, 40), W(183, "Marill", 25, 40), W(184, "Azumarill", 30, 40), W(550, "Basculin", 25, 40));
        AddWild(result, at["basic"], "Virbank City", EncounterMethod.Surf,
            W(592, "Frillish", 5, 15), W(593, "Jellicent", 5, 15), W(594, "Alomomola", 5, 15));
        AddWild(result, at["basic"], "Virbank Complex", EncounterMethod.Grass,
            W(58, "Growlithe", 11, 14), W(81, "Magnemite", 10, 13), W(109, "Koffing", 10, 14), W(239, "Elekid", 10, 13), W(504, "Patrat", 10, 14), W(519, "Pidove", 10, 13), W(531, "Audino", 10, 13));
        AddWild(result, at["basic"], "Virbank Complex", EncounterMethod.Surf,
            W(592, "Frillish", 5, 15), W(593, "Jellicent", 5, 15), W(594, "Alomomola", 5, 15));
        AddWild(result, at["wave"], "Wellspring Cave", EncounterMethod.Cave,
            W(525, "Boldore", 55, 58), W(527, "Woobat", 55, 58), W(530, "Excadrill", 55, 58));
        AddWild(result, at["wave"], "Wellspring Cave", EncounterMethod.Surf,
            W(550, "Basculin", 45, 60));
    }

    private readonly record struct WildEncounter(
        int SpeciesId,
        string SpeciesName,
        int MinimumLevel,
        int MaximumLevel);
}
