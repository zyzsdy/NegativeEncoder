using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.Interactivity;
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
    public void ManualToolAliasesIgnoreCaseButDistinctLocationsStillFail()
    {
        var paths = Paths();
        var folder = Path.Combine(paths.Root, "ffmpeg");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "ffmpeg.exe"), "manual");
        using var client = new DistributionClient(new ResponseHandler([]));
        var service = new ToolLibraryService(paths, new("win", "x64", new Version(10, 0)), client, new FakeRunner(false, ""));
        Assert.True(File.Exists(service.FindExecutable(ToolDefinition.All[0])));
        File.WriteAllText(Path.Combine(paths.Root, "ffmpeg.exe"), "another copy");
        Assert.Throws<InvalidDataException>(() => service.FindExecutable(ToolDefinition.All[0]));
    }

    [Fact]
    public void ToolkitPathIncludesActiveAndManualCommandsOnceAndSkipsStaleVersions()
    {
        var paths = Paths();
        var host = HostPlatform.Current;
        using var client = new DistributionClient(new ResponseHandler([]));
        var service = new ToolLibraryService(paths, host, client, new FakeRunner(false));
        var tool = ToolDefinition.All.Single(t => t.Id == "ffmpeg");
        Assert.Empty(ToolkitEnvironment.Read(service).Directories);
        var active = Path.Combine(".managed", "ffmpeg", "active");
        var binary = Path.Combine(active, "bin", tool.Executable(host));
        var companion = Path.Combine(active, "helpers");
        var manual = "user's tools & scripts";
        var name = host.Os == "win" ? "helper.cmd" : "helper";
        foreach (var file in new[] { binary, Path.Combine(companion, name), Path.Combine(manual, name),
            Path.Combine(".managed", "ffmpeg", "old", name), Path.Combine(".downloads", name) })
        {
            var full = Path.Combine(paths.Root, file);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, "");
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(full, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }
        LibraryJson.WriteAtomic(Path.Combine(paths.Root, "library.json"), new LibraryManifest(1, new()
        {
            [tool.Id] = new("1", active, binary, "test", [])
        }));
        var pathBefore = Environment.GetEnvironmentVariable("PATH");
        var environment = ToolkitEnvironment.Read(service);
        Assert.Equal(new[] { Path.GetDirectoryName(binary)!, companion, manual }
            .Select(dir => Path.Combine(paths.Root, dir)), environment.Directories);
        Assert.Empty(environment.Variables);
        Assert.Equal(pathBefore, Environment.GetEnvironmentVariable("PATH"));
        File.Delete(Path.Combine(paths.Root, binary));
        Assert.Throws<FileNotFoundException>(() => ToolkitEnvironment.Read(service));
    }

    [Fact]
    public void ToolkitShellDefaultsFollowAvailabilityAndExplicitChoicesPersist()
    {
        var file = Path.Combine(_root, "network.json");
        File.WriteAllText(file, """{"proxyAddress":""}""");
        var settings = new AppSettings(file);
        Assert.Equal("pwsh", settings.EffectiveWindowsShell(true));
        Assert.Equal("cmd", settings.EffectiveWindowsShell(false));
        foreach (var shell in new[] { "cmd", "powershell", "pwsh" })
        {
            settings.Save("system", 2, "", shell);
            settings.ApplyProxy("http://localhost:8080");
            settings = new AppSettings(file);
            Assert.Equal(shell, settings.EffectiveWindowsShell(true));
            Assert.Equal(shell == "pwsh" ? "cmd" : shell, settings.EffectiveWindowsShell(false));
        }
    }

    [Theory]
    [InlineData(true, true, false, true, "已是最新版", false)]
    [InlineData(true, false, false, true, "升级", true)]
    [InlineData(false, false, false, true, "安装", true)]
    [InlineData(true, true, true, true, "升级", true)]
    [InlineData(true, false, false, false, "升级", false)]
    [InlineData(true, true, false, false, "已是最新版", false)]
    public async Task RuntimeActionsReflectVersionProbeAndPlatform(bool installed, bool latest, bool probeFails,
        bool supported, string action, bool canInstall)
    {
        var paths = Paths();
        var catalog = RuntimeCatalog.Load();
        if (installed)
        {
            Directory.CreateDirectory(paths.Root);
            File.WriteAllText(Path.Combine(paths.Root, "python3"), "fake binary");
            File.WriteAllText(Path.Combine(paths.Root, "vspipe"), "fake binary");
        }
        var output = latest ? $"Python {catalog.PythonVersion}\nVapourSynth {catalog.VapourSynthVersion}" : "Python 3.12.0\nVapourSynth 78";
        var runner = new FakeRunner(probeFails, output);
        using var client = new DistributionClient(new ResponseHandler([]));
        var host = supported ? Mac : Mac with { Version = new(14, 0) };
        var model = new ToolLibraryViewModel(new(paths, host, client, runner), runner);

        await model.RefreshAsync(false);

        foreach (var row in model.Tools.Where(t => t.Id is "python" or "vapoursynth"))
        {
            Assert.Equal(action, row.Action);
            Assert.Equal(canInstall, row.CanInstall);
            Assert.Equal(canInstall, row.CanAct);
            if (latest) Assert.Equal(row.Target, row.Current);
            if (probeFails) Assert.Contains("版本检测失败", row.State);
            else if (!supported) Assert.Equal(catalog.Unavailable(host, "runtime"), row.State);
        }

        // Switching libraries must clear a previously detected latest-version state.
        model.LibraryPath = Path.Combine(_root, "empty-library");
        await model.ApplyPathAsync();
        foreach (var row in model.Tools.Where(t => t.Id is "python" or "vapoursynth"))
        {
            Assert.Equal("未安装", row.Current);
            Assert.Equal("安装", row.Action);
            Assert.Equal(supported, row.CanAct);
        }
    }

    [Fact]
    public async Task RuntimeProbeExceptionsKeepDiagnosticAndDisableInstallation()
    {
        var paths = Paths();
        LibraryJson.WriteAtomic(Path.Combine(paths.Root, "library.json"), new LibraryManifest(1, new()
        {
            ["vapoursynth"] = new("79", "missing-runtime", "missing-runtime/vspipe", "test", [])
        }));
        var runner = new FakeRunner(false);
        using var client = new DistributionClient(new ResponseHandler([]));
        var model = new ToolLibraryViewModel(new(paths, Mac, client, runner), runner);

        await model.RefreshAsync(false);

        foreach (var row in model.Tools.Where(t => t.Id is "python" or "vapoursynth"))
        {
            Assert.Equal("检测失败", row.Current);
            Assert.Contains("组件文件缺失", row.State);
            Assert.False(row.CanAct);
        }
        Assert.Contains("组件文件缺失", model.Log);
    }

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
            Assert.Empty(Directory.EnumerateDirectories(Path.Combine(paths.Root, ".managed", "vtenc")));
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
        Assert.Empty(Directory.EnumerateDirectories(Path.Combine(paths.Root, ".managed", "ffmpeg")));
        Assert.Empty(Directory.EnumerateFiles(paths.Root, "*.partial", SearchOption.AllDirectories));
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
    public void ToolLibraryOpensSharedSettingsAndKeepsLegacyProxy()
    {
        var settingsFile = Path.Combine(_root, "network.json");
        File.WriteAllText(settingsFile, """{"proxyAddress":"http://localhost:7890"}""");
        var settings = new AppSettings(settingsFile);
        var response = """{"draft":false,"prerelease":false,"tag_name":"v999.0.0"}"""u8.ToArray();
        var client = new DistributionClient(new ResponseHandler(response), settings);
        var runner = new FakeRunner(false);
        var model = new ToolLibraryViewModel(new(Paths(), Mac, client, runner), runner);
        var owner = new MainWindow(false, distributionClient: client);
        owner.Show();
        var library = new ToolLibraryWindow(model, false);
        library.Show(owner);
        try
        {
            Assert.Null(library.FindControl<TextBox>("ProxyAddressField"));
            library.FindControl<Button>("OpenSettingsButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var window = Assert.IsType<SettingsWindow>(Assert.Single(library.OwnedWindows));
            Dispatcher.UIThread.RunJobs();
            var field = window.FindControl<TextBox>("ProxyAddressField")!;
            var limit = window.FindControl<TextBox>("ConcurrencyField")!;
            var language = window.FindControl<ComboBox>("LanguageField")!;
            var save = window.FindControl<Button>("SaveButton")!;
            window.Width = window.MinWidth;
            window.Height = window.MinHeight;
            window.UpdateLayout();
            Assert.Empty(save.GetVisualAncestors().OfType<ScrollViewer>());
            var saveOrigin = save.TranslatePoint(default, window)!.Value;
            Assert.True(save.Bounds.Height > 0 && saveOrigin.Y >= 0 &&
                saveOrigin.Y + save.Bounds.Height <= window.ClientSize.Height);
            var notice = owner.FindControl<MenuItem>("NewRelease")!;
            var shellField = window.FindControl<ComboBox>("ShellField")!;
            Assert.Equal(OperatingSystem.IsWindows(), shellField.IsEffectivelyVisible);
            if (OperatingSystem.IsWindows())
            {
                var available = ToolkitTerminal.FindPwsh() is not null;
                Assert.Equal(available, window.FindControl<ComboBoxItem>("PwshOption")!.IsEnabled);
                Assert.Equal(available ? "pwsh" : "cmd", ((ComboBoxItem)shellField.SelectedItem!).Tag);
                shellField.SelectedIndex = 2;
            }
            Assert.Equal("http://localhost:7890", field.Text);
            Assert.Equal("3", limit.Text);
            Assert.Equal("system", ((ComboBoxItem)language.SelectedItem!).Tag);
            Assert.False(notice.IsVisible);

            limit.Text = "0";
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(limit.IsFocused);
            limit.Text = "5";
            field.Text = "invalid";
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(field.IsFocused);
            Assert.Contains("HTTP", Avalonia.Automation.AutomationProperties.GetHelpText(field));
            Assert.Equal("http://localhost:7890", settings.ProxyAddress);
            Assert.Equal(3, owner.Model.MaxConcurrentEncodings);

            field.Text = "socks5://localhost:1080/";
            language.SelectedIndex = 3;
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var saved = new AppSettings(settingsFile);
            Assert.Equal("socks5://localhost:1080", saved.ProxyAddress);
            Assert.Equal("ja-JP", saved.Language);
            Assert.Equal(OperatingSystem.IsWindows() ? "powershell" : null, saved.WindowsShell);
            Assert.Equal(5, saved.MaxConcurrentEncodings);
            Assert.Equal(5, owner.Model.MaxConcurrentEncodings);

            Assert.Empty(library.OwnedWindows);
            Dispatcher.UIThread.RunJobs();
            library.FindControl<Button>("OpenSettingsButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window = Assert.IsType<SettingsWindow>(Assert.Single(library.OwnedWindows));
            Assert.Equal("5", window.FindControl<TextBox>("ConcurrencyField")!.Text);
            Assert.Equal("socks5://localhost:1080", window.FindControl<TextBox>("ProxyAddressField")!.Text);
            Assert.Equal("ja-JP", ((ComboBoxItem)window.FindControl<ComboBox>("LanguageField")!.SelectedItem!).Tag);

            window.FindControl<Button>("CheckButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.True(notice.IsVisible);
            Assert.Equal("发现新版本 v999.0.0", notice.Header);
            var nativeHelp = NativeMenu.GetMenu(owner)?.Items.OfType<NativeMenuItem>().Single(i => i.Header == "帮助").Menu;
            if (nativeHelp is not null)
                Assert.Contains(nativeHelp.Items.OfType<NativeMenuItem>(), item => item.IsVisible && Equals(item.Header, notice.Header));
            window.Close();
            Assert.Empty(library.OwnedWindows);
            library.Close();
            owner.FindControl<MenuItem>("CheckUpdates")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal(notice.Header, owner.Model.Status);
        }
        finally
        {
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = null;
            owner.Close();
        }
    }

    [AvaloniaFact]
    public void ClosingSettingsSavesEditsAndCancelOrEscapeDiscardsThem()
    {
        var path = Path.Combine(_root, "network.json");
        using var client = new DistributionClient(settings: new AppSettings(path));
        var updates = new AppUpdateViewModel(client);
        SettingsWindow Open()
        {
            var window = new SettingsWindow(new AppSettings(path), updates, default);
            window.Show();
            return window;
        }

        var first = Open();
        first.FindControl<TextBox>("ConcurrencyField")!.Text = "7";
        first.Close();

        var second = Open();
        Assert.Equal("7", second.FindControl<TextBox>("ConcurrencyField")!.Text);
        second.FindControl<TextBox>("ConcurrencyField")!.Text = "0";
        second.Close();
        Assert.True(second.IsVisible);
        Assert.True(second.FindControl<TextBlock>("SaveStatus")!.IsEffectivelyVisible);
        second.FindControl<Button>("CancelButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.False(second.IsVisible);

        var third = Open();
        Assert.Equal("7", third.FindControl<TextBox>("ConcurrencyField")!.Text);
        third.FindControl<TextBox>("ConcurrencyField")!.Text = "9";
        third.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.False(third.IsVisible);
        Assert.Equal(7, new AppSettings(path).MaxConcurrentEncodings);
    }

    private string Generation(LibraryPaths paths, string tool, string contents = "old binary")
    {
        var directory = Path.Combine(paths.Root, ".managed", tool, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, tool), contents);
        return directory;
    }

    private static void Activate(LibraryPaths paths, string directory, string? executable = null)
    {
        LibraryJson.WriteAtomic(Path.Combine(paths.Root, "library.json"), new LibraryManifest(1, new()
        {
            ["ffmpeg"] = new("1.0", Path.GetRelativePath(paths.Root, directory),
                Path.GetRelativePath(paths.Root, executable ?? Path.Combine(directory, "ffmpeg")), "test", [])
        }));
    }

    [Fact]
    public async Task SuccessfulInstallCleansOldAndAbandonedGenerationsButKeepsOtherActiveTools()
    {
        var paths = Paths();
        var old = Generation(paths, "ffmpeg");
        var abandoned = Generation(paths, "vtenc");
        var activeVtenc = Generation(paths, "vtenc");
        Activate(paths, old);
        var manifest = LibraryPaths.ReadManifest(paths.Root);
        manifest.Tools["vtenc"] = new("1.0", Path.GetRelativePath(paths.Root, activeVtenc),
            Path.GetRelativePath(paths.Root, Path.Combine(activeVtenc, "vtenc")), "test", []);
        LibraryJson.WriteAtomic(Path.Combine(paths.Root, "library.json"), manifest);
        var manual = Path.Combine(paths.Root, ".managed", "ffmpeg", "manual");
        Directory.CreateDirectory(manual);
        File.WriteAllText(Path.Combine(manual, "custom"), "keep");
        var bytes = ZipBytes();
        using var client = new DistributionClient(new ResponseHandler(bytes));
        var service = new ToolLibraryService(paths, Mac, client, new FakeRunner(false));
        var release = new ToolRelease("1.2.3", "test", [new("https://github.com/test/test/tool.zip", Convert.ToHexString(SHA256.HashData(bytes)), "tool.zip")]);
        await service.InstallToolAsync(ToolDefinition.All[0], release, _ => { }, default);
        Assert.False(Directory.Exists(old));
        Assert.False(Directory.Exists(abandoned));
        Assert.True(File.Exists(Path.Combine(activeVtenc, "vtenc")));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(manual, "custom")));
        Assert.True(File.Exists(service.FindExecutable(ToolDefinition.All[0])));
        Assert.Single(Directory.EnumerateFiles(Path.Combine(paths.Root, ".downloads"), "*.zip", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RuntimeFailureOrCancellationOnlyRemovesCurrentPreparation(bool cancel)
    {
        var paths = Paths();
        var active = Generation(paths, "ffmpeg");
        Activate(paths, active);
        var abandoned = Generation(paths, "vapoursynth");
        var before = File.ReadAllText(Path.Combine(paths.Root, "library.json"));
        using var client = new DistributionClient(new ResponseHandler([1, 2, 3]));
        var service = new ToolLibraryService(paths, Mac, client, new FakeRunner(false));
        using var cancellation = new CancellationTokenSource();
        if (cancel) cancellation.Cancel();
        Task Install() => service.InstallRuntimeAsync([], _ => { }, cancellation.Token);
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(Install);
        else await Assert.ThrowsAsync<InvalidDataException>(Install);
        Assert.Equal(before, File.ReadAllText(Path.Combine(paths.Root, "library.json")));
        Assert.Equal(new[] { abandoned }, Directory.EnumerateDirectories(Path.Combine(paths.Root, ".managed", "vapoursynth")));
        Assert.Empty(Directory.EnumerateFiles(paths.Root, "*.partial", SearchOption.AllDirectories));
        Assert.Equal("old binary", File.ReadAllText(Path.Combine(active, "ffmpeg")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureAfterExtractionKeepsOldGenerationAndRemovesNewOne(bool cancel)
    {
        var paths = Paths();
        var old = Generation(paths, "ffmpeg");
        Activate(paths, old);
        var before = File.ReadAllText(Path.Combine(paths.Root, "library.json"));
        var bytes = ZipBytes();
        using var client = new DistributionClient(new ResponseHandler(bytes));
        using var cancellation = new CancellationTokenSource();
        var runner = new CallbackRunner(() =>
        {
            Assert.Equal(2, Directory.EnumerateDirectories(Path.Combine(paths.Root, ".managed", "ffmpeg")).Count());
            if (cancel) cancellation.Cancel();
            return new(cancel ? 0 : 1, "ffmpeg version 1.2.3 test", "original failure");
        });
        var service = new ToolLibraryService(paths, Mac, client, runner);
        var release = new ToolRelease("1.2.3", "test", [new("https://github.com/test/test/tool.zip", Convert.ToHexString(SHA256.HashData(bytes)), "tool.zip")]);
        Task Install() => service.InstallToolAsync(ToolDefinition.All[0], release, _ => { }, cancellation.Token);
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(Install);
        else Assert.Contains("original failure", (await Assert.ThrowsAsync<InvalidOperationException>(Install)).Message);
        Assert.Equal(before, File.ReadAllText(Path.Combine(paths.Root, "library.json")));
        Assert.Equal(new[] { old }, Directory.EnumerateDirectories(Path.Combine(paths.Root, ".managed", "ffmpeg")));
        Assert.Equal("old binary", File.ReadAllText(Path.Combine(old, "ffmpeg")));
    }

    [Fact]
    public void CleanupProtectsBothManifestPathsAndIsIdempotent()
    {
        var paths = Paths();
        var directory = Generation(paths, "ffmpeg");
        var executableDirectory = Generation(paths, "ffmpeg");
        var unused = Generation(paths, "ffmpeg");
        Activate(paths, directory, Path.Combine(executableDirectory, "ffmpeg"));
        var before = File.ReadAllText(Path.Combine(paths.Root, "library.json"));
        using var client = new DistributionClient(new ResponseHandler([]));
        var service = new ToolLibraryService(paths, Mac, client, new FakeRunner(false));
        service.CleanupUnusedGenerations(_ => { });
        service.CleanupUnusedGenerations(_ => { });
        Assert.True(Directory.Exists(directory));
        Assert.True(Directory.Exists(executableDirectory));
        Assert.False(Directory.Exists(unused));
        Assert.Equal(before, File.ReadAllText(Path.Combine(paths.Root, "library.json")));
    }

    [Fact]
    public void CleanupPreservesLinkedDependenciesAndNeverFollowsDiscardedLinks()
    {
        if (OperatingSystem.IsWindows()) return; // Creating symlinks requires developer mode/admin on Windows.
        var paths = Paths();
        var active = Generation(paths, "ffmpeg");
        var dependency = Generation(paths, "vtenc");
        var unused = Generation(paths, "vtenc");
        Activate(paths, active);
        File.CreateSymbolicLink(Path.Combine(active, "dependency"), Path.Combine(dependency, "vtenc"));
        var outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "custom"), "keep");
        Directory.CreateSymbolicLink(Path.Combine(unused, "outside"), outside);
        using var client = new DistributionClient(new ResponseHandler([]));
        var service = new ToolLibraryService(paths, Mac, client, new FakeRunner(false));
        service.CleanupUnusedGenerations(_ => { });
        Assert.True(Directory.Exists(dependency));
        Assert.False(Directory.Exists(unused));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(outside, "custom")));
    }

    [Fact]
    public async Task CleanupFailureAfterCommitKeepsNewVersionAndReportsActivation()
    {
        if (OperatingSystem.IsWindows()) return;
        var paths = Paths();
        var old = Generation(paths, "ffmpeg");
        Activate(paths, old);
        Directory.CreateSymbolicLink(Path.Combine(paths.Root, ".managed", "vtenc"), old);
        var bytes = ZipBytes();
        using var client = new DistributionClient(new ResponseHandler(bytes));
        var service = new ToolLibraryService(paths, Mac, client, new FakeRunner(false));
        var release = new ToolRelease("1.2.3", "test", [new("https://github.com/test/test/tool.zip", Convert.ToHexString(SHA256.HashData(bytes)), "tool.zip")]);
        var error = await Assert.ThrowsAsync<IOException>(() => service.InstallToolAsync(ToolDefinition.All[0], release, _ => { }, default));
        Assert.Contains("新版本已启用", error.Message);
        Assert.Equal("1.2.3", LibraryPaths.ReadManifest(paths.Root).Tools["ffmpeg"].Version);
        Assert.True(File.Exists(service.FindExecutable(ToolDefinition.All[0])));
        Assert.True(Directory.Exists(old));
    }

    [Fact]
    public void InvalidManifestStopsCleanupBeforeDeletingAnything()
    {
        var paths = Paths();
        var old = Generation(paths, "ffmpeg");
        File.WriteAllText(Path.Combine(paths.Root, "library.json"), "broken json");
        using var client = new DistributionClient(new ResponseHandler([]));
        var service = new ToolLibraryService(paths, Mac, client, new FakeRunner(false));
        Assert.Throws<JsonException>(() => service.CleanupUnusedGenerations(_ => { }));
        Assert.True(Directory.Exists(old));
        Activate(paths, old, Path.Combine(old, "missing"));
        Assert.Throws<FileNotFoundException>(() => service.CleanupUnusedGenerations(_ => { }));
        Assert.True(Directory.Exists(old));
    }

    [Fact]
    public async Task UsageLeaseBlocksInstallationAndCleanupAcrossServiceInstances()
    {
        var paths = Paths();
        var old = Generation(paths, "ffmpeg");
        Activate(paths, old);
        var unused = Generation(paths, "ffmpeg");
        using var client = new DistributionClient(new ResponseHandler([]));
        var first = new ToolLibraryService(paths, Mac, client, new FakeRunner(false));
        var second = new ToolLibraryService(Paths(), Mac, client, new FakeRunner(false));
        using (first.AcquireUsageLease())
        {
            using var shared = second.AcquireUsageLease();
            Assert.Throws<IOException>(() => second.CleanupUnusedGenerations(_ => { }));
            await Assert.ThrowsAsync<IOException>(() => second.InstallToolAsync(ToolDefinition.All[0], new("1.2.3", "test", []), _ => { }, default));
            Assert.True(Directory.Exists(unused));
        }
        second.CleanupUnusedGenerations(_ => { });
        Assert.False(Directory.Exists(unused));
        Assert.True(Directory.Exists(old));
    }

    [Theory]
    [InlineData("success")]
    [InlineData("failure")]
    [InlineData("cancel")]
    public async Task RuntimeActivationCleansOldOnlyAfterFinalValidation(string outcome)
    {
        var paths = Paths();
        var old = Generation(paths, "vapoursynth");
        File.WriteAllText(Path.Combine(old, "runtime_probe.py"), "probe");
        File.WriteAllText(Path.Combine(old, "custom.py"), "user script");
        LibraryJson.WriteAtomic(Path.Combine(old, "managed-files.json"), new[] { "runtime_probe.py", "vapoursynth" });
        LibraryJson.WriteAtomic(Path.Combine(paths.Root, "library.json"), new LibraryManifest(1, new()
        {
            ["vapoursynth"] = new("78", Path.GetRelativePath(paths.Root, old), Path.GetRelativePath(paths.Root, Path.Combine(old, "vapoursynth")), "test", [])
        }));
        var before = File.ReadAllText(Path.Combine(paths.Root, "library.json"));
        using var client = new DistributionClient(new ResponseHandler([]));
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var runner = new CallbackRunner(() =>
        {
            calls++;
            Assert.True(Directory.Exists(old));
            if (calls == 5)
            {
                if (outcome == "failure") return new(1, "", "frame validation failed");
                if (outcome == "cancel") cancellation.Cancel();
            }
            return new(0, "NE_PROBE:{\"available\":{\"runtime\":\"test\"},\"errors\":{}}", "");
        });
        var service = new ToolLibraryService(paths, Mac, client, runner);
        // Use synthetic, hash-verified archives; no real downloads or interpreters.
        void Cache(CatalogAsset[] assets, byte[] bytes, string fileName)
        {
            var index = Array.FindIndex(assets, a => a.Platform == Mac.Rid);
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            assets[index] = assets[index] with { Sha256 = hash, FileName = fileName };
            var folder = Path.Combine(paths.Root, ".downloads", hash);
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, fileName), bytes);
        }
        Cache(service.Catalog.PythonAssets, ZipBytes("python/bin/python3.13"), "python.zip");
        Cache(service.Catalog.Packages.Single(p => p.Group == "runtime").Assets, ZipBytes("vapoursynth/vspipe"), "vs.whl");
        Task Install() => service.InstallRuntimeAsync([], _ => { }, cancellation.Token);
        if (outcome == "failure") await Assert.ThrowsAsync<InvalidOperationException>(Install);
        else if (outcome == "cancel") await Assert.ThrowsAnyAsync<OperationCanceledException>(Install);
        else await Install();
        Assert.Equal(5, calls);
        var remaining = Assert.Single(Directory.EnumerateDirectories(Path.Combine(paths.Root, ".managed", "vapoursynth")));
        if (outcome == "success")
        {
            Assert.False(Directory.Exists(old));
            Assert.Equal(service.ActiveRuntime(), remaining);
            Assert.Equal("user script", File.ReadAllText(Path.Combine(remaining, "custom.py")));
            Assert.True(File.Exists(service.VspipePath(remaining)));
        }
        else
        {
            Assert.Equal(old, remaining);
            Assert.Equal(before, File.ReadAllText(Path.Combine(paths.Root, "library.json")));
            Assert.Equal("user script", File.ReadAllText(Path.Combine(old, "custom.py")));
        }
    }

    [Fact]
    public async Task CleanupFailurePreservesOriginalInstallationError()
    {
        var paths = Paths();
        var bytes = ZipBytes();
        using var client = new DistributionClient(new ResponseHandler(bytes));
        var service = new ToolLibraryService(paths, Mac, client, new CallbackRunner(() =>
        {
            File.WriteAllText(Path.Combine(paths.Root, "library.json"), "broken manifest");
            return new(1, "", "original process failure");
        }));
        var release = new ToolRelease("1.2.3", "test", [new("https://github.com/test/test/tool.zip", Convert.ToHexString(SHA256.HashData(bytes)), "tool.zip")]);
        var error = await Assert.ThrowsAsync<AggregateException>(() => service.InstallToolAsync(ToolDefinition.All[0], release, _ => { }, default));
        Assert.Equal(2, error.InnerExceptions.Count);
        Assert.Contains("original process failure", error.InnerExceptions[0].Message);
        Assert.IsType<JsonException>(error.InnerExceptions[1]);
        Assert.Contains("无法清理本次准备目录", error.Message);
    }

    [Fact]
    public void FailedAtomicWriteRemovesTemporaryManifest()
    {
        var destination = Path.Combine(_root, "library.json");
        Directory.CreateDirectory(destination); // Force the atomic rename to fail.
        var error = Record.Exception(() => LibraryJson.WriteAtomic(destination, LibraryManifest.Empty));
        Assert.True(error is IOException or UnauthorizedAccessException, $"Unexpected error: {error}");
        Assert.True(Directory.Exists(destination));
        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp"));
    }

    private sealed class CallbackRunner(Func<ProcessResult> callback) : ProcessRunner
    {
        public override Task<ProcessResult> RunAsync(string executable, IEnumerable<string> arguments, CancellationToken cancellationToken,
            IDictionary<string, string>? environment = null, int timeoutSeconds = 45) => Task.FromResult(callback());
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
    private sealed class FakeRunner(bool fail, string output = "ffmpeg version 1.2.3 test") : ProcessRunner
    {
        public override Task<ProcessResult> RunAsync(string executable, IEnumerable<string> arguments, CancellationToken cancellationToken,
            IDictionary<string, string>? environment = null, int timeoutSeconds = 45) =>
            Task.FromResult(new ProcessResult(fail ? 1 : 0, output, fail ? "missing dynamic library" : ""));
    }
}
