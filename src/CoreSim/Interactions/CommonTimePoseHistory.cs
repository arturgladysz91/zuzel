using CoreSim.PhysicalSpace;

namespace CoreSim.Interactions;

/// <summary>
/// Float heat-clock addition may put a preceding segment's double adapter end
/// one ULP past the next committed float start. Trim that predecessor only;
/// never sweep a state jump, create a pose, or tolerate a substantive overlap.
/// </summary>
internal static class CommonTimePoseHistory
{
    internal static ContestedSpaceReport Observe(IEnumerable<PhysicalPoseInterval> source)
        => ContestedSpaceResolver.Observe(Stitch(source));
    internal static SpaceCompatibilityResult Compatibility(IEnumerable<PhysicalPoseInterval> source, double ready)
        => ContestedSpaceResolver.ObserveCompatibility(Stitch(source), ready);
    internal static IReadOnlyList<PhysicalPoseInterval> Stitch(IEnumerable<PhysicalPoseInterval> source)
    {
        var result = new List<PhysicalPoseInterval>();
        foreach (var group in source.GroupBy(i => i.RiderId).OrderBy(g => g.Key))
        {
            var intervals = group.OrderBy(i => i.StartTimeSeconds).ToArray();
            for (var index = 0; index < intervals.Length; index++)
            {
                var interval = intervals[index];
                var end = index + 1 < intervals.Length ? Math.Min(interval.EndTimeSeconds, intervals[index + 1].StartTimeSeconds)
                    : interval.EndTimeSeconds;
                if (end < interval.EndTimeSeconds)
                {
                    var boundary = (float)end;
                    var ulp = Math.Abs((double)MathF.BitIncrement(boundary) - boundary);
                    if (interval.EndTimeSeconds - end > ulp)
                        throw new ArgumentException("Pose history overlaps by more than one float heat-clock ULP.");
                    if (end > interval.StartTimeSeconds) result.Add(new Trimmed(interval, end));
                }
                else result.Add(interval);
            }
        }
        return result;
    }
    private sealed class Trimmed(PhysicalPoseInterval source, double end) : PhysicalPoseInterval(source.RiderId,
        source.FrameId, source.StartTimeSeconds, end, source.Dimensions, source.StartsAtDiscontinuity, source.Source, source.SupportsLapWrap)
    {
        public override PhysicalBikePose Sample(double time) { Fraction(time); return source.Sample(time); }
        internal override BikePoseValue SampleValue(double time) { Fraction(time); return source.SampleValue(time); }
        internal override BikeFootprint SampleFootprint(double time) { Fraction(time); return source.SampleFootprint(time); }
        public override PoseRateBounds RateBounds(double start, double finish)
        { Fraction(start); Fraction(finish); return source.RateBounds(start, finish); }
    }
}
