using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.Linq;

public static class ImportCheck
{
    public static void Run()
    {
        foreach (var name in new[] { "present-a-cube", "reindeer" })
        {
            var path = $"Assets/Game/Art/PackProbe/KenneyHolidayKit/{name}.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Debug.Log($"PACKPROBE name={name} importer={importer?.GetType().Name} scale={importer?.globalScale} animationType={importer?.animationType} importAnimation={importer?.importAnimation} avatarSetup={importer?.avatarSetup}");
            if (model == null) { Debug.LogError($"PACKPROBE {name} failed to load"); continue; }
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            var skin = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var animator = model.GetComponentInChildren<Animator>(true);
            var triangleCount = model.GetComponentsInChildren<MeshFilter>(true).Where(x => x.sharedMesh != null).Sum(x => x.sharedMesh.triangles.Length / 3);
            var clips = AssetDatabase.LoadAllAssetsAtPath(path);
            Debug.Log($"PACKPROBE root={model.name} rootPosition={model.transform.localPosition} renderers={renderers.Length} skinned={skin.Length} triangles={triangleCount} animator={animator != null} avatar={(animator != null && animator.avatar != null ? animator.avatar.name : "none")} clips={string.Join(",", System.Array.ConvertAll(clips, x => x is AnimationClip clip ? clip.name : null).Where(x => x != null))}");
            foreach (var renderer in renderers)
            {
                var materialNames = System.Array.ConvertAll(renderer.sharedMaterials, x => x != null ? $"{x.name}:{x.shader.name}:main={x.mainTexture?.name}" : "none");
                Debug.Log($"PACKPROBE renderer={renderer.GetType().Name} name={renderer.name} localPosition={renderer.transform.localPosition} bounds={renderer.bounds} materials={string.Join(",", materialNames)}");
            }
            BuildUrpWrapper(name, model, renderers);
        }
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/PackProbe/KenneyHolidayKit/Textures/colormap.png");
        Debug.Log($"PACKPROBE externalColormapImported={(texture != null)} size={(texture != null ? texture.width + "x" + texture.height : "none")}");
        Debug.Log($"PACKPROBE activeRenderPipeline={GraphicsSettings.currentRenderPipeline?.GetType().Name}");
        AssetDatabase.SaveAssets();
    }

    private static void BuildUrpWrapper(string name, GameObject model, Renderer[] renderers)
    {
        var folder = "Assets/Game/Art/PackProbe/KenneyHolidayKit/";
        if (!AssetDatabase.IsValidFolder(folder + "Prefabs"))
            AssetDatabase.CreateFolder(folder.TrimEnd('/'), "Prefabs");
        var materialPath = folder + name + "_URP.mat";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "Textures/colormap.png");
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        var isNew = material == null;
        if (isNew)
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.name = name + "_URP";
        material.SetTexture("_BaseMap", texture);
        if (isNew)
            AssetDatabase.CreateAsset(material, materialPath);
        else
            EditorUtility.SetDirty(material);

        var wrapper = new GameObject(name + "_Visual");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        instance.transform.SetParent(wrapper.transform, false);
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterial = material;
        var prefabPath = folder + "Prefabs/" + name + "_Visual.prefab";
        PrefabUtility.SaveAsPrefabAsset(wrapper, prefabPath);
        Debug.Log($"PACKPROBE wrapper={prefabPath} material={material.shader.name} texture={material.GetTexture("_BaseMap")?.name} rendererCount={renderers.Length}");
        Object.DestroyImmediate(wrapper);
    }
}
