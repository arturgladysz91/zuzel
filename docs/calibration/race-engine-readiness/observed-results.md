# Numerical observations

Baseline `c13af7b8a16fe4e58e9d404c849cdf323649b0ed`. Generated from the machine-readable evidence with `tools/race-engine-readiness/report.py`. Tables preserve stored numeric values; exact source bits and paths remain in the typed traces. These are observations, not calibration targets.

## Starts, gates and first bend

| Gate / rider | Reaction / movement onset s | Speed at 1 s m/s | Speed at 2 s m/s | Profile 70 km/h s | First segment movement s |
| --- | --- | --- | --- | --- | --- |
| A / 1 | 0.24000001 | 7.44003 | 17.019146 | 2.2745256 | 2.714497 |
| B / 2 | 0.24000001 | 7.440004 | 17.018585 | 2.2745855 | 2.716211 |
| C / 3 | 0.24000001 | 7.440003 | 17.018429 | 2.2745976 | 2.7174566 |
| D / 4 | 0.24000001 | 7.4400034 | 17.019114 | 2.2745378 | 2.718346 |

Identical neutral riders have the same reaction input. Gate geometry creates small launch differences and substantial bend-exit differences; collection reversal does not reassign gates. These scenarios do not estimate a universal fair gate balance.

| Seed 19 / C | First-bend entry [clock, riders] | First-bend exit [clock, riders] | Classification |
| --- | --- | --- | --- |
| balanced-example | [[2.952852249145508, [2]], [2.9538726806640625, [3]], [2.954533815383911, [4]], [2.9568352699279785, [1]]] | [[6.682831764221191, [1]], [6.910556316375732, [2]], [7.1193084716796875, [3]], [7.398138523101807, [4]]] | [(1, 'Finished'), (4, 'Finished'), (3, 'Finished'), (2, 'Finished')] |
| balanced-motoarena | [[2.9544970989227295, [1]], [2.9562110900878906, [2]], [2.957456588745117, [3]], [2.958346128463745, [4]]] | [[7.229324817657471, [1]], [7.542989253997803, [2]], [7.942327499389648, [3]], [8.101994514465332, [4]]] | [(1, 'Finished'), (2, 'Finished'), (3, 'Finished'), (4, 'Finished')] |
| overall | [[2.8570902347564697, [4]], [2.920569658279419, [3]], [2.987250328063965, [2]], [3.0684573650360107, [1]]] | [[6.840372085571289, [3]], [6.995499134063721, [4]], [7.019440650939941, [1]], [7.021413326263428, [2]]] | [(4, 'Finished'), (3, 'Finished'), (2, 'Finished'), (1, 'Finished')] |
| starter-distance | [[2.8639256954193115, [1]], [3.005298137664795, [2]], [3.0063486099243164, [3]], [3.007032632827759, [4]]] | [[6.656811714172363, [1]], [6.930093765258789, [2]], [7.207431793212891, [3]], [7.465765953063965, [4]]] | [(4, 'Finished'), (2, 'Finished'), (3, 'Finished'), (1, 'Finished')] |

The outside gate D rider in `overall` reaches the first bend first; Start 80 rider 1 in `starter-distance` reaches entry/exit first but finishes behind the others. Faster distance riding and incidents can overcome launch position. `Starts.Profile` retains the actual acceleration profile; `Segments.Acceleration`, state transitions and first-bend contact episodes retain sudden changes and evasions. At tape release standing riders are one exact-progress tie group, not ordered by ID. Rolling initial progress is recorded individually.

## Complete corner observations

| First corner / C / seed 19 | Entry m/s | Minimum m/s | Apex speed IEEE bracket | Exit m/s | Sum native path distances m | Sum native durations s | Apex common-clock bracket s |
| --- | --- | --- | --- | --- | --- | --- | --- |
| same-line / 1 | 23.295776 | 18.779562 | float:41963C8B → float:41963CC2 | 20.760033 | 75.398226 | 3.7259965 | [4.7477834820747375, 4.801032721996307] |
| rain-motoarena / 1 | 24.666168 | 21.204552 | float:41A9F2F2 → float:41A9BA2A | 23.05522 | 97.38936900000002 | 4.2960417 | [5.016249716281891, 5.027157247066498] |
| rolling-C / 1 | 22.00196 | 18.713205 | float:4195B4A5 → float:4195B4CF | 20.869492 | 83.201236 | 4.1817228 | [4.706239342689514, 4.7596771121025085] |
| rolling-C / 2 | 26.205545 | 24.497618 | float:41C3FB1F → float:41C3FB2F | 26.358526 | 111.015547 | 4.3473288 | [4.576831638813019, 4.61765193939209] |

| First corner / C / seed 19 | Lateral offset range m | Curvature range 1/m | Sampled grip range | Node acceleration range m/s² | Exit net acceleration m/s² |
| --- | --- | --- | --- | --- | --- |
| same-line / 1 | [1, 1] | [0.041666668, 0.041666668] | [0.93399996, 0.95049995] | [-2.5624704701909593, 1.5577797655477614] | [] |
| rain-motoarena / 1 | [1, 1] | [0.032258064, 0.032258064] | [0.90815, 0.912825] | [-2.5433633265734557, 1.262613714522587] | [] |
| rolling-C / 1 | [1, 6.009617] | [0.034471326, 0.041666668] | [0.61680007, 0.7590764] | [-1.9512921216727006, 1.4562818251131593] | [] |
| rolling-C / 2 | [10, 13] | [0.027777778, 0.030303031] | [0.8926125, 0.991] | [-2.95335895952569, 1.2570406632212303] | [] |

The apex is the canonical halfway point of the existing corner topology; its surrounding executed nodes supply a bracket. Minimum speed can occur elsewhere. Per-segment evidence also retains longitudinal acceleration bounds, physical lateral range, curvature, route-sampled grip, correction and exit drive. The rolling outside case pays a longer path while retaining higher speed than the inner leader. This establishes a physical distance/speed tradeoff in this fixture, not a claim that every wider line is advantageous. No ordinary reference-point edge violation was witnessed in completed cases; full-bike uncertified intervals and unswept width changes remain an evidence gap.

## Nine maneuver probes

| Maneuver | Seed 19 / C | Strict racing transitions | First [ahead, behind, bracket] | Interpretation |
| --- | --- | --- | --- | --- |
| Faster follower on straight | rolling-A | 1 | [(2, 1, 2.3883955478668213, 2.3961875438690186)] | Verified rider 2 over 1 on segment 0. |
| Faster follower entering bend | bend-catch | 1 | [(2, 1, 4.384176716208458, 4.42447005212307)] | Catch completes on segment 3 after the bend; not a verified in-bend pass. |
| Inside attack | rolling-B | 1 | [(2, 1, 3.1652745604515076, 3.2046649158000946)] | Verified rider 2 over 1 on bend entry segment 1. |
| Outside attack | rolling-C | 1 | [(2, 1, 0.9175395369529724, 0.9526123404502869)] | Verified rider 2 over 1 before the bend; compare subsequent physical corner costs. No general outside-pass impossibility. |
| Defensive line occupation/widening | rolling-E | 0 | [] | Blocked-inner opportunity; no pass in this seed. A selected response or lane change alone does not prove successful defense or forced opponent widening. |
| Side-by-side corner entry | rolling-D | 0 | [] | Positive alongside exposure; no strict pass in this seed. |
| Three-rider squeeze | three-squeeze-binary | 6 | [(1, 3, 2.9142893254756927, 2.942701071500778)] | Completed companion records repeated lead changes and contact. Original decimal scenario is P1 REA-009 in every seed/configuration. |
| Four-rider close racing | four-close-regain-binary | 12 | [(2, 3, 2.750092715024948, 2.7546857595443726)] | Multiple strict pair lead changes in a completed four-rider heat. Original decimal scenario is P1 REA-009. |
| Regaining a position | four-close-regain-binary | 12 | [(2, 3, 2.750092715024948, 2.7546857595443726)] | Rider 1 passes 3, loses to 3, and passes 3 again; brackets are in trace-evidence.json. This proves re-passing, not attribution to a specific tactical intent. |

All A/B/C and seeds 7/19/83 remain separate in cases.json.gz. Corner/lap crossing orders are boundary arrival groups. Common-clock passes use actual executed canonical progress; close/tied intervals and missing coverage are reported. Retirement gains are classification effects, not passes. Tiny start/side-by-side lead reversals are not clear-bike, sustained-position or intention-success counts. The current observer cannot establish a causal forced-widening undercut solely from a selected CutInside candidate; physical paths and FullAudit alternatives support independent review.

## Interaction incidence, severity and recovery

| Config | Population | Complete | Failed | Rider status counts | Episodes | Predicted mechanical | Avoided predicted mechanical | Applied pairs | Applied rider severity |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| A | ordinary standing start | 45 | 0 | {'Crashed': 7, 'Finished': 173} | 0 | 0 | 0 | 0 | {} |
| A | ordinary rolling fixture | 21 | 0 | {'Crashed': 2, 'Finished': 82} | 0 | 0 | 0 | 0 | {} |
| A | extreme stress (excluded from ordinary rates) | 3 | 0 | {'Finished': 12} | 0 | 0 | 0 | 0 | {} |
| B | ordinary standing start | 44 | 1 | {'Crashed': 1, 'Finished': 175} | 317 | 95 | 83 | 0 | {} |
| B | ordinary rolling fixture | 21 | 0 | {'Crashed': 1, 'Finished': 83} | 11 | 3 | 3 | 0 | {} |
| B | extreme stress (excluded from ordinary rates) | 3 | 0 | {'Finished': 12} | 15 | 10 | 7 | 0 | {} |
| C | ordinary standing start | 44 | 1 | {'Crashed': 12, 'Finished': 164} | 319 | 108 | 88 | 21 | {'Brush': 10, 'Crash': 11, 'Disturbed': 8, 'LostRhythm': 12} |
| C | ordinary rolling fixture | 21 | 0 | {'Crashed': 1, 'Finished': 83} | 11 | 3 | 3 | 0 | {} |
| C | extreme stress (excluded from ordinary rates) | 3 | 0 | {'Crashed': 6, 'Finished': 6} | 3 | 3 | 0 | 9 | {'Crash': 6, 'MajorSave': 6} |

Ordinary standing C has 12 crashes among 176 riders in completed heats, including 11 applied Crash consequences; the other crash is a normal segment incident. A/B have 7/1 crashes among 180/176 completed-heat riders. These are small deliberately varied controlled matrices, not ordinary league crash-rate estimates. Stress C has 6 crashed and 6 finished riders across three extreme heats, with nine applied pairs and three frontiers containing three or more riders. Ordinary standing C records six repeated-overlap suppressions and no duplicated applied pair generation. Different run/capture denominators must not be mixed.

| C / population / metric | N | P10 | P25 | P50 | P75 | P90 |
| --- | --- | --- | --- | --- | --- | --- |
| ordinary standing start / SeverityRatio | 41 | 0.0 | 0.3574429300297805 | 0.8261664670749279 | 1.8031384966853061 | 2.6350173754064783 |
| ordinary standing start / RecoverySegmentSeconds | 20 | 1.290980339050293 | 1.390429973602295 | 1.4399585723876953 | 1.591876745223999 | 1.7320590972900392 |
| ordinary standing start / EndpointApplicationDelaySeconds | 41 | 0.3211847006914468 | 0.4637697734856374 | 1.3084196189548152 | 1.6040534973144531 | 1.690867155790329 |
| ordinary standing start / NearExposureBracketSeconds | 44 | 22.202026498317718 | 30.550589106976986 | 40.1761527992785 | 56.91819002106786 | 60.74907227577023 |
| extreme stress (excluded from ordinary rates) / SeverityRatio | 12 | 1.2987786859857053 | 1.2989302564630376 | 1.5678940237006227 | 1.8368429932341324 | 1.836950170599238 |
| extreme stress (excluded from ordinary rates) / RecoverySegmentSeconds | 6 | 1.365096092224121 | 1.3701488971710205 | 1.5835838317871094 | 1.7818603515625 | 1.7818603515625 |
| extreme stress (excluded from ordinary rates) / EndpointApplicationDelaySeconds | 12 | 2.348440647125244 | 2.5096893310546875 | 2.7033482789993286 | 2.938188374042511 | 3.222980499267578 |
| extreme stress (excluded from ordinary rates) / NearExposureBracketSeconds | 3 | 10.98052679747343 | 10.98052679747343 | 10.98052679747343 | 10.98052679747343 | 10.98052679747343 |

Near/alongside durations are sums of per-pair interval unions, hence pair-seconds upper brackets, not a heat-clock occupancy estimate. Recovery is one next active segment and varies in seconds; fresh contact may replace it. Endpoint application delay measures the committed endpoint minus verified first-touch frontier. Existing #59 intentionally changes endpoint speed without reintegrating the intervening remainder. It conserves the shadow impulse contract, but does not validate continuous impact timing. FullAudit candidate rejection reasons and selected responses are retained in the compressed diagnostic artifact; Summary captures do not provide all rejected candidates. No evidence here establishes a general defensive trap or an empirically excessive-yield frequency.

## Controlled rider sensitivities

| Variable | Values | Rider 1 total times s, C | Rider 1 peak speeds m/s, C | Reaction times s, C |
| --- | --- | --- | --- | --- |
| Start | [20.0, 50.0, 80.0] | [55.808605, 55.897003, 55.685226] | [26.452843, 26.501541, 26.452885] | [0.264, 0.24000001, 0.21599999] |
| Speed | [20.0, 50.0, 80.0] | [58.14207, 55.897003, 53.955265] | [25.10697, 26.501541, 27.675951] | [0.24000001, 0.24000001, 0.24000001] |
| SlideControl | [20.0, 50.0, 80.0] | [56.90022, 55.897003, 54.988434] | [26.113289, 26.501541, 26.641209] | [0.24000001, 0.24000001, 0.24000001] |
| TrackReading | [20.0, 50.0, 80.0] | [57.031322, 55.897003, 55.897003] | [27.162859, 26.501541, 26.501541] | [0.24000001, 0.24000001, 0.24000001] |
| PairRiding | [20.0, 50.0, 80.0] | [55.897003, 55.897003, 55.897003] | [26.501541, 26.501541, 26.501541] | [0.24000001, 0.24000001, 0.24000001] |
| Adaptability | [20.0, 50.0, 80.0] | [55.987625, 55.897003, 55.90199] | [26.4965, 26.501541, 26.506687] | [0.24000001, 0.24000001, 0.24000001] |
| Technique | [20.0, 50.0, 80.0] | [55.897003, 55.897003, 55.897003] | [26.501541, 26.501541, 26.501541] | [0.24000001, 0.24000001, 0.24000001] |
| Strength | [20.0, 50.0, 80.0] | [55.897003, 55.897003, 55.897003] | [26.501541, 26.501541, 26.501541] | [0.24000001, 0.24000001, 0.24000001] |
| Mass | [60.0, 67.5, 75.0] | [55.897003, 55.897003, 55.897003] | [26.501541, 26.501541, 26.501541] | [0.24000001, 0.24000001, 0.24000001] |
| Condition | [0.4, 0.7, 1.0] | [55.897003, 55.897003, 55.897003] | [26.501541, 26.501541, 26.501541] | [0.24000001, 0.24000001, 0.24000001] |
| Style | [0.2, 0.5, 0.8] | [55.897003, 55.897003, 57.165325] | [26.501541, 26.501541, 27.704546] | [0.24000001, 0.24000001, 0.24000001] |

Speed and SlideControl shorten time across these three tested inputs; Start reaction is monotonic but whole-heat time is not. TrackReading plateaus at 50/80 here; Adaptability and risk style are not monotonically faster. Technique/Strength/Mass/Condition/PairRiding yield identical rider-1 totals in this quiet seed-19 sweep, with no applied impact on rider 1. This bounds evidence in this context, not absence of their implemented consumers. Existing fixed-contact physiology fixtures isolate mass/reserve responses without arbitrary strength speed bonuses; whole-heat outcome dominance or acceptable effect sizes need a broader matched population. The controlled-contact fixture output is reproduced by batch, hashed in EvidenceFiles, and its impulse/reserve contract is covered by unchanged tests.

| Existing fixed-contact fixture | Rider 1 reserve | Demand | Severity ratio | System A mass kg | DeltaV A magnitude m/s |
| --- | --- | --- | --- | --- | --- |
| G-technique-20 | 0.9248571428571428 | 1.1023124635399686 | 1.1918732228575504 | 144.5 | 0.49999999999999994 |
| G-technique-50 | 1.092 | 1.1023124635399686 | 1.009443647930374 | 144.5 | 0.49999999999999994 |
| G-technique-80 | 1.2591428571428573 | 1.1023124635399686 | 0.8754467035148377 | 144.5 | 0.49999999999999994 |
| H-strength-20 | 0.9582857142857145 | 1.1023124635399686 | 1.1502962499671703 | 144.5 | 0.49999999999999994 |
| H-strength-50 | 1.092 | 1.1023124635399686 | 1.009443647930374 | 144.5 | 0.49999999999999994 |
| H-strength-80 | 1.2257142857142858 | 1.1023124635399686 | 0.8993225227016061 | 144.5 | 0.49999999999999994 |
| I-condition-1 | 1.092 | 1.1023124635399686 | 1.009443647930374 | 144.5 | 0.49999999999999994 |
| I-condition-0.7 | 1.0452000000000001 | 1.1023124635399686 | 1.0546426172406893 | 144.5 | 0.49999999999999994 |
| I-condition-0.4 | 0.9984000000000001 | 1.1023124635399686 | 1.1040789899238466 | 144.5 | 0.49999999999999994 |
| J-mass-60 | 1.092 | 1.1316813568847277 | 1.0363382389054283 | 137 | 0.5133214920071046 |
| J-mass-67.5 | 1.092 | 1.1023124635399686 | 1.009443647930374 | 144.5 | 0.49999999999999994 |
| J-mass-75 | 1.092 | 1.0744293489478949 | 0.9839096602086949 | 152 | 0.48735244519392906 |

## Motoarena reference distributions

| Metric (Vmax/spread km/h; average m/s; other s) | Population | N | P10 | P25 | P50 | P75 | P90 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| pge_clean_vmax | Real | 407 | 108.6 | 111 | 113.7 | 115.5 | 116.9 |
| pge_clean_vmax | A | 68 | 93.88928375244141 | 95.4721836090088 | 96.63288230895996 | 99.2352705001831 | 99.8782381439209 |
| pge_clean_vmax | B | 67 | 93.06604385375977 | 95.40554809570312 | 96.42177658081054 | 99.23303375244141 | 99.8331069946289 |
| pge_clean_vmax | C | 66 | 93.617533493042 | 95.42253913879395 | 96.50531730651855 | 99.2321273803711 | 99.8331069946289 |
| pge_clean_average_speed | Real | 407 | 21.47257725118 | 21.919482305949998 | 22.3848540291 | 22.8619377952 | 23.3573081154 |
| pge_clean_average_speed | A | 68 | 21.5895751953125 | 22.689655780792236 | 22.83321762084961 | 23.20831298828125 | 23.575478553771973 |
| pge_clean_average_speed | B | 67 | 21.34403839111328 | 22.688129425048828 | 22.872352600097656 | 23.203734397888184 | 23.497329330444337 |
| pge_clean_average_speed | C | 66 | 21.472076416015625 | 22.707414627075195 | 22.887954711914062 | 23.208353996276855 | 23.577741622924805 |
| pge_clean_l1_penalty | Real | 407 | 1.79 | 1.89 | 1.99 | 2.105 | 2.198000000000001 |
| pge_clean_l1_penalty | A | 68 | 1.3790738105773925 | 1.6365890502929688 | 1.9061870574951172 | 2.1181118488311768 | 2.2334460258483886 |
| pge_clean_l1_penalty | B | 67 | 1.3876352310180664 | 1.5075268745422363 | 1.8754396438598633 | 2.1724910736083984 | 2.6307098388671872 |
| pge_clean_l1_penalty | C | 66 | 1.4003801345825195 | 1.5446617603302002 | 1.8784451484680176 | 2.173159599304199 | 2.638113021850586 |
| pge_clean_heat_time | Real | 407 | 60.24 | 60.8505 | 61.479 | 62.068 | 62.587 |
| pge_clean_heat_time | A | 68 | 56.48872261047363 | 56.98523139953613 | 57.77618217468262 | 61.03699970245361 | 61.84988021850586 |
| pge_clean_heat_time | B | 67 | 56.52875289916992 | 57.05563926696777 | 57.92498779296875 | 61.037912368774414 | 61.849073791503905 |
| pge_clean_heat_time | C | 66 | 56.55715751647949 | 57.02657699584961 | 57.80864334106445 | 60.983811378479004 | 61.8499870300293 |
| pge_clean_l1_time | Real | 407 | 16.52 | 16.695 | 16.84 | 17.03 | 17.17 |
| pge_clean_l1_time | A | 71 | 15.286810874938965 | 15.543785572052002 | 15.904190063476562 | 16.58462619781494 | 16.914241790771484 |
| pge_clean_l1_time | B | 68 | 15.286810874938965 | 15.516685485839844 | 15.852630615234375 | 16.558730125427246 | 17.06409282684326 |
| pge_clean_l1_time | C | 68 | 15.286810874938965 | 15.516685485839844 | 15.852630615234375 | 16.558730125427246 | 17.06409282684326 |
| pge_four_rider_heat_time_spread | Real | 93 | 0.9845999999999989 | 1.2049999999999983 | 1.5890000000000057 | 1.8780000000000001 | 2.138600000000001 |
| pge_four_rider_heat_time_spread | A | 14 | 0.8201656341552734 | 0.9328279495239258 | 1.103811264038086 | 1.2209186553955078 | 1.5583007812500003 |
| pge_four_rider_heat_time_spread | B | 16 | 1.037607192993164 | 1.1813316345214844 | 1.3407115936279297 | 1.6923141479492188 | 2.1204700469970703 |
| pge_four_rider_heat_time_spread | C | 16 | 0.78009033203125 | 1.0454788208007812 | 1.2132282257080078 | 1.4598312377929688 | 1.831390380859375 |
| pge_four_rider_vmax_spread | Real | 93 | 1.6999999999999915 | 2.4000000000000057 | 4.6000000000000085 | 6.5 | 9.099999999999987 |
| pge_four_rider_vmax_spread | A | 14 | 1.988250732421875 | 2.601131629943848 | 2.9659755706787108 | 4.473768424987793 | 4.995736770629883 |
| pge_four_rider_vmax_spread | B | 16 | 1.988250732421875 | 2.952886390686035 | 3.224435806274414 | 4.755296516418457 | 5.199300384521484 |
| pge_four_rider_vmax_spread | C | 16 | 1.988250732421875 | 2.952886390686035 | 3.0601043701171875 | 4.755296516418457 | 5.199300384521484 |
| pge_four_rider_l1_spread | Real | 93 | 0.25399999999999995 | 0.33000000000000185 | 0.4499999999999993 | 0.5599999999999987 | 0.6400000000000006 |
| pge_four_rider_l1_spread | A | 14 | 0.5967079162597656 | 0.6290104389190674 | 0.6902608871459961 | 0.8864712715148926 | 1.2381534576416016 |
| pge_four_rider_l1_spread | B | 16 | 0.5795278549194336 | 0.5902481079101562 | 0.6413898468017578 | 1.2381534576416016 | 1.2428321838378906 |
| pge_four_rider_l1_spread | C | 16 | 0.5795278549194336 | 0.5902481079101562 | 0.6413898468017578 | 1.2381534576416016 | 1.2428321838378906 |

Only Motoarena standing scenarios enter this numerical comparison: 18/17/17 completed heats for A/B/C. Vmax/average/heat times use finished riders; first-lap observations may include a rider who later crashes; penalties require four laps; heat spreads require all four finished riders and 16 lap records. The real subset has 407 clean rider records and 93 four-attempt heat records. Its four-attempt grouping is the unchanged dataset definition, not a manufactured matching selection. Whole-dataset evaluator results are context only. Neutral/mixed-grip/rain audit cohorts are not mapped professionals; the measured peak/time differences cannot identify a force constant to change.

| Rider-relative own-median offsets / context | N | P10 | P25 | P50 | P75 | P90 |
| --- | --- | --- | --- | --- | --- | --- |
| Real / max_speed_kph | 403 | -3.880000000000004 | -1.7250000000000014 | 0.0 | 1.8500000000000014 | 3.299999999999997 |
| Real / average_speed_mps | 403 | -0.7401617307000016 | -0.33402030645000025 | 0.0 | 0.3073194986000001 | 0.7633665271800014 |
| Real / l1_penalty_s | 403 | -0.17899999999999994 | -0.07000000000000017 | 0.0 | 0.10250000000000004 | 0.19999999999999996 |
| A / max_speed_kph | 24 | -0.48503608703613565 | -0.32742347717285725 | 0.0 | 0.36682662963867685 | 0.7795695877075259 |
| A / average_speed_mps | 24 | -0.34114456176757807 | -0.17639780044555664 | 0.0 | 0.1805403232574463 | 0.3676695823669433 |
| A / l1_penalty_s | 24 | -0.17816405296325682 | -0.12718915939331055 | 0.0 | 0.08279955387115479 | 0.12967448234558104 |
| B / max_speed_kph | 24 | -1.2402713012695288 | -0.29556312561034304 | 0.0 | 0.6653114318847742 | 2.3797117996215786 |
| B / average_speed_mps | 24 | -0.3281821250915527 | -0.21054649353027344 | 0.0 | 0.1960277557373047 | 0.4782950401306152 |
| B / l1_penalty_s | 24 | -0.4108372688293457 | -0.10443782806396484 | 0.0 | 0.10216808319091797 | 0.14317002296447753 |
| C / max_speed_kph | 24 | -0.3869899749755916 | -0.32742347717285725 | 0.0 | 0.4565703392028766 | 2.397165298461911 |
| C / average_speed_mps | 24 | -0.26614627838134763 | -0.1733391284942627 | 0.0 | 0.2545449733734131 | 0.7202455520629882 |
| C / l1_penalty_s | 24 | -0.2500722885131836 | -0.07989764213562012 | 0.0 | 0.10093235969543457 | 0.20010919570922853 |

Real offsets cover repeated named records at the same exact venue, centered on each rider's own median; simulation offsets cover identical fixed-gate profiles across dry/rain and three seeds. No real names are mapped to invented ratings. Sample counts per real rider, distinct/repeated rider counts and original CSV hash remain in RiderRelativeEvidence. PGEE rank fields cannot validate reaction, 2 s physical speed, corner shape or impacts.

## Workload and exact correctness

| Complete-heat metric (failed captures excluded) | N | P10 | P25 | P50 | P75 | P90 |
| --- | --- | --- | --- | --- | --- | --- |
| A / ordinary standing start / HeatMilliseconds | 45 | 390.0795 | 397.6583 | 416.5027 | 535.2173 | 555.90768 |
| A / ordinary standing start / AllocatedBytes | 45 | 149419056.0 | 152347912.0 | 154293968.0 | 186402032.0 | 190626041.6 |
| A / ordinary rolling fixture / HeatMilliseconds | 21 | 67.2989 | 330.5185 | 345.8968 | 350.8708 | 377.8268 |
| A / ordinary rolling fixture / AllocatedBytes | 21 | 57907264.0 | 117759760.0 | 119624816.0 | 121591152.0 | 124161768.0 |
| A / extreme stress (excluded from ordinary rates) / HeatMilliseconds | 3 | 347.8277 | 348.29675 | 349.0785 | 351.32245 | 352.66882 |
| A / extreme stress (excluded from ordinary rates) / AllocatedBytes | 3 | 120178148.8 | 120428608.0 | 120846040.0 | 120940032.0 | 120996427.2 |
| B / ordinary standing start / HeatMilliseconds | 44 | 2940.94879 | 4043.711075 | 4446.97545 | 5076.799274999999 | 5686.96666 |
| B / ordinary standing start / AllocatedBytes | 44 | 258790752.0 | 273655334.0 | 288270772.0 | 307148686.0 | 317626197.6 |
| B / ordinary rolling fixture / HeatMilliseconds | 21 | 529.58 | 911.9204 | 979.3696 | 1235.8228 | 1277.7788 |
| B / ordinary rolling fixture / AllocatedBytes | 21 | 68514576.0 | 132927984.0 | 139863256.0 | 143277224.0 | 144169376.0 |
| B / extreme stress (excluded from ordinary rates) / HeatMilliseconds | 3 | 3395.70594 | 3441.06585 | 3516.6657 | 3625.47495 | 3690.7605 |
| B / extreme stress (excluded from ordinary rates) / AllocatedBytes | 3 | 194220473.6 | 194267408.0 | 194345632.0 | 197286896.0 | 199051654.4 |
| C / ordinary standing start / HeatMilliseconds | 44 | 1769.36504 | 3778.6922250000002 | 4672.2858 | 5390.9971000000005 | 6692.745590000001 |
| C / ordinary standing start / AllocatedBytes | 44 | 209062732.0 | 262706558.0 | 284376712.0 | 314881578.0 | 328776011.2 |
| C / ordinary rolling fixture / HeatMilliseconds | 21 | 555.8019 | 961.9678 | 1033.132 | 1303.0474 | 1317.2798 |
| C / ordinary rolling fixture / AllocatedBytes | 21 | 68524952.0 | 132929320.0 | 139873320.0 | 143316848.0 | 144202760.0 |
| C / extreme stress (excluded from ordinary rates) / HeatMilliseconds | 3 | 465.49858 | 472.83775 | 485.0697 | 486.87805000000003 | 487.96306 |
| C / extreme stress (excluded from ordinary rates) / AllocatedBytes | 3 | 78863507.2 | 78973876.0 | 79157824.0 | 79187472.0 | 79205260.8 |

| Config / population | Unique alternative projections | Actual verifications | Production resolutions | Narrow-phase evaluations |
| --- | --- | --- | --- | --- |
| B / ordinary standing start | 8966 | 1660 | 37296 | 247381898 |
| B / ordinary rolling fixture | 191 | 624 | 1420 | 22993819 |
| B / extreme stress (excluded from ordinary rates) | 375 | 102 | 1442 | 14390439 |
| C / ordinary standing start | 8617 | 1651 | 36132 | 240558043 |
| C / ordinary rolling fixture | 191 | 624 | 1420 | 22993819 |
| C / extreme stress (excluded from ordinary rates) | 60 | 102 | 297 | 1018237 |

Offline wall budget used 1960.8812953 s of 5400 s. Production timings include cheap capture/logging and allocations include worker threads; post-hoc native exposure, hashing and serialization are separate. They are machine-specific measurements, not a #61 speedup comparison. All 342 input reversals agree exactly; 23 offline unobserved checks agree. The six-case pilot additionally checks all six observer-free cases. All 16 named FullAudit reruns have exact Summary final-state parity, and their Summary hashes equal the batch. There are 43685 exact clock/distance checks, zero residuals and zero duplicated applied pair generations. The only recorded invariant witnesses are the 20 explicit production aborts. The reused-simulator test checks state isolation after contact stress. CI independently requires typed-hash baseline parity on both platforms, unchanged src/data and every historical golden/comparator. Green CI validates contracts and instrumentation; it cannot declare the P1 failed races successful.
