using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;

namespace SharedWorldSpeedway;

public sealed record State135(string Name,AwarenessCase134 Case,string Slot,double Time,FreeRacingInput Input);
public sealed record Pair135(string State,string Arm,string RawName,FreeRacingAction[] Actions,FreeLearningCandidate Prediction,RobustMetrics Baseline,RobustMetrics Native,bool ExactPrefix,bool ExactRequests,object Accuracy);
public static class MultiActionExperiment135
{
    public const string BaseHead="999b08aa34ffed09d7ade720ee1f6f2c1422b5f4";
    public static readonly string Historical=PhysicalSteeringAudit.PreviousPath("../track-awareness-anticipatory-riding");
    public static FreeLearningPolicy Policy()=>TrackAwarenessExperiment134.Read<AwarenessPolicy134[]>(Historical,"choices.json").Single(x=>x.Id==16015&&!x.Awareness).Policy;
    public static readonly AwarenessCase134[] Cases={
        new("D-B","progressive-b",new("D-B",4,16,5,18.125,.08f,.012f,.025f),true),
        new("D-C","progressive-c",new("D-C",4,12,7,18.625,-.10f,-.02f,-.035f),true),
        new("D-E","egg-asymmetric",new("D-E",4,15,4,18.125,.12f,.018f,.04f),true),
        new("V-B","progressive-b",new("V-B",4,13.75f,4.2f,19.125,-.09f,-.014f,.02f),true),
        new("V-C","progressive-c",new("V-C",4,17.25f,6.15f,19.125,.11f,.016f,-.03f),true),
        new("V-E","egg-asymmetric",new("V-E",4,11.75f,7.1f,19.125,-.13f,-.019f,.035f),true)};
    public static readonly string[][] Slots={
        new[]{"straight-end","entry1","middle1","exit1","straight2","preparation2","entry2","near-boundary"},
        new[]{"entry1","middle1","maximum-yaw","preparation2"},
        new[]{"entry1","entry2","near-boundary","exit1"},
        new[]{"straight-end","preparation2"},new[]{"straight-end","preparation2"},new[]{"straight-end","preparation2"}};
    public static Dictionary<string,string> Sources()=>Directory.GetFiles("tools/shared-world-speedway","*.cs").Order().ToDictionary(p=>Path.GetFileName(p)!, p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
    public static void Save(string d,string file,object value)=>RobustLearningExperiment.Save(d,file,value);
    public static T Read<T>(string d,string file)=>RobustLearningExperiment.Read<T>(d,file);
    public static TrackStateSnapshot Surface(TrackFrame f)=>TrackState.CreateDefault(f.Track,new(1,0,.5f)).Snapshot();
    public static FreeLearningDecision Baseline(FreeRacingInput i,AwarenessCase134 c)=>MultiActionPlanner135.Baseline(i,Policy(),FreeLearningExperiment.Rider(c.Scenario),TrackAwarenessExperiment134.Frame(c.Geometry),Surface(TrackAwarenessExperiment134.Frame(c.Geometry)),FreeLearningExperiment.Parameters());
    public static RobustEpisode Native(string d,string raw,RobustBudget b,string name,AwarenessCase134 c,double? at=null,FreeRacingAction[]? actions=null,double? duration=null,bool hold=false){
        if(b.Entries.Sum(e=>e.Forecasts)>240000)throw new InvalidOperationException("Frozen forecast limit reserve reached.");
        var frame=TrackAwarenessExperiment134.Frame(c.Geometry);var surface=Surface(frame);var spec=FreeLearningExperiment.Rider(c.Scenario);var p=FreeLearningExperiment.Parameters();var policy=Policy();
        FreeLearningDecision Choose(FreeRacingInput i){
            if(at is{} t&&actions is not null&&i.Time>=t-1e-8&&(hold||i.Time<t+actions.Length*FreeLearningController.ReviewSeconds-1e-8)){
                var index=Math.Min(actions.Length-1,(int)Math.Round((i.Time-t)/FreeLearningController.ReviewSeconds));
                return new(i.Time,actions[index],FreeActionSafety.Uncertain,false,Array.Empty<FreeLearningCandidate>(),0,0);
            }
            return MultiActionPlanner135.Baseline(i,policy,spec,frame,surface,p);
        }
        return MultiActionNative135.Execute(d,raw,b,"native",name,16015,policy,c.Scenario,shortSeconds:duration,twoBends:true,horizonSeconds:.9,normalizeProgress:true,
            fullThrottleCandidate:true,trackShape:TrackAwarenessExperiment134.Variant(c.Geometry),chooser:Choose);
    }
    public static bool Prefix(RobustEpisode baseline,RobustEpisode actual,double at,double reaction)=>baseline.Replay.Samples.Where(s=>s.Input.Time<at+reaction-1e-8).All(s=>{
        var q=actual.Replay.Samples.FirstOrDefault(x=>Math.Abs(s.Input.Time-x.Input.Time)<1e-8);
        return q is not null&&s.Input.Rider==q.Input.Rider&&s.Input.Omega==q.Input.Omega&&s.Input.Steering==q.Input.Steering&&s.Input.Active==q.Input.Active
            &&(s.Input.Time>=at-1e-8||s.Input.Pending.SequenceEqual(q.Input.Pending));});
    public static object Accuracy(FreeRacingPrediction forecast,RobustEpisode native,out bool passed){
        var errors=new List<object>();bool ok=true;int compared=0;double Deg(double x)=>Math.Abs(CoreSim.PhysicalSpace.BikeAngles.Wrap(x))*180/Math.PI;
        foreach(var q in forecast.Points){
            var sample=native.Replay.Samples.MinBy(s=>Math.Abs(s.Input.Time-q.Time))!;var delta=Math.Abs(q.Time-sample.Input.Time);
            bool required=Math.Abs(q.Time/.01-Math.Round(q.Time/.01))<1e-6||Math.Abs(q.Time-forecast.Points[0].Time-.9)<1e-8;
            if(delta>1e-7){if(required)ok=false;errors.Add(new{q.Time,Compared=false,Required=required,Delta=delta});continue;}
            var v=sample.Input.Rider;var xy=Vector2.Distance(q.Rider.Position,v.Position);var speed=Math.Abs(q.Rider.Speed-v.Speed);
            var chi=Deg(Math.Atan2(q.Rider.Velocity.Y,q.Rider.Velocity.X)-Math.Atan2(v.Velocity.Y,v.Velocity.X));var psi=Deg(q.Rider.Heading-v.Heading);var beta=Deg(q.Rider.SlideRadians-v.SlideRadians);
            var steer=Deg(q.Steering-sample.Input.Steering);var omega=Math.Abs(q.Omega-sample.Input.Omega);var yaw=Math.Abs(q.Rider.AngularVelocity-v.AngularVelocity);var optimism=Math.Max(q.Inner-sample.Inner,q.Outer-sample.Outer);
            bool pass=xy<=.1&&speed<=.2&&chi<=1&&psi<=1&&beta<=1&&steer<=1.5&&omega<=2&&yaw<=.05&&optimism<=.1;
            ok&=pass;compared++;errors.Add(new{q.Time,Compared=true,Passed=pass,Required=required,XY=xy,Speed=speed,Chi=chi,Psi=psi,Beta=beta,Steering=steer,Omega=omega,Yaw=yaw,ClearanceOptimism=optimism});
        }
        passed=ok&&compared>=91;return new{Passed=passed,Compared=compared,Errors=errors,Scope="Full H09 only for held-after-sequence native probes. For main trials, restored baseline feedback after the planned actions intentionally differs; do not call that predictor error."};
    }
    public static void Run(string d,string raw,string stage){
        Directory.CreateDirectory(d);Directory.CreateDirectory(raw);
        if(stage=="freeze"){
            if(File.Exists(Path.Combine(d,"protocol.json")))throw new IOException("Do not overwrite frozen evidence.");
            Save(d,"protocol.json",new{BaseHead,PolicyId=16015,Policy=Policy(),Cases,Slots,Sources=Sources(),HorizonSeconds=.9,ReviewSeconds=.2,Reaction="RiderController.ReactionSeconds(spec), asserted .24 for test rider",
                BranchLimits=new[]{4,3,2},RootRule="A choice, best coast, best full, hold; distinct then maximum steering diversity fills to4",
                ContinuationRule="hold, nearest lower steering with opposite extreme gas, nearest upper steering with opposite extreme gas; third depth takes first2; original physical action grid only",
                Utility="Literal134A terminal utility and safety ordering; first-action preferences, original weights, normalized progress. No terminal-value learning. Stable action-key tie break.",
                Execution="One frozen model-only sequence requested at t,t+.2,t+.4. Thereafter original134A replans from native observations. Full original prefix; no snapshot approximation or future native reads.",
                Oracle="Sampled native upper bound only: A, model-chosen B/C, plus highest predicted progress nonduplicate triple. Never used to tune/select causal planner.",
                Limits=new{Worlds=96,PreflightCpuSeconds=200,FullCpuCeilingSeconds=1400,ContactEvaluations=25000000,Forecasts=250000,EpisodeCpuSeconds=30},
                CpuRule="After6 preflight worlds: min(1400,max(400,96*1.35*max measured world CPU)). Frozen formula; never increase after main results.",
                GoRule=new{DiagnosticStatesWithTwoBendGainAtLeast015=3,IndependentStatesWithGainAtLeast015=3,MinimumIndependentGeometries=2,NewCausalDNF=0,AllPreflightPredictionChecks=true,SearchCpuAtMostSeconds=.25,SearchContactsAtMost=15000},
                CheckPool="V cases remain unopened until diagnostic-complete lock; no method, branch, weight or threshold changes afterwards.",
                OriginDiscrepancy="Read-only35/27 versus agreed31/31; log separate restoration task, never alter coordinates in135.",RawDirectory=Path.GetFullPath(raw),RawAvailability="Full raw local only; selected bundle and hashes in Git."});return;
        }
        var protocol=Read<JsonElement>(d,"protocol.json");foreach(var source in protocol.GetProperty("Sources").EnumerateObject())if(Sources()[source.Name]!=source.Value.GetString())throw new InvalidOperationException("Frozen source changed: "+source.Name);
        var limit=File.Exists(Path.Combine(d,"budget-freeze.json"))?Read<JsonElement>(d,"budget-freeze.json").GetProperty("CpuLimitSeconds").GetDouble():200;
        using var budget=new RobustBudget(d,new Dictionary<string,int>{{"native",96},{"offline",60}},96,limit,25000000);
        if(stage=="preflight"){Preflight(d,raw,budget);return;}
        if(!Read<JsonElement>(d,"preflight.json").GetProperty("Passed").GetBoolean())throw new InvalidOperationException("Preflight gate closed.");
        if(stage=="diagnostic"){Suite(d,raw,budget,false);return;}
        if(stage=="confirm"){
            var seal=Read<JsonElement>(d,"diagnostic-complete.json");if(seal.GetProperty("SourceHash").GetString()!=TrackAwarenessExperiment134.Hash(Sources()))throw new InvalidOperationException("Method changed after diagnostic.");
            Suite(d,raw,budget,true);return;
        }
        throw new ArgumentException("freeze|preflight|diagnostic|confirm");
    }
    static void Preflight(string d,string raw,RobustBudget b){
        var old=RiderDecisionExperiment133.Historical(Historical,"final-134-A-progressive-c-N00");var c=TrackAwarenessExperiment134.FinalTwo[0];var baseline=Native(d,raw,b,"preflight-off134",c);
        bool off=baseline.Metrics.Native.Fingerprint==old.Metrics.Native.Fingerprint,repeat=false,predicted=true;var rows=new List<object>();
        var f=TrackAwarenessExperiment134.Frame(c.Geometry);var surface=Surface(f);var spec=FreeLearningExperiment.Rider(c.Scenario);var p=FreeLearningExperiment.Parameters();
        foreach(var time in new[]{0d,1.2}){
            var input=RiderDecisionAudit133.BeforeDecision(baseline.Replay.Samples.Single(s=>Math.Abs(s.Input.Time-time)<1e-8));
            var reservation=b.Reserve("preflight-search-"+time,"offline",false);var search=MultiActionPlanner135.Search(input,Policy(),spec,f,surface,p);b.Complete(reservation,search.CpuSeconds,search.Contacts,search.Forecasts);
            foreach(var (arm,leaf) in new[]{("B",search.BestTwo),("C",search.BestThree)}){
                var native=Native(d,raw,b,$"preflight-{arm}-{time}",c,time,leaf.Actions,time+.9,true);var accuracy=Accuracy(leaf.Prediction,native,out bool valid);predicted&=valid;
                var prefix=Prefix(baseline,native,time,RiderController.ReactionSeconds(spec));predicted&=prefix;
                rows.Add(new{Time=time,Arm=arm,Actions=leaf.Actions,Accuracy=accuracy,ExactPrefix=prefix,SearchCpu=search.CpuSeconds,SearchContacts=search.Contacts,SearchForecasts=search.Forecasts});
                if(time==1.2&&arm=="C"){var again=Native(d,raw,b,"preflight-repeat",c,time,leaf.Actions,time+.9,true);repeat=again.Metrics.Native.Fingerprint==native.Metrics.Native.Fingerprint;}
            }
        }
        var geometry=Cases.Take(3).Select(c=>{var frame=TrackAwarenessExperiment134.Frame(c.Geometry);Save(d,c.Geometry+"-geometry.json",ReplayGeometry.Create(frame));return new{c.Geometry,StartToBend=frame.SegmentLengthMeters(0),BackStraight=frame.SegmentLengthMeters(4),ClosingStraight=frame.SegmentLengthMeters(8),frame.LapMeters,Origin=frame.At(0,0).Position,
            MaximumFirstBendWidth=Enumerable.Range(0,1001).Max(i=>frame.Shape!.Width(frame.SegmentStartMeters(1)+i/1000d*(frame.SegmentStartMeters(4)-frame.SegmentStartMeters(1)))),
            MaximumSecondBendWidth=Enumerable.Range(0,1001).Max(i=>frame.Shape!.Width(frame.SegmentStartMeters(5)+i/1000d*(frame.SegmentStartMeters(8)-frame.SegmentStartMeters(5))))};}).ToArray();
        var reaction=RiderController.ReactionSeconds(spec);bool passed=off&&repeat&&predicted&&Math.Abs(reaction-.24)<1e-8&&geometry.All(g=>g.StartToBend==35&&g.ClosingStraight==27&&g.BackStraight==62);
        Save(d,"preflight.json",new{Passed=passed,Off134Exact=off,NativeRepeatExact=repeat,PredictionsPassed=predicted,ReactionSeconds=reaction,Activations=new[]{reaction,reaction+.2,reaction+.4},Checks=rows,Geometry=geometry,Discrepancy="Agreed start31/31, implemented35/27. Separate task documented; unchanged here."});
        var max=b.Entries.Where(e=>e.NativeWorlds==1).Max(e=>e.CpuSeconds);Save(d,"budget-freeze.json",new{CpuLimitSeconds=Math.Min(1400,Math.Max(400,96*1.35*max)),MeasuredMaximumNativeCpuSeconds=max,WorldLimit=96,ContactLimit=25000000,ForecastLimit=250000,BeforeMainWorlds=true});
        if(!passed)throw new InvalidOperationException("Preflight failed; preserve evidence, no full diagnostic suite.");
    }
    static State135[] States(AwarenessCase134 c,string[] slots,RobustEpisode run){
        var f=TrackAwarenessExperiment134.Frame(c.Geometry);var samples=run.Replay.Decisions.Select(x=>run.Replay.Samples.Single(s=>Math.Abs(s.Input.Time-x.Time)<1e-8)).Where(s=>Math.Min(s.Inner,s.Outer)>.05).ToArray();
        return slots.Select(slot=>{
            double reference=slot switch{"straight-end"=>f.SegmentStartMeters(1)-3,"entry1"=>f.SegmentStartMeters(1)+3,"middle1"=>(f.SegmentStartMeters(1)+f.SegmentStartMeters(4))/2,"exit1"=>f.SegmentStartMeters(4)-3,"straight2"=>(f.SegmentStartMeters(4)+f.SegmentStartMeters(5))/2,"preparation2"=>f.SegmentStartMeters(5)-8,"entry2"=>f.SegmentStartMeters(5)+3,_=>0};
            var s=slot=="near-boundary"?samples.MinBy(x=>Math.Min(x.Inner,x.Outer))!:slot=="maximum-yaw"?samples.MaxBy(x=>Math.Abs(x.Input.Rider.AngularVelocity))!:samples.MinBy(x=>Math.Abs(x.Input.Rider.TotalReferenceMeters-reference))!;
            if(reference>0&&Math.Abs(s.Input.Rider.TotalReferenceMeters-reference)>5)throw new InvalidOperationException("Requested phase unavailable; no substitute.");
            return new State135(c.Name+"-"+slot,c,slot,s.Input.Time,RiderDecisionAudit133.BeforeDecision(s));
        }).ToArray();
    }
    static void Suite(string d,string raw,RobustBudget b,bool confirm){
        var category=confirm?"confirm":"diagnostic";var records=new List<Pair135>();var states=new List<State135>();var searchRows=new List<object>();
        for(int ci=confirm?3:0;ci<(confirm?6:3);ci++){
            var c=Cases[ci];var baseline=Native(d,raw,b,"baseline-"+c.Name,c);var pool=States(c,Slots[ci],baseline);states.AddRange(pool);Save(d,category+"-states.json",states);
            var f=TrackAwarenessExperiment134.Frame(c.Geometry);var spec=FreeLearningExperiment.Rider(c.Scenario);var surface=Surface(f);var p=FreeLearningExperiment.Parameters();
            foreach(var state in pool){
                var reservation=b.Reserve("search-"+state.Name,"offline",false);var search=MultiActionPlanner135.Search(state.Input,Policy(),spec,f,surface,p);b.Complete(reservation,search.CpuSeconds,search.Contacts,search.Forecasts);
                var two=search.BestTwo;var three=search.BestThree;
                var oracle=search.Three.Where(x=>!x.Actions.SequenceEqual(three.Actions)&&!x.Actions.SequenceEqual(two.Actions)).OrderByDescending(x=>x.Evaluation.Progress).ThenBy(x=>x.Evaluation.Score).First();
                searchRows.Add(new{State=state.Name,Search=search,Selection="Model only, before native B/C/oracle trials",ChosenTwo=two.Actions,ChosenThree=three.Actions,OracleSample=oracle.Actions});Save(d,category+"-search.json",searchRows);
                foreach(var (arm,leaf) in new[]{("B",two),("C",three),("oracle-sample",oracle)}){
                    var name=state.Name+"-"+arm;var native=Native(d,raw,b,name,c,state.Time,leaf.Actions);var reaction=RiderController.ReactionSeconds(spec);var prefix=Prefix(baseline,native,state.Time,reaction);
                    var requests=leaf.Actions.Select((a,j)=>new{Action=a,Time=state.Time+j*.2}).All(q=>native.Replay.Decisions.Any(x=>Math.Abs(x.Time-q.Time)<1e-8&&x.Action==q.Action)
                        &&native.Replay.Activations.Any(x=>Math.Abs(x.ObservedAt-q.Time)<1e-8&&Math.Abs(x.ActivationAt-q.Time-reaction)<1e-8&&x.Action==q.Action));
                    // Only the first .2/.4 seconds after the final planned activation match held H09;
                    // feedback afterwards is deliberately autonomous again. Report the divergence.
                    var accuracy=Accuracy(leaf.Prediction,native,out _);
                    records.Add(new(state.Name,arm,name,leaf.Actions,leaf.Evaluation,baseline.Metrics,native.Metrics,prefix,requests,accuracy));Save(d,category+"-pairs.json",records);
                    if(!prefix||!requests)throw new InvalidOperationException("Physical prefix, request or reaction queue mismatch.");
                }
            }
        }
        Save(d,category+"-complete.json",new{SourceHash=TrackAwarenessExperiment134.Hash(Sources()),States=states.Count,Pairs=records.Count,Worlds=b.Entries.Sum(e=>e.NativeWorlds),NativeResultsNeverUsedForCausalChoice=true});
    }
}
