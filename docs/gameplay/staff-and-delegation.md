# Personel, zadania i delegowanie

## Zasada — BINDING

Czas staje się interesującym zasobem dopiero wtedy, gdy pracę wykonują konkretne osoby na konkretnym sprzęcie.

Personel nie jest procentowym bonusem do wyniku. Jego rola polega przede wszystkim na:

- diagnozie,
- przygotowaniu sprzętu,
- wykonaniu decyzji,
- przepustowości pracy w ograniczonym czasie,
- jakości informacji przekazywanych managerowi.

## Granice odpowiedzialności — BINDING

### Manager klubu

Manager:

- wybiera zawodnika i Motor A/B do biegu,
- podejmuje ostateczne decyzje dotyczące czterech głównych regulatorów setupu,
- ustala priorytety pracy,
- decyduje, co ma być wykonane przy ograniczonym czasie,
- korzysta z feedbacku zawodnika i diagnozy mechanika.

Manager **nie** zarządza prywatnym magazynem silników zawodnika, harmonogramem remontów ani częściami wewnętrznymi silnika.

### Zawodnik

Zawodnik:

- wykonuje jazdę,
- przekazuje, co czuł na motocyklu i torze,
- może zasugerować własny kierunek zmiany,
- normalnie wykonuje zatwierdzony setup.

Wyraźny sprzeciw lub odmowa setupu są sytuacją wyjątkową zależną od relacji, zaufania i Niezależności.

### Mechanik / zespół mechaników

Mechanik:

- przygotowuje Motor A i Motor B,
- interpretuje feedback zawodnika,
- diagnozuje prawdopodobne przyczyny,
- proponuje korekty,
- wykonuje zatwierdzone prace,
- obsługuje drobne parametry techniczne, których manager nie musi ręcznie ustawiać, np. ciśnienie opon.

Mechanik nie tworzy dodatkowej mocy silnika jako procentowy bonus.

### Tuner

Tuner należy przede wszystkim do technicznego zaplecza zawodnika, a nie do bieżącej kolejki prac managera klubowego.

Tuner odpowiada za:

- budowę i rozwój silników,
- głęboki serwis i remonty,
- długoterminową charakterystykę jednostek,
- stabilność i powtarzalność pakietu silnikowego.

Decyzje o remoncie, serwisie lub nowej jednostce odbywają się w tle. Manager klubu nie wybiera ich ręcznie.

Wpływ tunera jest widoczny pośrednio przez charakterystykę, stabilność i sprzętową dyspozycję, a nie przez prosty bonus do wyniku.

## Kompetencje mechanika — PROVISIONAL

Model mechanika ma pozostać oszczędny.

Na obecnym etapie wystarczają dwie odrębne kompetencje:

- **Diagnoza** — jak trafnie mechanik łączy objawy z możliwą przyczyną,
- **Setup** — jak dobrze potrafi przełożyć decyzję na poprawne przygotowanie motocykla.

Nie dodajemy na tym etapie osobnych ratingów:

- szybkość pracy,
- przygotowanie,
- praca pod presją,
- niezawodność,
- wiedza o silnikach.

Jeśli podczas implementacji okaże się, że któraś z tych cech opisuje odrębne i potrzebne zjawisko, można ją ponownie rozważyć. Nie należy dodawać jej wyłącznie dla większej liczby statystyk.

## Kompetencje tunera — PROVISIONAL

Nie ustalono jeszcze finalnego zestawu widocznych cech tunera.

Dozwolony kierunek modelu:

- ogólna jakość pracy,
- powtarzalność,
- preferowana / typowa charakterystyka silników.

Nie stosujemy bezpośredniego `Tuner Rating → bonus do prędkości`.

Model tunera ma wpływać na rozkład i stabilność jakości sprzętu, a nie gwarantować identyczne jednostki.

## Zasoby i dostępność — BINDING

Każda osoba wykonująca pracę meczową ma:

- kalendarz dostępności,
- zestaw kompetencji,
- limit równoległej pracy,
- aktualne zadanie.

Motocykl, stanowisko, części i narzędzia również mogą być zasobami blokującymi zadanie.

Nie dodajemy osobnego systemu „zmęczenia mechanika” bez późniejszej, konkretnej potrzeby gameplayowej.

## Kolejka pracy — BINDING

Zadanie przechodzi przez stany:

`Planned → Queued → InProgress → Completed | Interrupted | Failed | Expired`

Zmiana priorytetu nie cofa automatycznie kosztu już wykonanej pracy. Przerwane zadanie zapisuje, czy można je wznowić i jaki stan pozostawiło.

## Dwa motocykle i równoległość — BINDING

Motor A i Motor B są osobnymi zasobami.

Pozwala to m.in.:

- przygotowywać alternatywny setup na drugim motocyklu,
- zachować jeden motocykl w gotowości,
- szybko przejść na inną charakterystykę lub wcześniejszą konfigurację,
- wykonywać część prac równolegle, jeśli są dostępni mechanicy i stanowiska.

Nie można wykonywać jednocześnie dwóch kolidujących prac na tym samym motocyklu.

## Delegowanie — BINDING

Gracz może ustawić polityki zamiast potwierdzać każdą drobną czynność.

Przykłady:

- mechanik sam wykonuje drobne korekty techniczne zgodne z przyjętym planem,
- cztery główne regulatory setupu pozostają decyzjami managera, chyba że gracz jawnie je oddeleguje,
- przy zbyt krótkim czasie zawodnik wybiera między ostatnim setupem a przygotowanym drugim motocyklem,
- asystent przygotowuje legalne warianty rezerwy,
- mechanicy utrzymują drugi motocykl lidera w określonej gotowości.

Polityka musi mieć jawny zakres, priorytet i sposób rozstrzygania konfliktu. Delegowanie nie oznacza automatycznie decyzji optymalnej.

## Feedback i diagnoza — BINDING

Podstawowa pętla techniczna:

**zawodnik czuje → zawodnik opisuje → mechanik interpretuje → manager decyduje → mechanik wykonuje → kolejny bieg weryfikuje**

Jeden objaw może mieć kilka przyczyn.

Przykładowo „mieli na wyjściu” może wynikać z:

- przełożenia,
- zapłonu,
- lokalnej przyczepności,
- charakterystyki silnika,
- długości motocykla,
- sposobu wykonania przez zawodnika.

Dlatego dobra Diagnoza nie powinna ujawniać graczowi pewnej odpowiedzi, tylko zawężać prawdopodobne przyczyny.

## Jakość wykonania — PROVISIONAL

Rezultat zadania może zależeć od:

- kompetencji mechanika,
- dostępnego czasu,
- kompletności feedbacku,
- stanu sprzętu.

Nie ustalono jeszcze, czy samo fizyczne wykonanie zatwierdzonej zmiany ma posiadać istotny losowy błąd. Preferowany kierunek to niewielka niepewność, aby gameplay koncentrował się na diagnozie i decyzji, a nie na losowym psuciu poprawnych poleceń.

## Projekt interfejsu — BINDING

Po biegu interfejs powinien eksponować niewielką liczbę decyzji o największym wpływie.

Dla sprzętu podstawowe pytania gracza to:

1. Motor A czy Motor B?
2. Czy zmienić przełożenie?
3. Czy zmienić zapłon?
4. Czy zmienić mieszankę?
5. Czy zmienić długość motocykla?
6. Które prace zdążymy wykonać przed następnym biegiem?

Szczegółowa kolejka pozostaje dostępna, lecz gra nie wymaga ręcznego zarządzania częściami silnika, remontami ani kilkunastoma mikroparametrami.

## Do ustalenia / kalibracji

- finalny sposób prezentacji kompetencji mechanika,
- finalny model jakości i specjalizacji tunera,
- liczebność mechaników przy zawodniku / drużynie,
- dokładne czasy poszczególnych prac,
- domyślne polityki delegowania,
- wpływ ograniczonego czasu na trafność diagnozy i wykonanie.
