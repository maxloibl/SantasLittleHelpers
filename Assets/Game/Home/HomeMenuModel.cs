using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SantasLittleHelpers.Savegames;
using SlotUnit = SantasLittleHelpers.Savegames.Unit;
using SettingsUnit = SantasLittleHelpers.Settings.Unit;
using SantasLittleHelpers.Settings;
using UnityEngine;

namespace SantasLittleHelpers.Home
{
    public enum HomeStatusKind
    {
        Info = 0,
        Success = 1,
        Warning = 2,
        Error = 3
    }

    /// <summary>
    /// UI-independent home menu state. The controller/view only render this model.
    /// Slots always come from the real <see cref="SaveSlotStore"/>; launching is injected.
    /// </summary>
    public sealed class HomeMenuModel
    {
        public const string DefaultEndpoint = "127.0.0.1";
        public const string DefaultPort = "7777";
        public const string DefaultHostEndpoint = "0.0.0.0";

        private readonly SaveSlotStore store;
        private readonly object launchGate = new object();

        private Func<HomeLaunchRequest, Task<HomeLaunchResult>> launcher;
        private IReadOnlyList<SaveSlotSummary> slots = Array.Empty<SaveSlotSummary>();
        private Guid? selectedSlotId;
        private SaveSlot loadedSlot;
        private string newSlotName = string.Empty;
        private string renameSlotName = string.Empty;
        private string endpointText = DefaultEndpoint;
        private string portText = DefaultPort;
        private string statusMessage = string.Empty;
        private HomeStatusKind statusKind = HomeStatusKind.Info;
        private LocalSettings settingsDraft = LocalSettings.Defaults;
        private bool isBusy;
        private bool isLaunching;
        private bool isSettingsOpen;
        private bool isDeleteConfirmationOpen;
        private int selectionVersion;

        public HomeMenuModel(
            SaveSlotStore store,
            Func<HomeLaunchRequest, Task<HomeLaunchResult>> launcher = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.launcher = launcher;
        }

        public event Action Changed;

        public event Action<HomeLaunchRequest> LaunchSucceeded;

        public SaveSlotStore Store => store;

        public IReadOnlyList<SaveSlotSummary> Slots => slots;

        public Guid? SelectedSlotId => selectedSlotId;

        public SaveSlotSummary SelectedSummary =>
            selectedSlotId.HasValue
                ? slots.FirstOrDefault(slot => slot.SlotId.Value == selectedSlotId.Value)
                : null;

        public SaveSlot LoadedSlot => loadedSlot;

        public bool HasLoadableSelection => SelectedSummary?.IsLoadable == true;

        public bool IsBusy => isBusy;

        public bool IsLaunching => isLaunching;

        public bool IsSettingsOpen => isSettingsOpen;

        public bool IsDeleteConfirmationOpen => isDeleteConfirmationOpen;

        public string NewSlotName => newSlotName;

        public string RenameSlotName => renameSlotName;

        public string EndpointText => endpointText;

        public string PortText => portText;

        public string StatusMessage => statusMessage;

        public HomeStatusKind StatusKind => statusKind;

        public LocalSettings SettingsDraft => settingsDraft;

        public Func<HomeLaunchRequest, Task<HomeLaunchResult>> Launcher => launcher;

        public bool CanLaunch => !isBusy && !isLaunching && HasLoadableSelection;

        public bool CanJoin => !isBusy && !isLaunching;

        public void SetLauncher(Func<HomeLaunchRequest, Task<HomeLaunchResult>> handler)
        {
            launcher = handler;
            Notify();
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (!LocalSettingsRuntime.IsInitialized)
            {
                SettingsResult<LocalSettings> initialized = await LocalSettingsRuntime.InitializeAsync(null, ct);
                if (!initialized.IsSuccess)
                {
                    SetStatus(HomeStatusKind.Warning,
                        $"Einstellungen konnten nicht geladen werden: {initialized.Error.Message}");
                }
            }

            settingsDraft = LocalSettingsRuntime.Effective ?? LocalSettings.Defaults;
            await RefreshSlotsAsync(ct);
        }

        public Task RefreshSlotsAsync(CancellationToken ct = default)
        {
            return RunBusyAsync(async () =>
            {
                SlotResult<IReadOnlyList<SaveSlotSummary>> result = await store.ListSlots(ct);
                if (result.IsFailure)
                {
                    slots = Array.Empty<SaveSlotSummary>();
                    loadedSlot = null;
                    SetStatus(HomeStatusKind.Error,
                        $"Spielstände konnten nicht gelesen werden ({result.Error}): {result.ErrorMessage}");
                    return;
                }

                slots = result.Value ?? Array.Empty<SaveSlotSummary>();
                if (selectedSlotId.HasValue &&
                    !slots.Any(slot => slot.SlotId.Value == selectedSlotId.Value))
                {
                    selectedSlotId = null;
                    loadedSlot = null;
                    renameSlotName = string.Empty;
                }

                if (!selectedSlotId.HasValue)
                {
                    SaveSlotSummary firstLoadable = slots.FirstOrDefault(slot => slot.IsLoadable);
                    if (firstLoadable != null)
                    {
                        selectedSlotId = firstLoadable.SlotId.Value;
                        renameSlotName = firstLoadable.DisplayName;
                    }
                }

                if (selectedSlotId.HasValue)
                {
                    await LoadSlotCoreAsync(selectedSlotId.Value, ct);
                }
                else if (slots.Count == 0)
                {
                    SetStatus(HomeStatusKind.Info,
                        "Noch keine Spielstände. Lege deinen ersten gemütlichen Spielstand an.");
                }
                else
                {
                    SetStatus(HomeStatusKind.Warning,
                        "Kein ladbarer Spielstand vorhanden. Bitte einen Eintrag reparieren oder neu anlegen.");
                }
            });
        }

        public Task SelectSlotAsync(SlotId slotId, CancellationToken ct = default)
        {
            return RunBusyAsync(async () =>
            {
                SaveSlotSummary summary = slots.FirstOrDefault(slot => slot.SlotId == slotId);
                if (summary == null)
                {
                    SetStatus(HomeStatusKind.Error, "Der gewählte Spielstand ist nicht mehr vorhanden.");
                    return;
                }

                selectedSlotId = summary.SlotId.Value;
                renameSlotName = summary.DisplayName;
                int version = ++selectionVersion;

                if (!summary.IsLoadable)
                {
                    loadedSlot = null;
                    SetStatus(HomeStatusKind.Error,
                        $"{summary.DisplayName}: {DescribeSlotStatus(summary)} {summary.ErrorMessage}".Trim());
                    return;
                }

                await LoadSlotCoreAsync(summary.SlotId.Value, ct, version);
            });
        }

        public Task<bool> LoadSelectedAsync(CancellationToken ct = default)
        {
            if (!selectedSlotId.HasValue)
            {
                SetStatus(HomeStatusKind.Error, "Bitte zuerst einen Spielstand auswählen.");
                return Task.FromResult(false);
            }

            return RunBusyAsync(async () =>
            {
                await LoadSlotCoreAsync(selectedSlotId.Value, ct, ++selectionVersion);
                return loadedSlot != null;
            });
        }

        public Task<SlotResult<SaveSlot>> CreateSlotAsync(string displayName, CancellationToken ct = default)
        {
            return RunBusyAsync(async () =>
            {
                SlotResult<SaveSlot> result = await store.CreateSlot(displayName, ct);
                if (result.IsFailure)
                {
                    SetStatus(HomeStatusKind.Error,
                        $"Spielstand konnte nicht angelegt werden ({result.Error}): {result.ErrorMessage}");
                    return result;
                }

                selectedSlotId = result.Value.SlotId.Value;
                loadedSlot = result.Value;
                renameSlotName = result.Value.DisplayName;
                newSlotName = string.Empty;
                SetStatus(HomeStatusKind.Success, $"Spielstand „{result.Value.DisplayName}“ angelegt.");
                await ReloadSlotsAfterMutationAsync(ct);
                return result;
            });
        }

        public Task<SlotResult<SlotUnit>> RenameSelectedAsync(string displayName, CancellationToken ct = default)
        {
            return RunBusyAsync(async () =>
            {
                if (!selectedSlotId.HasValue)
                {
                    SetStatus(HomeStatusKind.Error, "Bitte zuerst einen Spielstand auswählen.");
                    return SlotResult<SlotUnit>.Failure(SlotErrorCode.InvalidSlot, "No slot selected.");
                }

                SlotResult<SlotUnit> result = await store.RenameSlot(selectedSlotId.Value, displayName, ct);
                if (result.IsFailure)
                {
                    SetStatus(HomeStatusKind.Error,
                        $"Spielstand konnte nicht umbenannt werden ({result.Error}): {result.ErrorMessage}");
                    return result;
                }

                SetStatus(HomeStatusKind.Success, "Spielstand umbenannt.");
                await ReloadSlotsAfterMutationAsync(ct);
                return result;
            });
        }

        public void BeginDeleteConfirmation()
        {
            if (!selectedSlotId.HasValue)
            {
                SetStatus(HomeStatusKind.Error, "Bitte zuerst einen Spielstand auswählen.");
                return;
            }

            isDeleteConfirmationOpen = true;
            SetStatus(HomeStatusKind.Warning,
                $"Spielstand „{SelectedSummary?.DisplayName ?? string.Empty}“ wirklich löschen?");
        }

        public void CancelDeleteConfirmation()
        {
            if (!isDeleteConfirmationOpen)
            {
                return;
            }

            isDeleteConfirmationOpen = false;
            SetStatus(HomeStatusKind.Info, "Löschen abgebrochen.");
        }

        public Task<SlotResult<SlotUnit>> ConfirmDeleteAsync(CancellationToken ct = default)
        {
            return RunBusyAsync(async () =>
            {
                if (!selectedSlotId.HasValue)
                {
                    isDeleteConfirmationOpen = false;
                    SetStatus(HomeStatusKind.Error, "Kein Spielstand zum Löschen ausgewählt.");
                    return SlotResult<SlotUnit>.Failure(SlotErrorCode.InvalidSlot, "No slot selected.");
                }

                Guid slotId = selectedSlotId.Value;
                SlotResult<SlotUnit> result = await store.DeleteSlot(slotId, ct);
                isDeleteConfirmationOpen = false;
                if (result.IsFailure)
                {
                    SetStatus(HomeStatusKind.Error,
                        $"Spielstand konnte nicht gelöscht werden ({result.Error}): {result.ErrorMessage}");
                    return result;
                }

                selectedSlotId = null;
                loadedSlot = null;
                renameSlotName = string.Empty;
                SetStatus(HomeStatusKind.Success, "Spielstand gelöscht.");
                await ReloadSlotsAfterMutationAsync(ct);
                return result;
            });
        }

        public Task<HomeLaunchResult> LaunchSoloAsync(CancellationToken ct = default)
        {
            return LaunchSelectedAsync(HomeLaunchKind.Solo, ct);
        }

        public Task<HomeLaunchResult> LaunchHostAsync(CancellationToken ct = default)
        {
            return LaunchSelectedAsync(HomeLaunchKind.Host, ct);
        }

        public Task<HomeLaunchResult> LaunchJoinAsync(CancellationToken ct = default)
        {
            if (!TryBeginLaunch())
            {
                return Task.FromResult(HomeLaunchResult.Failure(
                    HomeLaunchErrorCode.Busy,
                    "Ein Startvorgang läuft bereits."));
            }

            return CompleteJoinLaunchAsync(ct);
        }

        public void SetNewSlotName(string value)
        {
            newSlotName = value ?? string.Empty;
        }

        public void SetRenameSlotName(string value)
        {
            renameSlotName = value ?? string.Empty;
        }

        public void SetEndpointText(string value)
        {
            endpointText = value ?? string.Empty;
        }

        public void SetPortText(string value)
        {
            portText = value ?? string.Empty;
        }

        public void OpenSettings()
        {
            isSettingsOpen = true;
            settingsDraft = LocalSettingsRuntime.Effective ?? LocalSettings.Defaults;
            SetStatus(HomeStatusKind.Info, "Einstellungen als Entwurf geöffnet. Änderungen erst mit Übernehmen speichern.");
        }

        public void CloseSettings()
        {
            isSettingsOpen = false;
            Notify();
        }

        public void PreviewMasterVolume(float value)
        {
            ApplySettingsPreview(settingsDraft.WithMasterVolume(value));
        }

        public void PreviewMouseSensitivity(float value)
        {
            ApplySettingsPreview(settingsDraft.WithMouseSensitivity(value));
        }

        public void PreviewFullscreen(bool value)
        {
            ApplySettingsPreview(settingsDraft.WithFullscreen(value));
        }

        public async Task<SettingsResult<SettingsUnit>> ApplySettingsAsync(CancellationToken ct = default)
        {
            SettingsResult<SettingsUnit> result = await LocalSettingsRuntime.SaveAsync(ct);
            if (result.IsSuccess)
            {
                settingsDraft = LocalSettingsRuntime.Persisted;
                SetStatus(HomeStatusKind.Success, "Einstellungen übernommen.");
            }
            else
            {
                SetStatus(HomeStatusKind.Error,
                    $"Einstellungen konnten nicht gespeichert werden ({result.ErrorCode}): {result.Error.Message}");
            }

            return result;
        }

        public void CancelSettings()
        {
            LocalSettingsRuntime.RevertPreview();
            settingsDraft = LocalSettingsRuntime.Persisted;
            isSettingsOpen = false;
            SetStatus(HomeStatusKind.Info, "Einstellungsänderungen verworfen.");
        }

        public void DefaultsSettings()
        {
            SettingsResult<LocalSettings> result = LocalSettingsRuntime.ResetToDefaultsPreview();
            if (!result.IsSuccess)
            {
                SetStatus(HomeStatusKind.Error,
                    $"Standardwerte konnten nicht geladen werden ({result.ErrorCode}): {result.Error.Message}");
                return;
            }

            settingsDraft = result.Value;
            SetStatus(HomeStatusKind.Info,
                "Standardwerte als Entwurf übernommen. Mit Übernehmen dauerhaft speichern.");
        }

        public void ShowNotice(string notice)
        {
            SetStatus(HomeStatusKind.Info, string.IsNullOrWhiteSpace(notice) ? string.Empty : notice);
        }

        public static string DescribeSlotStatus(SaveSlotSummary summary)
        {
            if (summary == null)
            {
                return "Unbekannter Spielstand.";
            }

            switch (summary.Status)
            {
                case SaveSlotStatus.Ok:
                    return "Bereit";
                case SaveSlotStatus.CorruptData:
                    return "Beschädigt";
                case SaveSlotStatus.UnsupportedSchema:
                    return "Inkompatible Version";
                case SaveSlotStatus.Unreadable:
                    return "Nicht lesbar";
                default:
                    return summary.Status.ToString();
            }
        }

        public static bool TryParseEndpoint(string endpointText, string portText, out IPEndPoint endpoint,
            out string error)
        {
            endpoint = null;
            error = string.Empty;

            string trimmedEndpoint = (endpointText ?? string.Empty).Trim();
            if (!IPAddress.TryParse(trimmedEndpoint, out IPAddress address) ||
                address.AddressFamily != AddressFamily.InterNetwork)
            {
                error = "Bitte eine gültige IPv4-Adresse eingeben, z. B. 192.168.1.20.";
                return false;
            }

            if (!int.TryParse((portText ?? string.Empty).Trim(), out int port) || port < 1 || port > 65535)
            {
                error = "Bitte einen UDP-Port zwischen 1 und 65535 eingeben.";
                return false;
            }

            endpoint = new IPEndPoint(address, port);
            return true;
        }

        private Task<HomeLaunchResult> LaunchSelectedAsync(HomeLaunchKind kind, CancellationToken ct)
        {
            if (!TryBeginLaunch())
            {
                return Task.FromResult(HomeLaunchResult.Failure(
                    HomeLaunchErrorCode.Busy,
                    "Ein Startvorgang läuft bereits."));
            }

            return CompleteSlotLaunchAsync(kind, ct);
        }

        private async Task<HomeLaunchResult> CompleteSlotLaunchAsync(HomeLaunchKind kind, CancellationToken ct)
        {
            try
            {
                SaveSlotSummary summary = SelectedSummary;
                if (summary == null)
                {
                    return FinishLaunch(HomeLaunchResult.Failure(
                        HomeLaunchErrorCode.InvalidSlot,
                        "Bitte zuerst einen Spielstand auswählen."));
                }

                if (!summary.IsLoadable)
                {
                    return FinishLaunch(HomeLaunchResult.Failure(
                        HomeLaunchErrorCode.InvalidSlot,
                        $"{summary.DisplayName}: {DescribeSlotStatus(summary)} {summary.ErrorMessage}".Trim()));
                }

                SlotResult<SaveSlot> loaded = await store.LoadSlot(summary.SlotId, ct);
                if (loaded.IsFailure)
                {
                    return FinishLaunch(HomeLaunchResult.Failure(
                        HomeLaunchErrorCode.InvalidSlot,
                        $"Spielstand konnte nicht geladen werden ({loaded.Error}): {loaded.ErrorMessage}"));
                }

                loadedSlot = loaded.Value;
                SetStatus(HomeStatusKind.Info,
                    kind == HomeLaunchKind.Solo
                        ? $"Solo wird für „{loaded.Value.DisplayName}“ vorbereitet …"
                        : $"Host wird für „{loaded.Value.DisplayName}“ vorbereitet …");

                HomeLaunchRequest request = kind == HomeLaunchKind.Solo
                    ? HomeLaunchRequest.Solo(loaded.Value.SlotId.Value)
                    : HomeLaunchRequest.Host(loaded.Value.SlotId.Value, DefaultHostEndpoint, ParsePortOrDefault());
                lastRequest = request;
                return FinishLaunch(await InvokeLauncherAsync(request));
            }
            catch (Exception exception)
            {
                return FinishLaunch(HomeLaunchResult.Failure(
                    HomeLaunchErrorCode.LaunchFailed,
                    $"Start fehlgeschlagen: {exception.Message}"));
            }
            finally
            {
                EndLaunch();
            }
        }

        private async Task<HomeLaunchResult> CompleteJoinLaunchAsync(CancellationToken ct)
        {
            try
            {
                if (!TryParseEndpoint(endpointText, portText, out IPEndPoint endpoint, out string error))
                {
                    return FinishLaunch(HomeLaunchResult.Failure(HomeLaunchErrorCode.InvalidEndpoint, error));
                }

                SetStatus(HomeStatusKind.Info,
                    $"Beitritt zu {endpoint.Address}:{endpoint.Port} wird vorbereitet …");
                HomeLaunchRequest request = HomeLaunchRequest.Join(endpoint.Address.ToString(), endpoint.Port);
                lastRequest = request;
                return FinishLaunch(await InvokeLauncherAsync(request));
            }
            catch (Exception exception)
            {
                return FinishLaunch(HomeLaunchResult.Failure(
                    HomeLaunchErrorCode.LaunchFailed,
                    $"Beitritt fehlgeschlagen: {exception.Message}"));
            }
            finally
            {
                EndLaunch();
            }
        }

        private async Task<HomeLaunchResult> InvokeLauncherAsync(HomeLaunchRequest request)
        {
            if (launcher == null)
            {
                return HomeLaunchResult.Failure(
                    HomeLaunchErrorCode.LaunchFailed,
                    "Kein Sitzungsdienst verbunden. Start ist derzeit nicht möglich.");
            }

            HomeLaunchResult result = await launcher(request);
            if (result.IsSuccess)
            {
                return result;
            }

            if (result.Error == HomeLaunchErrorCode.None)
            {
                return HomeLaunchResult.Failure(HomeLaunchErrorCode.LaunchFailed,
                    string.IsNullOrWhiteSpace(result.Message) ? "Start fehlgeschlagen." : result.Message);
            }

            return result;
        }

        private HomeLaunchResult FinishLaunch(HomeLaunchResult result)
        {
            if (result.IsSuccess)
            {
                SetStatus(HomeStatusKind.Success,
                    string.IsNullOrWhiteSpace(result.Message) ? "Start bestätigt." : result.Message);
                LaunchSucceeded?.Invoke(lastRequest);
            }
            else
            {
                SetStatus(HomeStatusKind.Error,
                    string.IsNullOrWhiteSpace(result.Message)
                        ? $"Start fehlgeschlagen ({result.Error})."
                        : result.Message);
            }

            return result;
        }

        private HomeLaunchRequest lastRequest;

        private async Task LoadSlotCoreAsync(Guid slotId, CancellationToken ct, int? version = null)
        {
            int expectedVersion = version ?? selectionVersion;
            if (!selectedSlotId.HasValue || selectedSlotId.Value != slotId)
            {
                return;
            }

            SlotResult<SaveSlot> result = await store.LoadSlot(slotId, ct);
            if (expectedVersion != selectionVersion ||
                !selectedSlotId.HasValue ||
                selectedSlotId.Value != slotId)
            {
                return;
            }

            if (result.IsFailure)
            {
                loadedSlot = null;
                SetStatus(HomeStatusKind.Error,
                    $"Spielstand konnte nicht geladen werden ({result.Error}): {result.ErrorMessage}");
                return;
            }

            loadedSlot = result.Value;
            SetStatus(HomeStatusKind.Success,
                $"Spielstand „{result.Value.DisplayName}“ geladen · {result.Value.CompletedMissionIds.Count} abgeschlossene Mission(en).");
        }

        private bool TryBeginLaunch()
        {
            lock (launchGate)
            {
                if (isLaunching || isBusy)
                {
                    return false;
                }

                isLaunching = true;
                isBusy = true;
                lastRequest = null;
            }

            SetStatus(HomeStatusKind.Info, "Start wird vorbereitet …");
            return true;
        }

        private void EndLaunch()
        {
            lock (launchGate)
            {
                isLaunching = false;
                isBusy = false;
            }

            Notify();
        }

        private async Task ReloadSlotsAfterMutationAsync(CancellationToken ct)
        {
            SlotResult<IReadOnlyList<SaveSlotSummary>> result = await store.ListSlots(ct);
            if (result.IsFailure)
            {
                SetStatus(HomeStatusKind.Error,
                    $"Spielstände konnten nicht aktualisiert werden ({result.Error}): {result.ErrorMessage}");
                return;
            }

            slots = result.Value ?? Array.Empty<SaveSlotSummary>();
            if (selectedSlotId.HasValue)
            {
                SaveSlotSummary summary = SelectedSummary;
                if (summary == null)
                {
                    selectedSlotId = null;
                    loadedSlot = null;
                    renameSlotName = string.Empty;
                }
                else
                {
                    renameSlotName = summary.DisplayName;
                }
            }
        }

        private void ApplySettingsPreview(LocalSettings settings)
        {
            SettingsResult<LocalSettings> result = LocalSettingsRuntime.Preview(settings);
            if (!result.IsSuccess)
            {
                settingsDraft = LocalSettingsRuntime.Effective ?? LocalSettings.Defaults;
                SetStatus(HomeStatusKind.Error,
                    $"Einstellung ungültig ({result.ErrorCode}): {result.Error.Message}");
                return;
            }

            settingsDraft = result.Value;
            SetStatus(HomeStatusKind.Info, "Einstellungsentwurf aktualisiert. Übernehmen speichert dauerhaft.");
            Notify();
        }

        private int ParsePortOrDefault()
        {
            if (int.TryParse((portText ?? string.Empty).Trim(), out int port) && port >= 1 && port <= 65535)
            {
                return port;
            }

            return 7777;
        }

        private void SetStatus(HomeStatusKind kind, string message)
        {
            statusKind = kind;
            statusMessage = message ?? string.Empty;
            Notify();
        }

        private void Notify()
        {
            Changed?.Invoke();
        }

        private async Task RunBusyAsync(Func<Task> action)
        {
            isBusy = true;
            Notify();
            try
            {
                await action();
            }
            finally
            {
                isBusy = false;
                Notify();
            }
        }

        private async Task<T> RunBusyAsync<T>(Func<Task<T>> action)
        {
            isBusy = true;
            Notify();
            try
            {
                return await action();
            }
            finally
            {
                isBusy = false;
                Notify();
            }
        }
    }
}
