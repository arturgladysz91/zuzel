import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {loadConstraints143,constraintsReadout} from './constraints143.mjs';
const root=new URL('../../docs/experiments/racing-constraints-speed/',import.meta.url);
const fetcher=async url=>new Response(fs.readFileSync(new URL(url.replace('local/',''),root)));
test('143 loader preserves full native scope, decisions and both completed/failed counterexamples',async()=>{
  const data=await loadConstraints143(fetcher,'H4-b-F','local/','H4-b');
  assert.match(data.replay.episodeScope,/62m/);
  assert.equal(data.group.Summary.F.Completed,false);
  assert.equal(data.group.Summary.K2.Completed,true);
  assert.equal(data.group.Summary.F.FullTimeSeconds,null);
  assert.equal(data.constraintsDecisions.F.length,data.group.Summary.F.DecisionCpu.Count);
  const last=data.document.Replay.Samples.at(-1),readout=constraintsReadout(data,100).find(r=>r.policy==='F');
  assert.equal(readout.sample,last);assert.equal(readout.ended,true);
  const end=data.replay.state(1,data.replay.duration+10),actual=data.replay.state(1,data.replay.duration);
  assert.equal(end.x,actual.x);assert.equal(end.y,actual.y);
  assert.equal(data.comparisons.find(r=>r.policy==='F').points.length,data.document.Replay.Samples.length);
  assert.equal(data.replay.constraintsData,data);
});
test('143 native readout never advances to a later sample or a forecast pose',async()=>{
  const data=await loadConstraints143(fetcher,'H0-B','local/','H0');
  for(const r of constraintsReadout(data,6.713)){
    assert.ok(r.sample.Input.Time<=6.713);assert.ok(r.decision.Time<=6.713);
    assert.ok(r.run.Replay.Samples.includes(r.sample));
  }
  assert.equal(data.group.Summary.B.Completed,true);
  assert.ok(data.group.Summary.B.FullTimeSeconds>data.group.Summary.B.HistoricalGateSeconds);
});
