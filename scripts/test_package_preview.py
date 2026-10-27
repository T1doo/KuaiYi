"""Synthetic archive/merge tests; fixtures are never runnable product artifacts."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

spec = importlib.util.spec_from_file_location("package_preview", Path(__file__).with_name("package-preview.py"))
p = importlib.util.module_from_spec(spec)
spec.loader.exec_module(p)


class PreviewTests(unittest.TestCase):
    @staticmethod
    def publish_fixture(root):
        (root / "scripts").mkdir()
        (root / "scripts/publish-windows-managed.py").write_bytes(b"PUBLIC_SYNTHETIC_PUBLISHER")
        out = root / "publish"
        out.mkdir()
        (out / "publisher-source.py").write_bytes(b"PUBLIC_SYNTHETIC_PUBLISHER")
        for n in ("sources-before.json", "sources-after.json"):
            (out / n).write_text("[]")
        roles = []
        for role in p.publisher.ROLES:
            folder = out / "roles" / role
            folder.mkdir(parents=True)
            (folder / "synthetic.txt").write_bytes(b"NOT_A_RUNNABLE_PAYLOAD")
            roles.append({"role": role, "files": p.publisher.manifest(folder)})
        report = {"kind": "UnsealedManagedWindowsPublish", "status": "Pass_UnsealedManagedInputsOnly",
                  "productionPins": "Unavailable", "windowsExecution": "NotRun", "sourcesUnchanged": True,
                  "sdk": "10.0.402", "runtime": "10.0.13", "sourceHead": "a" * 40,
                  "publisherSha256": p.digest(out / "publisher-source.py"),
                  "commands": [{"exit": 0, "settlement": "LeaderJoined_GroupEmpty"} for _ in range(16)], "roles": roles}
        report["skiaAudits"] = [{"package": name, "exit": 0, "settlement": "LeaderJoined_GroupEmpty", "lockedContentHashVerified": True} for name in ["SkiaSharp", "SkiaSharp.NativeAssets.Win32"]]
        (out / "result.json").write_text(json.dumps(report))
        return out, report

    def test_complete_original_inventory_and_source_binding_are_required(self):
        with tempfile.TemporaryDirectory() as d, patch.object(p.publisher, "product_sources", return_value=[]), patch.object(p.subprocess, "run"):
            root = Path(d)
            out, _ = self.publish_fixture(root)
            with patch.object(p, "REPO", root):
                self.assertEqual(8, len(p.validate_publish(out)[1]))
                (out / "sources-after.json").write_text('[{"changed":true}]')
                with self.assertRaises(ValueError):
                    p.validate_publish(out)

    def test_unsettled_build_and_modified_members_cannot_be_repackaged(self):
        with tempfile.TemporaryDirectory() as d, patch.object(p.publisher, "product_sources", return_value=[]), patch.object(p.subprocess, "run"):
            root = Path(d)
            out, report = self.publish_fixture(root)
            with patch.object(p, "REPO", root):
                report["commands"][0]["settlement"] = "Unknown"
                (out / "result.json").write_text(json.dumps(report))
                with self.assertRaises(ValueError):
                    p.validate_publish(out)
                report["commands"][0]["settlement"] = "LeaderJoined_GroupEmpty"
                (out / "result.json").write_text(json.dumps(report))
                (out / "roles/App/unlisted.txt").write_bytes(b"NOT_APPROVED")
                with self.assertRaises(ValueError):
                    p.validate_publish(out)

    def test_another_publisher_cannot_be_substituted_with_self_declared_digest(self):
        with tempfile.TemporaryDirectory() as d, patch.object(p.publisher, "product_sources", return_value=[]), patch.object(p.subprocess, "run"):
            root = Path(d)
            out, report = self.publish_fixture(root)
            (out / "publisher-source.py").write_bytes(b"DIFFERENT_PUBLISHER")
            report["publisherSha256"] = p.digest(out / "publisher-source.py")
            (out / "result.json").write_text(json.dumps(report))
            with patch.object(p, "REPO", root), self.assertRaises(ValueError):
                p.validate_publish(out)

    def test_reproducible_zip_and_readback_preserve_binary_bytes(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            source = root / "input"
            raw = bytes(range(256)) * 5
            source.write_bytes(raw)
            rows = [("app/test.dll", source, len(raw), hashlib.sha256(raw).hexdigest())]
            for n in ("a.zip", "b.zip"):
                p.write_zip(root / n, rows, {"manifest.json": b'{"fixture":true}\n'})
            self.assertEqual((root / "a.zip").read_bytes(), (root / "b.zip").read_bytes())
            with zipfile.ZipFile(root / "a.zip") as z:
                self.assertEqual(raw, z.read("app/test.dll"))
            self.assertEqual(raw, source.read_bytes())

    def test_windows_paths_aliases_and_file_directory_conflicts_refused(self):
        for path in ("../escape", "a/../b", "/absolute", "a\\b", "CON.txt", "a./x", "a//b", "a\nb", "a/./b", "a:b"):
            with self.subTest(path=path), self.assertRaises((ValueError, RuntimeError)):
                p.name(path)
        for names in (["app/X.dll", "app/x.dll"], ["app/file", "app/file/child"], ["manifest.json", "manifest.json"]):
            with self.subTest(names=names), self.assertRaises(ValueError):
                p.unique_members(names)

    def test_hash_mismatch_retains_failed_archive_and_original(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            source = root / "original"
            source.write_bytes(b"PUBLIC_FIXTURE")
            with self.assertRaises(ValueError):
                p.write_zip(root / "failed.zip", [("app/f.dll", source, 14, "0" * 64)], {})
            self.assertTrue((root / "failed.zip").exists())
            self.assertEqual(b"PUBLIC_FIXTURE", source.read_bytes())

    def test_input_change_during_write_refused_without_rewriting_original(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            source = root / "original"
            source.write_bytes(b"PUBLIC_FIXTURE")
            sha = p.digest(source)
            with patch.object(p, "digest", side_effect=[sha, "0" * 64]), self.assertRaises(ValueError):
                p.write_zip(root / "failed.zip", [("app/f.dll", source, source.stat().st_size, sha)], {})
            self.assertEqual(b"PUBLIC_FIXTURE", source.read_bytes())

    def test_link_hardlink_duplicate_json_and_output_reuse_refused(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            original = root / "original"
            original.write_text('{"x":1,"x":2}')
            with self.assertRaises(ValueError):
                p.document(original)
            link = root / "link"
            link.symlink_to(original)
            with self.assertRaises(ValueError):
                p.regular(link)
            hard = root / "hard"
            os.link(original, hard)
            with self.assertRaises(ValueError):
                p.regular(original)
            target = root / "existing.zip"
            target.write_bytes(b"KEEP")
            with self.assertRaises(FileExistsError):
                p.write_zip(target, [], {})
            self.assertEqual(b"KEEP", target.read_bytes())

    def test_count_and_total_bounds_fail_before_archive_creation(self):
        with tempfile.TemporaryDirectory() as d:
            target = Path(d) / "out.zip"
            with patch.object(p, "MAX_FILES", 0), self.assertRaises(ValueError):
                p.write_zip(target, [], {"manifest.json": b"x"})
            with patch.object(p, "MAX_BYTES", 0), self.assertRaises(ValueError):
                p.write_zip(target, [], {"manifest.json": b"x"})
            self.assertFalse(target.exists())

    def test_only_explicit_preview_version_is_accepted_before_any_io(self):
        for version in ("1.0.0", "0.1.0", "../release", "0.1.0-preview.20261027\n"):
            with self.subTest(version=version), self.assertRaises(ValueError):
                p.package(Path("absent"), Path("absent"), version)

    @staticmethod
    def runtime():
        return [{"Path": "WindowsBase.dll", "Id": p.publisher.INPUTS.RUNTIME.RUNTIME_IDS[1], "Version": "10.0.13",
                 "Sha256": "d" * 64, "Overrides": {"Id": p.publisher.INPUTS.RUNTIME.RUNTIME_IDS[0], "Sha256": "c" * 64}}]

    def test_precise_runtime_override_records_selected_and_replaced_roles(self):
        rows = [("managed-inputs/NativeHost/WindowsBase.dll", Path("stub"), 1, "c" * 64),
                ("managed-inputs/App/WindowsBase.dll", Path("wpf"), 2, "d" * 64),
                ("managed-inputs/CaptureHost/WindowsBase.dll", Path("stub2"), 1, "c" * 64),
                ("managed-inputs/App/KuaiYi.Core.pdb", Path("pdb"), 3, "e" * 64)]
        for source in (rows, list(reversed(rows))):
            entries, provenance = p.flatten(source, self.runtime())
            selected = next(x for x in entries if x[0] == "app/WindowsBase.dll")
            self.assertEqual("d" * 64, selected[3])
            record = next(x for x in provenance if x["path"] == selected[0])
            self.assertEqual(["App"], record["selectedSourceRoles"])
            self.assertEqual({"NativeHost", "CaptureHost"}, {x["role"] for x in record["replacedSources"]})
            self.assertTrue(any(x[0] == "symbols/app/KuaiYi.Core.pdb" for x in entries))

    def test_unknown_runtime_collision_and_weak_override_contract_fail_closed(self):
        rows = [("managed-inputs/App/WindowsBase.dll", Path("a"), 1, "d" * 64),
                ("managed-inputs/NativeHost/WindowsBase.dll", Path("b"), 2, "f" * 64)]
        with self.assertRaises(ValueError):
            p.flatten(rows, self.runtime())
        bad = self.runtime()
        bad[0]["Version"] = "10.0.12"
        with self.assertRaises(ValueError):
            p.flatten([], bad)
        rows = [("managed-inputs/App/foreign.dll", Path("a"), 1, "a" * 64),
                ("managed-inputs/NativeHost/foreign.dll", Path("b"), 2, "b" * 64)]
        with self.assertRaises(ValueError):
            p.flatten(rows, self.runtime())


if __name__ == "__main__":
    unittest.main()
