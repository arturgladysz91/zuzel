from pathlib import Path
import sys
r=Path('.');source=(r/'tools/trajectory-continuation/Planner161.Generated.cs').read_text(encoding='utf-8');s=source.replace('namespace Continuation161;','namespace Instrumentation163;\nusing Continuation161;').replace('Planner161','Planner163').replace('STUDY161','STUDY163')
s=s.replace('CpuProfile147 profile = new();','readonly ProfileMode163 profileMode;\n    CpuProfile163 profile = new();\n    public CpuProfile163 Measurements=>profile;')
s=s.replace('Settings161? settings=null)', 'Settings161? settings=null,ProfileMode163 profileMode=ProfileMode163.Aggregate)').replace('this.performance=performance??Program155.Options();','this.profileMode=profileMode;this.performance=performance??Program155.Options();')
s=s.replace('profile = new(); Runtime.Begin(input.Time);','profile = new(profileMode); Runtime.Begin(input.Time);')
s=s.replace('        var memo = Runtime.RolloutKey', '        using var measuredContinuation=profile.Continuation(phase);\n        var memo = Runtime.RolloutKey')
p=r/'tools/instrumentation-cost/Planner163.Generated.cs'
if '--check' in sys.argv:assert p.read_text(encoding='utf-8')==s,'163 planner/guard drift'
else:p.write_text(s,encoding='utf-8',newline='\n')
