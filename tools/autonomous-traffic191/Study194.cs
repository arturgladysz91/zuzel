using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using CoreSim;
using CoreSim.PhysicalSpace;
using Cyclic172;
using Safety153;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static readonly string[] Arms194 = ["solo-inside","solo-wide","four-nominal","four-alternate"];
    private sealed record PreviewPoint194(int Rider,double Time,double S,double X,double Y,double Offset,
        double Speed,double Course,double Heading,double Beta,double Yaw,double Lean,double Steer,double Drive,
        double Outer,double Sweep,double Target,string Reason,double? Fx,double? Fy,double? RearUse);
    private static RaceMind194[] CreateWideMinds(RiderSpec[] specs)
    {
        var field=new WideTrack194(Track);
        return specs.Select(spec=>new RaceMind194(field,Surface,Physics,spec,Handling,Timing,Mass,Inertia)).ToArray();
    }

    private static (StartingGate[] Gates,RiderSpec[] Specs) Grid194(string arm)
    {
        StartingGate[] gates=arm switch
        {
            "solo-inside"=>[StartingGate.A],
            "solo-wide"=>[StartingGate.D],
            _=>[StartingGate.A,StartingGate.B,StartingGate.C,StartingGate.D]
        };
        double[] ratios=arm switch
        {
            "solo-inside"=>[9],
            "solo-wide"=>[8],
            "four-alternate"=>[9,8,9,10],
            _=>[10,9,8,9]
        };
        return (gates,gates.Select((gate,i)=>Spec193(gate,i+1,ratios[i])).ToArray());
    }

    private static int Study194(string[] args)
    {
        if (args is ["check",var output])
        {
            Check194(output);
            return 0;
        }
        if (args is ["family",var source,var family])
        {
            Family194(source,family);
            return 0;
        }
        if (args is ["preview",var arm,var forecast] && Arms194.Contains(arm))
        {
            Preview194(arm,forecast);
            return 0;
        }
        if (args is ["native",var native, var directory] && Arms194.Contains(native) && native!="four-nominal")
        {
            Directory.CreateDirectory(directory);
            using var guard=new FileStream(Path.Combine(directory,"world-budget.lock"),FileMode.OpenOrCreate,
                FileAccess.ReadWrite,FileShare.None);
            string digest=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Study191).Assembly.Location)));
            var started=Directory.GetFiles(directory,"*.reserved");
            if (started.Length>=Arms194.Length || started.Any(path=>File.ReadAllText(path)!=digest))
                throw new InvalidOperationException("Study #194 native budget or frozen binary mismatch.");
            using (var log=new StreamWriter(new FileStream(Path.Combine(directory,native+".reserved"),FileMode.CreateNew)))
                log.Write(digest);
            var (gates,specs)=Grid194(native);
            RunRace(native,directory,specs,gates,wideDriving:true);
            return 0;
        }
        Console.Error.WriteLine("#194 is OFF. Use --study-194 check, preview or the reserved native arms.");
        return 2;
    }

    private static void Check194(string output)
    {
        Schedule188.Check();
        var field=new WideTrack194(Track);
        var checks=new List<object>();
        foreach (int i in Enumerable.Range(0,field.Bends.Count))
        {
            var bend=field.Bends.At(0,i);
            double offset=field.WideOffset(bend);
            var path=new SectorPath194(field,bend,bend.Entry-12,Lateral191.Rest(2.3),21,3.8,offset,Intent191.PrepareExit,false);
            double minimum=10, maximum=0, swept=10;
            for (double s=bend.Exit+2;s<bend.NextEntry-2;s+=.5)
            foreach (double angle in new[] { -.09,0,.09 })
            {
                var pose=field.Pose(s,offset,angle);
                var shift=field.Pose(s+.2,offset,angle);
                var wall=field.Boundary.Clearance(pose);
                minimum=Math.Min(minimum,wall.Outer);
                maximum=Math.Max(maximum,wall.Outer);
                swept=Math.Min(swept,Clearance153.SweepLowerBound(pose with { Velocity=pose.Road.Tangent*20 },
                    shift with { Velocity=shift.Road.Tangent*20 },.01,wall,field.Boundary.Clearance(shift)));
            }
            if(minimum<WideTrack194.CorridorNearMeters || maximum>WideTrack194.CorridorFarMeters || swept<.5)
                throw new InvalidOperationException("Wide corridor is not a legal whole-footprint straight.");
            foreach (double s in new[] { bend.Entry,path.Apex,bend.Exit,path.Settle,path.Preparation,bend.NextEntry,Frame.LapMeters })
            {
                var a=Track.At(path.At(s-1e-5),s-1e-5);
                var b=Track.At(path.At(s+1e-5),s+1e-5);
                if (Math.Abs(a.Offset-b.Offset)>.0001 || Math.Abs(BikeAngles.Wrap(a.Course-b.Course))>.0001 ||
                    Math.Abs(a.Curvature-b.Curvature)>.0001)
                    throw new InvalidOperationException($"Non-C2 corridor trajectory at {s:F4}.");
            }
            checks.Add(new {bend.Entry,bend.Exit,bend.NextEntry,Offset=offset,Minimum=minimum,Maximum=maximum,Swept=swept});
        }
        Save(output,new {Passed=true,Bends=checks});
        Console.WriteLine("PASS #194 exact capsule straight, swept, seam and C2 path checks. No Box2D.");
    }

    private static void Preview194(string arm,string output)
    {
        var (_,specs)=Grid194(arm);
        var minds=CreateWideMinds(specs);
        var states=specs.Select(Start).ToArray();
        var field=new WideTrack194(Track);
        var surface=Surface.SampleSurface(0,2);
        var active=specs.Select(_=>default(FreeRacingAction)).ToArray();
        FreeRacingCommand[][] pending=specs.Select(_=>Array.Empty<FreeRacingCommand>()).ToArray();
        var finish=new double[specs.Length];
        var swept=Enumerable.Repeat(10d,specs.Length).ToArray();
        var outer=states.Select(x=>field.Boundary.Clearance(x.Rider)).ToArray();
        var trace=new List<PreviewPoint194>();
        var started=Stopwatch.GetTimestamp();
        double pairMinimum=10;
        double pairTime=0;
        string outcome="timeout";
        for(int step=0;step<7500;step++)
        {
            double t=states[0].Time;
            bool review=step%5==0;
            var snapshot=new WorldSnapshot(t,Array.AsReadOnly(states.Select(x=>x.Rider).ToArray()));
            for(int i=0;i<states.Length;i++)
            {
                var state=states[i];
                if(pending[i].Length>0 && pending[i][0].ActivationAt<=t+1e-8)
                {
                    active[i]=pending[i][0].Action;
                    pending[i]=pending[i][1..];
                }
                if(review)
                {
                    var input=new FreeRacingInput(state.Rider with {Drive=active[i].Drive},t,state.Wheel,state.Steer,
                        active[i],pending[i],t);
                    var command=minds[i].Choose(input,new CrankClutch144(omega:state.Shaft),state.Lean,snapshot).Action;
                    pending[i]=[..pending[i],new(t,t+Timing.Delay(t,RiderController.ReactionSeconds(specs[i])),command)];
                }
            }
            bool invalid=false;
            for(int i=0;i<states.Length;i++)
            {
                var state=states[i];
                double fx=0,fy=0,utilization=0;
                states[i]=HandlingPreview.Advance(state,.01,active[i],pending[i],surface,Handling,specs[i],Physics,Frame,Mass,Inertia,
                    LaunchParameters144.Default,(forces,power)=>
                    {
                        fx=forces.RearLongitudinal;
                        fy=forces.RearLateral;
                        utilization=power.Wheel.RearUtilization;
                    });
                var next=states[i];
                var v=next.Rider;
                var margin=field.Boundary.Clearance(v);
                double interval=Clearance153.SweepLowerBound(state.Rider,v,.01,outer[i],margin);
                swept[i]=Math.Min(swept[i],interval);
                outer[i]=margin;
                if(v.TotalReferenceMeters>=Laps*Frame.LapMeters && finish[i]==0)
                    finish[i]=next.Time;
                if(interval<.5 || Math.Abs(v.SlideRadians)>Math.PI/4 || utilization>1.000001)
                {
                    outcome=$"R{i+1}: boundary, beta or contact-force limit";
                    invalid=true;
                }
                if(review || invalid)
                {
                    var d=minds[i].Journal[^1];
                    trace.Add(new(i+1,next.Time,v.TotalReferenceMeters,v.Position.X,v.Position.Y,v.Road.OffsetMeters,
                        v.Speed,v.Heading-v.SlideRadians,v.Heading,v.SlideRadians,v.AngularVelocity,next.Lean,next.Steer,
                        active[i].Drive,margin.Outer,interval,d.Target,d.Reason,fx,fy,utilization));
                }
            }
            for(int i=0;i<states.Length;i++)
            for(int j=i+1;j<states.Length;j++)
            {
                var a=states[i].Rider;
                var b=states[j].Rider;
                if(Vector2.DistanceSquared(a.Position,b.Position)>12) continue;
                double gap=MechanicalSeparation.Between(ContactStep191.Pose(a),ContactStep191.Pose(b)).SignedMeters;
                if(gap<pairMinimum){pairMinimum=gap;pairTime=states[i].Time;}
            }
            if(invalid) break;
            if(finish.All(x=>x>0)){outcome="forecast-complete";break;}
        }
        Save(output,new {Arm=arm,Outcome=outcome,Finish=finish,Swept=swept,PairMinimum=pairMinimum,PairTime=pairTime,
            Seconds=states[0].Time,WallSeconds=Stopwatch.GetElapsedTime(started).TotalSeconds,Trace=trace,
            Journal=minds.Select(x=>x.Journal.ToArray()).ToArray()},true);
        Console.WriteLine($"{arm}: {outcome}; s={states[0].Rider.TotalReferenceMeters:F2}; t={states[0].Time:F2}; " +
            $"swept={swept.Min():F3}; pair={pairMinimum:F3} at {pairTime:F2}.");
    }
}

