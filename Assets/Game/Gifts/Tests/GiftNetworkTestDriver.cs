using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SantasLittleHelpers.Gifts.Tests
{
    /// <summary>CLI-only deterministic two-process coverage for gift grip arbitration.</summary>
    public sealed class GiftNetworkTestDriver : NetworkBehaviour
    {
        private NetworkGiftCarryable heavy;
        private NetworkGiftCarryable light;

        public override void OnNetworkSpawn()
        {
            if (IsOwner) StartCoroutine(RunOwnerSequence());
            if (IsOwner && !IsServer) StartCoroutine(LogClientReplica());
            if (IsServer)
            {
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
                StartCoroutine(LogServerState());
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null)
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        private IEnumerator RunOwnerSequence()
        {
            transform.position = new Vector3(IsServer ? -0.8f : 0.8f, 0f, -1.1f);
            float deadline = Time.unscaledTime + 20f;
            while ((heavy == null || light == null) && Time.unscaledTime < deadline)
            {
                foreach (NetworkGiftCarryable gift in FindObjectsByType<NetworkGiftCarryable>(FindObjectsSortMode.None))
                {
                    if (!gift.IsSpawned) continue;
                    if (gift.GetComponent<Rigidbody>().mass >= 3f) heavy = gift;
                    else light = gift;
                }
                yield return null;
            }
            if (heavy == null || light == null) { Debug.LogError("GIFT_TEST_SETUP_FAIL gifts were not spawned"); yield break; }
            yield return new WaitForSeconds(2.5f);
            LogRequest("duplicate-heavy-grab");
            heavy.RequestGrip(true);
            heavy.RequestGrip(true);
            yield return new WaitForSeconds(3f);
            LogRequest("release-heavy");
            heavy.RequestGrip(false);
            yield return new WaitForSeconds(2f);
            LogRequest("competing-single-light-grab");
            light.RequestGrip(true);
            light.RequestGrip(true);
            yield return new WaitForSeconds(2f);
            LogRequest("release-single-light");
            light.RequestGrip(false);
            yield return new WaitForSeconds(2f);
            if (IsServer)
            {
                LogRequest("solo-heavy-grab");
                heavy.RequestGrip(true);
                heavy.RequestGrip(true);
                StartCoroutine(MoveOwnerForLoad());
            }
            else
            {
                LogRequest("separate-light-grab");
                light.RequestGrip(true);
                yield return new WaitForSeconds(6f);
                Debug.Log("GIFT_TEST_CLIENT_EXIT disconnecting for cleanup coverage");
                Application.Quit(0);
            }
        }

        private IEnumerator LogServerState()
        {
            bool sawTwoHeavySlots = false;
            bool sawHeavyRelease = false;
            bool sawSeparateGifts = false;
            bool sawOneLightGrip = false;
            int peakLightGripCount = 0;
            float peakSoloHeavySpeed = 0f;
            float stopAt = Time.unscaledTime + 24f;
            while (true)
            {
                if (heavy != null && light != null && heavy.IsSpawned && light.IsSpawned)
                {
                    sawTwoHeavySlots |= heavy.GripCount == 2;
                    sawHeavyRelease |= heavy.GripCount == 0;
                    sawSeparateGifts |= heavy.GripCount == 1 && light.GripCount == 1;
                    sawOneLightGrip |= light.GripCount == 1 && heavy.GripCount == 0;
                    peakLightGripCount = Mathf.Max(peakLightGripCount, light.GripCount);
                    float speed = heavy.GetComponent<Rigidbody>().linearVelocity.magnitude;
                    if (heavy.GripCount == 1 && light.GripCount == 0) peakSoloHeavySpeed = Mathf.Max(peakSoloHeavySpeed, speed);
                    Vector3 heavyPosition = heavy.transform.position;
                    Vector3 lightPosition = light.transform.position;
                    Debug.Log($"GIFT_TEST_STATE heavyGrips={heavy.GripCount} lightGrips={light.GripCount} heavySpeed={speed:F3} heavyPos=({heavyPosition.x:F2},{heavyPosition.y:F2},{heavyPosition.z:F2}) lightPos=({lightPosition.x:F2},{lightPosition.y:F2},{lightPosition.z:F2}) twoSlots={sawTwoHeavySlots} released={sawHeavyRelease} lightSingle={sawOneLightGrip} lightPeak={peakLightGripCount} separate={sawSeparateGifts}");
                }
                if (Time.unscaledTime >= stopAt)
                {
                    Debug.Log($"GIFT_TEST_RESULT heavyTwoSlots={sawTwoHeavySlots} release={sawHeavyRelease} lightSingleSlot={sawOneLightGrip} lightPeak={peakLightGripCount} separateGifts={sawSeparateGifts} soloHeavySpeedPeak={peakSoloHeavySpeed:F3} soloHeavySpeedBound={peakSoloHeavySpeed <= 4.3f}");
                    NetworkManager.Shutdown();
                    yield break;
                }
                yield return new WaitForSeconds(0.5f);
            }
        }

        private IEnumerator LogClientReplica()
        {
            while (heavy == null || light == null) yield return null;
            while (true)
            {
                Vector3 heavyPosition = heavy.transform.position;
                Vector3 lightPosition = light.transform.position;
                Debug.Log($"GIFT_TEST_CLIENT_REPLICA heavyPos=({heavyPosition.x:F2},{heavyPosition.y:F2},{heavyPosition.z:F2}) lightPos=({lightPosition.x:F2},{lightPosition.y:F2},{lightPosition.z:F2})");
                yield return new WaitForSeconds(0.5f);
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer || clientId == NetworkManager.ServerClientId || light == null || heavy == null) return;
            StartCoroutine(VerifyDisconnectCleanup(clientId));
        }

        private IEnumerator VerifyDisconnectCleanup(ulong clientId)
        {
            yield return new WaitForSeconds(0.25f);
            bool autoCleared = light.GripCount == 0;
            bool forceRelease = light.ForceReleaseOnServer();
            Debug.Log($"GIFT_TEST_DISCONNECT clientId={clientId} autoCleanup={autoCleared} forceRelease={forceRelease}");
            heavy.RequestGrip(true);
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"GIFT_TEST_REGRAB afterDisconnect={heavy.GripCount == 1} heavyGrips={heavy.GripCount}");
            yield return MoveOwnerForLoad();
        }

        private IEnumerator MoveOwnerForLoad()
        {
            if (!IsOwner) yield break;
            Vector3 start = transform.position;
            Vector3 end = start + Vector3.left * 2f;
            const int steps = 40;
            for (int i = 1; i <= steps; i++)
            {
                transform.position = Vector3.Lerp(start, end, i / (float)steps);
                yield return new WaitForSeconds(0.05f);
            }
        }

        private void LogRequest(string action) => Debug.Log($"GIFT_TEST_REQUEST client={OwnerClientId} action={action}");
    }
}
