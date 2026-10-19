"""Offline safety-gate tests. No .NET execution, package restore or model calls."""
import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

spec = importlib.util.spec_from_file_location("session_preparation", Path(__file__).with_name("prepare-session-test.py"))
preparation = importlib.util.module_from_spec(spec)
spec.loader.exec_module(preparation)


class AuditTests(unittest.TestCase):
    project = Path(__file__).resolve().parent.parent / "packaging/session-runtime-audit/session-runtime-audit.csproj"

    def inventory(self):
        return {"version": 1, "projects": [{"path": str(self.project), "frameworks": [{"framework": "net10.0", "topLevelPackages": [
            {"id": identity, "resolvedVersion": "10.0.13"} for identity in preparation.RUNTIME_IDS]}]}]}

    def audit(self):
        return {"version": 1, "sources": [preparation.NUGET_SOURCE], "projects": [{"path": str(self.project)}]}

    def check(self, audit, inventory=None):
        return preparation.require_no_advisories(audit, self.project, inventory or self.inventory(), "10.0.13")

    def test_clean_vulnerable_output_can_omit_unaffected_packages(self):
        self.assertEqual(2, self.check(self.audit())["PackageCount"])

    def test_empty_project_previously_accepted_is_refused(self):
        audit = self.audit()
        audit["projects"] = [{}]
        with self.assertRaises(RuntimeError):
            self.check(audit)

    def test_unrelated_project_with_error_previously_accepted_is_refused(self):
        audit = self.audit()
        audit.update(projects=[{"path": "unrelated.csproj", "frameworks": []}], problems=[{"level": "error"}])
        with self.assertRaises(RuntimeError):
            self.check(audit)

    def test_missing_wrong_duplicate_sources_are_refused(self):
        for sources in (None, [], ["PUBLIC_UNRELATED"], [preparation.NUGET_SOURCE] * 2):
            with self.subTest(sources=sources):
                audit = self.audit()
                audit["sources"] = sources
                with self.assertRaises(RuntimeError):
                    self.check(audit)

    def test_missing_or_multiple_projects_are_refused(self):
        for projects in (None, [], [{}], [self.audit()["projects"][0]] * 2):
            with self.subTest(projects=projects):
                audit = self.audit()
                audit["projects"] = projects
                with self.assertRaises(RuntimeError):
                    self.check(audit)

    def test_problems_at_each_inventory_and_audit_level_are_refused(self):
        for source in ("audit", "inventory"):
            for level in ("root", "project", "framework"):
                for key in ("problems", "errors", "warnings"):
                    with self.subTest(source=source, level=level, key=key):
                        inventory, audit = self.inventory(), self.audit()
                        audit["projects"][0]["frameworks"] = [{"framework": "net10.0"}]
                        doc = audit if source == "audit" else inventory
                        node = doc if level == "root" else doc["projects"][0]
                        if level == "framework":
                            node = node["frameworks"][0]
                        node[key] = [{"level": "warning", "text": "PUBLIC_AUDIT_INCOMPLETE"}]
                        with self.assertRaises(RuntimeError):
                            self.check(audit, inventory)

    def test_runtime_inventory_must_prove_both_exact_pins(self):
        for mode in ("missing", "wrong-version", "extra", "duplicate", "empty", "wrong-project"):
            with self.subTest(mode=mode):
                inventory = self.inventory()
                packages = inventory["projects"][0]["frameworks"][0]["topLevelPackages"]
                if mode == "missing":
                    packages.pop()
                elif mode == "wrong-version":
                    packages[0]["resolvedVersion"] = "10.0.12"
                elif mode == "extra":
                    packages.append({"id": "PUBLIC_EXTRA", "resolvedVersion": "1.0.0"})
                elif mode == "duplicate":
                    packages.append(copy.deepcopy(packages[0]))
                elif mode == "empty":
                    packages.clear()
                else:
                    inventory["projects"][0]["path"] = "PUBLIC_UNRELATED.csproj"
                with self.assertRaises(RuntimeError):
                    self.check(self.audit(), inventory)

    def test_reported_vulnerability_and_inventory_mismatch_are_refused(self):
        for mode in ("vulnerability", "malformed-vulnerabilities", "version", "framework", "package"):
            with self.subTest(mode=mode):
                audit = self.inventory()
                audit["sources"] = [preparation.NUGET_SOURCE]
                item = audit["projects"][0]["frameworks"][0]["topLevelPackages"][0]
                if mode == "vulnerability":
                    item["vulnerabilities"] = [{"severity": "High", "advisoryurl": "PUBLIC_ADVISORY"}]
                elif mode == "malformed-vulnerabilities":
                    item["vulnerabilities"] = {}
                elif mode == "version":
                    item["resolvedVersion"] = "10.0.12"
                elif mode == "framework":
                    audit["projects"][0]["frameworks"][0]["framework"] = "net9.0"
                else:
                    item["id"] = "PUBLIC_UNAUDITED"
                with self.assertRaises(RuntimeError):
                    self.check(audit)

    def test_app_zero_packages_require_actual_scoped_project_assets(self):
        inventory = self.inventory()
        inventory["projects"][0]["frameworks"][0]["topLevelPackages"] = []
        assets = {"version": 4, "libraries": {"PUBLIC_PROJECT/1.0.0": {"type": "project"}},
                  "project": {"restore": {"projectUniqueName": str(self.project)}, "frameworks": {"net10.0": {}}}}
        result = preparation.require_no_advisories(self.audit(), self.project, inventory, assets=assets)
        self.assertEqual(0, result["PackageCount"])
        for mode in ("missing-assets", "wrong-project", "package-present", "runtime-audit"):
            with self.subTest(mode=mode):
                changed = copy.deepcopy(assets)
                runtime = None
                if mode == "missing-assets":
                    changed = None
                elif mode == "wrong-project":
                    changed["project"]["restore"]["projectUniqueName"] = "PUBLIC_OTHER.csproj"
                elif mode == "package-present":
                    changed["libraries"]["PUBLIC_PACKAGE/1.0.0"] = {"type": "package"}
                else:
                    runtime = "10.0.13"
                with self.assertRaises(RuntimeError):
                    preparation.require_no_advisories(self.audit(), self.project, inventory, runtime=runtime, assets=changed)


class SignatureTests(unittest.TestCase):
    def output(self):
        return "\n".join(("Verifying PUBLIC_RUNTIME.10.0.13", "Content hash: PUBLIC_HASH",
                          "Signature type: Author", "Signature type: Repository"))

    def check(self, output):
        preparation.require_verified_signature(output, "PUBLIC_RUNTIME", "10.0.13", "PUBLIC_HASH")

    def test_complete_warning_free_verification_is_accepted(self):
        self.check(self.output())

    def test_inherited_offline_or_disabled_verification_is_refused(self):
        for env in ({}, {"NUGET_CERT_REVOCATION_MODE": "online", "DOTNET_NUGET_SIGNATURE_VERIFICATION": "true"}):
            preparation.require_signature_environment(env)
        for env in ({"NUGET_CERT_REVOCATION_MODE": "offline"}, {"DOTNET_NUGET_SIGNATURE_VERIFICATION": "false"}):
            with self.subTest(env=env), self.assertRaisesRegex(RuntimeError, "requires online"):
                preparation.require_signature_environment(env)

    def test_zero_exit_revocation_and_other_warnings_are_refused(self):
        for warning in ("warn : NU3018: RevocationStatusUnknown", "warn : NU3028: unable to get certificate CRL",
                        "warning : PUBLIC_UNKNOWN_WARNING", "error : PUBLIC_ERROR", "info : RevocationStatusUnknown"):
            with self.subTest(warning=warning), self.assertRaisesRegex(RuntimeError, "warnings/errors"):
                self.check(self.output() + "\n" + warning)

    def test_empty_or_unrelated_verification_is_refused(self):
        for output in ("", self.output().replace("PUBLIC_HASH", "PUBLIC_OTHER_HASH"),
                       self.output().replace("PUBLIC_RUNTIME", "PUBLIC_OTHER"),
                       self.output().replace("Signature type: Author", ""),
                       self.output().replace("Signature type: Repository", "")):
            with self.subTest(output=output), self.assertRaises(RuntimeError):
                self.check(output)


class HostTests(unittest.TestCase):
    def test_unsafe_host_is_refused_before_any_dotnet_process(self):
        with tempfile.TemporaryDirectory(prefix="kuaiyi-safe-sdk-test-") as temp:
            root = Path(temp)
            sdk = root / "sdk/10.0.402"
            sdk.mkdir(parents=True)
            (sdk / "dotnet.runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {"framework": {"version": "10.0.12"}}}))
            with patch.object(preparation.shutil, "which", return_value=str(root / "dotnet")), patch.object(preparation.subprocess, "run") as run:
                with self.assertRaisesRegex(RuntimeError, "no dotnet execution"):
                    preparation.require_safe_sdk({"sdk": "10.0.402", "runtime": "10.0.13"})
                run.assert_not_called()

    def test_private_xml_must_also_be_patched(self):
        with tempfile.TemporaryDirectory(prefix="kuaiyi-safe-sdk-test-") as temp:
            root = Path(temp)
            sdk = root / "sdk/10.0.402"
            sdk.mkdir(parents=True)
            (sdk / "dotnet.runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {"framework": {"version": "10.0.13"}}}))
            (sdk / "dotnet.deps.json").write_text(json.dumps({"libraries": {"System.Security.Cryptography.Xml/10.0.12": {}}}))
            with patch.object(preparation.shutil, "which", return_value=str(root / "dotnet")):
                with self.assertRaisesRegex(RuntimeError, "private XML"):
                    preparation.require_safe_sdk({"sdk": "10.0.402", "runtime": "10.0.13"})


class PayloadTests(unittest.TestCase):
    def write_pack(self, root, entry=None):
        pack = root / "PUBLIC.nupkg"
        member = "runtimes/win-x64/lib/net10.0/System.Private.CoreLib.dll"
        entry = entry or f'<File Type="Managed" Path="{member}" />'
        with zipfile.ZipFile(pack, "x") as archive:
            archive.writestr("data/RuntimeList.xml", "<FileList>" + entry + "</FileList>")
            archive.writestr(member, b"PUBLIC_PATCHED_13_BYTES")
        return pack

    def test_actual_payload_bytes_must_match_even_when_pack_and_version_remain_correct(self):
        with tempfile.TemporaryDirectory(prefix="kuaiyi-payload-test-") as temp:
            root = Path(temp)
            pack = self.write_pack(root)
            output = root / "publish"
            output.mkdir()
            target = output / "System.Private.CoreLib.dll"
            target.write_bytes(b"PUBLIC_PATCHED_13_BYTES")
            args = (output, [("PUBLIC_RUNTIME", "10.0.13", pack)])
            self.assertEqual(1, len(preparation.bind_runtime_payload(*args)))
            target.write_bytes(b"PUBLIC_REPLACED_12_BYTES")
            with self.assertRaisesRegex(RuntimeError, "differs"):
                preparation.bind_runtime_payload(*args)

    def test_missing_payload_is_refused(self):
        with tempfile.TemporaryDirectory(prefix="kuaiyi-payload-test-") as temp:
            root = Path(temp)
            with self.assertRaisesRegex(RuntimeError, "differs"):
                preparation.bind_runtime_payload(root, [("PUBLIC_RUNTIME", "10.0.13", self.write_pack(root))])

    def test_runtime_inventory_traversal_is_refused(self):
        with tempfile.TemporaryDirectory(prefix="kuaiyi-payload-test-") as temp:
            root = Path(temp)
            pack = self.write_pack(root, '<File Type="Managed" Path="runtimes/win-x64/../PUBLIC.dll" />')
            with self.assertRaisesRegex(RuntimeError, "path invalid"):
                preparation.bind_runtime_payload(root, [("PUBLIC_RUNTIME", "10.0.13", pack)])

    def test_wpf_resource_profile_and_shared_assets_use_exact_payload_paths(self):
        with tempfile.TemporaryDirectory(prefix="kuaiyi-payload-test-") as temp:
            root = Path(temp)
            pack = root / "PUBLIC_WPF.nupkg"
            shared = "runtimes/win-x64/lib/net10.0/PUBLIC_SHARED.dll"
            resource = "runtimes/win-x64/lib/net10.0/zh-Hans/PUBLIC.resources.dll"
            forms = "runtimes/win-x64/lib/net10.0/PUBLIC_FORMS.dll"
            with zipfile.ZipFile(pack, "x") as archive:
                archive.writestr("data/RuntimeList.xml", '<FileList>'
                                 f'<File Type="Managed" Path="{shared}" Profile="WindowsForms;WPF" />'
                                 f'<File Type="Resources" Path="{resource}" Culture="zh-Hans" Profile="WPF" />'
                                 f'<File Type="Managed" Path="{forms}" Profile="WindowsForms" />'
                                 '</FileList>')
                archive.writestr(shared, b"PUBLIC_SHARED")
                archive.writestr(resource, b"PUBLIC_RESOURCE")
                archive.writestr(forms, b"PUBLIC_FORMS_ONLY")
            (root / "PUBLIC_SHARED.dll").write_bytes(b"PUBLIC_SHARED")
            (root / "zh-Hans").mkdir()
            target = root / "zh-Hans/PUBLIC.resources.dll"
            target.write_bytes(b"PUBLIC_RESOURCE")
            args = (root, [("PUBLIC_RUNTIME", "10.0.13", pack)])
            self.assertEqual(["PUBLIC_SHARED.dll", "zh-Hans/PUBLIC.resources.dll"],
                             [item["Path"] for item in preparation.bind_runtime_payload(*args)])
            target.write_bytes(b"PUBLIC_REPLACED_RESOURCE")
            with self.assertRaisesRegex(RuntimeError, "differs"):
                preparation.bind_runtime_payload(*args)


if __name__ == "__main__":
    unittest.main()
