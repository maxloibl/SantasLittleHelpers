using System.Collections.Generic;
using SantasLittleHelpers.AI;
using SantasLittleHelpers.Capture;
using SantasLittleHelpers.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SantasLittleHelpers.Delivery
{
    /// <summary>Replicated delivery/return state. Gift and capture mutations are server-owned.</summary>
    public sealed class CoopNetworkRound : NetworkBehaviour
    {
        private readonly NetworkVariable<int> phase = new NetworkVariable<int>(0);
        private readonly NetworkVariable<int> delivered = new NetworkVariable<int>(0);
        private GameObject lightPrefab;
        private GameObject heavyPrefab;
        private NetworkObject lightGift;
        private NetworkObject heavyGift;
        private Transform sleigh;
        private Collider deliveryTrigger;
        private ResidentBrain[] residents;
        private readonly Dictionary<ulong, Vector3> playerStarts = new Dictionary<ulong, Vector3>();
        private float smokeStartedAt;
        private bool smokeHeavyReported;
        private bool smokeCaptureAttempted;
        private bool smokeCaptureSeen;
        private float smokeCaptureAt;
        private bool smokeCaptureGripReleaseReported;
        private bool smokeCapturedGrabRejectReported;
        private bool smokeDeliveryPlaced;
        private bool smokeRestarted;
        private bool quietDeliveryReported;
        private bool quietReturnReported;
        public int CurrentPhase => phase.Value;
        public bool IsTerminal => phase.Value >= 2;

        public void Initialize(GameObject light, GameObject heavy, Transform home, Collider delivery, ResidentBrain[] brains)
        {
            lightPrefab = light; heavyPrefab = heavy; sleigh = home; deliveryTrigger = delivery; residents = brains;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                smokeStartedAt = Time.unscaledTime;
                SpawnMissionGifts();
                NetworkManager.OnClientConnectedCallback += OnClientConnected;
            }
            phase.OnValueChanged += OnPhaseChanged;
            delivered.OnValueChanged += OnDeliveredChanged;
        }

        public override void OnNetworkDespawn()
        {
            phase.OnValueChanged -= OnPhaseChanged; delivered.OnValueChanged -= OnDeliveredChanged;
            if (IsServer && NetworkManager != null) NetworkManager.OnClientConnectedCallback -= OnClientConnected;
        }

        private void OnClientConnected(ulong clientId)
        {
            InitializePlayer(clientId);
        }

        public void InitializePlayer(ulong clientId)
        {
            if (!IsServer) return;
            if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) || client.PlayerObject == null) return;
            Vector3 home = sleigh != null ? sleigh.position : new Vector3(0,0,-6.25f);
            home.y = .05f;
            Vector3 offset = clientId == NetworkManager.ServerClientId ? new Vector3(-.8f,0,1.75f) : new Vector3(.8f,0,1.75f);
            Vector3 start = home + offset;
            playerStarts[clientId] = start;
            Debug.Log($"COOP_PLAYER_START owner={clientId} position={start}");
        }

        private void Update()
        {
            if (!IsSpawned) return;
            if (IsServer) { TickServer(); TickSmoke(); }
            if (IsServer && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && phase.Value >= 2) ResetRoundServer();
        }

        private void TickServer()
        {
            CaptureRoundCoordinator capture = CaptureRoundCoordinator.Instance;
            if (phase.Value < 2 && capture != null && capture.RoundLost) phase.Value = 3;
            if (phase.Value == 0 && lightGift != null && deliveryTrigger != null && deliveryTrigger.bounds.Contains(lightGift.transform.position))
            {
                Vector3 deliveredAt = lightGift.transform.position;
                lightGift.GetComponent<NetworkGiftCarryable>()?.ForceReleaseOnServer();
                lightGift.Despawn(true); lightGift = null;
                delivered.Value = 1; phase.Value = 1;
                Debug.Log($"COOP_GIFT_DELIVERED index=light progress=1 trigger=true position={deliveredAt}");
            }
            if (phase.Value != 1 || sleigh == null) return;
            if (NetworkManager.ConnectedClientsList.Count < 2) return;
            foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
            {
                if (client.PlayerObject == null || Vector3.Distance(client.PlayerObject.transform.position, sleigh.position) > 2.3f) return;
                CapturedPlayerState captured = client.PlayerObject.GetComponent<CapturedPlayerState>();
                if (captured != null && captured.IsCaptured) return;
            }
            phase.Value = 2;
            Debug.Log("COOP_ROUND_COMPLETE players=" + NetworkManager.ConnectedClientsList.Count);
        }

        private void TickSmoke()
        {
            if (System.Array.Exists(System.Environment.GetCommandLineArgs(), arg => arg == "--coop-quiet-smoke"))
            {
                TickQuietSmoke();
                return;
            }
            if (!System.Array.Exists(System.Environment.GetCommandLineArgs(), arg => arg == "--coop-smoke") || NetworkManager.ConnectedClientsList.Count < 2) return;
            float elapsed = Time.unscaledTime - smokeStartedAt;
            NetworkGiftCarryable heavy = FindSmokeGift(true);
            if (!smokeHeavyReported && elapsed > 6f)
            {
                if (heavy != null && heavy.GripCount == 2)
                {
                    Debug.Log("COOP_SMOKE_HEAVY_TWO_GRIPS_PASS count=2");
                    smokeHeavyReported = true;
                }
                else if (elapsed > 9f)
                {
                    Debug.Log($"COOP_SMOKE_HEAVY_TWO_GRIPS_FAIL count={(heavy != null ? heavy.GripCount : -1)}");
                    smokeHeavyReported = true;
                }
            }
            if (!smokeCaptureAttempted && elapsed > 10f) TrySmokeCapture();
            if (smokeCaptureSeen && !smokeCaptureGripReleaseReported)
            {
                CapturedPlayerState capturedPlayer = FindCapturedPlayer();
                NetworkGiftCarryable heavyAfterCapture = FindSmokeGift(true);
                if (capturedPlayer != null && heavyAfterCapture != null && heavyAfterCapture.GripCount == 1 &&
                    !heavyAfterCapture.HasServerGrip(capturedPlayer.OwnerClientId))
                {
                    Debug.Log($"COOP_SMOKE_CAPTURE_GRIP_RELEASE_PASS captured={capturedPlayer.OwnerClientId} gripCount=1 partnerRetained=true");
                    smokeCaptureGripReleaseReported = true;
                }
            }
            if (smokeCaptureSeen && !smokeCapturedGrabRejectReported)
            {
                CapturedPlayerState capturedPlayer = FindCapturedPlayer();
                NetworkGiftCarryable heavyAfterCapture = FindSmokeGift(true);
                if (capturedPlayer != null && heavyAfterCapture != null && heavyAfterCapture.WasCapturedGrabRejected(capturedPlayer.OwnerClientId))
                {
                    Debug.Log($"COOP_SMOKE_CAPTURED_GRAB_REJECT_PASS client={capturedPlayer.OwnerClientId} gripCount={heavyAfterCapture.GripCount}");
                    smokeCapturedGrabRejectReported = true;
                }
            }
            if (smokeCaptureSeen && !smokeDeliveryPlaced && elapsed - smokeCaptureAt > 2f)
            {
                bool anyCaptured = false;
                foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
                    if (client.PlayerObject != null && client.PlayerObject.GetComponent<CapturedPlayerState>()?.IsCaptured == true) anyCaptured = true;
                if (!anyCaptured)
                {
                    Debug.Log("COOP_SMOKE_CAPTURE_RESCUE_PASS remainedFree=true");
                    if (lightGift != null && deliveryTrigger != null)
                    {
                        Vector3 position = deliveryTrigger.bounds.center;
                        Rigidbody body = lightGift.GetComponent<Rigidbody>();
                        if (body != null) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; body.position = position; }
                        lightGift.transform.position = position;
                        Debug.Log($"COOP_SMOKE_REPOSITION scenario=light-delivery position={position}");
                        smokeDeliveryPlaced = true;
                    }
                }
            }
            if (!smokeRestarted && phase.Value == 2 && elapsed > smokeCaptureAt + 7f)
            {
                ResetRoundServer();
                smokeRestarted = true;
                Debug.Log($"COOP_SMOKE_RESTART_PASS phase={phase.Value} delivered={delivered.Value} players={NetworkManager.ConnectedClientsList.Count}");
            }
        }

        private void TickQuietSmoke()
        {
            if (NetworkManager.ConnectedClientsList.Count < 2) return;
            if (phase.Value == 1 && !quietDeliveryReported)
            {
                CapturedPlayerState[] players = FindObjectsByType<CapturedPlayerState>();
                bool allFree = true;
                foreach (CapturedPlayerState player in players) if (player.IsSpawned && player.IsCaptured) allFree = false;
                if (allFree)
                {
                    Debug.Log($"COOP_QUIET_DELIVERY_PASS progress={delivered.Value} allPlayersFree=true");
                    quietDeliveryReported = true;
                }
            }
            if (phase.Value == 2 && !quietReturnReported)
            {
                bool allHome = true;
                foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
                    if (client.PlayerObject == null || Vector3.Distance(client.PlayerObject.transform.position, sleigh.position) > 2.3f ||
                        client.PlayerObject.GetComponent<CapturedPlayerState>()?.IsCaptured == true) allHome = false;
                if (allHome)
                {
                    Debug.Log($"COOP_QUIET_RETURN_PASS players={NetworkManager.ConnectedClientsList.Count} phase={phase.Value} progress={delivered.Value} allPlayersFree=true");
                    quietReturnReported = true;
                }
            }
        }

        private CapturedPlayerState FindCapturedPlayer()
        {
            foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
            {
                CapturedPlayerState player = client.PlayerObject != null ? client.PlayerObject.GetComponent<CapturedPlayerState>() : null;
                if (player != null && player.IsCaptured) return player;
            }
            return null;
        }

        private void TrySmokeCapture()
        {
            smokeCaptureAttempted = true;
            CapturedPlayerState player = null;
            foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
                if (client.ClientId != NetworkManager.ServerClientId && client.PlayerObject != null)
                { player = client.PlayerObject.GetComponent<CapturedPlayerState>(); break; }
            if (player == null && NetworkManager.ConnectedClients.TryGetValue(NetworkManager.ServerClientId, out NetworkClient host) && host.PlayerObject != null)
                player = host.PlayerObject.GetComponent<CapturedPlayerState>();
            if (player == null) { Debug.Log("COOP_SMOKE_CAPTURE_FAIL player=missing"); return; }
            foreach (ResidentBrain resident in residents)
            {
                if (resident == null) continue;
                Vector3 direction = player.transform.forward;
                direction.y = 0f;
                if (direction.sqrMagnitude < .01f) direction = Vector3.forward;
                resident.transform.SetPositionAndRotation(player.transform.position + direction.normalized * 1.8f, Quaternion.LookRotation(-direction.normalized));
                resident.Target = player.transform;
                resident.SimulateStep(1f);
                float captureDistance = Vector3.Distance(resident.transform.position, player.transform.position);
                Debug.Log($"COOP_SMOKE_RESIDENT_PROBE state={resident.State} distance={captureDistance:0.00} player={player.transform.position} resident={resident.transform.position}");
                if (!CaptureRoundCoordinator.Instance.TryCaptureServer(resident, player)) continue;
                smokeCaptureSeen = true;
                smokeCaptureAt = Time.unscaledTime - smokeStartedAt;
                Debug.Log($"COOP_SMOKE_CAPTURE_PASS residentState={resident.State} player={player.OwnerClientId} repositionedResident=true");
                return;
            }
            Debug.Log("COOP_SMOKE_CAPTURE_FAIL residentChaseOrReach=false");
        }

        private static NetworkGiftCarryable FindSmokeGift(bool heavy)
        {
            foreach (NetworkGiftCarryable gift in FindObjectsByType<NetworkGiftCarryable>())
                if (gift != null && gift.TryGetComponent(out Rigidbody body) && (body.mass >= 3f) == heavy) return gift;
            return null;
        }

        private void ResetRoundServer()
        {
            if (!IsServer) return;
            if (lightGift != null && lightGift.IsSpawned) { lightGift.GetComponent<NetworkGiftCarryable>()?.ForceReleaseOnServer(); lightGift.Despawn(true); }
            if (heavyGift != null && heavyGift.IsSpawned) { heavyGift.GetComponent<NetworkGiftCarryable>()?.ForceReleaseOnServer(); heavyGift.Despawn(true); }
            lightGift = null; heavyGift = null;
            CaptureRoundCoordinator.Instance?.ResetRoundServer();
            foreach (ResidentBrain resident in residents) if (resident != null) resident.ResetForRound();
            foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
                if (client.PlayerObject != null && playerStarts.TryGetValue(client.ClientId, out Vector3 start))
                {
                    NetworkObjectReference playerRef = new NetworkObjectReference(client.PlayerObject);
                    ResetPlayerOwnerRpc(playerRef, start);
                }
            delivered.Value = 0; phase.Value = 0;
            SpawnMissionGifts();
            Debug.Log("COOP_HOST_RESET complete=true");
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void ResetPlayerOwnerRpc(NetworkObjectReference reference, Vector3 position, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;
            if (!reference.TryGet(out NetworkObject player) || !player.IsOwner) return;
            player.transform.SetPositionAndRotation(position, Quaternion.identity);
            if (player.TryGetComponent(out OwnerNetworkTransform networkTransform))
                networkTransform.Teleport(position, Quaternion.identity, player.transform.lossyScale);
        }

        private void SpawnMissionGifts()
        {
            if (lightPrefab != null)
            {
                lightGift = Instantiate(lightPrefab, new Vector3(-1.1f,.5f,-3f), Quaternion.identity).GetComponent<NetworkObject>();
                lightGift.Spawn();
            }
            if (heavyPrefab != null)
            {
                heavyGift = Instantiate(heavyPrefab, new Vector3(1.1f,.5f,-3f), Quaternion.identity).GetComponent<NetworkObject>();
                heavyGift.Spawn();
            }
        }

        private void OnPhaseChanged(int oldValue, int newValue) => Debug.Log($"COOP_PHASE old={oldValue} new={newValue} delivered={delivered.Value} peer={NetworkManager.LocalClientId}");
        private void OnDeliveredChanged(int oldValue, int newValue) => Debug.Log($"COOP_PROGRESS delivered={newValue} peer={NetworkManager.LocalClientId}");
        private void OnGUI()
        {
            string label = phase.Value == 0 ? "Deliver the light gift" : phase.Value == 1 ? "Return together to the sleigh" : phase.Value == 2 ? "Round complete — host: R to restart" : "Everyone is captured — host: R to restart";
            GUI.Box(new Rect(16, 16, 360, 75), "Coop delivery · " + delivered.Value + "/1\n" + label + "\nWASD/mouse · E grab/drop/rescue · host: R restart");
        }
    }
}
