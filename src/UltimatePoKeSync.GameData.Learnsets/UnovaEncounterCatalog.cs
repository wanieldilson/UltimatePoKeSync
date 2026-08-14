using System.Collections.ObjectModel;
using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.GameData.Learnsets;

/// <summary>
/// The main-story acquisition timeline for the original Pokémon Black and White. Wild species
/// and levels are a pinned snapshot of PKHeX's encounter tables for that version; story order
/// and prerequisites are kept explicitly here, because a legality table says where a Pokémon
/// exists, not when the player can reach it.
/// </summary>
/// <remarks>
/// <para>
/// One earliest actionable encounter is exposed per species. Gifts and event-only catches are
/// deliberately absent. The catalog ends at Victory Road: post-game routes must never leak
/// into advice for a player still collecting badges.
/// </para>
/// <para>
/// The two versions share the region, so they share this timeline and almost all of these
/// encounters; what differs is a handful of exclusives, listed once at the points where they
/// diverge rather than copied into a second catalog that would drift. See D-055.
/// </para>
/// </remarks>
public sealed class UnovaEncounterCatalog : IEncounterCatalog
{
    private const string FossilChoice = "relic-castle-fossil";

    private static readonly Lazy<UnovaEncounterCatalog> LazyBlack =
        new(() => new UnovaEncounterCatalog(isWhite: false));

    private static readonly Lazy<UnovaEncounterCatalog> LazyWhite =
        new(() => new UnovaEncounterCatalog(isWhite: true));

    private readonly bool _isWhite;
    private readonly IReadOnlyList<StoryMilestone> _milestones;
    private readonly IReadOnlyList<EncounterCandidate> _encounters;

    private UnovaEncounterCatalog(bool isWhite)
    {
        _isWhite = isWhite;
        StoryMilestone route1 = Milestone("route-1", "Route 1", 10, 0,
            "After receiving Poké Balls on Route 1.");
        StoryMilestone route2 = Milestone("route-2", "Route 2", 20, 0,
            "After reaching Route 2.");
        StoryMilestone dreamyard = Milestone("dreamyard", "Dreamyard", 30, 1,
            "After earning the Trio Badge and using Cut at the Dreamyard.", 1);
        StoryMilestone route3 = Milestone("route-3", "Route 3", 40, 1,
            "After leaving Striaton City for Route 3.");
        StoryMilestone wellspring = Milestone("wellspring-cave", "Wellspring Cave", 50, 1,
            "During the Team Plasma pursuit from Route 3.");
        StoryMilestone pinwheelOuter = Milestone("pinwheel-outer", "Pinwheel Forest entrance", 60, 1,
            "After reaching the outside of Pinwheel Forest.");
        StoryMilestone nacrene = Milestone("nacrene", "Nacrene City", 70, 2,
            "After earning the Basic Badge.", 2);
        StoryMilestone pinwheelInner = Milestone("pinwheel-inner", "Pinwheel Forest interior", 80, 2,
            "After pursuing Team Plasma into Pinwheel Forest.");
        StoryMilestone route4 = Milestone("route-4", "Route 4", 90, 2,
            "After reaching Castelia City; the southern Route 4 encounters are available before challenging Burgh.", 3);
        StoryMilestone desert = Milestone("desert-resort", "Desert Resort", 100, 3,
            "After Route 4 opens access to Desert Resort and Relic Castle.");
        StoryMilestone route5 = Milestone("route-5-16", "Routes 5 and 16", 110, 3,
            "After reaching Nimbasa City; both routes can be visited before challenging Elesa.", 4);
        StoryMilestone driftveil = Milestone("driftveil", "Driftveil City", 120, 4,
            "After crossing Driftveil Drawbridge.");
        StoryMilestone route6 = Milestone("route-6", "Route 6", 130, 5,
            "After earning the Quake Badge and leaving Driftveil City.", 5);
        StoryMilestone chargestone = Milestone("chargestone-cave", "Chargestone Cave", 140, 5,
            "After entering Chargestone Cave from Route 6.");
        StoryMilestone route7 = Milestone("route-7", "Route 7", 150, 5,
            "After leaving Chargestone Cave for Route 7, before challenging Skyla.", 6);
        StoryMilestone celestial = Milestone("celestial-tower", "Celestial Tower", 160, 5,
            "During the mandatory Celestial Tower visit before challenging Skyla.", 6);
        StoryMilestone surf = Milestone("surf-detours", "Surf detours", 170, 6,
            "After earning the Jet Badge, defeating Cheren at the Twist Mountain entrance, and receiving HM03 Surf from Alder.");
        StoryMilestone legendaryDetours = Milestone("legendary-detours", "Legendary detours", 175, 6,
            "After Surf and Strength open the Guidance Chamber and the other Sword of Justice encounters.");
        StoryMilestone twist = Milestone("twist-mountain", "Twist Mountain", 180, 6,
            "After entering Twist Mountain.");
        StoryMilestone route8 = Milestone("route-8", "Icirrus wetlands and Route 8", 190, 6,
            "After leaving Twist Mountain and reaching the Icirrus wetlands, before challenging Brycen.", 7);
        StoryMilestone dragonspiral = Milestone("dragonspiral-tower", "Dragonspiral Tower", 200, 7,
            "After the story sends the player to Dragonspiral Tower.");
        StoryMilestone route9 = Milestone("route-9", "Route 9", 210, 7,
            "After returning through Opelucid City toward Route 9.");
        StoryMilestone route10 = Milestone("route-10", "Route 10", 220, 8,
            "After earning the Legend Badge.", 8);
        StoryMilestone victoryRoad = Milestone("victory-road", "Victory Road", 230, 8,
            "After passing all eight Badge Check Gates.");

        _milestones = Array.AsReadOnly<StoryMilestone>(
        [
            route1, route2, dreamyard, route3, wellspring, pinwheelOuter, nacrene,
            pinwheelInner, route4, desert, route5, driftveil, route6, chargestone,
            route7, celestial, surf, legendaryDetours, twist, route8, dragonspiral, route9, route10,
            victoryRoad,
        ]);

        // Each listed species is independently obtainable at the milestone named. Levels
        // were verified against PKHeX.Core 26.7.7 and are pinned here: querying PKHeX's
        // shared encounter generator during app startup can race its internal buffers when
        // another analysis is already running.
        var result = new List<EncounterCandidate>();
        AddWild(result, route1, "Route 1", EncounterMethod.Grass,
            W(504, "Patrat", 2, 4), W(506, "Lillipup", 2, 4));
        AddWild(result, dreamyard, "Route 1 shaking grass", EncounterMethod.ShakingGrass,
            W(531, "Audino", 2, 4));
        AddWild(result, route2, "Route 2", EncounterMethod.Grass,
            W(509, "Purrloin", 4, 5));
        AddWild(result, dreamyard, "Dreamyard", EncounterMethod.Grass,
            W(517, "Munna", 8, 10));
        AddWild(result, route3, "Route 3", EncounterMethod.Grass,
            W(519, "Pidove", 8, 11), W(522, "Blitzle", 8, 11));
        AddWild(result, wellspring, "Wellspring Cave", EncounterMethod.Cave,
            W(524, "Roggenrola", 10, 13), W(527, "Woobat", 10, 13));
        AddWild(result, wellspring, "Wellspring Cave dust cloud", EncounterMethod.DustCloud,
            W(529, "Drilbur", 10, 13));
        // Throh and Sawk both exist in both versions, which is why this is not in the list of
        // exclusives above and why the PKHeX check is what found it. Their roles are what
        // swap: one is the common grass encounter from Lv.12 and the other only turns up in
        // shaking grass at Lv.15.
        AddWild(result, pinwheelOuter, "Pinwheel Forest entrance", EncounterMethod.Grass,
            W(532, "Timburr", 13, 14), W(535, "Tympole", 12, 15),
            _isWhite ? W(538, "Throh", 12, 15) : W(539, "Sawk", 12, 15));
        AddWild(result, pinwheelOuter, "Pinwheel Forest entrance shaking grass",
            EncounterMethod.ShakingGrass,
            _isWhite ? W(539, "Sawk", 15, 15) : W(538, "Throh", 15, 15));

        // The grass friend: Cottonee in Black, Petilil in White. Each is absent from the other
        // version's tables entirely, which is what makes this a swap rather than a preference.
        AddWild(result, pinwheelInner, "Pinwheel Forest interior", EncounterMethod.Grass,
            _isWhite ? W(548, "Petilil", 14, 17) : W(546, "Cottonee", 14, 17),
            W(540, "Sewaddle", 14, 17),
            W(543, "Venipede", 15, 16));
        AddWild(result, pinwheelInner, "Pinwheel Forest shaking grass", EncounterMethod.ShakingGrass,
            W(511, "Pansage", 15, 15), W(513, "Pansear", 15, 15),
            W(515, "Panpour", 15, 15));
        // The trade is the mirror of the catch: you hand over the one your version has.
        result.Add(_isWhite
            ? new EncounterCandidate(
                546, "Cottonee", pinwheelInner, "Nacrene City", EncounterMethod.InGameTrade,
                15, 15, "Catch a Petilil in Pinwheel Forest, then trade it in Nacrene City.")
            : new EncounterCandidate(
                548, "Petilil", pinwheelInner, "Nacrene City", EncounterMethod.InGameTrade,
                15, 15, "Catch a Cottonee in Pinwheel Forest, then trade it in Nacrene City."));

        AddWild(result, route4, "Route 4", EncounterMethod.Grass,
            W(551, "Sandile", 15, 18), W(554, "Darumaka", 15, 18),
            W(559, "Scraggy", 16, 17));
        AddWild(result, desert, "Desert Resort", EncounterMethod.Grass,
            W(556, "Maractus", 20, 20), W(557, "Dwebble", 20, 22),
            W(561, "Sigilyph", 20, 20));
        AddWild(result, desert, "Relic Castle", EncounterMethod.Cave,
            W(562, "Yamask", 19, 22));

        // The player chooses one fossil from the Backpacker at Relic Castle. Reviving it
        // in Nacrene is acquisition, not a gift; the exclusive id prevents plans from
        // recommending both sides of the irreversible choice.
        result.Add(new EncounterCandidate(
            564, "Tirtouga", desert, "Nacrene Museum", EncounterMethod.Fossil,
            25, 25, "Choose the Cover Fossil at Relic Castle, then revive it in Nacrene City.",
            ExclusiveGroup: FossilChoice));
        result.Add(new EncounterCandidate(
            566, "Archen", desert, "Nacrene Museum", EncounterMethod.Fossil,
            25, 25, "Choose the Plume Fossil at Relic Castle, then revive it in Nacrene City.",
            ExclusiveGroup: FossilChoice));

        AddWild(result, route5, "Route 5", EncounterMethod.Grass,
            W(568, "Trubbish", 19, 21), W(572, "Minccino", 19, 22),
            _isWhite ? W(577, "Solosis", 19, 22) : W(574, "Gothita", 19, 22));
        AddWild(result, route5, "Route 5 shaking grass", EncounterMethod.ShakingGrass,
            W(587, "Emolga", 20, 20));
        AddWild(result, driftveil, "Driftveil Drawbridge shadows", EncounterMethod.BridgeShadow,
            W(580, "Ducklett", 22, 25));
        AddWild(result, driftveil, "Cold Storage", EncounterMethod.Grass,
            W(582, "Vanillite", 20, 23));
        result.Add(new EncounterCandidate(
            550, "Basculin", driftveil, "Driftveil City", EncounterMethod.InGameTrade,
            25, 25, "Trade a Minccino."));

        AddWild(result, route6, "Route 6", EncounterMethod.Grass,
            W(585, "Deerling", 22, 24), W(588, "Karrablast", 22, 24),
            W(590, "Foongus", 23, 25));
        AddWild(result, chargestone, "Chargestone Cave", EncounterMethod.Cave,
            W(595, "Joltik", 24, 27), W(599, "Klink", 25, 27),
            W(597, "Ferroseed", 24, 25), W(602, "Tynamo", 27, 27));
        AddWild(result, celestial, "Celestial Tower", EncounterMethod.Cave,
            W(607, "Litwick", 26, 29), W(605, "Elgyem", 26, 29));

        // Surf becomes available after the sixth Gym, before entering Twist Mountain. Its
        // earliest useful main-story detours are deliberately represented once.
        AddWild(result, surf, "Mistralton Cave", EncounterMethod.Cave,
            W(610, "Axew", 30, 31));
        AddWild(result, surf, "Route 17 water", EncounterMethod.Surf,
            W(592, "Frillish", 5, 15));
        AddWild(result, surf, "Route 17 rippling water", EncounterMethod.RipplingWater,
            W(594, "Alomomola", 5, 20));
        result.Add(new EncounterCandidate(
            638, "Cobalion", legendaryDetours, "Guidance Chamber", EncounterMethod.Static,
            42, 42, "Use Surf and Strength to reach Guidance Chamber."));
        result.Add(new EncounterCandidate(
            640, "Virizion", legendaryDetours, "Rumination Field", EncounterMethod.Static,
            42, 42, "Encounter Cobalion, then enter Rumination Field from Pinwheel Forest."));

        AddWild(result, twist, "Twist Mountain", EncounterMethod.Cave,
            W(613, "Cubchoo", 28, 31), W(615, "Cryogonal", 28, 28));
        AddWild(result, route8, "Route 8 water", EncounterMethod.Surf,
            W(618, "Stunfisk", 15, 35, 100));
        AddWildWithRequirement(result, route8, "Route 8 shallow-water grass",
            EncounterMethod.Grass, "Unavailable in winter; catch it during spring, summer, or autumn.",
            availabilityIsConditional: true,
            W(616, "Shelmet", 30, 33));
        AddWild(result, dragonspiral, "Dragonspiral Tower", EncounterMethod.Cave,
            W(619, "Mienfoo", 30, 33), W(621, "Druddigon", 30, 33),
            W(622, "Golett", 30, 33));
        AddWild(result, route9, "Route 9", EncounterMethod.Grass,
            W(624, "Pawniard", 31, 39));
        AddWild(result, route10, "Route 10", EncounterMethod.Grass,
            _isWhite ? W(627, "Rufflet", 34, 36) : W(629, "Vullaby", 34, 36),
            W(626, "Bouffalant", 34, 35));
        result.Add(new EncounterCandidate(
            _isWhite ? 642 : 641, _isWhite ? "Thundurus" : "Tornadus",
            route10, "Unova roaming encounter", EncounterMethod.Roaming,
            40, 40, "Trigger the Route 7 storm event after earning the Legend Badge, then track the roaming Pokémon."));
        AddWild(result, victoryRoad, "Victory Road exterior", EncounterMethod.Grass,
            W(631, "Heatmor", 37, 40));
        AddWild(result, victoryRoad, "Victory Road cave", EncounterMethod.Cave,
            W(632, "Durant", 37, 40), W(633, "Deino", 38, 40));
        result.Add(new EncounterCandidate(
            639, "Terrakion", victoryRoad, "Trial Chamber", EncounterMethod.Static,
            42, 42, "Encounter Cobalion, then use Strength to reach Trial Chamber in Victory Road."));

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

    public static UnovaEncounterCatalog Black => LazyBlack.Value;

    public static UnovaEncounterCatalog White => LazyWhite.Value;

    public string SourceName =>
        $"PKHeX.Core 26.7.7 encounter snapshot + curated Pokémon {(_isWhite ? "White" : "Black")} story timeline";

    public bool Supports(GameIdentity game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return game.Generation == PokemonGeneration.Gen5 &&
            (_isWhite ? IsOriginalWhite(game.GameCode) : IsOriginalBlack(game.GameCode));
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

    /// <summary>
    /// The furthest checkpoint whose completion is guaranteed by the live badge mask.
    /// With no badges, Route 1 is the only conservative useful default; manual selection
    /// can refine that to Route 2 before the first Gym.
    /// </summary>
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
        ICollection<EncounterCandidate> target,
        StoryMilestone milestone,
        string location,
        EncounterMethod method,
        params WildEncounter[] encounters) =>
        AddWildWithRequirement(target, milestone, location, method, "", false, encounters);

    private static void AddWildWithRequirement(
        ICollection<EncounterCandidate> target,
        StoryMilestone milestone,
        string location,
        EncounterMethod method,
        string requirement,
        bool availabilityIsConditional = false,
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
                encounter.MaximumLevel,
                requirement,
                encounter.EncounterRatePercent,
                AvailabilityIsConditional: availabilityIsConditional));
        }
    }

    private static WildEncounter W(
        int speciesId,
        string speciesName,
        int minimumLevel,
        int maximumLevel,
        int? encounterRatePercent = null) =>
        new(speciesId, speciesName, minimumLevel, maximumLevel, encounterRatePercent);

    private static bool IsOriginalBlack(string gameCode) => gameCode is
        "IRBO" or "IRBE" or "IRBJ" or "IRBI" or "IRBS" or "IRBF" or "IRBD" or "IRBK";

    private static bool IsOriginalWhite(string gameCode) => gameCode is
        "IRAO" or "IRAE" or "IRAJ" or "IRAI" or "IRAS" or "IRAF" or "IRAD" or "IRAK";

    private void EnsureSupported(GameIdentity game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (!Supports(game))
        {
            throw new NotSupportedException(
                $"No Pokémon {(_isWhite ? "White" : "Black")} encounter timeline is available for {game}.");
        }
    }

    private readonly record struct WildEncounter(
        int SpeciesId,
        string SpeciesName,
        int MinimumLevel,
        int MaximumLevel,
        int? EncounterRatePercent);
}
