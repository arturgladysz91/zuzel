"""Derive isolated cyclic adapters from sealed controllers, without physical formula edits."""

import argparse
import hashlib
from pathlib import Path
import re

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]


def source(path):
    return (ROOT / path).read_text(encoding="utf-8-sig")


def replace(text, old, new, count=1):
    if text.count(old) != count:
        raise ValueError(f"Expected {count} anchors for {old[:70]}")
    return text.replace(old, new)


def namespace(text, old):
    return replace(text, "namespace " + old + ";", "namespace Cyclic172;")


def derive():
    result = {}
    policy = source("tools/autonomous-fast-corner/Policy.cs")
    policy = namespace(policy, "LearnedCorner166")
    # Share the sealed record definitions through the unchanged linked source.
    begin = policy.index("internal sealed record Weights(")
    end = policy.index("internal sealed class Policy", begin)
    policy = policy[:begin] + policy[end:]
    policy = re.sub(r"\bPolicy\b", "Policy172", policy)
    policy = "using LearnedCorner166;\n" + policy
    policy = replace(policy, "    private readonly bool pilot;", "    private readonly bool pilot;\n    private readonly CyclicReference172 targets;")
    policy = replace(policy, "float mass, float inertia, bool pilot = false)", "float mass, float inertia, CyclicReference172 targets, bool pilot = false)")
    policy = replace(policy, "        this.a = a;", "        this.targets = targets;\n        this.a = a;")
    policy = replace(policy, "    private ProfilePoint Target(double s)", "    internal ProfilePoint Target(double s) => targets.Target(s, LegacyTarget);\n\n    private ProfilePoint LegacyTarget(double s)")
    policy = replace(policy, "if (input.Rider.Speed < 3)", "if (input.Rider.Speed < 3 && input.Rider.TotalReferenceMeters < frame.SegmentStartMeters(1))")
    result["Policy172.Generated.cs"] = policy

    exit_policy = source("tools/two-corner-full-drive/ExitPolicy.cs")
    exit_policy = namespace(exit_policy, "FullDrive170")
    exit_policy = re.sub(r"\bExitPolicy\b", "CyclicExit172", exit_policy)
    exit_policy = replace(exit_policy, "Policy baseline", "Policy172 baseline")
    exit_policy = replace(exit_policy, "float mass, float inertia)", "float mass, float inertia, CyclicReference172 targets)")
    old = '''        bool second = s >= course.Second.Entry;
        var bend = second ? course.Second : course.First;
        var next = second ? frame.LapMeters + course.First.Entry : course.Second.Entry;'''
    new = '''        var visit = targets.Follow(s);
        var bend = new Bend(visit.Entry, visit.Exit);
        var next = visit.NextEntry;'''
    exit_policy = replace(exit_policy, old, new)
    exit_policy = replace(exit_policy, "        if ((second && !tuning.SecondExit) || s < bend.Exit - lead - input.Rider.Speed * .35 ||",
        "        if (s < bend.Exit - lead - input.Rider.Speed * .35 ||")
    exit_policy = replace(exit_policy, 'UseReference(second ? "second-bend-reference" : "first-bend-reference", 0, "reference")',
        'UseReference("cyclic-bend-reference", 0, "reference")')
    start = exit_policy.index("        double scale = entry <= course.Second.Entry")
    end = exit_policy.index("        return speed * speed <=", start)
    exit_policy = exit_policy[:start] + "        double entrySpeed = targets.FlyingEntrySpeed + tuning.SpeedHeadroom;\n" + exit_policy[end:]
    start = exit_policy.index("    private ProfilePoint Reference(")
    end = exit_policy.index("    private double Score(", start)
    exit_policy = exit_policy[:start] + "    private ProfilePoint Reference(double s, Bend bend, double next) => targets.Stability(s);\n\n" + exit_policy[end:]
    exit_policy = replace(exit_policy, "    private double releaseReview;", "    private double releaseReview;\n    private FreeRacingAction lastProposed;\n\n    public void ObserveApplied(FreeRacingAction action)\n    {\n        if (action != lastProposed) plannedRelease = null;\n    }")
    old = '''        Review Result(FreeRacingAction action, string mode, double mix, string gate) =>
            new(input.Time, s, mode, original, action, mix, gate, forecasts, queries,
                predictionTicks * 1000d / Stopwatch.Frequency);'''
    new = '''        Review Result(FreeRacingAction action, string mode, double mix, string gate)
        {
            lastProposed = action;
            return new(input.Time, s, mode, original, action, mix, gate, forecasts, queries,
                predictionTicks * 1000d / Stopwatch.Frequency);
        }'''
    exit_policy = replace(exit_policy, old, new)
    exit_policy = replace(exit_policy, "Course course, RiderSpec", "RiderSpec")
    result["CyclicExit172.Generated.cs"] = exit_policy

    planner = namespace(source("tools/whole-corner-planning/Planner.cs"), "WholeCorner171")
    planner = "using WholeCorner171;\n" + re.sub(r"\bPlanner\b", "Planner172", planner)
    planner = replace(planner, "NominalAt(visit.Index,", "NominalFor(visit,", count=2)
    needle = "    internal Nominal NominalAt(int corner, double s)"
    method = '''    private Nominal NominalFor(BendVisit visit, double relative)
    {
        bool launch = spec.SpeedMetersPerSecond == 0 && visit.Entry == frame.SegmentStartMeters(1);
        return NominalAt(launch ? 0 : 1, relative);
    }

'''
    planner = replace(planner, needle, method + needle)
    planner = replace(planner, "double start = visit.Entry - ApproachMeters;",
        "double start = observed?.TotalReferenceMeters ?? visit.Entry - ApproachMeters;")
    old = '''            var startIndex = observed is { } state ? Math.Clamp((int)Math.Ceiling((state.TotalReferenceMeters - start) / StepMeters), 0, n - 2) : 0;
            double correction = observed is { } observation ? Math.Clamp(observation.Road.OffsetMeters -
                NominalFor(visit, observation.TotalReferenceMeters - visit.Entry).Offset, -2.5, 2.5) : 0;'''
    new = '''            const int startIndex = 0;
            double RawOffset(double at)
            {
                double phase = (at - visit.Entry) / visit.Length;
                return NominalFor(visit, at - visit.Entry).Offset +
                    choice.Entry * Bump(phase, -.24, .55) + choice.Middle * Bump(phase, .14, .79) +
                    choice.Exit * Bump(phase, .64, 1 + ExitMeters / visit.Length);
            }
            double correction = observed is { } observation ? observation.Road.OffsetMeters - RawOffset(start) : 0;
            double referenceSpeed = observed is { } moving ? frame.ReferenceSpeed(moving) : 1;
            double actualSlope = observed is { } actual && referenceSpeed > .1 ?
                Vector2.Dot(actual.Velocity, actual.Road.Outward) / referenceSpeed : 0;
            double slopeError = observed is null ? 0 :
                actualSlope - (RawOffset(start + .25) - RawOffset(start - .25)) / .5;
            double joinLength = observed is { } initial ? Math.Clamp(initial.Speed * 1.5, 18, 38) : 18;'''
    planner = replace(planner, old, new)
    old = '''                double u = (basis[i] - visit.Entry) / visit.Length;
                double fit = correction * (1 - Smooth((i - startIndex) * StepMeters / 20));
                offsets[i] = nominal[i].Offset + fit
                    + choice.Entry * Bump(u, -.24, .55) + choice.Middle * Bump(u, .14, .79)
                    + choice.Exit * Bump(u, .64, 1 + ExitMeters / visit.Length);'''
    new = '''                double blend = Math.Clamp((basis[i] - start) / joinLength, 0, 1);
                double h00 = 1 - 10 * Math.Pow(blend, 3) + 15 * Math.Pow(blend, 4) - 6 * Math.Pow(blend, 5);
                double h10 = blend - 6 * Math.Pow(blend, 3) + 8 * Math.Pow(blend, 4) - 3 * Math.Pow(blend, 5);
                offsets[i] = RawOffset(basis[i]) + correction * h00 + slopeError * joinLength * h10;'''
    planner = replace(planner, old, new)
    planner = replace(planner, "double beta = nominal[i].Beta;", "double blendState = Smooth((basis[i] - start) / joinLength);\n                double beta = observed is { } accepted ? accepted.SlideRadians * (1 - blendState) + nominal[i].Beta * blendState : nominal[i].Beta;")
    planner = replace(planner, "double yaw = nominal[i].Yaw;", "double yaw = observed is { } acceptedYaw ? acceptedYaw.AngularVelocity * (1 - blendState) + nominal[i].Yaw * blendState : nominal[i].Yaw;")
    planner = replace(planner, "private const double ContactFraction = .86;", "private const double ContactFraction = 1;")
    result["Planner172.Generated.cs"] = planner

    tracker = namespace(source("tools/whole-corner-planning/Tracker.cs"), "WholeCorner171")
    tracker = replace(tracker, "using FullDrive170;", "using WholeCorner171;")
    for old,new in [("Tracker","Tracker172"),("Planner","Planner172"),("ExitPolicy","CyclicExit172"),("Tracking","Tracking172"),("Planning","Planning172")]:
        tracker = re.sub(r"\b"+old+r"\b",new,tracker)
    tracker = replace(tracker, "Study.Cpu", "Study172.Cpu", count=2)
    tracker = replace(tracker, "bool displaced = previousPoint is { } point &&", "bool displaced = chosen is null || previousPoint is { } point &&")
    tracker = replace(tracker, "displaced && replans < 1", "displaced && replans < 2")
    result["Tracker172.Generated.cs"] = tracker

    world = source("tools/autonomous-fast-corner/NativeWorld166.Generated.cs")
    world = namespace(world, "LearnedCorner166")
    world = re.sub(r"\bNativeWorld166\b", "NativeWorld172", world)
    world = replace(world, "&&finish.Any(q=>q is null)&&", "&&(finish.Any(q=>q is null)||postSeconds>0)&&")
    world = replace(world, "if(finish[i] is null&&time>=nextReview[i]-1e-10)", "if((finish[i] is null||postSeconds>0)&&time>=nextReview[i]-1e-10)")
    world = replace(world, "if(finish[i] is null&&nextReview[i]>time+1e-10)", "if((finish[i] is null||postSeconds>0)&&nextReview[i]>time+1e-10)")
    world = replace(world, "views[i]=views[i] with{Activity=RiderActivity.Finished};", "if(postSeconds<=0)views[i]=views[i] with{Activity=RiderActivity.Finished};")
    world = replace(world, "string,Decision144>? chooser=null){", "string,Decision144>? chooser=null,Action<double,double>? observeCpu=null){")
    world = replace(world, "                Sample(i,power,forces[i],legacy,certificate,excess);",
        "                Sample(i,power,forces[i],legacy,certificate,excess);\n                observeCpu?.Invoke(time,(Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds);")
    result["NativeWorld172.Generated.cs"] = world
    return result


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check",action="store_true")
    args=parser.parse_args()
    for name,content in derive().items():
        content="// Generated by generate172.py from sealed sources. Research only.\n#nullable enable\n"+content
        data=content.replace("\r\n","\n").encode("utf-8")
        path=HERE/name
        if args.check:
            if path.read_bytes()!=data:
                raise ValueError("Generated adapter mismatch: "+name)
        else:
            path.write_bytes(data)
    print("Cyclic adapters verified." if args.check else "Cyclic adapters generated.")


if __name__=="__main__":
    main()

