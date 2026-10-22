using System.Numerics;
using System.Text.Json;
using System.Globalization;
using System.IO.Compression;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>Read-only reproduction of the three captures. No WorldRunner or wheel commits.</summary>
public static class PreparedCornerReplay
{
    private static Dictionary<string,double>[] Read(string dir,string name){var file=Path.Combine(dir,name+".csv");
        using var stream=File.OpenRead(File.Exists(file)?file:file+".gz");using var reader=new StreamReader(File.Exists(file)?stream:new GZipStream(stream,CompressionMode.Decompress));
        var keys=reader.ReadLine()!.Split(',');var result=new List<Dictionary<string,double>>();
        while(reader.ReadLine() is { } line)result.Add(line.Split(',').Select((value,i)=>(keys[i],double.Parse(value,CultureInfo.InvariantCulture))).ToDictionary(x=>x.Item1,x=>x.Item2));return result.ToArray();}
    public static void Run(string dir)
    {
        var all=new List<object>();
        foreach(var name in new[]{"A","B","C"}){
            var options=new PreparedCornerOptions(name!="B",name!="A");var plan=PreparedCornerAudit.InitialPlan(options);
            var frame=plan.Path.Frame;var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
            var rows=Read(dir,name);var controls=Read(dir,name+"-control");
            using var saved=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,name+".json")));
            var initial=saved.RootElement.GetProperty("Initial");var final=saved.RootElement.GetProperty("Result").GetProperty("Riders")[0].Deserialize<RiderView>(InverseFeasibilityAudit.Json);
            double maximum=0;var states=new List<RiderView>();
            using(var writer=InverseFeasibilityAudit.Csv(dir,name+"-observed.csv","t,s,offset,road_heading,outward_speed,plan_offset_error,required_path_normal,required_controller_normal"))
            for(var i=0;i<rows.Length;i++){
                var row=rows[i];var v=PhysicalSteeringAudit.View(row,frame) with{TargetOffsetMeters=(float)controls[i]["target"]};states.Add(v);
                var f=TwoContactBikeDynamics.Forces(v,CornerEntryAudit.Rider(),frame,surface,PreparedCornerAudit.Parameters(),row["t"],147,new(row["omega_before"]*.33),new((float)row["steer_before"]),
                    (float)row["dt"],plan.Guidance(v,PreparedCornerAudit.Parameters(),row["t"]),inertia:52.72097f);
                maximum=Math.Max(maximum,new[]{Math.Abs(f.FrontForce.X-row["front_fx"]),Math.Abs(f.FrontForce.Y-row["front_fy"]),Math.Abs(f.RearForce.X-row["rear_fx"]),Math.Abs(f.RearForce.Y-row["rear_fy"]),Math.Abs(f.SteeringAfter-row["steer_after"])}.Max());
                var target=options.Path?plan.Path.At(v.TotalReferenceMeters).OffsetMeters:5;
                var k=options.Path?plan.Path.At(v.TotalReferenceMeters).Curvature:frame.Shape!.At(v.TotalReferenceMeters).Curvature/(1+5*frame.Shape.At(v.TotalReferenceMeters).Curvature);
                InverseFeasibilityAudit.Row(writer,row["t"],v.TotalReferenceMeters,v.Road.OffsetMeters,v.Road.Heading,Vector2.Dot(v.Velocity,v.Road.Outward),v.Road.OffsetMeters-target,
                    147*v.Speed*v.Speed*Math.Abs(k),147*f.Demand);
            }
            var reviews=JsonSerializer.Deserialize<PreparedCornerReview[]>(File.ReadAllText(Path.Combine(dir,name+"-reviews.json")),InverseFeasibilityAudit.Json)!;
            var policy=PreparedCornerAudit.InitialPlan(options);var mismatches=0;double profileRounding=0;
            foreach(var expected in reviews){var i=Enumerable.Range(0,rows.Length).MinBy(i=>Math.Abs(rows[i]["t"]-expected.Time));var row=rows[i];
                if(Math.Abs(row["t"]-expected.Time)>1e-8)throw new InvalidOperationException("Review not at accepted state.");
                var d=policy.Observe(states[i],CornerEntryAudit.Rider(),frame,surface,PreparedCornerAudit.Parameters(),expected.Time,147,52.72097f,new(row["omega_before"]*.33),new((float)row["steer_before"]));
                if(JsonSerializer.Serialize(d with{Profile=Array.Empty<PreparedSpeedPoint>()},InverseFeasibilityAudit.Json)!=JsonSerializer.Serialize(expected with{Profile=Array.Empty<PreparedSpeedPoint>()},InverseFeasibilityAudit.Json))mismatches++;
                for(var j=0;j<d.Profile.Length;j++)profileRounding=Math.Max(profileRounding,Math.Abs(d.Profile[j].CoastUpperBound-expected.Profile[j].CoastUpperBound));
            }
            if(maximum!=0||mismatches!=0||profileRounding>1e-12)throw new InvalidOperationException($"Replay changed: {name}, force={maximum:R}, decisions={mismatches}");
            var exit=frame.Shape!.At(plan.BendExitMeters);var delta=new MeterPoint(final.Position.X,final.Position.Y)-exit.Position;
            var exitPlane=MeterPoint.Dot(delta,new(Math.Cos(exit.Heading),Math.Sin(exit.Heading)));
            double travelTurn=0;var headings=rows.Select(r=>Math.Atan2(r["vy"],r["vx"])).Append(Math.Atan2(final.Velocity.Y,final.Velocity.X)).ToArray();
            for(var i=1;i<headings.Length;i++)travelTurn+=BikeAngles.Wrap(headings[i]-headings[i-1]);
            all.Add(new{Name=name,Worlds=0,Commits=0,States=rows.Length,MaximumForceOrSteerDifference=maximum,Reviews=reviews.Length,ChangedReviews=mismatches,
                MaximumCoastUpperBoundRounding=profileRounding,ExitPlaneMeters=exitPlane,TravelDirectionTurnRadians=travelTurn,RealExit=exitPlane>0&&travelTurn>2.8,
                Initial=initial.Clone(),Final=final,Rule="Actual XY exit plane and accumulated travel direction, independent of reference completion counter."});
        }
        InverseFeasibilityAudit.Save(dir,"replay.json",all);
    }
}
