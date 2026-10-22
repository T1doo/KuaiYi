"""Only offline component provenance helpers; synthetic archives never qualify an installer or source pins."""
import copy
import hashlib
import importlib.util
from pathlib import Path
import tempfile
import unittest
import zipfile

spec = importlib.util.spec_from_file_location("payload_inputs", Path(__file__).with_name("inspect-windows-payload.py"))
payload = importlib.util.module_from_spec(spec)
spec.loader.exec_module(payload)


class PayloadInputTests(unittest.TestCase):
    def test_paths_unknown_sources_case_aliases_and_bounds_are_refused(self):
        source = {"package": "skiasharp", "member": "lib/net10.0/SkiaSharp.dll", "archiveHash": payload.SKIA["skiasharp"]}
        rows = []
        payload.record(rows, "Generation", "SkiaSharp.dll", 10, "a" * 64, "SkiaPackage", source, "4.153.1")
        for target in ("../other.dll", "/other.dll", "dir\\file", "C:file", "CON.txt", "file.", "dir//file", "x" * 241, "skiasharp.DLL"):
            with self.subTest(target=target), self.assertRaises(RuntimeError):
                payload.record(rows, "Generation", target, 10, "b" * 64, "SkiaPackage", source, "4.153.1")
        for kind, version, metadata in [("Fixture", "4.153.1", source), ("SkiaPackage", "4.999.0", source), ("SkiaPackage", "4.153.1", {**source, "package": "PUBLIC_UNKNOWN"}), ("SkiaPackage", "4.153.1", {**source, "member": "../bad"}), ("ManagedBuildInput", "1.0.0", {"path": "src/role.dll", "qualification": "Pass"})]:
            with self.subTest(kind=kind, version=version, metadata=metadata), self.assertRaises(RuntimeError):
                payload.record([], "Generation", "other.dll", 10, "b" * 64, kind, metadata, version)
        for size in (0, -1, 256 * 1024 * 1024 + 1):
            with self.subTest(size=size), self.assertRaises(RuntimeError):
                payload.record([], "Generation", "other.dll", size, "b" * 64, "SkiaPackage", source, "4.153.1")
        full = [copy.copy(rows[0]) for _ in range(4096)]
        with self.assertRaises(RuntimeError):
            payload.record(full, "Generation", "other.dll", 10, "b" * 64, "SkiaPackage", source, "4.153.1")

    def test_approved_archive_snapshot_is_not_replaced_by_later_source_mutation(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "public-fixture.zip"
            with zipfile.ZipFile(path, "w") as z:
                z.writestr("member.bin", b"PUBLIC_ORIGINAL_INPUT")
            expected = hashlib.sha256(path.read_bytes()).hexdigest()
            with payload.verified_zip(path, expected, "sha256") as held:
                with zipfile.ZipFile(path, "w") as z:
                    z.writestr("member.bin", b"PUBLIC_CHANGED_INPUT")
                self.assertEqual(b"PUBLIC_ORIGINAL_INPUT", held.read("member.bin"))
            with self.assertRaises(RuntimeError):
                payload.verified_zip(path, expected, "sha256")

    def test_output_traversal_outside_links_and_existing_results_are_refused(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "artifacts").mkdir()
            (root / "src").mkdir()
            link = root / "artifacts/link"
            link.symlink_to(root / "src", target_is_directory=True)
            for candidate in (root / "artifacts/../src/new.json", root / "src/new.json", link / "new.json"):
                with self.subTest(candidate=str(candidate)), self.assertRaises(RuntimeError):
                    payload.checked_output(root, candidate)
            fresh = payload.checked_output(root, root / "artifacts/new.json")
            with fresh.open("x") as stream:
                stream.write("PUBLIC_RESULT")
            with self.assertRaises(RuntimeError):
                payload.checked_output(root, fresh)
            self.assertFalse((root / "src/new.json").exists())

    def test_current_obj_does_not_authenticate_old_or_replaced_binary(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            path = root / "src/role.dll"
            path.parent.mkdir()
            info = root / "AssemblyInfo.cs"
            head = "a" * 40
            info.write_text('AssemblyInformationalVersionAttribute("1.0.0+' + head + '")')
            for raw in (b"PUBLIC_OLD_BINARY", b"PUBLIC_REPLACED_BINARY"):
                path.write_bytes(raw)
                rows = []
                payload.observed_build_record(rows, root, path, info, "Generation", "OrdinaryBuild_NotSelfContainedPublish", head)
                self.assertEqual("ObservedBuildDirectoryInput", rows[0]["sourceKind"])
                self.assertIsNone(rows[0]["sourceRevision"])
                self.assertEqual("Unverified", rows[0]["version"])
                self.assertIs(rows[0]["source"]["revisionVerified"], False)
                self.assertEqual(head, rows[0]["source"]["declaredBuildRevision"])
                self.assertEqual(hashlib.sha256(raw).hexdigest(), rows[0]["sha256"])
                with self.assertRaises(RuntimeError):
                    payload.record([], "Generation", path.name, len(raw), rows[0]["sha256"], rows[0]["sourceKind"], rows[0]["source"], "1.0.0", head)

    def test_link_zero_byte_and_non_file_inputs_are_refused(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            path = root / "input.bin"
            path.write_bytes(b"PUBLIC_INPUT")
            link = root / "link.bin"
            link.symlink_to(path)
            for candidate in (link, root):
                with self.subTest(candidate=candidate.name), self.assertRaises(RuntimeError):
                    payload.file_hash(candidate)
            path.write_bytes(b"")
            with self.assertRaises(RuntimeError):
                payload.file_hash(path)


if __name__ == "__main__":
    unittest.main()
