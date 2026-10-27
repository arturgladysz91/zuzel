import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import zlib from 'node:zlib';
import {nativeAt,isEnded,failureAt} from './motorcycle-physics.mjs';
const root=new URL('../../docs/experiments/motorcycle-physics-validation/',import.meta.url);
const read=n=>JSON.parse(n.endsWith('.gz')?zlib.gunzipSync(fs.readFileSync(new URL(n,root))):fs.readFileSync(new URL(n,root)));
test('139 viewer keeps every native time and never extrapolates a DNF',()=>{
 for(const x of read('native-summaries.json')){
  const r=read(x.Viewer),last=r.Frames.at(-1);
  assert.equal(nativeAt(r.Frames,last.time+100),last);
  assert.equal(isEnded(r,last.time+100),true);
  assert.equal(failureAt(r,last.time),r.Metrics.Native.Outcome!=='completed');
  for(let j=1;j<r.Frames.length;j+=73){const a=r.Frames[j-1],b=r.Frames[j];assert.equal(nativeAt(r.Frames,(a.time+b.time)/2),a);}
 }
});
