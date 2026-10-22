// Recorded data only. This module contains no dynamics, track solver or prediction.
export const clamp = (x, lo, hi) => Math.max(lo, Math.min(hi, x));
const finite = (x, label) => { if (!Number.isFinite(x)) throw new Error(`Nieprawidłowa liczba: ${label}`); return x; };
export const angleDelta = (a, b) => Math.atan2(Math.sin(b - a), Math.cos(b - a));
export const angleAt = (a, b, fraction) => Math.atan2(Math.sin(a + angleDelta(a, b) * fraction), Math.cos(a + angleDelta(a, b) * fraction));
// Adapt authored observation-only captures; accepted poses/commands are never changed.
export function authoredReplayDocument(document, requiredLaps = null) {
  return { ...document, Run: { Initial: document.Initial, Result: document.Result,
    Riders: document.Initial.Riders.map(r => ({ Measurement: { RiderId: r.Id, CrossingSeconds: document.CrossingSeconds } })),
    Passes: [], Consequences: [], RequiredLaps: requiredLaps } };
}
export const enumName = (geometry, type, value) => typeof value === 'string' && !/^\d+$/.test(value)
  ? value : geometry.enums[type]?.[String(value)] ?? `Unknown(${value})`;
const upperBound = (items, time, key = x => x.t) => {
  let lo = 0, hi = items.length;
  while (lo < hi) { const mid = (lo + hi) >>> 1; if (key(items[mid]) <= time) lo = mid + 1; else hi = mid; }
  return lo;
};
export function parseCsv(text) {
  const lines = text.replace(/^\uFEFF/, '').trim().split(/\r?\n/);
  const header = lines.shift().split(',');
  const columns = ['time_s', 'rider', 'x_m', 'y_m', 'vx_mps', 'vy_mps', 'speed_mps', 'reference_m', 'offset_m', 'target_m', 'drive', 'heading_rad', 'activity'];
  if (columns.some(k => !header.includes(k))) throw new Error('CSV nie zawiera wymaganych pól zaakceptowanego stanu.');
  return lines.map((line, i) => {
    const cells = line.split(',');
    if (cells.length !== header.length) throw new Error(`Niepełny wiersz CSV ${i + 2}.`);
    const value = name => cells[header.indexOf(name)];
    const number = name => { if (!value(name).trim()) throw new Error(`Puste ${name}.`); return finite(Number(value(name)), name); };
    return { t: number('time_s'), id: number('rider'), x: number('x_m'), y: number('y_m'), vx: number('vx_mps'), vy: number('vy_mps'),
      speed: number('speed_mps'), reference: number('reference_m'), offset: number('offset_m'), target: number('target_m'),
      drive: number('drive'), heading: number('heading_rad'), activity: value('activity') };
  });
}
// Dense Before snapshots from the existing audit, never a resimulation.
// These four recordings have constant Drive; native target is present per row.
export function parseNativeCsv(text, accepted) {
  if (!accepted.length || new Set(accepted.map(p => p.id)).size !== 1 || new Set(accepted.map(p => p.drive)).size !== 1)
    throw new Error('Gęsty zapis wymaga jednego zawodnika i zapisanego stałego gazu.');
  const lines = text.trim().split(/\r?\n/), header = lines.shift().split(',');
  const fields = { t: 't', reference: 's', x: 'x', y: 'y', vx: 'vx', vy: 'vy', speed: 'speed', offset: 'offset', target: 'target', heading: 'body_heading' };
  const indexes = Object.fromEntries(Object.entries(fields).map(([key, name]) => [key, header.indexOf(name)]));
  if (Object.values(indexes).some(i => i < 0)) throw new Error('Brak pól natywnego zapisu XY/heading/v.');
  let current = 0;
  return lines.map(line => {
    const cells = line.split(','); if (cells.length !== header.length) throw new Error('Niepełny native snapshot.');
    const row = Object.fromEntries(Object.entries(indexes).map(([key, i]) => {
      if (!cells[i].trim()) throw new Error('Puste pole native snapshot.');
      return [key, finite(Number(cells[i]), key)];
    }));
    while (current + 1 < accepted.length && accepted[current + 1].t <= row.t) current++;
    return { ...row, id: accepted[0].id, drive: accepted[0].drive, activity: accepted[current].activity, source: 'native' };
  });
}
export function motionVectors(state, seconds = .2) {
  const vx = state.renderVx ?? state.vx, vy = state.renderVy ?? state.vy;
  const moving = Math.hypot(vx, vy) > .01, travel = Math.atan2(vy, vx);
  return { heading: { x: Math.cos(state.heading), y: Math.sin(state.heading) },
    velocity: { x: vx * seconds, y: vy * seconds }, travel, beta: moving ? angleDelta(travel, state.heading) : null };
}
// Three metres of recorded lead-in; no extrapolation to an imagined entry pose.
export function bendJumps(geometry, track) {
  return geometry.segments.filter(s => s.type === 'TurnEntry').slice(0, 2).map(segment => {
    let target = segment.startReferenceMeters - 3;
    while (target < track[0].reference - 1e-6) target += geometry.lapMeters;
    return track.find(p => p.reference >= target)?.t ?? null;
  });
}
export function boundaryStatus(document) {
  if (document.MinimumInner < 0 || document.MinimumOuter < 0)
    return { level: 'unsafe', text: 'Zaobserwowany wyjazd obrysu poza tor' };
  if (document.BoundarySafe)
    return { level: 'safe', text: 'Pełny certyfikat granic PASS' };
  return { level: 'caution', text: 'Brak pełnego certyfikatu między krokami; zapisane obrysy pozostają w torze' };
}
export function decisionStatus(d, time) {
  if (time < d.ObservedAt) return 'future';
  if (d.ActualActivation != null && d.ActualActivation <= time) return 'active';
  if (d.CancelledAt != null && d.CancelledAt <= time) return 'cancelled';
  return time >= d.RequestedActivation ? 'waiting' : 'pending';
}
export class Replay {
  constructor(csv, document, nativeCsv = null) {
    this.run = document.Run;
    if (!this.run?.Initial || !this.run?.Result) throw new Error('Brak Run.Initial / Run.Result.');
    this.result = this.run.Result;
    this.duration = finite(this.result.TimeSeconds, 'TimeSeconds');
    this.decisions = this.result.Decisions.map((d, index) => ({ ...d, index }));
    this.tracks = new Map();
    for (const v of this.run.Initial.Riders) {
      this.tracks.set(v.Id, [{ t: 0, id: v.Id, x: finite(v.Position.X, 'Initial.X'), y: finite(v.Position.Y, 'Initial.Y'),
        vx: v.Velocity.X, vy: v.Velocity.Y, speed: v.Speed, heading: v.Heading, reference: v.TotalReferenceMeters,
        offset: v.Road.OffsetMeters, target: v.TargetOffsetMeters, drive: v.Drive, activity: v.Activity }]);
    }
    this.recordedControls = Array.isArray(csv);
    const accepted = this.recordedControls ? csv : parseCsv(csv);
    this.native = nativeCsv != null || this.recordedControls;
    if (nativeCsv != null && (this.run.Initial.Riders.length !== 1 || this.run.Initial.Riders[0].Drive !== accepted[0]?.drive || this.decisions.some(d => d.ActualActivation != null)))
      throw new Error('Brak pełnego natywnego zapisu zmiennych aktuatorów.');
    for (const row of nativeCsv != null ? parseNativeCsv(nativeCsv, accepted) : accepted) {
      const track = this.tracks.get(row.id);
      if (this.native && row.t === 0 && track?.length === 1) {
        if (Math.hypot(row.x - track[0].x, row.y - track[0].y) > 1e-5 || Math.abs(angleDelta(row.heading, track[0].heading)) > 1e-5)
          throw new Error('Native t=0 nie zgadza się z Initial.');
        if (this.recordedControls) track[0] = row;
        continue;
      }
      if (!track || row.t <= track.at(-1).t || row.t > this.duration + 1e-6) throw new Error('Nieznany zawodnik lub nieuporządkowany czas CSV.');
      track.push(row);
    }
    if ([...this.tracks.values()].some(x => x.length < 2)) throw new Error('Brak próbek zawodnika.');
    this.activated = this.decisions.filter(d => d.ActualActivation != null).sort((a, b) => a.ActualActivation - b.ActualActivation || a.index - b.index);
    this.metrics = new Map(this.run.Riders.map(r => [r.Measurement.RiderId, r.Measurement]));
    this.sampleTimes = [...new Set([...this.tracks.values()].flat().map(s => s.t))].sort((a, b) => a - b);
    for (const d of this.decisions) {
      if (!this.tracks.has(d.RiderId) || d.RequestedActivation < d.ObservedAt ||
        d.ActualActivation != null && (d.ActualActivation < d.ObservedAt || d.CancelledAt != null && d.CancelledAt <= d.ActualActivation))
        throw new Error('Sprzeczny zapis aktywacji / anulowania komendy.');
    }
  }
  state(id, time, interpolatePose = true) {
    time = clamp(finite(time, 'czas odtwarzania'), 0, this.duration);
    const track = this.tracks.get(id); if (!track) throw new Error('Nieznany zawodnik.');
    const leftIndex = Math.max(0, upperBound(track, time) - 1), left = track[leftIndex], right = track[leftIndex + 1];
    const finish = this.result.FinishTimes[String(id)];
    const finished = finish != null && time >= finish;
    // After the finish, use the last accepted point already present at this time.
    // An accepted post-crossing point is still inspectable at its own timestamp.
    const fraction = right && !finished && interpolatePose ? clamp((time - left.t) / (right.t - left.t), 0, 1) : 0;
    const lerp = (a, b) => a + (b - a) * fraction;
    const state = { ...left, x: right ? lerp(left.x, right.x) : left.x, y: right ? lerp(left.y, right.y) : left.y,
      heading: right ? angleAt(left.heading, right.heading, fraction) : left.heading,
      reference: right ? lerp(left.reference, right.reference) : left.reference,
      renderVx: right ? lerp(left.vx, right.vx) : left.vx, renderVy: right ? lerp(left.vy, right.vy) : left.vy,
      sampleTime: left.t, poseKnownThrough: track.at(-1).t, interpolated: fraction > 0, stale: time > track.at(-1).t,
      finished, finish, intent: 0, lastDecision: null };
    const knownTime = Math.min(time, track.at(-1).t);
    for (const d of this.activated) {
      if (d.RiderId !== id || d.ActualActivation > knownTime) continue;
      const channel = d.Channels ?? 0;
      // Intent belongs to the line channel; a throttle carrying an old intent cannot replace it.
      if (channel === 0 || channel === 1) state.intent = d.Intent ?? 0;
      // #123 samples already contain post-Activate Drive and per-step path target.
      // Commands explain chronology; they must not overwrite these native values.
      if (this.recordedControls) continue;
      // A sample exactly at activation can precede Activate(), including Initial at t=0.
      if (d.ActualActivation < left.t) continue;
      if (channel === 0 || channel === 1) state.target = d.TargetOffsetMeters;
      if (channel === 0 || channel === 2) state.drive = d.Drive;
    }
    for (const d of this.decisions) if (d.RiderId === id && d.ObservedAt <= time &&
      (!state.lastDecision || d.ObservedAt >= state.lastDecision.ObservedAt)) state.lastDecision = d;
    const crossings = this.metrics.get(id)?.CrossingSeconds ?? [];
    state.completedLaps = upperBound(crossings, time, x => x);
    state.totalLaps = this.run.RequiredLaps ?? (crossings.length || 4);
    state.lap = Math.min(state.completedLaps + 1, state.totalLaps);
    return state;
  }
  step(time, direction) {
    if (direction > 0) return this.sampleTimes[upperBound(this.sampleTimes, time + 1e-9, x => x)] ?? null;
    const index = upperBound(this.sampleTimes, time - 1e-9, x => x) - 1;
    return index >= 0 ? this.sampleTimes[index] : null;
  }
  events(rider = null) {
    const rows = [];
    for (const d of this.decisions) if (rider == null || d.RiderId === rider) {
      for (const [field, kind] of [['ObservedAt', 'observed'], ['RequestedActivation', 'requested'], ['ActualActivation', 'activated'], ['CancelledAt', 'cancelled']])
        if (d[field] != null) rows.push({ t: d[field], kind, rider: d.RiderId, opponent: d.OpponentId, decision: d });
    }
    for (const p of this.run.Passes ?? []) if (rider == null || p.RiderId === rider || p.OpponentId === rider)
      rows.push({ t: p.ConfirmedAtSeconds, kind: 'pass', rider: p.RiderId, opponent: p.OpponentId });
    for (const c of this.result.Contacts ?? []) if (rider == null || c.RiderA === rider || c.RiderB === rider)
      rows.push({ t: c.TimeSeconds, kind: 'contact', rider: c.RiderA, opponent: c.RiderB, contact: c });
    for (const c of this.run.Consequences ?? []) if (rider == null || c.RiderId === rider)
      rows.push({ t: c.TimeSeconds, kind: 'consequence', rider: c.RiderId, opponent: c.OpponentId, consequence: c });
    for (const [id, t] of Object.entries(this.result.FinishTimes)) if (rider == null || Number(id) === rider)
      rows.push({ t, kind: 'finish', rider: Number(id) });
    for (const change of this.planChanges ?? []) rows.push({ t: change.Time,
      kind: change.Phase === 'activated' ? 'planActivated' : 'planPrepared', rider: 1, change });
    for (const steering of this.steeringEvents ?? []) {
      rows.push({ t: steering.ObservedAt, kind:'steeringObserved', rider:1, steering });
      if (steering.ActualActivation != null) rows.push({ t:steering.ActualActivation, kind:'steeringActivated', rider:1, steering });
    }
    return rows.sort((a, b) => a.t - b.t);
  }
}
export function crossSection(geometry, reference) {
  const s = ((reference % geometry.lapMeters) + geometry.lapMeters) % geometry.lapMeters;
  const segment = geometry.segments.find(x => s < x.startReferenceMeters + x.lengthReferenceMeters) ?? geometry.segments.at(-1);
  const samples = segment.sections, index = clamp(upperBound(samples, s, x => x.referenceMeters) - 1, 0, samples.length - 2);
  const a = samples[index], b = samples[index + 1], fraction = (s - a.referenceMeters) / (b.referenceMeters - a.referenceMeters);
  const interpolate = key => ({ x: a[key].x + (b[key].x - a[key].x) * fraction, y: a[key].y + (b[key].y - a[key].y) * fraction });
  const point = interpolate('reference'), normal = interpolate('outward');
  const norm = Math.hypot(normal.x, normal.y); normal.x /= norm; normal.y /= norm;
  return { point, normal };
}
export function worldCapsules(geometry, state) {
  const c = Math.cos(state.heading), s = Math.sin(state.heading);
  const transform = p => ({ x: state.x + c * p.x - s * p.y, y: state.y + s * p.x + c * p.y });
  return geometry.capsules.map(part => {
    const steer = p => {
      if (part.component !== 'Handlebar' || state.steering == null) return p;
      const pivot = geometry.dimensions.handlebarLongitudinalOffsetMeters;
      const x = p.x - pivot, a = state.steering;
      return { x: pivot + Math.cos(a) * x - Math.sin(a) * p.y, y: Math.sin(a) * x + Math.cos(a) * p.y };
    };
    return { ...part, start: transform(steer(part.start)), end: transform(steer(part.end)) };
  });
}
