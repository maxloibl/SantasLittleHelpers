using System;
using System.Collections.Generic;
using SantasLittleHelpers.Networking;
using SantasLittleHelpers.Networking.Session;
using Unity.Netcode;
using UnityEngine;

namespace SantasLittleHelpers.Cabin
{
    /// <summary>Owns lobby admission, cabin spawn slots, and the host-only mission release barrier.</summary>
    public sealed class CabinPhaseController : NetworkBehaviour
    {
        public enum CabinPhase : byte { Cabin, Mission }

        private readonly NetworkVariable<CabinPhase> phase = new NetworkVariable<CabinPhase>(CabinPhase.Cabin);
        private readonly Dictionary<ulong, int> clientSlots = new Dictionary<ulong, int>();
        private readonly Dictionary<ulong, float> placementRetryAt = new Dictionary<ulong, float>();
        private readonly HashSet<ulong> readyClients = new HashSet<ulong>();
        [SerializeField] private CabinAnchors anchors;

        public CabinPhase Phase => phase.Value;
        public bool IsAdmissionOpen => phase.Value == CabinPhase.Cabin;
        public event Action<CabinPhase> PhaseChanged;

        /// <summary>Connect this session's join approval to the authoritative cabin phase before starting it.</summary>
        public void ConfigureJoinGate(SessionOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            options.IsJoinAllowed = () => IsAdmissionOpen;
        }

        public override void OnNetworkSpawn()
        {
            phase.OnValueChanged += OnPhaseChanged;
            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
                foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
                    AssignSlotWhenPlayerReady(client.ClientId);
            }
            else
            {
                NetworkManager.OnClientConnectedCallback += OnLocalClientConnected;
                if (NetworkManager.IsConnectedClient) OnLocalClientConnected(NetworkManager.LocalClientId);
            }
        }

        public override void OnNetworkDespawn()
        {
            phase.OnValueChanged -= OnPhaseChanged;
            if (NetworkManager == null) return;
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.OnClientConnectedCallback -= OnLocalClientConnected;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || phase.Value != CabinPhase.Cabin) return;
            // PlayerObjects can be created just after the connection callback; retry until placement is delivered.
            foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
                if (!readyClients.Contains(client.ClientId)) AssignSlotWhenPlayerReady(client.ClientId);
        }

        /// <summary>Starts the mission exactly once, when called by the host after every connected peer is ready.</summary>
        public bool TryStartMission()
        {
            if (!IsSpawned || !IsServer || phase.Value != CabinPhase.Cabin) return false;
            int connectedPeers = 0;
            foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
            {
                if (!readyClients.Contains(client.ClientId)) return false;
                if (client.ClientId == NetworkManager.ServerClientId) continue;
                connectedPeers++;
                if (!readyClients.Contains(client.ClientId)) return false;
            }
            if (connectedPeers == 0) return false;

            // This phase change also closes SessionOptions.IsJoinAllowed synchronously before any later approval.
            phase.Value = CabinPhase.Mission;
            Debug.Log($"CABIN_MISSION_RELEASE host={NetworkManager.ServerClientId} peers={connectedPeers}");
            return true;
        }

        public bool TryGetAssignedSlot(ulong clientId, out int slot) => clientSlots.TryGetValue(clientId, out slot);
        public bool IsClientReady(ulong clientId) => readyClients.Contains(clientId);

        private void OnClientConnected(ulong clientId)
        {
            if (phase.Value != CabinPhase.Cabin && clientId != NetworkManager.ServerClientId)
            {
                Debug.Log($"CABIN_LATE_CONNECTION_REJECT client={clientId}");
                NetworkManager.DisconnectClient(clientId);
                return;
            }
            AssignSlotWhenPlayerReady(clientId);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (clientSlots.Remove(clientId, out int releasedSlot))
                Debug.Log($"CABIN_SLOT_RELEASE client={clientId} slot={releasedSlot}");
            placementRetryAt.Remove(clientId);
            readyClients.Remove(clientId);
        }

        private void OnLocalClientConnected(ulong clientId)
        {
            if (IsServer || clientId != NetworkManager.LocalClientId) return;
            // Server sends placement once the PlayerObject exists; this is only a diagnostic hook.
            Debug.Log($"CABIN_LOCAL_CONNECTED client={clientId}");
        }

        private void AssignSlotWhenPlayerReady(ulong clientId)
        {
            if (!IsServer || NetworkManager == null || !NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) ||
                client.PlayerObject == null || !client.PlayerObject.IsSpawned) return;

            if (!clientSlots.TryGetValue(clientId, out int slot))
            {
                slot = FindFreeSlot();
                if (slot < 0) return;
                clientSlots.Add(clientId, slot);
                Debug.Log($"CABIN_SLOT_ASSIGN client={clientId} slot={slot}");
            }

            Transform anchor = anchors != null && anchors.PlayerSpawnAnchors != null && slot < anchors.PlayerSpawnAnchors.Length
                ? anchors.PlayerSpawnAnchors[slot] : null;
            if (anchor == null)
            {
                Debug.LogError($"CABIN_ANCHOR_MISSING slot={slot}");
                return;
            }

            ApplyPose(client.PlayerObject, anchor.position, anchor.rotation);
            if (clientId == NetworkManager.ServerClientId)
            {
                readyClients.Add(clientId);
            }
            else
            {
                if (placementRetryAt.TryGetValue(clientId, out float nextRetry) && Time.unscaledTime < nextRetry) return;
                placementRetryAt[clientId] = Time.unscaledTime + 0.5f;
                SetPlayerPoseOwnerRpc(new NetworkObjectReference(client.PlayerObject), slot, anchor.position, anchor.rotation);
            }
        }

        private int FindFreeSlot()
        {
            bool[] occupied = new bool[4];
            foreach (int slot in clientSlots.Values)
                if (slot >= 0 && slot < occupied.Length) occupied[slot] = true;
            for (int i = 0; i < occupied.Length; i++) if (!occupied[i]) return i;
            return -1;
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void SetPlayerPoseOwnerRpc(NetworkObjectReference reference, int slot, Vector3 position,
            Quaternion rotation, RpcParams rpcParams = default)
        {
            if (!reference.TryGet(out NetworkObject player) || !player.IsOwner ||
                player.OwnerClientId != NetworkManager.LocalClientId) return;
            ApplyPose(player, position, rotation);
            Debug.Log($"CABIN_POSE_APPLIED owner={player.OwnerClientId} slot={slot} position={position}");
            ReportReadyServerRpc(slot, new NetworkObjectReference(player));
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ReportReadyServerRpc(int slot, NetworkObjectReference playerReference, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (!IsServer || !clientSlots.TryGetValue(sender, out int assignedSlot) || assignedSlot != slot ||
                !NetworkManager.ConnectedClients.TryGetValue(sender, out NetworkClient client) ||
                client.PlayerObject == null || !playerReference.TryGet(out NetworkObject player) ||
                client.PlayerObject.NetworkObjectId != player.NetworkObjectId || player.OwnerClientId != sender) return;
            readyClients.Add(sender);
            placementRetryAt.Remove(sender);
            Debug.Log($"CABIN_PEER_READY client={sender} slot={slot} position={player.transform.position}");
        }

        public bool TryPlaceSoloPlayer(Transform player)
        {
            if (IsSpawned || player == null || anchors == null || anchors.PlayerSpawnAnchors == null ||
                anchors.PlayerSpawnAnchors.Length == 0 || anchors.PlayerSpawnAnchors[0] == null) return false;
            Transform anchor = anchors.PlayerSpawnAnchors[0];
            ApplyPose(player, anchor.position, anchor.rotation);
            return true;
        }

        private static void ApplyPose(NetworkObject player, Vector3 position, Quaternion rotation)
        {
            ApplyPose(player.transform, position, rotation);
            if (player.IsOwner && player.TryGetComponent(out OwnerNetworkTransform ownerTransform))
                ownerTransform.Teleport(position, rotation, player.transform.lossyScale);
        }

        private static void ApplyPose(Transform player, Vector3 position, Quaternion rotation)
        {
            player.TryGetComponent(out CharacterController character);
            bool controllerEnabled = character != null && character.enabled;
            if (controllerEnabled) character.enabled = false;
            player.SetPositionAndRotation(position, rotation);
            if (controllerEnabled) character.enabled = true;
        }

        private void OnPhaseChanged(CabinPhase previous, CabinPhase current)
        {
            PhaseChanged?.Invoke(current);
            Debug.Log($"CABIN_PHASE old={previous} new={current} localClient={NetworkManager.LocalClientId}");
        }
    }
}
