using SantasLittleHelpers;
using SantasLittleHelpers.Delivery;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SantasLittleHelpers.Editor
{
    public static class CoopDeliverySetupBuilder
    {
        private const string Root = "Assets/Game/Delivery";
        private const string PlayerPath = Root + "/DeliveryNetworkPlayer.prefab";
        private const string AuthorityPath = Root + "/Prefabs/CoopMissionAuthority.prefab";
        private const string ResidentPath = Root + "/Prefabs/CoopResident.prefab";
        private const string ScenePath = "Assets/Game/Scenes/DeliveryHouse.unity";

        [MenuItem("Tools/Delivery/Build Coop Setup in DeliveryHouse")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath) != null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(AuthorityPath) != null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(ResidentPath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                if (Object.FindAnyObjectByType<CoopDeliveryEntry>() != null)
                {
                    InstallProductionVisuals();
                    return;
                }
            }

            BuildPlayer();
            BuildAuthority();
            BuildResident();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            CoopDeliveryEntry existing = Object.FindFirstObjectByType<CoopDeliveryEntry>();
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            GameObject root = new GameObject("Solo / Host / Client Entry");
            CoopDeliveryEntry entry = root.AddComponent<CoopDeliveryEntry>();
            SerializedObject data = new SerializedObject(entry);
            data.FindProperty("playerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            data.FindProperty("lightGiftPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Gifts/Prefabs/LightGift.prefab");
            data.FindProperty("heavyGiftPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Gifts/Prefabs/HeavyGift.prefab");
            data.FindProperty("authorityPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(AuthorityPath);
            data.FindProperty("residentPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ResidentPath);
            data.FindProperty("soloRound").objectReferenceValue = Object.FindFirstObjectByType<SantasLittleHelpers.Delivery.DeliveryRoundController>();
            data.FindProperty("soloPlayer").objectReferenceValue = GameObject.Find("LocalPlayer — Sleigh Start");
            data.FindProperty("sleigh").objectReferenceValue = GameObject.Find("SLED PARKING — Return Here")?.transform;
            data.FindProperty("deliveryTrigger").objectReferenceValue = GameObject.Find("Gift Delivery Zone")?.GetComponent<Collider>();
            data.FindProperty("residents").arraySize = Object.FindObjectsByType<SantasLittleHelpers.AI.ResidentBrain>(FindObjectsSortMode.None).Length;
            SantasLittleHelpers.AI.ResidentBrain[] residents = Object.FindObjectsByType<SantasLittleHelpers.AI.ResidentBrain>(FindObjectsSortMode.None);
            for (int i = 0; i < residents.Length; i++)
            {
                data.FindProperty("residents").GetArrayElementAtIndex(i).objectReferenceValue = residents[i];
                NetworkObject sceneNetworkObject = residents[i].GetComponent<NetworkObject>();
                if (sceneNetworkObject != null) Object.DestroyImmediate(sceneNetworkObject);
                Unity.Netcode.Components.NetworkTransform sceneNetworkTransform = residents[i].GetComponent<Unity.Netcode.Components.NetworkTransform>();
                if (sceneNetworkTransform != null) Object.DestroyImmediate(sceneNetworkTransform);
                SantasLittleHelpers.Delivery.NetworkResidentState sceneNetworkState = residents[i].GetComponent<SantasLittleHelpers.Delivery.NetworkResidentState>();
                if (sceneNetworkState != null) Object.DestroyImmediate(sceneNetworkState);
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            Transform soloPlayer = GameObject.Find("LocalPlayer — Sleigh Start")?.transform;
            if (soloPlayer != null)
            {
                Transform previous = soloPlayer.Find("Cozy Elf Visual");
                if (previous != null) Object.DestroyImmediate(previous.gameObject);
                GameObject soloVisual = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Art/Prefabs/CozyElf_Visual.prefab")) as GameObject;
                if (soloVisual != null)
                {
                    soloVisual.name = "Cozy Elf Visual"; soloVisual.transform.SetParent(soloPlayer, false); soloVisual.transform.localScale = Vector3.one * .5f;
                }
            }
            GameObject deliveryVisual = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Art/Prefabs/DeliveryTarget_Visual.prefab")) as GameObject;
            if (deliveryVisual != null)
            {
                deliveryVisual.name = "Delivery Target Visual";
                Transform trigger = GameObject.Find("Gift Delivery Zone")?.transform;
                if (trigger != null)
                {
                    Transform previous;
                    while ((previous = trigger.Find("Delivery Target Visual")) != null) Object.DestroyImmediate(previous.gameObject);
                    deliveryVisual.transform.SetParent(trigger, false); deliveryVisual.transform.localPosition = Vector3.zero;
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            InstallProductionVisuals();
            Debug.Log("COOP_SETUP_READY: Solo/Host/Client entry and mission-owned player prefab wired in DeliveryHouse.");
        }

        [MenuItem("Tools/Delivery/Build DeliveryHouse Windows Player")]
        public static void BuildWindowsPlayer()
        {
            Build();
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "C:/tmp/slh12-build/DeliveryHouse.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            });
            Debug.Log($"COOP_BUILD result={report.summary.result} errors={report.summary.totalErrors} warnings={report.summary.totalWarnings} path=C:/tmp/slh12-build/DeliveryHouse.exe");
            if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors > 0)
                throw new System.InvalidOperationException($"DeliveryHouse Windows build failed: {report.summary.result}, errors={report.summary.totalErrors}");
        }

        private static void BuildPlayer()
        {
            GameObject player = new GameObject("Delivery Network Player") { layer = 8 };
            CharacterController character = player.AddComponent<CharacterController>();
            character.height = 1.8f; character.radius = .32f; character.center = new Vector3(0, .9f, 0);
            player.AddComponent<NetworkObject>();
            player.AddComponent<SantasLittleHelpers.Networking.OwnerNetworkTransform>();
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Elf Visual Body"; body.transform.SetParent(player.transform, false); body.transform.localPosition = new Vector3(0, .9f, 0); body.transform.localScale = new Vector3(.65f,.9f,.65f);
            Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());
            body.GetComponent<Renderer>().enabled = false;
            GameObject visual = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Art/Prefabs/CozyElf_Visual.prefab")) as GameObject;
            if (visual != null) { visual.name = "Cozy Elf Visual"; visual.transform.SetParent(player.transform, false); visual.transform.localScale = Vector3.one * .5f; }
            GameObject view = new GameObject("Local Ego Camera") { layer = 8 };
            view.transform.SetParent(player.transform, false); view.transform.localPosition = new Vector3(0, 1.6f, 0);
            Camera camera = view.AddComponent<Camera>(); camera.tag = "MainCamera"; view.AddComponent<AudioListener>(); view.SetActive(false);
            GameObject anchor = new GameObject("Carry Anchor"); anchor.transform.SetParent(view.transform, false); anchor.transform.localPosition = new Vector3(0,-.2f,1.35f);
            FirstPersonController controller = player.AddComponent<FirstPersonController>(); controller.enabled = false;
            SerializedObject move = new SerializedObject(controller); move.FindProperty("viewPivot").objectReferenceValue = view.transform; move.ApplyModifiedPropertiesWithoutUndo();
            GiftInteractor interactor = player.AddComponent<GiftInteractor>();
            SerializedObject gift = new SerializedObject(interactor); gift.FindProperty("viewCamera").objectReferenceValue = camera; gift.FindProperty("carryAnchor").objectReferenceValue = anchor.transform; gift.ApplyModifiedPropertiesWithoutUndo();
            SantasLittleHelpers.Delivery.DeliveryPlayerOwnership ownership = player.AddComponent<SantasLittleHelpers.Delivery.DeliveryPlayerOwnership>();
            SerializedObject owner = new SerializedObject(ownership); owner.FindProperty("localView").objectReferenceValue = view; owner.FindProperty("controller").objectReferenceValue = controller; owner.ApplyModifiedPropertiesWithoutUndo();
            player.AddComponent<SantasLittleHelpers.Capture.CapturedPlayerState>();
            player.AddComponent<SantasLittleHelpers.Capture.CaptureRescueInput>();
            player.AddComponent<SantasLittleHelpers.Delivery.CoopSmokePlayerDriver>();
            PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
            Object.DestroyImmediate(player);
            AssetDatabase.SaveAssets();
        }

        private static void BuildAuthority()
        {
            System.IO.Directory.CreateDirectory("Assets/Game/Delivery/Prefabs");
            GameObject authority = new GameObject("Coop Mission Authority");
            authority.AddComponent<NetworkObject>();
            authority.AddComponent<SantasLittleHelpers.Capture.CaptureRoundCoordinator>();
            authority.AddComponent<SantasLittleHelpers.Delivery.CoopNetworkRound>();
            PrefabUtility.SaveAsPrefabAsset(authority, AuthorityPath);
            Object.DestroyImmediate(authority);
        }

        private static void BuildResident()
        {
            System.IO.Directory.CreateDirectory("Assets/Game/Delivery/Prefabs");
            GameObject resident = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            resident.name = "Coop Resident";
            string materialPath = AssetDatabase.GUIDToAssetPath("31321ba15b8f8eb4c954353edc038b1d");
            Material residentMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (residentMaterial != null) resident.GetComponent<Renderer>().sharedMaterial = residentMaterial;
            resident.AddComponent<NetworkObject>();
            resident.AddComponent<Unity.Netcode.Components.NetworkTransform>();
            resident.AddComponent<SantasLittleHelpers.AI.ResidentBrain>();
            resident.AddComponent<SantasLittleHelpers.Delivery.NetworkResidentState>();
            PrefabUtility.SaveAsPrefabAsset(resident, ResidentPath);
            Object.DestroyImmediate(resident);
        }

        [MenuItem("Tools/Delivery/Install Production Visuals")]
        public static void InstallProductionVisuals()
        {
            const string characterKit = "Assets/Game/Art/Characters/Prefabs/";
            const string missionKit = "Assets/Game/Art/MissionKit/Prefabs/";
            EditPrefab(PlayerPath, player => InstallCharacter(player, "Cozy Elf Visual", characterKit + "Elf_Visual.prefab", true));
            EditPrefab(ResidentPath, resident => InstallCharacter(resident, "Resident Visual", characterKit + "Resident_Visual.prefab", false));
            EditPrefab("Assets/Game/Gifts/Prefabs/LightGift.prefab", gift =>
                InstallGift(gift, missionKit + "LightGift_Visual.prefab", new Vector3(2f, 1.75f, 2f)));
            EditPrefab("Assets/Game/Gifts/Prefabs/HeavyGift.prefab", gift =>
                InstallGift(gift, missionKit + "HeavyGift_Visual.prefab", Vector3.one * 2f));

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject soloPlayer = RequireSceneObject("LocalPlayer — Sleigh Start");
            InstallCharacter(soloPlayer, "Cozy Elf Visual", characterKit + "Elf_Visual.prefab", true);
            // Serialize the hidden owner mesh for the authored solo player.
            foreach (Renderer renderer in soloPlayer.transform.Find("Cozy Elf Visual").GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;

            foreach (SantasLittleHelpers.AI.ResidentBrain brain in
                     Object.FindObjectsByType<SantasLittleHelpers.AI.ResidentBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                InstallCharacter(brain.gameObject, "Resident Visual", characterKit + "Resident_Visual.prefab", false);

            GameObject goal = RequireSceneObject("Gift Delivery Zone");
            Transform goalVisual = SetVisual(goal.transform, "Delivery Target Visual", missionKit + "DeliveryGoal_Visual.prefab");
            goalVisual.localPosition = Vector3.down * (goal.GetComponent<BoxCollider>().size.y * .5f);
            goalVisual.localScale = new Vector3(1.45f, 1f, 1f);

            GameObject sleigh = RequireSceneObject("SLED PARKING — Return Here");
            HideRootRenderer(sleigh);
            Transform sleighVisual = SetVisual(sleigh.transform, "Sleigh Visual", missionKit + "Sleigh_Visual.prefab");
            sleighVisual.localPosition = sleigh.transform.InverseTransformPoint(
                new Vector3(sleigh.transform.position.x, 0f, sleigh.transform.position.z));
            Vector3 sleighScale = sleigh.transform.lossyScale;
            sleighVisual.localScale = new Vector3(2f / sleighScale.x, 2f / sleighScale.y, 2f / sleighScale.z);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("DELIVERY_PRODUCTION_VISUALS_INSTALLED scene=DeliveryHouse models=6");
        }

        private static GameObject RequireSceneObject(string name)
        {
            return GameObject.Find(name) ?? throw new System.InvalidOperationException("Missing scene object: " + name);
        }

        private static void EditPrefab(string path, System.Action<GameObject> edit)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void InstallCharacter(GameObject root, string childName, string path, bool animateJump)
        {
            HideRootRenderer(root);
            Transform visual = SetVisual(root.transform, childName, path);
            if (root.TryGetComponent(out CapsuleCollider body))
                visual.localPosition = Vector3.up * (body.center.y - body.height * .5f);
            Animator animator = visual.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.applyRootMotion)
                throw new System.InvalidOperationException("Missing in-place character animator: " + path);

            DeliveryCharacterVisual motion = root.GetComponent<DeliveryCharacterVisual>();
            if (motion == null) motion = root.AddComponent<DeliveryCharacterVisual>();
            var data = new SerializedObject(motion);
            data.FindProperty("animator").objectReferenceValue = animator;
            data.FindProperty("visualRoot").objectReferenceValue = visual;
            data.FindProperty("animateJump").boolValue = animateJump;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void InstallGift(GameObject root, string path, Vector3 scale)
        {
            HideRootRenderer(root);
            Transform visual = SetVisual(root.transform, "Cranberry Gift Visual", path);
            visual.localPosition = Vector3.down * .5f;
            visual.localScale = scale;
        }

        private static void HideRootRenderer(GameObject root)
        {
            if (root.TryGetComponent(out Renderer renderer)) renderer.enabled = false;
        }

        private static Transform SetVisual(Transform parent, string name, string path)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new System.InvalidOperationException("Missing visual wrapper: " + path);

            Transform existing = parent.Find(name);
            GameObject visual = existing != null ? existing.gameObject : null;
            if (visual != null && PrefabUtility.GetCorrespondingObjectFromSource(visual) != asset)
            {
                Object.DestroyImmediate(visual);
                visual = null;
            }
            if (visual == null) visual = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            visual.name = name;
            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            visual.transform.localScale = Vector3.one;
            return visual.transform;
        }
    }
}
