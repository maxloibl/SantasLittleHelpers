using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using SantasLittleHelpers;
using SantasLittleHelpers.AI;
using SantasLittleHelpers.Capture;
using SantasLittleHelpers.Networking;

namespace SantasLittleHelpers.Capture.Editor
{
    public static class CaptureTestSceneBuilder
    {
        private const string Root = "Assets/Game/Capture";
        private const string ScenePath = Root + "/Scenes/CaptureTest.unity";
        [MenuItem("Tools/Capture/Build Capture Test Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Test Floor"; floor.transform.position = new Vector3(0,-.25f,0); floor.transform.localScale = new Vector3(30,.5f,30);
            GameObject net = new GameObject("Capture Network Manager");
            NetworkManager manager = net.AddComponent<NetworkManager>();
            manager.NetworkConfig.NetworkTransport = net.AddComponent<UnityTransport>();
            GameObject player = new GameObject("Capture Test Player");
            player.layer = 8;
            CharacterController cc = player.AddComponent<CharacterController>(); cc.height = 1.8f; cc.center = new Vector3(0,.9f,0);
            player.AddComponent<NetworkObject>(); player.AddComponent<OwnerNetworkTransform>();
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule); body.name = "Visible Player"; body.transform.SetParent(player.transform,false); body.transform.localPosition = new Vector3(0,.9f,0); Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());
            GameObject view = new GameObject("Local View"); view.transform.SetParent(player.transform,false); view.transform.localPosition = new Vector3(0,1.6f,0); view.AddComponent<Camera>(); view.AddComponent<AudioListener>(); view.SetActive(false);
            NetworkPlayerOwnership ownership = player.AddComponent<NetworkPlayerOwnership>();
            SerializedObject so = new SerializedObject(ownership); so.FindProperty("localView").objectReferenceValue=view; so.ApplyModifiedPropertiesWithoutUndo();
            FirstPersonController controller=player.AddComponent<FirstPersonController>(); controller.enabled=false; so.Update(); so.FindProperty("controller").objectReferenceValue=controller; so.ApplyModifiedPropertiesWithoutUndo();
            SerializedObject cso=new SerializedObject(controller); cso.FindProperty("viewPivot").objectReferenceValue=view.transform; cso.ApplyModifiedPropertiesWithoutUndo();
            player.AddComponent<CapturedPlayerState>();
            player.AddComponent<CaptureRescueInput>();
            PrefabUtility.SaveAsPrefabAsset(player, Root + "/CaptureTestPlayer.prefab"); Object.DestroyImmediate(player);
            SerializedObject nso=new SerializedObject(manager); SerializedProperty config=nso.FindProperty("NetworkConfig"); SerializedProperty pp=config.FindPropertyRelative("PlayerPrefab"); pp.objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/CaptureTestPlayer.prefab"); nso.ApplyModifiedPropertiesWithoutUndo();
            GameObject residentObject = GameObject.CreatePrimitive(PrimitiveType.Capsule); residentObject.name = "Test Chasing Resident"; residentObject.transform.position = new Vector3(-4, 0, -3); residentObject.transform.rotation = Quaternion.identity;
            ResidentBrain resident = residentObject.AddComponent<ResidentBrain>();
            ResidentCaptureContact contact = residentObject.AddComponent<ResidentCaptureContact>();
            SerializedObject rso = new SerializedObject(contact); rso.FindProperty("resident").objectReferenceValue = resident; rso.ApplyModifiedPropertiesWithoutUndo();
            GameObject coordinator = new GameObject("Capture Round Coordinator"); coordinator.AddComponent<NetworkObject>(); coordinator.AddComponent<CaptureRoundCoordinator>();
            net.AddComponent<CaptureTestNetworkJoin>();
            EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
        }
    }
}
