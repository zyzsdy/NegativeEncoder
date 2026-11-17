using NegativeEncoder.Desktop.EncodingTasks;
using NegativeEncoder.Desktop.Models;
using NegativeEncoder.Desktop.Networking;
using NegativeEncoder.Desktop.ToolLibrary;
using NegativeEncoder.Desktop.ViewModels;

if (args.Length < 2) throw new ArgumentException("Usage: Encoding.Verify LIBS_ROOT OUTPUT_DIRECTORY [--install-encoder] [--all-encoders] [--proxy socks5://HOST:PORT]");
var root = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(output);
var network = new NetworkSettings(Path.Combine(output, "network.json"));
var proxyIndex = Array.IndexOf(args, "--proxy");
if (proxyIndex >= 0) network.ApplyProxy(args[proxyIndex + 1]);
using var client = new DistributionClient(network: network);
var runner = new ProcessRunner();
var service = new ToolLibraryService(new LibraryPaths(Path.Combine(output, "settings.json"), root), HostPlatform.Current, client, runner);
using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(12));
var token = timeout.Token;
var hardware = await new HardwareDetector(runner).DetectAsync(token);
var selected = EncoderCapabilities.Default(service.Host, hardware);
Console.WriteLine($"Host={service.Host}; GPU={hardware.Description}; Default={selected}");
var encoders = selected is null ? Array.Empty<Encoder>() : new[] { selected.Value };
if (args.Contains("--all-encoders") && service.Host.Os == "win")
    encoders = new[] { hardware.Vendors.Contains("NVIDIA") ? (Encoder?)Encoder.NVENC : null, hardware.Vendors.Contains("Intel") ? (Encoder?)Encoder.QSV : null, hardware.Vendors.Contains("AMD") ? (Encoder?)Encoder.VCE : null }.OfType<Encoder>().ToArray();
if (args.Contains("--install-encoder"))
    foreach (var encoder in encoders)
    {
        var tool = ToolDefinition.All.Single(t => t.Id == EncoderCapabilities.ToolId(encoder));
        if (service.FindExecutable(tool) is null)
            await service.InstallToolAsync(tool, await service.Releases.ResolveAsync(tool, token), message => { if (!message.StartsWith("下载 ", StringComparison.Ordinal)) Console.WriteLine(message); }, token);
    }
var ffmpeg = service.FindExecutable(ToolDefinition.All.Single(t => t.Id == "ffmpeg")) ?? throw new FileNotFoundException("Install ffmpeg in verification Libs first.");
var input = Path.Combine(output, "输入 视频.mp4");
(await runner.RunAsync(ffmpeg, ["-nostdin", "-n", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=30", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000", "-t", "1", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-color_primaries", "bt709", "-color_trc", "bt709", "-colorspace", "bt709", "-color_range", "tv", "-c:a", "aac", input], token)).EnsureSuccess("fixture");
var ass = Path.Combine(output, "字幕 测试.ass");
await File.WriteAllTextAsync(ass, "[Script Info]\nScriptType: v4.00+\nPlayResX: 320\nPlayResY: 240\n[V4+ Styles]\nFormat: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\nStyle: Default,Arial,24,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,1,0,2,10,10,10,1\n[Events]\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\nDialogue: 0,0:00:00.00,0:00:01.00,Default,,0,0,0,,Encoding test\n", token);
var executor = new EncodingRunner(service);
async Task<string> Execute(string name, string operation, Preset preset, string extension = ".mp4", string? audio = null, string? source = null, string script = "")
{
    var destination = Path.Combine(output, name + extension);
    var task = new TaskDraft(operation, source ?? input, destination, preset.Copy(), script, ass, audio ?? "");
    try { await executor.RunAsync(task, token); }
    catch { Console.Error.WriteLine(task.Details); throw; }
    (await runner.RunAsync(ffmpeg, ["-v", "error", "-i", destination, "-f", "null", "-"], token)).EnsureSuccess("decode " + name);
    Console.WriteLine("PASS " + name);
    return destination;
}
var basic = new Preset { Encoder = selected ?? Encoder.NVENC, EncodeMode = selected == Encoder.VTEnc ? EncodeMode.ABR : EncodeMode.VBR, Decoder = Decoder.AVSW };
var aac = await Execute("audio", "编码音频", basic, ".m4a");
await Execute("extract", "提取音频", basic, ".m4a");
foreach (var format in new[] { OutputFormat.MP4, OutputFormat.MKV, OutputFormat.MPEGTS })
    await Execute("mux-" + format, "封装", new Preset { MuxFormat = format }, format == OutputFormat.MPEGTS ? ".ts" : "." + format.ToString().ToLowerInvariant(), aac);
foreach (var encoder in encoders)
{
    var p = basic.Copy(); p.Encoder = encoder; p.Codec = Codec.HEVC;
    var direct = await Execute(encoder + "-direct", "视频编码", p);
    p.AudioEncode = AudioEncode.None;
    await Execute(encoder + "-silent", "视频编码", p);
    p.AudioEncode = AudioEncode.Encode;
    await Execute(encoder + "-aac", "视频编码", p);
    await Execute(encoder + "-subtitles", "字幕压制", p);
    foreach (var action in new[] { "无音频", "复制音频", "处理音频与视频" })
        await Execute(encoder + "-pipe-" + action, "ffmpeg pipe · " + action, p);
    foreach (var hdr in new[] { "SDR", "HDR10", "HLG" })
        await Execute(encoder + "-tag-" + hdr, "标记为 " + hdr, p, source: direct);
    if (service.ActiveRuntime() is not null)
    {
        var capabilities = await service.ProbeVsAsync(token);
        var model = new MainWindowViewModel(capabilities);
        model.AddFiles([input]);
        if (model.VsIssue.Length > 0) throw new InvalidOperationException(model.VsIssue);
        await Execute(encoder + "-vs", "VapourSynth", p, script: model.Script);
    }
    else Console.WriteLine("NOT TESTED: VS runtime not installed");
}
if (encoders.Length == 0) Console.WriteLine("NOT TESTED: hardware video encoding (no GPU exposed)");
Console.WriteLine("VERIFICATION COMPLETE " + output);
