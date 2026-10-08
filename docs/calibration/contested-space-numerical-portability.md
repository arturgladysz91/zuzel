# Pre-existing contested-space numerical portability issue (#62)

Tracked separately from the performance optimization in Draft PR #61:
[issue #62](https://github.com/arturgladysz91/zuzel/issues/62).

Original merged main `5989301192565f6a265d53a2db12c7d00c94fedc` already has 22
Windows/Ubuntu-divergent cases in the complete contested-performance audit.
[Run 196](https://github.com/arturgladysz91/zuzel/actions/runs/37747518388) captures
108 cases and 3,860,491 leaves, including 3,121,627 typed IEEE values per OS.
Each optimized OS reproduces its own original-main output exactly. These
differences precede the optimization and require a separate numerical review.

## Affected cases

Both rider-order variants below are separate captured cases; all other cases
remain part of the complete comparison too.

| Fixture | Exact case names |
| --- | --- |
| Clean cutback | `C-cutback-clean/True/False`, `C-cutback-clean/True/True` |
| Poor cutback | `C-cutback-poor/True/False`, `C-cutback-poor/True/True` |
| Exit crossing | `F-exit-cross/True/False`, `F-exit-cross/True/True` |
| Four-rider first bend | `G-four-first-bend/True/False`, `G-four-first-bend/True/True` |
| Trapped edge | `edge-trapped/True/False`, `edge-trapped/True/True` |
| I / Dry / seed 19 | `heat/I/Dry/19/False`, `heat/I/Dry/19/True` |
| I / Dry / seed 7 | `heat/I/Dry/7/False`, `heat/I/Dry/7/True` |
| I / LightRain / seed 19 | `heat/I/LightRain/19/False`, `heat/I/LightRain/19/True` |
| I / LightRain / seed 7 | `heat/I/LightRain/7/False`, `heat/I/LightRain/7/True` |
| Ownership | `ownership` |
| Real bridge | `real-bridge/True/False`, `real-bridge/True/True` |
| Shadow real bridge | `shadow/real-bridge` |

## Exact evidence and reproduction

For `C-cutback-clean/True/False`, the field
`root[0].Interaction.Episodes[0].Candidates[7].MinimumSeparationMeters` is
`double:3FFF3C829310C52A` on Windows and `double:3FFF3C829310C522` on Ubuntu
(eight ULPs). Raw captures are byte-identical before/after within each OS.
Nine leaves differ in that case; 77 differ in the inspected `heat/I/Dry/7/False`
case, inside interaction geometry/diagnostics. These are examples, not the full
divergence map. Existing strict cross-platform goldens pass.

Use the same `tools/contested-performance` harness on both platforms, built once
against the original-main checkout via its absolute `CoreSimRoot` and once against
the optimized checkout. Run `capture OUTPUT --raw` for both versions. CI collects
every sidecar, verifies each raw file against its manifest SHA256 and leaf/IEEE
counts, and runs:

```text
python tools/contested-performance/compare.py results/contested-performance
python tools/contested-performance/compare.py captures --platforms
```

The second command expects `captures/determinism-windows-latest/contested-performance`
and `captures/determinism-ubuntu-latest/contested-performance`. Its
`contested-performance-comparison.json`, published in the `platform-comparison`
artifact of the final #61 CI run, retains **both complete maps**. Each entry includes
case, full field path, Windows type/value/bits and Ubuntu type/value/bits. Nothing
is rounded or truncated. The PR description links the exact final-head run.

## Investigation scope and acceptance

Isolate the first differing arithmetic operation on original main and establish
a separately reviewed numerical portability contract before proposing a numerical
change. The cause is not yet isolated: the observations alone do not establish
libm, JIT or hardware as the source. Preserve existing strict platform goldens and
independently review any proposed effect on physics or behavior.

PR #61 must preserve original-main values independently on each OS and the entire
before/after platform divergence map. Introduced, removed or changed divergences
fail. This issue does not permit ignoring the 22 cases, rounding floats, adding
tolerances or modifying production physics in the performance PR. It does not
authorize changes to PR #59 or any merge.
