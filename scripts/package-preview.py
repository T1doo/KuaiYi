"""Package verified managed build inputs for review, never installation authority.

No executable is launched. Failed staging is retained; existing outputs are never
overwritten. A candidate remains explicitly unsealed even when every hash matches.
"""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path, PurePosixPath
import re
import stat
import subprocess
import zipfile

REPO = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location("preview_publish", REPO / "scripts/publish-windows-managed.py")
publisher = importlib.util.module_from_spec(spec)
spec.loader.exec_module(publisher)
MAX_BYTES = 2 * 1024**3
MAX_FILES = 10000


def regular(path):
    for p in (path, *path.parents):
        if p.is_symlink():
            raise ValueError("Linked package input refused")
    s = path.stat()
    if not stat.S_ISREG(s.st_mode) or s.st_nlink != 1:
        raise ValueError("Non-exclusive regular package input refused")
    return s


def digest(path):
    regular(path)
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def name(value):
    return publisher.INPUTS.target_path(value)


def unique_members(names):
    folded = {name(n).lower() for n in names}
    if len(folded) != len(names) or len(names) > MAX_FILES:
        raise ValueError("Duplicate or excessive Windows package members")
    for n in folded:
        if any(str(p).lower() in folded for p in PurePosixPath(n).parents if str(p) != "."):
            raise ValueError("Package file/directory prefix conflict")


def document(path):
    regular(path)
    if path.stat().st_size > 8 * 1024**2:
        raise ValueError("Package metadata exceeds bound")
    def unique(pairs):
        d = {}
        for k, v in pairs:
            if k in d:
                raise ValueError("Duplicate metadata field")
            d[k] = v
        return d
    return json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=unique)


def validate_publish(root):
    root = root.absolute()
    report = document(root / "result.json")
    if (report.get("status") != "Pass_UnsealedManagedInputsOnly"
            or report.get("kind") != "UnsealedManagedWindowsPublish"
            or report.get("productionPins") != "Unavailable"
            or report.get("windowsExecution") != "NotRun"
            or report.get("sourcesUnchanged") is not True
            or report.get("sdk") != "10.0.402" or report.get("runtime") != "10.0.13"):
        raise ValueError("A complete original unsealed publish is required")
    sha = report["sourceHead"]
    if not re.fullmatch("[0-9a-f]{40}", sha):
        raise ValueError("Exact build revision required")
    subprocess.run(["git", "merge-base", "--is-ancestor", sha, "HEAD"], cwd=REPO, check=True)
    current = publisher.product_sources()
    if document(root / "sources-before.json") != current or document(root / "sources-after.json") != current:
        raise ValueError("Build inputs differ from current product source")
    if digest(root / "publisher-source.py") != report["publisherSha256"] or digest(REPO / "scripts/publish-windows-managed.py") != report["publisherSha256"]:
        raise ValueError("Original publisher differs")
    commands = report["commands"]
    if (len(commands) != 16 or any(c.get("exit") != 0 or c.get("settlement") != "LeaderJoined_GroupEmpty" for c in commands)):
        raise ValueError("Original build command settlement is not confirmed")
    roles = report["roles"]
    if [r["role"] for r in roles] != list(publisher.ROLES):
        raise ValueError("Exact eight managed roles required")
    entries = []
    for row in roles:
        folder = root / "roles" / row["role"]
        if publisher.manifest(folder) != row["files"]:
            raise ValueError("Published role bytes or membership changed")
        for item in row["files"]:
            rel = name("managed-inputs/" + row["role"] + "/" + item["path"])
            entries.append((rel, folder / item["path"], item["bytes"], item["sha256"]))
    return report, entries


def flatten(entries, runtime):
    """Seven-role development app closure; no generation catalog/installation grant."""
    outputs = {}
    provenance = {}
    wpf = next((x for x in runtime if x["Path"] == "WindowsBase.dll"), None)
    if (wpf is None or wpf["Id"] != publisher.INPUTS.RUNTIME.RUNTIME_IDS[1]
            or wpf["Version"] != "10.0.13" or wpf.get("Overrides", {}).get("Id") != publisher.INPUTS.RUNTIME.RUNTIME_IDS[0]):
        raise ValueError("Original verified WindowsBase override is missing")
    pair = {wpf["Sha256"], wpf["Overrides"]["Sha256"]}
    for rel, path, size, sha in entries:
        _, role, member = rel.split("/", 2)
        prefix = "bootstrap-input" if role == "Launcher" else "app"
        if member.startswith("KuaiYi.") and member.endswith(".pdb"):
            prefix = "symbols/" + prefix
        dest = name(prefix + "/" + member)
        key = dest.lower()
        old = outputs.get(key)
        if old is None:
            outputs[key] = (dest, path, size, sha)
            provenance[key] = {"path": dest, "selectedSourceRoles": [role], "replacedSources": []}
        elif old[2:] == (size, sha):
            provenance[key]["selectedSourceRoles"].append(role)
        elif (dest == "app/WindowsBase.dll" and old[0] == dest
                and {old[3], sha} == pair):
            record = provenance[key]
            if sha == wpf["Sha256"]:
                record["replacedSources"].extend({"role": r, "sha256": old[3]} for r in record["selectedSourceRoles"])
                record["selectedSourceRoles"] = [role]
                outputs[key] = (dest, path, size, sha)
            else:
                record["replacedSources"].append({"role": role, "sha256": sha})
            record["overrideReason"] = "OriginalPinned10.0.13DesktopImplementationOverridesCoreStub"
        else:
            raise ValueError("Differing managed role collision; no arbitrary overwrite")
    result = [outputs[k] for k in sorted(outputs)]
    unique_members([x[0] for x in result])
    return result, [provenance[k] for k in sorted(provenance)]


def write_zip(path, entries, metadata):
    names = [x[0] for x in entries] + list(metadata)
    unique_members(names)
    if sum(x[2] for x in entries) + sum(map(len, metadata.values())) > MAX_BYTES:
        raise ValueError("Package exceeds uncompressed size bound")
    def info(rel):
        z = zipfile.ZipInfo(name(rel), date_time=(1980, 1, 1, 0, 0, 0))
        z.compress_type = zipfile.ZIP_DEFLATED
        z.external_attr = 0o100644 << 16
        return z
    with zipfile.ZipFile(path, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for rel, source, size, sha in sorted(entries):
            if regular(source).st_size != size or digest(source) != sha:
                raise ValueError("Input changed before archive write")
            h = hashlib.sha256()
            count = 0
            with source.open("rb") as original, archive.open(info(rel), "w", force_zip64=True) as target:
                for block in iter(lambda: original.read(1024 * 1024), b""):
                    count += len(block)
                    if count > size:
                        raise ValueError("Input grew during archive write")
                    h.update(block)
                    target.write(block)
            if count != size or h.hexdigest() != sha or digest(source) != sha:
                raise ValueError("Input changed during archive write")
        for rel, raw in sorted(metadata.items()):
            archive.writestr(info(rel), raw)
    with zipfile.ZipFile(path) as archive:
        if sorted(archive.namelist()) != sorted(names) or archive.testzip() is not None:
            raise ValueError("Archive membership or CRC differs")
        for rel, _, size, sha in entries:
            h = hashlib.sha256()
            with archive.open(rel) as stream:
                for block in iter(lambda: stream.read(1024 * 1024), b""):
                    h.update(block)
            if archive.getinfo(rel).file_size != size or h.hexdigest() != sha:
                raise ValueError("Archived input differs")
        for rel, raw in metadata.items():
            if archive.read(rel) != raw:
                raise ValueError("Archived metadata differs")


def package(root, output, version):
    if not re.fullmatch(r"0\.1\.0-preview\.[0-9]{8}", version):
        raise ValueError("Only explicit dated preview versions are supported")
    output = output.absolute()
    if not output.is_relative_to(REPO / "artifacts") or any(p.is_symlink() for p in (output, *output.parents)):
        raise ValueError("Fresh local artifacts directory required")
    authored = ["scripts/package-preview.py", "scripts/verify-preview.ps1", "docs/release"]
    subprocess.run(["git", "diff", "--exit-code", "HEAD", "--", *authored], cwd=REPO, check=True, stdout=subprocess.DEVNULL)
    if subprocess.check_output(["git", "ls-files", "--others", "--exclude-standard", "--", *authored], cwd=REPO):
        raise ValueError("Commit and review package sources before packaging")
    pins = document(REPO / "packaging/installer-source-pins.json")
    if pins.get("availability") != "Unavailable" or pins.get("generationSha256") is not None or pins.get("bootstrapSha256") is not None:
        raise ValueError("Preview scope changed; source qualification requires new review")
    report, entries = validate_publish(root)
    assets = document(root / "build/obj/KuaiYi.App/project.assets.json")
    folders = list(assets["packageFolders"])
    if len(folders) != 1:
        raise ValueError("Exact original restored package cache required")
    cache = Path(folders[0])
    if not cache.is_relative_to(REPO / "artifacts") or any(p.is_symlink() for p in (cache, *cache.parents)):
        raise ValueError("Original local runtime cache required")
    packages = publisher.runtime_packages(cache)
    publisher.inspect_skia(root / "roles/ImageWorker", cache)
    runtime = publisher.INPUTS.RUNTIME.runtime_payload_records(packages)
    entries, provenance = flatten(entries, runtime)
    extension = subprocess.check_output(["git", "ls-files", "-z", "--", "extensions/chromium"], cwd=REPO).decode().split("\0")
    for rel in sorted(filter(None, extension)):
        p = REPO / rel
        entries.append((name(rel), p, regular(p).st_size, digest(p)))
    # Only known authored handoff files, never logs, secrets, user settings or a directory glob.
    for rel in ["docs/release/README-preview.md", "docs/release/WINDOWS-TESTS.md", "scripts/verify-preview.ps1"]:
        p = REPO / rel
        entries.append((name(rel), p, regular(p).st_size, digest(p)))
    manifest = {"schemaVersion": 1, "kind": "UnsealedManagedPreview_NotInstaller", "version": version,
                "buildSourceHead": report["sourceHead"], "packagingSourceHead": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=REPO, text=True).strip(),
                "packagerSha256": digest(Path(__file__)),
                "sdk": report["sdk"], "runtime": report["runtime"], "windowsExecution": "NotRunForThisBuild",
                "installer": False, "productionPins": "Unavailable", "nativeProductDlls": "NotIncluded",
                "normalStartupIssuer": "Incomplete_NotGrantedByThisManifest",
                "originalRoleInventories": report["roles"], "mergedProvenance": provenance,
                "files": [{"path": n, "bytes": s, "sha256": h} for n, _, s, h in entries]}
    raw = (json.dumps(manifest, indent=2, ensure_ascii=False) + "\n").encode()
    output.mkdir()
    target = output / ("KuaiYi-" + version + "-unsealed-managed-win-x64.zip")
    write_zip(target, entries, {"manifest.json": raw})
    # Whole original publish and product source are checked again after archive readback.
    validate_publish(root)
    result = {"Status": "Pass_PreviewPackagingOnly", "Asset": target.name, "Bytes": target.stat().st_size,
              "Sha256": digest(target), "BuildSourceHead": report["sourceHead"], "Files": len(entries) + 1,
              "WindowsExecution": "NotRun", "Installer": False}
    (output / "SHA256SUMS.txt").write_text(result["Sha256"] + "  " + target.name + "\n", encoding="ascii")
    (output / "package-result.json").write_text(json.dumps(result, indent=2) + "\n")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("publish", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--version", required=True)
    args = parser.parse_args()
    print(json.dumps(package(args.publish, args.output, args.version)))
