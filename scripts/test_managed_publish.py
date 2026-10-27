"""Local non-destructive publisher checks. Synthetic files never qualify product inputs."""
import importlib.util
import base64
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import unittest
import zipfile
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("managed_publish", Path(__file__).with_name("publish-windows-managed.py"))
publisher = importlib.util.module_from_spec(spec)
spec.loader.exec_module(publisher)


class ManagedPublishTests(unittest.TestCase):
    def skia_fixture(self, root):
        """Synthetic bytes prove checking logic, never a real runtime qualification."""
        cache, output = root / "cache", root / "output"
        output.mkdir()
        lock, libraries, target, pins = {}, {}, {}, {}
        for slug, identity, section, members in [
            ("skiasharp", "SkiaSharp", "runtime", [("lib/net10.0/SkiaSharp.dll", "SkiaSharp.dll")]),
            ("skiasharp.nativeassets.win32", "SkiaSharp.NativeAssets.Win32", "native", [
                ("runtimes/win-x64/native/libSkiaSharp.dll", "libSkiaSharp.dll"),
                ("runtimes/win-x64/native/libSkiaSharp.pdb", "libSkiaSharp.pdb")])]:
            path = cache / slug / "4.153.1" / (slug + ".4.153.1.nupkg")
            path.parent.mkdir(parents=True)
            with zipfile.ZipFile(path, "x") as archive:
                for member, relative in members:
                    raw = ("PUBLIC_SYNTHETIC_" + relative).encode()
                    archive.writestr(member, raw)
                    (output / relative).write_bytes(raw)
            pins[slug] = hashlib.sha256(path.read_bytes()).hexdigest()
            lock[identity] = {"resolved": "4.153.1", "contentHash": base64.b64encode(hashlib.sha512(path.read_bytes()).digest()).decode()}
            libraries[identity + "/4.153.1"] = {"type": "package"}
            target[identity + "/4.153.1"] = {section: {member: {} for member, _ in members}}
        target["SkiaSharp/4.153.1"]["dependencies"] = {"SkiaSharp.NativeAssets.Win32": "4.153.1"}
        (output / "KuaiYi.ImageWorker.deps.json").write_text(json.dumps({"targets": {".NETCoreApp,Version=v10.0/win-x64": target}, "libraries": libraries}))
        path = root / "src/KuaiYi.ImageWorker/packages.lock.json"
        path.parent.mkdir(parents=True)
        path.write_text(json.dumps({"dependencies": {"net10.0": lock}}))
        return cache, output, pins

    def test_skia_publish_requires_actual_pinned_members_and_deps(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            cache, output, pins = self.skia_fixture(root)
            with patch.object(publisher, "REPO", root), patch.dict(publisher.INPUTS.SKIA, pins):
                self.assertEqual(3, len(publisher.inspect_skia(output, cache)))
                (output / "libSkiaSharp.dll").write_bytes(b"REPLACED_PUBLIC_FIXTURE")
                with self.assertRaisesRegex(RuntimeError, "member differs"):
                    publisher.inspect_skia(output, cache)

    def test_skia_copied_files_without_original_dependency_record_are_refused(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            cache, output, pins = self.skia_fixture(root)
            path = output / "KuaiYi.ImageWorker.deps.json"
            document = json.loads(path.read_text())
            document["libraries"].pop("SkiaSharp/4.153.1")
            path.write_text(json.dumps(document))
            with patch.object(publisher, "REPO", root), patch.dict(publisher.INPUTS.SKIA, pins), self.assertRaisesRegex(RuntimeError, "closure is missing"):
                publisher.inspect_skia(output, cache)

    def test_skia_locked_archive_sha512_cannot_be_replaced_by_self_declared_sha256(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            cache, output, pins = self.skia_fixture(root)
            path = root / "src/KuaiYi.ImageWorker/packages.lock.json"
            doc = json.loads(path.read_text())
            doc["dependencies"]["net10.0"]["SkiaSharp"]["contentHash"] = "wrong"
            path.write_text(json.dumps(doc))
            with patch.object(publisher, "REPO", root), patch.dict(publisher.INPUTS.SKIA, pins), self.assertRaisesRegex(RuntimeError, "locked SHA512"):
                publisher.inspect_skia(output, cache)

    def test_real_command_output_and_exit_are_recorded_without_running_product(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            row = publisher.execute([sys.executable, "-c", "print('PUBLIC_OUTPUT'); raise SystemExit(7)"],
                                    os.environ.copy(), root / "command.log", time.monotonic() + 10, root)
            self.assertEqual(7, row["exit"])
            self.assertEqual("PUBLIC_OUTPUT\n", (root / "command.log").read_text())
            self.assertEqual(1, len(publisher.manifest(root)))

    def test_timeout_preserves_original_output_and_waits_original_child(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            code = "import os,time; print(os.getpid(),flush=True); time.sleep(30)"
            with self.assertRaises(subprocess.TimeoutExpired):
                publisher.execute([sys.executable, "-c", code], os.environ.copy(), root / "timeout.log", time.monotonic() + 0.6, root)
            pid = int((root / "timeout.log").read_text())
            with self.assertRaises(ProcessLookupError):
                os.kill(pid, 0)

    def test_inflight_size_limit_stops_only_new_child_and_retains_bytes(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            with patch.object(publisher, "LIMIT", 100), self.assertRaisesRegex(RuntimeError, "budget exceeded"):
                publisher.execute([sys.executable, "-c", "import time; print('X'*200,flush=True); time.sleep(30)"],
                                  os.environ.copy(), root / "bounded.log", time.monotonic() + 10, root)
            self.assertGreater((root / "bounded.log").stat().st_size, 100)

    def test_exited_leader_with_live_child_retains_original_receipt_and_refuses_success(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            receipt = {}
            code = "import subprocess,sys; p=subprocess.Popen([sys.executable,'-c','import time; time.sleep(30)']); print(p.pid,flush=True)"
            with self.assertRaisesRegex(RuntimeError, "settlement unconfirmed"):
                publisher.execute([sys.executable, "-c", code], os.environ.copy(), root / "child.log",
                                  time.monotonic() + 10, root, receipt)
            self.assertEqual(0, receipt["exit"])
            self.assertEqual("child.log", receipt["log"])
            self.assertIn("group", receipt)
            self.assertIn(receipt["settlement"], ["Unknown", "LeaderJoined_GroupEmpty", "LeaderJoined_ChildrenExited_UnreapedZombies"])

    def test_manifest_retains_zero_length_auxiliary_files_but_refuses_links(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "empty.log").touch()
            self.assertEqual(0, publisher.manifest(root)[0]["bytes"])
            (root / "link").symlink_to(root / "empty.log")
            with self.assertRaisesRegex(RuntimeError, "Special"):
                publisher.manifest(root)

    def test_source_snapshot_rejects_untracked_compile_input_and_changed_lock(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "src").mkdir()
            (root / "src/real.cs").write_text("PUBLIC_SOURCE")
            (root / "src/packages.lock.json").write_text("{}")
            def git(*args):
                return subprocess.run(["git", *args], cwd=root, check=True, capture_output=True)
            git("init", "-q")
            git("add", "src")
            git("-c", "user.name=Local Test", "-c", "user.email=local@example.invalid", "commit", "-qm", "fixture")
            with patch.object(publisher, "REPO", root):
                self.assertEqual(2, len(publisher.product_sources()))
                (root / "src/injected.cs").write_text("PUBLIC_UNTRACKED")
                with self.assertRaisesRegex(RuntimeError, "Untracked"):
                    publisher.product_sources()
                # Own synthetic test file only.
                (root / "src/injected.cs").unlink()
                (root / "assets").mkdir()
                (root / "assets/untracked.txt").write_text("PUBLIC_EMBEDDED_INPUT")
                with self.assertRaisesRegex(RuntimeError, "Untracked"):
                    publisher.product_sources()
                (root / "assets/untracked.txt").unlink()
                (root / "src/packages.lock.json").write_text('{"changed":true}')
                with self.assertRaises(subprocess.CalledProcessError):
                    publisher.product_sources()

    def test_role_rejects_framework_dependent_output_before_runtime_read(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "KuaiYi.App.runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {"framework": {"name": "Microsoft.NETCore.App", "version": "10.0.13"}}}))
            with self.assertRaisesRegex(RuntimeError, "self-contained"):
                publisher.inspect_role("App", root, [])

    def test_bootstrap_configuration_compares_the_complete_original_contract(self):
        original = {"tfm": "net10.0", "includedFrameworks": [{"name": "Microsoft.NETCore.App", "version": "10.0.13"}],
                    "configProperties": {"System.Reflection.Metadata.MetadataUpdater.IsSupported": False,
                                         "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization": False}}
        publisher.require_bootstrap_config({"runtimeOptions": original})
        for changed in ({**original, "extra": True}, {**original, "tfm": "net9.0"}, {**original, "configProperties": {}}):
            with self.assertRaisesRegex(RuntimeError, "original bootstrap"):
                publisher.require_bootstrap_config({"runtimeOptions": changed})
        with self.assertRaises(RuntimeError):
            publisher.require_bootstrap_config({"runtimeOptions": original, "extra": True})
        self.assertFalse(publisher.equal_json({"property": False}, {"property": 0}))
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "duplicate.json"
            path.write_text('{"runtimeOptions":{},"runtimeOptions":{}}')
            with self.assertRaisesRegex(RuntimeError, "Duplicate"):
                publisher.read_json(path)

    def test_role_selects_actual_framework_set_and_keeps_publish_additional_files(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            for suffix in (".dll", ".exe", ".deps.json", ".pdb"):
                (root / ("KuaiYi.App" + suffix)).write_text("PUBLIC_SYNTHETIC")
            packs = [("Core", "10.0.13", "core"), ("Desktop", "10.0.13", "desktop")]
            for names in (["Microsoft.NETCore.App"], ["Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App"]):
                (root / "KuaiYi.App.runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {"includedFrameworks": [{"name": n, "version": "10.0.13"} for n in names]}}))
                with patch.object(publisher, "bind_published_runtime", return_value=[]) as bind:
                    result = publisher.inspect_role("App", root, packs)
                self.assertEqual(packs[:len(names)], bind.call_args.args[1])
                self.assertIn("KuaiYi.App.pdb", [r["path"] for r in result["files"]])

    def test_explicit_wpf_profile_excludes_unlabelled_members_but_keeps_hash_gate(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            archive = root / "desktop.zip"
            with zipfile.ZipFile(archive, "w") as z:
                z.writestr("data/RuntimeList.xml", '<FileList TargetFrameworkVersion="10.0"><File Path="Wpf.dll" Profile="WPF"/><File Path="Forms.dll"/></FileList>')
            (root / "Wpf.dll").write_bytes(b"PUBLIC_RUNTIME")
            rows = [{"Id": "Desktop", "Member": name, "Path": name, "Sha256": hashlib.sha256(b"PUBLIC_RUNTIME").hexdigest()} for name in ("Wpf.dll", "Forms.dll")]
            packs = [("Core", "10.0.13", "unused"), ("Desktop", "10.0.13", archive)]
            with patch.object(publisher.INPUTS.RUNTIME, "runtime_payload_records", return_value=rows):
                self.assertEqual([rows[0]], publisher.bind_published_runtime(root, packs))
                (root / "Wpf.dll").write_bytes(b"PUBLIC_CHANGED")
                with self.assertRaisesRegex(RuntimeError, "hash differs"):
                    publisher.bind_published_runtime(root, packs)

    def test_packaging_copies_original_notices_and_never_overwrites(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            archive = root / "core.zip"
            with zipfile.ZipFile(archive, "w") as z:
                z.writestr("LICENSE.TXT", "PUBLIC_LICENSE")
                z.writestr("THIRD-PARTY-NOTICES.TXT", "PUBLIC_NOTICES")
            rows = publisher.add_runtime_notices(root / "stage", [("Core", "10.0.13", archive)])
            self.assertEqual(2, len(rows))
            self.assertEqual("PUBLIC_LICENSE", (root / "stage/licenses/dotnet/LICENSE.txt").read_text())
            with self.assertRaises(FileExistsError):
                publisher.add_runtime_notices(root / "stage", [("Core", "10.0.13", archive)])


if __name__ == "__main__":
    unittest.main()
