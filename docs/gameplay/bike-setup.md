# Motocykl i setup

## Rola setupu — BINDING

Setup jest kompromisem dopasowanym do toru, zawodnika i planu biegu. Nie istnieje ustawienie uniwersalnie najlepsze ani „droższy motor”, który daje stały bonus do wyniku.

Menedżer sugeruje zmianę. Zawodnik może ją zaakceptować, skorygować albo odrzucić zależnie od zaufania, niezależności, własnej diagnozy i dostępnego czasu.

## Zakres pierwszej wersji — PROVISIONAL

### Przełożenie

Motocykl żużlowy korzysta podczas jazdy z jednego biegu, a przełożenie końcowe
jest elementem setupu. Obecne API `BikeSetup.Gearing` pozostaje skalarem `0..1`
i należy je czytać jako oś `drive-oriented ↔ speed-oriented`: `0` jest bardziej
drive-oriented (lepszy corner-exit drive, niższa osiągalna prędkość), a `1`
bardziej speed-oriented (słabszy corner-exit drive, wyższa osiągalna prędkość).
Nie jest to jeszcze mapa na skalibrowane zębatki ani finalne dane telemetryczne.

Interfejs podstawowy:

- dużo krótsze,
- krótsze,
- bez zmiany,
- dłuższe,
- dużo dłuższe.

Interfejs zaawansowany może pokazywać liczbę zębów przedniej i tylnej zębatki oraz obliczone przełożenie końcowe:

`FinalDriveRatio = RearSprocketTeeth / FrontSprocketTeeth`

Większy stosunek oznacza krótsze przełożenie. Dokładne dozwolone zakresy zębów i skok zmian pozostają do zweryfikowania.

Konsekwencje krótszego przełożenia:

- lepsza reakcja i przyspieszenie,
- większa wrażliwość na wheelspin,
- bardziej nerwowe oddawanie mocy,
- wcześniejsze osiąganie ograniczenia użytecznych obrotów.

Konsekwencje dłuższego przełożenia:

- słabsza reakcja na starcie i po błędzie,
- łagodniejsze oddawanie mocy,
- łatwiejsze utrzymanie trakcji w niektórych warunkach,
- większy potencjał płynnego niesienia prędkości.

### Reakcja startowa i sprzęgło

Planowany parametr opisuje kompromis między agresywnym ruszeniem a kontrolą trakcji. Nie zastępuje statystyki startu zawodnika.

### Reakcja silnika i trakcja

Planowany parametr zmienia sposób oddawania mocy. Nie jest mnożnikiem całej prędkości motocykla.

### Drugi motocykl

Gotowość drugiego motocykla jest stanem zadania personelu, a nie suwakiem setupu. Może uratować sytuację bieg po biegu kosztem wcześniejszego czasu mechaników.

## Czas i mechanicy — BINDING

Zmiana setupu jest zadaniem z czasem wykonania, wymaganym motocyklem i mechanikiem. Część prac wymaga obecności zawodnika; inne mogą być przygotowane wcześniej na podstawie polityki delegowania.

Nie można zatwierdzić kilku zmian na tym samym motocyklu jako równoległych, jeśli fizycznie korzystają z tych samych zasobów.

## Wpływ na Race Engine — BINDING

Setup ma zmieniać parametry zachowania motocykla w konkretnych fazach — start, reakcję, trakcję, odzyskanie prędkości — a nie dodawać końcowe punkty albo ukrytą premię do czasu biegu.

Efekt zależy co najmniej od:

- warunków lokalnej nawierzchni,
- geometrii i długości toru,
- linii jazdy,
- stylu i umiejętności zawodnika,
- jakości wykonania zmiany.

## Relacja z istniejącym kodem

Obecne `BikeSetup` i `SetupResolver` są fundamentem. Nie należy ich zastępować równoległym systemem. Rozszerzenie modelu nastąpi dopiero po integracji Race Engine z Track Engine.

## Do ustalenia — TBD

- legalne i realistyczne zakresy zębatek,
- dokładne krzywe wpływu przełożenia,
- katalog prac przy sprzęgle i reakcji silnika,
- czasy standardowej i przyspieszonej zmiany,
- zakres decyzji zawodnika po odrzuceniu sugestii menedżera.
