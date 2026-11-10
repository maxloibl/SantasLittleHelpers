# Mission kit sources and import record

Issue #45 imports the gift, delivery-goal, and sleigh showcase art. Only the files listed below were copied from the approved archive; the full Kenney pack is not checked in.

## Source and license

- Official pack page: <https://kenney.nl/assets/holiday-kit>
- Pack: Holiday Kit 2.0, created 2024-12-11
- License: Creative Commons Zero (CC0), <https://creativecommons.org/publicdomain/zero/1.0/>
- License notice: `Assets/Game/Art/MissionKit/Source/License.txt`
- Approved archive: `Temp/ApprovedAssets/kenney-holiday-kit.zip`
- Archive SHA-256: `FDE4D514D7297388D98058E8933FF614E071886F7CE57F9AEA4B00D7698DD769`

## Picked files

The archive-relative paths are retained under `Assets/Game/Art/MissionKit/Source/`, including the shared atlas location expected by the FBX files.

| Source file | SHA-256 |
| --- | --- |
| `Models/FBX format/present-a-cube.fbx` | `92FB40E14CCF82A1BE90FB7BE78995385806562AFF4E9EF4AE3A59FFF928470D` |
| `Models/FBX format/present-b-cube.fbx` | `98477C21C7A49336BFD676169872BCD99D325A9B6A3E54B50F2FD83F611D0A1F` |
| `Models/FBX format/sled-long.fbx` | `0ED96F969704A56572D37E0A0D1FDDFE18951815757D32BF4C471F305F46522D` |
| `Models/FBX format/Textures/colormap.png` | `015FCC1664BE7576F468A98BC4F5F329EF8B3C9CAEF0D85B2A4DC0CA69516863` |
| `License.txt` | `6010F677D95F3AB7935FAF873D8F4EB96AD1E5F02FD0E4659C9D92852B768D6A` |

`present-a-cube` drives `LightGift_Visual`, `present-b-cube` drives `HeavyGift_Visual`, and `sled-long` drives `Sleigh_Visual`. The 512 x 512 `colormap.png` is shared by the gift and sleigh materials.

## Generated mission kit

- `Assets/Game/Art/MissionKit/Prefabs/LightGift_Visual.prefab`
- `Assets/Game/Art/MissionKit/Prefabs/HeavyGift_Visual.prefab`
- `Assets/Game/Art/MissionKit/Prefabs/DeliveryGoal_Visual.prefab`
- `Assets/Game/Art/MissionKit/Prefabs/Sleigh_Visual.prefab`
- `Assets/Game/Art/MissionKit/Scenes/MissionKitShowcase.unity`

The editor builder is `Assets/Game/Art/MissionKit/Editor/MissionKitBuilder.cs` (`MissionKitBuilder.Build`). It imports the source models at scale 1 with file scale enabled, assigns URP/Unlit materials, and rebuilds the prefabs and showcase scene idempotently.

Palettes:

- Light gift: warm cream atlas tint with a soft-gold accent.
- Heavy gift: deep berry atlas tint with a dark pine accent.
- Delivery goal: original Unity-primitive arch in teal, cream, and gold; no source model is used for the goal.
- Sleigh: warm atlas tint so the source sled reads as a wooden holiday sleigh.

## Pivot usage

Every generated prefab has a root transform at the world origin with identity rotation and scale. The builder measures the combined renderer bounds, moves the visual child so the pivot sits at the horizontal center of the footprint and at the floor (`y = 0`), then verifies the result. The goal is authored directly around the same floor-centered pivot.

Observed bounds and pivot offsets after the build:

| Prefab | Size (X, Y, Z) | Pivot offset applied |
| --- | --- | --- |
| `LightGift_Visual` | 0.450, 0.571, 0.450 | 0.000, 0.000, 0.000 |
| `HeavyGift_Visual` | 0.450, 0.471, 0.450 | 0.000, 0.000, 0.000 |
| `DeliveryGoal_Visual` | 1.680, 2.230, 1.150 | 0.000, 0.000, 0.000 |
| `Sleigh_Visual` | 0.625, 0.375, 1.000 | 0.000, 0.000, 0.000 |

The source models were already floor-centered, so the measured correction was zero; the normalization still runs on every rebuild. Integrate these prefabs as visual children under the gameplay gift, goal, and sleigh roots. Keep gameplay colliders, Rigidbody, grab pivots, triggers, and NetworkObject state on those gameplay prefabs.

The current `LightGift` and `HeavyGift` gameplay roots scale to 0.55 m and 0.90 m and each uses a unit box collider centered on its local origin. In #49, align each floor-origin wrapper at local Y = −0.5. Initial wrapper scales are (2.0, 1.75, 2.0) for light and (2.0, 2.0, 2.0) for heavy. These produce a mesh near the existing unit-box envelope before the gameplay root scale. Place the delivery goal at floor level around the existing delivery trigger, using its transform only as a placement reference. Keep the sleigh return point and its spawn/return logic fixed. Use the sleigh wrapper at approximately 2× scale for a 2 m display model and keep its riders' existing collision-free route clear.

All four prefabs contain renderers only: no colliders, no Rigidbody, and no NetworkObject or other MonoBehaviour. The showcase floor and labels are scene-only objects.

## Test and observed outcome

Run with the Unity project root as the working directory:

```powershell
C:\6000.6.8f1\Editor\Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod MissionKitBuilder.Build -logFile Logs/missionkit-build.log
```

Unity 6000.6.8f1 completed with exit code 0 and logged `Exiting batchmode successfully now!`. The atlas imported as 512 x 512. Source imports reported `present-a-cube` 2 renderers, `present-b-cube` 2 renderers, and `sled-long` 1 renderer, with no SkinnedMeshRenderer and no Animator. The build logged zero colliders, zero Rigidbody components, and zero MonoBehaviours for every generated prefab, and created the showcase scene with four prefabs, two directional lights, one camera, and no colliders.

The render-only gameplay props use URP Unlit to keep the two gift colors and the delivery marker readable in dark rooms. The URP Lit and Simple Lit variants preserved the right material data but produced gray props in the batch capture on this machine. The Unlit capture shows the intended red, cream, green, gold, and teal palette. It uses the same prefab assignments as the showcase scene.

To reproduce the showcase capture, run the same command with -force-d3d11 instead of -nographics and use -executeMethod MissionKitBuilder.Capture. The image is written to Logs/MissionKitShowcase.png.
