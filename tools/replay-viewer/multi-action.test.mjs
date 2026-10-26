import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import zlib from 'node:zlib';
import {planningReadout} from './multi-action.mjs';
import {loadRobust} from './robust-learning.mjs';
const root=new URL('../../docs/experiments/multi-action-planning/',import.meta.url);
const index=JSON.parse(fs.readFileSync(new URL('index.json',root)));
const bundle=JSON.parse(zlib.gunzipSync(fs.readFileSync(new URL('selected-replays.json.gz',root))));
test('135 sequence readout uses recorded requests and activations, never future poses',()=>{
  const g=index.Groups[0];const run=bundle.Runs[g.Runs.find(r=>r.Policy==='C').Name];const plan=g.Plans.find(p=>p.Arm==='C');
  const before=planningReadout(plan,run,plan.Time+.23);assert.equal(before.events[0].status,'pending');assert.equal(before.events[2].status,'przyszłe żądanie');assert.ok(before.sample.Input.Time<=plan.Time+.23+1e-8);
  const after=planningReadout(plan,run,plan.Time+.65);for(const e of after.events)assert.ok(Math.abs(e.actual-e.activation)<1e-8);
});
test('135 selected native bundle retains exact original prefices, full geometry and DNF endpoints',()=>{
  assert.ok(index.Groups.some(g=>g.Runs.some(r=>!bundle.Runs[r.Name].Replay.Metrics.Completed)));
  assert.ok(index.Groups.some(g=>g.GeometryFile.startsWith('egg-asymmetric')));
  for(const g of index.Groups){
    const geometry=JSON.parse(fs.readFileSync(new URL(g.GeometryFile,root)));assert.equal(geometry.segments[0].lengthReferenceMeters,35);assert.equal(geometry.segments[8].lengthReferenceMeters,27);
    for(const r of g.Runs){const run=bundle.Runs[r.Name];assert.equal(run.GeometryName+'-geometry.json',g.GeometryFile);assert.ok(Math.abs(run.Replay.Samples.at(-1).Input.Time-run.Replay.Metrics.DurationSeconds)<1e-7);}
  }
});
test('135 common loader selects per-case geometry and does not extrapolate failed native motion',async()=>{
  const fetcher=async url=>{
    const filename=url.split('/').at(-1),data=fs.readFileSync(new URL(filename,root));
    return new Response(data);
  };
  const group=index.Groups.find(g=>g.GeometryFile.startsWith('egg-asymmetric'));const data=await loadRobust(fetcher,group.Runs[1].Name,'local/',group.Id);
  assert.equal(data.document.GeometryName,'egg-asymmetric');assert.equal(data.geometry.segments.length,9);assert.equal(data.comparisons.length,4);
  const end=data.replay.state(1,data.replay.duration+2);const last=data.replay.state(1,data.replay.duration);assert.equal(end.x,last.x);assert.equal(end.y,last.y);
});
