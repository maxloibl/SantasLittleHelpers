using SantasLittleHelpers.Gifts.Tests;
using SantasLittleHelpers.Networking;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SantasLittleHelpers.Editor
{
    public static class GiftCoopNetworkTestSceneBuilder
    {
        private const string Root = "Assets/Game/Gifts/Tests";
        private const string ScenePath = Root + "/Scenes/GiftCoopNetworkTest.unity";
        private const string PlayerPath = Root + "/GiftCoopTestPlayer.prefab";
        private const string PrefabListPath = Root + "/GiftCoopNetworkPrefabs.asset";

        [MenuItem("Tools/Gifts/Build Coop Network Test")]
        public static void Build()
        {
            EnsureFolder(Root);
            EnsureFolder(Root + "/Scenes");
            GameObject player = CreatePlayer();
            PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
            Object.DestroyImmediate(player);

            GameObject lightGift = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Gifts/Prefabs/LightGift.prefab");
            GameObject heavyGift = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Gifts/Prefabs/HeavyGift.prefab");
            NetworkPrefabsList prefabList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(PrefabListPath);
            if (prefabList == null)
            {
                prefabList = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(prefabList, PrefabListPath);
            }
            foreach (NetworkPrefab oldPrefab in new System.Collections.Generic.List<NetworkPrefab>(prefabList.PrefabList))
                if (oldPrefab.Prefab != null) prefabList.Remove(oldPrefab);
            prefabList.Add(new NetworkPrefab { Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath) });
            prefabList.Add(new NetworkPrefab { Prefab = lightGift });
            prefabList.Add(new NetworkPrefab { Prefab = heavyGift });
            EditorUtility.SetDirty(prefabList);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateBox("Test Floor", new Vector3(0f, -0.25f, 0f), new Vector3(16f, 0.5f, 16f), new Color(0.25f, 0.3f, 0.25f));
            GameObject managerObject = new GameObject("Gift Coop Network Manager");
            NetworkManager manager = managerObject.AddComponent<NetworkManager>();
            UnityTransport transport = managerObject.AddComponent<UnityTransport>();
            manager.NetworkConfig.NetworkTransport = transport;
            SerializedObject serialized = new SerializedObject(manager);
            SerializedProperty config = serialized.FindProperty("NetworkConfig");
            SerializedProperty playerPrefab = config?.FindPropertyRelative("PlayerPrefab");
            if (playerPrefab == null) playerPrefab = serialized.FindProperty("NetworkConfig.PlayerPrefab");
            if (playerPrefab == null) playerPrefab = serialized.FindProperty("NetworkConfig.m_PlayerPrefab");
            if (playerPrefab == null) Debug.LogError("Could not configure gift test PlayerPrefab.");
            else playerPrefab.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            SerializedProperty lists = serialized.FindProperty("NetworkConfig.Prefabs.NetworkPrefabsLists");
            if (lists == null && config != null)
                lists = config.FindPropertyRelative("Prefabs")?.FindPropertyRelative("NetworkPrefabsLists");
            if (lists == null) Debug.LogError("Could not configure gift test network prefab list.");
            else
            {
                lists.arraySize = 1;
                lists.GetArrayElementAtIndex(0).objectReferenceValue = prefabList;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            LocalNetworkJoin join = managerObject.AddComponent<LocalNetworkJoin>();
            GiftNetworkTestBootstrap bootstrap = managerObject.AddComponent<GiftNetworkTestBootstrap>();
            SerializedObject bootstrapData = new SerializedObject(bootstrap);
            bootstrapData.FindProperty("lightGiftPrefab").objectReferenceValue = lightGift;
            bootstrapData.FindProperty("heavyGiftPrefab").objectReferenceValue = heavyGift;
            bootstrapData.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"GIFT_TEST_BUILT scene={ScenePath} join={join != null}");
        }

        public static void BuildWindowsPlayer()
        {
            string output = System.Environment.GetEnvironmentVariable("SLH9_TEST_OUTPUT");
            if (string.IsNullOrEmpty(output)) output = "C:/tmp/slh9-test/GiftCoopTest.exe";
            string directory = System.IO.Path.GetDirectoryName(output);
            if (!System.IO.Directory.Exists(directory)) System.IO.Directory.CreateDirectory(directory);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            Debug.Log($"GIFT_TEST_BUILD result={report.summary.result} errors={report.summary.totalErrors} output={output}");
        }

        private static GameObject CreatePlayer()
        {
            GameObject root = new GameObject("Gift Coop Test Player");
            root.layer = 8;
            CharacterController character = root.AddComponent<CharacterController>();
            character.height = 1.8f; character.radius = .35f; character.center = new Vector3(0f, .9f, 0f);
            root.AddComponent<NetworkObject>();
            root.AddComponent<OwnerNetworkTransform>();
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Player Body"; body.transform.SetParent(root.transform, false); body.transform.localPosition = new Vector3(0f, .9f, 0f);
            Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());
            GameObject view = new GameObject("Local View");
            view.layer = 8; view.transform.SetParent(root.transform, false); view.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            Camera camera = view.AddComponent<Camera>(); camera.tag = "MainCamera";
            view.AddComponent<AudioListener>();
            GameObject anchor = new GameObject("Carry Anchor"); anchor.transform.SetParent(view.transform, false); anchor.transform.localPosition = new Vector3(0f, -.2f, 1.35f);
            GiftInteractor interactor = root.AddComponent<GiftInteractor>();
            SerializedObject interactorData = new SerializedObject(interactor);
            interactorData.FindProperty("viewCamera").objectReferenceValue = camera;
            interactorData.FindProperty("carryAnchor").objectReferenceValue = anchor.transform;
            interactorData.ApplyModifiedPropertiesWithoutUndo();
            FirstPersonController controller = root.AddComponent<FirstPersonController>();
            controller.enabled = false;
            SerializedObject controllerData = new SerializedObject(controller);
            controllerData.FindProperty("viewPivot").objectReferenceValue = view.transform;
            controllerData.ApplyModifiedPropertiesWithoutUndo();
            GiftCoopTestOwnership owner = root.AddComponent<GiftCoopTestOwnership>();
            SerializedObject ownerData = new SerializedObject(owner);
            ownerData.FindProperty("localView").objectReferenceValue = view;
            ownerData.FindProperty("controller").objectReferenceValue = controller;
            ownerData.FindProperty("interactor").objectReferenceValue = interactor;
            ownerData.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<GiftNetworkTestDriver>();
            view.SetActive(false);
            return root;
        }

        private static void CreateBox(string name, Vector3 position, Vector3 scale, Color color)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name; box.transform.position = position; box.transform.localScale = scale;
            Renderer renderer = box.GetComponent<Renderer>();
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = color };
            renderer.sharedMaterial = material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
