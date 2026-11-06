import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {gunzipSync} from 'node:zlib';
import {study172Replay,loadStudy172} from './study172.mjs';
import {angleDelta} from './model.mjs';

const bytes=readFileSync(new URL('./recordings/172-s3-k4.json.gz',import.meta.url));
const bundle=JSON.parse(gunzipSync(bytes));

test('#172 keeps every native pose, active control and experimental geometry',()=>{
  const replay=study172Replay(bundle),track=replay.tracks.get(1);
  assert.equal(track.length,5636);assert.equal(replay.tracks.size,1);
  assert.equal(track[0].x,-.57);assert.equal(track[0].speed,0);
  assert.equal(bundle.geometry.frameId,'track-shape-101:progressive-b');
  assert.equal(bundle.geometry.segments.length,9);
  for(const [i,row] of bundle.rows.entries()){
    assert.deepEqual(track[i],row);
    const state=replay.state(1,row.t,false);
    for(const key of ['x','y','drive','steering','speed'])assert.equal(state[key],row[key]);
    assert.ok(Math.abs(angleDelta(state.heading,row.heading))<1e-12);
    if(i){const before=track[i-1],dt=row.t-before.t;
      assert.ok(dt>0);assert.ok(row.reference>=before.reference-1e-5);
      assert.ok(Math.hypot(row.x-before.x,row.y-before.y)<(Math.max(row.speed,before.speed)+1)*dt+.002);
    }
  }
  const slide=track.find(p=>p.speed>10&&Math.abs(angleDelta(Math.atan2(p.vy,p.vx),p.heading))>.15);
  assert.ok(slide,'body heading retains visible drift instead of aligning with velocity');
});

test('#172 counts three recorded crossings without counting launch or snapping the finish pose',()=>{
  const replay=study172Replay(bundle);
  assert.equal(replay.state(1,0).completedLaps,0);
  for(const [i,c] of bundle.provenance.crossings.entries()){
    const before=replay.state(1,c.Time-1e-7),after=replay.state(1,c.Time+1e-7);
    assert.equal(before.completedLaps,i);assert.equal(after.completedLaps,i+1);
    assert.ok(Math.hypot(after.x-before.x,after.y-before.y)<1e-4);
    assert.ok(Math.abs(angleDelta(before.heading,after.heading))<1e-4);
  }
  assert.equal(replay.state(1,replay.duration).completedLaps,3);
  assert.equal(replay.state(1,replay.duration).finish,56.22515215317);
  const end=replay.tracks.get(1).at(-1),future=replay.state(1,1000);
  assert.equal(future.x,end.x);assert.equal(future.y,end.y);assert.equal(future.heading,end.heading);
});

test('#172 interpolates only visual pose while measured speed and controls stay on their sample',()=>{
  const replay=study172Replay(bundle),track=replay.tracks.get(1);
  const i=track.findIndex((p,i)=>i>0&&p.drive!==track[i-1].drive);
  const left=track[i-1],right=track[i],state=replay.state(1,(left.t+right.t)/2);
  assert.equal(state.drive,left.drive);assert.equal(state.steering,left.steering);assert.equal(state.speed,left.speed);
  assert.ok(state.interpolated);assert.ok(Math.abs(state.x-(left.x+right.x)/2)<1e-9);
});

test('#172 browser loader consumes the static gzip and reports missing recordings',async()=>{
  const data=await loadStudy172(async path=>{assert.equal(path,'./recordings/172-s3-k4.json.gz');return new Response(bytes);});
  assert.equal(data.replay.duration,56.230000000000004);
  await assert.rejects(loadStudy172(async()=>new Response('',{status:404})),/Brak natywnego zapisu/);
  assert.throws(()=>study172Replay({...bundle,provenance:{...bundle.provenance,base:'other'}}),/pochodzenie/);
});
