using System;
using System.Collections;
using System.Collections.Generic;
using SantasLittleHelpers.AI;
using SantasLittleHelpers.Capture;
using SantasLittleHelpers.Networking;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace SantasLittleHelpers.Delivery
{
    /// <summary>Offers Solo, Host, and Client from DeliveryHouse; creates networking only after a choice.</summary>
    public sealed class CoopDeliveryEntry : MonoBehaviour, IMissionAdapter
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
        private CoopNetworkRound activeNetworkRound;
        private readonly HashSet<NetworkManager> configuredManagers = new HashSet<NetworkManager>();
        private Guid activeRoundId;
        private int soloCompletionCount;
        private bool menu = true;
        private string message = "Choose Solo or host a round for up to three clients. Local players use 127.0.0.1:7777.";
        public event Action<MissionCompleted> Completed;
        public MissionSnapshot Current
        {
            get
            {
                MissionState state = MissionState.NotStarted;
                if (activeNetworkRound != null)
                {
                    switch (activeNetworkRound.CurrentPhase)
                    {
                        case 0: state = MissionState.Delivering; break;
                        case 1: state = MissionState.Returning; break;
                        case 2: state = MissionState.Completed; break;
                        case 3: state = MissionState.Failed; break;
                    }
                    return new MissionSnapshot(state, activeNetworkRound.CurrentRoundId, "delivery-v1",
                        activeNetworkRound.DeliveredCount, 1);
                }
                if (soloRound != null && soloRound.enabled)
                {
                    if (soloRound.State == DeliveryRoundState.Complete) state = MissionState.Completed;
                    else if (soloRound.State == DeliveryRoundState.ReturnToSleigh) state = MissionState.Returning;
                    else state = MissionState.Delivering;
                    return new MissionSnapshot(state, soloRound.RoundId, "delivery-v1", soloRound.IsDelivered ? 1 : 0, 1);
                }
                return new MissionSnapshot(state, activeRoundId, "delivery-v1", 0, 1);
            }
        }

        private void Awake()
        {
            if (soloRound == null) soloRound = FindFirstObjectByType<DeliveryRoundController>();
            if (soloPlayer == null) soloPlayer = GameObject.Find("LocalPlayer — Sleigh Start");
            if (residents == null || residents.Length == 0) residents = FindObjectsByType<ResidentBrain>(FindObjectsSortMode.None);
            soloGifts = FindObjectsByType<GiftCarryable>(FindObjectsSortMode.None);
            if (soloRound != null)
            {
                soloRound.RoundStarted += OnSoloRoundStarted;
                soloRound.Completed += OnSoloCompleted;
            }
        }

        private void Start()
        {
            PrepareAuthoredWorld();
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
            GUILayout.Label("Solo keeps the one-player round. Host supports up to three clients (four players total).");
            GUILayout.Label("WASD move · mouse look · E grab/drop/rescue · Shift run · Ctrl/C crouch · Space jump · Esc free cursor.");
            GUILayout.Label("For localhost, connect clients to 127.0.0.1 using the selected port.");
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
            if (BeginSoloRound(Guid.NewGuid())) message = "Solo round started.";
        }

        /// <summary>Enables the authored local mission world after an explicit solo choice.</summary>
        public bool BeginSoloRound(Guid roundId)
        {
            if (roundId == Guid.Empty || soloRound == null || soloPlayer == null) return false;
            if (soloRound.enabled && activeRoundId != Guid.Empty)
                return activeRoundId == roundId;
            if (activeNetworkRound != null && activeNetworkRound.IsSpawned) return false;
            activeRoundId = roundId;
            menu = false;
            if (soloRound.enabled == false) soloRound.enabled = true;
            if (!soloRound.BeginRound(roundId)) return false;
            soloPlayer.SetActive(true);
            SetSoloGifts(true);
            foreach (ResidentBrain resident in residents)
                if (resident != null)
                {
                    resident.gameObject.SetActive(true);
                    resident.enabled = true;
                }
            return true;
        }

        /// <summary>Registers the existing mission player and NGO gameplay prefabs on the session-owned manager.</summary>
        public bool ConfigureNetworkManager(NetworkManager target)
        {
            if (target == null || target.NetworkConfig == null || playerPrefab == null || lightGiftPrefab == null ||
                heavyGiftPrefab == null || authorityPrefab == null || residentPrefab == null) return false;
            if (configuredManagers.Contains(target)) return true;
            target.NetworkConfig.PlayerPrefab = playerPrefab;
            target.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = lightGiftPrefab });
            target.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = heavyGiftPrefab });
            target.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = authorityPrefab });
            target.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = residentPrefab });
            configuredManagers.Add(target);
            return true;
        }

        /// <summary>Disables preloaded authored solo gameplay on clients before the session starts.</summary>
        public void PrepareClientWorld()
        {
            menu = false;
            PrepareAuthoredWorld();
        }

        private void PrepareAuthoredWorld()
        {
            if (soloRound != null)
            {
                soloRound.DeactivateRound();
                soloRound.enabled = false;
            }
            if (soloPlayer != null) soloPlayer.SetActive(false);
            SetSoloGifts(false);
            foreach (ResidentBrain resident in residents)
                if (resident != null)
                {
                    resident.enabled = false;
                    resident.gameObject.SetActive(false);
                }
        }

        /// <summary>Activates the server-authoritative mission only after the session has passed its readiness barrier.</summary>
        public bool BeginServerRound(NetworkManager activeManager, Guid roundId)
        {
            if (activeManager == null || activeManager != NetworkManager.Singleton || !activeManager.IsListening ||
                !activeManager.IsServer || roundId == Guid.Empty || activeNetworkRound != null) return false;
            if (!ConfigureNetworkManagerReferencesAvailable()) return false;
            PrepareClientWorld();
            activeRoundId = roundId;
            var networkResidents = new ResidentBrain[residents.Length];
            for (int i = 0; i < networkResidents.Length; i++)
            {
                ResidentBrain source = residents[i];
                Vector3 position = source != null ? source.transform.position : Vector3.zero;
                GameObject replica = Instantiate(residentPrefab, position, Quaternion.identity);
                NetworkObject residentObject = replica.GetComponent<NetworkObject>();
                if (residentObject == null) { Destroy(replica); return false; }
                ResidentBrain brain = replica.GetComponent<ResidentBrain>();
                if (source != null) brain.CopyRoundConfigurationFrom(source);
                brain.Target = null;
                replica.AddComponent<HostResidentTarget>();
                residentObject.Spawn();
                networkResidents[i] = brain;
            }
            GameObject capture = Instantiate(authorityPrefab);
            capture.name = "Coop Capture Round";
            NetworkObject networkObject = capture.GetComponent<NetworkObject>();
            activeNetworkRound = capture.GetComponent<CoopNetworkRound>();
            if (networkObject == null || activeNetworkRound == null)
            {
                Destroy(capture);
                activeNetworkRound = null;
                return false;
            }
            activeNetworkRound.Initialize(lightGiftPrefab, heavyGiftPrefab, sleigh, deliveryTrigger, networkResidents);
            networkObject.Spawn();
            if (!activeNetworkRound.BeginServerRound(roundId)) return false;
            activeNetworkRound.Completed += OnNetworkCompleted;
            foreach (NetworkClient client in activeManager.ConnectedClientsList) activeNetworkRound.InitializePlayer(client.ClientId);
            Debug.Log($"MISSION_START_AUTHORIZED roundId={roundId} players={activeManager.ConnectedClientsList.Count}");
            Debug.Log("COOP_HOST_READY players=" + activeManager.ConnectedClientsList.Count + " gifts=2 port=" + port);
            return true;
        }

        /// <summary>Stops mission gameplay and local authored objects; SessionService owns NetworkManager shutdown.</summary>
        public bool Cancel()
        {
            bool stopped = true;
            if (activeNetworkRound != null && activeNetworkRound.IsSpawned && activeNetworkRound.IsServer)
            {
                stopped = activeNetworkRound.EndServerRound();
                activeNetworkRound = null;
            }
            if (soloRound != null) soloRound.DeactivateRound();
            PrepareClientWorld();
            return stopped;
        }

        private void OnSoloCompleted(Guid roundId)
        {
            soloCompletionCount++;
            var completed = new MissionCompleted(roundId, "delivery-v1", DateTimeOffset.UtcNow);
            Debug.Log($"MISSION_EVENT type=Completed roundId={roundId} peerId=solo");
            Completed?.Invoke(completed);
        }

        private void OnSoloRoundStarted(Guid roundId) => activeRoundId = roundId;

        private void OnNetworkCompleted(MissionCompleted completed) => Completed?.Invoke(completed);

        private bool ConfigureNetworkManagerReferencesAvailable() => playerPrefab != null && lightGiftPrefab != null &&
            heavyGiftPrefab != null && authorityPrefab != null && residentPrefab != null;

        private IEnumerator RunSoloSmoke()
        {
            yield return null;
            GiftCarryable gift = soloRound != null ? soloRound.RequiredGift : null;
            Guid originalRoundId = soloRound != null ? soloRound.RoundId : Guid.Empty;
            int completionCountBefore = soloCompletionCount;
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
            bool duplicateRejected = !zone.TryHandleGift(gift);
            Debug.Log($"SOLO_SMOKE_DELIVERY delivered={delivered} duplicateRejected={duplicateRejected} state={soloRound.State} position={deliveryPosition}");
            if (sleigh != null)
            {
                Vector3 returnPosition = sleigh.position; returnPosition.y = .05f;
                soloPlayer.transform.SetPositionAndRotation(returnPosition, Quaternion.identity);
            }
            soloRound.CheckForReturn();
            bool completed = soloRound.State == DeliveryRoundState.Complete;
            soloRound.RestartRound();
            bool emittedOnce = soloCompletionCount - completionCountBefore == 1;
            bool reset = soloRound.State == DeliveryRoundState.DeliverGift && !soloRound.IsDelivered && soloRound.RoundId != originalRoundId;
            bool passed = pickedUp && delivered && duplicateRejected && completed && emittedOnce && reset;
            Debug.Log($"SOLO_SMOKE_{(passed ? "PASS" : "FAIL")} pickedUp={pickedUp} delivered={delivered} duplicateRejected={duplicateRejected} returned={completed} completionEvents={soloCompletionCount - completionCountBefore} reset={reset}");
            if (!passed) Application.Quit(1);
        }

        private void StartNetwork(bool host)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton != manager)
            { message = "A session already owns the network manager."; return; }
            if (!ConfigureNetworkManagerReferencesAvailable())
            { message = "Coop setup is incomplete. Rebuild the DeliveryHouse coop setup in Unity."; return; }
            PrepareClientWorld();
            manager = new GameObject("DeliveryHouse Network Manager").AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig();
            UnityTransport transport = manager.gameObject.AddComponent<UnityTransport>();
            manager.NetworkConfig.NetworkTransport = transport;
            if (!ConfigureNetworkManager(manager))
            { Destroy(manager.gameObject); manager = null; message = "Coop setup is incomplete."; return; }
            manager.OnServerStarted += OnServerStarted;
            transport.SetConnectionData(address, port, "0.0.0.0");
            bool started = host ? manager.StartHost() : manager.StartClient();
            message = started ? (host ? "Host started. Waiting for up to three clients." : "Connecting to host…") : "Connection could not be started.";
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
            // Keep the original DeliveryHouse host button and CLI path immediate. Product session
            // flows call BeginServerRound explicitly only after their readiness barrier.
            BeginServerRound(NetworkManager.Singleton, Guid.NewGuid());
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
