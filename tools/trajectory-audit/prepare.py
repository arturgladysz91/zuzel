"""Diagnostic only: install the identical workload alongside an audited checkout."""
from pathlib import Path
import sys

root = Path(sys.argv[1])
source = Path(sys.argv[2]).read_text(encoding="utf-8")
source = source.replace("TrajectoryBehaviorFingerprint", "CaptureProbe")
source = source.replace("    public static byte[] Capture()", "    public static object? FinalHeat { get; private set; }\n    public static object? Decisions { get; private set; }\n\n    public static byte[] Capture()")
old = '''        new HeatSimulator(new CaptureDecisions(decisions)).SimulateHeat(track, TrackState.CreateDefault(track),
            Enumerable.Range(1, 4).Select(id => new RiderState(RiderProfile.CreateDefault(id),
                (StartingGate)(id - 1), track)).ToList(),
            new HeatSimulationOptions { Laps = 4, IncidentFrequency = 0, EnableLogging = false });'''
new = '''        var surface = TrackState.CreateDefault(track);
        var riders = Enumerable.Range(1, 4).Select(id => new RiderState(RiderProfile.CreateDefault(id),
            (StartingGate)(id - 1), track)).ToList();
        var heat = new HeatSimulator(new CaptureDecisions(decisions)).SimulateHeat(track, surface, riders,
            new HeatSimulationOptions { Laps = 4, IncidentFrequency = 0, EnableLogging = false });
        Decisions = decisions;
        FinalHeat = new { heat.Classification, Riders = riders.Select(r => new {
            r.RiderId, r.Position, r.Lane, r.LateralPosition, r.Speed, r.Risk, r.Status,
            r.ElapsedTimeSeconds, r.DistanceMeters, r.Morale, r.LastResolvedSegmentId }),
            Surface = Enumerable.Range(0, surface.SegmentCount).Select(segment =>
                Enumerable.Range(0, surface.LinesCount).Select(lane => surface.GetSurface(segment, lane)).ToArray()).ToArray() };'''
assert old in source
(root / "tools/trajectory-audit/CaptureProbe.cs").write_text(source.replace(old, new), encoding="utf-8", newline="\n")
