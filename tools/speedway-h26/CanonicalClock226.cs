namespace SpeedwayHandling;

internal sealed record SolveAttempt46(double Duration, int MidpointIterations, double LastError, string Decision,
    ContactEvent215[] Events, double? WorkResidual);
internal sealed class SolveTrace46
{
    public double Requested { get; set; }
    public double Capped { get; set; }
    public State215? Active { get; set; }
    public State215? Candidate { get; set; }
    public Activation215? NextActivation { get; set; }
    public List<SolveAttempt46> Attempts { get; } = [];
}

internal readonly record struct LegacyStep46(State215 State, Step215 Step, double WindowEnd, double Requested, double Station);

/// <summary>Absolute event clock for the opt-in H45 MODEL integrator.</summary>
internal readonly record struct Clock46(long Origin, long StepTicks)
{
    internal const long PicosecondsPerSecond = 1_000_000_000_000;
    internal const long ReviewTicks = 60_000_000_000;
    internal const long ReactionTicks = 50_000_000_000;
    internal const long StockTicks = 2_500_000_000;

    internal static long Tick(double seconds) => checked((long)Math.Round(seconds * PicosecondsPerSecond,
        MidpointRounding.ToEven));
    internal static double Seconds(long ticks) => ticks / (double)PicosecondsPerSecond;
    internal long Next(long tick) => Origin + ((tick - Origin) / StepTicks + 1) * StepTicks;
    internal long AtOrAfter(long tick) => tick <= Origin ? Origin : Next(tick - 1);
    internal static Activation215 Import(Activation215 a)
    {
        if (!double.IsFinite(a.Requested) || !double.IsFinite(a.Time) || a.Requested > a.Time)
            throw new ArgumentException("Invalid command chronology.", nameof(a));
        return a with { Requested = Seconds(Tick(a.Requested)), Time = Seconds(Tick(a.Time)) };
    }
    internal static Activation215 Command(long review, Command215 command) =>
        new(Seconds(review), Seconds(checked(review + ReactionTicks)), command);
}
