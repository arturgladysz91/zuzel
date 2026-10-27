"""Observation-only collector adaptation; original WorldRunner/physics remain unchanged."""
from pathlib import Path
import sys
ROOT=Path(__file__).resolve().parents[2]
S=ROOT/'tools/shared-world-speedway'
D=S/'AutonomousAcceleration141'
D.mkdir(exist_ok=True)
s=(S/'TyreSeparation140/NativeCapture140.cs').read_text(encoding='utf-8')
def sub(a,b,n=1):
    global s
    assert s.count(a)==n,(a,s.count(a),n)
    s=s.replace(a,b)
sub('NativeCapture140','NativeCapture141')
s=s.replace('NativeCaptureResult140','NativeCaptureResult141')
sub('bool extendAfterSecondStraight=false,bool separated=false)','bool extendAfterSecondStraight=false,int studiedCorner=1)')
sub('var surface=TrackState.CreateDefault',
    'var nextCornerEnd=studiedCorner==1?frame.SegmentStartMeters(8):frame.LapMeters+frame.SegmentStartMeters(4);\n        var stopReference=nextCornerEnd+62;\n        var surface=TrackState.CreateDefault')
sub('new TyreWorld140(','new WorldRunner(')
sub(',separated:separated);',');')
sub('RobustBudget.EpisodeCpuSeconds','40')
sub('s.Riders[0].TotalReferenceMeters>=frame.SegmentStartMeters(8)+62','s.Riders[0].TotalReferenceMeters>=stopReference')
sub('result.Riders[0].TotalReferenceMeters>=frame.SegmentStartMeters(8)+62','result.Riders[0].TotalReferenceMeters>=stopReference')
sub('result.Work.RearWheelBoundaryEvaluations);','result.Work.RearWheelBoundaryEvaluations,nextCornerEnd,stopReference);')
sub('long NativeContacts,long BoundaryContacts);','long NativeContacts,long BoundaryContacts,double NextCornerEndReference,double StopReference);')
# Retain the original stop/guard outcome and evidence even on an invariant failure.
# Only the post-run export exception changes; no guard or accepted step changes.
sub('if(failure?.Contains("invariant")==true)throw new InvalidOperationException("Physical invariant failed; no training continuation.");return new(', 'return new(')
p=D/'NativeCapture141.cs'
if '--check' in sys.argv:assert p.read_text(encoding='utf-8').replace('\r\n','\n')==s.replace('\r\n','\n')
else:p.write_text(s,encoding='utf-8',newline='\n')
print('141 observation substitutions verified' if '--check' in sys.argv else '141 observation collector generated')
