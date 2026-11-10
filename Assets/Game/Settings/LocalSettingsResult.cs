using System;

namespace SantasLittleHelpers.Settings
{
    public enum SettingsErrorCode
    {
        None = 0,
        InvalidSettings = 1,
        UnsupportedSchema = 2,
        StorageUnavailable = 3,
        CorruptedData = 4,
        Cancelled = 5
    }

    public enum SettingsRecoveryReason
    {
        None = 0,
        MissingFile = 1,
        UnsupportedSchema = 2,
        CorruptedData = 3,
        InvalidValues = 4
    }

    public readonly struct SettingsError
    {
        public SettingsError(SettingsErrorCode code, string message)
        {
            Code = code;
            Message = message ?? string.Empty;
        }

        public SettingsErrorCode Code { get; }
        public string Message { get; }
        public bool IsNone => Code == SettingsErrorCode.None;

        public override string ToString() => IsNone ? "None" : $"{Code}: {Message}";
    }

    public readonly struct Unit
    {
        public static Unit Value => default;
    }

    public readonly struct SettingsResult<T>
    {
        private SettingsResult(
            bool isSuccess,
            T value,
            SettingsError error,
            bool recoveredDefaults,
            SettingsRecoveryReason recoveryReason)
        {
            IsSuccess = isSuccess;
            Value = value;
            Error = error;
            RecoveredDefaults = recoveredDefaults;
            RecoveryReason = recoveryReason;
        }

        public bool IsSuccess { get; }
        public T Value { get; }
        public SettingsError Error { get; }
        public bool RecoveredDefaults { get; }
        public SettingsRecoveryReason RecoveryReason { get; }
        public SettingsErrorCode ErrorCode => Error.Code;
        public bool HasError => !IsSuccess;

        public static SettingsResult<T> Success(
            T value,
            SettingsRecoveryReason recoveryReason = SettingsRecoveryReason.None)
        {
            return new SettingsResult<T>(
                true,
                value,
                default,
                recoveryReason != SettingsRecoveryReason.None,
                recoveryReason);
        }

        public static SettingsResult<T> Failure(SettingsErrorCode code, string message)
        {
            return new SettingsResult<T>(false, default, new SettingsError(code, message), false,
                SettingsRecoveryReason.None);
        }

        public override string ToString()
        {
            return IsSuccess ? $"Success({Value})" : Error.ToString();
        }
    }
}
