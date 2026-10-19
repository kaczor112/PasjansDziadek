using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pasjans.Core;
using Pasjans.UI;

namespace Pasjans.Tests
{
    public sealed class ReverseTableauSelectionTests
    {
        [Test]
        public void DifferentTableauColumnsAllowReverseAttempt()
        {
            Assert.That(PasjansApp.CanTryReverseTableauMove(
                new PileRef(PileKind.Tableau, 2), new PileRef(PileKind.Tableau, 7)), Is.True);
        }

        [Test]
        public void SameTableauColumnKeepsExistingSelectionRules()
        {
            Assert.That(PasjansApp.CanTryReverseTableauMove(
                new PileRef(PileKind.Tableau, 2), new PileRef(PileKind.Tableau, 2)), Is.False);
        }

        [TestCase(PileKind.Waste, PileKind.Tableau)]
        [TestCase(PileKind.Tableau, PileKind.Foundation)]
        [TestCase(PileKind.Foundation, PileKind.Tableau)]
        public void ReverseAttemptIsLimitedToTableau(PileKind first, PileKind second)
        {
            Assert.That(PasjansApp.CanTryReverseTableauMove(
                new PileRef(first, 0), new PileRef(second, 1)), Is.False);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void FreshTableauThenWasteMovesOnlyTopWasteCard(int decks)
        {
            var game = WasteMoveGame(decks);
            int moved = game.State.waste.Last().id;
            Assert.That(PasjansApp.TryReverseWasteMove(game, new PileRef(PileKind.Tableau), 0,
                new PileRef(PileKind.Waste), 1, true), Is.True);
            Assert.That(game.State.tableau[0].cards.Last().id, Is.EqualTo(moved));
            Assert.That(game.State.waste.Count, Is.EqualTo(1));
            Assert.That(game.MoveCount, Is.EqualTo(1));
            Assert.That(KlondikeGame.ValidateState(game.State, out string error), Is.True, error);
        }

        [Test]
        public void SelectionFromPreviousActionCannotMoveWaste()
        {
            var game = WasteMoveGame();
            int[] before = game.State.waste.Select(c => c.id).ToArray();
            Assert.That(PasjansApp.TryReverseWasteMove(game, new PileRef(PileKind.Tableau), 0,
                new PileRef(PileKind.Waste), 1, false), Is.False);
            Assert.That(game.State.waste.Select(c => c.id), Is.EqualTo(before));
            Assert.That(game.MoveCount, Is.Zero);
        }

        [Test]
        public void BuriedTableauCardCannotBecomeReverseWasteTarget()
        {
            var game = WasteMoveGame();
            game.State.tableau[0].cards.Insert(0, TakeStock(game, Suit.Diamonds, 9));
            Assert.That(PasjansApp.TryReverseWasteMove(game, new PileRef(PileKind.Tableau), 0,
                new PileRef(PileKind.Waste), 1, true), Is.False);
            Assert.That(game.State.tableau[0].cards.Count, Is.EqualTo(2));
            Assert.That(game.State.waste.Count, Is.EqualTo(2));
        }

        [Test]
        public void BuriedWasteCardCannotBeMovedInReverse()
        {
            var game = WasteMoveGame();
            game.State.waste.Insert(0, TakeStock(game, Suit.Diamonds, 7));
            Assert.That(PasjansApp.TryReverseWasteMove(game, new PileRef(PileKind.Tableau), 0,
                new PileRef(PileKind.Waste), 0, true), Is.False);
            Assert.That(game.State.waste.Count, Is.EqualTo(3));
        }

        [TestCase(Suit.Spades, 7)]
        [TestCase(Suit.Hearts, 6)]
        public void IllegalColorOrRankDoesNotMoveWaste(Suit suit, int rank)
        {
            var game = WasteMoveGame();
            var replacement = TakeStock(game, suit, rank);
            var previous = game.State.waste.Last();
            previous.faceUp = false;
            game.State.stock.Add(previous);
            game.State.waste[1] = replacement;
            Assert.That(PasjansApp.TryReverseWasteMove(game, new PileRef(PileKind.Tableau), 0,
                new PileRef(PileKind.Waste), 1, true), Is.False);
            Assert.That(game.State.tableau[0].cards.Count, Is.EqualTo(1));
            Assert.That(game.MoveCount, Is.Zero);
        }

        [TestCase(PileKind.Stock)]
        [TestCase(PileKind.Foundation)]
        public void ReverseWasteRuleDoesNotApplyToOtherPiles(PileKind kind)
        {
            var game = WasteMoveGame();
            Assert.That(PasjansApp.TryReverseWasteMove(game, new PileRef(PileKind.Tableau), 0,
                new PileRef(kind), 0, true), Is.False);
            Assert.That(PasjansApp.TryReverseWasteMove(game, new PileRef(kind), 0,
                new PileRef(PileKind.Waste), 1, true), Is.False);
            Assert.That(game.MoveCount, Is.Zero);
        }

        static KlondikeGame WasteMoveGame(int decks = 1)
        {
            var state = new GameState
            {
                deckCount = decks, selectedDeckCount = decks,
                foundations = GameState.CreatePiles(4 * decks), tableau = GameState.CreatePiles(5 * decks)
            };
            state.tableau[0].cards.Add(new CardData(Suit.Clubs, 8, true));
            state.waste.Add(new CardData(Suit.Spades, 6, true));
            state.waste.Add(new CardData(Suit.Hearts, 7, true));
            var used = new HashSet<int>(state.tableau[0].cards.Concat(state.waste).Select(c => c.id));
            for (int deck = 0; deck < decks; deck++)
                for (int suit = 0; suit < 4; suit++)
                    for (int rank = 1; rank <= 13; rank++)
                    {
                        var card = new CardData((Suit)suit, rank, false, deck);
                        if (!used.Contains(card.id)) state.stock.Add(card);
                    }
            var game = new KlondikeGame();
            Assert.That(game.TryLoad(state, out string error), Is.True, error);
            return game;
        }

        static CardData TakeStock(KlondikeGame game, Suit suit, int rank)
        {
            var card = game.State.stock.First(c => c.suit == suit && c.rank == rank);
            game.State.stock.Remove(card);
            card.faceUp = true;
            return card;
        }
    }
}
