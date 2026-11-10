using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace SantasLittleHelpers.Settings
{
    internal sealed class LocalSettingsSmokeDriver : MonoBehaviour
    {
        private static readonly LocalSettings SmokeSettings = new LocalSettings(
            LocalSettings.CurrentSchemaVersion,
            0.33f,
            0.44f,
            false,
            "smoke");

        private string mode;
        private int frames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateWhenRequested()
        {
            string requestedMode = LocalSettingsCommandLine.GetValue(
                Environment.GetCommandLineArgs(),
                "--settings-smoke=");

            if (string.IsNullOrEmpty(requestedMode) || string.IsNullOrWhiteSpace(LocalSettingsCommandLine.GetValue(Environment.GetCommandLineArgs(), "--settings-path=")))
                return;

            GameObject root = new GameObject("SETTINGS_SMOKE_DRIVER");
            DontDestroyOnLoad(root);
            root.AddComponent<LocalSettingsSmokeDriver>().mode = requestedMode;
        }

        private void Update()
        {
            if (++frames < 3)
                return;

            enabled = false;
            bool passed;
            string details;
            try
            {
                passed = Run(mode, out details);
            }
            catch (Exception exception)
            {
                passed = false;
                details = $"exception={exception.GetType().Name}:{exception.Message}";
            }

            LocalSettings settings = LocalSettingsRuntime.Effective;
            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "SETTINGS_SMOKE_{0} mode={1} path={2} persisted={3} effective={4} audioVolume={5:F3} sensitivity={6:F3} screenFullscreen={7} {8}",
                passed ? "PASS" : "FAIL",
                mode,
                LocalSettingsRuntime.SettingsPath,
                LocalSettingsRuntime.Persisted,
                settings,
                AudioListener.volume,
                FirstPersonController.ActiveMouseSensitivity,
                Screen.fullScreen,
                details));

            Application.Quit(passed ? 0 : 1);
        }

        private static bool Run(string requestedMode, out string details)
        {
            switch (requestedMode)
            {
                case "save":
                    return RunSave(out details);
                case "load":
                    return RunLoad(out details);
                case "default":
                    return RunDefault(out details);
                case "revert":
                    return RunRevert(out details);
                case "error":
                    return RunError(out details);
                default:
                    details = $"unknown-mode={requestedMode}";
                    return false;
            }
        }

        private static bool RunSave(out string details)
        {
            SettingsResult<LocalSettings> preview = LocalSettingsRuntime.Preview(SmokeSettings);
            if (!preview.IsSuccess)
            {
                details = $"preview={preview.ErrorCode}";
                return false;
            }

            SettingsResult<Unit> saved = LocalSettingsRuntime.SaveAsync().GetAwaiter().GetResult();
            if (!saved.IsSuccess)
            {
                details = $"save={saved.ErrorCode}";
                return false;
            }

            bool persisted = LocalSettingsRuntime.Persisted.Equals(SmokeSettings);
            bool effective = LocalSettingsRuntime.Effective.Equals(SmokeSettings);
            bool fileExists = File.Exists(LocalSettingsRuntime.SettingsPath);
            details = $"persisted={persisted} effective={effective} fileExists={fileExists}";
            return persisted && effective && fileExists && VerifyApplied(SmokeSettings, false, out details);
        }

        private static bool RunLoad(out string details)
        {
            if (LocalSettingsRuntime.Store == null)
            {
                details = "store=missing";
                return false;
            }

            SettingsResult<LocalSettings> disk = LocalSettingsRuntime.Store
                .LoadSettings(default)
                .GetAwaiter()
                .GetResult();

            if (!disk.IsSuccess)
            {
                details = $"disk={disk.ErrorCode}";
                return false;
            }

            bool localSettingsEqual = LocalSettingsRuntime.Persisted.Equals(disk.Value);
            bool effective = LocalSettingsRuntime.Effective.Equals(LocalSettingsRuntime.Persisted);
            details = $"disk={disk.Value} persistedMatchesDisk={localSettingsEqual} effectiveMatchesPersisted={effective}";
            return localSettingsEqual && effective && VerifyApplied(LocalSettingsRuntime.Effective, true, out details);
        }

        private static bool RunDefault(out string details)
        {
            SettingsResult<Unit> reset = LocalSettingsRuntime
                .ResetToDefaultsAndSaveAsync()
                .GetAwaiter()
                .GetResult();

            if (!reset.IsSuccess)
            {
                details = $"reset={reset.ErrorCode}";
                return false;
            }

            bool persisted = LocalSettingsRuntime.Persisted.Equals(LocalSettings.Defaults);
            bool effective = LocalSettingsRuntime.Effective.Equals(LocalSettings.Defaults);
            details = $"persistedDefaults={persisted} effectiveDefaults={effective}";
            return persisted && effective && VerifyApplied(LocalSettings.Defaults, false, out details);
        }

        private static bool RunRevert(out string details)
        {
            LocalSettings original = LocalSettingsRuntime.Persisted;
            LocalSettings changed = original.Equals(SmokeSettings) ? LocalSettings.Defaults : SmokeSettings;

            SettingsResult<LocalSettings> preview = LocalSettingsRuntime.Preview(changed);
            bool previewApplied = preview.IsSuccess
                && LocalSettingsRuntime.Effective.Equals(changed)
                && LocalSettingsRuntime.HasUnsavedChanges;

            LocalSettingsRuntime.RevertPreview();
            bool reverted = LocalSettingsRuntime.Effective.Equals(original)
                && !LocalSettingsRuntime.HasUnsavedChanges
                && LocalSettingsRuntime.Persisted.Equals(original);

            bool diskUnchanged = true;
            if (LocalSettingsRuntime.Store != null)
            {
                SettingsResult<LocalSettings> disk = LocalSettingsRuntime.Store
                    .LoadSettings(default)
                    .GetAwaiter()
                    .GetResult();
                diskUnchanged = disk.IsSuccess && disk.Value.Equals(original);
            }

            details = $"previewApplied={previewApplied} reverted={reverted} diskUnchanged={diskUnchanged}";
            return previewApplied && reverted && diskUnchanged
                && VerifyApplied(original, true, out details);
        }

        private static bool RunError(out string details)
        {
            if (LocalSettingsRuntime.Store == null)
            {
                details = "store=missing";
                return false;
            }

            string path = LocalSettingsRuntime.Store.FilePath;
            string backup = File.Exists(path) ? File.ReadAllText(path) : null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "not a settings document");

                SettingsResult<LocalSettings> corrupt = LocalSettingsRuntime.Store
                    .LoadSettings(default)
                    .GetAwaiter()
                    .GetResult();
                bool corruptRecovered = corrupt.IsSuccess
                    && corrupt.RecoveredDefaults
                    && corrupt.RecoveryReason == SettingsRecoveryReason.CorruptedData
                    && corrupt.Value.Equals(LocalSettings.Defaults);

                LocalSettings invalidSettings = new LocalSettings(
                    LocalSettings.CurrentSchemaVersion,
                    LocalSettings.MaximumMasterVolume + 1f,
                    LocalSettings.DefaultMouseSensitivity,
                    LocalSettings.DefaultFullscreen);
                SettingsResult<Unit> invalidSave = LocalSettingsRuntime.Store
                    .SaveSettings(invalidSettings, default)
                    .GetAwaiter()
                    .GetResult();
                bool invalidRejected = !invalidSave.IsSuccess
                    && invalidSave.ErrorCode == SettingsErrorCode.InvalidSettings;

                File.WriteAllText(path,
                    "{\"schemaVersion\":0,\"masterVolume\":0.5,\"mouseSensitivity\":0.2,\"fullscreen\":true}");
                SettingsResult<LocalSettings> unsupported = LocalSettingsRuntime.Store
                    .LoadSettings(default)
                    .GetAwaiter()
                    .GetResult();
                bool unsupportedRecovered = unsupported.IsSuccess
                    && unsupported.RecoveredDefaults
                    && unsupported.RecoveryReason == SettingsRecoveryReason.UnsupportedSchema
                    && unsupported.Value.Equals(LocalSettings.Defaults);

                details = $"corruptRecovered={corruptRecovered} invalidRejected={invalidRejected} unsupportedRecovered={unsupportedRecovered}";
                return corruptRecovered && invalidRejected && unsupportedRecovered;
            }
            finally
            {
                if (backup != null)
                    File.WriteAllText(path, backup);
                else if (File.Exists(path))
                    File.Delete(path);
            }
        }

        private static bool VerifyApplied(LocalSettings expected, bool verifyScreen, out string details)
        {
            bool audio = Mathf.Approximately(AudioListener.volume, Mathf.Clamp01(expected.MasterVolume));
            bool sensitivity = Mathf.Approximately(
                FirstPersonController.ActiveMouseSensitivity,
                expected.MouseSensitivity);
            bool screen = !verifyScreen || Screen.fullScreen == expected.Fullscreen;
            bool localController = ProbeLocalController(expected.MouseSensitivity);
            details = $"appliedAudio={audio} appliedSensitivity={sensitivity} appliedScreen={screen} localController={localController}";
            return audio && sensitivity && screen && localController;
        }

        private static bool ProbeLocalController(float expectedSensitivity)
        {
            GameObject root = new GameObject("SETTINGS_SENSITIVITY_PROBE");
            root.SetActive(false);
            try
            {
                root.AddComponent<CharacterController>();
                GameObject pivot = new GameObject("Pivot");
                pivot.transform.SetParent(root.transform, false);
                FirstPersonController controller = root.AddComponent<FirstPersonController>();
                FieldInfo viewPivot = typeof(FirstPersonController).GetField(
                    "viewPivot",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                viewPivot?.SetValue(controller, pivot.transform);
                root.SetActive(true);
                return Mathf.Approximately(controller.MouseSensitivity, expectedSensitivity);
            }
            finally
            {
                Destroy(root);
            }
        }
    }
}
