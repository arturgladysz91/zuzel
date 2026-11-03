using SharedWorldSpeedway;
namespace Continuation161;
public sealed class Controller161 {
 public Dynamic160.Controller160? Legacy{get;}public Planner161? Dynamic{get;}
 public Controller161(RiderSpec spec,Settings161? settings=null,Action? guard=null){if(settings?.Enabled==true)Dynamic=new(spec,false,new(Enabled:true,MaximumRoots:3),guard,settings:settings);else Legacy=new(spec);}
 public Decision144 Choose(FreeRacingInput input,CrankClutch144 crank)=>Dynamic is not null?Dynamic.Choose(input,crank):Legacy!.Choose(input,crank);
}
