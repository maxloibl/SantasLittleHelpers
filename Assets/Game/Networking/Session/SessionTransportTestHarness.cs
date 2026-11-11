using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace SantasLittleHelpers.Networking.Session
{
    public sealed class SessionTransportTestHarness : MonoBehaviour
    {
        [SerializeField] private GameObject playerPrefab;
        private SessionService service;
        private bool originalRunInBackground;
        private bool allowJoins = true;

        private async void Start()
        {
            string role = Argument("--session-role=");
            if (string.IsNullOrEmpty(role)) return;

            originalRunInBackground = Application.runInBackground;
            if (HasArgument("--session-expect-host-left"))
                Application.runInBackground = true;
            service = new SessionService();
            service.Changed += OnSessionChanged;
            string expectedError = Argument("--session-expect=");
            ushort port = ReadPort("--session-port=", 7871);
            SessionOptions options = CreateOptions();
            bool isHost = string.Equals(role, "host", StringComparison.OrdinalIgnoreCase);

            if (!isHost && HasArgument("--session-retry-port="))
            {
                ushort initialPort = ReadPort("--session-retry-port=", 7872);
                SessionResult first = await service.JoinAsync(new IPEndPoint(IPAddress.Loopback, initialPort), options);
                Debug.Log($"SESSION_TEST_FIRST_ATTEMPT success={first.Success} error={first.Error} managerClean={(service.ActiveManager == null)}");
                if (first.Success || service.ActiveManager != null)
                {
                    Application.Quit(2);
                    return;
                }
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            SessionResult result = isHost
                ? await service.StartHostAsync(new IPEndPoint(IPAddress.Any, port), options)
                : await service.JoinAsync(new IPEndPoint(IPAddress.Loopback, port), options);

            if (!result.Success)
            {
                bool expected = string.Equals(expectedError, result.Error.ToString(), StringComparison.OrdinalIgnoreCase);
                Debug.Log($"SESSION_TEST_EXPECTED pass={expected} role={role} error={result.Error} status={result.Snapshot.Status} message={result.Message}");
                Application.Quit(expected ? 0 : 2);
                return;
            }

            Debug.Log($"SESSION_TEST_RESULT role={role} status={result.Snapshot.Status} connected={result.Snapshot.ConnectedPlayers} max={result.Snapshot.MaxPlayers} manager={(service.ActiveManager != null ? 1 : 0)} backgroundBefore={originalRunInBackground} backgroundAfterStart={Application.runInBackground}");

            if (isHost && HasArgument("--session-close-joins-after="))
            {
                await Task.Delay(TimeSpan.FromSeconds(ReadSeconds("--session-close-joins-after=", 3f)));
                allowJoins = false;
                Debug.Log("SESSION_TEST_JOIN_GATE closed=true");
            }

            if (HasArgument("--session-restart"))
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
                SessionResult stopped = await service.ShutdownAsync();
                Debug.Log($"SESSION_TEST_LEAVE result={stopped.Success} managerClean={(service.ActiveManager == null)} backgroundRestored={(Application.runInBackground == originalRunInBackground)}");
                await Task.Delay(TimeSpan.FromSeconds(1));
                SessionResult restarted = await service.StartHostAsync(new IPEndPoint(IPAddress.Any, port), options);
                Debug.Log($"SESSION_TEST_RESTART result={restarted.Success} status={restarted.Snapshot.Status} manager={(service.ActiveManager != null ? 1 : 0)}");
                await Task.Delay(TimeSpan.FromSeconds(2));
                await service.ShutdownAsync();
                Application.Quit(restarted.Success ? 0 : 3);
                return;
            }

            if (HasArgument("--session-leave-after="))
            {
                await Task.Delay(TimeSpan.FromSeconds(ReadSeconds("--session-leave-after=", 2f)));
                SessionResult left = await service.ShutdownAsync();
                Debug.Log($"SESSION_TEST_LEAVE result={left.Success} managerClean={(service.ActiveManager == null)} backgroundRestored={(Application.runInBackground == originalRunInBackground)}");
                await Task.Delay(TimeSpan.FromSeconds(1));
                SessionResult rejoined = isHost
                    ? await service.StartHostAsync(new IPEndPoint(IPAddress.Any, port), options)
                    : await service.JoinAsync(new IPEndPoint(IPAddress.Loopback, port), options);
                Debug.Log($"SESSION_TEST_REJOIN result={rejoined.Success} status={rejoined.Snapshot.Status} manager={(service.ActiveManager != null ? 1 : 0)}");
                if (!rejoined.Success)
                {
                    Application.Quit(3);
                    return;
                }
            }

            if (HasArgument("--session-host-abort-after="))
            {
                await Task.Delay(TimeSpan.FromSeconds(ReadSeconds("--session-host-abort-after=", 5f)));
                SessionResult stopped = await service.ShutdownAsync();
                Debug.Log($"SESSION_TEST_HOST_ABORT result={stopped.Success} managerClean={(service.ActiveManager == null)}");
                await Task.Delay(TimeSpan.FromSeconds(1));
                Application.Quit(stopped.Success ? 0 : 3);
                return;
            }

            if (HasArgument("--session-expect-host-left"))
            {
                bool observed = await WaitForHostLeft(TimeSpan.FromSeconds(ReadSeconds("--session-hold-seconds=", 12f)));
                Debug.Log($"SESSION_TEST_HOST_LEFT pass={observed} managerClean={(service.ActiveManager == null)} status={service.Status}");
                Application.Quit(observed ? 0 : 4);
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(ReadSeconds("--session-hold-seconds=", 10f)));
            SessionResult shutdown = await service.ShutdownAsync();
            Debug.Log($"SESSION_TEST_SHUTDOWN result={shutdown.Success} managerClean={(service.ActiveManager == null)} backgroundRestored={(Application.runInBackground == originalRunInBackground)} backgroundNow={Application.runInBackground} backgroundExpected={originalRunInBackground}");
            Application.Quit(shutdown.Success ? 0 : 5);
        }

        private SessionOptions CreateOptions()
        {
            ushort version = ReadPort("--session-version=", 1);
            string compatibility = Argument("--session-key=");
            if (string.IsNullOrEmpty(compatibility)) compatibility = "slh-test-v1";
            int maxPlayers = ReadInt("--session-max=", 4);
            float timeoutSeconds = ReadSeconds("--session-timeout=", 8f);
            return new SessionOptions
            {
                ProtocolVersion = version,
                CompatibilityKey = compatibility,
                MaxPlayers = maxPlayers,
                ConnectionTimeout = TimeSpan.FromSeconds(timeoutSeconds),
                ApprovalReservationTimeout = TimeSpan.FromSeconds(10),
                IsJoinAllowed = () => allowJoins,
                ConfigureNetworkManager = manager =>
                {
                    manager.NetworkConfig.PlayerPrefab = playerPrefab;
                    manager.NetworkConfig.EnableSceneManagement = true;
                }
            };
        }

        private async Task<bool> WaitForHostLeft(TimeSpan timeout)
        {
            DateTime until = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < until)
            {
                if (service.Status == SessionStatus.HostLeft)
                {
                    while (DateTime.UtcNow < until && service.ActiveManager != null)
                        await Task.Delay(100);
                    return service.ActiveManager == null;
                }
                await Task.Delay(100);
            }
            return false;
        }

        private void OnSessionChanged(object sender, SessionEventArgs args)
        {
            Debug.Log($"SESSION_TEST_EVENT kind={args.Kind} status={args.Snapshot.Status} error={args.Snapshot.Error} players={args.Snapshot.ConnectedPlayers} clientId={args.ClientId}");
        }

        private static string Argument(string prefix)
        {
            string value = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            return value == null ? string.Empty : value.Substring(prefix.Length);
        }

        private static bool HasArgument(string prefix) =>
            Environment.GetCommandLineArgs().Any(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || a == prefix);

        private static ushort ReadPort(string prefix, ushort fallback) =>
            ushort.TryParse(Argument(prefix), NumberStyles.None, CultureInfo.InvariantCulture, out ushort value) ? value : fallback;

        private static int ReadInt(string prefix, int fallback) =>
            int.TryParse(Argument(prefix), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

        private static float ReadSeconds(string prefix, float fallback) =>
            float.TryParse(Argument(prefix), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : fallback;
    }
}
