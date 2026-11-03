using Continuation161;
using SharedWorldSpeedway;
namespace S3Performance162;
public sealed class Controller162 {
 public Controller161? Legacy{get;}
 public Planner162? Optimized{get;}
 public Controller162(RiderSpec spec,bool enabled=false){if(enabled)Optimized=new(spec,false,new(Enabled:true,MaximumRoots:3));else Legacy=new(spec);}
 public Decision144 Choose(FreeRacingInput input,CrankClutch144 crank)=>Optimized is not null?Optimized.Choose(input,crank):Legacy!.Choose(input,crank);
}
