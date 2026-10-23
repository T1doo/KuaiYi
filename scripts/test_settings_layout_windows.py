"""Offline scope/integrity rejections. These are not Windows raster tests."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location("settings_ci", Path(__file__).with_name("verify-settings-layout-windows.py"))
M = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(M)


class SettingsCiContracts(unittest.TestCase):
    def fixture(self, root):
        project = "tools/KuaiYi.GrowthHarness/KuaiYi.GrowthHarness.csproj"
        paths = [root / project, *(root / "src" / name / (name + ".csproj") for name in M.PROJECTS)]
        for path in paths:
            path.parent.joinpath("obj").mkdir(parents=True)
            assets = {"project": {"restore": {"projectUniqueName": str(path)}, "frameworks": {"net10.0": {}}},
                      "libraries": {}, "targets": {"net10.0": {}}}
            if path == root / project:
                assets["libraries"] = {name + "/1.0.0": {"type": "project"} for name in M.PROJECTS}
                assets["targets"]["net10.0"] = dict(assets["libraries"])
            path.parent.joinpath("obj/project.assets.json").write_text(json.dumps(assets))
            path.parent.joinpath("packages.lock.json").write_text(json.dumps({"dependencies": {"net10.0": {}}}))
        return project

    def check_mutation(self, mutate, target="obj/project.assets.json"):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            project = self.fixture(root)
            path = (root / project).parent / target
            data = json.loads(path.read_text())
            mutate(data)
            path.write_text(json.dumps(data))
            with self.assertRaises(RuntimeError): M.zero_package_closure(root, project)

    def test_project_only_closure_and_exact_count(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.assertEqual(9, len(M.zero_package_closure(root, self.fixture(root))))

    def test_package_library_rejected(self):
        self.check_mutation(lambda d: d["libraries"].update({"Bad/1": {"type": "package"}}))

    def test_direct_package_request_rejected(self):
        self.check_mutation(lambda d: d["project"]["frameworks"]["net10.0"].update(dependencies={"Bad": {}}))

    def test_lock_package_rejected(self):
        self.check_mutation(lambda d: d["dependencies"]["net10.0"].update({"bad": {"type": "Direct"}}), "packages.lock.json")

    def test_foreign_project_identity_rejected(self):
        self.check_mutation(lambda d: d["project"]["restore"].update(projectUniqueName="foreign.csproj"))

    def test_unknown_project_library_rejected(self):
        def mutation(d):
            d["libraries"]["Unknown/1"] = {"type": "project"}
            d["targets"]["net10.0"]["Unknown/1"] = {"type": "project"}
        self.check_mutation(mutation)

    def test_target_inventory_mismatch_rejected(self):
        self.check_mutation(lambda d: d["targets"]["net10.0"].update({"KuaiYi.Core/1": {"type": "project"}}))

    def test_missing_project_closure_rejected(self):
        def mutation(d):
            d["libraries"].clear()
            for target in d["targets"].values(): target.clear()
        self.check_mutation(mutation)

    def test_hosted_context_no_rerun_or_private(self):
        env = {"GITHUB_ACTIONS": "true", "GITHUB_REPOSITORY": "T1doo/KuaiYi", "GITHUB_REF": M.BRANCH,
               "GITHUB_EVENT_NAME": "push", "GITHUB_RUN_ATTEMPT": "1", "RUNNER_OS": "Windows", "GITHUB_SHA": "a" * 40}
        M.hosted_context(env, "win32", {"repository": {"private": False}})
        for changed, platform, event in [(dict(env, GITHUB_RUN_ATTEMPT="2"), "win32", {"repository": {"private": False}}),
                                         (env, "win32", {"repository": {"private": True}}),
                                         (env, "linux", {"repository": {"private": False}})]:
            with self.assertRaises(RuntimeError): M.hosted_context(changed, platform, event)

    def test_exact_source_scope_and_clean_tree(self):
        values = {"source-head": "a" * 40, "source-parent": "a" * 40 + " " + M.PARENT,
                  "source-scope": "\n".join(M.FILES), "source-clean": "", "fixed-product-source": ""}
        run = lambda argv, name, timeout: values[name]
        self.assertEqual("a" * 40, M.source_gate(run, "a" * 40))
        for name, replacement in [("source-parent", "a" * 40 + " " + "b" * 40), ("source-scope", "wrong"),
                                  ("source-clean", " M unexpected"), ("fixed-product-source", "src/changed.cs")]:
            old = values[name]; values[name] = replacement
            with self.assertRaises(RuntimeError): M.source_gate(run, "a" * 40)
            values[name] = old


if __name__ == "__main__": unittest.main()
