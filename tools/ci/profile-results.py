"""Verify executed cases and publish TRX duration observations, including top 20."""
from collections import Counter
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

directory=Path(sys.argv[1]); shard=sys.argv[2] if len(sys.argv)>2 else None
ns={"t":"http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
rows=[]
for file in directory.rglob("*.trx"):
    root=ET.parse(file).getroot()
    definitions={n.attrib["id"]:n.find("t:TestMethod",ns).attrib for n in root.findall(".//t:UnitTest",ns)}
    for node in root.findall(".//t:UnitTestResult",ns):
        h,m,s=node.attrib.get("duration","0:0:0").split(":")
        method=definitions[node.attrib["testId"]]
        rows.append({"Test":node.attrib["testName"],"Class":method["className"].split(",")[0],
            "Seconds":int(h)*3600+int(m)*60+float(s),"Outcome":node.attrib["outcome"]})
assert rows, "No TRX results"
classes=Counter()
for row in rows: classes[row["Class"]]+=row["Seconds"]
total=sum(classes.values())
trajectory=sum(v for k,v in classes.items() if any(word in k for word in ("Trajectory","FourRider","Adaptive","Projection")))
report={"Executed":len(rows),"Outcomes":dict(Counter(r["Outcome"] for r in rows)),
    "SummedTestSeconds":total,"TrajectoryPercent":100*trajectory/total if total else 0,
    "Top20Methods":sorted(rows,key=lambda row:row["Seconds"],reverse=True)[:20],
    "CumulativeSecondsPerClass":dict(classes.most_common())}
if shard:
    coverage=json.loads((directory/"discovery/coverage.json").read_text())
    expected=set(coverage["Shards"][shard]); actual=Counter(r["Test"] for r in rows)
    report.update(Missing=sorted(expected-actual.keys()),Extra=sorted(actual.keys()-expected),
        Duplicates=sorted(name for name,count in actual.items() if count!=1))
(directory/"profile.json").write_text(json.dumps(report,indent=2)+"\n")
print(json.dumps(report,indent=2))
assert all(row["Outcome"]=="Passed" for row in rows), "Failed/skipped case"
if shard: assert not report["Missing"] and not report["Extra"] and not report["Duplicates"]
