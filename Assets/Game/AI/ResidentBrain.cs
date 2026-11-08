using UnityEngine;

namespace SantasLittleHelpers.AI
{
    public enum ResidentState
    {
        Patrolling,
        Investigating,
        Chasing
    }

    /// <summary>Standalone resident prototype. Assign a target and patrol points in the Inspector.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class ResidentBrain : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform target;
        [SerializeField] private Transform[] patrolPoints = new Transform[0];
        [SerializeField] private LayerMask lineOfSightBlockers = ~0;
        [SerializeField] private LayerMask movementBlockers = ~0;
        [Header("Perception")]
        [SerializeField, Min(0f)] private float sightRange = 8f;
        [SerializeField, Range(0f, 180f)] private float fieldOfView = 100f;
        [SerializeField, Min(0f)] private float chaseSightHold = 0.65f;
        [SerializeField, Min(0f)] private float hearingRange = 12f;
        [Header("Movement and alarm")]
        [SerializeField, Min(0f)] private float patrolSpeed = 1.5f;
        [SerializeField, Min(0f)] private float investigateSpeed = 2f;
        [SerializeField, Min(0f)] private float chaseSpeed = 3.2f;
        [SerializeField, Min(0.01f)] private float alarmDecayPerSecond = 0.18f;
        [SerializeField, Min(0f)] private float waypointTolerance = 0.25f;
        [Header("Live state (read only at runtime)")]
        [SerializeField] private ResidentState state = ResidentState.Patrolling;
        [SerializeField, Range(0f, 1f)] private float alarm;

        private int patrolIndex;
        private Vector3 investigationPoint;
        private bool hasInvestigationPoint;
        private float continuousSight;
        private Vector3 startPosition;
        private Quaternion startRotation;
        public ResidentState State => state;
        public float Alarm => alarm;
        public Transform Target { get => target; set => target = value; }

        private void Awake()
        {
            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        /// <summary>Notify this resident of a sound. Intensity is normalized to 0..1.</summary>
        public void ReportNoise(Vector3 position, float intensity)
        {
            intensity = Mathf.Clamp01(intensity);
            float distance = Vector3.Distance(transform.position, position);
            if (intensity <= 0f || distance > hearingRange * intensity)
                return;

            investigationPoint = position;
            hasInvestigationPoint = true;
            alarm = Mathf.Max(alarm, intensity);
            if (state != ResidentState.Chasing)
                state = ResidentState.Investigating;
        }

        public void ResetForRound()
        {
            state = ResidentState.Patrolling;
            alarm = 0f;
            continuousSight = 0f;
            hasInvestigationPoint = false;
            patrolIndex = 0;
            transform.SetPositionAndRotation(startPosition, startRotation);
        }

        /// <summary>Public deterministic step also used by the batch smoke check.</summary>
        public void SimulateStep(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            bool visible = CanSeeTarget();
            if (visible)
            {
                continuousSight += deltaTime;
                alarm = Mathf.Max(alarm, Mathf.Clamp01(continuousSight / Mathf.Max(0.01f, chaseSightHold)));
                if (continuousSight >= chaseSightHold) state = ResidentState.Chasing;
            }
            else
            {
                continuousSight = 0f;
                alarm = Mathf.Max(0f, alarm - alarmDecayPerSecond * deltaTime);
                if (state == ResidentState.Chasing) state = hasInvestigationPoint ? ResidentState.Investigating : ResidentState.Patrolling;
                if (state == ResidentState.Investigating && alarm <= 0f)
                {
                    hasInvestigationPoint = false;
                    state = ResidentState.Patrolling;
                }
            }

            Vector3 destination;
            float speed;
            if (visible && state == ResidentState.Chasing)
            {
                destination = target.position;
                speed = chaseSpeed;
            }
            else if (state == ResidentState.Investigating && hasInvestigationPoint)
            {
                destination = investigationPoint;
                speed = investigateSpeed;
                if (HorizontalDistance(transform.position, destination) <= waypointTolerance)
                {
                    hasInvestigationPoint = false;
                    if (!visible) state = ResidentState.Patrolling;
                }
            }
            else
            {
                destination = CurrentPatrolPoint();
                speed = patrolSpeed;
                if (patrolPoints != null && patrolPoints.Length > 0 && HorizontalDistance(transform.position, destination) <= waypointTolerance)
                    patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            }

            MoveTowards(destination, speed, deltaTime);
        }

        private void Update() => SimulateStep(Time.deltaTime);

        private bool CanSeeTarget()
        {
            if (target == null) return false;
            Vector3 origin = transform.position + Vector3.up;
            Vector3 offset = target.position - origin;
            float distance = offset.magnitude;
            if (distance > sightRange || distance <= 0.001f) return false;
            if (Vector3.Angle(transform.forward, offset) > fieldOfView * 0.5f) return false;
            if (!Physics.Raycast(origin, offset.normalized, out RaycastHit hit, distance, lineOfSightBlockers, QueryTriggerInteraction.Ignore))
                return true;
            return hit.transform == target || target.IsChildOf(hit.transform) || hit.transform.IsChildOf(target);
        }

        private Vector3 CurrentPatrolPoint()
        {
            if (patrolPoints == null || patrolPoints.Length == 0 || patrolPoints[patrolIndex] == null)
                return transform.position;
            return patrolPoints[patrolIndex].position;
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            first.y = 0f;
            second.y = 0f;
            return Vector3.Distance(first, second);
        }

        private void MoveTowards(Vector3 destination, float speed, float deltaTime)
        {
            Vector3 flat = destination - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return;
            Vector3 direction = flat.normalized;
            float distance = Mathf.Min(flat.magnitude, speed * deltaTime);
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule != null && Physics.CapsuleCast(GetCapsuleBottom(capsule), GetCapsuleTop(capsule), GetCapsuleRadius(capsule),
                    direction, out RaycastHit hit, distance, movementBlockers, QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(0f, hit.distance - 0.02f);
            transform.position += direction * distance;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        private static Vector3 GetCapsuleBottom(CapsuleCollider capsule)
        {
            Bounds bounds = capsule.bounds;
            float radius = GetCapsuleRadius(capsule);
            return bounds.center - Vector3.up * Mathf.Max(0f, bounds.extents.y - radius);
        }

        private static Vector3 GetCapsuleTop(CapsuleCollider capsule)
        {
            Bounds bounds = capsule.bounds;
            float radius = GetCapsuleRadius(capsule);
            return bounds.center + Vector3.up * Mathf.Max(0f, bounds.extents.y - radius);
        }

        private static float GetCapsuleRadius(CapsuleCollider capsule)
        {
            Vector3 scale = capsule.transform.lossyScale;
            return capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = state == ResidentState.Chasing ? Color.red : state == ResidentState.Investigating ? Color.yellow : Color.green;
            Gizmos.DrawWireSphere(transform.position, sightRange);
            Gizmos.DrawRay(transform.position, Quaternion.AngleAxis(-fieldOfView * 0.5f, Vector3.up) * transform.forward * sightRange);
            Gizmos.DrawRay(transform.position, Quaternion.AngleAxis(fieldOfView * 0.5f, Vector3.up) * transform.forward * sightRange);
            if (hasInvestigationPoint) { Gizmos.color = Color.yellow; Gizmos.DrawSphere(investigationPoint, 0.18f); }
        }
    }
}
