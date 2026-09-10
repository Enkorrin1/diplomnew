using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RogueDrive.Gameplay;
using RogueDrive.Gameplay.Combat;
using RogueDrive.Gameplay.UI;
using RogueDrive.Meta;
using RogueDrive.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RogueDrive.EditorTools
{
    public static class SceneAuthoringMigration
    {
        static readonly Color Background = new Color(.045f,.06f,.085f,.97f);
        static readonly Color Accent = new Color(.15f,.46f,.40f,1);
        static Font font;
        static SceneUIView view;
        static Transform canvasRoot;

        [MenuItem("RogueDrive/Scene Authoring/Migrate All Scenes")]
        public static void MigrateAll()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before migration.");
            // Save pending author edits before opening the next scene.
            EditorSceneManager.SaveOpenScenes();
            foreach (string name in new[]{"MainMenuScene", "GarageScene", "Stage1_Outskirts"})
            {
                var scene = EditorSceneManager.OpenScene($"Assets/Scenes/{name}.unity");
                if (scene.GetRootGameObjects().Any(x => x.GetComponentInChildren<SceneUIView>(true) != null)) continue;
                Build(name);
                PersistGeneratedAssets(scene.GetRootGameObjects());
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenuScene.unity");
            AssetDatabase.SaveAssets();
        }

        static void Build(string sceneName)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var catalog = AssetDatabase.LoadAssetAtPath<GarageCatalog>("Assets/Content/GarageCatalog.asset");
            var canvas = new GameObject("UI_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvas, "Create editable UI");
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280,720); scaler.matchWidthOrHeight = .5f;
            var safe = new GameObject("SafeArea", typeof(RectTransform)); safe.transform.SetParent(canvas.transform,false);
            var safeRect = safe.GetComponent<RectTransform>(); safeRect.anchorMin = Vector2.zero; safeRect.anchorMax = Vector2.one; safeRect.offsetMin = safeRect.offsetMax = Vector2.zero;
            canvasRoot = safe.transform;
            view = canvas.AddComponent<SceneUIView>(); Set(view,"catalog",catalog);
            const string settingsPath = "Assets/Content/GameSettingsDefaults.asset";
            var defaults = AssetDatabase.LoadAssetAtPath<GameSettingsDefaults>(settingsPath);
            if (defaults == null) { defaults = ScriptableObject.CreateInstance<GameSettingsDefaults>(); AssetDatabase.CreateAsset(defaults,settingsPath); }
            Set(view,"defaults",defaults);
            if (Object.FindFirstObjectByType<EventSystem>() == null) new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));

            var main = Object.FindFirstObjectByType<MainMenuController>();
            var garage = Object.FindFirstObjectByType<GarageUIController>();
            if (main != null)
            {
                Flag(main); Set(main,"catalog",catalog);
                typeof(MainMenuController).GetMethod("EnsureEnvironment",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(main,null);
                BakeShowcase(main,catalog);
            }
            if (garage != null) { Flag(garage); Set(garage,"catalog",catalog); Set(view,"garage",garage); BakeShowcase(garage,catalog); }

            if (sceneName == "MainMenuScene") BuildMain();
            else if (sceneName == "GarageScene") BuildGarage(garage,catalog);
            else BuildRun();
            BuildSettings(); BuildAbout(); BuildCampaign();
            var campaign = Object.FindFirstObjectByType<CampaignMapModal>(); if (campaign != null) Flag(campaign);
            // Every hidden panel remains an authored object, selectable while outside Play mode.
        }

        [MenuItem("RogueDrive/Scene Authoring/Finalize Scene Editing")]
        public static void FinalizeScenes()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            EditorSceneManager.SaveOpenScenes();
            foreach(string name in new[]{"MainMenuScene","GarageScene","Stage1_Outskirts"})
            {
                var scene=EditorSceneManager.OpenScene($"Assets/Scenes/{name}.unity");
                var canvas=Object.FindFirstObjectByType<SceneUIView>(); if(canvas==null)throw new Exception("Missing scene UI: "+name);
                Transform safe=canvas.transform.Find("SafeArea");
                foreach(var slider in canvas.GetComponentsInChildren<Slider>(true))
                {
                    if(slider.fillRect!=null){slider.fillRect.anchorMin=Vector2.zero;slider.fillRect.anchorMax=Vector2.one;slider.fillRect.offsetMin=slider.fillRect.offsetMax=Vector2.zero;}
                    if(slider.handleRect!=null){slider.handleRect.anchorMin=slider.handleRect.anchorMax=new Vector2(0,.5f);slider.handleRect.pivot=new Vector2(.5f,.5f);slider.handleRect.anchoredPosition=Vector2.zero;slider.handleRect.sizeDelta=new Vector2(28,12);}
                    slider.SetValueWithoutNotify(slider.value);
                }
                foreach(string panelName in new[]{"Settings","About","Campaign","Pause","RunResults","LevelUp"})
                {
                    var panel=safe.Find(panelName) as RectTransform;if(panel==null||panel.Find("ModalBlocker")!=null)continue;
                    var blocker=Rect(panel,"ModalBlocker",-panel.anchoredPosition.x,panel.anchoredPosition.y,1280,720);var img=blocker.gameObject.AddComponent<Image>();img.color=new Color(0,0,0,.7f);blocker.SetAsFirstSibling();
                }
                var transition=Object.FindFirstObjectByType<SceneTransitionManager>();
                if(transition==null)transition=new GameObject("SceneTransitionManager").AddComponent<SceneTransitionManager>();
                if(transition.transform.Find("TransitionCanvas")==null)
                {
                    var fadeCanvas=new GameObject("TransitionCanvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));fadeCanvas.transform.SetParent(transition.transform,false);
                    var c=fadeCanvas.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=30000;
                    var overlay=Rect(fadeCanvas.transform,"FadeOverlay",0,0,0,0);overlay.anchorMin=Vector2.zero;overlay.anchorMax=Vector2.one;overlay.offsetMin=overlay.offsetMax=Vector2.zero;
                    var image=overlay.gameObject.AddComponent<Image>();image.color=Color.clear;image.raycastTarget=false;Set(transition,"fadeOverlay",image);
                }
                var garage=Object.FindFirstObjectByType<GarageUIController>();
                if(garage!=null)
                {
                    var so=new SerializedObject(garage);var models=so.FindProperty("showcaseModels");
                    for(int i=0;i<models.arraySize;i++)
                    {
                        var model=models.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;if(model==null)continue;
                        var wheels=model.GetComponent<RogueDrive.Gameplay.VFX.CarWheelUpgradeVisuals>()??model.AddComponent<RogueDrive.Gameplay.VFX.CarWheelUpgradeVisuals>();wheels.Configure(garage.Catalog.WheelUpgradePrefabs);
                        if(model.GetComponent<RogueDrive.Gameplay.VFX.CarSuspensionUpgradeVisuals>()==null)model.AddComponent<RogueDrive.Gameplay.VFX.CarSuspensionUpgradeVisuals>();
                    }
                    var panel=safe.Find("Garage/VehicleSelection");
                    for(int i=0;i<garage.Catalog.Cars.Count;i++)
                    {
                        var r=panel.Find($"Car_{i+1}") as RectTransform;if(r==null)continue;r.anchoredPosition=new Vector2(14,-(14+i*48));r.sizeDelta=new Vector2(294,44);
                        var label=r.GetComponentInChildren<Text>();label.fontSize=17;label.rectTransform.sizeDelta=new Vector2(274,34);
                    }
                    var info=panel.Find("VehicleStats").GetComponent<Text>();info.rectTransform.anchoredPosition=new Vector2(16,-306);info.rectTransform.sizeDelta=new Vector2(290,145);info.fontSize=16;
                }
                PersistGeneratedAssets(scene.GetRootGameObjects());EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            BuildSettingsAutoSync.SyncScenes();AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenuScene.unity");
        }

        static void BuildMain()
        {
            var panel = Panel("MainMenu",30,110,380,535); Set(view,"mainPanel",panel.gameObject);
            Label(panel,"Title","ROGUE DRIVE",24,16,332,60,36);
            Label(panel,"Subtitle","SURVIVAL",24,72,332,35,20);
            Action(panel,"StartRun","В ЗАЕЗД",24,135,332,58,0);
            Action(panel,"Garage","ГАРАЖ И АВТОПАРК",24,207,332,58,1);
            Action(panel,"Campaign","КАМПАНИЯ",24,279,332,58,6);
            Action(panel,"Settings","НАСТРОЙКИ",24,351,332,58,2);
            Action(panel,"About","ОБ ИГРЕ",24,423,160,54,3);
            Action(panel,"Quit","ВЫХОД",196,423,160,54,4);
            Set(view,"wallet",Label(canvasRoot,"Wallet","МОНЕТЫ 0",640,24,610,52,22));
        }

        static void BuildGarage(GarageUIController garage, GarageCatalog catalog)
        {
            var panel = Panel("Garage",0,0,1280,720,new Color(0,0,0,0)); Set(view,"garagePanel",panel.gameObject);
            Action(panel,"MainMenu","В МЕНЮ",24,18,160,52,5);
            Label(panel,"Title","ГАРАЖ ВЫЖИВШИХ",204,18,410,52,26);
            Set(view,"wallet",Label(panel,"Wallet","МОНЕТЫ 0",670,18,585,52,20));
            var left = Panel("VehicleSelection",24,90,322,526,Background,panel);
            var cars = new List<Object>();
            for(int i=0;i<catalog.Cars.Count;i++)
            {
                var b = Button(left,$"Car_{i+1}",catalog.Cars[i].DisplayName,14,14+i*54,294,48);
                UnityEventTools.AddIntPersistentListener(b.onClick,garage.BrowseCar,i); cars.Add(b);
            }
            SetArray(view,"carButtons",cars);
            Set(view,"carInfo",Label(left,"VehicleStats","Характеристики автомобиля",16,242,290,198,18));
            var buy = Button(left,"BuyOrSelect","ВЫБРАТЬ",14,462,294,50); UnityEventTools.AddPersistentListener(buy.onClick,garage.BuyOrSelectCar);
            Set(view,"buyCarButton",buy); Set(view,"buyCarLabel",buy.GetComponentInChildren<Text>());
            var right = Panel("Upgrades",895,90,360,526);
            Label(right,"Title","УЛУЧШЕНИЯ АВТОМОБИЛЯ",14,6,332,36,21);
            var upgrades = new List<Object>(); var labels = new List<Object>();
            float height = Mathf.Min(72,(470f / Mathf.Max(1,catalog.Upgrades.Count)));
            for(int i=0;i<catalog.Upgrades.Count;i++)
            {
                var b=Button(right,$"Upgrade_{catalog.Upgrades[i].name}",catalog.Upgrades[i].DisplayName,14,48+i*height,332,height-5);
                b.GetComponentInChildren<Text>().fontSize = 16;
                UnityEventTools.AddIntPersistentListener(b.onClick,garage.BuyUpgradeAt,i); upgrades.Add(b); labels.Add(b.GetComponentInChildren<Text>());
            }
            SetArray(view,"upgradeButtons",upgrades); SetArray(view,"upgradeLabels",labels);
            Action(panel,"Campaign","КАРТА КАМПАНИИ",350,643,260,54,6);
            Action(panel,"StartRun","В ЗАЕЗД",630,643,260,54,0);
            Action(panel,"Settings","НАСТРОЙКИ",24,643,250,54,2);
        }

        static void BuildRun()
        {
            var run=Object.FindFirstObjectByType<GameRunController>(); var car=Object.FindFirstObjectByType<ArcadeCarController>();
            var hud=Object.FindFirstObjectByType<PrototypeHud>(); var level=Object.FindFirstObjectByType<LevelUpView>();
            var pause=Object.FindFirstObjectByType<PauseMenuUI>() ?? new GameObject("PauseController").AddComponent<PauseMenuUI>();
            var combo=Object.FindFirstObjectByType<ComboScoreSystem>() ?? new GameObject("ComboController").AddComponent<ComboScoreSystem>();
            foreach(var c in new MonoBehaviour[]{hud,level,pause,combo}) if(c!=null) Flag(c);
            Set(view,"run",run); Set(view,"car",car); Set(view,"hud",hud); Set(view,"levelUp",level); Set(view,"pause",pause); Set(view,"combo",combo);
            Set(view,"experience",Object.FindFirstObjectByType<RunExperienceManager>());
            var h=Panel("HUD",20,18,525,185); Set(view,"hudPanel",h.gameObject);
            Set(view,"hudText",Label(h,"Telemetry","HP / ТОПЛИВО / НИТРО\nДИСТАНЦИЯ\nЛОКАЦИЯ",14,10,490,82,19));
            Set(view,"healthBar",Bar(h,"Health",14,102,156,14,new Color(.3f,.8f,.4f)));
            Set(view,"fuelBar",Bar(h,"Fuel",184,102,156,14,new Color(.95f,.7f,.2f)));
            Set(view,"nitroBar",Bar(h,"Nitro",354,102,156,14,new Color(.25f,.65f,1)));
            Set(view,"xpBar",Bar(h,"Experience",14,164,496,8,new Color(.6f,.4f,1)));
            Set(view,"xpText",Label(h,"ExperienceLabel","УРОВЕНЬ 1",14,127,490,28,16));
            Action(canvasRoot,"PauseButton","ПАУЗА",1115,20,140,54,9);
            Set(view,"bannerText",Label(canvasRoot,"BiomeBanner","",570,95,660,95,23));
            Set(view,"comboText",Label(canvasRoot,"ComboBanner","",440,565,410,60,24));
            Set(view,"bossText",Label(canvasRoot,"BossLabel","",560,205,620,65,20));
            Set(view,"bossBar",Bar(canvasRoot,"BossHealth",565,275,610,14,Color.red));
            var p=Modal("Pause",410,470); Set(view,"pausePanel",p.gameObject); Label(p,"Title","ПАУЗА",20,20,370,50,32);
            Action(p,"Resume","ПРОДОЛЖИТЬ",30,90,350,54,10); Action(p,"Restart","НАЧАТЬ ЗАНОВО",30,160,350,54,11);
            Action(p,"Settings","НАСТРОЙКИ",30,230,350,54,2); Action(p,"Garage","В ГАРАЖ",30,300,350,54,1); Action(p,"MainMenu","ГЛАВНОЕ МЕНЮ",30,370,350,54,5); p.gameObject.SetActive(false);
            var results=Modal("RunResults",560,460); Set(view,"resultsPanel",results.gameObject);
            Set(view,"resultText",Label(results,"Summary","ЗАЕЗД ЗАВЕРШЁН\nДистанция\nМонеты",30,25,500,280,25));
            Action(results,"Garage","В ГАРАЖ",30,340,240,62,1); Action(results,"Retry","ЕЩЁ ЗАЕЗД",290,340,240,62,11); results.gameObject.SetActive(false);
            var l=Modal("LevelUp",1040,500); Set(view,"levelPanel",l.gameObject); Label(l,"Title","НОВЫЙ УРОВЕНЬ — ВЫБЕРИТЕ УСИЛЕНИЕ",25,15,990,50,27);
            var offers=new List<Object>(); var texts=new List<Object>();
            for(int i=0;i<3;i++) { var b=Button(l,$"Offer_{i+1}","МОДИФИКАТОР\nОписание\nВЫБРАТЬ",25+i*335,80,320,310); UnityEventTools.AddIntPersistentListener(b.onClick,view.SelectOffer,i); offers.Add(b); texts.Add(b.GetComponentInChildren<Text>()); }
            SetArray(view,"offerButtons",offers); SetArray(view,"offerLabels",texts); Set(view,"rerollButton",Action(l,"Reroll","ОБНОВИТЬ ПРЕДЛОЖЕНИЯ",350,420,340,54,12)); l.gameObject.SetActive(false);
        }

        static void BuildSettings()
        {
            var panel=Modal("Settings",670,485); Set(view,"settingsPanel",panel.gameObject);
            Label(panel,"Title","НАСТРОЙКИ",25,18,620,55,30);
            string[] titles={"Общая громкость","Музыка","Звуковые эффекты","Чувствительность руля"}; string[] fields={"masterSlider","musicSlider","effectsSlider","steeringSlider"};
            for(int i=0;i<4;i++) { Label(panel,fields[i]+"Label",titles[i],30,92+i*62,270,48,20); var s=Bar(panel,fields[i],312,102+i*62,255,28,Accent,true); if(i==3){s.minValue=.5f;s.maxValue=2;s.value=1;} Set(view,fields[i],s); }
            var values=Label(panel,"Values","85%\n75%\n90%\n1.0×",585,100,70,245,20); values.lineSpacing=2.4f; Set(view,"settingsValues",values);
            Action(panel,"Save","СОХРАНИТЬ",30,394,290,60,8); Action(panel,"Back","НАЗАД",350,394,290,60,7); panel.gameObject.SetActive(false);
        }
        static void BuildAbout()
        {
            var panel=Modal("About",670,435); Set(view,"aboutPanel",panel.gameObject);
            Label(panel,"Title","ROGUE DRIVE: SURVIVAL",30,25,610,55,28);
            Label(panel,"Description","Безымянный выживший пробивается к безопасному городу.\n\nПроходите маршрут, улучшайте автомобиль и выбирайте усиления в каждом заезде.\n\nДипломный проект • БГУИР",30,100,610,230,22);
            Action(panel,"Close","ЗАКРЫТЬ",180,350,310,56,7); panel.gameObject.SetActive(false);
        }
        static void BuildCampaign()
        {
            var panel=Modal("Campaign",940,570); Set(view,"campaignPanel",panel.gameObject);
            Label(panel,"Title","МАРШРУТ К ЭВАКУАЦИИ",25,15,890,55,30);
            string[] names={"01 • ОКРАИНА ГОРОДА","02 • ПУСТОШЬ","03 • ПРОМЫШЛЕННАЯ ЗОНА","04 • БЕЗОПАСНЫЙ ГОРОД","БЕСКОНЕЧНЫЙ ЗАЕЗД"};
            var buttons=new List<Object>();
            for(int i=0;i<5;i++) { var b=Button(panel,$"Sector_{i+1}",names[i],25+(i%2)*455,90+(i/2)*115,i==4?890:435,96); UnityEventTools.AddIntPersistentListener(b.onClick,view.SelectSector,i+1); buttons.Add(b); }
            SetArray(view,"sectorButtons",buttons); Action(panel,"Close","ЗАКРЫТЬ",320,495,300,52,7); panel.gameObject.SetActive(false);
        }

        static void BakeShowcase(MonoBehaviour controller, GarageCatalog catalog)
        {
            var serialized=new SerializedObject(controller); var anchor=serialized.FindProperty("podiumAnchor").objectReferenceValue as Transform;
            if(anchor==null) { anchor=new GameObject("PodiumAnchor").transform; anchor.position=new Vector3(0,.5f,0); Set(controller,"podiumAnchor",anchor); }
            var models=new List<Object>();
            for(int i=0;i<catalog.Cars.Count;i++)
            {
                var prefab=catalog.Cars[i].EffectivePrefab;
                if(prefab==null) throw new InvalidOperationException("Missing car prefab: "+catalog.Cars[i].name);
                var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab,anchor); model.name="Showcase_"+catalog.Cars[i].Id;
                model.transform.localPosition=Vector3.zero; model.transform.localRotation=Quaternion.identity;
                foreach(var rb in model.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic=true;
                foreach(var col in model.GetComponentsInChildren<Collider>(true)) col.enabled=false;
                foreach(var script in model.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled=false;
                model.SetActive(i==0); models.Add(model);
            }
            SetArray(controller,"showcaseModels",models);
        }

        static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {
            var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false); var r=go.GetComponent<RectTransform>();
            r.anchorMin=r.anchorMax=new Vector2(0,1); r.pivot=new Vector2(0,1); r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(w,h); return r;
        }
        static RectTransform Panel(string name,float x,float y,float w,float h,Color? color=null,Transform parent=null)
        {
            var r=Rect(parent??canvasRoot,name,x,y,w,h); var image=r.gameObject.AddComponent<Image>(); image.color=color??Background; image.raycastTarget=image.color.a>0; return r;
        }
        static RectTransform Modal(string name,float w,float h) => Panel(name,(1280-w)/2,(720-h)/2,w,h);
        static Text Label(Transform parent,string name,string content,float x,float y,float w,float h,int size=20)
        {
            var t=Rect(parent,name,x,y,w,h).gameObject.AddComponent<Text>(); t.font=font; t.text=content; t.fontSize=size; t.color=new Color(.92f,.94f,.92f); t.raycastTarget=false; t.supportRichText=true; t.verticalOverflow=VerticalWrapMode.Truncate; return t;
        }
        static Button Button(Transform parent,string name,string title,float x,float y,float w,float h)
        {
            var r=Panel(name,x,y,w,h,Accent,parent); var b=r.gameObject.AddComponent<Button>(); b.targetGraphic=r.GetComponent<Image>(); var colors=b.colors; colors.disabledColor=new Color(.35f,.4f,.4f,.8f); b.colors=colors;
            var t=Label(r,"Label",title,10,5,w-20,h-10,20); t.alignment=TextAnchor.MiddleCenter; return b;
        }
        static Button Action(Transform parent,string name,string title,float x,float y,float w,float h,int action)
        {
            var b=Button(parent,name,title,x,y,w,h); UnityEventTools.AddIntPersistentListener(b.onClick,view.Navigate,action); return b;
        }
        static Slider Bar(Transform parent,string name,float x,float y,float w,float h,Color color,bool interactive=false)
        {
            var r=Panel(name,x,y,w,h,new Color(.15f,.19f,.22f),parent); var s=r.gameObject.AddComponent<Slider>(); s.minValue=0; s.maxValue=1; s.value=.75f; s.interactable=interactive;
            var fill=Panel("Fill",0,0,w,h,color,r); fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.offsetMin=fill.offsetMax=Vector2.zero; s.fillRect=fill; s.targetGraphic=fill.GetComponent<Image>();
            if(interactive) { var handle=Panel("Handle",0,0,28,h+12,Color.white,r); s.handleRect=handle; s.targetGraphic=handle.GetComponent<Image>(); }
            return s;
        }
        static void Flag(Object target) { var so=new SerializedObject(target); var p=so.FindProperty("useSceneUI"); if(p!=null){p.boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();} }
        static void Set(Object target,string field,Object value) { var so=new SerializedObject(target); var p=so.FindProperty(field); if(p==null)throw new Exception(target.name+" missing "+field); p.objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo(); }
        static void SetArray(Object target,string field,IList<Object> values) { var so=new SerializedObject(target); var p=so.FindProperty(field); p.arraySize=values.Count;for(int i=0;i<values.Count;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo(); }

        public static void PersistGeneratedAssets(GameObject[] roots)
        {
            const string folder="Assets/Content/SceneMaterials"; System.IO.Directory.CreateDirectory(folder);
            var persisted=new HashSet<Object>();
            foreach(var root in roots)
            {
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                    foreach(var material in renderer.sharedMaterials)
                        if(material!=null && !EditorUtility.IsPersistent(material) && persisted.Add(material)) AssetDatabase.CreateAsset(material,AssetDatabase.GenerateUniqueAssetPath(folder+"/Material.mat"));
                foreach(var collider in root.GetComponentsInChildren<MeshCollider>(true))
                    if(collider.sharedMesh!=null && !EditorUtility.IsPersistent(collider.sharedMesh) && persisted.Add(collider.sharedMesh)) AssetDatabase.CreateAsset(collider.sharedMesh,AssetDatabase.GenerateUniqueAssetPath(folder+"/RoadCollision.asset"));
            }
        }
    }
}
