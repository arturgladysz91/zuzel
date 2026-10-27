# Tabele wyników #139

Wartości z natywnych zapisów. Czasy od startu scenariusza; brakujące bramki nie są ekstrapolowane. Pełne pola w native-summaries.json i telemetry/.

## Wszystkie światy

|Próba|Wynik|Czas s|Historyczna bramka s|Pełna następna prosta|Max β °|Min certyfikat m|CPU s|
|---|---|---:|---:|---|---:|---:|---:|
|off-D0|completed|17.000|16.000|NIE / poza zakresem OFF|9.243|0.113|12.141|
|off-D1|completed|17.870|16.870|NIE / poza zakresem OFF|13.513|0.147|13.938|
|D0-A|control envelope|7.520|—|NIE / poza zakresem OFF|45.639|0.117|6.078|
|D0-B|control envelope|7.510|—|NIE / poza zakresem OFF|46.087|0.117|5.875|
|D0-C|completed|19.291|16.380|TAK|15.136|0.105|14.891|
|D1-A|control envelope|17.310|16.850|NIE / poza zakresem OFF|45.725|0.147|14.000|
|D1-B|control envelope|17.290|16.800|NIE / poza zakresem OFF|45.973|0.147|14.094|
|D1-C|completed|19.665|16.800|TAK|13.513|0.147|15.188|
|sensitivity-D0-front-slope-half|uncertified interval|2.550|—|NIE / poza zakresem OFF|2.651|-0.056|0.344|
|sensitivity-D0-rear-long-scale2|control envelope|7.570|—|NIE / poza zakresem OFF|45.478|0.457|0.125|
|sensitivity-D0-front-load40|uncertified interval|3.250|—|NIE / poza zakresem OFF|3.918|-0.065|0.062|
|sensitivity-D0-yaw-inertia125|control envelope|7.710|—|NIE / poza zakresem OFF|45.546|1.219|0.266|
|sensitivity-D1-front-slope-half|uncertified interval|2.870|—|NIE / poza zakresem OFF|1.551|-0.043|0.031|
|sensitivity-D1-rear-long-scale2|uncertified interval|14.910|—|NIE / poza zakresem OFF|13.828|-0.002|0.188|
|sensitivity-D1-front-load40|uncertified interval|3.730|—|NIE / poza zakresem OFF|1.917|-0.065|0.062|
|sensitivity-D1-yaw-inertia125|control envelope|17.350|16.790|NIE / poza zakresem OFF|45.776|0.389|0.219|
|H0-A|control envelope|7.290|—|NIE / poza zakresem OFF|45.193|0.126|5.766|
|H0-B|control envelope|7.260|—|NIE / poza zakresem OFF|45.282|0.126|5.734|
|H0-C|completed|18.774|16.040|TAK|12.087|0.093|14.516|
|H1-A|control envelope|16.060|15.680|NIE / poza zakresem OFF|45.716|0.117|11.406|
|H1-B|control envelope|16.400|15.860|NIE / poza zakresem OFF|45.810|0.117|12.016|
|H1-C|completed|18.763|15.860|TAK|16.738|0.117|13.234|

## Wyjście z badanego łuku i jego następna prosta

Punkty to pierwsze rzeczywiste próbki po przekroczeniu referencji, bez interpolacji. Prędkość w km/h. Pełna certyfikacja następnej prostej po drugim łuku jest osobną kolumną wyżej.

|Próba|Żądanie / aktywacja gaz1 s|Wyjście s|Gaz wyjście|v wyjście|β wyjście °|yaw rad/s|v +20 m|v +40 m|v +62 m|
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|
|D0-A|6.400 / 6.640|6.709|1.000|61.527|1.674|0.200|—|—|—|
|D0-B|6.400 / 6.640|6.758|1.000|60.618|2.828|0.255|—|—|—|
|D0-C|6.400 / 6.640|6.758|1.000|60.618|2.828|0.255|71.286|76.762|73.506|
|D1-A|16.200 / 16.440|16.621|1.000|64.098|2.563|0.366|—|—|—|
|D1-B|16.200 / 16.440|16.572|1.000|62.969|2.356|0.308|—|—|—|
|D1-C|16.200 / 16.440|16.572|1.000|62.969|2.356|0.308|71.212|76.685|73.472|
|H0-A|6.200 / 6.440|6.577|1.000|65.297|2.635|0.258|—|—|—|
|H0-B|6.200 / 6.440|6.613|1.000|65.849|3.025|0.410|—|—|—|
|H0-C|6.200 / 6.440|6.613|1.000|65.849|3.025|0.410|74.570|80.179|77.892|
|H1-A|15.000 / 15.240|15.454|1.000|64.080|3.808|0.521|—|—|—|
|H1-B|15.200 / 15.440|15.613|1.000|59.002|1.762|0.189|—|—|—|
|H1-C|15.200 / 15.440|15.613|1.000|59.002|1.762|0.189|69.590|76.233|73.061|

## Wszystkie DNF

Ostatni stan może już naruszać obwiednię. Siły to ostatni ukończony przedział, nie ponowne zapytanie dt=0 w stanie końcowym. Brak certyfikacji nie dowodzi przekroczenia bandy.

|Próba|Powód|Legalny czas s|Końcowe β °|yaw rad/s|Moment przód / tył Nm|Fx / Fy tył N|Slip|RPM|
|---|---|---:|---:|---:|---|---|---:|---:|
|D0-A|control envelope|7.510|45.639|2.269|581.783 / -570.306|282.467 / 950.509|0.806|6315.315|
|D0-B|control envelope|7.500|46.087|2.396|575.954 / -567.697|297.568 / 946.162|0.909|6333.271|
|D1-A|control envelope|17.300|45.725|2.287|560.824 / -568.440|292.986 / 947.399|0.838|6456.511|
|D1-B|control envelope|17.280|45.973|2.378|574.930 / -563.977|316.677 / 939.960|0.929|6719.339|
|sensitivity-D0-front-slope-half|uncertified interval|2.540|1.639|0.030|131.668 / -147.139|3.356 / 245.231|0.001|5050.666|
|sensitivity-D0-rear-long-scale2|control envelope|7.560|45.478|2.146|575.931 / -575.649|249.806 / 959.414|0.843|6038.981|
|sensitivity-D0-front-load40|uncertified interval|3.240|1.773|0.215|194.532 / -197.564|54.400 / 329.272|0.001|4950.805|
|sensitivity-D0-yaw-inertia125|control envelope|7.700|45.546|1.791|576.066 / -587.484|153.217 / 979.143|0.443|4726.839|
|sensitivity-D1-front-slope-half|uncertified interval|2.860|0.930|0.098|94.142 / -92.787|201.753 / 154.645|0.042|4992.785|
|sensitivity-D1-rear-long-scale2|uncertified interval|14.900|1.974|0.731|346.510 / -255.956|6.022 / 426.594|0.002|4176.837|
|sensitivity-D1-front-load40|uncertified interval|3.720|1.838|0.221|197.500 / -204.048|41.443 / 340.082|0.011|5002.483|
|sensitivity-D1-yaw-inertia125|control envelope|17.340|45.776|2.074|587.357 / -574.358|258.310 / 957.263|0.729|6069.986|
|H0-A|control envelope|7.280|45.193|2.704|575.799 / -485.378|576.089 / 808.961|1.816|10415.836|
|H0-B|control envelope|7.250|45.282|2.430|559.806 / -556.529|351.093 / 927.546|0.985|7199.698|
|H1-A|control envelope|16.050|45.716|2.470|560.102 / -555.720|355.204 / 926.199|1.055|7133.224|
|H1-B|control envelope|16.390|45.810|2.681|575.820 / -532.895|442.952 / 888.159|1.412|8161.419|

## Jednoczynnikowe warianty na identycznej taśmie B

Różnice względem B w tej samej absolutnej chwili (20 ms przed wcześniejszym końcem). To różne rozwinięte trajektorie; niższe β przy utracie certyfikacji nie oznacza lepszej jazdy.

|Wariant|Wspólny czas s|Δβ °|Δyaw rad/s|Δv km/h|
|---|---:|---:|---:|---:|
|sensitivity-D0-front-slope-half|2.530|-6.694|-0.563|5.189|
|sensitivity-D0-rear-long-scale2|7.490|-5.876|-0.254|0.464|
|sensitivity-D0-front-load40|3.230|-5.495|-0.278|7.072|
|sensitivity-D0-yaw-inertia125|7.490|-14.618|-0.584|4.743|
|sensitivity-D1-front-slope-half|2.850|-4.872|-0.366|3.126|
|sensitivity-D1-rear-long-scale2|14.890|0.251|-0.027|-1.372|
|sensitivity-D1-front-load40|3.710|-4.357|-0.273|4.671|
|sensitivity-D1-yaw-inertia125|17.270|-5.021|-0.312|1.902|
