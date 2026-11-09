using System.Collections;
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
            if (!IsServer) return;
            coordinator = GetComponent<CaptureRoundCoordinator>();
            if (coordinator != null) coordinator.RoundLostChanged += OnRoundLost;
        }

        public override void OnNetworkDespawn()
        {
            if (coordinator != null) coordinator.RoundLostChanged -= OnRoundLost;
        }

        private void OnRoundLost(bool lost)
        {
            if (!lost || resetStarted || !System.Array.Exists(System.Environment.GetCommandLineArgs(), arg => arg == "--capture-auto-reset")) return;
            resetStarted = true;
            StartCoroutine(ResetAfterDelay());
        }

        private IEnumerator ResetAfterDelay()
        {
            yield return new WaitForSecondsRealtime(1f);
            ResidentCaptureContact contact = FindAnyObjectByType<ResidentCaptureContact>();
            if (contact != null) contact.enabled = false;
            coordinator.ResetRoundServer();
        }
    }
}
