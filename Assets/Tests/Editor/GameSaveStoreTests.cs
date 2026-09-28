using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Pasjans.Core;
using Pasjans.Persistence;
using UnityEngine;

namespace Pasjans.Tests
{
    public class GameSaveStoreTests
    {
        private string directory;
        private GameSaveStore store;
        private string Primary => Path.Combine(directory, GameSaveStore.PrimaryFileName);
        private string Backup => Path.Combine(directory, GameSaveStore.BackupFileName);

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "PasjansTests", Guid.NewGuid().ToString("N"));
            store = new GameSaveStore(directory);
        }

        [TearDown]
        public void TearDown()
        {
            // Usuń tylko tymczasowy katalog tego testu, nigdy prawdziwego zapisu gry.
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void MissingSaveIsReportedWithoutCreatingAnyFiles()
        {
            LoadResult loaded = store.Load();
            Assert.That(loaded.Success, Is.False);
            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.Missing));
            Assert.That(loaded.CanSave, Is.True);
            Assert.That(Directory.Exists(directory), Is.False);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void SaveAndLoadRestoreEveryCardOrientationOrderMoveAndStockPhase(int drawCount)
        {
            var game = new KlondikeGame(drawCount, 203);
            game.TryDraw();
            game.TryDraw();
            AssertSaved(game.State);
            LoadResult loaded = new GameSaveStore(directory).Load();
            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.Loaded));
            Assert.That(loaded.Success, Is.True);
            Assert.That(JsonUtility.ToJson(loaded.State), Is.EqualTo(JsonUtility.ToJson(game.State)));
            Assert.That(loaded.State.drawCount, Is.EqualTo(drawCount));
            Assert.That(File.Exists(Path.Combine(directory, GameSaveStore.TemporaryFileName)), Is.False);
        }

        [Test]
        public void SecondSaveKeepsPreviousPlayableSnapshotAsBackup()
        {
            var game = new KlondikeGame(3, 998);
            string first = JsonUtility.ToJson(game.State);
            AssertSaved(game.State);
            game.TryDraw();
            AssertSaved(game.State);
            Assert.That(File.Exists(Backup), Is.True);
            File.WriteAllText(Primary, "{truncated");
            LoadResult loaded = store.Load();
            Assert.That(loaded.Success, Is.True);
            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.RecoveredBackup));
            Assert.That(JsonUtility.ToJson(loaded.State), Is.EqualTo(first));
            Assert.That(loaded.CanSave, Is.True);
        }

        [Test]
        public void MissingPrimaryCanRecoverBackupAndContinueSaving()
        {
            var game = new KlondikeGame(2, 73);
            AssertSaved(game.State);
            game.TryDraw();
            AssertSaved(game.State);
            File.Delete(Primary);
            LoadResult loaded = store.Load();
            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.RecoveredBackup));
            Assert.That(loaded.State.drawCount, Is.EqualTo(2));
            AssertSaved(loaded.State);
            Assert.That(store.Load().Status, Is.EqualTo(SaveLoadStatus.Loaded));
        }

        [Test]
        public void SavingAfterRecoveryDoesNotReplaceHealthyBackupWithCorruptPrimary()
        {
            var game = new KlondikeGame(1, 92);
            AssertSaved(game.State);
            game.TryDraw();
            AssertSaved(game.State);
            string healthyBackup = File.ReadAllText(Backup);
            File.WriteAllText(Primary, "broken");
            LoadResult recovered = store.Load();
            AssertSaved(recovered.State);
            Assert.That(File.ReadAllText(Backup), Is.EqualTo(healthyBackup));
            Assert.That(store.Load().Status, Is.EqualTo(SaveLoadStatus.Loaded));
        }

        [TestCase("")]
        [TestCase("not json")]
        [TestCase("{}")]
        [TestCase("{\"formatVersion\":1}")]
        public void CorruptSaveFailsSafelyAndCanBeReplacedByFreshGame(string contents)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Primary, contents);
            LoadResult loaded = store.Load();
            Assert.That(loaded.Success, Is.False);
            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.Invalid));
            AssertSaved(new KlondikeGame().State);
            Assert.That(store.Load().Success, Is.True);
        }

        [Test]
        public void ModifiedPayloadWithoutMatchingChecksumIsRejected()
        {
            AssertSaved(new KlondikeGame(3, 86).State);
            var envelope = JsonUtility.FromJson<GameSaveEnvelope>(File.ReadAllText(Primary));
            envelope.payload = JsonUtility.ToJson(new KlondikeGame(3, 87).State);
            File.WriteAllText(Primary, JsonUtility.ToJson(envelope));
            Assert.That(store.Load().Status, Is.EqualTo(SaveLoadStatus.Invalid));
        }

        [Test]
        public void MatchingChecksumDoesNotPermitAnInvalidDeck()
        {
            GameState badState = new KlondikeGame(3, 912).State;
            badState.stock[0] = badState.stock[1];
            WriteEnvelope(Primary, badState);
            Assert.That(store.Load().Status, Is.EqualTo(SaveLoadStatus.Invalid));
        }

        [Test]
        public void InvalidStateCannotReplaceAnExistingSave()
        {
            AssertSaved(new KlondikeGame(3, 33).State);
            string existing = File.ReadAllText(Primary);
            GameState invalid = new KlondikeGame(1, 22).State;
            invalid.stock.Clear();
            Assert.That(store.Save(invalid).Success, Is.False);
            Assert.That(File.ReadAllText(Primary), Is.EqualTo(existing));
        }

        [Test]
        public void FutureEnvelopeVersionIsPreservedAndBlocksSaving()
        {
            Directory.CreateDirectory(directory);
            const string future = "{\"formatVersion\":999,\"payload\":\"future-data\"}";
            File.WriteAllText(Primary, future);
            LoadResult loaded = store.Load();
            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.UnsupportedVersion));
            Assert.That(loaded.CanSave, Is.False);
            Assert.That(store.Save(new KlondikeGame().State).Success, Is.False);
            Assert.That(File.ReadAllText(Primary), Is.EqualTo(future));
        }

        [Test]
        public void FutureStateSchemaIsPreservedEvenWithValidEnvelope()
        {
            GameState state = new KlondikeGame().State;
            state.schemaVersion = GameState.CurrentSchemaVersion + 1;
            WriteEnvelope(Primary, state);
            string existing = File.ReadAllText(Primary);
            Assert.That(store.Load().Status, Is.EqualTo(SaveLoadStatus.UnsupportedVersion));
            Assert.That(store.Save(new KlondikeGame().State).Success, Is.False);
            Assert.That(File.ReadAllText(Primary), Is.EqualTo(existing));
        }

        [Test]
        public void FuturePrimaryCanRecoverOldBackupWithoutOverwritingNewerData()
        {
            var game = new KlondikeGame(2, 99);
            AssertSaved(game.State);
            game.TryDraw();
            AssertSaved(game.State);
            const string future = "{\"formatVersion\":999}";
            File.WriteAllText(Primary, future);
            LoadResult loaded = store.Load();
            Assert.That(loaded.Success, Is.True);
            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.RecoveredBackup));
            Assert.That(loaded.CanSave, Is.False);
            Assert.That(store.Save(loaded.State).Success, Is.False);
            Assert.That(File.ReadAllText(Primary), Is.EqualTo(future));
        }

        [Test]
        public void InterruptedTemporaryWriteDoesNotHideLastGoodSave()
        {
            var game = new KlondikeGame(2, 343);
            AssertSaved(game.State);
            File.WriteAllText(Path.Combine(directory, GameSaveStore.TemporaryFileName), "partial-write");
            LoadResult loaded = store.Load();
            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.Loaded));
            Assert.That(JsonUtility.ToJson(loaded.State), Is.EqualTo(JsonUtility.ToJson(game.State)));
            game.TryDraw();
            AssertSaved(game.State);
            Assert.That(File.Exists(Path.Combine(directory, GameSaveStore.TemporaryFileName)), Is.False);
        }

        [Test]
        public void FailedTemporaryWritePreservesBothExistingSnapshots()
        {
            var game = new KlondikeGame(3, 214);
            AssertSaved(game.State);
            game.TryDraw();
            AssertSaved(game.State);
            string primary = File.ReadAllText(Primary);
            string backup = File.ReadAllText(Backup);
            Directory.CreateDirectory(Path.Combine(directory, GameSaveStore.TemporaryFileName));
            game.TryDraw();
            Assert.That(store.Save(game.State).Success, Is.False);
            Assert.That(File.ReadAllText(Primary), Is.EqualTo(primary));
            Assert.That(File.ReadAllText(Backup), Is.EqualTo(backup));
            Assert.That(store.Load().Success, Is.True);
        }

        [Test]
        public void OversizedAndInvalidUtf8FilesFailWithoutThrowing()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Primary, new byte[256 * 1024 + 1]);
            Assert.That(store.Load().Status, Is.EqualTo(SaveLoadStatus.Invalid));
            File.WriteAllBytes(Primary, new byte[] {0xff, 0xff, 0xff});
            Assert.That(store.Load().Status, Is.EqualTo(SaveLoadStatus.Invalid));
        }

        [Test]
        public void UnusableSaveDirectoryReturnsFailureInsteadOfThrowing()
        {
            Directory.CreateDirectory(directory);
            string filePath = Path.Combine(directory, "regular-file");
            File.WriteAllText(filePath, "occupied");
            var badStore = new GameSaveStore(filePath);
            Assert.That(badStore.Save(new KlondikeGame().State).Success, Is.False);
            Assert.That(badStore.Load().Success, Is.False);
            Assert.That(File.ReadAllText(filePath), Is.EqualTo("occupied"));
        }

        private void AssertSaved(GameState state)
        {
            SaveResult result = store.Save(state);
            Assert.That(result.Success, Is.True, result.Message);
        }

        private void WriteEnvelope(string path, GameState state)
        {
            Directory.CreateDirectory(directory);
            string payload = JsonUtility.ToJson(state);
            string hash;
            using (SHA256 algorithm = SHA256.Create())
                hash = string.Concat(algorithm.ComputeHash(Encoding.UTF8.GetBytes(payload)).Select(value => value.ToString("x2")));
            File.WriteAllText(path, JsonUtility.ToJson(new GameSaveEnvelope
            {
                formatVersion = GameSaveStore.CurrentFormatVersion,
                savedUtcTicks = DateTime.UtcNow.Ticks,
                payload = payload,
                sha256 = hash
            }));
        }
    }
}
