using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.Build.Reporting;

namespace SantasLittleHelpers.Cabin.Tests.Editor
{
    public static class CabinMultiplayerTestSceneBuilder
    {
        private const string ScenePath = "Assets/Game/Cabin/Runtime/Tests/CabinMultiplayerTest.unity";
        private const string PlayerPath = "Assets/Game/Delivery/DeliveryNetworkPlayer.prefab";

        [MenuItem("Santas Little Helpers/Tests/Build Cabin Multiplayer Test Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cabinRoot = new GameObject("Cabin Multiplayer Test");
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Test Floor";
            floor.transform.position = new Vector3(0, -0.1f, 0);
            floor.transform.localScale = new Vector3(20, 0.2f, 20);
            Transform[] spawnPoints = new Transform[4];
            Vector3[] positions = { new Vector3(-1.4f, 0, 0), new Vector3(1.4f, 0, 0), new Vector3(-1.4f, 0, -2), new Vector3(1.4f, 0, -2) };
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                var anchor = new GameObject("PlayerSpawn" + (i + 1));
                anchor.transform.SetParent(cabinRoot.transform, false);
                anchor.transform.localPosition = positions[i];
                spawnPoints[i] = anchor.transform;
            }

            CabinAnchors anchors = cabinRoot.AddComponent<CabinAnchors>();
            var anchorsSerialized = new SerializedObject(anchors);
            SerializedProperty spawnArray = anchorsSerialized.FindProperty("playerSpawnAnchors");
            spawnArray.arraySize = spawnPoints.Length;
            for (int i = 0; i < spawnPoints.Length; i++) spawnArray.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
            anchorsSerialized.ApplyModifiedPropertiesWithoutUndo();

            var networkObjectRoot = new GameObject("Cabin Phase Controller");
            NetworkObject networkObject = networkObjectRoot.AddComponent<NetworkObject>();
            CabinPhaseController controller = networkObjectRoot.AddComponent<CabinPhaseController>();
            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("anchors").objectReferenceValue = anchors;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            CabinMultiplayerTestHarness harness = cabinRoot.AddComponent<CabinMultiplayerTestHarness>();
            var harnessSerialized = new SerializedObject(harness);
            harnessSerialized.FindProperty("cabin").objectReferenceValue = controller;
            harnessSerialized.FindProperty("playerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            harnessSerialized.ApplyModifiedPropertiesWithoutUndo();

            if (networkObject == null) throw new System.InvalidOperationException("Could not create the scene NetworkObject.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("CABIN_TEST_SCENE_BUILT path=" + ScenePath);
        }

        public static void BuildWindowsPlayer()
        {
            Build();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "C:/tmp/slh46-build/SantasLittleHelpersCabinTest.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.InvalidOperationException("Windows cabin test build failed: " + report.summary.result);
            Debug.Log("CABIN_TEST_PLAYER_BUILT path=" + options.locationPathName + " result=" + report.summary.result);
        }
    }
}
