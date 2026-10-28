import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {loadCorner145,cornerWindow} from './corner145.mjs';
import {standingReadout} from './standing144.mjs';
const root=new URL('../../docs/experiments/corner-speed-loss/',import.meta.url);
const fetcher=async url=>new Response(fs.readFileSync(new URL(url.replace('local/',''),root)));
test('145 extends three-lap native replay and compares all original variants at the same corner',async()=>{
  const d=await loadCorner145(fetcher,'K1','local/');
  assert.equal(d.run.Name,'K1');assert.equal(d.replay.state(1,0,false).speed,0);
  const q=standingReadout(d,34.67,1);assert.ok(Math.abs(q.sample.Input.Rider.Speed*3.6-46.1601792)<1e-5);
  const series=cornerWindow(d.cornerCurves,2,2);
  for(const n of ['A','B','F','K1'])assert.ok(series.find(q=>q.name===n).points.length>80);
  for(const s of series)for(const p of s.points){assert.equal(p.lap,2);assert.equal(p.bend,2);}
  assert.equal(d.replay.state(1,100,false).completedLaps,3);
});
test('145 failed autonomous trial ends at actual failure and is never extrapolated to finish',async()=>{
  const d=await loadCorner145(fetcher,'autonomous-two-command-K1','local/');
  assert.equal(d.run.Outcome,'confirmed footprint crossing');assert.equal(d.replay.state(1,100,false).finished,false);
  assert.equal(d.replay.state(1,100,false).completedLaps,0);
  const curve=d.cornerCurves.find(q=>q.name===d.run.Name);assert.equal(curve.points.at(-1).t,d.run.EndSeconds);
  assert.equal(cornerWindow([curve],2,2)[0].points.length,0);
  assert.equal(curve.points.at(-1).x,d.run.Riders[0].Samples.at(-1).Input.Rider.Position.X);
});
