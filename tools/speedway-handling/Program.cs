using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;
using Cyclic172;
using Safety153;
using SharedWorldSpeedway;
using Speed148;

namespace SpeedwayHandling;

internal sealed record StartCase(string Id, string Gate, double Start, float Offset);
internal sealed record StudyOutcome(string Id, bool StageA, bool Legal, int Laps, double? FinishSeconds,
    double EndSeconds, double EndReference, double MinSweep, double MaxBetaDegrees, string Reason);

internal static class Program
{
    private static readonly TrackFrame Frame = NativeWorld172.Frame();
    private static readonly WorldParameters Physics = FreeLearningExperiment.Parameters();
    private static readonly TrackStateSnapshot Surface = MultiActionExperiment135.Surface(Frame);
    private static readonly HandlingSettings Settings = new();
    private static readonly RiderTuning Tuning = new();
    private static readonly JsonSerializerOptions Json = new(FreeLearningExperiment.Json) { WriteIndented = false };
    private const double Review = .20, Reaction = .24;
    private const float Mass = 147, Inertia = 52.72097f;
    private const string Tool = "tools/speedway-handling";
    private static readonly StartCase FullStraight = new("full-straight", "C", -27, 7.7f);

    private static int Main(string[] args)
    {
        if (args.Length < 2 || args[0] != "--speedway-handling")
        {
            Console.Error.WriteLine("SpeedwayHandling OFF. Pass --speedway-handling check, model or native.");
            return 2;
        }
        switch (args)
        {
            case [_, "check"]:
                Check();
                break;
            case [_, "model", var directory, var test]:
                Directory.CreateDirectory(directory);
                Model(directory, Find(test));
                break;
            case [_, "native", var output, var id, var name] when id.All(c => char.IsAsciiLetterOrDigit(c) || c=='-'):
                Directory.CreateDirectory(output);
                using (var mutex = new FileStream(Path.Combine(output,".lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
                    Native(output, id, Find(name));
                break;
            default:
                throw new ArgumentException("Use check, model DIRECTORY CASE or native DIRECTORY ID CASE.");
        }
        return 0;
    }

    private static StartCase Find(string name)
    {
        if (name == FullStraight.Id) return FullStraight;
        if (name is not ("V2" or "V3")) throw new ArgumentException("Use full-straight, V2 or V3.");
        return Read<StartCase[]>("docs/experiments/trajectory-control/held177.json").Single(x => x.Id == name);
    }

    private static RiderSpec Spec(StartCase start) => NativeWorld172.Rider(Enum.Parse<StartingGate>(start.Gate)) with
    {
        ReferenceMeters = start.Start, OffsetMeters = start.Offset, TargetOffsetMeters = start.Offset
    };

    private static void Check()
    {
        int count = 0;
        var spec = Spec(FullStraight);
        var road = Frame.At(45,5);
        var track = Surface.SampleSurface(1,2);
        foreach (float speed in new[] { 0f, 10, 18, 24, 30 })
        foreach (float beta in new[] { -.25f, 0, .13f, .25f })
        foreach (float drive in new[] { 0f, .40f, 1 })
        {
            var body = new RiderView(1,road.Position,new(speed,0),beta,.30f,road.Road,45,0,
                RiderActivity.Racing,drive,5,0);
            double omega = speed / .33 * (drive > .5 ? 1.45 : 1);
            var wheel = ForecastFork148.Wheel(omega);
            var shaft = new CrankClutch144(omega:Math.Max(415,9*omega));
            var step = HandlingContact.Prepare(Settings,track,body,spec,Physics,2,Mass,Inertia,wheel,0,.4,.01f,.04f,shaft,Reaction);
            var f = step.Forces;
            var load = f.TyreLoads!;
            var original = LoadBasedTyreGrip.Budget(Mass,track,TwoContactBikeParameters.Default);
            if (load.FrontNormalNewtons != original.FrontNormalNewtons || load.RearNormalNewtons != original.RearNormalNewtons ||
                Math.Abs(load.TotalCapacityNewtons / original.TotalCapacityNewtons - Settings.GripScale) > 1e-8 ||
                Math.Abs(step.LeanAfter-.4) > Settings.LeanRateRadiansPerSecond*.0100001 ||
                Math.Abs(step.LeanAfter) > Settings.MaximumLeanRadians ||
                Math.Abs(f.FrontLateral) > load.FrontCapacityNewtons+.001 ||
                Math.Sqrt((double)f.RearLongitudinal*f.RearLongitudinal + (double)f.RearLateral*f.RearLateral) > load.RearCapacityNewtons+.001 ||
                Vector2.Dot(f.FrontForce,f.FrontVelocity) > 1e-4 ||
                f.RearLateral*Vector2.Dot(f.RearVelocity,f.BodyLeft) > 1e-4 ||
                !FreeLearningExperiment.SteeringStepIsPhysical(f,.01f) ||
                shaft.Prepared.Wheel.LongitudinalLossJoules < -1e-6 ||
                Math.Abs(shaft.Prepared.EnergyResidualJoules) > 1e-6 || shaft.Prepared.ClutchHeatJoules < -1e-6)
                throw new InvalidOperationException("Handling force, lean, steer or energy invariant.");
            count++;
        }
        Console.WriteLine($"PASS {count} handling contact and actuator cases; gameplay capacity ×{Settings.GripScale:F2}.");
    }

    private static void Model(string dir, StartCase test)
    {
        var spec = Spec(test);
        var initial = Frame.At(spec.ReferenceMeters,spec.OffsetMeters);
        var body = new RiderView(1,initial.Position,Vector2.Zero,(float)initial.Road.Heading,0,
            initial.Road,spec.ReferenceMeters,0,RiderActivity.Racing,0,spec.OffsetMeters,0,
            Frame.Context(initial.Road,spec.ReferenceMeters));
        var launch = LaunchParameters144.Default;
        var state = new HandlingForecast(body,0,0,0,0,new CrankClutch144().Omega);
        var control = new HandlingController(Frame,Surface,Physics,spec,Mass,Inertia,Settings,Tuning);
        var boundary = new RacingLineGeometry(Frame);
        var active = new FreeRacingAction(0,0);
        FreeRacingCommand[] pending = [];
        var rows = new List<object>();
        var decisions = new List<HandlingDecision>();
        var oldGap = boundary.Clearance(body);
        double minSweep = Math.Min(oldGap.Inner,oldGap.Outer);
        double maxBeta = 0;
        double goal = test == FullStraight ? Frame.SegmentStartMeters(5) : 3*Frame.LapMeters;
        double top = 0;
        string status = "bounded forecast";
        for (int tick = 0; tick < 8500; tick++)
        {
            while (pending.Length>0 && pending[0].ActivationAt<=state.Time+1e-9)
            {
                active = pending[0].Action;
                pending = pending[1..];
            }
            if (tick%20==0)
            {
                var input = new FreeRacingInput(state.Rider,state.Time,state.Wheel,state.Steer,active,pending,state.Time);
                var choice = control.Choose(input,new CrankClutch144(launch,state.Shaft),state.Lean);
                decisions.Add(choice);
                pending = [..pending,new(state.Time,state.Time+Reaction,choice.Action)];
            }
            var next = HandlingPreview.Advance(state,.01,active,pending,Surface.SampleSurface(0,2),Settings,
                spec,Physics,Frame,Mass,Inertia,launch);
            var gap = boundary.Clearance(next.Rider);
            double swept = Clearance153.SweepLowerBound(state.Rider,next.Rider,.01,oldGap,gap);
            minSweep = Math.Min(minSweep,swept);
            maxBeta = Math.Max(maxBeta,Math.Abs(next.Rider.SlideRadians)*180/Math.PI);
            state = next;
            oldGap = gap;
            top = Math.Max(top,next.Rider.Speed*3.6);
            rows.Add(new { State=next, Sweep=swept, Active=active });
            if (swept<.5||maxBeta>45)
            {
                status = swept<.5?"swept":"beta45";
                break;
            }
            if (next.Rider.TotalReferenceMeters>=goal)
            {
                status = "forecast goal";
                break;
            }
        }
        Save(Path.Combine(dir,test.Id+"-model.json.gz"),new { test,Settings,Tuning,State=state,MinSweep=minSweep,
            MaxBetaDegrees=maxBeta,Status=status,Rows=rows,Decisions=decisions },true);
        Console.WriteLine($"{test.Id} {status}: t={state.Time:F3} s={state.Rider.TotalReferenceMeters:F3} " +
            $"peak={top:F2} swept={minSweep:F3} beta={maxBeta:F2} offset={state.Rider.Road.OffsetMeters:F2}");
    }

    private static void Native(string dir, string id, StartCase test)
    {
        bool stageA = test==FullStraight;
        var files = Directory.GetFiles(dir,"*.reserved.json").Select(Read<Reservation>).ToArray();
        if (files.Count(x=>x.StageA==stageA)>=(stageA?4:2) || files.Any(x=>x.Id==id))
            throw new InvalidOperationException("Native world budget.");
        var sources = Directory.GetFiles(Tool,"*.cs").Concat(Directory.GetFiles(Tool,"*.csproj"))
            .Concat(new[]{Tool+"/packages.lock.json","tools/three-lap-global-planning/NativeWorld172.Generated.cs"})
            .ToDictionary(x=>x.Replace('\\','/'), x=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(x))));
        if (!stageA && !files.Any(x=>x.StageA && x.Settings==Settings && x.Tuning==Tuning &&
            x.Sources.OrderBy(y=>y.Key).SequenceEqual(sources.OrderBy(y=>y.Key)) &&
            File.Exists(Path.Combine(dir,x.Id+".result.json")) &&
            Read<StudyOutcome>(Path.Combine(dir,x.Id+".result.json")).Legal))
            throw new InvalidOperationException("Stage B requires the exact configuration from a legal stage A trial.");
        Save(Path.Combine(dir,id+".reserved.json"),new Reservation(id,stageA,test,Settings,Tuning,sources));
        var spec = Spec(test);
        var rig = new HandlingRig(Settings);
        var control = new HandlingController(Frame,Surface,Physics,spec,Mass,Inertia,Settings,Tuning);
        var decisions = new List<HandlingDecision>();
        double goal = stageA?Frame.SegmentStartMeters(5):3*Frame.LapMeters;
        using var world = new HandlingWorld(new(Enabled:true,RequiredLaps:3,MaximumSeconds:85,SoloGate:Enum.Parse<StartingGate>(test.Gate)),
            diagnosticFrame:Frame,diagnosticRider:spec,safetyHalfMeter:true,stopReference:goal,postSeconds:stageA?0:1,handling:rig);
        var run = world.Run(id,"K1",(input,shaft,_,_,_,_,mass,inertia,_) =>
        {
            if(mass!=Mass || Math.Abs(inertia-Inertia)>1e-5) throw new InvalidOperationException("Unexpected motorcycle mass properties.");
            var decision = control.Choose(input,shaft,rig.Lean);
            decisions.Add(decision);
            return new(new(input.Time,decision.Action,FreeActionSafety.Uncertain,false,[],decision.Queries,decision.Forecasts),0);
        });
        var rider = run.Riders.Single();
        var samples = rider.Samples;
        double minSweep = samples.Skip(1).Select(s=>s.Certificate ?? 0).DefaultIfEmpty(0).Min();
        double maxBeta = samples.Max(s=>Math.Abs(s.Input.Rider.SlideRadians)*180/Math.PI);
        bool cleared = minSweep>=.5 && maxBeta<=45 && !run.Contacts.Any() &&
            run.Outcome is "experiment timeout" or "completed" && samples[^1].Input.Rider.TotalReferenceMeters>=goal;
        bool legal = cleared && (stageA || rider.CompletedLaps==3 && rider.FinishSeconds is { } finish && run.EndSeconds-finish>=1);
        var result = new StudyOutcome(id,stageA,legal,rider.CompletedLaps,rider.FinishSeconds,run.EndSeconds,
            samples[^1].Input.Rider.TotalReferenceMeters,minSweep,maxBeta,run.Outcome);
        Save(Path.Combine(dir,id+".json.gz"),new {Run=run,Outcome=result,Decisions=decisions,Lean=rig.LeanSamples,
            test,Settings,Tuning,Goal=goal },true);
        Save(Path.Combine(dir,id+".result.json"),result);
        Console.WriteLine($"{id}: legal={legal} t={run.EndSeconds:F3} s={result.EndReference:F3} laps={result.Laps} " +
            $"finish={result.FinishSeconds:F4} swept={minSweep:F3} beta={maxBeta:F2} {run.Outcome}");
    }

    private sealed record Reservation(string Id,bool StageA,StartCase Start,HandlingSettings Settings,
        RiderTuning Tuning,Dictionary<string,string> Sources);

    private static T Read<T>(string p)=>JsonSerializer.Deserialize<T>(File.ReadAllBytes(p),Json)!;
    private static void Save(string p,object value,bool gzip=false)
    {
        using var file = new FileStream(p,FileMode.CreateNew);
        if (gzip)
        {
            using var compressed = new GZipStream(file,CompressionLevel.Fastest);
            JsonSerializer.Serialize(compressed,value,Json);
        }
        else JsonSerializer.Serialize(file,value,Json);
    }
}
