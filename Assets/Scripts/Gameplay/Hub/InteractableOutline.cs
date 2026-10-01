using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RogueDrive.Gameplay.Hub
{
    /// <summary>
    /// Экранная контурная подсветка интерактивных предметов (Screen-Space Silhouette Outline).
    ///
    /// Живёт на камере игрока и работает через OnRenderImage встроенного конвейера:
    /// 1) силуэты всех рендеров цели рисуются в отдельную R8-маску (CommandBuffer, исполняемый вручную
    ///    с матрицами этой камеры), 2) полноэкранный проход ищет границу маски и подмешивает цвет контура.
    ///
    /// В отличие от CommandBuffer на CameraEvent.BeforeImageEffects, этот путь одинаково работает
    /// при включённом MSAA, HDR и в цепочке с другими пост-эффектами (CameraPostProcessEffects).
    /// Материалы объектов не трогаются, толщина контура задаётся в пикселях экрана.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("RogueDrive/Interactable Outline")]
    public sealed class InteractableOutline : MonoBehaviour
    {
        private static InteractableOutline active;

        private static readonly int MaskTexId = Shader.PropertyToID("_OutlineMaskTex");
        private static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int WidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int UseSceneDepthId = Shader.PropertyToID("_UseSceneDepth");

        [Header("Default Outline Settings")]
        [SerializeField] private Color defaultOutlineColor = new Color(0.24f, 0.90f, 1.0f, 1.0f); // Бирюзовый неоновый
        [SerializeField, Range(1.0f, 6.0f)] private float defaultOutlineWidth = 2.2f;
        [Tooltip("Обводить только видимую часть предмета (нужна depth-текстура камеры).")]
        [SerializeField] private bool occludeBySceneDepth = true;

        private Camera cam;
        private Material outlineMaterial;
        private CommandBuffer maskCommands;

        private GameObject target;
        private Color color;
        private float width;
        private readonly List<Renderer> targetRenderers = new List<Renderer>();

        /// <summary>Есть ли сейчас подсвечиваемая цель.</summary>
        public static bool HasTarget => active != null && active.target != null;

        /// <summary>Текущая подсвечиваемая цель (для отладки и тестов).</summary>
        public static GameObject CurrentTarget => active != null ? active.target : null;

        /// <summary>
        /// Привязывает подсветку к конкретной камере (камере игрока от первого лица).
        /// Вызывается интерактором; без явной привязки используется Camera.main.
        /// </summary>
        public static InteractableOutline Bind(Camera camera)
        {
            if (camera == null) return active;

            var component = camera.GetComponent<InteractableOutline>();
            if (component == null)
            {
                component = camera.gameObject.AddComponent<InteractableOutline>();
            }

            active = component;
            return component;
        }

        /// <summary>Устанавливает текущий интерактивный объект для контурной подсветки.</summary>
        public static void SetTarget(GameObject newTarget, Color outlineColor, float outlineWidth = 2.2f)
        {
            if (newTarget == null)
            {
                Clear();
                return;
            }

            InteractableOutline manager = active;
            if (manager == null)
            {
                manager = Bind(Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>());
            }

            if (manager == null) return;
            manager.Apply(newTarget, outlineColor, outlineWidth);
        }

        /// <summary>Выключает подсветку контура.</summary>
        public static void Clear()
        {
            if (active != null)
            {
                active.Apply(null, default, 0f);
            }
        }

        private void Apply(GameObject newTarget, Color newColor, float newWidth)
        {
            if (newTarget != target)
            {
                target = newTarget;
                CollectRenderers();
            }

            color = newColor;
            width = Mathf.Max(1f, newWidth);
        }

        private void CollectRenderers()
        {
            targetRenderers.Clear();
            if (target == null) return;

            target.GetComponentsInChildren(true, targetRenderers);
            targetRenderers.RemoveAll(r => r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer);
        }

        private void Awake()
        {
            cam = GetComponent<Camera>();
            EnsureResources();
            if (active == null)
            {
                active = this;
            }
        }

        private void OnEnable()
        {
            EnsureResources();
        }

        private void OnDestroy()
        {
            if (active == this)
            {
                active = null;
            }

            if (maskCommands != null)
            {
                maskCommands.Dispose();
                maskCommands = null;
            }

            if (outlineMaterial != null)
            {
                DestroyImmediate(outlineMaterial);
                outlineMaterial = null;
            }
        }

        private void EnsureResources()
        {
            if (cam == null)
            {
                cam = GetComponent<Camera>();
            }

            if (outlineMaterial == null)
            {
                Shader shader = Shader.Find("RogueDrive/InteractableOutline");
                if (shader != null)
                {
                    outlineMaterial = new Material(shader)
                    {
                        name = "ScreenSpaceOutline_RuntimeMaterial",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
                else
                {
                    Debug.LogWarning("[InteractableOutline] Шейдер RogueDrive/InteractableOutline не найден — подсветка отключена.");
                }
            }

            if (maskCommands == null)
            {
                maskCommands = new CommandBuffer { name = "Interactable_SilhouetteMask" };
            }

            if (cam != null && occludeBySceneDepth)
            {
                cam.depthTextureMode |= DepthTextureMode.Depth;
            }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (target == null || !target.activeInHierarchy || outlineMaterial == null || cam == null || targetRenderers.Count == 0)
            {
                Graphics.Blit(source, destination);
                return;
            }

            // 1. Маска силуэта в разрешении кадра
            RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8)
                ? RenderTextureFormat.R8
                : RenderTextureFormat.ARGB32;
            RenderTexture mask = RenderTexture.GetTemporary(source.width, source.height, 24, format);

            maskCommands.Clear();
            maskCommands.SetRenderTarget(mask);
            maskCommands.ClearRenderTarget(true, true, Color.black);
            maskCommands.SetViewProjectionMatrices(cam.worldToCameraMatrix, cam.projectionMatrix);
            outlineMaterial.SetFloat(UseSceneDepthId, occludeBySceneDepth ? 1f : 0f);

            bool anyRenderer = false;
            for (int i = 0; i < targetRenderers.Count; i++)
            {
                Renderer r = targetRenderers[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;

                int subMeshCount = GetSubMeshCount(r);
                for (int sub = 0; sub < subMeshCount; sub++)
                {
                    maskCommands.DrawRenderer(r, outlineMaterial, sub, 0); // Pass 0: SilhouetteMask
                }
                anyRenderer = true;
            }

            if (!anyRenderer)
            {
                RenderTexture.ReleaseTemporary(mask);
                Graphics.Blit(source, destination);
                return;
            }

            Graphics.ExecuteCommandBuffer(maskCommands);

            // 2. Композит: кадр + контур по границе маски
            outlineMaterial.SetTexture(MaskTexId, mask);
            outlineMaterial.SetColor(ColorId, color);
            outlineMaterial.SetFloat(WidthId, width);
            Graphics.Blit(source, destination, outlineMaterial, 1); // Pass 1: CompositeOutline

            RenderTexture.ReleaseTemporary(mask);
        }

        private static int GetSubMeshCount(Renderer r)
        {
            if (r is SkinnedMeshRenderer smr)
            {
                return smr.sharedMesh != null ? smr.sharedMesh.subMeshCount : 0;
            }

            var mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                return mf.sharedMesh.subMeshCount;
            }

            return 1;
        }

        #region Backwards Compatibility

        /// <summary>Подсвечивает объект, на котором висит компонент (устаревший путь через per-object компонент).</summary>
        public void SetHighlighted(bool highlight, Color? overrideColor = null, float? overrideWidth = null)
        {
            if (highlight)
            {
                SetTarget(gameObject, overrideColor ?? defaultOutlineColor, overrideWidth ?? defaultOutlineWidth);
            }
            else if (CurrentTarget == gameObject)
            {
                Clear();
            }
        }

        #endregion
    }
}
