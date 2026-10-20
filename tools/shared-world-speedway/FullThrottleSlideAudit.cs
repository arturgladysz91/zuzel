using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>Read-only #106 replay audit and bounded constitutive probes. Never modifies a tyre, force or accepted state.</summary>
public static class FullThrottleSlideAudit
{
    public const string BaseHead="5648c4263739e46df40a366c96fabe4978cc1b3c";
    public const string SourceDirectory="docs/experiments/speed-grip-line-crossover";
    public static readonly string[] Cases={"baseline-inner","baseline-wide","grip85-inner","grip85-wide","grip70-inner","grip70-wide","grip55-inner","grip55-wide","power3-inner"};
    private static readonly JsonSerializerOptions Json=InverseFeasibilityAudit.Json;
    private static readonly Lazy<(float Mass,float Inertia)> Mechanical=new(InverseFeasibilityAudit.Mechanical);
    public sealed class Sampler
    {
        public AuthoredRacingPath Path {get;}
        private readonly RiderSpec rider;
        private readonly TrackStateSnapshot surface;
        private readonly WorldParameters parameters;
        public Sampler(TrackSurfaceState state,bool wide=false,ExperimentalDriveEnvelope? engine=null)
        {
            Path=AuthoredRacingPath.Crossover(wide);rider=RacingLineRootCauseAudit.Rider(Path);
            surface=TrackState.CreateDefault(Path.Frame.Track,state).Snapshot();parameters=SpeedGripCrossoverAudit.Parameters(engine);
        }
        public Query Evaluate(RiderView v,double omega,float seconds=0,double? curvature=null)
        {
            var wheel=new RearWheelState(omega*RearWheelParameters.Default.WheelRadiusMeters);
            SlipObservation slip=default;YawLimitObservation yaw=default;
            var q=Path.At(v.TotalReferenceMeters);var mech=Mechanical.Value;
            var force=SlipCoupledDynamics.Forces(v,rider,Path.Frame,surface,parameters,0,mech.Mass,mech.Inertia,
                observeSlip:x=>slip=x,observeYaw:x=>yaw=x,rearWheel:wheel,stepSeconds:seconds,
                diagnosticPath:curvature is { } k?new PathGuidanceSample(q.Position,q.Heading,k):Path.Guidance(v,parameters,true));
            return new(v,force.Force,force.Torque,slip,wheel.Prepared,yaw);
        }
        public Query At(double speed,double betaDegrees,double kappa,double reference=70)
        {
            var q=Path.At(reference);var v=new RiderView(1,new((float)q.Position.X,(float)q.Position.Y),new((float)(speed*q.Tangent.X),(float)(speed*q.Tangent.Y)),
                (float)(q.Heading+betaDegrees*Math.PI/180),0,Path.Frame.RoadAt(reference,q.OffsetMeters),reference,0,RiderActivity.Racing,1,(float)q.OffsetMeters,0);
            var p=RearWheelParameters.Default;var omega=(v.ForwardSpeed+kappa*Math.Sqrt(v.ForwardSpeed*v.ForwardSpeed+p.LowSpeedMetersPerSecond*p.LowSpeedMetersPerSecond))/p.WheelRadiusMeters;
            return Evaluate(v,omega);
        }
        public Query SteadyWheel(double speed,double betaDegrees)
        {
            var q=At(speed,betaDegrees,0);var low=0d;var high=600d;
            double Residual(Query e)=>e.Wheel.DriveTorqueNewtonMeters-RearWheelParameters.Default.WheelRadiusMeters*e.Wheel.LongitudinalNewtons-e.Wheel.BearingTorqueNewtonMeters;
            var a=Evaluate(q.View,low);var b=Evaluate(q.View,high);
            if(Residual(a)<0||Residual(b)>0)throw new InvalidOperationException("Unbracketed steady wheel.");
            for(var i=0;i<42;i++){var middle=(low+high)/2;var e=Evaluate(q.View,middle);if(Residual(e)>0)low=middle;else high=middle;}
            return Evaluate(q.View,(low+high)/2);
        }
    }
    public sealed record Query(RiderView View,Vector2 Force,float Torque,SlipObservation Slip,RearWheelStep Wheel,YawLimitObservation Yaw)
    {
        public double LateralLossWatts=>-Wheel.LateralNewtons*View.SideSpeed;
        public double LongitudinalLossWatts=>Wheel.LongitudinalNewtons*(RearWheelParameters.Default.WheelRadiusMeters*Wheel.BeforeOmega-View.ForwardSpeed);
        public double TangentialAcceleration=>Vector2.Dot(Force,View.Velocity/View.Speed)/Mechanical.Value.Mass;
        public double NormalAcceleration=>Vector2.Dot(Force,new Vector2(-View.Velocity.Y,View.Velocity.X)/View.Speed)/Mechanical.Value.Mass;
        public double WheelPowerWatts=>(Wheel.DriveTorqueNewtonMeters-RearWheelParameters.Default.WheelRadiusMeters*Wheel.LongitudinalNewtons-Wheel.BearingTorqueNewtonMeters)*Wheel.BeforeOmega;
    }
    private static void Save(string directory,string name,object value)=>InverseFeasibilityAudit.Save(directory,name,value);
    private static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    public static void Run(string directory,string stage)
    {
        Directory.CreateDirectory(directory);
        if(stage=="freeze"){
            Save(directory,"freeze.json",new{BaseHead,SourceDirectory,Cases,
                Sources=Cases.SelectMany(n=>new[]{n+".json",n+"-tracking.csv.gz",n+"-forces.csv.gz"}).Select(n=>new{File=n,Sha256=Hash(System.IO.Path.Combine(SourceDirectory,n))}),
                Parameters=SpeedGripCrossoverAudit.Parameters(null),Variant=RacingLineRootCauseAudit.Variant(),RearWheel=RearWheelParameters.Default,
                AdditionalRuns=new[]{17.1f,18f,18.9f},AdditionalRule="Three INNER/Grip1 baseline full-Drive8-lap worlds: exact #106 repeat and +/-5% initial-speed perturbations. Rolling initial wheel. No in-run intervention.",
                ExtractionRule="Use every saved native step up to and including the first physical footprint crossing (end inner<0 or outer<0). Preserve lack of between-step certification separately. No invalid timing ranking.",
                StaticRule="Original Forces/Evaluate at h=0. Distinct beta/kappa, Drive1. Steady-wheel root is an instantaneous calculation, never evidence of stable body motion.",
                StabilityRule="Report full and middle90% corner slide>=3deg time fractions, final3 periodicity<=0.5%, native/between-step safety, tracking gate and perturbation convergence separately. No empirical slip-angle target."});return;
        }
        using var freeze=JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(directory,"freeze.json")));
        if(freeze.RootElement.GetProperty("BaseHead").GetString()!=BaseHead)throw new InvalidOperationException("Wrong base.");
        foreach(var source in freeze.RootElement.GetProperty("Sources").EnumerateArray())if(Hash(System.IO.Path.Combine(SourceDirectory,source.GetProperty("File").GetString()!))!=source.GetProperty("Sha256").GetString())throw new InvalidOperationException("Source evidence changed.");
        if(stage=="extract") {Save(directory,"extraction.json",Cases.Select(name=>Extract(directory,name)).ToArray());return;}
        if(stage=="probes"){Probes(directory);return;}
        if(stage=="controller-probes"){ControllerProbes(directory);return;}
        if(stage=="perturbations"){
            var runs=new List<object>();
            foreach(var speed in new[]{17.1f,18f,18.9f}){
                var name=speed==18?"repeat18":speed<18?"initial-minus5":"initial-plus5";
                var row=SpeedGripCrossoverAudit.Capture(directory,name,false,initialSpeed:speed);
                runs.Add(row);Console.WriteLine("Additional "+name);
            }
            Save(directory,"perturbations.json",runs);return;
        }
        throw new ArgumentException("Use freeze, extract, probes or perturbations.");
    }
    private sealed class Csv
    {
        public Dictionary<string,int> Columns {get;}
        public List<double[]> Rows {get;}=new();
        public Csv(string path){using var zip=new GZipStream(File.OpenRead(path),CompressionMode.Decompress);using var r=new StreamReader(zip);
            Columns=r.ReadLine()!.Split(',').Select((v,i)=>(v,i)).ToDictionary(x=>x.v,x=>x.i);
            while(r.ReadLine() is { } line)Rows.Add(line.Split(',').Select(v=>double.Parse(v,CultureInfo.InvariantCulture)).ToArray());}
        public double Get(int i,string key)=>Rows[i][Columns[key]];
    }
    private static object Extract(string directory,string name)
    {
        var tracking=new Csv(System.IO.Path.Combine(SourceDirectory,name+"-tracking.csv.gz"));var forces=new Csv(System.IO.Path.Combine(SourceDirectory,name+"-forces.csv.gz"));
        using var doc=JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(SourceDirectory,name+".json")));
        var source=doc.RootElement;var grip=name.StartsWith("grip85",StringComparison.Ordinal)?.85f:name.StartsWith("grip70",StringComparison.Ordinal)?.7f:name.StartsWith("grip55",StringComparison.Ordinal)?.55f:1;
        var engine=name.StartsWith("power3",StringComparison.Ordinal)?SpeedGripCrossoverAudit.Engines().Single(e=>e?.Name=="power3"):null;
        var sampler=new Sampler(new(grip,0,.5f),name.EndsWith("wide",StringComparison.Ordinal),engine);
        if(tracking.Rows.Count!=forces.Rows.Count)throw new InvalidOperationException("Native record lengths differ.");
        var output=System.IO.Path.Combine(directory,name+"-decomposition.csv.gz");if(File.Exists(output))throw new IOException("Evidence exists.");
        using var writer=new StreamWriter(new GZipStream(File.Create(output),CompressionLevel.SmallestSize));
        writer.WriteLine("t,front_fy,rear_fy,forward_speed,side_speed,lateral_loss_work,longitudinal_loss_work,body_delta,wheel_delta,yaw_work,a_tangent,a_measured,force_replay_error");
        double T(int i,string k)=>tracking.Get(i,k);double F(int i,string k)=>forces.Get(i,k);
        double engineW=0,lat=0,lng=0,res=0,bearing=0,yaw=0,body=0,wheelEnergy=0,maxForceError=0,maxOmegaError=0,minLat=0,minLong=0,maxImpulse=0;
        var used=0;object? violation=null;
        for(var i=0;i<tracking.Rows.Count;i++){
            if(T(i,"t")!=F(i,"t")||T(i,"dt")!=F(i,"dt"))throw new InvalidOperationException("Native clocks differ.");
            var position=new Vector2((float)T(i,"x"),(float)T(i,"y"));var velocity=new Vector2((float)T(i,"vx"),(float)T(i,"vy"));
            var v=new RiderView(1,position,velocity,(float)T(i,"body_heading"),(float)T(i,"angular_velocity"),sampler.Path.Frame.Project(position),T(i,"s"),0,RiderActivity.Racing,1,(float)T(i,"target"),0);
            var q=sampler.Evaluate(v,F(i,"omega_before"),(float)T(i,"dt"));var h=(double)(float)T(i,"dt");
            var final=source.GetProperty("Result").GetProperty("Riders")[0];
            double Next(string column,string property,string? component=null)=>i+1<tracking.Rows.Count?T(i+1,column):component is null?final.GetProperty(property).GetDouble():final.GetProperty(property).GetProperty(component).GetDouble();
            var vx=Next("vx","Velocity","X");var vy=Next("vy","Velocity","Y");var spin=Next("angular_velocity","AngularVelocity");
            var forward=new Vector2(MathF.Cos(v.Heading),MathF.Sin(v.Heading));var side=new Vector2(-forward.Y,forward.X);
            var middleX=(v.Velocity.X+vx)/2;var middleY=(v.Velocity.Y+vy)/2;
            var um=middleX*forward.X+middleY*forward.Y;var wm=middleX*side.X+middleY*side.Y;
            var lateral= -T(i,"fy")*wm*h;var longitudinal=T(i,"fx")*(.33*(F(i,"omega_before")+F(i,"omega_after"))/2-um)*h;
            var resistance=(F(i,"force_x")*middleX+F(i,"force_y")*middleY-T(i,"fx")*um-T(i,"fy")*wm)*h;
            var yawWork=T(i,"yaw_energy")*(v.AngularVelocity+spin)/2*h;
            var db=.5*F(i,"mass")*(vx*vx+vy*vy-v.Velocity.X*(double)v.Velocity.X-v.Velocity.Y*(double)v.Velocity.Y)
                +.5*F(i,"inertia")*(spin*spin-v.AngularVelocity*(double)v.AngularVelocity);
            var dw=.5*.9*(F(i,"omega_after")*F(i,"omega_after")-F(i,"omega_before")*F(i,"omega_before"));
            var error=Math.Max(Math.Max(Math.Abs(q.Wheel.LongitudinalNewtons-T(i,"fx")),Math.Abs(q.Wheel.LateralNewtons-T(i,"fy"))),
                Math.Max(Math.Abs(q.Force.X-F(i,"force_x")),Math.Abs(q.Force.Y-F(i,"force_y"))));
            maxForceError=Math.Max(maxForceError,error);maxOmegaError=Math.Max(maxOmegaError,Math.Abs(q.Wheel.AfterOmega-F(i,"omega_after")));
            maxImpulse=Math.Max(maxImpulse,F(i,"body_force_residual")*h);minLat=Math.Min(minLat,lateral);minLong=Math.Min(minLong,longitudinal);
            var at=(F(i,"force_x")*v.Velocity.X+F(i,"force_y")*v.Velocity.Y)/(F(i,"mass")*v.Speed);
            var measured=(Math.Sqrt(vx*vx+vy*vy)-Math.Sqrt(v.Velocity.X*(double)v.Velocity.X+v.Velocity.Y*(double)v.Velocity.Y))/T(i,"dt");
            InverseFeasibilityAudit.Row(writer,T(i,"t"),q.Wheel.FrontLateralNewtons,q.Wheel.RearLateralNewtons,v.ForwardSpeed,v.SideSpeed,lateral,longitudinal,db,dw,yawWork,at,measured,error);
            engineW+=F(i,"engine_work");lat+=lateral;lng+=longitudinal;res+=resistance;bearing+=F(i,"bearing_loss");yaw+=yawWork;body+=db;wheelEnergy+=dw;used++;
            if(T(i,"inner")<0||T(i,"outer")<0){violation=new{Start=T(i,"t"),End=T(i,"t")+T(i,"dt"),Inner=T(i,"inner"),Outer=T(i,"outer"),Reference=T(i,"s"),BeforeSpeed=v.Speed,AfterSpeed=Math.Sqrt(vx*vx+vy*vy)};break;}
        }
        var residual=body+wheelEnergy-engineW+lat+lng+bearing-res-yaw;
        if(maxForceError>.003||maxOmegaError>1e-5||Math.Abs(residual)>5)throw new InvalidOperationException($"Replay decomposition failed {name}: F={maxForceError:R} omega={maxOmegaError:R} energy={residual:R}");
        return new{Name=name,Grip=grip,Engine=engine,SourceFingerprint=source.GetProperty("Result").GetProperty("Fingerprint").GetString(),NativeRows=tracking.Rows.Count,UsedRows=used,FirstPhysicalViolation=violation,
            MaximumForceReplayErrorNewtons=maxForceError,MaximumOmegaReplayError=maxOmegaError,MaximumNativeImpulseResidualNewtonSeconds=maxImpulse,
            MinimumLateralLossWork=minLat,MinimumLongitudinalLossWork=minLong,Energy=new{BodyChange=body,WheelChange=wheelEnergy,EngineWork=engineW,LateralLoss=lat,LongitudinalLoss=lng,
                ResistanceWork=res,BearingLoss=bearing,YawWork=yaw,Residual=residual},BoundarySafe=source.GetProperty("BoundarySafe").GetBoolean()};
    }
    public static object ProbeRow(Query q)=>new{Speed=q.View.Speed,BetaDegrees=q.View.SlideRadians*180/Math.PI,Forward=q.View.ForwardSpeed,Side=q.View.SideSpeed,
        Kappa=q.Wheel.InstantSlipRatio,Omega=q.Wheel.BeforeOmega,q.Wheel.LongitudinalNewtons,FrontFx=0,FrontFy=q.Wheel.FrontLateralNewtons,RearFy=q.Wheel.RearLateralNewtons,
        q.Slip.TargetSlipDegrees,
        q.Slip.GripBudgetNewtons,q.Slip.GripUtilization,q.Wheel.DriveTorqueNewtonMeters,q.Wheel.AvailableTorqueNewtonMeters,EnginePower=q.Wheel.DriveTorqueNewtonMeters*q.Wheel.BeforeOmega,
        BodyPower=Vector2.Dot(q.Force,q.View.Velocity),q.WheelPowerWatts,q.LateralLossWatts,q.LongitudinalLossWatts,q.Slip.ResistanceLossWatts,
        BearingPower=q.Wheel.BearingTorqueNewtonMeters*q.Wheel.BeforeOmega,q.TangentialAcceleration,q.NormalAcceleration};
    private static void Probes(string directory)
    {
        var grid=new List<object>();var equilibrium=new List<object>();
        foreach(var grip in new[]{1f,.85f,.7f,.55f}){
            var sampler=new Sampler(new(grip,0,.5f));
            foreach(var speed in new[]{14d,18,22})foreach(var beta in new[]{0d,2,5,10,15,20,30,40,60})foreach(var kappa in new[]{0d,.05,.1,.2,.5,1,2})
                grid.Add(new{Grip=grip,State=ProbeRow(sampler.At(speed,beta,kappa))});
            foreach(var speed in new[]{14d,18,22})foreach(var beta in new[]{0d,2,5,10,15,20,30,40,60})
                equilibrium.Add(new{Grip=grip,State=ProbeRow(sampler.SteadyWheel(speed,beta))});
        }
        Save(directory,"static-grid.json",grid);Save(directory,"steady-wheel.json",equilibrium);
        var surfaces=new List<object>();foreach(var grip in new[]{1f,.85f})foreach(var ruts in new[]{0f,.25f,.5f})foreach(var moisture in new[]{.2f,.35f,.5f,.8f}){
            var surface=new TrackSurfaceState(grip,ruts,moisture);var query=new Sampler(surface).At(18,15,.2);
            surfaces.Add(new{surface.Grip,surface.Ruts,surface.Moisture,surface.EffectiveGrip,State=ProbeRow(query)});}
        Save(directory,"surface-probes.json",surfaces);
        ControllerProbes(directory);
    }
    private static void ControllerProbes(string directory)
    {
        var demands=new List<object>();
        foreach(var grip in new[]{1f,.85f,.7f,.55f})foreach(var speed in new[]{14d,18,22}){
            var sampler=new Sampler(new(grip,0,.5f));var straight=sampler.SteadyWheel(speed,0);var budget=straight.Slip.GripBudgetNewtons;
            foreach(var fraction in new[]{.25,.5,.75,.85,.9,.95}){
                var required=fraction*budget/Mechanical.Value.Mass;var target=sampler.Evaluate(straight.View,straight.Wheel.BeforeOmega,curvature:required/(speed*speed));
                var actual=sampler.SteadyWheel(speed,target.Slip.TargetSlipDegrees);
                demands.Add(new{Grip=grip,Speed=speed,Fraction=fraction,RequiredNormalAcceleration=required,TargetBetaDegrees=target.Slip.TargetSlipDegrees,
                    ActualNormalAcceleration=actual.NormalAcceleration,ActualToRequested=actual.NormalAcceleration/required,State=ProbeRow(actual)});
            }
        }
        Save(directory,"controller-inversion-probes.json",demands);
    }
}
