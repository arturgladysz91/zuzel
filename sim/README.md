# Moduł symulacji jazdy

Biblioteka `SpeedwaySim` zawiera deterministyczny model fizyki okrążenia. Kod napisany w C# (docelowo do wpięcia w Unity).

## Wymagania

* .NET 7 SDK lub nowszy.

## Budowa

```bash
cd sim
 dotnet build SpeedwaySim.sln
```

## Testy

W katalogu `SpeedwaySim.Tests/` znajdują się testy jednostkowe oparte na xUnit. Uruchomienie:

```bash
cd sim
 dotnet test SpeedwaySim.sln
```

## Struktura

* `Models/` – definicje toru, linii jazdy, środowiska.
* `Physics/` – stałe i funkcje pomocnicze.
* `Simulation/` – główne klasy symulatora oraz typy zdarzeń i telemetrii.
