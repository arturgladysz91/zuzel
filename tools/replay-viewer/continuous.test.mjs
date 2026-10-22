import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { gunzipSync } from 'node:zlib';
import { continuousReplay, continuousPlans, numericRows, loadContinuous, continuousFiles } from './continuous.mjs';
import { motionVectors, bendJumps, crossSection } from './model.mjs';

const root = new URL('../../docs/experiments/continuous-corner-planning/', import.meta.url);
const read = name => readFileSync(new URL(name, root));
const unzip = name => gunzipSync(read(name)).toString();
const csv = unzip('continuous.csv.gz'), planCsv = unzip('continuous-plan.csv.gz');
const document = JSON.parse(read('continuous.json')), changes = JSON.parse(read('changes.json'));
const rows = numericRows(csv, ['t','dt','s','x','y','heading','vx','vy','speed','drive','steer_before','steer_after','beta']);
const plans = numericRows(planCsv, ['t','s','offset','target','path_x','path_y']);
const shape = JSON.parse(readFileSync(new URL(continuousFiles.at(-1), import.meta.url)));
const near = (a,b,e=1e-8) => assert.ok(Math.abs(a-b)<e, `${a} != ${b}`);
const fixture = () => continuousReplay(csv, planCsv, document, changes);

test('latest: every native pose, time, velocity, Drive and steering survives import unchanged', () => {
  const original = JSON.stringify(document), replay = fixture();
  assert.equal(rows.length, 2002); assert.equal(replay.tracks.size, 1); assert.equal(replay.duration, 19.98);
  assert.equal(replay.sampleTimes.length, 2003);
  for (const [i,r] of rows.entries()) {
    const state = replay.state(1,r.t,false);
    for (const k of ['x','y','vx','vy','speed','drive']) assert.equal(state[k],r[k]);
    near(state.heading,r.heading); assert.equal(state.sampleTime,r.t);
    assert.equal(state.reference,r.s); assert.equal(state.steering,r.steer_before);
    assert.equal(state.offset,plans[i].offset); assert.equal(state.target,plans[i].target);
    near(motionVectors(state).beta,r.beta,3e-7);
  }
  assert.equal(JSON.stringify(document),original);
  const end=replay.state(1,19.98,false), saved=document.Result.Riders[0];
  assert.equal(end.x,saved.Position.X); assert.equal(end.y,saved.Position.Y); near(end.heading,saved.Heading);
  assert.equal(end.steering,rows.at(-1).steer_after); assert.equal(end.source,'Result.Riders');
});

test('latest: active throttle agrees with all #123 activations and independent #124 audit', () => {
  const replay=fixture(), commands=document.Result.Decisions.filter(d=>d.ActualActivation!=null);
  const audit=numericRows(readFileSync(new URL('../../docs/experiments/speed-line-rider-root-cause/activation-timeline.csv',import.meta.url),'utf8'),['Decision','Activation','Drive']);
  assert.equal(commands.length,audit.length);
  for (const [i,d] of commands.entries()) {
    near(d.ActualActivation,audit[i].Activation); near(d.Drive,audit[i].Drive);
    const index=rows.findIndex(r=>Math.abs(r.t-d.ActualActivation)<1e-8);
    assert.ok(index>0);
    near(replay.state(1,rows[index].t,false).drive,d.Drive,1e-7);
    assert.equal(replay.state(1,rows[index].t-1e-7,false).drive,rows[index-1].drive);
    near(d.RequestedActivation-d.ObservedAt,.24);
  }
  // Commands which activate despite a newer zero intent in #124 remain visible.
  near(replay.state(1,.239,false).drive,1); near(replay.state(1,.24,false).drive,.05825833,1e-7);
  near(replay.state(1,10.15,false).drive,.485416,1e-7);
  near(replay.state(1,10.34,false).drive,.63541603,1e-7);
  assert.notEqual(replay.state(1,10.15,false).drive,replay.state(1,10.15,false).lastDecision.Drive);
  for(const r of rows){
    const active=commands.filter(d=>d.ActualActivation<=r.t+1e-9).at(-1);
    near(r.drive,active?.Drive??document.Initial.Riders[0].Drive,1e-7);
  }
});

test('latest: plan changes exactly at 10.15, preserving continuous snapshots and pending throttle', () => {
  const replay=fixture();
  assert.equal(replay.planAt(9.91),1); assert.equal(replay.planAt(10.15-1e-9),1); assert.equal(replay.planAt(10.15),2);
  const state=replay.state(1,10.15,false), saved=changes.find(c=>c.Phase==='activated');
  near(state.x,saved.Rider.Position.X,1e-5); near(state.y,saved.Rider.Position.Y,1e-5);
  near(state.heading,saved.Rider.Heading,1e-6); near(state.steering,saved.Steering,1e-7);
  near(state.lastDecision.RequestedActivation,saved.Pending.RequestedActivation);
  near(state.lastDecision.Drive,saved.Pending.Drive);
  assert.equal(replay.events().filter(e=>e.kind==='planActivated')[0].t,10.15);
  const first=gunzipSync(readFileSync(new URL('../../docs/experiments/feasible-path-speed-planning/planned-geometry.csv.gz',import.meta.url))).toString();
  const overlays=continuousPlans(first,unzip('diagnostic-geometry.csv.gz'),changes);
  assert.equal(overlays[0].generation,1); assert.equal(overlays[1].generation,2);
  assert.ok(overlays[1].points.at(-1).s>292);
  for (const p of plans) {
    const points=overlays[replay.planAt(p.t)-1].points, index=points.findIndex(q=>q.s>p.s);
    if(index<=0) continue; // The original export has a finite, documented extent.
    const a=points[index-1],b=points[index],f=(p.s-a.s)/(b.s-a.s);
    near(a.x+(b.x-a.x)*f,p.path_x,.002); near(a.y+(b.y-a.y)*f,p.path_y,.002);
  }
  for(let i=1;i<replay.sampleTimes.length;i++) {
    const a=replay.sampleTimes[i-1],b=replay.sampleTimes[i];
    assert.ok(b-a<=.01000001); assert.equal(replay.step(a,1),b); assert.equal(replay.step(b,-1),a);
  }
});

test('latest: recorded plan and Egg Mild edges align, both bends navigate without invented poses', () => {
  const replay=fixture(), track=replay.tracks.get(1), jumps=bendJumps(shape,track);
  assert.deepEqual(jumps,[1.45,13.03]);
  assert.equal(shape.frameId,'track-shape-101:progressive-b');
  for(const [i,r] of rows.entries()) {
    const section=crossSection(shape,r.s), p=plans[i];
    // Projection onto the sampled exported cross-section agrees with saved offset.
    near((r.x-section.point.x)*section.normal.x+(r.y-section.point.y)*section.normal.y,p.offset,.002);
  }
  for(const t of jumps) assert.ok(track.some(p=>p.t===t));
  const diagnostic=numericRows(unzip('diagnostic-geometry.csv.gz'),['s','inner_x','inner_y','outer_x','outer_y']);
  for(const p of diagnostic) {
    const section=crossSection(shape,p.s);
    near(section.point.x-section.normal.x,p.inner_x,.002); near(section.point.y-section.normal.y,p.inner_y,.002);
    // Both exported boundaries, including the locally varying bend width.
    const sections=shape.segments.flatMap(s=>s.sections);
    const index=sections.findIndex(s=>s.referenceMeters>p.s), a=sections[index-1], b=sections[index];
    const f=(p.s-a.referenceMeters)/(b.referenceMeters-a.referenceMeters);
    near(a.outer.x+(b.outer.x-a.outer.x)*f,p.outer_x,.002);
    near(a.outer.y+(b.outer.y-a.outer.y)*f,p.outer_y,.002);
  }
});

test('latest: smoothing is optional, controls stay stepped and the recorded endpoint never extrapolates', () => {
  const replay=fixture(), at=3.005, exact=replay.state(1,at,false), smooth=replay.state(1,at,true);
  assert.equal(exact.sampleTime,3); assert.equal(exact.interpolated,false); assert.equal(smooth.interpolated,true);
  assert.equal(smooth.drive,exact.drive); assert.equal(smooth.steering,exact.steering);
  assert.equal(replay.state(1,100,false).x,document.Result.Riders[0].Position.X);
  assert.equal(replay.step(19.98,1),null);
  assert.throws(()=>continuousReplay(csv,planCsv.replace('path_x','missing'),document,changes),/Brak wymaganych/);
  assert.throws(()=>continuousReplay(csv,planCsv.split('\n').slice(0,10).join('\n'),document,changes),/Niekompletne/);
});

test('latest: browser loader reads original gzip/JSON only and fails on missing evidence',async()=>{
  const result=await loadContinuous(async url=>new Response(readFileSync(new URL(url,import.meta.url))));
  assert.equal(result.replay.tracks.get(1).length,2003); assert.equal(result.comparisons.length,2);
  await assert.rejects(()=>loadContinuous(async()=>new Response('',{status:404})),/Brak oryginalnych/);
});
