"""One public Windows attempt with the approved job-local SDK. No publishing or CI API calls."""
import base64
from collections import Counter
import gzip
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import stat
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid
import xml.etree.ElementTree as xml
import zipfile


REPOSITORY = "T1doo/KuaiYi"
BRANCH = "refs/heads/dev/session-runtime-signature-windows"
PARENT = "98c3f27d07ee392bf98e647861596812c2d63a39"
NU_VERSION = "7.9.0.83"
FILES = frozenset((".github/workflows/windows-session-runtime.yml", "scripts/verify-session-runtime-windows.py",
                   "scripts/test_session_runtime_windows.py", "docs/progress.md"))
AUDIT = ["-p:NuGetAudit=true", "-p:NuGetAuditMode=all", "-p:NuGetAuditLevel=low", "-p:TreatWarningsAsErrors=true"]


def linked(path):
    if path.is_symlink():
        return True
    try:
        entry = path.lstat()
    except FileNotFoundError:
        return False
    return bool(getattr(entry, "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT)


def file_hash(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def load_policy(repo):
    spec = importlib.util.spec_from_file_location("session_preparation", repo / "scripts/prepare-session-test.py")
    policy = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(policy)
    return policy


def hosted_context(env, platform):
    expected = {"GITHUB_ACTIONS": "true", "GITHUB_REPOSITORY": REPOSITORY, "GITHUB_REF": BRANCH,
                "GITHUB_EVENT_NAME": "push", "GITHUB_RUN_ATTEMPT": "1", "RUNNER_OS": "Windows"}
    if platform != "win32" or any(env.get(key) != value for key, value in expected.items()):
        raise RuntimeError("Exact single-attempt hosted Windows context required")
    if not re.fullmatch(r"[0-9a-f]{40}", env.get("GITHUB_SHA", "")):
        raise RuntimeError("Event SHA missing or invalid")
    event = Path(env["GITHUB_EVENT_PATH"])
    if event.stat().st_size > 1024 * 1024:
        raise RuntimeError("Event metadata exceeds bound")
    metadata = json.loads(event.read_text(encoding="utf-8"))
    if metadata.get("repository", {}).get("private") is not False:
        raise RuntimeError("Public repository required; no private-runner allowance assumed")


def source_gate(run, event_sha):
    head = run(["git", "rev-parse", "HEAD"], "source-head", 15).strip()
    parents = run(["git", "rev-list", "--parents", "-n", "1", "HEAD"], "source-parent", 15).split()
    changed = run(["git", "diff", "--name-only", PARENT, "HEAD", "--"], "source-scope", 15).splitlines()
    if head != event_sha or parents != [head, PARENT] or Counter(changed) != Counter(FILES):
        raise RuntimeError("Reviewed source parent or exact validation scope mismatch")
    if run(["git", "status", "--porcelain", "--untracked-files=no"], "source-clean", 15).strip():
        raise RuntimeError("Tracked checkout differs from source")
    return head


def verifier_identity(path):
    path = Path(path)
    if path.name.lower() != "nuget.exe" or not path.is_file() or path.stat().st_size > 64 * 1024 * 1024:
        raise RuntimeError("Preinstalled NuGet.exe unavailable")
    data = path.read_bytes()
    # The official Windows CLI targets Framework, independently of the installed .NET SDK.
    if not data.startswith(b"MZ") or not any(marker in data for marker in
            (b".NETFramework,Version=v4.7.2", b".NETFramework,Version=v4.8")):
        raise RuntimeError("Preinstalled verifier does not declare the expected Framework target")
    return hashlib.sha256(data).hexdigest()


def verifier_observation(path):
    if not path.is_file() or path.stat().st_size > 64 * 1024 * 1024:
        raise RuntimeError("Preinstalled NuGet.exe unavailable or exceeds bound")
    data = path.read_bytes()
    return {"Bytes": len(data), "Sha256": hashlib.sha256(data).hexdigest(), "Mz": data.startswith(b"MZ"),
            "FrameworkMarkers": sorted({value.decode("ascii") for value in re.findall(
                rb"\.NET(?:Framework|CoreApp|Standard),Version=v[0-9]+(?:\.[0-9]+){1,2}", data)})}


def preinstalled_path(path, repo):
    # Check the original path before resolve can hide a symlink or reparse point.
    if not path.is_absolute() or any(linked(item) for item in (path, *path.parents)):
        raise RuntimeError("Preinstalled verifier path must be absolute and unlinked; retained")
    result = path.resolve()
    if result.is_relative_to(repo.resolve()):
        raise RuntimeError("Verifier must be preinstalled outside the checkout")
    return result


def resolve_verifier(lookup, env, repo, report):
    selected = Path(lookup)
    report.update(VerifierStage="LookupPathGuard", VerifierLookup={"Path": str(selected), "Kind": "PathCandidate"})
    selected = preinstalled_path(selected, repo)
    report["VerifierStage"] = "LookupStaticObservation"
    report["VerifierLookup"].update(verifier_observation(selected))
    target, kind = selected, "DirectFrameworkCandidate"
    install = Path(env["CHOCOLATEYINSTALL"]) if env.get("CHOCOLATEYINSTALL") else None
    if install is not None and install.is_absolute() and selected == install / "bin/nuget.exe":
        report["VerifierStage"] = "ChocolateyRootGuard"
        install = preinstalled_path(install, repo)
        # This is the one documented preinstalled package layout, not a search.
        target = install / "lib/NuGet.CommandLine/tools/nuget.exe"
        kind = "ChocolateyPackagePayload"
        report["VerifierLookup"]["Kind"] = "ChocolateyShim"
    report.update(VerifierStage="TargetPathGuard", VerifierTarget={"Path": str(target), "Kind": kind})
    target = preinstalled_path(target, repo)
    report["VerifierStage"] = "TargetStaticIdentity"
    report["VerifierTarget"].update(verifier_observation(target))
    digest = verifier_identity(target)
    report["VerifierStage"] = "FrameworkIdentityPassed"
    return str(target), digest


def require_verifier_version(output):
    if re.search(r"\bNuGet Version:\s*" + re.escape(NU_VERSION) + r"(?:\s|$)", output) is None:
        raise RuntimeError("Preinstalled verifier version differs from reviewed runner manifest")


def package_identity(package, identity, version, reviewed_hash):
    if base64.b64encode(hashlib.sha512(package.read_bytes()).digest()).decode() != reviewed_hash:
        raise RuntimeError("Official runtime archive hash differs from reviewed pin")
    with zipfile.ZipFile(package) as archive:
        entries = [entry for entry in archive.infolist() if entry.filename.lower().endswith(".nuspec")]
        if len(entries) != 1 or entries[0].file_size > 1024 * 1024:
            raise RuntimeError("Runtime package identity metadata invalid")
        root = xml.fromstring(archive.read(entries[0]))
    metadata = next(node for node in root if node.tag.split("}")[-1] == "metadata")
    values = {node.tag.split("}")[-1]: node.text for node in metadata}
    license_node = next((node for node in metadata if node.tag.split("}")[-1] == "license"), None)
    if values.get("id") != identity or values.get("version") != version or values.get("license") != "MIT" or license_node is None or license_node.get("type") != "expression" or values.get("requireLicenseAcceptance", "false").lower() != "false":
        raise RuntimeError("Runtime package identity/license differs from reviewed MIT package")


def signature_gate(policy, output, identity, version, content_hash):
    # CLI warnings can use a different prefix from dotnet. Refuse every NuGet code,
    # including an informational downgrade of a verification problem.
    if re.search(r"\bNU\d{4}\b", output) or re.search(r"offline.*revocation|revocation.*offline", output, re.I):
        raise RuntimeError("NuGet verification reported a diagnostic or offline revocation")
    policy.require_verified_signature(output, identity, version, content_hash)


def trx_counters(path):
    root = xml.parse(path).getroot()
    counters = root.find("{*}ResultSummary/{*}Counters")
    if counters is None:
        raise RuntimeError("Test execution lacks counters")
    values = {key: int(value) for key, value in counters.attrib.items()}
    if values.get("passed", 0) < 1 or values.get("total") != values.get("passed") or values.get("executed") != values.get("passed") or any(values.get(key, 0) for key in ("failed", "error", "timeout", "aborted", "notExecuted", "notRunnable")):
        raise RuntimeError("Complete test suite did not pass without skips")
    return values


def install_sdk(repo, contract, work, deadline, report):
    spec = importlib.util.spec_from_file_location("approved_windows_sdk", repo / "scripts/install-session-sdk-windows.py")
    installer = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(installer)
    return installer.install(contract, work, deadline, report)


def emit_ci_line(value):
    print(value, flush=True)


def emit_evidence(work, report):
    """Emit only registered command logs and the two test result directories, with bounds."""
    report.update(EvidenceStatus="InProgress", EvidenceFiles=[], EvidenceErrors=[])
    raw_total = compressed_total = 0
    try:
        paths = [(command["Name"], work / command["Log"]) for command in report["Commands"]]
        for suite in ("Core", "Provider"):
            if not any(command["Name"] == suite + "-full" for command in report["Commands"]):
                continue
            directory = work / (suite + "-results")
            if any(linked(path) for path in (directory, *directory.parents)):
                raise RuntimeError("Test evidence directory is linked")
            traces = list(directory.glob("*.trx"))
            if len(traces) != 1:
                report["EvidenceErrors"].append(suite + " expected exactly one actual TRX")
            else:
                paths.append((suite + "-trx", traces[0]))
        for name, path in paths:
            if (any(linked(item) for item in (path, *path.parents)) or not path.is_file() or
                    not path.resolve().is_relative_to(work.resolve()) or path.stat().st_size > 16 * 1024 * 1024):
                raise RuntimeError("Registered evidence unavailable, linked or oversized: " + name)
            data = path.read_bytes()
            raw_hash = hashlib.sha256(data).hexdigest()
            command = next((item for item in report["Commands"] if item["Name"] == name), None)
            if command is not None and (command.get("LogSha256", raw_hash) != raw_hash or command.get("LogBytes", len(data)) != len(data)):
                report["EvidenceErrors"].append(name + " changed after command receipt; emitted snapshot only")
            if raw_total + len(data) > 32 * 1024 * 1024:
                raise RuntimeError("Job evidence raw total exceeds bound")
            packed = gzip.compress(data, mtime=0)
            if compressed_total + len(packed) > 4 * 1024 * 1024:
                raise RuntimeError("Job evidence compressed total exceeds bound")
            raw_total += len(data)
            compressed_total += len(packed)
            encoded = base64.b64encode(packed).decode("ascii")
            chunks = [encoded[index:index + 8192] for index in range(0, len(encoded), 8192)]
            item = {"Name": name, "Bytes": len(data), "Sha256": raw_hash,
                    "GzipBytes": len(packed), "GzipSha256": hashlib.sha256(packed).hexdigest(), "Chunks": len(chunks)}
            emit_ci_line("KUAIYI_CI_FILE " + json.dumps(item))
            for index, chunk in enumerate(chunks, 1):
                emit_ci_line("KUAIYI_CI_CHUNK " + json.dumps({"Name": name, "Index": index, "Total": len(chunks), "Data": chunk}))
            report["EvidenceFiles"].append(item)
    except (OSError, ValueError, KeyError, RuntimeError) as error:
        report["EvidenceErrors"].append(str(error))
    report.update(EvidenceStatus="Incomplete" if report["EvidenceErrors"] else "EmittedToJobLog",
                  EvidenceRawBytes=raw_total, EvidenceGzipBytes=compressed_total,
                  EvidenceRetrieval="NotConfirmed_RemoteReaderMustVerifyAllChunksAndHashes")


def execute(repo, env):
    policy = load_policy(repo)
    policy.require_signature_environment(env)
    work = repo / "artifacts/session-runtime-windows" / uuid.uuid4().hex
    for path in (work, *work.parents):
        if linked(path):
            raise RuntimeError("CI output has linked ancestor; retained")
    work.mkdir(parents=True)
    report = {"Status": "Blocked", "Package": "Hold", "SourceCommit": None,
              "SignatureStatus": "NotRun", "SdkStatus": "NotRun", "ManagedTests": "NotRun", "BuildStatus": "NotRun", "Publish": "NotRun",
              "WindowsAppAcceptance": "NotRun", "Commands": [], "RuntimePackages": [], "ModelCalls": "None"}
    if env.get("GITHUB_RUN_ID", "").isdigit():
        report["CiRunUrl"] = f"https://github.com/{REPOSITORY}/actions/runs/{env['GITHUB_RUN_ID']}"
    deadline = time.monotonic() + 13 * 60
    code = 2

    def run(argv, name, timeout=120):
        remaining = int(deadline - time.monotonic())
        if remaining <= 0:
            raise RuntimeError("Attempt budget exhausted")
        log = work / (name + ".log")
        command = {"Name": name, "Argv": argv, "Exit": None, "Log": log.name}
        report["Commands"].append(command)
        try:
            with log.open("xb") as stream:
                result = subprocess.run(argv, cwd=repo, env=env, stdout=stream, stderr=subprocess.STDOUT,
                                        timeout=min(timeout, remaining))
            command["Exit"] = result.returncode
        except subprocess.TimeoutExpired:
            command.update(Outcome="TimedOut", Cleanup="NotConfirmed")
            raise
        finally:
            if log.is_file():
                command["LogBytes"] = log.stat().st_size
                command["LogSha256"] = file_hash(log)
                if name.startswith("verify-") or command["Exit"] != 0:
                    with log.open("rb") as stream:
                        command["OutputPrefix"] = stream.read(65536).decode("utf-8-sig", errors="replace")
                    command["OutputPrefixTruncated"] = log.stat().st_size > 65536
        if log.stat().st_size > 4 * 1024 * 1024:
            raise RuntimeError("Command log exceeds evidence bound; retained")
        output = log.read_text(encoding="utf-8-sig")
        if result.returncode:
            raise RuntimeError("Command failed: " + name)
        return output

    def download(identity, version):
        name = identity.lower()
        uri = f"https://api.nuget.org/v3-flatcontainer/{name}/{version}/{name}.{version}.nupkg"
        report["CurrentDownloadSource"] = uri
        target = work / (name + "." + version + ".nupkg")
        with urllib.request.urlopen(uri, timeout=45) as response, target.open("xb") as output:
            if response.geturl() != uri:
                raise RuntimeError("Runtime download redirected outside the fixed source")
            count = 0
            while block := response.read(1024 * 1024):
                count += len(block)
                if count > 50 * 1024 * 1024 or time.monotonic() >= deadline:
                    raise RuntimeError("Runtime download exceeds bound")
                output.write(block)
        return target, uri

    try:
        report["SourceCommit"] = source_gate(run, env["GITHUB_SHA"])
        contract = json.loads((repo / "packaging/session-test.json").read_text())
        reviewed = json.loads((repo / "packaging/session-runtime-audit/runtime-pack-integrity.json").read_text())["Packages"]
        locked = json.loads((repo / "packaging/session-runtime-audit/packages.lock.json").read_text())["dependencies"]["net10.0"]
        if set(reviewed) != set(policy.RUNTIME_IDS) or any(reviewed[key]["Version"] != contract["runtime"] or reviewed[key]["Source"] != policy.NUGET_SOURCE or reviewed[key]["NuGetContentHash"] != locked[key]["contentHash"] for key in reviewed):
            raise RuntimeError("Runtime identity/lock/integrity scope mismatch")
        nuget = shutil.which("nuget.exe")
        if nuget is None:
            raise RuntimeError("Preinstalled NuGet.exe missing; no installation attempted")
        nuget, report["VerifierSha256"] = resolve_verifier(nuget, env, repo, report)
        report["VerifierStage"] = "HelpVersion"
        version_output = run([nuget, "help", "-ForceEnglishOutput", "-NonInteractive"], "verifier-version", 30)
        require_verifier_version(version_output)
        report.update(VerifierStage="Ready", VerifierVersion=NU_VERSION, VerifierRuntime="PreinstalledFrameworkExecutable")
        report["SignatureStatus"] = "InProgress"
        for identity in policy.RUNTIME_IDS:
            item = reviewed[identity]
            package, uri = download(identity, contract["runtime"])
            package_identity(package, identity, contract["runtime"], item["ArchiveSha512"])
            output = run([nuget, "verify", "-All", str(package), "-ForceEnglishOutput", "-NonInteractive",
                          "-Verbosity", "normal"], "verify-" + identity)
            signature_gate(policy, output, identity, contract["runtime"], item["NuGetContentHash"])
            report["RuntimePackages"].append({"Id": identity, "Version": contract["runtime"], "Source": uri,
                                               "ArchiveSha512": item["ArchiveSha512"], "NuGetContentHash": item["NuGetContentHash"],
                                               "License": "MIT", "Signature": "Pass", "VerificationOutput": output})
        report["SignatureStatus"] = "Pass_WindowsNuGetFrameworkOnline"
        report["SdkStatus"] = "InProgress"
        try:
            sdk_root = install_sdk(repo, contract, work, deadline, report)
            # Original static security policy still runs before any dotnet process.
            policy.require_safe_sdk(contract, sdk_root / "dotnet.exe")
        except (OSError, ValueError, KeyError, RuntimeError, zipfile.BadZipFile):
            report["Status"] = "SignaturesPassedTestsBlocked"
            raise
        env = dict(env, DOTNET_ROOT=str(sdk_root), DOTNET_HOST_PATH=str(sdk_root / "dotnet.exe"),
                   DOTNET_ROOT_X64=str(sdk_root), PATH=str(sdk_root) + os.pathsep + env.get("PATH", ""),
                   DOTNET_CLI_HOME=str(work / "cli-home"), NUGET_PACKAGES=str(work / "nuget-packages"),
                   NUGET_HTTP_CACHE_PATH=str(work / "nuget-http-cache"), EnableWindowsTargeting="true")
        dotnet = str(sdk_root / "dotnet.exe")
        if run([dotnet, "--version"], "safe-sdk-version", 30).strip() != contract["sdk"]:
            raise RuntimeError("Selected SDK differs from pinned safe SDK")
        report["SdkInfoOutput"] = run([dotnet, "--info"], "safe-sdk-info", 30)
        report["SdkStatus"] = "Pass"
        config = ["--configfile", str(repo / "NuGet.Config"), *AUDIT]
        audit_project = "packaging/session-runtime-audit/session-runtime-audit.csproj"
        run([dotnet, "restore", audit_project, "--locked-mode", *config], "runtime-restore")
        inventory = json.loads(run([dotnet, "list", audit_project, "package", "--include-transitive", "--format", "json", "--no-restore"], "runtime-inventory"))
        audit = json.loads(run([dotnet, "list", audit_project, "package", "--vulnerable", "--include-transitive", "--format", "json", "--no-restore", "--source", policy.NUGET_SOURCE], "runtime-audit"))
        report["RuntimeAuditCoverage"] = policy.require_no_advisories(audit, repo / audit_project, inventory, contract["runtime"])
        report["TestCounters"] = {}
        report["ManagedTests"] = "InProgress"
        for name in ("Core", "Provider"):
            project = f"tests/KuaiYi.{name}.Tests/KuaiYi.{name}.Tests.csproj"
            run([dotnet, "restore", project, "--locked-mode", *config], name + "-restore")
            results = work / (name + "-results")
            run([dotnet, "test", project, "--no-restore", "-c", "Release", "-m:1", "-nr:false", "-p:UseSharedCompilation=false",
                 "--logger", "trx", "--results-directory", str(results)], name + "-full", 180)
            traces = list(results.glob("*.trx"))
            if len(traces) != 1:
                raise RuntimeError("Expected one complete suite TRX")
            report["TestCounters"][name] = trx_counters(traces[0])
        report["ManagedTests"] = "Pass"
        report["BuildStatus"] = "InProgress"
        props = ["-p:EnableWindowsTargeting=true", "-p:RuntimeFrameworkVersion=" + contract["runtime"]]
        for name in ("App", "ImageWorker"):
            project = f"src/KuaiYi.{name}/KuaiYi.{name}.csproj"
            run([dotnet, "restore", project, "--locked-mode", *config, *props], name + "-restore")
            run([dotnet, "build", project, "--no-restore", "-c", "Release", "-m:1", "-nr:false", "-p:UseSharedCompilation=false", *props], name + "-build")
            if name == "App":
                run([dotnet, "build", project, "--no-restore", "-c", "Release", "-m:1", "-nr:false", "-p:UseSharedCompilation=false",
                     "-p:KuaiYiIsolatedSessionTest=true", *props], "App-isolated-build")
        report.update(Status="ManagedValidationPass", BuildStatus="Pass")
        code = 0
    except urllib.error.HTTPError as error:
        report.update(Reason="OfficialSourceHttpError", HttpStatus=error.code,
                      Access="Denied_NoRetry" if error.code in (401, 403) else "TechnicalFailure_NoRetry")
    except urllib.error.URLError as error:
        report.update(Reason="OfficialSourceTransportError", ErrorType=type(error.reason).__name__,
                      Access="Denied_NoRetry" if "403 Forbidden" in str(error.reason) or "401 Unauthorized" in str(error.reason) else "TechnicalFailure_NoRetry")
    except (OSError, ValueError, KeyError, TypeError, RuntimeError, StopIteration, subprocess.SubprocessError, xml.ParseError, zipfile.BadZipFile) as error:
        report.update(Reason=str(error))
    finally:
        for key in ("SignatureStatus", "SdkStatus", "ManagedTests", "BuildStatus"):
            if report[key] == "InProgress":
                report[key] = "Blocked"
        try:
            emit_evidence(work, report)
        except Exception as error:
            report.update(EvidenceStatus="Incomplete", EvidenceErrors=["Evidence emission failed: " + type(error).__name__])
        if report["Status"] == "ManagedValidationPass" and report["EvidenceStatus"] != "EmittedToJobLog":
            report["Status"], code = "ManagedValidationEvidenceIncomplete", 2
        try:
            (work / "validation-result.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        except OSError as error:
            report["EvidenceStatus"] = "Incomplete"
            report.setdefault("EvidenceErrors", []).append("Result file write failed: " + type(error).__name__)
            if code == 0:
                report["Status"], code = "ManagedValidationEvidenceIncomplete", 2
    return report, code


def main():
    try:
        if len(sys.argv) != 1:
            raise RuntimeError("No invocation overrides accepted")
        if sys.version_info < (3, 9):
            raise RuntimeError("Preinstalled Python3.9+ required; no installation attempted")
        hosted_context(os.environ, sys.platform)
        result, code = execute(Path(__file__).resolve().parent.parent, dict(os.environ))
    except (OSError, ValueError, KeyError, RuntimeError) as error:
        result, code = {"Status": "Blocked", "Reason": str(error), "SignatureStatus": "NotRun", "Package": "Hold"}, 2
    print("KUAIYI_SESSION_RUNTIME_WINDOWS " + json.dumps(result))
    return code


if __name__ == "__main__":
    sys.exit(main())
