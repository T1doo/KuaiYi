"""One bounded production SettingsWindow raster attempt; no product entry or registration."""
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

PARENT = "3d775f8140ea711558fdee4fe2192dcb28818d22"
FIXED = PARENT
BRANCH = "refs/heads/dev/p7-fixed-signoff"
FILES = frozenset((".github/workflows/windows-warm-components.yml", "scripts/verify-settings-layout-windows.py",
                  "scripts/probe-settings-desktop.ps1", "scripts/test_settings_layout_windows.py",
                  "tools/KuaiYi.GrowthHarness/Program.cs", "tools/KuaiYi.GrowthHarness/SettingsLayoutProbe.cs",
                  "docs/progress.md", "docs/validation/settings-raster-windows.md"))
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
    if run(["git", "diff", "--name-only", FIXED, "HEAD", "--", "src", "tests", "packaging", "global.json",
            "Directory.Build.props", "Directory.Packages.props", "NuGet.Config"], "fixed-product-source", 15).strip():
        raise RuntimeError("Fixed product inputs changed")
    return head


def execute(repo, env):
    helper = load(repo / "scripts/verify-session-runtime-windows.py", "original_windows_gates")
    installer = load(repo / "scripts/install-session-sdk-windows.py", "approved_windows_installer")
    policy = helper.load_policy(repo)
    policy.require_signature_environment(env)
    work = repo / "artifacts/settings-layout-windows" / os.urandom(16).hex()
    if any(helper.linked(p) for p in (work, *work.parents)):
        raise RuntimeError("Linked job directory rejected")
    work.mkdir(parents=True, exist_ok=False)
    report = {"Kind": "ProductionSettingsRasterReview", "Status": "Blocked", "Stage": "Source", "FixedProductSource": FIXED,
              "Commands": [], "RuntimePackages": [], "EvidenceFiles": [], "Package": "Hold", "ProductionReuse": "Off",
              "ModelCalls": 0, "ProductionPins": "Unavailable", "DwmFluidity": "NotRun", "SystemDpiAcceptance": "NotRun", "NewAttempt": 1}
    deadline = time.monotonic() + 12 * 60
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
        run(["pwsh", "-NoProfile", "-NonInteractive", "-File", str(repo / "scripts/probe-settings-desktop.ps1"), "-Output", str(work / "desktop-facts.json")], "desktop-preflight", 45)
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
        report["AppUi"] = report["Worker"] = "NotRun"
        # Physical checkout hashes describe CRLF on Windows; source_gate binds canonical product inputs to FIXED.
        paths = run(["git", "ls-files", "src", "packaging", "tools/KuaiYi.GrowthHarness", "scripts/install-session-sdk-windows.py", "scripts/prepare-session-test.py", "scripts/verify-session-runtime-windows.py", "global.json", "Directory.Build.props", "Directory.Packages.props", "NuGet.Config"], "input-paths", 15).splitlines()
        inputs = {p: helper.file_hash(repo / p) for p in paths}
        (work / "checkout-inputs.json").write_text(json.dumps(inputs, indent=2) + "\n")
        report["BinaryInputs"] = {str(p.relative_to(target)): helper.file_hash(p) for p in target.rglob("*") if p.is_file()}
        report["Stage"] = "ProductionSettingsWindowRaster"
        if deadline - time.monotonic() < 65:
            raise RuntimeError("Insufficient remaining budget for original 60s process observation")
        run([str(target / "KuaiYi.GrowthHarness.exe"), "--ci-settings-layout", str(work)], "actual-settings-layout", 60)
        actual = json.loads((work / "settings-result.json").read_text())
        if actual.get("Kind") != "ProductionSettingsWpfRaster" or actual.get("Status") != "Pass_LimitedRaster" or len(actual.get("Scenes", [])) != 4 or actual.get("Errors") or actual.get("CleanupConfirmed") is not True or actual.get("SourceHead") != env["GITHUB_SHA"]:
            raise RuntimeError("Original settings raster probe did not pass")
        expected = ["01-wide", "02-narrow", "03-large-font", "04-short-recovery"]
        if [scene["Name"] for scene in actual["Scenes"]] != expected:
            raise RuntimeError("Exact four scenarios required")
        total = 0
        for scene in actual["Scenes"]:
            path = work / (scene["Name"] + ".png")
            if scene["File"] != path.name or path.stat().st_size != scene["Bytes"] or helper.file_hash(path) != scene["Sha256"] or not 0 < scene["Bytes"] <= 768 * 1024 or path.read_bytes()[:8] != b"\x89PNG\r\n\x1a\n":
                raise RuntimeError("Raster file identity or bound failed")
            total += scene["Bytes"]
        if total > 2 * 1024 * 1024 or {p: helper.file_hash(repo / p) for p in paths} != inputs:
            raise RuntimeError("Evidence/source bounds differ after execution")
        report.update(Status="Pass_LimitedRaster", Stage="Completed", Actual=actual, Cleanup="OriginalHarnessExited")
        code = 0
    except urllib.error.HTTPError as error:
        report.update(Reason="OfficialSourceHttpFailure_NoRetry", HttpStatus=error.code)
    except Exception as error:
        report.update(Reason=type(error).__name__, Error=str(error) if isinstance(error, RuntimeError) else None)
    finally:
        registry = list(report["Commands"])
        for name in ("settings-result.json", "checkout-inputs.json", "desktop-facts.json", "01-wide.png", "02-narrow.png", "03-large-font.png", "04-short-recovery.png"):
            path = work / name
            if path.is_file():
                registry.append({"Name": name, "Log": name, "LogBytes": path.stat().st_size, "LogSha256": helper.file_hash(path), "EvidenceOnly": True})
        evidence = {"Commands": registry};helper.emit_evidence(work, evidence)
        report.update({k: v for k, v in evidence.items() if k != "Commands"})
        if report["EvidenceStatus"] != "EmittedToJobLog":
            report["Status"], code = "EvidenceIncomplete", 2
        print("KUAIYI_SETTINGS_LAYOUT " + json.dumps(report), flush=True)
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
        print("KUAIYI_SETTINGS_LAYOUT " + json.dumps({"Status": "Blocked", "Reason": type(error).__name__, "Product": "NotRun", "Package": "Hold"}), flush=True)
        return 2


if __name__ == "__main__":
    sys.exit(main())
