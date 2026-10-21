using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Text.RegularExpressions;
using Pasjans.Core;
using Pasjans.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Pasjans.Editor
{
    /// <summary>
    /// Test uruchamiany w edytorze z grafiką. Użyj -batchmode -executeMethod
    /// Pasjans.Editor.SmokeCapture.Run --pasjans-save-dir &lt;project&gt;/Temp/SmokeSaves.
    /// Nie dodawaj -quit ani -nographics; narzędzie kończy się po wykonaniu zrzutów.
    /// Zrzuty obejmują rzeczywisty interfejs renderowany przez widok Game.
    /// </summary>
    [InitializeOnLoad]
    public static class SmokeCapture
    {
        const string Prefix = "Pasjans.SmokeCapture.";
        const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static EditorWindow gameView;
        static string beforeModeChange;
        static Mouse smokeMouse;
        static Mouse[] previousMice;

        public static void RunModes()
        {
            Run();
            SessionState.SetBool(Prefix + "Modes", true);
        }

        public static void RunGrandpa()
        {
            Run();
            SessionState.SetBool(Prefix + "Grandpa", true);
        }

        static SmokeCapture()
        {
            EditorApplication.update += Update;
        }

        public static void Run()
        {
            try
            {
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                    throw new InvalidOperationException("Screenshot smoke run requires graphics; remove -nographics.");
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Start the capture in edit mode.");
                string[] args = Environment.GetCommandLineArgs();
                bool hasIsolatedSaveDirectory = false;
                for (int i = 0; i + 1 < args.Length; i++)
                {
                    if (args[i] != "--pasjans-save-dir") continue;
                    string isolatedPath = Path.GetFullPath(args[i + 1]);
                    string tempRoot = Path.GetFullPath("Temp") + Path.DirectorySeparatorChar;
                    if (!isolatedPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Smoke saves must be inside the project's Temp directory.");
                    hasIsolatedSaveDirectory = true;
                    break;
                }
                if (!hasIsolatedSaveDirectory)
                    throw new InvalidOperationException("Pass --pasjans-save-dir <project>/Temp/SmokeSaves to protect personal saves.");

                string output = Path.GetFullPath(Path.Combine("Logs", "Screenshots", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")));
                Directory.CreateDirectory(output);
                SessionState.SetString(Prefix + "Output", output);
                SessionState.SetBool(Prefix + "Modes", false);
                SessionState.SetBool(Prefix + "Grandpa", false);
                SessionState.SetFloat(Prefix + "Deadline", (float)EditorApplication.timeSinceStartup + 180f);
                SessionState.SetInt(Prefix + "Stage", 1);
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
                SetGameViewSize(1280, 800);
                Debug.Log("Pasjans smoke screenshots: " + output);
                EditorApplication.isPlaying = true;
            }
            catch (Exception exception)
            {
                Finish(false, exception.ToString());
            }
        }

        static void Update()
        {
            int stage = SessionState.GetInt(Prefix + "Stage", 0);
            if (stage == 0) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Prefix + "Deadline", 0))
                    throw new TimeoutException("Timed out waiting for Unity frames or screenshot output (stage " + stage + ").");
                EditorApplication.QueuePlayerLoopUpdate();
                GetGameView().Repaint();
                if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
                if (SessionState.GetBool(Prefix + "Grandpa", false)) { UpdateGrandpa(stage); return; }
                if (SessionState.GetBool(Prefix + "Modes", false)) { UpdateModes(stage); return; }

                switch (stage)
                {
                    case 1:
                        if (FindApp() == null) return;
                        SetGameViewSize(1280, 800);
                        SetStage(2);
                        break;
                    case 2:
                        if (!Ready(1280, 800)) return;
                        FindApp().SendMessage("Draw", SendMessageOptions.RequireReceiver);
                        SetStage(3);
                        break;
                    case 3:
                        if (!Ready(1280, 800)) return;
                        Capture("landscape.png", 4);
                        break;
                    case 4:
                        if (!CaptureComplete("landscape.png", 1280, 800)) return;
                        FindApp().SendMessage("ShowAbout", SendMessageOptions.RequireReceiver);
                        SetStage(5);
                        break;
                    case 5:
                        if (!Ready(1280, 800)) return;
                        Capture("about.png", 6);
                        break;
                    case 6:
                        if (!CaptureComplete("about.png", 1280, 800)) return;
                        FindApp().SendMessage("CloseModal", SendMessageOptions.RequireReceiver);
                        SetGameViewSize(720, 1280);
                        SetStage(7);
                        break;
                    case 7:
                        if (!Ready(720, 1280)) return;
                        Capture("portrait.png", 8);
                        break;
                    case 8:
                        if (!CaptureComplete("portrait.png", 720, 1280)) return;
#if PASJANS_RANKING
                        FindApp().SendMessage("ShowRanking", SendMessageOptions.RequireReceiver);
                        SetStage(9);
#else
                        Finish(true, "Sprawdzono planszę, okno autora i obie orientacje bez rankingu.");
#endif
                        break;
#if PASJANS_RANKING
                    case 9:
                        if (!Ready(720, 1280)) return;
                        Capture("ranking-empty.png", 10);
                        break;
                    case 10:
                        if (!CaptureComplete("ranking-empty.png", 720, 1280)) return;
                        PopulateSampleRanking();
                        FindApp().SendMessage("ShowRanking", SendMessageOptions.RequireReceiver);
                        SetStage(11);
                        break;
                    case 11:
                        if (!Ready(720, 1280)) return;
                        Capture("ranking.png", 12);
                        break;
                    case 12:
                        if (!CaptureComplete("ranking.png", 720, 1280)) return;
                        Finish(true, "Captured game, About and ranking dialogs in " + SessionState.GetString(Prefix + "Output", ""));
                        break;
#endif
                }
            }
            catch (Exception exception)
            {
                Finish(false, exception.ToString());
            }
        }

        static void SetStage(int stage)
        {
            SessionState.SetInt(Prefix + "Stage", stage);
            SessionState.SetInt(Prefix + "StartFrame", Time.frameCount);
            SessionState.SetFloat(Prefix + "StartTime", (float)EditorApplication.timeSinceStartup);
        }

        // Scenariusz sprawdza komunikaty Dziadka i ukończenie gry po przegranej.
        static void UpdateGrandpa(int stage)
        {
            var app = FindApp() as PasjansApp;
            if (app == null) return;
            switch (stage)
            {
                case 1:
                    SetGameViewSize(1280, 800);
                    LoadGrandpaState(app, true, 148);
                    SetStage(2);
                    break;
                case 2:
                    if (!Ready(1280, 800)) return;
                    VerifyGrandpaBanner(app, true);
                    Capture("grandpa-win.png", 3);
                    break;
                case 3:
                    if (!CaptureComplete("grandpa-win.png", 1280, 800)) return;
                    LoadGrandpaState(app, false, 54);
                    SetStage(4);
                    break;
                case 4:
                    if (!Ready(1280, 800)) return;
                    VerifyGrandpaBanner(app, false);
                    Capture("grandpa-loss.png", 5);
                    break;
                case 5:
                    if (!CaptureComplete("grandpa-loss.png", 1280, 800)) return;
                    LoadGrandpaState(app, true, 54, true);
                    Field<Button>(app, "modalClose").onClick.Invoke();
                    SetStage(8);
                    break;
                case 8:
                    if (!Ready(1280, 800)) return;
                    VerifyGrandpaBanner(app, false);
                    Capture("grandpa-late-win-landscape.png", 9);
                    break;
                case 9:
                    if (!CaptureComplete("grandpa-late-win-landscape.png", 1280, 800)) return;
                    SetGameViewSize(720, 1280);
                    SetStage(10);
                    break;
                case 10:
                    if (!Ready(720, 1280)) return;
                    VerifyGrandpaBanner(app, false);
                    Capture("grandpa-late-win-portrait.png", 11);
                    break;
                case 11:
                    if (!CaptureComplete("grandpa-late-win-portrait.png", 720, 1280)) return;
#if PASJANS_RANKING
                    PopulateSampleRanking();
                    app.SendMessage("ShowRanking", SendMessageOptions.RequireReceiver);
                    SetGameViewSize(720, 1280);
                    SetStage(6);
#else
                    Finish(true, "Sprawdzono zieloną i czerwoną belkę oraz żółtą po ukończeniu gry, w obu orientacjach.");
#endif
                    break;
#if PASJANS_RANKING
                case 6:
                    if (!Ready(720, 1280)) return;
                    var cells = Field<Text[,]>(app, "rankingCells");
                    Check(cells[0, 3].text == "Wygrana wg Dziadka", "Brak czwartej kolumny rankingu.");
                    for (int row = 1; row <= 5; row++) Check(cells[row, 3].text == "Tak", "Wyniki Tak muszą być pierwsze.");
                    for (int row = 6; row <= 10; row++) Check(cells[row, 3].text == "Nie", "Wyniki Nie muszą być niżej.");
                    Capture("ranking-grandpa.png", 7);
                    break;
                case 7:
                    if (!CaptureComplete("ranking-grandpa.png", 720, 1280)) return;
                    Finish(true, "Zweryfikowano zielony i czerwony komunikat oraz priorytet Tak w rankingu.");
                    break;
#endif
            }
        }

        static void LoadGrandpaState(PasjansApp app, bool win, int moves, bool lateWin = false)
        {
            if (Field<RectTransform>(app, "modal").gameObject.activeSelf)
                Field<Button>(app, "modalClose").onClick.Invoke();
            var state = SampleGrandpaState(moves, win);
            if (lateWin)
            {
                state.drawCount = 1;
                state.stockRecycleCount = 3;
                state.grandpaOutcome = GameState.GrandpaLoss;
            }
            Check(app.Game.TryLoad(state, out string error), error);
            app.SendMessage("Layout", SendMessageOptions.RequireReceiver);
            app.SendMessage("Refresh", SendMessageOptions.RequireReceiver);
        }

        static void VerifyGrandpaBanner(PasjansApp app, bool win)
        {
            var banner = Field<Image>(app, "grandpaBanner");
            var label = Field<Text>(app, "grandpaBannerText");
            bool lateWin = !win && app.Game.IsWon;
            Check(banner.gameObject.activeSelf, "Komunikat Dziadka jest ukryty.");
            Check(label.text == (win ? "Wg Dziadka wygrałeś" : lateWin ? "Wygrałeś, ale Dziadek by nie uznał :)" : "Wg Dziadka przegrałeś"),
                "Niepoprawny tekst komunikatu.");
            Check(win ? banner.color.g > banner.color.r : lateWin ? banner.color.r > .8f && banner.color.g > .6f && banner.color.b < .3f :
                banner.color.r > banner.color.g && banner.color.g < .3f, "Niepoprawny kolor komunikatu.");
            Check(lateWin ? label.color == PasjansApp.Ink : label.color.r > .8f,
                "Tekst belki ma zbyt niski kontrast.");
            Check(label.preferredHeight <= label.rectTransform.rect.height, "Tekst nie mieści się w belce.");
            VerifyDrawLabel(app);
            VerifyNoScore(app);
        }

        // Scenariusz sprawdza przyciski i układ na odizolowanym zapisie testowym.
        static void UpdateModes(int stage)
        {
            var app = FindApp() as PasjansApp;
            if (app == null) return;
            switch (stage)
            {
                case 1:
                    SetGameViewSize(1280, 800);
                    Check(app.Game.State.DeckCount == 2 && app.Game.SelectedDeckCount == 2,
                        "Nowa instalacja musi domyślnie uruchamiać tryb dwóch talii.");
                    app.Game.SelectDeckCount(1);
                    app.SendMessage("ForceNewGame");
                    app.SendMessage("Draw");
                    SetStage(2);
                    break;
                case 2:
                    if (!Ready(1280, 800)) return;
                    VerifyBoard(app, 5, 4);
                    DragSmokeChecks.Verify(app);
                    Capture("one-deck-landscape.png", 3);
                    break;
                case 3:
                    if (!CaptureComplete("one-deck-landscape.png", 1280, 800)) return;
                    app.SendMessage("ToggleMenu");
                    var row = Field<RectTransform>(app, "modeItem");
                    Check(row.GetComponent<Button>() == null, "Wiersz trybów nie może być przyciskiem.");
                    var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                    ExecuteEvents.Execute(row.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                    Check(!Field<RectTransform>(app, "modePopup").gameObject.activeSelf, "Kliknięcie w Windows nie otwiera podmenu.");
                    Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, row.TransformPoint(row.rect.center));
                    // Wirtualny kursor uniezależnia test od myszy i ukrytego okna Unity.
                    previousMice = InputSystem.devices.OfType<Mouse>().Where(mouse => mouse.enabled).ToArray();
                    foreach (var mouse in previousMice) InputSystem.DisableDevice(mouse);
                    smokeMouse = InputSystem.AddDevice<Mouse>();
                    InputState.Change(smokeMouse.position, screenPoint);
                    ExecuteEvents.Execute(row.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                    Check(Field<RectTransform>(app, "modePopup").gameObject.activeSelf, "Najechanie musi otworzyć podmenu.");
                    SetStage(4);
                    break;
                case 4:
                    if (!Ready(1280, 800)) return;
                    Check(Field<RectTransform>(app, "modePopup").gameObject.activeSelf, "Podmenu znika pod kursorem.");
                    VerifyInformationMenu(app);
                    Capture("modes-windows.png", 5);
                    break;
                case 5:
                    if (!CaptureComplete("modes-windows.png", 1280, 800)) return;
                    beforeModeChange = JsonUtility.ToJson(app.Game.State);
                    Field<Button>(app, "twoDeckButton").onClick.Invoke();
                    Check(app.Game.SelectedDeckCount == 2, "Brak zmiany wyboru.");
                    CheckSelectionOnly(app);
                    app.SendMessage("AskNewGame");
                    Field<Button>(app, "modalCancel").onClick.Invoke();
                    CheckSelectionOnly(app);
                    app.SendMessage("AskNewGame");
                    Field<Button>(app, "modalAccept").onClick.Invoke();
                    Check(app.Game.State.DeckCount == 2 && app.Game.MoveCount == 0, "Potwierdzenie musi rozdać dwie talie.");
                    app.SendMessage("Draw");
                    SetStage(6);
                    break;
                case 6:
                    if (!Ready(1280, 800)) return;
                    VerifyBoard(app, 10, 8);
                    DragSmokeChecks.Verify(app);
                    Check(!Field<Button>(app, "rotateButton").gameObject.activeSelf, "Przycisk obrotu nie może być widoczny w Windows.");
                    var hidden = app.GetComponentsInChildren<CardView>().Where(c => !c.Card.faceUp).ToArray();
                    Check(hidden.Select(c => c.Card.deckIndex).Distinct().Count() == 2, "Brak dwóch talii na planszy.");
                    Check(hidden.All(c => c.GetComponent<Image>().sprite == app.Art.BackFor(c.Card.deckIndex)), "Niepoprawny rewers.");
                    Capture("two-decks-landscape.png", 7);
                    break;
                case 7:
                    if (!CaptureComplete("two-decks-landscape.png", 1280, 800)) return;
                    app.SimulateTouchMenu = true;
                    SetGameViewSize(720, 1280);
                    SetStage(8);
                    break;
                case 8:
                    if (!Ready(720, 1280)) return;
                    VerifyBoard(app, 10, 8);
                    DragSmokeChecks.Verify(app);
                    var rotate = Field<Button>(app, "rotateButton");
                    Check(rotate.gameObject.activeSelf, "Brak przycisku obrotu w układzie Androida.");
                    var rotateRect = (RectTransform)rotate.transform;
                    Check(Mathf.Abs(rotateRect.rect.width - rotateRect.rect.height) < .1f, "Przycisk obrotu musi być kwadratowy.");
                    Check(rotate.GetComponentsInChildren<Image>().Any(image => image.sprite == app.Art.RotateScreen), "Brak graficznej ikony obrotu.");
                    Capture("two-decks-portrait.png", 9);
                    break;
                case 9:
                    if (!CaptureComplete("two-decks-portrait.png", 720, 1280)) return;
                    app.SendMessage("ToggleMenu");
                    var touchRow = Field<RectTransform>(app, "modeItem");
                    var touch = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                    ExecuteEvents.Execute(touchRow.gameObject, touch, ExecuteEvents.pointerEnterHandler);
                    Check(!Field<RectTransform>(app, "modePopup").gameObject.activeSelf, "Dotykowe menu czeka na dotknięcie.");
                    ExecuteEvents.Execute(touchRow.gameObject, touch, ExecuteEvents.pointerClickHandler);
                    Check(Field<RectTransform>(app, "modePopup").gameObject.activeSelf, "Dotknięcie musi otworzyć podmenu.");
                    SetStage(10);
                    break;
                case 10:
                    if (!Ready(720, 1280)) return;
                    VerifyInformationMenu(app);
                    Capture("modes-android-portrait.png", 11);
                    break;
                case 11:
                    if (!CaptureComplete("modes-android-portrait.png", 720, 1280)) return;
                    Field<Button>(app, "oneDeckButton").onClick.Invoke();
                    Check(app.Game.State.DeckCount == 2 && app.Game.SelectedDeckCount == 1, "Wybór nie może zmieniać rozdania.");
                    app.SendMessage("AskNewGame");
                    Field<Button>(app, "modalAccept").onClick.Invoke();
                    SetStage(12);
                    break;
                case 12:
                    if (!Ready(720, 1280)) return;
                    VerifyBoard(app, 5, 4);
                    DragSmokeChecks.Verify(app);
                    Capture("one-deck-portrait.png", 13);
                    break;
                case 13:
                    if (!CaptureComplete("one-deck-portrait.png", 720, 1280)) return;
                    for (int decks = 1; decks <= 2; decks++)
                    {
                        VerifyReverseWasteClicks(app, decks);
                        VerifyDrawPhases(app, decks);
                        VerifyTableauWinDialog(app, decks);
                        VerifyTableauWinDialog(app, decks, true);
                    }
                    Finish(true, "Zweryfikowano oba układy, menu, przeciąganie, zaznaczenia, fazy dobierania i brak liczbowego wyniku.");
                    break;
            }
        }

        static void VerifyReverseWasteClicks(PasjansApp app, int decks)
        {
            var state = new GameState
            {
                deckCount = decks, selectedDeckCount = decks,
                tableau = GameState.CreatePiles(5 * decks), foundations = GameState.CreatePiles(4 * decks),
                // Licznik wyniku już stoi, ale nowa akcja nadal musi unieważnić zaznaczenie.
                moveCount = 9, finalMoves = 9, completedUtcTicks = DateTime.UtcNow.Ticks
            };
            state.tableau[0].cards.Add(new CardData(Suit.Clubs, 8, true));
            state.waste.Add(new CardData(Suit.Spades, 6, true));
            state.waste.Add(new CardData(Suit.Hearts, 7, true));
            var used = state.tableau[0].cards.Concat(state.waste).Select(c => c.id).ToArray();
            for (int deck = 0; deck < decks; deck++)
                for (int suit = 0; suit < 4; suit++)
                    for (int rank = 1; rank <= 13; rank++)
                    {
                        var card = new CardData((Suit)suit, rank, false, deck);
                        if (!used.Contains(card.id)) state.stock.Add(card);
                    }
            app.SendMessage("CancelSelection");
            Check(app.Game.TryLoad(state, out string error), error);
            app.SendMessage("Layout");
            app.SendMessage("Refresh");
            var views = Field<CardView[]>(app, "cards");
            var target = views[state.tableau[0].cards[0].id];
            var seven = views[state.waste[1].id];
            var six = views[state.waste[0].id];
            app.CardClicked(target, 1);
            VerifyDrawLabel(app);
            long oldSelection = Field<long>(app, "selectedActionSequence");
            app.CardClicked(seven, 1);
            Check(app.Game.State.tableau[0].cards.Count == 2 && app.Game.State.waste.Count == 1,
                "Kolejność kolumna → stos odkryty nie wykonała legalnego ruchu.");
            VerifyDrawLabel(app);
            Check(app.Game.MoveCount == 9 && Field<CardView>(app, "selected") == null,
                "Ruch musi usunąć zaznaczenie także po zatrzymaniu licznika.");

            // Odtwórz stary podświetlony widok po poprzedniej akcji.
            typeof(PasjansApp).GetField("selected", InstanceFlags).SetValue(app, seven);
            typeof(PasjansApp).GetField("selectedActionSequence", InstanceFlags).SetValue(app, oldSelection);
            app.CardClicked(six, 1);
            Check(app.Game.State.waste.Count == 1 && Field<CardView>(app, "selected") == six,
                "Stare zaznaczenie uruchomiło odwrotny ruch lub nie zaznaczono ostatniej karty.");
            app.CardClicked(seven, 1);
            Check(app.Game.State.tableau[0].cards.Count == 3 && app.Game.State.waste.Count == 0,
                "Zwykła kolejność stos odkryty → kolumna przestała działać.");

            app.CardClicked(six, 1);
            app.PileClicked(new PileRef(PileKind.Stock));
            Check(app.Game.State.waste.Count == 3 && Field<CardView>(app, "selected") == null,
                "Zakryta talia ma dobierać i usuwać poprzednie zaznaczenie.");
        }

        static void VerifyInformationMenu(PasjansApp app)
        {
            string before = JsonUtility.ToJson(app.Game.State);
            var about = Field<Button>(app, "aboutButton");
            var privacy = Field<Button>(app, "privacyButton");
            var quit = Field<Button>(app, "quitButton");
            Check(about.GetComponentInChildren<Text>().text == "O grze", "Nie zmieniono nazwy O grze.");
            var label = privacy.GetComponentInChildren<Text>();
            Check(label.text == "Polityka prywatności" && label.preferredWidth <= label.rectTransform.rect.width,
                "Napis polityki prywatności nie mieści się w jednym wierszu menu.");
            Check(((RectTransform)about.transform).anchoredPosition.y > ((RectTransform)privacy.transform).anchoredPosition.y &&
                ((RectTransform)privacy.transform).anchoredPosition.y > ((RectTransform)quit.transform).anchoredPosition.y,
                "Polityka prywatności musi być między O grze i Zakończ.");
            about.onClick.Invoke();
            Check(Field<Text>(app, "modalTitle").text == "O grze" &&
                Field<Text>(app, "modalText").text == "Autorem gry jest Paweł Kaczmarczyk", "Niepoprawne okno O grze.");
            Field<Button>(app, "modalClose").onClick.Invoke();
            app.SendMessage("ToggleMenu");
            string openedUrl = null;
            app.OpenUrlForTests = url => openedUrl = url;
            try { privacy.onClick.Invoke(); }
            finally { app.OpenUrlForTests = null; }
            Check(openedUrl == "https://pawelkaczmarczyk.pl/polityka-prywatnosci/", "Otwierany jest inny adres polityki prywatności.");
            Check(!Field<RectTransform>(app, "menuPopup").gameObject.activeSelf && !Field<RectTransform>(app, "modal").gameObject.activeSelf,
                "Po otwarciu polityki menu powinno wrócić do gry.");
            Check(before == JsonUtility.ToJson(app.Game.State), "Pozycje informacyjne zmieniły rozdanie.");
            app.SendMessage("ToggleMenu");
            app.ShowGameModes();
        }

        static void VerifyTableauWinDialog(PasjansApp app, int decks, bool lateWin = false)
        {
            var state = new GameState
            {
                deckCount = decks, selectedDeckCount = decks,
                moveCount = 98765, finalMoves = 98765, completedUtcTicks = DateTime.UtcNow.Ticks,
                drawCount = lateWin ? 1 : 3, stockRecycleCount = lateWin ? 3 : 0,
                grandpaOutcome = lateWin ? GameState.GrandpaLoss : GameState.GrandpaPending,
                tableau = GameState.CreatePiles(5 * decks), foundations = GameState.CreatePiles(4 * decks)
            };
            for (int deck = 0; deck < decks; deck++)
                for (int column = 0; column < 4; column++)
                    for (int rank = 13; rank >= 1; rank--)
                        state.tableau[deck * 4 + column].cards.Add(
                            new CardData((Suit)(rank % 2 == 1 ? column : column ^ 1), rank, true, deck));
            app.SendMessage("CancelSelection");
            typeof(PasjansApp).GetField("wonShown", InstanceFlags).SetValue(app, false);
            Check(app.Game.TryLoad(state, out string error), error);
            app.SendMessage("Layout");
            app.SendMessage("Refresh");
            Check(Field<RectTransform>(app, "modal").gameObject.activeSelf && Field<Text>(app, "modalTitle").text == "Brawo!",
                "Brak okna Brawo po ułożeniu pełnych kolumn król–as.");
            Check(Field<Text>(app, "modalText").text == "Wszystkie karty trafiły na swoje miejsce.",
                "Okno Brawo nie może wyświetlać liczby ruchów.");
            VerifyDrawLabel(app);
            VerifyNoScore(app);
            Field<Button>(app, "modalClose").onClick.Invoke();
            Check(!Field<RectTransform>(app, "modal").gameObject.activeSelf, "Zamknięte okno Brawo otworzyło się ponownie.");
            VerifyGrandpaBanner(app, !lateWin);
        }

        static void VerifyDrawPhases(PasjansApp app, int decks)
        {
            app.SendMessage("CancelSelection");
            app.Game.NewGame(seed: 317, deckCount: decks);
            app.SendMessage("Layout");
            app.SendMessage("Refresh");
            for (int draw = 3; draw >= 1; draw--)
            {
                Check(Field<Text>(app, "drawLabel").text == "Dobieranie po " + draw,
                    "Stopka nie pokazuje aktualnego dobierania.");
                Check(app.Game.TryLoad(app.Game.ExportState(), out string error), error);
                app.SendMessage("Refresh");
                VerifyDrawLabel(app);
                while (app.Game.State.stock.Count > 0) Check(app.Game.TryDraw(), "Nie udało się wyczerpać talii testowej.");
                app.SendMessage("Draw");
                VerifyDrawLabel(app);
                VerifyNoScore(app);
            }
            Check(app.Game.IsGrandpaLoss, "Czwarte przejście powinno rozstrzygnąć przegraną Dziadka.");
            VerifyGrandpaBanner(app, false);
            app.SendMessage("ForceNewGame");
            Check(Field<Text>(app, "drawLabel").text == "Dobieranie po 3", "Nowa gra nie przywróciła napisu dobierania.");

            var completed = new GameState
            {
                deckCount = decks, selectedDeckCount = decks,
                moveCount = 923, finalMoves = 923, completedUtcTicks = DateTime.UtcNow.Ticks,
                tableau = GameState.CreatePiles(5 * decks), foundations = GameState.CreatePiles(4 * decks)
            };
            for (int deck = 0; deck < decks; deck++)
                for (int suit = 0; suit < 4; suit++)
                    for (int rank = 1; rank <= 13; rank++)
                        completed.foundations[4 * deck + suit].cards.Add(new CardData((Suit)suit, rank, true, deck));
            for (int draw = 3; draw >= 1; draw--)
            {
                completed.drawCount = draw;
                completed.stockRecycleCount = 3 - draw;
                Check(app.Game.TryLoad(completed, out string error), error);
                typeof(PasjansApp).GetField("wonShown", InstanceFlags).SetValue(app, false);
                app.SendMessage("Refresh");
                VerifyGrandpaBanner(app, true);
                Check(Field<Text>(app, "modalText").text == "Wszystkie karty trafiły na swoje miejsce.",
                    "Okno Brawo pokazuje wynik liczbowy.");
                Field<Button>(app, "modalClose").onClick.Invoke();
            }

            // Ostatni legalny ruch zmienia czerwoną belkę w żółtą, także po wznowieniu.
            completed.drawCount = 1;
            completed.stockRecycleCount = 3;
            completed.grandpaOutcome = GameState.GrandpaLoss;
            var lastPile = completed.foundations[completed.foundations.Length - 1].cards;
            var lastCard = lastPile[lastPile.Count - 1];
            lastPile.RemoveAt(lastPile.Count - 1);
            completed.tableau[0].cards.Add(lastCard);
            Check(app.Game.TryLoad(completed, out string loadError), loadError);
            typeof(PasjansApp).GetField("wonShown", InstanceFlags).SetValue(app, false);
            app.SendMessage("Layout");
            app.SendMessage("Refresh");
            Check(!app.Game.IsWon, "Niepełny układ został uznany za wygraną.");
            VerifyGrandpaBanner(app, false);
            app.CardClicked(Field<CardView[]>(app, "cards")[lastCard.id], 2);
            Check(app.Game.IsWon && app.Game.IsGrandpaLoss, "Ukończenie gry zmieniło ocenę Dziadka.");
            Check(Field<RectTransform>(app, "modal").gameObject.activeSelf && Field<Text>(app, "modalTitle").text == "Brawo!",
                "Brak okna Brawo po ukończeniu gry mimo przegranej Dziadka.");
            VerifyGrandpaBanner(app, false);
            Field<Button>(app, "modalClose").onClick.Invoke();
            Check(app.Game.TryLoad(JsonUtility.FromJson<GameState>(JsonUtility.ToJson(app.Game.ExportState())), out loadError), loadError);
            app.SendMessage("Refresh");
            VerifyGrandpaBanner(app, false);
            app.SendMessage("ForceNewGame");
            Check(!Field<Image>(app, "grandpaBanner").gameObject.activeSelf, "Nowa gra zachowała żółtą belkę.");
        }

        static void VerifyDrawLabel(PasjansApp app)
        {
            var label = Field<Text>(app, "drawLabel");
            bool pending = app.Game.State.grandpaOutcome == GameState.GrandpaPending;
            Check(label.gameObject.activeSelf == pending && label.text == (pending ? "Dobieranie po " + app.Game.State.drawCount : ""),
                "Napis dobierania jest niezgodny z fazą gry lub rozstrzygnięciem Dziadka.");
        }

        static void VerifyNoScore(PasjansApp app)
        {
            Check(!app.GetComponentsInChildren<Text>().Any(label => Regex.IsMatch(label.text, @"(?i)(wynik\s*:|\d+\s+ruch)")),
                "Interfejs nie może wyświetlać wyniku ani liczby ruchów.");
        }

        static T Field<T>(PasjansApp app, string name) => (T)typeof(PasjansApp).GetField(name, InstanceFlags).GetValue(app);
        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        static void CheckSelectionOnly(PasjansApp app)
        {
            var state = app.Game.ExportState();
            state.selectedDeckCount = 1;
            Check(JsonUtility.ToJson(state) == beforeModeChange, "Wybór lub anulowanie zmieniło bieżące rozdanie.");
        }

        static void VerifyBoard(PasjansApp app, int columns, int foundations)
        {
#if PASJANS_RANKING
            Check(Field<Button>(app, "rankingButton").gameObject.activeSelf, "Po włączeniu rankingu przycisk musi być widoczny.");
#else
            Check(!app.GetComponentsInChildren<Button>(true).Any(button =>
                button.GetComponentInChildren<Text>()?.text == "Ranking"), "Przycisk rankingu musi być wyłączony.");
#endif
            Check(Field<Text>(app, "versionLabel").text == PasjansApp.DisplayVersion, "Brak prawidłowej wersji na dole ekranu.");
            VerifyDrawLabel(app);
            VerifyNoScore(app);
            var targets = app.GetComponentsInChildren<PileTarget>();
            Check(targets.Count(t => t.Pile.kind == PileKind.Tableau) == columns, "Nieprawidłowa liczba kolumn.");
            Check(targets.Count(t => t.Pile.kind == PileKind.Foundation) == foundations, "Nieprawidłowa liczba baz.");
            var views = app.GetComponentsInChildren<CardView>();
            Check(views.Select(v => v.Card.id).Distinct().Count() == views.Length, "Powielony widok karty.");
            Check(views.Count(v => v.Pile.kind == PileKind.Waste) == Math.Min(3, app.Game.State.waste.Count), "Nieprawidłowa liczba odkrytych kart.");
            var corners = new Vector3[4];
            foreach (var view in views)
            {
                view.Rect.GetWorldCorners(corners);
                Check(corners.All(p => p.x >= 0 && p.x <= Screen.width + 1 && p.y >= 0 && p.y <= Screen.height + 1), "Karta wychodzi poza ekran.");
            }
        }

#if PASJANS_RANKING
        static void PopulateSampleRanking()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--pasjans-save-dir");
            var store = new Pasjans.Persistence.RankingStore(args[index + 1]);
            for (int i = 0; i < 10; i++)
            {
                bool win = i % 2 == 0;
                var state = SampleGrandpaState(72 + i * 9, win);
                state.completedUtcTicks = DateTime.UtcNow.AddHours(-i).Ticks;
                if (!store.TryRecord(state, out string error)) throw new IOException(error);
            }
        }
#endif

        static Pasjans.Core.GameState SampleGrandpaState(int moves, bool win)
        {
            var state = new Pasjans.Core.GameState
            {
                moveCount = moves, finalMoves = moves, completedUtcTicks = DateTime.UtcNow.Ticks,
                // Zielony przykład kończy się już podczas pierwszego przejścia po 3 karty.
                drawCount = 3, stockRecycleCount = 0,
                grandpaOutcome = win ? Pasjans.Core.GameState.GrandpaWin : Pasjans.Core.GameState.GrandpaLoss
            };
            for (int s = 0; s < 4; s++) for (int r = 1; r <= 13; r++)
            {
                var card = new Pasjans.Core.CardData((Pasjans.Core.Suit)s, r, win);
                if (win) state.foundations[s].cards.Add(card); else state.stock.Add(card);
            }
            return state;
        }

        static bool Ready(int width, int height)
        {
            return Screen.width == width && Screen.height == height &&
                   Time.frameCount - SessionState.GetInt(Prefix + "StartFrame", 0) >= 8 &&
                   EditorApplication.timeSinceStartup - SessionState.GetFloat(Prefix + "StartTime", 0) >= .75;
        }

        static void Capture(string filename, int nextStage)
        {
            Canvas.ForceUpdateCanvases();
            ScreenCapture.CaptureScreenshot(Path.Combine(SessionState.GetString(Prefix + "Output", ""), filename));
            SetStage(nextStage);
        }

        static bool CaptureComplete(string filename, int width, int height)
        {
            string path = Path.Combine(SessionState.GetString(Prefix + "Output", ""), filename);
            // Niektóre tryby wsadowe pomijają zrzut na końcu klatki.
            // Wtedy odczytaj teksturę już wyrenderowanego widoku Game.
            if (!File.Exists(path) && EditorApplication.timeSinceStartup - SessionState.GetFloat(Prefix + "StartTime", 0) > 3)
                ReadGameViewTexture(path, width, height);
            if (!File.Exists(path)) return false;
            byte[] bytes;
            try { bytes = File.ReadAllBytes(path); }
            catch (IOException) { return false; }
            if (bytes.Length < 36) return false;
            // Poczekaj na znacznik IEND, a nie tylko nagłówek zapisywanego pliku.
            int tail = bytes.Length - 8;
            if (bytes[tail] != 73 || bytes[tail + 1] != 69 || bytes[tail + 2] != 78 || bytes[tail + 3] != 68)
                return false;
            if (bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71)
                throw new InvalidDataException("Screenshot is not a PNG: " + path);
            int actualWidth = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
            int actualHeight = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
            if (actualWidth != width || actualHeight != height)
                throw new InvalidDataException("Unexpected screenshot dimensions: " + actualWidth + "x" + actualHeight);
            Debug.Log("Verified smoke screenshot: " + path + " (" + width + "x" + height + ")");
            return true;
        }

        static void ReadGameViewTexture(string path, int width, int height)
        {
            var textureField = GetGameView().GetType().GetField("m_RenderTexture", InstanceFlags);
            var rendered = textureField == null ? null : textureField.GetValue(gameView) as RenderTexture;
            if (rendered == null || rendered.width != width || rendered.height != height) return;
            RenderTexture previous = RenderTexture.active;
            var screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = rendered;
                screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                screenshot.Apply(false);
                File.WriteAllBytes(path, screenshot.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(screenshot);
            }
        }

        static MonoBehaviour FindApp()
        {
            foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (behaviour.GetType().FullName == "Pasjans.UI.PasjansApp") return behaviour;
            return null;
        }

        static EditorWindow GetGameView()
        {
            if (gameView == null)
                gameView = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView", true));
            return gameView;
        }

        static void SetGameViewSize(int width, int height)
        {
            var assembly = typeof(EditorWindow).Assembly;
            Type sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
            Type sizeType = assembly.GetType("UnityEditor.GameViewSize", true);
            Type sizeKindType = assembly.GetType("UnityEditor.GameViewSizeType", true);
            Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singletonType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            object group = sizesType.GetProperty("currentGroup", InstanceFlags).GetValue(sizes);
            Type groupType = group.GetType();
            object fixedResolution = Enum.Parse(sizeKindType, "FixedResolution");
            object customSize = Activator.CreateInstance(sizeType, InstanceFlags, null,
                new object[] { fixedResolution, width, height, "Pasjans QA " + width + "x" + height }, null);
            groupType.GetMethod("AddCustomSize", InstanceFlags).Invoke(group, new[] { customSize });
            int total = (int)groupType.GetMethod("GetTotalCount", InstanceFlags).Invoke(group, null);
            EditorWindow view = GetGameView();
            view.GetType().GetMethod("SizeSelectionCallback", InstanceFlags).Invoke(view, new object[] { total - 1, null });
            view.Focus();
            view.Repaint();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        static void Finish(bool success, string message)
        {
            if (smokeMouse != null) InputSystem.RemoveDevice(smokeMouse);
            if (previousMice != null)
                foreach (var mouse in previousMice) if (mouse.added) InputSystem.EnableDevice(mouse);
            smokeMouse = null;
            previousMice = null;
            SessionState.SetInt(Prefix + "Stage", 0);
            if (success) Debug.Log("Pasjans smoke capture PASSED: " + message);
            else Debug.LogError("Pasjans smoke capture FAILED: " + message);
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }
    }
}
