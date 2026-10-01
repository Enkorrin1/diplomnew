# First route: stops, sound and storm cues

Implemented in `Assets/Scenes/Stage1_Outskirts.unity`, 2026-09-25.

## Stops and rewards

Rewards are finite physical objects. The visit component announces an approaching turn and first arrival; it never spawns or refills loot. Pickup, pockets, hands, cargo and item use are the existing gameplay systems.

| Distance | Location | Supplies / services |
|---|---|---|
| 1,400 m | Last gas station | 16 L gasoline, 2 L oil, wrench, repair kit |
| 3,200 m | Forest camp | 8 L water, medkit, four food portions; optional trail to off-road wheel at 90% and 5 L gasoline |
| 5,400 m | Suburban repair box | Road wheel at 82% condition, wrench, 2 L oil, medkit, repair kit; paid tier-1 service with tools/lift/bench, not safe |
| 7,600 m | Forestry yard | Off-road wheel at 62%, wrench, 5 L gasoline, food |
| 9,600 m | Freight yard | Cargo wheel at 70%, road wheel at 45%, 2 L oil, 7 L gasoline; parts can be taken to a trader |
| 11,600 m | Overlook | 6 L water, medkit, food |
| 13,040 m | Northern workshop | Existing safe workshop, repair and trade; main road continues |

Signs before the entry identify the stop and turn direction. The board in the yard can be read with E. The old repeated pairs of canisters were disabled locally. Extra prototype tents with poor back-face appearance were disabled after visual inspection; original camp tents remain.

Landmarks: abandoned checkpoint, fallen trunk beside the carriageway, forestry log yard and overlook seating. Added shelves, barrels, bins, lighting, campsite tables, cold fire rings and backpacks from existing assets. No new road barriers across the travel lane.

## Audio and storm

`JourneySoundscape` adds six audio layers: forest recording, field recording, rain/thunder recording, wind, asphalt tyre roll and dirt tyre roll. The three long recordings use streaming Vorbis imports. Wind and tyre loops are deterministic filtered-noise synthesis, created once per scene and destroyed on teardown.

The mix responds to biome, actual vehicle velocity, ground contact, road collider, whether the player is driving, SFX volume and storm distance. Biome layers crossfade, birds fade as the storm approaches, and rain/wind rise. Pause handling pauses the six sources. The existing engine source now receives speed and throttle load from the journey controller; vehicle physics are unchanged.

Storm cues use the actual gap: warnings below 700 / 400 / 180 m, rate-limited notifications and a bounded dust system (160 particles maximum). The safe workshop suppresses local weather intensity and announces shelter. Existing storm damage and safe-zone logic are retained.

## Preliminary timing model

Fixed storm speed changed from 11 to 13.5 m/s; initial lead remains 900 m. There is no adaptive chase speed. For 13,040 m at an assumed mean moving speed of 18 m/s:

| Total stop time | Arrival time | Estimated remaining gap |
|---|---|---|
| 2 min | 14.1 min | 2,540 m |
| 5 min | 17.1 min | 110 m |
| 7 min | 19.1 min | −1,510 m (front catches up before arrival) |

This is a pacing model, not a recorded playthrough. Detour distance, acceleration and actual driving style affect the outcome. A normal playthrough with chosen stops is needed to tune the new speed. User confirmed vehicle handling and the previous route pass; that work was not reopened.

## Verification

- Journey scripts compiled and earlier console queries returned zero errors. The missing `Unity.Netcode` package blocker was subsequently resolved through Unity Package Manager during the camp pass; cooperative-play source files were not edited. Current camp checks and the later editor PlayerLoop error are recorded in `FOREST_CAMP.md`.
- Seven stop components, nine configured fluid containers, four physical workshop wheels and eleven functional pocket items (24 supplies total).
- Actual medkit interaction stored it in the pocket inventory and removed it from the world.
- Actual wheel hand pickup and cargo storage succeeded; `wheel_road` identity and 0.82 condition survived the transfer.
- 366 side-loop clearance samples: no new content blocking the driving corridor.
- Visually inspected gas station, camp and the readable close-up notice. Screenshots under `Temp/JourneyContent/`.
- Six audio sources were observed playing with different biome/storm gains. Final continuous storm-transition and listening checks were interrupted by the user switching Play Mode (confirmed by the user); no full new-balance run has been completed.

Authoring: `JourneyContentAuthoring.Build`, then `Refine`. Both have duplicate guards; the current scene already contains their results. Pre-pass scene backup: `Temp/BeforeJourneyContent/Stage1_Outskirts.unity`.
