using System.Globalization;
using System.Numerics;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>Opt-in initial data only. No accepted body/wheel state is ever corrected.</summary>
public sealed record AuthoredSteadyStart(double RearWheelOmega,float Drive);

/// <summary>#105 constitutive inverse calls the original force/wheel equations; never integrates a second body model.</summary>
public static class InverseFeasibilityAudit
{
    public const string BaseHead="10f22806140189086860ed104009cd3da03a75e5";
    public const double Spacing=.05;
    public static readonly JsonSerializerOptions Json=new(Audit.Json){WriteIndented=false};
    public static AuthoredRacingPath[] MatchedPaths()=>new[]{AuthoredRacingPath.Matched(1),AuthoredRacingPath.Matched(5),AuthoredRacingPath.Matched(8.5),AuthoredRacingPath.Matched(5,15),AuthoredRacingPath.Matched(8.5,15)};
    public sealed record Local(double S,double Speed,double TangentialAcceleration,double Beta,double Omega,double Fx,double Fy,double Budget,double AvailableTorque,double Residual,double Utilization,double TravelYaw);
    public sealed record Evaluation(Vector2 Force,SlipObservation Slip,YawLimitObservation Yaw,RearWheelStep Wheel);
    public static (float Mass,float Inertia) Mechanical()
    {
        var path=new AuthoredRacingPath("inner");using var world=new WorldRunner(new[]{RacingLineRootCauseAudit.Rider(path)},parameters:LocalOffsetAudit.Parameters(),trackShape:RacingLineRootCauseAudit.Variant());
        var m=world.MechanicalProperties(1);return(m.MassKg,m.InertiaKgMetersSquared);
    }
    public static Evaluation Evaluate(AuthoredRacingPath path,double s,double speed,double beta,double omega,double drive=0,double angularVelocity=0,float dt=0,double? diagnosticCurvature=null)
    {
        var q=path.At(s);var mech=MechanicalCache.Value;var p=LocalOffsetAudit.Parameters();
        var v=new RiderView(1,new((float)q.Position.X,(float)q.Position.Y),new((float)(speed*q.Tangent.X),(float)(speed*q.Tangent.Y)),
            (float)(q.Heading+beta),(float)angularVelocity,path.Frame.RoadAt(s,q.OffsetMeters),s,0,RiderActivity.Racing,(float)drive,(float)q.OffsetMeters,0);
        var wheel=new RearWheelState(omega*RearWheelParameters.Default.WheelRadiusMeters);
        SlipObservation slip=default;YawLimitObservation yaw=default;
        var f=SlipCoupledDynamics.Forces(v,RacingLineRootCauseAudit.Rider(path),path.Frame,TrackState.CreateDefault(path.Frame.Track,new(1,0,.5f)).Snapshot(),p,0,
            mech.Mass,mech.Inertia,observeSlip:x=>slip=x,observeYaw:x=>yaw=x,rearWheel:wheel,stepSeconds:dt,diagnosticPath:new(q.Position,q.Heading,diagnosticCurvature??q.Curvature));
        return new(f.Force,slip,yaw,wheel.Prepared);
    }
    private static readonly Lazy<(float Mass,float Inertia)> MechanicalCache=new(Mechanical);
    public static AuthoredSteadyStart SteadyStart(double speed)
    {
        var local=Solve(new("inner"),20,speed);var wp=RearWheelParameters.Default;
        return new(local.Omega,(float)((wp.WheelRadiusMeters*local.Fx+wp.BearingResistanceNewtonMeterSeconds*local.Omega)/local.AvailableTorque));
    }
    public static (SlipControlValidation.Capture Capture,RearWheelObservation[] Wheels) Replay(AuthoredRacingPath path,double initialSpeed,string? csv=null)
    {
        var start=SteadyStart(initialSpeed);var rider=RacingLineRootCauseAudit.Rider(path) with{ReferenceMeters=20,SpeedMetersPerSecond=(float)initialSpeed,
            OffsetMeters=(float)path.Offset(20).D,TargetOffsetMeters=(float)path.Offset(20).D};
        var wheels=new List<RearWheelObservation>();var c=SlipControlValidation.Execute(new(path.Name,new[]{rider},180,3),LocalOffsetAudit.Parameters(),csvPath:csv,
            surface:TrackState.CreateDefault(path.Frame.Track,new(1,0,.5f)).Snapshot(),wheelObserver:wheels.Add,trackShape:RacingLineRootCauseAudit.Variant(),
            authoredPath:path,diagnosticPathGuidance:true,diagnosticPathPreview:true,diagnosticSteadyStart:start);
        return(c,wheels.ToArray());
    }
    public static Local Solve(AuthoredRacingPath path,double s,double speed,double tangentialAcceleration=0)
    {
        // Two force equations, two state unknowns: body slide and accepted wheel omega.
        // All residual evaluations call SlipCoupledDynamics.Forces -> RearWheelDynamics.Evaluate.
        var q=path.At(s);var mass=MechanicalCache.Value.Mass;var normal=new MeterPoint(-q.Tangent.Y,q.Tangent.X);
        var wanted=q.Tangent*(mass*tangentialAcceleration)+normal*(mass*speed*speed*q.Curvature);
        double beta=Math.Clamp(speed*speed*q.Curvature/100,-.6,.6),omega=speed/.33;
        (double X,double Y,Evaluation E) Residual(double b,double o){var e=Evaluate(path,s,speed,b,o);return(e.Force.X-wanted.X,e.Force.Y-wanted.Y,e);}
        var r=Residual(beta,omega);
        for(var i=0;i<35&&Math.Sqrt(r.X*r.X+r.Y*r.Y)>.001;i++){
            const double db=.0002, dw=.02;var b=Residual(beta+db,omega);var w=Residual(beta,omega+dw);
            var a=(b.X-r.X)/db;var c=(b.Y-r.Y)/db;var d=(w.X-r.X)/dw;var e=(w.Y-r.Y)/dw;var determinant=a*e-c*d;
            if(Math.Abs(determinant)<1e-9)break;
            var deltaBeta=Math.Clamp((-r.X*e+r.Y*d)/determinant,-.15,.15);var deltaOmega=Math.Clamp((-a*r.Y+c*r.X)/determinant,-30,30);
            var old=r.X*r.X+r.Y*r.Y;var scale=1d;
            for(var j=0;j<12;j++){var bn=Math.Clamp(beta+deltaBeta*scale,-1.5,1.5);var on=omega+deltaOmega*scale;var next=Residual(bn,on);
                if(next.X*next.X+next.Y*next.Y<old){beta=bn;omega=on;r=next;break;}scale*=.5;}
        }
        var bodySide=new MeterPoint(-Math.Sin(q.Heading+beta),Math.Cos(q.Heading+beta));
        var lateral=bodySide*r.E.Slip.LateralForceNewtons;var velocity=q.Tangent*speed;
        var travelYaw=(velocity.X*lateral.Y-velocity.Y*lateral.X)/(mass*(speed*speed+.25));
        return new(s,speed,tangentialAcceleration,beta,omega,r.E.Slip.DriveForceNewtons,r.E.Slip.LateralForceNewtons,r.E.Slip.GripBudgetNewtons,
            r.E.Wheel.AvailableTorqueNewtonMeters,Math.Sqrt(r.X*r.X+r.Y*r.Y),r.E.Slip.GripUtilization,travelYaw);
    }
    public static void Save(string directory,string name,object value)
    {
        var file=Path.Combine(directory,name);if(File.Exists(file))throw new IOException("Evidence exists: "+file);
        File.WriteAllText(file,JsonSerializer.Serialize(value,Json)+"\n");
    }
    public static void Row(StreamWriter w,params double[] values)=>w.WriteLine(string.Join(',',values.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))));
    public static StreamWriter Csv(string dir,string name,string header){var f=Path.Combine(dir,name);if(File.Exists(f))throw new IOException("Evidence exists: "+f);var w=new StreamWriter(f);w.WriteLine(header);return w;}
    public static void Inverse(string dir,AuthoredRacingPath path,double speed)
    {
        var n=(int)Math.Ceiling(path.Frame.LapMeters/Spacing);var ds=path.Frame.LapMeters/n;var mech=MechanicalCache.Value;var p=RearWheelParameters.Default;
        var states=Enumerable.Range(-2,n+5).Select(i=>Solve(path,i*ds,speed)).ToArray();
        using var w=Csv(dir,path.Name+$"-inverse-{speed}.csv","s,x,y,speed,k,ax,ay,fx,fy,budget,utilization,beta,omega,wheel_slip,required_drive_torque,available_drive_torque,drive,yaw_rate,yaw_acceleration,required_yaw_torque,available_yaw_torque,controller_requested_torque,controller_authority_torque,controller_energy_torque,energy_credit,force_residual,required_fx,required_fy,finite_dt_fx,finite_dt_fy,finite_dt_wheel_rate,wheel_required_rate,target_slip,required_target_slip,max_target_slip");
        for(var i=0;i<=n;i++){
            var a=states[i+2];var prev=states[i+1];var next=states[i+3];var q=path.At(a.S);var rate=speed/q.Metric;
            var omegaDot=(next.Omega-prev.Omega)/(2*ds)*rate;
            var requiredDrive=p.WheelInertiaKgMetersSquared*omegaDot+p.WheelRadiusMeters*a.Fx+p.BearingResistanceNewtonMeterSeconds*a.Omega;
            var drive=requiredDrive/a.AvailableTorque;
            double Yaw(int j){var state=states[j];var qq=path.At(state.S);return speed*qq.Curvature+(states[j+1].Beta-states[j-1].Beta)/(2*ds)*speed/qq.Metric;}
            var yawRate=Yaw(i+2);var yawAcc=(Yaw(i+3)-Yaw(i+1))/(2*ds)*rate;var torque=mech.Inertia*yawAcc;
            var e=Evaluate(path,a.S,speed,a.Beta,a.Omega,Math.Clamp(drive,0,1),yawRate,.01f);
            var authority=.75+.25*(RacingLineRootCauseAudit.Rider(path).Profile.Gameplay.Abilities.Technique-1)/98d;
            var bodySide=new MeterPoint(-Math.Sin(q.Heading+a.Beta),Math.Cos(q.Heading+a.Beta));var lateral=bodySide*e.Slip.LateralForceNewtons;
            var velocity=q.Tangent*speed;var finiteTravelYaw=(velocity.X*lateral.Y-velocity.Y*lateral.X)/(mech.Mass*(speed*speed+.25));
            var requiredSlip=a.Beta+LocalOffsetAudit.Parameters().SlipYawResponseSeconds*LocalOffsetAudit.Parameters().SlipYawResponseSeconds*
                (yawAcc/authority-2*(finiteTravelYaw-yawRate)/LocalOffsetAudit.Parameters().SlipYawResponseSeconds);
            // Query the actual clamp/velocity scale; do not copy a second tyre/slip formula.
            var maxSlip=Evaluate(path,a.S,speed,a.Beta,a.Omega,Math.Clamp(drive,0,1),yawRate,.01f,diagnosticCurvature:1e4).Slip.TargetSlipDegrees*Math.PI/180;
            var normal=new MeterPoint(-q.Tangent.Y,q.Tangent.X);var acc=normal*(speed*speed*q.Curvature);
            var resistance=CoreSim.LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons((float)speed)*speed/Math.Sqrt(speed*speed+.25);
            var tyre=q.Tangent*resistance+acc*mech.Mass;var forward=new MeterPoint(Math.Cos(q.Heading+a.Beta),Math.Sin(q.Heading+a.Beta));var side=new MeterPoint(-forward.Y,forward.X);
            Row(w,a.S,q.Position.X,q.Position.Y,speed,q.Curvature,acc.X,acc.Y,a.Fx,a.Fy,a.Budget,a.Utilization,a.Beta,a.Omega,
                RearWheelDynamics.SlipRatio(speed*Math.Cos(a.Beta),a.Omega,p),requiredDrive,a.AvailableTorque,drive,yawRate,yawAcc,torque,a.Budget*.5*authority,
                e.Yaw.RequestedNewtonMeters,e.Yaw.AfterAuthorityNewtonMeters,e.Yaw.AfterEnergyNewtonMeters,e.Yaw.DissipationCreditWatts,a.Residual,
                MeterPoint.Dot(tyre,forward),MeterPoint.Dot(tyre,side),e.Slip.DriveForceNewtons,e.Slip.LateralForceNewtons,(e.Wheel.AfterOmega-e.Wheel.BeforeOmega)/.01,omegaDot,
                e.Slip.TargetSlipDegrees*Math.PI/180,requiredSlip,maxSlip);
        }
    }
    public static object KnotProbe(AuthoredRacingPath path,double speed,double knot,double h)
    {
        var b=Solve(path,knot,speed);var left=Solve(path,knot-h,speed);var left2=Solve(path,knot-2*h,speed);
        var right=Solve(path,knot+h,speed);var right2=Solve(path,knot+2*h,speed);var q=path.At(knot);var m=MechanicalCache.Value;
        var derivativeLeft=(3*b.Beta-4*left.Beta+left2.Beta)/(2*h);var derivativeRight=(-3*b.Beta+4*right.Beta-right2.Beta)/(2*h);
        var yawLeft=speed*q.Curvature+speed/q.Metric*derivativeLeft;var yawRight=speed*q.Curvature+speed/q.Metric*derivativeRight;
        return new{Path=path.Name,Speed=speed,Knot=knot,H=h,YawLeft=yawLeft,YawRight=yawRight,DeltaYaw=yawRight-yawLeft,
            RequiredAngularImpulse=m.Inertia*(yawRight-yawLeft),AvailableTorque=b.Budget*.5*.875,
            MaximumForceResidual=new[]{b,left,left2,right,right2}.Max(x=>x.Residual)};
    }
    private static object[] KnotProbes(AuthoredRacingPath path,double speed)
    {
        var start=path.Frame.SegmentStartMeters(1);var exit=path.Frame.SegmentStartMeters(4);var apex=(start+exit)/2;
        var knots=path.MatchedEntryOffset is null?new[]{start,apex,exit}:new[]{start-path.TighteningBeforeEntryMeters,apex,exit+15};
        return knots.SelectMany(k=>new[]{.2,.1,.05,.02,.01,.005}.Select(h=>KnotProbe(path,speed,k,h))).ToArray();
    }
    public static double SelectLowerSpeed(string dir)
    {
        // Fixed, descending necessary-screen only; not time/line/controller optimization.
        // The returned profile starts at this speed. There is no braking trajectory.
        var candidates=new List<object>();double selected=0;
        foreach(var speed in Enumerable.Range(1,17).Reverse()){
            double maxDrive=double.NegativeInfinity,minDrive=double.PositiveInfinity,maxResidual=0,maxGrip=0;
            foreach(var name in new[]{"inner","outside-inside-outside"}){
                var path=new AuthoredRacingPath(name);var n=640;var ds=path.Frame.LapMeters/n;
                for(var i=0;i<n;i++){var s=(i+.371)*ds;var state=Solve(path,s,speed);var before=Solve(path,s-.02,speed);var after=Solve(path,s+.02,speed);
                    var omegaDot=(after.Omega-before.Omega)/.04*speed/path.At(s).Metric;var wp=RearWheelParameters.Default;
                    var drive=(wp.WheelInertiaKgMetersSquared*omegaDot+wp.WheelRadiusMeters*state.Fx+wp.BearingResistanceNewtonMeterSeconds*state.Omega)/state.AvailableTorque;
                    maxDrive=Math.Max(maxDrive,drive);minDrive=Math.Min(minDrive,drive);maxResidual=Math.Max(maxResidual,state.Residual);
                    maxGrip=Math.Max(maxGrip,speed*speed*Math.Abs(path.At(s).Curvature)/RacingLineRootCauseAudit.AccelerationBudget());
                }
            }
            var pass=maxDrive<=.8&&minDrive>=0&&maxGrip<=1&&maxResidual<.01;
            candidates.Add(new{Speed=speed,MaximumDrive=maxDrive,MinimumDrive=minDrive,MaximumForceResidual=maxResidual,MaximumScalarGripFraction=maxGrip,Pass=pass});
            if(pass){selected=speed;break;}
        }
        Save(dir,"lower-speed-selection.json",new{Rule="descending integer speed, original wheel/drive torque <=80% of envelope and nonnegative, scalar grip <=1 on both paths; no braking or new actuator",Candidates=candidates,Selected=selected});
        if(selected==0)throw new InvalidOperationException("No bounded lower profile passed the necessary drive screen.");return selected;
    }
    public static void Run(string dir,string stage)
    {
        Directory.CreateDirectory(dir);
        if(stage=="freeze"){
            Save(dir,"freeze.json",new{BaseHead,Variant=RacingLineRootCauseAudit.Variant(),Parameters=LocalOffsetAudit.Parameters(),Spacing,
                InitialSpeed=18,InitialReference=20,MatchedOffsets=new[]{1d,5,8.5},TighteningBeforeEntry=new[]{0,15},ApexFraction=.5,ExitOffset=1,
                EndWindowAfterExit=15,ResetRule="Hold offset1 until exit+15, quintic reset over32m to entry offset by next entry-15, then hold until tightening. Repeat half-lap. Inner identical in both variants.",
                InverseRule="Original Forces(dt=0) force balance; derivatives in reference distance, original wheel torque/engine envelope and yaw guard(dt=.01). Local solutions are NOT constructive proof.",
                LowerRule="One common constant speed: largest integer <=18 satisfying sampled engine torque and scalar grip on BOTH #104 paths, safety factor .8 on drive torque. No ideal braking. Constant-speed lower probe starts at that speed.",
                TrackingRule="Existing #104 path-preview, gains unchanged. All replay times diagnostic unless inverse AND tracking pass."});return;
        }
        using var frozen=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"freeze.json")));
        if(frozen.RootElement.GetProperty("BaseHead").GetString()!=BaseHead
            ||frozen.RootElement.GetProperty("Variant").GetRawText()!=JsonSerializer.Serialize(RacingLineRootCauseAudit.Variant(),Json)
            ||frozen.RootElement.GetProperty("Parameters").GetRawText()!=JsonSerializer.Serialize(LocalOffsetAudit.Parameters(),Json)
            ||frozen.RootElement.GetProperty("Spacing").GetDouble()!=Spacing
            ||frozen.RootElement.GetProperty("MatchedOffsets").GetRawText()!=JsonSerializer.Serialize(new[]{1d,5,8.5},Json)
            ||frozen.RootElement.GetProperty("TighteningBeforeEntry").GetRawText()!=JsonSerializer.Serialize(new[]{0,15},Json))throw new InvalidOperationException("Wrong freeze.");
        if(stage=="inverse"){
            foreach(var name in new[]{"inner","outside-inside-outside"}){Inverse(dir,new(name),18);Console.WriteLine("Inverse "+name);}
            return;
        }
        if(stage=="inverse-bounds"){
            foreach(var name in new[]{"inner","outside-inside-outside"})foreach(var speed in new[]{18d,15})Inverse(dir,new(name),speed);
            foreach(var path in MatchedPaths())Inverse(dir,path,18);return;
        }
        if(stage=="lower"){
            var lower=SelectLowerSpeed(dir);var knots=new List<object>();
            foreach(var name in new[]{"inner","outside-inside-outside"}){var path=new AuthoredRacingPath(name);Inverse(dir,path,lower);knots.AddRange(KnotProbes(path,18));knots.AddRange(KnotProbes(path,lower));}
            Save(dir,"knot-probes.json",knots);return;
        }
        if(stage=="geometry"){
            var paths=MatchedPaths();Save(dir,"geometry-summary.json",paths.Select(path=>RacingLineRootCauseAudit.ExportGeometry(dir,path)).ToArray());
            foreach(var path in paths){Inverse(dir,path,18);Console.WriteLine("Geometry/inverse "+path.Name);}
            Save(dir,"matched-knot-probes.json",paths.SelectMany(path=>KnotProbes(path,18)).ToArray());return;
        }
        if(stage=="replay-freeze"){
            var files=new[]{"freeze.json","lower-speed-selection.json","knot-probes.json","geometry-summary.json","matched-knot-probes.json"};
            Save(dir,"replay-freeze.json",new{Rule="Analysis precedes Box2D. Five matched paths start reference20 at18m/s in equivalent straight force/wheel equilibrium. Constant legal throttle equals the original straight maintenance throttle at18; lower #104 pair uses15. No speed clamp, brakes, state correction or extra force. All times diagnostic until inverse AND tracking pass.",
                Initial18=SteadyStart(18),Initial15=SteadyStart(15),GeometryAndInverseSha256=files.ToDictionary(f=>f,f=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(dir,f)))))});return;
        }
        if(stage=="replay"){
            using var gate=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"replay-freeze.json")));
            foreach(var kv in gate.RootElement.GetProperty("GeometryAndInverseSha256").EnumerateObject())
                if(kv.Value.GetString()!=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(dir,kv.Name)))))throw new InvalidOperationException("Analysis changed after replay freeze.");
            var cases=MatchedPaths().Select(p=>(Path:p,Speed:18d)).Concat(new[]{(new AuthoredRacingPath("inner"),15d),(new AuthoredRacingPath("outside-inside-outside"),15d)});
            var results=new List<object>();foreach(var (path,speed) in cases){var prefix=path.Name+$"-steady-{speed}";
                foreach(var suffix in new[]{"-accepted.csv","-tracking.csv",".json"})if(File.Exists(Path.Combine(dir,prefix+suffix)))throw new IOException("Evidence exists: "+prefix+suffix);
                var(c,w)=Replay(path,speed,Path.Combine(dir,prefix+"-accepted.csv"));results.Add(RacingLineRootCauseAudit.ExportCapture(dir,prefix,c,w,path,diagnosticPathFrame:true));Console.WriteLine("Captured "+prefix);}
            Save(dir,"runs.json",results);return;
        }
        if(stage=="verify"){
            var checks=new List<object>();
            using var original=JsonDocument.Parse(File.ReadAllText("docs/experiments/progressive-bend-width/measurements.json"));
            foreach(var plan in LocalOffsetAudit.Plans()){
                var expected=original.RootElement.EnumerateArray().Single(r=>r.GetProperty("Measurement").GetProperty("Geometry").GetString()=="progressive-b"&&r.GetProperty("Measurement").GetProperty("Plan").GetProperty("Name").GetString()==plan.Name).GetProperty("Measurement").GetProperty("Evaluation").GetProperty("Fingerprint").GetString();
                var actual=LocalOffsetAudit.Plain(RacingLineRootCauseAudit.Variant(),plan).Fingerprint;if(actual!=expected)throw new InvalidOperationException("#103 changed.");checks.Add(new{Case=plan.Name,Expected=expected,Actual=actual});
            }
            using var old=JsonDocument.Parse(File.ReadAllText("docs/experiments/racing-line-root-cause/outside-inside-outside-path-preview.json"));
            var repeat=RacingLineRootCauseAudit.Execute("outside-inside-outside",true,preview:true).Capture.Result.Fingerprint;
            var saved=old.RootElement.GetProperty("Result").GetProperty("Fingerprint").GetString();if(repeat!=saved)throw new InvalidOperationException("#104 changed.");checks.Add(new{Case="#104 OIO preview",Expected=saved,Actual=repeat});
            var matched=AuthoredRacingPath.Matched(8.5,15);var spec=RacingLineRootCauseAudit.Rider(matched) with{ReferenceMeters=20,OffsetMeters=8.5f,TargetOffsetMeters=8.5f};
            using var world=new WorldRunner(new[]{spec},parameters:LocalOffsetAudit.Parameters(),trackShape:RacingLineRootCauseAudit.Variant(),authoredPath:matched,diagnosticPathGuidance:true,diagnosticPathPreview:true,
                diagnosticSteadyStart:SteadyStart(18),frozenSurface:TrackState.CreateDefault(matched.Frame.Track,new(1,0,.5f)).Snapshot());
            var actualNew=world.Run("plain",.01,180,3,lapsFromInitialPosition:true).Fingerprint;
            using var newer=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,matched.Name+"-steady-18.json")));var expectedNew=newer.RootElement.GetProperty("Result").GetProperty("Fingerprint").GetString();
            if(actualNew!=expectedNew)throw new InvalidOperationException("Observer parity failed.");checks.Add(new{Case="#105 wide-early observed/plain",Expected=expectedNew,Actual=actualNew});Save(dir,"verification.json",checks);return;
        }
        if(stage=="entry-proof"){
            var path=new AuthoredRacingPath("outside-inside-outside");var s=path.Frame.SegmentStartMeters(1);var length=(path.Frame.SegmentStartMeters(4)-s)/2;
            var values=new List<object>();foreach(var speed in new[]{18d,15}){
                var state=Solve(path,s,speed);const double db=.0001;var lo=Evaluate(path,s,speed,-db,state.Omega);var hi=Evaluate(path,s,speed,db,state.Omega);
                var derivative=(hi.Force.Y-lo.Force.Y)/(2*db);var curvaturePrimeJump=60*(AuthoredRacingPath.OutsideMeters-AuthoredRacingPath.ApexMeters)/Math.Pow(length,3);
                var yawJump=speed*MechanicalCache.Value.Mass*speed*speed*curvaturePrimeJump/derivative;
                values.Add(new{Speed=speed,Reference=s,CurvaturePrimeJump=curvaturePrimeJump,ForceNormalBetaDerivative=derivative,YawRateJump=yawJump,
                    Inertia=MechanicalCache.Value.Inertia,RequiredAngularImpulse=MechanicalCache.Value.Inertia*yawJump,MaximumTorque=state.Budget*.5*.875,
                    MinimumTransitionSeconds=MechanicalCache.Value.Inertia*yawJump/(state.Budget*.5*.875),
                    Note="Analytic quintic third derivative plus Jacobian of the ORIGINAL force law at the straight equilibrium. Nonzero jump cannot be supplied instantaneously by bounded torque. Does not quantify metre-scale tracking error."});
            }
            Save(dir,"entry-proof.json",values);return;
        }
        if(stage=="preview-telemetry"){
            using var runs=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"runs.json")));var checks=new List<object>();
            foreach(var run in runs.RootElement.EnumerateArray()){
                var prefix=run.GetProperty("Case").GetString()!;var name=run.GetProperty("AuthoredPath").GetString()!;
                var path=MatchedPaths().SingleOrDefault(p=>p.Name==name)??new AuthoredRacingPath(name);
                var drive=run.GetProperty("Initial").GetProperty("Riders")[0].GetProperty("Drive").GetSingle();
                var lines=File.ReadLines(Path.Combine(dir,prefix+"-tracking.csv"));var header=lines.First().Split(',');var p=LocalOffsetAudit.Parameters();double maximumDifference=0;
                using var w=Csv(dir,prefix+"-preview.csv","t,s,feed_forward_curvature,feed_forward_acceleration,position_feedback_acceleration,velocity_feedback_acceleration,total_acceleration,recorded_acceleration,actual_normal_acceleration");
                foreach(var line in lines.Skip(1)){
                    var cells=line.Split(',');double V(string key)=>double.Parse(cells[Array.IndexOf(header,key)],CultureInfo.InvariantCulture);
                    var s=V("s");var v=new RiderView(1,new((float)V("x"),(float)V("y")),new((float)V("vx"),(float)V("vy")),(float)V("body_heading"),(float)V("angular_velocity"),
                        path.Frame.RoadAt(s,V("offset")),s,0,RiderActivity.Racing,drive,(float)V("target"),0);
                    var guidance=path.Guidance(v,p,true);var delta=new MeterPoint(v.Position.X,v.Position.Y)-guidance.Position;var velocity=new MeterPoint(v.Velocity.X,v.Velocity.Y);
                    var along=MeterPoint.Dot(velocity,guidance.Tangent);var response=p.LateralResponseSeconds+4*p.SlipYawResponseSeconds;
                    var feedForward=along*along*guidance.FeedForwardCurvature;var position=MeterPoint.Dot(delta,guidance.Outward)/(response*response);
                    var damping=2*MeterPoint.Dot(velocity,guidance.Outward)/response;var total=(float)(feedForward+position+damping);
                    maximumDifference=Math.Max(maximumDifference,Math.Abs(total-V("steering_demand")));
                    Row(w,V("t"),s,guidance.FeedForwardCurvature,feedForward,position,damping,total,V("steering_demand"),V("speed")*V("speed")*V("actual_curvature"));
                }
                if(maximumDifference>.0001)throw new InvalidOperationException("Preview reconstruction disagrees with recorded original demand.");
                checks.Add(new{Case=prefix,MaximumAccelerationDifference=maximumDifference});
            }
            Save(dir,"preview-telemetry-verification.json",checks);return;
        }
        throw new ArgumentException("Use freeze, inverse, lower, geometry, replay-freeze, replay or verify.");
    }
}
