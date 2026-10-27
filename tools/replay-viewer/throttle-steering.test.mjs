import test from 'node:test';import assert from 'node:assert/strict';
import {nativeAt,isEnded,failureAt,displayValue,bounds} from './throttle-steering.mjs';
test('accepted native samples never interpolate or invent a DNF continuation',()=>{const fs=[{time:0,x:1},{time:.01,x:2},{time:.02,x:3}];assert.equal(nativeAt(fs,.019),fs[1]);assert.equal(nativeAt(fs,10),fs[2]);assert.equal(isEnded({Frames:fs},.02),false);assert.equal(isEnded({Frames:fs},.021),true);});
test('missing checkpoint remains unavailable',()=>{assert.equal(displayValue(null),'N/D');assert.equal(displayValue(0),'0.00');});
test('first failed accepted sample is already marked at its exact timestamp',()=>{const run={Frames:[{time:1}],Metrics:{Native:{Outcome:'control envelope'}}};assert.equal(failureAt(run,.99),false);assert.equal(failureAt(run,1),true);});
test('fitting uses actual inner and variable outer width',()=>{assert.deepEqual(bounds({segments:[{sections:[{inner:{x:0,y:1},outer:{x:10,y:17}},{inner:{x:4,y:2},outer:{x:9,y:12}}]}]}),{minX:0,maxX:10,minY:1,maxY:17});});
