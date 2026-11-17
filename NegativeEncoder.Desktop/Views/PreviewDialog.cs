using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace NegativeEncoder.Desktop.Views;

// All app-owned dialogs share modal ownership, Escape, scrolling and initial focus.
public sealed class PreviewDialog : OwnedDialog
{
    private readonly StackPanel _body;
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
        grid.Children.Add(new ScrollViewer { Content = _body, MaxHeight = 510 });
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
        var dialog = new PreviewDialog($"任务详情 — {task.Name}", "") { Width = 850, CanResize = true };
        var details = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 300, DataContext = task };
        details.Bind(TextBox.TextProperty, new Avalonia.Data.Binding(nameof(task.Details)));
        Avalonia.Automation.AutomationProperties.SetName(details, "任务状态和执行日志");
        dialog._body.Children.Add(details);
        await ShowNoticeAsync(owner, dialog);
    }

    public static async Task AboutAsync(Window owner, string message)
    {
        using var stream = AssetLoader.Open(new Uri("avares://NegativeEncoder.Desktop/Assets/app-icon.png"));
        using var bitmap = new Bitmap(stream);
        var dialog = new PreviewDialog("关于消极压制", "") { Width = 640 };
        var icon = new Image
        {
            Source = bitmap,
            Width = 160,
            Height = 160,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 20, 0)
        };
        Avalonia.Automation.AutomationProperties.SetName(icon, "消极压制应用图标");
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
        var close = dialog.AddButton("关闭", () => dialog.Close());
        dialog.Opened += (_, _) => close.Focus();
        await dialog.ShowDialog(owner);
    }

    public static async Task<bool> ConfirmAsync(Window owner, string title, string message)
    {
        var dialog = new PreviewDialog(title, message);
        var cancel = dialog.AddButton("取消", () => dialog.Close(false));
        dialog.AddButton("删除预设", () => dialog.Close(true));
        dialog.Opened += (_, _) => cancel.Focus();
        return await dialog.ShowDialog<bool>(owner);
    }

    public static async Task EditNameAsync(Window owner, string title, string initial, Func<string, bool> apply, Func<string> error)
    {
        var dialog = new PreviewDialog(title, "");
        var input = new TextBox { Text = initial, Watermark = "预设名称" };
        Avalonia.Automation.AutomationProperties.SetName(input, "预设名称");
        var validation = new TextBlock { MinHeight = 22 };
        dialog._body.Children.Add(input);
        dialog._body.Children.Add(validation);
        dialog.AddButton("取消", () => dialog.Close());
        dialog.AddButton("保存", () =>
        {
            if (apply(input.Text ?? "")) dialog.Close();
            else { validation.Text = error(); input.Focus(); }
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
