using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace SantasLittleHelpers.Cabin.Greybox.Tests
{
    public sealed class CabinGreyboxPlayModeTests
    {
        private const string ScenePath = "Assets/Game/Cabin/Scenes/CozyCabin.unity";
        private const float CapsuleHeight = 1.8f;
        private const float CapsuleRadius = .32f;
        private const float EyeHeight = 1.6f;

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator CabinShell_AllowsFourCapsuleSpawnsAndExit()
        {
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return WaitForCabin();
            Physics.SyncTransforms();

            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Assert.IsNull(root.GetComponentInChildren<Camera>(true),
                    "CozyCabin must not contain a local Camera.");
                Assert.IsNull(root.GetComponentInChildren<AudioListener>(true),
                    "CozyCabin must not contain a local AudioListener.");
            }

            Transform visuals = FindPath("CabinRoot/Greybox/Visuals");
            Transform colliders = FindPath("CabinRoot/Greybox/Colliders");
            Assert.IsEmpty(visuals.GetComponentsInChildren<Collider>(true),
                "Greybox visuals must remain collider-free for replacement.");
            Assert.IsEmpty(colliders.GetComponentsInChildren<Renderer>(true),
                "Greybox colliders must remain renderer-free.");
            Assert.IsNotEmpty(colliders.GetComponentsInChildren<Collider>(true),
                "Greybox colliders are missing.");

            Transform spawnRoot = FindPath("CabinRoot/SpawnAnchors");
            Assert.AreEqual(4, spawnRoot.childCount, "There must be four ordered spawn anchors.");
            Transform missionStart = FindPath("CabinRoot/CabinIntegration/MissionStartAnchor");
            Transform departure = FindPath("CabinRoot/CabinIntegration/DepartureDirection");
            Transform promptAnchor = FindPath("CabinRoot/CabinIntegration/HostPromptTrigger/PromptAnchor");
            BoxCollider promptTrigger = FindPath("CabinRoot/CabinIntegration/HostPromptTrigger")
                .GetComponent<BoxCollider>();
            Assert.IsNotNull(promptTrigger, "HostPromptTrigger needs a BoxCollider.");
            Assert.IsTrue(promptTrigger.isTrigger, "HostPromptTrigger must be a trigger.");
            Assert.That(Vector3.Dot(departure.forward, Vector3.forward), Is.GreaterThan(.999f),
                "Departure direction must face +Z.");
            Assert.That(promptAnchor.position.y, Is.EqualTo(EyeHeight).Within(.05f),
                "Prompt anchor must sit at eye height.");

            PlayerRig rig = CreateRig();
            for (int i = 0; i < spawnRoot.childCount; i++)
            {
                Transform anchor = spawnRoot.GetChild(i);
                Assert.AreEqual("PlayerSpawn" + (i + 1), anchor.name, "Spawn anchor naming changed.");
                PlaceAndAssertClear(rig, anchor.position);
                yield return MoveTo(rig.Controller, missionStart.position, 8f);
                float distance = Vector3.Distance(
                    Vector3.Scale(rig.Controller.transform.position, new Vector3(1f, 0f, 1f)),
                    Vector3.Scale(missionStart.position, new Vector3(1f, 0f, 1f)));
                Assert.That(distance, Is.LessThan(.6f),
                    "PlayerSpawn" + (i + 1) + " has no clear capsule path to the mission start.");
            }

            PlaceAndAssertClear(rig, missionStart.position);
            yield return MoveTo(rig.Controller, departure.position + departure.forward * 2.2f, 4f);
            Assert.That(Vector3.Dot(rig.Controller.transform.position - departure.position, departure.forward), Is.GreaterThan(1.2f),
                "The 0.32 m capsule must pass the 2.6 m doorway without sticking.");
            Assert.That(rig.View.localPosition.y, Is.EqualTo(EyeHeight).Within(.001f),
                "Validation rig eye height changed.");

            Object.Destroy(rig.Root);
            Debug.Log("CABIN_GREYBOX_PLAYTEST_PASS capsules=1 height=" + CapsuleHeight +
                      " radius=" + CapsuleRadius + " eye=" + EyeHeight +
                      " spawnAnchors=4 doorway=passed");
#else
            Assert.Ignore("CozyCabin greybox validation runs in the Unity Editor play mode.");
            yield break;
#endif
        }

        private static IEnumerator WaitForCabin()
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (GameObject.Find("CabinRoot") == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(GameObject.Find("CabinRoot"), "CozyCabin scene did not load.");
        }

        private static Transform FindPath(string path)
        {
            GameObject found = GameObject.Find(path);
            Assert.IsNotNull(found, "Missing scene object: " + path);
            return found.transform;
        }

        private static PlayerRig CreateRig()
        {
            GameObject root = new GameObject("Cabin Capsule Validation Player");
            CharacterController controller = root.AddComponent<CharacterController>();
            controller.height = CapsuleHeight;
            controller.radius = CapsuleRadius;
            controller.skinWidth = .08f;
            controller.center = new Vector3(0f, CapsuleHeight * .5f, 0f);
            GameObject view = new GameObject("Eye Height");
            view.transform.SetParent(root.transform, false);
            view.transform.localPosition = new Vector3(0f, EyeHeight, 0f);
            return new PlayerRig(root, controller, view.transform);
        }

        private static void PlaceAndAssertClear(PlayerRig rig, Vector3 position)
        {
            rig.Root.transform.position = position + Vector3.up * .05f;
            rig.Root.transform.rotation = Quaternion.identity;
            Physics.SyncTransforms();
            Vector3 bottom = position + Vector3.up * .33f;
            Vector3 top = position + Vector3.up * 1.47f;
            foreach (Collider hit in Physics.OverlapCapsule(bottom, top, CapsuleRadius, ~0,
                         QueryTriggerInteraction.Ignore))
            {
                if (hit == rig.Controller || hit.transform.IsChildOf(rig.Root.transform)) continue;
                Assert.Fail("Capsule overlaps " + hit.name + " at " + position + ".");
            }
        }

        private static IEnumerator MoveTo(CharacterController controller, Vector3 target, float maxSeconds)
        {
            const float speed = 3f;
            const float step = 1f / 60f;
            float elapsed = 0f;
            while (elapsed < maxSeconds)
            {
                Vector3 delta = target - controller.transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= .01f) yield break;
                Vector3 movement = Vector3.ClampMagnitude(delta, speed * step);
                controller.Move(movement + Vector3.down * (2f * step));
                elapsed += step;
                yield return null;
            }
        }

        private readonly struct PlayerRig
        {
            public PlayerRig(GameObject root, CharacterController controller, Transform view)
            {
                Root = root;
                Controller = controller;
                View = view;
            }

            public GameObject Root { get; }
            public CharacterController Controller { get; }
            public Transform View { get; }
        }
    }
}
