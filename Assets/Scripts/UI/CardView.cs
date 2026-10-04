using Pasjans.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Pasjans.UI
{
    public sealed class CardView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public PileRef Pile;
        public int Index;
        public CardData Card;
        public RectTransform Rect { get; private set; }
        PasjansApp app;
        Image surface;
        Image selection;
        Text rank;
        Text lowerRank;
        Text court;
        Image smallSuit;
        Image lowerSuit;
        readonly Image[] pips = new Image[10];
        readonly CardTapCounter tapCounter = new CardTapCounter();
        bool dragging;

        public void Initialize(PasjansApp owner)
        {
            app = owner;
            Rect = (RectTransform)transform;
            surface = gameObject.AddComponent<Image>();
            surface.type = Image.Type.Sliced;
            selection = app.MakeImage(transform, "Selection", app.Art.Panel, new Color(1, .79f, .32f));
            selection.raycastTarget = false;
            rank = app.MakeText(transform, "Rank", "", 38, TextAnchor.UpperLeft, PasjansApp.Ink);
            smallSuit = app.MakeImage(transform, "Suit", null, Color.white);
            lowerRank = app.MakeText(transform, "Lower rank", "", 27, TextAnchor.LowerRight, PasjansApp.Ink);
            lowerSuit = app.MakeImage(transform, "Lower suit", null, Color.white);
            court = app.MakeText(transform, "Court", "", 52, TextAnchor.MiddleCenter, PasjansApp.Ink);
            // Grubsze oznaczenia pozostają czytelne na małym ekranie telefonu.
            rank.fontStyle = lowerRank.fontStyle = court.fontStyle = FontStyle.Bold;
            for (int i = 0; i < pips.Length; i++) pips[i] = app.MakeImage(transform, "Pip " + i, null, Color.white);
            foreach (var graphic in GetComponentsInChildren<Graphic>()) if (graphic != surface) graphic.raycastTarget = false;
        }

        public void Bind(CardData card, PileRef pile, int index, float width, float height, bool selected)
        {
            Card = card; Pile = pile; Index = index;
            surface.sprite = card.faceUp ? app.Art.Card : app.Art.BackFor(card.deckIndex);
            selection.gameObject.SetActive(selected);
            PasjansApp.Place(selection.rectTransform, -3, -3, width + 6, height + 6);
            // Zaznaczenie jest pod kartą, więc kolorowa obwódka pozostaje widoczna.
            selection.transform.SetAsFirstSibling();
            selection.color = new Color(1f, .82f, .36f, .22f);
            Color color = card.IsRed ? PasjansApp.Red : PasjansApp.Ink;
            string value = card.rank == 1 ? "A" : card.rank == 11 ? "J" : card.rank == 12 ? "Q" : card.rank == 13 ? "K" : card.rank.ToString();
            rank.text = lowerRank.text = value;
            rank.color = lowerRank.color = court.color = color;
            float scale = width / 120f;
            rank.fontSize = Mathf.RoundToInt(43 * scale);
            lowerRank.fontSize = Mathf.RoundToInt(32 * scale);
            PasjansApp.Place(rank.rectTransform, 7 * scale, 0, 60 * scale, 51 * scale);
            PasjansApp.Place(smallSuit.rectTransform, width - 39 * scale, 7 * scale, 31 * scale, 31 * scale);
            PasjansApp.Place(lowerRank.rectTransform, width - 52 * scale, height - 44 * scale, 43 * scale, 38 * scale);
            PasjansApp.Place(lowerSuit.rectTransform, 9 * scale, height - 35 * scale, 25 * scale, 25 * scale);
            smallSuit.sprite = lowerSuit.sprite = app.Art.Suits[(int)card.suit];
            smallSuit.color = lowerSuit.color = color;
            rank.gameObject.SetActive(card.faceUp);
            lowerRank.gameObject.SetActive(card.faceUp);
            smallSuit.gameObject.SetActive(card.faceUp);
            lowerSuit.gameObject.SetActive(card.faceUp);
            court.gameObject.SetActive(card.faceUp && card.rank > 10);
            court.text = card.rank == 11 ? "J" : card.rank == 12 ? "Q" : "K";
            court.fontSize = Mathf.RoundToInt(61 * scale);
            PasjansApp.Place(court.rectTransform, width * .12f, height * .24f, width * .76f, height * .5f);
            for (int i = 0; i < 10; i++)
            {
                bool visible = card.faceUp && card.rank <= 10 && i < card.rank;
                pips[i].gameObject.SetActive(visible);
                if (!visible) continue;
                pips[i].sprite = app.Art.Suits[(int)card.suit];
                pips[i].color = color;
                Vector2 pos = PipPosition(card.rank, i);
                float size = (card.rank == 1 ? 55 : 25) * scale;
                PasjansApp.Place(pips[i].rectTransform, width * pos.x - size / 2, height * pos.y - size / 2, size, size);
            }
        }

        static Vector2 PipPosition(int rank, int i)
        {
            if (rank == 1) return new Vector2(.5f, .53f);
            if (rank <= 3) return new Vector2(.5f, rank == 2 ? .36f + i * .32f : .34f + i * .19f);
            int pairs = rank / 2;
            if (rank % 2 == 1 && i == rank - 1) return new Vector2(.5f, .53f);
            return new Vector2(i % 2 == 0 ? .33f : .67f, .32f + (i / 2) * (.40f / Mathf.Max(1, pairs - 1)));
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (dragging || e.button != PointerEventData.InputButton.Left) return;
            app.CardClicked(this, tapCounter.Register(Time.unscaledTime, e.clickCount));
        }

        public void OnBeginDrag(PointerEventData e)
        {
            dragging = e.button == PointerEventData.InputButton.Left && app.BeginCardDrag(this, e);
            if (dragging) tapCounter.Reset();
        }
        public void OnDrag(PointerEventData e) { if (dragging) app.DragCards(e); }
        public void OnEndDrag(PointerEventData e) { if (dragging) app.EndCardDrag(e); dragging = false; }
    }

    public sealed class CardTapCounter
    {
        const float DoubleTapSeconds = 0.4f;
        float previousTapTime = float.NegativeInfinity;

        public int Register(float tapTime, int reportedClickCount)
        {
            if (reportedClickCount >= 2)
            {
                Reset();
                return reportedClickCount;
            }

            // Android zgłasza oba stuknięcia jako pojedyncze kliknięcia.
            bool isDoubleTap = tapTime >= previousTapTime && tapTime - previousTapTime <= DoubleTapSeconds;
            previousTapTime = isDoubleTap ? float.NegativeInfinity : tapTime;
            return isDoubleTap ? 2 : 1;
        }

        public void Reset() => previousTapTime = float.NegativeInfinity;
    }

    public sealed class PileTarget : MonoBehaviour, IPointerClickHandler
    {
        public PasjansApp App;
        public PileRef Pile;
        public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) App.PileClicked(Pile); }
    }
}
