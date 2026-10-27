using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CoreSim;
namespace SharedWorldSpeedway;

public sealed record NativeCase139(string Name,string Pool,AwarenessCase134 Case,int Corner,string BaselineDirectory,string BaselineName);
public sealed record NativeResult139(string Name,string Arm,NativeCase139 Case,string Variant,RobustMetrics Metrics,double? PreparationTime,double? OpenRequestTime,bool ExactInitial,bool PrefixExact,string Execution);

public static class PhysicsExperiment139
{
    public const string BaseHead="713b0cc4274efbc430d51d0a3874a658defe240a";
    public static FreeLearningPolicy Policy=>CausalYawExperiment137.Policy;
    public static readonly string Evidence="docs/experiments/motorcycle-physics-validation";
    public static NativeCase139[] Cases()=>new[]{
        new NativeCase139("D0","diagnostic",CausalYawExperiment137.Diagnostic[0],1,PhysicsAudit139.Old+"/native","off-D0"),
        new("D1","diagnostic",CausalYawExperiment137.Diagnostic[1],2,PhysicsAudit139.Old+"/native","off-D1"),
        new("H0","confirmation",CausalYawExperiment137.Confirmation[0],1,"docs/experiments/causal-yaw-throttle/native","extended-V0-b-A"),
        new("H1","confirmation",CausalYawExperiment137.Confirmation[3],2,"docs/experiments/causal-yaw-throttle/native","extended-V1-c-A")};
    public static Dictionary<string,string> Sources()=>Directory.GetFiles("tools/shared-world-speedway","*.cs",SearchOption.AllDirectories)
        .Concat(Directory.GetFiles("tools/motorcycle-physics-validation","*.*").Where(x=>x.EndsWith(".cs")||x.EndsWith(".py")||x.EndsWith(".csproj"))).Order()
        .ToDictionary(x=>x.Replace('\\','/'),x=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(x).Replace("\r\n","\n")))));
    static void Save(string d,string n,object v)=>PhysicsAudit139.Save(d,n,v);
    static T Read<T>(string d,string n)=>PhysicsAudit139.Read<T>(d,n);
    static RobustEpisode Baseline(NativeCase139 c)=>RobustLearningExperiment.ReadEpisode(c.BaselineDirectory,c.BaselineName);
    public static float PreparationAngle(FreeRacingInput input){
        var forward=new Vector2(MathF.Cos(input.Rider.Heading),MathF.Sin(input.Rider.Heading));var left=new Vector2(-forward.Y,forward.X);
        var vf=TwoContactForces.PointVelocity(input.Rider.Velocity,input.Rider.AngularVelocity,forward*TwoContactBikeParameters.Default.FrontAxleFromComMeters);
        // Controlled diagnostic perturbation: half the local front zero-slip angle,
        // capped at15deg. No target path, body heading write or learned preference.
        return (float)Math.Clamp(.5*Math.Atan2(Vector2.Dot(vf,left),Vector2.Dot(vf,forward)),-15*Math.PI/180,15*Math.PI/180);
    }
    static NativeResult139 Native(string d,string raw,RobustBudget budget,NativeCase139 c,string arm,PhysicsVariant139? variant=null,bool off=false){
        variant??=PhysicsVariant139.Original;var name=off?"off-"+c.Name:arm=="sensitivity"?"sensitivity-"+c.Name+"-"+variant.Name:c.Name+"-"+arm;
        if(File.Exists(Path.Combine(d,"result-"+name+".json")))return Read<NativeResult139>(d,"result-"+name+".json");
        var frame=TrackAwarenessExperiment134.Frame(c.Case.Geometry);var surface=MultiActionExperiment135.Surface(frame);var spec=FreeLearningExperiment.Rider(c.Case.Scenario);var p=FreeLearningExperiment.Parameters();
        var exit=SpeedThrottleAudit136.Exit(frame,c.Corner);var logs=new List<object>();double? preparation=null,opened=null;FreeRacingAction? held=null;
        FreeLearningDecision[]? tape=null;FreeLearningDecision[]? fallback=null;
        if(arm=="sensitivity"){
            tape=RobustLearningExperiment.ReadEpisode(raw,c.Name+"-B").Replay.Decisions;
            fallback=Baseline(c).Replay.Decisions;
        }
        FreeLearningDecision Choose(FreeRacingInput i){
            if(tape is not null){
                var recorded=tape.LastOrDefault(x=>x.Time<=i.Time+1e-8);
                if(i.Time>tape[^1].Time+1e-8)recorded=fallback!.LastOrDefault(x=>x.Time<=i.Time+1e-8)??tape[^1];
                return new(i.Time,recorded!.Action,FreeActionSafety.Uncertain,false,Array.Empty<FreeLearningCandidate>(),0,0);
            }
            var old=MultiActionPlanner135.Baseline(i,Policy,spec,frame,surface,p);if(off)return old;
            var remaining=exit-i.Rider.TotalReferenceMeters;var inBend=i.Rider.Road.IsTurn&&i.Rider.TotalReferenceMeters>=frame.SegmentStartMeters(c.Corner==1?1:5);
            string reason="frozen134A";var action=old.Action;
            if(arm is "B" or "C"&&preparation is null&&inBend&&remaining<=40){
                preparation=i.Time;action=new(PreparationAngle(i),(float)Policy.Drive);reason="one causal early-preparation request; half front local zero-slip angle";
            }
            // An observed geometric timing cue for this controlled physical test,
            // never a production cornerExit->throttle1 rule. Full reaction remains.
            if(opened is null&&inBend&&remaining<=Math.Max(5,frame.ReferenceSpeed(i.Rider.Road,i.Rider.Speed)*(.24+.2)+1)){
                opened=i.Time;held=new(i.Active.SteeringRadians,1);action=held.Value;reason="request full gas; activation at observed time+.24";
            }
            else if(opened is{} at&&arm is "A" or "B"&&i.Time<at+.6-1e-8){action=held!.Value;reason="bounded .6s request pulse; never1.4s";}
            // C executes only the first full request, then the unchanged causal
            // #134A chooses BOTH channels anew at every real .2s review.
            logs.Add(new{i.Time,RemainingMeters=remaining,Reason=reason,Action=action,Original=old.Action,Active=i.Active,Pending=i.Pending,Preparation=preparation,OpenRequest=opened});
            return old with{Action=action};
        }
        var run=PhysicsNative139.Execute(d,raw,budget,"native",name,16015,Policy,c.Case.Scenario,twoBends:true,horizonSeconds:.9,normalizeProgress:true,fullThrottleCandidate:true,
            trackShape:TrackAwarenessExperiment134.Variant(c.Case.Geometry),chooser:Choose,extendAfterSecondStraight:!off,validationVariant:variant);
        var baseline=Baseline(c);var first=run.Replay.Samples[0].Input;var origin=baseline.Replay.Samples[0].Input;
        var same=first.Rider==origin.Rider&&first.Omega==origin.Omega&&first.Steering==origin.Steering&&first.Pending.SequenceEqual(origin.Pending);
        bool prefix=true;
        if(!off&&arm!="sensitivity")prefix=MultiActionExperiment135.Prefix(baseline,run,preparation??opened??0,.24);
        if(off&&run.Replay.Metrics.Fingerprint!=baseline.Replay.Metrics.Fingerprint)throw new InvalidOperationException("Diagnostic OFF world differs from saved original.");
        if(!same||!prefix)throw new InvalidOperationException("Initial native state/pending/prefix changed.");
        ThrottleSteering138.Zip(Path.Combine(d,"plans-"+name+".json.gz"),logs);
        var row=new NativeResult139(name,arm,c,variant.Name,run.Metrics,preparation,opened,same,prefix,
            arm=="sensitivity"?"Prescribed originalB command tape; if B DNF, historical134A timeline afterwards. All requests retain .24s queue. Not autonomous.":off?"Original frozen134A; exact native fingerprint required":"Controlled preparation/pulse followed by causal frozen134A, never future native states.");
        Save(d,"result-"+name+".json",row);return row;
    }
    public static void Run(string d,string raw,string stage){
        Directory.CreateDirectory(d);Directory.CreateDirectory(raw);
        if(stage=="freeze"){
            if(File.Exists(Path.Combine(d,"protocol.json")))throw new IOException("Fresh evidence required; never overwrite freeze.");
            Save(d,"contact-states.json",PhysicsAudit139.States());Save(d,"cases.json",Cases());
            Save(d,"protocol.json",new{BaseHead,PolicyId=16015,Policy,Sources=Sources(),SourceNormalization="CRLF to LF only",NativeCases=Cases(),
                Ordering="Code audit + saved138 telemetry, frozen force maps (0worlds), explicit native hypothesis lock, preflight, diagnostics, lock before held cases, confirmation. No CEM or objective changes.",
                MapRanges=new{SteerDegrees="-35..35 step1, and beta/speed cuts step5",BetaDegrees="-45..45 step5",SpeedMps=new[]{10,15,20,25,30},YawRadPerSecond=new[]{0d,.5,1,2.875},
                    Kappa=new[]{-.5,0,.1,.25,.5,1,2,2.56,3,5},Gas=new[]{0,Policy.Drive,1},FrontLoadFraction=new[]{.3,.35,.4,6d/13,.5,.55,.6},FrontArmMeters=new[]{.55,.6,.65,.7,.75},WheelbaseMeters=1.3,
                    WheelInertia=new[]{.45,.9,1.8},TemporalRelaxationHypothesisSeconds=new[]{.01,.03,.06,.1}},
                MapMeaning="Original memoryless contact functions; dt0 queries are NOT native rollouts. Hypothetical perturbed states are not proved reachable. Zero yaw acceleration is not stability. Gas changes torque/omegaDot immediately, not Fx/Fy at fixed current omega.",
                NativeArms=new{A="Historical134A state, no preparation; .6s full-gas requested pulse near exit then restore frozenA",
                    B="One earlier .2s request at observed remaining<=40m: partial gas + half front zero-slip angle, capped15deg; then frozenA; same .6s full pulse",
                    C="Identical earlier preparation and first full request, then causal frozen134A re-evaluates gas AND steering every.2s. Never force1.4s full gas."},
                PulseTiming="At first native review in studied bend with remaining<=max(5,ReferenceSpeed*(.24+.2)+1)m request full gas at current active steering. Original .24s queue/90deg-s/35deg remain. Actual opening/exit gas measured, not guaranteed.",
                Sensitivity=PhysicsVariant139.NativeSensitivity,
                SensitivityMeaning="All variants UNCALIBRATED sensitivity, not calibration. Identical initial physical state and B command tape on diagnostic D0/D1. Front-slope-half changes only regularized front slope; rear-long-scale2 one original stiffness parameter; front-load40 static split at fixed arms; yaw-inertia125 only Box2D inertia. No engine/ratio/default change.",
                OtherSensitivity="Geometry/COM and numerical contact regularization: static maps only. Temporal relaxation: analytical uncalibrated step response only, not invented native force lag. No native completion/stability claim for these plots.",
                Limits=new{Worlds=24,CpuSeconds=450,ContactEvaluations=12000000,Forecasts=200000,EpisodeCpuSeconds=30},
                Allocation="2OFFpreflight+4states*3arms+2diagnosticstates*4sensitivity=22plannedworlds;2reserve never required. No reruns or budget increases. All DNFs retained.",
                Gates=new{PreflightExactFingerprint=true,FrontDriveToleranceNewtons=.001,ForceReproductionToleranceNewtons=.001,EnergyGuard="Original inherited strict guard",BetaDegrees=45,
                    PreparationClaim="At least2physically different cases show legal B improvement vsA with same fullpulse; no universal preparation claim from one case",
                    ShortCorrectionClaim="C must retain legal long scope and active gas1 at exit on2different cases; never treat extra gas time alone as success",
                    StabilityClaim="No stable trajectory claim from tau0 alone: betaDot, chiDot, omegaDot, speed evolution and native continuation required",
                    ModelVerdict="One of CONSISTENT,DEFICIENCY,STATE/PREPARATION,MIXED,INCONCLUSIVE; conservation alone does not validate tyres, no110km/h fitting"},
                FrozenBeforeNewPhysicsQueriesAndWorlds=true,ProductionDefaultChanged=false});return;
        }
        var protocol=Read<JsonElement>(d,"protocol.json");foreach(var x in protocol.GetProperty("Sources").EnumerateObject()){
            var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(x.Name).Replace("\r\n","\n"))));if(hash!=x.Value.GetString())throw new IOException("Frozen source changed: "+x.Name);}
        using var budget=new RobustBudget(d,new Dictionary<string,int>{{"native",24},{"offline",40}},24,450,12000000);
        if(budget.Entries.Any(x=>!x.Complete))throw new IOException("Incomplete reservation. Preserve it; do not retry.");
        if(stage=="maps"){PhysicsAudit139.Run(d,budget);return;}
        if(!Read<JsonElement>(d,"maps-complete.json").GetProperty("Passed").GetBoolean())throw new InvalidOperationException("Maps and exact native force audit first.");
        if(!File.Exists(Path.Combine(d,"native-hypothesis-lock.json")))throw new InvalidOperationException("Review maps and freeze physical hypothesis before new worlds.");
        if(stage=="preflight"){
            var rows=Cases().Take(2).Select(c=>Native(d,raw,budget,c,"off",off:true)).ToArray();Save(d,"preflight.json",new{Passed=true,Rows=rows,LimitsRetained=true});return;}
        if(!Read<JsonElement>(d,"preflight.json").GetProperty("Passed").GetBoolean())throw new InvalidOperationException("Native OFF preflight required.");
        if(stage is "diagnostic" or "confirmation"){
            if(stage=="confirmation"&&!File.Exists(Path.Combine(d,"confirmation-lock.json")))throw new IOException("Lock before opening held cases.");
            foreach(var c in Cases().Where(x=>x.Pool==stage))foreach(var arm in new[]{"A","B","C"})Native(d,raw,budget,c,arm);
        }else if(stage=="sensitivity")foreach(var c in Cases().Take(2))foreach(var variant in PhysicsVariant139.NativeSensitivity)Native(d,raw,budget,c,"sensitivity",variant);
        else throw new ArgumentException("freeze|maps|preflight|diagnostic|sensitivity|confirmation");
        Save(d,stage+"-complete.json",new{Worlds=budget.Entries.Sum(x=>x.NativeWorlds),CpuSeconds=budget.Entries.Sum(x=>x.CpuSeconds),Contacts=budget.Entries.Sum(x=>x.Evaluations),Forecasts=budget.Entries.Sum(x=>x.Forecasts)});
    }
}
