using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Pasjans.Core;

namespace Pasjans.Persistence
{
    public enum SaveLoadStatus
    {
        Loaded,
        RecoveredBackup,
        Missing,
        Invalid,
        UnsupportedVersion,
        IoError
    }

    public sealed class SaveResult
    {
        public bool Success { get; private set; }
        public string Message { get; private set; }

        internal SaveResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }
    }

    public sealed class LoadResult
    {
        public bool Success { get { return State != null; } }
        public GameState State { get; private set; }
        public SaveLoadStatus Status { get; private set; }
        public string Message { get; private set; }
        public bool CanSave { get; private set; }

        internal LoadResult(GameState state, SaveLoadStatus status, string message, bool canSave = true)
        {
            State = state;
            Status = status;
            Message = message;
            CanSave = canSave;
        }
    }

    [Serializable]
    public sealed class GameSaveEnvelope
    {
        public int formatVersion;
        public long savedUtcTicks;
        public string payload;
        public string sha256;
    }

    /// <summary>
    /// Zapisuje sprawdzony stan JSON w podanym katalogu, zwykle persistentDataPath.
    /// Plik tymczasowy zastępuje główny atomowo, a poprzedni poprawny zapis zostaje kopią.
    /// Konstruktor i odczyt nie modyfikują danych na dysku.
    /// </summary>
    public sealed class GameSaveStore
    {
        public const int CurrentFormatVersion = 1;
        public const string PrimaryFileName = "klondike-save.json";
        public const string BackupFileName = "klondike-save.backup.json";
        public const string TemporaryFileName = "klondike-save.tmp";
        private const long MaximumFileBytes = 256 * 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly string directoryPath;
        private readonly IGameSaveJsonSerializer serializer;
        private readonly object sync = new object();

        public GameSaveStore(string directoryPath)
            : this(directoryPath, new UnityGameSaveJsonSerializer())
        {
        }

        public GameSaveStore(string directoryPath, IGameSaveJsonSerializer serializer)
        {
            this.directoryPath = directoryPath;
            this.serializer = serializer;
        }

        public LoadResult Load()
        {
            lock (sync)
            {
                try
                {
                    if (serializer == null)
                        return new LoadResult(null, SaveLoadStatus.IoError, "Brak obsługi formatu zapisu.", false);

                    string primaryPath = GetPath(PrimaryFileName);
                    string backupPath = GetPath(BackupFileName);
                    Candidate primary = ReadCandidate(primaryPath);
                    Candidate backup = ReadCandidate(backupPath);
                    if (primary.Status == SaveLoadStatus.Loaded)
                    {
                        bool canSave = backup.Status != SaveLoadStatus.UnsupportedVersion &&
                                       backup.Status != SaveLoadStatus.IoError;
                        string message = canSave ? string.Empty :
                            "Wczytano grę, ale nie można bezpiecznie zastąpić kopii zapasowej. Zapis jest wyłączony.";
                        return new LoadResult(primary.State, SaveLoadStatus.Loaded, message, canSave);
                    }

                    bool futureVersion = primary.Status == SaveLoadStatus.UnsupportedVersion ||
                                         backup.Status == SaveLoadStatus.UnsupportedVersion;
                    if (backup.Status == SaveLoadStatus.Loaded)
                    {
                        string message = futureVersion
                            ? "Wczytano kopię zapasową. Nowszy format głównego zapisu został zachowany; zapis jest wyłączony."
                            : "Przywrócono grę z kopii zapasowej.";
                        bool canSave = !futureVersion && primary.Status != SaveLoadStatus.IoError;
                        if (!futureVersion && !canSave)
                            message += " Nie można uzyskać dostępu do głównego zapisu; zapis jest wyłączony.";
                        return new LoadResult(backup.State, SaveLoadStatus.RecoveredBackup, message, canSave);
                    }

                    if (futureVersion)
                        return new LoadResult(null, SaveLoadStatus.UnsupportedVersion,
                            "Zapis pochodzi z nowszej wersji gry i nie zostanie nadpisany.", false);
                    if (primary.Status == SaveLoadStatus.Missing && backup.Status == SaveLoadStatus.Missing)
                        return new LoadResult(null, SaveLoadStatus.Missing, string.Empty);
                    if (primary.Status == SaveLoadStatus.IoError || backup.Status == SaveLoadStatus.IoError)
                        return new LoadResult(null, SaveLoadStatus.IoError,
                            "Nie można odczytać zapisu gry. " + FirstMessage(primary, backup), false);
                    return new LoadResult(null, SaveLoadStatus.Invalid,
                        "Zapis gry jest uszkodzony. Można rozpocząć nową grę.");
                }
                catch (Exception exception) when (IsRecoverable(exception))
                {
                    return new LoadResult(null, SaveLoadStatus.IoError,
                        "Nie można odczytać zapisu gry. " + exception.Message, false);
                }
            }
        }

        public SaveResult Save(GameState state)
        {
            lock (sync)
            {
                string temporaryPath = null;
                try
                {
                    if (serializer == null)
                        return new SaveResult(false, "Brak obsługi formatu zapisu.");
                    string validationError;
                    if (!KlondikeGame.ValidateState(state, out validationError))
                        return new SaveResult(false, "Nieprawidłowy stan gry: " + validationError);

                    string primaryPath = GetPath(PrimaryFileName);
                    string backupPath = GetPath(BackupFileName);
                    Candidate primary = ReadCandidate(primaryPath);
                    Candidate backup = ReadCandidate(backupPath);
                    if (primary.Status == SaveLoadStatus.UnsupportedVersion ||
                        backup.Status == SaveLoadStatus.UnsupportedVersion)
                        return new SaveResult(false, "Zapis z nowszej wersji gry został zachowany i nie zostanie nadpisany.");
                    if (primary.Status == SaveLoadStatus.IoError || backup.Status == SaveLoadStatus.IoError)
                        return new SaveResult(false, "Nie można bezpiecznie zastąpić poprzedniego zapisu. " +
                            FirstMessage(primary, backup));

                    string payload = serializer.Serialize(state);
                    var envelope = new GameSaveEnvelope
                    {
                        formatVersion = CurrentFormatVersion,
                        savedUtcTicks = DateTime.UtcNow.Ticks,
                        payload = payload,
                        sha256 = ComputeChecksum(payload)
                    };
                    byte[] bytes = Utf8.GetBytes(serializer.Serialize(envelope));
                    if (bytes.LongLength > MaximumFileBytes)
                        return new SaveResult(false, "Zapis gry przekracza dozwolony rozmiar.");

                    Directory.CreateDirectory(Path.GetDirectoryName(primaryPath));
                    temporaryPath = GetPath(TemporaryFileName);
                    using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(true);
                    }

                    // Sprawdź zapis i bajty przed zastąpieniem poprawnego pliku.
                    Candidate written = ReadCandidate(temporaryPath);
                    if (written.Status != SaveLoadStatus.Loaded)
                        return new SaveResult(false, "Nie udało się zweryfikować nowego zapisu. " + written.Message);

                    if (primary.Status == SaveLoadStatus.Missing)
                    {
                        File.Move(temporaryPath, primaryPath);
                    }
                    else
                    {
                        // Po odzyskaniu nie zastępuj poprawnej kopii uszkodzonym plikiem głównym.
                        string replacementBackup = primary.Status == SaveLoadStatus.Loaded ? backupPath : null;
                        File.Replace(temporaryPath, primaryPath, replacementBackup);
                    }
                    temporaryPath = null;
                    return new SaveResult(true, string.Empty);
                }
                catch (Exception exception) when (IsRecoverable(exception))
                {
                    return new SaveResult(false, "Nie udało się zapisać gry. " + exception.Message);
                }
                finally
                {
                    if (temporaryPath != null)
                    {
                        try
                        {
                            File.Delete(temporaryPath);
                        }
                        catch (Exception exception) when (IsRecoverable(exception))
                        {
                            // Pozostały plik tymczasowy pomiń; istniejące zapisy zostają bez zmian.
                        }
                    }
                }
            }
        }

        private string GetPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                throw new ArgumentException("Nie określono katalogu zapisu.");
            return Path.Combine(Path.GetFullPath(directoryPath), fileName);
        }

        private Candidate ReadCandidate(string path)
        {
            string json;
            try
            {
                // Otwórz bezpośrednio, bo File.Exists ukrywa błędy dostępu.
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length <= 0 || stream.Length > MaximumFileBytes)
                        return new Candidate(SaveLoadStatus.Invalid, "Nieprawidłowy rozmiar pliku.");
                    using (var reader = new StreamReader(stream, Utf8, true))
                        json = reader.ReadToEnd();
                }
            }
            catch (FileNotFoundException)
            {
                return new Candidate(SaveLoadStatus.Missing);
            }
            catch (DirectoryNotFoundException)
            {
                return new Candidate(SaveLoadStatus.Missing);
            }
            catch (DecoderFallbackException)
            {
                return new Candidate(SaveLoadStatus.Invalid, "Nieprawidłowe kodowanie pliku.");
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                return new Candidate(SaveLoadStatus.IoError, exception.Message);
            }

            try
            {
                // JsonUtility dopuszcza brak pól, więc wymagane dane sprawdzamy ręcznie.
                GameSaveEnvelope envelope = serializer.Deserialize<GameSaveEnvelope>(json);
                if (envelope == null || envelope.formatVersion <= 0)
                    return new Candidate(SaveLoadStatus.Invalid, "Brak poprawnego nagłówka zapisu.");
                if (envelope.formatVersion > CurrentFormatVersion)
                    return new Candidate(SaveLoadStatus.UnsupportedVersion);
                if (envelope.formatVersion != CurrentFormatVersion || string.IsNullOrEmpty(envelope.payload) ||
                    string.IsNullOrEmpty(envelope.sha256) || envelope.sha256.Length != 64 ||
                    !string.Equals(ComputeChecksum(envelope.payload), envelope.sha256, StringComparison.OrdinalIgnoreCase))
                    return new Candidate(SaveLoadStatus.Invalid, "Nieprawidłowa suma kontrolna zapisu.");

                GameState state = serializer.Deserialize<GameState>(envelope.payload);
                if (state != null && state.schemaVersion > GameState.CurrentSchemaVersion)
                    return new Candidate(SaveLoadStatus.UnsupportedVersion);
                string validationError;
                if (!KlondikeGame.ValidateState(state, out validationError))
                    return new Candidate(SaveLoadStatus.Invalid, validationError);
                return new Candidate(SaveLoadStatus.Loaded, string.Empty, state);
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                return new Candidate(SaveLoadStatus.Invalid, "Nieprawidłowy format zapisu: " + exception.Message);
            }
        }

        private static string ComputeChecksum(string payload)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] digest = algorithm.ComputeHash(Utf8.GetBytes(payload));
                var builder = new StringBuilder(digest.Length * 2);
                for (int i = 0; i < digest.Length; i++)
                    builder.Append(digest[i].ToString("x2"));
                return builder.ToString();
            }
        }

        private static string FirstMessage(Candidate primary, Candidate backup)
        {
            return !string.IsNullOrEmpty(primary.Message) ? primary.Message : backup.Message;
        }

        private static bool IsRecoverable(Exception exception)
        {
            return !(exception is OutOfMemoryException) && !(exception is StackOverflowException) &&
                   !(exception is AccessViolationException);
        }

        private sealed class Candidate
        {
            internal readonly SaveLoadStatus Status;
            internal readonly string Message;
            internal readonly GameState State;

            internal Candidate(SaveLoadStatus status, string message = "", GameState state = null)
            {
                Status = status;
                Message = message;
                State = state;
            }
        }
    }
}
