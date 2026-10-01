# Changelog

All notable changes to this project will be documented in this file.

## [1.0.6] - 2026-09-21

### Fixed

Updated all types of colliders (performance, simple, and complex) across multiple prefabs, realigned crooked main meshes to ensure precise model positioning, and refined physics configurations:

- **Kitchen:**
  - `ChefsKnife`: Updated physics configuration.
  - `CeramicDessertPlate`: Updated physics configuration and colliders.
  - `CeramicDinnerPlate`: Updated physics configuration and colliders.
  - `CookingPot`: Updated main mesh positioning and added more accurate mesh colliders and physics configuration across all variants.
  - `NonStickPan`: Updated physics and collider setup.
  - `CoffeeMug`: Updated physics configuration and colliders.
  - `RegularDrinkingGlass`: Updated main mesh, physics configuration, and replaced mesh renderers for filled variants.
  - `KitchenTrashCan`: Updated main mesh and colliders. (Separators' origins are changed, props on scene may need re-adjustments).
  - `KitchenCabinets`: Realigned crooked main meshes and fixed separator origins across all variants. (Separators' origins are changed, props on scene may need re-adjustments).
  - `KitchenCounters`: Realigned crooked main meshes and centered off-axis separators and fixed separator origins across all variants.
  - `InductionStove`: Updated colliders.
  - `Microwave`: Refined collider bounds.
  - `Refrigerator`: Updated main mesh tri-count and refined collider bounds.

- **Dining Room:**
  - `DiningChair`: Realigned and centered main mesh and updated colliders bounds.
  - `DiningTable`: Refined colliders.
  - `DiningPendantLight`: Refined colliders.

- **Living Room:**
  - `CoffeeTable`: Applied style update to main mesh, updated colliders, and configured physics settings.
  - `FlatTV`: Updated main mesh tri-count, colliders, and physics settings.
  - `SofaThreeSeats`: Updated physics material and physics settings.
  - `SofaPillow`: Remove unused physics variant's override.
  - `CardBoardBox`: Resolved missing physics material assignments.

## [1.0.5] - 2026-09-16

### Fixed

- Updated `CardboardBox`'s mesh, colliders, and unifying rotational axes across all lids (tweaking rotation might be needed if already put on scene).

## [1.0.4] - 2026-09-16

### Fixed

- Updated inconsistent mesh positioning and multiple colliders (performance, simple and complex) across various prefabs:
  - **Kitchen:** `ChefsKnife` (including adjusting Rigidbody physics variants).
  - **Living Room:** `SofaPillow`, `SofaThreeSeatsBody`, `SofaThreeSeats`, and `CoffeeTable`.

## [1.0.3] - 2026-09-11

### Fixed

- Updated inconsistent colliders (performance, simple, and complex) across various prefabs to ensure reliable physics interaction:
  - **Kitchen:** `CerealBowl`, `CeramicDessertPlate`, `CeramicDinnerPlate`, `CookingPot`, `CookingPotBody`, `CookingPotLid`, `NonStickPan`, `CoffeeMug`, `RegularDrinkingGlass`, `OvenTray`, `Microwave` (added new concave variant and including adjusting Rigidbody physics variants), `ChefsKnife` (including adjusting Rigidbody physics variants), and `DryingRack` (including adjusting Rigidbody physics variants).
  - **Dining Room:** `DiningPendantLight`.
  - **Living Room:** `SofaPillow` (including adjusting Rigidbody physics variants).

## [1.0.2] - 2026-09-10

### Fixed

- Corrected mesh file indexing inside `DryingRackMesh_Hulls` to strictly enforce 0-based sequential numbering (`_0`, `_1`, `_2`).

## [1.0.1] - 2026-08-30

### Fixed

- Fixed minor symmetry and mirror inconsistencies on kitchen counter doors (`KitchenCounterDouble` and `KitchenCounterSingle`).

## [1.0.0] - 2026-08-05

### Added

- Initial release of the Modular Household Starter Pack.
