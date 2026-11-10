using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SantasLittleHelpers.Networking.Session.Editor
{
    public static class SessionTransportTestSceneBuilder
    {
        private const string Root = "Assets/Game/Networking/Session";
        private const string PrefabPath = Root + "/Prefabs/SessionTestPlayer.prefab";
        private const string ScenePath = Root + "/Scenes/SessionTransportTest.unity";

        [MenuItem("Tools/Networking/Build Session Transport Test Player")]
        public static void BuildTestPlayer()
        {
            EnsureFolder(Root + "/Prefabs");
            EnsureFolder(Root + "/Scenes");

            GameObject player = new GameObject("Session Test Player");
            player.AddComponent<NetworkObject>();
            PrefabUtility.SaveAsPrefabAsset(player, PrefabPath);
            Object.DestroyImmediate(player);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject harness = new GameObject("Session Transport Test Harness");
            SessionTransportTestHarness testHarness = harness.AddComponent<SessionTransportTestHarness>();
            var serializedHarness = new SerializedObject(testHarness);
            serializedHarness.FindProperty("playerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            serializedHarness.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Directory.CreateDirectory("C:/tmp/slh39-test");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "C:/tmp/slh39-test/SessionTransportTest.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            Debug.Log($"SESSION_TEST_BUILD result={report.summary.result} errors={report.summary.totalErrors} warnings={report.summary.totalWarnings}");
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.InvalidOperationException("Session transport test player build failed.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
