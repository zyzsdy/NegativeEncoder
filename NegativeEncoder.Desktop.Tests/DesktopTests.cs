using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NegativeEncoder.Desktop.Models;
using NegativeEncoder.Desktop.ViewModels;
using NegativeEncoder.Desktop.Views;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(NegativeEncoder.Desktop.Tests.TestApp))]

namespace NegativeEncoder.Desktop.Tests;

public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public sealed class DesktopTests
{
    [Fact]
    public void QueueRejectsMissingInputAndCapturesAnIndependentPreset()
    {
        var model = new MainWindowViewModel();
        Assert.False(model.AddTask("视频编码"));
        model.AddFiles([Path.Combine(Path.GetTempPath(), "中文 视频.mp4")]);
        model.CurrentPreset.VbrParam = "8000";
        Assert.True(model.AddTask("视频编码"));
        model.CurrentPreset.VbrParam = "1000";
        Assert.Equal("8000", model.Tasks[0].Preset.VbrParam);
        Assert.Equal("等待执行", model.Tasks[0].Status);
        Assert.EndsWith("中文 视频_encoded.mp4", model.Tasks[0].OutputPath);
    }

    [Fact]
    public void BatchUsesEveryFileAndDoesNotOverwriteInputs()
    {
        var model = new MainWindowViewModel { ProcessAll = true };
        model.AddFiles(["/videos/a.mp4", "/videos/b.mp4", "/videos/a.mp4"]);
        Assert.Equal(2, model.Files.Count);
        Assert.True(model.AddTask("视频编码"));
        Assert.Equal(2, model.Tasks.Select(t => t.OutputPath).Distinct().Count());
        model.ProcessAll = false;
        model.OutputPath = model.InputPath;
        Assert.False(model.AddTask("视频编码"));
        Assert.Equal(2, model.Tasks.Count);
    }

    [Fact]
    public void BatchRejectsConflictingOutputNames()
    {
        var model = new MainWindowViewModel { ProcessAll = true };
        model.AddFiles(["/videos/one/clip.mp4", "/videos/two/clip.mp4"]);
        Assert.False(model.AddTask("视频编码"));
        Assert.Empty(model.Tasks);
    }

    [Fact]
    public void PresetsRetainDraftEditsAndValidateNames()
    {
        var model = new MainWindowViewModel();
        Assert.False(model.IsPresetDirty);
        model.CurrentPreset.VbrParam = "1234";
        Assert.True(model.IsPresetDirty);
        var first = model.CurrentPreset;
        Assert.False(model.CreatePreset(" ", false));
        Assert.False(model.CreatePreset("Default", false));
        Assert.True(model.CreatePreset("测试", true));
        Assert.Equal("1234", model.CurrentPreset.VbrParam);
        model.CurrentPreset = first;
        Assert.True(model.IsPresetDirty);
        model.SavePreset();
        Assert.False(model.IsPresetDirty);
        model.DeletePreset();
        model.DeletePreset();
        Assert.Single(model.Presets);
    }

    [Fact]
    public void RemovingSelectedFileKeepsInputAndOutputConsistent()
    {
        var model = new MainWindowViewModel();
        model.AddFiles(["/videos/a.mp4", "/videos/b.mp4"]);
        model.RemoveFiles([model.SelectedFile!]);
        Assert.EndsWith("b.mp4", model.InputPath);
        model.CurrentPreset.OutputFormat = OutputFormat.MPEGTS;
        Assert.EndsWith(".ts", model.OutputPath);
        model.ClearFiles();
        Assert.True(model.FilesEmpty);
        Assert.Empty(model.InputPath);
        Assert.Empty(model.OutputPath);
    }

    [AvaloniaFact]
    public void AllSixTabsLoadAndBindingsRetainEditsAtMinimumSize()
    {
        var window = new MainWindow { Width = 1000, Height = 620 };
        window.Show();
        try
        {
            var view = window.FindControl<FunctionTabs>("EditorTabs")!;
            var tabs = view.FindControl<TabControl>("Tabs")!;
            Assert.Equal(6, tabs.Items.Count);
            for (var index = 0; index < tabs.Items.Count; index++)
            {
                tabs.SelectedIndex = index;
                Dispatcher.UIThread.RunJobs();
                Assert.NotNull(tabs.SelectedContent);
                Assert.True(tabs.Bounds.Height > 200);
            }
            tabs.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            var bitrate = view.FindControl<TextBox>("VbrQuailtyField")!;
            bitrate.Text = "29";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("29", window.Model.CurrentPreset.VbrQuailty);
            var encoder = view.FindControl<ComboBox>("EncoderSelect")!;
            encoder.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Encoder.QSV, window.Model.CurrentPreset.Encoder);
            Assert.Equal(OperatingSystem.IsWindows(), view.FindControl<ComboBox>("D3DModeSelect")!.IsEnabled);
            encoder.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            Assert.False(view.FindControl<ComboBox>("D3DModeSelect")!.IsEnabled);
            tabs.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            var script = view.FindControl<AvaloniaEdit.TextEditor>("ScriptEditor")!;
            Assert.NotNull(script.SyntaxHighlighting);
            script.Document.Text = "# 脚本编辑测试\n";
            Assert.Equal("# 脚本编辑测试\n", window.Model.Script);
            tabs.SelectedIndex = 3;
            tabs.SelectedIndex = 0;
            Assert.Equal("29", bitrate.Text);
        }
        finally { window.Close(); }
    }
}
