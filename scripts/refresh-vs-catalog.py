#!/usr/bin/env python3
"""Refresh explicitly pinned VS distribution metadata; never promotes an untested version.
Use HTTPS_PROXY if needed. Review the diff, then run the runtime smoke check before commit.
"""
import hashlib
import json
import pathlib
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[1]
CACHE = ROOT / 'artifacts/tool-library-research'
CATALOG = ROOT / 'NegativeEncoder.Desktop/ToolLibrary/runtime-catalog.json'
PINS = {'VapourSynth': ('79', 'runtime'), 'vapoursynth-lsmas': ('1310.0.0.0', 'basic'),
        'vapoursynth-vivtc': ('2.0', 'basic'), 'vapoursynth-subtext': ('7.0', 'basic'),
        'vapoursynth-bwdif': ('5.1', 'basic'), 'vapoursynth-mvtools': ('29', 'qtgmc'),
        'vapoursynth-znedi3': ('3.3', 'qtgmc'), 'vapoursynth-eedi3': ('10.0', 'qtgmc')}
RIDS = {'win-x64': 'win_amd64', 'osx-x64': 'macosx_15_0_x86_64', 'osx-arm64': 'macosx_15_0_arm64',
        'linux-x64': 'manylinux_x86_64', 'linux-arm64': 'manylinux_aarch64'}

def fetch(url, name):
    p = CACHE / name
    if not p.exists():
        with urllib.request.urlopen(url, timeout=60) as response:
            p.write_bytes(response.read())
    return p.read_bytes()

def match(filename, rid):
    if not filename.endswith('.whl') or ('cp3' in filename and '-cp312-abi3-' not in filename and '-cp313-cp313-' not in filename):
        return False
    arch = 'arm64' if rid.endswith('arm64') else 'x86_64'
    if rid.startswith('osx'): return 'macosx_' in filename and filename.endswith(arch + '.whl')
    if rid.startswith('linux'): return 'manylinux' in filename and filename.endswith(('aarch64' if arch == 'arm64' else arch) + '.whl')
    return filename.endswith('win_amd64.whl')

def main():
    CACHE.mkdir(parents=True, exist_ok=True)
    catalog = json.loads(CATALOG.read_text())
    packages = []
    for name, (version, group) in PINS.items():
        data = json.loads(fetch(f'https://pypi.org/pypi/{name}/{version}/json', name + '.json'))
        assert data['info']['version'] == version
        assets = []
        for rid in RIDS:
            matches = [a for a in data['urls'] if not a.get('yanked') and match(a['filename'], rid)]
            if not matches: continue
            if len(matches) != 1: raise ValueError((name, rid, [a['filename'] for a in matches]))
            a = matches[0]
            assets.append(dict(platform=rid, url=a['url'], sha256=a['digests']['sha256'], fileName=a['filename'], files={}))
        packages.append(dict(id=name, version=version, group=group, kind='wheel', assets=assets))
    for name in ['rgvs', 'misc', 'fmtc']:
        data = json.loads(fetch(f'https://raw.githubusercontent.com/vapoursynth/vsrepo/master/local/{name}.json', name + '.json'))
        assets = []
        for rid, key in [('win-x64','win64'),('osx-x64','darwin-x86_64'),('osx-arm64','darwin-aarch64'),('linux-x64','linux-glibc-x86_64')]:
            release = next((r for r in data['releases'] if key in r), None)
            if not release: continue
            a = release[key]; url = a['url']; fn = url.split('/')[-1]
            blob = fetch(url, name + '-' + rid + '-' + fn)
            assets.append(dict(platform=rid,url=url,sha256=hashlib.sha256(blob).hexdigest(),fileName=fn,
                               files={f'vapoursynth/plugins/{k}':v[0] for k,v in a['files'].items()},
                               fileHashes={f'vapoursynth/plugins/{k}':v[1] for k,v in a['files'].items()}, version=release['version']))
        packages.append(dict(id=name,version='catalog-pinned',group='qtgmc',kind='files',assets=assets))
    # These source archives have already been validated and pinned in the runtime
    # catalog. Preserve their platform mappings, file hashes and script patches.
    pinned = {package['id']: package for package in catalog['packages']}
    for name in ['havsfunc','mvsfunc','adjust','xyvsf','vsfm','yadifmod','nnedi3cl','nnedi3_weights']:
        packages.append(pinned[name])
    catalog['packages'] = packages
    catalog['combination'] = '2026-11-18.1'
    CATALOG.write_text(json.dumps(catalog,indent=2,ensure_ascii=False)+'\n')

if __name__ == '__main__': main()
