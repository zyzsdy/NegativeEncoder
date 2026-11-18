using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NegativeEncoder.Desktop.Models;
using NegativeEncoder.Desktop.ToolLibrary;
using NegativeEncoder.Desktop.ViewModels;
using NegativeEncoder.Desktop.Views;
using Xunit;

namespace NegativeEncoder.Desktop.Tests;

public sealed class PresetPersistenceTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("ne-presets-").FullName;
    public void Dispose() => Directory.Delete(_directory, true);
    private MainWindowViewModel Open() => new(VapourSynthTests.FullCapabilities, _directory);

    [Fact]
    public void DraftsRestoreImmediatelyAndSavingOnlyCommitsTheSelectedPreset()
    {
        var model = Open();
        var savedPath = Path.Combine(_directory, "presets.json");
        var original = File.ReadAllText(savedPath);
        Assert.DoesNotContain(nameof(Preset.AvailableModes), original);
        model.CurrentPreset.VbrParam = "8012";
        model.CurrentPreset.IsOutputHdr = true;
        model.ProcessAll = true;
        model.OutputPath = "/视频/out.mkv";
        model.AudioOutput = "/视频/声音.m4a";
        model.MuxAudio = "/视频/混音.m4a";
        model.MuxOutput = "/视频/封装.mkv";
        model.Subtitle = "/字幕/对白.ass";
        model.SubtitleRenderer = "xyvsf";
        model.UseRepeat = true;
        model.VsResize = true;
        model.VsWidth = ""; // Keep an incomplete edit even when the generator cannot run.
        model.ScriptDocument.Text = "# 手写脚本\n";
        Assert.Equal(original, File.ReadAllText(savedPath));

        var reopened = Open();
        Assert.True(reopened.IsPresetDirty);
        Assert.Contains("*Default", reopened.Title);
        Assert.Equal("8012", reopened.CurrentPreset.VbrParam);
        Assert.True(reopened.CurrentPreset.IsOutputHdr);
        Assert.True(reopened.ProcessAll);
        Assert.Equal(model.OutputPath, reopened.OutputPath);
        Assert.Equal(model.AudioOutput, reopened.AudioOutput);
        Assert.Equal(model.MuxAudio, reopened.MuxAudio);
        Assert.Equal(model.MuxOutput, reopened.MuxOutput);
        Assert.Equal(model.Subtitle, reopened.Subtitle);
        Assert.True(reopened.UseXyVsf && reopened.UseRepeat && reopened.VsResize);
        Assert.Equal("", reopened.VsWidth);
        Assert.Equal("# 手写脚本\n", reopened.Script);

        Assert.True(reopened.CreatePreset("副本", true));
        Assert.False(reopened.IsPresetDirty);
        Assert.Equal("# 手写脚本\n", reopened.Script);
        reopened.Script = "# 副本编辑";
        reopened.SavePreset();

        var selected = Open();
        Assert.Equal("副本", selected.CurrentPreset.PresetName);
        Assert.False(selected.IsPresetDirty);
        Assert.Equal("# 副本编辑", selected.Script);
        selected.CurrentPreset = selected.Presets.Single(p => p.PresetName == "Default");
        Assert.Equal("# 手写脚本\n", selected.Script);
        Assert.True(selected.IsPresetDirty);
        selected.RenamePreset("归档");
        Assert.Contains("*归档", Open().Title);
        selected.SavePreset();
        Assert.False(Open().IsPresetDirty);
        selected.DeletePreset();
        Assert.Equal("副本", Assert.Single(Open().Presets).PresetName);
        selected.DeletePreset();
        Assert.Equal("Default", Assert.Single(Open().Presets).PresetName);
        Assert.False(Open().IsPresetDirty);
    }

    [Fact]
    public void RevertingToSavedValuesClearsRecoveredDirtyStateAndProbePreservesEncoder()
    {
        var model = Open();
        model.CurrentPreset.Encoder = Encoder.QSV;
        model.SavePreset();
        model.CurrentPreset.AudioBitrate = "256";
        var reopened = Open();
        reopened.CurrentPreset.AudioBitrate = "192";
        Assert.False(reopened.IsPresetDirty);
        reopened = Open();
        reopened.ApplyHardware(new HostPlatform("win", "x64", new Version(10, 0)), new HardwareReport("AMD", ["AMD"], null));
        Assert.Equal(Encoder.QSV, reopened.CurrentPreset.Encoder);
        Assert.False(reopened.IsPresetDirty);
        Assert.Equal(Encoder.QSV, Open().CurrentPreset.Encoder);
    }

    [Fact]
    public void InvalidDraftIsReportedAndPreserved()
    {
        var model = Open();
        model.CurrentPreset.AudioBitrate = "256";
        var path = Path.Combine(_directory, "preset-draft.json");
        File.WriteAllText(path, "{");
        Assert.Contains(path, Assert.Throws<InvalidDataException>(() => Open()).Message);
        Assert.Equal("{", File.ReadAllText(path));
    }

    [Fact]
    public void FailedWritesExposeTheErrorAndKeepTheEditAvailableToRetry()
    {
        var model = Open();
        var path = Path.Combine(_directory, "preset-draft.json");
        Directory.CreateDirectory(path);
        var error = Record.Exception(() => { model.AudioOutput = "/audio.m4a"; });
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.Contains("草稿写入失败", model.Status);
        Assert.True(model.IsPresetDirty);
        Directory.Delete(path);
        model.SavePreset();
        Assert.Equal("/audio.m4a", Open().AudioOutput);
        Assert.False(Open().IsPresetDirty);
    }

    [AvaloniaFact]
    public void ReopenedWindowShowsRecoveredControlsAndStarredMenus()
    {
        var first = new MainWindow(false, _directory);
        first.Show();
        try
        {
            first.Model.CreatePreset("原片", false);
            first.Model.CurrentPreset.Encoder = Encoder.QSV;
            first.Model.CurrentPreset.Codec = Codec.AV1;
            first.Model.SavePreset();
            var view = first.FindControl<FunctionTabs>("EditorTabs")!;
            view.FindControl<TabControl>("Tabs")!.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            view.FindControl<CheckBox>("VsResizeCheckBox")!.IsChecked = true;
            view.FindControl<TextBox>("VsWidthField")!.Text = "1280";
            view.FindControl<AvaloniaEdit.TextEditor>("ScriptEditor")!.Document.Text = "# 原片";
        }
        finally { first.Close(); }

        var next = new MainWindow(false, _directory);
        next.Show();
        try
        {
            var view = next.FindControl<FunctionTabs>("EditorTabs")!;
            view.FindControl<TabControl>("Tabs")!.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("*原片", next.Title);
            Assert.Equal("*原片", next.FindControl<MenuItem>("PresetMenu")!.Items.OfType<MenuItem>().Single(i => i.IsChecked).Header);
            Assert.Equal("1280", view.FindControl<TextBox>("VsWidthField")!.Text);
            Assert.True(view.FindControl<CheckBox>("VsResizeCheckBox")!.IsChecked);
            var editor = view.FindControl<AvaloniaEdit.TextEditor>("ScriptEditor")!;
            Assert.Equal("# 原片", editor.Document.Text);
            var original = next.Model.CurrentPreset;
            next.Model.CurrentPreset = next.Model.Presets[0];
            next.Model.CurrentPreset = original;
            Assert.Equal("# 原片", editor.Document.Text);
            Assert.Equal(Codec.AV1, original.Codec);
        }
        finally { next.Close(); }
    }
}
