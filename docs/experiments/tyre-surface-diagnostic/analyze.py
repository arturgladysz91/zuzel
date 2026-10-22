"""Small static experiment. Physics is exclusively queried in the original C# assembly.

Run from the repository root after building probe.csproj. Requires numpy/scipy/matplotlib.
No integration, historical worlds, parameter fitting, or new runtime controller.
"""
import csv
import hashlib
import json
import math
from pathlib import Path
import subprocess
import platform

import numpy as np
from scipy.optimize import least_squares, brentq
from scipy.signal import place_poles

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
P = json.loads((HERE / "protocol.json").read_text())
assert subprocess.check_output(["git","merge-base",P["baseHead"],"HEAD"],cwd=ROOT,text=True).strip()==P["baseHead"]
subprocess.run(["git","diff","--exit-code",P["baseHead"],"--","src","tools","tests","data"],cwd=ROOT,check=True)
M, I, RW, IW, BEARING = P["massKg"], P["inertiaKgMetersSquared"], .33, .9, .02
server = subprocess.Popen(["dotnet", str(HERE / "bin/Release/net8.0/probe.dll")],
                          stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True, cwd=ROOT)
def query(op, **kw):
    server.stdin.write(json.dumps(dict(op=op, **kw)) + "\n")
    server.stdin.flush()
    line = server.stdout.readline()
    if not line:
        raise RuntimeError("C# contact adapter stopped")
    return json.loads(line)

def save(name, value):
    (HERE / name).write_text(json.dumps(value, indent=2, allow_nan=False) + "\n", encoding="utf-8")

historical = {}
for manifest in sorted((ROOT / "docs/experiments").glob("*/manifest.json")):
    if manifest.parent == HERE:
        continue
    entries = json.loads(manifest.read_text(encoding="utf-8-sig"))
    if not isinstance(entries, dict) or not all(isinstance(v, str) and len(v)==64 for v in entries.values()):
        continue
    for name, expected in entries.items():
        file = manifest.parent / name
        actual = hashlib.sha256(file.read_bytes()).hexdigest().upper()
        assert actual == expected.upper(), str(file)
        historical[str(file.relative_to(ROOT)).replace("\\", "/")] = actual

saved = [query("saved", t=t, replay=True) for t in P["gridSourceSeconds"]]
assert max(x["ReplayError"] for x in saved) == 0
grid = []
for t in P["gridSourceSeconds"]:
    for k in P["kappa"]:
        for a in P["alphaRearDegrees"]:
            grid.append(dict(SourceSeconds=t, AskedKappa=k, AskedAlpha=a, **query("grid", t=t, kappa=k, alpha=a)))
    for tail in P["tailQueries"]:
        grid.append(dict(SourceSeconds=t, AskedKappa=tail["kappa"], AskedAlpha=tail["alpha"], Tail=True,
                         **query("grid", t=t, **tail)))
assert max(x["DirectError"] for x in grid) == 0
assert all(math.hypot(x["Fx"], x["Fy"]) <= x["Br"] + 1e-4 for x in grid)
for t in P["gridSourceSeconds"]:
    for a in P["alphaRearDegrees"]:
        curve=[z for z in grid if z["SourceSeconds"]==t and z["AskedAlpha"]==a and not z.get("Tail")]
        assert np.min(np.diff([z["Fx"] for z in curve]))>=0
        assert np.max(np.diff([abs(z["Fy"]) for z in curve]))<=0
    for k in P["kappa"]:
        curve=[z for z in grid if z["SourceSeconds"]==t and z["AskedKappa"]==k and not z.get("Tail")]
        assert np.min(np.diff([abs(z["Fy"]) for z in curve]))>=0
save("static-grid.json", grid)
with (HERE / "static-grid.csv").open("w", newline="", encoding="utf-8") as f:
    keys=["SourceSeconds","AskedKappa","AskedAlpha","Ur","Fx","Fy","Br","BetaDegrees","AlphaRearDegrees","Kappa"]
    writer=csv.DictWriter(f,keys,extrasaction="ignore");writer.writeheader();writer.writerows(grid)

representative=saved[1]
throttle=[]
for d in P["throttle"]:
    # Frozen wheel versus torque-relaxed wheel. Neither advances a rigid body.
    h0=query("saved", drive=d)
    finite=query("saved", drive=d, h=.01)
    def wheel_balance(omega):
        return query("saved", drive=d, omega=omega)["OmegaDot"]
    omega=brentq(wheel_balance, 0, 140, xtol=1e-7)
    relaxed=query("saved", drive=d, omega=omega)
    eps=.01
    slope=(wheel_balance(omega+eps)-wheel_balance(max(0,omega-eps)))/(eps+min(eps,omega))
    throttle.append(dict(Drive=d,FrozenH0=h0,FrozenH10ms=finite,RelaxedWheel=relaxed,WheelEigenvalue=slope,
                         WheelTimeConstantSeconds=-1/slope if slope<0 else None))
assert len({(z["FrozenH0"]["Fx"],z["FrozenH0"]["Fy"]) for z in throttle})==1
save("saved-state.json", dict(Original=saved, RepresentativeSeconds=.5, Throttle=throttle,
    RequiredRadiusFromDemand=representative["Speed"]**2/representative["Demand"],
    PathRadius=1/representative["PathCurvature"],
    AbsoluteMinimumRadius=representative["Speed"]**2/(representative["B"]/M),
    RequiredNormalFromDemand=M*representative["Demand"]))

# Two predetermined initial guesses, one geometric fixture, no native world.
V, RADIUS = 18., 46.
R = V/RADIUS
def at_balance(x):
    beta, steer, omega, drive=x
    return query("state", u=V*math.cos(beta), w=-V*math.sin(beta), r=R,
                 omega=omega, steer=steer, drive=drive)
def balance(x):
    z=at_balance(x)
    return np.array([z["Tangential"]/M,z["Normal"]/M-V*V/RADIUS,z["Torque"]/I,z["OmegaDot"]*RW])
def jacobian(fun, x, steps):
    return np.column_stack([(fun(x+np.eye(len(x))[j]*h)-fun(x-np.eye(len(x))[j]*h))/(2*h)
                            for j,h in enumerate(steps)])
steps=np.array([2e-4,2e-4,.01,1e-4])
equilibria=[]
for seed in P["algebraicInitialGuesses"]:
    x0=np.array([math.radians(seed[0]),math.radians(seed[1]),seed[2],seed[3]])
    result=least_squares(balance,x0,jac=lambda x:jacobian(balance,x,steps),
                         bounds=([0,-math.radians(34.99),1,.0001],[math.radians(60),math.radians(34.99),139.5,.9999]),
                         xtol=1e-9,ftol=1e-9,gtol=1e-9,max_nfev=100,x_scale=[.1,.05,60,.5])
    x=result.x; forces=at_balance(x);residual=balance(x)
    record=dict(Seed=seed,Success=bool(np.max(np.abs(residual))<2e-5),Residual=residual.tolist(),
                FunctionEvaluations=result.nfev,Unknowns=x.tolist(),Forces=forces)
    if record["Success"]:
        # State includes the finite-rate steering actuator. Position/phase are symmetry modes,
        # not part of this local speed/slip/curvature-stability claim.
        state=np.array([forces["U"],forces["W"],R,x[2],x[1]])
        control=np.array([x[3],0.])
        def ode(s,c):
            u,w,r,omega,steer=s
            z=query("state",u=u,w=w,r=r,omega=omega,steer=steer,drive=c[0])
            return np.array([z["FxBody"]/M+r*w,z["FyBody"]/M-r*u,z["Torque"]/I,z["OmegaDot"],c[1]])
        # Large enough to resolve the original float force arithmetic, then halve as sensitivity.
        hs=np.array([.002,.002,2e-4,.02,2e-4]);hc=np.array([2e-4,2e-4])
        A=jacobian(lambda s:ode(s,control),state,hs)
        B=jacobian(lambda c:ode(state,c),control,hc)
        A2=jacobian(lambda s:ode(s,control),state,hs/2)
        B2=jacobian(lambda c:ode(state,c),control,hc/2)
        ctr=np.column_stack([np.linalg.matrix_power(A,i)@B for i in range(5)])
        rank=int(np.linalg.matrix_rank(ctr))
        poles=[-1,-2,-3,-4,-5]
        K=place_poles(A,B,poles).gain_matrix
        # Show a finite nonzero interior neighbourhood respecting both actuator bounds.
        scales=np.array([.01,.01,.001,.01,.0001])
        amplitude=np.abs(K)@scales
        assert rank==5 and np.max(np.linalg.eigvals(A2-B2@K).real)<0
        assert amplitude[0]<min(control[0],1-control[0]) and amplitude[1]<math.pi/2
        record["Linearization"]=dict(State=state.tolist(),Control=control.tolist(),A=A.tolist(),B=B.tolist(),
            AHalfStep=A2.tolist(),BHalfStep=B2.tolist(),ControllabilityRank=rank,
            OpenEigenvalues=[[z.real,z.imag] for z in np.linalg.eigvals(A)],
            OpenEigenvaluesHalfStep=[[z.real,z.imag] for z in np.linalg.eigvals(A2)],
            WitnessGain=K.tolist(),ClosedEigenvalues=[[z.real,z.imag] for z in np.linalg.eigvals(A-B@K)],
            ClosedHalfStep=[[z.real,z.imag] for z in np.linalg.eigvals(A2-B2@K)],
            LocalPerturbationBox=scales.tolist(),MaximumControlDeviation=amplitude.tolist(),
            Rule="Continuous contact linearization only; hypothetical feedback witness, not #117 or a nonlinear/native stability certificate.")
    equilibria.append(record)
save("local-equilibria.json",dict(Speed=V,Radius=RADIUS,Equilibria=equilibria))
counts=query("count");server.stdin.close();assert server.wait()==0
save("validation.json",dict(**counts,GridPoints=len(grid),SavedReplayMaximumError=max(z["ReplayError"] for z in saved),
    DirectMaximumError=max(z["DirectError"] for z in grid),HistoricalFilesChecked=len(historical),HistoricalSha256=historical,
    Python=platform.python_version(),Numpy=np.__version__,
    SourceSha256={name:hashlib.sha256((ROOT/name).read_bytes()).hexdigest() for name in [
        "tools/shared-world-speedway/RearWheelDynamics.cs","tools/shared-world-speedway/TwoContactBikeDynamics.cs",
        "tools/shared-world-speedway/Engine500.cs","tools/shared-world-speedway/LoadBasedTyreGrip.cs",
        "tools/shared-world-speedway/TravelDirectionSteering.cs",
        "docs/experiments/motorcycle-performance-500cc/C-both-egg-first.csv.gz"]},
    Rule="All prior experimental manifest hashes checked; no historical simulations/energy tests rerun. Local Release probe build only; no full Windows CI."))
print(json.dumps(dict(Counts=counts,Saved=representative,Equilibria=equilibria),indent=2))
