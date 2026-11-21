using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using NegativeEncoder.Desktop.Models;

namespace NegativeEncoder.Desktop.Views;

public enum EmptyLibraryChoice { Dismiss, OpenLibrary, NeverAskAgain }

// All app-owned dialogs share modal ownership, Escape, scrolling and initial focus.
public sealed class PreviewDialog : OwnedDialog
{
    private readonly StackPanel _body;
    private readonly ScrollViewer _bodyScroll;
    private readonly StackPanel _actions;

    public PreviewDialog(string title, string message)
    {
        Title = title;
        Width = 520;
        MaxHeight = 650;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        _body = new StackPanel { Spacing = 12, Margin = new Thickness(20) };
        if (!string.IsNullOrEmpty(message)) _body.Children.Add(new TextBlock { Text = message });
        _actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(20, 0, 20, 20) };
        var grid = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        _bodyScroll = new ScrollViewer { Content = _body, MaxHeight = 510 };
        grid.Children.Add(_bodyScroll);
        Grid.SetRow(_actions, 1);
        grid.Children.Add(_actions);
        Content = grid;
    }

    public static async Task NoticeAsync(Window owner, string title, string message)
    {
        var dialog = new PreviewDialog(title, message);
        await ShowNoticeAsync(owner, dialog);
    }

    public static async Task TaskAsync(Window owner, Models.TaskDraft task)
    {
        var dialog = new PreviewDialog(Loc.Format("PreviewDialog.TaskTitle", task.Name), "") { Width = 850, CanResize = true };
        var details = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 300, DataContext = task };
        details.Bind(TextBox.TextProperty, new Avalonia.Data.Binding(nameof(task.Details)));
        Avalonia.Automation.AutomationProperties.SetName(details, Loc.Text("PreviewDialog.TaskLog"));
        dialog._body.Children.Add(details);
        var scroll = dialog._bodyScroll;
        scroll.ScrollChanged += (_, e) =>
        {
            if (e.Source != scroll || (e.ExtentDelta.Y == 0 && e.ViewportDelta.Y == 0)) return;
            // Follow the tail only if the old end was already visible before layout changed.
            var previousExtent = scroll.Extent.Height - e.ExtentDelta.Y;
            var previousViewport = scroll.Viewport.Height - e.ViewportDelta.Y;
            if (scroll.Offset.Y + previousViewport >= previousExtent - 1)
                scroll.ScrollToEnd();
        };
        await ShowNoticeAsync(owner, dialog);
    }

    public static async Task AboutAsync(Window owner, string message)
    {
        using var stream = AssetLoader.Open(new Uri("avares://NegativeEncoder.Desktop/Assets/app-icon.png"));
        using var bitmap = new Bitmap(stream);
        var dialog = new PreviewDialog(Loc.Text("MainWindow.About"), "") { Width = 640 };
        var icon = new Image
        {
            Source = bitmap,
            Width = 160,
            Height = 160,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 20, 0)
        };
        Avalonia.Automation.AutomationProperties.SetName(icon, Loc.Text("PreviewDialog.AppIcon"));
        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        content.Children.Add(icon);
        var description = new TextBlock { Text = message };
        Grid.SetColumn(description, 1);
        content.Children.Add(description);
        dialog._body.Children.Add(content);
        await ShowNoticeAsync(owner, dialog);
    }

    private static async Task ShowNoticeAsync(Window owner, PreviewDialog dialog)
    {
        var close = dialog.AddButton(Loc.Text("SettingsWindow.Close"), () => dialog.Close());
        dialog.Opened += (_, _) => close.Focus();
        await dialog.ShowDialog(owner);
    }

    public static async Task<bool> ConfirmAsync(Window owner, string actionLabel, string message)
    {
        var dialog = new PreviewDialog(actionLabel, message);
        var cancel = dialog.AddButton(Loc.Text("PreviewDialog.Cancel"), () => dialog.Close(false));
        dialog.AddButton(actionLabel, () => dialog.Close(true));
        dialog.Opened += (_, _) => cancel.Focus();
        return await dialog.ShowDialog<bool>(owner);
    }

    public static Task<EmptyLibraryChoice> EmptyLibraryAsync(Window owner)
    {
        var dialog = new PreviewDialog(Loc.Text("MainWindow.ToolLibrary"), Loc.Text("PreviewDialog.EmptyLibrary"));
        dialog.AddButton(Loc.Text("PreviewDialog.NeverAskAgain"), () => dialog.Close(EmptyLibraryChoice.NeverAskAgain));
        var no = dialog.AddButton(Loc.Text("PreviewDialog.No"), () => dialog.Close(EmptyLibraryChoice.Dismiss));
        dialog.AddButton(Loc.Text("PreviewDialog.Yes"), () => dialog.Close(EmptyLibraryChoice.OpenLibrary));
        dialog.Opened += (_, _) => no.Focus();
        return dialog.ShowDialog<EmptyLibraryChoice>(owner);
    }

    public static Task<PresetChangeChoice> UnsavedPresetAsync(Window owner, string name)
    {
        var dialog = new PreviewDialog(Loc.Text("MainWindow.UnsavedPreset"), Loc.Format("MainWindow.SaveChangesToPreset", name));
        var cancel = dialog.AddButton(Loc.Text("PreviewDialog.Cancel"), () => dialog.Close(PresetChangeChoice.Cancel));
        dialog.AddButton(Loc.Text("PreviewDialog.DontSave"), () => dialog.Close(PresetChangeChoice.Discard));
        dialog.AddButton(Loc.Text("PreviewDialog.Save"), () => dialog.Close(PresetChangeChoice.Save));
        dialog.Opened += (_, _) => cancel.Focus();
        return dialog.ShowDialog<PresetChangeChoice>(owner);
    }

    public static async Task EditNameAsync(Window owner, string title, string initial, Func<string, bool> apply, Func<string> error)
    {
        var dialog = new PreviewDialog(title, "");
        var input = new TextBox { Text = initial, Watermark = Loc.Text("PreviewDialog.PresetName") };
        Avalonia.Automation.AutomationProperties.SetName(input, Loc.Text("PreviewDialog.PresetName"));
        var validation = new TextBlock { MinHeight = 22 };
        dialog._body.Children.Add(input);
        dialog._body.Children.Add(validation);
        dialog.AddButton(Loc.Text("PreviewDialog.Cancel"), () => dialog.Close());
        dialog.AddButton(Loc.Text("PreviewDialog.Save"), () =>
        {
            try
            {
                if (apply(input.Text ?? "")) { dialog.Close(); return; }
                validation.Text = error();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                validation.Text = Loc.Format("MainWindow.SaveFailed", ex.Message);
            }
            input.Focus();
        });
        // Do not bind Enter to submit: Enter must remain safe while composing Chinese text.
        dialog.Opened += (_, _) => Dispatcher.UIThread.Post(() => { input.Focus(); input.SelectAll(); });
        await dialog.ShowDialog(owner);
    }

    private Button AddButton(string label, Action action)
    {
        var button = new Button { Content = label };
        button.Click += (_, _) => action();
        _actions.Children.Add(button);
        return button;
    }
}
