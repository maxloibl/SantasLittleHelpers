# Cozy art and asset pipeline qualification

Issue #35 qualifies references and the import path. It does not approve a finished model or replace the visual review required by #43–45. The existing comparison scene is `Assets/Game/Art/Scenes/CozyArtTest.unity`; it displays the Unity-native elf, gift, and delivery arch together under one directional key and cool ambient fill.

## Style and room target

Keep the current soft, chunky read: broad silhouettes, rounded corners, restrained surface detail, and warm holiday accents against cool snow shadows. The prototype palette in `CozyArtBuilder.cs` is the reference: pine `#1F634A`, berry coat `#B82B40`, warm skin `#FFB885`, cream `#FFE8B8`, gold `#FFA12A`, cranberry `#BF1C30`, snow blue `#B8DEE8`, and delivery teal `#147A73`. Its test light is a white directional key at intensity 1.25 and rotation (45, -25, 0), with a cool blue ambient/background. For the mission interior, retain cool ambient fill but warm the key and fireplace accents; keep gifts and the teal delivery marker readable in the darkest tested corner. Prefer one shadow-casting directional light and baked/static room fill; make fireplace glow emissive rather than a second real-time shadow source.

The prototype elf is about 2.4 m tall, the gift box is 0.78 × 0.68 × 0.70 m with its root at floor level, and the delivery arch is about 1.65 m wide × 2.12 m high. These are useful comparison proportions, not final character or gameplay collider dimensions. Imported art must use a visual-only child wrapper and must not alter gameplay pivots, triggers, colliders, networking, or physics.

Target a compact hut with a 16 × 12 m clear interior and about 3.6 m ceiling height. Keep four 1.5 × 1.5 m start footprints in a readable 2 × 2 group, a clear 2.2 m central aisle, and an unobstructed exit visible from every start. Put the workbench on one long side and the fireplace on the other so they frame the exit sightline instead of covering it. The 3.6 m circle around the exit stays free of furniture and large props. The shell should read as a warm timber room, with a snow-visible opening or window that makes the outside obvious.

### Windows desktop budget targets

These are initial budgets for a 1920 × 1080, 60 fps Windows target in the existing URP project. They bound the small indoor play space without spending detail players cannot see; confirm frame time and visible counts in the #43–45 showcase scenes on the intended minimum PC before locking them.

| Visible group | Triangle target | Texture/material target | Reason |
| --- | ---: | --- | --- |
| Hut shell, trim, doors, windows | ≤ 60k | One 2k atlas; ≤ 2 opaque materials | Most of the room is visible at once; the cabin kit repeats modular pieces. |
| Fireplace and workbench | ≤ 12k combined | Share the hut atlas/material | Large landmarks need a clear silhouette, not small geometry. |
| Four player elves | ≤ 12k each | One shared 1k atlas/material | The player may view another character at close range. |
| Up to eight residents | ≤ 8k each | Share one or two 1k atlases/materials | Readable faces and role props matter more than dense surfaces. |
| Gift, goal, sled, winter props | ≤ 4k per hero prop; ≤ 20k total nearby | One shared 1k prop atlas; no more than 3 material slots per hero prop | The gift and delivery target must stay readable in motion and dim light. |
| Room draw/light budget | ≤ 100 visible renderers; ≤ 120 batches before instancing | One real-time shadow-casting directional light; baked/static fill | Keep a small cooperative scene predictable on mid-range Windows PCs. |

Use 1 Unity unit = 1 metre. Only use transparency where the silhouette needs it. Do not raise texture resolution to hide poor UVs; fix the UVs or simplify the material first.

## Sources qualified for private-repository use

Pack pages below are the primary license/version records. The Kenney archive used for the import probe contains its own CC0 notice, which is also checked into `Assets/Game/Art/PackProbe/KenneyHolidayKit/License.txt`. Kenney says its asset pages are CC0, usable in commercial projects, with attribution optional. A voluntary donation is not a purchase requirement. Keep source credit and license text anyway. For Quaternius, the official pack page states CC0 and offers a free download; its support/donation option is optional.

| Role and downstream issue | Source/version and exact starting assets | License, cost, redistribution, and fallback |
| --- | --- | --- |
| Hut shell, door, winter dressing — #43 | [Kenney Holiday Kit](https://kenney.nl/assets/holiday-kit), 2.0 (remade 2024): `cabin-wall`, `cabin-corner`, `cabin-wall-roof`, `cabin-roof-snow`, `cabin-roof-chimney`, `cabin-doorway`, `cabin-door-rotate`, `cabin-window-large`, `floor-wood`, `floor-wood-snow`, `wreath`, `lights-colored`, `lantern`, `tree-decorated-snow`. | CC0; free, optional donation; selected source models and notice may be kept in this private repo. `#43` imports only its curated selection into `Assets/Game/Art/CabinKit/`. No fireplace, full workbench, or complete interior is supplied. Make the fireplace from original Unity-native geometry and build the workbench from Furniture Kit pieces or original geometry. |
| Furniture and interior dressing — #43 | [Kenney Furniture Kit](https://kenney.nl/assets/furniture-kit), 1.0: `table`, `bench`, `chairRounded`, `kitchenCabinet`, `lampSquareTable`, `rugRectangle`, `books`. | CC0; free, optional donation; private-repo redistribution allowed with the license notice. The kit has no specific workbench; combine `table` + `kitchenCabinet` into an original wrapper. Native fireplace is the concrete gap fallback. |
| Elf and walking humanoid residents — #44 | **Preferred, rig-first combination:** [Quaternius Universal Base Characters](https://quaternius.com/packs/universalbasecharacters.html), August 2025, regular male base for the elf and regular female base for a resident; [Modular Character Outfits – Fantasy](https://quaternius.itch.io/modular-character-outfits-fantasy), v2.1 (2026-05-07), `Male_Wizard` parts for the elf and `Peasant_Male` for the resident; [Universal Animation Library](https://quaternius.itch.io/universal-animation-library), v3.0 (2026-06-16), no-root-motion set with idle and locomotion clips. The base pack has six animated humanoid-rig models and averages 13k triangles per model; outfits and animation library explicitly support CC0 and retargeting to the humanoid rig. Add original pointed ears/hat and tune colors in Unity to separate the elf silhouette. | All three sources are CC0; the base characters and standard outfit/animation packs have free downloads (support and paid source tiers are optional). Keep source and license attribution in the private repo manifest. Universal Base Characters is the best route for #44 because its official page documents the Humanoid rig and compatibility with the animation library. Issue #44 must still retrieve the selected files, cold-import, verify the actual Avatar and in-place clips, and approve the silhouettes. The older [Ultimate Animated Character Pack](https://quaternius.com/packs/ultimatedanimatedcharacter) is a fallback source with an exact `FBX/Elf.fbx` and `FBX/Worker.fbx`, but its Drive download was temporarily quota-limited during this qualification. If the preferred source is unavailable, make an original low-poly elf mesh and a simple custom skeleton, then author in-place idle/walk curves in Unity's Animation window; do not present the existing static CozyElf prototype as animation-ready. |
| Gifts — #45 | [Kenney Holiday Kit](https://kenney.nl/assets/holiday-kit), 2.0: `present-a-cube` for the light gift and `present-b-cube` for the heavy gift, plus `present-a-rectangle` / `present-b-rectangle` as profile variants. The probe imports `present-a-cube`. | CC0; free, optional donation; selected files and license may be redistributed privately. Use different profiles/scale and color grouping in wrappers; keep Rigidbody, collider, grab pivot and network state on the gameplay prefabs. If art does not survive distance/low-light review, use original URP cubes with cream/gold ribbon and distinct light/heavy profiles. |
| Goal marker and sled — #45 | Reuse the existing `DeliveryTarget_Visual` as the teal/gold design guide; Holiday Kit 2.0 has `sled` and `sled-long` but no dedicated glowing delivery marker or full Santa sleigh. | Existing marker is project-owned and contains renderers only. Create a marker from original URP geometry around the existing trigger. `sled`/`sled-long` are CC0 starting props; make a distinct original sleigh wrapper if they do not read as the project vehicle. Do not import gameplay colliders or trigger behavior from art packs. |
| Static winter residents/props — #43/#44 | Holiday Kit 2.0: `reindeer`, `snowman`, `gingerbread-man`, `gingerbread-woman`. | CC0, free, optional donation, redistributable with source notice. In the tested FBX import, `reindeer` is static with no rig, Avatar or clips. It can be a prop or stationary resident only; use a rigged humanoid donor for any NPC that must idle or walk. |

## Import probe and recorded result

The checked-in sample is intentionally limited to two Holiday Kit models, its 512 × 512 shared colormap, and the archive license notice. It demonstrates the production source → Unity import → URP material → visual wrapper path without checking the full pack into the project.

Archive retrieval: official [Holiday Kit page](https://kenney.nl/assets/holiday-kit), archive reports `Holiday Kit (2.0)` and creation date 2024-12-11. Archive SHA-256: `FDE4D514D7297388D98058E8933FF614E071886F7CE57F9AEA4B00D7698DD769`. Extracted source hashes: `present-a-cube.fbx` `92FB40E14CCF82A1BE90FB7BE78995385806562AFF4E9EF4AE3A59FFF928470D`; `reindeer.fbx` `1D1D1E3F9723786B0C52ABCBD49F480811304FBAD0BB14F4939E02855B5DA65E`; `Textures/colormap.png` `015FCC1664BE7576F468A98BC4F5F329EF8B3C9CAEF0D85B2A4DC0CA69516863`.

Unity 6000.6.8f1 (`237b6593792b`) imported both FBXs in this URP project with default model scale 1 and root position (0, 0, 0); the external atlas resolved. Unity recognized the FBX material as `Universal Render Pipeline/Lit`; the probe wrapper explicitly assigns the URP/Lit material and atlas so the conversion is reviewable. `present-a-cube` has two renderers (box and lid), 238 triangles, extents roughly 0.46 × 0.58 × 0.46 m, and its bottom sits at y=0. `reindeer` has one renderer, 870 triangles and extents roughly 0.62 × 1.12 × 1.08 m with its bottom at y=0. Both have zero SkinnedMeshRenderers, no Animator/Avatar, and no animation clips. This is a clean static prop import, not a valid character rig import; use the Quaternius character source for #44 and verify its rig there.

Reproduce the import/check and wrapper generation from the repository root with:

```powershell
C:\6000.6.8f1\Editor\Unity.exe -batchmode -quit -projectPath . -executeMethod ImportCheck.Run -logFile C:\tmp\slh-packprobe.log
```

The editor script logs importer scale/type, root position, renderer types, bounds, shaders, texture, avatar/clip availability, active render pipeline, and writes `present-a-cube_Visual.prefab` and `reindeer_Visual.prefab` with URP/Lit materials. Open the wrappers in a scene and perform the visible silhouette/material review before production adoption; an import pass alone is not model approval.

## Staging, wrapper, and cold-import rules

1. Download candidate packs to an external staging folder first. Record the official pack page, displayed pack version/date, exact filenames, retrieval date, archive checksum, license text, and any attribution in the downstream `*-SOURCES.md` manifest. Do not treat a “free” label or the package store page as license evidence.
2. Check that the license permits redistribution of selected source files in this private repository. Copy only the chosen source meshes and required textures/material inputs, preserving relative texture paths and the included notice; leave the full archive and unused variants outside the repo. No purchase or paid subscription is required for the packs qualified here.
3. Import with Unity 6000.6.8f1. Let Unity create visible `.meta` files and stable GUIDs. Do not hand-author GUIDs. Commit the source manifest, required license notice, selected source files, wrappers/materials, and all Unity `.meta` files together.
4. Each integration wrapper owns only the imported visual hierarchy and art-specific URP materials. Normalize scale and root pivot in the wrapper after checking bounds; keep game scripts, Rigidbody, colliders, triggers, NetworkObjects, and gameplay pivots in their existing owners.
5. For a cold import, use a clean worktree/clone with the same Unity version, no `Library/`, then open once and wait for imports to finish. Confirm no missing source textures, shader errors, importer warnings affecting scale/rig, broken GUID references, or accidental project/package changes. Run `python scripts/check-unity-repo.py`. Reopen the dedicated showcase and check typical mission light, distance, silhouette, material/color, and actual pivot alignment before accepting the art.

### Import qualification boundaries

- Asset/license qualification is permission to evaluate a source. It is not visual approval and does not close the review in #43–45.
- A model may be copied into the private repo only when its specific license permits it. If a source page, license file, version, or download cannot be rechecked, do not stage that source; use the original Unity-native fallback and record why.
- The tested Kenney character-shaped prop is not rigged. Quaternius is the documented free CC0 humanoid donor candidate; #44 still owns the actual character import, retarget/clip check, budget measurement, and silhouette acceptance.
