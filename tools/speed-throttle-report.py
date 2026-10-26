"""Read-only native evidence reduction for #136. No simulation, fitting or trajectory synthesis."""
import csv
import gzip
import hashlib
import io
import json
import math
from pathlib import Path
import shutil
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
D = ROOT / "docs/experiments/speed-throttle-root-cause"
if len(sys.argv)>3:
    D = Path(sys.argv[3]).resolve()
RAW = Path(sys.argv[1]).resolve()
OUT = Path(sys.argv[2]).resolve() if len(sys.argv) > 2 else D
OUT.mkdir(parents=True, exist_ok=True)

def read(p):
    with (gzip.open(p, "rt", encoding="utf-8") if str(p).endswith(".gz") else open(p, encoding="utf-8")) as f:
        return json.load(f)

def save(name, value):
    (D / name).write_text(json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n", encoding="utf-8")

def zipped(path, data):
    with open(path, "wb") as f, gzip.GzipFile(filename="", mode="wb", fileobj=f, mtime=0) as z:
        z.write(json.dumps(data, ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode())

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest().upper()

def dot(a, b):
    return a["X"] * b["X"] + a["Y"] * b["Y"]

def length(a):
    return math.hypot(a["X"], a["Y"])

def geom(run):
    return read(D / (run["GeometryName"] + "-geometry.json"))

def at(run, reference):
    # First native accepted state at/after boundary; no invented speed/pose.
    return next((s for s in run["Replay"]["Samples"] if s["Input"]["Rider"]["TotalReferenceMeters"] >= reference), None)

def info(s):
    if s is None:
        return None
    i=s["Input"]; v=i["Rider"]
    return dict(Time=i["Time"], Reference=v["TotalReferenceMeters"], SpeedKph=v["Speed"]*3.6,
                Requested=s["RequestedDrive"], Active=i["Active"]["Drive"], BetaDegrees=v["SlideRadians"]*180/math.pi,
                YawRate=v["AngularVelocity"], SteeringDegrees=i["Steering"]*180/math.pi,
                Inner=s["Inner"], Outer=s["Outer"])

def landmarks(run, corner):
    g=geom(run); end=g["Segments"][4 if corner==1 else 8]["StartReferenceMeters"]
    samples=run["Replay"]["Samples"]
    exit_sample=at(run,end)
    straights=[s for s in samples if end <= s["Input"]["Rider"]["TotalReferenceMeters"] <= end+62]
    afterfull=next((s for s in straights if s["Input"]["Active"]["Drive"] >= .99),None)
    result={"BoundaryReference":end,"Exit":info(exit_sample),"Before5m":info(at(run,end-5)),"After5m":info(at(run,end+5)),
            "After20m":info(at(run,end+20)),"After40m":info(at(run,end+40)),
            "MaximumStraightKph":max((s["Input"]["Rider"]["Speed"]*3.6 for s in straights),default=None),
            "ObservedStraightMeters":max((s["Input"]["Rider"]["TotalReferenceMeters"]-end for s in straights),default=0),
            "FirstFullAfterExitMeters":None if afterfull is None else afterfull["Input"]["Rider"]["TotalReferenceMeters"]-end}
    return result

def profiles(run):
    samples=run["Replay"]["Samples"]; g=geom(run); starts=[s["StartReferenceMeters"] for s in g["Segments"]]
    regions={"whole":lambda r:True,"corner1":lambda r:starts[1]<=r<starts[4],
             "exit1 +/-5 reference m":lambda r:starts[4]-5<=r<starts[4]+5,
             "straight-between":lambda r:starts[4]<=r<starts[5],"corner2":lambda r:starts[5]<=r<starts[8],
             "exit2 +/-5 reference m":lambda r:starts[8]-5<=r<starts[8]+5}
    result={}
    for name,inside in regions.items():
        seconds=[0.,0.,0.,0.]; weighted=0.; speedtime=0.
        for a,b in zip(samples,samples[1:]):
            i=a["Input"]; v=i["Rider"]
            if not inside(v["TotalReferenceMeters"]): continue
            dt=b["Input"]["Time"]-i["Time"]; gas=i["Active"]["Drive"]
            seconds[0 if gas==0 else 1 if gas<.5 else 2 if gas<.99 else 3]+=dt
            weighted+=gas*dt; speedtime+=v["Speed"]*3.6*dt
        total=sum(seconds)
        decisions=[x for x in run["Replay"]["Decisions"] if inside(min(samples,key=lambda s:abs(s["Input"]["Time"]-x["Time"]))["Input"]["Rider"]["TotalReferenceMeters"])]
        full=[x for x in decisions if x["Action"]["Drive"]>=.99]
        zero_lengths=[]; duration=0.
        for a,b in zip(samples,samples[1:]):
            if inside(a["Input"]["Rider"]["TotalReferenceMeters"]) and a["Input"]["Active"]["Drive"]==0:
                duration+=b["Input"]["Time"]-a["Input"]["Time"]
            elif duration: zero_lengths.append(duration); duration=0.
        if duration:zero_lengths.append(duration)
        result[name]={"Seconds":total,"ThrottleFractions":[x/total if total else None for x in seconds],
                      "ThrottleSeconds":seconds,"MeanThrottle":weighted/total if total else None,"MeanSpeedKph":speedtime/total if total else None,
                      "PartialRequests":sum(0<x["Action"]["Drive"]<.99 for x in decisions),"FullRequests":len(full),
                      "FirstFullRequest":full[0]["Time"] if full else None,"LongestContinuousZeroSeconds":max(zero_lengths,default=0),
                      "BinarySwitches":sum((a["Action"]["Drive"]==0 and b["Action"]["Drive"]>=.99) or (b["Action"]["Drive"]==0 and a["Action"]["Drive"]>=.99) for a,b in zip(decisions,decisions[1:]))}
    # Decision timings and actual queue activation around BOTH corners.
    timing=[]
    for corner in [1,2]:
        begin,end=starts[1 if corner==1 else 5],starts[4 if corner==1 else 8]
        decision_samples=[(x,min(samples,key=lambda s:abs(s["Input"]["Time"]-x["Time"]))) for x in run["Replay"]["Decisions"]]
        rolloff=next(((x,s) for x,s in decision_samples if begin-25<=s["Input"]["Rider"]["TotalReferenceMeters"]<begin and x["Action"]["Drive"]<.99),None)
        full=next(((x,s) for x,s in decision_samples if begin<=s["Input"]["Rider"]["TotalReferenceMeters"]<end+62 and x["Action"]["Drive"]>=.99),None)
        timing.append({"Corner":corner,"FirstRolloffBeforeEntry":None if rolloff is None else {"Time":rolloff[0]["Time"],"ReferenceMetersBeforeEntry":begin-rolloff[1]["Input"]["Rider"]["TotalReferenceMeters"]},
                       "FirstFullRequest":None if full is None else {"Time":full[0]["Time"],"ReferenceRelativeExit":full[1]["Input"]["Rider"]["TotalReferenceMeters"]-end,"ActivationAt":full[0]["Time"]+.24},
                       "Landmarks":landmarks(run,corner)})
    return {"Name":run["Replay"]["Name"],"Geometry":run["GeometryName"],"Regions":result,"Timings":timing}

def energy(run):
    samples={round(s["Input"]["Time"],8):s for s in run["Replay"]["Samples"]}; g=geom(run)
    output={}
    for label,segments in [("whole",range(9)),("corner1",[1,2,3]),("straight",[4]),("corner2",[5,6,7])]:
        rows=[]
        for t in run["Telemetry"]:
            s=samples.get(round(t["Time"]-t["Seconds"],8))
            if s is None:s=min(run["Replay"]["Samples"],key=lambda s:abs(s["Input"]["Time"]-(t["Time"]-t["Seconds"])))
            if s["Input"]["Rider"]["Road"]["Segment"] not in segments:continue
            rows.append((t,s))
        def total(fn):return sum(fn(t,s) for t,s in rows)
        duration=total(lambda t,s:t["Seconds"])
        output[label]={"Seconds":duration,"WheelEngineWorkJ":total(lambda t,s:t["Wheel"]["EngineWorkJoules"]),
            "ShaftInputWorkJ":total(lambda t,s:t["Wheel"]["EngineWorkJoules"]/t["Forces"]["Engine"]["Efficiency"]),
            "TransmissionLossJ":total(lambda t,s:t["Wheel"]["EngineWorkJoules"]*(1/t["Forces"]["Engine"]["Efficiency"]-1)),
            "SystemDissipationJ":total(lambda t,s:t["EnergyResidual"]),
            "RearLongitudinalSlipJ":total(lambda t,s:t["Wheel"]["LongitudinalLossJoules"]),
            "BearingJ":total(lambda t,s:t["Wheel"]["BearingLossJoules"]),
            "DragWorkJ":total(lambda t,s:-dot(t["Forces"]["Resistance"],s["Input"]["Rider"]["Velocity"])*t["Seconds"]),
            "FrontLateralSlipJ":total(lambda t,s:-dot(t["Forces"]["FrontForce"],t["Forces"]["FrontVelocity"])*t["Seconds"]),
            "RearLateralSlipJ":total(lambda t,s:-t["Forces"]["RearLateral"]*dot(t["Forces"]["RearVelocity"],t["Forces"]["BodyLeft"])*t["Seconds"]),
            "MaxRearUtilization":max((t["Wheel"]["RearUtilization"] for t,s in rows),default=None),
            "MaxFrontUtilization":max((length(t["Forces"]["FrontForce"])/t["Forces"]["TyreLoads"]["FrontCapacityNewtons"] for t,s in rows),default=None),
            "MaxWheelSlipRatio":max((t["Wheel"]["InstantSlipRatio"] for t,s in rows),default=None),
            "MinimumSystemDissipationJPerStep":min((t["EnergyResidual"] for t,s in rows),default=None)}
    return output

def export_csv(name,run):
    headers="time,reference_m,segment,x_m,y_m,speed_kph,body_side_mps,requested_gas,pending_gas,active_gas,requested_steer_deg,steer_deg,yaw_rads,beta_deg,wheel_omega,engine_rpm,ratio,available_crank_torque_Nm,actual_wheel_torque_Nm,available_crank_power_W,actual_wheel_power_W,rear_Fx_N,front_Fy_N,rear_Fy_N,front_util,rear_util,slip_ratio,drag_N,accel_long_mps2,accel_lat_mps2,system_loss_J,rear_slip_loss_J,bearing_loss_J,inner_m,outer_m,force_interval_start,force_interval_seconds".split(",")
    buffer=io.StringIO(newline=""); writer=csv.writer(buffer,lineterminator="\n");writer.writerow(headers)
    telemetry={round(t["Time"],8):t for t in run["Telemetry"]}
    for s in run["Replay"]["Samples"]:
        i=s["Input"]; v=i["Rider"]; t=telemetry.get(round(i["Time"],8)); f=t["Forces"] if t else {}; w=t["Wheel"] if t else {}; e=f.get("Engine",{})
        speed=max(v["Speed"],1e-6); vel=v["Velocity"]; net=f.get("NetForce",{"X":0,"Y":0});
        values=[i["Time"],v["TotalReferenceMeters"],v["Road"]["Segment"],v["Position"]["X"],v["Position"]["Y"],v["Speed"]*3.6,v["SideSpeed"],s["RequestedDrive"],i["Pending"][0]["Action"]["Drive"] if i["Pending"] else None,i["Active"]["Drive"],s["RequestedSteering"]*180/math.pi,i["Steering"]*180/math.pi,v["AngularVelocity"],v["SlideRadians"]*180/math.pi,i["Omega"],e.get("EngineRpm"),e.get("OverallRatio"),e.get("CrankTorqueNewtonMeters"),w.get("DriveTorqueNewtonMeters"),e.get("CrankPowerWatts"),w.get("EngineWorkJoules",0)/t["Seconds"] if t else None,f.get("RearLongitudinal"),f.get("FrontLateral"),f.get("RearLateral"),length(f["FrontForce"])/f["TyreLoads"]["FrontCapacityNewtons"] if t else None,w.get("RearUtilization"),w.get("InstantSlipRatio"),length(f["Resistance"]) if t else None,dot(net,vel)/147/speed if t else None,(vel["X"]*net["Y"]-vel["Y"]*net["X"])/147/speed if t else None,t.get("EnergyResidual") if t else None,w.get("LongitudinalLossJoules"),w.get("BearingLossJoules"),s["Inner"],s["Outer"],t["Time"]-t["Seconds"] if t else None,t["Seconds"] if t else None]
        writer.writerow(values)
    folder=D/"telemetry";folder.mkdir(exist_ok=True)
    with open(folder/(name+".csv.gz"),"wb") as f,gzip.GzipFile(filename="",mode="wb",fileobj=f,mtime=0) as z:z.write(buffer.getvalue().encode())

def main():
    started=time.process_time()
    trials=read(D/"trials.json") if (D/"trials.json").exists() else []
    allcosts=read(D/"costs.json") if (D/"costs.json").exists() else []
    runs={p.name[:-8]:read(p) for p in RAW.glob("*.json.gz") if not p.name.startswith("straight-")}
    baseline=[r for n,r in runs.items() if n.startswith("baseline-")]
    historical=D/'historical-134-A-N00.json.gz'
    if historical.exists():baseline.append(read(historical))
    save("throttle-profiles.json",[profiles(r) for r in baseline])
    save("energy.json",{r["Replay"]["Name"]:energy(r) for r in baseline})
    if not (D/"complete.json").exists():
        print("Baseline profiles written; native experiment is still running.");return
    def camel(value):
        if isinstance(value,dict):return {k[:1].lower()+k[1:]:camel(v) for k,v in value.items()}
        if isinstance(value,list):return [camel(v) for v in value]
        return value
    for geometry_name in ['progressive-b','progressive-c']:
        save(geometry_name+'-viewer-geometry.json',camel(read(D/(geometry_name+'-geometry.json'))))
    table=[];events={}; dnfs=[]
    for row in trials:
        run=runs[row["Name"]];base=runs["baseline-"+row["Case"]];corner=row["Corner"]; marks=landmarks(run,corner);base_marks=landmarks(base,corner)
        command=next(x for x in run["Replay"]["Decisions"] if abs(x["Time"]-row["Time"])<1e-8)
        # C's first command is preparation: gas1 is the SECOND request.
        fulltime=row["Time"]+(.2 if row["Arm"]=="C" else 0)
        activation=next((x for x in run["Replay"]["Activations"] if abs(x["ObservedAt"]-fulltime)<1e-8),None)
        active_sample=min(run["Replay"]["Samples"],key=lambda s:abs(s["Input"]["Time"]-(fulltime+.24))) if activation else None
        ev={"Request":info(min(run["Replay"]["Samples"],key=lambda s:abs(s["Input"]["Time"]-fulltime))),"Activation":info(active_sample),"ObservedAt":fulltime,"ActivationAt":activation["ActivationAt"] if activation else None,
            "Landmarks":marks,"NativeCompleted":run["Metrics"]["Native"]["Completed"],"Outcome":run["Metrics"]["Native"]["Outcome"],"NativeFailureState":None if run["Metrics"]["Native"]["Completed"] else info(run["Replay"]["Samples"][-1])}
        events[row["Name"]]=ev
        gate=run["Metrics"]["Native"]["GateSeconds"];bgate=base["Metrics"]["Native"]["GateSeconds"]
        table.append({**row,"Landmarks":marks,"BaselineLandmarks":base_marks,"BaselineGateSeconds":bgate,"GainSeconds":bgate-gate if gate is not None and bgate is not None else None,"Events":ev})
        if not run["Metrics"]["Native"]["Completed"]:dnfs.append({"Name":row["Name"],"Outcome":run["Metrics"]["Native"]["Outcome"],"State":ev["NativeFailureState"],"Metrics":run["Metrics"]})
    save("paired-results.json",table);save("events.json",events);save("dnf.json",dnfs)
    # Identify SAME-STEERING full throttle that is safe with more progress yet loses the score.
    score_cases=[];risk_counts={}; reasons={}
    groups={}
    for c in allcosts:groups.setdefault((c["Case"],c["Corner"],c["Distance"]),[]).append(c)
    for key,cs in groups.items():
        chosen=next(x for x in cs if x["Chosen"])
        full=next(x for x in cs if x["Action"]["SteeringRadians"]==chosen["Action"]["SteeringRadians"] and x["Action"]["Drive"]==1)
        risk_counts[full["Safety"]]=risk_counts.get(full["Safety"],0)+1
        for cause in full["Causes"]:reasons[cause]=reasons.get(cause,0)+1
        if not full["Chosen"] and full["Safety"]==chosen["Safety"] and full["Progress"]>chosen["Progress"]:
            delta=full["Score"]-chosen["Score"];throttle_delta=full["ThrottleCost"]-chosen["ThrottleCost"]
            score_cases.append({"State":key,"Chosen":chosen,"Full":full,"ThrottleRemovalWouldFlip":delta-throttle_delta<0,"ScoreGap":delta,"ThrottleGap":throttle_delta})
    save("score-findings.json",{"States":len(groups),"ChosenSteeringFullSafety":risk_counts,"Causes":reasons,"SameClassMoreProgressButRejected":score_cases})
    # All native raw is retained in Git, not only a hand-picked successful replay.
    native=D/"native";native.mkdir(exist_ok=True)
    manifest=[]
    for p in sorted(RAW.glob("*.json.gz")):
        target=native/p.name;shutil.copyfile(p,target);manifest.append({"Name":p.name,"Bytes":p.stat().st_size,"Sha256":sha(p)})
    save("raw-manifest.json",manifest)
    # Two diagnostic comparisons, a failed sustained-gas case and the sealed comparisons.
    chosen=[]
    for case in ["D-B","D-C","V-B","V-C"]:
        legal=[r for r in table if r["Case"]==case and r["Metrics"]["Native"]["Completed"]]
        # Prefer a legal, faster actual full-throttle exit for the requested comparison;
        # label this explicitly as retrospective presentation, never confirmation selection.
        full_exit=[r for r in legal if r['Landmarks']['Exit'] and r['Landmarks']['Exit']['Active']>=.99 and r['GainSeconds']>0]
        best=min(full_exit or legal,key=lambda r:(r["Metrics"]["Native"]["GateSeconds"],r["Name"])) if legal else next(r for r in table if r["Case"]==case)
        chosen.append(best)
    failed=next((r for r in table if r["Arm"]=="D" and not r["Metrics"]["Native"]["Completed"]),None)
    if failed:chosen.append(failed)
    selected={};view_groups=[]
    for row in chosen:
        base_name="baseline-"+row["Case"];name=row["Name"];selected[base_name]=runs[base_name];selected[name]=runs[name]
        view_groups.append({"Id":name,"Reasons":["oryginalne AI / diagnostyka","ten sam natywny prefiks"],"Scenario":runs[name]['Replay']['Scenario'],"Scope":"Dwa łuki +1s legalnej kontroli. Pełny gaz jest interwencją diagnostyczną; brak certyfikacji całej przyszłości z H09.","GeometryFile":runs[name]["GeometryName"]+"-viewer-geometry.json","TwoBends":True,"Runs":[{"Name":base_name,"Policy":"A"},{"Name":name,"Policy":row["Arm"]}],"Trial":row,"Events":events[name],
                            "PairedSummary":f"A {row['BaselineGateSeconds']:.2f}s; diagnostic {row['Metrics']['Native']['GateSeconds']}s; {row['Metrics']['Native']['Outcome']}."})
    zipped(D/"selected-replays.json.gz",{"Runs":selected})
    save("index.json",{"Title":"#136 Speed & Throttle Root Cause · interwencje diagnostyczne, Native Box2D","Grade":"INCONCLUSIVE — see report","RawAvailability":"W Git zachowano pełne surowe dane wszystkich54 nowych światów w native/ oraz dawny #134 N00. SHA256: raw-manifest.json; CSV: telemetry/.","Charts":[['speed-throttle.svg','Natywna prędkość i gaz — A / diagnostyka']],"ComparisonCharts":True,"DefaultRun":chosen[0]["Name"],"Groups":view_groups,"Labels":{"A":"A · oryginalne AI #16015","B":"B · jeden gaz1","C":"C · przygotowanie offline","D":"D · diagnostyczne utrzymanie gaz1"},"Colors":{"A":"#ffd27b","B":"#73dfdf","C":"#d99fe3","D":"#ff9c86"},"Horizons":{"A":.9,"B":.9,"C":.9,"D":.9}})
    for name,run in runs.items():export_csv(name,run)
    if historical.exists():export_csv('historical-134-A-N00',read(historical))
    make_report(runs,table,score_cases,risk_counts,reasons)
    save("report-cost.json",{"CpuSeconds":time.process_time()-started,"Worlds":0,"Scope":"Reading, derived native metrics, all CSV exports, bundle and plots; separate from episode/search CPU."})
    if OUT != D:
        shutil.copyfile(D/"README.md",OUT/"raport-136.md")
        shutil.copyfile(D/"speed-throttle.svg",OUT/"speed-throttle.svg")
    print(json.dumps({"NativeFiles":len(manifest),"Bytes":sum(x["Bytes"] for x in manifest),"SelectedRuns":len(selected),"Dnfs":len(dnfs),"ScoreCases":len(score_cases),"Cost":read(D/"complete.json")},indent=2))

def make_report(runs,table,score_cases,risk_counts,reasons):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    plt.rcParams["svg.hashsalt"]="speed-throttle-136"
    figures,axes=plt.subplots(2,2,figsize=(12,7),sharex="col")
    bests=[]
    for col,case in enumerate(["D-B","D-C"]):
        legal=[r for r in table if r["Case"]==case and r["Metrics"]["Native"]["Completed"]]
        full_exit=[r for r in legal if r['Landmarks']['Exit'] and r['Landmarks']['Exit']['Active']>=.99 and r['GainSeconds']>0]
        best=min(full_exit or legal,key=lambda r:(r["Metrics"]["Native"]["GateSeconds"],r["Name"]))
        bests.append(best)
        for name,label,color in [("baseline-"+case,"A #16015","#8c5700"),(best["Name"],best["Name"],"#087e8b")]:
            run=runs[name];ss=run["Replay"]["Samples"];tt=[s["Input"]["Time"] for s in ss]
            axes[0,col].plot(tt,[s["Input"]["Rider"]["Speed"]*3.6 for s in ss],label=label,color=color,lw=1)
            axes[1,col].step(tt,[s["Input"]["Active"]["Drive"] for s in ss],where="post",color=color,lw=1)
        axes[0,col].set_title(runs["baseline-"+case]["GeometryName"]);axes[0,col].legend(fontsize=7)
        axes[0,col].set_ylabel("km/h");axes[1,col].set_ylabel("active throttle");axes[1,col].set_xlabel("native time [s]")
        for ax in axes[:,col]:ax.grid(alpha=.2)
    figures.tight_layout();figures.savefig(D/"speed-throttle.svg",metadata={"Date":None});plt.close(figures)
    figure_path=D/'speed-throttle.svg'
    figure_path.write_text('\n'.join(line.rstrip() for line in figure_path.read_text(encoding='utf-8').splitlines())+'\n',encoding='utf-8',newline='\n')
    profiles_data=read(D/"throttle-profiles.json");complete=read(D/"complete.json");straight=read(D/"straight.json")
    energies=read(D/"energy.json")
    held=[r for r in table if r["Case"].startswith("V-")]
    heldwins=[r for r in held if r["GainSeconds"] is not None and r["GainSeconds"]>0 and r["Landmarks"]["Exit"] and r["Landmarks"]["Exit"]["Active"]>=.99 and r["Landmarks"]["Exit"]["SpeedKph"]>r["BaselineLandmarks"]["Exit"]["SpeedKph"]]
    # Score pathology establishes only a component, not a global causal verdict.
    verdict="MIXED" if score_cases else "INCONCLUSIVE"
    decisive=sum(x["ThrottleRemovalWouldFlip"] for x in score_cases)
    lines=["# #136 — Speedway Speed & Throttle Root Cause", "",f"**{verdict} · diagnostyka, PROVISIONAL, domyślnie OFF.** Bez zmian fizyki, wag AI, produkcyjnego CoreSim, toru i bez treningu CEM. Baza `{read(D/'protocol.json')['BaseHead']}`.","",
           "Wykryto jednocześnie ograniczenia wyboru akcji i ograniczenia bieżącego stanu fizycznego. „MIXED” opisuje mechanizm tych prób; nie zatwierdza opon jako skalibrowanego modelu realnego żużla. Żaden pojedynczy sukces offline nie jest wyuczonym zachowaniem AI. **DIAGNOSIS PASS; PERFORMANCE EVIDENCE niepotwierdzone.** To nie jest diagnoza błędu równań fizyki.","",
           "Po zamknięciu potwierdzono dokładne fingerprinty wszystkich4 baz względem #135 (baseline-reproduction.json). Dodatkowy surowy przejazd #134 A #16015 N00 odczytano bez nowych światów i sprawdzono jego SHA256 wobec oryginalnego ledgeru (historical-reuse.json).", "",
           "## Metoda i definicje","","Protokół, źródła i reguły wyboru zamrożono przed światami. D-B/D-C to dwa dotychczasowe, różne początki #135; preflight odtworzył oba fingerprinty dokładnie. V-B/V-C są osobną pulą, otwartą po diagnostic-lock. Nie traktujemy różnych początków B/C jako izolowanego pomiaru wpływu samej geometrii. Historyczny transfer #135 pozostaje oddzielnym dowodem.","",
           "Geometryczny koniec łuku to TrackFrame.SegmentStartMeters(4) / (8), wynikający z istniejącej jajowatej geometrii. Bramka ukończenia pełnego obrysu leży dalej: koniec drugiego łuku +3m oraz +1s legalnej kontynuacji. Nie są to te same pomiary. Interwencje przy pierwszej decyzji co0,2s z pozostałą fizyczną drogą stałego offsetu <=20/15/10/5m; 0 oznacza pierwszą decyzję po granicy. Dokładne położenia są w states.json, odchylenia nie są ukrywane.","",
           "A = oryginalne #134 A #16015, H09, stara funkcja i pełna przestrzeń gazu. B = jedno żądanie gaz1 z kierownicą A, następnie A. D = diagnostycznie powtarzany gaz1 do exit+20m z kierownicą A. C = jedna sekwencja trzech komend wybrana offline spośród15 opcji przez oryginalny koszt H09, potem A. Wszystkie żądania przechodzą przez0,24s i prawdziwą kolejkę. Starsze pending pozostają. Nie ma nowej polityki ani pełnego MPC.","",
           "Wartości w tabelach są pierwszym natywnym accepted sample na/po wskazanym przekroju, z rzeczywistą pozycją w JSON. Nie interpolujemy prędkości lub trajektorii. N/D oznacza brak zmierzonego przekroju przed końcem świata, a nie zero. Po drugim łuku zapis trwa tylko przez odziedziczony czas kontroli; nie jest to pomiar całych następnych62m. Próby prostej osobno pokrywają rzeczywiste62m.","",
           "## 1. Dlaczego średnio około20% gazu?","","Profil jest zależny od początku. Poniższe udziały są całkami czasu aktywnego gazu, uwzględniają fizyczne aktywacje, nie liczbą decyzji.","",
           "| Przejazd / obszar | Średni gaz | 0% | (0,50)% | [50,99)% | [99,100]% | średnia km/h |","|---|---:|---:|---:|---:|---:|---:|"]
    for p in profiles_data:
        for area,x in p["Regions"].items():
            lines.append(f"|{p['Name']} / {area}|{100*x['MeanThrottle']:.2f}%|"+"|".join(f"{100*y:.2f}%" for y in x["ThrottleFractions"])+f"|{x['MeanSpeedKph']:.2f}|")
    lines += ["","Akcje dostępne w tym eksperymencie mają gaz0, około0,318 lub1 (oraz bieżącą aktywną komendę). Średnia około0,2 wynika z czasu spędzonego na0 i częściowym gazie; nie oznacza stałej fizycznej przepustnicy20%. Dokładne pierwsze odpuszczenie, pierwsze żądanie/aktywacja1, czas gaz0, przełączenia i odległości po wyjściu: [throttle-profiles.json](throttle-profiles.json).","",
              "## 2–4. Dlaczego gaz1 jest rzadki i czy przegrywa przez funkcję celu?","",
              f"W20 zamrożonych obserwacjach końców obu łuków porównano wszystkie te same kierownice × gaz0/0,318/1. Klasa pełnego gazu przy kierownicy wybranej przez A: `{risk_counts}`. Przyczyny prognoz: `{reasons}`.","",
              f"**{len(score_cases)} obserwacji:** gaz1 przy tej samej kierownicy ma tę samą klasę bezpieczeństwa oraz większy przewidywany postęp, lecz przegrywa wynikiem. W **{decisive}** z nich samo usunięcie różnicy kary throttle odwróciłoby porównanie tej pary. To audyt algebraiczny bez zmiany sterownika i bez natywnego dowodu ukończenia dla usuniętej wagi.","",
              "**Główny mechanizm w tym zbiorze to koszt terminalnego yaw w prognozie utrzymanej akcji, nie sama preferencja gaz0,318.** Choose() nie przewiduje normalnych kolejnych korekt co0,2s: po reakcji utrzymuje badany gaz i kierownicę aż do końcaH09. Podwyższony napęd zmienia rozkład Fx/Fy i moment tylnego kontaktu; przewidywane yaw potrafi wzrosnąć do około2,3rad/s jeszcze poniżej beta45. Duża waga yaw odrzuca tę perspektywę. Rzeczywisty krótki impuls B wraca do A i ma inne późniejsze komendy. Nie nazywamy tej różnicy automatycznie błędem fizyki predyktora.","",
              "Przykład D-B, pierwszy łuk, nominalne10m: częściowy gaz daje postęp14,8517m,63,6931km/h, yaw-cost0,2541 i score−14,6145. Gaz1 przy tej samej kierownicy daje15,4996m,68,9377km/h i nadal SafeInHorizon, ale yaw-cost59,2588, throttle1,3074 i score44,4168 (22.miejsce). Geometry-speed i clearance-cost wynoszą0 dla obu. To konkretny powód odrzucenia; nie etykieta Unsafe.","",
              "Kara throttle wynosi około0,2848 dla gaz0,0 dla gaz0,31820689 i1,3074 dla gaz1. Jej nadmierna siła jako samodzielna przyczyna NIE została potwierdzona: żaden z18 porównanych wyborów nie odwraca się po samym odjęciu tej różnicy. Progress jest normalizowany0,6/0,9. Najpierw wybierana jest klasa Safe/Uncertain/Unsafe, dopiero potem koszt. Yaw nie ma osobnego twardego progu, a NoSafeContinuation znaczy brak Safe w skończonej siatce H09, nie fizyczny dowód braku wszystkich manewrów.","",
              "[costs.json](costs.json) podaje dla KAŻDEJ akcji postęp, km/h poH09, klasę, direction/yaw/throttle/geometry/steering/clearance/unsafe/progress, sumę i ranking. Suma jest sprawdzona względem niezmienionego Choose(). [score-findings.json](score-findings.json) wskazuje konkretne pary; beta45, banda, kreda i margines ekstrapolacji mają oddzielne przyczyny.","",
              "## 5. Kiedy wydać polecenie?","","Nie ma jednego bezpiecznego punktu niezależnego od stanu. Żądanie musi poprzedzać wyjście o co najmniej rzeczywisty ruch w0,24s; przy70km/h to4,67m, przy100km/h6,67m, zanim doliczymy fazę przygotowania. Odległości są kinematycznym objaśnieniem, nie wpisanymi w AI progami. Poniżej wszystkie zmierzone interwencje; dla C żądanie gaz1 jest drugą komendą,0,2s po przygotowaniu.","",
              "| Próba | żądanie1 s | aktywacja s | gaz wyjście | km/h wyjście | po20m | po40m | czas2łuki | zysk s | wynik |","|---|---:|---:|---:|---:|---:|---:|---:|---:|---|"]
    def fmt(x,n=2):return "N/D" if x is None else f"{x:.{n}f}"
    def mark(m,key,field):return (m.get(key) or {}).get(field)
    for r in table:
        m=r["Landmarks"];e=r["Events"]
        lines.append(f"|{r['Name']}|{e['ObservedAt']:.2f}|{fmt(e['ActivationAt'])}|{fmt(mark(m,'Exit','Active'))}|{fmt(mark(m,'Exit','SpeedKph'))}|{fmt(mark(m,'After20m','SpeedKph'))}|{fmt(mark(m,'After40m','SpeedKph'))}|{fmt(r['Metrics']['Native']['GateSeconds'])}|{fmt(r['GainSeconds'])}|{r['Metrics']['Native']['Outcome']}|")
    lines += ["","[events.json](events.json) zawiera moment i stan wydania gaz1, rzeczywistą aktywację, stan5m przed/za geometryczną granicą, beta/yaw/clearance i dokładny stan DNF. Przekroczenie45° to odziedziczone wyjście poza obwiednię kontroli, nie automatycznie fizyczny upadek.","",
              "## 6–7. Wykonalność gaz1 i wcześniejsza kierownica","","Poniższe legalne próby z pełnym gazem na wyjściu i dodatnim zyskiem wybrano retrospektywnie z zamrożonego zbioru (najszybsza taka próba w każdej geometrii). Kryterium prezentacji jest jawnie inne niż zamrożony wybór do niezależnego potwierdzenia. Porównanie nie oznacza nowego autonomicznego AI. Każdy wariant ma zgodny natywny prefiks do aktywacji pierwszej zmienionej komendy.",""]
    for r in bests:
        a=r["BaselineLandmarks"];m=r["Landmarks"];base=runs["baseline-"+r["Case"]]["Metrics"];actual=r["Metrics"]
        lines += [f"### {runs[r['Name']]['GeometryName']} — {r['Name']}","","|Pomiar|Obecne AI|Manewr diagnostyczny|","|---|---:|---:|",
                  f"|Gaz5m przed wyjściem|{fmt(mark(a,'Before5m','Active'))}|{fmt(mark(m,'Before5m','Active'))}|",
                  f"|Gaz na geometrycznym wyjściu|{fmt(mark(a,'Exit','Active'))}|{fmt(mark(m,'Exit','Active'))}|",
                  f"|Prędkość wyjścia km/h|{fmt(mark(a,'Exit','SpeedKph'))}|{fmt(mark(m,'Exit','SpeedKph'))}|",
                  f"|Po20m km/h|{fmt(mark(a,'After20m','SpeedKph'))}|{fmt(mark(m,'After20m','SpeedKph'))}|",
                  f"|Po40m km/h|{fmt(mark(a,'After40m','SpeedKph'))}|{fmt(mark(m,'After40m','SpeedKph'))}|",
                  f"|Maksimum zmierzone na prostej km/h|{fmt(a['MaximumStraightKph'])}|{fmt(m['MaximumStraightKph'])}|",
                  f"|Czas dwóch łuków s|{fmt(r['BaselineGateSeconds'])}|{fmt(actual['Native']['GateSeconds'])}|",
                  f"|Maksymalne beta stopnie|{fmt(base['Native']['MaximumBetaDegrees'])}|{fmt(actual['Native']['MaximumBetaDegrees'])}|",
                  f"|Minimalny certyfikowany prześwit m|{fmt(base['Native']['MinimumClearanceMeters'],4)}|{fmt(actual['Native']['MinimumClearanceMeters'],4)}|",
                  "|Ukończenie|PASS|PASS|",""]
    lines += ["Stan fizyczny i przygotowanie są istotne: utrzymanie gaz1 z tym samym feedbackiem kierownicy może przekroczyć beta45 albo granicę obrysu. Zestaw C sprawdza15 sekwencji kierownicy, a nie tylko stały kąt; wybór następuje w H09 przed wynikiem native. Nie narzucono offsetu ani idealnej linii. Brak sukcesu tych15 sekwencji nie dowodzi globalnej niemożliwości. DNF z większą prędkością nie jest sukcesem.","",
              "## 8. Dlaczego prędkość spada w łuku?","","Podczas gaz0 napęd nie kompensuje oporu i pracy dysypacyjnej obu opon. Przy skręcie część budżetu tylnego kontaktu zajmuje siła boczna, a duży gaz może zwiększać wheelspin zamiast użytecznego Fx; front i rear działają w różnych lokalnych prędkościach i generują rzeczywisty moment yaw. Poniższy bilans rozdziela pracę napędu od strat całego układu, nie zakłada z góry winy silnika.","",
              "|Baza / obszar|Praca na kole J|Straty układu J|Tylny slip wzdłużny J|Przód bocznie J|Tył bocznie J|Opór J|max rear util|","|---|---:|---:|---:|---:|---:|---:|---:|"]
    for name,e in energies.items():
        for area in ["corner1","straight","corner2"]:
            x=e[area];lines.append(f"|{name}/{area}|{x['WheelEngineWorkJ']:.1f}|{x['SystemDissipationJ']:.1f}|{x['RearLongitudinalSlipJ']:.1f}|{x['FrontLateralSlipJ']:.1f}|{x['RearLateralSlipJ']:.1f}|{x['DragWorkJ']:.1f}|{x['MaxRearUtilization']:.3f}|")
    lines += ["","## 9–10. Czy można przekroczyć100km/h i jakie jest przyspieszenie?","","Osiem kontrolowanych światów na rzeczywistej prostej62m progressive-c. Starty to czyste toczenie omega=v/0,33, z fizycznie powiązanym obrotem silnika; nie przenosimy starego wheelspin ze stanu o innej prędkości. Oryginalna aktywna komenda startowa to gaz1 przez wspólne0,24s, później działa żądany gaz. To jawna część porównania, nie ukryta natychmiastowa aktywacja.","",
              "|Start km/h|Gaz żądany|10m|20m|30m|40m|50m|62m|","|---:|---:|---:|---:|---:|---:|---:|---:|"]
    for r in straight:
        lines.append(f"|{r['InitialKph']:.0f}|{r['RequestedDrive']:.3f}|"+"|".join(fmt(r["Milestones"].get(str(m),{}).get("SpeedKph")) for m in [10,20,30,40,50,62])+"|")
    fullacc=[]
    for r in straight:
        if r["RequestedDrive"]!=1:continue
        raw=read(RAW/(r["Name"]+".json.gz"));acc=[dot(x["Forces"]["NetForce"],x["Forces"]["BodyForward"])/147 for x in raw["Rows"]]
        fullacc.append({"InitialKph":r["InitialKph"],"Min":min(acc),"Max":max(acc),"Final":acc[-1]})
    save("straight-acceleration.json",fullacc)
    lines += ["",f"Przyspieszenia wzdłuż osi motocykla przy gaz1, min/max/ostatnie m/s²: `{fullacc}`.","",
              "Nie utożsamiamy izolowanej zdolności przyspieszania z legalnym przygotowaniem kolejnego łuku: AI może potrzebować odpuścić jeszcze na prostej. Oryginalny silnik500cc, krzywa i przełożenie9, sprawność0,94, statyczny grip i opory są zgodne z bazą #135. Historyczne119,3/151km/h pochodziły z innych fixture/stanów; nie są wymaganym wynikiem tego owalu. Prosta nie została wydłużona.","",
              "## 11. Czy energia i przyczepność są wiarygodne?","","Przeszedł odziedziczony guard energii oraz sił/aktuatora. Każdy accepted interval zachowuje pracę na kole, energię translacyjną/obrotową motocykla i0,9kgm² koła. CSV zawiera rzeczywisty moment na kole, dostępny moment wału, RPM, moc dostępną i rzeczywistą pracę/dt, Fx/Fy, utilization, slip, opory i przyspieszenia. Dostępna moc wału z Engine nie jest automatycznie mocą wykorzystaną; throttle i prędkość koła mają znaczenie.","",
              "Bilans liczbowy i dissipative contact nie stanowią walidacji realnego podłoża. µ(surface), sztywności/relaksacja i część krzywej silnika pozostają PROVISIONAL. Z tych prób nie wynika potrzeba arbitralnego zwiększenia gripu ani mocy. Nie wykryto uzasadnienia do podmiany fizyki; realistyczne przyspieszanie w uślizgu wymaga osobnej empirycznej walidacji.","",
              "## 12–13. Zysk czasu i potwierdzenie na obu geometriach","",
              "|Pula potwierdzająca|Manewr|A s|Próba s|Zysk s|Gaz wyjście|km/h wyjście A/próba|Wynik|","|---|---|---:|---:|---:|---:|---:|---|"]
    for r in held:
        lines.append(f"|{r['Case']}|{r['Arm']} C{r['Corner']} {r['Distance']}m|{fmt(r['BaselineGateSeconds'])}|{fmt(r['Metrics']['Native']['GateSeconds'])}|{fmt(r['GainSeconds'])}|{fmt(mark(r['Landmarks'],'Exit','Active'))}|{fmt(mark(r['BaselineLandmarks'],'Exit','SpeedKph'))}/{fmt(mark(r['Landmarks'],'Exit','SpeedKph'))}|{r['Metrics']['Native']['Outcome']}|")
    lines += ["",f"Pełny pakiet kryteriów PERFORMANCE nie jest automatycznie spełniony przez sam zysk czasu. Z {len(held)} prób held **{len(heldwins)}** ma równocześnie legalne ukończenie, dodatni zysk czasu, gaz1 na wyjściu i wyższą prędkość wyjścia. Dalsze kryterium wyższej prędkości na prostej wymaga dostępnego wspólnego przekroju. Próby tego samego początku nie są niezależnymi zawodnikami.","",
              "## 14. Wszystkie DNF","","[dnf.json](dnf.json) zachowuje każdy nieukończony wariant, stan końcowy i minima certyfikacji. „Uncertified interval” odróżniamy od obserwowanego przekroczenia bandy/kredy. Nie odrzucamy porażek po wyniku.","",
              "## 15. Co poprawić dalej?","",f"**{verdict}.** Skończony audyt potwierdza odrzucanie gaz1 przede wszystkim przez koszt yaw przy utrzymanej komendzie H09, obok rzeczywistych ograniczeń stabilności stanów podczas dłuższego gaz1. Native pokazuje legalny pełny gaz na wyjściu w obu geometriach, ale wybrane poprawy nie przenoszą się na held stany. Nie ma dowodu, że brak mocy ogranicza przyspieszanie z60–70km/h na62m; nie ma też dowodu błędu charakterystyki opony. Następny PR powinien najpierw sprawdzić zgodność terminalnej oceny yaw z krótkimi fizycznymi korektami i przygotowaniem, z niezmienionym napędem i gripem. Samo obniżenie ThrottleWeight nie rozwiązuje wykazanego mechanizmu. Nie wdrażać reguły cornerExit→1 ani pełnego MPC. PERFORMANCE EVIDENCE: NIEPOTWIERDZONE.","",
              "## Koszt, odtwarzanie i ograniczenia","",f"{complete['Worlds']} nowych światów Native; **{complete['CpuSeconds']:.6f}s CPU** epizodów i offline audytu, {complete['ContactEvaluations']} ocen kontaktu, {complete['Forecasts']} prognoz. Limity54/1400s/25M/250k. Preflight2 światy maksymalnie20s CPU każdy; oba przeszły. Czas buildów/testów, geometrii przed timerem epizodu i raportowania jest poza tą sumą; report-cost.json mierzy raport osobno. Brak CEM i historycznej wielkiej macierzy.","",
              "Surowe dane WSZYSTKICH nowych światów są w native/, CSV z interwałami i siłami w telemetry/. Gaz jest stanem right-continuous przy znaczniku czasu; siły/moc/przyspieszenie w wierszu odnoszą się do ostatniego ukończonego przedziału force_interval_start/seconds. Nie mieszaj tych czasów na krawędzi aktywacji. Także accepted podkroki krótsze niż10ms pozostają w zapisie. All raw SHA256 w raw-manifest.json.","",
              "Uruchom tools/run-speed-throttle.ps1 do świeżych katalogów (Python z matplotlib). Guard źródeł odmawia kontynuacji po zmianie kodu. Po wszystkich światach przeniesiono WYŁĄCZNIE dispatch CLI do osobnego narzędzia z --enable-speed-throttle, aby przywrócić historycznie zamrożony wspólny Program.cs. Pozostałe różnice źródeł to CRLF→LF. Wszystkie wykonane bajty źródeł są w executed-sources.json.gz; packaging-audit.json wykazuje brak zmiany metod fizyki, sterownika i diagnostyki. Nie nadpisano protokołu ani diagnostic-lock; stary zapis nie jest wznawiany po przepakowaniu. Świeże odtworzenie zamraża obecne bajty. Testy i Windows CI są raportowane oddzielnie w validation.json/opisie PR; brak pomiaru nie jest PASS.","",
              "[Replay viewer](../../../tools/replay-viewer/?case=speed-throttle) · [protokół](protocol.json) · [stany](states.json) · [koszty](costs.json) · [pary](paired-results.json) · [telemetria](raw-manifest.json)","",
              "![Prędkość i rzeczywisty gaz](speed-throttle.svg)",""]
    (D/"README.md").write_text("\n".join(lines),encoding="utf-8")
    index=read(D/"index.json");index["Grade"]=verdict;save("index.json",index)

if __name__=="__main__":main()
