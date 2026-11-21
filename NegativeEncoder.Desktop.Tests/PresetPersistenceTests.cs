using System.Text.Json.Nodes;
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
    public void RecoveryKeepsPresetSettingsAndExcludesSessionInputs()
    {
        var model = Open();
        var savedPath = Path.Combine(_directory, "presets.json");
        var original = File.ReadAllText(savedPath);
        Assert.DoesNotContain(nameof(Preset.AvailableModes), original);
        model.AddFiles(["/视频/素材.mp4"]);
        model.MuxAudio = "/视频/混音.m4a";
        model.Subtitle = "/字幕/对白.ass";
        model.ScriptDocument.Text = "# 手写脚本\n";
        Assert.False(model.IsPresetDirty);
        Assert.True(model.AddTask(TaskOperation.VapourSynth));
        var queued = Assert.Single(model.Tasks);
        Assert.Equal(model.Script, queued.Script);
        Assert.Equal(model.Subtitle, queued.SubtitlePath);
        Assert.Equal(model.MuxAudio, queued.AudioInputPath);

        model.CurrentPreset.VbrParam = "8012";
        model.CurrentPreset.IsOutputHdr = true;
        model.ProcessAll = true;
        model.OutputPath = "/视频/out.mkv";
        model.AudioOutput = "/视频/声音.m4a";
        model.MuxOutput = "/视频/封装.mkv";
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
        Assert.Empty(reopened.MuxAudio);
        Assert.Equal(model.MuxOutput, reopened.MuxOutput);
        Assert.Empty(reopened.Subtitle);
        Assert.True(reopened.UseXyVsf && reopened.UseRepeat && reopened.VsResize);
        Assert.Equal("", reopened.VsWidth);
        Assert.Empty(reopened.Script);
        Assert.True(model.CreatePreset("副本", true, PresetChangeChoice.Save));
        Assert.False(model.IsPresetDirty);
        Assert.Equal("# 手写脚本\n", model.Script);
        Assert.Equal("/视频/混音.m4a", model.MuxAudio);
        Assert.Equal("/字幕/对白.ass", model.Subtitle);
        model.RenamePreset("归档");
        var recovered = Open();
        Assert.Contains("*归档", recovered.Title);
        recovered.SavePreset();
        Assert.False(Open().IsPresetDirty);
        foreach (var file in new[] { savedPath, Path.Combine(_directory, "preset-draft.json") })
        {
            var json = File.ReadAllText(file);
            foreach (var field in new[] { "Subtitle", "MuxAudio", "Script" })
                Assert.DoesNotContain($"\"{field}\"", json);
        }
    }

    [Fact]
    public void LegacyRecoveryKeepsOnlyTheSelectedDraftAndRemovesSessionInputs()
    {
        var model = Open();
        model.CreatePreset("第二个", false);
        var catalogPath = Path.Combine(_directory, "presets.json");
        var draftPath = Path.Combine(_directory, "preset-draft.json");
        var catalog = JsonNode.Parse(File.ReadAllText(catalogPath))!.AsArray();
        var recovery = JsonNode.Parse(File.ReadAllText(draftPath))!;
        foreach (var preset in catalog)
        {
            var settings = preset!["settings"]!;
            settings["Subtitle"] = "legacy.ass";
            settings["MuxAudio"] = "legacy.aac";
            settings["Script"] = "# legacy";
            var draft = settings.DeepClone();
            draft["AudioBitrate"] = "320";
            recovery["changes"]![preset["id"]!.GetValue<string>()] = draft;
        }
        File.WriteAllText(catalogPath, catalog.ToJsonString());
        File.WriteAllText(draftPath, recovery.ToJsonString());

        var reopened = Open();
        Assert.Contains("*第二个", reopened.Title);
        Assert.Equal("320", reopened.CurrentPreset.AudioBitrate);
        Assert.Equal("192", reopened.Presets[0].AudioBitrate);
        Assert.Equal("Default", reopened.PresetLabel(reopened.Presets[0]));
        Assert.Empty(reopened.Subtitle);
        Assert.Empty(reopened.MuxAudio);
        Assert.DoesNotContain("legacy", reopened.Script);
        Assert.DoesNotContain("legacy", File.ReadAllText(catalogPath));
        Assert.DoesNotContain("legacy", File.ReadAllText(draftPath));
        Assert.Single(JsonNode.Parse(File.ReadAllText(draftPath))!["changes"]!.AsObject());
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
    public void ObsoletePipeColorsAreIgnoredInSavedPresetsAndDrafts()
    {
        var model = Open();
        model.CurrentPreset.FrameRate = "120000/1001";
        model.SavePreset();
        var catalogPath = Path.Combine(_directory, "presets.json");
        var catalog = JsonNode.Parse(File.ReadAllText(catalogPath))!;
        var settings = catalog[0]!["settings"]!.AsObject();
        string[] obsolete = ["PipeMatrix", "PipeTransfer", "PipePrimaries", "PipeRange"];
        foreach (var name in obsolete)
        {
            Assert.False(settings.ContainsKey(name));
            settings[name] = "old value";
        }
        File.WriteAllText(catalogPath, catalog.ToJsonString());
        var draftPath = Path.Combine(_directory, "preset-draft.json");
        var draft = JsonNode.Parse(File.ReadAllText(draftPath))!;
        var edited = settings.DeepClone();
        edited["PipeMatrix"] = 123;
        draft["changes"]![draft["selected"]!.GetValue<string>()] = edited;
        File.WriteAllText(draftPath, draft.ToJsonString());

        var restored = Open();
        Assert.False(restored.IsPresetDirty);
        Assert.Equal("120000/1001", restored.CurrentPreset.FrameRate);
        restored.CurrentPreset.FrameRate = "24";
        restored.CurrentPreset.FrameRate = "120000/1001";
        Assert.False(restored.IsPresetDirty);
        restored.SavePreset();
        var saved = JsonNode.Parse(File.ReadAllText(catalogPath))![0]!["settings"]!.AsObject();
        Assert.All(obsolete, name => Assert.False(saved.ContainsKey(name)));
    }

    [Theory]
    [InlineData(PresetChangeChoice.Save)]
    [InlineData(PresetChangeChoice.Discard)]
    public void FailedSwitchKeepsCurrentPresetAndAllowsRetry(PresetChangeChoice choice)
    {
        var model = Open();
        Assert.True(model.CreatePreset("第二个", false));
        var edited = model.CurrentPreset;
        edited.AudioBitrate = "256";
        var path = Path.Combine(_directory, choice == PresetChangeChoice.Save ? "presets.json" : "preset-draft.json");
        File.Move(path, path + ".bak");
        Directory.CreateDirectory(path);
        var target = model.Presets[0];
        var error = Record.Exception(() => model.SelectPreset(target, choice));
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.Same(edited, model.CurrentPreset);
        Assert.True(model.IsPresetDirty);
        Assert.Equal("256", model.CurrentPreset.AudioBitrate);

        Directory.Delete(path);
        File.Move(path + ".bak", path);
        model.SelectPreset(target, choice);
        var reopened = Open();
        Assert.Equal("Default", reopened.CurrentPreset.PresetName);
        reopened.SelectPreset(reopened.Presets[1]);
        Assert.Equal(choice == PresetChangeChoice.Save ? "256" : "192", reopened.CurrentPreset.AudioBitrate);
        Assert.False(reopened.IsPresetDirty);
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
            Assert.Empty(editor.Document.Text);
            editor.Document.Text = "# 当次编辑";
            next.Model.SelectPreset(next.Model.Presets[0], PresetChangeChoice.Save);
            next.Model.SelectPreset(next.Model.Presets[1]);
            Assert.Equal("# 当次编辑", editor.Document.Text);
            Assert.Equal(Codec.AV1, next.Model.CurrentPreset.Codec);
        }
        finally { next.Close(); }
    }
}
