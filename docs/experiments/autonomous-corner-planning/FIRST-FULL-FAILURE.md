# First complete autonomous attempt: negative result, diagnosed before retry

`autonomous146-first` used the continuous standing-start world, no saved commands. It abstained at 12.0 s after completing the first bend, with no confirmed crossing (minimum inner 0.204562 m, outer 2.865542 m). This is DNF, not success.

The plan at 11.8 s predicted common-goal arrival at 11.971896 s and a legal 0.8 s preflight (minimum gap 2.865557 m, beta maximum 21.610935 degrees). At 12.0 s the actual state is reference 192.185317 m, 101.847485 km/h, beta 8.068 degrees, yaw 0.776512 rad/s. The previous goal has been passed; the next maneuver has no certified candidate. The newly generated K1 seed predicts a boundary violation, minimum gap -0.015418 m. All 27 first-coordinate alternatives fail a complete continuation. No unsafe command is submitted.

This is not native forecast divergence: maximum review-to-review position error is 0.00002413 m, yaw error 0.00002736 rad/s; all active actions and pending queues match. The complete selected sequences, actual submissions and activations are retained in native/autonomous146-first-plans.json.gz and analysis.json.

Two limitations are established. First, the objective ends three metres before the next bend without requiring a viable next maneuver; time improvements at 8.8, 9.6 and 10.4 s therefore allow excessive terminal speed. Second, the search keeps only roots whose *old tail* already completes legally, so an empty first layer prevents evaluation of a two- or three-coordinate repair. This proves a search restriction, not physical impossibility of recovery.

The next revision may retain bounded unsafe *intermediate search nodes*, ranked by envelope violation and clearance, to seek a final legal repaired sequence. The execution veto remains unchanged. This does not solve the terminal viability defect; later results must report whether reactive repair compensates for it. No new full attempt is authorized internally until this diagnosis exists.
