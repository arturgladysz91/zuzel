// Native observations only. No dynamics or invented training trajectories.
import { Replay, authoredReplayDocument } from './model.mjs';
import { loadContinuous } from './continuous.mjs';
import { readGzip } from './recordings.mjs';
export const freeRoot = '../../docs/experiments/free-racing/';
export function freeRacingReplay(document) {
  if (!document.Samples?.length || !document.Decisions?.length) throw new Error('Brak natywnego Free Racing.');
  const states = document.Samples.map((s, i) => {
    const input = s.Input, v = input.Rider;
    if (!Number.isFinite(input.Time) || i && input.Time <= document.Samples[i - 1].Input.Time)
      throw new Error('Niespójna chronologia Free Racing.');
    return { t:input.Time, id:v.Id, x:v.Position.X, y:v.Position.Y, vx:v.Velocity.X, vy:v.Velocity.Y,
      speed:v.Speed, heading:v.Heading, reference:v.TotalReferenceMeters, offset:v.Road.OffsetMeters,
      target:null, drive:v.Drive, activity:v.Activity, steering:input.Steering, source:'native Free Racing',
      requestedSteering:s.RequestedSteering, requestedDrive:s.RequestedDrive, inner:s.Inner, outer:s.Outer, omega:input.Omega };
  });
  const initial = document.Samples[0].Input.Rider, final = document.Samples.at(-1).Input.Rider;
  const activations = document.Activations;
  const decisions = document.Decisions.map(d => ({ RiderId:initial.Id, OpponentId:0, Action:0, Intent:0, Channels:2,
    ObservedAt:d.Time, DecisionAt:d.Time, RequestedActivation:d.Time + .24,
    ActualActivation:activations.find(a => Math.abs(a.ObservedAt - d.Time) < 1e-8)?.ActivationAt ?? null,
    TargetOffsetMeters:0, Drive:d.Action.Drive, Reason:`Free Racing · gaz ${(d.Action.Drive*100).toFixed(1)}% · skręt ${(d.Action.SteeringRadians*180/Math.PI).toFixed(3)}°` }));
  const result = { TimeSeconds:document.Metrics.DurationSeconds, Riders:[final], Decisions:decisions, Contacts:[], FinishTimes:{} };
  if (Math.abs(states.at(-1).t - result.TimeSeconds) > 1e-8) throw new Error('Brak rzeczywistego końca Free Racing.');
  const replay = new Replay(states, authoredReplayDocument({Initial:{Riders:[initial]},Result:result,CrossingSeconds:[]},0));
  replay.freeRacing = document;
  return replay;
}
export async function loadFreeRacing(fetcher = fetch, name = 'preflight-train-16-3') {
  const response = await fetcher(freeRoot + name + '.json.gz');
  const document = JSON.parse(await readGzip(response));
  const baseline = await loadContinuous(fetcher);
  // Historical native trace only, with different entry conditions clearly identified.
  const historical = [...baseline.replay.tracks.values()][0];
  return { replay:freeRacingReplay(document), geometry:baseline.geometry, document,
    comparisons:[{name:'Historyczny #124: 18m/s, offset1m — inne warunki',color:'#ffb36d',planned:false,points:historical}] };
}
export function freeRacingReadout(replay, time) {
  const run = replay.freeRacing;
  const sample = run.Samples.filter(s => s.Input.Time <= time + 1e-10).at(-1) ?? run.Samples[0];
  const decision = run.Decisions.filter(d => d.Time <= time + 1e-10).at(-1);
  return { sample, decision, run };
}
export function updateFreeRacingPanel(element, replay, time) {
  const {sample, decision, run} = freeRacingReadout(replay,time);
  const p = sample.Input, v = p.Rider;
  const lines = [
    `Polityka ${run.PolicyId} · ${run.Name} · ${run.Scenario.Speed}m/s / offset początkowy ${run.Scenario.Offset}m`,
    `Gaz żądany / aktywny: ${(sample.RequestedDrive*100).toFixed(1)}% / ${(v.Drive*100).toFixed(1)}%`,
    `Kierownica żądana / wykonana: ${(sample.RequestedSteering*180/Math.PI).toFixed(3)}° / ${(p.Steering*180/Math.PI).toFixed(3)}°`,
    `Kreda / banda (cały obrys): ${sample.Inner.toFixed(4)}m / ${sample.Outer.toFixed(4)}m`,
    `Yaw: ${v.AngularVelocity.toFixed(4)}rad/s · koło: ${p.Omega.toFixed(3)}rad/s · poślizg wzdłużny koła: ${(p.Omega*.33-v.ForwardSpeed).toFixed(3)}m/s · pending: ${p.Pending.length}`,
    `Parametry: ${JSON.stringify(Object.fromEntries(Object.entries(run.Policy).filter(([key])=>key!=='Values')))}`,
    `Wynik: ${run.Metrics.Outcome} · trening zatrzymany na bramce wstępnej. Brak polityki pośredniej/wyuczonej.`
  ];
  element.replaceChildren();
  const heading = document.createElement('h2'); heading.textContent='Free Racing · rzeczywisty przejazd i rozpatrywane działania'; element.append(heading);
  for (const line of lines) { const p=document.createElement('p'); p.textContent=line; element.append(p); }
  if (decision) {
    const details=document.createElement('details'), summary=document.createElement('summary');
    summary.textContent=`Prognozy 0,9s z obserwacji ${decision.Time.toFixed(2)}s · ${decision.Candidates.length} działań · ${decision.ForceEvaluations} ocen fizyki`;
    const table=document.createElement('table');
    const head=document.createElement('tr');
    for(const label of ['Skręt °','Gaz %','Postęp m','Min prześwit m','v końcowa m/s','β końcowe °','Koszt']) { const th=document.createElement('th');th.textContent=label;head.append(th); }
    table.append(head);
    for(const c of decision.Candidates) {
      const row=document.createElement('tr');
      for(const value of [c.Action.SteeringRadians*180/Math.PI,c.Action.Drive*100,c.ProgressMeters,c.MinimumClearanceMeters,c.ExitSpeed,c.ExitBeta*180/Math.PI,c.Score]) {
        const td=document.createElement('td');td.textContent=value.toFixed(3);row.append(td);
      }
      table.append(row);
    }
    details.append(summary,table);element.append(details);
  }
}
