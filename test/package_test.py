"""Exercise release-blocking checks against real, deliberately corrupted VSIXs."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from zipfile import ZipFile, ZIP_DEFLATED

ROOT = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location("verify_package", ROOT / "scripts/verify_package.py")
verifier = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verifier)
METADATA = json.loads((ROOT / "package.json").read_text(encoding="utf-8"))
ARTIFACT = ROOT / "dist" / f"ContextIME-{METADATA['version']}-win32-x64.vsix"


class ReleaseChecks(unittest.TestCase):
    def mutate(self, changes):
        with tempfile.TemporaryDirectory() as temporary:
            target = Path(temporary) / "test.vsix"
            with ZipFile(ARTIFACT) as source, ZipFile(target, "w", ZIP_DEFLATED) as dest:
                for name in source.namelist():
                    data = source.read(name)
                    dest.writestr(name, changes.get(name, lambda value: value)(data))
            with self.assertRaises(ValueError):
                verifier.verify(target, METADATA)

    def test_official_package_passes(self):
        self.assertEqual(len(verifier.verify(ARTIFACT, METADATA)), 64)

    def test_wrong_publisher_is_rejected(self):
        def change(raw):
            metadata = json.loads(raw)
            metadata["publisher"] = "local-tools"
            return json.dumps(metadata).encode()
        self.mutate({"extension/package.json": change})

    def test_wrong_platform_is_rejected(self):
        self.mutate({"extension.vsixmanifest": lambda value: value.replace(b'win32-x64', b'darwin-arm64')})

    def test_oversized_icon_is_rejected(self):
        self.mutate({"extension/" + METADATA["icon"]: lambda value: value + b"x" * (1024 * 1024)})

    def test_changed_runtime_is_rejected(self):
        self.mutate({"extension/extension.js": lambda value: value + b"\n// changed\n"})


if __name__ == "__main__":
    unittest.main()
