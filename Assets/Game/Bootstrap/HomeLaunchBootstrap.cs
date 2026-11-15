using System.Collections;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SantasLittleHelpers.Delivery;
using SantasLittleHelpers.Home;
using SantasLittleHelpers.Networking.Session;
using SantasLittleHelpers.Savegames;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SantasLittleHelpers.Bootstrap
{
    /// <summary>Connects the shipping Home menu to the existing DeliveryHouse mission.</summary>
    public sealed class HomeLaunchBootstrap : MonoBehaviour
    {
        private const string HomeScene = "Home";
        private const string MissionScene = "DeliveryHouse";
        private static HomeLaunchBootstrap instance;
        private readonly SessionService sessions = new SessionService();
        private CancellationTokenSource launchCancellation;
        private SaveSlotStore slotStore;
        private Guid activeSlot;
        private bool activeHostOrSolo;
        private bool returning;
        private HomeLaunchKind pendingKind;
        private Guid pendingRoundId;
        private IPAddress pendingAddress;
        private int pendingPort;
        private bool productLaunch;
        private MissionCommit pendingCommit;
        private bool hasPendingCommit;
        private string pendingNotice;
        private bool productSmokeStarted;
        private string productSmokeCase;
        private string productRole = "Solo";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            var root = new GameObject("Home Launch Bootstrap");
            instance = root.AddComponent<HomeLaunchBootstrap>();
            DontDestroyOnLoad(root);
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            string profile = GetArgumentValue("--profile=");
            slotStore = new SaveSlotStore(string.IsNullOrWhiteSpace(profile) ? null : Path.GetFullPath(profile));
            SceneManager.sceneLoaded += OnSceneLoaded;
            sessions.Changed += OnSessionChanged;
            // Keep the old MVP path available only through an explicit opt-in.
            string[] args = Environment.GetCommandLineArgs();
            bool productSmoke = Array.Exists(args, arg => arg.StartsWith("--product-smoke=", StringComparison.Ordinal));
            bool explicitLegacy = Array.Exists(args, arg => arg == "--legacy-start");
            bool legacyCli = Array.Exists(args, arg => arg == "--host" || arg == "--client" || arg == "--solo-smoke");
            if (explicitLegacy || (!Application.isEditor && !productSmoke && legacyCli))
                SceneManager.LoadScene(MissionScene, LoadSceneMode.Single);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            sessions.Changed -= OnSessionChanged;
            if (instance == this) instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == HomeScene)
            {
                HomeMenuController controller = FindAnyObjectByType<HomeMenuController>();
                if (controller != null) { controller.SetStore(slotStore); controller.Launch = LaunchAsync; }
                returning = false;
                if (!productSmokeStarted && TryGetArgumentValue("--product-smoke=", out productSmokeCase))
                    _ = StartProductSmokeAsync(controller);
                if (hasPendingCommit) _ = RetryPendingCommitAsync();
                else if (!string.IsNullOrEmpty(pendingNotice))
                {
                    FindAnyObjectByType<HomeMenuController>()?.Show(pendingNotice);
                    pendingNotice = null;
                }
                return;
            }
            if (scene.name == MissionScene && productLaunch) StartCoroutine(StartMissionAfterSceneStartup());
        }

        private IEnumerator StartMissionAfterSceneStartup()
        {
            yield return null;
            if (!productLaunch || returning) yield break;
            CoopDeliveryEntry mission = FindAnyObjectByType<CoopDeliveryEntry>();
            if (mission == null) { ReturnHome("Die Mission konnte nicht geladen werden."); yield break; }
            mission.Completed += OnMissionCompleted;
            mission.PrepareClientWorld();
            if (pendingKind == HomeLaunchKind.Solo)
            {
                activeHostOrSolo = true;
                if (!mission.BeginSoloRound(pendingRoundId)) ReturnHome("Die Solo-Mission konnte nicht gestartet werden.");
                else if (productSmokeCase == "solo-transition") mission.RunProductSoloCompletionSmoke();
            }
            else if (pendingKind == HomeLaunchKind.Host || pendingKind == HomeLaunchKind.Join)
                _ = StartNetworkAsync(mission, pendingKind, pendingAddress, pendingPort);
        }

        private async Task<HomeLaunchResult> LaunchAsync(HomeLaunchRequest request)
        {
            if (request == null || sessions.IsActive || launchCancellation != null)
                return HomeLaunchResult.Failure(HomeLaunchErrorCode.Busy, "Ein Start läuft bereits.");
            activeSlot = request.SlotId.HasValue ? request.SlotId.Value : Guid.Empty;
            productLaunch = true;
            pendingKind = request.Kind;
            pendingRoundId = Guid.NewGuid();
            pendingPort = request.Port;
            if (request.Kind == HomeLaunchKind.Join)
            {
                if (!IPAddress.TryParse(request.EndpointText, out pendingAddress) ||
                    pendingAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                    return HomeLaunchResult.Failure(HomeLaunchErrorCode.InvalidEndpoint, "Bitte eine gültige IPv4-Adresse eingeben.");
            }
            else pendingAddress = IPAddress.Any;

            launchCancellation = new CancellationTokenSource();
            try
            {
                AsyncOperation operation = SceneManager.LoadSceneAsync(MissionScene, LoadSceneMode.Single);
                if (operation == null)
                {
                    launchCancellation.Dispose(); launchCancellation = null;
                    return HomeLaunchResult.Failure(HomeLaunchErrorCode.LaunchFailed, "Die Missionsszene konnte nicht geöffnet werden.");
                }
                while (!operation.isDone) await Task.Yield();
                return HomeLaunchResult.Success();
            }
            catch (Exception exception)
            {
                launchCancellation.Dispose(); launchCancellation = null;
                return HomeLaunchResult.Failure(HomeLaunchErrorCode.LaunchFailed, exception.Message);
            }
        }

        private async Task StartNetworkAsync(CoopDeliveryEntry mission, HomeLaunchKind kind, IPAddress address, int port)
        {
            var options = new SessionOptions { ConfigureNetworkManager = manager =>
            {
                if (!mission.ConfigureNetworkManager(manager))
                    throw new InvalidOperationException("Die Missions-Netzwerkobjekte konnten nicht eingerichtet werden.");
            } };
            SessionResult result = kind == HomeLaunchKind.Host
                ? await sessions.StartHostAsync(new IPEndPoint(address, port), options, launchCancellation?.Token ?? CancellationToken.None)
                : await sessions.JoinAsync(new IPEndPoint(address, port), options, launchCancellation?.Token ?? CancellationToken.None);
            if (!result.Success)
            {
                await sessions.ShutdownAsync();
                Debug.Log($"SESSION_LAUNCH role={productRole} result=Failed error={result.Error}");
                ReturnHome(kind == HomeLaunchKind.Host ? "Hoststart fehlgeschlagen: " + result.Message : "Beitritt fehlgeschlagen: " + result.Message, result.Error.ToString());
                return;
            }
            Debug.Log($"SESSION_LAUNCH role={(kind == HomeLaunchKind.Host ? "Host" : "Guest")} result=Ready error=None");
            Debug.Log($"PRODUCT_FLOW role={(kind == HomeLaunchKind.Host ? "Host" : "Guest")} state=Mission result=Started error=None");
            activeHostOrSolo = kind == HomeLaunchKind.Host;
            if (productSmokeStarted && float.TryParse(GetArgumentValue("--leave-after="), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float seconds) && seconds > 0)
                StartCoroutine(LeaveAfter(seconds));
            if (kind == HomeLaunchKind.Host && !mission.BeginServerRound(sessions.ActiveManager, pendingRoundId))
                ReturnHome("Die Mission konnte nicht gestartet werden.");
        }

        private IEnumerator LeaveAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (productLaunch) ReturnHome("Mission verlassen.");
        }

        private async void OnMissionCompleted(MissionCompleted completed)
        {
            if (!activeHostOrSolo || activeSlot == Guid.Empty) return;
            SlotResult<Unit> saved = await slotStore.CommitMission(new MissionCommit(completed.RoundId,
                new SlotId(activeSlot), completed.MissionId, completed.CompletedAtUtc, SaveSlotStore.CurrentSchemaVersion), CancellationToken.None);
            if (saved.IsFailure)
            {
                pendingCommit = new MissionCommit(completed.RoundId, new SlotId(activeSlot), completed.MissionId,
                    completed.CompletedAtUtc, SaveSlotStore.CurrentSchemaVersion);
                hasPendingCommit = true;
                pendingNotice = "Speichern fehlgeschlagen. Home versucht den Fortschritt erneut zu sichern. " + saved.ErrorMessage;
                ReturnHome(pendingNotice);
                return;
            }
            Debug.Log($"SAVE_COMMIT role={productRole} roundId={completed.RoundId:D} slotId={activeSlot:D} missionId={completed.MissionId} result=Committed reason=None");
            ReturnHome("Mission abgeschlossen und gespeichert.");
        }

        private async Task RetryPendingCommitAsync()
        {
            MissionCommit retry = pendingCommit;
            if (!hasPendingCommit) return;
            SlotResult<Unit> result = await slotStore.CommitMission(retry, CancellationToken.None);
            if (result.IsSuccess)
            {
                pendingCommit = default;
                hasPendingCommit = false;
                pendingNotice = "Mission abgeschlossen und gespeichert.";
                FindAnyObjectByType<HomeMenuController>()?.Show(pendingNotice);
                pendingNotice = null;
            }
            else
            {
                pendingNotice = "Speichern weiterhin nicht möglich. Bitte Speicherplatz prüfen und den Startbildschirm erneut öffnen. " + result.ErrorMessage;
                FindAnyObjectByType<HomeMenuController>()?.Show(pendingNotice);
            }
        }

        private void OnSessionChanged(object sender, SessionEventArgs args)
        {
            if (!productLaunch || returning || args == null) return;
            if (args.Kind == SessionEventKind.HostLeft)
                ReturnHome("Die Verbindung wurde beendet. Du bist wieder im Startbildschirm.");
        }

        private void OnGUI()
        {
            if (returning) return;
            if (productLaunch && SceneManager.GetActiveScene().name == MissionScene &&
                Cursor.lockState != CursorLockMode.Locked &&
                GUI.Button(new Rect(16, Screen.height - 44, 180, 28), "Zurück zum Startbildschirm"))
                ReturnHome("Mission verlassen.");
            else if (hasPendingCommit && SceneManager.GetActiveScene().name == HomeScene &&
                GUI.Button(new Rect(16, Screen.height - 44, 200, 28), "Speichern erneut versuchen"))
                _ = RetryPendingCommitAsync();
        }

        private async void ReturnHome(string notice, string error = "None")
        {
            if (returning) return;
            returning = true;
            if (!hasPendingCommit) pendingNotice = notice;
            if (launchCancellation != null) { launchCancellation.Dispose(); launchCancellation = null; }
            if (sessions.IsActive) await sessions.ShutdownAsync();
            if (productSmokeStarted)
            {
                Debug.Log($"PRODUCT_FLOW role={productRole} state=Home result={(error == "None" ? "Completed" : "Failed")} error={error}");
                Debug.Log("SESSION_SHUTDOWN result=Completed managerCount=0 reason=None");
            }
            SceneManager.LoadScene(HomeScene, LoadSceneMode.Single);
            activeHostOrSolo = false;
            productLaunch = false;
        }

        private async Task StartProductSmokeAsync(HomeMenuController controller)
        {
            if (controller == null) return;
            productSmokeStarted = true;
            Application.runInBackground = true;
            bool host = Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "--host");
            bool guest = Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "--client");
            productRole = host ? "Host" : guest ? "Guest" : "Solo";
            int port = 7777;
            if (int.TryParse(GetArgumentValue("--port="), out int parsedPort)) port = parsedPort;
            Guid slotId = Guid.Empty;
            if (!guest)
            {
                SlotResult<SaveSlot> slot = await slotStore.CreateSlot("Product Smoke", CancellationToken.None);
                if (slot.IsFailure) { LogProductLaunchFailure(productRole, "StorageUnavailable"); return; }
                slotId = slot.Value.SlotId.Value;
            }

            HomeLaunchRequest request;
            if (host) request = HomeLaunchRequest.Host(slotId, "0.0.0.0", port);
            else if (guest) request = HomeLaunchRequest.Join(GetArgumentValue("--join="), port);
            else request = HomeLaunchRequest.Solo(slotId);
            HomeLaunchResult result = await controller.Launch(request);
            if (result.IsFailure)
            {
                string error = result.Error == HomeLaunchErrorCode.InvalidEndpoint ? "InvalidEndpoint" : "LaunchFailed";
                LogProductLaunchFailure(productRole, error);
                return;
            }
            if (request.Kind == HomeLaunchKind.Solo)
            {
                Debug.Log("SESSION_LAUNCH role=Solo result=Ready error=None");
                Debug.Log("PRODUCT_FLOW role=Solo state=Mission result=Started error=None");
            }
        }

        private void LogProductLaunchFailure(string role, string error)
        {
            Debug.Log($"SESSION_LAUNCH role={role} result=Failed error={error}");
            Debug.Log($"PRODUCT_FLOW role={role} state=Home result=Failed error={error}");
            Debug.Log("SESSION_SHUTDOWN result=Completed managerCount=0 reason=None");
        }

        private static string GetArgumentValue(string prefix)
        {
            foreach (string arg in Environment.GetCommandLineArgs())
                if (arg.StartsWith(prefix, StringComparison.Ordinal)) return arg.Substring(prefix.Length);
            return string.Empty;
        }

        private static bool TryGetArgumentValue(string prefix, out string value)
        {
            value = GetArgumentValue(prefix);
            return value.Length > 0;
        }
    }
}
