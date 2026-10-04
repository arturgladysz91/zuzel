# Trajectory platform audit

Diagnostic run [37228136668](https://github.com/arturgladysz91/zuzel/actions/runs/37228136668)
ran the identical all-candidate workload on both Windows and Ubuntu, against
audited production `9ff730c5b1b2cfca739c3ead2ccc2760be1b7c71` and current production
`6ab245cec42bb77d88e8417cca55b7b0508f334c` (diagnostic commit `c8dae079`).

**Case A:** the raw mismatch predates the performance refactor. Both Windows
captures produce `F20AE8E3895CF54625D549DB127BA1C8E2168A7AA3549ED94F305A04D89C3D18`;
both Ubuntu captures produce `9F7723F3DDF4DCACF6B11846EA9E5C4ABF37ECB2B0863B11657E80251AF2BB17`.

The first different byte is offset 1, immediately after `[`: Windows writes
`0x0D 0x0A`; Ubuntu writes `0x0A`. The Windows document has 337572 internal
CRLF sequences, accounting exactly for the 337572-byte size difference
(11652155 versus 11314583). Removing only document CR bytes makes the captures
byte-identical. The explicit terminal LF was already common.

There is **no first divergent JSON field or production numerical operation**.
All 117031 float/double observations have identical IEEE bits, including
candidate costs, achieved anchors, speeds, final rider values and raw surface
cells. Numeric differences = 0; max float/double ULP = 0; absolute/relative
delta = 0. Behavior differences = 0: selected intents/current targets, canonical
candidate and exit-speed ordering, completed routes, outcomes, requested/reached
anchors, heat decisions and classification match. The semantic diagnostic hash
is `D1F82B308E2C1C9E75989F760FAF764A8218EA80C32E8171EB6E56F96F831D84`.
The minimum winner margin is zero in exact ties; numeric normalization therefore
has no justification and is not introduced.

The root cause is the host newline used by .NET 8's indented JSON serializer,
after all production calculations have completed. `Capture()` now freezes the
original audited CRLF document plus terminal LF on both hosts. The single
original expected SHA remains unchanged. Same-platform Full/Lean bit parity and
cross-platform exact numeric/behavioral capture remain required; no OS-specific
golden, tolerance, rounding or physics change is needed.

`tools/trajectory-audit/compare.py` reports structural differences with context,
property path, numeric deltas and C#-captured IEEE bits. Diagnostic checkouts are
isolated from normal runtime. The original before/after captures remain in the
linked Actions artifacts.
