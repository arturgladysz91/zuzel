using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

// Research-only: not called by WorldRunner. Explicit opt-in is required even for queries.
public sealed record PredictiveRiderOptions(bool Enabled=false,double StepSeconds=.01);
public sealed record PredictiveRiderInput(RiderView Rider,double Time,double Omega,float Steering,
    PreparedControlMemory Memory,DecisionEvent? Pending);
public sealed record PredictedControlActivation(double Time,float Drive,bool PreviouslyPending);
public sealed record PredictedRiderPoint(double Time,RiderView Rider,double Omega,float Steering,double Inner,double Outer);
public sealed record PredictiveRiderResult(PredictedRiderPoint[] Points,PredictedControlActivation[] Activations,
    int ContactQueries,int ForceEvaluations,double MinimumInner,double MinimumOuter,double MaximumFrontUtilization,
    double MaximumRearUtilization,double MaximumSteeringRate,double DistanceMeters);

/// <summary>Small approximate rollout of existing contacts, never an alternate native world.
/// Rigid-body positions in this module are predictions only and are never written to WorldRunner.</summary>
public static class PredictiveRiderModel
{
    public static PredictiveRiderResult Predict(PredictiveRiderInput input,RiderSpec spec,PreparedCornerPlan disposablePlan,
        TrackStateSnapshot surface,WorldParameters p,float mass,float inertia,double horizon,
        PredictiveRiderOptions? options=null,float? delayedSteeringCorrection=null,bool reachableOnly=false)
    {
        options??=new();
        if(!options.Enabled)throw new InvalidOperationException("Predictive research model is OFF by default.");
        if(horizon<=0||horizon>.900001||options.StepSeconds<=0||options.StepSeconds>.010001
            ||!double.IsFinite(horizon)||!double.IsFinite(options.StepSeconds)||mass<=0||inertia<=0)
            throw new ArgumentException("Bounded finite prediction inputs required.");
        if(input.Pending is { } c&&(c.CancelledAt is not null||c.RequestedActivation<input.Time-1e-8||c.ObservedAt>input.Time+1e-8))
            throw new ArgumentException("Only an already observed, uncancelled pending command is causal.");
        var frame=disposablePlan.Path.Frame;var boundary=new RacingLineGeometry(frame);var v=input.Rider;
        var wheel=new RearWheelState(input.Omega*(p.RearWheel??RearWheelParameters.Default).WheelRadiusMeters,p.RearWheel);
        var steering=new TwoContactBikeState(input.Steering);var pending=input.Pending;
        disposablePlan.CarryControlMemory(input.Memory);
        var points=new List<PredictedRiderPoint>();var activations=new List<PredictedControlActivation>();
        var time=input.Time;var end=time+horizon;var reactionEnd=time+RiderController.ReactionSeconds(spec);
        float? targetSteering=null;int queries=0,forces=0;double inner=double.PositiveInfinity,outer=inner,frontUse=0,rearUse=0,maxRate=0,distance=0;
        void Point(){var gap=boundary.Clearance(v);inner=Math.Min(inner,gap.Inner);outer=Math.Min(outer,gap.Outer);
            points.Add(new(time,v,wheel.AngularVelocityRadPerSecond,steering.SteeringRadians,gap.Inner,gap.Outer));}
        Point();
        while(time<end-1e-10){
            if(pending is { } command&&time+1e-12>=command.RequestedActivation){
                v=v with{Drive=command.Drive,TargetOffsetMeters=command.TargetOffsetMeters};
                activations.Add(new(time,command.Drive,ReferenceEquals(command,input.Pending)));pending=null;
            }
            v=v with{TargetOffsetMeters=disposablePlan.Target(v,time)};
            if(time+1e-10>=disposablePlan.NextReviewSeconds){
                var review=disposablePlan.Observe(v,spec,frame,surface,p,time,mass,inertia,wheel,steering);queries+=review.ContactQueries;
                var drive=(float)review.Drive;
                if(pending is null&&drive!=v.Drive)
                    pending=new(v.Id,0,TrafficAction.KeepIntent,time,time,review.Activation,null,v.TargetOffsetMeters,drive,Reason:review.Reason);
            }
            points[^1]=points[^1] with{Rider=v}; // align post-activation controls with this accepted pose
            // A new steering intention cannot execute before full rider reaction.
            // The irrevocable throttle slot above is processed first, including at the same deadline.
            if(delayedSteeringCorrection is { } correction&&time+1e-12>=reactionEnd&&targetSteering is null)
                targetSteering=Math.Clamp(steering.SteeringRadians+correction,
                    -(p.TwoContactBike??TwoContactBikeParameters.Default).MaximumSteeringRadians,
                    (p.TwoContactBike??TwoContactBikeParameters.Default).MaximumSteeringRadians);
            var nominal=(Math.Floor((time+1e-8)/options.StepSeconds)+1)*options.StepSeconds;
            var duration=Math.Min(end-time,nominal-time);
            if(pending is { } slot&&slot.RequestedActivation>time+1e-12)duration=Math.Min(duration,slot.RequestedActivation-time);
            if(delayedSteeringCorrection is not null&&reactionEnd>time+1e-12)duration=Math.Min(duration,reactionEnd-time);
            var parameters=reachableOnly?p with{EnableReachableTravelDirectionSteering=true}:p;
            var guidance=disposablePlan.Guidance(v,p,time);
            float? steeringOverride=targetSteering is not null&&time<reactionEnd+PreparedCornerPlan.ReviewSeconds-1e-10?targetSteering:null;
            TwoContactForces Query(float dt){var f=TwoContactBikeDynamics.Forces(v,spec,frame,surface,parameters,time,mass,wheel,steering,dt,guidance,steeringOverride,inertia:inertia);
                queries+=1+(f.TravelSteering?.ContactQueries??0);forces++;return f;}
            // Same held-force curvature-event estimate as WorldRunner, but no native step or state mutation there.
            if(frame.DistanceToCurvatureChange(v.Road)<=(v.Speed+5)*duration+.001){
                var probe=Query(0);var along=Vector2.Dot(v.Velocity,v.Road.Tangent);
                var acceleration=Vector2.Dot(probe.NetForce,v.Road.Tangent)/mass;var b=(p.SolverSubsteps+1d)/(2*p.SolverSubsteps);
                var s=frame.DistanceToCurvatureChange(v.Road);var discriminant=along*along+4*b*acceleration*s;
                if(along>0&&discriminant>=0&&s<=along*duration+b*acceleration*duration*duration){
                    var crossing=2*s/(along+Math.Sqrt(discriminant));
                    duration=Math.Min(duration,Math.Max(1e-6,Math.Ceiling(crossing/1e-6)*1e-6));
                }
            }
            var h=(float)duration;var applied=Query(h);var rate=Math.Abs(applied.SteeringAfter-applied.SteeringBefore)/h;maxRate=Math.Max(maxRate,rate);
            frontUse=Math.Max(frontUse,Math.Abs(applied.FrontLateral)/applied.TyreLoads!.FrontCapacityNewtons);
            rearUse=Math.Max(rearUse,Math.Sqrt((double)applied.RearLateral*applied.RearLateral+(double)applied.RearLongitudinal*applied.RearLongitudinal)/applied.TyreLoads.RearCapacityNewtons);
            // Analytic held-force approximation to N semi-implicit native substeps.
            // This is explicitly validated against saved Box2D continuations; equality is NOT assumed.
            var a=applied.NetForce/mass;var alpha=applied.NetTorque/inertia;
            var coefficient=(p.SolverSubsteps+1f)/(2*p.SolverSubsteps);
            var position=v.Position+v.Velocity*h+a*(coefficient*h*h);
            var velocity=v.Velocity+a*h;var spin=v.AngularVelocity+alpha*h;
            var heading=(float)BikeAngles.Wrap(v.Heading+v.AngularVelocity*h+coefficient*alpha*h*h);
            var road=frame.Project(position);var ds=Vector2.Distance(v.Position,position);distance+=ds;
            v=v with{Position=position,Velocity=velocity,AngularVelocity=spin,Heading=heading,Road=road,
                TotalReferenceMeters=v.TotalReferenceMeters+frame.UnwrapDelta(v.Road.ReferenceMeters,road.ReferenceMeters),DistanceMeters=v.DistanceMeters+ds};
            wheel.Commit();steering.Commit();time+=duration;Point();
        }
        if(pending is { } terminal&&time+1e-12>=terminal.RequestedActivation){
            v=v with{Drive=terminal.Drive,TargetOffsetMeters=disposablePlan.Target(v,time)};
            activations.Add(new(time,terminal.Drive,ReferenceEquals(terminal,input.Pending)));
            points[^1]=points[^1] with{Rider=v};
        }
        return new(points.ToArray(),activations.ToArray(),queries,forces,inner,outer,frontUse,rearUse,maxRate,distance);
    }
}
