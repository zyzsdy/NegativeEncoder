using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;

namespace NegativeEncoder.Desktop.Views;

// All app-owned dialogs share modal ownership, Escape, scrolling and initial focus.
public sealed class PreviewDialog : Window
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
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _body = new StackPanel { Spacing = 12, Margin = new Thickness(20) };
        if (!string.IsNullOrEmpty(message)) _body.Children.Add(new TextBlock { Text = message });
        _actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(20, 0, 20, 20) };
        var grid = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        grid.Children.Add(new ScrollViewer { Content = _body, MaxHeight = 510 });
        Grid.SetRow(_actions, 1);
        grid.Children.Add(_actions);
        Content = grid;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
    }

    public static async Task NoticeAsync(Window owner, string title, string message)
    {
        var dialog = new PreviewDialog(title, message);
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
