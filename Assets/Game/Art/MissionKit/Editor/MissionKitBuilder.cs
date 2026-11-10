using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class MissionKitBuilder
{
    const string Root = "Assets/Game/Art/MissionKit/";
    const string Source = Root + "Source/Models/FBX format/";
    const string Materials = Root + "Materials/";
    const string Prefabs = Root + "Prefabs/";
    const string Scenes = Root + "Scenes/";
    const string AtlasPath = Source + "Textures/colormap.png";

    [MenuItem("Tools/Mission Kit/Build Mission Kit")]
    public static void Build()
    {
        EnsureFolders();
        AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
        var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        if (atlas == null) throw new InvalidOperationException("MissionKit atlas did not import: " + AtlasPath);
        if (atlas.width != 512 || atlas.height != 512)
            throw new InvalidOperationException($"MissionKit atlas must be 512x512, got {atlas.width}x{atlas.height}");

        ConfigureModel("present-a-cube");
        ConfigureModel("present-b-cube");
        ConfigureModel("sled-long");

        var goalTeal = Mat("GoalTeal", new Color(.08f, .48f, .45f));
        var goalCream = Mat("GoalCream", new Color(1f, .91f, .72f));
        var goalGold = Mat("GoalGold", new Color(1f, .63f, .16f));
        var giftLight = Mat("GiftLight_Atlas", new Color(1f, .93f, .80f), atlas);
        var giftLightAccent = Mat("GiftLight_Accent", new Color(1f, .72f, .30f));
        var giftHeavy = Mat("GiftHeavy_Atlas", new Color(.62f, .36f, .45f), atlas);
        var giftHeavyAccent = Mat("GiftHeavy_Accent", new Color(.09f, .25f, .22f));
        var sleigh = Mat("Sleigh_Atlas", new Color(.95f, .82f, .66f), atlas);
        var snow = Mat("ShowcaseSnow", new Color(.86f, .92f, .95f));

        BuildSourcePrefab("LightGift_Visual", "present-a-cube", new[] { giftLight, giftLightAccent });
        BuildSourcePrefab("HeavyGift_Visual", "present-b-cube", new[] { giftHeavy, giftHeavyAccent });
        BuildSourcePrefab("Sleigh_Visual", "sled-long", new[] { sleigh });
        BuildGoalPrefab(goalTeal, goalCream, goalGold);

        AssetDatabase.SaveAssets();
        BuildScene(snow);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("MISSIONKIT build complete.");
    }

    public static void Capture()
    {
        EditorSceneManager.OpenScene(Scenes + "MissionKitShowcase.unity", OpenSceneMode.Single);
        var camera = UnityEngine.Object.FindAnyObjectByType<Camera>();
        if (camera == null) throw new InvalidOperationException("Mission Kit showcase camera missing.");

        var target = new RenderTexture(1600, 900, 24);
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            var directory = System.IO.Path.GetFullPath("Logs");
            System.IO.Directory.CreateDirectory(directory);
            var path = System.IO.Path.Combine(directory, "MissionKitShowcase.png");
            System.IO.File.WriteAllBytes(path, pixels.EncodeToPNG());
            Debug.Log($"MISSIONKIT capture path={path}");
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(pixels);
        }
    }

    static void EnsureFolders()
    {
        foreach (var folder in new[] { Root + "Materials", Root + "Prefabs", Root + "Scenes", Root + "Editor" })
        {
            if (AssetDatabase.IsValidFolder(folder)) continue;
            var parent = folder.Substring(0, folder.LastIndexOf('/'));
            AssetDatabase.CreateFolder(parent, folder.Substring(parent.Length + 1));
        }
    }

    static void ConfigureModel(string name)
    {
        var path = Source + name + ".fbx";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Missing ModelImporter: " + path);
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.SaveAndReimport();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (model == null) throw new InvalidOperationException("Model did not load: " + path);
        Debug.Log($"MISSIONKIT source={name} scale={importer.globalScale} useFileScale={importer.useFileScale} " +
                  $"importAnimation={importer.importAnimation} " +
                  $"renderers={model.GetComponentsInChildren<Renderer>(true).Length} " +
                  $"skinned={model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length} " +
                  $"animators={model.GetComponentsInChildren<Animator>(true).Length}");
    }

    static Material Mat(string name, Color color, Texture2D map = null)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) throw new InvalidOperationException("URP Unlit shader not found.");
        var path = Materials + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.name = name;
        material.SetColor("_BaseColor", color);
        material.SetTexture("_BaseMap", map);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void BuildSourcePrefab(string prefabName, string sourceName, Material[] palette)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Source + sourceName + ".fbx");
        if (model == null) throw new InvalidOperationException("Source model missing: " + sourceName);
        var root = new GameObject(prefabName);
        var visual = UnityEngine.Object.Instantiate(model);
        visual.name = sourceName;
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.DestroyImmediate(collider);

        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        for (var i = 0; i < renderers.Length; i++)
        {
            var slots = Mathf.Max(1, renderers[i].sharedMaterials.Length);
            var materials = new Material[slots];
            for (var s = 0; s < slots; s++) materials[s] = palette[Mathf.Min(i, palette.Length - 1)];
            renderers[i].sharedMaterials = materials;
        }

        var bounds = CombinedBounds(visual);
        visual.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
        bounds = CombinedBounds(visual);
        SavePrefab(root, prefabName, bounds, visual.transform.localPosition);
    }

    static void BuildGoalPrefab(Material teal, Material cream, Material gold)
    {
        var root = new GameObject("DeliveryGoal_Visual");
        var visual = new GameObject("Goal geometry");
        visual.transform.SetParent(root.transform, false);

        Shape("Teal base", PrimitiveType.Cylinder, visual.transform, new Vector3(0, .08f, 0), new Vector3(1.15f, .08f, 1.15f), teal);
        Shape("Cream left post", PrimitiveType.Cylinder, visual.transform, new Vector3(-.68f, 1.02f, 0), new Vector3(.10f, .95f, .10f), cream);
        Shape("Cream right post", PrimitiveType.Cylinder, visual.transform, new Vector3(.68f, 1.02f, 0), new Vector3(.10f, .95f, .10f), cream);
        Shape("Gold lintel", PrimitiveType.Cube, visual.transform, new Vector3(0, 1.99f, 0), new Vector3(1.58f, .16f, .20f), gold);
        Shape("Gold left finial", PrimitiveType.Sphere, visual.transform, new Vector3(-.68f, 2.12f, 0), new Vector3(.22f, .22f, .22f), gold);
        Shape("Gold right finial", PrimitiveType.Sphere, visual.transform, new Vector3(.68f, 2.12f, 0), new Vector3(.22f, .22f, .22f), gold);
        Shape("Cream snow cap left", PrimitiveType.Sphere, visual.transform, new Vector3(-.68f, 1.94f, 0), new Vector3(.32f, .10f, .32f), cream);
        Shape("Cream snow cap right", PrimitiveType.Sphere, visual.transform, new Vector3(.68f, 1.94f, 0), new Vector3(.32f, .10f, .32f), cream);
        Shape("Teal marker", PrimitiveType.Cube, visual.transform, new Vector3(0, 1.42f, -.02f), new Vector3(.62f, .46f, .14f), teal);
        Shape("Cream marker inset", PrimitiveType.Cube, visual.transform, new Vector3(0, 1.42f, -.10f), new Vector3(.42f, .28f, .04f), cream);
        Shape("Gold marker diamond", PrimitiveType.Cube, visual.transform, new Vector3(0, 1.42f, -.14f), new Vector3(.18f, .18f, .05f), gold)
            .transform.localRotation = Quaternion.Euler(0, 0, 45);
        Shape("Gold base trim", PrimitiveType.Cylinder, visual.transform, new Vector3(0, .17f, 0), new Vector3(1.02f, .015f, 1.02f), gold);

        var bounds = CombinedBounds(root);
        SavePrefab(root, "DeliveryGoal_Visual", bounds, Vector3.zero);
    }

    static GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var shape = GameObject.CreatePrimitive(type);
        shape.name = name;
        UnityEngine.Object.DestroyImmediate(shape.GetComponent<Collider>());
        shape.transform.SetParent(parent, false);
        shape.transform.localPosition = position;
        shape.transform.localScale = scale;
        shape.GetComponent<Renderer>().sharedMaterial = material;
        return shape;
    }

    static Bounds CombinedBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.zero);
        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static void SavePrefab(GameObject root, string name, Bounds bounds, Vector3 pivotOffset)
    {
        AssertVisualOnly(root, name);
        var path = Prefabs + name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        var rendererCount = root.GetComponentsInChildren<Renderer>(true).Length;
        Debug.Log($"MISSIONKIT prefab={name} path={path} renderers={rendererCount} " +
                  $"size={bounds.size.x:F3},{bounds.size.y:F3},{bounds.size.z:F3} " +
                  $"pivotOffset={pivotOffset.x:F3},{pivotOffset.y:F3},{pivotOffset.z:F3} " +
                  $"colliders={root.GetComponentsInChildren<Collider>(true).Length} " +
                  $"rigidbodies={root.GetComponentsInChildren<Rigidbody>(true).Length} " +
                  $"monobehaviours={root.GetComponentsInChildren<MonoBehaviour>(true).Length}");
        UnityEngine.Object.DestroyImmediate(root);
    }

    static void AssertVisualOnly(GameObject root, string name)
    {
        if (root.GetComponentsInChildren<Collider>(true).Length > 0)
            throw new InvalidOperationException($"{name} contains a collider.");
        if (root.GetComponentsInChildren<Rigidbody>(true).Length > 0)
            throw new InvalidOperationException($"{name} contains a Rigidbody.");
        if (root.GetComponentsInChildren<MonoBehaviour>(true).Any(c => c != null && c.GetType().Name == "NetworkObject"))
            throw new InvalidOperationException($"{name} contains a NetworkObject.");
    }

    static void BuildScene(Material snow)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.45f, .52f, .62f);

        var cameraObject = new GameObject("Showcase Camera");
        var camera = cameraObject.AddComponent<Camera>();
        cameraObject.transform.position = new Vector3(0, 2.5f, -7.8f);
        cameraObject.transform.rotation = Quaternion.Euler(12, 0, 0);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.07f, .12f, .17f);
        camera.fieldOfView = 42;

        var key = new GameObject("Warm key light").AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(1f, .92f, .80f);
        key.intensity = 1.35f;
        key.shadows = LightShadows.Soft;
        key.transform.rotation = Quaternion.Euler(45, -25, 0);

        var fill = new GameObject("Cool fill light").AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(.55f, .72f, .92f);
        fill.intensity = .55f;
        fill.shadows = LightShadows.None;
        fill.transform.rotation = Quaternion.Euler(20, 145, 0);

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Snow display floor";
        UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
        floor.transform.position = new Vector3(0, -.10f, 0);
        floor.transform.localScale = new Vector3(14f, .20f, 7f);
        floor.GetComponent<Renderer>().sharedMaterial = snow;

        Place("LightGift", "LightGift_Visual", new Vector3(-3.5f, 0, 0), Quaternion.identity);
        Place("HeavyGift", "HeavyGift_Visual", new Vector3(-1.9f, 0, 0), Quaternion.identity);
        Place("DeliveryGoal", "DeliveryGoal_Visual", new Vector3(.55f, 0, 0), Quaternion.identity);
        Place("Sleigh", "Sleigh_Visual", new Vector3(3.25f, 0, .15f), Quaternion.Euler(0, 22, 0));

        Label("LIGHT GIFT", new Vector3(-3.5f, 1.05f, 0), new Color(1f, .93f, .80f));
        Label("HEAVY GIFT", new Vector3(-1.9f, 1.05f, 0), new Color(.62f, .36f, .45f));
        Label("DELIVERY GOAL", new Vector3(.55f, 2.55f, 0), new Color(.08f, .48f, .45f));
        Label("SLEIGH", new Vector3(3.25f, 1.45f, .15f), new Color(1f, .63f, .16f));
        Label("MISSION KIT SHOWCASE", new Vector3(0, 3.25f, .8f), new Color(1f, .91f, .72f));

        var path = Scenes + "MissionKitShowcase.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"MISSIONKIT scene={path} prefabs=4 lights=2 camera=1 colliders=0");
    }

    static void Place(string name, string prefabName, Vector3 position, Quaternion rotation)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + prefabName + ".prefab");
        if (prefab == null) throw new InvalidOperationException("Missing prefab: " + prefabName);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = name;
        instance.transform.position = position;
        instance.transform.rotation = rotation;
    }

    static void Label(string text, Vector3 position, Color color)
    {
        var label = new GameObject(text + " label");
        label.transform.position = position;
        var mesh = label.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.fontSize = 64;
        mesh.characterSize = .040f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = color;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            mesh.font = font;
            label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
    }
}
