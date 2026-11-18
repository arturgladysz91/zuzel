"""Isolate the new contact dispatch in the frozen #172 Box2D harness."""
from pathlib import Path
import sys

tool = Path(__file__).resolve().parent
original = tool.parent / "three-lap-global-planning/NativeWorld172.Generated.cs"
target = tool / "HandlingWorld.Generated.cs"


def replace(data, a, b):
    if data.count(a) != 1:
        raise ValueError("Frozen runner layout changed: "+a)
    return data.replace(a,b)


def generate():
    s = original.read_text(encoding="utf-8").replace("NativeWorld172","HandlingWorld")
    s = replace(s,"public sealed class HandlingWorld", "internal sealed class HandlingWorld")
    s = replace(s,"using SharedWorldSpeedway;","using SharedWorldSpeedway;\nusing SpeedwayHandling;")
    s = replace(s,"    RiderView[] views;", """    readonly HandlingRig? handling;
    RiderView[] views;""")
    s = replace(s,"double postSeconds=0){", """double postSeconds=0, HandlingRig? handling=null){
        this.handling=handling;""")
    s = replace(s,"        specs=gates.Select", """        if(handling is not null && (gates.Length!=1 || separated || observeBeyondBeta || !safetyHalfMeter))
            throw new ArgumentException("SpeedwayHandling is a single-rider experiment with the full safety envelope.");
        specs=gates.Select""")
    s = replace(s,"=>separated?Contact149.Forces", """=>handling is not null
            ?handling.Prepare(v,spec,frame,surface,p,time,mass,inertia!.Value,wheel,state,seconds,
                steeringOverride!.Value,powertrain!,releaseAt)
            :separated?Contact149.Forces""")
    s = replace(s,"            for(int i=0;i<specs.Length;i++){wheels[i].Commit();steering[i].Commit();shafts[i].Commit();}", """            for(int i=0;i<specs.Length;i++){
                if(handling is null){wheels[i].Commit();steering[i].Commit();}
                else{
                    wheels[i]=Speed148.ForecastFork148.Wheel(powers[i].Wheel.AfterOmega,wheels[i].Parameters);
                    steering[i]=new TwoContactBikeState(forces[i].SteeringAfter);
                    handling.Commit(time+dt);
                }
                shafts[i].Commit();
                if(wheels[i].AngularVelocityRadPerSecond!=powers[i].Wheel.AfterOmega ||
                    steering[i].SteeringRadians!=forces[i].SteeringAfter ||
                    shafts[i].Omega!=powers[i].AfterOmega)
                    throw new InvalidOperationException("Uncommitted handling step.");
            }""")
    return replace(s,"wheels[i].Prepared.LongitudinalLossJoules<-.001||wheels[i].Prepared.RearUtilization",
                   "power.Wheel.LongitudinalLossJoules<-.001||power.Wheel.RearUtilization")


data = "// Generated from #172 by generate.py. One native step and the original safety gates.\n" + generate()
if "--check" in sys.argv:
    assert data == target.read_text(encoding="utf-8")
else:
    target.write_text(data,encoding="utf-8",newline="\n")
