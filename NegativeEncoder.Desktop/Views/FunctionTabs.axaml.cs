using Avalonia.Controls;
using Avalonia.Interactivity;
using NegativeEncoder.Desktop.ViewModels;

namespace NegativeEncoder.Desktop.Views;

public partial class FunctionTabs : UserControl
{
    public FunctionTabs()
    {
        InitializeComponent();
        ScriptEditor.SyntaxHighlighting = AvaloniaEdit.Highlighting.HighlightingManager.Instance.GetDefinition("Python");
    }

    private void AddTask_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel model && sender is Button { Tag: string operation })
            model.AddTask(operation);
    }

    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow window && sender is Button { Tag: string target })
            await window.BrowseAsync(target);
    }
}
