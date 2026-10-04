using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoreSim.Decisions;
using CoreSim.Logging;
using CoreSim.Race;

namespace CoreSim.Analysis;

/// <summary>Explicit offline benchmark; no timing or allocation gate in ordinary tests.</summary>
public static class TrajectoryProjectionBenchmark
{
    public static string RunJson(int? maxDegreeOfParallelism = null)
    {
        var degree = maxDegreeOfParallelism ?? Math.Min(4, Environment.ProcessorCount);
        if (degree is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));
        var rows = new List<object>();
        foreach (var count in new[] { 1, 4 })
        {
            rows.Add(MeasureHeat(count, false, degree));
            if (count == 4) rows.Add(MeasureHeat(count, true, degree));
        }
        var phases = new List<object>();
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        for (var index = 0; index <= 3; index++)
        {
            var rider = new RiderState(RiderProfile.CreateDefault(17), StartingGate.C, track);
            var state = TrackState.CreateDefault(track); var engine = new SimulationEngine(new Hold());
            for (var s = 0; s < index; s++)
            {
                var input = engine.CaptureSnapshot(track, state, new[] { rider }, new(1,s,0,s,0,4));
                engine.Commit(engine.Resolve(input, engine.Decide(input), Options()), new[] { rider }, state, new SimLog(false));
            }
            var snapshot = engine.CaptureSnapshot(track,state,new[] { rider },new(1,index,0,index,0,4));
            var context = new RiderDecisionContext(snapshot,snapshot.Rider(17)); var model = new AdaptiveDecisionModel { MaxDegreeOfParallelism=degree };
            (double Ms,long Bytes) Run()
            {
                var before = GC.GetTotalAllocatedBytes(precise: true); var watch = Stopwatch.StartNew();
                model.Evaluate(context); watch.Stop();
                return (watch.Elapsed.TotalMilliseconds,GC.GetTotalAllocatedBytes(precise: true)-before);
            }
            for(var i=0;i<5;i++)Run(); var samples=Enumerable.Range(0,15).Select(_=>Run()).ToArray();
            phases.Add(new { Phase=track.Segments[index].Type, MedianMilliseconds=samples.Select(s=>s.Ms).Order().ElementAt(7),
                AllocatedBytesPerDecision=samples.Average(s=>s.Bytes) });
        }
        var counted = new Counting(true,degree); Heat(4,counted);
        var output = new { Runtime=RuntimeInformation.FrameworkDescription, Warmups=5, Samples=15,
            AllocationMeasurement="process-wide GC.GetTotalAllocatedBytes(precise: true), includes all branch workers",
            BranchDegree=degree, ProjectionCounterDegree=1,
            Laps=4, Incidents=false, Logging=false, ExistingContact=true, Rows=rows, Phases=phases,
            ProjectionCounts=Enum.GetValues<ProjectionMaterialization>().ToDictionary(k=>k.ToString(),k=>counted.Objects.GetValueOrDefault(k)),
            CandidateTraversals=counted.Candidates, UniquePhysicalEvaluations=counted.Resolutions,
            PossiblePrefixVisits=counted.Resolutions+counted.Objects.GetValueOrDefault(ProjectionMaterialization.PrefixCacheHit),
            ExecutedUniquePrefixes=counted.Resolutions,
            DuplicateExecutionsAvoided=counted.Objects.GetValueOrDefault(ProjectionMaterialization.PrefixCacheHit) };
        return JsonSerializer.Serialize(output,new JsonSerializerOptions { WriteIndented=true,
            Converters={new JsonStringEnumConverter()} })+"\n";
    }

    private static object MeasureHeat(int count,bool scripted,int degree)
    {
        (double Ms,long Bytes,int Candidates,int Resolutions,int[] Gc) Run()
        {
            var track=MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
            var riders=Enumerable.Range(1,count).Select(id=>new RiderState(RiderProfile.CreateDefault(id),(StartingGate)(id-1),track)).ToList();
            var state=TrackState.CreateDefault(track); var model=new Counting(false,degree);
            var gc=Enumerable.Range(0,3).Select(GC.CollectionCount).ToArray();
            var before=GC.GetTotalAllocatedBytes(precise: true);var watch=Stopwatch.StartNew();
            new HeatSimulator(scripted ? new Hold() : model).SimulateHeat(track,state,riders,Options());watch.Stop();
            var bytes=GC.GetTotalAllocatedBytes(precise: true)-before;
            return (watch.Elapsed.TotalMilliseconds,bytes,model.Candidates,model.Resolutions,
                Enumerable.Range(0,3).Select(i=>GC.CollectionCount(i)-gc[i]).ToArray());
        }
        for(var i=0;i<5;i++)Run();var samples=Enumerable.Range(0,15).Select(_=>Run()).ToArray();
        return new { Riders=count,Scripted=scripted,MedianMilliseconds=samples.Select(s=>s.Ms).Order().ElementAt(7),
            MeanMilliseconds=samples.Average(s=>s.Ms),AllocatedBytesPerRun=samples.Average(s=>s.Bytes),
            CandidateTraversals=samples[0].Candidates,ProductionResolutions=samples[0].Resolutions,
            Gen0=samples.Sum(s=>s.Gc[0]),Gen1=samples.Sum(s=>s.Gc[1]),Gen2=samples.Sum(s=>s.Gc[2]) };
    }
    private static HeatSimulationOptions Options()=>new(){Laps=4,IncidentFrequency=0,EnableLogging=false};
    private static void Heat(int count,IRiderDecisionModel model)
    {
        var track=MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        new HeatSimulator(model).SimulateHeat(track,TrackState.CreateDefault(track),
            Enumerable.Range(1,count).Select(id=>new RiderState(RiderProfile.CreateDefault(id),(StartingGate)(id-1),track)).ToList(),Options());
    }
    private sealed class Hold:IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment,RiderState rider)=>new(rider.Lane);
    }
    private sealed class Counting(bool observe,int degree):IRiderDecisionModel
    {
        private readonly AdaptiveDecisionModel model=new(){MaxDegreeOfParallelism=degree};
        internal int Candidates{get;private set;} internal int Resolutions{get;private set;}
        internal Dictionary<ProjectionMaterialization,long> Objects{get;}=new();
        public RiderDecision Decide(TrackSegment segment,RiderState rider)=>model.Decide(segment,rider);
        public RiderDecision Decide(RiderDecisionContext context)
        {
            var previous=ProjectionCaptureAudit.Observer;
            if(observe)ProjectionCaptureAudit.Observer=(kind,n)=>Objects[kind]=Objects.GetValueOrDefault(kind)+n;
            try { var result=model.EvaluateDecision(context);Candidates+=result.CandidateTraversals;Resolutions+=result.ProductionResolutions;return result.Decision; }
            finally { if(observe)ProjectionCaptureAudit.Observer=previous; }
        }
    }
}
