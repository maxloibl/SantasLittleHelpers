using SantasLittleHelpers;
using Unity.Netcode;
using UnityEngine;

namespace SantasLittleHelpers.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkPlayerOwnership : NetworkBehaviour
    {
        [SerializeField] private GameObject localView;
        [SerializeField] private FirstPersonController controller;
        private bool autoplay;
        private float nextLogTime;

        public override void OnNetworkSpawn()
        {
            bool isLocal = IsOwner;
            if (isLocal) transform.position = new Vector3(OwnerClientId == 0 ? -4f : 4f, 0f, 0f);
            if (localView != null) localView.SetActive(isLocal);
            if (controller != null) controller.enabled = isLocal;
            foreach (AudioListener listener in GetComponentsInChildren<AudioListener>(true))
                listener.enabled = isLocal;
            autoplay = isLocal && System.Array.Exists(System.Environment.GetCommandLineArgs(), arg => arg == "--autoplay");
            Debug.Log($"NET_PLAYER_SPAWN owner={OwnerClientId} localOwner={isLocal} localView={localView != null && localView.activeSelf} position={transform.position}");
        }

        private void Update()
        {
            if (!IsSpawned) return;
            if (autoplay && controller != null)
                controller.GetComponent<CharacterController>().Move(Vector3.right * (Mathf.Sin(Time.time * 0.7f) * 1.5f) * Time.deltaTime);
            if (Time.unscaledTime < nextLogTime) return;
            nextLogTime = Time.unscaledTime + 1f;
            Debug.Log($"NET_PLAYER_STATE owner={OwnerClientId} localOwner={IsOwner} position={transform.position}");
        }

        public override void OnNetworkDespawn()
        {
            Debug.Log($"NET_PLAYER_DESPAWN owner={OwnerClientId} localOwner={IsOwner}");
            if (IsOwner) Cursor.lockState = CursorLockMode.None;
        }
    }
}
