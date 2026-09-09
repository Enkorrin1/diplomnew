using System.Collections.Generic;
using UnityEngine;

namespace RogueDrive.Gameplay
{
    /// <summary>
    /// Глобальный пул объектов для снарядов, эффектов и сфер опыта.
    /// Исключает частые вызовы Instantiate/Destroy в игровом цикле.
    /// </summary>
    public sealed class GameplayPool : MonoBehaviour
    {
        public static GameplayPool Instance { get; private set; }

        readonly Dictionary<string, Queue<GameObject>> _pools = new Dictionary<string, Queue<GameObject>>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
                return null;

            string key = prefab.name;
            if (!_pools.TryGetValue(key, out Queue<GameObject> queue))
            {
                queue = new Queue<GameObject>();
                _pools[key] = queue;
            }

            GameObject instance = null;
            while (queue.Count > 0)
            {
                instance = queue.Dequeue();
                if (instance != null)
                    break;
            }

            if (instance == null)
            {
                instance = Instantiate(prefab, position, rotation);
                instance.name = key;
            }
            else
            {
                instance.transform.position = position;
                instance.transform.rotation = rotation;
                instance.SetActive(true);
            }

            return instance;
        }

        public void Despawn(GameObject instance)
        {
            if (instance == null)
                return;

            instance.SetActive(false);
            instance.transform.SetParent(transform, false);

            string key = instance.name;
            if (!_pools.TryGetValue(key, out Queue<GameObject> queue))
            {
                queue = new Queue<GameObject>();
                _pools[key] = queue;
            }

            queue.Enqueue(instance);
        }
    }
}
