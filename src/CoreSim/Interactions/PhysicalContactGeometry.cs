using CoreSim.PhysicalSpace;

namespace CoreSim.Interactions;

/// <summary>Locates a manifold for an already verified contact. It never decides whether a contact exists.</summary>
public static class PhysicalContactGeometry
{
    public const double DirectionToleranceMeters = 1e-9;
    public static MechanicalContactManifold? AtVerifiedTouch(PhysicalContactInput input, out string reason)
    {
        var c = input.Contact; var a = input.PoseA; var b = input.PoseB; reason = "";
        if (!c.EligibleForFutureInteraction) { reason = "#55 contact is ineligible or ambiguous"; return null; }
        if (a is null || b is null) { reason = "Missing verified first-touch pose coverage"; return null; }
        if (a.RiderId != c.RiderA || b.RiderId != c.RiderB || a.FrameId != c.FrameId || b.FrameId != c.FrameId
            || Math.Abs(a.CommonTimeSeconds-c.FirstTouchCommonTimeSeconds!.Value) > GeometryNumerics.TimeToleranceSeconds
            || Math.Abs(b.CommonTimeSeconds-c.FirstTouchCommonTimeSeconds.Value) > GeometryNumerics.TimeToleranceSeconds)
        { reason = "Missing matching first-touch pose coverage in the verified metric frame"; return null; }
        // Event components describe the minimum later in the interval. Select the
        // touching components at onset with #55's existing, unchanged separation.
        var separation = SeparationInPhysicalOrder(a,b);
        if (separation.SignedMeters > GeometryNumerics.MinimumSeparationToleranceMeters)
        { reason = "Supplied first-touch poses do not support the verified contact"; return null; }
        var ca = a.Footprint.Component(separation.ComponentA); var cb = b.Footprint.Component(separation.ComponentB);
        var (pa,pb) = ClosestAxisPoints(ca,cb);
        var delta = pb-pa; var source = ContactNormalSource.ClosestComponentAxes;
        if (delta.Length <= DirectionToleranceMeters) { delta = b.Position-a.Position; source = ContactNormalSource.BikeCenters; }
        if (delta.Length <= DirectionToleranceMeters) { delta = input.VelocityB-input.VelocityA; source = ContactNormalSource.RelativeVelocity; }
        if (delta.Length <= DirectionToleranceMeters)
        {
            // The shared reference lateral axis is deterministic, but coincident
            // identical bikes provide no A->B orientation. Do not invent an impact.
            var h = BikeAngles.Interpolate(a.ReferenceTangentHeadingRadians,b.ReferenceTangentHeadingRadians,.5);
            var referenceLateral = new MeterPoint(-Math.Sin(h),Math.Cos(h));
            reason = FormattableString.Invariant($"GeometryUnresolved: reference lateral axis ({referenceLateral.X:R},{referenceLateral.Y:R}) has no physical A-to-B sign");
            return null;
        }
        var normal = delta * (1/delta.Length);
        var pointA = SurfaceAlongRay(ca,pa,normal); var pointB = SurfaceAlongRay(cb,pb,normal*-1);
        return new(a.RiderId,b.RiderId,separation.ComponentA,separation.ComponentB,pointA,pointB,
            (pointA+pointB)*.5,normal,pointA-a.Position,pointB-b.Position,separation.SignedMeters,source);
    }
    private static FootprintSeparation SeparationInPhysicalOrder(PhysicalBikePose a, PhysicalBikePose b)
    {
        // #55's equal-minimum component priority is intentionally unchanged.
        // Present poses to it in geometric track-frame order, so simultaneous
        // chassis/bar ties cannot attach a different manifold after an ID swap.
        var forward=new MeterPoint(Math.Cos(a.ReferenceTangentHeadingRadians)+Math.Cos(b.ReferenceTangentHeadingRadians),
            Math.Sin(a.ReferenceTangentHeadingRadians)+Math.Sin(b.ReferenceTangentHeadingRadians));
        if(forward.Length<=DirectionToleranceMeters) forward=new(1,0); // fixed basis of the supplied metric frame
        var delta=b.Position-a.Position;var along=MeterPoint.Dot(delta,forward);var lateral=MeterPoint.Dot(delta,new(-forward.Y,forward.X));
        var reverse=along < -DirectionToleranceMeters || Math.Abs(along)<=DirectionToleranceMeters&&lateral<0;
        if(!reverse) return MechanicalSeparation.Between(a,b);
        var result=MechanicalSeparation.Between(b,a);
        return new(result.SignedMeters,result.ComponentB,result.ComponentA);
    }
    private static MeterPoint SurfaceAlongRay(MechanicalCapsule capsule, MeterPoint origin, MeterPoint direction)
    {
        // For nondegenerate closest axes this is exactly axisPoint +/- normal*radius.
        // Intersecting axes need the actual capsule boundary; shifting an interior
        // point by radius along the capsule's length would still be inside it.
        var axis=capsule.End-capsule.Start; var length=axis.Length;
        if(length==0) return origin+direction*capsule.RadiusMeters;
        var unit=axis*(1/length); var along=MeterPoint.Dot(direction,unit);
        var coordinate=MeterPoint.Dot(origin-capsule.Start,unit); var perpendicular=Math.Abs(MeterPoint.Cross(unit,direction));
        var exit=0d;
        if(perpendicular>DirectionToleranceMeters)
        {
            var distance=capsule.RadiusMeters/perpendicular;var at=coordinate+along*distance;
            if(at>=0&&at<=length) exit=distance;
        }
        void Cap(MeterPoint center,bool start)
        {
            var delta=origin-center;var dot=MeterPoint.Dot(delta,direction);
            var discriminant=dot*dot+capsule.RadiusMeters*capsule.RadiusMeters-MeterPoint.Dot(delta,delta);
            if(discriminant<0) return;
            var distance=-dot+Math.Sqrt(discriminant);var at=coordinate+along*distance;
            if(distance>=0&&(start?at<=0:at>=length)) exit=Math.Max(exit,distance);
        }
        Cap(capsule.Start,true);Cap(capsule.End,false);
        return origin+direction*exit;
    }
    private static (MeterPoint A, MeterPoint B) ClosestAxisPoints(MechanicalCapsule a, MechanicalCapsule b)
    {
        var u=a.End-a.Start; var v=b.End-b.Start; var w=b.Start-a.Start;
        var cross=MeterPoint.Cross(u,v);
        if(cross!=0)
        {
            var s=MeterPoint.Cross(w,v)/cross; var t=MeterPoint.Cross(w,u)/cross;
            if(s>=0&&s<=1&&t>=0&&t<=1) return (a.Start+u*s,b.Start+v*t);
        }
        static MeterPoint Project(MeterPoint p, MechanicalCapsule c)
        { var d=c.End-c.Start; var square=MeterPoint.Dot(d,d); return c.Start+d*(square==0?0:Math.Clamp(MeterPoint.Dot(p-c.Start,d)/square,0,1)); }
        var candidates=new[]{(A:a.Start,B:Project(a.Start,b)),(A:a.End,B:Project(a.End,b)),
            (A:Project(b.Start,a),B:b.Start),(A:Project(b.End,a),B:b.End)};
        var minimum=candidates.Min(p=>MeterPoint.Dot(p.B-p.A,p.B-p.A));
        // Average the equally closest parallel solutions: symmetric, geometric,
        // no endpoint, rider-ID or world-axis tie priority.
        var ties=candidates.Where(p=>Math.Abs(MeterPoint.Dot(p.B-p.A,p.B-p.A)-minimum)<=DirectionToleranceMeters*DirectionToleranceMeters).ToArray();
        return (new(ties.Sum(p=>p.A.X)/ties.Length,ties.Sum(p=>p.A.Y)/ties.Length),
            new(ties.Sum(p=>p.B.X)/ties.Length,ties.Sum(p=>p.B.Y)/ties.Length));
    }
}
