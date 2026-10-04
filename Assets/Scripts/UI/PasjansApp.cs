using System;
using System.Collections.Generic;
using Pasjans.Core;
using Pasjans.Persistence;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Pasjans.UI
{
    public sealed class PasjansApp : MonoBehaviour
    {
        // Schemat: vYYYY.MM.DD_CC_vKK_HASH. Data oznacza dzień zmiany, CC liczbę
        // wypchniętych commitów, a KK kolejną zmianę kodu (od 01 po zmianie CC).
        // HASH jest skrótem ostatniego wypchniętego commita wskazanym dla wydania.
        public const string DisplayVersion = "v2026.10.04_05_v01_7652287";
        public static readonly Color Ink = new Color32(10, 17, 20, 255);
        public static readonly Color Red = new Color32(205, 22, 40, 255);
        static readonly Color Cream = new Color32(245, 238, 214, 255);
        static readonly Color Muted = new Color32(161, 190, 174, 255);
        static readonly Color Gold = new Color32(224, 192, 119, 255);
        public GameArt Art { get; private set; }
        public KlondikeGame Game => game;
        public bool UsesTouchMenu
        {
            get
            {
#if UNITY_EDITOR
                if (SimulateTouchMenu) return true;
#endif
                return Application.isMobilePlatform;
            }
        }
        bool ShowsRotationButton
        {
            get
            {
#if UNITY_EDITOR
                if (SimulateTouchMenu) return true;
#endif
                return Application.platform == RuntimePlatform.Android;
            }
        }
#if UNITY_EDITOR
        // Pozwala sprawdzić dotykowe menu bez zmiany rzeczywistego zapisu gracza.
        public bool SimulateTouchMenu { get; set; }
#endif
        KlondikeGame game;
        GameSaveStore store;
        RankingStore rankingStore;
        Font font;
        CanvasScaler scaler;
        RectTransform root, board, modal, modalPanel;
        Text title, subtitle, stockLabel, wasteLabel, foundationLabel, grandpaBannerText, status, versionLabel;
        Text modalTitle, modalText;
        Button menuButton, rotateButton, newButton, rankingButton, aboutButton, quitButton, modalClose, modalAccept, modalCancel;
        RectTransform menuPopup, menuBackdrop, modeItem, modePopup;
        Button oneDeckButton, twoDeckButton;
        Image grandpaBanner;
        RectTransform rankingTable;
        readonly Text[,] rankingCells = new Text[11, 4];
        readonly CardView[] cards = new CardView[104];
        readonly List<Target> targets = new List<Target>(20);
        readonly List<CardView> dragged = new List<CardView>(13);
        readonly List<Vector2> dragOrigins = new List<Vector2>(13);
        readonly List<RaycastResult> raycasts = new List<RaycastResult>(32);
        CardView selected;
        Vector2 dragStart;
        int dragPointerId;
        int startupLayoutFrames = 3;
        int lastWidth, lastHeight;
        Rect lastSafeArea;
        float cardWidth, cardHeight, gap, margin, stockY, tableauY;
        bool initialized, dirty, dragging, wonShown, rankingOpen, resultRecorded, menuOpen;
        string notice, startupError;
        Action modalAction;

        sealed class Target
        {
            public PileRef pile;
            public RectTransform rect;
            public Image image;
            public Text label;
        }

        void Awake()
        {
            Debug.Log("Pasjans: rozpoczęto inicjalizację interfejsu.");
            try
            {
                InitializeApplication();
            }
            catch (Exception exception)
            {
                // Zamiast czarnego ekranu pokaż czytelny błąd startu.
                startupError = "Nie udało się uruchomić gry.\nKod błędu: START-01";
                Debug.LogException(exception);
            }
        }

        void InitializeApplication()
        {
            Application.targetFrameRate = 30;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = true;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            if (Application.isMobilePlatform) Screen.orientation = ScreenOrientation.AutoRotation;
            Art = new GameArt();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            game = new KlondikeGame();
            string saveDirectory = Application.persistentDataPath;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
                if (arguments[i] == "--pasjans-save-dir") saveDirectory = arguments[i + 1];
#endif
            store = new GameSaveStore(saveDirectory);
            rankingStore = new RankingStore(saveDirectory);
            var loaded = store.Load();
            if (loaded.Success && game.TryLoad(loaded.State, out _))
                notice = loaded.Status == SaveLoadStatus.RecoveredBackup ? "Przywrócono grę z kopii zapasowej." : "Wznowiono Twoją grę.";
            else
            {
                dirty = true;
                notice = loaded.Status == SaveLoadStatus.Missing ? "Dobierz kartę z talii, aby rozpocząć." : "Zapis jest niedostępny. Rozdano nowe karty.";
            }
            if (!loaded.CanSave) notice = loaded.Message;
            if (loaded.Success && string.IsNullOrEmpty(loaded.State.gameId)) dirty = true;
            BuildInterface();
            initialized = true;
            Layout();
            Refresh();
            if (dirty) Persist();
            RecordResult();
            Debug.Log("Pasjans: interfejs jest gotowy.");
        }

        void BuildInterface()
        {
            var canvasObject = new GameObject("Pasjans Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            var background = new GameObject("Felt", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            background.transform.SetParent(canvasObject.transform, false);
            Stretch(background.rectTransform);
            background.texture = Art.Felt;
            background.uvRect = new Rect(0, 0, 10, 10);
            background.raycastTarget = false;
            root = RectObject("Safe area", canvasObject.transform);
            board = RectObject("Board", root);
            Stretch(board);
            title = MakeText(root, "Title", "PASJANS", 37, TextAnchor.UpperLeft, Cream);
            title.fontStyle = FontStyle.Bold;
            subtitle = MakeText(root, "Subtitle", "Wersja od Dziadka", 19, TextAnchor.UpperLeft, Muted);
            bool mobileMenu = UsesTouchMenu;
            menuButton = MakeButton(root, mobileMenu ? "⋮" : "Plik", ToggleMenu, true);
            rotateButton = MakeIconButton(root, "Obróć ekran", Art.RotateScreen, RotateScreen);
            var menuShade = MakeImage(root, "Menu backdrop", null, Color.clear);
            menuShade.raycastTarget = true;
            menuBackdrop = menuShade.rectTransform;
            Stretch(menuBackdrop);
            menuShade.gameObject.AddComponent<Button>().onClick.AddListener(CloseMenu);
            menuBackdrop.gameObject.SetActive(false);
            var menuImage = MakeImage(root, "Menu popup", Art.Panel, new Color32(245, 238, 214, 255));
            menuImage.type = Image.Type.Sliced;
            menuImage.raycastTarget = true;
            menuPopup = menuImage.rectTransform;
            newButton = MakeButton(menuPopup, "Nowa gra", () => { CloseMenu(); AskNewGame(); }, true);
            // Wiersz trybów nie jest przyciskiem; Windows otwiera go po najechaniu.
            var modeImage = MakeImage(menuPopup, "Tryby gry", Art.Panel, new Color32(44, 94, 78, 255));
            modeImage.type = Image.Type.Sliced;
            modeImage.raycastTarget = true;
            modeItem = modeImage.rectTransform;
            modeImage.gameObject.AddComponent<GameModeMenu>().App = this;
            var modeLabel = MakeText(modeItem, "Label", "Tryby gry →", 24, TextAnchor.MiddleCenter, Cream);
            modeLabel.fontStyle = FontStyle.Bold;
            Stretch(modeLabel.rectTransform);
            var subImage = MakeImage(modeItem, "Tryby gry submenu", Art.Panel, Cream);
            subImage.type = Image.Type.Sliced;
            subImage.raycastTarget = true;
            modePopup = subImage.rectTransform;
            oneDeckButton = MakeButton(modePopup, "Jedna talia", () => SelectGameMode(1));
            twoDeckButton = MakeButton(modePopup, "Dwie talie", () => SelectGameMode(2));
            UpdateModeLabels();
            modePopup.gameObject.SetActive(false);
            rankingButton = MakeButton(menuPopup, "Ranking", () => { CloseMenu(); ShowRanking(); });
            // Ranking nadal zapisuje wyniki, ale jego pozycja w menu jest czasowo ukryta.
            rankingButton.gameObject.SetActive(false);
            aboutButton = MakeButton(menuPopup, "O mnie", () => { CloseMenu(); ShowAbout(); });
            quitButton = MakeButton(menuPopup, "Zakończ", () => { CloseMenu(); QuitGame(); });
            menuPopup.gameObject.SetActive(false);
            stockLabel = MakeText(root, "Stock label", "TALIA", 17, TextAnchor.MiddleLeft, Muted);
            wasteLabel = MakeText(root, "Waste label", "ODKRYTE", 17, TextAnchor.MiddleLeft, Muted);
            foundationLabel = MakeText(root, "Foundation label", "BAZY · AS → KRÓL", 17, TextAnchor.MiddleLeft, Muted);
            grandpaBanner = MakeImage(root, "Wynik Dziadka", Art.Panel, Color.white);
            grandpaBanner.type = Image.Type.Sliced;
            grandpaBannerText = MakeText(grandpaBanner.transform, "Wynik Dziadka tekst", "", 18, TextAnchor.MiddleCenter, Cream);
            grandpaBannerText.fontStyle = FontStyle.Bold;
            Stretch(grandpaBannerText.rectTransform);
            grandpaBanner.gameObject.SetActive(false);
            status = MakeText(root, "Status", "", 18, TextAnchor.MiddleLeft, Cream);
            versionLabel = MakeText(root, "Version", DisplayVersion, 17, TextAnchor.MiddleRight, Muted);
            AddTarget(new PileRef(PileKind.Stock), "↻");
            AddTarget(new PileRef(PileKind.Waste), "");
            for (int i = 0; i < 8; i++) AddTarget(new PileRef(PileKind.Foundation, i), "A");
            for (int i = 0; i < 10; i++) AddTarget(new PileRef(PileKind.Tableau, i), "");
            for (int i = 0; i < cards.Length; i++)
            {
                var rect = RectObject("Card " + i, board);
                cards[i] = rect.gameObject.AddComponent<CardView>();
                cards[i].Initialize(this);
            }
            if (EventSystem.current == null)
            {
                var events = new GameObject("Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                events.GetComponent<EventSystem>().pixelDragThreshold = Mathf.Max(10, Mathf.RoundToInt(Screen.dpi * .07f));
            }
            BuildModal();
        }

        void AddTarget(PileRef pile, string symbol)
        {
            var image = MakeImage(board, pile.kind + " " + pile.index, Art.Panel, new Color(0, .1f, .08f, .24f));
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            var handler = image.gameObject.AddComponent<PileTarget>();
            handler.App = this; handler.Pile = pile;
            var label = MakeText(image.transform, "Placeholder", symbol, 36, TextAnchor.MiddleCenter, new Color(.7f, .8f, .65f, .38f));
            targets.Add(new Target { pile = pile, rect = image.rectTransform, image = image, label = label });
        }

        void BuildModal()
        {
            var shade = MakeImage(root, "Dialog backdrop", null, new Color(0, .04f, .03f, .8f));
            shade.raycastTarget = true;
            modal = shade.rectTransform;
            Stretch(modal);
            modalPanel = MakeImage(modal, "Dialog", Art.Panel, Cream).rectTransform;
            modalPanel.GetComponent<Image>().type = Image.Type.Sliced;
            modalPanel.GetComponent<Image>().raycastTarget = true;
            modalTitle = MakeText(modalPanel, "Dialog title", "", 32, TextAnchor.UpperLeft, Ink);
            modalTitle.fontStyle = FontStyle.Bold;
            modalText = MakeText(modalPanel, "Dialog text", "", 25, TextAnchor.MiddleLeft, Ink);
            modalClose = MakeButton(modalPanel, "X", CloseModal);
            modalAccept = MakeButton(modalPanel, "Rozdaj karty", () => { var action = modalAction; CloseModal(); action?.Invoke(); }, true);
            modalCancel = MakeButton(modalPanel, "Anuluj", CloseModal);
            rankingTable = RectObject("Ranking table", modalPanel);
            for (int row = 0; row <= 10; row++)
                for (int col = 0; col < 4; col++)
                {
                    rankingCells[row, col] = MakeText(rankingTable, "Cell " + row + "," + col, "", row == 0 ? 18 : 22,
                        col == 1 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, Ink);
                    if (row == 0) rankingCells[row, col].fontStyle = FontStyle.Bold;
                }
            rankingTable.gameObject.SetActive(false);
            modal.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!initialized) return;
            if (menuOpen && modePopup.gameObject.activeSelf && !UsesTouchMenu && Mouse.current != null)
            {
                Vector2 point = Mouse.current.position.ReadValue();
                if (!RectTransformUtility.RectangleContainsScreenPoint(modeItem, point) &&
                    !RectTransformUtility.RectangleContainsScreenPoint(modePopup, point))
                    modePopup.gameObject.SetActive(false);
            }
            // Android może podać końcowy rozmiar powierzchni dopiero po pierwszej klatce.
            if (startupLayoutFrames > 0 || Screen.width != lastWidth || Screen.height != lastHeight || Screen.safeArea != lastSafeArea)
            {
                if (startupLayoutFrames > 0) startupLayoutFrames--;
                CancelSelection();
                Layout();
                Refresh();
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (modal.gameObject.activeSelf) CloseModal();
                else if (menuOpen) CloseMenu();
                else if (selected != null) { CancelSelection(); Refresh(); }
                else QuitGame();
            }
        }

        void Layout()
        {
            // Poczekaj, aż Android poda prawidłowy rozmiar powierzchni.
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                startupLayoutFrames = Mathf.Max(startupLayoutFrames, 1);
                return;
            }
            lastWidth = Screen.width; lastHeight = Screen.height; lastSafeArea = Screen.safeArea;
            bool portrait = Screen.height > Screen.width;
            float virtualWidth = portrait ? 800 : Mathf.Max(1100, Screen.width / (float)Mathf.Max(Screen.height, 1) * 800);
            scaler.scaleFactor = Screen.width / virtualWidth;
            var safe = Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
            root.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            root.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            root.offsetMin = root.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            float w = root.rect.width, h = root.rect.height;
            margin = portrait ? 20 : 32;
            gap = portrait ? 10 : 18;
            int gridColumns = Mathf.Max(game.State.tableau.Length, game.State.foundations.Length + 3);
            cardWidth = Mathf.Min(146, (w - margin * 2 - gap * (gridColumns - 1)) / gridColumns);
            bool compact = w < 1150;
            stockY = compact ? 226 : 153;
            // Dopasuj odstępy do najdłuższej kolumny, także po wykonaniu ruchu.
            float longestFan = 0;
            foreach (var pile in game.State.tableau)
            {
                float fan = 0;
                for (int i = 0; i < pile.cards.Count - 1; i++) fan += pile.cards[i].faceUp ? .36f : .17f;
                longestFan = Mathf.Max(longestFan, fan);
            }
            cardWidth = Mathf.Min(cardWidth, (h - stockY - (portrait ? 70 : 58) - 80) / (2.84f + longestFan));
            cardHeight = cardWidth * 1.42f;
            margin = (w - cardWidth * gridColumns - gap * (gridColumns - 1)) / 2;
            bool mobileMenu = UsesTouchMenu;
            menuButton.GetComponentInChildren<Text>().text = mobileMenu ? "⋮" : "Plik";
            float menuMargin = portrait ? 20 : 32;
            float menuButtonWidth = mobileMenu ? 70 : 120;
            float menuButtonX = mobileMenu ? w - menuMargin - menuButtonWidth : menuMargin;
            const float rotateButtonSize = 57;
            rotateButton.gameObject.SetActive(ShowsRotationButton);
            Place(title.rectTransform, mobileMenu ? menuMargin : menuMargin + menuButtonWidth + 20, 21, 340, 48);
            Place(subtitle.rectTransform, mobileMenu ? menuMargin + 2 : menuMargin + menuButtonWidth + 22, 67, 350, 28);
            Place((RectTransform)menuButton.transform, menuButtonX, 21, menuButtonWidth, 57);
            Place((RectTransform)rotateButton.transform, menuButtonX - rotateButtonSize - 12, 21, rotateButtonSize, rotateButtonSize);
            float popupWidth = 270;
            float popupX = mobileMenu ? w - menuMargin - popupWidth : menuMargin;
            Place(menuPopup, popupX, 86, popupWidth, 4 * 57 + 24);
            Place((RectTransform)newButton.transform, 12, 12, popupWidth - 24, 57);
            Place(modeItem, 12, 69, popupWidth - 24, 57);
            // Podmenu rozwija się w stronę, po której jest wolne miejsce.
            const float subWidth = 258;
            bool openLeft = popupX + popupWidth + subWidth > w;
            Place(modePopup, openLeft ? -subWidth : popupWidth - 24, 0, subWidth, 138);
            Place((RectTransform)oneDeckButton.transform, 12, 12, subWidth - 24, 57);
            Place((RectTransform)twoDeckButton.transform, 12, 69, subWidth - 24, 57);
            Place((RectTransform)aboutButton.transform, 12, 126, popupWidth - 24, 57);
            Place((RectTransform)quitButton.transform, 12, 183, popupWidth - 24, 57);
            stockY = compact ? 226 : 153;
            tableauY = stockY + cardHeight + (portrait ? 70 : 58);
            Place(stockLabel.rectTransform, X(0), stockY - 35, cardWidth + gap, 28);
            stockLabel.fontSize = Mathf.Clamp(Mathf.RoundToInt(cardWidth * .17f), 11, 17);
            Place(wasteLabel.rectTransform, X(1), stockY - 35, cardWidth * 1.8f, 28);
            float foundationsWidth = cardWidth * game.State.foundations.Length + gap * (game.State.foundations.Length - 1);
            Place(foundationLabel.rectTransform, X(3), stockY - 35, foundationsWidth, 28);
            Place(grandpaBanner.rectTransform, X(3), stockY - 75, foundationsWidth, 32);
            grandpaBannerText.fontSize = Mathf.Clamp(Mathf.RoundToInt(cardWidth * .18f), 12, 18);
            foreach (var target in targets)
            {
                bool active = target.pile.kind == PileKind.Foundation ? target.pile.index < game.State.foundations.Length :
                    target.pile.kind != PileKind.Tableau || target.pile.index < game.State.tableau.Length;
                target.rect.gameObject.SetActive(active);
                if (!active) continue;
                int col = target.pile.kind == PileKind.Stock ? 0 : target.pile.kind == PileKind.Waste ? 1 : target.pile.kind == PileKind.Foundation ? 3 + target.pile.index : target.pile.index;
                float y = target.pile.kind == PileKind.Tableau ? tableauY : stockY;
                float x = target.pile.kind == PileKind.Tableau ? TableauX(col) : X(col);
                Place(target.rect, x, y, cardWidth, target.pile.kind == PileKind.Tableau ? Mathf.Max(cardHeight, h - y - 72) : cardHeight);
                // Rysowany jest obszar wielkości karty, ale cała kolumna pozostaje celem ruchu.
                target.image.color = target.pile.kind == PileKind.Tableau ? new Color(0, .1f, .08f, .10f) : new Color(0, .1f, .08f, .24f);
                Place(target.label.rectTransform, 0, 0, cardWidth, cardHeight);
            }
            status.fontSize = portrait ? 21 : 18;
            Place(status.rectTransform, margin, h - 65, w - margin * 2 - (portrait ? 0 : 210), 31);
            Place(versionLabel.rectTransform, margin, h - (portrait ? 33 : 65), w - margin * 2, 28);
            float mw = Mathf.Min(640, w - 44), mh = rankingOpen ? 650 : 350;
            Place(modalPanel, (w - mw) / 2, (h - mh) / 2, mw, mh);
            Place(modalTitle.rectTransform, 30, 28, mw - 116, 50);
            Place((RectTransform)modalClose.transform, mw - 77, 18, 58, 54);
            Place(modalText.rectTransform, 30, 93, mw - 60, 142);
            Place((RectTransform)modalAccept.transform, 30, mh - 86, (mw - 76) / 2, 57);
            Place((RectTransform)modalCancel.transform, mw / 2 + 8, mh - 86, (mw - 76) / 2, 57);
            Place(rankingTable, 30, 92, mw - 60, 506);
            float tableWidth = mw - 60;
            for (int row = 0; row <= 10; row++)
            {
                float rowY = row == 0 ? 0 : 56 + (row - 1) * 44;
                float rowHeight = row == 0 ? 54 : 42;
                Place(rankingCells[row, 0].rectTransform, 0, rowY, 42, rowHeight);
                Place(rankingCells[row, 1].rectTransform, 48, rowY, 202, rowHeight);
                Place(rankingCells[row, 2].rectTransform, 256, rowY, 126, rowHeight);
                Place(rankingCells[row, 3].rectTransform, 388, rowY, tableWidth - 388, rowHeight);
            }
        }

        float X(int col) => margin + col * (cardWidth + gap);
        float TableauX(int col) => (root.rect.width - game.State.tableau.Length * cardWidth - (game.State.tableau.Length - 1) * gap) / 2 + col * (cardWidth + gap);

        void Refresh()
        {
            if (!initialized) return;
            for (int i = 0; i < cards.Length; i++) cards[i].gameObject.SetActive(false);
            var state = game.State;
            if (state.stock.Count > 0) ShowCard(state.stock[state.stock.Count - 1], new PileRef(PileKind.Stock), state.stock.Count - 1, X(0), stockY);
            int visibleWaste = Mathf.Min(3, state.waste.Count);
            for (int i = state.waste.Count - visibleWaste; i < state.waste.Count; i++)
                ShowCard(state.waste[i], new PileRef(PileKind.Waste), i, X(1) + (i - state.waste.Count + visibleWaste) * cardWidth * .27f, stockY);
            for (int f = 0; f < state.foundations.Length; f++)
            {
                var pile = state.foundations[f].cards;
                if (pile.Count > 0) ShowCard(pile[pile.Count - 1], new PileRef(PileKind.Foundation, f), pile.Count - 1, X(3 + f), stockY);
            }
            for (int t = 0; t < state.tableau.Length; t++)
            {
                var pile = state.tableau[t].cards;
                float required = 0;
                for (int i = 0; i < pile.Count - 1; i++) required += pile[i].faceUp ? cardWidth * .36f : cardWidth * .17f;
                float available = Mathf.Max(0, root.rect.height - tableauY - cardHeight - 80);
                float compression = required > 0 ? Mathf.Min(1, available / required) : 1;
                float y = tableauY;
                for (int i = 0; i < pile.Count; i++)
                {
                    ShowCard(pile[i], new PileRef(PileKind.Tableau, t), i, TableauX(t), y);
                    y += (pile[i].faceUp ? cardWidth * .36f : cardWidth * .17f) * compression;
                }
            }
            stockLabel.text = "TALIA · " + state.stock.Count;
            versionLabel.text = DisplayVersion;
            grandpaBanner.gameObject.SetActive(game.IsGrandpaWin || game.IsGrandpaLoss);
            if (game.IsGrandpaWin)
            {
                grandpaBanner.color = new Color32(34, 116, 72, 255);
                grandpaBannerText.text = "Wg Dziadka wygrałeś";
            }
            else if (game.IsGrandpaLoss)
            {
                grandpaBanner.color = new Color32(148, 38, 49, 255);
                grandpaBannerText.text = "Wg Dziadka przegrałeś";
            }
            status.text = notice ?? "Przeciągnij kartę lub dotknij karty i miejsca docelowego.";
            modal.SetAsLastSibling();
            if (game.IsWon && !wonShown)
            {
                wonShown = true;
                OpenModal("Brawo!", "Wszystkie karty trafiły na swoje miejsce.\nPasjans ułożony w " + game.MoveCount + " ruchach.", StartNewGame, "Nowa gra");
            }
        }

        void ShowCard(CardData card, PileRef pile, int index, float x, float y)
        {
            var view = cards[card.id];
            view.gameObject.SetActive(true);
            view.Bind(card, pile, index, cardWidth, cardHeight, selected == view);
            Place(view.Rect, x, y, cardWidth, cardHeight);
            view.transform.SetAsLastSibling();
        }

        bool CanSelect(CardView card)
        {
            if (!card.Card.faceUp) return false;
            if (card.Pile.kind == PileKind.Tableau) return true;
            return card.Pile.kind != PileKind.Stock && card.Index == game.GetPile(card.Pile).Count - 1;
        }

        public void CardClicked(CardView card, int clickCount)
        {
            if (modal.gameObject.activeSelf || menuOpen || dragging) return;
            if (card.Pile.kind == PileKind.Stock) { Draw(); return; }
            if (selected != null && selected != card)
            {
                if (TryMoveSelected(card.Pile)) return;
                // Między różnymi kolumnami sprawdź też kolejność cel → karta przenoszona.
                if (CanSelect(card) && CanTryReverseTableauMove(selected.Pile, card.Pile) &&
                    game.TryMove(card.Pile, card.Index, selected.Pile))
                {
                    AfterMove();
                    return;
                }
            }
            if (!CanSelect(card)) return;
            if (clickCount >= 2)
            {
                selected = card;
                for (int f = 0; f < game.State.foundations.Length; f++) if (TryMoveSelected(new PileRef(PileKind.Foundation, f))) return;
            }
            selected = selected == card ? null : card;
            notice = selected == null ? null : "Wybierz kolumnę lub bazę dla zaznaczonej karty.";
            Refresh();
        }

        public static bool CanTryReverseTableauMove(PileRef first, PileRef second)
        {
            return first.kind == PileKind.Tableau && second.kind == PileKind.Tableau && first.index != second.index;
        }

        public void PileClicked(PileRef pile)
        {
            if (modal.gameObject.activeSelf || menuOpen || dragging) return;
            if (pile.kind == PileKind.Stock) { Draw(); return; }
            if (selected != null && !TryMoveSelected(pile)) { notice = "Ten ruch nie pasuje do zasad pasjansa."; Refresh(); }
        }

        void Draw()
        {
            CancelSelection();
            if (game.TryDraw()) AfterMove();
            else { notice = "Talia jest pusta. Przenieś odkryte karty."; Refresh(); }
        }

        bool TryMoveSelected(PileRef destination)
        {
            if (selected == null || !game.TryMove(selected.Pile, selected.Index, destination)) return false;
            AfterMove();
            return true;
        }

        void AfterMove()
        {
            CancelSelection();
            dirty = true;
            notice = null;
            Persist();
            RecordResult();
            if (game.HasResult && game.State.grandpaOutcome == GameState.GrandpaPending)
                notice = "Wynik: " + game.State.finalMoves + " ruchów. Opróżnij talię przed czwartym przejściem.";
            else if (game.HasResult && resultRecorded)
                notice = "Wynik: " + game.State.finalMoves + " ruchów. Możesz dokończyć układanie.";
            Layout();
            Refresh();
        }

        public bool BeginCardDrag(CardView card, PointerEventData e)
        {
            if (modal.gameObject.activeSelf || menuOpen || dragging || !CanSelect(card)) return false;
            selected = card;
            Refresh();
            dragging = true;
            dragPointerId = e.pointerId;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(board, e.position, null, out dragStart);
            var pile = game.GetPile(card.Pile);
            for (int i = card.Index; i < pile.Count; i++)
            {
                var view = cards[pile[i].id];
                dragged.Add(view);
                dragOrigins.Add(view.Rect.anchoredPosition);
                view.transform.SetAsLastSibling();
                view.GetComponent<Image>().raycastTarget = false;
            }
            return true;
        }

        public void DragCards(PointerEventData e)
        {
            if (!dragging || dragPointerId != e.pointerId) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(board, e.position, null, out Vector2 point);
            for (int i = 0; i < dragged.Count; i++) dragged[i].Rect.anchoredPosition = dragOrigins[i] + point - dragStart;
        }

        public void EndCardDrag(PointerEventData e)
        {
            if (!dragging || dragPointerId != e.pointerId) return;
            raycasts.Clear();
            EventSystem.current.RaycastAll(e, raycasts);
            PileRef? destination = null;
            foreach (var hit in raycasts)
            {
                var card = hit.gameObject.GetComponentInParent<CardView>();
                if (card != null) { destination = card.Pile; break; }
                var target = hit.gameObject.GetComponentInParent<PileTarget>();
                if (target != null) { destination = target.Pile; break; }
            }
            dragging = false;
            if (destination.HasValue && TryMoveSelected(destination.Value)) return;
            CancelSelection();
            notice = null;
            Refresh();
        }

        void CancelSelection()
        {
            foreach (var card in dragged) card.GetComponent<Image>().raycastTarget = true;
            dragged.Clear(); dragOrigins.Clear();
            dragging = false; selected = null;
        }

        void AskNewGame() => OpenModal("Nowa gra?", "Bieżące rozdanie zostanie zastąpione.\nRozdać karty w trybie: " + (game.SelectedDeckCount == 1 ? "Jedna talia" : "Dwie talie") + "?", StartNewGame, "Rozdaj karty");

        void ToggleMenu()
        {
            if (modal.gameObject.activeSelf) return;
            CancelSelection();
            Refresh();
            menuOpen = !menuOpen;
            modePopup.gameObject.SetActive(false);
            menuBackdrop.gameObject.SetActive(menuOpen);
            menuBackdrop.SetAsLastSibling();
            menuPopup.gameObject.SetActive(menuOpen);
            menuPopup.SetAsLastSibling();
        }

        public void ShowGameModes()
        {
            if (!menuOpen) return;
            modeItem.SetAsLastSibling();
            modePopup.gameObject.SetActive(true);
        }

        public void ToggleGameModes()
        {
            if (modePopup.gameObject.activeSelf) modePopup.gameObject.SetActive(false);
            else ShowGameModes();
        }

        void SelectGameMode(int decks)
        {
            if (game.SelectDeckCount(decks))
            {
                dirty = true;
                Persist();
            }
            UpdateModeLabels();
            CloseMenu();
        }

        void UpdateModeLabels()
        {
            oneDeckButton.GetComponentInChildren<Text>().text = (game.SelectedDeckCount == 1 ? "✓  " : "") + "Jedna talia";
            twoDeckButton.GetComponentInChildren<Text>().text = (game.SelectedDeckCount == 2 ? "✓  " : "") + "Dwie talie";
        }

        void CloseMenu()
        {
            menuOpen = false;
            if (modePopup != null) modePopup.gameObject.SetActive(false);
            if (menuBackdrop != null) menuBackdrop.gameObject.SetActive(false);
            if (menuPopup != null) menuPopup.gameObject.SetActive(false);
        }

        void StartNewGame()
        {
            if (game.FinalizeGrandpaLoss()) dirty = true;
            if (!RecordResult())
            {
                OpenModal("Ranking niedostępny", "Nie udało się zachować wyniku. Spróbuj ponownie po zwolnieniu miejsca lub rozpocznij nowe rozdanie mimo to.", ForceNewGame, "Nowa gra mimo to");
                return;
            }
            ForceNewGame();
        }

        void ForceNewGame()
        {
            game.NewGame();
            UpdateModeLabels();
            wonShown = false; resultRecorded = false;
            AfterMove();
        }

        void ShowAbout() => OpenModal("O mnie", "Autorem gry jest Paweł Kaczmarczyk", null, null);

        void ShowRanking()
        {
            RecordResult();
            var result = rankingStore.Load();
            OpenModal("Ranking", result.Data.entries.Count == 0 ? "brak wpisów" : "", null, null);
            rankingOpen = true;
            rankingTable.gameObject.SetActive(result.Data.entries.Count > 0);
            modalText.gameObject.SetActive(result.Data.entries.Count == 0);
            rankingCells[0, 0].text = "Lp";
            rankingCells[0, 1].text = "Data z godziną";
            rankingCells[0, 2].text = "Ilość ruchów";
            rankingCells[0, 3].text = "Wygrana wg Dziadka";
            for (int i = 0; i < 10; i++)
            {
                bool active = i < result.Data.entries.Count;
                for (int col = 0; col < 4; col++) rankingCells[i + 1, col].gameObject.SetActive(active);
                if (!active) continue;
                var entry = result.Data.entries[i];
                rankingCells[i + 1, 0].text = (i + 1).ToString();
                rankingCells[i + 1, 1].text = new DateTime(entry.completedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("dd.MM.yyyy HH:mm");
                rankingCells[i + 1, 2].text = entry.moves.ToString();
                rankingCells[i + 1, 3].text = entry.grandpaWin ? "Tak" : "Nie";
            }
            Layout();
        }

        bool RecordResult()
        {
            if (!game.HasRankableResult || resultRecorded) return true;
            resultRecorded = rankingStore.TryRecord(game.State, out string error);
            if (!resultRecorded) { notice = error; if (status != null) status.text = error; }
            return resultRecorded;
        }

        void OpenModal(string heading, string message, Action action, string acceptText)
        {
            CancelSelection();
            CloseMenu();
            rankingOpen = false;
            rankingTable.gameObject.SetActive(false);
            modalText.gameObject.SetActive(true);
            // Nie odświeżaj tutaj, bo otwarcie okna zwycięstwa trwa w Refresh.
            modalTitle.text = heading; modalText.text = message; modalAction = action;
            modalAccept.gameObject.SetActive(action != null);
            modalCancel.gameObject.SetActive(action != null);
            if (action != null) modalAccept.GetComponentInChildren<Text>().text = acceptText;
            modal.gameObject.SetActive(true);
            modal.SetAsLastSibling();
            Layout();
        }

        void CloseModal() { modal.gameObject.SetActive(false); modalAction = null; Refresh(); }

        bool Persist()
        {
            if (!dirty) return true;
            var result = store.Save(game.State);
            if (result.Success) dirty = false;
            else
            {
                notice = "Zapis nie powiódł się. " + result.Message;
                if (status != null) status.text = notice;
                Debug.LogWarning("Pasjans: " + result.Message);
            }
            return result.Success;
        }

        void QuitGame()
        {
            if (Persist() && RecordResult()) ExitApplication();
            else OpenModal("Nie udało się zapisać", "Ostatnie ruchy mogą zostać utracone. Zwolnij miejsce i spróbuj ponownie lub zakończ mimo to.", ExitApplication, "Zakończ mimo to");
        }

        void RotateScreen()
        {
            ScreenOrientation target = NextForcedOrientation(Screen.orientation, Screen.height > Screen.width);
            Screen.orientation = target;
            startupLayoutFrames = 3;
            notice = target == ScreenOrientation.Portrait || target == ScreenOrientation.PortraitUpsideDown
                ? "Wymuszono pionową orientację ekranu."
                : "Wymuszono poziomą orientację ekranu.";
            Refresh();
        }

        public static ScreenOrientation NextForcedOrientation(ScreenOrientation current, bool portrait)
        {
            switch (current)
            {
                case ScreenOrientation.Portrait: return ScreenOrientation.LandscapeLeft;
                case ScreenOrientation.LandscapeLeft: return ScreenOrientation.PortraitUpsideDown;
                case ScreenOrientation.PortraitUpsideDown: return ScreenOrientation.LandscapeRight;
                case ScreenOrientation.LandscapeRight: return ScreenOrientation.Portrait;
                default: return portrait ? ScreenOrientation.LandscapeLeft : ScreenOrientation.Portrait;
            }
        }

        static void ExitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnApplicationPause(bool paused) { if (initialized && paused) { CancelSelection(); Persist(); RecordResult(); Refresh(); } }
        void OnApplicationFocus(bool focused)
        {
            if (initialized && !focused) { CancelSelection(); Persist(); RecordResult(); Refresh(); }
        }
        void OnApplicationQuit() { if (initialized) { Persist(); RecordResult(); } }
        void OnDestroy() { Art?.Dispose(); }

        void OnGUI()
        {
            if (string.IsNullOrEmpty(startupError)) return;
            GUI.color = new Color32(24, 77, 64, 255);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Screen.width / 24, 24, 52),
                wordWrap = true
            };
            GUI.Label(new Rect(Screen.width * .1f, Screen.height * .2f, Screen.width * .8f, Screen.height * .6f), startupError, style);
        }

        public Text MakeText(Transform parent, string name, string text, int size, TextAnchor anchor, Color color)
        {
            var label = RectObject(name, parent).gameObject.AddComponent<Text>();
            label.font = font; label.text = text; label.fontSize = size; label.alignment = anchor; label.color = color;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        public Image MakeImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var image = RectObject(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false;
            return image;
        }

        Button MakeButton(Transform parent, string text, Action action, bool accent = false)
        {
            var image = MakeImage(parent, text, Art.Panel, accent ? Gold : new Color32(44, 94, 78, 255));
            image.type = Image.Type.Sliced; image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(.89f, .95f, .9f);
            colors.pressedColor = new Color(.68f, .78f, .70f);
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => action());
            var label = MakeText(image.transform, "Label", text, 24, TextAnchor.MiddleCenter, accent ? Ink : Cream);
            label.fontStyle = FontStyle.Bold;
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(5, 2);
            label.rectTransform.offsetMax = new Vector2(-5, -2);
            return button;
        }

        Button MakeIconButton(Transform parent, string name, Sprite icon, Action action)
        {
            var image = MakeImage(parent, name, Art.Panel, Gold);
            image.type = Image.Type.Sliced; image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(.89f, .95f, .9f);
            colors.pressedColor = new Color(.68f, .78f, .70f);
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => action());
            var symbol = MakeImage(image.transform, "Ikona", icon, Ink);
            symbol.preserveAspect = true;
            Stretch(symbol.rectTransform);
            symbol.rectTransform.offsetMin = new Vector2(10, 10);
            symbol.rectTransform.offsetMax = new Vector2(-10, -10);
            return button;
        }

        static RectTransform RectObject(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
        }
    }
}
