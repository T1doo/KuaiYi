"""Process/pipe ownership checks only; synthetic Linux children are not WPF."""
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import sys
import subprocess
import tempfile
import threading
import unittest
from unittest import mock

spec = importlib.util.spec_from_file_location("overlay_runner", Path(__file__).with_name("overlay_stage_runner.py"))
M = importlib.util.module_from_spec(spec)
spec.loader.exec_module(M)
SHA = "b334c846a6b81fbf276adbd54491e367696abd08"


def result(root, **changes):
    data = dict(Kind="ActualProductionOverlayStages", Status="Pass_LimitedStages", SourceHead=SHA,
                TimedOut=False, Errors=[], OriginalDriverCompleted=True, OriginalCloseTaskCompleted=True,
                ClosedObserved=True, CloseConfirmed=True, CleanupConfirmed=True)
    data.update(changes)
    (root / "overlay-stages-result.json").write_text(json.dumps(data))


class Clock:
    value = 0
    def __call__(self): return self.value


class Pipe(io.BytesIO):
    def __init__(self, value=b"neutral", *, held=False, close_error=False):
        super().__init__(value)
        self.eof_entered, self.release = threading.Event(), threading.Event()
        self.close_error = close_error
        if not held: self.release.set()
    def read(self, count=-1):
        data = super().read(count)
        if not data:
            self.eof_entered.set()
            self.release.wait()
        return data
    def close(self):
        super().close()
        if self.close_error: raise OSError("original-pipe-close")


class Process:
    def __init__(self, *, exited=False, held=False, close_error=False):
        self.stdout, self.stderr = Pipe(held=held), Pipe(held=held, close_error=close_error)
        self.wait_entered, self.exit, self.killed = threading.Event(), threading.Event(), threading.Event()
        self.kill_count, self.wait_count = 0, 0
        if exited: self.exit.set()
    def wait(self):
        self.wait_count += 1
        self.wait_entered.set()
        self.exit.wait()
        return -9 if self.killed.is_set() else 0
    def kill(self):
        self.kill_count += 1
        self.killed.set()  # Intentionally doesn't pretend the process/EOF has settled.
    def release(self):
        self.exit.set()
        self.stdout.release.set()
        self.stderr.release.set()


class OverlayRunnerTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.owners, self.processes = [], []
    def tearDown(self):
        for process in self.processes: process.release()
        for owner in self.owners:
            owner.original.join(5)
            self.assertFalse(owner.original.is_alive(), "original test owner remained alive")
            owner.join_originals()
        self.temporary.cleanup()
    def owner(self, *, process=None, launch=None, clock=None, **kwargs):
        if process is not None:
            self.processes.append(process)
            launch = lambda *a, **k: process
        owner = M.OverlayProcessOwner(["synthetic-tool"], self.root, SHA, dict(os.environ, GITHUB_SHA=SHA),
                                      **({"_launch": launch} if launch else {}), **({"_clock": clock} if clock else {}), **kwargs)
        self.owners.append(owner)
        return owner

    def test_real_child_concurrent_pipe_drain_actual_exit_and_owned_state_absence(self):
        script = '''import json,os,pathlib,sys
p=pathlib.Path('overlay-state-fixture');p.mkdir();(p/'neutral').write_text('fixture');(p/'neutral').unlink();p.rmdir()
sys.stdout.buffer.write(b'a'*262144);sys.stdout.flush();sys.stderr.buffer.write(b'b'*262144);sys.stderr.flush()
d=dict(Kind='ActualProductionOverlayStages',Status='Pass_LimitedStages',SourceHead=os.environ['GITHUB_SHA'],TimedOut=False,Errors=[])
d.update({k:True for k in ('OriginalDriverCompleted','OriginalCloseTaskCompleted','ClosedObserved','CloseConfirmed','CleanupConfirmed')})
pathlib.Path('overlay-stages-result.json').write_text(json.dumps(d))
'''
        owner = M.OverlayProcessOwner([sys.executable, "-c", script], self.root, SHA, dict(os.environ, GITHUB_SHA=SHA))
        self.owners.append(owner)
        receipt = owner.observe()
        self.assertEqual("Pass_LimitedProcess", receipt["Status"])
        self.assertEqual(0, receipt["ExitCode"])
        self.assertTrue(receipt["CleanupConfirmed"])
        self.assertTrue(all(receipt["Operations"].values()))
        for label, byte in (("stdout", b"a"), ("stderr", b"b")):
            r = receipt["Streams"][label]
            self.assertEqual(262144, r["ReadBytes"])
            self.assertEqual(hashlib.sha256(byte*262144).hexdigest(), r["ReadSha256"])
            self.assertEqual(r["ReadSha256"], r["LogFileSha256"])

    def test_nonzero_original_exit_still_observes_absent_state_without_promoting_failure(self):
        script = "import sys; sys.exit(3)"
        owner = M.OverlayProcessOwner([sys.executable, "-c", script], self.root, SHA, dict(os.environ, GITHUB_SHA=SHA))
        self.owners.append(owner)
        receipt = owner.join_originals()
        self.assertEqual(3, receipt["ExitCode"])
        self.assertTrue(receipt["OriginalExitConfirmed"])
        self.assertTrue(all(receipt["Operations"].values()))
        self.assertTrue(receipt["StateDirectoryAbsenceConfirmed"])
        self.assertEqual("Absent", receipt["StateDirectoryObservation"])
        self.assertEqual("Failed", receipt["Status"])
        self.assertFalse(receipt["CleanupConfirmed"])
        self.assertFalse(receipt["RescueRequested"])
        self.assertTrue(any("did not exit successfully" in e["Error"] for e in receipt["Errors"]))

    def test_nonzero_exit_and_residue_report_both_without_deleting_state(self):
        script = "import pathlib,sys; pathlib.Path('overlay-state-original').mkdir(); sys.exit(3)"
        owner = M.OverlayProcessOwner([sys.executable, "-c", script], self.root, SHA, dict(os.environ, GITHUB_SHA=SHA))
        self.owners.append(owner)
        receipt = owner.join_originals()
        self.assertEqual(3, receipt["ExitCode"])
        self.assertEqual("Present", receipt["StateDirectoryObservation"])
        self.assertFalse(receipt["StateDirectoryAbsenceConfirmed"])
        self.assertTrue((self.root / "overlay-state-original").is_dir())
        self.assertEqual("Failed", receipt["Status"])
        self.assertFalse(receipt["CleanupConfirmed"])
        self.assertEqual({"OriginalOperation", "StateObservation"}, {e["Stage"] for e in receipt["Errors"]})

    def test_original_exit_does_not_substitute_for_held_stdout_and_stderr(self):
        clock, process = Clock(), Process(exited=True, held=True)
        owner = self.owner(process=process, clock=clock)
        self.assertTrue(process.stdout.eof_entered.wait(3)); self.assertTrue(process.stderr.eof_entered.wait(3))
        self.assertTrue(process.wait_entered.wait(3))
        owner.original_wait.join(3)
        clock.value = 60
        receipt = owner.observe()
        self.assertTrue(receipt["OriginalExitConfirmed"])
        self.assertFalse(receipt["CleanupConfirmed"])
        self.assertEqual("Unconfirmed", receipt["Status"])
        self.assertEqual(0, process.kill_count)
        self.assertTrue(owner.original.is_alive())
        result(self.root)
        process.release(); owner.join_originals()
        self.assertNotEqual("Pass_LimitedProcess", owner.snapshot()["Status"])
        self.assertEqual(1, process.wait_count)

    def test_deadline_keeps_exact_owner_until_actual_kill_exit_and_both_eofs(self):
        clock, process = Clock(), Process(held=True)
        owner = self.owner(process=process, clock=clock)
        self.assertTrue(process.wait_entered.wait(3)); clock.value = 60
        receipt = owner.observe(); self.assertTrue(process.killed.wait(3))
        self.assertIs(owner.process, process)
        self.assertEqual("Unconfirmed", receipt["Status"])
        self.assertFalse(receipt["OriginalExitConfirmed"])
        self.assertEqual(1, process.kill_count)
        owner.observe(); self.assertEqual(1, process.kill_count)
        process.release(); final = owner.join_originals()
        self.assertTrue(final["OriginalExitConfirmed"])
        self.assertEqual("Failed", final["Status"])
        self.assertFalse(final["CleanupConfirmed"])
        self.assertEqual(1, process.wait_count)

    def test_late_create_is_adopted_only_as_its_original_object_and_rescued_once(self):
        clock, process = Clock(), Process()
        self.processes.append(process)
        entered, release = threading.Event(), threading.Event()
        self.addCleanup(release.set)
        def launch(*args, **kwargs):
            entered.set(); release.wait(); return process
        owner = self.owner(launch=launch, clock=clock)
        self.assertTrue(entered.wait(3)); clock.value = 60
        self.assertEqual("Unconfirmed", owner.observe()["Status"])
        self.assertIsNone(owner.process)
        release.set(); self.assertTrue(process.killed.wait(3))
        self.assertIs(owner.process, process); self.assertEqual(1, process.kill_count)
        process.release(); self.assertEqual("Failed", owner.join_originals()["Status"])

    def test_timely_settlement_survives_late_observer_continuation(self):
        clock, process = Clock(), Process(exited=True, held=True)
        owner = self.owner(process=process, clock=clock)
        result(self.root)
        process.release(); owner.join_originals()
        clock.value = 61
        receipt = owner.observe()
        self.assertEqual("Pass_LimitedProcess", receipt["Status"])
        self.assertFalse(receipt["DeadlineExpired"])
        self.assertEqual(0, process.kill_count)

    def test_output_bound_continues_draining_without_promoting_truncated_log(self):
        process = Process(exited=True)
        process.stdout = Pipe(b"x"*200000)
        owner = self.owner(process=process, _output_limit=4096)
        owner.join_originals(); receipt = owner.snapshot()
        self.assertEqual("Failed", receipt["Status"])
        stream = receipt["Streams"]["stdout"]
        self.assertEqual(200000, stream["ReadBytes"])
        self.assertEqual(4096, stream["LogFileBytes"])
        self.assertEqual(hashlib.sha256(b"x"*200000).hexdigest(), stream["ReadSha256"])
        self.assertEqual(hashlib.sha256(b"x"*4096).hexdigest(), stream["LogFileSha256"])
        self.assertTrue(stream["Eof"] and stream["Closed"])
        self.assertTrue(any(e["Stage"] == "stdoutBound" for e in receipt["Errors"]))

    def test_write_and_close_errors_both_survive_while_full_output_is_drained(self):
        class BrokenWriter:
            def __init__(self, path): self.original = path.open("xb")
            def fileno(self): return self.original.fileno()
            def write(self, data): self.original.write(data[:2]); raise OSError("original-write")
            def flush(self): self.original.flush()
            def close(self): self.original.close(); raise OSError("original-log-close")
        process = Process(exited=True, close_error=True)
        owner = self.owner(process=process, _open_log=BrokenWriter)
        owner.join_originals(); receipt = owner.snapshot()
        stages = {e["Stage"] for e in receipt["Errors"]}
        self.assertTrue({"stdoutWrite", "stdoutLogClose", "stderrClose"} <= stages)
        self.assertEqual("Failed", receipt["Status"])
        self.assertTrue(all(s["Eof"] for s in receipt["Streams"].values()))

    def test_state_residue_or_false_close_result_cannot_pass_and_nothing_is_deleted(self):
        process = Process(exited=True, held=True)
        owner = self.owner(process=process)
        state = self.root / "overlay-state-original"
        state.mkdir(); (state / "keep").write_text("neutral")
        result(self.root)
        process.release(); receipt = owner.join_originals()
        self.assertEqual("Failed", receipt["Status"])
        self.assertTrue((state / "keep").exists())
        self.assertFalse(receipt["StateDirectoryAbsenceConfirmed"])

    def test_source_and_actual_close_claims_are_checked_after_original_settlement(self):
        for changes in ({"SourceHead": "a"*40}, {"OriginalCloseTaskCompleted": False}, {"TimedOut": True}):
            with self.subTest(changes=changes), tempfile.TemporaryDirectory() as temp:
                path = Path(temp); process = Process(exited=True, held=True)
                owner = M.OverlayProcessOwner(["fixture"], path, SHA, {"GITHUB_SHA": SHA}, _launch=lambda *a, **k: process)
                result(path, **changes); process.release()
                receipt = owner.join_originals()
                self.assertEqual("Failed", receipt["Status"])
                self.assertTrue(receipt["OriginalExitConfirmed"])

    def test_foreign_or_linked_output_is_rejected_before_launch(self):
        calls = []
        (self.root / "foreign").write_text("keep")
        with self.assertRaises(RuntimeError): self.owner(launch=lambda *a, **k: calls.append(a))
        self.assertEqual([], calls)
        self.assertEqual("keep", (self.root / "foreign").read_text())
        with tempfile.TemporaryDirectory() as elsewhere:
            link = Path(elsewhere) / "linked"; link.symlink_to(self.root, target_is_directory=True)
            with self.assertRaises(RuntimeError):
                M.OverlayProcessOwner(["fixture"], link, SHA, {"GITHUB_SHA": SHA}, _launch=lambda *a, **k: calls.append(a))
            self.assertEqual([], calls)

    def test_each_observer_start_failure_prevents_child_launch(self):
        original_thread = threading.Thread
        for failed_name in ("overlay-original-stdout", "overlay-original-stderr", "overlay-original-wait"):
            with self.subTest(failed_name=failed_name), tempfile.TemporaryDirectory() as temp:
                calls = []
                process = Process(exited=True)
                class RefuseStart(original_thread):
                    def start(self): raise RuntimeError("original-thread-start-refused")
                def thread(*args, **kwargs):
                    return (RefuseStart if kwargs.get("name") == failed_name else original_thread)(*args, **kwargs)
                def launch(*args, **kwargs): calls.append(args); return process
                with mock.patch.object(M.threading, "Thread", thread):
                    owner = M.OverlayProcessOwner(["fixture"], Path(temp), SHA, {"GITHUB_SHA": SHA}, _launch=launch)
                    owner.original.join(3)
                    self.assertFalse(owner.original.is_alive())
                    receipt = owner.join_originals()
                self.assertEqual([], calls, "A refused original observer must not leave a started child")
                self.assertFalse(receipt["CleanupConfirmed"])
                self.assertTrue(any("original-thread-start-refused" in e["Error"] for e in receipt["Errors"]))

    def test_rescue_start_failure_retains_original_observers_until_real_settlement(self):
        original_thread = threading.Thread
        class RefuseRescue(original_thread):
            def start(self): raise RuntimeError("original-rescue-start-refused")
        def thread(*args, **kwargs):
            return (RefuseRescue if kwargs.get("name") == "overlay-original-rescue" else original_thread)(*args, **kwargs)
        clock, process = Clock(), Process(held=True)
        with mock.patch.object(M.threading, "Thread", thread):
            owner = self.owner(process=process, clock=clock)
            self.assertTrue(process.wait_entered.wait(3)); clock.value = 60
            receipt = owner.observe()
            self.assertTrue(owner.original.is_alive())
            self.assertEqual("Unconfirmed", receipt["Status"])
            self.assertFalse(receipt["RescueSettled"])
            self.assertEqual(0, process.kill_count)
            process.release(); owner.join_originals()
        self.assertFalse(owner.snapshot()["CleanupConfirmed"])
        self.assertTrue(any(e["Stage"] == "OriginalRescueStart" for e in owner.snapshot()["Errors"]))

    def test_real_original_process_termination_is_failure_even_after_exit_and_eof(self):
        clock, launched = Clock(), threading.Event()
        def launch(*args, **kwargs):
            child = subprocess.Popen([sys.executable, "-c", "import threading;threading.Event().wait()"], **kwargs)
            launched.set()
            return child
        owner = self.owner(launch=launch, clock=clock)
        self.assertTrue(launched.wait(3)); clock.value = 60
        owner.observe()
        owner.original.join(5)
        self.assertFalse(owner.original.is_alive())
        receipt = owner.join_originals()
        self.assertTrue(receipt["OriginalExitConfirmed"])
        self.assertTrue(all(receipt["Operations"].values()))
        self.assertTrue(all(s["Eof"] and s["Closed"] for s in receipt["Streams"].values()))
        self.assertTrue(receipt["RescueRequested"])
        self.assertEqual("Failed", receipt["Status"])
        self.assertFalse(receipt["CleanupConfirmed"])

    def test_context_rejects_other_platform_repository_branch_or_rerun(self):
        env = {"GITHUB_ACTIONS": "true", "GITHUB_REPOSITORY": "T1doo/KuaiYi",
               "GITHUB_REF": "refs/heads/dev/p7-fixed-signoff", "GITHUB_EVENT_NAME": "push",
               "GITHUB_RUN_ATTEMPT": "1", "RUNNER_OS": "Windows"}
        M.require_windows_runner(env, "nt")
        for changed, platform in ((env, "posix"), (dict(env, GITHUB_REPOSITORY="foreign/repo"), "nt"),
                                  (dict(env, GITHUB_REF="refs/heads/main"), "nt"), (dict(env, GITHUB_RUN_ATTEMPT="2"), "nt")):
            with self.assertRaises(RuntimeError): M.require_windows_runner(changed, platform)

    def test_thin_wrapper_reports_pending_and_keeps_owner_across_report_failure(self):
        clock, process = Clock(), Process(held=True)
        owner = self.owner(process=process, clock=clock)
        self.assertTrue(process.wait_entered.wait(3)); clock.value = 60
        reported = []
        def report(receipt):
            reported.append(receipt["Status"])
            if len(reported) == 1:
                self.assertEqual("Unconfirmed", receipt["Status"])
                self.assertTrue(owner.original.is_alive())
                process.release()
                raise OSError("original-report-failure")
        with mock.patch.object(M, "start_overlay", return_value=owner):
            retained, receipt = M.run_overlay(None, None, SHA, {}, report)
        self.assertIs(owner, retained)
        self.assertEqual(["Unconfirmed", "Failed"], reported)
        self.assertFalse(receipt["CleanupConfirmed"])
        self.assertTrue(any(e["Stage"] == "PendingReport" for e in receipt["Errors"]))


if __name__ == "__main__": unittest.main()
