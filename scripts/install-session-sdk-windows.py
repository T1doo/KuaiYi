"""Prepare the specifically approved Windows SDK in one fresh CI job directory only."""
import hashlib
import json
from pathlib import Path
import re
import stat
import time
import urllib.error
import urllib.request
import zipfile


SDK = "10.0.402"
RUNTIME = "10.0.13"
URL = "https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.402/dotnet-sdk-10.0.402-win-x64.zip"
ARCHIVE_BYTES = 300755826
ARCHIVE_SHA512 = "ae4fd528cb7e9f6af6aaf7de36715a69f761f183744f1bb3025d19f5a92447090ec561410e46266c1d10a63781ed6f3de771760bc76c876d87daabc367b898cd"
LICENSES = {"LICENSE.txt": "7f6839a61ce892b79c6549e2dc5a81fdbd240a0b260f8881216b45b7fda8b45d",
            "ThirdPartyNotices.txt": "deb4427a295e1ed474b0d81c5a0d972c1b550b9a715cda939cdfa9236b1b418f"}
MAX_ENTRIES = 6000
MAX_EXPANDED = 1024 * 1024 * 1024
MAX_FILE = 128 * 1024 * 1024


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, stream, code, message, headers, new_url):
        if stream is not None:
            stream.close()
        raise urllib.error.HTTPError(request.full_url, code, "SDK redirect refused before following", headers, None)


def open_sdk():
    return urllib.request.build_opener(NoRedirect()).open(URL, timeout=45)


def unlinked(path):
    for item in (path, *path.parents):
        if item.is_symlink():
            return False
        try:
            if getattr(item.lstat(), "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT:
                return False
        except FileNotFoundError:
            continue
    return True


def checked_entries(archive):
    entries = archive.infolist()
    if not entries or len(entries) > MAX_ENTRIES:
        raise RuntimeError("SDK archive entry count exceeds bound")
    names, files, total = set(), set(), 0
    result = []
    for entry in entries:
        name = entry.orig_filename
        parts = name.rstrip("/").split("/")
        mode = stat.S_IFMT(entry.external_attr >> 16)
        if (not name or "\\" in name or any(not part or part in (".", "..") or part.rstrip(" .") != part or
                re.search(r'[<>:"|?*\x00-\x1f]', part) or
                re.match(r"^(?:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)", part, re.I) for part in parts) or
                mode not in (0, stat.S_IFREG, stat.S_IFDIR) or mode == stat.S_IFDIR and not entry.is_dir() or
                mode == stat.S_IFREG and entry.is_dir() or entry.external_attr & 0x400 or entry.flag_bits & 1 or
                entry.compress_type not in (zipfile.ZIP_STORED, zipfile.ZIP_DEFLATED) or
                entry.is_dir() and entry.file_size != 0):
            raise RuntimeError("SDK archive contains an unsafe Windows path/type")
        key = "/".join(parts).casefold()
        if key in names:
            raise RuntimeError("SDK archive contains duplicate Windows names")
        names.add(key)
        if not entry.is_dir():
            files.add(key)
        total += entry.file_size
        if entry.file_size > MAX_FILE or total > MAX_EXPANDED:
            raise RuntimeError("SDK archive expanded size exceeds bound")
        result.append((entry, parts))
    for _, parts in result:
        if any("/".join(parts[:index]).casefold() in files for index in range(1, len(parts))):
            raise RuntimeError("SDK archive file collides with a directory")
    return result, total


def verify_licenses(archive):
    records = {}
    for name, expected in LICENSES.items():
        entry = archive.getinfo(name)
        if entry.file_size > 2 * 1024 * 1024:
            raise RuntimeError("SDK license exceeds inspection bound")
        data = archive.read(entry)
        digest = hashlib.sha256(data).hexdigest()
        if digest != expected:
            raise RuntimeError("SDK license differs from the exact user-approved Windows archive")
        records[name] = {"Bytes": len(data), "Sha256": digest}
    return records


def verify_metadata(archive):
    def document(name):
        entry = archive.getinfo(name)
        if entry.file_size > 2 * 1024 * 1024:
            raise RuntimeError("SDK metadata exceeds inspection bound")
        return json.loads(archive.read(entry))
    config = document(f"sdk/{SDK}/dotnet.runtimeconfig.json")
    libraries = document(f"sdk/{SDK}/dotnet.deps.json")["libraries"]
    xml_versions = [name for name in libraries if name.startswith("System.Security.Cryptography.Xml/")]
    if config["runtimeOptions"]["framework"] != {"name": "Microsoft.NETCore.App", "version": RUNTIME} or xml_versions != ["System.Security.Cryptography.Xml/" + RUNTIME]:
        raise RuntimeError("SDK archive host/private XML is not the approved security patch")
    for name in ("dotnet.exe", f"shared/Microsoft.NETCore.App/{RUNTIME}/System.Private.CoreLib.dll",
                 f"shared/Microsoft.WindowsDesktop.App/{RUNTIME}/PresentationCore.dll"):
        archive.getinfo(name)


def install(contract, work, deadline, report):
    if contract.get("sdk") != SDK or contract.get("runtime") != RUNTIME:
        raise RuntimeError("Only the specifically approved SDK402/runtime13 is allowed")
    work = Path(work)
    if not work.is_absolute() or not unlinked(work) or not work.is_dir():
        raise RuntimeError("SDK job directory must already exist without links")
    target, packed = work / ("sdk-" + SDK), work / ("sdk-" + SDK + "-win-x64.zip")
    if target.exists() or not unlinked(target) or packed.exists() or not unlinked(packed):
        raise RuntimeError("SDK output already exists or is linked; retained")
    details = {"Status": "InProgress", "Stage": "Download", "Source": URL, "Root": str(target),
               "ApprovedSdk": SDK, "ApprovedRuntime": RUNTIME, "ExpectedArchiveSha512": ARCHIVE_SHA512,
               "LicenseApproval": "User accepted exact Windows archive license, 2026-10-19 16:31 Beijing",
               "LicenseApprovalSource": "Sentinel_e978556d48a48191aa940cf146bdb928"}
    report["SdkInstallation"] = details
    try:
        if time.monotonic() >= deadline:
            raise RuntimeError("SDK attempt time budget exhausted before download")
        digest, count = hashlib.sha512(), 0
        with open_sdk() as response, packed.open("xb") as output:
            if response.geturl() != URL or response.status != 200:
                raise RuntimeError("SDK archive did not arrive from the exact official source")
            while block := response.read(1024 * 1024):
                count += len(block)
                if count > ARCHIVE_BYTES or time.monotonic() >= deadline:
                    raise RuntimeError("SDK archive download exceeds size/time bound")
                digest.update(block)
                output.write(block)
        details.update(ArchiveBytes=count, ArchiveSha512=digest.hexdigest(), Stage="ArchiveIdentity")
        if count != ARCHIVE_BYTES or digest.hexdigest() != ARCHIVE_SHA512:
            raise RuntimeError("SDK archive differs from the pinned official bytes")
        with zipfile.ZipFile(packed) as archive:
            details["Stage"] = "AllEntryValidation"
            entries, total = checked_entries(archive)
            details["Licenses"] = verify_licenses(archive)
            verify_metadata(archive)
            details.update(Stage="ExtractFreshDirectory", Entries=len(entries), ExpandedBytes=total)
            target.mkdir(mode=0o700)
            for entry, parts in entries:
                if time.monotonic() >= deadline:
                    raise RuntimeError("SDK extraction time bound exceeded; partial files retained")
                path = target.joinpath(*parts)
                if not unlinked(path):
                    raise RuntimeError("SDK output path became linked; retained")
                if entry.is_dir():
                    path.mkdir(parents=True, exist_ok=True)
                    continue
                path.parent.mkdir(parents=True, exist_ok=True)
                written = 0
                with archive.open(entry) as source, path.open("xb") as output:
                    while block := source.read(1024 * 1024):
                        written += len(block)
                        if written > entry.file_size or time.monotonic() >= deadline:
                            raise RuntimeError("SDK entry exceeds declared size/time; partial files retained")
                        output.write(block)
                if written != entry.file_size:
                    raise RuntimeError("SDK entry truncated; partial files retained")
        details.update(Status="FilesPrepared", Stage="StaticPolicyRequired")
        return target
    finally:
        if details["Status"] == "InProgress":
            details["Status"] = "Blocked"
