"""Tests for release.py. Run from the repository root:

    python -m unittest discover -s Tools~ -p "test_*.py"
"""

import json
import os
import re
import shutil
import subprocess
import sys
import tarfile
import tempfile
import unittest

sys.dont_write_bytecode = True
TOOLS = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(TOOLS)
SCRIPT = os.path.join(TOOLS, "release.py")
sys.path.insert(0, TOOLS)
import release  # noqa: E402


def run_script(*args):
    return subprocess.run([sys.executable, "-B", SCRIPT, *args],
                          capture_output=True, text=True)


def package_version():
    with open(os.path.join(REPO, "package.json"), encoding="utf-8") as f:
        return json.load(f)["version"]


def read_package(path):
    """Returns {guid: {name: bytes}} for the files in a .unitypackage."""
    result = {}
    with tarfile.open(path, "r:gz") as tar:
        for member in tar.getmembers():
            guid, _, name = member.name.rstrip("/").partition("/")
            entry = result.setdefault(guid, {})
            if member.isfile():
                entry[name] = tar.extractfile(member).read()
    return result


class BuildFromRepoTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.mkdtemp()
        cls.version = package_version()
        proc = run_script("build", "--version", cls.version, "--out", cls.tmp)
        if proc.returncode != 0:
            raise AssertionError(proc.stderr)
        cls.path = os.path.join(cls.tmp, f"SheetsToSO-{cls.version}.unitypackage")
        cls.package = read_package(cls.path)
        cls.by_path = {
            entry["pathname"].decode("utf-8"): (guid, entry)
            for guid, entry in cls.package.items()}

    @classmethod
    def tearDownClass(cls):
        shutil.rmtree(cls.tmp, ignore_errors=True)

    def test_file_name_has_version(self):
        self.assertTrue(os.path.isfile(self.path))

    def test_pathnames_match_tracked_meta_files(self):
        tracked = subprocess.run(["git", "ls-files"], cwd=REPO, check=True,
                                 capture_output=True, text=True).stdout.splitlines()
        expected = {
            "Assets/SheetsToSO/" + p[:-len(".meta")]
            for p in tracked
            if p.endswith(".meta") and p.split("/")[0] not in ("Tests", "Tests.meta")
            and not any(s.startswith(".") or s.endswith("~") for s in p.split("/"))}
        self.assertEqual(set(self.by_path), expected)

    def test_entries_match_repo_files(self):
        for pathname, (guid, entry) in self.by_path.items():
            with self.subTest(pathname=pathname):
                self.assertNotIn("\n", pathname)
                rel = pathname[len("Assets/SheetsToSO/"):]
                with open(os.path.join(REPO, rel + ".meta"), "rb") as f:
                    meta = f.read()
                self.assertEqual(entry["asset.meta"], meta)
                guid_in_meta = re.search(rb"^guid:\s*([0-9a-f]{32})", meta, re.M).group(1)
                self.assertEqual(guid, guid_in_meta.decode("ascii"))
                full = os.path.join(REPO, rel)
                if os.path.isdir(full):
                    self.assertNotIn("asset", entry)
                    self.assertEqual(set(entry), {"asset.meta", "pathname"})
                else:
                    with open(full, "rb") as f:
                        self.assertEqual(entry["asset"], f.read())
                    self.assertEqual(set(entry), {"asset", "asset.meta", "pathname"})

    def test_excluded_and_required_paths(self):
        for pathname in self.by_path:
            for bad in ("Cli~", "Tests~", "Documentation~", "Tools~", ".github", ".gitignore"):
                self.assertNotIn(bad, pathname)
            self.assertFalse(pathname.startswith("Assets/SheetsToSO/Tests"), pathname)
        for rel in ("package.json", "README.md", "README.ru.md", "CHANGELOG.md", "LICENSE",
                    "Runtime", "Runtime/Core", "Runtime/Core/Json", "Editor",
                    "Editor/Localization", "Runtime/Core/CsvReader.cs",
                    "Editor/Localization/EditorText.cs"):
            self.assertIn("Assets/SheetsToSO/" + rel, self.by_path)
        self.assertNotIn("Assets/SheetsToSO", self.by_path)

    def test_rebuild_gives_same_entries(self):
        other = tempfile.mkdtemp()
        try:
            proc = run_script("build", "--version", self.version, "--out", other)
            self.assertEqual(proc.returncode, 0, proc.stderr)
            again = read_package(os.path.join(other, os.path.basename(self.path)))
            self.assertEqual(again, self.package)
        finally:
            shutil.rmtree(other, ignore_errors=True)

    def test_wrong_version_fails(self):
        proc = run_script("build", "--version", "0.0.0-wrong", "--out", self.tmp)
        self.assertNotEqual(proc.returncode, 0)
        self.assertIn("package.json", proc.stderr)


class FixtureTests(unittest.TestCase):
    def setUp(self):
        self.root = tempfile.mkdtemp()
        self.out = tempfile.mkdtemp()
        self.write("package.json", '{"version": "1.2.3"}')
        self.write_meta("package.json", "a" * 32)
        self.write("Runtime/A.cs", "class A {}")
        self.write_meta("Runtime/A.cs", "b" * 32)
        self.write_meta("Runtime", "c" * 32, folder=True)
        self.write("Tests~/T.cs", "class T {}")
        self.write("CHANGELOG.md",
                   "# Changelog\n\n## [Unreleased]\n\n## [1.2.3] — 2026-01-01\n\n"
                   "### Added\n\n- Thing.\n\n## [1.0.0]\n\n- Old.\n")
        self.write_meta("CHANGELOG.md", "d" * 32)

    def tearDown(self):
        shutil.rmtree(self.root, ignore_errors=True)
        shutil.rmtree(self.out, ignore_errors=True)

    def write(self, rel, text):
        path = os.path.join(self.root, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)

    def write_meta(self, rel, guid, folder=False):
        text = f"fileFormatVersion: 2\nguid: {guid}\n"
        if folder:
            text += "folderAsset: yes\n"
        self.write(rel + ".meta", text)

    def git_add(self):
        subprocess.run(["git", "init", "-q"], cwd=self.root, check=True)
        subprocess.run(["git", "add", "-A"], cwd=self.root, check=True)

    def build(self, version="1.2.3"):
        return run_script("build", "--version", version, "--out", self.out,
                          "--root", self.root)

    def test_valid_fixture_builds(self):
        self.git_add()
        proc = self.build()
        self.assertEqual(proc.returncode, 0, proc.stderr)
        package = read_package(os.path.join(self.out, "SheetsToSO-1.2.3.unitypackage"))
        self.assertEqual(
            {e["pathname"].decode() for e in package.values()},
            {"Assets/SheetsToSO/package.json", "Assets/SheetsToSO/Runtime",
             "Assets/SheetsToSO/Runtime/A.cs", "Assets/SheetsToSO/CHANGELOG.md"})
        self.assertEqual(set(package), {"a" * 32, "b" * 32, "c" * 32, "d" * 32})

    def add_unity_tests(self):
        self.write("Tests/Editor/X.cs", "class X {}")
        self.write_meta("Tests/Editor/X.cs", "e" * 32)
        self.write_meta("Tests/Editor", "f" * 32, folder=True)
        self.write_meta("Tests", "1" * 32, folder=True)

    def test_unity_tests_are_not_packaged(self):
        self.add_unity_tests()
        self.git_add()
        proc = self.build()
        self.assertEqual(proc.returncode, 0, proc.stderr)
        package = read_package(os.path.join(self.out, "SheetsToSO-1.2.3.unitypackage"))
        self.assertEqual(
            {e["pathname"].decode() for e in package.values()},
            {"Assets/SheetsToSO/package.json", "Assets/SheetsToSO/Runtime",
             "Assets/SheetsToSO/Runtime/A.cs", "Assets/SheetsToSO/CHANGELOG.md"})
        self.assertEqual(set(package), {"a" * 32, "b" * 32, "c" * 32, "d" * 32})

    def test_unity_test_without_meta_is_ignored(self):
        self.add_unity_tests()
        self.write("Tests/Editor/NoMeta.cs", "class NoMeta {}")
        self.git_add()
        proc = self.build()
        self.assertEqual(proc.returncode, 0, proc.stderr)

    def test_file_without_meta_fails(self):
        self.write("Runtime/B.cs", "class B {}")
        self.git_add()
        proc = self.build()
        self.assertNotEqual(proc.returncode, 0)
        self.assertIn("Runtime/B.cs.meta", proc.stderr)

    def test_folder_without_meta_fails(self):
        os.remove(os.path.join(self.root, "Runtime.meta"))
        self.git_add()
        proc = self.build()
        self.assertNotEqual(proc.returncode, 0)
        self.assertIn("Runtime.meta", proc.stderr)

    def test_meta_without_file_fails(self):
        self.write_meta("Runtime/Gone.cs", "e" * 32)
        self.git_add()
        proc = self.build()
        self.assertNotEqual(proc.returncode, 0)
        self.assertIn("Runtime/Gone.cs.meta", proc.stderr)

    def test_duplicate_guid_fails(self):
        self.write("Runtime/B.cs", "class B {}")
        self.write_meta("Runtime/B.cs", "b" * 32)
        self.git_add()
        proc = self.build()
        self.assertNotEqual(proc.returncode, 0)
        self.assertIn("duplicate guid", proc.stderr)

    def test_version_mismatch_fails(self):
        self.git_add()
        proc = self.build("1.2.4")
        self.assertNotEqual(proc.returncode, 0)
        self.assertFalse(os.listdir(self.out))

    def test_untracked_file_is_ignored(self):
        self.git_add()
        self.write("Runtime/obj/Junk.cs", "junk")
        proc = self.build()
        self.assertEqual(proc.returncode, 0, proc.stderr)

    def test_notes_fixture(self):
        self.git_add()
        self.assertEqual(release.release_notes(self.root, "1.2.3"), "### Added\n\n- Thing.\n")
        with self.assertRaises(release.ReleaseError):
            release.release_notes(self.root, "1.0.0")  # version mismatch


class NotesFromRepoTests(unittest.TestCase):
    def test_notes_for_current_version(self):
        version = package_version()
        out = os.path.join(tempfile.mkdtemp(), "notes.md")
        try:
            proc = run_script("notes", "--version", version, "--out", out)
            self.assertEqual(proc.returncode, 0, proc.stderr)
            with open(out, encoding="utf-8") as f:
                text = f.read()
            self.assertIn("### Added", text)
            self.assertNotIn(f"## [{version}]", text)
            self.assertNotIn("## [", text)
        finally:
            shutil.rmtree(os.path.dirname(out), ignore_errors=True)

    def test_missing_section_fails(self):
        tmp = tempfile.mkdtemp()
        try:
            with open(os.path.join(tmp, "package.json"), "w", encoding="utf-8") as f:
                f.write('{"version": "9.9.9"}')
            with open(os.path.join(tmp, "CHANGELOG.md"), "w", encoding="utf-8") as f:
                f.write("# Changelog\n\n## [Unreleased]\n\n## [9.9.8]\n\n- Old.\n")
            with self.assertRaises(release.ReleaseError):
                release.release_notes(tmp, "9.9.9")
            proc = run_script("notes", "--version", "9.9.9", "--out",
                              os.path.join(tmp, "n.md"), "--root", tmp)
            self.assertNotEqual(proc.returncode, 0)
        finally:
            shutil.rmtree(tmp, ignore_errors=True)

    def test_empty_section_fails(self):
        tmp = tempfile.mkdtemp()
        try:
            with open(os.path.join(tmp, "package.json"), "w", encoding="utf-8") as f:
                f.write('{"version": "9.9.9"}')
            with open(os.path.join(tmp, "CHANGELOG.md"), "w", encoding="utf-8") as f:
                f.write("# Changelog\n\n## [9.9.9]\n\n## [9.9.8]\n\n- Old.\n")
            with self.assertRaises(release.ReleaseError):
                release.release_notes(tmp, "9.9.9")
        finally:
            shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    unittest.main()
