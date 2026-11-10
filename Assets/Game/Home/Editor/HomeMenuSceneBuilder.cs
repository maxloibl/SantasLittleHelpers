using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SantasLittleHelpers.Home.Editor
{
    public static class HomeMenuSceneBuilder
    {
        public const string Root = "Assets/Game/Home";
        public const string ScenePath = Root + "/Scenes/Home.unity";
        public const string PanelSettingsPath = Root + "/HomeMenuPanelSettings.asset";
        public const string StyleSheetPath = Root + "/HomeMenu.uss";
        private const string ThemePath = Root + "/HomeMenuTheme.tss";

        [MenuItem("Tools/Home/Build Home Scene")]
        public static void BuildScene()
        {
            EnsureFolder(Root);
            EnsureFolder(Root + "/Scenes");

            AssetDatabase.ImportAsset(
                StyleSheetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            }

            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1600, 900);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            panelSettings.sortingOrder = 100;
            panelSettings.clearColor = true;
            panelSettings.colorClearValue = new Color(0.094f, 0.149f, 0.176f, 1f);

            AssetDatabase.ImportAsset(ThemePath, ImportAssetOptions.ForceSynchronousImport);
            ThemeStyleSheet theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null) throw new System.InvalidOperationException("Home UI Toolkit theme could not be imported.");
            var serializedPanel = new SerializedObject(panelSettings);
            serializedPanel.FindProperty("themeUss").objectReferenceValue = theme;
            serializedPanel.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(panelSettings);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("Home UI Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.094f, 0.149f, 0.176f, 1f);
            camera.cullingMask = 0;
            cameraObject.AddComponent<AudioListener>();

            GameObject home = new GameObject("Home Menu");
            UIDocument document = home.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.sortingOrder = 100;

            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (styleSheet == null)
            {
                styleSheet = AssetDatabase.LoadAllAssetsAtPath(StyleSheetPath).OfType<StyleSheet>().FirstOrDefault();
            }

            if (styleSheet == null) throw new System.InvalidOperationException("Home styles could not be imported.");
            HomeMenuController controller = home.AddComponent<HomeMenuController>();
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("document").objectReferenceValue = document;
            serialized.FindProperty("styleSheet").objectReferenceValue = styleSheet;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            home.AddComponent<HomeMenuPlayModeSmokeDriver>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"HOME_SCENE_BUILT path={ScenePath} panel={PanelSettingsPath} styles={(styleSheet != null ? "yes" : "missing")}");
        }

        [MenuItem("Tools/Home/Run Home Menu PlayMode Smoke")]
        public static void RunPlayModeSmoke()
        {
            BuildScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
