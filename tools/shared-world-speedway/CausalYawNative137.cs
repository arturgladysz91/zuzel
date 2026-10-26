using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.IO.Compression;
using CoreSim;
using CoreSim.PhysicalSpace;
using static SharedWorldSpeedway.RobustLearningExperiment;
namespace SharedWorldSpeedway;
// Observation-only clone of the frozen #135 collector. All inherited physical guards remain literal.
// #137 adds a separate post-second-bend straight measurement, retaining the old +1s gate stamp.
// All physics, clocks, queueing and accepted integration remain in the original WorldRunner.
public static class CausalYawNative137
{
    public static RobustEpisode Execute(string directory,string raw,RobustBudget budget,string category,string name,int id,FreeLearningPolicy policy,
        FreeLearningScenario scenario,double? shortSeconds=null,bool refinedDiagnostic=false,double? interveneAt=null,FreeRacingAction? intervention=null,bool twoBends=false,
        double horizonSeconds=.6,bool normalizeProgress=false,double? holdAt=null,FreeRacingAction? holdAction=null,bool fullThrottleCandidate=false,bool decisionObjective=false,
        TrackShapeVariant? trackShape=null,bool trackAwareness=false,Func<FreeRacingInput,FreeLearningDecision>? chooser=null,bool extendAfterSecondStraight=false){
        var previousReservation=budget.Entries.SingleOrDefault(e=>e.Name==name);
        if(previousReservation is not null){
            if(!previousReservation.Complete||previousReservation.RawSha256!=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(RawPath(raw,name)))))throw new IOException("Incomplete/corrupt attempt; never silently repeat.");
            var cached=ReadEpisode(raw,name);if(cached.Replay.Policy!=policy||cached.Replay.Scenario!=scenario||cached.GeometryName!=trackShape?.Name)throw new InvalidOperationException("Cache conditions differ.");return cached;
        }
        var reservation=budget.Reserve(name,category);var cpu=Process.GetCurrentProcess().TotalProcessorTime;
        var spec=FreeLearningExperiment.Rider(scenario);var p=FreeLearningExperiment.Parameters();
        var frame=trackShape is null?RacingLineRootCauseAudit.Frame():new TrackFrame(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack(),trackShape);
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var boundary=new RacingLineGeometry(frame);
        var samples=new List<FreeRacingSample>();var telemetry=new List<RobustTelemetry>();TwoContactForces force=default;RearWheelStep wheel=default;RiderView before=default;
        bool historicalCompleted=false;string? failure=null;string boundaryClass="none";double? gateTime=null,entrySpeed=null,exitSpeed=null,exitYaw=null,cornerMin=null;
        double legalProgress=0,legalSeconds=0,minCertified=double.PositiveInfinity,minLegacy=double.PositiveInfinity,minRefined=double.PositiveInfinity;
        double innerMin=double.PositiveInfinity,outerMin=double.PositiveInfinity,minSpeed=double.PositiveInfinity,maxBeta=0,maxYaw=0;
        double driveIntegral=0,fullDrive=0,boundaryApproach=0,recovery=0,previousProgress=0,stall=0,engineWork=0,dissipation=0;
        double lastOffset=scenario.Offset,lastExtreme=scenario.Offset;int direction=0,oscillations=0;long certificateQueries=0;
        using var world=new WorldRunner(new[]{spec},parameters:p,trackShape:trackShape??RacingLineRootCauseAudit.Variant(),frozenSurface:surface,
            twoContactInitial:new(scenario.HeadingError,scenario.YawRate),freeLearningPolicy:policy,
            diagnostics:new(){Force=f=>before=f.Rider,TwoContact=o=>force=o.Forces,RearWheel=o=>wheel=o.Step});
        var mechanical=world.MechanicalProperties(1);
        if(chooser is not null||interveneAt is not null||horizonSeconds!=.6||normalizeProgress||holdAt is not null||fullThrottleCandidate||decisionObjective||trackAwareness)world.FreeLearning!.DiagnosticChooser=input=>{
            if(chooser is not null)return chooser(input);
            if(holdAt is{} held&&input.Time>=held-1e-8&&holdAction is{} heldAction)
                return new(input.Time,heldAction,FreeActionSafety.Uncertain,false,Array.Empty<FreeLearningCandidate>(),0,0);
            var d=FreeLearningController.Choose(input,policy,spec,frame,surface,p,mechanical.MassKg,mechanical.InertiaKgMetersSquared,horizonSeconds,normalizeProgress,fullThrottleCandidate,decisionObjective,trackAwareness);
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
            if(failure is null&&gateTime is{} historicalGate&&snapshot.TimeSeconds>=historicalGate+1-1e-10)historicalCompleted=true;
            telemetry.Add(new(snapshot.TimeSeconds,dt,force,wheel,bodyDelta,loss,legacy,accepted));Sample(snapshot);
        },stopOnCrash:true,diagnosticStop:s=>failure is not null||(extendAfterSecondStraight?s.Riders[0].TotalReferenceMeters>=frame.SegmentStartMeters(8)+62:gateTime is{} t&&s.TimeSeconds>=t+1-1e-10));
        var completed=failure is null&&historicalCompleted&&(!extendAfterSecondStraight||result.Riders[0].TotalReferenceMeters>=frame.SegmentStartMeters(8)+62);var ctl=world.FreeLearning!;
        var native=new FreeLearningMetrics(completed,failure??(completed?"completed":shortSeconds is not null?"probe":"timeout"),completed?gateTime:null,result.TimeSeconds,
            legalProgress,legalSeconds,minCertified,entrySpeed,minSpeed,completed?exitSpeed:null,maxBeta,driveIntegral/result.TimeSeconds,fullDrive/result.TimeSeconds,oscillations,
            result.Riders[0].DistanceMeters,boundaryApproach,recovery,result.Work.SolverCalls,result.Work.RearWheelForceEvaluations+result.Work.RearWheelBoundaryEvaluations+ctl.Evaluations,
            ctl.Forecasts,(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,result.Fingerprint);
        var changes=ctl.Decisions.Zip(ctl.Decisions.Skip(1),(a,b)=>Math.Abs(b.Action.SteeringRadians-a.Action.SteeringRadians)*180/Math.PI).ToArray();
        var metrics=new RobustMetrics(native){MinimumInstantInner=innerMin,MinimumInstantOuter=outerMin,MinimumLegacyCertificate=minLegacy,MinimumRefinedCertificate=minRefined,
            CornerMinimumSpeed=cornerMin,MaximumYawRate=maxYaw,ExitYawRate=completed?exitYaw:null,SteeringRequestTravelDegrees=changes.Sum(),MaximumRequestChangeDegrees=changes.DefaultIfEmpty(0).Max(),
            EngineWorkJoules=engineWork,DissipationJoules=dissipation,NoSafeDecisions=ctl.Decisions.Count(d=>d.NoSafeContinuation),BoundaryClass=boundaryClass};
        var replay=new FreeLearningEpisode(name,id,policy,scenario,native,samples.ToArray(),ctl.Decisions.ToArray(),ctl.Activations.ToArray());var episode=new RobustEpisode(replay,metrics,telemetry.ToArray()){GeometryName=trackShape?.Name};
        Save(directory,"scope-"+name+".json",new{Extended=extendAfterSecondStraight,HistoricalTwoBendsCompleted=historicalCompleted,HistoricalGateSeconds=gateTime,
            WholeCaptureCompleted=completed,WholeCaptureOutcome=native.Outcome,FullSecondStraightReference=frame.SegmentStartMeters(8)+62,
            NativeForceQueries=result.Work.RearWheelForceEvaluations,NativeBoundaryQueries=result.Work.RearWheelBoundaryEvaluations,result.Work.SolverCalls,result.Work.SolverSubsteps,
            PredictionContacts=ctl.Evaluations,PredictionForecasts=ctl.Forecasts});
        SaveEpisode(raw,episode);budget.Complete(reservation,native.CpuSeconds,native.PhysicsEvaluations,native.Forecasts,metrics,RawPath(raw,name));
        Console.WriteLine($"{category} #{budget.Entries.Count(e=>e.Category==category)} {name}: {native.Outcome} legal={legalProgress:F1} gate={native.GateSeconds} CPU={native.CpuSeconds:F2} certQueries={certificateQueries}");
        if(failure?.Contains("invariant")==true)throw new InvalidOperationException("Physical invariant failed; no training continuation.");return episode;
    }

    private static void SaveEpisode(string raw,RobustEpisode run){
        Directory.CreateDirectory(raw);var path=RawPath(raw,run.Replay.Name);
        using(var z=new GZipStream(File.Create(path+".tmp"),CompressionLevel.SmallestSize))JsonSerializer.Serialize(z,run,Json);
        File.Move(path+".tmp",path,true);
    }
}
