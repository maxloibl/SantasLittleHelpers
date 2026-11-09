# MVP-Abnahme #16

Stand: 9. November 2026

Getesteter Quellcommit: `7dc4ef86bcd5aaf8710521e6f850a188422a77d3`

Basisstand: `289963f36a0304caa57dada9d5411c4e47947dd4`

Unity: `6000.6.8f1` (`237b6593792b`) · Windows x64 · Mono

## Ergebnis

**Bestanden auf dem lokalen Windows-Abnahmepfad.** Die Voraussetzungen für den lizenzierten CI-Runner aus Issue #15 waren nicht bestätigt. Wie im Sprint-Backlog vorgesehen, wurde deshalb ein frischer lokaler Windows-Player gebaut und headless geprüft. Issues #13, #14 und #29 waren im geprüften Basisstand bereits integriert.

Der Build wurde aus dem Issue-16-Worktree in den kurzen, eindeutigen Pfad `C:/tmp/slh16-windows` geschrieben. Die komplette portable Playerstruktur liegt dort, einschließlich `DeliveryHouse_Data/`, `MonoBleedingEdge/`, `UnityPlayer.dll`, `UnityCrashHandler64.exe` und den erzeugten D3D12-/Storage-Dateien. Es wurden keine Build-Binaries eingecheckt.

```text
Quellbasis:    289963f36a0304caa57dada9d5411c4e47947dd4
Buildmethode:  SantasLittleHelpers.Editor.WindowsPlayerBuild.Build
Buildlog:      C:/tmp/slh16-windows/build-final-3.log
Buildresultat: WINDOWS_BUILD result=Succeeded errors=0 warnings=15
Player:        C:/tmp/slh16-windows/DeliveryHouse.exe
Player SHA256: 8060790B33CF3CA520E289BF961094E2ED5473C7FAD5D6148D8489E96055D27B
Code SHA256:   386EFF6049E77B4C339853406B8C55AD5D2D42ED0CB5BFFA2EB61F5468D70A99
UnityPlayer:   38008AC8A1C61261F15DF9E5F7266AF25898A84A188278E33123B563C06EC3E6
```

Alle Playerläufe waren headless (`-batchmode -nographics`) und unsichtbar gestartet. Die Netzläufe verwendeten ausschließlich UDP-Ports `7792` bis `7795`; Port `7777` wurde nicht verwendet. Der Preview-Player des Nutzers blieb unangetastet.

## Abnahmeläufe auf demselben Player

| Fall | Ergebnis | Rohbelege |
| --- | --- | --- |
| 1 Spieler: Geschenk aufnehmen, zustellen, zurückkehren, Reset; zweiter Zustellaufruf wird abgewiesen | **Bestanden.** PID 14792, Exitcode 0; `pickedUp=True`, `delivered=True`, `duplicateRejected=True`, `returned=True`, `reset=True`. | `C:/tmp/slh16-windows/solo-final-2.log` |
| 2 Spieler: gemeinsamer Heavy-Grip, Capture, Befreiung, Lieferung, gemeinsamer Return und Reset | **Bestanden.** Host PID 2152 und Client PID 30868 beendeten sich natürlich mit Exitcode 0. Beide replizierten den Fortschritt und gingen in den Abschluss; der Host-Reset setzte `delivered=0`, `phase=0`. | `C:/tmp/slh16-windows/logs/final-2/two-player/host.log`, `client.log` |
| 4 Spieler: Host plus 3 Clients, gemeinsame vollständige Mission | **Bestanden.** Host PID 3924 und Clients 6448, 2392 und 32252 endeten natürlich mit Exitcode 0. Vier aktive Owner belegten Slots 0–3 mit vier verschiedenen Host-Replica-Posen. Jeder Prozess hatte nur für seinen eigenen Owner `localView=True`; alle drei Clients protokollierten eigene Bewegung, Greifanforderung, Fortschritt und Rückkehr. Der Host protokollierte Heavy-Grip, Capture und Rescue, genau eine Lieferung, `COOP_ROUND_COMPLETE players=4`, Reset und danach vier eindeutige Replica-Posen. | `C:/tmp/slh16-windows/logs/final-2/four-player/host.log`, `client1.log` bis `client3.log` |
| Geplanter Client-Disconnect mit dediziertem Heavy-Grip-Smoke | **Bestanden.** Vor dem Disconnect belegte der Host `COOP_DISCONNECT_PRE owner0Grip=True owner1Grip=True ... gripCount=2 pass=true`. Nach dem Client-Exit meldete der Host `COOP_DISCONNECT_POST owner=1 ownerGrip=False hostOwner=0 hostGrip=True gripCount=1 pass=True`. Der Modus verwendet echte Greifanforderungen; er lässt Capture und Lieferung aus und droppt das Geschenk nicht künstlich. Host PID 14440 und Client PID 29620 beendeten sich natürlich mit Exitcode 0. | `C:/tmp/slh16-windows/logs/disconnect-smoke-graceful/host.log`, `client.log` |
| Abrupter Client-Abbruch nach bestätigtem Heavy-Grip | **Bestanden.** Dieselben ownergenauen Vorher-Marker wie oben, danach `ownerGrip=False`, `hostGrip=True`, `gripCount=1 pass=True`. Client PID 20324 wurde absichtlich beendet (Exitcode -1); Host PID 16144 beobachtete weitere 45 Sekunden und wurde anschließend absichtlich beendet (Exitcode -1). | `C:/tmp/slh16-windows/logs/disconnect-smoke-abrupt/host.log`, `client.log` |

Der Zustell-Schutz wurde außerdem mit dem vorhandenen Editor-Smoke geprüft: `DeliveryTestSceneBuilder.RunBatchSmoke` ruft dieselbe Geschenkzustellung zweimal auf; nur der erste Aufruf zählt. Ergebnis `DELIVERY_SMOKE_PASS` in `C:/tmp/slh16-windows/double-delivery-editor.log`. Der Solo-Smoke des gebauten Players wiederholt den Aufruf ebenfalls und schlägt mit Exitcode 1 fehl, falls der zweite Aufruf nicht abgewiesen wird. In der vollständigen Zweier- und Vierer-Mission erschien je genau ein `COOP_GIFT_DELIVERED`-Marker.

## Automatisierung und Grenzen

Solo-, Zwei- und Vier-Spieler-Smokes sind Prozess- und Netzwerkbeobachtungen des gebauten Players, aber kein natürlicher Tastatur-/Mausdurchlauf. `CoopSmokePlayerDriver` bewegt die Figuren entlang protokollierter Testpositionen und sendet die Greif-/Befreiungsanforderungen. Der Koop-Round-Smoke setzt den Bewohner für Capture um, platziert das Liefergeschenk am Ziel und löst den Reset aus. Der separate `--coop-disconnect-smoke`-Modus bewegt nur Host und Client zum schweren Geschenk und sendet echte Griffanforderungen. Sein Hostmarker prüft unmittelbar vor dem Abbruch Serverbesitzer 0 und 1 samt `GripCount=2`; nach einem Frame Wartezeit auf Disconnect-Callbacks prüft er Owner 1 ohne Griff, Owner 0 mit Griff und `GripCount=1`. Der Modus führt weder Capture noch Lieferung, Reset oder erzwungene Freigabe aus. Alle Läufe waren versteckt und headless. Es wurde keine GUI- oder LAN-Abnahme behauptet.

Der vorangegangene #16-Beleg `heavyGripCount=1` aus dem allgemeinen Coop-Smoke war durch eine vorherige Capture-Aktion mehrdeutig und wird verworfen. Die beiden obigen Disconnect-Belege stammen ausschließlich aus dem Capture-freien Modus und enthalten ownergenaue Vorher-/Nachher-Griffmarker. Der isolierte #14-Gift-Test ist ergänzende Evidenz, nicht Ersatz für diese integrierten Läufe. Frühere Builds und Logs werden nicht als Buildartefakt dieser Abnahme ausgegeben.

## Nachprüfen

Der Build ist mit dem oben angegebenen Unity-Aufruf und dem Worktree reproduzierbar. `scripts/run-coop-regressions.ps1` dient weiter für die isolierten Regressionen. Die vier Prozesse der vollständigen Vier-Spieler-Mission wurden für diesen Lauf direkt mit `--coop-smoke`, `--port=7793` und getrennten `-logFile`-Pfaden gestartet; der Zwei-Spieler-Lauf analog auf Port `7792`. Die Disconnect-Läufe verwendeten `--coop-disconnect-smoke` auf Port `7794` (geplant) bzw. `7795` (abrupt). Die Regressionen wurden versteckt gestartet, es wurde keine GUI geöffnet.

`python scripts/check-unity-repo.py` und `git diff --check` liefen erfolgreich. Der Buildordner bleibt außerhalb des Repositories.
