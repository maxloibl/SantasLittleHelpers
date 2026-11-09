using SantasLittleHelpers.Capture;
using Unity.Netcode;
using UnityEngine;

namespace SantasLittleHelpers.Delivery
{
    [DisallowMultipleComponent]
    public sealed class DeliveryPlayerOwnership : NetworkBehaviour
    {
        [SerializeField] private GameObject localView;
        [SerializeField] private FirstPersonController controller;
        public override void OnNetworkSpawn()
        {
            bool local = IsOwner;
            if (local)
            {
                GameObject marker = GameObject.Find("SLED PARKING — Return Here");
                Vector3 home = marker != null ? marker.transform.position : new Vector3(0, 0, -6.25f);
                home.y = .05f;
                Vector3 offset = (OwnerClientId == NetworkManager.ServerClientId ? new Vector3(-.8f, 0, 1.75f) : new Vector3(.8f, 0, 1.75f));
                transform.SetPositionAndRotation(home + offset, Quaternion.identity);
                if (TryGetComponent(out SantasLittleHelpers.Networking.OwnerNetworkTransform networkTransform))
                    networkTransform.Teleport(home + offset, Quaternion.identity, transform.lossyScale);
            }
            if (localView != null) localView.SetActive(local);
            if (controller != null) controller.enabled = local;
            foreach (AudioListener listener in GetComponentsInChildren<AudioListener>(true)) listener.enabled = local;
            Debug.Log($"MISSION_PLAYER owner={OwnerClientId} localView={local} position={transform.position}");
        }
    }
}
