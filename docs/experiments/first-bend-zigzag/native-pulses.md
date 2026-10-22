# Ciągła natywna sekwencja impulsów #126

Wszystkie wartości start/koniec pochodzą z rzeczywistego strumienia Box2D. Różnice sił A/C są zapytaniami z tego samego zaakceptowanego stanu B, nie wykonanymi trajektoriami. Zmiany native zawierają jednocześnie działanie całego układu.

| Obserwacja | Aktywacja | Powrót #117 | Efektywny bias żądania [°] | Skok żądania przy powrocie [°] | Δψ / Δχ / Δβ native [°] |
| --- | --- | --- | --- | --- | --- |
| 1.20 | 1.44 | 1.54 | -1.3956…-0.9171 | 1.4383 | -0.1218/-0.3553/0.2335 |
| 1.60 | 1.84 | 1.94 | -1.6520…-0.9342 | 1.7203 | 1.0961/0.6211/0.4750 |
| 5.20 | 5.44 | 5.54 | -1.8939…0.9260 | 2.2869 | 0.8111/2.1797/-1.3686 |
| 5.60 | 5.84 | 5.94 | -1.4811…-0.8915 | 1.5750 | -4.2222/-2.8754/-1.3468 |
| 6.00 | 6.24 | 6.34 | -1.9425…-0.9684 | 2.0450 | -3.1758/-3.5205/0.3448 |
| 6.40 | 6.64 | 6.74 | -1.6861…-0.9684 | 1.7545 | -0.3656/-1.2776/0.9119 |
| 6.80 | 7.04 | 7.14 | -1.4298…-0.9342 | 1.4725 | 1.9858/1.4587/0.5271 |
| 7.20 | 7.44 | 7.54 | -1.2333…-0.9000 | 1.2589 | 2.8815/2.5585/0.3230 |
| 10.40 | 10.64 | 10.74 | -1.1991…-0.9085 | 1.2162 | -0.7866/-0.5143/-0.2724 |
| 10.80 | 11.04 | 11.14 | -1.3700…-0.9171 | 1.4212 | -0.8651/-0.7478/-0.1174 |



## 1.44–1.54s

| Pomiar | Początek | Koniec |
| --- | --- | --- |
| steering | 0.36743 | -0.53257 |
| request | -0.53257 | 0.90576 |
| psi | -5.16251 | -5.28432 |
| chi | -4.58140 | -4.93669 |
| beta | -0.58111 | -0.34763 |
| yaw | 0.01383 | -0.05373 |
| yaw_acceleration | -0.21929 | 0.20571 |
| error | -0.59191 | -0.70364 |
| eta | 3.22875 | 3.53720 |
| speed | 69.05422 | 68.77807 |
| drive | 0.00000 | 0.00000 |
| front_force | -84.54086 | -38.88947 |
| rear_force | -79.36208 | -63.44643 |
| front_use | 0.09889 | 0.04549 |
| rear_use | 0.07961 | 0.06367 |
| tyre_loss | 33.48144 | 13.17861 |
| torque | -11.56112 | 10.84526 |


Różnica względem natychmiastowej odpowiedzi #117 na tych samych stanach: max |Δχ̇|=0.019805rad/s; max |Δmoment|=39.094235N·m. Kąt i szybkość kierownicy pozostają fizycznie ograniczone.


## 1.84–1.94s

| Pomiar | Początek | Koniec |
| --- | --- | --- |
| steering | 1.32446 | 0.42446 |
| request | 0.42446 | 2.14478 |
| psi | -3.63196 | -2.53586 |
| chi | -4.30600 | -3.68493 |
| beta | 0.67403 | 1.14907 |
| yaw | 0.21476 | 0.16806 |
| yaw_acceleration | 0.04247 | 0.31565 |
| error | -1.01897 | -0.97650 |
| eta | -0.02098 | -3.15103 |
| speed | 67.95949 | 67.68112 |
| drive | 0.00000 | 0.00000 |
| front_force | 132.31459 | 198.58949 |
| rear_force | 150.61624 | 203.92563 |
| front_use | 0.15477 | 0.23229 |
| rear_use | 0.15104 | 0.20448 |
| tyre_loss | 97.76164 | 200.38289 |
| torque | 2.23928 | 16.64128 |


Różnica względem natychmiastowej odpowiedzi #117 na tych samych stanach: max |Δχ̇|=0.019543rad/s; max |Δmoment|=37.985009N·m. Kąt i szybkość kierownicy pozostają fizycznie ograniczone.


## 5.44–5.54s

| Pomiar | Początek | Koniec |
| --- | --- | --- |
| steering | -7.30001 | -8.20001 |
| request | -8.20001 | -5.91309 |
| psi | 129.22438 | 130.03548 |
| chi | 120.28856 | 122.46825 |
| beta | 8.93582 | 7.56723 |
| yaw | 0.54801 | -0.19540 |
| yaw_acceleration | -8.80983 | -5.52427 |
| error | -0.54619 | -1.24117 |
| eta | 32.86439 | 33.61722 |
| speed | 45.10596 | 46.10639 |
| drive | 50.00001 | 50.00001 |
| front_force | -66.63754 | 50.94300 |
| rear_force | 697.07050 | 544.29724 |
| front_use | 0.07795 | 0.05959 |
| rear_use | 0.88203 | 0.83764 |
| tyre_loss | 4023.55649 | 3810.16387 |
| torque | -464.46275 | -291.24485 |


Różnica względem natychmiastowej odpowiedzi #117 na tych samych stanach: max |Δχ̇|=0.028884rad/s; max |Δmoment|=37.538300N·m. Kąt i szybkość kierownicy pozostają fizycznie ograniczone.


## 5.84–5.94s

| Pomiar | Początek | Koniec |
| --- | --- | --- |
| steering | -4.43481 | -5.33481 |
| request | -5.33481 | -3.75976 |
| psi | 121.58630 | 117.36411 |
| chi | 122.97191 | 120.09649 |
| beta | -1.38561 | -2.73238 |
| yaw | -0.67314 | -0.78567 |
| yaw_acceleration | -1.21765 | -0.36201 |
| error | -3.39658 | -4.00440 |
| eta | 29.64504 | 25.13599 |
| speed | 47.69813 | 47.33887 |
| drive | 0.00000 | 5.00000 |
| front_force | -439.57745 | -508.40625 |
| rear_force | -403.98456 | -559.17517 |
| front_use | 0.51417 | 0.59468 |
| rear_use | 0.40507 | 0.56078 |
| tyre_loss | 697.80324 | 1183.57446 |
| torque | -64.19571 | -19.08545 |


Różnica względem natychmiastowej odpowiedzi #117 na tych samych stanach: max |Δχ̇|=0.017997rad/s; max |Δmoment|=24.646960N·m. Kąt i szybkość kierownicy pozostają fizycznie ograniczone.


## 6.24–6.34s

| Pomiar | Początek | Koniec |
| --- | --- | --- |
| steering | -2.05078 | -2.95078 |
| request | -2.95078 | -0.90576 |
| psi | 105.49092 | 102.31514 |
| chi | 110.01497 | 106.49444 |
| beta | -4.52406 | -4.17930 |
| yaw | -0.56858 | -0.54461 |
| yaw_acceleration | 0.59202 | 0.55380 |
| error | -5.17913 | -5.33478 |
| eta | 9.70736 | 4.30180 |
| speed | 46.43780 | 46.05258 |
| drive | 0.00000 | 0.00000 |
| front_force | -510.45920 | -491.90002 |
| rear_force | -646.98755 | -621.99768 |
| front_use | 0.59708 | 0.57537 |
| rear_use | 0.65101 | 0.62371 |
| tyre_loss | 1451.58180 | 1285.92160 |
| torque | 31.21115 | 29.19736 |


Różnica względem natychmiastowej odpowiedzi #117 na tych samych stanach: max |Δχ̇|=0.016454rad/s; max |Δmoment|=22.208923N·m. Kąt i szybkość kierownicy pozostają fizycznie ograniczone.


## 6.64–6.74s

| Pomiar | Początek | Koniec |
| --- | --- | --- |
| steering | 0.51270 | -0.38730 |
| request | -0.38730 | 1.36719 |
| psi | 97.19695 | 96.83130 |
| chi | 99.11642 | 97.83883 |
| beta | -1.91947 | -1.00752 |
| yaw | -0.06978 | -0.06415 |
| yaw_acceleration | 0.68961 | 0.69246 |
| error | -5.15589 | -4.92930 |
| eta | -8.77826 | -11.93472 |
| speed | 45.19471 | 44.99694 |
| drive | 0.00000 | 0.00000 |
| front_force | -191.93320 | -88.56254 |
| rear_force | -284.51782 | -164.16811 |
| front_use | 0.22450 | 0.10359 |
| rear_use | 0.28526 | 0.16460 |
| tyre_loss | 192.38187 | 54.38045 |
| torque | 36.35715 | 36.50727 |


Różnica względem natychmiastowej odpowiedzi #117 na tych samych stanach: max |Δχ̇|=0.029845rad/s; max |Δmoment|=38.602003N·m. Kąt i szybkość kierownicy pozostają fizycznie ograniczone.


## 7.04–7.14s

| Pomiar | Początek | Koniec |
| --- | --- | --- |
| steering | 2.73437 | 1.83437 |
| request | 1.83437 | 3.30688 |
| psi | 100.04334 | 102.02916 |
| chi | 99.36658 | 100.82530 |
| beta | 0.67676 | 1.20386 |
| yaw | 0.36327 | 0.33042 |
| yaw_acceleration | 0.13431 | 0.39741 |
| error | -3.99846 | -3.65573 |
| eta | -15.94733 | -16.31521 |
| speed | 44.44539 | 44.33196 |
| drive | 5.00000 | 5.00000 |
| front_force | 208.02641 | 275.04623 |
| rear_force | 230.70337 | 285.71176 |
| front_use | 0.24333 | 0.32172 |
| rear_use | 0.23149 | 0.29115 |
| tyre_loss | 157.51714 | 273.66637 |
| torque | 7.08107 | 20.95166 |


Różnica względem natychmiastowej odpowiedzi #117 na tych samych stanach: max |Δχ̇|=0.027607rad/s; max |Δmoment|=35.090630N·m. Kąt i szybkość kierownicy pozostają fizycznie ograniczone.


## 7.44–7.54s

| Pomiar | Początek | Koniec |
| --- | --- | --- |
| steering | 3.34961 | 2.44961 |
| request | 2.44961 | 3.70850 |
| psi | 109.94145 | 112.82293 |
| chi | 107.80968 | 110.36820 |
| beta | 2.13177 | 2.45473 |
| yaw | 0.53289 | 0.47775 |
| yaw_acceleration | -0.28362 | 0.13853 |
| error | -2.65807 | -2.35116 |
| eta | -14.81814 | -14.11358 |
| speed | 44.18093 | 44.41362 |
| drive | 20.00000 | 20.00000 |
| front_force | 359.52863 | 403.85217 |
| rear_force | 443.83276 | 458.38446 |
| front_use | 0.42054 | 0.47238 |
| rear_use | 0.49002 | 0.50780 |
| tyre_loss | 769.09355 | 895.40675 |
| torque | -14.95317 | 7.30380 |


Różnica względem natychmiastowej odpowiedzi #117 na tych samych stanach: max |Δχ̇|=0.022861rad/s; max |Δmoment|=29.031203N·m. Kąt i szybkość kierownicy pozostają fizycznie ograniczone.


## 10.64–10.74s

| Pomiar | Początek | Koniec |
| --- | --- | --- |
| steering | -0.41870 | -1.31870 |
| request | -1.31870 | -0.10254 |
| psi | 183.96854 | 183.18191 |
| chi | 184.14556 | 183.63131 |
| beta | -0.17702 | -0.44940 |
| yaw | -0.07447 | -0.19128 |
| yaw_acceleration | -0.87575 | -0.18131 |
| error | -0.51386 | -0.62232 |
| eta | 4.09707 | 3.52401 |
| speed | 58.18874 | 59.03271 |
| drive | 42.56003 | 42.56003 |
| front_force | -104.25011 | -103.03062 |
| rear_force | -44.65973 | -104.25906 |
| front_use | 0.12194 | 0.12051 |
| rear_use | 0.35321 | 0.50871 |
| tyre_loss | 571.98328 | 1243.89207 |
| torque | -46.17037 | -9.55846 |


Różnica względem natychmiastowej odpowiedzi #117 na tych samych stanach: max |Δχ̇|=0.023025rad/s; max |Δmoment|=38.299322N·m. Kąt i szybkość kierownicy pozostają fizycznie ograniczone.


## 11.04–11.14s

| Pomiar | Początek | Koniec |
| --- | --- | --- |
| steering | 0.00854 | -0.89146 |
| request | -0.89146 | 0.52979 |
| psi | 180.90572 | 180.04058 |
| chi | 181.96296 | 181.21520 |
| beta | -1.05724 | -1.17462 |
| yaw | -0.10076 | -0.19783 |
| yaw_acceleration | -0.55104 | -0.15346 |
| error | -0.84939 | -0.88326 |
| eta | 1.55557 | 0.67044 |
| speed | 62.33508 | 63.77897 |
| drive | 57.56003 | 57.56003 |
| front_force | -152.27937 | -141.02649 |
| rear_force | -129.23637 | -151.03929 |
| front_use | 0.17812 | 0.16496 |
| rear_use | 0.69356 | 0.73442 |
| tyre_loss | 2802.88977 | 3319.63216 |
| torque | -29.05116 | -8.09045 |


Różnica względem natychmiastowej odpowiedzi #117 na tych samych stanach: max |Δχ̇|=0.021305rad/s; max |Δmoment|=37.983673N·m. Kąt i szybkość kierownicy pozostają fizycznie ograniczone.
