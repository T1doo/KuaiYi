"""Offline checks of the new CI admission and exact closure collision refusal; no Windows/product claims."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("warm_ci", Path(__file__).with_name("verify-warm-components-windows.py"))
ci = importlib.util.module_from_spec(spec);spec.loader.exec_module(ci)


class Admission(unittest.TestCase):
    def env(self):
        return dict(GITHUB_ACTIONS="true", GITHUB_REPOSITORY="T1doo/KuaiYi", GITHUB_REF=ci.BRANCH,
                    GITHUB_EVENT_NAME="push", GITHUB_RUN_ATTEMPT="1", RUNNER_OS="Windows", GITHUB_SHA="a" * 40)

    def test_only_exact_public_first_attempt(self):
        ci.hosted_context(self.env(), "win32", {"repository": {"private": False}})
        for key, value in (("GITHUB_RUN_ATTEMPT", "2"), ("GITHUB_REF", "refs/heads/main"), ("GITHUB_EVENT_NAME", "workflow_dispatch"), ("GITHUB_REPOSITORY", "elsewhere/KuaiYi")):
            with self.subTest(key=key), self.assertRaises(RuntimeError):
                ci.hosted_context(dict(self.env(), **{key: value}), "win32", {"repository": {"private": False}})
        for private in (True, None, "false"):
            with self.subTest(private=private), self.assertRaises(RuntimeError):
                ci.hosted_context(self.env(), "win32", {"repository": {"private": private}})

    def test_scope_and_fixed_product_fail_closed_before_tools(self):
        def run_for(override):
            rows={"source-head":"a" * 40, "source-parent":"a" * 40 + " " + ci.PARENT,
                  "source-scope":"\n".join(sorted(ci.FILES)), "source-clean":"", "fixed-product-source":""}
            rows.update(override)
            return lambda argv, name, timeout: rows[name]
        self.assertEqual("a" * 40, ci.source_gate(run_for({}), "a" * 40))
        for override in ({"source-parent":"wrong"}, {"source-clean":"?? surprise"}, {"fixed-product-source":"src/KuaiYi.App/Program.cs"}, {"source-scope":"\n".join(sorted(ci.FILES)) + "\nextra"}):
            with self.subTest(override=override), self.assertRaises(RuntimeError):
                ci.source_gate(run_for(override), "a" * 40)

    def test_different_existing_payload_is_retained(self):
        import hashlib
        from types import SimpleNamespace
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);app=root/"app";worker=root/"worker";target=root/"target"
            for p in (app,worker,target):p.mkdir()
            (app/"KuaiYi.App.exe").write_bytes(b"public-one");(app/"KuaiYi.App.runtimeconfig.json").write_bytes(b"{}")
            (worker/"public.dll").write_bytes(b"fixture");(target/"KuaiYi.App.exe").write_bytes(b"retain-original")
            helper=SimpleNamespace(linked=lambda p:p.is_symlink(), file_hash=lambda p:hashlib.sha256(p.read_bytes()).hexdigest())
            with self.assertRaisesRegex(RuntimeError,"collision"):
                ci.deploy(app,worker,target,helper)
            self.assertEqual(b"retain-original",(target/"KuaiYi.App.exe").read_bytes())


if __name__ == "__main__":
    unittest.main()
