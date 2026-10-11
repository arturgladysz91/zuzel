"""Select CI work; source changes cannot opt out of pre-merge historical audits."""
import json
import os
from pathlib import Path
import subprocess


def select(event_name, event, paths):
    pr = event.get("pull_request")
    requested = any(label["name"] == "ci:historical-audit" for label in (pr or {}).get("labels", []))
    full = not pr or not pr["draft"] or requested
    risky = any(path.startswith(("src/", "tests/", "data/", "docs/calibration/"))
                or (path.startswith("tools/") and not path.startswith("tools/ci/"))
                or path.endswith((".csproj", ".sln", ".props", ".targets"))
                or path.lower() in {"global.json", "nuget.config"} for path in paths)
    historical = (event_name == "schedule" or requested
                  or bool(event.get("inputs", {}).get("historical_audits", False) in (True, "true"))
                  or bool(pr and full and risky))
    return {"full": full, "historical": historical, "risky": risky}


if __name__ == "__main__":
    event = json.loads(Path(os.environ["GITHUB_EVENT_PATH"]).read_text(encoding="utf-8"))
    pr = event.get("pull_request")
    paths = subprocess.check_output(
        ["git", "diff", "--name-only", "-z", pr["base"]["sha"] + "...HEAD"],
        text=True).split("\0") if pr else []
    decision = select(os.environ["GITHUB_EVENT_NAME"], event, paths)
    print(json.dumps(decision, indent=2))
    with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as output:
        for key, value in decision.items():
            output.write(f"{key}={str(value).lower()}\n")
        shards = ["core", "trajectory"]
        if decision["full"]:
            shards += ["historical-analysis", "four-rider"]
        output.write("shards=" + json.dumps(shards) + "\n")
