using System.Numerics;
using CoreSim;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static class ContactStep191
{
    private static readonly PhysicalContactParameters Severity = new();
    internal static double Limit(IReadOnlyList<RiderView> riders,double dt,double maxTravel)
    {
        double movement=0;
        var radius=SpeedwayBikeDimensions.Reference.BoundingRadiusMeters;
        for(int i=0;i<riders.Count;i++)
        for(int j=i+1;j<riders.Count;j++)
        {
            var a=riders[i]; var b=riders[j];
            double relative=Vector2.Distance(a.Velocity,b.Velocity)
                +radius*(Math.Abs(a.AngularVelocity)+Math.Abs(b.AngularVelocity));
            if(Vector2.Distance(a.Position,b.Position)<=2*radius+relative*dt+.1)
                movement=Math.Max(movement,relative*dt);
        }
        double steps=Math.Max(1,Math.Ceiling(movement/maxTravel));
        if(steps>128) throw new InvalidOperationException("Motion exceeds the existing nearby substep budget.");
        return dt/steps;
    }

    // Same consequence projection as WorldRunner, applied after Box2D. An
    // impairment changes future drive through H25, never the accepted motion.
    internal static (RiderView Rider,ConsequenceObservation Observation) Consequence(RiderView own,
        RiderView other,RiderSpec spec,float mass,float otherMass,TrackStateSnapshot surface,
        double time,double impulse,double closing)
    {
        var track=surface.SampleSurface(own.Road.Segment,Math.Clamp((float)own.Road.OffsetMeters/3,0,4));
        var reserve=PhysicalContactAnalyzer.Reserve(new(own.Id,spec.Profile.Gameplay,1,track.EffectiveGrip),Severity);
        double ratio=closing*otherMass/(mass+otherMass)/reserve.StabilityReserveMetersPerSecond;
        var result=PhysicalContactAnalyzer.Classify(ratio,Severity);
        var observation=new ConsequenceObservation(time,own.Id,other.Id,closing,impulse,
            reserve.StabilityReserveMetersPerSecond,ratio,result);
        if(own.Activity is RiderActivity.Crashed) return (own,observation);
        return result switch
        {
            PhysicalContactSeverity.Crash => (own with{Activity=RiderActivity.Crashed,Impairment=1,Drive=0},observation),
            PhysicalContactSeverity.Brush => (own,observation),
            _ => (own with{Activity=RiderActivity.Disturbed,
                Impairment=(float)new PhysicalContactConsequenceParameters().ControlLoss(ratio,Severity)},observation)
        };
    }

    internal static PhysicalBikePose Pose(RiderView v) => new(v.Id,"traffic191",new(v.Position.X,v.Position.Y),
        new(0,v.Heading-v.SlideRadians,v.Heading),SpeedwayBikeDimensions.Reference);
}

