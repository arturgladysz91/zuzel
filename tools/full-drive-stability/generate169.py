"""Derive the study-only native and predictor adapters from the frozen harnesses."""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent


def replace(source, before, after, count=1):
    assert source.count(before) == count, before
    return source.replace(before, after)


def native():
    s = (ROOT / "tools/autonomous-fast-corner/NativeWorld166.Generated.cs").read_text(encoding="utf-8")
    s = s.replace("NativeWorld166", "NativeWorld169")
    s = replace(s, "namespace LearnedCorner166;", "using Speed148;\nnamespace FullDrive169;")
    s = replace(s, "public sealed class NativeWorld169", "internal sealed class NativeWorld169")
    s = replace(s, "RiderView[] views;double time;bool ran;", """RiderView[] views;double time;bool ran;
    readonly PhysicsVariant experiment;
    readonly double experimentFrom;
    readonly double[] acceptedAx;
    public double AcceptedAx => acceptedAx[0];
    bool UsingVariant => experiment.Active && time>=experimentFrom-1e-10;""")
    s = replace(s, "double postSeconds=0){", "double postSeconds=0,PhysicsVariant? experiment=null,double experimentFrom=0){")
    s = replace(s, "this.postSeconds=postSeconds;", "this.experiment=experiment??PhysicsVariant.Original;this.experimentFrom=experimentFrom;this.postSeconds=postSeconds;")
    s = replace(s, "nextReview=new double[specs.Length];", "acceptedAx=new double[specs.Length];nextReview=new double[specs.Length];")
    s = replace(s, "var probe=Forces(views[i],", """var probe=UsingVariant?Contact169.Forces(views[i],specs[i],frame,surface,parameters,time,
                    physics.Masses[i],wheels[i],steering[i],0,active[i].SteeringRadians,shafts[i],RiderController.ReactionSeconds(specs[i]),experiment,acceptedAx[i])
                    :Forces(views[i],""")
    s = replace(s, "forces[i]=Forces(before[i],", """forces[i]=UsingVariant?Contact169.Forces(before[i],specs[i],frame,surface,parameters,time,
                    physics.Masses[i],wheels[i],steering[i],(float)dt,active[i].SteeringRadians,shafts[i],RiderController.ReactionSeconds(specs[i]),experiment,acceptedAx[i])
                    :Forces(before[i],""")
    s = replace(s, "for(int i=0;i<specs.Length;i++){wheels[i].Commit();steering[i].Commit();shafts[i].Commit();}", """for(int i=0;i<specs.Length;i++){
                if(UsingVariant){
                    wheels[i]=ForecastFork148.Wheel(powers[i].Wheel.AfterOmega,wheels[i].Parameters);
                    steering[i]=new TwoContactBikeState(forces[i].SteeringAfter);
                }else{wheels[i].Commit();steering[i].Commit();}
                shafts[i].Commit();
            }""")
    s = replace(s, "if(specs[i].SpeedMetersPerSecond==0&&time<RiderController", """acceptedAx[i]=Contact169.Acceleration(before[i],views[i],(float)dt);
                if(specs[i].SpeedMetersPerSecond==0&&time<RiderController""")
    s = replace(s, "wheels[i].Prepared.LongitudinalLossJoules", "power.Wheel.LongitudinalLossJoules")
    s = replace(s, "wheels[i].Prepared.RearUtilization", "power.Wheel.RearUtilization")
    return s


def predictor():
    s = (ROOT / "tools/shared-world-speedway/StandingStart144/Predictor144.cs").read_text(encoding="utf-8")
    s = replace(s, "namespace SharedWorldSpeedway;", "using SharedWorldSpeedway;\nusing Speed148;\nnamespace FullDrive169;")
    s = replace(s, "public static class Predictor144", "internal static class Predictor169")
    s = replace(s, "Action<double,double>? observeCrank=null)", """Action<double,double>? observeCrank=null,
        PhysicsVariant? experiment=null,double experimentFrom=0,double acceptedAx=0,double stepSeconds=.01)""")
    s = replace(s, "var shaft=powertrain?.Copy();", """CrankClutch144 shaft=powertrain?.Copy()??throw new ArgumentException("169 predictor requires the accepted crank.");
        var variant=experiment??PhysicsVariant.Original;""")
    s = replace(s, "shaft is not null&&(closedLoop is not null||causalPolicy is not null)", "closedLoop is not null||causalPolicy is not null")
    s = replace(s, "shaft?.Omega??0", "shaft.Omega")
    s = replace(s, "if (!p.EnableFreeRacing)", """if(stepSeconds is not (.02 or .01 or .005 or .0025))throw new ArgumentException("Bounded diagnostic step required.");
        if (!p.EnableFreeRacing)""")
    s = replace(s, "var nominal = (Math.Floor((time + 1e-8) / .01) + 1) * .01;",
                "var nominal = (Math.Floor((time + 1e-8) / stepSeconds) + 1) * stepSeconds;")
    s = replace(s, "new RearWheelState(input.Omega * (p.RearWheel ?? RearWheelParameters.Default).WheelRadiusMeters, p.RearWheel)",
                "ForecastFork148.Wheel(input.Omega,p.RearWheel)")
    s = replace(s, "TwoContactForces Query(float dt) {", """bool useVariant=variant.Active&&time>=experimentFrom-1e-10;
            TwoContactForces Query(float dt) {""")
    s = replace(s, "return Contact144.Forces", """if(useVariant)return Contact169.Forces(v,spec,frame,surface,p,time,mass,wheel,steering,dt,
                    active.SteeringRadians,shaft,reaction,variant,acceptedAx);
                return Contact144.Forces""")
    s = replace(s, "v = v with { Position = position", "var before=v;\n            v = v with { Position = position")
    s = replace(s, "wheel.Commit(); steering.Commit();shaft?.Commit();", """acceptedAx=Contact169.Acceleration(before,v,h);
            if(useVariant){
                wheel=ForecastFork148.Wheel(shaft.Prepared.Wheel.AfterOmega,wheel.Parameters);
                steering=new TwoContactBikeState(applied.SteeringAfter);
            }else{wheel.Commit();steering.Commit();}
            shaft.Commit();""")
    return s


for name, source in (("NativeWorld169", native()), ("Predictor169", predictor())):
    target = HERE / (name + ".Generated.cs")
    result = "// Generated by generate169.py. Research only.\n#nullable enable\n" + source
    if "--check" in sys.argv:
        assert target.read_text(encoding="utf-8") == result, target
    else:
        target.write_text(result, encoding="utf-8", newline="\n")
