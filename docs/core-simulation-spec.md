# Core simulation specification

This document defines the binding invariants of the speedway manager simulation.

## Scope

The core simulates decisions and believable consequences. It does not integrate a complete motorcycle physics model. Rendering, economy, league rules and persistence remain outside `CoreSim`.

## Determinism

Given identical domain state, options and random seed, a heat must produce identical classification, typed surface changes and text logs. Randomness may create uncertainty but cannot be hidden in global or time-based state.

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

## Riders and decisions

- Skills use a `0..100` scale: start, speed, slide control, track reading, pair riding and adaptability.
- Style uses normalized preferences: risk, lane changes, outside line and setup independence.
- Morale is mutable and separate from physical form. It changes stability and follows results or incidents.
- A decision model evaluates local lanes. Track reading controls observation quality; style controls preferences; occupied space is penalized.
- Lane evaluation uses projected route time (bend plus following straight), not raw maximum speed. This lets a clean outside route beat a worn inside route without making the outside universally superior.
- Rider-to-rider contact depends on the time gap, segment, surface and control skills. Contact in `TurnMiddle` is more dangerous than on a straight.

## Setup

- Setup is a trade-off and cannot represent a more expensive or universally faster motorcycle.
- A manager supplies advice with a confidence level.
- Trust, confidence and rider independence determine whether the advice is accepted, adjusted or ignored.

## Result and player information

The core returns factual internal results and logs. Segment logs distinguish entry speed, speed after the physical constraint and final exit speed. A change of running order between two active riders creates a typed overtake event; a retirement is not an overtake. Every completed lap creates a typed order snapshot with gaps to the active leader.

A starting-gate balance report must rotate the same four rider profiles evenly through gates 1..4. This prevents rider strength from being mistaken for gate advantage. Large diagnostic batches may disable log capture, but this must not change track evolution or the simulated classification.

A gameplay layer is responsible for hiding exact values and exposing observations, feedback and uncertainty to the player.
