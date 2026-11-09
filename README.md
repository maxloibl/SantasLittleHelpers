# Santa's Little Helpers

## 1. Unity unter Windows einrichten

1. Öffne [Unity Hub](https://unity.com/download) und melde dich mit deinem Unity-Konto an. Aktiviere in Hub eine Lizenz, falls der Editor danach fragt.
2. Installiere über **Installs → Install Editor** genau die Version aus [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt). Der aktuelle Stand verwendet **Unity 6000.6.8f1**. Auf diesem Rechner liegt der Editor unter `C:\6000.6.8f1`. [Unity-Releasearchiv](https://unity.com/releases/editor/whats-new/6000.6.8f1)
3. Installiere in Unity Hub unter **Installs → Add modules** für diese Editorversion **Windows Build Support (Mono)**. Der eingecheckte Build-Einstieg erzeugt einen portablen Windows-x64-Mono-Player; IL2CPP und Visual Studio werden dafür nicht benötigt.
4. Wähle in Hub **Projects → Add → Add project from disk** und dann den Ordner `SantasLittleHelpers`, der `Assets`, `Packages` und `ProjectSettings` enthält. Öffne das Projekt mit dem in Schritt 2 installierten Editor und lass den ersten Import fertiglaufen.
5. Öffne im Project-Fenster `Assets/Game/Scenes/MovementSandbox.unity`. Drücke **Play** und klicke in die **Game**-Ansicht.

[Unity-LTS-Übersicht](https://unity.com/releases/unity-6/support) · [URP-Projektanlage](https://docs.unity3d.com/6000.6/Documentation/Manual/urp/creating-a-new-project-with-urp.html) · [Input-System-Installation](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.20/manual/Installation.html)

## 2. Bedienung

| Eingabe | Aktion |
| --- | --- |
| W / A / S / D | Laufen in Blickrichtung |
| Maus | Umsehen |
| Linke Umschalttaste halten | Sprinten |
| Linke Strg-Taste oder C halten | Ducken |
| Leertaste | Springen |
| Esc | Maus freigeben |
| Linksklick im Game-Fenster | Maus wieder einfangen |

## 3. Abnahme im Play Mode

- **Laufen und Sprinten.** Richte die Kamera an der Startposition gerade auf die Wand aus. Zwischen den goldenen Markierungen liegen 8 m. Die Strecke dauert ungefähr 2 s gehend, 1,3 s sprintend und 4 s geduckt. W+D bewegt genauso schnell wie W allein. Wenn du die Taste loslässt, endet die horizontale Bewegung sofort.
- **Wand und Sprung.** Halte W an der grünen Wand. Die Spielfigur bleibt vor der Wand stehen. Gehe zum 60 cm hohen Holzblock links und springe darauf. Halten der Leertaste löst nach der Landung keinen zweiten Sprung aus. Erneutes Drücken in der Luft löst keinen Doppelsprung aus.
- **Niedriger Durchgang.** Versuche rechts, im Stehen unter das Dach zu laufen. Ducke dich und gehe hinein. Lasse Strg bzw. C unter dem Dach los. Die Kapsel und Kamera bleiben unten. Gehe weiter aus dem Durchgang. Die Figur richtet sich wieder auf. Sprinten und Springen sind geduckt gesperrt.
- **Kamera und Fokus.** Drehe dich bei gedrücktem W. Die Laufrichtung folgt sofort der horizontalen Blickrichtung. Bewege die Maus senkrecht, bis der Blick an beiden Enden stoppt. Bei Stillstand bleibt die Kamera ruhig. Drücke Esc, bewege die Maus und drücke W. Die Figur bewegt sich erst nach erneutem Klick in die Game-Ansicht. Prüfe auch einen Fensterwechsel per Alt+Tab.
- **Windows-Ziel.** Der Player startet in `DeliveryHouse`. Die globale Build-Szenenliste enthält zuerst `DeliveryHouse` und danach `MovementSandbox`; die Testszene bleibt im Editor direkt zum Spielen verfügbar. Für einen wiederholbaren Player-Build siehe Abschnitt 4.

## 4. Windows-Player bauen und starten

Voraussetzungen: Git, Unity **6000.6.8f1** samt Modul **Windows Build Support (Mono)** und ein freier UDP-Port (Vorgabe **7777**). PowerShell-Befehle im frischen Checkout:

```powershell
git clone https://github.com/maxloibl/SantasLittleHelpers.git
Set-Location SantasLittleHelpers
New-Item -ItemType Directory -Force C:/tmp/slh13-windows | Out-Null
& 'C:/6000.6.8f1/Editor/Unity.exe' -batchmode -quit -projectPath (Get-Location).Path -buildTarget Win64 -executeMethod SantasLittleHelpers.Editor.WindowsPlayerBuild.Build -slhBuildPath C:/tmp/slh13-windows/DeliveryHouse.exe -logFile C:/tmp/slh13-windows/build.log
```

Der Befehl endet erfolgreich mit `WINDOWS_BUILD result=Succeeded`; das Log liegt bei `C:/tmp/slh13-windows/build.log`. Das portable Artefakt ist `DeliveryHouse.exe` samt `DeliveryHouse_Data/`, `UnityPlayer.dll` und ggf. weiteren erzeugten Dateien im selben Ordner. Zum Start:

```powershell
& 'C:/tmp/slh13-windows/DeliveryHouse.exe'
```

Im Menü **Play Solo** startet die Mission lokal. **Start Host** öffnet einen Host für bis zu drei Clients (vier Spieler insgesamt). Die Vier-Spieler-Abnahme lief mit vier lokalen Prozessen über localhost (`127.0.0.1`) und denselben ausgewählten UDP-Port. Ein LAN-Lauf zwischen verschiedenen Rechnern wurde nicht geprüft. Die Verbindung verwendet direkte IPv4 ohne Relay, NAT-Traversal oder IPv6; der Host bindet an alle Netzwerkschnittstellen.

Steuerung im Player: **W/A/S/D** laufen, **Maus** umsehen, **E** Geschenk aufnehmen/ablegen bzw. einen nahen Mitspieler befreien, **Shift** sprinten, **Strg/C** ducken, **Leertaste** springen. Bei Gefangennahme stoppt die Bewegung; zum Neustart nach einer abgeschlossenen Solo-Runde oder verlorenen Koop-Runde **R** drücken. **Esc** gibt den Mauszeiger frei; Klick ins Spielfenster fängt ihn wieder ein. Alt+Tab wechselt das Fenster.

Für einen automatisierten Solo-Lauf des echten Players kann die Mission mit `--solo-smoke --shutdown-after=8` gestartet werden. Die Logzeilen `SOLO_SMOKE_DELIVERY` und `SOLO_SMOKE_PASS` belegen Geschenkaufnahme, Lieferung, Rückkehr, Abschluss und Neustart:

```powershell
& 'C:/tmp/slh13-windows/DeliveryHouse.exe' --solo-smoke --shutdown-after=8 -logFile C:/tmp/slh13-windows/solo-smoke.log
```

Bekannte Grenzen: Der Player wurde als Windows-x64-Mono-Build erstellt. Die Abnahme deckt Solo sowie bis zu vier Prozesse auf localhost ab. LAN-/WAN-Verbindungen und weitere Fehlerfälle jenseits der dokumentierten Disconnect-Tests wurden nicht geprüft.

## 5. Aufbau im Editor

`Assets/Game/Player/FirstPersonController.cs` enthält den Controller und seine lokalen Input Actions. Die Testszene liegt unter `Assets/Game/Scenes`, ihre Blockout-Materialien unter `Assets/Game/Materials`. Das URP-Profil liegt unter `Assets/Settings`.

Wähle **LocalPlayer** in der Hierarchy, um die Bewegungswerte im Inspector anzupassen. Das Objekt sitzt mit seinem Ursprung an den Füßen und hat Skalierung (1, 1, 1). Es verwendet einen **Character Controller** mit Höhe 1,8 m, Radius 0,32 m und Center Y 0,9 m. Die untergeordnete **ViewCamera** sitzt im Stand auf Y 1,65 m. Sie ist im Feld **View Pivot** zugewiesen. **Ceiling Layers** schließt den Spieler-Layer aus. Wände, Boden und Dach sind auf **Default** und haben Collider. Die Eingabe und Bewegung laufen im dynamischen Bild-Update, ebenso die Kamera als Kind des Spielerobjekts.

Falls du die Szene in einem frisch angelegten Projekt verwenden willst, wähle in Hub **New project → Universal 3D** mit der Editorversion aus `ProjectVersion.txt`. Installiere über **Window → Package Management → Package Manager → Unity Registry** das **Input System**. Setze unter **Edit → Project Settings → Player → Other Settings → Configuration** das Feld **Active Input Handling** auf **Input System Package (New)** und starte den Editor neu. Prüfe unter **Project Settings → Input System Package**, dass **Update Mode** auf **Process Events In Dynamic Update** steht. Kopiere `Assets/Game` einschließlich der `.meta`-Dateien, benenne Layer 8 in **Player** um und öffne die Testszene. Übernimm sie für einen Build in die Szenenliste des Zielprojekts.

Der Spieler nutzt die Kapsel für direkte Steuerung und Kollisionen. Die späteren Geschenke bekommen ihre eigene Rigidbody-Physik.

## 6. Git

Das lokale Repository liegt direkt im Unity-Projektordner. Unity speichert Assets, ihre `.meta`-Dateien, Paketdefinitionen und Projekteinstellungen im Repository. Generierte Ordner wie `Library`, `Temp` und `Builds` sind ausgeschlossen. Prüfe im Editor unter **Project Settings → Editor → Asset Serialization** die Einstellung **Force Text** und unter **Version Control** die Einstellung **Visible Meta Files**.

Für Issues, Worktrees, Pull Requests und CI siehe [Zusammenarbeit](CONTRIBUTING.md).
