"""Condense recorded evidence into the same results.json. No physics or forecasts."""
from pathlib import Path
import gzip
import hashlib
import json
import time

ROOT=Path(__file__).resolve().parents[2]
path=ROOT/'docs/experiments/tyre-law-regularization/results.json'
started=time.process_time()
r=json.loads(path.read_bytes())
assert all(x['Complete'] for x in r['Reservations'])
assert sum(x['NativeWorlds'] for x in r['Reservations'])==12

if r.get('PublicationSchemaVersion',0)>=2:
    print('Publication already complete; no repeated experiment or publication.')
    raise SystemExit(0)

old=ROOT/'docs/experiments/motorcycle-physics-validation'
maps=json.loads(gzip.decompress((old/'contact-maps.json.gz').read_bytes()))
comparisons=[]
for x in r['Mathematics']['Inertia']['Rows']:
    m=next(m for m in maps if m['State']=='D0-failed' and m['Kind']=='wheel-inertia-constitutive' and m['WheelInertia']==x['IwKgMetersSquared'])
    assert abs(x['OldFxNewtons']-m['RearFx'])<.001
    assert abs(x['OldFyNewtons']-m['RearFy'])<.001
    f=x['NewPure']
    comparisons.append({
        'IwKgMetersSquared':x['IwKgMetersSquared'],
        'OldFxNewtons':x['OldFxNewtons'],'OldFyNewtons':x['OldFyNewtons'],
        'NewFxNewtons':f['FxNewtons'],'NewFyNewtons':f['FyNewtons'],
        'OldRecordedYawMomentNewtonMeters':m['NetTorque'],
        'OldYawMomentAtNominalRearArmNewtonMeters':m['FrontTorque']-.6*x['OldFyNewtons'],
        'NewYawMomentAtNominalRearArmNewtonMeters':m['FrontTorque']-.6*f['FyNewtons'],
        'NominalGeometryRoundingDifferenceNewtonMeters':m['FrontTorque']-.6*x['OldFyNewtons']-m['NetTorque'],
        'OldLongitudinalDissipationWatts':m['RearLongLossWatts'],
        'NewLongitudinalDissipationWatts':f['LongitudinalLossWatts'],
        'OldLateralDissipationWatts':m['RearLatLossWatts'],
        'NewLateralDissipationWatts':f['LateralLossWatts'],
        'Meaning':'Identical instantaneous contact state, dt0. Both derived moments use b=.60m; front unchanged. Actual old float-world-point moment and its rounding difference are retained. Native rows use actual point forces and Box2D.'})
r['SameStateComparison']=comparisons

for x in r['CachedOld']:
    raw=old/'native'/(x['Name']+'.json.gz')
    x['ExistingCaptureSource']={'Path':str(raw.relative_to(ROOT)).replace('\\','/'),'Sha256':hashlib.sha256(raw.read_bytes()).hexdigest().upper(),'NewNativeWorlds':0}

# Two representative traces only. Keep the force path, spin, controls and errors;
# remove repeated derived vectors and unrelated planner telemetry.
for x in r['Native']:
    if x['Trace'] is None:
        continue
    for point in x['Trace']:
        if 'PreviousInterval' in point:
            continue
        f=point.pop('PreviousIntervalForces')
        w=point.pop('PreviousIntervalWheel')
        point['PreviousInterval']=None if f is None else {
            'FrontPointMeters':f['FrontPoint'],'RearPointMeters':f['RearPoint'],
            'FrontForceNewtons':f['FrontForce'],'RearForceNewtons':f['RearForce'],
            'FrontLateralNewtons':f['FrontLateral'],'RearFxNewtons':f['RearLongitudinal'],'RearFyNewtons':f['RearLateral'],
            'FrontTorqueNewtonMeters':f['FrontTorque'],'RearTorqueNewtonMeters':f['RearTorque'],'NetTorqueNewtonMeters':f['NetTorque'],
            'TyreLoads':f['TyreLoads'],'Rpm':f['Engine']['EngineRpm'],'DriveTorqueNewtonMeters':w['DriveTorqueNewtonMeters'],
            'SlipRatio':w['InstantSlipRatio'],'SolverResidualMetersPerSecond':w['RootResidualMetersPerSecond'],
            'SolverIterations':w['RootIterations'],'EngineWorkJoules':w['EngineWorkJoules'],
            'LongitudinalLossJoules':w['LongitudinalLossJoules']}

normal=[x for x in r['Native'] if x['Mode']!='off' and x['Case']['Pool']!='diagnostic-perturbation']
r['Summary']={
    'Verdict':'NUMERICAL IMPROVEMENT CONFIRMED + PHYSICS MODEL DEFICIENCY REMAINS',
    'Scope':'Constitutive independence and numerical checks confirmed; no claim of better empirical soil physics or a higher-order full planar integrator.',
    'OldLongCompleted':sum(x['Metrics']['Completed'] for x in r['CachedOld'] if x['Mode']=='long'),
    'OldShortCompleted':sum(x['Metrics']['Completed'] for x in r['CachedOld'] if x['Mode']=='short'),
    'NewLongCompleted':sum(x['Metrics']['Completed'] for x in normal if x['Mode']=='long'),
    'NewShortCompleted':sum(x['Metrics']['Completed'] for x in normal if x['Mode']=='short'),
    'NewPerturbedShortCompleted':sum(x['Metrics']['Completed'] for x in r['Native'] if x['Case']['Pool']=='diagnostic-perturbation'),
    'Dnf':[{'Name':x['Name'],'Model':x['Model'],'Cause':x['Metrics']['Outcome'],'LegalSeconds':x['Metrics']['LegalSeconds'],
            'MaximumBetaDegrees':x['Metrics']['MaximumBetaDegrees'],'Details':'LastTwo in the row with the same Name in CachedOld or Native'}
           for x in r['CachedOld']+r['Native'] if x['Metrics']['Outcome'] not in ['completed','probe']],
    'MaximumNativeEnergyLedgerResidualJoules':max(x['MaximumEnergyLedgerResidualJoules'] for x in r['Native']),
    'MaximumNativeEnergyLedgerToleranceRatio':max(x['MaximumEnergyLedgerToleranceRatio'] for x in r['Native']),
    'MaximumNativeYawUpdateErrorRadiansPerSecond':max(x['MaximumYawUpdateErrorRadiansPerSecond'] for x in r['Native']),
    'AllNativeNumericalGatesPassed':all(x['EnergyLedgerPassed'] and x['MaximumYawUpdateErrorRadiansPerSecond']<=2e-5 for x in r['Native']),
    'EmpiricalCalibration':'UNCALIBRATED. Reference-matched floors retain historical assumptions; measured speedway Fx/Fy(alpha,kappa,Fz), transient behaviour and loaded wheel properties are still required.'}
r['Cost']['ContactCountScope']='Native and prediction contacts only; standalone solver counts are in Mathematics.PhysicsGrid and per-run Convergence.ContactQueries.'
r['EvidenceInterpretation']={
    'FailedAnchor':'D0-failed was already beyond beta45; used only in local mathematical diagnostics, never as a claimed legal native reset.',
    'NativePerturbations':'Two new complete initial histories with +/-.25deg heading and +/-.01rad/s yaw. Finite successful continuations are not a Lyapunov or stable-high-slip proof.',
    'Front':'Original phenomenological slope floor remains; held-angle response is dt-independent. Inertia/dt are absent from its law, but full planar held-force coupling remains first order.',
    'OldComparison':'Eight completed historical experiments were read, not rerun. Their old CPU is reported in their original Metrics and excluded from the new budget.',
    'Ci':'Standard Windows workflow on final PR HEAD, status linked in PR metadata. No extra report files or source commit solely to store its own final SHA.'}
old_protocol=json.loads((old/'protocol.json').read_bytes())
r['Reproduction']={
    'PolicyId':16015,'Policy':old_protocol['Policy'],
    'PhysicsParameterFactory':'FreeLearningExperiment.Parameters() in frozen Sources',
    'MassKg':147,'BodyInertiaKgMetersSquared':52.72097,'WheelRadiusMeters':.33,'NativeWheelInertiaKgMetersSquared':.9,
    'BearingNewtonMeterSeconds':.02,'Engine':'Unchanged Engine500 curve, cutoff12000rpm, rigid ratio9, efficiency0.94',
    'UniformSurface':{'Grip':1,'Ruts':0,'Moisture':.5,'ReferenceMu':1.4,'EffectiveMu':1.2845000267028808},
    'NativeNominalStepSeconds':.01,'Box2DSubsteps':4,'Box2DPackage':'Box2D.NET 3.1.654',
    'ReactionSeconds':.24,'ReviewSeconds':.2,'SteeringLimitDegrees':35,'SteeringRateDegreesPerSecond':90,'ExperimentalBetaLimitDegrees':45,
    'NextStraightGateReferenceMeters':353.77874994277954,
    'HistoricalGate':'Original whole-body gate +1s retained, independently from full next62m',
    'SourceGeneration':'python tools/tyre-law-regularization/generate.py --check',
    'Stages':'freeze, math, preflight, diagnostic; commit confirmation lock; confirmation; read-only finalization',
    'FinalizerSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest().upper()}
r['Mathematics']['PhysicsGrid']['InputConfiguration']={
    'ContactForwardMetersPerSecond':[0,.001,10,30],'ContactSideMetersPerSecond':[0,1,10],
    'SpinFactors':[.2,1,20],'InitialOmegaFormula':'forward/0.33*spinFactor +10*spinFactor',
    'IwKgMetersSquared':[.45,.9,1.8],'StepsSeconds':[.02,.01,.005,.0025],'DriveTorqueNewtonMeters':[0,200],
    'NormalNewtons':[0,700,1000],'EffectiveMu':1.2845000267028808,'MassKg':147,
    'ExternalForceNewtons':-40,'ExternalForceMeaning':'Prescribed signed external load, explicitly accounted as external work; not claimed to be passive resistance at every sign of velocity.'}
r['Mathematics']['Front']['InputConfiguration']={'BetaDegrees':[-40,-10,0,10,40],'HeldSteeringDegrees':[-35,0,35],
    'StepsSeconds':[.02,.01,.005,.0025],'SpeedMetersPerSecond':20,'FiniteDtChecks':60,'AdditionalDt0Anchors':15}
r['PublicationCpuSeconds']=r.get('PublicationCpuSeconds',0)+time.process_time()-started
r['PublicationComplete']=True
r['PublicationSchemaVersion']=2
path.write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in r['Summary'].items() if k!='Dnf'},ensure_ascii=True))
print('Cost',r['Cost'],'bytes',path.stat().st_size)
