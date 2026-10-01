using System.Collections.Generic;
using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Модуль оформления трассы. Процедурная генерация полностью отключена по запросу пользователя:
    /// все дороги и окружение собираются вручную на сцене.
    /// </summary>
    public sealed class FirstMapDressing : MonoBehaviour
    {
        public void DressStart(Transform parentOverride = null)
        {
            // Процедурная генерация полностью отключена
        }

        public void Dress(TrackChunk chunk, float distance, bool safe, Transform parentOverride = null)
        {
            // Процедурная генерация полностью отключена
        }
    }
}
