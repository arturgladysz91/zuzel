"""Checked extension of frozen contacts/predictor: only the crank/clutch torque hook and its state commit.
No legacy file is overwritten. No second tyre law, body integrator or native world is introduced.
"""
from pathlib import Path
import sys
root=Path(__file__).resolve().parents[2]
def replace(s,a,b):
    assert s.count(a)==1,(a,s.count(a))
    return s.replace(a,b)
old=(root/'tools/shared-world-speedway/TwoContactBikeDynamics.cs').read_text(encoding='utf-8')
s='using System.Numerics;\nusing CoreSim;\nusing CoreSim.PhysicalSpace;\nnamespace SharedWorldSpeedway;\n'+old[old.index('public static class TwoContactBikeDynamics'):]
s=replace(s,'public static class TwoContactBikeDynamics','public static class Contact144')
s=replace(s,'Action<TwoContactObservation>? observeContacts=null,float? inertia=null)',
    'Action<TwoContactObservation>? observeContacts=null,float? inertia=null,CrankClutch144? powertrain=null,double releaseAt=0)')
s=replace(s,'engineCommand=Engine500.Evaluate(wheel.AngularVelocityRadPerSecond,spec.Setup,throttle,p.Engine500);',
'''engineCommand=powertrain is null?Engine500.Evaluate(wheel.AngularVelocityRadPerSecond,spec.Setup,throttle,p.Engine500):powertrain.Prepare(throttle,
                Engine500Parameters.Default.ShortOverallRatio+(Engine500Parameters.Default.LongOverallRatio-Engine500Parameters.Default.ShortOverallRatio)*spec.Setup.Gearing,
                Engine500Parameters.Default.Efficiency,seconds,time>=releaseAt-1e-10,
                torque=>RearWheelDynamics.Evaluate(ur,wr,wheel.AngularVelocityRadPerSecond,budget,mass,SlipCoupledDynamics.VelocityScaleSquared(ur,budget/mass),
                    torque,Math.Abs(torque),torque,Vector2.Dot(resistance+frontForce,forward),seconds,wp,rearBudgetNewtons:rearBudget));''')
s=replace(s,'var contact=RearWheelDynamics.Evaluate(', 'var contact=powertrain?.Prepared.Wheel??RearWheelDynamics.Evaluate(')
# Learning always supplies a steering override; do not expose the legacy nested inverse without copying the crank.
s=replace(s,'if(!p.EnableTwoContactBikeDynamics', 'if(powertrain is not null&&steeringOverride is null)throw new ArgumentException("Launch contact requires an explicit causal steering request.");\n        if(!p.EnableTwoContactBikeDynamics')
contact=s
old=(root/'tools/shared-world-speedway/FreeRacingModel.cs').read_text(encoding='utf-8')
s='using System.Numerics;\nusing CoreSim;\nusing CoreSim.PhysicalSpace;\nnamespace SharedWorldSpeedway;\n'+old[old.index('public static class FreeRacingModel'):]
s=replace(s,'public static class FreeRacingModel','public static class Predictor144')
s=replace(s,'double reviewSeconds = .1)', 'double reviewSeconds = .1,CrankClutch144? powertrain=null,Action<double,double>? observeCrank=null)')
s=replace(s,'if (!p.EnableFreeRacing)', 'var shaft=powertrain?.Copy();\n        if(shaft is not null&&(closedLoop is not null||causalPolicy is not null))throw new ArgumentException("Launch prediction accepts a held candidate only.");\n        if (!p.EnableFreeRacing)')
s=replace(s,'return TwoContactBikeDynamics.Forces(', 'return Contact144.Forces(')
s=replace(s,'steeringOverride: active.SteeringRadians, inertia: inertia);', 'steeringOverride: active.SteeringRadians, inertia: inertia,powertrain:shaft,releaseAt:reaction);')
s=replace(s,'void Point() { var gap', 'void Point() { observeCrank?.Invoke(time,shaft?.Omega??0); var gap')
s=replace(s,'wheel.Commit(); steering.Commit();', 'wheel.Commit(); steering.Commit();shaft?.Commit();')
for name,content in [('Contact144.cs',contact),('Predictor144.cs',s)]:
    p=root/'tools/shared-world-speedway/StandingStart144'/name
    if '--check' in sys.argv:assert p.read_text(encoding='utf-8')==content,name
    else:p.write_text(content,encoding='utf-8',newline='\n')
print('144: frozen tyre/body equations retained; crank/clutch hook and copied predictive state verified')
