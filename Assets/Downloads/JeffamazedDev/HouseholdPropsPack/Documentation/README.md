# 3D Low-Poly | Modular Household Asset Starter Pack

A high-performance, low-poly modular household asset pack featuring optimized structural building blocks, decoration variants, equipment variants, and functional physics setups. Meticulously built with optimization and flexibility in mind, every asset is highly performant—ranging from ultra-lightweight props at just **220 triangles** up to a maximum of **2,200 triangles** for more complex shapes. This ensures exceptional performance on mobile, VR, and low-end platforms while maintaining a clean, cohesive style.

---

## 📐 Dimensions & Layout

* **Counters:** Base dimensions are standardized to exactly **`0.6m x 0.6m x 0.9m`** (Width x Depth x Height).
* **Cabinets:** Base dimensions are standardized to exactly **`0.4m x 0.4m x 0.9m`** (Width x Depth x Height).
* **Other Kitchen Objects:** All accompanying props and appliances are meticulously built to scale relative to the core counters and cabinets to ensure visual consistency out of the box.

---

## 📦 Pack Contents

This modular pack includes assets and props optimized for the following layout environments:
* **Kitchen Area**
* **Dining Area**
* **Living Area**
* **Storeroom Area**

Each area features matching decoration and physics variants built to scale. (For equipments only include handheld items)

---

## ⚡ Physics & Colliders (Your Choices)

This pack provides multiple collider variants (primarily for irregular object shapes) so you can balance physics accuracy against performance:

* **Performance Colliders:** Pre-configured simple box collider for maximum performance.
* **Simple Colliders:** Pre-configured primitive boundaries for high-performance optimization.
* **Complex Colliders:** Provided by default for any assets that are organic, curved, or non-boxy. 
* **The Choice is Yours:** Swap between the provided simple and complex collider components depending on your target platform's performance constraints.

---

## 💡 Lighting Setup
* **Placements Provided:** Dedicated placeholder transform positions are included on relevant prefabs where lights should logically be attached.
* **Customization:** Simply drop your own Point, Spot, or Universal Render Pipeline (URP) lights directly into these pre-arranged slots to instantly illuminate the asset interiors or indicators.

---

## 🔄 Mirroring Guidelines (Left / Right Setup)

If you need to mirror an asset (e.g., turning a left-handed counter unit into a right-handed unit):
* **Do NOT scale the root GameObject.** Keep the main parent prefab transform scale at a clean `(1, 1, 1)`.
* **Scale the Child Objects instead:** Select the flat visual meshes and collider child objects inside the hierarchy, and **scale their X-axis to `-1`**. 

This isolates the negative matrix inversion to the local rendering space, keeping your global physics and root transform matrices completely stable.

---

## 📜 Credits
* **Textures:** Special thanks to **Imphenzia** for providing the texture palettes used in building and profiling these assets. Check out his work and tutorials on the [Imphenzia YouTube Channel](https://www.youtube.com/@Imphenzia).
