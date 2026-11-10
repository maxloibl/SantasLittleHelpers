using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SantasLittleHelpers.Settings
{
    public sealed class LocalSettingsStore
    {
        private const string TemporarySuffix = ".tmp";
        private readonly object gate = new object();

        public LocalSettingsStore(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("A settings file path is required.", nameof(filePath));

            FilePath = Path.GetFullPath(filePath);
        }

        public string FilePath { get; }

        public static LocalSettingsStore CreateDefault()
        {
            return new LocalSettingsStore(Path.Combine(Application.persistentDataPath, "settings.json"));
        }

        public Task<SettingsResult<LocalSettings>> LoadSettings(CancellationToken ct)
        {
            return Task.FromResult(LoadSettingsCore(ct));
        }

        public Task<SettingsResult<Unit>> SaveSettings(LocalSettings settings, CancellationToken ct)
        {
            return Task.FromResult(SaveSettingsCore(settings, ct));
        }

        private SettingsResult<LocalSettings> LoadSettingsCore(CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                return SettingsResult<LocalSettings>.Failure(SettingsErrorCode.Cancelled, "Loading settings was cancelled.");

            lock (gate)
            {
                if (!File.Exists(FilePath))
                    return SettingsResult<LocalSettings>.Success(LocalSettings.Defaults,
                        SettingsRecoveryReason.MissingFile);

                string text;
                try
                {
                    text = File.ReadAllText(FilePath, Encoding.UTF8);
                }
                catch (Exception exception) when (IsStorageException(exception))
                {
                    return SettingsResult<LocalSettings>.Failure(SettingsErrorCode.StorageUnavailable,
                        $"Could not read settings from {FilePath}: {exception.Message}");
                }

                if (string.IsNullOrWhiteSpace(text))
                    return Recover(SettingsRecoveryReason.CorruptedData);

                SettingsDocument document;
                try
                {
                    document = JsonUtility.FromJson<SettingsDocument>(text);
                }
                catch (Exception)
                {
                    return Recover(SettingsRecoveryReason.CorruptedData);
                }

                if (document == null)
                    return Recover(SettingsRecoveryReason.CorruptedData);

                if (document.schemaVersion != LocalSettings.CurrentSchemaVersion)
                    return Recover(SettingsRecoveryReason.UnsupportedSchema);

                LocalSettings loaded = new LocalSettings(
                    document.schemaVersion,
                    document.masterVolume,
                    document.mouseSensitivity,
                    document.fullscreen,
                    document.lastSelection);

                if (!LocalSettings.TryValidateValues(loaded, out _, out _))
                    return Recover(SettingsRecoveryReason.InvalidValues);

                return SettingsResult<LocalSettings>.Success(loaded);
            }
        }

        private SettingsResult<Unit> SaveSettingsCore(LocalSettings settings, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                return SettingsResult<Unit>.Failure(SettingsErrorCode.Cancelled, "Saving settings was cancelled.");

            if (!LocalSettings.TryValidate(settings, out SettingsErrorCode error, out string message))
                return SettingsResult<Unit>.Failure(error, message);

            lock (gate)
            {
                string directory = Path.GetDirectoryName(FilePath);
                string temporaryPath = FilePath + "." + Guid.NewGuid().ToString("N") + TemporarySuffix;

                try
                {
                    if (string.IsNullOrEmpty(directory))
                        throw new IOException("The settings path has no parent directory.");

                    Directory.CreateDirectory(directory);
                    SettingsDocument document = new SettingsDocument
                    {
                        schemaVersion = settings.SchemaVersion,
                        masterVolume = settings.MasterVolume,
                        mouseSensitivity = settings.MouseSensitivity,
                        fullscreen = settings.Fullscreen,
                        lastSelection = settings.LastSelection
                    };

                    File.WriteAllText(temporaryPath, JsonUtility.ToJson(document, true), new UTF8Encoding(false));

                    if (File.Exists(FilePath))
                        File.Replace(temporaryPath, FilePath, null);
                    else
                        File.Move(temporaryPath, FilePath);

                    return SettingsResult<Unit>.Success(Unit.Value);
                }
                catch (Exception exception) when (IsStorageException(exception))
                {
                    TryDelete(temporaryPath);
                    return SettingsResult<Unit>.Failure(SettingsErrorCode.StorageUnavailable,
                        $"Could not save settings to {FilePath}: {exception.Message}");
                }
            }
        }

        private static SettingsResult<LocalSettings> Recover(SettingsRecoveryReason reason)
        {
            return SettingsResult<LocalSettings>.Success(LocalSettings.Defaults, reason);
        }

        private static bool IsStorageException(Exception exception)
        {
            return exception is IOException
                || exception is UnauthorizedAccessException
                || exception is NotSupportedException
                || exception is ArgumentException
                || exception is System.Security.SecurityException;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception)
            {
            }
        }

        [Serializable]
        private sealed class SettingsDocument
        {
            public int schemaVersion;
            public float masterVolume;
            public float mouseSensitivity;
            public bool fullscreen;
            public string lastSelection;
        }
    }
}
