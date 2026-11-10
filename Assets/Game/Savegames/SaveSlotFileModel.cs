using System;
using System.Collections.Generic;

namespace SantasLittleHelpers.Savegames
{
    [Serializable]
    internal sealed class SaveSlotFileModel
    {
        public int schemaVersion;
        public string slotId;
        public string displayName;
        public string createdAtUtc;
        public List<string> completedMissionIds;
        public List<SaveSlotCommitFileModel> commitLedger;
    }

    [Serializable]
    internal sealed class SaveSlotCommitFileModel
    {
        public string roundId;
        public string missionId;
        public string completedAtUtc;
        public int schemaVersion;
    }
}
