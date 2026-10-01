using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RogueDrive.Gameplay.Hub
{
    public enum WorkshopSection { Engine, Suspension, Body }
    public enum WorkshopSlot { Engine, Turbo, Pistons, Filter, Radiator, FrontSprings, RearSprings, FrontDampers, RearDampers, FrontLeftWheel, FrontRightWheel, RearLeftWheel, RearRightWheel, Bumper, Underbody, Armor, RoofRack, Lights }
    public enum WorkshopPartKind { Engine, Turbo, Pistons, Filter, Radiator, Springs, Dampers, Wheel, Bumper, Underbody, Armor, RoofRack, Lights }
    [Flags] public enum WorkshopEquipment { None = 0, Tools = 1, Lift = 2, Crane = 4, Bench = 8, All = 15 }

    [Serializable] public sealed class WorkshopPartData : ISerializationCallbackReceiver
    {
        public string instanceId = Guid.NewGuid().ToString("N");
        public string definitionId;
        [Range(0, 1)] public float condition = 1;
        // Only engines contain components (turbo, pistons, filter). Serialize those
        // as leaves so Unity never expands a recursive type to its depth limit.
        [NonSerialized] List<WorkshopPartData> components = new List<WorkshopPartData>();
        public List<WorkshopPartData> Internals => components;
        // Keep the original field name: old JSON and authored scenes remain readable.
        [SerializeField] List<InternalPartData> internals = new List<InternalPartData>();

        [Serializable] sealed class InternalPartData
        {
            public string instanceId, definitionId;
            public float condition;
        }

        public void OnBeforeSerialize()
        {
            if (internals == null) internals = new List<InternalPartData>();
            internals.Clear();
            if (components == null) return;
            foreach (var part in components)
            {
                if (part == null) continue;
                internals.Add(new InternalPartData {
                    instanceId = part.instanceId, definitionId = part.definitionId, condition = part.condition
                });
            }
        }

        public void OnAfterDeserialize()
        {
            components = new List<WorkshopPartData>();
            if (internals == null) return;
            foreach (var part in internals)
            {
                if (part == null) continue;
                components.Add(new WorkshopPartData {
                    instanceId = part.instanceId, definitionId = part.definitionId, condition = part.condition
                });
            }
        }

        public WorkshopPartData Copy()
        {
            var copy = new WorkshopPartData { instanceId = instanceId, definitionId = definitionId, condition = condition };
            if (components != null)
                foreach (var part in components)
                    if (part != null) copy.Internals.Add(part.Copy());
            return copy;
        }
    }
    [Serializable] public sealed class WorkshopPartDefinition
    {
        public string id, title, role;
        public WorkshopPartKind kind;
        public int tier = 1, price = 100, labor = 25;
        public float seconds = 15, power, fuel, heat, cooling, grip, spring, damping, clearance, mass, protection, light;
        public int cargo;
        public WorkshopEquipment equipment = WorkshopEquipment.Tools;
    }
    public struct WorkshopStats
    {
        public float power, fuel, heat, cooling, grip, spring, damping, clearance, mass, protection, light;
        public int cargo;
        public static WorkshopStats Standard => new WorkshopStats { power=1, fuel=1, heat=1, cooling=1, grip=1, spring=1, damping=1, light=1 };
        public void Add(WorkshopPartData part)
        {
            var d=WorkshopCatalog.Get(part?.definitionId); if(d==null)return;
            float c=Mathf.Clamp01(part.condition);
            power+=d.power*c; if(d.kind==WorkshopPartKind.Engine)power-=.4f*(1-c);
            fuel+=d.fuel; heat+=d.heat; cooling+=d.cooling*c;
            grip+=d.grip*c; spring+=d.spring*c; damping+=d.damping*c; clearance+=d.clearance;
            mass+=d.mass; protection+=d.protection*c; light+=d.light*c; cargo+=d.cargo;
            if(d.kind==WorkshopPartKind.Engine) foreach(var child in part.Internals)Add(child);
        }
    }
    public static class WorkshopCatalog
    {
        [Serializable] sealed class CatalogFile { public WorkshopPartDefinition[] parts; }
        static WorkshopPartDefinition[] parts;
        public static IReadOnlyList<WorkshopPartDefinition> All => parts ?? (parts=Load());
        static WorkshopPartDefinition[] Load()
        {
            var asset=Resources.Load<TextAsset>("Workshop/catalog");
            if(asset==null)throw new InvalidOperationException("Workshop/catalog is missing");
            return JsonUtility.FromJson<CatalogFile>(asset.text).parts;
        }
        public static WorkshopPartDefinition Get(string id) => string.IsNullOrEmpty(id)?null:All.FirstOrDefault(p=>p.id==id);
        public static WorkshopPartKind Kind(WorkshopSlot slot)
        {
            if(slot==WorkshopSlot.FrontSprings||slot==WorkshopSlot.RearSprings)return WorkshopPartKind.Springs;
            if(slot==WorkshopSlot.FrontDampers||slot==WorkshopSlot.RearDampers)return WorkshopPartKind.Dampers;
            if(slot>=WorkshopSlot.FrontLeftWheel&&slot<=WorkshopSlot.RearRightWheel)return WorkshopPartKind.Wheel;
            return (WorkshopPartKind)Enum.Parse(typeof(WorkshopPartKind),slot.ToString());
        }
        public static bool Internal(WorkshopSlot slot)=>slot==WorkshopSlot.Turbo||slot==WorkshopSlot.Pistons||slot==WorkshopSlot.Filter;
        public static WorkshopSection Section(WorkshopSlot slot)=>slot<=WorkshopSlot.Radiator?WorkshopSection.Engine:slot<=WorkshopSlot.RearRightWheel?WorkshopSection.Suspension:WorkshopSection.Body;
        public static string SlotName(WorkshopSlot slot)
        {
            string[] names={"Двигатель","Турбина","Комплект поршней","Воздушный фильтр","Радиатор","Пружины · перед","Пружины · зад","Амортизаторы · перед","Амортизаторы · зад","Колесо · перед лев.","Колесо · перед прав.","Колесо · зад лев.","Колесо · зад прав.","Кенгурятник","Защита днища","Броня","Багажник на крыше","Фары"};
            return names[(int)slot];
        }
        public static WorkshopPartData New(string id)=>new WorkshopPartData{definitionId=id};
    }
}
