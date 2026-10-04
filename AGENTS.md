# Zasady projektu

## Wersja widoczna w grze

Przy każdym poleceniu zmieniającym kod zaktualizuj `PasjansApp.DisplayVersion`.

Format: `vYYYY.MM.DD_CC_vKK_HASH`.

- `YYYY.MM.DD` to dzień wprowadzenia zmiany.
- `CC` to liczba commitów wypchniętych na upstream bieżącej gałęzi, zapisana dwucyfrowo.
- `KK` rośnie przy każdym kolejnym poleceniu zmieniającym kod. Po zmianie `CC` zaczyna ponownie od `01`.
- `HASH` to skrót ostatniego wypchniętego commita. Obecna konwencja używa siedmiu znaków, zgodnie z przykładem `7652287`.

Używaj faktycznego stanu upstream. Nie licz lokalnych, niewypchniętych commitów do `CC` ani `HASH`.

Przy nowym wydaniu Androida zwiększ także `AndroidBundleVersionCode`.
