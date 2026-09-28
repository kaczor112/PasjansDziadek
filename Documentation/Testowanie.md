# Testowanie i budowanie

Projekt wymaga Unity **6000.3.25f1**. Testy korzystają z obecnego w projekcie Unity Test Framework; nie wymagają pobierania osobnych bibliotek. Testy używają wyłącznie własnych katalogów tymczasowych i nie zmieniają zapisu użytkownika.

## Automatyczna weryfikacja

W Unity: **Window → General → Test Runner → EditMode → Run All**.

Testy obejmują pełną talię i układ początkowy, prawidłowe oraz niedozwolone ruchy, odkrywanie kart, fundamenty, wygraną, kolejność talii po przewinięciu, dobieranie **3 → 2 → 1**, częściowe ostatnie dobieranie, walidację stanu oraz sekwencje mieszanych legalnych ruchów. Testy plików sprawdzają dokładne wznowienie, brak zapisu, plik uszkodzony, odzyskanie kopii zapasowej, sumę kontrolną, nowszy format, przerwany lub niemożliwy zapis i zbyt duży plik.

Ranking ma dodatkowe testy: końcowy ruch odsłaniający ostatnią kartę kolumny wchodzi do wyniku, licznik nie rośnie po zakończeniu, restart zachowuje datę/wynik i nie powiela wpisu, zapisywanych jest tylko dziesięć najlepszych wyników, remisy są porządkowane datą, brak lub uszkodzenie plików nie powoduje wyjątku, a błąd zapisu zachowuje poprzedni ranking.

Sprawdzenie 28.09.2026: **52/52 testy zakończone powodzeniem** (21 reguł, 21 zapisu gry, 10 rankingu). Raport: `TestResults/editmode.xml`. Testowe uruchomienie sceny w Unity sprawdziło układ 1280×800 i 720×1280 oraz okna autora i rankingu; zrzuty: `Logs/Screenshots/20260928-101701-976`. Wpisy widoczne na zrzucie rankingu są wyłącznie danymi QA w izolowanym katalogu `Temp`, nie są dołączane do gotowej gry.

Uruchomienie bez interfejsu (PowerShell, z katalogu projektu; zamknij wcześniej edytor tego projektu):

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe'
& $unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testResults "$PWD\TestResults\editmode.xml" -logFile "$PWD\TestResults\editmode.log"
```

Nie dodawaj `-quit` do `-runTests`: zakończeniem procesu zarządza Unity Test Framework. Plik XML zawiera liczbę wykonanych testów i wynik każdego przypadku. Sam poprawny start edytora lub brak błędu w konsoli nie potwierdza przejścia testów.

## Budowanie

Menu **Pasjans → Zbuduj → Windows (64-bit)** zapisuje wynik w `Builds/Windows/Pasjans.exe`. Menu **Pasjans → Zbuduj → Android (APK)** zapisuje wynik w `Builds/Android/Pasjans.apk`. Dla Androida w Unity Hub muszą być dodane Android Build Support, SDK/NDK i OpenJDK. Narzędzia korzystają z włączonych scen w Build Profiles.

Narzędzia Androida odrzucają ścieżkę projektu z polskimi znakami. Najprościej uruchomić `Tools/Build-Android.ps1`: skrypt sam używa krótkiej ścieżki Windows, osobnego cache Gradle i bezpłatnego OpenJDK dołączonego do Unity, a gotowy plik zapisuje w `Builds/Android/Pasjans.apk`. Ręczne wywołanie Unity wymaga podania krótkiej ścieżki projektu, np. `C:\Users\PAWE~1\OneDrive\APLIKA~1\UNITYP~1\PASJAN~1`.

Wersja Windows jest też spakowana w `Builds/Pasjans-Windows.zip`. Rozpakuj cały plik i uruchom `Pasjans.exe`; samo przeniesienie pliku EXE bez katalogu danych i bibliotek nie wystarczy.

Odpowiedniki do automatyzacji:

```powershell
& $unity -batchmode -nographics -quit -projectPath "$PWD" -buildTarget Win64 -executeMethod Pasjans.Editor.BuildGame.Windows -logFile "$PWD\TestResults\build-windows.log"
& "$PWD\Tools\Build-Android.ps1" -UnityPath $unity
```

## Kontrola na urządzeniu

1. Uruchom grę przy braku zapisu. Sprawdź siedem kolumn i możliwość przeniesienia legalnej sekwencji; niedozwolony ruch ma pozostawić planszę bez zmian.
2. Przejdź pełną talię: dobieranie ma zmieniać się kolejno z trzech na dwie i jedną kartę. Zamknij grę w drugim przejściu i sprawdź po uruchomieniu układ oraz dobieranie po dwie.
3. Wykonaj kilka ruchów, wybierz „Zakończ”, uruchom ponownie i porównaj pozycje oraz strony wszystkich kart. Na Androidzie sprawdź również przejście do ekranu głównego i zakończenie aplikacji przez system.
4. Otwórz menu **Plik** na Windows lub **⋮** na Androidzie. Sprawdź pozycje „Nowa gra”, „Ranking”, „O mnie” i „Zakończ”. Otwórz „O mnie”, sprawdź tekst autora i zamknięcie przyciskiem „X”. W oknie potwierdzenia „Nowa gra” sprawdź zarówno anulowanie, jak i rozpoczęcie świeżego rozdania.
5. Na Androidzie obróć urządzenie w obu kierunkach podczas rozgrywki oraz przy otwartym oknie. Sprawdź wcięcie ekranu, dolny pasek systemowy, dotyk i przeciąganie kart.
6. Sprawdź widoczność najdłuższej kolumny i celność wskazywania kart na małym telefonie oraz przy zmianie rozmiaru okna Windows.
7. Przy odsłonięciu ostatniej zakrytej karty kolumn sprawdź, czy wynik przestaje rosnąć. Otwórz „Ranking”, sprawdź datę i ruchy, zamknij „X”, uruchom grę ponownie i upewnij się, że wynik nie został zdublowany.

Testy EditMode nie zastępują sprawdzenia dotyku, obrotu ekranu, wydajności i cyklu życia na fizycznym urządzeniu z Androidem.
