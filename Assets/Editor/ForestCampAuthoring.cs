using System;
using System.Collections.Generic;
using System.Linq;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RogueDrive.EditorTools
{
    public static class ForestCampAuthoring
    {
        const string Folder="Assets/Content/ForestCamp";
        const string Forest="Assets/Downloads/Low Poly Forest - Free Starter Pack/Prefabs/";
        const string Household="Assets/Downloads/JeffamazedDev/HouseholdPropsPack/Prefabs/Decoration/";
        static StageRoute route;
        static MeshCollider[] grounds;
        static Transform root;
        static Material earth,post;
        static Vector3 At(float d,float x)
        {
            route.Evaluate(d,out var p,out var q);p+=q*Vector3.right*x;
            float top=float.NegativeInfinity;var ray=new Ray(p+Vector3.up*100,Vector3.down);
            foreach(var c in grounds)if(c.Raycast(ray,out var hit,200))top=Mathf.Max(top,hit.point.y);
            if(!float.IsNegativeInfinity(top))p.y=top;return p;
        }
        static float Yaw(float d){route.Evaluate(d,out _,out var q);return q.eulerAngles.y;}
        static GameObject Place(string path,float d,float x,float size,float angle=0,int collision=1)
        {int count=root.childCount;JourneyLandscapeAuthoring.Place(path,root,At(d,x),Yaw(d)+angle,size,collision);return root.GetChild(count).gameObject;}
        [MenuItem("RogueDrive/Route/Finish Forest Camp Episode")]
        public static void Build()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.name!="Stage1_Outskirts")throw new InvalidOperationException("Open Stage1_Outskirts.");
            route=UnityEngine.Object.FindFirstObjectByType<StageRoute>(FindObjectsInactive.Include);
            if(route.transform.Find("Forest_Camp_Episode")!=null)throw new InvalidOperationException("Camp already authored.");
            grounds=route.GetComponentsInChildren<MeshCollider>(true).Where(c=>c.name.StartsWith("Terrain_")||c.name.StartsWith("BrokenVector_Road_")).ToArray();
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Content","ForestCamp");
            root=new GameObject("Forest_Camp_Episode").transform;root.SetParent(route.transform,false);
            earth=Mat("CampEarth",new Color(.36f,.33f,.25f));post=Mat("WeatheredWood",new Color(.27f,.25f,.20f));
            var old=route.transform.Find("Journey_Landscape/Detour_2_Birch_Camp");old.Find("Open_Courtyard").gameObject.SetActive(false);
            var site=route.transform.Find("Journey_Encounters/Stop_2_ЛЕСНОЙ ЛАГЕРЬ");
            var details=route.transform.Find("Journey_Encounters/Stop_Detail");
            foreach(Transform t in details)if(route.ProjectDistance(t.position)>3130&&route.ProjectDistance(t.position)<3260)t.gameObject.SetActive(false);
            foreach(Transform t in site)if(t.name=="WoodenPallet"||t.name=="Props_Bench_1")t.gameObject.SetActive(false);
            var oldPallet=old.Find("LowPolyPallet");if(oldPallet!=null)oldPallet.gameObject.SetActive(false);
            Clearing();Path(new[]{new Vector2(3183,-79),new Vector2(3190,-91),new Vector2(3196,-101),new Vector2(3207,-104)},2.1f,"Camp_Footpath");
            Path(new[]{new Vector2(3200,-103),new Vector2(3214,-117),new Vector2(3227,-130),new Vector2(3242,-143)},1.3f,"Supply_Trail");
            // Establish a compact inhabited area: table near parking, tent behind the fire, supplies in context.
            var table=Place(Household+"DiningRoom/DEC_DiningTable.prefab",3192,-96,2.4f,90);
            Place(Household+"DiningRoom/DEC_DiningChair.prefab",3191,-98,1.0f,110);
            Place(Household+"DiningRoom/DEC_DiningChair.prefab",3194,-96,1.0f,-30);
            var tableTop=Top(table);
            var mug=Place("Assets/Downloads/Smiley's Low Poly Tabletop Items/Prefabs/Mug.prefab",3192,-96,.17f,0,0);Seat(mug,new Vector3(tableTop.x-.28f,tableTop.y,tableTop.z));
            var enamel=Mat("EnamelCup",new Color(.70f,.75f,.69f));
            foreach(var renderer in mug.GetComponentsInChildren<Renderer>())renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>enamel).ToArray();
            Place("Assets/Downloads/ithappy/Apocalypse_Free/Prefabs/Characters/Adult_Survivor/Backpack.prefab",3194,-100,.65f,75);
            for(int i=0;i<9;i++){float a=i*Mathf.PI*2/9;Place(Forest+"Rocks/stone_small_2.prefab",3200+Mathf.Sin(a)*1.4f,-102+Mathf.Cos(a)*1.4f,.48f,i*40);}
            var ash=Mat("ColdAsh",new Color(.16f,.17f,.15f));
            Patch(new[]{new Vector2(3199,-103),new Vector2(3201,-103),new Vector2(3201,-101),new Vector2(3199,-101)},"ColdFire",ash);
            Place(Forest+"Props/fallen_log_small_2.prefab",3200,-102,1.3f,40);
            Place(Forest+"Props/fallen_log_small_2.prefab",3200,-102,1.1f,-35);
            Place(Forest+"Props/fallen_log_small_2.prefab",3203,-99,3,90);
            var crate=Place(Household+"Storeroom/CardboardBoxes/DEC_CardboardBox_OPENED.prefab",3206,-100,.8f,30,0);
            var water=site.GetComponentInChildren<FluidContainer>(true).gameObject;Seat(water,At(3191,-94));
            var med=site.GetComponentsInChildren<GarageItemFunction>(true).First(f=>f.Kind==GarageItemFunction.ItemKind.Medkit).gameObject;
            var tent=old.Find("LowPolyTent").gameObject;Seat(tent,At(3214,-103));
            // Put medicine just inside the open entrance, avoiding the tent's collision faces.
            Seat(med,tent.transform.TransformPoint(new Vector3(.32f,.085f,.02f)));
            var food=site.GetComponentsInChildren<GarageItemFunction>(true).First(f=>f.Kind==GarageItemFunction.ItemKind.Food).gameObject;Seat(food,new Vector3(tableTop.x+.35f,tableTop.y,tableTop.z));
            // A second supply spot costs a 40–50 m walk each way, and the wheel occupies the player's hands.
            Place("Assets/Downloads/SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Pick up Truck_color01.prefab",3245,-150,5.2f,28);
            var supplyCrate=Place(Household+"Storeroom/CardboardBoxes/DEC_CardboardBox_OPENED.prefab",3240,-144,.9f,-20,0);
            var wheel=Place("Assets/Downloads/GarageAssetPack/Prefabs/CarWheel.prefab",3242,-142,.73f,15,0);
            MakePortable(wheel,"Запасное внедорожное колесо",8,false);wheel.AddComponent<WorkshopPartItem>().data=new WorkshopPartData{definitionId="wheel_offroad",condition=.90f};
            var fuel=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/VehicleService/FuelCanister.prefab"),root);
            fuel.GetComponent<FluidContainer>().Configure(BunkerFluidType.Gasoline,10,5);Seat(fuel,At(3241,-140)+Vector3.up*.05f);
            for(int i=0;i<7;i++)
            {
                Place("Assets/Downloads/SimpleNaturePack/Prefabs/Bush_01.prefab",3217+i*4,-137-i*1.8f,1.5f+i%3*.4f,35*i);
                if(i%3==0)Place(Forest+"Props/tree_stump_medium.prefab",3216+i*4,-112-i*2.8f,.8f,30*i);
            }
            Sign(3195,-104,"ЗАПАС\nПО ТРОПЕ →",Yaw(3195)+70,1.6f);
            Sign(3195,-84,"НА ТРАССУ →",Yaw(3195)+180,2.2f);
            Sign(3290,-42,"ВЫЕЗД →",Yaw(3290),2.0f);
            for(int i=0;i<9;i++)
            {
                float d=3180+i*1.7f;foreach(float x in new[]{-82.6f,-80.7f})Patch(new[]{new Vector2(d,x-.11f),new Vector2(d+1.1f,x-.11f),new Vector2(d+1.1f,x+.11f),new Vector2(d,x+.11f)},"Tyre_Mark",ash);
            }
            var stop=site.GetComponentInChildren<JourneyStop>();stop.Configure("ЛЕСНОЙ ЛАГЕРЬ","Вода · аптечка · запасное колесо","Вода у стола, аптечка в палатке. Запасное колесо и бензин — у пикапа по тропе за лагерем. Буря не ждёт. Выезд возвращает на трассу.",3200,-1);
            var episode=root.gameObject.AddComponent<ForestCampEpisode>();episode.Configure(new[]{water,med,food,wheel,fuel},new[]{"Вода","Аптечка","Еда","Внедорожное колесо","Бензин"},At(3190,-81));
            var trail=new[]{At(3190,-81),At(3196,-101),At(3207,-104),At(3214,-117),At(3227,-130),At(3242,-143)};
            foreach(var tree in route.GetComponentsInChildren<Transform>(true))
            {
                if(!(tree.name.StartsWith("birch_")||tree.name.StartsWith("oak_")||tree.name.StartsWith("pine_")||tree.name.StartsWith("Bush_")))continue;
                for(int i=1;i<trail.Length;i++)
                {var edge=trail[i]-trail[i-1];edge.y=0;var delta=tree.position-trail[i-1];delta.y=0;float t=Mathf.Clamp01(Vector3.Dot(delta,edge)/Mathf.Max(.01f,edge.sqrMagnitude));if((delta-edge*t).sqrMagnitude<3.2f*3.2f){tree.gameObject.SetActive(false);break;}}
            }
            Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("[ForestCamp] Five contextual supplies, optional stash, footpath, return markers and cargo feedback saved.");
        }
        static Bounds Bounds(GameObject go){var rs=go.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
        static Vector3 Top(GameObject go){var b=Bounds(go);return new Vector3(b.center.x,b.max.y+.025f,b.center.z);}
        static void Seat(GameObject go,Vector3 p){var b=Bounds(go);go.transform.position+=p-new Vector3(b.center.x,b.min.y,b.center.z);}
        static void MakePortable(GameObject go,string title,float mass,bool pocket)
        {
            var b=Bounds(go);var c=go.AddComponent<BoxCollider>();c.center=go.transform.InverseTransformPoint(b.center);c.size=new Vector3(b.size.x/go.transform.lossyScale.x,b.size.y/go.transform.lossyScale.y,b.size.z/go.transform.lossyScale.z);
            go.AddComponent<Rigidbody>().mass=mass;var prop=go.AddComponent<PhysicsProp>();prop.Configure(title);prop.SetPocketSized(pocket);
        }
        static Material Mat(string name,Color color){var m=new Material(Shader.Find("Standard")){name=name,color=color};m.SetFloat("_Glossiness",0);AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat");return m;}
        static void Clearing()
        {
            var edge=new List<Vector2>();for(int i=0;i<24;i++){float a=i*Mathf.PI*2/24;float r=1+.07f*Mathf.Sin(i*4.2f);edge.Add(new Vector2(3199+Mathf.Sin(a)*23*r,-100+Mathf.Cos(a)*17*r));}Patch(edge.ToArray(),"Forest_Clearing",earth);
        }
        static void Patch(Vector2[] edge,string name,Material m)
        {
            var v=edge.Select(p=>At(p.x,p.y)+Vector3.up*.025f).ToArray();var tr=new List<int>();for(int i=1;i<v.Length-1;i++){if(Vector3.Cross(v[i]-v[0],v[i+1]-v[0]).y>0)tr.AddRange(new[]{0,i,i+1});else tr.AddRange(new[]{0,i+1,i});}
            Mesh(name,v,tr.ToArray(),m);
        }
        static void Path(Vector2[] points,float width,string name)
        {
            var v=new List<Vector3>();var tr=new List<int>();for(int i=0;i<points.Length-1;i++)
            {
                int steps=Mathf.CeilToInt(Vector2.Distance(points[i],points[i+1])/1.5f);for(int j=0;j<steps;j++)
                {Vector2 p=Vector2.Lerp(points[i],points[i+1],j/(float)steps),next=Vector2.Lerp(points[i],points[i+1],(j+1f)/steps);var normal=new Vector2(-(next-p).y,(next-p).x).normalized*width*.5f;int n=v.Count;
                foreach(var a in new[]{p-normal,p+normal,next-normal,next+normal})v.Add(At(a.x,a.y)+Vector3.up*.045f);
                tr.AddRange(new[]{n,n+2,n+1,n+1,n+2,n+3});}
            }Mesh(name,v.ToArray(),tr.ToArray(),earth);
        }
        static void Mesh(string name,Vector3[] v,int[] tr,Material m)
        {for(int i=0;i<tr.Length;i+=3)if(Vector3.Cross(v[tr[i+1]]-v[tr[i]],v[tr[i+2]]-v[tr[i]]).y<0){int b=tr[i+1];tr[i+1]=tr[i+2];tr[i+2]=b;}var mesh=new UnityEngine.Mesh{name=name};mesh.vertices=v;mesh.triangles=tr;mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Folder+"/"+name+".asset"));var go=new GameObject(name);go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=m;}
        static void Sign(float d,float x,string text,float yaw,float width)
        {
            var go=new GameObject("Camp_Wayfinding");go.transform.SetParent(root,false);go.transform.position=At(d,x);
            var stem=GameObject.CreatePrimitive(PrimitiveType.Cube);stem.transform.SetParent(go.transform,false);stem.transform.localPosition=Vector3.up*.8f;stem.transform.localScale=new Vector3(.10f,1.6f,.10f);stem.GetComponent<Renderer>().sharedMaterial=post;
            var board=GameObject.CreatePrimitive(PrimitiveType.Cube);board.transform.SetParent(go.transform,false);board.transform.localPosition=Vector3.up*1.6f;board.transform.localScale=new Vector3(width,.6f,.1f);board.GetComponent<Renderer>().sharedMaterial=post;
            var label=new GameObject("Paint").AddComponent<TextMesh>();label.transform.SetParent(go.transform,false);label.transform.localPosition=new Vector3(0,1.6f,-.06f);label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.GetComponent<Renderer>().sharedMaterial=label.font.material;label.fontSize=64;label.text=text;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=new Color(.83f,.79f,.64f);var b=label.GetComponent<Renderer>().bounds;label.transform.localScale=Vector3.one*Mathf.Min((width-.2f)/b.size.x,.48f/b.size.y);go.transform.rotation=Quaternion.Euler(0,yaw,0);
        }
    }
}
