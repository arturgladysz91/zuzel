# Tabele #174 — krótkie próby, nie S3

| Próba | Model dopuszcza | BASE cel [s] | NEW cel [s] | Zysk lokalny [s] | V wej BASE/NEW | V min BASE/NEW | V wyj BASE/NEW | Swept NEW globalny / po impulsie | Ocena [ms] |
|---|---|---:|---:|---:|---|---|---|---|---:|
| P7 | tak, natywnie legalny | 20.274179 | 20.136242 | 0.137937 | 72.66/78.49 | 56.60/56.55 | 63.10/63.51 | 1.393/1.470 | 899.1 |
| P8 | tak, natywnie legalny | 20.274179 | 20.202290 | 0.071889 | 72.66/76.29 | 56.60/56.62 | 63.10/63.34 | 1.393/1.438 | 741.3 |
| X4 | tak, natywnie legalny | 20.722135 | 20.568092 | 0.154043 | 72.85/77.23 | 56.60/56.55 | 62.99/63.26 | 1.369/1.491 | 882.3 |

## Archiwalne stany: wynik modelu, nie natywny certyfikat

| Stan | Gaz historyczny | Propozycja | Stary CanPrepare | Decyzja modelu | Zysk modelu [s] | CPU [ms] | Powód |
|---|---:|---:|---|---|---:|---:|---|
| P1 | 17.12% | 95% | True | True | 0.09274566223080072 | 1093.8 | admitted: causal whole-bend model predicts common-goal time gain |
| P7 | 0.00% | 95% | False | True | 0.13576835960072842 | 1078.1 | admitted: causal whole-bend model predicts common-goal time gain |
| P8 | 2.36% | 95% | False | True | 0.07189471979339856 | 843.8 | admitted: causal whole-bend model predicts common-goal time gain |
| P2 | 40.54% | 95% | True | True | 0.02044757524351226 | 1000.0 | admitted: causal whole-bend model predicts common-goal time gain |
| P3 | 47.77% | 95% | True | True | 0.06078972464204213 | 1015.6 | admitted: causal whole-bend model predicts common-goal time gain |
| E41 | 10.91% | 95% | False | False | UNKNOWN | 687.5 | whole-bend model rejected: MODEL_SWEPT065 |
| E25 | 0.00% | 95% | False | False | UNKNOWN | 656.2 | whole-bend model rejected: MODEL_SWEPT065 |
| E12 | 0.00% | 95% | False | False | UNKNOWN | 593.8 | whole-bend model rejected: BETA45 |
| P4 | 41.37% | 95% | True | True | 0.03210691875599281 | 953.1 | admitted: causal whole-bend model predicts common-goal time gain |
| P5 | 48.87% | 95% | True | True | 0.02636342105244438 | 953.1 | admitted: causal whole-bend model predicts common-goal time gain |
| P6 | 43.43% | 95% | True | True | 0.03608079197613279 | 1046.9 | admitted: causal whole-bend model predicts common-goal time gain |
| P9 | 8.46% | 95% | False | True | 0.1506423442985323 | 968.8 | admitted: causal whole-bend model predicts common-goal time gain |
| X4 | 7.55% | 95% | False | True | 0.15350560566839988 | 984.4 | admitted: causal whole-bend model predicts common-goal time gain |
| XB | 0.00% | 95% | False | False | UNKNOWN | 703.1 | whole-bend model rejected: MODEL_SWEPT065 |

## Osiem odłożonych par — niewykonane

| Stan | Pole | Start [m] | Offset [m] | BASE | NEW |
|---|---|---:|---:|---|---|
| W1 | A | -0.5831 | 1.5137 | NOT_RUN | NOT_RUN |
| W2 | A | -0.5217 | 1.6379 | NOT_RUN | NOT_RUN |
| W3 | B | -0.6113 | 4.2871 | NOT_RUN | NOT_RUN |
| W4 | B | -0.4579 | 4.6033 | NOT_RUN | NOT_RUN |
| W5 | C | -0.6473 | 6.8879 | NOT_RUN | NOT_RUN |
| W6 | C | -0.4931 | 7.2417 | NOT_RUN | NOT_RUN |
| W7 | D | -0.5719 | 9.4123 | NOT_RUN | NOT_RUN |
| W8 | D | -0.4397 | 9.6789 | NOT_RUN | NOT_RUN |

Powód: niespełniona bramka kosztu z fazy D. Nie ma mediany S3, trzech czasów okrążeń ani poprawy sportowej dla #174. Pełne dane sekcji, czasu gazu ≥95%, drogi, prognoz oraz percentyli CPU zawiera summary174.json.
