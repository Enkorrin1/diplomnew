using System;
using System.Collections.Generic;
using RogueDrive.Meta;
using RogueDrive.Modifiers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.UI
{
    /// <summary>
    /// РљРѕРЅС‚СЂРѕР»Р»РµСЂ РіР»Р°РІРЅРѕРіРѕ РјРµРЅСЋ: РІРёС‚СЂРёРЅР° Р»СѓС‡С€РµРіРѕ Р°РІС‚РѕРјРѕР±РёР»СЏ РЅР° 3D-РїРѕРґРёСѓРјРµ,
    /// Р±Р°Р»Р°РЅСЃ РјРѕРЅРµС‚, СЂРµРєРѕСЂРґ РґРёСЃС‚Р°РЅС†РёРё, РїРµСЂРµС…РѕРґ РІ Р“Р°СЂР°Р¶, Р—Р°РµР·Рґ, РќР°СЃС‚СЂРѕР№РєРё Рё РёРЅС„РѕСЂРјР°С†РёСЋ Рѕ РґРёРїР»РѕРјРµ.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("3D Showcase")]
        [SerializeField] private Transform podiumAnchor;
        [SerializeField] private float rotationSpeed = 20f;
        [SerializeField] private GarageCatalog catalog;
        [SerializeField] private bool useSceneUI = true;
        [SerializeField] private GameObject[] showcaseModels;

        private MetaProgress metaProgress;
        private GameObject currentCarModel;
        private float currentAngle;

        private void Awake()
        {
            // Р—Р°С‰РёС‚Р°: MainMenuController РґРѕР»Р¶РµРЅ СЂР°Р±РѕС‚Р°С‚СЊ РўРћР›Р¬РљРћ РЅР° СЃС†РµРЅРµ РіР»Р°РІРЅРѕРіРѕ РјРµРЅСЋ
            if (!SceneManager.GetActiveScene().name.Contains("MainMenu"))
            {
                Destroy(gameObject);
                return;
            }

            Time.timeScale = 1f;
            useSceneUI = true;
            if (!useSceneUI) EnsureEnvironment();

            if (catalog == null)
            {
                catalog = Resources.Load<GarageCatalog>("GarageCatalog");
#if UNITY_EDITOR
                if (catalog == null)
                    catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<GarageCatalog>("Assets/Content/GarageCatalog.asset");
#endif
            }

            IReadOnlyList<UpgradeTrack> tracks = catalog != null ? catalog.Upgrades : Array.Empty<UpgradeTrack>();
            IReadOnlyList<CarDefinition> cars = catalog != null ? catalog.Cars : Array.Empty<CarDefinition>();
            metaProgress = SaveService.GetActiveProgress(tracks, cars);

            SetupShowcaseCar();
        }

        private void SetupShowcaseCar()
        {
            if (showcaseModels != null && showcaseModels.Length > 0 && catalog != null)
            {
                for (int i = 0; i < showcaseModels.Length; i++)
                    if (showcaseModels[i] != null) showcaseModels[i].SetActive(i < catalog.Cars.Count && catalog.Cars[i] == metaProgress.SelectedCar);
                return;
            }
            if (podiumAnchor == null)
            {
                GameObject podium = new GameObject("PodiumAnchor");
                podium.transform.position = new Vector3(0f, 0.5f, 0f);
                podiumAnchor = podium.transform;
            }

            if (catalog != null && catalog.Cars != null && catalog.Cars.Count > 0)
            {
                int selectedIndex = 0;
                string savedId = metaProgress != null && metaProgress.SelectedCar != null
                    ? metaProgress.SelectedCar.Id
                    : catalog.Cars[0].Id;

                for (int i = 0; i < catalog.Cars.Count; i++)
                {
                    if (catalog.Cars[i] != null && catalog.Cars[i].Id == savedId)
                    {
                        selectedIndex = i;
                        break;
                    }
                }

                CarDefinition def = catalog.Cars[selectedIndex];
                GameObject carPrefab = def != null ? def.EffectivePrefab : null;
                if (carPrefab != null)
                {
                    currentCarModel = Instantiate(carPrefab, podiumAnchor);
                    currentCarModel.transform.localPosition = Vector3.zero;
                    currentCarModel.transform.localRotation = Quaternion.identity;

                    // РћС‚РєР»СЋС‡Р°РµРј С„РёР·РёРєСѓ РЅР° РїРѕРґРёСѓРјРµ
                    Rigidbody rb = currentCarModel.GetComponent<Rigidbody>();
                    if (rb != null) rb.isKinematic = true;

                    // РћС‡РёС‰Р°РµРј Р»РёС€РЅРёРµ СЃРєСЂРёРїС‚С‹ СѓРїСЂР°РІР»РµРЅРёСЏ
                    MonoBehaviour[] scripts = currentCarModel.GetComponentsInChildren<MonoBehaviour>();
                    for (int i = 0; i < scripts.Length; i++)
                    {
                        if (scripts[i] != this) scripts[i].enabled = false;
                    }
                }
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.name.Contains("MainMenu"))
            {
                if (gameObject != null)
                {
                    Destroy(gameObject);
                }
            }
        }

        private void Update()
        {
            if (podiumAnchor != null)
            {
                currentAngle += rotationSpeed * Time.deltaTime;
                podiumAnchor.rotation = Quaternion.Euler(0f, currentAngle, 0f);
            }
        }

        private void EnsureEnvironment()
        {
            if (Camera.main == null && FindFirstObjectByType<Camera>() == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                Camera cam = camObj.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.04f, 0.05f, 0.08f);
                cam.fieldOfView = 50f;
                camObj.transform.position = new Vector3(0f, 2.2f, -5.5f);
                camObj.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
                camObj.tag = "MainCamera";
                camObj.AddComponent<AudioListener>();
            }

            if (FindFirstObjectByType<Light>() == null)
            {
                GameObject lightObj = new GameObject("Directional Light");
                Light dirLight = lightObj.AddComponent<Light>();
                dirLight.type = LightType.Directional;
                dirLight.color = new Color(0.85f, 0.9f, 1f);
                dirLight.intensity = 0.8f;
                lightObj.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

                GameObject spotObj = new GameObject("Podium Spotlight");
                Light spotLight = spotObj.AddComponent<Light>();
                spotLight.type = LightType.Spot;
                spotLight.color = new Color(0.2f, 0.9f, 1f);
                spotLight.intensity = 2.5f;
                spotLight.range = 15f;
                spotLight.spotAngle = 65f;
                spotObj.transform.position = new Vector3(0f, 6f, 0f);
                spotObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }

            RenderSettings.ambientLight = new Color(0.12f, 0.15f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.05f, 0.07f, 0.12f);
            RenderSettings.fogDensity = 0.035f;

            if (GameObject.Find("PodiumPedestal") == null)
            {
                GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pedestal.name = "PodiumPedestal";
                pedestal.transform.position = new Vector3(0f, -0.1f, 0f);
                pedestal.transform.localScale = new Vector3(4.5f, 0.2f, 4.5f);
                Renderer pedR = pedestal.GetComponent<Renderer>();
                if (pedR != null)
                {
                    Material m = new Material(Shader.Find("Standard"));
                    m.color = new Color(0.1f, 0.12f, 0.16f);
                    pedR.sharedMaterial = m;
                }

                GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "NeonRing";
                ring.transform.position = new Vector3(0f, -0.05f, 0f);
                ring.transform.localScale = new Vector3(4.7f, 0.05f, 4.7f);
                Renderer ringR = ring.GetComponent<Renderer>();
                if (ringR != null)
                {
                    Material rm = new Material(Shader.Find("Standard"));
                    rm.color = new Color(0f, 0.85f, 1f);
                    rm.EnableKeyword("_EMISSION");
                    rm.SetColor("_EmissionColor", new Color(0f, 0.85f, 1f) * 1.5f);
                    ringR.sharedMaterial = rm;
                }
            }
        }

        private void OnDestroy()
        {
            if (currentCarModel != null)
            {
                Destroy(currentCarModel);
            }
        }
    }
}
