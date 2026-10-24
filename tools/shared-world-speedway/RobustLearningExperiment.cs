using System.Diagnostics;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>Default-OFF experiment harness. Training, selection and sealed test are distinct stages.</summary>
public static class RobustLearningExperiment
{
    public const string BaseHead="9470f63a11ccc31e6c7bdb862743d11e4be053d4";
    public const uint Seed=1302026;
    public static readonly JsonSerializerOptions Json=FreeLearningExperiment.Json;
    public static readonly FreeLearningScenario[] High={
        new("H0",4,16,3,22,.1f,.02f,.02f),new("H1",4,18,6,22,-.1f,-.02f,-.02f),
        new("H2",4,17,4.5f,22,.15f,-.015f,.025f),new("H3",4,16.5f,7,22,-.12f,.015f,-.025f),
        new("H4",4,16,4.3f,22,-.08f,.025f,-.015f),new("H5",4,18,7.4f,22,.12f,-.025f,.015f),
        new("H6",4,17.5f,2.7f,22,.1f,.015f,-.02f),new("H7",4,16.5f,5.8f,22,-.15f,-.015f,.02f)};
    public static readonly FreeLearningScenario[] Medium={new("M0",3,12,3.5f,22,-.12f,.018f,.025f),new("M1",3,14.5f,6.5f,22,.12f,-.018f,-.025f)};
    public static readonly FreeLearningScenario[] Retention={new("R0",2,9.5f,3.8f,4,.08f,-.025f,.02f),new("R1",2,10.5f,6.2f,22,-.08f,.025f,-.02f)};
    public static readonly FreeLearningScenario[] Selection={
        new("S0",2,8.5f,4.4f,22,.1f,.015f,-.015f),new("S1",2,10.5f,6.7f,22,-.14f,-.025f,.02f),
        new("S2",3,12.5f,3.2f,22,.12f,-.02f,-.025f),new("S3",3,14.5f,5.6f,22,-.1f,.025f,.015f),
        new("S4",4,16.5f,4.1f,22,.15f,.02f,-.02f),new("S5",4,17.5f,7.3f,22,-.12f,-.015f,.025f)};
    public static readonly FreeLearningScenario[] SealedTest={
        new("T00",2,8.75f,2.9f,22,.08f,.015f,.02f),new("T01",2,9.25f,6.8f,22,-.12f,-.025f,-.015f),
        new("T02",2,10.75f,4.6f,22,.15f,-.02f,.025f),new("T03",2,11.25f,7.6f,22,-.18f,.03f,-.02f),
        new("T04",3,12.75f,3.9f,22,.1f,.025f,-.03f),new("T05",3,13.25f,6.1f,22,-.08f,-.015f,.02f),
        new("T06",3,14.75f,5.2f,22,.16f,-.03f,-.015f),new("T07",3,15.25f,2.8f,22,-.12f,.02f,.03f),
        new("T08",4,16.25f,7.1f,22,.18f,.03f,-.02f),new("T09",4,16.75f,4.7f,22,-.1f,-.02f,.025f),
        new("T10",4,17.25f,3.4f,22,.14f,-.025f,.015f),new("T11",4,17.75f,6.6f,22,-.15f,.015f,-.025f)};
    public static FreeLearningScenario[] Schedule(string variant,int generation){
        var block=generation%2;
        return variant=="A"?High.Skip(block*4).Take(4).ToArray():variant=="B"?
            new[]{High[block*4],High[block*4+1],Medium[block],Retention[block]}:throw new ArgumentException("A or B required.");
    }
    public static FreeLearningPolicy Reference(string id){
        var name=id switch{"129-100"=>"after-100","129-200"=>"after-200","129-final"=>"final",_=>throw new ArgumentException("Frozen reference required.")};
        return FreeLearningExperiment.Read<Dictionary<string,FreeLearningPolicy>>(PhysicalSteeringAudit.PreviousPath("../free-racing-learning"),"milestones.json")[name];
    }
    public static void Save(string directory,string name,object value){
        Directory.CreateDirectory(directory);var path=Path.Combine(directory,name);File.WriteAllText(path+".tmp",JsonSerializer.Serialize(value,Json));File.Move(path+".tmp",path,true);
    }
    public static T Read<T>(string directory,string name)=>JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(directory,name)),Json)!;
    public static string RawPath(string raw,string name)=>Path.Combine(raw,name+".json.gz");
    public static RobustEpisode ReadEpisode(string raw,string name){using var z=new GZipStream(File.OpenRead(RawPath(raw,name)),CompressionMode.Decompress);return JsonSerializer.Deserialize<RobustEpisode>(z,Json)!;}
    private static void SaveEpisode(string raw,RobustEpisode run){
        Directory.CreateDirectory(raw);var path=RawPath(raw,run.Replay.Name);
        using(var z=new GZipStream(File.Create(path+".tmp"),CompressionLevel.SmallestSize))JsonSerializer.Serialize(z,run,Json);
        File.Move(path+".tmp",path,true);
    }
    public static readonly string[] SourceNames={"FreeLearningController.cs","FreeLearningExperiment.cs","FreeRacingModel.cs","WorldRunner.cs","WorldModel.cs",
        "ContactAdapter.cs","TwoContactBikeDynamics.cs","RearWheelDynamics.cs","Engine500.cs","LoadBasedTyreGrip.cs","TrackFrame.cs","CurveBoundaryAudit.cs",
        "RobustLearningModel.cs","RobustLearningExperiment.cs","RobustLearningAudit.cs","RobustSweepCertificate.cs"};
    public static void VerifySources(string directory){
        var p=Read<JsonElement>(directory,"protocol.json");foreach(var source in p.GetProperty("Sources").EnumerateObject())
            if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes("tools/shared-world-speedway/"+source.Name)))!=source.Value.GetString())throw new InvalidOperationException("Frozen #130 source changed: "+source.Name);
    }
    public static void Run(string directory,string raw,string stage){
        raw=Path.GetFullPath(raw);Directory.CreateDirectory(raw);Directory.CreateDirectory(directory);
        if(stage=="freeze"){
            if(File.Exists(Path.Combine(directory,"protocol.json")))throw new IOException("Protocol already frozen.");
            Save(directory,"protocol.json",new{BaseHead,Seed,RawDirectory=raw,RawAvailability="Full native episodes stay local and untracked; Git contains hashes, all metrics and selected native replays only.",
                StartingCheckpoint="129-final: policy161, generation32; chosen as the latest checkpoint, not from known holdout results.",StartingPolicy=Reference("129-final"),
                ReferencePolicies=new[]{"129-100","129-200","129-final"}.ToDictionary(x=>x,Reference),High,Medium,Retention,Selection,SealedTest,
                ScheduleA=Enumerable.Range(0,5).Select(g=>Schedule("A",g).Select(s=>s.Name)),ScheduleB=Enumerable.Range(0,15).Select(g=>Schedule("B",g).Select(s=>s.Name)),
                CandidateCount=5,EpisodesPerCandidate=4,InitialGenerationsPerVariant=5,MaximumAdditionalBGenerations=10,
                Limits=new{TrainA=100,TrainB=300,Selection=72,SealedTest=48,Diagnostic=8,TwoBends=3,OfflineStages=8,NativeWorlds=531,
                    CpuSeconds=RobustBudget.CpuLimitSeconds,ContactEvaluations=RobustBudget.ContactLimit,EpisodeCpu=RobustBudget.EpisodeCpuSeconds,GenerationCpu=RobustBudget.GenerationCpuSeconds},
                CostBasis129=new{NativeWorlds=442,CpuSeconds=1347.953125,Contacts=18323957,MeanCpuPerWorld=1347.953125/442},
                CEM="Same seed and 32 normal draws per generation in both branches. Incumbent +3 Gaussian mutations +tanh-normal immigrant, same eight ranges as129. Two elites, mean0.4old+0.6elite, std floor15% range; no hybrid policy.",
                Ranking="Completed count first; worst failed legal progress; confirmed crossing/control count; worst all-completed time in50ms bins; mean in25ms bins; certified reserve; minimum exit speed; exit yaw; mean legal progress; stableID. Never average away DNFs.",
                MainCertificate="Exactly the legacy129 conservative swept bound, unchanged margins. Report confirmed crossing only below an additional1.6mm uncertainty allowance; otherwise uncertified interval. Refined certificate is isolated diagnostic only.",
                Horizon=.6,Reaction=.24,MaximumSteeringDegrees=35,SteeringRateDegreesPerSecond=90,
                Shortlist="Per variant: last incumbent and best archived winner of the recurring even-generation group. Selection evaluates each unique policy on the SAME six S scenarios; seed161 is a safety fallback. Old40/72 archived as references, never selected using old holdouts.",
                Extension="B must improve selection completions over seed and match/exceed A, or complete all6 with >=0.10s worst-time gain over seed and no lower certified reserve. No increase of confirmed/control failures. At most200 more B episodes; stop after two recurring-anchor evaluations without >=0.05s or completion improvement, or budget/unsafe plateau.",
                FinalChoice="Rank shortlisted policies by the same hierarchy on S only; stableID prefers seed in exact ties. Freeze final policy before ANY T outcome. The sealed T set never controls CEM, shortlist, stopping, bounds or variant choice.",
                TwoBendGate="Only if selected130 completes12/12 T and has at least as many completions as every129 reference. Then one uninterrupted native world, same queue, wheel and controller through two bends; otherwise skipped.",
                PredictionTolerances=new{XY=.1,Speed=.2,ChiDegrees=1,BetaDegrees=1,SteeringDegrees=1.5,Omega=2,YawRate=.05,ClearanceOptimism=.1},
                Sources=SourceNames.ToDictionary(n=>n,n=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes("tools/shared-world-speedway/"+n))))});return;
        }
        VerifySources(directory);using var budget=new RobustBudget(directory);
        if(stage=="audit"){RobustLearningAudit.Offline(directory,raw,budget);return;}
        if(stage=="diagnose"){RobustLearningAudit.Diagnose(directory,raw,budget);return;}
        if(stage=="preflight"){
            foreach(var s in new[]{Schedule("B",0)[^1],Schedule("B",1)[2]})Execute(directory,raw,budget,"diagnostic","preflight-"+s.Name,0,Reference("129-final"),s,shortSeconds:1);
            Save(directory,"preflight.json",new{Passed=true,Rule="Targeted code tests + legal initial footprints + unchanged native actuator/energy/projection invariants. No sealed test native worlds."});return;
        }
        if(stage=="train"){Train(directory,raw,budget);return;}
        if(stage=="select"){SelectAndFreeze(directory,raw,budget);return;}
        if(stage=="test"){FinalTest(directory,raw,budget);return;}
        throw new ArgumentException("freeze|audit|diagnose|preflight|train|select|test");
    }

    public static RobustEpisode Execute(string directory,string raw,RobustBudget budget,string category,string name,int id,FreeLearningPolicy policy,
        FreeLearningScenario scenario,double? shortSeconds=null,bool refinedDiagnostic=false,double? interveneAt=null,FreeRacingAction? intervention=null,bool twoBends=false,
        double horizonSeconds=.6,bool normalizeProgress=false,double? holdAt=null,FreeRacingAction? holdAction=null,bool fullThrottleCandidate=false,bool decisionObjective=false){
        var previousReservation=budget.Entries.SingleOrDefault(e=>e.Name==name);
        if(previousReservation is not null){
            if(!previousReservation.Complete||previousReservation.RawSha256!=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(RawPath(raw,name)))))throw new IOException("Incomplete/corrupt attempt; never silently repeat.");
            var cached=ReadEpisode(raw,name);if(cached.Replay.Policy!=policy||cached.Replay.Scenario!=scenario)throw new InvalidOperationException("Cache conditions differ.");return cached;
        }
        var reservation=budget.Reserve(name,category);var cpu=Process.GetCurrentProcess().TotalProcessorTime;
        var spec=FreeLearningExperiment.Rider(scenario);var p=FreeLearningExperiment.Parameters();var frame=RacingLineRootCauseAudit.Frame();
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var boundary=new RacingLineGeometry(frame);
        var samples=new List<FreeRacingSample>();var telemetry=new List<RobustTelemetry>();TwoContactForces force=default;RearWheelStep wheel=default;RiderView before=default;
        string? failure=null;string boundaryClass="none";double? gateTime=null,entrySpeed=null,exitSpeed=null,exitYaw=null,cornerMin=null;
        double legalProgress=0,legalSeconds=0,minCertified=double.PositiveInfinity,minLegacy=double.PositiveInfinity,minRefined=double.PositiveInfinity;
        double innerMin=double.PositiveInfinity,outerMin=double.PositiveInfinity,minSpeed=double.PositiveInfinity,maxBeta=0,maxYaw=0;
        double driveIntegral=0,fullDrive=0,boundaryApproach=0,recovery=0,previousProgress=0,stall=0,engineWork=0,dissipation=0;
        double lastOffset=scenario.Offset,lastExtreme=scenario.Offset;int direction=0,oscillations=0;long certificateQueries=0;
        using var world=new WorldRunner(new[]{spec},parameters:p,trackShape:RacingLineRootCauseAudit.Variant(),frozenSurface:surface,
            twoContactInitial:new(scenario.HeadingError,scenario.YawRate),freeLearningPolicy:policy,
            diagnostics:new(){Force=f=>before=f.Rider,TwoContact=o=>force=o.Forces,RearWheel=o=>wheel=o.Step});
        var mechanical=world.MechanicalProperties(1);
        if(interveneAt is not null||horizonSeconds!=.6||normalizeProgress||holdAt is not null||fullThrottleCandidate||decisionObjective)world.FreeLearning!.DiagnosticChooser=input=>{
            if(holdAt is{} held&&input.Time>=held-1e-8&&holdAction is{} heldAction)
                return new(input.Time,heldAction,FreeActionSafety.Uncertain,false,Array.Empty<FreeLearningCandidate>(),0,0);
            var d=FreeLearningController.Choose(input,policy,spec,frame,surface,p,mechanical.MassKg,mechanical.InertiaKgMetersSquared,horizonSeconds,normalizeProgress,fullThrottleCandidate,decisionObjective);
            return interveneAt is{} observation&&intervention is{} alternate&&Math.Abs(input.Time-observation)<1e-8?d with{Action=alternate}:d;
        };
        var gateReference=frame.SegmentStartMeters(twoBends?8:4)+3;var gate=frame.At(gateReference,0);
        var gatePoint=new MeterPoint(gate.Position.X,gate.Position.Y);var tangent=new MeterPoint(gate.Road.Tangent.X,gate.Road.Tangent.Y);
        double WholeGate(RiderView v){var bike=BikeFootprint.Create(new(v.Position.X,v.Position.Y),v.Heading,SpeedwayBikeDimensions.Reference);
            return new[]{bike.Chassis,bike.Handlebar}.Min(c=>Math.Min(MeterPoint.Dot(c.Start-gatePoint,tangent),MeterPoint.Dot(c.End-gatePoint,tangent))-c.RadiusMeters);}
        void Sample(WorldSnapshot snapshot){var v=snapshot.Riders[0];var gap=boundary.Clearance(v);var c=world.FreeLearning!;
            var input=c.Input(v,snapshot.TimeSeconds,world.RearWheel(1).RearWheelAngularVelocityRadPerSecond,snapshot.Steering![0].AngleRadians);
            var action=c.Decisions.LastOrDefault()?.Action??c.Active;samples.Add(new(input,gap.Inner,gap.Outer,action.SteeringRadians,action.Drive));}
        Sample(world.Snapshot);if(Math.Min(samples[0].Inner,samples[0].Outer)<=0)throw new ArgumentException("Illegal initial footprint.");
        var result=world.Run(name,.01,shortSeconds??(twoBends?50:26),acceptedObserver:snapshot=>{
            var c=world.FreeLearning!;var previous=samples[^1];samples[^1]=previous with{Input=previous.Input with{Rider=before,Active=c.Active,
                Pending=c.Input(before,previous.Input.Time,previous.Input.Omega,previous.Input.Steering).Pending,NextReview=c.NextReview},RequestedSteering=c.Decisions[^1].Action.SteeringRadians,RequestedDrive=c.Decisions[^1].Action.Drive};
            var v=snapshot.Riders[0];var dt=wheel.Seconds;var gap=boundary.Clearance(v);var old=boundary.Clearance(before);var distance=Vector2.Distance(v.Position,before.Position);
            innerMin=Math.Min(innerMin,Math.Min(old.Inner,gap.Inner));outerMin=Math.Min(outerMin,Math.Min(old.Outer,gap.Outer));
            var sweep=.5*(distance+SpeedwayBikeDimensions.Reference.BoundingRadiusMeters*Math.Abs(BikeAngles.Wrap(v.Heading-before.Heading)));
            var legacy=Math.Min(Math.Min(gap.Inner,gap.Outer),Math.Min(old.Inner,old.Outer))-sweep;minLegacy=Math.Min(minLegacy,legacy);
            var accepted=legacy;
            if(refinedDiagnostic&&legacy<0){var proof=RobustSweepCertificate.Refine(before,v,dt,force,mechanical.MassKg,mechanical.InertiaKgMetersSquared,p.SolverSubsteps,boundary);accepted=proof.Minimum;certificateQueries+=proof.Queries;}
            minRefined=Math.Min(minRefined,accepted);minCertified=Math.Min(minCertified,accepted);
            if(accepted<0){boundaryClass=Math.Min(gap.Inner,gap.Outer)<-.0016?"confirmed crossing":"uncertified interval";failure=boundaryClass;}
            var delta=v.TotalReferenceMeters-before.TotalReferenceMeters;
            if(Math.Abs(delta)>distance*1.5+.1)failure="projection";
            if(Vector2.Dot(v.Velocity,v.Road.Tangent)<=0||delta<-.02)failure="reverse";
            if(Math.Abs(v.SlideRadians)>Math.PI/4)failure="control envelope";
            if(!FreeLearningExperiment.SteeringStepIsPhysical(force,(float)dt)||force.TravelSteering is not null||force.SteeringControl is not null)failure="actuator invariant";
            var bodyDelta=TwoContactBikeAudit.BodyKinetic(v,mechanical.MassKg,mechanical.InertiaKgMetersSquared)-TwoContactBikeAudit.BodyKinetic(before,mechanical.MassKg,mechanical.InertiaKgMetersSquared);
            var energy=bodyDelta+.45*(wheel.AfterOmega*wheel.AfterOmega-wheel.BeforeOmega*wheel.BeforeOmega);var loss=wheel.EngineWorkJoules-energy;
            if(-loss>.05+1e-6*Math.Max(TwoContactBikeAudit.BodyKinetic(before,mechanical.MassKg,mechanical.InertiaKgMetersSquared),TwoContactBikeAudit.BodyKinetic(v,mechanical.MassKg,mechanical.InertiaKgMetersSquared)))failure="energy invariant";
            engineWork+=wheel.EngineWorkJoules;dissipation+=loss;
            if(failure is null){legalProgress=Math.Max(legalProgress,v.TotalReferenceMeters-scenario.Start);legalSeconds=snapshot.TimeSeconds;}
            if(legalProgress-previousProgress<.005)stall+=dt;else stall=0;previousProgress=legalProgress;
            if(stall>=1||v.Speed<.5)failure="no progress";
            if(entrySpeed is null&&v.TotalReferenceMeters>=frame.SegmentStartMeters(1))entrySpeed=v.Speed;
            if(v.Road.IsTurn)cornerMin=Math.Min(cornerMin??double.PositiveInfinity,v.Speed);
            minSpeed=Math.Min(minSpeed,v.Speed);maxBeta=Math.Max(maxBeta,Math.Abs(v.SlideRadians)*180/Math.PI);maxYaw=Math.Max(maxYaw,Math.Abs(v.AngularVelocity));
            driveIntegral+=before.Drive*dt;if(before.Drive>=.99)fullDrive+=dt;
            var oldClear=Math.Min(old.Inner,old.Outer);var newClear=Math.Min(gap.Inner,gap.Outer);boundaryApproach+=Math.Max(0,oldClear-newClear);if(newClear>oldClear&&oldClear<1)recovery+=dt;
            var d=Math.Sign(v.Road.OffsetMeters-lastOffset);if(d!=0&&d!=direction&&Math.Abs(lastOffset-lastExtreme)>=1){if(direction!=0)oscillations++;direction=d;lastExtreme=lastOffset;}
            if(direction==0&&d!=0)direction=d;lastOffset=v.Road.OffsetMeters;
            if(failure is null&&gateTime is null&&v.TotalReferenceMeters>=gateReference&&WholeGate(v)>=0){gateTime=snapshot.TimeSeconds;exitSpeed=v.Speed;exitYaw=v.AngularVelocity;}
            if(snapshot.TimeSeconds%1<.011&&(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds>RobustBudget.EpisodeCpuSeconds)failure="episode CPU limit";
            telemetry.Add(new(snapshot.TimeSeconds,dt,force,wheel,bodyDelta,loss,legacy,accepted));Sample(snapshot);
        },stopOnCrash:true,diagnosticStop:s=>failure is not null||gateTime is{} t&&s.TimeSeconds>=t+1-1e-10);
        var completed=failure is null&&gateTime is not null&&result.TimeSeconds>=gateTime+1-1e-8;var ctl=world.FreeLearning!;
        var native=new FreeLearningMetrics(completed,failure??(completed?"completed":shortSeconds is not null?"probe":"timeout"),completed?gateTime:null,result.TimeSeconds,
            legalProgress,legalSeconds,minCertified,entrySpeed,minSpeed,completed?exitSpeed:null,maxBeta,driveIntegral/result.TimeSeconds,fullDrive/result.TimeSeconds,oscillations,
            result.Riders[0].DistanceMeters,boundaryApproach,recovery,result.Work.SolverCalls,result.Work.RearWheelForceEvaluations+result.Work.RearWheelBoundaryEvaluations+ctl.Evaluations,
            ctl.Forecasts,(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,result.Fingerprint);
        var changes=ctl.Decisions.Zip(ctl.Decisions.Skip(1),(a,b)=>Math.Abs(b.Action.SteeringRadians-a.Action.SteeringRadians)*180/Math.PI).ToArray();
        var metrics=new RobustMetrics(native){MinimumInstantInner=innerMin,MinimumInstantOuter=outerMin,MinimumLegacyCertificate=minLegacy,MinimumRefinedCertificate=minRefined,
            CornerMinimumSpeed=cornerMin,MaximumYawRate=maxYaw,ExitYawRate=completed?exitYaw:null,SteeringRequestTravelDegrees=changes.Sum(),MaximumRequestChangeDegrees=changes.DefaultIfEmpty(0).Max(),
            EngineWorkJoules=engineWork,DissipationJoules=dissipation,NoSafeDecisions=ctl.Decisions.Count(d=>d.NoSafeContinuation),BoundaryClass=boundaryClass};
        var replay=new FreeLearningEpisode(name,id,policy,scenario,native,samples.ToArray(),ctl.Decisions.ToArray(),ctl.Activations.ToArray());var episode=new RobustEpisode(replay,metrics,telemetry.ToArray());
        SaveEpisode(raw,episode);budget.Complete(reservation,native.CpuSeconds,native.PhysicsEvaluations,native.Forecasts,metrics,RawPath(raw,name));
        Console.WriteLine($"{category} #{budget.Entries.Count(e=>e.Category==category)} {name}: {native.Outcome} legal={legalProgress:F1} gate={native.GateSeconds} CPU={native.CpuSeconds:F2} certQueries={certificateQueries}");
        if(failure?.Contains("invariant")==true)throw new InvalidOperationException("Physical invariant failed; no training continuation.");return episode;
    }

    private static void Train(string directory,string raw,RobustBudget budget){
        if(!Read<JsonElement>(directory,"preflight.json").GetProperty("Passed").GetBoolean())throw new InvalidOperationException("Preflight gate closed.");
        var a=TrainVariant(directory,raw,budget,"A",5);var b=TrainVariant(directory,raw,budget,"B",5);
        var initialSelection=SelectionRound(directory,raw,budget,a,b,"phase1");Save(directory,"phase1-selection.json",initialSelection);
        var seed=initialSelection.Single(e=>e.Variant=="seed");var bestA=RobustRanking.Rank(initialSelection.Where(e=>e.Variant=="A")).First();var bestB=RobustRanking.Rank(initialSelection.Where(e=>e.Variant=="B")).First();
        var safety=RobustRanking.ConfirmedOrControl(bestB)<=RobustRanking.ConfirmedOrControl(seed)&&RobustRanking.ConfirmedOrControl(bestB)<=RobustRanking.ConfirmedOrControl(bestA);
        var extend=safety&&((RobustRanking.Completed(bestB)>RobustRanking.Completed(seed)&&RobustRanking.Completed(bestB)>=RobustRanking.Completed(bestA))
            ||RobustRanking.Completed(bestB)==6&&RobustRanking.WorstTime(bestB)<=RobustRanking.WorstTime(seed)-.10&&bestB.Results.Min(r=>r.Native.MinimumClearanceMeters)>=seed.Results.Min(r=>r.Native.MinimumClearanceMeters));
        Save(directory,"extension-decision.json",new{Extend=extend,SelectionOnly=true,TestWorlds=budget.Entries.Count(e=>e.Category=="test"),Seed=seed.Id,BestA=bestA.Id,BestB=bestB.Id,
            SeedCompleted=RobustRanking.Completed(seed),ACompleted=RobustRanking.Completed(bestA),BCompleted=RobustRanking.Completed(bestB),NoSafetyRegression=safety,MaximumAdditionalEpisodes=200});
        if(extend)TrainVariant(directory,raw,budget,"B",15,extension:true);
        Save(directory,"training-complete.json",new{A=budget.Entries.Count(e=>e.Category=="train-A"),B=budget.Entries.Count(e=>e.Category=="train-B"),Frozen=true});
    }
    public static RobustGeneration[] TrainVariant(string directory,string raw,RobustBudget budget,string variant,int maximumGenerations,bool extension=false){
        var filename="generations-"+variant+".json";var generations=File.Exists(Path.Combine(directory,filename))?Read<List<RobustGeneration>>(directory,filename):new();
        var random=new FreeRacingExperiment.RandomStream(Seed);var incumbent=generations.Count>0?generations[^1].BestPolicy:Reference("129-final");
        var mean=generations.Count>0?generations[^1].Mean:incumbent.Values;var std=generations.Count>0?generations[^1].Std:mean.Select((_,i)=>(FreeLearningPolicy.Upper[i]-FreeLearningPolicy.Lower[i])*.3).ToArray();
        for(var draw=0;draw<generations.Count*32;draw++)random.Normal();
        var initial=generations.Count;var lastAnchorGain=initial;double previousAnchor=double.NegativeInfinity;
        for(var g=initial;g<maximumGenerations;g++){
            var cpu=Process.GetCurrentProcess().TotalProcessorTime;var cases=Schedule(variant,g);var candidates=new List<RobustEvaluation>();
            for(var c=0;c<5;c++){
                var id=(variant=="A"?1000:2000)+g*5+c;
                var policy=DrawPolicy(random,incumbent,mean,std,c);var metrics=cases.Select(s=>Execute(directory,raw,budget,"train-"+variant,$"{variant}-g{g:D2}-p{id}-"+s.Name,id,policy,s).Metrics).ToArray();
                candidates.Add(new(variant,g,id,policy,cases.Select(s=>s.Name).ToArray(),metrics));
            }
            var ranked=RobustRanking.Rank(candidates).ToArray();var elites=ranked.Take(2).ToArray();incumbent=ranked[0].Policy;
            for(var i=0;i<mean.Length;i++){var m=elites.Average(e=>e.Policy.Values[i]);mean[i]=.4*mean[i]+.6*m;std[i]=Math.Max(.15*(FreeLearningPolicy.Upper[i]-FreeLearningPolicy.Lower[i]),Math.Sqrt(elites.Average(e=>Math.Pow(e.Policy.Values[i]-m,2))));}
            var generation=new RobustGeneration(variant,g,budget.Entries.Count(e=>e.Category=="train-"+variant),candidates.ToArray(),ranked[0].Id,incumbent,(double[])mean.Clone(),(double[])std.Clone(),
                (Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,candidates.Sum(c=>c.Results.Sum(r=>r.Native.PhysicsEvaluations)),candidates.Sum(c=>c.Results.Sum(r=>r.Native.Forecasts)));
            generations.Add(generation);Save(directory,filename,generations);Save(directory,"archive-"+variant+".json",generations.SelectMany(x=>x.Candidates).Select(e=>new{e.Variant,e.Generation,e.Id,e.Policy,e.Scenarios,Completed=RobustRanking.Completed(e),WorstTime=double.IsFinite(RobustRanking.WorstTime(e))?RobustRanking.WorstTime(e):(double?)null}));
            Console.WriteLine($"GEN {variant}/{g}: completed={candidates.Sum(c=>RobustRanking.Completed(c))}/20 incumbent={ranked[0].Id} {RobustRanking.Completed(ranked[0])}/4 CPU={generation.CpuSeconds:F2}");
            if(generation.CpuSeconds>RobustBudget.GenerationCpuSeconds)throw new InvalidOperationException("Generation CPU guard; stop both branches.");
            if(extension&&g%2==0){var anchor=ranked[0];var metric=RobustRanking.Completed(anchor)*1000+(RobustRanking.Completed(anchor)==4?100-RobustRanking.WorstTime(anchor):RobustRanking.WorstFailure(anchor)/10);
                if(metric>previousAnchor+.05){previousAnchor=metric;lastAnchorGain=g;}
                if(g-lastAnchorGain>=4){Save(directory,"extension-stop.json",new{Generation=g,Reason="two recurring-anchor evaluations without50ms/completion gain"});break;}}
            if(budget.Entries.Sum(e=>e.CpuSeconds)>RobustBudget.CpuLimitSeconds-400||budget.Entries.Sum(e=>e.Evaluations)>RobustBudget.ContactLimit-5000000){Save(directory,"extension-stop.json",new{Generation=g,Reason="reserve final selection and test budget"});break;}
        }
        return generations.ToArray();
    }
    public static RobustEvaluation[] Shortlist(RobustGeneration[] generations){
        var last=generations[^1].Candidates.Single(e=>e.Id==generations[^1].BestId);
        var anchor=RobustRanking.Rank(generations.Where(g=>g.Generation%2==0).Select(g=>g.Candidates.Single(e=>e.Id==g.BestId))).First();
        return new[]{last,anchor}.DistinctBy(e=>string.Join("|",e.Policy.Values.Select(v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)))).ToArray();
    }
    public static FreeLearningPolicy DrawPolicy(FreeRacingExperiment.RandomStream random,FreeLearningPolicy incumbent,double[] mean,double[] std,int candidate){
        if(candidate is <0 or >4||mean.Length!=8||std.Length!=8)throw new ArgumentException("Five candidates, eight preferences.");
        if(candidate==0)return incumbent;
        return FreeLearningPolicy.From(mean.Select((m,i)=>Math.Clamp(candidate==4?FreeLearningPolicy.Lower[i]+(FreeLearningPolicy.Upper[i]-FreeLearningPolicy.Lower[i])*.5*(1+Math.Tanh(random.Normal())):
            m+std[i]*random.Normal(),FreeLearningPolicy.Lower[i],FreeLearningPolicy.Upper[i])).ToArray());
    }
    private static RobustEvaluation[] SelectionRound(string directory,string raw,RobustBudget budget,RobustGeneration[] a,RobustGeneration[] b,string stage){
        var candidates=Shortlist(a).Concat(Shortlist(b)).Append(new RobustEvaluation("seed",-1,0,Reference("129-final"),Array.Empty<string>(),Array.Empty<RobustMetrics>())).ToArray();
        return candidates.Select(c=>new RobustEvaluation(c.Variant,c.Generation,c.Id,c.Policy,Selection.Select(s=>s.Name).ToArray(),Selection.Select(s=>
            Execute(directory,raw,budget,"selection",$"select-{c.Id}-"+s.Name,c.Id,c.Policy,s).Metrics).ToArray())).ToArray();
    }
    private static void SelectAndFreeze(string directory,string raw,RobustBudget budget){
        if(File.Exists(Path.Combine(directory,"selected-policy.json")))throw new IOException("Selected policy is already frozen.");
        if(!Read<JsonElement>(directory,"training-complete.json").GetProperty("Frozen").GetBoolean()||budget.Entries.Any(e=>e.Category=="test"))throw new InvalidOperationException("Choose before test only.");
        var evaluations=SelectionRound(directory,raw,budget,Read<RobustGeneration[]>(directory,"generations-A.json"),Read<RobustGeneration[]>(directory,"generations-B.json"),"final");
        Save(directory,"final-selection.json",evaluations);var selected=RobustRanking.Rank(evaluations).First();
        Save(directory,"selected-policy.json",new{Selected=selected,PolicySha256=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(selected.Policy,Json))),
            Basis="Frozen ranking on six selection cases only",TestWorldsBeforeFreeze=budget.Entries.Count(e=>e.Category=="test"),Frozen=true});
    }
    private static void FinalTest(string directory,string raw,RobustBudget budget){
        var frozen=Read<JsonElement>(directory,"selected-policy.json");var selected=frozen.GetProperty("Selected").Deserialize<RobustEvaluation>(Json)!;
        var policies=new[]{("129-100",40,Reference("129-100")),("129-200",72,Reference("129-200")),("129-final",161,Reference("129-final")),("130-final",selected.Id,selected.Policy)};
        var results=new List<RobustEvaluation>();
        foreach(var (label,id,policy) in policies){var scores=SealedTest.Select(s=>Execute(directory,raw,budget,"test","test-"+label+"-"+s.Name,id,policy,s).Metrics).ToArray();
            results.Add(new(label,-1,id,policy,SealedTest.Select(s=>s.Name).ToArray(),scores));Save(directory,"test-results.json",results);}
        var latest=results[^1];var eligible=RobustRanking.Completed(latest)==12&&results.Take(3).All(e=>RobustRanking.Completed(latest)>=RobustRanking.Completed(e));
        Save(directory,"two-bend-decision.json",new{Run=eligible,Reason=eligible?"12/12 sealed single-bend tests, no completion regression":"single-bend sealed test gate not passed; no costly two-bend recording"});
        if(eligible)Execute(directory,raw,budget,"two-bends","two-bends-130-final",selected.Id,selected.Policy,Selection[2],twoBends:true);
    }
}
