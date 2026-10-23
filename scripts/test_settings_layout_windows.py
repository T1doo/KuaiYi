"""Offline scope/integrity rejections. These are not Windows raster tests."""
import importlib.util
import base64
import copy
import gzip
import hashlib
import json
import os
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location("settings_ci", Path(__file__).with_name("verify-settings-layout-windows.py"))
M = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(M)


class SettingsCiContracts(unittest.TestCase):
    def fixture(self, root):
        project = "tools/KuaiYi.GrowthHarness/KuaiYi.GrowthHarness.csproj"
        paths = [root / project, *(root / "src" / name / (name + ".csproj") for name in M.PROJECTS)]
        for path in paths:
            path.parent.joinpath("obj").mkdir(parents=True)
            assets = {"project": {"restore": {"projectUniqueName": str(path)}, "frameworks": {"net10.0": {}}},
                      "libraries": {}, "targets": {"net10.0": {}}}
            if path == root / project:
                assets["libraries"] = {name + "/1.0.0": {"type": "project"} for name in M.PROJECTS}
                assets["targets"]["net10.0"] = dict(assets["libraries"])
            path.parent.joinpath("obj/project.assets.json").write_text(json.dumps(assets))
            path.parent.joinpath("packages.lock.json").write_text(json.dumps({"dependencies": {"net10.0": {}}}))
        return project

    def check_mutation(self, mutate, target="obj/project.assets.json"):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            project = self.fixture(root)
            path = (root / project).parent / target
            data = json.loads(path.read_text())
            mutate(data)
            path.write_text(json.dumps(data))
            with self.assertRaises(RuntimeError): M.zero_package_closure(root, project)

    def test_project_only_closure_and_exact_count(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.assertEqual(9, len(M.zero_package_closure(root, self.fixture(root))))

    def test_package_library_rejected(self):
        self.check_mutation(lambda d: d["libraries"].update({"Bad/1": {"type": "package"}}))

    def test_direct_package_request_rejected(self):
        self.check_mutation(lambda d: d["project"]["frameworks"]["net10.0"].update(dependencies={"Bad": {}}))

    def test_lock_package_rejected(self):
        self.check_mutation(lambda d: d["dependencies"]["net10.0"].update({"bad": {"type": "Direct"}}), "packages.lock.json")

    def test_foreign_project_identity_rejected(self):
        self.check_mutation(lambda d: d["project"]["restore"].update(projectUniqueName="foreign.csproj"))

    def test_unknown_project_library_rejected(self):
        def mutation(d):
            d["libraries"]["Unknown/1"] = {"type": "project"}
            d["targets"]["net10.0"]["Unknown/1"] = {"type": "project"}
        self.check_mutation(mutation)

    def test_target_inventory_mismatch_rejected(self):
        self.check_mutation(lambda d: d["targets"]["net10.0"].update({"KuaiYi.Core/1": {"type": "project"}}))

    def test_missing_project_closure_rejected(self):
        def mutation(d):
            d["libraries"].clear()
            for target in d["targets"].values(): target.clear()
        self.check_mutation(mutation)

    def test_hosted_context_no_rerun_or_private(self):
        env = {"GITHUB_ACTIONS": "true", "GITHUB_REPOSITORY": "T1doo/KuaiYi", "GITHUB_REF": M.BRANCH,
               "GITHUB_EVENT_NAME": "push", "GITHUB_RUN_ATTEMPT": "1", "RUNNER_OS": "Windows", "GITHUB_SHA": "a" * 40}
        M.hosted_context(env, "win32", {"repository": {"private": False}})
        for changed, platform, event in [(dict(env, GITHUB_RUN_ATTEMPT="2"), "win32", {"repository": {"private": False}}),
                                         (env, "win32", {"repository": {"private": True}}),
                                         (env, "linux", {"repository": {"private": False}})]:
            with self.assertRaises(RuntimeError): M.hosted_context(changed, platform, event)

    def test_exact_source_scope_and_clean_tree(self):
        values = {"source-head": "a" * 40, "source-parent": "a" * 40 + " " + M.PARENT,
                  "source-scope": "\n".join(M.FILES), "source-clean": "", "fixed-product-source": ""}
        run = lambda argv, name, timeout: values[name]
        self.assertEqual("a" * 40, M.source_gate(run, "a" * 40))
        for name, replacement in [("source-parent", "a" * 40 + " " + "b" * 40), ("source-scope", "wrong"),
                                  ("source-clean", " M unexpected"), ("fixed-product-source", "src/changed.cs")]:
            old = values[name]; values[name] = replacement
            with self.assertRaises(RuntimeError): M.source_gate(run, "a" * 40)
            values[name] = old


class OverlayEvidenceIntegrationTests(unittest.TestCase):
    def entry(self, root, name, data):
        (root / name).write_bytes(data)
        return {"Name": name, "Log": name, "LogBytes": len(data), "LogSha256": hashlib.sha256(data).hexdigest()}

    def test_complete_frozen_set_roundtrips_and_later_disk_change_cannot_change_emission(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            entries = [self.entry(root, "result.json", b'{"source":"synthetic"}'), self.entry(root, "frame.png", b"synthetic-protocol-bytes")]
            prepared, raw, packed = M.prepare_evidence(root, entries, lambda p: p.is_symlink())
            (root / "frame.png").write_bytes(b"later-change")
            lines = []; M.emit_prepared(prepared, lines.append)
            self.assertEqual(3, len(prepared))  # Includes the manifest in both aggregate limits.
            self.assertEqual(raw, sum(h["Bytes"] for h, _ in prepared))
            self.assertEqual(packed, sum(h["GzipBytes"] for h, _ in prepared))
            originals = {}
            for header, chunks in prepared:
                zipped = base64.b64decode("".join(chunks), validate=True)
                self.assertEqual(header["GzipSha256"], hashlib.sha256(zipped).hexdigest())
                original = gzip.decompress(zipped)
                self.assertEqual(header["Sha256"], hashlib.sha256(original).hexdigest())
                originals[header["Name"]] = original
            self.assertEqual(b"synthetic-protocol-bytes", originals["frame.png"])
            self.assertEqual([e["Name"] for e in entries], [e["Name"] for e in json.loads(originals["evidence-manifest.json"])])
            self.assertTrue(all(line.startswith(("KUAIYI_CI_FILE ", "KUAIYI_CI_CHUNK ")) for line in lines))

    def test_late_raw_gzip_and_manifest_overflow_emit_no_prefix(self):
        for raw_limit, gzip_limit, data in ((1024, 4096, b"x"*1100), (4096, 80, os.urandom(300)), (100, 4096, b"x")):
            with self.subTest(raw=raw_limit, gzip=gzip_limit), tempfile.TemporaryDirectory() as temp:
                root = Path(temp); lines = []
                entries = [self.entry(root, "first.log", b"first"), self.entry(root, "last.log", data)]
                with self.assertRaises(RuntimeError):
                    prepared, _, _ = M.prepare_evidence(root, entries, lambda p: p.is_symlink(), _raw_limit=raw_limit, _gzip_limit=gzip_limit)
                    M.emit_prepared(prepared, lines.append)
                self.assertEqual([], lines)
                self.assertFalse((root / "evidence-manifest.json").exists())
                self.assertTrue((root / "last.log").exists())

    def test_registered_missing_changed_duplicate_and_linked_files_are_incomplete(self):
        for fault in ("missing", "changed", "name", "path", "link"):
            with self.subTest(fault=fault), tempfile.TemporaryDirectory() as temp:
                root = Path(temp); entry = self.entry(root, "original.log", b"public")
                entries = [entry]; lines = []
                if fault == "missing": (root / "original.log").unlink()
                elif fault == "changed": (root / "original.log").write_bytes(b"changed")
                elif fault == "name": entries.append(dict(entry))
                elif fault == "path": entries.append(dict(entry, Name="alias.log"))
                else:
                    (root / "target").write_bytes(b"public"); (root / "original.log").unlink(); (root / "original.log").symlink_to(root / "target")
                with self.assertRaises(RuntimeError):
                    prepared, _, _ = M.prepare_evidence(root, entries, lambda p: p.is_symlink())
                    M.emit_prepared(prepared, lines.append)
                self.assertEqual([], lines)

    def test_transport_failure_propagates_even_when_prefix_was_written(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); entry = self.entry(root, "original.log", b"public")
            prepared, _, _ = M.prepare_evidence(root, [entry], lambda p: p.is_symlink())
            lines = []
            def emit(line):
                if lines: raise BrokenPipeError("original transport")
                lines.append(line)
            with self.assertRaises(BrokenPipeError): M.emit_prepared(prepared, emit)
            self.assertEqual(1, len(lines))

    def fixture(self, root):
        sha = "a"*40
        names = ["SeedOpen", "LongStreamGrow", "Close", "Reopen", "CloseUntilReverse", "ReverseToOpen", "CloseToCompact", "OpenUntilReverse", "ReverseToClose"]
        actual = {"Phases": [{"Id": i+1, "Mode": "Timing" if i < 9 else "Raster", "Name": names[i%9], "CompletedQpc": i+100, "SawIntermediate": True} for i in range(18)], "RawCallbacks": 18, "EvidenceFiles": []}
        raw = json.dumps([{"Sequence": i+1} for i in range(18)]).encode()
        (root / "overlay-raw-frames.json").write_bytes(raw)
        # Valid tiny synthetic PNG for protocol checks only, never a native UI sample.
        png = base64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aN1sAAAAASUVORK5CYII=")
        pairs = [(phase, kind) for phase in range(10, 19) for kind in ("intermediate", "terminal") if kind != "terminal" or phase not in (14, 17)]
        for ordinal, (phase, kind) in enumerate(pairs, 1):
            name = f"overlay-{ordinal:02}-phase{phase}-{kind}.png"; (root / name).write_bytes(png)
            actual["EvidenceFiles"].append({"File": name, "Before": {"Frame": {"Phase": phase, "Mode": "Raster"}}, "After": {"Phase": phase, "Mode": "Raster"}, "Kind": kind,
                                             "Stable": True, "Bytes": len(png), "Sha256": hashlib.sha256(png).hexdigest(), "PixelWidth": 1, "PixelHeight": 1})
        receipt = {"Status": "Pass_LimitedProcess", "SourceHead": sha, "CleanupConfirmed": True, "OriginalExitConfirmed": True, "ExitCode": 0, "OriginalResult": {"Actual": actual}}
        self.write_result(root, receipt)
        return receipt, sha

    def write_result(self, root, receipt):
        data = json.dumps(receipt["OriginalResult"]["Actual"]).encode(); (root / "overlay-stages-result.json").write_bytes(data)
        receipt["OriginalResult"].update(Bytes=len(data), Sha256=hashlib.sha256(data).hexdigest())

    def verify(self, root, receipt, sha):
        return M.verify_overlay_result(receipt, root, sha, lambda p: hashlib.sha256(p.read_bytes()).hexdigest(), lambda p: p.is_symlink())

    def test_exact_stage_raw_and_image_collection_and_compact_receipt(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); receipt, sha = self.fixture(root)
            observed = self.verify(root, receipt, sha)
            self.assertEqual((18, 18, 16), (observed["ObservedPhases"], observed["RawCallbacks"], observed["Rasters"]))
            compact = M.compact_process_receipt(receipt)
            self.assertNotIn("Actual", compact["OriginalResult"])
            self.assertIn("Actual", receipt["OriginalResult"])
            self.assertEqual(receipt["OriginalResult"]["Sha256"], compact["OriginalResult"]["Sha256"])

    def test_stage_source_missing_reused_changed_and_misbound_rasters_are_rejected(self):
        for fault in ("source", "stage", "intermediate", "result", "raw", "missing", "reused", "misbound", "dimensions", "extra"):
            with self.subTest(fault=fault), tempfile.TemporaryDirectory() as temp:
                root = Path(temp); receipt, sha = self.fixture(root); actual = receipt["OriginalResult"]["Actual"]; first = actual["EvidenceFiles"][0]
                if fault == "source": receipt["SourceHead"] = "b"*40
                elif fault == "stage": actual["Phases"][0]["Name"] = "wrong"
                elif fault == "intermediate": actual["Phases"][0]["SawIntermediate"] = False
                elif fault == "raw": actual["RawCallbacks"] = 17
                elif fault == "missing": (root / first["File"]).unlink()
                elif fault == "reused": actual["EvidenceFiles"][1]["File"] = first["File"]
                elif fault == "misbound": first["After"]["Phase"] = 99
                elif fault == "dimensions": first["PixelWidth"] = 2
                elif fault == "extra": (root / "unexpected.png").write_bytes(b"neutral")
                self.write_result(root, receipt)
                if fault == "result": (root / "overlay-stages-result.json").write_bytes(b"{}");
                with self.assertRaises((RuntimeError, OSError)): self.verify(root, receipt, sha)


if __name__ == "__main__": unittest.main()
