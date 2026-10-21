using System.Diagnostics;
using System.Text.Json;

namespace SharedWorldSpeedway;

public static class AdaptiveRiderAudit
{
    public const string BaseHead="7f7ff47c926e2ecbd8a34f82abf27dbc0de14f77";
    public static WorldParameters Parameters()=>PhysicalSteeringAudit.Parameters() with{EnableAdaptiveRiderControl=true,EnableLocalOffsetLimit=false};
    public static bool Safe(TwoContactBikeAudit.Capture c,double reference)
    {
        var s=JsonSerializer.SerializeToElement(TwoContactBikeAudit.Summary(c),InverseFeasibilityAudit.Json);
        var e=s.GetProperty("Energy");
        return c.StopReason is null&&c.LegacyYawCalls==0&&c.Result.Riders[0].TotalReferenceMeters>=reference-1e-6
            &&s.GetProperty("ConservativeInner").GetDouble()>.05&&s.GetProperty("ConservativeOuter").GetDouble()>=.5
            &&e.GetProperty("MaximumExcessBeyondEngineJoules").GetDouble()==0
            &&e.GetProperty("MaximumLinearImpulseResidualNs").GetDouble()<.001&&e.GetProperty("MaximumAngularImpulseResidualNms").GetDouble()<.00003;
    }
    public static void Run(string dir,string stage)
    {
        Directory.CreateDirectory(dir);var frame=AuthoredRacingPath.Crossover(false).Frame;
        if(stage is "first-bend" or "adaptive-first-bend"){
            var reference=frame.SegmentStartMeters(4)+1.05;
            foreach(var adaptive in stage=="first-bend"?new[]{false,true}:new[]{true}){
                var name=adaptive?"adaptive-first-bend":"inner-first-bend";var decisions=new List<AdaptiveRiderDecision>();var started=Stopwatch.GetTimestamp();
                var c=TwoContactBikeAudit.Execute(new(name,Seconds:12,Drive:true),track:true,stopUnsafe:true,
                    parameters:adaptive?Parameters():PhysicalSteeringAudit.Parameters(),stopReference:reference,adaptiveObserver:decisions.Add);
                var elapsed=Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                Export(dir,name,c,decisions,elapsed,reference);
                if(!adaptive){
                    using var old=JsonDocument.Parse(File.ReadAllText("docs/experiments/physical-steering-control/first-bend.json"));
                    if(c.Result.Fingerprint!=old.RootElement.GetProperty("Result").GetProperty("Fingerprint").GetString())throw new InvalidOperationException("#113 OFF fingerprint changed.");
                }
            }
            return;
        }
        if(stage=="eight-laps"){
            using var gate=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"adaptive-first-bend-gate.json")));
            if(!gate.RootElement.GetProperty("Passed").GetBoolean())throw new InvalidOperationException("First bend failed; full laps prohibited.");
            using var settings=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"adaptive-first-bend-metrics.json")));
            if(settings.RootElement.GetProperty("Parameters").GetRawText()!=JsonSerializer.Serialize(Parameters(),InverseFeasibilityAudit.Json))
                throw new InvalidOperationException("First-bend parameters differ; do not reuse its gate for another variant.");
            var decisions=new List<AdaptiveRiderDecision>();var started=Stopwatch.GetTimestamp();
            var c=TwoContactBikeAudit.Execute(new("adaptive-eight-laps",Seconds:200,Drive:true),track:true,laps:8,stopUnsafe:true,
                parameters:Parameters(),adaptiveObserver:decisions.Add);
            Export(dir,"adaptive-eight-laps",c,decisions,Stopwatch.GetElapsedTime(started).TotalMilliseconds,32+8*frame.LapMeters);return;
        }
        throw new ArgumentException("Use first-bend, then eight-laps only after the executed first-bend gate.");
    }
    private static void Export(string dir,string name,TwoContactBikeAudit.Capture c,List<AdaptiveRiderDecision> decisions,double elapsed,double reference)
    {
        TwoContactBikeAudit.Export(dir,name,c);
        InverseFeasibilityAudit.Save(dir,name+"-decisions.json",decisions);
        InverseFeasibilityAudit.Save(dir,name+"-gate.json",new{Passed=Safe(c,reference),Reference=reference,c.StopReason,
            Rule="Executed whole footprint plus existing between-step travel/rotation bound; original energy and native impulse gates. Prediction alone cannot pass the gate."});
        InverseFeasibilityAudit.Save(dir,name+"-metrics.json",new{BaseHead,Parameters=decisions.Count>0?Parameters():PhysicalSteeringAudit.Parameters(),ElapsedMilliseconds=elapsed,
            OffsetMinimum=c.Ticks.Min(t=>t.After.Road.OffsetMeters),OffsetMaximum=c.Ticks.Max(t=>t.After.Road.OffsetMeters),
            MinimumSpeed=c.Ticks.Min(t=>t.After.Speed),MaximumSpeed=c.Ticks.Max(t=>t.After.Speed),
            DriveChanges=c.Ticks.Zip(c.Ticks.Skip(1)).Count(t=>t.First.Before.Drive!=t.Second.Before.Drive),
            PartialDriveSeconds=c.Ticks.Where(t=>t.Before.Drive<1).Sum(t=>(double)t.Seconds),
            SteeringQueries=c.Ticks.Sum(t=>(long)(t.Forces.SteeringControl?.ContactQueries??0)),
            Reviews=decisions.Count,Switches=decisions.Count(d=>d.Changed),EmergencyReviews=decisions.Count(d=>d.Emergency),
            InfeasibleCandidates=decisions.Sum(d=>d.Candidates.Count(a=>a.MaximumDemandRatio>1)),
            MinimumReviewSpacing=decisions.Count>1?decisions.Zip(decisions.Skip(1)).Min(d=>d.Second.TimeSeconds-d.First.TimeSeconds):0,
            Rule="Wall time includes diagnostics and predictor; work counters separate native execution, scalar wheel commits and disposable prediction. No traffic or dynamic axle loads."});
    }
}
