using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class CabinKitBuilder
{
    const string Root = "Assets/Game/Art/CabinKit";
    const string Sources = Root + "/Sources";
    const string Holiday = Sources + "/KenneyHolidayKit";
    const string Furniture = Sources + "/KenneyFurnitureKit";
    const string Materials = Root + "/Materials";
    const string Prefabs = Root + "/Prefabs";
    const string Scenes = Root + "/Scenes";
    const float TableHeightMeters = 0.75f;

    class WrapperInfo
    {
        public string Name;
        public string Source;
        public string PrefabPath;
    }

    [MenuItem("Tools/Cabin Kit/Build Wrappers and Showcase")]
    public static void Build()
    {
        EnsureFolders();

        var timber = CreateMaterial("CabinTimber", new Color(0.46f, 0.28f, 0.16f), 0.12f, 0f);
        var stone = CreateMaterial("CabinStone", new Color(0.47f, 0.46f, 0.44f), 0.08f, 0f);
        var metal = CreateMaterial("CabinMetal", new Color(0.20f, 0.21f, 0.22f), 0.35f, 0.65f);
        var cream = CreateMaterial("CabinCream", new Color(0.95f, 0.85f, 0.66f), 0.05f, 0f);
        var soot = CreateMaterial("CabinSoot", new Color(0.06f, 0.05f, 0.05f), 0.05f, 0f);
        var snow = CreateMaterial("CabinSnow", new Color(0.78f, 0.88f, 0.93f), 0f, 0f);
        var beforeGray = CreateMaterial("CabinBeforeGray", new Color(0.42f, 0.42f, 0.42f), 0.05f, 0f);
        var ember = CreateMaterial("CabinEmber", new Color(0.55f, 0.16f, 0.04f), 0.05f, 0f, null, new Color(1f, 0.34f, 0.08f) * 4f);

        var shellScale = ComputeScale(Holiday + "/cabin-door-rotate.fbx", 2.2f);
        var propScale = 1f;
        var furnitureScale = ComputeScale(Furniture + "/table.fbx", TableHeightMeters);
        Debug.Log($"CABINKIT SCALE shellScale={shellScale:F4} propScale={propScale:F4} furnitureScale={furnitureScale:F4} doorTarget=2.2 tableTarget={TableHeightMeters}");

        // Keep the source material slots: the imported Holiday UVs rely on their pack atlas mapping.
        var furnitureMaterials = new Dictionary<string, Material>();
        var wrappers = new List<WrapperInfo>();

        wrappers.Add(Imported("CabinFloor_Visual", Holiday + "/floor-wood.fbx", shellScale, false));
        wrappers.Add(Imported("CabinFloorSnow_Visual", Holiday + "/floor-wood-snow.fbx", shellScale, false));
        wrappers.Add(Imported("CabinWall_Visual", Holiday + "/cabin-wall.fbx", shellScale, false));
        wrappers.Add(Imported("CabinCorner_Visual", Holiday + "/cabin-corner.fbx", shellScale, false));
        wrappers.Add(Imported("CabinWallRoof_Visual", Holiday + "/cabin-wall-roof.fbx", shellScale, false));
        wrappers.Add(Imported("CabinRoofSnow_Visual", Holiday + "/cabin-roof-snow.fbx", shellScale, false));
        wrappers.Add(Imported("CabinRoofChimney_Visual", Holiday + "/cabin-roof-chimney.fbx", shellScale, false));
        wrappers.Add(Imported("CabinDoorway_Visual", Holiday + "/cabin-doorway.fbx", shellScale, false));
        wrappers.Add(Imported("CabinDoor_Visual", Holiday + "/cabin-door-rotate.fbx", shellScale, true));
        wrappers.Add(Imported("CabinWindow_Visual", Holiday + "/cabin-window-large.fbx", shellScale, false));
        wrappers.Add(Imported("Wreath_Visual", Holiday + "/wreath.fbx", propScale, false));
        wrappers.Add(Imported("Lantern_Visual", Holiday + "/lantern.fbx", propScale, false));
        wrappers.Add(Imported("ColoredLights_Visual", Holiday + "/lights-colored.fbx", propScale, false));
        wrappers.Add(Imported("SnowTree_Visual", Holiday + "/tree-decorated-snow.fbx", propScale, false));

        wrappers.Add(Imported("Table_Visual", Furniture + "/table.fbx", furnitureScale, false, furnitureMaterials));
        wrappers.Add(Imported("Bench_Visual", Furniture + "/bench.fbx", furnitureScale, false, furnitureMaterials));
        wrappers.Add(Imported("ChairRounded_Visual", Furniture + "/chairRounded.fbx", furnitureScale, false, furnitureMaterials));
        wrappers.Add(Imported("KitchenCabinet_Visual", Furniture + "/kitchenCabinet.fbx", furnitureScale, false, furnitureMaterials));
        wrappers.Add(Imported("TableLamp_Visual", Furniture + "/lampSquareTable.fbx", furnitureScale, false, furnitureMaterials));
        wrappers.Add(Imported("Rug_Visual", Furniture + "/rugRectangle.fbx", furnitureScale, false, furnitureMaterials));
        wrappers.Add(Imported("Books_Visual", Furniture + "/books.fbx", furnitureScale, false, furnitureMaterials));

        wrappers.Add(NativeFireplace(stone, timber, metal, soot, ember));
        wrappers.Add(NativeWorkbench(timber, cream, metal));

        BuildShowcase(snow, beforeGray);
        MeasureAndReport(wrappers);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("CABINKIT BUILD complete wrappers=" + wrappers.Count);
    }

    static WrapperInfo Imported(string name, string source, float scale, bool hinge, Dictionary<string, Material> unlitCache = null)
    {
        var wrapper = new GameObject(name);
        wrapper.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(source);
        if (asset == null)
        {
            Debug.LogError($"CABINKIT missing source={source}");
            Object.DestroyImmediate(wrapper);
            return null;
        }

        var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        model.name = "Model";
        model.transform.SetParent(wrapper.transform, false);
        model.transform.localScale = model.transform.localScale * scale;
        if (unlitCache != null)
        {
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (var i = 0; i < slots.Length; i++)
                    slots[i] = CreateFurnitureUnlitMaterial(slots[i], unlitCache);
                renderer.sharedMaterials = slots;
            }
        }

        NormalizePivot(wrapper, model.transform);
        if (hinge) AddHinge(wrapper, model.transform);

        var path = Prefabs + "/" + name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(wrapper, path);
        LogWrapper(name, source, scale, wrapper);
        Object.DestroyImmediate(wrapper);
        return new WrapperInfo { Name = name, Source = source, PrefabPath = path };
    }

    static Material CreateFurnitureUnlitMaterial(Material source, Dictionary<string, Material> cache)
    {
        if (source == null) return null;
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out var guid, out long localId);
        var key = guid + "_" + localId;
        if (cache.TryGetValue(key, out var cached)) return cached;

        var path = Materials + "/FurnitureUnlit_" + Sanitize(source.name) + "_" + localId + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = Shader.Find("Universal Render Pipeline/Unlit");
        material.name = "FurnitureUnlit_" + source.name;
        var color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor")
            : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
        var texture = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source.mainTexture;
        material.SetColor("_BaseColor", color);
        material.SetTexture("_BaseMap", texture);
        if (texture != null)
        {
            material.SetTextureScale("_BaseMap", source.GetTextureScale(source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex"));
            material.SetTextureOffset("_BaseMap", source.GetTextureOffset(source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex"));
        }
        EditorUtility.SetDirty(material);
        cache[key] = material;
        return material;
    }

    static WrapperInfo NativeWrapper(string name, System.Action<Transform> build)
    {
        var wrapper = new GameObject(name);
        var model = new GameObject("Model");
        model.transform.SetParent(wrapper.transform, false);
        build(model.transform);
        NormalizePivot(wrapper, model.transform);
        var path = Prefabs + "/" + name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(wrapper, path);
        LogWrapper(name, "authored", 1f, wrapper);
        Object.DestroyImmediate(wrapper);
        return new WrapperInfo { Name = name, Source = "authored", PrefabPath = path };
    }

    static WrapperInfo NativeFireplace(Material stone, Material timber, Material metal, Material soot, Material ember)
    {
        return NativeWrapper("Fireplace_Visual", root =>
        {
            Shape("Stone hearth", PrimitiveType.Cube, root, new Vector3(0f, 0.10f, 0f), new Vector3(2.30f, 0.20f, 1.10f), stone);
            Shape("Left pier", PrimitiveType.Cube, root, new Vector3(-0.85f, 1.00f, 0f), new Vector3(0.45f, 2.00f, 0.85f), stone);
            Shape("Right pier", PrimitiveType.Cube, root, new Vector3(0.85f, 1.00f, 0f), new Vector3(0.45f, 2.00f, 0.85f), stone);
            Shape("Back panel", PrimitiveType.Cube, root, new Vector3(0f, 1.05f, 0.28f), new Vector3(1.35f, 1.90f, 0.30f), stone);
            Shape("Firebox", PrimitiveType.Cube, root, new Vector3(0f, 0.70f, -0.18f), new Vector3(1.10f, 1.10f, 0.25f), soot);
            Shape("Timber mantel", PrimitiveType.Cube, root, new Vector3(0f, 2.08f, -0.02f), new Vector3(2.55f, 0.20f, 1.05f), timber);
            Shape("Chimney breast", PrimitiveType.Cube, root, new Vector3(0f, 2.75f, 0.20f), new Vector3(1.15f, 1.30f, 0.70f), stone);
            Shape("Chimney cap", PrimitiveType.Cube, root, new Vector3(0f, 3.48f, 0.20f), new Vector3(1.35f, 0.12f, 0.85f), stone);
            Shape("Ember bed", PrimitiveType.Cube, root, new Vector3(0f, 0.16f, -0.20f), new Vector3(1.05f, 0.10f, 0.35f), ember);
            var log1 = Shape("Log 1", PrimitiveType.Cylinder, root, new Vector3(-0.20f, 0.28f, -0.10f), new Vector3(0.18f, 0.60f, 0.18f), timber);
            log1.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var log2 = Shape("Log 2", PrimitiveType.Cylinder, root, new Vector3(0.20f, 0.32f, -0.10f), new Vector3(0.16f, 0.55f, 0.16f), timber);
            log2.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Shape("Flame left", PrimitiveType.Sphere, root, new Vector3(-0.28f, 0.48f, -0.22f), new Vector3(0.30f, 0.62f, 0.20f), ember);
            Shape("Flame center", PrimitiveType.Sphere, root, new Vector3(0f, 0.55f, -0.22f), new Vector3(0.38f, 0.78f, 0.22f), ember);
            Shape("Flame right", PrimitiveType.Sphere, root, new Vector3(0.28f, 0.44f, -0.22f), new Vector3(0.26f, 0.56f, 0.20f), ember);
            Shape("Fire screen", PrimitiveType.Cube, root, new Vector3(0f, 0.56f, -0.34f), new Vector3(1.12f, 0.90f, 0.03f), metal);
        });
    }

    static WrapperInfo NativeWorkbench(Material timber, Material cream, Material metal)
    {
        return NativeWrapper("Workbench_Visual", root =>
        {
            Shape("Worktop", PrimitiveType.Cube, root, new Vector3(0f, 0.98f, 0f), new Vector3(2.30f, 0.12f, 0.85f), timber);
            for (var i = 0; i < 4; i++)
            {
                var x = i < 2 ? -1.02f : 1.02f;
                var z = i % 2 == 0 ? -0.32f : 0.32f;
                Shape("Leg " + (i + 1), PrimitiveType.Cube, root, new Vector3(x, 0.45f, z), new Vector3(0.11f, 0.90f, 0.11f), timber);
            }
            Shape("Lower shelf", PrimitiveType.Cube, root, new Vector3(0f, 0.32f, 0f), new Vector3(2.00f, 0.08f, 0.68f), timber);
            Shape("Cabinet carcass", PrimitiveType.Cube, root, new Vector3(0.55f, 0.60f, 0.02f), new Vector3(0.95f, 0.56f, 0.72f), timber);
            Shape("Drawer upper", PrimitiveType.Cube, root, new Vector3(0.55f, 0.78f, -0.37f), new Vector3(0.80f, 0.16f, 0.03f), cream);
            Shape("Drawer lower", PrimitiveType.Cube, root, new Vector3(0.55f, 0.58f, -0.37f), new Vector3(0.80f, 0.16f, 0.03f), cream);
            Shape("Pegboard", PrimitiveType.Cube, root, new Vector3(0f, 1.55f, 0.40f), new Vector3(2.10f, 0.70f, 0.06f), timber);
            Shape("Pegboard rail", PrimitiveType.Cube, root, new Vector3(0f, 1.22f, 0.36f), new Vector3(2.10f, 0.06f, 0.06f), metal);
            Shape("Vice body", PrimitiveType.Cube, root, new Vector3(-0.82f, 1.10f, -0.05f), new Vector3(0.28f, 0.22f, 0.32f), metal);
            Shape("Vice jaw", PrimitiveType.Cube, root, new Vector3(-0.82f, 1.10f, -0.30f), new Vector3(0.24f, 0.16f, 0.10f), metal);
            var saw = Shape("Saw", PrimitiveType.Cube, root, new Vector3(0.55f, 1.55f, 0.34f), new Vector3(0.06f, 0.55f, 0.28f), metal);
            saw.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            Shape("Mallet handle", PrimitiveType.Cube, root, new Vector3(-0.35f, 1.55f, 0.34f), new Vector3(0.05f, 0.55f, 0.05f), timber);
            Shape("Mallet head", PrimitiveType.Cube, root, new Vector3(-0.35f, 1.82f, 0.34f), new Vector3(0.12f, 0.14f, 0.30f), timber);
        });
    }

    static GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    static Material CreateMaterial(string name, Color color, float smoothness, float metallic, Texture texture = null, Color? emission = null)
    {
        var path = Materials + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = Shader.Find("Universal Render Pipeline/Lit");
        material.name = name;
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);
        material.SetTexture("_BaseMap", texture);
        if (emission.HasValue)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission.Value);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
        }
        material.SetFloat("_EnvironmentReflections", 0f);
        material.SetFloat("_SpecularHighlights", 1f);
        material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
        EditorUtility.SetDirty(material);
        return material;
    }

    static void NormalizePivot(GameObject wrapper, Transform model)
    {
        var bounds = CalculateBounds(wrapper);
        var shift = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) - wrapper.transform.position;
        model.position -= shift;
    }

    static void AddHinge(GameObject wrapper, Transform model)
    {
        var bounds = CalculateBounds(wrapper);
        var hinge = new GameObject("Hinge");
        hinge.transform.SetParent(wrapper.transform, false);
        hinge.transform.localPosition = new Vector3(bounds.min.x - wrapper.transform.position.x, 0f, bounds.center.z - wrapper.transform.position.z);
        model.SetParent(hinge.transform, true);
    }

    static Bounds CalculateBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static float ComputeScale(string path, float targetHeight)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (model == null)
        {
            Debug.LogError($"CABINKIT scale source missing={path}");
            return 1f;
        }
        var height = CalculateBounds(model).size.y;
        return height > 0.001f ? targetHeight / height : 1f;
    }

    static int TriangleCount(GameObject go)
    {
        var total = 0;
        foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true))
            if (filter.sharedMesh != null) total += filter.sharedMesh.triangles.Length / 3;
        foreach (var skinned in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (skinned.sharedMesh != null) total += skinned.sharedMesh.triangles.Length / 3;
        return total;
    }

    static void LogWrapper(string name, string source, float scale, GameObject wrapper)
    {
        var bounds = CalculateBounds(wrapper);
        var scripts = wrapper.GetComponentsInChildren<MonoBehaviour>(true).Length;
        var colliders = wrapper.GetComponentsInChildren<Collider>(true).Length;
        var renderers = wrapper.GetComponentsInChildren<Renderer>(true).Length;
        Debug.Log($"CABINKIT WRAPPER name={name} source={source} scale={scale:F4} bounds={bounds.size.x:F3}x{bounds.size.y:F3}x{bounds.size.z:F3} pivotBottom={bounds.min.y:F4} triangles={TriangleCount(wrapper)} renderers={renderers} scripts={scripts} colliders={colliders}");
    }

    static void BuildShowcase(Material snow, Material beforeGray)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.42f, 0.50f, 0.60f);
        RenderSettings.ambientIntensity = 1f;

        var cameraObject = new GameObject("Showcase Camera");
        var camera = cameraObject.AddComponent<Camera>();
        cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 8.5f, -20f), Quaternion.Euler(16f, 0f, 0f));
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.09f, 0.14f, 0.20f);
        camera.fieldOfView = 60f;
        camera.farClipPlane = 120f;

        var keyObject = new GameObject("Warm Key Light");
        var key = keyObject.AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(1f, 0.84f, 0.66f);
        key.intensity = 1.35f;
        key.shadows = LightShadows.Soft;
        key.shadowStrength = 0.80f;
        keyObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Snow ground";
        Object.DestroyImmediate(ground.GetComponent<Collider>());
        ground.transform.position = new Vector3(0f, -0.06f, -2f);
        ground.transform.localScale = new Vector3(50f, 0.10f, 30f);
        ground.GetComponent<Renderer>().sharedMaterial = snow;

        const float beforeX = -8f;
        const float afterX = 8f;
        const float furnitureZ = -7.5f;
        const float rowA = 1.4f;
        const float rowB = -1.8f;
        var columns = new[] { -5.2f, -2.6f, 0f, 2.6f, 5.2f };

        PlaceRaw("Floor", Holiday + "/floor-wood.fbx", new Vector3(beforeX + columns[0], 0f, rowA));
        PlaceRaw("Wall", Holiday + "/cabin-wall.fbx", new Vector3(beforeX + columns[1], 0f, rowA));
        PlaceRaw("Roof", Holiday + "/cabin-roof-snow.fbx", new Vector3(beforeX + columns[2], 0f, rowA));
        PlaceRaw("Door", Holiday + "/cabin-door-rotate.fbx", new Vector3(beforeX + columns[3], 0f, rowA));
        PlaceBlock("Fireplace blockout", new Vector3(beforeX + columns[4], 1.70f, rowA), new Vector3(2.3f, 3.4f, 1.0f), beforeGray);
        PlaceBlock("Workbench blockout", new Vector3(beforeX + columns[0], 0.50f, rowB), new Vector3(2.3f, 1.0f, 0.9f), beforeGray);
        PlaceRaw("Wreath", Holiday + "/wreath.fbx", new Vector3(beforeX + columns[1], 0f, rowB));
        PlaceRaw("Lantern", Holiday + "/lantern.fbx", new Vector3(beforeX + columns[2], 0f, rowB));
        PlaceRaw("Lights", Holiday + "/lights-colored.fbx", new Vector3(beforeX + columns[3], 0f, rowB));
        PlaceRaw("Tree", Holiday + "/tree-decorated-snow.fbx", new Vector3(beforeX + columns[4], 0f, rowB));

        PlacePrefab("CabinFloor_Visual", new Vector3(afterX + columns[0], 0f, rowA));
        PlacePrefab("CabinWall_Visual", new Vector3(afterX + columns[1], 0f, rowA));
        PlacePrefab("CabinRoofSnow_Visual", new Vector3(afterX + columns[2], 0f, rowA));
        PlacePrefab("CabinDoor_Visual", new Vector3(afterX + columns[3], 0f, rowA));
        PlacePrefab("Fireplace_Visual", new Vector3(afterX + columns[4], 0f, rowA));
        PlacePrefab("Workbench_Visual", new Vector3(afterX + columns[0], 0f, rowB));
        PlacePrefab("Wreath_Visual", new Vector3(afterX + columns[1], 0f, rowB));
        PlacePrefab("Lantern_Visual", new Vector3(afterX + columns[2], 0f, rowB));
        PlacePrefab("ColoredLights_Visual", new Vector3(afterX + columns[3], 0f, rowB));
        PlacePrefab("SnowTree_Visual", new Vector3(afterX + columns[4], 0f, rowB));

        var furnitureXs = new[] { -7.8f, -5.2f, -2.6f, 0f, 2.6f, 5.2f, 7.8f };
        PlacePrefab("Table_Visual", new Vector3(furnitureXs[0], 0f, furnitureZ));
        PlacePrefab("Bench_Visual", new Vector3(furnitureXs[1], 0f, furnitureZ));
        PlacePrefab("ChairRounded_Visual", new Vector3(furnitureXs[2], 0f, furnitureZ));
        PlacePrefab("KitchenCabinet_Visual", new Vector3(furnitureXs[3], 0f, furnitureZ));
        PlacePrefab("TableLamp_Visual", new Vector3(furnitureXs[4], 0f, furnitureZ));
        PlacePrefab("Rug_Visual", new Vector3(furnitureXs[5], 0f, furnitureZ));
        PlacePrefab("Books_Visual", new Vector3(furnitureXs[6], 0f, furnitureZ));

        Label("BEFORE - raw Kenney sources and gray blockouts", new Vector3(beforeX, 6.6f, rowA), new Color(0.86f, 0.88f, 0.92f));
        Label("AFTER - CabinKit_Visual wrappers", new Vector3(afterX, 6.6f, rowA), new Color(1f, 0.86f, 0.62f));
        Label("Furniture wrappers", new Vector3(0f, 2.8f, furnitureZ), new Color(0.86f, 0.88f, 0.92f));
        Label("CabinKit Showcase - issue #43", new Vector3(0f, 8.6f, 3f), new Color(1f, 0.90f, 0.72f));

        EditorSceneManager.SaveScene(scene, Scenes + "/CabinKitShowcase.unity");
    }

    static void PlaceRaw(string label, string source, Vector3 position)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(source);
        if (asset == null)
        {
            Debug.LogError($"CABINKIT showcase missing raw={source}");
            return;
        }
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        instance.name = "BEFORE " + label;
        instance.transform.position = position;
    }

    static void PlaceBlock(string label, Vector3 position, Vector3 scale, Material material)
    {
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = "BEFORE " + label;
        Object.DestroyImmediate(block.GetComponent<Collider>());
        block.transform.position = position;
        block.transform.localScale = scale;
        block.GetComponent<Renderer>().sharedMaterial = material;
    }

    static void PlacePrefab(string prefabName, Vector3 position)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + prefabName + ".prefab");
        if (asset == null)
        {
            Debug.LogError($"CABINKIT showcase missing prefab={prefabName}");
            return;
        }
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        instance.name = "AFTER " + prefabName;
        instance.transform.position = position;
    }

    static void Label(string text, Vector3 position, Color color)
    {
        var go = new GameObject(text + " label");
        go.transform.position = position;
        var mesh = go.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.fontSize = 64;
        mesh.characterSize = 0.065f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = color;
    }

    static void MeasureAndReport(List<WrapperInfo> wrappers)
    {
        var lines = new List<string> { "CabinKit #43 showcase budget (measured by CabinKitBuilder)" };
        var totalTriangles = 0;
        var totalRenderers = 0;
        var materials = new HashSet<Material>();
        var textures = new HashSet<Texture>();

        foreach (var wrapper in wrappers)
        {
            if (wrapper == null) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(wrapper.PrefabPath);
            if (prefab == null) continue;
            var triangles = TriangleCount(prefab);
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            var localMaterials = new HashSet<Material>();
            foreach (var renderer in renderers)
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    localMaterials.Add(material);
                    materials.Add(material);
                    var texture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : material.mainTexture;
                    if (texture != null) textures.Add(texture);
                }
            totalTriangles += triangles;
            totalRenderers += renderers.Length;
            var line = $"{wrapper.Name}: triangles={triangles} renderers={renderers.Length} materials={localMaterials.Count} source={wrapper.Source}";
            lines.Add(line);
            Debug.Log("CABINKIT BUDGET " + line);
        }

        lines.Add($"TOTAL: triangles={totalTriangles} renderers={totalRenderers} uniqueMaterials={materials.Count} uniqueTextures={textures.Count}");
        Debug.Log($"CABINKIT BUDGET total triangles={totalTriangles} renderers={totalRenderers} uniqueMaterials={materials.Count} uniqueTextures={textures.Count}");
        File.WriteAllLines(Root + "/ShowcaseBudget.txt", lines);
        AssetDatabase.ImportAsset(Root + "/ShowcaseBudget.txt");
    }

    static string Sanitize(string value)
    {
        if (string.IsNullOrEmpty(value)) return "Default";
        var chars = value.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        return new string(chars);
    }

    public static void Validate()
    {
        var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { Prefabs });
        var ok = true;
        var validated = 0;
        var totalTriangles = 0;
        foreach (var guid in prefabGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            var scripts = prefab.GetComponentsInChildren<MonoBehaviour>(true).Length;
            var colliders = prefab.GetComponentsInChildren<Collider>(true).Length;
            var triangles = TriangleCount(prefab);
            var bounds = CalculateBounds(prefab);
            var pivotOk = Mathf.Abs(bounds.min.y) < 0.001f;
            var shaderOk = renderers.Length > 0 && renderers.All(renderer =>
                renderer.sharedMaterials.All(material => material != null && material.shader != null &&
                    (material.shader.name == "Universal Render Pipeline/Lit" || material.shader.name == "Universal Render Pipeline/Unlit")));
            var pass = renderers.Length > 0 && triangles > 0 && scripts == 0 && colliders == 0 && pivotOk && shaderOk;
            if (!pass) ok = false;
            validated++;
            totalTriangles += triangles;
            Debug.Log($"CABINKIT VALIDATE prefab={prefab.name} pass={pass} renderers={renderers.Length} triangles={triangles} scripts={scripts} colliders={colliders} pivotBottom={bounds.min.y:F4} shadersOk={shaderOk}");
        }

        var scene = EditorSceneManager.OpenScene(Scenes + "/CabinKitShowcase.unity", OpenSceneMode.Single);
        var camera = Object.FindAnyObjectByType<Camera>();
        var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var directional = lights.Count(light => light.type == LightType.Directional && light.shadows != LightShadows.None);
        var labels = Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        var ground = GameObject.Find("Snow ground");
        var sceneOk = camera != null && directional == 1 && labels >= 3 && ground != null;
        if (!sceneOk) ok = false;
        Debug.Log($"CABINKIT VALIDATE prefabs={validated} totalTriangles={totalTriangles} scene={scene.name} camera={camera != null} directionalShadowLights={directional} labels={labels} ground={ground != null} pass={sceneOk} overall={ok}");
    }

    public static void Capture()
    {
        var scene = EditorSceneManager.OpenScene(Scenes + "/CabinKitShowcase.unity", OpenSceneMode.Single);
        var camera = Object.FindAnyObjectByType<Camera>();
        if (camera == null)
        {
            Debug.LogError("CABINKIT CAPTURE missing camera");
            return;
        }

        var target = new RenderTexture(1600, 900, 24);
        camera.targetTexture = target;
        var previousBatching = GraphicsSettings.useScriptableRenderPipelineBatching;
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
        var directory = Path.GetFullPath("Logs");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "CabinKitShowcase.png");
        File.WriteAllBytes(path, texture.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(texture);
        Debug.Log($"CABINKIT CAPTURE scene={scene.name} path={path}");
    }

    static void EnsureFolders()
    {
        foreach (var folder in new[] { Root, Sources, Holiday, Holiday + "/Textures", Furniture, Materials, Prefabs, Scenes, Root + "/Editor" })
            EnsureFolder(folder);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = path.Substring(0, path.LastIndexOf('/'));
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(parent.Length + 1));
    }
}
