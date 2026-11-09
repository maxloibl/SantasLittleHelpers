using System;
using System.Collections.Generic;
using SantasLittleHelpers;
using SantasLittleHelpers.AI;
using SantasLittleHelpers.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SantasLittleHelpers.Capture
{
    /// <summary>Host-authoritative capture, release, loss state, disconnect cleanup and round reset.</summary>
    [DisallowMultipleComponent]
    public sealed class CaptureRoundCoordinator : NetworkBehaviour
    {
        [SerializeField, Min(0.1f)] private float captureRadius = 1.6f;
        [SerializeField, Min(0.1f)] private float releaseRadius = 2f;
        [SerializeField, Min(0f)] private float postReleaseProtectionSeconds = 2f;
        private readonly NetworkVariable<bool> roundLost = new NetworkVariable<bool>(false);
        private readonly Dictionary<ulong, float> releaseProtectionEndsAt = new Dictionary<ulong, float>();
        public static CaptureRoundCoordinator Instance { get; private set; }
        public bool RoundLost => roundLost.Value;
        public event Action<bool> RoundLostChanged;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            roundLost.OnValueChanged += OnRoundLostChanged;
            if (IsServer) NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        public override void OnNetworkDespawn()
        {
            if (roundLost != null) roundLost.OnValueChanged -= OnRoundLostChanged;
            if (NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            if (Instance == this) Instance = null;
        }

        private void OnRoundLostChanged(bool oldValue, bool newValue)
        {
            Debug.Log($"CAPTURE_ROUND_LOST value={newValue} peer={NetworkManager.LocalClientId}");
            RoundLostChanged?.Invoke(newValue);
        }

        /// <summary>Integration hook for #12: call on the host when a chasing resident reaches a player.</summary>
        public bool TryCaptureServer(ResidentBrain resident, CapturedPlayerState player)
        {
            if (!IsServer || resident == null || player == null || !player.IsSpawned || resident.State != ResidentState.Chasing) return false;
            if (Vector3.Distance(resident.transform.position, player.transform.position) > captureRadius) return false;
            if (player.IsCaptured || roundLost.Value) return false;
            if (releaseProtectionEndsAt.TryGetValue(player.OwnerClientId, out float protectedUntil))
            {
                if (Time.unscaledTime < protectedUntil) return false;
                releaseProtectionEndsAt.Remove(player.OwnerClientId);
            }
            player.SetCapturedServer(true);
            Debug.Log($"CAPTURE_HOST_CAPTURE player={player.OwnerClientId}");
            RefreshLossState();
            return true;
        }

        internal bool TryReleaseServer(ulong senderClientId, CapturedPlayerState target)
        {
            if (!IsServer || target == null || !target.IsSpawned || !target.IsCaptured || target.OwnerClientId == senderClientId) return false;
            if (!NetworkManager.ConnectedClients.TryGetValue(senderClientId, out NetworkClient sender) || sender.PlayerObject == null) return false;
            CapturedPlayerState rescuer = sender.PlayerObject.GetComponent<CapturedPlayerState>();
            if (rescuer == null || rescuer.IsCaptured || Vector3.Distance(sender.PlayerObject.transform.position, target.transform.position) > releaseRadius) return false;
            target.SetCapturedServer(false);
            float protectedUntil = Time.unscaledTime + postReleaseProtectionSeconds;
            releaseProtectionEndsAt[target.OwnerClientId] = protectedUntil;
            Debug.Log($"CAPTURE_HOST_RELEASE rescuer={senderClientId} target={target.OwnerClientId}");
            Debug.Log($"CAPTURE_RELEASE_PROTECTION player={target.OwnerClientId} seconds={postReleaseProtectionSeconds:0.0}");
            RefreshLossState();
            return true;
        }

        /// <summary>Integration hook for #12: clear capture state when restarting the mission.</summary>
        public void ResetRoundServer()
        {
            if (!IsServer) return;
            foreach (CapturedPlayerState player in FindObjectsByType<CapturedPlayerState>())
                if (player.IsSpawned) player.SetCapturedServer(false);
            releaseProtectionEndsAt.Clear();
            roundLost.Value = false;
            Debug.Log("CAPTURE_HOST_RESET complete=true");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            releaseProtectionEndsAt.Remove(clientId);
            RefreshLossState();
        }

        private void RefreshLossState()
        {
            if (!IsServer) return;
            int active = 0;
            bool anyFree = false;
            foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                CapturedPlayerState player = client.PlayerObject.GetComponent<CapturedPlayerState>();
                if (player == null) continue;
                active++;
                if (!player.IsCaptured) anyFree = true;
            }
            roundLost.Value = active > 0 && !anyFree;
        }

        private void Update()
        {
            if (!IsSpawned || !RoundLost || Keyboard.current == null || !Keyboard.current.rKey.wasPressedThisFrame) return;
            if (IsServer) ResetRoundServer();
            else RequestResetRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestResetRpc(RpcParams rpcParams = default)
        {
            // Only the host can restart the shared round.
            if (rpcParams.Receive.SenderClientId == NetworkManager.ServerClientId) ResetRoundServer();
        }

        private void OnGUI()
        {
            if (RoundLost) GUI.Box(new Rect(16, 100, 330, 48), "Everyone is captured. Host: press R to restart.");
        }
    }
}
