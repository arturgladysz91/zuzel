# SpeedwayHandling (#181)

PROVISIONAL experiment, off by default. The executable runs one autonomous rider in the existing Box2D harness with an optional contact model. The production simulator and the K4 tools are separate entry points. The original runner is the source for `HandlingWorld.Generated.cs`.

The delayed steering request specifies a desired yaw rate through the nominal wheelbase. The handling actuator estimates the front lateral force needed to approach that rate and converts it to a steering angle. The original angular and rate limits apply to the front wheel. Box2D receives two forces at the original contact arms. Lean follows the requested lateral acceleration with a bounded rate and changes the lateral slip scale of each contact. Static axle loads remain in force.

The rear contact reserves lateral force first and uses the rest of the same circle to solve wheel torque and longitudinal slip. The gameplay capacity is **1.25×** the BASE capacity at the same surface and axle loads, equivalent to effective μ about **1.606** on the frozen track. This is a tuning assumption for the prototype. The original engine, shaft, clutch, wheel inertia, actuator limits, track and clearance rules are reused.

From the repository root on Windows with .NET 8:

```powershell
python tools/speedway-handling/generate.py --check
dotnet restore tools/speedway-handling/speedway-handling.csproj --locked-mode
dotnet build tools/speedway-handling/speedway-handling.csproj -c Release --no-restore --warnaserror

$dll = "tools/speedway-handling/bin/Release/net8.0/speedway-handling.dll"
$evidence = "../handling-reproduction"
dotnet $dll --speedway-handling check
dotnet $dll --speedway-handling native $evidence A1 full-straight
dotnet $dll --speedway-handling native $evidence B2 V2
dotnet $dll --speedway-handling native $evidence B3 V3
python tools/speedway-handling/audit.py $evidence
```

Use a fresh evidence directory for reproductions. The short native test starts on the full 62 m straight from rest. V2 and V3 use the same standing start conditions as the corresponding archived pairs from #177. The second stage requires a legal first-stage run with the same configuration and source hashes. The audit uses NumPy and verifies the original pending and safety gates, accepted mechanical states, force budgets, impulses, work and finish crossings.
