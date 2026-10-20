using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;

namespace SharedWorldSpeedway;

public static class CoupledSlipInverseAudit
{
    public const string BaseHead="67204a2ad417de05b313b4a54bf301cb27356825";
    public const string SourceDirectory=FullThrottleSlideAudit.SourceDirectory;
    public static CoupledSlipInverse.Contact Contact(RiderView v,Vector2 force,RearWheelStep wheel,float budget,float mass)
    {
        var forward=new Vector2(MathF.Cos(v.Heading),MathF.Sin(v.Heading));var side=new Vector2(-forward.Y,forward.X);
        return new(v.Velocity,force-forward*wheel.LongitudinalNewtons-side*wheel.LateralNewtons,budget,mass,wheel.BeforeOmega,
            wheel.RequestedTorqueNewtonMeters,wheel.AvailableTorqueNewtonMeters,wheel.DriveTorqueNewtonMeters,(float)wheel.Seconds,RearWheelParameters.Default);
    }
    private static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Save(string dir,string name,object data)=>InverseFeasibilityAudit.Save(dir,name,data);
    public sealed class Csv
    {
        public string[] Header {get;}
        public List<double[]> Rows {get;}=new();
        private readonly Dictionary<string,int> columns;
        public Csv(string file){using var stream=File.OpenRead(file);using var zip=new GZipStream(stream,CompressionMode.Decompress);using var reader=new StreamReader(zip);
            Header=reader.ReadLine()!.Split(',');columns=Header.Select((n,i)=>(n,i)).ToDictionary(x=>x.n,x=>x.i);
            while(reader.ReadLine() is {} row)Rows.Add(row.Split(',').Select(v=>double.Parse(v,CultureInfo.InvariantCulture)).ToArray());}
        public double this[int row,string key]=>Rows[row][columns[key]];
    }
    public static void Run(string dir,string stage)
    {
        Directory.CreateDirectory(dir);
        if(stage=="math"){Mathematics(dir);return;}
        if(stage=="cost"){Cost(dir);return;}
        if(stage=="verify-envelope"){VerifyEnvelopes(dir);return;}
        if(stage=="verify-forces"){VerifyEnvelopes(dir,true);return;}
        if(stage=="freeze"){
            if(File.Exists(Path.Combine(dir,"freeze.json")))throw new IOException("Freeze exists.");
            if(!File.Exists(Path.Combine(dir,"mathematics.json")))throw new InvalidOperationException("Mathematics first.");
            var files=new[]{"baseline-inner","baseline-wide"}.SelectMany(n=>new[]{n+".json",n+"-tracking.csv.gz",n+"-forces.csv.gz"}).ToArray();
            Save(dir,"freeze.json",new{BaseHead,Parameters=SpeedGripCrossoverAudit.Parameters(null),Coupled=SpeedGripCrossoverAudit.Parameters(null) with{EnableCoupledSlipInverse=true},
                Variant=RacingLineRootCauseAudit.Variant(),Sources=files.Select(f=>new{File=f,Sha256=Hash(Path.Combine(SourceDirectory,f))}),
                MathematicsSha256=Hash(Path.Combine(dir,"mathematics.json")),SolverSha256=Hash("tools/shared-world-speedway/CoupledSlipInverse.cs"),
                AngleDomainDegrees=new[]{-45,45},CoupledSlipInverse.AngularIntervals,CoupledSlipInverse.ExtremumIterations,CoupledSlipInverse.RootIterations,
                CoupledSlipInverse.ForceToleranceNewtons,CoupledSlipInverse.EnvelopeUncertaintyNewtons,CoupledSlipInverse.AngleToleranceRadians,
                Branch="Enumerate sign-changing intervals including refined extrema; least absolute beta, smaller signed angle for exact ties. Tangent/uncertain requests remain separate.",
                Fallback="Original bounded legacy target for every UNREACHABLE or AMBIGUOUS; do not alter Drive, forces or accepted states.",
                FrozenGroups="All original native baseline samples classified before ON runs. Final2 laps -> fixed 0.5m reference bins. Mixed/absent baseline statuses => group3. Frozen group remains fixed in ON, and current-state classification is reported separately.",
                Safety="Unchanged #106 complete8laps, last3 durations max/min<=1.005, final2 lateral RMS<=.5/max<=1m and velocity heading RMS<=3/max<=10deg; whole-run inner>=.05/outer>=.5, interval inner>=0/outer>=.5.",
                NewWorlds="Initially two only: INNER and WIDE, Drive1, Grip1, rolling18m/s atreference32, h10ms,8laps. No legacy matrix rerun.",
                Envelope="Numerical angular envelope with 0.25N uncertainty; independently dense-checked before worlds. It is a local force test, not a trajectory/yaw feasibility certificate."});
            foreach(var wide in new[]{false,true})Classify(dir,"baseline"+(wide?"-wide":"-inner"),SourceDirectory,wide);
            Save(dir,"groups-freeze.json",new{BaseHead,FreezeSha256=Hash(Path.Combine(dir,"freeze.json")),
                InnerSha256=Hash(Path.Combine(dir,"baseline-inner-inverse.csv.gz")),WideSha256=Hash(Path.Combine(dir,"baseline-wide-inverse.csv.gz")),BinMeters=.5});return;
        }
        using var freeze=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"freeze.json")));
        foreach(var f in freeze.RootElement.GetProperty("Sources").EnumerateArray())
            if(Hash(Path.Combine(SourceDirectory,f.GetProperty("File").GetString()!))!=f.GetProperty("Sha256").GetString())throw new InvalidOperationException("Legacy source changed.");
        if(freeze.RootElement.GetProperty("SolverSha256").GetString()!=Hash("tools/shared-world-speedway/CoupledSlipInverse.cs"))throw new InvalidOperationException("Solver changed after freeze.");
        if(stage=="runs"){
            var binsSha256=Hash(Path.Combine(dir,"baseline-bins.json"));
            using var groups=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"groups-freeze.json")));
            if(groups.RootElement.GetProperty("FreezeSha256").GetString()!=Hash(Path.Combine(dir,"freeze.json")))throw new InvalidOperationException("Groups changed.");
            var results=new List<object>();
            foreach(var wide in new[]{false,true}){
                var name="coupled"+(wide?"-wide":"-inner");var observations=new List<CoupledSlipInverseObservation>();var timer=Stopwatch.StartNew();
                var run=SpeedGripCrossoverAudit.Capture(dir,name,wide,coupled:true,inverseObserver:observations.Add);timer.Stop();
                using(var writer=new StreamWriter(new GZipStream(File.Create(Path.Combine(dir,name+"-decisions.csv.gz")),CompressionLevel.SmallestSize))){
                    writer.WriteLine("t,status,required,min,max,legacy_beta,selected_beta,achieved,roots,evaluations");
                    foreach(var o in observations)InverseFeasibilityAudit.Row(writer,o.TimeSeconds,(int)o.Result.Status,o.Result.RequiredNewtons,o.Result.MinimumNewtons,o.Result.MaximumNewtons,
                        o.LegacyBetaRadians,o.AppliedBetaRadians,o.Result.AchievedNewtons,o.Result.Roots,o.Result.ContactEvaluations);
                }
                results.Add(new{Name=name,BaselineBinsSha256=binsSha256,ElapsedMillisecondsIncludingNativeExports=timer.Elapsed.TotalMilliseconds,ActualInverseCalls=observations.Count,
                    ContactEvaluations=observations.Sum(o=>(long)o.Result.ContactEvaluations),MaximumContactEvaluations=observations.Max(o=>o.Result.ContactEvaluations),Run=run});
                Classify(dir,name,dir,wide);
            }
            Save(dir,"runs.json",results);return;
        }
        throw new ArgumentException("Use math, freeze, runs, cost, verify-envelope or verify-forces.");
    }
    private static void Mathematics(string dir)
    {
        var sampler=new FullThrottleSlideAudit.Sampler(new(1,0,.5f));var states=new List<object>();var maximumEnvelopeMiss=0d;var maximumRootError=0d;var queries=0;
        foreach(var speed in new[]{5,18,24,35})foreach(var spin in new[]{.5,1,1.4,2})foreach(var drive in new[]{0f,1f})foreach(var h in new[]{0f,.005f,.01f,.02f}){
            var q=sampler.At(speed,0,0);var v=q.View with{Drive=drive};q=sampler.Evaluate(v,speed*spin/.33,h);
            var c=Contact(v,q.Force,q.Wheel,q.Slip.GripBudgetNewtons,147);var dense=new double[1441];
            for(var i=0;i<dense.Length;i++)dense[i]=c.NormalForce(-Math.PI/4+i*Math.PI/2880);
            var envelope=CoupledSlipInverse.Solve(c,0);var miss=Math.Max(dense.Max()-envelope.MaximumNewtons,envelope.MinimumNewtons-dense.Min());maximumEnvelopeMiss=Math.Max(maximumEnvelopeMiss,miss);
            if(miss>CoupledSlipInverse.EnvelopeUncertaintyNewtons)throw new InvalidOperationException("Angular envelope missed dense extrema.");
            foreach(var fraction in new[]{-1.01,-.95,-.75,-.25,0,.25,.75,.95,1.01}){
                var request=c.Budget*fraction;var r=CoupledSlipInverse.Solve(c,request);queries++;
                if(r.Status==SlipInverseStatus.Solved){var error=Math.Abs(c.NormalForce(r.BetaRadians)-request);maximumRootError=Math.Max(maximumRootError,error);
                    if(error>CoupledSlipInverse.ForceToleranceNewtons+.003)throw new InvalidOperationException("Bad root.");}
                if(r.Status==SlipInverseStatus.Unreachable&&dense.Min()<request&&request<dense.Max())throw new InvalidOperationException("Rejected dense bracket.");
            }
            states.Add(new{Speed=speed,WheelSpeedRatio=spin,Drive=drive,Seconds=h,DenseMinimum=dense.Min(),DenseMaximum=dense.Max(),envelope.MinimumNewtons,envelope.MaximumNewtons,EnvelopeMiss=miss});
        }
        var sample=sampler.At(18,10,.15);sample=sampler.Evaluate(sample.View,sample.Wheel.BeforeOmega,.01f);var benchmark=Contact(sample.View,sample.Force,sample.Wheel,sample.Slip.GripBudgetNewtons,147);
        object? performance=null;Exception? failure=null;
        var worker=new Thread(()=>{try{
            const int count=2000;double sink=0;for(var i=0;i<100;i++){sink+=benchmark.NormalForce(.2);sink+=CoupledSlipInverse.Solve(benchmark,1500).BetaRadians;}
            var bytes=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();var initialBytes=GC.GetAllocatedBytesForCurrentThread();
            for(var i=0;i<count;i++)sink+=benchmark.NormalForce(.2);timer.Stop();var contactMs=timer.Elapsed.TotalMilliseconds;var contactBytes=GC.GetAllocatedBytesForCurrentThread()-initialBytes;
            timer.Restart();initialBytes=GC.GetAllocatedBytesForCurrentThread();long evaluations=0;
            for(var i=0;i<count;i++){var r=CoupledSlipInverse.Solve(benchmark,1500);sink+=r.BetaRadians;evaluations+=r.ContactEvaluations;}
            timer.Stop();var inverseBytes=GC.GetAllocatedBytesForCurrentThread()-initialBytes;
            performance=new{Count=count,ContactMilliseconds=contactMs,InverseMilliseconds=timer.Elapsed.TotalMilliseconds,ContactAllocatedBytes=contactBytes,InverseAllocatedBytes=inverseBytes,
                Evaluations=evaluations,RatioToSingleContact=timer.Elapsed.TotalMilliseconds/contactMs,Sink=sink,SetupBytes=initialBytes-bytes};
            if(inverseBytes!=0)throw new InvalidOperationException("Inverse allocates.");
        }catch(Exception ex){failure=ex;}});worker.Start();worker.Join();if(failure is not null)throw new InvalidOperationException("Benchmark failed",failure);
        Save(dir,"mathematics.json",new{States=states,Queries=queries,MaximumEnvelopeMissNewtons=maximumEnvelopeMiss,MaximumRootErrorNewtons=maximumRootError,Performance=performance,
            Rule="128 original-contact states: V5/18/24/35, wheel speed ratio0.5/1/1.4/2, Drive0/1, h0/5/10/20ms. Independent1441-angle reference per state; no body integration. Numerical evidence, not a universal interval-arithmetic proof."});
    }
    private static void Classify(string dir,string name,string source,bool wide)
    {
        var tracking=new Csv(Path.Combine(source,name+"-tracking.csv.gz"));var forces=new Csv(Path.Combine(source,name+"-forces.csv.gz"));
        if(tracking.Rows.Count!=forces.Rows.Count)throw new InvalidOperationException("Clock lengths differ.");
        var sampler=new FullThrottleSlideAudit.Sampler(new(1,0,.5f),wide);var count=tracking.Rows.Count;double forceError=0;
        using var writer=new StreamWriter(new GZipStream(File.Create(Path.Combine(dir,name+"-inverse.csv.gz")),CompressionLevel.SmallestSize));
        writer.WriteLine("t,dt,s,status,required_normal,actual_normal,min_normal,max_normal,inverse_beta,math_residual,evaluations,roots,front_fy,rear_fy,forward,side");
        for(var i=0;i<count;i++){
            double T(string k)=>tracking[i,k];double F(string k)=>forces[i,k];
            if(T("t")!=F("t")||T("dt")!=F("dt"))throw new InvalidOperationException("Native clocks differ.");
            var position=new Vector2((float)T("x"),(float)T("y"));var velocity=new Vector2((float)T("vx"),(float)T("vy"));
            var v=new RiderView(1,position,velocity,(float)T("body_heading"),(float)T("angular_velocity"),sampler.Path.Frame.Project(position),T("s"),0,RiderActivity.Racing,1,(float)T("target"),0);
            var q=sampler.Evaluate(v,F("omega_before"),(float)T("dt"));var c=Contact(v,q.Force,q.Wheel,q.Slip.GripBudgetNewtons,(float)F("mass"));
            forceError=Math.Max(forceError,Math.Max(Math.Abs(q.Force.X-F("force_x")),Math.Abs(q.Force.Y-F("force_y"))));
            var request=T("steering_demand")*F("mass");var inverse=CoupledSlipInverse.Solve(c,request);var normal=new Vector2(-velocity.Y,velocity.X)/v.Speed;
            InverseFeasibilityAudit.Row(writer,T("t"),T("dt"),T("s"),(int)inverse.Status,request,Vector2.Dot(q.Force,normal),inverse.MinimumNewtons,inverse.MaximumNewtons,inverse.BetaRadians,
                inverse.Status==SlipInverseStatus.Solved?inverse.AchievedNewtons-request:0,inverse.ContactEvaluations,inverse.Roots,q.Wheel.FrontLateralNewtons,q.Wheel.RearLateralNewtons,v.ForwardSpeed,v.SideSpeed);
        }
        if(forceError>.003)throw new InvalidOperationException("Contact replay changed.");
        Save(dir,name+"-classification.json",new{Rows=count,MaximumForceReplayErrorNewtons=forceError,SourceFingerprint=JsonDocument.Parse(File.ReadAllText(Path.Combine(source,name+".json"))).RootElement.GetProperty("Result").GetProperty("Fingerprint").GetString()});
    }
    private static void Cost(string dir)
    {
        var native=new Csv(Path.Combine(SourceDirectory,"baseline-inner-tracking.csv.gz"));var forces=new Csv(Path.Combine(SourceDirectory,"baseline-inner-forces.csv.gz"));
        var sampler=new FullThrottleSlideAudit.Sampler(new(1,0,.5f));var path=sampler.Path;var rider=RacingLineRootCauseAudit.Rider(path);
        var surface=TrackState.CreateDefault(path.Frame.Track,new(1,0,.5f)).Snapshot();var p=SpeedGripCrossoverAudit.Parameters(null);var parameters=p with{EnableCoupledSlipInverse=true};
        var results=new List<object>();Exception? failure=null;
        var worker=new Thread(()=>{try{
            foreach(var phase in new[]{35,60,90,125}){
                var index=native.Rows.FindIndex(r=>r[2]>=6*path.Frame.LapMeters+phase);
                double T(string k)=>native[index,k];double F(string k)=>forces[index,k];
                var position=new Vector2((float)T("x"),(float)T("y"));var velocity=new Vector2((float)T("vx"),(float)T("vy"));
                var v=new RiderView(1,position,velocity,(float)T("body_heading"),(float)T("angular_velocity"),path.Frame.Project(position),T("s"),0,RiderActivity.Racing,1,(float)T("target"),0);
                var wheel=new RearWheelState(F("omega_before")*.33);var guidance=path.Guidance(v,p,true);var mass=(float)F("mass");var inertia=(float)F("inertia");var h=(float)T("dt");
                for(var i=0;i<100;i++){
                    _=SlipCoupledDynamics.Forces(v,rider,path.Frame,surface,p,0,mass,inertia,rearWheel:wheel,stepSeconds:h,diagnosticPath:guidance);
                    _=SlipCoupledDynamics.Forces(v,rider,path.Frame,surface,parameters,0,mass,inertia,rearWheel:wheel,stepSeconds:h,diagnosticPath:guidance);
                }
                var timer=new Stopwatch();const int count=2000;var modes=new List<object>();
                foreach(var on in new[]{false,true}){
                    var config=on?parameters:p;double sink=0;timer.Restart();var before=GC.GetAllocatedBytesForCurrentThread();
                    for(var i=0;i<count;i++){var f=SlipCoupledDynamics.Forces(v,rider,path.Frame,surface,config,0,mass,inertia,rearWheel:wheel,stepSeconds:h,diagnosticPath:guidance);sink+=f.Torque;}
                    var bytes=GC.GetAllocatedBytesForCurrentThread()-before;timer.Stop();
                    if(bytes!=0)throw new InvalidOperationException("Force call allocates.");
                    modes.Add(new{Coupled=on,Count=count,Milliseconds=timer.Elapsed.TotalMilliseconds,AllocatedBytes=bytes,Sink=sink});
                }
                results.Add(new{Reference=T("s"),Phase=phase,Speed=v.Speed,Seconds=h,Modes=modes});
            }
        }catch(Exception error){failure=error;}});worker.Start();worker.Join();
        if(failure is not null)throw new InvalidOperationException("Force cost failed.",failure);
        Save(dir,"force-cost.json",new{Samples=results,Rule="Four accepted native baseline INNER states, frozen seventh-lap reference35/60/90/125. Same original complete Forces, accepted wheel never committed. Guidance prepared outside each timed loop as in WorldRunner. Dedicated warmed thread;0B required. No new world or retuning."});
    }
    private static void VerifyEnvelopes(string dir,bool forcesOnly=false)
    {
        var results=new List<object>();
        foreach(var line in new[]{"inner","wide"})foreach(var mode in new[]{"baseline","coupled"}){
            var name=mode+"-"+line;var source=mode=="baseline"?SourceDirectory:dir;
            var native=new Csv(Path.Combine(source,name+"-tracking.csv.gz"));var forces=new Csv(Path.Combine(source,name+"-forces.csv.gz"));var labels=new Csv(Path.Combine(dir,name+"-inverse.csv.gz"));
            using var summary=JsonDocument.Parse(File.ReadAllText(Path.Combine(source,name+".json")));
            var start=summary.RootElement.GetProperty("CrossingSeconds")[5].GetDouble();var end=summary.RootElement.GetProperty("CrossingSeconds")[7].GetDouble();
            var sampler=new FullThrottleSlideAudit.Sampler(new(1,0,.5f),line=="wide");var checkedRows=0;var queries=0;double maximumMiss=0,forceError=0;
            for(var i=0;i<native.Rows.Count;i++){
                double T(string k)=>native[i,k];double F(string k)=>forces[i,k];
                var position=new Vector2((float)T("x"),(float)T("y"));var velocity=new Vector2((float)T("vx"),(float)T("vy"));
                var v=new RiderView(1,position,velocity,(float)T("body_heading"),(float)T("angular_velocity"),sampler.Path.Frame.Project(position),T("s"),0,RiderActivity.Racing,1,(float)T("target"),0);
                var q=sampler.Evaluate(v,F("omega_before"),(float)T("dt"));var c=Contact(v,q.Force,q.Wheel,q.Slip.GripBudgetNewtons,(float)F("mass"));
                forceError=Math.Max(forceError,Math.Max(Math.Max(Math.Abs(q.Force.X-F("force_x")),Math.Abs(q.Force.Y-F("force_y"))),
                    Math.Max(Math.Abs(q.Wheel.LongitudinalNewtons-T("fx")),Math.Abs(q.Wheel.LateralNewtons-T("fy")))));
                if(forcesOnly||T("t")<start||T("t")>=end||labels[i,"status"]==0)continue;
                double min=double.PositiveInfinity,max=double.NegativeInfinity;
                for(var j=0;j<=1440;j++){var value=c.NormalForce(-Math.PI/4+j*Math.PI/2880);min=Math.Min(min,value);max=Math.Max(max,value);queries++;}
                var miss=Math.Max(max-labels[i,"max_normal"],labels[i,"min_normal"]-min);maximumMiss=Math.Max(maximumMiss,miss);
                if(miss>CoupledSlipInverse.EnvelopeUncertaintyNewtons)throw new InvalidOperationException("Native envelope missed an extremum.");
                var request=labels[i,"required_normal"];
                if(labels[i,"status"]==1&&min<request&&request<max)throw new InvalidOperationException("Native UNREACHABLE has a dense bracket.");
                checkedRows++;
            }
            if(forceError!=0)throw new InvalidOperationException("Native force components changed.");
            results.Add(new{Name=name,NativeForceRows=native.Rows.Count,MaximumForceReplayErrorNewtons=forceError,
                CheckedNonSolvedNativeRows=checkedRows,IndependentContactQueries=queries,MaximumEnvelopeMissNewtons=maximumMiss});
        }
        Save(dir,forcesOnly?"native-force-validation.json":"native-envelope-validation.json",new{Cases=results,
            Rule=forcesOnly?"Every native Fx/Fy/global-force component checked against original contact, both modes/lines, entire8lap. No body integration.":
                "Every non-solved final2 native state from both modes/lines checked independently on1441 angles. Freeze labels never changed; a false rejection aborts validation. Offline queries only, no integration."});
    }
}
