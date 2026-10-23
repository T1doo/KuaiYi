"""One reviewed production overlay sample, reusing the original Windows job gates."""
import base64
import gzip
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.request
import zipfile

PARENT = "2ce69f410a6a6ca926eef5ca9a73d9271f15c069"
# Reviewed Begin clock and independent state observation; inputs match 3a47f9b.
# The exact parent identity and unchanged-input diff preserve depth-two checkout.
FIXED = PARENT
BRANCH = "refs/heads/dev/p7-fixed-signoff"
FILES = frozenset((".github/workflows/windows-warm-components.yml", "scripts/verify-settings-layout-windows.py",
                  "scripts/test_settings_layout_windows.py", "docs/progress.md", "docs/validation/overlay-stages-preparation.md"))
PROJECTS = {"KuaiYi.App", "KuaiYi.Contracts", "KuaiYi.Core", "KuaiYi.Imaging", "KuaiYi.NativeHost",
            "KuaiYi.OleTransfer", "KuaiYi.Providers", "KuaiYi.Windows"}


def zero_package_closure(repo, project):
    """This new CI mode explicitly requires a project-only restored closure, not a CLI-error exemption.

    Actual runtime packages still undergo online signature and vulnerability audit separately.
    No PackageReference is allowed in any of these nine locked projects.
    """
    projects = [repo / project, *(repo / "src" / name / (name + ".csproj") for name in sorted(PROJECTS))]
    records = []
    for path in projects:
        assets = json.loads((path.parent / "obj/project.assets.json").read_text(encoding="utf-8-sig"))
        lock = json.loads((path.parent / "packages.lock.json").read_text(encoding="utf-8-sig"))
        if Path(assets["project"]["restore"]["projectUniqueName"]).resolve() != path.resolve():
            raise RuntimeError("Restored project identity differs")
        libraries = assets["libraries"]
        if path == repo / project and set(libraries) != {name + "/1.0.0" for name in PROJECTS}:
            raise RuntimeError("Exact eight referenced projects required")
        if any(item.get("type") != "project" for item in libraries.values()):
            raise RuntimeError("Harness closure contains package libraries")
        if any(framework.get("dependencies") for framework in assets["project"]["frameworks"].values()):
            raise RuntimeError("Harness closure requests packages")
        if not assets["targets"] or any(set(target) != set(libraries) or any(item.get("type") != "project" for item in target.values()) for target in assets["targets"].values()):
            raise RuntimeError("Harness restored target differs from project-only inventory")
        allowed = {name.casefold() for name in PROJECTS}
        if any(name.split("/")[0].casefold() not in allowed for name in libraries):
            raise RuntimeError("Unexpected project in harness closure")
        if not lock.get("dependencies") or any(item.get("type") != "Project" or name not in allowed for group in lock["dependencies"].values() for name, item in group.items()):
            raise RuntimeError("Harness lock is not project-only")
        records.append({"Project": str(path.relative_to(repo)), "Packages": 0,
                        "AssetsSha256": hashlib.sha256((path.parent / "obj/project.assets.json").read_bytes()).hexdigest(),
                        "LockSha256": hashlib.sha256((path.parent / "packages.lock.json").read_bytes()).hexdigest()})
    return records


def load(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def hosted_context(env, platform, event):
    required = {"GITHUB_ACTIONS": "true", "GITHUB_REPOSITORY": "T1doo/KuaiYi", "GITHUB_REF": BRANCH,
                "GITHUB_EVENT_NAME": "push", "GITHUB_RUN_ATTEMPT": "1", "RUNNER_OS": "Windows"}
    if platform != "win32" or any(env.get(k) != v for k, v in required.items()):
        raise RuntimeError("Exact one-attempt public Windows context required")
    if not re.fullmatch(r"[0-9a-f]{40}", env.get("GITHUB_SHA", "")) or event.get("repository", {}).get("private") is not False:
        raise RuntimeError("Exact source and public standard runner required")


def source_gate(run, event):
    head = run(["git", "rev-parse", "HEAD"], "source-head", 15).strip()
    parents = run(["git", "rev-list", "--parents", "-n", "1", "HEAD"], "source-parent", 15).split()
    files = run(["git", "diff", "--name-only", PARENT, "HEAD"], "source-scope", 15).splitlines()
    if head != event or parents != [head, PARENT] or len(files) != len(FILES) or set(files) != FILES:
        raise RuntimeError("Exact reviewed parent/scope rejected")
    if run(["git", "status", "--porcelain=v1"], "source-clean", 15).strip():
        raise RuntimeError("Dirty source rejected")
    if run(["git", "diff", "--name-only", FIXED, "HEAD", "--", "src", "tests", "packaging", "tools/KuaiYi.GrowthHarness", "scripts/overlay_stage_runner.py", "global.json",
            "Directory.Build.props", "Directory.Packages.props", "NuGet.Config"], "fixed-product-source", 15).strip():
        raise RuntimeError("Fixed product inputs changed")
    return head


def compact_process_receipt(receipt):
    value = {key: item for key, item in receipt.items() if key != "OriginalResult"}
    if receipt.get("OriginalResult") is not None:
        value["OriginalResult"] = {key: item for key, item in receipt["OriginalResult"].items() if key != "Actual"}
    return value


def prepare_evidence(work, registry, is_linked, *, _raw_limit=32*1024*1024, _gzip_limit=4*1024*1024):
    """Freeze the complete set, including its manifest, before emitting any FILE."""
    if not 0 < _raw_limit <= 32*1024*1024 or not 0 < _gzip_limit <= 4*1024*1024 or not 1 <= len(registry) <= 96:
        raise RuntimeError("Original aggregate evidence bounds required")
    frozen, names, paths = [], set(), set()
    raw_total = 0
    for entry in registry:
        name, path = entry["Name"], work / entry["Log"]
        if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]{0,95}", name) or name in names or name == "evidence-manifest.json":
            raise RuntimeError("Duplicate or invalid evidence name")
        if any(is_linked(p) for p in (path, *path.parents)) or not path.is_file() or not path.resolve().is_relative_to(work.resolve()):
            raise RuntimeError("Registered evidence missing, linked or outside owned root: " + name)
        canonical = os.path.normcase(str(path.resolve()))
        if canonical in paths or path.stat().st_size > 16*1024*1024:
            raise RuntimeError("Duplicate path or oversized evidence: " + name)
        with path.open("rb") as stream:
            data = stream.read(16*1024*1024 + 1)
        digest = hashlib.sha256(data).hexdigest()
        if len(data) > 16*1024*1024 or entry["LogBytes"] != len(data) or entry["LogSha256"] != digest:
            raise RuntimeError("Registered evidence changed: " + name)
        names.add(name); paths.add(canonical)
        raw_total += len(data)
        if raw_total > _raw_limit:
            raise RuntimeError("Complete evidence raw aggregate exceeds bound; nothing emitted")
        frozen.append((name, data))
    manifest = [{"Name": name, "Bytes": len(data), "Sha256": hashlib.sha256(data).hexdigest()} for name, data in frozen]
    manifest_bytes = (json.dumps(manifest, indent=2) + "\n").encode()
    frozen.append(("evidence-manifest.json", manifest_bytes)); raw_total += len(manifest_bytes)
    if raw_total > _raw_limit:
        raise RuntimeError("Complete evidence including manifest exceeds raw bound; nothing emitted")
    prepared, gzip_total = [], 0
    for name, data in frozen:
        packed = gzip.compress(data, mtime=0); gzip_total += len(packed)
        if gzip_total > _gzip_limit:
            raise RuntimeError("Complete evidence gzip aggregate exceeds bound; nothing emitted")
        encoded = base64.b64encode(packed).decode("ascii")
        chunks = [encoded[i:i+8192] for i in range(0, len(encoded), 8192)]
        prepared.append(({"Name": name, "Bytes": len(data), "Sha256": hashlib.sha256(data).hexdigest(),
                          "GzipBytes": len(packed), "GzipSha256": hashlib.sha256(packed).hexdigest(), "Chunks": len(chunks)}, chunks))
    with (work / "evidence-manifest.json").open("xb") as stream:
        stream.write(manifest_bytes)
    return prepared, raw_total, gzip_total


def emit_prepared(prepared, emit):
    # Uses only frozen bytes. A transport failure after a prefix remains incomplete.
    for header, chunks in prepared:
        emit("KUAIYI_CI_FILE " + json.dumps(header))
        for index, chunk in enumerate(chunks, 1):
            emit("KUAIYI_CI_CHUNK " + json.dumps({"Name": header["Name"], "Index": index, "Total": len(chunks), "Data": chunk}))


def verify_overlay_result(receipt, directory, source, file_hash, is_linked):
    if (receipt.get("Status") != "Pass_LimitedProcess" or receipt.get("SourceHead") != source or
            receipt.get("CleanupConfirmed") is not True or receipt.get("OriginalExitConfirmed") is not True or receipt.get("ExitCode") != 0):
        raise RuntimeError("Original process/pipe/state settlement did not pass")
    original = receipt["OriginalResult"]; actual = original["Actual"]
    result_path = directory / "overlay-stages-result.json"
    if any(is_linked(p) for p in (result_path, *result_path.parents)):
        raise RuntimeError("Linked original result refused")
    if result_path.stat().st_size != original["Bytes"] or file_hash(result_path) != original["Sha256"]:
        raise RuntimeError("Original result differs from owned wrapper receipt")
    stage_names = ["SeedOpen", "LongStreamGrow", "Close", "Reopen", "CloseUntilReverse", "ReverseToOpen", "CloseToCompact", "OpenUntilReverse", "ReverseToClose"]
    phases = actual["Phases"]
    if (len(phases) != 18 or [p["Id"] for p in phases] != list(range(1, 19)) or
            [p["Mode"] for p in phases] != ["Timing"]*9+["Raster"]*9 or [p["Name"] for p in phases] != stage_names*2 or
            any(p.get("CompletedQpc") is None or p.get("SawIntermediate") is not True for p in phases)):
        raise RuntimeError("All eighteen actual observed phases are required")
    raw_path = directory / "overlay-raw-frames.json"
    if any(is_linked(p) for p in (raw_path, *raw_path.parents)) or raw_path.stat().st_size > 16*1024*1024:
        raise RuntimeError("Raw frame evidence bound")
    with raw_path.open("rb") as stream:
        raw_bytes = stream.read(16*1024*1024+1)
    if len(raw_bytes) > 16*1024*1024:
        raise RuntimeError("Raw frame evidence grew beyond bound")
    raw = json.loads(raw_bytes)
    if not 0 < len(raw) == actual["RawCallbacks"] <= 10000 or [r["Sequence"] for r in raw] != list(range(1, len(raw)+1)):
        raise RuntimeError("Original callback sequence incomplete")
    images, seen, names, total = actual["EvidenceFiles"], set(), set(), 0
    expected = {(phase, kind) for phase in range(10, 19) for kind in ("intermediate", "terminal") if kind != "terminal" or phase not in (14, 17)}
    if len(images) != len(expected):
        raise RuntimeError("Complete actual intermediate/terminal raster collection required")
    for image in images:
        name = image["File"]; pair = image["Before"]["Frame"]["Phase"], image["Kind"]
        match = re.fullmatch(r"overlay-[0-9]{2}-phase([0-9]{1,2})-(intermediate|terminal)\.png", name)
        if (not match or (int(match[1]), match[2]) != pair or name in names or pair not in expected or pair in seen or image["Stable"] is not True or
                image["Before"]["Frame"]["Mode"] != "Raster" or image["After"]["Phase"] != pair[0] or image["After"]["Mode"] != "Raster"):
            raise RuntimeError("Actual raster phase binding refused")
        seen.add(pair); names.add(name); path = directory / name
        if any(is_linked(p) for p in (path, *path.parents)) or path.stat().st_size > 768*1024:
            raise RuntimeError("Linked or oversized actual raster")
        with path.open("rb") as stream:
            data = stream.read(768*1024+1)
        if (not 0 < len(data) == image["Bytes"] <= 768*1024 or hashlib.sha256(data).hexdigest() != image["Sha256"] or
                data[:8] != b"\x89PNG\r\n\x1a\n" or len(data) < 24 or data[12:16] != b"IHDR" or
                int.from_bytes(data[16:20], "big") != image["PixelWidth"] or int.from_bytes(data[20:24], "big") != image["PixelHeight"]):
            raise RuntimeError("Actual raster bytes/hash/dimensions differ")
        total += len(data)
    if total > 8*1024*1024 or {p.name for p in directory.glob("*.png")} != {i["File"] for i in images}:
        raise RuntimeError("Raster aggregate or complete collection differs")
    return {"ObservedPhases": len(phases), "RawCallbacks": len(raw), "Rasters": len(images), "RasterBytes": total,
            "OriginalResultBytes": original["Bytes"], "OriginalResultSha256": original["Sha256"],
            "RawFrameBytes": len(raw_bytes), "RawFrameSha256": hashlib.sha256(raw_bytes).hexdigest(),
            "Scope": "ActualWpfContentAndGeometry_NotDwmFpsOrFullB07"}


def execute(repo, env):
    helper = load(repo / "scripts/verify-session-runtime-windows.py", "original_windows_gates")
    installer = load(repo / "scripts/install-session-sdk-windows.py", "approved_windows_installer")
    overlay_runner = load(repo / "scripts/overlay_stage_runner.py", "owned_overlay_runner")
    policy = helper.load_policy(repo)
    policy.require_signature_environment(env)
    work = repo / "artifacts/overlay-stages-windows" / os.urandom(16).hex()
    if any(helper.linked(p) for p in (work, *work.parents)):
        raise RuntimeError("Linked job directory rejected")
    work.mkdir(parents=True, exist_ok=False)
    report = {"Kind": "ProductionOverlayStageReview", "Status": "Blocked", "Stage": "Source", "FixedProductSource": FIXED,
              "Commands": [], "RuntimePackages": [], "EvidenceFiles": [], "Package": "Hold", "ProductionReuse": "Off",
              "ModelCalls": 0, "ProductionPins": "Unavailable", "DwmFluidity": "NotRun", "SystemDpiAcceptance": "NotRun", "NewAttempt": 1}
    deadline = time.monotonic() + 12 * 60
    code = 2
    owner = None  # Retained through the final complete evidence report.
    authored = {}

    def write_json(name, value):
        data = (json.dumps(value, indent=2) + "\n").encode()
        if len(data) > 16*1024*1024:
            raise RuntimeError("Owned report JSON bound")
        with (work / name).open("xb") as stream:
            stream.write(data)
        authored[name] = {"LogBytes": len(data), "LogSha256": hashlib.sha256(data).hexdigest()}

    def report_receipt(receipt):
        print("KUAIYI_OVERLAY_PROCESS_STATE " + json.dumps(compact_process_receipt(receipt)), flush=True)

    def run(argv, name, timeout=120):
        remaining = int(deadline - time.monotonic())
        if remaining <= 0:
            raise RuntimeError("Attempt budget exhausted")
        command = {"Name": name, "Argv": argv, "Exit": None, "Log": name + ".log"}
        report["Commands"].append(command)
        log = work / command["Log"]
        try:
            with log.open("xb") as output:
                result = subprocess.run(argv, cwd=repo, env=env, stdout=output, stderr=subprocess.STDOUT, timeout=min(timeout, remaining))
            command["Exit"] = result.returncode
        except subprocess.TimeoutExpired:
            command.update(Outcome="TimedOut", OriginalChildTreeCleanup="NotConfirmed_NoSuccessClaim")
            raise
        finally:
            if log.is_file():
                command.update(LogBytes=log.stat().st_size, LogSha256=helper.file_hash(log))
        if log.stat().st_size > 4 * 1024 * 1024:
            raise RuntimeError("Command output exceeds bound")
        text = log.read_text(encoding="utf-8-sig")
        if result.returncode:
            report["FailedCommand"] = name
            raise RuntimeError("Command failed: " + name)
        return text

    try:
        report["DeliverySource"] = source_gate(run, env["GITHUB_SHA"])
        report["Stage"] = "DesktopPreflight"
        # This remains subprocess.run's original direct child and inherited log.
        # Timeout does not establish descendant cleanup, or interrupt Popen creation.
        run(["pwsh", "-NoProfile", "-NonInteractive", "-File", str(repo / "scripts/probe-settings-desktop.ps1"), "-Output", str(work / "desktop-facts.json")], "desktop-preflight", 45)
        facts = work / "desktop-facts.json"
        authored[facts.name] = {"LogBytes": facts.stat().st_size, "LogSha256": helper.file_hash(facts)}
        contract = json.loads((repo / "packaging/session-test.json").read_text())
        reviewed = json.loads((repo / "packaging/session-runtime-audit/runtime-pack-integrity.json").read_text())["Packages"]
        locked = json.loads((repo / "packaging/session-runtime-audit/packages.lock.json").read_text())["dependencies"]["net10.0"]
        if set(reviewed) != set(policy.RUNTIME_IDS) or any(reviewed[k]["Version"] != contract["runtime"] or reviewed[k]["NuGetContentHash"] != locked[k]["contentHash"] or reviewed[k]["Source"] != policy.NUGET_SOURCE for k in reviewed):
            raise RuntimeError("Original runtime pins differ")
        report["Stage"] = "OnlineSignatures"
        nuget = shutil.which("nuget.exe")
        if not nuget:
            raise RuntimeError("Original preinstalled verifier missing")
        nuget, report["VerifierSha256"] = helper.resolve_verifier(nuget, env, repo, report)
        helper.require_verifier_version(run([nuget, "help", "-ForceEnglishOutput", "-NonInteractive"], "verifier-version", 30))
        opener = urllib.request.build_opener(installer.NoRedirect())
        for identity in policy.RUNTIME_IDS:
            pin = reviewed[identity]; name = identity.lower(); url = f"https://api.nuget.org/v3-flatcontainer/{name}/{contract['runtime']}/{name}.{contract['runtime']}.nupkg"
            packed = work / (name + ".nupkg"); size = 0
            with opener.open(url, timeout=45) as response, packed.open("xb") as stream:
                while block := response.read(1024 * 1024):
                    size += len(block)
                    if size > 50 * 1024 * 1024 or time.monotonic() >= deadline:
                        raise RuntimeError("Runtime download bound")
                    stream.write(block)
            helper.package_identity(packed, identity, contract["runtime"], pin["ArchiveSha512"])
            text = run([nuget, "verify", "-All", str(packed), "-ForceEnglishOutput", "-NonInteractive", "-Verbosity", "normal"], "verify-" + identity)
            helper.signature_gate(policy, text, identity, contract["runtime"], pin["NuGetContentHash"])
            report["RuntimePackages"].append({"Id": identity, "Signature": "Pass", "Version": contract["runtime"], "ArchiveSha512": pin["ArchiveSha512"]})
        report["Stage"] = "ApprovedSdk"
        sdk = helper.install_sdk(repo, contract, work, deadline, report)
        policy.require_safe_sdk(contract, sdk / "dotnet.exe")
        env = dict(env, DOTNET_ROOT=str(sdk), DOTNET_ROOT_X64=str(sdk), DOTNET_HOST_PATH=str(sdk / "dotnet.exe"), PATH=str(sdk) + os.pathsep + env.get("PATH", ""),
                   DOTNET_CLI_HOME=str(work / "cli"), NUGET_PACKAGES=str(work / "packages"), NUGET_HTTP_CACHE_PATH=str(work / "http"), EnableWindowsTargeting="true")
        dotnet = str(sdk / "dotnet.exe")
        if run([dotnet, "--version"], "sdk-version", 30).strip() != contract["sdk"]:
            raise RuntimeError("SDK version differs")
        config = ["--configfile", str(repo / "NuGet.Config"), *helper.AUDIT]
        props = ["-p:EnableWindowsTargeting=true", "-p:RuntimeFrameworkVersion=" + contract["runtime"]]
        report["Stage"] = "LockedRestoreAuditBuild"
        project = "packaging/session-runtime-audit/session-runtime-audit.csproj"
        run([dotnet, "restore", project, "--locked-mode", *config, *props], "runtime-restore")
        inventory = json.loads(run([dotnet, "list", project, "package", "--include-transitive", "--format", "json", "--no-restore"], "runtime-inventory"))
        audit = json.loads(run([dotnet, "list", project, "package", "--vulnerable", "--include-transitive", "--format", "json", "--no-restore", "--source", policy.NUGET_SOURCE], "runtime-audit"))
        report["AuditCoverage"] = [policy.require_no_advisories(audit, repo / project, inventory, contract["runtime"])]
        project = "tools/KuaiYi.GrowthHarness/KuaiYi.GrowthHarness.csproj"
        run([dotnet, "restore", project, "--locked-mode", *config, *props], "harness-restore")
        report["HarnessProjectOnlyClosure"] = zero_package_closure(repo, project)
        run([dotnet, "build", project, "--no-restore", "-c", "Release", "-m:1", "-nr:false", "-p:UseSharedCompilation=false", *props], "harness-build", 180)
        target = repo / "tools/KuaiYi.GrowthHarness/bin/Release/net10.0-windows/win-x64"
        report["Images"] = "NotRun_NoImageInput"
        report["AppUi"] = "NotRun"; report["Worker"] = "NotRun"
        # Physical checkout hashes describe CRLF on Windows; source_gate binds canonical product inputs to FIXED.
        paths = run(["git", "ls-files", "src", "packaging", "tools/KuaiYi.GrowthHarness", "scripts/overlay_stage_runner.py", "scripts/verify-settings-layout-windows.py", "scripts/install-session-sdk-windows.py", "scripts/prepare-session-test.py", "scripts/verify-session-runtime-windows.py", "global.json", "Directory.Build.props", "Directory.Packages.props", "NuGet.Config"], "input-paths", 15).splitlines()
        inputs = {p: helper.file_hash(repo / p) for p in paths}
        write_json("checkout-inputs.json", inputs)
        binaries = {str(p.relative_to(target)): helper.file_hash(p) for p in target.rglob("*") if p.is_file()}
        write_json("binary-inputs.json", binaries)
        report["Stage"] = "ProductionOverlayStages"
        if deadline - time.monotonic() < 65:
            raise RuntimeError("Insufficient remaining budget for original 60s process observation")
        owner, receipt = overlay_runner.run_overlay(target / "KuaiYi.GrowthHarness.exe", work, env["GITHUB_SHA"], env, report_receipt)
        write_json("overlay-process-receipt.json", compact_process_receipt(receipt))
        report["ProcessStatus"] = receipt["Status"]
        report["Observed"] = verify_overlay_result(receipt, owner.directory, env["GITHUB_SHA"], helper.file_hash, helper.linked)
        if {p: helper.file_hash(repo / p) for p in paths} != inputs or {str(p.relative_to(target)): helper.file_hash(p) for p in target.rglob("*") if p.is_file()} != binaries:
            raise RuntimeError("Source/binary inputs changed during original observation")
        report.update(Status="Pass_LimitedStages", Stage="Completed", AppUi="ActualProductionShell_ContentAndGeometryOnly", Cleanup="OriginalHarnessExitedAndPipesSettled")
        code = 0
    except urllib.error.HTTPError as error:
        report.update(Reason="OfficialSourceHttpFailure_NoRetry", HttpStatus=error.code)
    except Exception as error:
        report.update(Reason=type(error).__name__, Error=str(error) if isinstance(error, RuntimeError) else None)
    finally:
        registry = list(report["Commands"])
        report["NotProduced"] = []
        try:
            candidates = [(name, work / name) for name in ("checkout-inputs.json", "binary-inputs.json", "desktop-facts.json", "overlay-process-receipt.json")]
            expected = dict(authored)
            if owner is not None:
                candidates.extend(("overlay-" + name, owner.directory / name) for name in ("stdout.log", "stderr.log"))
                candidates.extend((name, owner.directory / name) for name in ("overlay-stages-result.json", "overlay-raw-frames.json"))
                # Include every generated PNG, even on a failed partial run. Never select only green frames.
                image_paths = {p.name: p for p in owner.directory.glob("*.png")}
                for label, stream in owner.streams.items():
                    if stream.get("LogFileSha256") is not None:
                        expected["overlay-" + label + ".log"] = {"LogBytes": stream["LogFileBytes"], "LogSha256": stream["LogFileSha256"]}
                if owner.result is not None:
                    expected["overlay-stages-result.json"] = {"LogBytes": owner.result["Bytes"], "LogSha256": owner.result["Sha256"]}
                    for image in owner.result["Actual"].get("EvidenceFiles", []):
                        name = image["File"]
                        if not re.fullmatch(r"overlay-[0-9]{2}-phase[0-9]{1,2}-(intermediate|terminal)\.png", name):
                            raise RuntimeError("Untrusted image evidence filename")
                        image_paths[name] = owner.directory / name
                        expected[name] = {"LogBytes": image["Bytes"], "LogSha256": image["Sha256"]}
                if report.get("Observed"):
                    expected["overlay-raw-frames.json"] = {"LogBytes": report["Observed"]["RawFrameBytes"], "LogSha256": report["Observed"]["RawFrameSha256"]}
                candidates.extend(sorted(image_paths.items()))
                candidates.extend((name, owner.directory / name) for name in ("overlay-deadline-pending.json", "overlay-emergency.json") if (owner.directory / name).exists())
            else:
                report["NotProduced"].append("OverlayOwnerAndFrames")
            for name, path in candidates:
                if name in expected:
                    # An already acknowledged file disappearing is incomplete, never NotProduced.
                    registry.append({"Name": name, "Log": str(path.relative_to(work)), **expected[name], "EvidenceOnly": True})
                    continue
                if not path.exists():
                    report["NotProduced"].append(name)
                    continue
                if any(helper.linked(p) for p in (path, *path.parents)) or not path.is_file() or path.stat().st_size > 16*1024*1024:
                    raise RuntimeError("Unsafe or oversized partial evidence")
                entry = {"Name": name, "Log": str(path.relative_to(work)), "LogBytes": path.stat().st_size,
                         "LogSha256": helper.file_hash(path), "EvidenceOnly": True}
                registry.append(entry)
            report["EvidenceStatus"] = "Prepared_NotYetEmitted"
            write_json("overlay-run-summary.json", report)
            path = work / "overlay-run-summary.json"
            registry.append({"Name": path.name, "Log": path.name, "LogBytes": path.stat().st_size, "LogSha256": helper.file_hash(path)})
            prepared, raw_total, gzip_total = prepare_evidence(work, registry, helper.linked)
            emit_prepared(prepared, helper.emit_ci_line)
            terminal = {"Kind": report["Kind"], "Status": report["Status"], "SourceHead": env["GITHUB_SHA"], "FixedProductSource": FIXED,
                        "EvidenceStatus": "EmittedToJobLog", "EvidenceRawBytes": raw_total, "EvidenceGzipBytes": gzip_total,
                        "EvidenceFiles": [header for header, _ in prepared], "Observed": report.get("Observed"),
                        "EvidenceRetrieval": "NotConfirmed_VerifyEveryChunkAndHash", "Cleanup": report.get("Cleanup")}
        except Exception as error:
            code = 2
            terminal = {"Kind": report["Kind"], "Status": "EvidenceIncomplete", "SourceHead": env["GITHUB_SHA"],
                        "EvidenceStatus": "Incomplete", "Error": str(error), "OriginalRunStatus": report["Status"],
                        "PartialFiles": "Retained_NoRetry_NoFullRecoveryClaim"}
        print("KUAIYI_OVERLAY_STAGES " + json.dumps(terminal), flush=True)
    return code


def main():
    try:
        if len(sys.argv) != 1:
            raise RuntimeError("No overrides accepted")
        event_path = Path(os.environ["GITHUB_EVENT_PATH"])
        if event_path.stat().st_size > 1024 * 1024:
            raise RuntimeError("Event metadata bound")
        hosted_context(os.environ, sys.platform, json.loads(event_path.read_text(encoding="utf-8")))
        return execute(Path(__file__).resolve().parent.parent, dict(os.environ))
    except Exception as error:
        print("KUAIYI_OVERLAY_STAGES " + json.dumps({"Status": "Blocked", "Reason": type(error).__name__, "Product": "NotRun", "Package": "Hold"}), flush=True)
        return 2


if __name__ == "__main__":
    sys.exit(main())
