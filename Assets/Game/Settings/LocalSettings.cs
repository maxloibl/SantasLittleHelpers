using System;
using UnityEngine;

namespace SantasLittleHelpers.Settings
{
    [Serializable]
    public sealed class LocalSettings : IEquatable<LocalSettings>
    {
        public const int CurrentSchemaVersion = 1;
        public const float DefaultMasterVolume = 1f;
        public const float DefaultMouseSensitivity = 0.12f;
        public const bool DefaultFullscreen = true;
        public const float MinimumMasterVolume = 0f;
        public const float MaximumMasterVolume = 1f;
        public const float MinimumMouseSensitivity = 0.01f;
        public const float MaximumMouseSensitivity = 1f;
        public const int MaximumLastSelectionLength = 128;

        public LocalSettings(
            int schemaVersion,
            float masterVolume,
            float mouseSensitivity,
            bool fullscreen,
            string lastSelection = null)
        {
            SchemaVersion = schemaVersion;
            MasterVolume = masterVolume;
            MouseSensitivity = mouseSensitivity;
            Fullscreen = fullscreen;
            LastSelection = string.IsNullOrEmpty(lastSelection) ? null : lastSelection;
        }

        public int SchemaVersion { get; }
        public float MasterVolume { get; }
        public float MouseSensitivity { get; }
        public bool Fullscreen { get; }
        public string LastSelection { get; }

        public static LocalSettings Defaults { get; } = new LocalSettings(
            CurrentSchemaVersion,
            DefaultMasterVolume,
            DefaultMouseSensitivity,
            DefaultFullscreen);

        public LocalSettings WithMasterVolume(float value)
        {
            return new LocalSettings(SchemaVersion, value, MouseSensitivity, Fullscreen, LastSelection);
        }

        public LocalSettings WithMouseSensitivity(float value)
        {
            return new LocalSettings(SchemaVersion, MasterVolume, value, Fullscreen, LastSelection);
        }

        public LocalSettings WithFullscreen(bool value)
        {
            return new LocalSettings(SchemaVersion, MasterVolume, MouseSensitivity, value, LastSelection);
        }

        public LocalSettings WithLastSelection(string value)
        {
            return new LocalSettings(SchemaVersion, MasterVolume, MouseSensitivity, Fullscreen, value);
        }

        public static bool TryValidate(LocalSettings settings, out SettingsErrorCode error, out string message)
        {
            if (settings == null)
            {
                error = SettingsErrorCode.InvalidSettings;
                message = "Settings are missing.";
                return false;
            }

            if (settings.SchemaVersion != CurrentSchemaVersion)
            {
                error = SettingsErrorCode.UnsupportedSchema;
                message = $"Unsupported settings schema {settings.SchemaVersion}; expected {CurrentSchemaVersion}.";
                return false;
            }

            return TryValidateValues(settings, out error, out message);
        }

        public static bool TryValidateValues(LocalSettings settings, out SettingsErrorCode error, out string message)
        {
            if (settings == null)
            {
                error = SettingsErrorCode.InvalidSettings;
                message = "Settings are missing.";
                return false;
            }

            if (!IsInRange(settings.MasterVolume, MinimumMasterVolume, MaximumMasterVolume))
            {
                error = SettingsErrorCode.InvalidSettings;
                message = $"Master volume {settings.MasterVolume} is outside {MinimumMasterVolume}..{MaximumMasterVolume}.";
                return false;
            }

            if (!IsInRange(settings.MouseSensitivity, MinimumMouseSensitivity, MaximumMouseSensitivity))
            {
                error = SettingsErrorCode.InvalidSettings;
                message = $"Mouse sensitivity {settings.MouseSensitivity} is outside {MinimumMouseSensitivity}..{MaximumMouseSensitivity}.";
                return false;
            }

            if (settings.LastSelection != null && settings.LastSelection.Length > MaximumLastSelectionLength)
            {
                error = SettingsErrorCode.InvalidSettings;
                message = $"Last selection exceeds {MaximumLastSelectionLength} characters.";
                return false;
            }

            error = SettingsErrorCode.None;
            message = string.Empty;
            return true;
        }

        public bool Equals(LocalSettings other)
        {
            return other != null
                && SchemaVersion == other.SchemaVersion
                && MasterVolume.Equals(other.MasterVolume)
                && MouseSensitivity.Equals(other.MouseSensitivity)
                && Fullscreen == other.Fullscreen
                && string.Equals(LastSelection, other.LastSelection, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => Equals(obj as LocalSettings);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SchemaVersion;
                hash = (hash * 397) ^ MasterVolume.GetHashCode();
                hash = (hash * 397) ^ MouseSensitivity.GetHashCode();
                hash = (hash * 397) ^ Fullscreen.GetHashCode();
                hash = (hash * 397) ^ (LastSelection != null ? LastSelection.GetHashCode() : 0);
                return hash;
            }
        }

        public override string ToString()
        {
            return $"schema={SchemaVersion} volume={MasterVolume} sensitivity={MouseSensitivity} fullscreen={Fullscreen} lastSelection={LastSelection ?? "none"}";
        }

        private static bool IsInRange(float value, float minimum, float maximum)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;
        }
    }
}
