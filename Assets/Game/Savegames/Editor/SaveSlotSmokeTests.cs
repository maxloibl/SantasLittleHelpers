using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SantasLittleHelpers.Savegames;
using UnityEngine;

namespace SantasLittleHelpers.Savegames.Editor
{
    internal static class SaveSlotSmokeTests
    {
        private static readonly CancellationToken NoCancellation = CancellationToken.None;

        public static int RunAll(string root)
        {
            Directory.CreateDirectory(root);
            SmokeCase[] cases =
            {
                new SmokeCase("restart-persistence", RestartPersistence),
                new SmokeCase("two-independent-slots", TwoIndependentSlots),
                new SmokeCase("rename-and-delete", RenameAndDelete),
                new SmokeCase("invalid-names-and-ids", InvalidNamesAndIds),
                new SmokeCase("corrupt-and-newer-data", CorruptAndNewerData),
                new SmokeCase("unreadable-slot-report", UnreadableSlotReport),
                new SmokeCase("duplicate-round-commit", DuplicateRoundCommit),
                new SmokeCase("atomic-failure-keeps-last-valid", AtomicFailureKeepsLastValid)
            };

            int passed = 0;
            foreach (SmokeCase smokeCase in cases)
            {
                string testRoot = Path.Combine(root, smokeCase.Name + "-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(testRoot);
                Debug.Log($"SAVE_SLOT_TEST_START name={smokeCase.Name} root={testRoot}");
                try
                {
                    smokeCase.Body(testRoot);
                    passed++;
                    Debug.Log($"SAVE_SLOT_TEST_PASS name={smokeCase.Name}");
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"SAVE_SLOT_TEST_FAIL name={smokeCase.Name} error={exception.GetType().Name} " +
                        $"message={exception.Message}\n{exception.StackTrace}");
                    throw new InvalidOperationException(
                        $"Save slot smoke test '{smokeCase.Name}' failed: {exception.Message}",
                        exception);
                }
            }

            return passed;
        }

        private static void RestartPersistence(string root)
        {
            SaveSlotStore store = new SaveSlotStore(root);
            SaveSlot created = ExpectSuccess(
                Await(store.CreateSlot("  Nordlicht  ", NoCancellation)),
                "create slot");
            AssertEqual("Nordlicht", created.DisplayName, "display name must be trimmed");
            AssertEqual(SaveSlotStore.CurrentSchemaVersion, created.SchemaVersion, "schema version");
            AssertEqual(0, created.CompletedMissionIds.Count, "new slot mission count");

            DateTimeOffset completedAt = Utc(2026, 11, 9, 12, 30, 15);
            MissionCommit commit = CommitFor(Guid.NewGuid(), created.SlotId, "mission-light-gift", completedAt);
            ExpectSuccess(Await(store.CommitMission(commit, NoCancellation)), "first commit");

            SaveSlotStore restarted = new SaveSlotStore(root);
            IReadOnlyList<SaveSlotSummary> summaries = ExpectSuccess(
                Await(restarted.ListSlots(NoCancellation)),
                "list after restart");
            AssertEqual(1, summaries.Count, "slot count after restart");
            AssertEqual(SaveSlotStatus.Ok, summaries[0].Status, "summary status");
            AssertEqual(created.SlotId, summaries[0].SlotId, "summary slot id");
            AssertEqual("Nordlicht", summaries[0].DisplayName, "summary display name");
            AssertEqual(1, summaries[0].CompletedMissionCount, "summary mission count");
            AssertEqual(1, summaries[0].CommitCount, "summary commit count");

            SaveSlot loaded = ExpectSuccess(
                Await(restarted.LoadSlot(created.SlotId, NoCancellation)),
                "load after restart");
            AssertEqual(created.SlotId, loaded.SlotId, "loaded slot id");
            AssertEqual(created.CreatedAtUtc, loaded.CreatedAtUtc, "loaded creation time");
            AssertEqual(1, loaded.CompletedMissionIds.Count, "loaded mission count");
            AssertEqual("mission-light-gift", loaded.CompletedMissionIds[0], "loaded mission id");
            AssertEqual(1, loaded.CommitLedger.Count, "loaded ledger count");
            AssertEqual(commit.RoundId, loaded.CommitLedger[0].RoundId, "loaded round id");
            AssertEqual(completedAt, loaded.CommitLedger[0].CompletedAtUtc, "loaded completion time");

            SlotResult<Unit> duplicate = Await(restarted.CommitMission(commit, NoCancellation));
            AssertTrue(duplicate.IsSuccess, "duplicate commit after restart must succeed");
            AssertTrue(duplicate.IsIdempotent, "duplicate commit after restart must be idempotent");

            SaveSlot afterDuplicate = ExpectSuccess(
                Await(restarted.LoadSlot(created.SlotId, NoCancellation)),
                "load after duplicate commit");
            AssertEqual(1, afterDuplicate.CompletedMissionIds.Count, "duplicate mission id");
            AssertEqual(1, afterDuplicate.CommitLedger.Count, "duplicate ledger entry");
        }

        private static void TwoIndependentSlots(string root)
        {
            SaveSlotStore store = new SaveSlotStore(root);
            SaveSlot first = ExpectSuccess(Await(store.CreateSlot("Eins", NoCancellation)), "create first");
            SaveSlot second = ExpectSuccess(Await(store.CreateSlot("Zwei", NoCancellation)), "create second");
            AssertTrue(first.SlotId != second.SlotId, "slot ids must be independent");
            AssertTrue(store.GetSlotPath(first.SlotId) != store.GetSlotPath(second.SlotId), "slot paths must differ");

            DateTimeOffset completedAt = Utc(2026, 11, 9, 13, 0, 0);
            ExpectSuccess(
                Await(store.CommitMission(CommitFor(Guid.NewGuid(), first.SlotId, "mission-first", completedAt), NoCancellation)),
                "commit first");
            ExpectSuccess(
                Await(store.CommitMission(CommitFor(Guid.NewGuid(), second.SlotId, "mission-second", completedAt), NoCancellation)),
                "commit second");

            SaveSlotStore restarted = new SaveSlotStore(root);
            SaveSlot loadedFirst = ExpectSuccess(Await(restarted.LoadSlot(first.SlotId, NoCancellation)), "load first");
            SaveSlot loadedSecond = ExpectSuccess(Await(restarted.LoadSlot(second.SlotId, NoCancellation)), "load second");
            AssertEqual("Eins", loadedFirst.DisplayName, "first name");
            AssertEqual("Zwei", loadedSecond.DisplayName, "second name");
            AssertEqual(1, loadedFirst.CompletedMissionIds.Count, "first mission count");
            AssertEqual(1, loadedSecond.CompletedMissionIds.Count, "second mission count");
            AssertEqual("mission-first", loadedFirst.CompletedMissionIds[0], "first mission id");
            AssertEqual("mission-second", loadedSecond.CompletedMissionIds[0], "second mission id");

            ExpectSuccess(
                Await(restarted.CommitMission(
                    CommitFor(Guid.NewGuid(), second.SlotId, "mission-second-extra", completedAt),
                    NoCancellation)),
                "commit extra mission to second slot");
            SaveSlot firstAfterSecondCommit = ExpectSuccess(
                Await(restarted.LoadSlot(first.SlotId, NoCancellation)),
                "reload first");
            AssertEqual(1, firstAfterSecondCommit.CompletedMissionIds.Count, "first slot must remain untouched");
        }

        private static void RenameAndDelete(string root)
        {
            SaveSlotStore store = new SaveSlotStore(root);
            SaveSlot created = ExpectSuccess(Await(store.CreateSlot("Alt", NoCancellation)), "create");
            DateTimeOffset completedAt = Utc(2026, 11, 9, 14, 0, 0);
            ExpectSuccess(
                Await(store.CommitMission(CommitFor(Guid.NewGuid(), created.SlotId, "mission-rename", completedAt), NoCancellation)),
                "commit before rename");

            ExpectSuccess(Await(store.RenameSlot(created.SlotId, "  Neu  ", NoCancellation)), "rename");
            SaveSlotStore restarted = new SaveSlotStore(root);
            SaveSlot renamed = ExpectSuccess(Await(restarted.LoadSlot(created.SlotId, NoCancellation)), "load renamed");
            AssertEqual("Neu", renamed.DisplayName, "renamed display name");
            AssertEqual(created.CreatedAtUtc, renamed.CreatedAtUtc, "rename must keep creation time");
            AssertEqual(1, renamed.CompletedMissionIds.Count, "rename must keep missions");
            AssertEqual("mission-rename", renamed.CompletedMissionIds[0], "rename must keep mission id");

            IReadOnlyList<SaveSlotSummary> summaries = ExpectSuccess(
                Await(restarted.ListSlots(NoCancellation)),
                "list renamed");
            AssertEqual(1, summaries.Count, "renamed slot count");
            AssertEqual("Neu", summaries[0].DisplayName, "renamed summary name");

            ExpectFailure(
                Await(restarted.RenameSlot(created.SlotId, "bad/name", NoCancellation)),
                SlotErrorCode.InvalidName,
                "invalid rename");
            SaveSlot stillRenamed = ExpectSuccess(
                Await(restarted.LoadSlot(created.SlotId, NoCancellation)),
                "load after invalid rename");
            AssertEqual("Neu", stillRenamed.DisplayName, "invalid rename must not change the name");

            ExpectSuccess(Await(restarted.DeleteSlot(created.SlotId, NoCancellation)), "delete");
            IReadOnlyList<SaveSlotSummary> afterDelete = ExpectSuccess(
                Await(restarted.ListSlots(NoCancellation)),
                "list after delete");
            AssertEqual(0, afterDelete.Count, "slot count after delete");
            ExpectFailure(
                Await(restarted.LoadSlot(created.SlotId, NoCancellation)),
                SlotErrorCode.SlotNotFound,
                "load deleted slot");
            ExpectFailure(
                Await(restarted.DeleteSlot(created.SlotId, NoCancellation)),
                SlotErrorCode.SlotNotFound,
                "delete twice");
        }

        private static void InvalidNamesAndIds(string root)
        {
            Directory.CreateDirectory(root);
            SaveSlotStore store = new SaveSlotStore(root);
            string[] invalidNames =
            {
                null,
                string.Empty,
                "   ",
                ".",
                "..",
                "../escape",
                "..\\escape",
                "bad/name",
                "bad\\name",
                "bad:name",
                "bad*name",
                "bad?name",
                "bad\"name",
                "bad<name",
                "bad>name",
                "bad|name",
                "line\nbreak",
                new string('x', SaveSlotStore.MaxDisplayNameLength + 1)
            };

            foreach (string invalidName in invalidNames)
            {
                ExpectFailure(
                    Await(store.CreateSlot(invalidName, NoCancellation)),
                    SlotErrorCode.InvalidName,
                    "invalid name " + (invalidName ?? "<null>"));
            }

            AssertEqual(0, Directory.GetFiles(root, "slot-*.slotsave").Length, "invalid names must not create slot files");

            ExpectFailure(
                Await(store.LoadSlot(default(SlotId), NoCancellation)),
                SlotErrorCode.InvalidSlot,
                "load empty slot id");
            ExpectFailure(
                Await(store.LoadSlot(SlotId.New(), NoCancellation)),
                SlotErrorCode.SlotNotFound,
                "load missing slot id");
            ExpectFailure(
                Await(store.RenameSlot(default(SlotId), "Name", NoCancellation)),
                SlotErrorCode.InvalidSlot,
                "rename empty slot id");
            ExpectFailure(
                Await(store.DeleteSlot(default(SlotId), NoCancellation)),
                SlotErrorCode.InvalidSlot,
                "delete empty slot id");

            ExpectFailure(
                Await(store.CommitMission(
                    new MissionCommit(Guid.NewGuid(), default(SlotId), "mission", Utc(2026, 11, 9, 0, 0, 0), SaveSlotStore.CurrentSchemaVersion),
                    NoCancellation)),
                SlotErrorCode.InvalidSlot,
                "commit empty slot id");
            SaveSlot created = ExpectSuccess(Await(store.CreateSlot("Gueltig", NoCancellation)), "create valid slot");
            ExpectFailure(
                Await(store.CommitMission(
                    new MissionCommit(Guid.Empty, created.SlotId, "mission", Utc(2026, 11, 9, 0, 0, 0), SaveSlotStore.CurrentSchemaVersion),
                    NoCancellation)),
                SlotErrorCode.InvalidCommit,
                "commit empty round id");
            ExpectFailure(
                Await(store.CommitMission(
                    new MissionCommit(Guid.NewGuid(), created.SlotId, " ", Utc(2026, 11, 9, 0, 0, 0), SaveSlotStore.CurrentSchemaVersion),
                    NoCancellation)),
                SlotErrorCode.InvalidCommit,
                "commit empty mission id");
            ExpectFailure(
                Await(store.CommitMission(
                    new MissionCommit(
                        Guid.NewGuid(),
                        created.SlotId,
                        new string('m', SaveSlotStore.MaxMissionIdLength + 1),
                        Utc(2026, 11, 9, 0, 0, 0),
                        SaveSlotStore.CurrentSchemaVersion),
                    NoCancellation)),
                SlotErrorCode.InvalidCommit,
                "commit oversized mission id");

            AssertFalse(SlotId.TryParse("../escape", out _), "path text must not parse as slot id");
            AssertFalse(SlotId.TryParse(string.Empty, out _), "empty text must not parse as slot id");
            AssertFalse(
                SlotId.TryParse("00000000-0000-0000-0000-000000000000", out _),
                "empty UUID must not parse as slot id");
            AssertThrows<ArgumentException>(() => store.GetSlotPath(default(SlotId)), "empty slot path");
        }

        private static void CorruptAndNewerData(string root)
        {
            Directory.CreateDirectory(root);
            SaveSlotStore store = new SaveSlotStore(root);

            SlotId corruptId = SlotId.New();
            string corruptPath = store.GetSlotPath(corruptId);
            File.WriteAllText(corruptPath, "{ this is not valid json", new UTF8Encoding(false));
            byte[] corruptBefore = File.ReadAllBytes(corruptPath);

            IReadOnlyList<SaveSlotSummary> corruptList = ExpectSuccess(
                Await(store.ListSlots(NoCancellation)),
                "list corrupt");
            SaveSlotSummary corruptSummary = FindSummary(corruptList, corruptId);
            AssertEqual(SaveSlotStatus.CorruptData, corruptSummary.Status, "corrupt status");
            AssertEqual(SlotErrorCode.CorruptData, corruptSummary.Error, "corrupt error");
            AssertSequenceEqual(corruptBefore, File.ReadAllBytes(corruptPath), "list must not rewrite corrupt slot");
            ExpectFailure(
                Await(store.LoadSlot(corruptId, NoCancellation)),
                SlotErrorCode.CorruptData,
                "load corrupt slot");

            SlotId newerId = SlotId.New();
            string newerPath = store.GetSlotPath(newerId);
            string newerJson =
                "{\"schemaVersion\":" + (SaveSlotStore.CurrentSchemaVersion + 1).ToString(CultureInfo.InvariantCulture) +
                ",\"slotId\":\"" + newerId.ToString() +
                "\",\"displayName\":\"Zukunft\",\"createdAtUtc\":\"" +
                Utc(2026, 11, 9, 15, 0, 0).ToString("O", CultureInfo.InvariantCulture) +
                "\",\"completedMissionIds\":[],\"commitLedger\":[]}";
            File.WriteAllText(newerPath, newerJson, new UTF8Encoding(false));
            byte[] newerBefore = File.ReadAllBytes(newerPath);

            IReadOnlyList<SaveSlotSummary> newerList = ExpectSuccess(
                Await(store.ListSlots(NoCancellation)),
                "list newer");
            SaveSlotSummary newerSummary = FindSummary(newerList, newerId);
            AssertEqual(SaveSlotStatus.UnsupportedSchema, newerSummary.Status, "newer status");
            AssertEqual(SlotErrorCode.UnsupportedSchema, newerSummary.Error, "newer error");
            AssertEqual("Zukunft", newerSummary.DisplayName, "newer display name");
            AssertSequenceEqual(newerBefore, File.ReadAllBytes(newerPath), "list must not rewrite newer slot");
            ExpectFailure(
                Await(store.LoadSlot(newerId, NoCancellation)),
                SlotErrorCode.UnsupportedSchema,
                "load newer slot");
            ExpectFailure(
                Await(store.CommitMission(
                    CommitFor(Guid.NewGuid(), newerId, "mission-newer", Utc(2026, 11, 9, 15, 0, 0)),
                    NoCancellation)),
                SlotErrorCode.UnsupportedSchema,
                "commit newer slot");
            ExpectFailure(
                Await(store.RenameSlot(newerId, "Umbau", NoCancellation)),
                SlotErrorCode.UnsupportedSchema,
                "rename newer slot");
            AssertSequenceEqual(newerBefore, File.ReadAllBytes(newerPath), "mutations must not rewrite newer slot");

            SlotId mismatchId = SlotId.New();
            string mismatchJson =
                "{\"schemaVersion\":1,\"slotId\":\"" + mismatchId.ToString() +
                "\",\"displayName\":\"Mismatch\",\"createdAtUtc\":\"" +
                Utc(2026, 11, 9, 16, 0, 0).ToString("O", CultureInfo.InvariantCulture) +
                "\",\"completedMissionIds\":[\"mission-a\"],\"commitLedger\":[]}";
            File.WriteAllText(store.GetSlotPath(mismatchId), mismatchJson, new UTF8Encoding(false));
            ExpectFailure(
                Await(store.LoadSlot(mismatchId, NoCancellation)),
                SlotErrorCode.CorruptData,
                "load ledger mismatch");

            SlotId duplicateId = SlotId.New();
            string duplicateJson =
                "{\"schemaVersion\":1,\"slotId\":\"" + duplicateId.ToString() +
                "\",\"displayName\":\"Duplicate\",\"createdAtUtc\":\"" +
                Utc(2026, 11, 9, 16, 0, 0).ToString("O", CultureInfo.InvariantCulture) +
                "\",\"completedMissionIds\":[\"mission-a\",\"mission-a\"],\"commitLedger\":[]}";
            File.WriteAllText(store.GetSlotPath(duplicateId), duplicateJson, new UTF8Encoding(false));
            ExpectFailure(
                Await(store.LoadSlot(duplicateId, NoCancellation)),
                SlotErrorCode.CorruptData,
                "load duplicate mission ids");
        }

        private static void UnreadableSlotReport(string root)
        {
            Directory.CreateDirectory(root);
            SaveSlotStore setupStore = new SaveSlotStore(root);
            SaveSlot created = ExpectSuccess(Await(setupStore.CreateSlot("Unlesbar", NoCancellation)), "create");

            FaultInjectingFileSystem faulting = new FaultInjectingFileSystem(new DefaultSaveSlotFileSystem())
            {
                FailReads = true
            };
            SaveSlotStore readOnlyStore = new SaveSlotStore(root, faulting);
            IReadOnlyList<SaveSlotSummary> summaries = ExpectSuccess(
                Await(readOnlyStore.ListSlots(NoCancellation)),
                "list unreadable");
            AssertEqual(1, summaries.Count, "unreadable slot count");
            AssertEqual(created.SlotId, summaries[0].SlotId, "unreadable slot id");
            AssertEqual(SaveSlotStatus.Unreadable, summaries[0].Status, "unreadable status");
            AssertEqual(SlotErrorCode.StorageUnavailable, summaries[0].Error, "unreadable error");
            ExpectFailure(
                Await(readOnlyStore.LoadSlot(created.SlotId, NoCancellation)),
                SlotErrorCode.StorageUnavailable,
                "load unreadable slot");
        }

        private static void DuplicateRoundCommit(string root)
        {
            SaveSlotStore store = new SaveSlotStore(root);
            SaveSlot created = ExpectSuccess(Await(store.CreateSlot("Runden", NoCancellation)), "create");
            DateTimeOffset completedAt = Utc(2026, 11, 9, 17, 0, 0);
            Guid roundId = Guid.NewGuid();
            MissionCommit commit = CommitFor(roundId, created.SlotId, "mission-a", completedAt);

            SlotResult<Unit> first = Await(store.CommitMission(commit, NoCancellation));
            AssertTrue(first.IsSuccess, "first round commit");
            AssertFalse(first.IsIdempotent, "first round commit must not be idempotent");

            SlotResult<Unit> duplicate = Await(store.CommitMission(commit, NoCancellation));
            AssertTrue(duplicate.IsSuccess, "duplicate round commit must succeed");
            AssertTrue(duplicate.IsIdempotent, "duplicate round commit must be idempotent");

            SaveSlot afterDuplicate = ExpectSuccess(Await(store.LoadSlot(created.SlotId, NoCancellation)), "load duplicate");
            AssertEqual(1, afterDuplicate.CompletedMissionIds.Count, "duplicate commit mission count");
            AssertEqual(1, afterDuplicate.CommitLedger.Count, "duplicate commit ledger count");

            ExpectFailure(
                Await(store.CommitMission(
                    CommitFor(roundId, created.SlotId, "mission-a", completedAt.AddSeconds(1)),
                    NoCancellation)),
                SlotErrorCode.CommitConflict,
                "same round different time");
            ExpectFailure(
                Await(store.CommitMission(
                    CommitFor(roundId, created.SlotId, "mission-b", completedAt),
                    NoCancellation)),
                SlotErrorCode.CommitConflict,
                "same round different mission");

            SaveSlot afterConflict = ExpectSuccess(Await(store.LoadSlot(created.SlotId, NoCancellation)), "load after conflict");
            AssertEqual(1, afterConflict.CompletedMissionIds.Count, "conflict mission count");
            AssertEqual(1, afterConflict.CommitLedger.Count, "conflict ledger count");

            Guid secondRoundId = Guid.NewGuid();
            ExpectSuccess(
                Await(store.CommitMission(
                    CommitFor(secondRoundId, created.SlotId, "mission-a", completedAt.AddMinutes(1)),
                    NoCancellation)),
                "new round same mission");
            SaveSlot afterSecondRound = ExpectSuccess(Await(store.LoadSlot(created.SlotId, NoCancellation)), "load second round");
            AssertEqual(1, afterSecondRound.CompletedMissionIds.Count, "same mission must not be duplicated");
            AssertEqual(2, afterSecondRound.CommitLedger.Count, "ledger must keep both rounds");

            Guid thirdRoundId = Guid.NewGuid();
            ExpectSuccess(
                Await(store.CommitMission(
                    CommitFor(thirdRoundId, created.SlotId, "mission-c", completedAt.AddMinutes(2)),
                    NoCancellation)),
                "new round new mission");
            SaveSlot afterThirdRound = ExpectSuccess(Await(store.LoadSlot(created.SlotId, NoCancellation)), "load third round");
            AssertEqual(2, afterThirdRound.CompletedMissionIds.Count, "new mission must be added");
            AssertEqual(3, afterThirdRound.CommitLedger.Count, "ledger must keep all rounds");

            DateTimeOffset shifted = completedAt.ToOffset(TimeSpan.FromHours(2));
            Guid offsetRoundId = Guid.NewGuid();
            ExpectSuccess(
                Await(store.CommitMission(
                    CommitFor(offsetRoundId, created.SlotId, "mission-offset", completedAt),
                    NoCancellation)),
                "offset round first");
            SlotResult<Unit> sameInstant = Await(store.CommitMission(
                CommitFor(offsetRoundId, created.SlotId, "mission-offset", shifted),
                NoCancellation));
            AssertTrue(sameInstant.IsSuccess && sameInstant.IsIdempotent, "same instant with another offset must be idempotent");
        }

        private static void AtomicFailureKeepsLastValid(string root)
        {
            SaveSlotStore store = new SaveSlotStore(root);
            SaveSlot created = ExpectSuccess(Await(store.CreateSlot("Atomar", NoCancellation)), "create");
            DateTimeOffset completedAt = Utc(2026, 11, 9, 18, 0, 0);
            ExpectSuccess(
                Await(store.CommitMission(
                    CommitFor(Guid.NewGuid(), created.SlotId, "mission-valid", completedAt),
                    NoCancellation)),
                "commit valid");
            string slotPath = store.GetSlotPath(created.SlotId);
            byte[] validBytes = File.ReadAllBytes(slotPath);

            FaultInjectingFileSystem faulting = new FaultInjectingFileSystem(new DefaultSaveSlotFileSystem());
            SaveSlotStore faultingStore = new SaveSlotStore(root, faulting);

            faulting.FailNextReplace = true;
            ExpectFailure(
                Await(faultingStore.CommitMission(
                    CommitFor(Guid.NewGuid(), created.SlotId, "mission-replace-failure", completedAt.AddMinutes(1)),
                    NoCancellation)),
                SlotErrorCode.StorageUnavailable,
                "replace failure");
            AssertSequenceEqual(validBytes, File.ReadAllBytes(slotPath), "replace failure must keep last valid bytes");
            AssertEqual(0, CountTempFiles(root), "replace failure must remove the temp file");

            SaveSlot afterReplaceFailure = ExpectSuccess(Await(store.LoadSlot(created.SlotId, NoCancellation)), "load after replace failure");
            AssertEqual(1, afterReplaceFailure.CompletedMissionIds.Count, "replace failure mission count");
            AssertEqual("mission-valid", afterReplaceFailure.CompletedMissionIds[0], "replace failure mission id");

            faulting.FailWriteAfterInner = true;
            ExpectFailure(
                Await(faultingStore.CommitMission(
                    CommitFor(Guid.NewGuid(), created.SlotId, "mission-write-failure", completedAt.AddMinutes(2)),
                    NoCancellation)),
                SlotErrorCode.StorageUnavailable,
                "write failure");
            AssertSequenceEqual(validBytes, File.ReadAllBytes(slotPath), "write failure must keep last valid bytes");
            AssertEqual(0, CountTempFiles(root), "write failure must remove the temp file");

            faulting.RecordReplacePaths = true;
            ExpectSuccess(
                Await(faultingStore.CommitMission(
                    CommitFor(Guid.NewGuid(), created.SlotId, "mission-replace-success", completedAt.AddMinutes(3)),
                    NoCancellation)),
                "successful replacement");
            AssertEqual(
                faulting.LastReplaceSourceDirectory,
                faulting.LastReplaceDestinationDirectory,
                "temp file must be on the destination volume");

            faulting.FailNextMove = true;
            int beforeFailedCreate = Directory.GetFiles(root, "slot-*.slotsave").Length;
            ExpectFailure(
                Await(faultingStore.CreateSlot("Fehlschlag", NoCancellation)),
                SlotErrorCode.StorageUnavailable,
                "create failure");
            AssertEqual(
                beforeFailedCreate,
                Directory.GetFiles(root, "slot-*.slotsave").Length,
                "failed create must not leave a slot file");
            AssertEqual(0, CountTempFiles(root), "failed create must remove the temp file");
        }

        private static MissionCommit CommitFor(Guid roundId, SlotId slotId, string missionId, DateTimeOffset completedAt)
        {
            return new MissionCommit(
                roundId,
                slotId,
                missionId,
                completedAt,
                SaveSlotStore.CurrentSchemaVersion);
        }

        private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute, int second)
        {
            return new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero);
        }

        private static SlotResult<T> Await<T>(Task<SlotResult<T>> task)
        {
            return task.GetAwaiter().GetResult();
        }

        private static T ExpectSuccess<T>(SlotResult<T> result, string context)
        {
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"{context} failed with {result.Error}: {result.ErrorMessage}");
            }

            return result.Value;
        }

        private static void ExpectFailure<T>(SlotResult<T> result, SlotErrorCode expected, string context)
        {
            if (result.IsSuccess)
            {
                throw new InvalidOperationException($"{context} unexpectedly succeeded.");
            }

            if (result.Error != expected)
            {
                throw new InvalidOperationException(
                    $"{context} failed with {result.Error} instead of {expected}: {result.ErrorMessage}");
            }
        }

        private static SaveSlotSummary FindSummary(IReadOnlyList<SaveSlotSummary> summaries, SlotId slotId)
        {
            foreach (SaveSlotSummary summary in summaries)
            {
                if (summary.SlotId == slotId)
                {
                    return summary;
                }
            }

            throw new InvalidOperationException($"Slot {slotId} is missing from the list result.");
        }

        private static int CountTempFiles(string root)
        {
            return Directory.GetFiles(root, "*", SearchOption.TopDirectoryOnly)
                .Count(path => path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void AssertFalse(bool condition, string message)
        {
            if (condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException($"{message}: expected '{expected}', actual '{actual}'.");
            }
        }

        private static void AssertSequenceEqual(byte[] expected, byte[] actual, string message)
        {
            if (!expected.SequenceEqual(actual))
            {
                throw new InvalidOperationException($"{message}: byte content changed.");
            }
        }

        private static void AssertThrows<TException>(Action action, string message) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException($"{message}: expected {typeof(TException).Name}.");
        }

        private sealed class SmokeCase
        {
            public SmokeCase(string name, Action<string> body)
            {
                Name = name;
                Body = body;
            }

            public string Name { get; }

            public Action<string> Body { get; }
        }

        private sealed class FaultInjectingFileSystem : ISaveSlotFileSystem
        {
            private readonly ISaveSlotFileSystem inner;

            public FaultInjectingFileSystem(ISaveSlotFileSystem inner)
            {
                this.inner = inner;
            }

            public bool FailReads { get; set; }

            public bool FailNextReplace { get; set; }

            public bool FailWriteAfterInner { get; set; }

            public bool FailNextMove { get; set; }

            public bool RecordReplacePaths { get; set; }

            public string LastReplaceSourceDirectory { get; private set; }

            public string LastReplaceDestinationDirectory { get; private set; }

            public bool DirectoryExists(string path)
            {
                return inner.DirectoryExists(path);
            }

            public void CreateDirectory(string path)
            {
                inner.CreateDirectory(path);
            }

            public IEnumerable<string> EnumerateFiles(string path, string searchPattern)
            {
                return inner.EnumerateFiles(path, searchPattern);
            }

            public bool FileExists(string path)
            {
                return inner.FileExists(path);
            }

            public string ReadAllText(string path)
            {
                if (FailReads)
                {
                    throw new IOException("Injected read failure.");
                }

                return inner.ReadAllText(path);
            }

            public void WriteAllText(string path, string contents)
            {
                if (FailWriteAfterInner)
                {
                    FailWriteAfterInner = false;
                    inner.WriteAllText(path, contents);
                    throw new IOException("Injected write failure after writing the temp file.");
                }

                inner.WriteAllText(path, contents);
            }

            public void Replace(string sourceFileName, string destinationFileName)
            {
                if (RecordReplacePaths)
                {
                    LastReplaceSourceDirectory = Path.GetDirectoryName(sourceFileName);
                    LastReplaceDestinationDirectory = Path.GetDirectoryName(destinationFileName);
                }

                if (FailNextReplace)
                {
                    FailNextReplace = false;
                    throw new IOException("Injected replace failure.");
                }

                inner.Replace(sourceFileName, destinationFileName);
            }

            public void Move(string sourceFileName, string destinationFileName)
            {
                if (FailNextMove)
                {
                    FailNextMove = false;
                    throw new IOException("Injected move failure.");
                }

                inner.Move(sourceFileName, destinationFileName);
            }

            public void Delete(string path)
            {
                inner.Delete(path);
            }
        }
    }
}
