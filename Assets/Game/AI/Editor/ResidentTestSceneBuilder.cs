using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SantasLittleHelpers.AI;

namespace SantasLittleHelpers.AI.Editor
{
    public static class ResidentTestSceneBuilder
    {
        private const string Root = "Assets/Game/AI";

        [MenuItem("Tools/AI/Build Resident Test Scene")]
        public static void BuildScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCube("Floor", new Vector3(0, -0.25f, 0), new Vector3(20, 0.5f, 16), new Color(0.25f, 0.3f, 0.25f));
            CreateCube("Occlusion Wall", new Vector3(0, 1.5f, 2.5f), new Vector3(3, 3, 0.25f), new Color(0.45f, 0.38f, 0.28f));
            GameObject resident = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            resident.name = "Resident (assign waypoints and target in Inspector)";
            resident.transform.position = new Vector3(-2, 1, -2);
            resident.transform.rotation = Quaternion.identity;
            ResidentBrain brain = resident.AddComponent<ResidentBrain>();
            Transform[] points = new Transform[2];
            points[0] = CreateMarker("Patrol A", new Vector3(-3, 0.15f, -1)).transform;
            points[1] = CreateMarker("Patrol B", new Vector3(3, 0.15f, -1)).transform;
            SerializedObject so = new SerializedObject(brain);
            so.FindProperty("patrolPoints").arraySize = 2;
            so.FindProperty("patrolPoints").GetArrayElementAtIndex(0).objectReferenceValue = points[0];
            so.FindProperty("patrolPoints").GetArrayElementAtIndex(1).objectReferenceValue = points[1];
            so.ApplyModifiedPropertiesWithoutUndo();
            GameObject target = CreateMarker("Target (move into view to test chase)", new Vector3(0, 1, 5));
            so = new SerializedObject(brain);
            so.FindProperty("target").objectReferenceValue = target.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, Root + "/Scenes/ResidentAlarmTest.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("Resident test scene built. Select the resident to inspect Alarm and State; the center wall blocks sight.");
        }

        [MenuItem("Tools/AI/Run Resident Batch Smoke")]
        public static void RunBatchSmoke()
        {
            GameObject resident = new GameObject("Smoke Resident");
            GameObject target = new GameObject("Smoke Target");
            GameObject patrolNear = new GameObject("Smoke Patrol Near");
            GameObject patrolFar = new GameObject("Smoke Patrol Far");
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                resident.transform.position = new Vector3(0f, 1f, 0f);
                resident.transform.forward = Vector3.forward;
                ResidentBrain brain = resident.AddComponent<ResidentBrain>();
                CapsuleCollider capsule = resident.GetComponent<CapsuleCollider>();
                Require(capsule != null, "ResidentBrain must require a capsule collider for movement blocking.");
                capsule.center = new Vector3(0f, 1f, 0f);
                capsule.height = 2f;
                capsule.radius = 0.4f;
                patrolNear.transform.position = new Vector3(0f, 0.15f, 0f);
                patrolFar.transform.position = new Vector3(2f, 0.15f, 0f);
                target.transform.position = new Vector3(0, 0, 4);
                SerializedObject so = new SerializedObject(brain);
                so.FindProperty("patrolPoints").arraySize = 2;
                so.FindProperty("patrolPoints").GetArrayElementAtIndex(0).objectReferenceValue = patrolNear.transform;
                so.FindProperty("patrolPoints").GetArrayElementAtIndex(1).objectReferenceValue = patrolFar.transform;
                so.FindProperty("lineOfSightBlockers").intValue = 1 << 9;
                so.FindProperty("movementBlockers").intValue = 1 << 8;
                so.FindProperty("target").objectReferenceValue = target.transform;
                so.FindProperty("sightRange").floatValue = 8f;
                so.FindProperty("fieldOfView").floatValue = 100f;
                so.FindProperty("chaseSightHold").floatValue = 0.6f;
                so.FindProperty("alarmDecayPerSecond").floatValue = 0.5f;
                so.FindProperty("hearingRange").floatValue = 10f;
                so.ApplyModifiedPropertiesWithoutUndo();
                Physics.SyncTransforms();
                brain.SimulateStep(0.01f);
                brain.SimulateStep(0.01f);
                Require(resident.transform.position.x > 0f, "XZ arrival at a lower patrol point must advance to the next waypoint.");
                resident.transform.position = Vector3.zero;
                resident.transform.forward = Vector3.forward;
                so = new SerializedObject(brain);
                so.FindProperty("patrolPoints").arraySize = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
                wall.transform.position = new Vector3(0, 1, 2);
                wall.transform.localScale = new Vector3(2, 3, 0.25f);
                wall.layer = 9;
                Physics.SyncTransforms();
                brain.SimulateStep(1f);
                Require(brain.State == ResidentState.Patrolling, "Occlusion wall must prevent sight alarm.");
                Object.DestroyImmediate(wall);
                Physics.SyncTransforms();
                brain.SimulateStep(0.3f);
                Require(brain.State != ResidentState.Chasing, "Short sight exposure must stay below chase threshold.");
                brain.SimulateStep(0.31f);
                Require(brain.State == ResidentState.Chasing, "Sustained sight must trigger chase.");
                target.transform.position = new Vector3(5, 0, 0);
                brain.SimulateStep(0.1f);
                brain.ReportNoise(new Vector3(0, 0.9f, 3), 0.8f);
                Require(brain.State == ResidentState.Investigating && brain.Alarm > 0f, "Noise must trigger investigation/alarm.");
                for (int i = 0; i < 15; i++) brain.SimulateStep(0.1f);
                Require(brain.State == ResidentState.Patrolling && brain.Alarm > 0.01f,
                    "Resident must reach and leave a height-offset noise point before alarm fully decays.");
                brain.Target = null;
                for (int i = 0; i < 30; i++) brain.SimulateStep(0.1f);
                Require(brain.Alarm < 0.01f && brain.State == ResidentState.Patrolling, "Alarm must decay and return to patrol without stimuli.");
                target.transform.position = new Vector3(10f, 0f, 10f);
                resident.transform.position = new Vector3(0f, 0f, -1f);
                resident.transform.forward = Vector3.forward;
                so = new SerializedObject(brain);
                so.FindProperty("target").objectReferenceValue = null;
                so.FindProperty("patrolPoints").arraySize = 2;
                so.FindProperty("patrolPoints").GetArrayElementAtIndex(0).objectReferenceValue = patrolFar.transform;
                so.FindProperty("patrolPoints").GetArrayElementAtIndex(1).objectReferenceValue = patrolFar.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
                patrolFar.transform.position = new Vector3(0f, 0.15f, 2f);
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = new Vector3(0f, 1f, 0.5f);
                wall.transform.localScale = new Vector3(2f, 3f, 0.25f);
                wall.layer = 8;
                Physics.SyncTransforms();
                brain.SimulateStep(1f);
                Require(resident.transform.position.z < 0.3f && resident.transform.position.z > -1f,
                    "Movement capsule must stop on the near side of a closed wall.");
                Debug.Log("Resident batch smoke passed: patrol waypoint change, wall occlusion, sustained-sight threshold, lower-height noise arrival, alarm decay, and wall-blocked movement.");
            }
            catch (System.Exception exception)
            {
                Debug.LogError("Resident batch smoke failed: " + exception.Message);
                EditorApplication.Exit(1);
                throw;
            }
            finally
            {
                Object.DestroyImmediate(resident);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(patrolNear);
                Object.DestroyImmediate(patrolFar);
                if (wall != null) Object.DestroyImmediate(wall);
            }
        }

        private static GameObject CreateMarker(string name, Vector3 position)
        {
            GameObject marker = new GameObject(name);
            marker.transform.position = position;
            return marker;
        }

        private static void CreateCube(string name, Vector3 position, Vector3 scale, Color color)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = position;
            cube.transform.localScale = scale;
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = color };
            cube.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new System.InvalidOperationException(message);
        }
    }
}
