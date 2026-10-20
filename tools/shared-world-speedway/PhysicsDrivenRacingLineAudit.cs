using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;

namespace SharedWorldSpeedway;

public static class PhysicsDrivenRacingLineAudit
{
    public const string BaseHead="b01d819fddc9e81a30e961517c27f952bb96dad4";
    public sealed record Condition(string Name,int Skill,float Grip);
    public static Condition[] Conditions()=>new[]{new Condition("A-neutral",50,1),new("B-low-grip",50,.85f),new("C-skill40",40,1),new("C-skill60",60,1)};
    public static RiderSpec Rider(Condition c)=>SlipDynamicsAudit.Rider() with {Profile=FullHeatSoloAudit.Profile(1,
        new(50,50,c.Skill,50,50,50,50,50),new(50,c.Skill,c.Skill,50,50,50))};
    private static TrackFrame Frame()=>new(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
    private static TrackStateSnapshot Surface(Condition c,TrackFrame frame)=>TrackState.CreateDefault(frame.Track,new(c.Grip,0,.5f)).Snapshot();
    private static readonly JsonSerializerOptions Compact=new(Audit.Json){WriteIndented=false};
    private static void Save(string path,object value){if(File.Exists(path))throw new IOException("Evidence already exists: "+path);File.WriteAllText(path,JsonSerializer.Serialize(value,Compact)+"\n");}
    public static void Freeze(string directory)
    {
        Directory.CreateDirectory(directory);var frame=Frame();
        Save(Path.Combine(directory,"plans.json"),new{BaseHead,Plans=PhysicsDrivenRacingLine.Plans(),Conditions=Conditions(),
            Common=new{ReferenceMeters=0,OffsetMeters=5,SpeedMetersPerSecond=18,InitialHeading="native tangent",InitialAngularVelocity=0,
                InitialTargetMeters=5,Drive=1,StepSeconds=.01,Laps=PhysicsDrivenRacingLine.Laps,Parameters=SlipControlValidation.Parameters(),
                PhysicsDrivenRacingLine.RequiredClearanceMeters,PhysicsDrivenRacingLine.RepeatableLapDifferenceSeconds,
                Objective="Lap2 + Lap3 seconds; reject incomplete, outside/unsafe, unstable, nonrepeatable or invalid energy. No line/angle reward.",
                SpatialActivation="First accepted boundary at/after landmark, zero authored-command reaction as #98, no additional solver step.",
                ThrottleLevels=new[]{1f,.7f,.4f},ThrottleSelection="Per condition: stage1 winner, inner and wide; reuse Drive1 results."},
            Geometry=frame.Track.Geometry,frame.LapMeters,Profiles=Conditions().Select(c=>new{c.Name,Rider=Rider(c),Gameplay=Rider(c).Profile.Gameplay})});
    }
    public static (RacingLineEvaluation Evaluation,SlipControlValidation.Capture Capture) Evaluate(Condition condition,RacingLinePlan plan,string? csv=null,
        WorldParameters? parameters=null,Action<RearWheelObservation>? wheelObserver=null,TrackShapeVariant? trackShape=null,
        SpatialControlCommand[]? authoredControls=null)
    {
        var frame=new TrackFrame(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack(),trackShape);
        var scenario=new Scenario(condition.Name+"/"+plan.Name,new[]{Rider(condition)},90,PhysicsDrivenRacingLine.Laps);
        var controls=authoredControls??PhysicsDrivenRacingLine.Compile(plan,frame);
        var watch=Stopwatch.GetTimestamp();
        var capture=SlipControlValidation.Execute(scenario,parameters,spatialControls:controls,surface:Surface(condition,frame),csvPath:csv,wheelObserver:wheelObserver,trackShape:trackShape);
        var elapsed=Stopwatch.GetElapsedTime(watch).TotalMilliseconds;watch=Stopwatch.GetTimestamp();
        var geometry=new RacingLineGeometry(frame);
        var ticks=capture.Ticks.Select(t=>{var gap=geometry.Clearance(t.After);return(Tick:t,Inner:gap.Inner,Outer:gap.Outer);}).ToArray();
        var initial=capture.Initial.Riders.Single();var initialGap=geometry.Clearance(initial);
        var crossing=new LapCrossings();
        foreach(var t in capture.Ticks)crossing.Accept(t.TimeSeconds,t.Before.TotalReferenceMeters,t.TimeSeconds+t.DurationSeconds,t.After.TotalReferenceMeters,frame.LapMeters,PhysicsDrivenRacingLine.Laps);
        var times=crossing.Times.ToArray();var laps=times.Select((t,i)=>t-(i==0?0:times[i-1])).ToArray();
        var minimumInner=Math.Min(initialGap.Inner,ticks.Min(x=>x.Inner));var minimumOuter=Math.Min(initialGap.Outer,ticks.Min(x=>x.Outer));
        double driveWork=0,lateralWork=0,resistanceWork=0,yawWork=0;
        foreach(var t in capture.Ticks){
            var mid=(t.Before.Velocity+t.After.Velocity)/2;var forward=new Vector2(MathF.Cos(t.Before.Heading),MathF.Sin(t.Before.Heading));
            var d=t.Slip.DriveForceNewtons*Vector2.Dot(mid,forward)*t.DurationSeconds;
            var y=t.Slip.LateralForceNewtons*Vector2.Dot(mid,new(-forward.Y,forward.X))*t.DurationSeconds;
            driveWork+=d;lateralWork+=y;resistanceWork+=Vector2.Dot(t.Applied.ForceNewtons,mid)*t.DurationSeconds-d-y;
            yawWork+=t.Applied.TorqueNewtonMeters*(t.Before.AngularVelocity+t.After.AngularVelocity)/2*t.DurationSeconds;
        }
        var kinetic=capture.Ticks.Sum(t=>t.KineticChangeJoules);var residual=kinetic-driveWork-lateralWork-resistanceWork-yawWork;
        var rejections=new List<string>();
        if(!capture.Result.Completed||laps.Length!=PhysicsDrivenRacingLine.Laps)rejections.Add("Incomplete");
        if(Math.Min(minimumInner,minimumOuter)<PhysicsDrivenRacingLine.RequiredClearanceMeters)rejections.Add("CapsuleClearance");
        if(capture.Result.Contacts.Count>0||capture.Result.Riders.Any(r=>r.Activity==RiderActivity.Crashed))rejections.Add("ContactOrCrash");
        var maxUsage=capture.Ticks.Max(t=>t.Slip.GripUtilization);
        if(maxUsage>1.00001)rejections.Add("TyreBudget");
        if(Math.Abs(residual)>Math.Max(5,.001*Math.Abs(driveWork)))rejections.Add("EnergyBalance");
        if(laps.Length==3&&Math.Abs(laps[2]-laps[1])>PhysicsDrivenRacingLine.RepeatableLapDifferenceSeconds)rejections.Add("NonrepeatableFlyingLap");
        var phases=ticks.Where(x=>x.Tick.Before.TotalReferenceMeters>=frame.LapMeters).GroupBy(x=>Phase(x.Tick.Before,frame)).Select(g=>{
            var a=g.ToArray();var duration=a.Sum(x=>x.Tick.DurationSeconds);
            double Mean(Func<SlipControlValidation.Tick,double> f)=>a.Sum(x=>f(x.Tick)*x.Tick.DurationSeconds)/duration;
            return(object)new{Phase=g.Key,DurationSeconds=duration,MeanSpeed=Mean(t=>t.Before.Speed),MinimumSpeed=a.Min(x=>x.Tick.Before.Speed),MaximumSpeed=a.Max(x=>x.Tick.Before.Speed),
                MeanAbsoluteSlipDegrees=Mean(t=>Math.Abs(t.Slip.ActualSlipDegrees)),MaximumAbsoluteSlipDegrees=a.Max(x=>Math.Abs(x.Tick.Slip.ActualSlipDegrees)),
                MeanAbsoluteTargetSlipDegrees=Mean(t=>Math.Abs(t.Slip.TargetSlipDegrees)),MaximumAbsoluteTargetSlipDegrees=a.Max(x=>Math.Abs(x.Tick.Slip.TargetSlipDegrees)),
                MeanFxNewtons=Mean(t=>t.Slip.DriveForceNewtons),MeanFyNewtons=Mean(t=>t.Slip.LateralForceNewtons),MeanDrive=Mean(t=>t.Before.Drive),
                MeanUtilization=Mean(t=>t.Slip.GripUtilization),MaximumUtilization=a.Max(x=>x.Tick.Slip.GripUtilization),
                SlipLossJoules=a.Sum(x=>x.Tick.Slip.LateralLossWatts*x.Tick.DurationSeconds),MeanOffsetMeters=Mean(t=>t.Before.Road.OffsetMeters),
                MinimumInnerClearanceMeters=a.Min(x=>x.Inner),MinimumOuterClearanceMeters=a.Min(x=>x.Outer),
                NearOuterSeconds=a.Where(x=>x.Outer<=1.5).Sum(x=>x.Tick.DurationSeconds),
                NearOuterFraction=a.Where(x=>x.Outer<=1.5).Sum(x=>x.Tick.DurationSeconds)/duration,
                SlidingAndAcceleratingSeconds=a.Where(x=>Math.Abs(x.Tick.Slip.ActualSlipDegrees)>=3&&x.Tick.After.Speed>x.Tick.Before.Speed).Sum(x=>x.Tick.DurationSeconds),
                AlmostStraightSeconds=a.Where(x=>Math.Abs(x.Tick.Slip.ActualSlipDegrees)<=2).Sum(x=>x.Tick.DurationSeconds)};
        }).ToArray();
        var corners=new List<object>();
        for(var i=0;i<capture.Ticks.Count;){
            if(!capture.Ticks[i].Before.Road.IsTurn){i++;continue;}
            var first=i;while(i<capture.Ticks.Count&&capture.Ticks[i].Before.Road.IsTurn)i++;
            var bend=capture.Ticks.Skip(first).Take(i-first).ToArray();var straight=capture.Ticks.Skip(i).TakeWhile(t=>!t.Before.Road.IsTurn).ToArray();
            corners.Add(new{Lap=(int)(bend[0].Before.TotalReferenceMeters/frame.LapMeters)+1,EntrySeconds=bend[0].TimeSeconds,
                EntrySpeed=bend[0].Before.Speed,MinimumSpeed=bend.Min(t=>Math.Min(t.Before.Speed,t.After.Speed)),ExitSpeed=bend[^1].After.Speed,
                EntryOffset=bend[0].Before.Road.OffsetMeters,ExitOffset=bend[^1].After.Road.OffsetMeters,
                NextStraightMaximumSpeed=straight.Select(t=>(double)t.After.Speed).DefaultIfEmpty().Max()});
        }
        var activations=capture.Result.Decisions.Select((d,i)=>{
            var t=capture.Ticks.First(t=>Math.Abs(t.TimeSeconds-d.ActualActivation!.Value)<1e-8);
            return(object)new{LandmarkReferenceMeters=controls[i].ReferenceMeters,AcceptedReferenceMeters=t.Before.TotalReferenceMeters,
                OvershootMeters=t.Before.TotalReferenceMeters-controls[i].ReferenceMeters,d.ObservedAt,d.ActualActivation,d.TargetOffsetMeters,d.Drive};
        }).ToArray();
        double DistanceAt(double time) {
            var t=capture.Ticks.First(t=>t.TimeSeconds+t.DurationSeconds>=time-1e-10);
            var fraction=Math.Clamp((time-t.TimeSeconds)/t.DurationSeconds,0,1);
            return t.Before.DistanceMeters+(t.After.DistanceMeters-t.Before.DistanceMeters)*fraction;
        }
        var closedDistance=times.Length==3?DistanceAt(times[^1]):capture.Result.Riders.Single().DistanceMeters;
        var evaluation=new RacingLineEvaluation(condition.Name,plan,rejections.Count==0,rejections.ToArray(),
            Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(capture.Initial,Audit.Json))),capture.Result.Fingerprint,laps,
            laps.Length==3?laps[1]+laps[2]:double.PositiveInfinity,closedDistance,times.Length==3?closedDistance-DistanceAt(times[0]):0,minimumInner,minimumOuter,
            capture.Ticks.Max(t=>Math.Abs(t.After.Road.OffsetMeters-t.After.TargetOffsetMeters)),maxUsage,capture.Ticks.Sum(t=>t.Slip.LateralLossWatts*t.DurationSeconds),
            driveWork,lateralWork,resistanceWork,yawWork,kinetic,residual,capture.Result.Work.MaximumStepDisplacementMeters,
            ticks.Count(x=>Math.Min(x.Inner,x.Outer)<0),capture.Result.Work.SolverCalls,elapsed,Stopwatch.GetElapsedTime(watch).TotalMilliseconds,
            phases,corners.ToArray(),activations);
        return(evaluation,capture);
    }
    private static string Phase(RiderView v,TrackFrame frame)
    {
        if(v.Road.IsTurn)return v.Road.CornerProgress<1d/3?"entry":v.Road.CornerProgress<2d/3?"middle-turn":"exit";
        var until=frame.DistanceToCurvatureChange(v.Road);
        if(until<=15)return "preparation";
        // Native combined straight length between corner ends, including the start/finish split.
        var straight=frame.Track.Geometry.StraightLengthMeters;
        return straight-until<=12?"straight-start":"straight-middle";
    }
    public static void Run(string directory,string stage)
    {
        if(stage=="freeze"){Freeze(directory);return;}
        Directory.CreateDirectory(directory);
        if(stage=="smoke"){
            var rows=new[]{"inner","wide","wide-tighten"}.Select(name=>Evaluate(Conditions()[0],PhysicsDrivenRacingLine.Plans().Single(p=>p.Name==name)).Evaluation).ToArray();
            Save(Path.Combine(directory,"smoke.json"),rows);
            foreach(var r in rows)Console.WriteLine($"{r.Plan.Name}: feasible={r.Feasible} laps={string.Join("/",r.LapSeconds.Select(t=>t.ToString("F4")))} gaps={r.MinimumInnerClearanceMeters:F3}/{r.MinimumOuterClearanceMeters:F3} residual={r.BalanceResidualJoules:F3}");return;
        }
        if(stage!="measure")throw new ArgumentException("Use freeze, smoke or measure.");
        using var frozen=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"plans.json")));
        var plans=frozen.RootElement.GetProperty("Plans").Deserialize<RacingLinePlan[]>(Audit.Json)!;
        if(!plans.Select(p=>p.NumericKey).SequenceEqual(PhysicsDrivenRacingLine.Plans().Select(p=>p.NumericKey)))throw new InvalidOperationException("Frozen candidates differ.");
        var rows1=new List<RacingLineEvaluation>();var rows2=new List<RacingLineEvaluation>();var winners=new List<object>();double selectionMilliseconds=0;
        foreach(var condition in Conditions()){
            foreach(var plan in plans){var r=Evaluate(condition,plan).Evaluation;rows1.Add(r);Console.WriteLine($"{condition.Name}/{plan.Name}: {r.Feasible} {r.FlyingSeconds:F4} {string.Join("/",r.Rejections)}");}
            var own=rows1.Where(r=>r.Case==condition.Name).ToArray();
            if(own.Select(r=>r.InitialFingerprint).Distinct().Count()!=1)throw new InvalidOperationException("Nonidentical physical starts.");
            var selectionWatch=Stopwatch.GetTimestamp();var winner=PhysicsDrivenRacingLine.Select(own);
            selectionMilliseconds+=Stopwatch.GetElapsedTime(selectionWatch).TotalMilliseconds;
            winners.Add(new{condition.Name,Stage1=winner?.Plan.Name,winner?.FlyingSeconds});
            var selected=new[]{winner?.Plan,plans.Single(p=>p.Name=="inner"),plans.Single(p=>p.Name=="wide")}.OfType<RacingLinePlan>().DistinctBy(p=>p.NumericKey);
            foreach(var plan in selected)foreach(var drive in new[]{.7f,.4f})rows2.Add(Evaluate(condition,plan with{PreparationDrive=drive}).Evaluation);
        }
        Save(Path.Combine(directory,"stage1.json"),rows1);Save(Path.Combine(directory,"stage2.json"),rows2);Save(Path.Combine(directory,"winners.json"),winners);
        var main=rows1.Where(r=>r.Case==Conditions()[0].Name).ToArray();var best=PhysicsDrivenRacingLine.Select(main);
        var replay=new List<(string Alias,RacingLineEvaluation Row)>();
        if(best is not null)replay.Add(("selected",best));
        replay.Add(("inner",main.Single(r=>r.Plan.Name=="inner")));replay.Add(("wide",main.Single(r=>r.Plan.Name=="wide")));
        var failed=main.FirstOrDefault(r=>!r.Feasible);if(failed is not null)replay.Add(("rejected",failed));
        var replayRows=new List<object>();
        foreach(var (alias,row) in replay){
            var (again,capture)=Evaluate(Conditions()[0],row.Plan,Path.Combine(directory,alias+"-accepted.csv"));
            if(again.Fingerprint!=row.Fingerprint)throw new InvalidOperationException("Replay differs from measured plan.");
            var times=row.LapSeconds.Select((_,i)=>row.LapSeconds.Take(i+1).Sum()).ToArray();
            Save(Path.Combine(directory,alias+".json"),new{Run=new{capture.Initial,capture.Result,Passes=Array.Empty<object>(),
                Riders=new[]{new{RiderId=1,Measurement=new{RiderId=1,CrossingSeconds=times,row.LapSeconds}}},capture.Consequences},
                Plan=row.Plan,Evaluation=row,Telemetry=capture.Ticks.Where((t,i)=>i%10==0).Select(SlipControlValidation.Sample)});
            replayRows.Add(new{Alias=alias,Plan=row.Plan.Name,row.Fingerprint,ReplayFingerprint=again.Fingerprint});
        }
        Save(Path.Combine(directory,"replays.json"),replayRows);
        object? replayCost=null;
        if(best is not null){
            var frame=Frame();var watch=Stopwatch.GetTimestamp();
            using var world=new WorldRunner(new[]{Rider(Conditions()[0])},parameters:SlipControlValidation.Parameters(),
                spatialControls:PhysicsDrivenRacingLine.Compile(best.Plan,frame),frozenSurface:Surface(Conditions()[0],frame));
            var normal=world.Run("selected-normal",.01,90,PhysicsDrivenRacingLine.Laps,lapsFromInitialPosition:true);
            var elapsed=Stopwatch.GetElapsedTime(watch).TotalMilliseconds;
            if(normal.Fingerprint!=best.Fingerprint||normal.Work.SolverCalls!=best.SolverCalls)throw new InvalidOperationException("Normal execution differs from the measured plan.");
            replayCost=new{ElapsedMilliseconds=elapsed,normal.Fingerprint,normal.Work.SolverCalls,OptimizerCallsDuringRun=0};
        }
        Save(Path.Combine(directory,"cost.json"),new{Stage1Runs=rows1.Count,Stage2Runs=rows2.Count,ReplayRuns=replay.Count,
            CaptureMilliseconds=rows1.Concat(rows2).Sum(r=>r.CaptureMilliseconds),ValidationMilliseconds=rows1.Concat(rows2).Sum(r=>r.ValidationMilliseconds),
            selectionMilliseconds,NormalReplay=replayCost});
    }
}
