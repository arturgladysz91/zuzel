import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {gunzipSync} from 'node:zlib';
import {learningReplay,learningReadout,loadLearning} from './free-learning.mjs';
const root=new URL('../../docs/experiments/free-racing-learning/',import.meta.url);
const index=JSON.parse(fs.readFileSync(new URL('index.json',root)));
const read=name=>JSON.parse(gunzipSync(fs.readFileSync(new URL(name+'.json.gz',root))));
test('Learning milestones retain every accepted native XY, heading, throttle and steering',()=>{
  for(const name of index.Milestones){const run=read(name),replay=learningReplay(run);
    assert.equal(replay.freeRacing,undefined);assert.equal(replay.learning.Name,name);
    for(const s of run.Samples){const state=replay.state(1,s.Input.Time,false),v=s.Input.Rider;
      assert.equal(state.x,v.Position.X);assert.equal(state.y,v.Position.Y);assert.equal(state.drive,v.Drive);
      assert.ok(Math.abs(state.heading-v.Heading)<1e-15);assert.equal(state.steering,s.Input.Steering);assert.equal(state.target,null);
    }
    assert.equal(replay.duration,run.Metrics.DurationSeconds);assert.deepEqual(replay.result.FinishTimes,{});
  }
});
test('Learning DNF has no gate time and never extrapolates beyond the actual failed state',()=>{
  const run=read('overlay-initial'),replay=learningReplay(run),last=run.Samples.at(-1).Input.Rider;
  assert.equal(run.Metrics.Completed,false);assert.equal(run.Metrics.GateSeconds,null);
  assert.equal(replay.state(1,100,false).x,last.Position.X);assert.equal(replay.state(1,100,false).finished,false);
  assert.equal(learningReadout(replay,0).decision.Time,0);
});
test('Learning loader overlays actual common-condition recordings and matched historical controller',async()=>{
  const fetcher=async path=>{const p=new URL(path,import.meta.url);return new Response(fs.readFileSync(p));};
  const data=await loadLearning(fetcher);
  assert.equal(data.replay.learning.Name,'overlay-final');assert.equal(data.comparisons.length,index.Milestones.length+1);
  for(const [i,name] of index.Milestones.entries()){
    const run=read(name);assert.equal(run.Scenario.Speed,9);assert.equal(run.Scenario.Offset,4);
    assert.equal(data.comparisons[i].planned,false);assert.equal(data.comparisons[i].points.length,run.Samples.length);
    assert.equal(data.comparisons[i].points.at(-1).x,run.Samples.at(-1).Input.Rider.Position.X);
  }
});
