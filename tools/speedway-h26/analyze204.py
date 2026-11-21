"""Study #204 numerical and native evidence, with no simulator execution."""

import argparse
from collections import Counter
import math
from pathlib import Path
from statistics import median
import struct

from analyze203 import CURVES, native_summary, read, write
import matplotlib.pyplot as plt


def f32(value):
    return struct.unpack("f", struct.pack("f", value))[0]


def vector(value):
    return f32(value["X"]), f32(value["Y"])


def dot(a, b):
    return a[0] * b[0] + a[1] * b[1]


def cross(a, b):
    return a[0] * b[1] - a[1] * b[0]


def sub(a, b):
    return a[0] - b[0], a[1] - b[1]


def native_metrics(path, geometry, frequency):
    record = read(path)
    driver = record["Run"]["Riders"][0]
    samples = driver["Samples"]
    rows = []
    projections, clutch_residual, loads, profiles = [], [], [], []
    for i, (prev, now) in enumerate(zip(samples, samples[1:])):
        before, after = prev["Input"]["Rider"], now["Input"]["Rider"]
        f, p = now["Forces"], now["Powertrain"]
        w, z, load = p["Wheel"], f["TyreLoads"], record["Loads"][0][i]
        dt = p["Seconds"]
        evidence = load["Precision"]
        profiles.append(evidence)
        forward = math.cos(f32(before["Heading"])), math.sin(f32(before["Heading"]))
        front, rear, drag = map(vector, (f["FrontForce"], f["RearForce"], f["Resistance"]))
        v0, v1 = map(vector, (before["Velocity"], after["Velocity"]))
        position = vector(before["Position"])
        arms = tuple(sub(vector(f[key]), position) for key in ("FrontPoint", "RearPoint"))
        v_mid = tuple((a + b) / 2 for a, b in zip(v0, v1))
        yaw0, yaw1 = f32(before["AngularVelocity"]), f32(after["AngularVelocity"])
        yaw_mid = (yaw0 + yaw1) / 2
        net = tuple(a+b+c for a,b,c in zip(front,rear,drag))
        torque = cross(arms[0],front)+cross(arms[1],rear)
        d_body = 147/2*(dot(v1,v1)-dot(v0,v0))+f32(52.72097)/2*(yaw1**2-yaw0**2)
        d_crank = .005*(p["AfterOmega"]**2-p["BeforeOmega"]**2)
        d_wheel = .45*(w["AfterOmega"]**2-w["BeforeOmega"]**2)
        drive = dot(rear, forward)
        rear_lat = sub(rear, tuple(drive*x for x in forward))

        def work(force, arm=(0,0)):
            vx,vy = v_mid[0]-yaw_mid*arm[1],v_mid[1]+yaw_mid*arm[0]
            return dt*dot(force,(vx,vy))

        full_energy = (p["CombustionWorkJoules"]-p["CrankDragWorkJoules"]-d_body-d_crank-d_wheel
                       -p["ClutchHeatJoules"]-p["TransmissionLossJoules"]-w["BearingLossJoules"]
                       -w["LongitudinalLossJoules"]+work(front,arms[0])+work(rear_lat,arms[1])+work(drag))
        rows.append(dict(time=now["Input"]["Time"],s=after["TotalReferenceMeters"],dt=dt,
                         speed=after["Speed"]*3.6,beta=math.degrees(after["SlideRadians"]),yaw=yaw1,
                         gas=now["Input"]["Active"]["Drive"],rpm=p["Engine"]["EngineRpm"],
                         spin=100*w["InstantSlipRatio"],front=z["FrontNormalNewtons"],rear=z["RearNormalNewtons"],
                         sweep=now["Certificate"],ax=dot(net,forward)/147,fx=drive,fy=cross(forward,rear),
                         posture=100*record["Posture"][i]["Posture"]["RiderHeightOffsetAfterMeters"],
                         height=record["Posture"][i]["Posture"]["CombinedMidpointHeightMeters"],
                         impulse=math.hypot(*(147*(v-u)-dt*x for u,v,x in zip(v0,v1,net))),
                         yawImpulse=f32(52.72097)*(yaw1-yaw0)-dt*torque,energy=full_energy,
                         crankResidual=p["EnergyResidualJoules"],rearUtilization=w["RearUtilization"],
                         positiveNormalResidual=z["FrontNormalNewtons"]+z["RearNormalNewtons"]-147*9.81))
        projections.append(abs(drive-w["AppliedLongitudinalNewtons"]))
        clutch_residual.append(abs(p["RootResidualNewtonMeters"]))
        loads.append(load)
    if not rows:
        return {"result":record["Result"],"finish":None}, samples
    metrics=native_summary(record,driver,rows,geometry)
    metrics.update(
        maxAbsBetaDegrees=max(abs(x["beta"]) for x in rows),
        maxAbsYaw=max(abs(x["yaw"]) for x in rows),
        wheelspinMin=min(x["spin"] for x in rows),wheelspinMax=max(x["spin"] for x in rows),
        rearFx=[min(x["fx"] for x in rows),max(x["fx"] for x in rows)],
        rearFy=[min(x["fy"] for x in rows),max(x["fy"] for x in rows)],
        maxInternalResidual=max(abs(x["Residual"]) for x in loads),
        maxAppliedResidual=max(abs(x["AppliedResidual"]) for x in profiles),
        maxForceRounding=max(x["ForceRoundingNewtons"] for x in profiles),
        maxMomentRounding=max(abs(x["MomentRoundingNewtonMeters"]) for x in profiles),
        maxWheelImpulseResidual=max(abs(x["WheelImpulseResidual"]) for x in profiles),
        maxRearForceProjectionMismatch=max(projections),
        maxAppliedClutchResidual=max(clutch_residual),
        maxPreRoundingClutchResidual=max(abs(x["ClutchResidualBeforeRounding"]) for x in profiles),
        solveQueries=sum(x["Iterations"] for x in loads),
        nativeResidualForceWallMs=sum(x["ForceTicks"] for x in profiles)*1000/frequency,
        nativeLoadSolveWallMs=sum(x["SolverTicks"] for x in profiles)*1000/frequency,
    )
    if max(projections)>1e-9 or metrics["maxAppliedResidual"]>2e-5 or metrics["maxInternalResidual"]>2e-5:
        raise ValueError(f"Applied precision/ledger mismatch in {path}")
    return metrics,samples


def divergence(reference,current):
    prior={round(x["Input"]["Time"],8):x for x in reference}
    first={}
    max_position=0.0
    for after in current:
        time=after["Input"]["Time"]
        before=prior.get(round(time,8))
        if before is None:
            continue
        a,b=before["Input"],after["Input"]
        position=math.hypot(*sub(vector(b["Rider"]["Position"]),vector(a["Rider"]["Position"])))
        max_position=max(position,max_position)
        tests={"transform-float":position>0,"position-1cm":position>.01,"position-10cm":position>.1,
               "active-command":a["Active"]!=b["Active"]}
        if before.get("Forces") and after.get("Forces"):
            tests["force-0.001N"]=max(math.hypot(*sub(vector(before["Forces"][key]),vector(after["Forces"][key])))
                                      for key in ("FrontForce","RearForce","Resistance"))>.001
        for name,happened in tests.items():
            if happened and name not in first:
                first[name]=dict(time=time,positionDeltaMeters=position,reference=a["Rider"]["TotalReferenceMeters"],
                                 originalCommand=a["Active"],newCommand=b["Active"])
    return {"first":first,"maxMatchedPositionDeltaMeters":max_position}


def reason(outcome):
    if outcome is None:
        return "admissible"
    if "NoPhysicallyAdmissibleRoot" in outcome:
        return "certified-load-domain"
    if "no positive-load" in outcome:
        return "legacy-endpoint-rejection"
    if "IterationLimit" in outcome or "convergence" in outcome:
        return "iteration-limit"
    if "AmbiguousOrMultipleRoots" in outcome:
        return "ambiguous"
    if "InvalidPhysicalState" in outcome:
        return "invalid-physical-state"
    return outcome.split(":")[0]


def throttle_summary(path):
    data=read(path)
    groups={}
    for trial in data["Trials"]:
        key=(trial["Archive"],trial["Physics"],trial["Horizon"])
        groups.setdefault(key,[]).append(trial)
    out=[]
    for (archive,physics,horizon),all_trials in groups.items():
        trials=[x for x in all_trials if x["Baseline"]["Outcome"] is None]
        followup=[x for x in trials if x["BaselineContinuation"] is not None and
                  x["BaselineContinuation"]["Outcome"] is None]
        legal=[x for x in followup if x["Continuation"]["Outcome"] is None]
        gains=[x["TimeGain"] for x in legal if x["TimeGain"] is not None]
        profile=[x["Full"]["Profile"] for x in trials]
        out.append(dict(archive=archive,physics=physics,horizon=horizon,eligible=len(all_trials),causallyComparable=len(trials),
                        baselineRejected=dict(Counter(reason(x["Baseline"]["Outcome"]) for x in all_trials
                                                      if x["Baseline"]["Outcome"] is not None)),
                        local=dict(Counter(reason(x["Full"]["Outcome"]) for x in trials)),
                        localHigherSpeed=sum(x["Full"]["Outcome"] is None and
                            x["Full"]["End"]["Speed"]-x["Baseline"]["End"]["Speed"]>.01 for x in trials),
                        followupWithValidBaseline=len(followup),legalContinuation=len(legal),
                        sameGoalReached=len(gains),faster=sum(x>1e-3 for x in gains),
                        medianGainSeconds=median(gains) if gains else None,
                        meanForceUsPerForecastStep=sum(x["ForceTicks"] for x in profile)*1e6/data["Frequency"]/
                        max(1,sum(x["Steps"] for x in profile)),
                        meanSolverUsPerForecastStep=sum(x.get("LoadSolverTicks",0) for x in profile)*1e6/data["Frequency"]/
                        max(1,sum(x["Steps"] for x in profile)) if any("LoadSolverTicks" in x for x in profile) else None))
    return dict(histograms=out,phases=data["Phases"],elapsedSeconds=data["ElapsedSeconds"])


def chart(model, output):
    data=read(model/"p0-residual.json")
    curve=data["Curve"]
    center=data["Points"][0]["Ax"]
    fig, axes=plt.subplots(1,2,figsize=(10.4,3.8),layout="constrained")
    for axis,title,field,color in zip(axes,("H27-P · poprzednia funkcja","H27-N · funkcja w double"),
                                     ("Legacy","Double"),("#b04b31","#176ca0")):
        if field=="Legacy":
            before=[x for x in curve if x["Ax"]<=center]
            after=[{"Ax":data["Points"][1]["Ax"],"Legacy":data["Points"][1]["LegacyResidual"]}]
            after.extend(x for x in curve if x["Ax"]>center)
            segments=(before,after)
        else:
            segments=(curve,)
        for segment in segments:
            axis.plot([(x["Ax"]-center)*1e6 for x in segment],[x[field]*1e6 for x in segment],color=color)
        axis.axhspan(-20,20,color="#b7cdb3",alpha=.38,label="Tolerancja ±20 µm/s²")
        axis.axhline(0,linewidth=.7,color="#222222")
        axis.set(xlabel="Odchylenie próbnego ax od 0,71413508 [µm/s²]",title=title)
        axis.set_xlim(-12,12)
        axis.set_ylim(-270,510)
        axis.grid(alpha=.2)
    points=data["Points"]
    axes[0].scatter([0,0],[p["LegacyResidual"]*1e6 for p in points],color="#202020",s=16,zorder=4)
    axes[0].set_ylabel("Reszta ax − Fx/m [µm/s²]")
    axes[1].legend(loc="upper left",fontsize=8)
    fig.savefig(output/"reszta-p0.png",dpi=170)
    plt.close(fig)


def main():
    args=argparse.ArgumentParser(description=__doc__)
    for arg in ("model","native","throttle","output"):
        args.add_argument(arg,type=Path)
    paths=args.parse_args()
    paths.output.mkdir(parents=True,exist_ok=True)
    geometry=read(CURVES)["geometry"]
    frequency=read(paths.model/"archive-model.json")["Frequency"]
    old_root=Path(__file__).resolve().parents[2]/"data/experiments/speedway203/native"
    native={}
    for path in sorted(paths.native.glob("H27-N-*.json.gz")):
        key=path.name.removesuffix(".json.gz")
        native[key],samples=native_metrics(path,geometry,frequency)
        previous=old_root/path.name.replace("H27-N","H27-P")
        if previous.is_file() and "-C-alt" not in key:
            reference=read(previous)["Run"]["Riders"][0]["Samples"]
            native[key]["matchedDivergence"]=divergence(reference,samples)
    write(paths.output/"native-summary.json",native)
    write(paths.output/"throttle-summary.json",throttle_summary(paths.throttle))
    chart(paths.model,paths.output)


if __name__=="__main__":
    main()
