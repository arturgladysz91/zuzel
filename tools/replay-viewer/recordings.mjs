// Existing frozen evidence only. Paths are relative to the viewer, not copied assets.
const root = '../../docs/experiments/';
export const currentRecordings = {
  'current-baseline-inner': { label: '#107 · baseline INNER · pełny gaz · poprawki OFF', root: root + 'full-throttle-slide-grip-audit/', name: 'repeat18', wide: false, mode: 'Bazowy regulator · coupled inverse OFF · full yaw OFF' },
  'current-baseline-wide': { label: '#106/#107 · baseline WIDE · pełny gaz · poprawki OFF', root: root + 'speed-grip-line-crossover/', name: 'baseline-wide', wide: true, mode: 'Bazowy regulator · coupled inverse OFF · full yaw OFF' },
  'current-full-inner': { label: '#109 · FULL INNER · regulator eksperymentalny', root: root + 'full-contact-yaw-feedforward/', name: 'full-inner', wide: false, mode: 'FULL eksperymentalny · coupled inverse ON · full yaw ON' },
  'current-full-wide': { label: '#109 · FULL WIDE · regulator eksperymentalny', root: root + 'full-contact-yaw-feedforward/', name: 'full-wide', wide: true, mode: 'FULL eksperymentalny · coupled inverse ON · full yaw ON' }
};
export async function readGzip(response) {
  if (!response.ok) throw new Error('Brak natywnego zapisu przejazdu.');
  if (typeof DecompressionStream === 'undefined') throw new Error('Ten zapis gzip wymaga aktualnego Chrome/Edge.');
  return new Response(response.body.pipeThrough(new DecompressionStream('gzip'))).text();
}
