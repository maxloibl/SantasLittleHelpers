# Entwicklung und Windows-Abnahme

## Einstieg

Für einen Issue-Branch den neuesten Stand von `origin/main` abrufen und einen eigenen Worktree anlegen. Abhängigkeiten und Szenenbesitz vor Beginn im Issue prüfen. Bei parallelen Aufgaben dürfen gemeinsam genutzte Szenen, Prefabs und Projekteinstellungen jeweils nur einen aktiven Bearbeiter haben. Ein Issue erhält einen zugehörigen PR mit `Closes #<Nummer>`, getesteten Fällen und offenen Prüfungen. [AGENTS](../../AGENTS.md) · [CONTRIBUTING](../../CONTRIBUTING.md)

## Build

Die Unity-Version kommt aus [ProjectVersion.txt](../../ProjectSettings/ProjectVersion.txt). Benötigt wird Windows Build Support (Mono). Befehle im Repo-Root, mit lokal angepasstem Editorpfad:

```powershell
$unity = 'C:/6000.6.8f1/Editor/Unity.exe'
$repo = (Get-Location).Path
New-Item -ItemType Directory -Force Builds/Windows, Logs | Out-Null
& $unity -batchmode -quit -projectPath $repo -buildTarget Win64 -executeMethod SantasLittleHelpers.Editor.WindowsPlayerBuild.Build -slhBuildPath "$repo/Builds/Windows/Santa.exe" -logFile "$repo/Logs/windows-build.log"
```

Das Log muss `WINDOWS_BUILD result=Succeeded` melden. Die EXE zusammen mit dem vollständigen Build-Ordner verwenden. Ein laufender Player kann seine Build-Dateien sperren. Vor dem Überschreiben eigene Testprozesse beenden oder einen neuen Zielordner verwenden. Bei lokaler Speicherknappheit wurde für die Tests der Modell- und Eingabeänderungen `--burst-disable-compilation` verwendet. Diese Build-Abweichung im jeweiligen Nachweis angeben.

## Prüfreihenfolge

1. `python scripts/check-unity-repo.py` und `git diff --check`. Die GitHub Action [Repository check](../../.github/workflows/unity-repository.yml) prüft die Repo-Struktur.
2. Szenen- und C#-Änderungen mit der festgelegten Unity-Version importieren und im Editor prüfen. Bei Art-Änderungen Build-Setup, Quelllizenzen, Pivot, Renderers und Materialbudgets nach [Art-Pipeline](../art/ASSET-PIPELINE.md) prüfen.
3. Parser-Fixtures und den echten Windows-Player prüfen. Die Prozesse nutzen getrennte Logs, Slot-Profile und UDP-Ports. Die vollständige Fallliste steht in der [Testmatrix](../PRODUCT-TEST-MATRIX.md).
4. Im sichtbaren Spiel Slot-CRUD, Laden nach Neustart, Settings, Fokus, Hütte, Missionsstart per Taste und Maus, Kamerasicht und Modelle prüfen. Einen LAN-Nachweis nur mit getrennten Rechnern ausweisen.

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/run-product-regressions.ps1 -Mode fixture
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/run-product-regressions.ps1 -Mode player -BuildPath Builds/Windows/Santa.exe -TimeoutSeconds 90
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/run-coop-regressions.ps1 -Scenario coop-smoke -BuildPath Builds/Windows/Santa.exe -Port 7784
```

`capture-all-caught` benötigt den isolierten Capture-Testplayer aus [build-coop-regression-players.ps1](../../scripts/build-coop-regression-players.ps1). Der Produktplayer deckt Capture/Rescue und Heavy-Grip über `coop-smoke` ab. `--legacy-start`, `--solo-smoke` und die vorhandenen Koop-Einstiege bleiben für die alten Regressionen verfügbar.

Zum Nachweis gehören Testdatum, Commit, Unity-Version, Build-Optionen, getestete Fälle, Ergebnisse und Fundstellen. Logs, lokale Profile und Builds gehören in ignorierte Verzeichnisse. Ein kompakter Nachweis und Verweise auf GitHub reichen fürs Repository. [Sprintstand](../delivery/STATUS.md)
