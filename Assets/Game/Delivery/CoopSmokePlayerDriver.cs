using SantasLittleHelpers.Capture;
using SantasLittleHelpers.Networking;
using Unity.Netcode;
using UnityEngine;

namespace SantasLittleHelpers.Delivery
{
    /// <summary>Opt-in headless two-process acceptance driver. All moves are logged as test repositioning.</summary>
    public sealed class CoopSmokePlayerDriver : NetworkBehaviour
    {
        private bool movedToHeavy;
        private bool requestedHeavyGrip;
        private bool capturedGrabRequested;
        private bool movedToSleigh;
        private bool rescueRequested;
        private bool moving;
        private bool quietGripRequested;
        private bool quietGripConfirmed;
        private bool quietGripTimeoutReported;
        private int quietRouteStage;
        private float quietStageAt;
        private float lastMoveProgressAt;
        private Vector3 lastMovePosition;
        private bool quietStallReported;
        private Vector3 moveTarget;
        private string moveStage;
        private void Update()
        {
            if (!IsOwner || !IsSpawned) return;
            if (System.Array.Exists(System.Environment.GetCommandLineArgs(), arg => arg == "--coop-quiet-smoke"))
            { TickQuietCarry(); return; }
            if (!System.Array.Exists(System.Environment.GetCommandLineArgs(), arg => arg == "--coop-smoke")) return;
            TickMove();
            if (!movedToHeavy && Time.unscaledTime > 3f)
            {
                Vector3 position = new Vector3(OwnerClientId == NetworkManager.ServerClientId ? .15f : 2.05f, 0, -4.0f);
                BeginMove(position, "heavy-grip"); movedToHeavy = true;
            }
            if (!requestedHeavyGrip && Time.unscaledTime > 4f)
            {
                NetworkGiftCarryable heavy = FindHeavy();
                if (heavy != null && heavy.IsSpawned) { heavy.RequestGrip(true); Debug.Log($"COOP_SMOKE_GRIP_REQUEST owner={OwnerClientId} gift=heavy"); requestedHeavyGrip = true; }
            }
            CapturedPlayerState self = GetComponent<CapturedPlayerState>();
            if (self != null && self.IsCaptured)
            {
                if (!capturedGrabRequested && FindHeavy() is NetworkGiftCarryable capturedHeavy)
                {
                    capturedHeavy.RequestGrip(true);
                    capturedGrabRequested = true;
                    Debug.Log($"COOP_SMOKE_CAPTURED_GRIP_REQUEST owner={OwnerClientId}");
                }
            }
            if (!rescueRequested && self != null && !self.IsCaptured)
            {
                CapturedPlayerState captured = null;
                foreach (CapturedPlayerState state in FindObjectsByType<CapturedPlayerState>())
                    if (state != null && state.IsSpawned && state.IsCaptured) { captured = state; break; }
                if (captured != null)
                {
                    Vector3 offset = captured.transform.position + Vector3.right * 1.1f;
                    if (!moving) BeginMove(offset, "rescue");
                    if ((transform.position - captured.transform.position).sqrMagnitude <= 1.5f * 1.5f)
                    {
                        self.RequestRelease(captured);
                        Debug.Log($"COOP_SMOKE_RESCUE_REQUEST rescuer={OwnerClientId} target={captured.OwnerClientId}");
                        rescueRequested = true;
                    }
                }
            }
            CoopNetworkRound round = FindFirstObjectByType<CoopNetworkRound>();
            if (!movedToSleigh && round != null && round.CurrentPhase == 1)
            {
                GameObject marker = GameObject.Find("SLED PARKING — Return Here");
                Vector3 home = marker != null ? marker.transform.position : new Vector3(0, 0, -8f);
                home.y = .05f;
                BeginMove(home + new Vector3(OwnerClientId == NetworkManager.ServerClientId ? -.8f : .8f, 0, 1.75f), "return");
                movedToSleigh = true;
            }
        }

        private void TickQuietCarry()
        {
            if (OwnerClientId != NetworkManager.ServerClientId) { TickQuietPassenger(); return; }
            TickMove();
            NetworkGiftCarryable light = FindLight();
            CoopNetworkRound round = FindFirstObjectByType<CoopNetworkRound>();
            if (quietRouteStage == 0 && !moving && !quietGripRequested && round != null && round.QuietPassengerReturnedAfterHouseVisit)
            {
                BeginMove(new Vector3(-1.1f, .05f, -4.8f), "quiet-pickup");
                quietRouteStage = 1;
            }
            if (quietRouteStage == 1 && !moving && !quietGripRequested && light != null)
            {
                light.RequestGrip(true);
                quietGripRequested = true;
                quietStageAt = Time.unscaledTime;
                Debug.Log("COOP_QUIET_GRIP_REQUEST owner=0 gift=light");
            }
            if (quietRouteStage == 1 && quietGripRequested && light != null && light.HasLocalGrip && !moving)
            {
                quietGripConfirmed = true;
                Debug.Log("COOP_QUIET_GRIP_CONFIRMED owner=0 gift=light");
                BeginMove(new Vector3(0f, .05f, 2f), "quiet-door"); quietRouteStage = 2;
            }
            if (quietRouteStage == 2 && !moving) { BeginMove(new Vector3(0f, transform.position.y, 4f), "quiet-house-entry"); quietRouteStage = 3; }
            if (quietRouteStage == 3 && !moving) { BeginMove(new Vector3(-6.3f, transform.position.y, 4f), "quiet-barrier-passage"); quietRouteStage = 4; }
            if (quietRouteStage == 4 && !moving) { BeginMove(new Vector3(-6.3f, transform.position.y, 10f), "quiet-delivery-approach"); quietRouteStage = 5; }
            if (quietRouteStage == 5 && !moving) { BeginMove(new Vector3(-4f, transform.position.y, 10f), "quiet-delivery"); quietRouteStage = 6; }
            if (quietRouteStage == 6 && !moving && round != null && round.CurrentPhase == 1)
            {
                BeginMove(new Vector3(-6.3f, transform.position.y, 10f), "quiet-return-passage"); quietRouteStage = 7;
            }
            if (quietRouteStage == 7 && !moving) { BeginMove(new Vector3(-6.3f, transform.position.y, 4f), "quiet-return-barrier"); quietRouteStage = 8; }
            if (quietRouteStage == 8 && !moving) { BeginMove(new Vector3(0f, transform.position.y, 4f), "quiet-return-house"); quietRouteStage = 9; }
            if (quietRouteStage == 9 && !moving) { BeginMove(new Vector3(0f, transform.position.y, 2f), "quiet-return-door"); quietRouteStage = 10; }
            if (quietRouteStage == 10 && !moving) { BeginMove(new Vector3(-.8f, transform.position.y, -6.25f), "quiet-return-home"); quietRouteStage = 11; }
            if (quietGripRequested && !quietGripConfirmed && !quietGripTimeoutReported && Time.unscaledTime - quietStageAt > 8f)
            {
                Debug.Log("COOP_QUIET_GRIP_TIMEOUT light=" + (light != null));
                quietGripTimeoutReported = true;
            }
        }

        private void TickQuietPassenger()
        {
            TickMove();
            if (quietRouteStage == 0 && !moving && Time.unscaledTime >= 2f)
            {
                BeginMove(new Vector3(3.5f, transform.position.y, -6.25f), "quiet-client-clear-sleigh"); quietRouteStage = 1;
            }
            if (quietRouteStage == 1 && !moving) { BeginMove(new Vector3(3.5f, transform.position.y, 2f), "quiet-client-door-approach"); quietRouteStage = 2; }
            if (quietRouteStage == 2 && !moving) { BeginMove(new Vector3(0f, transform.position.y, 2f), "quiet-client-door-entry"); quietRouteStage = 3; }
            if (quietRouteStage == 3 && !moving) { BeginMove(new Vector3(0f, transform.position.y, 4f), "quiet-client-house-entry"); quietRouteStage = 4; }
            if (quietRouteStage == 4 && !moving) { BeginMove(new Vector3(-6.3f, transform.position.y, 4f), "quiet-client-barrier-passage"); quietRouteStage = 5; }
            if (quietRouteStage == 5 && !moving) { BeginMove(new Vector3(-6.3f, transform.position.y, 10f), "quiet-client-delivery-approach"); quietRouteStage = 6; }
            if (quietRouteStage == 6 && !moving) { BeginMove(new Vector3(-4f, transform.position.y, 10f), "quiet-client-delivery"); quietRouteStage = 7; }
            if (quietRouteStage == 7 && !moving) { BeginMove(new Vector3(-6.3f, transform.position.y, 10f), "quiet-client-return-passage"); quietRouteStage = 8; }
            if (quietRouteStage == 8 && !moving) { BeginMove(new Vector3(-6.3f, transform.position.y, 4f), "quiet-client-return-barrier"); quietRouteStage = 9; }
            if (quietRouteStage == 9 && !moving) { BeginMove(new Vector3(0f, transform.position.y, 4f), "quiet-client-return-house"); quietRouteStage = 10; }
            if (quietRouteStage == 10 && !moving) { BeginMove(new Vector3(0f, transform.position.y, 2f), "quiet-client-return-door"); quietRouteStage = 11; }
            if (quietRouteStage == 11 && !moving) { BeginMove(new Vector3(3.5f, transform.position.y, 2f), "quiet-client-clear-door"); quietRouteStage = 12; }
            if (quietRouteStage == 12 && !moving) { BeginMove(new Vector3(3.5f, transform.position.y, -6.25f), "quiet-client-return-sleigh-approach"); quietRouteStage = 13; }
            if (quietRouteStage == 13 && !moving) { BeginMove(new Vector3(.8f, transform.position.y, -6.25f), "quiet-client-return-home"); quietRouteStage = 14; }
        }

        private void BeginMove(Vector3 position, string stage)
        {
            moveTarget = position;
            moveStage = stage;
            moving = true;
            lastMovePosition = transform.position;
            lastMoveProgressAt = Time.unscaledTime;
            quietStallReported = false;
            Debug.Log($"COOP_SMOKE_MOVE_BEGIN owner={OwnerClientId} stage={stage} target={position}");
        }

        private void TickMove()
        {
            if (!moving) return;
            Vector3 delta = moveTarget - transform.position;
            delta.y = 0f;
            if (delta.magnitude <= .12f)
            {
                moving = false;
                Debug.Log($"COOP_SMOKE_REPOSITION owner={OwnerClientId} stage={moveStage} position={transform.position}");
                return;
            }
            CharacterController character = GetComponent<CharacterController>();
            float speed = moveStage.StartsWith("quiet-") ? 4f : 8f;
            Vector3 step = delta.normalized * Mathf.Min(delta.magnitude, speed * Time.deltaTime);
            if (step.sqrMagnitude > .0001f) transform.rotation = Quaternion.LookRotation(new Vector3(step.x, 0f, step.z));
            if (character != null && character.enabled) character.Move(step);
            else transform.position += step;
            if ((transform.position - lastMovePosition).sqrMagnitude > .000004f)
            {
                lastMovePosition = transform.position;
                lastMoveProgressAt = Time.unscaledTime;
            }
            else if (!quietStallReported && moveStage.StartsWith("quiet-") && Time.unscaledTime - lastMoveProgressAt > 2f)
            {
                NetworkGiftCarryable carried = FindLight();
                Collider[] nearby = Physics.OverlapSphere(transform.position + Vector3.up * .9f, .65f);
                string blockers = string.Empty;
                foreach (Collider hit in nearby) blockers += (blockers.Length == 0 ? string.Empty : ",") + hit.name;
                Debug.Log($"COOP_QUIET_STALLED owner={OwnerClientId} stage={moveStage} position={transform.position} target={moveTarget} gift={(carried != null ? carried.transform.position.ToString() : "missing")} blockers={blockers}");
                quietStallReported = true;
            }
        }

        private static NetworkGiftCarryable FindHeavy()
        {
            foreach (NetworkGiftCarryable gift in FindObjectsByType<NetworkGiftCarryable>())
                if (gift != null && gift.TryGetComponent(out Rigidbody body) && body.mass >= 3f) return gift;
            return null;
        }

        private static NetworkGiftCarryable FindLight()
        {
            foreach (NetworkGiftCarryable gift in FindObjectsByType<NetworkGiftCarryable>())
                if (gift != null && gift.TryGetComponent(out Rigidbody body) && body.mass < 3f) return gift;
            return null;
        }
    }
}
