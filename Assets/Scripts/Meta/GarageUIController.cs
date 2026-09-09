using System;
using System.Collections.Generic;
using RogueDrive.Modifiers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueDrive.Meta
{
    /// <summary>
    /// Контроллер экрана Гаража:
    /// 1. Управляет 3D-подиумом и визуализацией выбранного автомобиля (вращение мышью/пальцем).
    /// 2. Отображает характеристики 4 архетипов машин (Седан, Фургон, Джип, Броневик).
    /// 3. Позволяет покупать новые машины за накопленные монеты.
    /// 4. Позволяет прокачивать 6 узлов шасси и вооружения (Двигатель, КПП, Бак, Броня, Шины, Турель).
    /// 5. Запускает боевой заезд ("В ЗАЕЗД") с сохранением всех модификаторов.
    /// </summary>
    public sealed class GarageUIController : MonoBehaviour
    {
        [Header("Каталог и данные")]
        [SerializeField] private GarageCatalog catalog;

        [Header("3D Сцена подиума")]
        [SerializeField] private Transform podiumAnchor;
        [SerializeField] private float autoRotationSpeed = 15f;

        MetaProgress _meta;
        int _selectedCarIndex;
        GameObject _currentCarModel;
        float _rotationAngle;
        bool _isDragging;
        Vector2 _lastMousePos;

        // Стили интерфейса
        GUIStyle _titleStyle;
        GUIStyle _coinsStyle;
        GUIStyle _headerStyle;
        GUIStyle _cardTitleStyle;
        GUIStyle _textStyle;
        GUIStyle _statLabelStyle;
        GUIStyle _statValueStyle;
        GUIStyle _btnPlayStyle;
        GUIStyle _badgeSelectedStyle;
        GUIStyle _cardBgStyle;

        Texture2D _darkTex;
        Texture2D _cardTex;
        Texture2D _goldTex;
        Texture2D _greenTex;
        Texture2D _accentTex;

        private void Awake()
        {
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

            // Находим индекс сохранённой выбранной машины
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
            Update3DCarVisual();
        }

        private void Update()
        {
            // Плавное вращение подиума
            if (!_isDragging)
            {
                _rotationAngle += autoRotationSpeed * Time.deltaTime;
            }

            // Ручное вращение перетаскиванием мыши/тача
            if (Input.GetMouseButtonDown(0))
            {
                // Если клик не в правой/левой панели интерфейса, захватываем вращение
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

            // Горячая клавиша старта
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                StartRun();
            }
        }

        private void OnDestroy()
        {
            if (_darkTex != null) Destroy(_darkTex);
            if (_cardTex != null) Destroy(_cardTex);
            if (_goldTex != null) Destroy(_goldTex);
            if (_greenTex != null) Destroy(_greenTex);
            if (_accentTex != null) Destroy(_accentTex);
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (_meta == null || catalog == null)
            {
                GUI.Label(new Rect(50, 50, 400, 40), "Загрузка Гаража...", _titleStyle);
                return;
            }

            // 1. Верхний бар: Заголовок и Баланс монет
            DrawHeaderBar();

            // 2. Левая панель: Выбор автомобиля и характеристики
            DrawCarSelectionPanel();

            // 3. Правая панель: 6 веток модернизации узлов
            DrawUpgradesPanel();

            // 4. Нижний бар: Кнопка старта и отладочные инструменты
            DrawBottomBar();
        }

        void DrawHeaderBar()
        {
            GUI.Box(new Rect(0, 0, Screen.width, 54), string.Empty);

            GUI.Label(new Rect(25, 12, 350, 32), "ГАРАЖ ВЫЖИВШИХ", _titleStyle);

            string statsText = $"Рекорд: {_meta.Data.BestEndlessDistance:0} м  |  Заездов: {_meta.Data.TotalRuns}";
            GUI.Label(new Rect(320, 16, 400, 26), statsText, _textStyle);

            string coinsStr = $"💰 {_meta.Coins} МОНЕТ";
            GUI.Label(new Rect(Screen.width - 260, 10, 240, 36), coinsStr, _coinsStyle);
        }

        void DrawCarSelectionPanel()
        {
            float panelW = 310f;
            float panelH = Screen.height - 150f;
            Rect panel = new Rect(20, 70, panelW, panelH);
            GUI.Box(panel, string.Empty);

            GUI.Label(new Rect(panel.x + 16, panel.y + 12, panelW - 32, 28), "АВТОПАРК", _headerStyle);

            var cars = catalog.Cars;
            float tabY = panel.y + 48;
            for (int i = 0; i < cars.Count; i++)
            {
                CarDefinition car = cars[i];
                if (car == null) continue;

                bool isCurrent = (_selectedCarIndex == i);
                bool isOwned = _meta.OwnsCar(car.Id);
                bool isEquipped = (_meta.SelectedCar != null && _meta.SelectedCar.Id == car.Id);

                string prefix = isEquipped ? "★ " : (isOwned ? "✔ " : "🔒 ");
                string btnText = $"{prefix}{car.DisplayName}";

                if (isCurrent) GUI.color = new Color(0.3f, 0.85f, 1f);
                if (GUI.Button(new Rect(panel.x + 12, tabY, panelW - 24, 34), btnText))
                {
                    _selectedCarIndex = i;
                    Update3DCarVisual();
                }
                GUI.color = Color.white;
                tabY += 38;
            }

            // Описание выбранной машины
            if (_selectedCarIndex >= 0 && _selectedCarIndex < cars.Count)
            {
                CarDefinition currentCar = cars[_selectedCarIndex];
                if (currentCar != null)
                {
                    float infoY = tabY + 12;
                    GUI.Label(new Rect(panel.x + 14, infoY, panelW - 28, 48), currentCar.Description, _textStyle);
                    infoY += 54;

                    // Параметры
                    DrawCarStat(panel.x + 14, ref infoY, "Скорость", $"{currentCar.GetStat(StatId.Speed, 20):0} м/с", currentCar.GetStat(StatId.Speed, 20) / 30f);
                    DrawCarStat(panel.x + 14, ref infoY, "Прочность (HP)", $"{currentCar.GetStat(StatId.MaxHealth, 100):0}", currentCar.GetStat(StatId.MaxHealth, 100) / 300f);
                    DrawCarStat(panel.x + 14, ref infoY, "Топливный бак", $"{currentCar.GetStat(StatId.FuelCapacity, 100):0} л", currentCar.GetStat(StatId.FuelCapacity, 100) / 200f);
                    DrawCarStat(panel.x + 14, ref infoY, "Масса тарана", $"{currentCar.GetStat(StatId.Mass, 1200):0} кг", currentCar.GetStat(StatId.Mass, 1200) / 3500f);

                    // Сокеты
                    string socketsInfo = $"Сокеты: Крыша({currentCar.GetSocketCount(SocketType.Roof)}) | Капот({currentCar.GetSocketCount(SocketType.Hood)}) | Бок({currentCar.GetSocketCount(SocketType.Side)})";
                    GUI.Label(new Rect(panel.x + 14, infoY, panelW - 28, 22), socketsInfo, _statLabelStyle);
                    infoY += 30;

                    // Статус владения / Кнопка покупки
                    bool isOwned = _meta.OwnsCar(currentCar.Id);
                    bool isEquipped = (_meta.SelectedCar != null && _meta.SelectedCar.Id == currentCar.Id);

                    float actionBtnY = panel.y + panelH - 52;
                    if (isEquipped)
                    {
                        GUI.Box(new Rect(panel.x + 14, actionBtnY, panelW - 28, 38), "ВЫБРАН ДЛЯ ЗАЕЗДА", _badgeSelectedStyle);
                    }
                    else if (isOwned)
                    {
                        if (GUI.Button(new Rect(panel.x + 14, actionBtnY, panelW - 28, 38), "ВЫБРАТЬ ЭТОТ АВТОМОБИЛЬ"))
                        {
                            _meta.SelectCar(currentCar.Id);
                            SaveService.SaveActive();
                        }
                    }
                    else
                    {
                        bool canAfford = _meta.Coins >= currentCar.Price;
                        GUI.enabled = canAfford;
                        string buyText = canAfford ? $"КУПИТЬ ЗА {currentCar.Price} МОНЕТ" : $"НЕДОСТАТОЧНО МОНЕТ ({currentCar.Price})";
                        if (GUI.Button(new Rect(panel.x + 14, actionBtnY, panelW - 28, 38), buyText))
                        {
                            if (_meta.BuyCar(currentCar, currentCar.Price))
                            {
                                _meta.SelectCar(currentCar.Id);
                                SaveService.SaveActive();
                            }
                        }
                        GUI.enabled = true;
                    }
                }
            }
        }

        void DrawCarStat(float x, ref float y, string label, string valStr, float pct)
        {
            GUI.Label(new Rect(x, y, 140, 20), label, _statLabelStyle);
            GUI.Label(new Rect(x + 180, y, 90, 20), valStr, _statValueStyle);
            y += 20;

            // Индикатор-полоска
            GUI.DrawTexture(new Rect(x, y, 270, 6), _darkTex);
            GUI.DrawTexture(new Rect(x, y, 270 * Mathf.Clamp01(pct), 6), _accentTex);
            y += 14;
        }

        void DrawUpgradesPanel()
        {
            float panelW = 380f;
            float panelH = Screen.height - 150f;
            Rect panel = new Rect(Screen.width - panelW - 20, 70, panelW, panelH);
            GUI.Box(panel, string.Empty);

            GUI.Label(new Rect(panel.x + 16, panel.y + 12, panelW - 32, 28), "МОДЕРНИЗАЦИЯ УЗЛОВ", _headerStyle);

            var upgrades = catalog.Upgrades;
            float cardY = panel.y + 46;
            float cardH = (panelH - 60) / Mathf.Max(1, upgrades.Count);

            for (int i = 0; i < upgrades.Count; i++)
            {
                UpgradeTrack track = upgrades[i];
                if (track == null) continue;

                DrawUpgradeCard(panel.x + 12, cardY, panelW - 24, cardH - 6, track);
                cardY += cardH;
            }
        }

        void DrawUpgradeCard(float x, float y, float w, float h, UpgradeTrack track)
        {
            GUI.Box(new Rect(x, y, w, h), string.Empty);

            int currentLevel = _meta.GetUpgradeLevel(track);
            bool isMax = currentLevel >= track.MaxLevel;
            int cost = track.GetCost(currentLevel);
            bool canAfford = _meta.CanBuyUpgrade(track);

            // Название и уровень в виде кубиков
            string levelPips = GetLevelPips(currentLevel, track.MaxLevel);
            GUI.Label(new Rect(x + 10, y + 6, 200, 22), track.DisplayName, _cardTitleStyle);
            GUI.Label(new Rect(x + 215, y + 6, w - 225, 22), levelPips, _statValueStyle);

            // Бонус и описание
            float bonus = track.GetBonus(currentLevel);
            string bonusStr = bonus > 0f ? $"Бонус: +{bonus:0.#}" : "Базовый уровень";
            GUI.Label(new Rect(x + 10, y + 26, w - 150, 18), bonusStr, _statLabelStyle);

            // Кнопка улучшения
            float btnW = 120f;
            float btnH = h - 16;
            Rect btnRect = new Rect(x + w - btnW - 8, y + 8, btnW, btnH);

            if (isMax)
            {
                GUI.Box(btnRect, "МАКСИМУМ", _badgeSelectedStyle);
            }
            else
            {
                GUI.enabled = canAfford;
                string btnLabel = $"УЛУЧШИТЬ\n💰 {cost}";
                if (GUI.Button(btnRect, btnLabel))
                {
                    if (_meta.BuyUpgrade(track))
                    {
                        SaveService.SaveActive();
                    }
                }
                GUI.enabled = true;
            }
        }

        string GetLevelPips(int current, int max)
        {
            char[] pips = new char[max];
            for (int i = 0; i < max; i++)
            {
                pips[i] = (i < current) ? '■' : '□';
            }
            return new string(pips) + $" {current}/{max}";
        }

        void DrawBottomBar()
        {
            float barY = Screen.height - 65;

            // Кнопка "В ЗАЕЗД" по центру
            float playW = 280f;
            float playH = 48f;
            float playX = (Screen.width - playW) * 0.5f;

            if (GUI.Button(new Rect(playX, barY, playW, playH), "В ЗАЕЗД! [ПРОБЕЛ]", _btnPlayStyle))
            {
                StartRun();
            }

            // Отладочные инструменты слева снизу
            if (GUI.Button(new Rect(20, barY + 12, 140, 28), "+500 Монет"))
            {
                _meta.AddCoins(500);
                SaveService.SaveActive();
            }

            if (GUI.Button(new Rect(165, barY + 12, 140, 28), "Сброс сейва"))
            {
                SaveService.ResetToNew(catalog.Upgrades, catalog.Cars);
                _meta = SaveService.GetActiveProgress(catalog.Upgrades, catalog.Cars);
                _selectedCarIndex = 0;
                Update3DCarVisual();
            }
        }

        public void StartRun()
        {
            // Сохраняем текущую выбранную машину
            if (catalog != null && _selectedCarIndex >= 0 && _selectedCarIndex < catalog.Cars.Count)
            {
                CarDefinition car = catalog.Cars[_selectedCarIndex];
                if (car != null && _meta.OwnsCar(car.Id))
                {
                    _meta.SelectCar(car.Id);
                }
            }
            SaveService.SaveActive();

            SceneManager.LoadScene("RogueDrivePrototype");
        }

        void Update3DCarVisual()
        {
            if (podiumAnchor == null || catalog == null || catalog.Cars.Count == 0)
                return;

            if (_currentCarModel != null)
            {
                Destroy(_currentCarModel);
            }

            CarDefinition car = (_selectedCarIndex >= 0 && _selectedCarIndex < catalog.Cars.Count)
                ? catalog.Cars[_selectedCarIndex]
                : catalog.Cars[0];

            if (car == null) return;

            _currentCarModel = new GameObject($"Podium_{car.Id}");
            _currentCarModel.transform.SetParent(podiumAnchor, false);
            _currentCarModel.transform.localPosition = Vector3.zero;

            // Построение процедурного 3D макета автомобиля выбранного типа
            BuildPodiumCarModel(_currentCarModel.transform, car);
        }

        void BuildPodiumCarModel(Transform parent, CarDefinition car)
        {
            Color carColor = car.BodyColor;

            if (car.Id == "truck")
            {
                // Фургон: массивный высокий кузов
                CreateMeshPart(parent, "Chassis", new Vector3(0f, 0.4f, 0f), new Vector3(2.0f, 0.6f, 4.0f), carColor);
                CreateMeshPart(parent, "Cabin", new Vector3(0f, 1.05f, 0.8f), new Vector3(1.9f, 0.75f, 1.6f), new Color(0.68f, 0.88f, 1f));
                CreateMeshPart(parent, "VanBody", new Vector3(0f, 1.15f, -0.9f), new Vector3(1.95f, 0.95f, 2.0f), carColor * 0.9f);
                CreateMeshPart(parent, "FrontBumper", new Vector3(0f, 0.3f, 2.05f), new Vector3(2.1f, 0.45f, 0.3f), new Color(0.2f, 0.2f, 0.2f));
            }
            else if (car.Id == "suv")
            {
                // Джип: клиренс, кенгурятник, мощные дуги
                CreateMeshPart(parent, "Chassis", new Vector3(0f, 0.5f, 0f), new Vector3(1.95f, 0.65f, 3.8f), carColor);
                CreateMeshPart(parent, "Cabin", new Vector3(0f, 1.05f, -0.2f), new Vector3(1.6f, 0.55f, 1.8f), new Color(0.68f, 0.88f, 1f));
                CreateMeshPart(parent, "BullBar", new Vector3(0f, 0.45f, 1.95f), new Vector3(2.0f, 0.7f, 0.25f), new Color(0.15f, 0.15f, 0.18f));
                CreateMeshPart(parent, "RoofRack", new Vector3(0f, 1.4f, -0.2f), new Vector3(1.5f, 0.15f, 1.5f), new Color(0.2f, 0.2f, 0.2f));
            }
            else if (car.Id == "armored")
            {
                // Броневик: угловатые скосы, бронепластины
                CreateMeshPart(parent, "Hull", new Vector3(0f, 0.6f, 0f), new Vector3(2.2f, 0.8f, 4.4f), carColor);
                CreateMeshPart(parent, "ArmorPlate_F", new Vector3(0f, 0.8f, 1.8f), new Vector3(2.1f, 0.4f, 0.8f), carColor * 0.8f);
                CreateMeshPart(parent, "Tower", new Vector3(0f, 1.25f, -0.2f), new Vector3(1.2f, 0.5f, 1.4f), new Color(0.3f, 0.3f, 0.35f));
                CreateMeshPart(parent, "HeavyPlating_L", new Vector3(-1.15f, 0.55f, 0f), new Vector3(0.2f, 0.6f, 3.5f), new Color(0.2f, 0.2f, 0.22f));
                CreateMeshPart(parent, "HeavyPlating_R", new Vector3(1.15f, 0.55f, 0f), new Vector3(0.2f, 0.6f, 3.5f), new Color(0.2f, 0.2f, 0.22f));
            }
            else
            {
                // Седан (легковая)
                CreateMeshPart(parent, "Chassis", new Vector3(0f, 0.35f, 0f), new Vector3(1.75f, 0.55f, 3.5f), carColor);
                CreateMeshPart(parent, "Cabin", new Vector3(0f, 0.85f, -0.2f), new Vector3(1.35f, 0.55f, 1.6f), new Color(0.68f, 0.88f, 1f));
                CreateMeshPart(parent, "FrontBumper", new Vector3(0f, 0.25f, 1.75f), new Vector3(1.82f, 0.35f, 0.25f), new Color(0.2f, 0.2f, 0.25f));
            }

            // Колеса
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

            // Турель на крыше
            Transform turret = CreateMeshPart(parent, "Turret_Base", new Vector3(0f, 1.25f, -0.2f), new Vector3(0.5f, 0.15f, 0.5f), new Color(0.25f, 0.28f, 0.35f));
            CreateMeshPart(turret, "Turret_Barrel", new Vector3(0f, 0.15f, 0.4f), new Vector3(0.12f, 0.12f, 0.65f), new Color(0.85f, 0.35f, 0.15f));
        }

        Transform CreateMeshPart(Transform parent, string name, Vector3 pos, Vector3 scale, Color color)
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

        void EnsureStyles()
        {
            if (_titleStyle != null)
                return;

            EnsureTextures();

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.2f) }
            };

            _coinsStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.15f) }
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _cardTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.95f, 1f) }
            };

            _textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                normal = { textColor = new Color(0.85f, 0.85f, 0.9f) }
            };

            _statLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.75f, 0.8f, 0.85f) }
            };

            _statValueStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.35f, 0.9f, 1f) }
            };

            _btnPlayStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _badgeSelectedStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.3f, 1f, 0.4f) }
            };
        }

        void EnsureTextures()
        {
            if (_darkTex != null)
                return;

            _darkTex = MakeColorTex(new Color(0.12f, 0.14f, 0.18f, 0.9f));
            _cardTex = MakeColorTex(new Color(0.16f, 0.18f, 0.24f, 0.95f));
            _goldTex = MakeColorTex(new Color(0.95f, 0.75f, 0.15f));
            _greenTex = MakeColorTex(new Color(0.2f, 0.85f, 0.35f));
            _accentTex = MakeColorTex(new Color(0.2f, 0.65f, 1f));
        }

        Texture2D MakeColorTex(Color col)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }
    }
}
