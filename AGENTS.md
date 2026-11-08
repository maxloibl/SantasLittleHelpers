# AGENTS.md

## Arbeitsauftrag

- Ein GitHub Issue, ein verantwortlicher Agent und ein Pull Request. Das Issue enthält Ziel, Abnahmekriterien, Abhängigkeiten und die betroffenen Dateien oder Systeme.
- Starte nur freigegebene Issues mit erfüllten Abhängigkeiten. Arbeite in einem eigenen Git-Worktree auf einem Branch `issue/<nummer>-<kurzname>`, ausgehend von `origin/main`. Prüfe vor Änderungen den Branch und den Arbeitsstand.
- Stimme Überschneidungen vor der Umsetzung ab. Für gemeinsam genutzte Szenen, Prefabs, Eingabe-Assets, `ProjectSettings/` und `Packages/` gibt es jeweils genau einen aktiven Bearbeiter. Nutze für isolierte Spieltests eine eigene Testszene, wenn das Issue eine braucht.
- Bleib beim Issue. Melde Abweichungen, neue Paketabhängigkeiten oder Änderungen an gemeinsam genutzten Schnittstellen, sobald sie absehbar sind.
- Eröffne zur Übergabe einen Pull Request gegen `main` mit `Closes #<nummer>`. Liste durchgeführte Prüfungen mit Ergebnis und offene Prüfungen auf. Der Maintainer entscheidet über den Merge.

## Unity und C#

- Verwende die exakte Version aus `ProjectSettings/ProjectVersion.txt`. Änderungen an Unity-Version, Paketen und Projekteinstellungen gehören in ein eigenes Issue, sofern sie nicht Teil des aktuellen Auftrags sind. Committe `Packages/manifest.json` und `Packages/packages-lock.json` gemeinsam, wenn sich Pakete ändern.
- Spielcode liegt unter `Assets/Game/`. Folge der vorhandenen C#-Struktur. Verwende PascalCase für Typen und Methoden, camelCase für private Felder und `[SerializeField] private` für Inspector-Referenzen.
- Ändere Assets samt `.meta`-Dateien. Verschiebe oder benenne sie im Unity Editor um, damit ihre GUIDs und Referenzen erhalten bleiben. Unity-Assets verwenden Force Text und Visible Meta Files.
- Halte den Diff aufgabenbezogen. Behalte bestehende Eingaben und Inspector-Referenzen bei, solange das Issue keine Änderung daran vorsieht. Generierte Ordner, Builds, lokale Einstellungen und Zugangsdaten bleiben außerhalb von Git.
- Führe `python scripts/check-unity-repo.py` aus, nachdem neue Dateien vorgemerkt sind. Prüfe C#- und Asset-Änderungen im Unity Editor. Teste geändertes Spielverhalten in Play Mode anhand der Akzeptanzkriterien. Halte im PR fest, welche Unity-Prüfungen du tatsächlich durchgeführt hast.
