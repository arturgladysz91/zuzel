using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
public sealed class ZigzagControlAuditTests
{
    private static (PredictiveRiderInput Input,PreparedCornerPlan Plan,TrackStateSnapshot Surface,WorldParameters Parameters) Fixture()
    {
        var rows=DynamicAxleLoadAudit.ReadSaved("../predictive-rider-control/controlled/candidate");var paths=DynamicAxleLoadAudit.ReadSaved("../predictive-rider-control/controlled/candidate-plan");
        var plan=PreparedCornerAudit.InitialPlan();var frame=plan.Path.Frame;var p=ContinuousCornerAudit.Parameters();
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var index=Array.FindIndex(rows,r=>Math.Abs(r["t"]-5.2)<1e-8);var row=rows[index];
        using var file=File.OpenRead(PhysicalSteeringAudit.PreviousPath("../predictive-rider-control/controlled/candidate-reviews.json.gz"));
        using var zip=new System.IO.Compression.GZipStream(file,System.IO.Compression.CompressionMode.Decompress);
        var reviews=JsonSerializer.Deserialize<PreparedCornerReview[]>(zip)!;var review=reviews.Last(r=>r.Time<=row["t"]+1e-8);
        using var run=JsonDocument.Parse(File.ReadAllText(PhysicalSteeringAudit.PreviousPath("../predictive-rider-control/controlled/candidate.json")));
        var commands=run.RootElement.GetProperty("Result").GetProperty("Decisions").Deserialize<DecisionEvent[]>(InverseFeasibilityAudit.Json)!;
        var pending=commands.FirstOrDefault(d=>d.ObservedAt<=row["t"]+1e-8&&d.RequestedActivation>row["t"]+1e-8&&d.CancelledAt is null);
        return(new(PhysicalSteeringAudit.View(row,frame) with{TargetOffsetMeters=(float)paths[index]["target"]},row["t"],row["omega_before"],(float)row["steer_before"],
            new((float)review.Drive,review.Time+.1),pending is null?null:pending with{ActualActivation=null}),plan,surface,p);
    }
    [Fact]
    public void OfflineBiasPreservesAcceptedStatePlanMemoryAndCompleteDelayedPrefix()
    {
        var f=Fixture();var original=f.Input;var memory=f.Plan.ControlMemory;
        PredictiveRiderResult Predict(bool bias,float? correction)=>PredictiveRiderModel.Predict(f.Input,CornerEntryAudit.Rider(),f.Plan.ForkForPrediction(),f.Surface,f.Parameters,147,52.72097f,.6,new(true),correction,diagnosticRequestBias:bias);
        var a=Predict(false,null);var b=Predict(false,-PredictiveRiderControl.CorrectionRadians);var c=Predict(true,-PredictiveRiderControl.CorrectionRadians);
        Assert.Equal(original,f.Input);Assert.Equal(memory,f.Plan.ControlMemory);
        foreach(var point in c.Points.Where(q=>q.Time<=f.Input.Time+.24+1e-8)){
            Assert.Equal(a.Points.Single(q=>Math.Abs(q.Time-point.Time)<1e-8).Rider,point.Rider);
            Assert.Equal(b.Points.Single(q=>Math.Abs(q.Time-point.Time)<1e-8).Rider,point.Rider);
        }
        Assert.Equal(a.Activations.Where(q=>q.PreviouslyPending),c.Activations.Where(q=>q.PreviouslyPending));
        Assert.All(c.Points,p=>Assert.InRange(Math.Abs(p.Steering),0,TwoContactBikeParameters.Default.MaximumSteeringRadians));
        Assert.InRange(c.MaximumSteeringRate,0,Math.PI/2+1e-4);
    }
    [Fact]
    public void BiasFollowsCurrentOriginalRequestAtEveryActiveStepAndReleasesAfterPointOneSeconds()
    {
        var f=Fixture();var forecast=PredictiveRiderModel.Predict(f.Input,CornerEntryAudit.Rider(),f.Plan.ForkForPrediction(),f.Surface,f.Parameters,147,52.72097f,.6,new(true),-PredictiveRiderControl.CorrectionRadians,diagnosticRequestBias:true);
        foreach(var (before,after) in forecast.Points.Zip(forecast.Points.Skip(1))){
            var v=before.Rider;var dt=(float)(after.Time-before.Time);var plan=f.Plan;
            var original=TwoContactBikeDynamics.Forces(v,CornerEntryAudit.Rider(),plan.Path.Frame,f.Surface,f.Parameters,before.Time,147,new(before.Omega*.33),new(before.Steering),dt,plan.Guidance(v,f.Parameters,before.Time),inertia:52.72097f);
            var active=before.Time>=f.Input.Time+.24-1e-8&&before.Time<f.Input.Time+.34-1e-8;
            var expected=active?TwoContactBikeDynamics.Forces(v,CornerEntryAudit.Rider(),plan.Path.Frame,f.Surface,f.Parameters,before.Time,147,new(before.Omega*.33),new(before.Steering),dt,plan.Guidance(v,f.Parameters,before.Time),
                Math.Clamp(original.SteeringRequest-PredictiveRiderControl.CorrectionRadians,-TwoContactBikeParameters.Default.MaximumSteeringRadians,TwoContactBikeParameters.Default.MaximumSteeringRadians),inertia:52.72097f):original;
            Assert.Equal(expected.SteeringAfter,after.Steering);
        }
    }
    [Fact]
    public void DefaultModeRemainsExactlyTheSaved126ForecastAndTheOriginal63NativeGate()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(PhysicalSteeringAudit.PreviousPath("../first-bend-zigzag/execution-and-cost.json")));
        var equal=data.RootElement.GetProperty("DefaultEquality").EnumerateArray().ToArray();Assert.Equal(30,equal.Length);
        foreach(var record in equal)foreach(var key in new[]{"Position","Steering","Drive"})Assert.Equal(0,record.GetProperty(key).GetDouble());
        using var gate=JsonDocument.Parse(File.ReadAllText(PhysicalSteeringAudit.PreviousPath("../first-bend-zigzag/default-model-validation.json")));
        var records=gate.RootElement.GetProperty("Records").EnumerateArray().ToArray();Assert.Equal(63,records.Length);Assert.All(records,r=>Assert.True(r.GetProperty("Passed").GetBoolean()));
    }
    [Fact]
    public void DiagnosticBiasIsDeterministicAndRemainsOptIn()
    {
        var f=Fixture();
        PredictiveRiderResult Predict()=>PredictiveRiderModel.Predict(f.Input,CornerEntryAudit.Rider(),f.Plan.ForkForPrediction(),f.Surface,f.Parameters,147,52.72097f,.6,new(true),-PredictiveRiderControl.CorrectionRadians,diagnosticRequestBias:true);
        Assert.Equal(JsonSerializer.Serialize(Predict(),InverseFeasibilityAudit.Json),JsonSerializer.Serialize(Predict(),InverseFeasibilityAudit.Json));
        Assert.Throws<InvalidOperationException>(()=>PredictiveRiderModel.Predict(f.Input,CornerEntryAudit.Rider(),f.Plan.ForkForPrediction(),f.Surface,f.Parameters,147,52.72097f,.6,delayedSteeringCorrection:-PredictiveRiderControl.CorrectionRadians,diagnosticRequestBias:true));
    }
}
