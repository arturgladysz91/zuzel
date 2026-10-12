using CoreSim.Interactions;
using CoreSim.PhysicalSpace;

namespace CoreSim;

internal sealed record RiderContactStepStop(RiderIntegrationCursor Cursor, ContestedSpaceReport Verification,
    double FirstTouchSeconds);

/// <summary>One TurnExit step against one frozen production opponent history; no consequence or publication.</summary>
internal static class RiderPartialStepContact
{
    internal static RiderContactStepStop SplitNextStepAtFirstTouch(this RiderIntegrationCursor cursor,
        TrackStateSnapshot surfaceVersion, IReadOnlyList<PhysicalPoseInterval> opponentHistory)
    {
        ArgumentNullException.ThrowIfNull(opponentHistory);
        var snapshot = cursor.Snapshot; var rider = snapshot.Riders.Single(); var start = cursor.Boundary;
        if (opponentHistory.Count == 0 || opponentHistory.Select(p => p.RiderId).Distinct().Count() != 1
            || opponentHistory.Any(p => p.RiderId == rider.RiderId || p.Source is null))
            throw new RiderIntegrationRefusalException("First-touch splitting needs one other rider's actual production pose history.");
        var embedding = new TrackMetricEmbedding(snapshot.Track, deterministicArithmetic: true);
        var opponents = CommonTimePoseHistory.Stitch(opponentHistory.Select(p => ResolvedBikePoses.InEmbedding(p, embedding)));
        var commonStart = rider.ElapsedTimeSeconds + start.TimeSeconds;
        ContestedSpaceReport Observe(ExecutedStepEndpoint endpoint, bool requireCertificate)
        {
            var commonEnd = rider.ElapsedTimeSeconds + endpoint.TimeSeconds;
            var covered = commonStart;
            foreach (var pose in opponents)
            {
                if (pose.EndTimeSeconds <= covered) continue;
                if (pose.StartTimeSeconds > covered) break;
                covered = pose.EndTimeSeconds;
                if (covered >= commonEnd) break;
            }
            if (covered < commonEnd)
                throw new RiderIntegrationRefusalException("Opponent production history does not continuously cover the trial step.");
            var report = ContestedSpaceResolver.Observe(ResolvedBikePoses.FromExecutedStep(snapshot, start, endpoint, embedding)
                .Concat(opponents.Where(p => p.EndTimeSeconds > commonStart && p.StartTimeSeconds < commonEnd)));
            // A sampled touch can bracket a trial even if #55 exhausted its later minimum
            // search. It is only a witness, never a certificate. A clear lower bracket and
            // the final accepted endpoint must have complete resolved certification.
            if (report.FrameCoverageGaps.Count != 0 || report.Intervals.Any(p => p.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous))
                || report.Intervals.Any(p => !p.NumericallyResolved) && (requireCertificate || !report.Intervals.Any(p => p.HasConflict)))
            {
                var failed = report.Intervals.FirstOrDefault(p => !p.NumericallyResolved || p.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous));
                throw new RiderIntegrationRefusalException(FormattableString.Invariant($"#55 cannot certify geometry at {commonEnd:R}: gaps={report.FrameCoverageGaps.Count}; interval={failed?.IntervalStartSeconds:R}..{failed?.IntervalEndSeconds:R}; kind={failed?.Kind}; min={failed?.MinimumSeparationMeters:R}; touch={failed?.FirstTouchCommonTimeSeconds:R}."));
            }
            return report;
        }
        bool Reached(ExecutedStepEndpoint endpoint)
        {
            if (endpoint.TimeSeconds == start.TimeSeconds) return false; // No zero-duration sweep.
            var report = Observe(endpoint, requireCertificate: false);
            if (report.Intervals.Any(p => p.FirstTouchCommonTimeSeconds <= commonStart))
                throw new RiderIntegrationRefusalException("Contact already exists at the accepted boundary.");
            return report.Intervals.Any(p => p.HasConflict);
        }
        var stopped = cursor.SplitNextStep(surfaceVersion, new(Reached));
        var boundary = stopped.Boundary;
        var verified = Observe(new(boundary.CanonicalProgress, boundary.TimeSeconds, boundary.DistanceMeters,
            boundary.Speed, boundary.LateralPosition), requireCertificate: true);
        var touch = verified.Intervals.Where(p => p.HasConflict).Min(p => p.FirstTouchCommonTimeSeconds)!.Value;
        if (rider.ElapsedTimeSeconds + boundary.TimeSeconds - touch > ExecutedPathTraversal.TimeToleranceSeconds)
            throw new RiderIntegrationRefusalException("The accepted endpoint is beyond the newly certified first-touch uncertainty band.");
        return new(stopped, verified, touch);
    }
}
