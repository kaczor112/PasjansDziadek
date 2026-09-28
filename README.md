# Pasjans Dziadka

Lekka gra w Unity 6000.3.25f1 na Windows i Android. Scena startowa: `Assets/Scenes/SampleScene.unity`. Otwórz ją i naciśnij **Play**. Interfejs i wszystkie ilustracje kart są tworzone przez kod przy uruchomieniu — nie trzeba podpinać prefabów ani pobierać grafik.

Gra jest przeznaczona do bezpłatnego udostępniania graczom. Nie zawiera reklam, zakupów ani abonamentu. Kod gry jest napisany w C#. Narzędzie Java używane wyłącznie przy budowaniu Androida to **Eclipse Temurin OpenJDK 17** dołączone do Unity, udostępniane bez opłat na GPLv2 z Classpath Exception — zobacz [FAQ Adoptium](https://adoptium.net/docs/faq). Nie jest potrzebna płatna subskrypcja Oracle JDK ani osobna instalacja Javy u gracza.

## Zasady i obsługa

- Klondike: 52 karty, 7 kolumn, naprzemienne czerwone i czarne karty malejąco. Na pustą kolumnę można położyć króla lub prawidłowy stos rozpoczynający się królem.
- Cztery bazy buduje się od asa do króla w tym samym kolorze karcianym. Dozwolony jest powrót wierzchniej karty z bazy do kolumny.
- Pierwsze przejście przez talię: po **3** karty. Drugie: po **2**. Trzecie i wszystkie kolejne: po **1**. Kliknięcie pustej talii przekłada odkryte karty na nowo. Liczba przejść jest nieograniczona.
- Przenieś kartę lub odkryty stos przez przeciągnięcie albo dotknij karty, a następnie miejsca docelowego. Dwuklik / podwójne dotknięcie próbuje przenieść wierzchnią kartę na bazę. W odkrytych kartach zawsze widać do trzech ostatnich kart; ruch nadal dotyczy wyłącznie wierzchniej.
- Akcje gry są w menu: Windows używa przycisku **Plik** w stylu klasycznego menu programu, a Android przycisku **⋮** z rozwijaną listą.
- **Nowa gra** wymaga potwierdzenia i tasuje pełną talię. Rozdanie nie jest gwarantowane jako wygrywalne, tak jak przy zwykłym losowym tasowaniu.
- **Ranking** jest między „Nowa gra” a „O mnie”. Przechowuje 10 najlepszych wyników (mniej ruchów to lepiej), w kolumnach **Lp**, **Data z godziną**, **Ilość ruchów**. Przy remisie wcześniejszy wynik ma pierwszeństwo. Bez wpisów wyświetla „brak wpisów”; **X** wraca do gry.
- **O mnie** wyświetla tekst „Autorem gry jest Paweł Kaczmarczyk”; okno zamyka **X**.
- **Zakończ** zamyka aplikację. W edytorze Unity kończy tryb Play.
- Escape / systemowy przycisk Wstecz zamyka okno, usuwa zaznaczenie albo kończy grę. Android obsługuje pion, poziom i obszar bezpieczny ekranu.
- Brak dźwięku, wibracji, reklam i połączeń sieciowych w kodzie gry.

## Zapis

Licznik rozpoczyna od zera i zlicza tylko poprawne przeniesienia, dobierania i przewinięcia talii. Odsłonięcie **ostatniej zakrytej karty w siedmiu kolumnach** kończy liczenie — także wtedy, gdy w talii pozostały karty. Ten końcowy ruch wchodzi do wyniku. Dalsze układanie jest możliwe, ale nie zmienia wyniku. Ranking zapisuje datę i godzinę ukończenia; wznowienie tego samego rozdania nie powiela wpisu.

Gra automatycznie zapisuje każdy udany ruch, dobieranie, przełożenie talii i nowe rozdanie. Ponawia niezakończony zapis przy utracie fokusu, pauzie i wyjściu. Wczytanie odtwarza kolejność wszystkich kart, odkrycia, licznik ruchów oraz bieżące dobieranie 3/2/1.

Pliki znajdują się w `Application.persistentDataPath`. Na Windows standardowo `%USERPROFILE%\AppData\LocalLow\PawelKaczmarczyk\Pasjans Dziadka`, a na Androidzie w katalogu danych aplikacji `pl.pawelkaczmarczyk.pasjans`.

- `klondike-save.json` — bieżący zapis;
- `klondike-save.backup.json` — poprzedni poprawny zapis;
- `klondike-save.tmp` — plik roboczy przed atomowym zastąpieniem.
- `ranking.json`, `ranking.backup.json` — ranking oraz jego poprzednia poprawna wersja; niezależne od bieżącego rozdania.

Zapis ma numer wersji, sumę kontrolną i pełną walidację 52 kart. Brak plików oznacza nowe rozdanie; przy uszkodzeniu głównego pliku gra próbuje kopii. Jeśli oba są uszkodzone, uruchamia świeżą grę. Nowszy, nierozpoznawany format jest zachowywany. Błędy zapisu nie przerywają rozgrywki; gra informuje o problemie, a przy wyjściu ostrzega o ryzyku utraty ostatnich ruchów. Usunięcie danych aplikacji lub jej odinstalowanie może usunąć zapis.

## Budowanie i testy

Menu Unity **Pasjans → Zbuduj → Windows (64-bit)** tworzy `Builds/Windows/Pasjans.exe` wraz z wymaganymi plikami. Do przeniesienia na inny komputer potrzebny jest cały folder Windows.

**Pasjans → Zbuduj → Android (APK)** tworzy `Builds/Android/Pasjans.apk`. Potrzebny jest moduł Android Build Support wraz z SDK, NDK i OpenJDK, instalowany w Unity Hub. Wersja lokalna korzysta z podpisu debug; dystrybucja sklepowa wymaga własnego podpisu wydawcy.

Testy reguł i zapisu są w **Window → General → Test Runner → EditMode**. Szczegóły i lista sprawdzeń urządzeń: [Documentation/Testowanie.md](Documentation/Testowanie.md).

## Kod i wydajność

- `Assets/Scripts/Core` — reguły bez zależności od Unity;
- `Assets/Scripts/Persistence` — zapis JSON i bezpieczne odzyskiwanie;
- `Assets/Scripts/UI` — obsługa myszy i dotyku, układ, własne grafiki proceduralne.

Współdzielone tekstury powstają tylko przy uruchomieniu. Widoki 52 kart są ponownie wykorzystywane; nie powstają nowe obiekty przy ruchach. Układ jest przeliczany po ruchu lub zmianie ekranu, a limit 30 klatek/s ogranicza obciążenie. Brak fizyki, animacji ciągłych i logiki przeszukującej planszę w każdej klatce.
