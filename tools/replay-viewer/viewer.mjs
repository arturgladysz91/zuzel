import { Replay, clamp, enumName, decisionStatus, crossSection, worldCapsules } from './model.mjs';

const $ = id => document.getElementById(id);
const canvas = $('track'), context = canvas.getContext('2d');
const colors = ['#70e2e4', '#ffb36d', '#e5a5f5', '#b5d878'];
const sources = { solo: 'rolling-S3-entry', pack: 'rolling-straight' };
const dataRoot = '../../docs/experiments/autonomous-racing-line/';
const lineRoot = '../../docs/experiments/physics-driven-racing-line/';
const wheelRoot = '../../docs/experiments/rear-wheel-dynamics/';
const shapeRoot = '../../docs/experiments/track-geometry-variants/';
const format = (x, digits = 3) => Number(x).toFixed(digits);
const kindLabels = { observed: 'Obserwacja', requested: 'Termin planu', activated: 'Aktywacja', cancelled: 'Anulowanie', pass: 'Potwierdzone minięcie', contact: 'Kontakt', consequence: 'Konsekwencja', finish: 'Meta' };
const statusLabels = { future: 'Jeszcze nie zaobserwowano', active: 'Komenda aktywowana', cancelled: 'Anulowana, bez aktywacji', waiting: 'Termin planu minął — oczekuje', pending: 'Oczekuje na aktywację' };
let geometry, nativeGeometry, replay, time = 0, selected = 1, playing = false, dirty = true, loadGeneration = 0;
let width = 0, height = 0, dpr = 1, fitScale = 1, eventNodes = [], lastPanelTime = -1;
const camera = { x: 0, y: 0, scale: 1 };
const color = id => colors[[...replay.tracks.keys()].indexOf(id) % colors.length];
const worldToScreen = p => ({ x: width / 2 + (p.x - camera.x) * camera.scale, y: height / 2 - (p.y - camera.y) * camera.scale });
const screenToWorld = p => ({ x: camera.x + (p.x - width / 2) / camera.scale, y: camera.y - (p.y - height / 2) / camera.scale });
const pointPath = (points, close = false) => {
  context.beginPath(); points.forEach((p, i) => { const s = worldToScreen(p); i ? context.lineTo(s.x, s.y) : context.moveTo(s.x, s.y); });
  if (close) context.closePath();
};
function fit() {
  if (!geometry || !width || !height) return;
  const points = geometry.segments.flatMap(s => s.sections.flatMap(p => [p.inner, p.outer]));
  const minX = Math.min(...points.map(p => p.x)), maxX = Math.max(...points.map(p => p.x));
  const minY = Math.min(...points.map(p => p.y)), maxY = Math.max(...points.map(p => p.y));
  camera.x = (minX + maxX) / 2; camera.y = (minY + maxY) / 2;
  fitScale = Math.min((width - 100) / (maxX - minX), (height - 95) / (maxY - minY));
  camera.scale = fitScale; $('follow').checked = false; dirty = true;
}
function zoom(factor, anchor = { x: width / 2, y: height / 2 }) {
  const before = screenToWorld(anchor);
  camera.scale = clamp(camera.scale * factor, fitScale * .5, fitScale * 25);
  if (!$('follow').checked) { const after = screenToWorld(anchor); camera.x += before.x - after.x; camera.y += before.y - after.y; }
  dirty = true;
}
function resize() {
  const rect = $('canvas-wrap').getBoundingClientRect(); width = rect.width; height = rect.height;
  dpr = Math.min(devicePixelRatio || 1, 2); canvas.width = Math.round(width * dpr); canvas.height = Math.round(height * dpr);
  fit(); dirty = true;
}
function setTime(value, immediate = true) {
  if (!replay) return;
  time = clamp(Number(value), 0, replay.duration); $('scrub').value = String(time); dirty = true; if (immediate) lastPanelTime = -1;
}
function setPlaying(value) {
  playing = Boolean(value && replay);
  $('play').textContent = playing ? 'Ⅱ Pauza' : '▶ Odtwórz'; $('play').setAttribute('aria-label', playing ? 'Pauza' : 'Odtwarzaj');
}
function chooseRider(id) {
  selected = id;
  for (const button of $('riders').children) { const current = Number(button.dataset.id) === id; button.classList.toggle('selected', current); button.setAttribute('aria-pressed', String(current)); }
  $('follow-id').textContent = `R${id}`; rebuildEvents(); lastPanelTime = -1; dirty = true;
}
function rebuildEvents() {
  if (!replay) return;
  eventNodes = []; const fragment = document.createDocumentFragment();
  const events = replay.events($('all-events').checked ? null : selected);
  for (const event of events) {
    const button = document.createElement('button'); button.className = `event ${event.kind}`;
    const top = document.createElement('span'); top.className = 'event-top';
    const eventTime = document.createElement('span'); eventTime.className = 'event-time'; eventTime.textContent = `${format(event.t)} s`;
    const kind = document.createElement('span'); kind.textContent = kindLabels[event.kind]; top.append(eventTime, kind);
    const reason = document.createElement('span'); reason.className = 'event-reason';
    const detail = document.createElement('span'); detail.className = 'event-details';
    if (event.decision) {
      const d = event.decision, channel = enumName(geometry, 'channels', d.Channels ?? 0);
      reason.textContent = `R${event.rider} · ${d.Reason || enumName(geometry, 'action', d.Action)}`;
      detail.textContent = `${enumName(geometry, 'action', d.Action)} · ${enumName(geometry, 'intent', d.Intent ?? 0)}${d.OpponentId ? ` · rywal R${d.OpponentId}` : ''}`;
      const badge = document.createElement('span'); badge.className = `channel ${channel.toLowerCase()}`; badge.textContent = ` · ${channel}`; detail.append(badge);
      button.title = `${statusLabels[decisionStatus(d, time)]}\nObservedAt ${format(d.ObservedAt)} s\nRequestedActivation ${format(d.RequestedActivation)} s\nActualActivation ${d.ActualActivation == null ? 'brak' : format(d.ActualActivation) + ' s'}\nCancelledAt ${d.CancelledAt == null ? 'brak' : format(d.CancelledAt) + ' s'}\nTarget ${format(d.TargetOffsetMeters, 2)} m · Drive ${format(d.Drive * 100, 1)}%`;
    } else {
      reason.textContent = event.opponent ? `R${event.rider} / R${event.opponent}` : `R${event.rider}`;
      detail.textContent = event.kind === 'pass' ? 'Minięcie potwierdzone w zapisanych danych' : event.kind === 'finish' ? 'Czas przekroczenia z FinishTimes; bez dopisywania pozycji' : event.kind === 'contact' ? event.contact.Kind : enumName(geometry, 'severity', event.consequence.Severity);
    }
    button.append(top, reason, detail); button.addEventListener('click', () => { setPlaying(false); setTime(event.t); });
    button.setAttribute('aria-label', `${kindLabels[event.kind]}, ${format(event.t)} sekund, ${reason.textContent}`);
    fragment.append(button); eventNodes.push({ button, event, kind });
  }
  $('events').replaceChildren(fragment);
  $('event-summary').textContent = `${events.length} znaczników · kontakty: ${replay.result.Contacts.filter(c => c.Kind === 'begin').length} · potwierdzone minięcia: ${replay.run.Passes.length}`;
  dirty = true; lastPanelTime = -1;
}
function updatePanel() {
  const state = replay.state(selected, time), last = state.lastDecision;
  $('clock').textContent = `${format(time)} / ${format(replay.duration)} s`;
  $('rider-name').textContent = `R${selected}`; $('rider-name').style.color = color(selected);
  $('speed').textContent = format(state.speed * 3.6, 1); $('drive').textContent = `${format(state.drive * 100, 1)}%`;
  $('drive-fill').style.width = `${clamp(state.drive, 0, 1) * 100}%`;
  $('offset').textContent = `${format(state.offset, 2)} m`; $('target').textContent = `${format(state.target, 2)} m`;
  $('status').textContent = state.finished ? 'Finished' : enumName(geometry, 'activity', state.activity);
  $('status').classList.toggle('finished', state.finished);
  $('lap').textContent = state.finished ? `${state.totalLaps}/${state.totalLaps} · meta ${format(state.finish)} s` : `${state.lap}/${state.totalLaps} · ukończono ${state.completedLaps}`;
  $('intent').textContent = enumName(geometry, 'intent', state.intent);
  $('sample-at').textContent = `${format(state.sampleTime)} s${state.stale ? ' · ostatni' : state.interpolated ? ' · pozycja interpolowana' : ' · zaakceptowany'}`;
  $('last-reason').textContent = last ? last.Reason || enumName(geometry, 'action', last.Action) : 'Brak obserwacji w tym czasie';
  $('last-state').textContent = last ? `${statusLabels[decisionStatus(last, time)]} · ${enumName(geometry, 'channels', last.Channels ?? 0)} · obserwacja ${format(last.ObservedAt)} s · plan ${format(last.RequestedActivation)} s${last.ActualActivation != null && last.ActualActivation <= time ? ` · aktywacja ${format(last.ActualActivation)} s` : ''}${last.CancelledAt != null && last.CancelledAt <= time ? ` · anulowanie ${format(last.CancelledAt)} s` : ''}` : 'Pozycje i sterowanie pochodzą z zapisu.';
  $('previous').disabled = replay.step(time, -1) == null; $('next').disabled = replay.step(time, 1) == null;
  $('zoom-level').textContent = `${Math.round(camera.scale / fitScale * 100)}%`;
  let current = -1;
  eventNodes.forEach(({ button, event, kind }, index) => {
    button.classList.toggle('future', event.t > time);
    if (event.t <= time) current = index;
    if (event.kind === 'requested' && event.decision.CancelledAt != null && event.decision.CancelledAt <= time) kind.textContent = 'Plan anulowanej komendy';
    else kind.textContent = kindLabels[event.kind];
    if (event.decision) {
      const state = statusLabels[decisionStatus(event.decision, time)];
      button.setAttribute('data-command-state', state);
      // Keep all four provenance times accessible while scrubbing backwards too.
      button.title = state + button.title.slice(button.title.indexOf('\n'));
    }
  });
  eventNodes.forEach(({ button }, index) => button.classList.toggle('current', index === current));
  const absent = [...replay.tracks.keys()].map(id => replay.state(id, time)).filter(s => s.stale || s.finished && !s.interpolated && s.sampleTime < time);
  $('pose-note').hidden = absent.length === 0;
  $('pose-note').textContent = absent.map(s => `R${s.id}: ostatnia pozycja ${format(s.sampleTime)} s${s.finished ? ` · meta ${format(s.finish)} s` : ''}`).join(' | ') + ' · bez ekstrapolacji';
}
function draw() {
  context.setTransform(dpr, 0, 0, dpr, 0, 0); context.clearRect(0, 0, width, height);
  if (!geometry || !replay) return;
  if ($('follow').checked) { const rider = replay.state(selected, time); camera.x = rider.x; camera.y = rider.y; }
  // An unobtrusive metric grid is an overlay, not track geometry.
  const lo = screenToWorld({ x: 0, y: height }), hi = screenToWorld({ x: width, y: 0 });
  context.strokeStyle = '#20333b'; context.lineWidth = .5; context.setLineDash([]);
  for (let x = Math.floor(lo.x / 10) * 10; x <= hi.x; x += 10) { pointPath([{ x, y: lo.y }, { x, y: hi.y }]); context.stroke(); }
  for (let y = Math.floor(lo.y / 10) * 10; y <= hi.y; y += 10) { pointPath([{ x: lo.x, y }, { x: hi.x, y }]); context.stroke(); }
  const allSections = geometry.segments.flatMap(s => s.sections);
  pointPath(allSections.map(p => p.inner), true); context.fillStyle = '#19352f'; context.fill();
  for (const segment of geometry.segments) {
    pointPath([...segment.sections.map(p => p.outer), ...segment.sections.map(p => p.inner).reverse()], true);
    context.fillStyle = '#8f795d'; context.fill();
  }
  for (const edge of ['inner', 'outer']) { pointPath(allSections.map(p => p[edge]), true); context.strokeStyle = '#c3ac87'; context.lineWidth = 1.2; context.stroke(); }
  context.setLineDash([5, 5]); context.strokeStyle = '#b4e2ce9c'; context.lineWidth = .8;
  pointPath(allSections.map(p => p.reference), true); context.stroke(); context.setLineDash([]);
  if ($('segments').checked) for (const segment of geometry.segments) {
    const p = segment.sections[0]; pointPath([p.inner, p.outer]); context.strokeStyle = '#e4d8ba88'; context.lineWidth = .8; context.stroke();
    const label = worldToScreen(p.outer); context.font = '10px Segoe UI'; context.fillStyle = '#d3e1df'; context.fillText(`S${segment.index} ${segment.type}`, label.x + 6, label.y - 6);
  }
  const origin = geometry.segments[0].sections[0]; pointPath([origin.inner, origin.outer]); context.strokeStyle = '#f1e7d1'; context.lineWidth = 2; context.stroke();
  const start = worldToScreen(origin.outer); context.font = '10px Segoe UI'; context.fillStyle = '#b7c9d1'; context.fillText('ref. 0', start.x + 5, start.y + 15);
  const states = [...replay.tracks.keys()].map(id => replay.state(id, time));
  for (const state of states) {
    if ($('trails').checked) {
      const points = replay.tracks.get(state.id).filter(p => p.t <= time);
      if (state.interpolated) points.push(state);
      pointPath(points); context.strokeStyle = color(state.id); context.globalAlpha = state.id === selected ? .65 : .4;
      context.lineWidth = 1.6; context.stroke(); context.globalAlpha = 1;
    }
    if ($('targets').checked) {
      const section = crossSection(geometry, state.reference);
      const at = offset => ({ x: section.point.x + section.normal.x * offset, y: section.point.y + section.normal.y * offset });
      pointPath([at(state.offset), at(state.target)]); context.strokeStyle = '#ffe095'; context.lineWidth = 1; context.setLineDash([3, 3]); context.stroke(); context.setLineDash([]);
      const target = worldToScreen(at(state.target)); context.strokeStyle = '#ffe095'; context.lineWidth = 1.2; context.beginPath();
      context.moveTo(target.x - 5, target.y); context.lineTo(target.x + 5, target.y); context.moveTo(target.x, target.y - 5); context.lineTo(target.x, target.y + 5); context.stroke();
      if (state.id === selected) { context.font = '10px Segoe UI'; context.fillStyle = '#ffe095'; context.fillText(`cel ${format(state.target, 2)} m`, target.x + 9, target.y + 14); }
    }
  }
  for (const state of states) {
    const pos = worldToScreen(state); const ghost = state.stale || state.finished;
    if (state.id === selected) { context.beginPath(); context.arc(pos.x, pos.y, Math.max(10, camera.scale * 1.3), 0, Math.PI * 2); context.strokeStyle = color(state.id); context.lineWidth = 1; context.setLineDash(ghost ? [3, 3] : []); context.stroke(); context.setLineDash([]); }
    context.globalAlpha = ghost ? .5 : 1;
    for (const part of worldCapsules(geometry, state)) {
      pointPath([part.start, part.end]); context.lineCap = 'round'; context.strokeStyle = part.component === 'Handlebar' ? '#fff4db' : color(state.id);
      context.lineWidth = part.radiusMeters * 2 * camera.scale; context.stroke();
    }
    context.lineCap = 'butt'; context.globalAlpha = 1;
    const labelX = pos.x + 11, labelY = pos.y - 12;
    context.font = '600 12px Segoe UI'; context.lineWidth = 4; context.strokeStyle = '#10202d'; context.strokeText(`R${state.id}`, labelX, labelY);
    context.fillStyle = color(state.id); context.fillText(`R${state.id}`, labelX, labelY);
  }
  const rawMeters = 75 / camera.scale, unit = 10 ** Math.floor(Math.log10(rawMeters));
  const meters = unit * (rawMeters / unit >= 5 ? 5 : rawMeters / unit >= 2 ? 2 : 1), pixels = meters * camera.scale;
  context.strokeStyle = '#a4bac7'; context.lineWidth = 1; context.beginPath(); context.moveTo(width - 25 - pixels, height - 22); context.lineTo(width - 25, height - 22); context.stroke();
  context.font = '10px Segoe UI'; context.fillStyle = '#a4bac7'; context.fillText(`${meters} m`, width - 25 - pixels, height - 29);
}
async function loadScenario(key) {
  const generation = ++loadGeneration; setPlaying(false); $('load-state').hidden = false; $('load-state').classList.remove('error');
  $('load-state').textContent = 'Wczytywanie zaakceptowanych stanów…';
  try {
    const base = sources[key];
    const line = key.startsWith('shape-') ? key.slice(6) : key.startsWith('wheel-') ? key.slice(6) : key.startsWith('line-') ? key.slice(5) : null;
    const selectedRoot = key.startsWith('shape-') ? shapeRoot : key.startsWith('wheel-') ? wheelRoot : lineRoot;
    const [csvResponse, jsonResponse] = await Promise.all(line
      ? [fetch(`${selectedRoot}${line}-accepted.csv`), fetch(`${selectedRoot}${line}.json`)]
      : [fetch(`${dataRoot}${base}-accepted.csv`), fetch(`${dataRoot}${base}-ON.json`)]);
    if (!csvResponse.ok || !jsonResponse.ok) throw new Error('Nie znaleziono zapisów przejazdu. Uruchom serwer z katalogu repozytorium.');
    const [csv, json] = await Promise.all([csvResponse.text(), jsonResponse.json()]);
    let nextGeometry = nativeGeometry;
    if (key.startsWith('shape-')) {
      if (!/^[a-z-]+-geometry\.json$/.test(json.GeometryFile)) throw new Error('Brak geometrii tego przejazdu.');
      const response = await fetch(`${shapeRoot}${json.GeometryFile}`);
      if (!response.ok) throw new Error('Nie znaleziono geometrii tego przejazdu.');
      nextGeometry = await response.json();
    }
    if (generation !== loadGeneration) return;
    geometry = nextGeometry;
    document.querySelector('.map-title small').textContent = key.startsWith('shape-') ? `#101 · ${json.Geometry} · metry` : 'Geometria modelu · metry';
    replay = new Replay(csv, json); selected = [...replay.tracks.keys()][0];
    const buttons = [...replay.tracks.keys()].map(id => { const button = document.createElement('button'); button.textContent = `R${id}`;
      button.dataset.id = String(id); button.style.setProperty('--rider', color(id)); button.addEventListener('click', () => chooseRider(id)); return button; });
    $('riders').replaceChildren(...buttons); $('source-count').textContent = `${replay.tracks.size} ${replay.tracks.size === 1 ? 'motocykl' : 'motocykle'}`;
    $('scrub').max = String(replay.duration); setTime(0); chooseRider(selected); fit(); $('load-state').hidden = true;
  } catch (error) {
    if (generation !== loadGeneration) return;
    replay = null; $('load-state').hidden = false; $('load-state').classList.add('error');
    $('load-state').textContent = `${error.message}\nZ katalogu repo: py -m http.server 8000 --bind 127.0.0.1\nOtwórz http://127.0.0.1:8000/tools/replay-viewer/`; dirty = true;
  }
}
$('scenario').addEventListener('change', e => loadScenario(e.target.value));
$('play').addEventListener('click', () => { if (replay && time >= replay.duration) setTime(0); setPlaying(!playing); });
$('scrub').addEventListener('input', e => { setPlaying(false); setTime(e.target.value); });
for (const [id, direction] of [['previous', -1], ['next', 1]]) $(id).addEventListener('click', () => { if (!replay) return; setPlaying(false); const t = replay.step(time, direction); if (t != null) setTime(t); });
$('fit').addEventListener('click', fit); $('zoom-in').addEventListener('click', () => zoom(1.25)); $('zoom-out').addEventListener('click', () => zoom(.8));
$('follow').addEventListener('change', () => { if ($('follow').checked) camera.scale = Math.max(camera.scale, fitScale * 3); dirty = true; });
for (const id of ['trails', 'targets', 'segments']) $(id).addEventListener('change', () => { dirty = true; });
$('all-events').addEventListener('change', rebuildEvents);
canvas.addEventListener('wheel', e => { e.preventDefault(); const r = canvas.getBoundingClientRect(); zoom(Math.exp(-e.deltaY * .0015), { x: e.clientX - r.left, y: e.clientY - r.top }); }, { passive: false });
let drag = null;
canvas.addEventListener('pointerdown', e => { const rect = canvas.getBoundingClientRect(); drag = { x: e.clientX, y: e.clientY, startX: e.clientX, startY: e.clientY, rect, moved: false }; canvas.setPointerCapture(e.pointerId); });
canvas.addEventListener('pointermove', e => {
  if (!drag) return;
  const dx = e.clientX - drag.x, dy = e.clientY - drag.y;
  if (Math.hypot(e.clientX - drag.startX, e.clientY - drag.startY) > 3) drag.moved = true;
  if (drag.moved) { $('follow').checked = false; camera.x -= dx / camera.scale; camera.y += dy / camera.scale; dirty = true; }
  drag.x = e.clientX; drag.y = e.clientY;
});
canvas.addEventListener('pointerup', e => {
  if (drag && !drag.moved && replay) {
    const mouse = { x: e.clientX - drag.rect.left, y: e.clientY - drag.rect.top };
    const riders = [...replay.tracks.keys()].map(id => ({ id, ...worldToScreen(replay.state(id, time)) }));
    riders.sort((a, b) => Math.hypot(a.x - mouse.x, a.y - mouse.y) - Math.hypot(b.x - mouse.x, b.y - mouse.y));
    if (riders[0] && Math.hypot(riders[0].x - mouse.x, riders[0].y - mouse.y) < Math.max(20, camera.scale * 1.4)) chooseRider(riders[0].id);
  }
  drag = null;
});
canvas.addEventListener('pointercancel', () => { drag = null; });
document.addEventListener('keydown', e => {
  if (['INPUT', 'SELECT', 'BUTTON', 'TEXTAREA'].includes(document.activeElement.tagName)) return;
  if (e.code === 'Space') { e.preventDefault(); $('play').click(); }
  if (e.key === 'ArrowLeft' || e.key === 'ArrowRight') { e.preventDefault(); $(e.key === 'ArrowLeft' ? 'previous' : 'next').click(); }
});
new ResizeObserver(resize).observe($('canvas-wrap'));
let lastFrame = performance.now();
function frame(now) {
  if (playing && replay) { setTime(time + Math.min((now - lastFrame) / 1000, .1) * Number($('rate').value), false); if (time >= replay.duration) setPlaying(false); }
  lastFrame = now;
  if (dirty && geometry && replay) { draw(); if (lastPanelTime < 0 || Math.abs(time - lastPanelTime) >= .03 || !playing) { updatePanel(); lastPanelTime = time; } dirty = false; }
  requestAnimationFrame(frame);
}
requestAnimationFrame(frame);
try {
  const response = await fetch('./geometry.json'); if (!response.ok) throw new Error('Nie znaleziono geometrii modelu.');
  nativeGeometry = await response.json(); geometry = nativeGeometry; await loadScenario($('scenario').value);
} catch (error) { $('load-state').classList.add('error'); $('load-state').textContent = `${error.message}\nUruchom statyczny serwer HTTP z katalogu repozytorium (instrukcja w README).`; }
