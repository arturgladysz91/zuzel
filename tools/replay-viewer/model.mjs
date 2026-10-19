// Recorded data only. This module contains no dynamics, track solver or prediction.
export const clamp = (x, lo, hi) => Math.max(lo, Math.min(hi, x));
const finite = (x, label) => { if (!Number.isFinite(x)) throw new Error(`Nieprawidłowa liczba: ${label}`); return x; };
export const angleDelta = (a, b) => Math.atan2(Math.sin(b - a), Math.cos(b - a));
export const angleAt = (a, b, fraction) => Math.atan2(Math.sin(a + angleDelta(a, b) * fraction), Math.cos(a + angleDelta(a, b) * fraction));
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
export function decisionStatus(d, time) {
  if (time < d.ObservedAt) return 'future';
  if (d.ActualActivation != null && d.ActualActivation <= time) return 'active';
  if (d.CancelledAt != null && d.CancelledAt <= time) return 'cancelled';
  return time >= d.RequestedActivation ? 'waiting' : 'pending';
}
export class Replay {
  constructor(csv, document) {
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
    for (const row of parseCsv(csv)) {
      const track = this.tracks.get(row.id);
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
  state(id, time) {
    time = clamp(finite(time, 'czas odtwarzania'), 0, this.duration);
    const track = this.tracks.get(id); if (!track) throw new Error('Nieznany zawodnik.');
    const leftIndex = Math.max(0, upperBound(track, time) - 1), left = track[leftIndex], right = track[leftIndex + 1];
    const finish = this.result.FinishTimes[String(id)];
    const finished = finish != null && time >= finish;
    // After the finish, use the last accepted point already present at this time.
    // An accepted post-crossing point is still inspectable at its own timestamp.
    const fraction = right && !finished ? clamp((time - left.t) / (right.t - left.t), 0, 1) : 0;
    const lerp = (a, b) => a + (b - a) * fraction;
    const state = { ...left, x: right ? lerp(left.x, right.x) : left.x, y: right ? lerp(left.y, right.y) : left.y,
      heading: right ? angleAt(left.heading, right.heading, fraction) : left.heading,
      reference: right ? lerp(left.reference, right.reference) : left.reference,
      sampleTime: left.t, poseKnownThrough: track.at(-1).t, interpolated: fraction > 0, stale: time > track.at(-1).t,
      finished, finish, intent: 0, lastDecision: null };
    const knownTime = Math.min(time, track.at(-1).t);
    for (const d of this.activated) {
      if (d.RiderId !== id || d.ActualActivation > knownTime) continue;
      const channel = d.Channels ?? 0;
      // Intent belongs to the line channel; a throttle carrying an old intent cannot replace it.
      if (channel === 0 || channel === 1) state.intent = d.Intent ?? 0;
      // A sample exactly at activation can precede Activate(), including Initial at t=0.
      if (d.ActualActivation < left.t) continue;
      if (channel === 0 || channel === 1) state.target = d.TargetOffsetMeters;
      if (channel === 0 || channel === 2) state.drive = d.Drive;
    }
    for (const d of this.decisions) if (d.RiderId === id && d.ObservedAt <= time &&
      (!state.lastDecision || d.ObservedAt >= state.lastDecision.ObservedAt)) state.lastDecision = d;
    const crossings = this.metrics.get(id)?.CrossingSeconds ?? [];
    state.completedLaps = upperBound(crossings, time, x => x);
    state.totalLaps = crossings.length || 4;
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
  return geometry.capsules.map(part => ({ ...part, start: transform(part.start), end: transform(part.end) }));
}
