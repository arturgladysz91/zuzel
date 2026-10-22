using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>PROVISIONAL intent parameters, not tyre calibration or speed constraints on the body.</summary>
public sealed record PreparedCornerOptions(bool Path=true,bool Throttle=true,double PreferredOffsetMeters=5,
    double AlternativeOffsetMeters=8.5)
{
    public void Validate(){if(!double.IsFinite(PreferredOffsetMeters)||!double.IsFinite(AlternativeOffsetMeters)
        ||PreferredOffsetMeters<.5||AlternativeOffsetMeters<.5||!Path&&!Throttle)throw new ArgumentException("Finite legal line intents and at least one planning action required.");}
}
public sealed record PreparedSpeedPoint(double S,double Curvature,double CurvatureDerivative,double Offset,
    double Inner,double Outer,double RequiredNormal,double ContactCeiling,double ResponseCeiling,
    double SpeedLimit,double CoastEstimate,double CoastUpperBound,bool ResidualOverspeed);
public sealed record PreparedCornerReview(double Time,double Activation,double Drive,double SpeedTarget,double Beta,double BetaDot,
    double Wheelspin,double ActualFx,double ActualFy,double ImmediateNormal,double HeldSteeringNormal,double ContactCeiling,double FrontBudget,double RearBudget,
    double ResponseSeconds,int ContactQueries,bool NoCertifiedCoastSolution,string Reason,PreparedSpeedPoint[] Profile);

/// <summary>Geometry and work estimates only. Does not integrate a rigid body or commit a wheel.</summary>
public sealed class PreparedCornerPlan
{
    public const double ReviewSeconds=.1,ForceReserve=.85,WheelAllowanceSeconds=.55,ThrottleRisePerSecond=.5;
    public const double SpeedFeedbackPerMeterPerSecond=.16,GeometryStepMeters=2,PreviewSeconds=.7;
    public const double FootprintReserveMeters=.5;
    public PreparedCornerOptions Options {get;}
    public AuthoredRacingPath Path {get;}
    public double ActivationSeconds {get;}
    public double NextReviewSeconds {get;private set;}
    public double BendExitMeters {get;}
    public double TransitionStartMeters {get;}
    public double TransitionEndMeters {get;}
    public double SelectedOffsetMeters {get;}
    private readonly RacingLineGeometry geometry;
    private float plannedDrive=1;
    public PreparedCornerPlan(RiderView v,RiderSpec spec,TrackFrame frame,WorldParameters p,double time,float mass,
        RearWheelState wheel,TwoContactBikeState steering,PreparedCornerOptions? options=null,TrackStateSnapshot? surface=null)
    {
        Options=options??new();Options.Validate();geometry=new(frame);
        ActivationSeconds=time+RiderController.ReactionSeconds(spec);
        // Static topology is shared by both bends and lap wraps. No Egg coordinates or turn numbers.
        var lap=Math.Floor(v.TotalReferenceMeters/frame.LapMeters)*frame.LapMeters;
        var bends=frame.Track.CornerTopology.Corners.SelectMany(c=>new[]{lap,lap+frame.LapMeters}.Select(origin=>
            (Start:origin+frame.SegmentStartMeters(c.StartSegmentIndex),End:origin+frame.SegmentStartMeters(c.StartSegmentIndex+c.SegmentCount))))
            .Where(c=>c.End>v.TotalReferenceMeters).OrderBy(c=>c.Start).ToArray();
        if(bends.Length==0)throw new InvalidOperationException("No future bend in closed topology.");
        var bend=bends[0];BendExitMeters=bend.End;
        // Whole initial reaction can accelerate, so delay the geometric transition by an upper distance bound.
        var observedSurface=surface?.SampleSurface(v.Road.Segment,Math.Clamp((float)v.Road.OffsetMeters/Math.Max(1,frame.MinimumUsableWidthMeters)*4,0,4))??new(1,0,.5f);
        var budget=LoadBasedTyreGrip.Budget(mass,observedSurface,p.TwoContactBike??TwoContactBikeParameters.Default,p.LoadBasedTyres);
        var reaction=RiderController.ReactionSeconds(spec);
        TransitionStartMeters=v.TotalReferenceMeters+Math.Max(0,frame.ReferenceSpeed(v))*reaction+.5*budget.TotalCapacityNewtons/mass*reaction*reaction;
        TransitionEndMeters=Math.Max(TransitionStartMeters+v.Speed*(p.LateralResponseSeconds+4*p.SlipYawResponseSeconds),
            (bend.Start+bend.End)/2);
        AuthoredRacingPath Make(double offset)=>AuthoredRacingPath.Prepared(frame,new(TransitionStartMeters,v.Road.OffsetMeters),new(TransitionEndMeters,offset));
        var preferred=Make(Options.PreferredOffsetMeters);var alternative=Make(Options.AlternativeOffsetMeters);
        // Two line intents only. Prefer the requested line unless its conservative coast estimate fails.
        bool Legal(AuthoredRacingPath path)=>Enumerable.Range(0,(int)Math.Ceiling((bend.End-v.TotalReferenceMeters)/.25)+1)
            .Select(i=>path.At(Math.Min(bend.End,v.TotalReferenceMeters+i*.25))).All(q=>new[]{0d,-Math.PI/4,Math.PI/4}.All(beta=>{
                var clearance=geometry.Clearance(Pose(v,frame,q) with{Heading=(float)(q.Heading+beta)});
                var interpolation=.125*q.Metric*(1+Math.Abs(q.Curvature)*SpeedwayBikeDimensions.Reference.OverallMechanicalLengthMeters/2);
                return clearance.Inner>=FootprintReserveMeters+interpolation&&clearance.Outer>=FootprintReserveMeters+interpolation;}));
        var pendingWork=MaximumWheelPower(p)*v.Drive*reaction;
        double Score(AuthoredRacingPath path)=>Enumerable.Range(0,(int)Math.Ceiling((bend.End-v.TotalReferenceMeters)/GeometryStepMeters)+1)
            .Select(i=>path.At(Math.Min(bend.End,v.TotalReferenceMeters+i*GeometryStepMeters)))
            .Max(q=>mass*Math.Pow(Coast(v,wheel,pendingWork,Math.Max(0,q.ReferenceMeters-v.TotalReferenceMeters),mass).Upper,2)*Math.Abs(q.Curvature)/(ForceReserve*budget.TotalCapacityNewtons));
        if(!Legal(preferred)&&!Legal(alternative))throw new InvalidOperationException("No legal footprint for either line intent.");
        var useAlternative=!Legal(preferred)||Score(preferred)>1&&Legal(alternative)&&Score(alternative)<Score(preferred);
        Path=useAlternative?alternative:preferred;SelectedOffsetMeters=useAlternative?Options.AlternativeOffsetMeters:Options.PreferredOffsetMeters;
    }
    public static RiderView Pose(RiderView v,TrackFrame frame,AuthoredPathSample q)=>v with{
        Position=new((float)q.Position.X,(float)q.Position.Y),Heading=(float)q.Heading,Road=frame.RoadAt(q.ReferenceMeters,q.OffsetMeters),TotalReferenceMeters=q.ReferenceMeters};
    public PathGuidanceSample? Guidance(RiderView v,WorldParameters p,double time)=>Options.Path&&time+1e-10>=ActivationSeconds?Path.Guidance(v,p,true):null;
    public float Target(RiderView v,double time)=>time+1e-10<ActivationSeconds?v.TargetOffsetMeters:Options.Path?(float)Path.Offset(v.TotalReferenceMeters).D:(float)Options.PreferredOffsetMeters;
    /// <summary>Resistance-only work estimate; no tyre loss is assumed to provide braking.
    /// Upper bound releases ALL stored wheel energy to translation and assumes no bearing/side loss.
    /// Pending drive work is supplied separately; no predicted speed ever writes to RiderView.</summary>
    public static (double Estimate,double Upper) Coast(RiderView v,RearWheelState wheel,double pendingEngineWork,double distance,float mass)
    {
        if(distance<0||pendingEngineWork<0)throw new ArgumentOutOfRangeException(nameof(distance));
        double Integrate(double square){var n=Math.Max(1,(int)Math.Ceiling(distance/GeometryStepMeters));var ds=distance/n;
            for(var i=0;i<n;i++){
                var speed=Math.Sqrt(Math.Max(0,square));var a=LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons((float)speed)/mass;
                var predicted=Math.Sqrt(Math.Max(0,square-2*a*ds));var middle=(speed+predicted)/2;
                square=Math.Max(0,square-2*LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons((float)middle)*ds/mass);
            }return Math.Sqrt(square);}
        var wheelEnergy=.5*wheel.Parameters.WheelInertiaKgMetersSquared*Math.Pow(wheel.AngularVelocityRadPerSecond,2);
        return(Integrate(v.Speed*v.Speed+2*pendingEngineWork/mass),Integrate(v.Speed*v.Speed+2*(pendingEngineWork+wheelEnergy)/mass));
    }
    public static double MaximumWheelPower(WorldParameters p)
    {
        double maximum=0;var curve=Engine500.Curve;
        for(var i=1;i<curve.Count;i++){
            var a=curve[i-1];var b=curve[i];var slope=(b.NewtonMeters-a.NewtonMeters)/(b.Rpm-a.Rpm);var intercept=a.NewtonMeters-slope*a.Rpm;
            var candidates=new[]{a.Rpm,b.Rpm,slope<0?Math.Clamp(-intercept/(2*slope),a.Rpm,b.Rpm):a.Rpm};
            maximum=Math.Max(maximum,candidates.Max(rpm=>rpm*(intercept+slope*rpm)*Math.PI/30));
        }
        return maximum*(p.Engine500??Engine500Parameters.Default).Efficiency;
    }
    public PreparedCornerReview Observe(RiderView v,RiderSpec spec,TrackFrame frame,TrackStateSnapshot surface,WorldParameters p,
        double time,float mass,float inertia,RearWheelState wheel,TwoContactBikeState steering)
    {
        var bike=p.TwoContactBike??TwoContactBikeParameters.Default;var queryCount=0;
        TwoContactForces Query(float angle){queryCount++;return TwoContactBikeDynamics.Forces(v,spec,frame,surface,p,time,mass,
            new(wheel.AngularVelocityRadPerSecond*wheel.Parameters.WheelRadiusMeters,wheel.Parameters),new(angle),0,steeringOverride:angle);}
        var f=Query(steering.SteeringRadians);var motion=TravelDirectionState.Observe(v,f.NetForce,mass);
        double Normal(TwoContactForces contact)=>v.Speed<.5?0:TravelDirectionState.Observe(v,contact.NetForce,mass).ChiDot*mass*v.Speed;
        var held=Enumerable.Range(0,9).Select(i=>Query(-bike.MaximumSteeringRadians+2*bike.MaximumSteeringRadians*i/8)).ToArray();
        var maxNormal=held.Max(Normal);var reachable=new[]{-1f,0,1}.Select(sign=>Query(Math.Clamp(steering.SteeringRadians+sign*bike.SteeringRateRadiansPerSecond*.01f,-bike.MaximumSteeringRadians,bike.MaximumSteeringRadians))).Max(Normal);
        var outwardNormal=-held.Min(Normal);
        var rear=f.TyreLoads!.RearCapacityNewtons;var ceiling=f.TyreLoads.FrontCapacityNewtons+
            Math.Sqrt(Math.Max(0,rear*rear-f.RearLongitudinal*f.RearLongitudinal))*Math.Abs(Math.Cos(motion.Beta))+Math.Max(0,f.RearLongitudinal*Math.Sin(motion.Beta));
        var yawAcceleration=held.Max(contact=>Math.Abs(contact.NetTorque))/inertia;
        var lookahead=Math.Max(v.Speed*3,GeometryStepMeters);var profile=new List<PreparedSpeedPoint>();
        var horizon=Math.Max(BendExitMeters,v.TotalReferenceMeters+lookahead);
        // A bounded geometry-only horizon; do not look past the next full bend.
        horizon=Math.Min(horizon,v.TotalReferenceMeters+frame.LapMeters/2);
        var reaction=RiderController.ReactionSeconds(spec);
        var pendingPower=MaximumWheelPower(p)*v.Drive;
        var pendingWork=pendingPower*reaction;
        var oldLine=AuthoredRacingPath.Prepared(frame,new(v.TotalReferenceMeters-1,Options.PreferredOffsetMeters),new(horizon+1,Options.PreferredOffsetMeters));
        double responseMaximum=0;
        for(var s=v.TotalReferenceMeters;s<=horizon+1e-6;s+=GeometryStepMeters){
            var q=Options.Path?Path.At(s):oldLine.At(s);
            var gap=geometry.Clearance(Pose(v,frame,q));var ahead=Math.Max(0,s-v.TotalReferenceMeters);
            var desiredYaw=v.Speed*q.Curvature;
            var steerTravel=Math.Abs(Math.Atan((bike.FrontAxleFromComMeters+bike.RearAxleFromComMeters)*q.Curvature)-steering.SteeringRadians)/bike.SteeringRateRadiansPerSecond;
            var response=reaction+p.LateralResponseSeconds+4*p.SlipYawResponseSeconds+WheelAllowanceSeconds+steerTravel
                +Math.Abs(desiredYaw-v.AngularVelocity)/Math.Max(.01,yawAcceleration);
            responseMaximum=Math.Max(responseMaximum,response);
            var growth=Math.Clamp(ahead/Math.Max(.1,v.Speed)/response,0,1);
            var signedCeiling=q.Curvature>=0?ceiling:f.TyreLoads.FrontCapacityNewtons+
                Math.Sqrt(Math.Max(0,rear*rear-f.RearLongitudinal*f.RearLongitudinal))*Math.Abs(Math.Cos(motion.Beta))+Math.Max(0,-f.RearLongitudinal*Math.Sin(motion.Beta));
            var heldNormal=q.Curvature>=0?maxNormal:outwardNormal;
            var responseCeiling=Math.Min(signedCeiling,Math.Max(0,heldNormal)+(signedCeiling-Math.Max(0,heldNormal))*growth);
            // Necessary normal-force/response envelope, never a sufficient tracking certificate.
            var normal=mass*v.Speed*v.Speed*Math.Abs(q.Curvature);
            var limit=Math.Abs(q.Curvature)<1e-6?double.PositiveInfinity:Math.Sqrt(ForceReserve*Math.Max(0,responseCeiling)/(mass*Math.Abs(q.Curvature)));
            var coast=Coast(v,wheel,pendingWork,ahead,mass);
            profile.Add(new(s,q.Curvature,(Options.Path?Path:oldLine).CurvatureDerivative(s),q.OffsetMeters,gap.Inner,gap.Outer,normal,signedCeiling,responseCeiling,
                double.IsFinite(limit)?limit:1000,coast.Estimate,coast.Upper,coast.Upper>limit));
        }
        var upcoming=profile.Where(q=>q.S<=v.TotalReferenceMeters+lookahead).ToArray();
        var target=upcoming.Min(q=>q.SpeedLimit);var kappa=RearWheelDynamics.SlipRatio(Vector2.Dot(f.RearVelocity,f.BodyForward),wheel.AngularVelocityRadPerSecond,wheel.Parameters);
        var frontLoss=Math.Max(0,-Vector2.Dot(f.FrontForce,f.FrontVelocity));
        var rearLoss=Math.Max(0,-f.RearLateral*Vector2.Dot(f.RearVelocity,f.BodyLeft));
        var dragPower=Math.Max(0,-Vector2.Dot(f.Resistance,v.Velocity));
        var availablePower=f.Engine!.AvailableWheelTorqueNewtonMeters*wheel.AngularVelocityRadPerSecond;
        var sustaining=(frontLoss+rearLoss+dragPower)/Math.Max(1,availablePower);
        var request=target>100?1:Math.Clamp(sustaining+SpeedFeedbackPerMeterPerSecond*(target-v.Speed),0,1);
        if(Math.Abs(kappa)>.25||motion.Beta*motion.BetaDot>.05)request=0;
        plannedDrive=(float)Math.Min(request,plannedDrive+ThrottleRisePerSecond*ReviewSeconds);
        NextReviewSeconds=time+ReviewSeconds;
        return new(time,time+reaction,Options.Throttle?plannedDrive:.15,target,motion.Beta,motion.BetaDot,kappa,f.RearLongitudinal,f.RearLateral,
            reachable,maxNormal,ceiling,f.TyreLoads.FrontCapacityNewtons,rear,responseMaximum,queryCount,upcoming.Any(q=>q.ResidualOverspeed),
            upcoming.Any(q=>q.ResidualOverspeed)?"coasting/response bound does not certify requested speed; bounded drive request on gentler legal intent":"reserved envelope; track speed by engine work only",profile.ToArray());
    }
}
