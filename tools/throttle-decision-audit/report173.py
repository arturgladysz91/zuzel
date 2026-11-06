"""Write the diagnosis from completed receipts; no simulations or tuning."""
import json
from pathlib import Path
from statistics import mean

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]
DOCS=ROOT/'docs/experiments/throttle-decision-audit'


def report(evidence):
    s=json.loads((DOCS/'summary173.json').read_bytes())
    receipts=[json.loads(p.read_bytes()) for p in evidence.glob('*.done.json')]
    cpu_csharp=sum(r['CpuSeconds'] for r in receipts)
    cpu_all=cpu_csharp+sum(json.loads(p.read_bytes())['CpuSeconds'] for p in evidence.glob('*.cpu.json'))
    cpu_all+=json.loads((evidence/'archive-analysis-cpu.json').read_bytes())['CpuSeconds']
    mechanisms={r['mechanism']:r for r in s['mechanisms'] if r['region']=='straight'}
    strongest=max(s['actual_straight_drop_events'],key=lambda r:r['drop'])
    native={r['arm']:r for r in s['native']}
    denominator=s['s3_straight_decisions']
    window=denominator-mechanisms['NO_BOOST_WINDOW']['count']
    strict=mechanisms['ENTRY_SPEED_BUDGET']['count']+mechanisms['CONTINUATION_ENTRY_BUDGET_ONLY']['count']
    text=f'''# Badanie #173 — dlaczego #172 odpuszcza gaz

Baza: `c5f0a4cc4de34fc4c8c47ef912cafabf34055f29`, Draft PR #171.
Nowa gałąź: `experiment/throttle-decision-audit-173`. Diagnostyka OFF,
bez zmian fizyki, historycznego #172, treningu ani ponownej optymalizacji linii.

**Wynik diagnostyki: przyczyną niskiej propozycji jest przede wszystkim
historyczna referencja gazu/prędkości. Późniejsze zwiększenie gazu blokuje
głównie `CanPrepare`, wprost lub w kontroli kontynuacji.** Największe faktyczne
skoki w dół powstają przy odmowie dalszego impulsu przez kontrolę kontynuacji,
która zwraca znacznie niższy gaz bazowy. Nie są wywołane odcięciem silnika.

W autentycznym P7 natywnie sfalsyfikowano konieczność odmowy **jednego impulsu**:
95% przez jeden okres decyzyjny przy historycznym skręcie pozwoliło legalnie
ukończyć cały następny łuk i 20 m prostej. Nie udowodniono bezpieczeństwa
ciągłego pełnego gazu ani poprawy całego S3.

**PASS diagnostyczny oczekuje na pełny Windows CI końcowego SHA oraz osobną
kompilację/smoke narzędzia #173.** Końcowy wynik zostanie uzupełniony w Draft PR.
Nie stosujemy tutaj progu poprawy czasu ani werdyktu sportowego.

[Diagram i audyt kodu](AUDYT.md) · [Tabele A/B/C i każda prosta](TABELE.md) ·
[Wyniki maszynowe](summary173.json) · [Manifest](SHA256SUMS).

## Dane i granica przypisania przyczyny

Odczytano obie serie #172, protokół i dane rozwojowe. Podstawą przypisania są
pięć zamrożonych K4. Odtworzono kolejno **1438/1438** decyzji z identycznymi
akcjami, propozycjami, wynikami Exit, trybami i licznikami prognoz/sił.
**1413** decyzji przypada przed trzecią metą, w tym **505** na prostych;
pozostałe **25** należy do autonomicznej sekundy rezerwy. Tabela udziałów
dotyczy S3, a pełne dane i wykresy zachowują również rezerwę.

Archiwalny `Tracking.Baseline` jest wynikiem Exit, a nie pierwotnej Policy.
Podczas K4 pole `Tracking.Gate` zasłania pierwotny powód Exit. Dlatego nie
przypisywano tych przyczyn na podstawie samych niskich obrotów/gazu ani
korelacji z pozycją. Odczytowy adapter dodaje tylko obserwacje do kopii klas;
generator sprawdza dokładne kotwice. Historyczne pliki pozostają nietknięte.
Wykonane wersje źródeł r1/q1/n1 zachowano w archiwum z hashami z początku faz.

Dla bazowej Policy policzono także efekt usunięcia pojedynczego składnika
przy zachowaniu innych i zacisku 0..1. Ujemny składnik, który po usunięciu
wciąż pozostawia gaz równy zero, nie jest zaliczany jako skuteczne ograniczenie.
Efekty nie są addytywne. Oddzielnie raportujemy późniejsze weto zwiększenia
gazu oraz rzeczywistą zmianę aktywnej komendy w czasie.

`decisions173.jsonl.gz` zawiera każdą decyzję, fazę, geometrię, wszystkie
warstwy gazu, skręt żądany/zaakceptowany/aktywny i fizyczny, β/βdot/yaw,
RPM, koło, pending, pose/swept oraz ledger sił/napędu poprzedniego interwału.
Fx/Fy i moment z tego ledgera dotyczą właśnie zakończonego kroku, nie
niewykonanej komendy. Prognozy zarejestrowane w rekonstrukcji mają osobne
receipty warstwy, horyzontu i kandydata. Punkty +0,24/+0,44/+0,84 są dostępne
tylko wtedy, gdy dany przebieg rzeczywiście je obejmował. Brak prognozy nie
oznacza bezpieczeństwa; nie dopisano brakujących przyszłych stanów wału/sił,
których API predyktora nie zwraca.

## Łańcuch ograniczania

1. Teacher jest przestrzennym fitem stanu i **aktywnego** gazu demonstracji.
   CyclicReference tworzy profil lotny i proste. Na prostej korzysta m.in.
   z `0.22 + 0.16*StraightBoost*cos(pi*phase)` oraz przygotowania wejścia.
   Dla StraightBoost≈2,430 ta część ma zakres około −0,169…0,609 przed
   połączeniami i zaciskiem. Jest to referencja sterownika, nie prawo fizyczne.
2. Policy przewiduje stan aktywacji po 0,24 s. Gaz to `target.Drive +
   0.14*(target.Speed-predicted.Speed) - slipPenalty`. Kara poślizgu działa
   tylko powyżej κ=0,25 i jest mnożona przez błąd kursu. Nie ma osobnego
   bezpośredniego cięcia gazu od samego β lub offsetu. Błędy te wpływają na skręt
   i dalszą ocenę kandydatów.
3. Exit w swoim oknie proponuje `mix(original,1,blend)`. Poza oknem zwraca
   referencję/zaplanowane odpuszczenie. Najpierw sprawdza CanPrepare, potem
   siatkę skrętu przez 0,44 s i kontynuację do 0,84 s. Odmowa może nadal
   pozostawić aktywną stabilizację skrętu, lecz gaz wraca do oryginalnego prawa.
4. K4 porównuje bazowy gaz z jedną korektą `baseline + 0.24*speedDelta`.
   Tempo=0; główna korekta pochodzi z różnicy profili K4/K0. Nie jest to
   swobodne wyszukiwanie całego zakresu gazu ani optymalizacja pełnej prostej.
   W analizowanej serii wszystkie ocenione krótkie propozycje przechodziły
   bramki twardej legalności i tolerancji trackera; część przegrywała kosztem.
5. Końcowa kontrola przejścia nie nadpisała żadnej z 1438 propozycji.
   Nowa komenda działa dopiero po **0,24 s**. Każda aktualna aktywacja i pełne
   pending zostały sprawdzone z historią. Gaz żądany i aktywny są rozdzielone.

## Jedenaście odpowiedzi

**1. Który mechanizm najczęściej ogranicza gaz na prostych?**

Na etapie powstawania propozycji **niska referencja** ma niezerowy wpływ
na Policy w **{mechanisms['REFERENCE_LOW_DRIVE']['count']}/{denominator}
({mechanisms['REFERENCE_LOW_DRIVE']['percent']:.2f}%)** decyzji na prostych.
Ujemna korekta prędkości ma niezerowy wpływ po zacisku w
**{mechanisms['SPEED_TARGET_REACHED']['count']}/{denominator}
({mechanisms['SPEED_TARGET_REACHED']['percent']:.2f}%)**. To składniki bazowej
propozycji; Exit może je następnie przykryć impulsem. Główną późniejszą
bramką blokującą większy gaz jest CanPrepare. Te dwie odpowiedzi opisują
różne etapy i nie wolno ich zsumować.

**2. Który odpowiada za największe ograniczenia?**

Największy faktyczny spadek aktywnego gazu na prostej to
**{strongest['drop']*100:.2f} p.p.**, z {strongest['previous']:.3f} do
{strongest['drive']:.3f}, {strongest['trial']}, aktywacja
{strongest['activation']:.3f} s przy {strongest['speed_kph']:.2f} km/h.
Powód: **roll-off:continuation**. Duża różnica bierze się z zakończenia
impulsu i powrotu do niskiej Policy. Odmowy kontynuacji blokowały średnio
{s['foregone_by_mechanism']['CONTINUATION_REJECT']['mean']*100:.2f} p.p.
proponowanego zwiększenia, a bezpośredni CanPrepare
{s['foregone_by_mechanism']['ENTRY_SPEED_BUDGET']['mean']*100:.2f} p.p.
Nie jest to dodatkowe odejmowanie tych wartości od gazu bazowego.

**3. Jak często profil prędkości, a jak często prognozowane zagrożenie?**

Skuteczny wpływ korekty prędkości: {mechanisms['SPEED_TARGET_REACHED']['percent']:.2f}%
decyzji prostych, średnio {mechanisms['SPEED_TARGET_REACHED']['mean_drop']*100:.2f}
p.p. na Policy. W zbadanym S3 **0/{s['diagnostic_gate_counts']['Exit 0,44 s']['pass']}
krótkich kandydatów Exit i 0/{s['diagnostic_gate_counts']['Tracker 0,44 s']['pass']}
kandydatów trackera** odrzucono przez ich bramki legalności/tolerancji.
Nie oznacza to, że każdy możliwy większy gaz był bezpieczny: heurystyka
wcześniej wykluczała część propozycji. Settled i CanPrepare są wymaganiami
kontrolera o motywacji fizycznej, nie dowodami rzeczywistego DNF.

**4. Czy CanPrepare jest dominującym powodem odpuszczania?**

Tak jako **późniejsze weto zwiększenia**, nie jako źródło bazowego gazu.
Na prostych działa bezpośrednio w {mechanisms['ENTRY_SPEED_BUDGET']['count']}
decyzjach ({mechanisms['ENTRY_SPEED_BUDGET']['percent']:.2f}%). Kolejne 46
odmów kontynuacji ma wyłącznie nieprzechodzący CanPrepare; 19 miesza ten
powód z Settled. W oknie boost jest {window} decyzji: co najmniej
**{strict}/{window} ({100*strict/window:.2f}%)** odmawia wyłącznie z tego
powodu na pierwszej bramce albo we wszystkich ocenionych ogonach.
Poza oknem boost jest 233/505 decyzji — to kontekst generowania, nie
automatycznie 233 cięcia gazu. Zwykłe wyjście K4 poza okno nie jest
awaryjnym PLAN_FALLBACK.

**5. Czy parametry mają potwierdzenie w dynamice?**

W repozytorium nie znaleziono pomiaru uzasadniającego **0,65 m/s²**.
To stała modelu przygotowania. CanPrepare zakłada także 0,2 s przyspieszania
po 3,5 m/s² i cel wejścia **{s['entry_budget_kph']:.3f} km/h**,
czyli FlyingEntrySpeed z Teacher + 1,6 m/s. Headroom=1,6 wybrano w #170
w porównaniu manewrów, a nie w badaniu maksymalnego legalnego wejścia.
Zachowane SecondExtraHeadroom=1 nie jest używane przez adapter #172.

Z {s['coast_samples_s3']} natywnych interwałów S3 z gazem zero, V>60 km/h,
|β|<3°, |yaw|<0,2 i oboma końcami na prostej otrzymano medianę redukcji
**{s['coast_median_s3']:.3f} m/s²**, średnią ważoną czasem
**{s['coast_weighted_mean_s3']:.3f} m/s²**. W {100*s['coast_below065']:.2f}%
czasu tej próby lokalna redukcja była mniejsza niż 0,65. Nie jest więc
uniwersalnym, udowodnionym dolnym ograniczeniem. Stan wału/koła, sprzęgło,
pending i ruch boczny mają znaczenie. Sama wymiana 0,65 na medianę również
nie certyfikuje bezpiecznego wejścia.

Co ważniejsze, legalny natywny P7 wszedł w łuk przy **78,49 km/h**, znacznie
powyżej przyjętego celu 73,66. Ten cel nie jest fizyczną granicą dla tego
stanu. Nie znamy maksymalnego legalnego wejścia dla wszystkich stanów.

**6. Czy są lokalnie wykonalne większe propozycje?**

Tak. Dziewięć deterministycznie wybranych momentów obejmuje pierwsze wyjście,
lotne wyjścia, środek prostej, przygotowanie łuku i pole zewnętrzne.
**180/180** prognoz do 0,84 s przechodzi β45 i swept≥0,5 m. Porównano
historyczny gaz, +20 p.p., 95% i 100%, najpierw przy identycznym skręcie,
potem w małej z góry ustalonej siatce skrętu. Każdy wariant jest zachowany.
Oddzielono utrzymanie gazu od pojedynczego impulsu z następnym odpuszczeniem.
W wielu wariantach CanPrepare nadal odmawia mimo lokalnej legalności.

To prognozy, nie przejazdy. Dla P1–P6/P8/P9 konieczność odpuszczenia poza
horyzontem pozostaje **UNKNOWN**. Dla P7 wykonano cztery krótkie ramiona
natywne i potwierdzono, że pojedynczy większy impuls nie był fizycznie
wykluczony także przez cały następny łuk.

**7. Przyczepność tylna czy ostrożność polityki?**

W konkretnych odtworzonych odmowach dominują referencja i heurystyczna
bramka prędkości wejścia, nie wykazane przekroczenie budżetu opony.
Kara wheelspinu ma niezerowy wpływ na bazę w
{mechanisms['WHEELSLIP_REDUCTION']['count']}/{denominator}
({mechanisms['WHEELSLIP_REDUCTION']['percent']:.2f}%), średnio
{mechanisms['WHEELSLIP_REDUCTION']['mean_drop']*100:.2f} p.p.
Nie jest głównym wytłumaczeniem dużych odpuszczeń. Fizyczna konkurencja
Fx/Fy pozostaje rzeczywista w modelu; P7 z 95% zwiększył β z 11,70° do 19,39°,
ale pozostał legalny. Krótka wykonalność nie dowodzi bezpiecznego ciągłego
pełnego gazu. Nie zmieniono μ, transferu nacisku ani prawa kontaktu.

**8. Czy profil jest sztucznym pułapem prędkości?**

Tak jako **miękka referencja sterowania** i źródło sztywnego kryterium wejścia.
Po przekroczeniu celu feedback odejmuje gaz, a K4 daje ograniczoną korektę
względną. Nie ma zacisku prędkości nadwozia. Około 80 km/h wynika z tego,
jak długo kontroler dopuszcza napęd, i z dalszej geometrii — nie z instrukcji
ustawiającej V=80. Nie wykazano optymalności obecnego profilu.

**9. Czy około 80 km/h jest granicą fizyczną?**

Nie jest twardym limitem silnika lub nadwozia w tym modelu. Engine500
odcina moment przy 12000 RPM; najwyższy obserwowany wał miał około
{s['rpm_max']:.0f} RPM, przy postępie {s['rpm_max_location']:.1f} m.
W P7 uzyskano natywnie **{native['95-fixed']['maximum_kph']:.3f} km/h** i
legalne wejście 78,49 zamiast 72,66 km/h. Maksymalna wykonalna prędkość
na całej prostej z następnym łukiem pozostaje nieustalona. Wyników idealnej
prostej #168 nie przenosimy na te warunki i nie ogłaszamy 110–120 km/h.

**10. Jakie dane rozstrzygną pozostałe niewiadome?**

Potrzebna jest stanowa obwiednia wykonalnych wejść: offset/χ/β/yaw,
prędkość, wał/koło/sprzęgło, aktywny napęd i cała kolejka muszą przejść
przez cały kolejny łuk, z oryginalnymi aktuatorami i z przyczynowym
odpuszczeniem/korektą. Trzeba zapisać odrzuconą i przyjętą sekwencję,
pierwszą fizyczną przyczynę odmowy, marginesy i błąd prognozy względem
natywnego świata. Sama prędkość lub pojedyncza średnia redukcji nie wystarczy.
Pozostają też pełne dopasowane S3 nowej reguły — nie zostały wykonane w #173.

**11. Jaka jedna zmiana ma największy potencjał i jak ją sfalsyfikować?**

Najlepiej uzasadniona hipoteza: **zastąpić twarde weto CanPrepare oparte na
Teacher+1,6 i stałym 0,65 stanową oceną dopuszczenia pojedynczego impulsu,
sprawdzającą ciągłą wykonalność aż przez następny łuk**. Dotychczasowy cel
może zostać kosztem miękkim. Nie podnosić profilu globalnie ani usuwać
zabezpieczeń bez zastępczego dowodu wykonalności. To propozycja następnego
badania, nie zaimplementowana nowa polityka.

Przed próbami zamrozić tę jedną zmianę, resztę kontrolera i fizykę.
Porównać na nowych identycznych parach pełne S3 z #172. Hipotezę odrzucić,
jeśli zysk krótkiego okna nie obniża mediany pełnego S3, pogarsza legalność,
prowadzi do DNF, narusza β45/swept0,5 lub ma nieakceptowalny koszt decyzji.
Większe Vmax samo nie wystarczy. Nie rozpoczęto tego badania.

## Kontrolowana próba natywna P7

P7: t=11,8 s, s≈163,587 m, V≈77,059 km/h, około 30,802 m do drugiego
łuku; historyczna komenda gazu zero. Rozwidlenie zachowuje identyczny stan
ciała, koła, wału, sprzęgła, aktywnej komendy i pending. Wszystkie cztery
ramiona mają ten sam hash obserwacji. **1207 próbek fizycznego prefiksu**
jest identycznych aż do aktywacji 12,04 s, włącznie z siłami i napędem;
baseline odpowiada dokładnie całemu odpowiadającemu prefiksowi #172.

Jedna komenda różni się w t=11,8. Od następnego review działa oryginalny
zamrożony K4, widzący rzeczywiście zmieniony pending i stan. Ramię sterowania
dobrano przed próbą natywną z zapisanej siatki lokalnych prognoz. Zmiana
skrętu jest osobnym ramieniem przy tym samym 95%, nie ukrytą zmianą fizyki.
Cel to rzeczywiste wyjście następnego całego łuku +20 m, s≈311,779 m.
To nie trzy okrążenia ani nawet zaliczona pierwsza meta. Wszystkie cztery
ramiona osiągnęły cel legalnie; zero DNF, wszystkie końcowe stany zachowano.

95-fixed: do wspólnego celu **{native['95-fixed']['goal_time']:.6f} s**
wobec **{native['baseline']['goal_time']:.6f} s** bazy,
różnica **{native['95-fixed']['gain_to_local_goal_seconds']:.6f} s**.
Minimum swept po rozwidleniu **{native['95-fixed']['minimum_sweep']:.3f} m**.
To dowód zbędności konkretnej odmowy pojedynczego impulsu, nie oszacowanie
zysku wyścigu ani dowód poprawy wszystkich stanów.

![Natywne P7](173-native-P7.png)

## Budżet, weryfikacja i dowody

**4/6 nowych krótkich światów**, zero nowych treningów i pełnych S3.
Zarejestrowane wykonania C# (rekonstrukcja, kontrfaktyczne prognozy,
natywne ramiona i geometria) zużyły **{cpu_csharp:.5f} CPU-s**.
Razem z rejestrowanymi analizami Python i dwoma renderowaniami:
**{cpu_all:.5f}/100 CPU-s**. Kompilacja, lokalna regresja historyczna i CI
są raportowane osobno. Nie jest to koszt całej sesji edytorskiej.
Natywne próby zamknięto; nie używano dwóch pozostałych slotów dla samego
wykorzystania limitu. Nie dostrajano kontrolera po wynikach.

`173-diagnostics.zip` zawiera pełne logi rekonstrukcji, 180 prognoz,
wybrane stany, receipty CPU, protokoły, zamknięcie prób i wykonane wersje
źródeł. `173-native.zip` zachowuje cztery pełne natywne historie,
decyzje, plany, wynik i rozwidlenie. `INPUTS.json` pinuje starsze źródła,
profile i archiwa. `SHA256SUMS` obejmuje kod, nowe dane, raport i wykresy.
Konfiguracja CI ma dodatkowo tożsamość w końcowym commicie Git.

Po zakończeniu prób uzupełniono wyłącznie diagnostykę: bogatszy eksport
obserwacji/geometrii, zachowanie błędnego prefiksu i blokadę dalszych prób.
Wersje faktycznie wykonane są dostępne z dokładnymi hashami. Historia #172,
jego fizyka, plany i zarchiwizowane wyniki pozostają niezmienione.

Lokalna walidacja: kompilacja narzędzia i rozwiązania Release z warnaserror,
172/172 dobranych testów C#, 443/443 testy Python kalibracji, 7/7 reguł CI,
124/124 Node, generator, kontrola manifestów i wspólnych prefiksów.
Lokalny restore korzystał z -p:NuGetAudit=false z powodu niedostępnej usługi
audytu NuGet w tym środowisku; CI zachowuje zwykły restore i audyt.
Pełny Windows CI końcowego SHA i osobny build #173 są wymagane przed
ostatecznym PASS diagnostycznym; ich końcowy wynik będzie w opisie Draft PR.

## Wykresy i odtwarzanie

Pełny S1 pokazuje wszystkie trzy okrążenia i sekundę rezerwy; trzy pionowe
linie to mety, szare pasy to łuki. Referencja jest tą używaną dla prognozowanej
aktywacji +0,24 s, a nie arbitralnie przesuniętą prędkością bieżącą.
Wykresy prostych są osobne dla wszystkich pięciu biegów. Ostatnia home
obejmuje tylko zaobserwowaną część do końca rezerwy. Zielona/purpurowa/czerwona
linia oznacza ≥95%, późniejszy spadek <95% i <70%. Pasek opisuje bramkę
w chwili decyzji, a czarna linia rzeczywistą późniejszą aktywację.

![Łańcuch decyzji](173-lancuch.png)

![Siły i stabilność](173-fizyka.png)

![Bramki i udziały czasu](173-bramki-czas.png)

![Wszystkie proste S1](173-proste-S1.png)

[Proste S2](173-proste-S2.png) · [Proste S3](173-proste-S3.png) ·
[Proste S4](173-proste-S4.png) · [Proste S5](173-proste-S5.png).

Instrukcja odczytu, regeneracji i granic kosztu jest w
`tools/throttle-decision-audit/README.md`. Automatyczna weryfikacja używa
zapisanych dowodów i nie uruchamia nowych światów. Bez merge i bez następnego badania.
'''
    (DOCS/'RAPORT.md').write_text(text,encoding='utf-8')


if __name__=='__main__':
    import sys
    report(Path(sys.argv[1]))
