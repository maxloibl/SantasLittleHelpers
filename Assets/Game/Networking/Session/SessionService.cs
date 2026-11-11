using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace SantasLittleHelpers.Networking.Session
{
    /// <summary>Owns one NGO/UTP manager and its direct IPv4 host/join lifecycle.</summary>
    public sealed class SessionService
    {
        private const int MaximumJoinPayloadBytes = 128;
        private const ushort NetworkProtocolVersion = 1;
        private readonly Dictionary<ulong, DateTime> approvalReservations = new Dictionary<ulong, DateTime>();
        private NetworkManager manager;
        private UnityTransport transport;
        private SessionOptions options;
        private IPEndPoint endpoint;
        private bool isHost;
        private bool startInProgress;
        private bool closing;
        private bool localJoinConnected;
        private bool originalRunInBackground;
        private bool ownsRunInBackground;
        private Task<SessionResult> activeAttempt;
        private CancellationTokenSource activeAttemptCancellation;
        private TaskCompletionSource<SessionResult> joinCompletion;
        private SessionStatus status = SessionStatus.Idle;
        private SessionError lastError = SessionError.None;
        private string statusMessage = string.Empty;

        public event EventHandler<SessionEventArgs> Changed;
        public event EventHandler<SessionSceneEventArgs> SceneEventReceived;

        public NetworkManager ActiveManager => manager;
        public bool IsActive => manager != null && manager.IsListening;
        public SessionStatus Status => status;
        public SessionSnapshot Current => CreateSnapshot();

        public Task<SessionResult> StartHostAsync(IPEndPoint listenEndpoint, SessionOptions sessionOptions,
            CancellationToken cancellationToken = default)
        {
            return BeginAttempt(true, listenEndpoint, sessionOptions, cancellationToken);
        }

        public Task<SessionResult> JoinAsync(IPEndPoint remoteEndpoint, SessionOptions sessionOptions,
            CancellationToken cancellationToken = default)
        {
            return BeginAttempt(false, remoteEndpoint, sessionOptions, cancellationToken);
        }

        public async Task<SessionResult> ShutdownAsync(CancellationToken cancellationToken = default)
        {
            if (startInProgress)
            {
                activeAttemptCancellation?.Cancel();
                Task<SessionResult> attempt = activeAttempt;
                if (attempt != null)
                {
                    try { await attempt; }
                    catch (Exception exception) { Debug.LogException(exception); }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (manager == null)
            {
                RestoreRunInBackground();
                SetStatus(SessionStatus.Stopped, SessionError.None, "Session stopped.", SessionEventKind.ShutdownCompleted);
                return SessionResult.Succeeded(Current);
            }

            SetStatus(SessionStatus.Returning, SessionError.None, "Closing session.");
            try
            {
                await DestroyManagerAsync();
                SetStatus(SessionStatus.Stopped, SessionError.None, "Session stopped.", SessionEventKind.ShutdownCompleted);
                return SessionResult.Succeeded(Current);
            }
            catch (Exception exception)
            {
                SetStatus(SessionStatus.Failed, SessionError.ShutdownFailed, exception.Message, SessionEventKind.TransportFailure);
                return SessionResult.Failed(Current, SessionError.ShutdownFailed, exception.Message);
            }
        }

        private Task<SessionResult> BeginAttempt(bool host, IPEndPoint requestedEndpoint, SessionOptions sessionOptions,
            CancellationToken cancellationToken)
        {
            if (startInProgress || manager != null)
                return Task.FromResult(SessionResult.Failed(Current, SessionError.Busy, "A session is already active or starting."));
            if (NetworkManager.Singleton != null)
                return Task.FromResult(SessionResult.Failed(Current, SessionError.Busy, "Another NetworkManager already owns this process."));

            SessionError validationError = ValidateEndpoint(requestedEndpoint, host, out string validationMessage);
            if (validationError != SessionError.None)
                return Task.FromResult(SessionResult.Failed(Current, validationError, validationMessage));
            if (sessionOptions == null)
                return Task.FromResult(SessionResult.Failed(Current, SessionError.InvalidOptions, "Session options are required."));
            validationError = sessionOptions.Validate(out validationMessage);
            if (validationError != SessionError.None)
                return Task.FromResult(SessionResult.Failed(Current, validationError, validationMessage));
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(SessionResult.Failed(Current, SessionError.Cancelled, "Session start was cancelled."));

            Task<SessionResult> attempt = StartAsync(host, requestedEndpoint, sessionOptions, cancellationToken);
            activeAttempt = attempt;
            return attempt;
        }

        private async Task<SessionResult> StartAsync(bool host, IPEndPoint requestedEndpoint, SessionOptions sessionOptions,
            CancellationToken cancellationToken)
        {
            startInProgress = true;
            isHost = host;
            options = sessionOptions;
            endpoint = CloneEndpoint(requestedEndpoint);
            localJoinConnected = false;
            closing = false;
            activeAttemptCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var cancelCompletion = new TaskCompletionSource<SessionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (activeAttemptCancellation.Token.Register(() => cancelCompletion.TrySetResult(
                       SessionResult.Failed(Current, SessionError.Cancelled, "Session start was cancelled."))))
            {
                try
                {
                    SetStatus(SessionStatus.PreparingWorld, SessionError.None, "Preparing local world.");
                    if (options.PrepareLocalScenesAsync != null)
                    {
                        bool prepared = await options.PrepareLocalScenesAsync(activeAttemptCancellation.Token);
                        if (!prepared)
                            return await FailStartAsync(SessionStatus.Failed, SessionError.ScenePreparationFailed,
                                "Local world preparation failed.");
                    }

                    if (activeAttemptCancellation.IsCancellationRequested)
                        return await FailStartAsync(SessionStatus.Stopped, SessionError.Cancelled, "Session start was cancelled.");

                    CreateManager(host, requestedEndpoint, options);
                    SetStatus(SessionStatus.Starting, SessionError.None, host ? "Starting host." : "Connecting to host.");
                    if (!host)
                    {
                        joinCompletion = new TaskCompletionSource<SessionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                    }

                    bool started = host ? manager.StartHost() : manager.StartClient();
                    if (!started)
                        return await FailStartAsync(SessionStatus.Failed, SessionError.StartFailed,
                            host ? "Host could not bind the requested port." : "Client transport could not be started.");

                    manager.SceneManager.OnSceneEvent += OnNetworkSceneEvent;

                    if (host)
                    {
                        SetStatus(SessionStatus.Connected, SessionError.None, "Host is listening.", SessionEventKind.Connected);
                        return SessionResult.Succeeded(Current);
                    }

                    Task timeout = Task.Delay(options.ConnectionTimeout);
                    Task completed = await Task.WhenAny(joinCompletion.Task, cancelCompletion.Task, timeout);
                    if (completed == joinCompletion.Task)
                    {
                        SessionResult joinResult = await joinCompletion.Task;
                        if (joinResult.Success) return joinResult;
                        return await FailStartAsync(ToTerminalStatus(joinResult.Error), joinResult.Error, joinResult.Message);
                    }
                    if (completed == cancelCompletion.Task)
                    {
                        SessionResult cancelResult = await cancelCompletion.Task;
                        return await FailStartAsync(SessionStatus.Stopped, cancelResult.Error, cancelResult.Message);
                    }

                    SetStatus(SessionStatus.TimedOut, SessionError.ConnectionTimeout, "Connection attempt timed out.", SessionEventKind.TimedOut);
                    return await FailStartAsync(SessionStatus.TimedOut, SessionError.ConnectionTimeout, "Connection attempt timed out.");
                }
                catch (OperationCanceledException)
                {
                    return await FailStartAsync(SessionStatus.Stopped, SessionError.Cancelled, "Session start was cancelled.");
                }
                catch (Exception exception)
                {
                    return await FailStartAsync(SessionStatus.Failed, SessionError.StartFailed, exception.Message);
                }
                finally
                {
                    startInProgress = false;
                    activeAttemptCancellation.Dispose();
                    activeAttemptCancellation = null;
                    activeAttempt = null;
                    joinCompletion = null;
                }
            }
        }

        private void CreateManager(bool host, IPEndPoint requestedEndpoint, SessionOptions sessionOptions)
        {
            originalRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            ownsRunInBackground = true;

            var managerObject = new GameObject("Session NetworkManager");
            UnityEngine.Object.DontDestroyOnLoad(managerObject);
            manager = managerObject.AddComponent<NetworkManager>();
            transport = managerObject.AddComponent<UnityTransport>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport };

            sessionOptions.ConfigureNetworkManager?.Invoke(manager);
            if (manager.NetworkConfig == null) manager.NetworkConfig = new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.ProtocolVersion = NetworkProtocolVersion;
            manager.NetworkConfig.ClientConnectionBufferTimeout = Math.Max(1,
                (int)Math.Ceiling(sessionOptions.ApprovalReservationTimeout.TotalSeconds));
            manager.NetworkConfig.ConnectionData = host ? Array.Empty<byte>() : BuildJoinPayload(sessionOptions);
            if (!host && manager.NetworkConfig.ConnectionData.Length > MaximumJoinPayloadBytes)
                throw new InvalidOperationException("Session join payload exceeds the protocol limit.");

            string address = host ? IPAddress.Loopback.ToString() : requestedEndpoint.Address.ToString();
            string listenAddress = host ? requestedEndpoint.Address.ToString() : IPAddress.Any.ToString();
            transport.SetConnectionData(true, address, (ushort)requestedEndpoint.Port, listenAddress);

            manager.ConnectionApprovalCallback += OnConnectionApproval;
            manager.OnClientConnectedCallback += OnClientConnected;
            manager.OnClientDisconnectCallback += OnClientDisconnected;
            manager.OnTransportFailure += OnTransportFailure;
        }

        private void OnConnectionApproval(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = true;
            response.Approved = false;
            response.Reason = SessionError.IncompatibleVersion.ToString();

            if (request.ClientNetworkId == NetworkManager.ServerClientId)
            {
                response.Approved = true;
                response.Reason = string.Empty;
                return;
            }

            if (options?.IsJoinAllowed != null)
            {
                bool joinAllowed;
                try { joinAllowed = options.IsJoinAllowed(); }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    joinAllowed = false;
                }

                if (!joinAllowed)
                {
                    response.Reason = SessionError.SessionClosed.ToString();
                    Reject(request.ClientNetworkId, SessionError.SessionClosed);
                    return;
                }
            }

            PruneExpiredReservations();
            if (request.Payload == null || !TryReadJoinPayload(request.Payload, out ushort version, out string key) ||
                options == null || version != options.ProtocolVersion ||
                !string.Equals(key, options.CompatibilityKey, StringComparison.Ordinal))
            {
                Reject(request.ClientNetworkId, SessionError.IncompatibleVersion);
                return;
            }

            int connected = manager != null ? manager.ConnectedClientsList.Count : 0;
            int reserved = approvalReservations.Count;
            if (connected + reserved >= options.MaxPlayers)
            {
                response.Reason = SessionError.SessionFull.ToString();
                Reject(request.ClientNetworkId, SessionError.SessionFull);
                return;
            }

            approvalReservations[request.ClientNetworkId] = DateTime.UtcNow;
            response.Approved = true;
            response.Reason = string.Empty;
        }

        private void OnClientConnected(ulong clientId)
        {
            approvalReservations.Remove(clientId);
            if (manager == null) return;

            if (!isHost && manager.IsClient && clientId == manager.LocalClientId)
            {
                localJoinConnected = true;
                SetStatus(SessionStatus.Connected, SessionError.None, "Connected to host.", SessionEventKind.Connected, clientId);
                joinCompletion?.TrySetResult(SessionResult.Succeeded(Current));
                return;
            }

            if (isHost && clientId != NetworkManager.ServerClientId)
                Raise(SessionEventKind.PeerJoined, clientId);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            approvalReservations.Remove(clientId);
            if (manager == null || closing) return;

            if (!isHost && manager.IsClient &&
                (clientId == manager.LocalClientId || clientId == NetworkManager.ServerClientId))
            {
                SessionError error = localJoinConnected ? SessionError.HostLeft : MapDisconnectReason(manager.DisconnectReason);
                SessionStatus terminal = localJoinConnected ? SessionStatus.HostLeft : ToTerminalStatus(error);
                string message = string.IsNullOrEmpty(manager.DisconnectReason) ? error.ToString() : manager.DisconnectReason;
                SetStatus(terminal, error, message,
                    localJoinConnected ? SessionEventKind.HostLeft : SessionEventKind.JoinRejected, clientId);
                joinCompletion?.TrySetResult(SessionResult.Failed(Current, error, message));
                if (localJoinConnected) _ = CleanupUnexpectedDisconnectAsync();
                return;
            }

            if (isHost && clientId != NetworkManager.ServerClientId)
                Raise(SessionEventKind.PeerLeft, clientId);
        }

        private void OnTransportFailure()
        {
            if (manager == null || closing) return;
            SessionError error = isHost ? SessionError.ConnectionFailed :
                localJoinConnected ? SessionError.HostLeft : SessionError.ConnectionFailed;
            SessionStatus terminal = isHost ? SessionStatus.Failed :
                localJoinConnected ? SessionStatus.HostLeft : SessionStatus.Rejected;
            SetStatus(terminal, error, "Network transport failed.", SessionEventKind.TransportFailure);
            joinCompletion?.TrySetResult(SessionResult.Failed(Current, error, statusMessage));
            if (localJoinConnected || isHost) _ = CleanupUnexpectedDisconnectAsync();
        }

        private void OnNetworkSceneEvent(SceneEvent sceneEvent)
        {
            var handler = SceneEventReceived;
            if (handler == null) return;
            try
            {
                handler(this, new SessionSceneEventArgs(sceneEvent.SceneEventType, sceneEvent.SceneName, sceneEvent.ClientId));
            }
            catch (Exception exception) { Debug.LogException(exception); }
            Raise(SessionEventKind.SceneEvent, sceneEvent.ClientId);
        }

        private void Reject(ulong clientId, SessionError reason)
        {
            Raise(SessionEventKind.JoinRejected, clientId);
            Debug.Log($"SESSION_JOIN_REJECT clientId={clientId} reason={reason}");
        }

        private async Task<SessionResult> FailStartAsync(SessionStatus terminalStatus, SessionError error, string message)
        {
            if (manager != null)
            {
                try { await DestroyManagerAsync(); }
                catch (Exception exception)
                {
                    SetStatus(SessionStatus.Failed, SessionError.ShutdownFailed, exception.Message, SessionEventKind.TransportFailure);
                    return SessionResult.Failed(Current, SessionError.ShutdownFailed, exception.Message);
                }
            }

            SetStatus(terminalStatus, error, message, error == SessionError.SessionFull || error == SessionError.IncompatibleVersion || error == SessionError.SessionClosed
                ? SessionEventKind.JoinRejected : terminalStatus == SessionStatus.TimedOut ? SessionEventKind.TimedOut : SessionEventKind.StateChanged);
            return SessionResult.Failed(Current, error, message);
        }

        private async Task CleanupUnexpectedDisconnectAsync()
        {
            SessionStatus finalStatus = status;
            SessionError finalError = lastError;
            string finalMessage = statusMessage;
            try { await DestroyManagerAsync(); }
            catch (Exception exception) { Debug.LogException(exception); }
            status = finalStatus;
            lastError = finalError;
            statusMessage = finalMessage;
        }

        private async Task DestroyManagerAsync()
        {
            if (manager == null)
            {
                RestoreRunInBackground();
                return;
            }
            closing = true;
            NetworkManager oldManager = manager;
            GameObject managerObject = oldManager.gameObject;
            oldManager.ConnectionApprovalCallback -= OnConnectionApproval;
            oldManager.OnClientConnectedCallback -= OnClientConnected;
            oldManager.OnClientDisconnectCallback -= OnClientDisconnected;
            oldManager.OnTransportFailure -= OnTransportFailure;
            if (oldManager.SceneManager != null) oldManager.SceneManager.OnSceneEvent -= OnNetworkSceneEvent;
            oldManager.ConnectionApprovalCallback = null;
            approvalReservations.Clear();

            if (oldManager.IsListening)
            {
                var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                Action<bool> stoppedHandler = _ => stopped.TrySetResult(true);
                oldManager.OnClientStopped += stoppedHandler;
                oldManager.OnServerStopped += stoppedHandler;
                oldManager.Shutdown();

                Task completed = await Task.WhenAny(stopped.Task, Task.Delay(TimeSpan.FromSeconds(10)));
                oldManager.OnClientStopped -= stoppedHandler;
                oldManager.OnServerStopped -= stoppedHandler;
                if (completed != stopped.Task)
                    throw new TimeoutException("NetworkManager did not finish shutting down within ten seconds.");
            }

            manager = null;
            transport = null;
            joinCompletion = null;
            if (managerObject != null) UnityEngine.Object.Destroy(managerObject);
            await Task.Yield();
            await Task.Yield();

            RestoreRunInBackground();
            closing = false;
        }

        private void RestoreRunInBackground()
        {
            if (!ownsRunInBackground) return;
            Application.runInBackground = originalRunInBackground;
            ownsRunInBackground = false;
        }

        private void PruneExpiredReservations()
        {
            if (options == null || approvalReservations.Count == 0) return;
            DateTime cutoff = DateTime.UtcNow - options.ApprovalReservationTimeout;
            foreach (ulong id in approvalReservations.Where(pair => pair.Value <= cutoff).Select(pair => pair.Key).ToArray())
            {
                approvalReservations.Remove(id);
                Debug.Log($"SESSION_APPROVAL_RESERVATION_EXPIRED clientId={id}");
            }
        }

        private static byte[] BuildJoinPayload(SessionOptions sessionOptions)
        {
            string text = sessionOptions.ProtocolVersion + "\n" + sessionOptions.CompatibilityKey;
            return Encoding.UTF8.GetBytes(text);
        }

        private static bool TryReadJoinPayload(byte[] payload, out ushort protocolVersion, out string compatibilityKey)
        {
            protocolVersion = 0;
            compatibilityKey = string.Empty;
            if (payload == null || payload.Length == 0 || payload.Length > MaximumJoinPayloadBytes) return false;
            string text;
            try { text = Encoding.UTF8.GetString(payload); }
            catch { return false; }
            int separator = text.IndexOf('\n');
            if (separator <= 0 || separator != text.LastIndexOf('\n')) return false;
            if (!ushort.TryParse(text.Substring(0, separator), out protocolVersion)) return false;
            compatibilityKey = text.Substring(separator + 1);
            return compatibilityKey.Length > 0 && compatibilityKey.Length <= 64 && compatibilityKey.IndexOf('\r') < 0;
        }

        private static SessionError ValidateEndpoint(IPEndPoint requestedEndpoint, bool host, out string message)
        {
            if (requestedEndpoint == null || requestedEndpoint.Port < 1 || requestedEndpoint.Port > ushort.MaxValue ||
                requestedEndpoint.Address == null || requestedEndpoint.Address.AddressFamily != AddressFamily.InterNetwork)
            {
                message = "A valid IPv4 address and UDP port from 1 to 65535 are required.";
                return SessionError.InvalidEndpoint;
            }

            byte[] bytes = requestedEndpoint.Address.GetAddressBytes();
            bool multicast = bytes[0] >= 224 && bytes[0] <= 239;
            bool broadcast = requestedEndpoint.Address.Equals(IPAddress.Broadcast);
            if (broadcast || multicast || (!host && requestedEndpoint.Address.Equals(IPAddress.Any)))
            {
                message = "The endpoint must be a unicast IPv4 address.";
                return SessionError.InvalidEndpoint;
            }

            message = string.Empty;
            return SessionError.None;
        }

        public static bool TryParseJoinEndpoint(string addressText, string portText, out IPEndPoint parsedEndpoint,
            out SessionError error, out string message)
        {
            parsedEndpoint = null;
            error = SessionError.InvalidEndpoint;
            message = "Enter a direct IPv4 address and UDP port.";
            string[] octets = (addressText ?? string.Empty).Split('.');
            if (octets.Length != 4 || !byte.TryParse(octets[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out byte a) ||
                !byte.TryParse(octets[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out byte b) ||
                !byte.TryParse(octets[2], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out byte c) ||
                !byte.TryParse(octets[3], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out byte d))
            {
                message = "Join requires four numeric IPv4 octets; host names and IPv6 are not supported.";
                return false;
            }
            var address = new IPAddress(new[] { a, b, c, d });
            if (!ushort.TryParse(portText, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out ushort port) || port == 0)
            {
                message = "UDP port must be a number from 1 to 65535.";
                return false;
            }
            var endpoint = new IPEndPoint(address, port);
            error = ValidateEndpoint(endpoint, false, out message);
            if (error != SessionError.None) return false;
            parsedEndpoint = endpoint;
            return true;
        }

        private static IPEndPoint CloneEndpoint(IPEndPoint value) => new IPEndPoint(value.Address, value.Port);

        private static SessionError MapDisconnectReason(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return SessionError.ConnectionFailed;
            if (Contains(reason, SessionError.SessionFull.ToString())) return SessionError.SessionFull;
            if (Contains(reason, SessionError.SessionClosed.ToString())) return SessionError.SessionClosed;
            if (Contains(reason, SessionError.IncompatibleVersion.ToString()) || Contains(reason, "ProtocolVersion"))
                return SessionError.IncompatibleVersion;
            if (Contains(reason, "timeout") || Contains(reason, "MaxConnectionAttempts")) return SessionError.ConnectionTimeout;
            return SessionError.ConnectionFailed;
        }

        private static bool Contains(string value, string search) =>
            value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        private static SessionStatus ToTerminalStatus(SessionError error)
        {
            if (error == SessionError.ConnectionTimeout) return SessionStatus.TimedOut;
            if (error == SessionError.SessionFull || error == SessionError.SessionClosed || error == SessionError.IncompatibleVersion)
                return SessionStatus.Rejected;
            if (error == SessionError.HostLeft) return SessionStatus.HostLeft;
            return SessionStatus.Failed;
        }

        private SessionSnapshot CreateSnapshot()
        {
            int connected = manager != null && manager.IsListening ? manager.ConnectedClientsList.Count : 0;
            return new SessionSnapshot(status, lastError, statusMessage, isHost, connected,
                options != null ? options.MaxPlayers : 0, endpoint != null ? CloneEndpoint(endpoint) : null);
        }

        private void SetStatus(SessionStatus nextStatus, SessionError error, string message,
            SessionEventKind eventKind = SessionEventKind.StateChanged, ulong clientId = ulong.MaxValue)
        {
            status = nextStatus;
            lastError = error;
            statusMessage = message ?? string.Empty;
            Raise(eventKind, clientId);
        }

        private void Raise(SessionEventKind kind, ulong clientId = ulong.MaxValue)
        {
            var handler = Changed;
            if (handler == null) return;
            try { handler(this, new SessionEventArgs(kind, CreateSnapshot(), clientId)); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}
