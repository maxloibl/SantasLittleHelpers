using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using SantasLittleHelpers.Networking.Session;
using Unity.Netcode;
using UnityEngine;

namespace SantasLittleHelpers.Cabin.Tests
{
    /// <summary>Command-line driver for isolated Windows host/client process verification.</summary>
    public sealed class CabinMultiplayerTestHarness : MonoBehaviour
    {
        [SerializeField] private CabinPhaseController cabin;
        [SerializeField] private GameObject playerPrefab;
        private SessionService session;
        private bool host;
        private bool autoRelease;
        private bool started;
        private float nextAuditAt;
        private float autoReleaseAt;
        private int port;

        private async void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (!TryGetArg(args, "--cabin-role", out string role) ||
                !TryGetArg(args, "--cabin-port", out string portText) || !int.TryParse(portText, out port))
            {
                Debug.Log("CABIN_TEST_IDLE missing --cabin-role host|client or --cabin-port");
                return;
            }
            host = string.Equals(role, "host", StringComparison.OrdinalIgnoreCase);
            if (!host && !string.Equals(role, "client", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("CABIN_TEST_ERROR invalid role");
                return;
            }
            autoRelease = args.Contains("--cabin-auto-release");
            float releaseDelay = TryGetArg(args, "--cabin-release-delay", out string delayText) &&
                                 float.TryParse(delayText, System.Globalization.NumberStyles.Float,
                                     System.Globalization.CultureInfo.InvariantCulture, out float parsedDelay)
                ? Mathf.Max(0f, parsedDelay) : 0f;
            string address = TryGetArg(args, "--cabin-address", out string configuredAddress) ? configuredAddress : "127.0.0.1";
            if (!IPAddress.TryParse(address, out IPAddress ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                Debug.LogError("CABIN_TEST_ERROR invalid IPv4 address");
                return;
            }
            if (cabin == null || playerPrefab == null)
            {
                Debug.LogError("CABIN_TEST_ERROR missing cabin controller or player prefab");
                return;
            }

            session = new SessionService();
            session.Changed += (_, change) => Debug.Log($"CABIN_TEST_SESSION_EVENT kind={change.Kind} status={change.Snapshot.Status} client={change.ClientId}");
            var options = new SessionOptions
            {
                MaxPlayers = 4,
                ConfigureNetworkManager = manager => manager.NetworkConfig.PlayerPrefab = playerPrefab
            };
            cabin.ConfigureJoinGate(options);
            cabin.PhaseChanged += phase => Debug.Log($"CABIN_TEST_PHASE phase={phase}");
            started = true;
            SessionResult result = host
                ? await session.StartHostAsync(new IPEndPoint(ip, port), options)
                : await session.JoinAsync(new IPEndPoint(ip, port), options);
            Debug.Log($"CABIN_TEST_SESSION role={role} success={result.Success} status={result.Snapshot.Status} error={result.Error} message={result.Message}");
            if (result.Success && TryGetArg(args, "--cabin-leave-after", out string leaveText) &&
                int.TryParse(leaveText, out int leaveSeconds) && leaveSeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(leaveSeconds));
                await session.ShutdownAsync();
                Debug.Log("CABIN_TEST_LEAVE completed");
                Application.Quit();
                return;
            }
            if (result.Success && host && args.Contains("--cabin-auto-release-after-ready"))
            {
                autoRelease = true;
                autoReleaseAt = Time.unscaledTime + releaseDelay;
            }
        }

        private void Update()
        {
            if (!started || session?.ActiveManager == null) return;
            NetworkManager manager = session.ActiveManager;
            if (host && autoRelease && Time.unscaledTime >= autoReleaseAt && manager.ConnectedClientsList.Count >= 2)
            {
                bool ready = manager.ConnectedClientsList
                    .Where(client => client.ClientId != NetworkManager.ServerClientId)
                    .All(client => cabin.IsClientReady(client.ClientId));
                if (ready && cabin.TryStartMission()) Debug.Log("CABIN_TEST_AUTO_RELEASE result=started");
            }
            if (Time.unscaledTime < nextAuditAt) return;
            nextAuditAt = Time.unscaledTime + 1f;
            foreach (NetworkClient client in manager.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                string slot = cabin.TryGetAssignedSlot(client.ClientId, out int index) ? index.ToString() : "pending";
                Debug.Log($"CABIN_REPLICA_POSE observer={manager.LocalClientId} owner={client.ClientId} slot={slot} ready={cabin.IsClientReady(client.ClientId)} position={client.PlayerObject.transform.position} phase={cabin.Phase}");
            }
        }

        private static bool TryGetArg(string[] args, string key, out string value)
        {
            string prefix = key + "=";
            foreach (string arg in args)
            {
                if (arg.StartsWith(prefix, StringComparison.Ordinal))
                {
                    value = arg.Substring(prefix.Length);
                    return true;
                }
            }
            value = string.Empty;
            return false;
        }
    }
}
