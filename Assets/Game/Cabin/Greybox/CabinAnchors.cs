using UnityEngine;

namespace SantasLittleHelpers.Cabin
{
    public sealed class CabinAnchors : MonoBehaviour
    {
        [SerializeField] private Transform[] playerSpawnAnchors = new Transform[4];
        [SerializeField] private Transform missionStartAnchor;
        [SerializeField] private Transform departureDirection;
        [SerializeField] private Transform promptAnchor;
        [SerializeField] private Collider promptTrigger;

        public Transform[] PlayerSpawnAnchors => playerSpawnAnchors;
        public Transform MissionStartAnchor => missionStartAnchor;
        public Transform DepartureDirection => departureDirection;
        public Transform PromptAnchor => promptAnchor;
        public Collider PromptTrigger => promptTrigger;
    }
}
