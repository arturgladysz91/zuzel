# Badanie #153

[Raport i cztery tabele](REPORT.md) zawierają rozdzielone wyniki L i S, pełne
ukończenia, nieudane próby, analizę historycznych marginesów oraz koszt CPU.

[Instrukcje narzędzia](../../../tools/safe-fast-contingency/README.md) opisują
reprodukcję z nowym katalogiem wyników i niezmiennym ledgerem historycznym.
Replay: `tools/replay-viewer/?case=safety153` z katalogu głównego repozytorium.

Historyczny czas53,989766 s jest wynikiem legacy; nie spełnia wymogu S0,5 m.
Tak samo55,167079 s #152 nie jest pełnym legalnym wynikiem S. Zapis PARTIAL
pozostaje przerwanym przejazdem, nawet jeśli wszystkie wykonane interwały były legalne.
