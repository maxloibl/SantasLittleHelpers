using System.Collections.Generic;
using SantasLittleHelpers.Capture;
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
        private readonly HashSet<ulong> capturedGrabRejections = new HashSet<ulong>();
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

        /// <summary>Removes only this client's grip and preserves any partner's heavy-gift slot.</summary>
        public bool ForceReleaseClientOnServer(ulong clientId)
        {
            return IsServer && IsSpawned && ReleaseClient(clientId);
        }

        public bool HasServerGrip(ulong clientId) => IsServer && gripSlots.Contains(clientId);
        public bool WasCapturedGrabRejected(ulong clientId) => IsServer && capturedGrabRejections.Contains(clientId);

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
                capturedGrabRejections.Clear();
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
            {
                TestLog($"GIFT_GRIP_REJECT reason=player-missing sender={sender}");
                return;
            }

            if (grab)
            {
                CapturedPlayerState playerState = client.PlayerObject.GetComponent<CapturedPlayerState>();
                if (playerState != null && playerState.IsCaptured)
                {
                    capturedGrabRejections.Add(sender);
                    TestLog($"GIFT_GRIP_REJECT reason=captured-grab sender={sender}");
                    return;
                }
                if (gripSlots.Contains(sender)) { TestLog($"GIFT_GRIP_DUPLICATE sender={sender} slots={gripSlots.Count}"); return; } // retries are idempotent
                bool heavy = body.mass >= heavyMass;
                float distance = Vector3.Distance(client.PlayerObject.transform.position + Vector3.up * 1.55f, body.position);
                if (gripSlots.Count >= (heavy ? 2 : 1)) { TestLog($"GIFT_GRIP_REJECT reason=full sender={sender} slots={gripSlots.Count} heavy={heavy}"); return; }
                if (!IsWithinReach(client.PlayerObject.transform)) { TestLog($"GIFT_GRIP_REJECT reason=out-of-reach sender={sender} distance={distance:0.00} player={client.PlayerObject.transform.position} gift={body.position}"); return; }
                if (gripSlots.Count == 0) carryable.SetNetworkHeld(true);
                gripSlots.Add(sender);
                gripOwners.Add(sender);
                TestLog($"GIFT_GRIP_ACCEPT sender={sender} heavy={heavy} distance={distance:0.00} slots={gripSlots.Count}");
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

        private bool ReleaseClient(ulong clientId)
        {
            if (!gripSlots.Remove(clientId)) return false;
            if (gripOwners.Contains(clientId)) gripOwners.Remove(clientId);
            gripCount.Value = gripSlots.Count;
            if (gripSlots.Count == 0) carryable.SetNetworkHeld(false);
            return true;
        }

        private void ClearGrips()
        {
            gripSlots.Clear();
            if (IsServer) gripOwners.Clear();
            if (carryable != null) carryable.SetNetworkHeld(false);
            if (IsServer) gripCount.Value = 0;
        }

        private static void TestLog(string message)
        {
            if (System.Array.Exists(System.Environment.GetCommandLineArgs(), arg => arg == "--coop-smoke") ||
                System.Array.Exists(System.Environment.GetCommandLineArgs(), arg => arg == "--coop-quiet-smoke")) Debug.Log(message);
        }
    }
}
