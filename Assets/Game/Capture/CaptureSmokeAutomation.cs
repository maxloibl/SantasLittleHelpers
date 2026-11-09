using System.Collections;
using SantasLittleHelpers.AI;
using Unity.Netcode;
using UnityEngine;

namespace SantasLittleHelpers.Capture
{
    /// <summary>Restarts a lost round when --capture-auto-reset is used in the isolated smoke player.</summary>
    [DisallowMultipleComponent]
    public sealed class CaptureSmokeAutomation : NetworkBehaviour
    {
        private CaptureRoundCoordinator coordinator;
        private bool resetStarted;

        public override void OnNetworkSpawn()
        {
            coordinator = GetComponent<CaptureRoundCoordinator>();
            if (coordinator != null) coordinator.RoundLostChanged += OnRoundLost;
        }

        public override void OnNetworkDespawn()
        {
            if (coordinator != null) coordinator.RoundLostChanged -= OnRoundLost;
        }

        private void OnRoundLost(bool lost)
        {
            if (!lost)
            {
                ResetIsolatedTestRound();
                return;
            }
            if (!IsServer || resetStarted || !System.Array.Exists(System.Environment.GetCommandLineArgs(), arg => arg == "--capture-auto-reset")) return;
            resetStarted = true;
            StartCoroutine(ResetAfterDelay());
        }

        private IEnumerator ResetAfterDelay()
        {
            yield return new WaitForSecondsRealtime(1f);
            coordinator.ResetRoundServer();
        }

        private void ResetIsolatedTestRound()
        {
            foreach (ResidentBrain resident in FindObjectsByType<ResidentBrain>())
                if (resident != null) resident.ResetForRound();
            foreach (CaptureTestSpawnPlacement placement in FindObjectsByType<CaptureTestSpawnPlacement>())
                if (placement != null) placement.ResetToStartPose();
            if (IsServer)
            {
                CapturedPlayerState[] players = FindObjectsByType<CapturedPlayerState>();
                int freePlayers = 0;
                foreach (CapturedPlayerState player in players)
                    if (player != null && !player.IsCaptured) freePlayers++;
                ResidentBrain resident = FindAnyObjectByType<ResidentBrain>();
                Debug.Log($"CAPTURE_SMOKE_SECOND_ROUND_START contactActive={FindAnyObjectByType<ResidentCaptureContact>()?.enabled ?? false} residentState={resident?.State} freePlayers={freePlayers} totalPlayers={players.Length}");
            }
        }
    }
}
