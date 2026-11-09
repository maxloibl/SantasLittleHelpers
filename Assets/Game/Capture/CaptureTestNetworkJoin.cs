using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using System.Collections;

namespace SantasLittleHelpers.Capture
{
    /// <summary>Starts the isolated Capture multiplayer test on its dedicated localhost port.</summary>
    [DisallowMultipleComponent]
    public sealed class CaptureTestNetworkJoin : MonoBehaviour
    {
        public const ushort Port = 7780;
        private NetworkManager manager;

        private void Awake()
        {
            manager = FindAnyObjectByType<NetworkManager>();
            if (manager == null) Debug.LogError("Capture test NetworkManager is missing.", this);
            else
            {
                manager.OnClientConnectedCallback += LogConnected;
                manager.OnClientDisconnectCallback += LogDisconnected;
            }
        }

        private void Start()
        {
            if (manager == null || NetworkManager.Singleton != manager) return;
            string[] args = System.Environment.GetCommandLineArgs();
            if (System.Array.Exists(args, arg => arg == "--host")) StartHost();
            else if (System.Array.Exists(args, arg => arg == "--client")) StartClient();
            if (TryGetShutdownDelay(args, out float delay)) StartCoroutine(ShutdownAfter(delay));
        }

        private void OnDestroy()
        {
            if (manager == null) return;
            manager.OnClientConnectedCallback -= LogConnected;
            manager.OnClientDisconnectCallback -= LogDisconnected;
        }

        private void StartHost()
        {
            ConfigureTransport(true);
            bool started = manager.StartHost();
            Debug.Log($"CAPTURE_NET_START role=host port={Port} started={started}");
        }

        private void StartClient()
        {
            ConfigureTransport(false);
            bool started = manager.StartClient();
            Debug.Log($"CAPTURE_NET_START role=client port={Port} started={started}");
        }

        private IEnumerator ShutdownAfter(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            Debug.Log("CAPTURE_NET_SHUTDOWN requested=true");
            manager.Shutdown();
        }

        private static bool TryGetShutdownDelay(string[] args, out float delay)
        {
            foreach (string arg in args)
                if (arg.StartsWith("--shutdown-after=") && float.TryParse(arg.Substring(17), out delay)) return true;
            delay = 0f;
            return false;
        }

        private void ConfigureTransport(bool host)
        {
            UnityTransport transport = manager.GetComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", Port, host ? "0.0.0.0" : "127.0.0.1");
        }

        private static void LogConnected(ulong id) => Debug.Log($"CAPTURE_NET_CONNECTED clientId={id}");
        private static void LogDisconnected(ulong id) => Debug.Log($"CAPTURE_NET_DISCONNECTED clientId={id}");
    }
}
