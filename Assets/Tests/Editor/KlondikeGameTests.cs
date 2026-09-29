using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pasjans.Core;

namespace Pasjans.Tests
{
    public class KlondikeGameTests
    {
        private static readonly PileRef Waste = new PileRef(PileKind.Waste);

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void NewDealHasEveryCardExactlyOnceAndOnlyTableauTopsFaceUp(int drawCount)
        {
            var game = new KlondikeGame(drawCount, 2718);
            Assert.That(game.State.stock.Count, Is.EqualTo(37));
            Assert.That(game.State.waste, Is.Empty);
            Assert.That(game.State.foundations.All(pile => pile.cards.Count == 0), Is.True);
            for (int i = 0; i < 5; i++)
            {
                var cards = game.State.tableau[i].cards;
                Assert.That(cards.Count, Is.EqualTo(i + 1));
                Assert.That(cards.Take(i).All(card => !card.faceUp), Is.True);
                Assert.That(cards[i].faceUp, Is.True);
            }
            AssertValid(game);
            Assert.That(game.MoveCount, Is.Zero);
            Assert.That(game.IsWon, Is.False);
        }

        [Test]
        public void SeedReproducesDealAndAnotherSeedChangesDeal()
        {
            var a = new KlondikeGame(1, 123);
            var b = new KlondikeGame(1, 123);
            var c = new KlondikeGame(1, 124);
            Assert.That(AllIds(a.State), Is.EqualTo(AllIds(b.State)));
            Assert.That(AllIds(a.State), Is.Not.EqualTo(AllIds(c.State)));
        }

        [Test]
        public void DrawOneAndRecyclePreserveOriginalStockOrder()
        {
            var game = new KlondikeGame(1, 83);
            int[] originalStock = game.State.stock.Select(card => card.id).ToArray();
            for (int i = 0; i < originalStock.Length; i++)
            {
                Assert.That(game.TryDraw(), Is.True);
                Assert.That(game.State.waste.Last().id, Is.EqualTo(originalStock[originalStock.Length - 1 - i]));
                Assert.That(game.State.waste.Last().faceUp, Is.True);
            }
            Assert.That(game.State.stock, Is.Empty);
            Assert.That(game.TryDraw(), Is.True);
            Assert.That(game.State.waste, Is.Empty);
            Assert.That(game.State.stock.Select(card => card.id), Is.EqualTo(originalStock));
            Assert.That(game.State.stock.All(card => !card.faceUp), Is.True);
            Assert.That(game.TryDraw(), Is.True);
            Assert.That(game.State.waste[0].id, Is.EqualTo(originalStock.Last()));
            AssertValid(game);
        }

        [Test]
        public void DrawThreeLeavesOnlyFinalWasteCardAvailable()
        {
            var game = new KlondikeGame(3, 7);
            Assert.That(game.TryDraw(), Is.True);
            Assert.That(game.State.stock.Count, Is.EqualTo(34));
            Assert.That(game.State.waste.Count, Is.EqualTo(3));
            for (int i = 0; i < 4; i++)
                Assert.That(game.CanMove(Waste, 0, Foundation(i)), Is.False);
            for (int i = 0; i < 5; i++)
                Assert.That(game.CanMove(Waste, 1, Tableau(i)), Is.False);
            AssertValid(game);
        }

        [Test]
        public void DefaultGameProgressesFromThreeToTwoToOneCardPerDraw()
        {
            var game = new KlondikeGame();
            Assert.That(game.State.drawCount, Is.EqualTo(3));
            for (int expected = 2; expected >= 1; expected--)
            {
                while (game.State.stock.Count > 0) Assert.That(game.TryDraw(), Is.True);
                Assert.That(game.TryDraw(), Is.True);
                Assert.That(game.State.drawCount, Is.EqualTo(expected));
                Assert.That(game.State.waste, Is.Empty);
                Assert.That(game.TryDraw(), Is.True);
                Assert.That(game.State.waste.Count, Is.EqualTo(expected));
            }
            while (game.State.stock.Count > 0) Assert.That(game.TryDraw(), Is.True);
            Assert.That(game.TryDraw(), Is.True);
            Assert.That(game.State.drawCount, Is.EqualTo(1));
            AssertValid(game);
            game.NewGame();
            Assert.That(game.State.drawCount, Is.EqualTo(3));
            Assert.That(game.MoveCount, Is.Zero);
        }

        [Test]
        public void DrawThreeDrawsLastPartialPacket()
        {
            GameState state = EmptyState();
            state.drawCount = 3;
            // Przenieś kartę do fundamentu, a potem do kolumny, aby zostawić pakiet dwóch kart.
            state.foundations[0].cards.Add(Card(Suit.Clubs, 1));
            state.tableau[0].cards.Add(Card(Suit.Hearts, 13));
            var game = LoadComplete(state);
            for (int i = 0; i < 16; i++) Assert.That(game.TryDraw(), Is.True);
            Assert.That(game.State.stock.Count, Is.EqualTo(2));
            Assert.That(game.TryDraw(), Is.True);
            Assert.That(game.State.stock, Is.Empty);
            Assert.That(game.State.waste.Count, Is.EqualTo(50));
            AssertValid(game);
        }

        [Test]
        public void TableauRequiresDescendingRanksAndAlternatingColors()
        {
            GameState state = EmptyState();
            state.waste.Add(Card(Suit.Hearts, 7));
            state.tableau[0].cards.Add(Card(Suit.Clubs, 8));
            state.tableau[1].cards.Add(Card(Suit.Diamonds, 8));
            state.tableau[2].cards.Add(Card(Suit.Spades, 9));
            var game = LoadComplete(state);
            Assert.That(game.CanMove(Waste, 0, Tableau(0)), Is.True);
            Assert.That(game.CanMove(Waste, 0, Tableau(1)), Is.False);
            Assert.That(game.CanMove(Waste, 0, Tableau(2)), Is.False);
            Assert.That(game.TryMove(Waste, 0, Tableau(0)), Is.True);
            Assert.That(game.State.tableau[0].cards.Last().rank, Is.EqualTo(7));
            AssertValid(game);
        }

        [Test]
        public void EmptyTableauKeepsExistingRuleAllowingAnyVisibleSequence()
        {
            GameState state = EmptyState();
            state.waste.Add(Card(Suit.Hearts, 12));
            state.tableau[0].cards.AddRange(new[] {Card(Suit.Spades, 13), Card(Suit.Diamonds, 12)});
            var game = LoadComplete(state);
            Assert.That(game.CanMove(Waste, 0, Tableau(1)), Is.True);
            Assert.That(game.CanMove(Tableau(0), 1, Tableau(1)), Is.True);
            Assert.That(game.TryMove(Tableau(0), 0, Tableau(1)), Is.True);
            Assert.That(game.State.tableau[1].cards.Select(card => card.rank), Is.EqualTo(new[] {13, 12}));
            AssertValid(game);
        }

        [Test]
        public void MovingAVisibleRunExposesPreviousHiddenCard()
        {
            GameState state = EmptyState();
            state.tableau[1].cards.AddRange(new[]
            {
                Card(Suit.Clubs, 3, false), Card(Suit.Hearts, 7), Card(Suit.Spades, 6)
            });
            state.tableau[2].cards.Add(Card(Suit.Clubs, 8));
            var game = LoadComplete(state);
            Assert.That(game.CanMove(Tableau(1), 0, Tableau(2)), Is.False);
            Assert.That(game.TryMove(Tableau(1), 1, Tableau(2)), Is.True);
            Assert.That(game.State.tableau[1].cards.Count, Is.EqualTo(1));
            Assert.That(game.State.tableau[1].cards[0].faceUp, Is.True);
            Assert.That(game.State.tableau[2].cards.Select(card => card.rank), Is.EqualTo(new[] {8, 7, 6}));
            AssertValid(game);
        }

        [Test]
        public void FoundationsStartWithAceAndContinueSameSuit()
        {
            GameState state = EmptyState();
            state.waste.Add(Card(Suit.Hearts, 2));
            state.foundations[0].cards.Add(Card(Suit.Hearts, 1));
            state.foundations[1].cards.Add(Card(Suit.Diamonds, 1));
            state.tableau[0].cards.Add(Card(Suit.Clubs, 1));
            var game = LoadComplete(state);
            Assert.That(game.CanMove(Waste, 0, Foundation(0)), Is.True);
            Assert.That(game.CanMove(Waste, 0, Foundation(1)), Is.False);
            Assert.That(game.CanMove(Waste, 0, Foundation(2)), Is.False);
            Assert.That(game.TryMove(Tableau(0), 0, Foundation(2)), Is.True);
            Assert.That(game.TryMove(Waste, 0, Foundation(0)), Is.True);
            AssertValid(game);
        }

        [Test]
        public void FoundationCannotTakeAGroupOrBuriedCard()
        {
            GameState state = EmptyState();
            state.foundations[0].cards.Add(Card(Suit.Clubs, 1));
            state.tableau[0].cards.AddRange(new[] {Card(Suit.Clubs, 2), Card(Suit.Hearts, 1)});
            var game = LoadComplete(state);
            Assert.That(game.TryMove(Tableau(0), 0, Foundation(0)), Is.False);
            Assert.That(game.TryMove(Tableau(0), 1, Foundation(1)), Is.True);
            Assert.That(game.TryMove(Tableau(0), 0, Foundation(0)), Is.True);
            AssertValid(game);
        }

        [Test]
        public void FoundationTopMayReturnToTableau()
        {
            GameState state = EmptyState();
            state.foundations[0].cards.AddRange(new[] {Card(Suit.Hearts, 1), Card(Suit.Hearts, 2)});
            state.tableau[0].cards.Add(Card(Suit.Spades, 3));
            var game = LoadComplete(state);
            Assert.That(game.CanMove(Foundation(0), 0, Tableau(0)), Is.False);
            Assert.That(game.TryMove(Foundation(0), 1, Tableau(0)), Is.True);
            AssertValid(game);
        }

        [Test]
        public void IllegalMoveDoesNotChangeCardsOrMoveCount()
        {
            var game = new KlondikeGame(1, 90);
            int[] before = AllIds(game.State);
            Assert.That(game.TryMove(Tableau(0), 0, Tableau(0)), Is.False);
            Assert.That(game.TryMove(new PileRef(PileKind.Stock), 0, Tableau(0)), Is.False);
            Assert.That(game.TryMove(Tableau(0), -1, Tableau(1)), Is.False);
            Assert.That(game.TryMove(Tableau(0), 99, Tableau(1)), Is.False);
            Assert.That(game.MoveCount, Is.Zero);
            Assert.That(AllIds(game.State), Is.EqualTo(before));
            AssertValid(game);
        }

        [Test]
        public void InvalidLoadedDeckIsRejectedWithoutReplacingCurrentGame()
        {
            var game = new KlondikeGame(1, 123);
            int[] before = AllIds(game.State);
            GameState invalid = new KlondikeGame(1, 456).State;
            invalid.stock[0] = invalid.stock[1];
            Assert.That(game.TryLoad(invalid, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(AllIds(game.State), Is.EqualTo(before));
        }

        [Test]
        public void LoadedStateIsCopiedAndCallerCannotMutateGameThroughInput()
        {
            var game = new KlondikeGame(1, 0);
            GameState input = new KlondikeGame(3, 987).State;
            Assert.That(game.TryLoad(input, out string error), Is.True, error);
            int firstId = game.State.stock[0].id;
            input.stock[0].rank = 88;
            input.tableau[0].cards.Clear();
            Assert.That(game.State.stock[0].id, Is.EqualTo(firstId));
            Assert.That(game.State.tableau[0].cards.Count, Is.EqualTo(1));
            AssertValid(game);
        }

        [Test]
        public void ValidationRejectsMissingAndMalformedState()
        {
            Assert.That(KlondikeGame.ValidateState(null, out _), Is.False);
            GameState state = new KlondikeGame(1, 10).State;
            state.stock.RemoveAt(0);
            Assert.That(KlondikeGame.ValidateState(state, out _), Is.False);
            state = new KlondikeGame(1, 10).State;
            state.stock[0].faceUp = true;
            Assert.That(KlondikeGame.ValidateState(state, out _), Is.False);
            state = new KlondikeGame(1, 10).State;
            state.tableau[0].cards[0].faceUp = false;
            Assert.That(KlondikeGame.ValidateState(state, out _), Is.False);
            state = new KlondikeGame(1, 10).State;
            state.drawCount = 4;
            Assert.That(KlondikeGame.ValidateState(state, out _), Is.False);
        }

        [Test]
        public void FourCompletedFoundationsAreAWinAndEmptyStockCannotDraw()
        {
            GameState state = EmptyState();
            for (int suit = 0; suit < 4; suit++)
                for (int rank = 1; rank <= 13; rank++)
                    state.foundations[suit].cards.Add(Card((Suit)suit, rank));
            var game = LoadComplete(state);
            Assert.That(game.IsWon, Is.True);
            Assert.That(game.TryDraw(), Is.False);
            Assert.That(game.MoveCount, Is.Zero);
            AssertValid(game);
        }

        [TestCase(1, 150, 1)]
        [TestCase(3, 151, 1)]
        [TestCase(3, 152, 2)]
        public void MixedLegalMovesAndRecyclingAlwaysPreserveDeckInvariants(int drawCount, int seed, int decks)
        {
            var random = new System.Random(seed);
            var game = new KlondikeGame(drawCount, seed, decks);
            int columns = game.State.tableau.Length;
            int piles = columns + game.State.foundations.Length;
            for (int step = 0; step < 300; step++)
            {
                var choices = new List<System.Action>();
                for (int sourceIndex = -1; sourceIndex < piles; sourceIndex++)
                {
                    PileRef source = sourceIndex < 0 ? Waste : sourceIndex < columns ? Tableau(sourceIndex) : Foundation(sourceIndex - columns);
                    for (int cardIndex = 0; cardIndex < game.GetPile(source).Count; cardIndex++)
                    {
                        for (int destinationIndex = 0; destinationIndex < piles; destinationIndex++)
                        {
                            PileRef destination = destinationIndex < columns ? Tableau(destinationIndex) : Foundation(destinationIndex - columns);
                            if (!game.CanMove(source, cardIndex, destination)) continue;
                            int selectedIndex = cardIndex;
                            choices.Add(() => Assert.That(game.TryMove(source, selectedIndex, destination), Is.True));
                        }
                    }
                }
                if (choices.Count == 0 || random.Next(3) == 0) game.TryDraw();
                else choices[random.Next(choices.Count)]();
                AssertValid(game);
            }
        }

        internal static GameState EmptyState()
        {
            return new GameState
            {
                stock = new List<CardData>(),
                waste = new List<CardData>(),
                foundations = Enumerable.Range(0, 4).Select(_ => new PileData {cards = new List<CardData>()}).ToArray(),
                tableau = Enumerable.Range(0, 5).Select(_ => new PileData {cards = new List<CardData>()}).ToArray()
            };
        }

        private static KlondikeGame LoadComplete(GameState state)
        {
            var used = new HashSet<int>(AllIds(state));
            for (int suit = 0; suit < 4; suit++)
                for (int rank = 1; rank <= 13; rank++)
                {
                    CardData card = Card((Suit)suit, rank, false);
                    if (!used.Contains(card.id)) state.stock.Add(card);
                }
            var game = new KlondikeGame();
            Assert.That(game.TryLoad(state, out string error), Is.True, error);
            return game;
        }

        private static CardData Card(Suit suit, int rank, bool faceUp = true) => new CardData(suit, rank, faceUp);
        private static PileRef Tableau(int index) => new PileRef(PileKind.Tableau, index);
        private static PileRef Foundation(int index) => new PileRef(PileKind.Foundation, index);
        private static int[] AllIds(GameState state) => state.stock.Concat(state.waste)
            .Concat(state.foundations.SelectMany(pile => pile.cards))
            .Concat(state.tableau.SelectMany(pile => pile.cards)).Select(card => card.id).ToArray();

        private static void AssertValid(KlondikeGame game)
        {
            Assert.That(KlondikeGame.ValidateState(game.State, out string error), Is.True, error);
            int[] ids = AllIds(game.State);
            Assert.That(ids.Length, Is.EqualTo(52 * game.State.DeckCount));
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
        }
    }
}
