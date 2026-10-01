# Forest camp episode

Scene: `Assets/Scenes/Stage1_Outskirts.unity`. Optional stop at route distance 3,200 m, on the left.

## Layout and purpose

The main road continues past the stop. The existing side loop provides vehicle access and return. A compact clearing holds a dining table, two chairs, a mug, backpack, cold stone fire ring, fallen-log seating and a tent. Existing imported assets provide the objects. The old rectangular courtyard and duplicate staging props are disabled locally.

Water (8 L) stands beside the table; four food portions are on it; the medkit is placed at the tent entrance. A narrow trail leads about 40–50 m beyond the clearing to an abandoned pickup with an off-road wheel (90% condition) and a canister containing 5 L gasoline. Carrying the wheel back occupies the player's hands and consumes time while the storm continues moving. These are finite physical items, not rewards generated on arrival.

## Construction

`ForestCampAuthoring.Build` places objects in route coordinates: longitudinal distance and lateral offset. It evaluates the winding route frame, then raycasts against authored ground meshes to seat props on the actual terrain. The irregular clearing is a 24-vertex polygon; footpaths are mesh strips sampled every 1.5 m. Ground colliders remain underneath them. Vegetation within 3.2 m of the access trail is disabled to preserve passage. The builder refuses a second build when its root already exists.

`ForestCampEpisode` checks the five item references every 0.3 seconds. Leaving the vehicle near camp shows a supply hint. Moving a supply under the existing cargo hierarchy triggers a short synthesized loading sound and a notification naming the item and showing occupied cargo slots. The existing hands, pockets and trunk implement the transfer. Return requires driving beyond 3,390 m within 8 m of the road centre. No vehicle physics or storm speed changes are part of this episode.

## Verification

- Scene authoring and script compilation succeeded. Camp overview, table, corrected mug material, tent entrance and optional stash were inspected through Unity camera captures.
- In Play Mode, parking and leaving the vehicle set `Entered=true`.
- Actual hand pickup and `VehicleCargoTrunk.StoreFromPlayer` succeeded for the new wheel; its 0.90 condition survived transfer.
- Repeated scene/Play Mode switching interrupted the subsequent cargo-feedback and return-notification checks. These are not verified end to end. No full timed drive or listening check was completed.
- The final console query reported Unity's internal recursive PlayerLoop error. Earlier compilation checks were clean; the editor error remains unresolved and should be rechecked in an uninterrupted editor session.

The already-declared `com.unity.netcode.gameobjects` package was resolved through Unity Package Manager, removing the earlier missing-package compilation blocker. Cooperative-play source files were not edited.

Pre-pass scene backup: `Temp/BeforeForestCamp/Stage1_Outskirts.unity`. Inspection captures: `Temp/ForestCamp/`.
