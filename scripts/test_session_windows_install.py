"""Offline SDK install/evidence gates; no external network or SDK process execution."""
import base64
from contextlib import contextmanager
import gzip
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import stat
import tempfile
import time
from types import SimpleNamespace
import unittest
from unittest.mock import patch
import urllib.error
import urllib.request
import zipfile


ROOT = Path(__file__).resolve().parent.parent


def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT / "scripts" / filename)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


sdk = module("windows_sdk", "install-session-sdk-windows.py")
ci = module("windows_sdk_ci", "verify-session-runtime-windows.py")
CONTRACT = {"sdk": "10.0.402", "runtime": "10.0.13"}


@contextmanager
def approved_archive(host="10.0.13"):
    data = io.BytesIO()
    licenses = {"LICENSE.txt": b"PUBLIC_ACCEPTED_LICENSE", "ThirdPartyNotices.txt": b"PUBLIC_NOTICES"}
    with zipfile.ZipFile(data, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for name, value in licenses.items():
            archive.writestr(name, value)
        archive.writestr("dotnet.exe", b"MZ_PUBLIC_NEVER_EXECUTED")
        archive.writestr("sdk/10.0.402/dotnet.runtimeconfig.json", json.dumps({"runtimeOptions": {"framework": {"name": "Microsoft.NETCore.App", "version": host}}}))
        archive.writestr("sdk/10.0.402/dotnet.deps.json", json.dumps({"libraries": {"System.Security.Cryptography.Xml/10.0.13": {}}}))
        archive.writestr("shared/Microsoft.NETCore.App/10.0.13/System.Private.CoreLib.dll", b"PUBLIC_CORE")
        archive.writestr("shared/Microsoft.WindowsDesktop.App/10.0.13/PresentationCore.dll", b"PUBLIC_WPF")
    packed = data.getvalue()
    response = io.BytesIO(packed)
    response.geturl = lambda: sdk.URL
    response.status = 200
    with patch.object(sdk, "ARCHIVE_BYTES", len(packed)), patch.object(sdk, "ARCHIVE_SHA512", hashlib.sha512(packed).hexdigest()), \
            patch.object(sdk, "LICENSES", {name: hashlib.sha256(value).hexdigest() for name, value in licenses.items()}), \
            patch.object(sdk, "open_sdk", return_value=response) as request:
        yield packed, request


class SdkInstallationTests(unittest.TestCase):
    def test_pins_match_the_exact_user_accepted_official_archive(self):
        evidence = json.loads((ROOT / "docs/test-evidence/2026-10-19-session-windows-sdk-assessment.json").read_text())
        self.assertEqual(evidence["Asset"]["url"], sdk.URL)
        self.assertEqual(evidence["Asset"]["hash"], sdk.ARCHIVE_SHA512)
        self.assertEqual(evidence["ArchiveBytes"], sdk.ARCHIVE_BYTES)
        for name, expected in sdk.LICENSES.items():
            path = ROOT / "docs/test-evidence" / ("2026-10-19-session-windows-sdk-assessment-originals__windows-" + name)
            self.assertEqual(expected, hashlib.sha256(path.read_bytes()).hexdigest())

    def test_prepares_only_fresh_owned_directory_and_keeps_archive_and_licenses(self):
        with tempfile.TemporaryDirectory() as folder, approved_archive() as (packed, request):
            work, report = Path(folder), {}
            target = sdk.install(CONTRACT, work, time.monotonic() + 30, report)
            self.assertEqual(work / "sdk-10.0.402", target)
            self.assertTrue((target / "dotnet.exe").is_file())
            self.assertEqual(packed, (work / "sdk-10.0.402-win-x64.zip").read_bytes())
            self.assertEqual("FilesPrepared", report["SdkInstallation"]["Status"])
            self.assertEqual(set(sdk.LICENSES), set(report["SdkInstallation"]["Licenses"]))
            self.assertEqual(1, request.call_count)

    def test_mismatched_archive_license_or_host_stops_before_sdk_directory(self):
        for failure in ("hash", "license", "host"):
            with self.subTest(failure=failure), tempfile.TemporaryDirectory() as folder, approved_archive(
                    host="10.0.12" if failure == "host" else "10.0.13"):
                report = {}
                with patch.object(sdk, "ARCHIVE_SHA512", "0" * 128 if failure == "hash" else sdk.ARCHIVE_SHA512), \
                        patch.object(sdk, "LICENSES", {"LICENSE.txt": "0" * 64} if failure == "license" else sdk.LICENSES):
                    with self.assertRaises(RuntimeError):
                        sdk.install(CONTRACT, Path(folder), time.monotonic() + 30, report)
                self.assertFalse((Path(folder) / "sdk-10.0.402").exists())
                self.assertEqual("Blocked", report["SdkInstallation"]["Status"])

    def test_existing_linked_expired_or_other_version_refuses_before_network(self):
        for failure in ("existing", "linked", "expired", "version"):
            with self.subTest(failure=failure), tempfile.TemporaryDirectory() as folder, approved_archive() as (_, request):
                work, contract, deadline = Path(folder), CONTRACT, time.monotonic() + 30
                if failure == "existing":
                    (work / "sdk-10.0.402").mkdir()
                    (work / "sdk-10.0.402/PUBLIC_KEEP").write_text("PUBLIC_UNCHANGED")
                elif failure == "linked":
                    target = work / "PUBLIC_REAL"
                    target.mkdir()
                    work = work / "PUBLIC_LINK"
                    work.symlink_to(target, target_is_directory=True)
                elif failure == "expired":
                    deadline = 0
                else:
                    contract = dict(CONTRACT, sdk="10.0.401")
                with self.assertRaises(RuntimeError):
                    sdk.install(contract, work, deadline, {})
                request.assert_not_called()
                if failure == "existing":
                    self.assertEqual("PUBLIC_UNCHANGED", (work / "sdk-10.0.402/PUBLIC_KEEP").read_text())

    def test_http_denial_is_one_call_and_redirect_handler_never_follows(self):
        with tempfile.TemporaryDirectory() as folder, patch.object(sdk, "open_sdk", side_effect=urllib.error.HTTPError(
                sdk.URL, 403, "PUBLIC_DENIED", {}, None)) as request:
            report = {}
            with self.assertRaises(urllib.error.HTTPError):
                sdk.install(CONTRACT, Path(folder), time.monotonic() + 30, report)
            self.assertEqual(1, request.call_count)
            self.assertEqual("Blocked", report["SdkInstallation"]["Status"])
        stream = io.BytesIO(b"PUBLIC_REDIRECT")
        with self.assertRaises(urllib.error.HTTPError):
            sdk.NoRedirect().redirect_request(urllib.request.Request(sdk.URL), stream, 302, "PUBLIC", {}, "https://PUBLIC_UNRELATED.invalid")
        self.assertTrue(stream.closed)

    def test_windows_path_aliases_duplicates_and_file_parent_collisions_are_rejected(self):
        for names in (("/absolute",), ("../escape",), ("a/../escape",), ("a\\b",), ("C:/drive",),
                      ("file:stream",), ("bad.",), ("bad ",), ("CON",), ("AUX.txt",), ("a//b",),
                      ("bad\x00hidden",), ("a", "A"), ("a", "a/b")):
            with self.subTest(names=names):
                entries = [zipfile.ZipInfo(name) for name in names]
                with self.assertRaises(RuntimeError):
                    sdk.checked_entries(SimpleNamespace(infolist=lambda: entries))

    def test_links_reparse_types_and_expansion_limits_are_rejected(self):
        for attributes in ((stat.S_IFLNK | 0o777) << 16, 0x400, stat.S_IFDIR << 16):
            entry = zipfile.ZipInfo("PUBLIC")
            entry.external_attr = attributes
            with self.subTest(attributes=attributes), self.assertRaises(RuntimeError):
                sdk.checked_entries(SimpleNamespace(infolist=lambda: [entry]))
        entry = zipfile.ZipInfo("PUBLIC")
        entry.file_size = 2
        for limit in ("MAX_ENTRIES", "MAX_FILE", "MAX_EXPANDED"):
            with self.subTest(limit=limit), patch.object(sdk, limit, 0), self.assertRaises(RuntimeError):
                sdk.checked_entries(SimpleNamespace(infolist=lambda: [entry]))


class EvidenceTests(unittest.TestCase):
    def test_emitted_chunks_recover_exact_raw_and_gzip_bytes_only_for_registered_paths(self):
        with tempfile.TemporaryDirectory() as folder:
            work = Path(folder)
            data = ("PUBLIC_UTF8_测试\n" * 1000).encode()
            (work / "PUBLIC.log").write_bytes(data)
            (work / "UNREGISTERED.log").write_text("PUBLIC_NOT_EMITTED")
            report = {"Commands": [{"Name": "PUBLIC", "Log": "PUBLIC.log"}]}
            lines = []
            with patch.object(ci, "emit_ci_line", side_effect=lines.append):
                ci.emit_evidence(work, report)
            self.assertEqual("EmittedToJobLog", report["EvidenceStatus"])
            self.assertEqual(["PUBLIC"], [item["Name"] for item in report["EvidenceFiles"]])
            manifest = json.loads(lines[0].split(" ", 1)[1])
            chunks = [json.loads(line.split(" ", 1)[1]) for line in lines[1:]]
            self.assertEqual(list(range(1, manifest["Chunks"] + 1)), [item["Index"] for item in chunks])
            self.assertTrue(all(item["Total"] == manifest["Chunks"] for item in chunks))
            packed = base64.b64decode("".join(item["Data"] for item in chunks))
            self.assertEqual(manifest["GzipSha256"], hashlib.sha256(packed).hexdigest())
            self.assertEqual(manifest["GzipBytes"], len(packed))
            self.assertEqual(manifest["Sha256"], hashlib.sha256(data).hexdigest())
            self.assertEqual(manifest["Bytes"], len(data))
            self.assertEqual(data, gzip.decompress(packed))

    def test_missing_or_oversized_evidence_is_incomplete_and_original_failure_remains(self):
        for failure in ("missing", "oversized", "trx-missing"):
            with self.subTest(failure=failure), tempfile.TemporaryDirectory() as folder:
                work = Path(folder)
                name = "Core-full" if failure == "trx-missing" else "PUBLIC"
                if failure != "missing":
                    with (work / (name + ".log")).open("wb") as stream:
                        stream.truncate(16 * 1024 * 1024 + 1 if failure == "oversized" else 0)
                report = {"Status": "Blocked", "Reason": "PUBLIC_ORIGINAL_FAILURE", "Commands": [{"Name": name, "Log": name + ".log"}]}
                with patch.object(ci, "emit_ci_line"):
                    ci.emit_evidence(work, report)
                self.assertEqual("Incomplete", report["EvidenceStatus"])
                self.assertEqual("PUBLIC_ORIGINAL_FAILURE", report["Reason"])
                self.assertEqual("Blocked", report["Status"])
                self.assertTrue(report["EvidenceErrors"])

    def test_changed_log_emits_snapshot_but_marks_evidence_incomplete(self):
        with tempfile.TemporaryDirectory() as folder:
            work = Path(folder)
            (work / "PUBLIC.log").write_text("PUBLIC_LATER_BYTES")
            report = {"Reason": "PUBLIC_FAILURE", "Commands": [{"Name": "PUBLIC", "Log": "PUBLIC.log", "LogSha256": "0" * 64}]}
            with patch.object(ci, "emit_ci_line"):
                ci.emit_evidence(work, report)
            self.assertEqual("Incomplete", report["EvidenceStatus"])
            self.assertEqual("PUBLIC_FAILURE", report["Reason"])
            self.assertEqual(1, len(report["EvidenceFiles"]))


if __name__ == "__main__":
    unittest.main()
