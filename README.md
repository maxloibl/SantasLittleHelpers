# Santa's Little Helpers

## 1. Unity unter Windows einrichten

1. Öffne [Unity Hub](https://unity.com/download) und melde dich mit deinem Unity-Konto an. Aktiviere in Hub eine Lizenz, falls der Editor danach fragt.
2. Installiere über **Installs → Install Editor** genau die Version aus [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt). Der aktuelle Stand verwendet **Unity 6000.6.8f1**. Auf diesem Rechner liegt der Editor unter `C:\6000.6.8f1`. [Unity-Releasearchiv](https://unity.com/releases/editor/whats-new/6000.6.8f1)
3. Wähle für spätere Windows-Builds bei Bedarf **Windows Build Support (IL2CPP)**. Für den ersten Test reicht der Editor. Git und Visual Studio wurden auf diesem Rechner bereits gefunden. In Visual Studio kann bei Bedarf über den Visual Studio Installer die Workload **Game development with Unity** ergänzt werden.
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
- **Windows-Ziel.** Öffne **File → Build Profiles**, wähle **Windows** und aktiviere das Profil, falls nötig. `MovementSandbox` ist in der globalen Szenenliste eingetragen. Wähle **Build and Run** und speichere den Build im Ordner `Builds/Windows`. Wiederhole den Durchgangstest im eigenständigen Fenster.

## 4. Aufbau im Editor

`Assets/Game/Player/FirstPersonController.cs` enthält den Controller und seine lokalen Input Actions. Die Testszene liegt unter `Assets/Game/Scenes`, ihre Blockout-Materialien unter `Assets/Game/Materials`. Das URP-Profil liegt unter `Assets/Settings`.

Wähle **LocalPlayer** in der Hierarchy, um die Bewegungswerte im Inspector anzupassen. Das Objekt sitzt mit seinem Ursprung an den Füßen und hat Skalierung (1, 1, 1). Es verwendet einen **Character Controller** mit Höhe 1,8 m, Radius 0,32 m und Center Y 0,9 m. Die untergeordnete **ViewCamera** sitzt im Stand auf Y 1,65 m. Sie ist im Feld **View Pivot** zugewiesen. **Ceiling Layers** schließt den Spieler-Layer aus. Wände, Boden und Dach sind auf **Default** und haben Collider. Die Eingabe und Bewegung laufen im dynamischen Bild-Update, ebenso die Kamera als Kind des Spielerobjekts.

Falls du die Szene in einem frisch angelegten Projekt verwenden willst, wähle in Hub **New project → Universal 3D** mit der Editorversion aus `ProjectVersion.txt`. Installiere über **Window → Package Management → Package Manager → Unity Registry** das **Input System**. Setze unter **Edit → Project Settings → Player → Other Settings → Configuration** das Feld **Active Input Handling** auf **Input System Package (New)** und starte den Editor neu. Prüfe unter **Project Settings → Input System Package**, dass **Update Mode** auf **Process Events In Dynamic Update** steht. Kopiere `Assets/Game` einschließlich der `.meta`-Dateien, benenne Layer 8 in **Player** um und öffne die Testszene. Übernimm sie für einen Build in die Szenenliste des Zielprojekts.

Der Spieler nutzt die Kapsel für direkte Steuerung und Kollisionen. Die späteren Geschenke bekommen ihre eigene Rigidbody-Physik.

## 5. Git

Das lokale Repository liegt direkt im Unity-Projektordner. Unity speichert Assets, ihre `.meta`-Dateien, Paketdefinitionen und Projekteinstellungen im Repository. Generierte Ordner wie `Library`, `Temp` und `Builds` sind ausgeschlossen. Prüfe im Editor unter **Project Settings → Editor → Asset Serialization** die Einstellung **Force Text** und unter **Version Control** die Einstellung **Visible Meta Files**.

Für Issues, Worktrees, Pull Requests und CI siehe [Zusammenarbeit](CONTRIBUTING.md).
