using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class CozyArtBuilder
{
    const string Root = "Assets/Game/Art/";
    [MenuItem("Tools/Cozy Art/Build Prototype Assets")]
    public static void Build()
    {
        EnsureFolders();
        var pine = Mat("PineGreen", new Color(.12f,.39f,.29f), .12f);
        var coat = Mat("ElfCoatBerry", new Color(.72f,.17f,.25f), .08f);
        var skin = Mat("WarmSkin", new Color(1f,.72f,.52f), .02f);
        var cream = Mat("SoftCream", new Color(1f,.91f,.72f), .04f);
        var gold = Mat("WarmGold", new Color(1f,.63f,.16f), .25f);
        var gift = Mat("GiftCranberry", new Color(.75f,.11f,.19f), .08f);
        var snow = Mat("SnowBlue", new Color(.72f,.87f,.91f), .04f);
        var teal = Mat("DeliveryTeal", new Color(.08f,.48f,.45f), .12f);
        SaveElf(pine, coat, skin, cream, gold);
        SaveGift(gift, cream, gold);
        SaveTarget(teal, snow, gold);
        BuildScene(pine, coat, skin, cream, gold, gift, snow, teal);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("Cozy art assets built.");
    }
    static void EnsureFolders() { foreach (var f in new[]{"Assets/Game/Art/Materials","Assets/Game/Art/Prefabs","Assets/Game/Art/Scenes","Assets/Game/Art/Editor"}) if(!AssetDatabase.IsValidFolder(f)) { var p=f.Substring(0,f.LastIndexOf('/')); AssetDatabase.CreateFolder(p,f.Substring(p.Length+1)); } }
    static Material Mat(string n, Color c, float smooth) { string p=Root+"Materials/"+n+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(p); if(!m) { m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,p); } m.name=n; m.SetColor("_BaseColor",c); m.SetFloat("_Smoothness",smooth); return m; }
    static GameObject Group(string n, Transform parent, Vector3 pos) { var g=new GameObject(n); g.transform.SetParent(parent,false); g.transform.localPosition=pos; return g; }
    static GameObject Shape(string n, PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m) { var g=GameObject.CreatePrimitive(t); g.name=n; Object.DestroyImmediate(g.GetComponent<Collider>()); g.transform.SetParent(parent,false); g.transform.localPosition=pos; g.transform.localScale=scale; g.GetComponent<Renderer>().sharedMaterial=m; return g; }
    static void SavePrefab(GameObject g,string name) { PrefabUtility.SaveAsPrefabAsset(g,Root+"Prefabs/"+name+".prefab"); Object.DestroyImmediate(g); }
    static void SaveElf(Material pine,Material coat,Material skin,Material cream,Material gold) { var root=new GameObject("CozyElf_Visual"); Shape("Soft tunic",PrimitiveType.Sphere,root.transform,new Vector3(0,.78f,0),new Vector3(.62f,.72f,.42f),coat); Shape("Round head",PrimitiveType.Sphere,root.transform,new Vector3(0,1.48f,0),new Vector3(.55f,.55f,.52f),skin); Shape("Hat brim",PrimitiveType.Sphere,root.transform,new Vector3(0,1.78f,0),new Vector3(.64f,.17f,.57f),cream); var hat=Shape("Drooping green cap",PrimitiveType.Sphere,root.transform,new Vector3(0,2.03f,.02f),new Vector3(.38f,.56f,.38f),pine); hat.transform.localRotation=Quaternion.Euler(0,0,-14); Shape("Hat pom",PrimitiveType.Sphere,root.transform,new Vector3(-.12f,2.29f,.02f),new Vector3(.21f,.21f,.21f),gold);
        Shape("Left ear",PrimitiveType.Sphere,root.transform,new Vector3(-.34f,1.46f,0),new Vector3(.27f,.14f,.17f),skin); Shape("Right ear",PrimitiveType.Sphere,root.transform,new Vector3(.34f,1.46f,0),new Vector3(.27f,.14f,.17f),skin);
        Shape("Left eye",PrimitiveType.Sphere,root.transform,new Vector3(-.12f,1.52f,-.244f),new Vector3(.055f,.075f,.035f),pine); Shape("Right eye",PrimitiveType.Sphere,root.transform,new Vector3(.12f,1.52f,-.244f),new Vector3(.055f,.075f,.035f),pine); Shape("Nose",PrimitiveType.Sphere,root.transform,new Vector3(0,1.42f,-.27f),new Vector3(.09f,.075f,.07f),skin);
        Shape("Scarf",PrimitiveType.Sphere,root.transform,new Vector3(0,1.12f,0),new Vector3(.67f,.17f,.46f),gold); Shape("Scarf tail",PrimitiveType.Sphere,root.transform,new Vector3(.2f,.96f,-.2f),new Vector3(.16f,.4f,.12f),gold);
        Shape("Left boot",PrimitiveType.Sphere,root.transform,new Vector3(-.19f,.17f,-.03f),new Vector3(.31f,.3f,.42f),pine); Shape("Right boot",PrimitiveType.Sphere,root.transform,new Vector3(.19f,.17f,-.03f),new Vector3(.31f,.3f,.42f),pine);
        SavePrefab(root,"CozyElf_Visual"); }
    static void SaveGift(Material red,Material cream,Material gold) { var root=new GameObject("Gift_Cranberry_Visual"); Shape("Rounded gift",PrimitiveType.Cube,root.transform,new Vector3(0,.35f,0),new Vector3(.78f,.68f,.68f),red); Shape("Ribbon vertical",PrimitiveType.Cube,root.transform,new Vector3(0,.35f,-.004f),new Vector3(.13f,.69f,.70f),cream); Shape("Ribbon horizontal",PrimitiveType.Cube,root.transform,new Vector3(0,.35f,0),new Vector3(.79f,.13f,.70f),gold); Shape("Bow left",PrimitiveType.Sphere,root.transform,new Vector3(-.12f,.76f,0),new Vector3(.24f,.12f,.16f),gold); Shape("Bow right",PrimitiveType.Sphere,root.transform,new Vector3(.12f,.76f,0),new Vector3(.24f,.12f,.16f),gold); SavePrefab(root,"Gift_Cranberry_Visual"); }
    static void SaveTarget(Material teal,Material snow,Material gold) { var root=new GameObject("DeliveryTarget_Visual"); Shape("Soft platform",PrimitiveType.Cylinder,root.transform,new Vector3(0,.1f,0),new Vector3(1.25f,.18f,1.05f),teal); Shape("Back post",PrimitiveType.Cylinder,root.transform,new Vector3(-.72f,1.05f,0),new Vector3(.12f,1.95f,.12f),snow); Shape("Front post",PrimitiveType.Cylinder,root.transform,new Vector3(.72f,1.05f,0),new Vector3(.12f,1.95f,.12f),snow); Shape("Top lintel",PrimitiveType.Cube,root.transform,new Vector3(0,2.02f,0),new Vector3(1.65f,.2f,.2f),gold); Shape("Delivery marker",PrimitiveType.Sphere,root.transform,new Vector3(0,1.38f,-.17f),new Vector3(.42f,.42f,.18f),teal); SavePrefab(root,"DeliveryTarget_Visual"); }
    static GameObject Instance(string path,Vector3 pos) { var p=AssetDatabase.LoadAssetAtPath<GameObject>(path); return (GameObject)PrefabUtility.InstantiatePrefab(p); }
    static void BuildScene(Material pine,Material coat,Material skin,Material cream,Material gold,Material gift,Material snow,Material teal) { var s=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); RenderSettings.ambientLight=new Color(.58f,.65f,.73f); var cam=new GameObject("ArtTest Camera").AddComponent<Camera>(); cam.transform.position=new Vector3(0,3.5f,-9); cam.transform.rotation=Quaternion.Euler(14,0,0); cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.12f,.2f,.29f); cam.fieldOfView=42;
        var light=new GameObject("Soft Key Light").AddComponent<Light>(); light.type=LightType.Directional; light.intensity=1.25f; light.transform.rotation=Quaternion.Euler(45,-25,0);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name="Snow display floor"; floor.transform.position=new Vector3(0,-.18f,0); floor.transform.localScale=new Vector3(11,.25f,5); floor.GetComponent<Renderer>().sharedMaterial=snow;
        Place("elf",Root+"Prefabs/CozyElf_Visual.prefab",new Vector3(-3,0,0)); Place("gift",Root+"Prefabs/Gift_Cranberry_Visual.prefab",new Vector3(0,0,0)); Place("target",Root+"Prefabs/DeliveryTarget_Visual.prefab",new Vector3(3,0,0));
        Label("PLAYER",new Vector3(-3,2.9f,-.2f),pine); Label("GIFT",new Vector3(0,1.35f,-.2f),gift); Label("DELIVERY",new Vector3(3,2.7f,-.2f),teal);
        EditorSceneManager.SaveScene(s,Root+"Scenes/CozyArtTest.unity"); }
    static void Place(string n,string p,Vector3 pos) { var g=Instance(p,pos); g.name=n; g.transform.position=pos; }
    static void Label(string t,Vector3 p,Material m) { var g=new GameObject(t+" label"); g.transform.position=p; g.transform.rotation=Quaternion.Euler(0,0,0); var tm=g.AddComponent<TextMesh>(); tm.text=t; tm.fontSize=64; tm.characterSize=.065f; tm.anchor=TextAnchor.MiddleCenter; tm.alignment=TextAlignment.Center; tm.color=m.color; }
}
