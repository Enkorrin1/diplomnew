using System;
using System.Collections.Generic;
using RogueDrive.Gameplay.VFX;
using RogueDrive.Modifiers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.Meta
{
    /// <summary>
    /// РљРѕРЅС‚СЂРѕР»Р»РµСЂ СЌРєСЂР°РЅР° Р“Р°СЂР°Р¶Р°:
    /// 1. РЈРїСЂР°РІР»СЏРµС‚ 3D-РїРѕРґРёСѓРјРѕРј Рё РІРёР·СѓР°Р»РёР·Р°С†РёРµР№ РІС‹Р±СЂР°РЅРЅРѕРіРѕ Р°РІС‚РѕРјРѕР±РёР»СЏ (РІСЂР°С‰РµРЅРёРµ РјС‹С€СЊСЋ/РїР°Р»СЊС†РµРј).
    /// 2. РћС‚РѕР±СЂР°Р¶Р°РµС‚ С…Р°СЂР°РєС‚РµСЂРёСЃС‚РёРєРё 4 Р°СЂС…РµС‚РёРїРѕРІ РјР°С€РёРЅ (РЎРµРґР°РЅ, Р¤СѓСЂРіРѕРЅ, Р”Р¶РёРї, Р‘СЂРѕРЅРµРІРёРє).
    /// 3. РџРѕР·РІРѕР»СЏРµС‚ РїРѕРєСѓРїР°С‚СЊ РЅРѕРІС‹Рµ РјР°С€РёРЅС‹ Р·Р° РЅР°РєРѕРїР»РµРЅРЅС‹Рµ РјРѕРЅРµС‚С‹.
    /// 4. РџРѕР·РІРѕР»СЏРµС‚ РѕС‚РґРµР»СЊРЅРѕ РїСЂРѕРєР°С‡РёРІР°С‚СЊ РєРѕР»С‘СЃР° Рё РїРѕРґРІРµСЃРєСѓ, Р° С‚Р°РєР¶Рµ РґСЂСѓРіРёРµ СѓР·Р»С‹ Р°РІС‚РѕРјРѕР±РёР»СЏ.
    /// 5. Р—Р°РїСѓСЃРєР°РµС‚ Р±РѕРµРІРѕР№ Р·Р°РµР·Рґ ("Р’ Р—РђР•Р—Р”") СЃ СЃРѕС…СЂР°РЅРµРЅРёРµРј РІСЃРµС… РјРѕРґРёС„РёРєР°С‚РѕСЂРѕРІ.
    /// </summary>
    public sealed class GarageUIController : MonoBehaviour
    {
        [Header("РљР°С‚Р°Р»РѕРі Рё РґР°РЅРЅС‹Рµ")]
        [SerializeField] private GarageCatalog catalog;
        [SerializeField] private bool useSceneUI = true;
        public bool UseSceneUI => useSceneUI;
        [SerializeField] private GameObject[] showcaseModels;
        public GarageCatalog Catalog => catalog;
        public MetaProgress Progress => _meta;
        public int SelectedIndex => _selectedCarIndex;
        public void BrowseCar(int index)
        {
            if (catalog == null || index < 0 || index >= catalog.Cars.Count) return;
            _selectedCarIndex = index;
            Update3DCarVisual();
        }
        public void BuyOrSelectCar()
        {
            if (_meta == null || catalog == null || _selectedCarIndex < 0 || _selectedCarIndex >= catalog.Cars.Count) return;
            var selected = catalog.Cars[_selectedCarIndex];
            if (selected == null) return;
            if (_meta.OwnsCar(selected.Id) || _meta.BuyCar(selected, selected.Price))
            {
                _meta.SelectCar(selected.Id);
                SaveService.SaveActive();
                Update3DCarVisual();
            }
        }
        public void BuyUpgradeAt(int index)
        {
            if (_meta == null || catalog == null || index < 0 || index >= catalog.Upgrades.Count) return;
            var car = (_selectedCarIndex >= 0 && _selectedCarIndex < catalog.Cars.Count) ? catalog.Cars[_selectedCarIndex] : _meta.SelectedCar;
            if (car == null || !_meta.OwnsCar(car.Id)) return;
            if (_meta.BuyUpgrade(car.Id, catalog.Upgrades[index]))
            {
                SaveService.SaveActive();
                _currentWheelVisuals?.RefreshForCurrentProgress(car.Id);
                _currentSuspensionVisuals?.RefreshForCurrentProgress(car.Id);
                _currentTuningVisuals?.RefreshTuning(car.Id, _meta);
            }
        }

        [Header("3D РЎС†РµРЅР° РїРѕРґРёСѓРјР°")]
        [SerializeField] private Transform podiumAnchor;
        [SerializeField] private float autoRotationSpeed = 15f;

        MetaProgress _meta;
        int _selectedCarIndex;
        GameObject _currentCarModel;
        CarWheelUpgradeVisuals _currentWheelVisuals;
        CarSuspensionUpgradeVisuals _currentSuspensionVisuals;
        RogueDrive.Gameplay.VFX.CarVisualTuning _currentTuningVisuals;
        float _rotationAngle;
        bool _isDragging;
        private RogueDrive.UI.SceneUIView sceneView;
        Vector2 _lastMousePos;


        private void Awake()
        {
            useSceneUI = true;
            if (catalog == null)
            {
                catalog = Resources.Load<GarageCatalog>("GarageCatalog");
                if (catalog == null)
                {
#if UNITY_EDITOR
                    catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<GarageCatalog>("Assets/Content/GarageCatalog.asset");
#endif
                }
            }

            IReadOnlyList<UpgradeTrack> tracks = catalog != null ? catalog.Upgrades : Array.Empty<UpgradeTrack>();
            IReadOnlyList<CarDefinition> cars = catalog != null ? catalog.Cars : Array.Empty<CarDefinition>();

            _meta = SaveService.GetActiveProgress(tracks, cars);

            // РќР°С…РѕРґРёРј РёРЅРґРµРєСЃ СЃРѕС…СЂР°РЅС‘РЅРЅРѕР№ РІС‹Р±СЂР°РЅРЅРѕР№ РјР°С€РёРЅС‹
            if (catalog != null && catalog.Cars.Count > 0)
            {
                string savedId = _meta.SelectedCar != null ? _meta.SelectedCar.Id : catalog.Cars[0].Id;
                for (int i = 0; i < catalog.Cars.Count; i++)
                {
                    if (catalog.Cars[i] != null && catalog.Cars[i].Id == savedId)
                    {
                        _selectedCarIndex = i;
                        break;
                    }
                }
            }
        }

        private void Start()
        {
            sceneView = FindFirstObjectByType<RogueDrive.UI.SceneUIView>();
            Update3DCarVisual();
        }

        private void Update()
        {
            if (sceneView != null && sceneView.BlocksBackgroundInput) return;

            // Р•СЃР»Рё РІ РіР°СЂР°Р¶Рµ Р°РєС‚РёРІРµРЅ СЂРµР¶РёРј РѕС‚ РїРµСЂРІРѕРіРѕ Р»РёС†Р° (GaragePlayerController),
            // РѕС‚РєР»СЋС‡Р°РµРј РїРµСЂРµС…РІР°С‚ РєР»Р°РІРёС€ РјРµРЅСЋ (WASD) Рё Р°РІС‚Рѕ-РІСЂР°С‰РµРЅРёРµ РїРѕРґРёСѓРјР°:
            if (FindFirstObjectByType<RogueDrive.Gameplay.Hub.GaragePlayerController>() != null)
            {
                return;
            }

            // РџР»Р°РІРЅРѕРµ РІСЂР°С‰РµРЅРёРµ РїРѕРґРёСѓРјР°
            if (!_isDragging)
            {
                _rotationAngle += autoRotationSpeed * Time.deltaTime;
            }

            // Р СѓС‡РЅРѕРµ РІСЂР°С‰РµРЅРёРµ РїРµСЂРµС‚Р°СЃРєРёРІР°РЅРёРµРј РјС‹С€Рё/С‚Р°С‡Р°
            if (Input.GetMouseButtonDown(0))
            {
                // Р•СЃР»Рё РєР»РёРє РЅРµ РІ РїСЂР°РІРѕР№/Р»РµРІРѕР№ РїР°РЅРµР»Рё РёРЅС‚РµСЂС„РµР№СЃР°, Р·Р°С…РІР°С‚С‹РІР°РµРј РІСЂР°С‰РµРЅРёРµ
                if (Input.mousePosition.x > 320 && Input.mousePosition.x < Screen.width - 380 && Input.mousePosition.y > 120)
                {
                    _isDragging = true;
                    _lastMousePos = Input.mousePosition;
                }
            }
            else if (Input.GetMouseButton(0) && _isDragging)
            {
                Vector2 delta = (Vector2)Input.mousePosition - _lastMousePos;
                _rotationAngle -= delta.x * 0.45f;
                _lastMousePos = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _isDragging = false;
            }

            if (podiumAnchor != null)
            {
                podiumAnchor.rotation = Quaternion.Euler(0f, _rotationAngle, 0f);
            }

            // РњРµРЅСЋ-РЅР°РІРёРіР°С†РёСЏ: РїРµСЂРµРєР»СЋС‡РµРЅРёРµ РјР°С€РёРЅ РўРћР›Р¬РљРћ СЃС‚СЂРµР»РєР°РјРё РІР»РµРІРѕ/РІРїСЂР°РІРѕ (A/D Р·Р°СЂРµР·РµСЂРІРёСЂРѕРІР°РЅС‹ РїРѕРґ РїРµСЂРµРјРµС‰РµРЅРёРµ РїРµСЂСЃРѕРЅР°Р¶Р°)
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (catalog != null && catalog.Cars.Count > 0)
                {
                    int prev = (_selectedCarIndex - 1 + catalog.Cars.Count) % catalog.Cars.Count;
                    BrowseCar(prev);
                }
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (catalog != null && catalog.Cars.Count > 0)
                {
                    int next = (_selectedCarIndex + 1) % catalog.Cars.Count;
                    BrowseCar(next);
                }
            }

            // PC: E вЂ” РІС‹Р±СЂР°С‚СЊ / РєСѓРїРёС‚СЊ РїСЂРѕСЃРјР°С‚СЂРёРІР°РµРјСѓСЋ РјР°С€РёРЅСѓ
            if (Input.GetKeyDown(KeyCode.E))
            {
                BuyOrSelectCar();
            }

            // PC: цифры 1..6 — прокачка соответствующего улучшения
            for (int k = 0; k < 6; k++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + k))
                {
                    BuyUpgradeAt(k);
                }
            }

            // Горячая клавиша старта
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                StartRun();
            }

            // ESC — возврат в Главное меню
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                RogueDrive.UI.SceneTransitionManager.SwitchScene("MainMenuScene");
            }
        }

        public void StartRun()
        {
            if (catalog != null && _selectedCarIndex >= 0 && _selectedCarIndex < catalog.Cars.Count)
            {
                CarDefinition car = catalog.Cars[_selectedCarIndex];
                if (car != null && _meta.OwnsCar(car.Id))
                {
                    _meta.SelectCar(car.Id);
                }
            }
            SaveService.SaveActive();

            int selectedSector = RogueDrive.Gameplay.CampaignMapModal.SelectedStartSector;
            if (selectedSector < 1 || selectedSector > 4)
            {
                selectedSector = _meta != null ? Mathf.Clamp(_meta.Data.HighestCampaignLevel, 1, 4) : 1;
            }
            RogueDrive.UI.SceneUIView.LoadStage(selectedSector);
        }

        public void Update3DCarVisual()
        {
            CarDefinition car = (_selectedCarIndex >= 0 && _selectedCarIndex < catalog.Cars.Count)
                ? catalog.Cars[_selectedCarIndex]
                : (catalog.Cars.Count > 0 ? catalog.Cars[0] : null);
            string carId = car != null ? car.Id : "light";

            if (showcaseModels != null && showcaseModels.Length > 0)
            {
                for (int i = 0; i < showcaseModels.Length; i++)
                    if (showcaseModels[i] != null) showcaseModels[i].SetActive(i == _selectedCarIndex);
                _currentCarModel = showcaseModels[_selectedCarIndex];
                _currentWheelVisuals = _currentCarModel != null ? _currentCarModel.GetComponent<CarWheelUpgradeVisuals>() : null;
                _currentSuspensionVisuals = _currentCarModel != null ? _currentCarModel.GetComponent<CarSuspensionUpgradeVisuals>() : null;
                _currentWheelVisuals?.RefreshForCurrentProgress(carId);
                _currentSuspensionVisuals?.RefreshForCurrentProgress(carId);
                return;
            }
            if (podiumAnchor == null || catalog == null || catalog.Cars.Count == 0)
                return;

            if (_currentCarModel != null)
            {
                Destroy(_currentCarModel);
            }
            _currentWheelVisuals = null;
            _currentSuspensionVisuals = null;

            if (car == null) return;

            GameObject carPrefab = car.EffectivePrefab;
            if (carPrefab != null)
            {
                _currentCarModel = Instantiate(carPrefab, podiumAnchor);
                _currentCarModel.transform.localPosition = Vector3.zero;
                _currentCarModel.transform.localRotation = Quaternion.identity;

                foreach (var rb in _currentCarModel.GetComponentsInChildren<Rigidbody>())
                {
                    rb.isKinematic = true;
                }
                foreach (var col in _currentCarModel.GetComponentsInChildren<Collider>())
                {
                    col.enabled = false;
                }
            }
            else
            {
                _currentCarModel = new GameObject($"Podium_{car.Id}");
                _currentCarModel.transform.SetParent(podiumAnchor, false);
                BuildPodiumCarModel(_currentCarModel.transform, car);
            }

            _currentWheelVisuals = _currentCarModel.GetComponent<CarWheelUpgradeVisuals>() ?? _currentCarModel.AddComponent<CarWheelUpgradeVisuals>();
            _currentWheelVisuals.RefreshForCurrentProgress(carId);
            _currentSuspensionVisuals = _currentCarModel.GetComponent<CarSuspensionUpgradeVisuals>() ?? _currentCarModel.AddComponent<CarSuspensionUpgradeVisuals>();
            _currentSuspensionVisuals.RefreshForCurrentProgress(carId);
            _currentTuningVisuals = _currentCarModel.GetComponent<RogueDrive.Gameplay.VFX.CarVisualTuning>() ?? _currentCarModel.AddComponent<RogueDrive.Gameplay.VFX.CarVisualTuning>();
            _currentTuningVisuals.RefreshTuning(carId, _meta);
        }

        private void BuildPodiumCarModel(Transform parent, CarDefinition car)
        {
            Color carColor = car.BodyColor;

            if (car.Id == "truck")
            {
                CreateMeshPart(parent, "Chassis", new Vector3(0f, 0.4f, 0f), new Vector3(2.0f, 0.6f, 4.0f), carColor);
                CreateMeshPart(parent, "Cabin", new Vector3(0f, 1.05f, 0.8f), new Vector3(1.9f, 0.75f, 1.6f), new Color(0.68f, 0.88f, 1f));
                CreateMeshPart(parent, "VanBody", new Vector3(0f, 1.15f, -0.9f), new Vector3(1.95f, 0.95f, 2.0f), carColor * 0.9f);
                CreateMeshPart(parent, "FrontBumper", new Vector3(0f, 0.3f, 2.05f), new Vector3(2.1f, 0.45f, 0.3f), new Color(0.2f, 0.2f, 0.2f));
            }
            else if (car.Id == "suv")
            {
                CreateMeshPart(parent, "Chassis", new Vector3(0f, 0.5f, 0f), new Vector3(1.95f, 0.65f, 3.8f), carColor);
                CreateMeshPart(parent, "Cabin", new Vector3(0f, 1.05f, -0.2f), new Vector3(1.6f, 0.55f, 1.8f), new Color(0.68f, 0.88f, 1f));
                CreateMeshPart(parent, "BullBar", new Vector3(0f, 0.45f, 1.95f), new Vector3(2.0f, 0.7f, 0.25f), new Color(0.15f, 0.15f, 0.18f));
                CreateMeshPart(parent, "RoofRack", new Vector3(0f, 1.4f, -0.2f), new Vector3(1.5f, 0.15f, 1.5f), new Color(0.2f, 0.2f, 0.2f));
            }
            else if (car.Id == "armored")
            {
                CreateMeshPart(parent, "Hull", new Vector3(0f, 0.6f, 0f), new Vector3(2.2f, 0.8f, 4.4f), carColor);
                CreateMeshPart(parent, "ArmorPlate_F", new Vector3(0f, 0.8f, 1.8f), new Vector3(2.1f, 0.4f, 0.8f), carColor * 0.8f);
                CreateMeshPart(parent, "Tower", new Vector3(0f, 1.25f, -0.2f), new Vector3(1.2f, 0.5f, 1.4f), new Color(0.3f, 0.3f, 0.35f));
                CreateMeshPart(parent, "HeavyPlating_L", new Vector3(-1.15f, 0.55f, 0f), new Vector3(0.2f, 0.6f, 3.5f), new Color(0.2f, 0.2f, 0.22f));
                CreateMeshPart(parent, "HeavyPlating_R", new Vector3(1.15f, 0.55f, 0f), new Vector3(0.2f, 0.6f, 3.5f), new Color(0.2f, 0.2f, 0.22f));
            }
            else
            {
                CreateMeshPart(parent, "Chassis", new Vector3(0f, 0.35f, 0f), new Vector3(1.75f, 0.55f, 3.5f), carColor);
                CreateMeshPart(parent, "Cabin", new Vector3(0f, 0.85f, -0.2f), new Vector3(1.35f, 0.55f, 1.6f), new Color(0.68f, 0.88f, 1f));
                CreateMeshPart(parent, "FrontBumper", new Vector3(0f, 0.25f, 1.75f), new Vector3(1.82f, 0.35f, 0.25f), new Color(0.2f, 0.2f, 0.25f));
            }

            Vector3[] wheelOffsets =
            {
                new Vector3(-0.95f, 0.25f, 1.15f),
                new Vector3(0.95f, 0.25f, 1.15f),
                new Vector3(-0.95f, 0.25f, -1.15f),
                new Vector3(0.95f, 0.25f, -1.15f)
            };
            for (int i = 0; i < wheelOffsets.Length; i++)
            {
                CreateMeshPart(parent, $"Wheel_{i + 1}", wheelOffsets[i], new Vector3(0.25f, 0.58f, 0.58f), new Color(0.1f, 0.1f, 0.12f));
            }

            Transform turret = CreateMeshPart(parent, "Turret_Base", new Vector3(0f, 1.25f, -0.2f), new Vector3(0.5f, 0.15f, 0.5f), new Color(0.25f, 0.28f, 0.35f));
            CreateMeshPart(turret, "Turret_Barrel", new Vector3(0f, 0.15f, 0.4f), new Vector3(0.12f, 0.12f, 0.65f), new Color(0.85f, 0.35f, 0.15f));
        }

        private Transform CreateMeshPart(Transform parent, string name, Vector3 pos, Vector3 scale, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            Collider c = part.GetComponent<Collider>();
            if (c != null) Destroy(c);

            part.transform.SetParent(parent, false);
            part.transform.localPosition = pos;
            part.transform.localScale = scale;

            Renderer r = part.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color"));
                m.color = color;
                r.sharedMaterial = m;
            }

            return part.transform;
        }
    }
}
