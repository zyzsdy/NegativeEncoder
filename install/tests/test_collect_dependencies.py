import hashlib
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

spec = importlib.util.spec_from_file_location("collector", Path(__file__).parents[1] / "collect_dependencies.py")
collector = importlib.util.module_from_spec(spec)
spec.loader.exec_module(collector)


class DependencyCollectionTests(unittest.TestCase):
    def test_rejects_paths_outside_staging(self):
        with tempfile.TemporaryDirectory() as temporary:
            with self.assertRaises(ValueError):
                collector.inside(temporary, "../escaped.dll")

    def test_rejects_unsafe_zip_members(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            archive = root / "bad.zip"
            with zipfile.ZipFile(archive, "w") as output:
                output.writestr("../escaped.dll", "bad")
            with self.assertRaises(ValueError):
                collector.extract(archive, root / "extracted", None)
            self.assertFalse((root / "escaped.dll").exists())

    def test_refuses_to_overwrite_conflicting_dll(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source, destination = root / "source.dll", root / "destination.dll"
            source.write_bytes(b"new")
            destination.write_bytes(b"old")
            with self.assertRaises(ValueError):
                collector.copy_file(source, destination)
            self.assertEqual(b"old", destination.read_bytes())

    def test_verifies_vsrepo_file_hash_before_copying(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            archive = root / "plugin.zip"
            with zipfile.ZipFile(archive, "w") as output:
                output.writestr("win64/plugin.dll", b"plugin")
            asset = {"name": "plugin", "mode": "files", "destination": "Libs/plugins",
                     "files": {"plugin.dll": {"path": "win64/plugin.dll", "sha256": "0" * 64}}}
            with self.assertRaises(ValueError):
                collector.assemble_asset(asset, archive, root / "staging", root / "work", None)
            self.assertFalse((root / "staging/Libs/plugins/plugin.dll").exists())
            asset["files"]["plugin.dll"]["sha256"] = hashlib.sha256(b"plugin").hexdigest()
            collector.assemble_asset(asset, archive, root / "staging", root / "work", None)
            self.assertEqual(b"plugin", (root / "staging/Libs/plugins/plugin.dll").read_bytes())

    def test_cache_is_verified_before_reuse(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            asset = {"name": "test", "version": "1", "url": "https://example.invalid/test.zip",
                     "sha256": hashlib.sha256(b"expected").hexdigest()}
            cached = root / (asset["sha256"] + "-test.zip")
            cached.write_bytes(b"expected")
            with patch("urllib.request.urlopen") as request:
                self.assertEqual(cached, collector.download(asset, root))
                request.assert_not_called()
            cached.write_bytes(b"tampered")
            with patch("urllib.request.urlopen", side_effect=ValueError("re-download")) as request:
                with self.assertRaisesRegex(ValueError, "re-download"):
                    collector.download(asset, root)
                request.assert_called_once()
                self.assertFalse(cached.exists())


if __name__ == "__main__":
    unittest.main()
