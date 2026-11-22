import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { gunzipSync } from 'node:zlib';

const directory = process.argv[2] ?? 'data/experiments/speedway208';
const read = name => JSON.parse(gunzipSync(readFileSync(join(directory, name + '.json.gz'))));
const files = ['b4', 'c4', 'd4', 'e4', 'x5-c', 'x5-d', 'x5-e', 'x6-c', 'x6-d', 'x6-e'];
const bounds = values => values.length ? [Math.min(...values), Math.max(...values)] : null;
const finite = values => values.filter(Number.isFinite);
const counts = (rows, key) => rows.reduce((result, row) => {
    const label = key(row);
    result[label] = (result[label] ?? 0) + 1;
    return result;
}, {});
const gate = (run, name) => run.Gates.find(x => x.Name === name);
const compact = run => ({
    Key: run.Key, Stabilization: run.Stabilization, Continued: run.Continuation,
    Finish: run.Finish, FinishSeconds: run.FinishSeconds, Stop: run.Stop, LastLegalS: run.LegalS,
    LegalSweep: run.LegalSweep, ExitToEnd: run.ExitToEnd, FutureSweep: run.FutureSweep,
    TimeToSettle: run.TimeToSettle, Failure: run.Failure,
    Entry: gate(run, 'entry')?.Sample,
    Apex: gate(run, 'apex')?.Sample,
    Exit: gate(run, 'exit'),
    Stabilized: gate(run, 'stabilization'),
    End: run.End,
});
const audit = read('fourth-exit-audit');
assert.equal(audit.Baseline, 12152);
assert.equal(audit.NativeWorlds, 0);
assert.equal(audit.Audit.length, 5);
const baseline = audit.Audit.map(x => ({
    Fixture: x.Fixture, LastLegalS: x.LastLegalS, FailureS: x.End.S,
    LastLegalSweep: x.LegalSweep, FailureSweep: x.End.Sweep, Stop: x.Stop,
}));
const arms = {};
for (const name of files) {
    const a = read('arm-' + name);
    const best = a.Trials.find(x => x.Id === a.Best);
    const completed = a.Trials.filter(t => t.Runs.every(x => x.Continuation));
    const focused = a.Trials.filter(t => t.Runs.every(x => x.Gates.some(g => g.Name === 'exit')));
    const exits = focused.flatMap(t => t.Runs.map(r => gate(r, 'exit')));
    const successes = completed.flatMap(t => t.Runs.map(r => gate(r, 'exit')));
    const range = rows => ({
        Outward: bounds(rows.map(x => x.Motion.Outward)), Course: bounds(rows.map(x => x.Motion.Course)),
        Yaw: bounds(rows.map(x => x.Motion.Yaw)), Speed: bounds(rows.map(x => x.Sample.Speed)),
        FrontLoad: bounds(finite(rows.map(x => x.FrontLoad))), RearUse: bounds(finite(rows.map(x => x.RearUse))),
        Outer: bounds(rows.map(x => x.Sample.Outer)), Steer: bounds(rows.map(x => x.Sample.Steer)),
    });
    const failures = a.Trials.flatMap(x => x.Runs).filter(x => !x.Continuation);
    const suffixes = a.Trials.flatMap(x => x.Successors?.slice(1) ?? []);
    arms[name] = {
        Budget: a.Budget, Calls: a.Calls, Steps: a.Steps, Cpu: a.CpuSeconds, Wall: a.WallSeconds,
        Halt: a.Stop, SelectedOriginalArm: a.Best, SelectedManeuver: best.Maneuver,
        UniqueProposals: a.Trials.length, Retained: a.Retained, RetainedParents:
            new Set(a.Trials.filter(x => a.Retained.includes(x.Id)).map(x => x.Parent)).size,
        AllTrainingExit: focused.length, AllTrainingStabilize: a.Trials.filter(x => x.Runs.every(r => r.Stabilization)).length,
        AllTrainingContinue: completed.length, AllTrainingFinish: a.Trials.filter(x => x.Runs.every(y => y.Finish)).length,
        PairedSuffixes: suffixes.length, CommonViableSuffixes: suffixes.filter(s => s.Runs.every(x => x.Continuation)).length,
        ExitEnvelope: range(exits), DemonstratedContinuationExitEnvelope: range(successes),
        FutureSwept: bounds(finite(completed.flatMap(x => x.Runs.map(r => r.FutureSweep)))),
        SettleTime: bounds(finite(completed.flatMap(x => x.Runs.map(r => r.TimeToSettle)))),
        Failures: counts(failures, x => x.Failure?.Condition ?? x.Stop),
        SuccessorFailures: counts(suffixes.flatMap(x => x.Runs).filter(x => !x.Continuation), x => x.Failure?.Condition ?? x.Stop),
        OriginalSelectedResults: best.Runs.map(compact),
    };
}
const paired = read('paired-training').Cases.map(x => ({
    Source: x.Source, Label: x.Label, ReachedStabilization: x.Runs.filter(r => r.Stabilization).length,
    ContinuedTo28m: x.Runs.filter(r => r.Continuation).length, WorstS: Math.min(...x.Runs.map(r => r.LegalS)),
    Stops: x.Runs.map(r => r.Stop),
    Nominal: compact(x.Runs[0]),
}));
const selection = read('training-selection');
const screened = selection.Options.map(x => ({
    Index: x.Index, Source: x.Source, StrictFinishes: x.Runs.filter(r => r.Finish).length,
    FourthStabilizations: x.Runs.filter(r => r.Stabilization).length,
    LastLegal: Math.min(...x.Runs.map(r => r.LegalS)), MinLegalSweep: Math.min(...x.Runs.map(r => r.LegalSweep)),
}));
const winner = selection.Options.find(x => x.Index === selection.Winner);
const official = read('official/official');
assert.deepEqual(winner.Plan, official.result.Plan);
const measured = official.result.Results.map(run => {
    const replay = read('official/' + run.Key);
    assert.deepEqual(replay.result, run);
    const full = replay.Trace;
    const legal = run.Finish ? full : full.slice(0, -1);
    const peak = Math.max(...legal.map(x => x.Observed.Start.State.Rider.Speed)) * 3.6;
    const distance = legal.at(-1).Observed.Start.State.Rider.DistanceMeters;
    const sections = [513.168, 561.863, 610.557, 622.557, 672.557, 769.947, 781.947, 831.947, 929.336, 941.336];
    return { ...compact(run), PeakKmh: peak, TravelMeters: distance,
        PrecedingS: sections.map(s => {
            const measure = legal.find(x => x.Observed.Start.State.Rider.TotalReferenceMeters >= s);
            if (!measure) return null;
            return { S: s, ObservedS: measure.Observed.Start.State.Rider.TotalReferenceMeters,
                Speed: measure.Observed.Start.State.Rider.Speed, Yaw: measure.Velocity.Yaw,
                Outward: measure.Velocity.Outward, Course: measure.Observed.Course, Swept: measure.Observed.Swept,
                Time: measure.Observed.Start.State.Time };
        }),
    };
});
const timings = read('model-timing').Runs.map(x => ({
    Repeat: x.Repeat, Timing: x.Timing, GuardsRejected: x.Rejected, Cpu: x.CpuSeconds, Wall: x.WallSeconds,
}));
const ledger = readFileSync(join(directory, 'world-ledger.jsonl'), 'utf8').trim()
    .split(/\r?\n/).filter(Boolean).map(line => JSON.parse(line));
assert(ledger.length <= 8);
const native = [];
const allDecisions = [];
for (const run of official.result.Results) {
    const key = 'native/H29-V-' + run.Key;
    let capture;
    try {
        capture = read(key);
    } catch (error) {
        if (error.code === 'ENOENT') continue;
        throw error;
    }
    const replay = read('official/' + run.Key);
    const modelAt = new Map(replay.Trace.map(step => [step.Observed.Start.State.Time.toFixed(6), step.Observed.Start.State.Rider]));
    const differences = capture.Decisions.flatMap(decision => {
        const state = decision.State.State;
        const model = modelAt.get(state.Time.toFixed(6));
        if (!model) return [];
        return [{ Time: state.Time, Position: Math.hypot(state.Rider.Position.X-model.Position.X,
            state.Rider.Position.Y-model.Position.Y), Reference: Math.abs(state.Rider.TotalReferenceMeters-model.TotalReferenceMeters),
            Speed: Math.abs(state.Rider.Speed-model.Speed), Yaw: Math.abs(state.Rider.AngularVelocity-model.AngularVelocity) }];
    });
    allDecisions.push(...capture.Decisions.map(x => x.Milliseconds));
    const max = name => Math.max(...differences.map(x => x[name]));
    native.push({...capture.Result, FinishDeltaFromModel: capture.Result.Finish-run.FinishSeconds,
        CheckedReviews: differences.length, MaxBodyDistanceDelta: max('Position'), MaxReferenceDelta: max('Reference'),
        MaxSpeedDelta: max('Speed'), MaxYawDelta: max('Yaw'),
        PeakKmh: 3.6*Math.max(...capture.Run.Riders[0].Samples.map(x => x.Input.Rider.Speed)),
        PeakDecision: capture.Decisions.reduce((a,b) => b.Milliseconds>a.Milliseconds ? b : a).Milliseconds,
        DeadlineOverruns: capture.Decisions.filter(x => x.Milliseconds>50)
            .map(x => ({Time:x.State.State.Time,Milliseconds:x.Milliseconds})),
    });
}
assert.equal(native.length, ledger.length);
allDecisions.sort((a,b) => a-b);
const at = p => allDecisions[Math.floor((allDecisions.length-1)*p)] ?? null;
const pooled = {Count: allDecisions.length, P50: at(.5), P95: at(.95), P99: at(.99),
    Max: at(1), Over50: allDecisions.filter(x => x>50).length};
writeFileSync(join(directory, 'summary.json'), JSON.stringify({
    Study: 208, Base: '11e4858dc146098c74a7b90e639bf56a599ced1e', Bend: audit.Bend,
    HistoricalBaseline: baseline,
    Authority: audit.Authority.map(({Interventions,Start,AxleLimits,...x}) => ({
        ...x, S: Start.State.Rider.TotalReferenceMeters, Time: Start.State.Time,
        FrontLoad: AxleLimits?.FrontNormalNewtons, RearLoad: AxleLimits?.RearNormalNewtons,
        Friction: AxleLimits?.EffectiveFrictionCoefficient,
    })),
    Arms: arms, ControlledInterventions: paired, Screening: {Starts: selection.Training, Winner: winner.Source, Options: screened},
    Official: {FourthStabilizations: official.FourthExitPassed, FullFinishes: official.FullFinishes,
        Offsets: official.Offsets, Runs: measured},
    ModelTiming: timings, Native: {Runs: native, PooledDecisions: pooled}, Box2DLedger: ledger,
}, null, 2) + '\n');
