using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using CoreSim;
using CoreSim.Setup;

namespace SharedWorldSpeedway;

public static class SlipDynamicsAudit
{
    public const string Baseline="7ebf687fa78a4534e71f301310e62e37bef0188c";
    public static RiderSpec Rider(float offset=5,double reference=0,float speed=18,int id=1)
        =>new(FullHeatSoloAudit.Profile(id),BikeSetup.Neutral,reference,offset,speed,offset,TrafficEnabled:false);
    public static Scenario[] Cases()=>new[] {
        new Scenario("A-straight",new[]{Rider()},1),
        new Scenario("A-arc",new[]{Rider(reference:65)},1),
        new Scenario("B-inner",new[]{Rider(2)},60,2),
        new Scenario("B-middle",new[]{Rider(5)},60,2),
        new Scenario("B-outer",new[]{Rider(8)},60,2),
        new Scenario("C-grip",new[]{Rider(5)},60,2),
        new Scenario("C-throttle",new[]{Rider(5)},60,2),
        new Scenario("D-pack",new[]{Rider(2,0,18,1),Rider(4,4,18,2),Rider(6,8,18,3),Rider(8,12,18,4)},10)
    };
    public static void Run(string directory,string stage)
    {
        Directory.CreateDirectory(directory);
        if(stage=="focus" || stage=="runs") {
            var cases=stage=="focus"?Cases().Where(s=>s.Name.StartsWith("A-",StringComparison.Ordinal)||s.Name=="B-middle")
                :Cases().Where(s=>!s.Name.StartsWith("A-",StringComparison.Ordinal)&&s.Name!="B-middle");
            foreach(var s in cases)foreach(var on in new[]{false,true})Execute(s,on,directory);
            return;
        }
        if(stage=="benchmark") {Benchmark(directory);return;}
        if(stage!="baseline")throw new ArgumentException("Unknown stage.");
        var path=Path.Combine(directory,"baseline.json");
        if(File.Exists(path))throw new InvalidOperationException("Baseline already sealed.");
        var rows=new List<object>();
        foreach(var s in Cases().Where(s=>s.Name.StartsWith("A-",StringComparison.Ordinal))) {
            var forces=new List<ForceObservation>();
            using var w=new WorldRunner(s.Riders,diagnostics:new(){Force=forces.Add});
            var initial=w.Snapshot;var result=w.Run(s.Name,.01,s.DurationSeconds);
            rows.Add(new{s.Name,Initial=initial,Result=result,Forces=forces});
        }
        File.WriteAllText(path,JsonSerializer.Serialize(new{Baseline,Cases=Cases(),Rows=rows},Audit.Json)+"\n");
    }
    public static object Execute(Scenario s,bool on,string? directory=null,bool reverse=false)
    {
        var p=new WorldParameters{EnableSlipCoupledDynamics=on};
        var state=s.Name=="C-grip"?new TrackSurfaceState(.85f,0,.5f):TrackSurfaceState.Default;
        var track=CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var surface=TrackState.CreateDefault(track,state).Snapshot();
        var controls=s.Name=="C-throttle"?new[]{new ControlCommand(1,0,5,.65f)}:Array.Empty<ControlCommand>();
        var slip=new List<SlipObservation>();var sampled=new List<SlipObservation>();
        var lastForce=new Dictionary<int,ForceObservation>();var lastSlip=new Dictionary<int,SlipObservation>();
        var crossings=s.Riders.ToDictionary(r=>r.Profile.Id,_=>new LapCrossings());
        var energy=s.Riders.ToDictionary(r=>r.Profile.Id,_=>new SlipEnergy());
        var motion=s.Riders.ToDictionary(r=>r.Profile.Id,_=>new List<RiderView>());
        var consequences=new List<ConsequenceObservation>();
        using var w=new WorldRunner(reverse?s.Riders.Reverse():s.Riders,track,p,new(){
            Force=f=>lastForce[f.Rider.Id]=f,Slip=f=>{lastSlip[f.RiderId]=f;slip.Add(f);},Consequence=consequences.Add},controls,surface);
        var initial=w.Snapshot;var previous=initial;double nextSample=0;
        var file=s.Name+(on?"-ON":"-OFF");
        using var csv=new HeatMeasurements(w.Frame,initial,directory is null?null:Path.Combine(directory,file+"-accepted.csv"),true);
        var watch=Stopwatch.GetTimestamp();
        var result=w.Run(s.Name,.01,s.DurationSeconds,s.Laps,snapshot=>{
            var dt=snapshot.TimeSeconds-previous.TimeSeconds;
            foreach(var v in snapshot.Riders) {
                var old=previous.Riders.Single(x=>x.Id==v.Id);
                crossings[v.Id].Accept(previous.TimeSeconds,old.TotalReferenceMeters-s.Riders.Single(r=>r.Profile.Id==v.Id).ReferenceMeters,
                    snapshot.TimeSeconds,v.TotalReferenceMeters-s.Riders.Single(r=>r.Profile.Id==v.Id).ReferenceMeters,w.Frame.LapMeters,s.Laps);
                motion[v.Id].Add(v);
                if(lastForce.TryGetValue(v.Id,out var f)) {
                    energy[v.Id].AppliedWorkJoules+=Vector2.Dot(f.ForceNewtons,(old.Velocity+v.Velocity)/2)*dt
                        +f.TorqueNewtonMeters*(old.AngularVelocity+v.AngularVelocity)/2*dt;
                    // The force is held during this accepted Box2D interval, so midpoint work uses accepted velocities.
                }
                if(lastSlip.TryGetValue(v.Id,out var q)) {
                    energy[v.Id].SlipLossJoules+=q.LateralLossWatts*dt;
                    energy[v.Id].NearGripSeconds+=q.GripUtilization>=.95f?dt:0;
                    energy[v.Id].SlipDegreeSeconds+=Math.Abs(q.ActualSlipDegrees)*dt;
                    energy[v.Id].MaximumSlipDegrees=Math.Max(energy[v.Id].MaximumSlipDegrees,Math.Abs(q.ActualSlipDegrees));
                } else {
                    energy[v.Id].SlipDegreeSeconds+=Math.Abs(old.SlideRadians)*180/Math.PI*dt;
                    energy[v.Id].MaximumSlipDegrees=Math.Max(energy[v.Id].MaximumSlipDegrees,Math.Abs(old.SlideRadians)*180/Math.PI);
                }
            }
            csv.Accept(snapshot);
            if(snapshot.TimeSeconds+1e-9>=nextSample) {
                sampled.AddRange(lastSlip.Values.OrderBy(x=>x.RiderId));nextSample=snapshot.TimeSeconds+.1;
            }
            previous=snapshot;
        },lapsFromInitialPosition:true);
        var elapsed=Stopwatch.GetElapsedTime(watch).TotalMilliseconds;
        var reports=result.Riders.Select(v=>{
            var states=motion[v.Id];var e=energy[v.Id];var times=crossings[v.Id].Times.ToArray();
            // Phase values are from the first complete corner after the initial straight.
            var fromCorner=states.SkipWhile(x=>!x.Road.IsTurn).ToArray();
            var corner=fromCorner.TakeWhile(x=>x.Road.IsTurn).ToArray();
            var nextStraight=fromCorner.Skip(corner.Length).TakeWhile(x=>!x.Road.IsTurn).ToArray();
            var own=slip.Where(x=>x.RiderId==v.Id).ToArray();
            var properties=w.MechanicalProperties(v.Id);var first=initial.Riders.Single(x=>x.Id==v.Id);
            double Kinetic(RiderView x)=>.5*properties.MassKg*x.Velocity.LengthSquared()+.5*properties.InertiaKgMetersSquared*x.AngularVelocity*x.AngularVelocity;
            return new{RiderId=v.Id,Measurement=new{CrossingSeconds=times,LapSeconds=times.Select((t,i)=>t-(i==0?0:times[i-1])).ToArray()},
                EntryMetersPerSecond=corner.FirstOrDefault().Speed,MinimumCornerMetersPerSecond=corner.Length==0?0:corner.Min(x=>x.Speed),
                ExitMetersPerSecond=corner.LastOrDefault().Speed,NextStraightMaximumMetersPerSecond=nextStraight.Length==0?0:nextStraight.Max(x=>x.Speed),
                MeanAbsoluteSlipDegrees=e.SlipDegreeSeconds/result.TimeSeconds,e.MaximumSlipDegrees,e.NearGripSeconds,
                DistanceMeters=v.DistanceMeters,MinimumClearanceMeters=states.Min(x=>HeatMeasurements.Clearance(x,w.Frame)),
                MaximumOffsetErrorMeters=states.Max(x=>Math.Abs(x.Road.OffsetMeters-x.TargetOffsetMeters)),
                MaximumGripUtilization=own.Length==0?0:own.Max(x=>x.GripUtilization),e.AppliedWorkJoules,e.SlipLossJoules,
                InitialKineticEnergyJoules=Kinetic(first),FinalKineticEnergyJoules=Kinetic(v),
                WorkBalanceResidualJoules=Kinetic(v)-Kinetic(first)-e.AppliedWorkJoules};
        }).ToArray();
        var output=new{Run=new{Name=s.Name,Parameters=p,Surface=state,Initial=initial,Result=result,Riders=reports,
            Passes=csv.Passes.Events,Consequences=consequences},Slip=sampled,WallMilliseconds=elapsed};
        if(directory is not null)File.WriteAllText(Path.Combine(directory,file+".json"),JsonSerializer.Serialize(output,Audit.Json)+"\n");
        Console.WriteLine($"{file}: complete={result.Completed} time={result.TimeSeconds:F3} maxSlip={reports.Max(x=>x.MaximumSlipDegrees):F2} clearance={reports.Min(x=>x.MinimumClearanceMeters):F3} calls={result.Work.SolverCalls}");
        return output;
    }
    private sealed class SlipEnergy
    {
        public double AppliedWorkJoules,SlipLossJoules,NearGripSeconds,SlipDegreeSeconds,MaximumSlipDegrees;
    }
    private static void Benchmark(string directory)
    {
        // Warmed, paired timings of identical accepted states and fixed-duration worlds.
        var rider=Rider(reference:65);var frame=new TrackFrame(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
        var surface=TrackState.CreateDefault(frame.Track).Snapshot();
        RiderView v;using(var w=new WorldRunner(new[]{rider}))v=w.Snapshot.Riders[0] with{Heading=w.Snapshot.Riders[0].Heading+.12f};
        const int count=100000;var forces=new List<object>();float sink=0;
        foreach(var on in new[]{false,true}) {
            var p=new WorldParameters{EnableSlipCoupledDynamics=on};
            for(var i=0;i<10000;i++)sink+=RiderController.Forces(v,rider,frame,surface,p,1,142,50).Force.X;
            var timings=new double[5];var allocations=new long[5];
            for(var batch=0;batch<5;batch++) {
                var bytes=GC.GetAllocatedBytesForCurrentThread();var start=Stopwatch.GetTimestamp();
                for(var i=0;i<count;i++)sink+=RiderController.Forces(v,rider,frame,surface,p,1,142,50).Force.X;
                allocations[batch]=GC.GetAllocatedBytesForCurrentThread()-bytes;timings[batch]=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            }
            forces.Add(new{On=on,Calls=count,Milliseconds=timings,AllocatedBytes=allocations});
        }
        var worldTimes=new List<object>();var pack=Cases().Single(x=>x.Name=="D-pack");
        foreach(var on in new[]{false,true})for(var batch=-1;batch<5;batch++) {
            var start=Stopwatch.GetTimestamp();using var run=new WorldRunner(pack.Riders,parameters:new(){EnableSlipCoupledDynamics=on});
            var r=run.Run("benchmark",.01,pack.DurationSeconds);var ms=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            if(batch>=0)worldTimes.Add(new{On=on,Milliseconds=ms,r.Fingerprint,r.Work.SolverCalls});
        }
        File.WriteAllText(Path.Combine(directory,"performance.json"),JsonSerializer.Serialize(new{Forces=forces,Worlds=worldTimes,Sink=sink,
            Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,OS=System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            TieredCompilation=Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),Stopwatch.Frequency},Audit.Json)+"\n");
    }
}
