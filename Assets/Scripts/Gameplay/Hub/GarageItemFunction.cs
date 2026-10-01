using UnityEngine;
using RogueDrive.Gameplay.Narrative;
namespace RogueDrive.Gameplay.Hub
{
    /// <summary>State lives on the physical item and follows it through slots, boxes and cargo.</summary>
    public sealed class GarageItemFunction : MonoBehaviour
    {
        public enum ItemKind { None,Battery,Flashlight,Radio,Wrench,Hammer,Screwdriver,Bolt,Burner,Food,Drink,Mug,Pot,Plate,Barrel,Medkit,RepairKit }
        [SerializeField] ItemKind kind;
        [SerializeField,Range(0,1)] float charge=1;
        [SerializeField,Min(1)] float durationSeconds=300;
        [SerializeField] float waterLiters;
        [SerializeField] int portions;
        [SerializeField] float heatSeconds;
        [SerializeField] bool boiled;
        [SerializeField] Light lamp;
        [SerializeField] ParticleSystem flame;
        [SerializeField] Transform flameAnchor;
        bool powered;
        float nextUse;
        public ItemKind Kind=>kind;
        public float Charge=>charge;
        public float WaterLiters=>waterLiters;
        public int Portions=>portions;
        public bool IsBoiled=>boiled;
        public bool IsRunning=>powered;
        public bool UsesBattery=>kind==ItemKind.Flashlight||kind==ItemKind.Radio;
        public bool WorldUsable=>kind==ItemKind.Burner||kind==ItemKind.Pot||kind==ItemKind.Barrel||kind==ItemKind.Food||kind==ItemKind.Drink;
        public string Status=>UsesBattery?$"Заряд {charge:P0}":kind==ItemKind.Burner?$"Газ {charge:P0}":kind==ItemKind.Pot?$"Вода {waterLiters:0.#} л · {(boiled?"кипячёная":$"нагрев {Mathf.Clamp01(heatSeconds/20f):P0}")}":kind==ItemKind.Barrel||kind==ItemKind.Mug||kind==ItemKind.Drink?$"Вода {waterLiters:0.#} л":kind==ItemKind.Food||kind==ItemKind.Plate?$"Порций: {portions}":"";
        public string ActionLabel=>kind switch
        {
            ItemKind.Flashlight=>(powered?"Выключить":"Включить")+$" · {Status} · [R] Батарейка",
            ItemKind.Radio=>$"Слушать · {Status} · [R] Батарейка",
            ItemKind.Burner=>(powered?"Погасить":"Зажечь")+$" · {Status}",
            ItemKind.Wrench=>"Затянуть крепёж (болт) / снять колесо",
            ItemKind.Hammer=>"Выправить бампер",ItemKind.Screwdriver=>"Обслужить клеммы АКБ",
            ItemKind.Battery=>"Вставить в фонарь или рацию",ItemKind.Bolt=>"Крепёж для ремонта ключом",
            ItemKind.Medkit=>"Перевязаться",ItemKind.RepairKit=>"Восстановить бампер (нужен ключ)",ItemKind.Food=>"Поесть · "+Status,ItemKind.Plate=>"Взять порцию / поесть · "+Status,
            ItemKind.Drink=>"Выпить · "+Status,ItemKind.Mug=>"Набрать воду / выпить · "+Status,
            ItemKind.Pot=>"Проверить кипячение · "+Status,ItemKind.Barrel=>"Запас воды · "+Status,_=>"Использовать"
        };
        public void Configure(ItemKind value)
        {
            kind=value;
            if(kind==ItemKind.Food)portions=4;
            if(kind==ItemKind.Drink)waterLiters=.5f;
            if(kind==ItemKind.Flashlight){durationSeconds=300;lamp=GetComponentInChildren<Light>(true);if(lamp!=null)lamp.enabled=false;}
            if(kind==ItemKind.Radio)durationSeconds=600;
        }
        public static ItemKind Classify(string n)
        {
            if(n.Contains("RepairKit"))return ItemKind.RepairKit;
            if(n.Contains("Flashlight"))return ItemKind.Flashlight;
            if(n.Contains("Walkie"))return ItemKind.Radio;
            if(n.Contains("BatterySmall")||n.Contains("BatteryLarge"))return ItemKind.Battery;
            if(n.Contains("Wrench"))return ItemKind.Wrench;if(n.Contains("Hammer"))return ItemKind.Hammer;
            if(n.Contains("Screwdriver"))return ItemKind.Screwdriver;if(n.Contains("Bolt"))return ItemKind.Bolt;
            if(n.Contains("Gas_Burner"))return ItemKind.Burner;if(n.Contains("First_Aid"))return ItemKind.Medkit;
            if(n.Contains("Pizza"))return ItemKind.Food;if(n.Contains("SoftDrink"))return ItemKind.Drink;
            if(n.Contains("CoffeeMug"))return ItemKind.Mug;if(n.Contains("CookingPot"))return ItemKind.Pot;
            if(n.Contains("DinnerPlate"))return ItemKind.Plate;if(n.Contains("Barrel"))return ItemKind.Barrel;
            return ItemKind.None;
        }
        public static GarageItemFunction Ensure(GameObject obj)
        {
            var existing=obj.GetComponent<GarageItemFunction>();if(existing!=null)return existing;
            var value=Classify(obj.name);if(value==ItemKind.None)return null;
            var result=obj.AddComponent<GarageItemFunction>();result.Configure(value);return result;
        }
        void Awake(){if(kind==ItemKind.Flashlight&&lamp==null)lamp=GetComponentInChildren<Light>(true);}
        void OnDisable(){SetPower(false);}
        void Update(){Advance(Time.deltaTime);}
        public void Advance(float seconds)
        {
            if(seconds<=0)return;
            if(powered&&kind==ItemKind.Burner&&(IsCarried||Vector3.Dot(transform.up,Vector3.up)<.65f))SetPower(false);
            if(powered&&(kind==ItemKind.Flashlight||kind==ItemKind.Burner))
            {
                charge=Mathf.Max(0,charge-seconds/durationSeconds);
                if(charge<=0){SetPower(false);Notify(kind==ItemKind.Burner?"В горелке закончился газ.":"Фонарь разрядился. Нажмите R, чтобы заменить батарейку.");}
                else if(lamp!=null)lamp.intensity=kind==ItemKind.Burner?1.3f+Mathf.Sin(Time.time*17)*.15f:Mathf.Lerp(.3f,2f,Mathf.Clamp01(charge*5));
            }
            if(kind==ItemKind.Pot&&waterLiters>0&&!boiled)
            {
                bool heating=false;
                foreach(var burner in FindObjectsByType<GarageItemFunction>(FindObjectsSortMode.None))
                    if(burner.kind==ItemKind.Burner&&burner.powered&&Vector3.Distance(transform.position,burner.transform.position)<.8f&& !IsCarried && Vector3.Dot(transform.up,Vector3.up)>.65f){heating=true;break;}
                heatSeconds=heating?Mathf.Min(20,heatSeconds+seconds):Mathf.Max(0,heatSeconds-seconds*.25f);
                if(heatSeconds>=20){boiled=true;Notify("Вода вскипела. Наберите её кружкой.");}
            }
        }
        bool IsCarried=>GetComponent<PhysicsProp>()?.IsHeld==true;
        void SetPower(bool value)
        {
            powered=value;if(lamp!=null)lamp.enabled=value;
            if(flame!=null){if(value)flame.Play();else flame.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
        }
        public bool ReplaceBattery()
        {
            if(!UsesBattery)return false;
            if(charge>.99f){Notify("Батарейка ещё полная.");return false;}
            if(PlayerPocketInventory.Instance==null||!PlayerPocketInventory.Instance.ConsumeSupply(ItemKind.Battery)){Notify("Нужна батарейка в инвентаре.");return false;}
            charge=1;Notify("Батарейка заменена. Заряд 100%.");return true;
        }
        public bool ReceiveWater(FluidContainer source)
        {
            if(source==null||source.FluidType!=BunkerFluidType.Water){Notify("В эту ёмкость можно наливать только воду.");return false;}
            if(kind==ItemKind.Barrel&&(source.IsEmpty||Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)))
            {
                float drawn=source.Fill(Mathf.Min(waterLiters,source.MaxCapacityLiters-source.CurrentLiters),BunkerFluidType.Water);
                if(drawn<=0){Notify("Бочка пуста или канистра заполнена.");return false;}
                waterLiters-=drawn;Notify($"Из бочки набрано {drawn:0.##} л воды.");return true;
            }
            float capacity=kind==ItemKind.Pot?2:kind==ItemKind.Barrel?50:kind==ItemKind.Mug?.35f:0;
            float amount=source.PourOut(Mathf.Min(source.CurrentLiters,Mathf.Max(0,capacity-waterLiters)));
            if(amount<=0){Notify("Нет воды или ёмкость уже полная.");return false;}
            waterLiters+=amount;boiled=false;heatSeconds=0;Notify($"Перелито {amount:0.##} л воды.");return true;
        }
        public bool Use(GaragePlayerController player,GarageItemFunction target=null)
        {
            if(Time.unscaledTime<nextUse)return false;nextUse=Time.unscaledTime+.35f;
            if(kind==ItemKind.Flashlight||kind==ItemKind.Burner)
            {
                if(kind==ItemKind.Burner&&IsCarried){Notify("Поставьте горелку на ровную поверхность: Q. Затем наведитесь на неё и нажмите F.");return false;}
                if(charge<=0){Notify(UsesBattery?"Нет заряда. Нужна батарейка: R.":"Газ закончился. Нужен заправленный баллон горелки.");return false;}
                if(kind==ItemKind.Burner&&Vector3.Dot(transform.up,Vector3.up)<.65f){Notify("Поставьте горелку вертикально.");return false;}
                if(kind==ItemKind.Burner)EnsureFlame();SetPower(!powered);return true;
            }
            if(kind==ItemKind.Radio)
            {
                if(charge<12f/durationSeconds){Notify("Рация разряжена. Замените батарейку: R.");return false;}
                charge-=12f/durationSeconds;nextUse=Time.unscaledTime+12;
                var radio=RadioTransmissionSystem.Instance;if(radio==null)radio=new GameObject("PortableRadioBroadcast").AddComponent<RadioTransmissionSystem>();radio.PlayBunkerWakeup();return true;
            }
            if(kind==ItemKind.Battery)
            {
                if(target!=null&&target.UsesBattery)return target.ReplaceBattery();
                Notify("Выберите фонарь или рацию в быстром слоте и нажмите R.");return false;
            }
            if(kind==ItemKind.Wrench||kind==ItemKind.Hammer||kind==ItemKind.Screwdriver||kind==ItemKind.RepairKit)return UseTool(player);
            if(kind==ItemKind.Bolt){Notify("Болт расходуется при ремонте креплений гаечным ключом.");return false;}
            if(kind==ItemKind.Pot||kind==ItemKind.Barrel){Notify(Status+(kind==ItemKind.Pot?". Поставьте кастрюлю рядом с горящей горелкой; наберите воду кружкой.":". Перелейте воду из канистры; набирайте кружкой."));return false;}
            if(kind==ItemKind.Mug&&target!=null&&(target.kind==ItemKind.Pot||target.kind==ItemKind.Barrel))
            {
                if(target.kind==ItemKind.Pot&&!target.boiled){Notify("Дождитесь кипячения воды.");return false;}
                float amount=Mathf.Min(.35f-waterLiters,target.waterLiters);if(amount<=0)return false;
                target.waterLiters-=amount;waterLiters+=amount;boiled=target.boiled;Notify("Кружка наполнена.");return true;
            }
            if(kind==ItemKind.Plate&&target!=null&&target.kind==ItemKind.Food)
            {if(portions>0||target.portions<=0)return false;target.portions--;portions++;Notify("Порция на тарелке.");return true;}
            var needs=PlayerFieldNeeds.For(player);if(needs==null)return false;
            if(kind==ItemKind.Medkit)
            {
                if(!needs.Heal(35)){Notify("Лечение не требуется.");return false;}
                var prop=GetComponent<PhysicsProp>();if(PlayerPocketInventory.Instance?.ConsumePhysical(prop)!=true)PlayerHandsInventory.Instance?.ConsumeHeldItem();Notify("Перевязка: восстановлено здоровье.");return true;
            }
            if(kind==ItemKind.Food||kind==ItemKind.Plate)
            {if(portions<=0){Notify("Еды не осталось.");return false;}if(!needs.Eat(25)){Notify("Вы сыты.");return false;}portions--;Notify("Вы поели. "+Status);return true;}
            if(kind==ItemKind.Drink||kind==ItemKind.Mug)
            {if(waterLiters<=0){Notify("Ёмкость пуста.");return false;}float sip=Mathf.Min(.2f,waterLiters);if(!needs.Drink(sip*100)){Notify("Пить пока не хочется.");return false;}waterLiters-=sip;Notify("Вы попили. "+Status);return true;}
            return false;
        }
        bool UseTool(GaragePlayerController player)
        {
            var target=player!=null?player.GetComponentInChildren<GarageInteractionRaycaster>()?.CurrentTarget as Component:null;
            var spot=target as VehiclePartHotspot;
            if(kind==ItemKind.Wrench&&spot!=null&&spot.RequiredItem==BunkerAssemblyItemType.Wheel&&spot.IsInstalled)return spot.TryRemoveWheel(player);
            var car=target!=null?target.GetComponentInParent<VehicleModularState>():null;
            if(car==null){Notify("Наведитесь на узел машины.");return false;}
            if(kind==ItemKind.Screwdriver){Notify("Отвёртка нужна для установки и подключения аккумулятора под капотом.");return false;}
            if(kind==ItemKind.RepairKit)
            {
                if(!(PlayerPocketInventory.Instance?.HasTool(ItemKind.Wrench)??false)){Notify("Нужен гаечный ключ в инвентаре.");return false;}
                if(!car.TryFieldRepair(out var repairMessage)){Notify(repairMessage);return false;}
                if(PlayerPocketInventory.Instance?.ConsumePhysical(GetComponent<PhysicsProp>())!=true)PlayerHandsInventory.Instance?.ConsumeHeldItem();
                Notify(repairMessage);return true;
            }
            if(car.BumperIntegrity>=.999f){Notify("Бампер и крепления исправны.");return false;}
            if(kind==ItemKind.Wrench&&!(PlayerPocketInventory.Instance?.ConsumeSupply(ItemKind.Bolt)??false)){Notify("Для креплений нужен болт в инвентаре.");return false;}
            car.RepairBumper(kind==ItemKind.Hammer?.08f:.2f);Notify(kind==ItemKind.Hammer?"Вмятина выправлена.":"Крепление заменено и затянуто. Потрачен 1 болт.");return true;
        }
        void EnsureFlame()
        {
            if(flame!=null)return;
            var root=new GameObject("BurnerFlame");root.transform.SetParent(transform,false);
            var bounds=new Bounds(transform.position,Vector3.zero);foreach(var r in GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);
            root.transform.position=new Vector3(bounds.center.x,bounds.max.y+.015f,bounds.center.z);root.transform.rotation=Quaternion.Euler(-90,0,0);
            // The authored gas burner is a blowtorch: its nozzle faces local -X.
            var mesh=GetComponentInChildren<MeshFilter>();
            if(mesh!=null&&mesh.sharedMesh!=null)
            {
                var localBounds=mesh.sharedMesh.bounds;
                root.transform.position=mesh.transform.TransformPoint(new Vector3(localBounds.min.x-.008f,localBounds.max.y-.035f,localBounds.center.z));
                root.transform.rotation=Quaternion.LookRotation(mesh.transform.TransformDirection(new Vector3(-1,.3f,0)),transform.up);
            }
            flameAnchor=root.transform;
            flame=root.AddComponent<ParticleSystem>();flame.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=flame.main;main.loop=true;main.playOnAwake=false;main.startLifetime=.18f;main.startSpeed=.6f;main.startSize=.035f;main.maxParticles=50;main.startColor=new Color(.35f,.7f,1,.9f);main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=flame.emission;emission.rateOverTime=90;
            var shape=flame.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=7;shape.radius=.012f;
            var size=flame.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,.15f));
            var color=flame.colorOverLifetime;color.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(.15f,.4f,1),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});color.color=gradient;
            var renderer=flame.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=Resources.Load<Material>("EquipmentFlame");
            lamp=root.AddComponent<Light>();lamp.type=LightType.Point;lamp.range=1.5f;lamp.intensity=1.3f;lamp.color=new Color(.25f,.55f,1);lamp.enabled=false;
        }
        static void Notify(string message)=>GarageInteractionUI.Instance?.ShowNotification(message,3f);
    }
}

