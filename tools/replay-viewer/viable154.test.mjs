import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import zlib from 'node:zlib';
import {standingReplay} from './standing144.mjs';
import {completion154,loadViable154} from './viable154.mjs';
const root=new URL('../../docs/experiments/viable-safe-contingency/',import.meta.url);
const json=name=>JSON.parse(fs.readFileSync(new URL(name,root)));
const fetcher=async url=>new Response(fs.readFileSync(new URL(url.replace('local/',''),root)));
test('154 saved native replay starts from rest and labels every failed gate',async()=>{const d=await loadViable154(fetcher,null,'local/');assert.equal(d.studyNumber,154);assert.equal(d.run.Riders[0].Samples[0].Input.Rider.Speed,0);assert.equal(d.variant154,'S0');assert.match(d.index.Note,/PARTIAL/);});
test('154 no stopped prefix becomes a legal three-lap finish',()=>{const a=json('analysis.json');for(const r of a.full_runs){if(r.finish_s==null)assert.equal(r.full_s_pass,false);if(r.full_s_pass){assert.equal(r.laps,3);assert.ok(r.minimum_sweep_m>=.5);assert.equal(r.half_meter_pose_violations,0);}}});
test('154 completed safety requires every recorded swept interval certificate',()=>{const run={Outcome:'completed',Contacts:[],Riders:[{CompletedLaps:3,FinishSeconds:60,Samples:[{Inner:1,Outer:1,Certificate:null},{Inner:1,Outer:1,Certificate:.6}]}]};assert.equal(completion154(run),true);run.Riders[0].Samples[1].Certificate=null;assert.equal(completion154(run),false);run.Riders[0].Samples[1].Certificate=.49;assert.equal(completion154(run),false);});
test('154 complete policy keeps all finite observation signs and nine reserves',()=>{const x=JSON.parse(zlib.gunzipSync(fs.readFileSync(new URL('S0-A-v1.json.gz',root))));const r=x.Review;assert.equal(r.Tree.length,13);assert.equal(r.Terminals.length,9);assert.equal(r.Independent,true);assert.equal(r.ConditionalCertificate,true);});

test('154 rejection before movement displays the one actual time-zero sample',async()=>{const d=await loadViable154(fetcher,'S1-B-v2','local/');assert.equal(d.replay.duration,0);assert.deepEqual(d.replay.sampleTimes,[0]);const r=d.run.Riders[0];assert.equal(d.replay.state(r.Spec.Profile.Id,10).sampleTime,0);assert.equal(completion154(d.run),false);});
test('154 a positive-duration native run still requires later real samples',()=>{const r=JSON.parse(zlib.gunzipSync(fs.readFileSync(new URL('native/S1-B-v2.json.gz',root))));r.EndSeconds=.1;assert.throws(()=>standingReplay(r),/Brak próbek/);});
