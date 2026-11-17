from copy import deepcopy
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

import test_check_packages as fixtures
from check_packages import PACKAGES, inspect_packages
from verify_package import read_inventory


class InstalledPackageChecks(unittest.TestCase):
    def setUp(self):
        fixture = fixtures.PackageChecks()
        fixture.setUp()
        self.addCleanup(fixture.doCleanups)
        self.root = fixture.root
        errors, self.inventory = inspect_packages(self.root, include_fingerprints=True)
        self.assertEqual([], errors)
        self.manifest = self.root / "inventory.json"
        self.manifest.write_text(json.dumps(self.inventory))

    def command(self, identity="rimweave.framework", folder=None):
        return subprocess.run([sys.executable, str(Path(__file__).with_name("verify_package.py")),
                               str(self.manifest), identity, str(folder or self.root / "mod")],
                              capture_output=True, text=True)

    def test_each_package_can_be_verified_alone_under_an_arbitrary_folder_name(self):
        for folder, identity, _, _ in PACKAGES:
            with self.subTest(identity=identity), tempfile.TemporaryDirectory() as temporary:
                target = Path(temporary) / "chosen-mod-folder"
                shutil.copytree(self.root / folder, target)
                before = {p.relative_to(target): p.read_bytes() for p in target.rglob("*") if p.is_file()}
                result = self.command(identity, target)
                self.assertEqual(0, result.returncode, result.stderr)
                self.assertIn(identity, result.stdout)
                self.assertEqual(before, {p.relative_to(target): p.read_bytes() for p in target.rglob("*") if p.is_file()})

    def test_same_size_content_change_is_reported_without_repair(self):
        path = self.root / "mod/Assemblies/RimWeave.dll"
        changed = b"N" + path.read_bytes()[1:]
        path.write_bytes(changed)
        result = self.command()
        self.assertEqual(1, result.returncode)
        self.assertIn("content differs", result.stderr)
        self.assertEqual(changed, path.read_bytes())

    def test_missing_and_extra_files_are_distinct(self):
        (self.root / "mod/Assemblies/RimWeave.dll").unlink()
        extra = self.root / "mod/Assemblies/RimWeave.pdb"
        extra.write_bytes(b"not recorded")
        result = self.command()
        self.assertEqual(1, result.returncode)
        self.assertIn("recorded file missing", result.stderr)
        self.assertIn("file not recorded", result.stderr)
        self.assertEqual(b"not recorded", extra.read_bytes())

    def test_recorded_optional_files_are_required_by_the_saved_inventory(self):
        path = self.root / "mod/Assemblies/RimWeave.pdb"
        path.write_bytes(b"recorded symbols")
        errors, inventory = inspect_packages(self.root, include_fingerprints=True)
        self.assertEqual([], errors)
        self.manifest.write_text(json.dumps(inventory))
        self.assertEqual(0, self.command().returncode)
        path.unlink()
        self.assertIn("RimWeave.pdb: recorded file missing", self.command().stderr)

    def test_symbolic_links_are_refused(self):
        path = self.root / "mod/Assemblies/RimWeave.dll"
        path.unlink()
        path.symlink_to(self.manifest)
        result = self.command()
        self.assertEqual(1, result.returncode)
        self.assertIn("symbolic link", result.stderr)
        self.assertTrue(path.is_symlink())

    def test_invalid_manifest_fields_are_refused(self):
        variants = []
        for field, value in [("formatVersion", 2), ("formatVersion", True), ("validation", "compatible"),
                             ("packages", None)]:
            value_copy = deepcopy(self.inventory)
            value_copy[field] = value
            variants.append(value_copy)
        for field, value in [("path", "../private"), ("path", "/etc/passwd"),
                             ("path", "Assemblies\\alias.dll"), ("bytes", True), ("bytes", -1),
                             ("sha256", "invalid")]:
            value_copy = deepcopy(self.inventory)
            value_copy["packages"][0]["files"][0][field] = value
            variants.append(value_copy)
        duplicate_package = deepcopy(self.inventory)
        duplicate_package["packages"].append(duplicate_package["packages"][0])
        variants.append(duplicate_package)
        duplicate_file = deepcopy(self.inventory)
        duplicate_file["packages"][0]["files"].append(duplicate_file["packages"][0]["files"][0])
        variants.append(duplicate_file)
        missing = deepcopy(self.inventory)
        missing["packages"][0]["files"] = []
        variants.append(missing)
        for value in variants:
            with self.subTest(value=value):
                self.manifest.write_text(json.dumps(value))
                result = self.command()
                self.assertEqual(1, result.returncode)
                self.assertEqual("", result.stdout)
                self.assertIn("Cannot verify package", result.stderr)

    def test_duplicate_json_fields_are_not_silently_overwritten(self):
        self.manifest.write_text('{"formatVersion": 1, "formatVersion": 1}')
        with self.assertRaisesRegex(ValueError, "Duplicate JSON field"):
            read_inventory(self.manifest)

    def test_invalid_json_and_missing_folder_are_reported(self):
        result = self.command(folder=self.root / "missing")
        self.assertEqual(1, result.returncode)
        self.assertIn("directory missing", result.stderr)
        self.manifest.write_text("{invalid")
        result = self.command()
        self.assertEqual(1, result.returncode)
        self.assertIn("Cannot verify package", result.stderr)
        self.assertNotIn("Traceback", result.stderr)


if __name__ == "__main__":
    unittest.main()
