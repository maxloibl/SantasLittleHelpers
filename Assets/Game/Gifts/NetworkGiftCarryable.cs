using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SantasLittleHelpers
{
    /// <summary>Server-owned grip arbitration and Rigidbody control for a network gift.</summary>
    [RequireComponent(typeof(NetworkObject), typeof(Rigidbody), typeof(GiftCarryable))]
    public sealed class NetworkGiftCarryable : NetworkBehaviour
    {
        [SerializeField, Min(0.1f)] private float reach = 2.5f;
        [SerializeField, Min(0.1f)] private float followResponsiveness = 12f;
        [SerializeField, Min(0f)] private float heavyMass = 3f;
        [SerializeField, Min(0.1f)] private float soloHeavySpeed = 0.35f;
        [SerializeField, Min(0.1f)] private float twoPlayerHeavySpeed = 1f;
        private readonly HashSet<ulong> gripSlots = new HashSet<ulong>();
        private readonly NetworkList<ulong> gripOwners = new NetworkList<ulong>(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private Rigidbody body;
        private GiftCarryable carryable;
        private NetworkVariable<int> gripCount = new NetworkVariable<int>(0);

        public int GripCount => gripCount.Value;
        public bool HasLocalGrip => IsSpawned && NetworkManager != null && gripOwners.Contains(NetworkManager.LocalClientId);

        /// <summary>Host hook for capture, delivery, or scripted drops.</summary>
        public bool ForceReleaseOnServer()
        {
            if (!IsServer || !IsSpawned) return false;
            ClearGrips();
            return true;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            carryable = GetComponent<GiftCarryable>();
        }

        public override void OnNetworkSpawn()
        {
            body.isKinematic = !IsServer;
            body.useGravity = IsServer;
            if (IsServer)
            {
                gripSlots.Clear();
                gripOwners.Clear();
                gripCount.Value = 0;
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnect;
            }
        }

        public void RequestGrip(bool grab)
        {
            if (!IsSpawned || NetworkManager == null) return;
            RequestGripRpc(grab);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestGripRpc(bool grab, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (!NetworkManager.ConnectedClients.TryGetValue(sender, out NetworkClient client) || client.PlayerObject == null)
                return;

            if (grab)
            {
                if (gripSlots.Contains(sender)) return; // retries are idempotent
                bool heavy = body.mass >= heavyMass;
                if (gripSlots.Count >= (heavy ? 2 : 1) || !IsWithinReach(client.PlayerObject.transform)) return;
                if (gripSlots.Count == 0) carryable.SetNetworkHeld(true);
                gripSlots.Add(sender);
                gripOwners.Add(sender);
            }
            else
            {
                gripSlots.Remove(sender);
                if (gripOwners.Contains(sender)) gripOwners.Remove(sender);
                if (gripSlots.Count == 0) carryable.SetNetworkHeld(false);
            }
            gripCount.Value = gripSlots.Count;
        }

        private bool IsWithinReach(Transform player)
        {
            Vector3 eye = player.position + Vector3.up * 1.55f;
            return Vector3.Distance(eye, body.position) <= reach + 0.75f;
        }

        private void FixedUpdate()
        {
            if (!IsServer || gripSlots.Count == 0) return;
            Vector3 target = Vector3.zero;
            int valid = 0;
            foreach (ulong id in gripSlots)
            {
                if (!NetworkManager.ConnectedClients.TryGetValue(id, out NetworkClient client) || client.PlayerObject == null) continue;
                Transform player = client.PlayerObject.transform;
                target += player.position + Vector3.up * 1.15f + player.forward * 1.2f;
                valid++;
            }
            if (valid == 0) { ClearGrips(); return; }
            target /= valid;
            float speed = body.mass >= heavyMass
                ? (valid >= 2 ? twoPlayerHeavySpeed : soloHeavySpeed)
                : twoPlayerHeavySpeed;
            Vector3 desired = Vector3.ClampMagnitude((target - body.position) * followResponsiveness, followResponsiveness * speed);
            body.linearVelocity = Vector3.MoveTowards(body.linearVelocity, desired, followResponsiveness * Time.fixedDeltaTime * speed);
        }

        private void OnClientDisconnect(ulong clientId) { if (IsServer) ReleaseClient(clientId); }

        public override void OnNetworkDespawn()
        {
            if (IsServer) { NetworkManager.OnClientDisconnectCallback -= OnClientDisconnect; ClearGrips(); }
            body.isKinematic = false;
            body.useGravity = true;
        }

        private void ReleaseClient(ulong clientId)
        {
            if (!gripSlots.Remove(clientId)) return;
            if (gripOwners.Contains(clientId)) gripOwners.Remove(clientId);
            gripCount.Value = gripSlots.Count;
            if (gripSlots.Count == 0) carryable.SetNetworkHeld(false);
        }

        private void ClearGrips()
        {
            gripSlots.Clear();
            if (IsServer) gripOwners.Clear();
            if (carryable != null) carryable.SetNetworkHeld(false);
            if (IsServer) gripCount.Value = 0;
        }
    }
}
