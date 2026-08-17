# Obserwacje i feedback

## Zasada niepełnej informacji — BINDING

CoreSim przechowuje faktyczny stan. Gracz otrzymuje oszacowania tworzone przez źródła informacji. Warstwa gameplay nie może kopiować debugowych wartości do interfejsu i jedynie zaokrąglać ich do procentów.

## Rekord obserwacji — PROVISIONAL

Obserwacja powinna zawierać:

- źródło,
- czas i bieg, z których pochodzi,
- segment, linię albo zakres toru,
- jakościowy opis zjawiska,
- kierunek zmiany,
- poziom pewności,
- czas ważności lub tempo starzenia,
- możliwą sprzeczność z innymi obserwacjami.

Przykłady komunikatów:

- „wewnętrzna na drugim łuku szybko się wypolerowała”,
- „szeroka zaczyna nieść, ale wejście nadal jest luźne”,
- „pole trzecie wygląda wilgotniej niż przed poprzednim biegiem”,
- „zawodnik nie jest pewien, czy problemem był tor, czy motocykl”.

## Źródła — BINDING

- obserwacja menedżera,
- feedback zawodnika po biegu,
- mechanik i dane z oględzin motocykla,
- asystent lub specjalista od toru,
- komunikat osoby oficjalnej,
- prognoza i bieżący pomiar pogody.

Źródło nie daje automatycznie prawdy. Jakość informacji zależy między innymi od czytania toru, jakości feedbacku, doświadczenia personelu, znajomości obiektu, stanu zawodnika i wieku danych.

## Pewność i sprzeczność — BINDING

- Pewność opisuje wiarygodność oszacowania, nie siłę efektu.
- Kilka niezależnych zgodnych źródeł może zwiększać pewność.
- Świeższa informacja nie zawsze jest lepsza, jeśli pochodzi ze słabego źródła.
- Sprzeczne relacje pozostają widoczne; system nie uśrednia ich bez śladu do jednej liczby.
- Gracz może wybrać hipotezę roboczą, lecz nie zmienia to ukrytego stanu toru.

## Starzenie wiedzy — BINDING

Informacja traci aktualność wraz z:

- upływem czasu,
- przejazdami kolejnych zawodników,
- pracami torowymi,
- zmianą pogody,
- zmianą części toru, której obserwacja dotyczyła.

Tempo starzenia jest lokalne. Pole startowe może zmienić się szybciej niż niewykorzystywana część prostej.

## Feedback zawodnika — PROVISIONAL

Feedback powinien rozdzielać:

- co zawodnik poczuł,
- gdzie wystąpiło zjawisko,
- kiedy pojawiło się w biegu,
- czy podejrzewa tor, setup, błąd własny albo zachowanie rywala,
- jak pewny jest diagnozy,
- czego chce spróbować w następnym starcie.

Zawodnik może się mylić, ale komunikat powinien wynikać ze zdarzeń biegu, a nie z losowego generatora tekstu.

## Do ustalenia — TBD

- skala i nazwy poziomów pewności,
- sposób łączenia obserwacji na mapie toru,
- interfejs porównywania hipotez i efektów podjętej decyzji,
- liczba komunikatów widocznych automatycznie po biegu.
