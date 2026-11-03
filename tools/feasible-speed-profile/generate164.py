from pathlib import Path
import sys
r=Path(__file__).resolve().parents[2]
for name in ['Control','Feedback','Generator','Planner']:
 filename=name+'161'+('.Generated' if name=='Planner' else '')+'.cs'
 s=(r/'tools/trajectory-continuation'/filename).read_text(encoding='utf-8')
 s=s.replace('namespace Continuation161;','namespace FeasibleSpeed164;\nusing Continuation161;')
 for n in ['Control','Feedback','Generator','Planner']:s=s.replace(n+'161',n+'164')
 s=s.replace('STUDY161','STUDY164')
 if name=='Control':
  s=s.replace('public sealed class Control164 {','public sealed class Control164 {\n public static Action<Control164,State146,FreeRacingAction>? DiagnosticObserver{get;set;}')
  s=s.replace(' public FreeRacingAction Action(State146 observed){',' public SpeedTarget164? LastSpeedProfile{get;private set;}\n public FreeRacingAction Action(State146 observed){')
  s=s.replace('speedTarget*=.95;}','speedTarget*=.95;}\n   LastSpeedProfile=SpeedProfile164.Propose(future,rt.Frame,speedTarget,steeringPreparation);speedTarget=LastSpeedProfile.TargetMetersPerSecond;')
  s=s.replace('Target=best.Detail};','Target=best.Detail,SpeedProfile=LastSpeedProfile};')
  s=s.replace('return new(steer,gas);','var selectedAction=new FreeRacingAction(steer,gas);DiagnosticObserver?.Invoke(this,observed,selectedAction);return selectedAction;')
 path=r/'tools/feasible-speed-profile'/(name+'164'+('.Generated' if name=='Planner' else '')+'.cs')
 if '--check' in sys.argv:assert path.read_text(encoding='utf-8')==s,'164 permitted proposal transformation drift: '+name
 else:path.write_text(s,encoding='utf-8',newline='\n')
