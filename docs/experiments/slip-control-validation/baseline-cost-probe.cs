// Compile this unchanged probe in isolated #97 and #98 tool trees. No observers.
using System.Diagnostics;
using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;

public static class ValidationCostProbe
{
    public static void Run(string destination)
    {
        var spec=SlipDynamicsAudit.Rider(reference:65);var p=new WorldParameters{EnableSlipCoupledDynamics=true};
        using var world=new WorldRunner(new[]{spec},parameters:p);
        var state=world.Snapshot.Riders[0] with{Heading=world.Snapshot.Riders[0].Heading+.12f};
        var surface=TrackState.CreateDefault(world.Frame.Track).Snapshot();var props=world.MechanicalProperties(1);
        float sink=0;const int count=100000;
        for(var i=0;i<10000;i++)sink+=RiderController.Forces(state,spec,world.Frame,surface,p,1,props.MassKg,props.InertiaKgMetersSquared).Torque;
        var times=new double[7];var allocations=new long[7];
        for(var b=0;b<7;b++) {
            var bytes=GC.GetAllocatedBytesForCurrentThread();var start=Stopwatch.GetTimestamp();
            for(var i=0;i<count;i++)sink+=RiderController.Forces(state,spec,world.Frame,surface,p,1,props.MassKg,props.InertiaKgMetersSquared).Torque;
            times[b]=Stopwatch.GetElapsedTime(start).TotalMilliseconds;allocations[b]=GC.GetAllocatedBytesForCurrentThread()-bytes;
        }
        var runs=new List<object>();
        for(var b=-1;b<7;b++) {
            var start=Stopwatch.GetTimestamp();
            using var w=new WorldRunner(new[]{SlipDynamicsAudit.Rider(2,0,18,1),SlipDynamicsAudit.Rider(4,4,18,2),
                SlipDynamicsAudit.Rider(6,8,18,3),SlipDynamicsAudit.Rider(8,12,18,4)},parameters:p);
            var r=w.Run("D1-four-laps",.01,90,4,lapsFromInitialPosition:true);
            var elapsed=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            if(b>=0)runs.Add(new{Milliseconds=elapsed,r.Fingerprint,r.Work.SolverCalls});
        }
        File.WriteAllText(destination,JsonSerializer.Serialize(new{Calls=count,Milliseconds=times,AllocatedBytes=allocations,Worlds=runs,Sink=sink,
            Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            TieredCompilation=Environment.GetEnvironmentVariable("DOTNET_TieredCompilation")},Audit.Json)+"\n");
    }
}
