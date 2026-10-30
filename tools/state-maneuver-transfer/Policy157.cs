using CoreSim.PhysicalSpace;
using System.Text.Json;
using Autonomous146;
using SharedWorldSpeedway;
namespace Transfer157;
public sealed record Prototype157(string Family,double[] Features,float Steering,float Drive);
/// <summary>Unordered current observation/action examples. No epoch, source identity,
/// sample index, trajectory successor, future point or absolute position is available.</summary>
public sealed class Policy157 {
 readonly Prototype157[] rows;
 public const double HalfLapMeters=159.389374970247;
 public static readonly double[] Scales={12,12,4,8,.25,.35,.25,.06,30,300,.06,1,.06,1,.06,1,.24,.24};
 public Policy157(){using var s=typeof(Policy157).Assembly.GetManifestResourceStream("state-maneuver-transfer.model157.json")??throw new IOException("Embedded frozen training model required");rows=JsonSerializer.Deserialize<Prototype157[]>(s)!;if(rows.Length==0||rows.Any(r=>r.Features.Length!=Scales.Length))throw new IOException("Invalid model");}
 public static double[] Features(State146 s){var i=s.Input;var v=i.Rider;var phase=v.TotalReferenceMeters%HalfLapMeters;var theta=phase/HalfLapMeters*2*Math.PI;var p=i.Pending.Where(p=>p.ObservedAt<i.Time-1e-8).OrderBy(p=>p.ActivationAt).ToArray();var first=p.FirstOrDefault();var last=p.LastOrDefault();double Wait(FreeRacingCommand? c)=>c is null?0:c.ActivationAt-i.Time;
  return new[]{Math.Sin(theta)*12,Math.Cos(theta)*12,v.Road.OffsetMeters,(double)v.Speed,(double)v.SlideRadians,(double)v.AngularVelocity,BikeAngles.Wrap(Math.Atan2(v.Velocity.Y,v.Velocity.X)-v.Road.Heading),(double)i.Steering,i.Omega,s.CrankOmega,(double)i.Active.SteeringRadians,(double)i.Active.Drive,(double)(first?.Action.SteeringRadians??i.Active.SteeringRadians),(double)(first?.Action.Drive??i.Active.Drive),(double)(last?.Action.SteeringRadians??i.Active.SteeringRadians),(double)(last?.Action.Drive??i.Active.Drive),Wait(first),Wait(last)};
 }
 public (FreeRacingAction Action,double Distance) Suggest(State146 state,string family){var f=Features(state);Prototype157? best=null;double distance=double.PositiveInfinity;foreach(var r in rows){if(r.Family!=family&&r.Family!="launch")continue;double d=0;for(int j=0;j<f.Length;j++){var q=(f[j]-r.Features[j])/Scales[j];d+=q*q;}if(d<distance){distance=d;best=r;}}if(best is null)throw new InvalidOperationException("No proposal for family");return(new(best.Steering,best.Drive),Math.Sqrt(distance));}
}
