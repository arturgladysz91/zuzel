# #56C2 physical contact consequences and recovery

Status: implementation in progress; Draft, independent review required. Calibration decision recorded before consequence code.

## Prerequisite

PR #58 merged on 2026-10-07 at 20:45:15 UTC as `5989301192565f6a265d53a2db12c7d00c94fedc`. Its tree is exactly `400eee32bc10d4df001fc2c8d7243052bdd81ab2`, equal to reviewed head `51d07b8762a7925aeb9f8a31ea8018a29d2dc853`. Final-head Actions run 187 (`37534295406`) passed all nine jobs, including Windows/Ubuntu determinism and cross-platform comparison. This branch starts at that merged main.

## Applied severity calibration

Retain centralized boundaries **0.35 / 0.70 / 1.05 / 1.55** after reviewing the complete #56C1 evidence. No impulse, reserve, threshold or frozen #56C1 evidence changes are needed. This is an explicit applied design decision, still a synthetic calibration rather than measured motorcycle recovery capability.

Gentle parallel brush has ratio 0.0505; moderate side contact 1.0094 and rear closing 0.9158 sit in LostRhythm; crossing rider A is 4.7241. Center versus lever and chassis versus bar show additional geometric yaw demand rather than component bonuses. Technique 20/50/80 produces 1.1919/1.0094/0.8754; Strength produces 1.1503/1.0094/0.8993; Condition 1/0.7/0.4 produces 1.0094/1.0546/1.1041. One-rider mass 60/67.5/75 kg produces 1.0363/1.0094/0.9839. Grip changes reserve monotonically. The three-rider squeeze retains aggregate demand despite canceled net velocity (middle ratio 1.4278, MajorSave); four-rider and disconnected frontiers retain independent, simultaneous aggregates.

The ordinary synthetic population (2,916 rows) has ratio P10/P25/P50/P75/P90/P95/P99/MAX = .041/.078/.258/.651/1.233/1.582/2.020/2.388 and labels **1683 Brush, 576 Disturbed, 267 LostRhythm, 243 MajorSave, 147 Crash**. The deliberately severe population (1,458 rows) has 0/0/3/147/1308 respectively, with ratio median 3.588 and maximum 12.592. These are equal-weight synthetic grids, not frequencies of racing contacts. Crash is confined to the high-demand tail of ordinary settings; moderate reference contacts recover, MajorSave has a distinct useful region, and severe crossing exceeds recovery. No thresholds were fitted to a crash percentage or legacy outcomes.

Continuous recoverable control loss is `0.8 * (SeverityRatio / CrashBoundary)^2`, bounded to [0,0.8]. At the retained boundaries it is approximately .041/.163/.367/.8. Drive and voluntary lateral availability are `1 - ControlLoss01`. Brush stores no recovery; the other recoverable classes store one active step. These parameters are centralized in the consequence layer. Crash is terminal. Controlled below/at/above boundary evidence and production recovery measurements will accompany implementation.

Legacy comparison remains diagnostic. The unchanged #56C1 twelve-case sample contains 27 NoLegacyOccurrence, 2 LegacyLostRhythm, 1 LegacyCrash and 12 NotAuthorized rider observations. Matching those rates is not a calibration objective.
