using UnityEngine;

namespace SantasLittleHelpers.Delivery
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class GiftDeliveryZone : MonoBehaviour
    {
        [SerializeField] private DeliveryRoundController round;
        private void Reset() => GetComponent<Collider>().isTrigger = true;
        private void OnTriggerEnter(Collider other)
        {
            GiftCarryable gift = other.GetComponentInParent<GiftCarryable>();
            if (gift != null) TryHandleGift(gift);
        }

        public bool TryHandleGift(GiftCarryable gift) => round != null && round.TryDeliver(gift);
    }
}
