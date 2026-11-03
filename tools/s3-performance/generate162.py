from pathlib import Path
import sys
def emit(name,s):
 s="#nullable enable\n"+s
 p=dst/name
 if "--check" in sys.argv:
  assert p.read_text(encoding="utf-8")==s,name
 else:p.write_text(s,encoding="utf-8",newline="\n")
r=Path('.');src=r/'tools/trajectory-continuation';dst=r/'tools/s3-performance'
s=(src/'Control161.cs').read_text(encoding='utf-8');s=s.replace('namespace Continuation161;','namespace S3Performance162;\nusing Continuation161;');s='\n'.join(l for l in s.splitlines() if not l.startswith('public sealed record Settings161') and not l.startswith('public enum Maneuver161') and not l.startswith('public sealed record Equilibrium161'))+'\n';s=s.replace('Control161','Control162')
s=s.replace(' readonly Runtime158 rt;', ' readonly ResponseCache162 responses;\n public ResponseStatistics162 ResponseStatistics=>responses.Statistics;\n readonly Runtime158 rt;')
s=s.replace('Settings161? settings=null){rt=runtime;', 'Settings161? settings=null,bool memoizeResponses=true,ResponseStatistics162? responseStatistics=null){responses=new(memoizeResponses,responseStatistics);rt=runtime;')
s=s.replace('public void Begin(){prefix.Begin();', 'public void Begin(){responses.Clear();prefix.Begin();')
s=s.replace('  new FreeRacingAction(steer,drive).Validate();var v=', '  new FreeRacingAction(steer,drive).Validate();return responses.Get(state,steer,drive,()=>ComputeResponse(state,steer,drive));\n }\n Robust150.Response150 ComputeResponse(State146 state,float steer,float drive){\n  var v=')
emit('Control162.Generated.cs',s)
s=(src/'Feedback161.cs').read_text(encoding='utf-8').replace('namespace Continuation161;','namespace S3Performance162;\nusing Continuation161;').replace('Feedback161','Feedback162').replace('Control161','Control162');emit('Feedback162.Generated.cs',s)
s=(src/'Generator161.cs').read_text(encoding='utf-8').replace('namespace Continuation161;','namespace S3Performance162;\nusing Continuation161;').replace('Generator161','Generator162').replace('Generation161','Generation162').replace('Control161','Control162')
s=s.replace(' readonly Runtime158 rt;', ' readonly bool memoizeResponses;public ResponseStatistics162 ResponseStatistics{get;}=new();\n readonly Runtime158 rt;')
s=s.replace('Settings161? settings=null){this.rt=rt;', 'Settings161? settings=null,bool memoizeResponses=true){this.memoizeResponses=memoizeResponses;this.rt=rt;').replace('Control=new(rt,settings??new(Enabled:true,Adaptive:true));','Control=new(rt,settings??new(Enabled:true,Adaptive:true),memoizeResponses,ResponseStatistics);').replace('new Control162(rt,settings);','new Control162(rt,settings,memoizeResponses,ResponseStatistics);')
emit('Generator162.Generated.cs',s)
s=(src/'Planner161.Generated.cs').read_text(encoding='utf-8').replace('namespace Continuation161;','namespace S3Performance162;\nusing Continuation161;').replace('Planner161','Planner162').replace('Generator161','Generator162').replace('Feedback161','Feedback162').replace('STUDY161','STUDY162').replace('Settings161? settings=null)', 'Settings161? settings=null,bool memoizeResponses=true)').replace('Generator=new(Runtime,Step,Guard,settings);','Generator=new(Runtime,Step,Guard,settings,memoizeResponses);')
emit('Planner162.Generated.cs',s)
