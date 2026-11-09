using Unity.Netcode;
using UnityEngine;

namespace SantasLittleHelpers.Gifts.Tests
{
    public sealed class GiftCoopTestOwnership : NetworkBehaviour
    {
        [SerializeField] private GameObject localView;
        [SerializeField] private FirstPersonController controller;
        [SerializeField] private GiftInteractor interactor;

        public override void OnNetworkSpawn()
        {
            if (IsOwner) transform.position = new Vector3(OwnerClientId == 0 ? -0.8f : 0.8f, 0f, -1.1f);
            if (localView != null) localView.SetActive(IsOwner);
            if (controller != null) controller.enabled = IsOwner;
            if (interactor != null) interactor.enabled = IsOwner;
        }
    }
}
