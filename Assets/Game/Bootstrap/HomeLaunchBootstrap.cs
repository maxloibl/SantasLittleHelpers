using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SantasLittleHelpers.Cabin;
using SantasLittleHelpers.Delivery;
using SantasLittleHelpers.Home;
using SantasLittleHelpers.Networking;
using SantasLittleHelpers.Networking.Session;
using SantasLittleHelpers.Savegames;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SantasLittleHelpers.Bootstrap
{
    public sealed class HomeLaunchBootstrap : MonoBehaviour
    {
        private const string HomeScene = "Home";

        private const string MissionScene = "DeliveryHouse";

        private const string CabinScene = "CozyCabin";

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
        private bool productSmokeFinished;

        private string productSmokeCase;

        private string productRole = "Solo";

        private CabinPhaseController cabin;

        private CabinNetworkPrefabCatalog prefabCatalog;

        private CoopDeliveryEntry mission;

        private bool startRequested;

        private bool missionStartAuthorized;

        private GameObject soloPlayer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (!(instance != null))
            {
                GameObject obj = new GameObject("Home Launch Bootstrap");
                instance = obj.AddComponent<HomeLaunchBootstrap>();
                UnityEngine.Object.DontDestroyOnLoad(obj);
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                UnityEngine.Object.Destroy(base.gameObject);
                return;
            }
            instance = this;
            string value;
            bool flag = TryGetArgumentValue("--product-smoke=", out value);
            string text = (flag ? GetArgumentValue("--profile=") : string.Empty);
            slotStore = new SaveSlotStore(string.IsNullOrWhiteSpace(text) ? null : Path.GetFullPath(text));
            SceneManager.sceneLoaded += OnSceneLoaded;
            sessions.Changed += OnSessionChanged;
            sessions.SceneEventReceived += OnNetworkSceneEvent;
            string[] commandLineArgs = Environment.GetCommandLineArgs();
            bool flag2 = Array.Exists(commandLineArgs, (string arg) => arg == "--legacy-start");
            bool flag3 = Array.Exists(commandLineArgs, (string arg) => arg == "--host" || arg == "--client" || arg == "--solo-smoke");
            if (flag2 || (!Application.isEditor && !flag && flag3))
            {
                SceneManager.LoadScene("DeliveryHouse", LoadSceneMode.Single);
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            sessions.Changed -= OnSessionChanged;
            sessions.SceneEventReceived -= OnNetworkSceneEvent;
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Update()
        {
            if (productLaunch && !returning && pendingKind == HomeLaunchKind.Join && !(sessions.ActiveManager == null) && !sessions.ActiveManager.IsServer)
            {
                CoopNetworkRound coopNetworkRound = UnityEngine.Object.FindAnyObjectByType<CoopNetworkRound>();
                if (coopNetworkRound != null && coopNetworkRound.IsTerminal)
                {
                    ReturnHome("Die Mission wurde abgeschlossen.");
                }
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Home")
            {
                HomeMenuController homeMenuController = UnityEngine.Object.FindAnyObjectByType<HomeMenuController>();
                if (homeMenuController != null)
                {
                    homeMenuController.SetStore(slotStore);
                    homeMenuController.Launch = LaunchAsync;
                }
                returning = false;
                if (!productSmokeStarted && TryGetArgumentValue("--product-smoke=", out productSmokeCase))
                {
                    StartProductSmokeAsync(homeMenuController);
                }
                if (hasPendingCommit)
                {
                    RetryPendingCommitAsync();
                }
                else if (!string.IsNullOrEmpty(pendingNotice))
                {
                    UnityEngine.Object.FindAnyObjectByType<HomeMenuController>()?.Show(pendingNotice);
                    pendingNotice = null;
                }
                return;
            }
            if (scene.name == "DeliveryHouse" && mode == LoadSceneMode.Single && productLaunch && (pendingKind == HomeLaunchKind.Host || pendingKind == HomeLaunchKind.Join))
            {
                mission = UnityEngine.Object.FindAnyObjectByType<CoopDeliveryEntry>();
                if (mission != null)
                {
                    mission.Completed -= OnMissionCompleted;
                    mission.Completed += OnMissionCompleted;
                    mission.PrepareClientWorld();
                }
            }
            if (scene.name == "DeliveryHouse" && mode == LoadSceneMode.Single && productLaunch && pendingKind == HomeLaunchKind.Solo)
            {
                StartCoroutine(StartSoloAfterSceneStartup());
            }
        }

        private IEnumerator StartSoloAfterSceneStartup()
        {
            yield return null;
            if (!productLaunch || returning)
            {
                yield break;
            }
            mission = UnityEngine.Object.FindAnyObjectByType<CoopDeliveryEntry>();
            if (mission == null)
            {
                ReturnHome("Die Mission konnte nicht geladen werden.");
                yield break;
            }
            mission.Completed += OnMissionCompleted;
            mission.PrepareClientWorld();
            activeHostOrSolo = true;
            Debug.Log("PRODUCT_FLOW role=Solo state=Mission result=Started error=None");
            if (!mission.BeginSoloRound(pendingRoundId))
            {
                ReturnHome("Die Solo-Mission konnte nicht gestartet werden.");
            }
            else if (productSmokeCase == "solo-transition" || productSmokeCase == "save-roundtrip")
            {
                mission.RunProductSoloCompletionSmoke();
            }
        }

        private async Task<HomeLaunchResult> LaunchAsync(HomeLaunchRequest request)
        {
            if (request == null || sessions.IsActive || launchCancellation != null)
            {
                return HomeLaunchResult.Failure(HomeLaunchErrorCode.Busy, "Ein Start läuft bereits.");
            }
            if (request.Kind != HomeLaunchKind.Join)
            {
                if (!request.SlotId.HasValue)
                {
                    return HomeLaunchResult.Failure(HomeLaunchErrorCode.InvalidSlot, "Bitte zuerst einen Spielstand auswählen.");
                }
                SlotResult<SaveSlot> slotResult = await slotStore.LoadSlot(new SlotId(request.SlotId.Value), CancellationToken.None);
                if (slotResult.IsFailure)
                {
                    return HomeLaunchResult.Failure(HomeLaunchErrorCode.InvalidSlot, slotResult.ErrorMessage);
                }
            }
            productRole = request.Kind == HomeLaunchKind.Host ? "Host" : request.Kind == HomeLaunchKind.Join ? "Guest" : "Solo";
            missionStartAuthorized = false;
            activeSlot = request.SlotId ?? Guid.Empty;
            productLaunch = true;
            pendingKind = request.Kind;
            pendingRoundId = Guid.NewGuid();
            pendingPort = request.Port;
            if (request.Kind == HomeLaunchKind.Join)
            {
                if (!IPAddress.TryParse(request.EndpointText, out pendingAddress) || pendingAddress.AddressFamily != AddressFamily.InterNetwork)
                {
                    return HomeLaunchResult.Failure(HomeLaunchErrorCode.InvalidEndpoint, "Bitte eine gültige IPv4-Adresse eingeben.");
                }
            }
            else
            {
                pendingAddress = IPAddress.Any;
            }
            launchCancellation = new CancellationTokenSource();
            try
            {
                if (!(await LoadSceneAsync("CozyCabin", LoadSceneMode.Single, launchCancellation.Token)))
                {
                    return FailLaunch("Die Hüttenszene konnte nicht geöffnet werden.");
                }
                SceneManager.SetActiveScene(SceneManager.GetSceneByName("CozyCabin"));
                cabin = UnityEngine.Object.FindAnyObjectByType<CabinPhaseController>();
                prefabCatalog = UnityEngine.Object.FindAnyObjectByType<CabinNetworkPrefabCatalog>();
                if (cabin == null || prefabCatalog == null)
                {
                    return FailLaunch("Hüttenszene oder Netzwerk-Prefabs fehlen.");
                }
                Debug.Log("PRODUCT_FLOW role=" + productRole + " state=Hut result=Started error=None");
                if (request.Kind == HomeLaunchKind.Solo)
                {
                    soloPlayer = UnityEngine.Object.Instantiate(prefabCatalog.PlayerPrefab);
                    if (soloPlayer == null || !cabin.TryPlaceSoloPlayer(soloPlayer.transform))
                    {
                        return FailLaunch("Der lokale Spieler konnte nicht in der Hütte platziert werden.");
                    }
                    NetworkObject component = soloPlayer.GetComponent<NetworkObject>();
                    if (component != null)
                    {
                        component.enabled = false;
                    }
                    OwnerNetworkTransform component2 = soloPlayer.GetComponent<OwnerNetworkTransform>();
                    if (component2 != null)
                    {
                        component2.enabled = false;
                    }
                    DeliveryPlayerOwnership component3 = soloPlayer.GetComponent<DeliveryPlayerOwnership>();
                    if (component3 != null)
                    {
                        component3.ActivateLocalSoloView();
                    }
                }
                else
                {
                    await StartNetworkAsync(prefabCatalog, request.Kind, pendingAddress, pendingPort);
                    if (!productLaunch || returning)
                    {
                        return HomeLaunchResult.Failure(HomeLaunchErrorCode.LaunchFailed, "Der Sitzungsstart ist fehlgeschlagen.");
                    }
                }
                return HomeLaunchResult.Success();
            }
            catch (Exception ex)
            {
                return FailLaunch(ex.Message);
            }
        }

        private HomeLaunchResult FailLaunch(string message)
        {
            if (launchCancellation != null)
            {
                launchCancellation.Dispose();
                launchCancellation = null;
            }
            productLaunch = false;
            returning = false;
            SceneManager.LoadScene("Home", LoadSceneMode.Single);
            return HomeLaunchResult.Failure(HomeLaunchErrorCode.LaunchFailed, message);
        }

        private static async Task<bool> LoadSceneAsync(string sceneName, LoadSceneMode mode, CancellationToken token)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, mode);
            if (operation == null)
            {
                return false;
            }
            while (!operation.isDone)
            {
                token.ThrowIfCancellationRequested();
                await Task.Yield();
            }
            return true;
        }

        private async Task StartNetworkAsync(CabinNetworkPrefabCatalog catalog, HomeLaunchKind kind, IPAddress address, int port)
        {
            SessionOptions sessionOptions = new SessionOptions
            {
                ConfigureNetworkManager = delegate(NetworkManager manager)
                {
                    if (!catalog.ConfigureNetworkManager(manager))
                    {
                        throw new InvalidOperationException("Die Missions-Netzwerk-Prefabs konnten nicht eingerichtet werden.");
                    }
                }
            };
            cabin.ConfigureJoinGate(sessionOptions);
            SessionResult sessionResult = ((kind != HomeLaunchKind.Host) ? (await sessions.JoinAsync(new IPEndPoint(address, port), sessionOptions, launchCancellation?.Token ?? CancellationToken.None)) : (await sessions.StartHostAsync(new IPEndPoint(address, port), sessionOptions, launchCancellation?.Token ?? CancellationToken.None)));
            SessionResult result = sessionResult;
            if (!result.Success)
            {
                await sessions.ShutdownAsync();
                Debug.Log($"SESSION_LAUNCH role={productRole} result=Failed error={result.Error}");
                ReturnHome((kind == HomeLaunchKind.Host) ? ("Hoststart fehlgeschlagen: " + result.Message) : ("Beitritt fehlgeschlagen: " + result.Message), result.Error.ToString());
                return;
            }
            Debug.Log("SESSION_LAUNCH role=" + ((kind == HomeLaunchKind.Host) ? "Host" : "Guest") + " result=Ready error=None");
            Debug.Log("SESSION_STATE state=" + ((kind == HomeLaunchKind.Host) ? "Hosting" : "Joining") + " role=" + ((kind == HomeLaunchKind.Host) ? "Host" : "Guest"));
            activeHostOrSolo = kind == HomeLaunchKind.Host;
            if (productSmokeStarted && float.TryParse(GetArgumentValue("--leave-after="), NumberStyles.Float, CultureInfo.InvariantCulture, out var result2) && result2 > 0f)
            {
                StartCoroutine(LeaveAfter(result2));
            }
            if (productSmokeStarted && kind == HomeLaunchKind.Host && float.TryParse(GetArgumentValue("--auto-mission-after="), NumberStyles.Float, CultureInfo.InvariantCulture, out var result3) && result3 > 0f)
            {
                StartCoroutine(AutoStartMissionAfter(result3));
            }
        }

        private IEnumerator AutoStartMissionAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            int expectedPlayers = productSmokeCase == "four-transition" ? 4 : 2;
            while (productLaunch && cabin != null &&
                   (!cabin.CanStartMission || sessions.ActiveManager == null ||
                    sessions.ActiveManager.ConnectedClientsList.Count < expectedPlayers))
            {
                yield return null;
            }
            StartMissionFromCabin();
        }

        private void StartMissionFromCabin()
        {
            if (!startRequested && !returning && !(cabin == null) && !(sessions.ActiveManager == null) && cabin.TryStartMission())
            {
                startRequested = true;
                Debug.Log("SESSION_STATE state=PreparingMission role=Host");
                if (sessions.ActiveManager.SceneManager.LoadScene("DeliveryHouse", LoadSceneMode.Single) != SceneEventProgressStatus.Started)
                {
                    ReturnHome("Die Missionsszene konnte nicht synchron geladen werden.", "SceneLoadFailed");
                }
            }
        }

        private async void StartSoloMission()
        {
            if (pendingKind != HomeLaunchKind.Solo || returning || startRequested ||
                SceneManager.GetActiveScene().name != CabinScene) return;
            startRequested = true;
            if (!await LoadSceneAsync(MissionScene, LoadSceneMode.Single, CancellationToken.None))
            {
                ReturnHome("Die Missionsszene konnte nicht geöffnet werden.", "SceneLoadFailed");
            }
        }

        private void OnNetworkSceneEvent(object sender, SessionSceneEventArgs args)
        {
            if (productLaunch && !returning && args != null && !(args.SceneName != "DeliveryHouse"))
            {
                if (args.EventType == SceneEventType.LoadComplete && sessions.ActiveManager != null && !sessions.ActiveManager.IsServer)
                {
                    Debug.Log("PRODUCT_FLOW role=" + productRole + " state=Mission result=Started error=None");
                }
                else if (args.EventType == SceneEventType.LoadEventCompleted && sessions.ActiveManager != null && sessions.ActiveManager.IsServer)
                {
                    StartCoroutine(StartNetworkMissionAfterSceneStartup());
                }
            }
        }

        private IEnumerator StartNetworkMissionAfterSceneStartup()
        {
            yield return null;
            if (!productLaunch || returning || missionStartAuthorized || mission == null ||
                sessions.ActiveManager == null || !sessions.ActiveManager.IsServer)
            {
                yield break;
            }
            foreach (NetworkClient connectedClients in sessions.ActiveManager.ConnectedClientsList)
            {
                Debug.Log($"SESSION_READY peerId={connectedClients.ClientId} scene=Mission roundId={pendingRoundId:D}");
            }
            missionStartAuthorized = true;
            Debug.Log($"PRODUCT_FLOW role={productRole} state=Mission result=Started error=None");
            if (!mission.BeginServerRound(sessions.ActiveManager, pendingRoundId))
            {
                ReturnHome("Die Mission konnte nicht gestartet werden.", "SceneLoadFailed");
            }
        }

        private IEnumerator LeaveAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (productLaunch)
            {
                ReturnHome("Sitzung verlassen.", "Disconnected");
            }
        }

        private async void OnMissionCompleted(MissionCompleted completed)
        {
            if (activeHostOrSolo && !(activeSlot == Guid.Empty))
            {
                SlotResult<Unit> slotResult = await slotStore.CommitMission(new MissionCommit(completed.RoundId, new SlotId(activeSlot), completed.MissionId, completed.CompletedAtUtc, SaveSlotStore.CurrentSchemaVersion), CancellationToken.None);
                if (slotResult.IsFailure)
                {
                    pendingCommit = new MissionCommit(completed.RoundId, new SlotId(activeSlot), completed.MissionId, completed.CompletedAtUtc, SaveSlotStore.CurrentSchemaVersion);
                    hasPendingCommit = true;
                    pendingNotice = "Speichern fehlgeschlagen. Home versucht den Fortschritt erneut zu sichern. " + slotResult.ErrorMessage;
                    ReturnHome(pendingNotice);
                }
                else
                {
                    Debug.Log($"SAVE_COMMIT role={productRole} roundId={completed.RoundId:D} slotId={activeSlot:D} missionId={completed.MissionId} result=Committed reason=None");
                    ReturnHome("Mission abgeschlossen und gespeichert.");
                }
            }
        }

        private async Task RetryPendingCommitAsync()
        {
            MissionCommit commit = pendingCommit;
            if (hasPendingCommit)
            {
                SlotResult<Unit> slotResult = await slotStore.CommitMission(commit, CancellationToken.None);
                if (slotResult.IsSuccess)
                {
                    pendingCommit = default(MissionCommit);
                    hasPendingCommit = false;
                    pendingNotice = "Mission abgeschlossen und gespeichert.";
                    UnityEngine.Object.FindAnyObjectByType<HomeMenuController>()?.Show(pendingNotice);
                    pendingNotice = null;
                }
                else
                {
                    pendingNotice = "Speichern weiterhin nicht möglich. Bitte Speicherplatz prüfen und den Startbildschirm erneut öffnen. " + slotResult.ErrorMessage;
                    UnityEngine.Object.FindAnyObjectByType<HomeMenuController>()?.Show(pendingNotice);
                }
            }
        }

        private void OnSessionChanged(object sender, SessionEventArgs args)
        {
            if (productLaunch && !returning && args != null && args.Kind == SessionEventKind.HostLeft)
            {
                ReturnHome("Die Verbindung wurde beendet. Du bist wieder im Startbildschirm.", "HostLeft");
            }
        }

        private void OnGUI()
        {
            if (returning) return;

            string scene = SceneManager.GetActiveScene().name;
            bool cursorAvailable = Cursor.lockState != CursorLockMode.Locked;
            Rect returnButton = new Rect(16, Screen.height - 48, 224, 32);
            if (productLaunch && scene == CabinScene)
            {
                bool solo = pendingKind == HomeLaunchKind.Solo;
                bool host = pendingKind == HomeLaunchKind.Host;
                if (solo || host)
                {
                    bool previousEnabled = GUI.enabled;
                    GUI.enabled = !startRequested && (solo || (cabin != null && cabin.CanStartMission));
                    bool start = GUI.Button(new Rect(16, Screen.height - 88, 224, 32), "Mission starten");
                    GUI.enabled = previousEnabled;
                    if (cursorAvailable && start)
                    {
                        if (solo) StartSoloMission();
                        else StartMissionFromCabin();
                        return;
                    }
                }
                if (!cursorAvailable)
                {
                    GUI.Label(new Rect(16, Screen.height - 48, 280, 32), "Esc für Maus und Hüttenmenü");
                    return;
                }
                if (GUI.Button(returnButton, "Zurück zum Startbildschirm"))
                    ReturnHome("Sitzung verlassen.");
                return;
            }

            if (productLaunch && scene == MissionScene && cursorAvailable &&
                GUI.Button(returnButton, "Zurück zum Startbildschirm"))
            {
                ReturnHome("Mission verlassen.");
            }
            else if (hasPendingCommit && scene == HomeScene &&
                     GUI.Button(returnButton, "Speichern erneut versuchen"))
            {
                _ = RetryPendingCommitAsync();
            }
        }

        private async void ReturnHome(string notice, string error = "None")
        {
            if (!returning)
            {
                returning = true;
                if (!hasPendingCommit)
                {
                    pendingNotice = notice;
                }
                if (launchCancellation != null)
                {
                    launchCancellation.Dispose();
                    launchCancellation = null;
                }
                if (sessions.IsActive)
                {
                    await sessions.ShutdownAsync();
                }
                cabin = null;
                mission = null;
                prefabCatalog = null;
                startRequested = false;
                activeHostOrSolo = false;
                productLaunch = false;
                await LoadSceneAsync(HomeScene, LoadSceneMode.Single, CancellationToken.None);
                if (productSmokeStarted && !productSmokeFinished)
                {
                    productSmokeFinished = true;
                    Debug.Log("PRODUCT_FLOW role=" + productRole + " state=Home result=" + ((error == "None") ? "Completed" : "Failed") + " error=" + error);
                    int managers = UnityEngine.Object.FindObjectsByType<NetworkManager>(FindObjectsInactive.Include,
                        FindObjectsSortMode.None).Length;
                    Debug.Log(managers == 0
                        ? "SESSION_SHUTDOWN result=Completed managerCount=0 reason=None"
                        : "SESSION_SHUTDOWN result=Failed managerCount=1 reason=Disconnected");
                    Application.Quit();
                }
            }
        }

        private async Task StartProductSmokeAsync(HomeMenuController controller)
        {
            if (controller == null)
            {
                return;
            }
            productSmokeStarted = true;
            Application.runInBackground = true;
            bool host = Array.Exists(Environment.GetCommandLineArgs(), (string arg) => arg == "--host");
            bool guest = Array.Exists(Environment.GetCommandLineArgs(), (string arg) => arg == "--client");
            productRole = (host ? "Host" : (guest ? "Guest" : "Solo"));
            int port = 7777;
            if (int.TryParse(GetArgumentValue("--port="), out var result))
            {
                port = result;
            }
            if (productSmokeCase == "read-slot")
            {
                if (Guid.TryParse(GetArgumentValue("--slot="), out var savedSlot))
                {
                    SlotResult<SaveSlot> slotResult = await slotStore.LoadSlot(new SlotId(savedSlot), CancellationToken.None);
                    Debug.Log(string.Format("SAVE_RELOAD slotId={0:D} result={1} commits={2}", savedSlot, slotResult.IsSuccess ? "Loaded" : "Failed", slotResult.IsSuccess ? slotResult.Value.CommitLedger.Count : 0));
                }
                return;
            }
            Guid slotId = Guid.Empty;
            if (!guest)
            {
                if (productSmokeCase == "save-roundtrip")
                {
                    SlotResult<System.Collections.Generic.IReadOnlyList<SaveSlotSummary>> existing =
                        await slotStore.ListSlots(CancellationToken.None);
                    if (existing.IsSuccess && existing.Value.Count > 0)
                        slotId = existing.Value[0].SlotId.Value;
                }
                if (slotId == Guid.Empty)
                {
                    SlotResult<SaveSlot> created = await slotStore.CreateSlot("Product Smoke", CancellationToken.None);
                    if (created.IsFailure)
                    {
                        LogProductLaunchFailure(productRole, "StorageUnavailable");
                        return;
                    }
                    slotId = created.Value.SlotId.Value;
                }
            }
            HomeLaunchRequest request = (host ? HomeLaunchRequest.Host(slotId, "0.0.0.0", port) : ((!guest) ? HomeLaunchRequest.Solo(slotId) : HomeLaunchRequest.Join(GetArgumentValue("--join="), port)));
            HomeLaunchResult homeLaunchResult = await controller.Launch(request);
            if (homeLaunchResult.IsFailure)
            {
                string error = ((homeLaunchResult.Error == HomeLaunchErrorCode.InvalidEndpoint) ? "InvalidEndpoint" : "LaunchFailed");
                if (!returning && !productSmokeFinished) LogProductLaunchFailure(productRole, error);
            }
            else if (request.Kind == HomeLaunchKind.Solo)
            {
                Debug.Log("SESSION_LAUNCH role=Solo result=Ready error=None");
                if (productSmokeCase == "solo-transition" || productSmokeCase == "save-roundtrip")
                {
                    StartSoloMission();
                }
            }
        }

        private void LogProductLaunchFailure(string role, string error)
        {
            productSmokeFinished = true;
            Debug.Log("SESSION_LAUNCH role=" + role + " result=Failed error=" + error);
            if (error == "InvalidEndpoint")
            {
                Debug.Log("SESSION_JOIN_REJECT reason=InvalidEndpoint");
            }
            Debug.Log("PRODUCT_FLOW role=" + role + " state=Home result=Failed error=" + error);
            Debug.Log("SESSION_SHUTDOWN result=Completed managerCount=0 reason=None");
            Application.Quit();
        }

        private static string GetArgumentValue(string prefix)
        {
            string[] commandLineArgs = Environment.GetCommandLineArgs();
            foreach (string text in commandLineArgs)
            {
                if (text.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return text.Substring(prefix.Length);
                }
            }
            return string.Empty;
        }

        private static bool TryGetArgumentValue(string prefix, out string value)
        {
            value = GetArgumentValue(prefix);
            return value.Length > 0;
        }
    }
}
