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
    public sealed class ResidentBrain : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform target;
        [SerializeField] private Transform[] patrolPoints = new Transform[0];
        [SerializeField] private LayerMask lineOfSightBlockers = ~0;
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

        private int patrolIndex;
        private Vector3 investigationPoint;
        private bool hasInvestigationPoint;
        private float continuousSight;
        private float alarm;

        public ResidentState State { get; private set; } = ResidentState.Patrolling;
        public float Alarm => alarm;
        public Transform Target { get => target; set => target = value; }

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
            if (State != ResidentState.Chasing)
                State = ResidentState.Investigating;
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
                if (continuousSight >= chaseSightHold) State = ResidentState.Chasing;
            }
            else
            {
                continuousSight = 0f;
                alarm = Mathf.Max(0f, alarm - alarmDecayPerSecond * deltaTime);
                if (State == ResidentState.Chasing) State = hasInvestigationPoint ? ResidentState.Investigating : ResidentState.Patrolling;
                if (State == ResidentState.Investigating && alarm <= 0f)
                {
                    hasInvestigationPoint = false;
                    State = ResidentState.Patrolling;
                }
            }

            Vector3 destination;
            float speed;
            if (visible && State == ResidentState.Chasing)
            {
                destination = target.position;
                speed = chaseSpeed;
            }
            else if (State == ResidentState.Investigating && hasInvestigationPoint)
            {
                destination = investigationPoint;
                speed = investigateSpeed;
                if (Vector3.Distance(transform.position, destination) <= waypointTolerance)
                {
                    hasInvestigationPoint = false;
                    if (!visible) State = ResidentState.Patrolling;
                }
            }
            else
            {
                destination = CurrentPatrolPoint();
                speed = patrolSpeed;
                if (patrolPoints != null && patrolPoints.Length > 0 && Vector3.Distance(transform.position, destination) <= waypointTolerance)
                    patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            }

            MoveTowards(destination, speed, deltaTime);
        }

        private void Update() => SimulateStep(Time.deltaTime);

        private bool CanSeeTarget()
        {
            if (target == null) return false;
            Vector3 offset = target.position - transform.position;
            float distance = offset.magnitude;
            if (distance > sightRange || distance <= 0.001f) return false;
            if (Vector3.Angle(transform.forward, offset) > fieldOfView * 0.5f) return false;
            return !Physics.Raycast(transform.position + Vector3.up, offset.normalized, distance, lineOfSightBlockers, QueryTriggerInteraction.Ignore);
        }

        private Vector3 CurrentPatrolPoint()
        {
            if (patrolPoints == null || patrolPoints.Length == 0 || patrolPoints[patrolIndex] == null)
                return transform.position;
            return patrolPoints[patrolIndex].position;
        }

        private void MoveTowards(Vector3 destination, float speed, float deltaTime)
        {
            Vector3 flat = destination - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return;
            transform.position += flat.normalized * speed * deltaTime;
            transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = State == ResidentState.Chasing ? Color.red : State == ResidentState.Investigating ? Color.yellow : Color.green;
            Gizmos.DrawWireSphere(transform.position, sightRange);
            Gizmos.DrawRay(transform.position, Quaternion.AngleAxis(-fieldOfView * 0.5f, Vector3.up) * transform.forward * sightRange);
            Gizmos.DrawRay(transform.position, Quaternion.AngleAxis(fieldOfView * 0.5f, Vector3.up) * transform.forward * sightRange);
            if (hasInvestigationPoint) { Gizmos.color = Color.yellow; Gizmos.DrawSphere(investigationPoint, 0.18f); }
        }
    }
}
