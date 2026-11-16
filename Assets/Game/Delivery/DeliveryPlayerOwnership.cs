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
            if (localView != null) localView.SetActive(local);
            if (controller != null) controller.enabled = local;
            foreach (AudioListener listener in GetComponentsInChildren<AudioListener>(true)) listener.enabled = local;
            if (TryGetComponent(out DeliveryCharacterVisual visual)) visual.SetFirstPerson(local);
            Debug.Log($"MISSION_PLAYER owner={OwnerClientId} localView={local} position={transform.position}");
        }

        public void ActivateLocalSoloView()
        {
            if (localView != null) localView.SetActive(true);
            if (controller != null) controller.enabled = true;
            foreach (AudioListener listener in GetComponentsInChildren<AudioListener>(true)) listener.enabled = true;
            if (TryGetComponent(out DeliveryCharacterVisual visual)) visual.SetFirstPerson(true);
        }
    }
}
