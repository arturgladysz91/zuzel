using CoreSim;
using SharedWorldSpeedway;
namespace Autonomous146;

public sealed record State146(FreeRacingInput Input,double CrankOmega);
public sealed record Transition146(State146 End,FreeRacingPrediction Prediction);
/// <summary>Composes unchanged #144 prediction intervals, preserving crank and pending commands.</summary>
public sealed class Model146(RiderSpec spec,TrackFrame frame,TrackStateSnapshot surface,WorldParameters parameters,float mass=147,float inertia=52.72097f,Action? budgetGuard=null)
{
    public long Forecasts {get;private set;}
    public long Contacts {get;private set;}
    public Transition146 Step(State146 state,FreeRacingAction action,double seconds=.2){
        budgetGuard?.Invoke();action.Validate();var i=state.Input;double omega=state.CrankOmega;
        var prediction=Predictor144.Predict(i,spec,frame,surface,parameters,mass,inertia,seconds,action,
            powertrain:new CrankClutch144(omega:state.CrankOmega),observeCrank:(t,w)=>omega=w);
        Forecasts++;Contacts+=prediction.ForceEvaluations;var end=prediction.Points[^1];
        var queue=i.Pending.Append(new FreeRacingCommand(i.Time,i.Time+RiderController.ReactionSeconds(spec),action)).OrderBy(c=>c.ActivationAt).ToArray();
        var active=queue.Where(c=>c.ActivationAt<=end.Time+1e-10).Select(c=>c.Action).LastOrDefault(i.Active);
        var input=new FreeRacingInput(end.Rider with{Drive=active.Drive},end.Time,end.Omega,end.Steering,active,queue.Where(c=>c.ActivationAt>end.Time+1e-10).ToArray(),end.Time);
        return new(new(input,omega),prediction);
    }
}
