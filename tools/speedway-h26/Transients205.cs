using System.Diagnostics;
using System.Numerics;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static void Transients205(string repository,string output)
    {
        var archive=Read199<Archive204>(Path.Combine(repository,NativeSource205));
        var run=archive.Run.Riders.Single();
        var shots=Shots205(repository,archive);
        foreach(var (label,g) in new[]{("open-from-zero",0f),("close-from-full",1f)})
        {
            int i=Enumerable.Range(1,run.Samples.Length-2).Where(j=>run.Samples[j].Input.Rider.Speed>=7&&
                run.Samples[j].Input.Time<40).MinBy(j=>Math.Abs(run.Samples[j].Input.Active.Drive-g));
            shots.Add(NativeShot205(archive,i,label));
        }
        var rows=new List<object>();
        var local=new List<object>();
        var timer=Stopwatch.StartNew();
        foreach(var shot in shots)
        foreach(string variant in Physics205)
        foreach(float request in new[]{0f,1f})
        {
            local.Add(new {shot.Name,Gas=request,Mode="instantaneous, same accepted state",
                Result=Measure205(shot with {Start=shot.Start with {Active=shot.Start.Active with {Drive=request}}},variant)});
            var predictor=new Predictor198(Frame,Surface,Physics,Settings202(variant),Timing,shot.Spec,Mass,Inertia);
            var start=shot.Start;
            var action=start.Active with {Drive=request};
            var commands=Enumerable.Repeat(action,40).ToArray();
            var cursor=predictor.Begin(start,commands,0,double.PositiveInfinity,.5,profile:true);
            double due=Timing.Due(start.Review,SharedWorldSpeedway.RiderController.ReactionSeconds(shot.Spec),
                start.Pending.LastOrDefault());
            var samples=new List<object>();
            foreach(double horizon in new[] {.01,.05,.10,.20,.50,.80,1.50})
            {
                cursor.Advance(250,start.State.Time+horizon);
                var last=cursor.LastContact;
                var power=cursor.LastPower;
                object? mechanical=null;
                if(last is not null&&power is not null)
                {
                    var before=cursor.LastContactInput;
                    var f=last.Forces;
                    var along=before.Speed>.01f?Vector2.Normalize(before.Velocity):f.BodyForward;
                    var left=new Vector2(-along.Y,along.X);
                    double n=Vector2.Dot(f.NetForce,left)/Mass;
                    mechanical=new {Gas=before.Drive,Speed=before.Speed,Course=Math.Atan2(along.Y,along.X),
                        Heading=before.Heading,Beta=before.SlideRadians,Yaw=before.AngularVelocity,
                        Ft=Vector2.Dot(f.NetForce,along),Fn=Vector2.Dot(f.NetForce,left),
                        At=Vector2.Dot(f.NetForce,along)/Mass,An=n,
                        BetaDot=before.Speed>.01f?before.AngularVelocity-n/before.Speed:(double?)null,
                        YawDot=f.NetTorque/Inertia,power.Wheel.InstantSlipRatio,power.Engine.EngineRpm,
                        Fx=power.Wheel.AppliedLongitudinalNewtons,Fy=f.RearLateral,MzRear=f.RearTorque,Mz=f.NetTorque,
                        f.SteeringBefore,f.SteeringAfter,f.SteeringRequest,RateLimited=f.SteeringAfter!=f.SteeringRequest,
                        last.LoadH26,power.Wheel.LongitudinalLossJoules};
                }
                samples.Add(new {Horizon=horizon,cursor.Outcome,cursor.Sweep,End=cursor.Snapshot,Forces=mechanical});
            }
            rows.Add(new {shot.Name,Variant=variant,GasRequest=request,
                InitialGas=start.Active.Drive,Snapshot=start,FirstRequestedActivation=due,
                cursor.Counters,cursor.ContactFailure,Samples=samples});
        }
        Save(Path.Combine(output,"transients-model.json.gz"),new {Instantaneous=local,Rollouts=rows,
            Stopwatch.Frequency,ElapsedSeconds=timer.Elapsed.TotalSeconds,NativeWorlds=0},true);
    }
}
