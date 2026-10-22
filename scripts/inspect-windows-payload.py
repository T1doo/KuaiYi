"""Offline, unsealed Windows payload INPUT inventory. Never creates packages, source pins or installation state."""
import argparse
import base64
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import re
import subprocess
import zipfile

REPO = Path(__file__).resolve().parent.parent
SPEC = importlib.util.spec_from_file_location("runtime_preparation", Path(__file__).with_name("prepare-session-test.py"))
RUNTIME = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RUNTIME)
ROLES = ("App", "NativeHost", "CaptureBroker", "ClipboardHost", "CaptureHost", "OleReceiver", "ImageWorker")
NATIVE = ("KuaiYi.CaptureNative.dll", "KuaiYi.OleNativeBridge.dll", "KuaiYi.ShellExtension.dll", "KuaiYi.OcrNative.dll")
SKIA = {"skiasharp": "9cf0fa681223d4956bfbd37995c9eaa8d259935ae3d195fac681ff9a4e9c4816",
        "skiasharp.nativeassets.win32": "c27b04338d1e4d400c20de534356310b9971e123e081c732af97e56f5572e1ae"}
KINDS = {"RuntimePackage", "SkiaPackage", "GitResource", "ObservedBuildDirectoryInput", "ObservedBootstrapDirectoryInput"}


def regular(path):
    if any(p.is_symlink() for p in (path, *path.parents)) or not path.is_file():
        raise RuntimeError("Input must be a regular, non-link local file")


def file_hash(path, algorithm="sha256"):
    regular(path)
    digest = hashlib.new(algorithm)
    with path.open("rb") as source:
        before = os.fstat(source.fileno())
        if not 0 < before.st_size <= 256 * 1024 * 1024:
            raise RuntimeError("Input exceeds file bound")
        total = 0
        while chunk := source.read(65536):
            total += len(chunk)
            if total > before.st_size:
                raise RuntimeError("Input grew during observation")
            digest.update(chunk)
        after = os.fstat(source.fileno())
        if total != before.st_size or (before.st_size, before.st_mtime_ns, before.st_ctime_ns) != (after.st_size, after.st_mtime_ns, after.st_ctime_ns):
            raise RuntimeError("Input changed during observation")
    return total, digest.digest()


def target_path(value):
    if not isinstance(value, str) or not 1 <= len(value) <= 240:
        raise RuntimeError("Unbounded target path")
    parts = value.split("/")
    if len(parts) > 16:
        raise RuntimeError("Target path too deep")
    for part in parts:
        if not re.fullmatch(r"[A-Za-z0-9_.-]+", part) or part in (".", "..") or part.endswith(".") or part.split(".")[0].upper() in {"CON", "PRN", "AUX", "NUL", *(f"COM{i}" for i in range(1, 10)), *(f"LPT{i}" for i in range(1, 10))}:
            raise RuntimeError("Unsafe target path")
    return value


def record(rows, group, target, size, digest, kind, source, version, revision=None):
    target_path(target)
    if group not in ("Generation", "Bootstrap") or kind not in KINDS or not isinstance(source, dict) or not source or not version or revision is not None and not re.fullmatch(r"[a-fA-F0-9]{40}", revision):
        raise RuntimeError("Unknown or unqualified input provenance")
    if kind in ("RuntimePackage", "SkiaPackage"):
        allowed = set(RUNTIME.RUNTIME_IDS) if kind == "RuntimePackage" else set(SKIA)
        if set(source) != {"package", "member", "archiveHash"} or source["package"] not in allowed or version != ("10.0.13" if kind == "RuntimePackage" else "4.153.1"):
            raise RuntimeError("Unknown package provenance")
        target_path(source["member"])
    elif kind == "GitResource":
        if revision is None or set(source) != {"path"}:
            raise RuntimeError("Git source revision/provenance is incomplete")
        target_path(source["path"])
        if not source["path"].startswith(("packaging/identity/", "extensions/chromium/")):
            raise RuntimeError("Unlisted Git resource")
    else:
        if revision is not None or version != "Unverified" or set(source) != {"path", "qualification", "declaredBuildRevision", "declaredVersion", "revisionVerified"}:
            raise RuntimeError("Observed directory cannot authenticate binary source revision")
        target_path(source["path"])
        if not source["path"].startswith("src/") or source["qualification"] not in ("OrdinaryBuild_NotSelfContainedPublish", "FrameworkDependentDevelopment") or source["revisionVerified"] is not False or not re.fullmatch(r"[a-fA-F0-9]{40}", source["declaredBuildRevision"]) or not source["declaredVersion"]:
            raise RuntimeError("Incomplete observed build directory provenance")
    if not 0 < size <= 256 * 1024 * 1024 or not re.fullmatch(r"[a-f0-9]{64}", digest):
        raise RuntimeError("Invalid input size or hash")
    if len(rows) >= 4096 or sum(x["bytes"] for x in rows if x["group"] == group) + size > 1024 * 1024 * 1024:
        raise RuntimeError("Input inventory exceeds package bounds")
    if any(x["group"] == group and x["path"].lower() == target.lower() for x in rows):
        raise RuntimeError("Conflicting or case-aliased input targets")
    rows.append({"group": group, "path": target, "bytes": size, "sha256": digest, "sourceKind": kind, "source": source, "version": version, "sourceRevision": revision})


def observed_build_record(rows, repo, path, info, group, qualification, head):
    regular(info)
    versions = re.findall(r'AssemblyInformationalVersionAttribute\("([^"\n]*)\+([a-fA-F0-9]{40})"\)', info.read_text())
    if len(versions) != 1 or versions[0][1] != head:
        raise RuntimeError("Observed obj declaration is not current; binary revision is unverified")
    count, digest = file_hash(path)
    record(rows, group, path.name, count, digest.hex(),
           "ObservedBuildDirectoryInput" if group == "Generation" else "ObservedBootstrapDirectoryInput",
           {"path": path.relative_to(repo).as_posix(), "qualification": qualification,
            "declaredBuildRevision": versions[0][1], "declaredVersion": versions[0][0], "revisionVerified": False}, "Unverified")


def checked_output(repo, requested):
    if ".." in requested.parts:
        raise RuntimeError("Parent traversal in output refused")
    output = Path(os.path.abspath(requested))
    if any(p.is_symlink() for p in (output, *output.parents)):
        raise RuntimeError("Output links refused")
    parent = output.parent.resolve(strict=True)
    allowed = (repo / "artifacts").resolve(strict=True)
    try:
        parent.relative_to(allowed)
    except ValueError as error:
        raise RuntimeError("Output must stay within actual artifacts directory") from error
    if not parent.is_dir() or output.exists():
        raise RuntimeError("Output must be a new file in an existing artifacts directory")
    return parent / output.name


def verified_zip(path, expected, algorithm):
    regular(path)
    with path.open("rb") as source:
        raw = source.read(256 * 1024 * 1024 + 1)
    if not 0 < len(raw) <= 256 * 1024 * 1024:
        raise RuntimeError("Input archive exceeds memory bound")
    actual = hashlib.new(algorithm, raw).digest()
    text = base64.b64encode(actual).decode() if algorithm == "sha512" else actual.hex()
    if text != expected:
        raise RuntimeError("Input package differs from approved archive pin")
    z = zipfile.ZipFile(io.BytesIO(raw))
    try:
        if len(z.infolist()) > 16384 or len({x.filename for x in z.infolist()}) != len(z.infolist()) or sum(x.file_size for x in z.infolist()) > 1024 * 1024 * 1024 or z.testzip() is not None:
            raise RuntimeError("Input ZIP inventory/CRC exceeds contract")
    except BaseException:
        z.close()
        raise
    return z


def member_record(rows, group, target, archive, member, kind, identity, version, archive_hash):
    info = archive.getinfo(member)
    if not 0 < info.file_size <= 256 * 1024 * 1024 or info.flag_bits & 1 or info.is_dir():
        raise RuntimeError("Invalid package member")
    raw = archive.read(member)
    record(rows, group, target, len(raw), hashlib.sha256(raw).hexdigest(), kind,
           {"package": identity, "member": member, "archiveHash": archive_hash}, version)


def inventory(repo, runtime_directory):
    rows, missing = [], []
    head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=repo, text=True).strip()
    if not re.fullmatch(r"[a-f0-9]{40}", head):
        raise RuntimeError("Full source revision required")
    if subprocess.check_output(["git", "status", "--porcelain"], cwd=repo):
        raise RuntimeError("Freeze current source before inventory; dirty input is not qualified")
    contract = json.loads((repo / "packaging/session-runtime-audit/runtime-pack-integrity.json").read_text())["Packages"]
    packs = []
    for name, pin in contract.items():
        version = pin["Version"]
        if version != "10.0.13" or pin["Source"] != RUNTIME.NUGET_SOURCE:
            raise RuntimeError("Unreviewed runtime input")
        path = runtime_directory / f"{name.lower()}.{version}.nupkg"
        with verified_zip(path, pin["ArchiveSha512"], "sha512") as archive:
            packs.append((name, version, io.BytesIO(archive.fp.getvalue())))
    mapped = RUNTIME.runtime_payload_records(packs)
    for name, version, snapshot in packs:
        snapshot.seek(0)
        with zipfile.ZipFile(snapshot) as archive:
            for item in mapped:
                if item["Id"] == name:
                    member_record(rows, "Generation", item["Path"], archive, item["Member"], "RuntimePackage", name, version, contract[name]["ArchiveSha512"])
                    if rows[-1]["sha256"] != item["Sha256"]:
                        raise RuntimeError("Runtime member changed after mapping")
                    if "Overrides" in item:
                        rows[-1]["overrides"] = item["Overrides"]
            if name == RUNTIME.RUNTIME_IDS[0]:
                for member, target in [("LICENSE.TXT", "licenses/dotnet/LICENSE.txt"), ("THIRD-PARTY-NOTICES.TXT", "licenses/dotnet/THIRD-PARTY-NOTICES.txt")]:
                    member_record(rows, "Generation", target, archive, member, "RuntimePackage", name, version, contract[name]["ArchiveSha512"])
    cache = repo / "artifacts/session-sdk-state/cli/.nuget/packages"
    for slug, pin in SKIA.items():
        path = cache / slug / "4.153.1" / f"{slug}.4.153.1.nupkg"
        with verified_zip(path, pin, "sha256") as archive:
            members = [("lib/net10.0/SkiaSharp.dll", "SkiaSharp.dll")] if slug == "skiasharp" else [("runtimes/win-x64/native/libSkiaSharp.dll", "libSkiaSharp.dll"), ("LICENSE.txt", "licenses/SkiaSharp-4.153.1/LICENSE.txt"), ("THIRD-PARTY-NOTICES.txt", "licenses/SkiaSharp-4.153.1/THIRD-PARTY-NOTICES.txt")]
            for member, target in members:
                member_record(rows, "Generation", target, archive, member, "SkiaPackage", slug, "4.153.1", pin)
    for prefix, target in [("packaging/identity", "identity"), ("extensions/chromium", "browser-extension")]:
        paths = subprocess.check_output(["git", "ls-files", "-z", prefix], cwd=repo).decode().split("\0")
        for name in filter(None, paths):
            path = repo / name
            count, digest = file_hash(path)
            original = subprocess.check_output(["git", "show", f"{head}:{name}"], cwd=repo)
            if digest != hashlib.sha256(original).digest():
                raise RuntimeError("Resource differs from fixed Git source")
            record(rows, "Generation", target + "/" + path.relative_to(repo / prefix).as_posix(), count, digest.hex(), "GitResource", {"path": name}, "Git", head)
    build_inputs = [("Generation", x, suffix) for x in ROLES for suffix in (".exe", ".dll", ".deps.json", ".runtimeconfig.json")]
    build_inputs += [("Generation", x, ".dll") for x in ("Core", "Contracts", "Imaging", "Windows", "Providers", "OleTransfer")]
    build_inputs += [("Bootstrap", "Launcher", x) for x in (".exe", ".dll", ".deps.json", ".runtimeconfig.json")]
    for group, short, suffix in build_inputs:
        framework = "net10.0-windows" if short in ("App", "CaptureBroker", "ClipboardHost", "CaptureHost", "OleReceiver", "Windows") else "net10.0"
        sub = framework + ("/win-x64" if short in (*ROLES, "Launcher") else "")
        folder = repo / "src" / ("KuaiYi." + short)
        path = folder / "bin/Release" / sub / ("KuaiYi." + short + suffix)
        if not path.is_file():
            missing.append({"kind": "MissingBuildDirectoryInput", "name": path.name})
            continue
        info = folder / "obj/Release" / sub / ("KuaiYi." + short + ".AssemblyInfo.cs")
        observed_build_record(rows, repo, path, info, group, "OrdinaryBuild_NotSelfContainedPublish", head)
    path = repo / "src/KuaiYi.Launcher/bin/Release/net10.0/win-x64/KuaiYi.Core.dll"
    info = repo / "src/KuaiYi.Core/obj/Release/net10.0/KuaiYi.Core.AssemblyInfo.cs"
    observed_build_record(rows, repo, path, info, "Bootstrap", "FrameworkDependentDevelopment", head)
    digest = rows[-1]["sha256"]
    core = next(x for x in rows if x["group"] == "Generation" and x["path"] == "KuaiYi.Core.dll")
    if digest != core["sha256"]:
        raise RuntimeError("Bootstrap/generation Core inputs differ")
    missing += [{"kind": "NativeWindowsBuild", "name": name} for name in NATIVE]
    missing += [{"kind": "SelfContainedRolePublish", "name": "KuaiYi." + name} for name in ROLES]
    missing += [{"kind": "VerifiedManagedArtifactSourceRevision", "name": "Observed ordinary build directories do not authenticate binary source revisions"}, {"kind": "CurrentOnlineSignatureQualification", "name": "Approved runtime and Skia input archives"}, {"kind": "RealExtensionOrigin", "name": "1-2 actual extension IDs"}, {"kind": "SelfContainedBootstrapContract", "name": "Existing exact five-file B remains FrameworkDependentDevelopment"}]
    return {"kind": "UnsealedWindowsPayloadInputInventory", "status": "Blocked", "sealed": False, "sourceRevision": head,
            "files": sorted(rows, key=lambda x: (x["group"], x["path"])), "missing": missing,
            "generationSourceSha256": None, "bootstrapSourceSha256": None, "installerPinsWritten": False, "nativeOrRuntimeExecuted": False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--runtime-input-directory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    output = checked_output(REPO, args.output)
    result = inventory(REPO, args.runtime_input_directory.absolute())
    with output.open("x", encoding="utf-8") as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2)
        stream.write("\n")
    print(json.dumps({"status": result["status"], "files": len(result["files"]), "missing": len(result["missing"]), "sealed": False, "installerPinsWritten": False}))
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
