import {awarenessReadout,knowledgeEdges} from './track-awareness.mjs';
import {planningPanel} from './multi-action.mjs';
import {speedThrottlePanel,throttleMarkers} from './speed-throttle.mjs';
import {causalPanel,causalMarkers} from './causal-yaw.mjs';
import {loadConstraints143,constraintsReadout,constraintsPanel} from './constraints143.mjs';
import {loadStanding144,setupStanding144,standingPanel144} from './standing144.mjs';
import {loadCorner145,setupCorner145} from './corner145.mjs';
import {loadPlanning146,setupPlanning146} from './planning146.mjs';
import {loadEfficient147,setupEfficient147} from './efficient147.mjs';
import {loadSpeed148,setupSpeed148} from './speed148.mjs';
import {loadTyre149,setupTyre149} from './tyre149.mjs';
import {loadRobust150,setupRobust150} from './robust150.mjs';
import {loadEfficient152,setupEfficient152} from './efficient152.mjs';
import {loadViable154,setupViable154} from './viable154.mjs?v=154';
import {loadSafety153,setupSafety153} from './safety153.mjs?v=153';
import {loadContingency151,setupContingency151} from './contingency151.mjs';
import { Replay, parseCsv, clamp, enumName, decisionStatus, crossSection, worldCapsules, authoredReplayDocument, motionVectors, bendJumps, boundaryStatus } from './model.mjs';
import { currentRecordings, readGzip } from './recordings.mjs';
import { loadContinuous } from './continuous.mjs';
import {loadStudy172} from './study172.mjs';
import {isH25, loadH25, raceKey, setupRaces, updateRaceTable} from './h25.mjs';
import { loadZigzag, createZigzagPanel } from './zigzag.mjs';
import { loadFreeRacing, updateFreeRacingPanel } from './free-racing.mjs';
import { loadLearning, setupLearning, updateLearningPanel } from './free-learning.mjs';
import { loadRobust, setupRobust, updateRobustPanel, robustColors } from './robust-learning.mjs';

const $ = id => document.getElementById(id);
const canvas = $('track'), context = canvas.getContext('2d');
const colors = ['#70e2e4', '#ffb36d', '#e5a5f5', '#b5d878'];
const sources = { solo: 'rolling-S3-entry', pack: 'rolling-straight' };
const dataRoot = '../../docs/experiments/autonomous-racing-line/';
const lineRoot = '../../docs/experiments/physics-driven-racing-line/';
const wheelRoot = '../../docs/experiments/rear-wheel-dynamics/';
const shapeRoot = '../../docs/experiments/track-geometry-variants/';
const localRoot = '../../docs/experiments/local-offset-limit/';
const widthRoot = '../../docs/experiments/progressive-bend-width/';
const inverseRoot = '../../docs/experiments/inverse-feasibility-matched-apex/';
const crossoverRoot = '../../docs/experiments/speed-grip-line-crossover/';
const currentGroup = document.createElement('optgroup'); currentGroup.label = 'Historyczna fizyka #106–#109 · pełny gaz';
for (const [key, recording] of Object.entries(currentRecordings)) {
  const option = document.createElement('option'); option.value = key; option.textContent = recording.label; currentGroup.append(option);
}
$('scenario').prepend(currentGroup);
const latestOption = document.createElement('option'); latestOption.value = 'latest-two-bends';
latestOption.textContent = 'HISTORYCZNY #123 · dwa łuki Egg Mild · 1 zawodnik';
$('scenario').prepend(latestOption);
const robustOption=document.createElement('option');robustOption.value='robust-learning';robustOption.textContent='#130 · Robust Learning · A/B i12 nowych przypadków';$('scenario').prepend(robustOption);
const extendedOption=document.createElement('option');extendedOption.value='extended-learning';extendedOption.textContent='#131 · Extended Learning · H06/H09 i dwa łuki';$('scenario').prepend(extendedOption);
const throttleOption=document.createElement('option');throttleOption.value='full-throttle-learning';throttleOption.textContent='#132 · Full Throttle Learning · gaz A/B i dwa łuki';$('scenario').prepend(throttleOption);
const awarenessOption=document.createElement('option');awarenessOption.value='track-awareness';awarenessOption.textContent='#134 · Track Awareness · geometria i reakcja';$('scenario').prepend(awarenessOption);
const planningOption=document.createElement('option');planningOption.value='multi-action';planningOption.textContent='#135 · Multi-Action Planning · sekwencje i Box2D';$('scenario').prepend(planningOption);
const speedThrottleOption=document.createElement('option');speedThrottleOption.value='speed-throttle';speedThrottleOption.textContent='#136 · Speed & Throttle · native A / interwencje';$('scenario').prepend(speedThrottleOption);
const causalOption=document.createElement('option');causalOption.value='causal-yaw';causalOption.textContent='#137 · Causal Yaw · autonomiczne A / B';$('scenario').prepend(causalOption);
const constraintsOption=document.createElement('option');constraintsOption.value='constraints143';constraintsOption.textContent='#143 · Audyt ograniczeń · PARTIAL · pełne A/B';$('scenario').prepend(constraintsOption);
const standingOption=document.createElement('option');standingOption.value='standing144';standingOption.textContent='#144 · Start0 → trzy okrążenia · solo / wspólny świat';$('scenario').prepend(standingOption);
const robust150Option=document.createElement('option');robust150Option.value='robust150';robust150Option.textContent='#150 · Odporna kontynuacja · PARTIAL';$('scenario').prepend(robust150Option);
const efficient152Option=document.createElement('option');efficient152Option.value='efficient152';efficient152Option.textContent='#152 · Wydajny planner warunkowy';$('scenario').prepend(efficient152Option);
const viable154Option=document.createElement('option');viable154Option.value='viable154';viable154Option.textContent='#154 · Pełny obrys0,5m i koszt planowania';$('scenario').prepend(viable154Option);
const safety153Option=document.createElement('option');safety153Option.value='safety153';safety153Option.textContent='#153 · Legacy oraz fizyczny obrys0,5m';$('scenario').prepend(safety153Option);
const contingency151Option=document.createElement('option');contingency151Option.value='contingency151';contingency151Option.textContent='#151 · Planowanie warunkowe';$('scenario').prepend(contingency151Option);
const tyre149Option=document.createElement('option');tyre149Option.value='tyre149';tyre149Option.textContent='#149 · Opony, prędkość i geometria';$('scenario').prepend(tyre149Option);
const speed148Option=document.createElement('option');speed148Option.value='speed148';speed148Option.textContent='#148 · Dokładna optymalizacja i przyczyny wolnej jazdy';$('scenario').prepend(speed148Option);
const efficient147Option=document.createElement('option');efficient147Option.value='efficient147';efficient147Option.textContent='#147 · Wydajny planner · pełne trzy okrążenia';$('scenario').prepend(efficient147Option);
const planning146Option=document.createElement('option');planning146Option.value='planning146';planning146Option.textContent='#146 · Plan sekwencji · prognoza i wykonanie';$('scenario').prepend(planning146Option);
const cornerOption=document.createElement('option');cornerOption.value='corner145';cornerOption.textContent='#145 · Straty w łukach · A/B/F/K1 i interwencje';$('scenario').prepend(cornerOption);
const decisionOption=document.createElement('option');decisionOption.value='rider-decision';decisionOption.textContent='#133 · Rider Decision Optimization · koszt i selekcja';$('scenario').prepend(decisionOption);
const learningOption=document.createElement('option');learningOption.value='free-learning';
learningOption.textContent='#129 · Free Racing Learning · proces uczenia i kamienie milowe';$('scenario').prepend(learningOption);
const freeOption=document.createElement('option');freeOption.value='free-racing';
freeOption.textContent='#128 · Free Racing · próba wstępna PARTIAL · trening zatrzymany';$('scenario').prepend(freeOption);
for (const [key,label] of [['predictive-baseline','Predykcja · baseline #123 / #124 · 19,98 s'],['predictive-candidate','Predykcja · kandydat · PARTIAL · 20,27 s']]) {
  const option=document.createElement('option'); option.value=key; option.textContent=label; $('scenario').prepend(option);
}
for (const [name, label] of [['inner', 'INNER · bezpieczna kontrola'], ['wide', 'WIDE STRAIGHT · bezpieczna kontrola'], ['stress', '200 km/h · ODRZUCONY']]) {
  const option = document.createElement('option'); option.value = 'crossover-viewer-' + name; option.textContent = '#106 · ' + label; $('scenario').append(option);
}
for (const name of ['matched-inner-entry-steady-18', 'matched-mid-entry-steady-18', 'matched-wide-entry-steady-18', 'matched-mid-early-steady-18', 'matched-wide-early-steady-18', 'inner-steady-15', 'outside-inside-outside-steady-15']) {
  const option = document.createElement('option'); option.value = 'inverse-' + name; option.textContent = '#105 · ' + name; $('scenario').append(option);
}
const widthColors = ['#ffb36d', '#70e2e4', '#e5a5f5'];
for(const [key,label] of [['zigzag-baseline','Zygzak #127 · baseline #124'],['zigzag-candidate','Zygzak #127 · kandydat #126']]){
  const option=document.createElement('option');option.value=key;option.textContent=label;$('scenario').prepend(option);
}
const study172Option=document.createElement('option');study172Option.value='study172';study172Option.textContent='Solo — #172 · S3-K4 · 3 okrążenia';
$('scenario').prepend($('scenario').querySelector('option[value="pack"]'));$('scenario').prepend(study172Option);
let zigzagPanel=null,zigzagDataPromise=null,zigzagWindow='main';
let widthGeometries = [];
let comparisonTrails = [];
let raceEntries = [];
let h25Compare = null;
let compareGeneration = 0;
let activeH25Key = null;
const format = (x, digits = 3) => Number(x).toFixed(digits);
const kindLabels = { observed: 'Obserwacja', requested: 'Termin planu', activated: 'Aktywacja', cancelled: 'Anulowanie', pass: 'Potwierdzone minięcie', contact: 'Kontakt', consequence: 'Konsekwencja', finish: 'Meta', tactic: 'Taktyka', lap: 'Okrążenie' };
Object.assign(kindLabels, { planPrepared:'Plan 2 przygotowany', planActivated:'Plan 2 aktywny' });
Object.assign(kindLabels, { steeringObserved:'Decyzja skrętu', steeringActivated:'Aktywacja skrętu' });
const statusLabels = { future: 'Jeszcze nie zaobserwowano', active: 'Komenda aktywowana', cancelled: 'Anulowana, bez aktywacji', waiting: 'Termin planu minął — oczekuje', pending: 'Oczekuje na aktywację' };
let geometry, nativeGeometry, replay, time = 0, selected = 1, playing = false, dirty = true, loadGeneration = 0;
let clipRecorder = null;
let width = 0, height = 0, dpr = 1, fitScale = 1, eventNodes = [], lastPanelTime = -1;
const camera = { x: 0, y: 0, scale: 1 };
const color = id => replay.robust?(replay.robustColor??robustColors[replay.robustPolicy]??colors[0]):colors[[...replay.tracks.keys()].indexOf(id) % colors.length];
const displayState = (id = selected) => replay.state(id, time, $('smooth').checked);
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
  fitScale = Math.min((width - 80) / (maxX - minX), (height - 100) / (maxY - minY));
  camera.scale = fitScale; $('follow').checked = false; dirty = true;
}
function zoom(factor, anchor = { x: width / 2, y: height / 2 }) {
  const before = screenToWorld(anchor);
  camera.scale = clamp(camera.scale * factor, fitScale * .5, Math.max(fitScale * 25, 120));
  if (!$('follow').checked) { const after = screenToWorld(anchor); camera.x += before.x - after.x; camera.y += before.y - after.y; }
  dirty = true;
}
function resize() {
  const previous = { ...camera }, following = $('follow').checked;
  const rect = $('canvas-wrap').getBoundingClientRect(); width = rect.width; height = rect.height;
  dpr = Math.min(devicePixelRatio || 1, 2); canvas.width = Math.round(width * dpr); canvas.height = Math.round(height * dpr);
  fit(); dirty = true;
  if (following) { Object.assign(camera, previous); $('follow').checked = true; }
}
function closeup() {
  if (!replay) return;
  const state = displayState(); camera.x = state.x; camera.y = state.y;
  camera.scale = Math.max(24, Math.min(38, Math.min(width, height) / 12));
  $('follow').checked = true; dirty = true;
}
function jumpBend(index) {
  if (!replay) return;
  const at = bendJumps(geometry, replay.tracks.get(selected))[index];
  if (at != null) { setPlaying(false); setTime(at); closeup(); }
}
function setTime(value, immediate = true) {
  if (!replay) return;
  time = clamp(Number(value), 0, replay.duration); $('scrub').value = String(time); dirty = true; if (immediate) lastPanelTime = -1;
  if(replay.freeRacing||replay.learning){const exact=replay.sampleTimes.find(t=>Math.abs(t-time)<1e-7);if(exact!=null)time=exact;}
}
function setPlaying(value) {
  playing = Boolean(value && replay);
  dirty = true; lastPanelTime = -1;
  $('play').disabled=playing; $('pause').disabled=!playing; $('play').setAttribute('aria-pressed',String(playing));
}
function chooseRider(id) {
  selected = id;
  for (const button of $('riders').children) { const current = Number(button.dataset.id) === id; button.classList.toggle('selected', current); button.setAttribute('aria-pressed', String(current)); }
  $('follow-rider').value = String(id);
  $('follow-id').textContent = `R${id}`; rebuildEvents(); lastPanelTime = -1; dirty = true;
  const jumps = bendJumps(geometry, replay.tracks.get(id));
  for (let i = 0; i < 2; i++) { $('bend-' + (i + 1)).disabled = jumps[i] == null; $('bend-' + (i + 1)).title = jumps[i] == null ? 'Brak zapisanego najazdu' : `Najazd 3 m przed łukiem · próbka ${format(jumps[i])} s`; }
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
    if (event.label) {
      reason.textContent = 'R' + event.rider + (event.opponent ? ' / R' + event.opponent : '') + ' · ' + event.label;
      detail.textContent = event.detail ?? '';
    } else if (event.decision) {
      const d = event.decision, channel = enumName(geometry, 'channels', d.Channels ?? 0);
      reason.textContent = `R${event.rider} · ${d.Reason || enumName(geometry, 'action', d.Action)}`;
      detail.textContent = `${enumName(geometry, 'action', d.Action)} · ${enumName(geometry, 'intent', d.Intent ?? 0)}${d.OpponentId ? ` · rywal R${d.OpponentId}` : ''}`;
      if(replay.learning)detail.textContent='Gaz i kierownica · bez celu poprzecznego';
      const badge = document.createElement('span'); badge.className = `channel ${channel.toLowerCase()}`; badge.textContent = ` · ${channel}`; detail.append(badge);
      button.title = `${statusLabels[decisionStatus(d, time)]}\nObservedAt ${format(d.ObservedAt)} s\nRequestedActivation ${format(d.RequestedActivation)} s\nActualActivation ${d.ActualActivation == null ? 'brak' : format(d.ActualActivation) + ' s'}\nCancelledAt ${d.CancelledAt == null ? 'brak' : format(d.CancelledAt) + ' s'}\n${replay.learning?'Brak celu poprzecznego':'Target '+format(d.TargetOffsetMeters,2)+' m'} · Drive ${format(d.Drive * 100, 1)}%`;
    } else if (event.steering) {
      reason.textContent = `R1 · korekta ${format(event.steering.CorrectionRadians * 180 / Math.PI, 2)}° · reakcja 0,24 s`;
      detail.textContent = `Obserwacja ${format(event.steering.ObservedAt)} s → aktywacja ${format(event.steering.ActualActivation)} s · impuls 0,1 s`;
      button.title = event.steering.Reason;
    } else if (event.change) {
      reason.textContent = 'Ten sam zawodnik · ciągłość obu łuków';
      detail.textContent = event.kind === 'planPrepared' ? 'Plan 1 działa podczas reakcji 0,24 s' : 'Zmiana planu bez resetu pozycji, gazu i kolejki';
    } else {
      reason.textContent = event.opponent ? `R${event.rider} / R${event.opponent}` : `R${event.rider}`;
      detail.textContent = event.kind === 'pass' ? 'Minięcie potwierdzone w zapisanych danych' : event.kind === 'finish' ? 'Czas przekroczenia z FinishTimes; bez dopisywania pozycji' : event.kind === 'contact' ? (event.contact.Kind === 'begin' && replay.h25 ? 'Początek kontaktu · ' + format(event.contact.ImpulseNewtonSeconds, 1) + ' Ns' : event.contact.Kind) : enumName(geometry, 'severity', event.consequence.Severity);
    }
    button.dataset.time = String(event.t);
    button.append(top, reason, detail); button.addEventListener('click', () => { setPlaying(false); setTime(event.t); });
    button.setAttribute('aria-label', `${kindLabels[event.kind]}, ${format(event.t)} sekund, ${reason.textContent}`);
    fragment.append(button); eventNodes.push({ button, event, kind });
  }
  $('events').replaceChildren(fragment);
  $('event-summary').textContent = `${events.length} znaczników · kontakty: ${replay.result.Contacts.filter(c => c.Kind === 'begin').length} · potwierdzone minięcia: ${replay.run.Passes.length}`;
  dirty = true; lastPanelTime = -1;
}
function sampleAtReference(points, value) {
  if (!points?.length || value < points[0].reference || value > points.at(-1).reference) return null;
  let lo = 0, hi = points.length - 1;
  while (lo < hi) {
    const mid = (lo + hi) >> 1;
    if (points[mid].reference < value) lo = mid + 1;
    else hi = mid;
  }
  return points[lo];
}

function updateComparisonChoices(entries) {
  raceEntries = entries;
  const select = $('h25-compare'), previous = select.value;
  const choices = entries.filter(entry => raceKey(entry.id) !== activeH25Key);
  select.replaceChildren(new Option('Wybierz…', ''), ...choices.map(entry => new Option(entry.label, raceKey(entry.id))));
  if (choices.some(entry => raceKey(entry.id) === previous)) select.value = previous;
}

function comparedController(recording) {
  return recording.exitStudy?.name.startsWith('#200') || recording.exitStudy?.native200 ? '#200' : recording.exitStudy?.native199 ? '#199' : recording.exitStudy?.native198 ? '#198' : recording.exitStudy?.native197 ? '#197' : recording.exitStudy?.native196 ? '#196' : recording.exitStudy?.name.startsWith('#194') ? '#194' : 'baza';
}

async function selectComparison(key) {
  const generation = ++compareGeneration;
  h25Compare = null;
  delete canvas.dataset.comparison;
  const status = $('h25-compare-info');
  status.textContent = key ? 'Ładowanie zapisu…' : 'Wybierz przejazd z archiwum.';
  dirty = true;
  if (!key || !replay?.h25) return;
  try {
    const result = await loadH25(key);
    if (generation !== compareGeneration) return;
    if (result.geometry.frameId !== geometry.frameId || result.geometry.lapMeters !== geometry.lapMeters)
      throw new Error('Nagrania muszą mieć tę samą geometrię i skalę.');
    h25Compare = result.replay;
    dirty = true;
  } catch (error) {
    if (generation === compareGeneration) status.textContent = error.message;
  }
}

let solo200 = [];
const colors200 = ['#95c6ff', '#ffb698', '#d4b6ff', '#82ebcd'];
const dashes200 = [[2, 4], [7, 4], [11, 4, 2, 4], []];

function drawSolo200(state) {
  if (!replay.exitStudy?.native200 || !$('solo200-comparison').open || solo200.length !== 4) {
    delete canvas.dataset.solo200;
    return;
  }
  const length = Number(geometry.lapMeters);
  const lap = Math.min(2, Math.max(0, Math.floor(state.reference / length)));
  const labels = [['a', '#199 A'], ['b', '#199 B'], ['prev', '#199 B płynny'], ['new', '#200 C']];
  const displayed = [];
  const readout = [];
  for (const [index, race] of solo200.entries()) {
    const [key, label] = labels[index];
    if (!$('trace200-' + key).checked) continue;
    const samples = race.tracks.get(1);
    const row = sampleAtReference(samples, state.reference);
    const bounds = samples.filter(sample => sample.reference >= lap * length && sample.reference <= (lap + 1) * length);
    if (bounds.length > 1) {
      context.save();
      context.strokeStyle = colors200[index];
      context.lineWidth = 2.1;
      context.setLineDash(dashes200[index]);
      pointPath(bounds);
      context.stroke();
      context.restore();
    }
    if (!row) continue;
    const p = worldToScreen(row);
    context.fillStyle = colors200[index];
    context.beginPath();
    context.arc(p.x, p.y, 4, 0, Math.PI * 2);
    context.fill();
    const crossings = race.run.Riders[0].Measurement.CrossingSeconds;
    const time = crossings[lap] - (lap ? crossings[lap - 1] : 0);
    const formatLap = Number.isFinite(time) ? format(time, 3) + ' s' : '—';
    const drive = Number.isFinite(row.drive) ? format(row.drive * 100, 0) + '%' : '—';
    const clearance = Number.isFinite(row.outer) ? format(row.outer, 2) + ' m' : '—';
    readout.push(label + ' · ' + format(row.speed * 3.6, 1) + ' km/h · okr. ' + (lap + 1) + ' ' + formatLap +
        '\ngaz ' + drive + ' · banda ' + clearance);
    displayed.push({ label, x: row.x, y: row.y, reference: row.reference, speed: row.speed });
  }
  $('trace200-readout').textContent = readout.join('\n');
  canvas.dataset.solo200 = JSON.stringify(displayed);
}

let solo199 = [];
const palette199 = ['#78ecd2','#ffd682','#b2c1ff'];

function drawSolo199(state) {
  if (!replay.exitStudy?.native199 || !$('solo199-comparison').open || solo199.length!==4) {
    delete canvas.dataset.solo199; return;
  }
  const length=Number(geometry.lapMeters),lap=Math.min(2,Math.max(0,Math.floor(state.reference/length)));
  const from=lap*length,to=(lap+1)*length;
  const owners=[['a','A'],['b','B'],['base','#198']];
  const applied=[];
  const readout=[];
  const selectedRuns = [solo199[0],solo199[$('trace199-b-version').value==='fast'?3:1],solo199[2]];
  for (const [i, item] of selectedRuns.entries()) {
    const [id,tag]=owners[i];
    if (!$('trace199-'+id).checked) continue;
    const path = item.tracks.get(1);
    const near=sampleAtReference(path,state.reference);
    const bounds=path.filter(s=>s.reference>=from && s.reference<=to);
    if (bounds.length>1) {
      context.save(); context.strokeStyle=palette199[i]; context.lineWidth=2.1;
      context.setLineDash(i===0?[]:i===1?[8,4]:[2,4]);
      pointPath(bounds); context.stroke(); context.restore();
    }
    if (near) {
      const p=worldToScreen(near); context.fillStyle=palette199[i];
      context.beginPath(); context.arc(p.x,p.y,4,0,Math.PI*2); context.fill();
      applied.push({tag,reference:near.reference,x:near.x,y:near.y});
      const crossed=item.run.Riders?.[0]?.Measurement?.CrossingSeconds??[];
      const lapTime=crossed[lap]-(lap?crossed[lap-1]:0);
      readout.push(tag+' · '+format(near.speed*3.6,1)+' km/h · okr. '+(lap+1)+
        ' '+format(lapTime,3)+' s');
    }
  }
  $('trace199-readout').textContent=readout.join('\n');
  canvas.dataset.solo199=JSON.stringify(applied);
}
function drawH25Comparison(state) {
  if (!h25Compare || !$('h25-comparison').open) return;
  const coordinates = h25Compare.tracks.get(state.id);
  const other = sampleAtReference(coordinates, state.reference);
  if (!other) {
    delete canvas.dataset.comparison;
    $('h25-compare-info').textContent = 'Drugi zapis nie zawiera tego zawodnika lub odcinka.';
    return;
  }
  const shade = recording => (['#194',comparedController(replay)==='#198'?'#198':'#197']).includes(comparedController(recording)) ? '#80f3d5' : '#fac685';
  const start = state.reference - 29, end = state.reference + 11;
  for (const [recording, index] of [[replay, 0], [h25Compare, 1]]) {
    const line = recording.tracks.get(state.id).filter(p => p.reference >= start && p.reference <= end);
    if (line.length < 2) continue;
    context.save();
    context.strokeStyle = shade(recording);
    context.fillStyle = shade(recording);
    context.lineWidth = 2.6;
    context.setLineDash(index ? [6, 4] : []);
    pointPath(line);
    context.stroke();
    context.setLineDash([]);
    for (const exit of recording.exitStudy?.exits[state.id - 1] ?? []) {
      for (const [seconds, label] of [[exit.taperStart, 'Prostowanie'], [exit.secondTurn, 'Drugi skręt']]) {
        if (seconds == null) continue;
        const marker = recording.state(state.id, seconds, false);
        if (marker.reference < start || marker.reference > end) continue;
        const p = worldToScreen(marker);
        context.beginPath();
        context.arc(p.x, p.y, 4.5, 0, 2 * Math.PI);
        context.fill();
        context.font = '11px Segoe UI';
        const y = p.y + (index ? 17 : -11);
        context.lineWidth = 3;
        context.strokeStyle = '#10202d';
        context.strokeText(label, p.x + 7, y);
        context.fillText(label, p.x + 7, y);
        context.strokeStyle = shade(recording);
      }
    }
    context.restore();
  }
  const screen = worldToScreen(other);
  context.save();
  context.strokeStyle = shade(h25Compare);
  context.lineWidth = 1.5;
  context.beginPath(); context.arc(screen.x, screen.y, 8, 0, 2 * Math.PI); context.stroke();
  const course = Math.atan2(other.vy, other.vx);
  for (const [angle, color] of [[other.heading, '#ffe095'], [course, '#95c9ff']]) {
    pointPath([other, {x: other.x + 3.5 * Math.cos(angle), y: other.y + 3.5 * Math.sin(angle)}]);
    context.strokeStyle = color;
    context.stroke();
  }
  context.restore();
  const outer = value => Number.isFinite(value) ? format(value, 2) + ' m' : '—';
  $('h25-compare-info').textContent =
    comparedController(replay) + ' · ' + format(state.speed*3.6,1) + ' km/h · ' + format(state.sampleTime,3) +
    ' s · banda ' + outer(state.outer) + ' | ' + comparedController(h25Compare) + ' · ' +
    format(other.speed*3.6,1) + ' km/h · ' + format(other.t,3) + ' s · banda ' + outer(other.outer) +
    ' · ślad odniesienia przerywany';
  canvas.dataset.comparison = JSON.stringify({reference: other.reference, x: other.x, y: other.y, heading: other.heading, course});
}

function exitDecision(state) {
  const test = replay.exitStudy?.native200 ?? replay.exitStudy?.native199 ?? replay.exitStudy?.native198 ?? replay.exitStudy?.native197 ?? replay.exitStudy?.native196;
  if (test && state.sampleTime >= test.start) return null;
  return replay.exitStudy?.decisions[state.id-1]?.findLast(d => d.t <= state.sampleTime + 1e-8);
}

function exitTarget(decision) {
  if (!decision?.reference) return null;
  const plan = decision.adjustment;
  if (plan?.Action)
    return {x: plan.TargetX, y: plan.TargetY, curvature: plan.PlannedCurvature};
  return {x: decision.reference.X, y: decision.reference.Y, curvature: decision.reference.AheadCurvature};
}

function updateExitPanel(state) {
  const decision = exitDecision(state);
  const goal = exitTarget(decision);
  const percent = value => Number.isFinite(value) ? format(value*100, 1)+'%' : '—';
  const degrees = (value, suffix='°') => Number.isFinite(value) ? format(value*180/Math.PI, 2)+suffix : '—';
  $('exit-readout').textContent =
    'β ' + degrees(state.beta) + ' · przechył ' + degrees(state.lean) +
    ' · yaw ' + degrees(state.yaw,'°/s') + ' · gaz zadany ' + percent(state.requestedDrive) +
    ' · aktywny ' + percent(state.drive) + ' · skręt zadany ' + degrees(state.requestedSteering) +
    ' · kolejka ' + (state.pending ?? '—') +
    ((replay.exitStudy.native200 || replay.exitStudy.native199 || replay.exitStudy.native198 || replay.exitStudy.native197 || replay.exitStudy.native196) ? ' · heading ' + degrees(state.heading) +
      ' · kierunek ruchu ' + degrees(Math.atan2(state.vy,state.vx)) : '') +
    ((replay.exitStudy.native200 || replay.exitStudy.native199 || replay.exitStudy.native198 || replay.exitStudy.native197 || replay.exitStudy.native196) && Number.isFinite(state.swept) ? ' · swept ' + format(state.swept,3) + ' m' : '') +
    (replay.exitStudy.native198 ? ' · żółte punkty: przyjęte plany · kropkowany odcinek: prognoza 50 ms' : '') +
    (Number.isFinite(state.outer) ? ' · banda ' + format(state.outer, 2) + ' m' : '') + (goal ? ' · κ celu ' + format(goal.curvature,4) + ' 1/m' : '');
  $('intent').textContent = replay.exitStudy.native200 ? 'Technika jazdy · #200' : replay.exitStudy.native199 ? 'Uczenie na pełnym cyklu · #199' : replay.exitStudy.native198 ? 'Zweryfikowana kontynuacja · #198' : replay.exitStudy.native197 ? 'Optymalizacja w pętli · #197' : replay.exitStudy.native196 ? 'Zapis natywny · #196' : decision?.wide ? 'Plan szerokiej prostej · #194' :
    decision?.adjustment?.Action ? 'Prognoza H25 · #193' : 'FAST50 · #191';
  $('target').textContent = decision ? format(decision.target, 2)+' m' : '—';
  const jump = $('exit-jump');
  const key = replay.h25.fingerprint + ':' + state.id;
  if (jump.dataset.key !== key) {
    jump.dataset.key = key;
    const exits = replay.exitStudy.exits[state.id-1] ?? [];
    const test = replay.exitStudy.native200 ?? replay.exitStudy.native199 ?? replay.exitStudy.native198 ?? replay.exitStudy.native197 ?? replay.exitStudy.native196;
    const points = test?.tests?.[state.id-1]?.points ?? test?.points ?? (test ? [['Początek prostej', test.start], ['Vmax', test.peak], ['Wejście w łuk', test.entry],
      ['Wyjście z łuku', test.exit], ['20 m za łukiem', test.goal]] :
      exits.map(e => ['Okr. '+e.lap+' · łuk '+e.bend+' · '+format(e.time,2)+' s', e.taperStart ?? e.time]));
    jump.replaceChildren(new Option('Wybierz…',''), ...points.map(([label,at]) => new Option(label,at)));
  }
}

function updatePanel() {
  zigzagPanel?.update(time);
  const state = displayState(), last = state.lastDecision, motion = motionVectors(state);
  if (replay.h25) {
    updateRaceTable(replay, time, $('follow').checked ? selected : null, followRider);
    $('race-outcome').hidden = time < replay.duration - 1e-8;
    $('race-outcome').textContent = outcomeLabel(replay.h25.outcome);
    $('lean').textContent = state.lean == null ? '—' : format(state.lean * 180 / Math.PI, 1) + '°';
  }
  $('clock').textContent = `${format(time)} / ${format(replay.duration)} s`; syncRate();
  $('rider-name').textContent = `R${selected}`; $('rider-name').style.color = color(selected);
  $('speed').textContent = format(state.speed * 3.6, 1); $('drive').textContent = `${format(state.drive * 100, 1)}%`;
  $('drive-fill').style.width = `${clamp(state.drive, 0, 1) * 100}%`;
  $('offset').textContent = `${format(state.offset, 2)} m`; $('target').textContent = `${format(state.target, 2)} m`;
  if (replay.freeRacing) {
    $('target').textContent='brak narzuconej linii';$('intent').textContent='gaz i kierownica';
    $('comparison-legend').textContent='Turkusowy: natywny Free Racing. Pomarańczowy: historyczny #124 (18m/s, offset1m — inne warunki); wyłącznie odniesienie.';
    updateFreeRacingPanel($('free-racing-analysis'),replay,time);
  }
  if(replay.learning&&!replay.robust){$('target').textContent='brak narzuconej linii';updateLearningPanel($('learning-telemetry'),replay,time);}
  if(replay.robust){$('target').textContent='brak narzuconej linii';updateRobustPanel($('robust-telemetry'),replay,time);if(replay.constraintsData)constraintsPanel($('constraints143-panel'),replay.constraintsData,time);if(replay.causalData)causalPanel($('causal-yaw-panel'),replay.causalData,time);if(replay.throttleData)speedThrottlePanel($('speed-throttle-panel'),replay.throttleData,time);if(replay.planningData){const data=replay.planningData;planningPanel($('planning-panel'),data.group.Plans?.find(p=>p.Arm===replay.robustPolicy),data.document,time,data.group);}}
  $('status').textContent = replay.h25
    ? (state.finished ? 'Na mecie' : (['Jedzie', 'Utrata rytmu', 'Upadek'][state.activity] ?? 'Przerwano'))
    : (state.finished ? 'Finished' : enumName(geometry, 'activity', state.activity));
  if ((replay.exitStudy?.native196 && time >= replay.exitStudy.native196.goal) ||
      (replay.exitStudy?.native197?.complete && time >= replay.exitStudy.native197.goal))
    $('status').textContent = 'Odcinek ukończony';
  $('status').classList.toggle('finished', state.finished);
  $('lap').textContent = replay.planAt ? 'Dwa łuki · bez pełnego okrążenia' : state.finished ? `${state.totalLaps}/${state.totalLaps} · meta ${format(state.finish)} s` : `${state.lap}/${state.totalLaps} · ukończono ${state.completedLaps}`;
  const hudScope=replay.planAt?'dwa łuki':replay.freeRacing?'próba wstępna':replay.learning?'próba diagnostyczna':state.finished?'Meta':`okr. ${state.lap}/${state.totalLaps}`;
  $('race-hud').textContent=`R${selected} · ${format(state.speed*3.6,1)} km/h · ${hudScope}`;
  $('steering').textContent = state.steering == null ? 'brak zapisu' : `${format(state.steering * 180 / Math.PI, 2)}°`;
  $('steering').title = 'steer_before: zaakceptowany kąt w czasie próbki; bez interpolacji. Kierownica w skali obraca się tym kątem.';
  $('active-plan-row').hidden = !replay.planAt;
  if (replay.planAt) {
    const plan = replay.planAt(time);
    $('active-plan').textContent = `Plan ${plan}${plan === 1 ? ' · #122' : ' · #123 od 10,15 s'}`;
    $('comparison-legend').textContent = `Zielona przerywana: aktywny plan ${plan}. Turkusowa: rzeczywisty ślad XY.`;
  }
  $('intent').textContent = enumName(geometry, 'intent', state.intent);
  if (replay.h25) {
    $('target').textContent = 'Cel ruchomy';
    $('intent').textContent = 'Autonomia #191';
  }
  if (replay.exitStudy && $('exit-diagnostics').open) updateExitPanel(state);
  if(replay.study172){$('target').textContent='brak narzuconej linii';$('intent').textContent='Autonomiczny K4 · #172';}
  if(replay.freeRacing){$('intent').textContent='Autonomiczne działania';$('lap').textContent='Kontrola wstępna · nieukończony';}
  if(replay.learning){$('intent').textContent='Wyuczone preferencje gazu i kierownicy';$('lap').textContent=replay.episodeScope??(replay.learning.Metrics.Completed?'Pierwszy łuk + 1s kontroli na wyjściu':'Próba nieukończona');}
  if(replay.robust?.Replay.Name.startsWith('two-bends'))$('lap').textContent='Dwa kolejne łuki · '+(replay.learning.Metrics.Completed?'ukończone':'próba nieukończona');
  $('sample-at').textContent = `${format(state.sampleTime)} s${state.stale ? ' · ostatni' : state.interpolated ? ' · pozycja interpolowana' : ' · zaakceptowany'}`;
  $('slip-angle').textContent = motion.beta == null ? '—' : `${format(motion.beta * 180 / Math.PI, 1)}°`;
  $('body-angle').textContent = `${format(state.heading * 180 / Math.PI, 1)}°`;
  $('velocity-angle').textContent = motion.beta == null ? '—' : `${format(motion.travel * 180 / Math.PI, 1)}°`;
  $('vector-note').textContent = state.interpolated ? 'XY, heading i wektory interpolowane do renderingu; bez nowych kroków fizyki.' : `Geometria, wektory i β z tego samego snapshotu ${format(state.sampleTime)} s.`;
  $('last-reason').textContent = last ? last.Reason || enumName(geometry, 'action', last.Action) : 'Brak obserwacji w tym czasie';
  $('last-state').textContent = last ? `${statusLabels[decisionStatus(last, time)]} · ${enumName(geometry, 'channels', last.Channels ?? 0)} · obserwacja ${format(last.ObservedAt)} s · plan ${format(last.RequestedActivation)} s${last.ActualActivation != null && last.ActualActivation <= time ? ` · aktywacja ${format(last.ActualActivation)} s` : ''}${last.CancelledAt != null && last.CancelledAt <= time ? ` · anulowanie ${format(last.CancelledAt)} s` : ''}` : 'Pozycje i sterowanie pochodzą z zapisu.';
  if (replay.planAt && last) $('last-state').textContent += ` · żądanie ${format(last.Drive * 100, 1)}% (oddzielne od aktywnego Drive powyżej)`;
  if(replay.standingData){
    $('target').textContent='brak narzuconej linii';$('intent').textContent=replay.standingData.studyNumber===154?'Bezpieczne kontynuacje · #154':replay.standingData.studyNumber===153?'Planowanie warunkowe · #153':replay.standingData.studyNumber===147?'Wydajny planner · #147':replay.standingData.analysis146?'Planowanie sekwencji · #146':replay.standingData.cornerAnalysis?'Zapis bazowy / interwencja badawcza · #145':'Autonomiczne gaz / kierownica · #143';
    standingPanel144($('standing-telemetry'),replay.standingData,time,selected);
    const outcome=replay.standingData.run.Riders.find(r=>r.Spec.Profile.Id===selected).Outcome;
    if(time>=replay.duration-1e-8&&!state.finished)$('status').textContent=outcome;
  }
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
function drawMotion(state) {
  const motion = motionVectors(state), start = worldToScreen(state);
  const arrow = (dx, dy, shade, label) => {
    const tip = worldToScreen({ x: state.x + dx, y: state.y + dy }), angle = Math.atan2(tip.y - start.y, tip.x - start.x);
    context.strokeStyle = shade; context.fillStyle = shade; context.lineWidth = 2; context.setLineDash([]);
    context.beginPath(); context.moveTo(start.x, start.y); context.lineTo(tip.x, tip.y); context.stroke();
    context.beginPath(); context.moveTo(tip.x, tip.y);
    context.lineTo(tip.x - 9 * Math.cos(angle - .4), tip.y - 9 * Math.sin(angle - .4));
    context.lineTo(tip.x - 9 * Math.cos(angle + .4), tip.y - 9 * Math.sin(angle + .4)); context.closePath(); context.fill();
    if (camera.scale >= 12) { context.font = '600 11px Segoe UI'; context.fillText(label, tip.x + 8, tip.y + (label === 'heading' ? -14 : 16)); }
  };
  arrow(motion.heading.x * 3.4, motion.heading.y * 3.4, '#ffd27b', 'heading');
  if (motion.beta != null) {
    arrow(motion.velocity.x, motion.velocity.y, '#8eb8ff', 'v · 0,2 s');
    const radius = 2.3, points = Array.from({ length: 25 }, (_, i) => {
      const angle = motion.travel + motion.beta * i / 24;
      return { x: state.x + radius * Math.cos(angle), y: state.y + radius * Math.sin(angle) };
    });
    pointPath(points); context.strokeStyle = '#edb1ec'; context.lineWidth = 2; context.stroke();
    if (camera.scale >= 12) {
      const angle = motion.travel + motion.beta / 2, at = worldToScreen({ x: state.x + 1.7 * Math.cos(angle), y: state.y + 1.7 * Math.sin(angle) });
      context.font = '600 12px Segoe UI'; context.fillStyle = '#f3c4f3'; context.fillText(`β ${format(motion.beta * 180 / Math.PI, 1)}°`, at.x + 3, at.y + 16);
    }
  }
}
function drawOverview(sections, states) {
  if (camera.scale < fitScale * 2 || width < 600) return;
  const size = 172, left = width - size - 14, top = 14;
  const outer = sections.map(p => p.outer), inner = sections.map(p => p.inner);
  const minX = Math.min(...outer.map(p => p.x)), maxX = Math.max(...outer.map(p => p.x));
  const minY = Math.min(...outer.map(p => p.y)), maxY = Math.max(...outer.map(p => p.y));
  const scale = Math.min((size - 20) / (maxX - minX), 78 / (maxY - minY));
  const at = p => ({ x: left + size / 2 + (p.x - (minX + maxX) / 2) * scale, y: top + 64 - (p.y - (minY + maxY) / 2) * scale });
  context.save(); context.setLineDash([]); context.fillStyle = '#10202ded'; context.fillRect(left, top, size, 116);
  context.strokeStyle = '#3d5565'; context.lineWidth = 1; context.strokeRect(left, top, size, 116);
  context.font = '10px Segoe UI'; context.fillStyle = '#b6c8d5'; context.fillText('Cały tor · oba brzegi', left + 10, top + 15);
  for (const [points, shade] of [[outer, '#e8b67e'], [inner, '#e4e8de']]) {
    context.beginPath(); points.forEach((p, i) => { const s = at(p); i ? context.lineTo(s.x, s.y) : context.moveTo(s.x, s.y); });
    context.closePath(); context.strokeStyle = shade; context.stroke();
  }
  for (const state of states) { const p = at(state); context.beginPath(); context.arc(p.x, p.y, 3, 0, Math.PI * 2); context.fillStyle = color(state.id); context.fill(); }
  context.restore();
}
function draw() {
  context.setTransform(dpr, 0, 0, dpr, 0, 0); context.clearRect(0, 0, width, height);
  if (!geometry || !replay) return;
  const advancedView=document.body.classList.contains('advanced');
  if ($('follow').checked) { const rider = displayState(); camera.x = rider.x; camera.y = rider.y; }
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
  for (const edge of ['inner', 'outer']) { pointPath(allSections.map(p => p[edge]), true); context.strokeStyle = edge === 'inner' ? '#e4e8de' : '#e8b67e'; context.lineWidth = edge === 'outer' ? 2 : 1.2; context.stroke(); }
  if (advancedView && $('outer-limit').checked && geometry.localOuterLimit) {
    pointPath(geometry.localOuterLimit.sections.map(p => p.point), true);
    context.strokeStyle = '#ffcf78'; context.lineWidth = 1; context.setLineDash([3, 4]); context.stroke(); context.setLineDash([]);
  }
  if (advancedView && $('compare-widths').checked) for (const [i, shape] of widthGeometries.entries()) {
    pointPath(shape.segments.flatMap(s => s.sections.map(p => p.outer)), true);
    context.strokeStyle = widthColors[i]; context.lineWidth = 1.5; context.stroke();
  }
  if(replay.robust){
    const knowledge=awarenessReadout(replay.robust,time).knowledge;
    const edges=knowledgeEdges(knowledge,geometry.innerReferenceOffsetMeters);
    if(edges.length){
      for(const edge of ['inner','outer']){pointPath(edges.map(p=>p[edge]));context.strokeStyle='#d99fe3';context.lineWidth=3;context.stroke();}
      for(const p of edges.filter((_,i)=>i%4===0)){pointPath([p.inner,p.outer]);context.strokeStyle='#d99fe34d';context.lineWidth=1;context.stroke();}
      for(const [ref,label] of [[knowledge.NextCornerStartReferenceMeters,'Łuk początek'],[knowledge.NextCornerEndReferenceMeters,'Łuk koniec']]){
        const section=allSections.reduce((best,p)=>Math.abs(p.referenceMeters-ref)<Math.abs(best.referenceMeters-ref)?p:best);
        pointPath([section.inner,section.outer]);context.strokeStyle='#b4f1ba';context.lineWidth=3;context.stroke();
        const pos=worldToScreen(section.outer);context.fillStyle='#b4f1ba';context.font='11px Segoe UI';context.fillText(label,pos.x+4,pos.y-4);
      }
    }
  }
  if (advancedView && $('compare-lines').checked) for (const line of comparisonTrails) {
    if(line.visible===false)continue;
    if (line.generation && line.generation !== replay.planAt(time)) continue;
    pointPath(line.points); context.strokeStyle = line.color; context.lineWidth = 1.8; context.globalAlpha = .8; context.setLineDash(line.planned ? [8, 5] : []); context.stroke(); context.globalAlpha = 1; context.setLineDash([]);
    if(line.gainSamples)for(const sample of line.gainSamples){
      const p=worldToScreen({x:sample.NewXY.X,y:sample.NewXY.Y});context.fillStyle=sample.GainSeconds>=0?'#8be08f':'#ff9cbe';
      context.beginPath();context.arc(p.x,p.y,2.5,0,Math.PI*2);context.fill();
      if((sample.ReferenceMeters-30)%25===0){context.font='10px Segoe UI';context.fillText(`${sample.GainSeconds>=0?'+':''}${sample.GainSeconds.toFixed(2)}s`,p.x+5,p.y-5);}
    }
  }
  if(replay.standingData?.cornerAnalysis){
    const c=replay.standingData.cornerAnalysis.corners.find(c=>c.run===replay.standingData.run.Name&&c.entry.time_s<=time&&c.exit.time_s>=time);
    if(c)for(const [label,q,color] of [['V min',c.minimum,'#fff07b'],['Max opóźnienie',c.worst_deceleration,'#ff8eaa']]){
      const p=worldToScreen({x:q.x_m,y:q.y_m});context.beginPath();context.arc(p.x,p.y,4,0,Math.PI*2);context.strokeStyle=color;context.lineWidth=2;context.stroke();
      context.fillStyle=color;context.font='11px Segoe UI';context.fillText(label,p.x+7,p.y-5);
    }
  }
  if(replay.constraintsData&&$('compare-lines').checked)for(const r of constraintsReadout(replay.constraintsData,time)){
    if(comparisonTrails.find(c=>c.policy===r.policy)?.visible===false)continue;
    const p=worldToScreen({x:r.sample.Input.Rider.Position.X,y:r.sample.Input.Rider.Position.Y});
    context.beginPath();context.arc(p.x,p.y,4,0,Math.PI*2);context.fillStyle=r.color;context.fill();
    context.font='11px Segoe UI';context.fillText(r.policy+(r.ended?' STOP':''),p.x+6,p.y-5);
  }
  context.setLineDash([5, 5]); context.strokeStyle = '#b4e2ce9c'; context.lineWidth = .8;
  pointPath(allSections.map(p => p.reference), true); context.stroke(); context.setLineDash([]);
  if (advancedView && $('segments').checked) for (const segment of geometry.segments) {
    const p = segment.sections[0]; pointPath([p.inner, p.outer]); context.strokeStyle = '#e4d8ba88'; context.lineWidth = .8; context.stroke();
    const label = worldToScreen(p.outer); context.font = '10px Segoe UI'; context.fillStyle = '#d3e1df'; context.fillText(`S${segment.index} ${segment.type}`, label.x + 6, label.y - 6);
  }
  if(replay.standingData)for(const [index,label] of [[0,'Start / meta'],[1,'Łuk1 początek'],[4,'Łuk1 koniec'],[5,'Łuk2 początek'],[8,'Łuk2 koniec']]){
    const section=geometry.segments[index].sections[0];pointPath([section.inner,section.outer]);
    context.strokeStyle=index===0?'#ffd27b':'#a5d4eb';context.lineWidth=2;context.stroke();
    const p=worldToScreen(section.outer);context.fillStyle=context.strokeStyle;context.font='11px Segoe UI';context.fillText(label,p.x+4,p.y-8);
  }
  const origin = geometry.segments[0].sections[0];
  // Gate markings follow the exported physical start cross-section; no authored oval.
  const tangent={x:-origin.outward.y,y:origin.outward.x};
  for(let gate=0;gate<4;gate++){
    const at=f=>({x:origin.inner.x+(origin.outer.x-origin.inner.x)*f,y:origin.inner.y+(origin.outer.y-origin.inner.y)*f});
    const a=at(gate/4),b=at((gate+1)/4),back=p=>({x:p.x-3*tangent.x,y:p.y-3*tangent.y});
    pointPath([a,b,back(b),back(a)],true);context.fillStyle=colors[gate]+'28';context.fill();context.strokeStyle='#e1d7bf99';context.lineWidth=.8;context.stroke();
    const label=worldToScreen(back(at((gate+.5)/4)));context.font='10px Segoe UI';context.fillStyle='#ede6d8';context.fillText('ABCD'[gate],label.x-4,label.y-3);
  }
  pointPath([origin.inner, origin.outer]); context.strokeStyle = '#f1e7d1'; context.lineWidth = 2; context.stroke();
  const start = worldToScreen(origin.outer); context.font = '10px Segoe UI'; context.fillStyle = '#b7c9d1'; context.fillText('Start / meta', start.x + 5, start.y + 15);
  const states = [...replay.tracks.keys()].map(id => displayState(id));
  if (replay.h25) {
    canvas.dataset.riders = JSON.stringify(states.map(({id,x,y,heading,sampleTime}) => ({id,x,y,heading,sampleTime})));
    canvas.dataset.camera = JSON.stringify({x: camera.x, y: camera.y, follow: $('follow').checked ? selected : null});
    const incident = replay.result.Contacts.find(c => c.Kind === 'begin' && c.XY
      && time >= c.TimeSeconds - .0005 && time < c.TimeSeconds + 1.3);
    canvas.dataset.contact = incident ? 'R' + incident.RiderA + '-R' + incident.RiderB : '';
    if (incident) {
      const point = worldToScreen({x: incident.XY.X, y: incident.XY.Y});
      context.beginPath(); context.arc(point.x, point.y, 15, 0, Math.PI * 2);
      context.lineWidth = 2; context.strokeStyle = '#ff9b89'; context.stroke();
    }
  }
  const rendered=states.find(state=>state.id===selected);
  canvas.dataset.renderX=String(rendered.x);canvas.dataset.renderY=String(rendered.y);canvas.dataset.renderHeading=String(rendered.heading);
  if(replay.throttleData){
    const markers=throttleMarkers(replay.throttleData),left=Math.max(12,width-230);
    context.fillStyle='#10202de8';context.fillRect(left-6,8,224,markers.length*16+10);
    for(const [index,marker] of markers.entries()){
      const p=worldToScreen(marker);context.beginPath();context.arc(p.x,p.y,4,0,Math.PI*2);context.fillStyle=marker.color;context.fill();
      context.font='10px Segoe UI';context.fillText(`${index+1}`,p.x+5,p.y-5-index*9);
      const label=marker.label.replace('Geometryczny koniec','Koniec').replace('Rzeczywista aktywacja','Aktywacja').replace('Termin reakcji −0,24s (warunek czasowy)','Termin reakcji (czas)');
      context.fillText(`${index+1}. ${label} · ${marker.time.toFixed(2)}s`,left,23+index*16);
    }
    for(const comparison of replay.throttleData.comparisonRuns.filter(r=>r.policy!==replay.robustPolicy)){
      const sample=comparison.run.Replay.Samples.filter(s=>s.Input.Time<=time+1e-9).at(-1);
      if(!sample)continue;const native=sample.Input.Rider.Position,p=worldToScreen({x:native.X,y:native.Y});
      context.beginPath();context.arc(p.x,p.y,5,0,Math.PI*2);context.strokeStyle=comparison.color;context.lineWidth=2;context.stroke();
      context.fillStyle=comparison.color;context.fillText(comparison.policy,p.x+8,p.y+12);
    }
  }
  if(replay.causalData){
    const markers=causalMarkers(replay.causalData),left=Math.max(12,width-254);
    context.fillStyle='#10202de8';context.fillRect(left-6,8,248,markers.length*15+10);
    for(const [index,marker] of markers.entries()){
      const p=worldToScreen(marker);context.beginPath();context.arc(p.x,p.y,4,0,Math.PI*2);context.fillStyle=marker.color;context.fill();
      context.font='9px Segoe UI';context.fillText(`${index+1}`,p.x+4,p.y-6-index*8);
      context.fillText(`${index+1}. ${marker.shortLabel} · ${marker.time.toFixed(2)}s`,left,22+index*15);
    }
    for(const comparison of replay.causalData.comparisonRuns.filter(r=>r.policy!==replay.robustPolicy)){
      const sample=comparison.run.Replay.Samples.filter(s=>s.Input.Time<=time+1e-9).at(-1);if(!sample)continue;
      const native=sample.Input.Rider.Position,p=worldToScreen({x:native.X,y:native.Y});context.beginPath();context.arc(p.x,p.y,5,0,Math.PI*2);
      context.strokeStyle=comparison.color;context.lineWidth=2;context.stroke();context.fillStyle=comparison.color;context.fillText(comparison.policy,p.x+8,p.y+12);
    }
  }
  for (const state of states) {
    if ($('trails').checked) {
      const points = replay.tracks.get(state.id).filter(p => p.t <= time);
      if (state.interpolated) points.push(state);
      pointPath(points); context.strokeStyle = color(state.id); context.globalAlpha = state.id === selected ? .65 : .4;
      context.lineWidth = 1.6; context.stroke(); context.globalAlpha = 1;
    }
    if (advancedView && $('targets').checked) {
      const section = crossSection(geometry, state.reference);
      const at = offset => ({ x: section.point.x + section.normal.x * offset, y: section.point.y + section.normal.y * offset });
      pointPath([at(state.offset), at(state.target)]); context.strokeStyle = '#ffe095'; context.lineWidth = 1; context.setLineDash([3, 3]); context.stroke(); context.setLineDash([]);
      const target = worldToScreen(at(state.target)); context.strokeStyle = '#ffe095'; context.lineWidth = 1.2; context.beginPath();
      context.moveTo(target.x - 5, target.y); context.lineTo(target.x + 5, target.y); context.moveTo(target.x, target.y - 5); context.lineTo(target.x, target.y + 5); context.stroke();
      if (state.id === selected) { context.font = '10px Segoe UI'; context.fillStyle = '#ffe095'; context.fillText(`cel ${format(state.target, 2)} m`, target.x + 9, target.y + 14); }
    }
  }
  if (replay.h25) drawH25Comparison(rendered);
  for (const state of states) {
    const pos = worldToScreen(state); const ghost = state.stale || (state.finished && !replay.h25);
    if (state.id === selected) { context.beginPath(); context.arc(pos.x, pos.y, Math.max(10, camera.scale * 1.3), 0, Math.PI * 2); context.strokeStyle = color(state.id); context.lineWidth = 1; context.setLineDash(ghost ? [3, 3] : []); context.stroke(); context.setLineDash([]); }
    context.globalAlpha = ghost ? .5 : 1;
    for (const part of worldCapsules(geometry, state)) {
      pointPath([part.start, part.end]); context.lineCap = 'round'; context.strokeStyle = part.component === 'Handlebar' ? '#fff4db' : color(state.id);
      context.lineWidth = part.radiusMeters * 2 * camera.scale; context.stroke();
    }
    // A minimum-size UI symbol preserves recorded heading and XY; it is not a footprint.
    if(geometry.dimensions.overallMechanicalLengthMeters*camera.scale<16){
      context.save();context.translate(pos.x,pos.y);context.rotate(-state.heading);
      context.fillStyle=color(state.id);context.strokeStyle='#09141e';context.lineWidth=1.2;
      context.beginPath();context.moveTo(9,0);context.lineTo(2,-3.5);context.lineTo(-7,-3);context.lineTo(-7,3);context.lineTo(2,3.5);context.closePath();context.fill();context.stroke();
      context.fillStyle='#fff4db';context.fillRect(4,-3,2,6);context.fillStyle='#14222a';context.fillRect(-9,-2,3,4);context.restore();
    }
    context.lineCap = 'butt'; context.globalAlpha = 1;
    if (state.id === selected && ((advancedView && $('vectors').checked) ||
        (replay.exitStudy && $('exit-diagnostics').open && $('exit-vectors').checked))) drawMotion(state);
    const slot=[...replay.tracks.keys()].indexOf(state.id);
    const labelX = pos.x + (states.length===1||slot%2?11:-25), labelY = pos.y + (slot<2?-12:18);
    context.font = '600 12px Segoe UI'; context.lineWidth = 4; context.strokeStyle = '#10202d'; context.strokeText(`R${state.id}`, labelX, labelY);
    context.fillStyle = color(state.id); context.fillText(`R${state.id}`, labelX, labelY);
  }
  drawSolo199(rendered);
  drawSolo200(rendered);
  if ((replay.exitStudy?.native200 || replay.exitStudy?.native199) && $('exit-diagnostics').open) {
    const markers=(replay.exitStudy.native200 ?? replay.exitStudy.native199).markers.filter(p=>p.t<=time && p.t>=time-5);
    for (const m of markers) {
      const p=worldToScreen(m);
      const palette={plan:'#fae388',onset:'#d9e7fd',slip:'#f2a49d',slipEnd:'#8be7d3',entry:'#aecafa',exit:'#ffc97e'};
      context.fillStyle=palette[m.kind]??'#eee';
      context.beginPath(); context.arc(p.x,p.y,m.kind==='plan'?4:3,0,Math.PI*2); context.fill();
    }
  }
  if (replay.exitStudy?.native198 && $('exit-diagnostics').open) {
    const data = replay.exitStudy.native198.tests[rendered.id-1];
    const previous = data?.markers.filter(d=>d.t<=time).slice(-8) ?? [];
    for (const marker of previous) {
      const p=worldToScreen(marker);
      context.fillStyle='#fae388';
      context.beginPath(); context.arc(p.x,p.y,3.4,0,Math.PI*2); context.fill();
    }
    const next=data?.forecast.findLast(d=>d.t<=time+1e-6);
    if(next && next.at>=time) {
      pointPath([rendered,next]);
      context.strokeStyle='#a8daf8';
      context.lineWidth=2;
      context.setLineDash([2,3]); context.stroke(); context.setLineDash([]);
    }
    canvas.dataset.planner198=JSON.stringify({count:previous.length,version:next?.version??0});
  } else delete canvas.dataset.planner198;
  if (replay.exitStudy && $('exit-diagnostics').open && $('exit-target').checked) {
    const decision = exitDecision(rendered);
    const goal = exitTarget(decision);
    if (goal) {
      pointPath([rendered, goal]);
      context.strokeStyle = '#ffe095';
      context.lineWidth = 1.5;
      context.setLineDash([4, 4]);
      context.stroke();
      context.setLineDash([]);
      const point = worldToScreen(goal);
      context.beginPath();
      context.arc(point.x, point.y, 4, 0, Math.PI * 2);
      context.stroke();
      context.fillStyle = '#ffe095';
      context.font = '11px Segoe UI';
      context.fillText('κ ' + format(goal.curvature, 4) + '/m', point.x+7, point.y-5);
    }
  }
  drawOverview(allSections, states);
  const rawMeters = 75 / camera.scale, unit = 10 ** Math.floor(Math.log10(rawMeters));
  const meters = unit * (rawMeters / unit >= 5 ? 5 : rawMeters / unit >= 2 ? 2 : 1), pixels = meters * camera.scale;
  context.strokeStyle = '#a4bac7'; context.lineWidth = 1; context.beginPath(); context.moveTo(width - 25 - pixels, height - 22); context.lineTo(width - 25, height - 22); context.stroke();
  context.font = '10px Segoe UI'; context.fillStyle = '#a4bac7'; context.fillText(`${meters} m`, width - 25 - pixels, height - 29);
  if (replay.planAt && (camera.scale >= fitScale * 2 || clipRecorder)) {
    const state = displayState(), beta = motionVectors(state).beta;
    context.fillStyle = '#10202ded'; context.fillRect(12, 70, Math.min(width-24, 465), 49);
    context.fillStyle = '#e6edf5'; context.font = '600 13px Segoe UI';
    context.fillText(`R1 · ${format(time,2)} s · ${format(state.speed*3.6,1)} km/h · Drive ${format(state.drive*100,1)}%`, 22, 90);
    context.font = '12px Segoe UI'; context.fillStyle = '#efc8ef';
    context.fillText(`β ${format(beta*180/Math.PI,1)}° · kierownica ${format(state.steering*180/Math.PI,2)}° · plan ${replay.planAt(time)} · tempo ${$('rate').value}×`,22,109);
  }
}
function recordClip() {
  if (clipRecorder) { clipRecorder.stop(); return; }
  if (!replay || !canvas.captureStream || typeof MediaRecorder === 'undefined') return;
  const stream = canvas.captureStream(30), chunks = [], from = time;
  const recorder = new MediaRecorder(stream); clipRecorder = recorder;
  $('record-clip').textContent = '■ Zakończ klip';
  recorder.ondataavailable = event => { if (event.data.size) chunks.push(event.data); };
  const timer = setTimeout(() => { if (recorder.state === 'recording') recorder.stop(); }, 12000);
  recorder.onstop = () => {
    clearTimeout(timer); stream.getTracks().forEach(track => track.stop()); clipRecorder = null;
    setPlaying(false); $('record-clip').textContent = 'Zapisz klip (12 s)';
    const url = URL.createObjectURL(new Blob(chunks, {type:recorder.mimeType}));
    const link = document.createElement('a'); link.href = url; link.download = `box2d-${$('scenario').value}-${format(from,2)}-${format(time,2)}.webm`;
    link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
  };
  recorder.start(); setPlaying(true);
}
function outcomeLabel(outcome) {
  if (outcome === 'completed') return 'Bieg ukończony';
  if (outcome === 'solver contact: crash threshold') return 'Bieg przerwany po upadku';
  if (outcome?.includes('clearance')) return 'Bieg przerwany po naruszeniu granicy toru';
  if (outcome === 'experiment timeout') return 'Przekroczono limit czasu biegu';
  return outcome ?? 'Bieg nieukończony';
}

function followRider(id) {
  chooseRider(id);
  $('follow').checked = true;
  camera.scale = Math.max(camera.scale, fitScale * 3);
  dirty = true;
}

async function loadScenario(key,keepTime=null) {
  if (clipRecorder) clipRecorder.stop();
  const generation = ++loadGeneration;
  setPlaying(false);
  replay = null;
  $('load-state').hidden = false;
  $('load-state').classList.remove('error');
  document.querySelector('.brand p').textContent=[...$('scenario').options].find(option=>option.value===key)?.textContent??'Zapis historyczny';
  zigzagPanel=null;$('zigzag-analysis').hidden=true;
  $('free-racing-analysis').hidden=true;
  $('free-learning-analysis').hidden=true;
  $('robust-learning-analysis').hidden=true;
  $('targets').disabled=['free-racing','free-learning','robust-learning','extended-learning','full-throttle-learning','rider-decision','track-awareness','multi-action','speed-throttle','causal-yaw','constraints143','standing144'].includes(key);
  $('outer-limit').disabled=['free-racing','free-learning','robust-learning','extended-learning','full-throttle-learning','rider-decision','track-awareness','multi-action','speed-throttle','causal-yaw','constraints143','standing144'].includes(key);
  $('load-state').textContent = 'Wczytywanie zaakceptowanych stanów…';
  $('race-leaderboard').hidden = true;
  $('exit-diagnostics').hidden = true;
  $('h25-comparison').hidden = true;
  $('solo199-comparison').hidden=true;
  $('solo200-comparison').hidden=true;
  solo200=[];
  delete canvas.dataset.solo200;
  solo199=[];
  delete canvas.dataset.solo199;
  activeH25Key = null; h25Compare = null; compareGeneration++;
  delete canvas.dataset.comparison;
  $('follow-rider').hidden = true;
  $('lean-row').hidden = true;
  delete canvas.dataset.riders;
  delete canvas.dataset.contact;
  document.body.classList.remove('h25-view');
  try {
    if (isH25(key)) {
      const data = await loadH25(key);
      if (generation !== loadGeneration) return false;
      geometry = data.geometry; replay = data.replay;
      const ids = [...replay.tracks.keys()];
      selected = ids[0]; comparisonTrails = []; widthGeometries = [];
      for (const id of ['width-controls','width-comparison','comparison-legend','safety-note','record-clip']) $(id).hidden = true;
      document.body.classList.add('h25-view');
      $('race-leaderboard').hidden = false; $('follow-rider').hidden = false; $('lean-row').hidden = false;
      $('follow-rider').replaceChildren(new Option('Wybierz zawodnika', ''), ...ids.map(id => new Option('R' + id, id)));
      $('recording-mode').hidden = false;
      $('recording-mode').textContent = replay.exitStudy?.name ?? (ids.length + ' motocykle · H25 / FAST50 · 3 okrążenia');
      const test = replay.exitStudy?.native200 ?? replay.exitStudy?.native199 ?? replay.exitStudy?.native198 ?? replay.exitStudy?.native197 ?? replay.exitStudy?.native196;
      $('exit-target').closest('label').hidden = Boolean(test);
      $('exit-jump').previousSibling.textContent = test ? 'Punkt ' : 'Wyjście ';
      $('exit-jump').setAttribute('aria-label', test ? 'Wybierz punkt pomiaru' : 'Przejdź do prostowania');
      if (test) {
        const recordedPeak=Math.max(...[...replay.tracks.values()].flatMap(track=>track.map(row=>row.speed*3.6)));
        $('recording-mode').textContent += ' · ' + test.verdict + ' · Vmax ' + format(test.vmax,3).replace('.', ',') +
          ' km/h przed łukami · całe nagranie '+format(recordedPeak,3)+' km/h · min. swept ' + format(test.swept,3).replace('.', ',') + ' m';
        if (replay.exitStudy.native199 || replay.exitStudy.native200) {
          $('recording-mode').textContent += ' · '+format(test.finish,3)+' s · korytarz '+format(test.wide*100,1)+'%';
        }
        if (test.primary) {
          $('recording-mode').textContent += ' · odcinek ' + format(test.section,3) + ' s (#197: ' +
            format(test.compareSection,3) + ' s) · wyjście ' + format(test.exitSpeed,1) + ' km/h';
        }
        if (test.complete) {
          $('recording-mode').textContent += ' · ' + format(test.duration,3) + ' s (#196: ' +
            format(test.baselineDuration,3) + ' s) · wyjście ' + format(test.exitSpeed,1) +
            ' km/h, offset ' + format(test.exitOffset,2) + ' m';
        }
        $('exit-diagnostics').open = true;
      }
      $('exit-diagnostics').hidden = !replay.exitStudy;
      $('exit-diagnostics').querySelector('summary').textContent = test ? 'Telemetria ' + comparedController(replay) :
        comparedController(replay) === '#194' ? 'Diagnostyka wyjścia · #194' : 'Diagnostyka wyjścia';
      $('h25-comparison').hidden = false;
      activeH25Key = key;
      updateComparisonChoices(raceEntries);
      $('h25-compare').value = '';
      $('h25-compare-info').textContent = 'Wybierz przejazd z archiwum.';
      document.querySelector('.map-title span').textContent = 'MOTOARENA · TORUŃ';
      document.querySelector('.map-title small').textContent = 'Geometria progressive-b · metry';
      canvas.setAttribute('aria-label', 'Stadion Motoarena, liczba motocykli: ' + ids.length);
      $('smooth').checked = true; $('targets').disabled = true;
      $('outer-limit').checked = false; $('compare-lines').checked = false;
      $('sampling-note').textContent = 'Natywne pozycje Box2D';
      $('all-events').checked = true;
      $('riders').replaceChildren(...ids.map(id => {
        const button = document.createElement('button');
        button.textContent = 'R' + id; button.dataset.id = String(id);
        button.style.setProperty('--rider', color(id));
        button.addEventListener('click', () => chooseRider(id));
        return button;
      }));
      $('source-count').textContent = ids.length === 1 ? '1 motocykl' : ids.length + ' motocykle';
      $('scrub').max = String(replay.duration); $('scrub').step = '0.001';
      const requestedTime = new URL(location.href).searchParams.get('t');
      setTime(keepTime ?? (requestedTime == null ? test?.start ?? 0 : Number(requestedTime) || 0));
      fit(); chooseRider(selected); $('load-state').hidden = true;
      if (replay.exitStudy?.native200) {
        const peerKeys=['199-swobodna','199-bazowy-b','199-solo','200-solo'];
        const sources=await Promise.all(peerKeys.map(key=>loadH25('h25-'+key)));
        if(generation!==loadGeneration)return false;
        if(sources.some(source=>source.geometry.frameId!==geometry.frameId ||
          source.geometry.lapMeters!==geometry.lapMeters))throw new Error('Niezgodna geometria porównania #200.');
        solo200=sources.map(source=>source.replay);
        $('h25-comparison').hidden=true;
        $('solo200-comparison').hidden=false; $('solo200-comparison').open=true;
        dirty=true;
      } else if (replay.exitStudy?.native199) {
        const peers=await Promise.all(['199-swobodna','199-solo','198-solo','199-bazowy-b'].map(id=>loadH25('h25-'+id)));
        if(generation!==loadGeneration)return false;
        if(peers.some(source=>source.geometry.frameId!==geometry.frameId ||
          source.geometry.lapMeters!==geometry.lapMeters))throw new Error('Niezgodna geometria pomiaru #199.');
        solo199=peers.map(source=>source.replay);
        $('h25-comparison').hidden=true;
        $('solo199-comparison').hidden=false; $('solo199-comparison').open=true;
        dirty=true;
      } else if (test?.primary && raceEntries.some(entry => entry.id === '197-autonomiczna')) {
        $('h25-comparison').open = true;
        $('h25-compare').value = 'h25-197-autonomiczna';
        await selectComparison('h25-197-autonomiczna');
      } else if (test?.complete && raceEntries.some(entry => entry.id === '196-szybka-prosta')) {
        $('h25-comparison').open = true;
        $('h25-compare').value = 'h25-196-szybka-prosta';
        await selectComparison('h25-196-szybka-prosta');
      }
      return true;
    }
    if(key==='study172'){
      const data=await loadStudy172();if(generation!==loadGeneration)return;
      geometry=data.geometry;replay=data.replay;selected=[...replay.tracks.keys()][0];comparisonTrails=[];widthGeometries=[];
      for(const id of ['width-controls','width-comparison','comparison-legend','safety-note','record-clip'])$(id).hidden=true;
      $('recording-mode').hidden=false;$('recording-mode').textContent='Solo — #172 · S3-K4 · trzy pełne okrążenia';
      document.querySelector('.brand p').textContent='Aktualny zapis autonomicznego sterownika · jeden motocykl';
      document.querySelector('.map-title span').textContent='PROGRESSIVE-B · GEOMETRIA EKSPERYMENTALNA';
      document.querySelector('.map-title small').textContent='Geometria symulatora · nie jest dokładnym odwzorowaniem Motoareny';
      canvas.setAttribute('aria-label','Stadion progressive-b z rzeczywistej geometrii #172, jeden motocykl, trzy okrążenia');
      $('smooth').checked=true;$('targets').checked=false;$('vectors').checked=false;$('outer-limit').checked=false;$('compare-lines').checked=false;$('rate').value='1';
      $('targets').disabled=true;$('outer-limit').disabled=true;
      $('sampling-note').textContent=replay.study172.samples+' natywnych stanów · interpolacja wyłącznie obrazu';
      $('riders').replaceChildren();for(const id of replay.tracks.keys()){
        const button=document.createElement('button');button.textContent='R'+id;button.dataset.id=String(id);button.style.setProperty('--rider',color(id));button.addEventListener('click',()=>chooseRider(id));$('riders').append(button);
      }
      $('source-count').textContent='1 motocykl · Solo — #172';$('scrub').max=String(replay.duration);$('scrub').step='0.001';
      setTime(keepTime??Number(new URL(location.href).searchParams.get('t')??0));chooseRider(selected);fit();$('load-state').hidden=true;return;
    }
    if(key==='viable154'||key==='safety153'||key==='efficient152'||key==='contingency151'||key==='robust150'||key==='standing144'||key==='corner145'||key==='planning146'||key==='efficient147'||key==='speed148'||key==='tyre149'){
      const data=await (key==='viable154'?loadViable154:key==='safety153'?loadSafety153:key==='efficient152'?loadEfficient152:key==='contingency151'?loadContingency151:key==='robust150'?loadRobust150:key==='tyre149'?loadTyre149:key==='speed148'?loadSpeed148:key==='efficient147'?loadEfficient147:key==='planning146'?loadPlanning146:key==='corner145'?loadCorner145:loadStanding144)(fetch,new URL(location.href).searchParams.get('run')??(key==='corner145'?'K1':null));if(generation!==loadGeneration)return;
      geometry=data.geometry;replay=data.replay;selected=[...replay.tracks.keys()][0];comparisonTrails=[];widthGeometries=[];
      for(const id of ['width-controls','width-comparison','comparison-legend'])$(id).hidden=true;
      $('recording-mode').hidden=false;$('recording-mode').textContent=(key==='viable154'?'#154 · BEZPIECZNE KONTYNUACJE · ':key==='safety153'?'#153 · LEGACY / OBRYS 0,5 m · ':key==='efficient152'?'#152 · WYDAJNY PLANNER WARUNKOWY · ':key==='contingency151'?'#151 · PLANOWANIE WARUNKOWE · ':key==='robust150'?'#150 · ODPORNOŚĆ · PARTIAL · ':key==='tyre149'?'#149 · OPONY I GRANICA PRĘDKOŚCI · ':key==='speed148'?'#148 · DOKŁADNA OPTYMALIZACJA · ':key==='efficient147'?'#147 · WYDAJNY PLANNER · ':key==='planning146'?'#146 · PLANOWANIE · ':key==='corner145'?'#145 · BADANIE STRAT · ':'#144 · START0 →3 OKRĄŻENIA · ')+data.run.Riders.length+' motocykli · '+data.run.Outcome;
      document.querySelector('.brand p').textContent=data.index.Title;
      $('safety-note').hidden=false;$('safety-note').className='safety-note'+(data.run.Outcome==='completed'?'':' unsafe');
      $('safety-note').textContent=data.index.Note;
      document.querySelector('.map-title span').textContent='TOR EKSPERYMENTALNY · PROGRESSIVE-B';document.querySelector('.map-title small').textContent='Start35m przed łukiem · ciągłe native · bez resetów';
      $('smooth').checked=false;$('targets').checked=false;$('outer-limit').checked=false;$('rate').value='1';
      $('record-clip').hidden=typeof MediaRecorder==='undefined'||!canvas.captureStream;
      $('sampling-note').textContent=data.run.Riders.reduce((n,r)=>n+r.Samples.length,0)+' natywnych stanów · bez ekstrapolacji';
      (key==='viable154'?setupViable154:key==='safety153'?setupSafety153:key==='efficient152'?setupEfficient152:key==='contingency151'?setupContingency151:key==='robust150'?setupRobust150:key==='tyre149'?setupTyre149:key==='speed148'?setupSpeed148:key==='efficient147'?setupEfficient147:key==='planning146'?setupPlanning146:key==='corner145'?setupCorner145:setupStanding144)($('robust-learning-analysis'),data,run=>{const url=new URL(location.href);url.searchParams.set('run',run);url.searchParams.delete('t');history.replaceState(null,'',url);loadScenario(key,0);},
        (t,id)=>{if(id!=null)chooseRider(id);setPlaying(false);setTime(t);},lines=>{comparisonTrails=lines;$('compare-lines').checked=true;dirty=true;});
      $('riders').replaceChildren();for(const id of replay.tracks.keys()){
        const button=document.createElement('button');button.textContent='R'+id;button.dataset.id=String(id);button.addEventListener('click',()=>chooseRider(id));$('riders').append(button);
      }
      $('source-count').textContent=data.run.Riders.length+' motocykli · jeden świat';$('scrub').max=String(replay.duration);$('scrub').step=['viable154','safety153'].includes(key)?'any':'0.001';
      setTime(keepTime??Number(new URL(location.href).searchParams.get('t')??0));chooseRider(selected);fit();
      $('robust-learning-analysis').hidden=false;$('load-state').hidden=true;return;
    }
    if(['robust-learning','extended-learning','full-throttle-learning','rider-decision','track-awareness','multi-action','speed-throttle','causal-yaw','constraints143'].includes(key)){
      const root=key==='constraints143'?'../../docs/experiments/racing-constraints-speed/':key==='causal-yaw'?'../../docs/experiments/causal-yaw-throttle/':key==='speed-throttle'?'../../docs/experiments/speed-throttle-root-cause/':key==='multi-action'?'../../docs/experiments/multi-action-planning/':key==='track-awareness'?'../../docs/experiments/track-awareness-anticipatory-riding/':key==='rider-decision'?'../../docs/experiments/rider-decision-optimization/':key==='full-throttle-learning'?'../../docs/experiments/full-throttle-learning/':key==='extended-learning'?'../../docs/experiments/autonomous-speedway-extended-learning/':undefined;
      const data=await (key==='constraints143'?loadConstraints143:loadRobust)(fetch,new URL(location.href).searchParams.get('run'),root,new URL(location.href).searchParams.get('group'));if(generation!==loadGeneration)return;
      if(key==='causal-yaw'){data.causalPlans=JSON.parse(await readGzip(await fetch(root+data.group.PlansFile)));if(generation!==loadGeneration)return;}
      geometry=data.geometry;replay=data.replay;if(key==='multi-action')replay.planningData=data;if(key==='speed-throttle')replay.throttleData=data;selected=[...replay.tracks.keys()][0];comparisonTrails=data.comparisons;widthGeometries=[];
      if(key==='causal-yaw'){replay.causalData=data;
        if(data.document.Replay.Name.startsWith('extended-'))replay.episodeScope='Dwa łuki + pełna następna prosta · osobny pomiar';}
      for(const id of ['width-controls','width-comparison'])$(id).hidden=true;
      $('comparison-legend').hidden=false;$('comparison-legend').replaceChildren();
      for(const c of data.comparisons){const span=document.createElement('span');span.style.color=c.color;span.textContent=c.name+' · ';$('comparison-legend').append(span);}
      $('recording-mode').hidden=false;$('recording-mode').textContent=`${key==='multi-action'?'MULTI-ACTION PLANNING':'ROBUST LEARNING'} · ${data.index.Grade} · zapisów natywnych: ${data.group.Runs.length} · identyczne warunki`;
      document.querySelector('.brand p').textContent=data.index.Title??'Robust Free Racing Learning · kontrolowane A/B · nowy niezależny test';
      $('safety-note').hidden=false;$('safety-note').className='safety-note'+(data.document.Replay.Metrics.Completed||data.document.Replay.Metrics.Outcome==='probe'?'':' unsafe');
      $('safety-note').textContent=`${data.group.Id}: ${data.document.Replay.Metrics.Outcome}. Potwierdzone przekroczenie i brak certyfikacji są odrębnymi wynikami.`;
      document.querySelector('.map-title span').textContent=['track-awareness','multi-action','speed-throttle','causal-yaw','constraints143'].includes(key)?data.document.GeometryName.toUpperCase():'EGG MILD · PROGRESSIVE B';document.querySelector('.map-title small').textContent='Rzeczywiste ślady · bez idealnej linii';
      $('smooth').checked=false;$('targets').checked=false;$('outer-limit').checked=false;$('rate').value='0.5';
      $('sampling-note').textContent=`${data.document.Replay.Samples.length} natywnych stanów · bez ekstrapolacji`;
      $('record-clip').hidden=typeof MediaRecorder==='undefined'||!canvas.captureStream;
      setupRobust($('robust-learning-analysis'),data,(run,groupId)=>{const at=time;const url=new URL(location.href);url.searchParams.set('run',run);if(groupId)url.searchParams.set('group',groupId);url.searchParams.delete('t');history.replaceState(null,'',url);loadScenario(key,at);},
        (policy,visible)=>{comparisonTrails.find(c=>c.policy===policy).visible=visible;dirty=true;});
      const button=document.createElement('button');button.textContent=`R${selected}`;button.dataset.id=String(selected);button.addEventListener('click',()=>chooseRider(selected));
      $('riders').replaceChildren(button);$('source-count').textContent='1 motocykl';$('scrub').max=String(replay.duration);
      setTime(keepTime??Number(new URL(location.href).searchParams.get('t')??0));chooseRider(selected);fit();
      if(key==='speed-throttle'){const panel=document.createElement('div');panel.id='speed-throttle-panel';$('robust-learning-analysis').prepend(panel);speedThrottlePanel(panel,data,time);}
      if(key==='causal-yaw'){const panel=document.createElement('div');panel.id='causal-yaw-panel';$('robust-learning-analysis').prepend(panel);causalPanel(panel,data,time);$('recording-mode').textContent='#137 CAUSAL YAW · '+data.index.Grade+' · autonomiczne A / '+data.group.Runs.find(r=>r.Policy!=='A')?.Policy;}
      if(key==='constraints143'){const panel=document.createElement('div');panel.id='constraints143-panel';$('robust-learning-analysis').prepend(panel);constraintsPanel(panel,data,time);$('recording-mode').textContent='#143 · PARTIAL · pełne autonomiczne przejazdy';}
      if(key==='multi-action'){const panel=document.createElement('div');panel.id='planning-panel';$('robust-learning-analysis').prepend(panel);planningPanel(panel,data.group.Plans?.find(p=>p.Arm===replay.robustPolicy),data.document,time,data.group);}
      $('robust-learning-analysis').hidden=false;$('load-state').hidden=true;return;
    }
    if(key==='free-learning'){
      const name=new URL(location.href).searchParams.get('run')??'overlay-final';
      const data=await loadLearning(fetch,name);if(generation!==loadGeneration)return;
      geometry=data.geometry;replay=data.replay;selected=[...replay.tracks.keys()][0];comparisonTrails=data.comparisons;widthGeometries=[];
      for(const id of ['width-controls','width-comparison'])$(id).hidden=true;
      $('comparison-legend').hidden=false;$('comparison-legend').replaceChildren();
      for(const c of data.comparisons){const span=document.createElement('span');span.style.color=c.color;span.textContent=c.name+' · ';$('comparison-legend').append(span);}
      $('recording-mode').hidden=false;$('recording-mode').textContent=`FREE RACING LEARNING · ${data.index.Grade} · rzeczywiste przejazdy treningowe i niezależna walidacja`;
      document.querySelector('.brand p').textContent='Free Racing Learning · jeden zawodnik · bez narzuconej linii';
      $('safety-note').hidden=false;$('safety-note').className='safety-note'+(data.run.Metrics.Completed?'':' unsafe');
      $('safety-note').textContent=`${data.run.Name}: ${data.run.Metrics.Outcome}. Nakładki pokazują wspólny stan 9m/s / offset4m; pozostałe próby mogą mieć inne warunki wejściowe.`;
      document.querySelector('.map-title span').textContent='EGG MILD · PROGRESSIVE B';document.querySelector('.map-title small').textContent='Natywne XY · brak idealnej linii';
      $('smooth').checked=false;$('targets').checked=false;$('outer-limit').checked=false;$('rate').value='0.5';
      $('sampling-note').textContent=`${data.run.Samples.length} zaakceptowanych stanów · bez ekstrapolacji`;
      $('record-clip').hidden=typeof MediaRecorder==='undefined'||!canvas.captureStream;
      setupLearning($('free-learning-analysis'),data,run=>{const url=new URL(location.href);url.searchParams.set('run',run);url.searchParams.delete('t');history.replaceState(null,'',url);loadScenario('free-learning',0);});
      const button=document.createElement('button');button.textContent=`R${selected}`;button.dataset.id=String(selected);button.addEventListener('click',()=>chooseRider(selected));
      $('riders').replaceChildren(button);$('source-count').textContent='1 motocykl';$('scrub').max=String(replay.duration);
      setTime(keepTime??Number(new URL(location.href).searchParams.get('t')??0));chooseRider(selected);fit();
      $('free-learning-analysis').hidden=false;$('load-state').hidden=true;return;
    }
    if(key==='free-racing'){
      const data=await loadFreeRacing();if(generation!==loadGeneration)return;
      geometry=data.geometry;replay=data.replay;selected=[...replay.tracks.keys()][0];comparisonTrails=data.comparisons;widthGeometries=[];
      for(const id of ['width-controls','width-comparison'])$(id).hidden=true;
      $('comparison-legend').hidden=false;$('recording-mode').hidden=false;
      $('recording-mode').textContent='FREE RACING · PARTIAL · 1/40 natywnych przejazdów · polityka początkowa · brak gotowej trajektorii';
      document.querySelector('.brand p').textContent='Free Racing · kontrola wstępna · trening nieuruchomiony';
      $('safety-note').hidden=false;$('safety-note').textContent='STOP w 1,91s: obrys przekroczył bandę na prostej. Nie wykonano treningu, walidacji ani finału.';
      $('safety-note').className='safety-note unsafe';
      document.querySelector('.map-title span').textContent='EGG MILD · PROGRESSIVE B';
      document.querySelector('.map-title small').textContent='Ruch natywny · bez gotowej linii';
      $('smooth').checked=false;$('rate').value='0.25';$('targets').checked=false;$('outer-limit').checked=false;
      $('sampling-note').textContent=`${data.document.Samples.length} zapisanych stanów · stop na granicy · bez ekstrapolacji`;
      const button=document.createElement('button');button.textContent=`R${selected}`;button.dataset.id=String(selected);button.addEventListener('click',()=>chooseRider(selected));
      $('riders').replaceChildren(button);$('source-count').textContent='1 motocykl';$('scrub').max=String(replay.duration);
      setTime(keepTime??Number(new URL(location.href).searchParams.get('t')??0));chooseRider(selected);fit();
      $('free-racing-analysis').hidden=false;$('load-state').hidden=true;return;
    }
    const zigzag=key.startsWith('zigzag-');
    const predictive = key.startsWith('predictive-')||zigzag, candidate = key === 'predictive-candidate'||key==='zigzag-candidate';
    $('record-clip').hidden = !(key === 'latest-two-bends' || predictive) || typeof MediaRecorder === 'undefined' || !canvas.captureStream;
    if (key === 'latest-two-bends' || predictive) {
      const dataset = candidate ? {root:'../../docs/experiments/predictive-rider-control/controlled/', name:'candidate'} : null;
      const data = await loadContinuous(fetch, dataset);
      if (candidate) {
        const response = await fetch(dataset.root + 'candidate-steering-events.json');
        if (!response.ok) throw new Error('Brak zapisanych chwil decyzji/aktywacji skrętu.');
        data.replay.steeringEvents = await response.json();
      }
      if (generation !== loadGeneration) return;
      geometry = data.geometry; replay = data.replay; selected = [...replay.tracks.keys()][0];
      comparisonTrails = data.comparisons; widthGeometries = [];
      for (const id of ['width-controls','width-comparison']) $(id).hidden = true;
      $('comparison-legend').hidden = false;
      $('recording-mode').hidden = false;
      $('recording-mode').textContent = candidate ? 'KANDYDAT · PARTIAL · rzeczywisty Box2D · 20,27 s / 2031 kroków · ten sam plan co baseline, bezpiecznie, ale wolniej i z większym β.' : '#123 / #124 · BASELINE · rzeczywisty Box2D · jeden zawodnik · dwa łuki · 19,98 s / 2002 kroki.';
      document.querySelector('.brand p').textContent = predictive ? 'Sterowanie predykcyjne · porównanie baseline / kandydat · wynik PARTIAL' : 'Rzeczywista jazda Box2D · przejazd #123 · audyt #124';
      $('safety-note').hidden = false; $('safety-note').className = 'safety-note';
      $('safety-note').textContent = `Oba łuki PASS · konserwatywny prześwit kreda ${format(data.document.ConservativeInner, 3)} m / banda ${format(data.document.ConservativeOuter, 3)} m · duży błąd linii w łuku 1 pozostaje w zapisie.`;
      $('safety-note').title = candidate ? 'Bezpieczeństwo i poprawne przewidywanie nie oznaczają poprawy czasu. Sterownik pozostaje domyślnie OFF.' : 'Wyniki oryginalnego #123. PASS nie potwierdza realistycznie szybkiej jazdy.';
      document.querySelector('.map-title span').textContent = 'EGG MILD · PROGRESSIVE B';
      document.querySelector('.map-title small').textContent = 'Cały oryginalny tor · motocykl w skali · plan ≠ ruch';
      canvas.setAttribute('aria-label', `Egg Mild: rzeczywiste pozycje i heading Box2D ${candidate ? 'kandydata' : 'baseline #123'}, jeden motocykl`);
      $('smooth').checked = false; $('rate').value = '0.25'; $('targets').checked = false;
      $('sampling-note').textContent = `${candidate ? 2031 : 2002} interwałów + zapisany stan końcowy · max Δt 10 ms · wygładzanie OFF`;
      const button = document.createElement('button'); button.textContent = `R${selected}`; button.dataset.id = String(selected);
      button.style.setProperty('--rider', color(selected)); button.addEventListener('click', () => chooseRider(selected));
      $('riders').replaceChildren(button); $('source-count').textContent = '1 motocykl';
      $('scrub').max = String(replay.duration); setTime(0); chooseRider(selected); fit(); $('load-state').hidden = true;
      jumpBend(0);
      if(zigzag){
        const audit=await(zigzagDataPromise??=loadZigzag());if(generation!==loadGeneration)return;
        zigzagPanel=createZigzagPanel($('zigzag-analysis'),audit,{selected:candidate?'candidate':'baseline',windowId:zigzagWindow,
          onSeek:t=>{setPlaying(false);setTime(t);},onWindow:id=>{zigzagWindow=id;},
          onCompare:t=>{const other=candidate?'zigzag-baseline':'zigzag-candidate';$('scenario').value=other;
            const url=new URL(location.href);url.searchParams.set('case',other);url.searchParams.set('t',String(t));history.replaceState(null,'',url);loadScenario(other,t);}});
        const requestedTime=Number(new URL(location.href).searchParams.get('t')??5.44);
        setTime(keepTime??(Number.isFinite(requestedTime)?requestedTime:5.44));closeup();
        $('recording-mode').textContent=`AUDYT #127 · NATIVE ${candidate?'#126 kandydat':'#124 baseline'} · brak nowego przejazdu · zsynchronizowane wykresy poniżej.`;
        document.querySelector('.brand p').textContent='Audyt zygzaka · rzeczywisty ruch i telemetria · PARTIAL';
      }
      return;
    }
    const base = sources[key];
    const recording = currentRecordings[key];
    const inverse = key.startsWith('inverse-');
    const crossover = key.startsWith('crossover-');
    const authored = inverse || crossover || !!recording;
    const progressive = key.startsWith('width-');
    const widthA = key.startsWith('width-a-');
    const local = key.startsWith('local-') || progressive;
    const line = recording ? recording.name : crossover ? key.slice(10) : inverse ? key.slice(8) : progressive ? (widthA ? 'egg-mild-' + key.slice(8) : 'progressive-' + key.slice(6)) : local ? key.slice(6) : key.startsWith('shape-') ? key.slice(6) : key.startsWith('wheel-') ? key.slice(6) : key.startsWith('line-') ? key.slice(5) : null;
    const selectedRoot = recording ? recording.root : crossover ? crossoverRoot : inverse ? inverseRoot : progressive && !widthA ? widthRoot : local ? localRoot : key.startsWith('shape-') ? shapeRoot : key.startsWith('wheel-') ? wheelRoot : lineRoot;
    const [csvResponse, jsonResponse, nativeResponse] = await Promise.all(line
      ? [fetch(`${selectedRoot}${line}-accepted.csv`), fetch(`${selectedRoot}${line}.json`), recording ? fetch(`${selectedRoot}${line}-tracking.csv.gz`) : null]
      : [fetch(`${dataRoot}${base}-accepted.csv`), fetch(`${dataRoot}${base}-ON.json`)]);
    if (!csvResponse.ok || !jsonResponse.ok) throw new Error('Nie znaleziono zapisów przejazdu. Uruchom serwer z katalogu repozytorium.');
    const [csv, rawJson, nativeCsv] = await Promise.all([csvResponse.text(), jsonResponse.json(), recording ? readGzip(nativeResponse) : null]);
    const json = authored ? authoredReplayDocument(rawJson, recording ? 8 : crossover ? (key.endsWith('-stress') ? 1 : 8) : null) : rawJson;
    let nextGeometry = nativeGeometry;
    if (authored) {
      const response = await fetch(widthRoot + 'progressive-b-geometry.json');
      if (!response.ok) throw new Error('Brak zamrożonej geometrii progressive-b.');
      nextGeometry = await response.json();
    }
    if (key.startsWith('shape-') || local) {
      if (!/^[a-z-]+-geometry\.json$/.test(json.GeometryFile)) throw new Error('Brak geometrii tego przejazdu.');
      const response = await fetch(`${selectedRoot}${json.GeometryFile}`);
      if (!response.ok) throw new Error('Nie znaleziono geometrii tego przejazdu.');
      nextGeometry = await response.json();
    }
    const comparisons = recording ? await (async () => {
      const response = await fetch(crossoverRoot + 'viewer-paths.json'); if (!response.ok) throw new Error('Brak zapisanej linii C3.');
      return (await response.json()).filter(p => p.name === (recording.wide ? 'crossover-wide-straight' : 'crossover-inner'))
        .map(p => ({ name: 'Plan C3 · ' + p.name, color: '#badd98', planned: true, points: p.points }));
    })() : crossover ? await (async () => {
      const response = await fetch(crossoverRoot + 'viewer-paths.json'); if (!response.ok) throw new Error('Brak planowanych linii #106.');
      return (await response.json()).map((p, i) => ({ name: p.name + ' (plan C3)', color: ['#9be6a8', '#82bfff'][i], rejected: false, points: p.points }));
    })() : inverse ? await (async () => {
      const response = await fetch(inverseRoot + 'viewer-paths.json'); if (!response.ok) throw new Error('Brak planowanych linii #105.');
      const plans = await response.json(); const early = key.includes('-early-');
      return plans.filter(p => p.name === 'matched-inner-entry' || p.name.endsWith(early ? '-early' : '-entry'))
        .map((p, i) => ({ name: p.name + ' (plan C2)', color: ['#9be6a8', '#82bfff', '#f498c8'][i], rejected: false, points: p.points }));
    })() : local ? await Promise.all((progressive ? [
      ['inner', '#9be6a8'], ['middle-to-outer', '#82bfff'], ['outer-max', '#f498c8']
    ] : [
      ['inner', '#9be6a8'], ['middle-tighten', '#82bfff'], ['outer-tighten', '#f498c8']
    ]).map(async ([plan, color]) => {
      const alias = json.Geometry + '-' + plan;
      const responses = await Promise.all([fetch(selectedRoot + alias + '-accepted.csv'), fetch(selectedRoot + alias + '.json')]);
      if (responses.some(r => !r.ok)) throw new Error('Brak zapisu do porównania linii.');
      const [text, document] = await Promise.all([responses[0].text(), responses[1].json()]);
      const crossings = document.Run.Riders[0].Measurement.CrossingSeconds;
      return { name: plan, color, rejected: !document.Evaluation.Feasible, points: parseCsv(text).filter(p => p.t >= crossings[0] && p.t <= crossings[1]) };
    })) : [];
    const nextWidthGeometries = progressive ? await Promise.all([
      localRoot + 'egg-mild-geometry.json', widthRoot + 'progressive-b-geometry.json', widthRoot + 'progressive-c-geometry.json'
    ].map(async url => { const response = await fetch(url); if (!response.ok) throw new Error('Brak geometrii porównawczej A/B/C.'); return response.json(); })) : [];
    if (generation !== loadGeneration) return;
    geometry = nextGeometry;
    widthGeometries = nextWidthGeometries;
    $('width-controls').hidden = !progressive;
    $('width-comparison').hidden = !progressive;
    comparisonTrails = comparisons;
    $('comparison-legend').hidden = !(local || authored);
    $('comparison-legend').replaceChildren();
    for (const line of comparisons) {
      const label = document.createElement('span'); label.style.color = line.color;
      label.textContent = `${line.name}${line.rejected ? ' (odrzucony)' : ''} · `;
      $('comparison-legend').append(label);
    }
    if (local) $('comparison-legend').append(document.createTextNode('zapis okr. 2'));
    $('safety-note').hidden = !(local || authored);
    $('safety-note').textContent = crossover ? `#106 · ${key.endsWith('-stress') ? 'ODRZUCONY: 200 km/h, wyjazd poza bandę; bez rankingu' : 'PASS: wolniejsza kontrola, bezpieczny tracking'} · kolorowe plany C3, turkusowy ślad Box2D. Obrys i pozycje bez korekt.` : inverse ? `#105 · ${json.BoundarySafe ? 'prześwit PASS' : 'prześwit FAIL'} · czas diagnostyczny; brak konstruktywnego dowodu inverse. Kolorowe linie: plany, turkusowy ślad: Box2D.` : local ? `${json.Plan.Name} · ${json.Evaluation.Feasible ? 'WYKONALNY' : 'ODRZUCONY'} · min banda ${format(json.Evaluation.MinimumOuterClearanceMeters, 3)} m · wymagane ≥0,5 m` : '';
    $('safety-note').title = local ? json.Evaluation.Rejections.join(', ') : '';
    $('safety-note').classList.toggle('unsafe', (crossover && key.endsWith('-stress')) || (local && !json.Evaluation.Feasible));
    $('safety-note').classList.remove('caution');
    $('recording-mode').hidden = !(recording || key === 'pack' || key === 'solo');
    $('recording-mode').textContent = recording ? recording.mode + ' · pełny gaz 100%' : 'HISTORYCZNY #95 · starsza fizyka, sprzed niezależnego tylnego koła #100';
    if (recording) {
      const status = boundaryStatus(rawJson);
      $('safety-note').textContent = `${status.text} · min kreda ${format(rawJson.MinimumInner, 3)} m / banda ${format(rawJson.MinimumOuter, 3)} m · tracking FAIL`;
      $('safety-note').classList.toggle('unsafe', status.level === 'unsafe');
      $('safety-note').classList.toggle('caution', status.level === 'caution');
      $('safety-note').title = `Between-step bound kreda ${format(rawJson.ConservativeInner, 3)} m / banda ${format(rawJson.ConservativeOuter, 3)} m. Brak certyfikatu nie oznacza zaobserwowanego crossing.`;
    }
    document.querySelector('.map-title span').textContent = (authored || local || key.startsWith('shape-')) ? 'GEOMETRIA EKSPERYMENTALNA' : 'MOTOARENA · TORUŃ';
    canvas.setAttribute('aria-label', authored ? 'Tor progressive-b; pozycje i yaw z zapisu przejazdu' : (local || key.startsWith('shape-')) ? `Tor ${json.Geometry}; pozycje i yaw z zapisu przejazdu` : 'Motoarena w skali metrycznej. Wybierz zawodnika klikając motocykl.');
    document.querySelector('.map-title small').textContent = crossover ? '#106 · progressive-b · INNER / WIDE STRAIGHT / overspeed' : inverse ? '#105 · progressive-b · matched apex / legalne siły' : progressive ? `#103 · ${widthA ? 'A' : key[6].toUpperCase()} · bandy: A pomarańczowa / B turkusowa / C fioletowa` : local ? `#102 · ${json.Geometry} · obrys + 0,5 m` : key.startsWith('shape-') ? `#101 · ${json.Geometry} · metry` : 'Geometria modelu · metry';
    if (recording) document.querySelector('.map-title small').textContent = 'progressive-b · zapis Box2D · plan C3';
    replay = new Replay(csv, json, nativeCsv); selected = [...replay.tracks.keys()][0];
    $('smooth').checked = !recording;
    if (recording) $('rate').value = '0.1';
    const track = replay.tracks.get(selected), gap = track.slice(1).reduce((maximum, p, i) => Math.max(maximum, p.t - track[i].t), 0);
    $('sampling-note').textContent = `${recording ? 'Native snapshoty' : 'Zapis CSV'} · max Δt ${format(gap * 1000, 1)} ms · interpolacja = rendering, nie symulacja`;
    const buttons = [...replay.tracks.keys()].map(id => { const button = document.createElement('button'); button.textContent = `R${id}`;
      button.dataset.id = String(id); button.style.setProperty('--rider', color(id)); button.addEventListener('click', () => chooseRider(id)); return button; });
    $('riders').replaceChildren(...buttons); $('source-count').textContent = `${replay.tracks.size} ${replay.tracks.size === 1 ? 'motocykl' : 'motocykle'}`;
    $('scrub').max = String(replay.duration); setTime(0); chooseRider(selected); fit(); $('load-state').hidden = true;
    if (recording) jumpBend(0);
  } catch (error) {
    if (generation !== loadGeneration) return;
    replay = null; $('load-state').hidden = false; $('load-state').classList.add('error');
    $('load-state').textContent = `${error.message}\nUruchom stadion poleceniem .\\uruchom-stadion.ps1.`; dirty = true;
  }
}
function rememberScenario(key) {
  const url = new URL(location.href);
  url.searchParams.set('case', key);
  for (const param of ['run', 'group', 't']) url.searchParams.delete(param);
  history.replaceState(null, '', url);
}
$('scenario').addEventListener('change', e => {
  rememberScenario(e.target.value);
  loadScenario(e.target.value);
});
$('follow-rider').addEventListener('change', event => {
  if (event.target.value) followRider(Number(event.target.value));
});
$('play').addEventListener('click', () => { if (replay && time >= replay.duration) setTime(0); setPlaying(true); });
$('pause').addEventListener('click',()=>setPlaying(false));
$('restart').addEventListener('click',()=>{setPlaying(false);setTime(0);});
$('advanced-toggle').addEventListener('click',()=>{const shown=document.body.classList.toggle('advanced');$('advanced-toggle').setAttribute('aria-expanded',String(shown));dirty=true;lastPanelTime=-1;});
for(const button of document.querySelectorAll('[data-rate]'))button.addEventListener('click',()=>{$('rate').value=button.dataset.rate;syncRate();});
function syncRate(){for(const button of document.querySelectorAll('[data-rate]'))button.setAttribute('aria-pressed',String(button.dataset.rate===$('rate').value));}
$('rate').addEventListener('change',syncRate);
$('scrub').addEventListener('input', e => { setPlaying(false); setTime(e.target.value); });
for (const [id, direction] of [['previous', -1], ['next', 1]]) $(id).addEventListener('click', () => { if (!replay) return; setPlaying(false); const t = replay.step(time, direction); if (t != null) setTime(t); });
$('fit').addEventListener('click', fit); $('zoom-in').addEventListener('click', () => zoom(1.25)); $('zoom-out').addEventListener('click', () => zoom(.8));
$('closeup').addEventListener('click', closeup);
$('record-clip').addEventListener('click', recordClip);
for (let i = 0; i < 2; i++) $('bend-' + (i + 1)).addEventListener('click', () => jumpBend(i));
$('follow').addEventListener('change', () => { if ($('follow').checked) camera.scale = Math.max(camera.scale, fitScale * 3); dirty = true; });
for (const id of ['trails', 'targets', 'segments', 'outer-limit', 'compare-lines', 'compare-widths', 'vectors', 'smooth']) $(id).addEventListener('change', () => { dirty = true; lastPanelTime = -1; });
$('all-events').addEventListener('change', rebuildEvents);
$('h25-compare').addEventListener('change', event => selectComparison(event.target.value));
$('h25-comparison').addEventListener('toggle', () => {
  if (!$('h25-comparison').open) delete canvas.dataset.comparison;
  dirty = true;
});
for (const [id,event] of [['exit-vectors','change'],['exit-target','change'],['exit-diagnostics','toggle']])
  $(id).addEventListener(event, () => { dirty=true; lastPanelTime=-1; });
for (const id of ['a','b','prev','new'])
  $('trace200-'+id).addEventListener('change',()=>{dirty=true;});
$('solo200-comparison').addEventListener('toggle',()=>{dirty=true;});
for (const id of ['trace199-a' ,'trace199-b','trace199-base'])
  $(id).addEventListener('change',()=>{dirty=true;});
$('trace199-b-version').addEventListener('change',()=>{dirty=true;});
$('solo199-comparison').addEventListener('toggle',()=>{dirty=true;});
$('exit-jump').addEventListener('change', event => {
  if (event.target.value==='') return;
  setPlaying(false);
  setTime(Number(event.target.value));
  event.target.value='';
});
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
    const riders = [...replay.tracks.keys()].map(id => ({ id, ...worldToScreen(displayState(id)) }));
    riders.sort((a, b) => Math.hypot(a.x - mouse.x, a.y - mouse.y) - Math.hypot(b.x - mouse.x, b.y - mouse.y));
    if (riders[0] && Math.hypot(riders[0].x - mouse.x, riders[0].y - mouse.y) < Math.max(20, camera.scale * 1.4)) chooseRider(riders[0].id);
  }
  drag = null;
});
canvas.addEventListener('pointercancel', () => { drag = null; });
document.addEventListener('keydown', e => {
  if (['INPUT', 'SELECT', 'BUTTON', 'TEXTAREA'].includes(document.activeElement.tagName)) return;
  if (e.code === 'Space') { e.preventDefault(); if(replay&&time>=replay.duration)setTime(0); setPlaying(!playing); }
  if (e.key === 'ArrowLeft' || e.key === 'ArrowRight') { e.preventDefault(); $(e.key === 'ArrowLeft' ? 'previous' : 'next').click(); }
});
new ResizeObserver(resize).observe($('canvas-wrap'));
let lastFrame = performance.now();
function frame(now) {
  if (playing && replay) { setTime(time + Math.min((now - lastFrame) / 1000, .1) * Number($('rate').value), false); if (time >= replay.duration) { setPlaying(false); if (clipRecorder?.state === 'recording') clipRecorder.stop(); } }
  lastFrame = now;
  if (dirty && geometry && replay) { draw(); if (lastPanelTime < 0 || Math.abs(time - lastPanelTime) >= .03 || !playing) { updatePanel(); lastPanelTime = time; } dirty = false; }
  requestAnimationFrame(frame);
}
requestAnimationFrame(frame);
try {
  const response = await fetch('./geometry.json'); if (!response.ok) throw new Error('Nie znaleziono geometrii modelu.');
  nativeGeometry = await response.json(); geometry = nativeGeometry;
  const entries = await setupRaces(async key => {
    $('scenario').value = key;
    rememberScenario(key);
    if (await loadScenario(key, 0)) setPlaying(true);
    else throw new Error('Bieg zapisano, ale odtwarzacz nie mógł wczytać nagrania.');
  }, () => setPlaying(false), updateComparisonChoices);
  if (document.body.hasAttribute('data-portable')) {
    const included = new Set(['study172', 'pack', 'solo']);
    for (const option of [...$('scenario').options]) {
      if (!isH25(option.value) && !included.has(option.value)) option.remove();
    }
    for (const link of document.querySelectorAll('.advanced-link')) link.hidden = true;
  }
  const requested = new URL(location.href).searchParams.get('case');
  const available = [...$('scenario').options].some(o => o.value === requested);
  const latest191 = entries.find(item => item.id === '191-gearing');
  $('scenario').value = available ? requested : latest191 ? raceKey(latest191.id)
    : entries.length ? raceKey(entries[0].id) : 'study172';
  await loadScenario($('scenario').value);
} catch (error) { $('load-state').classList.add('error'); $('load-state').textContent = `${error.message}\nUruchom statyczny serwer HTTP z katalogu repozytorium (instrukcja w README).`; }
