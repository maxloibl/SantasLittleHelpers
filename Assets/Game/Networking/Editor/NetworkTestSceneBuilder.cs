using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using SantasLittleHelpers.Networking;

namespace SantasLittleHelpers.Editor
{
    public static class NetworkTestSceneBuilder
    {
        private const string Root = "Assets/Game/Networking";
        private const string ScenePath = Root + "/Scenes/NetworkMovementTest.unity";

        [MenuItem("Tools/Networking/Build Movement Test Scene")]
        public static void Build()
        {
            EnsureFolder(Root + "/Scenes");
            GameObject player = CreatePlayer();
            PrefabUtility.SaveAsPrefabAsset(player, Root + "/NetworkPlayer.prefab");
            Object.DestroyImmediate(player);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateBox("Ground", new Vector3(0, -0.25f, 0), new Vector3(30, 0.5f, 30), new Color(.22f,.3f,.23f));
            CreateBox("Center divider", new Vector3(0, 1.5f, 0), new Vector3(.2f, 3, 10), new Color(.55f,.4f,.25f));
            for (int i = 0; i < 4; i++) CreateBox("Marker " + i, new Vector3(i < 2 ? -4 : 4, .5f, i % 2 == 0 ? -4 : 4), Vector3.one, i < 2 ? Color.cyan : Color.magenta);

            GameObject net = new GameObject("Network Manager");
            NetworkManager manager = net.AddComponent<NetworkManager>();
            UnityTransport transport = net.AddComponent<UnityTransport>();
            manager.NetworkConfig.NetworkTransport = transport;
            SerializedObject serialized = new SerializedObject(manager);
            SerializedProperty config = serialized.FindProperty("NetworkConfig");
            SerializedProperty playerPrefab = config?.FindPropertyRelative("PlayerPrefab");
            if (playerPrefab == null) playerPrefab = serialized.FindProperty("NetworkConfig.PlayerPrefab");
            if (playerPrefab == null) playerPrefab = serialized.FindProperty("NetworkConfig.m_PlayerPrefab");
            if (playerPrefab == null) Debug.LogError("Could not find NetworkManager player prefab field.");
            else playerPrefab.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/NetworkPlayer.prefab");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            net.AddComponent<LocalNetworkJoin>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Network movement test scene generated at " + ScenePath);
        }

        [MenuItem("Tools/Networking/Build Windows Development Test Player")]
        public static void BuildWindowsPlayer()
        {
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/NetworkMovementTest/NetworkMovementTest.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"NETWORK_BUILD result={report.summary.result} errors={report.summary.totalErrors} warnings={report.summary.totalWarnings}");
        }

        private static GameObject CreatePlayer()
        {
            GameObject root = new GameObject("Network Player");
            root.layer = 8;
            root.transform.position = new Vector3(-3, 0, 0);
            CharacterController character = root.AddComponent<CharacterController>();
            character.height = 1.8f; character.radius = .35f; character.center = new Vector3(0,.9f,0); character.skinWidth = .08f;
            NetworkObject networkObject = root.AddComponent<NetworkObject>();
            root.AddComponent<OwnerNetworkTransform>();
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Remote visible body"; body.transform.SetParent(root.transform, false); body.transform.localPosition = new Vector3(0,.9f,0); body.transform.localScale = new Vector3(.65f,.9f,.65f);
            Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());
            body.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Remote Player", new Color(.8f,.18f,.15f));
            GameObject view = new GameObject("Local View"); view.layer = 8; view.transform.SetParent(root.transform, false); view.transform.localPosition = new Vector3(0,1.6f,0);
            Camera camera = view.AddComponent<Camera>(); camera.tag = "MainCamera"; view.AddComponent<AudioListener>();
            view.SetActive(false);
            NetworkPlayerOwnership owner = root.AddComponent<NetworkPlayerOwnership>();
            SerializedObject ownSerialized = new SerializedObject(owner);
            ownSerialized.FindProperty("localView").objectReferenceValue = view;
            ownSerialized.FindProperty("controller").objectReferenceValue = null;
            ownSerialized.ApplyModifiedPropertiesWithoutUndo();
            FirstPersonController controller = root.AddComponent<FirstPersonController>();
            controller.enabled = false;
            SerializedObject control = new SerializedObject(controller); control.FindProperty("viewPivot").objectReferenceValue = view.transform; control.ApplyModifiedPropertiesWithoutUndo();
            ownSerialized.Update(); ownSerialized.FindProperty("controller").objectReferenceValue = controller; ownSerialized.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static void CreateBox(string name, Vector3 position, Vector3 scale, Color color)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name; box.transform.position = position; box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = MakeMaterial(name, color);
        }

        private static Material MakeMaterial(string name, Color color)
        {
            string path = Root + "/" + name.Replace(" ", "") + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { name = name }; AssetDatabase.CreateAsset(material,path); }
            material.color = color; EditorUtility.SetDirty(material); return material;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\','/');
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}



