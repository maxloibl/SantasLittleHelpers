using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SantasLittleHelpers.Savegames
{
    public enum SaveSlotStatus
    {
        Ok = 0,
        CorruptData,
        UnsupportedSchema,
        Unreadable
    }

    public sealed class SaveSlotCommit
    {
        public SaveSlotCommit(Guid roundId, string missionId, DateTimeOffset completedAtUtc, int schemaVersion)
        {
            RoundId = roundId;
            MissionId = missionId ?? string.Empty;
            CompletedAtUtc = completedAtUtc.ToUniversalTime();
            SchemaVersion = schemaVersion;
        }

        public Guid RoundId { get; }

        public string MissionId { get; }

        public DateTimeOffset CompletedAtUtc { get; }

        public int SchemaVersion { get; }
    }

    public sealed class SaveSlot
    {
        internal SaveSlot(
            SlotId slotId,
            int schemaVersion,
            string displayName,
            DateTimeOffset createdAtUtc,
            IList<string> completedMissionIds,
            IList<SaveSlotCommit> commitLedger)
        {
            SlotId = slotId;
            SchemaVersion = schemaVersion;
            DisplayName = displayName ?? string.Empty;
            CreatedAtUtc = createdAtUtc.ToUniversalTime();
            CompletedMissionIds = new ReadOnlyCollection<string>(completedMissionIds);
            CommitLedger = new ReadOnlyCollection<SaveSlotCommit>(commitLedger);
        }

        public SlotId SlotId { get; }

        public int SchemaVersion { get; }

        public string DisplayName { get; }

        public DateTimeOffset CreatedAtUtc { get; }

        public IReadOnlyList<string> CompletedMissionIds { get; }

        public IReadOnlyList<SaveSlotCommit> CommitLedger { get; }
    }

    public sealed class SaveSlotSummary
    {
        internal SaveSlotSummary(
            SlotId slotId,
            string displayName,
            DateTimeOffset? createdAtUtc,
            int completedMissionCount,
            int commitCount,
            int schemaVersion,
            SaveSlotStatus status,
            SlotErrorCode error,
            string errorMessage)
        {
            SlotId = slotId;
            DisplayName = displayName ?? string.Empty;
            CreatedAtUtc = createdAtUtc;
            CompletedMissionCount = completedMissionCount;
            CommitCount = commitCount;
            SchemaVersion = schemaVersion;
            Status = status;
            Error = error;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public SlotId SlotId { get; }

        public string DisplayName { get; }

        public DateTimeOffset? CreatedAtUtc { get; }

        public int CompletedMissionCount { get; }

        public int CommitCount { get; }

        public int SchemaVersion { get; }

        public SaveSlotStatus Status { get; }

        public SlotErrorCode Error { get; }

        public SlotErrorCode ErrorCode => Error;

        public string ErrorMessage { get; }

        public bool IsLoadable => Status == SaveSlotStatus.Ok;
    }

    public readonly struct MissionCommit
    {
        public MissionCommit(
            Guid roundId,
            SlotId slotId,
            string missionId,
            DateTimeOffset completedAtUtc,
            int schemaVersion)
        {
            RoundId = roundId;
            SlotId = slotId;
            MissionId = missionId ?? string.Empty;
            CompletedAtUtc = completedAtUtc;
            SchemaVersion = schemaVersion;
        }

        public Guid RoundId { get; }

        public SlotId SlotId { get; }

        public string MissionId { get; }

        public DateTimeOffset CompletedAtUtc { get; }

        public int SchemaVersion { get; }
    }
}
