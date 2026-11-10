using System;

namespace SantasLittleHelpers.Savegames
{
    public enum SlotErrorCode
    {
        None = 0,
        InvalidSlot,
        InvalidName,
        SlotNotFound,
        StorageUnavailable,
        CorruptData,
        UnsupportedSchema,
        CommitConflict,
        InvalidCommit,
        Cancelled
    }

    public readonly struct Unit
    {
        public static Unit Value => default;
    }

    public readonly struct SlotResult<T>
    {
        private readonly T value;
        private readonly string errorMessage;

        private SlotResult(bool isSuccess, bool isIdempotent, T value, SlotErrorCode error, string errorMessage)
        {
            IsSuccess = isSuccess;
            IsIdempotent = isIdempotent;
            this.value = value;
            Error = error;
            this.errorMessage = errorMessage ?? string.Empty;
        }

        public bool IsSuccess { get; }

        public bool IsFailure => !IsSuccess;

        public bool IsIdempotent { get; }

        public bool AlreadyCommitted => IsIdempotent;

        public SlotErrorCode Error { get; }

        public SlotErrorCode ErrorCode => Error;

        public string ErrorMessage => errorMessage;

        public string Message => errorMessage;

        public T Value => value;

        public static SlotResult<T> Success(T value)
        {
            return new SlotResult<T>(true, false, value, SlotErrorCode.None, string.Empty);
        }

        public static SlotResult<T> Idempotent(T value)
        {
            return new SlotResult<T>(true, true, value, SlotErrorCode.None, string.Empty);
        }

        public static SlotResult<T> Failure(SlotErrorCode error, string message)
        {
            if (error == SlotErrorCode.None)
            {
                throw new ArgumentOutOfRangeException(nameof(error));
            }

            return new SlotResult<T>(false, false, default, error, message);
        }
    }
}
