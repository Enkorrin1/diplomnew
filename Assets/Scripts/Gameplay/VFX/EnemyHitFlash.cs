using System.Collections;
using UnityEngine;

namespace RogueDrive.Gameplay.VFX
{
    /// <summary>
    /// Мгновенная белая вспышка всех рендереров врага при получении урона.
    /// Классический аркадный «hit flash» — враг на 0.08 с становится ярко-белым.
    /// </summary>
    public sealed class EnemyHitFlash : MonoBehaviour
    {
        private Renderer[] renderers;
        private MaterialPropertyBlock flashBlock;
        private MaterialPropertyBlock normalBlock;
        private bool isFlashing;

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColor = Shader.PropertyToID("_Color");

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            flashBlock = new MaterialPropertyBlock();
            normalBlock = new MaterialPropertyBlock();
        }

        /// <summary>
        /// Запускает кратковременную белую вспышку на всех рендерерах.
        /// </summary>
        public void Flash()
        {
            if (isFlashing || !gameObject.activeInHierarchy) return;
            StartCoroutine(FlashCoroutine());
        }

        private IEnumerator FlashCoroutine()
        {
            isFlashing = true;

            // Включаем яркую белую вспышку через MaterialPropertyBlock (не создаёт новые экземпляры Material)
            flashBlock.SetColor(BaseColor, Color.white);
            flashBlock.SetColor(EmissionColor, Color.white * 3f);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].gameObject.activeInHierarchy)
                {
                    renderers[i].SetPropertyBlock(flashBlock);
                }
            }

            // Ждём 0.08 реальных секунд (не зависит от timeScale)
            yield return new WaitForSecondsRealtime(0.08f);

            // Возвращаем нормальное состояние (очищаем property block)
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].gameObject.activeInHierarchy)
                {
                    renderers[i].SetPropertyBlock(null);
                }
            }

            isFlashing = false;
        }

        /// <summary>
        /// Обновляет массив рендереров (вызывается после смены визуальной модели).
        /// </summary>
        public void RefreshRenderers()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }
    }
}
