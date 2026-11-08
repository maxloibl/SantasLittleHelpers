using UnityEngine;

namespace SantasLittleHelpers
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class GiftCarryable : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float carrySpeed = 12f;
        [SerializeField, Min(0.1f)] private float carryResponsiveness = 18f;

        private Rigidbody body;
        private GiftInteractor holder;

        public bool IsHeld => holder != null;

        private void Awake() => body = GetComponent<Rigidbody>();

        public bool TryPickUp(GiftInteractor interactor)
        {
            if (interactor == null || holder != null || !isActiveAndEnabled)
                return false;

            holder = interactor;
            body.isKinematic = false;
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            return true;
        }

        public void Release(GiftInteractor interactor)
        {
            if (holder != interactor)
                return;

            holder = null;
            body.useGravity = true;
            body.isKinematic = false;
        }

        private void FixedUpdate()
        {
            if (holder == null)
                return;

            Vector3 destination = holder.CarryPosition;
            Vector3 desiredVelocity = Vector3.ClampMagnitude((destination - body.position) * carryResponsiveness, carrySpeed);
            // Velocity control preserves dynamic collision response while heavier gifts accelerate more slowly.
            float maxDelta = carryResponsiveness * Time.fixedDeltaTime;
            body.linearVelocity = Vector3.MoveTowards(body.linearVelocity, desiredVelocity, maxDelta / Mathf.Max(0.1f, body.mass));
        }

        private void OnDisable()
        {
            if (holder != null)
                holder.Drop(this);
        }
    }
}
