using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using CoreSim;
using CoreSim.Analysis;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>Frozen, bounded geometry experiment. The body/wheel/driver equations are reused unchanged.</summary>
public static class TrackGeometryAudit
{
    public const string BaseHead="e2498f457e0eab672f3ada8008f4329efd2d701c";
    private static readonly JsonSerializerOptions Json=new(Audit.Json){WriteIndented=false};
    public static RacingLinePlan[] Plans()=>new[]{PhysicsDrivenRacingLine.Plans().Single(p=>p.Name=="inner"),
        PhysicsDrivenRacingLine.Plans().Single(p=>p.Name=="middle-tighten"),PhysicsDrivenRacingLine.Plans().Single(p=>p.Name=="wide-tighten"),
        new RacingLinePlan("speedway-wide-entry",9,9,2,7,15,0,.5,.85),new("speedway-open-exit",9,6,1,9.5f,15,0,.4,.65)};
    public static TrackFrame Frame(TrackShapeVariant? variant)=>new(MatchedVenueProfiles.CreateMotoarenaStandingStartTrack(),variant);
    private static void Save(string dir,string file,object value){var path=Path.Combine(dir,file);if(File.Exists(path))throw new IOException("Evidence exists: "+path);File.WriteAllText(path,JsonSerializer.Serialize(value,file.EndsWith("-geometry.json",StringComparison.Ordinal)?ReplayGeometry.Json:Json)+"\n");}
    public static string Name(TrackShapeVariant? v)=>v?.Name??"baseline";
    public static TrackShapeVariant?[] Shapes()=>new TrackShapeVariant?[]{null}.Concat(TrackShapeVariant.All()).ToArray();
    public static void Run(string directory,string stage)
    {
        Directory.CreateDirectory(directory);
        if(stage=="export"){
            foreach(var v in Shapes())File.WriteAllText(Path.Combine(directory,Name(v)+"-geometry.json"),JsonSerializer.Serialize(ReplayGeometry.Create(Frame(v)),ReplayGeometry.Json)+"\n");
            return;
        }
        if(stage=="verify"){Verify(directory);return;}
        if(stage=="freeze"){
            Save(directory,"freeze.json",new{BaseHead,Variants=Shapes(),Plans=Plans(),Condition=PhysicsDrivenRacingLineAudit.Conditions()[0],
                Parameters=RearWheelAudit.Parameters(true),ExperimentalTrackShape.RampFraction,ExperimentalTrackShape.BendIntervals,ExperimentalTrackShape.WidthTransitionMeters,
                Initial=new{Reference=0,Offset=5,Speed=18,Target=5,Drive=1,Laps=3,StepSeconds=.01},
                Selection="Among four egg variants with feasible base plans, smallest middle-tighten minus inner; then wide-tighten minus inner, then name. At most two historical controls, no plan search.",
                Scope="Uniform frozen surface. Unchanged #100 tyre, wheel, yaw, mass, dimensions and drive laws. No production geometry switch."});
            foreach(var v in Shapes())Save(directory,Name(v)+"-geometry.json",ReplayGeometry.Create(Frame(v)));
            return;
        }
        if(stage!="measure")throw new ArgumentException("Use freeze or measure.");
        using var frozen=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"freeze.json")));
        if(frozen.RootElement.GetProperty("BaseHead").GetString()!=BaseHead ||
            JsonSerializer.Serialize(frozen.RootElement.GetProperty("Variants"))!=JsonSerializer.Serialize(Shapes(),Json) ||
            !frozen.RootElement.GetProperty("Plans").Deserialize<RacingLinePlan[]>(Audit.Json)!.Select(p=>p.NumericKey).SequenceEqual(Plans().Select(p=>p.NumericKey)))
            throw new InvalidOperationException("Frozen inputs differ.");
        var rows=new List<Measurement>();
        foreach(var v in Shapes())foreach(var plan in Plans()){
            var replay=plan.Name is "inner" or "wide-tighten" && (v is null||v.Name.StartsWith("egg-",StringComparison.Ordinal));
            var row=Measure(v,plan,true,directory,replay);rows.Add(row);Console.WriteLine($"{row.Geometry}/{plan.Name}: {row.Evaluation.FlyingSeconds:F6}s feasible={row.Evaluation.Feasible} gaps={row.Evaluation.MinimumInnerClearanceMeters:F4}/{row.Evaluation.MinimumOuterClearanceMeters:F4} {string.Join('/',row.Evaluation.Rejections)}");
        }
        Save(directory,"measurements.json",rows);
        var selected=rows.Where(r=>r.Geometry.StartsWith("egg-",StringComparison.Ordinal)).GroupBy(r=>r.Geometry)
            .Where(g=>g.Where(r=>RearWheelAudit.Plans.Contains(r.Evaluation.Plan.Name)).All(r=>r.Evaluation.Feasible))
            .OrderBy(g=>g.Single(r=>r.Evaluation.Plan.Name=="middle-tighten").Evaluation.FlyingSeconds-g.Single(r=>r.Evaluation.Plan.Name=="inner").Evaluation.FlyingSeconds)
            .ThenBy(g=>g.Single(r=>r.Evaluation.Plan.Name=="wide-tighten").Evaluation.FlyingSeconds-g.Single(r=>r.Evaluation.Plan.Name=="inner").Evaluation.FlyingSeconds)
            .ThenBy(g=>g.Key,StringComparer.Ordinal).Take(2).Select(g=>g.Key).ToArray();
        Save(directory,"selection.json",selected);
        var historical=new List<Measurement>();
        foreach(var name in new[]{"baseline"}.Concat(selected))foreach(var plan in Plans().Take(3))
            historical.Add(Measure(Shapes().Single(v=>Name(v)==name),plan,false,directory,false));
        Save(directory,"historical-controls.json",historical);
        var repeats=new List<object>();
        foreach(var v in Shapes()){
            var again=Plain(v,Plans()[0]);var row=rows.Single(r=>r.Geometry==Name(v)&&r.Evaluation.Plan.Name=="inner");
            if(again.Fingerprint!=row.Evaluation.Fingerprint)throw new InvalidOperationException("Unobserved repeat differs: "+Name(v));
            repeats.Add(new{Geometry=Name(v),again.Fingerprint,ObservedFingerprint=row.Evaluation.Fingerprint,again.Work.SolverCalls,again.Completed});
        }
        Save(directory,"repeatability.json",repeats);
        Save(directory,"performance.json",Cost(Shapes().Where(v=>v is null||selected.Take(1).Contains(Name(v))).ToArray()));
    }
    private static void Verify(string directory)
    {
        using var evidence=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"measurements.json")));
        var rows=evidence.RootElement.EnumerateArray().ToArray();var validation=new List<object>();
        foreach(var variant in TrackShapeVariant.All()){
            var frame=Frame(variant);var shape=frame.Shape!;double maxK=0,minK=double.PositiveInfinity,maxProjectionError=0,maxEdgeSecond=0,maxChordDistance=0;
            MeterPoint Edge(double s){var q=shape.At(s);return q.Position+new MeterPoint(Math.Sin(q.Heading),-Math.Cos(q.Heading))*(shape.Width(s)-1);}
            for(var i=0;i<16000;i++){
                var s=i*frame.LapMeters/16000;var k=shape.At(s).Curvature;maxK=Math.Max(maxK,k);minK=Math.Min(minK,k);
                if(!variant.Circular||!new[]{1,4,5,8}.Any(j=>Math.Abs(s-frame.SegmentStartMeters(j))<.002))
                    maxEdgeSecond=Math.Max(maxEdgeSecond,(Edge(s-.001)+Edge(s+.001)-Edge(s)*2).Length*1e6);
                var a=Edge(s-.025);var b=Edge(s+.025);var d=b-a;var p=Edge(s);
                maxChordDistance=Math.Max(maxChordDistance,(p-a-d*Math.Clamp(MeterPoint.Dot(p-a,d)/MeterPoint.Dot(d,d),0,1)).Length);
                if(i%16==0)foreach(var offset in new[]{-1d,1,5,9.5,15.6}){
                    var road=frame.Project(frame.At(s,offset).Position);maxProjectionError=Math.Max(maxProjectionError,Math.Abs(frame.UnwrapDelta(s,road.ReferenceMeters)));}
            }
            if(maxK>.1||minK< -1e-8||maxProjectionError>.00004||maxEdgeSecond>2||maxChordDistance>.0007)throw new InvalidOperationException("Geometry bound failed: "+variant.Name);
            var points=Enumerable.Range(0,1000).Select(i=>frame.At(i*frame.LapMeters/1000,1+i%9).Position).ToArray();
            for(var i=0;i<5000;i++)frame.Project(points[i%1000]);var perf=new List<object>();var checksum=0d;
            for(var j=0;j<5;j++){var allocated=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.GetTimestamp();
                for(var i=0;i<10000;i++)checksum+=frame.Project(points[i%1000]).ReferenceMeters;
                var elapsed=Stopwatch.GetElapsedTime(watch).TotalMicroseconds;var bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
                if(bytes!=0)throw new InvalidOperationException("Projection allocated.");perf.Add(new{MicrosecondsPerProjection=elapsed/10000,AllocatedBytes=bytes});}
            validation.Add(new{variant.Name,variant.FirstCompression,variant.SecondCompression,shape.HeightMeters,shape.FirstExtentMeters,shape.SecondExtentMeters,
                frame.LapMeters,FirstBendLength=frame.SegmentStartMeters(4)-frame.SegmentStartMeters(1),SecondBendLength=frame.SegmentStartMeters(8)-frame.SegmentStartMeters(5),
                MinimumCurvature=minK,MaximumCurvature=maxK,MaximumProjectionErrorMeters=maxProjectionError,MaximumOuterEdgeSecondDerivative=maxEdgeSecond,
                MaximumOuterEdgeChordDistanceMeters=maxChordDistance,Projection=perf,Checksum=checksum});
        }
        Save(directory,"geometry-validation.json",validation);
        var failures=new List<object>();
        foreach(var row in rows.Where(r=>!r.GetProperty("Evaluation").GetProperty("Feasible").GetBoolean())){
            var name=row.GetProperty("Geometry").GetString()!;var variant=TrackShapeVariant.All().Single(v=>v.Name==name);
            var plan=Plans().Single(p=>p.Name==row.GetProperty("Evaluation").GetProperty("Plan").GetProperty("Name").GetString());
            var (e,c)=PhysicsDrivenRacingLineAudit.Evaluate(PhysicsDrivenRacingLineAudit.Conditions()[0],plan,parameters:RearWheelAudit.Parameters(true),trackShape:variant);
            if(e.Fingerprint!=row.GetProperty("Evaluation").GetProperty("Fingerprint").GetString())throw new InvalidOperationException("Rejected repeat differs.");
            var geometry=new RacingLineGeometry(Frame(variant));var scored=c.Ticks.Select(t=>(Tick:t,Gap:geometry.Clearance(t.After))).ToArray();
            var worst=scored.MinBy(t=>Math.Min(t.Gap.Inner,t.Gap.Outer));
            failures.Add(new{Geometry=name,Plan=plan.Name,e.Fingerprint,e.Rejections,Worst=new{Seconds=worst.Tick.TimeSeconds+worst.Tick.DurationSeconds,
                worst.Tick.After,Inner=worst.Gap.Inner,Outer=worst.Gap.Outer},OutsideSamples=scored.Count(t=>Math.Min(t.Gap.Inner,t.Gap.Outer)<0),
                NearWorst=scored.Where(t=>Math.Abs(t.Tick.TimeSeconds-worst.Tick.TimeSeconds)<.15).Select(t=>new{t.Tick.TimeSeconds,t.Tick.After.Position,t.Tick.After.Heading,t.Tick.After.Road,Inner=t.Gap.Inner,Outer=t.Gap.Outer})});
        }
        Save(directory,"rejected-details.json",failures);
    }
    public sealed record Measurement(string Geometry,bool Wheel,RacingLineEvaluation Evaluation,object Energy,
        double MaximumSlideDegrees,double MaximumSlipRatio,object[] Corners,object Work,object[] Trace);
    public static Measurement Measure(TrackShapeVariant? variant,RacingLinePlan plan,bool wheel,string directory,bool replay)
    {
        var name=Name(variant);var alias=name+"-"+plan.Name;var wheels=new List<RearWheelObservation>();
        var frame=Frame(variant);var geometry=new RacingLineGeometry(frame);
        var (e,c)=PhysicsDrivenRacingLineAudit.Evaluate(PhysicsDrivenRacingLineAudit.Conditions()[0],plan,
            replay?Path.Combine(directory,alias+"-accepted.csv"):null,RearWheelAudit.Parameters(wheel),wheels.Add,variant);
        var energy=RearWheelAudit.Energy(new(c,wheels.ToArray()));
        var corners=new List<object>();
        for(var lap=1;lap<3;lap++)foreach(var corner in frame.Track.CornerTopology.Corners){
            var start=lap*frame.LapMeters+frame.SegmentStartMeters(corner.StartSegmentIndex);
            var end=lap*frame.LapMeters+frame.SegmentStartMeters(corner.StartSegmentIndex+corner.SegmentCount);var length=end-start;
            var bend=c.Ticks.Where(t=>t.Before.TotalReferenceMeters>=start&&t.Before.TotalReferenceMeters<end).ToArray();if(bend.Length==0)continue;
            var entry=bend[0];var middle=bend.MinBy(t=>Math.Abs(t.Before.TotalReferenceMeters-(start+.5*length)))!;
            var straight=c.Ticks.Where(t=>t.Before.TotalReferenceMeters>=end&&t.Before.TotalReferenceMeters<=end+12).ToArray();
            var exit=straight.Length>0?straight[0].Before:bend[^1].After;
            var nextStraight=c.Ticks.Where(t=>t.Before.TotalReferenceMeters>=end+12&&t.Before.TotalReferenceMeters<=end+45).ToArray();
            corners.Add(new{Lap=lap+1,Corner=corner.StartSegmentIndex,EntrySpeed=entry.Before.Speed,MiddleSpeed=middle.Before.Speed,ExitSpeed=exit.Speed,
                MinimumSpeed=bend.Min(t=>Math.Min(t.Before.Speed,t.After.Speed)),EntryOffset=entry.Before.Road.OffsetMeters,ApexOffset=middle.Before.Road.OffsetMeters,
                ExitOffset=exit.Road.OffsetMeters,StraightOffset=nextStraight.Length>0?nextStraight.Average(t=>t.Before.Road.OffsetMeters):double.NaN,
                ExitOutwardSpeed=Vector2.Dot(exit.Velocity,exit.Road.Outward),
                AddedWideningFirst12Meters=straight.Select(t=>t.After.Road.OffsetMeters-exit.Road.OffsetMeters).DefaultIfEmpty(0).Max(),
                NextStraightMaximumSpeed=straight.Concat(nextStraight).Select(t=>t.After.Speed).DefaultIfEmpty(0).Max()});
        }
        var trace=c.Ticks.Where(t=>t.Before.TotalReferenceMeters>=frame.LapMeters&&t.Before.TotalReferenceMeters<2*frame.LapMeters)
            .Where((_,i)=>i%10==0).Select(t=>(object)new{Reference=t.Before.TotalReferenceMeters-frame.LapMeters,t.Before.Position.X,t.Before.Position.Y,
                t.Before.Speed,Offset=t.Before.Road.OffsetMeters,t.Slip.ActualSlipDegrees,Curvature=frame.Shape?.At(t.Before.Road.ReferenceMeters).Curvature??(t.Before.Road.IsTurn?1/(t.Before.Road.RadiusMeters-t.Before.Road.OffsetMeters):0)}).ToArray();
        var r=new Measurement(name,wheel,e,energy,c.Ticks.Max(t=>Math.Abs(t.Before.SlideRadians)*180/Math.PI),
            wheels.Select(w=>w.Step.InstantSlipRatio).DefaultIfEmpty(0).Max(),corners.ToArray(),new{c.Result.Work.SolverCalls,c.Result.Work.PhysicsUpdates,c.Result.Work.RearWheelForceEvaluations,
                c.Result.Work.RearWheelBoundaryEvaluations,c.Result.Work.RearWheelCommits,c.Result.Work.CurvatureLimitedCalls,c.Result.Work.MaximumStepDisplacementMeters},trace);
        if(replay){var times=e.LapSeconds.Select((_,i)=>e.LapSeconds.Take(i+1).Sum()).ToArray();
            Save(directory,alias+".json",new{GeometryFile=name+"-geometry.json",Run=new{c.Initial,c.Result,Passes=Array.Empty<object>(),c.Consequences,
                Riders=new[]{new{RiderId=1,Measurement=new{RiderId=1,CrossingSeconds=times,e.LapSeconds}}}},Geometry=name,Plan=plan});}
        return r;
    }
    public static RunResult Plain(TrackShapeVariant? variant,RacingLinePlan plan)
    {
        var frame=Frame(variant);using var w=new WorldRunner(new[]{PhysicsDrivenRacingLineAudit.Rider(PhysicsDrivenRacingLineAudit.Conditions()[0])},
            parameters:RearWheelAudit.Parameters(true),trackShape:variant,spatialControls:PhysicsDrivenRacingLine.Compile(plan,frame),
            frozenSurface:TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot());
        return w.Run("geometry-plain",.01,90,3,lapsFromInitialPosition:true);
    }
    private static object Cost(TrackShapeVariant?[] variants)
    {
        var rows=new List<object>();
        foreach(var variant in variants){var frame=Frame(variant);var points=Enumerable.Range(0,1000).Select(i=>frame.At(i*frame.LapMeters/1000,1+(i%9)).Position).ToArray();
            var checksum=0d;for(var i=0;i<5000;i++)checksum+=frame.Project(points[i%points.Length]).ReferenceMeters;
            var samples=new List<object>();
            for(var trial=0;trial<5;trial++){var allocated=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.GetTimestamp();
                for(var i=0;i<10000;i++)checksum+=frame.Project(points[i%points.Length]).ReferenceMeters;
                var elapsed=Stopwatch.GetElapsedTime(watch).TotalMicroseconds;var bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
                samples.Add(new{MicrosecondsPerProjection=elapsed/10000,AllocatedBytes=bytes});}
            var worldSamples=new List<object>();Plain(variant,Plans()[0]);
            for(var trial=0;trial<3;trial++){var watch=Stopwatch.GetTimestamp();var result=Plain(variant,Plans()[0]);var elapsed=Stopwatch.GetElapsedTime(watch).TotalMilliseconds;
                worldSamples.Add(new{Milliseconds=elapsed,result.Fingerprint,result.Work.SolverCalls,result.Work.RearWheelForceEvaluations,result.Work.RearWheelBoundaryEvaluations});}
            rows.Add(new{Geometry=Name(variant),Projection=samples,PlainWorld=worldSamples,Checksum=checksum});
        }
        return rows;
    }
}
