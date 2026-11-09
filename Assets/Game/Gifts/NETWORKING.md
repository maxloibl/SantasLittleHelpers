# Networked gift carrying

`NetworkGiftCarryable` owns grip arbitration and Rigidbody forces on the host. Each gift root has `GiftCarryable`, `Rigidbody`, `NetworkObject`, server-authoritative `NetworkTransform`, `NetworkRigidbody`, and `NetworkGiftCarryable`. The LightGift prefab allows one grip slot; a heavy gift allows two distinct client IDs. Repeated requests from the same sender are idempotent. Heavy carrying moves at a reduced solo speed and uses both player positions when two slots are occupied. Clients remain kinematic and receive the host's Rigidbody transform.

Clients request grab and release through `GiftInteractor`. The host reads the sender identity from NGO RPC metadata, resolves that client's player object, and checks reach before assigning a slot. Release is idempotent. Disconnect and network despawn clear slots and restore gravity. `GripCount` is replicated for mission/UI reads. `ForceReleaseOnServer()` is the host hook for capture, delivery, or scripted drops in #12; call it before moving or despawning a gift. Mission triggers should use the host-authoritative gift position and never change a client Rigidbody.

## Scene and prefab wiring

Register `LightGift` and `HeavyGift` in the active `NetworkManager` prefab list and spawn them only on the server. The existing solo `GiftPhysicsTest` continues to use the original player interaction path. `Tools/Gifts/Configure Network Prefabs` adds the NGO components to those two gift prefabs. It does not edit the shared network player prefab or `DeliveryHouse`.

The isolated two-player harness lives in this folder. `Tools/Gifts/Build Coop Network Test` creates `Tests/GiftCoopTestPlayer.prefab`, `Tests/GiftCoopNetworkPrefabs.asset`, and `Tests/Scenes/GiftCoopNetworkTest.unity`. The scene has its own owner-synchronized player prefab, Unity Transport manager, prefab registrations, floor, and server gift spawner. The test driver runs from command-line flags, so no window interaction is needed.

## Reproducing the Windows host/client check

From the project root, use Unity `6000.6.8f1` and call these methods in batch mode:

```powershell
Unity.exe -batchmode -nographics -quit -projectPath <project> -executeMethod SantasLittleHelpers.Editor.GiftCoopNetworkTestSceneBuilder.Build
Unity.exe -batchmode -nographics -quit -projectPath <project> -executeMethod SantasLittleHelpers.Editor.GiftCoopNetworkTestSceneBuilder.BuildWindowsPlayer
```

The second method writes a Development Player to `C:/tmp/slh9-test/GiftCoopTest.exe`. Start two separate processes on the local default port 7777:

```powershell
GiftCoopTest.exe --host -batchmode -nographics -logFile C:/tmp/slh9-host.log
GiftCoopTest.exe --client -batchmode -nographics -logFile C:/tmp/slh9-client.log
```

The script sends duplicate requests for both players on the same heavy gift, competes for the single LightGift slot, releases, carries the heavy gift solo while moving the host player, carries separate gifts, exits the client, checks disconnect cleanup, and requests a fresh host grip. Logs report replicated gift positions from both processes.

## Run performed for #9

Unity `6000.6.8f1` compiled the project and built the Windows Development Player with zero build errors. Two headless Windows player processes connected over localhost. Host and client logs showed two heavy grip slots after duplicate requests, release back to zero, only one LightGift slot under competing requests (`lightPeak=1`), and simultaneous carrying of separate gifts. Host and client logs both showed the heavy gift moving from its spawn position to approximately `x=-3.7`, confirming position replication. After the client exited, host cleanup reported `autoCleanup=True`; a subsequent host request reported `afterDisconnect=True`. Peak measured solo heavy speed was `2.692`, below the configured `4.2` movement bound (the harness allows `4.3` for sampling tolerance).

The host and client logs are local evidence at `C:/tmp/slh9-host4.log` and `C:/tmp/slh9-client4.log`; they are not repository artifacts. The run covers two local processes only, not LAN latency or four-player load.
