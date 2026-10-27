"""Build the eight real Windows managed roles into a fresh local staging directory.

This produces unsealed build inputs, never installation/source authority. Run with
the existing approved SDK and cache; no application, native DLL or CI is executed.
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import signal
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
import zipfile

REPO = Path(__file__).resolve().parent.parent
ROLES = ("App", "NativeHost", "CaptureBroker", "ClipboardHost", "CaptureHost", "OleReceiver", "ImageWorker", "Launcher")
LIMIT = 6 * 1024**3
SECONDS = 600


def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, REPO / "scripts" / filename)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


INPUTS = module("managed_publish_inputs", "inspect-windows-payload.py")
SDK = module("managed_publish_sdk", "session-sdk.py")


def manifest(root):
    rows = []
    total = 0
    for path in sorted(root.rglob("*")):
        if path.is_symlink() or not (path.is_file() or path.is_dir()):
            raise RuntimeError("Special staging entry refused; original retained")
        if path.is_file():
            size, digest = INPUTS.file_hash(path) if path.stat().st_size else (0, hashlib.sha256(b"").digest())
            total += size
            if total > LIMIT or len(rows) >= 20000:
                raise RuntimeError("Staging output exceeds bound; original retained")
            rows.append({"path": path.relative_to(root).as_posix(), "bytes": size, "sha256": digest.hex()})
    return rows


def product_sources():
    prefixes = ["src", "packaging", "assets", "licenses", "extensions", "Directory.*", "NuGet.Config", "global.json",
                "scripts/session-sdk.py", "scripts/prepare-session-test.py", "scripts/inspect-windows-payload.py"]
    subprocess.run(["git", "diff", "--exit-code", "HEAD", "--", *prefixes], cwd=REPO, check=True, stdout=subprocess.DEVNULL)
    unknown = subprocess.check_output(["git", "ls-files", "--others", "--exclude-standard", "--", *prefixes], cwd=REPO)
    if unknown:
        raise RuntimeError("Untracked product inputs refused")
    names = subprocess.check_output(["git", "ls-files", "-z", "--", *prefixes], cwd=REPO).decode().split("\0")
    return [{"path": name, "bytes": size, "sha256": digest.hex()}
            for name in sorted(filter(None, names)) for size, digest in [INPUTS.file_hash(REPO / name)]]


def runtime_packages(cache):
    pins = json.loads((REPO / "packaging/session-runtime-audit/runtime-pack-integrity.json").read_text())["Packages"]
    if set(pins) != set(INPUTS.RUNTIME.RUNTIME_IDS):
        raise RuntimeError("Unexpected runtime contract")
    packages = []
    for identity in INPUTS.RUNTIME.RUNTIME_IDS:
        pin = pins[identity]
        if pin["Version"] != "10.0.13" or pin["Source"] != INPUTS.RUNTIME.NUGET_SOURCE:
            raise RuntimeError("Unreviewed runtime source")
        path = cache / identity.lower() / "10.0.13" / (identity.lower() + ".10.0.13.nupkg")
        with INPUTS.verified_zip(path, pin["ArchiveSha512"], "sha512"):
            pass
        packages.append((identity, "10.0.13", path))
    return packages


def read_json(path):
    INPUTS.regular(path)
    raw = path.read_bytes()
    if len(raw) > 65536:
        raise RuntimeError("Configuration exceeds original document bound")
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise RuntimeError("Duplicate configuration property")
            result[key] = value
        return result
    return json.loads(raw, object_pairs_hook=unique)


def equal_json(left, right):
    return json.dumps(left, sort_keys=True, allow_nan=False) == json.dumps(right, sort_keys=True, allow_nan=False)


def bind_published_runtime(output, packages):
    records = INPUTS.RUNTIME.runtime_payload_records(packages)
    if len(packages) == 2:
        # SDK 10.0.402 ResolveRuntimePackAssets lines 167-175: a WPF
        # profile excludes entries with NO Profile as well as other profiles.
        # Keep the original inventory mapper's archive hashes/collision rules.
        with zipfile.ZipFile(packages[1][2]) as archive:
            root = ET.fromstring(archive.read("data/RuntimeList.xml"))
            if root.get("TargetFrameworkVersion") != "10.0":
                raise RuntimeError("Unexpected desktop profile contract")
            members = {x.get("Path") for x in root if "WPF" in x.get("Profile", "").split(";")}
        records = [x for x in records if x["Id"] != packages[1][0] or x["Member"] in members]
    for record in records:
        if INPUTS.file_hash(output / record["Path"])[1].hex() != record["Sha256"]:
            raise RuntimeError("Published runtime hash differs: " + record["Path"])
    return records


def add_runtime_notices(output, packages):
    """Original approved Core pack notices, at the existing bootstrap paths."""
    rows = []
    with zipfile.ZipFile(packages[0][2]) as archive:
        for member, relative in [("LICENSE.TXT", "licenses/dotnet/LICENSE.txt"),
                                 ("THIRD-PARTY-NOTICES.TXT", "licenses/dotnet/THIRD-PARTY-NOTICES.txt")]:
            target = output / relative
            if any(p.is_symlink() for p in (target, *target.parents)):
                raise RuntimeError("Linked notice output refused")
            raw = archive.read(member)
            if not 0 < len(raw) <= 1024 * 1024:
                raise RuntimeError("Notice bound exceeded")
            target.parent.mkdir(parents=True, exist_ok=True)
            with target.open("xb") as stream:
                stream.write(raw)
            rows.append({"path": relative, "member": member, "package": packages[0][0],
                         "bytes": len(raw), "sha256": hashlib.sha256(raw).hexdigest()})
    return rows


def inspect_role(role, output, packages):
    name = "KuaiYi." + role
    document = read_json(output / (name + ".runtimeconfig.json"))
    config = document["runtimeOptions"]
    frameworks = config.get("includedFrameworks", [])
    actual = {x["name"]: x["version"] for x in frameworks}
    if ("framework" in config or "frameworks" in config or len(actual) != len(frameworks)
            or actual.get("Microsoft.NETCore.App") != "10.0.13"
            or set(actual) - {"Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App"}
            or any(v != "10.0.13" for v in actual.values())):
        raise RuntimeError("Actual self-contained runtime configuration required")
    selected = packages if "Microsoft.WindowsDesktop.App" in actual else packages[:1]
    runtime = bind_published_runtime(output, selected)
    for suffix in (".exe", ".dll", ".deps.json", ".runtimeconfig.json"):
        INPUTS.file_hash(output / (name + suffix))
    if role == "Launcher":
        contract = json.loads((REPO / "src/KuaiYi.Core/Installation/bootstrap-runtime-win-x64-10.0.13.json").read_text())
        for row in contract["files"]:
            size, digest = INPUTS.file_hash(output / row["path"])
            if size != row["bytes"] or digest.hex() != row["sha256"].lower():
                raise RuntimeError("Launcher runtime differs from the original bootstrap contract")
        if not equal_json(read_json(output / (name + ".deps.json")), contract["dependencies"]):
            raise RuntimeError("Launcher dependencies differ from original bootstrap contract")
        require_bootstrap_config(document)
    skia = inspect_skia(output, packages[0][2].parents[2]) if role == "ImageWorker" else None
    return {"role": role, "runtimeMembers": len(runtime), "files": manifest(output), **({"skia": skia} if skia is not None else {})}


def inspect_skia(output, cache):
    """Require actual publish/deps and both existing pinned Skia package inputs."""
    deps = read_json(output / "KuaiYi.ImageWorker.deps.json")
    target = deps["targets"][".NETCoreApp,Version=v10.0/win-x64"]
    lock = read_json(REPO / "src/KuaiYi.ImageWorker/packages.lock.json")["dependencies"]["net10.0"]
    records = []
    for slug, identity, section, members in [
        ("skiasharp", "SkiaSharp", "runtime", [("lib/net10.0/SkiaSharp.dll", "SkiaSharp.dll")]),
        ("skiasharp.nativeassets.win32", "SkiaSharp.NativeAssets.Win32", "native", [
            ("runtimes/win-x64/native/libSkiaSharp.dll", "libSkiaSharp.dll"),
            ("runtimes/win-x64/native/libSkiaSharp.pdb", "libSkiaSharp.pdb")])]:
        key = identity + "/4.153.1"
        if (key not in deps["libraries"] or deps["libraries"][key].get("type") != "package"
                or any(member not in target.get(key, {}).get(section, {}) for member, _ in members)
                or lock[identity]["resolved"] != "4.153.1"):
            raise RuntimeError("ImageWorker publish dependency closure is missing Skia")
        if identity == "SkiaSharp" and target[key].get("dependencies", {}).get("SkiaSharp.NativeAssets.Win32") != "4.153.1":
            raise RuntimeError("Skia managed/native dependency differs")
        path = cache / slug / "4.153.1" / (slug + ".4.153.1.nupkg")
        with INPUTS.verified_zip(path, INPUTS.SKIA[slug], "sha256") as archive:
            # Signed NuGet content hash is not raw ZIP SHA512. The original archive
            # pin binds these bytes; fresh SDK verification below checks its signed hash.
            metadata = read_json(path.parent / ".nupkg.metadata")
            if metadata.get("contentHash") != lock[identity]["contentHash"] or metadata.get("source") != INPUTS.RUNTIME.NUGET_SOURCE:
                raise RuntimeError("Skia metadata differs from original locked SHA512 content hash")
            for member, relative in members:
                raw = archive.read(member)
                size, sha = INPUTS.file_hash(output / relative)
                if size != len(raw) or sha != hashlib.sha256(raw).digest():
                    raise RuntimeError("Published Skia member differs from pinned original")
                records.append({"path": relative, "package": identity, "version": "4.153.1", "member": member,
                                "bytes": size, "sha256": sha.hex()})
    return records


def verify_skia_inputs(cache, env, output, deadline, receipts):
    """Use the locked SDK's NuGet implementation for signed content hash semantics."""
    sdk, _ = SDK.environment(REPO)
    lock = read_json(REPO / "src/KuaiYi.ImageWorker/packages.lock.json")["dependencies"]["net10.0"]
    for slug, identity in [("skiasharp", "SkiaSharp"), ("skiasharp.nativeassets.win32", "SkiaSharp.NativeAssets.Win32")]:
        package = cache / slug / "4.153.1" / (slug + ".4.153.1.nupkg")
        with INPUTS.verified_zip(package, INPUTS.SKIA[slug], "sha256"):
            pass
        log = output / (slug + "-signature.log")
        row = {"package": identity, "version": "4.153.1"}
        receipts.append(row)
        execute([str(sdk / "dotnet"), "nuget", "verify", str(package), "--all"], env, log, deadline, output, row)
        matches = re.findall(r"^Content hash: ([A-Za-z0-9+/]+=*)\s*$", log.read_text(encoding="utf-8"), re.MULTILINE)
        if row["exit"] != 0 or matches != [lock[identity]["contentHash"]]:
            raise RuntimeError("Original NuGet signature/content hash qualification failed")
        with INPUTS.verified_zip(package, INPUTS.SKIA[slug], "sha256"):
            pass
        row.update(lockedContentHashVerified=True, archiveSha256=INPUTS.SKIA[slug])


def require_bootstrap_config(document):
    source = (REPO / "src/KuaiYi.Core/Installation/InstallationBootstrapPackage.cs").read_text()
    match = re.findall(r'SelfContainedRuntime="""\s*(.*?)\s*""";', source, re.DOTALL)
    if len(match) != 1 or not equal_json(document, json.loads(match[0])):
        raise RuntimeError("Launcher runtime configuration differs from original bootstrap contract")


def group_members(group):
    members = []
    for entry in Path("/proc").iterdir():
        if not entry.name.isdigit():
            continue
        try:
            fields = (entry / "stat").read_text().rsplit(")", 1)[1].split()
            if int(fields[2]) == group:
                members.append({"pid": int(entry.name), "state": fields[0]})
        except FileNotFoundError:
            continue
    return members


def output_size(root):
    total = 0
    for path in root.rglob("*"):
        if path.is_symlink():
            raise RuntimeError("Linked output refused; retained")
        if path.is_file():
            total += path.stat().st_size
    return total


def execute(argv, env, log, deadline, work, receipt=None):
    """Own only the newly created group; retain logs and fail closed on timeout."""
    row = receipt if receipt is not None else {}
    row.update(argv=argv, startUnix=time.time(), log=log.name, exit=None, settlement="NotStarted")
    process = None
    with log.open("xb") as stream:
        process = subprocess.Popen(argv, cwd=REPO, env=env, stdout=stream, stderr=subprocess.STDOUT, start_new_session=True)
        row.update(pid=process.pid, group=process.pid, settlement="Running")
        try:
            while process.poll() is None:
                if time.monotonic() >= deadline:
                    raise subprocess.TimeoutExpired(argv, SECONDS)
                if output_size(work) > LIMIT:
                    raise RuntimeError("In-flight output budget exceeded; retained")
                time.sleep(0.2)
            row["exit"] = process.wait()
            grace = time.monotonic() + 5
            while any(x["state"] not in ("Z", "X") for x in group_members(process.pid)):
                if time.monotonic() >= grace:
                    raise RuntimeError("Original child group remains; settlement unconfirmed")
                time.sleep(0.1)
        except BaseException:
            # Build servers are disabled. Never target pre-existing processes.
            try:
                os.killpg(process.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
            process.wait()
            raise
        finally:
            row["exit"] = process.poll()
            remaining = group_members(process.pid)
            row.update(endUnix=time.time(), groupMembersAfter=remaining,
                       settlement="Unknown" if any(x["state"] not in ("Z", "X") for x in remaining) else
                       "LeaderJoined_ChildrenExited_UnreapedZombies" if remaining else "LeaderJoined_GroupEmpty")
    return row


def publish(output):
    output = INPUTS.checked_output(REPO, output)
    if os.statvfs(output.parent).f_bavail * os.statvfs(output.parent).f_frsize < LIMIT + 1024**3:
        raise RuntimeError("Insufficient free space for bounded publish")
    before = product_sources()
    head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=REPO, text=True).strip()
    sdk, env = SDK.environment(REPO)
    INPUTS.RUNTIME.require_signature_environment(env)
    assets = json.loads((REPO / "src/KuaiYi.App/obj/project.assets.json").read_text())
    folders = list(assets["packageFolders"])
    if len(folders) != 1:
        raise RuntimeError("One existing restored cache required")
    cache = Path(folders[0])
    if not cache.is_relative_to(REPO / "artifacts") or any(p.is_symlink() for p in (cache, *cache.parents)):
        raise RuntimeError("Existing cache must be inside this workspace")
    packages = runtime_packages(cache)
    output.mkdir()
    (output / "sources-before.json").write_text(json.dumps(before, indent=2) + "\n")
    script = Path(__file__).read_bytes()
    (output / "publisher-source.py").write_bytes(script)
    env.update(NUGET_PACKAGES=str(cache), MSBUILDDISABLENODEREUSE="1", DOTNET_CLI_UI_LANGUAGE="en-US")
    report = {"kind": "UnsealedManagedWindowsPublish", "status": "InProgress", "sourceHead": head,
              "sdk": "10.0.402", "runtime": "10.0.13", "commands": [], "roles": [], "skiaAudits": [],
              "productionPins": "Unavailable", "issuer": "NotImplemented", "windowsExecution": "NotRun",
              "nativeBuild": "NotRun", "wholeGenerationSha256": None, "bootstrapSourceSha256": None,
              "signatureVerification": "RuntimeOnly_NotRerun_ArchiveBytesMatchExistingPins", "originals": "RetainedLocally"}
    report["publisherSha256"] = hashlib.sha256(script).hexdigest()
    deadline = time.monotonic() + SECONDS
    try:
        verify_skia_inputs(cache, env, output, deadline, report["skiaAudits"])
        props = ["-p:EnableWindowsTargeting=true", "-p:RuntimeFrameworkVersion=10.0.13", "-p:SelfContained=true",
                 "-p:PublishSingleFile=false", "-p:PublishTrimmed=false", "-p:PublishReadyToRun=false",
                 "-p:KuaiYiTextAlpha=false", "-p:KuaiYiIsolatedSessionTest=false", "-p:KuaiYiIncludeWindowsCiProbe=false",
                 "-maxcpucount:1", "-nodeReuse:false",
                 "-p:UseSharedCompilation=false", "-p:NuGetAudit=true", "-p:NuGetAuditMode=all",
                 "-p:NuGetAuditLevel=low", "-p:TreatWarningsAsErrors=true", "-p:SourceRevisionId=" + head]
        for role in ROLES:
            if time.monotonic() >= deadline:
                raise TimeoutError("Publish deadline reached before next role")
            manifest(output)
            project = "src/KuaiYi." + role + "/KuaiYi." + role + ".csproj"
            # Each executable already declares win-x64. A global -r also changes
            # reference-library RIDs and invalidates their original locked graph.
            common = ["--artifacts-path", str(output / "build"), "--disable-build-servers", *props]
            commands = [(["restore", project, "--locked-mode", "--configfile", str(REPO / "NuGet.Config"), *common], "restore"),
                        (["publish", project, "--no-restore", "-c", "Release", "--self-contained", "true",
                          "-o", str(output / "roles" / role), *common], "publish")]
            for args, phase in commands:
                row = {}
                report["commands"].append(row)
                execute([str(sdk / "dotnet"), *args], env, output / (role + "-" + phase + ".log"), deadline, output, row)
                if row["exit"] != 0:
                    raise RuntimeError(role + " " + phase + " failed; original log retained")
            notices = add_runtime_notices(output / "roles" / role, packages)
            inspected = inspect_role(role, output / "roles" / role, packages)
            inspected["notices"] = notices
            report["roles"].append(inspected)
        cores = {INPUTS.file_hash(output / "roles" / role / "KuaiYi.Core.dll")[1].hex() for role in ROLES}
        if len(cores) != 1:
            raise RuntimeError("Published Core copies differ")
        after = product_sources()
        (output / "sources-after.json").write_text(json.dumps(after, indent=2) + "\n")
        if after != before or subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=REPO, text=True).strip() != head:
            raise RuntimeError("Product sources changed during publish")
        runtime_packages(cache)
        report.update(status="Pass_UnsealedManagedInputsOnly", coreSha256=cores.pop(), sourcesUnchanged=True,
                      outputBytes=sum(x["bytes"] for x in manifest(output)))
    except BaseException as error:
        report.update(status="Blocked" if isinstance(error, (TimeoutError, subprocess.TimeoutExpired, KeyboardInterrupt)) else "Fail",
                      errorType=type(error).__name__, error=str(error),
                      producerSettlement=[x.get("settlement", "Unknown") for x in report["commands"]])
        raise
    finally:
        with (output / "result.json").open("x") as stream:
            json.dump(report, stream, indent=2)
            stream.write("\n")
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path, help="Fresh directory below existing artifacts; failures are retained")
    args = parser.parse_args()
    try:
        result = publish(args.output)
        print(json.dumps({k: result[k] for k in ("status", "sourceHead", "coreSha256", "outputBytes")}))
    except (OSError, ValueError, KeyError, RuntimeError, subprocess.SubprocessError) as error:
        print(type(error).__name__ + ": " + str(error), file=sys.stderr)
        sys.exit(2)
