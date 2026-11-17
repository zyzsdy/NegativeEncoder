using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NegativeEncoder.Desktop.ViewModels;
using NegativeEncoder.Desktop.ToolLibrary;
using NegativeEncoder.Desktop.Views;
using Xunit;

namespace NegativeEncoder.Desktop.Tests;

public sealed class VapourSynthTests
{
    internal static VsCapabilities FullCapabilities => new("/libs/python", "/libs/site-packages", "/libs/user-plugins", "/libs/user-scripts",
        new[] { "runtime", "source", "ivtc", "qtgmc", "yadif", "bwdif", "subtext", "xyvsf", "vsf", "vsfm" }.ToDictionary(x => x, _ => "tested"), new());
    [Theory]
    [InlineData(nameof(MainWindowViewModel.UseRepeat), "repeat=1")]
    [InlineData(nameof(MainWindowViewModel.UseQtgmc), "video = QTGMC(video, Preset='fast', TFF=True)")]
    [InlineData(nameof(MainWindowViewModel.UseIvtc), "core.vivtc.VDecimate(video)")]
    [InlineData(nameof(MainWindowViewModel.UseYadifDouble), "field=3), order=1, field=-1, mode=1")]
    [InlineData(nameof(MainWindowViewModel.UseYadifNormal), "field=1), order=1, field=-1, mode=0")]
    [InlineData(nameof(MainWindowViewModel.VsResize), "core.resize.Lanczos(video, 1920, 1080)")]
    public void EveryOptionRegeneratesOnEnableAndDisable(string propertyName, string generatedCode)
    {
        var model = new MainWindowViewModel(FullCapabilities) { Subtitle = "/videos/subtitle.ass" };
        var original = model.Script;
        var property = typeof(MainWindowViewModel).GetProperty(propertyName)!;
        var notifications = new List<string?>();
        model.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        property.SetValue(model, true);
        Assert.Contains(propertyName, notifications);
        Assert.Contains(generatedCode, model.Script);
        property.SetValue(model, false);
        Assert.Equal(original, model.Script);
    }

    [Fact]
    public void PathsDimensionsAndInputSelectionAlwaysRebuildTheSameDocument()
    {
        var model = new MainWindowViewModel(FullCapabilities);
        var document = model.ScriptDocument;
        Assert.Contains("VIDEO_PATH = ''", model.Script);
        Assert.EndsWith("video.set_output()\n", model.Script);

        model.AddFiles(["/videos/中文 '片名'.mp4", "/videos/second.mp4"]);
        Assert.Contains("VIDEO_PATH = '/videos/中文 \\'片名\\'.mp4'", model.Script);
        model.Subtitle = "C:\\字幕\\it's\n字幕.ass";
        Assert.Contains("SUB_PATH = 'C:\\\\字幕\\\\it\\'s\\n字幕.ass'", model.Script);
        Assert.Contains("core.sub.TextFile(video, SUB_PATH)", model.Script);
        Assert.DoesNotContain("core.xyvsf", model.Script);

        model.VsResize = true;
        model.VsWidth = "1280";
        Assert.Contains("Lanczos(video, 1280, 1080)", model.Script);
        model.VsHeight = "720";
        Assert.Contains("Lanczos(video, 1280, 720)", model.Script);
        model.SelectedFile = model.Files[1];
        Assert.Contains("VIDEO_PATH = '/videos/second.mp4'", model.Script);
        model.Subtitle = "";
        Assert.DoesNotContain("SUB_PATH", model.Script);
        Assert.DoesNotContain("TextSub", model.Script);
        model.ClearFiles();
        Assert.Contains("VIDEO_PATH = ''", model.Script);
        Assert.Same(document, model.ScriptDocument);

        model.Script = "# 手动编辑\n";
        model.GenerateVsScript();
        Assert.DoesNotContain("# 手动编辑", model.Script);
        model.VsResize = false;
        model.Script = "# 手动编辑\n";
        model.VsWidth = "640";
        Assert.DoesNotContain("# 手动编辑", model.Script);
        Assert.DoesNotContain("Lanczos", model.Script);
    }

    [AvaloniaFact]
    public void CompactToolbarEnablesDimensionsAndMenuChangesRegenerateTheEditor()
    {
        var window = new MainWindow(false) { Width = 1000, Height = 620 };
        window.Model.ApplyVsCapabilities(FullCapabilities);
        window.Show();
        try
        {
            var view = window.FindControl<FunctionTabs>("EditorTabs")!;
            var tabs = view.FindControl<TabControl>("Tabs")!;
            tabs.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            var resize = view.FindControl<CheckBox>("VsResizeCheckBox")!;
            var width = view.FindControl<TextBox>("VsWidthField")!;
            var height = view.FindControl<TextBox>("VsHeightField")!;
            var editor = view.FindControl<AvaloniaEdit.TextEditor>("ScriptEditor")!;
            Assert.False(width.IsEffectivelyEnabled);
            Assert.False(height.IsEffectivelyEnabled);
            resize.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Assert.True(width.IsEffectivelyEnabled);
            Assert.True(height.IsEffectivelyEnabled);
            width.Text = "1280";
            height.Text = "720";
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("Lanczos(video, 1280, 720)", editor.Document.Text);

            foreach (var name in new[] { "UseRepeat", "UseQtgmc", "UseIvtc", "UseYadifDouble", "UseYadifNormal" })
            {
                var item = view.FindControl<MenuItem>(name + "Item")!;
                editor.Document.Text = "# 手动编辑\n";
                item.IsChecked = true;
                Dispatcher.UIThread.RunJobs();
                Assert.True((bool)typeof(MainWindowViewModel).GetProperty(name)!.GetValue(window.Model)!);
                Assert.DoesNotContain("# 手动编辑", editor.Document.Text);
                item.IsChecked = false;
            }
            editor.Document.Text = "# 保留手动编辑\n";
            tabs.SelectedIndex = 0;
            tabs.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("# 保留手动编辑\n", editor.Document.Text);
            view.FindControl<MenuItem>("GenerateVsScriptItem")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Contains("Lanczos(video, 1280, 720)", editor.Document.Text);

            foreach (var windowWidth in new[] { 1000, 1320, 1600, 1000 })
            {
                window.Width = windowWidth;
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                var toolbar = view.FindControl<Grid>("VsGeneratorToolbar")!;
                var menu = view.FindControl<Menu>("VsGeneratorMenu")!;
                var controls = new Control[] { menu, resize, width, height };
                var bounds = controls.Select(control => new Rect(control.TranslatePoint(default, toolbar)!.Value, control.Bounds.Size)).ToArray();
                Assert.All(bounds, rect => Assert.True(rect.Right <= toolbar.Bounds.Width + 1));
                for (var i = 1; i < bounds.Length; i++)
                {
                    Assert.True(bounds[i].X >= bounds[i - 1].Right);
                    Assert.InRange(Math.Abs(bounds[i].Center.Y - bounds[0].Center.Y), 0, 1);
                }
                Assert.True(editor.Bounds.Height > 150);
            }
            resize.IsChecked = false;
            Dispatcher.UIThread.RunJobs();
            Assert.False(width.IsEffectivelyEnabled);
            Assert.False(height.IsEffectivelyEnabled);
            Assert.DoesNotContain("Lanczos", editor.Document.Text);
            resize.IsChecked = true;
            Assert.Contains("Lanczos(video, 1280, 720)", editor.Document.Text);
        }
        finally { window.Close(); }
    }
}
