# Zasady projektu

## Wersja widoczna w grze

Przy każdym poleceniu zmieniającym kod zaktualizuj `PasjansApp.DisplayVersion`.

Format: `vYYYY.MM.DD_vCC.KK_HASH`.

- `YYYY.MM.DD` to dzień wprowadzenia zmiany.
- `CC` to liczba commitów wypchniętych na upstream bieżącej gałęzi, zapisana dwucyfrowo.
- `KK` rośnie przy każdym kolejnym poleceniu zmieniającym kod. Po zmianie `CC` zaczyna ponownie od `01`.
- `HASH` to skrót ostatniego wypchniętego commita. Obecna konwencja używa siedmiu znaków, zgodnie z przykładem `7652287`.

Używaj faktycznego stanu upstream. Nie licz lokalnych, niewypchniętych commitów do `CC` ani `HASH`.

Przy nowym wydaniu Androida zwiększ także `AndroidBundleVersionCode`.

## Ranking

Ranking jest obecnie wyłączony przez brak symbolu `PASJANS_RANKING`. Nie włączaj go przy zwykłych zmianach gry. Kod i istniejące pliki wyników zachowaj do ewentualnego powrotu funkcji. Po świadomym włączeniu symbolu sprawdź ponownie odczyt, zapis i układ menu.

# Legal, Licensing, Privacy and Security Compliance

## 1. Nowe dependencies

Przed dodaniem NuGet/Unity package, DLL, pluginu, SDK, JavaScript/Android library lub assetu sprawdź źródło, wersję i licencję, także zależności przechodnich. Nie dodawaj komponentu o nieznanej licencji bez ostrzeżenia programisty. Obecność w repo/cache nie dowodzi praw.

Po zmianie zaktualizuj `Compliance/Licenses/LICENSE_INVENTORY.md`, `Compliance/Security/DEPENDENCY_INVENTORY.md`, `Compliance/Legal/THIRD_PARTY_NOTICES.md`, odpowiednie teksty licencji i `SOURCE_PROVENANCE.md`. Rozróżnij narzędzia budowania/edytora i komponenty faktycznie dystrybuowane; zachowaj dowód i hash. MPL/LGPL/GPL/AGPL oraz inne obowiązki źródłowe wymagają osobnej analizy; samo dołączenie licencji nie zastępuje źródeł.

## 2. Nowe assety

Przed dodaniem grafiki, ikony, kart, muzyki, fontu, dźwięku, tekstury, modelu, animacji, materiału lub shaderu ustal źródło, autora, licencję, komercyjne użycie, modyfikację, dystrybucję i attribution. Aktualizuj `ASSET_INVENTORY.md` i provenance. Jeżeli praw nie można ustalić, ostrzeż programistę i oznacz `BLOCKING — SOURCE/RIGHTS UNKNOWN`; nie przypisuj domyślnej MIT/OFL/CC. Deklaracja autorstwa gry, .meta ani generator nie zastępują dowodu praw do konkretnego materiału.

## 3. Unity Asset Store

Sprawdź dla każdego nowego pakietu standardową EULA, Restricted Asset, non-standard EULA i dodatkowe third-party licences. Zapisz product ID, wersję, sprzedawcę i dowód nabycia (w repo tylko bezpieczny identyfikator). Samo pobranie z Asset Store nie wystarcza. UPM pakiet nie jest automatycznie Asset Store assetem. Potwierdź właściwy plan i aktualne prawa Unity runtime.

## 4. Dane osobowe

Zmiana dodająca Analytics, crash reporting, reklamy, telemetry, logowanie, konta, backend, cloud/save, push, lokalizację, kontakty, mikrofon, kamerę lub identyfikatory wymaga ponownego audytu prywatności i ostrzeżenia o możliwej aktualizacji Privacy PL/EN, Google Play Data Safety, zgód i regulaminu. Kontroluj Engine Diagnostics/Developer Data osobno od starego Analytics. Sprawdź faktyczny runtime, dane lokalne, backup, IndexedDB/cache/cookies, hosting logs, odbiorców, retencję i transfery. Nie deklaruj „brak danych” wyłącznie na podstawie kodu własnego albo braku reklam.

## 5. Google Play

Zmiany permissions, SDK, target API, reklam, danych, login/kont, płatności lub grupy wiekowej wymagają ostrzeżenia i ponownego sprawdzenia aktualnych oficjalnych zasad Play. Zaktualizuj checklistę i właściwe deklaracje. Nie zgaduj ratingu IARC, wieku, account verification ani obowiązku closed tests. Audytuj finalny AAB/split manifests; debug APK nie jest wydaniem produkcyjnym.

## 6. Security

Nie dodawaj do repo hardcoded secrets, prywatnych API keys, passwords, private certificates ani signing keys. Publiczne certyfikaty/identyfikatory odróżniaj od sekretów. Jeśli sekret zostanie wykryty, natychmiast ostrzeż programistę bez wyświetlania jego wartości i zaplanuj bezpieczną rotację; .gitignore nie usuwa historii wycieku. Kontroluj zależności, Unity advisories, podpisy, update channel i procedury Security. Przestrzegaj właściwych terminów CRA/RODO, jeśli dotyczą; nie zakładaj, że reporting CRA zaczyna się dopiero w 2027.

## 7. Release

Przed nazwaniem wersji produkcyjną przeczytaj `Compliance/STATUS.md`, `Compliance/ACTION_REQUIRED.md` oraz checklisty GooglePlay, Windows i Web. Przy `BLOCKING` nie stwierdzaj gotowości bez wyraźnego ostrzeżenia. Zamknij blocker dowodem, datą i zakresem; nie zmieniaj statusu tylko dla ukończenia zadania. Zachowaj finalne hashes, rzeczywisty SBOM, notices/source availability, publiczne dokumenty i approvals według `Compliance/Releases/README.md`. Zasady DisplayVersion/AndroidBundleVersionCode powyżej nadal obowiązują.

## 8. Prawo i polityki mogą się zmieniać

Przed pierwszym publicznym release, dużym releasem, reklamami, płatnościami, kontami, telemetry lub nową platformą sprawdź aktualne oficjalne źródła. Utrzymuj Last reviewed/Last verified, wersję projektu/Unity i Document status. Nie używaj Verified dla dokumentu z nierozstrzygniętym zakresem. Nie nadpisuj ręcznych danych firmy/kontaktu, decyzji prawnych, potwierdzonych licencji ani wyjątków bez wyraźnego powodu; przy sprzeczności nowego audytu ostrzeż zamiast zmieniać po cichu.

## 9. Niepewność

Przy niepewnej licencji, prawach, wymaganiach platformy lub sposobie przetwarzania danych powiedz `LEGAL/COMPLIANCE REVIEW REQUIRED` i wskaż dokładny komponent, brakujący dowód i działanie programisty. Oznacz REQUIRES_REVIEW lub BLOCKING zgodnie z audytem. Nie wymyślaj autorów, licencji, źródeł, wersji, danych wydawcy ani prawnej zgodności. Nie ukrywaj niepewności, żeby zakończyć zadanie.
