using UnityEngine;
using UnityEngine.InputSystem;

namespace SantasLittleHelpers
{
    [DisallowMultipleComponent]
    public sealed class GiftInteractor : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField, Min(0.1f)] private float reach = 2.5f;
        [SerializeField] private LayerMask giftLayers = ~0;
        [SerializeField] private Transform carryAnchor;

        private InputActionMap input;
        private InputAction interact;
        private GiftCarryable heldGift;
        private NetworkGiftCarryable heldNetworkGift;
        private NetworkGiftCarryable pendingNetworkGift;
        private float pendingNetworkGripStartedAt;

        private const float NetworkGripConfirmationTimeout = 2f;

        internal Vector3 CarryPosition => carryAnchor != null ? carryAnchor.position : viewCamera.transform.position + viewCamera.transform.forward;

        private void Awake()
        {
            if (viewCamera == null)
            {
                Debug.LogError("Assign a view camera to GiftInteractor.", this);
                enabled = false;
                return;
            }

            input = new InputActionMap("GiftInteraction");
            interact = input.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
        }

        private void OnEnable() => input?.Enable();

        private void OnDisable()
        {
            input?.Disable();
            if (heldNetworkGift != null) heldNetworkGift.RequestGrip(false);
            if (pendingNetworkGift != null && pendingNetworkGift != heldNetworkGift)
                pendingNetworkGift.RequestGrip(false);
            heldNetworkGift = null;
            pendingNetworkGift = null;
            if (heldGift != null)
                heldGift.Release(this);
            heldGift = null;
        }

        private void OnDestroy() => input?.Dispose();

        private void Update()
        {
            UpdateNetworkGripState();
            if (interact != null && interact.WasPressedThisFrame())
                Interact();
        }

        private void UpdateNetworkGripState()
        {
            if (heldNetworkGift != null && (!heldNetworkGift.IsSpawned || !heldNetworkGift.HasLocalGrip))
                heldNetworkGift = null;

            if (pendingNetworkGift == null) return;
            if (!pendingNetworkGift.IsSpawned)
            {
                pendingNetworkGift = null;
                return;
            }

            if (pendingNetworkGift.HasLocalGrip)
            {
                heldNetworkGift = pendingNetworkGift;
                pendingNetworkGift = null;
            }
            else if (Time.unscaledTime - pendingNetworkGripStartedAt >= NetworkGripConfirmationTimeout)
            {
                // A competing grab may have won. Clear the pending request so E can retry.
                pendingNetworkGift = null;
            }
        }

        private void Interact()
        {
            if (heldNetworkGift != null)
            {
                heldNetworkGift.RequestGrip(false);
                heldNetworkGift = null;
                return;
            }
            if (pendingNetworkGift != null) return;
            if (heldGift != null)
            {
                Drop(heldGift);
                return;
            }

            Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, reach, giftLayers, QueryTriggerInteraction.Ignore))
                return;

            GiftCarryable gift = hit.collider.GetComponentInParent<GiftCarryable>();
            if (gift == null) return;
            NetworkGiftCarryable networkGift = gift.GetComponent<NetworkGiftCarryable>();
            if (networkGift != null && networkGift.IsSpawned)
            {
                networkGift.RequestGrip(true);
                pendingNetworkGift = networkGift;
                pendingNetworkGripStartedAt = Time.unscaledTime;
            }
            else if (gift.TryPickUp(this)) heldGift = gift;
        }

        internal void Drop(GiftCarryable gift)
        {
            if (heldGift != gift)
                return;

            heldGift = null;
            gift.Release(this);
        }
    }
}
