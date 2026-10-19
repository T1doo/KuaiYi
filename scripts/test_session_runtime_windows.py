"""Offline contracts for the one Windows CI attempt. No external requests or .NET execution."""
import base64
from contextlib import contextmanager
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch
import zipfile

import yaml


ROOT = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location("runtime_windows", ROOT / "scripts/verify-session-runtime-windows.py")
ci = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ci)
policy = ci.load_policy(ROOT)


@contextmanager
def execution_fixture(timeout=False, chocolatey=False, framework=True):
    """Public synthetic bytes only; no external request or executable is started."""
    with tempfile.TemporaryDirectory() as folder:
        root = Path(folder)
        repo = root / "repo"
        (repo / "scripts").mkdir(parents=True)
        shutil.copyfile(ROOT / "scripts/prepare-session-test.py", repo / "scripts/prepare-session-test.py")
        audit = repo / "packaging/session-runtime-audit"
        audit.mkdir(parents=True)
        (repo / "packaging/session-test.json").write_text(json.dumps({"sdk": "10.0.402", "runtime": "10.0.13"}))
        packages, reviewed, locked = {}, {}, {}
        for identity in policy.RUNTIME_IDS:
            data = io.BytesIO()
            with zipfile.ZipFile(data, "w") as archive:
                archive.writestr("PUBLIC.nuspec", f'<package><metadata><id>{identity}</id><version>10.0.13</version><license type="expression">MIT</license></metadata></package>')
            packages[identity.lower()] = data.getvalue()
            content_hash = "PUBLIC_CONTENT_" + identity
            reviewed[identity] = {"Version": "10.0.13", "Source": policy.NUGET_SOURCE,
                                  "ArchiveSha512": base64.b64encode(hashlib.sha512(data.getvalue()).digest()).decode(),
                                  "NuGetContentHash": content_hash}
            locked[identity] = {"contentHash": content_hash}
        (audit / "runtime-pack-integrity.json").write_text(json.dumps({"Packages": reviewed}))
        (audit / "packages.lock.json").write_text(json.dumps({"dependencies": {"net10.0": locked}}))
        if chocolatey:
            install = root / "chocolatey"
            lookup = install / "bin/nuget.exe"
            nuget = install / "lib/NuGet.CommandLine/tools/nuget.exe"
            lookup.parent.mkdir(parents=True)
            nuget.parent.mkdir(parents=True)
            lookup.write_bytes(b"MZ_PUBLIC_NATIVE_SHIM_WITHOUT_FRAMEWORK_METADATA")
        else:
            lookup = nuget = root / "nuget.exe"
        nuget.write_bytes(b"MZ_PUBLIC_FIXTURE_" + (b".NETFramework,Version=v4.7.2" if framework else b".NETCoreApp,Version=v10.0"))

        def request(uri, **kwargs):
            self_bytes = packages[uri.split("/")[-3]]
            response = io.BytesIO(self_bytes)
            response.geturl = lambda: uri
            return response

        def process(argv, **kwargs):
            if argv[0] != str(nuget):
                raise AssertionError("Shim or unexpected executable must never run: " + str(argv))
            if argv[1] == "help":
                kwargs["stdout"].write(b"NuGet Version: 7.9.0.83\n")
                if timeout:
                    kwargs["stdout"].write(b"PUBLIC_TIMEOUT_DETAIL\n")
                    raise subprocess.TimeoutExpired(argv, kwargs["timeout"])
            else:
                if argv[0] != str(nuget) or argv[1:3] != ["verify", "-All"]:
                    raise AssertionError("Unexpected real SDK/tool invocation: " + str(argv))
                identity = next(key for key in reviewed if Path(argv[3]).name.startswith(key.lower() + "."))
                output = "\n".join((f"Verifying {identity}.10.0.13", "Content hash: " + reviewed[identity]["NuGetContentHash"],
                                     "Signature type: Author", "Signature type: Repository"))
                kwargs["stdout"].write(output.encode())
            return subprocess.CompletedProcess(argv, 0)

        with patch.object(ci, "source_gate", return_value="a" * 40), patch.object(ci.shutil, "which", return_value=str(lookup)), \
                patch.object(ci.urllib.request, "urlopen", side_effect=request) as download, \
                patch.object(ci.subprocess, "run", side_effect=process) as run:
            # Windows os.environ stores uppercase keys; execute receives a plain dict.
            env = {"GITHUB_SHA": "a" * 40, "PROGRAMFILES": str(root / "missing-sdk")}
            if chocolatey:
                env["CHOCOLATEYINSTALL"] = str(install)
            yield repo, env, run, download


class WindowsCiTests(unittest.TestCase):
    def test_known_chocolatey_shim_resolves_payload_before_framework_gate(self):
        with execution_fixture(chocolatey=True) as (repo, env, run, download):
            report, code = ci.execute(repo, env)
            self.assertEqual(2, code)
            self.assertEqual("SignaturesPassedTestsBlocked", report["Status"])
            self.assertEqual("Pass_WindowsNuGetFrameworkOnline", report["SignatureStatus"])
            self.assertEqual(2, len(report["RuntimePackages"]))
            self.assertEqual(3, run.call_count)
            self.assertEqual(2, download.call_count)
            self.assertEqual("ChocolateyShim", report["VerifierLookup"]["Kind"])
            self.assertEqual([], report["VerifierLookup"]["FrameworkMarkers"])
            self.assertEqual("ChocolateyPackagePayload", report["VerifierTarget"]["Kind"])
            self.assertEqual([".NETFramework,Version=v4.7.2"], report["VerifierTarget"]["FrameworkMarkers"])

    def test_chocolatey_payload_still_requires_framework_before_any_process(self):
        with execution_fixture(chocolatey=True, framework=False) as (repo, env, run, download):
            report, code = ci.execute(repo, env)
            self.assertEqual(2, code)
            self.assertEqual("NotRun", report["SignatureStatus"])
            self.assertIn("expected Framework target", report["Reason"])
            self.assertEqual("TargetStaticIdentity", report["VerifierStage"])
            self.assertEqual([".NETCoreApp,Version=v10.0"], report["VerifierTarget"]["FrameworkMarkers"])
            run.assert_not_called()
            download.assert_not_called()

    def test_missing_or_linked_known_payload_never_executes_or_downloads(self):
        for problem in ("missing", "target-link", "lookup-link", "root-reparse"):
            with self.subTest(problem=problem), execution_fixture(chocolatey=True) as (repo, env, run, download):
                install = Path(env["CHOCOLATEYINSTALL"])
                target = install / "lib/NuGet.CommandLine/tools/nuget.exe"
                lookup = install / "bin/nuget.exe"
                if problem in ("missing", "target-link"):
                    target.unlink()
                if problem == "target-link":
                    replacement = repo.parent / "PUBLIC-nuget-payload.exe"
                    replacement.write_bytes(b"MZ_PUBLIC_FIXTURE_.NETFramework,Version=v4.7.2")
                    target.symlink_to(replacement)
                if problem == "lookup-link":
                    replacement = repo.parent / "PUBLIC-nuget-shim.exe"
                    replacement.write_bytes(lookup.read_bytes())
                    lookup.unlink()
                    lookup.symlink_to(replacement)
                original = ci.linked
                with patch.object(ci, "linked", side_effect=lambda path: (problem == "root-reparse" and path == install) or original(path)):
                    report, code = ci.execute(repo, env)
                self.assertEqual(2, code)
                self.assertEqual("NotRun", report["SignatureStatus"])
                self.assertEqual("LookupPathGuard" if problem in ("lookup-link", "root-reparse") else
                                 "TargetStaticIdentity" if problem == "missing" else "TargetPathGuard", report["VerifierStage"])
                run.assert_not_called()
                download.assert_not_called()

    def test_checkout_verifier_path_is_rejected_without_execution(self):
        with execution_fixture() as (repo, env, run, download):
            path = repo / "nuget.exe"
            path.write_bytes(b"MZ_PUBLIC_FIXTURE_.NETFramework,Version=v4.7.2")
            with patch.object(ci.shutil, "which", return_value=str(path)):
                report, code = ci.execute(repo, env)
            self.assertEqual(2, code)
            self.assertIn("outside the checkout", report["Reason"])
            self.assertEqual("LookupPathGuard", report["VerifierStage"])
            run.assert_not_called()
            download.assert_not_called()

    def test_unknown_shim_does_not_search_chocolatey_or_execute(self):
        with execution_fixture(framework=False) as (repo, env, run, download):
            env["CHOCOLATEYINSTALL"] = str(repo.parent / "unrelated-chocolatey")
            report, code = ci.execute(repo, env)
            self.assertEqual(2, code)
            self.assertEqual("NotRun", report["SignatureStatus"])
            self.assertIn("expected Framework target", report["Reason"])
            run.assert_not_called()
            download.assert_not_called()

    def test_reparse_guard_works_without_python312_junction_api(self):
        path = Mock()
        path.is_symlink.return_value = False
        path.lstat.return_value = SimpleNamespace(st_file_attributes=1024)
        self.assertTrue(ci.linked(path))
        path.lstat.return_value = SimpleNamespace(st_file_attributes=0)
        self.assertFalse(ci.linked(path))
        path.lstat.side_effect = FileNotFoundError
        self.assertFalse(ci.linked(path))

    def test_signature_path_finishes_before_missing_safe_sdk_blocks_tests(self):
        with execution_fixture() as (repo, env, run, download):
            report, code = ci.execute(repo, env)
            self.assertEqual(2, code)
            self.assertEqual("SignaturesPassedTestsBlocked", report["Status"])
            self.assertEqual("Pass_WindowsNuGetFrameworkOnline", report["SignatureStatus"])
            self.assertEqual("NotRun", report["ManagedTests"])
            self.assertEqual("NotRun", report["BuildStatus"])
            self.assertEqual(2, len(report["RuntimePackages"]))
            self.assertEqual(2, download.call_count)
            self.assertEqual(3, run.call_count)
            self.assertEqual({"nuget.exe"}, {Path(call.args[0][0]).name for call in run.call_args_list})
            self.assertEqual(1, len(list((repo / "artifacts").rglob("validation-result.json"))))

    def test_timeout_retains_failed_prefix_and_does_not_claim_cleanup(self):
        with execution_fixture(timeout=True) as (repo, env, run, download):
            report, code = ci.execute(repo, env)
            self.assertEqual(2, code)
            self.assertEqual("Blocked", report["Status"])
            self.assertEqual("NotRun", report["SignatureStatus"])
            command = report["Commands"][-1]
            self.assertEqual("TimedOut", command["Outcome"])
            self.assertEqual("NotConfirmed", command["Cleanup"])
            self.assertIn("PUBLIC_TIMEOUT_DETAIL", command["OutputPrefix"])
            self.assertEqual(1, run.call_count)
            download.assert_not_called()

    def test_workflow_is_one_public_readonly_bounded_push(self):
        path = ROOT / ".github/workflows/windows-session-runtime.yml"
        workflow = yaml.load(path.read_text(), Loader=yaml.BaseLoader)
        self.assertEqual({"push": {"branches": [ci.BRANCH.removeprefix("refs/heads/")],
                                   "paths": [path.relative_to(ROOT).as_posix()]}}, workflow["on"])
        self.assertEqual({"contents": "read"}, workflow["permissions"])
        self.assertEqual(1, len(workflow["jobs"]))
        job = workflow["jobs"]["signature-then-managed"]
        self.assertEqual("windows-2022", job["runs-on"])
        self.assertEqual("15", job["timeout-minutes"])
        self.assertIn("github.event.repository.private == false", job["if"])
        self.assertIn("github.run_attempt == '1'", job["if"])
        checkout, execute = job["steps"]
        self.assertEqual("actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803", checkout["uses"])
        self.assertEqual("false", checkout["with"]["persist-credentials"])
        self.assertEqual("2", checkout["with"]["fetch-depth"])
        self.assertEqual("powershell", execute["shell"])
        self.assertEqual("14", execute["timeout-minutes"])
        self.assertNotRegex(path.read_text(), "upload-artifact|actions/cache|setup-dotnet|secrets\\.")

    def test_context_rejects_private_and_rerun_before_execution(self):
        with tempfile.TemporaryDirectory() as folder:
            event = Path(folder) / "event.json"
            event.write_text(json.dumps({"repository": {"private": False}}))
            env = {"GITHUB_ACTIONS": "true", "GITHUB_REPOSITORY": ci.REPOSITORY, "GITHUB_REF": ci.BRANCH,
                   "GITHUB_EVENT_NAME": "push", "GITHUB_RUN_ATTEMPT": "1", "RUNNER_OS": "Windows",
                   "GITHUB_SHA": "a" * 40, "GITHUB_EVENT_PATH": str(event)}
            ci.hosted_context(env, "win32")
            for key, value in (("GITHUB_RUN_ATTEMPT", "2"), ("GITHUB_REPOSITORY", "PUBLIC_OTHER/REPO"),
                               ("GITHUB_SHA", "bad"), ("GITHUB_EVENT_NAME", "workflow_dispatch")):
                changed = dict(env, **{key: value})
                with self.subTest(key=key), self.assertRaises(RuntimeError):
                    ci.hosted_context(changed, "win32")
            event.write_text(json.dumps({"repository": {"private": True}}))
            with self.assertRaisesRegex(RuntimeError, "Public repository"):
                ci.hosted_context(env, "win32")

    def test_non_windows_entry_never_executes_or_downloads(self):
        with patch.object(ci.sys, "argv", ["verify-session-runtime-windows.py"]), patch.object(ci.sys, "platform", "linux"), patch.object(ci, "execute") as execute:
            self.assertEqual(2, ci.main())
            execute.assert_not_called()

    def test_exact_parent_and_scope_reject_extra_or_missing_source(self):
        head = "a" * 40
        def runner(parents=None, files=None):
            answers = [head, " ".join(parents or [head, ci.PARENT]), "\n".join(ci.FILES if files is None else files), ""]
            return lambda *_: answers.pop(0)
        self.assertEqual(head, ci.source_gate(runner(), head))
        for run in (runner(parents=[head, "b" * 40]), runner(files=ci.FILES | {"src/PUBLIC_CHANGE.cs"}),
                    runner(files=sorted(ci.FILES)[:-1])):
            with self.assertRaises(RuntimeError):
                ci.source_gate(run, head)

    def test_verifier_must_declare_framework_and_reviewed_version(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "nuget.exe"
            path.write_bytes(b"MZ_PUBLIC_FIXTURE_.NETFramework,Version=v4.7.2")
            self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), ci.verifier_identity(path))
            path.write_bytes(b"MZ_PUBLIC_FIXTURE_.NETCoreApp,Version=v10.0")
            with self.assertRaises(RuntimeError):
                ci.verifier_identity(path)
        ci.require_verifier_version("NuGet Version: 7.9.0.83\n")
        for output in ("", "NuGet Version: 7.9.0.830", "NuGet Version: 7.8.0.0"):
            with self.subTest(output=output), self.assertRaises(RuntimeError):
                ci.require_verifier_version(output)

    def test_content_archive_license_and_identity_are_independent_gates(self):
        with tempfile.TemporaryDirectory() as folder:
            package = Path(folder) / "PUBLIC.nupkg"
            def write(identity="PUBLIC_RUNTIME", license="MIT", acceptance="false"):
                with zipfile.ZipFile(package, "w") as archive:
                    archive.writestr("PUBLIC.nuspec", f'<package><metadata><id>{identity}</id><version>10.0.13</version><license type="expression">{license}</license><requireLicenseAcceptance>{acceptance}</requireLicenseAcceptance></metadata></package>')
                return base64.b64encode(hashlib.sha512(package.read_bytes()).digest()).decode()
            pin = write()
            ci.package_identity(package, "PUBLIC_RUNTIME", "10.0.13", pin)
            with self.assertRaisesRegex(RuntimeError, "hash"):
                ci.package_identity(package, "PUBLIC_RUNTIME", "10.0.13", "PUBLIC_WRONG_HASH")
            for kwargs in ({"identity": "PUBLIC_WRONG"}, {"license": "PUBLIC_NEW_LICENSE"}, {"acceptance": "true"}):
                pin = write(**kwargs)
                with self.subTest(kwargs=kwargs), self.assertRaisesRegex(RuntimeError, "identity/license"):
                    ci.package_identity(package, "PUBLIC_RUNTIME", "10.0.13", pin)

    def test_nuget_diagnostics_refused_even_when_not_prefixed_warning(self):
        good = "\n".join(("Verifying PUBLIC_RUNTIME.10.0.13", "Content hash: PUBLIC_HASH", "Signature type: Author", "Signature type: Repository"))
        ci.signature_gate(policy, good, "PUBLIC_RUNTIME", "10.0.13", "PUBLIC_HASH")
        for diagnostic in ("NU3018: PUBLIC_UNKNOWN", "INFO: NU3028: PUBLIC_UNKNOWN", "info: offline revocation", "RevocationStatusUnknown", "WARNING: PUBLIC_WARNING"):
            with self.subTest(diagnostic=diagnostic), self.assertRaises(RuntimeError):
                ci.signature_gate(policy, good + "\n" + diagnostic, "PUBLIC_RUNTIME", "10.0.13", "PUBLIC_HASH")

    def test_no_empty_or_skipped_suite_can_be_passed(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "PUBLIC.trx"
            def write(values):
                attributes = " ".join(f'{key}="{value}"' for key, value in values.items())
                path.write_text(f'<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><ResultSummary><Counters {attributes}/></ResultSummary></TestRun>')
            counters = {"total": 2, "executed": 2, "passed": 2, "failed": 0, "notExecuted": 0}
            write(counters)
            self.assertEqual(counters, ci.trx_counters(path))
            for changed in ({"total": 0, "passed": 0, "executed": 0}, {"notExecuted": 1}, {"failed": 1}, {"executed": 1}):
                write(dict(counters, **changed))
                with self.subTest(changed=changed), self.assertRaises(RuntimeError):
                    ci.trx_counters(path)


if __name__ == "__main__":
    unittest.main()
