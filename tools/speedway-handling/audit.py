"""Independently audit #181 samples and compare their exact archived starts."""
import gzip
import io
import json
from pathlib import Path
from statistics import median
import sys
from zipfile import ZipFile

import numpy as np

TOOL = Path(__file__).resolve().parent
REPO = TOOL.parents[1]
sys.path.insert(0,str(REPO/"tools/trajectory-control"))
from verify177 import committed, dot, kinetic, midpoint, queue_audit, regression
sys.path.insert(0,str(REPO/"tools/track-grip-root-cause"))
from analyze175 import native_trace, window

DOCS = REPO/"docs/experiments/trajectory-control"
LAP = 318.77874994277954
FIRST_ENTRY = 35.
BEND = (LAP-124)/2
FIRST_EXIT = FIRST_ENTRY+BEND
SECOND_ENTRY = FIRST_EXIT+62
SECOND_EXIT = SECOND_ENTRY+BEND


def inspect(path):
    recorded = json.loads(gzip.decompress(path.read_bytes()))
    run = recorded["Run"]
    rider = run["Riders"][0]
    rows = rider["Samples"]
    leans = recorded["Lean"]
    settings = recorded["Settings"]
    queue_audit(run,recorded["Decisions"])
    regression(rows)
    assert len(rows)==run["NativeSteps"]+1==len(leans)
    assert len(run["Contacts"])==0
    assert rows[0]["Input"]["Rider"]["Speed"]==0
    closure = 0.
    max_impulse = 0.
    for a,b,prior,accepted in zip(rows,rows[1:],leans,leans[1:]):
        committed(a,b)
        p,f = b["Powertrain"],b["Forces"]
        w = p["Wheel"]
        h = p["Seconds"]
        assert 0<h<=.010000001 and abs(h-(b["Input"]["Time"]-a["Input"]["Time"]))<1e-9
        assert abs(accepted["Seconds"]-b["Input"]["Time"])<1e-9
        assert abs(accepted["ActualRadians"]-prior["ActualRadians"])<=settings["LeanRateRadiansPerSecond"]*h+1e-9
        assert abs(accepted["RequestRadians"])<=settings["MaximumLeanRadians"]+1e-9
        assert abs(accepted["ActualRadians"])<=settings["MaximumLeanRadians"]+1e-9
        assert abs(f["SteeringAfter"]-f["SteeringBefore"]) <= 1.5707964*h+1e-7
        loads = f["TyreLoads"]
        assert f.get("AxleLoad") is None
        assert abs(loads["EffectiveFrictionCoefficient"]-1.2845000267028808*settings["GripScale"])<1e-9
        assert abs(loads["FrontNormalNewtons"]-665.5707895749277)<1e-8
        assert abs(loads["RearNormalNewtons"]-776.4992104250724)<1e-8
        assert np.hypot(f["RearLongitudinal"],f["RearLateral"])<=loads["RearCapacityNewtons"]+.001
        assert abs(f["FrontLateral"])<=loads["FrontCapacityNewtons"]+.001
        assert dot(f["FrontForce"],f["FrontVelocity"])<=.001
        assert f["RearLateral"]*dot(f["BodyLeft"],f["RearVelocity"])<=.001
        assert abs(p["EnergyResidualJoules"])<1e-6 and w["LongitudinalLossJoules"]>=-1e-6 and p["ClutchHeatJoules"]>=-1e-6
        va,vb=a["Input"]["Rider"],b["Input"]["Rider"]
        impulse=max(abs(vb["Velocity"][k]-va["Velocity"][k]-f["NetForce"][k]*h/147) for k in ("X","Y"))
        yaw=abs(vb["AngularVelocity"]-va["AngularVelocity"]-f["NetTorque"]*h/52.72097)
        assert impulse<4e-5 and yaw<4e-5
        max_impulse=max(max_impulse,impulse)
        heat=p["ClutchHeatJoules"]+p["TransmissionLossJoules"]+p["CrankDragWorkJoules"]
        heat+=w["LongitudinalLossJoules"]+w["BearingLossJoules"]
        heat-=h*(dot(f["FrontForce"],midpoint(va,vb,f["FrontArm"]))+
                 f["RearLateral"]*dot(f["BodyLeft"],midpoint(va,vb,f["RearArm"]))+
                 dot(f["Resistance"],midpoint(va,vb,dict(X=0,Y=0))))
        excess=kinetic(b)-kinetic(a)-p["CombustionWorkJoules"]
        assert abs(excess-b["EnergyExcessJoules"])<.025
        assert excess<.11
        closure-=excess+heat
    assert abs(closure)<12,closure
    outcome=recorded["Outcome"]
    if outcome["Legal"]:
        assert min(min(q["Inner"],q["Outer"]) for q in rows)>=.5
        assert min(q["Certificate"] for q in rows[1:])>=.5
        assert max(abs(q["Input"]["Rider"]["SlideRadians"]) for q in rows)<=np.pi/4
        assert rows[-1]["Input"]["Rider"]["TotalReferenceMeters"]>=recorded["Goal"]
        if not outcome["StageA"]:
            assert run["EndSeconds"]-run["Crossings"][-1]["Time"]>=1
            assert [q["Lap"] for q in run["Crossings"]]==[1,2,3]
            for q in run["Crossings"]:
                assert q["BeforeReference"]<q["Lap"]*LAP<=q["AfterReference"]
    measured = metrics(run,recorded["Goal"])
    return dict(outcome=outcome,start=recorded["test"],settings=settings,tuning=recorded["Tuning"],
                max_lean_deg=float(np.degrees(max(abs(x["ActualRadians"]) for x in leans))),
                intervals=len(rows)-1,energy_closure_J=closure,max_impulse_error_mps=max_impulse,**measured)


def metrics(run,goal):
    trace=native_trace(run)
    s,t,v=trace["s"],trace["t"],trace["speed"]
    reached=s[-1]>=goal
    end=float(np.interp(goal,s,t)) if reached else float(t[-1])
    during=(t<=end)
    peak=lambda mask:float(v[mask].max()*3.6) if np.any(mask) else None
    raw=run["Riders"][0]["Samples"]
    is_straight=np.array([x["Input"]["Rider"]["Road"]["Segment"] in (0,4,8) for x in raw])
    time_dt=np.clip(np.minimum(t[1:],end)-t[:-1],0,None)
    stats=window(trace,0,end)["energy_J"]
    bend_rows=[]
    for lap in range(3):
        for i,(a,b) in enumerate(((FIRST_ENTRY,FIRST_EXIT),(SECOND_ENTRY,SECOND_EXIT)),1):
            a,b=a+lap*LAP,b+lap*LAP
            if b>s[-1] or b>goal:
                continue
            ta,tb=np.interp([a,b],s,t)
            dt=np.clip(np.minimum(t[1:],tb)-np.maximum(t[:-1],ta),0,None)
            mask=(s>=a)&(s<=b)
            driven=np.sum(dt[(abs(trace["beta"][1:])>=5)&(trace["gas"][1:]>=.25)&(trace["kappa"]>.05)])
            bend_rows.append(dict(lap=lap+1,bend=i,entry_kph=float(np.interp(a,s,v)*3.6),
                min_kph=float(v[mask].min()*3.6),exit_kph=float(np.interp(b,s,v)*3.6),
                drive_mean=float(np.sum(dt*trace["gas"][1:])/(tb-ta)),
                high_drive_fraction=float(np.sum(dt[trace["gas"][1:]>=.70])/(tb-ta)),
                driven_slide_s=float(driven),seconds=float(tb-ta),path_m=float(np.diff(np.interp([a,b],s,trace["dist"]))[0])))
    return dict(goal_time_s=end if reached else None, actual_distance_m=float(np.interp(end,t,trace["dist"])),
        max_straight_kph=peak(during&is_straight),max_beta_deg=float(abs(trace["beta"][during]).max()),
        min_swept=float(np.nanmin(trace["swept"][during])),high_drive_fraction=float(time_dt[trace["gas"][1:]>=.70].sum()/end),
        full_drive_straight_s=float(time_dt[(trace["gas"][1:]>=.95)&is_straight[1:]].sum()),
        max_kappa_moving=float(abs(trace["kappa"][(time_dt>0)&(v[:-1]>10)]).max()),
        max_yaw_rad_s=float(abs(trace["yaw"][during]).max()),
        slip_kJ=(stats["wheel_slip"]+stats["front_lateral"]+stats["rear_lateral"])/1000,
        bend_rows=bend_rows,mean_bend_drive=median([x["drive_mean"] for x in bend_rows]) if bend_rows else None)


def archived(case,arm):
    with ZipFile(DOCS/"177-walidacja.zip") as z:
        with ZipFile(io.BytesIO(z.read(f"held-{case}-{arm}.zip"))) as member:
            run=json.loads(member.read("native.json"))
            trial=json.loads(member.read("trial.json"))
    outcome=trial["Outcome"]
    assert outcome["S3"] and outcome["Reserve"] and outcome["ReachedStop"]
    return dict(finish=outcome["FinishTime"],start=outcome["Start"],**metrics(run,3*LAP))


def main(folder):
    results={}
    for path in sorted(folder.glob("*.json.gz")):
        if path.name.endswith("-model.json.gz"):
            continue
        r=inspect(path)
        results[path.name[:-8]]=r
        if not r["outcome"]["StageA"]:
            start=r["start"]
            r["comparisons"]={}
            for arm in ("BASE","NEW"):
                reference=archived(start["Id"],arm)
                assert all(reference["start"][k]==v for k,v in start.items())
                r["comparisons"][arm]=reference
                if r["outcome"]["Legal"]:
                    reference["gain_pct"]=100*(reference["finish"]-r["outcome"]["FinishSeconds"])/reference["finish"]
        print(path.name,r["outcome"],"Vmax",r["max_straight_kph"],"lean",r["max_lean_deg"],"closure",r["energy_closure_J"])
    (folder/"summary.json").write_text(json.dumps(results,indent=2,allow_nan=False)+"\n",encoding="utf-8")


if __name__=="__main__":
    main(Path(sys.argv[1]))
