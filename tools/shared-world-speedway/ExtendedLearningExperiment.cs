using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

// PROVISIONAL tools-only experiment. Every rollout uses the original native world.
public sealed record ExtendedCandidate(string Label,int Id,FreeLearningPolicy Policy,double Horizon);
public static class ExtendedLearningExperiment
{
    public const string BaseHead="5dbdbd53145c39ec27356bd43cdeb41983a119cf";
    public static readonly uint[] Seeds={1312026,2312026};
    public static readonly JsonSerializerOptions Json=FreeLearningExperiment.Json;
    public static readonly Dictionary<string,int> Limits=new(){["train-ab"]=320,["train-two"]=400,["evaluation"]=180,["reserve"]=100,["offline"]=32};
    public static readonly FreeLearningScenario[] Training={
        new("E0",2,9,3.6f,22,.11f,.021f,-.017f),new("E1",3,12,6.4f,22,-.13f,-.023f,.019f),
        new("E2",4,15,4.8f,22,.17f,-.017f,-.023f),new("E3",4,18,7.2f,22,-.09f,.027f,.021f),
        new("E4",2,10,6.1f,22,-.11f,-.021f,.017f),new("E5",3,13,3.7f,22,.13f,.023f,-.019f),
        new("E6",4,16,7.4f,22,-.17f,.017f,.023f),new("E7",4,17,4.2f,22,.09f,-.027f,-.021f)};
    public static readonly FreeLearningScenario[] Selection={
        new("Q0",2,9.8f,4.15f,22,.09f,-.018f,.023f),new("Q1",3,12.8f,6.85f,22,-.17f,.026f,-.018f),
        new("Q2",4,15.8f,3.45f,22,.13f,.018f,-.027f),new("Q3",4,17.8f,6.25f,22,-.09f,-.026f,.018f)};
    // New combinations, reserved before ANY rollout; never read by CEM or selection.
    public static readonly FreeLearningScenario[] FinalSingle=Enumerable.Range(0,24).Select(i=>new FreeLearningScenario(
        $"U{i:D2}",i<8?2:i<16?3:4,9.15f+i*.37f,2.65f+(i*7%17)*.29f,22,
        (i%2==0?1:-1)*(.065f+(i%4)*.035f),(i%3==0?1:-1)*(.013f+i%5*.004f),(i%2==0?-1:1)*(.012f+i%6*.003f))).ToArray();
    public static readonly FreeLearningScenario[] FinalTwo=Enumerable.Range(0,12).Select(i=>new FreeLearningScenario(
        $"V{i:D2}",i<4?2:i<8?3:4,9.45f+i*.73f,2.85f+(i*5%13)*.34f,22,
        (i%2==0?-1:1)*(.075f+i%4*.03f),(i%3==0?-1:1)*(.014f+i%4*.005f),(i%2==0?1:-1)*(.013f+i%5*.003f))).ToArray();
    public static FreeLearningPolicy StartPolicy()=>RobustLearningExperiment.Read<JsonElement>(PhysicalSteeringAudit.PreviousPath("../robust-free-racing-learning"),"selected-policy.json")
        .GetProperty("Selected").GetProperty("Policy").Deserialize<FreeLearningPolicy>(Json)!;
    private static string Hash(object o)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(o,Json)));
    private static void Save(string d,string n,object o)=>RobustLearningExperiment.Save(d,n,o);
    private static T Read<T>(string d,string n)=>RobustLearningExperiment.Read<T>(d,n);
    public static FreeLearningScenario[] Schedule(int g)=>Training.Skip(g%2*4).Take(4).ToArray();
    private static ExtendedCandidate Start(double h)=>new("130-H"+(h==.6?"06":"09"),1020,StartPolicy(),h);
    private static RobustEpisode Execute(string d,string raw,RobustBudget b,string category,string name,ExtendedCandidate c,FreeLearningScenario s,bool two=false,
        bool normalized=true,double? duration=null,double? holdAt=null,FreeRacingAction? held=null,double? intervene=null,FreeRacingAction? alternate=null)=>
        RobustLearningExperiment.Execute(d,raw,b,category,name,c.Id,c.Policy,s,shortSeconds:duration,twoBends:two,horizonSeconds:c.Horizon,normalizeProgress:normalized,
            holdAt:holdAt,holdAction:held,interveneAt:intervene,intervention:alternate);
    public static void Run(string d,string raw,string stage){
        if(stage=="freeze"){
            if(File.Exists(Path.Combine(d,"protocol.json")))throw new IOException("Protocol already frozen.");
            Save(d,"protocol.json",new{BaseHead,Seeds,Training,Selection,FinalSingle,FinalTwo,StartingPolicy=StartPolicy(),StartingPolicyHash=Hash(StartPolicy()),
                CEM="Original130 DrawPolicy, 5 candidates, 4 shared states, 4 generations per horizon/seed; same two elites and bounds. Two repeated groups. No representation change in A/B.",
                Normalization="Only progress is a path integral: progress * 0.6 / horizon. Other terms are endpoint state or one requested-action costs, evaluated once, not accumulated. H06 arithmetic is unchanged. Different future states are the intended horizon treatment.",
                Reaction=.24,Review=.2,SteeringDegrees=35,SteeringRate=90,Horizons=new[]{.6,.9},Limits,MaximumWorlds=1000,CpuSeconds=6000,ContactEvaluations=90000000,
                CostBasis=new{PR129MeanCpu=1347.953125/442,PR130MeanCpu=1429.953125/450,PR130Contacts=19415734,PR130Forecasts=315663,H09ContactRatio=1.501},
                Tolerances=new{XY=.1,Speed=.2,ChiDegrees=1,PsiDegrees=1,BetaDegrees=1,SteeringDegrees=1.5,Omega=2,YawRate=.05,ClearanceOptimism=.1},
                PredictorGate="Six held-command native continuations from physically reached130 two-bend states: straight, entry, middle, exit, transition, largest available beta<=30deg with0.9s remaining. Original full prefix replay, existing pending preserved; never restore a partial snapshot. Missing endpoint or any failed tolerance closes H09. No threshold changes.",
                SelectionRule="Per horizon/seed: final incumbent plus recurring even-group best; unique policies, same Q0-Q3, original RobustRanking. Freeze A/B choice on Q only. Compare unretrained130 at both horizons on Q. Two-bend selection uses Q0-Q3 full worlds, same hierarchy. Sealed U/V never choose or stop training.",
                ContinuousTraining="At most20 generations x20 episodes: one first-bend retention and three full continuous two-bend worlds per candidate. The latter contain physically reached exit/straight/preparation states, without checkpoint resets. Alternate training groups; stop after three recurring anchor evaluations without completion gain or>=1m worst-DNF gain or>=0.05s all-completed time gain. Archive every candidate.",
                Schedule="freeze -> technical tests -> predictor+beta audit -> A/B if H09 valid -> A/B selection -> two-bend training -> freeze winner -> open final U/V once -> report. Stop both training stages on predictor disagreement, invariant failure, CPU/contact exhaustion. No automatic representation experiment without separate frozen protocol.",
                PASS="H09 if used passes predictor; >=22/24 U, >=10/12 V, >=2 V completions over130; unchanged physical sources; no leakage; paired median time regression<=5% and worst paired<=15% in each suite; all completed rollouts within beta45 envelope. PARTIAL for reproducible progress or physics-validity limit. FAIL for physics manipulation/leakage/unreproducibility/nonautonomous policy.",
                RawDirectory=Path.GetFullPath(raw),Sources=Sources()});return;
        }
        var frozen=Read<JsonElement>(d,"protocol.json");foreach(var x in frozen.GetProperty("Sources").EnumerateObject())
            if(Sources()[x.Name]!=x.Value.GetString())throw new InvalidOperationException("Frozen source changed: "+x.Name);
        using var budget=new RobustBudget(d,Limits,1000,6000,90000000);
        if(stage=="audit"){Audit(d,raw,budget);return;}
        if(stage=="ab"){if(!Read<JsonElement>(d,"predictor.json").GetProperty("Passed").GetBoolean())throw new InvalidOperationException("H09 gate closed; no A/B training.");
            foreach(var seed in Seeds)foreach(var h in new[]{.6,.9})Train(d,raw,budget,$"H{(h==.6?"06":"09")}-s{seed}",seed,Start(h),4,false);return;}
        if(stage=="select-ab"){SelectAb(d,raw,budget);return;}
        if(stage=="two"){if(!Read<JsonElement>(d,"beta-audit.json").GetProperty("Complete").GetBoolean())throw new InvalidOperationException("Beta audit missing.");
            var initial=Read<ExtendedCandidate>(d,"ab-winner.json");Train(d,raw,budget,"two",3312026,initial,20,true);return;}
        if(stage=="freeze-winner"){FreezeWinner(d,raw,budget);return;}
        if(stage=="final"){Final(d,raw,budget);return;}
        throw new ArgumentException("freeze|audit|ab|select-ab|two|freeze-winner|final");
    }
    public static Dictionary<string,string> Sources()=>RobustLearningExperiment.SourceNames.Append("ExtendedLearningExperiment.cs").Append("Program.cs")
        .ToDictionary(n=>n,n=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes("tools/shared-world-speedway/"+n))));

    private static void Audit(string d,string raw,RobustBudget b){
        var root=Execute(d,raw,b,"evaluation","audit130-two",Start(.6),RobustLearningExperiment.Selection[2],true,false);
        var repeat=Execute(d,raw,b,"evaluation","audit130-repeat",Start(.6),RobustLearningExperiment.Selection[2],true,false);
        Save(d,"determinism.json",new{Passed=root.Replay.Metrics.Fingerprint==repeat.Replay.Metrics.Fingerprint,root.Replay.Metrics.Fingerprint});
        var frame=RacingLineRootCauseAudit.Frame();var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var p=FreeLearningExperiment.Parameters();
        var spec=FreeLearningExperiment.Rider(root.Replay.Scenario);var points=root.Replay.Samples;
        var available=root.Replay.Decisions.Where(x=>x.Time+.9<root.Replay.Metrics.DurationSeconds).Select(x=>points.Single(s=>Math.Abs(s.Input.Time-x.Time)<1e-8)).ToArray();
        FreeRacingSample At(double reference)=>available.MinBy(s=>Math.Abs(s.Input.Rider.TotalReferenceMeters-reference))!;
        var origins=new[]{("straight",At(25)),("entry",At(frame.SegmentStartMeters(1))),
            ("middle",At(frame.SegmentStartMeters(2)+8)),("exit",At(frame.SegmentStartMeters(3)+8)),
            ("transition",At(frame.SegmentStartMeters(4))),
            ("higher-slip",available.Where(s=>Math.Abs(s.Input.Rider.SlideRadians)<=Math.PI/6).MaxBy(s=>Math.Abs(s.Input.Rider.SlideRadians))!)};
        var n=b.Reserve("predictor-offline","offline",false);var cpu=Process.GetCurrentProcess().TotalProcessorTime;long queries=0,forecasts=0;var checks=new List<object>();bool passed=true;
        foreach(var (label,origin) in origins){
            var input=origin.Input with{Pending=origin.Input.Pending.Where(c=>c.ObservedAt<origin.Input.Time-1e-8).ToArray(),NextReview=origin.Input.Time};
            var action=root.Replay.Decisions.Single(x=>Math.Abs(x.Time-input.Time)<1e-8).Action;
            var predicted=FreeRacingModel.Predict(input,spec,frame,surface,p,147,52.72097f,.9,action);queries+=predicted.ForceEvaluations;forecasts++;
            var native=Execute(d,raw,b,"evaluation","validate-"+label,Start(.6),root.Replay.Scenario,true,false,input.Time+.9,input.Time,action);
            // Offline CPU excludes the separately charged native world.
            var actual=native.Replay.Samples.FirstOrDefault(s=>Math.Abs(s.Input.Time-input.Time-.9)<1e-7);
            var prefix=points.Where(s=>s.Input.Time<=input.Time+1e-8).All(s=>native.Replay.Samples.Any(t=>Math.Abs(t.Input.Time-s.Input.Time)<1e-8&&t.Input.Rider==s.Input.Rider&&t.Input.Omega==s.Input.Omega&&t.Input.Steering==s.Input.Steering));
            if(actual is null){passed=false;checks.Add(new{Label=label,Origin=input.Time,Passed=false,PrefixExact=prefix,Reason="No legal native endpoint",Outcome=native.Metrics.Native.Outcome});continue;}
            var end=predicted.Points[^1];var v=actual.Input.Rider;
            double Degrees(double a)=>Math.Abs(BikeAngles.Wrap(a))*180/Math.PI;
            var xy=Vector2.Distance(end.Rider.Position,v.Position);var speed=Math.Abs(end.Rider.Speed-v.Speed);
            var chi=Degrees(Math.Atan2(end.Rider.Velocity.Y,end.Rider.Velocity.X)-Math.Atan2(v.Velocity.Y,v.Velocity.X));var psi=Degrees(end.Rider.Heading-v.Heading);
            var beta=Degrees(end.Rider.SlideRadians-v.SlideRadians);var yaw=Math.Abs(end.Rider.AngularVelocity-v.AngularVelocity);
            var steering=Degrees(end.Steering-actual.Input.Steering);var omega=Math.Abs(end.Omega-actual.Input.Omega);
            var optimism=Math.Max(end.Inner-actual.Inner,end.Outer-actual.Outer);
            var ok=prefix&&xy<=.1&&speed<=.2&&chi<=1&&psi<=1&&beta<=1&&yaw<=.05&&steering<=1.5&&omega<=2&&optimism<=.1;passed&=ok;
            checks.Add(new{Label=label,Origin=input.Time,OriginBeta=Degrees(input.Rider.SlideRadians),Passed=ok,PrefixExact=prefix,XY=xy,Speed=speed,Chi=chi,Psi=psi,Beta=beta,YawRate=yaw,Steering=steering,Omega=omega,ClearanceOptimism=optimism});
        }
        var total=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds;
        var nativeCpu=b.Entries.Where(e=>e.Name.StartsWith("validate-")).Sum(e=>e.CpuSeconds);
        b.Complete(n,Math.Max(0,total-nativeCpu),queries,forecasts);Save(d,"predictor.json",new{Passed=passed,Checks=checks,Queries=queries,Forecasts=forecasts,ThresholdsChanged=false});
        var auditN=b.Reserve("beta-offline","offline",false);cpu=Process.GetCurrentProcess().TotalProcessorTime;queries=0;forecasts=0;var shadows=new List<object>();var interventions=new List<(double,FreeRacingAction)>();
        foreach(var decision in root.Replay.Decisions.Where(x=>x.Time>=root.Replay.Metrics.DurationSeconds-3)){
            var s=points.Single(s=>Math.Abs(s.Input.Time-decision.Time)<1e-8);var input=s.Input with{Pending=s.Input.Pending.Where(c=>c.ObservedAt<s.Input.Time-1e-8).ToArray(),NextReview=s.Input.Time};
            FreeLearningDecision Choose(double h){var choice=FreeLearningController.Choose(input,root.Replay.Policy,spec,frame,surface,p,147,52.72097f,h,true);queries+=choice.ForceEvaluations;forecasts+=choice.Forecasts;return choice;}
            var shortD=Choose(.6);var longD=Choose(.9);var shortAtLong=longD.Candidates.Single(c=>c.Action==shortD.Action);
            if(longD.Action!=shortD.Action&&longD.Safety<shortAtLong.Safety&&interventions.Count<3)interventions.Add((input.Time,longD.Action));
            shadows.Add(new{Time=input.Time,Input=input,H06=shortD,H09=longD,ShortAtLong=shortAtLong});
        }
        b.Complete(auditN,(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,queries,forecasts);
        var counter=new List<object>();foreach(var (time,action) in interventions){var e=Execute(d,raw,b,"evaluation","counter-"+time.ToString("F2",System.Globalization.CultureInfo.InvariantCulture),Start(.6),root.Replay.Scenario,true,false,intervene:time,alternate:action);
            var exact=points.Where(s=>s.Input.Time<=time+.24+1e-9).All(s=>e.Replay.Samples.Any(t=>Math.Abs(t.Input.Time-s.Input.Time)<1e-8&&t.Input.Rider==s.Input.Rider));counter.Add(new{Observation=time,Action=action,PrefixThroughReactionExact=exact,e.Metrics});}
        Save(d,"beta-audit.json",new{Complete=true,root.Metrics,FirstWorldFingerprint=root.Replay.Metrics.Fingerprint,Shadows=shadows,Counterfactuals=counter,
            LastThreeSeconds=root.Telemetry.Where(t=>t.Time>=root.Replay.Metrics.DurationSeconds-3),States=points.Where(s=>s.Input.Time>=root.Replay.Metrics.DurationSeconds-3),
            Scope="All forces, wheel state, yaw, beta derivative (adjacent native timestamps), throttle, executed/requested steering and complete pending queues retained. Beta45 is model envelope, not physical fall. No learned use of beyond-envelope states."});
    }

    private static RobustGeneration[] Train(string d,string raw,RobustBudget b,string label,uint seed,ExtendedCandidate initial,int maximum,bool two){
        var file="generations-"+label+".json";var gs=File.Exists(Path.Combine(d,file))?Read<List<RobustGeneration>>(d,file):new();
        var random=new FreeRacingExperiment.RandomStream(seed);for(int i=0;i<gs.Count*32;i++)random.Normal();
        var incumbent=gs.Count>0?gs[^1].BestPolicy:initial.Policy;var mean=gs.Count>0?gs[^1].Mean:incumbent.Values;
        var std=gs.Count>0?gs[^1].Std:mean.Select((_,i)=>(FreeLearningPolicy.Upper[i]-FreeLearningPolicy.Lower[i])*.3).ToArray();
        int stale=0;double anchor=double.NegativeInfinity;
        for(var g=gs.Count;g<maximum;g++){
            if(b.Entries.Sum(e=>e.CpuSeconds)>4900||b.Entries.Sum(e=>e.Evaluations)>73000000){Save(d,"stop-"+label+".json",new{Generation=g,Reason="reserve1100 CPU seconds/17M contacts for selection+final"});break;}
            var cpu=Process.GetCurrentProcess().TotalProcessorTime;var cases=Schedule(g);var candidates=new List<RobustEvaluation>();
            for(int c=0;c<5;c++){
                var id=two?4000+g*5+c:(initial.Horizon==.6?10000:20000)+(seed==Seeds[0]?0:1000)+g*5+c;
                var policy=RobustLearningExperiment.DrawPolicy(random,incumbent,mean,std,c);var candidate=new ExtendedCandidate(label,id,policy,initial.Horizon);
                var scores=cases.Select((s,i)=>Execute(d,raw,b,two?"train-two":"train-ab",$"{label}-g{g:D2}-p{id}-"+s.Name,candidate,s,two&&i>0).Metrics).ToArray();
                candidates.Add(new(label,g,id,policy,cases.Select((s,i)=>s.Name+(two&&i>0?"-two":"-single")).ToArray(),scores));
            }
            var ranked=RobustRanking.Rank(candidates).ToArray();incumbent=ranked[0].Policy;
            for(int i=0;i<8;i++){var m=ranked.Take(2).Average(c=>c.Policy.Values[i]);mean[i]=.4*mean[i]+.6*m;std[i]=Math.Max(.15*(FreeLearningPolicy.Upper[i]-FreeLearningPolicy.Lower[i]),Math.Sqrt(ranked.Take(2).Average(c=>Math.Pow(c.Policy.Values[i]-m,2))));}
            var gen=new RobustGeneration(label,g,(g+1)*20,candidates.ToArray(),ranked[0].Id,incumbent,(double[])mean.Clone(),(double[])std.Clone(),(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,
                candidates.Sum(c=>c.Results.Sum(r=>r.Native.PhysicsEvaluations)),candidates.Sum(c=>c.Results.Sum(r=>r.Native.Forecasts)));gs.Add(gen);Save(d,file,gs);
            Console.WriteLine($"GEN {label}/{g}: {candidates.Sum(RobustRanking.Completed)}/20 best={ranked[0].Id} {RobustRanking.Completed(ranked[0])}/4 CPU={gen.CpuSeconds:F2}");
            if(gen.CpuSeconds>150){Save(d,"stop-"+label+".json",new{Generation=g,Reason="generation150 CPU seconds"});break;}
            if(two&&g%2==0){var best=ranked[0];var metric=10000*RobustRanking.Completed(best)+(best.Results.All(r=>r.Native.Completed)?1000-20*RobustRanking.WorstTime(best):RobustRanking.WorstFailure(best));
                if(metric>=anchor+1){anchor=metric;stale=0;}else stale++;if(stale>=3){Save(d,"stop-two.json",new{Generation=g,Reason="three recurring anchor evaluations without declared gain"});break;}}
        }
        return gs.ToArray();
    }
    private static RobustEvaluation Evaluate(string d,string raw,RobustBudget b,ExtendedCandidate c,bool two,string prefix)=>new(c.Label,-1,c.Id,c.Policy,Selection.Select(s=>s.Name).ToArray(),
        Selection.Select(s=>Execute(d,raw,b,"evaluation",prefix+"-"+c.Label+"-"+c.Id+"-"+s.Name,c,s,two).Metrics).ToArray());
    private static void SelectAb(string d,string raw,RobustBudget b){
        if(b.Entries.Any(e=>e.Name.StartsWith("final-")))throw new InvalidOperationException("No selection after final test.");
        var candidates=new List<ExtendedCandidate>();foreach(var seed in Seeds)foreach(var h in new[]{.6,.9}){
            var label=$"H{(h==.6?"06":"09")}-s{seed}";var gs=Read<RobustGeneration[]>(d,"generations-"+label+".json");
            candidates.AddRange(RobustLearningExperiment.Shortlist(gs).Select(x=>new ExtendedCandidate(label,x.Id,x.Policy,h)));}
        candidates.AddRange(new[]{Start(.6),Start(.9)});var scores=candidates.Select(c=>Evaluate(d,raw,b,c,false,"ab-select")).ToArray();Save(d,"ab-selection.json",scores);
        var winner=RobustRanking.Rank(scores).First();Save(d,"ab-winner.json",candidates.Single(c=>c.Label==winner.Variant&&c.Id==winner.Id));
        Save(d,"ab-candidates.json",candidates);
    }
    private static void FreezeWinner(string d,string raw,RobustBudget b){
        if(File.Exists(Path.Combine(d,"winner.json"))||b.Entries.Any(e=>e.Name.StartsWith("final-")))throw new InvalidOperationException("Winner already frozen or final opened.");
        var initial=Read<ExtendedCandidate>(d,"ab-winner.json");var gs=Read<RobustGeneration[]>(d,"generations-two.json");
        var cs=RobustLearningExperiment.Shortlist(gs).Select(x=>new ExtendedCandidate("two",x.Id,x.Policy,initial.Horizon)).Append(initial).ToArray();
        var scores=cs.Select(c=>Evaluate(d,raw,b,c,true,"two-select")).ToArray();Save(d,"two-selection.json",scores);var best=RobustRanking.Rank(scores).First();
        var winner=cs.Single(c=>c.Label==best.Variant&&c.Id==best.Id);Save(d,"winner.json",winner);Save(d,"winner-lock.json",new{PolicyHash=Hash(winner),FinalWorlds=0,Frozen=true});
    }
    private static void Final(string d,string raw,RobustBudget b){
        var winner=Read<ExtendedCandidate>(d,"winner.json");if(Hash(winner)!=Read<JsonElement>(d,"winner-lock.json").GetProperty("PolicyHash").GetString())throw new InvalidOperationException("Winner hash mismatch.");
        var cs=new[]{new ExtendedCandidate("129-after200",72,RobustLearningExperiment.Reference("129-200"),.6),Start(.6),winner with{Label="131-final"}};
        var results=new List<RobustEvaluation>();foreach(var c in cs)foreach(var two in new[]{false,true}){
            var scenarios=two?FinalTwo:FinalSingle;var metrics=scenarios.Select(s=>Execute(d,raw,b,"evaluation","final-"+c.Label+"-"+s.Name,c,s,two).Metrics).ToArray();
            results.Add(new(c.Label+(two?"-two":"-single"),-1,c.Id,c.Policy,scenarios.Select(s=>s.Name).ToArray(),metrics));Save(d,"final-results.json",results);}
    }
}
