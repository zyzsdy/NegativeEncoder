using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Platform.Storage;
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
        Assert.False(model.AddTask(TaskOperation.Video));
        model.AddFiles([Path.Combine(Path.GetTempPath(), "中文 视频.mp4")]);
        model.CurrentPreset.VbrParam = "8000";
        Assert.True(model.AddTask(TaskOperation.Video));
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
        Assert.True(model.AddTask(TaskOperation.Video));
        Assert.Equal(2, model.Tasks.Select(t => t.OutputPath).Distinct().Count());
        model.ProcessAll = false;
        model.OutputPath = model.InputPath;
        Assert.False(model.AddTask(TaskOperation.Video));
        Assert.Equal(2, model.Tasks.Count);
    }

    [Fact]
    public void BatchRejectsConflictingOutputNames()
    {
        var model = new MainWindowViewModel { ProcessAll = true };
        model.AddFiles(["/videos/one/clip.mp4", "/videos/two/clip.mp4"]);
        Assert.False(model.AddTask(TaskOperation.Video));
        Assert.Empty(model.Tasks);
    }

    [Fact]
    public void SaveAsCopiesEditsAndValidatesNames()
    {
        var model = new MainWindowViewModel();
        Assert.False(model.IsPresetDirty);
        model.CurrentPreset.VbrParam = "1234";
        Assert.True(model.IsPresetDirty);
        Assert.False(model.CreatePreset(" ", false, PresetChangeChoice.Discard));
        Assert.False(model.CreatePreset("Default", false, PresetChangeChoice.Discard));
        Assert.True(model.CreatePreset("测试", true, PresetChangeChoice.Discard));
        Assert.Equal("1234", model.CurrentPreset.VbrParam);
        model.SelectPreset(model.Presets[0]);
        Assert.Equal("6500", model.CurrentPreset.VbrParam);
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
    public async Task FileDropsRespectTheTargetAndKeepPathBindings()
    {
        var window = new MainWindow(false);
        var directory = Directory.CreateTempSubdirectory();
        window.Show();
        try
        {
            var first = Path.Combine(directory.FullName, "字幕 A.ass");
            var second = Path.Combine(directory.FullName, "track B.m4a");
            using var files = new DataTransfer();
            foreach (var path in new[] { first, second, first })
            {
                File.WriteAllText(path, "");
                var file = await window.StorageProvider.TryGetFileFromPathAsync(path);
                files.Add(DataTransferItem.CreateFile(Assert.IsAssignableFrom<IStorageFile>(file)));
            }

            void Drop(Control target)
            {
                Dispatcher.UIThread.RunJobs();
                var center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
                window.DragDrop(center, RawDragEventType.DragEnter, files, DragDropEffects.Copy, RawInputModifiers.None);
                window.DragDrop(center, RawDragEventType.DragOver, files, DragDropEffects.Copy, RawInputModifiers.None);
                window.DragDrop(center, RawDragEventType.Drop, files, DragDropEffects.Copy, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
            }

            var model = window.Model;
            var existing = Path.Combine(directory.FullName, "existing.mp4");
            model.AddFiles([existing]);
            var view = window.FindControl<FunctionTabs>("EditorTabs")!;
            var tabs = view.FindControl<TabControl>("Tabs")!;
            foreach (var (tab, name) in new[] { (2, "SubtitleField"), (5, "BurnSubtitleField"), (4, "MuxAudioField") })
            {
                tabs.SelectedIndex = tab;
                var field = view.FindControl<TextBox>(name)!;
                model.Subtitle = model.MuxAudio = "old";
                Drop(field);
                Assert.Equal(first, field.Text);
                Assert.Equal(first, tab == 4 ? model.MuxAudio : model.Subtitle);
                Assert.Equal("old", tab == 4 ? model.Subtitle : model.MuxAudio);
            }
            Assert.Single(model.Files);
            Drop(window.FindControl<TextBox>("InputPath")!);
            Assert.Equal(new[] { first, second, existing }, model.Files.Select(f => f.Path));
            Assert.Equal(first, model.InputPath);
            var fileList = window.FindControl<ListBox>("FileList")!;
            Assert.Same(model.Files[0], fileList.SelectedItem);

            model.RemoveFiles(model.Files.Take(2).ToArray());
            Drop(fileList);
            Assert.Equal(new[] { existing, first, second }, model.Files.Select(f => f.Path));
            Assert.Equal(existing, model.InputPath);
        }
        finally
        {
            window.Close();
            directory.Delete(true);
        }
    }

    [AvaloniaFact]
    public void AllSixTabsLoadAndBindingsRetainEditsAtMinimumSize()
    {
        var window = new MainWindow(false) { Width = 1000, Height = 620 };
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
