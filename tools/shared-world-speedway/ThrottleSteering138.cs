using System.Diagnostics;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CoreSim;

namespace SharedWorldSpeedway;

public sealed record FactorCase138(string Name,string Pool,AwarenessCase134 Case,int Corner,double BeforeExit,string BaselineDirectory,string BaselineName);
public sealed record SteeringPlan138(string Name,float First,float Second);
public sealed record FactorLeaf138(SteeringPlan138 Plan,float Drive,FreeRacingAction[] Actions,FreeLearningCandidate Evaluation,FreeRacingPrediction Prediction,double YawCost);
public sealed record FactorState138(FactorCase138 Definition,FreeRacingInput Input,double ActualBeforeExit,SteeringPlan138 Hold);
public sealed record FactorResult138(string Name,string Arm,string Pool,FactorState138 State,SteeringPlan138 Plan,float Drive,RobustMetrics Metrics,bool ExactPrefix,bool ExactObservation,object Accuracy);

/// <summary>Opt-in controlled interventions, never a production rider or native-outcome selector.
/// The only dynamics/guards/queue are the unchanged #137 collector and WorldRunner.</summary>
public static class ThrottleSteering138
{
    public const string BaseHead="53e2c00de383fa51930d8a3888b296ca4f7e2b16";
    public const double RequestWindow=1.6,GasRequestDelay=.2,GasActivation=.44;
    public static FreeLearningPolicy Policy=>CausalYawExperiment137.Policy;
    public static string Old=>"docs/experiments/causal-yaw-throttle";
    static readonly JsonSerializerOptions Json=FreeLearningExperiment.Json;
    public static void Save(string d,string n,object v)=>RobustLearningExperiment.Save(d,n,v);
    public static T Read<T>(string d,string n)=>RobustLearningExperiment.Read<T>(d,n);
    public static T Unzip<T>(string path){using var z=new GZipStream(File.OpenRead(path),CompressionMode.Decompress);return JsonSerializer.Deserialize<T>(z,Json)!;}
    public static void Zip(string path,object v){Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var z=new GZipStream(File.Create(path),CompressionLevel.SmallestSize);JsonSerializer.Serialize(z,v,Json);}
    public static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    public static Dictionary<string,string> Sources()=>Directory.GetFiles("tools/shared-world-speedway","*.cs").Concat(Directory.GetFiles("tools/throttle-steering-decoupling","*.cs")).Order().ToDictionary(x=>x.Replace('\\','/'),x=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(x).Replace("\r\n","\n")))));
    public static FactorCase138[] Cases(){
        var d=CausalYawExperiment137.Diagnostic;var s=CausalYawExperiment137.Selection;var h=CausalYawExperiment137.Confirmation;
        return new[]{new FactorCase138("D0","diagnostic",d[0],1,20,CausalYawExperiment137.Historical+"/native","baseline-"+d[0].Name),
            new("D1","diagnostic",d[1],2,15,CausalYawExperiment137.Historical+"/native","baseline-"+d[1].Name),
            new("S0","selection",s[0],1,15,Old+"/native","select-"+s[0].Name+"-A"),
            new("S1","selection",s[1],2,20,Old+"/native","select-"+s[1].Name+"-A")}
            .Concat(h.Select((c,j)=>new FactorCase138("H"+j,"confirmation",c,j<2?1:2,15,Old+"/native","extended-"+c.Name+"-A"))).ToArray();
    }
    public static RobustEpisode Baseline(FactorCase138 c)=>RobustLearningExperiment.ReadEpisode(c.BaselineDirectory,c.BaselineName);
    public static FactorState138 State(FactorCase138 c){
        var run=Baseline(c);var f=TrackAwarenessExperiment134.Frame(c.Case.Geometry);var end=SpeedThrottleAudit136.Exit(f,c.Corner);
        // Last observed review at or before the requested mark; never interpolate/reset a native state.
        var sample=run.Replay.Decisions.Select(d=>run.Replay.Samples.Single(s=>Math.Abs(s.Input.Time-d.Time)<1e-8))
            .Where(s=>s.Input.Rider.TotalReferenceMeters<=end-c.BeforeExit).Last();
        var input=RiderDecisionAudit133.BeforeDecision(sample);
        return new(c,input,end-input.Rider.TotalReferenceMeters,new("hold-active",input.Active.SteeringRadians,input.Active.SteeringRadians));
    }
    public static SteeringPlan138[] Plans(FreeRacingInput input){
        var a=input.Active.SteeringRadians;var sign=a!=0?Math.Sign(a):1;
        float Angle(double x)=>(float)Math.Clamp(x,-TwoContactBikeParameters.Default.MaximumSteeringRadians,TwoContactBikeParameters.Default.MaximumSteeringRadians);
        var small=Angle(a-sign*.5*Math.PI/180);var large=Angle(a-sign*1.5*Math.PI/180);
        return new[]{new SteeringPlan138("hold-active",a,a),new("reduce-small",a,small),new("reduce-large",a,large),
            new("increase",a,Angle(a+sign*1.5*Math.PI/180)),new("through-zero",a,0),new("opposite",a,Angle(-sign*.5*Math.PI/180)),
            new("early-prepare",large,large)}.DistinctBy(x=>(x.First,x.Second)).ToArray();
    }
    public static FreeRacingAction Action(FreeRacingInput input,SteeringPlan138 plan,float drive,int review)=>new(review==0?plan.First:plan.Second,review==0?input.Active.Drive:drive);
    public static FactorLeaf138 Forecast(FactorState138 state,SteeringPlan138 plan,float drive){
        var c=state.Definition.Case;var f=TrackAwarenessExperiment134.Frame(c.Geometry);var surface=MultiActionExperiment135.Surface(f);var spec=FreeLearningExperiment.Rider(c.Scenario);
        var actions=new[]{Action(state.Input,plan,drive,0),Action(state.Input,plan,drive,1)};
        var prediction=CausalYawPlanner137.ReferencePair(state.Input,actions,spec,f,surface,FreeLearningExperiment.Parameters());
        var leaf=CausalYawPlanner137.Evaluate(state.Input,Policy,f,surface,prediction,actions,false);
        return new(plan,drive,actions,leaf.Evaluation,prediction,leaf.TerminalYawCost);
    }
    public static IOrderedEnumerable<FactorLeaf138> Rank(IEnumerable<FactorLeaf138> leaves)=>leaves.OrderBy(x=>x.Evaluation.Safety).ThenBy(x=>x.Evaluation.Score).ThenBy(x=>x.Plan.Name,StringComparer.Ordinal);
    static bool Same(FreeRacingInput a,FreeRacingInput b)=>a.Rider==b.Rider&&a.Time==b.Time&&a.Omega==b.Omega&&a.Steering==b.Steering&&a.Active==b.Active&&a.Pending.SequenceEqual(b.Pending)&&Math.Abs(a.NextReview-b.NextReview)<1e-8;
    static FactorResult138 Native(string d,string raw,RobustBudget budget,FactorState138 state,string arm,SteeringPlan138 plan,float drive,double? duration=null,string? nameOverride=null){
        var name=nameOverride??state.Definition.Name+"-"+arm;var rowFile="result-"+name+".json";
        if(File.Exists(Path.Combine(d,rowFile)))return Read<FactorResult138>(d,rowFile);
        var predictionFile="accuracy-prediction-"+name+".json.gz";FactorLeaf138 forecast;
        if(File.Exists(Path.Combine(d,predictionFile)))forecast=Unzip<FactorLeaf138>(Path.Combine(d,predictionFile));
        else {var reservation=budget.Reserve("accuracy-prediction-"+name,"offline",false);var started=Process.GetCurrentProcess().TotalProcessorTime;
            forecast=Forecast(state,plan,drive);budget.Complete(reservation,(Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds,forecast.Prediction.ForceEvaluations,1);Zip(Path.Combine(d,predictionFile),forecast);}
        var c=state.Definition.Case;var f=TrackAwarenessExperiment134.Frame(c.Geometry);var surface=MultiActionExperiment135.Surface(f);var spec=FreeLearningExperiment.Rider(c.Scenario);var p=FreeLearningExperiment.Parameters();bool exact=false;
        FreeLearningDecision Choose(FreeRacingInput i){
            var old=MultiActionPlanner135.Baseline(i,Policy,spec,f,surface,p);
            if(Math.Abs(i.Time-state.Input.Time)<1e-8)exact=Same(state.Input,i);
            if(i.Time<state.Input.Time-1e-8||i.Time>=state.Input.Time+RequestWindow-1e-8)return old;
            var index=(int)Math.Round((i.Time-state.Input.Time)/.2);
            return old with{Action=Action(state.Input,plan,drive,index)};
        }
        var run=CausalYawNative137.Execute(d,raw,budget,"native",name,16015,Policy,c.Scenario,shortSeconds:duration,twoBends:true,horizonSeconds:.9,normalizeProgress:true,
            fullThrottleCandidate:true,trackShape:TrackAwarenessExperiment134.Variant(c.Geometry),chooser:Choose,extendAfterSecondStraight:duration is null);
        var prefix=MultiActionExperiment135.Prefix(Baseline(state.Definition),run,state.Input.Time,.24);
        // Reading a previously completed reservation never silently reruns its world.
        if(!exact)exact=Same(state.Input,RiderDecisionAudit133.BeforeDecision(run.Replay.Samples.Single(s=>Math.Abs(s.Input.Time-state.Input.Time)<1e-8)));
        var accuracy=MultiActionExperiment135.Accuracy(forecast.Prediction,run,out var pass);
        // A DNF inside H09 intentionally truncates the native trace; its unexecuted forecast is not evidence.
        if(run.Replay.Samples[^1].Input.Time>=state.Input.Time+.9-1e-8&&!pass)throw new InvalidOperationException("Native/prediction parity failed.");
        var row=new FactorResult138(name,arm,state.Definition.Pool,state,plan,drive,run.Metrics,prefix,exact,accuracy);Save(d,rowFile,row);
        if(!prefix||!exact)throw new InvalidOperationException("Native prefix or initial pending observation differs.");return row;
    }
    static void Existing18(string d){
        var facts=Read<JsonElement[]>(Old,"same-first-full-comparison.json");
        Save(d,"existing-18.json",new{NewNativeWorlds=0,Count=facts.Length,ZeroCorrections=facts.Count(x=>x.GetProperty("Second").GetProperty("Drive").GetSingle()==0),
            PartialCorrections=facts.Count(x=>x.GetProperty("Second").GetProperty("Drive").GetSingle() is >0 and <1),
            FullCorrections=facts.Count(x=>x.GetProperty("Second").GetProperty("Drive").GetSingle()==1),
            SameSafety=facts.Count(x=>x.GetProperty("HeldSafety").GetInt32()==x.GetProperty("CorrectedSafety").GetInt32()),
            MeanSpeedCostKph=facts.Average(x=>x.GetProperty("HeldSpeedKph").GetDouble()-x.GetProperty("CorrectedSpeedKph").GetDouble()),
            Interpretation="Same H09 safety in all 18; the objective ranks different yaw/direction/progress/throttle/steering outcomes. This does not establish that gas0 is physically necessary or that gas1 has a legal long continuation.",Rows=facts});
    }
    static void Offline18(string d,RobustBudget budget){
        if(File.Exists(Path.Combine(d,"offline-18-complete.json")))return;
        var reserve=budget.Reserve("offline-18","offline",false);var started=Process.GetCurrentProcess().TotalProcessorTime;long contacts=0,forecasts=0;var rows=new List<object>();
        foreach(var file in Directory.GetFiles(Old,"offline-*.json.gz").Order()){
            var data=Unzip<JsonElement>(file);var state=data.GetProperty("State").Deserialize<YawState137>(Json)!;var search=data.GetProperty("Search").Deserialize<YawSearch137>(Json)!;
            var group=search.Pairs.Where(x=>x.Actions[0].Drive==1).GroupBy(x=>x.Actions[0]).Single();var old=CausalYawPlanner137.Rank(group,state.Input).First();var f=TrackAwarenessExperiment134.Frame(state.Case.Geometry);
            var spec=FreeLearningExperiment.Rider(state.Case.Scenario);var surface=MultiActionExperiment135.Surface(f);var p=FreeLearningExperiment.Parameters();
            var prefix=FreeRacingModel.Predict(state.Input,spec,f,surface,p,147,52.72097f,.2,state.Input.Active);contacts+=prefix.ForceEvaluations;forecasts++;
            var next=CausalYawPlanner137.AfterPrefix(state.Input,prefix,group.Key,spec);var leaves=new List<YawLeaf137>();
            foreach(var angle in Plans(next).SelectMany(x=>new[]{x.First,x.Second}).Append(old.Actions[1].SteeringRadians).Distinct())foreach(var gas in new[]{1f,(float)Policy.Drive,0f}){
                var action=new FreeRacingAction(angle,gas);var suffix=FreeRacingModel.Predict(next,spec,f,surface,p,147,52.72097f,.7,action);contacts+=suffix.ForceEvaluations;forecasts++;
                leaves.Add(CausalYawPlanner137.Evaluate(state.Input,Policy,f,surface,CausalYawPlanner137.Join(prefix,suffix),new[]{group.Key,action},false,next));
            }
            var fixedGas=CausalYawPlanner137.Rank(leaves.Where(x=>x.Actions[1].Drive==1),state.Input).First();
            // Variable throttle is searched only after choosing the full-gas steering correction.
            var variable=CausalYawPlanner137.Rank(leaves.Where(x=>x.Actions[1].SteeringRadians==fixedGas.Actions[1].SteeringRadians),state.Input).First();
            object Row(YawLeaf137 x)=>new{x.Actions,x.Evaluation,x.TerminalYawCost,x.MeanYawCost,SpeedKph=x.Prediction.Points[^1].Rider.Speed*3.6,MaxBeta=x.Prediction.Points.Max(q=>Math.Abs(q.Rider.SlideRadians))*180/Math.PI};
            rows.Add(new{state.Name,Original137=Row(old),FixedGas138=Row(fixedGas),VariableGas138=Row(variable),Held=Row(search.Held.Single(x=>x.Actions[0]==group.Key)),Basis="Forecast only; identical native observation/irrevocable pending and first full action. No native outcome selection."});
        }
        budget.Complete(reserve,(Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds,contacts,forecasts);Save(d,"offline-18-complete.json",rows);
    }
    public static void Run(string d,string raw,string stage){
        Directory.CreateDirectory(d);Directory.CreateDirectory(raw);
        if(stage=="freeze"){
            if(File.Exists(Path.Combine(d,"protocol.json")))throw new IOException("Do not overwrite a frozen experiment.");Existing18(d);
            var states=Cases().Select(State).ToArray();Save(d,"states.json",states);
            Save(d,"protocol.json",new{BaseHead,PolicyId=16015,Policy,Sources=Sources(),SourceNormalization="CRLF->LF only",Cases=Cases(),
                Arms=new[]{"A full/hold","B full/corrected","C partial/hold","D partial/same correction as B"},
                Candidates="At most 7 deterministic target pairs: hold, reduce .5deg, reduce1.5deg, increase1.5deg, zero, opposite .5deg, early preparation at t0. Clamp targets only at original +/-35deg; original90deg/s actuator integrates them.",
                RequestWindow,GasRequestDelay,GasActivation,ReactionSeconds=.24,
                Timing="At t0 request original active gas and preparation steering. At t+.2 request target gas and second steering; repeated to t+1.4. Factorial target gas first activates at+.44; baseline A resumes requests at+1.6 and activates at+1.84. All old pending remain. A/C identical requested angles; B/D identical requested angles. Window refers to new requested/active commands, never immediate gas at t0.",
                Choice="Choose B solely by original #137 terminal score/safety over H09 forecast from initial observation; no native future. D reuses B angles exactly. Native2x2 is controlled offline, not autonomous. Retrospective winners are sampled oracle only.",
                LongContinuation="One continuous native world from original start through2bends and next62m; restore frozen134A requests after intervention. Preserve old whole-footprint gate+1s separately. No state/wheel/pending reset.",
                Pools="D0/D1 diagnostic, S0/S1 selection, H0..H3 confirmation held until lock; H0/H1 shareV0 and H2/H3 shareV1 initial families across B/C and are not independent geometry confirmations if executed trajectories coincide.",
                Limits=new{NativeWorlds=48,EpisodeAndSearchCpuSeconds=650,Contacts=20000000,Forecasts=250000,PerEpisodeCpuSeconds=30},
                Allocation="4preflight(2originalA parity +2H09B/D probes),32factorial(8states*4arms). Reserve12conditional autonomous worlds; optional egg quartet only if autonomous gate closed. Never increase limits or retry incomplete reservations.",
                PrototypeGate="Before confirmation: B must complete long scope and beat D by>=2km/h at studied exit and>=.10s at same historical gate in both diagnostic/selection physical families, with no new DNF vsD. Otherwise no autonomous prototype. A finite failure grid is not global physical impossibility.",
                PhysicsFeasibility="On at least3 physically distinct baseline families: corrected full B legal through extended scope, active full gas at studied geometric exit, exit>=D+2km/h, historical time<=D-.10s, positive certified clearance and beta<=45. Duplicate B/C trajectories count once. No beta/yaw-zero objective.",
                AutonomousGo="Only conditional prototype: same3independent-family speed/time/legal gates; zero new DNF vs frozen134A and137B, mean search<=.163482CPU-s/review, complete<=650CPU-s/48worlds. No deployment in thisPR.",
                BetaMeaning="45deg is the inherited experimental control envelope, NOT a validated real-motorcycle fall threshold.",
                ThresholdsFrozenBeforeNewNativeWorlds=true});
            foreach(var geometry in new[]{"progressive-b","progressive-c","egg-asymmetric"}){
                var frame=TrackAwarenessExperiment134.Frame(geometry);File.WriteAllText(Path.Combine(d,geometry+"-geometry.json"),JsonSerializer.Serialize(ReplayGeometry.Create(frame),ReplayGeometry.Json));
            }return;
        }
        var protocol=Read<JsonElement>(d,"protocol.json");foreach(var x in protocol.GetProperty("Sources").EnumerateObject()){
            var now=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(x.Name).Replace("\r\n","\n"))));if(now!=x.Value.GetString())throw new IOException("Frozen source changed: "+x.Name);
        }
        using var budget=new RobustBudget(d,new Dictionary<string,int>{{"native",48},{"offline",80}},48,650,20000000);
        if(budget.Entries.Any(x=>!x.Complete))throw new IOException("Incomplete attempt: preserve evidence, never rerun.");
        if(stage=="offline"){Offline18(d,budget);return;}
        var all=Read<FactorState138[]>(d,"states.json");
        if(stage=="preflight"){
            var checks=new List<object>();foreach(var state in all.Take(2)){
                var c=state.Definition.Case;var name="off-"+state.Definition.Name;var f=TrackAwarenessExperiment134.Frame(c.Geometry);var surface=MultiActionExperiment135.Surface(f);var spec=FreeLearningExperiment.Rider(c.Scenario);
                var run=CausalYawNative137.Execute(d,raw,budget,"native",name,16015,Policy,c.Scenario,twoBends:true,horizonSeconds:.9,normalizeProgress:true,fullThrottleCandidate:true,
                    trackShape:TrackAwarenessExperiment134.Variant(c.Geometry),chooser:i=>MultiActionPlanner135.Baseline(i,Policy,spec,f,surface,FreeLearningExperiment.Parameters()));
                var original=Baseline(state.Definition);var same=run.Replay.Metrics.Fingerprint==original.Replay.Metrics.Fingerprint;
                checks.Add(new{Name=name,OffExact=same,Cpu=run.Metrics.Native.CpuSeconds});if(!same)throw new InvalidOperationException("OFF parity failed.");
            }
            var first=all[0];var r=budget.Reserve("preflight-steering-search","offline",false);var started=Process.GetCurrentProcess().TotalProcessorTime;
            var leaves=Plans(first.Input).Select(x=>Forecast(first,x,1)).ToArray();var selected=Rank(leaves).First();
            budget.Complete(r,(Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds,leaves.Sum(x=>(long)x.Prediction.ForceEvaluations),leaves.Length);
            foreach(var arm in new[]{"B","D"})checks.Add(Native(d,raw,budget,first,arm,selected.Plan,arm=="B"?1:(float)Policy.Drive,first.Input.Time+.9,"probe-"+arm));
            Save(d,"preflight.json",new{Passed=true,Checks=checks,Worlds=budget.Entries.Sum(x=>x.NativeWorlds),CpuSeconds=budget.Entries.Sum(x=>x.CpuSeconds),BudgetRetained=true});return;
        }
        if(stage is "diagnostic" or "selection" or "confirmation"){
            if(!Read<JsonElement>(d,"preflight.json").GetProperty("Passed").GetBoolean())throw new InvalidOperationException("Preflight required.");
            if(stage=="confirmation"&&!File.Exists(Path.Combine(d,"selection-lock.json")))throw new InvalidOperationException("Lock before opening confirmation.");
            foreach(var state in all.Where(x=>x.Definition.Pool==stage)){
                var file="forecast-"+state.Definition.Name+".json.gz";FactorLeaf138[] leaves;
                if(File.Exists(Path.Combine(d,file)))leaves=Unzip<FactorLeaf138[]>(Path.Combine(d,file));
                else {var r=budget.Reserve("forecast-"+state.Definition.Name,"offline",false);var start=Process.GetCurrentProcess().TotalProcessorTime;
                    leaves=Plans(state.Input).SelectMany(plan=>new[]{Forecast(state,plan,1),Forecast(state,plan,(float)Policy.Drive)}).ToArray();
                    budget.Complete(r,(Process.GetCurrentProcess().TotalProcessorTime-start).TotalSeconds,leaves.Sum(x=>(long)x.Prediction.ForceEvaluations),leaves.Length);Zip(Path.Combine(d,file),leaves);}
                var best=Rank(leaves.Where(x=>x.Drive==1)).First();Save(d,"choice-"+state.Definition.Name+".json",new{Chosen=best.Plan,Ranking="Initial forecast only; no native outcomes",ChosenBeforeNative=true});
                foreach(var arm in new[]{"A","B","C","D"})Native(d,raw,budget,state,arm,arm is "A" or "C"?state.Hold:best.Plan,arm is "A" or "B"?1:(float)Policy.Drive);
            }
            Save(d,stage+"-complete.json",new{Worlds=budget.Entries.Sum(x=>x.NativeWorlds),CpuSeconds=budget.Entries.Sum(x=>x.CpuSeconds),Contacts=budget.Entries.Sum(x=>x.Evaluations),Forecasts=budget.Entries.Sum(x=>x.Forecasts)});return;
        }
        throw new ArgumentException("freeze|offline|preflight|diagnostic|selection|confirmation");
    }
}
