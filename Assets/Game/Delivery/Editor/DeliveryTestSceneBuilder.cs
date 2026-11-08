using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Reflection;
using SantasLittleHelpers.Delivery;

namespace SantasLittleHelpers.Editor
{
    public static class DeliveryTestSceneBuilder
    {
        private const string Root = "Assets/Game/Delivery";
        [MenuItem("Tools/Delivery/Build Solo Round Test Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MakeBlock("Ground", new Vector3(0, -0.25f, 4), new Vector3(10, 0.5f, 20), new Color(.24f,.34f,.25f));
            GameObject sleigh = MakeBlock("Sleigh (start and return)", new Vector3(0, .45f, -4), new Vector3(2, .9f, 1.4f), new Color(.65f,.12f,.08f));
            GameObject house = MakeBlock("House delivery area", new Vector3(0, 1.5f, 9), new Vector3(8, 3, .4f), new Color(.35f,.22f,.12f));
            GameObject target = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Gifts/Prefabs/LightGift.prefab")) as GameObject;
            target.name = "Required Gift (deliver this one)"; target.transform.position = new Vector3(0, .35f, 2);
            GameObject distractor = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Gifts/Prefabs/HeavyGift.prefab")) as GameObject;
            distractor.name = "Other Gift (does not count)"; distractor.transform.position = new Vector3(2, .5f, 3);
            GameObject player = new GameObject("Solo Player"); player.layer = 8; player.transform.position = new Vector3(0,0,-2.5f);
            CharacterController character = player.AddComponent<CharacterController>(); character.height=1.8f; character.radius=.35f; character.center=new Vector3(0,.9f,0);
            GameObject view = new GameObject("View Camera"); view.layer=8; view.tag="MainCamera"; view.transform.SetParent(player.transform,false); view.transform.localPosition=new Vector3(0,1.6f,0);
            Camera camera = view.AddComponent<Camera>(); view.AddComponent<AudioListener>();
            GameObject anchor=new GameObject("Carry Anchor"); anchor.transform.SetParent(view.transform,false); anchor.transform.localPosition=new Vector3(0,-.2f,1.35f);
            GiftInteractor interactor=player.AddComponent<GiftInteractor>();
            SerializedObject interaction=new SerializedObject(interactor); interaction.FindProperty("viewCamera").objectReferenceValue=camera; interaction.FindProperty("carryAnchor").objectReferenceValue=anchor.transform; interaction.ApplyModifiedPropertiesWithoutUndo();
            SantasLittleHelpers.FirstPersonController movement=player.AddComponent<SantasLittleHelpers.FirstPersonController>();
            SerializedObject movementData=new SerializedObject(movement); movementData.FindProperty("viewPivot").objectReferenceValue=view.transform; movementData.ApplyModifiedPropertiesWithoutUndo();
            GameObject roundObject=new GameObject("Solo Delivery Round"); DeliveryRoundController round=roundObject.AddComponent<DeliveryRoundController>();
            SerializedObject roundData=new SerializedObject(round); roundData.FindProperty("player").objectReferenceValue=player.transform; roundData.FindProperty("sleigh").objectReferenceValue=sleigh.transform; roundData.FindProperty("requiredGift").objectReferenceValue=target.GetComponent<GiftCarryable>(); roundData.FindProperty("returnRadius").floatValue=2f; roundData.ApplyModifiedPropertiesWithoutUndo();
            GameObject zone=new GameObject("Delivery Trigger (correct gift only)"); zone.transform.position=new Vector3(0,.8f,7.2f);
            BoxCollider trigger=zone.AddComponent<BoxCollider>(); trigger.isTrigger=true; trigger.size=new Vector3(3,2,2);
            GiftDeliveryZone delivery=zone.AddComponent<GiftDeliveryZone>(); SerializedObject deliveryData=new SerializedObject(delivery); deliveryData.FindProperty("round").objectReferenceValue=round; deliveryData.ApplyModifiedPropertiesWithoutUndo();
            MakeBlock("Delivery marker", new Vector3(0,.05f,7.2f), new Vector3(3,.1f,2), new Color(.95f,.75f,.1f));
            EditorSceneManager.SaveScene(scene, Root+"/Scenes/SoloDeliveryTest.unity"); AssetDatabase.SaveAssets();
            Debug.Log("Solo delivery scene built. E picks/drops; deliver the light gift to the marked trigger, return to the sleigh, then press R to reset.");
        }
        public static void RunBatchSmoke()
        {
            Build();
            Scene scene = EditorSceneManager.OpenScene(Root + "/Scenes/SoloDeliveryTest.unity");
            DeliveryRoundController round = Object.FindAnyObjectByType<DeliveryRoundController>();
            GiftDeliveryZone zone = Object.FindAnyObjectByType<GiftDeliveryZone>();
            GameObject player = GameObject.Find("Solo Player");
            GiftCarryable target = GameObject.Find("Required Gift (deliver this one)").GetComponent<GiftCarryable>();
            GiftCarryable other = GameObject.Find("Other Gift (does not count)").GetComponent<GiftCarryable>();
            if (round == null || zone == null || player == null || target == null || other == null)
                throw new System.Exception("Delivery smoke setup is incomplete.");
            Transform sleigh = GameObject.Find("Sleigh (start and return)").transform;
            Vector3 expectedStartPosition = player.transform.position;
            Quaternion expectedStartRotation = player.transform.rotation;
            typeof(DeliveryRoundController).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(round, null);
            Physics.SyncTransforms();
            if (Physics.ComputePenetration(player.GetComponent<CharacterController>(), player.transform.position, player.transform.rotation,
                    sleigh.GetComponent<Collider>(), sleigh.position, sleigh.rotation, out _, out _))
                throw new System.Exception("Player start pose overlaps the sleigh collider.");
            zone.TryHandleGift(other);
            if (round.IsDelivered) throw new System.Exception("Wrong gift was counted.");
            zone.TryHandleGift(target);
            zone.TryHandleGift(target);
            if (!round.IsDelivered || round.State != DeliveryRoundState.ReturnToSleigh)
                throw new System.Exception("Correct gift did not enter the return state exactly once.");
            player.transform.position = GameObject.Find("Sleigh (start and return)").transform.position;
            round.CheckForReturn();
            if (round.State != DeliveryRoundState.Complete) throw new System.Exception("Returning to the sleigh did not complete the round.");
            round.RestartRound();
            if (round.State != DeliveryRoundState.DeliverGift || round.IsDelivered)
                throw new System.Exception("Restart did not reset round progress.");
            if ((player.transform.position - expectedStartPosition).sqrMagnitude > .001f ||
                Quaternion.Angle(player.transform.rotation, expectedStartRotation) > .1f)
                throw new System.Exception("Restart did not restore the collision-free player start pose.");
            if ((target.transform.position - new Vector3(0, .35f, 2)).sqrMagnitude > .001f)
                throw new System.Exception("Restart did not restore the gift position.");
            Debug.Log("DELIVERY_SMOKE_PASS: wrong gift ignored; correct gift counted once; return completed; restart restored round and gift.");
        }
        private static GameObject MakeBlock(string name, Vector3 position, Vector3 scale, Color color)
        {
            GameObject block=GameObject.CreatePrimitive(PrimitiveType.Cube); block.name=name; block.transform.position=position; block.transform.localScale=scale;
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard"); Material material=new Material(shader){color=color}; block.GetComponent<Renderer>().sharedMaterial=material; return block;
        }
    }
}
