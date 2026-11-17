from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from check_packages import PACKAGES, check_packages


class PackageChecks(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        for folder, identity, assembly, definitions in PACKAGES:
            root = self.root / folder
            (root / "About").mkdir(parents=True)
            (root / "Assemblies").mkdir()
            dependency = "" if identity == "rimweave.framework" else (
                "<modDependencies><li><packageId>rimweave.framework</packageId></li></modDependencies>"
                "<loadAfter><li>rimweave.framework</li></loadAfter>")
            (root / "About/About.xml").write_text(
                f"<ModMetaData><name>Fixture</name><description>Fixture</description>"
                f"<author>RimWeave Framework</author><packageId>{identity}</packageId>"
                f"<supportedVersions><li>1.7</li></supportedVersions>{dependency}</ModMetaData>")
            # The validator intentionally does not interpret or execute assembly bytes.
            (root / f"Assemblies/{assembly}.dll").write_bytes(b"not executable fixture")
            for name in definitions:
                path = root / "Defs/HediffDefs" / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text("<Defs />")

    def test_accepts_complete_packages_without_writing(self):
        before = {p.relative_to(self.root): p.read_bytes() for p in self.root.rglob("*") if p.is_file()}
        self.assertEqual([], check_packages(self.root))
        after = {p.relative_to(self.root): p.read_bytes() for p in self.root.rglob("*") if p.is_file()}
        self.assertEqual(before, after)

    def test_missing_and_empty_files_are_reported_together(self):
        (self.root / "mod/About/About.xml").unlink()
        (self.root / "mod/Assemblies/RimWeave.dll").write_bytes(b"")
        errors = check_packages(self.root)
        self.assertTrue(any("About/About.xml: required file missing" in x for x in errors))
        self.assertTrue(any("RimWeave.dll: empty file" in x for x in errors))

    def test_foreign_assembly_is_rejected_even_with_different_case(self):
        for name in ["Assembly-CSharp.dll", "UNITYSTUBS.DLL", "Other.dll"]:
            with self.subTest(name=name):
                path = self.root / "mod/Assemblies" / name
                path.write_bytes(b"foreign")
                self.assertTrue(any(name + ": unexpected file" in x for x in check_packages(self.root)))
                path.unlink()

    def test_manifest_contract_is_checked(self):
        path = self.root / "examples/RimWeave.Example/About/About.xml"
        original = path.read_text()
        for before, after, diagnostic in [
            ("rimweave.example", "wrong.id", "packageId"),
            ("<li>1.7</li>", "<li>1.6</li>", "supportedVersions"),
            ("<packageId>rimweave.framework</packageId>", "", "modDependencies"),
            ("<li>rimweave.framework</li>", "", "loadAfter"),
            ("<packageId>rimweave.example</packageId>", "<packageId>rimweave.example</packageId>" * 2, "packageId"),
        ]:
            with self.subTest(diagnostic=diagnostic):
                path.write_text(original.replace(before, after))
                self.assertTrue(any(diagnostic in x for x in check_packages(self.root)))
        path.write_text(original)

    def test_invalid_xml_and_definition_root_are_rejected(self):
        path = self.root / "examples/RimWeave.Example/Defs/HediffDefs/ExampleMarker.xml"
        for content, diagnostic in [("<broken", "unreadable XML"), ("<Wrong/>", "expected Defs root")]:
            with self.subTest(content=content):
                path.write_text(content)
                self.assertTrue(any(diagnostic in x for x in check_packages(self.root)))

    def test_file_and_directory_links_are_not_followed(self):
        for relative, directory in [("mod/Assemblies/alias.dll", False), ("mod/foreign", True)]:
            with self.subTest(relative=relative):
                path = self.root / relative
                target = self.root / "link-target"
                if directory:
                    target.mkdir(exist_ok=True)
                path.symlink_to(target, target_is_directory=directory)
                self.assertTrue(any("symbolic link" in x for x in check_packages(self.root)))
                path.unlink()

    def test_package_root_link_is_rejected(self):
        (self.root / "mod").rename(self.root / "original")
        (self.root / "mod").symlink_to(self.root / "original", target_is_directory=True)
        self.assertTrue(any("symbolic link in package path" in x for x in check_packages(self.root)))

    def test_cli_reports_failure_and_success(self):
        command = [sys.executable, str(Path(__file__).with_name("check_packages.py")), str(self.root)]
        success = subprocess.run(command, capture_output=True, text=True)
        self.assertEqual(0, success.returncode)
        self.assertIn("Checked rimweave.example.peer", success.stdout)
        (self.root / "mod/Assemblies/RimWeave.dll").unlink()
        failure = subprocess.run(command, capture_output=True, text=True)
        self.assertEqual(1, failure.returncode)
        self.assertIn("RimWeave.dll: required file missing", failure.stderr)


if __name__ == "__main__":
    unittest.main()
