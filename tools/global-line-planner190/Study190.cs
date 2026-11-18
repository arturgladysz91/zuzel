using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;
using Cyclic172;
using Safety153;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal sealed record Start190(string Id, double ReferenceMeters, float OffsetMeters);
internal sealed record Forecast190(bool Complete, double Seconds, double Progress, double Swept,
    double BetaDegrees, double SteerDegrees, double LeanDegrees, double SlipLossJ,
    double FrontUse, double RearUse, double PeakMps, double MaxOffsetError, int Reviews);
internal sealed record PlanFile190(Start190 Start, LineShape190 First, LineShape190 Second,
    LaneKnot190[] FirstPoints, LaneKnot190[] SecondPoints, SpeedProfile190 Profile, string Pace, Forecast190 Forecast);
internal sealed record NativeResult190(string Id, bool StageA, bool Legal, int Laps, double? FinishSeconds,
    double EndSeconds, double EndReference, double MinSweep, double MaxBetaDegrees, string Reason,
    double SimulationCpuSeconds, double DecisionWallMsMean, double DecisionWallMsMax);

internal static class Study190
{
    private static readonly TrackFrame Frame = NativeWorld172.Frame();
    private static readonly TrackStateSnapshot Surface = MultiActionExperiment135.Surface(Frame);
    private static readonly WorldParameters Physics = FreeLearningExperiment.Parameters();
    private static readonly HandlingSettings Settings = new();
    private static readonly RiderTuning Tuning = new();
    private static readonly Schedule188 Timing = new(0,.05,.05);
    private static readonly Start190 Match = new("V5",-1.762,4.192f);
    private static readonly Start190 Confirmation = new("V2",-1.267,3.847f);
    private static readonly JsonSerializerOptions Json = new(FreeLearningExperiment.Json) { WriteIndented = false };
    private const float Mass=147, Inertia=52.72097f;
    private const double TailSeconds=1.01;
    private const double MinPlanningSweep=.75;
    private const int ShortPerSector=17;
    private const int PrecisePairCount=21;

    private static int Main(string[] args)
    {
        if (args is ["--study-190","check"])
        {
            Schedule188.Check();
            return 0;
        }
        if (args is ["--study-190","plan",var planDir])
        {
            Directory.CreateDirectory(planDir);
            Plan(planDir);
            return 0;
        }
        if (args is ["--study-190","native",var arm,var evidence] &&
            arm is "baseline" or "planner" or "confirmation")
        {
            Native(evidence,arm);
            return 0;
        }
        Console.Error.WriteLine("Study #190 is OFF. Use --study-190 check, plan DIR, or native ARM DIR.");
        return 2;
    }

    private static RiderSpec Spec(Start190 start) => NativeWorld172.Rider(StartingGate.B) with
    {
        ReferenceMeters=start.ReferenceMeters, OffsetMeters=start.OffsetMeters, TargetOffsetMeters=start.OffsetMeters
    };

    private static HandlingController Controller(RiderSpec spec,LapPlan190? plan) =>
        new(Frame,Surface,Physics,spec,Mass,Inertia,Settings,Tuning,Timing,plan);
    private static Pace190 Pace(LineCatalog190 catalog,RiderSpec spec) =>
        new(Frame,Surface,Physics,catalog,spec,Mass,Inertia,Settings);
    private static double Cpu(Process process) => process.TotalProcessorTime.TotalSeconds;

    private static void Plan(string dir)
    {
        using var process = Process.GetCurrentProcess();
        double began=Cpu(process);
        var catalog=new LineCatalog190(Frame);
        var first=catalog.Build(0);
        var second=catalog.Build(1);
        if (first.Accepted.Length<500 || second.Accepted.Length<500)
            throw new InvalidOperationException($"Geometry got {first.Accepted.Length}/{second.Accepted.Length}, requires 500 per bend.");
        double geometryCpu=Cpu(process)-began;
        Console.WriteLine($"Geometry: {first.Accepted.Length}/{second.Accepted.Length} accepted; CPU {geometryCpu:F3} s");
        var spec=Spec(Match);
        var pace=Pace(catalog,spec);
        (Sector190 Sector, SpeedProfile190 Profile)[] Rank(Catalog190 list) => list.Accepted
            .Select(s=>(Sector:s,Profile:pace.Profile(s,null,Pace190.NominalUse,1)))
            .OrderBy(x=>x.Profile.Seconds).ThenBy(x=>x.Sector.Shape.Id).ToArray();
        var rank1=Rank(first);
        var rank2=Rank(second);
        static Sector190[] Shortlist((Sector190 Sector, SpeedProfile190 Profile)[] rank)
        {
            var mandatory=rank.GroupBy(x=>x.Sector.Shape.Family,StringComparer.Ordinal)
                .Select(g=>g.First().Sector).ToArray();
            var options=mandatory.Concat(rank.Take(8).Select(x=>x.Sector)).Distinct().ToList();
            foreach (var (candidate, _) in rank)
            {
                if (options.Count==ShortPerSector) break;
                var shape=candidate.Shape;
                if (options.All(x=>Math.Abs(x.Shape.Apex-shape.Apex)+Math.Abs(x.Shape.Entry-shape.Entry)+
                    Math.Abs(x.Shape.Exit-shape.Exit)+5*Math.Abs(x.Shape.ApexFraction-shape.ApexFraction)>3.5))
                    options.Add(candidate);
            }
            return options.ToArray();
        }
        var shortlist1=Shortlist(rank1);
        var shortlist2=Shortlist(rank2);
        var pairs=(from a in shortlist1 from b in shortlist2
            let p=pace.Profile(a,b,Pace190.NominalUse,1)
            select new Candidate190(a,b,p)).OrderBy(x=>x.Profile.Seconds)
            .ThenBy(x=>x.Id,StringComparer.Ordinal).ToArray();
        double fastCpu=Cpu(process)-began-geometryCpu;
        var joint=new List<Candidate190>(pairs.Take(9));
        var usedFirst=joint.Select(p=>p.First.Shape.Id).ToHashSet();
        var usedSecond=joint.Select(p=>p.Second.Shape.Id).ToHashSet();
        foreach(var pair in pairs)
        {
            if(joint.Count==PrecisePairCount) break;
            if(!usedFirst.Contains(pair.First.Shape.Id)||!usedSecond.Contains(pair.Second.Shape.Id))
            {
                joint.Add(pair); usedFirst.Add(pair.First.Shape.Id); usedSecond.Add(pair.Second.Shape.Id);
            }
        }
        foreach(var pair in pairs)
        {
            if(joint.Count==PrecisePairCount) break;
            if(!joint.Contains(pair)) joint.Add(pair);
        }
        (double Use,double Coast,string Id)[] profileOptions=[(.94,1,"nominal"),(.98,1.13,"attack"),(.90,.89,"reserve")];
        var scores=new List<(Candidate190 Pair,SpeedProfile190 Profile,string Pace,Forecast190 Forecast)>();
        double beganPrecise=Cpu(process);
        foreach(var candidate in joint)
        foreach(var option in profileOptions)
        {
            var profile=option.Id=="nominal"?candidate.Profile:
                pace.Profile(candidate.First,candidate.Second,option.Use,option.Coast);
            var plan=new LapPlan190(Frame,catalog,candidate,profile,spec,pace.Ratio,option.Id);
            var forecast=Forecast(spec,plan,Frame.LapMeters*2);
            scores.Add((candidate,profile,option.Id,forecast));
        }
        var eligible=scores.Where(x=>x.Forecast.Complete && x.Forecast.Swept>=MinPlanningSweep)
            .OrderBy(x=>Math.Round(x.Forecast.Seconds,2))
            .ThenBy(x=>x.Forecast.SlipLossJ)
            .ThenByDescending(x=>x.Forecast.Swept)
            .ThenBy(x=>x.Forecast.FrontUse+x.Forecast.RearUse)
            .ThenBy(x=>x.Forecast.Reviews)
            .ThenBy(x=>x.Pair.Id,StringComparer.Ordinal).ToArray();
        var full = new List<object>();
        PlanFile190? selected=null;
        foreach (var entry in eligible.Take(8))
        {
            var (pair,profile,style,_) = entry;
            var candidate=new LapPlan190(Frame,catalog,pair,profile,spec,pace.Ratio,style);
            var forecast=Forecast(spec,candidate,3*Frame.LapMeters);
            full.Add(new {candidate.Id,Forecast=forecast});
            if (!forecast.Complete || forecast.Swept<MinPlanningSweep) continue;
            if (selected is null || forecast.Seconds<selected.Forecast.Seconds)
                selected=new(Match,pair.First.Shape,pair.Second.Shape,pair.First.Points,pair.Second.Points,
                    profile,style,forecast);
        }
        double baselineStart=Cpu(process);
        var basePrediction=Forecast(spec,null,3*Frame.LapMeters);
        double baselineForecastCpu=Cpu(process)-baselineStart;
        Save(Path.Combine(dir,"planowanie.json"),new
        {
            GeometryCpuSeconds=geometryCpu, FastCpuSeconds=fastCpu, DetailedCpuSeconds=baselineStart-beganPrecise,
            BaselineForecastCpuSeconds=baselineForecastCpu, TotalCpuSeconds=Cpu(process)-began,
            Frame.LapMeters,catalog.Origin,catalog.Step, Passes=3, PreciseCandidatePairs=joint.Count,
            PairCount=pairs.Length,
            Catalogs=new[]{Describe(first,rank1,shortlist1),Describe(second,rank2,shortlist2)},
            Pairs=pairs.Select(x=>new{x.Id,x.Distance,x.Profile.Seconds}).ToArray(),
            Forecasts=scores.Select(x=>new{ x.Pair.Id, x.Pace, x.Profile.Seconds,x.Forecast }).ToArray(),
            FullForecasts=full, ReferenceForecast=basePrediction, Best=selected is null?null:
                new {selected.First,selected.Second,selected.Pace,selected.Forecast},
            Track=Enumerable.Range(0,639).Select(i=>
            {
                double s=i*Frame.LapMeters/638;
                return new {S=s,Inner=Frame.At(s,-1).Position,Outer=Frame.At(s,Frame.Shape!.Width(s)-1).Position};
            }).ToArray(),
            TopGeometry=pairs.Take(13).Select(p=>new {p.Id,First=p.First.Points,Second=p.Second.Points}).ToArray()
        });
        if(selected is null)
            throw new InvalidOperationException("No candidate passed the S3 forecast legality gate.");
        Save(Path.Combine(dir,"wybor.json"),selected);
        Console.WriteLine($"Chosen: {selected.First.Id}/{selected.Second.Id}/{selected.Pace} forecast {selected.Forecast.Seconds:F3}s " +
            $"swept {selected.Forecast.Swept:F3} versus baseline forecast {basePrediction.Seconds:F3}s; " +
            $"{Cpu(process)-began:F3} CPU s. Full checks: {full.Count}");

        static object Describe(Catalog190 c,(Sector190 Sector,SpeedProfile190 Profile)[] ranks,Sector190[] shortlist) =>
            new {c.Draws,c.Boundary,c.Similar,Accepted=c.Accepted.Length,
                Shortlist=shortlist.Select(x=>x.Shape.Id).ToArray(),
                Ranking=ranks.Select(x=>new{x.Sector.Shape,x.Profile.Seconds}).ToArray()};
    }

    private static Forecast190 Forecast(RiderSpec spec, LapPlan190? plan, double goal)
    {
        var (position,road)=Frame.At(spec.ReferenceMeters,spec.OffsetMeters);
        var body=new RiderView(1,position,default,(float)road.Heading,0,road,spec.ReferenceMeters,0,
            RiderActivity.Racing,0,spec.OffsetMeters,0,Frame.Context(road,spec.ReferenceMeters));
        var launch=LaunchParameters144.Default;
        var state=new HandlingForecast(body,0,0,0,0,new CrankClutch144().Omega);
        var control=Controller(spec,plan);
        var boundary=new RacingLineGeometry(Frame);
        var active=new FreeRacingAction(0,0);
        FreeRacingCommand[] pending=[];
        var gap=boundary.Clearance(body);
        double sweep=double.PositiveInfinity, beta=0, steer=0, lean=0, loss=0, front=0, rear=0, peak=0;
        double finish=0;
        int reviews=0;
        var track=Surface.SampleSurface(0,2);
        for(int tick=0;tick<8500 && finish==0;tick++)
        {
            if(pending.Length>0 && pending[0].ActivationAt<=state.Time+1e-9)
            {
                active=pending[0].Action;
                pending=pending[1..];
            }
            if(tick%5==0)
            {
                var input=new FreeRacingInput(state.Rider,state.Time,state.Wheel,state.Steer,active,pending,state.Time);
                var decision=control.Choose(input,new CrankClutch144(launch,state.Shaft),state.Lean);
                pending=[..pending,new(state.Time,Timing.Due(state.Time,.24,pending.LastOrDefault()),decision.Action)];
                reviews++;
            }
            var next=HandlingPreview.Advance(state,.01,active,pending,track,Settings,spec,Physics,Frame,Mass,Inertia,launch,
                (forces,clutch)=>
                {
                    loss+=clutch.Wheel.LongitudinalLossJoules-.01*(System.Numerics.Vector2.Dot(forces.FrontForce,forces.FrontVelocity)
                        +forces.RearLateral*System.Numerics.Vector2.Dot(forces.BodyLeft,forces.RearVelocity));
                    front=Math.Max(front,Math.Abs(forces.FrontLateral)/forces.TyreLoads!.FrontCapacityNewtons);
                    rear=Math.Max(rear,clutch.Wheel.RearUtilization);
                });
            var nextGap=boundary.Clearance(next.Rider);
            sweep=Math.Min(sweep,Clearance153.SweepLowerBound(state.Rider,next.Rider,.01,gap,nextGap));
            gap=nextGap;
            beta=Math.Max(beta,Math.Abs(next.Rider.SlideRadians)*180/Math.PI);
            steer=Math.Max(steer,Math.Abs(next.Steer)*180/Math.PI);
            lean=Math.Max(lean,Math.Abs(next.Lean)*180/Math.PI);
            peak=Math.Max(peak,next.Rider.Speed);
            if (next.Rider.TotalReferenceMeters>=goal && sweep>=.5 && beta<=45)
                finish=state.Time+.01*(goal-state.Rider.TotalReferenceMeters)/
                    (next.Rider.TotalReferenceMeters-state.Rider.TotalReferenceMeters);
            state=next;
            if(sweep<.5 || beta>45 || state.Time>5 && state.Rider.Speed<.05) break;
        }
        return new(finish>0,finish>0?finish:state.Time,state.Rider.TotalReferenceMeters,sweep,beta,steer,lean,loss,
            front,rear,peak,plan?.MaxAbsOffsetError??0,reviews);
    }

    private static void Native(string dir,string arm)
    {
        var planFile=Path.Combine(dir,"wybor.json");
        var file=Read<PlanFile190>(planFile);
        using var gate=new FileStream(Path.Combine(dir,"native.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        string[] order=["baseline","planner","confirmation"];
        int worlds=Directory.GetFiles(dir,"*.reserved").Length;
        if (worlds>=3 || arm!=order[worlds]) throw new InvalidOperationException("Native world budget or order.");
        if (arm=="confirmation")
        {
            var baseline=Read<NativeResult190>(Path.Combine(dir,"baseline.result.json"));
            var planner=Read<NativeResult190>(Path.Combine(dir,"planner.result.json"));
            if (!baseline.Legal || !planner.Legal || baseline.FinishSeconds is not double b ||
                planner.FinishSeconds is not double p || b-p<Math.Max(1,.03*b))
                throw new InvalidOperationException("Confirmation requires a legal gain above the predefined threshold.");
        }
        var digest=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(planFile)));
        var binary=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(Study190).Assembly.Location)));
        foreach (var prev in order.Take(worlds))
        {
            if (File.ReadAllText(Path.Combine(dir,prev+".reserved"))!=digest+"\n"+binary)
                throw new InvalidOperationException("Frozen plan or executable changed.");
        }
        using(var marker=new FileStream(Path.Combine(dir,arm+".reserved"),FileMode.CreateNew))
        using(var writer=new StreamWriter(marker)) writer.Write(digest+"\n"+binary);
        var test = arm=="confirmation"?Confirmation:Match;
        if (file.Start!=Match || !file.Forecast.Complete) throw new InvalidOperationException("Mismatched plan.");
        var spec=Spec(test);
        var catalog=new LineCatalog190(Frame);
        var candidate=new Candidate190(new(file.First,file.FirstPoints),new(file.Second,file.SecondPoints),file.Profile);
        var plan=arm=="baseline"?null:new LapPlan190(Frame,catalog,candidate,file.Profile,spec,Pace(catalog,spec).Ratio,file.Pace);
        var control=Controller(spec,plan);
        var rig=new HandlingRig(Settings);
        var decisions=new List<HandlingDecision>();
        var elapsed=new List<double>();
        double goal=3*Frame.LapMeters;
        using var world=new HandlingWorld(new(Enabled:true,RequiredLaps:3,MaximumSeconds:85,SoloGate:StartingGate.B),
            diagnosticFrame:Frame,diagnosticRider:spec,safetyHalfMeter:true,stopReference:goal,postSeconds:TailSeconds,
            handling:rig,timing188:Timing);
        var run=world.Run(arm,"K1",(input,shaft,_,_,_,_,mass,inertia,_)=>
        {
            if (mass!=Mass || Math.Abs(inertia-Inertia)>1e-5) throw new InvalidOperationException("Frozen mass or inertia changed.");
            long started=Stopwatch.GetTimestamp();
            var choice=control.Choose(input,shaft,rig.Lean);
            elapsed.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            decisions.Add(choice);
            return new(new(input.Time,choice.Action,FreeActionSafety.Uncertain,false,[],choice.Queries,choice.Forecasts),0);
        });
        var rider=run.Riders.Single();
        var samples=rider.Samples;
        double sweep=samples.Skip(1).Min(s=>s.Certificate??0);
        double beta=samples.Max(s=>Math.Abs(s.Input.Rider.SlideRadians)*180/Math.PI);
        bool legal=rider.CompletedLaps==3 && rider.FinishSeconds is double finish && run.EndSeconds-finish>=1 &&
            run.Outcome=="completed" && run.Contacts.Length==0 && sweep>=.5 && beta<=45;
        var result=new NativeResult190(arm,false,legal,rider.CompletedLaps,rider.FinishSeconds,run.EndSeconds,
            samples[^1].Input.Rider.TotalReferenceMeters,sweep,beta,run.Outcome,run.TotalCpuSeconds,
            elapsed.DefaultIfEmpty().Average(),elapsed.DefaultIfEmpty().Max());
        using(var data=new FileStream(Path.Combine(dir,arm+".json.gz"),FileMode.CreateNew))
        using(var gzip=new GZipStream(data,CompressionLevel.Fastest))
            JsonSerializer.Serialize(gzip,new {Run=run,Outcome=result,Decisions=decisions,Lean=rig.LeanSamples,
                test,Settings,Tuning,Goal=goal,Timing,DecisionMilliseconds=elapsed,PlanId=plan?.Id,
                MaxAbsOffsetError=plan?.MaxAbsOffsetError},Json);
        Save(Path.Combine(dir,arm+".result.json"),result);
        Console.WriteLine($"{arm}: legal={legal} finish={result.FinishSeconds:F5} laps={result.Laps} " +
            $"swept={sweep:F4} beta={beta:F3} CPU={run.TotalCpuSeconds:F3} s {run.Outcome}");
    }

    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllBytes(path),Json)!;

    private static void Save(string path,object value)
    {
        using var stream=new FileStream(path,FileMode.CreateNew);
        JsonSerializer.Serialize(stream,value,Json);
    }
}

