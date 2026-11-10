using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using SantasLittleHelpers.Cabin;

namespace SantasLittleHelpers.Cabin.Editor
{
    public static class CozyCabinGreyboxBuilder
    {
        private const string CabinFolder = "Assets/Game/Cabin";
        private const string GreyboxFolder = CabinFolder + "/Greybox";
        private const string MaterialsFolder = GreyboxFolder + "/Materials";
        private const string ScenesFolder = CabinFolder + "/Scenes";
        private const string ScenePath = ScenesFolder + "/CozyCabin.unity";
        private const string RootName = "CabinRoot";
        private const string IntegrationName = "CabinIntegration";
        private const string GreyboxName = "Greybox";
        private const string VisualsName = "Visuals";
        private const string CollidersName = "Colliders";
        private const string SpawnAnchorsName = "SpawnAnchors";
        private static readonly Vector3 RootOffset = new Vector3(100f, 0f, 0f);
        private static readonly Vector3[] SpawnPositions =
        {
            new Vector3(-1.1f, 0f, -3.5f),
            new Vector3(1.1f, 0f, -3.5f),
            new Vector3(-1.1f, 0f, -1.3f),
            new Vector3(1.1f, 0f, -1.3f)
        };
        private static readonly Vector3 MissionStartPosition = new Vector3(0f, 0f, 5.4f);
        private static readonly Vector3 PromptPosition = new Vector3(0f, 1.3f, 4.9f);
        private static readonly Vector3 PromptSize = new Vector3(3f, 2.6f, 1.8f);
        private static readonly Vector3 DeparturePosition = new Vector3(0f, 0f, 6.6f);

        [MenuItem("Tools/Cabin/Build Cozy Cabin Greybox")]
        public static void BuildAndValidate()
        {
            Build();
            ValidateBuiltScene();
            Debug.Log("CABIN_GREYBOX_BUILD_PASS root=" + Format(RootOffset) +
                      " scene=" + ScenePath +
                      " spawnAnchors=" + SpawnPositions.Length +
                      " missionStart=" + Format(MissionStartPosition) +
                      " departure=+Z");
        }

        public static void Build()
        {
            EnsureFolder(CabinFolder);
            EnsureFolder(ScenesFolder);
            EnsureFolder(GreyboxFolder);
            EnsureFolder(MaterialsFolder);

            Material floor = CreateMaterial("CabinFloor", new Color(.43f, .27f, .15f), .05f);
            Material wall = CreateMaterial("CabinWall", new Color(.50f, .31f, .17f), .04f);
            Material trim = CreateMaterial("CabinTrim", new Color(.23f, .12f, .06f), .08f);
            Material stone = CreateMaterial("CabinStone", new Color(.34f, .32f, .29f), .02f);
            Material snow = CreateMaterial("CabinSnow", new Color(.72f, .87f, .91f), .12f);
            Material ember = CreateMaterial("CabinEmber", new Color(1f, .30f, .05f), .15f,
                new Color(2.2f, .5f, .08f));
            Material rug = CreateMaterial("CabinRug", new Color(.45f, .09f, .12f), .02f);
            Material cream = CreateMaterial("CabinCream", new Color(1f, .91f, .72f), .05f);
            Material gold = CreateMaterial("CabinGold", new Color(1f, .63f, .16f), .25f);
            Material dark = CreateMaterial("CabinDark", new Color(.06f, .04f, .03f), .02f);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.42f, .48f, .58f);
            RenderSettings.fog = false;

            GameObject root = new GameObject(RootName);
            root.transform.position = RootOffset;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            Transform greybox = CreateNode(GreyboxName, root.transform);
            Transform visuals = CreateNode(VisualsName, greybox);
            Transform colliders = CreateNode(CollidersName, greybox);

            BuildShell(visuals, colliders, floor, wall, trim, snow, gold);
            BuildInterior(visuals, colliders, floor, wall, trim, stone, ember, rug, cream, gold, dark);
            BuildOutside(visuals, colliders, floor, stone, snow, gold);
            BuildSpawnFootprints(visuals, cream);

            Transform spawnAnchors = CreateNode(SpawnAnchorsName, root.transform);
            Transform[] players = new Transform[SpawnPositions.Length];
            for (int i = 0; i < SpawnPositions.Length; i++)
            {
                Transform anchor = CreateNode("PlayerSpawn" + (i + 1), spawnAnchors);
                anchor.localPosition = SpawnPositions[i];
                anchor.localRotation = Quaternion.identity;
                players[i] = anchor;
            }

            Transform integration = CreateNode(IntegrationName, root.transform);
            GameObject promptObject = new GameObject("HostPromptTrigger");
            promptObject.transform.SetParent(integration, false);
            promptObject.transform.localPosition = PromptPosition;
            BoxCollider promptTrigger = promptObject.AddComponent<BoxCollider>();
            promptTrigger.isTrigger = true;
            promptTrigger.size = PromptSize;
            Transform promptAnchor = CreateNode("PromptAnchor", promptObject.transform);
            promptAnchor.localPosition = new Vector3(0f, .3f, 0f);
            Transform missionStart = CreateNode("MissionStartAnchor", integration);
            missionStart.localPosition = MissionStartPosition;
            Transform departure = CreateNode("DepartureDirection", integration);
            departure.localPosition = DeparturePosition;
            departure.localRotation = Quaternion.identity;
            CabinAnchors anchors = integration.gameObject.AddComponent<CabinAnchors>();
            SerializedObject anchorData = new SerializedObject(anchors);
            SerializedProperty spawnArray = anchorData.FindProperty("playerSpawnAnchors");
            spawnArray.arraySize = players.Length;
            for (int i = 0; i < players.Length; i++)
                spawnArray.GetArrayElementAtIndex(i).objectReferenceValue = players[i];
            anchorData.FindProperty("missionStartAnchor").objectReferenceValue = missionStart;
            anchorData.FindProperty("departureDirection").objectReferenceValue = departure;
            anchorData.FindProperty("promptAnchor").objectReferenceValue = promptAnchor;
            anchorData.FindProperty("promptTrigger").objectReferenceValue = promptTrigger;
            anchorData.ApplyModifiedPropertiesWithoutUndo();

            GameObject lightObject = new GameObject("Warm Key Light");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 6f, 0f);
            lightObject.transform.localRotation = Quaternion.Euler(45f, -25f, 0f);
            Light key = lightObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, .88f, .74f);
            key.intensity = 1.25f;
            key.shadows = LightShadows.Soft;
            RenderSettings.sun = key;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("CABIN_GREYBOX_GENERATED scene=" + ScenePath);
        }

        public static void ValidateBuiltScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject root = FindRoot(scene, RootName);
            Require(root != null, "Missing " + RootName + ".");
            Require(Vector3.Distance(root.transform.position, RootOffset) < .001f,
                "Cabin root offset changed.");
            Require(root.transform.rotation == Quaternion.identity, "Cabin root rotation must stay identity.");

            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                Require(rootObject.GetComponentInChildren<Camera>(true) == null,
                    "Production cabin scene must not contain a Camera.");
                Require(rootObject.GetComponentInChildren<AudioListener>(true) == null,
                    "Production cabin scene must not contain an AudioListener.");
            }

            Transform greybox = root.transform.Find(GreyboxName);
            Require(greybox != null, "Missing Greybox root.");
            Transform visuals = greybox.Find(VisualsName);
            Transform colliders = greybox.Find(CollidersName);
            Require(visuals != null, "Missing Greybox/Visuals.");
            Require(colliders != null, "Missing Greybox/Colliders.");
            Require(visuals.GetComponentsInChildren<Collider>(true).Length == 0,
                "Greybox visuals must not own colliders.");
            Require(colliders.GetComponentsInChildren<Renderer>(true).Length == 0,
                "Greybox colliders must not own renderers.");
            Require(colliders.GetComponentsInChildren<Collider>(true).Length > 0,
                "Greybox colliders are missing.");

            Transform spawnRoot = root.transform.Find(SpawnAnchorsName);
            Require(spawnRoot != null, "Missing SpawnAnchors.");
            Require(spawnRoot.childCount == SpawnPositions.Length, "Spawn anchor count must be four.");
            for (int i = 0; i < SpawnPositions.Length; i++)
            {
                Transform anchor = spawnRoot.GetChild(i);
                Require(anchor.name == "PlayerSpawn" + (i + 1), "Spawn anchors must keep stable ordered names.");
                Require(Vector3.Distance(anchor.localPosition, SpawnPositions[i]) < .001f,
                    "Spawn anchor position changed.");
                Require(anchor.localRotation == Quaternion.identity, "Spawn anchors must face +Z.");
            }

            Transform integration = root.transform.Find(IntegrationName);
            Require(integration != null, "Missing CabinIntegration.");
            CabinAnchors anchorsComponent = integration.GetComponent<CabinAnchors>();
            Require(anchorsComponent != null, "Missing CabinAnchors on CabinIntegration.");
            Transform[] wiredAnchors = anchorsComponent.PlayerSpawnAnchors;
            Require(wiredAnchors != null && wiredAnchors.Length == SpawnPositions.Length,
                "CabinAnchors must expose four spawn anchors.");
            for (int i = 0; i < SpawnPositions.Length; i++)
                Require(wiredAnchors[i] == spawnRoot.GetChild(i), "Spawn anchor reference order changed.");
            Require(anchorsComponent.MissionStartAnchor != null &&
                    Vector3.Distance(anchorsComponent.MissionStartAnchor.localPosition, MissionStartPosition) < .001f,
                "Mission start anchor is not wired.");
            Require(anchorsComponent.DepartureDirection != null &&
                    Vector3.Dot(anchorsComponent.DepartureDirection.forward, Vector3.forward) > .999f,
                "Departure direction must face +Z.");
            Require(anchorsComponent.PromptAnchor != null, "Prompt anchor is not wired.");
            Require(anchorsComponent.PromptTrigger != null && anchorsComponent.PromptTrigger.isTrigger,
                "Prompt trigger must be a wired trigger collider.");
            Require(anchorsComponent.PromptTrigger is BoxCollider,
                "Prompt trigger must use a BoxCollider.");
        }

        private static void BuildShell(Transform visuals, Transform colliders,
            Material floor, Material wall, Material trim, Material snow, Material gold)
        {
            AddBox("Shell/Floor", new Vector3(0f, -.2f, 0f), new Vector3(17.2f, .4f, 13.6f), floor, visuals, colliders);
            AddBox("Shell/Ceiling", new Vector3(0f, 3.8f, 0f), new Vector3(17.2f, .4f, 13.6f), wall, visuals, colliders);
            AddBox("Shell/WestWall", new Vector3(-8.2f, 1.8f, 0f), new Vector3(.4f, 3.6f, 12.8f), wall, visuals, colliders);
            AddBox("Shell/EastWall", new Vector3(8.2f, 1.8f, 0f), new Vector3(.4f, 3.6f, 12.8f), wall, visuals, colliders);
            AddBox("Shell/SouthWallLeft", new Vector3(-3.2f, 1.8f, -6.2f), new Vector3(10.4f, 3.6f, .4f), wall, visuals, colliders);
            AddBox("Shell/SouthWallRight", new Vector3(6.2f, 1.8f, -6.2f), new Vector3(4.4f, 3.6f, .4f), wall, visuals, colliders);
            AddBox("Shell/SouthWallBelowWindow", new Vector3(3f, .7f, -6.2f), new Vector3(2f, 1.4f, .4f), wall, visuals, colliders);
            AddBox("Shell/SouthWallAboveWindow", new Vector3(3f, 3.1f, -6.2f), new Vector3(2f, 1f, .4f), wall, visuals, colliders);
            AddBox("Shell/NorthWallLeft", new Vector3(-4.85f, 1.8f, 6.2f), new Vector3(7.1f, 3.6f, .4f), wall, visuals, colliders);
            AddBox("Shell/NorthWallRight", new Vector3(4.85f, 1.8f, 6.2f), new Vector3(7.1f, 3.6f, .4f), wall, visuals, colliders);
            AddBox("Shell/NorthWallHeader", new Vector3(0f, 3.1f, 6.2f), new Vector3(2.6f, 1f, .4f), wall, visuals, colliders);

            AddVisual("Door/JambLeft", new Vector3(-1.36f, 1.3f, 5.98f), new Vector3(.12f, 2.6f, .16f), trim, visuals);
            AddVisual("Door/JambRight", new Vector3(1.36f, 1.3f, 5.98f), new Vector3(.12f, 2.6f, .16f), trim, visuals);
            AddVisual("Door/Lintel", new Vector3(0f, 2.66f, 5.98f), new Vector3(2.84f, .12f, .16f), trim, visuals);
            AddVisual("Door/Threshold", new Vector3(0f, .015f, 5.98f), new Vector3(2.6f, .03f, .5f), gold, visuals);
            AddVisual("Window/Sill", new Vector3(3f, 1.4f, -5.98f), new Vector3(2.2f, .12f, .12f), trim, visuals);
            AddVisual("Window/Header", new Vector3(3f, 2.6f, -5.98f), new Vector3(2.2f, .12f, .12f), trim, visuals);
            AddVisual("Window/LeftJamb", new Vector3(1.94f, 2f, -5.98f), new Vector3(.12f, 1.32f, .12f), trim, visuals);
            AddVisual("Window/RightJamb", new Vector3(4.06f, 2f, -5.98f), new Vector3(.12f, 1.32f, .12f), trim, visuals);
            AddVisual("Window/CrossBar", new Vector3(3f, 2f, -5.98f), new Vector3(2f, .06f, .1f), trim, visuals);
            AddVisual("Window/Glass", new Vector3(3f, 2f, -6.18f), new Vector3(2f, 1.2f, .04f), snow, visuals);

            for (int i = -3; i <= 3; i++)
                AddVisual("Ceiling/Beam" + i, new Vector3(0f, 3.47f, i * 1.6f),
                    new Vector3(16f, .16f, .18f), trim, visuals);
            AddVisual("Trim/WestBaseboard", new Vector3(-7.96f, .12f, 0f), new Vector3(.08f, .24f, 12.4f), trim, visuals);
            AddVisual("Trim/EastBaseboard", new Vector3(7.96f, .12f, 0f), new Vector3(.08f, .24f, 12.4f), trim, visuals);
            AddVisual("Trim/SouthBaseboard", new Vector3(0f, .12f, -5.96f), new Vector3(16f, .24f, .08f), trim, visuals);
            AddVisual("Trim/NorthBaseboardLeft", new Vector3(-4.85f, .12f, 5.96f), new Vector3(7.1f, .24f, .08f), trim, visuals);
            AddVisual("Trim/NorthBaseboardRight", new Vector3(4.85f, .12f, 5.96f), new Vector3(7.1f, .24f, .08f), trim, visuals);
        }

        private static void BuildInterior(Transform visuals, Transform colliders,
            Material floor, Material wall, Material trim, Material stone, Material ember,
            Material rug, Material cream, Material gold, Material dark)
        {
            AddBox("Fireplace/Body", new Vector3(-7.5f, 1.1f, 0f), new Vector3(1.2f, 2.2f, 2.8f), stone, visuals, colliders);
            AddBox("Fireplace/Hearth", new Vector3(-6.95f, .1f, 0f), new Vector3(1.7f, .2f, 3.2f), stone, visuals, colliders);
            AddBox("Fireplace/Mantel", new Vector3(-7.05f, 2.3f, 0f), new Vector3(1.6f, .18f, 3.2f), trim, visuals, colliders);
            AddBox("Fireplace/Chimney", new Vector3(-7.6f, 2.95f, 0f), new Vector3(1f, 1.3f, 1.1f), stone, visuals, colliders);
            AddVisual("Fireplace/Firebox", new Vector3(-6.88f, .7f, 0f), new Vector3(.16f, 1f, 1.5f), dark, visuals);
            AddVisual("Fireplace/Embers", new Vector3(-6.86f, .33f, 0f), new Vector3(.08f, .3f, 1.1f), ember, visuals);

            AddBox("Workbench/Top", new Vector3(7.35f, 1f, 0f), new Vector3(1.3f, .14f, 3.6f), wall, visuals, colliders);
            AddBox("Workbench/LegNorthWest", new Vector3(7f, .5f, 1.5f), new Vector3(.12f, 1f, .12f), trim, visuals, colliders);
            AddBox("Workbench/LegNorthEast", new Vector3(7.7f, .5f, 1.5f), new Vector3(.12f, 1f, .12f), trim, visuals, colliders);
            AddBox("Workbench/LegSouthWest", new Vector3(7f, .5f, -1.5f), new Vector3(.12f, 1f, .12f), trim, visuals, colliders);
            AddBox("Workbench/LegSouthEast", new Vector3(7.7f, .5f, -1.5f), new Vector3(.12f, 1f, .12f), trim, visuals, colliders);
            AddBox("Workbench/Cabinet", new Vector3(7.5f, .45f, -1.2f), new Vector3(1f, .9f, 1.2f), wall, visuals, colliders);
            AddVisual("Workbench/Backboard", new Vector3(8.02f, 1.65f, 0f), new Vector3(.08f, 1.2f, 3.4f), trim, visuals);
            AddVisual("Workbench/ToolBlock", new Vector3(7.3f, 1.13f, .9f), new Vector3(.4f, .12f, .2f), gold, visuals);
            AddVisual("Workbench/Cloth", new Vector3(7.3f, 1.15f, -.7f), new Vector3(.32f, .16f, .32f), cream, visuals);

            AddVisual("Rug/Center", new Vector3(0f, .02f, .6f), new Vector3(3.2f, .02f, 5f), rug, visuals);
            AddVisual("Rug/Border", new Vector3(0f, .025f, 2.6f), new Vector3(2.4f, .02f, .16f), cream, visuals);
        }

        private static void BuildOutside(Transform visuals, Transform colliders,
            Material floor, Material stone, Material snow, Material gold)
        {
            AddBox("Outside/PorchFloor", new Vector3(0f, -.1f, 7.9f), new Vector3(7f, .2f, 3.2f), floor, visuals, colliders);
            AddBox("Outside/SnowGround", new Vector3(0f, -.2f, 16f), new Vector3(16f, .4f, 14f), snow, visuals, colliders);
            AddBox("Outside/SnowBankLeft", new Vector3(-3.8f, .4f, 8.6f), new Vector3(1.6f, .8f, 2.4f), snow, visuals, colliders);
            AddBox("Outside/SnowBankRight", new Vector3(3.8f, .4f, 8.6f), new Vector3(1.6f, .8f, 2.4f), snow, visuals, colliders);
            AddVisual("Outside/SouthSnowBackdrop", new Vector3(3f, 1.9f, -8.8f), new Vector3(10f, 4f, .4f), snow, visuals);
            AddVisual("Outside/DeparturePostLeft", new Vector3(-2.5f, 1f, 9f), new Vector3(.14f, 2f, .14f), stone, visuals);
            AddVisual("Outside/DeparturePostRight", new Vector3(2.5f, 1f, 9f), new Vector3(.14f, 2f, .14f), stone, visuals);
            AddVisual("Outside/DepartureLintel", new Vector3(0f, 2f, 9f), new Vector3(5.14f, .14f, .14f), gold, visuals);
        }

        private static void BuildSpawnFootprints(Transform visuals, Material cream)
        {
            for (int i = 0; i < SpawnPositions.Length; i++)
            {
                Vector3 position = SpawnPositions[i];
                position.y = .015f;
                AddVisual("StartFootprints/Footprint" + (i + 1), position,
                    new Vector3(1.5f, .03f, 1.5f), cream, visuals);
            }
        }

        private static void AddBox(string name, Vector3 position, Vector3 size, Material material,
            Transform visuals, Transform colliders)
        {
            AddVisual(name, position, size, material, visuals);
            AddCollider(name, position, size, colliders);
        }

        private static void AddVisual(string name, Vector3 position, Vector3 size, Material material,
            Transform visuals)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = name;
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(visuals, false);
            visual.transform.localPosition = position;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = size;
            visual.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void AddCollider(string name, Vector3 position, Vector3 size, Transform colliders)
        {
            GameObject colliderObject = new GameObject(name);
            colliderObject.transform.SetParent(colliders, false);
            colliderObject.transform.localPosition = position;
            colliderObject.transform.localRotation = Quaternion.identity;
            colliderObject.transform.localScale = Vector3.one;
            BoxCollider box = colliderObject.AddComponent<BoxCollider>();
            box.size = size;
        }

        private static Transform CreateNode(string name, Transform parent)
        {
            GameObject node = new GameObject(name);
            node.transform.SetParent(parent, false);
            node.transform.localPosition = Vector3.zero;
            node.transform.localRotation = Quaternion.identity;
            node.transform.localScale = Vector3.one;
            return node.transform;
        }

        private static Material CreateMaterial(string name, Color color, float smoothness, Color? emission = null)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.name = name;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("CABIN_GREYBOX_VALIDATION_FAIL: " + message);
        }

        private static string Format(Vector3 value)
        {
            return value.x.ToString("0.###") + "," + value.y.ToString("0.###") + "," + value.z.ToString("0.###");
        }
    }
}
