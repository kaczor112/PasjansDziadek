using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Pasjans.Core;
using UnityEngine;

namespace Pasjans.Persistence
{
    [Serializable]
    public sealed class RankingEntry
    {
        public string gameId;
        public long completedUtcTicks;
        public int moves;
        public bool grandpaWin;
    }

    [Serializable]
    public sealed class RankingData
    {
        public int version = 2;
        public List<RankingEntry> entries = new List<RankingEntry>();
    }

    public sealed class RankingLoadResult
    {
        public RankingData Data { get; internal set; } = new RankingData();
        public bool CanSave { get; internal set; } = true;
        public string Message { get; internal set; } = "";
    }

    /// <summary>Ranking przechowuje dziesięć wyników i ma sumę kontrolną.</summary>
    public sealed class RankingStore
    {
        public const string PrimaryFileName = "ranking.json";
        public const string BackupFileName = "ranking.backup.json";
        readonly string directory;
        readonly object sync = new object();
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        enum Condition { Missing, Valid, Invalid, Protected }
        sealed class Candidate
        {
            public Condition condition;
            public RankingData data;
        }

        public RankingStore(string directory) { this.directory = directory; }

        public RankingLoadResult Load()
        {
            lock (sync)
            {
                try { return LoadCandidates(out _, out _); }
                catch (Exception e) when (Recoverable(e))
                {
                    return new RankingLoadResult { CanSave = false, Message = "Nie można odczytać rankingu." };
                }
            }
        }

        public bool TryRecord(GameState state, out string error)
        {
            error = null;
            lock (sync)
            {
                string temp = null;
                try
                {
                    if (state == null || state.completedUtcTicks <= 0 ||
                        state.grandpaOutcome == GameState.GrandpaPending || !KlondikeGame.ValidateState(state, out _))
                    { error = "Brak poprawnego ukończonego wyniku."; return false; }
                    var result = LoadCandidates(out Candidate primary, out _);
                    if (!result.CanSave) { error = result.Message; return false; }
                    foreach (var entry in result.Data.entries)
                        if (entry.gameId == state.gameId) return true;
                    result.Data.version = 2;
                    result.Data.entries.Add(new RankingEntry
                    {
                        gameId = state.gameId, completedUtcTicks = state.completedUtcTicks,
                        moves = state.finalMoves, grandpaWin = state.grandpaOutcome == GameState.GrandpaWin
                    });
                    result.Data.entries.Sort(Compare);
                    if (result.Data.entries.Count > 10) result.Data.entries.RemoveRange(10, result.Data.entries.Count - 10);
                    // Wynik poza pierwszą dziesiątką nie wymaga zapisu poprawnego pliku.
                    if (primary.condition == Condition.Valid && !result.Data.entries.Exists(e => e.gameId == state.gameId)) return true;
                    string payload = JsonUtility.ToJson(result.Data);
                    string json = JsonUtility.ToJson(new GameSaveEnvelope { formatVersion = 1, payload = payload, sha256 = Hash(payload), savedUtcTicks = DateTime.UtcNow.Ticks });
                    string path = PathFor(PrimaryFileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    temp = PathFor("ranking.tmp");
                    byte[] bytes = Utf8.GetBytes(json);
                    using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                    { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                    if (Read(temp).condition != Condition.Valid) throw new IOException("Nieprawidłowy zapis roboczy rankingu.");
                    if (primary.condition == Condition.Missing) File.Move(temp, path);
                    else File.Replace(temp, path, primary.condition == Condition.Valid ? PathFor(BackupFileName) : null);
                    temp = null;
                    return true;
                }
                catch (Exception e) when (Recoverable(e))
                { error = "Nie udało się zapisać rankingu. Poprzednie wpisy zostały zachowane."; return false; }
                finally
                {
                    if (temp != null)
                        try { File.Delete(temp); } catch (Exception e) when (Recoverable(e)) { }
                }
            }
        }

        RankingLoadResult LoadCandidates(out Candidate primary, out Candidate backup)
        {
            primary = Read(PathFor(PrimaryFileName)); backup = Read(PathFor(BackupFileName));
            bool canSave = primary.condition != Condition.Protected && backup.condition != Condition.Protected;
            var result = new RankingLoadResult { CanSave = canSave };
            if (primary.condition == Condition.Valid) result.Data = primary.data;
            else if (backup.condition == Condition.Valid)
            { result.Data = backup.data; result.Message = "Przywrócono ranking z kopii zapasowej."; }
            if (!canSave) result.Message = "Ranking jest niedostępny lub pochodzi z nowszej wersji gry. Pliki zostały zachowane.";
            else if (primary.condition == Condition.Invalid && backup.condition != Condition.Valid) result.Message = "Nie można odczytać poprzednich wpisów rankingu.";
            return result;
        }

        Candidate Read(string path)
        {
            string json;
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length <= 0 || stream.Length > 32768) return new Candidate { condition = Condition.Invalid };
                    using (var reader = new StreamReader(stream, Utf8, true)) json = reader.ReadToEnd();
                }
            }
            catch (FileNotFoundException) { return new Candidate { condition = Condition.Missing }; }
            catch (DirectoryNotFoundException) { return new Candidate { condition = Condition.Missing }; }
            catch (DecoderFallbackException) { return new Candidate { condition = Condition.Invalid }; }
            catch (Exception e) when (Recoverable(e)) { return new Candidate { condition = Condition.Protected }; }
            try
            {
                var envelope = JsonUtility.FromJson<GameSaveEnvelope>(json);
                if (envelope != null && envelope.formatVersion > 1) return new Candidate { condition = Condition.Protected };
                if (envelope == null || envelope.formatVersion != 1 || string.IsNullOrEmpty(envelope.payload) || !string.Equals(envelope.sha256, Hash(envelope.payload), StringComparison.Ordinal))
                    return new Candidate { condition = Condition.Invalid };
                var data = JsonUtility.FromJson<RankingData>(envelope.payload);
                if (data != null && data.version > 2) return new Candidate { condition = Condition.Protected };
                if (data == null || data.version < 1 || data.version > 2 || data.entries == null || data.entries.Count > 10)
                    return new Candidate { condition = Condition.Invalid };
                var ids = new HashSet<string>();
                foreach (var entry in data.entries)
                    if (entry == null || !Guid.TryParseExact(entry.gameId, "N", out _) || !ids.Add(entry.gameId) || entry.moves < 1 || entry.completedUtcTicks <= 0 || entry.completedUtcTicks > DateTime.MaxValue.Ticks)
                        return new Candidate { condition = Condition.Invalid };
                data.entries.Sort(Compare);
                return new Candidate { condition = Condition.Valid, data = data };
            }
            catch (Exception e) when (Recoverable(e)) { return new Candidate { condition = Condition.Invalid }; }
        }

        static int Compare(RankingEntry a, RankingEntry b)
        {
            if (a.grandpaWin != b.grandpaWin) return a.grandpaWin ? -1 : 1;
            int score = a.moves.CompareTo(b.moves);
            if (score != 0) return score;
            int date = a.completedUtcTicks.CompareTo(b.completedUtcTicks);
            return date != 0 ? date : string.CompareOrdinal(a.gameId, b.gameId);
        }

        string PathFor(string file)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Brak katalogu rankingu.");
            return Path.Combine(Path.GetFullPath(directory), file);
        }

        static string Hash(string payload)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Utf8.GetBytes(payload));
                var text = new StringBuilder(64);
                foreach (byte b in hash) text.Append(b.ToString("x2"));
                return text.ToString();
            }
        }

        static bool Recoverable(Exception e) => !(e is OutOfMemoryException) && !(e is StackOverflowException) && !(e is AccessViolationException);
    }
}
