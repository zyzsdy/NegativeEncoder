"""Render frames through the built-in software processing and subtitle plugins."""
import sys
from vapoursynth import core

clip = core.lsmas.LWLibavSource(sys.argv[1])
clip.get_frame(0)
core.vivtc.VDecimate(core.vivtc.VFM(clip, 1, mode=3, cthresh=8, mi=64)).get_frame(0)
core.xyvsf.TextSub(clip, sys.argv[2]).get_frame(0)
core.vsfm.TextSubMod(clip, sys.argv[2]).get_frame(0)
print("Decoded video, IVTC, xy-VSFilter and VSFilterMod rendered successfully")
