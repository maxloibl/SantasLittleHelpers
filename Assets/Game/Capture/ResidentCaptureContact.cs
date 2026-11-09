using SantasLittleHelpers.AI;
using Unity.Netcode;
using UnityEngine;

namespace SantasLittleHelpers.Capture
{
    /// <summary>Isolated test-scene adapter that feeds host-side resident chase/proximity into capture.</summary>
    [DisallowMultipleComponent]
    public sealed class ResidentCaptureContact : MonoBehaviour
    {
        [SerializeField] private ResidentBrain resident;
        private ResidentState lastLoggedState;
        private void Update()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || resident == null) return;
            bool waitForClient = System.Array.Exists(System.Environment.GetCommandLineArgs(), arg => arg == "--capture-wait-for-client");
            if (waitForClient && NetworkManager.Singleton.ConnectedClientsList.Count < 2) return;
            CapturedPlayerState[] players = FindObjectsByType<CapturedPlayerState>();
            Transform target = null;
            float nearest = float.MaxValue;
            foreach (CapturedPlayerState player in players)
            {
                if (player == null || !player.IsSpawned || player.IsCaptured) continue;
                float distance = (player.transform.position - resident.transform.position).sqrMagnitude;
                if (distance < nearest) { nearest = distance; target = player.transform; }
            }
            resident.Target = target;
            if (resident.State != lastLoggedState)
            {
                lastLoggedState = resident.State;
                Debug.Log($"CAPTURE_RESIDENT_STATE state={lastLoggedState} target={(target != null ? target.name : "none")}");
            }
            foreach (CapturedPlayerState player in players)
                if (player != null && player.IsSpawned) CaptureRoundCoordinator.Instance?.TryCaptureServer(resident, player);
        }
    }
}
