import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {gunzipSync} from 'node:zlib';
import {loadRobust,robustReplay} from './robust-learning.mjs';
import {awarenessReadout,knowledgeEdges,actuatorEnvelope} from './track-awareness.mjs';
const root='../../docs/experiments/track-awareness-anticipatory-riding/';
const read=n=>JSON.parse(fs.readFileSync(new URL(root+n,import.meta.url)));
const index=read('index.json'),runs=JSON.parse(gunzipSync(fs.readFileSync(new URL(root+'selected-replays.json.gz',import.meta.url)))).Runs;

test('Track awareness loads actual per-group geometry and identical native initial states',async()=>{
  const urls=[];const fetcher=async path=>{urls.push(path);return new Response(fs.readFileSync(new URL(path,import.meta.url)));};
  for(const group of index.Groups){
    const data=await loadRobust(fetcher,group.Runs[0].Name,root,group.Id);
    assert.ok(urls.includes(root+group.GeometryFile));
    assert.deepEqual(data.geometry,read(group.GeometryFile));
    for(const r of group.Runs){
      const run=runs[r.Name];assert.equal(group.GeometryFile,run.GeometryName+'-geometry.json');
      const a=data.document.Replay.Samples[0].Input,b=run.Replay.Samples[0].Input;
      for(const key of ['Rider','Steering','Omega','Active'])assert.deepEqual(a[key],b[key]);
      const replay=robustReplay(run);
      for(const s of run.Replay.Samples){const state=replay.state(1,s.Input.Time,false);assert.equal(state.x,s.Input.Rider.Position.X);assert.equal(state.y,s.Input.Rider.Position.Y);assert.equal(state.target,null);}
      if(!run.Metrics.Native.Completed)assert.equal(run.Metrics.Native.GateSeconds,null);
    }
  }
});

test('Knowledge overlay represents observed geometry edges without a target trajectory',()=>{
  let observations=0;
  const sections=new Map();
  for(const run of Object.values(runs))if(!sections.has(run.GeometryName))sections.set(run.GeometryName,new Map(read(run.GeometryName+'-geometry.json').segments.flatMap(s=>s.sections).map(s=>[s.referenceMeters.toFixed(7),s])));
  for(const run of Object.values(runs))for(const d of run.Replay.Decisions){
    const r=awarenessReadout(run,d.Time);assert.strictEqual(r.decision,d);
    if(!d.TrackKnowledge){assert.equal(r.knowledge,null);assert.deepEqual(knowledgeEdges(null),[]);continue;}
    observations++;assert.strictEqual(r.knowledge,d.TrackKnowledge);
    const edges=knowledgeEdges(r.knowledge);assert.equal(edges.length,r.knowledge.Profile.length);
    for(const [i,p] of r.knowledge.Profile.entries()){
      assert.equal(edges[i].inner.x,p.X-p.OutwardX);assert.equal(edges[i].inner.y,p.Y-p.OutwardY);
      assert.ok(Math.abs(Math.hypot(edges[i].outer.x-edges[i].inner.x,edges[i].outer.y-edges[i].inner.y)-p.WidthMeters)<1e-5);
      const native=sections.get(run.GeometryName).get(p.ReferenceMeters.toFixed(7));
      if(native)for(const side of ['inner','outer'])assert.ok(Math.hypot(edges[i][side].x-native[side].x,edges[i][side].y-native[side].y)<.0001);
    }
    assert.ok(r.reachability.Classification.includes('heuristic'));
    if(r.activation)assert.ok(Math.abs(r.activation.ActivationAt-d.Time-.24)<1e-7);
  }
  assert.ok(observations>0);
});

test('Required #134 examples retain continuous worlds, transfer and failed counterfactual',()=>{
  for(const id of ['M00','N00','T06','AB-P1','AB-P3','timing-P3-020-drive','timing-P3-040-drive'])assert.ok(index.Groups.some(g=>g.Id===id),id);
  const two=index.Groups.find(g=>g.Id==='N00').Runs.map(r=>runs[r.Name]);
  assert.ok(two.some(r=>r.Replay.Samples.some(s=>s.Input.Rider.Road.Segment===4)&&r.Replay.Samples.some(s=>s.Input.Rider.Road.Segment>=5)));
  for(const run of two)for(let i=1;i<run.Replay.Samples.length;i++)assert.ok(run.Replay.Samples[i].Input.Time>run.Replay.Samples[i-1].Input.Time);
  assert.equal(runs['timing-P3-020-drive'].Metrics.Native.Completed,false);
  assert.equal(runs['timing-P3-040-drive'].Metrics.Native.Completed,true);
});

test('Actuator diagnostics retain pending motion before new-command latency and match native prefixes',()=>{
  const input={Time:0,Steering:0,Active:{SteeringRadians:0},Pending:[{ObservedAt:-.2,ActivationAt:.04,Action:{SteeringRadians:Math.PI/12}}]};
  const range=actuatorEnvelope(input,.1);assert.ok(Math.abs(range.low-.06*Math.PI/2)<1e-10);assert.equal(range.low,range.high);
  let compared=0;
  for(const run of Object.values(runs))for(const d of run.Replay.Decisions.filter((_,i)=>i%10===0)){
    const start=run.Replay.Samples.find(s=>Math.abs(s.Input.Time-d.Time)<1e-8),end=run.Replay.Samples.find(s=>Math.abs(s.Input.Time-d.Time-.23)<1e-8);
    if(!start||!end)continue;
    const q=actuatorEnvelope(start.Input,.23);assert.ok(Math.abs(q.low-end.Input.Steering)<1e-5);assert.ok(Math.abs(q.high-end.Input.Steering)<1e-5);compared++;
  }
  assert.ok(compared>100);
});
