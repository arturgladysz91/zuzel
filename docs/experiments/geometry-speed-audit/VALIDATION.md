# Walidacja #159

Werdykt badawczy: **PARTIAL**, brak nowego natywnego wyniku autonomii S3.
Dziesięć istniejących pełnych biegów #158 pozostaje zamrożonym wejściem.
Nowe narzędzie wymaga jawnej komendy, pozostaje OFF; nie ma ścieżki produkcyjnej.

Walidacja obejmuje locked restore, Release build warnings-as-errors,
pełne shard core i trajectory według aktualnej polityki Draft, discovery
wszystkich czterech shardów bez luk/duplikatów, wszystkie testy Python calibration
i CI-policy, Node replay oraz strażnika zamrożonych konsumentów.
Historical-analysis i four-rider są discovery-only w Draft; nie raportujemy ich
jako wykonanych. Nie zmieniono polityki CI ani wymagań certyfikacji.

Wstępna pełna kontrola lokalna wykonała2563 testy C# i118 Node bez porażek.
Python wykrył zmianę LF→CRLF w kilku niezabezpieczonych plikach starego
checkoutu #143; przywrócono dokładne bajty blobów Git, a7 testów tego modułu
przeszło ponownie. Oczekiwane hashe i historyczne źródła w Git pozostają
niezmienione. Końcowa czysta kopia używa `core.autocrlf=false`, jak Windows CI.

Nowe testy sprawdzają rozróżnienie prognoza/natywny bieg, pełne trzy okrążenia i
sześć łuków źródeł, SHA-256 wejściowych stanów, dokładne indeksy replay, geometrię
B/C, faktyczne wielokąty granic, bilans energii do mety, reszty momentów
lokalnej równowagi i budżet wraz z nieudanymi uruchomieniami.

Po przygotowaniu commitu te same wymagane testy są uruchamiane na dokładnym
końcowym SHA w osobnej czystej kopii, a zdalne Windows CI na tym samym SHA
musi zakończyć obowiązujące zadania Draft. Dokładny końcowy SHA, wyniki i URL
CI zostają podane w opisie Draft PR i dostarczonym `Windows-CI-159.json`.
Nie zapisujemy własnego SHA w nowym commicie dokumentacji po walidacji.

Wczesny brak miejsca na C: przerwał wyłącznie pobranie pełnego checkoutu i
eksport HTML. Zachowano wszystkie pomiary; robocza kopia została przeniesiona
na D: i udostępniona pod tym samym katalogiem work. HTML wygenerowano ponownie.
Nie jest to DNF świata; żaden nowy świat nie był uruchomiony.
