"""No CLR, product effects or sample allocation: consumed identity and opt-in boundary."""
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

SPEC = importlib.util.spec_from_file_location("full_chain_runner", Path(__file__).with_name("run_maintenance_full_chain.py"))
RUNNER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RUNNER)


def binding(output="Release", case="exact.case", digest="a" * 64):
    prefix = f"tests/KuaiYi.Provider.Tests/bin/{output}/net10.0/"
    return {"case": case, "inputs": [
        {"path": prefix + "KuaiYi.Provider.Tests.dll", "bytes": 123, "sha256": digest},
        {"path": prefix + "de/adapter.resources.dll", "bytes": 17, "sha256": "b" * 64},
    ]}


class FullChainConsumptionTests(unittest.TestCase):
    def test_output_directory_change_cannot_replay_old_consumed_case(self):
        old = binding()
        with tempfile.TemporaryDirectory() as name, patch.object(RUNNER, "ROOT", Path(name)):
            raw = json.dumps(old, sort_keys=True, indent=2).encode()
            sha = hashlib.sha256(raw).hexdigest()
            folder = Path(name) / ("candidate-" + sha[:24])
            folder.mkdir()
            (folder / "binding.json").write_bytes(raw)
            # Legacy marker spelling deliberately differs from the normalized identity.
            (Path(name) / "execution-legacy-key.json").write_text(json.dumps({"candidateSha256": sha}))
            with self.assertRaisesRegex(RuntimeError, "already consumed"):
                RUNNER.reject_consumed(binding("MaintenanceFullChain"))
            self.assertIsInstance(RUNNER.reject_consumed(binding(case="different.case")), str)

    def test_changed_bytes_and_satellite_paths_change_identity(self):
        original = binding()
        changed = binding(digest="c" * 64)
        satellite = binding()
        satellite["inputs"][1]["path"] = satellite["inputs"][1]["path"].replace("de/", "fr/")
        self.assertNotEqual(RUNNER.execution_identity(original), RUNNER.execution_identity(changed))
        self.assertNotEqual(RUNNER.execution_identity(original), RUNNER.execution_identity(satellite))

    def test_missing_or_tampered_consumed_binding_fails_closed(self):
        with tempfile.TemporaryDirectory() as name, patch.object(RUNNER, "ROOT", Path(name)):
            sha = "a" * 64
            (Path(name) / "execution-old.json").write_text(json.dumps({"candidateSha256": sha}))
            with self.assertRaises(FileNotFoundError):
                RUNNER.reject_consumed(binding())
            folder = Path(name) / ("candidate-" + sha[:24])
            folder.mkdir()
            (folder / "binding.json").write_text(json.dumps(binding()))
            with self.assertRaisesRegex(RuntimeError, "damaged"):
                RUNNER.reject_consumed(binding())

    def test_project_full_fixture_requires_explicit_opt_in(self):
        root = ET.parse(RUNNER.REPO / "tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj").getroot()
        groups = list(root.findall("ItemGroup"))
        exclusion = [g for g in groups if g.find("Compile[@Remove='InstallationMaintenanceFullChainTests.cs']") is not None]
        self.assertEqual([g.attrib["Condition"] for g in exclusion], ["'$(KuaiYiMaintenanceFullChain)' != 'true'"])
        for supplier in ("MaterializedPayloadSource", "BootstrapSource"):
            owners = [g for g in groups for item in g.findall("Compile") if item.attrib.get("Include", "").endswith(supplier + ".cs")]
            self.assertEqual([g.attrib["Condition"] for g in owners], ["'$(KuaiYiMaintenanceFullChain)' == 'true'"])


if __name__ == "__main__":
    unittest.main()
