using NegativeEncoder.VsScriptBuilder;
using Xunit;

namespace NegativeEncoder.Tests;

public class VsScriptPackagingTests
{
    [Fact]
    public void SubtitleScript_AcceptsPackagedXyVsfilterAndLegacyVsf()
    {
        var script = NegativeEncoder.VsScriptBuilder.VsScriptBuilder.Build(new VsScript { SubFile = "test.ass" }, "video.mp4");
        Assert.Contains("if hasattr(core, 'xyvsf'):", script);
        Assert.Contains("core.xyvsf.TextSub(video, SUB_PATH)", script);
        Assert.Contains("core.vsf.TextSub(video, SUB_PATH)", script);
    }

    [Fact]
    public void ModifiedSubtitleScript_UsesAutoloadedPluginBeforeLegacyDirectory()
    {
        var script = NegativeEncoder.VsScriptBuilder.VsScriptBuilder.Build(new VsScript { SubFile = "test.ass", UseVsfmod = true }, "video.mp4");
        Assert.Contains("if not hasattr(core, 'vsfm'):", script);
        Assert.Contains("core.vsfm.TextSubMod(video, SUB_PATH)", script);
        Assert.DoesNotContain("core.xyvsf.TextSub", script);
    }
}
