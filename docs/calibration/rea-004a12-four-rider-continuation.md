# REA-004A12 — bounded four-rider continuation and a real geometry barrier

Base: Draft #80, `experiment/rea-004a11-four-rider-causal-coordinator`,
`387d660f2f8b84b350f2f889161daaa2223c3996`. Evidence date: 2026-10-15.

**The real control/19/C/heat91 does not achieve the requested 1–2 second
continuation.** Its last safe frontier is **4.1836476179597195 s**, about 3.009 ms
after the first R3–R4 contact at **4.1806383146820165 s**. All four exact event
cursors reopen and execute new native forecasts; only R3 reaches another native
accepted endpoint before the geometry barrier. No claim of four accepted rider
states at 6.18 s is made.

The bounded mechanism works in a separate physical Disturbed fixture: all four
riders continue more than one second. Another fixture identifies a subsequent
R1–R2 contact and stops before any second consequence transaction. Ordinary
A/B/C, default options, equations, dimensions, calibrations and old results are
unchanged. There is no integrator rewrite, general pileup solver or full heat.

## Retained execution, clocks and recovery

The existing coordinator keeps its four accepted cursor tokens after a
nonterminal first event. `ContinueAfterFirstContact(throughSeconds)` invokes
the existing `ResumeAfterContact` event transition with a causal-only zero-step
pause. It replaces the participant's endpoint speed once and retains the
classified integration context, intent, native accumulator and prefix. The
other two riders use the same event partition without a consequence. Disposable
post-event forks retain the active contact controls. Crash remains terminal.

Native clocks stay at their actual endpoints; they never rewind to the earlier
geometric touch. A11's final common coverage ends at **4.180639154468221 s**.
The short already executed tails to the other three native endpoints are retained
and clipped into the next window. History starts at its previous coverage end,
not at the touch a second time. No tail creates another step, distance or wear.
The existing frozen journal extends each retained prefix through the same atomic
four-state/history/ownership transaction.

R3: **21.974556 → 21.878466 m/s** at the impulse, then **21.871866 m/s** at its
accepted TurnEntry end, **4.1836476179597195 s**. Its drive/lateral availability
is **0.8373502755748274** during that remaining traversal. Its completion consumes
recovery; the new TurnMiddle decision is taken only afterwards.

R4: **23.01685 → 23.105322 m/s** at the impulse. Its last accepted native clock
remains **4.180639154468221 s** with pending active recovery and availability
**0.9088494219165346**. Its new physical forecast uses those actual post-impact
values. R1/R2 retain their event speeds **19.754972 / 21.43645 m/s**, receive no
recovery and execute their captured segment controls.

This preserves #79's existing causal recovery semantics: the remaining current
production traversal is the active recovery step. Native metre-step pauses do
not consume, renew or compound that recovery. The next segment clears it.
This explicit causal rule does not alter ordinary #56C2 following-step recovery.

The local event agreement stays **10 microseconds**. The unchanged first event
still has maximum native/touch difference **0.8691662474 microseconds**, center
displacement **19.34928210 micrometres** and footprint displacement bound
**20.26014099 micrometres**. Neither #55's 100 ns root tolerance nor its 1 micrometre
contact/penetration bound is widened.

## The concrete real-run barrier

The next proposed window is **[4.1836476179597195, 4.213195855919761] s**.
R3 is now in TurnMiddle, remaining at lateral position 1; R4 is still in
TurnEntry, moving inward from 1.3531259 toward 1 under its retained intent.
The existing scalar impulse and partial authority loss do not stop that closure.

The actual integrated forecast gives R3–R4 sampled separation
**-0.03594021364363538 m**, with lower bound **-0.03604018113418417 m**.
The sampled penetration alone exceeds the unchanged bound by orders of magnitude;
this is not a demand for another nanosecond of root refinement. The whole-window
certificate also cannot establish nonpenetration. The failed poses/certificate
are returned as `GeometryBarrier`, explicitly unaccepted.

The publication and journal stop at the preceding safe frontier: **269 steps**,
**252.87654902651488 m**, one original consequence plan and the consumed generation
0. The unsafe interval contributes zero accepted distance/wear. No fresh contact
is declared in this real run: it is the continuing original R3–R4 interaction.
Consumed ownership suppresses a second impulse, never the geometry guard.

The smallest missing capability is a bounded nonpenetration response for the
still-executing R4 against R3's retained trajectory. The current #56C1 scalar-speed
impulse/#56C2 impairment does not enforce that constraint. Changing R4's current
steering would contradict this task's retained-control constraint; silently adding
a lateral impulse, clamp or separation teleport would change the physical model.
None is introduced. Increasing time tolerance cannot repair centimetre overlap.

## Positive continuation and next-event evidence

The simple rolling fixture uses ordinary straight geometry, unchanged physical
parameters and held lines. R3/R4 start in independently executing segments so
their captured drive remains positive. Actual rear closing causes **Disturbed**
at **0.4053851896209031 s**. All four continue to common frontier
**1.5105390943768948 s**; even the oldest native endpoint has advanced more than
one second. Both impacted riders reach **18.609571 m/s**, retaining their original
control with drive/lateral availability **0.9395853076190868**.

Recovery has a measured physical effect: their first post-event native step ends
at **16.574745 m/s**. The existing scalar primitive with identical impulse, actual
distance, surface, setup and drive, but recovery removed, gives **16.585487 m/s**.
No accepted prefix is replayed for that scalar counterfactual. Native step-entry
observations separately verify all four exact start clocks, distances, impulse
speeds and the actual drive/lateral multipliers passed into production dynamics.

A second rolling fixture preserves close, unreleased R3/R4 ownership while both
complete their current traversals and clear recovery. It detects the next fresh
**R1–R2 contact at 3.5865327806357543 s**. Four freshly solved partial trial paths
and all six pair certificates establish that time. `NextContactPending` retains
the prior safe publication at **3.5702055758102387 s**. No second endpoint states,
effects or wear are accepted. Its differently clocked later trial endpoints are
not claimed to be an admissible second event transaction.

Every case is repeated with reversed rider collection/processing order and exact
publication, ownership, next-event/barrier equality. Certified history is continuous,
the original plan remains the same object, ledger totals agree with prefix deltas,
R1/R2 receive no first-event effects, and weather updates remain zero. The existing
`PhysicalPairObservation` advances ownership only after accepted actual coverage:
resolved clearance above 0.65 m for 0.45 s, with no added cooldown.

## Measured work

One representative Windows/.NET 8 Release run, with counters enabled, excluding
initial fixture capture and assertions. Times are machine/JIT dependent; A11's
previous 1.040486 s is not comparable as a speedup benchmark. Its first-event
physics and geometry operation counts remain exactly unchanged here.

| Work | Start → first contact | Real continuation → refusal | Simple +1.105 s | Second fixture: continuation → next contact |
| --- | ---: | ---: | ---: | ---: |
| Wall seconds | 0.2415429 | 0.0069319 | 0.0274473 | 0.0849813 |
| Coupled evaluations, including planning | 161788 | 6158 | 168 | 1427 |
| Physical evaluations, including planning | 824 | 32 | 0 | 5 |
| All-six certificates | 265 | 2 | 60 | 190 |
| Pair certificates / pair time trials | 11 / 11 | 0 / 0 | 0 / 0 | 12 / 12 |
| Individual time trials | 24 | 0 | 0 | 26 |
| Coordinator narrow evaluations | 39211 | 386 | 2120 | 10194 |
| All narrow evaluations including planning | 52352 | 386 | 2120 | 10194 |
| Observed native step starts | 292 | 5 | 84 | 218 |
| Reexecuted accepted step starts | 0 | 0 | 0 | 0 |

The replay count is now instrumentation, replacing A11's constant-returning
property. Each actual causal integrator step entry reports its origin and native
progress; the meter compares it with that origin's already published boundary.
It counts forecast/trial work as well as accepted work, but not unrelated #54
projections as accepted-prefix replay. A deliberate old-token native execution
positive control records **one observed start and one replay**. The table excludes
that isolated meter control. No new optimization algorithm is added.

## Validation and remaining scope

Three new targeted C# facts cover the real refusal, positive recovery continuation,
the next event, reversal, prefix/time/distance continuity and replay instrumentation.
**157 targeted C# cases pass** across the coordinator, frozen heat, pair execution,
generation and consequence suites. **22 extraction tests pass**, restoring exact
#80 before the unchanged earlier extractions and the original 39 consumer hashes.
One final local Release build has **0 warnings / 0 errors**; the three new facts
also pass from that build. One standard Draft Windows CI is required for this
commit; no Ubuntu, historical audit matrix or merge is requested.

The immediate real-run blocker is the centimetre-scale R3–R4 closure above.
Further work for a complete heat still includes second-event consequences,
simultaneous groups, terminal/crash lifecycle, unsupported narrowing/closed-lap
seams, race completion and final accepted wear/weather settlement. Those are
outside this PR; the real 6.18 s objective remains explicitly unmet.
