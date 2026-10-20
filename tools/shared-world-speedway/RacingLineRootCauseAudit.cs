using System.Globalization;
using System.Numerics;
using System.Text.Json;
using CoreSim;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

public static class RacingLineRootCauseAudit
{
    public const string BaseHead="9ae0117e90c8076634c8838d9cfee59111ed9ef7";
    public const double GeometrySpacingMeters=.05, InitialReferenceMeters=4, TrackingRmsMeters=.5, TrackingMaximumMeters=1;
    private static readonly JsonSerializerOptions Json=new(Audit.Json){WriteIndented=false};
    public static TrackShapeVariant Variant()=>TrackShapeVariant.ProgressiveWidths()[0];
    public static TrackFrame Frame()=>TrackGeometryAudit.Frame(Variant());
    public static RiderSpec Rider(AuthoredRacingPath path)=>PhysicsDrivenRacingLineAudit.Rider(PhysicsDrivenRacingLineAudit.Conditions()[0])
        with{ReferenceMeters=InitialReferenceMeters,OffsetMeters=(float)path.Offset(InitialReferenceMeters).D,TargetOffsetMeters=(float)path.Offset(InitialReferenceMeters).D};
    public static float MassKg=>Rider(new("inner")).Profile.Gameplay.Physical.MassKg+(float)new PhysicalContactParameters().ReferenceBikeMassKg;
    public static float AccelerationBudget()
    {
        var rider=Rider(new("inner"));var safe=SegmentPhysics.MaxSafeTurnSpeedForRadius(31,new(1,0,.5f),rider.Profile.Skills,rider.Setup);
        return safe*safe/31;
    }
    private static void Save(string dir,string name,object value)
    {
        var p=Path.Combine(dir,name);if(File.Exists(p))throw new IOException("Evidence exists: "+p);
        File.WriteAllText(p,JsonSerializer.Serialize(value,Json)+"\n");
    }
    private static StreamWriter Csv(string dir,string name,string header,bool compress=false)
    {
        var p=Path.Combine(dir,name);if(File.Exists(p))throw new IOException("Evidence exists: "+p);
        var stream=File.Create(p);var writer=new StreamWriter(compress?new System.IO.Compression.GZipStream(stream,System.IO.Compression.CompressionLevel.SmallestSize):stream);writer.WriteLine(header);return writer;
    }
    private static void Write(StreamWriter writer,params double[] values)=>writer.WriteLine(string.Join(',',values.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))));
    public static (double PreviewCurvature,double Geometry,double Offset,double Damping) ExistingDemand(RiderView v,TrackFrame f,WorldParameters p)
    {
        var road=v.Road;var ahead=Math.Max(.1,f.ReferenceSpeed(road,v.Speed)*2*p.YawResponseSeconds);
        var angle=BikeAngles.Wrap(f.RoadAt(v.TotalReferenceMeters+ahead,v.TargetOffsetMeters).Heading-road.Heading);
        var curvature=angle/(ahead+Math.Abs(angle)*Math.Max(0,road.OffsetMeters));
        var longitudinal=Vector2.Dot(v.Velocity,road.Tangent);var response=p.LateralResponseSeconds+4*p.SlipYawResponseSeconds;
        return(curvature,longitudinal*longitudinal*curvature,((float)road.OffsetMeters-v.TargetOffsetMeters)/(response*response),2*Vector2.Dot(v.Velocity,road.Outward)/response);
    }
    public static object ExportGeometry(string directory,string name)
        =>ExportGeometry(directory,new AuthoredRacingPath(name));
    public static object ExportGeometry(string directory,AuthoredRacingPath path)
    {
        var name=path.Name;var f=path.Frame;var g=new RacingLineGeometry(f);var n=(int)Math.Ceiling(f.LapMeters/GeometrySpacingMeters);
        var ds=f.LapMeters/n;var maxMetric=0d;var minInner=double.PositiveInfinity;var minOuter=double.PositiveInfinity;var maxK=0d;double length=0;
        using var writer=Csv(directory,name+"-geometry.csv","s,x,y,offset,offset_first,offset_second,metric,heading,curvature,ay_at18,speed_cap,old_perfect_demand_at18,old_geometry_at18,old_damping_at18,inner_circle,outer_circle");
        var p=LocalOffsetAudit.Parameters();var accelerationBudget=AccelerationBudget();
        for(var i=0;i<=n;i++){
            var q=path.At(i*ds);maxMetric=Math.Max(maxMetric,q.Metric);maxK=Math.Max(maxK,Math.Abs(q.Curvature));
            var inner=g.InnerPointDistance(q.Position)-SpeedwayBikeDimensions.Reference.BoundingRadiusMeters;
            var outer=g.OuterPointDistance(q.Position)-SpeedwayBikeDimensions.Reference.BoundingRadiusMeters;
            minInner=Math.Min(minInner,inner);minOuter=Math.Min(minOuter,outer);
            if(i<n)length+=path.At((i+.5)*ds).Metric*ds;
            var v=new RiderView(1,new((float)q.Position.X,(float)q.Position.Y),new((float)(18*q.Tangent.X),(float)(18*q.Tangent.Y)),(float)q.Heading,0,
                f.RoadAt(q.ReferenceMeters,q.OffsetMeters),q.ReferenceMeters,0,RiderActivity.Racing,1,(float)q.OffsetMeters,0);
            var old=ExistingDemand(v,f,p);
            Write(writer,q.ReferenceMeters,q.Position.X,q.Position.Y,q.OffsetMeters,q.OffsetFirst,q.OffsetSecond,q.Metric,q.Heading,q.Curvature,
                18*18*Math.Abs(q.Curvature),Math.Sqrt(accelerationBudget/Math.Max(1e-12,Math.Abs(q.Curvature))),old.Geometry+old.Offset+old.Damping,old.Geometry,old.Damping,inner,outer);
        }
        // Distance to the entire fixed edge is 1-Lipschitz; bound movement between samples.
        var intervalAllowance=maxMetric*ds/2+.0001;
        return new{Name=name,ReferenceLapMeters=f.LapMeters,LengthMeters=length,MaximumAbsoluteCurvature=maxK,MinimumRadiusMeters=1/maxK,
            MaximumAyAt18=324*maxK,MinimumTractionSpeed=Math.Sqrt(accelerationBudget/maxK),MaximumMetric=maxMetric,
            MinimumInnerCircle=minInner,MinimumOuterCircle=minOuter,ConservativeInnerCircle=minInner-intervalAllowance,ConservativeOuterCircle=minOuter-intervalAllowance,
            MaximumReferenceSpacing=ds,GeometricallySafe=minInner-intervalAllowance>=.05&&minOuter-intervalAllowance>=.5};
    }
    public static (SlipControlValidation.Capture Capture,RearWheelObservation[] Wheels) Execute(string name,bool diagnostic,string? csv=null,bool preview=false)
    {
        var path=new AuthoredRacingPath(name);var f=path.Frame;var wheels=new List<RearWheelObservation>();
        var scenario=new Scenario(name+(preview?"-path-preview":diagnostic?"-path-frame":"-existing"),new[]{Rider(path)},90,3);
        var capture=SlipControlValidation.Execute(scenario,LocalOffsetAudit.Parameters(),csvPath:csv,
            surface:TrackState.CreateDefault(f.Track,new(1,0,.5f)).Snapshot(),wheelObserver:wheels.Add,trackShape:Variant(),authoredPath:path,diagnosticPathGuidance:diagnostic,diagnosticPathPreview:preview);
        return(capture,wheels.ToArray());
    }
    public static object ExportRun(string directory,string name,bool diagnostic)
    {
        var prefix=name+(diagnostic?"-path-frame":"-existing");
        RequireNewCase(directory,prefix);
        var(c,w)=Execute(name,diagnostic,Path.Combine(directory,prefix+"-accepted.csv"));
        return ExportCapture(directory,prefix,c,w,new(name));
    }
    private static void RequireNewCase(string directory,string prefix)
    {
        foreach(var suffix in new[]{"-accepted.csv","-tracking.csv",".json"})
            if(File.Exists(Path.Combine(directory,prefix+suffix)))throw new IOException("Evidence exists: "+prefix+suffix);
    }
    public static object ExportCapture(string directory,string prefix,SlipControlValidation.Capture c,RearWheelObservation[] wheels,AuthoredRacingPath? path,bool? diagnosticPathFrame=null,int requiredLaps=3,bool compressTracking=false)
    {
        var f=Frame();var g=new RacingLineGeometry(f);var previous=g.Clearance(c.Initial.Riders.Single());
        double minInner=previous.Inner,minOuter=previous.Outer,minIntervalInner=double.PositiveInfinity,minIntervalOuter=double.PositiveInfinity;
        var ws=wheels.ToDictionary(w=>Math.Round(w.TimeSeconds,8));
        using var writer=Csv(directory,prefix+"-tracking.csv"+(compressTracking?".gz":""),"t,dt,s,x,y,vx,vy,speed,offset,target,path_x,path_y,path_heading,path_curvature,lateral_error,longitudinal_error,body_heading,velocity_heading,velocity_heading_error,body_heading_error,actual_curvature,fx,fy,budget,utilization,wheelspin,yaw_requested,yaw_authority,yaw_energy,yaw_error,angular_velocity,steering_demand,old_preview_curvature,old_geometry_demand,old_offset_demand,old_damping_demand,required_path_accel,inner,outer,interval_inner,interval_outer",compressTracking);
        foreach(var tick in c.Ticks){var v=tick.Before;var q=path?.At(v.TotalReferenceMeters);var goal=q?.Position??new MeterPoint(v.Position.X,v.Position.Y);
            var heading=q?.Heading??v.Road.Heading;var tangent=new MeterPoint(Math.Cos(heading),Math.Sin(heading));var outward=new MeterPoint(tangent.Y,-tangent.X);
            var delta=new MeterPoint(v.Position.X,v.Position.Y)-goal;var travel=Math.Atan2(v.Velocity.Y,v.Velocity.X);
            var end=tick.After;var endTravel=Math.Atan2(end.Velocity.Y,end.Velocity.X);
            var actualCurvature=BikeAngles.Wrap(endTravel-travel)/(Math.Max(.001,(v.Speed+end.Speed)/2)*tick.DurationSeconds);
            var gap=g.Clearance(end);var allowance=(Math.Max(v.Speed,end.Speed)+1.05*Math.Max(Math.Abs(v.AngularVelocity),Math.Abs(end.AngularVelocity)))*tick.DurationSeconds/2+.012;
            var intervalInner=Math.Min(previous.Inner,gap.Inner)-allowance;var intervalOuter=Math.Min(previous.Outer,gap.Outer)-allowance;previous=gap;
            minInner=Math.Min(minInner,gap.Inner);minOuter=Math.Min(minOuter,gap.Outer);minIntervalInner=Math.Min(minIntervalInner,intervalInner);minIntervalOuter=Math.Min(minIntervalOuter,intervalOuter);
            var old=ExistingDemand(v,f,LocalOffsetAudit.Parameters());var spin=ws[Math.Round(tick.TimeSeconds,8)].Step.InstantSlipRatio;
            Write(writer,tick.TimeSeconds,tick.DurationSeconds,v.TotalReferenceMeters,v.Position.X,v.Position.Y,v.Velocity.X,v.Velocity.Y,v.Speed,v.Road.OffsetMeters,v.TargetOffsetMeters,
                goal.X,goal.Y,heading,q?.Curvature??double.NaN,MeterPoint.Dot(delta,outward),MeterPoint.Dot(delta,tangent),v.Heading,travel,
                BikeAngles.Wrap(travel-heading),BikeAngles.Wrap(v.Heading-heading),actualCurvature,tick.Slip.DriveForceNewtons,tick.Slip.LateralForceNewtons,
                tick.Slip.GripBudgetNewtons,tick.Slip.GripUtilization,spin,tick.Yaw.RequestedNewtonMeters,tick.Yaw.AfterAuthorityNewtonMeters,tick.Yaw.AfterEnergyNewtonMeters,
                tick.Yaw.OrientationErrorRadians,v.AngularVelocity,tick.Force.DesiredInwardNewtons/c.Mechanical[1].MassKg,old.PreviewCurvature,old.Geometry,old.Offset,old.Damping,
                v.Speed*v.Speed*(q?.Curvature??double.NaN),gap.Inner,gap.Outer,intervalInner,intervalOuter);
        }
        var crossing=new LapCrossings();var origin=c.Initial.Riders.Single().TotalReferenceMeters;
        foreach(var t in c.Ticks)crossing.Accept(t.TimeSeconds,t.Before.TotalReferenceMeters-origin,t.TimeSeconds+t.DurationSeconds,t.After.TotalReferenceMeters-origin,f.LapMeters,requiredLaps);
        var times=crossing.Times.ToArray();
        var summary=new{Case=prefix,AuthoredPath=path?.Name,DiagnosticPathFrame=diagnosticPathFrame??(prefix.EndsWith("path-frame",StringComparison.Ordinal)||prefix.EndsWith("path-preview",StringComparison.Ordinal)),Origin=origin,
            c.Initial,Result=c.Result,CrossingSeconds=times,LapSeconds=times.Select((t,i)=>t-(i==0?0:times[i-1])).ToArray(),
            MinimumInner=minInner,MinimumOuter=minOuter,ConservativeInner=minIntervalInner,ConservativeOuter=minIntervalOuter,
            BoundarySafe=minInner>=.05&&minOuter>=.5&&minIntervalInner>=0&&minIntervalOuter>=.5,
            Energy=RearWheelAudit.Energy(new(c,wheels)),MaximumUtilization=c.Ticks.Max(t=>t.Slip.GripUtilization)};
        Save(directory,prefix+".json",summary);return summary;
    }
    public static void Run(string directory,string stage)
    {
        Directory.CreateDirectory(directory);
        if(stage=="preview-freeze"){
            Save(directory,"preview-freeze.json",new{BaseHead,Paths=new[]{"inner","outside-inside-outside"},
                Reason="Current-curvature-only diagnostic degraded even inner. Preserve it; restore the exact historical 2*YawResponse reference lookahead, full path heading difference/arc length, same gains and tyre. No path or parameter fit.",
                Parameters=LocalOffsetAudit.Parameters(),OriginalRunsSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(directory,"runs.json"))))});return;
        }
        if(stage=="freeze"){
            Save(directory,"freeze.json",new{BaseHead,Geometry=Variant(),Paths=AuthoredRacingPath.Names().Select(n=>new{Name=n,ApexFraction=n=="late-apex"?.65:.5}),
                AuthoredRacingPath.OutsideMeters,AuthoredRacingPath.ApexMeters,AuthoredRacingPath.PreparationMeters,GeometrySpacingMeters,InitialReferenceMeters,
                InitialSpeed=18,Drive=1,Laps=3,StepSeconds=.01,Parameters=LocalOffsetAudit.Parameters(),MassKg,AccelerationBudget=AccelerationBudget(),
                TrackingRmsMeters,TrackingMaximumMeters,VelocityHeadingRmsDegrees=3,VelocityHeadingMaximumDegrees=10,
                Rule="Symmetry selects midpoint apex; late .65 is a fixed sensitivity, no timing/force fit. Four C2 paths x existing/path-frame, plus one legacy wide telemetry recapture. Wheelspin comparison gated on geometry AND existing tracking pass.",
                Diagnostic="Same controller gains/steer clamp/tyre/drive/yaw, current full-path curvature and path tangent/normal errors. Default absent. No position edits."});return;
        }
        using var freeze=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"freeze.json")));
        if(freeze.RootElement.GetProperty("BaseHead").GetString()!=BaseHead||freeze.RootElement.GetProperty("Parameters").GetRawText()!=JsonSerializer.Serialize(LocalOffsetAudit.Parameters(),Json)
            ||freeze.RootElement.GetProperty("Geometry").GetRawText()!=JsonSerializer.Serialize(Variant(),Json)
            ||freeze.RootElement.GetProperty("OutsideMeters").GetDouble()!=AuthoredRacingPath.OutsideMeters
            ||freeze.RootElement.GetProperty("ApexMeters").GetDouble()!=AuthoredRacingPath.ApexMeters
            ||freeze.RootElement.GetProperty("PreparationMeters").GetDouble()!=AuthoredRacingPath.PreparationMeters
            ||freeze.RootElement.GetProperty("Paths").GetRawText()!=JsonSerializer.Serialize(AuthoredRacingPath.Names().Select(n=>new{Name=n,ApexFraction=n=="late-apex"?.65:.5}),Json))
            throw new InvalidOperationException("Frozen inputs differ.");
        if(stage=="geometry"){
            Save(directory,"geometry-summary.json",AuthoredRacingPath.Names().Select(n=>ExportGeometry(directory,n)).ToArray());
            var rider=Rider(new("inner"));var force=new List<object>();
            foreach(var turn in new[]{false,true})for(var i=0;i<=1000;i++){
                var speed=i*.05f;var surface=new TrackSurfaceState(1,0,.5f);
                var reference=turn?LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(rider.Profile.Skills,rider.Setup,surface):LongitudinalDynamics.CalculateStraightAvailableDriveForceNewtons(rider.Profile.Skills,rider.Setup,surface);
                force.Add(new{Turn=turn,Speed=speed,AvailableDrive=LongitudinalDynamics.CalculateAvailableDriveForceAtSpeedNewtons(reference,speed,rider.Setup),
                    Resistance=LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons(speed)*speed/MathF.Sqrt(speed*speed+.25f)});
            }
            Save(directory,"frozen-drive-envelope.json",force);return;
        }
        if(stage=="preview"){
            using var probe=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"preview-freeze.json")));
            var sha=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(directory,"runs.json"))));
            if(probe.RootElement.GetProperty("OriginalRunsSha256").GetString()!=sha)throw new InvalidOperationException("Original evidence changed.");
            var probes=new List<object>();foreach(var n in new[]{"inner","outside-inside-outside"}){
                var prefix=n+"-path-preview";RequireNewCase(directory,prefix);var(data,wheels)=Execute(n,true,Path.Combine(directory,prefix+"-accepted.csv"),true);
                probes.Add(ExportCapture(directory,prefix,data,wheels,new(n)));Console.WriteLine("Captured "+prefix);}
            Save(directory,"preview-runs.json",probes);return;
        }
        if(stage=="verify"){
            using var previous=JsonDocument.Parse(File.ReadAllText("docs/experiments/progressive-bend-width/measurements.json"));var checks=new List<object>();
            foreach(var controlPlan in LocalOffsetAudit.Plans()){
                var frozenFp=previous.RootElement.EnumerateArray().Single(r=>r.GetProperty("Measurement").GetProperty("Geometry").GetString()==Variant().Name&&r.GetProperty("Measurement").GetProperty("Plan").GetProperty("Name").GetString()==controlPlan.Name)
                    .GetProperty("Measurement").GetProperty("Evaluation").GetProperty("Fingerprint").GetString();
                var actual=LocalOffsetAudit.Plain(Variant(),controlPlan).Fingerprint;
                if(frozenFp!=actual)throw new InvalidOperationException("Historical #103 fingerprint changed.");checks.Add(new{Case=controlPlan.Name,Expected=frozenFp,Actual=actual});
            }
            foreach(var preview in new[]{false,true}){
                var path=new AuthoredRacingPath("outside-inside-outside");
                using var world=new WorldRunner(new[]{Rider(path)},parameters:LocalOffsetAudit.Parameters(),trackShape:Variant(),authoredPath:path,
                    diagnosticPathGuidance:preview,diagnosticPathPreview:preview,frozenSurface:TrackState.CreateDefault(path.Frame.Track,new(1,0,.5f)).Snapshot());
                var actual=world.Run("plain-repeat",.01,90,3,lapsFromInitialPosition:true).Fingerprint;
                var prefix=path.Name+(preview?"-path-preview":"-existing");using var saved=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,prefix+".json")));
                var observedFp=saved.RootElement.GetProperty("Result").GetProperty("Fingerprint").GetString();
                if(observedFp!=actual)throw new InvalidOperationException("Observed repeat differs.");checks.Add(new{Case=prefix,Expected=observedFp,Actual=actual});
            }
            Save(directory,"verification.json",checks);return;
        }
        if(stage!="measure")throw new ArgumentException("Use freeze, geometry, measure, preview-freeze, preview or verify.");
        var rows=new List<object>();foreach(var n in AuthoredRacingPath.Names())foreach(var diagnostic in new[]{false,true}){
            var row=ExportRun(directory,n,diagnostic);rows.Add(row);Console.WriteLine($"Captured {n}/{(diagnostic?"path-frame":"existing")}");}
        var w=new List<RearWheelObservation>();
        var plan=LocalOffsetAudit.Plans().Single(p=>p.Name=="wide-tighten");
        var(original,c)=PhysicsDrivenRacingLineAudit.Evaluate(PhysicsDrivenRacingLineAudit.Conditions()[0],TrackGeometryAudit.Plans().Single(p=>p.Name=="wide-tighten"),
            parameters:LocalOffsetAudit.Parameters(false),wheelObserver:w.Add,trackShape:Variant(),authoredControls:LocalOffsetAudit.Compile(plan,Frame()));
        rows.Add(ExportCapture(directory,"legacy-wide-tighten",c,w.ToArray(),null));
        Save(directory,"runs.json",rows);
        using var frozenLegacy=JsonDocument.Parse(File.ReadAllText("docs/experiments/progressive-bend-width/measurements.json"));
        var expected=frozenLegacy.RootElement.EnumerateArray().Single(e=>e.GetProperty("Measurement").GetProperty("Geometry").GetString()=="progressive-b"&&e.GetProperty("Measurement").GetProperty("Plan").GetProperty("Name").GetString()=="wide-tighten")
            .GetProperty("Measurement").GetProperty("Evaluation").GetProperty("Fingerprint").GetString();
        if(c.Result.Fingerprint!=expected)throw new InvalidOperationException("#103 control changed.");
        Save(directory,"control.json",new{Expected=expected,Actual=c.Result.Fingerprint,Matches=true});
    }
}
