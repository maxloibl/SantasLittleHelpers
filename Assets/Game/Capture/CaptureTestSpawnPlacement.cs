using Unity.Netcode;
using UnityEngine;

namespace SantasLittleHelpers.Capture
{
    /// <summary>Positions the second test player beside the resident for deterministic local smoke runs.</summary>
    [DisallowMultipleComponent]
    public sealed class CaptureTestSpawnPlacement : NetworkBehaviour
    {
        public override void OnNetworkSpawn()
        {
            if (IsOwner && OwnerClientId != NetworkManager.ServerClientId)
                ResetToStartPose();
        }

        public void ResetToStartPose()
        {
            if (!IsOwner) return;
            Vector3 start = OwnerClientId == NetworkManager.ServerClientId ? new Vector3(-4f, 0f, 0f) : new Vector3(-1f, 0f, 0f);
            transform.SetPositionAndRotation(start, Quaternion.identity);
            Debug.Log($"CAPTURE_TEST_SPAWN owner={OwnerClientId} position={transform.position}");
        }
    }
}
