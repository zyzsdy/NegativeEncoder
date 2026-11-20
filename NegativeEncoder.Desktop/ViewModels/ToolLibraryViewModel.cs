using System.Collections.ObjectModel;
using Avalonia.Threading;
using NegativeEncoder.Desktop.ToolLibrary;

namespace NegativeEncoder.Desktop.ViewModels;

public sealed class ToolRowViewModel(string id, string name, string description) : ObservableObject
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string Description { get; } = description;
    private string _current = Loc.Text("ToolLibrary.Detecting");
    public string Current { get => _current; set => Set(ref _current, value); }
    private string _target = Loc.Text("ToolLibrary.Unchecked");
    public string Target { get => _target; set => Set(ref _target, value); }
    private string _state = "";
    public string State { get => _state; set => Set(ref _state, value); }
    private string _source = "";
    public string Source { get => _source; set { if (Set(ref _source, value)) Raise(nameof(HasSource)); } }
    public bool? Installed { get; set; }
    public bool HasSource => Source.Length > 0;
    private string _action = Loc.Text("ToolLibrary.Install");
    public string Action { get => _action; set => Set(ref _action, value); }
    private bool _canInstall;
    public bool CanInstall { get => _canInstall; set { if (Set(ref _canInstall, value)) Raise(nameof(CanAct)); } }
    private bool _busy;
    public bool Busy { get => _busy; set { if (Set(ref _busy, value)) Raise(nameof(CanAct)); } }
    public bool CanAct => CanInstall && !Busy;
    public ToolRelease? Release { get; set; }
}

public sealed class ToolLibraryViewModel : ObservableObject
{
    public ToolLibraryService Service { get; }
    private readonly ProcessRunner _runner;
    private CancellationTokenSource? _operation;
    public ObservableCollection<ToolRowViewModel> Tools { get; } = [];
    public ObservableCollection<ToolRowViewModel> Groups { get; } = [];
    public VsCapabilities Capabilities { get; private set; } = VsCapabilities.Empty;
    private string _path;
    public string LibraryPath { get => _path; set => Set(ref _path, value); }
    private bool _busy;
    public bool Busy
    {
        get => _busy;
        private set
        {
            if (!Set(ref _busy, value)) return;
            Raise(nameof(Idle));
            foreach (var row in Tools.Concat(Groups)) row.Busy = value;
        }
    }
    public bool Idle => !Busy;
    private string _hardware = Loc.Text("ToolLibrary.HardwareUnchecked");
    public string Hardware { get => _hardware; private set => Set(ref _hardware, value); }
    private string _status = Loc.Text("MainWindow.Ready");
    public string Status { get => _status; private set => Set(ref _status, value); }
    private string _log = "";
    public string Log { get => _log; private set => Set(ref _log, value); }
    private HardwareReport _report = new("", [], Loc.Text("ToolLibrary.NotDetectedYet"));
    private bool _hasErrors;
    public event Action<VsCapabilities>? CapabilitiesChanged;

    public ToolLibraryViewModel(ToolLibraryService service, ProcessRunner runner)
    {
        Service = service;
        _runner = runner;
        _path = service.Paths.Root;
        foreach (var definition in ToolDefinition.All) Tools.Add(new(definition.Id, definition.Name, ""));
        foreach (var group in RuntimeCatalog.Groups) Groups.Add(new(group.Id, group.Name, group.Description));
    }

    public async Task RefreshAsync(bool online) => await RunAsync(async token =>
    {
        _report = await new HardwareDetector(_runner).DetectAsync(token);
        Hardware = $"{Service.Host.Rid} · {_report.Description}";
        if (_report.Error is not null) Append(_report.Error);
        await RefreshLocalAsync(token);
        if (online) await RefreshReleasesAsync(token);
        Status = _hasErrors ? Loc.Text("ToolLibrary.PartialFailure") : Loc.Text("ToolLibrary.ScanComplete");
    });

    public async Task ApplyPathAsync() => await RunAsync(async token =>
    {
        Service.Paths.Apply(LibraryPath);
        LibraryPath = Service.Paths.Root;
        // Clear capabilities before probing: a failed new environment cannot leave old capabilities enabled.
        Capabilities = VsCapabilities.Empty;
        CapabilitiesChanged?.Invoke(Capabilities);
        await RefreshLocalAsync(token);
        Status = Tools.All(t => t.Installed == false) ? Loc.Text("ToolLibrary.SwitchedEmpty") : Loc.Text("ToolLibrary.Switched");
        Append(Status);
    });

    private async Task RefreshLocalAsync(CancellationToken token)
    {
        foreach (var tool in ToolDefinition.All)
        {
            var row = Tools.Single(r => r.Id == tool.Id);
            row.Installed = null;
            var isRuntime = tool.Id is "python" or "vapoursynth";
            if (isRuntime) row.Target = tool.Id == "python" ? Service.Catalog.PythonVersion : Service.Catalog.VapourSynthVersion;
            try
            {
                var probe = await Service.ProbeAsync(tool, token);
                row.Installed = probe is not null;
                row.Current = probe?.Version ?? Loc.Text("ToolLibrary.NotInstalled");
                row.Action = probe is null ? Loc.Text("ToolLibrary.Install") : Loc.Text("ToolLibrary.Upgrade");
                row.Source = probe?.Executable ?? "";
                row.State = probe is { Runs: false } ? Loc.Text("ToolLibrary.VersionProbeFailed") :
                    (row.Release?.IsPrerelease == true ? Loc.Text("ToolLibrary.Prerelease") : "") + _report.Hint(tool);
                if (probe is { Runs: false }) { _hasErrors = true; Append(Loc.Format("Common.Diagnostic", tool.Name, probe.Diagnostic)); }
                if (row.Release is not null) row.CanInstall = probe is null || !probe.Runs || probe.Version != row.Release.Version;
                if (isRuntime)
                {
                    var unavailable = Service.Catalog.Unavailable(Service.Host, "runtime");
                    var isLatest = probe is { Runs: true } && probe.Version == row.Target;
                    if (isLatest) row.Action = Loc.Text("ToolLibrary.Latest");
                    if (probe is not { Runs: false })
                        row.State = unavailable ?? (tool.Id == "python" ? Loc.Text("ToolLibrary.PythonRuntime") : Loc.Text("ToolLibrary.ManagePlugins"));
                    row.CanInstall = unavailable is null && !isLatest;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { _hasErrors = true; row.Current = Loc.Text("ToolLibrary.ProbeFailed"); row.State = ex.Message; row.CanInstall = false; Append(ex.ToString()); }
            if (!isRuntime && ReleaseResolver.Unsupported(tool, Service.Host) is { } reason) { row.State = reason; row.CanInstall = false; }
        }
        try { Capabilities = await Service.ProbeVsAsync(token); }
        catch (OperationCanceledException) { Capabilities = VsCapabilities.Empty; CapabilitiesChanged?.Invoke(Capabilities); throw; }
        catch (Exception ex) { _hasErrors = true; Capabilities = VsCapabilities.Empty; Append(ex.ToString()); Status = Loc.Text("ToolLibrary.VsProbeFailed"); }
        CapabilitiesChanged?.Invoke(Capabilities);
        var manifest = LibraryPaths.ReadManifest(Service.Paths.Root);
        manifest.Tools.TryGetValue("vapoursynth", out var runtime);
        foreach (var group in Groups)
        {
            var features = ToolLibraryService.FeaturesForGroup(group.Id);
            var unavailable = Service.Catalog.Unavailable(Service.Host, group.Id);
            var installed = runtime?.Groups.Contains(group.Id) == true;
            group.Current = features.All(Capabilities.Has) ? Loc.Text("ToolLibrary.InstalledWorking") : installed ? Loc.Text("ToolLibrary.InstalledBroken") : Loc.Text("ToolLibrary.NotInstalled");
            group.Target = Service.Catalog.Combination;
            group.Action = installed ? Loc.Text("ToolLibrary.Upgrade") : Loc.Text("ToolLibrary.Install");
            group.State = unavailable ?? (Service.ActiveRuntime() is null ? Loc.Text("ToolLibrary.InstallVsFirst") : Loc.Text("ToolLibrary.PinnedPlugins"));
            group.Source = string.Join("\n", Service.Catalog.Packages.Where(p => p.Group == group.Id).Select(p => p.Id + " " +
                (p.Assets.SingleOrDefault(a => a.Platform == Service.Host.Rid)?.Version ?? p.Version)));
            group.CanInstall = unavailable is null;
            if (installed)
                foreach (var feature in features.Where(f => Capabilities.Errors.ContainsKey(f))) Append(Loc.Format("Common.Diagnostic", feature, Capabilities.Errors[feature]));
        }
    }

    private async Task RefreshReleasesAsync(CancellationToken token)
    {
        foreach (var definition in ToolDefinition.All.Where(t => t.Repository is not null))
        {
            var row = Tools.Single(r => r.Id == definition.Id);
            row.Release = null;
            row.CanInstall = false;
            if (ReleaseResolver.Unsupported(definition, Service.Host) is { } unsupported) { row.State = unsupported; row.Target = Loc.Text("ToolLibrary.NoMatchingRelease"); continue; }
            Status = definition.Id == "ffmpeg" ? Loc.Format("ToolLibrary.CheckStable", definition.Name) : Loc.Format("ToolLibrary.CheckRelease", definition.Name);
            try
            {
                var release = await Service.Releases.ResolveAsync(definition, token);
                row.Release = release;
                row.Target = release.Version;
                row.CanInstall = true;
                row.State = (release.IsPrerelease ? Loc.Text("ToolLibrary.Prerelease") : "") + _report.Hint(definition);
                row.Source = (row.Source.Length == 0 ? "" : row.Source + "\n") + release.Source;
                if (row.Current == row.Target) { row.Action = Loc.Text("ToolLibrary.Latest"); row.CanInstall = false; }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (System.Net.Http.HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            { row.Target = Loc.Text("ToolLibrary.NoRelease"); row.State = Loc.Text("ToolLibrary.UnavailableRelease"); Append(ex.Message); }
            catch (Exception ex) { _hasErrors = true; row.Target = Loc.Text("ToolLibrary.CheckFailed"); row.State = ex.Message; Append(ex.ToString()); }
        }
    }

    public async Task InstallAsync(ToolRowViewModel row) => await RunAsync(async token =>
    {
        if (!row.CanInstall) throw new InvalidOperationException(Loc.Format("ToolLibrary.CannotInstall", row.State));
        if (row.Id is "python" or "vapoursynth" || Groups.Contains(row))
            await Service.InstallRuntimeAsync(Groups.Contains(row) ? [row.Id] : [], Report, token, message => Status = message);
        else
            await Service.InstallToolAsync(ToolDefinition.All.Single(t => t.Id == row.Id), row.Release ?? throw new InvalidOperationException(Loc.Text("ToolLibrary.CheckVersionFirst")), Report, token, message => Status = message);
        await RefreshLocalAsync(token);
        Status = Loc.Text("ToolLibrary.Installed");
    });

    public async Task InstallRecommendedAsync() => await RunAsync(async token =>
    {
        var groups = Groups.Where(g => g.Id != "yadif" && g.CanInstall).Select(g => g.Id).ToArray();
        if (groups.Length == 0) throw new NotSupportedException(Loc.Text("ToolLibrary.NoRecommendedSet"));
        await Service.InstallRuntimeAsync(groups, Report, token, message => Status = message);
        await RefreshLocalAsync(token);
        Status = Loc.Text("ToolLibrary.PluginsInstalled");
    });

    public async Task CleanupAsync() => await RunAsync(async token =>
    {
        Status = Loc.Text("ToolLibrary.Cleaning");
        var removed = await Task.Run(() => Service.CleanupUnusedGenerations(
            message => Dispatcher.UIThread.Invoke(() => Report(message)), token), token);
        Report(removed == 0 ? Loc.Text("ToolLibrary.NothingToClean") : Loc.Format("ToolLibrary.CleanupComplete", removed));
    });

    public void Cancel() { _operation?.Cancel(); Status = Loc.Text("ToolLibrary.Cancelling"); }
    private void Report(string message)
    {
        Status = message;
        Append(message);
    }
    private void Append(string message) => Log += $"[{DateTimeOffset.Now:HH:mm:ss}] {message}\n";
    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (Busy) return;
        Busy = true;
        _hasErrors = false;
        using var operation = new CancellationTokenSource();
        _operation = operation;
        try { await action(operation.Token); }
        catch (OperationCanceledException) when (operation.IsCancellationRequested) { Status = Loc.Text("ToolLibrary.Cancelled"); Append(Status); }
        catch (Exception ex) { Status = Loc.Format("ToolLibrary.OperationFailed", ex.Message); Append(ex.ToString()); }
        finally { _operation = null; Busy = false; }
    }
}
