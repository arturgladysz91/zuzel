using System.Security.Cryptography;
using System.Text.Json;

namespace SharedWorldSpeedway;

public sealed record RobustMetrics(FreeLearningMetrics Native)
{
    public double MinimumInstantInner {get;init;}
    public double MinimumInstantOuter {get;init;}
    public double MinimumLegacyCertificate {get;init;}
    public double MinimumRefinedCertificate {get;init;}
    public double? CornerMinimumSpeed {get;init;}
    public double MaximumYawRate {get;init;}
    public double? ExitYawRate {get;init;}
    public double SteeringRequestTravelDegrees {get;init;}
    public double MaximumRequestChangeDegrees {get;init;}
    public double EngineWorkJoules {get;init;}
    public double DissipationJoules {get;init;}
    public int NoSafeDecisions {get;init;}
    public string BoundaryClass {get;init;}="none";
}
public sealed record RobustTelemetry(double Time,double Seconds,TwoContactForces Forces,RearWheelStep Wheel,
    double BodyEnergyDelta,double EnergyResidual,double LegacyCertificate,double AcceptedCertificate);
public sealed record RobustEpisode(FreeLearningEpisode Replay,RobustMetrics Metrics,RobustTelemetry[] Telemetry){
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? GeometryName {get;init;}
}
public sealed record RobustEvaluation(string Variant,int Generation,int Id,FreeLearningPolicy Policy,string[] Scenarios,RobustMetrics[] Results);
public sealed record RobustGeneration(string Variant,int Generation,int Episodes,RobustEvaluation[] Candidates,
    int BestId,FreeLearningPolicy BestPolicy,double[] Mean,double[] Std,double CpuSeconds,long Evaluations,long Forecasts);
public sealed record RobustReservation(int Number,string Name,string Category,int NativeWorlds,bool Complete=false,
    double CpuSeconds=0,long Evaluations=0,long Forecasts=0,string? RawSha256=null,long RawBytes=0,RobustMetrics? Metrics=null);

/// <summary>Reserve before every native world. Cache reads never create a second world.</summary>
public sealed class RobustBudget : IDisposable
{
    public const double CpuLimitSeconds=2600,EpisodeCpuSeconds=30,GenerationCpuSeconds=150;
    public const long ContactLimit=38000000;
    private readonly string directory;private readonly FileStream lease;
    private readonly IReadOnlyDictionary<string,int>? categoryLimits;
    private readonly int worldLimit;
    private readonly double cpuLimit;
    private readonly long contactLimit;
    public List<RobustReservation> Entries {get;}
    public RobustBudget(string directory,IReadOnlyDictionary<string,int>? categoryLimits=null,int worldLimit=531,double cpuLimit=CpuLimitSeconds,long contactLimit=ContactLimit){
        this.categoryLimits=categoryLimits;this.worldLimit=worldLimit;this.cpuLimit=cpuLimit;this.contactLimit=contactLimit;
        this.directory=directory;Directory.CreateDirectory(directory);
        lease=new(Path.Combine(directory,"budget.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        Entries=File.Exists(Path.Combine(directory,"attempts.json"))?RobustLearningExperiment.Read<List<RobustReservation>>(directory,"attempts.json"):new();
    }
    public int Reserve(string name,string category,bool native=true){
        var max=categoryLimits is not null?categoryLimits[category]:category switch{"train-A"=>100,"train-B"=>300,"selection"=>72,"test"=>48,"diagnostic"=>8,"two-bends"=>3,"offline"=>8,_=>throw new ArgumentException("Unknown budget category.")};
        if(Entries.Any(e=>e.Name==name)||Entries.Count(e=>e.Category==category)>=max
            ||Entries.Sum(e=>e.NativeWorlds)+(native?1:0)>worldLimit||Entries.Sum(e=>e.CpuSeconds)>=cpuLimit-60
            ||Entries.Sum(e=>e.Evaluations)>=contactLimit-1000000)throw new InvalidOperationException("Frozen native/CPU/contact budget or duplicate reservation.");
        var n=Entries.Count+1;Entries.Add(new(n,name,category,native?1:0));Save();return n;
    }
    public void Complete(int n,double cpu,long evaluations,long forecasts,RobustMetrics? metrics=null,string? rawPath=null){
        if(!double.IsFinite(cpu)||cpu<0||evaluations<0||forecasts<0)throw new ArgumentException("Finite work accounting required.");
        var hash=rawPath is null?null:Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(rawPath)));
        Entries[n-1]=Entries[n-1] with{Complete=true,CpuSeconds=cpu,Evaluations=evaluations,Forecasts=forecasts,Metrics=metrics,RawSha256=hash,RawBytes=rawPath is null?0:new FileInfo(rawPath).Length};Save();
    }
    private void Save()=>RobustLearningExperiment.Save(directory,"attempts.json",Entries);
    public void Dispose()=>lease.Dispose();
}

public static class RobustRanking
{
    public static int Completed(RobustEvaluation e)=>e.Results.Count(r=>r.Native.Completed);
    public static double WorstFailure(RobustEvaluation e)=>e.Results.Where(r=>!r.Native.Completed).Select(r=>r.Native.LegalProgress).DefaultIfEmpty(0).Min();
    public static int ConfirmedOrControl(RobustEvaluation e)=>e.Results.Count(r=>r.BoundaryClass=="confirmed crossing"||r.Native.Outcome=="control envelope");
    public static double WorstTime(RobustEvaluation e)=>e.Results.All(r=>r.Native.Completed)?e.Results.Max(r=>r.Native.GateSeconds!.Value):double.PositiveInfinity;
    // Times within the declared bins are comparable: reserve then breaks ties, without an offset target.
    public static IOrderedEnumerable<RobustEvaluation> Rank(IEnumerable<RobustEvaluation> candidates)=>candidates
        .OrderByDescending(Completed).ThenByDescending(WorstFailure).ThenBy(ConfirmedOrControl)
        .ThenBy(e=>Math.Ceiling(WorstTime(e)/.05))
        .ThenBy(e=>e.Results.All(r=>r.Native.Completed)?Math.Ceiling(e.Results.Average(r=>r.Native.GateSeconds!.Value)/.025):double.PositiveInfinity)
        .ThenByDescending(e=>e.Results.Min(r=>r.Native.MinimumClearanceMeters))
        .ThenByDescending(e=>e.Results.Where(r=>r.Native.Completed).Select(r=>r.Native.ExitSpeed!.Value).DefaultIfEmpty(0).Min())
        .ThenBy(e=>e.Results.Where(r=>r.Native.Completed).Select(r=>Math.Abs(r.ExitYawRate??0)).DefaultIfEmpty(double.PositiveInfinity).Max())
        .ThenByDescending(e=>e.Results.Average(r=>r.Native.LegalProgress)).ThenBy(e=>e.Id);
}
