using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>Replay the existing contact at saved states; never create or advance a world.</summary>
public static class NaturalRacingLineProbes
{
    private static CoupledSlipInverse.Contact Contact(Dictionary<string,double> r,Dictionary<string,double> f)
    {
        var v=new Vector2((float)r["vx"],(float)r["vy"]);var speed=v.Length();
        var resistance=-v*(LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons(speed)/MathF.Sqrt(speed*speed+.25f));
        return new(v,resistance,(float)r["budget"],(float)f["mass"],f["omega_before"],f["requested_engine_torque"],f["available_engine_torque"],
            f["delivered_engine_torque"],(float)r["dt"],RearWheelParameters.Default);
    }
    private static Dictionary<double,Dictionary<string,double>> Read(string file)
    {
        using var zip=new GZipStream(File.OpenRead(file),CompressionMode.Decompress);using var reader=new StreamReader(zip);
        var header=reader.ReadLine()!.Split(',');var rows=new Dictionary<double,Dictionary<string,double>>();
        while(reader.ReadLine() is { } line){var a=line.Split(',');var row=header.Select((h,i)=>(h,double.Parse(a[i],CultureInfo.InvariantCulture))).ToDictionary(p=>p.h,p=>p.Item2);rows.Add(row["t"],row);}
        return rows;
    }
    public static void Run(string dir)
    {
        using var analysis=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"analysis.json")));var probes=new List<object>();var prefixes=new List<object>();
        foreach(var c in analysis.RootElement.GetProperty("Cases").EnumerateArray()){
            var name=c.GetProperty("Name").GetString()!;var source=c.GetProperty("Natural").GetBoolean()?dir:NaturalRacingLineAudit.HistoricalDirectory;
            var rows=Read(Path.Combine(source,name+"-tracking.csv.gz"));var forces=Read(Path.Combine(source,name+"-forces.csv.gz"));
            var through=c.GetProperty("Events").GetProperty("TrackingAbove1m").GetProperty("BracketStart").GetDouble();
            object? firstPath=null,firstController=null;var queries=0;var states=0;
            foreach(var(time,r) in rows.Where(p=>p.Key<=through)){
                var f=forces[time];var contact=Contact(r,f);states++;
                var path=CoupledSlipInverse.Solve(contact,f["mass"]*r["speed"]*r["speed"]*r["path_curvature"]);
                var controller=CoupledSlipInverse.Solve(contact,f["mass"]*r["steering_demand"]);queries+=path.ContactEvaluations+controller.ContactEvaluations;
                object State(SlipInverseResult inverse)=>new{Time=time,Reference=r["s"],SpeedKph=r["speed"]*3.6,LateralError=r["lateral_error"],Inverse=inverse};
                if(firstPath is null&&path.Status==SlipInverseStatus.Unreachable)firstPath=State(path);
                if(firstController is null&&controller.Status==SlipInverseStatus.Unreachable)firstController=State(controller);
            }
            prefixes.Add(new{Name=name,ThroughSeconds=through,States=states,ContactQueries=queries,FirstUnreachablePath=firstPath,FirstUnreachableController=firstController});
            foreach(var key in new[]{"TrackingAbove1m","PathAboveTotalBudget","OuterCrossed"}){
                var time=c.GetProperty("Events").GetProperty(key).GetProperty("BracketStart").GetDouble();var r=rows[time];var f=forces[time];
                var velocity=new Vector2((float)r["vx"],(float)r["vy"]);var speed=velocity.Length();
                var contact=Contact(r,f);
                var actualBeta=(float)BikeAngles.Wrap(r["body_heading"]-Math.Atan2(velocity.Y,velocity.X));
                var targetBeta=(float)BikeAngles.Wrap(actualBeta+r["yaw_error"]);
                var pathRequired=f["mass"]*r["speed"]*r["speed"]*r["path_curvature"];
                var controllerRequired=f["mass"]*r["steering_demand"];
                var pathInverse=CoupledSlipInverse.Solve(contact,pathRequired);
                var controllerInverse=CoupledSlipInverse.Solve(contact,controllerRequired);
                var current=contact.NormalForce(actualBeta);var legacyTarget=contact.NormalForce(targetBeta);
                // Replay uses the exact frozen law and torque of THIS recorded contact.
                // It proves an instantaneous force root only, not yaw reachability or a safe lap.
                probes.Add(new{Name=name,Event=key,Time=time,Reference=r["s"],SpeedKph=r["speed"]*3.6,
                    ActualBetaDegrees=actualBeta*180/Math.PI,LegacyTargetBetaDegrees=targetBeta*180/Math.PI,
                    PathRequiredNewtons=pathRequired,ControllerRequiredNewtons=controllerRequired,
                    ActualNormalNewtons=current,RecordedNormalAtInitialVelocity=Vector2.Dot(new((float)f["force_x"],(float)f["force_y"]),new Vector2(-velocity.Y,velocity.X)/speed),
                    NormalAtLegacyTargetNewtons=legacyTarget,PathInverse=pathInverse,ControllerInverse=controllerInverse,
                    ContactQueries=2+pathInverse.ContactEvaluations+controllerInverse.ContactEvaluations});
            }
        }
        InverseFeasibilityAudit.Save(dir,"fixed-state-probes.json",new{NewBodySteps=0,WheelCommits=0,ControllerEnabled=false,
            Rule="Offline queries of original #108 contact inverse; original saved wheel, torques and timestep. Never applied to simulation. A contact root does not prove a dynamically reachable beta or a safe trajectory.",Prefixes=prefixes,Probes=probes});
    }
}
