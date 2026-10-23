import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {gunzipSync} from 'node:zlib';
import {robustReplay,robustReadout,loadRobust} from './robust-learning.mjs';
const root=new URL('../../docs/experiments/robust-free-racing-learning/',import.meta.url);
const index=JSON.parse(fs.readFileSync(new URL('index.json',root)));
const runs=JSON.parse(gunzipSync(fs.readFileSync(new URL('selected-replays.json.gz',root)))).Runs;
test('Robust learning preserves every selected native position, pose, throttle and steering',()=>{
  for(const run of Object.values(runs)){
    const replay=robustReplay(run);assert.equal(replay.duration,run.Replay.Metrics.DurationSeconds);assert.deepEqual(replay.result.FinishTimes,{});
    for(const s of run.Replay.Samples){const state=replay.state(1,s.Input.Time,false),v=s.Input.Rider;
      assert.equal(state.x,v.Position.X);assert.equal(state.y,v.Position.Y);assert.ok(Math.abs(state.heading-v.Heading)<1e-15);
      assert.equal(state.drive,v.Drive);assert.equal(state.steering,s.Input.Steering);assert.equal(state.target,null);
    }
  }
});
test('Robust comparisons overlay only identical physical initial conditions and actual full traces',async()=>{
  const fetcher=async path=>new Response(fs.readFileSync(new URL(path,import.meta.url)));
  const data=await loadRobust(fetcher);assert.equal(data.comparisons.length,4);
  const initial=data.document.Replay.Samples[0].Input.Rider;
  for(const [i,reference] of data.group.Runs.entries()){
    const run=runs[reference.Name];assert.deepEqual(run.Replay.Samples[0].Input.Rider,initial);
    assert.equal(data.comparisons[i].planned,false);assert.equal(data.comparisons[i].points.length,run.Replay.Samples.length);
    assert.equal(data.comparisons[i].points.at(-1).x,run.Replay.Samples.at(-1).Input.Rider.Position.X);
  }
});
test('Uncertified DNF and confirmed crossing remain failures without an invented finish time',()=>{
  for(const run of Object.values(runs).filter(r=>!r.Replay.Metrics.Completed)){
    const replay=robustReplay(run);assert.equal(run.Replay.Metrics.GateSeconds,null);
    assert.equal(replay.state(1,1000,false).x,run.Replay.Samples.at(-1).Input.Rider.Position.X);
    assert.equal(replay.state(1,1000,false).finished,false);
  }
  assert.equal(runs['diagnostic-boundary'].Metrics.BoundaryClass,'confirmed crossing');
});
test('Telemetry reads the latest accepted interval without future forces or pending observations',()=>{
  const run=runs[index.DefaultRun],replay=robustReplay(run),time=3.2;
  const readout=robustReadout(replay,time);assert.ok(readout.sample.Input.Time<=time+1e-10);
  assert.ok(readout.telemetry.Time<=time+1e-10);assert.ok(readout.decision.Time<=time+1e-10);
  assert.ok(readout.sample.Input.Pending.every(c=>c.ObservedAt<=time+1e-10));assert.equal(robustReadout(replay,0).telemetry,null);
});
