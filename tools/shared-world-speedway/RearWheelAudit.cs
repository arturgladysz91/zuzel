using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;

namespace SharedWorldSpeedway;

/// <summary>Bounded #100 observations; no alternative body integrator or line search.</summary>
public static class RearWheelAudit
{
    public const string BaseHead="d504e3d252216eef0393d2ea2df2c0f64c7a6b87";
    public static readonly string[] Plans={"inner","middle-tighten","wide-tighten"};
    public static WorldParameters Parameters(bool wheel)=>SlipControlValidation.Parameters() with{EnableRearWheelDynamics=wheel};
    public static TrackFrame Frame()=>new(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
    private static readonly JsonSerializerOptions Json=new(Audit.Json){WriteIndented=false};
    public sealed record Capture(SlipControlValidation.Capture Body,RearWheelObservation[] Wheels);
    private static void Save(string directory,string name,object value)
    {
        Directory.CreateDirectory(directory);var path=Path.Combine(directory,name+".json");
        if(File.Exists(path))throw new IOException("Evidence already exists: "+path);
        File.WriteAllText(path,JsonSerializer.Serialize(value,Json)+"\n");
    }
    public static Capture Execute(Scenario scenario,bool wheel,ControlCommand[]? controls=null,
        SpatialControlCommand[]? spatial=null,TrackStateSnapshot? surface=null,string? csv=null,bool reverse=false,double step=.01)
    {
        var observations=new List<RearWheelObservation>();
        var c=SlipControlValidation.Execute(scenario,Parameters(wheel),controls,reverse,csv,spatial,surface,observations.Add,step);
        return new(c,observations.ToArray());
    }
    public static string Prefix(Capture c,double through)
        =>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{Body=SlipControlValidation.PrefixFingerprint(c.Body,through),
            Wheels=c.Wheels.Where(w=>w.TimeSeconds+w.Step.Seconds<=through+1e-8).Select(w=>new{w.TimeSeconds,w.RiderId,w.Step.AfterOmega})},Json)));
    public static double[] CornerEnds(TrackFrame frame)
    {
        var starts=new List<double>{0};foreach(var s in frame.Embedding.Segments)
            starts.Add(starts[^1]+(s.SegmentType==SegmentType.Straight?s.StraightLengthMeters:s.InnerRadiusMeters*s.TurnAngleRadians));
        return frame.Track.CornerTopology.Corners.Select(c=>starts[c.StartSegmentIndex+c.SegmentCount]).ToArray();
    }
    private static RearWheelStep? Wheel(Capture c,int index)=>c.Wheels.Length==0?null:c.Wheels[index].Step;
    public static object Point(SlipControlValidation.Tick t,RearWheelStep? wheel,RacingLineGeometry geometry)
    {
        var v=t.Before;var f=new Vector2(MathF.Cos(v.Heading),MathF.Sin(v.Heading));var side=new Vector2(-f.Y,f.X);
        var gap=geometry.Clearance(v);var lateral=side*t.Slip.LateralForceNewtons;var drive=f*t.Slip.DriveForceNewtons;
        var p=RearWheelParameters.Default;
        return new{t.TimeSeconds,t.DurationSeconds,ReferenceMeters=v.TotalReferenceMeters,v.Position.X,v.Position.Y,v.Speed,v.Heading,
            TravelHeading=t.Slip.TravelHeadingRadians,t.Slip.ActualSlipDegrees,t.Slip.TargetSlipDegrees,
            RoadOutwardVelocity=Vector2.Dot(v.Velocity,v.Road.Outward),BikeSideVelocity=v.SideSpeed,
            Omega=wheel?.BeforeOmega,Rpm=wheel?.BeforeOmega*30/Math.PI,CircumferentialSpeed=wheel?.BeforeOmega*p.WheelRadiusMeters,
            SlipRatio=wheel?.InstantSlipRatio,DriveTorque=wheel?.DriveTorqueNewtonMeters,AvailableTorque=wheel?.AvailableTorqueNewtonMeters,
            RequestedTorque=wheel?.RequestedTorqueNewtonMeters,FrontFy=wheel?.FrontLateralNewtons,RearFy=wheel?.RearLateralNewtons,
            WheelEnergyJoules=wheel is { } w?.5*p.WheelInertiaKgMetersSquared*w.BeforeOmega*w.BeforeOmega:(double?)null,
            LongitudinalSlipLossWatts=wheel is { } x&&x.Seconds>0?x.LongitudinalLossJoules/x.Seconds:(double?)null,
            t.Slip.DriveForceNewtons,t.Slip.LateralForceNewtons,t.Slip.GripBudgetNewtons,t.Slip.GripUtilization,
            t.Yaw.RequestedNewtonMeters,t.Yaw.AfterAuthorityNewtonMeters,t.Yaw.AfterEnergyNewtonMeters,
            EnergyLimited=Math.Abs(t.Yaw.AfterAuthorityNewtonMeters-t.Yaw.AfterEnergyNewtonMeters)>1e-5,t.Force.DesiredInwardNewtons,
            LateralOutwardForce=Vector2.Dot(lateral,v.Road.Outward),DriveOutwardForce=Vector2.Dot(drive,v.Road.Outward),
            ResistanceOutwardForce=Vector2.Dot(t.Applied.ForceNewtons-lateral-drive,v.Road.Outward),
            v.Road.OffsetMeters,v.TargetOffsetMeters,InnerClearance=gap.Inner,OuterClearance=gap.Outer,v.Drive};
    }
    public static object Energy(Capture c)
    {
        var p=RearWheelParameters.Default;double engine=0,bearing=0,longitudinal=0,physicalLongitudinal=0,lateral=0,resistance=0,yaw=0,wheelChange=0;
        var maxRoot=0d;var minLong=0d;var maxPositiveNoEngine=0d;var maxGuardExcess=0d;
        for(var i=0;i<c.Body.Ticks.Count;i++){
            var t=c.Body.Ticks[i];var ws=Wheel(c,i);var middle=(t.Before.Velocity+t.After.Velocity)/2;
            var f=new Vector2(MathF.Cos(t.Before.Heading),MathF.Sin(t.Before.Heading));var side=new Vector2(-f.Y,f.X);
            var dt=ws?.Seconds??t.DurationSeconds;
            var driveWork=t.Slip.DriveForceNewtons*Vector2.Dot(middle,f)*dt;
            var lateralLoss=-t.Slip.LateralForceNewtons*Vector2.Dot(middle,side)*dt;
            lateral+=lateralLoss;resistance+=Vector2.Dot(t.Applied.ForceNewtons,middle)*dt-driveWork+lateralLoss;
            yaw+=t.Applied.TorqueNewtonMeters*(t.Before.AngularVelocity+t.After.AngularVelocity)/2*dt;
            if(ws is { } w){
                var dw=.5*p.WheelInertiaKgMetersSquared*(w.AfterOmega*w.AfterOmega-w.BeforeOmega*w.BeforeOmega);
                wheelChange+=dw;engine+=w.EngineWorkJoules;bearing+=w.BearingLossJoules;
                var loss=w.LongitudinalNewtons*(p.WheelRadiusMeters*w.MidOmega-Vector2.Dot(middle,f))*dt;
                longitudinal+=loss;physicalLongitudinal+=w.LongitudinalLossJoules;minLong=Math.Min(minLong,loss);
                maxRoot=Math.Max(maxRoot,Math.Abs(w.RootResidualMetersPerSecond));
                if(w.DriveTorqueNewtonMeters==0)maxPositiveNoEngine=Math.Max(maxPositiveNoEngine,t.KineticChangeJoules+dw);
                if(t.Yaw.InertiaKgMetersSquared>0)maxGuardExcess=Math.Max(maxGuardExcess,t.Applied.TorqueNewtonMeters*t.Before.AngularVelocity
                    +dt*t.Applied.TorqueNewtonMeters*t.Applied.TorqueNewtonMeters/(2*t.Yaw.InertiaKgMetersSquared)-t.Yaw.DissipationCreditWatts);
            }else engine+=driveWork;
        }
        var body=c.Body.Ticks.Sum(t=>t.KineticChangeJoules);
        return new{BodyKineticChangeJoules=body,WheelKineticChangeJoules=wheelChange,EngineWorkJoules=engine,
            BearingLossJoules=bearing,LongitudinalSlipLossJoules=longitudinal,ConstitutiveLongitudinalLossJoules=physicalLongitudinal,
            LateralSlipLossJoules=lateral,ResistanceWorkJoules=resistance,YawWorkJoules=yaw,
            BalanceResidualJoules=body+wheelChange-engine+bearing+longitudinal+lateral-resistance-yaw,
            MaximumRootResidual=maxRoot,MinimumIntervalLongitudinalLossJoules=minLong,
            MaximumNoEngineKineticIncreaseJoules=maxPositiveNoEngine,MaximumYawGuardExcessWatts=maxGuardExcess};
    }
    public static object[] Exits(Capture c)
    {
        var frame=Frame();var rows=new List<object>();var ends=CornerEnds(frame);var geometry=new RacingLineGeometry(frame);
        for(var lap=0;lap<3;lap++)foreach(var end in ends){
            var reference=lap*frame.LapMeters+end;
            var window=c.Body.Ticks.Select((t,i)=>(t,i)).Where(x=>x.t.Before.TotalReferenceMeters>=reference-25&&x.t.Before.TotalReferenceMeters<=reference+25).ToArray();
            var straight=window.Where(x=>x.t.Before.TotalReferenceMeters>=reference).ToArray();
            if(straight.Length==0)continue;
            var first=straight[0].t;var before=first.Before;var axis=before.Road.Outward;var initial=Vector2.Dot(before.Velocity,axis);
            double impulse=0,driveImpulse=0,resistanceImpulse=0;
            foreach(var (t,_) in straight){var f=new Vector2(MathF.Cos(t.Before.Heading),MathF.Sin(t.Before.Heading));var side=new Vector2(-f.Y,f.X);
                impulse+=t.Slip.LateralForceNewtons*Vector2.Dot(side,axis)*t.DurationSeconds;
                driveImpulse+=t.Slip.DriveForceNewtons*Vector2.Dot(f,axis)*t.DurationSeconds;
                resistanceImpulse+=Vector2.Dot(t.Applied.ForceNewtons-f*t.Slip.DriveForceNewtons-side*t.Slip.LateralForceNewtons,axis)*t.DurationSeconds;}
            var last=straight[^1].t.After;var duration=straight.Sum(x=>x.t.DurationSeconds);
            var reverse=straight.FirstOrDefault(x=>initial>0&&Vector2.Dot(x.t.After.Velocity,axis)<=0).t;
            var small=straight.FirstOrDefault(x=>Math.Abs(Vector2.Dot(x.t.After.Velocity,axis))<=.1).t;
            rows.Add(new{Lap=lap+1,EndReference=reference,ExitSeconds=first.TimeSeconds,ExitSpeed=before.Speed,ExitSlipDegrees=first.Slip.ActualSlipDegrees,
                ExitSlipRatio=Wheel(c,straight[0].i)?.InstantSlipRatio,ExitOutwardVelocity=initial,ExitOffset=before.Road.OffsetMeters,
                WideningMeters=straight.Max(x=>Math.Max(x.t.Before.Road.OffsetMeters,x.t.After.Road.OffsetMeters))-before.Road.OffsetMeters,
                NoForceOutwardDisplacementMeters=initial*duration,ActualOutwardDisplacementMeters=last.Road.OffsetMeters-before.Road.OffsetMeters,
                ReversalSeconds=reverse is null?(double?)null:reverse.TimeSeconds+reverse.DurationSeconds-first.TimeSeconds,
                SmallOutwardVelocitySeconds=small is null?(double?)null:small.TimeSeconds+small.DurationSeconds-first.TimeSeconds,
                LateralImpulse=impulse,DriveImpulse=driveImpulse,ResistanceImpulse=resistanceImpulse,
                VelocityResidual=Vector2.Dot(last.Velocity,axis)-initial-(impulse+driveImpulse+resistanceImpulse)/c.Body.Mechanical[1].MassKg,
                First=Point(window[0].t,Wheel(c,window[0].i),geometry),AtExit=Point(first,Wheel(c,straight[0].i),geometry),
                Last=Point(window[^1].t,Wheel(c,window[^1].i),geometry),Intervals=window.Length});
        }
        return rows.ToArray();
    }
    private static void Trace(string path,Capture c,bool exitsOnly)
    {
        if(File.Exists(path))throw new IOException("Evidence already exists: "+path);
        var geometry=new RacingLineGeometry(Frame());var frame=Frame();var ends=CornerEnds(frame);
        using var file=new StreamWriter(path);file.NewLine="\n";var header=false;
        for(var i=0;i<c.Body.Ticks.Count;i++){
            var t=c.Body.Ticks[i];var phase=t.Before.TotalReferenceMeters%frame.LapMeters;
            if(exitsOnly&&!ends.Any(e=>Math.Abs(phase-e)<=25))continue;
            var point=JsonSerializer.SerializeToElement(Point(t,Wheel(c,i),geometry),Json);
            if(!header){file.WriteLine(string.Join(',',point.EnumerateObject().Select(p=>p.Name)));header=true;}
            file.WriteLine(string.Join(',',point.EnumerateObject().Select(p=>p.Value.ValueKind switch{JsonValueKind.Null=>"",JsonValueKind.True=>"1",JsonValueKind.False=>"0",_=>p.Value.GetRawText()})));
        }
    }
    private static void Replay(string directory,string name,Capture c,object summary)
    {
        var measurements=JsonSerializer.SerializeToElement(SlipControlValidation.Summary(c.Body),Json).GetProperty("Riders");
        var geometry=new RacingLineGeometry(Frame());
        Save(directory,name,new{Run=new{c.Body.Initial,c.Body.Result,Passes=Array.Empty<object>(),
            Riders=measurements.EnumerateArray().Select(m=>new{RiderId=m.GetProperty("RiderId").GetInt32(),Measurement=m}),c.Body.Consequences},
            Summary=summary,Telemetry=c.Body.Ticks.Select((t,i)=>(t,i)).Where(x=>x.i%10==0).Select(x=>Point(x.t,Wheel(c,x.i),geometry))});
    }
    private static void Freeze(string directory)
    {
        Save(directory,"configuration",new{BaseHead,Parameters=Parameters(true),Wheel=RearWheelParameters.Default,
            FrozenPlans=PhysicsDrivenRacingLine.Plans().Where(p=>Plans.Contains(p.Name)),Conditions=PhysicsDrivenRacingLineAudit.Conditions().Take(2),
            StepSeconds=.01,Laps=3,SignificantWheelspinRatio=.2,SignificantSlideDegrees=15,
            Hypothesis="Only wheel state plus dissipative combined rear contact; no post-peak lateral curve, no Drive penalty, no re-tuning.",
            Assumptions=new{Radius="0.33m effective rolling radius, assumed; no measured tyre specification.",Inertia="0.9kgm² equivalent to 8.26kg thin ring at 0.33m; an effective rotating mass assumption, no load/mass added to chassis.",
                Load="Static 40% front/60% rear split of inherited budget, forces at COM; no extra axle yaw moment.",
                Stiffness="0.1 longitudinal nondimensional slope scale; 30ms minimum relaxation bounds stiff low-speed coupling, not clipped spin.",
                Drive="Inherited one-gear reference force/envelope evaluated at wheel circumference; no separate engine state.",Bearing="0.02Nms viscous bearing resistance, provisional."},
            Probe="Each model's own unchanged accepted inner-plan prefix: first corner cp>=0.2 and |slide|>=15deg, plus first accepted exit. Drive0.4/0.7/1 for 1s; samples0/.2/.5/1. Preserve original landmarks.",
            NoRetuning=true});
    }
    public static Capture Probe(Capture baseline,SlipControlValidation.Tick anchor,float drive,bool wheel)
    {
        var reference=anchor.Before.TotalReferenceMeters;
        var controls=PhysicsDrivenRacingLine.Compile(PhysicsDrivenRacingLine.Plans().Single(p=>p.Name=="inner"),Frame())
            .Where(c=>Math.Abs(c.ReferenceMeters-reference)>1e-9).Select(c=>c.ReferenceMeters>=reference?c with{Drive=drive}:c).ToList();
        controls.Add(new(1,reference,anchor.Before.TargetOffsetMeters,drive));
        var c=Execute(new("causal-throttle",new[]{PhysicsDrivenRacingLineAudit.Rider(PhysicsDrivenRacingLineAudit.Conditions()[0])},anchor.TimeSeconds+1.03),
            wheel,spatial:controls.ToArray());
        if(Prefix(c,anchor.TimeSeconds)!=Prefix(baseline,anchor.TimeSeconds))throw new InvalidOperationException("Accepted body/wheel prefix changed.");
        if(!c.Body.Result.Decisions.Any(d=>Math.Abs(d.ActualActivation!.Value-anchor.TimeSeconds)<1e-8&&d.Drive==drive))
            throw new InvalidOperationException("Wrong throttle activation.");
        return c;
    }
    private static object InstantQuery(Capture c,int index,float drive)
    {
        var t=c.Body.Ticks[index];var frame=Frame();var parameters=Parameters(c.Wheels.Length>0);var spec=SlipDynamicsAudit.Rider();
        var mechanical=c.Body.Mechanical[1];var surface=TrackState.CreateDefault(frame.Track).Snapshot();
        var state=c.Wheels.Length==0?null:new RearWheelState(c.Wheels[index].Step.BeforeOmega*RearWheelParameters.Default.WheelRadiusMeters);
        SlipObservation slip=default;YawLimitObservation yaw=default;
        var force=RiderController.Forces(t.Before with{Drive=drive},spec,frame,surface,parameters,t.TimeSeconds,mechanical.MassKg,mechanical.Inertia,
            observeSlip:s=>slip=s,observeYaw:y=>yaw=y,rearWheel:state,stepSeconds:0);
        return new{Drive=drive,slip.DriveForceNewtons,slip.LateralForceNewtons,slip.ActualSlipDegrees,slip.TargetSlipDegrees,force.Torque,
            Wheel=state?.Prepared};
    }
    private static void Curves(string directory)
    {
        var p=RearWheelParameters.Default;var rows=new List<object>();const float budget=1800,mass=147;
        foreach(var u in new[]{0d,5d,20d,30d})foreach(var angle in new[]{0d,15d,30d})
        foreach(var kappa in new[]{-1d,-.2,-.1,0,.05,.1,.2,.5,1,2,5}){
            var side=-u*Math.Tan(angle*Math.PI/180);var omega=(u+kappa*Math.Sqrt(u*u+.25))/p.WheelRadiusMeters;
            var scale=.12*.12*(u*u+.25)+Math.Pow(budget/mass*.03,2);
            var s=RearWheelDynamics.Evaluate(u,side,omega,budget,mass,scale,0,0,0,0,0,p);
            rows.Add(new{ForwardSpeed=u,SlideDegrees=angle,SlipRatio=kappa,Omega=omega,s.LongitudinalNewtons,s.FrontLateralNewtons,s.RearLateralNewtons,
                s.LateralNewtons,s.RearUtilization,LongitudinalLossWatts=s.LongitudinalNewtons*(p.WheelRadiusMeters*omega-u),LateralLossWatts=-s.LateralNewtons*side});
        }
        Save(directory,"contact-curves",new{BudgetNewtons=budget,MassKg=mass,Parameters=p,Rows=rows});
    }
    private static void Measure(string directory)
    {
        using var frozen=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"configuration.json")));
        if(frozen.RootElement.GetProperty("BaseHead").GetString()!=BaseHead
            ||frozen.RootElement.GetProperty("Wheel").GetRawText()!=JsonSerializer.Serialize(RearWheelParameters.Default,Json))
            throw new InvalidOperationException("Frozen wheel configuration differs.");
        Curves(directory);var frame=Frame();var geometry=new RacingLineGeometry(frame);var all=new List<object>();var probes=new List<object>();
        var costWatch=Stopwatch.GetTimestamp();var captureMs=0d;var analysisMs=0d;var solver=0L;var calls=0L;
        using var previous=JsonDocument.Parse(File.ReadAllText(Path.Combine("docs/experiments/physics-driven-racing-line","stage1.json")));
        foreach(var wheel in new[]{false,true})foreach(var condition in PhysicsDrivenRacingLineAudit.Conditions().Take(2))foreach(var planName in Plans){
            var key=(wheel?"new":"old")+"-"+condition.Name+"-"+planName;var observations=new List<RearWheelObservation>();
            var (e,body)=PhysicsDrivenRacingLineAudit.Evaluate(condition,PhysicsDrivenRacingLine.Plans().Single(p=>p.Name==planName),
                Path.Combine(directory,key+"-accepted.csv"),Parameters(wheel),observations.Add);
            var c=new Capture(body,observations.ToArray());captureMs+=e.CaptureMilliseconds;analysisMs+=e.ValidationMilliseconds;solver+=e.SolverCalls;
            calls+=wheel?body.Result.Work.RearWheelForceEvaluations+body.Result.Work.RearWheelBoundaryEvaluations:body.Ticks.Count;
            if(!wheel){var old=previous.RootElement.EnumerateArray().Single(r=>r.GetProperty("Case").GetString()==condition.Name&&r.GetProperty("Plan").GetProperty("Name").GetString()==planName);
                if(e.Fingerprint!=old.GetProperty("Fingerprint").GetString())throw new InvalidOperationException("Historical #99 fingerprint changed.");}
            var flight=body.Ticks.Select((t,i)=>(t,i)).Where(x=>x.t.Before.TotalReferenceMeters>=frame.LapMeters).ToArray();
            var ws=observations.Where(w=>w.Before.TotalReferenceMeters>=frame.LapMeters).ToArray();
            var energy=Energy(c);var balance=JsonSerializer.SerializeToElement(energy,Json).GetProperty("BalanceResidualJoules").GetDouble();
            var rejection=e.Rejections.ToList();if(Math.Abs(balance)>5)rejection.Add("WholeSystemEnergyBalance");
            if(observations.Any(w=>w.Step.LongitudinalLossJoules<-.00001))rejection.Add("NegativeLongitudinalDissipation");
            var row=new{Model=wheel?"wheel":"historical",e.Case,Plan=planName,Evaluation=e,Feasible=rejection.Count==0,Rejections=rejection,
                InitialBodyFingerprint=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(body.Initial.Riders,Json))),
                MinimumFlyingCornerSpeed=flight.Where(x=>x.t.Before.Road.IsTurn).Min(x=>Math.Min(x.t.Before.Speed,x.t.After.Speed)),
                MaximumSlideDegrees=flight.Max(x=>Math.Abs(x.t.Slip.ActualSlipDegrees)),
                SignificantSlideSeconds=flight.Where(x=>Math.Abs(x.t.Slip.ActualSlipDegrees)>=15).Sum(x=>x.t.DurationSeconds),
                MaximumSlipRatio=ws.Select(w=>Math.Abs(w.Step.InstantSlipRatio)).DefaultIfEmpty().Max(),
                SignificantWheelspinSeconds=ws.Where(w=>w.Step.InstantSlipRatio>=.2).Sum(w=>w.Step.Seconds),
                MeanAbsoluteFy=flight.Average(x=>Math.Abs(x.t.Slip.LateralForceNewtons)),MeanUtilization=flight.Average(x=>x.t.Slip.GripUtilization),
                MaximumBikeSideSpeed=flight.Max(x=>Math.Abs(x.t.Before.SideSpeed)),
                MeanAbsoluteTrackingError=flight.Average(x=>Math.Abs(x.t.Before.Road.OffsetMeters-x.t.Before.TargetOffsetMeters)),
                MaximumOutwardTrackingError=flight.Max(x=>x.t.Before.Road.OffsetMeters-x.t.Before.TargetOffsetMeters),
                EnergyLimitedSteps=body.Ticks.Count(t=>Math.Abs(t.Yaw.AfterEnergyNewtonMeters-t.Yaw.AfterAuthorityNewtonMeters)>1e-5),
                Energy=energy,Exits=Exits(c)};
            all.Add(row);Trace(Path.Combine(directory,key+"-exits.csv"),c,true);Replay(directory,key,c,row);
            Console.WriteLine($"{key}: feasible={row.Feasible} flight={e.FlyingSeconds:F6} minCorner={row.MinimumFlyingCornerSpeed:F3} kappa={row.MaximumSlipRatio:F3} gaps={e.MinimumInnerClearanceMeters:F3}/{e.MinimumOuterClearanceMeters:F3} energy={balance:F4}");
            if(condition.Name=="A-neutral"&&planName=="inner"){
                var anchors=new[]{(Name:"corner",Index:body.Ticks.FindIndex(t=>t.Before.Road.IsTurn&&t.Before.Road.CornerProgress>=.2&&Math.Abs(t.Slip.ActualSlipDegrees)>=15)),
                    (Name:"exit",Index:body.Ticks.FindIndex(t=>!t.Before.Road.IsTurn&&t.Before.TotalReferenceMeters>CornerEnds(frame)[0]))};
                foreach(var anchor in anchors){
                    if(anchor.Index<0)throw new InvalidOperationException("Frozen probe rule found no state.");
                    var original=body.Ticks[anchor.Index];var trials=new List<object>();
                    foreach(var drive in new[]{.4f,.7f,1f}){
                        var trial=Probe(c,original,drive,wheel);
                        var sampleRows=new[]{0d,.2,.5,1}.Select(delay=>{
                            var i=trial.Body.Ticks.FindIndex(t=>t.TimeSeconds>=original.TimeSeconds+delay-1e-9);
                            return new{DelaySeconds=delay,ActualDelay=trial.Body.Ticks[i].TimeSeconds-original.TimeSeconds,Sample=Point(trial.Body.Ticks[i],Wheel(trial,i),geometry)};}).ToArray();
                        var name=(wheel?"new":"old")+"-"+anchor.Name+"-drive"+drive.ToString("0.0",CultureInfo.InvariantCulture);
                        Trace(Path.Combine(directory,name+"-trace.csv"),new Capture(trial.Body with{Ticks=trial.Body.Ticks.Where(t=>t.TimeSeconds>=original.TimeSeconds-1e-9).ToList()},
                            trial.Wheels.Where(w=>w.TimeSeconds>=original.TimeSeconds-1e-9).ToArray()),false);
                        trials.Add(new{Drive=drive,PrefixFingerprint=Prefix(trial,original.TimeSeconds),trial.Body.Result.Fingerprint,Samples=sampleRows,Energy=Energy(trial)});
                    }
                    probes.Add(new{Model=wheel?"wheel":"historical",Anchor=anchor.Name,ActivationSeconds=original.TimeSeconds,
                        AcceptedState=original.Before,AcceptedWheel=Wheel(c,anchor.Index),InstantQueries=new[]{.4f,.7f,1f}.Select(d=>InstantQuery(c,anchor.Index,d)),Trials=trials});
                }
            }
        }
        Save(directory,"line-comparison",all);Save(directory,"throttle-probes",probes);
        var straight=new List<object>();
        foreach(var item in new[]{(Name:"free-roll",Speed:18f,Grip:1f,Drive:0f,Cut:false),
            (Name:"launch",Speed:0f,Grip:1f,Drive:1f,Cut:false),(Name:"launch-cut",Speed:0f,Grip:1f,Drive:1f,Cut:true),
            (Name:"launch-low",Speed:0f,Grip:.85f,Drive:1f,Cut:false),(Name:"launch-stress",Speed:0f,Grip:.35f,Drive:1f,Cut:false)}){
            var commands=new List<ControlCommand>{new(1,0,5,item.Drive)};if(item.Cut)commands.Add(new(1,1,5,.4f));
            var duration=item.Speed>0?1.2:2d;var surface=TrackState.CreateDefault(frame.Track,new(item.Grip,0,.5f)).Snapshot();
            var key="straight-"+item.Name;var c=Execute(new(key,new[]{SlipDynamicsAudit.Rider(speed:item.Speed)},duration),true,commands.ToArray(),surface:surface,csv:Path.Combine(directory,key+"-accepted.csv"));
            var samples=new[]{0d,.25,.5,.75,1,1.2,1.5,1.9}.Where(t=>t<duration).Select(time=>{
                var i=c.Body.Ticks.FindIndex(t=>t.TimeSeconds>=time-1e-9);return Point(c.Body.Ticks[i],Wheel(c,i),geometry);}).ToArray();
            var summary=new{item.Name,item.Grip,Energy=Energy(c),Samples=samples};straight.Add(summary);
            Trace(Path.Combine(directory,key+"-trace.csv"),c,false);Replay(directory,key,c,summary);
        }
        Save(directory,"straight-comparison",straight);
        Save(directory,"experiment-cost",new{MainWorlds=all.Count,ThrottleWorlds=probes.Count*3,StraightWorlds=straight.Count,
            MainCaptureMilliseconds=captureMs,MainAnalysisMilliseconds=analysisMs,MainSolverCalls=solver,MainForceEvaluations=calls,
            TotalWithExportMilliseconds=Stopwatch.GetElapsedTime(costWatch).TotalMilliseconds,OptimizerCalls=0,AdditionalBox2DSolvers=0});
    }
    private static void Benchmark(string directory)
    {
        var frame=Frame();var surface=TrackState.CreateDefault(frame.Track).Snapshot();var spec=SlipDynamicsAudit.Rider(reference:65);
        using var initial=new WorldRunner(new[]{spec});var state=initial.Snapshot.Riders[0] with{Heading=initial.Snapshot.Riders[0].Heading+.12f};
        var m=initial.MechanicalProperties(1);var forceRows=new List<object>();var worldRows=new List<object>();double sink=0;
        foreach(var wheel in new[]{false,true}){
            var p=Parameters(wheel);var wheelState=wheel?new RearWheelState(state.ForwardSpeed*1.2):null;
            for(var i=0;i<10000;i++)sink+=RiderController.Forces(state,spec,frame,surface,p,1,m.MassKg,m.InertiaKgMetersSquared,rearWheel:wheelState,stepSeconds:wheel?.01f:0).Torque;
            var elapsed=new double[5];var allocated=new long[5];
            for(var batch=0;batch<5;batch++){
                var bytes=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.GetTimestamp();
                for(var i=0;i<100000;i++)sink+=RiderController.Forces(state,spec,frame,surface,p,1,m.MassKg,m.InertiaKgMetersSquared,rearWheel:wheelState,stepSeconds:wheel?.01f:0).Torque;
                elapsed[batch]=Stopwatch.GetElapsedTime(watch).TotalMilliseconds;allocated[batch]=GC.GetAllocatedBytesForCurrentThread()-bytes;
            }
            forceRows.Add(new{Wheel=wheel,CallsPerBatch=100000,Milliseconds=elapsed,AllocatedBytes=allocated});
            foreach(var pack in new[]{false,true})for(var b=-1;b<5;b++){
                var scenario=pack?SlipControlValidation.Pack():new Scenario("solo",new[]{PhysicsDrivenRacingLineAudit.Rider(PhysicsDrivenRacingLineAudit.Conditions()[0])},90,3);
                var plan=pack?null:PhysicsDrivenRacingLine.Compile(PhysicsDrivenRacingLine.Plans().Single(x=>x.Name=="inner"),frame);
                var bytes=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.GetTimestamp();
                using var w=new WorldRunner(scenario.Riders,parameters:p,spatialControls:plan);
                var r=w.Run(scenario.Name,.01,scenario.DurationSeconds,scenario.Laps,lapsFromInitialPosition:true);
                var ms=Stopwatch.GetElapsedTime(watch).TotalMilliseconds;var memory=GC.GetAllocatedBytesForCurrentThread()-bytes;
                if(!r.Completed)throw new InvalidOperationException("Benchmark did not complete.");
                if(b>=0)worldRows.Add(new{Wheel=wheel,Pack=pack,Milliseconds=ms,AllocatedBytes=memory,r.Fingerprint,r.Work.SolverCalls,
                    r.Work.RearWheelForceEvaluations,r.Work.RearWheelBoundaryEvaluations,r.Work.RearWheelCommits});
            }
        }
        Save(directory,"performance",new{Forces=forceRows,Worlds=worldRows,Sink=sink,Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            OS=System.Runtime.InteropServices.RuntimeInformation.OSDescription,TieredCompilation=Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
            Baseline="#99-compatible historical ON in same binary; exact frozen fingerprints checked separately."});
    }
    private static void Stability(string directory)
    {
        var frame=Frame();var rows=new List<object>();
        foreach(var step in new[]{.005,.01,.02})foreach(var item in new (string Name,float Speed,float Side,float Drive,float Grip)[]{
            (Name:"idle",Speed:0f,Side:0f,Drive:0f,Grip:1f),("start",0f,0f,1f,1f),
            ("crawl",.01f,.1f,0f,1f),("fast",35f,0f,1f,1f),("coast",22f,0f,0f,1f),("low-grip",0f,0f,1f,.35f)}){
            var spec=SlipDynamicsAudit.Rider(speed:item.Speed) with{InitialOutwardSpeed=item.Side};
            var c=Execute(new(item.Name,new[]{spec},3),true,new[]{new ControlCommand(1,0,5,item.Drive)},
                surface:TrackState.CreateDefault(frame.Track,new(item.Grip,0,.5f)).Snapshot(),step:step);
            var geometry=new RacingLineGeometry(frame);
            rows.Add(new{item.Name,StepSeconds=step,c.Body.Result.Fingerprint,c.Body.Result.Work.SolverCalls,
                MaximumSlipRatio=c.Wheels.Max(w=>Math.Abs(w.Step.InstantSlipRatio)),
                MinimumLongitudinalLoss=c.Wheels.Min(w=>w.Step.LongitudinalLossJoules),
                MaximumOmega=c.Wheels.Max(w=>Math.Abs(w.Step.AfterOmega)),MaximumSpeed=c.Body.Ticks.Max(t=>t.After.Speed),
                MinimumClearance=c.Body.Ticks.Min(t=>Math.Min(geometry.Clearance(t.After).Inner,geometry.Clearance(t.After).Outer)),
                Energy=Energy(c)});
        }
        var crash=Execute(new("crash",new[]{SlipDynamicsAudit.Rider(5,0,35,1),SlipDynamicsAudit.Rider(5,3,8,2)},2),true);
        var continuity=crash.Wheels.GroupBy(w=>w.RiderId).All(g=>g.Skip(1).Select(w=>w.Step.BeforeOmega).SequenceEqual(g.SkipLast(1).Select(w=>w.Step.AfterOmega)));
        Save(directory,"stability",new{Cases=rows,Crash=new{crash.Body.Result.Fingerprint,crash.Body.Result.Contacts,
            FinalBodies=crash.Body.Result.Riders,WheelContinuity=continuity,CrashedIntervals=crash.Wheels.Count(w=>w.Before.Activity==RiderActivity.Crashed),
            MaximumCrashedDriveTorque=crash.Wheels.Where(w=>w.Before.Activity==RiderActivity.Crashed).Select(w=>w.Step.DriveTorqueNewtonMeters).DefaultIfEmpty().Max(),
            Wheels=crash.Wheels.Where((w,i)=>i%10==0).Select(w=>new{w.TimeSeconds,w.RiderId,w.Before.Activity,w.Before.Speed,w.Step})}});
    }
    private static void Stress(string directory)
    {
        var frame=Frame();var geometry=new RacingLineGeometry(frame);var rows=new List<object>();
        foreach(var wheel in new[]{false,true}){
            var name="corner-stress-"+(wheel?"new":"old");
            var c=Execute(new(name,new[]{SlipDynamicsAudit.Rider(5,65,25)},6),wheel,
                new[]{new ControlCommand(1,0,5,1),new(1,1,5,.4f)},
                surface:TrackState.CreateDefault(frame.Track,new(.35f,0,.5f)).Snapshot(),csv:Path.Combine(directory,name+"-accepted.csv"));
            var inner=c.Body.Ticks.Min(t=>geometry.Clearance(t.After).Inner);var outer=c.Body.Ticks.Min(t=>geometry.Clearance(t.After).Outer);
            var summary=new{Model=wheel?"wheel":"historical",Grip=.35,InitialReference=65,InitialSpeed=25,ThrottleCutSeconds=1,
                MinimumInnerClearance=inner,MinimumOuterClearance=outer,Feasible=Math.Min(inner,outer)>=.05,
                Rejections=Math.Min(inner,outer)<.05?new[]{"CapsuleClearance"}:Array.Empty<string>(),
                MaximumSlipRatio=c.Wheels.Select(w=>Math.Abs(w.Step.InstantSlipRatio)).DefaultIfEmpty().Max(),
                MaximumSlideDegrees=c.Body.Ticks.Max(t=>Math.Abs(t.Slip.ActualSlipDegrees)),Energy=Energy(c),Exits=Exits(c)};
            rows.Add(summary);Trace(Path.Combine(directory,name+"-trace.csv"),c,false);Replay(directory,name,c,summary);
        }
        Save(directory,"corner-stress",rows);
    }
    private static void LaunchCorner(string directory)
    {
        // Continue the same authored low-grip launch experiment far enough to
        // enter the first corner; do not initialize a fictitious spun-up wheel.
        var frame=Frame();var name="launch-through-corner";
        var c=Execute(new(name,new[]{SlipDynamicsAudit.Rider(speed:0)},7),true,
            new[]{new ControlCommand(1,0,5,1)},surface:TrackState.CreateDefault(frame.Track,new(.35f,0,.5f)).Snapshot(),
            csv:Path.Combine(directory,name+"-accepted.csv"));
        var geometry=new RacingLineGeometry(frame);var corner=c.Wheels.Where(w=>w.Before.Road.IsTurn).ToArray();
        var gap=c.Body.Ticks.Min(t=>Math.Min(geometry.Clearance(t.After).Inner,geometry.Clearance(t.After).Outer));
        var summary=new{Grip=.35,Feasible=gap>=.05,MinimumCapsuleClearance=gap,Rejections=gap<.05?new[]{"CapsuleClearance"}:Array.Empty<string>(),
            MaximumCornerSlipRatio=corner.Max(w=>w.Step.InstantSlipRatio),Energy=Energy(c),Exits=Exits(c)};
        Trace(Path.Combine(directory,name+"-trace.csv"),c,false);Replay(directory,name,c,summary);
    }
    public static void Run(string directory,string stage)
    {
        if(stage=="freeze"){Freeze(directory);return;}
        if(stage=="measure"){Measure(directory);return;}
        if(stage=="benchmark"){Benchmark(directory);return;}
        if(stage=="stability"){Stability(directory);return;}
        if(stage=="stress"){Stress(directory);return;}
        if(stage=="launch-corner"){LaunchCorner(directory);return;}
        if(stage!="pilot")throw new ArgumentException("Use freeze, pilot, measure, stability, stress or benchmark.");
        var observations=new List<RearWheelObservation>();
        var (e,c)=PhysicsDrivenRacingLineAudit.Evaluate(PhysicsDrivenRacingLineAudit.Conditions()[0],PhysicsDrivenRacingLine.Plans()[0],
            parameters:Parameters(true),wheelObserver:observations.Add);
        var captured=new Capture(c,observations.ToArray());
        Save(directory,"pilot",new{Evaluation=e,Energy=Energy(captured),Exits=Exits(captured),
            MaximumSlipRatio=observations.Max(w=>Math.Abs(w.Step.InstantSlipRatio))});
        Console.WriteLine(JsonSerializer.Serialize(new{e.Feasible,e.FlyingSeconds,e.MinimumInnerClearanceMeters,e.MinimumOuterClearanceMeters,Energy=Energy(captured)},Json));
    }
}
