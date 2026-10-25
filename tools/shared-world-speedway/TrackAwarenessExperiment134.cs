using System.Diagnostics;
using System.Text.Json;
using CoreSim;

namespace SharedWorldSpeedway;

public sealed record AwarenessCase134(string Name,string Geometry,FreeLearningScenario Scenario,bool Two);
public sealed record AwarenessPolicy134(string Label,int Id,FreeLearningPolicy Policy,bool Awareness,bool FullThrottle=true);

/// <summary>Same eight-parameter policy, native R0 CEM and H09; one spatial-information treatment.</summary>
public static class TrackAwarenessExperiment134
{
    public const string BaseHead="5ed4499b2118ca8206bf8dcaf9741dc0790c01ac";
    public static readonly uint[] Seeds={1342026,2342026};
    public static readonly Dictionary<string,int> Limits=new(){["geometry"]=5,["diagnostic"]=40,["offline"]=20,["train-ab"]=240,["selection"]=72,["final"]=192};
    public static FreeLearningPolicy StartPolicy()=>RiderDecisionExperiment133.Read<RiderDecisionCandidate[]>(PhysicalSteeringAudit.PreviousPath("../rider-decision-optimization"),"ranked-choices.json").Single(c=>c.Label=="A0"&&c.Id==12023&&!c.NewObjective&&c.FullThrottle).Policy;
    public static TrackShapeVariant Variant(string name)=>TrackShapeVariant.All().Concat(TrackShapeVariant.ProgressiveWidths()).Single(v=>v.Name==name);
    public static TrackFrame Frame(string name)=>TrackGeometryAudit.Frame(Variant(name));
    private static AwarenessCase134 C(string name,string geometry,bool two,float speed,float offset,double start,float lateral,float heading,float yaw)=>new(name,geometry,new(name,speed<11?2:speed<15?3:4,speed,offset,start,lateral,heading,yaw),two);
    public static readonly AwarenessCase134[] Training={
        C("G0","progressive-b",false,10.175f,4.375f,18.875,.105f,-.0175f,.0215f),C("G1","progressive-b",true,13.175f,6.375f,18.875,-.115f,.0235f,-.0175f),C("G2","egg-medium",true,16.175f,4.875f,18.875,.145f,-.0215f,.0195f),
        C("G3","egg-medium",false,11.175f,6.175f,18.375,-.105f,.0195f,-.0215f),C("G4","progressive-b",true,16.875f,4.175f,18.375,.115f,-.0235f,.0175f),C("G5","egg-medium",true,17.475f,6.875f,18.375,-.145f,.0215f,-.0195f)};
    public static readonly AwarenessCase134[] Selection={
        C("K0","progressive-b",false,10.475f,4.625f,19.175,.085f,.01875f,-.02075f),C("K1","egg-medium",false,16.475f,6.625f,19.175,-.095f,-.02275f,.01675f),
        C("K2","progressive-b",true,11.475f,3.625f,19.175,.125f,-.01675f,.02275f),C("K3","egg-medium",true,13.875f,6.925f,19.175,-.085f,.02475f,-.01875f),
        C("K4","progressive-b",true,16.075f,4.925f,19.175,.135f,.02075f,-.02475f),C("K5","egg-medium",true,17.675f,5.925f,19.175,-.125f,-.01875f,.02075f)};
    public static readonly AwarenessCase134[] Diagnostic={
        C("P0","progressive-b",false,10.125f,4.325f,17.875,.095f,-.01925f,.01825f),C("P1","progressive-b",true,12.825f,6.275f,19.125,-.125f,.02125f,-.02225f),
        C("P2","egg-medium",true,15.825f,4.275f,18.125,.115f,-.02325f,.02025f),C("P3","egg-medium",true,17.525f,6.125f,19.375,-.095f,.01725f,-.01625f)};
    public static readonly AwarenessCase134[] FinalSingle=Enumerable.Range(0,24).Select(i=>C($"M{i:D2}","progressive-c",false,9.475f+i*.35f,3.075f+(i*7%17)*.271f,18.625,
        (i%2==0?1:-1)*(.08375f+i%4*.0275f),(i%3==0?1:-1)*(.01775f+i%5*.003f),(i%2==0?-1:1)*(.01675f+i%6*.002f))).ToArray();
    public static readonly AwarenessCase134[] FinalTwo=Enumerable.Range(0,12).Select(i=>C($"N{i:D2}","progressive-c",true,9.775f+i*.695f,3.275f+(i*5%13)*.305f,18.625,
        (i%2==0?-1:1)*(.09375f+i%4*.02375f),(i%3==0?-1:1)*(.01875f+i%4*.00375f),(i%2==0?1:-1)*(.01775f+i%5*.002f))).ToArray();
    public static readonly AwarenessCase134[] Transfer=Enumerable.Range(0,12).Select(i=>C($"T{i:D2}","egg-asymmetric",i>=6,10.275f+i*.625f,3.575f+(i*3%11)*.317f,18.125,
        (i%2==0?1:-1)*.11275f,(i%3==0?1:-1)*.02175f,(i%2==0?-1:1)*.01975f)).ToArray();
    public static AwarenessCase134[] Schedule(int g)=>Training.Skip(g%2*3).Take(3).ToArray();
    public static void Save(string d,string name,object o)=>RobustLearningExperiment.Save(d,name,o);
    public static T Read<T>(string d,string name)=>RobustLearningExperiment.Read<T>(d,name);
    public static string Hash(object value)=>RiderDecisionExperiment133.Hash(value);
    public static Dictionary<string,string> Sources()=>RiderDecisionExperiment133.Sources().Keys.Concat(new[]{"TrackAwareness134.cs","TrackAwarenessExperiment134.cs","TrackAwarenessAudit134.cs"})
        .ToDictionary(n=>n,n=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes("tools/shared-world-speedway/"+n))));
    public static RobustEpisode Execute(string d,string raw,RobustBudget b,string category,string name,AwarenessPolicy134 policy,AwarenessCase134 c,
        double? duration=null,double? interveneAt=null,FreeRacingAction? alternative=null,double? holdAt=null,FreeRacingAction? held=null){
        if(b.Entries.Sum(e=>e.Forecasts)>1380000)throw new InvalidOperationException("Forecast reserve exhausted.");
        var run=RobustLearningExperiment.Execute(d,raw,b,category,name,policy.Id,policy.Policy,c.Scenario,shortSeconds:duration,twoBends:c.Two,horizonSeconds:.9,normalizeProgress:true,
            fullThrottleCandidate:policy.FullThrottle,trackShape:Variant(c.Geometry),trackAwareness:policy.Awareness,interveneAt:interveneAt,intervention:alternative,holdAt:holdAt,holdAction:held);
        if(run.Metrics.Native.Outcome=="projection")throw new InvalidOperationException("Track/model projection disagreement; not a rider DNF.");return run;
    }
    public static void Run(string d,string raw,string stage){
        if(stage=="freeze"){
            if(File.Exists(Path.Combine(d,"protocol.json")))throw new IOException("Protocol exists; preserve evidence.");Directory.CreateDirectory(d);
            Save(d,"protocol.json",new{BaseHead,Seeds,Training,Selection,Diagnostic,FinalSingle,FinalTwo,Transfer,StartingPolicy=StartPolicy(),StartingPolicyHash=Hash(StartPolicy()),Sources=Sources(),Limits,
                NativeWorldLimit=600,CpuSeconds=6500,Forecasts=1400000,Contacts=125000000,GenerationCpuSeconds=600,EpisodeCpuSeconds=30,
                Basis133=new{Worlds=636,CpuSeconds=4617.59375,Forecasts=1235197,Contacts=111767891},
                Geometry="Train/select progressive-b + egg-medium (different curvature and width profiles). Main held progressive-c24single/12two; transfer held egg-asymmetric6single/6two. Neither held geometry enters preflight/training/selection driving. Cheap static shape validation may inspect all frozen geometries without rider outcomes.",
                Mechanism="Only geometry-speed utility changes in B, same GeometrySpeedWeight and seven other original terms/weights. Read-only adaptive20-60m profile plus next-corner descriptor. Replace single-point geometry-speed penalty by spatial continuation heuristic: actual H09 endpoint, same-angle H09 coast response, tyre capacity, actuator delay, observed yaw authority, and optimistic width affordance. No fixed line, speed map, forced coasting or turn point. Extrapolated arrival/coast/width estimates are explicitly uncertified; H09 safety and native certificate unchanged. Threatened endpoints keep old cost. Eight same parameters/bounds; no133 B1 objective.",
                CEM="2seeds x2arms x4generations x5policies x3shared cases=240. One single+two uninterrupted two bends; two geometry cohorts. Same132 R0, DrawPolicy,32normal draws/generation,2elites,.4/.6mean,std floor.15range. Clone archive distributions on resume. No extra generations.",
                SelectionRule="Per arm/seed up to2 distinct vectors: final R0 winner and best R0 winner on recurring even cohort. Add unchanged12023 and131 references; at most10x6=60 selection worlds within72cap. Same native R0 for both arms. Freeze A/B and all final aliases/hashes at0 final worlds; no swap after opening final.",
                Preflight="Four frozen cases on training geometries, unchanged12023 A/B before learning. Geometry and H09/native continuation parity must pass; native repeats/OFF exact. Diagnose when B loses >1 completion or common two median regresses>10%; training remains closed until cause resolved. No final outcomes to decide training. Counterfactual timing uses only finite sampled maneuvers, not a global last-reaction claim.",
                Reserve="Before each training generation stop if native+offline CPU>3800 or contacts>70M or forecasts>800k, leaving2700CPU/55Mcontacts/600kforecasts for selection/final. No raising budgets to fill generations. Every native world reserved before creation.",
                PASS="24/24 single12/12 two on progressive-c; paired median two<=-2% vs133A0; paired median single<=+5%, max<=+15%; transfer at least as reliable as trainedA and measurable geometry-specific benefit; unchanged physics/safety/defaultOFF, independent and reproducible. PARTIAL for valid but insufficient benefit; FAIL for geometry/physics/queue/leak/reproduction error.",RawDirectory=Path.GetFullPath(raw)});return;
        }
        foreach(var x in Read<JsonElement>(d,"protocol.json").GetProperty("Sources").EnumerateObject())if(Sources()[x.Name]!=x.Value.GetString())throw new InvalidOperationException("Frozen source changed: "+x.Name);
        using var b=new RobustBudget(d,Limits,600,6500,125000000);
        if(stage=="geometry"){TrackAwarenessAudit134.Geometry(d,raw,b);return;}
        if(stage=="audit"){TrackAwarenessAudit134.Audit(d,b);return;}
        if(stage=="preflight"){TrackAwarenessAudit134.Preflight(d,raw,b);return;}
        if(stage=="train"){
            if(!Read<JsonElement>(d,"preflight.json").GetProperty("TrainingGatePassed").GetBoolean()||File.Exists(Path.Combine(d,"training-complete.json")))throw new InvalidOperationException("Training gate closed.");
            foreach(var seed in Seeds)foreach(var aware in new[]{false,true})Train(d,raw,b,$"{(aware?"B":"A")}-s{seed}",seed,aware);
            Save(d,"training-complete.json",new{Worlds=b.Entries.Count(e=>e.Category=="train-ab"),FinalWorlds=0});return;
        }
        if(stage=="select"){Select(d,raw,b);return;}
        if(stage=="freeze-winners"){Freeze(d,b);return;}
        if(stage=="final"){Final(d,raw,b);return;}
        throw new ArgumentException("freeze|geometry|audit|preflight|train|select|freeze-winners|final");
    }
    private static void Train(string d,string raw,RobustBudget b,string label,uint seed,bool aware){
        var file="generations-"+label+".json";var gs=File.Exists(Path.Combine(d,file))?Read<List<RobustGeneration>>(d,file):new();
        var random=new FreeRacingExperiment.RandomStream(seed);for(int i=0;i<gs.Count*32;i++)random.Normal();
        var incumbent=gs.Count>0?gs[^1].BestPolicy:StartPolicy();var mean=gs.Count>0?(double[])gs[^1].Mean.Clone():incumbent.Values;
        var std=gs.Count>0?(double[])gs[^1].Std.Clone():mean.Select((_,i)=>(FreeLearningPolicy.Upper[i]-FreeLearningPolicy.Lower[i])*.3).ToArray();
        for(int g=gs.Count;g<4;g++){
            if(b.Entries.Sum(e=>e.CpuSeconds)>3800||b.Entries.Sum(e=>e.Evaluations)>70000000||b.Entries.Sum(e=>e.Forecasts)>800000)throw new InvalidOperationException("Independent evaluation reserve reached.");
            var cpu=Process.GetCurrentProcess().TotalProcessorTime;var cases=Schedule(g);var cs=new List<RobustEvaluation>();
            for(int c=0;c<5;c++){
                var id=14000+(seed==Seeds[1]?2000:0)+(aware?1000:0)+g*5+c;var policy=RobustLearningExperiment.DrawPolicy(random,incumbent,mean,std,c);
                var p=new AwarenessPolicy134(label,id,policy,aware);
                cs.Add(new(label,g,id,policy,cases.Select(s=>s.Geometry+"-"+s.Name+(s.Two?"-two":"-single")).ToArray(),
                    cases.Select(s=>Execute(d,raw,b,"train-ab",$"{label}-g{g:D2}-p{id}-{s.Geometry}-{s.Name}",p,s).Metrics).ToArray()));
            }
            var ranked=FullThrottleLearningExperiment.Rank(cs).ToArray();incumbent=ranked[0].Policy;
            for(int i=0;i<8;i++){var m=ranked.Take(2).Average(c=>c.Policy.Values[i]);mean[i]=.4*mean[i]+.6*m;std[i]=Math.Max(.15*(FreeLearningPolicy.Upper[i]-FreeLearningPolicy.Lower[i]),Math.Sqrt(ranked.Take(2).Average(c=>Math.Pow(c.Policy.Values[i]-m,2))));}
            var gen=new RobustGeneration(label,g,(g+1)*15,cs.ToArray(),ranked[0].Id,incumbent,(double[])mean.Clone(),(double[])std.Clone(),(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,cs.Sum(c=>c.Results.Sum(r=>r.Native.PhysicsEvaluations)),cs.Sum(c=>c.Results.Sum(r=>r.Native.Forecasts)));
            gs.Add(gen);Save(d,file,gs);Console.WriteLine($"GEN {label}/{g}: {cs.Sum(RobustRanking.Completed)}/15 best={gen.BestId} CPU={gen.CpuSeconds:F2}");
            if(gen.CpuSeconds>600)throw new InvalidOperationException("Generation CPU limit.");
        }
    }
    private static AwarenessPolicy134[] Shortlist(string d,string label,bool aware){
        var gs=Read<RobustGeneration[]>(d,"generations-"+label+".json");var last=FullThrottleLearningExperiment.Rank(gs[^1].Candidates).First();
        var anchor=FullThrottleLearningExperiment.Rank(gs.Where(g=>g.Generation%2==0).Select(g=>FullThrottleLearningExperiment.Rank(g.Candidates).First())).First();
        return new[]{last,anchor}.DistinctBy(x=>Hash(x.Policy)).Select(x=>new AwarenessPolicy134(label,x.Id,x.Policy,aware)).ToArray();
    }
    private static void Select(string d,string raw,RobustBudget b){
        if(!File.Exists(Path.Combine(d,"training-complete.json"))||b.Entries.Any(e=>e.Category=="final"))throw new InvalidOperationException("Selection stage closed.");
        var cs=Seeds.SelectMany(s=>new[]{false,true}.SelectMany(a=>Shortlist(d,$"{(a?"B":"A")}-s{s}",a))).Concat(new[]{new AwarenessPolicy134("133-A0",12023,StartPolicy(),false),new("131-H09",4061,RiderDecisionExperiment133.StartPolicy(),false,false)}).ToArray();
        Save(d,"selection-candidates.json",cs);var scores=new List<RobustEvaluation>();
        foreach(var c in cs){scores.Add(new(c.Label,-1,c.Id,c.Policy,Selection.Select(s=>s.Geometry+"-"+s.Name+(s.Two?"-two":"-single")).ToArray(),Selection.Select(s=>Execute(d,raw,b,"selection",$"select-{c.Label}-{c.Id}-{s.Geometry}-{s.Name}",c,s).Metrics).ToArray()));Save(d,"selection.json",scores);}
        Save(d,"choices.json",new[]{false,true}.Select(a=>{var best=FullThrottleLearningExperiment.Rank(scores.Where(s=>s.Variant.StartsWith(a?"B-":"A-",StringComparison.Ordinal))).First();return cs.Single(c=>c.Id==best.Id) with{Label=a?"134-B":"134-A"};}).ToArray());
    }
    private static void Freeze(string d,RobustBudget b){
        if(File.Exists(Path.Combine(d,"winner-lock.json"))||b.Entries.Any(e=>e.Category=="final"))throw new InvalidOperationException("Final already opened/sealed.");
        var choices=Read<AwarenessPolicy134[]>(d,"choices.json");var plan=new[]{new AwarenessPolicy134("131-H09",4061,RiderDecisionExperiment133.StartPolicy(),false,false),new("133-A0",12023,StartPolicy(),false)}.Concat(choices).ToArray();
        Save(d,"final-plan.json",plan);Save(d,"winner.json",choices.Single(c=>c.Awareness));Save(d,"winner-lock.json",new{PlanHash=Hash(plan),WinnerHash=Hash(choices.Single(c=>c.Awareness)),FinalWorlds=0,ReservationCount=b.Entries.Count});
    }
    private static string Semantics(AwarenessPolicy134 p)=>Hash(new{p.Policy,p.Awareness,p.FullThrottle});
    private static void Final(string d,string raw,RobustBudget b){
        var plan=Read<AwarenessPolicy134[]>(d,"final-plan.json");var seal=Read<JsonElement>(d,"winner-lock.json");
        if(Hash(plan)!=seal.GetProperty("PlanHash").GetString()||Hash(Read<AwarenessPolicy134>(d,"winner.json"))!=seal.GetProperty("WinnerHash").GetString())throw new InvalidOperationException("Frozen choice changed.");
        var aliases=plan.ToDictionary(c=>c.Label,c=>plan.First(p=>Semantics(p)==Semantics(c)).Label);Save(d,"final-aliases.json",aliases);var results=new List<RobustEvaluation>();
        foreach(var c in plan)foreach(var suite in new[]{"single","two","transfer"}){
            var cases=suite=="single"?FinalSingle:suite=="two"?FinalTwo:Transfer;
            var metrics=aliases[c.Label]==c.Label?cases.Select(s=>Execute(d,raw,b,"final",$"final-{c.Label}-{s.Geometry}-{s.Name}",c,s).Metrics).ToArray():results.Single(r=>r.Variant==aliases[c.Label]+"-"+suite).Results;
            results.Add(new(c.Label+"-"+suite,-1,c.Id,c.Policy,cases.Select(s=>s.Geometry+"-"+s.Name).ToArray(),metrics));Save(d,"final-results.json",results);
        }
    }
}
