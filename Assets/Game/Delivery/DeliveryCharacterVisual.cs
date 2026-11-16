using UnityEngine;

namespace SantasLittleHelpers.Delivery
{
    /// <summary>Animates the presentation from the same movement seen by local and remote viewers.</summary>
    [DisallowMultipleComponent]
    public sealed class DeliveryCharacterVisual : MonoBehaviour
    {
        private static readonly int MoveSpeed = Animator.StringToHash("MoveSpeed");
        private static readonly int Action = Animator.StringToHash("Action");

        [SerializeField] private Animator animator;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private bool animateJump;

        private Vector3 previousPosition;
        private bool wasRising;

        private void OnEnable()
        {
            previousPosition = transform.position;
            wasRising = false;
        }

        public void SetFirstPerson(bool firstPerson)
        {
            if (visualRoot == null) return;
            foreach (Renderer renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
                renderer.forceRenderingOff = firstPerson;
        }

        private void LateUpdate()
        {
            Vector3 position = transform.position;
            Vector3 displacement = position - previousPosition;
            previousPosition = position;
            if (animator == null || Time.deltaTime <= 0f) return;

            // Warps between scenes and spawn slots have no locomotion animation.
            Vector3 velocity = displacement.sqrMagnitude < 1f ? displacement / Time.deltaTime : Vector3.zero;
            float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            animator.SetFloat(MoveSpeed, horizontalSpeed, .12f, Time.deltaTime);

            bool rising = velocity.y > .75f;
            if (animateJump && rising && !wasRising) animator.SetTrigger(Action);
            wasRising = rising;
        }
    }
}
