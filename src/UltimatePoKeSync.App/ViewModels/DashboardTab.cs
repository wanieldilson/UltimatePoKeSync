namespace UltimatePoKeSync.App.ViewModels;

/// <summary>
/// The six screens of the dashboard. Switching one changes the content area only: the
/// header and the party rail never move. See D-047.
/// </summary>
public enum DashboardTab
{
    Pokemon = 0,
    Stats = 1,
    Build = 2,
    Learnset = 3,
    Team = 4,
    Bridge = 5,
}
