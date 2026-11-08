# MVP-Backlog und Review Loop

**MVP-Ziel:** 1–4 Spieler starten am Schlitten, bringen Geschenke ins Haus und kehren zum Schlitten zurück. Gewicht und Geräusche beeinflussen die Auslieferung. Bewohner können Spieler entdecken und festsetzen. Im Koop können Spieler ein schweres Geschenk gemeinsam tragen und einander befreien.

Der First-Person-Controller und `MovementSandbox` sind die Ausgangsbasis. Die Sprintnummern geben eine Reihenfolge vor. Jeder Sprint endet mit einem spielbaren Ergebnis. Die Paket-IDs unten werden beim Übertragen nach GitHub durch Issue-Nummern ergänzt.

| Sprint | Parallel starten | Danach | Sprint-Abnahme auf `main` |
| --- | --- | --- | --- |
| 1 · Tragen | S1-A, S1-B, S1-C | S1-D nach A und B | Ein Geschenk lässt sich im Haus aufnehmen, tragen und ablegen. |
| 2 · Ausliefern | S2-A, S2-B, S2-C | S2-D nach A und B | Eine Solo-Runde funktioniert mit Lieferung, Rückkehr und Alarm. Zwei Spieler können sich in einer separaten Testszene verbinden. |
| 3 · Koop | S3-A, S3-B, S3-C | S3-D nach A, B und C | Zwei Spieler schaffen dieselbe Runde gemeinsam, tragen ein schweres Geschenk und können einander befreien. |
| 4 · MVP-Abnahme | S4-A, S4-B, S4-C | S4-D nach A, B und C | Eine Runde funktioniert solo, zu zweit und zu viert im Windows-Build. |

Innerhalb jeder Parallelgruppe bekommt jedes Paket seinen eigenen Worktree und, falls nötig, eine eigene Testszene. Die Hauptszene `DeliveryHouse` gehört jeweils dem Integrationspaket. Änderungen an `Packages/`, `ProjectSettings/` und gemeinsamen Prefabs werden vor dem Start einem Bearbeiter zugewiesen. **Startfreigabe für Sprint 1** gibt es, sobald Agent-Regeln, Vorlagen und Repository-Check auf `main` sind.

## Sprint 1 · Geschenkphysik und Testhaus

### S1-A · Ein Geschenk aufnehmen, tragen und ablegen

- **Abhängigkeit und Dateigrenze:** Startbereit nach Repo-Einrichtung. `Assets/Game/Gifts/` und eigene Testszene. Die Eingabe zum Greifen bleibt am Interaktionsskript. Der First-Person-Controller bleibt außerhalb des Issues.
- **Abnahme:** Ein lokaler Spieler kann per Blick und Interaktion ein nahes Geschenk aufnehmen und ablegen. Leichte und schwere Rigidbody-Varianten unterscheiden sich beim Tragen spürbar. Das Geschenk bleibt bei einem Wandkontakt physisch begrenzt, folgt nach Freigabe wieder der Schwerkraft und kann nach dem Ablegen erneut aufgenommen werden.

### S1-B · Haus und Schlittenstellplatz als Blockout

- **Abhängigkeit und Dateigrenze:** Startbereit nach Repo-Einrichtung. `Assets/Game/Environment/` und eigene Blockout-Szene.
- **Abnahme:** Ein Spieler kann vom Schlittenstellplatz über einen Eingang zu einem Lieferplatz und zurück laufen. Eine Sichtbarriere und ein niedriger Weg lassen sich mit dem vorhandenen Controller testen. Spawn, Lieferplatz und Schlitten sind im Editor eindeutig markiert.

### S1-C · Netzcode mit zwei Instanzen prüfen

- **Abhängigkeit und Dateigrenze:** Startbereit nach Repo-Einrichtung. Wegwerf-Prototyp im eigenen Worktree. Der PR hält Ergebnis, Paket und exakte Version, Startschritte und Zuständigkeit für Rigidbody-Physik fest.
- **Abnahme:** Host und Client laufen in zwei Instanzen mit der eingecheckten Unity-Version. Beide sehen die Position des jeweils anderen. Der Versuch zeigt, wie ein gemeinsames Rigidbody vom Host gesteuert werden kann, und dokumentiert Verbindungsabbruch und bekannte Grenzen. Das Ergebnis benennt eine Entscheidung oder einen konkreten Blocker für S2-C.

### S1-D · Erster spielbarer Weg mit Geschenk

- **Abhängigkeit und Dateigrenze:** S1-A, S1-B. Integration in `Assets/Game/Scenes/DeliveryHouse.unity` und, falls nötig, exklusiv in der Build-Szenenliste.
- **Abnahme:** Ein Spieler startet am Schlitten, trägt ein Geschenk durch den Hauseingang, legt es am markierten Ort ab und läuft zurück. Der Ablauf gelingt nach Neustart erneut. `MovementSandbox` dient weiterhin dem Controller-Test.

## Sprint 2 · Solo-Lieferung, Alarm und Netzbasis

### S2-A · Lieferziel, Schlitten und Rundenabschluss

- **Abhängigkeit und Dateigrenze:** S1-A, S1-B. `Assets/Game/Delivery/` und eigene Testszene.
- **Abnahme:** Eine Runde startet am Schlitten. Das Ziel zählt das richtige Geschenk genau einmal. Die Runde endet erfolgreich, sobald das Ziel erfüllt ist und der Spieler zum Schlitten zurückkehrt. Neustart setzt Fortschritt und Geschenke zurück. Die Erfolgsbedingung ist in der Testszene sichtbar.

### S2-B · Bewohnerpatrouille und eskalierender Alarm

- **Abhängigkeit und Dateigrenze:** S1-B. `Assets/Game/AI/` und eigene Testszene. Dokumentiere die Eingänge für Sichtkontakt und Geräusch für die spätere Integration.
- **Abnahme:** Ein Bewohner patrouilliert, untersucht ein Geräusch und wechselt bei anhaltendem Sichtkontakt in Verfolgung. Eine Sichtbarriere unterbricht den Kontakt. Nach einer prüfbaren Zeit sinkt der Alarm bei fehlenden Reizen. Die Zustände sind im Spiel oder Inspector sichtbar.

### S2-C · Host und Client in getrennten Windows-Instanzen

- **Abhängigkeit und Dateigrenze:** S1-C mit bestätigtem Paket. `Assets/Game/Networking/`, eigene Netz-Testszene und exklusiver Zugriff auf `Packages/`. Änderungen am Spieler-Prefab für den Netzeinstieg gehören zu diesem Paket.
- **Abnahme:** Host und Client können über eine direkte lokale Verbindung beitreten, unabhängig laufen und sich gegenseitig sehen. Eingabe und Kamera wirken nur auf den eigenen Spieler. Ein normaler Client-Abbruch räumt seine Figur auf. Einrichtung und Start sind im PR reproduzierbar.

### S2-D · Solo-Mission mit hörbarem Geschenk

- **Abhängigkeit und Dateigrenze:** S1-D, S2-A, S2-B. Integration in `DeliveryHouse`. Die Verknüpfung von Kollisionsgeräusch und Alarm gehört zu diesem Paket.
- **Abnahme:** Der vollständige Solo-Weg von Schlitten zu Lieferziel und zurück ist spielbar. Ein kräftiger Geschenkaufprall kann den Bewohner ablenken. Ein ruhiger Weg bleibt ohne Alarm möglich. Abschluss und Neustart funktionieren nach beiden Varianten.

## Sprint 3 · Koop-Interaktionen

Vor der Vergabe werden Zuständigkeiten für drei Schnittstellen festgehalten. S3-A besitzt den Zustand von Geschenken, S3-B den Zustand gefangener Spieler und S3-D bindet beides an die Mission. Beide Systeme prüfen Interaktionen auf dem Host.

### S3-A · Geschenke im Koop und gemeinsames Tragen

- **Abhängigkeit und Dateigrenze:** S1-A, S2-C. Geschenk-Prefab, Tragesystem und Netzadapter unter `Assets/Game/Gifts/`, eigene Koop-Testszene.
- **Abnahme:** Zwei Instanzen sehen dasselbe Geschenk an konsistenter Position. Ein leichtes Geschenk kann solo getragen werden. Ein schweres lässt sich allein eingeschränkt bewegen und zu zweit kontrolliert tragen. Loslassen oder Trennung eines Clients hinterlässt ein wieder greifbares Geschenk. Gleichzeitige Greifversuche erzeugen keine doppelten Griffe am selben Platz.

### S3-B · Gefangennahme und Befreiung

- **Abhängigkeit und Dateigrenze:** S2-B, S2-C. `Assets/Game/Capture/` und eigene Testszene. Übergabe aus dem Alarmzustand über den vereinbarten Eingang.
- **Abnahme:** Ein Bewohner kann bei erreichter Verfolgungsbedingung einen Spieler festsetzen. Ein freier Mitspieler kann ihn befreien. Das trägt der Host für beide Spieler sichtbar ein. Bei Solo-Gefangennahme oder wenn kein freier Spieler mehr übrig ist, endet die Runde mit einer Neustartmöglichkeit.

### S3-C · Cozy-Lesbarkeit im Blockout

- **Abhängigkeit und Dateigrenze:** S1-B. Eigene Art-Testszene und neue Dateien unter `Assets/Game/Art/`.
- **Abnahme:** Ein eigenständiger, rundlicher Elf-Prototyp und ein kleiner Satz weihnachtlicher Materialien machen Spieler, Geschenk und Lieferziel in der Testszene klar unterscheidbar. Materialien und Prefabs sind für den Integrator nutzbar. Gameplay-Collider bleiben Eigentum der jeweiligen Gameplay-Pakete.

### S3-D · Gemeinsame Runde am Schlitten integrieren

- **Abhängigkeit und Dateigrenze:** S2-D, S3-A, S3-B, S3-C. Integration in `DeliveryHouse` und alleinige Zuständigkeit für die Verbindung von Rundenzustand, Alarm und Koop-Interaktionen.
- **Abnahme:** Host und Client starten am Schlitten, liefern gemeinsam ein Geschenk ab und kehren zurück. Beide sehen denselben Fortschritt. Festsetzen, Befreien und eine Runde ohne Alarm funktionieren in derselben Szene. Die Mission bleibt solo mit einem leichten Geschenk abschließbar.

## Sprint 4 · Windows-Abnahme

### S4-A · Windows-Build und Startanleitung

- **Abhängigkeit und Dateigrenze:** S3-D. Build-Einstellungen und Anleitung.
- **Abnahme:** Ein Windows-Build aus einem frischen Checkout startet die Mission. Host- und Client-Start, Steuerung und bekannte Grenzen stehen in der Anleitung. Der Build wird als Artefakt übergeben und bleibt außerhalb des Repositorys.

### S4-B · Regressionen für Koop-Zustände prüfen

- **Abhängigkeit und Dateigrenze:** S3-D. Testfälle und isolierte Testszenen. Gameplay-Funde als separate Fix-Issues mit klarer Dateizuständigkeit.
- **Abnahme:** Wiederholbare Prüfungen für doppelte Lieferung, gleichzeitiges Greifen, Verbindungsabbruch beim Tragen, alle Spieler gefangen und Neustart. Ergebnisse und Reproduktionsschritte sind erfasst. Automatisiere stabil prüfbare Zustandsregeln, sofern ein Unity-Testrunner bereitsteht.

### S4-C · Unity-CI für den freigegebenen Editor

- **Abhängigkeit und Dateigrenze:** Repo-Einrichtung sowie bestätigter Runner mit passender Unity-Version und Lizenz. Nur Workflow und Runner-Dokumentation. Wenn die Voraussetzung fehlt, bleibt dieses Issue blockiert. S4-D kann nach dokumentierter manueller Windows-Abnahme laufen.
- **Abnahme:** Ein vertrauenswürdiger Lauf kompiliert den Projektstand und veröffentlicht die Ergebnisse der vorhandenen Unity-Tests. Ein gezielt eingefügter Kompilierfehler lässt den Check scheitern. Lizenz und Zugangsdaten liegen in der geschützten Runner-Umgebung oder in GitHub-Secrets.

### S4-D · MVP-Abnahme mit 1, 2 und 4 Spielern

- **Abhängigkeit und Dateigrenze:** S4-A, S4-B sowie S4-C oder dokumentierte manuelle Prüfung. Abnahmeprotokoll und Integration etwaiger freigegebener Fixes.
- **Abnahme:** Solo-Abschluss, gemeinsames Tragen zu zweit und eine vollständige Runde zu viert im Windows-Build. Prüfe Erfolg, Gefangennahme, Befreiung, Client-Abbruch und Neustart. Jeder blockierende Fund bekommt ein Issue. Die MVP-Abnahme schließt erst, wenn die blockierenden Funde behoben und erneut geprüft sind.

## Review Loop

| Schritt | Wer | Ergebnis |
| --- | --- | --- |
| 1 · Auftrag freigeben | Maintainer | Ein überprüfbares Ergebnis, Dateigrenzen und Abhängigkeiten stehen im Issue. `ready` gibt die Umsetzung frei. |
| 2 · Zur Prüfung übergeben | Umsetzungsagent | Der PR enthält `Closes #<nummer>`, Testschritte, Prüfergebnisse, offene Prüfungen und Hinweise zur Integration. |
| 3 · Unabhängig prüfen | Zweiter Agent in separater Aufgabe | Prüft den PR-Stand gegen das Issue. Meldet konkrete Funde mit Datei oder Repro-Schritten und trennt Blocker von optionalen Hinweisen. Dokumentiert, welche Unity-Prüfungen er selbst ausführen konnte. |
| 4 · Nachbessern und erneut prüfen | Umsetzung und Reviewer | Der Umsetzungsagent behebt Blocker im selben PR und nennt den neuen Commit. Der Reviewer prüft die Änderungen und wiederholt die betroffenen Checks. Schritte 3 und 4 wiederholen sich bis zur Freigabe. |
| 5 · Mergen und gemeinsam testen | Maintainer und Sprint-Integrator | Nach grünem Repository-Check und geklärten Blockern führt der Maintainer den Squash-Merge aus. Am Sprintende läuft der oben definierte Abnahmepfad auf dem zusammengeführten Stand von `main`. |

**Blocker** sind verletzte Akzeptanzkriterien, reproduzierbare Regressionen, kaputte Unity-Referenzen und fehlende Nachweise für ein verändertes Kernverhalten. Kleinere Ideen werden nur dann zu Folge-Issues, wenn der Maintainer sie übernehmen will. Wenn ein Agent keinen Unity Editor nutzen kann, übernimmt ein Reviewer mit Editor oder der Maintainer die offene Prüfung.

Für eine separate Review-Aufgabe genügt der Auftrag: „Prüfe PR <URL> gegen das verknüpfte Issue. Arbeite lesend auf dem aktuellen PR-Stand. Liste Blocker mit Repro-Schritten und nenne die Prüfungen, die du selbst ausführen konntest.“

**Erste Vergabe:** Nach dem Setup können S1-A, S1-B und S1-C zeitgleich starten. Weise jedem PR einen anderen Agent als Reviewer zu. S1-D bleibt bis zum Merge von S1-A und S1-B blockiert.
