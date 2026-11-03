using Continuation161;
using SharedWorldSpeedway;
namespace Instrumentation163;
public sealed class Controller163 {
 public Controller161? Legacy{get;}
 public Planner163? Experimental{get;}
 public Controller163(RiderSpec spec,bool enabled=false){if(enabled)Experimental=new(spec,false,new(Enabled:true,MaximumRoots:3));else Legacy=new(spec);}
 public Decision144 Choose(FreeRacingInput input,CrankClutch144 crank)=>Experimental is not null?Experimental.Choose(input,crank):Legacy!.Choose(input,crank);
}
