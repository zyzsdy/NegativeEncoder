using NegativeEncoder.Desktop.Models;
using NegativeEncoder.Desktop.ToolLibrary;

namespace NegativeEncoder.Desktop.EncodingTasks;

public sealed record EncoderCommand(string Executable, IReadOnlyList<string> Arguments, IDictionary<string, string>? Environment = null)
{
    public string Preview => string.Join(" ", new[] { Executable }.Concat(Arguments).Select(a => System.Text.Json.JsonSerializer.Serialize(a)));
}
public sealed record EncodingStep(string Name, EncoderCommand Command, EncoderCommand? Producer = null);
public sealed record EncodingPlan(string WorkDirectory, string StagedOutput, List<EncodingStep> Steps, string? ScriptPath = null, string? Script = null);

public sealed class EncodingPlanBuilder(ToolLibraryService library)
{
    private static bool IsPipe(string operation) => operation is "VapourSynth" or "ffmpeg pipe · 无音频"
        or "ffmpeg pipe · 复制音频" or "ffmpeg pipe · 处理音频与视频";

    public string? SourceColorProbe(TaskDraft task) =>
        task.Preset.IsVtEnc && !task.Preset.IsUseCustomParameters && (task.Operation == "字幕压制" || IsPipe(task.Operation))
            ? library.FindFfprobe() : null;

    public static string WorkDirectoryFor(TaskDraft task) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(task.OutputPath))!, ".negativeencoder-" + Guid.NewGuid().ToString("N"));

    public EncodingPlan Build(TaskDraft task, string work, VtSourceColor? sourceColor)
    {
        var p = task.Preset;
        var output = Path.Combine(work, "output" + Path.GetExtension(task.OutputPath));
        var steps = new List<EncodingStep>();
        var plan = new EncodingPlan(work, output, steps);
        EncoderCommand Command(string id, IEnumerable<string> args, IDictionary<string, string>? environment = null) =>
            new(library.FindExecutable(ToolDefinition.All.Single(t => t.Id == id)) ?? throw new FileNotFoundException($"当前工具库未安装 {id}，请打开工具库管理。"), args.ToArray(), environment);
        EncoderCommand Ffmpeg(IEnumerable<string> args) => Command("ffmpeg", new[] { "-hide_banner", "-nostdin", "-n" }.Concat(args));
        void Audio(string destination)
        {
            if (library.Host.Os == "osx")
            {
                // VTEnc currently has no audio-only mode. Its native AudioToolbox path is used
                // in a small intermediate movie, then AAC packets are copied without re-encoding.
                var movie = Path.Combine(work, "audio-source.mp4");
                steps.Add(new("VTEnc AAC", Command("vtenc", ["-i", task.InputPath, "-o", movie, "--avsw", "--codec", "hevc", "--output-depth", "10", "--bitrate", "1000", "--output-res", "64x64", "--vpp-colorspace", "matrix=auto:auto,transfer=auto:auto,colorprim=auto:auto,range=auto:auto", "--audio-codec", "aac", "--audio-bitrate", p.AudioBitrate])));
                steps.Add(new("提取 AAC", Ffmpeg(["-i", movie, "-map", "0:a:0", "-vn", "-c:a", "copy", destination])));
            }
            else steps.Add(new("FFmpeg AAC", Ffmpeg(["-i", task.InputPath, "-map", "0:a:0", "-vn", "-c:a", "aac", "-b:a", p.AudioBitrate + "k", destination])));
        }
        void Mux(string video, string audio, OutputFormat format)
        {
            var args = new List<string> { "-i", video, "-i", audio, "-map", "0:v:0", "-map", "1:a", "-c", "copy" };
            if (format == OutputFormat.MP4) args.AddRange(["-movflags", "+faststart"]);
            args.AddRange(["-f", Format(format), output]);
            steps.Add(new("封装", Ffmpeg(args)));
        }
        if (task.Operation == "编码音频") { Audio(output); return plan; }
        if (task.Operation == "提取音频")
        {
            steps.Add(new("提取音频", Ffmpeg(["-i", task.InputPath, "-map", "0:a", "-vn", "-sn", "-c:a", "copy", output])));
            return plan;
        }
        if (task.Operation == "封装") { Mux(task.InputPath, task.AudioInputPath, p.MuxFormat); return plan; }
        if (task.Operation.StartsWith("标记为 ", StringComparison.Ordinal))
        {
            // Matches the original Windows operation: HEVC bitstream metadata only.
            var (prim, transfer, matrix) = task.Operation switch
            {
                "标记为 SDR" => (1, 1, 1),
                "标记为 HDR10" => (9, 16, 9),
                "标记为 HLG" => (9, 18, 9),
                _ => throw new InvalidOperationException("未知色彩标记任务。")
            };
            steps.Add(new("HEVC 色彩标记", Ffmpeg(["-i", task.InputPath, "-map", "0", "-c", "copy", "-bsf:v", $"hevc_metadata=colour_primaries={prim}:transfer_characteristics={transfer}:matrix_coefficients={matrix}", "-f", Format(p.OutputFormat), output])));
            return plan;
        }
        var pipe = IsPipe(task.Operation);
        if (!pipe && task.Operation is not ("视频编码" or "HDR 编码" or "字幕压制" or "字幕压制（HDR）"))
            throw new InvalidOperationException($"未知操作：{task.Operation}");
        var audioMode = task.Operation switch
        {
            "ffmpeg pipe · 无音频" => AudioEncode.None,
            "ffmpeg pipe · 复制音频" => AudioEncode.Copy,
            "ffmpeg pipe · 处理音频与视频" => AudioEncode.Encode,
            _ => p.AudioEncode
        };
        // Windows/Linux audio always goes through the managed FFmpeg, not a bundled
        // encoder's potentially different AAC backend. VTEnc handles direct macOS audio.
        var separateAudio = audioMode != AudioEncode.None && (pipe || !p.IsUseCustomParameters && audioMode == AudioEncode.Encode && library.Host.Os != "osx");
        var videoOutput = separateAudio ? Path.Combine(work, "video.mkv") : output;
        var args = VideoArguments.Build(p, library.Host, task.Operation.Contains("HDR", StringComparison.Ordinal), pipe);
        if (sourceColor is not null)
            args.AddRange(["--vpp-colorspace", sourceColor.Colorspace, "--input-chroma-loc", sourceColor.VtChromaLocation]);
        args.AddRange(["-i", pipe ? "-" : task.InputPath, "-o", videoOutput, "-f", separateAudio ? "matroska" : Format(p.OutputFormat)]);
        args.Add(pipe ? p.IsVtEnc ? "--avsw" : "--y4m" : p.Decoder == Decoder.AVHW ? "--avhw" : "--avsw");
        if (p.IsVtEnc && (pipe || separateAudio || !p.IsUseCustomParameters && audioMode == AudioEncode.None)) args.Add("--no-audio");
        else if (!pipe && !separateAudio && !p.IsUseCustomParameters)
        {
            if (audioMode == AudioEncode.Copy) args.Add("--audio-copy");
            if (audioMode == AudioEncode.Encode) args.AddRange(["--audio-codec", "aac", "--audio-bitrate", p.AudioBitrate]);
        }
        if (task.Operation.StartsWith("字幕压制", StringComparison.Ordinal))
        {
            if (task.SubtitlePath.Contains('"')) throw new ArgumentException("字幕路径不能包含双引号。");
            args.AddRange(["--vpp-subburn", "filename=\"" + task.SubtitlePath + "\""]);
        }
        EncoderCommand? producer = null;
        if (task.Operation == "VapourSynth")
        {
            if (string.IsNullOrWhiteSpace(task.Script)) throw new InvalidOperationException("VS 脚本为空，请先生成或填写脚本。");
            var runtime = library.ActiveRuntime() ?? throw new InvalidOperationException("当前工具库未安装 VapourSynth。");
            var scriptPath = Path.Combine(work, "input.vpy");
            plan = plan with { ScriptPath = scriptPath, Script = task.Script };
            producer = Command("vapoursynth", ["-c", "y4m", scriptPath, "-"], library.RuntimeEnvironment(runtime));
        }
        else if (pipe)
        {
            var ffmpegArgs = new List<string> { "-i", task.InputPath, "-map", "0:v:0", "-an", "-sn" };
            if (sourceColor is not null) ffmpegArgs.AddRange(["-vf", sourceColor.FfmpegScale]);
            if (sourceColor is not null && p.ColorDepth == ColorDepth.C10Bit)
                ffmpegArgs.AddRange(["-pix_fmt", "yuv420p10le", "-strict", "-1"]);
            else ffmpegArgs.AddRange(["-pix_fmt", "yuv420p"]);
            ffmpegArgs.AddRange(["-f", "yuv4mpegpipe", "-"]);
            producer = Ffmpeg(ffmpegArgs);
        }
        steps.Add(new("视频编码", Command(EncoderCapabilities.ToolId(p.Encoder), args), producer));
        if (separateAudio)
        {
            var audio = task.InputPath;
            if (audioMode == AudioEncode.Encode) { audio = Path.Combine(work, "audio.m4a"); Audio(audio); }
            Mux(videoOutput, audio, p.OutputFormat);
        }
        return plan;
    }

    public static string Format(OutputFormat format) => format switch
    {
        OutputFormat.MP4 => "mp4",
        OutputFormat.MKV => "matroska",
        OutputFormat.MPEGTS => "mpegts",
        OutputFormat.FLV => "flv",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };
}
