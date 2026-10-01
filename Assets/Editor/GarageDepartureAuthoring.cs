using System;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class GarageDepartureAuthoring
{
    static Material dark, steel, amber, cream, green, red;
    [MenuItem("RogueDrive/Bunker/Author Departure Preparation")]
    public static void Author()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "GarageScene") throw new InvalidOperationException("Open GarageScene outside Play Mode.");
        if (GameObject.Find("Garage_Departure") != null) throw new InvalidOperationException("Departure already authored. Edit its saved objects.");
        dark = Mat("Departure_Ink", new Color(.035f,.055f,.06f)); steel = Mat("Departure_Steel", new Color(.19f,.25f,.25f));
        amber = Mat("Departure_Amber", new Color(.92f,.61f,.24f)); cream = Mat("Departure_Paper", new Color(.83f,.81f,.66f));
        green = Mat("Departure_Safe", new Color(.34f,.68f,.49f)); red = Mat("Departure_Storm", new Color(.65f,.22f,.15f));
        var root = new GameObject("Garage_Departure").transform;
        root.gameObject.AddComponent<GarageDepartureCheckpoint>();
        var board = new GameObject("Route_Board").transform; board.SetParent(root); board.position = new Vector3(-4,2,-4.5f);
        Box(board,"Frame",Vector3.zero,new Vector3(4.2f,2.75f,.14f),steel);
        Box(board,"Map",new Vector3(0,0,-.09f),new Vector3(4,2.55f,.05f),dark);
        Label(board,"МАРШРУТ  /  СЕКТОР 01",new Vector3(0,1.01f,-.135f),.036f,cream.color);
        Label(board,"БЛИЖАЙШЕЕ УКРЫТИЕ",new Vector3(0,.73f,-.14f),.022f,amber.color);
        // A schematic, not a promise of unexplored road geometry.
        Vector3[] points = { new Vector3(-1.55f,-.3f,-.15f), new Vector3(-.9f,-.3f,-.15f), new Vector3(-.55f,.2f,-.15f), new Vector3(.2f,.2f,-.15f), new Vector3(.6f,-.1f,-.15f),new Vector3(1.5f,-.1f,-.15f) };
        for(int i=1;i<points.Length;i++) Segment(board,"Road",points[i-1],points[i],.045f,amber);
        Box(board,"Bunker",points[0],new Vector3(.17f,.17f,.055f),cream);
        Box(board,"Safe_STO",points[5],new Vector3(.24f,.24f,.08f),green);
        Label(board,"Бункер 07",new Vector3(-1.4f,-.6f,-.17f),.032f,cream.color);
        Label(board,"СТО",new Vector3(1.5f,-.4f,-.17f),.055f,green.color);
        Label(board,"Окраина",new Vector3(-.15f,.44f,-.17f),.033f,cream.color);
        for(int i=0;i<5;i++) { var stripe=Box(board,"Storm_Front",new Vector3(-1.8f+i*.11f,.15f,-.15f),new Vector3(.045f,.85f,.018f),red);stripe.transform.localRotation=Quaternion.Euler(0,0,-18); }
        Label(board,"БУРЯ →",new Vector3(-1.5f,.65f,-.17f),.027f,red.color);
        Label(board,"ДОБЫЧА СТОИТ ВРЕМЕНИ. ДОЕЗЖАЙТЕ ДО УКРЫТИЯ.",new Vector3(0,-1.03f,-.15f),.016f,cream.color);
        for(int i=-1;i<=1;i+=2)Box(board,"Stand",new Vector3(i*1.65f,-1.5f,.1f),new Vector3(.09f,1.3f,.09f),steel);
        var collider=board.gameObject.AddComponent<BoxCollider>();collider.size=new Vector3(4.2f,2.75f,.25f);
        var preparation=board.gameObject.AddComponent<GarageDeparturePreparation>();

        var supplies=new GameObject("Supplies_Table").transform;supplies.SetParent(root);supplies.position=new Vector3(5.4f,0,-2.8f);
        Box(supplies,"Top",new Vector3(0,.85f,0),new Vector3(3.8f,.12f,1.15f),steel,true);
        foreach(float x in new[]{-1.6f,1.6f})foreach(float z in new[]{-.4f,.4f})Box(supplies,"Leg",new Vector3(x,.42f,z),new Vector3(.075f,.84f,.075f),dark);
        Box(supplies,"Supply_Sign",new Vector3(0,1.79f,.48f),new Vector3(3.7f,.57f,.08f),dark);
        foreach(float x in new[]{-1.7f,1.7f})Box(supplies,"Sign_Support",new Vector3(x,1.35f,.5f),new Vector3(.045f,.9f,.045f),steel);
        Label(supplies,"ПРИПАСЫ В ДОРОГУ",new Vector3(0,1.86f,.425f),.026f,amber.color);
        Label(supplies,"ОБЯЗАТЕЛЬНО",new Vector3(-1,1.64f,.42f),.016f,cream.color);
        Label(supplies,"НА ВЫБОР",new Vector3(.9f,1.64f,.42f),.016f,green.color);
        var source=Object.FindObjectsByType<CarPartItem>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(i=>i.ItemType==BunkerAssemblyItemType.FuelCanister && i.GetComponent<FluidContainer>()!=null);
        if(source==null)throw new InvalidOperationException("No authored fuel canister model.");
        var fuel=Object.Instantiate(source.gameObject,supplies);fuel.name="Departure_FuelReserve";
        fuel.transform.localPosition=new Vector3(-1.35f,1.17f,0);fuel.transform.localRotation=Quaternion.identity;fuel.transform.localScale=source.transform.lossyScale;
        fuel.GetComponent<CarPartItem>().Configure(BunkerAssemblyItemType.FuelCanister,"Запас бензина · 10 л");
        fuel.GetComponent<FluidContainer>().Configure(BunkerFluidType.Gasoline,20,10);
        var repair=Case(supplies,"Departure_RepairKit","Ремкомплект",new Vector3(-.48f,1.04f,0),amber,GarageItemFunction.ItemKind.RepairKit);
        Case(supplies,"Departure_Medkit","Аптечка",new Vector3(.4f,1.04f,0),green,GarageItemFunction.ItemKind.Medkit);
        var water=Object.Instantiate(fuel,supplies);water.name="Departure_WaterReserve";water.transform.localPosition=new Vector3(1.35f,1.17f,0);
        water.GetComponent<CarPartItem>().Configure(BunkerAssemblyItemType.WaterCanister,"Запас воды для радиатора · 5 л");
        water.GetComponent<FluidContainer>().Configure(BunkerFluidType.Water,20,5);
        foreach(var r in water.GetComponentsInChildren<Renderer>())r.sharedMaterial=green;
        Box(supplies,"Supply_Instructions",new Vector3(0,.59f,-.58f),new Vector3(3.5f,.35f,.035f),dark);
        Label(supplies,"БЕНЗИН + РЕМКОМПЛЕКТ → БАГАЖНИК",new Vector3(0,.67f,-.609f),.016f,cream.color);
        Label(supplies,"Ключ для ремонта — на верстаке",new Vector3(0,.52f,-.609f),.014f,amber.color);
        foreach(float x in new[]{-1.35f,-.48f,.4f,1.35f})Box(supplies,"Supply_Mat",new Vector3(x,.917f,0),new Vector3(.66f,.008f,.8f),dark);

        // Workshop wear, zoned floor paint, conduit and an extraction grille.
        for(int i=0;i<9;i++)
        {
            var scratch=Box(root,"Floor_Wear",new Vector3(-3.2f+(i%3)*.24f,.022f,-2.5f+(i/3)*.22f),new Vector3(.6f,.006f,.026f),steel);
            scratch.transform.localRotation=Quaternion.Euler(0,23+i*7,0);
        }
        foreach(float x in new[]{-6.3f,-1.6f,1.5f,5.9f})Box(root,"Preparation_Zone",new Vector3(x,.025f,-4.8f),new Vector3(.075f,.01f,2),amber);
        Box(root,"Vent_Back",new Vector3(-11.83f,3.6f,-4),new Vector3(.08f,1.1f,1.7f),dark);
        for(int i=0;i<7;i++)Box(root,"Vent_Slat",new Vector3(-11.77f,3.15f+i*.14f,-4),new Vector3(.05f,.035f,1.6f),steel);
        Box(root,"Wall_Conduit",new Vector3(-11.72f,2.55f,0),new Vector3(.06f,.06f,12),steel);
        var memo=Box(root,"Personal_Memo",new Vector3(-4.55f,.94f,3),new Vector3(.28f,.008f,.38f),cream);
        memo.transform.localRotation=Quaternion.Euler(0,18,0);
        LightAt(root,new Vector3(-4,3.9f,-5.7f),new Color(1,.8f,.54f),3,7);
        LightAt(root,new Vector3(5.4f,3.5f,-3.4f),new Color(.67f,.85f,1),2.5f,6);
        LightAt(root,new Vector3(0,4,0),new Color(1,.84f,.67f),2,7);

        preparation.briefingShots=new[]{Shot(root,"Route_Shot",new Vector3(-4,2.1f,-9.7f),board.position-Vector3.up*.2f),Shot(root,"Storm_Shot",new Vector3(-4.7f,2.1f,-9.5f),board.position+Vector3.left*.15f-Vector3.up*.2f),Shot(root,"Supplies_Shot",new Vector3(5.4f,2.45f,-6.2f),supplies.position+Vector3.up*1.15f)};
        var canvas=new GameObject("Departure_Briefing",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler)).GetComponent<Canvas>();canvas.transform.SetParent(root);canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=200;
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        Panel(canvas.transform,"Letterbox_Top",new Vector2(0, .9f),Vector2.one,new Color(.015f,.02f,.022f,.98f));
        var bottom=Panel(canvas.transform,"Letterbox_Bottom",Vector2.zero,new Vector2(1,.19f),new Color(.015f,.02f,.022f,.98f));
        preparation.subtitle=UiText(bottom.transform,"Subtitle",30,TextAnchor.MiddleCenter,new Vector2(.08f,.16f),new Vector2(.92f,.94f));
        UiText(canvas.transform,"Chapter",24,TextAnchor.MiddleLeft,new Vector2(.05f,.91f),new Vector2(.7f,.99f)).text="Бункер 07   /   Подготовка к первому выезду";
        UiText(canvas.transform,"Skip",20,TextAnchor.MiddleRight,new Vector2(.72f,.91f),new Vector2(.95f,.99f)).text="Пробел / Esc — пропустить";
        preparation.briefingCanvas=canvas;canvas.enabled=false;
        // Every physical object can travel through pockets, boxes and cargo without losing identity.
        foreach(var obj in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Where(b=>b.gameObject.scene==SceneManager.GetActiveScene() && (b is PhysicsProp || b is CarPartItem)).Select(b=>b.gameObject).Distinct())
        {
            var identity=obj.GetComponent<GarageCheckpointItem>()??obj.AddComponent<GarageCheckpointItem>();
            if(string.IsNullOrEmpty(identity.id))identity.id=Guid.NewGuid().ToString("N");
        }
        // The tutorial starts with an empty trunk; supplies must actually be loaded.
        var trunk=Object.FindFirstObjectByType<VehicleCargoTrunk>(FindObjectsInactive.Include) ?? GameObject.Find("Classic Car_9").AddComponent<VehicleCargoTrunk>();var ts=new SerializedObject(trunk);
        ts.FindProperty("grid").arraySize=15;ts.FindProperty("items").arraySize=0;ts.FindProperty("physicalItems").arraySize=0;ts.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
    }
    static GameObject Case(Transform root,string name,string title,Vector3 pos,Material mat,GarageItemFunction.ItemKind kind)
    {
        var obj=new GameObject(name);obj.transform.SetParent(root,false);obj.transform.localPosition=pos;
        Box(obj.transform,"Case",Vector3.zero,new Vector3(.55f,.23f,.35f),mat);
        Box(obj.transform,"Lid",new Vector3(0,.13f,0),new Vector3(.56f,.035f,.36f),steel);
        Box(obj.transform,"Handle",new Vector3(0,.18f,0),new Vector3(.2f,.05f,.055f),dark);
        Box(obj.transform,"Stripe",new Vector3(0,0,-.179f),new Vector3(.05f,.17f,.01f),cream);
        if(kind==GarageItemFunction.ItemKind.Medkit)Box(obj.transform,"Cross",new Vector3(0,0,-.182f),new Vector3(.17f,.05f,.012f),cream);
        obj.AddComponent<BoxCollider>().size=new Vector3(.56f,.3f,.36f);obj.AddComponent<Rigidbody>().mass=1;
        var prop=obj.AddComponent<BunkerPhysicsProp>();prop.Configure(title);prop.SetPocketSized(true);
        obj.AddComponent<GarageItemFunction>().Configure(kind);return obj;
    }
    static Material Mat(string name,Color color)
    {
        const string folder="Assets/Materials/GaragePresentation";System.IO.Directory.CreateDirectory(folder);string path=folder+"/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat==null){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);}mat.color=color;mat.SetFloat("_Glossiness",.12f);return mat;
    }
    static GameObject Box(Transform root,string name,Vector3 position,Vector3 scale,Material mat,bool collision=false)
    {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;if(!collision)Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
    static void Label(Transform root,string text,Vector3 pos,float size,Color color)
    {var label=new GameObject(text).AddComponent<TextMesh>();label.transform.SetParent(root,false);label.transform.localPosition=pos;label.text=text;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.fontSize=72;label.characterSize=size;label.color=color;}
    static void Segment(Transform root,string name,Vector3 a,Vector3 b,float width,Material mat)
    {var go=Box(root,name,(a+b)*.5f,new Vector3(Vector3.Distance(a,b),width,.02f),mat);go.transform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);}
    static Transform Shot(Transform root,string name,Vector3 pos,Vector3 target)
    {var t=new GameObject(name).transform;t.SetParent(root);t.position=pos;t.rotation=Quaternion.LookRotation(target-pos);return t;}
    static void LightAt(Transform root,Vector3 pos,Color color,float intensity,float range)
    {var light=new GameObject("Preparation_Light").AddComponent<Light>();light.transform.SetParent(root);light.transform.position=pos;light.type=LightType.Point;light.color=color;light.intensity=intensity;light.range=range;var so=new SerializedObject(Object.FindFirstObjectByType<GaragePrologueManager>());var list=so.FindProperty("mainWorkshopLights");list.InsertArrayElementAtIndex(list.arraySize);list.GetArrayElementAtIndex(list.arraySize-1).objectReferenceValue=light;so.ApplyModifiedPropertiesWithoutUndo();}
    static GameObject Panel(Transform parent,string name,Vector2 min,Vector2 max,Color color)
    {var obj=new GameObject(name,typeof(RectTransform),typeof(Image));obj.transform.SetParent(parent,false);var rect=(RectTransform)obj.transform;rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;obj.GetComponent<Image>().color=color;return obj;}
    static Text UiText(Transform parent,string name,int size,TextAnchor alignment,Vector2 min,Vector2 max)
    {var obj=new GameObject(name,typeof(RectTransform),typeof(Text));obj.transform.SetParent(parent,false);var rect=(RectTransform)obj.transform;rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;var text=obj.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=size;text.alignment=alignment;text.color=new Color(.91f,.9f,.82f);text.raycastTarget=false;return text;}
}
