using System;
using System.Collections.Generic;

namespace Pasjans.Core
{
    /// <summary>
    /// Zasady klasycznego pasjansa Klondike bez grafiki i dostępu do plików.
    /// Talie można przewijać bez limitu: najpierw dobierane są 3 karty, potem 2, a następnie 1.
    /// Każde udane dobranie, przewinięcie lub przeniesienie liczy się jako jeden ruch.
    /// </summary>
    public sealed class KlondikeGame
    {
        private static readonly CardData[] EmptyPile = new CardData[0];

        public GameState State { get; private set; }
        public int MoveCount { get { return State.moveCount; } }
        public bool HasResult { get { return State.completedUtcTicks > 0; } }

        public bool IsWon
        {
            get
            {
                for (int i = 0; i < 4; i++)
                {
                    if (State.foundations[i].cards.Count != 13)
                        return false;
                }
                return true;
            }
        }

        public KlondikeGame(int drawCount = 3, int? seed = null)
        {
            NewGame(drawCount, seed);
        }

        public void NewGame(int drawCount = 3, int? seed = null)
        {
            if (drawCount < 1 || drawCount > 3)
                throw new ArgumentOutOfRangeException(nameof(drawCount), "Draw count must be between 1 and 3.");

            var next = new GameState { drawCount = drawCount };
            for (int suit = 0; suit < 4; suit++)
            {
                for (int rank = 1; rank <= 13; rank++)
                    next.stock.Add(new CardData((Suit)suit, rank));
            }

            // Stałe ziarno powtarza rozdanie w testach, a losowe tworzy nowe rozdanie.
            var random = new Random(seed ?? Guid.NewGuid().GetHashCode());
            for (int i = next.stock.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                CardData swap = next.stock[i];
                next.stock[i] = next.stock[j];
                next.stock[j] = swap;
            }

            for (int row = 0; row < 7; row++)
            {
                for (int column = row; column < 7; column++)
                {
                    CardData card = RemoveTop(next.stock);
                    card.faceUp = row == column;
                    next.tableau[column].cards.Add(card);
                }
            }

            State = next;
        }

        /// <summary>Niepoprawny zapis nie zastępuje bieżącego stanu gry.</summary>
        public bool TryLoad(GameState state, out string error)
        {
            if (!ValidateState(state, out error))
                return false;

            State = CloneState(state);
            if (string.IsNullOrEmpty(State.gameId)) State.gameId = Guid.NewGuid().ToString("N");
            return true;
        }

        public GameState ExportState()
        {
            return CloneState(State);
        }

        public IReadOnlyList<CardData> GetPile(PileRef pile)
        {
            List<CardData> cards = ResolvePile(pile);
            return cards == null ? (IReadOnlyList<CardData>)EmptyPile : cards;
        }

        public bool TryDraw()
        {
            if (State.stock.Count > 0)
            {
                int count = Math.Min(State.drawCount, State.stock.Count);
                for (int i = 0; i < count; i++)
                {
                    CardData card = RemoveTop(State.stock);
                    card.faceUp = true;
                    State.waste.Add(card);
                }
            }
            else if (State.waste.Count > 0)
            {
                // Przewiń cały stos odrzuconych kart z zachowaniem kolejności.
                while (State.waste.Count > 0)
                {
                    CardData card = RemoveTop(State.waste);
                    card.faceUp = false;
                    State.stock.Add(card);
                }
                if (State.drawCount > 1)
                    State.drawCount--;
            }
            else
            {
                return false;
            }

            CountMove();
            return true;
        }

        public bool CanPickUp(PileRef source, int cardIndex)
        {
            List<CardData> cards = ResolvePile(source);
            if (cards == null || cardIndex < 0 || cardIndex >= cards.Count || !cards[cardIndex].faceUp)
                return false;

            if (source.kind == PileKind.Waste || source.kind == PileKind.Foundation)
                return cardIndex == cards.Count - 1;

            if (source.kind != PileKind.Tableau)
                return false;

            for (int i = cardIndex + 1; i < cards.Count; i++)
            {
                if (!cards[i].faceUp || !CanStackOnTableau(cards[i], cards[i - 1]))
                    return false;
            }
            return true;
        }

        /// <summary>cardIndex wskazuje pierwszą przenoszoną kartę, licząc od spodu stosu.</summary>
        public bool CanMove(PileRef source, int cardIndex, PileRef destination)
        {
            if (source.Equals(destination) || !CanPickUp(source, cardIndex))
                return false;

            List<CardData> target = ResolvePile(destination);
            if (target == null)
                return false;

            List<CardData> from = ResolvePile(source);
            CardData card = from[cardIndex];
            if (destination.kind == PileKind.Tableau)
            {
                if (target.Count == 0)
                    return true; // Pusty fundament przyjmuje dowolną kartę.

                CardData top = target[target.Count - 1];
                return top.faceUp && CanStackOnTableau(card, top);
            }

            if (destination.kind != PileKind.Foundation || cardIndex != from.Count - 1)
                return false;

            if (target.Count == 0)
                return card.rank == 1;

            CardData foundationTop = target[target.Count - 1];
            return target.Count < 13 && card.suit == foundationTop.suit && card.rank == foundationTop.rank + 1;
        }

        public bool TryMove(PileRef source, int cardIndex, PileRef destination)
        {
            if (!CanMove(source, cardIndex, destination))
                return false;

            List<CardData> from = ResolvePile(source);
            List<CardData> target = ResolvePile(destination);
            int count = from.Count - cardIndex;
            for (int i = cardIndex; i < from.Count; i++)
                target.Add(from[i]);
            from.RemoveRange(cardIndex, count);

            if (source.kind == PileKind.Tableau && from.Count > 0)
                from[from.Count - 1].faceUp = true;

            CountMove();
            return true;
        }

        /// <summary>
        /// Sprawdza strukturę, identyfikatory, wszystkie 52 karty, ich strony i zasady stosów.
        /// Niekompletne lub uszkodzone zapisy są odrzucane bez naprawiania danych.
        /// </summary>
        public static bool ValidateState(GameState state, out string error)
        {
            error = null;
            if (state == null)
                return Fail("Game state is missing.", out error);
            if (state.schemaVersion != GameState.CurrentSchemaVersion)
                return Fail("Unsupported game state version.", out error);
            if (state.drawCount < 1 || state.drawCount > 3)
                return Fail("Invalid stock draw count.", out error);
            if (state.moveCount < 0)
                return Fail("Invalid move count.", out error);
            if (!string.IsNullOrEmpty(state.gameId) && !Guid.TryParseExact(state.gameId, "N", out _))
                return Fail("Invalid game identity.", out error);
            if (state.completedUtcTicks < 0 || state.completedUtcTicks > DateTime.MaxValue.Ticks ||
                state.finalMoves < 0 || (state.completedUtcTicks > 0 &&
                (string.IsNullOrEmpty(state.gameId) || state.finalMoves < 1 || state.finalMoves != state.moveCount)))
                return Fail("Invalid completed result.", out error);
            if (state.foundations == null || state.foundations.Length != 4 ||
                state.tableau == null || state.tableau.Length != 7)
                return Fail("Incorrect number of piles.", out error);

            var seen = new bool[52];
            int total = 0;
            if (!ValidateCards(state.stock, 52, seen, ref total, out error) ||
                !ValidateCards(state.waste, 52, seen, ref total, out error))
                return false;

            for (int i = 0; i < state.stock.Count; i++)
            {
                if (state.stock[i].faceUp)
                    return Fail("Stock contains a face-up card.", out error);
            }
            for (int i = 0; i < state.waste.Count; i++)
            {
                if (!state.waste[i].faceUp)
                    return Fail("Waste contains a face-down card.", out error);
            }

            for (int pile = 0; pile < 4; pile++)
            {
                PileData foundation = state.foundations[pile];
                if (foundation == null)
                    return Fail("Foundation is missing.", out error);
                if (!ValidateCards(foundation.cards, 13, seen, ref total, out error))
                    return false;

                for (int i = 0; i < foundation.cards.Count; i++)
                {
                    CardData card = foundation.cards[i];
                    if (!card.faceUp || card.rank != i + 1 || card.suit != foundation.cards[0].suit)
                        return Fail("Invalid foundation sequence.", out error);
                }
            }

            for (int pile = 0; pile < 7; pile++)
            {
                PileData tableau = state.tableau[pile];
                if (tableau == null)
                    return Fail("Tableau pile is missing.", out error);
                if (!ValidateCards(tableau.cards, 19, seen, ref total, out error))
                    return false;

                bool reachedFaceUp = false;
                int hiddenCount = 0;
                for (int i = 0; i < tableau.cards.Count; i++)
                {
                    CardData card = tableau.cards[i];
                    if (!card.faceUp)
                    {
                        if (reachedFaceUp)
                            return Fail("Face-down card follows a face-up tableau card.", out error);
                        hiddenCount++;
                    }
                    else
                    {
                        if (reachedFaceUp && !CanStackOnTableau(card, tableau.cards[i - 1]))
                            return Fail("Invalid visible tableau sequence.", out error);
                        reachedFaceUp = true;
                    }
                }

                if (hiddenCount > pile)
                    return Fail("Too many hidden cards in a tableau pile.", out error);
                if (state.completedUtcTicks > 0 && hiddenCount > 0)
                    return Fail("Completed game still has hidden tableau cards.", out error);
                if (tableau.cards.Count > 0 && !reachedFaceUp)
                    return Fail("Tableau top card must be face-up.", out error);
            }

            return total == 52 || Fail("Game state must contain exactly 52 cards.", out error);
        }

        private static bool ValidateCards(List<CardData> cards, int maximumCount, bool[] seen,
            ref int total, out string error)
        {
            error = null;
            if (cards == null || cards.Count > maximumCount)
                return Fail("Missing or oversized card pile.", out error);

            for (int i = 0; i < cards.Count; i++)
            {
                CardData card = cards[i];
                if (card == null || card.rank < 1 || card.rank > 13 || (int)card.suit < 0 || (int)card.suit > 3)
                    return Fail("Invalid card data.", out error);
                int expectedId = (int)card.suit * 13 + card.rank - 1;
                if (card.id != expectedId || seen[expectedId])
                    return Fail("Invalid or duplicate card identity.", out error);

                seen[expectedId] = true;
                total++;
            }
            return true;
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }

        private static bool CanStackOnTableau(CardData card, CardData below)
        {
            return card.rank == below.rank - 1 && card.IsRed != below.IsRed;
        }

        private List<CardData> ResolvePile(PileRef pile)
        {
            switch (pile.kind)
            {
                case PileKind.Stock:
                    return pile.index == 0 ? State.stock : null;
                case PileKind.Waste:
                    return pile.index == 0 ? State.waste : null;
                case PileKind.Foundation:
                    return pile.index >= 0 && pile.index < 4 ? State.foundations[pile.index].cards : null;
                case PileKind.Tableau:
                    return pile.index >= 0 && pile.index < 7 ? State.tableau[pile.index].cards : null;
                default:
                    return null;
            }
        }

        private static CardData RemoveTop(List<CardData> cards)
        {
            int lastIndex = cards.Count - 1;
            CardData card = cards[lastIndex];
            cards.RemoveAt(lastIndex);
            return card;
        }

        private void CountMove()
        {
            if (HasResult) return;
            if (State.moveCount < int.MaxValue)
                State.moveCount++;
            for (int p = 0; p < State.tableau.Length; p++)
                for (int i = 0; i < State.tableau[p].cards.Count; i++)
                    if (!State.tableau[p].cards[i].faceUp) return;
            State.finalMoves = State.moveCount;
            State.completedUtcTicks = DateTime.UtcNow.Ticks;
        }

        private static GameState CloneState(GameState source)
        {
            var copy = new GameState
            {
                schemaVersion = source.schemaVersion,
                drawCount = source.drawCount,
                moveCount = source.moveCount,
                gameId = source.gameId,
                completedUtcTicks = source.completedUtcTicks,
                finalMoves = source.finalMoves,
                stock = CloneCards(source.stock),
                waste = CloneCards(source.waste)
            };
            for (int i = 0; i < 4; i++)
                copy.foundations[i].cards = CloneCards(source.foundations[i].cards);
            for (int i = 0; i < 7; i++)
                copy.tableau[i].cards = CloneCards(source.tableau[i].cards);
            return copy;
        }

        private static List<CardData> CloneCards(List<CardData> source)
        {
            var cards = new List<CardData>(source.Count);
            for (int i = 0; i < source.Count; i++)
                cards.Add(source[i].Clone());
            return cards;
        }
    }
}
