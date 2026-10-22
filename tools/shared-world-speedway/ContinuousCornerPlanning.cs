using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

public sealed record CornerPlanChange(string Phase,int Generation,double Time,double Activation,
    double PreviousExit,double BendStart,double BendExit,double Splice,double EndTransition,double Offset,
    RiderView Rider,RearWheelView Wheel,float Steering,DecisionEvent? Pending,PreparedControlMemory Memory,
    TravelDirectionState Motion,double ExitPlaneClearance,int DiagnosticContactQueries);

/// <summary>Only manages path lifecycle. The #122 throttle policy, clocks, pending commands and all mechanics survive.</summary>
public sealed class ContinuousCornerPlanning(PreparedCornerPlan initial)
{
    public PreparedCornerPlan Active {get;private set;}=initial;
    public PreparedCornerPlan? Waiting {get;private set;}
    public int Generation {get;private set;}=1;
    public static double ExitPlaneClearance(RiderView v,TrackFrame frame,double exitReference)
    {
        var q=frame.Shape!.At(exitReference);var tangent=new MeterPoint(Math.Cos(q.Heading),Math.Sin(q.Heading));
        var bike=BikeFootprint.Create(new(v.Position.X,v.Position.Y),v.Heading,SpeedwayBikeDimensions.Reference);
        return new[]{bike.Chassis,bike.Handlebar}.Min(part=>Math.Min(MeterPoint.Dot(part.Start-q.Position,tangent),MeterPoint.Dot(part.End-q.Position,tangent))-part.RadiusMeters);
    }
    public static bool Cleared(RiderView v,TrackFrame frame,double exitReference)=>v.TotalReferenceMeters>=exitReference&&ExitPlaneClearance(v,frame,exitReference)>0;
    public CornerPlanChange? Advance(RiderView v,RiderSpec spec,TrackFrame frame,TrackStateSnapshot surface,WorldParameters p,
        double time,float mass,RearWheelState wheel,TwoContactBikeState steering,DecisionEvent? pending)
    {
        var previous=Active;
        string phase;
        if(Waiting is { } ready){
            if(time+1e-10<ready.ActivationSeconds)return null;
            ready.CarryControlMemory(Active.ControlMemory);Active=ready;Waiting=null;Generation++;phase="activated";
        }else{
            if(!Cleared(v,frame,Active.BendExitMeters))return null;
            Waiting=new(v,spec,frame,p,time,mass,wheel,steering,Active.Options,surface,new(Active.Path,Active.ControlMemory,pending));
            phase="prepared";
        }
        var plan=Waiting??Active;
        var f=TwoContactBikeDynamics.Forces(v,spec,frame,surface,p,time,mass,
            new(wheel.AngularVelocityRadPerSecond*wheel.Parameters.WheelRadiusMeters,wheel.Parameters),new(steering.SteeringRadians),0,steeringOverride:steering.SteeringRadians);
        return new(phase,phase=="prepared"?Generation+1:Generation,time,plan.ActivationSeconds,previous.BendExitMeters,
            plan.BendStartMeters,plan.BendExitMeters,plan.TransitionStartMeters,plan.TransitionEndMeters,plan.SelectedOffsetMeters,
            v,wheel.View(v.Id),steering.SteeringRadians,pending,Active.ControlMemory,TravelDirectionState.Observe(v,f.NetForce,mass),
            ExitPlaneClearance(v,frame,previous.BendExitMeters),1);
    }
}
