using System;
using System.Collections.Generic;
using SantasLittleHelpers.AI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SantasLittleHelpers.Delivery
{
    public enum DeliveryRoundState { DeliverGift, ReturnToSleigh, Complete }

    [DisallowMultipleComponent]
    public sealed class DeliveryRoundController : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Transform sleigh;
        [SerializeField] private GiftCarryable requiredGift;
        [SerializeField] private ResidentBrain[] residents = new ResidentBrain[0];
        [SerializeField, Min(0.1f)] private float returnRadius = 2f;
        private readonly Dictionary<GiftCarryable, GiftSnapshot> snapshots = new Dictionary<GiftCarryable, GiftSnapshot>();
        private DeliveryRoundState state;
        private bool delivered;
        private string status = "Bring the marked gift to the delivery point.";
        private Vector3 playerStartPosition;
        private Quaternion playerStartRotation;
        private Guid roundId;
        private bool completionRaised;
        private bool activated;
        public DeliveryRoundState State => state;
        public bool IsDelivered => delivered;
        public GiftCarryable RequiredGift => requiredGift;
        public event Action<DeliveryRoundState> StateChanged;
        public event Action<Guid> Completed;
        public event Action<Guid> RoundStarted;
        public Guid RoundId => roundId;

        private void Awake()
        {
            if (player == null || sleigh == null || requiredGift == null)
            {
                Debug.LogError("Assign player, sleigh and required gift on DeliveryRoundController.", this);
                enabled = false;
                return;
            }
            foreach (GiftCarryable gift in FindObjectsByType<GiftCarryable>(FindObjectsSortMode.None))
                snapshots.Add(gift, new GiftSnapshot(gift));
            playerStartPosition = player.position;
            playerStartRotation = player.rotation;
            state = DeliveryRoundState.DeliverGift;
            roundId = Guid.NewGuid();
        }

        private void Update()
        {
            if (!activated) return;
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening) return;
            CheckForReturn();
            if (state == DeliveryRoundState.Complete && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                RestartRound();
        }

        public void CheckForReturn()
        {
            if (!activated || state != DeliveryRoundState.ReturnToSleigh || (player.position - sleigh.position).sqrMagnitude > returnRadius * returnRadius) return;
            state = DeliveryRoundState.Complete;
            status = "Delivery complete! Press R to play again.";
            StateChanged?.Invoke(state);
            RaiseCompletedOnce();
        }
        // One idempotent delivery entry point; a future host can authorize this call.
        public bool TryDeliver(GiftCarryable gift)
        {
            if (!activated || state != DeliveryRoundState.DeliverGift || delivered || gift != requiredGift) return false;
            delivered = true;
            NetworkGiftCarryable networkGift = gift.GetComponent<NetworkGiftCarryable>();
            if (networkGift != null) networkGift.ForceReleaseOnServer();
            state = DeliveryRoundState.ReturnToSleigh;
            status = "Correct gift delivered. Return to the sleigh.";
            StateChanged?.Invoke(state);
            return true;
        }

        public void RestartRound()
        {
            BeginRound(Guid.NewGuid());
        }

        public bool BeginRound(Guid id)
        {
            if (id == Guid.Empty) return false;
            roundId = id;
            completionRaised = false;
            activated = true;
            ResetAuthoredState();
            RoundStarted?.Invoke(roundId);
            return true;
        }

        public void DeactivateRound()
        {
            activated = false;
            ResetAuthoredState();
        }

        private void ResetAuthoredState()
        {
            foreach (KeyValuePair<GiftCarryable, GiftSnapshot> entry in snapshots)
                if (entry.Key != null) entry.Value.Restore(entry.Key);
            if (player != null && player.TryGetComponent(out Unity.Netcode.NetworkObject playerNetworkObject) && playerNetworkObject.IsSpawned)
            {
                if (playerNetworkObject.IsOwner)
                {
                    player.SetPositionAndRotation(playerStartPosition, playerStartRotation);
                    if (player.TryGetComponent(out SantasLittleHelpers.Networking.OwnerNetworkTransform ownerTransform))
                        ownerTransform.Teleport(playerStartPosition, playerStartRotation, player.lossyScale);
                }
            }
            else if (player != null) player.SetPositionAndRotation(playerStartPosition, playerStartRotation);
            foreach (ResidentBrain resident in residents)
                if (resident != null) resident.ResetForRound();
            delivered = false;
            state = DeliveryRoundState.DeliverGift;
            status = "Bring the marked gift to the delivery point.";
            StateChanged?.Invoke(state);
        }

        private void RaiseCompletedOnce()
        {
            if (!activated || completionRaised) return;
            completionRaised = true;
            Completed?.Invoke(roundId);
        }

        private void OnGUI()
        {
            if (!activated) return;
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening) return;
            GUI.Box(new Rect(16, 16, 390, 72), "Solo delivery · WASD/mouse · E grab/drop · R restart\n" + status);
        }

        private struct GiftSnapshot
        {
            private readonly Vector3 position;
            private readonly Quaternion rotation;
            private readonly Vector3 scale;
            private readonly bool active;
            public GiftSnapshot(GiftCarryable gift)
            {
                position = gift.transform.position; rotation = gift.transform.rotation;
                scale = gift.transform.localScale; active = gift.gameObject.activeSelf;
            }
            public void Restore(GiftCarryable gift)
            {
                gift.gameObject.SetActive(false);
                gift.transform.SetPositionAndRotation(position, rotation);
                gift.transform.localScale = scale;
                gift.gameObject.SetActive(active);
                Rigidbody body = gift.GetComponent<Rigidbody>();
                if (body != null) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; body.useGravity = true; body.isKinematic = false; }
            }
        }
    }
}
