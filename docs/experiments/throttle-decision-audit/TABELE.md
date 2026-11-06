# Tabele diagnostyczne #173

Mechanizmy mogą współwystępować. Efekty składników Policy to czyste kontrfaktyczne obliczenia: usuń jeden składnik przy zachowaniu pozostałych i clamp 0..1. Nie są sumowalne i nie opisują automatycznie końcowej komendy po Exit/K4.

Zakres tabeli A: 1413 decyzji przed trzecią metą, w tym 505 na prostych. Dalsze 25 decyzji rezerwy zachowano w danych, lecz wyłączono z tej tabeli.

| Mechanizm — proste | Liczba / 505 | Udział | Śr. efekt na propozycję danej warstwy | Śr. zablokowane zwiększenie | Znaczenie |
|---|---:|---:|---:|---:|---|
| CONTINUATION_ENTRY_AND_SETTLED | 19 | 3.76% | — | 56.47 p.p. | weto zwiększenia lub kontekst wyboru |
| CONTINUATION_ENTRY_BUDGET_ONLY | 46 | 9.11% | — | 70.16 p.p. | weto zwiększenia lub kontekst wyboru |
| CONTINUATION_REJECT | 65 | 12.87% | — | 66.16 p.p. | weto zwiększenia lub kontekst wyboru |
| ENTRY_SPEED_BUDGET | 203 | 40.20% | — | 74.57 p.p. | weto zwiększenia lub kontekst wyboru |
| K4_DRIVE_REDUCTION | 45 | 8.91% | 2.71 p.p. | — | zmniejszenie propozycji Exit przez K4 |
| NO_BOOST_WINDOW | 233 | 46.14% | — | — | weto zwiększenia lub kontekst wyboru |
| REFERENCE_LOW_DRIVE | 468 | 92.67% | 65.00 p.p. | — | zmniejszenie bazowej propozycji |
| SPEED_TARGET_REACHED | 448 | 88.71% | 15.70 p.p. | — | zmniejszenie bazowej propozycji |
| TRACKING_SCORE_RETAINED | 47 | 9.31% | — | — | weto zwiększenia lub kontekst wyboru |
| WHEELSLIP_REDUCTION | 117 | 23.17% | 2.96 p.p. | — | zmniejszenie bazowej propozycji |
| SHORT_PREDICTION_REJECT | 0 | 0% | — | — | brak odrzucenia w ocenionym zbiorze |
| BOUNDARY_CLEARANCE / BETA45 | 0 | 0% | — | — | brak odrzucenia w ocenionym zbiorze |
| PLAN_FALLBACK (brak dopuszczalnej linii) | 0 | 0% | — | — | zwykłe wyjście poza okno K4 nie jest awarią planu |
| TRANSITION_OVERRIDE | 0 | 0% | — | — | wszystkie końcowe przejścia continuous |

UNKNOWN dotyczy optymalnego wejścia i konieczności ograniczenia poza zbadanym horyzontem, nie tożsamości odtworzonej komendy. Pewność przypisania kodowego jest wysoka: wszystkie 1438 akcji/trybów/liczników odpowiadają historii.

## Każda prosta i rzeczywista aktywacja gazu

Czasy absolutne [s]. Spadki poniżej 95% i 70% liczymy po pierwszym osiągnięciu ≥95%. Gdy próg nie został osiągnięty, pola pozostają puste. Gaz aktywny pochodzi z rzeczywistych aktywacji, nie z bieżącej propozycji. Ostatnia home jest niepełna; początkowy rozbieg zapisano osobno.

| Próba | Okr./prosta | V pocz. | Gaz pocz. | Pierwsze ≥95% | <95% | <70% | V przy <95% | Do łuku przy <95% | V wejścia w bramce | Powód komendy spadku |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| v1-K4-S1 | 0/launch | 0.00 | 0.000 | 0.240 | 1.040 | 1.640 | 16.879 | 33.734 | 73.656 | reference |
| v1-K4-S1 | 1/main | 60.50 | 0.867 | 10.440 | 10.840 | 10.840 | 71.833 | 51.208 | 73.656 | roll-off:continuation |
| v1-K4-S1 | 1/home | 63.10 | 0.868 | 19.440 | 19.640 | 19.640 | 70.218 | 55.245 | 73.656 | roll-off:continuation |
| v1-K4-S1 | 2/main | 63.15 | 0.807 | 28.240 | 28.440 | 28.440 | 68.621 | 56.830 | 73.656 | roll-off:continuation |
| v1-K4-S1 | 2/home | 63.09 | 0.859 | 37.240 | 37.440 | 37.440 | 69.973 | 55.474 | 73.656 | roll-off:continuation |
| v1-K4-S1 | 3/main | 63.28 | 0.796 | 46.040 | 46.240 | 46.240 | 68.454 | 57.106 | 73.656 | roll-off:continuation |
| v1-K4-S1 | 3/home (część) | 62.97 | 0.845 | 55.040 | 55.240 | 55.240 | 69.488 | 55.828 | 73.656 | roll-off:continuation |
| v1-K4-S2 | 0/launch | 0.00 | 0.000 | 0.240 | 1.040 | 1.840 | 16.879 | 34.354 | 73.656 | reference |
| v1-K4-S2 | 1/main | 60.55 | 0.795 | 10.440 | 10.840 | 10.840 | 71.068 | 51.963 | 73.656 | roll-off:continuation |
| v1-K4-S2 | 1/home | 63.09 | 0.820 | 19.440 | 19.640 | 19.640 | 68.952 | 56.464 | 73.656 | roll-off:continuation |
| v1-K4-S2 | 2/main | 63.47 | 0.744 | 28.440 | 28.640 | 28.640 | 71.463 | 54.387 | 73.656 | roll-off:continuation |
| v1-K4-S2 | 2/home | 62.97 | 0.835 | 37.240 | 37.440 | 37.440 | 69.229 | 56.087 | 73.656 | roll-off:continuation |
| v1-K4-S2 | 3/main | 63.33 | 0.761 | 46.240 | 46.440 | 46.440 | 71.589 | 54.178 | 73.656 | roll-off:continuation |
| v1-K4-S2 | 3/home (część) | 62.97 | 0.840 | 55.040 | 55.240 | 55.240 | 69.330 | 55.976 | 73.656 | roll-off:continuation |
| v1-K4-S3 | 0/launch | 0.00 | 0.000 | 0.240 | 1.040 | 1.840 | 16.879 | 34.124 | 73.656 | reference |
| v1-K4-S3 | 1/main | 61.08 | 0.599 | 10.640 | 10.840 | 10.840 | 69.058 | 54.154 | 73.656 | roll-off:continuation |
| v1-K4-S3 | 1/home | 63.10 | 0.869 | 19.640 | 19.840 | 19.840 | 70.234 | 55.231 | 73.656 | roll-off:continuation |
| v1-K4-S3 | 2/main | 63.20 | 0.802 | 28.440 | 28.640 | 28.640 | 68.553 | 56.954 | 73.656 | roll-off:continuation |
| v1-K4-S3 | 2/home | 63.02 | 0.854 | 37.440 | 37.640 | 37.640 | 69.772 | 55.605 | 73.656 | roll-off:continuation |
| v1-K4-S3 | 3/main | 63.32 | 0.791 | 46.240 | 46.440 | 46.440 | 68.340 | 57.251 | 73.656 | roll-off:continuation |
| v1-K4-S3 | 3/home (część) | 62.97 | 0.839 | 55.240 | 55.440 | 55.440 | 69.313 | 56.001 | 73.656 | roll-off:continuation |
| v1-K4-S4 | 0/launch | 0.00 | 0.000 | 0.240 | 1.040 | 1.040 | 16.879 | 34.794 | 73.656 | reference |
| v1-K4-S4 | 1/main | 61.24 | 0.778 | 10.840 | 11.240 | 11.240 | 71.609 | 51.935 | 73.656 | roll-off:continuation |
| v1-K4-S4 | 1/home | 62.99 | 0.834 | 19.840 | 20.040 | 20.040 | 69.218 | 56.115 | 73.656 | roll-off:continuation |
| v1-K4-S4 | 2/main | 63.36 | 0.774 | 28.840 | 29.040 | 29.040 | 71.629 | 54.181 | 73.656 | roll-off:continuation |
| v1-K4-S4 | 2/home | 62.97 | 0.840 | 37.640 | 37.840 | 37.840 | 69.328 | 55.980 | 73.656 | roll-off:continuation |
| v1-K4-S4 | 3/main | 63.34 | 0.777 | 46.640 | 46.840 | 46.840 | 71.763 | 54.037 | 73.656 | roll-off:continuation |
| v1-K4-S4 | 3/home (część) | 62.96 | 0.843 | 55.440 | 55.640 | 55.640 | 69.418 | 55.887 | 73.656 | roll-off:continuation |
| v1-K4-S5 | 0/launch | 0.00 | 0.000 | 0.240 | 1.040 | 1.040 | 16.879 | 35.044 | 73.656 | reference |
| v1-K4-S5 | 1/main | 60.67 | 0.591 | 10.640 | 11.040 | 11.040 | 72.429 | 50.725 | 73.656 | roll-off:continuation |
| v1-K4-S5 | 1/home | 63.28 | 0.739 | 19.640 | 19.840 | 19.840 | 71.133 | 54.572 | 73.656 | roll-off:continuation |
| v1-K4-S5 | 2/main | 63.03 | 0.829 | 28.440 | 28.640 | 28.640 | 69.117 | 56.244 | 73.656 | roll-off:continuation |
| v1-K4-S5 | 2/home | 63.31 | 0.755 | 37.440 | 37.640 | 37.640 | 71.452 | 54.293 | 73.656 | roll-off:continuation |
| v1-K4-S5 | 3/main | 62.98 | 0.836 | 46.240 | 46.440 | 46.440 | 69.253 | 56.074 | 73.656 | roll-off:continuation |
| v1-K4-S5 | 3/home (część) | 63.35 | 0.774 | 55.240 | 55.440 | 55.440 | 71.640 | 54.161 | 73.656 | roll-off:continuation |

## Tabela B — charakterystyczne decyzje

| Stan | Okr./prosta | s [m] | V [km/h] | Gaz profilu | Policy | Exit | K4/zaakceptowany | Aktywny | Główne późniejsze weto | Uzasadnienie fizyczne |
|---|---|---:|---:|---:|---:|---:|---:|---:|---|---|
| P1 | 1/main | 138.578 | 66.905 | 0.552 | 0.171 | 0.171 | 0.171 | 1.000 | roll-off:continuation | lokalnie wykonalne; UNKNOWN poza 0,84 s |
| P2 | 1/home | 294.056 | 65.462 | 0.586 | 0.405 | 0.405 | 0.405 | 0.868 | roll-off:continuation | lokalnie wykonalne; UNKNOWN poza 0,84 s |
| P3 | 2/main | 451.964 | 63.973 | 0.596 | 0.478 | 0.478 | 0.478 | 0.807 | roll-off:continuation | lokalnie wykonalne; UNKNOWN poza 0,84 s |
| P4 | 2/home | 612.625 | 65.239 | 0.588 | 0.414 | 0.414 | 0.414 | 0.859 | roll-off:continuation | lokalnie wykonalne; UNKNOWN poza 0,84 s |
| P5 | 3/main | 770.473 | 63.819 | 0.597 | 0.489 | 0.489 | 0.489 | 0.796 | roll-off:continuation | lokalnie wykonalne; UNKNOWN poza 0,84 s |
| P6 | 3/home | 931.082 | 64.785 | 0.590 | 0.434 | 0.434 | 0.434 | 0.845 | roll-off:continuation | lokalnie wykonalne; UNKNOWN poza 0,84 s |
| P7 | 1/main | 163.587 | 77.059 | 0.117 | 0.000 | 0.000 | 0.000 | 0.030 | roll-off:entry-speed-budget | weto pojedynczego impulsu sfalsyfikowane natywnie |
| P8 | 1/main | 184.597 | 74.202 | 0.259 | 0.093 | 0.093 | 0.024 | 0.000 | reference | lokalnie wykonalne; UNKNOWN poza 0,84 s |
| P9 | 1/main | 145.685 | 74.725 | 0.452 | 0.085 | 0.085 | 0.085 | 0.146 | roll-off:entry-speed-budget | lokalnie wykonalne; UNKNOWN poza 0,84 s |

## Tabela C — prognozy przy historycznym skręcie

Każdy stan ma te same położenie, prędkości, wał, koło, parametry sprzęgła, aktywną komendę i pending. Horizon 0,84 s od decyzji obejmuje 0,24 s reakcji. pulse oznacza jedną komendę i kolejne odpuszczenie przez Policy, z całą kolejką; held utrzymuje nowy gaz do końca horyzontu. Wszystkie wyniki to prognozy, bez zaliczenia łuku/S3.

| Stan | Wariant | Gaz | Skręt [°] | V po 0,84 s | Min swept | Max β | Legalność lokalna | CanPrepare na końcu |
|---|---|---:|---:|---:|---:|---:|---|---|
| P1 | held-0.84 / historical | 0.171 | -0.487 | 78.479 | 3.438 | 1.961 | lokalnie wykonalne | False |
| P1 | pulse0.2-policy-release / historical | 0.171 | -0.487 | 76.716 | 3.438 | 8.449 | lokalnie wykonalne | True |
| P1 | held-0.84 / plus20pp | 0.371 | -0.487 | 81.426 | 3.438 | 2.623 | lokalnie wykonalne | False |
| P1 | pulse0.2-policy-release / plus20pp | 0.371 | -0.487 | 77.448 | 3.438 | 9.693 | lokalnie wykonalne | True |
| P1 | held-0.84 / 95percent | 0.950 | -0.487 | 83.133 | 3.438 | 11.408 | lokalnie wykonalne | False |
| P1 | pulse0.2-policy-release / 95percent | 0.950 | -0.487 | 78.723 | 3.438 | 14.720 | lokalnie wykonalne | False |
| P1 | held-0.84 / 100percent | 1.000 | -0.487 | 83.139 | 3.438 | 11.835 | lokalnie wykonalne | False |
| P1 | pulse0.2-policy-release / 100percent | 1.000 | -0.487 | 78.793 | 3.438 | 15.119 | lokalnie wykonalne | False |
| P2 | held-0.84 / historical | 0.405 | -1.347 | 79.916 | 3.019 | 2.181 | lokalnie wykonalne | False |
| P2 | pulse0.2-policy-release / historical | 0.405 | -1.347 | 77.193 | 3.019 | 11.377 | lokalnie wykonalne | True |
| P2 | held-0.84 / plus20pp | 0.605 | -1.347 | 81.746 | 3.019 | 2.193 | lokalnie wykonalne | False |
| P2 | pulse0.2-policy-release / plus20pp | 0.605 | -1.347 | 77.653 | 3.019 | 13.255 | lokalnie wykonalne | True |
| P2 | held-0.84 / 95percent | 0.950 | -1.347 | 82.204 | 3.019 | 4.818 | lokalnie wykonalne | False |
| P2 | pulse0.2-policy-release / 95percent | 0.950 | -1.347 | 78.058 | 3.019 | 16.205 | lokalnie wykonalne | True |
| P2 | held-0.84 / 100percent | 1.000 | -1.347 | 82.216 | 3.019 | 5.292 | lokalnie wykonalne | False |
| P2 | pulse0.2-policy-release / 100percent | 1.000 | -1.347 | 78.097 | 3.019 | 16.583 | lokalnie wykonalne | True |
| P3 | held-0.84 / historical | 0.478 | -1.484 | 78.936 | 2.772 | 2.457 | lokalnie wykonalne | False |
| P3 | pulse0.2-policy-release / historical | 0.478 | -1.484 | 76.107 | 2.772 | 11.322 | lokalnie wykonalne | True |
| P3 | held-0.84 / plus20pp | 0.678 | -1.484 | 80.271 | 2.772 | 2.573 | lokalnie wykonalne | False |
| P3 | pulse0.2-policy-release / plus20pp | 0.678 | -1.484 | 76.494 | 2.772 | 13.627 | lokalnie wykonalne | True |
| P3 | held-0.84 / 95percent | 0.950 | -1.484 | 80.485 | 2.772 | 6.359 | lokalnie wykonalne | False |
| P3 | pulse0.2-policy-release / 95percent | 0.950 | -1.484 | 76.739 | 2.772 | 16.350 | lokalnie wykonalne | True |
| P3 | held-0.84 / 100percent | 1.000 | -1.484 | 80.487 | 2.772 | 6.963 | lokalnie wykonalne | False |
| P3 | pulse0.2-policy-release / 100percent | 1.000 | -1.484 | 76.766 | 2.772 | 16.780 | lokalnie wykonalne | True |
| P4 | held-0.84 / historical | 0.414 | -1.605 | 79.710 | 3.003 | 2.643 | lokalnie wykonalne | False |
| P4 | pulse0.2-policy-release / historical | 0.414 | -1.605 | 76.905 | 3.003 | 11.577 | lokalnie wykonalne | True |
| P4 | held-0.84 / plus20pp | 0.614 | -1.605 | 81.410 | 3.003 | 2.775 | lokalnie wykonalne | False |
| P4 | pulse0.2-policy-release / plus20pp | 0.614 | -1.605 | 77.294 | 3.003 | 13.780 | lokalnie wykonalne | True |
| P4 | held-0.84 / 95percent | 0.950 | -1.605 | 81.717 | 3.003 | 7.220 | lokalnie wykonalne | False |
| P4 | pulse0.2-policy-release / 95percent | 0.950 | -1.605 | 77.600 | 3.003 | 17.038 | lokalnie wykonalne | True |
| P4 | held-0.84 / 100percent | 1.000 | -1.605 | 81.714 | 3.003 | 7.818 | lokalnie wykonalne | False |
| P4 | pulse0.2-policy-release / 100percent | 1.000 | -1.605 | 77.628 | 3.003 | 17.460 | lokalnie wykonalne | True |
| P5 | held-0.84 / historical | 0.489 | -1.304 | 78.845 | 2.744 | 2.156 | lokalnie wykonalne | False |
| P5 | pulse0.2-policy-release / historical | 0.489 | -1.304 | 76.085 | 2.744 | 10.986 | lokalnie wykonalne | True |
| P5 | held-0.84 / plus20pp | 0.689 | -1.304 | 80.173 | 2.744 | 2.224 | lokalnie wykonalne | False |
| P5 | pulse0.2-policy-release / plus20pp | 0.689 | -1.304 | 76.519 | 2.744 | 13.153 | lokalnie wykonalne | True |
| P5 | held-0.84 / 95percent | 0.950 | -1.304 | 80.424 | 2.744 | 5.339 | lokalnie wykonalne | False |
| P5 | pulse0.2-policy-release / 95percent | 0.950 | -1.304 | 76.802 | 2.744 | 15.686 | lokalnie wykonalne | True |
| P5 | held-0.84 / 100percent | 1.000 | -1.304 | 80.434 | 2.744 | 5.885 | lokalnie wykonalne | False |
| P5 | pulse0.2-policy-release / 100percent | 1.000 | -1.304 | 76.835 | 2.744 | 16.098 | lokalnie wykonalne | True |
| P6 | held-0.84 / historical | 0.434 | -1.690 | 79.433 | 2.992 | 2.847 | lokalnie wykonalne | False |
| P6 | pulse0.2-policy-release / historical | 0.434 | -1.690 | 76.534 | 2.992 | 11.833 | lokalnie wykonalne | True |
| P6 | held-0.84 / plus20pp | 0.634 | -1.690 | 80.930 | 2.992 | 3.085 | lokalnie wykonalne | False |
| P6 | pulse0.2-policy-release / plus20pp | 0.634 | -1.690 | 76.879 | 2.992 | 14.227 | lokalnie wykonalne | True |
| P6 | held-0.84 / 95percent | 0.950 | -1.690 | 81.131 | 2.992 | 8.130 | lokalnie wykonalne | False |
| P6 | pulse0.2-policy-release / 95percent | 0.950 | -1.690 | 77.118 | 2.992 | 17.428 | lokalnie wykonalne | True |
| P6 | held-0.84 / 100percent | 1.000 | -1.690 | 81.122 | 2.992 | 8.779 | lokalnie wykonalne | False |
| P6 | pulse0.2-policy-release / 100percent | 1.000 | -1.690 | 77.140 | 2.992 | 17.868 | lokalnie wykonalne | True |
| P7 | held-0.84 / historical | 0.000 | -0.049 | 74.674 | 4.132 | 0.720 | lokalnie wykonalne | True |
| P7 | pulse0.2-policy-release / historical | 0.000 | -0.049 | 74.673 | 4.138 | 0.720 | lokalnie wykonalne | True |
| P7 | held-0.84 / plus20pp | 0.200 | -0.049 | 77.886 | 4.122 | 0.720 | lokalnie wykonalne | False |
| P7 | pulse0.2-policy-release / plus20pp | 0.200 | -0.049 | 75.853 | 4.133 | 0.720 | lokalnie wykonalne | False |
| P7 | held-0.84 / 95percent | 0.950 | -0.049 | 86.738 | 4.085 | 0.720 | lokalnie wykonalne | False |
| P7 | pulse0.2-policy-release / 95percent | 0.950 | -0.049 | 80.530 | 4.118 | 0.720 | lokalnie wykonalne | False |
| P7 | held-0.84 / 100percent | 1.000 | -0.049 | 86.909 | 4.084 | 0.720 | lokalnie wykonalne | False |
| P7 | pulse0.2-policy-release / 100percent | 1.000 | -0.049 | 80.841 | 4.117 | 0.720 | lokalnie wykonalne | False |
| P8 | held-0.84 / historical | 0.024 | 2.579 | 70.633 | 3.062 | 7.623 | lokalnie wykonalne | True |
| P8 | pulse0.2-policy-release / historical | 0.024 | 2.579 | 70.466 | 3.071 | 7.383 | lokalnie wykonalne | True |
| P8 | held-0.84 / plus20pp | 0.224 | 2.579 | 73.437 | 3.054 | 9.002 | lokalnie wykonalne | True |
| P8 | pulse0.2-policy-release / plus20pp | 0.224 | 2.579 | 71.568 | 3.075 | 7.745 | lokalnie wykonalne | True |
| P8 | held-0.84 / 95percent | 0.950 | 2.579 | 78.304 | 2.964 | 29.651 | lokalnie wykonalne | False |
| P8 | pulse0.2-policy-release / 95percent | 0.950 | 2.579 | 74.906 | 3.026 | 14.789 | lokalnie wykonalne | False |
| P8 | held-0.84 / 100percent | 1.000 | 2.579 | 78.323 | 2.962 | 31.044 | lokalnie wykonalne | False |
| P8 | pulse0.2-policy-release / 100percent | 1.000 | 2.579 | 75.062 | 3.020 | 15.814 | lokalnie wykonalne | False |
| P9 | held-0.84 / historical | 0.085 | 0.487 | 76.840 | 4.500 | 2.066 | lokalnie wykonalne | True |
| P9 | pulse0.2-policy-release / historical | 0.085 | 0.487 | 75.685 | 4.500 | 7.911 | lokalnie wykonalne | True |
| P9 | held-0.84 / plus20pp | 0.285 | 0.487 | 80.008 | 4.500 | 2.360 | lokalnie wykonalne | False |
| P9 | pulse0.2-policy-release / plus20pp | 0.285 | 0.487 | 76.582 | 4.500 | 7.934 | lokalnie wykonalne | True |
| P9 | held-0.84 / 95percent | 0.950 | 0.487 | 85.175 | 4.500 | 17.750 | lokalnie wykonalne | False |
| P9 | pulse0.2-policy-release / 95percent | 0.950 | 0.487 | 79.717 | 4.500 | 11.049 | lokalnie wykonalne | False |
| P9 | held-0.84 / 100percent | 1.000 | 0.487 | 85.181 | 4.500 | 19.030 | lokalnie wykonalne | False |
| P9 | pulse0.2-policy-release / 100percent | 1.000 | 0.487 | 79.915 | 4.500 | 11.688 | lokalnie wykonalne | False |

## Cztery nowe krótkie ramiona natywne P7

| Ramię | Gaz interwencji | Skręt [°] | Czas do lokalnego celu | Zysk [s] | Vmax po rozwidleniu | Wejście w łuk 2 | Min swept po rozwidleniu | Max β po rozwidleniu | Wynik |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 95-coordinated | 0.950 | -1.195 | 20.144076 | 0.130103 | 80.915 | 78.386 | 1.479 | 18.169 | legalny cały następny łuk +20 m |
| 95-fixed | 0.950 | -0.049 | 20.136242 | 0.137937 | 80.975 | 78.491 | 1.470 | 19.394 | legalny cały następny łuk +20 m |
| baseline | 0.000 | -0.049 | 20.274179 | 0.000000 | 78.941 | 72.655 | 1.404 | 11.699 | legalny cały następny łuk +20 m |
| plus20pp | 0.200 | -0.049 | 20.232777 | 0.041401 | 79.319 | 73.738 | 1.412 | 13.040 | legalny cały następny łuk +20 m |
