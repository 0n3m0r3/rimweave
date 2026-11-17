from pathlib import Path
import subprocess
import hashlib
import json
import shutil
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

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
                path.write_bytes((Path(__file__).resolve().parent.parent / folder / "Defs/HediffDefs" / name).read_bytes())

    def test_accepts_complete_packages_without_writing(self):
        before = {p.relative_to(self.root): p.read_bytes() for p in self.root.rglob("*") if p.is_file()}
        self.assertEqual([], check_packages(self.root))
        after = {p.relative_to(self.root): p.read_bytes() for p in self.root.rglob("*") if p.is_file()}
        self.assertEqual(before, after)

    def test_repository_manifests_match_the_package_contract(self):
        repository = Path(__file__).resolve().parent.parent
        for folder, _, _, _ in PACKAGES:
            (self.root / folder / "About/About.xml").write_bytes(
                (repository / folder / "About/About.xml").read_bytes())
        self.assertEqual([], check_packages(self.root))

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

    def test_example_definition_identity_and_count_are_checked(self):
        path = self.root / "examples/RimWeave.Example/Defs/HediffDefs/ExampleMarker.xml"
        original = path.read_text()
        cases = ["<Defs />", original.replace("<HediffDef>", '<HediffDef Abstract="true">'),
                 original.replace("<Defs>", '<Defs MayRequire="absent.mod">'), original.replace("RimWeave_ExampleMarker", "WrongName"),
                 original.replace("Verse.HediffWithComps", "Wrong.Class"),
                 original.replace("</Defs>", "<HediffDef><defName>Extra</defName></HediffDef></Defs>")]
        for content in cases:
            with self.subTest(content=content):
                path.write_text(content)
                self.assertTrue(any("ExampleMarker.xml:" in x for x in check_packages(self.root)))
        path.write_text(original)

    def test_example_neutrality_fields_must_be_explicit_and_unique(self):
        path = self.root / "examples/RimWeave.Example.Peer/Defs/HediffDefs/CompositionMarker.xml"
        original = path.read_text()
        for field in ["isBad", "makesAlert", "everCurableByItem", "tendable", "scenarioCanAdd"]:
            node = f"<{field}>false</{field}>"
            for replacement in ["", f"<{field}>true</{field}>", node * 2]:
                with self.subTest(field=field, replacement=replacement):
                    path.write_text(original.replace(node, replacement))
                    self.assertTrue(any(field in x for x in check_packages(self.root)))
        path.write_text(original)

    def test_required_example_values_reject_attributes_and_nested_elements(self):
        path = self.root / "examples/RimWeave.Example/Defs/HediffDefs/ExampleMarker.xml"
        original = path.read_text()
        fields = ["defName", "hediffClass", "isBad", "makesAlert", "everCurableByItem", "tendable", "scenarioCanAdd"]
        for field in fields:
            for shape in ("attribute", "child"):
                with self.subTest(field=field, shape=shape):
                    document = ET.fromstring(original)
                    node = document[0].find(field)
                    if shape == "attribute":
                        node.set("MayRequire", "absent.example")
                    else:
                        ET.SubElement(node, "unexpected")
                    path.write_text(ET.tostring(document, encoding="unicode"))
                    before = path.read_bytes()
                    errors = check_packages(self.root)
                    self.assertTrue(any("ExampleMarker.xml:" in error and field in error for error in errors))
                    self.assertEqual(before, path.read_bytes())
        path.write_text(original)
        self.assertEqual([], check_packages(self.root))

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

    def json_command(self, root=None):
        return subprocess.run([sys.executable, str(Path(__file__).with_name("check_packages.py")),
                               str(root or self.root), "--json"], capture_output=True, text=True)

    def test_json_inventory_is_portable_ordered_and_read_only(self):
        before = {p.relative_to(self.root): p.read_bytes() for p in self.root.rglob("*") if p.is_file()}
        first = self.json_command()
        self.assertEqual(0, first.returncode, first.stderr)
        document = json.loads(first.stdout)
        self.assertEqual(1, document["formatVersion"])
        self.assertEqual("structure-only", document["validation"])
        self.assertNotIn(str(self.root), first.stdout)
        self.assertEqual(sorted(x["packageId"] for x in document["packages"]),
                         [x["packageId"] for x in document["packages"]])
        for package in document["packages"]:
            self.assertEqual(sorted(x["path"] for x in package["files"]), [x["path"] for x in package["files"]])
        with tempfile.TemporaryDirectory() as other:
            copy = Path(other) / "another-location"
            shutil.copytree(self.root, copy)
            self.assertEqual(first.stdout, self.json_command(copy).stdout)
        self.assertEqual(before, {p.relative_to(self.root): p.read_bytes() for p in self.root.rglob("*") if p.is_file()})

    def test_json_fingerprints_actual_bytes_and_optional_files(self):
        path = self.root / "mod/Assemblies/RimWeave.pdb"
        path.write_bytes(b"symbols fixture")
        first = json.loads(self.json_command().stdout)
        package = next(x for x in first["packages"] if x["packageId"] == "rimweave.framework")
        entry = next(x for x in package["files"] if x["path"] == "Assemblies/RimWeave.pdb")
        self.assertEqual({"path": "Assemblies/RimWeave.pdb", "bytes": 15,
                          "sha256": hashlib.sha256(b"symbols fixture").hexdigest()}, entry)
        path.write_bytes(b"changed fixture")
        second = json.loads(self.json_command().stdout)
        changed = next(x for x in second["packages"] if x["packageId"] == "rimweave.framework")
        self.assertNotEqual(package, changed)
        self.assertEqual([x for x in first["packages"] if x is not package],
                         [x for x in second["packages"] if x is not changed])

    def test_json_is_not_emitted_for_invalid_packages(self):
        (self.root / "mod/Assemblies/RimWeave.dll").unlink()
        result = self.json_command()
        self.assertEqual(1, result.returncode)
        self.assertEqual("", result.stdout)
        self.assertIn("required file missing", result.stderr)

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
