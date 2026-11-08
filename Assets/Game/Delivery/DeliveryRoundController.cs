using System;
using System.Collections.Generic;
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
        [SerializeField, Min(0.1f)] private float returnRadius = 2f;
        private readonly Dictionary<GiftCarryable, GiftSnapshot> snapshots = new Dictionary<GiftCarryable, GiftSnapshot>();
        private DeliveryRoundState state;
        private bool delivered;
        private string status = "Bring the marked gift to the delivery point.";
        private Vector3 playerStartPosition;
        private Quaternion playerStartRotation;
        public DeliveryRoundState State => state;
        public bool IsDelivered => delivered;
        public event Action<DeliveryRoundState> StateChanged;

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
        }

        private void Update()
        {
            CheckForReturn();
            if (state == DeliveryRoundState.Complete && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                RestartRound();
        }

        public void CheckForReturn()
        {
            if (state != DeliveryRoundState.ReturnToSleigh || (player.position - sleigh.position).sqrMagnitude > returnRadius * returnRadius) return;
            state = DeliveryRoundState.Complete;
            status = "Delivery complete! Press R to play again.";
            StateChanged?.Invoke(state);
        }
        // One idempotent delivery entry point; a future host can authorize this call.
        public bool TryDeliver(GiftCarryable gift)
        {
            if (state != DeliveryRoundState.DeliverGift || delivered || gift != requiredGift) return false;
            delivered = true;
            state = DeliveryRoundState.ReturnToSleigh;
            status = "Correct gift delivered. Return to the sleigh.";
            StateChanged?.Invoke(state);
            return true;
        }

        public void RestartRound()
        {
            foreach (KeyValuePair<GiftCarryable, GiftSnapshot> entry in snapshots)
                if (entry.Key != null) entry.Value.Restore(entry.Key);
            player.SetPositionAndRotation(playerStartPosition, playerStartRotation);
            delivered = false;
            state = DeliveryRoundState.DeliverGift;
            status = "Bring the marked gift to the delivery point.";
            StateChanged?.Invoke(state);
        }

        private void OnGUI() => GUI.Box(new Rect(16, 16, 390, 72), "Solo delivery\n" + status);

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
