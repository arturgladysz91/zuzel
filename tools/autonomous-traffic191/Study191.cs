using System.Diagnostics;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;
using CoreSim.Setup;
using Cyclic172;
using Safety153;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static readonly TrackFrame Frame=NativeWorld172.Frame();
    private static readonly Track191 Track=new(Frame);
    private static readonly WorldParameters Physics=FreeLearningExperiment.Parameters();
    private static readonly TrackStateSnapshot Surface=MultiActionExperiment135.Surface(Frame);
    private static readonly HandlingSettings Handling=new();
    private static readonly Schedule188 Timing=new(0,.05,.05);
    private static readonly JsonSerializerOptions Json=new(FreeLearningExperiment.Json){WriteIndented=false};
    private const float Mass=147,Inertia=52.72097f;
    private const int Laps=3;

    private sealed record StadiumConfig(double[]? Ratios,string? Controller=null);

    private static int Main(string[] args)
    {
#if SPEEDWAY200_INTEGRATION
        if(args is ["--check-speedway200"])
        {
            CheckStadium200();
            return 0;
        }
#endif
        if (args is ["--study-194", .. var wideStudy])
            return Study194(wideStudy);
        if (args is ["--study-193", .. var study])
            return Study193(study);
        if (args is ["--stadium-race", var configPath, var outputDirectory])
        {
            StadiumRace(configPath, outputDirectory);
            return 0;
        }
        if(args is ["--study-191","check",var checkDir])
        {
            Directory.CreateDirectory(checkDir);
            Checks(checkDir);
            return 0;
        }
        if(args is ["--study-191","forecast",var forecastArm,var folder] && forecastArm is "base" or "dynamic" or "alternate")
        {
            ForecastOnly(forecastArm,folder);
            return 0;
        }
        if(args is ["--study-191","native",var arm,var nativeDir] && arm is "base" or "dynamic" or "alternate")
        {
            Native(arm,nativeDir);
            return 0;
        }
        Console.Error.WriteLine("Study #191 OFF. Explicit check, forecast or native required.");
        return 2;
    }

    private static RiderSpec[] Grid(string arm)
    {
        float[] gears=arm=="alternate"?[.5f,1,.5f,0]:[0,.5f,1,.5f];
        StartingGate[] gates=[StartingGate.A,StartingGate.B,StartingGate.C,StartingGate.D];
        return gates.Select((g,i)=>
        {
            var rider=TrafficWorld191.Rider(g,i+1);
            return rider with{Setup=new BikeSetup(gears[i],rider.Setup.TractionBias)};
        }).ToArray();
    }

    private static RaceMind191 Mind(RiderSpec spec,bool dynamic) =>
        new(Track,Surface,Physics,spec,Handling,Timing,dynamic);

    private static HandlingForecast Start(RiderSpec spec)
    {
        var wp=Frame.At(spec.ReferenceMeters,spec.OffsetMeters);
        var body=new RiderView(spec.Profile.Id,wp.Position,default,(float)wp.Road.Heading,0,
            wp.Road,spec.ReferenceMeters,0,RiderActivity.Racing,0,spec.OffsetMeters,0,Frame.Context(wp.Road,0));
        return new(body,0,0,0,0,new CrankClutch144().Omega);
    }

    private static void Checks(string dir)
    {
        Schedule188.Check();
        var specs=Grid("dynamic");
        var scenario=specs.Select(Start).ToArray();
        var first=new Curve191(0,new(3.2,.026,-.004),8.73,32);
        var join=first.At(15);
        var splice=new Curve191(15,join,1.23,31);
        if(splice.At(15)!=join || splice.At(46)!=Lateral191.Rest(1.23) ||
            Math.Abs(Track.At(first,15).Course-Track.At(splice,15).Course)>1e-12 ||
            Math.Abs(Track.At(first,15).Curvature-Track.At(splice,15).Curvature)>.0005)
            throw new InvalidOperationException("Discontinuous C2 replan.");
        var contactA=scenario[0].Rider with {Velocity=new(18,7),Position=new(10,0)};
        var contactB=scenario[1].Rider with {Velocity=new(19,-6),Position=new(10,2.2f)};
        if(ContactStep191.Limit(new[]{contactA,contactB},.01,Physics.NearbyMaximumTravelMeters)>=.01 ||
            ContactStep191.Limit(new[]{contactA,contactB with{Position=new(10,20)}},.01,Physics.NearbyMaximumTravelMeters)!=.01)
            throw new InvalidOperationException("Close-pair partition differs.");
        var brush=ContactStep191.Consequence(contactA,contactB,specs[0],Mass,Mass,Surface,2,12,.10);
        var crash=ContactStep191.Consequence(contactA,contactB,specs[0],Mass,Mass,Surface,2,500,10);
        if(brush.Rider!=contactA || crash.Rider.Activity!=RiderActivity.Crashed ||
            crash.Rider.Position!=contactA.Position || crash.Rider.Velocity!=contactA.Velocity)
            throw new InvalidOperationException("Gameplay consequence added an impulse.");

        var gearing=specs.Take(3).Select(spec=>
        {
            var geometry=Frame.At(15,5);
            var start=new RiderView(spec.Profile.Id,geometry.Position,new(20,0),0,0,geometry.Road,15,0,
                RiderActivity.Racing,.65f,5,0);
            var rigid=Engine500.Evaluate(8500*Math.PI/30/(10-2*spec.Setup.Gearing),spec.Setup,1);
            var state=new HandlingForecast(start,3,20/.33,0,0,20/.33*9);
            var evaluated=HandlingPreview.Advance(state,.60,new(.005f,1),[],Surface.SampleSurface(0,2),
                Handling,spec,Physics,Frame,Mass,Inertia,LaunchParameters144.Default,(force,power)=>
                {
                    var grip=force.TyreLoads!;
                    if(power.Wheel.RearUtilization>1.000001 || Math.Abs(force.FrontLateral)>grip.FrontCapacityNewtons+.001 ||
                        power.ClutchHeatJoules < -1e-6 || power.Wheel.LongitudinalLossJoules < -1e-6)
                        throw new InvalidOperationException("Nonphysical H25 forecast.");
                });
            return new {spec.Setup.Gearing,rigid.OverallRatio,WheelTorqueAt8500=rigid.AvailableWheelTorqueNewtonMeters,
                CutoffKph=Engine500.CutoffRpm*Math.PI/30*.33/rigid.OverallRatio*3.6,
                BeforeSpeed=state.Rider.Speed,AfterSpeed=evaluated.Rider.Speed,
                AfterRpm=evaluated.Shaft*30/Math.PI,Wheelspin=(evaluated.Wheel*.33-evaluated.Rider.ForwardSpeed)/20};
        }).ToArray();
        if(gearing.Select(x=>x.AfterSpeed).Distinct().Count()!=3 ||
            gearing[0].WheelTorqueAt8500<=gearing[2].WheelTorqueAt8500 ||
            gearing[0].CutoffKph>=gearing[2].CutoffKph)
            throw new InvalidOperationException("Gearing integration regression.");

        // All minds run from the same observed geometry. Reversing observation
        // order is tested with sorted snapshots supplied by the world adapter.
        var own=OnTrack(specs[0],91,5.1,21);
        var blocker=OnTrack(specs[1],94.1,5.1,19.4f);
        var outside=OnTrack(specs[2],91.1,6.65,21f);
        var inside=OnTrack(specs[3],90.6,3.55,20);
        var behaviors=new List<Decision191>();
        foreach(var set in new[]{new[]{own,blocker,outside,inside},new[]{own,blocker,
            outside with{Position=outside.Position+new Vector2(400,0)},inside with{Position=inside.Position+new Vector2(400,0)}}})
        {
            var mind=Mind(specs[0],true);
            var snap=new WorldSnapshot(4,Array.AsReadOnly(set.OrderBy(v=>v.Id).ToArray()));
            var request=new FreeRacingInput(own,4,own.Speed/.33,0f,new(.03f,.35f),[],4);
            mind.Choose(request,new CrankClutch144(omega:own.Speed/.33*10),.64,snap).Action.Validate();
            var result=mind.Journal.Single();
            if(result.Rivals.Length!=3 || result.Options is null || result.Options.Length<4)
                throw new InvalidOperationException("Opponents did not enter the candidate forecast.");
            behaviors.Add(result);
        }
        if(!behaviors[0].Options!.Any(x=>x.Reason=="occupied-corridor") ||
            !behaviors.SelectMany(x=>x.Options!).Any(x=>x.Feasible))
            throw new InvalidOperationException("Occupancy veto or usable candidate regression.");
        Save(Path.Combine(dir,"preflight.json"),new{Passed=true,Gearing=gearing,Behaviors=behaviors,
            Contact=new{brush.Observation,Crash=crash.Observation}});
        Console.WriteLine("PASS pending, continuous splice, separate rider observations, occupancy veto, axle force budgets, gearing and contact consequences (no Box2D).");
    }

    private static RiderView OnTrack(RiderSpec spec,double s,double offset,float speed)
    {
        var at=Frame.At(s,offset);
        return Start(spec).Rider with { Position=at.Position,Road=at.Road,Heading=(float)at.Road.Heading+.10f,
            AngularVelocity=(float)(speed*Frame.Shape!.At(s).Curvature),Velocity=at.Road.Tangent*speed,
            TotalReferenceMeters=s,TargetOffsetMeters=(float)offset,Drive=.35f,Context=Frame.Context(at.Road,s)};
    }

    private static void ForecastOnly(string arm,string dir)
    {
        Directory.CreateDirectory(dir);
        using var process=Process.GetCurrentProcess();
        double cpu=process.TotalProcessorTime.TotalSeconds;
        var specs=Grid(arm);
        var states=specs.Select(Start).ToArray();
        var minds=specs.Select(s=>Mind(s,arm!="base")).ToArray();
        var active=Enumerable.Repeat(new FreeRacingAction(0,0),4).ToArray();
        FreeRacingCommand[][] pending=[[],[],[],[]];
        double[] finish=new double[4],swept=[10,10,10,10];
        var boundary=new RacingLineGeometry(Frame);
        double minGap=10,minGapTime=0;
        int step;
        for(step=0;step<7500 && finish.Any(t=>t==0);step++)
        {
            var snapshot=new WorldSnapshot(states[0].Time,Array.AsReadOnly(states.Select(s=>s.Rider).ToArray()));
            for(int i=0;i<4;i++)
            {
                if(pending[i].Length>0 && pending[i][0].ActivationAt<=states[i].Time+1e-9)
                {
                    active[i]=pending[i][0].Action; pending[i]=pending[i][1..];
                }
                if(step%5==0)
                {
                    var r=states[i];
                    var input=new FreeRacingInput(r.Rider with{Drive=active[i].Drive},r.Time,r.Wheel,r.Steer,
                        active[i],pending[i],r.Time);
                    var next=minds[i].Choose(input,new CrankClutch144(omega:r.Shaft),r.Lean,snapshot);
                    pending[i]=[..pending[i],new(r.Time,r.Time+.05,next.Action)];
                }
            }
            var old=(HandlingForecast[])states.Clone();
            for(int i=0;i<4;i++)
            {
                states[i]=HandlingPreview.Advance(old[i],.01,active[i],pending[i],Surface.SampleSurface(0,2),
                    Handling,specs[i],Physics,Frame,Mass,Inertia,LaunchParameters144.Default);
                var r=states[i].Rider;
                swept[i]=Math.Min(swept[i],Clearance153.SweepLowerBound(old[i].Rider,r,.01,
                    boundary.Clearance(old[i].Rider),boundary.Clearance(r)));
                if(finish[i]==0&&r.TotalReferenceMeters>=Frame.LapMeters*Laps) finish[i]=states[i].Time;
            }
            for(int i=0;i<4;i++)
            for(int j=i+1;j<4;j++)
            {
                if(Vector2.DistanceSquared(states[i].Rider.Position,states[j].Rider.Position)>9) continue;
                double gap=MechanicalSeparation.Between(ContactStep191.Pose(states[i].Rider),
                    ContactStep191.Pose(states[j].Rider)).SignedMeters;
                if(gap<minGap){minGap=gap;minGapTime=states[i].Time;}
            }
            if(swept.Min()<.5 || states.Any(s=>Math.Abs(s.Rider.SlideRadians)>Math.PI/4))break;
        }
        var result=new{Arm=arm,Finish=finish,Swept=swept,MinGap=minGap,MinGapTime=minGapTime,
            ForecastSeconds=.01*(step+1),CpuSeconds=process.TotalProcessorTime.TotalSeconds-cpu,
            State=states,Decisions=minds.Select(x=>x.Journal).ToArray()};
        Save(Path.Combine(dir,"forecast-"+arm+".json.gz"),result,true);
        Console.WriteLine($"{arm} forecast finish {string.Join('/',finish.Select(x=>x.ToString("F2")))} " +
            $"swept {swept.Min():F3} min bike gap {minGap:F3} at {minGapTime:F2}; CPU {result.CpuSeconds:F2}s");
    }

    private static void Native(string arm,string dir)
    {
        string[] order=["base","dynamic","alternate"];
        using var process=Process.GetCurrentProcess();
        using var mutex=new FileStream(Path.Combine(dir,"native.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        var binary=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Study191).Assembly.Location)));
        int count=Directory.GetFiles(dir,"*.reserved").Length;
        if(count>=3 || order[count]!=arm) throw new InvalidOperationException("Native budget exhausted or wrong order.");
        if(!File.Exists(Path.Combine(dir,"preflight.json"))) throw new InvalidOperationException("Missing preflight.");
        foreach(var prev in order.Take(count))
            if(File.ReadAllText(Path.Combine(dir,prev+".reserved"))!=binary)
                throw new InvalidOperationException("The compared executable changed.");
        if(arm=="alternate")
        {
            using var previous=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(dir,"dynamic.result.json")));
            if(!previous.RootElement.GetProperty("Complete").GetBoolean())
                throw new InvalidOperationException("Additional grid requires a complete dynamic race.");
        }
        using(var f=new FileStream(Path.Combine(dir,arm+".reserved"),FileMode.CreateNew))
        using(var w=new StreamWriter(f))w.Write(binary);

        RunRace(arm,dir,Grid(arm));
    }

    private static void StadiumRace(string configPath,string dir)
    {
        var config=JsonSerializer.Deserialize<StadiumConfig>(File.ReadAllBytes(configPath),Json);
        var ratios=config?.Ratios;
#if SPEEDWAY200_INTEGRATION
        if(config?.Controller=="speedway200")
        {
            RunStadium200(dir,ratios);
            return;
        }
#endif
        if(ratios is null || ratios.Length is not (1 or 2 or 4) ||
            ratios.Any(r=>!double.IsFinite(r) || r<8 || r>10))
            throw new ArgumentException("Wymagane dwa lub cztery przełożenia z zakresu 8:1–10:1.");
        var gates=ratios.Length==1?new[]{StartingGate.D}:ratios.Length==2?new[]{StartingGate.B,StartingGate.C}:
            new[]{StartingGate.A,StartingGate.B,StartingGate.C,StartingGate.D};
        var specs=gates.Select((gate,i)=>
        {
            var rider=TrafficWorld191.Rider(gate,i+1);
            // H25 resolves the ratio from Gearing on every force call with 10-2*Gearing.
            var gearing=(float)((Engine500Parameters.Default.ShortOverallRatio-ratios[i])/
                (Engine500Parameters.Default.ShortOverallRatio-Engine500Parameters.Default.LongOverallRatio));
            return rider with{Setup=new BikeSetup(gearing,rider.Setup.TractionBias)};
        }).ToArray();
        if (config?.Controller is not (null or "base" or "wide194"))
            throw new ArgumentException("Nieznany wariant sterowania.");
        RunRace("race",dir,specs,gates,wideDriving:config?.Controller=="wide194");
    }

    private static void RunRace(string arm,string dir,RiderSpec[] specs,StartingGate[]? gates=null,bool experimentalExit=false,bool wideDriving=false)
    {
        var rigs=specs.Select(_=>new HandlingRig(Handling)).ToArray();
        var minds=wideDriving?null:specs.Select(s=>new RaceMind191(Track,Surface,Physics,s,Handling,Timing,arm!="base",experimentalExit)).ToArray();
        var wide=wideDriving?CreateWideMinds(specs):null;
        using var world=new TrafficWorld191(new(Enabled:true,RequiredLaps:Laps,MaximumSeconds:85),Frame,specs,rigs,Timing,gates);
        var run=world.Run(arm,"K1",(input,shaft,spec,snapshot)=>
        {
            int i=spec.Profile.Id-1;
            var choice=wide is null?minds![i].Choose(input,shaft,rigs[i].Lean,snapshot):
                wide[i].Choose(input,shaft,rigs[i].Lean,snapshot);
            return new(new(input.Time,choice.Action,FreeActionSafety.Uncertain,false,[],0,1),0);
        });
        var journal=wide is null?minds!.Select(x=>x.Journal).ToArray():wide.Select(x=>x.Journal).ToArray();
        var complete=run.Outcome=="completed" && run.Riders.All(r=>r.CompletedLaps==Laps&&r.FinishSeconds is not null);
        var results=new{Arm=arm,Controller=wideDriving?"wide194":experimentalExit?"exit193":"base",Complete=complete,run.EndSeconds,run.Outcome,run.NativeSteps,run.TotalCpuSeconds,run.Fingerprint,
            world.MinimumBikeGap,world.ContactManifolds,world.SolvedPoints,
            Finish=run.Riders.Select(r=>new{r.Spec.Profile.Id,r.FinishSeconds,r.CompletedLaps,r.Outcome,
                Sweep=r.Samples.Skip(1).Select(x=>x.Certificate??0).DefaultIfEmpty(0).Min(),
                Beta=r.Samples.Max(x=>Math.Abs(x.Input.Rider.SlideRadians)*180/Math.PI)}).ToArray(),
            Stages=journal.Select(x=>new{TacticalWallMs=x.Sum(d=>d.TacticMs),PlanningWallMs=x.Sum(d=>d.PlannerMs),
                Fast50WallMs=x.Sum(d=>d.Fast50Ms),Reviews=x.Count,Replans=x.Count(d=>d.Replanned),
                Tactical=x.Count(d=>d.Tactical)}).ToArray()};
        Save(Path.Combine(dir,arm+".result.json"),results);
        Save(Path.Combine(dir,arm+".json.gz"),new{Run=run,Result=results,Timing,Handling,
            Lean=rigs.Select(r=>r.LeanSamples).ToArray(),Journal=journal,world.ContactPoints,world.Consequences},true);
        Console.WriteLine($"{arm} {run.Outcome} t={run.EndSeconds:F3}s L={string.Join('/',run.Riders.Select(r=>r.CompletedLaps))} " +
            $"contacts={world.SolvedPoints} CPU={run.TotalCpuSeconds:F3}s");
    }

    private static void Save(string path,object value,bool gzip=false)
    {
        using var stream=new FileStream(path,FileMode.CreateNew);
        if(gzip)
        {
            using var compressed=new GZipStream(stream,CompressionLevel.Fastest);
            JsonSerializer.Serialize(compressed,value,Json);
        }
        else JsonSerializer.Serialize(stream,value,Json);
    }
}

