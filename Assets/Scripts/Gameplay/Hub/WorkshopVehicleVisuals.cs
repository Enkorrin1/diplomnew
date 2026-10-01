using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>Low-poly bolt-on silhouettes; no scene or location authoring.</summary>
    public sealed class WorkshopVehicleVisuals : MonoBehaviour
    {
        Transform root;
        Material metal;
        public void Apply(VehicleWorkshop workshop)
        {
            if(root==null){root=new GameObject("WorkshopAttachments").transform;root.SetParent(transform,false);}
            for(int i=root.childCount-1;i>=0;i--){root.GetChild(i).gameObject.SetActive(false);Destroy(root.GetChild(i).gameObject);}
            if(metal==null)
            {
                var renderer=GetComponentInChildren<MeshRenderer>();
                if(renderer!=null&&renderer.sharedMaterial!=null){metal=new Material(renderer.sharedMaterial);metal.color=new Color(.22f,.26f,.23f);}
            }
            if(workshop.Installed(WorkshopSlot.Bumper)!=null)
            {
                Bar("Bumper",new Vector3(0,.6f,2),new Vector3(1.7f,.12f,.15f));
                Bar("BumperL",new Vector3(-.6f,.8f,2),new Vector3(.12f,.6f,.15f));Bar("BumperR",new Vector3(.6f,.8f,2),new Vector3(.12f,.6f,.15f));
            }
            if(workshop.Installed(WorkshopSlot.RoofRack)!=null)
            {
                for(int i=0;i<5;i++)Bar("RackSlat",new Vector3(0,1.8f,-.6f+i*.3f),new Vector3(1.5f,.07f,.07f));
                Bar("RackL",new Vector3(-.75f,1.9f,0),new Vector3(.06f,.2f,1.4f));Bar("RackR",new Vector3(.75f,1.9f,0),new Vector3(.06f,.2f,1.4f));
            }
            if(workshop.Installed(WorkshopSlot.Underbody)!=null)Bar("Underbody",new Vector3(0,.1f,0),new Vector3(1.5f,.08f,2.7f));
            if(workshop.Installed(WorkshopSlot.Armor)!=null)
            {Bar("ArmorL",new Vector3(-.92f,.7f,0),new Vector3(.06f,.65f,1.8f));Bar("ArmorR",new Vector3(.92f,.7f,0),new Vector3(.06f,.65f,1.8f));}
            if(workshop.Installed(WorkshopSlot.Lights)!=null)
                foreach(float x in new[]{-.5f,.5f})
                {
                    var lamp=Bar("Worklight",new Vector3(x,1.85f,.65f),new Vector3(.24f,.16f,.15f));
                    var light=lamp.AddComponent<Light>();light.type=LightType.Spot;light.range=35;light.spotAngle=65;light.intensity=workshop.Stats.light;light.color=new Color(1,.92f,.7f);
                }
        }
        GameObject Bar(string title,Vector3 position,Vector3 size)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=title;go.transform.SetParent(root,false);go.transform.localPosition=position;go.transform.localScale=size;
            var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            if(metal!=null)go.GetComponent<Renderer>().sharedMaterial=metal;return go;
        }
        void OnDestroy(){if(metal!=null)Destroy(metal);}
    }
}
