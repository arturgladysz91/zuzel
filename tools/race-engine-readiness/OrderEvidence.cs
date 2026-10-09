using CoreSim;

namespace RaceReadiness;

internal sealed record ProgressPiece(int RiderId, double Start, double End, double P0, double P1, int SegmentIndex)
{
    internal double At(double time) => time == Start ? P0 : time == End ? P1 : P0 + (P1 - P0) * ((time - Start) / (End - Start));
}
internal sealed record VerifiedPass(int Ahead, int Behind, double BracketStart, double BracketEnd, string Phase, int SegmentIndex);
internal sealed record OrderAudit(IReadOnlyList<VerifiedPass> Passes, int TiedIntervals, int CoverageBreaks);

internal static class OrderEvidence
{
    // Observe each positive-duration executed-node piece. Never interpolate zero-time state events.
    internal static IReadOnlyList<ProgressPiece> Pieces(IEnumerable<ResolvedRiderMotion> motions)
    {
        var rows = new List<ProgressPiece>();
        foreach (var motion in motions)
        for (var i = 1; i < motion.Nodes.Count; i++)
        {
            var a = motion.Nodes[i - 1]; var b = motion.Nodes[i];
            if (b.LocalTimeSeconds > a.LocalTimeSeconds)
                rows.Add(new(motion.RiderId, (double)motion.StartElapsedTimeSeconds + a.LocalTimeSeconds,
                    (double)motion.StartElapsedTimeSeconds + b.LocalTimeSeconds, a.CanonicalProgress, b.CanonicalProgress, motion.SegmentIndex));
        }
        // Same one-float-clock-ULP predecessor trimming contract as CommonTimePoseHistory.
        var stitched = new List<ProgressPiece>();
        foreach (var group in rows.GroupBy(p => p.RiderId).OrderBy(g => g.Key))
        {
            var sorted = group.OrderBy(p => p.Start).ToArray();
            for (var i = 0; i < sorted.Length; i++)
            {
                var p = sorted[i]; var end = i + 1 < sorted.Length ? Math.Min(p.End, sorted[i + 1].Start) : p.End;
                if (end < p.End && p.End - end > Math.Abs((double)MathF.BitIncrement((float)end) - (float)end))
                    throw new InvalidOperationException("Substantive common-time motion overlap.");
                if (end > p.Start) stitched.Add(p with { End = end, P1 = p.At(end) });
            }
        }
        return stitched;
    }

    internal static OrderAudit Analyze(IReadOnlyList<ProgressPiece> pieces, double firstBendExit)
    {
        var passes = new List<VerifiedPass>(); var ties = 0; var breaks = 0;
        var groups = pieces.GroupBy(p => p.RiderId).OrderBy(g => g.Key).Select(g => g.OrderBy(p => p.Start).ToArray()).ToArray();
        for (var a = 0; a < groups.Length; a++) for (var b = a + 1; b < groups.Length; b++)
        {
            var i = 0; var j = 0; double? previousEnd = null; double previousA = 0, previousB = 0;
            var priorSign = 0; double signTime = 0, signProgress = 0;
            while (i < groups[a].Length && j < groups[b].Length)
            {
                var x = groups[a][i]; var y = groups[b][j];
                var start = Math.Max(x.Start, y.Start); var end = Math.Min(x.End, y.End);
                if (end > start)
                {
                    var p0 = x.At(start); var q0 = y.At(start);
                    // Missing time, explicit progress jump or retirement ends a verified continuous comparison.
                    if (previousEnd != start || previousA != p0 || previousB != q0)
                    { if (previousEnd.HasValue) breaks++; priorSign = 0; }
                    Observe(start, p0, q0); Observe(end, x.At(end), y.At(end));
                    previousEnd = end; previousA = x.At(end); previousB = y.At(end);
                    if (p0 == q0 && previousA == previousB) ties++;
                    void Observe(double time, double px, double py)
                    {
                        var sign = Math.Sign(px - py);
                        if (sign == 0) return; // A tie carries no rider-ID ordering.
                        var progress = Math.Min(px, py);
                        if (priorSign != 0 && sign != priorSign)
                            passes.Add(new(sign > 0 ? x.RiderId : y.RiderId, sign > 0 ? y.RiderId : x.RiderId,
                                signTime, time, progress < firstBendExit ? "start" : signProgress < firstBendExit ? "first-bend boundary" : "racing", x.SegmentIndex));
                        priorSign = sign; signTime = time; signProgress = progress;
                    }
                }
                if (x.End <= y.End) i++; else j++;
            }
        }
        return new(passes.OrderBy(p => p.BracketEnd).ThenBy(p => p.Ahead).ToArray(), ties, breaks);
    }
}
