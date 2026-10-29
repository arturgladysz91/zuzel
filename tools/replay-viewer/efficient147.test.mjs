import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {loadEfficient147} from './efficient147.mjs';
import {review146} from './planning146.mjs';
const root=new URL('../../docs/experiments/efficient-autonomous-planner/',import.meta.url);
const fetcher=async url=>new Response(fs.readFileSync(new URL(url.replace('local/',''),root)));
test('147 credits exactly three native crossings from a zero start',async()=>{
 const d=await loadEfficient147(fetcher,'autonomous147-shortlist','local/');assert.equal(d.studyNumber,147);assert.equal(d.replay.state(1,0,false).speed,0);assert.equal(d.replay.state(1,100,false).completedLaps,3);assert.equal(d.replay.state(1,100,false).finished,true);assert.equal(d.run.Riders[0].FinishSeconds,53.98976567401982);
});
test('147 has all six measured corners and all 270 durable decisions',async()=>{
 const d=await loadEfficient147(fetcher,'autonomous147-shortlist','local/');assert.equal(d.analysis146.corners.filter(c=>c.run===d.run.Name&&c.complete).length,6);assert.equal(d.analysis146.plan_audits[d.run.Name].reviews,270);assert.deepEqual(review146(d,22.6).selected[0],review146(d,22.6).actual_action);
});
test('147 keeps the partial 146 baseline partial instead of projecting a finish',async()=>{
 const d=await loadEfficient147(fetcher,'autonomous146-viable','local/');assert.equal(d.replay.state(1,100,false).completedLaps,2);assert.equal(d.replay.state(1,100,false).finished,false);
});
