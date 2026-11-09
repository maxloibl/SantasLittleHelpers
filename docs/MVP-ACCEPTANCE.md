# MVP-Abnahme #16

Stand: 9. November 2026

Getesteter Quellcommit: `0dff7d8e7506c9926b9629dfe8e41cedf0503f9d`

Basisstand: `289963f36a0304caa57dada9d5411c4e47947dd4`

Unity: `6000.6.8f1` (`237b6593792b`) · Windows x64 · Mono

## Ergebnis

**Bestanden auf dem lokalen Windows-Abnahmepfad.** Die Voraussetzungen für den lizenzierten CI-Runner aus Issue #15 waren nicht bestätigt. Wie im Sprint-Backlog vorgesehen, wurde deshalb ein frischer lokaler Windows-Player gebaut und headless geprüft. Issues #13, #14 und #29 waren im geprüften Basisstand bereits integriert.

Der Build wurde aus dem Issue-16-Worktree in den kurzen, eindeutigen Pfad `C:/tmp/slh16-windows` geschrieben. Die komplette portable Playerstruktur liegt dort, einschließlich `DeliveryHouse_Data/`, `MonoBleedingEdge/`, `UnityPlayer.dll`, `UnityCrashHandler64.exe` und den erzeugten D3D12-/Storage-Dateien. Es wurden keine Build-Binaries eingecheckt.

```text
Quellbasis:    289963f36a0304caa57dada9d5411c4e47947dd4
Buildmethode:  SantasLittleHelpers.Editor.WindowsPlayerBuild.Build
Buildlog:      C:/tmp/slh16-windows/build-final.log
Buildresultat: WINDOWS_BUILD result=Succeeded errors=0 warnings=15
Player:        C:/tmp/slh16-windows/DeliveryHouse.exe
Player SHA256: 8060790B33CF3CA520E289BF961094E2ED5473C7FAD5D6148D8489E96055D27B
Code SHA256:   EFC82542A20DE6C65F930762B9F8F9F758EA202B5CFE841E04001DB94DC89230
UnityPlayer:   38008AC8A1C61261F15DF9E5F7266AF25898A84A188278E33123B563C06EC3E6
```

Alle Playerläufe waren headless (`-batchmode -nographics`) und unsichtbar gestartet. Die Netzläufe verwendeten ausschließlich UDP-Ports `7792` bis `7795`; Port `7777` wurde nicht verwendet. Der Preview-Player des Nutzers blieb unangetastet.

## Abnahmeläufe auf demselben Player

| Fall | Ergebnis | Rohbelege |
| --- | --- | --- |
| 1 Spieler: Geschenk aufnehmen, zustellen, zurückkehren, Reset; zweiter Zustellaufruf wird abgewiesen | **Bestanden.** Exitcode 0; `pickedUp=True`, `delivered=True`, `duplicateRejected=True`, `returned=True`, `reset=True`. | `C:/tmp/slh16-windows/solo-final.log` |
| 2 Spieler: gemeinsamer Heavy-Grip, Capture, Befreiung, Lieferung, gemeinsamer Return und Reset | **Bestanden.** Host und Client beendeten sich natürlich mit Exitcode 0. Beide replizierten den Fortschritt und gingen in den Abschluss; der Host-Reset setzte `delivered=0`, `phase=0`. | `C:/tmp/slh16-windows/logs/final/two-player-coop/host.log`, `client.log` |
| 4 Spieler: Host plus 3 Clients, gemeinsame vollständige Mission | **Bestanden.** Vier aktive Owner belegten Slots 0–3 mit vier unterschiedlichen Host-Replica-Posen. Jeder Prozess hatte genau seinen eigenen `localView=True`-Owner; alle drei Clients protokollierten eigene Bewegung, Greifanforderung, Fortschritt und Rückkehr. Die vier Prozesse endeten natürlich mit Exitcode 0. Der Host protokollierte Heavy-Grip, Capture und Rescue, genau eine Lieferung, `COOP_ROUND_COMPLETE players=4`, Reset und danach vier eindeutige Replica-Posen. | `C:/tmp/slh16-windows/logs/final/four-player-coop/host.log`, `client1.log` bis `client3.log` |
| Client-Abbruch während beide Spieler das schwere Geschenk halten | **Bestanden.** Nach zwei bestätigten Griffen wurde der Client normal beendet. Der Host protokollierte `COOP_PLAYER_SLOT_RELEASED owner=1 slot=1` und nach den Gift-Disconnect-Callbacks `COOP_SMOKE_DISCONNECT_CLEANUP owner=1 heavyGripCount=1 pass=True`: der Client-Griff war entfernt, der Host-Griff blieb erhalten. Beide Prozesse beendeten sich natürlich mit Exitcode 0. | `C:/tmp/slh16-windows/logs/disconnect-final/host.log`, `client.log` |
| Abrupter Prozessabbruch desselben tragenden Clients | **Bestanden.** Nach dem bestätigten Zweifachgriff wurde ausschließlich der Testclient hart beendet. Nach 45 Sekunden Beobachtung meldete der Host Slot-Freigabe und `heavyGripCount=1 pass=True`. Der Host wurde nach dem Beobachtungsfenster vom Runner beendet; sein Exitcode ist daher kein natürlicher Gameplay-Exit. | `C:/tmp/slh16-windows/logs/disconnect-abrupt-final/host.log`, `client.log` |

Der Zustell-Schutz wurde außerdem mit dem vorhandenen Editor-Smoke geprüft: `DeliveryTestSceneBuilder.RunBatchSmoke` ruft dieselbe Geschenkzustellung zweimal auf; nur der erste Aufruf zählt. Ergebnis `DELIVERY_SMOKE_PASS` in `C:/tmp/slh16-windows/double-delivery-editor.log`. Der Solo-Smoke des gebauten Players wiederholt den Aufruf ebenfalls und schlägt mit Exitcode 1 fehl, falls der zweite Aufruf nicht abgewiesen wird. In der vollständigen Zweier- und Vierer-Mission erschien je genau ein `COOP_GIFT_DELIVERED`-Marker.

## Automatisierung und Grenzen

Solo-, Zwei- und Vier-Spieler-Smokes sind Prozess- und Netzwerkbeobachtungen des gebauten Players, aber kein natürlicher Tastatur-/Mausdurchlauf. `CoopSmokePlayerDriver` bewegt die Figuren entlang protokollierter Testpositionen und sendet die Greif-/Befreiungsanforderungen. Der Koop-Round-Smoke setzt den Bewohner für Capture um, platziert das Liefergeschenk am Ziel und löst den Reset aus. Die Owner-/Replica-Posen, Serverzustände, Replikation, Slotbelegung, Disconnect-Cleanup und Prozessausgänge werden aus den getrennten Host-/Clientlogs geprüft. Es wurde keine GUI- oder LAN-Abnahme behauptet.

Der Ablauf mit abruptem Abbruch belegt den Cleanup nach dem Transport-Timeout; der geplante Disconnect deckt zusätzlich den natürlichen Exit des Clients ab. Der isolierte #14-Gift-Test ist ergänzende Evidenz und kein Ersatz für diese integrierten Läufe. Die in dessen Protokoll aufgeführten früheren Builds und Logs werden nicht als Buildartefakt dieser Abnahme ausgegeben.

## Nachprüfen

Der Build ist mit dem oben angegebenen Unity-Aufruf und dem Worktree reproduzierbar. `scripts/run-coop-regressions.ps1` dient weiter für die isolierten Regressionen. Die vier Prozesse der vollständigen Vier-Spieler-Mission wurden für diesen Lauf direkt mit `--coop-smoke`, `--port=7793` und getrennten `-logFile`-Pfaden gestartet; der Zwei-Spieler-Lauf analog auf Port `7792`. Die Regressionen wurden versteckt gestartet, es wurde keine GUI geöffnet.

`python scripts/check-unity-repo.py` lief erfolgreich; die abschließende Git-Diff-Prüfung erfolgt vor dem lokalen Dokumentationscommit. Der Buildordner bleibt außerhalb des Repositories.
