import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import zlib from 'node:zlib';
import {causalReadout,causalMarkers} from './causal-yaw.mjs';
import {loadRobust} from './robust-learning.mjs';
test('137 predicted second command is distinct from the actual later replan',()=>{
  const samples=[1,1.2,1.24,1.4].map(t=>({Input:{Time:t,Rider:{Position:{X:t,Y:0}}}}));
  const actual={Drive:1,SteeringRadians:0};const variant={Replay:{Samples:samples,Decisions:[{Time:1.2,Action:actual}]}};
  const plan={Time:1,First:actual,Second:{Drive:0,SteeringRadians:.1}};
  const data={comparisonRuns:[{policy:'B',run:variant}],causalPlans:[plan],group:{Events:[{Time:1.24,Kind:'activation',Label:'Aktywacja'}]}};
  assert.equal(causalReadout(data,1.19).actualNext,null);
  assert.equal(causalReadout(data,1.2).actualNext.Action.Drive,1);
  assert.equal(causalReadout(data,1.2).plan.Second.Drive,0);
  data.causalPlans.push({Time:1.2,First:actual,Second:actual});
  const replanned=causalReadout(data,1.2);
  assert.equal(replanned.plan.Time,1.2);
  assert.equal(replanned.previousPlan.Second.Drive,0);
  assert.equal(replanned.previousActual.Action.Drive,1);
});
test('137 markers anchor only to recorded native states of the new variant',()=>{
  const samples=[0,.2,.24,.4].map(t=>({Input:{Time:t,Rider:{Position:{X:t,Y:2}}}}));
  const data={comparisonRuns:[{policy:'A',run:{Replay:{Samples:[{Input:{Time:0,Rider:{Position:{X:999,Y:999}}}}]}}},{policy:'B',run:{Replay:{Samples:samples}}}],
    group:{Events:[{Time:.24,Kind:'activation',Label:'Gaz1'}]}};
  const markers=causalMarkers(data);assert.equal(markers[0].x,.24);assert.equal(markers[0].y,2);assert.equal(markers[0].time,.24);
});
test('137 common loader preserves full-second-straight native data and geometry',async()=>{
  const root=new URL('../../docs/experiments/causal-yaw-throttle/',import.meta.url);
  const fetcher=async url=>new Response(fs.readFileSync(new URL(url.split('/').at(-1),root)));
  const data=await loadRobust(fetcher,null,'local/');
  data.causalPlans=JSON.parse(zlib.gunzipSync(fs.readFileSync(new URL(data.group.PlansFile,root))));
  assert.equal(data.geometry.segments.length,9);assert.equal(data.geometry.segments[4].lengthReferenceMeters,62);
  const last=data.document.Replay.Samples.at(-1);assert.ok(last.Input.Rider.TotalReferenceMeters>=data.geometry.segments[8].startReferenceMeters+62);
  for(const marker of causalMarkers(data))assert.ok(data.comparisonRuns.find(r=>r.policy==='B').run.Replay.Samples.some(s=>s.Input.Time===marker.time&&s.Input.Rider.Position.X===marker.x));
  const end=data.replay.state(1,data.replay.duration+10);assert.equal(end.x,data.replay.state(1,data.replay.duration).x);
});
