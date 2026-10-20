using System.Diagnostics;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;

namespace SharedWorldSpeedway;

/// <summary>Recorded-force arithmetic audit; no contact evaluation or integration.</summary>
public static class FullContactYawAudit
{
    public const string BaseHead="d394a177856662467d78b559236533f3a649ac84";
    public const string SourceDirectory="docs/experiments/coupled-slip-inverse";
    public static float YawRate(Vector2 velocity,Vector2 force,float mass)
    {
        var speed=velocity.Length();
        return (velocity.X*force.Y-velocity.Y*force.X)/(mass*(speed*speed+
            SlipCoupledDynamics.LowSpeedMetersPerSecond*SlipCoupledDynamics.LowSpeedMetersPerSecond));
    }
    public static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Save(string dir,string name,object data)=>InverseFeasibilityAudit.Save(dir,name,data);
    public static void Run(string dir,string stage)
    {
        Directory.CreateDirectory(dir);
        if(stage=="math"){
            Save(dir,"native-mathematics.json",new{BaseHead,NewWorlds=0,AdditionalContactEvaluations=0,
                Cases=new[]{false,true}.Select(w=>Recorded(dir,"control"+(w?"-wide":"-inner"),SourceDirectory,"coupled"+(w?"-wide":"-inner"),false)).ToArray()});return;
        }
        if(stage=="off-reference"){
            if(!File.Exists(Path.Combine(dir,"native-mathematics.json")))throw new InvalidOperationException("Recorded mathematics first.");
            var c=SpeedGripCrossoverAudit.Execute(false,coupled:true,duration:1).Body;
            Save(dir,"off-reference.json",new{BaseHead,DurationSeconds=1,Name="coupled-inner",c.Result.Fingerprint,
                Rule="One short validation reference captured before editing travelYaw; not another main8lap control. No changed tyre/target/gains."});return;
        }
        if(stage=="cost"){Cost(dir);return;}
        if(stage=="freeze"){
            if(!File.Exists(Path.Combine(dir,"native-mathematics.json"))||!File.Exists(Path.Combine(dir,"off-reference.json")))
                throw new InvalidOperationException("Native mathematics and pre-edit OFF reference first.");
            var parameters=SpeedGripCrossoverAudit.Parameters(null) with{EnableCoupledSlipInverse=true};
            using var old=JsonDocument.Parse(File.ReadAllText(Path.Combine(SourceDirectory,"freeze.json")));
            if(JsonSerializer.Serialize(parameters,InverseFeasibilityAudit.Json)!=old.RootElement.GetProperty("Coupled").GetRawText())
                throw new InvalidOperationException("Control configuration differs from #108.");
            var files=new[]{"coupled-inner","coupled-wide"}.SelectMany(n=>new[]{n+".json",n+"-tracking.csv.gz",n+"-forces.csv.gz",n+"-decisions.csv.gz"})
                .Concat(new[]{"baseline-bins.json","groups-freeze.json","freeze.json"});
            Save(dir,"freeze.json",new{BaseHead,ControlParameters=parameters,FullParameters=parameters with{EnableFullContactYawFeedForward=true},
                Sources=files.Select(file=>new{File=file,Sha256=Hash(Path.Combine(SourceDirectory,file))}),
                MathematicsSha256=Hash(Path.Combine(dir,"native-mathematics.json")),OffReferenceSha256=Hash(Path.Combine(dir,"off-reference.json")),
                InverseSourceSha256=Hash("tools/shared-world-speedway/CoupledSlipInverse.cs"),
                MainRuns="Two FULL worlds only. Control8lap reused #108. Initial reference32/speed18/rollingomega, Grip1/Drive1, h10ms with original physical splits,8lap/final2.",
                FrozenGroups="Use #108 baseline-bins.json byte-for-byte. Same common0.5m reference bins, equal-bin weighting, group3 separate. Also report current runtime inverse labels.",
                Rule="Change only travelYaw source lateralForce -> already computed tyreForce. Keep force/passiveForce, target, PD/authority, moment/energy guard and all physics/geometry unchanged.",
                Acceptance="Unchanged #106 RMS lateral<=.5/max<=1m, RMS/max heading<=3/10deg,8lap/last3maxmin<=1.005, whole footprint and between-step safety. No ranking of failed times."});return;
        }
        if(stage=="runs"){
            using var freeze=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"freeze.json")));
            foreach(var item in freeze.RootElement.GetProperty("Sources").EnumerateArray())
                if(Hash(Path.Combine(SourceDirectory,item.GetProperty("File").GetString()!))!=item.GetProperty("Sha256").GetString())throw new InvalidOperationException("Frozen control changed.");
            if(Hash("tools/shared-world-speedway/CoupledSlipInverse.cs")!=freeze.RootElement.GetProperty("InverseSourceSha256").GetString())throw new InvalidOperationException("Target inverse changed.");
            var runs=new List<object>();
            foreach(var wide in new[]{false,true}){
                var name="full"+(wide?"-wide":"-inner");var observations=new List<CoupledSlipInverseObservation>();var timer=Stopwatch.StartNew();
                var result=SpeedGripCrossoverAudit.Capture(dir,name,wide,coupled:true,inverseObserver:observations.Add,fullContactYaw:true);timer.Stop();
                using(var writer=new StreamWriter(new GZipStream(File.Create(Path.Combine(dir,name+"-decisions.csv.gz")),CompressionLevel.SmallestSize))){
                    writer.WriteLine("t,status,required,min,max,legacy_beta,selected_beta,achieved,roots,evaluations");
                    foreach(var o in observations)InverseFeasibilityAudit.Row(writer,o.TimeSeconds,(int)o.Result.Status,o.Result.RequiredNewtons,o.Result.MinimumNewtons,o.Result.MaximumNewtons,
                        o.LegacyBetaRadians,o.AppliedBetaRadians,o.Result.AchievedNewtons,o.Result.Roots,o.Result.ContactEvaluations);
                }
                var projection=Recorded(dir,name,dir,name,true);
                runs.Add(new{Name=name,ElapsedMillisecondsIncludingExports=timer.Elapsed.TotalMilliseconds,InverseCalls=observations.Count,
                    InverseContactEvaluations=observations.Sum(o=>(long)o.Result.ContactEvaluations),Result=result,Recorded=projection});
            }
            Save(dir,"runs.json",runs);return;
        }
        throw new ArgumentException("Use math, off-reference, freeze, runs or cost.");
    }
    public static object Recorded(string dir,string name,string source,string sourceName,bool full)
    {
        var t=new CoupledSlipInverseAudit.Csv(Path.Combine(source,sourceName+"-tracking.csv.gz"));
        var f=new CoupledSlipInverseAudit.Csv(Path.Combine(source,sourceName+"-forces.csv.gz"));
        if(t.Rows.Count!=f.Rows.Count)throw new InvalidOperationException("Native row counts differ.");
        var frame=RacingLineRootCauseAudit.Frame();var path=AuthoredRacingPath.Crossover(sourceName.EndsWith("wide",StringComparison.Ordinal));
        var spec=RacingLineRootCauseAudit.Rider(path);var p=SpeedGripCrossoverAudit.Parameters(null);
        var authority=.75f+.25f*(spec.Profile.Gameplay.Abilities.Technique-1)/98f;
        using var writer=new StreamWriter(new GZipStream(File.Create(Path.Combine(dir,name+"-yaw.csv.gz")),CompressionLevel.SmallestSize));
        writer.WriteLine("t,dt,s,legacy_yaw,full_yaw,delta_yaw,legacy_alpha,full_alpha,legacy_requested,full_requested,legacy_authority,full_authority,legacy_guarded,full_guarded,error_degrees,actual_normal,lateral_normal,fx_normal,resistance_yaw");
        double maxRequestedError=0,maxAuthorityError=0,maxGuardedError=0,maxResistanceYaw=0,duration=0,deltaYaw=0,deltaAlpha=0,deltaRequested=0,deltaAvailable=0;
        for(var i=0;i<t.Rows.Count;i++){
            double T(string key)=>t[i,key];double F(string key)=>f[i,key];
            if(T("t")!=F("t")||T("dt")!=F("dt"))throw new InvalidOperationException("Native clocks differ.");
            var velocity=new Vector2((float)T("vx"),(float)T("vy"));var heading=(float)T("body_heading");
            var forward=new Vector2(MathF.Cos(heading),MathF.Sin(heading));var side=new Vector2(-forward.Y,forward.X);
            var mass=(float)F("mass");var inertia=(float)F("inertia");var h=(float)T("dt");var omega=(float)T("angular_velocity");
            var fx=(float)T("fx");var fy=(float)T("fy");var lateralForce=side*fy;var tyreForce=forward*fx+lateralForce;
            var applied=new Vector2((float)F("force_x"),(float)F("force_y"));var resistance=applied-tyreForce;
            var legacy=YawRate(velocity,lateralForce,mass);var contact=YawRate(velocity,tyreForce,mass);
            var error=(float)T("yaw_error");
            float Alpha(float yaw)=>error/(p.SlipYawResponseSeconds*p.SlipYawResponseSeconds)+2*(yaw-omega)/p.SlipYawResponseSeconds;
            var oldAlpha=Alpha(legacy);var newAlpha=Alpha(contact);
            float Authority(float alpha)=>Math.Clamp(inertia*alpha,-(float)T("budget")*SlipCoupledDynamics.YawLeverArmMeters,
                (float)T("budget")*SlipCoupledDynamics.YawLeverArmMeters)*authority;
            // Diagnostic reconstruction of the unchanged original finite-h guard,
            // using only recorded contact work, accepted omega and applied force.
            var credit=Math.Max(0,fx*.33*((F("omega_before")+F("omega_after"))/2)+F("bearing_loss")/h
                -Vector2.Dot(applied,velocity)-h*applied.LengthSquared()/(2*mass));
            var rate=(double)inertia/h;var root=Math.Sqrt((double)omega*omega+2*credit/rate);
            float Guard(float torque)=>Math.Clamp(torque,(float)(rate*(-omega-root)),(float)(rate*(-omega+root)));
            var oldAuthority=Authority(oldAlpha);var newAuthority=Authority(newAlpha);
            var oldGuard=Guard(oldAuthority);var newGuard=Guard(newAuthority);
            maxRequestedError=Math.Max(maxRequestedError,Math.Abs(inertia*(full?newAlpha:oldAlpha)-T("yaw_requested")));
            maxAuthorityError=Math.Max(maxAuthorityError,Math.Abs((full?newAuthority:oldAuthority)-T("yaw_authority")));
            maxGuardedError=Math.Max(maxGuardedError,Math.Abs((full?newGuard:oldGuard)-T("yaw_energy")));
            var normal=new Vector2(-velocity.Y,velocity.X)/velocity.Length();var resYaw=YawRate(velocity,resistance,mass);
            maxResistanceYaw=Math.Max(maxResistanceYaw,Math.Abs(resYaw));
            InverseFeasibilityAudit.Row(writer,T("t"),T("dt"),T("s"),legacy,contact,contact-legacy,oldAlpha,newAlpha,
                inertia*oldAlpha,inertia*newAlpha,oldAuthority,newAuthority,oldGuard,newGuard,error*180/Math.PI,
                Vector2.Dot(applied,normal)/mass,Vector2.Dot(lateralForce,normal)/mass,Vector2.Dot(forward*fx,normal)/mass,resYaw);
            if(frame.RoadAt(T("s"),0).IsTurn){var dt=T("dt");duration+=dt;deltaYaw+=(contact-legacy)*dt;deltaAlpha+=(newAlpha-oldAlpha)*dt;
                deltaRequested+=inertia*(newAlpha-oldAlpha)*dt;deltaAvailable+=(newGuard-oldGuard)*dt;}
        }
        if(Math.Max(maxRequestedError,Math.Max(maxAuthorityError,maxGuardedError))>.003)
            throw new InvalidOperationException($"Recorded yaw reconstruction failed: request={maxRequestedError:R}, authority={maxAuthorityError:R},guard={maxGuardedError:R}");
        return new{Name=name,Rows=t.Rows.Count,Full=full,MeanCornerDeltaYawRadiansPerSecond=deltaYaw/duration,
            MeanCornerDeltaYawAcceleration=deltaAlpha/duration,MeanCornerDeltaRequestedNewtonMeters=deltaRequested/duration,
            MeanCornerDeltaAvailableNewtonMeters=deltaAvailable/duration,MaximumRequestedReplayError=maxRequestedError,
            MaximumAuthorityReplayError=maxAuthorityError,MaximumGuardReplayError=maxGuardedError,MaximumResistanceYawRadiansPerSecond=maxResistanceYaw,
            SlipErrorDeltaOnFrozenState=0,AdditionalContactEvaluations=0,BodySteps=0};
    }
    private static void Cost(string dir)
    {
        var t=new CoupledSlipInverseAudit.Csv(Path.Combine(SourceDirectory,"coupled-inner-tracking.csv.gz"));
        var f=new CoupledSlipInverseAudit.Csv(Path.Combine(SourceDirectory,"coupled-inner-forces.csv.gz"));
        var path=AuthoredRacingPath.Crossover(false);var rider=RacingLineRootCauseAudit.Rider(path);
        var surface=CoreSim.TrackState.CreateDefault(path.Frame.Track,new(1,0,.5f)).Snapshot();
        var control=SpeedGripCrossoverAudit.Parameters(null) with{EnableCoupledSlipInverse=true};
        var full=control with{EnableFullContactYawFeedForward=true};var samples=new List<object>();Exception? failure=null;
        var worker=new Thread(()=>{try{
            foreach(var phase in new[]{35,60,90,125}){
                var index=t.Rows.FindIndex(r=>r[2]>=6*path.Frame.LapMeters+phase);
                double T(string key)=>t[index,key];double F(string key)=>f[index,key];
                var position=new Vector2((float)T("x"),(float)T("y"));var velocity=new Vector2((float)T("vx"),(float)T("vy"));
                var v=new RiderView(1,position,velocity,(float)T("body_heading"),(float)T("angular_velocity"),path.Frame.Project(position),T("s"),0,RiderActivity.Racing,1,(float)T("target"),0);
                var wheel=new RearWheelState(F("omega_before")*.33);var guidance=path.Guidance(v,control,true);var mass=(float)F("mass");var inertia=(float)F("inertia");var h=(float)T("dt");
                CoupledSlipInverseObservation a=default,b=default;
                _=SlipCoupledDynamics.Forces(v,rider,path.Frame,surface,control,0,mass,inertia,rearWheel:wheel,stepSeconds:h,diagnosticPath:guidance,observeInverse:x=>a=x);
                _=SlipCoupledDynamics.Forces(v,rider,path.Frame,surface,full,0,mass,inertia,rearWheel:wheel,stepSeconds:h,diagnosticPath:guidance,observeInverse:x=>b=x);
                if(a.Result!=b.Result)throw new InvalidOperationException("Yaw flag changed target/contact evaluations.");
                for(var i=0;i<100;i++)foreach(var p in new[]{control,full})_=SlipCoupledDynamics.Forces(v,rider,path.Frame,surface,p,0,mass,inertia,rearWheel:wheel,stepSeconds:h,diagnosticPath:guidance);
                var modes=new List<object>();var timer=new Stopwatch();const int count=2000;
                foreach(var p in new[]{control,full}){
                    double sink=0;timer.Restart();var before=GC.GetAllocatedBytesForCurrentThread();
                    for(var i=0;i<count;i++)sink+=SlipCoupledDynamics.Forces(v,rider,path.Frame,surface,p,0,mass,inertia,rearWheel:wheel,stepSeconds:h,diagnosticPath:guidance).Torque;
                    var bytes=GC.GetAllocatedBytesForCurrentThread()-before;timer.Stop();if(bytes!=0)throw new InvalidOperationException("Force call allocates.");
                    modes.Add(new{Full=p.EnableFullContactYawFeedForward,Count=count,Milliseconds=timer.Elapsed.TotalMilliseconds,AllocatedBytes=bytes,Sink=sink});
                }
                samples.Add(new{Phase=phase,Seconds=h,ExistingInverseContactEvaluations=a.Result.ContactEvaluations,
                    AdditionalContactEvaluations=0,TargetInverseUnchanged=true,Modes=modes});
            }
        }catch(Exception error){failure=error;}});worker.Start();worker.Join();if(failure is not null)throw new InvalidOperationException("Cost validation failed",failure);
        Save(dir,"force-cost.json",new{Samples=samples,BodySteps=0,WheelCommits=0,
            Rule="Same accepted #108 states, both modes COUPLED. Original complete Forces, guidance ready as in WorldRunner. Dedicated warmed thread,0B. No contact query added to obtain tyreForce."});
    }
}
