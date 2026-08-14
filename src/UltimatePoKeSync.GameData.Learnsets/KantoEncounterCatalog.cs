using System.Collections.ObjectModel;
using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.GameData.Learnsets;

/// <summary>Which Kanto game a catalog answers for.</summary>
public enum KantoVersion
{
    FireRed = 0,
    LeafGreen = 1,
}

/// <summary>
/// The main-story acquisition timeline for Kanto: FireRed and LeafGreen. Built the way Hoenn
/// is (D-057, D-058), and sharing nothing with it: a different region, different routes and a
/// different order of Gyms.
/// </summary>
/// <remarks>
/// <para>
/// The encounters are PKHeX's and a test checks every one. The story order is curated with
/// nothing to check it against, so it is anchored to badge counts and errs late.
/// </para>
/// <para>Places PKHeX knows and this catalog will not suggest:</para>
/// <list type="bullet">
/// <item>Altering Cave (FRLG): an event-only cave that holds Zubat unless a distribution changed it.</item>
/// <item>Cerulean Cave: post-game, and it opens only after the Hall of Fame.</item>
/// <item>Tanoby Ruins and its chambers: post-game Unown, on the far Sevii Islands.</item>
/// <item>the Sevii Islands: Celio's errands open them, not badges, and the story flags that gate each island are not something a badge count can stand in for.</item>
/// </list>
/// <para>
/// The Sevii Islands are the largest of those, and the reason is worth stating plainly: they
/// open on Celio's errands rather than on badges, so the checkpoint model this catalog is
/// built on cannot express when they are reachable. Guessing would send somebody to an island
/// they cannot sail to. See D-059.
/// </para>
/// </remarks>
public sealed class KantoEncounterCatalog : IEncounterCatalog
{
    private static readonly Lazy<KantoEncounterCatalog> LazyFireRed =
        new(() => new(KantoVersion.FireRed));

    private static readonly Lazy<KantoEncounterCatalog> LazyLeafGreen =
        new(() => new(KantoVersion.LeafGreen));

    private readonly KantoVersion _version;
    private readonly IReadOnlyList<StoryMilestone> _milestones;
    private readonly IReadOnlyList<EncounterCandidate> _encounters;

    private KantoEncounterCatalog(KantoVersion version)
    {
        _version = version;

        StoryMilestone[] timeline =
        [
            Milestone("route-1", "Route 1", 10, 0,
                "After leaving Pallet Town for Viridian City."),
            Milestone("route-22", "Route 22", 20, 0,
                "West of Viridian City, before the Pokémon League gate."),
            Milestone("route-2", "Route 2", 30, 0,
                "North of Viridian City, at the edge of Viridian Forest."),
            Milestone("viridian-forest", "Viridian Forest", 40, 0,
                "Crossing Viridian Forest toward Pewter City."),
            Milestone("route-3", "Route 3", 50, 1,
                "East of Pewter City, after the Boulder Badge.", 1),
            Milestone("mt-moon", "Mt. Moon", 60, 1,
                "Inside Mt. Moon.", 1),
            Milestone("route-4", "Route 4", 70, 1,
                "Coming down from Mt. Moon toward Cerulean City.", 1),
            Milestone("cerulean", "Cerulean City", 80, 2,
                "The water around Cerulean City, after the Cascade Badge.", 2),
            Milestone("route-24", "Route 24", 90, 2,
                "North of Cerulean, across Nugget Bridge.", 2),
            Milestone("route-25", "Route 25", 100, 2,
                "East along the coast to Bill's cottage.", 2),
            Milestone("route-5", "Route 5", 110, 2,
                "South of Cerulean City.", 2),
            Milestone("route-6", "Route 6", 120, 2,
                "North of Vermilion City.", 2),
            Milestone("ss-anne", "S.S. Anne", 130, 2,
                "Aboard the S.S. Anne, before it sails.", 2),
            Milestone("vermilion", "Vermilion City", 140, 3,
                "The harbour at Vermilion, after the Thunder Badge.", 3),
            Milestone("route-11", "Route 11", 150, 3,
                "East of Vermilion City.", 3),
            Milestone("diglett", "Diglett's Cave", 160, 3,
                "Through Diglett's Cave, between Route 11 and Route 2.", 3),
            Milestone("route-9", "Route 9", 170, 3,
                "East of Cerulean, on the way to Rock Tunnel.", 3),
            Milestone("route-10", "Route 10", 180, 3,
                "Outside Rock Tunnel and the Power Plant.", 3),
            Milestone("rock-tunnel", "Rock Tunnel", 190, 3,
                "Inside Rock Tunnel, with Flash to see by.", 3),
            Milestone("route-8", "Route 8", 200, 3,
                "West of Lavender Town.", 3),
            Milestone("route-7", "Route 7", 210, 3,
                "East of Celadon City.", 3),
            Milestone("celadon", "Celadon City", 220, 4,
                "The water in Celadon City, after the Rainbow Badge.", 4),
            Milestone("pokemon-tower", "Pokémon Tower", 230, 4,
                "Climbing Pokémon Tower with the Silph Scope.", 4),
            Milestone("route-12", "Route 12", 240, 4,
                "South of Lavender Town.", 4),
            Milestone("route-13", "Route 13", 250, 4,
                "Further south along the coast.", 4),
            Milestone("route-14", "Route 14", 260, 4,
                "West toward Fuchsia City.", 4),
            Milestone("route-15", "Route 15", 270, 4,
                "The last stretch before Fuchsia City.", 4),
            Milestone("fuchsia", "Fuchsia City", 280, 5,
                "The water in Fuchsia City, after the Soul Badge.", 5),
            Milestone("safari", "Safari Zone", 290, 5,
                "Paying the entry fee at the Safari Zone in Fuchsia City.", 5),
            Milestone("route-16", "Route 16", 300, 5,
                "West of Celadon, at the top of Cycling Road.", 5),
            Milestone("route-17", "Route 17", 310, 5,
                "Down Cycling Road.", 5),
            Milestone("route-18", "Route 18", 320, 5,
                "The bottom of Cycling Road, east of Fuchsia.", 5),
            Milestone("route-19", "Route 19", 330, 5,
                "The sea south of Fuchsia City.", 5),
            Milestone("route-20", "Route 20", 340, 5,
                "The sea west toward Cinnabar Island.", 5),
            Milestone("power-plant", "Power Plant", 350, 6,
                "Inside the abandoned Power Plant, reached by Surf.", 6),
            Milestone("seafoam", "Seafoam Islands", 360, 6,
                "Inside the Seafoam Islands.", 6),
            Milestone("cinnabar", "Cinnabar Island", 370, 6,
                "The water around Cinnabar Island.", 6),
            Milestone("mansion", "Pokémon Mansion", 380, 6,
                "Searching the Pokémon Mansion for the Secret Key.", 6),
            Milestone("route-21", "Route 21", 390, 7,
                "The sea between Cinnabar and Pallet, after the Volcano Badge.", 7),
            Milestone("route-23", "Route 23", 400, 8,
                "North of Victory Road, past the Badge gates.", 8),
            Milestone("victory-road", "Victory Road", 410, 8,
                "Inside Victory Road, after all eight Badges.", 8),
        ];

        _milestones = Array.AsReadOnly(timeline);
        Dictionary<string, StoryMilestone> at = timeline.ToDictionary(milestone => milestone.Id);

        var result = new List<EncounterCandidate>();
        if (version == KantoVersion.FireRed)
        {
            AddFireRed(result, at);
        }
        else
        {
            AddLeafGreen(result, at);
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

    public static KantoEncounterCatalog FireRed => LazyFireRed.Value;

    public static KantoEncounterCatalog LeafGreen => LazyLeafGreen.Value;

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

    private string[] Codes() => _version == KantoVersion.FireRed
        ? ["BPRE", "BPRF", "BPRD", "BPRS", "BPRI", "BPRJ"]
        : ["BPGE", "BPGF", "BPGD", "BPGS", "BPGI", "BPGJ"];

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

    /// <summary>FireRed's wild table, emitted from PKHeX rather than typed by hand.</summary>
    private static void AddFireRed(
        List<EncounterCandidate> result,
        IReadOnlyDictionary<string, StoryMilestone> at)
    {
        AddWild(result, at["celadon"], "Celadon City", EncounterMethod.Surf,
            W(54, "Psyduck", 5, 40), W(109, "Koffing", 30, 40));
        AddWild(result, at["cerulean"], "Cerulean City", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["cinnabar"], "Cinnabar Island", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["diglett"], "Diglett's Cave", EncounterMethod.Cave,
            W(50, "Diglett", 15, 22), W(51, "Dugtrio", 29, 31));
        AddWild(result, at["fuchsia"], "Fuchsia City", EncounterMethod.Surf,
            W(54, "Psyduck", 20, 40));
        AddWild(result, at["mt-moon"], "Mt. Moon", EncounterMethod.Cave,
            W(35, "Clefairy", 8, 12), W(41, "Zubat", 7, 11), W(46, "Paras", 5, 12), W(74, "Geodude", 7, 10));
        AddWild(result, at["route-21"], "Pallet Town", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["mansion"], "Pokémon Mansion", EncounterMethod.Cave,
            W(19, "Rattata", 26, 28), W(20, "Raticate", 32, 38), W(58, "Growlithe", 30, 32), W(88, "Grimer", 28, 28), W(109, "Koffing", 28, 30), W(110, "Weezing", 32, 34), W(132, "Ditto", 30, 30));
        AddWild(result, at["pokemon-tower"], "Pokémon Tower", EncounterMethod.Cave,
            W(92, "Gastly", 13, 19), W(93, "Haunter", 20, 25), W(104, "Cubone", 15, 19));
        AddWild(result, at["power-plant"], "Power Plant", EncounterMethod.Cave,
            W(25, "Pikachu", 22, 26), W(81, "Magnemite", 22, 25), W(82, "Magneton", 31, 34), W(100, "Voltorb", 22, 25), W(125, "Electabuzz", 32, 35));
        AddWild(result, at["rock-tunnel"], "Rock Tunnel", EncounterMethod.Cave,
            W(41, "Zubat", 15, 16), W(56, "Mankey", 16, 17), W(66, "Machop", 16, 17), W(74, "Geodude", 15, 17), W(95, "Onix", 13, 17));
        AddWild(result, at["route-1"], "Route 1", EncounterMethod.Grass,
            W(16, "Pidgey", 2, 5), W(19, "Rattata", 2, 4));
        AddWild(result, at["route-10"], "Route 10", EncounterMethod.Grass,
            W(21, "Spearow", 13, 17), W(23, "Ekans", 11, 17), W(100, "Voltorb", 14, 17));
        AddWild(result, at["route-10"], "Route 10", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["route-11"], "Route 11", EncounterMethod.Grass,
            W(21, "Spearow", 13, 17), W(23, "Ekans", 12, 15), W(96, "Drowzee", 11, 15));
        AddWild(result, at["route-11"], "Route 11", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["route-12"], "Route 12", EncounterMethod.Grass,
            W(16, "Pidgey", 23, 27), W(43, "Oddish", 22, 26), W(44, "Gloom", 28, 30), W(48, "Venonat", 24, 26));
        AddWild(result, at["route-12"], "Route 12", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["route-13"], "Route 13", EncounterMethod.Grass,
            W(16, "Pidgey", 25, 27), W(17, "Pidgeotto", 29, 29), W(43, "Oddish", 22, 26), W(44, "Gloom", 28, 30), W(48, "Venonat", 24, 26), W(132, "Ditto", 25, 25));
        AddWild(result, at["route-13"], "Route 13", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["route-14"], "Route 14", EncounterMethod.Grass,
            W(16, "Pidgey", 27, 27), W(17, "Pidgeotto", 29, 29), W(43, "Oddish", 22, 26), W(44, "Gloom", 30, 30), W(48, "Venonat", 24, 26), W(132, "Ditto", 23, 23));
        AddWild(result, at["route-15"], "Route 15", EncounterMethod.Grass,
            W(16, "Pidgey", 25, 27), W(17, "Pidgeotto", 29, 29), W(43, "Oddish", 22, 26), W(44, "Gloom", 28, 30), W(48, "Venonat", 24, 26), W(132, "Ditto", 25, 25));
        AddWild(result, at["route-16"], "Route 16", EncounterMethod.Grass,
            W(19, "Rattata", 18, 22), W(20, "Raticate", 23, 25), W(21, "Spearow", 20, 22), W(84, "Doduo", 18, 22));
        AddWild(result, at["route-17"], "Route 17", EncounterMethod.Grass,
            W(19, "Rattata", 22, 22), W(20, "Raticate", 25, 29), W(21, "Spearow", 20, 22), W(22, "Fearow", 25, 27), W(84, "Doduo", 24, 28));
        AddWild(result, at["route-18"], "Route 18", EncounterMethod.Grass,
            W(19, "Rattata", 22, 22), W(20, "Raticate", 25, 29), W(21, "Spearow", 20, 22), W(22, "Fearow", 25, 29), W(84, "Doduo", 24, 28));
        AddWild(result, at["route-19"], "Route 19", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["route-2"], "Route 2", EncounterMethod.Grass,
            W(10, "Caterpie", 4, 5), W(13, "Weedle", 4, 5), W(16, "Pidgey", 2, 5), W(19, "Rattata", 2, 5));
        AddWild(result, at["route-20"], "Route 20", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["route-21"], "Route 21", EncounterMethod.Grass,
            W(114, "Tangela", 17, 28));
        AddWild(result, at["route-21"], "Route 21", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["route-22"], "Route 22", EncounterMethod.Grass,
            W(19, "Rattata", 2, 5), W(21, "Spearow", 3, 5), W(56, "Mankey", 2, 5));
        AddWild(result, at["route-22"], "Route 22", EncounterMethod.Surf,
            W(54, "Psyduck", 20, 40));
        AddWild(result, at["route-23"], "Route 23", EncounterMethod.Grass,
            W(21, "Spearow", 32, 34), W(22, "Fearow", 40, 44), W(23, "Ekans", 32, 34), W(24, "Arbok", 44, 44), W(56, "Mankey", 32, 34), W(57, "Primeape", 42, 42));
        AddWild(result, at["route-23"], "Route 23", EncounterMethod.Surf,
            W(54, "Psyduck", 20, 40));
        AddWild(result, at["route-24"], "Route 24", EncounterMethod.Grass,
            W(10, "Caterpie", 7, 7), W(11, "Metapod", 8, 8), W(13, "Weedle", 7, 7), W(14, "Kakuna", 8, 8), W(16, "Pidgey", 11, 13), W(43, "Oddish", 12, 14), W(63, "Abra", 8, 12));
        AddWild(result, at["route-24"], "Route 24", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["route-25"], "Route 25", EncounterMethod.Grass,
            W(10, "Caterpie", 8, 8), W(11, "Metapod", 9, 9), W(13, "Weedle", 8, 8), W(14, "Kakuna", 9, 9), W(16, "Pidgey", 11, 13), W(43, "Oddish", 12, 14), W(63, "Abra", 9, 13));
        AddWild(result, at["route-25"], "Route 25", EncounterMethod.Surf,
            W(54, "Psyduck", 20, 40));
        AddWild(result, at["route-3"], "Route 3", EncounterMethod.Grass,
            W(16, "Pidgey", 6, 7), W(21, "Spearow", 6, 8), W(29, "Nidoran♀", 6, 6), W(32, "Nidoran♂", 6, 7), W(39, "Jigglypuff", 3, 7), W(56, "Mankey", 7, 7));
        AddWild(result, at["route-4"], "Route 4", EncounterMethod.Grass,
            W(19, "Rattata", 8, 12), W(21, "Spearow", 8, 12), W(23, "Ekans", 6, 12), W(56, "Mankey", 10, 12));
        AddWild(result, at["route-4"], "Route 4", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["route-5"], "Route 5", EncounterMethod.Grass,
            W(16, "Pidgey", 13, 16), W(43, "Oddish", 13, 16), W(52, "Meowth", 10, 16));
        AddWild(result, at["route-6"], "Route 6", EncounterMethod.Grass,
            W(16, "Pidgey", 13, 16), W(43, "Oddish", 13, 16), W(52, "Meowth", 10, 16));
        AddWild(result, at["route-6"], "Route 6", EncounterMethod.Surf,
            W(54, "Psyduck", 20, 40));
        AddWild(result, at["route-7"], "Route 7", EncounterMethod.Grass,
            W(16, "Pidgey", 19, 22), W(43, "Oddish", 19, 22), W(52, "Meowth", 17, 20), W(58, "Growlithe", 18, 20));
        AddWild(result, at["route-8"], "Route 8", EncounterMethod.Grass,
            W(16, "Pidgey", 18, 20), W(23, "Ekans", 17, 19), W(52, "Meowth", 18, 20), W(58, "Growlithe", 15, 18));
        AddWild(result, at["route-9"], "Route 9", EncounterMethod.Grass,
            W(19, "Rattata", 14, 17), W(21, "Spearow", 13, 17), W(23, "Ekans", 11, 17));
        AddWild(result, at["ss-anne"], "S.S. Anne", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["safari"], "Safari Zone (Kanto)", EncounterMethod.Cave,
            W(29, "Nidoran♀", 24, 30), W(30, "Nidorina", 30, 31), W(32, "Nidoran♂", 22, 30), W(33, "Nidorino", 30, 33), W(46, "Paras", 22, 23), W(47, "Parasect", 25, 30), W(48, "Venonat", 22, 23), W(49, "Venomoth", 32, 32), W(84, "Doduo", 26, 26), W(102, "Exeggcute", 23, 27), W(111, "Rhyhorn", 25, 26), W(113, "Chansey", 23, 26), W(115, "Kangaskhan", 25, 28), W(123, "Scyther", 23, 28), W(128, "Tauros", 25, 28));
        AddWild(result, at["safari"], "Safari Zone (Kanto)", EncounterMethod.Surf,
            W(54, "Psyduck", 20, 40));
        AddWild(result, at["seafoam"], "Seafoam Islands", EncounterMethod.Cave,
            W(41, "Zubat", 22, 26), W(42, "Golbat", 26, 30), W(54, "Psyduck", 26, 33), W(55, "Golduck", 32, 35), W(86, "Seel", 28, 34), W(87, "Dewgong", 32, 36));
        AddWild(result, at["seafoam"], "Seafoam Islands", EncounterMethod.Surf,
            W(54, "Psyduck", 30, 40), W(55, "Golduck", 35, 40), W(86, "Seel", 25, 35), W(87, "Dewgong", 35, 40), W(116, "Horsea", 25, 30));
        AddWild(result, at["vermilion"], "Vermilion City", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["victory-road"], "Victory Road (Kanto)", EncounterMethod.Cave,
            W(24, "Arbok", 44, 46), W(41, "Zubat", 32, 34), W(42, "Golbat", 44, 46), W(57, "Primeape", 42, 42), W(66, "Machop", 32, 34), W(67, "Machoke", 44, 48), W(74, "Geodude", 32, 34), W(95, "Onix", 40, 48), W(105, "Marowak", 44, 48));
        AddWild(result, at["route-2"], "Viridian City", EncounterMethod.Surf,
            W(54, "Psyduck", 20, 40));
        AddWild(result, at["viridian-forest"], "Viridian Forest", EncounterMethod.Cave,
            W(10, "Caterpie", 3, 5), W(11, "Metapod", 5, 5), W(13, "Weedle", 3, 5), W(14, "Kakuna", 4, 6), W(25, "Pikachu", 3, 5));
    }

    /// <summary>LeafGreen's wild table, emitted from PKHeX rather than typed by hand.</summary>
    private static void AddLeafGreen(
        List<EncounterCandidate> result,
        IReadOnlyDictionary<string, StoryMilestone> at)
    {
        AddWild(result, at["celadon"], "Celadon City", EncounterMethod.Surf,
            W(79, "Slowpoke", 5, 40), W(109, "Koffing", 30, 40));
        AddWild(result, at["cerulean"], "Cerulean City", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["cinnabar"], "Cinnabar Island", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["diglett"], "Diglett's Cave", EncounterMethod.Cave,
            W(50, "Diglett", 15, 22), W(51, "Dugtrio", 29, 31));
        AddWild(result, at["fuchsia"], "Fuchsia City", EncounterMethod.Surf,
            W(79, "Slowpoke", 20, 40));
        AddWild(result, at["mt-moon"], "Mt. Moon", EncounterMethod.Cave,
            W(35, "Clefairy", 8, 12), W(41, "Zubat", 7, 11), W(46, "Paras", 5, 12), W(74, "Geodude", 7, 10));
        AddWild(result, at["route-21"], "Pallet Town", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["mansion"], "Pokémon Mansion", EncounterMethod.Cave,
            W(19, "Rattata", 26, 28), W(20, "Raticate", 32, 38), W(37, "Vulpix", 30, 32), W(88, "Grimer", 28, 30), W(89, "Muk", 32, 34), W(109, "Koffing", 28, 28), W(132, "Ditto", 30, 30));
        AddWild(result, at["pokemon-tower"], "Pokémon Tower", EncounterMethod.Cave,
            W(92, "Gastly", 13, 19), W(93, "Haunter", 20, 25), W(104, "Cubone", 15, 19));
        AddWild(result, at["power-plant"], "Power Plant", EncounterMethod.Cave,
            W(25, "Pikachu", 22, 26), W(81, "Magnemite", 22, 25), W(82, "Magneton", 31, 34), W(100, "Voltorb", 22, 25));
        AddWild(result, at["rock-tunnel"], "Rock Tunnel", EncounterMethod.Cave,
            W(41, "Zubat", 15, 16), W(56, "Mankey", 16, 17), W(66, "Machop", 16, 17), W(74, "Geodude", 15, 17), W(95, "Onix", 13, 17));
        AddWild(result, at["route-1"], "Route 1", EncounterMethod.Grass,
            W(16, "Pidgey", 2, 5), W(19, "Rattata", 2, 4));
        AddWild(result, at["route-10"], "Route 10", EncounterMethod.Grass,
            W(21, "Spearow", 13, 17), W(27, "Sandshrew", 11, 17), W(100, "Voltorb", 14, 17));
        AddWild(result, at["route-10"], "Route 10", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["route-11"], "Route 11", EncounterMethod.Grass,
            W(21, "Spearow", 13, 17), W(27, "Sandshrew", 12, 15), W(96, "Drowzee", 11, 15));
        AddWild(result, at["route-11"], "Route 11", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["route-12"], "Route 12", EncounterMethod.Grass,
            W(16, "Pidgey", 23, 27), W(48, "Venonat", 24, 26), W(69, "Bellsprout", 22, 26), W(70, "Weepinbell", 28, 30));
        AddWild(result, at["route-12"], "Route 12", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["route-13"], "Route 13", EncounterMethod.Grass,
            W(16, "Pidgey", 25, 27), W(17, "Pidgeotto", 29, 29), W(48, "Venonat", 24, 26), W(69, "Bellsprout", 22, 26), W(70, "Weepinbell", 28, 30), W(132, "Ditto", 25, 25));
        AddWild(result, at["route-13"], "Route 13", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["route-14"], "Route 14", EncounterMethod.Grass,
            W(16, "Pidgey", 27, 27), W(17, "Pidgeotto", 29, 29), W(48, "Venonat", 24, 26), W(69, "Bellsprout", 22, 26), W(70, "Weepinbell", 30, 30), W(132, "Ditto", 23, 23));
        AddWild(result, at["route-15"], "Route 15", EncounterMethod.Grass,
            W(16, "Pidgey", 25, 27), W(17, "Pidgeotto", 29, 29), W(48, "Venonat", 24, 26), W(69, "Bellsprout", 22, 26), W(70, "Weepinbell", 28, 30), W(132, "Ditto", 25, 25));
        AddWild(result, at["route-16"], "Route 16", EncounterMethod.Grass,
            W(19, "Rattata", 18, 22), W(20, "Raticate", 23, 25), W(21, "Spearow", 20, 22), W(84, "Doduo", 18, 22));
        AddWild(result, at["route-17"], "Route 17", EncounterMethod.Grass,
            W(19, "Rattata", 22, 22), W(20, "Raticate", 25, 29), W(21, "Spearow", 20, 22), W(22, "Fearow", 25, 27), W(84, "Doduo", 24, 28));
        AddWild(result, at["route-18"], "Route 18", EncounterMethod.Grass,
            W(19, "Rattata", 22, 22), W(20, "Raticate", 25, 29), W(21, "Spearow", 20, 22), W(22, "Fearow", 25, 29), W(84, "Doduo", 24, 28));
        AddWild(result, at["route-19"], "Route 19", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["route-2"], "Route 2", EncounterMethod.Grass,
            W(10, "Caterpie", 4, 5), W(13, "Weedle", 4, 5), W(16, "Pidgey", 2, 5), W(19, "Rattata", 2, 5));
        AddWild(result, at["route-20"], "Route 20", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["route-21"], "Route 21", EncounterMethod.Grass,
            W(114, "Tangela", 17, 28));
        AddWild(result, at["route-21"], "Route 21", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["route-22"], "Route 22", EncounterMethod.Grass,
            W(19, "Rattata", 2, 5), W(21, "Spearow", 3, 5), W(56, "Mankey", 2, 5));
        AddWild(result, at["route-22"], "Route 22", EncounterMethod.Surf,
            W(79, "Slowpoke", 20, 40));
        AddWild(result, at["route-23"], "Route 23", EncounterMethod.Grass,
            W(21, "Spearow", 32, 34), W(22, "Fearow", 40, 44), W(27, "Sandshrew", 32, 34), W(28, "Sandslash", 44, 44), W(56, "Mankey", 32, 34), W(57, "Primeape", 42, 42));
        AddWild(result, at["route-23"], "Route 23", EncounterMethod.Surf,
            W(79, "Slowpoke", 20, 40));
        AddWild(result, at["route-24"], "Route 24", EncounterMethod.Grass,
            W(10, "Caterpie", 7, 7), W(11, "Metapod", 8, 8), W(13, "Weedle", 7, 7), W(14, "Kakuna", 8, 8), W(16, "Pidgey", 11, 13), W(63, "Abra", 8, 12), W(69, "Bellsprout", 12, 14));
        AddWild(result, at["route-24"], "Route 24", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["route-25"], "Route 25", EncounterMethod.Grass,
            W(10, "Caterpie", 8, 8), W(11, "Metapod", 9, 9), W(13, "Weedle", 8, 8), W(14, "Kakuna", 9, 9), W(16, "Pidgey", 11, 13), W(63, "Abra", 9, 13), W(69, "Bellsprout", 12, 14));
        AddWild(result, at["route-25"], "Route 25", EncounterMethod.Surf,
            W(79, "Slowpoke", 20, 40));
        AddWild(result, at["route-3"], "Route 3", EncounterMethod.Grass,
            W(16, "Pidgey", 6, 7), W(21, "Spearow", 6, 8), W(29, "Nidoran♀", 6, 7), W(32, "Nidoran♂", 6, 6), W(39, "Jigglypuff", 3, 7), W(56, "Mankey", 7, 7));
        AddWild(result, at["route-4"], "Route 4", EncounterMethod.Grass,
            W(19, "Rattata", 8, 12), W(21, "Spearow", 8, 12), W(27, "Sandshrew", 6, 12), W(56, "Mankey", 10, 12));
        AddWild(result, at["route-4"], "Route 4", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["route-5"], "Route 5", EncounterMethod.Grass,
            W(16, "Pidgey", 13, 16), W(52, "Meowth", 10, 16), W(69, "Bellsprout", 13, 16));
        AddWild(result, at["route-6"], "Route 6", EncounterMethod.Grass,
            W(16, "Pidgey", 13, 16), W(52, "Meowth", 10, 16), W(69, "Bellsprout", 13, 16));
        AddWild(result, at["route-6"], "Route 6", EncounterMethod.Surf,
            W(79, "Slowpoke", 20, 40));
        AddWild(result, at["route-7"], "Route 7", EncounterMethod.Grass,
            W(16, "Pidgey", 19, 22), W(37, "Vulpix", 18, 20), W(52, "Meowth", 17, 20), W(69, "Bellsprout", 19, 22));
        AddWild(result, at["route-8"], "Route 8", EncounterMethod.Grass,
            W(16, "Pidgey", 18, 20), W(27, "Sandshrew", 17, 19), W(37, "Vulpix", 15, 18), W(52, "Meowth", 18, 20));
        AddWild(result, at["route-9"], "Route 9", EncounterMethod.Grass,
            W(19, "Rattata", 14, 17), W(21, "Spearow", 13, 17), W(27, "Sandshrew", 11, 17));
        AddWild(result, at["ss-anne"], "S.S. Anne", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["safari"], "Safari Zone (Kanto)", EncounterMethod.Cave,
            W(29, "Nidoran♀", 22, 30), W(30, "Nidorina", 30, 33), W(32, "Nidoran♂", 24, 30), W(33, "Nidorino", 30, 31), W(46, "Paras", 22, 23), W(47, "Parasect", 25, 30), W(48, "Venonat", 22, 23), W(49, "Venomoth", 32, 32), W(84, "Doduo", 26, 26), W(102, "Exeggcute", 23, 27), W(111, "Rhyhorn", 25, 26), W(113, "Chansey", 23, 26), W(115, "Kangaskhan", 25, 28), W(127, "Pinsir", 23, 28), W(128, "Tauros", 25, 28));
        AddWild(result, at["safari"], "Safari Zone (Kanto)", EncounterMethod.Surf,
            W(79, "Slowpoke", 20, 40));
        AddWild(result, at["seafoam"], "Seafoam Islands", EncounterMethod.Cave,
            W(41, "Zubat", 22, 26), W(42, "Golbat", 26, 30), W(79, "Slowpoke", 26, 33), W(80, "Slowbro", 32, 35), W(86, "Seel", 28, 34), W(87, "Dewgong", 32, 36));
        AddWild(result, at["seafoam"], "Seafoam Islands", EncounterMethod.Surf,
            W(79, "Slowpoke", 30, 40), W(80, "Slowbro", 35, 40), W(86, "Seel", 25, 35), W(87, "Dewgong", 35, 40), W(98, "Krabby", 25, 30));
        AddWild(result, at["vermilion"], "Vermilion City", EncounterMethod.Surf,
            W(72, "Tentacool", 5, 40));
        AddWild(result, at["victory-road"], "Victory Road (Kanto)", EncounterMethod.Cave,
            W(28, "Sandslash", 44, 46), W(41, "Zubat", 32, 34), W(42, "Golbat", 44, 46), W(57, "Primeape", 42, 42), W(66, "Machop", 32, 34), W(67, "Machoke", 44, 48), W(74, "Geodude", 32, 34), W(95, "Onix", 40, 48), W(105, "Marowak", 44, 48));
        AddWild(result, at["route-2"], "Viridian City", EncounterMethod.Surf,
            W(79, "Slowpoke", 20, 40));
        AddWild(result, at["viridian-forest"], "Viridian Forest", EncounterMethod.Cave,
            W(10, "Caterpie", 3, 5), W(11, "Metapod", 4, 6), W(13, "Weedle", 3, 5), W(14, "Kakuna", 5, 5), W(25, "Pikachu", 3, 5));
    }

    private readonly record struct WildEncounter(
        int SpeciesId,
        string SpeciesName,
        int MinimumLevel,
        int MaximumLevel);
}
