using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SantasLittleHelpers.Savegames
{
    public sealed class SaveSlotStore
    {
        public const int CurrentSchemaVersion = 1;
        public const int MaxDisplayNameLength = 64;
        public const int MaxMissionIdLength = 128;
        public const string DefaultStorageFolderName = "save-slots";
        public const string SlotFilePrefix = "slot-";
        public const string SlotFileExtension = ".slotsave";

        private const string TempExtension = ".tmp";
        private const int MaxCreateAttempts = 10;

        private readonly object sync = new object();
        private readonly string rootDirectory;
        private readonly ISaveSlotFileSystem fileSystem;

        public SaveSlotStore()
            : this(null, null)
        {
        }

        public SaveSlotStore(string storageRoot)
            : this(storageRoot, null)
        {
        }

        public SaveSlotStore(string storageRoot, ISaveSlotFileSystem fileSystem)
        {
            string root = string.IsNullOrWhiteSpace(storageRoot) ? DefaultStorageDirectory : storageRoot;
            rootDirectory = Path.GetFullPath(root);
            this.fileSystem = fileSystem ?? new DefaultSaveSlotFileSystem();
        }

        public static string DefaultStorageDirectory =>
            Path.Combine(Application.persistentDataPath, DefaultStorageFolderName);

        public string RootDirectory => rootDirectory;

        public static string GetSlotFileName(SlotId slotId)
        {
            if (slotId.IsEmpty)
            {
                throw new ArgumentException("A slot id is required.", nameof(slotId));
            }

            return SlotFilePrefix + slotId.ToFileToken() + SlotFileExtension;
        }

        public string GetSlotPath(SlotId slotId)
        {
            return Path.Combine(rootDirectory, GetSlotFileName(slotId));
        }

        public Task<SlotResult<IReadOnlyList<SaveSlotSummary>>> ListSlots(CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                return Task.FromResult(SlotResult<IReadOnlyList<SaveSlotSummary>>.Failure(
                    SlotErrorCode.Cancelled,
                    "Listing save slots was cancelled."));
            }

            lock (sync)
            {
                return Task.FromResult(ListSlotsCore());
            }
        }

        public Task<SlotResult<SaveSlot>> LoadSlot(SlotId slotId, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                return Task.FromResult(SlotResult<SaveSlot>.Failure(
                    SlotErrorCode.Cancelled,
                    "Loading the save slot was cancelled."));
            }

            lock (sync)
            {
                return Task.FromResult(LoadSlotCore(slotId));
            }
        }

        public Task<SlotResult<SaveSlot>> CreateSlot(string displayName, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                return Task.FromResult(SlotResult<SaveSlot>.Failure(
                    SlotErrorCode.Cancelled,
                    "Creating the save slot was cancelled."));
            }

            lock (sync)
            {
                return Task.FromResult(CreateSlotCore(displayName));
            }
        }

        public Task<SlotResult<Unit>> RenameSlot(SlotId slotId, string displayName, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                return Task.FromResult(SlotResult<Unit>.Failure(
                    SlotErrorCode.Cancelled,
                    "Renaming the save slot was cancelled."));
            }

            lock (sync)
            {
                return Task.FromResult(RenameSlotCore(slotId, displayName));
            }
        }

        public Task<SlotResult<Unit>> DeleteSlot(SlotId slotId, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                return Task.FromResult(SlotResult<Unit>.Failure(
                    SlotErrorCode.Cancelled,
                    "Deleting the save slot was cancelled."));
            }

            lock (sync)
            {
                return Task.FromResult(DeleteSlotCore(slotId));
            }
        }

        public Task<SlotResult<Unit>> CommitMission(MissionCommit commit, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                return Task.FromResult(SlotResult<Unit>.Failure(
                    SlotErrorCode.Cancelled,
                    "Committing the mission was cancelled."));
            }

            lock (sync)
            {
                return Task.FromResult(CommitMissionCore(commit));
            }
        }

        private SlotResult<IReadOnlyList<SaveSlotSummary>> ListSlotsCore()
        {
            List<string> files;
            try
            {
                EnsureRootDirectory();
                files = fileSystem.EnumerateFiles(rootDirectory, SlotFilePrefix + "*" + SlotFileExtension).ToList();
            }
            catch (Exception exception)
            {
                return SlotResult<IReadOnlyList<SaveSlotSummary>>.Failure(
                    SlotErrorCode.StorageUnavailable,
                    exception.Message);
            }

            List<SaveSlotSummary> summaries = new List<SaveSlotSummary>();
            foreach (string file in files)
            {
                if (!TryParseSlotFileName(Path.GetFileName(file), out SlotId slotId))
                {
                    continue;
                }

                if (!TryReadModel(file, out SaveSlotFileModel model, out SlotErrorCode readError, out string readMessage))
                {
                    if (readError == SlotErrorCode.SlotNotFound)
                    {
                        continue;
                    }

                    SaveSlotStatus status = readError == SlotErrorCode.UnsupportedSchema
                        ? SaveSlotStatus.UnsupportedSchema
                        : readError == SlotErrorCode.CorruptData
                            ? SaveSlotStatus.CorruptData
                            : SaveSlotStatus.Unreadable;
                    summaries.Add(CreateSummary(slotId, model, status, readError, readMessage));
                    continue;
                }

                if (!TryBuildSlot(model, slotId, out SaveSlot slot, out SlotErrorCode error, out string message))
                {
                    SaveSlotStatus status = error == SlotErrorCode.UnsupportedSchema
                        ? SaveSlotStatus.UnsupportedSchema
                        : SaveSlotStatus.CorruptData;
                    summaries.Add(CreateSummary(slotId, model, status, error, message));
                    continue;
                }

                summaries.Add(new SaveSlotSummary(
                    slot.SlotId,
                    slot.DisplayName,
                    slot.CreatedAtUtc,
                    slot.CompletedMissionIds.Count,
                    slot.CommitLedger.Count,
                    slot.SchemaVersion,
                    SaveSlotStatus.Ok,
                    SlotErrorCode.None,
                    string.Empty));
            }

            summaries.Sort(CompareSummaries);
            return SlotResult<IReadOnlyList<SaveSlotSummary>>.Success(summaries.AsReadOnly());
        }

        private SlotResult<SaveSlot> LoadSlotCore(SlotId slotId)
        {
            if (slotId.IsEmpty)
            {
                return SlotResult<SaveSlot>.Failure(SlotErrorCode.InvalidSlot, "A non-empty slot id is required.");
            }

            string path = GetSlotPath(slotId);
            if (!TryReadModel(path, out SaveSlotFileModel model, out SlotErrorCode readError, out string readMessage))
            {
                return SlotResult<SaveSlot>.Failure(readError, readMessage);
            }

            if (!TryBuildSlot(model, slotId, out SaveSlot slot, out SlotErrorCode error, out string message))
            {
                return SlotResult<SaveSlot>.Failure(error, message);
            }

            return SlotResult<SaveSlot>.Success(slot);
        }

        private SlotResult<SaveSlot> CreateSlotCore(string displayName)
        {
            if (!TryNormalizeDisplayName(displayName, out string normalizedName))
            {
                return SlotResult<SaveSlot>.Failure(
                    SlotErrorCode.InvalidName,
                    "The display name must be 1 to 64 characters and must not contain path or control characters.");
            }

            try
            {
                EnsureRootDirectory();
            }
            catch (Exception exception)
            {
                return SlotResult<SaveSlot>.Failure(SlotErrorCode.StorageUnavailable, exception.Message);
            }

            for (int attempt = 0; attempt < MaxCreateAttempts; attempt++)
            {
                SlotId slotId = SlotId.New();
                try
                {
                    if (fileSystem.FileExists(GetSlotPath(slotId)))
                    {
                        continue;
                    }
                }
                catch (Exception exception)
                {
                    return SlotResult<SaveSlot>.Failure(SlotErrorCode.StorageUnavailable, exception.Message);
                }

                DateTimeOffset createdAtUtc = DateTimeOffset.UtcNow;
                SaveSlotFileModel model = new SaveSlotFileModel
                {
                    schemaVersion = CurrentSchemaVersion,
                    slotId = slotId.ToString(),
                    displayName = normalizedName,
                    createdAtUtc = FormatStoredDate(createdAtUtc),
                    completedMissionIds = new List<string>(),
                    commitLedger = new List<SaveSlotCommitFileModel>()
                };

                try
                {
                    WriteModelAtomic(slotId, model);
                }
                catch (Exception exception)
                {
                    return SlotResult<SaveSlot>.Failure(SlotErrorCode.StorageUnavailable, exception.Message);
                }

                SaveSlot slot = new SaveSlot(
                    slotId,
                    CurrentSchemaVersion,
                    normalizedName,
                    createdAtUtc,
                    new List<string>(),
                    new List<SaveSlotCommit>());
                return SlotResult<SaveSlot>.Success(slot);
            }

            return SlotResult<SaveSlot>.Failure(
                SlotErrorCode.StorageUnavailable,
                "Could not allocate a unique slot id.");
        }

        private SlotResult<Unit> RenameSlotCore(SlotId slotId, string displayName)
        {
            if (slotId.IsEmpty)
            {
                return SlotResult<Unit>.Failure(SlotErrorCode.InvalidSlot, "A non-empty slot id is required.");
            }

            if (!TryNormalizeDisplayName(displayName, out string normalizedName))
            {
                return SlotResult<Unit>.Failure(
                    SlotErrorCode.InvalidName,
                    "The display name must be 1 to 64 characters and must not contain path or control characters.");
            }

            string path = GetSlotPath(slotId);
            if (!TryReadModel(path, out SaveSlotFileModel model, out SlotErrorCode readError, out string readMessage))
            {
                return SlotResult<Unit>.Failure(readError, readMessage);
            }

            if (!TryBuildSlot(model, slotId, out _, out SlotErrorCode error, out string message))
            {
                return SlotResult<Unit>.Failure(error, message);
            }

            model.displayName = normalizedName;
            try
            {
                WriteModelAtomic(slotId, model);
            }
            catch (Exception exception)
            {
                return SlotResult<Unit>.Failure(SlotErrorCode.StorageUnavailable, exception.Message);
            }

            return SlotResult<Unit>.Success(Unit.Value);
        }

        private SlotResult<Unit> DeleteSlotCore(SlotId slotId)
        {
            if (slotId.IsEmpty)
            {
                return SlotResult<Unit>.Failure(SlotErrorCode.InvalidSlot, "A non-empty slot id is required.");
            }

            string path = GetSlotPath(slotId);
            try
            {
                if (!fileSystem.FileExists(path))
                {
                    return SlotResult<Unit>.Failure(SlotErrorCode.SlotNotFound, "The save slot does not exist.");
                }

                fileSystem.Delete(path);
                return SlotResult<Unit>.Success(Unit.Value);
            }
            catch (FileNotFoundException)
            {
                return SlotResult<Unit>.Failure(SlotErrorCode.SlotNotFound, "The save slot does not exist.");
            }
            catch (DirectoryNotFoundException)
            {
                return SlotResult<Unit>.Failure(SlotErrorCode.SlotNotFound, "The save slot does not exist.");
            }
            catch (Exception exception)
            {
                return SlotResult<Unit>.Failure(SlotErrorCode.StorageUnavailable, exception.Message);
            }
        }

        private SlotResult<Unit> CommitMissionCore(MissionCommit commit)
        {
            if (commit.SlotId.IsEmpty)
            {
                return SlotResult<Unit>.Failure(SlotErrorCode.InvalidSlot, "A non-empty slot id is required.");
            }

            if (commit.RoundId == Guid.Empty)
            {
                return SlotResult<Unit>.Failure(SlotErrorCode.InvalidCommit, "A non-empty round id is required.");
            }

            if (!IsValidMissionId(commit.MissionId))
            {
                return SlotResult<Unit>.Failure(
                    SlotErrorCode.InvalidCommit,
                    "A non-empty mission id of at most 128 characters without control characters is required.");
            }

            if (commit.SchemaVersion > CurrentSchemaVersion)
            {
                return SlotResult<Unit>.Failure(
                    SlotErrorCode.UnsupportedSchema,
                    $"Commit schema {commit.SchemaVersion} is newer than supported schema {CurrentSchemaVersion}.");
            }

            if (commit.SchemaVersion != CurrentSchemaVersion)
            {
                return SlotResult<Unit>.Failure(
                    SlotErrorCode.InvalidCommit,
                    $"Commit schema must be {CurrentSchemaVersion}.");
            }

            string path = GetSlotPath(commit.SlotId);
            if (!TryReadModel(path, out SaveSlotFileModel model, out SlotErrorCode readError, out string readMessage))
            {
                return SlotResult<Unit>.Failure(readError, readMessage);
            }

            if (!TryBuildSlot(model, commit.SlotId, out _, out SlotErrorCode error, out string message))
            {
                return SlotResult<Unit>.Failure(error, message);
            }

            DateTimeOffset completedAtUtc = commit.CompletedAtUtc.ToUniversalTime();
            SaveSlotCommitFileModel existing = model.commitLedger.FirstOrDefault(
                entry => Guid.TryParse(entry.roundId, out Guid roundId) && roundId == commit.RoundId);
            if (existing != null)
            {
                if (existing.missionId == commit.MissionId &&
                    TryParseStoredDate(existing.completedAtUtc, out DateTimeOffset existingCompletedAtUtc) &&
                    existingCompletedAtUtc.ToUniversalTime() == completedAtUtc &&
                    existing.schemaVersion == commit.SchemaVersion)
                {
                    return SlotResult<Unit>.Idempotent(Unit.Value);
                }

                return SlotResult<Unit>.Failure(
                    SlotErrorCode.CommitConflict,
                    "The round id was already committed with different content.");
            }

            model.commitLedger.Add(new SaveSlotCommitFileModel
            {
                roundId = commit.RoundId.ToString("D"),
                missionId = commit.MissionId,
                completedAtUtc = FormatStoredDate(completedAtUtc),
                schemaVersion = commit.SchemaVersion
            });

            if (!model.completedMissionIds.Contains(commit.MissionId, StringComparer.Ordinal))
            {
                model.completedMissionIds.Add(commit.MissionId);
            }

            try
            {
                WriteModelAtomic(commit.SlotId, model);
            }
            catch (Exception exception)
            {
                return SlotResult<Unit>.Failure(SlotErrorCode.StorageUnavailable, exception.Message);
            }

            return SlotResult<Unit>.Success(Unit.Value);
        }

        private bool TryReadModel(
            string path,
            out SaveSlotFileModel model,
            out SlotErrorCode error,
            out string message)
        {
            model = null;
            error = SlotErrorCode.None;
            message = string.Empty;

            string json;
            try
            {
                if (!fileSystem.FileExists(path))
                {
                    error = SlotErrorCode.SlotNotFound;
                    message = "The save slot does not exist.";
                    return false;
                }

                json = fileSystem.ReadAllText(path);
            }
            catch (FileNotFoundException)
            {
                error = SlotErrorCode.SlotNotFound;
                message = "The save slot does not exist.";
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                error = SlotErrorCode.SlotNotFound;
                message = "The save slot does not exist.";
                return false;
            }
            catch (Exception exception)
            {
                error = SlotErrorCode.StorageUnavailable;
                message = exception.Message;
                return false;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                error = SlotErrorCode.CorruptData;
                message = "The save slot file is empty.";
                return false;
            }

            try
            {
                model = JsonUtility.FromJson<SaveSlotFileModel>(json);
            }
            catch (Exception exception)
            {
                error = SlotErrorCode.CorruptData;
                message = "The save slot file is not valid JSON: " + exception.Message;
                return false;
            }

            if (model == null)
            {
                error = SlotErrorCode.CorruptData;
                message = "The save slot file does not contain a slot object.";
                return false;
            }

            if (model.schemaVersion > CurrentSchemaVersion)
            {
                error = SlotErrorCode.UnsupportedSchema;
                message = $"Slot schema {model.schemaVersion} is newer than supported schema {CurrentSchemaVersion}.";
                return false;
            }

            return true;
        }

        private static bool TryBuildSlot(
            SaveSlotFileModel model,
            SlotId expectedSlotId,
            out SaveSlot slot,
            out SlotErrorCode error,
            out string message)
        {
            slot = null;
            error = SlotErrorCode.None;
            message = string.Empty;

            if (model == null)
            {
                error = SlotErrorCode.CorruptData;
                message = "The save slot file does not contain a slot object.";
                return false;
            }

            if (model.schemaVersion > CurrentSchemaVersion)
            {
                error = SlotErrorCode.UnsupportedSchema;
                message = $"Slot schema {model.schemaVersion} is newer than supported schema {CurrentSchemaVersion}.";
                return false;
            }

            if (model.schemaVersion != CurrentSchemaVersion)
            {
                error = SlotErrorCode.CorruptData;
                message = $"Slot schema {model.schemaVersion} is not supported.";
                return false;
            }

            if (!SlotId.TryParse(model.slotId, out SlotId storedSlotId) || storedSlotId != expectedSlotId)
            {
                error = SlotErrorCode.CorruptData;
                message = "The stored slot id does not match the slot file name.";
                return false;
            }

            if (!TryNormalizeDisplayName(model.displayName, out string displayName))
            {
                error = SlotErrorCode.CorruptData;
                message = "The stored display name is invalid.";
                return false;
            }

            if (!TryParseStoredDate(model.createdAtUtc, out DateTimeOffset createdAtUtc))
            {
                error = SlotErrorCode.CorruptData;
                message = "The stored creation time is invalid.";
                return false;
            }

            if (model.completedMissionIds == null)
            {
                error = SlotErrorCode.CorruptData;
                message = "The completed mission list is missing.";
                return false;
            }

            if (model.commitLedger == null)
            {
                error = SlotErrorCode.CorruptData;
                message = "The commit ledger is missing.";
                return false;
            }

            List<string> completedMissionIds = new List<string>();
            HashSet<string> completedMissionSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (string missionId in model.completedMissionIds)
            {
                if (!IsValidMissionId(missionId))
                {
                    error = SlotErrorCode.CorruptData;
                    message = "The completed mission list contains an invalid mission id.";
                    return false;
                }

                if (!completedMissionSet.Add(missionId))
                {
                    error = SlotErrorCode.CorruptData;
                    message = "The completed mission list contains a duplicate mission id.";
                    return false;
                }

                completedMissionIds.Add(missionId);
            }

            List<SaveSlotCommit> commitLedger = new List<SaveSlotCommit>();
            HashSet<Guid> committedRounds = new HashSet<Guid>();
            HashSet<string> ledgerMissionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (SaveSlotCommitFileModel entry in model.commitLedger)
            {
                if (entry == null ||
                    !Guid.TryParse(entry.roundId, out Guid roundId) ||
                    roundId == Guid.Empty ||
                    !IsValidMissionId(entry.missionId) ||
                    !TryParseStoredDate(entry.completedAtUtc, out DateTimeOffset completedAtUtc) ||
                    entry.schemaVersion != CurrentSchemaVersion)
                {
                    error = SlotErrorCode.CorruptData;
                    message = "The commit ledger contains an invalid entry.";
                    return false;
                }

                if (!committedRounds.Add(roundId))
                {
                    error = SlotErrorCode.CorruptData;
                    message = "The commit ledger contains a duplicate round id.";
                    return false;
                }

                ledgerMissionIds.Add(entry.missionId);
                commitLedger.Add(new SaveSlotCommit(roundId, entry.missionId, completedAtUtc, entry.schemaVersion));
            }

            if (!completedMissionSet.SetEquals(ledgerMissionIds))
            {
                error = SlotErrorCode.CorruptData;
                message = "The commit ledger does not match the completed mission list.";
                return false;
            }

            slot = new SaveSlot(
                storedSlotId,
                model.schemaVersion,
                displayName,
                createdAtUtc,
                completedMissionIds,
                commitLedger);
            return true;
        }

        private void WriteModelAtomic(SlotId slotId, SaveSlotFileModel model)
        {
            EnsureRootDirectory();
            string destination = GetSlotPath(slotId);
            string tempPath = destination + "." + Guid.NewGuid().ToString("N") + TempExtension;
            try
            {
                string json = JsonUtility.ToJson(model, true);
                fileSystem.WriteAllText(tempPath, json);
                if (fileSystem.FileExists(destination))
                {
                    fileSystem.Replace(tempPath, destination);
                }
                else
                {
                    fileSystem.Move(tempPath, destination);
                }
            }
            finally
            {
                try
                {
                    if (fileSystem.FileExists(tempPath))
                    {
                        fileSystem.Delete(tempPath);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private void EnsureRootDirectory()
        {
            if (!fileSystem.DirectoryExists(rootDirectory))
            {
                fileSystem.CreateDirectory(rootDirectory);
            }
        }

        private static bool TryParseSlotFileName(string fileName, out SlotId slotId)
        {
            slotId = SlotId.Empty;
            if (string.IsNullOrEmpty(fileName) ||
                !fileName.StartsWith(SlotFilePrefix, StringComparison.OrdinalIgnoreCase) ||
                !fileName.EndsWith(SlotFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string token = fileName.Substring(
                SlotFilePrefix.Length,
                fileName.Length - SlotFilePrefix.Length - SlotFileExtension.Length);
            if (token.Length != 32 || !Guid.TryParseExact(token, "N", out Guid parsed) || parsed == Guid.Empty)
            {
                return false;
            }

            slotId = new SlotId(parsed);
            return true;
        }

        private static bool TryNormalizeDisplayName(string value, out string normalizedName)
        {
            normalizedName = (value ?? string.Empty).Trim();
            if (normalizedName.Length == 0 ||
                normalizedName.Length > MaxDisplayNameLength ||
                normalizedName == "." ||
                normalizedName == "..")
            {
                return false;
            }

            foreach (char character in normalizedName)
            {
                if (char.IsControl(character) ||
                    character == '/' ||
                    character == '\\' ||
                    character == ':' ||
                    character == '*' ||
                    character == '?' ||
                    character == '"' ||
                    character == '<' ||
                    character == '>' ||
                    character == '|')
                {
                    normalizedName = string.Empty;
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidMissionId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > MaxMissionIdLength)
            {
                return false;
            }

            foreach (char character in value)
            {
                if (char.IsControl(character))
                {
                    return false;
                }
            }

            return true;
        }

        private static string FormatStoredDate(DateTimeOffset value)
        {
            return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }

        private static bool TryParseStoredDate(string value, out DateTimeOffset result)
        {
            return DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out result);
        }

        private static SaveSlotSummary CreateSummary(
            SlotId slotId,
            SaveSlotFileModel model,
            SaveSlotStatus status,
            SlotErrorCode error,
            string message)
        {
            DateTimeOffset? createdAtUtc = null;
            if (model != null && TryParseStoredDate(model.createdAtUtc, out DateTimeOffset parsedCreatedAtUtc))
            {
                createdAtUtc = parsedCreatedAtUtc.ToUniversalTime();
            }

            return new SaveSlotSummary(
                slotId,
                SafeSummaryName(model?.displayName),
                createdAtUtc,
                model?.completedMissionIds?.Count ?? 0,
                model?.commitLedger?.Count ?? 0,
                model?.schemaVersion ?? 0,
                status,
                error,
                message);
        }

        private static string SafeSummaryName(string value)
        {
            string name = (value ?? string.Empty).Trim();
            return name.Length <= MaxDisplayNameLength ? name : name.Substring(0, MaxDisplayNameLength);
        }

        private static int CompareSummaries(SaveSlotSummary left, SaveSlotSummary right)
        {
            if (left.Status != right.Status)
            {
                if (left.Status == SaveSlotStatus.Ok)
                {
                    return -1;
                }

                if (right.Status == SaveSlotStatus.Ok)
                {
                    return 1;
                }

                return left.Status.CompareTo(right.Status);
            }

            int createdComparison = Nullable.Compare(left.CreatedAtUtc, right.CreatedAtUtc);
            if (createdComparison != 0)
            {
                return createdComparison;
            }

            return left.SlotId.Value.CompareTo(right.SlotId.Value);
        }
    }
}
