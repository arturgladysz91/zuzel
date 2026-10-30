import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import zlib from 'node:zlib';
import {sample156,endTime156} from './study156-model.mjs';
const root=new URL('../../',import.meta.url);
const data=JSON.parse(fs.readFileSync(new URL('docs/experiments/line-throttle-physics/replay.json',root)));
test('156 replay contains accepted native samples with their true endings',()=>{
 for(const run of data.runs){
  const raw=JSON.parse(zlib.gunzipSync(fs.readFileSync(new URL(`docs/experiments/line-throttle-physics/native/${run.name}.json.gz`,root))));
  const ss=raw.Riders[0].Samples.filter(s=>s.Input.Time>=run.start-1e-8);
  assert.equal(ss.length,run.samples.length);
  for(let i=0;i<ss.length;i++){const a=ss[i],b=run.samples[i];assert.equal(a.Input.Rider.Position.X,b.x);assert.equal(a.Input.Rider.Position.Y,b.y);assert.equal(a.Input.Rider.Heading,b.heading);assert.equal(a.Input.Active.Drive,b.drive);}
  const last=run.samples.at(-1);const after=sample156(run.samples,last.t+1);assert.equal(after.x,last.x);assert.equal(after.y,last.y);assert.equal(after.ended,true);
 }
});
test('156 interpolates circular heading and holds the actual active throttle',()=>{
 const a={t:0,x:0,y:0,speed:10,s:1,gap:1,beta:0,heading:179*Math.PI/180,drive:0};const b={...a,t:1,x:2,heading:-179*Math.PI/180,drive:1};const p=sample156([a,b],.5);assert.equal(p.x,1);assert.equal(p.drive,0);assert.ok(Math.abs(Math.abs(p.heading)-Math.PI)<1e-10);
});
test('156 timeline reaches the final native sample despite floating clock rounding',()=>{
 const samples=[{t:0,x:1},{t:13.05999999999998,x:2}];const end=endTime156([{samples}]);assert.equal(end,13.06);assert.ok(sample156(samples,end).ended);assert.equal(sample156(samples,end).x,2);
});
test('156 comparison table only promotes complete continuously certified native paths',()=>{
 const a=JSON.parse(fs.readFileSync(new URL('docs/experiments/line-throttle-physics/analysis.json',root)));
 assert.equal(a.matrix_results.length,18);
 for(const r of a.matrix_results.filter(r=>r.status==='PASS')){assert.ok(r.complete&&r.safe&&r.exact_initial&&r.family_achieved);assert.ok(r.sweep_m>=.5);assert.ok(r.max_beta_deg<=45);}
});
