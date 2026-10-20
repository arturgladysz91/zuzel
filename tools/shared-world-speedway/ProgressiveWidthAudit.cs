using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>Width-only experiment. #102 A evidence is read, never remeasured or overwritten.</summary>
public static class ProgressiveWidthAudit
{
    public const string BaseHead="4d65219367d8b27f51d3a5ea3ff2e1f517a6bcd1";
    public static TrackShapeVariant[] Variants()=>new[]{LocalOffsetAudit.Mild()}.Concat(TrackShapeVariant.ProgressiveWidths()).ToArray();
    private static void Save(string dir,string file,object value,bool geometry=false)
    {
        var path=Path.Combine(dir,file);if(File.Exists(path))throw new IOException("Evidence exists: "+path);
        File.WriteAllText(path,JsonSerializer.Serialize(value,geometry?ReplayGeometry.Json:LocalOffsetAudit.Json)+"\n");
    }
    public static MeterPoint Edge(ExperimentalTrackShape shape,double s)
    {
        var q=shape.At(s);return q.Position+new MeterPoint(Math.Sin(q.Heading),-Math.Cos(q.Heading))*(shape.Width(s)-1);
    }
    public sealed record BoundaryCheck(string Geometry,double MaximumSecondDerivative,double MaximumAbsoluteCurvature,
        double MinimumDerivativeLength,double MaximumJoinTangentError,double MaximumJoinCurvatureJump,
        double MaximumChordSagMeters,int NonAdjacentIntersections,double MaximumReferenceDifference);
    public static BoundaryCheck CheckBoundary(TrackShapeVariant variant)
    {
        var shape=TrackGeometryAudit.Frame(variant).Shape!;var reference=TrackGeometryAudit.Frame(LocalOffsetAudit.Mild()).Shape!;
        const double h=.001;const int count=16000;
        double second=0,curvature=0,minimumSpeed=double.PositiveInfinity,sag=0,referenceDifference=0;
        var points=new MeterPoint[count+1];
        (MeterPoint D,double K) Differential(double s){var a=Edge(shape,s-h);var b=Edge(shape,s);var c=Edge(shape,s+h);
            var d=(c-a)*(1/(2*h));var dd=(a+c-b*2)*(1/(h*h));
            return(d,MeterPoint.Cross(d,dd)/Math.Pow(d.Length,3));}
        for(var i=0;i<count;i++){
            var s=i*shape.LapMeters/count;var a=Edge(shape,s-h);var b=Edge(shape,s);var c=Edge(shape,s+h);points[i]=b;
            second=Math.Max(second,((a+c-b*2)*(1/(h*h))).Length);
            var(d,k)=Differential(s);minimumSpeed=Math.Min(minimumSpeed,d.Length);curvature=Math.Max(curvature,Math.Abs(k));
            var left=Edge(shape,s-.025);var delta=Edge(shape,s+.025)-left;
            sag=Math.Max(sag,(left+delta*Math.Clamp(MeterPoint.Dot(b-left,delta)/MeterPoint.Dot(delta,delta),0,1)-b).Length);
            var q=shape.At(s);var old=reference.At(s);
            referenceDifference=Math.Max(referenceDifference,(q.Position-old.Position).Length+Math.Abs(q.Heading-old.Heading)+Math.Abs(q.Curvature-old.Curvature));
        }
        points[^1]=points[0];double tangentError=0,curvatureJump=0;
        var joins=new[]{shape.Starts[1],(shape.Starts[1]+shape.Starts[4])/2,shape.Starts[4],shape.Starts[5],(shape.Starts[5]+shape.Starts[8])/2,shape.Starts[8],0};
        foreach(var s in joins){var l=Differential(s-.002);var r=Differential(s+.002);
            tangentError=Math.Max(tangentError,Math.Abs(MeterPoint.Cross(l.D,r.D)/(l.D.Length*r.D.Length)));
            curvatureJump=Math.Max(curvatureJump,Math.Abs(l.K-r.K));}
        // Independent whole-XY polygon crossing test: x-sweep of bounding boxes,
        // then strict segment intersection. Adjacent edges and closure share endpoints.
        var boxes=Enumerable.Range(0,count).Select(i=>new{I=i,X0=Math.Min(points[i].X,points[i+1].X),X1=Math.Max(points[i].X,points[i+1].X),
            Y0=Math.Min(points[i].Y,points[i+1].Y),Y1=Math.Max(points[i].Y,points[i+1].Y)}).OrderBy(b=>b.X0).ToArray();
        var intersections=0;
        for(var i=0;i<boxes.Length;i++)for(var j=i+1;j<boxes.Length&&boxes[j].X0<=boxes[i].X1;j++){
            var a=boxes[i];var b=boxes[j];if(Math.Abs(a.I-b.I)<=1||Math.Abs(a.I-b.I)==count-1||a.Y1<b.Y0||b.Y1<a.Y0)continue;
            var p=points[a.I];var u=points[a.I+1]-p;var q=points[b.I];var v=points[b.I+1]-q;var cross=MeterPoint.Cross(u,v);
            if(Math.Abs(cross)<1e-15)continue;var t=MeterPoint.Cross(q-p,v)/cross;var z=MeterPoint.Cross(q-p,u)/cross;
            if(t is >=0 and <=1&&z is >=0 and <=1)intersections++;
        }
        return new(variant.Name,second,curvature,minimumSpeed,tangentError,curvatureJump,sag,intersections,referenceDifference);
    }
    public sealed record IntervalState(double TimeSeconds,double ReferenceMeters,double OffsetMeters,double TargetMeters,
        double OutwardSpeed,double SlideDegrees,double Inner,double Outer,double ConservativeInner,double ConservativeOuter);
    public sealed record Evidence(LocalOffsetAudit.Measurement Measurement,double ConservativeMinimumInnerClearanceMeters,
        double MaximumAbsoluteOutwardSpeed,IntervalState? FirstOuterViolation,IntervalState WorstOuterState);
    public static Evidence Measure(TrackShapeVariant variant,LocalOffsetAudit.Plan plan,string? directory=null)
    {
        SlipControlValidation.Capture? capture=null;
        var measurement=LocalOffsetAudit.Measure(variant,plan,directory,c=>capture=c);var c=capture!;
        var g=new RacingLineGeometry(TrackGeometryAudit.Frame(variant));var previous=g.Clearance(c.Initial.Riders.Single());
        var rows=c.Ticks.Select(t=>{
            var gap=g.Clearance(t.After);var allowance=(Math.Max(t.Before.Speed,t.After.Speed)+SpeedwayBikeDimensions.Reference.BoundingRadiusMeters*
                Math.Max(Math.Abs(t.Before.AngularVelocity),Math.Abs(t.After.AngularVelocity)))*t.DurationSeconds/2+LocalOffsetAudit.IntervalNumericalAllowanceMeters;
            var row=new IntervalState(t.TimeSeconds,t.After.TotalReferenceMeters,t.After.Road.OffsetMeters,t.Before.TargetOffsetMeters,
                Vector2.Dot(t.After.Velocity,t.After.Road.Outward),t.After.SlideRadians*180/Math.PI,gap.Inner,gap.Outer,
                Math.Min(previous.Inner,gap.Inner)-allowance,Math.Min(previous.Outer,gap.Outer)-allowance);previous=gap;return row;
        }).ToArray();
        var evidence=new Evidence(measurement,rows.Min(r=>r.ConservativeInner),rows.Max(r=>Math.Abs(r.OutwardSpeed)),
            rows.FirstOrDefault(r=>r.Outer<.5),rows.MinBy(r=>r.Outer)!);
        if(evidence.ConservativeMinimumInnerClearanceMeters<0)throw new InvalidOperationException("Uncertified inner boundary: retain and explicitly reject before publication.");
        if(directory is not null){
            var path=Path.Combine(directory,variant.Name+"-"+plan.Name+"-dynamics.csv");if(File.Exists(path))throw new IOException(path);
            using var writer=new StreamWriter(path);writer.WriteLine("time_s,reference_m,offset_m,target_m,outward_speed_mps,slide_deg,inner_m,outer_m,conservative_inner_m,conservative_outer_m");
            foreach(var r in rows)writer.WriteLine(FormattableString.Invariant($"{r.TimeSeconds:R},{r.ReferenceMeters:R},{r.OffsetMeters:R},{r.TargetMeters:R},{r.OutwardSpeed:R},{r.SlideDegrees:R},{r.Inner:R},{r.Outer:R},{r.ConservativeInner:R},{r.ConservativeOuter:R}"));
        }
        return evidence;
    }
    public static void Run(string directory,string stage)
    {
        Directory.CreateDirectory(directory);
        if(stage=="freeze"){
            Save(directory,"freeze.json",new{BaseHead,Variants=Variants(),Plans=LocalOffsetAudit.Plans(),Parameters=LocalOffsetAudit.Parameters(),
                Condition=PhysicsDrivenRacingLineAudit.Conditions()[0],Initial=new{Reference=0,Offset=5,Speed=18,Drive=1,Laps=3,StepSeconds=.01},
                Interpretation="PROVISIONAL width hypothesis; 17/16.2 are assumed maxima at bend midpoints, not measured apex widths. A reused verbatim from #102. No plan tuning."});
            foreach(var v in TrackShapeVariant.ProgressiveWidths())Save(directory,v.Name+"-geometry.json",ReplayGeometry.CreateWithLocalLimit(TrackGeometryAudit.Frame(v)),true);
            Save(directory,"boundary-checks.json",Variants().Select(CheckBoundary).ToArray());return;
        }
        if(stage=="cost"){Save(directory,"performance.json",Cost());return;}
        if(stage!="measure")throw new ArgumentException("Use freeze, measure or cost.");
        using var frozen=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"freeze.json")));
        if(frozen.RootElement.GetProperty("BaseHead").GetString()!=BaseHead||
            frozen.RootElement.GetProperty("Parameters").GetRawText()!=JsonSerializer.Serialize(LocalOffsetAudit.Parameters(),LocalOffsetAudit.Json)||
            frozen.RootElement.GetProperty("Condition").GetRawText()!=JsonSerializer.Serialize(PhysicsDrivenRacingLineAudit.Conditions()[0],LocalOffsetAudit.Json)||
            frozen.RootElement.GetProperty("Plans").GetRawText()!=JsonSerializer.Serialize(LocalOffsetAudit.Plans(),LocalOffsetAudit.Json)||
            frozen.RootElement.GetProperty("Variants").GetRawText()!=JsonSerializer.Serialize(Variants(),LocalOffsetAudit.Json))throw new InvalidOperationException("Frozen inputs differ.");
        var rows=new List<Evidence>();foreach(var v in TrackShapeVariant.ProgressiveWidths())foreach(var p in LocalOffsetAudit.Plans()){
            var row=Measure(v,p,directory);rows.Add(row);var m=row.Measurement;
            Console.WriteLine($"{v.Name}/{p.Name}: {m.Evaluation.FlyingSeconds:F6}s feasible={m.Evaluation.Feasible} outer={m.Evaluation.MinimumOuterClearanceMeters:F4} intervalInner={row.ConservativeMinimumInnerClearanceMeters:F4}");}
        Save(directory,"measurements.json",rows);
        var checks=new List<object>();
        using var baseline=JsonDocument.Parse(File.ReadAllText("docs/experiments/local-offset-limit/measurements.json"));
        foreach(var p in LocalOffsetAudit.Plans()){
            var expected=baseline.RootElement.EnumerateArray().Single(r=>r.GetProperty("Geometry").GetString()=="egg-mild"&&r.GetProperty("Plan").GetProperty("Name").GetString()==p.Name).GetProperty("Evaluation");
            var result=LocalOffsetAudit.Plain(LocalOffsetAudit.Mild(),p);
            if(result.Fingerprint!=expected.GetProperty("Fingerprint").GetString())throw new InvalidOperationException("#102 A changed.");
            checks.Add(new{Geometry="A",Plan=p.Name,result.Fingerprint,MatchesFrozen102=true,result.Work.SolverCalls});
        }
        foreach(var v in TrackShapeVariant.ProgressiveWidths())foreach(var name in new[]{"inner","outer-max"}){
            var result=LocalOffsetAudit.Plain(v,LocalOffsetAudit.Plans().Single(p=>p.Name==name));
            var expected=rows.Single(r=>r.Measurement.Geometry==v.Name&&r.Measurement.Plan.Name==name).Measurement.Evaluation;
            if(result.Fingerprint!=expected.Fingerprint)throw new InvalidOperationException("Observed repeat differs.");
            checks.Add(new{Geometry=v.Name,Plan=name,result.Fingerprint,ObservedRepeat=true,result.Work.SolverCalls});}
        Save(directory,"controls.json",checks);
    }
    private static object Cost()
    {
        var rows=new List<object>();foreach(var variant in Variants()){
            // Warm geometry and cache code before the five measured independent constructions.
            _=TrackGeometryAudit.Frame(variant).LocalOffsets;
            var trials=new List<object>();double sink=0;
            for(var trial=0;trial<5;trial++){
                var bytes=GC.GetAllocatedBytesForCurrentThread();var start=Stopwatch.GetTimestamp();var frame=TrackGeometryAudit.Frame(variant);
                var geometryMs=Stopwatch.GetElapsedTime(start).TotalMilliseconds;var geometryBytes=GC.GetAllocatedBytesForCurrentThread()-bytes;
                bytes=GC.GetAllocatedBytesForCurrentThread();start=Stopwatch.GetTimestamp();var table=frame.LocalOffsets;
                var cacheMs=Stopwatch.GetElapsedTime(start).TotalMilliseconds;var cacheBytes=GC.GetAllocatedBytesForCurrentThread()-bytes;
                var points=Enumerable.Range(0,100).Select(i=>frame.At(i*3,5).Position).ToArray();
                for(var i=0;i<10000;i++){sink+=table.At(i*.031);sink+=frame.Project(points[i%100]).ReferenceMeters;}
                bytes=GC.GetAllocatedBytesForCurrentThread();start=Stopwatch.GetTimestamp();for(var i=0;i<100000;i++)sink+=table.At(i*.031);
                var queryUs=Stopwatch.GetElapsedTime(start).TotalMicroseconds/100000;var queryBytes=GC.GetAllocatedBytesForCurrentThread()-bytes;
                bytes=GC.GetAllocatedBytesForCurrentThread();start=Stopwatch.GetTimestamp();
                for(var i=0;i<10000;i++){var r=frame.Project(points[i%100]);sink+=frame.PhysicalWidthMeters(r)+frame.RoadAt(r.ReferenceMeters+10,5).Heading;}
                var previewUs=Stopwatch.GetElapsedTime(start).TotalMicroseconds/10000;
                var previewBytes=GC.GetAllocatedBytesForCurrentThread()-bytes;
                trials.Add(new{GeometryMilliseconds=geometryMs,GeometryBytes=geometryBytes,CacheMilliseconds=cacheMs,CacheBytes=cacheBytes,
                    CacheQueryMicroseconds=queryUs,CacheQueryBytes=queryBytes,ProjectWidthPreviewMicroseconds=previewUs,ProjectWidthPreviewBytes=previewBytes});
            }
            rows.Add(new{Geometry=variant.Name,Trials=trials,Sink=sink});}
        return new{Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,TieredCompilation=Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),Rows=rows};
    }
}
