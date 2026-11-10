using System;
using System.Globalization;
using UnityEngine;

namespace SantasLittleHelpers.Settings
{
    public static class LocalSettingsBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            try
            {
                LocalSettingsStore store = LocalSettingsRuntime.CreateStoreFromCommandLine(
                    Environment.GetCommandLineArgs());
                SettingsResult<LocalSettings> result = LocalSettingsRuntime.InitializeAsync(store)
                    .GetAwaiter()
                    .GetResult();

                Debug.Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "SETTINGS_LOAD path={0} result={1} error={2} recovered={3} reason={4} volume={5:F3} sensitivity={6:F3} fullscreen={7}",
                    store.FilePath,
                    result.IsSuccess ? "Loaded" : "Failed",
                    result.ErrorCode,
                    result.RecoveredDefaults,
                    result.RecoveryReason,
                    LocalSettingsRuntime.Effective.MasterVolume,
                    LocalSettingsRuntime.Effective.MouseSensitivity,
                    LocalSettingsRuntime.Effective.Fullscreen));
            }
            catch (Exception exception)
            {
                LocalSettingsRuntime.Apply(LocalSettings.Defaults);
                Debug.LogError($"SETTINGS_LOAD result=Failed error={exception.GetType().Name} message={exception.Message}");
            }
        }
    }
}
