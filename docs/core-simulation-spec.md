# Core simulation specification

This document defines the binding invariants of the speedway manager simulation.

## Scope

The core simulates decisions and believable consequences. It does not integrate a complete motorcycle physics model. Rendering, economy, league rules and persistence remain outside `CoreSim`.

## Determinism

Given identical domain state, options and random seed, a heat must produce identical classification, typed surface changes and text logs. Randomness may create uncertainty but cannot be hidden in global or time-based state. Race draws are addressed by stable domain keys; changing the order of riders in the input collection must not change which draw belongs to which rider.

Every segment uses snapshot/propose/resolve/commit processing. Decisions, movement and battle intent are calculated from one immutable snapshot of all riders. No proposal may observe another rider's uncommitted movement or same-segment lane wear.

## Track

- A track is an ordered list of `TurnEntry`, `TurnMiddle`, `TurnExit` and `Straight` segments.
- Every segment has five local reference lanes numbered `0..4` from inside to outside.
- The chosen lane is discrete; `LateralPosition` is continuous and cannot jump directly across the track.
- Every segment/lane cell stores base grip, ruts and moisture. `EffectiveGrip` derives usable grip from all three values.
- Weather changes moisture unevenly. Rider passages wear the used lane and nearby material. Manager work can grade, water or pack a selected area.

## Physical constraints

- A turn has a safe speed dependent on lane, surface, slide control, morale and setup fit.
- Wider lanes allow a slightly higher speed but also add distance. On an equal surface, the projected full-lap time of the innermost and outermost reference lanes must stay within 2%; no gate may be an automatic winning strategy.
- Exceeding the safe speed must cause braking, running wide or a crash.
- A rider cannot run wider than lane 4; an unresolved high-speed run-wide there becomes a crash.
- A straight preserves speed. It cannot create a passing advantage by itself; it only carries an advantage created at corner exit and positions riders for the next turn.
- Safe turn speed is tied to lane radius and effective grip. An outside route can carry more speed but has a longer arc.
- An advanced run-wide consequence loses speed. The legacy neutral `SegmentPhysics.Apply(segment, lane, speed)` overload keeps its original compatibility contract.

## Riders and decisions

- Skills use a `0..100` scale: start, speed, slide control, track reading, pair riding and adaptability.
- Style uses normalized preferences: risk, lane changes, outside line and setup independence.
- Morale is mutable and separate from physical form. It changes stability and follows results or incidents.
- A decision model evaluates local lanes. Track reading controls observation quality; style controls preferences; occupied space is penalized.
- Lane evaluation uses projected route time (bend plus following straight), not raw maximum speed. This lets a clean outside route beat a worn inside route without making the outside universally superior.
- Rider position includes lap, segment, progress within the segment, local lane, continuous lateral position, speed, gap to the rider ahead and nearby occupied lanes.
- An attack requires a small gap plus a speed advantage or corner-exit advantage. It must use a reachable free lane. Success follows projected physical progress, never a random passing bonus.
- A defender with sufficient reading, pair-riding and control can close a reachable lane. An occupied or closed lane blocks the attack.
- A failed attack costs speed and time. Higher risk tolerance increases attack frequency and the probability of contact; contact in `TurnMiddle` is more dangerous than on a straight.
- Start performance combines start skill (reaction), setup gearing (launch torque), starting-field grip and the route to the first turn. The model does not force equal gate win rates.

## Setup

- Setup is a trade-off and cannot represent a more expensive or universally faster motorcycle.
- A manager supplies advice with a confidence level.
- Trust, confidence and rider independence determine whether the advice is accepted, adjusted or ignored.

## Result and player information

The core returns factual internal results and logs. Segment logs distinguish entry speed, speed after the physical constraint and final exit speed. Typed events cover starts, attacks, defence, blocks, failures, contacts, running wide, lane changes and overtakes. A change of running order between two active riders creates a typed overtake event; a retirement is not an overtake. Every segment creates an order/gap snapshot, and every completed lap retains the existing lap snapshot.

A starting-gate balance report must rotate the same four rider profiles evenly through gates 1..4. This prevents rider strength from being mistaken for gate advantage. Large diagnostic batches may disable log capture, but this must not change track evolution or the simulated classification.

A gameplay layer is responsible for hiding exact values and exposing observations, feedback and uncertainty to the player.
