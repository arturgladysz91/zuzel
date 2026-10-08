"""Compare complete exact typed captures from original and optimized engines on each OS."""
import json
from pathlib import Path
import sys

def compare(directory):
    root=Path(directory)
    before=json.loads((root/"before.json").read_text())
    after=json.loads((root/"after.json").read_text())
    assert before.keys()==after.keys(), "Capture case coverage changed"
    for case in before:
        assert before[case]==after[case], f"Exact original-main behavior changed: {case}"
        if case.endswith("/False"):
            reversed_case=case.removesuffix("/False")+"/True"
            assert after[case]==after[reversed_case], f"Rider-order dependence: {case}"
    return {"Cases":len(after),"Leaves":sum(row["Leaves"] for row in after.values()),
            "IEEELeaves":sum(row["IEEELeaves"] for row in after.values()),"OriginalMainExact":True,"RiderOrderExact":True}

if __name__=="__main__":
    if len(sys.argv)==2:
        print(json.dumps(compare(sys.argv[1]),indent=2))
    else:
        windows=Path(sys.argv[1])/"determinism-windows-latest/contested-performance"
        ubuntu=Path(sys.argv[1])/"determinism-ubuntu-latest/contested-performance"
        evidence={"Windows":compare(windows),"Ubuntu":compare(ubuntu)}
        w=json.loads((windows/"after.json").read_text())
        u=json.loads((ubuntu/"after.json").read_text())
        different=[case for case in w if w[case]!=u[case]]
        if different:
            wb=json.loads((windows/"before.json").read_text())
            ub=json.loads((ubuntu/"before.json").read_text())
            print(json.dumps({"CrossPlatformDifferentCases":different,
                "SameDivergenceOnOriginalMain":all(wb[case]==w[case] and ub[case]==u[case] for case in different)},indent=2))
            for case in different:
                filename="after.json."+case.replace("/","_")+".json"
                if (windows/filename).exists() and (ubuntu/filename).exists():
                    left=json.loads((windows/filename).read_text())
                    right=json.loads((ubuntu/filename).read_text())
                    differences=[{"Path":key,"Windows":left[key],"Ubuntu":right[key]} for key in left if left[key]!=right[key]]
                    print(json.dumps({"Case":case,"DifferentLeaves":len(differences),"FirstDifferences":differences[:20]},indent=2))
        assert (windows/"after.json").read_bytes()==(ubuntu/"after.json").read_bytes(), "Cross-platform exact behavior/IEEE divergence"
        evidence["CrossPlatformExact"]=True
        print(json.dumps(evidence,indent=2))
