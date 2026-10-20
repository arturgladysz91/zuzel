import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { Replay, parseCsv, angleAt, angleDelta, worldCapsules, crossSection, decisionStatus } from './model.mjs';

const geometry = JSON.parse(readFileSync(new URL('./geometry.json', import.meta.url), 'utf8'));
const fixture = name => {
  const folder = new URL('../../docs/experiments/autonomous-racing-line/', import.meta.url);
  const csv = readFileSync(new URL(name + '-accepted.csv', folder), 'utf8');
  const json = JSON.parse(readFileSync(new URL(name + '-ON.json', folder), 'utf8'));
  return { csv, json, replay: new Replay(csv, json) };
};
const near = (a, b, epsilon = 1e-8) => assert.ok(Math.abs(a - b) < epsilon, `${a} != ${b}`);
test('#103 twelve real replays retain yaw, own C2 boundaries, interval evidence and unsafe trajectories', () => {
  const folder = new URL('../../docs/experiments/progressive-bend-width/', import.meta.url);
  const evidence = JSON.parse(readFileSync(new URL('measurements.json', folder), 'utf8'));
  assert.equal(evidence.length, 12);
  const old = JSON.parse(readFileSync(new URL('../../docs/experiments/local-offset-limit/egg-mild-geometry.json', import.meta.url), 'utf8'));
  for (const row of evidence) {
    const m = row.Measurement, alias = m.Geometry + '-' + m.Plan.Name;
    const json = JSON.parse(readFileSync(new URL(alias + '.json', folder), 'utf8'));
    const csv = readFileSync(new URL(alias + '-accepted.csv', folder), 'utf8');
    const own = JSON.parse(readFileSync(new URL(json.GeometryFile, folder), 'utf8'));
    const replay = new Replay(csv, json);
    assert.equal(own.lapMeters, old.lapMeters); assert.equal(own.localOuterLimit.requiredOuterClearanceMeters, .5);
    own.segments.forEach((segment, i) => segment.sections.forEach((s, j) => {
      assert.deepEqual(s.reference, old.segments[i].sections[j].reference);
      assert.deepEqual(s.inner, old.segments[i].sections[j].inner);
    }));
    for (const pose of parseCsv(csv)) {
      const state = replay.state(1, pose.t); near(state.x, pose.x); near(state.y, pose.y); near(angleDelta(state.heading, pose.heading), 0);
    }
    if (m.Plan.Name === 'outer-max') {
      assert.equal(json.Evaluation.Feasible, false); assert.ok(row.FirstOuterViolation); assert.ok(row.WorstOuterState.Outer < 0);
      assert.ok(parseCsv(csv).some(p => p.offset > 11));
    } else {
      assert.equal(json.Evaluation.Feasible, true); assert.ok(m.ConservativeMinimumOuterClearanceMeters >= .5);
      assert.ok(row.ConservativeMinimumInnerClearanceMeters >= 0);
    }
  }
});
test('#102 own geometry, local footprint limit and rejected poses remain recorded data', () => {
  const folder = new URL('../../docs/experiments/local-offset-limit/', import.meta.url);
  for (const shape of ['egg-mild', 'baseline']) for (const plan of ['inner', 'middle-tighten', 'wide-tighten', 'outer-max', 'outer-tighten', 'middle-to-outer']) {
    const alias = `${shape}-${plan}`;
    const csv = readFileSync(new URL(alias + '-accepted.csv', folder), 'utf8');
    const json = JSON.parse(readFileSync(new URL(alias + '.json', folder), 'utf8'));
    const replay = new Replay(csv, json);
    const own = JSON.parse(readFileSync(new URL(json.GeometryFile, folder), 'utf8'));
    assert.equal(own.localOuterLimit.requiredOuterClearanceMeters, .5);
    assert.ok(Math.max(...own.localOuterLimit.sections.map(p => p.maximumSafeTargetOffsetMeters)) > 13.8);
    for (const row of parseCsv(csv)) {
      const state = replay.state(row.id, row.t); near(state.x, row.x); near(state.y, row.y); near(angleDelta(state.heading, row.heading), 0);
    }
    if (plan === 'outer-max' || plan === 'outer-tighten') {
      assert.equal(json.Evaluation.Feasible, false);
      assert.ok(json.Evaluation.MinimumOuterClearanceMeters < .5);
      assert.ok(parseCsv(csv).some(p => p.offset > 9.5));
    }
    for (const limit of own.localOuterLimit.sections) {
      const section = crossSection(own, limit.referenceMeters);
      const point = { x: section.point.x + section.normal.x * limit.maximumSafeTargetOffsetMeters,
        y: section.point.y + section.normal.y * limit.maximumSafeTargetOffsetMeters };
      assert.ok(Math.hypot(point.x - limit.point.x, point.y - limit.point.y) < .001);
    }
  }
});
test('#101 recorded poses use dataset geometry and preserve rejected inner trajectories', () => {
  const folder = new URL('../../docs/experiments/track-geometry-variants/', import.meta.url);
  for (const shape of ['baseline','egg-mild','egg-medium','egg-strong','egg-asymmetric']) for (const plan of ['inner','wide-tighten']) {
    const alias = `${shape}-${plan}`;
    const csv = readFileSync(new URL(alias + '-accepted.csv', folder), 'utf8');
    const json = JSON.parse(readFileSync(new URL(alias + '.json', folder), 'utf8'));
    const track = JSON.parse(readFileSync(new URL(json.GeometryFile, folder), 'utf8'));
    const replay = new Replay(csv, json);
    assert.equal(track.segments.length, 9); assert.ok(Math.abs(track.lapMeters-318.77875)<.00001);
    for (const row of parseCsv(csv)) {
      const state = replay.state(1,row.t); near(state.x,row.x); near(state.y,row.y); near(angleDelta(state.heading,row.heading),0);
      const section = crossSection(track,row.reference); assert.ok(Number.isFinite(section.point.x));
      const parts = worldCapsules(track,state); assert.equal(parts.length,2);
    }
    assert.equal(json.Run.Initial.RearWheels.length,1);
    assert.equal(replay.metrics.get(1).LapSeconds.length,3);
    if (shape !== 'baseline') assert.ok(track.source.includes(shape));
  }
});
test('#100 body replay remains compatible while independent wheel telemetry is retained', () => {
  const folder = new URL('../../docs/experiments/rear-wheel-dynamics/', import.meta.url);
  for (const name of ['old-A-neutral-inner', 'new-A-neutral-inner', 'new-A-neutral-wide-tighten', 'straight-launch-stress', 'launch-through-corner']) {
    const csv = readFileSync(new URL(name + '-accepted.csv', folder), 'utf8');
    const json = JSON.parse(readFileSync(new URL(name + '.json', folder), 'utf8'));
    const replay = new Replay(csv, json);
    for (const row of parseCsv(csv)) {const s = replay.state(row.id, row.t); near(s.x, row.x); near(s.y, row.y); near(angleDelta(s.heading, row.heading), 0);}
    if (name.startsWith('new-') || name.startsWith('straight-') || name === 'launch-through-corner') {
      assert.equal(json.Run.Initial.RearWheels.length, 1);
      assert.ok(json.Telemetry.some(t => t.SlipRatio > .2));
      assert.ok(json.Telemetry.every(t => Number.isFinite(t.Omega)));
    }
    assert.ok(Array.isArray(json.Run.Passes));
  }
});
test('#99 actual Box2D plans preserve every saved pose, spatial command and individual lap', () => {
  const folder = new URL('../../docs/experiments/physics-driven-racing-line/', import.meta.url);
  for (const alias of ['selected', 'inner', 'wide', 'rejected']) {
    const csv = readFileSync(new URL(alias + '-accepted.csv', folder), 'utf8');
    const json = JSON.parse(readFileSync(new URL(alias + '.json', folder), 'utf8'));
    const replay = new Replay(csv, json);
    assert.ok(Array.isArray(json.Run.Passes));
    for (const row of parseCsv(csv)) {const state = replay.state(row.id, row.t); near(state.x, row.x); near(state.y, row.y); near(angleDelta(state.heading, row.heading), 0);}
    for (const event of json.Run.Result.Decisions) {
      const state = replay.state(1, event.ActualActivation);
      assert.equal(state.target, event.TargetOffsetMeters); assert.equal(state.drive, event.Drive);
      assert.equal(event.ActualActivation, event.ObservedAt);
    }
    assert.equal(json.Run.Riders[0].Measurement.LapSeconds.length, 3);
    assert.equal(replay.state(1, replay.duration).completedLaps, 3);
    assert.equal(json.Evaluation.Feasible, alias !== 'rejected');
  }
});
test('Both unmodified CSV/JSON pairs load initial time zero and every recorded point', () => {
  for (const [name, riders] of [['rolling-S3-entry', 1], ['rolling-straight', 4]]) {
    const { csv, json, replay } = fixture(name), original = JSON.stringify(json);
    assert.equal(replay.tracks.size, riders);
    for (const v of json.Run.Initial.Riders) { const s = replay.state(v.Id, 0); assert.equal(s.x, v.Position.X); assert.equal(s.y, v.Position.Y); }
    for (const row of parseCsv(csv)) { const s = replay.state(row.id, row.t); near(s.x, row.x); near(s.y, row.y); near(angleDelta(s.heading, row.heading), 0); }
    assert.equal(JSON.stringify(json), original);
    assert.throws(() => parseCsv('time_s,rider\n0,1'), /wymaganych/);
  }
});
test('Bike capsules keep real dimensions and CSV body heading, independently of velocity', () => {
  const { replay } = fixture('rolling-S3-entry');
  const s = [...replay.tracks.values()][0].find(x => Math.abs(angleDelta(Math.atan2(x.vy, x.vx), x.heading)) > .1);
  assert.ok(s); const parts = worldCapsules(geometry, s), body = parts[0], bar = parts[1];
  near(Math.hypot(body.end.x - body.start.x, body.end.y - body.start.y) + body.radiusMeters * 2, 2.10);
  near(body.radiusMeters * 2, .30); near(Math.hypot(bar.end.x - bar.start.x, bar.end.y - bar.start.y) + bar.radiusMeters * 2, .80);
  near(angleDelta(s.heading, Math.atan2(body.end.y - body.start.y, body.end.x - body.start.x)), 0);
  const rotated = worldCapsules(geometry, { x: 4, y: 8, heading: Math.PI / 2 });
  near(rotated[1].start.y, 8.65); near(rotated[1].end.y, 8.65);
});
test('Commands activate only at ActualActivation; cancelled plans and the other channel cannot execute', () => {
  const { replay } = fixture('rolling-S3-entry');
  near(replay.state(1, .339).target, 7); near(replay.state(1, .34).target, 5.5); near(replay.state(1, .34).offset, 7);
  const { replay: pack } = fixture('rolling-straight');
  const cancelled = pack.decisions.find(d => d.CancelledAt != null);
  assert.equal(decisionStatus(cancelled, cancelled.CancelledAt), 'cancelled');
  assert.equal(pack.events().filter(e => e.decision === cancelled && e.kind === 'activated').length, 0);
  const { json, csv } = fixture('rolling-S3-entry');
  json.Run.Result.Decisions = [
    { RiderId: 1, ObservedAt: .01, RequestedActivation: .02, ActualActivation: .3, CancelledAt: null, TargetOffsetMeters: 9, Drive: .2, Channels: 2, Intent: 6 },
    { RiderId: 1, ObservedAt: .01, RequestedActivation: .02, ActualActivation: null, CancelledAt: .1, TargetOffsetMeters: 2, Drive: 0, Channels: 1 },
    { RiderId: 1, ObservedAt: .01, RequestedActivation: .02, ActualActivation: .305, CancelledAt: null, TargetOffsetMeters: 6, Drive: 1, Channels: 1, Intent: 1 }
  ];
  const synthetic = new Replay(csv, json);
  assert.equal(decisionStatus(synthetic.decisions[0], .1), 'waiting');
  near(synthetic.state(1, .299).drive, 1); near(synthetic.state(1, .3).drive, .2); near(synthetic.state(1, .3).target, 7);
  near(synthetic.state(1, .305).drive, .2); near(synthetic.state(1, .305).target, 6); assert.equal(synthetic.state(1, .305).intent, 1);
});
test('Shortest-arc angles interpolate across pi while discrete controls and missing tails do not', () => {
  near(Math.abs(angleAt(Math.PI - .1, -Math.PI + .1, .5)), Math.PI);
  const { replay } = fixture('rolling-S3-entry'); const points = replay.tracks.get(1), a = points[12], b = points[13];
  const s = replay.state(1, (a.t + b.t) / 2); near(s.x, (a.x + b.x) / 2); near(s.y, (a.y + b.y) / 2);
  const last = points.at(-1), beyond = replay.state(1, replay.duration + 100);
  near(beyond.x, last.x); near(beyond.y, last.y); near(angleDelta(beyond.heading, last.heading), 0); assert.ok(beyond.stale);
  assert.equal(replay.step(0, -1), null); assert.equal(replay.step(0, 1), .01);
});
test('Individual rolling crossings and finish times never substitute final-world positions for missing poses', () => {
  const { replay } = fixture('rolling-straight');
  for (const [id, points] of replay.tracks) {
    const finish = replay.result.FinishTimes[String(id)], crossings = replay.metrics.get(id).CrossingSeconds;
    assert.equal(replay.state(id, crossings[0] - 1e-6).lap, 1); assert.equal(replay.state(id, crossings[0]).lap, 2);
    assert.equal(replay.state(id, finish - 1e-6).finished, false);
    const s = replay.state(id, finish), accepted = points.filter(p => p.t <= finish).at(-1);
    assert.equal(s.finished, true); assert.equal(s.lap, 4); near(s.x, accepted.x); near(s.y, accepted.y);
    near(replay.state(id, replay.duration).x, points.at(-1).x);
  }
  assert.equal(replay.events().filter(e => e.kind === 'pass').length, 2);
  assert.equal(replay.events().filter(e => e.kind === 'contact').length, 0);
});
test('Target cross-sections read exported model positions/normals; offset zero is a metre outside the inner edge', () => {
  assert.equal(geometry.innerReferenceOffsetMeters, 1);
  for (const segment of geometry.segments) {
    const sample = segment.sections[4], section = crossSection(geometry, sample.referenceMeters);
    near(section.point.x, sample.reference.x); near(section.point.y, sample.reference.y);
    near(Math.hypot(sample.inner.x - section.point.x, sample.inner.y - section.point.y), 1);
    near(Math.hypot(sample.outer.x - sample.inner.x, sample.outer.y - sample.inner.y), segment.physicalWidthMeters);
  }
  assert.equal(geometry.enums.channels['1'], 'Line'); assert.equal(geometry.enums.channels['2'], 'Throttle');
});
