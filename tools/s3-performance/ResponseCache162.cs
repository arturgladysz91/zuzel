using Autonomous146;
using Efficient147;
using Robust150;
using SharedWorldSpeedway;
namespace S3Performance162;

public sealed class ResponseStatistics162 {
 public long Requests {get;internal set;}
 public long Hits {get;internal set;}
 public long Evaluations {get;internal set;}
 public long CapacityClears {get;internal set;}
}

/// <summary>Proposal-only, exact-bit memo. Every control owns its storage; no
/// result is exposed to Runtime158's fresh independent provider.</summary>
public sealed class ResponseCache162(bool enabled=true,ResponseStatistics162? statistics=null) {
 public const int MaximumEntries=32768;
 readonly Dictionary<(string State,int Steer,int Drive),Response150> entries=new();
 public ResponseStatistics162 Statistics{get;}=statistics??new();
 public int Count=>entries.Count;
 public void Clear()=>entries.Clear();
 public Response150 Get(State146 state,float steer,float drive,Func<Response150> calculate){
  new FreeRacingAction(steer,drive).Validate();Statistics.Requests++;
  if(!enabled){Statistics.Evaluations++;return calculate();}
  var key=(ExactKey147.State(state),BitConverter.SingleToInt32Bits(steer),BitConverter.SingleToInt32Bits(drive));
  if(entries.TryGetValue(key,out var hit)){Statistics.Hits++;return hit;}
  Statistics.Evaluations++;var value=calculate();
  if(entries.Count>=MaximumEntries){entries.Clear();Statistics.CapacityClears++;}
  entries.Add(key,value);return value;
 }
}
