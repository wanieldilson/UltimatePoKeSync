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

    public MainWindow()
    {
        // The script has to be on disk before the setup screen points at it.
        SetupGuide.EnsureScript();
        _viewModel = new MainWindowViewModel();

        AvaloniaXamlLoader.Load(this);
        DataContext = _viewModel;

        this.FindControl<Button>("CopyPathButton")!.Click += CopyScriptPath;
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

    protected override async void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        await _viewModel.DisposeAsync();
    }
}
