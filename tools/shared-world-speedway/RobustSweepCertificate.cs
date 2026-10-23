using System.Numerics;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>Isolated diagnostic: certify a tube around the endpoint pose chord, never reduce edge margins.
/// For a collision-free held wrench, semi-implicit substep positions depart from the endpoint chord
/// by at most |a|h²/8. The same bound applies to yaw acceleration; sum atan integration errors.
/// An extra0.5mm covers float pose arithmetic in the explicitly bounded domain below.
/// This certificate is NOT used to accept A/B or final-test episodes.</summary>
public static class RobustSweepCertificate
{
    public const double FloatTubeMeters=.0005;
    public sealed record Proof(double Minimum,int Queries,double TubeError,string Assumptions);
    public static Proof Refine(RiderView a,RiderView b,double seconds,TwoContactForces force,float mass,float inertia,int substeps,RacingLineGeometry boundary){
        if(seconds<=0||seconds>.020001||substeps<1||a.Speed>30||b.Speed>30||Math.Max(Math.Abs(a.AngularVelocity),Math.Abs(b.AngularVelocity))>10
            ||Math.Max(Math.Max(Math.Abs(a.Position.X),Math.Abs(a.Position.Y)),Math.Max(Math.Abs(b.Position.X),Math.Abs(b.Position.Y)))>=256)
            return new(double.NegativeInfinity,0,double.PositiveInfinity,"outside bounded diagnostic assumptions");
        var radius=SpeedwayBikeDimensions.Reference.BoundingRadiusMeters;var angle=Math.Abs(BikeAngles.Wrap(b.Heading-a.Heading));
        var acceleration=force.NetForce.Length()/mass;var alpha=Math.Abs(force.NetTorque/inertia);var omega=Math.Max(Math.Abs(a.AngularVelocity),Math.Abs(b.AngularVelocity));
        var tube=(acceleration+radius*alpha)*seconds*seconds/8+radius*substeps*Math.Pow(omega*seconds/substeps,3)/3+FloatTubeMeters;
        const int pieces=32;var minimum=double.PositiveInfinity;var delta=BikeAngles.Wrap(b.Heading-a.Heading);
        for(var i=0;i<=pieces;i++){
            var t=(double)i/pieces;var pose=a with{Position=Vector2.Lerp(a.Position,b.Position,(float)t),Heading=(float)BikeAngles.Wrap(a.Heading+t*delta)};
            var gap=boundary.Clearance(pose);minimum=Math.Min(minimum,Math.Min(gap.Inner,gap.Outer));
        }
        var segmentMotion=(Vector2.Distance(a.Position,b.Position)+radius*angle)/pieces;
        return new(minimum-segmentMotion/2-tube,pieces+1,tube,"one isolated collision-free body; original constant held contact wrench; original substeps; h<=20ms, |XY|<256m, v<=30m/s, |yaw|<=10rad/s; existing edge allowances +0.5mm retained");
    }
}
