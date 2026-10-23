import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {gunzipSync} from 'node:zlib';
import {freeRacingReplay,freeRacingReadout} from './free-racing.mjs';
const run=JSON.parse(gunzipSync(fs.readFileSync(new URL('../../docs/experiments/free-racing/preflight-train-16-3.json.gz',import.meta.url))));
test('Free Racing retains every native position, orientation and both actuator observations',()=>{
  const replay=freeRacingReplay(run);
  assert.equal(replay.tracks.get(1).length,run.Samples.length);
  for(const s of run.Samples){const state=replay.state(1,s.Input.Time,false),v=s.Input.Rider;
    assert.equal(state.x,v.Position.X);assert.equal(state.y,v.Position.Y);assert.ok(Math.abs(state.heading-v.Heading)<1e-15);
    assert.equal(state.drive,v.Drive);assert.equal(state.steering,s.Input.Steering);assert.equal(state.target,null);
  }
  assert.equal(replay.duration,1.9100000000000001);assert.deepEqual(replay.result.FinishTimes,{});
});
test('Failed preflight is never shown as completed training and does not extrapolate',()=>{
  const replay=freeRacingReplay(run),end=replay.state(1,100,false);
  assert.equal(end.x,run.Samples.at(-1).Input.Rider.Position.X);assert.equal(end.finished,false);
  assert.equal(run.Metrics.Completed,false);assert.ok(run.Samples.at(-1).Outer<0);
  assert.equal(freeRacingReadout(replay,0).decision.Time,0);
  assert.equal(freeRacingReadout(replay,1.9).run.PolicyId,0);
});
test('Chronology and recorded end are checked',()=>{
  const bad=structuredClone(run);bad.Samples[1].Input.Time=0;assert.throws(()=>freeRacingReplay(bad));
  const short=structuredClone(run);short.Samples.pop();assert.throws(()=>freeRacingReplay(short));
});
