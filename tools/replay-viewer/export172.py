"""Project frozen #172 observations into the existing viewer; never run a world."""
import argparse
import gzip
import hashlib
import io
import json
from pathlib import Path
import zipfile

BASE = "c5f0a4cc4de34fc4c8c47ef912cafabf34055f29"
FOLDER = "docs/experiments/three-lap-global-planning"
ARCHIVE_SHA256 = "30169597f5c8ca723460c1d69a2f7e757cc73a122181689a5147d4518d7ae45c"
PROTOCOL_SHA256 = "217d1470180b6d9c7d0f194f49f31c1ebfbb8c66b13ebae234699d96a7f71b50"


def camel(value):
    if isinstance(value, dict):
        return {key[:1].lower() + key[1:]: camel(item) for key, item in value.items()}
    if isinstance(value, list):
        return [camel(item) for item in value]
    return value


def export(root):
    archive = root / FOLDER / "172-validation-K4.zip"
    protocol = root / FOLDER / "172-protocol.zip"
    assert hashlib.sha256(archive.read_bytes()).hexdigest() == ARCHIVE_SHA256, "Changed #172 archive"
    assert hashlib.sha256(protocol.read_bytes()).hexdigest() == PROTOCOL_SHA256, "Changed #172 protocol"
    with zipfile.ZipFile(archive) as outer:
        nested = outer.read("v1-K4-S1.zip")
    with zipfile.ZipFile(io.BytesIO(nested)) as inner:
        raw_native = inner.read("native.json")
        native = json.loads(raw_native)
        result = json.loads(inner.read("result.json"))
    with zipfile.ZipFile(protocol) as source:
        raw_geometry = source.read("geometry.json")
    rider, = native["Riders"]
    crossings = native["Crossings"]
    assert result["S3"] and result["AcceptedBends"] == 6
    assert len(crossings) == 3 and [c["Lap"] for c in crossings] == [1, 2, 3]
    # Include the actual first sample after the third crossing, never synthesize a finish pose.
    end = crossings[-1]["AfterTime"]
    samples = [s for s in rider["Samples"] if s["Input"]["Time"] <= end]
    assert samples[-1]["Input"]["Time"] == end
    rows = []
    for sample in samples:
        state, bike = sample["Input"], sample["Input"]["Rider"]
        rows.append(dict(t=state["Time"], id=bike["Id"], x=bike["Position"]["X"], y=bike["Position"]["Y"],
                         vx=bike["Velocity"]["X"], vy=bike["Velocity"]["Y"], speed=bike["Speed"],
                         heading=bike["Heading"], reference=bike["TotalReferenceMeters"],
                         offset=bike["Road"]["OffsetMeters"], target=bike["TargetOffsetMeters"],
                         drive=state["Active"]["Drive"], activity=bike["Activity"], steering=state["Steering"],
                         source="native #172 / v1-K4-S1"))
    first = samples[0]["Input"]["Rider"]
    initial = {"Id": first["Id"], "Position": first["Position"], "Velocity": first["Velocity"],
               "Heading": first["Heading"], "Speed": first["Speed"], "TotalReferenceMeters": first["TotalReferenceMeters"],
               "Road": {"OffsetMeters": first["Road"]["OffsetMeters"]}, "TargetOffsetMeters": first["TargetOffsetMeters"],
               "Drive": first["Drive"], "Activity": first["Activity"]}
    document = {"Run": {"Initial": {"Riders": [initial]}, "RequiredLaps": 3, "ContinueAfterFinish": True,
                         "Result": {"TimeSeconds": end, "Decisions": [], "Contacts": native["Contacts"],
                                    "FinishTimes": {str(first["Id"]): rider["FinishSeconds"]}},
                         "Riders": [{"Measurement": {"RiderId": first["Id"],
                                     "CrossingSeconds": [c["Time"] for c in crossings]}}],
                         "Passes": [], "Consequences": []}}
    # Commands are intentionally omitted: samples retain actual active drive/steering.
    # Original detailed decisions remain in the sealed archive, not reconstructed in JS.
    provenance = {"base": BASE, "run": result["Id"], "fingerprint": native["Fingerprint"],
                  "archive": FOLDER + "/172-validation-K4.zip", "member": "v1-K4-S1.zip/native.json",
                  "archiveSha256": hashlib.sha256(archive.read_bytes()).hexdigest(),
                  "nativeSha256": hashlib.sha256(raw_native).hexdigest(),
                  "protocolSha256": hashlib.sha256(protocol.read_bytes()).hexdigest(),
                  "geometrySha256": hashlib.sha256(raw_geometry).hexdigest(),
                  "samples": len(rows), "crossings": crossings, "finishSeconds": rider["FinishSeconds"],
                  "lastSampleSeconds": end, "reserveOmitted": True, "newNativeWorlds": 0}
    return {"geometry": camel(json.loads(raw_geometry)), "document": document, "rows": rows, "provenance": provenance}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    payload = export(args.source_root)
    raw = json.dumps(payload, ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode()
    compressed = gzip.compress(raw, compresslevel=9, mtime=0)
    target = Path(__file__).with_name("recordings") / "172-s3-k4.json.gz"
    if args.check:
        assert gzip.decompress(target.read_bytes()) == raw, "Export differs from frozen observations"
    else:
        target.parent.mkdir(exist_ok=True)
        target.write_bytes(compressed)
    print(f"#172: {len(payload['rows'])} exact native poses; three crossings; zero new worlds.")


if __name__ == "__main__":
    main()
