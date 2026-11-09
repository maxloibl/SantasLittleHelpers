# Issue #14 – Koop-Regressions: Laufprotokoll

Stand: 9. November 2026 · Repository-Stand `649a2534e9fda0a254cd130ed3d784b20aca6619` · Unity `6000.6.8f1`

Dieses Protokoll trennt echte Prozess-/Netzwerkbeobachtungen von Smoke-Automation, die Positionen verändert oder Zustände direkt auslöst. Die isolierten Testspieler sind Development Builds. Alle Logs liegen außerhalb des Repositorys unter `C:\tmp`.

## Wiederholung

1. Mit `scripts/build-coop-regression-players.ps1` die beiden isolierten Testspieler bauen. Unity schreibt beim ersten Import einige generierte Szenen-/Einstellungsdateien; das Skript sichert und stellt diese Dateien am Ende wieder her. Die Player und Unity-Logs bleiben außerhalb von Git.
2. `scripts/run-coop-regressions.ps1 -Scenario <Szenario> -BuildPath <Pfad>` ausführen. Der Host startet zuerst, dann folgen die Clients. Der Standardport für DeliveryHouse ist `7784`, das Gift-Testprojekt verwendet fest `7777`, das Capture-Testprojekt fest `7780`.
3. `-RunSeconds` setzt die Obergrenze der Beobachtungszeit. Natürlich beendete Prozesse müssen Exitcode `0` haben. Beim Zeitlimit beendet das Skript nur seine eigenen Prozesshandles und weist diese bewusst gestoppten Prozesse getrennt aus. Jeder Lauf erhält einen UTC-Zeitstempel mit GUID im Logverzeichnis.

Beispiele:

```powershell
.\scripts\build-coop-regression-players.ps1
.\scripts\run-coop-regressions.ps1 -Scenario coop-smoke -BuildPath C:\tmp\slh12-build\DeliveryHouse.exe -Port 7784 -RunSeconds 35
$fourPlayerBuild = 'C:\tmp\slh29-build\DeliveryHouse.exe' # frischen #29-Build hier ablegen
.\scripts\run-coop-regressions.ps1 -Scenario four-player -BuildPath $fourPlayerBuild -Port 7784 -RunSeconds 45
.\scripts\run-coop-regressions.ps1 -Scenario gift-regression -BuildPath C:\tmp\slh14-build\GiftCoopTest.exe -RunSeconds 30
.\scripts\run-coop-regressions.ps1 -Scenario capture-all-caught -BuildPath .\Builds\CaptureSmoke\CaptureSmoke.exe -RunSeconds 35
.\scripts\test-coop-regression-log-checks.ps1
```

Der Runner wertet die positiven Felder in Disconnect-, Regrab- und Gift-Summary-Markern einzeln aus. Vier-Spieler-Auswertung bindet vier gleichzeitig aktive Owner an vier Slots. Vier Host-Replica-Posen müssen vor dem Reset-Marker liegen; danach müssen genau ein `COOP_FOUR_PLAYER_RESET_SMOKE` und ein `COOP_HOST_RESET complete=true` in dieser Reihenfolge erscheinen, gefolgt von vier neuen Host-Replica-Posen. Die Owner-IDs werden aus dem Log gelesen; sie werden nicht als `0..3` vorausgesetzt.

Verwendete Startoptionen sind `-batchmode -nographics -logFile <datei> --host|--client`. DeliveryHouse liest `--port=<udp-port>` und `--shutdown-after=<sekunden>`; die Koop-Automation wird nur durch `--coop-smoke` aktiviert. `LocalNetworkJoin` liest keinen Port-Parameter, daher bleibt der Gift-Test auf `7777`. CaptureTest verwendet konstant `7780` und erkennt `--capture-auto-reset` sowie `--capture-wait-for-client`.

## Ergebnisse

| Prüfung | Ergebnis / Beleg | Art des Laufs |
| --- | --- | --- |
| Vier Teilnehmer verbinden | Auf dem vorgefundenen #12-Build verbanden sich vier echte Windows-Prozesse. Der damalige Lauf bestätigte lokale Besitzerkameras, zeigte aber die Spawnkollision unten. Mit dem gemergten #29-Build bestanden vier Prozesse plus automatischer Host-Reset; alle natürlichen Exitcodes waren `0`. Vier Owner-IDs wurden den Slots 0–3 zugeordnet. | Echte Prozesse und localhost-Netzwerk. Keine Eingabeautomation für Bewegungen. |
| Spawnplätze bei vier Teilnehmern | #12-Baseline: **Fehlgeschlagen / Blocker #29**; Clients überlappten auf `(0.8, 0.05, -6.25)`. #29-Retest: **bestanden**. Die vier aktiven Owner belegen gemeinsam die Slots 0–3; Host-Replica-Logs bestätigen vier unterschiedliche Positionen vor und nach dem Reset. Der Testdriver startete nach 6 Sekunden den gemeinsamen Reset; die danach protokollierten Replica-Positionen blieben je Owner eindeutig und entsprachen dem jeweiligen Startplatz. | Echte Vier-Prozess- und Host-Replica-Beobachtung; Reset wird über `--coop-four-player-reset-smoke` ausgelöst. |
| Doppelte Lieferung | Der Coop-Smoke meldete eine Lieferung (`COOP_GIFT_DELIVERED ... progress=1`) und den Rundenabschluss. Ein zweiter natürlicher Trigger ist mit dem vorhandenen Smoke nicht ausgelöst: nach Annahme wird das Geschenk despawned. Der vorhandene Solo-Smoke prüft den normalen Einzellieferpfad, nicht eine doppelte Zustellung. **Expliziter Duplikatversuch bleibt offen** und ist kein Pass. | Lieferung wurde durch `--coop-smoke` automatisiert positioniert; kein normaler Spielerweg. |
| Gleichzeitiges Greifen | **Teilabdeckung bestanden.** Der Coop-Smoke belegte zwei Griffe am schweren Geschenk (`COOP_SMOKE_HEAVY_TWO_GRIPS_PASS count=2`). Der isolierte Test wartet auf beide Teilnehmer und sendet danach zwei RPC-Anfragen pro Besitzer für denselben Light-Gift-Einzelslot. Besitzer 0 erhielt `hasSlot=True`, owner 1 `hasSlot=False`, beide sahen `gripCount=1`; das Ergebnis blieb `lightPeak=1`. Das belegt die Einzelslot-Arbitrierung im synchronisierten Testfenster. | Zwei echte Prozesse; die RPC-Anfragen werden durch den Testdriver ausgelöst. |
| Disconnect beim Tragen | Isolierter Gift-Test: Client forderte Light-Gift-Griff an und beendete danach den Prozess. Host: `GIFT_TEST_DISCONNECT ... autoCleanup=True`, anschließend erneuter Griff mit `GIFT_TEST_REGRAB afterDisconnect=True`. | Zwei echte Windows-Prozesse; Client beendet sich automatisiert während des getragenen Geschenks. |
| Alle gefangen und Neustart | Capture-Smoke: Host fing zuerst owner 0, danach owner 1; `CAPTURE_ROUND_LOST value=True`. `--capture-auto-reset` löste `CAPTURE_HOST_RESET complete=true` aus; der zweite Rundenstart meldete `freePlayers=2 totalPlayers=2`. Der Lauf fing danach erneut beide Spieler. | Zwei echte Prozesse; Capture folgt den Spawnpositionen des isolierten Teststands, Reset wird automatisiert ausgelöst. |
| Zwei-Spieler-Mission und Neustart | Coop-Smoke meldete zwei Heavy-Griffe, Capture, erhaltenen Griff des Partners, abgewiesenen Greifversuch des gefangenen Spielers, Rescue, eine Lieferung, Rückkehr und `COOP_SMOKE_RESTART_PASS phase=0 delivered=0 players=2`. | Zwei echte Prozesse, aber Testdriver setzt Spieler und Bewohner um (`COOP_SMOKE_MOVE_*`, `COOP_SMOKE_REPOSITION`) und löst Rescue/Restart automatisiert aus. Kein Nachweis für einen natürlichen Spielerweg. |

### Rohlogs der ausgeführten Läufe

- Erste 4-Prozess-Prüfung des #12-Builds: `C:\tmp\slh14-four-player\host.log` und `client1.log` bis `client3.log`.
- #29-Vier-Prozess-Reset-Retest: `C:\tmp\slh14-regressions\four-player-20261109-133718-450-ef5e711e213d4d4b82a84f62737e1ab4\host.log` und `client*.log`. Der gestartete Build hatte SHA-256 `AB35ACA49C0E90C26B211ACED621B60F0C615CFF948BF758C4163BFA642DC6E4`.
- Separater Rejoin-Beleg aus #29: `C:\tmp\slh14-four-player\run4\host.log` enthält `COOP_PLAYER_SLOT_RELEASED owner=3 slot=3` und danach `COOP_PLAYER_START owner=4 slot=3`.
- Zwei-Prozess-Coop-Smoke des #12-Builds: `C:\tmp\slh14-coop-smoke\host.log` und `client.log`.
- Gift-Test-Build (`GIFT_TEST_BUILD result=Succeeded`, 0 Fehler): `C:\tmp\slh14-build-logs\gift-player.log`. Aktualisierter Lauf mit Einzelslot-Konkurrenz: `C:\tmp\slh14-regressions\gift-regression-20261109-142210\`. Strikter Runner-Retest aus Buildpfad mit Leerzeichen, Prozesse natürlich beendet mit Exitcode `0`: `C:\tmp\slh14-regressions\gift-regression-20261109-133628-462-c580e802e2b0402a96e57bdc26846e58\`.
- Capture-Test-Build (`CAPTURE_WINDOWS_BUILD result=Succeeded`, 0 Fehler): `C:\tmp\slh14-build-logs\capture-player.log`. Lauf: `C:\tmp\slh14-regressions\capture-all-caught-20261109-141531\`.

Die Unity-Stacktraces in den Headless-Logs folgen auf `Debug.Log`-Zeilen wie `MISSION_PLAYER` und `COOP_RESIDENT_NETWORK_SPAWN`; in den untersuchten Logs gibt es keine `Exception`, `NullReferenceException` oder Error-Meldung für diese Stacks.

Die Parser-Negativproben lassen sich mit `scripts/test-coop-regression-log-checks.ps1` wiederholen. Sie prüfen falsche Cleanup-/Regrabwerte (einschließlich späterer False-Marker), leere Giftlogs, doppelt belegte aktive Slots, doppelte Host-Posen, fehlende/mehrdeutige Reset-Bestätigung, nach dem Resetmarker verschobene Vorher-Posen und alte Marker ohne Slot/Replica-Belege. Ein absichtlich unvollständiger Build (nur `.exe`, ohne übrige Player-Dateien) endete mit `-1073741515`; der Runner meldete natürliche Fehlercodes und fehlende Logs als `RESULT=FAIL`, ohne fremde Prozesse zu beenden. Vier-Spieler-Rohlogs: `C:\tmp\slh14-regressions\four-player-20261109-133803-435-21d27a160d0f44348e5ac01e7b2195c8\`. Leere Giftlogs und relative Logpfade werden ebenfalls als kontrollierter `RESULT=FAIL` protokolliert; der Runner löst `LogRoot` vor dem Start der Player zu einem absoluten Pfad auf.

## Abnahmegrenzen

- Der vorgefundene Windows-Build `C:\tmp\slh12-build\DeliveryHouse.exe` wurde zuerst auf dem Vier-Prozess-Pfad und danach mit `--coop-smoke` geprüft. Die Belege wurden gesichert; der Buildpfad wurde später durch den parallelen #29-Fixlauf überschrieben. Die genannten Rohlogs stammen aus dem vorherigen #12-Lauf.
- #29 ist nach diesem Retest mit Startplätzen, Reset und separatem Rejoin-Beleg abgedeckt. Der abschließende integrierte #16-Gameplay-Weg bleibt davon getrennt.
- Doppelte Lieferung hat mit den vorgefundenen Testtreibern keinen gezielten zweiten Zustellversuch erhalten. Die Implementierung enthält einen Guard, aber dieser Testlauf bestätigt die Duplikat-Abnahme noch nicht. Ein zweiter natürlicher Trigger ist nach dem Despawn nicht erreichbar; ein isolierter Test für wiederholte Zustellaufrufe muss deshalb den Zustell-Einstieg gezielt zweimal aufrufen.
- Keine GUI-/Fensterprüfung, kein LAN zwischen Rechnern, keine natürliche Capture-/Rescue-Steuerung und kein erneuter Main-Gameplay-Weg wurden in diesem Paket behauptet.
