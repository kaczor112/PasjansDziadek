using System;
using System.IO;
using System.Reflection;
using System.Linq;
using Pasjans.Core;
using Pasjans.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
                        FindApp().SendMessage("ShowRanking", SendMessageOptions.RequireReceiver);
                        SetStage(9);
                        break;
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

        // Scenariusz sprawdza oba komunikaty Dziadka i cztery kolumny rankingu.
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
                    PopulateSampleRanking();
                    app.SendMessage("ShowRanking", SendMessageOptions.RequireReceiver);
                    SetGameViewSize(720, 1280);
                    SetStage(6);
                    break;
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
            }
        }

        static void LoadGrandpaState(PasjansApp app, bool win, int moves)
        {
            var state = SampleRankingState(moves, win);
            Check(app.Game.TryLoad(state, out string error), error);
            app.SendMessage("Layout", SendMessageOptions.RequireReceiver);
            app.SendMessage("Refresh", SendMessageOptions.RequireReceiver);
        }

        static void VerifyGrandpaBanner(PasjansApp app, bool win)
        {
            var banner = Field<Image>(app, "grandpaBanner");
            var label = Field<Text>(app, "grandpaBannerText");
            Check(banner.gameObject.activeSelf, "Komunikat Dziadka jest ukryty.");
            Check(label.text == (win ? "Wg Dziadka wygrałeś" : "Wg Dziadka przegrałeś"), "Niepoprawny tekst komunikatu.");
            Check(win ? banner.color.g > banner.color.r : banner.color.r > banner.color.g, "Niepoprawny kolor komunikatu.");
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
                    Mouse.current?.WarpCursorPosition(screenPoint);
                    ExecuteEvents.Execute(row.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                    Check(Field<RectTransform>(app, "modePopup").gameObject.activeSelf, "Najechanie musi otworzyć podmenu.");
                    SetStage(4);
                    break;
                case 4:
                    if (!Ready(1280, 800)) return;
                    Check(Field<RectTransform>(app, "modePopup").gameObject.activeSelf, "Podmenu znika pod kursorem.");
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
                    Capture("one-deck-portrait.png", 13);
                    break;
                case 13:
                    if (!CaptureComplete("one-deck-portrait.png", 720, 1280)) return;
                    Finish(true, "Zweryfikowano podmenu, przycisk obrotu, wybór, anulowanie, dwa rewersy oraz oba układy planszy.");
                    break;
            }
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

        static void PopulateSampleRanking()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--pasjans-save-dir");
            var store = new Pasjans.Persistence.RankingStore(args[index + 1]);
            for (int i = 0; i < 10; i++)
            {
                bool win = i % 2 == 0;
                var state = SampleRankingState(72 + i * 9, win);
                state.completedUtcTicks = DateTime.UtcNow.AddHours(-i).Ticks;
                if (!store.TryRecord(state, out string error)) throw new IOException(error);
            }
        }

        static Pasjans.Core.GameState SampleRankingState(int moves, bool win)
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
            SessionState.SetInt(Prefix + "Stage", 0);
            if (success) Debug.Log("Pasjans smoke capture PASSED: " + message);
            else Debug.LogError("Pasjans smoke capture FAILED: " + message);
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }
    }
}
