using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Pasjans.Core;
using Pasjans.Persistence;
using UnityEngine;

namespace Pasjans.Tests
{
    public sealed class GameModeTests
    {
        string directory;

        [SetUp] public void SetUp() => directory = Path.Combine(Path.GetTempPath(), "PasjansModes", Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

        [TestCase(1, 5, 37)]
        [TestCase(2, 10, 49)]
        public void NewDealContainsFullShuffledDecksAndCorrectColumns(int decks, int columns, int stock)
        {
            var game = new KlondikeGame(seed: 208, deckCount: decks);
            Assert.That(game.State.tableau.Length, Is.EqualTo(columns));
            Assert.That(game.State.foundations.Length, Is.EqualTo(4 * decks));
            Assert.That(game.State.stock.Count, Is.EqualTo(stock));
            Assert.That(game.SelectedDeckCount, Is.EqualTo(decks));
            var all = AllCards(game.State).ToArray();
            Assert.That(all.Select(c => c.id).OrderBy(id => id), Is.EqualTo(Enumerable.Range(0, 52 * decks)));
            for (int deck = 0; deck < decks; deck++)
                Assert.That(all.Count(c => c.deckIndex == deck), Is.EqualTo(52));
            for (int col = 0; col < columns; col++)
            {
                var cards = game.State.tableau[col].cards;
                Assert.That(cards.Count, Is.EqualTo(col + 1));
                Assert.That(cards.Take(col).All(c => !c.faceUp), Is.True);
                Assert.That(cards.Last().faceUp, Is.True);
            }
            if (decks == 2)
                Assert.That(game.State.tableau.SelectMany(p => p.cards).Select(c => c.deckIndex).Distinct().Count(), Is.EqualTo(2));
            Assert.That(KlondikeGame.ValidateState(game.State, out string error), Is.True, error);
        }

        [Test]
        public void SelectionOnlyChangesNextDealAndSurvivesRestart()
        {
            var game = new KlondikeGame(seed: 34);
            game.TryDraw();
            string before = JsonUtility.ToJson(game.ExportState());
            Assert.That(game.SelectDeckCount(2), Is.True);
            var onlySelection = game.ExportState();
            onlySelection.selectedDeckCount = 1;
            Assert.That(JsonUtility.ToJson(onlySelection), Is.EqualTo(before));
            var store = new GameSaveStore(directory);
            Assert.That(store.Save(game.State).Success, Is.True);
            var restored = new KlondikeGame();
            Assert.That(restored.TryLoad(store.Load().State, out _), Is.True);
            Assert.That(restored.SelectedDeckCount, Is.EqualTo(2));
            Assert.That(restored.State.DeckCount, Is.EqualTo(1));
            Assert.That(restored.MoveCount, Is.EqualTo(1));
            restored.NewGame(seed: 72);
            Assert.That(restored.State.DeckCount, Is.EqualTo(2));
            Assert.That(restored.State.tableau.Length, Is.EqualTo(10));
            Assert.That(restored.MoveCount, Is.Zero);
            Assert.That(restored.State.drawCount, Is.EqualTo(3));
            Assert.That(restored.State.gameId, Is.Not.EqualTo(game.State.gameId));
            restored.SelectDeckCount(1);
            Assert.That(restored.State.DeckCount, Is.EqualTo(2));
            restored.NewGame();
            Assert.That(restored.State.tableau.Length, Is.EqualTo(5));
        }

        [Test]
        public void TwoDecksRoundTripPreservesIdentityBacksMovesAndDrawPhase()
        {
            var game = new KlondikeGame(seed: 42, deckCount: 2);
            while (game.State.stock.Count > 0) game.TryDraw();
            game.TryDraw();
            game.TryDraw();
            game.SelectDeckCount(1);
            var store = new GameSaveStore(directory);
            Assert.That(store.Save(game.State).Success, Is.True);
            var restored = new KlondikeGame();
            Assert.That(restored.TryLoad(store.Load().State, out string error), Is.True, error);
            Assert.That(JsonUtility.ToJson(restored.State), Is.EqualTo(JsonUtility.ToJson(game.State)));
            Assert.That(restored.State.drawCount, Is.EqualTo(2));
            // Uszkodzenie głównego zapisu nie usuwa kopii dwóch talii.
            restored.TryDraw();
            Assert.That(store.Save(restored.State).Success, Is.True);
            File.WriteAllText(Path.Combine(directory, GameSaveStore.PrimaryFileName), "broken");
            Assert.That(store.Load().Status, Is.EqualTo(SaveLoadStatus.RecoveredBackup));
            Assert.That(JsonUtility.ToJson(store.Load().State), Is.EqualTo(JsonUtility.ToJson(game.State)));
        }

        [Test]
        public void LegacySevenColumnSaveResumesUntilNewGame()
        {
            var legacy = new GameState { schemaVersion = 1, deckCount = 0, selectedDeckCount = 0, tableau = GameState.CreatePiles(7) };
            for (int suit = 0; suit < 4; suit++)
                for (int rank = 1; rank <= 13; rank++) legacy.stock.Add(new CardData((Suit)suit, rank));
            for (int row = 0; row < 7; row++)
                for (int col = row; col < 7; col++)
                {
                    var card = legacy.stock.Last();
                    legacy.stock.RemoveAt(legacy.stock.Count - 1);
                    card.faceUp = row == col;
                    legacy.tableau[col].cards.Add(card);
                }
            // Symulacja starego JSON bez pól dotyczących talii.
            string json = JsonUtility.ToJson(legacy).Replace("\"deckIndex\":0,", "").Replace("\"deckCount\":0,", "").Replace("\"selectedDeckCount\":0,", "");
            legacy = JsonUtility.FromJson<GameState>(json);
            var store = new GameSaveStore(directory);
            Assert.That(store.Save(legacy).Success, Is.True);
            var game = new KlondikeGame();
            Assert.That(game.TryLoad(store.Load().State, out string error), Is.True, error);
            Assert.That(game.State.stock.Count, Is.EqualTo(24));
            Assert.That(game.State.tableau.Length, Is.EqualTo(7));
            Assert.That(game.SelectedDeckCount, Is.EqualTo(1));
            Assert.That(AllCards(game.State).All(c => c.deckIndex == 0), Is.True);
            game.SelectDeckCount(2);
            Assert.That(store.Save(game.State).Success, Is.True);
            Assert.That(game.TryLoad(store.Load().State, out error), Is.True, error);
            game.NewGame();
            Assert.That(game.State.schemaVersion, Is.EqualTo(GameState.CurrentSchemaVersion));
            Assert.That(game.State.tableau.Length, Is.EqualTo(10));
        }

        [Test]
        public void BothCopiesOfSuitCanCompleteSeparateFoundations()
        {
            var state = new GameState { deckCount = 2, selectedDeckCount = 2, tableau = GameState.CreatePiles(10), foundations = GameState.CreatePiles(8) };
            for (int deck = 0; deck < 2; deck++)
                for (int suit = 0; suit < 4; suit++)
                    for (int rank = 1; rank <= 13; rank++)
                        state.foundations[deck * 4 + suit].cards.Add(new CardData((Suit)suit, rank, true, (deck + rank) % 2));
            var game = new KlondikeGame();
            Assert.That(game.TryLoad(state, out string error), Is.True, error);
            Assert.That(game.IsWon, Is.True);
            Assert.That(game.TryMove(new PileRef(PileKind.Foundation, 7), 12, new PileRef(PileKind.Tableau, 9)), Is.True);
            Assert.That(game.IsWon, Is.False);
            Assert.That(game.TryMove(new PileRef(PileKind.Tableau, 9), 0, new PileRef(PileKind.Foundation, 7)), Is.True);
            Assert.That(game.IsWon, Is.True);
        }

        [TestCase(0)]
        [TestCase(3)]
        public void InvalidModeIsRejectedWithoutChangingGame(int invalid)
        {
            var game = new KlondikeGame();
            string before = JsonUtility.ToJson(game.State);
            Assert.Throws<ArgumentOutOfRangeException>(() => game.SelectDeckCount(invalid));
            Assert.That(JsonUtility.ToJson(game.State), Is.EqualTo(before));
            var bad = game.ExportState();
            bad.deckCount = invalid;
            Assert.That(game.TryLoad(bad, out _), Is.False);
        }

        [Test]
        public void DuplicateOrForeignCardCannotMasqueradeAsSecondDeck()
        {
            var game = new KlondikeGame(seed: 29, deckCount: 2);
            var bad = game.ExportState();
            bad.stock[0] = bad.stock[1].Clone();
            Assert.That(game.TryLoad(bad, out _), Is.False);
            bad = game.ExportState();
            bad.stock[0].deckIndex = 2;
            Assert.That(game.TryLoad(bad, out _), Is.False);
            bad = game.ExportState();
            bad.stock[0].id = (bad.stock[0].id + 52) % 104;
            Assert.That(game.TryLoad(bad, out _), Is.False);
        }

        [Test]
        public void TenthColumnLastHiddenCardCompletesAndRecordsResultOnce()
        {
            var state = new GameState { deckCount = 2, selectedDeckCount = 2, tableau = GameState.CreatePiles(10), foundations = GameState.CreatePiles(8), moveCount = 55 };
            var hidden = new CardData(Suit.Hearts, 9, false, 1);
            var ace = new CardData(Suit.Spades, 1, true, 1);
            state.tableau[9].cards.AddRange(new[] { hidden, ace });
            for (int deck = 0; deck < 2; deck++)
                for (int suit = 0; suit < 4; suit++)
                    for (int rank = 1; rank <= 13; rank++)
                    {
                        var card = new CardData((Suit)suit, rank, false, deck);
                        if (card.id != hidden.id && card.id != ace.id) state.stock.Add(card);
                    }
            var game = new KlondikeGame();
            Assert.That(game.TryLoad(state, out string error), Is.True, error);
            game.SelectDeckCount(1);
            Assert.That(game.HasResult, Is.False);
            Assert.That(game.MoveCount, Is.EqualTo(55));
            Assert.That(game.TryMove(new PileRef(PileKind.Tableau, 9), 1, new PileRef(PileKind.Foundation, 7)), Is.True);
            Assert.That(game.HasResult, Is.True);
            Assert.That(game.State.finalMoves, Is.EqualTo(56));
            game.TryDraw();
            Assert.That(game.MoveCount, Is.EqualTo(56));
            Assert.That(game.FinalizeGrandpaLoss(), Is.True);
            var ranking = new RankingStore(directory);
            Assert.That(ranking.TryRecord(game.State, out error), Is.True, error);
            Assert.That(ranking.TryRecord(game.State, out error), Is.True, error);
            Assert.That(ranking.Load().Data.entries.Count, Is.EqualTo(1));
        }

        static IEnumerable<CardData> AllCards(GameState state) => state.stock.Concat(state.waste)
            .Concat(state.foundations.SelectMany(p => p.cards)).Concat(state.tableau.SelectMany(p => p.cards));
    }
}
