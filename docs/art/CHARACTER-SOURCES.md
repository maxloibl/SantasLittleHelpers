# Character source and import notes

## Qualified animation source

The character evaluation uses Kenney's [Animated Characters Protagonists](https://kenney.nl/assets/animated-characters-protagonists) archive. The archive notice included with the files identifies itself as `Animated Characters Protagonists (1.1)` and grants CC0; the public pack page currently lists release 1.0 (2020) and CC0. The pack is free; support and attribution are optional. The exact local notice is retained at `Assets/Game/Art/Characters/PackProbe/KenneyAnimatedCharacters/License.txt`.

Archive retrieved 2026-11-09 as `kenney-animated-protagonists.zip` (581,441 bytes), SHA-256 `EC3787DE70FA2200256848D74201B10F6B6C3126594E9857BF989753312C2B84`. Selected archive members and SHA-256:

| File | SHA-256 |
| --- | --- |
| `Model/characterMedium.fbx` | `18835FEF534EEDE635B081EE7FE647D01A885550A591D2E6BF071010906167D8` |
| `Skins/skaterFemaleA.png` | `DE575F5075A8991907A4807FEA3EF291B8E57778C6C4F4ADA990ABE7582026DF` |
| `Skins/criminalMaleA.png` | `E2F66E682C95253FBB45206AB44BDB4FE61B03AF3481ABBD0E4344A6958A0530` |
| `Animations/idle.fbx` | `C8A24E0294376EE5A195C56752A13310E1C0B5F8588A4DB50E094120E3E4CC74` |
| `Animations/run.fbx` | `E635461FC8DACE85EC67A7F7941E949A7C3F108B51AE4D2DA1557E6E01749DF8` |
| `Animations/jump.fbx` | `B88429077A7A1AF5D3F55F43CFD8CE0F7441B4F6F7BB15A8070D7ED15D275F74` |

The model is imported as Humanoid with a valid human Avatar. The three animation FBXs are imported as Humanoid clips, with root translation and rotation locked; the visual Animators have `applyRootMotion` disabled. Idle is the default state, `MoveSpeed` selects the looping run clip, and the `Action` trigger selects the one-shot jump clip. The checked Unity 6000.6.8f1 import reported clip lengths of 1.067 s, 0.667 s, and 0.5 s, respectively. The source mesh has 1,604 triangles, 1,029 vertices, and 61 transforms. With the elf and resident accessory geometry, the wrappers contain 2,468 and 2,452 triangles.

## Runtime integration contract

- `Elf_Visual.prefab` and `Resident_Visual.prefab` are visual-only children. Their roots stay at the character floor pivot; the imported model is nested at scale 0.5. For integration, reserve a provisional standing capsule of 0.55 m diameter × 1.9 m height, centered over the floor pivot. Keep the capsule, movement, interaction, and gameplay pivots on the gameplay prefab.
- The wrappers contain an Animator and renderers only. They contain no `NetworkObject`, collider, Rigidbody, or gameplay/network script. The showcase driver lives only in the isolated showcase scene.
- Set `MoveSpeed` from local velocity on the owner and from replicated velocity on remote proxies. Raise `Action` from the appropriate replicated action state/event so all clients select the same state. The Animator itself is not networked.
- The art wrapper does not choose first-person visibility policy. The player prefab should hide its local body renderers from the owner camera when required by the camera layout, while keeping the renderers enabled for remote proxies.
- The source textures are 1024 × 1024. The elf uses `skaterFemaleA.png`, pointed ear geometry, a red cap, and gold scarf. The resident uses `criminalMaleA.png`, a teal cap, and cranberry scarf. Their colorway/material distinctions are visible in the generated front-facing frame. Both character surfaces and accessories use URP/Unlit because the current batch URP/Lit preview rendered the source colors as grayscale or brown. The elf is below the 12k triangle target and the resident is below the 8k target from the art budget; the wrappers currently use three to four materials because the accessories have separate colors.

## Showcase and visual-review status

`Assets/Game/Art/Characters/Scenes/CharacterShowcase.unity` is the isolated warm-key/cool-fill scene. It displays the idle state beside the resident. `CharacterShowcaseDriver` cycles the elf through idle, run, and jump when the scene is played; its Animator parameters are `MoveSpeed` (float) and `Action` (trigger).

Unity 6000.6.8f1 successfully imported the source model, Humanoid Avatar, and all three clips; the editor pipeline built both wrappers and the showcase scene. The front-facing batch camera frame displays the distinct skin atlases and accessory colorways; it is a static edit-mode preview and appears in the model's T-pose. The editor Play Mode check drove the Animator from Idle to Move to Jump and back to Idle, confirmed bone-pose changes in both movement/action clips, and recorded 0 m Animator-root displacement with root motion disabled. Still review the elf and resident in the normal Editor Game view under typical mission lighting and distance before treating the art as fully approved.

The wrappers remain visual candidates, not gameplay characters. Recheck the provisional capsule alignment and presentation at mission distance and lighting before folding them into the player or resident prefabs. Local-camera visibility, remote-proxy visibility, animation parameters driven by replicated gameplay state, and player/resident prefab integration remain #49 checks. For #49, keep four players plus up to eight residents inside the existing per-character triangle targets; profile material slots and visible renderer/batch counts after integration.
