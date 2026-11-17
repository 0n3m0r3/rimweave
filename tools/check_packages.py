#!/usr/bin/env python3
"""Read-only structural checks for the three in-repository mod folders."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

PACKAGES = (
    ("mod", "rimweave.framework", "RimWeave", ()),
    ("examples/RimWeave.Example", "rimweave.example", "RimWeave.Example",
     ("CompositionMarker.xml", "ExampleMarker.xml", "ReferenceMarker.xml")),
    ("examples/RimWeave.Example.Peer", "rimweave.example.peer", "RimWeave.Example.Peer",
     ("CompositionMarker.xml",)),
)


# These technical examples are explicitly applied by developer commands only.
EXAMPLE_DEFINITIONS = {
    ("rimweave.example", "CompositionMarker.xml"): ("RimWeave_CompositionMedical", "RimWeave.Health.ContributionHediff"),
    ("rimweave.example", "ExampleMarker.xml"): ("RimWeave_ExampleMarker", "Verse.HediffWithComps"),
    ("rimweave.example", "ReferenceMarker.xml"): ("RimWeave_ReferenceMarker", "RimWeave.Health.ContributionHediff"),
    ("rimweave.example.peer", "CompositionMarker.xml"): ("RimWeave_CompositionTemporary", "RimWeave.Health.ContributionHediff"),
}


def check_example_definition(document, identity, filename, label):
    if document.tag != "Defs":
        return [f"{label}: expected Defs root"]
    if len(document) != 1 or document[0].tag != "HediffDef":
        return [f"{label}: expected exactly one HediffDef"]
    if document.attrib or document[0].attrib:
        return [f"{label}: expected an unconditional concrete definition without XML attributes"]
    name, effect_class = EXAMPLE_DEFINITIONS[identity, filename]
    expected = {"defName": name, "hediffClass": effect_class,
                **{field: "false" for field in
                   ("isBad", "makesAlert", "everCurableByItem", "tendable", "scenarioCanAdd")}}
    return [f"{label}: expected one {field} with value {value!r}"
            for field, value in expected.items()
            if [node.text for node in document[0].findall(field)] != [value]]


def fingerprint(path):
    """Read bytes without loading an assembly; callers provide a checked regular file."""
    digest = hashlib.sha256()
    size = 0
    with path.open("rb") as source:
        while block := source.read(1024 * 1024):
            size += len(block)
            digest.update(block)
    return {"bytes": size, "sha256": digest.hexdigest()}


def check_packages(repository):
    """Return all structural errors without loading assemblies or changing files."""
    return inspect_packages(repository)[0]


def inspect_packages(repository, include_fingerprints=False):
    """Return validation errors and an optional portable content inventory."""
    errors = []
    packages = []
    for folder, identity, assembly, definitions in PACKAGES:
        root = repository / folder
        inventory = []
        required = {"About/About.xml", f"Assemblies/{assembly}.dll"}
        required.update(f"Defs/HediffDefs/{name}" for name in definitions)
        allowed = required | {f"Assemblies/{assembly}.pdb", f"Assemblies/{assembly}.xml"}
        found = set()
        if any(path.is_symlink() for path in (root, *root.parents)):
            errors.append(f"{folder}: symbolic link in package path")
            continue
        if not root.is_dir():
            errors.append(f"{folder}: package directory missing")
            continue
        def walk_error(error):
            raise error

        for current, directories, files in os.walk(root, followlinks=False, onerror=walk_error):
            for name in list(directories):
                path = Path(current) / name
                if path.is_symlink():
                    errors.append(f"{folder}/{path.relative_to(root)}: symbolic link")
                    directories.remove(name)
            for name in files:
                path = Path(current) / name
                relative = path.relative_to(root).as_posix()
                label = f"{folder}/{relative}"
                if path.is_symlink():
                    errors.append(f"{label}: symbolic link")
                    continue
                if not path.is_file():
                    errors.append(f"{label}: not a regular file")
                    continue
                if relative not in allowed:
                    errors.append(f"{label}: unexpected file")
                    continue
                found.add(relative)
                if path.stat().st_size == 0:
                    errors.append(f"{label}: empty file")
                    continue
                if include_fingerprints:
                    inventory.append({"path": relative, **fingerprint(path)})
                if path.suffix == ".xml":
                    try:
                        document = ET.parse(path).getroot()
                    except (ET.ParseError, OSError) as error:
                        errors.append(f"{label}: unreadable XML ({error})")
                        continue
                    if relative == "About/About.xml":
                        expected = {
                            "packageId": [identity],
                            "supportedVersions/li": ["1.7"],
                            "author": ["RimWeave Framework"],
                            "modDependencies/li/packageId": [] if identity == "rimweave.framework" else ["rimweave.framework"],
                            "loadAfter/li": [] if identity == "rimweave.framework" else ["rimweave.framework"],
                        }
                        if document.tag != "ModMetaData":
                            errors.append(f"{label}: expected ModMetaData root")
                        for field, values in expected.items():
                            if [node.text for node in document.findall(field)] != values:
                                errors.append(f"{label}: unexpected {field}; expected {values}")
                        for field in ("name", "description"):
                            if not (document.findtext(field) or "").strip():
                                errors.append(f"{label}: missing {field}")
                    elif relative.startswith("Defs/"):
                        errors.extend(check_example_definition(document, identity, path.name, label))
        for missing in sorted(required - found):
            errors.append(f"{folder}/{missing}: required file missing")
        if include_fingerprints:
            packages.append({"packageId": identity, "files": sorted(inventory, key=lambda item: item["path"])})
    document = {"formatVersion": 1, "validation": "structure-only",
                "packages": sorted(packages, key=lambda item: item["packageId"])}
    return errors, document


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("repository", nargs="?", type=Path, default=Path(__file__).absolute().parent.parent)
    parser.add_argument("--json", action="store_true", help="Print a portable SHA-256 inventory after successful validation")
    args = parser.parse_args()
    try:
        errors, inventory = inspect_packages(args.repository.absolute(), include_fingerprints=args.json)
    except OSError as error:
        print(f"Cannot inspect packages: {error}", file=sys.stderr)
        return 1
    if errors:
        for error in errors:
            print(error, file=sys.stderr)
        return 1
    if args.json:
        print(json.dumps(inventory, indent=2, sort_keys=True))
        return 0
    for folder, identity, _, _ in PACKAGES:
        print(f"Checked {identity}: {folder}")
    print("Structure only: assembly contents, build configuration and game compatibility are not verified.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
