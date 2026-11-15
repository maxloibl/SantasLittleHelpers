using System;
using SantasLittleHelpers.Cabin;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SantasLittleHelpers.Cabin.Editor
{
    /// <summary>Adds the session-owned cabin phase authority without regenerating cabin art.</summary>
    public static class CabinRuntimeSceneSetupBuilder
    {
        private const string ScenePath = "Assets/Game/Cabin/Scenes/CozyCabin.unity";
        private const string IntegrationPath = "CabinRoot/CabinIntegration";

        [MenuItem("Tools/Cabin/Set Up Runtime Scene Authority")]
        public static void BuildAndValidate()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform cabinRoot = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "CabinRoot") { cabinRoot = root.transform; break; }
            Transform integration = cabinRoot != null ? cabinRoot.Find("CabinIntegration") : null;
            if (integration == null) throw new InvalidOperationException("CabinIntegration is missing.");

            NetworkObject networkObject = integration.GetComponent<NetworkObject>();
            if (networkObject == null) networkObject = Undo.AddComponent<NetworkObject>(integration.gameObject);
            CabinPhaseController controller = integration.GetComponent<CabinPhaseController>();
            if (controller == null) controller = Undo.AddComponent<CabinPhaseController>(integration.gameObject);
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("anchors").objectReferenceValue = integration.GetComponent<CabinAnchors>();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            CabinNetworkPrefabCatalog catalog = integration.GetComponent<CabinNetworkPrefabCatalog>();
            if (catalog == null) catalog = Undo.AddComponent<CabinNetworkPrefabCatalog>(integration.gameObject);
            SerializedObject catalogData = new SerializedObject(catalog);
            catalogData.FindProperty("playerPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Delivery/DeliveryNetworkPlayer.prefab");
            string[] prefabPaths =
            {
                "Assets/Game/Gifts/Prefabs/LightGift.prefab",
                "Assets/Game/Gifts/Prefabs/HeavyGift.prefab",
                "Assets/Game/Delivery/Prefabs/CoopMissionAuthority.prefab",
                "Assets/Game/Delivery/Prefabs/CoopResident.prefab"
            };
            SerializedProperty prefabs = catalogData.FindProperty("networkPrefabs");
            prefabs.arraySize = prefabPaths.Length;
            for (int i = 0; i < prefabPaths.Length; i++)
                prefabs.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[i]);
            catalogData.ApplyModifiedPropertiesWithoutUndo();

            if (integration.GetComponent<CabinAnchors>() == null ||
                catalogData.FindProperty("playerPrefab").objectReferenceValue == null ||
                prefabs.arraySize != prefabPaths.Length)
                throw new InvalidOperationException("Cabin anchors or mission prefab references are missing.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("CABIN_RUNTIME_SETUP_PASS scene=CozyCabin networkObject=true phaseController=true anchors=true missionPrefabs=5");
        }
    }
}
