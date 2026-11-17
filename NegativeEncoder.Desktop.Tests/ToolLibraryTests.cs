using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.Interactivity;
using NegativeEncoder.Desktop.Networking;
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

    [Theory]
    [InlineData("VTEnc 0.1.0-alpha.1 (macOS arm64; VideoToolbox hardware encoder)", "0.1.0-alpha.1")]
    [InlineData("VTEnc 0.1.0", "0.1.0")]
    [InlineData("vtenc version v0.1.0-rc.2", "0.1.0-rc.2")]
    [InlineData("VTEnc 0.1.0-alpha.1+build.42", "0.1.0-alpha.1+build.42")]
    [InlineData("VTEnc 0.1.0+build.42", "0.1.0+build.42")]
    [InlineData("VTEnc version unavailable", "")]
    public void VtencVersionPreservesPrereleaseAndBuildMetadata(string output, string expected)
    {
        Assert.Equal(expected, ToolLibraryService.ParseVersion("vtenc", output));
    }

    [Theory]
    [InlineData("0.1.0-alpha.1", true)]
    [InlineData("0.1.0", false)]
    [InlineData("0.1.0-alpha.2", false)]
    [InlineData("0.1.1-alpha.1", false)]
    public async Task VtencInstallRequiresExactPrereleaseVersion(string actual, bool matches)
    {
        var paths = Paths();
        Directory.CreateDirectory(paths.Root);
        var manifestPath = Path.Combine(paths.Root, "library.json");
        LibraryJson.WriteAtomic(manifestPath, new LibraryManifest(1, new()
        {
            ["vtenc"] = new("0.0.1", "old", "old/vtenc", "test", [])
        }));
        var before = File.ReadAllText(manifestPath);
        var bytes = ZipBytes("bin/vtenc");
        using var client = new DistributionClient(new ResponseHandler(bytes));
        var output = $"VTEnc {actual} (macOS arm64; VideoToolbox hardware encoder)";
        var service = new ToolLibraryService(paths, Mac, client, new FakeRunner(false, output));
        var tool = ToolDefinition.All.Single(t => t.Id == "vtenc");
        var release = new ToolRelease("0.1.0-alpha.1", "test",
            [new("https://github.com/test/test/tool.zip", Convert.ToHexString(SHA256.HashData(bytes)), "tool.zip")], true);

        if (matches)
        {
            await service.InstallToolAsync(tool, release, _ => { }, default);
            var installed = LibraryPaths.ReadManifest(paths.Root).Tools["vtenc"];
            Assert.Equal(release.Version, installed.Version);
            Assert.StartsWith(".managed", installed.Directory);
            var probe = await service.ProbeAsync(tool, default);
            Assert.NotNull(probe);
            Assert.True(probe.Runs);
            Assert.Equal(release.Version, probe.Version);
        }
        else
        {
            var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
                service.InstallToolAsync(tool, release, _ => { }, default));
            Assert.Contains($"期望 {release.Version}，实际 {actual}", error.Message);
            Assert.Contains(output, error.Message);
            Assert.Equal(before, File.ReadAllText(manifestPath));
        }
    }

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

    [AvaloniaFact]
    public async Task ProxyFormValidatesPersistsAndLocksDuringRequests()
    {
        var settingsFile = Path.Combine(_root, "network.json");
        var network = new NetworkSettings(settingsFile);
        network.ApplyProxy("http://localhost:7890");
        using var client = new DistributionClient(new PendingHandler(), network);
        var runner = new FakeRunner(false);
        var model = new ToolLibraryViewModel(new(Paths(), Mac, client, runner), runner);
        var owner = new MainWindow(false);
        owner.Show();
        var window = new ToolLibraryWindow(model, false) { Width = 820, Height = 640 };
        window.Show(owner);
        try
        {
            Dispatcher.UIThread.RunJobs();
            var field = window.FindControl<TextBox>("ProxyAddressField")!;
            var apply = window.FindControl<Button>("ApplyProxyButton")!;
            Assert.Equal("http://localhost:7890", field.Text);
            field.Text = "invalid";
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("invalid", model.ProxyAddress);
            Assert.Equal("http://localhost:7890", network.ProxyAddress);
            Assert.True(field.IsFocused);
            Assert.Equal(model.ProxyError, Avalonia.Automation.AutomationProperties.GetHelpText(field));
            Assert.True(window.FindControl<TextBlock>("ProxyErrorText")!.IsVisible);
            Assert.Contains("代理未保存", model.Status);
            Assert.True(window.FindControl<ListBox>("ToolRows")!.Bounds.Height > 100);

            field.Text = "socks5://localhost:1080";
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(model.ProxyError);
            Assert.Equal(field.Text, new NetworkSettings(settingsFile).ProxyAddress);
            model.LibraryPath = Path.Combine(_root, "another-library");
            await model.ApplyPathAsync();
            Assert.Equal(field.Text, new NetworkSettings(settingsFile).ProxyAddress);

            var refresh = model.RefreshAsync(true);
            Dispatcher.UIThread.RunJobs();
            Assert.True(model.Busy);
            Assert.False(field.IsEffectivelyEnabled);
            Assert.False(apply.IsEffectivelyEnabled);
            Assert.False(model.ApplyProxy());
            model.Cancel();
            await refresh;
            Dispatcher.UIThread.RunJobs();
            Assert.True(field.IsEffectivelyEnabled);
            field.Text = "";
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Empty(new NetworkSettings(settingsFile).ProxyAddress);
        }
        finally { window.Close(); owner.Close(); }
    }

    private static byte[] ZipBytes(params string[] names)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
            foreach (var name in names.Length == 0 ? ["ffmpeg", "ffprobe"] : names)
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
    private sealed class PendingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Pending request should only finish by cancellation.");
        }
    }
    private sealed class FakeRunner(bool fail, string output = "ffmpeg version 1.2.3 test") : ProcessRunner
    {
        public override Task<ProcessResult> RunAsync(string executable, IEnumerable<string> arguments, CancellationToken cancellationToken,
            IDictionary<string, string>? environment = null, int timeoutSeconds = 45) =>
            Task.FromResult(new ProcessResult(fail ? 1 : 0, output, fail ? "missing dynamic library" : ""));
    }
}
