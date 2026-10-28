import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {loadPlanning146,review146} from './planning146.mjs';
const root=new URL('../../docs/experiments/autonomous-corner-planning/',import.meta.url);
const fetcher=async url=>new Response(fs.readFileSync(new URL(url.replace('local/',''),root)));
test('146 veto remains a DNF with no extrapolated lap or command',async()=>{
  const d=await loadPlanning146(fetcher,'autonomous146-first','local/');
  assert.equal(d.replay.state(1,0,false).speed,0);
  assert.equal(d.replay.state(1,100,false).finished,false);
  assert.equal(d.replay.state(1,100,false).completedLaps,0);
  const last=review146(d,100);assert.match(last.reason,/abstain/);assert.equal(last.actual_action,null);assert.equal(last.selected,null);
  assert.equal(d.analysis146.corners.filter(c=>c.run===d.run.Name&&c.complete).length,1);
});
test('146 submitted plan actions use real native activations and full prefixes',async()=>{
  const d=await loadPlanning146(fetcher,'local-K1-29','local/');const a=d.analysis146.plan_audits[d.run.Name];
  assert.equal(a.submitted_match,true);assert.equal(a.activation_match,true);assert.equal(a.queues_match,true);
  const row=review146(d,29);assert.equal(row.time_s<29.000001,true);
  assert.ok(Math.abs(row.actual_activation.ActivationAt-row.time_s-.24)<1e-8);
  assert.deepEqual(row.selected[0],row.actual_action);
  assert.ok(d.curves146.find(c=>c.name==='F').points.length>1000);
});
