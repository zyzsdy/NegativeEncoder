"""Assemble Windows x64 Libs from reviewed, hash-locked upstream assets.

VSRepo file maps are exported into the lock file. No update/latest lookup or pip
dependency resolution occurs during a release build. Requires Python 3.11+ and 7z.
"""

import argparse
import concurrent.futures
import hashlib
import json
import os
import re
from pathlib import Path
import shutil
import subprocess
import tempfile
import time
import urllib.parse
import urllib.request
import zipfile


def sha256(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def inside(root, relative):
    root = Path(root).resolve()
    result = (root / relative).resolve()
    if result != root and root not in result.parents:
        raise ValueError(f"Path escapes staging directory: {relative}")
    return result


def validate_lock(lock):
    if lock.get("schema") != 1:
        raise ValueError("Unsupported dependency lock schema")
    names = set()
    for asset in lock["assets"]:
        if not re.fullmatch(r"[A-Za-z0-9_-]+", asset["name"]):
            raise ValueError("Invalid asset name")
        if asset.get("extension") not in {".zip", ".7z", ".exe"}:
            raise ValueError(f"Invalid archive extension: {asset['name']}")
        if asset["name"] in names:
            raise ValueError(f"Duplicate asset: {asset['name']}")
        names.add(asset["name"])
        if urllib.parse.urlsplit(asset["url"]).scheme != "https":
            raise ValueError(f"HTTPS required: {asset['name']}")
        digest = asset["sha256"]
        if len(digest) != 64 or any(c not in "0123456789abcdef" for c in digest):
            raise ValueError(f"Invalid SHA256: {asset['name']}")
        if asset["mode"] not in {"directory", "files", "python", "vapoursynth"}:
            raise ValueError(f"Unknown extraction mode: {asset['name']}")
        inside(Path.cwd(), asset["destination"])


def download(asset, cache):
    filename = asset["sha256"] + "-" + asset["name"] + asset.get("extension", ".zip")
    target = cache / filename
    if target.exists():
        if sha256(target) == asset["sha256"]:
            return target
        target.unlink()
    request = urllib.request.Request(asset["url"], headers={"User-Agent": "NegativeEncoder-packaging"})
    for attempt in range(3):
        temporary = target.with_suffix(target.suffix + ".partial")
        try:
            print(f"Downloading {asset['name']} {asset['version']}", flush=True)
            with urllib.request.urlopen(request, timeout=60) as response, temporary.open("wb") as output:
                shutil.copyfileobj(response, output)
            if sha256(temporary) != asset["sha256"]:
                raise ValueError(f"SHA256 mismatch: {asset['name']}")
            temporary.replace(target)
            return target
        except ValueError:
            temporary.unlink(missing_ok=True)
            raise
        except (OSError, urllib.error.URLError):
            temporary.unlink(missing_ok=True)
            if attempt == 2:
                curl = shutil.which("curl.exe")
                if not curl:
                    raise
                # Windows curl uses the system TLS store when a local Python TLS setup fails.
                subprocess.run([curl, "--fail", "--location", "--silent", "--show-error",
                                "--connect-timeout", "30", "--max-time", "600",
                                "--output", str(temporary), asset["url"]], check=True)
                if sha256(temporary) != asset["sha256"]:
                    temporary.unlink(missing_ok=True)
                    raise ValueError(f"SHA256 mismatch: {asset['name']}")
                temporary.replace(target)
                return target
            time.sleep(2 ** attempt)


def extract(archive, destination, seven_zip):
    destination.mkdir(parents=True, exist_ok=True)
    if zipfile.is_zipfile(archive):
        with zipfile.ZipFile(archive) as source:
            for member in source.infolist():
                inside(destination, member.filename)
            source.extractall(destination)
    else:
        subprocess.run([str(seven_zip), "x", "-y", "-bso0", "-bsp0", str(archive),
                        f"-o{destination}"], check=True)


def copy_file(source, target):
    target.parent.mkdir(parents=True, exist_ok=True)
    if target.exists() and sha256(source) != sha256(target):
        raise ValueError(f"Dependency file collision: {target}")
    shutil.copy2(source, target)


def copy_directory(source, destination):
    for file in source.rglob("*"):
        if file.is_file():
            copy_file(file, inside(destination, file.relative_to(source)))


def assemble_asset(asset, archive, staging, extracted, seven_zip):
    root = extracted / asset["name"]
    extract(archive, root, seven_zip)
    destination = inside(staging, asset["destination"])
    mode = asset["mode"]
    if mode in {"directory", "python"}:
        if "executable" in asset:
            matches = list(root.rglob(asset["executable"]))
            if len(matches) != 1:
                raise ValueError(f"Expected one {asset['executable']}, got {matches}")
            source = matches[0].parent
        else:
            source = root
        copy_directory(source, destination)
    elif mode == "vapoursynth":
        wheels = list(root.rglob("*.whl"))
        if len(wheels) != 1:
            raise ValueError("Expected one bundled VapourSynth wheel")
        wheel = extracted / "vapoursynth-wheel"
        extract(wheels[0], wheel, seven_zip)
        copy_directory(wheel, destination)
    else:
        for target, spec in asset["files"].items():
            source = inside(root, spec["path"])
            if not source.is_file() or sha256(source) != spec["sha256"]:
                raise ValueError(f"VSRepo file verification failed: {asset['name']}/{target}")
            output = inside(destination, target)
            if spec.get("replacements"):
                content = source.read_bytes().decode("utf-8")
                for patch in spec["replacements"]:
                    if content.count(patch["old"]) != patch["count"]:
                        raise ValueError(f"Compatibility patch no longer matches: {asset['name']}")
                    content = content.replace(patch["old"], patch["new"])
                patched = content.encode("utf-8")
                if hashlib.sha256(patched).hexdigest() != spec["patched_sha256"]:
                    raise ValueError(f"Compatibility patch checksum mismatch: {asset['name']}")
                output.parent.mkdir(parents=True, exist_ok=True)
                if output.exists() and output.read_bytes() != patched:
                    raise ValueError(f"Dependency file collision: {output}")
                output.write_bytes(patched)
            else:
                copy_file(source, output)

    # Preserve notices wherever an upstream archive places them, including outside bin/x64.
    for file in root.rglob("*"):
        if file.is_file() and any(word in file.name.lower() for word in
                                  ("license", "licence", "copying", "copyright", "readme", "notice")):
            copy_file(file, inside(staging / "ThirdParty" / asset["name"], file.relative_to(root)))


def configure_python(staging, lock):
    runtime = staging / "Libs" / "python"
    pth = list(runtime.glob("python*._pth"))
    if len(pth) != 1:
        raise ValueError("Expected one embedded Python _pth file")
    stdlib = next(runtime.glob("python*.zip")).name
    pth[0].write_text(f"{stdlib}\n.\nLib/site-packages\n../site-packages\nimport site\n", encoding="utf-8")
    (runtime / "Lib" / "site-packages").mkdir(parents=True, exist_ok=True)
    # Do not copy Scripts launchers: pip's Windows entry points embed the CI Python path.
    (staging / "Libs" / "vspipe.bat").write_text(
        '@echo off\n"%~dp0python\\Lib\\site-packages\\vapoursynth\\vspipe.exe" %*\n', encoding="utf-8")
    (staging / "dependencies.lock.json").write_text(json.dumps(lock, indent=2) + "\n", encoding="utf-8")
    notices = ["# Third-party software", "", "Apple Application Support is not bundled. Install it to use qaac.",
               "Compatible GPU hardware/drivers (including OpenCL for NNEDI3CL) are required.",
               "Microsoft Visual C++ 2015-2026 x64 runtime is a prerequisite for native tools.",
               "", "Upstream license/readme files are preserved under ThirdParty/<name>.",
               "Package provenance and exact checksums are in dependencies.lock.json.", ""]
    for asset in lock["assets"]:
        notices.extend([f"## {asset['name']} {asset['version']}", asset["url"],
                        f"Source: {asset.get('source', asset['url'])}", ""])
    (staging / "THIRD-PARTY-NOTICES.md").write_text("\n".join(notices), encoding="utf-8")


def run_checked(command, **kwargs):
    print("Checking " + str(command[0]), flush=True)
    result = subprocess.run(command, capture_output=True, text=True, errors="replace", timeout=120, **kwargs)
    if result.returncode:
        raise RuntimeError(f"Command failed ({result.returncode}): {command}\n{result.stdout}\n{result.stderr}")
    if result.stdout.strip():
        print(result.stdout.strip().splitlines()[0], flush=True)
    return result.stdout


def verify(staging):
    libs = staging / "Libs"
    for file, args in [(libs / "ffmpeg.exe", ["-version"]), (libs / "7z.exe", ["i"]),
                       (libs / "MKVToolNix" / "mkvmerge.exe", ["--version"]),
                       *[(libs / name / (name + "C64.exe"), ["--version"])
                         for name in ("NVEnc", "QSVEnc", "VCEEnc")]]:
        run_checked([str(file), *args])
    if not (libs / "qaac" / "qaac64.exe").is_file():
        raise ValueError("qaac64.exe is missing")
    python = libs / "python" / "python.exe"
    run_checked([str(python), "-c", "from vapoursynth import core; from havsfunc import QTGMC; "
                 "required=['lsmas','vivtc','yadifmod','xyvsf','vsfm','mv','misc','rgvs','fmtc','znedi3','eedi3m']; "
                 "assert all(hasattr(core, n) for n in required), required; "
                 "print('NNEDI3CL loaded:', hasattr(core, 'nnedi3cl'), '(OpenCL driver required)'); "
                 "QTGMC(core.std.BlankClip(width=320,height=240,length=8,format=__import__('vapoursynth').YUV420P8).std.SetFieldBased(2), "
                 "Preset='fast', TFF=True).get_frame(0)"])
    vspipe = libs / "python" / "Lib" / "site-packages" / "vapoursynth" / "vspipe.exe"
    run_checked([str(vspipe), "--version"])
    with tempfile.TemporaryDirectory(prefix="ne-vspipe-") as temp:
        script = Path(temp) / "blank.vpy"
        script.write_text("from vapoursynth import core\ncore.std.BlankClip(width=320,height=240,length=2).set_output()\n")
        run_checked([str(vspipe), "--info", str(script), "-"])
        sample = Path(temp) / "sample.mp4"
        subtitle = Path(temp) / "sample.ass"
        run_checked([str(libs / "ffmpeg.exe"), "-hide_banner", "-loglevel", "error", "-y",
                     "-f", "lavfi", "-i", "color=c=black:s=320x240:r=30", "-t", "1",
                     "-c:v", "libx264", "-pix_fmt", "yuv420p", str(sample)])
        subtitle.write_text(
            "[Script Info]\nScriptType: v4.00+\nPlayResX: 320\nPlayResY: 240\n"
            "[V4+ Styles]\n"
            "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, "
            "Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, "
            "Alignment, MarginL, MarginR, MarginV, Encoding\n"
            "Style: Default,Arial,20,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,1,0,2,10,10,10,1\n"
            "[Events]\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\n"
            "Dialogue: 0,0:00:00.00,0:00:01.00,Default,,0,0,0,,Smoke test\n", encoding="utf-8")
        run_checked([str(python), str(Path(__file__).with_name("smoke_vapoursynth.py").resolve()),
                     str(sample), str(subtitle)])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--lock", type=Path, default=Path(__file__).with_name("dependencies.lock.json"))
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--cache", type=Path, default=Path("artifacts/dependency-cache"))
    parser.add_argument("--seven-zip", type=Path, default=Path(shutil.which("7z") or
                        os.environ.get("ProgramFiles", "C:/Program Files") + "/7-Zip/7z.exe"))
    parser.add_argument("--verify-only", action="store_true")
    args = parser.parse_args()
    if args.verify_only:
        verify(args.output.resolve())
        return
    lock = json.loads(args.lock.read_text(encoding="utf-8"))
    validate_lock(lock)
    if args.output.exists() and any(args.output.iterdir()):
        raise ValueError("Dependency staging directory must be empty")
    args.output.mkdir(parents=True, exist_ok=True)
    args.cache.mkdir(parents=True, exist_ok=True)
    if not args.seven_zip.is_file():
        raise ValueError(f"7-Zip not found: {args.seven_zip}")
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as executor:
        archives = list(executor.map(lambda asset: download(asset, args.cache), lock["assets"]))
    with tempfile.TemporaryDirectory(prefix="ne-dependencies-") as temporary:
        for asset, archive in zip(lock["assets"], archives):
            assemble_asset(asset, archive, args.output.resolve(), Path(temporary), args.seven_zip.resolve())
    configure_python(args.output.resolve(), lock)
    shutil.copyfile(args.lock, args.output / "dependencies.lock.json")
    verify(args.output.resolve())
    print(f"Verified dependencies: {args.output.resolve()}")


if __name__ == "__main__":
    main()
