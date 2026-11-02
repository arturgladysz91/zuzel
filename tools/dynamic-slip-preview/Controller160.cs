using SharedWorldSpeedway;
using ThreeLap158;
namespace Dynamic160;
/// <summary>OFF is direct delegation to the unchanged158 planner, including its defaults.</summary>
public sealed class Controller160 {
 public Planner158? Legacy{get;}public Planner160? Dynamic{get;}
 public Controller160(RiderSpec spec,Settings160? settings=null,Action? guard=null){if(settings?.Enabled==true)Dynamic=new(spec,false,new(Enabled:true,MaximumRoots:3),guard,performance:Hierarchical155.Program155.Options() with{RankPolicies=true},settings:settings);else Legacy=new(spec,false,new(Enabled:true,MaximumRoots:3),guard);}
 public Decision144 Choose(FreeRacingInput input,CrankClutch144 crank)=>Dynamic is not null?Dynamic.Choose(input,crank):Legacy!.Choose(input,crank);
}
