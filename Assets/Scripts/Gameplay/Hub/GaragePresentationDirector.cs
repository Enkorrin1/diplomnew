using System.Collections;
using UnityEngine;
using RogueDrive.Gameplay.Narrative;

namespace RogueDrive.Gameplay.Hub
{
    // Scene-owned presentation only: inventory, quest state and driving remain in their existing owners.
    public sealed class GaragePresentationDirector : MonoBehaviour
    {
        public static GaragePresentationDirector Instance { get; private set; }
        public AudioSource generatorLoop, generatorEffects, exteriorWind, gateEffects, workshopEffects;
        public AudioClip starter, latch, metalRattle, gateMotor, installation, pour, ignition;
        public Transform generatorVisual, ventilationFan;
        public Light gateDaylight, dashboardGlow;
        public ParticleSystem gateDust;
        GaragePrologueManager manager;
        BunkerStarterCarAssembly assembly;
        Vector3 generatorOrigin;
        float nextCreak;
        bool batterySeen, departurePlayed;

        void Awake() { Instance = this; if (generatorVisual != null) generatorOrigin = generatorVisual.localPosition; }
        void Start()
        {
            manager = GaragePrologueManager.Instance;
            assembly = BunkerStarterCarAssembly.Instance;
            if (manager != null) { manager.PowerStateChanged += PowerChanged; manager.GateOpened += GateOpened; PowerChanged(manager.IsPowerOn); }
            if (assembly != null) batterySeen = assembly.IsBatteryInstalled;
            if (dashboardGlow != null) dashboardGlow.enabled = batterySeen;
            nextCreak = Time.time + 18f;
            if (exteriorWind != null) exteriorWind.Play();
        }
        void Update()
        {
            bool powered = manager != null && manager.IsPowerOn;
            if (powered && ventilationFan != null) ventilationFan.Rotate(0, 0, 170 * Time.deltaTime, Space.Self);
            if (generatorVisual != null) generatorVisual.localPosition = generatorOrigin + (powered ? Vector3.up * (Mathf.Sin(Time.time * 47f) * .0015f) : Vector3.zero);
            if (assembly != null && !batterySeen && assembly.IsBatteryInstalled)
            { batterySeen = true; if (dashboardGlow != null) dashboardGlow.enabled = true; }
            if (exteriorWind != null)
            {
                float target = manager != null && manager.IsGateOpen ? .42f : batterySeen ? .18f : .10f;
                exteriorWind.volume = Mathf.MoveTowards(exteriorWind.volume, target, Time.deltaTime * .08f);
            }
            if (Time.time > nextCreak)
            { nextCreak = Time.time + Random.Range(22f, 38f); if (gateEffects != null && metalRattle != null) gateEffects.PlayOneShot(metalRattle, .15f); }
        }
        public void CrankGenerator()
        { if (generatorEffects != null && starter != null) { generatorEffects.pitch = .72f; generatorEffects.PlayOneShot(starter, .65f); } }
        void PowerChanged(bool on)
        {
            if (generatorLoop == null) return;
            if (on) { if (!generatorLoop.isPlaying) generatorLoop.Play(); }
            else { generatorLoop.Stop(); if(generatorEffects != null) generatorEffects.Stop(); }
        }
        void GateOpened() { if (!departurePlayed) { departurePlayed = true; StartCoroutine(RevealExit()); } }
        IEnumerator RevealExit()
        {
            if (gateEffects != null && gateMotor != null) gateEffects.PlayOneShot(gateMotor, .65f);
            if (gateDust != null) gateDust.Play();
            if (gateDaylight != null)
            {
                gateDaylight.enabled = true;
                for (float t = 0; t < 3.4f; t += Time.deltaTime)
                { gateDaylight.intensity = Mathf.SmoothStep(0, 3.8f, t / 3.4f); yield return null; }
                gateDaylight.intensity = 3.8f;
            }
            RadioTransmissionSystem.Instance?.EnqueueTransmission("МАЯК / СЕКТОР 01", "Ветер усиливается. Держитесь шоссе. Следующее укрытие — СТО на окраине.", 5f);
        }
        public void PlayIgnition(Vector3 position)
        { if (workshopEffects != null && ignition != null) { workshopEffects.transform.position = position; workshopEffects.PlayOneShot(ignition, .55f); } }
        public void InstallationFeedback(VehiclePartHotspot spot, GameObject visual)
        {
            if (workshopEffects != null)
            {
                workshopEffects.transform.position = spot.transform.position;
                var clip = spot.RequiredItem == BunkerAssemblyItemType.FuelCanister ? pour : installation;
                if (clip != null) workshopEffects.PlayOneShot(clip, .6f);
            }
            if (visual != null && spot.RequiredItem != BunkerAssemblyItemType.FuelCanister) StartCoroutine(SeatPart(spot, visual.transform));
        }
        IEnumerator SeatPart(VehiclePartHotspot spot, Transform part)
        {
            Vector3 destination = part.localPosition;
            Vector3 offset = spot.RequiredItem == BunkerAssemblyItemType.Wheel ? Vector3.left * .22f : Vector3.up * .18f;
            // Convert world displacement to this model's local axes and scale.
            if (part.parent != null) offset = part.parent.InverseTransformVector(offset);
            try
            {
                for(float t=0;t<.5f;t+=Time.deltaTime)
                { if (part == null) yield break; part.localPosition = destination + offset * (1-Mathf.SmoothStep(0,1,t/.5f)); yield return null; }
            }
            finally { if (part != null) part.localPosition = destination; }
        }
        void OnDisable()
        {
            StopAllCoroutines();
            if (generatorLoop != null) generatorLoop.Stop();
            if (exteriorWind != null) exteriorWind.Stop();
            if (generatorVisual != null) generatorVisual.localPosition = generatorOrigin;
        }
        void OnDestroy()
        {
            if (manager != null) { manager.PowerStateChanged -= PowerChanged; manager.GateOpened -= GateOpened; }
            if (Instance == this) Instance = null;
        }
    }
}
