#!/usr/bin/env python3
"""Release helpers for the Sheets to ScriptableObject package.

Commands (run from the repository root):

    python Tools~/release.py build --version 0.4.0 --out dist/
        Builds dist/SheetsToSO-0.4.0.unitypackage from the tracked package files.

    python Tools~/release.py notes --version 0.4.0 --out dist/notes.md
        Writes the body of the "## [0.4.0]" section of CHANGELOG.md.

Both commands fail if --version differs from "version" in package.json.
Only the Python standard library is used.
"""

import argparse
import gzip
import io
import json
import os
import re
import subprocess
import sys
import tarfile

INSTALL_ROOT = "Assets/SheetsToSO"
PACKAGE_FILE_NAME = "SheetsToSO-{version}.unitypackage"
FIXED_MTIME = 315532800  # 1980-01-01, the same for every build
GUID_RE = re.compile(r"^guid:\s*([0-9a-fA-F]{32})\s*$", re.MULTILINE)
DEV_ONLY_ROOTS = ("Tests",)  # package development only, not for Assets/ of a user
DEFAULT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


class ReleaseError(Exception):
    pass


def check_version(root, version):
    path = os.path.join(root, "package.json")
    try:
        with open(path, encoding="utf-8") as f:
            actual = json.load(f).get("version")
    except (OSError, ValueError) as e:
        raise ReleaseError(f"cannot read package.json: {e}")
    if actual != version:
        raise ReleaseError(
            f"--version {version} does not match package.json version {actual}")


def is_excluded(path):
    """Whether a tracked path stays out of the .unitypackage: hidden (".*"), Unity-ignored ("*~")
    and development-only top-level folders (DEV_ONLY_ROOTS, e.g. the Unity tests in Tests/),
    with their own .meta (Tests.meta). Excluded paths are not checked for .meta files."""
    parts = path.split("/")
    top = parts[0][:-len(".meta")] if len(parts) == 1 and parts[0].endswith(".meta") else parts[0]
    if top in DEV_ONLY_ROOTS:
        return True
    return any(part.startswith(".") or part.endswith("~") for part in parts)


def tracked_files(root):
    try:
        out = subprocess.run(
            ["git", "-c", "core.quotepath=off", "ls-files", "-z"],
            cwd=root, check=True, capture_output=True).stdout
    except (OSError, subprocess.CalledProcessError) as e:
        raise ReleaseError(f"git ls-files failed: {e}")
    return [p for p in out.decode("utf-8").split("\0") if p]


def read_guid(root, meta_path):
    with open(os.path.join(root, meta_path), encoding="utf-8-sig") as f:
        match = GUID_RE.search(f.read())
    if not match:
        raise ReleaseError(f"no guid in {meta_path}")
    return match.group(1).lower()


def collect_assets(root):
    """Returns a sorted list of (path, guid, is_folder) for the package."""
    files = [p for p in tracked_files(root) if not is_excluded(p)]
    metas = {p[:-len(".meta")] for p in files if p.endswith(".meta")}
    assets = sorted(p for p in files if not p.endswith(".meta"))
    asset_set = set(assets)

    folders = set()
    for path in assets:
        parts = path.split("/")[:-1]
        for i in range(1, len(parts) + 1):
            folders.add("/".join(parts[:i]))

    errors = []
    for path in sorted(asset_set | folders):
        if path not in metas:
            errors.append(f"missing meta: {path}.meta")
    for path in sorted(metas):
        if path not in asset_set and path not in folders:
            errors.append(f"meta without file or folder: {path}.meta")
    if errors:
        raise ReleaseError("\n".join(errors))

    result = []
    seen = {}
    for path in sorted(asset_set | folders):
        guid = read_guid(root, path + ".meta")
        if guid in seen:
            errors.append(f"duplicate guid {guid}: {seen[guid]}.meta, {path}.meta")
        seen[guid] = path
        result.append((path, guid, path in folders))
    if errors:
        raise ReleaseError("\n".join(errors))
    return result


def _add(tar, name, data=None):
    info = tarfile.TarInfo(name)
    info.mtime = FIXED_MTIME
    info.uid = info.gid = 0
    info.uname = info.gname = ""
    if data is None:
        info.type = tarfile.DIRTYPE
        info.mode = 0o755
        tar.addfile(info)
    else:
        info.mode = 0o644
        info.size = len(data)
        tar.addfile(info, io.BytesIO(data))


def build(root, version, out_dir):
    check_version(root, version)
    assets = collect_assets(root)

    entries = []
    for path, guid, is_folder in assets:
        with open(os.path.join(root, path + ".meta"), "rb") as f:
            meta = f.read()
        entries.append((guid, "asset.meta", meta))
        entries.append((guid, "pathname", f"{INSTALL_ROOT}/{path}".encode("utf-8")))
        if not is_folder:
            with open(os.path.join(root, path), "rb") as f:
                entries.append((guid, "asset", f.read()))
    entries.sort(key=lambda e: (e[0], e[1]))

    os.makedirs(out_dir, exist_ok=True)
    out_path = os.path.join(out_dir, PACKAGE_FILE_NAME.format(version=version))
    with open(out_path, "wb") as raw:
        with gzip.GzipFile(filename="", mode="wb", fileobj=raw, mtime=0) as gz:
            with tarfile.open(fileobj=gz, mode="w", format=tarfile.USTAR_FORMAT) as tar:
                last_guid = None
                for guid, name, data in entries:
                    if guid != last_guid:
                        _add(tar, guid + "/")
                        last_guid = guid
                    _add(tar, f"{guid}/{name}", data)
    return out_path


def release_notes(root, version):
    check_version(root, version)
    with open(os.path.join(root, "CHANGELOG.md"), encoding="utf-8-sig") as f:
        lines = f.read().splitlines()
    header = f"## [{version}]"
    start = next((i for i, line in enumerate(lines) if line.startswith(header)), None)
    if start is None:
        raise ReleaseError(f"no '{header}' section in CHANGELOG.md")
    end = next((i for i in range(start + 1, len(lines)) if lines[i].startswith("## [")),
               len(lines))
    body = "\n".join(lines[start + 1:end]).strip()
    if not body:
        raise ReleaseError(f"'{header}' section in CHANGELOG.md is empty")
    return body + "\n"


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    sub = parser.add_subparsers(dest="command", required=True)
    for name in ("build", "notes"):
        p = sub.add_parser(name)
        p.add_argument("--version", required=True)
        p.add_argument("--out", required=True)
        p.add_argument("--root", default=DEFAULT_ROOT, help="repository root")
    args = parser.parse_args(argv)

    try:
        if args.command == "build":
            print(build(args.root, args.version, args.out))
        else:
            notes = release_notes(args.root, args.version)
            parent = os.path.dirname(args.out)
            if parent:
                os.makedirs(parent, exist_ok=True)
            with open(args.out, "w", encoding="utf-8", newline="\n") as f:
                f.write(notes)
            print(args.out)
    except ReleaseError as e:
        print(f"error: {e}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
