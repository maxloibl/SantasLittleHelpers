using SantasLittleHelpers.AI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SantasLittleHelpers.Capture
{
    /// <summary>Isolated test-scene adapter that feeds host-side resident chase/proximity into capture.</summary>
    [DisallowMultipleComponent]
    public sealed class ResidentCaptureContact : MonoBehaviour
    {
        [SerializeField] private ResidentBrain resident;
        private void Update()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || resident == null) return;
            CapturedPlayerState[] players = FindObjectsByType<CapturedPlayerState>(FindObjectsSortMode.None);
            Transform target = null;
            float nearest = float.MaxValue;
            foreach (CapturedPlayerState player in players)
            {
                if (player == null || !player.IsSpawned) continue;
                float distance = (player.transform.position - resident.transform.position).sqrMagnitude;
                if (distance < nearest) { nearest = distance; target = player.transform; }
            }
            resident.Target = target;
            foreach (CapturedPlayerState player in players)
                if (player != null && player.IsSpawned) CaptureRoundCoordinator.Instance?.TryCaptureServer(resident, player);
        }
    }

    /// <summary>E key helper used only by the isolated capture test player.</summary>
    [DisallowMultipleComponent]
    public sealed class CaptureRescueInput : NetworkBehaviour
    {
        [SerializeField, Min(0.1f)] private float interactionRadius = 2f;
        private void Update()
        {
            if (!IsOwner || !IsSpawned || Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;
            CapturedPlayerState[] players = FindObjectsByType<CapturedPlayerState>(FindObjectsSortMode.None);
            CapturedPlayerState nearest = null;
            float nearestDistance = interactionRadius;
            foreach (CapturedPlayerState player in players)
            {
                if (player == null || player.IsOwner || !player.IsCaptured) continue;
                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance < nearestDistance) { nearestDistance = distance; nearest = player; }
            }
            GetComponent<CapturedPlayerState>()?.RequestRelease(nearest);
        }
    }
}
