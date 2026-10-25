import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {gunzipSync} from 'node:zlib';
import {loadRobust,robustReplay,comparisonSeries} from './robust-learning.mjs';
const root='../../docs/experiments/rider-decision-optimization/';
const read=n=>JSON.parse(fs.readFileSync(new URL(root+n,import.meta.url)));
const index=read('index.json'),runs=JSON.parse(gunzipSync(fs.readFileSync(new URL(root+'selected-replays.json.gz',import.meta.url)))).Runs;
test('Rider decision viewer compares identical native initial states with explicit failures',async()=>{
  const fetcher=async path=>new Response(fs.readFileSync(new URL(path,import.meta.url)));
  for(const group of index.Groups){
    const data=await loadRobust(fetcher,group.Runs[0].Name,root,group.Id);
    assert.equal(data.comparisonRuns.length,group.Runs.length);
    assert.equal(new Set(data.comparisons.map(c=>c.color)).size,group.Runs.length);
    for(const r of group.Runs){const run=runs[r.Name];
      const initial=run.Replay.Samples[0].Input,reference=data.document.Replay.Samples[0].Input;
      for(const k of ['Rider','Steering','Omega','Active'])assert.deepEqual(initial[k],reference[k]);
      assert.deepEqual(initial.Pending.filter(c=>c.ObservedAt<initial.Time-1e-8),reference.Pending.filter(c=>c.ObservedAt<reference.Time-1e-8));
      const replay=robustReplay(run);for(const s of run.Replay.Samples){const state=replay.state(1,s.Input.Time,false);assert.equal(state.x,s.Input.Rider.Position.X);assert.equal(state.y,s.Input.Rider.Position.Y);assert.equal(state.drive,s.Input.Rider.Drive);assert.equal(state.target,null);}
      if(!run.Metrics.Native.Completed)assert.equal(run.Metrics.Native.GateSeconds,null);
    }
    const gain=data.comparisons.find(c=>c.policy==='133-B1');
    for(const point of gain?.gainSamples??[]){
      const newer=runs[`final-133-B1-${group.Id}`].Replay.Samples.find(s=>s.Input.Rider.TotalReferenceMeters>=point.ReferenceMeters).Input;
      const older=runs[`final-131-H09-${group.Id}`].Replay.Samples.find(s=>s.Input.Rider.TotalReferenceMeters>=point.ReferenceMeters).Input;
      assert.deepEqual(point.NewXY,newer.Rider.Position);assert.equal(point.NewTime,newer.Time);assert.deepEqual(point.OldXY,older.Rider.Position);assert.equal(point.OldTime,older.Time);
      assert.equal(point.GainSeconds,older.Time-newer.Time);
    }
  }
});
test('Comparative charts use every actual sample and retain a common native clock',()=>{
  for(const run of Object.values(runs)){
    const series=comparisonSeries(run);assert.equal(series.length,7);
    for(const chart of series){assert.equal(chart.points.length,run.Replay.Samples.length);assert.equal(chart.points.at(-1)[0],run.Replay.Samples.at(-1).Input.Time);}
    for(const [i,s] of run.Replay.Samples.entries())assert.deepEqual(series.map(c=>c.points[i][1]),[s.Input.Rider.Speed*3.6,s.RequestedDrive*100,s.Input.Active.Drive*100,s.RequestedSteering*180/Math.PI,s.Input.Steering*180/Math.PI,s.Input.Rider.SlideRadians*180/Math.PI,Math.min(s.Inner,s.Outer)]);
  }
});
test('Switching policies reuses the frozen native bundle and retains the selected group',async()=>{
  let reads=0;const fetcher=async path=>{if(path.endsWith('selected-replays.json.gz'))reads++;return new Response(fs.readFileSync(new URL(path,import.meta.url)));};
  const group=index.Groups.find(g=>g.Id==='X00');
  const a=await loadRobust(fetcher,group.Runs[0].Name,root,group.Id);
  const b=await loadRobust(fetcher,group.Runs[1].Name,root,group.Id);
  assert.equal(reads,1);assert.equal(a.group.Id,b.group.Id);assert.equal(b.document.Replay.Name,group.Runs[1].Name);
  assert.strictEqual(a.comparisonRuns[0].run,b.comparisonRuns[0].run);
});
