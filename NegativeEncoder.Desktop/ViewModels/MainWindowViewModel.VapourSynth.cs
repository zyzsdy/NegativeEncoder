using System.Text;
using AvaloniaEdit.Document;
using NegativeEncoder.Desktop.ToolLibrary;

namespace NegativeEncoder.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    public TextDocument ScriptDocument { get; } = new();
    private bool _scriptEdited;
    public string Script { get => ScriptDocument.Text; set => ScriptDocument.Text = value; }

    private VsCapabilities _vsCapabilities = VsCapabilities.Empty;
    private string _vsIssue = Loc.Text("MainWindow.VsMissing");
    public string VsIssue { get => _vsIssue; private set => Set(ref _vsIssue, value); }
    public bool CanVsSource => _vsCapabilities.Has("source");
    public bool CanIvtc => _vsCapabilities.Has("ivtc") || UseIvtc;
    public bool CanQtgmc => _vsCapabilities.Has("qtgmc") || UseQtgmc;
    public bool CanYadif => _vsCapabilities.Has("yadif") || UseYadifNormal || UseYadifDouble;
    public bool CanBwdif => _vsCapabilities.Has("bwdif") || UseBwdifNormal || UseBwdifDouble;
    public bool CanSubtext => _vsCapabilities.Has("subtext");
    public bool CanXyVsf => _vsCapabilities.Has("xyvsf");
    public bool CanLegacyVsf => _vsCapabilities.Has("vsf");
    public bool CanVsfmod => _vsCapabilities.Has("vsfm");
    public string SubtitleRenderer { get => CurrentPreset.SubtitleRenderer; set => CurrentPreset.SubtitleRenderer = value; }
    public bool UseSubtext { get => SubtitleRenderer == "subtext"; set { if (value) SubtitleRenderer = "subtext"; } }
    public bool UseXyVsf { get => SubtitleRenderer == "xyvsf"; set { if (value) SubtitleRenderer = "xyvsf"; } }
    public bool UseLegacyVsf { get => SubtitleRenderer == "vsf"; set { if (value) SubtitleRenderer = "vsf"; } }

    public void ApplyVsCapabilities(VsCapabilities capabilities)
    {
        _vsCapabilities = capabilities;
        foreach (var property in new[] { nameof(CanVsSource), nameof(CanIvtc), nameof(CanQtgmc), nameof(CanYadif), nameof(CanBwdif), nameof(CanSubtext), nameof(CanXyVsf), nameof(CanLegacyVsf), nameof(CanVsfmod) }) Raise(property);
        // Refresh availability without overwriting a hand-edited script.
        VsIssue = UnavailableVsReason();
    }

    public void SetVsDetectionError(string message)
    {
        ApplyVsCapabilities(VsCapabilities.Empty);
        VsIssue = message;
    }

    public bool UseVsfmod { get => SubtitleRenderer == "vsfm"; set { if (value) SubtitleRenderer = "vsfm"; } }
    private string _subtitle = "";
    public string Subtitle
    {
        get => _subtitle;
        set { if (Set(ref _subtitle, value)) GenerateVsScript(); }
    }
    public bool UseRepeat { get => CurrentPreset.UseRepeat; set => CurrentPreset.UseRepeat = value; }
    public bool UseQtgmc { get => CurrentPreset.UseQtgmc; set => CurrentPreset.UseQtgmc = value; }
    public bool UseIvtc { get => CurrentPreset.UseIvtc; set => CurrentPreset.UseIvtc = value; }
    public bool UseYadifDouble { get => CurrentPreset.UseYadifDouble; set => CurrentPreset.UseYadifDouble = value; }
    public bool UseYadifNormal { get => CurrentPreset.UseYadifNormal; set => CurrentPreset.UseYadifNormal = value; }
    public bool UseBwdifNormal { get => CurrentPreset.UseBwdifNormal; set => CurrentPreset.UseBwdifNormal = value; }
    public bool UseBwdifDouble { get => CurrentPreset.UseBwdifDouble; set => CurrentPreset.UseBwdifDouble = value; }
    public bool VsResize { get => CurrentPreset.VsResize; set => CurrentPreset.VsResize = value; }
    public string VsWidth { get => CurrentPreset.VsWidth; set => CurrentPreset.VsWidth = value; }
    public string VsHeight { get => CurrentPreset.VsHeight; set => CurrentPreset.VsHeight = value; }

    private static readonly string[] VsOptions =
    [
        nameof(SubtitleRenderer), nameof(UseRepeat), nameof(UseQtgmc), nameof(UseIvtc),
        nameof(UseYadifDouble), nameof(UseYadifNormal), nameof(UseBwdifNormal), nameof(UseBwdifDouble),
        nameof(VsResize), nameof(VsWidth), nameof(VsHeight)
    ];

    private void RefreshVsControls()
    {
        foreach (var name in new[] { nameof(UseSubtext), nameof(UseXyVsf), nameof(UseLegacyVsf), nameof(UseVsfmod),
                     nameof(CanIvtc), nameof(CanQtgmc), nameof(CanYadif), nameof(CanBwdif) }) Raise(name);
    }

    private string UnavailableVsReason()
    {
        var required = new List<(string Feature, string Label)> { ("runtime", "VapourSynth"), ("source", "L-SMASH-Works") };
        if (UseIvtc) required.Add(("ivtc", "VIVTC"));
        if (UseQtgmc) required.Add(("qtgmc", "QTGMC"));
        if (UseYadifNormal || UseYadifDouble) required.Add(("yadif", "Yadifmod / NNEDI3CL"));
        if (UseBwdifNormal || UseBwdifDouble) required.Add(("bwdif", "Bwdif"));
        if (!string.IsNullOrEmpty(Subtitle)) required.Add((SubtitleRenderer, SubtitleRenderer));
        var missing = required.Where(r => !_vsCapabilities.Has(r.Feature)).Select(r => r.Label).ToArray();
        if (missing.Length > 0) return Loc.Format("MainWindow.VsMissingFeatures", string.Join(Loc.Text("Common.ListSeparator"), missing));
        if (new[] { UseQtgmc, UseYadifNormal, UseYadifDouble, UseBwdifNormal, UseBwdifDouble }.Count(v => v) > 1)
            return Loc.Text("MainWindow.ConflictingFilters");
        if (VsResize && (!int.TryParse(VsWidth, out var width) || !int.TryParse(VsHeight, out var height) || width <= 0 || height <= 0 || width % 2 != 0 || height % 2 != 0))
            return Loc.Text("MainWindow.InvalidDimensions");
        return "";
    }

    public void GenerateVsScript()
    {
        VsIssue = UnavailableVsReason();
        if (VsIssue.Length > 0) return;
        var script = new StringBuilder("import os\nimport sys\nfrom pathlib import Path\n");
        script.Append($"VS_SITE = {PythonString(_vsCapabilities.SitePackages)}\n");
        script.Append("os.environ.pop('VAPOURSYNTH_EXTRA_PLUGIN_PATH', None)\nsys.path.insert(0, VS_SITE)\n");
        script.Append($"sys.path.insert(0, {PythonString(_vsCapabilities.UserScripts)})\n");
        script.Append("import vapoursynth as vs\nif not Path(vs.__file__).resolve().is_relative_to(Path(VS_SITE).resolve()):\n    raise RuntimeError(" + PythonString(Loc.Text("MainWindow.ForeignRuntime")) + ")\nfrom vapoursynth import core, YUV420P8\n");
        script.Append($"for plugin in sorted(Path({PythonString(_vsCapabilities.UserPlugins)}).rglob('*')):\n    if plugin.suffix.lower() in ('.dll', '.dylib', '.so'):\n        core.std.LoadPlugin(str(plugin))\n");
        if (UseQtgmc) script.Append("from havsfunc import QTGMC\n");
        script.Append($"\nVIDEO_PATH = {PythonString(InputPath)}\n");
        if (!string.IsNullOrEmpty(Subtitle)) script.Append($"SUB_PATH = {PythonString(Subtitle)}\n");
        script.Append("\nvideo = core.lsmas.LWLibavSource(VIDEO_PATH");
        script.Append(UseRepeat ? ", repeat=1)\n" : ", repeat=0)\n");

        if (UseIvtc)
        {
            script.Append("video = core.vivtc.VFM(video, 1, mode=3, cthresh=8, mi=64)\n");
            script.Append("video = core.vivtc.VDecimate(video)\n");
        }
        if (UseQtgmc) script.Append("video = QTGMC(video, Preset='fast', TFF=True)\n");
        if (UseYadifDouble)
            script.Append("video = core.yadifmod.Yadifmod(video, core.nnedi3cl.NNEDI3CL(video,field=3), order=1, field=-1, mode=1)\n");
        if (UseYadifNormal)
            script.Append("video = core.yadifmod.Yadifmod(video, core.nnedi3cl.NNEDI3CL(video,field=1), order=1, field=-1, mode=0)\n");
        if (UseBwdifNormal) script.Append("video = core.bwdif.Bwdif(video, field=1)\n");
        if (UseBwdifDouble) script.Append("video = core.bwdif.Bwdif(video, field=3)\n");

        script.Append("video = core.resize.Bicubic(video, format=YUV420P8)\n");
        if (VsResize) script.Append($"video = core.resize.Lanczos(video, {VsWidth}, {VsHeight})\n");
        if (!string.IsNullOrEmpty(Subtitle))
        {
            script.Append(SubtitleRenderer switch
            {
                "subtext" => "video = core.sub.TextFile(video, SUB_PATH)\n",
                "xyvsf" => "video = core.xyvsf.TextSub(video, SUB_PATH)\n",
                "vsf" => "video = core.vsf.TextSub(video, SUB_PATH)\n",
                "vsfm" => "video = core.vsfm.TextSubMod(video, SUB_PATH)\n",
                _ => throw new InvalidOperationException(Loc.Text("MainWindow.UnknownRenderer"))
            });
        }
        script.Append("video.set_output()\n");
        Script = script.ToString();
        _scriptEdited = false;
    }

    private static string PythonString(string value) => "'" + value
        .Replace("\\", "\\\\")
        .Replace("'", "\\'")
        .Replace("\r", "\\r")
        .Replace("\n", "\\n")
        .Replace("\t", "\\t")
        .Replace("\0", "\\x00") + "'";
}
