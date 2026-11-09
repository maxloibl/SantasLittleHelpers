using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;

namespace SantasLittleHelpers.Editor
{
    public static class GiftTestSceneBuilder
    {
        private const string Root = "Assets/Game/Gifts";

        [MenuItem("Tools/Gifts/Configure Network Prefabs")]
        public static void ConfigureNetworkPrefabs()
        {
            foreach (string name in new[] { "LightGift", "HeavyGift" })
            {
                string path = Root + "/Prefabs/" + name + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (instance.GetComponent<NetworkObject>() == null) instance.AddComponent<NetworkObject>();
                if (instance.GetComponent<NetworkTransform>() == null) instance.AddComponent<NetworkTransform>();
                NetworkRigidbody networkRigidbody = instance.GetComponent<NetworkRigidbody>();
                if (networkRigidbody == null) networkRigidbody = instance.AddComponent<NetworkRigidbody>();
                ConfigureNetworkRigidbody(networkRigidbody);
                if (instance.GetComponent<NetworkGiftCarryable>() == null) instance.AddComponent<NetworkGiftCarryable>();
                PrefabUtility.SaveAsPrefabAsset(instance, path);
                Object.DestroyImmediate(instance);
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Gifts/Validate Solo Prefab Physics")]
        public static void ValidateSoloPrefabPhysics()
        {
            foreach (string name in new[] { "LightGift", "HeavyGift" })
            {
                string path = Root + "/Prefabs/" + name + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                GameObject instance = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab) : null;
                if (instance == null) throw new System.InvalidOperationException($"Missing gift prefab: {path}");
                Rigidbody body = instance.GetComponent<Rigidbody>();
                NetworkRigidbody networkRigidbody = instance.GetComponent<NetworkRigidbody>();
                bool dynamicBeforeSpawn = body != null && !body.isKinematic && body.useGravity
                    && networkRigidbody != null && !networkRigidbody.AutoUpdateKinematicState
                    && !networkRigidbody.AutoSetKinematicOnDespawn;
                Debug.Log($"GIFT_TEST_SOLO_PREFAB name={name} dynamicBeforeSpawn={dynamicBeforeSpawn} kinematic={body?.isKinematic} gravity={body?.useGravity} autoKinematic={networkRigidbody?.AutoUpdateKinematicState}");
                Object.DestroyImmediate(instance);
                if (!dynamicBeforeSpawn) throw new System.InvalidOperationException($"Gift is not dynamic before network spawn: {name}");
            }
        }

        public static void BuildAndValidateSolo()
        {
            Build();
            ValidateSoloPrefabPhysics();
        }

        [MenuItem("Tools/Gifts/Build Test Scene")]
        public static void Build()
        {
            EnsureFolder(Root + "/Prefabs");
            EnsureFolder(Root + "/Scenes");
            Material lightMat = MakeMaterial("Gift Light", new Color(0.85f, 0.15f, 0.12f));
            Material heavyMat = MakeMaterial("Gift Heavy", new Color(0.12f, 0.3f, 0.85f));
            CreateGift("LightGift", 0.8f, 0.55f, lightMat);
            CreateGift("HeavyGift", 8f, 0.9f, heavyMat);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Test Floor";
            floor.transform.position = new Vector3(0, -0.25f, 0);
            floor.transform.localScale = new Vector3(12, 0.5f, 12);
            floor.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Test Floor", new Color(0.28f, 0.32f, 0.28f));
            CreateWall("Left Wall", new Vector3(-2, 1.5f, 3), new Vector3(0.25f, 3, 6));
            CreateWall("Right Wall", new Vector3(2, 1.5f, 3), new Vector3(0.25f, 3, 6));
            CreateWall("Back Wall", new Vector3(0, 1.5f, 6), new Vector3(4, 3, 0.25f));
            CreateWall("Carry Obstacle", new Vector3(0, 0.65f, 2.4f), new Vector3(1.2f, 1.3f, 0.35f));

            GameObject player = new GameObject("Gift Test Player");
            player.layer = 8;
            player.transform.position = new Vector3(0, 0, -3);
            CharacterController character = player.AddComponent<CharacterController>();
            character.height = 1.8f;
            character.radius = 0.35f;
            character.center = new Vector3(0, 0.9f, 0);
            character.skinWidth = 0.08f;
            GameObject cameraObject = new GameObject("View Camera");
            cameraObject.layer = 8;
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 1.6f, 0);
            cameraObject.tag = "MainCamera";
            Camera cam = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            GameObject anchor = new GameObject("Carry Anchor");
            anchor.transform.SetParent(cameraObject.transform, false);
            anchor.transform.localPosition = new Vector3(0, -0.2f, 1.35f);
            GiftInteractor interactor = player.AddComponent<GiftInteractor>();
            SerializedObject serialized = new SerializedObject(interactor);
            serialized.FindProperty("viewCamera").objectReferenceValue = cam;
            serialized.FindProperty("carryAnchor").objectReferenceValue = anchor.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            SantasLittleHelpers.FirstPersonController controller = player.AddComponent<SantasLittleHelpers.FirstPersonController>();
            SerializedObject controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("viewPivot").objectReferenceValue = cameraObject.transform;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            SpawnGift("Light Gift", "LightGift", new Vector3(-0.8f, 0.3f, 0));
            SpawnGift("Heavy Gift", "HeavyGift", new Vector3(0.8f, 0.45f, 0));
            EditorSceneManager.SaveScene(scene, Root + "/Scenes/GiftPhysicsTest.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("Gift physics test scene built. WASD and mouse look use the existing controller; E picks up and drops gifts.");
        }

        private static void CreateGift(string name, float mass, float size, Material material)
        {
            GameObject gift = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gift.name = name;
            gift.transform.localScale = Vector3.one * size;
            gift.GetComponent<Renderer>().sharedMaterial = material;
            Rigidbody body = gift.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.linearDamping = 0.15f;
            gift.AddComponent<GiftCarryable>();
            gift.AddComponent<NetworkObject>();
            gift.AddComponent<NetworkTransform>();
            ConfigureNetworkRigidbody(gift.AddComponent<NetworkRigidbody>());
            gift.AddComponent<NetworkGiftCarryable>();
            PrefabUtility.SaveAsPrefabAsset(gift, Root + "/Prefabs/" + name + ".prefab");
            Object.DestroyImmediate(gift);
        }

        private static void SpawnGift(string name, string prefabName, Vector3 position)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + prefabName + ".prefab");
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.position = position;
        }

        private static void CreateWall(string name, Vector3 position, Vector3 scale)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().sharedMaterial = MakeMaterial(name + " Material", new Color(0.42f, 0.39f, 0.32f));
        }

        private static Material MakeMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            string path = Root + "/" + name.Replace(" ", "") + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureNetworkRigidbody(NetworkRigidbody networkRigidbody)
        {
            networkRigidbody.AutoUpdateKinematicState = false;
            networkRigidbody.AutoSetKinematicOnDespawn = false;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string folder = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}

