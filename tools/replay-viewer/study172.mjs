// Read-only adapter. XY, body heading, velocities and active controls are recorded values.
import {Replay} from './model.mjs';
import {readGzip} from './recordings.mjs';

export function study172Replay(bundle) {
  if(bundle.provenance?.base !== 'c5f0a4cc4de34fc4c8c47ef912cafabf34055f29' ||
     bundle.provenance.run !== 'v1-K4-S1' || bundle.provenance.crossings.length !== 3)
    throw new Error('Nieprawidłowe pochodzenie nagrania #172.');
  const replay = new Replay(bundle.rows, bundle.document);
  replay.study172 = bundle.provenance;
  return replay;
}

export async function loadStudy172(fetcher=fetch) {
  const bundle = JSON.parse(await readGzip(await fetcher('./recordings/172-s3-k4.json.gz')));
  return {geometry:bundle.geometry, replay:study172Replay(bundle)};
}
