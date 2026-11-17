using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using NegativeEncoder.Desktop;
using NegativeEncoder.Desktop.Models;
using NegativeEncoder.Desktop.Views;

return AppBuilder.Configure<VerificationApp>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);

sealed class VerificationApp : App
{
    public override void OnFrameworkInitializationCompleted()
    {
        // The native UI verifier deliberately does not probe hardware or run encoders.
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) throw new InvalidOperationException("Desktop lifetime missing");
        var window = new MainWindow(false);
        desktop.MainWindow = window;
        window.Opened += (_, _) => Dispatcher.UIThread.Post(() => Verify(desktop, window));
    }

    private static void Verify(IClassicDesktopStyleApplicationLifetime desktop, MainWindow window)
    {
        try
        {
            var view = window.FindControl<FunctionTabs>("EditorTabs")!;
            var tabs = view.FindControl<TabControl>("Tabs")!;
            Require(tabs.Items.Count == 6, "six tabs");
            foreach (var width in new[] { 1000, 1320, 1600 })
            {
                window.Width = width;
                for (var i = 0; i < 6; i++)
                {
                    tabs.SelectedIndex = i;
                    window.UpdateLayout();
                    Require(tabs.SelectedContent is not null && tabs.Bounds.Height > 200, $"tab {i}, width {width}");
                }
            }
            tabs.SelectedIndex = 0;
            var encoder = view.FindControl<ComboBox>("EncoderSelect")!;
            encoder.SelectedIndex = 1;
            Require(window.Model.CurrentPreset.Encoder == Encoder.QSV, "QSV binding");
            Require(!view.FindControl<ComboBox>("D3DModeSelect")!.IsEnabled, "no Windows D3D on Linux");
            var bitrate = view.FindControl<TextBox>("VbrQuailtyField")!;
            bitrate.Text = "28";
            Require(window.Model.CurrentPreset.VbrQuailty == "28", "editable form");
            encoder.IsDropDownOpen = true;
            window.UpdateLayout();
            Require(encoder.IsDropDownOpen, "native dropdown opens");
            encoder.IsDropDownOpen = false;
            window.Model.CreatePreset("Linux UI", true);
            Require(window.Model.CurrentPreset.VbrQuailty == "28", "preset snapshot");
            tabs.SelectedIndex = 2;
            var editor = view.FindControl<AvaloniaEdit.TextEditor>("ScriptEditor")!;
            editor.Document.Text = "# Linux UI verification\n";
            Require(window.Model.Script == editor.Document.Text, "script binding");
            tabs.SelectedIndex = 0;
            Require(bitrate.Text == "28", "tab retains edit");
            Require(!window.Model.AddTask("视频编码"), "empty input validation");
            Console.WriteLine("PASS native Linux UI: six tabs, three sizes, encoder/form/preset/script bindings, popup, validation");
            desktop.Shutdown(0);
        }
        catch (Exception error) { Console.Error.WriteLine(error); desktop.Shutdown(1); }
    }
    private static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("UI verification failed: " + description);
    }
}
