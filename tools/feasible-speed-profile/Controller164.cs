using Continuation161;
using SharedWorldSpeedway;
namespace FeasibleSpeed164;
public sealed class Controller164 {
 public Controller161? Legacy{get;}public Planner164? Experimental{get;}
 public Controller164(RiderSpec spec,bool enabled=false){if(enabled)Experimental=new(spec,false,new(Enabled:true,MaximumRoots:3),settings:new(Enabled:true,Adaptive:true));else Legacy=new(spec);}
 public Decision144 Choose(FreeRacingInput input,CrankClutch144 crank)=>Experimental is not null?Experimental.Choose(input,crank):Legacy!.Choose(input,crank);
}
