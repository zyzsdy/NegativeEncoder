using NegativeEncoder.Desktop.ToolLibrary;

if (args.Length < 2) throw new ArgumentException("Usage: ToolLibrary.Verify ABSOLUTE_ROOT probe|runtime|ffmpeg|releases|generated [basic qtgmc ...]");
var root = Path.GetFullPath(args[0]);
var paths = new LibraryPaths(Path.Combine(root, "verify-settings.json"), root);
using var client = new DistributionClient();
var service = new ToolLibraryService(paths, HostPlatform.Current, client, new ProcessRunner());
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
var token = cancellation.Token;
Console.WriteLine($"Platform: {service.Host}, root: {root}");
if (args[1] == "runtime") await service.InstallRuntimeAsync(args.Skip(2), Console.WriteLine, token);
if (args[1] == "ffmpeg")
{
    var tool = ToolDefinition.All.Single(t => t.Id == "ffmpeg");
    var release = await service.Releases.ResolveAsync(tool, token);
    await service.InstallToolAsync(tool, release, Console.WriteLine, token);
}
if (args[1] == "releases")
    foreach (var tool in ToolDefinition.All.Where(t => t.Repository is not null))
        try { Console.WriteLine(await service.Releases.ResolveAsync(tool, token)); }
        catch (NotSupportedException ex) { Console.WriteLine(tool.Name + ": " + ex.Message); }
foreach (var tool in ToolDefinition.All)
    Console.WriteLine($"{tool.Name}: {await service.ProbeAsync(tool, token)}");
var capabilities = await service.ProbeVsAsync(token);
foreach (var feature in capabilities.Available) Console.WriteLine($"AVAILABLE {feature.Key}: {feature.Value}");
foreach (var feature in capabilities.Errors) Console.WriteLine($"UNAVAILABLE {feature.Key}: {feature.Value}");
if (args[1] == "generated")
{
    var runtime = service.ActiveRuntime() ?? throw new InvalidOperationException("Install runtime first.");
    var scratch = Path.Combine(root, "verify-generated");
    Directory.CreateDirectory(scratch);
    var input = Path.Combine(scratch, "input.y4m");
    using (var stream = File.Create(input))
    {
        stream.Write(System.Text.Encoding.ASCII.GetBytes("YUV4MPEG2 W320 H240 F30000:1001 It A1:1 C420jpeg\n"));
        for (var frame = 0; frame < 12; frame++)
        {
            stream.Write(System.Text.Encoding.ASCII.GetBytes("FRAME\n"));
            stream.Write(Enumerable.Repeat((byte)(16 + frame), 320 * 240).ToArray());
            stream.Write(Enumerable.Repeat((byte)128, 320 * 240 / 2).ToArray());
        }
    }
    var subtitle = Path.Combine(scratch, "subtitle.ass");
    File.WriteAllText(subtitle, "[Script Info]\nScriptType: v4.00+\nPlayResX: 320\nPlayResY: 240\n[Events]\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\nDialogue: 0,0:00:00.00,0:00:10.00,Default,,0,0,0,,NegativeEncoder\n");
    foreach (var feature in new[] { "source", "ivtc", "bwdif", "qtgmc", "subtext" })
    {
        if (!capabilities.Has(feature)) throw new InvalidOperationException("Missing " + feature);
        var model = new NegativeEncoder.Desktop.ViewModels.MainWindowViewModel(capabilities);
        model.AddFiles([input]);
        model.UseIvtc = feature == "ivtc";
        model.UseBwdifDouble = feature == "bwdif";
        model.UseQtgmc = feature == "qtgmc";
        if (feature == "subtext") model.Subtitle = subtitle;
        if (model.VsIssue.Length > 0) throw new InvalidOperationException(model.VsIssue);
        var script = Path.Combine(scratch, feature + ".vpy");
        File.WriteAllText(script, model.Script);
        var result = await new ProcessRunner().RunAsync(service.VspipePath(runtime), ["--end", "5", script, "."], token, service.RuntimeEnvironment(runtime), 90);
        result.EnsureSuccess("Generated script " + feature);
        Console.WriteLine("GENERATED SCRIPT PASSED: " + feature);
    }
}
