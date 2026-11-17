"""Run only with the private Python. No system/user site or plugin search paths."""
import importlib.metadata
import json
import os
from pathlib import Path
import sys
import tempfile

runtime = Path(sys.argv[1]).resolve()
user_plugins = Path(sys.argv[2]).resolve()
user_scripts = Path(sys.argv[3]).resolve()
if Path(sys.prefix).resolve() != (runtime / 'python').resolve():
    raise RuntimeError('The probe is not using the selected private Python')
os.environ.pop('VAPOURSYNTH_EXTRA_PLUGIN_PATH', None)
sys.path.insert(0, str(user_scripts))
import vapoursynth as vs
if not Path(vs.__file__).resolve().is_relative_to(runtime):
    raise RuntimeError('VapourSynth was imported from outside the selected Libs environment')
core = vs.core
core.num_threads = min(core.num_threads, 4)
available = {'runtime': str(vs.__version__)}
errors = {}

# Load user plugins explicitly: LoadPlugin reports failures instead of autoload warnings.
try:
    for path in sorted(user_plugins.rglob('*')):
        if path.suffix.lower() in ('.dll', '.dylib', '.so'):
            core.std.LoadPlugin(str(path))
except Exception as exc:
    errors['user-plugins'] = f'{type(exc).__name__}: {exc}'

with tempfile.TemporaryDirectory(prefix='.probe-', dir=runtime) as scratch:
    scratch = Path(scratch)
    video = scratch / 'probe.y4m'
    with video.open('wb') as f:
        f.write(b'YUV4MPEG2 W320 H240 F30000:1001 Ip A1:1 C420jpeg\n')
        for i in range(12):
            f.write(b'FRAME\n' + bytes([16+i]) * (320*240) + bytes([128])*(320*240//2))
    subtitle = scratch / 'probe.ass'
    subtitle.write_text('[Script Info]\nScriptType: v4.00+\nPlayResX: 320\nPlayResY: 240\n'
        '[V4+ Styles]\nFormat: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, '
        'Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\n'
        'Style: Default,Arial,20,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,1,0,2,10,10,10,1\n'
        '[Events]\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\n'
        'Dialogue: 0,0:00:00.00,0:00:10.00,Default,,0,0,0,,NegativeEncoder\n', encoding='utf-8')
    clip = core.std.BlankClip(width=320, height=240, length=12, format=vs.YUV420P8, fpsnum=30000, fpsden=1001).std.SetFieldBased(2)
    def qtgmc():
        from havsfunc import QTGMC
        return QTGMC(clip, Preset='fast', TFF=True)
    tests = {
        'source': lambda: core.lsmas.LWLibavSource(str(video), repeat=0),
        'ivtc': lambda: core.vivtc.VDecimate(core.vivtc.VFM(clip, order=1, mode=3, cthresh=8, mi=64)),
        'subtext': lambda: core.sub.TextFile(clip, str(subtitle)),
        'bwdif': lambda: core.bwdif.Bwdif(clip, field=3),
        'qtgmc': qtgmc,
        'xyvsf': lambda: core.xyvsf.TextSub(clip, str(subtitle)),
        'vsf': lambda: core.vsf.TextSub(clip, str(subtitle)),
        'vsfm': lambda: core.vsfm.TextSubMod(clip, str(subtitle)),
        'yadif': lambda: core.yadifmod.Yadifmod(clip, core.nnedi3cl.NNEDI3CL(clip, field=3), order=1, field=-1, mode=1),
    }
    for name, test in tests.items():
        try:
            result = test()
            result.get_frame(0)
            result.get_frame(min(5, result.num_frames - 1))
            available[name] = '已通过取帧检查'
        except Exception as exc:
            errors[name] = f'{type(exc).__name__}: {exc}'

versions = {d.metadata['Name']: d.version for d in importlib.metadata.distributions() if d.metadata.get('Name', '').lower().startswith('vapoursynth')}
print('NE_PROBE:' + json.dumps({'available': available, 'errors': errors, 'versions': versions}, ensure_ascii=False))
