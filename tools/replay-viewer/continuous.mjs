// Read-only #123 adapter. No world, integration, path following or synthesized poses.
import { Replay, authoredReplayDocument } from './model.mjs';
import { readGzip } from './recordings.mjs';

export const continuousRoot = '../../docs/experiments/continuous-corner-planning/';
const preparedRoot = '../../docs/experiments/feasible-path-speed-planning/';
export const continuousFiles = [continuousRoot + 'continuous.csv.gz', continuousRoot + 'continuous-plan.csv.gz',
  continuousRoot + 'continuous.json', continuousRoot + 'changes.json', preparedRoot + 'planned-geometry.csv.gz',
  continuousRoot + 'diagnostic-geometry.csv.gz', '../../docs/experiments/progressive-bend-width/progressive-b-geometry.json'];

export function numericRows(text, required) {
  const lines = text.trim().split(/\r?\n/), header = lines.shift().split(',');
  if (required.some(k => !header.includes(k))) throw new Error('Brak wymaganych pól natywnego zapisu #123.');
  return lines.map(line => {
    const cells = line.split(',');
    if (cells.length !== header.length) throw new Error('Niepełny wiersz #123.');
    return Object.fromEntries(required.map(k => {
      const value = cells[header.indexOf(k)];
      if (!value.trim() || !Number.isFinite(Number(value))) throw new Error(`Nieprawidłowe pole #123: ${k}`);
      return [k, Number(value)];
    }));
  });
}

export function continuousReplay(csv, planCsv, document, changes) {
  if (document.Initial.Riders.length !== 1 || document.Result.Riders.length !== 1)
    throw new Error('#123 wymaga dokładnie jednego zapisanego zawodnika.');
  const rows = numericRows(csv, ['t','dt','s','x','y','vx','vy','speed','heading','drive','steer_before','steer_used','steer_after']);
  const plans = numericRows(planCsv, ['t','s','offset','target','path_x','path_y']);
  if (!rows.length || rows.length !== plans.length) throw new Error('Niekompletne pary stan/plan #123.');
  const initial = document.Initial.Riders[0], final = document.Result.Riders[0];
  const states = rows.map((r, i) => {
    const p = plans[i];
    if (r.t !== p.t || r.s !== p.s || r.dt <= 0 || r.drive < 0 || r.drive > 1 ||
        i && (r.t <= rows[i-1].t || Math.abs(rows[i-1].t + rows[i-1].dt - r.t) > 1e-7 ||
        Math.abs(rows[i-1].steer_after - r.steer_before) > 1e-7)) throw new Error('Niespójna chronologia #123.');
    // Force diagnostics capture Before AFTER Activate(), see WorldRunner and #124 audit.
    // Trust this Drive, not an AI review, a constant 1, or a rounded JSON command.
    return { t:r.t, id:initial.Id, x:r.x, y:r.y, vx:r.vx, vy:r.vy, speed:r.speed, heading:r.heading,
      reference:r.s, offset:p.offset, target:p.target, drive:r.drive, activity:initial.Activity,
      steering:r.steer_before, steeringUsed:r.steer_used, steeringAfter:r.steer_after, source:'native' };
  });
  if (Math.abs(rows.at(-1).t + rows.at(-1).dt - document.Result.TimeSeconds) > 1e-7)
    throw new Error('Brak końca ostatniego interwału #123.');
  // The endpoint is explicitly recorded in Result.Riders, not extrapolated from vx/dt.
  states.push({ t:document.Result.TimeSeconds, id:final.Id, x:final.Position.X, y:final.Position.Y,
    vx:final.Velocity.X, vy:final.Velocity.Y, speed:final.Speed, heading:final.Heading,
    reference:final.TotalReferenceMeters, offset:final.Road.OffsetMeters, target:final.TargetOffsetMeters,
    drive:final.Drive, activity:final.Activity, steering:rows.at(-1).steer_after, source:'Result.Riders' });
  const replay = new Replay(states, authoredReplayDocument(document, 0));
  replay.planChanges = changes;
  replay.planAt = time => changes.some(c => c.Phase === 'activated' && c.Time <= time) ? 2 : 1;
  return replay;
}

export function continuousPlans(firstCsv, nextCsv, changes) {
  const first = numericRows(firstCsv, ['s','x','y']);
  const next = numericRows(nextCsv, ['s','x','y']);
  const activation = changes.find(c => c.Phase === 'activated');
  if (!activation || !first.length || !next.length) throw new Error('Brak zapisanego przełączenia planu #123.');
  return [
    { name:'Plan 1 · zachowany #122', color:'#badd98', planned:true, generation:1, points:first },
    { name:'Plan 2 · aktywny od ' + activation.Time + ' s', color:'#badd98', planned:true, generation:2,
      points:[...first.filter(p => p.s < next[0].s), ...next] }
  ];
}

export async function loadContinuous(fetcher = fetch) {
  const responses = await Promise.all(continuousFiles.map(url => fetcher(url)));
  if (responses.some(r => !r.ok)) throw new Error('Brak oryginalnych plików #123 / #122 lub geometrii Egg Mild.');
  const [csv, planCsv, document, changes, firstCsv, nextCsv, geometry] = await Promise.all(responses.map((r,i) =>
    [0,1,4,5].includes(i) ? readGzip(r) : r.json()));
  return { replay:continuousReplay(csv, planCsv, document, changes), geometry,
    comparisons:continuousPlans(firstCsv, nextCsv, changes), document };
}
