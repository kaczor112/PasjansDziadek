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
        public static readonly Color Ink = new Color32(30, 43, 48, 255);
        public static readonly Color Red = new Color32(184, 43, 55, 255);
        static readonly Color Cream = new Color32(245, 238, 214, 255);
        static readonly Color Muted = new Color32(161, 190, 174, 255);
        static readonly Color Gold = new Color32(224, 192, 119, 255);
        public GameArt Art { get; private set; }
        public KlondikeGame Game => game;
        KlondikeGame game;
        GameSaveStore store;
        RankingStore rankingStore;
        Font font;
        CanvasScaler scaler;
        RectTransform root, board, modal, modalPanel;
        Text title, subtitle, stockLabel, wasteLabel, foundationLabel, status, counter;
        Text modalTitle, modalText;
        Button menuButton, newButton, rankingButton, aboutButton, quitButton, modalClose, modalAccept, modalCancel;
        RectTransform menuPopup;
        RectTransform rankingTable;
        readonly Text[,] rankingCells = new Text[11, 3];
        readonly CardView[] cards = new CardView[52];
        readonly List<Target> targets = new List<Target>(13);
        readonly List<CardView> dragged = new List<CardView>(13);
        readonly List<Vector2> dragOrigins = new List<Vector2>(13);
        readonly List<RaycastResult> raycasts = new List<RaycastResult>(32);
        CardView selected;
        Vector2 dragStart;
        int dragPointerId;
        int lastWidth, lastHeight;
        Rect lastSafeArea;
        float cardWidth, cardHeight, gap, margin, stockY, tableauY;
        bool initialized, dirty, dragging, wonShown, rankingOpen, resultRecorded, menuOpen;
        string notice;
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
            bool mobileMenu = Application.isMobilePlatform;
            menuButton = MakeButton(root, mobileMenu ? "⋮" : "Plik", ToggleMenu, true);
            var menuImage = MakeImage(root, "Menu popup", Art.Panel, new Color32(245, 238, 214, 255));
            menuImage.type = Image.Type.Sliced;
            menuImage.raycastTarget = true;
            menuPopup = menuImage.rectTransform;
            newButton = MakeButton(menuPopup, "Nowa gra", () => { CloseMenu(); AskNewGame(); }, true);
            rankingButton = MakeButton(menuPopup, "Ranking", () => { CloseMenu(); ShowRanking(); });
            aboutButton = MakeButton(menuPopup, "O mnie", () => { CloseMenu(); ShowAbout(); });
            quitButton = MakeButton(menuPopup, "Zakończ", () => { CloseMenu(); QuitGame(); });
            menuPopup.gameObject.SetActive(false);
            stockLabel = MakeText(root, "Stock label", "TALIA", 17, TextAnchor.MiddleLeft, Muted);
            wasteLabel = MakeText(root, "Waste label", "ODKRYTE", 17, TextAnchor.MiddleLeft, Muted);
            foundationLabel = MakeText(root, "Foundation label", "BAZY · AS → KRÓL", 17, TextAnchor.MiddleLeft, Muted);
            status = MakeText(root, "Status", "", 18, TextAnchor.MiddleLeft, Cream);
            counter = MakeText(root, "Counter", "", 17, TextAnchor.MiddleRight, Muted);
            AddTarget(new PileRef(PileKind.Stock), "↻");
            AddTarget(new PileRef(PileKind.Waste), "");
            for (int i = 0; i < 4; i++) AddTarget(new PileRef(PileKind.Foundation, i), "A");
            for (int i = 0; i < 7; i++) AddTarget(new PileRef(PileKind.Tableau, i), "K");
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
                for (int col = 0; col < 3; col++)
                {
                    rankingCells[row, col] = MakeText(rankingTable, "Cell " + row + "," + col, "", row == 0 ? 21 : 24,
                        col == 2 ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, Ink);
                    if (row == 0) rankingCells[row, col].fontStyle = FontStyle.Bold;
                }
            rankingTable.gameObject.SetActive(false);
            modal.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!initialized) return;
            if (Screen.width != lastWidth || Screen.height != lastHeight || Screen.safeArea != lastSafeArea)
            {
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
            lastWidth = Screen.width; lastHeight = Screen.height; lastSafeArea = Screen.safeArea;
            bool portrait = Screen.height > Screen.width;
            float virtualWidth = portrait ? 800 : Mathf.Max(1100, Screen.width / (float)Mathf.Max(Screen.height, 1) * 800);
            scaler.scaleFactor = Screen.width / virtualWidth;
            var safe = Screen.safeArea;
            root.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            root.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            root.offsetMin = root.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            float w = root.rect.width, h = root.rect.height;
            margin = portrait ? 20 : 32;
            gap = portrait ? 10 : 18;
            cardWidth = Mathf.Min(146, (w - margin * 2 - gap * 6) / 7);
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
            margin = (w - cardWidth * 7 - gap * 6) / 2;
            bool mobileMenu = Application.isMobilePlatform;
            float menuButtonWidth = mobileMenu ? 70 : 120;
            float menuButtonX = mobileMenu ? w - margin - menuButtonWidth : margin;
            Place(title.rectTransform, mobileMenu ? margin : margin + menuButtonWidth + 20, 21, 340, 48);
            Place(subtitle.rectTransform, mobileMenu ? margin + 2 : margin + menuButtonWidth + 22, 67, 350, 28);
            Place((RectTransform)menuButton.transform, menuButtonX, 21, menuButtonWidth, 57);
            float popupWidth = mobileMenu ? 260 : 250;
            float popupX = mobileMenu ? w - margin - popupWidth : margin;
            Place(menuPopup, popupX, 86, popupWidth, 4 * 57 + 24);
            Place((RectTransform)newButton.transform, 12, 12, popupWidth - 24, 57);
            Place((RectTransform)rankingButton.transform, 12, 69, popupWidth - 24, 57);
            Place((RectTransform)aboutButton.transform, 12, 126, popupWidth - 24, 57);
            Place((RectTransform)quitButton.transform, 12, 183, popupWidth - 24, 57);
            stockY = compact ? 226 : 153;
            tableauY = stockY + cardHeight + (portrait ? 70 : 58);
            Place(stockLabel.rectTransform, X(0), stockY - 35, cardWidth * 1.1f, 28);
            Place(wasteLabel.rectTransform, X(1), stockY - 35, cardWidth * 1.8f, 28);
            Place(foundationLabel.rectTransform, X(3), stockY - 35, cardWidth * 4 + gap * 3, 28);
            foreach (var target in targets)
            {
                int col = target.pile.kind == PileKind.Stock ? 0 : target.pile.kind == PileKind.Waste ? 1 : target.pile.kind == PileKind.Foundation ? 3 + target.pile.index : target.pile.index;
                float y = target.pile.kind == PileKind.Tableau ? tableauY : stockY;
                Place(target.rect, X(col), y, cardWidth, target.pile.kind == PileKind.Tableau ? Mathf.Max(cardHeight, h - y - 72) : cardHeight);
                // Rysowany jest obszar wielkości karty, ale cała kolumna pozostaje celem ruchu.
                target.image.color = target.pile.kind == PileKind.Tableau ? new Color(0, .1f, .08f, .10f) : new Color(0, .1f, .08f, .24f);
                Place(target.label.rectTransform, 0, 0, cardWidth, cardHeight);
            }
            status.fontSize = portrait ? 21 : 18;
            Place(status.rectTransform, margin, h - 65, w - margin * 2 - (portrait ? 0 : 210), 31);
            Place(counter.rectTransform, margin, h - (portrait ? 33 : 65), w - margin * 2, 28);
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
                Place(rankingCells[row, 0].rectTransform, 0, row * 45, 54, 42);
                Place(rankingCells[row, 1].rectTransform, 67, row * 45, tableWidth - 236, 42);
                Place(rankingCells[row, 2].rectTransform, tableWidth - 163, row * 45, 163, 42);
            }
        }

        float X(int col) => margin + col * (cardWidth + gap);

        void Refresh()
        {
            if (!initialized) return;
            for (int i = 0; i < cards.Length; i++) cards[i].gameObject.SetActive(false);
            var state = game.State;
            if (state.stock.Count > 0) ShowCard(state.stock[state.stock.Count - 1], new PileRef(PileKind.Stock), state.stock.Count - 1, X(0), stockY);
            int visibleWaste = Mathf.Min(3, state.waste.Count);
            for (int i = state.waste.Count - visibleWaste; i < state.waste.Count; i++)
                ShowCard(state.waste[i], new PileRef(PileKind.Waste), i, X(1) + (i - state.waste.Count + visibleWaste) * cardWidth * .27f, stockY);
            for (int f = 0; f < 4; f++)
            {
                var pile = state.foundations[f].cards;
                if (pile.Count > 0) ShowCard(pile[pile.Count - 1], new PileRef(PileKind.Foundation, f), pile.Count - 1, X(3 + f), stockY);
            }
            for (int t = 0; t < 7; t++)
            {
                var pile = state.tableau[t].cards;
                float required = 0;
                for (int i = 0; i < pile.Count - 1; i++) required += pile[i].faceUp ? cardWidth * .36f : cardWidth * .17f;
                float available = Mathf.Max(0, root.rect.height - tableauY - cardHeight - 80);
                float compression = required > 0 ? Mathf.Min(1, available / required) : 1;
                float y = tableauY;
                for (int i = 0; i < pile.Count; i++)
                {
                    ShowCard(pile[i], new PileRef(PileKind.Tableau, t), i, X(t), y);
                    y += (pile[i].faceUp ? cardWidth * .36f : cardWidth * .17f) * compression;
                }
            }
            stockLabel.text = "TALIA · " + state.stock.Count;
            counter.text = "Dobieranie: " + state.drawCount + "   ·   " + (game.HasResult ? "Wynik: " : "Ruchy: ") + game.MoveCount;
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
            if (modal.gameObject.activeSelf || dragging) return;
            if (card.Pile.kind == PileKind.Stock) { Draw(); return; }
            if (selected != null && selected != card && TryMoveSelected(card.Pile)) return;
            if (!CanSelect(card)) return;
            if (clickCount >= 2)
            {
                selected = card;
                for (int f = 0; f < 4; f++) if (TryMoveSelected(new PileRef(PileKind.Foundation, f))) return;
            }
            selected = selected == card ? null : card;
            notice = selected == null ? null : "Wybierz kolumnę lub bazę dla zaznaczonej karty.";
            Refresh();
        }

        public void PileClicked(PileRef pile)
        {
            if (modal.gameObject.activeSelf || dragging) return;
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
            if (game.HasResult && resultRecorded) notice = "Wynik: " + game.State.finalMoves + " ruchów. Możesz dokończyć układanie.";
            Layout();
            Refresh();
        }

        public bool BeginCardDrag(CardView card, PointerEventData e)
        {
            if (modal.gameObject.activeSelf || dragging || !CanSelect(card)) return false;
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

        void AskNewGame() => OpenModal("Nowa gra?", "Bieżące rozdanie zostanie zastąpione.\nRozdać ponownie potasowane karty?", StartNewGame, "Rozdaj karty");

        void ToggleMenu()
        {
            if (modal.gameObject.activeSelf) return;
            menuOpen = !menuOpen;
            menuPopup.gameObject.SetActive(menuOpen);
            menuPopup.SetAsLastSibling();
        }

        void CloseMenu()
        {
            menuOpen = false;
            if (menuPopup != null) menuPopup.gameObject.SetActive(false);
        }

        void StartNewGame()
        {
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
            for (int i = 0; i < 10; i++)
            {
                bool active = i < result.Data.entries.Count;
                for (int col = 0; col < 3; col++) rankingCells[i + 1, col].gameObject.SetActive(active);
                if (!active) continue;
                var entry = result.Data.entries[i];
                rankingCells[i + 1, 0].text = (i + 1).ToString();
                rankingCells[i + 1, 1].text = new DateTime(entry.completedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("dd.MM.yyyy HH:mm");
                rankingCells[i + 1, 2].text = entry.moves.ToString();
            }
            Layout();
        }

        bool RecordResult()
        {
            if (!game.HasResult || resultRecorded) return true;
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
