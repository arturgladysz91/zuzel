using System.Globalization;
using CoreSim;

namespace SharedWorldSpeedway;

public sealed record RacingLinePlan(string Name,float Straight,float Entry,float Apex,float Exit,
    double PreparationMeters=15,double EntryFraction=0,double ApexFraction=.35,double ExitFraction=.8,float PreparationDrive=1)
{
    public string NumericKey=>string.Join("/",new double[]{Straight,Entry,Apex,Exit,PreparationMeters,EntryFraction,ApexFraction,ExitFraction,PreparationDrive}
        .Select(x=>x.ToString("R",CultureInfo.InvariantCulture)));
}

/// <summary>Bounded OFFLINE plan compiler/selector. Runtime only executes accepted landmark commands.</summary>
public static class PhysicsDrivenRacingLine
{
    public const int Laps=3;
    public const double RequiredClearanceMeters=.05,RepeatableLapDifferenceSeconds=.05;
    public static RacingLinePlan[] Plans()=>new[] {
        new RacingLinePlan("inner",1,1,1,1),new("middle",5,5,5,5),new("wide",9.5f,9.5f,9.5f,9.5f),
        new("wide-smooth-early",9.5f,7,4,9.5f),new("wide-smooth-short",9.5f,7,4,9.5f,8),
        new("wide-tighten",9.5f,7,2,9.5f),new("wide-late-tight",9.5f,7,1,9.5f,0,.05,.55,.85),
        new("early-open-exit",9.5f,7,2,9.5f,15,0,.35,.65),
        new("middle-open",5,7,4,8),new("inner-open",1,5,8,5),
        new("wide-late-apex",9.5f,9.5f,5,9.5f,15,0,.5,.85),new("middle-tighten",5,3,1,5,15,0,.45,.8)
    };
    public static SpatialControlCommand[] Compile(RacingLinePlan plan,TrackFrame frame,int riderId=1,int laps=Laps)
    {
        if(laps<1||laps>4 || new[]{plan.Straight,plan.Entry,plan.Apex,plan.Exit}.Any(t=>!float.IsFinite(t)||t<.5||t>frame.MinimumUsableWidthMeters-.5)
            || !float.IsFinite(plan.PreparationDrive)||plan.PreparationDrive is <0 or >1
            || !double.IsFinite(plan.PreparationMeters)||plan.PreparationMeters<0||plan.PreparationMeters>20
            || !double.IsFinite(plan.EntryFraction)||!double.IsFinite(plan.ApexFraction)||!double.IsFinite(plan.ExitFraction)
            || plan.EntryFraction<0||plan.EntryFraction>=plan.ApexFraction||plan.ApexFraction>=plan.ExitFraction||plan.ExitFraction>=1)
            throw new ArgumentException("Invalid bounded racing-line plan.",nameof(plan));
        var commands=new List<SpatialControlCommand>{new(riderId,0,plan.Straight,1)};
        var start=0d;var starts=new double[frame.Track.Segments.Count+1];
        for(var i=0;i<frame.Embedding.Segments.Count;i++){
            starts[i]=start;var s=frame.Embedding.Segments[i];start+=s.SegmentType==SegmentType.Straight?s.StraightLengthMeters:s.InnerRadiusMeters*s.TurnAngleRadians;
        }starts[^1]=start;
        if(frame.Shape is not null)for(var i=0;i<starts.Length;i++)starts[i]=frame.SegmentStartMeters(i);
        for(var lap=0;lap<laps;lap++)foreach(var corner in frame.Track.CornerTopology.Corners){
            var entry=lap*frame.LapMeters+starts[corner.StartSegmentIndex];
            var end=lap*frame.LapMeters+starts[corner.StartSegmentIndex+corner.SegmentCount];var length=end-entry;
            // Preparation precedes the corner or is explicitly a late accepted entry.
            var trigger=plan.EntryFraction>0?entry+plan.EntryFraction*length:entry-plan.PreparationMeters;
            commands.Add(new(riderId,trigger,plan.Entry,plan.PreparationDrive));
            commands.Add(new(riderId,entry+.2*length,plan.Entry,1));
            commands.Add(new(riderId,entry+plan.ApexFraction*length,plan.Apex,1));
            commands.Add(new(riderId,entry+plan.ExitFraction*length,plan.Exit,1));
            commands.Add(new(riderId,end,plan.Straight,1));
        }
        return commands.OrderBy(c=>c.ReferenceMeters).ToArray();
    }
    public static RacingLineEvaluation? Select(IEnumerable<RacingLineEvaluation> evaluations)
        =>evaluations.Where(r=>r.Feasible).OrderBy(r=>r.FlyingSeconds).ThenBy(r=>r.Plan.NumericKey,StringComparer.Ordinal).FirstOrDefault();
}

public sealed record RacingLineEvaluation(string Case,RacingLinePlan Plan,bool Feasible,string[] Rejections,
    string InitialFingerprint,string Fingerprint,double[] LapSeconds,double FlyingSeconds,double DistanceMeters,double FlyingDistanceMeters,
    double MinimumInnerClearanceMeters,double MinimumOuterClearanceMeters,double MaximumOffsetErrorMeters,
    double MaximumGripUtilization,double SlipLossJoules,double DriveWorkJoules,double LateralWorkJoules,
    double ResistanceWorkJoules,double YawWorkJoules,double KineticChangeJoules,double BalanceResidualJoules,
    double MaximumDisplacementMeters,int OutsideSamples,long SolverCalls,double CaptureMilliseconds,double ValidationMilliseconds,
    object[] Phases,object[] Corners,object[] Activations);
