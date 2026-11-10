using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SantasLittleHelpers.Savegames;
using SlotUnit = SantasLittleHelpers.Savegames.Unit;
using SettingsUnit = SantasLittleHelpers.Settings.Unit;
using SantasLittleHelpers.Settings;
using UnityEngine;

namespace SantasLittleHelpers.Home.Editor
{
    public static class HomeMenuSmokeTests
    {
        public static int RunAll(string root)
        {
            Directory.CreateDirectory(root);
            int cases = 0;

            RunEmptyState(root, ref cases);
            RunSlotLifecycle(root, ref cases);
            RunInvalidRecords(root, ref cases);
            RunLaunchRequests(root, ref cases);
            RunJoinValidation(root, ref cases);
            RunConcurrentLaunchGuard(root, ref cases);
            RunSettingsDraftFlow(root, ref cases);

            return cases;
        }

        private static void RunEmptyState(string root, ref int cases)
        {
            HomeMenuModel model = CreateModel(root, "empty", out _);
            Await(model.InitializeAsync());

            Require(model.Slots.Count == 0, "Empty store must list no slots.");
            Require(!model.SelectedSlotId.HasValue, "Empty store must not select a slot.");
            Require(!model.CanLaunch, "Empty store must block Solo/Host.");
            Require(model.StatusMessage.Contains("keine Spielstände", StringComparison.OrdinalIgnoreCase),
                "Empty state must explain that no save exists.");
            cases++;
        }

        private static void RunSlotLifecycle(string root, ref int cases)
        {
            HomeMenuModel model = CreateModel(root, "lifecycle", out SaveSlotStore store);
            Await(model.InitializeAsync());

            SlotResult<SaveSlot> created = Await(model.CreateSlotAsync("  Nordlicht  "));
            Require(created.IsSuccess, "Creating a slot through the model must succeed.");
            SaveSlotIdEquals(created.Value.SlotId, model.SelectedSlotId, "Created slot must be selected.");
            Require(model.Slots.Count == 1 && model.Slots[0].DisplayName == "Nordlicht",
                "Created slot must appear in the real store listing.");
            Require(model.HasLoadableSelection, "Created slot must be loadable.");

            SlotResult<SlotUnit> renamed = Await(model.RenameSelectedAsync("Nordlicht 2"));
            Require(renamed.IsSuccess, "Renaming the selected slot must succeed.");
            Require(model.Slots.Single().DisplayName == "Nordlicht 2", "Rename must be persisted.");

            bool loaded = Await(model.LoadSelectedAsync());
            Require(loaded && model.LoadedSlot != null, "Loading the selected host slot must succeed.");
            Require(model.LoadedSlot.CompletedMissionIds.Count == 0, "Fresh slot must have no completed missions.");

            model.BeginDeleteConfirmation();
            Require(model.IsDeleteConfirmationOpen, "Delete must require explicit confirmation.");
            Require(Await(store.ListSlots(default)).Value.Count == 1,
                "Opening delete confirmation must not delete the slot.");

            SlotResult<SlotUnit> deleted = Await(model.ConfirmDeleteAsync());
            Require(deleted.IsSuccess, "Confirmed delete must succeed.");
            Require(!model.IsDeleteConfirmationOpen, "Confirmation must close after delete.");
            Require(Await(store.ListSlots(default)).Value.Count == 0, "Deleted slot must be gone from disk.");
            cases++;
        }

        private static void RunInvalidRecords(string root, ref int cases)
        {
            HomeMenuModel model = CreateModel(root, "invalid", out SaveSlotStore store);
            Await(model.InitializeAsync());

            SlotResult<SaveSlot> valid = Await(store.CreateSlot("Gültig", default));
            SlotResult<SaveSlot> corrupt = Await(store.CreateSlot("Beschädigt", default));
            SlotResult<SaveSlot> incompatible = Await(store.CreateSlot("Zukunft", default));
            Require(valid.IsSuccess && corrupt.IsSuccess && incompatible.IsSuccess, "Test slot setup must succeed.");

            File.WriteAllText(store.GetSlotPath(corrupt.Value.SlotId), "this is not a slot document");
            File.WriteAllText(store.GetSlotPath(incompatible.Value.SlotId),
                "{\"schemaVersion\":99,\"slotId\":\"" + incompatible.Value.SlotId + "\"}");

            Await(model.RefreshSlotsAsync());
            SaveSlotSummary corruptSummary = model.Slots.Single(slot => slot.SlotId == corrupt.Value.SlotId);
            SaveSlotSummary incompatibleSummary = model.Slots.Single(slot => slot.SlotId == incompatible.Value.SlotId);
            Require(corruptSummary.Status == SaveSlotStatus.CorruptData,
                "Corrupt slot must be reported as CorruptData.");
            Require(incompatibleSummary.Status == SaveSlotStatus.UnsupportedSchema,
                "Future schema must be reported as UnsupportedSchema per record.");
            Require(model.Slots.Count == 3, "Corrupt records must remain visible instead of silently disappearing.");

            Await(model.SelectSlotAsync(corrupt.Value.SlotId));
            Require(!model.HasLoadableSelection, "A corrupt slot must not be launchable.");
            int launches = 0;
            model.SetLauncher(request =>
            {
                launches++;
                return Task.FromResult(HomeLaunchResult.Success());
            });
            HomeLaunchResult blocked = Await(model.LaunchSoloAsync());
            Require(blocked.IsFailure && blocked.Error == HomeLaunchErrorCode.InvalidSlot,
                "Solo must be blocked with InvalidSlot for corrupt records.");
            Require(launches == 0, "A corrupt slot must never reach the launcher.");
            Require(model.StatusMessage.Contains("Beschädigt"), "Corrupt error must be visible in the UI status.");
            cases++;
        }

        private static void RunLaunchRequests(string root, ref int cases)
        {
            HomeMenuModel model = CreateModel(root, "launch", out SaveSlotStore store);
            Await(model.InitializeAsync());
            SlotResult<SaveSlot> slot = Await(store.CreateSlot("Host-Slot", default));
            Await(model.RefreshSlotsAsync());
            Await(model.SelectSlotAsync(slot.Value.SlotId));

            List<HomeLaunchRequest> requests = new List<HomeLaunchRequest>();
            model.SetLauncher(request =>
            {
                requests.Add(request);
                return Task.FromResult(HomeLaunchResult.Success("Bereit"));
            });

            HomeLaunchResult solo = Await(model.LaunchSoloAsync());
            Require(solo.IsSuccess, "Solo launch from a valid selected slot must succeed.");
            Require(requests.Count == 1 && requests[0].Kind == HomeLaunchKind.Solo,
                "Solo request must be typed as Solo.");
            Require(requests[0].SlotId == slot.Value.SlotId.Value, "Solo request must carry the selected slot id.");

            HomeLaunchResult host = Await(model.LaunchHostAsync());
            Require(host.IsSuccess, "Host launch from a valid selected slot must succeed.");
            Require(requests.Count == 2 && requests[1].Kind == HomeLaunchKind.Host,
                "Host request must be typed as Host.");
            Require(requests[1].SlotId == slot.Value.SlotId.Value, "Host request must carry the selected slot id.");
            Require(requests[1].Port == 7777, "Host must use the configured UDP port.");
            cases++;
        }

        private static void RunJoinValidation(string root, ref int cases)
        {
            HomeMenuModel model = CreateModel(root, "join", out _);
            Await(model.InitializeAsync());
            List<HomeLaunchRequest> requests = new List<HomeLaunchRequest>();
            model.SetLauncher(request =>
            {
                requests.Add(request);
                return Task.FromResult(HomeLaunchResult.Success());
            });

            model.SetEndpointText("localhost");
            model.SetPortText("7777");
            HomeLaunchResult rejectedName = Await(model.LaunchJoinAsync());
            Require(rejectedName.IsFailure && rejectedName.Error == HomeLaunchErrorCode.InvalidEndpoint,
                "Join must reject host names and non-IPv4 endpoints.");
            Require(requests.Count == 0, "Invalid endpoint must not reach the launcher.");

            model.SetEndpointText("::1");
            HomeLaunchResult rejectedIpv6 = Await(model.LaunchJoinAsync());
            Require(rejectedIpv6.IsFailure && rejectedIpv6.Error == HomeLaunchErrorCode.InvalidEndpoint,
                "Join must reject IPv6 in v1.");

            model.SetEndpointText("192.168.1.20");
            model.SetPortText("0");
            HomeLaunchResult rejectedPort = Await(model.LaunchJoinAsync());
            Require(rejectedPort.IsFailure && rejectedPort.Error == HomeLaunchErrorCode.InvalidEndpoint,
                "Join must reject ports outside 1..65535.");

            model.SetPortText("7798");
            HomeLaunchResult accepted = Await(model.LaunchJoinAsync());
            Require(accepted.IsSuccess, "Valid IPv4 plus UDP port must launch Join.");
            Require(requests.Count == 1 && requests[0].Kind == HomeLaunchKind.Join,
                "Join request must be typed as Join.");
            Require(!requests[0].SlotId.HasValue, "Join must never carry a local slot.");
            Require(requests[0].EndpointText == "192.168.1.20" && requests[0].Port == 7798,
                "Join request must carry endpoint text and port.");
            cases++;
        }

        private static void RunConcurrentLaunchGuard(string root, ref int cases)
        {
            HomeMenuModel model = CreateModel(root, "double-launch", out SaveSlotStore store);
            Await(model.InitializeAsync());
            SlotResult<SaveSlot> slot = Await(store.CreateSlot("Doppelklick", default));
            Await(model.RefreshSlotsAsync());
            Await(model.SelectSlotAsync(slot.Value.SlotId));

            TaskCompletionSource<HomeLaunchResult> pending =
                new TaskCompletionSource<HomeLaunchResult>();
            int calls = 0;
            model.SetLauncher(request =>
            {
                calls++;
                return pending.Task;
            });

            Task<HomeLaunchResult> first = model.LaunchSoloAsync();
            HomeLaunchResult second = Await(model.LaunchJoinAsync());
            Require(second.IsFailure && second.Error == HomeLaunchErrorCode.Busy,
                "A second click during a pending launch must be rejected as Busy.");
            Require(calls == 1, "Only one launcher call may be active.");

            pending.SetResult(HomeLaunchResult.Success());
            Require(Await(first).IsSuccess, "First launch must finish successfully.");
            Require(!model.IsLaunching && !model.IsBusy, "Launch guard must release after completion.");
            cases++;
        }

        private static void RunSettingsDraftFlow(string root, ref int cases)
        {
            LocalSettingsStore settingsStore = new LocalSettingsStore(
                Path.Combine(root, "settings-flow", "settings.json"));
            SettingsResult<LocalSettings> initialized = Await(LocalSettingsRuntime.InitializeAsync(settingsStore));
            Require(initialized.IsSuccess, "Settings runtime must initialize for the home draft flow.");

            HomeMenuModel model = CreateModel(root, "settings-flow", out _);
            Await(model.InitializeAsync());
            model.OpenSettings();
            Require(model.IsSettingsOpen, "Settings panel must open.");

            model.PreviewMasterVolume(0.35f);
            model.PreviewMouseSensitivity(0.4f);
            Require(Mathf.Approximately(LocalSettingsRuntime.Effective.MasterVolume, 0.35f),
                "Preview must apply volume immediately through LocalSettingsRuntime.");
            Require(LocalSettingsRuntime.HasUnsavedChanges, "Preview must remain an unsaved draft.");

            SettingsResult<SettingsUnit> applied = Await(model.ApplySettingsAsync());
            Require(applied.IsSuccess, "Apply must save the draft through LocalSettingsRuntime.");
            Require(Mathf.Approximately(LocalSettingsRuntime.Persisted.MasterVolume, 0.35f),
                "Apply must persist the draft values.");
            Require(!LocalSettingsRuntime.HasUnsavedChanges, "Apply must clear unsaved state.");

            model.PreviewMasterVolume(0.9f);
            model.CancelSettings();
            Require(Mathf.Approximately(LocalSettingsRuntime.Effective.MasterVolume, 0.35f),
                "Cancel must revert to the persisted settings through LocalSettingsRuntime.");
            Require(Mathf.Approximately(model.SettingsDraft.MasterVolume, 0.35f),
                "Cancel must keep the draft aligned with persisted values.");

            model.OpenSettings();
            model.DefaultsSettings();
            Require(Mathf.Approximately(model.SettingsDraft.MasterVolume, LocalSettings.DefaultMasterVolume),
                "Defaults must preview LocalSettings.Defaults in the draft.");
            Require(Mathf.Approximately(LocalSettingsRuntime.Persisted.MasterVolume, 0.35f),
                "Defaults must not save until Apply is pressed.");
            SettingsResult<SettingsUnit> defaultApplied = Await(model.ApplySettingsAsync());
            Require(defaultApplied.IsSuccess, "Applying defaults must succeed.");
            Require(LocalSettingsRuntime.Persisted.Equals(LocalSettings.Defaults),
                "Applying defaults must persist LocalSettings.Defaults.");
            cases++;
        }

        private static HomeMenuModel CreateModel(string root, string name, out SaveSlotStore store)
        {
            string folder = Path.Combine(root, name + "-" + Guid.NewGuid().ToString("N"));
            store = new SaveSlotStore(folder);
            return new HomeMenuModel(store);
        }

        private static void SaveSlotIdEquals(SlotId expected, Guid? actual, string message)
        {
            Require(actual.HasValue && actual.Value == expected.Value, message);
        }

        private static T Await<T>(Task<T> task)
        {
            return task.GetAwaiter().GetResult();
        }

        private static void Await(Task task)
        {
            task.GetAwaiter().GetResult();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
