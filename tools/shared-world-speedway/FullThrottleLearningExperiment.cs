using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

public sealed record ThrottleCandidate(string Label,int Id,FreeLearningPolicy Policy,bool FullThrottle,double Horizon=.9);

/// <summary>PROVISIONAL, tools-only. The sole A/B treatment is an additional requested gas1 action.</summary>
public static class FullThrottleLearningExperiment
{
    public const string BaseHead="52519764ca5338f73c610a4302fe13656ffe1b05";
    public static readonly uint[] Seeds={1322026,2322026};
    public static readonly Dictionary<string,int> Limits=new(){["train-ab"]=400,["continuation"]=100,["selection"]=84,["final"]=144,["diagnostic"]=22,["offline"]=32};
    public static readonly FreeLearningScenario[] Training={
        new("F0",2,9.35f,3.85f,21,.105f,.019f,-.021f),new("F1",3,12.35f,6.55f,21,-.125f,-.021f,.017f),
        new("F2",4,15.35f,4.65f,21,.155f,-.019f,-.021f),new("F3",4,17.65f,7.05f,21,-.095f,.025f,.019f),
        new("F4",2,10.35f,6.05f,21,-.105f,-.019f,.021f),new("F5",3,13.35f,3.85f,21,.125f,.021f,-.017f),
        new("F6",4,16.35f,7.25f,21,-.155f,.019f,.021f),new("F7",4,17.35f,4.35f,21,.095f,-.025f,-.019f)};
    public static readonly FreeLearningScenario[] Diagnostic={
        new("D0",2,9.65f,4.35f,21,.085f,.017f,-.019f),new("D1",3,12.65f,6.35f,21,-.145f,-.023f,.021f),
        new("D2",4,15.65f,3.65f,21,.135f,-.017f,-.025f),new("D3",4,17.65f,6.65f,21,-.085f,.023f,.017f)};
    public static readonly FreeLearningScenario[] Selection={
        new("K0",2,9.85f,4.45f,21,.095f,-.017f,.021f),new("K1",4,16.85f,6.45f,21,-.115f,.023f,-.019f),
        new("K2",2,10.85f,3.25f,21,.125f,.021f,-.023f),new("K3",3,13.85f,6.75f,21,-.095f,-.025f,.017f),
        new("K4",4,15.85f,4.75f,21,.145f,-.019f,-.021f),new("K5",4,17.85f,6.25f,21,-.125f,.017f,.023f)};
    public static readonly FreeLearningScenario[] FinalSingle=Enumerable.Range(0,24).Select(i=>new FreeLearningScenario(
        $"W{i:D2}",i<8?2:i<16?3:4,9.235f+i*.365f,2.775f+(i*7%17)*.285f,21,
        (i%2==0?-1:1)*(.0725f+i%4*.0325f),(i%3==0?-1:1)*(.0145f+i%5*.0035f),(i%2==0?1:-1)*(.0135f+i%6*.0025f))).ToArray();
    public static readonly FreeLearningScenario[] FinalTwo=Enumerable.Range(0,12).Select(i=>new FreeLearningScenario(
        $"Z{i:D2}",i<4?2:i<8?3:4,9.535f+i*.715f,2.975f+(i*5%13)*.325f,21,
        (i%2==0?1:-1)*(.0825f+i%4*.0275f),(i%3==0?1:-1)*(.0155f+i%4*.0045f),(i%2==0?-1:1)*(.0145f+i%5*.0025f))).ToArray();
    public static FreeLearningPolicy StartPolicy()=>Read<ExtendedCandidate>(PhysicalSteeringAudit.PreviousPath("../autonomous-speedway-extended-learning"),"winner.json").Policy;
    public static FreeLearningScenario[] Schedule(int generation)=>Training.Skip(generation%2*4).Take(4).ToArray();
    private static ThrottleCandidate Start(bool full)=>new(full?"131-B":"131-A",4061,StartPolicy(),full);
    private static readonly JsonSerializerOptions Json=FreeLearningExperiment.Json;
    private static string Hash(object value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value,Json)));
    private static void Save(string d,string n,object value)=>RobustLearningExperiment.Save(d,n,value);
    private static T Read<T>(string d,string n)=>RobustLearningExperiment.Read<T>(d,n);
    public static Dictionary<string,string> Sources()=>ExtendedLearningExperiment.Sources().Keys.Append("FullThrottleLearningExperiment.cs")
        .ToDictionary(n=>n,n=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes("tools/shared-world-speedway/"+n))));
    private static RobustEpisode Execute(string d,string raw,RobustBudget b,string category,string name,ThrottleCandidate c,FreeLearningScenario s,bool two,
        double? duration=null,double? holdAt=null,FreeRacingAction? held=null){
        if(b.Entries.Sum(e=>e.Forecasts)>1180000)throw new InvalidOperationException("Forecast reserve exhausted.");
        return RobustLearningExperiment.Execute(d,raw,b,category,name,c.Id,c.Policy,s,shortSeconds:duration,twoBends:two,
            horizonSeconds:c.Horizon,normalizeProgress:true,holdAt:holdAt,holdAction:held,fullThrottleCandidate:c.FullThrottle);
    }
    public static IEnumerable<RobustMetrics> Two(RobustEvaluation e)=>e.Results.Where((_,i)=>e.Scenarios[i].EndsWith("-two",StringComparison.Ordinal));
    public static int TwoCompleted(RobustEvaluation e)=>Two(e).Count(r=>r.Native.Completed);
    private static double WorstTwo(RobustEvaluation e)=>Two(e).All(r=>r.Native.Completed)?Two(e).Max(r=>r.Native.GateSeconds!.Value):double.PositiveInfinity;
    public static IOrderedEnumerable<RobustEvaluation> Rank(IEnumerable<RobustEvaluation> cs)=>cs.OrderByDescending(TwoCompleted)
        .ThenBy(RobustRanking.ConfirmedOrControl).ThenByDescending(RobustRanking.Completed).ThenByDescending(RobustRanking.WorstFailure)
        .ThenBy(e=>Math.Ceiling(WorstTwo(e)/.05)).ThenBy(e=>Two(e).All(r=>r.Native.Completed)?Math.Ceiling(Two(e).Average(r=>r.Native.GateSeconds!.Value)/.025):double.PositiveInfinity)
        .ThenBy(e=>e.Results.All(r=>r.Native.Completed)?Math.Ceiling(e.Results.Where((_,i)=>e.Scenarios[i].EndsWith("-single",StringComparison.Ordinal)).Max(r=>r.Native.GateSeconds!.Value)/.05):double.PositiveInfinity)
        .ThenByDescending(e=>e.Results.Min(r=>r.Native.MinimumClearanceMeters)).ThenByDescending(e=>e.Results.Where(r=>r.Native.Completed).Select(r=>r.Native.ExitSpeed!.Value).DefaultIfEmpty(0).Min())
        .ThenBy(e=>e.Results.Where(r=>r.Native.Completed).Select(r=>Math.Abs(r.ExitYawRate??0)).DefaultIfEmpty(double.PositiveInfinity).Max()).ThenBy(e=>e.Id);
    public static void Run(string d,string raw,string stage){
        if(stage=="freeze"){
            if(File.Exists(Path.Combine(d,"protocol.json")))throw new IOException("Frozen protocol exists.");
            Save(d,"protocol.json",new{BaseHead,Seeds,Training,Diagnostic,Selection,FinalSingle,FinalTwo,StartingPolicy=StartPolicy(),
                StartingPolicyHash=Hash(StartPolicy()),Sources=Sources(),Limits,MaximumWorlds=750,CpuSeconds=6000,ContactEvaluations=100000000,Forecasts=1200000,
                CostBasis=new{PR131Worlds=736,PR131Cpu=2798.734375,PR131Contacts=63082861,PR131Forecasts=751281,BExtraActionRatio=1.5},
                Horizon=.9,Reaction=.24,Review=.2,SteeringDegrees=35,SteeringRate=90,BetaEnvelopeDegrees=45,
                Treatment="A: exact131 actions; B: same angles, gas0/Drive/1 plus active, stable Distinct. Same eight parameters, bounds, objective, CEM and native physics. No throttle reward or forced pattern.",
                CEM="2 seeds x2 variants x5 generations x5 candidates x4 shared scenarios=400; same inherited DrawPolicy and32 normal draws/generation. Incumbent,3 Gaussian,1 immigrant; two elites; mean.4old+.6elite; std floor.15range. Alternate frozen cohorts; one single+three full two-bend worlds per candidate.",
                Ranking="Two-bend completions first; control/confirmed failures; all completions; worst failed legal progress; worst two time50ms, mean25ms; single time50ms; reserve, exit speed, yaw, ID. Same in both arms. No fast DNF outranks legal two-bend completion.",
                SelectionRule="Per arm/seed: last incumbent plus best even-cohort archived winner, unique vectors; same K0-K5 (two single,four two). Include unchanged131 A/B references. Learned winner must be from trained shortlist. Hash choice before any W/Z world; no final feedback.",
                Continuation="Only B: selection two completions>=both references/A; all6 completed; worst two time at least.10s faster than131-A and best A; no lower minimum certified reserve. At most5x20 episodes, seed3322026, same CEM. Stop after three comparable even-cohort evaluations without >=.05s/completion/1m failed progress gain; no changed objective training.",
                Guards="Reserve1800 CPU/30M contacts/360k forecasts for independent selection+final before training; generation225CPU; episode30CPU; stop on physical invariant/predictor disagreement. No training after training-complete or winner lock. Technical tests are separate, not training worlds.",
                Predictor="Four B held-command continuations from D1: entry,middle,second entry,first full-gas reopening (fallback largest beta with legal .9 remaining). Preserve full native prefix and pending queue; compare all points, require endpoint. XY.1m, speed.2, chi/psi/beta1deg, steering1.5deg, omega2rad/s,yaw.05rad/s,clearance optimism.1m. No raised tolerances.",
                PASS=">=23/24 single,>=11/12 two; paired median two improvement>=2% vs131-A; paired single median regression<=5% and max<=15% vs130-H06; unchanged physics/beta45; unseen final; endogenous choice; all predictor/technical/leak/determinism gates. PARTIAL if valid but speed/safety insufficient; FAIL for errors/leak/false data/unreproducibility.",
                RawDirectory=Path.GetFullPath(raw),Stages="freeze -> targeted tests -> diagnose+offline audit+predictor -> train -> select -> optional continuation -> freeze-winner -> final -> report"});return;
        }
        var p=Read<JsonElement>(d,"protocol.json");foreach(var x in p.GetProperty("Sources").EnumerateObject())if(Sources()[x.Name]!=x.Value.GetString())throw new InvalidOperationException("Frozen source changed: "+x.Name);
        using var b=new RobustBudget(d,Limits,750,6000,100000000);
        if(stage=="diagnose"){Diagnose(d,raw,b);return;}
        if(stage=="train"){
            if(File.Exists(Path.Combine(d,"winner-lock.json"))||File.Exists(Path.Combine(d,"training-complete.json")))throw new InvalidOperationException("Training sealed.");
            if(!Read<JsonElement>(d,"preflight.json").GetProperty("Passed").GetBoolean())throw new InvalidOperationException("Preflight closed.");
            foreach(var seed in Seeds)foreach(var full in new[]{false,true})Train(d,raw,b,$"{(full?"B":"A")}-s{seed}",seed,Start(full),5,false);
            Save(d,"phase1-complete.json",new{Worlds=b.Entries.Count(e=>e.Category=="train-ab"),FinalWorlds=0});return;
        }
        if(stage=="select"){Select(d,raw,b);return;}
        if(stage=="continue"){
            if(File.Exists(Path.Combine(d,"winner-lock.json"))||File.Exists(Path.Combine(d,"training-complete.json")))throw new InvalidOperationException("Training sealed.");
            var choice=Read<JsonElement>(d,"continuation-decision.json");if(choice.GetProperty("Extend").GetBoolean()){
                var initial=Read<ThrottleCandidate>(d,"continuation-start.json");Train(d,raw,b,"B-continued",3322026,initial,5,true);
            }
            Save(d,"training-complete.json",new{Worlds=b.Entries.Count(e=>e.Category is "train-ab" or "continuation"),FinalWorlds=0});return;
        }
        if(stage=="freeze-winner"){FreezeWinner(d,raw,b);return;}
        if(stage=="final"){Final(d,raw,b);return;}
        throw new ArgumentException("freeze|diagnose|train|select|continue|freeze-winner|final");
    }
    private static void Diagnose(string d,string raw,RobustBudget b){
        var runs=new List<RobustEpisode>();foreach(var full in new[]{false,true})for(int i=0;i<Diagnostic.Length;i++)
            runs.Add(Execute(d,raw,b,"diagnostic",$"diag-{(full?"B":"A")}-D{i}",Start(full),Diagnostic[i],i>0));
        var repeats=new List<object>();foreach(var full in new[]{false,true}){
            var original=runs.Single(r=>r.Replay.Name==$"diag-{(full?"B":"A")}-D1");
            var repeat=Execute(d,raw,b,"diagnostic",$"repeat-{(full?"B":"A")}-D1",Start(full),Diagnostic[1],true);
            repeats.Add(new{Variant=full?"B":"A",Passed=repeat.Replay.Metrics.Fingerprint==original.Replay.Metrics.Fingerprint});
        }
        var old=Execute(d,raw,b,"diagnostic","parity131-V00",Start(false),ExtendedLearningExperiment.FinalTwo[0],true);
        var oldLedger=Read<RobustReservation[]>(PhysicalSteeringAudit.PreviousPath("../autonomous-speedway-extended-learning"),"attempts.json");
        var expected=oldLedger.Single(e=>e.Name=="final-131-final-V00").Metrics!.Native.Fingerprint;
        var parity=old.Metrics.Native.Fingerprint==expected;Save(d,"determinism.json",new{Repeats=repeats,Legacy131Exact=parity,Expected=expected,Actual=old.Metrics.Native.Fingerprint});
        var frame=RacingLineRootCauseAudit.Frame();var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var parameters=FreeLearningExperiment.Parameters();
        var n=b.Reserve("throttle-offline","offline",false);var cpu=Process.GetCurrentProcess().TotalProcessorTime;long queries=0,forecasts=0;var records=new List<object>();int weightReachable=0;
        foreach(var run in runs){var spec=FreeLearningExperiment.Rider(run.Replay.Scenario);foreach(var dec in run.Replay.Decisions){
            var sample=run.Replay.Samples.Single(s=>Math.Abs(s.Input.Time-dec.Time)<1e-8);
            var input=sample.Input with{Pending=sample.Input.Pending.Where(c=>c.ObservedAt<dec.Time-1e-8).ToArray(),NextReview=dec.Time};
            var expanded=FreeLearningController.Choose(input,run.Replay.Policy,spec,frame,surface,parameters,147,52.72097f,.9,true,true);queries+=expanded.ForceEvaluations;forecasts+=expanded.Forecasts;
            object Rerank(double weight){var ranked=expanded.Candidates.OrderBy(c=>c.Safety).ThenBy(c=>c.Score+(weight-run.Replay.Policy.ThrottleWeight)*Math.Pow(c.Action.Drive-run.Replay.Policy.Drive,2))
                    .ThenBy(c=>Math.Abs(c.Action.SteeringRadians-input.Active.SteeringRadians)).ThenBy(c=>c.Action.SteeringRadians).First();
                if(weight==0&&ranked.Action.Drive==1)weightReachable++;return new{Weight=weight,ranked.Action,ranked.Safety};}
            records.Add(new{Run=run.Replay.Name,Time=dec.Time,Segment=input.Rider.Road.Segment,Input=input,Chosen=dec.Action,dec.Safety,
                OriginalHasFull=dec.Candidates.Any(c=>c.Action.Drive==1),SafePositive=dec.Candidates.Any(c=>c.Action.Drive>0&&c.Safety==FreeActionSafety.SafeInHorizon),
                ExpandedChosen=expanded.Action,ExpandedSafety=expanded.Safety,OfflineRanks=new[]{Rerank(0),Rerank(1),Rerank(4)},
                Candidates=expanded.Candidates.Select(c=>new{Candidate=c,ThrottleCost=run.Replay.Policy.ThrottleWeight*Math.Pow(c.Action.Drive-run.Replay.Policy.Drive,2),
                    ProgressCost=-run.Replay.Policy.ProgressWeight*c.Progress*.6/.9,DirectionCost=run.Replay.Policy.DirectionWeight*c.DirectionError*c.DirectionError*10})});
        }}
        b.Complete(n,(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,queries,forecasts);
        Save(d,"throttle-audit.json",new{Records=records,ZeroWeightFullSelections=weightReachable,PolicyUnchanged=true,Scope="Counterfactual rankings on identical saved states only; no changed-policy native trajectory and no full-bend safety inference."});
        var source=runs.Single(r=>r.Replay.Name=="diag-B-D1");var points=source.Replay.Samples;var available=source.Replay.Decisions.Where(x=>x.Time+.9<source.Metrics.Native.DurationSeconds).ToArray();
        FreeLearningDecision At(int segment)=>available.MinBy(x=>Math.Abs(points.Single(s=>Math.Abs(s.Input.Time-x.Time)<1e-8).Input.Rider.TotalReferenceMeters-frame.SegmentStartMeters(segment)))!;
        var reopening=available.FirstOrDefault(x=>x.Action.Drive==1&&source.Replay.Decisions.Any(prior=>prior.Time<x.Time&&prior.Action.Drive<.99))??available.MaxBy(x=>Math.Abs(points.Single(s=>Math.Abs(s.Input.Time-x.Time)<1e-8).Input.Rider.SlideRadians))!;
        var origins=new[]{At(1),At(2),At(5),reopening};var checks=new List<object>();bool predictor=true;
        for(int i=0;i<origins.Length;i++){
            var dec=origins[i];var origin=points.Single(s=>Math.Abs(s.Input.Time-dec.Time)<1e-8);
            var input=origin.Input with{Pending=origin.Input.Pending.Where(c=>c.ObservedAt<dec.Time-1e-8).ToArray(),NextReview=dec.Time};
            n=b.Reserve("predict-offline-"+i,"offline",false);cpu=Process.GetCurrentProcess().TotalProcessorTime;
            var predicted=FreeRacingModel.Predict(input,FreeLearningExperiment.Rider(source.Replay.Scenario),frame,surface,parameters,147,52.72097f,.9,dec.Action);
            b.Complete(n,(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,predicted.ForceEvaluations,1);
            var native=Execute(d,raw,b,"diagnostic","validate-B-"+i,Start(true),source.Replay.Scenario,true,dec.Time+.9,dec.Time,dec.Action);
            double Deg(double x)=>Math.Abs(BikeAngles.Wrap(x))*180/Math.PI;
            var errors=predicted.Points.Select(q=>{var a=native.Replay.Samples.FirstOrDefault(s=>Math.Abs(s.Input.Time-q.Time)<1e-7);if(a is null)return null;
                return new{q.Time,XY=(double)Vector2.Distance(q.Rider.Position,a.Input.Rider.Position),Speed=(double)Math.Abs(q.Rider.Speed-a.Input.Rider.Speed),
                    Chi=Deg(Math.Atan2(q.Rider.Velocity.Y,q.Rider.Velocity.X)-Math.Atan2(a.Input.Rider.Velocity.Y,a.Input.Rider.Velocity.X)),Psi=Deg(q.Rider.Heading-a.Input.Rider.Heading),Beta=Deg(q.Rider.SlideRadians-a.Input.Rider.SlideRadians),
                    Steering=Deg(q.Steering-a.Input.Steering),Omega=Math.Abs(q.Omega-a.Input.Omega),YawRate=(double)Math.Abs(q.Rider.AngularVelocity-a.Input.Rider.AngularVelocity),ClearanceOptimism=Math.Max(q.Inner-a.Inner,q.Outer-a.Outer)};}).ToArray();
            bool prefix=points.Where(s=>s.Input.Time<dec.Time-1e-8).All(s=>native.Replay.Samples.Any(a=>Math.Abs(a.Input.Time-s.Input.Time)<1e-8&&a.Input.Rider==s.Input.Rider&&a.Input.Omega==s.Input.Omega&&a.Input.Steering==s.Input.Steering));
            bool pass=prefix&&errors.All(e=>e is not null&&e.XY<=.1&&e.Speed<=.2&&e.Chi<=1&&e.Psi<=1&&e.Beta<=1&&e.Steering<=1.5&&e.Omega<=2&&e.YawRate<=.05&&e.ClearanceOptimism<=.1);
            predictor&=pass;checks.Add(new{Origin=dec.Time,Action=dec.Action,Passed=pass,PrefixExact=prefix,Errors=errors});
        }
        Save(d,"predictor.json",new{Passed=predictor,Checks=checks,TolerancesChanged=false});
        var deterministic=repeats.All(o=>JsonSerializer.SerializeToElement(o).GetProperty("Passed").GetBoolean());
        bool reachable=runs.Where(r=>r.Replay.Name.StartsWith("diag-B",StringComparison.Ordinal)).Any(r=>r.Replay.Decisions.Any(x=>x.Action.Drive==1))||weightReachable>0;
        Save(d,"preflight.json",new{Passed=predictor&&parity&&deterministic&&reachable,Predictor=predictor,LegacyParity=parity,Deterministic=deterministic,RepresentableFullThrottle=reachable,FinalWorlds=0});
    }
    private static RobustGeneration[] Train(string d,string raw,RobustBudget b,string label,uint seed,ThrottleCandidate initial,int maximum,bool extension){
        var file="generations-"+label+".json";var gs=File.Exists(Path.Combine(d,file))?Read<List<RobustGeneration>>(d,file):new();
        var random=new FreeRacingExperiment.RandomStream(seed);for(int i=0;i<gs.Count*32;i++)random.Normal();
        var incumbent=gs.Count>0?gs[^1].BestPolicy:initial.Policy;var mean=gs.Count>0?gs[^1].Mean:incumbent.Values;
        var std=gs.Count>0?gs[^1].Std:mean.Select((_,i)=>(FreeLearningPolicy.Upper[i]-FreeLearningPolicy.Lower[i])*.3).ToArray();
        foreach(var g in Enumerable.Range(gs.Count,maximum-gs.Count)){
            if(b.Entries.Sum(e=>e.CpuSeconds)>4200||b.Entries.Sum(e=>e.Evaluations)>70000000||b.Entries.Sum(e=>e.Forecasts)>840000)throw new InvalidOperationException("Independent evaluation reserve reached; training stopped.");
            var cpu=Process.GetCurrentProcess().TotalProcessorTime;var cases=Schedule(g);var cs=new List<RobustEvaluation>();
            for(int c=0;c<5;c++){
                int id=(extension?9000:5000+(seed==Seeds[1]?2000:0)+(initial.FullThrottle?1000:0))+g*5+c;
                var policy=RobustLearningExperiment.DrawPolicy(random,incumbent,mean,std,c);var candidate=new ThrottleCandidate(label,id,policy,initial.FullThrottle);
                cs.Add(new(label,g,id,policy,cases.Select((s,i)=>s.Name+(i>0?"-two":"-single")).ToArray(),
                    cases.Select((s,i)=>Execute(d,raw,b,extension?"continuation":"train-ab",$"{label}-g{g:D2}-p{id}-"+s.Name,candidate,s,i>0).Metrics).ToArray()));
            }
            var ranked=Rank(cs).ToArray();incumbent=ranked[0].Policy;
            for(int i=0;i<8;i++){var m=ranked.Take(2).Average(c=>c.Policy.Values[i]);mean[i]=.4*mean[i]+.6*m;std[i]=Math.Max(.15*(FreeLearningPolicy.Upper[i]-FreeLearningPolicy.Lower[i]),Math.Sqrt(ranked.Take(2).Average(c=>Math.Pow(c.Policy.Values[i]-m,2))));}
            var gen=new RobustGeneration(label,g,(g+1)*20,cs.ToArray(),ranked[0].Id,incumbent,(double[])mean.Clone(),(double[])std.Clone(),(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,
                cs.Sum(c=>c.Results.Sum(r=>r.Native.PhysicsEvaluations)),cs.Sum(c=>c.Results.Sum(r=>r.Native.Forecasts)));gs.Add(gen);Save(d,file,gs);
            Console.WriteLine($"GEN {label}/{g} completed={cs.Sum(RobustRanking.Completed)}/20 two={cs.Sum(TwoCompleted)}/15 best={ranked[0].Id} CPU={gen.CpuSeconds:F2}");
            if(gen.CpuSeconds>225)throw new InvalidOperationException("Generation CPU limit.");
            if(g>=4&&g%2==0){var anchors=gs.Where(x=>x.Generation%2==0).TakeLast(3).Select(x=>Rank(x.Candidates).First()).ToArray();
                double Metric(RobustEvaluation x)=>10000*TwoCompleted(x)+100*RobustRanking.Completed(x)+(x.Results.All(r=>r.Native.Completed)?1000-20*WorstTwo(x):RobustRanking.WorstFailure(x));
                if(anchors.Length==3&&Metric(anchors[2])<Metric(anchors[0])+1&&Metric(anchors[1])<Metric(anchors[0])+1){Save(d,"stop-"+label+".json",new{Generation=g,Reason="Three comparable anchor evaluations without declared gain"});break;}}
        }
        return gs.ToArray();
    }
    private static ThrottleCandidate[] Shortlist(string d,string label,bool full){var gs=Read<RobustGeneration[]>(d,"generations-"+label+".json");
        var last=Rank(gs[^1].Candidates).First();var anchor=Rank(gs.Where(g=>g.Generation%2==0).Select(g=>Rank(g.Candidates).First())).First();
        return new[]{last,anchor}.DistinctBy(x=>string.Join("|",x.Policy.Values.Select(v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)))).Select(x=>new ThrottleCandidate(label,x.Id,x.Policy,full)).ToArray();}
    private static RobustEvaluation Evaluate(string d,string raw,RobustBudget b,ThrottleCandidate c,string prefix)=>new(c.Label,-1,c.Id,c.Policy,Selection.Select((s,i)=>s.Name+(i>=2?"-two":"-single")).ToArray(),
        Selection.Select((s,i)=>Execute(d,raw,b,"selection",$"{prefix}-{c.Label}-{c.Id}-"+s.Name,c,s,i>=2).Metrics).ToArray());
    private static void Select(string d,string raw,RobustBudget b){
        if(b.Entries.Any(e=>e.Category=="final"))throw new InvalidOperationException("Selection after final forbidden.");
        var cs=Seeds.SelectMany(seed=>new[]{false,true}.SelectMany(full=>Shortlist(d,$"{(full?"B":"A")}-s{seed}",full))).Concat(new[]{Start(false),Start(true)}).ToArray();
        Save(d,"selection-candidates.json",cs);var scores=cs.Select(c=>Evaluate(d,raw,b,c,"select")).ToArray();Save(d,"selection.json",scores);
        var a=Rank(scores.Where(x=>x.Variant.StartsWith("A-",StringComparison.Ordinal))).First();var best=Rank(scores.Where(x=>x.Variant.StartsWith("B-",StringComparison.Ordinal))).First();var baseline=scores.Single(x=>x.Variant=="131-A");
        bool extend=TwoCompleted(best)>=TwoCompleted(a)&&TwoCompleted(best)>=TwoCompleted(baseline)&&RobustRanking.Completed(best)==6&&WorstTwo(best)<=WorstTwo(baseline)-.1&&WorstTwo(best)<=WorstTwo(a)-.1
            &&best.Results.Min(r=>r.Native.MinimumClearanceMeters)>=baseline.Results.Min(r=>r.Native.MinimumClearanceMeters);
        Save(d,"continuation-decision.json",new{Extend=extend,BestB=best.Id,BestA=a.Id,FinalWorlds=0,MaximumAdditionalWorlds=100});Save(d,"continuation-start.json",cs.Single(c=>c.Label==best.Variant&&c.Id==best.Id));
    }
    private static void FreezeWinner(string d,string raw,RobustBudget b){
        if(!File.Exists(Path.Combine(d,"training-complete.json"))||File.Exists(Path.Combine(d,"winner-lock.json"))||b.Entries.Any(e=>e.Category=="final"))throw new InvalidOperationException("Choice stage invalid.");
        var cs=Read<ThrottleCandidate[]>(d,"selection-candidates.json").Where(c=>c.Label is not "131-A" and not "131-B").ToList();var scores=Read<RobustEvaluation[]>(d,"selection.json").Where(c=>c.Variant is not "131-A" and not "131-B").ToList();
        if(File.Exists(Path.Combine(d,"generations-B-continued.json"))){var extra=Shortlist(d,"B-continued",true);cs.AddRange(extra);scores.AddRange(extra.Select(c=>Evaluate(d,raw,b,c,"continued-select")));}
        var best=Rank(scores).First();var winner=cs.Single(c=>c.Id==best.Id&&c.Label==best.Variant);Save(d,"all-learned-selection.json",scores);Save(d,"winner.json",winner);
        Save(d,"winner-lock.json",new{PolicyHash=Hash(winner),FinalWorlds=b.Entries.Count(e=>e.Category=="final"),ReservationCount=b.Entries.Count,Frozen=true});
    }
    private static void Final(string d,string raw,RobustBudget b){
        var winner=Read<ThrottleCandidate>(d,"winner.json");if(Hash(winner)!=Read<JsonElement>(d,"winner-lock.json").GetProperty("PolicyHash").GetString())throw new InvalidOperationException("Winner changed.");
        var cs=new[]{new ThrottleCandidate("130-H06-A",1020,ExtendedLearningExperiment.StartPolicy(),false,.6),Start(false),Start(true),winner with{Label="132-final"}};
        var results=new List<RobustEvaluation>();foreach(var c in cs)foreach(var two in new[]{false,true}){
            var scenarios=two?FinalTwo:FinalSingle;var metrics=scenarios.Select(s=>Execute(d,raw,b,"final","final-"+c.Label+"-"+s.Name,c,s,two).Metrics).ToArray();
            results.Add(new(c.Label+(two?"-two":"-single"),-1,c.Id,c.Policy,scenarios.Select(s=>s.Name).ToArray(),metrics));Save(d,"final-results.json",results);}
    }
}
