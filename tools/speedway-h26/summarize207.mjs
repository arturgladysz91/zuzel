import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { gunzipSync } from 'node:zlib';
import assert from 'node:assert/strict';

const directory = process.argv[2] ?? 'data/experiments/speedway207';
const read = name => JSON.parse(gunzipSync(readFileSync(join(directory, name + '.json.gz'))));
const sum = values => values.reduce((a, b) => a + b, 0);
const bounds = values => values.length ? [Math.min(...values), Math.max(...values)] : null;
const countBy = (values, key) => Object.fromEntries(Object.entries(
    values.reduce((counts, value) => {
        const label = key(value);
        counts[label] = (counts[label] ?? 0) + 1;
        return counts;
    }, {}),
).sort(([a], [b]) => a.localeCompare(b)));
const eligible = branch => branch.Outcomes.every(result => result.Accepted && result.Checkpoint);
const stop = run => ({
    Fixture: run.Fixture, Complete: run.Complete, Stop: run.Stop, Finish: run.Finish,
    WorstSwept: run.Sweep, LastLegalS: run.LastLegalS, LegalSweep: run.LegalSweep,
    End: run.End, Failure: run.Failure, Contact: run.Contact,
});
const fields = ['Swept', 'CourseError', 'Yaw', 'Beta', 'Speed', 'Outer', 'FrontNormal',
    'RearFraction', 'YawMoment', 'SteeringTravel', 'DriftOneSecond', 'PendingDelay', 'Quality'];
const arms = {};
for (const name of ['pilot', 'greedy', 'beam', 'recovery', 'pace']) {
    const data = read('search-' + name);
    const locked = read('locked-' + name);
    const best = locked.Holdouts[0];
    assert.equal(locked.Best, best.Plan.Branch);
    assert.equal(locked.Best, data.Best);
    assert.equal(locked.NativeWorlds, 0);
    assert(best);
    assert.equal(data.LockedFixtures.length, 5);
    assert.equal(best.Results.length, 5);
    const branches = new Map(data.Attempts.map(attempt => [attempt.Id, attempt]));
    const outcomes = [...branches.values()].flatMap(branch => branch.Outcomes);
    const firstPhase = data.FirstPhase ?? 0;
    const stalled = data.Frontier.at(-1)?.Phase;
    const lastLegal = data.Frontier.findLast(phase => phase.Legal > 0)?.Phase;
    const counts = countBy(outcomes.filter(x => !x.Accepted), x =>
        x.Stop.startsWith('physics: H28-F NoPhysicallyAdmissibleRoot:') ? 'no physically admissible root' : x.Stop);
    const envelopes = [...new Set([9, 10, 13, 34, 38, 39, 41, 42, 43, stalled, lastLegal])]
        .filter(phase => Number.isInteger(phase) && phase >= firstPhase).sort((a, b) => a - b)
        .map(phase => {
            const attempted = data.Attempts.filter(x => x.Phase === phase);
            const accepted = attempted.filter(eligible);
            const measured = accepted.flatMap(x => x.Outcomes);
            const failed = attempted.flatMap(x => x.Outcomes.filter(r => !r.Accepted));
            const ids = data.Frontier.findLast(x => x.Phase === phase)?.Retained ?? [];
            const retained = ids.map(id => branches.get(id));
            return {
                ...data.Phases[phase], CandidateCount: attempted.length, LegalCandidates: accepted.length,
                EnteringHistories: new Set(attempted.map(x => x.Parent)).size,
                Retained: ids, RetainedDistinctParents: new Set(retained.map(x => x.Parent)).size,
                LegalBounds: Object.fromEntries(fields.map(field =>
                    [field, bounds(measured.map(x => x.Reserve[field]).filter(Number.isFinite))])),
                Failures: countBy(failed, x => x.Stop), FailedProgress: bounds(failed.map(x => x.End.S)),
            };
        });
    const finishers = locked.Holdouts.flatMap(x => x.Results.filter(r => r.Complete));
    arms[name] = {
        Budget: data.Budget, FirstPhase: firstPhase, LastLegalPhase: data.Phases[lastLegal],
        Periods: data.Phases.length, Termination: data.Termination, Best: data.Best, Winner: data.Winner,
        LockedCandidates: locked.Holdouts.length, RestoredPrefixes: data.Restored,
        WallSeconds: data.WallSeconds, CpuSeconds: data.CpuSeconds, PhaseTrials: data.PhaseTrials,
        NewBranches: branches.size, RestoredPhaseTrials: data.PhaseTrials - outcomes.length, ForecastSteps: data.ForecastSteps,
        FullFixtureTests: locked.Holdouts.length * 5, StrictFinishesInAnyLockedCandidate: finishers.length,
        FailureCounts: counts, Envelopes: envelopes, LockedResults: best.Results.map(stop),
    };
    assert.equal(data.NativeWorlds, 0);
}

const selected = read('selected-replays');
assert.equal(selected.plan.Branch, arms.pace.Best);
const racing = [];
for (const run of selected.Runs) {
    const rows = run.Trace;
    const legal = rows.slice(0, run.Complete ? rows.length : -1);
    const dt = (i) => i === 0 ? 0 : legal[i].Start.State.Time - legal[i - 1].Start.State.Time;
    const peak = Math.max(...legal.map(point => point.Start.State.Rider.Speed)) * 3.6;
    const last = legal.at(-1).Start.State.Rider;
    assert.equal(run.LastLegalS, last.TotalReferenceMeters);
    assert.equal(run.LegalSweep, Math.min(...legal.slice(1).map(p => p.Swept)));
    assert.deepEqual(stop(run), arms.pace.LockedResults.find(x => x.Fixture === run.Fixture));
    racing.push({
        Fixture: run.Fixture, LastLegalReference: last.TotalReferenceMeters, LastLegalTravel: last.DistanceMeters,
        MaxSpeedKmh: peak,
        SecondsAbove100OnStraights: sum(legal.map((p, i) =>
            !p.Start.State.Rider.Road.IsTurn && p.Start.State.Rider.Speed >= 100 / 3.6 ? dt(i) : 0)),
        SteeringRateSaturatedSteps: legal.filter(p => p.SteeringRateLimited).length,
        SteeringAngleSaturatedSteps: legal.filter(p => p.SteeringAngleLimited).length,
        Forces: Object.fromEntries(['FxRear', 'FyRear', 'NFront', 'Mz'].map(field =>
            [field, bounds(legal.map(p => p[field]).filter(Number.isFinite))])),
        LossesJoules: Object.fromEntries(['CrankDrag', 'Clutch', 'Transmission', 'RearSlip', 'Bearing']
            .map(field => [field, sum(legal.map(p => p.Losses?.[field] ?? 0))])),
    });
}
const timings = Object.fromEntries(['concurrent', 'isolated'].map(name => [
    name, read('model-guard-timing-' + name).Runs.map(({ Repeat, Statistics, Rejected, CpuSeconds, WallSeconds }) =>
        ({ Repeat, Statistics, Rejected, CpuSeconds, WallSeconds })),
]));
const nativeWorlds = readFileSync(join(directory, 'world-ledger.jsonl'), 'utf8').trim().split(/\r?\n/).filter(Boolean).length;
assert.equal(nativeWorlds, 0);
writeFileSync(join(directory, 'summary.json'), JSON.stringify({
    Study: 207, Base: 'ea6aaf21debab9f977cec1ed300d0932b0eeef3f', NativeWorlds: nativeWorlds,
    TrainingStarts: ['nominal', '-1 cm', '+1 cm'], EvaluationOffsetsCm: [0, -2, 2, -12, 12],
    SelectedArm: 'pace', SelectedBranch: selected.plan.Branch, Arms: arms, PartialSportingMeasures: racing,
    ModelGuardTiming: timings,
}, null, 2) + '\n');
