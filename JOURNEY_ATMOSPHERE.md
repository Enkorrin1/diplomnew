# Stage 1 — afternoon landscape

Saved in `Assets/Scenes/Stage1_Outskirts.unity`, 2026-09-25.

- Low warm sun, cool ambient light, animated cloud sky, distance haze; no vignette or full day/night cycle.
- Atmosphere cools gradually over 15 minutes of elapsed play time. The actual storm gap can strengthen the effect. Travel distance does not drive the lighting timeline.
- Road elevation 0–45 m, smooth ramps and flat plateaus around stops. Main route length is now 15,289.84 m (about 1.44 m longer after elevation).
- Raised side hills, collidable distant landscape and scattered crest trees. Existing six side loops and the roadside northern workshop remain accessible.
- Old terrain ribbon winding corrected on inner bends; outer terrain blends into the broad horizon grid. Plants and rocks seated against the final terrain triangles.
- Imported prefab geometry/materials were not edited by this pass. Source road builder now respects route elevation.

## Validation

- Compiled in Unity 6000.0.57f1; final console error query returned zero entries. Journey sky shader reported no compiler messages.
- Play Mode: 4,581 road samples (10 m intervals, lateral offsets −4/0/+4 m): no road collision holes and no distant terrain over the road. Maximum sampled road grade approximately 4%.
- Six side loops: 366 samples, no missing ground or blocking collision boxes.
- Northern workshop: safe zone at raised position; route beyond the stop is outside that zone.
- Visually inspected city, birch forest, pine forest and rocky uplands. Verified actual driving camera at the pine section, plus a forced 85% storm lighting preview. Preview values and car teleport were runtime-only and discarded on leaving Play Mode.
- Continuous 15–20 minute drive and performance profiling of the complete journey remain untested.

Screenshots: `Temp/Atmosphere/driving-afternoon-final.png`, `birch-afternoon.png`, `uplands-final.png`, `storm-lighting-preview.png`.

## Authoring

`JourneyAtmosphereAuthoring.Build` authors the initial elevation and atmosphere once. `BlendEdges` finishes the horizon joins and terrain winding once. Both guard against applying the deformation twice. The saved scene already contains both passes; do not rebuild the earlier route/landscape from its flat baseline.

Pre-pass local backup: `Temp/BeforeAtmosphere/` (scene, JourneyLandscape and JourneyRoad).
