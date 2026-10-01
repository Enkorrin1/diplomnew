using System.Collections;
using UnityEngine;

namespace RogueDrive.Gameplay.Track
{
    /// <summary>
    /// Видимая стена бури. Объекты лежат в сцене; компонент только ставит стену на фронт
    /// CreepingStormBarrier, бьёт молниями и включает пыль вокруг камеры, когда буря накрыла машину.
    /// </summary>
    public sealed class StormFrontView : MonoBehaviour
    {
        [SerializeField] Transform wall;
        [SerializeField] Renderer[] shells;
        [SerializeField] Light flashLight;
        [SerializeField] LineRenderer[] bolts;
        [SerializeField] AudioSource thunder;
        [SerializeField] AudioClip[] thunderClips;
        [SerializeField] ParticleSystem engulfDust;
        [Header("Молнии")]
        [SerializeField] Vector2 flashInterval = new Vector2(2.5f, 7f);
        [SerializeField, Min(0)] float flashLightIntensity = 1.6f;
        [SerializeField] Vector2 boltArea = new Vector2(320, 210);
        [Header("Пыль внутри")]
        [SerializeField, Min(0)] float engulfDistance = 30f;

        static readonly int FlashId = Shader.PropertyToID("_Flash");
        MaterialPropertyBlock block;
        float flash, nextFlash;
        bool engulfed;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            nextFlash = Time.time + Random.Range(flashInterval.x, flashInterval.y);
            SetBolts(false);
            if (flashLight != null) flashLight.intensity = 0;
        }

        void LateUpdate()
        {
            var storm = CreepingStormBarrier.Instance;
            Vector3 front = default; Quaternion heading = Quaternion.identity;
            bool show = storm != null && !storm.IsSheltered && storm.TryGetFront(out front, out heading);
            if (show) wall.SetPositionAndRotation(front, heading);
            if (wall.gameObject.activeSelf != show) wall.gameObject.SetActive(show);
            UpdateEngulf(show && storm.DistanceToCar < engulfDistance);
            if (!show) return;

            if (Time.time >= nextFlash)
            {
                float urgency = storm.DistanceToCar < 250 ? .45f : 1f;
                nextFlash = Time.time + Random.Range(flashInterval.x, flashInterval.y) * urgency;
                Strike(storm.DistanceToCar);
            }
            // Затухание с мерцанием, как у настоящего разряда.
            flash = Mathf.MoveTowards(flash, 0, Time.deltaTime * 2.6f);
            float flicker = flash > .05f ? Mathf.Lerp(.55f, 1f, Mathf.PerlinNoise(Time.time * 38f, 0)) : 0;
            float lit = flash * flicker;
            block.SetFloat(FlashId, lit);
            foreach (var shell in shells) if (shell != null) shell.SetPropertyBlock(block);
            if (flashLight != null) flashLight.intensity = lit * flashLightIntensity;
            SetBolts(flash > .45f);
        }

        void Strike(float distanceToCar)
        {
            flash = 1;
            foreach (var bolt in bolts) if (bolt != null) ShapeBolt(bolt);
            if (thunder != null && thunderClips.Length > 0)
                StartCoroutine(Thunder(Mathf.Max(0, distanceToCar) / 343f, Mathf.InverseLerp(1400, 60, distanceToCar)));
        }

        // Ломаный разряд сверху вниз внутри толщи стены.
        void ShapeBolt(LineRenderer bolt)
        {
            int count = bolt.positionCount;
            float x = Random.Range(-boltArea.x, boltArea.x);
            float top = boltArea.y, bottom = Random.Range(0f, 60f);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                x += Random.Range(-14f, 14f);
                bolt.SetPosition(i, new Vector3(x, Mathf.Lerp(top, bottom, t), Random.Range(-8f, 8f)));
            }
        }

        IEnumerator Thunder(float delay, float loudness)
        {
            yield return new WaitForSeconds(delay);
            if (thunder == null) yield break;
            thunder.pitch = Random.Range(.42f, .58f);
            thunder.PlayOneShot(thunderClips[Random.Range(0, thunderClips.Length)], Mathf.Lerp(.25f, 1f, loudness));
        }

        void UpdateEngulf(bool inside)
        {
            if (engulfDust == null) return;
            var cam = Camera.main;
            if (inside && cam != null) engulfDust.transform.position = cam.transform.position + cam.transform.forward * 6f;
            if (inside == engulfed) return;
            engulfed = inside;
            if (inside) engulfDust.Play(); else engulfDust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        void SetBolts(bool on)
        {
            foreach (var bolt in bolts) if (bolt != null && bolt.enabled != on) bolt.enabled = on;
        }
    }
}
