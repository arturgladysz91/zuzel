using System.Diagnostics;
using System.Numerics;
using Autonomous146;
using Efficient147;
using Robust150;
using SharedWorldSpeedway;
namespace Contingency151;

public sealed record Options151(bool Enabled = false, int Laps = 3, int ObservationDepth = 2, int MaximumRepairs = 6, int MaximumRoots = 7);
public sealed record Envelope151(double PositionMeters = .00015, double VelocityMetersPerSecond = .00075, double HeadingRadians = .00012, double YawRadiansPerSecond = .00055, double WheelRadiansPerSecond = .0025, double CrankRadiansPerSecond = .023) {
    public const string Scope = "Finite signed correlated innovations after each next observation. Rounded >2x saved identical-command one-review component maxima; NOT a global bound, independent box coverage, or a bound on long open forecasts. On the proved axis-aligned invariant subspace only longitudinal/wheel/crank innovations apply; any nonzero yaw, transverse velocity, active or pending steer restores all components.";
}
public sealed record Node151(int Id, int? Parent, int Sign, int Depth, State146 Observed, FreeRacingAction Action, Safety146 Execution, int? TerminalPlan, string Source);
public sealed record Terminal151(int Id, int Node, Tape150 Tape);
public sealed record Attempt151(string Origin, bool Legal, double? Arrival, string Reason, object? FirstFailure, FreeRacingAction? RootAction);
public sealed record RejectedTree151(string Origin, Node151[] Nodes, Terminal151[] Terminals);
public sealed record PhysicalFault151(string Operation, double ForecastStart, double LastAcceptedPointTime, FreeRacingAction? Action, string Error);
public sealed record Review151(double Time, State146 Observed, double Goal, double SafetyGoal, string Reason, bool ConditionalCertificate, bool Independent, double IrrevocableUntil, Node151[] Tree, Terminal151[] Terminals, RejectedTree151[] RejectedTrees, Attempt151[] Attempts, Tape150? Nominal, object Profile, object Counts, double CpuSeconds, PhysicalFault151[]? PhysicalFaults = null);

/// <summary>Default-OFF finite AND/OR recourse over the original physical providers. A leaf needs a complete legal reserve maneuver through the next bend, not just a short safe step.</summary>
public sealed class Planner151 {
    public Options151 Options { get; }
    public Runtime150 Runtime { get; }
    public Envelope151 Envelope { get; }
    public Review151? Last { get; private set; }
    public Tape150? SelectedAnchor { get; private set; }
    readonly Action guard;
    CpuProfile147 profile = new();
    readonly List<Tape150> portfolio = new();
    readonly List<Node151> nodes = new();
    readonly List<Terminal151> terminals = new();
    readonly List<Attempt151> attempts = new();
    readonly List<RejectedTree151> rejectedTrees = new();
    readonly List<PhysicalFault151> faults = new();
    readonly Dictionary<string, FreeRacingAction> safeSeeds = new();
    long additionalForecasts, additionalContacts, failedK1Batches;
    long reviewAdditionalForecasts, reviewAdditionalContacts;
    public static bool PhysicalForecastFailure(InvalidOperationException error) => error.Message is "Crank/clutch passivity or energy identity failed." or "Clutch solve did not converge." or "Unbracketed passive clutch solve." or "No nonnegative crank midpoint; no hidden stall reset.";
    double goal, safetyGoal;
    int repairCount;
    long reviewForecasts, reviewContacts;
    TimeSpan reviewCpu;
    const long MaximumReviewForecasts = 32768, MaximumReviewContacts = 2000000;
    void Guard() {
        guard();
        if (Runtime.Forecasts - reviewForecasts + additionalForecasts - reviewAdditionalForecasts >= MaximumReviewForecasts || Runtime.Contacts - reviewContacts + additionalContacts - reviewAdditionalContacts >= MaximumReviewContacts || faults.Count >= 512 || (Process.GetCurrentProcess().TotalProcessorTime - reviewCpu).TotalSeconds >= 30)
            throw new PlanningBudget146("Study151 deterministic forecast/contact or30 CPU-s review cap");
    }

    public Planner151(RiderSpec spec, bool separated, Options151? options = null, Action? guard = null, Envelope151? envelope = null) {
        Options = options ?? new();
        if (!Options.Enabled) throw new InvalidOperationException("Planner151 is OFF by default.");
        if (Options.Laps is < 1 or > 3 || Options.ObservationDepth is < 2 or > 3 || Options.MaximumRepairs is < 0 or > 60 || Options.MaximumRoots is < 1 or > 15) throw new ArgumentException("Bounded contingency planner required.");
        Envelope = envelope ?? new(); this.guard = guard ?? (() => { });
        Runtime = new(spec, separated, workBudget: false);
    }
    public void InitializeDiagnosticTape(Tape150 tape) { if (portfolio.Count != 0) throw new InvalidOperationException("Initialize once"); portfolio.Add(tape); }
    public void InitializeDiagnosticReview(Review151 review) {
        if (portfolio.Count != 0 || review.Nominal is null || !review.ConditionalCertificate) throw new InvalidOperationException("A complete prior diagnostic policy is required");
        Remember(review.Nominal, review.Tree, review.Terminals, review.Time);
    }
    public static bool AxisAlignedInvariant(State146 state) {
        var i = state.Input; var v = i.Rider;
        return !v.Road.IsTurn && v.Road.Tangent == Vector2.UnitX && v.Heading == 0 && v.AngularVelocity == 0 && v.Velocity.Y == 0 && v.Velocity.X >= 0 && i.Steering == 0 && i.Active.SteeringRadians == 0 && i.Pending.All(c => c.Action.SteeringRadians == 0);
    }
    public State146 Scenario(State146 state, int sign) {
        if (sign is < -1 or > 1) throw new ArgumentException("Finite signs only");
        if (sign == 0 || state.Input.Time <= RiderController.ReactionSeconds(Runtime.Spec) + 1e-9) return state;
        var e = Envelope; var v = state.Input.Rider; var axis = AxisAlignedInvariant(state);
        // In this exact subspace both axle lateral velocities/forces, moment arms' Y, yaw and transverse motion stay exactly zero.
        // Longitudinal rounding and wheel/crank uncertainty remain. This is an equation invariant, not a smaller historical maximum.
        var direction = axis ? Vector2.UnitX : v.Road.Outward;
        var pos = v.Position + direction * (float)(sign * e.PositionMeters);
        var road = Runtime.Frame.Project(pos);
        var reference = v.TotalReferenceMeters + Runtime.Frame.UnwrapDelta(v.Road.ReferenceMeters, road.ReferenceMeters);
        var rider = v with { Position = pos, Velocity = v.Velocity + direction * (float)(sign * e.VelocityMetersPerSecond), Heading = v.Heading + (axis ? 0 : (float)(sign * e.HeadingRadians)), AngularVelocity = v.AngularVelocity + (axis ? 0 : (float)(sign * e.YawRadiansPerSecond)), Road = road, TotalReferenceMeters = reference, Context = Runtime.Frame.Context(road, reference) };
        // Accepted active command, queue, steering and all activation timestamps remain immutable.
        return new(state.Input with { Rider = rider, Omega = Math.Max(0, state.Input.Omega + sign * e.WheelRadiansPerSecond) }, Math.Max(0, state.CrankOmega + sign * e.CrankRadiansPerSecond));
    }
    Transition146 Step(State146 state, FreeRacingAction action, string phase) { Guard(); using var cost = profile.Phase(phase); return Runtime.Step(state, action); }
    FreeRacingAction Seed(State146 state) {
        Guard(); using var cost = profile.Phase("full_k1");
        var key = ExactKey147.State(state); if (safeSeeds.TryGetValue(key, out var prior)) return prior;
        try { return Runtime.Seed(state).Choice.Action; }
        catch (InvalidOperationException error) when (PhysicalForecastFailure(error)) {
            failedK1Batches++; faults.Add(new("K1 batch rejected; re-evaluate proposals individually", state.Input.Time, state.Input.Time, null, error.Message));
            var frame = Runtime.Frame; var parameters = FreeLearningExperiment.Parameters(); var surface = MultiActionExperiment135.Surface(frame); var candidates = new List<FreeLearningCandidate>();
            foreach (var action in FreeLearningController.Actions(state.Input, Controller143.Policy, true)) {
                Guard(); double lastAccepted = state.Input.Time;
                try {
                    FreeRacingPrediction prediction = Runtime.Separated
                        ? Limits149.FastPredictor149.Predict(state.Input, Runtime.Spec, frame, surface, parameters, 147, 52.72097f, .9, action, powertrain: new(omega: state.CrankOmega), observeCrank: (t, _) => lastAccepted = t)
                        : Speed148.FastPredictor148.Predict(state.Input, Runtime.Spec, frame, surface, parameters, 147, 52.72097f, .9, action, powertrain: new(omega: state.CrankOmega), observeCrank: (t, _) => lastAccepted = t);
                    additionalForecasts++; additionalContacts += prediction.ForceEvaluations;
                    var candidate = Objective142.FromPrediction(state.Input, Controller143.Policy, frame, surface, prediction, action); var score = Controller143.Score(candidate, Policy144.Variant("K1"));
                    candidates.Add(candidate.Candidate with { Score = score.Score, Safety = score.Safety });
                } catch (InvalidOperationException rejected) when (PhysicalForecastFailure(rejected)) { faults.Add(new("individual K1 proposal excluded", state.Input.Time, lastAccepted, action, rejected.Message)); }
            }
            if (candidates.Count == 0) throw new InvalidOperationException("No physically evaluable K1 proposal");
            var selected = candidates.OrderBy(c => c.Safety).ThenBy(c => c.Score).ThenBy(c => Math.Abs(c.Action.SteeringRadians - state.Input.Active.SteeringRadians)).ThenBy(c => c.Action.SteeringRadians).First().Action;
            safeSeeds[key] = selected; return selected;
        }
    }
    FreeRacingAction Feedback(State146 state, Tape150 anchor, string phase) { using var cost = profile.Phase(phase); return Feedback150.Action(Runtime, state, anchor); }
    Tape150 Rollout(State146 start, Tape150? anchor, FreeRacingAction[]? fixedActions, string origin, string phase) {
        var states = new List<State146> { start }; var points = new List<FreeRacingPoint>(); var actions = new List<FreeRacingAction>();
        var state = start; double? arrival = null;
        string? physicalFailure = null;
        for (int k = 0; k < 130; k++) {
            Guard();
            var index = anchor is null ? -1 : (int)Math.Round((state.Input.Time - anchor.CreatedAt) / .2);
            FreeRacingAction action; Transition146 step;
            try {
                action = fixedActions is not null && k < fixedActions.Length ? fixedActions[k] : anchor is not null && index >= 0 && index < anchor.Actions.Length ? Feedback(state, anchor, phase) : Seed(state);
                step = Step(state, action, phase);
            } catch (InvalidOperationException error) when (PhysicalForecastFailure(error) || error.Message == "No physically evaluable K1 proposal") {
                physicalFailure = error.Message; faults.Add(new(origin, state.Input.Time, state.Input.Time, null, error.Message)); break;
            }
            actions.Add(action); states.Add(step.End);
            foreach (var point in step.Prediction.Points.Skip(k == 0 ? 0 : 1)) {
                if (arrival is null && point.Rider.TotalReferenceMeters >= goal && points.Count > 0) {
                    var prev = points[^1]; var ds = point.Rider.TotalReferenceMeters - prev.Rider.TotalReferenceMeters;
                    arrival = prev.Time + (point.Time - prev.Time) * Math.Clamp((goal - prev.Rider.TotalReferenceMeters) / ds, 0, 1);
                }
                points.Add(point);
            }
            state = step.End;
            if (!Planner146.Check(step.Prediction.Points, .1).Legal || state.Input.Rider.TotalReferenceMeters >= safetyGoal) break;
        }
        var safety = points.Count == 0 ? new Safety146(false, 0, 0, 0, 0, "no physically accepted forecast point") : Planner146.Check(points.ToArray(), .1);
        if (physicalFailure is not null) safety = safety with { Legal = false, Reason = "physical forecast rejected: " + physicalFailure };
        if (arrival is null || state.Input.Rider.TotalReferenceMeters < safetyGoal) safety = safety with { Legal = false, Reason = safety.Legal ? "reserve maneuver did not reach terminal geometric section" : safety.Reason };
        var tape = new Tape150(start.Input.Time, goal, safetyGoal, actions.ToArray(), states.ToArray(), points.ToArray(), arrival, safety, origin);
        attempts.Add(new(origin, safety.Legal, arrival, safety.Reason, Audit151.FirstFailure(tape), actions.FirstOrDefault())); return tape;
    }
    Tape150? Complete(State146 state, Tape150? anchor, string phase, FreeRacingAction? forcedRoot = null) {
        var tape = Rollout(state, anchor, null, anchor is null ? "fresh-reserve" : "revalidated-feedback", phase);
        if (forcedRoot is not null && tape.Actions.Length > 0 && tape.Actions[0] != forcedRoot.Value) {
            tape = Rollout(state, null, new[] { forcedRoot.Value }, "root-alternative-regenerated-tail", phase);
        }
        if (tape.Safety.Legal) return tape;
        // Retain scenario-specific reserve policies from the previous certified tree. Every one is propagated again from this accepted observation.
        foreach (var other in portfolio.Where(p => !ReferenceEquals(p, anchor) && p.CreatedAt <= state.Input.Time + 1e-8 && p.SafetyGoal >= safetyGoal - 1e-7).Take(9)) {
            var alternative = Rollout(state, other, null, "retained-conditional-reserve", phase);
            if (forcedRoot is not null && (alternative.Actions.Length == 0 || alternative.Actions[0] != forcedRoot.Value)) continue;
            if (alternative.Safety.Legal) return alternative;
        }
        // K1 is only a proposal generator. Its entire continuation must pass exactly the same physical and terminal tests.
        var regenerated = Rollout(state, null, null, "observation-specific-fresh-reserve", phase);
        if (forcedRoot is not null && regenerated.Actions.Length > 0 && regenerated.Actions[0] != forcedRoot.Value) {
            var actions = regenerated.Actions.ToArray(); actions[0] = forcedRoot.Value;
            regenerated = Rollout(state, null, actions, "observation-specific-root-reserve", phase);
        }
        if (regenerated.Safety.Legal) return regenerated;
        if (regenerated.End.Input.Rider.TotalReferenceMeters > tape.End.Input.Rider.TotalReferenceMeters) tape = regenerated;
        if (tape.Actions.Length == 0) return null;
        // Local repair before the first failing physical interval. Both signs and throttle channels are offered.
        var failureTime = tape.Points.FirstOrDefault(p => !Planner146.Check(new[] { p }, .1).Legal)?.Time ?? tape.End.Input.Time;
        var riskEpoch = Math.Max(0, (int)Math.Floor((failureTime - state.Input.Time - RiderController.ReactionSeconds(Runtime.Spec)) / .2) - 3);
        var epochs = new[] { Math.Clamp(riskEpoch, 0, tape.Actions.Length - 1), Math.Max(0, riskEpoch - 3), 0 }.Distinct().ToArray();
        foreach (var epoch in epochs) {
            var candidates = RepairActions(tape.Actions[Math.Min(epoch, tape.Actions.Length - 1)]);
            foreach (var action in candidates) {
                if (repairCount++ >= Options.MaximumRepairs) return null;
                var actions = tape.Actions.ToArray();
                for (int j = epoch; j < Math.Min(epoch + 2, actions.Length); j++) actions[j] = action;
                if (forcedRoot is not null) actions[0] = forcedRoot.Value;
                var repaired = Rollout(state, null, actions, "local-repair-" + epoch, phase);
                if (repaired.Safety.Legal) return repaired;
            }
        }
        return null;
    }
    public static FreeRacingAction[] RepairActions(FreeRacingAction original) {
        var max = TwoContactBikeParameters.Default.MaximumSteeringRadians;
        var shifts = new[] { -.5f, .5f, -1f, 1f, -2f, 2f }.Select(x => x * MathF.PI / 180);
        var drives = new[] { original.Drive, 0, (float)Controller143.Policy.Drive, 1f }.Distinct();
        return shifts.SelectMany(shift => drives.Select(d => new FreeRacingAction(Math.Clamp(original.SteeringRadians + shift, -max, max), d))).Distinct().ToArray();
    }
    bool Tree(State146 state, Tape150 anchor, int depth, int? parent, int sign) {
        Guard();
        var tape = depth == 0 ? anchor : Complete(state, anchor, "conditional_continuations");
        if (tape is null || !tape.Safety.Legal) return false;
        var action = tape.Actions[0]; var transition = Step(state, action, "uncertainty_scenarios");
        var safety = Planner146.Check(transition.Prediction.Points, .1); if (!safety.Legal) return false;
        var id = nodes.Count; var terminalId = depth == Options.ObservationDepth ? terminals.Count : (int?)null;
        nodes.Add(new(id, parent, sign, depth, state, action, safety, terminalId, tape.Origin));
        if (terminalId is int leaf) { terminals.Add(new(leaf, id, tape)); return true; }
        // Separate future observation => separate legal continuation and action. No anticipatory change to pending controls.
        foreach (var futureSign in new[] { 0, 1, -1 }) {
            var child = Scenario(transition.End, futureSign);
            var nodeMark = nodes.Count; var terminalMark = terminals.Count;
            if (Tree(child, tape, depth + 1, id, futureSign)) continue;
            rejectedTrees.Add(new("future-observation-" + child.Input.Time + "-sign-" + futureSign, nodes.Skip(nodeMark).ToArray(), terminals.Skip(terminalMark).ToArray()));
            nodes.RemoveRange(nodeMark, nodes.Count - nodeMark); terminals.RemoveRange(terminalMark, terminals.Count - terminalMark);
            // Bounded OR at the new observation: force a different command now, regenerate its causal future.
            var repaired = false;
            foreach (var alternative in new[] { 0f, (float)Controller143.Policy.Drive, 1f }.Where(d => d != tape.Actions[Math.Min(depth + 1, tape.Actions.Length - 1)].Drive)) {
                var proposal = Complete(child, tape, "conditional_continuations", tape.Actions[Math.Min(depth + 1, tape.Actions.Length - 1)] with { Drive = alternative });
                if (proposal is null) continue;
                // A proposal is an anchor at the child epoch. The recursive call must still certify its descendants.
                if (Tree(child, proposal, depth + 1, id, futureSign)) { repaired = true; break; }
                rejectedTrees.Add(new("future-OR-throttle-" + alternative, nodes.Skip(nodeMark).ToArray(), terminals.Skip(terminalMark).ToArray()));
                nodes.RemoveRange(nodeMark, nodes.Count - nodeMark); terminals.RemoveRange(terminalMark, terminals.Count - terminalMark);
            }
            if (!repaired) return false;
        }
        return true;
    }
    bool Independent() {
        using var role = Runtime.Operation("independent_preflight");
        foreach (var node in nodes) {
            var transition = Step(node.Observed, node.Action, "independent_preflight");
            if (!Planner146.Check(transition.Prediction.Points, .1).Legal) return false;
            foreach (var child in nodes.Where(x => x.Parent == node.Id))
                if (ExactKey147.State(Scenario(transition.End, child.Sign)) != ExactKey147.State(child.Observed)) return false;
        }
        foreach (var terminal in terminals) {
            var replay = Rollout(terminal.Tape.States[0], null, terminal.Tape.Actions, "independent-reserve", "independent_preflight");
            if (!replay.Safety.Legal || !replay.Actions.SequenceEqual(terminal.Tape.Actions)) return false;
        }
        return true;
    }
    public Decision144 Choose(FreeRacingInput input, CrankClutch144 crank, FreeRacingAction? diagnosticRoot = null) {
        var began = Process.GetCurrentProcess().TotalProcessorTime; guard(); profile = new(); Runtime.Begin(input.Time);
        reviewCpu = began; reviewForecasts = Runtime.Forecasts; reviewContacts = Runtime.Contacts; SelectedAnchor = null;
        reviewAdditionalForecasts = additionalForecasts; reviewAdditionalContacts = additionalContacts;
        nodes.Clear(); terminals.Clear(); attempts.Clear(); rejectedTrees.Clear(); faults.Clear(); repairCount = 0; Last = null;
        var state = new State146(input with { NextReview = input.Time }, crank.Omega);
        var retained = portfolio.FirstOrDefault(x => x.Goal > input.Rider.TotalReferenceMeters + .001);
        goal = retained?.Goal ?? Planner146.Goal(Runtime.Frame, input.Rider.TotalReferenceMeters, Options.Laps);
        safetyGoal = Planner146.Goal(Runtime.Frame, goal + .005, Options.Laps);
        var forecasts = Runtime.Forecasts; var contacts = Runtime.Contacts;
        Tape150? selected = null; var independent = false; var reason = "abstain: finite recourse search has no certified complete reserve";
        try {
            var seed = Complete(state, retained, "recertification", diagnosticRoot);
            var roots = new List<FreeRacingAction?> { null };
            using (profile.Phase("candidate_generation")) {
                var root = seed?.Actions[0] ?? input.Active;
                roots.AddRange(new[] { 0f, (float)Controller143.Policy.Drive, 1f }.Where(d => d != root.Drive).Select(d => (FreeRacingAction?)(root with { Drive = d })));
                roots.AddRange(RepairActions(root).Select(a => (FreeRacingAction?)a));
            }
            foreach (var forced in roots.Take(diagnosticRoot is null ? Options.MaximumRoots : 1)) {
                nodes.Clear(); terminals.Clear();
                var candidate = forced is null ? seed : Complete(state, seed ?? retained, "nominal_propagation", forced);
                if (candidate is null) continue;
                if (Tree(state, candidate, 0, null, 0) && Independent()) { selected = candidate; independent = true; reason = "finite conditional reserve certified"; break; }
                rejectedTrees.Add(new("root-" + forced, nodes.ToArray(), terminals.ToArray()));
            }
        } catch (PlanningBudget146 error) { reason = "abstain: " + error.Message; }
        catch (InvalidOperationException error) when (error.Message == "STUDY151_CPU_STOP") { reason = "abstain: " + error.Message; }
        if (selected is not null) {
            Remember(selected, nodes, terminals, input.Time);
            SelectedAnchor = selected;
        }
        var elapsed = (Process.GetCurrentProcess().TotalProcessorTime - began).TotalSeconds;
        Last = new(input.Time, state, goal, safetyGoal, reason, selected is not null, independent, input.Time + .2 + RiderController.ReactionSeconds(Runtime.Spec), nodes.ToArray(), terminals.ToArray(), rejectedTrees.ToArray(), attempts.ToArray(), selected, profile.Snapshot(), new { Physical = Runtime.Counters(), AdditionalCompletedForecasts = additionalForecasts, AdditionalCompletedContacts = additionalContacts, FailedK1Batches = failedK1Batches, FaultCountsScope = "Original provider counters count completed calls; rejected calls remain in PhysicalFaults and actual measured CPU, not an invented force count." }, elapsed, faults.ToArray());
        if (selected is null) throw new InvalidOperationException("STUDY151_ABSTAIN: " + reason);
        return new(new(input.Time, selected.Actions[0], FreeActionSafety.SafeInHorizon, false, Array.Empty<FreeLearningCandidate>(), checked((int)(Runtime.Contacts - contacts + additionalContacts - reviewAdditionalContacts)), checked((int)(Runtime.Forecasts - forecasts + additionalForecasts - reviewAdditionalForecasts))), elapsed);
    }
    void Remember(Tape150 selected, IEnumerable<Node151> tree, IEnumerable<Terminal151> reserves, double createdAt) {
        portfolio.Clear(); portfolio.Add(selected);
        foreach (var terminal in reserves) {
            var leaf = tree.Single(n => n.Id == terminal.Node); var path = new List<Node151>(); var parent = leaf.Parent;
            while (parent is int id) { var node = tree.Single(n => n.Id == id); path.Insert(0, node); parent = node.Parent; }
            var actions = path.Select(n => n.Action).Concat(terminal.Tape.Actions).ToArray();
            var states = path.Select(n => n.Observed).Concat(terminal.Tape.States).ToArray();
            portfolio.Add(new(createdAt, selected.Goal, selected.SafetyGoal, actions, states, Array.Empty<FreeRacingPoint>(), terminal.Tape.Arrival, terminal.Tape.Safety with { Legal = false, Reason = "policy reference only; certificate must be recomputed" }, "retained-conditional-policy"));
        }
    }
}
