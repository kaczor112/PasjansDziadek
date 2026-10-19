# Testowanie i budowanie

Projekt wymaga Unity **6000.3.25f1**. Testy korzystają z obecnego w projekcie Unity Test Framework; nie wymagają pobierania osobnych bibliotek. Testy używają wyłącznie własnych katalogów tymczasowych i nie zmieniają zapisu użytkownika.

## Automatyczna weryfikacja

W Unity: **Window → General → Test Runner → EditMode → Run All**.

Testy obejmują pełną talię i układ początkowy, prawidłowe oraz niedozwolone ruchy, odkrywanie kart, fundamenty, wygraną, kolejność talii po przewinięciu, dobieranie **3 → 2 → 1**, częściowe ostatnie dobieranie, walidację stanu oraz sekwencje mieszanych legalnych ruchów. Testy plików sprawdzają dokładne wznowienie, brak zapisu, plik uszkodzony, odzyskanie kopii zapasowej, sumę kontrolną, nowszy format, przerwany lub niemożliwy zapis i zbyt duży plik.

Ranking ma dodatkowe testy: końcowy ruch odsłaniający ostatnią kartę kolumny wchodzi do wyniku, licznik nie rośnie po zakończeniu, pełne przejście 3 → 2 → 1 daje „Tak”, kolejne przełożenie daje „Nie”, „Tak” ma pierwszeństwo nad mniejszą liczbą ruchów z „Nie”, restart zachowuje datę/wynik i nie powiela wpisu, zapisywanych jest tylko dziesięć najlepszych wyników, remisy są porządkowane datą, brak lub uszkodzenie plików nie powoduje wyjątku, a błąd zapisu zachowuje poprzedni ranking.

Sprawdzenie trybów 28.09.2026: **63/63 testy zakończone powodzeniem**. Raport: `TestResults/editmode-modes.xml`. Nowe testy obejmują 52/104 unikalne karty, 5/10 kolumn, osiem baz, wznowienie dwóch talii z kopii, wybór następnej gry, starszy zapis z siedmioma kolumnami i zakończenie punktowania w dziesiątej kolumnie.

Sprawdzenie zasad Dziadka i trybu domyślnego 29.09.2026: **70/70 testów zakończonych powodzeniem**. Raport: `TestResults/editmode-grandpa.xml`. Scenariusze graficzne sprawdziły domyślny tryb dwóch talii, oba układy planszy, zielony i czerwony komunikat nad bazami, czwartą kolumnę oraz pierwszeństwo wpisów „Tak”. Raporty: `TestResults/smoke-default-two.log` i `TestResults/smoke-grandpa-early.log`; zrzuty: `Logs/Screenshots/20260929-104947-905` oraz `Logs/Screenshots/20260929-105057-780`.

Poprawka startu Androida 29.09.2026: interfejs ponownie oblicza układ w pierwszych klatkach po przygotowaniu powierzchni ekranu. APK używa `UnityPlayerActivity` i OpenGL ES 3; manifest, biblioteki ARM64 oraz pakowanie Gradle zostały sprawdzone. **70/70 testów** i scenariusz graficzny przeszły ponownie. Raport: `TestResults/smoke-android-fix.log`; zrzuty: `Logs/Screenshots/20260929-151626-906`.

Druga poprawka startu Androida 29.09.2026: usunięto zbędny renderer URP, zabezpieczono układ przed zerowym rozmiarem ekranu i dodano komunikat `START-01` na wypadek wyjątku podczas startu. APK ma `versionCode 3`, podpis v2, aktywność `UnityPlayerActivity`, OpenGL ES 3 i bibliotekę ARM64. **70/70 testów** oraz scenariusz graficzny obu orientacji i trybów zakończyły się powodzeniem. Raport: `TestResults/smoke-android-builtin.log`; zrzuty: `Logs/Screenshots/20260929-154125-127`.

Poprawka wyniku Dziadka 29.09.2026: „Tak” wymaga odkrycia wszystkich kart w kolumnach oraz użycia wszystkich kart z talii zakrytej i stosu odkrytego przed czwartym przełożeniem. Zapis wersji 3 z przedwcześnie przyznanym wynikiem wraca do oczekiwania, a wpis rankingu tego samego rozdania może zostać poprawiony. Usunięto kropkę po „Wg” we wszystkich napisach interfejsu. APK ma `versionCode 4`. **72/72 testy zakończone powodzeniem**. Raport graficzny: `TestResults/smoke-grandpa-waste.log`; zrzuty: `Logs/Screenshots/20260929-174710-489`.

Ostateczna poprawka startu Androida 29.09.2026: usunięto z zapisanej sceny wadliwy komponent ze zgubionym odwołaniem do skryptu, a interfejs uruchamia teraz zachowywany przez IL2CPP moduł startowy. APK ma `versionCode 6`. **72/72 testy zakończone powodzeniem**. Wersję zainstalowano i uruchomiono na ASUS ROG Phone 8 z Androidem 14; pełny interfejs pojawił się po zimnym starcie, a log nie zawiera błędu brakującego skryptu ani wyjątku aplikacji. Raport: `TestResults/editmode-android-bootstrap.xml`; log urządzenia: `TestResults/android-device-logcat-v6.txt`; zrzut urządzenia: `TestResults/android-device-v6.png`.

Obsługa podwójnego stuknięcia na Androidzie 29.09.2026: karta rozpoznaje dwa osobne dotknięcia wykonane w czasie do 0,4 sekundy i przekazuje je do tej samej obsługi baz co dwuklik Windows. Przeciągnięcie zeruje rozpoczęty gest. APK ma `versionCode 7`. **75/75 testów zakończonych powodzeniem**; testy obejmują szybkie i zbyt wolne stuknięcia oraz zerowanie po przeciągnięciu. Raport: `TestResults/editmode-android-double-tap.xml`.

Poprawa czytelności kart 03.10.2026: powiększono i pogrubiono wartości kart, symbole kolorów oraz figury, a czerwień i czerń otrzymały większy kontrast. APK ma `versionCode 8`. **75/75 testów zakończonych powodzeniem**. Scenariusz graficzny sprawdził oba tryby oraz układy pionowy i poziomy. Raporty: `TestResults/editmode-card-readability.xml` i `TestResults/smoke-card-readability.log`; zrzuty: `Logs/Screenshots/20261003-084624-381`.

Ręczny obrót Androida 04.10.2026: obok menu z trzema kropkami dodano kwadratowy przycisk z generowaną ikoną obracanego ekranu. Każde naciśnięcie wymusza kolejną orientację o 90°, niezależnie od systemowej blokady obrotu; przycisk pozostaje ukryty w Windows. APK ma `versionCode 9`. **81/81 testów zakończonych powodzeniem**. Raporty: `TestResults/editmode-screen-rotation.xml` i `TestResults/smoke-screen-rotation.log`; zrzuty: `Logs/Screenshots/20261003-222601-211`.

Zmiany interfejsu 04.10.2026: przycisk rankingu i bieżący licznik ruchów są ukryte, a na dole widnieje wersja `v2026.10.04_05_v01_7652287`. Kliknięcie dwóch kart z różnych kolumn próbuje także legalnego ruchu w odwrotnej kolejności; w jednej kolumnie obowiązuje dotychczasowe zaznaczanie. Ekran „Made with Unity” został wyłączony w ustawieniach i podczas budowania. APK ma `versionCode 10`. **86/86 testów zakończonych powodzeniem**. Raporty: `TestResults/editmode-version-ranking-selection.xml` i `TestResults/smoke-version-ranking-selection.log`; zrzuty: `Logs/Screenshots/20261004-211132-840`.

Zmiany 05.10.2026: kod rankingu jest wyłączony symbolem `PASJANS_RANKING`; bieżąca gra nie odczytuje ani nie zapisuje wyników, a istniejące pliki rankingu pozostają nietknięte. Dodano build WebGL z responsywną stroną, względnymi ścieżkami, bez kompresji wymagającej dodatkowych nagłówków i z automatyczną synchronizacją zapisu w IndexedDB. Wersja widoczna w grze: `v2026.10.05_06_v01_2a372d7`. **86/86 testów zakończonych powodzeniem** (`TestResults/editmode-web-no-ranking.xml`). Unity zbudowało WWW i Windows; APK przygotował skrypt `Tools/Build-Android.ps1` przez Gradle po błędzie pakowania w Unity. APK ma `versionCode 11`. Przeglądarka potwierdziła dobieranie i wznowienie rozdania po odświeżeniu strony. Test interfejsu obu układów: `TestResults/smoke-web-no-ranking.log`.

Zmiany 19.10.2026: komunikat „Brawo!” pojawia się również po ułożeniu wszystkich kart w pełne, odkryte kolumny król–as z naprzemiennymi kolorami. Ukończone bazy można łączyć z pełnymi kolumnami, ale talia i stos odkryty muszą być puste. Wersja: `v2026.10.19_v06.03_2a372d7`. **34/34 testy reguł przeszły poza edytorem**, przez lokalny .NET 10 i istniejący NUnit (`TestResults/core-tableau-win.xml`); obejmują oba tryby, końcowy ruch, wznowienie stanu i blokadę przedwczesnej wygranej. Kompilacja bibliotek gry, edytora i testów przeszła kompilatorem Unity (`TestResults/compile-tableau-win.log`). Pełny Unity Test Runner i nowe buildy wymagają aktywnej licencji edytora; próba uruchomienia została zatrzymana błędem „No valid Unity Editor license found”.

Kolejna zmiana 19.10.2026: dodano legalny ruch przy kolejności wierzch kolumny → wierzch stosu odkrytego. Wymaga świeżego zaznaczenia; osobny numer akcji unieważnia je także po zatrzymaniu licznika wyniku. Pozostałe karty kolumny, zakryta talia, bazy i karty poniżej wierzchu stosu odkrytego nie korzystają z tej reguły. Wersja: `v2026.10.19_v06.04_2a372d7`, Android `versionCode 12`. Po uruchomieniu Unity poza piaskownicą aktywna licencja zadziałała: **107/107 testów EditMode przeszło** (`TestResults/editmode-reverse-waste.xml`). Test graficzny potwierdził zwykłą i odwrotną kolejność, brak reakcji na stare zaznaczenie przy zatrzymanym liczniku, dobieranie z zakrytej talii oraz okno „Brawo!” dla obu trybów. Raport: `TestResults/smoke-reverse-waste.log`; zrzuty: `Logs/Screenshots/20261019-102540-400`. W teście menu zastosowano wirtualną mysz, aby ukryte okno nie zależało od położenia kursora użytkownika.

Buildy testowe tej wersji: Windows i WebGL zbudowane poprawnie (`TestResults/build-windows-reverse-waste.log`, `TestResults/build-web-reverse-waste.log`). Android zakończył kompilację, ale pakowanie w Unity napotkało błąd połączenia loopback Javy; istniejący skrypt ukończył pakowanie przez Gradle (`TestResults/gradle-android.log`). APK ma `versionCode 12`, ARM64 i poprawny podpis v2. Archiwa Windows i WWW zawierają pliki startowe; z ZIP Windows wyłączono katalogi `DoNotShip`. Sumy SHA-256: `TestResults/build-reverse-waste.sha256`. To artefakty do testów, bez zatwierdzenia do publicznego wydania; status i otwarte punkty audytu `Compliance/STATUS.md` oraz `Compliance/ACTION_REQUIRED.md` pozostają aktualnymi ograniczeniami publikacji.

Poprawka przeciągania 19.10.2026: punkt naciśnięcia sprzed przekroczenia progu przeciągania pozostaje pod wskaźnikiem, również przy przenoszeniu grupy. Upuszczenie rozpoznaje geometrycznie najbliższą kolumnę lub bazę pod wskaźnikiem, z ograniczonym marginesem. Kolumny działają na całej wysokości; odrzucenie ruchu nie szuka innego legalnego celu obok. Drugi palec nie przejmuje gestu, a anulowanie odblokowuje następne przeciąganie. Wersja `v2026.10.19_v06.05_2a372d7`, Android `versionCode 13`. **107/107 testów EditMode przeszło** (`TestResults/editmode-drag.xml`). `DragSmokeChecks` sprawdził mysz i symulowany dotyk przy 1280×800 oraz 720×1280 w obu trybach: punkt złapania z dokładnością 0,25 piksela, zachowanie odstępów grupy, puszczenie bez końcowego zdarzenia ruchu, margines kolumny/bazy, niskie i puste kolumny, drugi palec, anulowanie i niedozwolony cel. Raport `TestResults/smoke-drag.log`; zrzuty `Logs/Screenshots/20261019-105122-434`. Test używa odizolowanego zapisu, nie danych gracza.

Buildy testowe poprawki przeciągania: Windows i WWW zakończone powodzeniem (`TestResults/build-windows-drag.log`, `TestResults/build-web-drag.log`). APK numer 13, ARM64, z poprawnym podpisem v2 powstał przez istniejący skrypt i bezpośrednie pakowanie Gradle po błędzie loopback w Unity (`TestResults/gradle-android.log`). ZIP Windows zawiera pliki uruchomieniowe bez katalogów `DoNotShip`, a ZIP WWW zawiera `index.html` i folder `Build`. Sumy SHA-256: `TestResults/build-drag.sha256`. Ograniczenia publicznego wydania z audytu `Compliance/STATUS.md` oraz `Compliance/ACTION_REQUIRED.md` nadal obowiązują.

Scenariusz `Pasjans.Editor.SmokeCapture.RunModes` sprawdził układy 1280×800 i 720×1280, menu otwierane kursorem i symulowanym dotknięciem, wybór obu trybów, anulowanie nowej gry, dwa rewersy i widoczność kart w granicach ekranu. Raport: `TestResults/smoke-modes.log`; zrzuty: `Logs/Screenshots/20260928-183705-524`. Scenariusz używa izolowanego zapisu w `Temp/SmokeSavesModes`. Nie zmienia danych gracza. Wcześniejsze sprawdzenie okien autora i rankingu: `Logs/Screenshots/20260928-101701-976`.

Uruchomienie bez interfejsu (PowerShell, z katalogu projektu; zamknij wcześniej edytor tego projektu):

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe'
& $unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testResults "$PWD\TestResults\editmode.xml" -logFile "$PWD\TestResults\editmode.log"
```

Nie dodawaj `-quit` do `-runTests`: zakończeniem procesu zarządza Unity Test Framework. Plik XML zawiera liczbę wykonanych testów i wynik każdego przypadku. Sam poprawny start edytora lub brak błędu w konsoli nie potwierdza przejścia testów.

## Budowanie

Menu **Pasjans → Zbuduj → Windows (64-bit)** zapisuje wynik w `Builds/Windows/Pasjans.exe`. Menu **Pasjans → Zbuduj → Android (APK)** zapisuje wynik w `Builds/Android/Pasjans.apk`. Dla Androida w Unity Hub muszą być dodane Android Build Support, SDK/NDK i OpenJDK. Narzędzia korzystają z włączonych scen w Build Profiles.

Menu **Pasjans → Zbuduj → WWW (WebGL)** zapisuje stronę w `Builds/Web`. Wgraj jej całą zawartość do `/pasjans/` na serwerze i otwórz `https://mojastrona.pl/pasjans/`. Moduł WebGL Build Support musi być zainstalowany w Unity Hub. Przeglądarka zapisuje stan gry w IndexedDB związanym z adresem katalogu strony; sprawdź wznowienie po odświeżeniu na tym samym adresie.

Narzędzia Androida odrzucają ścieżkę projektu z polskimi znakami. Najprościej uruchomić `Tools/Build-Android.ps1`: skrypt sam używa krótkiej ścieżki Windows, osobnego cache Gradle i bezpłatnego OpenJDK dołączonego do Unity, a gotowy plik zapisuje w `Builds/Android/Pasjans.apk`. Ręczne wywołanie Unity wymaga podania krótkiej ścieżki projektu, np. `C:\Users\PAWE~1\OneDrive\APLIKA~1\UNITYP~1\PASJAN~1`.

Przy problemach z pakowaniem w Unity skrypt uruchamia Gradle bezpośrednio i używa nowego katalogu `Temp/AndroidPackage-*`, aby nie zastępować zablokowanych plików roboczych OneDrive. Opcja `-PackageOnly` ponawia tylko pakowanie już wygenerowanego projektu: używaj jej wyłącznie po pełnej kompilacji bieżącego kodu, gdy nie udał się sam etap Gradle. Po zmianach kodu uruchom pełny skrypt bez tej opcji.

Wersja Windows jest też spakowana w `Builds/Pasjans-Windows.zip`. Rozpakuj cały plik i uruchom `Pasjans.exe`; samo przeniesienie pliku EXE bez katalogu danych i bibliotek nie wystarczy.

Odpowiedniki do automatyzacji:

```powershell
& $unity -batchmode -nographics -quit -projectPath "$PWD" -buildTarget Win64 -executeMethod Pasjans.Editor.BuildGame.Windows -logFile "$PWD\TestResults\build-windows.log"
& "$PWD\Tools\Build-Android.ps1" -UnityPath $unity
```

## Kontrola na urządzeniu

1. Uruchom grę przy braku zapisu. Sprawdź pięć kolumn i możliwość przeniesienia legalnej sekwencji; niedozwolony ruch ma pozostawić planszę bez zmian.
2. Przejdź pełną talię: dobieranie ma zmieniać się kolejno z trzech na dwie i jedną kartę. Zamknij grę w drugim przejściu i sprawdź po uruchomieniu układ oraz dobieranie po dwie.
3. Wykonaj kilka ruchów, wybierz „Zakończ”, uruchom ponownie i porównaj pozycje oraz strony wszystkich kart. Na Androidzie sprawdź również przejście do ekranu głównego i zakończenie aplikacji przez system.
4. Otwórz menu **Plik** na Windows lub **⋮** na Androidzie. Sprawdź pozycje „Nowa gra”, „Tryby gry →”, „O mnie” i „Zakończ” oraz brak widocznego przycisku rankingu. Na Windows najedź na tryby, a na Androidzie dotknij wiersza. Wybierz dwie talie: bieżące rozdanie ma zostać bez zmian. Anuluj nowe rozdanie, a następnie ponownie wybierz „Nowa gra” i potwierdź. Sprawdź 10 kolumn, 8 baz i oba kolory rewersów. Powtórz dla jednej talii. Otwórz „O mnie”, sprawdź tekst autora i zamknięcie przyciskiem „X”.
5. Na Androidzie obróć urządzenie w obu kierunkach podczas rozgrywki oraz przy otwartym oknie. Sprawdź wcięcie ekranu, dolny pasek systemowy, dotyk i przeciąganie kart.
6. Sprawdź widoczność najdłuższej kolumny i celność wskazywania kart na małym telefonie oraz przy zmianie rozmiaru okna Windows.
7. Przy odsłonięciu ostatniej zakrytej karty kolumn sprawdź zasady Dziadka. Użyj wszystkich kart z talii i stosu odkrytego podczas przejścia po 3, 2 albo 1 karcie i sprawdź zielony komunikat „Wg Dziadka wygrałeś”. W drugim rozdaniu rozpocznij czwarte przejście i sprawdź czerwony komunikat „Wg Dziadka przegrałeś”. Sprawdź, czy gra nie tworzy ani nie zmienia plików rankingu.

Testy EditMode nie zastępują sprawdzenia dotyku, obrotu ekranu, wydajności i cyklu życia na fizycznym urządzeniu z Androidem.
