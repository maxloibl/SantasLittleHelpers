using System.Collections.Generic;
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
        private Transform rescueSmokeTarget;
        private readonly HashSet<ulong> observedCapturedPlayers = new HashSet<ulong>();
        private readonly Dictionary<ulong, float> releaseVerificationTimes = new Dictionary<ulong, float>();
        private void Update()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || resident == null) return;
            bool waitForClient = System.Array.Exists(System.Environment.GetCommandLineArgs(), arg => arg == "--capture-wait-for-client");
            if (waitForClient && NetworkManager.Singleton.ConnectedClientsList.Count < 2) return;
            CapturedPlayerState[] players = FindObjectsByType<CapturedPlayerState>();
            TrackReleaseWindow(players);
            Transform target = null;
            float nearest = float.MaxValue;
            foreach (CapturedPlayerState player in players)
            {
                if (player == null || !player.IsSpawned || player.IsCaptured) continue;
                float distance = (player.transform.position - resident.transform.position).sqrMagnitude;
                if (distance < nearest) { nearest = distance; target = player.transform; }
            }
            bool pinRescueSmokeTarget = System.Array.Exists(System.Environment.GetCommandLineArgs(), arg => arg == "--capture-rescue-smoke");
            if (pinRescueSmokeTarget && resident.State == ResidentState.Chasing && rescueSmokeTarget != null)
                target = rescueSmokeTarget;
            else if (target != null)
                rescueSmokeTarget = target;
            resident.Target = target;
            if (resident.State != lastLoggedState)
            {
                lastLoggedState = resident.State;
                Debug.Log($"CAPTURE_RESIDENT_STATE state={lastLoggedState} target={(target != null ? target.name : "none")}");
            }
            foreach (CapturedPlayerState player in players)
                if (player != null && player.IsSpawned) CaptureRoundCoordinator.Instance?.TryCaptureServer(resident, player);
        }

        private void TrackReleaseWindow(CapturedPlayerState[] players)
        {
            if (!System.Array.Exists(System.Environment.GetCommandLineArgs(), arg => arg == "--capture-rescue-smoke")) return;
            foreach (CapturedPlayerState player in players)
            {
                if (player == null || !player.IsSpawned) continue;
                ulong id = player.OwnerClientId;
                if (player.IsCaptured) observedCapturedPlayers.Add(id);
                else if (observedCapturedPlayers.Remove(id)) releaseVerificationTimes[id] = Time.unscaledTime + 1.5f;

                if (!releaseVerificationTimes.TryGetValue(id, out float verifyAt) || Time.unscaledTime < verifyAt) continue;
                float distance = resident != null ? Vector3.Distance(resident.transform.position, player.transform.position) : float.MaxValue;
                Debug.Log($"CAPTURE_SMOKE_RELEASE_WINDOW player={id} remainedFree={!player.IsCaptured} seconds=1.5 adapterActive={enabled} residentState={resident?.State} residentDistance={distance:0.00}");
                releaseVerificationTimes.Remove(id);
            }
        }
    }
}
