using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NegativeEncoder.Desktop.EncodingTasks;
using NegativeEncoder.Desktop.Models;
using NegativeEncoder.Desktop.ToolLibrary;
using NegativeEncoder.Desktop.ViewModels;
using NegativeEncoder.Desktop.Views;
using Xunit;

namespace NegativeEncoder.Desktop.Tests;

public sealed class EncodingTests
{
    private static HostPlatform Host(string os, int version = 26) => new(os, os == "osx" ? "arm64" : "x64", new Version(version, 0));

    [Theory]
    [InlineData("osx", "Apple", Encoder.VTEnc)]
    [InlineData("osx", "Intel", Encoder.VTEnc)]
    [InlineData("win", "NVIDIA", Encoder.NVENC)]
    [InlineData("linux", "Intel", Encoder.QSV)]
    [InlineData("win", "AMD", Encoder.VCE)]
    public void SelectsHardwareDefault(string os, string vendor, Encoder expected) =>
        Assert.Equal(expected, EncoderCapabilities.Default(Host(os), new(vendor, [vendor], null)));

    [Fact]
    public void HardwareErrorsAreNotConvertedIntoInventedHardware()
    {
        Assert.Null(EncoderCapabilities.Default(Host("linux"), new("无显卡", [], null)));
        Assert.Throws<InvalidOperationException>(() => EncoderCapabilities.Default(Host("win"), new("失败", [], "probe failed")));
        Assert.Equal(Encoder.NVENC, EncoderCapabilities.Default(Host("win"), new("mixed", ["Intel", "NVIDIA"], null)));
    }

    [Fact]
    public void VtModesFollowImplementationAndOsVersion()
    {
        Assert.Equal([EncodeMode.ABR, EncodeMode.Quality, EncodeMode.CBR], EncoderCapabilities.Modes(Encoder.VTEnc, Host("osx", 15)));
        Assert.Contains(EncodeMode.VBR, EncoderCapabilities.Modes(Encoder.VTEnc, Host("osx")));
        Assert.DoesNotContain(EncodeMode.CQP, EncoderCapabilities.Modes(Encoder.VTEnc, Host("osx")));
        var preset = new Preset { Encoder = Encoder.QSV, EncodeMode = EncodeMode.LAICQ, Codec = Codec.AV1 };
        preset.Encoder = Encoder.VTEnc;
        Assert.Equal(EncodeMode.ABR, preset.EncodeMode);
        Assert.Equal(Codec.AVC, preset.Codec);
        Assert.Equal([Codec.AVC, Codec.HEVC], preset.AvailableCodecs);
        Assert.Equal([ColorDepth.C8Bit], preset.AvailableDepths);
        preset.Encoder = Encoder.NVENC;
        preset.ColorDepth = ColorDepth.C10Bit;
        preset.Encoder = Encoder.VTEnc;
        Assert.Equal(ColorDepth.C8Bit, preset.ColorDepth);
    }

    [Theory]
    [InlineData(Encoder.NVENC, "--vbr-quality", "default")]
    [InlineData(Encoder.QSV, "--d3d", "balanced")]
    [InlineData(Encoder.VCE, "--vbr", "balanced")]
    public void PreservesLegacyParameters(Encoder encoder, string flag, string quality)
    {
        var args = VideoArguments.Build(new() { Encoder = encoder }, Host("win"), false, false);
        Assert.Contains(flag, args);
        Assert.Contains(quality, args);
        Assert.Contains("auto", args);
    }

    [Fact]
    public void QsvLinuxDoesNotReceiveWindowsD3DFlags()
    {
        var args = VideoArguments.Build(new() { Encoder = Encoder.QSV }, Host("linux"), false, false);
        Assert.DoesNotContain(args, s => s.Contains("d3d"));
    }

    [Fact]
    public void VtArgumentsDoNotReuseUnsupportedLegacyControls()
    {
        var p = new Preset { Encoder = Encoder.VTEnc, EncodeMode = EncodeMode.Quality, VtQuality = "0.7", IsSetMaxGop = true, IsStrictGop = true };
        var args = VideoArguments.Build(p, Host("osx"), false, false);
        Assert.Contains("--quality", args);
        Assert.Contains("0.7", args);
        Assert.DoesNotContain("--strict-gop", args);
        Assert.DoesNotContain("--vbr-quality", args);
        Assert.DoesNotContain("--colormatrix", args);
        p.IsSetMaxBitrate = true;
        Assert.Throws<InvalidOperationException>(() => VideoArguments.Build(p, Host("osx"), false, false));
        p.IsSetMaxBitrate = false; p.VtQuality = "NaN";
        Assert.Throws<InvalidOperationException>(() => VideoArguments.Build(p, Host("osx"), false, false));
    }

    [Fact]
    public void VtCbrRequiresExplicitFixedTiming()
    {
        var p = new Preset { Encoder = Encoder.VTEnc, EncodeMode = EncodeMode.CBR };
        Assert.Throws<InvalidOperationException>(() => VideoArguments.Build(p, Host("osx"), false, false));
        p.IsSetAvSync = true; p.FrameRate = "24000/1001";
        Assert.Contains("24000/1001", VideoArguments.Build(p, Host("osx"), false, false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VtColorArgumentsOnlyUseHdrSettingsForHdrJobs(bool pipe)
    {
        var p = new Preset
        {
            Encoder = Encoder.VTEnc, IsOutputHdr = true, OutputHdrType = HdrType.HDR10,
            IsConvertHdrType = true, OldHdrType = HdrType.HLG, NewHdrType = HdrType.HDR10
        };
        var ordinary = VideoArguments.Build(p, Host("osx"), false, pipe);
        Assert.DoesNotContain("--vpp-colorspace", ordinary);
        Assert.DoesNotContain("--color-tags", ordinary);

        var hdr = VideoArguments.Build(p, Host("osx"), true, pipe);
        Assert.Contains("matrix=bt2020nc:bt2020nc,colorprim=bt2020:bt2020,transfer=arib-std-b67:smpte2084,range=auto:auto", hdr);
        Assert.Contains("--color-tags", hdr);
        p.IsConvertHdrType = false;
        Assert.Contains("matrix=auto:auto,colorprim=auto:auto,transfer=auto:auto,range=auto:auto",
            VideoArguments.Build(p, Host("osx"), true, pipe));
    }

    [Fact]
    public void VtSubtitlesProbeSourceColorOnlyForOrdinaryAutomaticArguments()
    {
        var root = Directory.CreateTempSubdirectory("ne-subtitle-color-").FullName;
        try
        {
            foreach (var name in new[] { "ffmpeg", "ffprobe" }) File.WriteAllText(Path.Combine(root, name), "");
            using var client = new DistributionClient();
            var library = new ToolLibraryService(new LibraryPaths(Path.Combine(root, "settings.json"), root),
                Host("osx"), client, new ProcessRunner());
            var builder = new EncodingPlanBuilder(library);
            var preset = new Preset { Encoder = Encoder.VTEnc };
            TaskDraft Subtitle(string operation) => new(operation, "input.mp4", "output.mp4", preset, "", "sub.ass", "");

            Assert.Equal(Path.Combine(root, "ffprobe"), builder.SourceColorProbe(Subtitle("字幕压制")));
            Assert.Null(builder.SourceColorProbe(Subtitle("字幕压制（HDR）")));
            preset.IsUseCustomParameters = true;
            Assert.Null(builder.SourceColorProbe(Subtitle("字幕压制")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void VtPipeKeepsDetectedFieldsAndDefaultsOnlyMissingOnes()
    {
        var log = new List<string>();
        var color = VtSourceColor.FromProbe("""
            {"streams":[{"color_space":"bt2020nc","color_transfer":"arib-std-b67",
                         "color_primaries":"unknown","color_range":"pc","chroma_location":"topleft"}]}
            """, log.Add);
        Assert.Equal("matrix=bt2020nc:auto,transfer=arib-std-b67:auto,colorprim=bt709:auto,range=full:auto", color.Colorspace);
        Assert.Equal("top-left", color.VtChromaLocation);
        Assert.Contains("out_range=full", color.FfmpegScale);
        Assert.Contains("color_primaries", Assert.Single(log));

        log.Clear();
        color = VtSourceColor.FromProbe("""{"streams":[{}]}""", log.Add);
        Assert.Equal(new VtSourceColor("bt709", "bt709", "bt709", "limited", "left"), color);
        Assert.Equal(5, log.Count);
    }

    [Theory]
    [InlineData("""{"streams":[]}""", "v:0")]
    [InlineData("""{"streams":[{"color_space":"bt2020c"}]}""", "color_space=bt2020c")]
    public void VtPipeReportsMissingStreamsAndUnsupportedColor(string probe, string diagnostic)
    {
        var error = Assert.Throws<InvalidDataException>(() => VtSourceColor.FromProbe(probe, _ => { }));
        Assert.Contains(diagnostic, error.Message);
    }

    [AvaloniaFact]
    public void VtFrameRateAcceptsMenuSelectionAndCustomText()
    {
        var window = new MainWindow(false);
        window.Show();
        try
        {
            var preset = window.Model.CurrentPreset;
            preset.Encoder = Encoder.VTEnc;
            preset.IsSetAvSync = true;
            var view = window.FindControl<FunctionTabs>("EditorTabs")!;
            var combo = view.FindControl<ComboBox>("VtFrameRate")!;
            combo.BringIntoView();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("30000/1001", combo.Text);
            Assert.Contains(combo.Items.OfType<FrameRateOption>(), o => o.Label == "30000/1001 (29.97)");
            combo.IsDropDownOpen = true;
            combo.SelectedItem = combo.Items.OfType<FrameRateOption>().Single(o => o.Value == "60000/1001");
            combo.IsDropDownOpen = false;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("60000/1001", preset.FrameRate);

            var editor = combo.GetVisualDescendants().OfType<TextBox>().Single();
            editor.Focus();
            editor.Text = "120000/1001";
            view.FindControl<CheckBox>("ResizeCheckBox")!.Focus();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("120000/1001", preset.FrameRate);
            Assert.True(window.Model.CreatePreset("另一帧率", false));
            Assert.Equal("30000/1001", combo.Text);
            window.Model.CurrentPreset = preset;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("120000/1001", editor.Text);
            var args = VideoArguments.Build(preset.Copy(), Host("osx"), false, false);
            Assert.Equal("120000/1001", args[args.IndexOf("--fps") + 1]);
            editor.Text = "120000/0";
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("CFR 目标帧率", Assert.Throws<InvalidOperationException>(
                () => VideoArguments.Build(preset, Host("osx"), false, false)).Message);
        }
        finally { window.Close(); }
    }

    [Fact]
    public void CustomArgumentsRemainShellFreeAndKeepWindowsPaths()
    {
        Assert.Equal(["--keyfile", @"C:\test dir\file.txt", "--vbr", "1000", ";", "echo"], VideoArguments.ParseCustom("--keyfile \"C:\\test dir\\file.txt\" --vbr 1000 ; echo"));
        Assert.Throws<ArgumentException>(() => VideoArguments.ParseCustom("--vbr \"1000"));
        Assert.Throws<ArgumentException>(() => VideoArguments.ParseCustom("-o other.mp4"));
    }

    [Fact]
    public void StartupDetectionPreservesEditedPresetAndChangesNewDefault()
    {
        var model = new MainWindowViewModel();
        model.CurrentPreset.VbrParam = "9999";
        var encoder = model.CurrentPreset.Encoder;
        model.ApplyHardware(Host("win"), new("AMD", ["AMD"], null));
        Assert.Equal(encoder, model.CurrentPreset.Encoder);
        model.CreatePreset("new", false);
        Assert.Equal(Encoder.VCE, model.CurrentPreset.Encoder);
    }

    [Fact]
    public void RejectsDuplicateQueuedOutputAndFixedScriptBatch()
    {
        var model = new MainWindowViewModel();
        model.AddFiles([Path.Combine(Path.GetTempPath(), "test.mp4")]);
        Assert.True(model.AddTask("视频编码"));
        Assert.False(model.AddTask("视频编码"));
        model.AddFiles([Path.Combine(Path.GetTempPath(), "test2.mp4")]);
        model.ProcessAll = true;
        Assert.False(model.AddTask("VapourSynth"));
    }

    [Fact]
    public void GeneratedVsBatchRebindsEachInputWithoutChangingEditor()
    {
        var model = new MainWindowViewModel { ProcessAll = true };
        model.AddFiles(["/videos/first.mp4", "/videos/second.mp4"]);
        model.Script = "VIDEO_PATH = '/videos/first.mp4'\nvideo = core.lsmas.LWLibavSource(VIDEO_PATH)\n";
        Assert.True(model.AddTask("VapourSynth"));
        Assert.Contains("VIDEO_PATH = '/videos/first.mp4'", model.Tasks[0].Script);
        Assert.Contains("VIDEO_PATH = '/videos/second.mp4'", model.Tasks[1].Script);
        Assert.Contains("VIDEO_PATH = '/videos/first.mp4'", model.Script);
    }

    [AvaloniaFact]
    public void VtUiHidesUnsupportedModesAndKeepsSelectionValid()
    {
        var window = new MainWindow(false);
        window.Show();
        try
        {
            var model = window.Model;
            model.CurrentPreset.Encoder = Encoder.QSV;
            model.CurrentPreset.EncodeMode = EncodeMode.LAICQ;
            model.CurrentPreset.Encoder = Encoder.VTEnc;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(EncodeMode.ABR, model.CurrentPreset.EncodeMode);
            Assert.Equal(Codec.AVC, model.CurrentPreset.Codec);
            var tabs = window.FindControl<FunctionTabs>("EditorTabs")!;
            var fields = tabs.FindControl<ResponsiveFormPanel>("RateControlFields")!;
            Assert.False(fields.Children[2].IsVisible); // CQP
            Assert.False(fields.Children[3].IsVisible); // LA
            Assert.True(fields.Children[6].IsVisible); // ABR
            Assert.Equal(2, tabs.FindControl<ComboBox>("CodecSelect")!.ItemCount);
            model.CurrentPreset.Codec = Codec.HEVC;
            model.CurrentPreset.ColorDepth = ColorDepth.C10Bit;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(ColorDepth.C10Bit, model.CurrentPreset.ColorDepth);
            model.CurrentPreset.Codec = Codec.AVC;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(ColorDepth.C8Bit, model.CurrentPreset.ColorDepth);
        }
        finally { window.Close(); }
    }

    [Fact]
    public async Task QueueStopsAfterFailureThenResumesWithoutRerunningCompletedJobs()
    {
        var model = new MainWindowViewModel { ProcessAll = true };
        model.AddFiles([Path.Combine(Path.GetTempPath(), "first.mp4"), Path.Combine(Path.GetTempPath(), "second.mp4")]);
        var runner = new StubRunner();
        model.ConfigureEncoding(runner);
        Assert.True(model.AddTask("视频编码"));
        await model.RunQueueAsync();
        Assert.Equal("失败", model.Tasks[0].Status);
        Assert.Equal("等待执行", model.Tasks[1].Status);
        Assert.True(model.Tasks[0].Finished);
        runner.Fail = false;
        await model.RunQueueAsync();
        Assert.Equal("已完成", model.Tasks[1].Status);
        Assert.Equal(2, runner.Calls);
        await model.RunQueueAsync();
        Assert.Equal(2, runner.Calls);
    }

    [Fact]
    public async Task CancellingPendingTaskSkipsIt()
    {
        var model = new MainWindowViewModel();
        model.AddFiles([Path.Combine(Path.GetTempPath(), "input.mp4")]);
        var runner = new StubRunner { Fail = false };
        model.ConfigureEncoding(runner);
        Assert.True(model.AddTask("视频编码"));
        model.CancelTask(model.Tasks[0]);
        await model.RunQueueAsync();
        Assert.Equal("已取消", model.Tasks[0].Status);
        Assert.Equal(0, runner.Calls);
    }

    [AvaloniaFact]
    public async Task ConcurrentQueueRefillsInOrderAndRespectsLiveLimitChanges()
    {
        var model = new MainWindowViewModel { ProcessAll = true };
        model.AddFiles(Enumerable.Range(1, 7).Select(i => Path.Combine(Path.GetTempPath(), $"clip-{i}.mp4")));
        var runner = new ControlledRunner();
        model.ConfigureEncoding(runner);
        Assert.True(model.AddTask("视频编码"));
        var queue = model.RunQueueAsync();
        Assert.Equal(model.Tasks.Take(3), runner.Calls.Keys);
        await model.RunQueueAsync();
        model.MaxConcurrentEncodings = 4;
        await runner.WaitForStartAsync(model.Tasks[3]);

        model.MaxConcurrentEncodings = 2;
        runner.Calls[model.Tasks[0]].SetResult();
        runner.Calls[model.Tasks[1]].SetResult();
        runner.Calls[model.Tasks[2]].SetResult();
        await runner.WaitForStartAsync(model.Tasks[4]);
        Assert.False(model.Tasks[5].Started);
        Assert.Equal(2, model.Tasks.Count(task => task.Started && !task.Finished));

        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failingTask = model.Tasks[3];
        failingTask.PropertyChanged += (_, _) => { if (failingTask.Status == "失败") failed.TrySetResult(); };
        runner.Calls[failingTask].SetException(new IOException("encoder failure"));
        await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        runner.Calls[model.Tasks[4]].SetResult();
        await queue.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("失败", model.Tasks[3].Status);
        Assert.Equal("已完成", model.Tasks[4].Status);
        Assert.All(model.Tasks.Skip(5), task => Assert.False(task.Started));

        model.MaxConcurrentEncodings = 1;
        queue = model.RunQueueAsync();
        Assert.True(model.Tasks[5].Started);
        Assert.False(model.Tasks[6].Started);
        model.CancelTask(model.Tasks[5]);
        await runner.WaitForStartAsync(model.Tasks[6]);
        runner.Calls[model.Tasks[6]].SetResult();
        await queue.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("已取消", model.Tasks[5].Status);
        Assert.Equal("已完成", model.Tasks[6].Status);
    }

    private sealed class ControlledRunner : IEncodingRunner
    {
        public Dictionary<TaskDraft, TaskCompletionSource> Calls { get; } = [];
        private readonly Dictionary<TaskDraft, TaskCompletionSource> _started = [];
        public void Validate(TaskDraft task) { }

        public Task WaitForStartAsync(TaskDraft task)
        {
            if (Calls.ContainsKey(task)) return Task.CompletedTask;
            _started[task] = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return _started[task].Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public Task RunAsync(TaskDraft task, CancellationToken token)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Calls.Add(task, completion);
            if (_started.Remove(task, out var started)) started.SetResult();
            return completion.Task.WaitAsync(token);
        }
    }

    private sealed class StubRunner : IEncodingRunner
    {
        public bool Fail { get; set; } = true;
        public int Calls { get; private set; }
        public void Validate(TaskDraft task) { }
        public Task RunAsync(TaskDraft task, CancellationToken token)
        {
            Calls++;
            return Fail ? Task.FromException(new IOException("encoder failure")) : Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ProducerFailureCannotBecomeSuccessAndCancellationStopsBothProcesses()
    {
        EncoderCommand Command(string unix, string windows) => OperatingSystem.IsWindows()
            ? new("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", windows])
            : new("/bin/sh", ["-c", unix]);
        var errors = new List<string>();
        var failure = new EncodingStep("failure", Command("cat >/dev/null", "$input | Out-Null"), Command("echo data; echo failure >&2; exit 7", "[Console]::WriteLine('data'); [Console]::Error.WriteLine('failure'); exit 7"));
        await Assert.ThrowsAnyAsync<Exception>(() => EncodingRunner.RunStepAsync(failure, s => { lock (errors) errors.Add(s); }, CancellationToken.None));
        Assert.Contains("failure", string.Join("", errors));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var slow = new EncodingStep("cancel", Command("cat >/dev/null", "$input | Out-Null"), Command("sleep 30", "Start-Sleep 30"));
        await Assert.ThrowsAnyAsync<Exception>(() => EncodingRunner.RunStepAsync(slow, _ => { }, timeout.Token).WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(timeout.IsCancellationRequested);
    }
}
