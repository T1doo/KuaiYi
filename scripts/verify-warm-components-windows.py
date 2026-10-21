"""One public Windows component attempt, with the already approved SDK/signature/audit gates."""
import base64
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

FIXED = "92e3548845bd07ee7717b53df9588c412d128f9d"
PARENT = "8adf512bb30ba855ced011ea156327dd99237a6c"
BRANCH = "refs/heads/dev/p7-fixed-signoff"
FILES = frozenset((".github/workflows/windows-warm-components.yml", "scripts/verify-warm-components-windows.py",
                  "tools/KuaiYi.GrowthHarness/WarmWindowsProbe.cs", "tools/KuaiYi.GrowthHarness/WarmWindowsPipeProbe.cs",
                  "docs/progress.md", "docs/validation/warm-components-windows.md"))


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
    if run(["git", "diff", "--name-only", FIXED, "HEAD", "--", "src", "tests", "packaging", "global.json",
            "Directory.Build.props", "Directory.Packages.props", "NuGet.Config"], "fixed-product-source", 15).strip():
        raise RuntimeError("Fixed product inputs changed")
    return head


def deploy(default_app, worker, target, helper):
    """Explicit build closure copy, never overwrites different bytes or merges a publish directory blindly."""
    records = []
    inputs = [default_app / "KuaiYi.App.exe", default_app / "KuaiYi.App.runtimeconfig.json"]
    worker_files = sorted(p for p in worker.rglob("*") if p.is_file())
    if not worker_files or len(worker_files) > 512 or sum(p.stat().st_size for p in worker_files) > 256 * 1024 * 1024:
        raise RuntimeError("Worker build closure exceeds bound")
    for path in [*inputs, *worker_files]:
        if any(helper.linked(p) for p in (path, *path.parents)) or not path.is_file():
            raise RuntimeError("Build payload missing or linked")
        relative = path.relative_to(worker) if path in worker_files else Path(path.name)
        destination = target / relative
        if any(helper.linked(p) for p in (destination, *destination.parents)):
            raise RuntimeError("Deployment target is linked")
        digest = helper.file_hash(path)
        if destination.exists():
            if not destination.is_file() or helper.file_hash(destination) != digest:
                raise RuntimeError("Build closure collision differs: " + str(relative))
            action = "ExistingExactBytes"
        else:
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(path, destination)
            if helper.file_hash(destination) != digest:
                raise RuntimeError("Copied payload differs")
            action = "CopiedFresh"
        records.append({"Path": str(relative), "Bytes": path.stat().st_size, "Sha256": digest, "Action": action})
    for name in ("KuaiYi.App.dll", "KuaiYi.ImageWorker.exe", "KuaiYi.ImageWorker.dll", "KuaiYi.ImageWorker.deps.json",
                 "KuaiYi.ImageWorker.runtimeconfig.json", "SkiaSharp.dll", "libSkiaSharp.dll"):
        if not (target / name).is_file():
            raise RuntimeError("Deployed exact closure incomplete: " + name)
    if helper.file_hash(target / "KuaiYi.App.dll") != helper.file_hash(default_app / "KuaiYi.App.dll"):
        raise RuntimeError("Default App differs from harness referenced App")
    return records


def execute(repo, env):
    helper = load(repo / "scripts/verify-session-runtime-windows.py", "original_windows_gates")
    installer = load(repo / "scripts/install-session-sdk-windows.py", "approved_windows_installer")
    policy = helper.load_policy(repo)
    policy.require_signature_environment(env)
    work = repo / "artifacts/warm-components-windows" / os.urandom(16).hex()
    if any(helper.linked(p) for p in (work, *work.parents)):
        raise RuntimeError("Linked job directory rejected")
    work.mkdir(parents=True, exist_ok=False)
    report = {"Kind": "WarmWindowsComponents", "Status": "Blocked", "Stage": "Source", "FixedProductSource": FIXED,
              "Commands": [], "RuntimePackages": [], "EvidenceFiles": [], "Package": "Hold", "ProductionReuse": "Off",
              "ModelCalls": 0, "PositiveWarm": "Blocked_NoMediumEntry", "Explorer": "NotRun", "NewAttempt": 1}
    deadline = time.monotonic() + 13 * 60
    code = 2

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
        projects = [("runtime", "packaging/session-runtime-audit/session-runtime-audit.csproj"),
                    ("harness", "tools/KuaiYi.GrowthHarness/KuaiYi.GrowthHarness.csproj"), ("worker", "src/KuaiYi.ImageWorker/KuaiYi.ImageWorker.csproj")]
        report["AuditCoverage"] = []
        for name, project in projects:
            run([dotnet, "restore", project, "--locked-mode", *config, *props], name + "-restore")
            inventory = json.loads(run([dotnet, "list", project, "package", "--include-transitive", "--format", "json", "--no-restore"], name + "-inventory"))
            audit = json.loads(run([dotnet, "list", project, "package", "--vulnerable", "--include-transitive", "--format", "json", "--no-restore", "--source", policy.NUGET_SOURCE], name + "-audit"))
            assets = json.loads((repo / project).parent.joinpath("obj/project.assets.json").read_text())
            report["AuditCoverage"].append(policy.require_no_advisories(audit, repo / project, inventory, contract["runtime"] if name == "runtime" else None, assets))
            if name != "runtime":
                run([dotnet, "build", project, "--no-restore", "-c", "Release", "-m:1", "-nr:false", "-p:UseSharedCompilation=false", *props], name + "-build")
        target = repo / "tools/KuaiYi.GrowthHarness/bin/Release/net10.0-windows/win-x64"
        report["Stage"] = "ExactDeployment"
        report["Deployment"] = deploy(repo / "src/KuaiYi.App/bin/Release/net10.0-windows/win-x64", repo / "src/KuaiYi.ImageWorker/bin/Release/net10.0/win-x64", target, helper)
        # Physical checkout hashes describe CRLF on Windows; source_gate binds canonical product inputs to FIXED.
        paths = run(["git", "ls-files", "src", "packaging", "tools/KuaiYi.GrowthHarness", "tests/fixtures/images", "scripts/install-session-sdk-windows.py", "scripts/prepare-session-test.py", "scripts/verify-session-runtime-windows.py", "global.json", "Directory.Build.props", "Directory.Packages.props", "NuGet.Config"], "input-paths", 15).splitlines()
        inputs = {p: helper.file_hash(repo / p) for p in paths}
        (work / "checkout-inputs.json").write_text(json.dumps(inputs, indent=2) + "\n")
        report["BinaryInputs"] = {str(p.relative_to(target)): helper.file_hash(p) for p in target.rglob("*") if p.is_file()}
        report["Stage"] = "RealWindowsHarness"
        run([str(target / "KuaiYi.GrowthHarness.exe"), "--ci-local-image-shell", str(work / "probe-result.json"), str(repo / "tests/fixtures/images")], "actual-components", 120)
        actual = json.loads((work / "probe-result.json").read_text())
        if actual.get("Status") != "Pass_ComponentsOnly" or len(actual.get("Cases", [])) != 3 or actual.get("Errors") or not actual.get("OriginalCloseJoined"):
            raise RuntimeError("Original component assertions did not pass")
        if {p: helper.file_hash(repo / p) for p in paths} != inputs:
            raise RuntimeError("Source changed during actual execution")
        report.update(Status="Pass_ComponentsOnly", Stage="Completed", Actual=actual)
        code = 0
    except urllib.error.HTTPError as error:
        report.update(Reason="OfficialSourceHttpFailure_NoRetry", HttpStatus=error.code)
    except Exception as error:
        report.update(Reason=type(error).__name__, Error=str(error) if isinstance(error, RuntimeError) else None)
    finally:
        registry = list(report["Commands"])
        for name in ("probe-result.json", "probe-result.json.phases.log", "checkout-inputs.json", "synthetic-two-frame.gif"):
            path = work / name
            if path.is_file():
                registry.append({"Name": name, "Log": name, "LogBytes": path.stat().st_size, "LogSha256": helper.file_hash(path)})
        evidence = {"Commands": registry};helper.emit_evidence(work, evidence)
        report.update({k: v for k, v in evidence.items() if k != "Commands"})
        if report["EvidenceStatus"] != "EmittedToJobLog":
            report["Status"], code = "EvidenceIncomplete", 2
        print("KUAIYI_WARM_WINDOWS_COMPONENTS " + json.dumps(report), flush=True)
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
        print("KUAIYI_WARM_WINDOWS_COMPONENTS " + json.dumps({"Status": "Blocked", "Reason": type(error).__name__, "Product": "NotRun", "Package": "Hold"}), flush=True)
        return 2


if __name__ == "__main__":
    sys.exit(main())
