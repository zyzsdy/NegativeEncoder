using System.Runtime.CompilerServices;
using System.Text;
using AvaloniaEdit.Document;

namespace NegativeEncoder.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    public TextDocument ScriptDocument { get; } = new();
    public string Script { get => ScriptDocument.Text; set => ScriptDocument.Text = value; }

    private string _subtitle = "";
    public string Subtitle { get => _subtitle; set => SetVsOption(ref _subtitle, value); }
    private bool _useVsfmod;
    public bool UseVsfmod { get => _useVsfmod; set => SetVsOption(ref _useVsfmod, value); }
    private bool _useRepeat;
    public bool UseRepeat { get => _useRepeat; set => SetVsOption(ref _useRepeat, value); }
    private bool _useQtgmc;
    public bool UseQtgmc { get => _useQtgmc; set => SetVsOption(ref _useQtgmc, value); }
    private bool _useIvtc;
    public bool UseIvtc { get => _useIvtc; set => SetVsOption(ref _useIvtc, value); }
    private bool _useYadifDouble;
    public bool UseYadifDouble { get => _useYadifDouble; set => SetVsOption(ref _useYadifDouble, value); }
    private bool _useYadifNormal;
    public bool UseYadifNormal { get => _useYadifNormal; set => SetVsOption(ref _useYadifNormal, value); }
    private bool _vsResize;
    public bool VsResize { get => _vsResize; set => SetVsOption(ref _vsResize, value); }
    private string _vsWidth = "1920";
    public string VsWidth { get => _vsWidth; set => SetVsOption(ref _vsWidth, value); }
    private string _vsHeight = "1080";
    public string VsHeight { get => _vsHeight; set => SetVsOption(ref _vsHeight, value); }

    private void SetVsOption<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Set(ref field, value, name)) GenerateVsScript();
    }

    public void GenerateVsScript()
    {
        // Match the Windows generator's filter order. The desktop app only produces
        // source: VapourSynth and its plugins must be installed in the execution environment.
        var script = new StringBuilder("from vapoursynth import core, YUV420P8\nimport sys\n");
        if (UseQtgmc) script.Append("from havsfunc import QTGMC\n");
        script.Append($"\nVIDEO_PATH = {PythonString(InputPath)}\n");
        if (!string.IsNullOrEmpty(Subtitle)) script.Append($"SUB_PATH = {PythonString(Subtitle)}\n");
        script.Append("\nvideo = core.lsmas.LWLibavSource(VIDEO_PATH");
        if (UseRepeat) script.Append(", repeat=True");
        script.Append(")\n");

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

        script.Append("video = core.resize.Bicubic(video, format=YUV420P8)\n");
        if (VsResize) script.Append($"video = core.resize.Lanczos(video, {VsWidth}, {VsHeight})\n");
        if (!string.IsNullOrEmpty(Subtitle))
        {
            if (UseVsfmod)
                script.Append("video = core.vsfm.TextSubMod(video, SUB_PATH)\n");
            else
            {
                script.Append("if hasattr(core, 'xyvsf'):\n");
                script.Append("    video = core.xyvsf.TextSub(video, SUB_PATH)\n");
                script.Append("else:\n");
                script.Append("    video = core.vsf.TextSub(video, SUB_PATH)\n");
            }
        }
        script.Append("video.set_output()\n");
        Script = script.ToString();
    }

    private static string PythonString(string value) => "'" + value
        .Replace("\\", "\\\\")
        .Replace("'", "\\'")
        .Replace("\r", "\\r")
        .Replace("\n", "\\n")
        .Replace("\t", "\\t")
        .Replace("\0", "\\x00") + "'";
}
