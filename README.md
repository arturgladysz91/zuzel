# Speedway Manager – Faza 1

Projekt zakłada budowę gry "Speedway Manager". W tej fazie skupiamy się na deterministycznym module symulacji fizyki jazdy.

## Wybór silnika

Wybrano **Unity (C#)** jako docelowy silnik z następujących powodów:

1. **Szybka iteracja** – edytor Unity oraz możliwość korzystania z hot-reload dla skryptów C# przyspiesza weryfikację zmian w modelu jazdy.
2. **Łatwe buildy na Windows** – Unity posiada dopracowany pipeline eksportu na platformę docelową oraz dobrą integrację z CI/CD.
3. **Silna ekosystemowa integracja z .NET** – pozwala na implementację i testowanie logiki w postaci biblioteki .NET, co ułatwia budowanie testów jednostkowych i narzędzi symulacyjnych niezależnie od UI.

Katalog `/sim/` zawiera moduł logiki fizyki napisany w C#, gotowy do wpięcia w projekt Unity.

## Struktura repozytorium

```
/game/        # zasoby i sceny gry (szkic na kolejne fazy)
/sim/         # moduł fizyki jazdy + testy jednostkowe
/data/        # pliki JSON z parametrami torów
/tools/       # narzędzia developerskie (generatory, build)
/docs/        # dokumentacja wysokopoziomowa
/ci/          # konfiguracja pipeline'u CI
```

## Stan obecny (Faza 1)

* moduł symulacji okrążenia z obsługą prostych i łuków,
* deterministyczna fizyka linii jazdy z kontrolowanym RNG,
* przykładowe dane toru oraz testy weryfikujące powtarzalność wyników.

Instrukcje budowy i uruchomienia testów znajdują się w `sim/README.md`.
