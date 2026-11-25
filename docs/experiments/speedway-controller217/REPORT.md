
# Study #217 · Corner controller audit

The baseline is the published #216 head
`f7888f6fe00b35d6b8134309401d18f0428bba8f`.
This study runs in MODEL through the shared #216 step and track
checks. The #216 experiment still selects its original controller.
H25 remains the application default.

## Controller restrictions and model limits

| Control216 behavior | Classification and isolated diagnostic |
|---|---|
| Force Drive to zero before entry +2 m when previewed centripetal demand exceeds 10.5 m/s² | Controller policy. The comparison removes this one write and leaves the selected Drive, exit boost and drivetrain intact. The value is a spatially tapered demand from v²/r. The branch reads neither measured tyre utilization nor available front authority. |
| Add 0.35 Drive after exit −8 m and clip Drive at 1 | The addition is policy and has its own ablation. Drive ∈ [0,1] is the physical model's command domain. |
| Clamp lean-equivalent acceleration to [−10.5, +11.8] m/s² before atan(a/g) | Controller policy. The ablation keeps atan and permits the calculated demand through. The integrated orientation and the model's roll and chassis rejection still apply. |
| Clamp requested steering to ±0.37 rad | Additional controller restriction. The ablation sends the raw request into the original physical angle and rate limit. Telemetry distinguishes the request from the attained angle. |
| Roll proportional gains from the #216 settings, rate damping 5 and a linear 50 ms lead | Controller tuning. The ablations drop each term separately. The actual 50 ms activation delay stays in the simulator even when the lead term is removed. |
| Multiply desired roll acceleration by 20 kg·m² | A fixed inverse-control approximation. A separate variant uses the local body-x angular mobility. It perturbs world angular momentum along the bike's roll axis while holding pose, rotor momenta and relative momenta fixed. Its reciprocal gives the effective inertia in the mechanical state. |
| Use front-force inversion only above 40 N of front capacity, replace the signed lever by max(0.15 m, lever), and reserve 10% of front utilization | Three controller choices. The ablations use positive capacity, use the signed lever with a zero-denominator guard, and let the inverse use the full capacity, respectively. The combined-slip law still controls actual force. |
| Force the alignment denominator to at least 1 m/s, and add −0.036×roll + 0.034×roll feedback as the unloaded-front fallback | The denominator and the correction are separately ablated. A wheel with no ground contact still exerts zero tyre force. |
| Move the rider forward by a decreasing 8.5 cm schedule, laterally in proportion to target lean, and down by a tapered 2.5 cm | Prescribed command policy. The comparison holds the rider target already active at the fork. Passive pads, internal recoil, force and power saturation and movement dynamics continue. |
| Set left-foot demand to clamp(1.23×(\|target lean\|−0.24), 0, 0.75) | Prescribed command policy. The isolated comparison requests retraction through the same leg actuator. The boot's geometry, inertia, passive limits and ground contact continue. |
| 5 m curvature preview, 15 m minimum radius, entry and exit taper, 1.6 m sine-apex excursion, 11 m offset response, 1.8 heading-error gain and ±0.34 rad course-error cap | Trajectory and guidance heuristics. Individual comparisons remove each lookahead, floor, taper, lane excursion, offset correction, acceleration correction or cap. The ±0.01 m preview endpoint inset and the normalized phase fraction keep the sample within the selected bend. |

The model retains the physical steering range and slew rate from the
motorcycle configuration, the 700 N and 350 W shared muscle budget,
relative command travel limits, original engine and tyre laws, and
contact and chassis envelopes. The inherited 0.50 m swept-clearance
screen, including its 1.35 m projection allowance, is a safety gate.
The inherited ±π/4 sideslip and forward-progress checks are trajectory
eligibility conditions. None of these is ablated. The #216 grid budget,
slide duration and exit-roll criteria belong to the experiment and
its completion test.

## Matched diagnosis

The ten primary roots are archived #216 starts at 80, 90, 100, 110
and 120 km/h with and without the leg model. They share the
3.6 m lane, −0.24 rad starting bank, roll gain 15 and a planned
Drive of 1. Every active command and all physical coordinates are
deserialized directly. The baseline also captures the first decision
where its nonzero planned Drive gets cut and an earlier command is
still awaiting activation.
Each such replay begins from the exact same accepted mechanical state
with all previous commands still in their original activation queue.
The first command at a matched state isolates the effect of the
tested rule. Later trajectory differences include the closed-loop
response to the changed state.

A separate exit witness rotates and translates the mechanically
balanced #216 circle to six metres before the exit. This observes
the exit boost from a loaded, moving state. It is a phase probe.

Requested Drive, the command eventually active in the drivetrain,
steering request, actual angle and rate, actual engine and ground
forces, and the terminal condition are tracked separately. Time in
the legacy entry-cut region is classified from the current midpoint
state, allowing a previously issued command to be active while a
new request is still pending.

## Force and trajectory preview controller

The new controller advances a copy of the full mechanical state
through its existing command queue to the instant a new command
can take effect. It samples reachable steering angles from the
physical angle limit and slew rate. One proposal estimates the
front lateral force needed to follow the projected lane, using
the geometry of a future track point, the actual front capacity,
and the rear, boot and drag forces in the forecast. That estimate
proposes an angle. The coupled model determines actual forces.

The candidate Drive set covers the normalized range, with the active
Drive also eligible. The two best primary steering/Drive candidates
get additional rider-position and boot trials. Rider proposals
include offsets scaled by attainable displacement under the shared
force budget. Boot proposals include retraction and the first
geometric reach of the ground along the articulated leg path.
Candidates still undergo the coupled support and actuator solve.
A leg with a positive gap produces no ground force.

Each candidate advances a 0.24 s trajectory after activation.
The score combines along-track progress, lateral and course error,
clearance reserve, roll-rate drift and front unloaded time. There
is no target lean clamp or entry Drive switch. The decision checks
the best three eligible proposals at the execution timestep from
the original state and queue. If none reaches the horizon inside
the unchanged limits, the diagnostic records an abstention. This
finite preview observes local reachability and does not establish
recursive feasibility for an entire corner.

Only the dedicated audit command can invoke the new controller.
All its outputs and the one-rule ablations are diagnostic. The
official qualification workflow contains neither.

## Entry cut witness

In the paired 120 km/h wheels-only entry snapshot, the bike was
1.67 m before the entry and the legacy centripetal demand was
16.01 m/s². The active Drive was already 1. The baseline
requested 0 and the one-rule ablation requested 1, with identical
steering and rider targets. The active Drive stayed at 1 for the
first 50 ms under the baseline because the cut was still in its
activation queue. In the current-state region where the rule would
request a cut, full Drive lasted 0.050 s with the baseline and
0.110 s with the ablation. The predictive controller requested 1
from the same snapshot and recorded 0.111 s of active full Drive
in that region.

The rule suppresses a mechanically admissible Drive request.
The paired continuations still ended before the exit. Their
terminal causes distinguish the course envelope, the frame's
ground envelope and the predictive controller's refusal of an
uncertified continuation. The test leaves the feasible full-corner
high-Drive envelope open.

## Matched outcomes

The audit ran 504 rollouts from 21 matched states, with the
baseline, 22 single-factor ablations and the predictive policy
at each state. Each of the ten queued entry forks returned the
same first-decision result. Baseline requested 0, removal of
the entry cut requested 1, and the predictive policy verified
a continuation starting with a full-Drive request. The executed
full-Drive interval inside the legacy gate region rose from
0.050 s to 0.0925–0.1275 s in the one-rule ablations.

Full-Drive time over the surviving bend segment increased at
five of the ten original starts and decreased at the other five.
At 100 km/h with wheels only it rose from 0.249 to 0.937 s and
reference progress rose from 9.78 to 22.52 m. At 80 km/h with
the leg model, removing the cut shortened the surviving
prefix and reduced the total full-Drive time. At 80 km/h with
wheels only, the cut ablation produced the longest full-Drive
interval in the audit at 1.162 s. The original accepted-state
track screen remained in force in each pair.

| Isolated change | Measured result |
|---|---|
| Remove the lean-demand clamp | Changed at least one command at 20 of 21 states. Median progress across the ten original starts fell by 12.82 m. |
| Remove the extra steering clamp | Changed requests at 16 of 21 states. Actual steering reached the configured 0.611 rad stop. Median progress across original starts changed by less than 1 cm. |
| Remove roll proportional action, damping or latency lead | Each changed commands at all 21 states. Median progress changed by −2.81, −14.34 and −1.06 m, respectively. |
| Substitute mechanical roll inertia | The first-decision values were 12.00–12.09 kg·m². Commands changed at all 21 states. Median progress changed by −0.59 m. |
| Hold the original rider target | Changed commands at all 21 states. Median progress changed by +0.10 m. |
| Retract the boot through its actuator | Changed commands in all ten states with the leg model. |
| Remove the exit boost | The exit witness requested 0.66 instead of 1 at its first decision. Both arms crossed the nearby exit. |
| Remove the front authority threshold or the utilization reserve | Changed commands at 3 and 21 states, respectively. |
| Remove the signed-lever floor, radius floor, alignment speed floor or course error cap | These four changes never altered a command on the tested states. |

A decision is counted as changed when its request differs from
the legacy law evaluated at that same current state. The full
outcome archive includes the guidance, fallback and scheduled
posture comparisons as well as these selected rows.

The predictive policy evaluated 10,368 proposals and performed
433 execution-step checks. The smallest swept margin in an
issued command's verified prediction was 0.5055 m. It abstained
on all 20 entrance and gate rollouts when the finite candidate
set yielded no verified continuation. Its median progress on
the ten original starts was 6.09 m shorter than the legacy
controller's. It reached the exit in the already near-exit
witness. Complete-corner performance remains unestablished.

No rollout beginning at an entrance or a legacy entry-cut
snapshot reached the exit. The greatest post-entry distance
was 24.85 m of the 97.39 m reference bend. All 24 recorded
exit crossings belong to the intentionally near-exit witness.
No result was submitted to official qualification.

[Matched initial states and activation queues](../../../data/experiments/speedway217/seeds.json) ·
[All 504 diagnostic outcomes](../../../data/experiments/speedway217/outcomes.json) ·
[Legacy 120 km/h trace](../../../data/experiments/speedway217/trace-120-wheels-gate-Baseline.json.gz) ·
[120 km/h trace with the entry cut removed](../../../data/experiments/speedway217/trace-120-wheels-gate-NoEntryDriveCut.json.gz) ·
[Predictive 120 km/h trace](../../../data/experiments/speedway217/trace-120-wheels-gate-Predictive.json.gz)

## Verification

The independent verifier checked all 504 initial-state and
physical-limit receipts and 8,022 steps in the detailed traces.
It reconstructed the active command from the activation queue
and recomputed the linear and angular impulse from both wheels,
drag, gravity and the boot. Two processes replayed the audit
with different thread caps. The legacy #216 entry point
reproduced all 59 frozen artifacts byte for byte.

The Release build treated warnings as errors. All 27 focused
mechanics tests passed. Both full guarded core runs had
2,093 passes out of 2,314 cases. The inherited 221 failures
match by test and error. They include 219 native construction
guard cases and two frozen-source assertions. Zero native
worlds were created.
[Independent checks](../../../data/experiments/speedway217/validation.json) ·
[Determinism](../../../data/experiments/speedway217/determinism.json) ·
[Baseline replay](../../../data/experiments/speedway217/baseline-replay.json) ·
[Core regression](../../../data/experiments/speedway217/regression.json)

~~~powershell
$env:SPEEDWAY_BOX2D_NO_WORLDS='1'
dotnet build tools/speedway-h26/H26.csproj -c Release -m:1
dotnet run --project tools/speedway-h26/H26.csproj -c Release --no-build -- controller-217 . out217
python tools/speedway-h26/verify217.py out217 out217/validation.json
~~~
