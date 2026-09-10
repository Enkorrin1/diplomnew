using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RogueDrive.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace RogueDrive.EditorTools
{
    public static class CampaignSceneAuthoring
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [MenuItem("RogueDrive/Scene Authoring/Save All Campaign Maps")]
        public static void Build()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            EditorSceneManager.SaveOpenScenes();
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/Stage1_Outskirts.unity");
            var generator=Object.FindFirstObjectByType<ProceduralTrackGenerator>();
            var existing=new SerializedObject(generator).FindProperty("authoredCampaign").objectReferenceValue;
            if(existing!=null) return; // Never replace an authored map on repeated invocation.
            var first=generator.transform.Find("BakedFirstMap");
            if(first==null) { generator.BakeFirstMapIntoScene(); first=generator.transform.Find("BakedFirstMap"); }
            var world=new GameObject("Campaign_Maps"); world.transform.SetParent(generator.transform,false);
            Undo.RegisterCreatedObjectUndo(world,"Author campaign maps"); first.SetParent(world.transform,true);
            var data=AssetDatabase.LoadAssetAtPath<CampaignSceneSettings>("Assets/Content/CampaignSceneSettings.asset");
            if(data==null) { data=ScriptableObject.CreateInstance<CampaignSceneSettings>(); AssetDatabase.CreateAsset(data,"Assets/Content/CampaignSceneSettings.asset"); }
            for(int i=0;i<data.Biomes.Length;i++) { data.Biomes[i].startDistance=i*1000; data.Biomes[i].endDistance=i==3?99999:(i+1)*1000; }
            EditorUtility.SetDirty(data);
            Set(generator,"campaignSettings",data); Set(generator,"authoredCampaign",world.transform);
            var initial=first.GetComponentsInChildren<TrackChunk>().OrderBy(x=>x.GetComponent<BakedMapChunk>().Order).ToArray();
            var last=initial.Last(); Vector3 position=last.EndPosition; Quaternion rotation=last.EndRotation;
            var assets=Resources.Load<FirstMapAssets>("FirstMapAssets");
            string[] names={"01_Outskirts","02_Wasteland","03_Industrial","04_EvacuationCity"};
            var all=new List<TrackChunk>(initial);
            first.name=names[0];
            for(int sector=1;sector<4;sector++)
            {
                var root=new GameObject(names[sector]); root.transform.SetParent(world.transform,false);
                for(int n=0;n<10;n++)
                {
                    TrackChunk c;
                    var biome=data.Biomes[sector];
                    if(n==2||n==6) c=(TrackChunk)Call(generator,"CreateCurvedChunk",position,rotation,biome,n==2?22f:-22f);
                    else if(n==4) c=(TrackChunk)Call(generator,"CreateBottleneckChunk",position,rotation,biome);
                    else if(n==7 && sector==2) c=(TrackChunk)Call(generator,"CreateForkChunk",position,rotation,biome);
                    else c=(TrackChunk)Call(generator,"CreateStraightChunk",position,rotation,biome,100f,24f);
                    c.transform.SetParent(root.transform,true); c.name=$"Map_{sector*10+n+1:00}_{c.Type}";
                    c.gameObject.AddComponent<BakedMapChunk>().Configure(sector*10+n);
                    Dress(c,sector,n,assets,biome); all.Add(c); position=c.EndPosition;rotation=c.EndRotation;
                }
            }
            // Authored checkpoints have real trigger volumes and editable arch geometry.
            for(int i=0;i<3;i++)
            {
                var end=all[i*10+9]; var go=new GameObject($"Checkpoint_{i+1}"); go.transform.SetParent(end.transform,false); go.transform.SetPositionAndRotation(end.EndPosition,end.EndRotation);
                var cp=go.AddComponent<SectorCheckpoint>(); cp.Configure(i+1,names[i]);
                var trigger=go.GetComponent<BoxCollider>(); trigger.isTrigger=true;trigger.center=new Vector3(0,4,0);trigger.size=new Vector3(24,8,4);
                var arch=new GameObject("CheckpointArchVisual"); arch.transform.SetParent(go.transform,false);
                Cube(arch.transform,"Pillar_Left",new Vector3(-11,4,0),new Vector3(1,8,1),Color.gray);
                Cube(arch.transform,"Pillar_Right",new Vector3(11,4,0),new Vector3(1,8,1),Color.gray);
                Cube(arch.transform,"ArchNeonBanner",new Vector3(0,8,0),new Vector3(23,1,1),new Color(.9f,.65f,.15f));
            }
            var bossGo=new GameObject("Boss_Juggernaut_Encounter");bossGo.SetActive(false);bossGo.transform.SetParent(all[38].transform,false);bossGo.transform.localPosition=new Vector3(0,.5f,32);
            var boss=bossGo.AddComponent<BossJuggernaut>();
            // Model construction is used only by this editor migration; runtime reuses these children.
            Call(boss,"BuildBossModel");
            foreach(var collider in bossGo.GetComponentsInChildren<Collider>(true)) if(collider.gameObject!=bossGo) Object.DestroyImmediate(collider);
            Set(generator,"authoredBoss",bossGo);
            BuildEndlessModules(generator,data.Biomes[0]);
            BuildObstaclePrefabs(generator,assets);
            // Old bake stored temporary meshes in scene; rebuild only the derived collision mesh.
            foreach(var chunk in all)
                foreach(var road in chunk.RoadRenderers)
                {
                    if(road==null) continue;
                    foreach(var meshCollider in road.GetComponents<MeshCollider>()) Object.DestroyImmediate(meshCollider);
                    var box=road.GetComponent<BoxCollider>(); if(box!=null)box.enabled=true;
                }
            SceneAuthoringMigration.PersistGeneratedAssets(scene.GetRootGameObjects());
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Selection.activeGameObject=world;
        }

        static void BuildEndlessModules(ProceduralTrackGenerator generator,BiomeConfig biome)
        {
            const string folder="Assets/Prefabs/TrackModules";System.IO.Directory.CreateDirectory(folder);var modules=new List<Object>();
            for(int i=0;i<6;i++)
            {
                TrackChunk c;
                if(i==0)c=(TrackChunk)Call(generator,"CreateStraightChunk",Vector3.zero,Quaternion.identity,biome,100f,24f);
                else if(i<3)c=(TrackChunk)Call(generator,"CreateCurvedChunk",Vector3.zero,Quaternion.identity,biome,i==1?-22f:22f);
                else c=(TrackChunk)Call(generator,i==3?"CreateSCurveChunk":i==4?"CreateBottleneckChunk":"CreateForkChunk",Vector3.zero,Quaternion.identity,biome);
                SceneAuthoringMigration.PersistGeneratedAssets(new[]{c.gameObject});
                var prefab=PrefabUtility.SaveAsPrefabAsset(c.gameObject,$"{folder}/{c.Type}.prefab");modules.Add(prefab.GetComponent<TrackChunk>());Object.DestroyImmediate(c.gameObject);
            }
            var so=new SerializedObject(generator);var p=so.FindProperty("endlessModules");p.arraySize=modules.Count;for(int i=0;i<modules.Count;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=modules[i];so.ApplyModifiedPropertiesWithoutUndo();
        }
        static void BuildObstaclePrefabs(ProceduralTrackGenerator generator,FirstMapAssets assets)
        {
            const string folder="Assets/Prefabs/TrackModules";
            for(int i=0;i<2;i++)
            {
                var go=new GameObject(i==0?"ExplosiveBarrel":"SupplyCrate");go.SetActive(false);
                Cube(go.transform,"Body",new Vector3(0,.6f,0),new Vector3(1,1.2f,1),i==0?new Color(.7f,.15f,.1f):new Color(.3f,.5f,.25f));
                var col=go.AddComponent<BoxCollider>();col.center=new Vector3(0,.6f,0);col.size=new Vector3(1,1.2f,1);
                if(i==0)go.AddComponent<ExplosiveBarrel>();else go.AddComponent<SupplyCrate>();
                SceneAuthoringMigration.PersistGeneratedAssets(new[]{go});var prefab=PrefabUtility.SaveAsPrefabAsset(go,$"{folder}/{go.name}.prefab");Set(generator,i==0?"explosiveBarrelPrefab":"supplyCratePrefab",prefab);Object.DestroyImmediate(go);
            }
        }
        static void Dress(TrackChunk chunk,int sector,int index,FirstMapAssets assets,BiomeConfig biome)
        {
            var root=new GameObject("Scenery");root.transform.SetParent(chunk.transform,false);
            Color ground=sector==1?new Color(.48f,.35f,.22f):sector==2?new Color(.18f,.26f,.22f):new Color(.24f,.25f,.27f);
            Cube(root.transform,"Landscape_Left",new Vector3(-57,-.65f,50),new Vector3(86,1,110),ground);
            Cube(root.transform,"Landscape_Right",new Vector3(57,-.65f,50),new Vector3(86,1,110),ground);
            for(int side=-1;side<=1;side+=2)
            {
                for(int n=0;n<3;n++)
                {
                    GameObject prefab=sector==1?(assets.rocks!=null&&assets.rocks.Length>0?assets.rocks[(index+n)%assets.rocks.Length]:assets.barrel):sector==2?assets.container:assets.station;
                    Place(root.transform,prefab,new Vector3(side*(22+n%2*12),0,15+n*30),new Vector3(12,14,16),side*90);
                    if(sector!=1)Place(root.transform,assets.lamp,new Vector3(side*15,0,15+n*30),new Vector3(2,8,3),side*90);
                }
                if(sector==1) Place(root.transform,assets.van,new Vector3(side*20,0,55),new Vector3(4,4,7),25*side);
                if(sector==2) Place(root.transform,assets.barrel,new Vector3(side*17,0,65),new Vector3(2,3,2),0);
                if(sector==3) Place(root.transform,assets.military,new Vector3(side*20,0,55),new Vector3(5,5,8),0);
            }
            if(index%3==0)
            {
                var hazard=Place(root.transform,assets.barrier,new Vector3(index%2==0?-6:6,0,50),new Vector3(4,2,2),0);
                if(hazard!=null){var box=hazard.AddComponent<BoxCollider>();box.center=new Vector3(0,.75f,0);box.size=new Vector3(4,1.5f,2);hazard.AddComponent<TrackObstacle>().Configure(false);}
            }
            AlignScenery(chunk);
        }

        [MenuItem("RogueDrive/Scene Authoring/Align Roadside Scenery")]
        public static void AlignAllScenery()
        {
            foreach(var chunk in Object.FindObjectsByType<TrackChunk>(FindObjectsSortMode.None)) AlignScenery(chunk);
            var scene=EditorSceneManager.GetActiveScene();SceneAuthoringMigration.PersistGeneratedAssets(scene.GetRootGameObjects());EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        static void AlignScenery(TrackChunk chunk)
        {
            var root=chunk.transform.Find("Scenery");if(root==null||chunk.RoadRenderers.Length==0)return;
            var children=root.Cast<Transform>().ToArray();
            Color ground=new Color(.3f,.28f,.22f);
            foreach(var child in children)
            {
                if(child.name.StartsWith("Landscape_")){ground=child.GetComponent<Renderer>().sharedMaterial.color;Object.DestroyImmediate(child.gameObject);continue;}
                Vector3 local=child.localPosition;Quaternion rotation=child.localRotation;
                var road=chunk.RoadRenderers.OrderBy(r=>Mathf.Abs(chunk.transform.InverseTransformPoint(r.transform.position).z-local.z)).First();
                float z=chunk.transform.InverseTransformPoint(road.transform.position).z;
                Vector3 pos=road.transform.position+road.transform.forward*((local.z-z)/Mathf.Max(.3f,Vector3.Dot(road.transform.forward,chunk.transform.forward)))+road.transform.right*local.x;
                pos.y=chunk.transform.position.y+local.y;child.SetPositionAndRotation(pos,road.transform.rotation*rotation);
            }
            int i=0;
            foreach(var road in chunk.RoadRenderers)
            {
                foreach(int side in new[]{-1,1})
                {
                    var terrain=new GameObject($"Roadside_{i}_{side}");terrain.transform.SetParent(root,false);terrain.transform.SetPositionAndRotation(road.transform.position,road.transform.rotation);
                    Cube(terrain.transform,"Ground",new Vector3(side*57,-.65f,0),new Vector3(86,1,road.transform.lossyScale.z+2),ground);
                }
                i++;
            }
            root.name="Scenery_RoadAligned";
        }
        static GameObject Place(Transform parent,GameObject prefab,Vector3 pos,Vector3 maxSize,float yaw)
        {
            if(prefab==null)return null;var holder=new GameObject(prefab.name);holder.transform.SetParent(parent,false);
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(prefab,holder.transform);visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;
            var renderers=visual.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return holder;
            Bounds bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            var size=bounds.size;float scale=Mathf.Min(maxSize.x/Mathf.Max(.01f,size.x),maxSize.y/Mathf.Max(.01f,size.y),maxSize.z/Mathf.Max(.01f,size.z));
            Vector3 offset=holder.transform.InverseTransformPoint(bounds.center);visual.transform.localScale*=scale;visual.transform.localPosition=-offset*scale+Vector3.up*size.y*scale*.5f;
            foreach(var c in visual.GetComponentsInChildren<Collider>())c.enabled=false;foreach(var r in visual.GetComponentsInChildren<Rigidbody>())r.isKinematic=true;
            holder.transform.localPosition=pos;holder.transform.localRotation=Quaternion.Euler(0,yaw,0);return holder;
        }
        static void Cube(Transform parent,string name,Vector3 position,Vector3 scale,Color color)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=new Material(Shader.Find("Standard")){color=color};
        }
        static object Call(object target,string method,params object[] args)
        {
            var info=target.GetType().GetMethod(method,Private);var parameters=info.GetParameters();
            var values=new object[parameters.Length];
            for(int i=0;i<values.Length;i++) values[i]=i<args.Length?args[i]:parameters[i].DefaultValue;
            return info.Invoke(target,values);
        }
        static void Set(Object target,string field,Object value){var so=new SerializedObject(target);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    }
}
