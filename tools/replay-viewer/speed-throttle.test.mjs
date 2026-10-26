import test from 'node:test';
import assert from 'node:assert/strict';
import {throttleMarkers,rejectedFull} from './speed-throttle.mjs';
import fs from 'node:fs';
import {loadRobust} from './robust-learning.mjs';
const samples=[0,.2,.24,.4,.44,.6].map(t=>({Input:{Time:t,Rider:{Position:{X:t,Y:0}}}}));
test('Throttle markers use recorded samples and distinguish request from real activation',()=>{
  const data={document:{Replay:{Samples:samples}},group:{Events:{ObservedAt:.2,ActivationAt:.44,Landmarks:{Exit:{Time:.4}}}}};
  const m=throttleMarkers(data);
  assert.equal(m.find(x=>x.label.startsWith('Żądanie')).time,.2);
  assert.equal(m.find(x=>x.label.startsWith('Rzeczywista')).time,.44);
  assert.equal(m.find(x=>x.label.startsWith('Termin')).time,0);
  assert.ok(m.every(x=>samples.some(s=>s.Input.Time===x.time&&s.Input.Rider.Position.X===x.x)));
  data.group.Events.ActivationAt=null;
  assert.ok(!throttleMarkers(data).some(x=>x.label.startsWith('Rzeczywista')));
});
test('Rejected full gas compares the same steering and never claims later decisions early',()=>{
  const a={SteeringRadians:0,Drive:.318},chosen={Action:a,Safety:0,Progress:10},full={Action:{...a,Drive:1},Safety:0,Progress:11};
  const run={Replay:{Decisions:[{Time:.2,Action:a,Candidates:[chosen,full]}]}};
  assert.equal(rejectedFull(run,.19),null);assert.equal(rejectedFull(run,.2).sameSafety,true);assert.equal(rejectedFull(run,.2).moreProgress,true);
});
test('136 bundle supplies the common viewer schema and real nine-segment geometry',async()=>{
  const root=new URL('../../docs/experiments/speed-throttle-root-cause/',import.meta.url);
  const index=JSON.parse(fs.readFileSync(new URL('index.json',root)));
  for(const g of index.Groups){assert.ok(g.Reasons.length);assert.ok(g.Scenario);assert.ok(g.Scope);}
  const fetcher=async url=>new Response(fs.readFileSync(new URL(url.split('/').at(-1),root)));
  const data=await loadRobust(fetcher,null,'local/');
  assert.equal(data.geometry.segments.length,9);assert.equal(data.geometry.segments[4].lengthReferenceMeters,62);
  assert.ok(data.geometry.enums.channels);assert.equal(data.comparisons.length,2);
  const a=data.comparisonRuns.find(r=>r.policy==='A');const selectedA={...data,document:a.run};
  assert.deepEqual(throttleMarkers(data),throttleMarkers(selectedA));
});
