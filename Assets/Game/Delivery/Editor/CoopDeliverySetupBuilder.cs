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
            BuildPlayer();
            BuildAuthority();
            BuildResident();
            IntegrateGiftVisuals();
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

        private static void IntegrateGiftVisuals()
        {
            string visualPath = "Assets/Game/Art/Prefabs/Gift_Cranberry_Visual.prefab";
            foreach (string giftPath in new[] { "Assets/Game/Gifts/Prefabs/LightGift.prefab", "Assets/Game/Gifts/Prefabs/HeavyGift.prefab" })
            {
                GameObject gift = PrefabUtility.LoadPrefabContents(giftPath);
                if (gift.transform.Find("Cranberry Gift Visual") == null)
                {
                    GameObject visual = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(visualPath)) as GameObject;
                    visual.name = "Cranberry Gift Visual"; visual.transform.SetParent(gift.transform, false);
                }
                PrefabUtility.SaveAsPrefabAsset(gift, giftPath);
                PrefabUtility.UnloadPrefabContents(gift);
            }
        }
    }
}
