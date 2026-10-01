using System;
using System.Collections.Generic;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Hub;
using UnityEditor;
using UnityEngine;

public static class VehicleSystemsValidation
{
    [MenuItem("RogueDrive/Validate vehicle damage and fluids")]
    public static void RunMenu()=>Debug.Log(Run());

    public static string Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Run in Play Mode.");
        var car=UnityEngine.Object.FindFirstObjectByType<VehicleModularState>();
        if(car==null)throw new InvalidOperationException("No vehicle.");
        var workshop=VehicleWorkshop.For(car);
        string original=JsonUtility.ToJson(car),parts=workshop.Capture();
        var passed=new List<string>();
        void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);passed.Add(name);}
        void Reset()
        {
            workshop.Restore(parts);
            workshop.RepairPart(WorkshopSlot.Engine,1,1);
            workshop.RepairPart(WorkshopSlot.Radiator,1,1);
            JsonUtility.FromJsonOverwrite("{\"currentRadiatorWater\":10,\"currentEngineOil\":5,\"engineTemperature\":88,\"oilPanIntegrity\":1,\"bumperIntegrity\":1,\"hasCowcatcher\":false,\"currentFuelLiters\":25}",car);
        }
        try
        {
            Reset();car.SimulateSystems(60,true,1,20);
            float healthyWater=car.RadiatorWater,healthyOil=car.EngineOil;
            Check(healthyWater>9.5f&&healthyWater<10&&healthyOil>4.9f&&healthyOil<5&&car.EngineTemperature<100,"healthy 60s: small fluid use, stable heat");
            Reset();car.SimulateSystems(60,false,0,0);
            Check(car.EngineOil==5&&car.RadiatorWater==10&&car.EngineTemperature<88,"stopped healthy: no consumption, cools");
            Reset();car.ApplyComponentDamage(50,Vector3.forward);float radiator=car.RadiatorIntegrity;
            Check(radiator<.75f&&car.WaterLeakPerSecond>0,"front impact damages radiator");
            car.SimulateSystems(60,false,0,0);
            Check(car.RadiatorWater<healthyWater,"radiator leaks with engine off");
            car.AddRadiatorWater(100);Check(car.RadiatorWater==10&&car.RadiatorIntegrity==radiator,"refill cannot repair radiator");
            Reset();car.ApplyComponentDamage(50,Vector3.down);
            car.SimulateSystems(60,false,0,0);
            Check(car.OilPanIntegrity<.75f&&car.EngineOil<healthyOil,"underbody impact creates persistent oil leak");
            string damaged=JsonUtility.ToJson(car);float pan=car.OilPanIntegrity;
            car.RestoreOilPan();JsonUtility.FromJsonOverwrite(damaged,car);
            Check(Mathf.Approximately(car.OilPanIntegrity,pan),"oil pan survives serialization");
            Reset();car.AddRadiatorWater(-10);car.SimulateSystems(45,true,1,20);
            Check(car.IsOverheated&&car.EngineIntegrity<.99f&&car.EnginePowerMultiplier<.8f,"dry radiator overheats, wears motor, loses power");
            Reset();JsonUtility.FromJsonOverwrite("{\"currentEngineOil\":0}",car);
            car.SimulateSystems(75,true,1,20);
            Check(car.IsEngineSeized&&!car.CanProvidePower,"oil starvation seizes engine");
            car.AddEngineOil(5);Check(car.IsEngineSeized,"oil refill cannot undo seizure");
            string seizedParts=workshop.Capture();workshop.RepairPart(WorkshopSlot.Engine,1,1);workshop.Restore(seizedParts);
            Check(car.IsEngineSeized,"engine damage survives workshop save/restore");
            workshop.RepairPart(WorkshopSlot.Engine,1,1);Check(!car.IsEngineSeized,"workshop repair restores engine");
            Reset();workshop.DamagePart(WorkshopSlot.Radiator,.6f);car.SimulateSystems(30,true,.7f,12);
            float water=car.RadiatorWater,temp=car.EngineTemperature,oil=car.EngineOil,engine=car.EngineIntegrity;
            Reset();workshop.DamagePart(WorkshopSlot.Radiator,.6f);
            for(int i=0;i<1800;i++)car.SimulateSystems(1f/60,true,.7f,12);
            Check(Mathf.Abs(water-car.RadiatorWater)<.01f&&Mathf.Abs(temp-car.EngineTemperature)<.05f&&Mathf.Abs(oil-car.EngineOil)<.01f&&Mathf.Abs(engine-car.EngineIntegrity)<.001f,"integration consistent at 20Hz and 60Hz");
            Reset();Check(car.ApplyCollisionDamage(Vector3.forward,2.9f)==0&&car.RadiatorIntegrity==1,"parking contact below damage threshold");
            Reset();workshop.DamagePart(WorkshopSlot.Radiator,.7f);JsonUtility.FromJsonOverwrite("{\"oilPanIntegrity\":0.2,\"engineTemperature\":80,\"currentFuelLiters\":0}",car);
            var body=car.GetComponent<Rigidbody>();var velocity=body.linearVelocity;body.linearVelocity=Vector3.zero;
            try
            {
                Check(car.TryFieldRepair(out _)&&car.WaterLeakPerSecond==0&&car.OilLeakPerSecond==0,"field kit seals both leaks");
                Check(!car.TryFieldRepair(out _),"healthy vehicle does not consume repair kit");
                JsonUtility.FromJsonOverwrite("{\"engineTemperature\":110}",car);
                Check(!car.CanService(true,out _),"hot radiator blocks opening");
            }
            finally{body.linearVelocity=velocity;}
            return "PASS "+passed.Count+"\n"+string.Join("\n",passed);
        }
        finally{JsonUtility.FromJsonOverwrite(original,car);workshop.Restore(parts);}
    }
}
