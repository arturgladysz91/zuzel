import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {gunzipSync} from 'node:zlib';
import {loadRobust,robustReplay,throttleSeries} from './robust-learning.mjs';
const root='../../docs/experiments/full-throttle-learning/';
const index=JSON.parse(fs.readFileSync(new URL(root+'index.json',import.meta.url)));
const runs=JSON.parse(gunzipSync(fs.readFileSync(new URL(root+'selected-replays.json.gz',import.meta.url)))).Runs;
test('Full-throttle comparison loads identical native initial states and distinguishes all variants',async()=>{
  const fetcher=async path=>new Response(fs.readFileSync(new URL(path,import.meta.url)));
  for(const group of index.Groups){
    const data=await loadRobust(fetcher,group.Runs[0].Name,root,group.Id);
    assert.equal(data.group.Id,group.Id);assert.equal(data.comparisons.length,group.Runs.length);assert.equal(new Set(data.comparisons.map(c=>c.color)).size,group.Runs.length);
    for(const r of group.Runs){const run=runs[r.Name];assert.deepEqual(run.Replay.Samples[0].Input.Rider,data.document.Replay.Samples[0].Input.Rider);
      const replay=robustReplay(run);for(const s of run.Replay.Samples){const state=replay.state(1,s.Input.Time,false);assert.equal(state.x,s.Input.Rider.Position.X);assert.equal(state.y,s.Input.Rider.Position.Y);assert.equal(state.drive,s.Input.Rider.Drive);assert.equal(state.target,null);}
    }
    const line=data.comparisons.find(c=>c.policy==='132-final');
    if(line)for(const point of line.gainSamples??[]){
      const source=runs[`final-132-final-${group.Id}`].Replay.Samples.find(s=>s.Input.Rider.TotalReferenceMeters>=point.ReferenceMeters);
      assert.deepEqual(point.NewXY,source.Input.Rider.Position);assert.equal(point.NewTime,source.Input.Time);assert.equal(point.GainSeconds,point.OldTime-point.NewTime);
    }
  }
});
test('Throttle chart preserves requested pending active gas and marks only reopened100%',()=>{
  for(const run of Object.values(runs)){
    const series=throttleSeries(run);assert.equal(series.points.length,run.Replay.Samples.length);
    for(const [i,s] of run.Replay.Samples.entries())assert.deepEqual(series.points[i],[s.Input.Time,s.RequestedDrive*100,s.Input.Active.Drive*100,s.Input.Pending.length?s.Input.Pending[0].Action.Drive*100:null]);
    const ds=run.Replay.Decisions;assert.deepEqual(series.reopenings,ds.filter((d,i)=>i>0&&d.Action.Drive>=.99&&ds[i-1].Action.Drive<.99).map(d=>d.Time));
    const active=new Map(run.Replay.Samples.map(s=>[s.Input.Time.toFixed(8),s.Input.Active.Drive]));
    assert.deepEqual(series.activeDropRequests,ds.filter(d=>d.Action.Drive>=.99&&active.get(d.Time.toFixed(8))<.99).map(d=>d.Time));
    const activations=run.Replay.Activations;assert.deepEqual(series.activeReopenings,activations.filter((a,i)=>i>0&&a.Action.Drive>=.99&&activations[i-1].Action.Drive<.99).map(a=>a.ActivationAt));
  }
});
