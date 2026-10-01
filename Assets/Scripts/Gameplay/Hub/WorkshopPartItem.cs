using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    public sealed class WorkshopPartItem : MonoBehaviour
    {
        public WorkshopPartData data;
        public bool Retired { get; private set; }
        public static void Retire(PocketSlotData item)
        {
            var part=item.WorldObject!=null?item.WorldObject.GetComponent<WorkshopPartItem>():null;
            if(part!=null)part.Retired=true;
        }
        Material finish;
        void OnDestroy(){if(finish!=null)Destroy(finish);}
        public static WorkshopPartData Read(PocketSlotData item)
        {
            if(item.IsEmpty)return null;
            var physical=item.WorldObject!=null?item.WorldObject.GetComponent<WorkshopPartItem>():null;
            if(physical!=null)return physical.data;
            string id=item.legacyType==BunkerAssemblyItemType.Engine?"engine_stock":item.legacyType==BunkerAssemblyItemType.Radiator?"radiator_stock":item.legacyType==BunkerAssemblyItemType.Wheel?"wheel_road":null;
            return id==null?null:new WorkshopPartData{definitionId=id,instanceId=item.id};
        }
        public static PocketSlotData Create(WorkshopPartData data,string identity=null)
        {
            var definition=WorkshopCatalog.Get(data.definitionId);
            // Procedural low-poly transport models; independent of item identity and balance data.
            var obj=GameObject.CreatePrimitive(definition.kind==WorkshopPartKind.Wheel?PrimitiveType.Cylinder:PrimitiveType.Cube);
            obj.name=definition.title;
            obj.transform.localScale=definition.kind==WorkshopPartKind.Wheel?new Vector3(.55f,.14f,.55f):new Vector3(.5f,.32f,.38f);
            obj.AddComponent<Rigidbody>().mass=8;
            var prop=obj.AddComponent<PhysicsProp>();prop.Configure(definition.title);
            prop.SetPocketSized(definition.kind==WorkshopPartKind.Filter||definition.kind==WorkshopPartKind.Turbo);
            var part=obj.AddComponent<WorkshopPartItem>();part.data=data.Copy();
            part.finish=new Material(obj.GetComponent<Renderer>().sharedMaterial);
            part.finish.color=definition.kind==WorkshopPartKind.Wheel?new Color(.12f,.14f,.14f):new Color(.32f,.38f,.30f);
            obj.GetComponent<Renderer>().sharedMaterial=part.finish;
            if(definition.kind==WorkshopPartKind.Engine)
            {
                for(int i=0;i<4;i++)Detail(obj,"CylinderHead",new Vector3(-.33f+i*.22f,.55f,0),new Vector3(.17f,.18f,.65f),part.finish);
                Detail(obj,"Sump",new Vector3(0,-.55f,0),new Vector3(.75f,.18f,.65f),part.finish);
            }
            else if(definition.kind==WorkshopPartKind.Radiator)
            {
                obj.transform.localScale=new Vector3(.65f,.5f,.12f);
                for(int i=0;i<8;i++)Detail(obj,"CoolingFin",new Vector3(-.42f+i*.12f,0,-.6f),new Vector3(.04f,.85f,.18f),part.finish);
            }
            else if(definition.kind==WorkshopPartKind.RoofRack)
            {
                obj.transform.localScale=new Vector3(.65f,.08f,.8f);
                for(int i=0;i<2;i++)Detail(obj,"Rail",new Vector3(i==0?-.47f:.47f,1,0),new Vector3(.06f,1.5f,1),part.finish);
            }
            else if(definition.kind==WorkshopPartKind.Springs||definition.kind==WorkshopPartKind.Dampers)
            {
                obj.transform.localScale=new Vector3(.3f,.5f,.2f);
                for(int i=0;i<6;i++)Detail(obj,"SpringCoil",new Vector3(0,-.42f+i*.16f,0),new Vector3(1.25f,.055f,1.25f),part.finish);
            }
            obj.AddComponent<GarageCheckpointItem>().id=identity??"workshop_"+data.instanceId;
            var item=InventoryStackOps.FromObject(obj,obj.transform.localScale);
            return item;
        }
        static void Detail(GameObject parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent.transform,false);go.transform.localPosition=position;go.transform.localScale=scale;
            var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);go.GetComponent<Renderer>().sharedMaterial=material;
        }
    }
}
