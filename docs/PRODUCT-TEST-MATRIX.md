# Produktfluss-Regressionsmatrix (#50)

Diese Matrix prüft die in `docs/PRODUCT-CONTRACTS.md` (S5-A) festgelegten Marker und den MVP-Produktfluss. Sie ergänzt die bestehenden Koop-Skripte, ersetzt sie nicht: `scripts/run-coop-regressions.ps1`, `scripts/build-coop-regression-players.ps1` und die zugehörigen Logprüfungen bleiben unverändert nutzbar.

Neue Runtime-Hooks gehören zu den jeweiligen Runtime-Issues. #50 liefert nur Skripte, Fixtures und diese Dokumentation.

## Marker-Vertrag

Der Parser akzeptiert ausschließlich die folgenden Marker, jeweils mit `Feld=Wert`-Paaren. Marker müssen am Tokenanfang stehen; ein Präfix wie `XPRODUCT_FLOW` ist kein gültiger Treffer. Doppelte Felder, unbekannte Felder, fehlende Pflichtfelder und mehrdeutige Felder werden abgelehnt.

```text
PRODUCT_FLOW role=Solo|Host|Guest state=Home|Hut|Mission|Returning result=Started|Completed|Failed error=<code>
SESSION_LAUNCH role=Solo|Host|Guest result=Ready|Failed error=<code>
SESSION_STATE state=Idle|LoadingHut|Hosting|Joining|HutReady|PreparingMission|MissionReady|Running|Returning|Failed|Stopped role=Solo|Host|Guest
SESSION_JOIN_REJECT reason=SessionClosed|SessionFull|SessionNotFound|InvalidEndpoint|NotReady
SESSION_READY peerId=<id> scene=Mission roundId=<guid>
MISSION_START_AUTHORIZED roundId=<guid> players=<n>
MISSION_EVENT type=GiftDelivered|PlayerCaptured|PlayerFreed|Completed|Failed|Abandoned roundId=<guid> peerId=<id>
SAVE_COMMIT role=Solo|Host roundId=<guid> slotId=<guid> missionId=<id> result=Committed|AlreadyCommitted|Rejected reason=<reason>
SESSION_SHUTDOWN result=Completed|Failed managerCount=0|1 reason=<code>
```

Prozesslog-Regeln: genau ein `SESSION_LAUNCH` und genau ein `SESSION_SHUTDOWN` pro Prozesslog, genau ein terminales `PRODUCT_FLOW` pro Rolle, genau ein terminales `MISSION_EVENT` pro `roundId`, eindeutige `SESSION_READY`-Paare, keine identische `SAVE_COMMIT`-Wiederholung, `managerCount=0` bei sauberem Shutdown.

## Harness

`scripts/run-product-regressions.ps1` startet entweder die Fixture-Abnahme oder den Prozess-Smoke gegen einen Windows-Player.

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/run-product-regressions.ps1 -ListCases
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/run-product-regressions.ps1 -Mode fixture
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/run-product-regressions.ps1 -Mode player -BuildPath <Pfad>\Player.exe -TimeoutSeconds 90
```

- Standardmodus ist `fixture`; vor dem Runtime-Merge genügt diese Abnahme.
- `-Mode player` ist der S7-F-Lauf am integrierten Windows-Build.
- `-Case` wählt einzelne Prozessfälle im Player-Modus. Manuelle GUI-/LAN-Fälle werden hier abgelehnt und nur über diese Matrix abgenommen. Die Fixture-Abnahme führt immer die vollständige Prozessmatrix aus.
- `-LogRoot` und `-ProfileRoot` müssen absolute oder auf das Worktree bezogene Pfade unter `Logs/` oder `Builds/` sein. Der Harness lehnt Pfade außerhalb des Worktrees ab.
- Jeder Lauf erhält eine eindeutige Run-ID, getrennte Logs pro Prozess und ein eigenes temporäres Profilverzeichnis pro Prozess. Nur `save-roundtrip` teilt bewusst ein Profil zwischen zwei sequenziellen Solo-Läufen.
- Jeder Fall erhält einen eigenen Port ab `-Port`; der Standard beginnt bei `7796`. Bei mehreren Fällen werden die Ports fortlaufend vergeben.
- `-TimeoutSeconds` begrenzt Wartezeit und Prozessabbau. Der Harness beendet ausschließlich selbst gestartete Prozesse, prüft deren Exitstatus und entfernt die temporären Profile. Logs bleiben erhalten. `-KeepProfiles` behält Profile zur Fehlersuche.
- Ausgabe: `PROCESS=`, `CASE_PASS`, `CASE_FAIL`, `PROCESS_CLOSED`, `RESULT=PASS|FAIL`. Fehler werden als `FAILURE=` mit Fallbezug ausgegeben.

### Gepaarte Player-Flags

Der Player-Modus übergibt pro Prozess immer `--profile` (absolutes, isoliertes Verzeichnis) und `--port` (Port des Falls) gemeinsam:

| Flag | Bedeutung |
| --- | --- |
| `-batchmode -nographics` | Versteckter, headless Prozess-Smoke |
| `-logFile <absoluter Pfad>` | Getrenntes Log pro Prozess |
| `--product-smoke=<case>` | Fall aus der Matrix |
| `--profile=<absolutes Verzeichnis>` | Isoliertes Profil; bei `save-roundtrip` zwischen Lauf 1 und 2 geteilt |
| `--port=<n>` | Ein Port pro Fall, nicht pro Prozess |
| `--host` | Hostprozess |
| `--client` | Gastprozess |
| `--join=127.0.0.1` | Gültiger direkter IPv4-Join; kein Relay, kein Matchmaking, kein IPv6 |
| `--join=999.999.999.999` | Ungültiger Join für `invalid-join` |
| `--leave-after=<s>` | Geordnetes Verlassen der Hütte für `rejoin` |

Diese Flags sind der in S5-A beschriebene provisorische CLI-Vertrag. Fehlen die Runtime-Hooks im Build, schlägt `-Mode player` mit fehlenden Markern fehl; das ist erwartet und kein Fixture-Fehler.

## Fälle

| Fall | Art | Prozesse | Prüfung |
| --- | --- | --- | --- |
| `solo-transition` | Prozess-Smoke | 1 | Solo-Start, Mission, Home, genau ein Host-Commit ohne NetworkManager, Shutdown mit `managerCount=0` |
| `duo-transition` | Prozess-Smoke | 2 | Host plus Gast, beide ready vor genau einer Autorisierung, Host-Commit genau einmal, Gast ohne Save |
| `four-transition` | Prozess-Smoke | 4 | Host plus drei Gäste, vier eindeutige Ready-Peers, eine Autorisierung mit `players=4`, Host-Commit genau einmal |
| `save-roundtrip` | Prozess-Smoke | 2 | Zwei sequenzielle Solo-Läufe, gleiches Profil und gleicher Slot, neue `roundId`, zweiter Commit `Committed` oder `AlreadyCommitted` |
| `no-client-write` | Prozess-Smoke | 2 | Gast schreibt nie `SAVE_COMMIT`; Storage-Sentinel bleibt unverändert |
| `invalid-join` | Prozess-Smoke | 1 | Join mit ungültiger IPv4 endet als `InvalidEndpoint` vor jeder Ready-Markierung; sauberer Shutdown |
| `rejoin` | Prozess-Smoke | 3 | Gast verlässt die Hütte, zweiter Gast tritt bei; keine Autorisierung, kein Save, kein Abschluss |
| `full-hut` | Prozess-Smoke | 5 | Drei akzeptierte Gäste füllen die Hütte; fünfter Spieler wird mit `SessionFull` abgelehnt, kein Start, kein Save |
| `host-loss` | Prozess-Smoke | 2 | Harness beendet eigenen Host; Gast meldet `HostLeft`, kehrt nach Home zurück, schreibt keinen Save |
| `manual-gui-solo` | Manuell | 1 | Sichtbares Fenster, echte Eingabe, Save-Roundtrip über Neustart, Rückkehr nach Home |
| `manual-gui-duo` | Manuell | 2 | Zwei sichtbare Fenster: Hütte, eine Mission, nur Host-Commit, Hostverlust |
| `manual-gui-four` | Manuell | 4 | Vier sichtbare Fenster: volle Hütte, abgelehnter fünfter Join, Rejoin, Mission, nur Host-Commit |
| `manual-lan-duo` | Manuell | 2 | Zwei Maschinen im selben LAN, direkter IPv4/UDP-Join ohne Relay oder IPv6 |
| `manual-lan-four` | Manuell | 4 | Vier Maschinen im LAN: Kapazität, Ready-Barriere, Host-Commit, Hostverlust |

## Fixture-Abnahme

`-Mode fixture` führt `scripts/test-product-regression-log-checks.ps1` aus und schreibt die Ausgabe nach `<LogRoot>/<run-id>/fixture-parser-checks.log`. Die Abnahme gilt nur bei `PARSER_CHECKS=PASS` und Exitcode `0`.

Die Fixtures decken positive Verläufe sowie diese Negativfälle ab:

- Falsch positive Marker: Präfix `XPRODUCT_FLOW`, nur teilweise vorhandene Marker, `SAVE_COMMIT` eines Gasts, abweichender Ready-Round, falscher Join-Grund.
- Unvollständige Ereignisse: fehlender Shutdown, fehlende Gast-Ready-Markierung, fehlender zweiter Save-Lauf, fehlender Mission-Abschluss.
- Doppelte Ereignisse: doppelter Launch, doppelter Abschluss, doppelter Save, doppelte Ready-Peers, doppelte Join-Ablehnung.
- Zustandsfehler: `managerCount=1`, späterer falscher Shutdown, abgelehnter Commit, geänderter Storage-Sentinel, Gast-Commit bei `no-client-write`.

Der Fixture-Lauf ist die Abnahme vor dem Runtime-Merge. Er startet keinen Player und benötigt keinen Build.

## Manuelle GUI- und LAN-Abnahme

Die manuellen Fälle werden nicht automatisiert und nicht als Prozess-Smoke ausgegeben. Für die Abnahme sind sichtbare Fenster, echte Tastatur-/Mauseingabe und getrennte Maschinen erforderlich.

1. `manual-gui-solo`: Solo starten, Profil/Slot wählen, Mission abschließen, Spiel beenden, gleichen Slot erneut laden und denselben Fortschritt prüfen.
2. `manual-gui-duo`: Host starten, Gast im Hüttenzustand joinen, eine Mission abschließen. Nur der Host darf speichern. Danach Host beenden und `HostLeft` beim Gast prüfen.
3. `manual-gui-four`: Host plus drei Gäste, fünften Join auf `SessionFull` prüfen, einen Gast verlassen und erneut joinen, Mission abschließen, nur ein Host-Commit.
4. `manual-lan-duo` und `manual-lan-four`: Auf getrennten Maschinen im selben LAN über direkte IPv4-Adresse und UDP-Port joinen. Relay, Matchmaking und IPv6 bleiben aus.

Evidenz je Fall: Datum, Build/Commit, Windows-Version, beteiligte Maschinen, verwendete Ports, Screenshots oder kurze Videosequenz, Beobachtung von Join, Mission, Commit und Hostverlust sowie der Hinweis, ob der Lauf bestanden, fehlgeschlagen oder ungeprüft ist. Manuelle Läufe dürfen nicht als automatisch bestanden gemeldet werden.

## MVP-Regressionen

Die bereits dokumentierten MVP-Regressionen bleiben über die vorhandenen Skripte abgedeckt und werden hier nur zugeordnet:

| MVP-Regression | Abdeckung |
| --- | --- |
| Doppelte Lieferung wird abgewiesen, Reset/Restart bleibt konsistent | `solo-transition`, `save-roundtrip`; bestehender `coop-smoke` |
| Gleichzeitiger Heavy-Grip | bestehender `run-coop-regressions.ps1 -Scenario coop-smoke` |
| Verbindungsabbruch beim Tragen | bestehende `coop-smoke`- und `capture-all-caught`-Läufe |
| Alle Spieler gefangen | bestehender `run-coop-regressions.ps1 -Scenario capture-all-caught` |
| Hostverlust ohne Gast-Save | `host-loss`, zusätzlich `no-client-write` für den Gast-Schreibschutz |
| Volle Hütte und später Join | `full-hut`, `rejoin`, `invalid-join` |
| Solo, Duo und Vier-Spieler-Übergänge | `solo-transition`, `duo-transition`, `four-transition` |

## Status und Grenzen

- Fixture-Abnahme: lokal mit Unity-unabhängigem PowerShell ausführbar und bestanden, sofern `PARSER_CHECKS=PASS` gemeldet wird.
- Prozess-Smoke gegen einen echten Windows-Player: bis zum Vorliegen der Runtime-Hooks aus den jeweiligen Issues ungeprüft. S7-F führt den Harness am integrierten Build aus.
- Manuelle GUI-/LAN-Abnahme: immer ungeprüft, solange keine dokumentierte Evidenz vorliegt.
- Keine Paket-, `ProjectSettings/`- oder Szenenänderung durch #50.
