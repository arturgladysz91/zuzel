"""Exercise status transitions and fail-closed audit routing without GitHub access."""
import unittest
from policy import select


class PolicyTests(unittest.TestCase):
    def pr(self, draft, paths=(), labels=()):
        return select("pull_request", {"pull_request": {"draft": draft,
                      "labels": [{"name": label} for label in labels]}}, paths)

    def test_every_draft_update_keeps_critical_shards_and_defers_audits(self):
        self.assertEqual(self.pr(True, ["src/CoreSim/SimulationEngine.cs"]),
                         {"full": False, "historical": False, "risky": True})

    def test_ready_and_reopened_ready_require_full(self):
        self.assertTrue(self.pr(False)["full"])
        self.assertTrue(self.pr(False, ["src/CoreSim/SimulationEngine.cs"])["historical"])

    def test_converted_to_draft_cannot_emit_full_success(self):
        self.assertFalse(self.pr(True)["full"])

    def test_risky_paths_include_deletions_and_build_or_evidence_changes(self):
        for path in ("src/CoreSim/Old.cs", "tests/fixtures/baseline.json", "data/a.json",
                     "docs/calibration/a.json", "tools/contact-decision-audit/compare.py",
                     "Directory.Build.props", "new/test.csproj", "global.json", "NuGet.Config"):
            with self.subTest(path=path):
                self.assertTrue(self.pr(False, [path])["historical"])

    def test_ci_and_general_docs_only_use_normal_full_validation(self):
        self.assertEqual(self.pr(False, [".github/workflows/ci.yml", "tools/ci/policy.py", "docs/ci.md"]),
                         {"full": True, "historical": False, "risky": False})

    def test_explicit_audit_is_available_on_a_draft(self):
        result = self.pr(True, labels=["ci:historical-audit"])
        self.assertTrue(result["full"] and result["historical"])

    def test_main_always_full_and_weekly_or_manual_runs_can_audit(self):
        self.assertEqual(select("push", {}, []), {"full": True, "historical": False, "risky": False})
        self.assertTrue(select("schedule", {}, [])["historical"])
        for value in (True, "true"):
            self.assertTrue(select("workflow_dispatch", {"inputs": {"historical_audits": value}}, [])["historical"])
        self.assertFalse(select("workflow_dispatch", {"inputs": {"historical_audits": "false"}}, [])["historical"])


if __name__ == "__main__":
    unittest.main()
