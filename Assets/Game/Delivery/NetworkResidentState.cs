using SantasLittleHelpers.AI;
using Unity.Netcode;
using UnityEngine;

namespace SantasLittleHelpers.Delivery
{
    /// <summary>Replicates the host's resident brain state on the in-scene resident NetworkObject.</summary>
    [DisallowMultipleComponent]
    public sealed class NetworkResidentState : NetworkBehaviour
    {
        private readonly NetworkVariable<int> state = new NetworkVariable<int>(0);
        private ResidentBrain brain;
        private float nextQuietPositionSample;
        private Vector3 lastQuietPosition;
        private bool hasQuietPosition;
        private bool quietReplicaMoveReported;
        private void Awake() => brain = GetComponent<ResidentBrain>();
        public override void OnNetworkSpawn()
        {
            state.OnValueChanged += OnStateChanged;
            if (IsServer && brain != null) state.Value = (int)brain.State;
            else if (brain != null) brain.enabled = false;
            if (System.Array.Exists(System.Environment.GetCommandLineArgs(), arg => arg == "--coop-smoke"))
                Debug.Log($"COOP_RESIDENT_NETWORK_SPAWN peer={NetworkManager.LocalClientId} server={IsServer} sceneObject={NetworkObject.IsSceneObject}");
        }
        public override void OnNetworkDespawn() => state.OnValueChanged -= OnStateChanged;
        private void Update()
        {
            if (IsServer && brain != null && state.Value != (int)brain.State) state.Value = (int)brain.State;
            if (!IsServer && System.Array.Exists(System.Environment.GetCommandLineArgs(), arg => arg == "--coop-quiet-smoke") && Time.unscaledTime >= nextQuietPositionSample)
            {
                nextQuietPositionSample = Time.unscaledTime + 1f;
                Vector3 position = transform.position;
                Debug.Log($"COOP_QUIET_RESIDENT_REPLICA_POSITION peer={NetworkManager.LocalClientId} position={position}");
                if (hasQuietPosition && !quietReplicaMoveReported && Vector3.Distance(lastQuietPosition, position) > .05f)
                {
                    quietReplicaMoveReported = true;
                    Debug.Log($"COOP_QUIET_RESIDENT_REPLICA_MOVE_PASS peer={NetworkManager.LocalClientId} from={lastQuietPosition} to={position}");
                }
                lastQuietPosition = position;
                hasQuietPosition = true;
            }
        }
        private void OnStateChanged(int previous, int current)
        {
            if (System.Array.Exists(System.Environment.GetCommandLineArgs(), arg => arg == "--coop-smoke"))
                Debug.Log($"COOP_RESIDENT_STATE old={(ResidentState)previous} new={(ResidentState)current} peer={NetworkManager.LocalClientId}");
        }
    }
}
