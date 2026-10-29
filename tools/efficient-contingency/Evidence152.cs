using System.Security.Cryptography;
using Autonomous146;
using Contingency151;
using Robust150;
namespace Efficient152;
/// <summary>Content-addressed policy DAG: shared tape inputs, never duplicated dense predictions.</summary>
public static class Evidence152 {
 public static object Compact(Review151 r){var pool=new Dictionary<string,object>();string Tape(Tape150 t){var data=new{Start=t.States[0],t.CreatedAt,t.Goal,t.SafetyGoal,t.Actions,t.Arrival,t.Safety};var bytes=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(data,Program152.Json);var id=Convert.ToHexString(SHA256.HashData(bytes));pool.TryAdd(id,data);return id;}
  var nominal=r.Nominal is null?null:Tape(r.Nominal);var leaves=r.Terminals.Select(t=>new{t.Id,t.Node,Tape=Tape(t.Tape)}).ToArray();var rejected=r.RejectedTrees.Select(t=>new{t.Origin,t.Nodes,Terminals=t.Terminals.Select(x=>new{x.Id,x.Node,Tape=Tape(x.Tape)}).ToArray()}).ToArray();
  return new{Format="PolicyDag152-v1",r.Time,r.Observed,r.Goal,r.SafetyGoal,r.Reason,r.ConditionalCertificate,r.Independent,r.IrrevocableUntil,r.Tree,Terminals=leaves,RejectedTrees=rejected,r.Attempts,Nominal=nominal,Tapes=pool,PhysicalHash=Bits152.Review(r),r.Profile,r.Counts,r.CpuSeconds,r.PhysicalFaults,Reproduction="Replay each tape from its Start using frozen dynamics and Actions. Compare the full dense review PhysicalHash after regenerating the unchanged planner. Stored policies are inputs, never certificates for new observations."};
 }
}
