using UnityEngine;

namespace RogueDrive.Gameplay
{
    public sealed class PrototypeCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 8f, -12f);
        [SerializeField, Min(0f)] private float followSharpness = 8f;

        public void Configure(Transform newTarget)
        {
            target = newTarget;
        }

        void LateUpdate()
        {
            if (target == null)
                return;

            float blend = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, target.position + offset, blend);
            transform.LookAt(target.position + Vector3.forward * 8f);
        }
    }
}
