using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SantasLittleHelpers.Settings
{
    public static class LocalSettingsRuntime
    {
        private static LocalSettings persisted = LocalSettings.Defaults;
        private static LocalSettings effective = LocalSettings.Defaults;

        public static LocalSettings Persisted => persisted;
        public static LocalSettings Effective => effective;
        public static bool HasUnsavedChanges { get; private set; }
        public static bool IsInitialized { get; private set; }
        public static SettingsError LastError { get; private set; }
        public static LocalSettingsStore Store { get; private set; }
        public static string SettingsPath => Store != null ? Store.FilePath : string.Empty;

        public static event Action<LocalSettings> EffectiveChanged;
        public static event Action<LocalSettings> PersistedChanged;
        public static event Action<SettingsError> ErrorOccurred;

        public static async Task<SettingsResult<LocalSettings>> InitializeAsync(
            LocalSettingsStore store = null,
            CancellationToken ct = default)
        {
            Store = store ?? LocalSettingsStore.CreateDefault();
            SettingsResult<LocalSettings> result = await Store.LoadSettings(ct);

            if (result.IsSuccess)
            {
                LastError = default;
                persisted = result.Value;
                effective = result.Value;
            }
            else
            {
                LastError = result.Error;
                persisted = LocalSettings.Defaults;
                effective = LocalSettings.Defaults;
            }

            HasUnsavedChanges = false;
            IsInitialized = true;
            Apply(effective);
            PersistedChanged?.Invoke(persisted);
            EffectiveChanged?.Invoke(effective);

            if (result.HasError)
                ErrorOccurred?.Invoke(result.Error);

            return result;
        }

        public static SettingsResult<LocalSettings> Preview(LocalSettings settings)
        {
            if (!LocalSettings.TryValidate(settings, out SettingsErrorCode error, out string message))
                return ReportFailure(error, message);

            effective = settings;
            HasUnsavedChanges = !effective.Equals(persisted);
            Apply(effective);
            EffectiveChanged?.Invoke(effective);
            return SettingsResult<LocalSettings>.Success(effective);
        }

        public static SettingsResult<LocalSettings> SetMasterVolume(float value)
        {
            return Preview(effective.WithMasterVolume(value));
        }

        public static SettingsResult<LocalSettings> SetMouseSensitivity(float value)
        {
            return Preview(effective.WithMouseSensitivity(value));
        }

        public static SettingsResult<LocalSettings> SetFullscreen(bool value)
        {
            return Preview(effective.WithFullscreen(value));
        }

        public static SettingsResult<LocalSettings> SetLastSelection(string value)
        {
            return Preview(effective.WithLastSelection(value));
        }

        public static async Task<SettingsResult<Unit>> SaveAsync(CancellationToken ct = default)
        {
            if (Store == null)
                return ReportUnitFailure(SettingsErrorCode.StorageUnavailable, "Settings storage is not initialized.");

            SettingsResult<Unit> result = await Store.SaveSettings(effective, ct);
            if (!result.IsSuccess)
                return ReportUnitFailure(result.ErrorCode, result.Error.Message);

            persisted = effective;
            HasUnsavedChanges = false;
            LastError = default;
            PersistedChanged?.Invoke(persisted);
            return result;
        }

        public static void RevertPreview()
        {
            effective = persisted;
            HasUnsavedChanges = false;
            Apply(effective);
            EffectiveChanged?.Invoke(effective);
        }

        public static SettingsResult<LocalSettings> ResetToDefaultsPreview()
        {
            return Preview(LocalSettings.Defaults);
        }

        public static async Task<SettingsResult<Unit>> ResetToDefaultsAndSaveAsync(CancellationToken ct = default)
        {
            SettingsResult<LocalSettings> preview = ResetToDefaultsPreview();
            if (!preview.IsSuccess)
                return ReportUnitFailure(preview.ErrorCode, preview.Error.Message);

            return await SaveAsync(ct);
        }

        public static void Apply(LocalSettings settings)
        {
            if (settings == null)
                return;

            AudioListener.volume = Mathf.Clamp01(settings.MasterVolume);
            Screen.fullScreen = settings.Fullscreen;
            FirstPersonController.ApplyGlobalMouseSensitivity(settings.MouseSensitivity);
        }

        internal static LocalSettingsStore CreateStoreFromCommandLine(string[] args)
        {
            string mode = LocalSettingsCommandLine.GetValue(args, "--settings-smoke=");
            string path = string.IsNullOrEmpty(mode) ? null : LocalSettingsCommandLine.GetValue(args, "--settings-path=");
            return string.IsNullOrWhiteSpace(path)
                ? LocalSettingsStore.CreateDefault()
                : new LocalSettingsStore(Path.GetFullPath(path));
        }

        private static SettingsResult<LocalSettings> ReportFailure(SettingsErrorCode code, string message)
        {
            SettingsError error = new SettingsError(code, message);
            LastError = error;
            ErrorOccurred?.Invoke(error);
            return SettingsResult<LocalSettings>.Failure(code, message);
        }

        private static SettingsResult<Unit> ReportUnitFailure(SettingsErrorCode code, string message)
        {
            SettingsError error = new SettingsError(code, message);
            LastError = error;
            ErrorOccurred?.Invoke(error);
            return SettingsResult<Unit>.Failure(code, message);
        }
    }

    internal static class LocalSettingsCommandLine
    {
        internal static string GetValue(string[] args, string prefix)
        {
            if (args == null)
                return null;

            foreach (string argument in args)
                if (argument != null && argument.StartsWith(prefix, StringComparison.Ordinal))
                    return argument.Substring(prefix.Length);

            return null;
        }
    }
}
