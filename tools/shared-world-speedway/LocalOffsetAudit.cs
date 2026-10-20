using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using CoreSim;

namespace SharedWorldSpeedway;

public static class LocalOffsetAudit
{
    public const string BaseHead="e27be466416dcba2f3b71bdbb1a5a989f01d43f7";
    // Covers native angle/float-coordinate approximation in the between-state bound.
    public const double IntervalNumericalAllowanceMeters=.012;
    public sealed record Target(float Meters,float? OuterFraction=null);
    public sealed record Plan(string Name,Target Straight,Target Entry,Target Apex,Target Exit,
        bool Local,double ApexFraction=.4,double ExitFraction=.75);
    public static Plan[] Plans()=>new[]{
        new Plan("inner",new(1),new(1),new(1),new(1),false,.35,.8),
        new("middle-tighten",new(5),new(3),new(1),new(5),false,.45,.8),
        new("wide-tighten",new(9.5f),new(7),new(2),new(9.5f),false,.35,.8),
        new("outer-max",new(.5f,1),new(.5f,1),new(.5f,1),new(.5f,1),true),
        new("outer-tighten",new(.5f,1),new(.5f,1),new(5),new(.5f,1),true),
        new("middle-to-outer",new(5),new(.5f,.85f),new(7),new(.5f,.75f),true)};
    public static WorldParameters Parameters(bool local=true)=>RearWheelAudit.Parameters(true) with{EnableLocalOffsetLimit=local};
    public static TrackShapeVariant Mild()=>TrackShapeVariant.All().Single(v=>v.Name=="egg-mild");
    private static RacingLinePlan Legacy(Plan p)=>TrackGeometryAudit.Plans().Single(q=>q.Name==p.Name);
    public static SpatialControlCommand[] Compile(Plan p,TrackFrame frame)
    {
        if(!p.Local)return PhysicsDrivenRacingLine.Compile(Legacy(p),frame);
        SpatialControlCommand Command(double s,Target t)=>new(1,s,t.Meters,1,t.OuterFraction);
        var list=new List<SpatialControlCommand>{Command(0,p.Straight)};
        for(var lap=0;lap<3;lap++)foreach(var corner in frame.Track.CornerTopology.Corners){
            var start=lap*frame.LapMeters+frame.SegmentStartMeters(corner.StartSegmentIndex);
            var end=lap*frame.LapMeters+frame.SegmentStartMeters(corner.StartSegmentIndex+corner.SegmentCount);
            list.Add(Command(start-15,p.Entry));list.Add(Command(start+p.ApexFraction*(end-start),p.Apex));
            list.Add(Command(start+p.ExitFraction*(end-start),p.Exit));list.Add(Command(end,p.Straight));
        }
        return list.OrderBy(c=>c.ReferenceMeters).ToArray();
    }
    internal static readonly JsonSerializerOptions Json=CreateJson();
    private static JsonSerializerOptions CreateJson()
    {
        var resolver=new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info=>{if(info.Type==typeof(RacingLineEvaluation))info.Properties.Remove(info.Properties.Single(p=>p.Name=="Plan"));});
        // The actual policy is Measurement.Plan. Do not export the legacy evaluator's label adapter as a second numerical plan.
        return new(Audit.Json){WriteIndented=false,TypeInfoResolver=resolver};
    }
    private static void Save(string dir,string file,object value,bool geometry=false)
    {
        var path=Path.Combine(dir,file);if(File.Exists(path))throw new IOException("Evidence exists: "+path);
        File.WriteAllText(path,JsonSerializer.Serialize(value,geometry?ReplayGeometry.Json:Json)+"\n");
    }
    public sealed record Measurement(string Geometry,Plan Plan,RacingLineEvaluation Evaluation,object Energy,
        double MaximumSlideDegrees,double MaximumWheelspin,double MaximumActualOffsetMeters,double MaximumTargetMeters,
        double AboveOldLimitSeconds,double TargetAboveOldLimitSeconds,double ConservativeMinimumOuterClearanceMeters,
        object[] Corners,object[] WorstStates);
    public static Measurement Measure(TrackShapeVariant? variant,Plan p,string? directory=null,Action<SlipControlValidation.Capture>? capture=null)
    {
        var frame=TrackGeometryAudit.Frame(variant);var controls=Compile(p,frame);var name=TrackGeometryAudit.Name(variant)+"-"+p.Name;
        if(directory is not null&&(File.Exists(Path.Combine(directory,name+".json"))||File.Exists(Path.Combine(directory,name+"-accepted.csv"))))
            throw new IOException("Evidence exists: "+name);
        var wheels=new List<RearWheelObservation>();var proxy=p.Local?new RacingLinePlan(p.Name,5,5,5,5):Legacy(p);
        var (original,c)=PhysicsDrivenRacingLineAudit.Evaluate(PhysicsDrivenRacingLineAudit.Conditions()[0],proxy,
            directory is null?null:Path.Combine(directory,name+"-accepted.csv"),Parameters(p.Local),wheels.Add,variant,controls);
        capture?.Invoke(c);
        var geometry=new RacingLineGeometry(frame);var previousGap=geometry.Clearance(c.Initial.Riders.Single()).Outer;
        var footprintRadius=CoreSim.PhysicalSpace.SpeedwayBikeDimensions.Reference.BoundingRadiusMeters;
        var scored=c.Ticks.Select(t=>{
            var gap=geometry.Clearance(t.After);
            // One isolated body, held world force/torque, no impacts: intermediate
            // linear and angular speeds are bounded by the accepted endpoints.
            var speed=Math.Max(t.Before.Speed,t.After.Speed)+footprintRadius*Math.Max(Math.Abs(t.Before.AngularVelocity),Math.Abs(t.After.AngularVelocity));
            var bound=Math.Min(previousGap,gap.Outer)-speed*t.DurationSeconds/2-IntervalNumericalAllowanceMeters;previousGap=gap.Outer;
            return new{t.TimeSeconds,t.DurationSeconds,t.After,Inner=gap.Inner,Outer=gap.Outer,ConservativeOuter=bound};
        }).ToArray();
        var conservativeOuter=scored.Min(t=>t.ConservativeOuter);
        var errors=original.Rejections.ToList();if(original.MinimumOuterClearanceMeters<LocalOffsetLimit.RequiredOuterClearanceMeters)errors.Add("OuterClearanceBelow0.5m");
        if(conservativeOuter<LocalOffsetLimit.RequiredOuterClearanceMeters)errors.Add("OuterIntervalClearanceBelow0.5m");
        var evaluation=original with{Feasible=errors.Count==0,Rejections=errors.ToArray()};
        var corners=new List<object>();
        for(var lap=1;lap<3;lap++)foreach(var corner in frame.Track.CornerTopology.Corners){
            var start=lap*frame.LapMeters+frame.SegmentStartMeters(corner.StartSegmentIndex);
            var end=lap*frame.LapMeters+frame.SegmentStartMeters(corner.StartSegmentIndex+corner.SegmentCount);
            var bend=c.Ticks.Where(t=>t.Before.TotalReferenceMeters>=start&&t.Before.TotalReferenceMeters<end).ToArray();
            if(bend.Length==0)continue;
            var entry=bend[0].Before;var middle=bend.MinBy(t=>Math.Abs(t.Before.TotalReferenceMeters-(start+end)/2))!.Before;
            var straight=c.Ticks.Where(t=>t.Before.TotalReferenceMeters>=end&&t.Before.TotalReferenceMeters<=end+12).ToArray();
            var exit=straight.Length>0?straight[0].Before:bend[^1].After;
            var next=c.Ticks.Where(t=>t.Before.TotalReferenceMeters>=end+12&&t.Before.TotalReferenceMeters<=end+45).ToArray();
            corners.Add(new{Lap=lap+1,Corner=corner.StartSegmentIndex,EntrySpeed=entry.Speed,MiddleSpeed=middle.Speed,ExitSpeed=exit.Speed,
                MinimumSpeed=bend.Min(t=>Math.Min(t.Before.Speed,t.After.Speed)),NextStraightMaximumSpeed=straight.Concat(next).Select(t=>t.After.Speed).DefaultIfEmpty().Max(),
                EntryOffset=entry.Road.OffsetMeters,ApexOffset=middle.Road.OffsetMeters,ExitOffset=exit.Road.OffsetMeters,
                StraightOffset=next.Select(t=>t.Before.Road.OffsetMeters).DefaultIfEmpty().Average(),
                ExitOutwardSpeed=Vector2.Dot(exit.Velocity,exit.Road.Outward),
                AddedWideningFirst12Meters=straight.Select(t=>t.After.Road.OffsetMeters-exit.Road.OffsetMeters).DefaultIfEmpty().Max()});
        }
        var worst=scored.OrderBy(t=>t.ConservativeOuter).Take(8).Cast<object>().ToArray();
        var row=new Measurement(TrackGeometryAudit.Name(variant),p,evaluation,RearWheelAudit.Energy(new(c,wheels.ToArray())),
            c.Ticks.Max(t=>Math.Abs(t.Before.SlideRadians)*180/Math.PI),wheels.Select(w=>w.Step.InstantSlipRatio).DefaultIfEmpty().Max(),
            c.Ticks.Max(t=>t.After.Road.OffsetMeters),c.Ticks.Max(t=>t.Before.TargetOffsetMeters),
            c.Ticks.Where(t=>t.Before.Road.OffsetMeters>9.5).Sum(t=>t.DurationSeconds),
            c.Ticks.Where(t=>t.Before.TargetOffsetMeters>9.5).Sum(t=>t.DurationSeconds),conservativeOuter,corners.ToArray(),worst);
        if(directory is not null){
            var times=evaluation.LapSeconds.Select((_,i)=>evaluation.LapSeconds.Take(i+1).Sum()).ToArray();
            Save(directory,name+".json",new{Geometry=TrackGeometryAudit.Name(variant),GeometryFile=TrackGeometryAudit.Name(variant)+"-geometry.json",
                Plan=p,Evaluation=evaluation,Run=new{c.Initial,c.Result,Passes=Array.Empty<object>(),c.Consequences,
                    Riders=new[]{new{Measurement=new{RiderId=1,CrossingSeconds=times,evaluation.LapSeconds}}}}});
        }
        return row;
    }
    public static RunResult Plain(TrackShapeVariant? variant,Plan p)
    {
        var f=TrackGeometryAudit.Frame(variant);
        using var w=new WorldRunner(new[]{PhysicsDrivenRacingLineAudit.Rider(PhysicsDrivenRacingLineAudit.Conditions()[0])},parameters:Parameters(p.Local),
            trackShape:variant,spatialControls:Compile(p,f),frozenSurface:TrackState.CreateDefault(f.Track,new(1,0,.5f)).Snapshot());
        return w.Run("local-plain",.01,90,3,lapsFromInitialPosition:true);
    }
    public static void Run(string directory,string stage)
    {
        Directory.CreateDirectory(directory);
        if(stage=="freeze"){
            Save(directory,"freeze.json",new{BaseHead,Plans=Plans(),LocalOffsetLimit.RequiredOuterClearanceMeters,
                LocalOffsetLimit.MaximumReferenceSpacingMeters,LocalOffsetLimit.FloatPositionAllowanceMeters,
                IntervalNumericalAllowanceMeters,
                Geometries=new[]{"egg-mild","baseline"},Parameters=Parameters(),Condition=PhysicsDrivenRacingLineAudit.Conditions()[0],
                Initial=new{Reference=0,Offset=5,Speed=18,Laps=3,StepSeconds=.01},
                Interpretation="New local plans use fraction of [.5, certified local cap], evaluated on each accepted boundary, no future state. Literal references preserve #101 commands with the new 0.5m outer audit. No tuning after results."});
            foreach(var v in new[]{Mild(),null})Save(directory,TrackGeometryAudit.Name(v)+"-geometry.json",ReplayGeometry.CreateWithLocalLimit(TrackGeometryAudit.Frame(v)),true);
            return;
        }
        if(stage=="cost"){Save(directory,"performance.json",Cost());return;}
        if(stage=="world-cost"){Save(directory,"world-performance.json",WorldCost());return;}
        if(stage!="measure")throw new ArgumentException("Use freeze, measure, cost or world-cost.");
        using(var freeze=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"freeze.json"))))
            if(freeze.RootElement.GetProperty("Plans").GetRawText()!=JsonSerializer.Serialize(Plans(),Json))throw new InvalidOperationException("Frozen plans differ.");
        var rows=new List<Measurement>();
        foreach(var v in new[]{Mild(),null})foreach(var p in Plans()){
            var row=Measure(v,p,directory);rows.Add(row);
            Console.WriteLine($"{row.Geometry}/{p.Name}: {row.Evaluation.FlyingSeconds:F6}s feasible={row.Evaluation.Feasible} outer={row.Evaluation.MinimumOuterClearanceMeters:F4} maxOffset={row.MaximumActualOffsetMeters:F3} {string.Join('/',row.Evaluation.Rejections)}");
        }
        Save(directory,"measurements.json",rows);
        var repeats=new List<object>();
        foreach(var name in new[]{"inner","outer-tighten"}){
            var p=Plans().Single(p=>p.Name==name);var result=Plain(Mild(),p);var expected=rows.Single(r=>r.Geometry=="egg-mild"&&r.Plan.Name==name).Evaluation;
            if(result.Fingerprint!=expected.Fingerprint)throw new InvalidOperationException("Observed repeat differs.");
            repeats.Add(new{Plan=name,result.Fingerprint,result.Work.SolverCalls,Observed=expected.Fingerprint});
        }
        Save(directory,"repeatability.json",repeats);
    }
    private static object Cost()
    {
        var samples=new List<object>();
        foreach(var v in new[]{Mild(),null}){
            var frame=TrackGeometryAudit.Frame(v);var allocatedBefore=GC.GetAllocatedBytesForCurrentThread();var start=Stopwatch.GetTimestamp();var table=frame.LocalOffsets;
            var build=Stopwatch.GetElapsedTime(start).TotalMilliseconds;double sink=0;
            var buildBytes=GC.GetAllocatedBytesForCurrentThread()-allocatedBefore;
            for(var i=0;i<10000;i++)sink+=table.At(i*.031);
            var runs=new List<object>();for(var j=0;j<5;j++){
                var bytes=GC.GetAllocatedBytesForCurrentThread();start=Stopwatch.GetTimestamp();
                for(var i=0;i<100000;i++)sink+=table.At(i*.031);
                var us=Stopwatch.GetElapsedTime(start).TotalMicroseconds;var allocated=GC.GetAllocatedBytesForCurrentThread()-bytes;
                runs.Add(new{MicrosecondsPerQuery=us/100000,AllocatedBytes=allocated});
            }
            samples.Add(new{Geometry=TrackGeometryAudit.Name(v),BuildMilliseconds=build,BuildAllocatedBytes=buildBytes,table.ReferenceSpacingMeters,
                table.CellMotionAllowanceMeters,table.FootprintRadiusMeters,table.MaximumAcrossTrackMeters,Query=runs,Sink=sink});
        }
        return new{Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            TieredCompilation=Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),Rows=samples};
    }
    private static object WorldCost()
    {
        var rows=new List<object>();var plan=Plans()[0];
        foreach(var v in new[]{Mild(),null})for(var trial=-1;trial<5;trial++)foreach(var enabled in trial%2==0?new[]{false,true}:new[]{true,false}){
            var frame=TrackGeometryAudit.Frame(v);var controls=Compile(plan,frame);var started=Stopwatch.GetTimestamp();
            using var w=new WorldRunner(new[]{PhysicsDrivenRacingLineAudit.Rider(PhysicsDrivenRacingLineAudit.Conditions()[0])},
                parameters:Parameters(enabled),spatialControls:controls,trackShape:v,
                frozenSurface:TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot());
            var built=Stopwatch.GetTimestamp();var result=w.Run("identical-inner-cost",.01,90,3,lapsFromInitialPosition:true);
            var run=Stopwatch.GetElapsedTime(built).TotalMilliseconds;
            if(trial>=0)rows.Add(new{Geometry=TrackGeometryAudit.Name(v),Enabled=enabled,
                ConstructionMilliseconds=Stopwatch.GetElapsedTime(started,built).TotalMilliseconds,RunMilliseconds=run,
                TotalMilliseconds=Stopwatch.GetElapsedTime(started).TotalMilliseconds,result.Fingerprint,result.Work.SolverCalls});
        }
        return new{Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            TieredCompilation=Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),Rows=rows};
    }
}
