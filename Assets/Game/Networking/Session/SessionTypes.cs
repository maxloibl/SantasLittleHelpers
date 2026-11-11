using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;

namespace SantasLittleHelpers.Networking.Session
{
    public enum SessionStatus
    {
        Idle,
        PreparingWorld,
        Starting,
        Connected,
        Rejected,
        TimedOut,
        HostLeft,
        Returning,
        Stopped,
        Failed
    }

    public enum SessionError
    {
        None,
        InvalidEndpoint,
        InvalidOptions,
        Busy,
        ScenePreparationFailed,
        StartFailed,
        ConnectionTimeout,
        SessionFull,
        IncompatibleVersion,
        SessionClosed,
        ConnectionFailed,
        HostLeft,
        Cancelled,
        ShutdownFailed
    }

    public enum SessionEventKind
    {
        StateChanged,
        Connected,
        PeerJoined,
        PeerLeft,
        JoinRejected,
        TimedOut,
        HostLeft,
        TransportFailure,
        SceneEvent,
        ShutdownCompleted
    }

    public sealed class SessionOptions
    {
        public ushort ProtocolVersion { get; set; } = 1;
        public string CompatibilityKey { get; set; } = "santas-little-helpers-v1";
        public int MaxPlayers { get; set; } = 4;
        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(8);
        public TimeSpan ApprovalReservationTimeout { get; set; } = TimeSpan.FromSeconds(10);
        public Func<bool> IsJoinAllowed { get; set; }
        public Func<CancellationToken, Task<bool>> PrepareLocalScenesAsync { get; set; }
        public Action<NetworkManager> ConfigureNetworkManager { get; set; }

        internal SessionError Validate(out string message)
        {
            if (ProtocolVersion == 0)
            {
                message = "ProtocolVersion must be greater than zero.";
                return SessionError.InvalidOptions;
            }
            if (string.IsNullOrWhiteSpace(CompatibilityKey) || CompatibilityKey.Length > 64 ||
                CompatibilityKey.IndexOf('\n') >= 0 || CompatibilityKey.IndexOf('\r') >= 0)
            {
                message = "CompatibilityKey must contain 1 to 64 characters and no line breaks.";
                return SessionError.InvalidOptions;
            }
            if (MaxPlayers < 1 || MaxPlayers > 4)
            {
                message = "MaxPlayers must be from one through four, including the host.";
                return SessionError.InvalidOptions;
            }
            if (ConnectionTimeout < TimeSpan.FromSeconds(1) || ConnectionTimeout > TimeSpan.FromSeconds(60))
            {
                message = "ConnectionTimeout must be between one and sixty seconds.";
                return SessionError.InvalidOptions;
            }
            if (ApprovalReservationTimeout < TimeSpan.FromSeconds(1) || ApprovalReservationTimeout > TimeSpan.FromSeconds(30))
            {
                message = "ApprovalReservationTimeout must be between one and thirty seconds.";
                return SessionError.InvalidOptions;
            }

            message = string.Empty;
            return SessionError.None;
        }
    }

    public sealed class SessionSnapshot
    {
        public SessionStatus Status { get; }
        public SessionError Error { get; }
        public string Message { get; }
        public bool IsHost { get; }
        public int ConnectedPlayers { get; }
        public int MaxPlayers { get; }
        public IPEndPoint Endpoint { get; }

        internal SessionSnapshot(SessionStatus status, SessionError error, string message, bool isHost,
            int connectedPlayers, int maxPlayers, IPEndPoint endpoint)
        {
            Status = status;
            Error = error;
            Message = message ?? string.Empty;
            IsHost = isHost;
            ConnectedPlayers = connectedPlayers;
            MaxPlayers = maxPlayers;
            Endpoint = endpoint;
        }
    }

    public sealed class SessionResult
    {
        public bool Success { get; }
        public SessionSnapshot Snapshot { get; }
        public SessionError Error { get; }
        public string Message { get; }

        private SessionResult(bool success, SessionSnapshot snapshot, SessionError error, string message)
        {
            Success = success;
            Snapshot = snapshot;
            Error = error;
            Message = message ?? string.Empty;
        }

        internal static SessionResult Succeeded(SessionSnapshot snapshot) =>
            new SessionResult(true, snapshot, SessionError.None, string.Empty);

        internal static SessionResult Failed(SessionSnapshot snapshot, SessionError error, string message) =>
            new SessionResult(false, snapshot, error, message);
    }

    public sealed class SessionEventArgs : EventArgs
    {
        public SessionEventKind Kind { get; }
        public SessionSnapshot Snapshot { get; }
        public ulong ClientId { get; }

        internal SessionEventArgs(SessionEventKind kind, SessionSnapshot snapshot, ulong clientId = ulong.MaxValue)
        {
            Kind = kind;
            Snapshot = snapshot;
            ClientId = clientId;
        }
    }

    public sealed class SessionSceneEventArgs : EventArgs
    {
        public SceneEventType EventType { get; }
        public string SceneName { get; }
        public ulong ClientId { get; }

        internal SessionSceneEventArgs(SceneEventType eventType, string sceneName, ulong clientId)
        {
            EventType = eventType;
            SceneName = sceneName ?? string.Empty;
            ClientId = clientId;
        }
    }
}
