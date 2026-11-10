using System;
using System.Threading.Tasks;

namespace SantasLittleHelpers.Home
{
    public enum HomeLaunchKind
    {
        Solo = 0,
        Host = 1,
        Join = 2
    }

    public enum HomeLaunchErrorCode
    {
        None = 0,
        InvalidSlot = 1,
        InvalidEndpoint = 2,
        Busy = 3,
        LaunchFailed = 4,
        Cancelled = 5
    }

    /// <summary>
    /// Typed request handed from the home screen to the session integration (#41).
    /// Join never carries a slot. Solo and Host always carry the selected host slot.
    /// </summary>
    public sealed class HomeLaunchRequest
    {
        private HomeLaunchRequest(
            HomeLaunchKind kind,
            Guid? slotId,
            string endpointText,
            int port)
        {
            Kind = kind;
            SlotId = slotId;
            EndpointText = endpointText ?? string.Empty;
            Port = port;
        }

        public HomeLaunchKind Kind { get; }

        public Guid? SlotId { get; }

        public string EndpointText { get; }

        public int Port { get; }

        public static HomeLaunchRequest Solo(Guid slotId)
        {
            return new HomeLaunchRequest(HomeLaunchKind.Solo, RequireSlot(slotId), string.Empty, 0);
        }

        public static HomeLaunchRequest Host(Guid slotId, string endpointText, int port)
        {
            return new HomeLaunchRequest(HomeLaunchKind.Host, RequireSlot(slotId), endpointText, port);
        }

        public static HomeLaunchRequest Join(string endpointText, int port)
        {
            return new HomeLaunchRequest(HomeLaunchKind.Join, null, endpointText, port);
        }

        public override string ToString()
        {
            return SlotId.HasValue
                ? $"kind={Kind} slot={SlotId.Value:D} endpoint={EndpointText} port={Port}"
                : $"kind={Kind} endpoint={EndpointText} port={Port}";
        }

        private static Guid RequireSlot(Guid slotId)
        {
            if (slotId == Guid.Empty)
            {
                throw new ArgumentException("Solo and Host launches require a host slot.", nameof(slotId));
            }

            return slotId;
        }
    }

    public readonly struct HomeLaunchResult
    {
        private HomeLaunchResult(bool isSuccess, HomeLaunchErrorCode error, string message)
        {
            IsSuccess = isSuccess;
            Error = error;
            Message = message ?? string.Empty;
        }

        public bool IsSuccess { get; }

        public bool IsFailure => !IsSuccess;

        public HomeLaunchErrorCode Error { get; }

        public string Message { get; }

        public static HomeLaunchResult Success(string message = null)
        {
            return new HomeLaunchResult(true, HomeLaunchErrorCode.None, message);
        }

        public static HomeLaunchResult Failure(HomeLaunchErrorCode error, string message)
        {
            if (error == HomeLaunchErrorCode.None)
            {
                throw new ArgumentOutOfRangeException(nameof(error));
            }

            return new HomeLaunchResult(false, error, message);
        }

        public override string ToString()
        {
            return IsSuccess ? "Success" : $"{Error}: {Message}";
        }
    }

    public delegate Task<HomeLaunchResult> HomeLaunchHandler(HomeLaunchRequest request);
}
