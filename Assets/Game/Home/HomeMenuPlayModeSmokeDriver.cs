using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SantasLittleHelpers.Savegames;
using SantasLittleHelpers.Settings;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SantasLittleHelpers.Home
{
    /// <summary>
    /// Isolated PlayMode harness for the real home scene. It uses a real SaveSlotStore
    /// below the worktree's ignored Logs folder and a fake launch delegate only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomeMenuPlayModeSmokeDriver : MonoBehaviour
    {
        private const string SmokeArgument = "--home-menu-smoke";
        private readonly List<HomeLaunchRequest> requests = new List<HomeLaunchRequest>();
        private HomeMenuController controller;
        private HomeLaunchRequest requestLast;
        private string root;
        private int cases;

        private void Start()
        {
            if (!HasArgument(SmokeArgument))
            {
                enabled = false;
                return;
            }

            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return null;
            yield return null;

            bool passed;
            string failure = string.Empty;
            try
            {
                passed = RunChecks(out failure);
            }
            catch (Exception exception)
            {
                passed = false;
                failure = exception.GetType().Name + ": " + exception.Message;
                Debug.LogException(exception);
            }

            if (Directory.Exists(root))
            {
                try
                {
                    Directory.Delete(root, true);
                }
                catch (Exception)
                {
                }
            }

            Debug.Log($"HOME_MENU_PLAY_SMOKE_{(passed ? "PASS" : "FAIL")} cases={cases} " +
                      $"requests={requests.Count} root={root} failure={failure}");
            Quit(passed ? 0 : 1);
        }

        private bool RunChecks(out string failure)
        {
            failure = string.Empty;

            root = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Logs",
                "home-menu-playmode",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            SaveSlotStore slotStore = new SaveSlotStore(Path.Combine(root, "slots"));
            LocalSettingsStore settingsStore = new LocalSettingsStore(Path.Combine(root, "settings.json"));
            SettingsResult<LocalSettings> settings = LocalSettingsRuntime.InitializeAsync(settingsStore)
                .GetAwaiter()
                .GetResult();
            Require(settings.IsSuccess, "isolated settings initialize");

            controller = FindAnyObjectByType<HomeMenuController>();
            Require(controller != null, "home scene contains HomeMenuController");
            controller.SetStore(slotStore);
            controller.Launch = FakeLaunch;
            controller.Show("PlayMode-Smoke startet.");

            HomeMenuModel model = controller.Model;
            Require(model != null, "controller exposes model");
            model.InitializeAsync().GetAwaiter().GetResult();
            Require(model.Slots.Count == 0, "empty real store starts empty");
            Require(model.StatusMessage.Contains("keine Spielstände", StringComparison.OrdinalIgnoreCase),
                "empty state is visible");
            cases++;

            SlotResult<SaveSlot> created = model.CreateSlotAsync("PlayMode Hütte").GetAwaiter().GetResult();
            Require(created.IsSuccess, "real store creates slot");
            Require(model.SelectedSlotId == created.Value.SlotId.Value, "created slot is selected");
            Require(model.HasLoadableSelection, "created slot is loadable");
            cases++;

            UIDocument document = controller.GetComponent<UIDocument>();
            Require(document != null && document.rootVisualElement != null, "UIDocument has a live root");
            Require(document.rootVisualElement.childCount > 0, "programmatic UI Toolkit view is built");

            HomeLaunchResult solo = model.LaunchSoloAsync().GetAwaiter().GetResult();
            Require(solo.IsSuccess, "solo launch with fake succeeds");
            Require(requestLast.Kind == HomeLaunchKind.Solo && requestLast.SlotId.HasValue,
                "solo request is typed and carries the slot");
            Require(document.rootVisualElement.style.display.value == DisplayStyle.None,
                "successful launch hides the home view");
            cases++;

            controller.Show("Zurück in Home.");
            Require(document.rootVisualElement.style.display.value == DisplayStyle.Flex,
                "Show makes the home view visible");
            model.SetEndpointText("192.168.1.50");
            model.SetPortText("7798");
            HomeLaunchResult join = model.LaunchJoinAsync().GetAwaiter().GetResult();
            Require(join.IsSuccess && requestLast.Kind == HomeLaunchKind.Join && !requestLast.SlotId.HasValue,
                "join launch is typed without a slot");
            cases++;

            controller.Show("Fehlerzustand prüfen.");
            model.SetEndpointText("300.1.1.1");
            HomeLaunchResult invalid = model.LaunchJoinAsync().GetAwaiter().GetResult();
            Require(invalid.IsFailure && invalid.Error == HomeLaunchErrorCode.InvalidEndpoint,
                "invalid IPv4 is rejected visibly");
            Require(model.StatusKind == HomeStatusKind.Error, "invalid endpoint shows an error status");
            cases++;

            string corruptPath = slotStore.GetSlotPath(created.Value.SlotId);
            File.WriteAllText(corruptPath, "corrupt on purpose");
            model.RefreshSlotsAsync().GetAwaiter().GetResult();
            Require(model.Slots.Count == 1 && model.Slots[0].Status == SaveSlotStatus.CorruptData,
                "corrupt record remains visible per record");
            Require(!model.CanLaunch, "corrupt record blocks Solo/Host");
            cases++;

            model.OpenSettings();
            model.PreviewMasterVolume(0.27f);
            SettingsResult<SantasLittleHelpers.Settings.Unit> applied = model.ApplySettingsAsync()
                .GetAwaiter()
                .GetResult();
            Require(applied.IsSuccess, "settings draft applies through LocalSettingsRuntime");
            Require(File.Exists(settingsStore.FilePath), "settings save stays inside Logs");
            cases++;

            return true;
        }

        private Task<HomeLaunchResult> FakeLaunch(HomeLaunchRequest request)
        {
            requests.Add(request);
            requestLast = request;
            return Task.FromResult(HomeLaunchResult.Success("PlayMode-Fake bereit."));
        }

        private static bool HasArgument(string value)
        {
            return Array.Exists(Environment.GetCommandLineArgs(), argument => argument == value);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void Quit(int exitCode)
        {
#if UNITY_EDITOR
            EditorApplication.Exit(exitCode);
#else
            Application.Quit(exitCode);
#endif
        }
    }
}
