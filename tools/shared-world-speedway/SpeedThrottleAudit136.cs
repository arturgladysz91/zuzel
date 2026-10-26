using System.Diagnostics;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

public sealed record ThrottleState136(string Case,int Corner,double Distance,double Time,double Remaining,FreeRacingInput Input);
public sealed record ThrottleTrial136(string Name,string Case,int Corner,double Distance,string Arm,double Time,FreeRacingAction[] Actions,bool ExactPrefix,RobustMetrics Metrics);

/// <summary>Default-OFF finite diagnostic interventions. Never a learned or production controller.</summary>
public static class SpeedThrottleAudit136
{
    public const string BaseHead="faadcba0652e0661de57e201d05fedb0ab4a52d0";
    public static readonly double[] Distances={20,15,10,5,0};
    public static readonly AwarenessCase134[] Diagnostic=MultiActionExperiment135.Cases.Take(2).ToArray();
    public static readonly AwarenessCase134[] Confirm=MultiActionExperiment135.Cases.Skip(3).Take(2).ToArray();
    static readonly JsonSerializerOptions Json=FreeLearningExperiment.Json;
    static FreeLearningPolicy Policy=>MultiActionExperiment135.Policy();
    static void Save(string d,string file,object value)=>RobustLearningExperiment.Save(d,file,value);
    static T Read<T>(string d,string file)=>RobustLearningExperiment.Read<T>(d,file);
    static string Hash(string file)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
    public static Dictionary<string,string> Sources()=>Directory.GetFiles("tools/shared-world-speedway","*.cs").Order().ToDictionary(x=>Path.GetFileName(x)!,x=>Hash(x));
    public static double Exit(TrackFrame f,int corner)=>f.SegmentStartMeters(corner==1?4:8);
    public static ThrottleState136 Select(AwarenessCase134 c,RobustEpisode baseline,int corner,double distance){
        var f=TrackAwarenessExperiment134.Frame(c.Geometry);var exit=Exit(f,corner);var start=f.SegmentStartMeters(corner==1?1:5);
        var samples=baseline.Replay.Decisions.Select(d=>baseline.Replay.Samples.Single(s=>Math.Abs(s.Input.Time-d.Time)<1e-8));
        var sample=samples.First(s=>s.Input.Rider.TotalReferenceMeters>=start && (distance==0?s.Input.Rider.TotalReferenceMeters>=exit:
            s.Input.Rider.TotalReferenceMeters<exit&&f.DistanceToCurvatureChange(s.Input.Rider.Road)<=distance));
        return new(c.Name,corner,distance,sample.Input.Time,sample.Input.Rider.TotalReferenceMeters<exit?f.DistanceToCurvatureChange(sample.Input.Rider.Road):exit-sample.Input.Rider.TotalReferenceMeters,
            RiderDecisionAudit133.BeforeDecision(sample));
    }
    static RobustEpisode Native(string d,string raw,RobustBudget b,string name,AwarenessCase134 c,ThrottleState136? state=null,string arm="A",FreeRacingAction[]? actions=null){
        var f=TrackAwarenessExperiment134.Frame(c.Geometry);var spec=FreeLearningExperiment.Rider(c.Scenario);var surface=MultiActionExperiment135.Surface(f);var p=FreeLearningExperiment.Parameters();
        FreeLearningDecision Choose(FreeRacingInput i){
            var decision=MultiActionPlanner135.Baseline(i,Policy,spec,f,surface,p);
            if(state is null||i.Time<state.Time-1e-8)return decision;
            if(arm=="B"&&Math.Abs(i.Time-state.Time)<1e-8)return decision with{Action=decision.Action with{Drive=1}};
            if(arm=="D"&&i.Rider.TotalReferenceMeters<Exit(f,state.Corner)+20)return decision with{Action=decision.Action with{Drive=1}};
            if(arm=="C"&&actions is not null&&i.Time<state.Time+actions.Length*.2-1e-8){
                var n=(int)Math.Round((i.Time-state.Time)/.2);return decision with{Action=actions[n]};
            }
            return decision;
        }
        return MultiActionNative135.Execute(d,raw,b,"native",name,16015,Policy,c.Scenario,twoBends:true,horizonSeconds:.9,normalizeProgress:true,
            fullThrottleCandidate:true,trackShape:TrackAwarenessExperiment134.Variant(c.Geometry),chooser:Choose);
    }
    static FreeRacingAction[] Preparation(ThrottleState136 state,AwarenessCase134 c,out object evidence,out long contacts){
        var f=TrackAwarenessExperiment134.Frame(c.Geometry);var spec=FreeLearningExperiment.Rider(c.Scenario);var surface=MultiActionExperiment135.Surface(f);var p=FreeLearningExperiment.Parameters();
        // A finite offline diagnostic: prepare at partial gas, then full gas, then release steering.
        // No future native observation, offset, ideal path, fitted weights, or recurrent planner.
        var options=(from first in new[]{-5f,-1.5f,0f,1.5f,5f} from second in new[]{-1.5f,0f,1.5f}
            select new[]{new FreeRacingAction(first*MathF.PI/180,(float)Policy.Drive),new(second*MathF.PI/180,1),new(0,1)}).ToArray();
        var leaves=options.Select(a=>{var pred=MultiActionPlanner135.Predict(state.Input,a,spec,f,surface,p);return new{Actions=a,Evaluation=MultiActionPlanner135.Evaluate(state.Input,Policy,f,surface,pred,a[0]),
            MaxBeta=pred.Points.Max(q=>Math.Abs(q.Rider.SlideRadians))*180/Math.PI,Contacts=pred.ForceEvaluations};}).ToArray();
        contacts=leaves.Sum(x=>(long)x.Contacts);
        var chosen=leaves.OrderBy(x=>x.Evaluation.Safety).ThenBy(x=>x.Evaluation.Score).ThenBy(x=>x.Actions[0].SteeringRadians).ThenBy(x=>x.Actions[1].SteeringRadians).First();
        evidence=new{state.Case,state.Corner,state.Distance,state.Time,Options=leaves,Chosen=chosen.Actions,Rule="Original frozen safety/score, deterministic angle ties; diagnostic only, not AI behaviour"};return chosen.Actions;
    }
    public static object[] Costs(ThrottleState136 state,AwarenessCase134 c,out long contacts){
        var f=TrackAwarenessExperiment134.Frame(c.Geometry);var spec=FreeLearningExperiment.Rider(c.Scenario);var surface=MultiActionExperiment135.Surface(f);var p=FreeLearningExperiment.Parameters();var policy=Policy;
        var decision=MultiActionPlanner135.Baseline(state.Input,policy,spec,f,surface,p);contacts=decision.ForceEvaluations;
        var ranked=decision.Candidates.OrderBy(x=>x.Safety).ThenBy(x=>x.Score).ThenBy(x=>Math.Abs(x.Action.SteeringRadians-state.Input.Active.SteeringRadians)).ThenBy(x=>x.Action.SteeringRadians).ToArray();
        var rows=new List<object>();
        foreach(var candidate in decision.Candidates){
            var pred=FreeRacingModel.Predict(state.Input,spec,f,surface,p,147,52.72097f,.9,candidate.Action);contacts+=pred.ForceEvaluations;
            var v=pred.Points[^1].Rider;var road=f.RoadAt(v.TotalReferenceMeters+Math.Max(.1,f.ReferenceSpeed(v.Road,v.Speed)*policy.PreviewSeconds),v.Road.OffsetMeters);
            var desiredYaw=v.Speed*(road.IsTurn&&double.IsFinite(road.RadiusMeters)?1/road.RadiusMeters:0);
            var lane=Math.Clamp((float)v.Road.OffsetMeters/LaneModel.UsableRacingWidthMeters(f.Track.Segments[v.Road.Segment].Type,f.Track.Geometry)*4,0,4);
            var capacity=LoadBasedTyreGrip.Budget(147,surface.SampleSurface(v.Road.Segment,lane),TwoContactBikeParameters.Default,null).TotalCapacityNewtons/147;
            var envelope=desiredYaw>1e-8?Math.Sqrt(.8*capacity/(desiredYaw/Math.Max(v.Speed,.5))):100;
            var progress=-policy.ProgressWeight*candidate.Progress*.6/.9;var direction=policy.DirectionWeight*candidate.DirectionError*candidate.DirectionError*10;
            var yaw=policy.YawWeight*Math.Pow(v.AngularVelocity-desiredYaw,2)*10;var throttle=policy.ThrottleWeight*Math.Pow(candidate.Action.Drive-policy.Drive,2);
            var steering=policy.SteeringChangeWeight*Math.Pow((candidate.Action.SteeringRadians-state.Input.Active.SteeringRadians)*180/Math.PI,2);
            var geometry=policy.GeometrySpeedWeight*Math.Pow(Math.Max(0,v.Speed-envelope),2);var clearance=10*Math.Pow(Math.Max(0,.2-candidate.FutureClearance),2);
            var unsafeCost=candidate.Safety==FreeActionSafety.Unsafe?10000*Math.Pow(Math.Max(.001,-candidate.Clearance),2):0;
            var sum=progress+direction+yaw+throttle+steering+geometry+clearance+unsafeCost;
            if(Math.Abs(sum-candidate.Score)>1e-8)throw new InvalidOperationException("Cost decomposition differs from frozen objective.");
            var causes=new List<string>();if(pred.Points.Any(q=>q.Inner<0))causes.Add("inner footprint crossing");if(pred.Points.Any(q=>q.Outer<0))causes.Add("outer footprint crossing");
            if(pred.Points.Any(q=>Math.Abs(q.Rider.SlideRadians)>Math.PI/4))causes.Add("beta45");if(candidate.Clearance>=0&&candidate.Clearance<.2)causes.Add("horizon clearance below .2m");
            if(candidate.FutureClearance<.1)causes.Add("closing extrapolation below .1m");
            rows.Add(new{state.Case,state.Corner,state.Distance,state.Time,candidate.Action,Safety=candidate.Safety.ToString(),Rank=Array.IndexOf(ranked,candidate)+1,Chosen=candidate.Action==decision.Action,
                candidate.Progress,SpeedKph=candidate.ExitSpeed*3.6,candidate.Score,ProgressCost=progress,DirectionCost=direction,YawCost=yaw,ThrottleCost=throttle,SteeringCost=steering,GeometryCost=geometry,ClearanceCost=clearance,UnsafeCost=unsafeCost,
                candidate.Clearance,candidate.FutureClearance,MaxBetaDegrees=pred.Points.Max(q=>Math.Abs(q.Rider.SlideRadians))*180/Math.PI,MaxYawRate=pred.Points.Max(q=>Math.Abs(q.Rider.AngularVelocity)),Causes=causes,
                Note="Yaw is a cost, not a separate hard limit; NoSafeContinuation means all H09 candidates fail SafeInHorizon, not proof of global impossibility"});
        }
        return rows.ToArray();
    }
    static ThrottleTrial136 Trial(string d,string raw,RobustBudget b,AwarenessCase134 c,RobustEpisode baseline,ThrottleState136 s,string arm,List<ThrottleTrial136> rows,FreeRacingAction[]? actions=null){
        var name=$"{c.Name}-{arm}-C{s.Corner}-M{s.Distance}";var run=Native(d,raw,b,name,c,s,arm,actions);var exact=MultiActionExperiment135.Prefix(baseline,run,s.Time,.24);
        var row=new ThrottleTrial136(name,c.Name,s.Corner,s.Distance,arm,s.Time,actions??new[]{run.Replay.Decisions.Single(x=>Math.Abs(x.Time-s.Time)<1e-8).Action},exact,run.Metrics);
        rows.Add(row);Save(d,"trials.json",rows);if(!exact)throw new InvalidOperationException("Native prefix differs before first command activation.");
        foreach(var request in run.Replay.Decisions.Where(x=>x.Time>=s.Time-1e-8&&x.Time<(arm=="C"?s.Time+3*.2:s.Time+.2)-1e-8)){
            if(request.Time+.24<run.Metrics.Native.DurationSeconds-1e-8&&!run.Replay.Activations.Any(x=>Math.Abs(x.ObservedAt-request.Time)<1e-8&&Math.Abs(x.ActivationAt-request.Time-.24)<1e-8&&x.Action==request.Action))throw new InvalidOperationException("Missing queued activation before world end.");
        }
        return row;
    }
    static void Straight(string d,string raw,RobustBudget b){
        var summaries=new List<object>();
        foreach(var speed in new[]{60d,70,80,90})foreach(var gas in new[]{Policy.Drive,1d}){
            var name=$"straight-{speed}-{(gas==1?"full":"partial")}";if(File.Exists(Path.Combine(raw,name+".json.gz")))throw new IOException("Refuse to overwrite straight evidence.");
            var n=b.Reserve(name,"native");var cpu=Process.GetCurrentProcess().TotalProcessorTime;var f=TrackAwarenessExperiment134.Frame("progressive-c");var start=f.SegmentStartMeters(4);
            var scenario=new FreeLearningScenario(name,4,(float)(speed/3.6),5,start);var spec=FreeLearningExperiment.Rider(scenario);var p=FreeLearningExperiment.Parameters();
            RiderView before=default;TwoContactForces force=default;RearWheelStep wheel=default;var rows=new List<object>();string? failure=null;
            using var world=new WorldRunner(new[]{spec},parameters:p,trackShape:TrackAwarenessExperiment134.Variant("progressive-c"),frozenSurface:MultiActionExperiment135.Surface(f),twoContactInitial:new(),freeLearningPolicy:Policy,
                diagnostics:new(){Force=x=>before=x.Rider,TwoContact=x=>force=x.Forces,RearWheel=x=>wheel=x.Step});
            world.FreeLearning!.DiagnosticChooser=i=>new(i.Time,new(0,(float)gas),FreeActionSafety.SafeInHorizon,false,Array.Empty<FreeLearningCandidate>(),0,0);
            var initial=world.Snapshot;var mechanical=world.MechanicalProperties(1);var boundary=new RacingLineGeometry(f);var milestones=new Dictionary<int,object>();
            var result=world.Run(name,.01,8,acceptedObserver:s=>{
                var v=s.Riders[0];var gap=boundary.Clearance(v);var dist=v.TotalReferenceMeters-start;
                var bodyDelta=TwoContactBikeAudit.BodyKinetic(v,mechanical.MassKg,mechanical.InertiaKgMetersSquared)-TwoContactBikeAudit.BodyKinetic(before,mechanical.MassKg,mechanical.InertiaKgMetersSquared);
                var excess=bodyDelta+.45*(wheel.AfterOmega*wheel.AfterOmega-wheel.BeforeOmega*wheel.BeforeOmega)-wheel.EngineWorkJoules;
                if(gap.Inner<0||gap.Outer<0)failure="footprint crossing";if(Math.Abs(v.SlideRadians)>Math.PI/4)failure="beta45";
                if(excess>.05+1e-6*Math.Max(TwoContactBikeAudit.BodyKinetic(before,mechanical.MassKg,mechanical.InertiaKgMetersSquared),TwoContactBikeAudit.BodyKinetic(v,mechanical.MassKg,mechanical.InertiaKgMetersSquared)))failure="energy invariant";
                rows.Add(new{Time=s.TimeSeconds,Distance=dist,Before=before,After=v,Forces=force,Wheel=wheel,Inner=gap.Inner,Outer=gap.Outer,EnergyExcess=excess});
                foreach(var m in new[]{10,20,30,40,50,62})if(dist>=m&&!milestones.ContainsKey(m))milestones[m]=new{Time=s.TimeSeconds,ActualDistance=dist,SpeedKph=v.Speed*3.6,Drive=v.Drive};
            },diagnosticStop:s=>failure is not null||s.Riders[0].TotalReferenceMeters>=start+62);
            using(var z=new GZipStream(File.Create(Path.Combine(raw,name+".json.gz")),CompressionLevel.SmallestSize))JsonSerializer.Serialize(z,new{Initial=initial,Result=result,Rows=rows},Json);
            var elapsed=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds;b.Complete(n,elapsed,result.Work.RearWheelForceEvaluations+result.Work.RearWheelBoundaryEvaluations,0,rawPath:Path.Combine(raw,name+".json.gz"));
            summaries.Add(new{Name=name,InitialKph=speed,RequestedDrive=gas,InitialWheelOmega=initial.RearWheels![0].RearWheelAngularVelocityRadPerSecond,InitialEngine=Engine500.Evaluate(initial.RearWheels[0].RearWheelAngularVelocityRadPerSecond,spec.Setup,1),
                StartReference=start,EndReference=start+62,Milestones=milestones,Failure=failure,CpuSeconds=elapsed,Note="Pure rolling initial wheel; rigid crank ratio, no independent crank state. Initial active gas1 for shared .24s latency, then requested gas. First accepted native sample at/after each distance, actual distance included; no speed interpolation."});
            Save(d,"straight.json",summaries);Console.WriteLine(name+": "+JsonSerializer.Serialize(milestones.GetValueOrDefault(62),Json));
        }
    }
    public static void Run(string d,string raw,string stage){
        Directory.CreateDirectory(d);Directory.CreateDirectory(raw);
        if(stage=="freeze"){
            if(File.Exists(Path.Combine(d,"protocol.json")))throw new IOException("Frozen protocol exists.");
            Save(d,"protocol.json",new{BaseHead,PolicyId=16015,Policy,Diagnostic,Confirm,Distances,Sources=Sources(),Parameters=FreeLearningExperiment.Parameters(),
                Geometry="Existing TrackFrame progressive-b/c. Ends at SegmentStartMeters(4)/(8); complete footprint gate is a different downstream plane. 35+27 unchanged.",
                States="First baseline .2s decision in each bend with fixed-offset physical remaining distance <=20/15/10/5m; 0=first decision at/after geometric end. Exact states locked after baseline, before interventions. No replacement after results.",
                Arms="A frozen134A. B one full-throttle request with A steering then A. D diagnostic full throttle repeatedly through exit+20 reference metres, A steering; only progressive-c, all5 timings. C at15/10m:15 finite 3-command options, original H09 safety/score chooses before native. Then A. Never label B/C/D learned.",
                Limits=new{NativeWorlds=54,CpuSeconds=1400,ContactEvaluations=25000000,Forecasts=250000,EpisodeCpuSeconds=30,PreflightWorlds=2},
                Allocation="2 replay preflight baselines +20 B +10 D +8 C +8 straight +2 held baselines +4 held trials =54 worlds. CPU preflight max<=20s; otherwise stop and narrow before further worlds.",
                Confirmation="V-B/V-C unopened until diagnostic lock. Per geometry choose legal minimum full-time B and C (stable name ties), retain corner/distance/exact C sequence. No positive-gain claim without held legal continuation and gain. No retuning.",
                Success="DIAGNOSIS: complete measured causes and timing, two geometries, native legal continuations. PERFORMANCE: >=3 distinct held states across2geometries, active1 at geometric exit, higher exit/straight speed, lower two-bend time, zero new DNF. Finite failure never proves global physical impossibility.",
                Straight="Actual62m back straight,60/70/80/90kmh, pure rolling wheel omega=v/.33 and rigid engine, partial vs full through real queue; same surface/setup/drag. No extended fixture or teleport.",
                Historical="Reuse selected #135 episodes for audit; exact fresh native baselines must reproduce stored fingerprints. Old #116 long straight uses different fixture: no unqualified119/151kmh transfer.",RawDirectory=Path.GetFullPath(raw)});return;
        }
        var protocol=Read<JsonElement>(d,"protocol.json");foreach(var s in protocol.GetProperty("Sources").EnumerateObject())if(Sources()[s.Name]!=s.Value.GetString())throw new InvalidOperationException("Frozen source changed: "+s.Name);
        using var b=new RobustBudget(d,new Dictionary<string,int>{{"native",54},{"offline",80}},54,1400,25000000);
        if(stage=="preflight"){
            using var zip=new GZipStream(File.OpenRead("docs/experiments/multi-action-planning/selected-replays.json.gz"),CompressionMode.Decompress);
            var old=JsonSerializer.Deserialize<JsonElement>(zip,Json).GetProperty("Runs");var states=new List<ThrottleState136>();var checks=new List<object>();bool passed=true;
            foreach(var c in Diagnostic){
                var baseline=Native(d,raw,b,"baseline-"+c.Name,c);var historical=old.GetProperty("baseline-"+c.Name).Deserialize<RobustEpisode>(Json)!;
                var exact=baseline.Metrics.Native.Fingerprint==historical.Metrics.Native.Fingerprint;passed&=exact&&baseline.Metrics.Native.Completed&&baseline.Metrics.Native.CpuSeconds<=20;
                checks.Add(new{c.Name,ExactHistoricalFingerprint=exact,baseline.Metrics,HistoricalFingerprint=historical.Metrics.Native.Fingerprint});
                foreach(var corner in new[]{1,2})foreach(var dist in Distances)states.Add(Select(c,baseline,corner,dist));
                Save(d,c.Geometry+"-geometry.json",ReplayGeometry.Create(TrackAwarenessExperiment134.Frame(c.Geometry)));
            }
            Save(d,"states.json",states);Save(d,"preflight.json",new{Passed=passed,Checks=checks});if(!passed)throw new InvalidOperationException("Preflight failed; preserve evidence and stop.");return;
        }
        if(!Read<JsonElement>(d,"preflight.json").GetProperty("Passed").GetBoolean())throw new InvalidOperationException("Preflight gate closed.");
        if(stage=="diagnostic"){
            if(File.Exists(Path.Combine(d,"diagnostic-lock.json")))throw new IOException("Diagnostic stage sealed.");
            var states=Read<ThrottleState136[]>(d,"states.json");var trials=File.Exists(Path.Combine(d,"trials.json"))?Read<List<ThrottleTrial136>>(d,"trials.json"):new();var costs=new List<object>();var preparations=new List<object>();
            var cpu=Process.GetCurrentProcess().TotalProcessorTime;long contacts=0;int forecasts=0;
            foreach(var c in Diagnostic){var baseline=RobustLearningExperiment.ReadEpisode(raw,"baseline-"+c.Name);
                foreach(var s in states.Where(x=>x.Case==c.Name)){
                    costs.AddRange(Costs(s,c,out var evals));contacts+=evals;forecasts+=2*FreeLearningController.Actions(s.Input,Policy,true).Length;
                    Trial(d,raw,b,c,baseline,s,"B",trials);
                    if(c.Geometry=="progressive-c")Trial(d,raw,b,c,baseline,s,"D",trials);
                    if(s.Distance is 15 or 10){var actions=Preparation(s,c,out var evidence,out var contact);contacts+=contact;forecasts+=15;preparations.Add(evidence);Save(d,"preparations.json",preparations);Trial(d,raw,b,c,baseline,s,"C",trials,actions);}
                }
            }
            var n=b.Reserve("counterfactual-and-sequence-audit","offline",false);b.Complete(n,(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds-b.Entries.Where(x=>x.Name.Contains("-B-")||x.Name.Contains("-C-")||x.Name.Contains("-D-")).Sum(x=>x.CpuSeconds),contacts,forecasts);
            Save(d,"costs.json",costs);Straight(d,raw,b);
            var selected=Diagnostic.SelectMany(c=>new[]{"B","C"}.Select(arm=>trials.Where(x=>x.Case==c.Name&&x.Arm==arm&&x.Metrics.Native.Completed).OrderBy(x=>x.Metrics.Native.GateSeconds).ThenBy(x=>x.Name).FirstOrDefault())).ToArray();
            Save(d,"diagnostic-lock.json",new{Sources=Sources(),Selected=selected,TrialsHash=Hash(Path.Combine(d,"trials.json")),CostsHash=Hash(Path.Combine(d,"costs.json")),FrozenBeforeConfirmation=true});return;
        }
        if(stage=="confirm"){
            var seal=Read<JsonElement>(d,"diagnostic-lock.json");var selected=seal.GetProperty("Selected").Deserialize<ThrottleTrial136?[]>(Json)!;var trials=Read<List<ThrottleTrial136>>(d,"trials.json");var confirmStates=new List<ThrottleState136>();
            for(int ci=0;ci<Confirm.Length;ci++){
                var c=Confirm[ci];var baseline=Native(d,raw,b,"baseline-"+c.Name,c);
                foreach(var selection in selected.Where(x=>x?.Case==Diagnostic[ci].Name)){
                    var s=Select(c,baseline,selection!.Corner,selection.Distance);confirmStates.Add(s);Save(d,"confirm-states.json",confirmStates);
                    Trial(d,raw,b,c,baseline,s,selection.Arm,trials,selection.Arm=="C"?selection.Actions:null);
                }
            }
            Save(d,"complete.json",new{Worlds=b.Entries.Sum(e=>e.NativeWorlds),CpuSeconds=b.Entries.Sum(e=>e.CpuSeconds),ContactEvaluations=b.Entries.Sum(e=>e.Evaluations),Forecasts=b.Entries.Sum(e=>e.Forecasts),ExactPrefixes=trials.All(x=>x.ExactPrefix)});return;
        }
        throw new ArgumentException("freeze|preflight|diagnostic|confirm");
    }
}
