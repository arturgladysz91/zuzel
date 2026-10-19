using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;

namespace SharedWorldSpeedway;

/// <summary>#98 observations only. Commands are authored before each world; no snapshot restart.</summary>
public static class SlipControlValidation
{
    public const string BaseHead="c0de934c8992cb902fbea8da24366482d5b7ae54";
    public static WorldParameters Parameters(float yaw=.1f)=>new(){EnableSlipCoupledDynamics=true,SlipYawResponseSeconds=yaw};
    public sealed record Plan(double GasAtSeconds,ControlCommand[] WideCommands,string PilotFingerprint,
        string Rule="First accepted control state at corner progress >=0.20; command times rounded UP to 10ms. Wide: target5 at cp.20, target3 at cp.55, target8 on following straight. Frozen before comparisons.");
    public sealed record Tick(double TimeSeconds,double DurationSeconds,RiderView Before,RiderView After,
        ForceObservation Force,SlipObservation Slip,YawLimitObservation Yaw,AppliedForceObservation Applied,
        double KineticChangeJoules,double WorkJoules,double ClearanceMeters,double PairSeparationMeters);
    public sealed record Capture(WorldSnapshot Initial,RunResult Result,List<Tick> Ticks,
        ContactPointObservation[] ContactPoints,ConsequenceObservation[] Consequences,
        Dictionary<int,(float MassKg,float Inertia)> Mechanical);
    public static object Sample(Tick t)=>new{t.TimeSeconds,t.DurationSeconds,RiderId=t.Before.Id,
        ReferenceMeters=t.Before.TotalReferenceMeters,t.Before.Speed,t.Before.AngularVelocity,
        t.Before.Road.OffsetMeters,t.Before.TargetOffsetMeters,t.Before.Drive,t.Slip.ActualSlipDegrees,t.Slip.TargetSlipDegrees,
        t.Slip.DriveForceNewtons,t.Slip.LateralForceNewtons,t.Slip.GripBudgetNewtons,t.Slip.GripUtilization,
        DrivePowerWatts=t.Slip.DriveForceNewtons*t.Before.ForwardSpeed,t.Slip.LateralLossWatts,t.Slip.ResistanceLossWatts,
        t.Yaw.RequestedNewtonMeters,t.Yaw.AfterAuthorityNewtonMeters,t.Yaw.AfterEnergyNewtonMeters,
        AppliedTorqueNewtonMeters=t.Applied.TorqueNewtonMeters,t.Yaw.OrientationErrorRadians,t.Yaw.DissipationCreditWatts,
        t.Slip.TranslationalPowerWatts,t.Slip.YawPowerWatts,t.KineticChangeJoules,t.WorkJoules,t.ClearanceMeters,t.PairSeparationMeters};

    public static Scenario Pack()=>new("D1-four-laps",new[]{SlipDynamicsAudit.Rider(2,0,18,1),
        SlipDynamicsAudit.Rider(4,4,18,2),SlipDynamicsAudit.Rider(6,8,18,3),SlipDynamicsAudit.Rider(8,12,18,4)},90,4);
    public static Scenario Contact()=>new("D2-active-contact",new[]{SlipDynamicsAudit.Rider(5,0,20,1),
        SlipDynamicsAudit.Rider(5,3,18,2)},2);
    public static ControlCommand[] ContactControls()=>new[]{new ControlCommand(1,0,5,.4f),new ControlCommand(2,0,5,.4f)};

    public static Capture Execute(Scenario scenario,WorldParameters? parameters=null,ControlCommand[]? controls=null,
        bool reverse=false,string? csvPath=null, SpatialControlCommand[]? spatialControls=null, TrackStateSnapshot? surface=null,
        Action<RearWheelObservation>? wheelObserver=null,double stepSeconds=.01)
    {
        var forces=new Dictionary<int,ForceObservation>();var slips=new Dictionary<int,SlipObservation>();
        var yaws=new Dictionary<int,YawLimitObservation>();var applied=new Dictionary<int,AppliedForceObservation>();
        var contacts=new List<ContactPointObservation>();var consequences=new List<ConsequenceObservation>();
        using var world=new WorldRunner(reverse?scenario.Riders.Reverse():scenario.Riders,parameters:parameters??Parameters(),
            controls:controls,spatialControls:spatialControls,frozenSurface:surface,diagnostics:new(){Force=f=>forces[f.Rider.Id]=f,Slip=s=>slips[s.RiderId]=s,
                YawLimit=y=>yaws[y.RiderId]=y,AppliedForce=a=>applied[a.RiderId]=a,
                ContactPoint=contacts.Add,Consequence=consequences.Add,RearWheel=wheelObserver});
        CloseRacingAudit.ValidateInitial(world);
        var mechanical=scenario.Riders.ToDictionary(s=>s.Profile.Id,s=>world.MechanicalProperties(s.Profile.Id));
        var initial=world.Snapshot;var previous=initial;var ticks=new List<Tick>();
        using var csv=new HeatMeasurements(world.Frame,initial,csvPath,true);
        var result=world.Run(scenario.Name,stepSeconds,scenario.DurationSeconds,scenario.Laps,snapshot=>{
            var dt=snapshot.TimeSeconds-previous.TimeSeconds;
            foreach(var after in snapshot.Riders) {
                var old=previous.Riders.Single(v=>v.Id==after.Id);var id=after.Id;var f=forces.GetValueOrDefault(id);
                // Crashed/tape/passive paths do not emit ON tyre stages. Do not reuse a stale observation.
                var fresh=Math.Abs(f.TimeSeconds-previous.TimeSeconds)<1e-9 && f.Rider.Id==id;
                var before=fresh?f.Rider:old;var a=applied[id];
                var properties=mechanical[id];
                double K(RiderView v)=>.5*properties.MassKg*v.Velocity.LengthSquared()
                    +.5*properties.InertiaKgMetersSquared*v.AngularVelocity*v.AngularVelocity;
                var work=Vector2.Dot(a.ForceNewtons,(before.Velocity+after.Velocity)/2)*dt
                    +a.TorqueNewtonMeters*(before.AngularVelocity+after.AngularVelocity)/2*dt;
                var gap=snapshot.Riders.Where(v=>v.Id!=id).Select(v=>CloseRacingAudit.Separation(after,v,snapshot.TimeSeconds))
                    .DefaultIfEmpty(double.PositiveInfinity).Min();
                ticks.Add(new(previous.TimeSeconds,dt,before,after,fresh?f:default,
                    slips.TryGetValue(id,out var s)&&Math.Abs(s.TimeSeconds-previous.TimeSeconds)<1e-9?s:default,
                    yaws.TryGetValue(id,out var y)&&Math.Abs(y.TimeSeconds-previous.TimeSeconds)<1e-9?y:default,
                    a,K(after)-K(before),work,HeatMeasurements.Clearance(after,world.Frame),gap));
            }
            csv.Accept(snapshot);previous=snapshot;
        },lapsFromInitialPosition:true);
        return new(initial,result,ticks,contacts.ToArray(),consequences.ToArray(),mechanical);
    }
    public static string PrefixFingerprint(Capture capture,double throughSeconds)
    {
        // Exact serialized accepted states, including actuator values. No rounding/tolerances.
        var states=new[]{capture.Initial}.Concat(capture.Ticks.Where(t=>t.TimeSeconds+t.DurationSeconds<=throughSeconds+1e-10)
            .GroupBy(t=>t.TimeSeconds+t.DurationSeconds).Select(g=>new WorldSnapshot(g.Key,g.OrderBy(t=>t.After.Id).Select(t=>t.After).ToArray())));
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(states,Audit.Json)));
    }
    public static object Summary(Capture c)
    {
        var lapMeters=new TrackFrame(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack()).LapMeters;
        return new{c.Result.Completed,c.Result.TimeSeconds,c.Result.Fingerprint,c.Result.Work.SolverCalls,
            c.Result.Work.SolverSubsteps,c.Result.Work.Episodes,c.Result.Work.SolvedPoints,c.Result.Work.MaximumStepDisplacementMeters,
            MinimumPairSeparationMeters=c.Ticks.Min(t=>t.PairSeparationMeters),
            ContactPointCount=c.ContactPoints.Length,MaximumContactImpulse=c.ContactPoints.Select(p=>p.TotalNormalImpulseNewtonSeconds).DefaultIfEmpty().Max(),
            c.Result.FinishTimes,Riders=c.Initial.Riders.Select(first=>{
                var all=c.Ticks.Where(t=>t.Before.Id==first.Id).ToArray();var active=all.Where(t=>t.Slip.RiderId==first.Id).ToArray();
                var duration=all.Sum(t=>t.DurationSeconds);var yawDuration=active.Sum(t=>t.DurationSeconds);
                bool Limited(Tick t)=>Math.Abs(t.Yaw.AfterAuthorityNewtonMeters-t.Yaw.AfterEnergyNewtonMeters)>1e-5;
                bool Error(Tick t)=>Math.Abs(t.Yaw.OrientationErrorRadians)>5*Math.PI/180;
                var limited=active.Where(Limited).ToArray();var large=active.Where(Error).ToArray();
                double Mean(IEnumerable<Tick> ts,Func<Tick,double> f) {var a=ts.ToArray();return a.Length==0?0:a.Sum(t=>f(t)*t.DurationSeconds)/a.Sum(t=>t.DurationSeconds);}
                var cross=new LapCrossings();
                foreach(var t in all)cross.Accept(t.TimeSeconds,t.Before.TotalReferenceMeters-first.TotalReferenceMeters,
                    t.TimeSeconds+t.DurationSeconds,t.After.TotalReferenceMeters-first.TotalReferenceMeters,
                    lapMeters,4);
                var fromCorner=all.SkipWhile(t=>!t.After.Road.IsTurn).ToArray();
                var corner=fromCorner.TakeWhile(t=>t.After.Road.IsTurn).ToArray();
                var straight=fromCorner.Skip(corner.Length).TakeWhile(t=>!t.After.Road.IsTurn).ToArray();
                var entry=corner.FirstOrDefault();var exit=corner.LastOrDefault();var last=all[^1].After;
                var properties=c.Mechanical[first.Id];
                double K(RiderView v)=>.5*properties.MassKg*v.Velocity.LengthSquared()+.5*properties.Inertia*v.AngularVelocity*v.AngularVelocity;
                var times=cross.Times.ToArray();
                return new{RiderId=first.Id,CrossingSeconds=times,LapSeconds=times.Select((v,i)=>v-(i==0?0:times[i-1])).ToArray(),
                    last.DistanceMeters,ProgressMeters=last.TotalReferenceMeters-first.TotalReferenceMeters,
                    MinimumClearanceMeters=all.Min(t=>t.ClearanceMeters),MaximumOffsetErrorMeters=all.Max(t=>Math.Abs(t.After.Road.OffsetMeters-t.After.TargetOffsetMeters)),
                    MeanOffsetErrorMeters=Mean(all,t=>Math.Abs(t.After.Road.OffsetMeters-t.After.TargetOffsetMeters)),
                    EntrySeconds=entry?.TimeSeconds,EntrySpeed=entry?.After.Speed,MinimumCornerSpeed=corner.Select(t=>t.After.Speed).DefaultIfEmpty().Min(),
                    ExitSpeed=exit?.After.Speed,NextStraightMaximumSpeed=straight.Select(t=>t.After.Speed).DefaultIfEmpty().Max(),
                    EntryOffset=entry?.After.Road.OffsetMeters,ExitOffset=exit?.After.Road.OffsetMeters,
                    TargetFiveDegreesSeconds=active.FirstOrDefault(t=>Math.Abs(t.Slip.TargetSlipDegrees)>=5)?.TimeSeconds,
                    ActualFiveDegreesSeconds=active.FirstOrDefault(t=>Math.Abs(t.Slip.ActualSlipDegrees)>=5)?.TimeSeconds,
                    MeanAbsoluteSlipDegrees=Mean(active,t=>Math.Abs(t.Slip.ActualSlipDegrees)),MaximumAbsoluteSlipDegrees=active.Select(t=>Math.Abs(t.Slip.ActualSlipDegrees)).DefaultIfEmpty().Max(),
                    MeanDrive=Mean(all,t=>t.Before.Drive),MaximumUtilization=active.Select(t=>t.Slip.GripUtilization).DefaultIfEmpty().Max(),
                    NearGripSeconds=active.Where(t=>t.Slip.GripUtilization>=.95).Sum(t=>t.DurationSeconds),
                    SlipLossJoules=active.Sum(t=>t.Slip.LateralLossWatts*t.DurationSeconds),
                    AppliedWorkJoules=all.Sum(t=>t.WorkJoules),InitialKineticJoules=K(first),FinalKineticJoules=K(last),
                    WorkResidualJoules=K(last)-K(first)-all.Sum(t=>t.WorkJoules),
                    MaximumZeroDriveEnergyIncreaseJoules=all.Where(t=>t.Before.Drive==0).Select(t=>t.KineticChangeJoules).DefaultIfEmpty().Max(),
                    YawObservedSeconds=yawDuration,EnergyLimitedPercent=100*limited.Sum(t=>t.DurationSeconds)/yawDuration,
                    // Technique authority scales every nonzero request; distinguish it from force saturation.
                    AuthorityScalingPercent=100*active.Where(t=>Math.Abs(t.Yaw.RequestedNewtonMeters-t.Yaw.AfterAuthorityNewtonMeters)>1e-5).Sum(t=>t.DurationSeconds)/yawDuration,
                    ForceSaturatedPercent=100*active.Where(t=>Math.Abs(t.Yaw.RequestedNewtonMeters)>t.Slip.GripBudgetNewtons*SlipCoupledDynamics.YawLeverArmMeters+1e-5).Sum(t=>t.DurationSeconds)/yawDuration,
                    MaximumRequestedToForceLimitRatio=active.Max(t=>Math.Abs(t.Yaw.RequestedNewtonMeters)/(t.Slip.GripBudgetNewtons*SlipCoupledDynamics.YawLeverArmMeters)),
                    MeanRemovedTorqueNewtonMeters=Mean(active,t=>Math.Abs(t.Yaw.AfterAuthorityNewtonMeters)-Math.Abs(t.Yaw.AfterEnergyNewtonMeters)),
                    MaximumRemovedTorqueNewtonMeters=active.Select(t=>Math.Abs(t.Yaw.AfterAuthorityNewtonMeters)-Math.Abs(t.Yaw.AfterEnergyNewtonMeters)).DefaultIfEmpty().Max(),
                    RemovedTorqueFraction=active.Sum(t=>(Math.Abs(t.Yaw.AfterAuthorityNewtonMeters)-Math.Abs(t.Yaw.AfterEnergyNewtonMeters))*t.DurationSeconds)
                        /Math.Max(1e-20,active.Sum(t=>Math.Abs(t.Yaw.AfterAuthorityNewtonMeters)*t.DurationSeconds)),
                    MeanAbsoluteOrientationErrorDegrees=Mean(active,t=>Math.Abs(t.Yaw.OrientationErrorRadians)*180/Math.PI),
                    MaximumAbsoluteOrientationErrorDegrees=active.Select(t=>Math.Abs(t.Yaw.OrientationErrorRadians)*180/Math.PI).DefaultIfEmpty().Max(),
                    LargeErrorSeconds=large.Sum(t=>t.DurationSeconds),LimitedAndLargeErrorSeconds=limited.Where(Error).Sum(t=>t.DurationSeconds),
                    LimitedMeanOrientationErrorDegrees=Mean(limited,t=>Math.Abs(t.Yaw.OrientationErrorRadians)*180/Math.PI),
                    UnlimitedMeanOrientationErrorDegrees=Mean(active.Where(t=>!Limited(t)),t=>Math.Abs(t.Yaw.OrientationErrorRadians)*180/Math.PI),
                    LimitedMeanOffsetErrorMeters=Mean(limited,t=>Math.Abs(t.Before.Road.OffsetMeters-t.Before.TargetOffsetMeters)),
                    UnlimitedMeanOffsetErrorMeters=Mean(active.Where(t=>!Limited(t)),t=>Math.Abs(t.Before.Road.OffsetMeters-t.Before.TargetOffsetMeters)),
                    MaximumAppliedTorqueMismatch=active.Max(t=>Math.Abs(t.Yaw.AfterEnergyNewtonMeters-t.Applied.TorqueNewtonMeters)),
                    MaximumHeldYawWorkExcessWatts=active.Max(t=>t.Yaw.AfterEnergyNewtonMeters*(double)t.Yaw.AngularVelocity
                        +SlipCoupledDynamics.MaximumForceHoldSeconds*t.Yaw.AfterEnergyNewtonMeters*(double)t.Yaw.AfterEnergyNewtonMeters/(2*t.Yaw.InertiaKgMetersSquared)-t.Yaw.DissipationCreditWatts),
                    MaximumSpeed=all.Max(t=>t.After.Speed),FinalActivity=last.Activity.ToString(),DurationSeconds=duration};
            }).ToArray()};
    }
    private static void Save(string directory,string name,object value)
    {
        var path=Path.Combine(directory,name+".json");
        if(File.Exists(path))throw new InvalidOperationException("Evidence already exists: "+path);
        File.WriteAllText(path,JsonSerializer.Serialize(value,Audit.Json)+"\n");
    }
    private static Capture Record(string directory,Scenario scenario,WorldParameters? p=null,ControlCommand[]? controls=null,bool reverse=false)
    {
        if(File.Exists(Path.Combine(directory,scenario.Name+".json"))||File.Exists(Path.Combine(directory,scenario.Name+"-accepted.csv")))
            throw new InvalidOperationException("Evidence already exists: "+scenario.Name);
        var c=Execute(scenario,p,controls,reverse,Path.Combine(directory,scenario.Name+"-accepted.csv"));
        // Aggregates use EVERY interval; long runs retain only one force sample per 500ms.
        var telemetryPeriod=scenario.DurationSeconds>5?.5:.1;
        var samples=c.Ticks.GroupBy(t=>(t.Before.Id,Bucket:(long)Math.Floor((t.TimeSeconds+1e-9)/telemetryPeriod)))
            .Select(g=>g.First()).OrderBy(t=>t.TimeSeconds).ThenBy(t=>t.Before.Id).ToArray();
        var summary=JsonSerializer.SerializeToElement(Summary(c),Audit.Json);
        Save(directory,scenario.Name,new{BaseHead,Parameters=p??Parameters(),Scenario=scenario,Controls=controls??Array.Empty<ControlCommand>(),TelemetryPeriodSeconds=telemetryPeriod,
            Run=new{Name=scenario.Name,c.Initial,c.Result,Riders=summary.GetProperty("Riders").EnumerateArray()
                .Select(r=>new{Measurement=r}).ToArray(),c.Consequences},Summary=summary,Telemetry=samples.Select(Sample).ToArray(),c.ContactPoints});
        Console.WriteLine($"{scenario.Name}: completed={c.Result.Completed} time={c.Result.TimeSeconds:F3} calls={c.Result.Work.SolverCalls} contacts={c.Result.Work.Episodes}");
        return c;
    }
    public static void Run(string directory,string stage)
    {
        Directory.CreateDirectory(directory);
        if(stage=="configuration") {
            var rider=SlipDynamicsAudit.Rider();using var w=new WorldRunner(new[]{rider},parameters:Parameters());
            var mechanical=w.MechanicalProperties(1);
            Save(directory,"configuration",new{BaseHead,Parameters=Parameters(),StepSeconds=.01,
                CanonicalProfile=rider.Profile.Gameplay,LegacyProfile=rider.Profile.Skills,rider.Setup,
                mechanical.MassKg,mechanical.InertiaKgMetersSquared,Surface=TrackSurfaceState.Default,
                Geometry=w.Frame.Track.Geometry,w.Frame.LapMeters,MaximumTargetMeters=w.Frame.MinimumUsableWidthMeters-.5f});return;
        }
        if(stage=="pilot") {
            var pilot=Record(directory,new("pilot-wide-control",new[]{SlipDynamicsAudit.Rider(8)},45,2));
            var middle=Record(directory,new("pilot-middle-control",new[]{SlipDynamicsAudit.Rider()},5));
            double Deadline(Tick t)=>Math.Ceiling((t.TimeSeconds+t.DurationSeconds-1e-9)*100)/100;
            var gas=Deadline(middle.Ticks.First(t=>t.After.Road.IsTurn&&t.After.Road.CornerProgress>=.2));
            var commands=new List<ControlCommand>();var states=pilot.Ticks.ToArray();var cursor=0;
            while(cursor<states.Length) {
                while(cursor<states.Length&&!states[cursor].After.Road.IsTurn)cursor++;
                if(cursor==states.Length)break;
                var corner=states.Skip(cursor).TakeWhile(t=>t.After.Road.IsTurn).ToArray();cursor+=corner.Length;
                commands.Add(new(1,Deadline(corner.First(t=>t.After.Road.CornerProgress>=.2)),5,1));
                commands.Add(new(1,Deadline(corner.First(t=>t.After.Road.CornerProgress>=.55)),3,1));
                if(cursor<states.Length)commands.Add(new(1,Deadline(states[cursor]),8,1));
            }
            Save(directory,"plan",new Plan(gas,commands.ToArray(),pilot.Result.Fingerprint));return;
        }
        if(stage=="benchmark") {Benchmark(directory);return;}
        if(stage!="measure")throw new ArgumentException("Use configuration, pilot, measure or benchmark.");
        var plan=JsonSerializer.Deserialize<Plan>(File.ReadAllText(Path.Combine(directory,"plan.json")),Audit.Json)!;
        var paired=new List<object>();Capture? control=null;
        foreach(var drive in new[]{.4f,.7f,1f}) {
            var c=Record(directory,new(FormattableString.Invariant($"A2-gas-{drive:F1}"),new[]{SlipDynamicsAudit.Rider()},plan.GasAtSeconds+1.1),
                controls:new[]{new ControlCommand(1,plan.GasAtSeconds,5,drive)});
            if(drive==1)control=c;
            paired.Add(new{Drive=drive,Prefix=PrefixFingerprint(c,plan.GasAtSeconds),
                Windows=new[]{0,.2,.5,1}.Select(dt=>Sample(c.Ticks.First(t=>t.TimeSeconds+1e-9>=plan.GasAtSeconds+dt))).ToArray()});
        }
        var state=control!.Ticks.First(t=>Math.Abs(t.TimeSeconds-plan.GasAtSeconds)<1e-9).Before;
        if(!state.Road.IsTurn)throw new InvalidOperationException("Chosen activation must lie in a real accepted corner.");
        var frame=new TrackFrame(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
        var surface=TrackState.CreateDefault(frame.Track).Snapshot();var prop=control.Mechanical[1];
        var direct=new List<SlipObservation>();
        foreach(var drive in new[]{.4f,.7f,1f})RiderController.Forces(state with{Drive=drive},SlipDynamicsAudit.Rider(),frame,surface,
            Parameters(),plan.GasAtSeconds,prop.MassKg,prop.Inertia,observeSlip:direct.Add);
        Save(directory,"A-gas-comparison",new{plan.GasAtSeconds,State=state,Mechanical=new{prop.MassKg,prop.Inertia},
            A1=direct.Select(s=>new{Slip=s,DrivePowerWatts=s.DriveForceNewtons*state.ForwardSpeed}).ToArray(),A2=paired});
        foreach(var yaw in new[]{.08f,.1f,.15f}) {
            if(yaw!=.1f)Record(directory,new(FormattableString.Invariant($"B-entry-{yaw:F2}"),new[]{SlipDynamicsAudit.Rider()},5),Parameters(yaw));
            Record(directory,new(FormattableString.Invariant($"B-full-{yaw:F2}"),new[]{SlipDynamicsAudit.Rider()},60,2),Parameters(yaw));
        }
        Record(directory,new("C-fixed-2",new[]{SlipDynamicsAudit.Rider(2)},60,2));
        Save(directory,"comparison-index",new{BEntryDefault="pilot-middle-control",CFixedMiddle="B-full-0.10",CFixedOuter="pilot-wide-control"});
        Record(directory,new("C-wide-tighten",new[]{SlipDynamicsAudit.Rider(8)},60,2),controls:plan.WideCommands);
        var pack=Pack();var heat=Record(directory,pack);
        var reverse=Execute(pack,reverse:true);
        using(var noObservers=new WorldRunner(pack.Riders,parameters:Parameters())) {
            var plain=noObservers.Run(pack.Name,.01,pack.DurationSeconds,pack.Laps,lapsFromInitialPosition:true);
            Save(directory,"D1-parity",new{Forward=heat.Result.Fingerprint,Reverse=reverse.Result.Fingerprint,Unobserved=plain.Fingerprint,
                ForwardCalls=heat.Result.Work.SolverCalls,ReverseCalls=reverse.Result.Work.SolverCalls,UnobservedCalls=plain.Work.SolverCalls,
                ReverseSummary=Summary(reverse)});
        }
        var contact=Record(directory,Contact(),controls:ContactControls());var contactReverse=Execute(Contact(),controls:ContactControls(),reverse:true);
        Save(directory,"D2-parity",new{Forward=contact.Result.Fingerprint,Reverse=contactReverse.Result.Fingerprint,
            SameContactEvents=contact.Result.Contacts.SequenceEqual(contactReverse.Result.Contacts),ReverseSummary=Summary(contactReverse)});
        Record(directory,new("D-zero-drive",new[]{SlipDynamicsAudit.Rider(reference:65)},8),controls:new[]{new ControlCommand(1,0,5,0)});
        var grid=new List<object>();const float budget=1800,mass=147;
        foreach(var u in new[]{5f,18f,30f})foreach(var angle in new[]{0f,3f,6f,12f,20f,35f}) {
            var w=-u*MathF.Tan(angle*MathF.PI/180);var v=state with{Heading=0,Velocity=new(u,w)};
            var tyre=SlipCoupledDynamics.Tyre(v,0,budget,mass);
            grid.Add(new{ForwardMetersPerSecond=u,SlipDegrees=angle,SideMetersPerSecond=w,BudgetNewtons=budget,MassKg=mass,
                tyre.LateralNewtons,LateralUtilization=Math.Abs(tyre.LateralNewtons)/budget,
                RemainingDriveNewtons=MathF.Sqrt(Math.Max(0,budget*budget-tyre.LateralNewtons*tyre.LateralNewtons)),tyre.LateralLossWatts});
        }
        Save(directory,"tyre-grid",grid);
    }
    private static void Benchmark(string directory)
    {
        var spec=SlipDynamicsAudit.Rider(reference:65);using var world=new WorldRunner(new[]{spec},parameters:Parameters());
        var state=world.Snapshot.Riders[0] with{Heading=world.Snapshot.Riders[0].Heading+.12f};
        var surface=TrackState.CreateDefault(world.Frame.Track).Snapshot();var mechanical=world.MechanicalProperties(1);
        var timings=new List<object>();float sink=0;Action<YawLimitObservation> yaw=_=>{};Action<ForceObservation> force=_=>{};Action<SlipObservation> slip=_=>{};
        foreach(var enabled in new[]{false,true}) {
            for(var i=0;i<10000;i++)sink+=RiderController.Forces(state,spec,world.Frame,surface,Parameters(),1,
                mechanical.MassKg,mechanical.InertiaKgMetersSquared,enabled?force:null,enabled?slip:null,enabled?yaw:null).Torque;
            // Parameters must be outside measured loop (no harness allocation attributed to Forces).
            var p=Parameters();var ms=new double[7];var bytes=new long[7];
            for(var batch=0;batch<7;batch++) {
                var allocated=GC.GetAllocatedBytesForCurrentThread();var start=Stopwatch.GetTimestamp();
                for(var i=0;i<100000;i++)sink+=RiderController.Forces(state,spec,world.Frame,surface,p,1,
                    mechanical.MassKg,mechanical.InertiaKgMetersSquared,enabled?force:null,enabled?slip:null,enabled?yaw:null).Torque;
                ms[batch]=Stopwatch.GetElapsedTime(start).TotalMilliseconds;bytes[batch]=GC.GetAllocatedBytesForCurrentThread()-allocated;
            }
            timings.Add(new{Observers=enabled,Calls=100000,Milliseconds=ms,AllocatedBytes=bytes});
        }
        var worlds=new List<object>();
        foreach(var enabled in new[]{false,true})for(var b=-1;b<7;b++) {
            var watch=Stopwatch.GetTimestamp();var pack=Pack();
            using var w=new WorldRunner(pack.Riders,parameters:Parameters(),diagnostics:enabled?new(){YawLimit=yaw,Force=force,Slip=slip,AppliedForce=_=>{}}:null);
            var r=w.Run(pack.Name,.01,pack.DurationSeconds,pack.Laps,lapsFromInitialPosition:true);
            var elapsed=Stopwatch.GetElapsedTime(watch).TotalMilliseconds;
            if(b>=0)worlds.Add(new{Observers=enabled,Milliseconds=elapsed,r.Fingerprint,r.Work.SolverCalls});
        }
        Save(directory,"performance",new{Forces=timings,Worlds=worlds,Sink=sink,BaseHead,
            Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            OS=System.Runtime.InteropServices.RuntimeInformation.OSDescription,TieredCompilation=Environment.GetEnvironmentVariable("DOTNET_TieredCompilation")});
    }
}
