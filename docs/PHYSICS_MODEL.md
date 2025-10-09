# Model fizyki – Faza 1

Moduł fizyki odwzorowuje zachowanie motocykla żużlowego na prostej i w łuku przy użyciu uproszczonych, ale kontrolowanych równań.

## Prosta

Prędkość jest aktualizowana według równania:

```
v_{t+1} = clamp(v_t + a_{accel} - dragFactor, 0, v_{max})
```

gdzie `dragFactor` zależy od prędkości, globalnego współczynnika oporu oraz parametru środowiskowego `env.drag`.

## Łuk

Każda linia jazdy posiada stabilną prędkość `v_stable(line, grip)`. Gdy prędkość wejściowa przekracza tę wartość, generowany jest dryf:

```
drift = k_d * max(0, v_in - v_stable)
```

* Jeżeli `drift` przekracza tolerancję linii, następuje wypchnięcie na zewnętrzną linię i kara prędkości (`v_penalty`).
* Utrzymanie linii skutkuje mniejszą karą i niewielkim bonusem prędkości.

Parametry `k_d`, tolerancje oraz bonusy są zdefiniowane jako stałe globalne, co pozwala na deterministyczne kalibrowanie modelu bez zależności od atrybutów zawodników.

## Zdarzenia

Symulator zwraca listę zdarzeń (`LINE_HELD`, `PUSHED_OUT`, `SLIP`), ułatwiając dalszą analizę taktyki i ryzyka.

## RNG

Jedno źródło losowości (`System.Random`) kontrolowane przez `env.rngSeed`. Wprowadzona wariancja dotyczy drobnych odchyłek w stabilnej prędkości i umożliwia powtarzalne testy.
