using System.Diagnostics;
using CoreSim.PhysicalSpace;
using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using SharedWorldSpeedway;
namespace Autonomous146;
public static class SequenceAudit146{
    public static void Run(string directory){
        Directory.CreateDirectory(directory);var path=Path.Combine(directory,"executed-sequence-audit.json");if(File.Exists(path))throw new IOException("Preserve completed audit.");
        var cpu=Process.GetCurrentProcess().TotalProcessorTime;var json=FreeLearningExperiment.Json;
        using var z=new GZipStream(File.OpenRead("docs/experiments/autonomous-corner-planning/native/autonomous146-viable.json.gz"),CompressionMode.Decompress);
        var run=JsonSerializer.Deserialize<Run144>(z,json)!;var r=run.Riders[0];var frame=StandingWorld144.Frame();var rows=new List<object>();
        foreach(double at in new[]{28.4,29,31}){
            var s=r.Samples.MinBy(s=>Math.Abs(s.Input.Time-at))!;var input=s.Input with{Time=at,NextReview=at,Pending=s.Input.Pending.Where(c=>c.ObservedAt<at-1e-8).ToArray()};
            var state=new State146(input,s.CrankOmega);var model=new Model146(r.Spec,frame,MultiActionExperiment135.Surface(frame),FreeLearningExperiment.Parameters());
            var points=new List<FreeRacingPoint>();double crankError=0;
            while(state.Input.Time<Math.Min(run.EndSeconds,at+8)-1e-8){
                var d=r.Decisions.First(d=>Math.Abs(d.Choice.Time-state.Input.Time)<1e-7);
                var transition=model.Step(state,d.Choice.Action);points.AddRange(transition.Prediction.Points.Skip(points.Count==0?0:1));state=transition.End;
                var accepted=r.Samples.MinBy(s=>Math.Abs(s.Input.Time-state.Input.Time))!;crankError=Math.Max(crankError,Math.Abs(accepted.CrankOmega-state.CrankOmega));
            }
            double xy=0,speed=0,yaw=0,beta=0,wheel=0;int count=0;
            foreach(var p in points){var a=r.Samples.MinBy(s=>Math.Abs(s.Input.Time-p.Time))!;if(Math.Abs(a.Input.Time-p.Time)>1e-7)continue;
                xy=Math.Max(xy,Vector2.Distance(p.Rider.Position,a.Input.Rider.Position));speed=Math.Max(speed,Math.Abs(p.Rider.Speed-a.Input.Rider.Speed));yaw=Math.Max(yaw,Math.Abs(p.Rider.AngularVelocity-a.Input.Rider.AngularVelocity));
                beta=Math.Max(beta,Math.Abs(BikeAngles.Wrap(p.Rider.SlideRadians-a.Input.Rider.SlideRadians)));wheel=Math.Max(wheel,Math.Abs(p.Omega-a.Input.Omega));count++;
            }
            double? TimeAt(IEnumerable<FreeRacingPoint> pp,double reference){FreeRacingPoint? previous=null;foreach(var p in pp){if(previous is not null&&previous.Rider.TotalReferenceMeters<reference&&p.Rider.TotalReferenceMeters>=reference)return previous.Time+(p.Time-previous.Time)*(reference-previous.Rider.TotalReferenceMeters)/(p.Rider.TotalReferenceMeters-previous.Rider.TotalReferenceMeters);previous=p;}return null;}
            var exit=run.LapMeters+frame.SegmentStartMeters(8);var predictedExit=TimeAt(points,exit);
            var nativePoints=r.Samples.Select(s=>new FreeRacingPoint(s.Input.Time,s.Input.Rider,s.Input.Omega,s.Input.Steering,s.Inner,s.Outer));var nativeExit=TimeAt(nativePoints,exit);
            rows.Add(new{At=at,End=state.Input.Time,Points=count,MaximumXYMeters=xy,MaximumSpeedMps=speed,MaximumYawRate=yaw,MaximumBetaRadians=beta,MaximumWheelOmega=wheel,MaximumCrankOmega=crankError,
                PredictedCornerExitTime=predictedExit,NativeCornerExitTime=nativeExit,ExitTimeErrorSeconds=predictedExit-nativeExit,
                Passed=xy<.03&&speed<.03&&yaw<.01&&predictedExit is not null&&Math.Abs((predictedExit-nativeExit)??1)<.01,model.Forecasts,model.Contacts});
        }
        File.WriteAllText(path,JsonSerializer.Serialize(new{NativeWorlds=0,CpuSeconds=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,
            Scope="Retrospective forecast of the ACTUALLY EXECUTED native command sequence. Future actions are supplied only to this read-only audit. This verifies model integration, not original planner intent, ranking or the missing 120 reviews.",Rows=rows},json));
        Console.WriteLine(File.ReadAllText(path));
    }
}
