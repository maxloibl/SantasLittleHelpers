using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SantasLittleHelpers.Capture
{
    /// <summary>E key recovery helper used only by the isolated capture test player.</summary>
    [DisallowMultipleComponent]
    public sealed class CaptureRescueInput : NetworkBehaviour
    {
        [SerializeField, Min(0.1f)] private float interactionRadius = 2f;
        [SerializeField, Min(0.1f)] private float autoRescueSpeed = 5f;
        private float nextAutoRequestTime;
        private void Update()
        {
            if (!IsOwner || !IsSpawned) return;
            bool automatic = System.Array.Exists(System.Environment.GetCommandLineArgs(), arg => arg == "--capture-auto-rescue");
            if (!automatic && (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame)) return;
            CapturedPlayerState[] players = FindObjectsByType<CapturedPlayerState>();
            CapturedPlayerState nearest = null;
            float nearestDistance = automatic ? float.MaxValue : interactionRadius;
            foreach (CapturedPlayerState player in players)
            {
                if (player == null || player.IsOwner || !player.IsCaptured) continue;
                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance < nearestDistance) { nearestDistance = distance; nearest = player; }
            }
            if (nearest == null) return;
            if (automatic)
            {
                Vector3 direction = nearest.transform.position - transform.position;
                direction.y = 0f;
                if (nearestDistance > 1.5f)
                {
                    CharacterController character = GetComponent<CharacterController>();
                    Vector3 step = direction.normalized * autoRescueSpeed * Time.deltaTime;
                    if (character != null && character.enabled) character.Move(step);
                    else transform.position += step;
                    return;
                }
                if (Time.unscaledTime < nextAutoRequestTime) return;
                nextAutoRequestTime = Time.unscaledTime + 0.5f;
                Debug.Log($"CAPTURE_SMOKE_RESCUE_REQUEST rescuer={OwnerClientId} target={nearest.OwnerClientId}");
            }
            GetComponent<CapturedPlayerState>()?.RequestRelease(nearest);
        }
    }
}
