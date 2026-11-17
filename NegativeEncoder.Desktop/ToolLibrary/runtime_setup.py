"""Configure vspipe inside this generation, never register a global VS installation."""
from pathlib import Path
import json
import os
import sys
runtime = Path(sys.argv[1]).resolve()
if Path(sys.prefix).resolve() != (runtime / 'python').resolve():
    raise RuntimeError('Unexpected Python runtime')
import vapoursynth
if sys.version.split()[0] != sys.argv[2] or str(vapoursynth.__version__).lstrip('R') != sys.argv[3]:
    raise RuntimeError(f'Runtime version mismatch: Python {sys.version}, VS {vapoursynth.__version__}')
vapoursynth.vapoursynth_config()
config = runtime / 'config' / 'vapoursynth' / 'vapoursynth.toml'
if sys.platform != 'win32' and not config.is_file():
    raise RuntimeError('VapourSynth configuration was not written')
print(json.dumps({'python': sys.version.split()[0], 'vapoursynth': str(vapoursynth.__version__)}))
