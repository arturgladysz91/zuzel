"""Observation/stop plumbing only; frozen original WorldRunner and guards untouched."""
from pathlib import Path
import sys
root=Path(__file__).resolve().parents[2]
s=(root/'tools/shared-world-speedway/AutonomousAcceleration141/NativeCapture141.cs').read_text(encoding='utf-8')
s=s.replace('NativeCapture141','NativeCapture142').replace('NativeCaptureResult141','NativeCaptureResult142')
def sub(a,b):
    global s
    assert s.count(a)==1,(a,s.count(a))
    s=s.replace(a,b)
sub('int studiedCorner=1)', 'int studiedCorner=1,Func<string?>? controllerHalt=null,double cpuCeilingSeconds=440)')
sub('if(snapshot.TimeSeconds%1<.011&&(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds>40)failure="episode CPU limit";',
    'if((Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds>cpuCeilingSeconds)failure="total experiment CPU limit";')
sub('diagnosticStop:s=>failure is not null||(', 'diagnosticStop:s=>failure is not null||controllerHalt?.Invoke() is not null||(')
sub('var completed=failure is null&&historicalCompleted', 'failure??=controllerHalt?.Invoke();var completed=failure is null&&historicalCompleted')
p=root/'tools/shared-world-speedway/RacingObjective142/NativeCapture142.cs'
p.parent.mkdir(exist_ok=True)
if '--check' in sys.argv:assert p.read_text(encoding='utf-8').replace('\r\n','\n')==s.replace('\r\n','\n')
else:p.write_text(s,encoding='utf-8',newline='\n')
print('142 collector observation-only substitutions verified')
