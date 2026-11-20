import {Replay} from './model.mjs';
import {readGzip} from './recordings.mjs';

const $ = id => document.getElementById(id);
const number = new Intl.NumberFormat('pl-PL', {maximumFractionDigits: 1});
const places = ['A', 'B', 'C', 'D'];

export const raceKey = id => "h25-" + id;
export const isH25 = key => /^h25-(191-(nominal|gearing)|193-(solo|four|alternate|two)-(stock|exit)|194-(solo-inside|solo-wide|four-stock|four-nominal|four-alternate|alternate-stock)|202-(h25|l|e|le|learned|alternate|l-trained|e-trained)|196-szybka-prosta|197-(autonomiczna|solo-dnf)|198-(autonomia|solo|czterech)|200-(solo|b|inny-start|inne-przelozenie|potwierdzenie)|199-(swobodna|swobodna-inny-stan|solo|bazowy-b|inny-stan|drugi-luk)|[a-f0-9]{32})$/.test(key);

export async function raceRequest(url, options) {
  const response = await fetch(url, options);
  if (!response.ok) {
    const error = await response.json().catch(() => null);
    throw new Error(error?.error ?? 'Serwer stadionu jest niedostępny. Uruchom uruchom-stadion.ps1.');
  }
  return response.json();
}

export function raceFromRecording(payload) {
  const riders = payload?.riders;
  if (payload?.format !== 'stadium-h25-v1' || !(payload?.exitStudy ? [1, 2, 4] : [2, 4]).includes(riders?.length)
      || !Array.isArray(payload.rows) || !payload.geometry?.segments?.length
      || !Array.isArray(payload.document?.Run?.Initial?.Riders)
      || riders.some((r, i) => r.Id !== i + 1 || !Number.isFinite(r.Ratio) || r.Ratio < 8 || r.Ratio > 10
                   || !places.includes(r.Gate))) {
    throw new Error('Niekompletne nagranie H25. Brakuje geometrii, stanów lub konfiguracji motocykli.');
  }
  const replay = new Replay(payload.rows, payload.document);
  if (replay.tracks.size !== riders.length || replay.run.RequiredLaps !== 3
      || riders.some(r => replay.tracks.get(r.Id)?.at(-1)?.t !== replay.duration)) {
    throw new Error('Nagranie H25 nie zawiera pełnej, wspólnej osi czasu.');
  }
  replay.h25 = {riders, outcome: payload.outcome, fingerprint: payload.fingerprint};
  replay.exitStudy = payload.exitStudy ?? null;
  replay.physics202 = payload.physics202 ?? null;
  return {geometry: payload.geometry, replay};
}

export async function loadH25(key) {
  let response;
  try {
    response = await fetch('/api/races/' + key.slice(4));
  } catch {
    throw new Error('Nie można połączyć się z lokalnym serwerem nagrań.');
  }
  if (!response.ok) {
    const failure = await response.json().catch(() => null);
    throw new Error(failure?.error ?? 'Nie można wczytać nagrania H25.');
  }
  let payload;
  try {
    payload = JSON.parse(await readGzip(response));
  } catch {
    throw new Error('Nagranie H25 jest uszkodzone, niekompletne lub przeglądarka nie obsługuje formatu gzip.');
  }
  return raceFromRecording(payload);
}

export function updateRaceTable(replay, time, tracked, selectRider) {
  const states = replay.h25.riders.map(r => ({rider: r, state: replay.state(r.Id, time, false)}));
  states.sort((a, b) => Number(b.state.finished) - Number(a.state.finished)
      || (a.state.finished && b.state.finished ? a.state.finish - b.state.finish
                                               : b.state.reference - a.state.reference)
      || a.rider.Id - b.rider.Id);
  const atEnd = time >= replay.duration - 1e-8;
  $('leaderboard-title').textContent = atEnd ? 'Klasyfikacja końcowa' : 'Klasyfikacja';
  const body = $('race-order');
  // Reuse rows to keep the follow buttons focused as playback advances.
  if (body.dataset.fingerprint !== replay.h25.fingerprint || body.rows.length !== states.length) {
    body.replaceChildren();
    for (const {rider} of states) {
      const tr = body.insertRow();
      tr.dataset.rider = rider.Id;
      const button = document.createElement('button');
      button.type = 'button';
      button.textContent = 'R' + rider.Id;
      button.setAttribute('aria-label', 'Śledź R' + rider.Id);
      button.addEventListener('click', () => selectRider(rider.Id));
      tr.insertCell().append(button);
      for (let i = 0; i < 4; i++) tr.insertCell();
    }
    body.dataset.fingerprint = replay.h25.fingerprint;
  }
  const rows = new Map(Array.from(body.rows, row => [Number(row.dataset.rider), row]));
  for (const [index, {rider, state}] of states.entries()) {
    const tr = rows.get(rider.Id);
    if (body.rows[index] !== tr) body.insertBefore(tr, body.rows[index]);
    tr.classList.toggle('tracked', tracked === rider.Id);
    tr.cells[0].firstChild.setAttribute('aria-pressed', String(tracked === rider.Id));
    tr.cells[1].textContent = String(index + 1);
    tr.cells[2].textContent = state.finished ? state.finish.toFixed(3) + ' s' : atEnd ? ((replay.exitStudy?.native196 || replay.exitStudy?.native197?.complete) ? 'Odcinek ukończony' : 'DNF') : state.lap + '/3';
    tr.cells[3].textContent = number.format(state.speed * 3.6);
    tr.cells[4].textContent = number.format(rider.Ratio) + ':1';
  }
}

export async function setupRaces(onRecorded, onStart, onEntries) {
  const form = $('race-config'), fields = $('race-gearing'), count = $('race-count');
  const settings = [10, 9, 8, 9];
  function update() {
    const controls = [];
    for (let i = 0; i < Number(count.value); i++) {
      const label = document.createElement('label'), rider = document.createElement('span');
      const ratio = document.createElement('input'), output = document.createElement('output');
      const field = count.value === '1' ? 'D' : count.value === '2' ? places[i+1] : places[i];
      rider.textContent = 'R' + (i + 1) + ' · pole ' + field;
      ratio.type = 'range';
      ratio.id = 'ratio-' + (i + 1);
      ratio.setAttribute('aria-label', 'Przełożenie R' + (i + 1));
      ratio.min = 8; ratio.max = 10; ratio.step = .1; ratio.value = settings[i];
      function show() {
        settings[i] = Number(ratio.value);
        output.textContent = number.format(settings[i]) + ':1';
        const label = settings[i] === 8 ? ' · długie' : settings[i] === 9 ? ' · neutralne' : settings[i] === 10 ? ' · krótkie' : '';
        ratio.setAttribute('aria-valuetext', output.textContent + label);
      }
      show();
      ratio.addEventListener('input', show);
      label.append(rider, ratio, output);
      controls.push(label);
    }
    fields.replaceChildren(...controls);
  }
  function setEntries(entries) {
    const group = $('h25-recordings');
    const selected = $('scenario').value;
    group.replaceChildren(...entries.map(entry => new Option(entry.label, raceKey(entry.id))));
    group.hidden = entries.length === 0;
    onEntries(entries);
    if (Array.from($('scenario').options).some(option => option.value === selected)) $('scenario').value = selected;
  }
  const status = $('race-status');
  let entries;
  try {
    entries = await raceRequest('/api/races');
  } catch {
    form.hidden = true;
    return [];
  }
  setEntries(entries);
  update();
  count.addEventListener('change', update);
  $('race-controller').addEventListener('change', () => {
    if ($('race-controller').value === 'speedway200' || $('race-controller').value.startsWith('h26-')) {
      count.value = '1';
      settings[0] = 8;
    }
    update();
    status.textContent = $('race-controller').value.startsWith('h26-') ? 'H26: osobna wyuczona polityka solo 8:1. Parametry niezwalidowane pomiarowo. C #200 jest niekompatybilna.' : '';
  });
  form.addEventListener('submit', async event => {
    event.preventDefault();
    if ($('race-run').disabled) return;
    onStart();
    const fieldset = form.querySelector('fieldset');
    $('race-run').disabled = true;
    fieldset.disabled = true;
    status.classList.remove('race-error');
    status.textContent = 'Obliczanie biegu…';
    try {
      const ratios = settings.slice(0, Number(count.value));
      const saved = await raceRequest('/api/races', {
        method: 'POST',
        headers: {'Content-Type': 'application/json', 'X-Stadium-Request': 'run'},
        body: JSON.stringify({ratios, controller: $('race-controller').value}),
      });
      entries = [saved, ...entries.filter(item => item.id !== saved.id)];
      setEntries(entries);
      status.textContent = saved.complete ? 'Bieg zapisany. Trzy okrążenia ukończone.'
                                         : 'Bieg zapisany. Co najmniej jeden zawodnik nie ukończył.';
      await onRecorded(raceKey(saved.id));
    } catch (error) {
      status.classList.add('race-error');
      status.textContent = error.message;
    } finally {
      fieldset.disabled = false;
      $('race-run').disabled = false;
    }
  });
  return entries;
}

