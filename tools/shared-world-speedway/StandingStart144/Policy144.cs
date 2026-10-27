using System.Diagnostics;
using CoreSim;
namespace SharedWorldSpeedway;
public sealed record Decision144(FreeLearningDecision Choice,double CpuSeconds);
public static class Policy144
{
    public static Variant143 Variant(string id)=>id switch{
        "A"=>new("A",Array.Empty<string>()),"B"=>new("B",new[]{"B"}),"F"=>new("F",new[]{"F"}),"K1"=>new("K1",new[]{"B","F"}),_=>throw new ArgumentException("Frozen A/B/F/K1 only.")};
    public static Decision144 Choose(FreeRacingInput input,CrankClutch144 shaft,RiderSpec spec,TrackFrame frame,TrackStateSnapshot surface,WorldParameters p,float mass,float inertia,string variant){
        var started=Process.GetCurrentProcess().TotalProcessorTime;var policy=Controller143.Policy;var v=Variant(variant);
        var rows=new List<FreeLearningCandidate>();int contacts=0;int forecasts=0;
        foreach(var action in FreeLearningController.Actions(input,policy,true)){
            var forecast=Predictor144.Predict(input,spec,frame,surface,p,mass,inertia,.9,action,powertrain:shaft);contacts+=forecast.ForceEvaluations;forecasts++;
            var old=Objective142.FromPrediction(input,policy,frame,surface,forecast,action);var score=Controller143.Score(old,v);
            rows.Add(old.Candidate with{Score=score.Score,Safety=score.Safety});
        }
        var chosen=rows.OrderBy(r=>r.Safety).ThenBy(r=>r.Score).ThenBy(r=>Math.Abs(r.Action.SteeringRadians-input.Active.SteeringRadians)).ThenBy(r=>r.Action.SteeringRadians).First();
        return new(new(input.Time,chosen.Action,chosen.Safety,rows.All(r=>r.Safety!=FreeActionSafety.SafeInHorizon),rows.ToArray(),contacts,forecasts),
            (Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds);
    }
}
