using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SantasLittleHelpers.Cabin.Presentation.Editor
{
    public static class CozyCabinDressingBuilder
    {
        private const string ScenePath = "Assets/Game/Cabin/Scenes/CozyCabin.unity";
        private const string PrefabFolder = "Assets/Game/Art/CabinKit/Prefabs";
        private const string MaterialFolder = "Assets/Game/Art/CabinKit/Materials";
        private const string RootName = "CabinRoot";
        private const string GreyboxName = "Greybox";
        private const string VisualsName = "Visuals";
        private const string CollidersName = "Colliders";
        private const string PresentationName = "CabinPresentation";
        private const string LogFolder = "Logs";

        private sealed class ShellMetrics
        {
            public float West;
            public float East;
            public float South;
            public float North;
            public float Ceiling;
            public float DoorWidth;
            public float DoorHeight;
            public float WindowWidth;
            public float WindowHeight;
            public float WindowCenterX;
            public float WindowBottom;
        }

        [MenuItem("Tools/Cabin/Apply Cozy Cabin Dressing")]
        public static void ApplyAndValidate()
        {
            Apply(true);
        }

        [MenuItem("Tools/Cabin/Validate Cozy Cabin Dressing")]
        public static void ValidateOnly()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject root = FindRoot(scene, RootName);
            Require(root != null, "Missing " + RootName + ".");
            Transform presentation = root.transform.Find(PresentationName);
            Require(presentation != null, "Missing " + PresentationName + ". Run Apply Cozy Cabin Dressing first.");
            Validate(root.transform, presentation, true);
        }

        public static void Apply(bool capture)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject root = FindRoot(scene, RootName);
            Require(root != null, "Missing " + RootName + ".");
            Transform greybox = root.transform.Find(GreyboxName);
            Require(greybox != null, "Missing Greybox root.");
            Transform visuals = greybox.Find(VisualsName);
            Transform colliders = greybox.Find(CollidersName);
            Require(visuals != null, "Missing Greybox/Visuals.");
            Require(colliders != null, "Missing Greybox/Colliders.");

            string anchorSignature = AnchorSignature(root.transform);
            string colliderSignature = ColliderSignature(colliders);
            ShellMetrics metrics = ReadShellMetrics(colliders);

            Transform previous = root.transform.Find(PresentationName);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            SetGreyboxVisuals(visuals, true);
            if (capture) Capture(root.transform, "CabinDressing_Before.png", new Vector3(0f, 1.65f, -3.4f), new Vector3(0f, 1.45f, 5.6f), 62f);

            Transform presentation = new GameObject(PresentationName).transform;
            presentation.SetParent(root.transform, false);
            presentation.localPosition = Vector3.zero;
            presentation.localRotation = Quaternion.identity;
            presentation.localScale = Vector3.one;

            BuildShellDressing(presentation, metrics);
            BuildFurniture(presentation, metrics);
            BuildWinter(presentation, metrics);
            BuildLighting(presentation, metrics);
            SetGreyboxVisuals(visuals, false);

            string anchorsAfter = AnchorSignature(root.transform);
            string collidersAfter = ColliderSignature(colliders);
            Require(anchorSignature == anchorsAfter, "CabinAnchors changed during dressing.");
            Require(colliderSignature == collidersAfter, "Greybox collider geometry changed during dressing.");

            Validate(root.transform, presentation, false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            if (capture)
            {
                Capture(root.transform, "CabinDressing_AfterSpawn.png", new Vector3(0f, 1.65f, -3.4f), new Vector3(0f, 1.45f, 5.6f), 62f);
                Capture(root.transform, "CabinDressing_AfterWindow.png", new Vector3(3f, 1.62f, -2.7f), new Vector3(3f, 1.9f, -6.1f), 58f);
                Capture(root.transform, "CabinDressing_AfterSouthWest.png", new Vector3(-1.8f, 1.65f, 3.7f), new Vector3(-6.9f, 1.3f, -1.2f), 60f);
                Capture(root.transform, "CabinDressing_AfterWorkbench.png", new Vector3(2.1f, 1.65f, 2.35f), new Vector3(7.4f, 1.15f, -.2f), 58f);
                Capture(root.transform, "CabinDressing_AfterNorthWall.png", new Vector3(4.2f, 1.7f, 2.2f), new Vector3(6.1f, 1.8f, 5.9f), 55f);
            }

            Debug.Log("CABIN_DRESSING_APPLY_PASS root=" + PresentationName +
                      " props=" + presentation.GetComponentsInChildren<Renderer>(true).Length +
                      " lights=" + presentation.GetComponentsInChildren<Light>(true).Length +
                      " colliders=" + presentation.GetComponentsInChildren<Collider>(true).Length +
                      " anchorsPreserved=true collidersPreserved=true scene=" + ScenePath);
        }

        private static void BuildShellDressing(Transform presentation, ShellMetrics metrics)
        {
            Transform group = CreateGroup("FinalShell", presentation);

            float windowLeft = metrics.WindowCenterX - metrics.WindowWidth * .5f;
            float windowRight = metrics.WindowCenterX + metrics.WindowWidth * .5f;
            GameObject shutterLeft = Place("WindowShutterLeft", "CabinWindow_Visual", group);
            Bounds shutterBounds = Measure(shutterLeft);
            ScaleToSize(shutterLeft, .64f, metrics.WindowHeight, shutterBounds.size.z);
            shutterLeft.transform.localPosition = new Vector3(windowLeft - .34f, metrics.WindowBottom, metrics.South + .035f);
            shutterLeft.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            GameObject shutterRight = Place("WindowShutterRight", "CabinWindow_Visual", group);
            ScaleToSize(shutterRight, .64f, metrics.WindowHeight, shutterBounds.size.z);
            shutterRight.transform.localPosition = new Vector3(windowRight + .34f, metrics.WindowBottom, metrics.South + .035f);
            shutterRight.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            GameObject doorway = Place("Doorway", "CabinDoorway_Visual", group);
            Bounds doorBounds = Measure(doorway);
            ScaleToSize(doorway, metrics.DoorWidth + .44f, metrics.DoorHeight + .3f, doorBounds.size.z);
            doorway.transform.localPosition = new Vector3(0f, 0f, metrics.North - .02f);
            doorway.transform.localRotation = Quaternion.identity;
            SetRendererMaterials(doorway, LoadMaterial("CabinTimber"));

            GameObject wreath = Place("Wreath", "Wreath_Visual", group);
            wreath.transform.localPosition = new Vector3(0f, metrics.DoorHeight + .36f, metrics.North - .08f);
            wreath.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            wreath.transform.localScale = Vector3.one;

            GameObject lights = Place("DoorwayLights", "ColoredLights_Visual", group);
            Bounds lightBounds = Measure(lights);
            float lightsScale = lightBounds.size.x > .01f ? Mathf.Min(1f, (metrics.DoorWidth + .5f) / lightBounds.size.x) : 1f;
            lights.transform.localScale = Vector3.one * lightsScale;
            lights.transform.localPosition = new Vector3(0f, metrics.DoorHeight + .12f, metrics.North - .16f);
            lights.transform.localRotation = Quaternion.identity;

            PlaceWallPanels(group, metrics);
        }

        private static void PlaceWallPanels(Transform group, ShellMetrics metrics)
        {
            GameObject template = Place("WallTemplate", "CabinWall_Visual", group);
            Bounds bounds = Measure(template);
            UnityEngine.Object.DestroyImmediate(template);
            bool lengthAlongX = bounds.size.x >= bounds.size.z;
            float moduleWidth = Mathf.Max(.5f, lengthAlongX ? bounds.size.x : bounds.size.z);
            float moduleHeight = Mathf.Max(.5f, bounds.size.y);
            float thickness = Mathf.Max(.06f, lengthAlongX ? bounds.size.z : bounds.size.x);
            float wallHeight = metrics.Ceiling - .1f;
            float verticalScale = wallHeight / moduleHeight;
            float lengthYaw = lengthAlongX ? 0f : 90f;
            Material wallMaterial = CreateWallMaterial();
            Debug.Log("CABIN_DRESSING_WALL_MODULE size=" + Format(bounds.size) +
                      " lengthAlongX=" + lengthAlongX + " moduleWidth=" + moduleWidth.ToString("0.###") +
                      " material=" + wallMaterial.name);

            float usableWidth = metrics.North - metrics.South;
            PlaceWallRun(group, "WallWest", metrics, true, metrics.West + thickness * .5f + .015f,
                metrics.South, usableWidth, moduleWidth, lengthYaw + 90f, verticalScale, lengthAlongX, wallMaterial);
            PlaceWallRun(group, "WallEast", metrics, true, metrics.East - thickness * .5f - .015f,
                metrics.South, usableWidth, moduleWidth, lengthYaw + 90f, verticalScale, lengthAlongX, wallMaterial);

            float southLeftWidth = metrics.WindowCenterX - metrics.WindowWidth * .5f - metrics.West;
            PlaceWallRun(group, "WallSouthLeft", metrics, false, metrics.South + thickness * .5f + .015f,
                metrics.West, southLeftWidth, moduleWidth, lengthYaw + 180f, verticalScale, lengthAlongX, wallMaterial);
            float southRightStart = metrics.WindowCenterX + metrics.WindowWidth * .5f;
            PlaceWallRun(group, "WallSouthRight", metrics, false, metrics.South + thickness * .5f + .015f,
                southRightStart, metrics.East - southRightStart, moduleWidth, lengthYaw + 180f, verticalScale, lengthAlongX, wallMaterial);
            float northLeftEnd = -metrics.DoorWidth * .5f;
            PlaceWallRun(group, "WallNorthLeft", metrics, false, metrics.North - thickness * .5f - .015f,
                metrics.West, northLeftEnd - metrics.West, moduleWidth, lengthYaw, verticalScale, lengthAlongX, wallMaterial);
            float northRightStart = metrics.DoorWidth * .5f;
            PlaceWallRun(group, "WallNorthRight", metrics, false, metrics.North - thickness * .5f - .015f,
                northRightStart, metrics.East - northRightStart, moduleWidth, lengthYaw, verticalScale, lengthAlongX, wallMaterial);
        }

        private static Material CreateWallMaterial()
        {
            const string folder = "Assets/Game/Cabin/Presentation/Materials";
            const string path = folder + "/CabinWallDoubleSided.mat";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Game/Cabin/Presentation", "Materials");
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/CabinWall_Visual.prefab");
            Renderer[] renderers = source != null ? source.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            Require(renderers.Length > 0 && renderers[0].sharedMaterial != null,
                "Cannot read the CabinWall_Visual material.");
            Material template = renderers[0].sharedMaterial;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(template);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.CopyPropertiesFromMaterial(template);
            }
            material.name = "CabinWallDoubleSided";
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
            if (material.HasProperty("_CullMode")) material.SetFloat("_CullMode", (float)CullMode.Off);
            material.doubleSidedGI = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void PlaceWallRun(Transform group, string prefix, ShellMetrics metrics, bool alongZ,
            float wallCoordinate, float start, float length, float moduleWidth, float yaw, float verticalScale,
            bool lengthAlongX, Material wallMaterial)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(length / moduleWidth));
            float step = length / count;
            Vector3 scale = Vector3.one;
            scale.y = verticalScale;
            if (lengthAlongX) scale.x = step / moduleWidth;
            else scale.z = step / moduleWidth;
            for (int i = 0; i < count; i++)
            {
                float offset = start + step * (i + .5f);
                Vector3 position = alongZ
                    ? new Vector3(wallCoordinate, 0f, offset)
                    : new Vector3(offset, 0f, wallCoordinate);
                GameObject panel = Place(prefix + (i + 1), "CabinWall_Visual", group);
                panel.transform.localScale = scale;
                panel.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                panel.transform.localPosition = position;
                GameObject back = Place(prefix + "Back" + (i + 1), "CabinWall_Visual", group);
                back.transform.localScale = scale;
                back.transform.localRotation = Quaternion.Euler(0f, yaw + 180f, 0f);
                back.transform.localPosition = position;
            }
        }

        private static void BuildFurniture(Transform presentation, ShellMetrics metrics)
        {
            Transform group = CreateGroup("Furniture", presentation);

            GameObject fireplace = Place("Fireplace", "Fireplace_Visual", group);
            Bounds fireplaceBounds = Measure(fireplace);
            fireplace.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            fireplace.transform.localPosition = new Vector3(metrics.West + fireplaceBounds.size.z * .5f + .02f, 0f, 0f);

            GameObject workbench = Place("Workbench", "Workbench_Visual", group);
            Bounds workbenchBounds = Measure(workbench);
            workbench.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            workbench.transform.localPosition = new Vector3(metrics.East - workbenchBounds.size.z * .5f - .02f, 0f, -.1f);

            GameObject rug = Place("Rug", "Rug_Visual", group);
            Bounds rugBounds = Measure(rug);
            ScaleToSize(rug, 2.8f, rugBounds.size.y, 4.2f);
            rug.transform.localPosition = new Vector3(0f, .012f, 1.1f);
            rug.transform.localRotation = Quaternion.identity;
            SetRendererMaterials(rug, AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Game/Cabin/Greybox/Materials/CabinRug.mat"));

            GameObject table = Place("Table", "Table_Visual", group);
            table.transform.localPosition = new Vector3(-3.25f, 0f, 2.05f);
            table.transform.localRotation = Quaternion.Euler(0f, 8f, 0f);

            GameObject chairWest = Place("ChairWest", "ChairRounded_Visual", group);
            chairWest.transform.localPosition = new Vector3(-4.55f, 0f, 1.9f);
            chairWest.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            GameObject chairSouth = Place("ChairSouth", "ChairRounded_Visual", group);
            chairSouth.transform.localPosition = new Vector3(-3.1f, 0f, .85f);
            chairSouth.transform.localRotation = Quaternion.Euler(0f, 6f, 0f);

            GameObject bench = Place("Bench", "Bench_Visual", group);
            bench.transform.localPosition = new Vector3(-5.45f, 0f, -5.18f);
            bench.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            GameObject cabinet = Place("KitchenCabinet", "KitchenCabinet_Visual", group);
            cabinet.transform.localPosition = new Vector3(-5.7f, 0f, 5.42f);
            cabinet.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

            GameObject books = Place("Books", "Books_Visual", group);
            books.transform.localPosition = new Vector3(-3.15f, .76f, 2.1f);
            books.transform.localRotation = Quaternion.Euler(0f, 24f, 0f);

            GameObject lamp = Place("TableLamp", "TableLamp_Visual", group);
            lamp.transform.localPosition = new Vector3(-3.75f, .76f, 1.85f);
            lamp.transform.localRotation = Quaternion.Euler(0f, -35f, 0f);

            GameObject lantern = Place("Lantern", "Lantern_Visual", group);
            Bounds lanternBounds = Measure(lantern);
            float lanternScale = lanternBounds.size.y > .01f ? 1.05f / lanternBounds.size.y : 1f;
            lantern.transform.localScale = Vector3.one * lanternScale;
            lantern.transform.localPosition = new Vector3(-1.82f, 0f, 4.92f);
            lantern.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
        }

        private static void BuildWinter(Transform presentation, ShellMetrics metrics)
        {
            Transform group = CreateGroup("Winter", presentation);
            Material snow = LoadMaterial("CabinSnow");
            CreateBox("SouthSnowGround", group,
                new Vector3(metrics.WindowCenterX, -.16f, metrics.South - 3.2f),
                new Vector3(13f, .32f, 6.4f), snow);
            CreateBox("NorthWinterBackdrop", group,
                new Vector3(0f, 2f, metrics.North + 7f),
                new Vector3(18f, 4.4f, .4f), snow);

            PlaceTree(group, "SouthTreeLeft", new Vector3(-.9f, 0f, metrics.South - 1.6f), 1f, 18f);
            PlaceTree(group, "SouthTreeRight", new Vector3(1.9f, 0f, metrics.South - 1.95f), .82f, -22f);
            PlaceTree(group, "SouthTreeFar", new Vector3(5.1f, 0f, metrics.South - 2.25f), 1.2f, 7f);
            PlaceTree(group, "NorthTreeLeft", new Vector3(-2.6f, 0f, metrics.North + 2.65f), 1f, -14f);
            PlaceTree(group, "NorthTreeRight", new Vector3(1.85f, 0f, metrics.North + 2.55f), .9f, 21f);
            PlaceTree(group, "NorthTreeFar", new Vector3(4.7f, 0f, metrics.North + 3.5f), 1.2f, -8f);
        }

        private static void PlaceTree(Transform group, string name, Vector3 position, float scale, float yaw)
        {
            GameObject tree = Place(name, "SnowTree_Visual", group);
            tree.transform.localPosition = position;
            tree.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            tree.transform.localScale = Vector3.one * scale;
        }

        private static void BuildLighting(Transform presentation, ShellMetrics metrics)
        {
            Transform group = CreateGroup("Lights", presentation);
            CreateLight(group, "FireplaceGlow", new Vector3(metrics.West + 1.05f, .92f, .05f),
                new Color(1f, .43f, .16f), 3.4f, 7.2f, LightShadows.Soft, "fireplace");
            CreateLight(group, "TableLampGlow", new Vector3(-3.75f, 1.2f, 1.85f),
                new Color(1f, .70f, .42f), 1.7f, 4.2f, LightShadows.None, "table lamp");
            CreateLight(group, "LanternGlow", new Vector3(-1.82f, .92f, 4.92f),
                new Color(1f, .61f, .30f), 1.9f, 5f, LightShadows.None, "door lantern");
            CreateLight(group, "WindowColdFill", new Vector3(metrics.WindowCenterX, 2.25f, metrics.South - 1.15f),
                new Color(.62f, .76f, 1f), 1.35f, 6f, LightShadows.None, "window snow fill");
            CreateLight(group, "NorthSnowFill", new Vector3(0f, 2.1f, metrics.North + 2.2f),
                new Color(.58f, .72f, 1f), 1.25f, 7f, LightShadows.None, "doorway snow fill");
            CreateLight(group, "WorkbenchTaskLight", new Vector3(metrics.East - 1.1f, 2.35f, -.15f),
                new Color(1f, .76f, .52f), 1.4f, 4.6f, LightShadows.None, "workbench task light");
            CreateLight(group, "RoomWarmFill", new Vector3(.5f, 2.95f, 2.2f),
                new Color(1f, .82f, .65f), 1.15f, 9f, LightShadows.None, "room warm fill");
            CreateLight(group, "SouthWarmFill", new Vector3(1.8f, 2.6f, -3.4f),
                new Color(1f, .78f, .58f), .8f, 7f, LightShadows.None, "south side fill");
        }

        private static void CreateLight(Transform parent, string name, Vector3 position, Color color,
            float intensity, float range, LightShadows shadows, string purpose)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = shadows;
            light.shadowStrength = shadows == LightShadows.None ? 0f : .72f;
            light.renderMode = LightRenderMode.ForcePixel;
            light.name = name + " (" + purpose + ")";
        }

        private static void Validate(Transform root, Transform presentation, bool logOnly)
        {
            Transform visuals = root.Find(GreyboxName + "/" + VisualsName);
            Transform colliders = root.Find(GreyboxName + "/" + CollidersName);
            Require(visuals != null, "Missing Greybox/Visuals.");
            Require(colliders != null, "Missing Greybox/Colliders.");
            Require(visuals.GetComponentsInChildren<Collider>(true).Length == 0,
                "Greybox visuals must stay collider-free.");
            Require(colliders.GetComponentsInChildren<Renderer>(true).Length == 0,
                "Greybox colliders must stay renderer-free.");
            Require(presentation.GetComponentsInChildren<Collider>(true).Length == 0,
                "Dressing must not add colliders.");
            Require(presentation.GetComponentsInChildren<MonoBehaviour>(true).Length == 0,
                "Dressing must not add scripts.");

            Transform spawnRoot = root.Find("SpawnAnchors");
            Require(spawnRoot != null && spawnRoot.childCount == 4, "Spawn anchor count must stay four.");
            for (int i = 0; i < 4; i++)
                Require(spawnRoot.GetChild(i).name == "PlayerSpawn" + (i + 1),
                    "Spawn anchor order changed.");

            GameObject integration = root.Find("CabinIntegration")?.gameObject;
            Require(integration != null, "Missing CabinIntegration.");
            object anchors = integration.GetComponent("CabinAnchors");
            Require(anchors != null, "Missing CabinAnchors.");
            var anchorType = anchors.GetType();
            Require(anchorType.GetProperty("PlayerSpawnAnchors")?.GetValue(anchors) is Transform[] wired && wired.Length == 4,
                "CabinAnchors spawn wiring changed.");
            Require(anchorType.GetProperty("MissionStartAnchor")?.GetValue(anchors) != null,
                "CabinAnchors mission anchor missing.");
            Require(anchorType.GetProperty("DepartureDirection")?.GetValue(anchors) != null,
                "CabinAnchors departure anchor missing.");
            Require(anchorType.GetProperty("PromptAnchor")?.GetValue(anchors) != null,
                "CabinAnchors prompt anchor missing.");
            Require(anchorType.GetProperty("PromptTrigger")?.GetValue(anchors) != null,
                "CabinAnchors prompt trigger missing.");

            Renderer[] renderers = presentation.GetComponentsInChildren<Renderer>(true);
            var materials = new HashSet<Material>();
            var vertices = 0;
            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    Require(material != null, "Dressing renderer " + renderer.name + " has a missing material.");
                    Require(material.shader != null && material.shader.isSupported,
                        "Dressing material " + material.name + " has an unsupported shader.");
                    Require(material.shader.name != "Hidden/InternalErrorShader",
                        "Dressing material " + material.name + " would render pink.");
                    materials.Add(material);
                }
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null) vertices += filter.sharedMesh.vertexCount;
            }

            Light[] lights = presentation.GetComponentsInChildren<Light>(true);
            Require(lights.Length >= 3, "Dressing needs warm practical lights.");
            foreach (Light light in lights)
            {
                Require(light.type == LightType.Point, "Dressing lights must stay point lights.");
                Require(!light.GetComponent<Animator>() && !light.GetComponent<Animation>(),
                    "Dressing lights must stay steady and flicker-free.");
            }

            string summary = "CABIN_DRESSING_VALIDATE_PASS renderers=" + renderers.Length +
                             " materials=" + materials.Count +
                             " vertices=" + vertices +
                             " lights=" + lights.Length +
                             " shadows=" + lights.Count(light => light.shadows != LightShadows.None) +
                             " colliders=0 scripts=0 anchors=4";
            if (logOnly) Debug.Log(summary);
            else WriteBudget(renderers.Length, materials.Count, vertices, lights.Length);
        }

        private static void WriteBudget(int renderers, int materials, int vertices, int lights)
        {
            Directory.CreateDirectory(LogFolder);
            string path = Path.Combine(LogFolder, "CabinDressingBudget.txt");
            File.WriteAllText(path,
                "CozyCabin dressing budget\n" +
                "renderers=" + renderers + "\n" +
                "uniqueMaterials=" + materials + "\n" +
                "vertices=" + vertices + "\n" +
                "lights=" + lights + "\n" +
                "colliders=0\n");
            Debug.Log("CABIN_DRESSING_BUDGET path=" + Path.GetFullPath(path) +
                      " renderers=" + renderers + " materials=" + materials +
                      " vertices=" + vertices + " lights=" + lights);
        }

        private static ShellMetrics ReadShellMetrics(Transform colliders)
        {
            BoxCollider west = FindBox(colliders, "Shell/WestWall");
            BoxCollider east = FindBox(colliders, "Shell/EastWall");
            BoxCollider south = FindBox(colliders, "Shell/SouthWallLeft");
            BoxCollider north = FindBox(colliders, "Shell/NorthWallLeft");
            BoxCollider northRight = FindBox(colliders, "Shell/NorthWallRight");
            BoxCollider ceiling = FindBox(colliders, "Shell/Ceiling");
            BoxCollider windowBelow = FindBox(colliders, "Shell/SouthWallBelowWindow");
            BoxCollider windowAbove = FindBox(colliders, "Shell/SouthWallAboveWindow");
            BoxCollider southRight = FindBox(colliders, "Shell/SouthWallRight");
            Require(west != null && east != null && south != null && north != null && northRight != null &&
                    ceiling != null && windowBelow != null && windowAbove != null && southRight != null,
                "Cannot read cabin shell collider metrics.");

            Vector3 westCenter = BoxPosition(west);
            Vector3 eastCenter = BoxPosition(east);
            Vector3 southCenter = BoxPosition(south);
            Vector3 northCenter = BoxPosition(north);
            Vector3 northRightCenter = BoxPosition(northRight);
            Vector3 ceilingCenter = BoxPosition(ceiling);
            Vector3 windowBelowCenter = BoxPosition(windowBelow);
            Vector3 windowAboveCenter = BoxPosition(windowAbove);
            Vector3 southRightCenter = BoxPosition(southRight);
            float windowLeft = southCenter.x + south.size.x * .5f;
            float windowRight = southRightCenter.x - southRight.size.x * .5f;
            var metrics = new ShellMetrics
            {
                West = westCenter.x + west.size.x * .5f,
                East = eastCenter.x - east.size.x * .5f,
                South = southCenter.z + south.size.z * .5f,
                North = northCenter.z - north.size.z * .5f,
                Ceiling = ceilingCenter.y - ceiling.size.y * .5f,
                DoorWidth = northRightCenter.x - northRight.size.x * .5f - (northCenter.x + north.size.x * .5f),
                DoorHeight = 2.6f,
                WindowCenterX = (windowLeft + windowRight) * .5f,
                WindowWidth = windowRight - windowLeft,
                WindowBottom = windowBelowCenter.y + windowBelow.size.y * .5f,
                WindowHeight = (windowAboveCenter.y - windowAbove.size.y * .5f) - (windowBelowCenter.y + windowBelow.size.y * .5f)
            };
            Require(metrics.DoorWidth > 2.4f && metrics.DoorWidth < 2.8f, "Door width does not match the shell.");
            Require(Mathf.Abs(metrics.WindowCenterX - 3f) < .01f, "Window center does not match the shell.");
            Debug.Log("CABIN_DRESSING_SHELL west=" + metrics.West + " east=" + metrics.East +
                      " south=" + metrics.South + " north=" + metrics.North +
                      " ceiling=" + metrics.Ceiling + " door=" + metrics.DoorWidth +
                      " window=" + metrics.WindowWidth + "x" + metrics.WindowHeight);
            return metrics;
        }

        private static Vector3 BoxPosition(BoxCollider box)
        {
            return box.transform.localPosition + box.center;
        }

        private static BoxCollider FindBox(Transform colliders, string path)
        {
            foreach (Transform child in colliders.GetComponentsInChildren<Transform>(true))
                if (child.name == path) return child.GetComponent<BoxCollider>();
            return null;
        }

        private static void SetGreyboxVisuals(Transform visuals, bool showOriginal)
        {
            string[] replaced = { "Fireplace/", "Workbench/", "Rug/", "Door/", "Window/Glass" };
            foreach (Renderer renderer in visuals.GetComponentsInChildren<Renderer>(true))
            {
                bool hide = !showOriginal && replaced.Any(prefix => renderer.transform.name.StartsWith(prefix, StringComparison.Ordinal));
                renderer.enabled = !hide;
            }
        }

        private static string AnchorSignature(Transform root)
        {
            var builder = new StringBuilder();
            Transform spawnRoot = root.Find("SpawnAnchors");
            if (spawnRoot == null) return "missing";
            for (int i = 0; i < spawnRoot.childCount; i++)
            {
                Transform anchor = spawnRoot.GetChild(i);
                builder.Append(anchor.name).Append(Format(anchor.localPosition)).Append(Format(anchor.localRotation.eulerAngles)).Append(';');
            }
            Transform integration = root.Find("CabinIntegration");
            if (integration == null) return builder.Append("missing").ToString();
            foreach (string name in new[] { "MissionStartAnchor", "DepartureDirection", "HostPromptTrigger/PromptAnchor" })
            {
                Transform anchor = integration.Find(name);
                builder.Append(name).Append(anchor != null ? Format(anchor.localPosition) + Format(anchor.localRotation.eulerAngles) : "missing").Append(';');
            }
            return builder.ToString();
        }

        private static string ColliderSignature(Transform colliders)
        {
            var builder = new StringBuilder();
            foreach (BoxCollider box in colliders.GetComponentsInChildren<BoxCollider>(true)
                         .OrderBy(collider => collider.name, StringComparer.Ordinal))
            {
                builder.Append(box.name).Append(Format(BoxPosition(box))).Append(Format(box.size)).Append(';');
            }
            return builder.ToString();
        }

        private static GameObject Place(string name, string prefabName, Transform parent)
        {
            string path = PrefabFolder + "/" + prefabName + ".prefab";
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new InvalidOperationException("Missing prefab " + path + ".");
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            instance.name = name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        private static Transform CreateGroup(string name, Transform parent)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            group.transform.localPosition = Vector3.zero;
            group.transform.localRotation = Quaternion.identity;
            group.transform.localScale = Vector3.one;
            return group.transform;
        }

        private static Material LoadMaterial(string name)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/" + name + ".mat");
            if (material == null) throw new InvalidOperationException("Missing material " + name + ".");
            return material;
        }

        private static void SetRendererMaterials(GameObject go, Material material)
        {
            if (material == null) throw new InvalidOperationException("Missing renderer material override.");
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = material;
                renderer.sharedMaterials = slots;
            }
        }

        private static Bounds Measure(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void ScaleToSize(GameObject go, float width, float height, float depth)
        {
            Bounds bounds = Measure(go);
            Vector3 scale = go.transform.localScale;
            if (bounds.size.x > .001f) scale.x *= width / bounds.size.x;
            if (bounds.size.y > .001f) scale.y *= height / bounds.size.y;
            if (bounds.size.z > .001f) scale.z *= depth / bounds.size.z;
            go.transform.localScale = scale;
        }

        private static void Capture(Transform root, string fileName, Vector3 localPosition, Vector3 localTarget, float fieldOfView)
        {
            string directory = Path.GetFullPath(LogFolder);
            Directory.CreateDirectory(directory);
            GameObject cameraObject = new GameObject("Cabin Dressing Preview Camera");
            cameraObject.transform.SetParent(root, false);
            cameraObject.transform.localPosition = localPosition;
            cameraObject.transform.localRotation = Quaternion.LookRotation(localTarget - localPosition, Vector3.up);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.04f, .06f, .09f);
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 80f;
            camera.allowHDR = true;

            var target = new RenderTexture(1280, 720, 24);
            camera.targetTexture = target;
            bool previousBatching = GraphicsSettings.useScriptableRenderPipelineBatching;
            try
            {
                GraphicsSettings.useScriptableRenderPipelineBatching = false;
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            }
            finally
            {
                GraphicsSettings.useScriptableRenderPipelineBatching = previousBatching;
            }
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(directory, fileName), texture.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            Debug.Log("CABIN_DRESSING_CAPTURE file=" + Path.Combine(directory, fileName));
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("CABIN_DRESSING_VALIDATION_FAIL: " + message);
        }

        private static string Format(Vector3 value)
        {
            return "(" + value.x.ToString("0.###") + "," + value.y.ToString("0.###") + "," + value.z.ToString("0.###") + ")";
        }
    }
}
