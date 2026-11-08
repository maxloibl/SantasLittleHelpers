using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace SantasLittleHelpers.Networking
{
    public sealed class LocalNetworkJoin : MonoBehaviour
    {
        private NetworkManager manager;
        private string address = "127.0.0.1";
        private ushort port = 7777;
        private string status = "Choose Host or Client. Both use localhost:7777.";

        private void Awake()
        {
            manager = FindFirstObjectByType<NetworkManager>();
            if (manager == null)
            {
                status = "NetworkManager is missing from this test scene.";
                enabled = false;
                return;
            }
            manager.OnClientConnectedCallback += OnConnected;
            manager.OnClientDisconnectCallback += OnDisconnected;
        }

        private void Start()
        {
            if (NetworkManager.Singleton != manager)
            {
                status = "NetworkManager singleton was not initialized.";
                Debug.LogError(status, manager);
                return;
            }
            string[] args = System.Environment.GetCommandLineArgs();
            if (System.Array.Exists(args, arg => arg == "--host")) StartHost();
            else if (System.Array.Exists(args, arg => arg == "--client")) StartClient();
        }

        private void OnDestroy()
        {
            if (manager == null) return;
            manager.OnClientConnectedCallback -= OnConnected;
            manager.OnClientDisconnectCallback -= OnDisconnected;
        }

        private void OnConnected(ulong clientId)
        {
            status = $"Connected: {clientId}";
            Debug.Log($"NET_CONNECTED clientId={clientId}");
        }

        private void OnDisconnected(ulong clientId)
        {
            status = $"Disconnected: {clientId}";
            Debug.Log($"NET_DISCONNECTED clientId={clientId}");
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16, 16, 360, 260), GUI.skin.box);
            GUILayout.Label("Network movement test");
            GUILayout.Label("Address (client only)");
            address = GUILayout.TextField(address);
            GUILayout.Label("Port");
            ushort.TryParse(GUILayout.TextField(port.ToString()), out port);
            GUI.enabled = !manager.IsListening;
            if (GUILayout.Button("Start Host")) StartHost();
            if (GUILayout.Button("Join as Client")) StartClient();
            GUI.enabled = manager.IsListening;
            if (GUILayout.Button("Disconnect / Stop Host")) manager.Shutdown();
            GUI.enabled = true;
            GUILayout.Label(status);
            GUILayout.Label("WASD move · mouse look · Esc release cursor");
            GUILayout.EndArea();
        }

        private void StartHost()
        {
            ConfigureTransport();
            status = manager.StartHost() ? "Host started. Waiting for clients." : "Host failed to start.";
        }

        private void StartClient()
        {
            ConfigureTransport();
            bool started = manager.StartClient();
            status = started ? "Connecting…" : "Client failed to start.";
            if (started && TryGetShutdownDelay(out float delay)) StartCoroutine(ShutdownNormallyAfter(delay));
        }

        private bool TryGetShutdownDelay(out float delay)
        {
            foreach (string arg in System.Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("--shutdown-after=") && float.TryParse(arg.Substring(17), out delay)) return true;
            }
            delay = 0f;
            return false;
        }

        private IEnumerator ShutdownNormallyAfter(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            Debug.Log("NET_NORMAL_SHUTDOWN requested=true");
            manager.Shutdown();
        }

        private void ConfigureTransport()
        {
            UnityTransport transport = manager.GetComponent<UnityTransport>();
            transport.SetConnectionData(address, port, "0.0.0.0");
        }
    }
}


