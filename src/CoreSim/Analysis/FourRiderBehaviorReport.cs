using System.Globalization;
using System.Text;

namespace CoreSim.Analysis;

public static class FourRiderBehaviorReport
{
    public const string MarkdownFileName = "four-rider-race-behavior-audit.md";
    public const string EvidenceFileName = "four-rider-race-behavior-evidence.json";

    public static string RenderEvidence(BehaviorAuditResult result) => FourRiderBehaviorEvidence.Render(result);

    public static string Render(BehaviorAuditResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var text = new StringBuilder();
        void Line(string value = "") => text.Append(value).Append('\n');
        Line("# Four-rider production race behavior audit");
        Line();
        Line($"Baseline: `{FourRiderBehaviorSuite.BaselineSha}` (merged #50). Analysis only; no production tuning.");
        Line("Frozen method: [protocol and model inventory](four-rider-race-behavior-protocol.md). Typed evidence: [JSON appendix](four-rider-race-behavior-evidence.json).");
        Line();
        text.Append(ExecutiveSummary).Append('\n');
        Line();
        text.Append(ScenarioMatrix).Append('\n');
        Line();
        Line("## Measured scenario outcomes");
        Line();
        Line("Every main case: actual AdaptiveDecisionModel → HeatSimulator → SimulationEngine, four laps, seeds 0..31, incidents 1, all calibration adjustments null. Counts below are totals over 32 heats; initial ties are excluded from established-pass totals. R2/R1 is a focal diagnostic, not a required winner.");
        Line();
        Line("| case | initial order (seed 0) | final order (seed 0) | established pair inversions / tied inversions / production events | R2 passes R1 (heats) | R2 ahead of R1 at finish (both finish) | contacts / LostRhythm / crashes | first-boundary event mismatch | traffic changes target | mean winner time s |");
        Line("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var scenario in result.Scenarios)
        {
            var runs = result.Runs.Where(item => item.ScenarioId == scenario.Id).ToArray();
            var first = runs[0];
            var wins = runs.Count(item => BothFinish(item) && Finish(item, 2).Position < Finish(item, 1).Position);
            Line($"| {scenario.Id} | {Order(first.InitialOrder)} | {Order(first.FinalOrder)} | {N(runs.Sum(item => item.BoundaryPasses.Count(pass => !pass.PreviouslyTied)))}/{N(runs.Sum(item => item.BoundaryPasses.Count(pass => pass.PreviouslyTied)))}/{N(runs.Sum(item => item.ProductionOvertakes.Count))} | {N(runs.Count(item => item.BoundaryPasses.Any(pass => pass.RiderId == 2 && pass.PassedRiderId == 1 && !pass.PreviouslyTied)))} | {N(wins)}/{N(runs.Count(BothFinish))} | {N(runs.Sum(item => item.Contacts.Count))}/{N(runs.Sum(item => item.LostRhythm))}/{N(runs.Sum(item => item.Crashes))} | {N(runs.Sum(item => item.FirstBoundaryEventMismatchCount))} | {N(runs.Sum(item => item.Choices.Count(choice => choice.TargetLane != choice.NoTrafficTargetLane)))} | {F(runs.Average(item => item.Classification.First(entry => entry.Position == 1).TimeSeconds))} |");
        }
        Line();
        Line("Crash total includes constraint outcomes, random surface incidents and contact crashes; contact-crash total is contacts minus LostRhythm. Typed Resolve outcomes do not distinguish a constraint crash from a random-incident crash, so non-contact totals are not attributed to RNG alone. A first-boundary mismatch is the symmetric difference of production events and true-initial-order inversions. It does not accuse normal starting-grid events of being false passes.");
        Line("Two causes must stay separate: rolling B/H/J start from a different true order than the production lane/id grid; I instead exposes the net-rank-gainer event filter (some pair inversions are missed during multi-rider reshuffles). Neither count is a continuous manoeuvre count.");
        Line();
        Line("## Focal execution and geometry — seed 0");
        Line();
        Line("| case | R2 targets through first bend | R2 planned lanes | R2 exit lateral positions | R2 end speeds m/s | first R2→R1 established inversion (lap/segment/boundary) | R1/R2 first-bend path m | R1/R2 bend-exit speed m/s |");
        Line("| --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var scenario in result.Scenarios)
        {
            var run = result.Runs.Single(item => item.ScenarioId == scenario.Id && item.Seed == 0);
            var bend = scenario.Track.CornerTopology.Corners[0];
            var steps = run.Steps.Where(item => item.RiderId == 2 && item.Lap == 1 && item.SegmentIndex <= bend.EndSegmentIndex).ToArray();
            var pass = run.BoundaryPasses.FirstOrDefault(item => item.RiderId == 2 && item.PassedRiderId == 1 && !item.PreviouslyTied);
            var firstPath = run.Steps.Where(item => item.RiderId == 1 && item.Lap == 1 && item.SegmentIndex >= bend.StartSegmentIndex && item.SegmentIndex <= bend.EndSegmentIndex).ToArray();
            var secondPath = steps.Where(item => item.SegmentIndex >= bend.StartSegmentIndex).ToArray();
            Line($"| {scenario.Id} | {string.Join(",", steps.Select(item => N(item.TargetLane)))} | {string.Join(",", steps.Select(item => N(item.PlannedLane)))} | {string.Join(",", steps.Select(item => F(item.ExitLateralPosition)))} | {string.Join(",", steps.Select(item => F(item.FinalSpeedMetersPerSecond)))} | {(pass is null ? "none" : $"{N(pass.Lap)}/{N(pass.SegmentIndex)}/{F(pass.ObservedBoundaryProgress)}")} | {F(firstPath.Sum(item => item.PathDistanceMeters))}/{F(secondPath.Sum(item => item.PathDistanceMeters))} | {LastSpeed(firstPath)}/{LastSpeed(secondPath)} |");
        }
        Line();
        Line("The boundary is only where a changed order was observed. No exact intra-segment crossing coordinate is inferred. Distances are integrated production traversal distances, not a replacement trajectory solver. H starts after the apex, so its apex observation is unavailable.");
        Line();
        Line("## Fixed-route projection sanity checks — separate, non-stochastic solo controls");
        Line();
        Line("Same rider/setup/surface/entry speed within each set. Fixed lanes 0..4, no incidents, no rivals, zero drying. Actual time integrates the whole first bend and following straight. Projected time transcribes the existing static route formula without perception noise or other costs. These are not optimized trajectories or main four-rider results.");
        Line();
        Line("| source | lane | projected s | production integrated s | bend path m | entry / actual apex / bend exit / straight exit m/s |");
        Line("| --- | --- | --- | --- | --- | --- |");
        foreach (var item in result.RouteProbes)
            Line($"| {item.ScenarioId} | {N(item.Lane)} | {F(item.ProjectedRouteTimeSeconds)} | {F(item.ActualBendAndStraightTimeSeconds)} | {F(item.BendDistanceMeters)} | {F(item.BendEntrySpeedMetersPerSecond)}/{(item.ActualApexSpeedMetersPerSecond is { } apex ? F(apex) : "n/a")}/{F(item.BendExitSpeedMetersPerSecond)}/{F(item.StraightExitSpeedMetersPerSecond)} |");
        Line();
        foreach (var group in result.RouteProbes.GroupBy(item => item.ScenarioId))
            Line($"- {group.Key}: projected fastest lane {N(group.MinBy(item => item.ProjectedRouteTimeSeconds)!.Lane)}, integrated fastest lane {N(group.MinBy(item => item.ActualBendAndStraightTimeSeconds)!.Lane)}.");
        Line();
        Line("H's three-arc projection is deliberately checked against a one-segment whole bend: it exposes topology sensitivity and must not be treated as an ordinary three-segment venue calibration.");
        Line();
        Line("## Targeted production mechanics controls — separate from main AI cases");
        Line();
        Line("These fixed-intent controls disable random incidents and have no eligible contacts. They diagnose mechanism capability, not how often the AI chooses the inputs. Equal-start uses identical six skills/setup, not the main I profiles; reference lanes are not physical A/B/C/D gate coordinates.");
        Line();
        Line("| control | rider | first-step entry→exit lateral | first-step arrival s | first-step path m | first-step traversal exit m/s | contacts in heat |");
        Line("| --- | --- | --- | --- | --- | --- | --- |");
        foreach (var controlItem in result.DiagnosticControls)
        foreach (var step in controlItem.Run.Steps.Where(item => item.Step == 0 && (controlItem.Id == "equal-start" || item.RiderId <= 2)))
            Line($"| {controlItem.Id} | {N(step.RiderId)} | {F(step.EntryLateralPosition)}→{F(step.ExitLateralPosition)} | {F(step.EndTimeSeconds)} | {F(step.PathDistanceMeters)} | {F(step.TraversalExitSpeedMetersPerSecond)} | {N(controlItem.Run.Contacts.Count)} |");
        Line();
        var starts = result.Runs.Where(item => item.ScenarioId == "I").ToArray();
        var firstCornerContacts = starts.SelectMany(item => item.Contacts).Where(item => item.Lap == 1 && item.SegmentIndex <= 3).ToArray();
        Line($"Main I first start/corner across 32 heats: contacts {N(firstCornerContacts.Length)}, LostRhythm {N(firstCornerContacts.Count(item => item.Type == SimulationEventType.ContactLostRhythm))}, contact crashes {N(firstCornerContacts.Count(item => item.Type == SimulationEventType.ContactCrash))}. Whole-heat counts above must not be presented as first-corner frequency.");
        Line();
        Line("## Six current production skills — matched 20/80 controls");
        Line();
        Line("Only rider 2's named skill changes, paired seeds 0..31; incidents disabled to isolate other effects (contacts still stochastic). Start uses I, Speed A, reading F. SlideControl/PairRiding use a fixed aligned contact probe; Adaptability a fixed inward target. These intent controls are separate from the main AI suite. Time averages below use only pairs where rider 2 finishes both versions; negative high-minus-low means high is faster. A changed time alone does not measure the full skill's quality.");
        Line();
        Line("| skill | paired finished heats | R2 mean high-minus-low time s | R2 contacts low/high | all heat contacts low/high | R2 target differences | R2 first-step lateral movement low/high |");
        Line("| --- | --- | --- | --- | --- | --- | --- |");
        foreach (var group in result.SkillPairs.GroupBy(item => item.Skill))
        {
            var finished = group.Where(item => Finish(item.Low, 2).Finished && Finish(item.High, 2).Finished).ToArray();
            var differences = group.Sum(pair => pair.Low.Choices.Join(pair.High.Choices,
                item => (item.Step, item.RiderId), item => (item.Step, item.RiderId),
                (low, high) => low.RiderId == 2 && low.TargetLane != high.TargetLane ? 1 : 0).Sum());
            float Movement(BehaviorRun run) { var step = run.Steps.First(item => item.RiderId == 2); return MathF.Abs(step.ExitLateralPosition - step.EntryLateralPosition); }
            Line($"| {group.Key} | {N(finished.Length)} | {(finished.Length == 0 ? "n/a" : F(finished.Average(item => Finish(item.High, 2).TimeSeconds - Finish(item.Low, 2).TimeSeconds)))} | {N(group.Sum(item => item.Low.Contacts.Count(contact => contact.RiderId == 2)))}/{N(group.Sum(item => item.High.Contacts.Count(contact => contact.RiderId == 2)))} | {N(group.Sum(item => item.Low.Contacts.Count))}/{N(group.Sum(item => item.High.Contacts.Count))} | {N(differences)} | {F(group.Average(item => Movement(item.Low)))}/{F(group.Average(item => Movement(item.High)))} |");
        }
        Line();
        Line("## Surface feedback — six repeated four-rider heats, 32 matched seeds");
        Line();
        Line("B inputs are reset each heat; surface is retained, not reset. No rain/drying; wear remains production. Fresh replays have the same seed/riders/options. Changed-choice counts compare only common (step,rider) observations: differential crashes can truncate a path and are not silently treated as choices. Histograms include all observed choices.");
        Line();
        Line("| heat | mean grip before→after | mean ruts before→after | changed cells (sum) | targets different from fresh | retained target 0/1/2/3/4 | fresh target 0/1/2/3/4 |");
        Line("| --- | --- | --- | --- | --- | --- | --- |");
        foreach (var item in result.WearHeats)
            Line($"| {N(item.Heat)} | {F(item.MeanGripBefore)}→{F(item.MeanGripAfter)} | {F(item.MeanRutsBefore)}→{F(item.MeanRutsAfter)} | {N(item.SurfaceCellChanges)} | {N(item.DecisionsDifferentFromFresh)} | {string.Join("/", item.TargetHistogram.Select(N))} | {string.Join("/", item.FreshTargetHistogram.Select(N))} |");
        Line();
        Line("## Separate single-seed non-stochastic control");
        Line();
        var control = result.NonStochasticControl;
        Line($"Fixed separated straight paths: initial/final {Order(control.InitialOrder)}/{Order(control.FinalOrder)}, contacts {N(control.Contacts.Count)}, inversions {N(control.BoundaryPasses.Count)}, crashes {N(control.Crashes)}. Seed independence is regression-tested; main Adaptive/contact cases are never classified as RNG-free.");
        Line();
        text.Append(Interpretation).Append('\n');
        Line();
        Line("## Reproduction and limits");
        Line();
        Line("Run `dotnet run --project src/Sandbox -c Release --no-build -- four-rider-race-behavior-report docs/calibration`. The writer emits only this Markdown and its JSON evidence; frozen protocol is maintained separately. UTF-8 without BOM, LF, invariant numeric formatting, no clock/generated timestamp or HEAD-dependent content. Hashes and exact-head CI are recorded in the Draft PR body to avoid self-referential hashes.");
        Line("This is a synthetic behavior audit, not empirical calibration or proof that any elapsed time/contact frequency matches real racing. Existing geometry, setup, incidents and surface assumptions remain provisional. Production files are unchanged; only analysis, tests and the Sandbox command are added.");
        Line($"Fractional-progress compatibility probe (B with initial 0.05 instead of 0.0625): `{result.FractionalProgressProbe}`. Float remainder rounding can leave canonical progress below the boundary tolerance. The accepted battle input is the exactly representable 0.0625 (3.75 m on 60 m), not a production repair; the rejected input remains recorded.");
        return text.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private const string ExecutiveSummary = """
        ## Executive summary

        **Verdict: C — core race behavior still structurally insufficient for credible four-rider battles.**
        This is not a rejection of the longitudinal/corner foundation or a request for motorcycle dynamics.
        The suite finds two blockers, not a list of missing realism features:

        1. **INTERACTION: no shared-time swept-space race resolution.** Faster riders catch and order changes are detected, but rivals mostly traverse independently. A separate equal-speed production control swaps two adjacent lines at identical arrival times without any contact or yielding. In A, a leader 15 m ahead at initial time zero still changes the follower's occupancy-based target. Endpoint time-gap contacts and overtake events are not overtaking mechanics.
        2. **DECISION: static safe-speed route cost can invert the actual production ranking.** C's outer line has higher exit speed, but its longer route is not compensated in the integrated bend-plus-straight traversal. The existing projection ranks that outer route first. Constant-lane refreshes do not constitute an entry/apex/exit or opening-exit plan.

        Positive evidence is substantial: A's faster follower passes in every seed; B recognizes an inner alternative; side-by-side riders can complete corners; F/G reverse line choice with reversed surface; geometry changes J; all six current skills have consumers; retained wear changes later decisions. None proves overlap, held space, defence or undercut response.

        Start/gate geometry is **not established as the next blocker**: I's first-corner contacts are limited in this seed set, and equal-skill fixed-line launch differences have an existing geometric explanation. Gate calibration remains unverified. Frozen entry radius/surface during lateral motion and incident scaling are important limitations, not independently established third blockers. Classification C follows the impossible shared-time crossing control and measured decision misranking, not the number of TODOs. No fix, tuning, Ready or merge is part of this PR.
        """;

    private const string ScenarioMatrix = """
        ## Scenario matrix

        Credibility here means support for the requested behavior, not empirical validation of lap times.
        Repeated BLOCKER labels refer to the same two mechanisms, not additional blockers.

        | scenario | observed behavior | credible? | primary issue class | severity |
        | --- | --- | --- | --- | --- |
        | A | Faster follower closes 15 m and passes; contact not necessary. Occupancy still reacts to the distant initial leader. | catch yes; spatial traffic no | INTERACTION | BLOCKER |
        | B | Inner target and bounded lateral execution; inside pair inversions occur. Initial-grid event artifact; no defended overlap. | partly | INTERACTION | BLOCKER |
        | C | Follower selects wide, gets higher exit speed; static projection prefers the slower integrated route. First pass is already on the straight, not proof of a bend exit pass. | decision ranking no | DECISION | BLOCKER |
        | D | Distinct continuous paths coexist through entry; later order can change. No forced first-entry contact. | supported at boundary scale; held space unproven | INTERACTION | ACCEPTABLE SIMPLIFICATION |
        | E | Occupancy changes target, generally to an adjacent alternative; seed 0 returns inward at exit. No wholesale extreme-lane escape. | yes locally; temporal occupancy approximate | DECISION | IMPORTANT |
        | F | Worn inside pushes targets outward; reading/style/control do not produce four distinct guaranteed choices. Many non-contact crashes. | surface direction yes; severity not calibrated | DATA/CALIBRATION | IMPORTANT |
        | G | Reverse surface makes riders target inner despite outside preference; no invariant outer bias. | yes in this fixture | DECISION | PASS |
        | H | Inner shorter, outer faster at exit; AI targets inner rather than explicitly planning an opening exit. Coarse whole-bend topology inflates static projection. | trade-off exists; dynamic battle unproven | DECISION | IMPORTANT |
        | I | Launch skill changes crossing times; first-corner order compresses/reorders without gate bonuses. Event tie/order artifacts must not be called passes. | useful foundation; physical gate claims unverified | START | ACCEPTABLE SIMPLIFICATION |
        | J-tight | Same riders choose/execute differently; shorter lap times and different pair outcomes. | geometry sensitivity yes | PHYSICS | PASS |
        | J-wide | Different width/radius/time budget changes lines, path costs and contacts; no artificial equalization. | geometry sensitivity yes | PHYSICS | PASS |
        """;

    private const string Interpretation = """
        ## Top blockers — two, both mechanism failures

        ### 1. INTERACTION — arrival-time sorting is not spatial racing

        Main A demonstrates speed-driven catching without needing contact, but its seed-0 follower selects target 2 versus no-traffic target 1 while its same-line leader is 15 m ahead. The occupancy predicate uses equal elapsed time and segment membership, not longitudinal separation. All subsequent riders are synchronized to topology boundaries despite different arrival times. The contact phase sees endpoint lateral positions and arrival-time differences, not coexistence on a common-time trajectory.

        The fixed crossing control strengthens this beyond code inspection: R1 goes 0→1, R2 1→0, on the same straight with identical speed, distance, arrival time and surface. Both full-segment longitudinal profiles are identical. Any continuous realization of these simultaneous lateral swaps crosses occupied space, yet the solver accepts both and registers zero contacts/yields because endpoints are separated. This proves unsupported contested-space mechanics; it does **not** estimate how often Adaptive chooses that manoeuvre. Main D succeeding on separated lines does not cure it.

        Production overtakes are order-change events, only emitted for net-rank gainers. They do not causally generate speed/passing. The audit independently counts all active pair inversions and marks previous ties. Initial lane/id ordering also invents rolling-fixture events. Those logging discrepancies are narrower symptoms; the blocker is the absence of common-time overlap, held territory, closure and response, not merely the event formatter.

        ### 2. DECISION — candidate score is inconsistent with its own physics

        C's fixed-route controls keep rider/setup/surface/entry speed equal. The projection prefers lane 4, while actually integrating the bend and next straight prefers lane 0. The wide route does have higher exit speed; that benefit is insufficient to pay its extra distance. Main C follows the static wide preference. Its initial straight inversion cannot be used to declare an outside-exit manoeuvre successful. B's ranking agrees in the simpler uniform fixture, so this is a counterexample rather than a claim that every decision fails.

        `routeLength/projectedSafeSpeed` is therefore insufficient as the sole performance estimate. It ignores current speed, braking/carry/drive phases, achievable lateral transition, remaining corner progress and the following straight's acceleration. H separately exposes the hard-coded three-arc topology assumption; that coarser fixture cannot by itself calibrate a normal bend. Neither model anticipates a rival's response or its own entry/apex/exit intent. Occupancy/style costs can redirect a target, but cannot repair the predicted travel-time ranking.

        ## Decision, execution, physics and interaction are not interchangeable

        Five reference targets are **not demonstrated to be intrinsically too few** for manager-scale AI. F/G and J already differentiate surfaces and geometry using them. Adding more samples to the same wrong cost would not cure C. The larger deficiency is temporal trajectory intent: bounded alternatives for entry, apex and exit, evaluated with achievable motion and a consistent production speed profile. This is a recommendation, not a new API in this PR; no optimized #47–#50 trajectory replaces the heat.

        Continuous lateral execution is useful but not sufficient by itself. The matched solo D control moves from 3 to about 2.178 instead of holding 3, yet both first traversals have exactly the same path distance, time and exit speed. Production freezes entry lateral position for that segment's radius/surface/distance and integrates lateral movement afterwards. This is an **EXECUTION/PHYSICS — IMPORTANT** cost-consistency limitation. Its scale has not been shown to independently break race order enough to call it a third blocker. Better intent alone would still need coherent costs along the executed motion and spatial interaction constraints.

        F/G's profile differences are intentionally mixed scenario inputs, not an isolated skill experiment. The one-skill controls separate the consumers:

        | skill | current consumer | strength supported by this audit |
        | --- | --- | --- |
        | Start | Standing reaction and launch force; zero-speed compatibility bootstrap outside this suite's rolling cases | Clear launch effect; modest whole-heat delta that can cascade into traffic |
        | Speed | Longitudinal drive reference force | Strong elapsed-time differentiation in matched A |
        | SlideControl | Corner capability/correction/overspeed retention; half lateral execution; contact occurrence/severity | Strong, multi-subsystem; aligned control also changes contact eligibility through physics |
        | TrackReading | Addressed noise in perceived grip/ruts | Context-dependent meaningful target differences, not guaranteed always-better finish |
        | PairRiding | Contact occurrence factor only | Narrow and weak observed count difference in 32 seeds; not team riding or overtake intelligence |
        | Adaptability | Half lateral traversal-rate weight | Narrow execution effect; no learned-surface/trajectory adaptation in production |

        None is literally consumer-free. PairRiding/Adaptability are narrow, not proof of rich tactical behavior. Matched contact counts are descriptive: eligibility and consequence chains can change, so they are not unbiased estimators of each probability coefficient. Start and Speed control heat times are measured, not new target values.

        ## What is not a blocker / what remains uncalibrated

        - **Start:** defer physical A/B/C/D gate geometry. Main I shows compression and differentiated launch, not an automatic collision storm. The equal-skill fixed-line control removes its Start/gate confounding and shows existing lookahead/geometry can produce different launch endpoints. Reference lanes are not gate coordinates. This does not validate real gate advantages, but no separate gate mechanism failure outranks contested-space resolution.
        - **Surface feedback:** retained wear raises ruts, lowers grip and changes later choices against identical fresh replays. The loop is alive. Entry-path sampling and wear kernels remain coarse, provisional spatial approximations; zero drying in the matched probe prevents a weather confound.
        - **Incidents/contact calibration:** F and C's crashes deserve review, but synthetic counts alone cannot establish real acceptable rates. Planned outer lane 4 makes a triggered random surface incident a crash regardless of its severity draw. Do not conflate this with contact crash probability. This audit changes no probabilities or 0.55 m / 0.12 s / 0.50 m constants.
        - **Fractional initialization:** the 0.05 probe's boundary exception is a real technical limitation for resumed/custom states, not evidence of a routine zero-progress standing-start failure. It belongs with the temporal/progress resolution work, not a balancing tweak.
        - **Manager-scale scope:** no RPM, Pacejka, precise CdA, suspension, tyre temperature or granular soil is required by these failures. Lack of telemetry keeps absolute physics magnitudes DATA/CALIBRATION, not automatically BLOCKER. Different gate/line advantages are allowed; equality of winners is not an acceptance target.

        ## Recommended next PRs — at most three, no implementation here

        1. **Common-time contested-space resolution.** Problem: independent segment endpoints allow simultaneous crossed paths and false occupancy at a 15 m gap. Scope: existing SimulationEngine/position/interaction owners, bounded common-time or swept occupancy resolution, pair holding/yielding and coherent order-event attribution; include fractional-boundary initialization. Do not retune acceleration/safe-speed/start/contact probabilities, add combined grip, change skills or implement motorcycle dynamics. Acceptance: crossing control cannot traverse occupied space unconditionally; an initially distant leader does not block merely because arrival offsets match; A/B/D/I have reproducible overlap/response evidence; no DNF or tie-break pseudo-passes; deterministic reversed collections and all 32 seeds.
        2. **Motion/cost consistency for continuous lateral execution.** Problem: inward and held D paths receive identical full-segment entry-radius costs. Scope: carry executed lateral motion into physical distance/surface/curvature sampling with a bounded manager-scale approximation, keeping current longitudinal/corner formulas. Do not retune their constants, add #48 turning-slip cost or #50 combined grip, or redesign rider attributes. Acceptance: held-line controls preserve their baseline; moving-line distance/surface observations follow the executed path without teleportation; entry/apex/exit and segment-splitting consistency are tested; shared-time interactions remain valid.
        3. **Bounded trajectory-intent decision scoring.** Problem: C static ranking disagrees with integrated production, while H has no opening-exit plan. Scope: entry/apex/exit intent built from existing five reference anchors, current speed, achievable transitions and the same production profile costs; bounded rival-response/occupancy constraints from PR 1. Do not multiply lane samples as a substitute for intent, tune grip/drive/contact, import optimizer physics or implement manager AI. Acceptance: C's predicted fixed-route ranking matches production under equal constraints; B still finds free inner space; H compares short/slow versus wide/fast exit profiles explicitly; F/G/J respond without forced winners; score differences decompose into time/movement/style/traffic/risk.

        The sequence puts spatial constraints before predicted manoeuvres. It does not prescribe new gate or contact constants. **Final judgment remains C**, because producing believable permutations is not yet a believable process of four riders contesting space. After this analysis PR, stop; the recommendations require separate approval.
        """;

    private static bool BothFinish(BehaviorRun run) => Finish(run, 1).Finished && Finish(run, 2).Finished;
    private static CoreSim.Race.RiderHeatResult Finish(BehaviorRun run, int id) => run.Classification.Single(item => item.RiderId == id);
    private static string LastSpeed(IReadOnlyList<BehaviorStep> steps) => steps.Count == 0 ? "n/a" : F(steps[^1].TraversalExitSpeedMetersPerSecond);
    private static string Order(IEnumerable<int> order) => string.Join(">", order.Select(N));
    private static string F(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);
    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
}
