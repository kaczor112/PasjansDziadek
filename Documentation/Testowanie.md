# Testowanie i budowanie

Projekt wymaga Unity **6000.3.25f1**. Testy korzystają z obecnego w projekcie Unity Test Framework; nie wymagają pobierania osobnych bibliotek. Testy używają wyłącznie własnych katalogów tymczasowych i nie zmieniają zapisu użytkownika.

## Automatyczna weryfikacja

W Unity: **Window → General → Test Runner → EditMode → Run All**.

Testy obejmują pełną talię i układ początkowy, prawidłowe oraz niedozwolone ruchy, odkrywanie kart, fundamenty, wygraną, kolejność talii po przewinięciu, dobieranie **3 → 2 → 1**, częściowe ostatnie dobieranie, walidację stanu oraz sekwencje mieszanych legalnych ruchów. Testy plików sprawdzają dokładne wznowienie, brak zapisu, plik uszkodzony, odzyskanie kopii zapasowej, sumę kontrolną, nowszy format, przerwany lub niemożliwy zapis i zbyt duży plik.

Ranking ma dodatkowe testy: końcowy ruch odsłaniający ostatnią kartę kolumny wchodzi do wyniku, licznik nie rośnie po zakończeniu, pełne przejście 3 → 2 → 1 daje „Tak”, kolejne przełożenie daje „Nie”, „Tak” ma pierwszeństwo nad mniejszą liczbą ruchów z „Nie”, restart zachowuje datę/wynik i nie powiela wpisu, zapisywanych jest tylko dziesięć najlepszych wyników, remisy są porządkowane datą, brak lub uszkodzenie plików nie powoduje wyjątku, a błąd zapisu zachowuje poprzedni ranking.

Sprawdzenie trybów 28.09.2026: **63/63 testy zakończone powodzeniem**. Raport: `TestResults/editmode-modes.xml`. Nowe testy obejmują 52/104 unikalne karty, 5/10 kolumn, osiem baz, wznowienie dwóch talii z kopii, wybór następnej gry, starszy zapis z siedmioma kolumnami i zakończenie punktowania w dziesiątej kolumnie.

Sprawdzenie zasad Dziadka 29.09.2026: **67/67 testów zakończonych powodzeniem**. Raport: `TestResults/editmode-grandpa.xml`. Scenariusz graficzny potwierdził zielony i czerwony komunikat nad bazami, czwartą kolumnę oraz pierwszeństwo wpisów „Tak”. Raport: `TestResults/smoke-grandpa.log`; zrzuty: `Logs/Screenshots/20260928-215027-974`.

Scenariusz `Pasjans.Editor.SmokeCapture.RunModes` sprawdził układy 1280×800 i 720×1280, menu otwierane kursorem i symulowanym dotknięciem, wybór obu trybów, anulowanie nowej gry, dwa rewersy i widoczność kart w granicach ekranu. Raport: `TestResults/smoke-modes.log`; zrzuty: `Logs/Screenshots/20260928-183705-524`. Scenariusz używa izolowanego zapisu w `Temp/SmokeSavesModes`. Nie zmienia danych gracza. Wcześniejsze sprawdzenie okien autora i rankingu: `Logs/Screenshots/20260928-101701-976`.

Uruchomienie bez interfejsu (PowerShell, z katalogu projektu; zamknij wcześniej edytor tego projektu):

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe'
& $unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testResults "$PWD\TestResults\editmode.xml" -logFile "$PWD\TestResults\editmode.log"
```

Nie dodawaj `-quit` do `-runTests`: zakończeniem procesu zarządza Unity Test Framework. Plik XML zawiera liczbę wykonanych testów i wynik każdego przypadku. Sam poprawny start edytora lub brak błędu w konsoli nie potwierdza przejścia testów.

## Budowanie

Menu **Pasjans → Zbuduj → Windows (64-bit)** zapisuje wynik w `Builds/Windows/Pasjans.exe`. Menu **Pasjans → Zbuduj → Android (APK)** zapisuje wynik w `Builds/Android/Pasjans.apk`. Dla Androida w Unity Hub muszą być dodane Android Build Support, SDK/NDK i OpenJDK. Narzędzia korzystają z włączonych scen w Build Profiles.

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
4. Otwórz menu **Plik** na Windows lub **⋮** na Androidzie. Sprawdź pozycje „Nowa gra”, „Tryby gry →”, „Ranking”, „O mnie” i „Zakończ”. Na Windows najedź na tryby, a na Androidzie dotknij wiersza. Wybierz dwie talie: karty i licznik mają zostać bez zmian. Anuluj nowe rozdanie, a następnie ponownie wybierz „Nowa gra” i potwierdź. Sprawdź 10 kolumn, 8 baz i oba kolory rewersów. Powtórz dla jednej talii. Otwórz „O mnie”, sprawdź tekst autora i zamknięcie przyciskiem „X”.
5. Na Androidzie obróć urządzenie w obu kierunkach podczas rozgrywki oraz przy otwartym oknie. Sprawdź wcięcie ekranu, dolny pasek systemowy, dotyk i przeciąganie kart.
6. Sprawdź widoczność najdłuższej kolumny i celność wskazywania kart na małym telefonie oraz przy zmianie rozmiaru okna Windows.
7. Przy odsłonięciu ostatniej zakrytej karty kolumn sprawdź, czy wynik przestaje rosnąć. Opróżnij talię w pierwszym przejściu po jednej karcie i sprawdź zielony komunikat „Wg. Dziadka wygrałeś”. W drugim rozdaniu rozpocznij kolejne przejście po jednej karcie i sprawdź czerwony komunikat „Wg. Dziadka przegrałeś”. Otwórz „Ranking”, sprawdź cztery kolumny i pierwszeństwo „Tak”, zamknij „X”, uruchom grę ponownie i upewnij się, że wynik nie został zdublowany.

Testy EditMode nie zastępują sprawdzenia dotyku, obrotu ekranu, wydajności i cyklu życia na fizycznym urządzeniu z Androidem.
