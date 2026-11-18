using System.Security.Cryptography;
using Cyclic172;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static double Aligned200(WideTrack194 field,double s,double gap)
    {
        double low=0,high=Frame.PhysicalWidthMeters(Frame.RoadAt(s,0));
        for(int j=0;j<24;j++)
        {
            double offset=(low+high)/2;
            if(field.Boundary.Clearance(field.Pose(s,offset)).Outer>gap)low=offset;
            else high=offset;
        }
        return (low+high)/2;
    }

    private static void NearBand200(string workspace,string output)
    {
        var (seed,_,spec)=Fixture200(workspace,"C");
        var training=Read199<Training199>(Path.Combine(workspace,"outputs","badanie199","training-B-smooth.json.gz"));
        var specField=new WideTrack194(Track);
        var bend=Map199.At(0,1);
        double start=bend.Entry-18,length=10;
        const double diagnosticSweep=.02;
        var archived=training.Winner.Result.Knots!.MinBy(x=>Math.Abs(x.S-start))!.Start;
        var model=Model199(spec);
        var trials=new List<object>();
        int replayable=0;
        foreach(double gap in new[]{.16,.21,.26})
        {
            var at=Frame.At(start,Aligned200(specField,start,gap));
            foreach(double speed in new[]{18d,22,26,30})
            foreach(float gas in new[]{0f,.55f,1f})
            {
                var rider=archived.State.Rider with {Position=at.Position,Road=Frame.Project(at.Position),
                    Velocity=at.Road.Tangent*(float)speed,Heading=(float)at.Road.Heading,
                    TotalReferenceMeters=start,AngularVelocity=0};
                var action=new FreeRacingAction(0,gas);
                double omega=speed/.33;
                var initial=archived with {Active=action,Pending=[],Review=archived.State.Time,
                    State=new(rider,archived.State.Time,omega,0,0,omega*8)};
                string? Gate(RiderView state,double outer) => outer is >=.10 and <=.30 &&
                    Math.Abs(Corridor199.CourseError(state))<=Corridor199.CourseLimit?null:"near-band";
                object Run(double margin,double step,out bool reaches)
                {
                    var tape=Enumerable.Repeat(action,80).ToArray();
                    var cursor=model.Begin(initial,tape,0,start+length,margin,constraint:Gate,stepSeconds:step);
                    var trace=new List<HeadingSample198>();
                    for(int i=0;i<400 && cursor.Outcome is null;i++)
                    {
                        cursor.Advance(1);
                        trace.Add(cursor.Snapshot);
                    }
                    reaches=cursor.Reached;
                    return new{cursor.Outcome,cursor.GoalTime,cursor.Sweep,At=cursor.Snapshot.S,
                        Minimum=trace.Min(p=>p.Outer),Maximum=trace.Max(p=>p.Outer),
                        Start=initial,End=cursor.Snapshot};
                }
                var standard=Run(.50,.01,out _);
                var diagnostic=Run(diagnosticSweep,.01,out bool coarse);
                var refined=Run(diagnosticSweep,.005,out bool fine);
                if(coarse && fine)replayable++;
                trials.Add(new{gap,speed,gas,Standard=standard,Diagnostic=diagnostic,Refined=refined});
            }
        }
        Save(Path.Combine(output,"geometria-blisko-bandy.json.gz"),new{
            StartS=start,Length=length,HistoricalSweep=.50,DiagnosticSweep=diagnosticSweep,
            SyntheticAligned=true,TrainedFrom=seed.Arm,Trials=trials,Replayable=replayable},true);
        Console.WriteLine($"Near-wall aligned diagnostic: {replayable}/{trials.Count} legal in both time steps.");
    }

    private static void NearNative200(string workspace,string output)
    {
        using var budget=new FileStream(Path.Combine(output,"world-budget.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        if(Directory.GetFiles(output,"*.reserved").Length>=6)throw new InvalidOperationException("Native world budget used.");
        var (seed,gate,spec)=Fixture200(workspace,"C");
        var field=new WideTrack194(Track);
        double start=Map199.At(0,1).Entry-18,goal=start+10;
        const double gap=.21;
        float offset=(float)Aligned200(field,start,gap);
        spec=spec with {ReferenceMeters=start,OffsetMeters=offset,TargetOffsetMeters=offset,SpeedMetersPerSecond=22};
        var rig=new HandlingRig(Handling);
        var options=seed.Archive.Run.Options with {MaximumSeconds=2};
        Save(Path.Combine(output,"blisko-powtorzenie.reserved"),new{Arm="blisko-powtorzenie",Goal=goal,Protocol="short-diagnostic",
            Executable=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Entry200).Assembly.Location)))});
        using var world=new TrafficWorld191(options,Frame,[spec],[rig],Timing,[gate],measureThrough:goal,diagnosticSweep:.02);
        var run=world.Run("200-diagnostic-near","K1", (input,_,_,_) =>
            new(new(input.Time,new FreeRacingAction(0,.55f),FreeActionSafety.Uncertain,false,[],0,1),0));
        var trace=run.Riders[0].Samples;
        var swept=trace.Where(x=>x.Certificate.HasValue).Min(x=>x.Certificate!.Value);
        double finalS=trace[^1].Input.Rider.TotalReferenceMeters;
        bool pass=finalS>=goal && swept>=.02 && trace.Min(q=>q.Outer)>=.10 && trace.Max(q=>q.Outer)<=.30 &&
            world.ContactManifolds==0 && run.Contacts.Length==0;
        Save(Path.Combine(output,"blisko-box2d.json.gz"),new{Run=run,Handling,Timing,Lean=new[]{rig.LeanSamples},
            Start=start,Goal=goal,TargetClearance=gap,HistoricalSweep=.50,DiagnosticSweep=.02,Pass=pass,FinalS=finalS,
            Swept=swept,OuterMin=trace.Min(q=>q.Outer),OuterMax=trace.Max(q=>q.Outer),world.ContactManifolds},true);
        Console.WriteLine($"Short native near-wall: pass={pass} S={finalS:F3} swept={swept:F4} outer={trace.Min(q=>q.Outer):F4}..{trace.Max(q=>q.Outer):F4}.");
    }
}
