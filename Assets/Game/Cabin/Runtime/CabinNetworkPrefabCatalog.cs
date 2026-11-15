using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SantasLittleHelpers.Cabin
{
    /// <summary>Registers mission network prefabs before the session-owned manager starts.</summary>
    public sealed class CabinNetworkPrefabCatalog : MonoBehaviour
    {
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private GameObject[] networkPrefabs;
        private readonly HashSet<NetworkManager> configuredManagers = new HashSet<NetworkManager>();
        public GameObject PlayerPrefab => playerPrefab;

        public bool ConfigureNetworkManager(NetworkManager manager)
        {
            if (manager == null || manager.NetworkConfig == null || playerPrefab == null ||
                networkPrefabs == null || networkPrefabs.Length == 0) return false;
            if (configuredManagers.Contains(manager)) return true;
            manager.NetworkConfig.PlayerPrefab = playerPrefab;
            foreach (GameObject prefab in networkPrefabs)
            {
                if (prefab == null || prefab.GetComponent<NetworkObject>() == null) return false;
                manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
            }
            configuredManagers.Add(manager);
            return true;
        }
    }
}
