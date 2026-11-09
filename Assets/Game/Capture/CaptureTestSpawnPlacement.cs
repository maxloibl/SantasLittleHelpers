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
            {
                transform.position = new Vector3(-1f, 0f, 0f);
                Debug.Log($"CAPTURE_TEST_SPAWN client={OwnerClientId} position={transform.position}");
            }
        }
    }
}
