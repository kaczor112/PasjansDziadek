using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Pasjans.Core;
using Pasjans.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Pasjans.Editor
{
    /// <summary>Sprawdza przeciąganie prawdziwych widoków kart na odizolowanej planszy testowej.</summary>
    static class DragSmokeChecks
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

        internal static void Verify(PasjansApp app)
        {
            var original = app.Game.ExportState();
            string notice = Field<string>(app, "notice");
            try
            {
                foreach (int pointer in new[] { -1, 17 })
                {
                    Load(app, SampleState(original.DeckCount));
                    var source = View(app, app.Game.State.tableau[0].cards[0]);
                    var child = View(app, app.Game.State.tableau[0].cards[1]);
                    Vector3 spacing = child.Rect.position - source.Rect.position;
                    Vector2 grip = new Vector2(source.Rect.rect.width * .77f, -source.Rect.rect.height * .1f);
                    var drag = Begin(source, grip, pointer);
                    AssertGrip(source, grip, drag.position);
                    drag.position += new Vector2(77, -49);
                    source.OnDrag(drag);
                    AssertGrip(source, grip, drag.position);
                    Check((child.Rect.position - source.Rect.position - spacing).sqrMagnitude < .01f,
                        "Przeciągana grupa zmieniła odstępy między kartami.");

                    var otherFinger = new PointerEventData(EventSystem.current)
                    {
                        pointerId = pointer + 1, position = drag.position + new Vector2(100, 100)
                    };
                    Vector3 held = source.Rect.position;
                    source.OnBeginDrag(otherFinger);
                    source.OnDrag(otherFinger);
                    source.OnEndDrag(otherFinger);
                    Check(source.Rect.position == held && Field<bool>(app, "dragging"),
                        "Drugi palec przejął lub zakończył przeciąganie.");
                    drag.position += new Vector2(11, -8);
                    source.OnDrag(drag);
                    AssertGrip(source, grip, drag.position);
                    var target = Target(app, PileKind.Tableau, 1);
                    drag.position = ScreenPoint(target, new Vector2(target.rect.xMax + Field<float>(app, "gap") * .4f,
                        -Field<float>(app, "cardHeight") * .5f));
                    source.OnEndDrag(drag);
                    Check(app.Game.State.tableau[0].cards.Count == 0 && app.Game.State.tableau[1].cards.Count == 3,
                        "Upuszczenie obok krawędzi kolumny nie przeniosło poprawnej grupy.");
                    Check(source.GetComponent<Image>().raycastTarget && child.GetComponent<Image>().raycastTarget,
                        "Karty po upuszczeniu nie odzyskały obsługi dotyku.");

                    VerifyLowColumnDrop(app, original.DeckCount, pointer, 1);
                    VerifyLowColumnDrop(app, original.DeckCount, pointer, 3);
                    VerifyRejectedDrop(app, original.DeckCount, pointer, false);
                    VerifyRejectedDrop(app, original.DeckCount, pointer, true);
                    for (int count = 1; count <= 3; count++)
                    {
                        VerifyFoundationDrag(app, original.DeckCount, pointer, count, false);
                        VerifyFoundationDrag(app, original.DeckCount, pointer, count, true);
                    }

                    Load(app, SampleState(original.DeckCount));
                    source = View(app, app.Game.State.waste.Last());
                    grip = new Vector2(source.Rect.rect.width * .8f, -source.Rect.rect.height * .85f);
                    drag = Begin(source, grip, pointer);
                    app.SendMessage("CancelSelection");
                    app.SendMessage("Refresh");
                    drag = Begin(source, grip, pointer);
                    AssertGrip(source, grip, drag.position);
                    target = Target(app, PileKind.Foundation, 0);
                    drag.position = ScreenPoint(target, new Vector2(target.rect.xMax + Field<float>(app, "gap") * .4f,
                        target.rect.yMax + Field<float>(app, "cardHeight") * .08f));
                    source.OnEndDrag(drag);
                    Check(app.Game.State.foundations[0].cards.Count == 2 && app.Game.State.waste.Count == 0,
                        "Upuszczenie przy krawędzi bazy nie przeniosło pasującej karty.");
                }
                Debug.Log("Pasjans drag checks PASSED: " + original.DeckCount + " talie, " + Screen.width + "x" + Screen.height +
                    ", mysz i dotyk, punkt złapania, grupa, marginesy, puste kolumny, bazy, karta pod przeciąganą bazą i odrzucanie błędnych celów.");
            }
            finally
            {
                Load(app, original);
                typeof(PasjansApp).GetField("notice", Fields).SetValue(app, notice);
                app.SendMessage("Refresh");
            }
        }

        static void VerifyLowColumnDrop(PasjansApp app, int decks, int pointer, int column)
        {
            Load(app, SampleState(decks));
            var source = View(app, app.Game.State.tableau[0].cards[0]);
            var drag = Begin(source, new Vector2(4, -4), pointer);
            var target = Target(app, PileKind.Tableau, column);
            drag.position = ScreenPoint(target, new Vector2(target.rect.center.x, target.rect.yMin + 8));
            source.OnEndDrag(drag);
            Check(app.Game.State.tableau[0].cards.Count == 0 && source.Pile.Equals(new PileRef(PileKind.Tableau, column)),
                "Kolumna nie przyjęła upuszczenia poniżej widocznych kart.");
        }

        static void VerifyRejectedDrop(PasjansApp app, int decks, int pointer, bool outside)
        {
            Load(app, SampleState(decks));
            var source = View(app, app.Game.State.tableau[0].cards[0]);
            Vector3 origin = source.Rect.position;
            var drag = Begin(source, new Vector2(3, -3), pointer);
            var target = Target(app, PileKind.Tableau, 2);
            // Karta może zasłaniać legalny cel obok, ale liczy się pozycja palca.
            drag.position = outside ? new Vector2(-20, Screen.height + 20) :
                ScreenPoint(target, new Vector2(target.rect.xMax - 1, -Field<float>(app, "cardHeight") * .5f));
            source.OnEndDrag(drag);
            Check(app.Game.State.tableau[0].cards.Count == 2 && app.Game.MoveCount == 0,
                "Niedozwolone upuszczenie przeniosło karty lub zmieniło licznik.");
            Check((source.Rect.position - origin).sqrMagnitude < .01f && source.GetComponent<Image>().raycastTarget &&
                Field<CardView>(app, "selected") == null && !Field<bool>(app, "dragging"),
                "Anulowane przeciąganie nie przywróciło kart i ich obsługi.");
        }

        static void VerifyFoundationDrag(PasjansApp app, int decks, int pointer, int count, bool reject)
        {
            var state = SampleState(decks);
            for (int rank = 2; rank <= count; rank++)
            {
                var card = rank == 2 ? state.waste.Last() : state.stock.Single(c => c.suit == Suit.Hearts && c.rank == rank && c.deckIndex == 0);
                if (rank == 2) state.waste.Remove(card); else state.stock.Remove(card);
                card.faceUp = true;
                state.foundations[0].cards.Add(card);
            }
            Load(app, state);
            string before = JsonUtility.ToJson(app.Game.State);
            var pile = new PileRef(PileKind.Foundation, 0);
            var source = View(app, app.Game.State.foundations[0].cards.Last());
            Vector3 origin = source.Rect.position;
            var drag = Begin(source, new Vector2(6, -6), pointer);
            drag.position += new Vector2(80, -100);
            source.OnDrag(drag);
            Check(JsonUtility.ToJson(app.Game.State) == before, "Samo uniesienie karty zmieniło rozdanie.");
            if (count > 1)
            {
                var underneath = View(app, app.Game.State.foundations[0].cards[count - 2]);
                Check(underneath.gameObject.activeInHierarchy && (underneath.Rect.position - origin).sqrMagnitude < .01f &&
                    underneath.transform.GetSiblingIndex() < source.transform.GetSiblingIndex(),
                    "Pod kartą przeciąganą z bazy nie widać poprzedniej karty na jej miejscu.");
            }

            var target = Target(app, PileKind.Tableau, 3);
            drag.position = reject ? new Vector2(-20, Screen.height + 20) : ScreenPoint(target, target.rect.center);
            source.OnEndDrag(drag);
            if (reject)
                Check(JsonUtility.ToJson(app.Game.State) == before && (source.Rect.position - origin).sqrMagnitude < .01f,
                    "Odrzucony ruch z bazy nie odtworzył pierwotnego stosu.");
            else
            {
                Check(app.Game.State.foundations[0].cards.Count == count - 1 && source.Pile.Equals(new PileRef(PileKind.Tableau, 3)),
                    "Upuszczenie karty z bazy na pustą kolumnę nie wykonało ruchu.");
                Check(Field<CardView[]>(app, "cards").Any(c => c.gameObject.activeInHierarchy && c.Pile.Equals(pile)) == (count > 1),
                    "Widok bazy po ruchu nie zgadza się z liczbą pozostałych kart.");
            }
            Check(!Field<bool>(app, "dragging") && source.GetComponent<Image>().raycastTarget,
                "Koniec przeciągania z bazy nie przywrócił obsługi kart.");
        }

        static PointerEventData Begin(CardView source, Vector2 grip, int pointer)
        {
            Vector2 pressed = ScreenPoint(source.Rect, grip);
            var drag = new PointerEventData(EventSystem.current)
            {
                pointerId = pointer, button = PointerEventData.InputButton.Left,
                pressPosition = pressed, position = pressed + new Vector2(23, -17)
            };
            source.OnBeginDrag(drag);
            return drag;
        }

        static void AssertGrip(CardView source, Vector2 grip, Vector2 pointer)
        {
            Check(Vector2.Distance(ScreenPoint(source.Rect, grip), pointer) < .25f,
                "Punkt złapania karty odsunął się od kursora lub palca.");
        }

        static void Load(PasjansApp app, GameState state)
        {
            app.SendMessage("CancelSelection");
            Check(app.Game.TryLoad(state, out string error), error);
            app.SendMessage("Layout");
            app.SendMessage("Refresh");
        }

        static GameState SampleState(int decks)
        {
            var state = new GameState
            {
                deckCount = decks, selectedDeckCount = decks,
                tableau = GameState.CreatePiles(5 * decks), foundations = GameState.CreatePiles(4 * decks)
            };
            state.tableau[0].cards.AddRange(new[] { new CardData(Suit.Spades, 6, true), new CardData(Suit.Hearts, 5, true) });
            state.tableau[1].cards.Add(new CardData(Suit.Diamonds, 7, true));
            state.tableau[2].cards.Add(new CardData(Suit.Clubs, 7, true));
            state.foundations[0].cards.Add(new CardData(Suit.Hearts, 1, true));
            state.waste.Add(new CardData(Suit.Hearts, 2, true));
            var used = new HashSet<int>(state.tableau.SelectMany(p => p.cards).Concat(state.foundations.SelectMany(p => p.cards))
                .Concat(state.waste).Select(c => c.id));
            for (int deck = 0; deck < decks; deck++)
                for (int suit = 0; suit < 4; suit++)
                    for (int rank = 1; rank <= 13; rank++)
                    {
                        var card = new CardData((Suit)suit, rank, false, deck);
                        if (!used.Contains(card.id)) state.stock.Add(card);
                    }
            return state;
        }

        static CardView View(PasjansApp app, CardData card) => Field<CardView[]>(app, "cards")[card.id];
        static RectTransform Target(PasjansApp app, PileKind kind, int index) => (RectTransform)app.GetComponentsInChildren<PileTarget>()
            .First(target => target.Pile.Equals(new PileRef(kind, index))).transform;
        static Vector2 ScreenPoint(RectTransform rect, Vector2 point) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(point));
        static T Field<T>(PasjansApp app, string name) => (T)typeof(PasjansApp).GetField(name, Fields).GetValue(app);
        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
