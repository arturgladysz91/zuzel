# Combined-grip execution-reserve follow-up — analysis only

Same Draft PR #50; reviewed starting HEAD `cea5ab20bb04796b81048e36a5d509771c355024`, main `58771bac635d7d4962e11aa3ae5ad37a2d0c8d94`. No production physics/calibration change. The q law, correction, envelope, spline, sampling, objective and search tolerances are frozen.

## Reserve semantics and limits

This uses the existing per-replay-interval execution reserve exactly: remaining budget = max(0, lateralCapacity(stepTime) - reserve) - required lateral movement; invalid below -0.0001 m. Straight reposition uses the same rule. Actual headroom below is capacity minus movement BEFORE subtracting reserve. It is not the adjusted remaining budget or a tyre-grip margin.

The clamp preserves zero-motion feasibility when an interval capacity is smaller than reserve. Short binding-constraint intervals and final remainders make this test mesh-dependent. In particular, a valid ConstantInner can have actual minimum headroom below the requested reserve. Thus ‘survives N cm’ refers to this existing withheld-budget gate, not a guaranteed mesh-independent N cm physical margin. Do not infer that a return to inner disproves combined-grip physics. The search requests 106 starts / up to 48 valid diverse starts; fewer eligible starts are reported, without changing the search.

## Eight-case decision table

Free-inner delta is winner sector minus inner sector (negative is faster); exit delta is winner minus inner. Winner is the best unrestricted candidate, including constant lines.

| q | required reserve m | winner | free-inner delta s | extra path m | exit delta m/s | actual min headroom m | interpretation |
|---:|---:|---|---:|---:|---:|---:|---|
| 0.500000000 | 0.000000000 | FCT-AE7832278759 | -0.009528160 | 1.065055847 | 0.274202347 | 0.000039342 | zero-reserve crossover |
| 0.500000000 | 0.020000000 | ConstantInner / FCT-B843ADDB373E | 0.000000000 | 0.000000000 | 0.000000000 | 0.019414285 | returns to ConstantInner; boundary-dependent; search uncertain |
| 0.500000000 | 0.050000001 | ConstantInner / FCT-B843ADDB373E | 0.000000000 | 0.000000000 | 0.000000000 | 0.019414285 | returns to ConstantInner; boundary-dependent; search uncertain |
| 0.500000000 | 0.100000001 | ConstantInner / FCT-B843ADDB373E | 0.000000000 | 0.000000000 | 0.000000000 | 0.019414285 | returns to ConstantInner; boundary-dependent; search uncertain |
| 0.750000000 | 0.000000000 | FCT-AF9E514C9A01 | -0.022162437 | 1.094551086 | 0.394069672 | 0.000064534 | zero-reserve crossover |
| 0.750000000 | 0.020000000 | ConstantInner / FCT-B843ADDB373E | 0.000000000 | 0.000000000 | 0.000000000 | 0.019414524 | returns to ConstantInner; boundary-dependent; search uncertain |
| 0.750000000 | 0.050000001 | ConstantInner / FCT-B843ADDB373E | 0.000000000 | 0.000000000 | 0.000000000 | 0.019414524 | returns to ConstantInner; boundary-dependent; search uncertain |
| 0.750000000 | 0.100000001 | ConstantInner / FCT-B843ADDB373E | 0.000000000 | 0.000000000 | 0.000000000 | 0.019414524 | returns to ConstantInner; boundary-dependent; search uncertain |

## Timing, geometry and actual margins

| q | reserve m | inner sector/corner/exit s,s,m/s | winner sector/corner/exit s,s,m/s | min R m | max curvature 1/m | actual / adjusted min headroom m | shortest replay step m | best nonconstant delta s | original zero-reserve winner validity |
|---:|---:|---|---|---:|---:|---|---:|---|---|
| 0.500000000 | 0.000000000 | 6.758304596/4.220424652/22.628875732 | 6.748776436/4.233346462/22.903078079 | 30.295263290 | 0.033008460 | 0.000039342/0.000039342 | 0.000587463 | -0.009528160 | Valid |
| 0.500000000 | 0.020000000 | 6.758304596/4.220424652/22.628875732 | 6.758304596/4.220424652/22.628875732 | 31.000000000 | 0.032258064 | 0.019414285/0.000000000 | 0.231704712 | 0.004131794 | LateralExecutionConstraint |
| 0.500000000 | 0.050000001 | 6.758304596/4.220424652/22.628875732 | 6.758304596/4.220424652/22.628875732 | 31.000000000 | 0.032258064 | 0.019414285/0.000000000 | 0.231704712 | none found | LateralExecutionConstraint |
| 0.500000000 | 0.100000001 | 6.758304596/4.220424652/22.628875732 | 6.758304596/4.220424652/22.628875732 | 31.000000000 | 0.032258064 | 0.019414285/0.000000000 | 0.231704712 | none found | LateralExecutionConstraint |
| 0.750000000 | 0.000000000 | 6.831285477/4.241098881/22.005310059 | 6.809123039/4.252175808/22.399379730 | 30.295328140 | 0.033008389 | 0.000064534/0.000064534 | 0.000959396 | -0.022162437 | Valid |
| 0.750000000 | 0.020000000 | 6.831285477/4.241098881/22.005310059 | 6.831285477/4.241098881/22.005310059 | 31.000000000 | 0.032258064 | 0.019414524/0.000000000 | 0.231704712 | 0.004117012 | LateralExecutionConstraint |
| 0.750000000 | 0.050000001 | 6.831285477/4.241098881/22.005310059 | 6.831285477/4.241098881/22.005310059 | 31.000000000 | 0.032258064 | 0.019414524/0.000000000 | 0.231704712 | none found | LateralExecutionConstraint |
| 0.750000000 | 0.100000001 | 6.831285477/4.241098881/22.005310059 | 6.831285477/4.241098881/22.005310059 | 31.000000000 | 0.032258064 | 0.019414524/0.000000000 | 0.231704712 | none found | LateralExecutionConstraint |

| q | reserve m | winner controls m |
|---:|---:|---|
| 0.500000000 | 0.000000000 | `0.52211154,0.61745787,0.60453284,0.4882735,0.31308693,0.14588839,0.050542116,0.063467115,0.17972651,0.354913,0.58461154` |
| 0.500000000 | 0.020000000 | `0,0,0,0,0,0,0,0,0,0,0` |
| 0.500000000 | 0.050000001 | `0,0,0,0,0,0,0,0,0,0,0` |
| 0.500000000 | 0.100000001 | `0,0,0,0,0,0,0,0,0,0,0` |
| 0.750000000 | 0.000000000 | `0.52211154,0.61745787,0.60453284,0.4882735,0.31308693,0.14588839,0.050542116,0.063467115,0.17972651,0.354913,0.77211154` |
| 0.750000000 | 0.020000000 | `0,0,0,0,0,0,0,0,0,0,0` |
| 0.750000000 | 0.050000001 | `0,0,0,0,0,0,0,0,0,0,0` |
| 0.750000000 | 0.100000001 | `0,0,0,0,0,0,0,0,0,0,0` |

### Is the small raw margin an almost exhausted execution budget?

For the zero-reserve winners, show the interval with smallest raw headroom and the maximum used fraction across ALL corner intervals. Used fraction = required lateral movement / allowed movement. This is read-only diagnosis of the existing samples, not a new reserve gate or normalized physical parameter.

| q | min-headroom progress | step distance/time m/s | allowed/required/headroom m | used fraction at min headroom | maximum used fraction | max-use progress |
|---:|---:|---|---|---:|---:|---:|
| 0.500000000 | 0.779293895 | 0.000587463/0.000026146 | 0.000047716/0.000008374/0.000039342 | 0.175505251 | 0.305487007 | 0.999755859 |
| 0.750000000 | 0.191401422 | 0.000959396/0.000038595 | 0.000070435/0.000005901/0.000064534 | 0.083776973 | 0.588867545 | 0.997802794 |

## Search evidence

| q | reserve m | starts/refined (initial valid) | objective | geometry | independent near starts | top-three closed | single/pair residual s | valid/evaluated | boundary/self intersection/non smooth/lateral/run wide/crash/reposition/non traversable |
|---:|---:|---|---|---|---:|---|---|---|---|
| 0.500000000 | 0.000000000 | 106/48 (103) | YES | YES | 4 | YES | 0.000000000/0.000000000 | 6040/9560 | 1237/0/0/1098/1090/93/2/0 |
| 0.500000000 | 0.020000000 | 106/5 (5) | NO | YES | 2 | YES | 0.000000000/0.000000000 | 63/1008 | 309/0/0/570/61/5/0/0 |
| 0.500000000 | 0.050000001 | 106/5 (5) | NO | YES | 2 | YES | 0.000000000/0.000000000 | 57/1008 | 309/0/0/583/54/5/0/0 |
| 0.500000000 | 0.100000001 | 106/5 (5) | NO | YES | 2 | YES | 0.000000000/0.000000000 | 56/1008 | 309/0/0/596/42/5/0/0 |
| 0.750000000 | 0.000000000 | 106/48 (103) | YES | YES | 3 | YES | 0.000000000/0.000000000 | 6344/10163 | 1354/0/0/1222/1148/93/2/0 |
| 0.750000000 | 0.020000000 | 106/5 (5) | NO | YES | 2 | YES | 0.000000000/0.000000000 | 63/1008 | 309/0/0/570/61/5/0/0 |
| 0.750000000 | 0.050000001 | 106/5 (5) | NO | YES | 2 | YES | 0.000000000/0.000000000 | 57/1008 | 309/0/0/583/54/5/0/0 |
| 0.750000000 | 0.100000001 | 106/5 (5) | NO | YES | 2 | YES | 0.000000000/0.000000000 | 56/1008 | 309/0/0/596/42/5/0/0 |

Top-three closure details (unchanged 0.002 s tolerance):

| q | reserve m | family | closure moves/passes | single/expanded single/pair residual s | stable |
|---:|---:|---|---|---|---|
| 0.500000000 | 0.000000000 | Low-phase-neutral | 0/1 | 0.000000000/0.000000000/0.000000000 | YES |
| 0.500000000 | 0.000000000 | Flat-0 | 0/1 | 0.000000000/0.000000000/0.000000000 | YES |
| 0.500000000 | 0.000000000 | Flat-25 | 0/1 | 0.000000000/0.000000000/0.000000000 | YES |
| 0.500000000 | 0.020000000 | Flat-0 | 0/1 | 0.000000000/0.000000000/0.000000000 | YES |
| 0.500000000 | 0.020000000 | Flat-25 | 0/1 | 0.000000000/0.000000000/0.000000000 | YES |
| 0.500000000 | 0.020000000 | Flat-50 | 0/1 | 0.000000000/0.000000000/0.000000000 | YES |
| 0.500000000 | 0.050000001 | Flat-0 | 0/1 | 0.000000000/0.000000000/0.000000000 | YES |
| 0.500000000 | 0.050000001 | Flat-25 | 0/1 | 0.000000000/0.000000000/0.000000000 | YES |
| 0.500000000 | 0.050000001 | Flat-50 | 0/1 | 0.000000000/0.000000000/0.000000000 | YES |
| 0.500000000 | 0.100000001 | Flat-0 | 0/1 | 0.000000000/0.000000000/0.000000000 | YES |
| 0.500000000 | 0.100000001 | Flat-25 | 0/1 | 0.000000000/0.000000000/0.000000000 | YES |
| 0.500000000 | 0.100000001 | Flat-50 | 0/1 | 0.000000000/0.000000000/0.000000000 | YES |
| 0.750000000 | 0.000000000 | Low-phase-neutral | 0/1 | 0.000000000/0.000000000/0.000000000 | YES |
| 0.750000000 | 0.000000000 | Flat-25 | 2/3 | 0.001096725/0.001994133/0.000000000 | YES |
| 0.750000000 | 0.000000000 | Halton-001 | 3/4 | 0.000823975/0.000823975/0.000858307 | YES |
| 0.750000000 | 0.020000000 | Flat-0 | 0/1 | 0.000000000/0.000000000/0.000000000 | YES |
| 0.750000000 | 0.020000000 | Flat-25 | 0/1 | 0.000000000/0.000000000/0.000000000 | YES |
| 0.750000000 | 0.020000000 | Flat-50 | 0/1 | 0.000000000/0.000000000/0.000000000 | YES |
| 0.750000000 | 0.050000001 | Flat-0 | 0/1 | 0.000000000/0.000000000/0.000000000 | YES |
| 0.750000000 | 0.050000001 | Flat-25 | 0/1 | 0.000000000/0.000000000/0.000000000 | YES |
| 0.750000000 | 0.050000001 | Flat-50 | 0/1 | 0.000000000/0.000000000/0.000000000 | YES |
| 0.750000000 | 0.100000001 | Flat-0 | 0/1 | 0.000000000/0.000000000/0.000000000 | YES |
| 0.750000000 | 0.100000001 | Flat-25 | 0/1 | 0.000000000/0.000000000/0.000000000 | YES |
| 0.750000000 | 0.100000001 | Flat-50 | 0/1 | 0.000000000/0.000000000/0.000000000 | YES |

## Powered exit: like-progress winner / inner

Shown for every faster nonconstant winner. Potential positive propulsion uses exact spline curvature and profile speed at the stated progress, independently of controller label. It is distinct from an actual negative scrub request; controller state is reported alongside it.

| q | reserve m | progress | speed m/s | curvature 1/m | u | factor | requested positive N | usable positive N | controller winner/inner |
|---:|---:|---:|---|---|---|---|---|---|---|
| 0.500000000 | 0.000000000 | 0.600000024 | 21.964874268/21.641086578 | 0.031006604/0.032258064 | 0.994524956/1.004385591 | 0.710957170/0.703992069 | 70.166465759/70.756881714 | 70.166465759/70.756881714 | Drive/Drive |
| 0.500000000 | 0.000000000 | 0.699999988 | 22.211473465/21.899242401 | 0.030974116/0.032258067 | 1.015915751/1.028491139 | 0.695670605/0.686369419 | 209.150268555/210.858306885 | 209.150268555/210.858306885 | Drive/Drive |
| 0.500000000 | 0.000000000 | 0.800000012 | 22.517581940/22.219881058 | 0.031643268/0.032258064 | 1.066666961/1.058828950 | 0.656590223/0.662903249 | 311.213623047/313.656433105 | 210.226135254/213.913421631 | Drive/Drive |
| 0.500000000 | 0.000000000 | 0.899999976 | 22.723253250/22.438465118 | 0.031164652/0.032258067 | 1.069811702/1.079763532 | 0.654027164/0.645798206 | 318.442321777/320.846496582 | 208.269927979/207.202087402 | Drive/Drive |
| 0.500000000 | 0.000000000 | 1.000000000 | 22.903078079/22.628875732 | 0.031670362/0.032258064 | 1.104446650/1.098166704 | 0.624578834/0.630091190 | 316.924224854/319.239044189 | 197.944168091/201.149719238 | Drive/Drive |
| 0.750000000 | 0.000000000 | 0.600000024 | 21.970430374/21.641086578 | 0.031067571/0.032258064 | 0.996984601/1.004385591 | 0.504496038/0.493363231 | 70.156333923/70.756881714 | 70.156333923/70.756881714 | Drive/Drive |
| 0.750000000 | 0.000000000 | 0.699999988 | 22.190532684/21.861537933 | 0.030747054/0.032258067 | 1.006567717/1.024952650 | 0.490016401/0.460547566 | 209.264831543/211.064559937 | 158.245666504/150.008132935 | Drive/Drive |
| 0.750000000 | 0.000000000 | 0.800000012 | 22.263217926/21.964982986 | 0.032483749/0.032258064 | 1.070399880/1.034675241 | 0.375077426/0.443942934 | 313.300811768/315.748016357 | 120.897178650/144.212036133 | Drive/Drive |
| 0.750000000 | 0.000000000 | 0.899999976 | 22.328754425/21.987838745 | 0.028069712/0.032258067 | 0.930402756/1.036829829 | 0.592252493/0.440156817 | 321.772674561/324.650695801 | 190.570678711/142.897216797 | Drive/Drive |
| 0.750000000 | 0.000000000 | 1.000000000 | 22.399379730/22.005310059 | 0.031510249/0.032258064 | 1.051060796/1.038478017 | 0.414069265/0.437232822 | 321.176452637/324.503173828 | 132.989303589/141.883438110 | Drive/Drive |

## Maximum-curvature correction diagnostic

For each q choose the largest reserve retaining a faster nonconstant winner; otherwise show the 10 cm inner winner plus the zero-reserve free winner. Peak is the exact maximum among existing geometry samples. u/factor/request use the actual containing replay interval's force-evaluation state; its midpoint curvature is also shown, avoiding attribution of a controller label to a different force sample.

| q | reserve m | subject | peak progress / geometry k | interval progress / k | controller | u | factor | signed actual request / usable N |
|---:|---:|---|---|---|---|---:|---:|---|
| 0.500000000 | 0.100000001 | ConstantInner | 0.017089844/0.032258067 | 0.015402094/0.032258064 | Neutral | 1.502894402 | 0.000000000 | 0.000000000/0.000000000 |
| 0.500000000 | 0.000000000 | free winner | 0.100097656/0.033008460 | 0.095416658/0.032948289 | Correction/carry | 1.453576684 | 0.000000000 | -369.199981689/-369.199981689 |
| 0.750000000 | 0.100000001 | ConstantInner | 0.017089844/0.032258067 | 0.015402094/0.032258064 | Neutral | 1.502894402 | 0.000000000 | 0.000000000/0.000000000 |
| 0.750000000 | 0.000000000 | free winner | 0.100097656/0.033008389 | 0.095416665/0.032948215 | Correction/carry | 1.453575492 | 0.000000000 | -369.199981689/-369.199981689 |

No controller-label discontinuity or capacity disappearance was detected. However, effective correction/scrub remains outside the positive-propulsion combined-force allocation, so a physically meaningful correction-phase exploitation cannot yet be excluded. ‘Tighter while scrubbing → opens before powered exit’ may be valid speedway behaviour; it is not proven physically sound by this allocation model. No braking traction ellipse or scrub force decomposition is added.

Capacity check q=0.500000000, reserve=0.000000000: 1943 intervals, missing 0, maximum/transition residual 0.000000000/0.000000000 N.
Capacity check q=0.500000000, reserve=0.020000000: 100 intervals, missing 0, maximum/transition residual 0.000000000/0.000000000 N.
Capacity check q=0.500000000, reserve=0.050000001: 100 intervals, missing 0, maximum/transition residual 0.000000000/0.000000000 N.
Capacity check q=0.500000000, reserve=0.100000001: 100 intervals, missing 0, maximum/transition residual 0.000000000/0.000000000 N.
Capacity check q=0.750000000, reserve=0.000000000: 1943 intervals, missing 0, maximum/transition residual 0.000000000/0.000000000 N.
Capacity check q=0.750000000, reserve=0.020000000: 100 intervals, missing 0, maximum/transition residual 0.000000000/0.000000000 N.
Capacity check q=0.750000000, reserve=0.050000001: 100 intervals, missing 0, maximum/transition residual 0.000000000/0.000000000 N.
Capacity check q=0.750000000, reserve=0.100000001: 100 intervals, missing 0, maximum/transition residual 0.000000000/0.000000000 N.

The zero-reserve free winners peak during Correction/carry with zero positive capacity and an unchanged negative scrub request. Their geometry therefore still uses tighter while scrubbing, then opens before powered exit. Positive-reserve inner winners have essentially uniform curvature; the reported sample peak there is float rounding, not a physical apex.

## Four small perturbations

Subject q=0.750000000, reserve=0.000000000, FCT-AF9E514C9A01. No faster positive-reserve nonconstant winner: zero-reserve q=.75 fallback, not a robust winner. No optimization or clipping of controls.

| probe | shift m | validity | sector delta s | actual min headroom m |
|---|---:|---|---:|---:|
| Outward translation | -0.050000001 | TrackBoundary | — | — |
| Outward translation | 0.050000001 | Valid | 0.002256393 | 0.000016837 |
| Smooth exit fan | -0.050000001 | Valid | 0.003287315 | 0.000019957 |
| Smooth exit fan | 0.050000001 | Valid | -0.000505924 | 0.000065035 |

## Required answers

1. q=.5 crossover at 2.000000000 cm existing budget reserve: NO. Only five starts are eligible; objective convergence is uncertain. This is not a certificate of the same actual physical margin.
2. q=.5 crossover at 5.000000000 cm existing budget reserve: NO. Only five starts are eligible; objective convergence is uncertain. This is not a certificate of the same actual physical margin.
3. q=.5 crossover at 10.000000000 cm existing budget reserve: NO. Only five starts are eligible; objective convergence is uncertain. This is not a certificate of the same actual physical margin.
4. q=.75 crossover at 2/5/10 cm existing budget reserve: 2.000000000 cm: NO; 5.000000000 cm: NO; 10.000000000 cm: NO. Positive-reserve searches have only five eligible starts and uncertain objective convergence.
5. Current non-constant optimum depends materially on the lateral-execution boundary under the existing reserve gate. The longer-path/lower-powered-utilization/more-propulsion/higher-exit chain is visible at zero reserve, but no surviving positive-reserve free optimum demonstrates it away from that boundary.
6. Effect size/continuity: zero-reserve gains are approximately 9.53/22.16 ms; the valid +/-5 cm probes change time by at most about 3.3 ms. These are finite, locally small changes, but do not establish a robust positive-reserve effect or smoothness across an invalid boundary. Inward translation is invalid.
7. Structural promise: the simple positive-propulsion coupling remains a plausible hypothesis, but this follow-up does not establish a physically robust line optimum. Mesh-dependent reserve semantics limit that inference.
8. No empirical basis selects q=.5, .75 or any other q. These are structural probes, not calibrated values.
9. Correction remains an unresolved interpretation limit: effective negative scrub is outside the shared positive-propulsion allocation. A physical correction-phase exploit is neither proven nor excluded.
10. Recommendation: further analysis of this same subsystem, first with an explicitly specified mesh-independent execution-margin definition and correction data/interpretation; do not promote the current nonconstant optimum to production or reject the propulsion law solely from this mesh-dependent test. Keep Draft PR #50 for independent review.
