using System;
using SantasLittleHelpers;
using Unity.Netcode;
using UnityEngine;

namespace SantasLittleHelpers.Capture
{
    /// <summary>Replicated capture state and owner-side movement lock for a network player.</summary>
    [DisallowMultipleComponent]
    public sealed class CapturedPlayerState : NetworkBehaviour
    {
        private readonly NetworkVariable<bool> captured = new NetworkVariable<bool>(false);
        private FirstPersonController controller;
        private GiftInteractor giftInteractor;
        public bool IsCaptured => captured.Value;
        public event Action<bool> CaptureChanged;

        public override void OnNetworkSpawn()
        {
            controller = GetComponent<FirstPersonController>();
            giftInteractor = GetComponent<GiftInteractor>();
            captured.OnValueChanged += HandleChanged;
            ApplyLock(captured.Value);
        }

        public override void OnNetworkDespawn()
        {
            captured.OnValueChanged -= HandleChanged;
            ApplyLock(false);
        }

        private void HandleChanged(bool previous, bool current)
        {
            ApplyLock(current);
            CaptureChanged?.Invoke(current);
            Debug.Log($"CAPTURE_STATE player={OwnerClientId} captured={current} movementEnabled={(controller != null && controller.enabled)} peer={NetworkManager.LocalClientId}");
        }

        private void ApplyLock(bool locked)
        {
            if (!IsOwner) return;
            if (controller != null) controller.enabled = !locked;
            if (giftInteractor != null) giftInteractor.enabled = !locked;
        }

        internal void SetCapturedServer(bool value)
        {
            if (IsServer) captured.Value = value;
        }

        /// <summary>Called by a free local player when interacting with a nearby captured player.</summary>
        public void RequestRelease(CapturedPlayerState target)
        {
            if (IsOwner && IsSpawned && target != null && target.IsSpawned)
                RequestReleaseRpc(new NetworkObjectReference(target.NetworkObject));
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestReleaseRpc(NetworkObjectReference targetReference, RpcParams rpcParams = default)
        {
            if (!targetReference.TryGet(out NetworkObject targetObject) || targetObject == NetworkObject) return;
            CaptureRoundCoordinator coordinator = CaptureRoundCoordinator.Instance;
            if (coordinator != null)
                coordinator.TryReleaseServer(rpcParams.Receive.SenderClientId, targetObject.GetComponent<CapturedPlayerState>());
        }
    }
}
