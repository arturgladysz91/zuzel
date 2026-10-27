# #141 — Autonomous acceleration and next-corner preparation

PROVISIONAL, default OFF. Base: #140 `932fc33ba20a8c96c2cd1a102957c9b297d412ea`.
Original engine, point-contact tyres, Box2D, track, mass/inertia, surface and guards remain unchanged.

The read-only audit in `results.json` precedes all new worlds. The throttle prior
sometimes decides a straight's ranking (full gas costs 1.307 points), but removing
it alone makes full gas win only 2/18 archived observations. Terminal yaw and
direction/geometric penalties also dominate. Replacing terminal yaw with its
existing mean and removing the prior makes full gas win 3/18. This is diagnostic,
not native evidence. The geometry remainder closes the saved score algebraically;
it is not independent validation. In archived native straights, requesting a
reduction precedes activation by .24s and about 5.0–5.2 physical metres. Unrequested
second actions were often reconsidered; already issued pending commands survive.

One prototype reuses the original actions and weights, removes the throttle prior
and uses mean yaw. It keeps the best root per gas and original #134A fallback.
Each receives one physical .9s coast/recovery steering forecast after H09. Beyond
1.8s, attained offset, measured coast deceleration, reaction, steering travel and
response times provide an uncertified preparation estimate. Only first requests
enter the original reaction queue. There is no commanded path or gas schedule.

Frozen gates: all four legal when A is legal, no new DNF; in at least two physically
different families +3 km/h at +40m and ≥.10s improvement over the same full scope;
mean decision CPU B/A ≤1.5 and B ≤.20 CPU-s. Two D0 A/B3s preflights precede
D0/D1 diagnosis, then a committed H0/H1 confirmation lock. No tuning on H cases.
Each full world continues through the next bend and another62m; D1/H1 include
bend3. Maximum20 worlds/400 CPU-s. Stop if preflight guards or CPU fail.

Run only via `dotnet run -c Release --project tools/autonomous-acceleration --
--enable-autonomous-acceleration141 docs/experiments/autonomous-acceleration/results.json <stage>`.
Stages: `freeze`, `preflight`, `diagnostic`, `confirmation`. The two report files
are the entire exported evidence; code/tests remain in the PR. Results pending.
