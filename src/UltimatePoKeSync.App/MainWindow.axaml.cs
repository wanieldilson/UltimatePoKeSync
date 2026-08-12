using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using UltimatePoKeSync.App.Services;
using UltimatePoKeSync.App.ViewModels;

namespace UltimatePoKeSync.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    private double? _normalWidth;
    private double? _normalHeight;
    private PixelPoint? _normalPosition;

    public MainWindow()
    {
        // The script has to be on disk before the setup screen points at it.
        SetupGuide.EnsureScript();
        _viewModel = new MainWindowViewModel();

        AvaloniaXamlLoader.Load(this);
        DataContext = _viewModel;

        RestoreLayout(AppSettings.Load());
        PositionChanged += (_, _) => RememberNormalBounds();

        this.FindControl<Button>("CopyPathButton")!.Click += CopyScriptPath;
        this.FindControl<Button>("CopyHeaderPathButton")!.Click += CopyScriptPath;
        _viewModel.Start();
    }

    /// <summary>
    /// The path is the one thing a user has to move by hand into mGBA's file dialog, so
    /// it is a button rather than something to retype.
    /// </summary>
    private async void CopyScriptPath(object? sender, RoutedEventArgs e)
    {
        if (Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(_viewModel.ScriptPath);
            if (sender is Button button)
            {
                button.Content = "Copied";
            }
        }
    }

    /// <summary>
    /// Where the window was last time. A stored size that is too small to use is ignored —
    /// a window saved at 40×20 by a crash or a hand-edited file would reopen unusable — but
    /// a stored maximised state is honoured either way. See D-038.
    /// </summary>
    private void RestoreLayout(AppSettings settings)
    {
        if (settings.HasUsableSize)
        {
            _normalWidth = Width = settings.WindowWidth!.Value;
            _normalHeight = Height = settings.WindowHeight!.Value;

            if (settings.HasPosition)
            {
                _normalPosition = new PixelPoint(settings.WindowX!.Value, settings.WindowY!.Value);
                WindowStartupLocation = WindowStartupLocation.Manual;
                Position = _normalPosition.Value;
            }
        }

        if (settings.WindowMaximised)
        {
            WindowState = WindowState.Maximized;
        }
    }

    /// <summary>
    /// The size and position from before it was maximised, kept as the window moves. A
    /// maximised window reports the screen's bounds, and saving those makes it reopen
    /// filling the screen with no way back to the size the player chose.
    /// </summary>
    private void RememberNormalBounds()
    {
        if (WindowState != WindowState.Normal)
        {
            return;
        }

        _normalWidth = Width;
        _normalHeight = Height;
        _normalPosition = Position;
    }

    protected override void OnResized(WindowResizedEventArgs e)
    {
        base.OnResized(e);
        RememberNormalBounds();
    }

    private void SaveLayout() =>
        new AppSettings
        {
            WindowWidth = _normalWidth,
            WindowHeight = _normalHeight,
            WindowX = _normalPosition?.X,
            WindowY = _normalPosition?.Y,
            WindowMaximised = WindowState == WindowState.Maximized,
            CompetitiveProfile = _viewModel.IsCompetitive,
        }.Save();

    protected override async void OnClosed(EventArgs e)
    {
        SaveLayout();
        base.OnClosed(e);
        await _viewModel.DisposeAsync();
    }
}
