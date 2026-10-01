using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RogueDrive.Gameplay.Hub;
public static class VehicleServiceAuthoring
{
    public static void Apply()
    {
        if(Application.isPlaying)throw new System.InvalidOperationException("Stop Play Mode first.");
        EditorSceneManager.OpenScene("Assets/Scenes/GarageScene.unity");
        var water=GameObject.Find("Departure_WaterReserve");
        var table=water.transform.parent;
        var oil=GameObject.Find("Departure_OilReserve");
        if(oil==null)oil=Object.Instantiate(water,table);
        oil.name="Departure_OilReserve";
        oil.GetComponent<CarPartItem>().Configure(BunkerAssemblyItemType.OilCanister,"Моторное масло");
        oil.GetComponent<FluidContainer>().Configure(BunkerFluidType.EngineOil,5,3);
        oil.GetComponent<GarageCheckpointItem>().id="departure_oil";
        oil.transform.localPosition=new Vector3(1.64f,1.17f,0);
        var matPath="Assets/Art/GarageUpgrade/OilCanister.mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if(mat==null){mat=new Material(water.GetComponentInChildren<Renderer>().sharedMaterial);mat.color=new Color(.65f,.39f,.12f);AssetDatabase.CreateAsset(mat,matPath);}
        foreach(var r in oil.GetComponentsInChildren<Renderer>())r.sharedMaterial=mat;
        string[] objects={"Departure_FuelReserve","Departure_RepairKit","Departure_Medkit","Departure_WaterReserve","Departure_OilReserve"};
        string[] names={"БЕНЗИН","РЕМКОМПЛЕКТ","АПТЕЧКА","ВОДА","МАСЛО"};
        float[] xs={-1.64f,-.82f,0,.82f,1.64f};
        table.Find("Top").localScale=new Vector3(4.4f,.12f,1.15f);
        for(int i=0;i<5;i++){var t=GameObject.Find(objects[i]).transform;var p=t.localPosition;p.x=xs[i];t.localPosition=p;}
        var rail=table.Find("Supply_Label_Rail");
        if(rail!=null)rail.gameObject.SetActive(false);
        var old=table.Find("Service_Supply_Labels");if(old!=null)Object.DestroyImmediate(old.gameObject);
        var labels=new GameObject("Service_Supply_Labels").transform;labels.SetParent(table,false);
        for(int i=0;i<5;i++)
        {
            var go=new GameObject("Label_"+names[i]);go.transform.SetParent(labels,false);go.transform.localPosition=new Vector3(xs[i],.77f,-.615f);
            var label=go.AddComponent<TextMesh>();label.text=names[i];label.fontSize=72;label.characterSize=.08f;label.fontStyle=FontStyle.Bold;
            label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=new Color(.83f,.82f,.71f);
            var bounds=label.GetComponent<Renderer>().localBounds.size;go.transform.localScale=Vector3.one*Mathf.Min(.66f/Mathf.Max(.01f,bounds.x),.085f/Mathf.Max(.01f,bounds.y));
            var plate=GameObject.CreatePrimitive(PrimitiveType.Cube);plate.name="EnamelPlate";plate.transform.SetParent(labels,false);plate.transform.localPosition=new Vector3(xs[i],.77f,-.597f);plate.transform.localScale=new Vector3(.76f,.18f,.025f);
            Object.DestroyImmediate(plate.GetComponent<Collider>());
            plate.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/GarageUpgrade/Black.mat") ?? table.Find("Top").GetComponent<Renderer>().sharedMaterial;
        }
        System.IO.Directory.CreateDirectory("Assets/Resources/VehicleService");
        SaveSupply(water,"WaterCanister");SaveSupply(oil,"OilCanister");SaveSupply(GameObject.Find("Departure_FuelReserve"),"FuelCanister");
        EditorSceneManager.MarkSceneDirty(table.gameObject.scene);EditorSceneManager.SaveScene(table.gameObject.scene);AssetDatabase.SaveAssets();
    }
    static void SaveSupply(GameObject source,string name)
    {
        var copy=Object.Instantiate(source);copy.name=name;
        Object.DestroyImmediate(copy.GetComponent<GarageCheckpointItem>());
        copy.transform.SetParent(null);copy.transform.position=Vector3.zero;
        PrefabUtility.SaveAsPrefabAsset(copy,"Assets/Resources/VehicleService/"+name+".prefab");
        Object.DestroyImmediate(copy);
    }
}
