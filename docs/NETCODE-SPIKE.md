# Netzcode-Spike für S1-C

Stand: 2026-11-08
Basis: `5835246`, Branch `issue/3-netzcode-prototyp`
Editor: Unity `6000.6.8f1` (Revision `237b6593792b`)
Issue: [#3](https://github.com/maxloibl/SantasLittleHelpers/issues/3)

## Entscheidung für #7 / S2-C

**NGO `2.13.3` funktioniert mit diesem Editor im isolierten Versuch und ist die empfohlene Option für S2-C.** Unitys Paketübersicht für `6000.6` listet ausschließlich `2.13.3` und kennzeichnet es als **Pre-release**. Das Ergebnis belegt die prototypische Eignung, aber nicht den Released-/Produktionsstatus. Wenn S2-C kein Pre-release verwenden darf, bleibt die Paketfreigabe blockiert, bis Unity eine stabile Version für den Projekt-Editor ausweist.

Die stabile NGO-Version `2.7.0` ist laut Unity für Editor `6000.0` released. UPM löste sie in `6000.6.8f1` auf, aber die Kompilierung schlug in NGO-Code mit mehreren `CS0619`-Fehlern fehl: `EntityId.implicit operator int` ist in dieser Editor-Version obsolete und als Fehler behandelt. Betroffen waren unter anderem `UnityTransport.cs` und `RigidbodyContactEventManager.cs`. Daher nicht `2.7.0` für `6000.6.8f1` pinnen.

NGO `2.13.3` wurde aus `https://packages.unity.com` aufgelöst und kompilierte fehlerfrei. Die Paketmetadaten deklarieren Transport `2.6.0`; in diesem Unity-Editor enthält das Lockfile `com.unity.transport` als Editor-Builtin `6.6.0`. Die getestete Kombination lautet:

| Bestandteil | Version / Status |
|---|---|
| Unity Editor | `6000.6.8f1` (`237b6593792b`) |
| Netcode for GameObjects | `2.13.3` (Pre-release für Unity 6000.6) |
| Unity Transport | `6.6.0` (Builtin des Editors; NGO-Metadaten verlangen `2.6.0`) |
| Windows Player | Development Build, zwei getrennte Prozesse |

## Laufzeitnachweis

Ein ausschließlich für den Versuch generierter Testaufbau enthielt einen NetworkManager mit Unity Transport, zwei automatisch gespawnte Player-Objekte mit `NetworkObject` und serverautoritativer `NetworkTransform` sowie ein vom Host gespawntes Rigidbody mit `NetworkObject`, `NetworkTransform` und `NetworkRigidbody`. Testbewegungen liefen deterministisch per `--autoplay`; der Host gab dem separat platzierten Rigidbody einen Impuls. Es wurde kein produktiver Gameplay-Code verwendet.

- Editor- und Scriptkompilierung mit NGO `2.13.3`: erfolgreich; keine C#-Compilerfehler.
- Windows Player Build: `SPIKE_BUILD result=Succeeded errors=0`.
- Host und Client liefen als zwei Prozesse. Client-Log: `SPIKE_CONNECTED clientId=1`.
- Beide Instanzen protokollierten Player `owner=0` und `owner=1` mit veränderlichen Positionen. Der Client sah die bewegte Host-Figur; der Host sah die bewegte Client-Figur.
- Das Host-Rigidbody startete bei `(0, 2, -10)`. Der Host-Impuls wurde protokolliert (`SPIKE_BODY_HOST_IMPULSE applied=true`); Host und Client protokollierten anschließend übereinstimmend `(1.30, 0.50, -10.00)`. Das Objekt blieb auch nach dem Client-Abbruch beim Host vorhanden.
- Der Client-Prozess wurde abrupt beendet. Nach dem Transport-Timeout meldete der Host `SPIKE_DISCONNECTED clientId=1 connected=1`; der Player mit `owner=1` erschien danach nicht mehr in den Host-Zustandslogs. Das Host-Rigidbody blieb gespawnt.

Das ist eine lokale Windows-Laufzeitprüfung auf demselben Rechner. Eine normale Client-Beendigung, LAN-Verbindung zwischen Rechnern, Paketverlust, Latenz, Build auf einem frischen Checkout, Ownership-Übergabe, Gameplay-Integration und Vier-Spieler-Betrieb wurden nicht geprüft. Die UI-Fenster waren nicht über die Desktop-Fensterliste verfügbar; Positionsevidenz stammt daher aus den Laufzeitlogs.

## Reproduzierbare Schritte

1. Projekt mit `C:\6000.6.8f1\Editor\Unity.exe` öffnen und in einem isolierten Spike-Projekt NGO `2.13.3` über den Package Manager installieren. Der in diesem Versuch nötige Registry-Zugriff auf `packages.unity.com` muss verfügbar sein.
2. Eigene `NetcodeSpike`-Szene und Player-Prefab erstellen. Player-Prefab: `NetworkObject`, `NetworkTransform`, lokale Testbewegung, die über Server-RPCs auf dem Host angewendet wird. Im NetworkManager das Player-Prefab registrieren und Unity Transport konfigurieren.
3. Ein getrenntes Rigidbody-Prefab mit `NetworkObject`, `NetworkTransform`, `NetworkRigidbody` und `Rigidbody` registrieren. Der Host spawnt dieses Objekt und wendet den Impuls serverseitig an.
4. Zwei Development-Player aus der Spike-Szene bauen. Host und Client auf demselben Rechner nacheinander starten (Port `7777`):

   ```powershell
   .\NetcodeSpike.exe --host --autoplay -logFile host-player.log
   .\NetcodeSpike.exe --client --autoplay -logFile client-player.log
   ```

5. In beiden Logs `SPIKE_CONNECTED`, Positionsänderungen beider Player und die Rigidbody-Positionen vergleichen. Den Client-Prozess abrupt schließen; nach dem NGO/Transport-Timeout im Host-Log `SPIKE_DISCONNECTED` und das Entfernen des Client-Players prüfen.

Die Szene, Build-Dateien, temporären Skripte und Logs wurden nur im Worktree für diesen Versuch erzeugt und sind nicht Teil des Ergebnis-Commits. Die beiden Fenster wurden in der Versuchskonfiguration mit `800x600` und `-screen-fullscreen 0` gestartet.

## Offizielle Paketinfos

- [Netcode for GameObjects für Unity 6000.6](https://docs.unity3d.com/6000.6/Documentation/Manual/com.unity.netcode.gameobjects.html): `2.13.3`, Pre-release; NGO passt zum GameObject/MonoBehaviour-Workflow des MVP.
- [Netcode for GameObjects für Unity 6000.0](https://docs.unity3d.com/ja/current/Manual/com.unity.netcode.gameobjects.html): `2.7.0` ist dort Released für `6000.0`; der lokale Compilerfehler zeigt, dass dies keine Kompatibilitätszusage für `6000.6.8f1` ist.
- [Unity Transport für Unity 6000.6](https://docs.unity3d.com/6000.6/Documentation/Manual/com.unity.transport.html): Transport ist eine Core-Paketebene, die an die Editor-Version gebunden ist.

## Grenzen

Der Versuch prüft nur direkten lokalen Host/Client-Verkehr, einfache Positionssynchronisierung, hostautoritative Physik und abrupten Client-Prozessabbruch. Es ist kein produktiver Netcode, keine Sicherheitsbewertung und keine Aussage zu Sitzungsverwaltung oder Internet-Hosting. NGO `2.13.3` bleibt ein Pre-release; diese Status- und Versionsbindung muss S2-C explizit übernehmen und gegen die im Projekt verwendete Editor-Version erneut prüfen.
