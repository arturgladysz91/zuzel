"""Build the H28-C report figures and compact evidence without opening Box2D."""

import argparse
from collections import Counter, defaultdict
from pathlib import Path
from statistics import median

from analyze203 import CURVES, read, write
from analyze204 import native_metrics, reason, throttle_summary
import matplotlib.pyplot as plt


LAW = ("H27-N", "H28-C")
COLORS = {"H27-N":"#b04b31", "H28-C":"#176ca0"}


def number_map(rows):
    fields = ["Fx", "Fy", "RearMoment", "NetMoment", "YawDeficit", "aT", "aN", "BetaDot", "YawDot"]
    differences = defaultdict(list)
    for row in rows:
        a,b=(x["State"] for x in row["Results"])
        if a and b:
            for name in fields:
                differences[name].append(b[name]-a[name])
    return {name:dict(minimum=min(x),median=median(x),maximum=max(x),
                      positive=sum(y>1e-5 for y in x),negative=sum(y< -1e-5 for y in x))
            for name,x in differences.items()}


def load_telemetry(path):
    a=read(path)
    driver=a["Run"]["Riders"][0]
    rows=[]
    for sample in driver["Samples"][1:]:
        state=sample["Input"]["Rider"]
        force=sample["Forces"]
        power=sample["Powertrain"]
        rows.append(dict(s=state["TotalReferenceMeters"],speed=state["Speed"]*3.6,
                         gas=sample["Input"]["Active"]["Drive"],rpm=power["Engine"]["EngineRpm"],
                         kappa=power["Wheel"]["InstantSlipRatio"],beta=state["SlideRadians"],
                         yaw=state["AngularVelocity"],fx=force["RearLongitudinal"],fy=force["RearLateral"],
                         fzf=force["TyreLoads"]["FrontNormalNewtons"],steer=sample["Input"]["Steering"]))
    return rows


def paired_throttle(data):
    grouped=defaultdict(dict)
    for trial in data["Trials"]:
        grouped[trial["Archive"],trial["Horizon"],trial["Time"]][trial["Physics"]]=trial
    horizons=defaultdict(list)
    for (archive,horizon,_),pair in grouped.items():
        if all(variant in pair for variant in LAW):
            horizons[archive,horizon].append(pair)
    rows=[]
    for (archive,horizon),pairs in sorted(horizons.items()):
        shared=[pair for pair in pairs if all(pair[variant]["Baseline"]["Outcome"] is None for variant in LAW)]
        for variant in LAW:
            trials=[pair[variant] for pair in shared]
            legal=[trial for trial in trials if trial["Continuation"] and trial["Continuation"]["Outcome"] is None
                   and trial["BaselineContinuation"]["Outcome"] is None]
            failures=[trial["Full"]["ContactDiagnostics"] for trial in trials
                      if trial["Full"]["Outcome"]=="swept" and trial["Full"]["ContactDiagnostics"]]
            rows.append(dict(archive=archive,horizon=horizon,variant=variant,matched=len(pairs),
                             sharedBaseline=len(shared),local=dict(Counter(reason(t["Full"]["Outcome"]) for t in trials)),
                             legalContinuation=len(legal),faster=sum((t["TimeGain"] or 0)>.001 for t in legal),
                             sweptWithLastContact=len(failures),
                             sweptWithNecessaryYawDeficit=sum(d["NecessaryFrontYawDeficit"]>0 for d in failures),
                             sweptWithRoadLateralDeficit=sum(d["ActualLateralForce"]<d["ApproximateRoadLateralDemand"]
                                                            for d in failures),
                             sweptRateLimited=sum(d["RateLimited"] for d in failures)))
    return rows


def coupled_native(path, metrics, geometry, frequency):
    archive=read(path)
    rows=archive["Run"]["Riders"][0]["Samples"][1:]
    evidence=[item["Precision"]["Combined"] for item in archive["Loads"][0]]
    speeds=[row["Powertrain"]["Engine"]["EngineRpm"] for row in rows]
    laps=geometry["lapMeters"]
    exit_points=[seg["startReferenceMeters"] for seg in geometry["segments"]
                 if seg["type"]=="Straight" and seg["startReferenceMeters"]>0]
    terminal_start=2*laps+max(exit_points)
    terminal=[row["Input"]["Rider"]["Speed"]*3.6
              for row in rows if terminal_start<=row["Input"]["Rider"]["TotalReferenceMeters"]<=3*laps]
    metrics.update(rpmRange=[min(speeds),max(speeds)],terminalStraightMax=max(terminal,default=None),
                   maxFxLawRounding=max(abs(x["AppliedTyreFxResidual"]) for x in evidence),
                   maxFyLawRounding=max(abs(x["AppliedTyreFyResidual"]) for x in evidence),
                   minRearLateralLossJ=min(x["LateralDissipationJoules"] for x in evidence),
                   totalRearLateralLossJ=sum(x["LateralDissipationJoules"] for x in evidence),
                   proofBoxes=sum(x["ProofBoxes"] for x in evidence),
                   innerWheelQueries=sum(x["WheelQueries"] for x in evidence),
                   innerWheelIterations=sum(x["WheelIterations"] for x in evidence),
                   nativePowerWallMs=1000*sum(x["PowerTicks"] for x in evidence)/frequency,
                   nativeProofWallMs=1000*sum(x["ProofTicks"] for x in evidence)/frequency)


def figures(model, transient, equilibria, output, native_paths):
    fig, axes = plt.subplots(2,2,figsize=(10.6,6.0),layout="constrained")
    fixed=[x for x in model if x["SpeedKph"]==80 and x["Beta"]>0 and x["Yaw"]>0
           and x["FrontNormal"]==200 and abs(x["Dt"]-.01)<1e-6]
    for ax, field, label in zip(axes.flat,("Fy","NetMoment","aT","aN"),
                                ("Δ Fyr [N]","Δ Mz netto [N·m]","Δ a wzdłuż prędkości [m/s²]","Δ a poprzeczne [m/s²]")):
        for kappa,color in zip((-.5,0,.5),("#783894","#777777","#177944")):
            group=sorted((x for x in fixed if x["Kappa"]==kappa),key=lambda x:x["Gas"])
            ax.plot([100*x["Gas"] for x in group],
                    [x["Results"][1]["State"][field]-x["Results"][0]["State"][field] for x in group],
                    color=color,marker="o",label=f"κ = {kappa:g}")
        ax.axhline(0,color="#555555",linewidth=.7)
        ax.set(xlabel="Gaz [%]",ylabel=label)
        ax.grid(alpha=.2)
    axes[0,0].legend(fontsize=8)
    fig.suptitle("H28-C minus H27-N · 80 km/h, β = 0,18 rad, yaw = 0,7 rad/s, Fzf = 200 N")
    fig.savefig(output/"mapa-sil-i-yaw.png",dpi=165)
    plt.close(fig)

    fig, axes=plt.subplots(3,2,figsize=(11.0,7.7),sharex="col",layout="constrained")
    for column,section in enumerate(("bend-1-exit","bend-5-exit")):
        group=[x for x in transient["Rollouts"] if x["Name"]==section]
        for i,(field,label) in enumerate((("Fy","Fyr [N]"),("Mz","Mz netto [N·m]"),("At","a styczne [m/s²]"))):
            ax=axes[i,column]
            for x in group:
                available=[q for q in x["Samples"] if q["Forces"] and
                           q["End"]["Time"]>=x["Snapshot"]["State"]["Time"]+q["Horizon"]-1e-7]
                ax.plot([s["Horizon"] for s in available],[s["Forces"][field] for s in available],
                        color=COLORS[x["Variant"]],linestyle="-" if x["GasRequest"] else "--",marker="o",
                        label=f"{x['Variant']} · {x['GasRequest']*100:.0f}%")
            if group:
                x=group[0]
                ax.axvline(x["FirstRequestedActivation"]-x["Snapshot"]["State"]["Time"],
                           color="#797979",linestyle=":",linewidth=.9)
            ax.set_ylabel(label)
            ax.grid(alpha=.2)
        axes[0,column].set_title("Wyjście z łuku "+("1" if column==0 else "5"))
        axes[2,column].set_xlabel("Czas od checkpointu [s]")
    if axes[0,0].lines:
        axes[0,0].legend(fontsize=8)
    fig.suptitle("Skok komendy gazu z zachowanym opóźnieniem aktywacji")
    fig.savefig(output/"odpowiedz-gaz.png",dpi=165)
    plt.close(fig)

    fig,axes=plt.subplots(1,2,figsize=(10.5,4.3),layout="constrained")
    for ax,field,label in zip(axes,("Tangential","NetYaw"),("a styczne [m/s²]","Mz netto [N·m]")):
        for variant,marker in zip(LAW,("o","x")):
            candidates=[x for x in equilibria if x["Variant"]==variant and x["Status"]=="QuasiSteadyCandidate"]
            ax.scatter([x["Normal"] for x in candidates],[x[field] for x in candidates],s=23,marker=marker,
                       color=COLORS[variant],alpha=.77,label=variant)
        ax.set(xlabel="a poprzeczne [m/s²]",ylabel=label)
        ax.grid(alpha=.2)
    axes[0].legend()
    fig.suptitle("Kandydaci quasi-ustalonego skrętu przy promieniach 32, 38 i 44 m")
    fig.savefig(output/"rownowagi.png",dpi=165)
    plt.close(fig)

    if len(native_paths)<2:
        return
    fig,axes=plt.subplots(5,2,figsize=(11.8,11.0),sharex=True,layout="constrained")
    for path,variant in zip(native_paths,LAW):
        rows=load_telemetry(path)
        for ax,(field,label) in zip(axes.flat,(("speed","Prędkość [km/h]"),("gas","Aktywny gaz"),
                   ("rpm","Wał [RPM]"),("kappa","κ"),("beta","β [rad]"),("yaw","Yaw [rad/s]"),
                   ("fx","Fx tyłu [N]"),("fy","Fy tyłu [N]"),("fzf","Fzf [N]"),("steer","Skręt [rad]"))):
            ax.plot([x["s"] for x in rows],[x[field] for x in rows],linewidth=1,color=COLORS[variant],label=variant)
            ax.set_ylabel(label)
            ax.grid(alpha=.2)
    axes[0,0].legend()
    for ax in axes[-1]:
        ax.set_xlabel("Pozycja na linii referencyjnej [m]")
    fig.suptitle("Nominalne przejazdy · zapisane kroki Box2D · osobne polityki")
    fig.savefig(output/"telemetria.png",dpi=165)
    plt.close(fig)


def main():
    cli=argparse.ArgumentParser(description=__doc__)
    for name in ("model","transients","equilibria","throttle","native","output"):
        cli.add_argument(name,type=Path)
    args=cli.parse_args()
    args.output.mkdir(parents=True,exist_ok=True)
    root=Path(__file__).resolve().parents[2]
    reference=root/"data/experiments/speedway204/native/H27-N-learned-C.json.gz"
    base=read(root/"data/experiments/speedway204/native-summary.json")["H27-N-learned-C"]
    native={"H27-N-archived-nominal":base}
    geom=read(CURVES)["geometry"]
    throttle=read(args.throttle)
    frequency=throttle["Frequency"]
    actual=sorted(args.native.rglob("H28-C-*.json.gz")) if args.native.exists() else []
    chart_nat=[reference]
    for path in actual:
        result,_=native_metrics(path,geom,frequency)
        coupled_native(path,result,geom,frequency)
        name=path.name.removesuffix(".json.gz")
        if name in native:
            name=f"{path.parent.name}/{name}"
        native[name]=result
        if name=="H28-C-learned-C" and result.get("finish"):
            chart_nat.append(path)
    write(args.output/"native-summary.json",native)
    grid=read(args.model/"synthetic-grid-model.json.gz")
    transients=read(args.transients)
    equilibria=read(args.equilibria)["Cases"]
    success=defaultdict(list)
    for x in equilibria:
        if x["Status"]=="QuasiSteadyCandidate":
            success[x["Variant"],x["Radius"],x["SpeedKph"]].append(x)
    best=[dict(variant=key[0],radius=key[1],speedKph=key[2],count=len(value),
               best=max(value,key=lambda r:r["Tangential"])) for key,value in success.items()]
    write(args.output/"diagnostic-summary.json",dict(
        gridDifferences=number_map(grid),
        gridStatuses=dict(Counter(y["Variant"]+" "+y["Status"] for x in grid for y in x["Results"])),
        equilibriaBest=best,
        equilibriaStatuses=dict(Counter(x["Variant"]+" "+x["Status"] for x in equilibria)),
        transientsLastOutcomes=dict(Counter(x["Variant"]+" "+str(x["Samples"][-1]["Outcome"]) for x in transients["Rollouts"]))))
    summary=throttle_summary(args.throttle)
    summary["paired"]=paired_throttle(throttle)
    write(args.output/"throttle-summary.json",summary)
    figures(grid,transients,equilibria,args.output,chart_nat)


if __name__=="__main__":
    main()
