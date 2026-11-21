using System.Diagnostics;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const string NativeSource205="data/experiments/speedway204/native/H27-N-learned-C.json.gz";

    private static Start197 State205(Archive204 a,int i)
    {
        var run=a.Run.Riders.Single();
        return Sample199(run.Samples[i],a.Lean[0][i]) with
        { RiderHeightOffsetMeters=i>0&&a.Posture is {Length:>0}?a.Posture[i-1].Posture.RiderHeightOffsetAfterMeters:0 };
    }

    private static void Preflight205(string repository,string output,string? learned)
    {
        var old=Read199<Archive204>(Path.Combine(repository,NativeSource205));
        var driver=old.Run.Riders.Single();
        var frozen=Read199<Training199>(Path.Combine(repository,Training204)).Winner.Result;
        var learnedResult=learned is null?null:Read199<Training199>(learned).Winner.Result;
        var traces=new List<object>();
        void Run(string variant,string label,string fixture,RiderSpec spec,Start197 start,FreeRacingAction[] commands,bool adaptive)
        {
            var corridor=new Corridor199(Map199,double.PositiveInfinity);
            var predictor=new Predictor198(Frame,Surface,Physics,Settings202(variant),Timing,spec,Mass,Inertia);
            var cursor=predictor.Begin(start,commands,0,3*Frame.LapMeters,.5,profile:true,
                constraint:adaptive?corridor.Check:null);
            var timer=Stopwatch.StartNew();
            var previous=cursor.Snapshot;
            var outSamples=new List<object>{new {Checkpoint=start,End=previous}};
            int next=1;
            while (cursor.Outcome is null&&next<1800)
            {
                cursor.Advance(25,.05*next++);
                var checkpoint=cursor.Checkpoint();
                outSamples.Add(new {Checkpoint=checkpoint,End=cursor.Snapshot});
                previous=cursor.Snapshot;
            }
            var result=new {Variant=variant,Label=label,Fixture=fixture,cursor.Outcome,cursor.Sweep,
                End=previous,FinishSeconds=cursor.Reached?cursor.GoalTime:(double?)null,cursor.Counters,
                cursor.ContactFailure,WallMs=timer.Elapsed.TotalMilliseconds,Trace=outSamples};
            traces.Add(result);
            Console.WriteLine($"{variant} {label}/{fixture} {cursor.Outcome} t={previous.Time:F3} S={previous.S:F3}");
        }
        foreach (string variant in new[] {"H27-N","H28-C"})
        {
            Run(variant,"archived","C",driver.Spec,State205(old,0) with {Pending=[],Review=0},
                driver.Decisions.Select(d=>d.Choice.Action).ToArray(),false);
            foreach (string fixture in Fixtures204)
            {
                var (seed,_,spec)=Fixture200(repository,fixture);
                var start=seed.Start with {Pending=[],Review=0};
                Run(variant,"frozen",fixture,spec,start,Pad200(frozen.Controls!),true);
                if(variant=="H28-C"&&learnedResult?.Controls is { } controls)
                    Run(variant,"relearned",fixture,spec,start,Pad200(controls),true);
            }
        }
        Save(Path.Combine(output,"preflight-model.json.gz"),traces,true);
    }

    private static void Native205(string repository,string output,string fixture,string? learned)
    {
        if(fixture=="archived")
        {
            string tape=Path.Combine(repository,NativeSource205);
            var a=Read199<Archive204>(tape);
            var driver=a.Run.Riders.Single();
            var predictor=new Predictor198(Frame,Surface,Physics,Settings202("H28-C"),Timing,driver.Spec,Mass,Inertia);
            var start=State205(a,0) with {Pending=[],Review=0};
            var cursor=predictor.Begin(start,driver.Decisions.Select(d=>d.Choice.Action).ToArray(),0,Frame.LapMeters*3,.5);
            cursor.Advance(10000);
            if(!cursor.Reached)
            {
                Save(Path.Combine(output,"H28-C-archived.preflight.json"),
                    new {cursor.Outcome,cursor.Sweep,End=cursor.Snapshot,cursor.ContactFailure,NativeWorlds=0});
                return;
            }
            SameNative202(repository,output,"H28-C",tape,"H28-C-archived");
            return;
        }
        if(!Fixtures204.Contains(fixture)) throw new ArgumentException("Require a predeclared native fixture.");
        NativeLearned202(repository,output,"H28-C",learned??Path.Combine(repository,Training204),fixture);
    }
}
