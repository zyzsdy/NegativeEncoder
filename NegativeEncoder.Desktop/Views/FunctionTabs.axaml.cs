using Avalonia.Controls;
using Avalonia.Interactivity;
using NegativeEncoder.Desktop.Models;
using NegativeEncoder.Desktop.ViewModels;

namespace NegativeEncoder.Desktop.Views;

public partial class FunctionTabs : UserControl
{
    public FunctionTabs()
    {
        InitializeComponent();
        ScriptEditor.SyntaxHighlighting = AvaloniaEdit.Highlighting.HighlightingManager.Instance.GetDefinition("Python");
    }

    private async void AddTask_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: TaskOperation operation } && TopLevel.GetTopLevel(this) is MainWindow window)
            await window.RunQueueAsync(operation);
    }

    private void GenerateVsScript_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel model) model.GenerateVsScript();
    }

    private async void ToolLibrary_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow window) await window.ShowToolLibraryAsync();
    }

    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow window && sender is Button { Tag: string target })
            await window.BrowseAsync(target);
    }
}
