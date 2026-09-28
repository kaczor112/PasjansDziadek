using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Pasjans.Core;
using Pasjans.Persistence;
using UnityEngine;

namespace Pasjans.Tests
{
    public sealed class RankingTests
    {
        string directory;
        RankingStore store;
        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "PasjansRankingTests", Guid.NewGuid().ToString("N"));
            store = new RankingStore(directory);
        }
        [TearDown] public void TearDown() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

        [Test] public void MissingRankingIsEmptyAndCreatesNothing()
        {
            Assert.That(store.Load().Data.entries, Is.Empty);
            Assert.That(Directory.Exists(directory), Is.False);
        }

        [Test] public void LastHiddenTableauCardFreezesCountIncludingItsMoveWhileStockRemains()
        {
            var game = NearFinish();
            Assert.That(game.MoveCount, Is.EqualTo(41));
            Assert.That(game.HasResult, Is.False);
            Assert.That(game.TryMove(new PileRef(PileKind.Tableau, 1), 1, new PileRef(PileKind.Foundation, 0)), Is.True);
            Assert.That(game.State.tableau[1].cards[0].faceUp, Is.True);
            Assert.That(game.HasResult, Is.True);
            Assert.That(game.MoveCount, Is.EqualTo(42));
            Assert.That(game.State.finalMoves, Is.EqualTo(42));
            long date = game.State.completedUtcTicks;
            Assert.That(game.State.stock.Count, Is.EqualTo(50));
            Assert.That(game.TryDraw(), Is.True);
            Assert.That(game.MoveCount, Is.EqualTo(42));
            Assert.That(game.State.completedUtcTicks, Is.EqualTo(date));
            var copy = new KlondikeGame();
            Assert.That(copy.TryLoad(JsonUtility.FromJson<GameState>(JsonUtility.ToJson(game.State)), out _), Is.True);
            copy.TryDraw();
            Assert.That(copy.MoveCount, Is.EqualTo(42));
            Assert.That(copy.State.gameId, Is.EqualTo(game.State.gameId));
        }

        [Test] public void InvalidMovesAndMenusDoNotCreateResultOrCount()
        {
            var game = NearFinish();
            Assert.That(game.TryMove(new PileRef(PileKind.Tableau, 1), 0, new PileRef(PileKind.Foundation)), Is.False);
            Assert.That(game.MoveCount, Is.EqualTo(41));
            Assert.That(game.HasResult, Is.False);
            Assert.That(store.TryRecord(game.State, out _), Is.False);
        }

        [Test] public void OnlyTenLowestScoresSurviveReload()
        {
            for (int i = 20; i >= 1; i--) Assert.That(store.TryRecord(Finished(i), out _), Is.True);
            var entries = new RankingStore(directory).Load().Data.entries;
            Assert.That(entries.Count, Is.EqualTo(10));
            Assert.That(entries.Select(e => e.moves), Is.EqualTo(Enumerable.Range(1, 10)));
        }

        [Test] public void SameGameDoesNotDuplicateAfterRestart()
        {
            var state = Finished(32);
            Assert.That(store.TryRecord(state, out _), Is.True);
            Assert.That(new RankingStore(directory).TryRecord(state, out _), Is.True);
            Assert.That(store.Load().Data.entries.Count, Is.EqualTo(1));
        }

        [Test] public void EqualScoresPreferEarlierCompletion()
        {
            var older = Finished(60); var newer = Finished(60);
            older.completedUtcTicks = newer.completedUtcTicks - TimeSpan.TicksPerHour;
            Assert.That(store.TryRecord(newer, out _), Is.True);
            Assert.That(store.TryRecord(older, out _), Is.True);
            Assert.That(store.Load().Data.entries[0].gameId, Is.EqualTo(older.gameId));
        }

        [Test] public void CorruptPrimaryRecoversBackupAndBothCorruptAreEmpty()
        {
            store.TryRecord(Finished(25), out _); store.TryRecord(Finished(18), out _);
            File.WriteAllText(Path.Combine(directory, RankingStore.PrimaryFileName), "{broken");
            Assert.That(store.Load().Data.entries.Single().moves, Is.EqualTo(25));
            File.WriteAllText(Path.Combine(directory, RankingStore.BackupFileName), "broken");
            Assert.That(store.Load().Data.entries, Is.Empty);
            Assert.That(store.TryRecord(Finished(14), out _), Is.True);
            Assert.That(store.Load().Data.entries.Single().moves, Is.EqualTo(14));
        }

        [Test] public void FutureVersionIsPreservedAndFailedWriteKeepsScores()
        {
            Directory.CreateDirectory(directory);
            string file = Path.Combine(directory, RankingStore.PrimaryFileName);
            File.WriteAllText(file, "{\"formatVersion\":999}");
            Assert.That(store.Load().CanSave, Is.False);
            Assert.That(store.TryRecord(Finished(4), out _), Is.False);
            Assert.That(File.ReadAllText(file), Is.EqualTo("{\"formatVersion\":999}"));
        }

        [Test] public void InterruptedTempWritePreservesLastGoodRanking()
        {
            store.TryRecord(Finished(25), out _);
            Directory.CreateDirectory(Path.Combine(directory, "ranking.tmp"));
            Assert.That(store.TryRecord(Finished(12), out _), Is.False);
            Assert.That(store.Load().Data.entries.Single().moves, Is.EqualTo(25));
        }

        [Test] public void NewGameResetsFrozenCountAndIdentity()
        {
            var game = NearFinish();
            game.TryMove(new PileRef(PileKind.Tableau, 1), 1, new PileRef(PileKind.Foundation));
            string oldId = game.State.gameId;
            game.NewGame();
            Assert.That(game.HasResult, Is.False);
            Assert.That(game.MoveCount, Is.Zero);
            Assert.That(game.State.gameId, Is.Not.EqualTo(oldId));
            Assert.That(game.State.drawCount, Is.EqualTo(3));
        }

        static KlondikeGame NearFinish()
        {
            var state = new GameState { moveCount = 41 };
            state.tableau[1].cards.Add(new CardData(Suit.Hearts, 9));
            state.tableau[1].cards.Add(new CardData(Suit.Spades, 1, true));
            for (int s = 0; s < 4; s++) for (int r = 1; r <= 13; r++)
                if (!(s == 2 && r == 9) && !(s == 3 && r == 1)) state.stock.Add(new CardData((Suit)s, r));
            var game = new KlondikeGame();
            Assert.That(game.TryLoad(state, out string error), Is.True, error);
            return game;
        }

        static GameState Finished(int score)
        {
            var state = new GameState { moveCount = score, finalMoves = score, completedUtcTicks = DateTime.UtcNow.Ticks };
            for (int s = 0; s < 4; s++) for (int r = 1; r <= 13; r++) state.stock.Add(new CardData((Suit)s, r));
            return state;
        }
    }
}
