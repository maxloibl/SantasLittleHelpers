using UnityEngine;
using SantasLittleHelpers.AI;

namespace SantasLittleHelpers
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class GiftCarryable : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float carrySpeed = 12f;
        [SerializeField, Min(0.1f)] private float carryResponsiveness = 18f;
        [SerializeField, Min(0f)] private float noiseSpeedThreshold = 2.5f;
        [SerializeField, Min(0.1f)] private float noiseFullIntensitySpeed = 5.5f;
        [SerializeField, Min(0f)] private float noiseCooldown = 0.5f;

        private Rigidbody body;
        private GiftInteractor holder;
        private float nextNoiseTime;
        private bool networkHeld;

        public bool IsHeld => holder != null || networkHeld;

        internal void SetNetworkHeld(bool value)
        {
            networkHeld = value;
            if (value)
            {
                body.isKinematic = false;
                body.useGravity = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            else if (holder == null)
            {
                body.useGravity = true;
                body.isKinematic = false;
            }
        }

        private void Awake() => body = GetComponent<Rigidbody>();

        public bool TryPickUp(GiftInteractor interactor)
        {
            if (interactor == null || holder != null || networkHeld || !isActiveAndEnabled)
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
            if (networkHeld) return;
            if (holder == null)
                return;

            Vector3 destination = holder.CarryPosition;
            Vector3 desiredVelocity = Vector3.ClampMagnitude((destination - body.position) * carryResponsiveness, carrySpeed);
            // Velocity control preserves dynamic collision response while heavier gifts accelerate more slowly.
            float maxDelta = carryResponsiveness * Time.fixedDeltaTime;
            body.linearVelocity = Vector3.MoveTowards(body.linearVelocity, desiredVelocity, maxDelta / Mathf.Max(0.1f, body.mass));
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time < nextNoiseTime || noiseSpeedThreshold <= 0f) return;
            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed < noiseSpeedThreshold) return;

            float intensity = Mathf.InverseLerp(noiseSpeedThreshold,
                Mathf.Max(noiseSpeedThreshold + 0.1f, noiseFullIntensitySpeed), impactSpeed);
            Vector3 position = collision.contactCount > 0 ? collision.GetContact(0).point : body.position;
            foreach (ResidentBrain resident in FindObjectsByType<ResidentBrain>())
                resident.ReportNoise(position, intensity);
            nextNoiseTime = Time.time + noiseCooldown;
        }

        private void OnDisable()
        {
            if (holder != null)
                holder.Drop(this);
        }
    }
}
