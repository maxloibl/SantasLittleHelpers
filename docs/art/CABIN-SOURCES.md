# Cabin kit sources, wrappers and showcase

Issue #43 delivers the hut shell, door, fireplace, workbench and curated winter/furniture props under `Assets/Game/Art/CabinKit/`. It adds no gameplay scripts, colliders, network components, triggers or physics ownership. Physics approval stays with the hut scene owner.

## Retrieved sources

Retrieved 2026-11-09 from the official Kenney pages. Only the files listed below were copied into this private repository; archives and unused variants stayed in external staging.

| Pack | Official page | Version/date from archive license | Archive SHA-256 | License |
| --- | --- | --- | --- | --- |
| Holiday Kit | https://kenney.nl/assets/holiday-kit | Holiday Kit 2.0, creation date 2024-12-11 | `FDE4D514D7297388D98058E8933FF614E071886F7CE57F9AEA4B00D7698DD769` | CC0, attribution optional. Notice: `Sources/KenneyHolidayKit/License.txt` |
| Furniture Kit | https://kenney.nl/assets/furniture-kit | Furniture Kit 2.0, creation date 2018-10-20 | `E67652D0932CEE41683F74711C03D3E192A2AF9979EF8E6B237711F5482D46B0` | CC0, attribution optional. Notice: `Sources/KenneyFurnitureKit/License.txt` |

Attribution: "Kenney (www.kenney.nl)" for both kits. The pipeline manifest listed Furniture Kit as 1.0; the official archive retrieved for this issue identifies itself as 2.0 (2018-10-20). The staged files match the retrieved 2.0 archive.

### Staged source files

Holiday Kit (`Sources/KenneyHolidayKit/`): `cabin-wall`, `cabin-corner`, `cabin-wall-roof`, `cabin-roof-snow`, `cabin-roof-chimney`, `cabin-doorway`, `cabin-door-rotate`, `cabin-window-large`, `floor-wood`, `floor-wood-snow`, `wreath`, `lights-colored`, `lantern`, `tree-decorated-snow` (FBX), plus `Textures/colormap.png` and `License.txt`.

Furniture Kit (`Sources/KenneyFurnitureKit/`): `table`, `bench`, `chairRounded`, `kitchenCabinet`, `lampSquareTable`, `rugRectangle`, `books` (FBX) and `License.txt`.

## Wrappers

All wrappers are visual-only prefabs under `Assets/Game/Art/CabinKit/Prefabs/`. Every root is at floor level with the bottom of the combined renderer bounds at `y = 0`; the door has an additional `Hinge` child at its left edge. Holiday shell uses a 2.2 m door/wall module target so the pieces read at character scale. The snow roof wrapper is 3.25 m tall and steep, so the hut scene owner must confirm the final roofline against the 3.6 m ceiling target during assembly. Holiday winter props already use metres and stay at scale 1. Furniture keeps one kit-wide scale derived from a 0.75 m table height. Imported wrappers keep their source FBX materials so each model's atlas UV/material mapping stays intact.

| Wrapper | Source | Scale | Measured bounds (m) | Triangles |
| --- | --- | ---: | --- | ---: |
| `CabinFloor_Visual` | `floor-wood.fbx` | 2.2 | 2.20 x 0.17 x 2.20 | 112 |
| `CabinFloorSnow_Visual` | `floor-wood-snow.fbx` | 2.2 | 2.20 x 0.17 x 2.20 | 112 |
| `CabinWall_Visual` | `cabin-wall.fbx` | 2.2 | 2.20 x 2.20 x 0.66 | 84 |
| `CabinCorner_Visual` | `cabin-corner.fbx` | 2.2 | 0.33 x 2.20 x 0.33 | 10 |
| `CabinWallRoof_Visual` | `cabin-wall-roof.fbx` | 2.2 | 2.20 x 2.20 x 0.66 | 84 |
| `CabinRoofSnow_Visual` | `cabin-roof-snow.fbx` | 2.2 | 2.89 x 3.25 x 3.08 | 116 |
| `CabinRoofChimney_Visual` | `cabin-roof-chimney.fbx` | 2.2 | 2.82 x 3.13 x 3.08 | 116 |
| `CabinDoorway_Visual` | `cabin-doorway.fbx` | 2.2 | 2.20 x 2.20 x 0.88 | 200 |
| `CabinDoor_Visual` | `cabin-door-rotate.fbx` | 2.2 | 2.20 x 2.20 x 0.88 | 252 |
| `CabinWindow_Visual` | `cabin-window-large.fbx` | 2.2 | 2.20 x 2.20 x 1.54 | 194 |
| `Wreath_Visual` | `wreath.fbx` | 1.0 | 0.67 x 0.76 x 0.26 | 288 |
| `Lantern_Visual` | `lantern.fbx` | 1.0 | 0.53 x 1.74 x 0.46 | 172 |
| `ColoredLights_Visual` | `lights-colored.fbx` | 1.0 | 1.01 x 0.32 x 0.06 | 760 |
| `SnowTree_Visual` | `tree-decorated-snow.fbx` | 1.0 | 1.25 x 2.29 x 1.25 | 810 |
| `Table_Visual` | `table.fbx` | 0.2295 | 1.93 x 0.75 x 1.03 | 120 |
| `Bench_Visual` | `bench.fbx` | 0.2295 | 0.92 x 1.08 x 0.46 | 170 |
| `ChairRounded_Visual` | `chairRounded.fbx` | 0.2295 | 0.46 x 1.04 x 0.46 | 280 |
| `KitchenCabinet_Visual` | `kitchenCabinet.fbx` | 0.2295 | 0.99 x 1.03 x 1.03 | 114 |
| `TableLamp_Visual` | `lampSquareTable.fbx` | 0.2295 | 0.28 x 0.67 x 0.28 | 60 |
| `Rug_Visual` | `rugRectangle.fbx` | 0.2295 | 3.60 x 0.02 x 2.11 | 28 |
| `Books_Visual` | `books.fbx` | 0.2295 | 0.35 x 0.24 x 0.22 | 62 |
| `Fireplace_Visual` | authored | 1.0 | 2.55 x 3.54 x 1.18 | 2584 |
| `Workbench_Visual` | authored | 1.0 | 2.30 x 1.90 x 0.92 | 192 |

The fireplace and workbench are original Unity-native geometry. The fireplace has a stone hearth, piers, timber mantel, chimney breast, logs, metal screen and an emissive ember/flame set. The workbench has a worktop, legs, lower shelf, cabinet, drawers, pegboard, vice, saw and mallet. Neither contains gameplay behaviour.

## Materials and showcase lighting

- Imported Holiday FBX materials use `Universal Render Pipeline/Lit` and remain assigned by the model importer. This preserves the authored Holiday Kit atlas mapping instead of replacing it with a wrapper-wide material.
- The Holiday Kit models use the pack `colormap.png` (512 x 512). The wrappers do not create or remap an atlas material.
- Furniture wrappers use cached `Universal Render Pipeline/Unlit` copies of their imported FBX base colours and base maps. In the showcase render this removed the repeated reflection pattern while keeping the furniture readable; the room shell and floors remain lit so their shadows stay visible. The copies reference source textures rather than adding any.
- Native fireplace/workbench use a small opaque palette (`CabinStone`, `CabinTimber`, `CabinMetal`, `CabinCream`, `CabinSoot`) plus emissive `CabinEmber`. Fireplace glow is emissive only; there is no second real-time shadow source.

Showcase scene: `Assets/Game/Art/CabinKit/Scenes/CabinKitShowcase.unity`.

- Camera at `(0, 8.5, -20)`, rotation `(16, 0, 0)`, FOV 60, solid background `#172433`.
- One shadow-casting directional key: colour `(1.00, 0.84, 0.66)`, intensity 1.35, rotation `(48, -35, 0)`, soft shadows, strength 0.80.
- Flat cool ambient `(0.42, 0.50, 0.60)`, intensity 1.0.
- Snow ground plane, 50 x 30 m, smoothness 0 with environment reflections and specular highlights disabled so the review sees the intended snow value rather than the probe cubemap. The gray before-blockouts use the same non-reflective treatment.
- "Before" group at `x = -8`: raw Kenney sources plus gray fireplace/workbench blockouts in two rows. "After" group at `x = +8`: the CabinKit wrappers in the same two-row arrangement. The furniture wrappers form a third row at `z = -7.5`.

## Budget measurement

Measured by `CabinKitBuilder.MeasureAndReport` and stored in `Assets/Game/Art/CabinKit/ShowcaseBudget.txt`.

| Group | Triangles | Renderers | Material slots | Textures |
| --- | ---: | ---: | ---: | ---: |
| Shell wrappers (10) | 1280 | 11 | 10 imported | 1 |
| Fireplace + workbench | 2776 | 31 | 8 native | 0 |
| Winter props (4) | 2030 | 4 | 4 imported | 1 |
| Furniture wrappers (7) | 834 | 8 | 14 copied | 0 |
| **All 23 wrappers** | **6920** | **54** | **28 unique** | **1** |

The measured set has 6,920 triangles, 54 visible renderers, 28 unique materials and one source texture. Imported Holiday shell and winter wrappers retain their source FBX materials; furniture uses cached Unlit copies of source material values to avoid the batch-render reflection pattern. The authored fireplace and workbench use a compact native palette. The change stays inside the triangle, renderer and texture budgets.

## Verification

Performed with Unity `6000.6.8f1` (`237b6593792b`) at `C:/6000.6.8f1/Editor/Unity.exe`, batch mode, hidden processes, logs under ignored `Logs/`.

Commands:

```powershell
C:\6000.6.8f1\Editor\Unity.exe -batchmode -quit -projectPath . -executeMethod CabinKitBuilder.Build -logFile Logs/cabinkit-build-cold-final.log
C:\6000.6.8f1\Editor\Unity.exe -batchmode -quit -projectPath . -executeMethod CabinKitBuilder.Capture -logFile Logs/cabinkit-capture-cold-final.log
C:\6000.6.8f1\Editor\Unity.exe -batchmode -quit -projectPath . -executeMethod CabinKitBuilder.Validate -logFile Logs/cabinkit-validate-cold-final.log
```

Results:

- Cold import: the final build ran in a fresh project copy that started without a `Library/`; Unity `6000.6.8f1` imported sources and generated 23 wrappers with exit code 0. It measured 6,920 triangles, 54 renderers, 28 unique materials and 1 texture. Log: `Logs/cabinkit-build-cold-final-unlit.log`.
- Validation: 23/23 prefabs pass renderer, triangle, pivot, no-script, no-collider and URP Lit/Unlit shader checks. Scene check finds the camera, exactly one shadow-casting directional light, four labels and the snow ground. Log: `Logs/cabinkit-validate-cold-final-unlit.log`.
- Capture: the showcase renders through URP's standard render request; the reviewed cold-import frame shows the colored Holiday atlas and clean, matte furniture while the shell/floors retain Lit shadows. The comparison groups, snow ground and furniture row are visible. Image: `Logs/CabinKitShowcase-cold-final.png`. Log: `Logs/cabinkit-capture-cold-final-unlit.log`.
- Repository check: `python scripts/check-unity-repo.py` after staging passed (`Unity repository checks passed.`).
- Not verified: visual acceptance on the minimum Windows PC, final in-hut placement/light bake and physics/collider approval remain with the hut scene owner and the #43 visual review.
