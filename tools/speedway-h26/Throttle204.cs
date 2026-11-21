using System.Diagnostics;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record ThrottleResult204(string? Outcome,double Sweep,HeadingSample198 End,Profile198 Profile,
        double? TimeToGoal,ContactFailure203? ContactFailure);

    private static void AuditThrottle204(string repository,string output,string? additionalNative)
    {
        var paths=new List<string>{Path.Combine(repository,Source204,"native/H27-P-learned-C.json.gz")};
        if (additionalNative is not null) paths.Add(additionalNative);
        var rows=new List<object>();
        var phases=new List<object>();
        var elapsed=Stopwatch.StartNew();
        foreach (string path in paths)
        {
            var archive=Read199<Archive204>(path);
            var run=archive.Run.Riders.Single();
            var actions=run.Decisions.Select(d=>d.Choice.Action).ToArray();
            var corridor=new Corridor199(Map199,double.PositiveInfinity);
            foreach (string physics in new[] {"H27-P","H27-N"})
            {
                var predict=new Predictor198(Frame,Surface,Physics,Settings202(physics),Timing,run.Spec,Mass,Inertia);
                var seen=new HashSet<(int,int)>();
                for (int j=0, i=0;j<actions.Length;j++)
                {
                    double t=run.Decisions[j].Choice.Time;
                    while (i+1<run.Samples.Length&&run.Samples[i+1].Input.Time<=t+1e-8) i++;
                    if (actions[j].Drive>=.999f||Math.Abs(run.Samples[i].Input.Time-t)>1e-8) continue;
                    var start=Sample199(run.Samples[i],archive.Lean[0][i]) with {Review=t,
                        RiderHeightOffsetMeters=i==0?0:archive.Posture![i-1].Posture.RiderHeightOffsetAfterMeters};
                    if (start.Pending.Any(command=>command.ObservedAt>=t-1e-9)) continue;
                    double maxTime=run.Samples[^1].Input.Time;
                    var reference=actions.Skip(j).ToArray();
                    var full=reference.ToArray();
                    ThrottleResult204 Forecast(FreeRacingAction[] controls,double time,double goal=double.PositiveInfinity,
                        bool withCorridor=false)
                    {
                        var cursor=predict.Begin(start,controls,0,goal,.5,profile:true,
                            constraint:withCorridor?corridor.Check:null);
                        cursor.Advance(10000,time);
                        return new(cursor.Outcome,cursor.Sweep,cursor.Snapshot,cursor.Counters,
                            cursor.Reached?cursor.GoalTime-t:null,cursor.ContactFailure);
                    }
                    foreach (var (horizon,reviews) in new[] {(.20,1),(.80,10),(1.50,24)})
                    {
                        if (t+horizon>maxTime+1e-9||reference.Length<reviews) continue;
                        for (int k=0;k<reviews;k++) full[k]=reference[k] with {Drive=1};
                        var baseline=Forecast(reference,t+horizon);
                        var local=Forecast(full,t+horizon);
                        ThrottleResult204? continuing=null,baselineContinuing=null,goalResult=null;
                        // Keep the same archived steering and commands after the intervention, and test
                        // another 0.70 s against the runtime corridor in addition to the physical gates.
                        if (baseline.Outcome is null&&local.Outcome is null&&t+horizon+.70<=maxTime+1e-9)
                        {
                            continuing=Forecast(full,t+horizon+.70,withCorridor:true);
                            baselineContinuing=Forecast(reference,t+horizon+.70,withCorridor:true);
                            if (continuing.Outcome is null&&baselineContinuing.Outcome is null)
                                goalResult=Forecast(full,t+horizon+.70,baseline.End.S,true);
                        }
                        rows.Add(new {Archive=Path.GetFileName(path),Physics=physics,Time=t,S=start.State.Rider.TotalReferenceMeters,
                            RequestedDrive=reference[0].Drive,Horizon=horizon,Reviews=reviews,Pending=start.Pending,
                            Baseline=baseline,Full=local,BaselineContinuation=baselineContinuing,Continuation=continuing,
                            SameGeometricGoal=baseline.End.S,FullGoal=goalResult,
                            TimeGain=goalResult?.TimeToGoal is double time?horizon-time:(double?)null});
                    }
                    var where=Map199.Locate(start.State.Rider.TotalReferenceMeters);
                    if (where.Current is not { } corner||corner.Exit-start.State.Rider.TotalReferenceMeters>8||
                        !seen.Add((corner.Lap,corner.Index))) continue;
                    double sectionGoal=Math.Min(corner.NextEntry,Frame.LapMeters*3);
                    var allFull=reference.Select(a=>a with {Drive=1}).ToArray();
                    var basePhase=Forecast(reference,maxTime,sectionGoal,true);
                    var fullPhase=Forecast(allFull,maxTime,sectionGoal,true);
                    phases.Add(new {Archive=Path.GetFileName(path),Physics=physics,Time=t,
                        Start=start.State.Rider.TotalReferenceMeters,Goal=sectionGoal,corner.Lap,corner.Index,
                        Baseline=basePhase,Full=fullPhase,TimeGain=basePhase.TimeToGoal-fullPhase.TimeToGoal});
                }
            }
        }
        Save(Path.Combine(output,"throttle-model.json.gz"),new {Trials=rows,Phases=phases,ElapsedSeconds=elapsed.Elapsed.TotalSeconds,
            Stopwatch.Frequency,NativeWorlds=0},true);
    }
}
