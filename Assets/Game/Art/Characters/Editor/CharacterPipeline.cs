using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class CharacterPipeline
{
    private const string Root = "Assets/Game/Art/Characters/";
    private const string Source = Root + "PackProbe/KenneyAnimatedCharacters/";
    private const string ModelPath = Source + "Model/characterMedium.fbx";
    private const float CharacterScale = 0.5f;

    public static void Build()
    {
        EnsureFolders();
        ConfigureImports();
        AssetDatabase.Refresh();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
        var clips = new[] { "idle", "run", "jump" }.ToDictionary(n => n, LoadClip);
        if (model == null || avatar == null || !avatar.isHuman || clips.Values.Any(c => c == null))
            throw new System.Exception("Kenney source model, Humanoid Avatar, or animation clip missing.");

        var controller = BuildController(clips);
        BuildVisual("Elf_Visual", model, avatar, controller, "skaterFemaleA", true);
        BuildVisual("Resident_Visual", model, avatar, controller, "criminalMaleA", false);
        BuildShowcase();
        AssetDatabase.SaveAssets();
        Debug.Log($"CHARPIPE built triangles={Triangles(model)} vertices={Vertices(model)} bones={model.GetComponentsInChildren<Transform>(true).Length} avatarValid={avatar.isValid} avatarHuman={avatar.isHuman} clips={string.Join(",", clips.Select(x => x.Key + ":" + x.Value.length + "s human=" + x.Value.humanMotion + " curves=" + AnimationUtility.GetCurveBindings(x.Value).Length))}");
    }

    private static void EnsureFolders()
    {
        foreach (var folder in new[] { Root + "Prefabs", Root + "Materials", Root + "Meshes", Root + "Animation", Root + "Scenes", Root + "Editor" })
            if (!AssetDatabase.IsValidFolder(folder))
            {
                var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
                var name = Path.GetFileName(folder);
                AssetDatabase.CreateFolder(parent, name);
            }
    }

    private static void ConfigureImports()
    {
        var modelImporter = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        modelImporter.globalScale = 1f;
        modelImporter.importAnimation = false;
        modelImporter.materialImportMode = ModelImporterMaterialImportMode.None;
        modelImporter.animationType = ModelImporterAnimationType.Human;
        modelImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        modelImporter.SaveAndReimport();
        foreach (var name in new[] { "idle", "run", "jump" })
        {
            var path = Source + "Animations/" + name + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale = 1f;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            var settings = importer.clipAnimations;
            if (settings.Length == 0) settings = importer.defaultClipAnimations;
            if (settings.Length > 0)
            {
                settings[0].name = name;
                settings[0].loopTime = name != "jump";
                settings[0].loopPose = name != "jump";
                settings[0].lockRootPositionXZ = true;
                settings[0].lockRootHeightY = true;
                settings[0].lockRootRotation = true;
                settings[0].heightFromFeet = true;
                importer.clipAnimations = settings;
            }
            importer.SaveAndReimport();
        }
    }

    private static AnimationClip LoadClip(string name)
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(Source + "Animations/" + name + ".fbx").OfType<AnimationClip>();
        return clips.Where(c => !c.name.StartsWith("__preview__") && c.length > .1f).OrderByDescending(c => c.length).FirstOrDefault();
    }

    private static AnimatorController BuildController(System.Collections.Generic.Dictionary<string, AnimationClip> clips)
    {
        var path = Root + "Animation/CharacterVisual.controller";
        var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (c != null) AssetDatabase.DeleteAsset(path);
        c = AnimatorController.CreateAnimatorControllerAtPath(path);
        c.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
        c.AddParameter("Action", AnimatorControllerParameterType.Trigger);
        var sm = c.layers[0].stateMachine;
        var idle = sm.AddState("Idle"); idle.motion = clips["idle"];
        var move = sm.AddState("Move"); move.motion = clips["run"];
        var jump = sm.AddState("Jump"); jump.motion = clips["jump"];
        sm.defaultState = idle;
        var im = idle.AddTransition(move); im.hasExitTime = false; im.AddCondition(AnimatorConditionMode.Greater, .1f, "MoveSpeed"); im.duration = .12f;
        var mi = move.AddTransition(idle); mi.hasExitTime = false; mi.AddCondition(AnimatorConditionMode.Less, .1f, "MoveSpeed"); mi.duration = .12f;
        var ij = sm.AddAnyStateTransition(jump); ij.hasExitTime = false; ij.AddCondition(AnimatorConditionMode.If, 0, "Action"); ij.duration = .08f;
        var ji = jump.AddTransition(idle); ji.hasExitTime = true; ji.exitTime = .95f; ji.duration = .1f;
        return c;
    }

    private static void BuildVisual(string name, GameObject model, Avatar avatar, RuntimeAnimatorController controller, string skinName, bool elf)
    {
        var skin = AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "Skins/" + skinName + ".png");
        var material = MakeMaterial(name + "_URP", skin, elf ? new Color(.20f, .56f, .30f) : new Color(.48f, .69f, .73f));
        var wrapper = new GameObject(name);
        var character = (GameObject)PrefabUtility.InstantiatePrefab(model);
        character.name = "CharacterModel";
        character.transform.SetParent(wrapper.transform, false);
        character.transform.localScale = Vector3.one * CharacterScale;
        var animator = character.GetComponent<Animator>();
        if (animator == null) animator = character.AddComponent<Animator>();
        animator.avatar = avatar; animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        var usesRootMotion = animator.applyRootMotion;
        foreach (var renderer in character.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
            renderer.updateWhenOffscreen = false;
        }
        AddWinterSilhouette(character.transform, animator, elf, skinName);
        var triangleCount = Triangles(character);
        var path = Root + "Prefabs/" + name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(wrapper, path);
        Object.DestroyImmediate(wrapper);
        Debug.Log($"CHARPIPE prefab={path} scale={CharacterScale:F3} materials={material.shader.name} applyRootMotion={usesRootMotion} tris={triangleCount}");
    }

    private static Material MakeMaterial(string name, Texture2D texture, Color fallback)
    {
        var path = Root + "Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Universal Render Pipeline/Unlit");
        m.color = Color.white; m.mainTexture = texture; m.SetColor("_BaseColor", Color.white);
        if (texture == null) m.SetColor("_BaseColor", fallback);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void AddWinterSilhouette(Transform model, Animator animator, bool elf, string suffix)
    {
        var head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null) head = model;
        var compensation = new Vector3(1f / head.lossyScale.x, 1f / head.lossyScale.y, 1f / head.lossyScale.z);
        Debug.Log($"CHARPIPE headBone={head.name} position={head.position:F2} lossyScale={head.lossyScale:F2} compensation={compensation:F4}");
        var hat = CreatePrimitive("WinterCap", PrimitiveType.Sphere, head, Vector3.Scale(new Vector3(0, .5f, 0), compensation), Vector3.Scale(new Vector3(.24f, .22f, .22f), compensation), elf ? new Color(.78f,.16f,.12f) : new Color(.18f,.46f,.48f));
        hat.GetComponent<Renderer>().sharedMaterial = MakeMaterial("WinterCap_" + suffix, null, elf ? new Color(.78f,.16f,.12f) : new Color(.18f,.46f,.48f));
        if (elf)
        {
            var earMaterial = MakeMaterial("ElfEar_Mat", null, new Color(1f,.91f,.72f));
            for (int side = -1; side <= 1; side += 2)
            {
                var ear = new GameObject("ElfEar");
                ear.transform.SetParent(head, false);
                ear.transform.localPosition = Vector3.Scale(new Vector3(side * .14f, .2f, 0), compensation);
                ear.transform.localScale = compensation;
                ear.transform.localRotation = Quaternion.Euler(0, 0, side * -8f);
                ear.AddComponent<MeshFilter>().sharedMesh = GetPointedEarMesh(side);
                ear.AddComponent<MeshRenderer>().sharedMaterial = earMaterial;
            }
        }
        var scarf = CreatePrimitive("WovenScarf", PrimitiveType.Cylinder, model, new Vector3(0, 1.32f, 0), new Vector3(.21f, .055f, .21f), elf ? new Color(.96f,.72f,.16f) : new Color(.82f,.27f,.14f));
        scarf.GetComponent<Renderer>().sharedMaterial = MakeMaterial("WovenScarf_" + suffix, null, elf ? new Color(.96f,.72f,.16f) : new Color(.82f,.27f,.14f));
    }

    private static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static Mesh GetPointedEarMesh(int side)
    {
        var path = "Assets/Game/Art/Characters/Meshes/ElfEar" + (side < 0 ? "Left" : "Right") + ".mesh";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;

        var outerSign = side < 0 ? -1f : 1f;
        var vertices = new[]
        {
            new Vector3(0, -.06f, -.025f), new Vector3(0, .06f, -.025f), new Vector3(outerSign * .30f, .015f, -.025f),
            new Vector3(0, -.06f, .025f), new Vector3(0, .06f, .025f), new Vector3(outerSign * .30f, .015f, .025f)
        };
        var front = outerSign > 0 ? new[] { 0, 2, 1 } : new[] { 0, 1, 2 };
        var back = outerSign > 0 ? new[] { 3, 4, 5 } : new[] { 3, 5, 4 };
        mesh = new Mesh { name = side < 0 ? "ElfEarLeft" : "ElfEarRight" };
        mesh.vertices = vertices;
        mesh.triangles = new[] { front[0], front[1], front[2], back[0], back[1], back[2], 0, 3, 5, 0, 5, 2, 1, 2, 5, 1, 5, 4, 0, 1, 4, 0, 4, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static void BuildShowcase()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var light = new GameObject("Warm Key Light", typeof(Light)); light.GetComponent<Light>().type = LightType.Directional; light.GetComponent<Light>().color = new Color(1f,.82f,.63f); light.transform.rotation = Quaternion.Euler(42,-32,0);
        var fill = new GameObject("Cool Fill Light", typeof(Light)); fill.GetComponent<Light>().type = LightType.Directional; fill.GetComponent<Light>().color = new Color(.48f,.67f,1f); fill.GetComponent<Light>().intensity = .5f; fill.transform.rotation = Quaternion.Euler(30,145,0);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Showcase Ground"; floor.transform.position = new Vector3(0,-.12f,0); floor.transform.localScale = new Vector3(8,.2f,5); Object.DestroyImmediate(floor.GetComponent<Collider>()); floor.GetComponent<Renderer>().sharedMaterial = MakeMaterial("ShowcaseGround_URP", null, new Color(.18f,.25f,.22f));
        var elf = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Elf_Visual.prefab")); elf.name = "Elf (Idle to Move to Action)"; elf.transform.position = new Vector3(-1.1f,0,0); elf.transform.rotation = Quaternion.Euler(0,180,0);
        var resident = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Resident_Visual.prefab")); resident.name = "Resident (Idle)"; resident.transform.position = new Vector3(1.1f,0,0); resident.transform.rotation = Quaternion.Euler(0,180,0);
        foreach (var a in new[] { elf, resident })
            foreach (var renderer in a.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.updateWhenOffscreen = true;
        var elfAnimator = elf.GetComponentInChildren<Animator>();
        elfAnimator.Play("Idle", 0, .2f);
        elfAnimator.Update(.2f);
        var driver = new GameObject("Showcase Animation Driver").AddComponent<CharacterShowcaseDriver>();
        var field = typeof(CharacterShowcaseDriver).GetField("movingAnimator", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field.SetValue(driver, elfAnimator);
        var cam = new GameObject("Showcase Camera", typeof(Camera)); cam.transform.position = new Vector3(0,1.7f,-7); cam.transform.rotation = Quaternion.identity; cam.GetComponent<Camera>().fieldOfView = 42; cam.GetComponent<Camera>().backgroundColor = new Color(.12f,.18f,.25f); cam.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
        EditorSceneManager.SaveScene(scene, Root + "Scenes/CharacterShowcase.unity");
        CapturePreview(cam.GetComponent<Camera>());
    }

    private static void CapturePreview(Camera camera)
    {
        var target = new RenderTexture(1280, 720, 24);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
        File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "slh-character-showcase.png"), image.EncodeToPNG());
        camera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        Object.DestroyImmediate(image);
        target.Release();
        Object.DestroyImmediate(target);
    }

    private static int Triangles(GameObject go) =>
        go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMesh != null).Sum(r => r.sharedMesh.triangles.Length / 3) +
        go.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).Sum(f => f.sharedMesh.triangles.Length / 3);
    private static int Vertices(GameObject go) => go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMesh != null).Sum(r => r.sharedMesh.vertexCount);
}
