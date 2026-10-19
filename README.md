# Pasjans Dziadka

Lekka gra w Unity 6000.3.25f1 na Windows, Android i WWW. Scena startowa: `Assets/Scenes/SampleScene.unity`. Otwórz ją i naciśnij **Play**. Interfejs i wszystkie ilustracje kart są tworzone przez kod przy uruchomieniu — nie trzeba podpinać prefabów ani pobierać grafik.

Gra jest przeznaczona do bezpłatnego udostępniania graczom. Nie zawiera reklam, zakupów ani abonamentu. Kod gry jest napisany w C#. Narzędzie Java używane wyłącznie przy budowaniu Androida to **Eclipse Temurin OpenJDK 17** dołączone do Unity, udostępniane bez opłat na GPLv2 z Classpath Exception — zobacz [FAQ Adoptium](https://adoptium.net/docs/faq). Nie jest potrzebna płatna subskrypcja Oracle JDK ani osobna instalacja Javy u gracza.

## Zasady i obsługa

- Jedna talia: 52 karty i 5 kolumn. Dwie talie (domyślnie): 104 karty tasowane razem i 10 kolumn. Kolejne kolumny otrzymują 1, 2, 3 itd. kart, z odkrytą kartą na wierzchu. Pozostałe 37 lub 49 kart trafia do talii dobierania.
- Karty układa się malejąco, naprzemiennie czerwone i czarne. Zachowana jest dotychczasowa zasada kodu: pusta kolumna przyjmuje dowolną odkrytą kartę lub poprawną sekwencję.
- Cztery lub osiem baz buduje się od asa do króla w tym samym kolorze karcianym. Karty z obu talii można łączyć; kolor rewersu nie ogranicza ruchów. Dozwolony jest powrót wierzchniej karty z bazy do kolumny.
- Komunikat **Brawo!** pojawia się, gdy wszystkie karty trafią na ukończone bazy albo tworzą w kolumnach pełne, odkryte sekwencje od króla do asa z naprzemiennymi kolorami. Można łączyć ukończone bazy i pełne kolumny; talia dobierania i stos odkryty muszą być puste.
- Pierwsza talia ma granatowy rewers, druga ciemnoczerwony. Każda karta ma własny identyfikator, także przy tej samej randze i kolorze.
- Pierwsze przejście przez talię: po **3** karty. Drugie: po **2**. Trzecie i wszystkie kolejne: po **1**. Kliknięcie pustej talii przekłada odkryte karty na nowo. Liczba przejść jest nieograniczona.
- Przenieś kartę lub odkryty stos przez przeciągnięcie albo dotknij karty, a następnie miejsca docelowego. Dwuklik / podwójne dotknięcie próbuje przenieść wierzchnią kartę na bazę. W odkrytych kartach zawsze widać do trzech ostatnich kart; ruch nadal dotyczy wyłącznie wierzchniej.
- Przy przeciąganiu punkt złapania karty pozostaje pod kursorem lub palcem, także po przekroczeniu progu rozpoczęcia ruchu. Miejsce upuszczenia wyznacza pozycja kursora/palca: cała wysokość kolumny i niewielki margines wokół pól ułatwiają trafienie. Niedozwolony cel lub puszczenie poza planszą przywraca karty na miejsce.
- Jeśli najpierw dotkniesz wierzchniej karty kolumny, a potem wierzchniej karty stosu odkrytego, gra wykona legalny ruch ze stosu odkrytego do kolumny. Ta odwrotna kolejność wymaga świeżego zaznaczenia; zaznaczenie z poprzedniej akcji nie wystarcza. Zakryta talia nadal służy do dobierania.
- Akcje gry są w menu: Windows używa przycisku **Plik** w stylu klasycznego menu programu, a Android przycisku **⋮** z rozwijaną listą.
- Pod „Nowa gra” znajduje się **Tryby gry →**. Na Windows najechanie otwiera boczne podmenu; sam wiersz nie jest klikalny. Na Androidzie podmenu otwiera dotknięcie. Zaznaczenie przy „Jedna talia” lub „Dwie talie” wskazuje wybór dla następnego rozdania.
- Zmiana trybu jest zapamiętywana, ale nie rusza kart. **Nowa gra** wymaga potwierdzenia i dopiero wtedy tasuje wybraną liczbę pełnych talii. Anulowanie zachowuje bieżące rozdanie. Rozdanie nie jest gwarantowane jako wygrywalne.
- **Ranking jest czasowo wyłączony.** Gra nie odczytuje ani nie zapisuje plików rankingu. Kod pozostał za symbolem kompilacji `PASJANS_RANKING`; jego ponowne włączenie wymaga dodania tego symbolu w ustawieniach Unity i przebudowania gry.
- **O mnie** wyświetla tekst „Autorem gry jest Paweł Kaczmarczyk”; okno zamyka **X**.
- **Zakończ** zamyka aplikację. W edytorze Unity kończy tryb Play, a w przeglądarce zatrzymuje grę na bieżącej stronie; odświeżenie ją wznawia.
- Escape / systemowy przycisk Wstecz zamyka okno, usuwa zaznaczenie albo kończy grę. Android obsługuje pion, poziom i obszar bezpieczny ekranu.
- Brak dźwięku, wibracji, reklam i połączeń sieciowych w kodzie gry.

## Zapis

Wewnętrzny licznik rozpoczyna od zera i zlicza tylko poprawne przeniesienia, dobierania i przewinięcia talii. Odsłonięcie **ostatniej zakrytej karty we wszystkich kolumnach** kończy liczenie — także wtedy, gdy w talii pozostały karty. Licznik nie jest wyświetlany, a ranking nie zapisuje wyników. Stan rozgrywki nadal zachowuje dane potrzebne do wznowienia gry i zasad Dziadka.

„Wygrana wg Dziadka” pojawia się, gdy wszystkie karty w kolumnach są odkryte, a wszystkie karty z talii zakrytej i stosu odkrytego zostaną użyte podczas przejścia po 3, 2 albo 1 karcie. Rozpoczęcie następnego przejścia po jednej karcie pokazuje czerwony komunikat „Wg Dziadka przegrałeś”. Spełnienie wszystkich warunków wcześniej pokazuje zielony komunikat „Wg Dziadka wygrałeś”. Jeśli stół odkryto wcześniej, rozstrzygnięcie czeka na użycie pozostałych kart; zamknięcie gry zachowuje tę możliwość.

Gra automatycznie zapisuje każdy udany ruch, dobieranie, przełożenie talii, nowe rozdanie oraz wybór trybu następnej gry. Ponawia niezakończony zapis przy utracie fokusu, pauzie i wyjściu. Wczytanie odtwarza kolejność wszystkich kart, ich talie i rewersy, odkrycia, licznik ruchów oraz bieżące dobieranie 3/2/1. Starszy zapis z siedmioma kolumnami można dokończyć; nowe rozdanie używa już 5 lub 10 kolumn.

Pliki znajdują się w `Application.persistentDataPath`. Na Windows standardowo `%USERPROFILE%\AppData\LocalLow\PawelKaczmarczyk\Pasjans Dziadka`, na Androidzie w katalogu danych aplikacji `pl.pawelkaczmarczyk.pasjans`, a na WWW w IndexedDB przeglądarki.

- `klondike-save.json` — bieżący zapis;
- `klondike-save.backup.json` — poprzedni poprawny zapis;
- `klondike-save.tmp` — plik roboczy przed atomowym zastąpieniem.
- `ranking.json`, `ranking.backup.json` — istniejące pliki rankingu pozostają na miejscu, ale nie są obecnie odczytywane ani zapisywane.

Zapis ma numer wersji, sumę kontrolną i pełną walidację 52 lub 104 kart. Brak plików oznacza nowe rozdanie; przy uszkodzeniu głównego pliku gra próbuje kopii. Jeśli oba są uszkodzone, uruchamia świeżą grę. Nowszy, nierozpoznawany format jest zachowywany. Błędy zapisu nie przerywają rozgrywki; gra informuje o problemie, a przy wyjściu ostrzega o ryzyku utraty ostatnich ruchów. Usunięcie danych aplikacji lub jej odinstalowanie może usunąć zapis.

## Budowanie i testy

Menu Unity **Pasjans → Zbuduj → Windows (64-bit)** tworzy `Builds/Windows/Pasjans.exe` wraz z wymaganymi plikami. Do przeniesienia na inny komputer potrzebny jest cały folder Windows.

**Pasjans → Zbuduj → Android (APK)** tworzy `Builds/Android/Pasjans.apk`. Potrzebny jest moduł Android Build Support wraz z SDK, NDK i OpenJDK, instalowany w Unity Hub. Wersja lokalna korzysta z podpisu debug; dystrybucja sklepowa wymaga własnego podpisu wydawcy.

**Pasjans → Zbuduj → WWW (WebGL)** tworzy `Builds/Web`. Prześlij całą zawartość katalogu, łącznie z `index.html` i folderem `Build`, do katalogu `/pasjans/` na serwerze. Otwórz `https://mojastrona.pl/pasjans/`. Build używa względnych ścieżek, nie wymaga nagłówków dla skompresowanych zasobów Unity i synchronizuje zapis gry z IndexedDB przeglądarki. Zapis jest związany z domeną i katalogiem strony. Uruchamianie przez `file://` nie działa; serwer powinien podawać `.wasm` jako `application/wasm` dla szybszego startu.

Testy reguł i zapisu są w **Window → General → Test Runner → EditMode**. Szczegóły i lista sprawdzeń urządzeń: [Documentation/Testowanie.md](Documentation/Testowanie.md).

## Kod i wydajność

- `Assets/Scripts/Core` — reguły bez zależności od Unity;
- `Assets/Scripts/Persistence` — zapis JSON i bezpieczne odzyskiwanie;
- `Assets/Scripts/UI` — obsługa myszy i dotyku, układ, własne grafiki proceduralne.

Współdzielone tekstury powstają tylko przy uruchomieniu. Pula 104 widoków kart jest ponownie wykorzystywana; nie powstają nowe obiekty przy ruchach, a niepotrzebne widoki są wyłączone. Układ jest przeliczany po ruchu lub zmianie ekranu, a limit 30 klatek/s ogranicza obciążenie. Brak fizyki, animacji ciągłych i logiki przeszukującej planszę w każdej klatce.
