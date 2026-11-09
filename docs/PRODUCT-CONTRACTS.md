# Produktverträge v1

Stand: 9. November 2026
Basis: MVP-Abnahme #16, `main` @ `712cf291de5c086fb0a1fb3a4cb3db886bddd38a`
Unity: `6000.6.8f1` · NGO `2.13.3` (Pre-release laut `docs/NETCODE-SPIKE.md`)

Dieses Dokument legt die Verträge fest, die UI, lokaler Speicher, Sitzung und Mission bei der Erweiterung des MVP einhalten. Es ist eine Implementierungsgrundlage, keine Behauptung, dass die beschriebenen Home-, Hütte- und Missionsszenen schon existieren. Heute liegt der spielbare Fluss in `DeliveryHouse`; Solo/Host/Client werden dort angeboten. Die beschriebenen Produktannahmen sind v1 und können nur über ein eigenes Produkt- oder Architektur-Issue geändert werden.

## Verbindlicher MVP-Rahmen und Produktannahmen

- Unity `6000.6.8f1`, NGO `2.13.3` (Pre-release), Unity Transport `6.6.0` (Editor-Builtin) bleiben fest. Paket- oder Editorwechsel sind außerhalb dieses Vertrags.
- CLI-Smokes, maximal vier Spieler (Host plus höchstens drei Clients) und heutige Missionsregeln bleiben erhalten: ein leichtes Pflichtgeschenk liefern, Rückkehr zum Schlitten; Koop-Freigabe durch Host erst ab mindestens zwei verbundenen Spielern. Solo hat einen eigenen Startpfad und verlangt keine Sitzung.
- Join nutzt direkte IPv4 plus UDP (Standardport `7777`); kein Relay, Matchmaking, NAT-Traversal oder IPv6 in v1. Ein Join ist nur in der Hütte vor Missionsbeginn möglich. Beim Missionsstart werden Join-Versuche verständlich mit `SessionClosed` abgelehnt; es gibt keinen späten Eintritt in die laufende Mission.
- Der Host ist Sitzungs- und Fortschrittseigentümer. Ein Beitretender hat keinen lokalen Spielstand-Slot für diese Runde und schreibt keine Missionsdaten. Verlassen/Abbruch führt über die Hütte zurück zu Home; ein erneuter Start ist eine neue Runde mit neuer ID.
- v1 verwendet eine Home-Szene, eine Hütte/Lobby-Szene und eine Mission-Szene. Vor Aktivierung der Sitzung sind alle benötigten Welten lokal geladen. Szenennamen und UI-Layouts sind Implementierungsdetails; Zustandsübergänge und Verträge hier sind bindend.

## Zustandsmaschine und Aufrufreihenfolge

```text
Home
 ├─ Solo ───────────────> Hütte (lokal, Profil/Slot gewählt) ─> Mission ─> Home
 ├─ Host ───────────────> Hütte (Sitzung offen, Slot gehört Host)
 │                           └─ Startfreigabe ab 2 Spielern ─> Mission ─> Home
 └─ Join (IPv4:Port) ───> Hütte (Gast, ohne Slot)
                             └─ Host startet / Host verlässt ─> Home
```

1. `Home` liest Profileinstellungen über den von #37 verantworteten Settings-Vertrag und Host-Slots über den von #36 verantworteten Slot-Vertrag. Ein Slotfehler (beschädigt, nicht lesbar, Schema inkompatibel) blockiert Solo/Host mit Wiederholen, anderen Slot wählen oder zurück; es gibt keinen stillen Slot und keinen Modus ohne Speichern.
2. Solo: Spieler wählt Host-eigenen Slot und übergibt dessen `SlotId` an den von #39 verantworteten Session-Service. Nach erfolgreicher lokaler Szenenladung startet die Mission über den Adapter von #40.
3. Host: Slot wählen, `StartHost(slotId, endpoint)`; Hütte und Mission werden lokal additiv geladen, Mission bleibt inaktiv. Erst nach bestätigter lokaler Ladung wird der eine NetworkManager gestartet. `SessionOpened` bedeutet: Host ist verbindungsbereit, Mission läuft noch nicht. Die Mission lädt nicht erneut bei Host-Start.
4. Join: IPv4/Port validieren, Hütte und inaktive Mission lokal additiv laden und erst danach `Join(endpoint)` ausführen. `JoinAccepted` bestätigt die Anmeldung in der Hütte. Fehlende Sitzung, ungültige Adresse, Timeout, volle Sitzung und geschlossene Anmeldung sind unterscheidbare Fehler. Bei Scene-Load-Fehler wird lokal kein Join gestartet.
5. Host drückt „Mission starten“. UI fordert `RequestStartMission`; Session lehnt mit `NotHost`, `TooFewPlayers` (unter 2), `AlreadyStarted` oder `NotReady` ab. Beim Beginn des Startversuchs schließt Session den Join-Eingang und friert die Menge aktueller Peers ein. Alle müssen die lokal vorab geladene Mission als bereit bestätigen. Disconnect entfernt einen Peer aus der Menge; fällt die Sitzung unter zwei Spieler, wird der Versuch ohne Start abgebrochen und die Hütte kann erneut geöffnet werden. Nur nach erfüllter Bereitschaft autorisiert der Server Start und aktiviert Mission-Gameplay über den replizierten Sitzungs-/Rundenzustand. Cancel oder Timeout autorisiert nie Start, hält Gameplay inaktiv und kehrt zur Hütte zurück; der Host kann den Start wiederholen oder die Sitzung schließen.
6. Mission besitzt Rundenregeln. Ein einziger `Completed`-Übergang wird ausgelöst, wenn bestehende Missionsregeln Erfolg bestätigen (Lieferung und Rückkehr); er ersetzt weder vorhandene Liefer-/Rückkehr-Handler noch CLI-Smokes. Client-Anfragen werden serverseitig validiert. Der Integrationsadapter von #41 übergibt `Completed` an den Host-Slot-Commit. Fehlschlag oder Abbruch committen nicht.
7. `ReturnToHome` stoppt Gameplay und Sitzung, führt `NetworkManager.Shutdown` aus, wartet auf Abschluss, entlädt Mission/Hütte und lädt Home. Abbruch, Host-Ende oder Disconnect sperrt Gameplay sofort, baut den lokalen Manager explizit ab und führt nach Home mit verständlicher Meldung. Ein Missionslade-Timeout bricht in die Hütte zurück; dort kann der Host erneut versuchen oder sauber beenden. Shutdown-Timeout wird als `ShutdownFailed` angezeigt und verhindert eine zweite Manager-Instanz.

Fehlerzustände sind sichtbare, typisierte Ergebnisse und nie nur Logmeldungen: `InvalidSlot`, `StorageUnavailable`, `InvalidEndpoint`, `BindFailed`, `ConnectionTimeout`, `SessionNotFound`, `SessionFull`, `SessionClosed`, `NotHost`, `TooFewPlayers`, `NotReady`, `SceneLoadFailed`, `HostLeft`, `Disconnected`, `AlreadyStarted`, `CommitFailed`. Hütte bietet nach transienten Fehlern Wiederholen/Zurück; Mission bietet bei Abbruch keine Fortschrittsgutschrift und Rückkehr zu Home.

## Typisierte Verträge und Eigentümer

Die folgenden Contracts haben getrennte Issue-Eigentümer und dürfen keine gemeinsam benötigte Schnittstellendatei voraussetzen. #36 besitzt Slot-Typen und Slot-Store-API; #37 besitzt Profil-/Settings-Typen und Settings-API; #39 besitzt Session-Typen/-API; #40 besitzt Mission-Adapter-Typen/-API. #41 integriert die Typen und darf dafür Übersetzungsadapter in eigenen Dateien hinzufügen. Jedes Issue kann so separat auf `main` kompilieren. Die Signaturen sind vorgeschlagene v1-Verträge; konkrete `Result<T>`/Error-Typen gehören dem jeweiligen Eigentümer, nicht einem neuen gemeinsamen Modul.

```csharp
// #36: SaveSlotStore owns only host slots and completed mission IDs.
Task<SlotResult<IReadOnlyList<SaveSlotSummary>>> ListSlots(CancellationToken ct);
Task<SlotResult<SaveSlot>> LoadSlot(SlotId slotId, CancellationToken ct);
Task<SlotResult<SaveSlot>> CreateSlot(string displayName, CancellationToken ct);
Task<SlotResult<Unit>> RenameSlot(SlotId slotId, string displayName, CancellationToken ct);
Task<SlotResult<Unit>> DeleteSlot(SlotId slotId, CancellationToken ct);
Task<SlotResult<Unit>> CommitMission(MissionCommit commit, CancellationToken ct);

// #37: LocalSettingsStore owns only settings (including mouse sensitivity).
Task<SettingsResult<LocalSettings>> LoadSettings(CancellationToken ct);
Task<SettingsResult<Unit>> SaveSettings(LocalSettings settings, CancellationToken ct);

// #39: SessionService owns NGO/UTP, NetworkManager, peers, lobby join-gate, scenes.
SessionSnapshot Current { get; }
event Action<SessionEvent> Changed;
Task<SessionResult<SessionSnapshot>> StartSolo(SlotId slotId, CancellationToken ct);
Task<SessionResult<SessionSnapshot>> StartHost(SlotId slotId, IPEndPoint endpoint, CancellationToken ct);
Task<SessionResult<SessionSnapshot>> Join(IPEndPoint endpoint, CancellationToken ct);
Task<SessionResult<Unit>> RequestStartMission(CancellationToken ct);
Task<SessionResult<Unit>> ReturnToHome(CancellationToken ct);

// #40: MissionAdapter owns the bridge to existing rules, not replacement handlers.
MissionSnapshot Current { get; }
event Action<MissionEvent> Changed; // one terminal Completed or Failed/Abandoned per round
Task<MissionResult<Unit>> Begin(MissionContext context, CancellationToken ct);
Task<MissionResult<Unit>> Cancel(CancellationToken ct);
// Existing delivery/return code remains authoritative; adapter observes validated transitions.
event Action<MissionCompleted> Completed;
```

`LocalSettings` (#37): `schemaVersion`, Audio-/Anzeigeoptionen, Maussensitivität und optionale letzte Auswahl; keine Slot- oder Missionszustände. `SaveSlot` (#36): stabile zufällige UUID-`SlotId`, `schemaVersion`, Name, Erstellungszeitpunkt, `completedMissionIds` (stabile Missions-IDs; Duplikate ausgeschlossen) sowie ein Commit-Ledger aus `roundId` und `missionId`, damit Idempotenz auch nach Prozessabbruch erhalten bleibt. Slot-IDs werden nie wiederverwendet. Slot-Dateien sind unabhängig, nur lokal; inkompatible Versionen liefern `UnsupportedSchema` und werden nicht still überschrieben. #36 und #37 legen ihre getrennten atomaren Dateiformate jeweils selbst fest.

`MissionCommit` (#36): `Guid roundId`, `SlotId slotId`, `string missionId`, `DateTimeOffset completedAtUtc`, `int schemaVersion`. `roundId` wird beim Rundenerzeugen zufällig erzeugt, ist für die ganze Runde stabil und gilt global eindeutig. `CommitMission` fügt missionId nur hinzu, wenn `roundId` noch nicht verarbeitet wurde; gleiches roundId und gleicher Inhalt ist erfolgreich-idempotent, gleiche ID mit abweichendem Inhalt ergibt `CommitConflict`. Nur #41 ruft den Slot-Store für den aktiven Host-Slot. Bestätigter Erfolg ist das servervalidierte einmalige `Completed` der bestehenden Liefer-/Rückkehrregeln; Disconnect, Niederlage, Abbruch, Gaststatus oder UI-Meldung committen nicht.

Gespeichert wird nur abgeschlossenes Missionsfortschritt und lokale Settings. Niemals gespeichert werden laufende Physik, Positionen, Geschenke im Flug, NPC-/Alarm-/Capture-Zustand, NGO-IDs, Spielerzahl, Sitzung, Netzwerkzustand oder Teilfortschritt. Ein Neustart beginnt an einer definierten Missionseinstiegsposition mit frischem Missionszustand.

`SessionSnapshot.state`: `Idle | LoadingHut | Hosting | Joining | HutReady | PreparingMission | MissionReady | Running | Returning | Failed | Stopped`; Eigenschaften `role: Solo | Host | Guest`, `playerCount`, `maxPlayers=4`, `acceptingJoins`, `roundId?`, `error?`. Nur `SessionService` setzt Sitzungszustand. `SessionEvent`: `StateChanged`, `PeerJoined`, `PeerLeft`, `JoinRejected`, `MissionLoadStarted`, `PeerMissionReady`, `MissionStartAuthorized`, `HostLeft`, `TransportFailed`, `ShutdownCompleted`.

`MissionSnapshot.state`: `NotStarted | Loading | Delivering | Returning | Completed | Failed | Abandoned`; Werte `roundId`, `missionId`, `deliveredCount`, `requiredCount`, `freePlayersReturned`, `error?`. Der bestehende Missions-Regelbesitzer setzt autoritativen Regelzustand; #40 beobachtet ihn und meldet genau ein terminales Ereignis. `MissionEvent`: `GiftDelivered`, `PlayerCaptured`, `PlayerFreed`, `ReturnProgressChanged`, `Completed`, `Failed`, `Abandoned`. Ereignisse sind Fakten nach Validierung; UI darf Ereignisse darstellen, nie Regelzustand ableiten oder mutieren.

### Zuständigkeit je Schnittstelle

| Vertrag | Einziger Eigentümer | Darf nicht besitzen |
| --- | --- | --- |
| UI-Aktionen, Anzeigen, Fehlertext | UI | Transport, Dateien, Missionsregeln |
| Host-Slots/Commit-Idempotenz | #36 `SaveSlotStore` | lokale Settings, NGO-Objekte, Szenenladung |
| Lokale Optionen inkl. Maussensitivität | #37 `LocalSettingsStore` | Slots, Rundenzustand |
| Peerstatus, NetworkManager, Join-Gate, Netz-Szenenbarriere | #39 `SessionService` | Liefer-/Capture-Regeln, Speicherformat |
| Beobachtung bestehender Missionsregeln, einmaliger `Completed` | #40 `MissionAdapter` | Ersatz für Liefer-/Rückkehr-Handler, UI, Transport |
| Save-Mutation nach `Completed` | #41 Integrationsbrücke ruft als Host #36 auf | Gäste/Client, UI |

## Testbarkeit mit Fakes

Jedes Issue definiert seine Fakes lokal oder nutzt bereits eingecheckte öffentliche Typen; kein vorgezogenes gemeinsames Interface ist Voraussetzung. #36 faked Dateisystem, #37 Einstellungen, #39 `ISessionTransport`/`ISceneLoader`, #40 die vorhandenen Missionsregeln. UI von #38 testet mit lokalen Fake-Adaptern; #41 testet Übersetzung und Reihenfolge. EditMode prüft Übergänge, Fehler, Cancel und Doppelauslieferung ohne echte Verbindung. PlayMode nutzt isolierte Szenen; nur Integrations-Smokes binden NGO und echte Hütte/Mission ein. Fakes ersetzen keine bestehenden CLI-Smokes.

Für CLI-/Integrations-Smokes standardisieren Implementierungen diese maschinenlesbaren Signale, immer mit `roundId` bei Rundenereignissen und `peerId` wo zutreffend:

```text
PRODUCT_FLOW role=Solo|Host|Guest state=Home|Hut|Mission|Returning|Home result=Started|Completed|Failed error=<code>
SESSION_LAUNCH role=Solo|Host|Guest result=Ready|Failed error=<code>
SESSION_STATE state=Idle|LoadingHut|Hosting|Joining|HutReady|PreparingMission|MissionReady|Running|Returning|Failed|Stopped role=Solo|Host|Guest
SESSION_JOIN_REJECT reason=SessionClosed|SessionFull|SessionNotFound|InvalidEndpoint|NotReady
SESSION_READY peerId=<id> scene=Mission roundId=<guid>
MISSION_START_AUTHORIZED roundId=<guid> players=<n>
MISSION_EVENT type=GiftDelivered|PlayerCaptured|PlayerFreed|Completed|Failed|Abandoned roundId=<guid> peerId=<id>
SAVE_COMMIT role=Host roundId=<guid> slotId=<guid> missionId=<id> result=Committed|AlreadyCommitted|Rejected reason=<reason>
SESSION_SHUTDOWN result=Completed|Failed managerCount=0|1 reason=<code>
```

Die oben genannten neuen `--...` Optionen in diesen Beispielen sind **provisorische Vorschläge** für #50, keine bereits implementierten Kommandozeilen-Flags; bestehende Flags und Marker bleiben unverändert. `--profile=<isolated-id>` für getrennte temporäre Smoke-Daten ist ebenfalls nur ein Vorschlag. Jeder Startpfad protokolliert genau ein `SESSION_LAUNCH`: `Ready` nur nach bestätigter Bereitschaft, sonst `Failed` mit maschinenlesbarem Fehler. Bestehende `SOLO_SMOKE_*`, `COOP_*`, `GIFT_*` Marker werden nicht umbenannt und ihre Aktionen nicht dupliziert.

Erfolgskriterium für v1-Smoke: Host plus bis zu drei Clients, `SESSION_READY` für alle beim Autorisierungszeitpunkt, bevor genau ein `MISSION_START_AUTHORIZED`; spätere Joiner werden abgelehnt. Bei Start-Cancel, Peer-Abbruch oder Scene-Load-Timeout wird nicht autorisiert, noch nicht bereite/abgebrochene Peers werden aus der aktuellen Bereitschaftsmenge entfernt, verbleibende aktuelle Peers gehen zurück zur Hütte und können erneut versuchen. Host-Commit genau einmal, Gast ohne Save-Aufruf, join nach Autorisierung `SESSION_JOIN_REJECT reason=SessionClosed`, expliziter Shutdown mit `managerCount=0`. Solo-Smoke erhält dieselben Missionsregeln, aber erzeugt keinen NetworkManager.

## Unity-Szenen- und Netzwerkvertrag

Ein gestarteter Host/Client besitzt exakt einen `NetworkManager` für die gesamte Sitzung. Transport, Prefab-Registrierung, Callbacks und Manager werden von `SessionService` eingerichtet; `NetworkManager.Singleton` darf nicht von Szenenbesitz abhängen. Manager bleibt über Hütte und Mission hinweg bestehen, wird aber nicht dauerhaft in Home oder im globalen Bootstrap gehalten. Solo erzeugt keinen Manager.

Für Host und Client gilt dieselbe Reihenfolge: Hütte und Missionsszene lokal additiv laden; Mission-Gameplay-Root bleibt inaktiv und Eingaben, Trigger und KI bleiben gesperrt. Danach Hostlistener starten bzw. Join senden. Vor Missionsbeginn dürfen Peers der Hütte betreten und wieder verlassen. NGO Scene Management synchronisiert beim Verbindungsaufbau den Szenenstand; vorab identisch geladene Szenen werden clientseitig nicht ein zweites Mal geladen. Bei Start schließt der Server den Join-Eingang, wartet auf die Bereitschaft der eingefrorenen Peer-Menge und aktiviert Gameplay erst nach erfolgreicher Barriere durch replizierten Sitzungs-/Rundenzustand. Während des Startversuchs und nach `MissionStartAuthorized` sind Joins geschlossen. Bei Cancel/Timeout/Unterschreiten der Mindestspielerzahl startet kein Peer; Gameplay bleibt inaktiv, Sitzung kehrt zur Hütte zurück.

**Isolierte Laufzeitprobe bestanden:** Unity `6000.6.8f1`, NGO `2.13.3`, Player aus `NetworkMovementTest` mit bestehendem dynamischen `NetworkPlayer.prefab`, Host und Client auf UDP `7798`. Beide Prozesse luden vor Host/Join eine additive `CabinProbe`-Szene und eine additive `MissionProbe`-Szene mit deaktiviertem `MissionGameplayRoot`. Danach synchronisierte NGO beide Peers; beide Logs meldeten `connected=2 dynamicPlayers=2 cabinLoaded=True missionLoaded=True missionGameplayActive=False`, Exitcode `0`. Logs: `C:/tmp/slh34-probe/host-3.log`, `C:/tmp/slh34-probe/client-3.log`. NGO-Quellcode bestätigt außerdem in `NetworkSceneManager.OnClientBeginSync`, dass eine bereits geladene Szene im Additive-Modus durch `ClientShouldPassThrough` nicht erneut geladen wird. Das ist Quellcodeeinsicht zusätzlich zum generischen Laufzeitprobe-Ergebnis. Der isolierte Aufbau nutzte absichtlich leere Cabin-/Mission-Szenen und das vorhandene dynamische Netzwerkspieler-Prefab; er testete weder echte DeliveryHouse-Missionsobjekte noch Gift-/Resident-/Authority-Prefab-Kombinationen oder die finale Aktivierungs-RPC. Diese produktiven Prefab- und Join-Gate-Checks bleiben Integration in #46/#48. **Kleinste Alternative bei Fehlschlag:** Missionsszene erst nach Verbindung gemeinsam laden, Gameplay gesperrt halten und Readiness-Barriere abwarten. NetworkManager bleibt im Session-Root; keine Manager-Kopie pro Szene. Unity löste Entitlement-Details auf; das ist kein CI-Lizenzstatusnachweis.

## Evidenz, Annahmen und offene Integration

- **Belegt durch MVP #16/#33 und eingecheckte Smoke-Protokolle:** 1/2/4 lokale Windows-Prozesse bzw. Rundenregeln, direkter NGO/UTP-Host/Client, Host plus drei Clients, bestehende Lieferung/Rückkehr/Capture/Befreiung, Reset, Client-Abbruch und eindeutige vorhandene Marker. Belegstellen: `docs/MVP-ACCEPTANCE.md`, `docs/NETCODE-SPIKE.md`, `Assets/Game/Delivery/CoopDeliveryEntry.cs`, `Assets/Game/Delivery/CoopNetworkRound.cs`.
- **Produktannahmen dieses Vertrags:** Home/Hütte/Mission-Fluss, Solo ohne Netzwerkmanager, Slots/Schema/Commit, Gast ohne Save, Join-Gate am Missionsstart, IPv4/UDP und Fehlertexte.
- **Probegrenze / noch offen für Integration:** das generische additive Vorladen wurde mit Host und Client bestanden; reale Mission-Prefabs und replizierte Aktivierung sind in #46/#48 zu prüfen. Unity startete und Entitlement-Details wurden aufgelöst, aber dies bestätigt keinen Lizenzstatus für CI.
- **Keine Gameplay-Implementierung in #34:** Abnahmekriterium ist diese Schnittstellenspezifikation in `docs/PRODUCT-CONTRACTS.md`. Integrationspakete übernehmen die Signaturen/Marker und bewahren vorhandene CLI-Argumente und deren bestehende Marker.
