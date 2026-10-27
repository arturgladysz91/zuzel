"""Only #143 native beta-detector parameter and observation callback; physical code unchanged."""
from pathlib import Path
import sys
root=Path(__file__).resolve().parents[2]
s=(root/'tools/shared-world-speedway/RacingObjective142/NativeCapture142.cs').read_text(encoding='utf-8')
s=s[:s.index('\npublic sealed record NativeCaptureResult142')]
s=s.replace('public static class NativeCapture142','public static class NativeCapture143')
def sub(a,b):
    global s
    assert s.count(a)==1,(a,s.count(a))
    s=s.replace(a,b)
sub('double cpuCeilingSeconds=440)', 'double cpuCeilingSeconds=1800,double? betaLimitDegrees=45,Action<RunResult>? completedObservation=null)')
sub('if(Math.Abs(v.SlideRadians)>Math.PI/4)failure="control envelope";',
    'if(betaLimitDegrees is{} betaLimit&&(betaLimit==45?Math.Abs(v.SlideRadians)>Math.PI/4:Math.Abs(v.SlideRadians)>betaLimit*Math.PI/180))failure="control envelope";')
sub('failure??=controllerHalt?.Invoke();var completed=', 'completedObservation?.Invoke(result);failure??=controllerHalt?.Invoke();var completed=')
p=root/'tools/shared-world-speedway/RacingConstraints143/NativeCapture143.cs'
if '--check' in sys.argv:assert p.read_text(encoding='utf-8').replace('\r\n','\n')==s.replace('\r\n','\n')
else:p.write_text(s,encoding='utf-8',newline='\n')
print('143 collector: unchanged physics/guards, isolated beta detector option and final observation callback verified')
