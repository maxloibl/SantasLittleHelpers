using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace SantasLittleHelpers.Settings.Editor
{
    public static class LocalSettingsSmokeTests
    {
        private static readonly string QualityRoot = Path.Combine(Path.GetTempPath(), "slh-settings-tests");

        public static void Run()
        {
            string root = Path.Combine(QualityRoot, "editmode-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            int cases = 0;

            try
            {
                VerifyMissingFileUsesDefaults(root);
                cases++;
                VerifySaveAndLoadRoundtrip(root);
                cases++;
                VerifyCorruptedFileRecoversDefaults(root);
                cases++;
                VerifyOldAndFutureSchemasRecoverDefaults(root);
                cases++;
                VerifyInvalidValuesRecoverDefaults(root);
                cases++;
                VerifyMissingSchemaRecoversDefaults(root);
                cases++;
                VerifyInvalidSaveIsRejected(root);
                cases++;
                VerifyRuntimePreviewSaveAndRevert(root);
                cases++;

                Debug.Log($"SETTINGS_EDITOR_SMOKE_PASS cases={cases} root={root}");
            }
            catch (Exception exception)
            {
                Debug.LogError($"SETTINGS_EDITOR_SMOKE_FAIL cases={cases} root={root} error={exception}");
                throw;
            }
        }

        private static void VerifyMissingFileUsesDefaults(string root)
        {
            LocalSettingsStore store = new LocalSettingsStore(Path.Combine(root, "missing.json"));
            SettingsResult<LocalSettings> result = Await(store.LoadSettings(CancellationToken.None));

            Require(result.IsSuccess, "Missing file load should succeed.");
            Require(result.RecoveredDefaults, "Missing file should initialize defaults.");
            Require(result.RecoveryReason == SettingsRecoveryReason.MissingFile, "Missing file reason should be MissingFile.");
            Require(result.Value.Equals(LocalSettings.Defaults), "Missing file should produce defaults.");
            Require(Mathf.Approximately(LocalSettings.Defaults.MouseSensitivity, 0.12f), "Default sensitivity must stay 0.12.");
        }

        private static void VerifySaveAndLoadRoundtrip(string root)
        {
            string path = Path.Combine(root, "roundtrip.json");
            LocalSettingsStore store = new LocalSettingsStore(path);
            LocalSettings custom = new LocalSettings(
                LocalSettings.CurrentSchemaVersion,
                0.25f,
                0.5f,
                false,
                "slot-a");

            SettingsResult<Unit> saved = Await(store.SaveSettings(custom, CancellationToken.None));
            Require(saved.IsSuccess, "Valid settings should save.");
            Require(File.Exists(path), "Saved settings file should exist.");

            SettingsResult<LocalSettings> loaded = Await(store.LoadSettings(CancellationToken.None));
            Require(loaded.IsSuccess, "Saved settings should load.");
            Require(!loaded.RecoveredDefaults, "Valid settings must not be recovered.");
            Require(loaded.Value.Equals(custom), $"Roundtrip mismatch: {loaded.Value} != {custom}");
        }

        private static void VerifyCorruptedFileRecoversDefaults(string root)
        {
            string path = Path.Combine(root, "corrupted.json");
            File.WriteAllText(path, "not a settings document");
            LocalSettingsStore store = new LocalSettingsStore(path);

            SettingsResult<LocalSettings> result = Await(store.LoadSettings(CancellationToken.None));
            Require(result.IsSuccess, "Corrupt data should not block startup.");
            Require(result.RecoveredDefaults, "Corrupt data should recover defaults.");
            Require(result.RecoveryReason == SettingsRecoveryReason.CorruptedData, "Corrupt data reason should be CorruptedData.");
            Require(result.Value.Equals(LocalSettings.Defaults), "Corrupt data should produce defaults.");
        }

        private static void VerifyOldAndFutureSchemasRecoverDefaults(string root)
        {
            string path = Path.Combine(root, "schema.json");
            LocalSettingsStore store = new LocalSettingsStore(path);

            File.WriteAllText(path, "{\"schemaVersion\":0,\"masterVolume\":0.5,\"mouseSensitivity\":0.25,\"fullscreen\":true}");
            SettingsResult<LocalSettings> old = Await(store.LoadSettings(CancellationToken.None));
            Require(old.IsSuccess && old.RecoveredDefaults, "Old schema should recover defaults.");
            Require(old.RecoveryReason == SettingsRecoveryReason.UnsupportedSchema, "Old schema reason should be UnsupportedSchema.");
            Require(old.Value.Equals(LocalSettings.Defaults), "Old schema should produce defaults.");

            File.WriteAllText(path, "{\"schemaVersion\":2,\"masterVolume\":0.5,\"mouseSensitivity\":0.25,\"fullscreen\":true}");
            SettingsResult<LocalSettings> future = Await(store.LoadSettings(CancellationToken.None));
            Require(future.IsSuccess && future.RecoveredDefaults, "Future schema should recover defaults.");
            Require(future.RecoveryReason == SettingsRecoveryReason.UnsupportedSchema, "Future schema reason should be UnsupportedSchema.");
            Require(future.Value.Equals(LocalSettings.Defaults), "Future schema should produce defaults.");
        }

        private static void VerifyInvalidValuesRecoverDefaults(string root)
        {
            string path = Path.Combine(root, "invalid-values.json");
            File.WriteAllText(path,
                "{\"schemaVersion\":1,\"masterVolume\":1.5,\"mouseSensitivity\":0.25,\"fullscreen\":true}");
            LocalSettingsStore store = new LocalSettingsStore(path);

            SettingsResult<LocalSettings> result = Await(store.LoadSettings(CancellationToken.None));
            Require(result.IsSuccess && result.RecoveredDefaults, "Invalid values should recover defaults.");
            Require(result.RecoveryReason == SettingsRecoveryReason.InvalidValues, "Invalid values reason should be InvalidValues.");
            Require(result.Value.Equals(LocalSettings.Defaults), "Invalid values should produce defaults.");
        }

        private static void VerifyMissingSchemaRecoversDefaults(string root)
        {
            string path = Path.Combine(root, "missing-schema.json");
            File.WriteAllText(path, "{\"masterVolume\":0.5,\"mouseSensitivity\":0.25,\"fullscreen\":true}");
            LocalSettingsStore store = new LocalSettingsStore(path);

            SettingsResult<LocalSettings> result = Await(store.LoadSettings(CancellationToken.None));
            Require(result.IsSuccess && result.RecoveredDefaults, "Missing schema should recover defaults.");
            Require(result.RecoveryReason == SettingsRecoveryReason.UnsupportedSchema, "Missing schema reason should be UnsupportedSchema.");
            Require(result.Value.Equals(LocalSettings.Defaults), "Missing schema should produce defaults.");
        }

        private static void VerifyInvalidSaveIsRejected(string root)
        {
            string path = Path.Combine(root, "invalid-save.json");
            LocalSettingsStore store = new LocalSettingsStore(path);
            LocalSettings valid = new LocalSettings(
                LocalSettings.CurrentSchemaVersion,
                0.25f,
                0.5f,
                true,
                "slot-a");

            Require(Await(store.SaveSettings(valid, CancellationToken.None)).IsSuccess, "Setup save should succeed.");
            string before = File.ReadAllText(path);

            LocalSettings invalidRange = valid.WithMasterVolume(2f);
            SettingsResult<Unit> rejectedRange = Await(store.SaveSettings(invalidRange, CancellationToken.None));
            Require(!rejectedRange.IsSuccess, "Out-of-range save should fail.");
            Require(rejectedRange.ErrorCode == SettingsErrorCode.InvalidSettings, "Out-of-range save error should be InvalidSettings.");
            Require(File.ReadAllText(path) == before, "Rejected save must not modify the persisted file.");

            LocalSettings invalidSchema = new LocalSettings(0, 0.25f, 0.5f, true);
            SettingsResult<Unit> rejectedSchema = Await(store.SaveSettings(invalidSchema, CancellationToken.None));
            Require(!rejectedSchema.IsSuccess, "Unsupported schema save should fail.");
            Require(rejectedSchema.ErrorCode == SettingsErrorCode.UnsupportedSchema, "Unsupported schema save error should be UnsupportedSchema.");

            SettingsResult<LocalSettings> loaded = Await(store.LoadSettings(CancellationToken.None));
            Require(loaded.IsSuccess && loaded.Value.Equals(valid), "Rejected saves must preserve previous valid settings.");
        }

        private static void VerifyRuntimePreviewSaveAndRevert(string root)
        {
            string path = Path.Combine(root, "runtime.json");
            LocalSettingsStore store = new LocalSettingsStore(path);
            SettingsResult<LocalSettings> initialized = Await(LocalSettingsRuntime.InitializeAsync(store));

            Require(initialized.IsSuccess, "Runtime initialization should load defaults.");
            Require(LocalSettingsRuntime.IsInitialized, "Runtime should report initialized.");
            Require(LocalSettingsRuntime.Effective.Equals(LocalSettings.Defaults), "Runtime should start with defaults.");
            Require(LocalSettingsRuntime.Persisted.Equals(LocalSettings.Defaults), "Runtime persisted should start with defaults.");
            Require(!LocalSettingsRuntime.HasUnsavedChanges, "Defaults should not be marked unsaved.");
            Require(Mathf.Approximately(AudioListener.volume, LocalSettings.DefaultMasterVolume), "Default volume should apply.");
            Require(Mathf.Approximately(FirstPersonController.ActiveMouseSensitivity, LocalSettings.DefaultMouseSensitivity),
                "Default sensitivity should apply to the controller hook.");

            LocalSettings custom = new LocalSettings(
                LocalSettings.CurrentSchemaVersion,
                0.25f,
                0.5f,
                false,
                "slot-b");

            SettingsResult<LocalSettings> preview = LocalSettingsRuntime.Preview(custom);
            Require(preview.IsSuccess, "Valid preview should succeed.");
            Require(LocalSettingsRuntime.Effective.Equals(custom), "Preview should become effective.");
            Require(LocalSettingsRuntime.Persisted.Equals(LocalSettings.Defaults), "Preview must not change persisted values.");
            Require(LocalSettingsRuntime.HasUnsavedChanges, "Preview should be marked unsaved.");
            Require(Mathf.Approximately(AudioListener.volume, 0.25f), "Preview volume should apply immediately.");
            Require(Mathf.Approximately(FirstPersonController.ActiveMouseSensitivity, 0.5f),
                "Preview sensitivity should apply immediately.");

            SettingsResult<Unit> saved = Await(LocalSettingsRuntime.SaveAsync());
            Require(saved.IsSuccess, "Runtime save should succeed.");
            Require(LocalSettingsRuntime.Persisted.Equals(custom), "Save should promote effective values to persisted.");
            Require(!LocalSettingsRuntime.HasUnsavedChanges, "Save should clear unsaved state.");
            Require(Await(store.LoadSettings(CancellationToken.None)).Value.Equals(custom), "Runtime save should write the file.");

            LocalSettings changed = custom.WithMasterVolume(0.75f);
            Require(LocalSettingsRuntime.Preview(changed).IsSuccess, "Second preview should succeed.");
            Require(LocalSettingsRuntime.Effective.Equals(changed), "Second preview should be effective.");
            Require(LocalSettingsRuntime.HasUnsavedChanges, "Second preview should be unsaved.");

            LocalSettingsRuntime.RevertPreview();
            Require(LocalSettingsRuntime.Effective.Equals(custom), "Revert should restore persisted values.");
            Require(!LocalSettingsRuntime.HasUnsavedChanges, "Revert should clear unsaved state.");
            Require(Mathf.Approximately(AudioListener.volume, 0.25f), "Revert should reapply persisted volume.");
            Require(Mathf.Approximately(FirstPersonController.ActiveMouseSensitivity, 0.5f),
                "Revert should reapply persisted sensitivity.");

            SettingsResult<LocalSettings> reset = LocalSettingsRuntime.ResetToDefaultsPreview();
            Require(reset.IsSuccess, "Defaults preview should succeed.");
            Require(LocalSettingsRuntime.Effective.Equals(LocalSettings.Defaults), "Defaults preview should become effective.");
            Require(LocalSettingsRuntime.HasUnsavedChanges, "Defaults preview should be marked unsaved.");
            Require(Mathf.Approximately(AudioListener.volume, LocalSettings.DefaultMasterVolume),
                "Defaults preview should apply default volume.");
            Require(Mathf.Approximately(FirstPersonController.ActiveMouseSensitivity, LocalSettings.DefaultMouseSensitivity),
                "Defaults preview should apply default sensitivity.");

            SettingsResult<Unit> resetSaved = Await(LocalSettingsRuntime.ResetToDefaultsAndSaveAsync());
            Require(resetSaved.IsSuccess, "Defaults save should succeed.");
            Require(LocalSettingsRuntime.Persisted.Equals(LocalSettings.Defaults), "Defaults save should persist defaults.");
            Require(Await(store.LoadSettings(CancellationToken.None)).Value.Equals(LocalSettings.Defaults),
                "Defaults save should write defaults.");

            LocalSettings invalid = LocalSettings.Defaults.WithMouseSensitivity(2f);
            SettingsResult<LocalSettings> rejected = LocalSettingsRuntime.Preview(invalid);
            Require(!rejected.IsSuccess, "Invalid preview should fail.");
            Require(rejected.ErrorCode == SettingsErrorCode.InvalidSettings, "Invalid preview should report InvalidSettings.");
            Require(LocalSettingsRuntime.Effective.Equals(LocalSettings.Defaults), "Invalid preview must not change effective values.");
        }

        private static T Await<T>(Task<T> task)
        {
            return task.GetAwaiter().GetResult();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
