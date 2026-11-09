# Cozy art prototype

Open `CozyArtTest.unity` to compare the soft elf silhouette, cranberry gift, and teal delivery arch in one neutral display scene. The scene uses only built-in Unity geometry, a directional light, and text labels.

## Prefabs for integration (#12)

- `Prefabs/CozyElf_Visual.prefab`: rounded head and body, long ears, drooping pine cap, warm scarf. Parent under the gameplay player's visual root; adjust local scale to the gameplay rig. Contains renderers only.
- `Prefabs/Gift_Cranberry_Visual.prefab`: berry-red box, cream ribbon, gold bow. Add beneath the existing gift root and align its origin to the gameplay gift pivot; keep the gameplay collider on the gameplay prefab.
- `Prefabs/DeliveryTarget_Visual.prefab`: teal base, pale uprights, gold lintel. Place beside/around the existing delivery trigger; it has no gameplay collider.

All models are visual only: no colliders, scripts, or NetworkObjects. Materials use `Universal Render Pipeline/Lit`. Colors deliberately separate the player (pine/berry), gift (cranberry/cream/gold), and goal (teal/snow/gold) at a glance. The editor menu `Tools > Cozy Art > Build Prototype Assets` rebuilds the prefabs, materials, and test scene from Unity-native primitives.
