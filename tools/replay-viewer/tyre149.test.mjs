import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {loadTyre149} from './tyre149.mjs';
import {review146} from './planning146.mjs';
const root=new URL('../../docs/experiments/tyre-physics-corner-limits/',import.meta.url);
const fetcher=async url=>new Response(fs.readFileSync(new URL(url.replace('local/',''),root)));
test('149 loads actual continuous A and B from rest and completed three laps',async()=>{
 for(const name of ['baseline149-final148','separated149-autonomous']){const d=await loadTyre149(fetcher,name,'local/');assert.equal(d.studyNumber,149);assert.equal(d.replay.state(1,0,false).speed,0);assert.equal(d.replay.state(1,100,false).completedLaps,3);assert.deepEqual(review146(d,22.6).actual_action,review146(d,22.6).selected[0]);}
});
test('149 physics separation is not presented as faster autonomous riding',async()=>{
 const a=JSON.parse(fs.readFileSync(new URL('analysis.json',root),'utf8'));const original=a.full_runs.find(x=>x.name==='baseline149-final148'),separated=a.full_runs.find(x=>x.name==='separated149-autonomous');assert.ok(separated.finish_s>original.finish_s);assert.ok(a.B_repeatability.samples_equal);assert.equal(a.corners.filter(x=>x.run===separated.name&&x.complete).length,6);
});
test('149 rolling fixture keeps its observed boundary stop and initial speed',async()=>{const d=await loadTyre149(fetcher,'entry-100-offset-10-B','local/');assert.ok(d.replay.state(1,0,false).speed>27);assert.equal(d.run.Outcome,'confirmed footprint crossing');assert.equal(d.run.Riders[0].FinishSeconds,null);});
test('149 sequence capability stop is a controller abstention with a legal observed prefix',async()=>{const d=await loadTyre149(fetcher,'planned-entry-90-outer-B','local/');assert.equal(d.run.EndSeconds,1);assert.match(d.run.Outcome,/STUDY146_ABSTAIN/);assert.ok(d.run.Riders[0].Samples.every(s=>s.Inner>0&&s.Outer>0));});
