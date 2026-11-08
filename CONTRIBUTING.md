# Zusammenarbeit

GitHub Issues halten Aufträge und Abhängigkeiten fest. Git-Worktrees geben jedem Agent einen eigenen Projektordner. Die Pull Requests führen die Ergebnisse auf `main` zusammen.

## Ablauf

1. Lege pro Arbeitspaket ein Issue mit der Vorlage **Umsetzungsauftrag** an. Markiere mit `ready`, welche Issues parallel starten können. Halte Blocker im Issue fest.
2. Weise jedes Issue einer separaten Codex-Aufgabe zu und gib die Issue-URL und den gewünschten Branch mit. Nutze pro Aufgabe einen eigenen Worktree. Kennzeichne begonnene Issues mit `in-progress`.
3. Prüfe vor der Vergabe die Dateigrenzen. Beispielsweise können ein Geschenk-Prefab und ein KI-Prototyp mit eigener Testszene parallel entstehen. Wenn beide dieselbe Spielszene oder eine gemeinsame Schnittstelle brauchen, vereinbare diese zuerst und plane die Integration als eigenes Issue.
4. Der Agent legt einen PR mit `Closes #<nummer>` und seinen Prüfergebnissen vor. Nach Review und erfolgreichem Check führt der Maintainer den PR per Squash-Merge zusammen. GitHub schließt das verknüpfte Issue beim Merge in den Standardbranch.
5. Bei einer veralteten Basis aktualisiert der jeweilige Agent seinen Branch und wiederholt die betroffenen Prüfungen. Nimm bei Konflikten an Szenen oder Prefabs die Änderung im Unity Editor vor und kontrolliere die Referenzen.

## GitHub einmal einrichten

- Lege die Labels `ready` und `in-progress` an. Issues ohne Startfreigabe bleiben im Backlog.
- Sobald der erste Lauf grün ist, richte für `main` ein Ruleset ein, sofern der GitHub-Tarif es unterstützt. Verlange einen Pull Request und den Check **Repository check**. Blockiere Force Pushes. Bei mehreren menschlichen Reviewern kannst du eine Freigabe verlangen. Als alleiniger Maintainer prüfst und mergst du Agent-PRs selbst.
- **Repository check** prüft getrackte Unity-Metadateien, GUID-Duplikate, die Editorversion, direkte Pakete im Lockfile und versehentlich eingecheckte generierte Ordner. Lokal läuft dieselbe Prüfung mit `python scripts/check-unity-repo.py` im Projektordner.
- Unity-Kompilierung, EditMode- und PlayMode-Tests sind ein separater CI-Ausbauschritt. Dafür muss ein Runner die exakte Editorversion und eine passende Unity-Lizenz bereitstellen. Bis dahin stehen Editor- und Play-Mode-Prüfungen im jeweiligen PR. Unity-Zugangsdaten gehören in GitHub-Secrets oder eine geschützte Runner-Umgebung.
