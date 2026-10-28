using Autonomous146;
using SharedWorldSpeedway;
namespace Efficient147;
/// <summary>All accepted/forecast state fields in explicit IEEE bits; no tolerance, rounding or hash-only equality.</summary>
public static class ExactKey147{
    public static string State(State146 state,double seconds=0,FreeRacingAction? action=null){
        using var stream=new MemoryStream(256);using var w=new BinaryWriter(stream);
        var i=state.Input;var v=i.Rider;var r=v.Road;
        w.Write(i.Time);w.Write(i.NextReview);w.Write(state.CrankOmega);w.Write(i.Omega);w.Write(i.Steering);
        w.Write(v.Id);w.Write(v.Position.X);w.Write(v.Position.Y);w.Write(v.Velocity.X);w.Write(v.Velocity.Y);w.Write(v.Heading);w.Write(v.AngularVelocity);
        w.Write(r.Segment);w.Write(r.Progress);w.Write(r.ReferenceMeters);w.Write(r.OffsetMeters);w.Write(r.Heading);w.Write(r.RadiusMeters);w.Write(r.CornerProgress);
        w.Write(v.TotalReferenceMeters);w.Write(v.DistanceMeters);w.Write((int)v.Activity);w.Write(v.Drive);w.Write(v.TargetOffsetMeters);w.Write(v.Impairment);w.Write((int)v.Context);
        w.Write(i.Active.SteeringRadians);w.Write(i.Active.Drive);w.Write(i.Pending.Length);
        foreach(var c in i.Pending){w.Write(c.ObservedAt);w.Write(c.ActivationAt);w.Write(c.Action.SteeringRadians);w.Write(c.Action.Drive);}
        w.Write(seconds);w.Write(action is not null);if(action is {} a){w.Write(a.SteeringRadians);w.Write(a.Drive);}
        return Convert.ToBase64String(stream.GetBuffer(),0,(int)stream.Length);
    }
}
