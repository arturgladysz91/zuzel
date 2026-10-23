using System.Diagnostics;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

public sealed record FreeLearningScenario(string Name,int Level,float Speed,float Offset,double Start=22,
    float OutwardSpeed=0,float HeadingError=0,float YawRate=0);
public sealed record FreeLearningMetrics(bool Completed,string Outcome,double? GateSeconds,double DurationSeconds,
    double LegalProgress,double LegalSeconds,double MinimumClearanceMeters,double? EntrySpeed,double MinimumSpeed,double? ExitSpeed,
    double MaximumBetaDegrees,double MeanDrive,double FullDriveFraction,int LargeTrajectoryOscillations,double DistanceMeters,
    double BoundaryApproach,double RecoverySeconds,long NativeSteps,long PhysicsEvaluations,long Forecasts,double CpuSeconds,string Fingerprint);
public sealed record FreeLearningEpisode(string Name,int PolicyId,FreeLearningPolicy Policy,FreeLearningScenario Scenario,
    FreeLearningMetrics Metrics,FreeRacingSample[] Samples,FreeLearningDecision[] Decisions,FreeRacingCommand[] Activations);
public sealed record FreeLearningEvaluation(int Id,int Generation,int Level,FreeLearningPolicy Policy,FreeLearningMetrics[] Results);
public sealed record FreeLearningGeneration(int Generation,int Level,int Episodes,FreeLearningEvaluation[] Candidates,
    int BestId,FreeLearningPolicy BestPolicy,double[] Mean,double[] Std,int Completed,double MaximumLegalProgress,
    double AverageLegalProgress,int Exits,Dictionary<string,int> Causes,double CpuSeconds,long Evaluations,long Forecasts);
public sealed record FreeLearningLedgerEntry(int Number,string Name,string Category,bool Complete=false,double CpuSeconds=0,long Evaluations=0,long Forecasts=0);

public sealed class FreeLearningBudget : IDisposable
{
    private readonly string path;private readonly FileStream lease;
    public List<FreeLearningLedgerEntry> Entries {get;}
    public FreeLearningBudget(string dir){Directory.CreateDirectory(dir);path=Path.Combine(dir,"budget.json");
        lease=new(Path.Combine(dir,"budget.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        Entries=File.Exists(path)?JsonSerializer.Deserialize<List<FreeLearningLedgerEntry>>(File.ReadAllText(path),FreeRacingExperiment.Json)!:new();}
    public int Reserve(string name,string category,int trainingLimit=200){
        var count=Entries.Count(e=>e.Category==category);var max=category=="training"?trainingLimit:category=="physics"?8:36;
        var cpu=Entries.Sum(e=>e.CpuSeconds);var evaluations=Entries.Sum(e=>e.Evaluations);
        var cpuLimit=trainingLimit<=200?2400:trainingLimit<=1000?7200:12000;
        var evaluationLimit=trainingLimit<=200?50000000L:trainingLimit<=1000?160000000L:300000000L;
        if(count>=max||Entries.Any(e=>e.Name==name)||cpu>=cpuLimit||evaluations>=evaluationLimit)
            throw new InvalidOperationException("Episode/CPU/contact budget exhausted or duplicate run.");
        var n=Entries.Count+1;Entries.Add(new(n,name,category));Save();return n;
    }
    public void Complete(int n,double cpu,long evaluations,long forecasts){Entries[n-1]=Entries[n-1] with{Complete=true,CpuSeconds=cpu,Evaluations=evaluations,Forecasts=forecasts};Save();}
    private void Save(){File.WriteAllText(path+".tmp",JsonSerializer.Serialize(Entries,FreeRacingExperiment.Json));File.Move(path+".tmp",path,true);}
    public void Dispose()=>lease.Dispose();
}

public static class FreeLearningExperiment
{
    public static readonly JsonSerializerOptions Json=new(FreeRacingExperiment.Json){NumberHandling=System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals};
    public const uint Seed=1292026;
    public const string BaseHead="e23261afe1205c71a8db3733b6553485f4de29f5";
    public static WorldParameters Parameters()=>FreeRacingExperiment.Parameters() with{EnableFreeRacingLearning=true};
    public static readonly FreeLearningScenario[] Validation={new("holdout-9-4",2,9,4,22,.12f,.025f,.02f),new("holdout-11-7",2,11,7,22,-.15f,-.02f,-.02f),new("holdout-14-5",3,14,5,22,.1f,.015f,.015f)};
    public static FreeLearningScenario[] Scenarios(int level)=>level switch{
        1=>new[]{new FreeLearningScenario("straight-6-3",1,6,3,4,.1f,.02f,.02f),new("straight-8-6",1,8,6,4,-.1f,-.02f,-.02f)},
        2=>new[]{new FreeLearningScenario("bend-8-3",2,8,3,22,.1f,.02f,.02f),new("bend-10-6",2,10,6,22,-.1f,-.02f,-.02f)},
        3=>new[]{new FreeLearningScenario("bend-12-3",3,12,3,22,.1f,.02f,.02f),new("bend-14-6",3,14,6,22,-.1f,-.02f,-.02f)},
        4=>new[]{new FreeLearningScenario("bend-16-3",4,16,3,22,.1f,.02f,.02f),new("bend-18-6",4,18,6,22,-.1f,-.02f,-.02f)},
        _=>throw new ArgumentException("Four curriculum levels only.")};
    public static RiderSpec Rider(FreeLearningScenario s)=>PhysicsDrivenRacingLineAudit.Rider(PhysicsDrivenRacingLineAudit.Conditions()[0]) with{
        ReferenceMeters=s.Start,OffsetMeters=s.Offset,TargetOffsetMeters=s.Offset,SpeedMetersPerSecond=s.Speed,InitialOutwardSpeed=s.OutwardSpeed,TrafficEnabled=false};
    public static void Save(string dir,string name,object value)=>File.WriteAllText(Path.Combine(dir,name),JsonSerializer.Serialize(value,Json));
    public static T Read<T>(string dir,string name)=>JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(dir,name)),Json)!;
    public static FreeLearningEpisode ReadEpisode(string dir,string name){using var z=new GZipStream(File.OpenRead(Path.Combine(dir,name+".json.gz")),CompressionMode.Decompress);return JsonSerializer.Deserialize<FreeLearningEpisode>(z,Json)!;}
    public static void SaveEpisode(string dir,FreeLearningEpisode episode){using var z=new GZipStream(File.Create(Path.Combine(dir,episode.Name+".json.gz")),CompressionLevel.SmallestSize);JsonSerializer.Serialize(z,episode,Json);}
    public static void Run(string dir,string stage){
        Directory.CreateDirectory(dir);
        if(stage=="freeze-physics"){
            if(File.Exists(Path.Combine(dir,"physics-protocol.json")))throw new IOException("Physics protocol exists.");
            Save(dir,"physics-protocol.json",new{BaseHead,AnglesDegrees=new[]{0,5,-5,15,-15},Start=new{Speed=8,Offset=5,Reference=4,Drive=1},Reaction=.24,Duration=.5,
                PhysicsSources=FreeRacingExperiment.Sources().Where(x=>!x.Key.StartsWith("Free")&&!x.Key.StartsWith("World")).ToDictionary(),Rule="Five identical native initial states. Zero steering until full .24s reaction, then held impulse. Original frozen contacts; record both tyres and r cross F."});return;
        }
        using var budget=new FreeLearningBudget(dir);
        if(stage=="physics"){Physics(dir,budget);return;}
        using var physics=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"physics.json")));
        if(!physics.RootElement.GetProperty("Passed").GetBoolean())throw new InvalidOperationException("Physical signs must pass before training.");
        if(stage=="freeze"){
            if(File.Exists(Path.Combine(dir,"protocol.json")))throw new IOException("Training protocol exists.");
            Save(dir,"protocol.json",new{BaseHead,Seed,InitialPolicy=new FreeLearningPolicy(),FreeLearningPolicy.Names,FreeLearningPolicy.Lower,FreeLearningPolicy.Upper,
                Curriculum=Enumerable.Range(1,4).Select(Scenarios),Validation,ReviewSeconds=FreeLearningController.ReviewSeconds,HorizonSeconds=FreeLearningController.HorizonSeconds,
                Phase1Episodes=200,Phase2Episodes=1000,OptionalPhase3Episodes=2000,PhysicsLimit=8,ValidationLimit=36,
                PhaseCpuSeconds=new[]{2400,7200,12000},PhaseContactEvaluations=new long[]{50000000,160000000,300000000},MaximumEpisodeCpuSeconds=90,MaximumGenerationCpuSeconds=150,
                Advancement="Two consecutive generations with >=6/10 legal completions and best2/2; only training outcomes advance curriculum.",
                FailureFeedback="Rank legal completions first, then minimum legal progress across both identical scenarios, then mean progress, boundary approach per legal metre, recovery per metre. No survival-time-only reward; failure step and illegal progress excluded.",
                CEM="Five candidates x same two scenarios per generation. Incumbent + three bounded Gaussian mutations + one uniform immigrant. Best two including DNFs update mean (0.6 new/0.4 old), std floor15% range. All outcomes from native Box2D. Stable seed; no retries.",
                Extension="At200, extend only with full-bend completion or >=10m controlled legal-progress gain in current bend level. Beyond200 stop after8 generations without >=20ms time or >=1m legal-progress improvement. At any point stop6 consecutive generations repeating unsafe outward failure without gain. No validation-based parameter selection.",
                Checkpoints=new[]{50,100,200},Gate="Whole footprint past fixed plane, +1s legal continuation. Swept boundary/reverse/projection/beta45/energy/stall failures precede finish.",
                PredictionTolerances=new{XY=.1,Speed=.2,ChiDegrees=1,BetaDegrees=1,SteeringDegrees=1.5,Omega=2,YawRate=.05,ClearanceOptimism=.1},
                Sources=new[]{"FreeLearningController.cs","FreeLearningExperiment.cs","FreeRacingModel.cs","WorldRunner.cs","WorldModel.cs"}.ToDictionary(n=>n,n=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine("tools/shared-world-speedway",n)))))});return;
        }
        if(stage=="probe"){
            foreach(var s in Scenarios(1))Execute(dir,budget,"physics","probe-"+s.Name,0,new(),s,shortSeconds:2);
            ValidatePredictions(dir);return;
        }
        if(stage=="train"){Train(dir,budget);return;}
        if(stage=="final"){Final(dir,budget);return;}
        if(stage=="validate-predictions"){ValidatePredictions(dir);return;}
        throw new ArgumentException("freeze-physics|physics|freeze|probe|train|final|validate-predictions");
    }

    private static void Physics(string dir,FreeLearningBudget budget){
        using var protocol=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"physics-protocol.json")));
        var records=new List<object>();bool passed=true;
        foreach(var angle in new[]{0,5,-5,15,-15}){
            var name="impulse-"+angle;var n=budget.Reserve(name,"physics");var cpu=Process.GetCurrentProcess().TotalProcessorTime;
            var s=new FreeLearningScenario(name,1,8,5,4);var spec=Rider(s);var p=FreeRacingExperiment.Parameters() with{EnableFreeRacing=false};
            TwoContactForces force=default;var frames=new List<object>();var boundary=new RacingLineGeometry(RacingLineRootCauseAudit.Frame());
            using var world=new WorldRunner(new[]{spec},parameters:p,trackShape:RacingLineRootCauseAudit.Variant(),twoContactInitial:new(),
                frozenSurface:TrackState.CreateDefault(RacingLineRootCauseAudit.Frame().Track,new(1,0,.5f)).Snapshot(),
                steeringCommands:new[]{new TwoContactSteeringCommand(1,0,0),new(1,.24,angle*MathF.PI/180)},
                diagnostics:new(){TwoContact=o=>force=o.Forces});
            var initial=world.Snapshot.Riders[0];var result=world.Run(name,.01,.5,acceptedObserver:snapshot=>{
                var v=snapshot.Riders[0];var gap=boundary.Clearance(v);
                frames.Add(new{Time=snapshot.TimeSeconds,Rider=v,Steering=snapshot.Steering![0].AngleRadians,Front=force.FrontForce,Rear=force.RearForce,
                    force.FrontTorque,force.RearTorque,YawTorque=force.NetTorque,force.SteeringBefore,force.SteeringAfter,Inner=gap.Inner,Outer=gap.Outer});
            });
            var end=result.Riders[0];var chi=Math.Atan2(end.Velocity.Y,end.Velocity.X);var gapEnd=boundary.Clearance(end);
            var sign=angle==0?Math.Abs(end.Position.Y-initial.Position.Y)<1e-4:Math.Sign(end.Heading)==Math.Sign(angle)&&Math.Sign(chi)==Math.Sign(angle)&&Math.Sign(end.Position.Y-initial.Position.Y)==Math.Sign(angle);
            passed&=sign;
            var record=new{Name=name,AngleDegrees=angle,Initial=initial,Final=end,DeltaPsi=end.Heading-initial.Heading,DeltaChi=chi,DeltaBeta=end.SlideRadians,
                DeltaX=end.Position.X-initial.Position.X,DeltaY=end.Position.Y-initial.Position.Y,EndSteering=world.Snapshot.Steering![0].AngleRadians,
                Inner=gapEnd.Inner,Outer=gapEnd.Outer,SignPassed=sign,Steps=result.Work.SolverCalls};records.Add(record);
            Save(dir,name+".json",new{Summary=record,Frames=frames});budget.Complete(n,(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,result.Work.RearWheelForceEvaluations+result.Work.RearWheelBoundaryEvaluations,0);
        }
        var frame=RacingLineRootCauseAudit.Frame();var a=frame.RoadAt(35,5);var b=frame.RoadAt(45,5);
        passed&=BikeAngles.Wrap(b.Heading-a.Heading)>0;
        Save(dir,"physics.json",new{Passed=passed,GlobalConvention="+X forward on start straight; +Y left/infield; positive angle/torque CCW. Beta=psi-chi. Upcoming left bend has increasing heading.",LeftBendHeadingChange=BikeAngles.Wrap(b.Heading-a.Heading),Records=records});
        Console.WriteLine("Physical sign gate: "+passed);
    }

    public static FreeLearningEpisode Execute(string dir,FreeLearningBudget budget,string category,string name,int id,FreeLearningPolicy policy,
        FreeLearningScenario scenario,int trainingLimit=200,double? shortSeconds=null){
        var reservation=budget.Reserve(name,category,trainingLimit);var cpu=Process.GetCurrentProcess().TotalProcessorTime;
        var spec=Rider(scenario);var frame=RacingLineRootCauseAudit.Frame();var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var boundary=new RacingLineGeometry(frame);
        var samples=new List<FreeRacingSample>();TwoContactForces force=default;RearWheelStep wheel=default;RiderView before=default;
        string? failure=null;double? gateTime=null,entrySpeed=null,exitSpeed=null;double legalProgress=0,legalSeconds=0,minGap=double.PositiveInfinity;
        double minSpeed=double.PositiveInfinity,maxBeta=0,driveIntegral=0,fullDrive=0,boundaryApproach=0,recoverySeconds=0,lastOffset=scenario.Offset,lastExtreme=scenario.Offset;
        int direction=0,oscillations=0;double previousProgress=0,stallSeconds=0;
        using var world=new WorldRunner(new[]{spec},parameters:Parameters(),trackShape:RacingLineRootCauseAudit.Variant(),frozenSurface:surface,
            twoContactInitial:new(scenario.HeadingError,scenario.YawRate),freeLearningPolicy:policy,
            diagnostics:new(){Force=f=>before=f.Rider,TwoContact=o=>force=o.Forces,RearWheel=o=>wheel=o.Step});
        var mechanical=world.MechanicalProperties(1);var gateReference=scenario.Level==1?28:frame.SegmentStartMeters(4)+3;
        var gate=frame.At(gateReference,0);var gatePoint=new MeterPoint(gate.Position.X,gate.Position.Y);var tangent=new MeterPoint(gate.Road.Tangent.X,gate.Road.Tangent.Y);
        double WholeGate(RiderView v){var bike=BikeFootprint.Create(new(v.Position.X,v.Position.Y),v.Heading,SpeedwayBikeDimensions.Reference);
            return new[]{bike.Chassis,bike.Handlebar}.Min(c=>Math.Min(MeterPoint.Dot(c.Start-gatePoint,tangent),MeterPoint.Dot(c.End-gatePoint,tangent))-c.RadiusMeters);}
        void Sample(WorldSnapshot snapshot){var v=snapshot.Riders[0];var gap=boundary.Clearance(v);var c=world.FreeLearning!;
            var input=c.Input(v,snapshot.TimeSeconds,world.RearWheel(1).RearWheelAngularVelocityRadPerSecond,snapshot.Steering![0].AngleRadians);
            var action=c.Decisions.LastOrDefault()?.Action??c.Active;samples.Add(new(input,gap.Inner,gap.Outer,action.SteeringRadians,action.Drive));}
        Sample(world.Snapshot);
        if(Math.Min(samples[0].Inner,samples[0].Outer)<=0)throw new ArgumentException("Illegal initial footprint.");
        var result=world.Run(name,.01,shortSeconds??(scenario.Level==1?9:26),acceptedObserver:snapshot=>{
            var c=world.FreeLearning!;var previous=samples[^1];samples[^1]=previous with{Input=previous.Input with{Rider=before,Active=c.Active,Pending=c.Input(before,previous.Input.Time,previous.Input.Omega,previous.Input.Steering).Pending,NextReview=c.NextReview},RequestedSteering=c.Decisions[^1].Action.SteeringRadians,RequestedDrive=c.Decisions[^1].Action.Drive};
            var v=snapshot.Riders[0];var dt=wheel.Seconds;var gap=boundary.Clearance(v);var oldGap=boundary.Clearance(before);var distance=Vector2.Distance(v.Position,before.Position);
            var sweep=.5*(distance+SpeedwayBikeDimensions.Reference.BoundingRadiusMeters*Math.Abs(BikeAngles.Wrap(v.Heading-before.Heading)));
            var clearance=Math.Min(Math.Min(gap.Inner,gap.Outer),Math.Min(oldGap.Inner,oldGap.Outer))-sweep;minGap=Math.Min(minGap,clearance);
            if(clearance<0)failure="boundary";
            var delta=v.TotalReferenceMeters-before.TotalReferenceMeters;
            if(Math.Abs(delta)>distance*1.5+.1)failure="projection";
            if(Vector2.Dot(v.Velocity,v.Road.Tangent)<=0||delta<-.02)failure="reverse";
            if(Math.Abs(v.SlideRadians)>Math.PI/4)failure="control beta45";
            if(!SteeringStepIsPhysical(force,(float)dt)||force.TravelSteering is not null||force.SteeringControl is not null)failure="actuator invariant";
            var energy=TwoContactBikeAudit.BodyKinetic(v,mechanical.MassKg,mechanical.InertiaKgMetersSquared)-TwoContactBikeAudit.BodyKinetic(before,mechanical.MassKg,mechanical.InertiaKgMetersSquared)
                +.45*(wheel.AfterOmega*wheel.AfterOmega-wheel.BeforeOmega*wheel.BeforeOmega);
            if(energy-wheel.EngineWorkJoules>.05+1e-6*Math.Max(TwoContactBikeAudit.BodyKinetic(before,mechanical.MassKg,mechanical.InertiaKgMetersSquared),TwoContactBikeAudit.BodyKinetic(v,mechanical.MassKg,mechanical.InertiaKgMetersSquared)))failure="energy invariant";
            if(failure is null){legalProgress=Math.Max(legalProgress,v.TotalReferenceMeters-scenario.Start);legalSeconds=snapshot.TimeSeconds;}
            if(legalProgress-previousProgress<.005)stallSeconds+=dt;else stallSeconds=0;previousProgress=legalProgress;
            if(stallSeconds>=1||v.Speed<.5)failure="no progress";
            if(entrySpeed is null&&v.TotalReferenceMeters>=frame.SegmentStartMeters(1))entrySpeed=v.Speed;
            minSpeed=Math.Min(minSpeed,v.Speed);maxBeta=Math.Max(maxBeta,Math.Abs(v.SlideRadians)*180/Math.PI);
            driveIntegral+=before.Drive*dt;if(before.Drive>=.99)fullDrive+=dt;
            var oldClear=Math.Min(oldGap.Inner,oldGap.Outer);var newClear=Math.Min(gap.Inner,gap.Outer);
            boundaryApproach+=Math.Max(0,oldClear-newClear);if(newClear>oldClear&&oldClear<1)recoverySeconds+=dt;
            var d=Math.Sign(v.Road.OffsetMeters-lastOffset);if(d!=0&&d!=direction&&Math.Abs(lastOffset-lastExtreme)>=1){if(direction!=0)oscillations++;direction=d;lastExtreme=lastOffset;}
            if(direction==0&&d!=0)direction=d;lastOffset=v.Road.OffsetMeters;
            if(failure is null&&gateTime is null&&v.TotalReferenceMeters>=gateReference&&WholeGate(v)>=0){gateTime=snapshot.TimeSeconds;exitSpeed=v.Speed;}
            if(snapshot.TimeSeconds%1<.011&&(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds>90)failure="episode CPU limit";
            Sample(snapshot);
        },stopOnCrash:true,diagnosticStop:s=>failure is not null||gateTime is{} t&&s.TimeSeconds>=t+1-1e-10);
        var completed=failure is null&&gateTime is not null&&result.TimeSeconds>=gateTime+1-1e-8;
        var metrics=new FreeLearningMetrics(completed,failure??(completed?"completed":shortSeconds is not null?"probe":"timeout"),completed?gateTime:null,result.TimeSeconds,
            legalProgress,legalSeconds,minGap,entrySpeed,minSpeed,completed?exitSpeed:null,maxBeta,driveIntegral/result.TimeSeconds,fullDrive/result.TimeSeconds,oscillations,result.Riders[0].DistanceMeters,
            boundaryApproach,recoverySeconds,result.Work.SolverCalls,result.Work.RearWheelForceEvaluations+result.Work.RearWheelBoundaryEvaluations+world.FreeLearning!.Evaluations,
            world.FreeLearning.Forecasts,(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,result.Fingerprint);
        var episode=new FreeLearningEpisode(name,id,policy,scenario,metrics,samples.ToArray(),world.FreeLearning.Decisions.ToArray(),world.FreeLearning.Activations.ToArray());
        SaveEpisode(dir,episode);Save(dir,name+"-metrics.json",metrics);budget.Complete(reservation,metrics.CpuSeconds,metrics.PhysicsEvaluations,metrics.Forecasts);
        Console.WriteLine($"{category} {budget.Entries.Count(e=>e.Category==category)} {name}: {metrics.Outcome} legal={legalProgress:F1} gate={metrics.GateSeconds} cpu={metrics.CpuSeconds:F2}");return episode;
    }

    public static IOrderedEnumerable<FreeLearningEvaluation> Rank(IEnumerable<FreeLearningEvaluation> xs)=>xs.OrderByDescending(e=>e.Results.Count(r=>r.Completed))
        .ThenBy(e=>e.Results.All(r=>r.Completed)?e.Results.Max(r=>r.GateSeconds!.Value):double.PositiveInfinity)
        .ThenByDescending(e=>e.Results.Min(r=>r.LegalProgress)).ThenByDescending(e=>e.Results.Average(r=>r.LegalProgress))
        .ThenBy(e=>e.Results.Sum(r=>r.BoundaryApproach)/Math.Max(1,e.Results.Sum(r=>r.LegalProgress)))
        .ThenByDescending(e=>e.Results.Sum(r=>r.RecoverySeconds)/Math.Max(1,e.Results.Sum(r=>r.LegalProgress))).ThenBy(e=>e.Id);

    public static bool SteeringStepIsPhysical(TwoContactForces force,float seconds){
        // Reproduce the ORIGINAL float actuator, rather than divide two rounded angles by a microsecond.
        var p=TwoContactBikeParameters.Default;
        var target=Math.Clamp(force.SteeringRequest,-p.MaximumSteeringRadians,p.MaximumSteeringRadians);
        var expected=force.SteeringBefore+Math.Clamp(target-force.SteeringBefore,-p.SteeringRateRadiansPerSecond*seconds,p.SteeringRateRadiansPerSecond*seconds);
        return force.SteeringAfter==expected&&Math.Abs(force.SteeringAfter)<=p.MaximumSteeringRadians;
    }

    private static bool RoundingOnlyAbort(FreeLearningEpisode episode){
        if(episode.Metrics.Outcome!="actuator invariant")return false;
        var a=episode.Samples[^2].Input;var b=episode.Samples[^1].Input;var frame=RacingLineRootCauseAudit.Frame();
        var force=TwoContactBikeDynamics.Forces(a.Rider,Rider(episode.Scenario),frame,TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot(),Parameters(),a.Time,147,
            new(a.Omega*.33),new(a.Steering),(float)(b.Time-a.Time),steeringOverride:a.Active.SteeringRadians,inertia:52.72097f);
        return SteeringStepIsPhysical(force,(float)(b.Time-a.Time))&&force.SteeringAfter==b.Steering;
    }

    private static void ValidatePredictions(string dir){
        var records=new List<object>();var passed=true;long evaluations=0;var cpu=Process.GetCurrentProcess().TotalProcessorTime;
        foreach(var scenario in Scenarios(1)){
            var run=ReadEpisode(dir,"probe-"+scenario.Name);var frame=RacingLineRootCauseAudit.Frame();var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
            foreach(var origin in new[]{0d,.4,.8})foreach(var horizon in new[]{.3,.6,.9}){
                var start=run.Samples.SingleOrDefault(s=>Math.Abs(s.Input.Time-origin)<1e-8);var actual=run.Samples.SingleOrDefault(s=>Math.Abs(s.Input.Time-origin-horizon)<1e-8);
                if(start is null||actual is null){passed=false;records.Add(new{Scenario=scenario.Name,Origin=origin,Horizon=horizon,Passed=false,Missing=true});continue;}
                var prediction=FreeRacingModel.Predict(start.Input,Rider(scenario),frame,surface,Parameters(),147,52.72097f,horizon,
                    causalPolicy:input=>{var d=FreeLearningController.Choose(input,run.Policy,Rider(scenario),frame,surface,Parameters(),147,52.72097f);return(d.Action,d.ForceEvaluations);},reviewSeconds:FreeLearningController.ReviewSeconds);
                evaluations+=prediction.ForceEvaluations;var end=prediction.Points[^1];var v=actual.Input.Rider;
                var xy=Vector2.Distance(end.Rider.Position,v.Position);var speed=Math.Abs(end.Rider.Speed-v.Speed);
                var chi=Math.Abs(BikeAngles.Wrap(Math.Atan2(end.Rider.Velocity.Y,end.Rider.Velocity.X)-Math.Atan2(v.Velocity.Y,v.Velocity.X)))*180/Math.PI;
                var beta=Math.Abs(BikeAngles.Wrap(end.Rider.SlideRadians-v.SlideRadians))*180/Math.PI;var steer=Math.Abs(end.Steering-actual.Input.Steering)*180/Math.PI;
                var omega=Math.Abs(end.Omega-actual.Input.Omega);var yaw=Math.Abs(end.Rider.AngularVelocity-v.AngularVelocity);var optimism=Math.Max(end.Inner-actual.Inner,end.Outer-actual.Outer);
                var ok=xy<=.1&&speed<=.2&&chi<=1&&beta<=1&&steer<=1.5&&omega<=2&&yaw<=.05&&optimism<=.1;passed&=ok;
                records.Add(new{Scenario=scenario.Name,Origin=origin,Horizon=horizon,Passed=ok,XY=xy,Speed=speed,Chi=chi,Beta=beta,Steering=steer,Omega=omega,Yaw=yaw,ClearanceOptimism=optimism});
            }
        }
        Save(dir,"prediction-validation.json",new{Passed=passed,Records=records,NativeWorlds=0,Evaluations=evaluations,CpuSeconds=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds});
        Console.WriteLine("New policy causal prediction validation: "+passed);
    }

    private static void Train(string dir,FreeLearningBudget budget){
        using var prediction=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"prediction-validation.json")));
        if(!prediction.RootElement.GetProperty("Passed").GetBoolean())throw new InvalidOperationException("Predictor gate closed.");
        var protocol=Read<JsonElement>(dir,"protocol.json");foreach(var item in protocol.GetProperty("Sources").EnumerateObject())
            if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine("tools/shared-world-speedway",item.Name))))!=item.Value.GetString())throw new InvalidOperationException("Frozen training source changed.");
        var random=new FreeRacingExperiment.RandomStream(Seed);var incumbent=new FreeLearningPolicy();var mean=incumbent.Values;
        var std=mean.Select((_,i)=>(FreeLearningPolicy.Upper[i]-FreeLearningPolicy.Lower[i])*.3).ToArray();
        var generations=File.Exists(Path.Combine(dir,"generations.json"))?Read<List<FreeLearningGeneration>>(dir,"generations.json"):new();
        var level=1;var stageGenerations=0;var stageSuccess=0;var lastGain=0;double bestMetric=double.NegativeInfinity;
        FreeLearningEvaluation? bestStage=null,bestVerified=null;int limit=200;string stop="phase complete";
        var milestones=File.Exists(Path.Combine(dir,"milestones.json"))?Read<Dictionary<string,FreeLearningPolicy>>(dir,"milestones.json"):new(){{"initial",incumbent}};
        var checkpointCandidates=File.Exists(Path.Combine(dir,"milestone-policy-ids.json"))?Read<Dictionary<string,int>>(dir,"milestone-policy-ids.json"):new(){{"initial",0}};
        var startGeneration=generations.Count;
        if(startGeneration>0){
            var last=generations[^1];incumbent=last.BestPolicy;mean=last.Mean;std=last.Std;level=last.Level;
            var stage=generations.Where(g=>g.Level==level).ToArray();stageGenerations=stage.Length;
            bestStage=Rank(stage.SelectMany(g=>g.Candidates)).First();
            bestVerified=Rank(generations.Where(g=>g.Level>=2).SelectMany(g=>g.Candidates).Where(e=>e.Results.All(r=>r.Completed))).OrderByDescending(e=>e.Level).FirstOrDefault();
            stageSuccess=stage.Reverse().TakeWhile(g=>g.Completed>=6).Count();lastGain=last.Generation;
            bestMetric=bestStage.Results.All(r=>r.Completed)?10000-bestStage.Results.Max(r=>r.GateSeconds!.Value):bestStage.Results.Min(r=>r.LegalProgress);
            if(level<4&&stageSuccess>=2){level++;stageSuccess=0;stageGenerations=0;bestStage=null;bestMetric=double.NegativeInfinity;}
            for(var i=0;i<startGeneration*32;i++)random.Normal();
            if(File.Exists(Path.Combine(dir,"extension-200.json"))&&Read<JsonElement>(dir,"extension-200.json").GetProperty("Extend").GetBoolean())limit=1000;
            Save(dir,"resume.json",new{FromGeneration=startGeneration,ExistingTrainingEpisodes=budget.Entries.Count(e=>e.Category=="training"),Reason="diagnostic float-rounding guard repair; no policy, dynamics, objective or RNG mutation changes",RandomDrawsSkipped=startGeneration*32});
        }
        void Checkpoint(){
            var count=budget.Entries.Count(e=>e.Category=="training");
            if(count is not (50 or 100 or 200)||File.Exists(Path.Combine(dir,$"assessment-{count}.json")))return;
            milestones["after-"+count]=incumbent;checkpointCandidates["after-"+count]=bestStage?.Id??-1;
            foreach(var scenario in Validation)Execute(dir,budget,"validation",$"after-{count}-"+scenario.Name,bestStage?.Id??-1,incumbent,scenario,limit);
            Save(dir,$"assessment-{count}.json",new{TrainingEpisodes=count,Level=level,Best=bestStage,UseValidationForSelection=false,
                Completed=generations.Sum(g=>g.Completed),CpuSeconds=budget.Entries.Sum(e=>e.CpuSeconds),Evaluations=budget.Entries.Sum(e=>e.Evaluations),Note="Frozen incumbent after complete generation updates; partial current generation not selected."});
            if(count==200){var current=generations.Where(g=>g.Level==level&&level>=2).ToArray();
                var progress=bestVerified is not null||current.Length>=2&&current[^1].MaximumLegalProgress>=current[0].MaximumLegalProgress+10;
                Save(dir,"extension-200.json",new{Extend=progress,NextLimit=progress?1000:200,Reason=bestVerified is not null?"native full-bend legal completion":progress?"controlled legal progress increased >=10m":"no qualifying progress"});
                if(progress)limit=1000;
            }
        }
        // Initial untouched validation is read-only feedback; never consumed by CEM or curriculum.
        foreach(var scenario in Validation){
            var name="initial-"+scenario.Name;var priorValidation=budget.Entries.FirstOrDefault(e=>e.Name==name);
            if(priorValidation?.Complete==true)continue;
            if(budget.Entries.Any(e=>e.Name==name+"-export-retry"&&e.Complete))continue;
            Execute(dir,budget,"validation",priorValidation is null?name:name+"-export-retry",0,incumbent,scenario);
        }
        for(var generation=startGeneration;generation<limit/10;generation++){
            var genCpu=Process.GetCurrentProcess().TotalProcessorTime;var candidates=new List<FreeLearningEvaluation>();stageGenerations++;
            for(var c=0;c<5;c++){
                var id=generation*5+c;
                var values=c==0?incumbent.Values:mean.Select((m,i)=>Math.Clamp(c==4?FreeLearningPolicy.Lower[i]+(FreeLearningPolicy.Upper[i]-FreeLearningPolicy.Lower[i])*.5*(1+Math.Tanh(random.Normal())):m+std[i]*random.Normal(),FreeLearningPolicy.Lower[i],FreeLearningPolicy.Upper[i])).ToArray();
                var policy=FreeLearningPolicy.From(values);var results=new List<FreeLearningMetrics>();
                foreach(var scenario in Scenarios(level)){
                    var name=$"g{generation:D3}-p{id:D4}-{scenario.Name}";
                    var existing=budget.Entries.FirstOrDefault(e=>e.Name==name);
                    var episode=existing is null?Execute(dir,budget,"training",name,id,policy,scenario,limit):ReadEpisode(dir,name);
                    if(existing is not null&&RoundingOnlyAbort(episode))episode=Execute(dir,budget,"training",name+"-rate-retry",id,policy,scenario,limit);
                    results.Add(episode.Metrics);Checkpoint();
                    if(episode.Metrics.Outcome.Contains("invariant"))throw new InvalidOperationException("Physical invariant failure; training halted.");
                }
                candidates.Add(new(id,generation,level,policy,results.ToArray()));
            }
            var ranked=Rank(candidates).ToArray();var elites=ranked.Take(2).ToArray();
            for(var i=0;i<mean.Length;i++){
                var m=elites.Average(e=>e.Policy.Values[i]);mean[i]=.4*mean[i]+.6*m;
                std[i]=Math.Max(.15*(FreeLearningPolicy.Upper[i]-FreeLearningPolicy.Lower[i]),Math.Sqrt(elites.Average(e=>Math.Pow(e.Policy.Values[i]-m,2))));
            }
            var prior=bestStage;bestStage=Rank(bestStage is null?candidates:candidates.Append(bestStage)).First();incumbent=bestStage.Policy;
            var metric=bestStage.Results.All(r=>r.Completed)?10000-bestStage.Results.Max(r=>r.GateSeconds!.Value):bestStage.Results.Min(r=>r.LegalProgress);
            if(metric>bestMetric+(metric>9000?.02:1)){bestMetric=metric;lastGain=generation;}
            if(!milestones.ContainsKey("first-improvement")&&generation>0&&prior is not null&&metric>(prior.Results.All(r=>r.Completed)?10000-prior.Results.Max(r=>r.GateSeconds!.Value):prior.Results.Min(r=>r.LegalProgress))+(metric>9000?.02:1)){
                milestones["first-improvement"]=incumbent;checkpointCandidates["first-improvement"]=bestStage.Id;
            }
            if(level>=2&&bestStage.Results.All(r=>r.Completed)){
                bestVerified=bestStage;
                if(!milestones.ContainsKey("first-safe-bend")){milestones["first-safe-bend"]=incumbent;checkpointCandidates["first-safe-bend"]=bestStage.Id;}
                Save(dir,"best-verified-policy.json",new{Verified=true,Evaluation=bestVerified});
            }
            var count=budget.Entries.Count(e=>e.Category=="training");var gen=new FreeLearningGeneration(generation,level,count,candidates.ToArray(),bestStage.Id,incumbent,(double[])mean.Clone(),(double[])std.Clone(),
                candidates.Sum(e=>e.Results.Count(r=>r.Completed)),candidates.Max(e=>e.Results.Max(r=>r.LegalProgress)),candidates.Average(e=>e.Results.Average(r=>r.LegalProgress)),
                candidates.Sum(e=>e.Results.Count(r=>r.Outcome=="boundary")),candidates.SelectMany(e=>e.Results).GroupBy(r=>r.Outcome).ToDictionary(g=>g.Key,g=>g.Count()),
                (Process.GetCurrentProcess().TotalProcessorTime-genCpu).TotalSeconds,candidates.Sum(e=>e.Results.Sum(r=>r.PhysicsEvaluations)),candidates.Sum(e=>e.Results.Sum(r=>r.Forecasts)));
            generations.Add(gen);Save(dir,$"generation-{generation:D3}.json",gen);Save(dir,"generations.json",generations);
            Save(dir,"best-training-candidate.json",new{Verified=bestStage.Results.All(r=>r.Completed),Evaluation=bestStage});
            Save(dir,"milestones.json",milestones);Save(dir,"milestone-policy-ids.json",checkpointCandidates);
            Console.WriteLine($"generation {generation} level{level} completed={gen.Completed}/10 best={bestMetric:F3} episodes={count}");
            stageSuccess=gen.Completed>=6&&bestStage.Results.All(r=>r.Completed)?stageSuccess+1:0;
            if(level<4&&stageSuccess>=2){level++;stageSuccess=0;stageGenerations=0;bestStage=null;bestMetric=double.NegativeInfinity;lastGain=generation;}
            if(stageGenerations>=6&&generation-lastGain>=6&&generations.TakeLast(6).All(g=>g.Completed==0&&g.Causes.GetValueOrDefault("boundary")>=8)){stop="six generations without progress, repeated boundary failures";break;}
            if(count>200&&generation-lastGain>=8){stop="eight generations at plateau";break;}
            if(gen.CpuSeconds>150){stop="generation CPU limit";break;}
            if(budget.Entries.Sum(e=>e.CpuSeconds)>=(limit<=200?2300:7100)||budget.Entries.Sum(e=>e.Evaluations)>=(limit<=200?48000000:158000000)){stop="phase computation budget";break;}
        }
        var final=bestVerified?.Policy??incumbent;
        Save(dir,"training-result.json",new{Stop=stop,TrainingEpisodes=budget.Entries.Count(e=>e.Category=="training"),FinalPolicy=final,Verified=bestVerified is not null,
            BestVerified=bestVerified,CurriculumLevel=level,Generations=generations.Count,Optional2000Phase="not invoked; plateau/cost assessment at <=1000",Frozen=true});
        milestones["final"]=final;Save(dir,"milestones.json",milestones);
    }

    private static void Final(string dir,FreeLearningBudget budget){
        var result=Read<JsonElement>(dir,"training-result.json");var policy=result.GetProperty("FinalPolicy").Deserialize<FreeLearningPolicy>(FreeRacingExperiment.Json)!;
        var milestones=Read<Dictionary<string,FreeLearningPolicy>>(dir,"milestones.json");
        foreach(var scenario in Validation)Execute(dir,budget,"validation","final-"+scenario.Name,-1,policy,scenario,1000);
        foreach(var item in milestones.Where(x=>x.Key!="after-50"))Execute(dir,budget,"validation","overlay-"+item.Key,-1,item.Value,Validation[0],1000);
        Execute(dir,budget,"validation","deterministic-repeat",-1,policy,Validation[0],1000);
        var first=ReadEpisode(dir,"final-"+Validation[0].Name);var repeat=ReadEpisode(dir,"deterministic-repeat");
        Save(dir,"determinism.json",new{SameFingerprint=first.Metrics.Fingerprint==repeat.Metrics.Fingerprint,First=first.Metrics.Fingerprint,Repeat=repeat.Metrics.Fingerprint});
    }
}
