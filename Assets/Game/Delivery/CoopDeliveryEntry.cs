using System;
using System.Collections;
using SantasLittleHelpers.AI;
using SantasLittleHelpers.Capture;
using SantasLittleHelpers.Networking;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace SantasLittleHelpers.Delivery
{
    /// <summary>Offers Solo, Host, and Client from DeliveryHouse; creates networking only after a choice.</summary>
    public sealed class CoopDeliveryEntry : MonoBehaviour
    {
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private GameObject lightGiftPrefab;
        [SerializeField] private GameObject heavyGiftPrefab;
        [SerializeField] private GameObject authorityPrefab;
        [SerializeField] private GameObject residentPrefab;
        [SerializeField] private DeliveryRoundController soloRound;
        [SerializeField] private GameObject soloPlayer;
        [SerializeField] private ResidentBrain[] residents = Array.Empty<ResidentBrain>();
        [SerializeField] private Transform sleigh;
        [SerializeField] private Collider deliveryTrigger;
        private GiftCarryable[] soloGifts;
        [SerializeField] private string address = "127.0.0.1";
        [SerializeField] private ushort port = 7777;
        private NetworkManager manager;
        private bool menu = true;
        private string message = "Choose Solo, Host, or Client. Local host and client use 127.0.0.1:7777.";

        private void Awake()
        {
            if (soloRound == null) soloRound = FindFirstObjectByType<DeliveryRoundController>();
            if (soloPlayer == null) soloPlayer = GameObject.Find("LocalPlayer — Sleigh Start");
            if (residents == null || residents.Length == 0) residents = FindObjectsByType<ResidentBrain>(FindObjectsSortMode.None);
            soloGifts = FindObjectsByType<GiftCarryable>(FindObjectsSortMode.None);
        }

        private void Start()
        {
            if (soloRound != null) soloRound.enabled = false;
            if (soloPlayer != null) soloPlayer.SetActive(false);
            SetSoloGifts(false);
            string[] args = Environment.GetCommandLineArgs();
            foreach (string arg in args)
            {
                if (arg.StartsWith("--address=")) address = arg.Substring(10);
                else if (arg.StartsWith("--port=") && ushort.TryParse(arg.Substring(7), out ushort parsedPort)) port = parsedPort;
            }
            if (Array.Exists(args, arg => arg == "--host")) StartNetwork(true);
            else if (Array.Exists(args, arg => arg == "--client")) StartNetwork(false);
            if (Array.Exists(args, arg => arg == "--solo-smoke"))
            {
                StartSolo(); StartCoroutine(RunSoloSmoke());
                if (TryGetShutdownDelay(out float soloDelay)) StartCoroutine(ShutdownNormallyAfter(soloDelay));
            }
        }

        private void OnGUI()
        {
            if (!menu) return;
            GUILayout.BeginArea(new Rect(16, 16, 420, 270), GUI.skin.box);
            GUILayout.Label("Weihnachts Friendslop — DeliveryHouse");
            GUILayout.Label("Solo keeps the original one-player round. Coop starts two players at the sleigh.");
            GUILayout.Label("WASD move · mouse look · E grab/drop/rescue · Shift run · Ctrl/C crouch · Space jump · Esc free cursor.");
            GUILayout.Label("Host and Client can connect directly on 127.0.0.1:7777, or enter a host address below.");
            if (GUILayout.Button("Play Solo")) StartSolo();
            GUILayout.Space(8);
            if (GUILayout.Button("Start Host")) StartNetwork(true);
            address = GUILayout.TextField(address);
            ushort.TryParse(GUILayout.TextField(port.ToString()), out port);
            if (GUILayout.Button("Join as Client")) StartNetwork(false);
            GUILayout.Label(message);
            GUILayout.EndArea();
        }

        private void StartSolo()
        {
            menu = false;
            if (soloPlayer != null) soloPlayer.SetActive(true);
            if (soloRound != null) soloRound.enabled = true;
            SetSoloGifts(true);
            message = "Solo round started.";
        }

        private IEnumerator RunSoloSmoke()
        {
            yield return null;
            GiftCarryable gift = soloRound != null ? soloRound.RequiredGift : null;
            GiftInteractor interactor = soloPlayer != null ? soloPlayer.GetComponent<GiftInteractor>() : null;
            GiftDeliveryZone zone = deliveryTrigger != null ? deliveryTrigger.GetComponent<GiftDeliveryZone>() : null;
            bool pickedUp = gift != null && interactor != null && gift.TryPickUp(interactor);
            if (!pickedUp || zone == null)
            {
                Debug.Log("SOLO_SMOKE_FAIL setup-or-pickup=false");
                yield break;
            }
            Vector3 deliveryPosition = deliveryTrigger.bounds.center;
            gift.transform.position = deliveryPosition;
            Physics.SyncTransforms();
            bool delivered = zone.TryHandleGift(gift);
            Debug.Log($"SOLO_SMOKE_DELIVERY delivered={delivered} state={soloRound.State} position={deliveryPosition}");
            if (sleigh != null)
            {
                Vector3 returnPosition = sleigh.position; returnPosition.y = .05f;
                soloPlayer.transform.SetPositionAndRotation(returnPosition, Quaternion.identity);
            }
            soloRound.CheckForReturn();
            bool completed = soloRound.State == DeliveryRoundState.Complete;
            soloRound.RestartRound();
            bool reset = soloRound.State == DeliveryRoundState.DeliverGift && !soloRound.IsDelivered;
            Debug.Log($"SOLO_SMOKE_PASS pickedUp={pickedUp} delivered={delivered} returned={completed} reset={reset}");
        }

        private void StartNetwork(bool host)
        {
            if (playerPrefab == null || lightGiftPrefab == null || heavyGiftPrefab == null || authorityPrefab == null || residentPrefab == null)
            { message = "Coop setup is incomplete. Rebuild the DeliveryHouse coop setup in Unity."; return; }
            if (soloPlayer != null) soloPlayer.SetActive(false);
            if (soloRound != null) soloRound.enabled = false;
            SetSoloGifts(false);
            foreach (ResidentBrain resident in residents) if (resident != null) resident.gameObject.SetActive(false);
            manager = new GameObject("DeliveryHouse Network Manager").AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig();
            UnityTransport transport = manager.gameObject.AddComponent<UnityTransport>();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = playerPrefab;
            manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = lightGiftPrefab });
            manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = heavyGiftPrefab });
            manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = authorityPrefab });
            manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = residentPrefab });
            manager.OnServerStarted += OnServerStarted;
            transport.SetConnectionData(address, port, "0.0.0.0");
            bool started = host ? manager.StartHost() : manager.StartClient();
            message = started ? (host ? "Host started. Waiting for one client." : "Connecting to host…") : "Connection could not be started.";
            if (!started) { Destroy(manager.gameObject); manager = null; }
            menu = !started;
            if (started && TryGetShutdownDelay(out float delay)) StartCoroutine(ShutdownNormallyAfter(delay));
        }

        private bool TryGetShutdownDelay(out float delay)
        {
            foreach (string arg in Environment.GetCommandLineArgs())
                if (arg.StartsWith("--shutdown-after=") && float.TryParse(arg.Substring(17), out delay)) return true;
            delay = 0f;
            return false;
        }

        private IEnumerator ShutdownNormallyAfter(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            Debug.Log("COOP_NORMAL_SHUTDOWN requested=true");
            if (manager != null) manager.Shutdown();
            Application.Quit();
        }

        private void OnServerStarted()
        {
            bool quietSmoke = Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "--coop-quiet-smoke");
            var networkResidents = new ResidentBrain[quietSmoke ? 0 : residents.Length];
            for (int i = 0; i < networkResidents.Length; i++)
            {
                Vector3 position = residents[i] != null ? residents[i].transform.position : Vector3.zero;
                GameObject replica = Instantiate(residentPrefab, position, Quaternion.identity);
                NetworkObject residentObject = replica.GetComponent<NetworkObject>();
                residentObject.Spawn();
                ResidentBrain brain = replica.GetComponent<ResidentBrain>();
                if (residents[i] != null) brain.CopyRoundConfigurationFrom(residents[i]);
                brain.Target = null;
                replica.AddComponent<HostResidentTarget>();
                networkResidents[i] = brain;
            }
            GameObject capture = Instantiate(authorityPrefab);
            capture.name = "Coop Capture Round";
            NetworkObject networkObject = capture.GetComponent<NetworkObject>();
            CoopNetworkRound round = capture.GetComponent<CoopNetworkRound>();
            round.Initialize(lightGiftPrefab, heavyGiftPrefab, sleigh, deliveryTrigger, networkResidents);
            networkObject.Spawn();
            foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList) round.InitializePlayer(client.ClientId);
            Debug.Log("COOP_HOST_READY players=1 gifts=2 port=" + port);
        }

        private void SetSoloGifts(bool active)
        {
            if (soloGifts == null) return;
            foreach (GiftCarryable gift in soloGifts) if (gift != null) gift.gameObject.SetActive(active);
        }

    }

    /// <summary>Host-only bridge from the real resident brain to live players and capture.</summary>
    [DisallowMultipleComponent]
    public sealed class HostResidentTarget : MonoBehaviour
    {
        private ResidentBrain resident;
        private void Awake() => resident = GetComponent<ResidentBrain>();
        private void Update()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || resident == null) return;
            CoopNetworkRound mission = FindFirstObjectByType<CoopNetworkRound>();
            if (mission != null && mission.IsTerminal) { resident.Target = null; return; }
            CapturedPlayerState nearest = null;
            float distance = float.MaxValue;
            foreach (NetworkClient client in manager.ConnectedClientsList)
            {
                CapturedPlayerState player = client.PlayerObject != null ? client.PlayerObject.GetComponent<CapturedPlayerState>() : null;
                if (player == null || !player.IsSpawned || player.IsCaptured) continue;
                float candidate = (player.transform.position - resident.transform.position).sqrMagnitude;
                if (candidate < distance) { nearest = player; distance = candidate; }
            }
            resident.Target = nearest != null ? nearest.transform : null;
            if (nearest != null) CaptureRoundCoordinator.Instance?.TryCaptureServer(resident, nearest);
        }
    }
}
