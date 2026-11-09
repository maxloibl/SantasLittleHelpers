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
        private bool movedToSleigh;
        private bool rescueRequested;
        private bool moving;
        private Vector3 moveTarget;
        private string moveStage;
        private void Update()
        {
            if (!IsOwner || !IsSpawned || !System.Array.Exists(System.Environment.GetCommandLineArgs(), arg => arg == "--coop-smoke")) return;
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
            if (!rescueRequested && self != null && !self.IsCaptured && OwnerClientId != NetworkManager.ServerClientId)
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

        private void BeginMove(Vector3 position, string stage)
        {
            moveTarget = position;
            moveStage = stage;
            moving = true;
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
            Vector3 step = delta.normalized * Mathf.Min(delta.magnitude, 8f * Time.deltaTime);
            if (character != null && character.enabled) character.Move(step);
            else transform.position += step;
        }

        private static NetworkGiftCarryable FindHeavy()
        {
            foreach (NetworkGiftCarryable gift in FindObjectsByType<NetworkGiftCarryable>())
                if (gift != null && gift.TryGetComponent(out Rigidbody body) && body.mass >= 3f) return gift;
            return null;
        }
    }
}
