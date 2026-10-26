using System.Diagnostics;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

public sealed record YawState137(string Name,AwarenessCase134 Case,string Phase,double Time,FreeRacingInput Input);
public sealed record YawNative137(string Name,string Arm,string Mode,AwarenessCase134 Case,RobustMetrics Metrics,double SearchCpu,int Searches,long NativeContacts,long PredictionContacts,long Forecasts);
public static class CausalYawExperiment137
{
    public const string BaseHead="060d6819cbae1ab701f3439d74a6e6266230419a";
    public static readonly string Historical=PhysicalSteeringAudit.PreviousPath("../speed-throttle-root-cause");
    public static FreeLearningPolicy Policy=>MultiActionExperiment135.Policy();
    public static readonly string[] Arms={"A","B","C","D"};
    public static readonly AwarenessCase134[] Diagnostic=MultiActionExperiment135.Cases.Take(2).ToArray();
    public static readonly AwarenessCase134[] Selection={
        new("S-B","progressive-b",new("S",4,17.1f,5.8f,18.4,.095f,.015f,-.022f),true),
        new("S-C","progressive-c",new("S",4,17.1f,5.8f,18.4,.095f,.015f,-.022f),true)};
    public static readonly AwarenessCase134[] Confirmation=(from n in new[]{0,1} from geometry in new[]{"progressive-b","progressive-c"}
        select new AwarenessCase134($"V{n}-{geometry[^1]}",geometry,n==0?new("V0",4,15.65f,4.85f,18.85,-.0875f,-.0165f,.019f):new("V1",4,17.85f,6.35f,19.05,.0975f,.0185f,-.021f),true)).ToArray();
    public static readonly string[] Phases={"middle1","exit1-20","exit1-10","exit1-5","exit1","straight+10","preparation2","exit2-5","near-control"};
    static readonly JsonSerializerOptions Json=FreeLearningExperiment.Json;
    public static void Save(string d,string name,object value)=>RobustLearningExperiment.Save(d,name,value);
    public static T Read<T>(string d,string name)=>RobustLearningExperiment.Read<T>(d,name);
    public static void Zip(string d,string name,object value){using var stream=new GZipStream(File.Create(Path.Combine(d,name)),CompressionLevel.SmallestSize);JsonSerializer.Serialize(stream,value,Json);}
    public static Dictionary<string,string> Sources(){
        string Hash(string file)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(file).Replace("\r\n","\n"))));
        var result=Directory.GetFiles("tools/shared-world-speedway","*.cs").Order().ToDictionary(x=>Path.GetFileName(x)!,Hash);
        foreach(var name in new[]{"Program.cs","causal-yaw-throttle.csproj"})result["causal-yaw-throttle/"+name]=Hash("tools/causal-yaw-throttle/"+name);
        return result;
    }
    public static RobustEpisode HistoricalRun(AwarenessCase134 c)=>RobustLearningExperiment.ReadEpisode(Path.Combine(Historical,"native"),"baseline-"+c.Name);
    public static YawState137 State(AwarenessCase134 c,string phase){
        var run=HistoricalRun(c);var f=TrackAwarenessExperiment134.Frame(c.Geometry);
        if(phase.StartsWith("exit1-")||phase=="exit1"||phase=="exit2-5"){
            var corner=phase.StartsWith("exit2")?2:1;var meters=phase.Contains('-')?double.Parse(phase.Split('-')[1],System.Globalization.CultureInfo.InvariantCulture):0;
            var x=SpeedThrottleAudit136.Select(c,run,corner,meters);return new(c.Name+"-"+phase,c,phase,x.Time,x.Input);
        }
        var samples=run.Replay.Decisions.Select(d=>run.Replay.Samples.Single(s=>Math.Abs(s.Input.Time-d.Time)<1e-8)).ToArray();
        var target=phase=="middle1"?(f.SegmentStartMeters(1)+f.SegmentStartMeters(4))/2:phase=="straight+10"?f.SegmentStartMeters(4)+10:f.SegmentStartMeters(5)-8;
        var sample=phase=="near-control"?samples.MinBy(s=>Math.Min(s.Inner,s.Outer))!:samples.MinBy(s=>Math.Abs(s.Input.Rider.TotalReferenceMeters-target))!;
        return new(c.Name+"-"+phase,c,phase,sample.Input.Time,RiderDecisionAudit133.BeforeDecision(sample));
    }
    public static YawSearch137 Search(YawState137 state){var frame=TrackAwarenessExperiment134.Frame(state.Case.Geometry);
        return CausalYawPlanner137.Search(state.Input,Policy,FreeLearningExperiment.Rider(state.Case.Scenario),frame,MultiActionExperiment135.Surface(frame),FreeLearningExperiment.Parameters());}
    static YawNative137 Native(string d,string raw,RobustBudget budget,string name,AwarenessCase134 c,string arm,
        YawState137? intervention=null,FreeRacingAction[]? actions=null,double? duration=null,bool extend=false){
        if(File.Exists(Path.Combine(d,"work-"+name+".json")))return Read<YawNative137>(d,"work-"+name+".json");
        var f=TrackAwarenessExperiment134.Frame(c.Geometry);var surface=MultiActionExperiment135.Surface(f);var spec=FreeLearningExperiment.Rider(c.Scenario);var p=FreeLearningExperiment.Parameters();
        var logs=new List<object>();double searchCpu=0;int searches=0;
        FreeLearningDecision Choose(FreeRacingInput i){
            var started=Process.GetCurrentProcess().TotalProcessorTime;
            if(intervention is not null){
                var baseline=MultiActionPlanner135.Baseline(i,Policy,spec,f,surface,p);searchCpu+=(Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds;searches++;
                if(i.Time<intervention.Time-1e-8||i.Time>=intervention.Time+.9-1e-8)return baseline;
                var index=Math.Min(actions!.Length-1,(int)Math.Round((i.Time-intervention.Time)/.2));return baseline with{Action=actions[index]};
            }
            if(arm=="A"){
                var decision=MultiActionPlanner135.Baseline(i,Policy,spec,f,surface,p);var cpu=(Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds;searchCpu+=cpu;searches++;
                logs.Add(new{i.Time,Arm=arm,First=decision.Action,Second=(FreeRacingAction?)null,CpuSeconds=cpu,Contacts=decision.ForceEvaluations,decision.Forecasts,Roots=0,MaxCorrections=0});return decision;
            }
            var search=CausalYawPlanner137.Search(i,Policy,spec,f,surface,p,arm is "B" or "D");var leaf=search.Best[arm];searchCpu+=search.CpuSeconds;searches++;
            logs.Add(new{i.Time,Arm=arm,First=leaf.Actions[0],Second=leaf.Actions.Length>1?(FreeRacingAction?)leaf.Actions[1]:null,leaf.TerminalYawCost,leaf.MeanYawCost,leaf.ThrottleCost,leaf.SteeringCost,
                Score=leaf.Evaluation.Score,Safety=leaf.Evaluation.Safety.ToString(),BaselineFirst=search.Best["A"].Actions[0],BaselineScore=search.Best["A"].Evaluation.Score,
                CpuSeconds=search.CpuSeconds,Contacts=search.Contacts,search.Forecasts,search.Roots,MaxCorrections=search.MaximumCorrections,
                SecondObservation=leaf.SecondObservation,Execution="First request only; next native review replans. Second is a predicted contingent continuation, not an irrevocably prequeued native command."});
            return CausalYawPlanner137.Decision(i,search,arm);
        }
        var episode=CausalYawNative137.Execute(d,raw,budget,"native",name,16015,Policy,c.Scenario,shortSeconds:duration,twoBends:true,horizonSeconds:.9,
            normalizeProgress:true,fullThrottleCandidate:true,trackShape:TrackAwarenessExperiment134.Variant(c.Geometry),chooser:Choose,extendAfterSecondStraight:extend);
        Zip(d,"plans-"+name+".json.gz",logs);var scope=Read<JsonElement>(d,"scope-"+name+".json");
        var row=new YawNative137(name,arm,intervention is null?extend?"extended-autonomous":"autonomous":"controlled-H09-then-A",c,episode.Metrics,searchCpu,searches,
            scope.GetProperty("NativeForceQueries").GetInt64()+scope.GetProperty("NativeBoundaryQueries").GetInt64(),scope.GetProperty("PredictionContacts").GetInt64(),scope.GetProperty("PredictionForecasts").GetInt64());
        Save(d,"work-"+name+".json",row);return row;
    }
    static object Accuracy(FreeRacingPrediction prediction,RobustEpisode native,out bool pass){
        var samples=native.Replay.Samples;double xy=0,speed=0,heading=0,beta=0,omega=0,yaw=0,steering=0,clearance=0;int compared=0;
        foreach(var q in prediction.Points){
            var s=samples.MinBy(x=>Math.Abs(x.Input.Time-q.Time))!;
            if(Math.Abs(s.Input.Time-q.Time)>1e-7)continue;
            var v=s.Input.Rider;xy=Math.Max(xy,Vector2.Distance(v.Position,q.Rider.Position));speed=Math.Max(speed,Math.Abs(v.Speed-q.Rider.Speed));
            heading=Math.Max(heading,Math.Abs(BikeAngles.Wrap(v.Heading-q.Rider.Heading))*180/Math.PI);beta=Math.Max(beta,Math.Abs(BikeAngles.Wrap(v.SlideRadians-q.Rider.SlideRadians))*180/Math.PI);
            omega=Math.Max(omega,Math.Abs(s.Input.Omega-q.Omega));yaw=Math.Max(yaw,Math.Abs(v.AngularVelocity-q.Rider.AngularVelocity));steering=Math.Max(steering,Math.Abs(s.Input.Steering-q.Steering)*180/Math.PI);
            clearance=Math.Max(clearance,Math.Max(q.Inner-s.Inner,q.Outer-s.Outer));compared++;
        }
        pass=compared>=80&&xy<=.1&&speed<=.2&&heading<=1&&beta<=1&&omega<=2&&yaw<=.05&&steering<=1.5&&clearance<=.1;
        return new{Passed=pass,Compared=compared,XY=xy,Speed=speed,HeadingDegrees=heading,BetaDegrees=beta,Omega=omega,Yaw=yaw,SteeringDegrees=steering,ClearanceOptimism=clearance};
    }
    public static object[] Trace(YawState137 state,YawLeaf137 leaf){
        var f=TrackAwarenessExperiment134.Frame(state.Case.Geometry);var surface=MultiActionExperiment135.Surface(f);var spec=FreeLearningExperiment.Rider(state.Case.Scenario);var p=FreeLearningExperiment.Parameters();
        var commands=state.Input.Pending.Concat(leaf.Actions.Select((a,j)=>new FreeRacingCommand(state.Time+j*.2,state.Time+j*.2+.24,a))).OrderBy(x=>x.ActivationAt).ToArray();
        return leaf.Prediction.Points.Select(q=>{
            var active=commands.Where(x=>x.ActivationAt<=q.Time+1e-10).Select(x=>x.Action).LastOrDefault(state.Input.Active);var v=q.Rider with{Drive=active.Drive};
            var wheel=new RearWheelState(q.Omega*.33);var force=TwoContactBikeDynamics.Forces(v,spec,f,surface,p,q.Time,147,wheel,new(q.Steering),0,steeringOverride:active.SteeringRadians,inertia:52.72097f);
            var ahead=Math.Max(.1,f.ReferenceSpeed(v.Road,v.Speed)*Policy.PreviewSeconds);var preview=f.RoadAt(v.TotalReferenceMeters+ahead,v.Road.OffsetMeters);
            var desired=CausalYawPlanner137.DesiredYaw(v,Policy,f);var speed=Math.Max(.1,v.Speed);
            return (object)new{q.Time,Elapsed=q.Time-state.Time,YawRate=v.AngularVelocity,DesiredYaw=desired,YawError=v.AngularVelocity-desired,PreviewReference=v.TotalReferenceMeters+ahead,PreviewSegment=preview.Segment,PreviewRadius=double.IsFinite(preview.RadiusMeters)?preview.RadiusMeters:(double?)null,
                CurrentCurvature=v.Road.IsTurn&&double.IsFinite(v.Road.RadiusMeters)?1/v.Road.RadiusMeters:0,TravelCurvature=TwoContactForces.Cross(v.Velocity,force.NetForce)/(147*Math.Pow(speed,3)),
                BetaDegrees=v.SlideRadians*180/Math.PI,ChiDegrees=Math.Atan2(v.Velocity.Y,v.Velocity.X)*180/Math.PI,PsiDegrees=v.Heading*180/Math.PI,SpeedKph=v.Speed*3.6,
                Drive=active.Drive,SteeringDegrees=q.Steering*180/Math.PI,FrontFx=Vector2.Dot(force.FrontForce,force.BodyForward),FrontFy=Vector2.Dot(force.FrontForce,force.BodyLeft),RearFx=force.RearLongitudinal,RearFy=force.RearLateral,
                FrontUtilization=force.FrontForce.Length()/force.TyreLoads!.FrontCapacityNewtons,RearUtilization=force.RearForce.Length()/force.TyreLoads.RearCapacityNewtons,
                RearSlip=wheel.Prepared.InstantSlipRatio,Engine=force.Engine,Inner=q.Inner,Outer=q.Outer,
                Basis="Forecast state; original uncommitted instantaneous contact query dt0, not an applied native interval"};
        }).ToArray();
    }
    static void Offline(string d,RobustBudget budget){
        if(File.Exists(Path.Combine(d,"offline-complete.json")))return;
        var n=budget.Reserve("offline-abcd","offline",false);var cpu=Process.GetCurrentProcess().TotalProcessorTime;long contacts=0;int forecasts=0;var rows=new List<object>();
        foreach(var c in Diagnostic)foreach(var phase in Phases){
            var state=State(c,phase);var search=Search(state);contacts+=search.Contacts;forecasts+=search.Forecasts;
            var frame=TrackAwarenessExperiment134.Frame(c.Geometry);var original=MultiActionPlanner135.Baseline(state.Input,Policy,FreeLearningExperiment.Rider(c.Scenario),frame,MultiActionExperiment135.Surface(frame),FreeLearningExperiment.Parameters());
            contacts+=original.ForceEvaluations;forecasts+=original.Forecasts;
            if(search.Best["A"].Evaluation!=original.Candidates.Single(x=>x.Action==original.Action))throw new InvalidOperationException("A differs from frozen134 utility.");
            var referenceAngle=search.Best["A"].Actions[0].SteeringRadians;
            var traces=search.Held.Where(x=>x.Actions[0].SteeringRadians==referenceAngle).Select(x=>new{Role="same-steer-gas"+x.Actions[0].Drive.ToString("R",System.Globalization.CultureInfo.InvariantCulture),x.Actions,Points=Trace(state,x)})
                .Concat(new[]{"B","D"}.Select(arm=>new{Role=arm,Actions=search.Best[arm].Actions,Points=Trace(state,search.Best[arm])})).ToArray();contacts+=traces.Sum(x=>(long)x.Points.Length);
            Zip(d,"traces-"+state.Name+".json.gz",new{State=state,Traces=traces});
            Zip(d,"offline-"+state.Name+".json.gz",new{State=state,Search=search});rows.Add(new{State=state,Best=search.Best.ToDictionary(x=>x.Key,x=>new{x.Value.Actions,x.Value.Evaluation,x.Value.TerminalYawCost,x.Value.MeanYawCost,x.Value.ThrottleCost,x.Value.SteeringCost}),search.CpuSeconds,search.Contacts,search.Forecasts,search.Roots,search.MaximumCorrections});
        }
        budget.Complete(n,(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,contacts,forecasts);Save(d,"offline-complete.json",rows);
    }
    public static void Run(string d,string raw,string stage){
        Directory.CreateDirectory(d);Directory.CreateDirectory(raw);
        if(stage=="freeze"){
            if(File.Exists(Path.Combine(d,"protocol.json")))throw new IOException("Do not overwrite frozen evidence.");
            var states=Diagnostic.SelectMany(c=>Phases.Select(phase=>State(c,phase))).ToArray();
            Save(d,"states.json",states);Save(d,"protocol.json",new{BaseHead,PolicyId=16015,Policy,Diagnostic,Selection,Confirmation,Phases,Sources=Sources(),SourceNormalization="CRLF->LF only",
                ControlledPhases=new[]{"middle1","exit1-10","exit2-5"},Arms,RootLimit=CausalYawPlanner137.RootLimit,CorrectionLimit=CausalYawPlanner137.CorrectionLimit,
                Scoring="A frozen134. C replaces terminal yaw only by trapezoidal mean of identical geometry-preview yaw error over fullH09. B uses two commands and original terminal yaw. D combines both. All other terminal utility terms unchanged. Every planned command pays time-normalized throttle/steering cost over [.24,.9]; identical channel requests coalesce so held pairs retain original cost. No gas bonus or smaller-yaw hard guard.",
                CommonPrefix="The first new request cannot activate before.24; all roots share the original model's first.2s. Construct second predicted observation with original irrevocable pending plus first request, then evaluate bounded suffixes. Validate against monolithic causalPolicy AND native Box2D.",
                Execution="Controlled diagnostics hold the selected last command toH09 then restoreA. Autonomous B/C/D execute ONLY chosen first request and replan every.2s on then-current native observations; predicted second actions are not guaranteed to execute. No future native input, fixed line or forced gas. Offline oracle never selects autonomous actions.",
                Limits=new{Worlds=60,CpuSeconds=900,ContactEvaluations=30000000,Forecasts=300000,EpisodeCpuSeconds=30},
                Allocation="10preflight (2fullOFF+4H09probes+4shortcost),24controlled,8selection,8held,8full-second-straight,2egg-transfer=60max. Cached reads create no world. Stop before any world when reserve would exhaust CPU/contacts; never raise limits.",
                Preflight="Offline18states first;2exactA replicas;4fullH09 probes;4B/D autonomous2s cost probes. All queue/native comparisons must pass. Before main freeze CPU limits and GO thresholds from these measurements, never held outcomes.",
                Geometry="Existing progressive-b/c; corners end at SegmentStartMeters4/8. Retain35+27,62m straights, full footprint, beta45, ±35deg/90deg-s. Egg-asymmetric transfer optional reserved2worlds at sameV1initial after main.",
                Pools="Diagnostic D-B/D-C historical136; selectionS uses a different common physical start on both profiles; confirmationV0/V1 each both profiles unopened until winner and numericalGOlock. Four held initial/geometry cases, same conditions across all arms.",
                Finish="Keep old two-bend full-footprint+1s definition. Separate extended copies continue through actual next62m and can DNF after having completed historical scope; report both scopes and preserve exact common prefix.",RawDirectory=Path.GetFullPath(raw)});return;
        }
        foreach(var s in Read<JsonElement>(d,"protocol.json").GetProperty("Sources").EnumerateObject())if(Sources()[s.Name]!=s.Value.GetString())throw new InvalidOperationException("Frozen source changed: "+s.Name);
        using var budget=new RobustBudget(d,new Dictionary<string,int>{{"native",60},{"offline",80}},60,900,30000000);
        if(stage=="preflight"){Preflight(d,raw,budget);return;}
        if(!Read<JsonElement>(d,"preflight.json").GetProperty("Passed").GetBoolean())throw new InvalidOperationException("Preflight gate closed.");
        if(stage=="controlled"){Controlled(d,raw,budget);return;}
        if(stage=="selection"){Select(d,raw,budget);return;}
        if(stage=="confirm"){Confirm(d,raw,budget);return;}
        throw new ArgumentException("freeze|preflight|controlled|selection|confirm");
    }
    static void Preflight(string d,string raw,RobustBudget b){
        Offline(d,b);var checks=new List<object>();bool passed=true;
        foreach(var c in Diagnostic){
            var a=Native(d,raw,b,"off-"+c.Name,c,"A");var equal=a.Metrics.Native.Fingerprint==HistoricalRun(c).Metrics.Native.Fingerprint;passed&=equal;checks.Add(new{Case=c.Name,Off136Exact=equal});
            foreach(var phase in new[]{"middle1","exit1-10"}){
                var started=Process.GetCurrentProcess().TotalProcessorTime;var state=State(c,phase);var search=Search(state);var options=search.Pairs.Where(x=>x.Actions[0].Drive==1&&x.Evaluation.Safety==FreeActionSafety.SafeInHorizon).ToArray();
                var leaf=options.Length>0?CausalYawPlanner137.Rank(options,state.Input).First():search.Best["B"];
                var frame=TrackAwarenessExperiment134.Frame(c.Geometry);FreeRacingInput? observed=null;
                var reference=CausalYawPlanner137.ReferencePair(state.Input,leaf.Actions,FreeLearningExperiment.Rider(c.Scenario),frame,MultiActionExperiment135.Surface(frame),FreeLearningExperiment.Parameters(),q=>observed=q);
                var cachedMax=leaf.Prediction.Points.Max(q=>{var point=reference.Points.MinBy(r=>Math.Abs(r.Time-q.Time))!;return (double)Vector2.Distance(q.Rider.Position,point.Rider.Position);});
                bool queue=observed is not null&&observed.Pending.SequenceEqual(leaf.SecondObservation!.Pending)&&observed.Active==leaf.SecondObservation.Active;
                var n=b.Reserve("parity-search-"+state.Name,"offline",false);b.Complete(n,(Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds,search.Contacts+reference.ForceEvaluations,search.Forecasts+1);
                Save(d,"probe-plan-"+state.Name+".json",new{state,leaf.Actions,leaf.Evaluation,ExpectedActivations=new[]{state.Time+.24,state.Time+.44},CachedVsCausalPolicyMaximumXY=cachedMax,ExactPredictedPending=queue});
                var row=Native(d,raw,b,"probe-"+state.Name,c,"B",state,leaf.Actions,state.Time+.9);var native=RobustLearningExperiment.ReadEpisode(raw,row.Name);
                var accuracy=Accuracy(leaf.Prediction,native,out var ok);var prefix=MultiActionExperiment135.Prefix(HistoricalRun(c),native,state.Time,.24);passed&=ok&&prefix&&queue&&cachedMax<=.001;
                checks.Add(new{State=state.Name,Accuracy=accuracy,ExactPrefix=prefix,ExactPredictedPending=queue,CachedVsCallbackXY=cachedMax,Native=row});
            }
            foreach(var arm in new[]{"B","D"})Native(d,raw,b,"cost-"+c.Name+"-"+arm,c,arm,duration:2);
        }
        var works=Directory.GetFiles(d,"work-*.json").Select(p=>Read<YawNative137>(d,Path.GetFileName(p))).ToArray();
        var baselineMean=works.Where(x=>x.Arm=="A").Average(x=>x.SearchCpu/x.Searches);
        var newMean=works.Where(x=>(x.Arm is "B" or "D")&&x.Mode=="autonomous").Select(x=>x.SearchCpu/Math.Max(1,x.Searches)).DefaultIfEmpty(.2).Max();
        var projected=20/.2*newMean+3;
        // Numeric gates fixed here, before any selection/final output. A speed/time gain
        // is never success if reliability or this measured cost feasibility fails.
        Save(d,"thresholds.json",new{BaselineMeanDecisionCpu=baselineMean,NewPreflightMaximumMeanDecisionCpu=newMean,Projected20SecondEpisodeCpu=projected,
            TimeGainSeconds=.15,ExitSpeedGainKph=1d,StraightSpeedGainKph=2d,MinimumHeldCasesImproved=3,MinimumGeometries=2,NewDnf=0,
            MaximumMeanDecisionCpu=Math.Min(.2,1.75*baselineMean),MaximumFourRiderSearchCpuPerSimSecond=4d,
            Rule="Require >=3 of4held cases improve full time>=.15s, geometric exit>=1kmh and shared straight sample>=2kmh, with two geometries and no newDNF. Mean decision CPU<=min(.2,1.75*Apreflight) and20decisions/s<=4CPU-s/s; excludes native/traffic, so no production readiness claim.",BeforeSelectionAndConfirmation=true});
        passed&=projected<30&&b.Entries.Sum(x=>x.CpuSeconds)<200;
        Save(d,"preflight.json",new{Passed=passed,Checks=checks,ThresholdsFrozen=true,EpisodeCostFeasible=projected<30});
        if(!passed)throw new InvalidOperationException("Prediction/queue/cost preflight failed; stop new worlds and preserve evidence.");
    }
    static void Controlled(string d,string raw,RobustBudget b){
        var rows=new List<object>();
        foreach(var c in Diagnostic)foreach(var phase in new[]{"middle1","exit1-10","exit2-5"}){
            var state=State(c,phase);var search=Search(state);var n=b.Reserve("controlled-search-"+state.Name,"offline",false);b.Complete(n,search.CpuSeconds,search.Contacts,search.Forecasts);
            Save(d,"controlled-plan-"+state.Name+".json",new{state,Best=search.Best});
            foreach(var arm in Arms){
                var leaf=search.Best[arm];var row=Native(d,raw,b,"control-"+state.Name+"-"+arm,c,arm,state,leaf.Actions);var native=RobustLearningExperiment.ReadEpisode(raw,row.Name);
                var prefix=MultiActionExperiment135.Prefix(HistoricalRun(c),native,state.Time,.24);var accuracy=Accuracy(leaf.Prediction,native,out var ok);
                rows.Add(new{state.Name,Arm=arm,Native=row,ExactPrefix=prefix,Accuracy=accuracy,FiniteContinuationConfirmed=native.Metrics.Native.Completed,SelectedActions=leaf.Actions});Save(d,"controlled.json",rows);
                if(!prefix||native.Metrics.Native.DurationSeconds>=state.Time+.9-1e-8&&!ok)throw new InvalidOperationException("Controlled H09 diverges from native; no further worlds.");
            }
        }
        Save(d,"controlled-complete.json",new{Pairs=rows.Count,SourceHash=TrackAwarenessExperiment134.Hash(Sources())});
    }
    static void Select(string d,string raw,RobustBudget b){
        if(!File.Exists(Path.Combine(d,"controlled-complete.json")))throw new InvalidOperationException("Controlled gate closed.");
        var rows=new List<YawNative137>();foreach(var c in Selection)foreach(var arm in Arms){rows.Add(Native(d,raw,b,"select-"+c.Name+"-"+arm,c,arm));Save(d,"selection.json",rows);}
        var winner=Arms.Skip(1).OrderByDescending(a=>rows.Count(x=>x.Arm==a&&x.Metrics.Native.Completed)).ThenBy(a=>rows.Where(x=>x.Arm==a).All(x=>x.Metrics.Native.Completed)?rows.Where(x=>x.Arm==a).Average(x=>x.Metrics.Native.GateSeconds!.Value):double.PositiveInfinity)
            .ThenBy(a=>rows.Where(x=>x.Arm==a).Average(x=>x.SearchCpu/Math.Max(1,x.Searches))).ThenBy(x=>x,StringComparer.Ordinal).First();
        Save(d,"selection-lock.json",new{Winner=winner,Sources=Sources(),Thresholds=Read<JsonElement>(d,"thresholds.json"),ConfirmationNotOpened=true,Rule="Reliability first, mean legal two-bend time, measured mean search CPU, ordinal arm. Never force B/D to win over C."});
    }
    static void Confirm(string d,string raw,RobustBudget b){
        var winner=Read<JsonElement>(d,"selection-lock.json").GetProperty("Winner").GetString()!;var rows=new List<YawNative137>();
        foreach(var c in Confirmation)foreach(var arm in new[]{"A",winner}){
            rows.Add(Native(d,raw,b,"final-"+c.Name+"-"+arm,c,arm));Save(d,"confirmation.json",rows);
        }
        foreach(var c in Confirmation)foreach(var arm in new[]{"A",winner}){
            var row=Native(d,raw,b,"extended-"+c.Name+"-"+arm,c,arm,extend:true);rows.Add(row);Save(d,"confirmation-and-extensions.json",rows);
            var standard=RobustLearningExperiment.ReadEpisode(raw,"final-"+c.Name+"-"+arm);var ext=RobustLearningExperiment.ReadEpisode(raw,row.Name);
            if(!MultiActionExperiment135.Prefix(standard,ext,standard.Metrics.Native.DurationSeconds,0))throw new InvalidOperationException("Extended capture changed historical prefix.");
        }
        var transfer=Confirmation[^1] with{Name="T-E",Geometry="egg-asymmetric"};foreach(var arm in new[]{"A",winner}){rows.Add(Native(d,raw,b,"transfer-"+arm,transfer,arm));Save(d,"transfer.json",rows.Where(x=>x.Case.Name=="T-E").ToArray());}
        Save(d,"complete.json",new{Winner=winner,Worlds=b.Entries.Sum(x=>x.NativeWorlds),CpuSeconds=b.Entries.Sum(x=>x.CpuSeconds),Contacts=b.Entries.Sum(x=>x.Evaluations),Forecasts=b.Entries.Sum(x=>x.Forecasts),AllReservationsComplete=b.Entries.All(x=>x.Complete)});
    }
}
