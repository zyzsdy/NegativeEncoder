using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NegativeEncoder.Desktop.ToolLibrary;
using NegativeEncoder.Desktop.ViewModels;
using NegativeEncoder.Desktop.Views;
using Xunit;

namespace NegativeEncoder.Desktop.Tests;

public sealed class ToolLibraryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ne-library-test-" + Guid.NewGuid().ToString("N"));
    public ToolLibraryTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);
    private LibraryPaths Paths() => new(Path.Combine(_root, "settings.json"), Path.Combine(_root, "Libs"));
    private static HostPlatform Mac => new("osx", "arm64", new Version(15, 0));

    [Fact]
    public void SwitchingPersistsWithoutMovingAndInvalidSwitchKeepsPreviousRoot()
    {
        var paths = Paths();
        paths.Apply(Path.Combine(_root, "first"));
        File.WriteAllText(Path.Combine(paths.Root, "custom.txt"), "keep");
        var first = paths.Root;
        paths.Apply(Path.Combine(_root, "second"));
        Assert.True(File.Exists(Path.Combine(first, "custom.txt")));
        Assert.False(File.Exists(Path.Combine(paths.Root, "custom.txt")));
        Assert.Equal(paths.Root, Paths().Root);
        var second = paths.Root;
        Assert.Throws<ArgumentException>(() => paths.Apply("relative"));
        var broken = Path.Combine(_root, "broken");
        Directory.CreateDirectory(broken);
        File.WriteAllText(Path.Combine(broken, "library.json"), "not json");
        Assert.Throws<JsonException>(() => paths.Apply(broken));
        Assert.Equal(second, Paths().Root);
        Assert.Contains("Application Support", LibraryPaths.DefaultRoot(Mac, "/Applications/NE.app", "/Users/test"));
        Assert.Equal(Path.GetFullPath("/NegativeEncoder/Libs"), LibraryPaths.DefaultRoot(new("linux", "x64", new(6, 0)), "/NegativeEncoder/NEbin", "/home/test"));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("folder/../../escape")]
    [InlineData("C:/escape")]
    [InlineData("/absolute")]
    [InlineData("..\\escape")]
    public void ArchivePathsCannotEscape(string entry)
    {
        var file = Path.Combine(_root, "bad.zip");
        using (var zip = ZipFile.Open(file, ZipArchiveMode.Create)) zip.CreateEntry(entry);
        Assert.Throws<InvalidDataException>(() => SafeArchive.Extract(file, Path.Combine(_root, "out"), default));
    }

    [Fact]
    public void CatalogChecksPlatformAbiAndEveryAssetHasHash()
    {
        var catalog = RuntimeCatalog.Load();
        Assert.Null(catalog.Unavailable(Mac, "qtgmc"));
        Assert.NotNull(catalog.Unavailable(Mac with { Version = new(14, 0) }, "basic"));
        Assert.NotNull(catalog.Unavailable(new("linux", "arm64", new(6, 0), new(2, 28)), "qtgmc"));
        Assert.Null(catalog.Unavailable(new("linux", "arm64", new(6, 0), new(2, 28)), "basic"));
        Assert.NotNull(catalog.Unavailable(new("linux", "x64", new(6, 0)), "basic"));
        Assert.NotNull(catalog.Unavailable(Mac, "subtitles"));
        foreach (var asset in catalog.PythonAssets.Concat(catalog.Packages.SelectMany(p => p.Assets)))
        {
            Assert.Matches("^[0-9a-fA-F]{64}$", asset.Sha256);
            DistributionClient.ValidateUrl(asset.Url);
        }
    }

    [Fact]
    public void StableReleaseSelectionRejectsNightlyAndAmbiguousAssets()
    {
        using var release = JsonDocument.Parse("""{"draft":false,"prerelease":true,"assets":[{"name":"NVEncC_9.41_x64.7z"},{"name":"NVEncC_9.41_x86.7z"}]}""");
        Assert.Throws<InvalidDataException>(() => ReleaseResolver.EnsureStable(release.RootElement));
        Assert.Equal("NVEncC_9.41_x64.7z", ReleaseResolver.SelectAsset(release.RootElement, "_x64\\.7z$").GetProperty("name").GetString());
        Assert.Throws<NotSupportedException>(() => ReleaseResolver.SelectAsset(release.RootElement, "\\.7z$"));
        Assert.Throws<InvalidDataException>(() => DistributionClient.ValidateUrl("https://example.com/tool.zip"));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task FailedHashOrExecutionOrCancellationNeverReplacesActiveManifest(bool wrongHash, bool cancel)
    {
        var paths = Paths();
        Directory.CreateDirectory(paths.Root);
        var manifest = new LibraryManifest(1, new() { ["ffmpeg"] = new("1.0", "old", "old/ffmpeg", "test", []) });
        LibraryJson.WriteAtomic(Path.Combine(paths.Root, "library.json"), manifest);
        var before = File.ReadAllText(Path.Combine(paths.Root, "library.json"));
        var bytes = ZipBytes();
        using var client = new DistributionClient(new ResponseHandler(bytes));
        var service = new ToolLibraryService(paths, Mac, client, new FakeRunner(fail: true));
        var hash = wrongHash ? new string('0', 64) : Convert.ToHexString(SHA256.HashData(bytes));
        var release = new ToolRelease("1.2.3", "test", [new("https://github.com/test/test/tool.zip", hash, "tool.zip")]);
        using var cancellation = new CancellationTokenSource();
        if (cancel) cancellation.Cancel();
        Task Install() => service.InstallToolAsync(ToolDefinition.All[0], release, _ => { }, cancellation.Token);
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(Install);
        else if (wrongHash) await Assert.ThrowsAsync<InvalidDataException>(Install);
        else await Assert.ThrowsAsync<InvalidOperationException>(Install);
        Assert.Equal(before, File.ReadAllText(Path.Combine(paths.Root, "library.json")));
    }

    [Fact]
    public async Task SuccessfulInstallActivatesVerifiedGenerationWithoutDeletingOldFiles()
    {
        var paths = Paths();
        Directory.CreateDirectory(paths.Root);
        File.WriteAllText(Path.Combine(paths.Root, "ffmpeg"), "manual version");
        var bytes = ZipBytes();
        using var client = new DistributionClient(new ResponseHandler(bytes));
        var service = new ToolLibraryService(paths, Mac, client, new FakeRunner(false));
        var release = new ToolRelease("1.2.3", "test", [new("https://github.com/test/test/tool.zip", Convert.ToHexString(SHA256.HashData(bytes)), "tool.zip")]);
        await service.InstallToolAsync(ToolDefinition.All[0], release, _ => { }, default);
        var installed = LibraryPaths.ReadManifest(paths.Root).Tools["ffmpeg"];
        Assert.Equal("1.2.3", installed.Version);
        Assert.StartsWith(".managed", installed.Directory);
        Assert.Equal("manual version", File.ReadAllText(Path.Combine(paths.Root, "ffmpeg")));
        Assert.Equal(LibraryPaths.Within(paths.Root, installed.Executable), service.FindExecutable(ToolDefinition.All[0]));
    }

    [Fact]
    public void CustomFilesSurviveUpgradeAndCollisionAborts()
    {
        var old = Path.Combine(_root, "old");
        var next = Path.Combine(_root, "next");
        Directory.CreateDirectory(old);
        Directory.CreateDirectory(next);
        LibraryJson.WriteAtomic(Path.Combine(old, "managed-files.json"), new[] { "owned.py" });
        File.WriteAllText(Path.Combine(old, "owned.py"), "old version");
        File.WriteAllText(Path.Combine(old, "manual.py"), "user plugin");
        ToolLibraryService.PreserveCustomFiles(old, next, default);
        Assert.Equal("user plugin", File.ReadAllText(Path.Combine(next, "manual.py")));
        Assert.False(File.Exists(Path.Combine(next, "owned.py")));
        Assert.Throws<IOException>(() => ToolLibraryService.PreserveCustomFiles(old, next, default));
        Assert.Equal("user plugin", File.ReadAllText(Path.Combine(old, "manual.py")));
    }

    [Fact]
    public void GeneratorBlocksMissingFeaturesAndNeverFallsBackOrOverwritesOnRefresh()
    {
        var model = new MainWindowViewModel { Script = "# manual" };
        model.GenerateVsScript();
        Assert.Equal("# manual", model.Script);
        Assert.NotEmpty(model.VsIssue);
        model.ApplyVsCapabilities(VapourSynthTests.FullCapabilities);
        Assert.Equal("# manual", model.Script);
        model.Subtitle = "/test.ass";
        model.UseVsfmod = true;
        Assert.Contains("core.vsfm.TextSubMod", model.Script);
        Assert.DoesNotContain("core.sub.TextFile", model.Script);
        var capabilities = VapourSynthTests.FullCapabilities;
        capabilities.Available.Remove("vsfm");
        model.ApplyVsCapabilities(capabilities);
        var script = model.Script;
        model.GenerateVsScript();
        Assert.Equal(script, model.Script);
        Assert.NotEmpty(model.VsIssue);
        model.UseSubtext = true;
        Assert.Empty(model.VsIssue);
        Assert.Contains("core.sub.TextFile", model.Script);
        model.UseQtgmc = true;
        model.UseBwdifDouble = true;
        Assert.NotEmpty(model.VsIssue);
        model.UseQtgmc = false;
        Assert.Empty(model.VsIssue);
        Assert.Contains("core.bwdif.Bwdif(video, field=3)", model.Script);
        model.VsResize = true;
        script = model.Script;
        model.VsWidth = "1279";
        Assert.Equal(script, model.Script);
        Assert.NotEmpty(model.VsIssue);
    }

    [AvaloniaFact]
    public void LibraryWindowKeepsPathWarningAndActionsAccessibleAtMinimumSize()
    {
        using var client = new DistributionClient(new ResponseHandler([]));
        var runner = new FakeRunner(false);
        var model = new ToolLibraryViewModel(new(Paths(), Mac, client, runner), runner);
        var owner = new MainWindow(false);
        owner.Show();
        var window = new ToolLibraryWindow(model, false) { Width = 820, Height = 640 };
        window.Show(owner);
        try
        {
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(7, model.Tools.Count);
            Assert.Equal(4, model.Groups.Count);
            Assert.True(window.FindControl<TextBox>("LibraryPathField")!.IsEffectivelyEnabled);
            Assert.True(window.FindControl<ListBox>("ToolRows")!.Bounds.Height > 100);
            Assert.True(window.FindControl<Button>("CloseLibraryButton")!.IsEffectivelyEnabled);
            window.FindControl<TabControl>("LibraryTabs")!.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            Assert.True(window.FindControl<ListBox>("PluginRows")!.Bounds.Height > 100);
        }
        finally { window.Close(); owner.Close(); }
    }

    private static byte[] ZipBytes()
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
            foreach (var name in new[] { "ffmpeg", "ffprobe" })
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write("fake binary");
            }
        return buffer.ToArray();
    }
    private sealed class ResponseHandler(byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        }
    }
    private sealed class FakeRunner(bool fail) : ProcessRunner
    {
        public override Task<ProcessResult> RunAsync(string executable, IEnumerable<string> arguments, CancellationToken cancellationToken,
            IDictionary<string, string>? environment = null, int timeoutSeconds = 45) =>
            Task.FromResult(new ProcessResult(fail ? 1 : 0, "ffmpeg version 1.2.3 test", fail ? "missing dynamic library" : ""));
    }
}
