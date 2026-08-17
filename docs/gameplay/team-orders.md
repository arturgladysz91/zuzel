# Polecenia drużynowe i jazda parą

## Zasada — BINDING

Polecenie zmienia priorytety decyzyjne zawodnika, ale nie gwarantuje wyniku ani nie przejmuje sterowania motocyklem. Wykonanie zależy od sytuacji, umiejętności jazdy parą, stylu, zaufania, widoczności rywala i dostępnej przestrzeni.

Polecenie nie może nakazywać nielegalnego ustawiania wyniku, kolizji ani ignorowania bezpieczeństwa.

## Kandydaci do pierwszej wersji — PROVISIONAL

- **Jedź swobodnie** — zawodnik używa własnego modelu decyzji.
- **Zabezpiecz 3:3** — ogranicz ryzyko obustronnego nieudanego ataku i pilnuj pozycji dających remis biegowy.
- **Atakuj po 5:1** — zwiększ gotowość do skoordynowanego ataku, akceptując większe ryzyko utraty układu.
- **Chroń partnera** — dopasuj linię i tempo, jeśli partner znajduje się w pozycji możliwej do obrony.
- **Lider atakuje, partner zabezpiecza** — rozdziel role przed startem.
- **Nie walcz z partnerem** — unikaj manewru, który ma małą wartość drużynową i wysokie ryzyko kontaktu.

Lista nie jest jeszcze zatwierdzona do implementacji. Przed kodowaniem trzeba sprawdzić, czy każde polecenie tworzy odmienny, czytelny kompromis.

## Cykl polecenia — BINDING

1. menedżer wybiera intencję oraz zawodników,
2. system sprawdza legalność, czas i możliwość kontaktu,
3. polecenie jest przekazywane i potwierdzane,
4. zawodnik przyjmuje, modyfikuje albo odrzuca instrukcję,
5. Race Engine otrzymuje priorytet taktyczny, nie gotowy wynik,
6. po biegu log wyjaśnia, czy i dlaczego polecenie miało wpływ.

## Ograniczenia informacji — BINDING

Zawodnik nie zna przyszłej trajektorii rywala. Polecenia wykorzystują tę samą niepełną informację i ten sam snapshot co pozostałe decyzje. Nie mogą odblokowywać wiedzy o ukrytym stanie toru.

## Polecenia przygotowane wcześniej — PROVISIONAL

Menedżer może tworzyć proste warunki, np. „jeżeli po starcie prowadzimy 5:1, priorytetem jest ochrona partnera”. Wcześniej omówiony plan zmniejsza koszt komunikacji w krótkim oknie, ale nadal wymaga rozpoznania sytuacji przez zawodników.

## Do ustalenia — TBD

- ostateczna lista poleceń pierwszej wersji,
- które polecenia dotyczą jednego zawodnika, a które pary,
- wpływ sprzecznych preferencji zawodników,
- sposób prezentowania stopnia wykonania bez zdradzania wewnętrznych wag AI.
