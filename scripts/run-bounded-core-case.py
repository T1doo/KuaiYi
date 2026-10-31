"""One fixed remaining Core case. No substitute runner or synthetic TRX.

Default inspection is read-only. --prepare compiles only the changed test project.
--execute requires an independently reviewed prepared manifest AND a runtime that
supports the readonly-root sandbox; there is no unsandboxed fallback or retry.
"""
import argparse
import hashlib
import importlib.util
import json
import os
import re
from pathlib import Path
import shutil
import stat
import sys
import threading
import time
import uuid
import xml.etree.ElementTree as ET

REPO = Path(__file__).resolve().parent.parent
OUT = REPO / "artifacts/2026-10-29-core-output-contract"
CASE = "KuaiYi.Core.Tests.InstallationDeploymentSessionTests.CompensationCancellationAfterTemporaryPreparationCannotReplaceFirstRecord"
DLL = REPO / "tests/KuaiYi.Core.Tests/bin/Release/net10.0/KuaiYi.Core.Tests.dll"
SOURCES = ("tests/KuaiYi.Core.Tests/BoundedDeploymentFixture.cs", "tests/KuaiYi.Core.Tests/InstallationDeploymentSessionTests.cs")
STREAM = 1024 * 1024
RESERVATION = 128 * 1024 * 1024
AGGREGATE = 1342177280
PRESERVED = 918676127
OLD_SECONDS = 216.468
PREPARATION_CHARGE = 60.0  # includes the preceding FIFO probes and this bounded build
ACCOUNTING_CHARGE = 2.0  # two later one-second synthetic-check charges; never reset the original budget
# Not an authorization token: this source-level gate remains false until the
# whole-case file/count/allocation formula is independently reviewed. A runtime
# configuration change alone must never enable this proposed reservation.
RESOURCE_PROOF_COMPLETE = False
NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def module():
    spec = importlib.util.spec_from_file_location("bounded_case_publisher", REPO / "scripts/publish-windows-managed.py")
    pub = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(pub)
    return pub


def save(path, value):
    raw = (json.dumps(value, indent=2) + "\n").encode()
    if len(raw) > STREAM:
        raise RuntimeError("Receipt budget exceeded; not written")
    errors = []
    f = None
    try:
        f = path.open("xb", buffering=0)
        offset = 0
        while offset < len(raw):
            written = f.write(raw[offset:])
            if not written:
                raise OSError("Receipt write made no progress")
            offset += written
        os.fsync(f.fileno())
    except BaseException as error:
        errors.append(error)
    finally:
        if f is not None:
            try:
                f.close()
            except BaseException as error:
                errors.append(error)
    if len(errors) == 1:
        raise errors[0]
    if errors:
        raise BaseExceptionGroup("Receipt write and close failed", errors)


def uid_map_runtime():
    """Read existing runtime metadata only; never probe namespace creation."""
    target = Path("/proc/self/uid_map").resolve(strict=True)
    mount = enclosing_mount(str(target), Path("/proc/self/mountinfo").read_text())
    return {"UidMapFilesystemReadonly": bool(os.statvfs(target).f_flag & os.ST_RDONLY),
            "EnclosingMount": mount, "SandboxSetupAttempted": False,
            "IsolationCapability": "NotEstablishedByMountMetadata",
            "RecoveryBoundary": "Platform-provided original isolation channel; no host changes or fallback"}


def enclosing_mount(target, mountinfo):
    matches = []
    for line in mountinfo.splitlines():
        fields = line.split()
        try:
            separator = fields.index("-")
            if separator < 6 or len(fields) < separator + 4:
                raise ValueError()
            point = re.sub(r"\\([0-7]{3})", lambda m: chr(int(m[1], 8)), fields[4])
            if target == point or target.startswith(point.rstrip("/") + "/"):
                matches.append({"MountPoint": point, "MountOptions": fields[5].split(","),
                                "FilesystemType": fields[separator + 1],
                                "SuperblockOptions": fields[separator + 3].split(",")})
        except ValueError as error:
            raise RuntimeError("Malformed mount metadata; runtime prerequisite unknown") from error
    if not matches:
        raise RuntimeError("No enclosing mount metadata; runtime prerequisite unknown")
    return max(matches, key=lambda row: len(row["MountPoint"]))


def inventory(root):
    """Observed pathname accounting, not a proof of peak usage or metadata size.

    Charge every name (including hardlinks) conservatively. Never follow a link.
    Allocation uses Linux st_blocks units of 512 bytes, not filesystem block size.
    A vanished/unreadable entry fails accounting rather than returning zero.
    """
    result = dict(LogicalBytes=0, AllocatedBytes=0, RegularFiles=0,
                  Directories=0, Fifos=0, Sockets=0, Entries=0)
    def visit(name, parent_fd=None):
        entry = os.stat(name, dir_fd=parent_fd, follow_symlinks=False)
        mode = entry.st_mode
        if stat.S_ISLNK(mode):
            raise RuntimeError("Unexpected linked fixture/output; retained")
        if parent_fd is None and not stat.S_ISDIR(mode):
            raise RuntimeError("Output root is not a directory")
        result["Entries"] += 1
        result["AllocatedBytes"] += entry.st_blocks * 512
        if stat.S_ISREG(mode):
            result["LogicalBytes"] += entry.st_size
            result["RegularFiles"] += 1
        elif stat.S_ISDIR(mode):
            result["Directories"] += 1
            fd = os.open(name, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC,
                         dir_fd=parent_fd)
            try:
                opened = os.fstat(fd)
                if (opened.st_dev, opened.st_ino) != (entry.st_dev, entry.st_ino):
                    raise RuntimeError("Directory changed before accounting; retained")
                # Anchor traversal to the opened directory, never its mutable
                # pathname. This is still a snapshot, not a peak-storage quota.
                with os.scandir(fd) as children:
                    for child in children:
                        visit(child.name, fd)
                current = os.stat(name, dir_fd=parent_fd, follow_symlinks=False)
                if (current.st_dev, current.st_ino, stat.S_IFMT(current.st_mode)) != (
                        entry.st_dev, entry.st_ino, stat.S_IFDIR):
                    raise RuntimeError("Directory changed during accounting; retained")
            finally:
                primary = sys.exc_info()[1]
                try:
                    os.close(fd)
                except BaseException as error:
                    if primary is not None:
                        raise BaseExceptionGroup("Original accounting and directory close failed", [primary, error])
                    raise
        elif stat.S_ISFIFO(mode):
            result["Fifos"] += 1
        elif stat.S_ISSOCK(mode):
            result["Sockets"] += 1
        else:
            raise RuntimeError("Unexpected special output; retained")

    visit(root)
    return result


def size(root):
    observed = inventory(root)
    return observed["LogicalBytes"], observed["RegularFiles"]


class TrxCapture:
    """Retain the actual logger byte stream, not a generated replacement TRX."""
    def __init__(self, fifo, target):
        self.fifo, self.target = fifo, target
        self.done = threading.Event()
        self.errors = []
        self.bytes = 0
        self.eof = False
        self.thread = threading.Thread(target=self.read, name="original-trx-reader")

    def read(self):
        fd = None
        out = None
        try:
            fd = os.open(self.fifo, os.O_RDONLY | os.O_NONBLOCK | os.O_NOFOLLOW)
            out = self.target.open("xb", buffering=0)
            while True:
                try:
                    raw = os.read(fd, 65536)
                except BlockingIOError:
                    if self.done.is_set():
                        raise RuntimeError("TRX writer still present after group settlement")
                    time.sleep(.01)
                    continue
                if not raw:
                    if self.done.is_set():
                        self.eof = True
                        return
                    time.sleep(.01)  # no writer yet is not successful EOF
                    continue
                keep = raw[:STREAM - self.bytes]
                offset = 0
                while offset < len(keep):
                    written = out.write(keep[offset:])
                    if not written:
                        raise OSError("TRX prefix write made no progress")
                    self.bytes += written
                    offset += written
                if len(keep) != len(raw):
                    raise RuntimeError("Original TRX stream exceeds reservation; prefix retained")
        except BaseException as error:
            self.errors.append(type(error).__name__ + ": " + str(error))
        finally:
            if out is not None:
                try:
                    out.close()
                except BaseException as error:
                    self.errors.append("TRX output close: " + type(error).__name__)
            if fd is not None:
                try:
                    os.close(fd)
                except BaseException as error:
                    self.errors.append("TRX close: " + type(error).__name__)

    def finish(self):
        self.done.set()
        self.thread.join(2)
        if self.thread.is_alive() or self.errors or not self.eof or not self.bytes:
            raise RuntimeError("Original TRX capture is incomplete; original prefix retained")


def settle_case(receipt, captured, fixture, path, start, primary):
    errors = []
    receipt.update(ReaderJoined=False, FixtureRetainedBytes=None, FixtureInventory=None)
    try:
        captured.done.set()
        if captured.thread.ident is None:
            raise RuntimeError("Original TRX reader never started")
        captured.thread.join(2)
        receipt["ReaderJoined"] = not captured.thread.is_alive()
        if not receipt["ReaderJoined"]:
            raise RuntimeError("Original TRX reader remains active")
    except BaseException as error:
        errors.append(error)
    try:
        receipt["FixtureInventory"] = inventory(fixture)
        receipt["FixtureRetainedBytes"] = receipt["FixtureInventory"]["LogicalBytes"]
    except BaseException as error:
        errors.append(error)
    receipt.update(Seconds=round(time.monotonic() - start, 3), ReaderErrors=captured.errors,
                   ReaderEof=captured.eof, CapturedBytes=captured.bytes, ProtectedOriginalsTouched=False)
    if errors:
        receipt.update(Outcome="FailedOrUnconfirmed", FinalizationErrors=[type(e).__name__ for e in errors])
    try:
        save(path, receipt)
    except BaseException as error:
        errors.append(error)
        receipt.update(Outcome="FailedOrUnconfirmed", ReceiptWriteStatus="Unconfirmed")
    if errors:
        if primary is not None:
            errors.insert(0, primary)
        if len(errors) == 1:
            raise errors[0]
        raise BaseExceptionGroup("Original case and independent settlement failed", errors)


def prepare(pub):
    folder = OUT / "prepared-r02"
    folder.mkdir()  # exclusive; never replace a previous preparation or artifact
    before = pub.product_sources()
    if before != json.loads((REPO / "artifacts/2026-10-27-release-managed-r03/sources-before.json").read_text()):
        raise RuntimeError("Product inputs differ from the preserved source binding")
    originals = folder / "original-test-binary"
    originals.mkdir()
    for suffix in (".dll", ".pdb", ".deps.json", ".runtimeconfig.json"):
        path = DLL.with_name("KuaiYi.Core.Tests" + suffix)
        shutil.copyfile(path, originals / path.name)
    sdk, env = pub.SDK.environment(REPO)
    env.update(MSBUILDDISABLENODEREUSE="1")
    row = {}
    start = time.monotonic()
    try:
        pub.execute([str(sdk / "dotnet"), "build", "tests/KuaiYi.Core.Tests/KuaiYi.Core.Tests.csproj",
                     "--no-restore", "-c", "Release", "-p:BuildProjectReferences=false",
                     "-p:UseSharedCompilation=false", "-nodeReuse:false", "-maxcpucount:1", "--nologo", "-v:minimal"],
                    env, folder / "build.log", time.monotonic() + 30, folder, row, log_limit_bytes=STREAM)
        if row.get("exit") != 0 or row.get("settlement") != "LeaderJoined_GroupEmpty":
            raise RuntimeError("Test-only build did not settle successfully")
        if pub.product_sources() != before:
            raise RuntimeError("Test-only build changed product source inputs")
        save(folder / "manifest.json", {"Kind": "TestOnlyBoundedFixturePreparation", "TestDllSha256": digest(DLL),
             "OriginalTestDllSha256": digest(originals / DLL.name), "Sources": {s: digest(REPO / s) for s in SOURCES},
             "ProductInputs": before, "Build": row, "Seconds": round(time.monotonic() - start, 3),
             "PreparationChargeSeconds": PREPARATION_CHARGE, "CoreCasesExecuted": 0})
    finally:
        primary = sys.exc_info()[1]
        try:
            save(folder / "build-settlement.json", row)
        except BaseException as error:
            if primary is not None:
                raise BaseExceptionGroup("Original build and settlement receipt failed", [primary, error])
            raise


def execute(pub):
    # An earlier direct sandbox probe failed here. This is deliberately not a
    # privileged retry, alternate namespace setup or unsandboxed fallback.
    if os.statvfs("/proc/self/uid_map").f_flag & os.ST_RDONLY:
        raise RuntimeError("Runtime uid-map filesystem is readonly; bounded sandbox unavailable; no child started")
    if not RESOURCE_PROOF_COMPLETE:
        raise RuntimeError("Whole-case resource proof is incomplete; no child started")
    manifest = json.loads((OUT / "prepared-r02/manifest.json").read_text())
    if (manifest.get("Kind") != "TestOnlyBoundedFixturePreparation" or
            manifest.get("Build", {}).get("exit") != 0 or
            manifest.get("Build", {}).get("settlement") != "LeaderJoined_GroupEmpty" or
            manifest.get("CoreCasesExecuted") != 0 or
            manifest.get("PreparationChargeSeconds") != PREPARATION_CHARGE or
            set(manifest.get("Sources", {})) != set(SOURCES)):
        raise RuntimeError("Prepared manifest does not match the fixed successful build contract")
    if digest(DLL) != manifest["TestDllSha256"] or any(digest(REPO / s) != h for s, h in manifest["Sources"].items()):
        raise RuntimeError("Prepared test bytes differ; no execution")
    if pub.product_sources() != manifest["ProductInputs"]:
        raise RuntimeError("Product source binding changed; no execution")
    old = json.loads((REPO / "artifacts/2026-10-27-core-remaining47/result.json").read_text())
    identity = old["NotExecuted"][0]
    if identity["Name"] != CASE or old["RetainedOwnTmpBytes"] != PRESERVED:
        raise RuntimeError("Original next case or retention binding differs")
    if PRESERVED + RESERVATION > AGGREGATE or shutil.disk_usage(REPO).free < 1024**3 + RESERVATION:
        raise RuntimeError("Frozen resource admission refused")
    run = OUT / "case-29"
    run.mkdir()
    opaque = run / "opaque"
    opaque.mkdir(mode=0o500)
    results = run / "results"
    results.mkdir(mode=0o700)
    fifo = results / "original.trx"
    os.mkfifo(fifo, 0o600)
    results.chmod(0o500)
    parent = Path("/workspace") / ("ky29-" + uuid.uuid4().hex[:6])
    parent.mkdir(mode=0o700)
    fixture = parent / ("kuaiyi-upgrade-tests-" + uuid.uuid4().hex)
    fixture.mkdir(mode=0o700)
    initial = fixture.stat()
    sdk, env = pub.SDK.environment(REPO)
    env.update({"TMPDIR": str(opaque), "TMP": str(opaque), "TEMP": str(opaque),
                "XDG_RUNTIME_DIR": str(opaque), "XDG_DATA_HOME": str(opaque),
                "XDG_CACHE_HOME": str(opaque), "XDG_CONFIG_HOME": str(opaque),
                "DOTNET_BUNDLE_EXTRACT_BASE_DIR": str(opaque), "DOTNET_EnableDiagnostics": "0",
                "DOTNET_DbgEnableMiniDump": "0", "COMPlus_DbgEnableMiniDump": "0",
                "VSTEST_DISABLE_ARTIFACTS_POSTPROCESSING": "1", "VSTEST_LOGTELEMETRY": "0",
                "KUAIYI_BOUNDED_DEPLOYMENT_ROOT": str(fixture)})
    for key in ("VSTEST_DIAG", "VSTEST_HOST_DEBUG", "VSTEST_RUNNER_DEBUG", "VSTEST_DATACOLLECTOR_DEBUG"):
        env.pop(key, None)
    settings = run / "case.runsettings"
    settings.write_text("<RunSettings><RunConfiguration><MaxCpuCount>1</MaxCpuCount>"
                        "<DisableAppDomain>true</DisableAppDomain><TestSessionTimeout>120000</TestSessionTimeout>"
                        "</RunConfiguration><xUnit><ShadowCopy>false</ShadowCopy><DiagnosticMessages>false</DiagnosticMessages>"
                        "<InternalDiagnosticMessages>false</InternalDiagnosticMessages><ParallelizeTestCollections>false</ParallelizeTestCollections>"
                        "</xUnit><DataCollectionRunSettings><DataCollectors /></DataCollectionRunSettings>"
                        "<InProcDataCollectionRunSettings><InProcDataCollectors /></InProcDataCollectionRunSettings></RunSettings>")
    captured = TrxCapture(fifo, run / "original-captured.trx")
    receipt = {"Kind": "OneOriginalCase_NotWholeCore", "Identity": identity, "ReservationBytes": RESERVATION,
               "OriginalConsumedSeconds": OLD_SECONDS, "PreparationChargedSeconds": PREPARATION_CHARGE, "AccountingChargedSeconds": ACCOUNTING_CHARGE,
               "Fixture": str(fixture), "FixtureDevice": initial.st_dev, "FixtureInode": initial.st_ino,
               "TestDllSha256": manifest["TestDllSha256"], "Outcome": "Unknown", "Command": {}}
    scan = pub.output_size
    def monitor(work):
        if captured.errors:
            raise RuntimeError("Original TRX capture failed")
        current = fixture.stat()
        if (current.st_dev, current.st_ino) != (initial.st_dev, initial.st_ino):
            raise RuntimeError("Original fixture directory was replaced")
        fixture_usage, output_usage = inventory(fixture), inventory(run)
        used, output = fixture_usage["LogicalBytes"], output_usage["LogicalBytes"]
        allocated = fixture_usage["AllocatedBytes"] + output_usage["AllocatedBytes"]
        entries = fixture_usage["Entries"] + output_usage["Entries"]
        receipt["ObservedInventory"] = {"Fixture": fixture_usage, "Output": output_usage}
        if entries > 416 or allocated > RESERVATION or used + output > RESERVATION or PRESERVED + used + output > AGGREGATE:
            raise RuntimeError("Frozen ordinary-case bound exceeded; retained")
        if shutil.disk_usage(REPO).free < 1024**3 or list(opaque.iterdir()) or list(results.iterdir()) != [fifo]:
            raise RuntimeError("Output shape or free-space gate failed; retained")
        return scan(work)
    pub.output_size = monitor
    start = time.monotonic()
    try:
        captured.thread.start()
        argv = ["bwrap", "--unshare-user", "--cap-drop", "ALL", "--die-with-parent", "--ro-bind", "/", "/",
                "--bind", str(fixture), str(fixture), "--chdir", str(REPO), "--", str(sdk / "dotnet"),
                str(sdk / "sdk/10.0.402/vstest.console.dll"), str(DLL), "/Settings:" + str(settings),
                "/TestCaseFilter:" + CASE, "/ResultsDirectory:" + str(results),
                "/Logger:trx;LogFileName=" + str(fifo), "/Logger:console;verbosity=normal"]
        pub.execute(argv, env, run / "console.log", time.monotonic() + min(120, 900 - OLD_SECONDS - PREPARATION_CHARGE - ACCOUNTING_CHARGE - 5),
                    run, receipt["Command"], log_limit_bytes=STREAM)
        captured.finish()
        monitor(run)
        doc = ET.parse(captured.target)
        rows = doc.findall(".//t:UnitTestResult", NS)
        if len(rows) != 1 or rows[0].get("testName") != CASE or rows[0].get("testId") != identity["TestId"]:
            raise RuntimeError("Original single-case identity differs; no qualification")
        if rows[0].get("outcome") != "Passed" or receipt["Command"].get("exit") != 0 or receipt["Command"].get("settlement") != "LeaderJoined_GroupEmpty":
            raise RuntimeError("Original case or original process group did not pass")
        receipt.update(Outcome="Passed_OneOriginalCaseOnly", TrxSha256=digest(captured.target), TrxBytes=captured.bytes)
    except BaseException as error:
        receipt.update(Outcome="FailedOrUnconfirmed", ErrorType=type(error).__name__, Error=str(error))
        raise
    finally:
        pub.output_size = scan
        settle_case(receipt, captured, fixture, run / "result.json", start, sys.exc_info()[1])
        # No deletion of failure roots, original endpoints or retained fixtures.


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("inspect", "prepare", "execute"), nargs="?", default="inspect")
    args = parser.parse_args()
    if args.action == "inspect":
        print(json.dumps({"UidMapFilesystemReadonly": bool(os.statvfs("/proc/self/uid_map").f_flag & os.ST_RDONLY),
                          "RuntimeDiagnostic": uid_map_runtime(),
                          "ResourceProofComplete": RESOURCE_PROOF_COMPLETE,
                          "NextOriginalCase": CASE, "CoreCasesExecuted": 0, "ProposedCaseReservationBytes": RESERVATION,
                          "AggregateHeadroomBytes": AGGREGATE - PRESERVED,
                          "RemainingAfterConservativePreparationChargeSeconds": 900 - OLD_SECONDS - PREPARATION_CHARGE - ACCOUNTING_CHARGE}))
    elif args.action == "prepare":
        prepare(module())
    else:
        execute(module())


if __name__ == "__main__":
    main()
