# Autonomous Traffic #191

Local, default-OFF run over the existing H25 handling and ContactAdapter.
The generated sources bind the #190 FAST50 scheduler and steering/gas
controller to one shared four-rider world. Each rider receives its own
rig, drivetrain, command queue, and decision memory. Other riders are
observed through a common accepted snapshot containing no pending commands.

Motion191 fits a quintic to a continuous offset goal and carries offset,
slope and second derivative across replans. RaceMind191 evaluates local
alternatives with H25 calls and observed rival motion. FAST50 keeps the
50 ms review and activation deadlines. A safety decision submits a new
request through the same queue. Previously issued requests stay irrevocable.

From the repository root, after preparing #190:

    python tools/autonomous-traffic191/prepare.py
    dotnet restore tools/autonomous-traffic191/autonomous-traffic191.csproj --locked-mode
    dotnet build tools/autonomous-traffic191/autonomous-traffic191.csproj -c Release --no-restore --warnaserror
    dotnet tools/autonomous-traffic191/bin/Release/net8.0/autonomous-traffic191.dll --study-191 check <new-evidence-dir>

Use forecast ARM DIR for the no-contact rollout without Box2D. Native arms in order
are base, dynamic and alternate. Each requires the check output. The third is
enabled only after a complete dynamic race. It permutes the starting gears.
The executable records all accepted states, force/energy data, decisions,
contacts, and reservation hashes.

The baseline is H25/FAST50 with the original lines and an observed-gap throttle
guard. It serves as an integration reference because earlier H25/FAST50 runs
were solo. The dynamic planner supports local continuous changes and has no
dependency on the #190 catalog. Candidate rival forecasts and track-edge
filters are estimates. The native certificate uses the full mechanical
footprint. Production entry points and existing study artifacts stay separate.


## Exit planning #193

The experiment is OFF in the #192 Stadium race command. Existing H25
parameters and the default controller are shared. The new control search uses
the accepted-state feed, the original command schedule and the H25 preview.
Only the native world scores actual completion and swept clearance.

    dotnet tools/autonomous-traffic191/bin/Release/net8.0/autonomous-traffic191.dll --study-193 audit <native-recording.json.gz> <new-audit.json>

The command checks the decision reconstruction and the prediction from replayed
activations without building another Box2D world. Native validation uses
'--study-193 native ARM DIR' with a shared evidence directory and four
reservations in this order: solo-stock, solo-exit, four-exit, alternate-exit.
The paired baselines for the latter two already exist in the #192 archive.
Use 'audit193.py' to derive the exit metrics and full-resolution windows.

In the stadium the #193 recordings are optional archive entries and show the
same native poses with optional course, body heading, goal and curvature.


## Wide straight #194

The portable stadium selects this default-OFF controller using
'{"Ratios":[10,9,8,9],"Controller":"wide194"}' with '--stadium-race'.
The existing 'base' arm and configurations without 'Controller' retain #192.

'WideTrack194' solves the whole-footprint straight corridor on the original
frame. 'SectorPath194' joins the approach, apex, exit and next approach with
continuous lateral derivatives. 'RaceMind194' rolls H25 forward from the
activation state, reserves space for observed rivals and tracks course, beta
and yaw through FAST50. The whole-sector geometric filter and the short H25
rollout are predictions, with the original native certificate at each step.

Use '--study-194 check OUTPUT.json' for geometry and schedule checks,
'--study-194 family ARCHIVE.json.gz OUTPUT.json' for the occupied/open H25
windows, and '--study-194 preview ARM OUTPUT.json.gz' for an offline run.
The study reserves four native worlds: two solo arms, a stadium-generated
nominal four-rider race, and the alternate four-rider arm. 'audit194.py'
converts accepted telemetry for the report. None of the previews use Box2D.

