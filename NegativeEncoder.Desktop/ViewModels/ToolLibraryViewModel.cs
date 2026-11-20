using System.Collections.ObjectModel;
using NegativeEncoder.Desktop.ToolLibrary;

namespace NegativeEncoder.Desktop.ViewModels;

public sealed class ToolRowViewModel(string id, string name, string description) : ObservableObject
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string Description { get; } = description;
    private string _current = "检测中";
    public string Current { get => _current; set => Set(ref _current, value); }
    private string _target = "未检查";
    public string Target { get => _target; set => Set(ref _target, value); }
    private string _state = "";
    public string State { get => _state; set => Set(ref _state, value); }
    private string _source = "";
    public string Source { get => _source; set { if (Set(ref _source, value)) Raise(nameof(HasSource)); } }
    public bool HasSource => Source.Length > 0;
    private string _action = "安装";
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
    private string _hardware = "尚未检测硬件";
    public string Hardware { get => _hardware; private set => Set(ref _hardware, value); }
    private string _status = "就绪";
    public string Status { get => _status; private set => Set(ref _status, value); }
    private string _log = "";
    public string Log { get => _log; private set => Set(ref _log, value); }
    private HardwareReport _report = new("", [], "尚未检测");
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
        Status = _hasErrors ? "检测完成，部分组件检查失败；请查看对应状态和操作日志。" : "检测完成。";
    });

    public async Task ApplyPathAsync() => await RunAsync(async token =>
    {
        Service.Paths.Apply(LibraryPath);
        LibraryPath = Service.Paths.Root;
        // Clear capabilities before probing: a failed new environment cannot leave old capabilities enabled.
        Capabilities = VsCapabilities.Empty;
        CapabilitiesChanged?.Invoke(Capabilities);
        await RefreshLocalAsync(token);
        Status = Tools.All(t => t.Current == "未安装") ? "已切换位置。此目录尚未安装工具组件；原目录中的文件未移动或删除。" : "已切换位置并重新检测；原目录中的文件未移动或删除。";
        Append(Status);
    });

    private async Task RefreshLocalAsync(CancellationToken token)
    {
        foreach (var tool in ToolDefinition.All)
        {
            var row = Tools.Single(r => r.Id == tool.Id);
            var isRuntime = tool.Id is "python" or "vapoursynth";
            if (isRuntime) row.Target = tool.Id == "python" ? Service.Catalog.PythonVersion : Service.Catalog.VapourSynthVersion;
            try
            {
                var probe = await Service.ProbeAsync(tool, token);
                row.Current = probe?.Version ?? "未安装";
                row.Action = probe is null ? "安装" : "升级";
                row.Source = probe?.Executable ?? "";
                row.State = probe is { Runs: false } ? "组件存在，但版本检测失败；查看日志" :
                    (row.Release?.IsPrerelease == true ? "预发布版本；" : "") + _report.Hint(tool);
                if (probe is { Runs: false }) { _hasErrors = true; Append(tool.Name + "：" + probe.Diagnostic); }
                if (row.Release is not null) row.CanInstall = probe is null || !probe.Runs || probe.Version != row.Release.Version;
                if (isRuntime)
                {
                    var unavailable = Service.Catalog.Unavailable(Service.Host, "runtime");
                    var isLatest = probe is { Runs: true } && probe.Version == row.Target;
                    if (isLatest) row.Action = "已是最新版";
                    if (probe is not { Runs: false })
                        row.State = unavailable ?? (tool.Id == "python" ? "VapourSynth 的独立依赖，联动安装／升级" : "使用固定推荐环境；插件在 VS 插件页管理");
                    row.CanInstall = unavailable is null && !isLatest;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { _hasErrors = true; row.Current = "检测失败"; row.State = ex.Message; row.CanInstall = false; Append(ex.ToString()); }
            if (!isRuntime && ReleaseResolver.Unsupported(tool, Service.Host) is { } reason) { row.State = reason; row.CanInstall = false; }
        }
        try { Capabilities = await Service.ProbeVsAsync(token); }
        catch (OperationCanceledException) { Capabilities = VsCapabilities.Empty; CapabilitiesChanged?.Invoke(Capabilities); throw; }
        catch (Exception ex) { _hasErrors = true; Capabilities = VsCapabilities.Empty; Append(ex.ToString()); Status = "VapourSynth 检测失败，生成器已停止使用该环境。"; }
        CapabilitiesChanged?.Invoke(Capabilities);
        var manifest = LibraryPaths.ReadManifest(Service.Paths.Root);
        manifest.Tools.TryGetValue("vapoursynth", out var runtime);
        foreach (var group in Groups)
        {
            var features = ToolLibraryService.FeaturesForGroup(group.Id);
            var unavailable = Service.Catalog.Unavailable(Service.Host, group.Id);
            var installed = runtime?.Groups.Contains(group.Id) == true;
            group.Current = features.All(Capabilities.Has) ? "已安装 · 运行正常" : installed ? "已安装 · 检测失败" : "未安装";
            group.Target = Service.Catalog.Combination;
            group.Action = installed ? "升级" : "安装";
            group.State = unavailable ?? (Service.ActiveRuntime() is null ? "先安装 VapourSynth，或点击此处联动安装" : "固定推荐组合；安装后进行取帧检查");
            group.Source = string.Join("\n", Service.Catalog.Packages.Where(p => p.Group == group.Id).Select(p => p.Id + " " +
                (p.Assets.SingleOrDefault(a => a.Platform == Service.Host.Rid)?.Version ?? p.Version)));
            group.CanInstall = unavailable is null;
            if (installed)
                foreach (var feature in features.Where(f => Capabilities.Errors.ContainsKey(f))) Append(feature + "：" + Capabilities.Errors[feature]);
        }
    }

    private async Task RefreshReleasesAsync(CancellationToken token)
    {
        foreach (var definition in ToolDefinition.All.Where(t => t.Repository is not null))
        {
            var row = Tools.Single(r => r.Id == definition.Id);
            row.Release = null;
            row.CanInstall = false;
            if (ReleaseResolver.Unsupported(definition, Service.Host) is { } unsupported) { row.State = unsupported; row.Target = "无匹配发行包"; continue; }
            Status = "检查 " + definition.Name + (definition.Id == "ffmpeg" ? " 最新稳定版…" : " 最新版本（含预发布）…");
            try
            {
                var release = await Service.Releases.ResolveAsync(definition, token);
                row.Release = release;
                row.Target = release.Version;
                row.CanInstall = true;
                row.State = (release.IsPrerelease ? "预发布版本；" : "") + _report.Hint(definition);
                row.Source = (row.Source.Length == 0 ? "" : row.Source + "\n") + release.Source;
                if (row.Current == row.Target) { row.Action = "已是最新版"; row.CanInstall = false; }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (System.Net.Http.HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            { row.Target = "无可用发行包"; row.State = "上游尚未发布所需版本，或仓库资源不可访问。"; Append(ex.Message); }
            catch (Exception ex) { _hasErrors = true; row.Target = "检查失败"; row.State = ex.Message; Append(ex.ToString()); }
        }
    }

    public async Task InstallAsync(ToolRowViewModel row) => await RunAsync(async token =>
    {
        if (!row.CanInstall) throw new InvalidOperationException("此组件当前不可安装：" + row.State);
        if (row.Id is "python" or "vapoursynth" || Groups.Contains(row))
            await Service.InstallRuntimeAsync(Groups.Contains(row) ? [row.Id] : [], Report, token);
        else
            await Service.InstallToolAsync(ToolDefinition.All.Single(t => t.Id == row.Id), row.Release ?? throw new InvalidOperationException("请先检查最新版本。"), Report, token);
        await RefreshLocalAsync(token);
        Status = "安装完成，当前环境已重新检测。";
    });

    public async Task InstallRecommendedAsync() => await RunAsync(async token =>
    {
        var groups = Groups.Where(g => g.Id != "yadif" && g.CanInstall).Select(g => g.Id).ToArray();
        if (groups.Length == 0) throw new NotSupportedException("当前平台没有可安装的推荐组合。");
        await Service.InstallRuntimeAsync(groups, Report, token);
        await RefreshLocalAsync(token);
        Status = "推荐插件安装完成。";
    });

    public void Cancel() { _operation?.Cancel(); Status = "正在取消；等待当前操作安全退出…"; }
    private void Report(string message)
    {
        Status = message;
        if (!message.StartsWith("下载 ", StringComparison.Ordinal)) Append(message);
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
        catch (OperationCanceledException) when (operation.IsCancellationRequested) { Status = "操作已取消；未完成的安装未启用。"; Append(Status); }
        catch (Exception ex) { Status = "操作失败：" + ex.Message; Append(ex.ToString()); }
        finally { _operation = null; Busy = false; }
    }
}
