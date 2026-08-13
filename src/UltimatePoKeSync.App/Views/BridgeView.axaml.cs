using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using UltimatePoKeSync.App.ViewModels;

namespace UltimatePoKeSync.App.Views;

public partial class BridgeView : UserControl
{
    public BridgeView() => AvaloniaXamlLoader.Load(this);

    private async void CopyScriptPath(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel
            || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        await clipboard.SetTextAsync(viewModel.ScriptPath);
        if (sender is Button button)
        {
            button.Content = "Copied";
        }
    }
}
