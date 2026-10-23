import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {gunzipSync} from 'node:zlib';
import {loadRobust,robustReplay,robustReadout} from './robust-learning.mjs';
const root='../../docs/experiments/autonomous-speedway-extended-learning/';
const index=JSON.parse(fs.readFileSync(new URL(root+'index.json',import.meta.url)));
const runs=JSON.parse(gunzipSync(fs.readFileSync(new URL(root+'selected-replays.json.gz',import.meta.url)))).Runs;
test('Extended horizon groups load only full native traces on identical initial physical states',async()=>{
  const fetcher=async path=>new Response(fs.readFileSync(new URL(path,import.meta.url)));
  for(const group of index.Groups){
    const data=await loadRobust(fetcher,group.Runs[0].Name,root,group.Id);
    assert.equal(data.group.Id,group.Id);assert.equal(data.comparisons.length,group.Runs.length);
    assert.equal(new Set(data.comparisons.map(c=>c.color)).size,data.comparisons.length);
    if(group.TwoBends&&data.document.Replay.Metrics.Outcome!=='probe')assert.match(data.replay.episodeScope,/Dwa kolejne łuki/);
    for(const [i,ref] of group.Runs.entries()){
      const run=runs[ref.Name];assert.deepEqual(run.Replay.Samples[0].Input.Rider,data.document.Replay.Samples[0].Input.Rider);
      assert.equal(data.comparisons[i].planned,false);assert.equal(data.comparisons[i].points.length,run.Replay.Samples.length);
    }
  }
});
test('Extended playback preserves native wheel-independent pose, executed steering and actual throttle',()=>{
  for(const run of Object.values(runs)){
    const replay=robustReplay(run);
    for(const s of run.Replay.Samples){const actual=replay.state(1,s.Input.Time,false),v=s.Input.Rider;
      assert.equal(actual.x,v.Position.X);assert.equal(actual.y,v.Position.Y);assert.equal(actual.steering,s.Input.Steering);assert.equal(actual.drive,v.Drive);assert.equal(actual.target,null);
    }
    if(!run.Replay.Metrics.Completed){assert.equal(run.Replay.Metrics.GateSeconds,null);assert.equal(replay.state(1,100,false).finished,false);}
    const readout=robustReadout(replay,2);assert.ok(readout.sample.Input.Time<=2+1e-10);assert.ok(!readout.telemetry||readout.telemetry.Time<=2+1e-10);
  }
});
