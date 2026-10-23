"""Owned subprocess observation for one future Windows overlay sample; no CI dispatch.

A deadline is a decision boundary, not evidence that a process or pipe exited.
An unresolved owner must stay alive (non-daemon operations) until settled or the
existing external job boundary ends it. Never replace the original process by PID.
"""
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import threading
import time
import uuid

BUDGET_SECONDS = 60
OUTPUT_LIMIT = 4 * 1024 * 1024
RESULT_LIMIT = 16 * 1024 * 1024


def linked(path):
    value = path.lstat()
    return stat.S_ISLNK(value.st_mode) or bool(getattr(value, "st_file_attributes", 0) & 0x400)


def plain_directory(path):
    if not path.is_dir() or any(linked(p) for p in (path, *path.parents)):
        raise RuntimeError("Exclusive plain output directory required")
    value = path.stat()
    return value.st_dev, value.st_ino


class OverlayProcessOwner:
    """Retains one launch, one OS wait and both original pipe drains.

    Constructor collaborators are private test seams. Production start_overlay
    supplies the real Popen, monotonic clock, fixed limits and fixed arguments.
    """
    def __init__(self, argv, directory, source_head, env, *, _launch=subprocess.Popen,
                 _clock=time.monotonic, _output_limit=OUTPUT_LIMIT, _open_log=None):
        self.directory = Path(directory)
        self.identity = plain_directory(self.directory)
        if any(self.directory.iterdir()) or not re.fullmatch(r"[0-9a-f]{40}", source_head):
            raise RuntimeError("Empty owned directory and full source SHA required")
        self.source_head, self.argv, self.env = source_head, list(argv), dict(env)
        if self.env.get("GITHUB_SHA") != source_head:
            raise RuntimeError("Child source environment differs")
        self.clock, self.launch = _clock, _launch
        self.limit = _output_limit
        self.open_log = _open_log or (lambda path: path.open("xb"))
        self.started = self.clock()
        self.deadline = self.started + BUDGET_SECONDS
        self.lock = threading.RLock()
        self.process = None
        self.launch_attempted = False
        self.launch_ready = threading.Event()
        self.original_wait = self.original_stdout = self.original_stderr = None
        self.rescue = None
        self.deadline_expired = self.rescue_requested = False
        self.exit_code = self.completed_at = None
        self.result = None
        self.errors = []
        self.streams = {}
        self.state_absent = False
        self.state_observation = "NotObserved"
        self.done = threading.Event()
        self.original = threading.Thread(target=self._run, name="overlay-original-owner", daemon=False)
        # Publish original ownership before the only launch can begin.
        self.original.start()

    def _error(self, stage, error):
        with self.lock:
            self.errors.append({"Stage": stage, "Type": type(error).__name__, "Error": str(error)})

    def _drain(self, label, stream):
        record = {"Eof": False, "Closed": False, "LogClosed": False, "ReadBytes": 0,
                  "SavedBytes": 0, "ReadSha256": None, "ConfirmedWrittenSha256": None, "Exceeded": False,
                  "LogFileBytes": None, "LogFileSha256": None}
        raw, saved = hashlib.sha256(), hashlib.sha256()
        output = None
        file_identity = None
        try:
            try:
                output = self.open_log(self.directory / (label + ".log"))
                value = os.fstat(output.fileno())
                file_identity = value.st_dev, value.st_ino
            except Exception as error:
                self._error(label + "Open", error)
            writing = output is not None
            while True:
                try:
                    block = stream.read(65536)
                except Exception as error:
                    self._error(label + "Read", error)
                    break
                if not block:
                    record["Eof"] = True
                    break
                raw.update(block)
                record["ReadBytes"] += len(block)
                if record["ReadBytes"] > self.limit and not record["Exceeded"]:
                    record["Exceeded"] = True
                    self._error(label + "Bound", RuntimeError("Output exceeds retained-log bound; continue draining"))
                retain = block[:max(0, self.limit - record["SavedBytes"])]
                if writing and retain:
                    try:
                        count = output.write(retain)
                        if count != len(retain):
                            raise OSError("Short log write; saved digest unconfirmed")
                        saved.update(retain)
                        record["SavedBytes"] += count
                    except Exception as error:
                        writing = False
                        self._error(label + "Write", error)
        finally:
            try:
                stream.close()
                record["Closed"] = True
            except Exception as error:
                self._error(label + "Close", error)
            if output is not None:
                try:
                    output.flush()
                    os.fsync(output.fileno())
                except Exception as error:
                    self._error(label + "Flush", error)
                try:
                    output.close()
                    record["LogClosed"] = True
                except Exception as error:
                    self._error(label + "LogClose", error)
            record["ReadSha256"] = raw.hexdigest()
            # Only confirmed complete writes enter this digest; failures remain failures.
            record["ConfirmedWrittenSha256"] = saved.hexdigest()
            if record["LogClosed"] and file_identity is not None:
                try:
                    path = self.directory / (label + ".log")
                    if linked(path):
                        raise RuntimeError("Replaced log rejected")
                    with path.open("rb") as original_log:
                        value = os.fstat(original_log.fileno())
                        if (value.st_dev, value.st_ino) != file_identity:
                            raise RuntimeError("Original log identity differs")
                        raw_log = original_log.read(self.limit + 1)
                    if len(raw_log) > self.limit:
                        raise RuntimeError("Original log grew beyond bound")
                    record["LogFileBytes"] = len(raw_log)
                    record["LogFileSha256"] = hashlib.sha256(raw_log).hexdigest()
                except Exception as error:
                    self._error(label + "LogVerify", error)
            with self.lock:
                self.streams[label] = record

    def _wait(self):
        try:
            self.exit_code = self.process.wait()  # Exactly this Popen, exactly one original wait.
        except Exception as error:
            self._error("OriginalWait", error)

    def _request_rescue(self, *, expired=True):
        with self.lock:
            self.deadline_expired |= expired
            if self.process is None or self.exit_code is not None or self.rescue_requested:
                return
            self.rescue_requested = True
            original = self.process
            def kill_original():
                try:
                    original.kill()  # No PID lookup, process tree, second signal or replacement owner.
                except Exception as error:
                    self._error("OriginalProcessKill", error)
            self.rescue = threading.Thread(target=kill_original, name="overlay-original-rescue", daemon=False)
            try:
                self.rescue.start()
            except Exception as error:
                self._error("OriginalRescueStart", error)

    def _run(self):
        try:
            def after_launch(action):
                self.launch_ready.wait()
                if self.process is not None:
                    action()
            with self.lock:
                self.original_wait = threading.Thread(target=after_launch, args=(self._wait,), name="overlay-original-wait", daemon=False)
                self.original_stdout = threading.Thread(target=after_launch, args=(lambda: self._drain("stdout", self.process.stdout),), name="overlay-original-stdout", daemon=False)
                self.original_stderr = threading.Thread(target=after_launch, args=(lambda: self._drain("stderr", self.process.stderr),), name="overlay-original-stderr", daemon=False)
            for operation in (self.original_stdout, self.original_stderr, self.original_wait):
                operation.start()
            if self.deadline_expired or self.clock() >= self.deadline:
                self.deadline_expired = True
                raise TimeoutError("Observer preparation exhausted the original deadline; child not launched")
            self.launch_attempted = True
            process = self.launch(self.argv, cwd=self.directory, env=self.env, stdin=subprocess.DEVNULL,
                                  stdout=subprocess.PIPE, stderr=subprocess.PIPE, bufsize=0)
            with self.lock:
                self.process = process
            self.launch_ready.set()
            if self.deadline_expired or self.clock() >= self.deadline:
                self._request_rescue()  # A late launch still belongs to this owner.
            for operation in (self.original_wait, self.original_stdout, self.original_stderr):
                operation.join()
            if (self.exit_code is not None and len(self.streams) == 2 and
                    all(s["Eof"] and s["Closed"] and s["LogClosed"] for s in self.streams.values())):
                self._observe_state()
            if self.exit_code != 0:
                raise RuntimeError("Original process did not exit successfully")
            self._verify_result()
        except Exception as error:
            self._error("OriginalOperation", error)
            self._request_rescue(expired=False)
        finally:
            self.launch_ready.set()  # Release prepared observers even when no child was created.
            # A partially started operation list still belongs to this owner.
            for operation in (self.original_wait, self.original_stdout, self.original_stderr):
                if operation is not None and operation.ident is not None:
                    operation.join()
            with self.lock:
                self.completed_at = self.clock()
                if self.completed_at >= self.deadline:
                    self.deadline_expired = True
                self.done.set()

    def _observe_state(self):
        # Independent, read-only observation after the exact child and both logs settle.
        # A failed child may have cleaned its own state; that never makes the run pass.
        try:
            if plain_directory(self.directory) != self.identity:
                raise RuntimeError("Owned output directory was replaced")
            present = False
            for count, path in enumerate(self.directory.iterdir(), 1):
                if count > 64:
                    raise RuntimeError("Owned directory observation exceeds bound")
                present |= path.name.startswith("overlay-state-")
            if plain_directory(self.directory) != self.identity:
                raise RuntimeError("Owned output directory changed during observation")
            self.state_observation = "Present" if present else "Absent"
            self.state_absent = not present
            if present:
                raise RuntimeError("Original harness state cleanup unconfirmed")
        except Exception as error:
            if self.state_observation != "Present":
                self.state_observation = "Error"
            self._error("StateObservation", error)

    def _verify_result(self):
        if not self.state_absent or plain_directory(self.directory) != self.identity:
            raise RuntimeError("Owned output directory/state absence unconfirmed")
        path = self.directory / "overlay-stages-result.json"
        if linked(path) or not path.is_file():
            raise RuntimeError("Plain original result required")
        with path.open("rb") as stream:
            raw = stream.read(RESULT_LIMIT + 1)
        if len(raw) > RESULT_LIMIT:
            raise RuntimeError("Original result exceeds bound")
        actual = json.loads(raw)
        required = ("OriginalDriverCompleted", "OriginalCloseTaskCompleted", "ClosedObserved", "CloseConfirmed", "CleanupConfirmed")
        if (actual.get("Kind") != "ActualProductionOverlayStages" or actual.get("Status") != "Pass_LimitedStages" or
                actual.get("SourceHead") != self.source_head or actual.get("TimedOut") is not False or
                actual.get("Errors") != [] or any(actual.get(k) is not True for k in required)):
            raise RuntimeError("Original source/exit/cleanup result refused")
        self.result = {"Bytes": len(raw), "Sha256": hashlib.sha256(raw).hexdigest(), "Actual": actual}

    def observe(self):
        self.done.wait(max(0, self.deadline - self.clock()))
        if self.done.is_set():
            self.original.join(max(0, self.deadline - self.clock()))
        # Completion timestamp wins over a late observer continuation.
        with self.lock:
            timely = self.completed_at is not None and self.completed_at < self.deadline
        if not timely:
            self._request_rescue()
        return self.snapshot()

    def snapshot(self):
        with self.lock:
            operations = {name: op is not None and op.ident is not None and not op.is_alive()
                          for name, op in (("Wait", self.original_wait), ("Stdout", self.original_stdout), ("Stderr", self.original_stderr))}
            rescue_settled = self.rescue is None or self.rescue.ident is not None and not self.rescue.is_alive()
            io_ok = len(self.streams) == 2 and all(s["Eof"] and s["Closed"] and s["LogClosed"] for s in self.streams.values())
            settled = self.done.is_set() and not self.original.is_alive() and all(operations.values()) and rescue_settled
            passed = settled and io_ok and not self.errors and not self.deadline_expired and self.exit_code == 0 and self.state_absent
            return {"Kind": "OriginalOverlayProcessObservation", "SourceHead": self.source_head,
                    "Status": "Pass_LimitedProcess" if passed else "Failed" if settled else "Unconfirmed",
                    "DeadlineSeconds": BUDGET_SECONDS, "DeadlineExpired": self.deadline_expired,
                    "LaunchAttempted": self.launch_attempted, "OriginalProcessCreated": self.process is not None,
                    "OriginalExitConfirmed": self.exit_code is not None,
                    "ExitCode": self.exit_code, "Operations": operations, "Streams": dict(self.streams),
                    "RescueRequested": self.rescue_requested, "RescueSettled": rescue_settled,
                    "StateDirectoryObservation": self.state_observation,
                    "StateDirectoryAbsenceConfirmed": self.state_absent, "CleanupConfirmed": passed,
                    "OriginalResult": self.result, "Errors": list(self.errors),
                    "Owner": "OriginalReferenceRetained_NoPidAdoption_NoDirectoryDeletion"}

    def join_originals(self):
        """Caller retains this owner. May remain pending; never label deadline as exit."""
        self.original.join()
        if self.rescue is not None and self.rescue.ident is not None:
            self.rescue.join()
        return self.snapshot()


def require_windows_runner(env, platform):
    expected = {"GITHUB_ACTIONS": "true", "GITHUB_REPOSITORY": "T1doo/KuaiYi",
                "GITHUB_REF": "refs/heads/dev/p7-fixed-signoff", "GITHUB_EVENT_NAME": "push",
                "GITHUB_RUN_ATTEMPT": "1", "RUNNER_OS": "Windows"}
    if platform != "nt" or any(env.get(k) != v for k, v in expected.items()):
        raise RuntimeError("Only the existing one-attempt Windows runner is allowed")


def start_overlay(executable, parent, source_head, env):
    """Only the fixed production tool mode; caller must retain owner through reporting.

    Future CI must validate its source/binary inventory before this call. There is
    deliberately no CLI, workflow trigger, SDK installation or network operation.
    """
    require_windows_runner(env, os.name)
    if not re.fullmatch(r"[0-9a-f]{40}", source_head) or env.get("GITHUB_SHA") != source_head:
        raise RuntimeError("Reviewed source binding required before directory creation")
    executable, parent = Path(executable).absolute(), Path(parent).absolute()
    plain_directory(parent)
    if executable.name != "KuaiYi.GrowthHarness.exe" or not executable.is_file() or any(linked(p) for p in (executable, *executable.parents)):
        raise RuntimeError("Reviewed plain harness executable required")
    directory = parent / ("overlay-observation-" + uuid.uuid4().hex)
    directory.mkdir(exist_ok=False)
    return OverlayProcessOwner([str(executable), "--ci-overlay-stages", str(directory)], directory, source_head, env)


def run_overlay(executable, parent, source_head, env, report):
    """Retain the original owner across deadline reporting and actual settlement.

    Unconfirmed at60s is reported immediately, then remains owned. The existing
    external step/job bound may end a stuck wrapper; that is never cleanup proof.
    Do not run this owner in a disposable subprocess.run(timeout=60) wrapper.
    """
    owner = start_overlay(executable, parent, source_head, env)
    receipt = owner.observe()
    if receipt["Status"] == "Unconfirmed":
        try:
            report(receipt)
        except Exception as error:
            owner._error("PendingReport", error)
    owner.join_originals()
    receipt = owner.snapshot()
    try:
        report(receipt)
    except Exception as error:
        owner._error("FinalReport", error)
        receipt = owner.snapshot()
    return owner, receipt
