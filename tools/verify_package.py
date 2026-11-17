#!/usr/bin/env python3
"""Compare one explicitly chosen mod folder with a saved package inventory, without changing files."""
import argparse
import json
from pathlib import Path
import re
import sys

from check_packages import PACKAGES, inspect_package, package_files


def unique_fields(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate JSON field")
        result[key] = value
    return result


def read_inventory(path):
    document = json.loads(path.read_text(), object_pairs_hook=unique_fields)
    if not isinstance(document, dict) or set(document) != {"formatVersion", "validation", "packages"}:
        raise ValueError("Invalid inventory fields")
    if type(document["formatVersion"]) is not int or document["formatVersion"] != 1:
        raise ValueError("Unsupported inventory formatVersion")
    if document["validation"] != "structure-only" or not isinstance(document["packages"], list):
        raise ValueError("Invalid inventory validation or packages")
    contracts = {package[1]: package for package in PACKAGES}
    result = {}
    for package in document["packages"]:
        if not isinstance(package, dict) or set(package) != {"packageId", "files"}:
            raise ValueError("Invalid package fields")
        identity = package["packageId"]
        if not isinstance(identity, str) or identity not in contracts or identity in result:
            raise ValueError("Unknown or duplicate package identity")
        if not isinstance(package["files"], list):
            raise ValueError("Invalid file inventory")
        _, _, assembly, definitions = contracts[identity]
        required, allowed = package_files(assembly, definitions)
        files = {}
        for file in package["files"]:
            if not isinstance(file, dict) or set(file) != {"path", "bytes", "sha256"}:
                raise ValueError("Invalid file fields")
            name = file["path"]
            # Only the fixed package inventory can become a filesystem path.
            if not isinstance(name, str) or name not in allowed or name in files:
                raise ValueError("Unknown, unsafe or duplicate file path")
            if type(file["bytes"]) is not int or file["bytes"] <= 0:
                raise ValueError("Invalid file byte count")
            if not isinstance(file["sha256"], str) or not re.fullmatch(r"[0-9a-f]{64}", file["sha256"]):
                raise ValueError("Invalid file digest")
            files[name] = file
        if not required <= files.keys():
            raise ValueError("Inventory omits required files")
        result[identity] = files
    if set(result) != set(contracts):
        raise ValueError("Inventory must describe all three known packages")
    return result


def verify_package(root, identity, expected):
    package = next(package for package in PACKAGES if package[1] == identity)
    errors, inventory = inspect_package(root, package, include_fingerprints=True)
    actual = {file["path"]: file for file in inventory["files"]}
    for name in sorted(expected.keys() - actual.keys()):
        errors.append(f"{name}: recorded file missing")
    for name in sorted(actual.keys() - expected.keys()):
        errors.append(f"{name}: file not recorded in inventory")
    for name in sorted(expected.keys() & actual.keys()):
        if actual[name] != expected[name]:
            errors.append(f"{name}: content differs from inventory")
    return errors


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("inventory", type=Path)
    parser.add_argument("package_id", choices=[package[1] for package in PACKAGES])
    parser.add_argument("folder", type=Path)
    args = parser.parse_args()
    try:
        inventory = read_inventory(args.inventory)
        errors = verify_package(args.folder.absolute(), args.package_id, inventory[args.package_id])
    except (OSError, ValueError) as error:
        print(f"Cannot verify package: {error}", file=sys.stderr)
        return 1
    if errors:
        print("\n".join(errors), file=sys.stderr)
        return 1
    print(f"Matches recorded bytes: {args.package_id}")
    print("Content comparison only; authenticity and game compatibility are not verified.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
