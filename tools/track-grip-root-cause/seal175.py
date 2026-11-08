"""Create the input and artifact manifests for the completed #175 audit."""
import json

from verify175 import DOC, ROOT, input_bytes, sha

SOURCE_PATHS = [
    'tools/shared-world-speedway/ExperimentalTrackShape.cs',
    'tools/shared-world-speedway/TrackFrame.cs',
    'src/CoreSim/Track/TrackGeometry.cs',
    'src/CoreSim/PhysicalSpace/TrackMetricEmbedding.cs',
    'src/CoreSim/Analysis/MatchedVenueProfile.cs',
    'tools/shared-world-speedway/TrackGeometryAudit.cs',
    'tools/shared-world-speedway/CurveBoundaryAudit.cs',
    'tools/shared-world-speedway/RacingLineGeometry.cs',
    'src/CoreSim/Track/TrackSurfaceState.cs',
    'src/CoreSim/Track/TrackState.cs',
    'tools/shared-world-speedway/TrackAwarenessExperiment134.cs',
    'tools/shared-world-speedway/FreeLearningExperiment.cs',
    'tools/shared-world-speedway/MultiActionExperiment135.cs',
    'tools/shared-world-speedway/LoadBasedTyreGrip.cs',
    'tools/shared-world-speedway/RearWheelDynamics.cs',
    'tools/shared-world-speedway/SlipCoupledDynamics.cs',
    'tools/shared-world-speedway/TwoContactBikeDynamics.cs',
    'tools/shared-world-speedway/StandingStart144/Contact144.cs',
    'tools/shared-world-speedway/DynamicAxleLoad.cs',
    'tools/shared-world-speedway/Engine500.cs',
    'tools/shared-world-speedway/StandingStart144/CrankClutch144.cs',
    'src/CoreSim/Track/LongitudinalDynamics.cs',
    'tools/whole-corner-planning/BendMap.cs',
    'tools/three-lap-global-planning/Planner172.Generated.cs',
    'tools/three-lap-global-planning/CyclicController172.cs',
    'tools/three-lap-global-planning/NativeWorld172.Generated.cs',
    'tools/safe-fast-contingency/PredictorA153.Generated.cs',
    'tools/safe-fast-contingency/Clearance153.cs',
    'tools/full-drive-stability/Contact169.cs',
]
ARCHIVE_PATHS = [
    'geometry-speed-audit/geometry.json.gz',
    'geometry-speed-audit/REPORT.md',
    'throttle-contact-audit/168-dowody.zip',
    'full-drive-stability/169-dowody.zip',
    'three-lap-global-planning/172-development.zip',
    'three-lap-global-planning/172-validation-C0.zip',
    'three-lap-global-planning/172-validation-K4.zip',
    'three-lap-global-planning/172-protocol.zip',
    'throttle-decision-audit/173-native.zip',
    'throttle-decision-audit/summary173.json',
    'state-feasible-throttle/174-native.zip',
    'state-feasible-throttle/summary174.json',
]


def main():
    inputs = []
    for name in sorted(SOURCE_PATHS + ['docs/experiments/' + n for n in ARCHIVE_PATHS]):
        path = ROOT / name
        normalized = path.suffix in ('.cs','.md','.json')
        inputs.append(dict(path=name, normalize_crlf=normalized, sha256=sha(input_bytes(path,normalized))))
    payload = dict(base='cf585987374e0673e3f0cf89df636d20bf524af5',
                   reference='c5f0a4cc4de34fc4c8c47ef912cafabf34055f29', files=inputs)
    (DOC / 'INPUTS.json').write_text(json.dumps(payload,indent=2)+'\n',encoding='utf-8',newline='\n')
    paths = [p for p in DOC.iterdir() if p.is_file() and p.name != 'SHA256SUMS']
    paths += [p for p in (ROOT/'tools/track-grip-root-cause').iterdir() if p.is_file()]
    paths += [ROOT/'tests/calibration/test_track_grip_root_cause175.py', ROOT/'.github/workflows/study-175.yml']
    lines = [sha(p.read_bytes())+'  '+p.relative_to(ROOT).as_posix() for p in sorted(paths)]
    (DOC / 'SHA256SUMS').write_text('\n'.join(lines)+'\n',encoding='utf-8',newline='\n')
    print(f'{len(inputs)} frozen inputs, {len(paths)} sealed artifacts.')


if __name__ == '__main__':
    main()
