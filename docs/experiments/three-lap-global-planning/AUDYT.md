# Audyt sterowania #172

Przed implementacją odczytano BendMap, Planner, Tracker, ExitPolicy, Policy, Teacher, TrackFrame, CornerTopology oraz wykonawcę NativeWorld166.

BendMap poprawnie rozdziela floor całkowitego postępu przez LapMeters, lokalny postęp i logiczny łuk, uwzględniając ujemne pozycje startowe. TrackFrame zawija geometrię, a jego segmenty i CornerTopology wyznaczają rzeczywiste granice trzech sekcji łuku. Profil Teacher kończy się na wyjściu pierwszego łuku. Policy166 obsługuje launch i później odczytuje przesunięty pierwszy profil. Po drugim łuku pozostaje na końcowym węźle. ExitPolicy170 ustawia second na podstawie bezwzględnego postępu i zachowuje tę fazę na kolejnych okrążeniach. Samo cykliczne BendMap w Tracker171 nie naprawia tych zależności.

Planner171 buduje geometrię dla dowolnego BendVisit, lecz pierwszy profil nominalny pochodzi ze startu. Na lotnym pierwszym łuku należy użyć dynamiki lotnego łuku przy zachowaniu właściwej lokalnej geometrii. Referencja prędkości dla lotnego łuku ma odrębny profil i skalę, uprzednio sprawdzoną na drugim łuku. Wszystkie późniejsze łuki powinny być liczone w ich rzeczywistych granicach. Prosta domowa jest sumą dwóch segmentów po obu stronach mety, więc stan odniesienia powinien zależeć od odległości między poprzednim wyjściem a następnym wejściem. Linia mety leży wewnątrz tej ciągłej prostej.

NativeWorld166 prawidłowo nalicza przecięcia n*LapMeters i nie liczy początkowej pozycji zero. Po RequiredLaps zapisuje finish i ustawia RiderActivity.Finished, co odcina moment napędu. Pętla i callback dodatkowo kończą się, gdy finish jest ustawione. Wersja diagnostyczna #172 ma zachować dokładnie trzy kredytowane przecięcia oraz ich interpolowane czasy, a następnie kontynuować ten sam stan Racing, kolejkę i decyzje do sekundy za trzecią metą. Zmiana dotyczy warunków końca diagnostyki. Równania sił, kontaktów i krok Box2D pozostają skopiowane z oryginału bez zmian.

Plan adaptacji obejmuje osobną CyclicReference172, pochodne Policy/Exit/Tracker/Planner i wykonawcę generowane z zamrożonych plików przez sprawdzane zamiany. Cykliczne odniesienia przechodzą przez metę bez resetu. Wyjątek launch dotyczy pierwszego zatrzymanego podejścia. K4 i C0 korzystają z tej samej naprawionej referencji i stabilizatora, a K4 dodatkowo wybiera przestrzenną trajektorię całego łuku.

Test bez świata sprawdzi sześć łuków, 18 sekcji, granice z obu stron, trzy mety i prestart, wraz z ciągłością kursu, offsetu, β, yaw i prędkości docelowej. Dopiero potem C1 obejmie pierwszy lotny łuk, C2 oba łuki drugiego okrążenia i przygotowanie kolejnego, a C3 pełne S3. Każdy DNF zachowa pierwszą przyczynę, zaakceptowany stan i nieodwołalne komendy. Budżet rezerwuje 10 światów na odłożone dopasowane porównania.

Historyczne #161 ma progressive-b i ten sam rodzaj oryginalnych kontaktów, lecz różni się kontrolerem certyfikującym i zestawem startów. Mediana 79,603 s pozostaje kontekstem do czasu porównania pełnych Spec, geometrii i obowiązków rezerwy z nowymi scenariuszami.

