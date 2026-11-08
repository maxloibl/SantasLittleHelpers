# Network Movement Test

Generated in Unity from **Tools > Networking > Build Movement Test Scene**. This isolated scene leaves `DeliveryHouse` and `MovementSandbox` untouched. The Windows builder passes this scene through `BuildPlayerOptions.scenes`; it does not change project-wide build settings.

## Local Windows run

1. Open with Unity `6000.6.8f1` and build `Assets/Game/Networking/Scenes/NetworkMovementTest.unity` as a Development Build (Windows x86_64).
2. Start one process, click **Start Host**; start a second process, enter `127.0.0.1` and click **Join as Client**. Both use UDP port `7777`.
3. Look for the other player's capsule and verify that its movement appears in both windows.
4. Use **Disconnect / Stop Host** to stop normally. The host should despawn a disconnected client's player.

For repeatable local verification, the test player accepts `--host` or `--client` to start automatically and `--autoplay` to move the local owner through the existing `FirstPersonController`'s `CharacterController`. Add `--shutdown-after=12` to a client to call `NetworkManager.Shutdown()` normally after twelve seconds.

## Ownership

Each player has its own `NetworkObject`. `OwnerNetworkTransform` lets the owner publish movement. Only the owner enables the existing `FirstPersonController`, its local camera, and its `AudioListener`; other peers keep the capsule visible while disabling the local view and controller. NGO removes a disconnecting client's player object under its normal player-object lifecycle.

## Validation record

- Unity `6000.6.8f1` package resolution succeeded: NGO `2.13.3` (registry), Unity Transport `6.6.0` (editor builtin, lock depth 1).
- Windows x86_64 Development Build: `NETWORK_BUILD result=Succeeded errors=0 warnings=1`.
- Two separate Windows processes connected over localhost port `7777`: host PID `38264`, client PID `42380`.
- Host log showed owner 0 as `localOwner=True localView=True`, owner 1 as `localOwner=False localView=False`. Client log showed the inverse.
- `--autoplay` produced two separately visible player figures with independent movement observed by both peers. Host logged owner 0 moving from x `-4.00` to about `-3.84`, and owner 1 moving from `-3.00` to about `-2.91`; client saw owner 1 move from `-3.00` to about `-2.90` while receiving host positions. The client briefly initializes at x `+4.00`, then syncs to the prefab spawn at x `-3.00`; distinct starting sides are not guaranteed by this test scene.
- Normal client shutdown logged `NET_NORMAL_SHUTDOWN`, `NET_PLAYER_DESPAWN owner=1`, and `NET_DISCONNECTED clientId=1`; host logged despawn of owner 1 and `NET_DISCONNECTED clientId=1`. No runtime exceptions occurred in this successful run.

The generated build and logs stay outside Git in the private validation copy. Gift and capture replication are out of scope.
