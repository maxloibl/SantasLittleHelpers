using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SantasLittleHelpers.Gifts.Tests
{
    /// <summary>Host-only runtime spawner for the isolated network test scene.</summary>
    public sealed class GiftNetworkTestBootstrap : MonoBehaviour
    {
        [SerializeField] private GameObject lightGiftPrefab;
        [SerializeField] private GameObject heavyGiftPrefab;
        private NetworkManager manager;

        private void Awake() => manager = GetComponent<NetworkManager>();

        private IEnumerator Start()
        {
            while (manager == null || !manager.IsServer) yield return null;
            SpawnGift(heavyGiftPrefab, new Vector3(0f, 0.5f, 0f), "HeavyGift Network Test");
            SpawnGift(lightGiftPrefab, new Vector3(1.4f, 0.4f, 0f), "LightGift Network Test");
            Debug.Log("GIFT_TEST_HOST_SPAWN gifts=2");
        }

        private static void SpawnGift(GameObject prefab, Vector3 position, string instanceName)
        {
            if (prefab == null) { Debug.LogError($"GIFT_TEST_SETUP_FAIL missing prefab {instanceName}"); return; }
            GameObject instance = Instantiate(prefab, position, Quaternion.identity);
            instance.name = instanceName;
            instance.GetComponent<NetworkObject>().Spawn();
        }
    }
}
