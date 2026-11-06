"""Add observation-only hooks to exact #172 sources; never edit historical files."""
import argparse
from pathlib import Path

HERE = Path(__file__).resolve().parent
SOURCE = HERE.parent / 'three-lap-global-planning'


def replace(s, a, b, count=1):
    if s.count(a) != count:
        raise ValueError(f'Expected {count} exact anchors: {a[:80]}')
    return s.replace(a, b)


def derive():
    files = {}
    s = (SOURCE/'Policy172.Generated.cs').read_text(encoding='utf-8-sig')
    s = replace(s, '            return new(0, 1);', '        { Trace173.Event("launch", new { input.Time }); return new(0, 1); }')
    s = replace(s, '        ForecastContactQueries += forecast.ForceEvaluations;', '        Trace173.Prediction("policy", input, .24, null, forecast);\n        ForecastContactQueries += forecast.ForceEvaluations;')
    s = replace(s, '        double available = TwoContactBikeParameters.Default.MaximumSteeringRadians;', '''        double available = TwoContactBikeParameters.Default.MaximumSteeringRadians;
        Trace173.Event("policy", new { input.Time, Target = target, Predicted = Trace173.State(end),
            CourseError = courseError, HeadingError = headingError, OffsetError = offsetError,
            YawError = yawError, WheelSlip = wheelSlip, ProfileDrive = target.Drive,
            SpeedCorrection = w.DriveFeedback * (target.Speed - bike.Speed),
            SlipPenalty = w.WheelSlipFeedback * Math.Max(0, wheelSlip - .25) * Math.Min(1, Math.Abs(courseError) * 5),
            RawDrive = gas, Drive = (float)Math.Clamp(gas, 0, 1),
            Steering = (float)Math.Clamp(target.Steer + correction, -available, available) });''')
    files['Policy173.Generated.cs'] = s

    s = (SOURCE/'CyclicExit172.Generated.cs').read_text(encoding='utf-8-sig')
    s = replace(s, '        var original = Baseline(input, crank);', '        var original = Baseline(input, crank);\n        Trace173.Event("exit-original", new { input.Time, original });')
    s = replace(s, '        float gas = Mix(original.Drive, 1, blend);', '        float gas = Mix(original.Drive, 1, blend);\n        Trace173.Event("exit-boost", new { input.Time, at, next, blend, gas, original });')
    s = replace(s, '                if (!Legal(points)) return;', '''                if (!Legal(points)) { Trace173.Event("exit-candidate", new { action, Legal = false }); return; }''')
    s = replace(s, '                var score = Score(points, bend, next, steer - previous.SteeringRadians);', '''                var score = Score(points, bend, next, steer - previous.SteeringRadians);
                Trace173.Event("exit-candidate", new { action, Legal = true, score });''')
    s = replace(s, '            if (Legal(tail) && CanPrepare(tail[^1].Rider, next, checkPulse: false) && Settled(tail, bend, next))', '''            bool legal = Legal(tail);
            bool prepare = legal && CanPrepare(tail[^1].Rider, next, checkPulse: false);
            bool settled = prepare && Settled(tail, bend, next);
            Trace173.Event("continuation-gate", new { followUp, legal, PrepareEvaluated = legal,
                prepare, SettledEvaluated = prepare, settled });
            if (legal && prepare && settled)''')
    s = replace(s, '            lastProposed = action;', '            Trace173.Event("exit-result", new { input.Time, original, action, mode, mix, gate, fallback });\n            lastProposed = action;')
    s = replace(s, '\n        predictionTicks += Stopwatch.GetTimestamp() - start;', '\n        Trace173.Prediction("exit", input, horizon, candidate, result);\n        predictionTicks += Stopwatch.GetTimestamp() - start;')
    s = replace(s, '        return speed * speed <= entrySpeed * entrySpeed + 2 * CoastMetersPerSecondSquared * remaining;', '''        bool accepted = speed * speed <= entrySpeed * entrySpeed + 2 * CoastMetersPerSecondSquared * remaining;
        Trace173.Event("can-prepare", new { checkPulse, Entry = entry, Reference = bike.TotalReferenceMeters,
            ActualSpeed = bike.Speed, PulseSpeed = speed, Remaining = remaining, EntrySpeed = entrySpeed,
            Deceleration = CoastMetersPerSecondSquared,
            AllowedSpeed = Math.Sqrt(entrySpeed * entrySpeed + 2 * CoastMetersPerSecondSquared * remaining),
            RequiredDeceleration = remaining > 0 ? Math.Max(0, (speed * speed - entrySpeed * entrySpeed) / (2 * remaining)) : (double?)null,
            Accepted = accepted });
        return accepted;''')
    s = replace(s, '''        return Math.Abs(beta) + .2 * Math.Max(0, rate * Math.Sign(beta)) < .17 &&
            Math.Abs(bike.AngularVelocity - target.Yaw) < .48 &&
            Math.Abs(BikeAngles.Wrap(Teacher.CourseError(bike) - target.Course)) < .21;''', '''        double betaGate = Math.Abs(beta) + .2 * Math.Max(0, rate * Math.Sign(beta));
        double yawGate = Math.Abs(bike.AngularVelocity - target.Yaw);
        double courseGate = Math.Abs(BikeAngles.Wrap(Teacher.CourseError(bike) - target.Course));
        Trace173.Event("settled", new { beta, rate, betaGate, yawGate, courseGate });
        return betaGate < .17 && yawGate < .48 && courseGate < .21;''')
    files['Exit173.Generated.cs'] = s

    s = (SOURCE/'Tracker172.Generated.cs').read_text(encoding='utf-8-sig')
    s = replace(s, '        var baseline = reference.Choose(input, crank);', '        var baseline = reference.Choose(input, crank);\n        Trace173.Event("exit-review", baseline);')
    s = replace(s, '        double targetSpeed = expected.Speed + speedDelta;', '''        double targetSpeed = expected.Speed + speedDelta;
        Trace173.Event("tracker-target", new { targetSpeed, targetOffset, targetCourse, targetYaw, targetBeta,
            speedDelta, ramp, ShapeTarget = target, Nominal = baseTarget });''')
    s = replace(s, '''            if (!Safe(points) || Math.Abs(BikeAngles.Wrap(terminal.SlideRadians - target.Beta)) > .26 ||
                Math.Abs(terminal.AngularVelocity - targetYaw) > .73)''', '''            bool safe = Safe(points);
            double betaError = Math.Abs(BikeAngles.Wrap(terminal.SlideRadians - target.Beta));
            double yawError = Math.Abs(terminal.AngularVelocity - targetYaw);
            Trace173.Event("tracker-candidate", new { action, safe, betaError, yawError });
            if (!safe || betaError > .26 || yawError > .73)''')
    s = replace(s, '            double score = Score(terminal, action);', '            double score = Score(terminal, action);\n            Trace173.Event("tracker-score", new { action, score, PreviousBest = double.IsFinite(best) ? best : (double?)null });')
    s = replace(s, '            predictionMs += Stopwatch.GetElapsedTime(tick).TotalMilliseconds;', '            Trace173.Prediction("tracker", input, seconds, action, result);\n            predictionMs += Stopwatch.GetElapsedTime(tick).TotalMilliseconds;')
    files['Tracker173.Generated.cs'] = s

    s = (SOURCE/'CyclicController172.cs').read_text(encoding='utf-8-sig')
    s = replace(s, '    public IReadOnlyList<Planning172> Plans => tracker.Plans;', '    public IReadOnlyList<Planning172> Plans => tracker.Plans;\n    public void DiagnosticApplied173(FreeRacingAction action) => stabilizer.ObserveApplied(action);')
    s = replace(s, '            forecasts++;', '            Trace173.Prediction("transition", input, .44, action, result);\n            forecasts++;')
    files['Controller173.Generated.cs'] = s
    return files


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check',action='store_true')
    args=parser.parse_args()
    for name, data in derive().items():
        data=('// Observation-only #173 derivation. No historical file is edited.\n'+data).replace('\r\n','\n').encode()
        path=HERE/name
        if args.check:
            assert path.read_bytes()==data, name
        else:
            path.write_bytes(data)
    print('Read-only adapters verified.' if args.check else 'Read-only adapters generated.')


if __name__=='__main__':
    main()
