using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim.PhysicalSpace;
using LearnedCorner166;
using SharedWorldSpeedway;
using WholeCorner171;
using Scenario=LearnedCorner166.Scenario;

namespace Cyclic172;

internal static class Native174
{
    internal static void Develop(Context174 study)
    {
        var selected=Context174.Read<Selected174[]>(Path.Combine(study.Directory,"selected173.json"))
            .Concat(Context174.Read<Selected174[]>(Path.Combine(study.Directory,"x1-selection.json"))).ToArray();
        var states=new[]{"P7","P8","X4"}.Select(id=>selected.Single(s=>s.Id==id)).ToArray();
        Context174.Save(study.At("-native-protocol.json"),new{Question="Does the new admission and causal K4 continuation match its whole-bend prediction?",States=states,
            MaximumNewWorlds=3,Reference="Original frozen #172 native trajectories with identical verified prefixes",Stop="next bend exit +20m",PerformancePeriodMilliseconds=200});
        using var original=ZipFile.OpenRead(Context174.Docs172+"/172-validation-K4.zip");
        foreach(var state in states)
        {
            study.Guard();if(study.PriorWorlds+study.Worlds>=6)throw new InvalidOperationException("Development world cap reached");
            if(File.Exists(Path.Combine(study.Directory,"native-failed.json")))throw new InvalidOperationException("Prior accepted native illegality; stop development");
            using var from=original.GetEntry(state.Trial+".zip")!.Open();using var memory=new MemoryStream();from.CopyTo(memory);memory.Position=0;using var z=new ZipArchive(memory);
            var archived=Context174.Read<Run144>(z,"native.json");var rider=archived.Riders.Single();var old=Context174.Read<CyclicDecision172[]>(z,"decisions.json");
            var visit=new BendMap(study.Frame).Locate(state.Input.Rider.TotalReferenceMeters).Next;double goal=visit.Exit+20;
            study.Worlds++;Context174.Save(study.At("-"+state.Id+".world-reserved.json"),new{state.Id,World=study.PriorWorlds+study.Worlds,Goal=goal,Cpu=study.Prior+Study172.Cpu-study.Began});
            Controller174? controller=null;var decisions=new List<CyclicDecision172>();string? branchHash=null;int prefix=0;
            Decision144 Choose(FreeRacingInput input,CrankClutch144 crank,RiderSpec spec,TrackFrame frame,
                CoreSim.TrackStateSnapshot surface,WorldParameters parameters,float mass,float inertia,string variant)
            {
                study.Guard();controller??=new(study,spec);var choice=controller.Choose(input,crank,input.Time>=state.Input.Time-1e-8);
                if(input.Time<=state.Input.Time+1e-8)
                {
                    var sample=rider.Samples.MinBy(s=>Math.Abs(s.Input.Time-input.Time))!;
                    if(JsonSerializer.Serialize(new{Input=input,CrankOmega=crank.Omega},Context174.Json)!=JsonSerializer.Serialize(new{Input=Context174.Observation(sample,input.Time),sample.CrankOmega},Context174.Json))
                        throw new InvalidOperationException("Native initial state/pending prefix mismatch");
                    prefix++;
                    if(input.Time<state.Input.Time-1e-8&&choice.Action!=old.Single(d=>Math.Abs(d.Time-input.Time)<1e-8).Action)
                        throw new InvalidOperationException("Native command prefix mismatch");
                    if(Math.Abs(input.Time-state.Input.Time)<1e-8)
                        branchHash=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{input,CrankOmega=crank.Omega,spec},Context174.Json)));
                }
                decisions.Add(choice);return new(new(input.Time,choice.Action,FreeActionSafety.Uncertain,false,[],choice.Queries,choice.Forecasts),choice.CpuSeconds);
            }
            double began=Study172.Cpu;
            using var world=new NativeWorld172(new(Enabled:true,RequiredLaps:3,MaximumSeconds:40,SoloGate:rider.Gate),
                diagnosticFrame:study.Frame,diagnosticRider:rider.Spec,safetyHalfMeter:true,stopReference:goal);
            var run=world.Run(study.Label+"-"+state.Id,"K1",Choose);
            var plans=controller?.Base.Plans.ToArray()??[];
            var scenario=new Scenario(state.Id,state.Spec.OffsetMeters<2?"A":state.Spec.OffsetMeters<5?"B":state.Spec.OffsetMeters<8?"C":"D",state.Spec.ReferenceMeters,state.Spec.OffsetMeters);
            var outcome=Inspect172.Analyze(run,"development","NEW174",scenario,study.Frame,goal,decisions.ToArray(),plans,Study172.Cpu-began);
            var admissions=controller?.Admissions.ToArray()??[];var branch=admissions.FirstOrDefault(a=>Math.Abs(a.Time-state.Input.Time)<1e-8);
            var native=run.Riders.Single().Samples;
            var before=native.Where(s=>s.Input.Time<state.Input.Time+.24-1e-8).ToArray();
            var oldBefore=rider.Samples.Where(s=>s.Input.Time<state.Input.Time+.24-1e-8).ToArray();
            bool physical=before.Length==oldBefore.Length&&before.Zip(oldBefore).All(pair=>Physical(pair.First)==Physical(pair.Second));
            var report=new{state.Id,State=state,BranchHash=branchHash,PrefixReviews=prefix,PhysicalPrefixSamples=before.Length,PhysicalPrefixEqual=physical,
                Admitted=branch?.Admitted??false,StateHashMatches=branchHash==branch?.StateSha256,Outcome=outcome,
                BaselineGoalSeconds=Crossing(rider.Samples,goal),NewGoalSeconds=Crossing(native,goal),Admissions=admissions.Length,
                MaximumDecisionMs=decisions.Max(d=>d.DecisionMilliseconds),BranchDecisionMs=branch?.Milliseconds};
            using(var archive=ZipFile.Open(study.At("-"+state.Id+".zip"),ZipArchiveMode.Create))
            {
                Add(archive,"native.json",run);Add(archive,"result.json",outcome);Add(archive,"decisions.json",decisions);
                Add(archive,"plans.json",plans);Add(archive,"admissions.json",admissions);Add(archive,"branch.json",report);
            }
            Context174.Save(study.At("-"+state.Id+"-result.json"),report);
            Console.WriteLine($"{state.Id}: admitted={report.Admitted} legal={outcome.ReachedStop} old={report.BaselineGoalSeconds:F6} new={report.NewGoalSeconds:F6} swept={outcome.MinSweep:F3} beta={outcome.MaxBetaDegrees:F2} decision={report.BranchDecisionMs:F1}ms {outcome.Reason}");
            if(!physical||!report.StateHashMatches||branchHash is null)throw new InvalidOperationException("Native controlled-prefix audit failed; evidence retained");
            if(report.Admitted&&!outcome.ReachedStop)
            {Context174.Save(Path.Combine(study.Directory,"native-failed.json"),report);break;}
            if(!report.Admitted)break;
        }
    }
    private static string Physical(StartSample144 s)=>JsonSerializer.Serialize(new{s.Input.Time,s.Input.Rider,s.Input.Omega,s.Input.Steering,s.Input.Active,s.CrankOmega,s.Forces,s.Powertrain},Context174.Json);
    private static double? Crossing(StartSample144[] samples,double goal)
    {
        int i=Array.FindIndex(samples,s=>s.Input.Rider.TotalReferenceMeters>=goal);if(i<1)return null;
        var a=samples[i-1];var b=samples[i];double u=(goal-a.Input.Rider.TotalReferenceMeters)/(b.Input.Rider.TotalReferenceMeters-a.Input.Rider.TotalReferenceMeters);
        return a.Input.Time+u*(b.Input.Time-a.Input.Time);
    }
    private static void Add(ZipArchive z,string name,object value){using var s=z.CreateEntry(name,CompressionLevel.Optimal).Open();JsonSerializer.Serialize(s,value,Context174.Json);}
}
