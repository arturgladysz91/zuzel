# REA-004A13 — Simple Rider Contact Recovery

Base: Draft #82, `67f1f7ab61df401de38e7335398c7eec14628b90`. 2026-10-16.
**Recommendation C — reject this implementation as the target contact model.**
Driver recovery works in the three required fixtures, but continuous contact still
needs hundreds of retries and millions of geometry evaluations. The implementation
grew rather than becoming simpler. Stop this solver experiment here.

1. **Why #82 stopped.** Its first scalar impact did not remove the imported lateral
   closure/yaw or the inward driver intent. A normal reaction at one instantaneous
   closest feature does not exactly constrain the subsequent rotating capsules.
   Separately, #55's generic Lipschitz bound charges both rotations even when their
   relative shape changes slowly, repeatedly searches an already owned onset and
   exhausts its 4096-evaluation budget. These are different problems. At the original
   5/10 ms refusals, sampled gaps were positive, approximately +0.988/+1.014 µm;
   lower bounds -11.990/-103.806 µm proved **uncertainty**, not observed penetration.
   The original later 20 ms run sampled -134.408 µm: actual unacceptable penetration.
   A13's extra oblique `encroach` fixture still refuses at 0.218828/0.221094 s:
   final rejected samples -0.963/-0.934 µm remain inside tolerance, but conservative
   floors -1.004/-1.100 µm do not. It is not classified as Crash or counted as success.

2. **Impact versus driver.** One fresh episode applies the existing zero-restitution
   relative contact-point normal impulse, yaw change and #56 reserve/severity once.
   No total-speed impact proxy, occurrence RNG or wear transaction was added.
   `RiderContactRecovery` stores onset, reaction time, end, target and throttle.
   Conscious response waits the existing 0.20–0.28 s reaction proxy and the next
   100 ms command tick. Impairment is immediate and fades over its existing deadline;
   imported loss/deadlines are preserved. New behavior lasts a provisional 0.5 s.
   The outer participant requests reachable free space; the other can hold its
   current line and roll off. Space, nearby bikes, speed, grip, technique, condition,
   current yaw and impairment bound that request. Tyre/yaw limits execute it through
   acceleration; no position or heading is assigned by recovery. Existing visual
   threat behavior resumes afterward. These reaction assumptions are uncalibrated.

3. **Three required scenarios.** Same #55 chassis/handlebar capsules and mechanics;
   START_STRAIGHT / FIRST_BEND / NORMAL_RACING are fixture contexts only.

   | Scenario | 5 / 10 ms first touch | Episodes / consequence transactions | Result |
   | --- | ---: | ---: | --- |
   | Light straight, similar speeds | 0.033326 / 0.033346 s | 1 / 1 | Brush, both race for 2 s; impulse 5.15 / 5.14 Ns |
   | Inner rider encroaches in bend | 0.003707 / 0.003707 s | 1 / 1 | Disturbed, both recover space and race for 2 s |
   | Strong closure, little outer room, low grip | 0.005042 / 0.005042 s | 1 / 1 | Existing severity classifies both Crash; physical interruption at impact |

4. **Continuous touch and release.** Existing six pair flags retain ownership.
   Normal support acts only within the unchanged 1 µm skin and against closure;
   tangential sliding is free. It never refreshes recovery or consequences. Release
   requires an actual accepted separation above the unchanged 0.1 mm hysteresis,
   with an interior release witness retained before a sampled return. There is no
   timer cooldown. A targeted consumed-pair test distinguishes 0.05 mm nonrelease
   from a 1 mm release/recontact, including absence of force through free space.

5. **Geometry and energy.** Fresh onsets still use #55; unresolved fresh detection
   refuses. All six actual quadratic paths must satisfy the same -1 µm floor.
   A small PoC-only separating-axis bound handles continuous touch: each projected
   relative endpoint coordinate has interpolation error at most `M*h²/8`, where
   `M = |n·relativeAcceleration| + sum(R*(|angularAcceleration|+maxAngularSpeed²))`.
   Minima over four endpoint combinations and four capsule pairs give a conservative
   floor. Relative common transport cancels. Uncertain intervals get at most 128
   local subdivisions, then the existing six step halvings; no accepted history is
   replayed. Random rotating-capsule samples and a hidden between-endpoint crossing
   test challenge the bound. There are no new cursors or production #55 changes.
   Impulses check full kinetic energy; support-force candidates also cannot exceed
   the same free-force kinetic prediction beyond numerical roundoff. Real maximum
   impulse energy gain is 1.46e-11 J; maximum checked endpoint penetration 0.4953 µm.
   Endpoint sampling alone does not establish safety: the whole-window floor gates
   admission. Numerical refusal never manufactures a fall.

6. **Step convergence.** After two seconds, light/bend position differences are
   1.284 / 13.299 mm; first-touch differences 19.302 / 0 µs. Episode counts and severity
   agree. Strong-case fall time agrees; its position difference is below 1e-11 m.
   The oblique regression remains incomplete at both steps. No universal convergence
   or full-heat stability is claimed. See [all ten comparisons](simple-rider-contact-recovery-convergence.csv).

7. **Actual control/19/C/heat91.** Import retains #82's real positions, headings,
   post-impact speeds and impairment at 4.180639183848264 s, including its documented
   inferred velocity/yaw and native-clock initialization approximations. R3–R4's
   impact is not reapplied. Both react, then a new R1–R3 impact physically interrupts:
   5 ms at **5.196936639 s** (J=153.381 Ns), 10 ms at **5.199483788 s** (152.075 Ns).
   Existing disturbance/reserve classifies R1 and R3 Crash, with energy losses
   226.09 / 222.67 J. No fallen-body path is invented: simulation stops at that solved
   impact state, without zeroing velocities or moving footprints. Thus **not two
   elapsed seconds**: 1.016297 / 1.018845 s, followed by a physical interruption,
   not a geometric refusal. At a shared +0.8 s, position difference is 4.671 mm;
   terminal times differ 2.547 ms. One imported plus one fresh episode; one new pair
   consequence transaction affects two riders. No repeated R3–R4 consequence.

8. **Release cost.** Windows/.NET 8, tiered compilation off equally for both versions,
   one warmup and median of three fresh runs; capture and reflection construction
   outside timers, no diagnostic observers inside. Exact preserved #82 Release DLL
   SHA256: `CDBF9171BAC09048207D713C61AE55EBB588D5BABCBB7D821AA7EFB81AD6AAF8`.
   Its preserved runtime source blob matches #82 (`139fc82319e77c9aecaf92c843baef16e4dff9ed`).

   | Same scenario / executed horizon | #82 5 / 10 ms | A13 5 / 10 ms |
   | --- | ---: | ---: |
   | Light straight, 2 s | 34.345 / 23.391 ms | 3.630 / 1.908 ms |
   | Bend contact, 2 s | 304.898 / 318.906 ms | 221.867 / 251.320 ms |
   | Real pair, **both execute +20 ms** | 25.392 / 8.385 ms | **160.870 / 166.833 ms** |

   Real +20 ms requires 1464/1728 physics evaluations, 232078/221132 exact separation
   evaluations, 75612/83552 projection bounds and 285/351 retries. #82 reports
   60/24 physics, 75264/24704 detector narrow evaluations and 6/2 retries. A13 counts
   direct support/endpoints too; `observer_narrow` separately exposes detector-only
   work (110592/86016). Projection bounds are additional work, not hidden savings.
   To the new physical interruption, A13 costs **1755.050 / 2297.708 ms**, with
   **1096/1463 retries**, 3.842/5.186 million total exact separations and
   484080/589399 projection bounds. #82 refuses much earlier, so full-attempt times
   cannot be compared as speedups. Strong-case times also have different horizons.
   [44 measurement rows, including counters, allocations and refusals](simple-rider-contact-recovery-measurements.csv).

9. **Simplicity and decision.** Runtime grows from **432 to 597 lines**, scenario
   adapters 86→124, runner 112→117, tests 107→205. Delayed driver behavior is useful,
   but this rotating-contact constraint/search combination remains too expensive
   and still refuses an oblique case. **C** for adoption; do not enlarge tolerances,
   tune specifically for control/19 or start another large solver rewrite here.

Validation: seven targeted Release cases pass, including actual physical interruption,
not a claimed two-second finish. Exactly **one final local solution Release build**,
zero warnings/errors; development builds used Debug. Standard Draft Windows CI;
no historical matrix/audit. Production CoreSim, A/B/C and calibrations are unchanged.

Reproduce after the solution Release build: run the targeted `TimeStepPhysicsTests`
with `--no-build`; preserve the #82 Release DLL before building A13, then set
`DOTNET_TieredCompilation=0` and invoke the tool with `output.json baseline/CoreSim.Tests.dll`.
`--simple` and `--probe` print the isolated or real outcomes without timings.
