import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {loadStanding144,standingReadout,standingMilestones} from './standing144.mjs';
const root=new URL('../../docs/experiments/standing-start-three-laps/',import.meta.url);
const fetcher=async url=>new Response(fs.readFileSync(new URL(url.replace('local/',''),root)));
test('144 full native replay begins at rest and credits exactly three completed forward laps',async()=>{
  const data=await loadStanding144(fetcher,'A','local/');
  const zero=data.replay.state(1,0,false);assert.equal(zero.speed,0);assert.equal(zero.omega,0);assert.equal(zero.completedLaps,0);assert.equal(zero.totalLaps,3);
  for(const c of data.run.Crossings){
    assert.equal(data.replay.state(1,c.BeforeTime,false).completedLaps,c.Lap-1);
    assert.equal(data.replay.state(1,c.AfterTime,false).completedLaps,c.Lap);
  }
  const end=data.replay.state(1,100,false),last=data.run.Riders[0].Samples.at(-1).Input.Rider;
  assert.equal(end.x,last.Position.X);assert.equal(end.y,last.Position.Y);assert.equal(end.completedLaps,3);assert.equal(end.finished,true);
  assert.ok(standingReadout(data,50,1).sample.Input.Rider.TotalReferenceMeters>2*data.run.LapMeters);
  const marks=standingMilestones(data,1);assert.equal(marks.filter(q=>q.Kind==='crossing').length,3);assert.equal(marks.filter(q=>q.Kind==='bend').length,12);
});
test('144 shared heat is four recorded bodies on one clock with actual contacts, not stitched solos',async()=>{
  const data=await loadStanding144(fetcher,'shared-A','local/');assert.equal(data.replay.tracks.size,4);
  assert.ok(data.run.Contacts.some(c=>c.Kind==='begin'&&c.ImpulseNewtonSeconds>0));
  assert.equal(data.run.Outcome,'experimental beta45 envelope');
  for(const id of data.replay.tracks.keys()){
    const q=standingReadout(data,100,id);assert.equal(q.completedLaps,0);assert.equal(q.rider.FinishSeconds,null);
    assert.equal(data.replay.state(id,100,false).finished,false);
    assert.equal(data.replay.state(id,100,false).x,q.sample.Input.Rider.Position.X);
  }
});
