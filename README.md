# Pasjans Dziadka

Lekka gra w Unity 6000.3.25f1 na Windows i Android. Scena startowa: `Assets/Scenes/SampleScene.unity`. Otwórz ją i naciśnij **Play**. Interfejs i wszystkie ilustracje kart są tworzone przez kod przy uruchomieniu — nie trzeba podpinać prefabów ani pobierać grafik.

Gra jest przeznaczona do bezpłatnego udostępniania graczom. Nie zawiera reklam, zakupów ani abonamentu. Kod gry jest napisany w C#. Narzędzie Java używane wyłącznie przy budowaniu Androida to **Eclipse Temurin OpenJDK 17** dołączone do Unity, udostępniane bez opłat na GPLv2 z Classpath Exception — zobacz [FAQ Adoptium](https://adoptium.net/docs/faq). Nie jest potrzebna płatna subskrypcja Oracle JDK ani osobna instalacja Javy u gracza.

## Zasady i obsługa

- Jedna talia: 52 karty i 5 kolumn. Dwie talie (domyślnie): 104 karty tasowane razem i 10 kolumn. Kolejne kolumny otrzymują 1, 2, 3 itd. kart, z odkrytą kartą na wierzchu. Pozostałe 37 lub 49 kart trafia do talii dobierania.
- Karty układa się malejąco, naprzemiennie czerwone i czarne. Zachowana jest dotychczasowa zasada kodu: pusta kolumna przyjmuje dowolną odkrytą kartę lub poprawną sekwencję.
- Cztery lub osiem baz buduje się od asa do króla w tym samym kolorze karcianym. Karty z obu talii można łączyć; kolor rewersu nie ogranicza ruchów. Dozwolony jest powrót wierzchniej karty z bazy do kolumny.
- Pierwsza talia ma granatowy rewers, druga ciemnoczerwony. Każda karta ma własny identyfikator, także przy tej samej randze i kolorze.
- Pierwsze przejście przez talię: po **3** karty. Drugie: po **2**. Trzecie i wszystkie kolejne: po **1**. Kliknięcie pustej talii przekłada odkryte karty na nowo. Liczba przejść jest nieograniczona.
- Przenieś kartę lub odkryty stos przez przeciągnięcie albo dotknij karty, a następnie miejsca docelowego. Dwuklik / podwójne dotknięcie próbuje przenieść wierzchnią kartę na bazę. W odkrytych kartach zawsze widać do trzech ostatnich kart; ruch nadal dotyczy wyłącznie wierzchniej.
- Akcje gry są w menu: Windows używa przycisku **Plik** w stylu klasycznego menu programu, a Android przycisku **⋮** z rozwijaną listą.
- Pod „Nowa gra” znajduje się **Tryby gry →**. Na Windows najechanie otwiera boczne podmenu; sam wiersz nie jest klikalny. Na Androidzie podmenu otwiera dotknięcie. Zaznaczenie przy „Jedna talia” lub „Dwie talie” wskazuje wybór dla następnego rozdania.
- Zmiana trybu jest zapamiętywana, ale nie rusza kart ani licznika. **Nowa gra** wymaga potwierdzenia i dopiero wtedy tasuje wybraną liczbę pełnych talii. Anulowanie zachowuje bieżące rozdanie. Rozdanie nie jest gwarantowane jako wygrywalne.
- **Ranking** przechowuje 10 najlepszych wyników w kolumnach **Lp**, **Data z godziną**, **Ilość ruchów** i **Wygrana wg Dziadka**. Wszystkie wpisy „Tak” są wyżej od wpisów „Nie”, nawet gdy mają więcej ruchów. W każdej grupie mniej ruchów oznacza lepszą pozycję, a przy remisie wcześniejszy wynik ma pierwszeństwo. Starsze wpisy bez nowej informacji są wyświetlane jako „Nie”. Bez wpisów wyświetla „brak wpisów”; **X** wraca do gry.
- **O mnie** wyświetla tekst „Autorem gry jest Paweł Kaczmarczyk”; okno zamyka **X**.
- **Zakończ** zamyka aplikację. W edytorze Unity kończy tryb Play.
- Escape / systemowy przycisk Wstecz zamyka okno, usuwa zaznaczenie albo kończy grę. Android obsługuje pion, poziom i obszar bezpieczny ekranu.
- Brak dźwięku, wibracji, reklam i połączeń sieciowych w kodzie gry.

## Zapis

Licznik rozpoczyna od zera i zlicza tylko poprawne przeniesienia, dobierania i przewinięcia talii. Odsłonięcie **ostatniej zakrytej karty we wszystkich kolumnach** kończy liczenie — także wtedy, gdy w talii pozostały karty. Ten końcowy ruch wchodzi do wyniku. Dalsze układanie jest możliwe, ale nie zmienia wyniku. Ranking pozostaje wspólną tabelą 10 wyników dla obu trybów. Zapisuje datę i godzinę ukończenia; wznowienie tego samego rozdania nie powiela wpisu.

„Wygrana wg Dziadka” otrzymuje wartość **Tak**, gdy wszystkie karty w kolumnach są odkryte, a wszystkie karty z talii zakrytej i stosu odkrytego zostaną użyte podczas przejścia po 3, 2 albo 1 karcie. Rozpoczęcie następnego przejścia po jednej karcie ustawia nieodwracalne **Nie** i pokazuje czerwony komunikat „Wg Dziadka przegrałeś”. Spełnienie wszystkich warunków wcześniej pokazuje zielony komunikat „Wg Dziadka wygrałeś”. Jeśli stół odkryto wcześniej, wynik czeka na użycie pozostałych kart; zamknięcie gry zachowuje tę możliwość. Rozpoczęcie nowej gry przed rozstrzygnięciem zapisuje dotychczasowy standardowy wynik jako „Nie”.

Gra automatycznie zapisuje każdy udany ruch, dobieranie, przełożenie talii, nowe rozdanie oraz wybór trybu następnej gry. Ponawia niezakończony zapis przy utracie fokusu, pauzie i wyjściu. Wczytanie odtwarza kolejność wszystkich kart, ich talie i rewersy, odkrycia, licznik ruchów oraz bieżące dobieranie 3/2/1. Starszy zapis z siedmioma kolumnami można dokończyć; nowe rozdanie używa już 5 lub 10 kolumn.

Pliki znajdują się w `Application.persistentDataPath`. Na Windows standardowo `%USERPROFILE%\AppData\LocalLow\PawelKaczmarczyk\Pasjans Dziadka`, a na Androidzie w katalogu danych aplikacji `pl.pawelkaczmarczyk.pasjans`.

- `klondike-save.json` — bieżący zapis;
- `klondike-save.backup.json` — poprzedni poprawny zapis;
- `klondike-save.tmp` — plik roboczy przed atomowym zastąpieniem.
- `ranking.json`, `ranking.backup.json` — ranking oraz jego poprzednia poprawna wersja; niezależne od bieżącego rozdania.

Zapis ma numer wersji, sumę kontrolną i pełną walidację 52 lub 104 kart. Brak plików oznacza nowe rozdanie; przy uszkodzeniu głównego pliku gra próbuje kopii. Jeśli oba są uszkodzone, uruchamia świeżą grę. Nowszy, nierozpoznawany format jest zachowywany. Błędy zapisu nie przerywają rozgrywki; gra informuje o problemie, a przy wyjściu ostrzega o ryzyku utraty ostatnich ruchów. Usunięcie danych aplikacji lub jej odinstalowanie może usunąć zapis.

## Budowanie i testy

Menu Unity **Pasjans → Zbuduj → Windows (64-bit)** tworzy `Builds/Windows/Pasjans.exe` wraz z wymaganymi plikami. Do przeniesienia na inny komputer potrzebny jest cały folder Windows.

**Pasjans → Zbuduj → Android (APK)** tworzy `Builds/Android/Pasjans.apk`. Potrzebny jest moduł Android Build Support wraz z SDK, NDK i OpenJDK, instalowany w Unity Hub. Wersja lokalna korzysta z podpisu debug; dystrybucja sklepowa wymaga własnego podpisu wydawcy.

Testy reguł i zapisu są w **Window → General → Test Runner → EditMode**. Szczegóły i lista sprawdzeń urządzeń: [Documentation/Testowanie.md](Documentation/Testowanie.md).

## Kod i wydajność

- `Assets/Scripts/Core` — reguły bez zależności od Unity;
- `Assets/Scripts/Persistence` — zapis JSON i bezpieczne odzyskiwanie;
- `Assets/Scripts/UI` — obsługa myszy i dotyku, układ, własne grafiki proceduralne.

Współdzielone tekstury powstają tylko przy uruchomieniu. Pula 104 widoków kart jest ponownie wykorzystywana; nie powstają nowe obiekty przy ruchach, a niepotrzebne widoki są wyłączone. Układ jest przeliczany po ruchu lub zmianie ekranu, a limit 30 klatek/s ogranicza obciążenie. Brak fizyki, animacji ciągłych i logiki przeszukującej planszę w każdej klatce.
