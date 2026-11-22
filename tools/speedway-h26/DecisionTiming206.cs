namespace SpeedwayHandling;

internal sealed record DecisionTiming206(int Count,double? P50,double? P95,double? P99,double? Max,int Over50)
{
    internal bool PassesDeadline=>Count>0&&Over50==0;
    internal bool HasMargin=>PassesDeadline&&P99<40;

    internal static DecisionTiming206 Measure(IEnumerable<double> milliseconds)
    {
        var times=milliseconds.Order().ToArray();
        if(times.Any(x=>!double.IsFinite(x)||x<0))
            throw new ArgumentException("Expected finite nonnegative decision times.");
        double? At(double percentile)=>times.Length==0?null:times[(int)Math.Floor((times.Length-1)*percentile)];
        return new(times.Length,At(.50),At(.95),At(.99),At(1),times.Count(x=>x>50));
    }
}
