# Sprintstand und Nachweise

Stand der Bestandsaufnahme: 16. November 2026. Die verlinkten Issues und PRs zeigen den jeweils aktuellen Bearbeitungsstand.

| Sprint | Umfang und Abhängigkeiten | Stand |
| --- | --- | --- |
| [Sprint 5](https://github.com/maxloibl/SantasLittleHelpers/milestone/1) | Produktverträge, Asset-Regeln und Quellenprüfung, Speichergrundlage, lokale Einstellungen, MVP-Smoke-Stabilisierung | Geschlossen |
| [Sprint 6](https://github.com/maxloibl/SantasLittleHelpers/milestone/2) | Home, Save-/Settings-Anbindung, direkte Sitzung, Missionsadapter, Hütten-Blockout und Art-Kits. Home-Integration nach Home, SessionService und Missionsadapter. | Geschlossen |
| [Sprint 7](https://github.com/maxloibl/SantasLittleHelpers/milestone/3) | Ausgestattete Hütte und Hütten-Runtime → integrierter Spielfluss → finale Modelle → Windows-Abnahme. Parser und Testmatrix waren als eigener Track parallel. | Abnahme offen |

## Issue-Register

| Arbeitsbereich | Aufgaben |
| --- | --- |
| Produktgrundlagen | [Produktverträge #34](https://github.com/maxloibl/SantasLittleHelpers/issues/34) · [Art-Stil und Packprüfung #35](https://github.com/maxloibl/SantasLittleHelpers/issues/35) · [Versionierter Speicher #36](https://github.com/maxloibl/SantasLittleHelpers/issues/36) · [Lokale Einstellungen #37](https://github.com/maxloibl/SantasLittleHelpers/issues/37) · [Capture-/Rescue-Smoke #52](https://github.com/maxloibl/SantasLittleHelpers/issues/52) |
| Home und parallel vorbereitete Assets | [Home UI #38](https://github.com/maxloibl/SantasLittleHelpers/issues/38) · [Sitzungsdienst #39](https://github.com/maxloibl/SantasLittleHelpers/issues/39) · [Missionsadapter #40](https://github.com/maxloibl/SantasLittleHelpers/issues/40) · [Home-Integration #41](https://github.com/maxloibl/SantasLittleHelpers/issues/41) · [Hütten-Blockout #42](https://github.com/maxloibl/SantasLittleHelpers/issues/42) · [Hütten-Kit #43](https://github.com/maxloibl/SantasLittleHelpers/issues/43) · [Charaktermodelle #44](https://github.com/maxloibl/SantasLittleHelpers/issues/44) · [Missionsmodelle #45](https://github.com/maxloibl/SantasLittleHelpers/issues/45) · [Join-Kapazität #67](https://github.com/maxloibl/SantasLittleHelpers/issues/67) |
| Integration und Abnahme | [Hütten-Runtime #46](https://github.com/maxloibl/SantasLittleHelpers/issues/46) · [Licht und Ausstattung #47](https://github.com/maxloibl/SantasLittleHelpers/issues/47) · [Integrierter Spielfluss #48](https://github.com/maxloibl/SantasLittleHelpers/issues/48) · [Runtime-Modelle #49](https://github.com/maxloibl/SantasLittleHelpers/issues/49) · [Testmatrix #50](https://github.com/maxloibl/SantasLittleHelpers/issues/50) · [Abnahme #51](https://github.com/maxloibl/SantasLittleHelpers/issues/51) · [Anordnung der Hüttenaktionen #73](https://github.com/maxloibl/SantasLittleHelpers/issues/73) · [Vier-Spieler-Reset #74](https://github.com/maxloibl/SantasLittleHelpers/issues/74) · [Missionsstart-Eingabe #77](https://github.com/maxloibl/SantasLittleHelpers/issues/77) |

Die GitHub-Issues führen die Abnahmekriterien, Dateiverantwortung und blockierenden Aufgaben. Bei der Ausführung wurden Luna und OpenCode Go mit DeepSeek v4.1 Flash für abgegrenzte Aufgaben verwendet. Session-, Szenen- und Build-Integration wurden jeweils im eigenen Worktree zusammengeführt.

## Build-Nachweise

| Stand | Unity / Ziel | Reproduzierbarer Nachweis |
| --- | --- | --- |
| Integrierter Spielfluss, PR #71, Windows-Player aus Commit `8412bf0` | 6000.6.8f1 / Windows x64 | Produktmatrix, alle neun Prozessfälle über mehrere Läufe bestanden. |
| Runtime-Modelle, PR #72, Build aus Commit `f9bf66c` | 6000.6.8f1 / Windows x64 Mono, Burst lokal deaktiviert | Solo/Duo/Vierergruppe und `coop-smoke` bestanden. Der Legacy-Viererreset zeigte die in #74 dokumentierte Reihenfolge. |
| Hütten-Eingabekorrektur, PR #78, Build aus Commit `40ba0a9` | 6000.6.8f1 / Windows x64 Mono, Burst lokal deaktiviert | Sichtbarer Mausklick in Solo wechselte von CozyCabin nach DeliveryHouse. Das Log meldete `PRODUCT_FLOW role=Solo state=Mission result=Started error=None`. |

## Integrierte Bausteine

- [PR #69](https://github.com/maxloibl/SantasLittleHelpers/pull/69) verbindet den Windows-Home-Einstieg mit Slot, Sitzung und Missionsabschluss. Der Windows-Neustart bestätigte genau einen gespeicherten Solo-Commit.
- [PR #66](https://github.com/maxloibl/SantasLittleHelpers/pull/66) ergänzt Möblierung, Licht und Winterdetails. [PR #70](https://github.com/maxloibl/SantasLittleHelpers/pull/70) ergänzt vier Hüttenposen, Wiedervergabe frei gewordener Slots und Host-Freigabe. [PR #68](https://github.com/maxloibl/SantasLittleHelpers/pull/68) korrigiert die Kapazitätsprüfung bei gleichzeitigen Joins.
- [PR #71](https://github.com/maxloibl/SantasLittleHelpers/pull/71) führt Home, Hütte und DeliveryHouse zusammen. Alle neun Windows-Prozessfälle bestanden über getrennte Läufe. Darunter waren Solo/Duo/Vierergruppe, Save-Roundtrip, Gast-Schreibschutz, ungültige Adresse, Rejoin, volle Hütte und Hostverlust.
- [PR #72](https://github.com/maxloibl/SantasLittleHelpers/pull/72) installiert die finalen Charakter- und Missionsmodelle. Kalter Unity-Import, Windows-Build, Solo/Duo/Vierergruppe und Koop-Smoke mit Heavy-Grip, Capture, Rescue und Reset bestanden.
- [PR #75](https://github.com/maxloibl/SantasLittleHelpers/pull/75) trennt die überlappenden Hüttenbuttons. Der Windows-Build des gemergten Quellstands öffnete erfolgreich Home und die Hütte.

## Offene Freigabe

| Aufgabe | Nachweis und nächste Prüfung |
| --- | --- |
| [Missionsstart per Maus #77](https://github.com/maxloibl/SantasLittleHelpers/issues/77) | Die Kamerasteuerung beanspruchte den Mausklick im Hüttenmenü. Der lokale Windows-Build zu [PR #78](https://github.com/maxloibl/SantasLittleHelpers/pull/78) lud nach echtem Mausklick die Solo-Mission. Der PR ergänzt Enter als Startoption. PR-Status und weitere Prüfungen sind dort verlinkt. |
| [Vier-Spieler-Reset #74](https://github.com/maxloibl/SantasLittleHelpers/issues/74) | Ein Legacy-Smoke protokollierte vor dem Reset erst drei Replika-Posen. [PR #76](https://github.com/maxloibl/SantasLittleHelpers/pull/76) wartet auf vier initialisierte Spieler und ihre Posen. Ergebnis im erneuten Windows-Lauf nachweisen. |
| [Windows-Abnahme #51](https://github.com/maxloibl/SantasLittleHelpers/issues/51) | Dieselbe frische Windows-Version für Prozessmatrix, Koop-Regression und sichtbaren Solo-/Koop-Durchlauf verwenden. LAN über zwei Rechner separat protokollieren. Dafür liegt bisher kein Nachweis vor. |
| [CI-Ausbau #15](https://github.com/maxloibl/SantasLittleHelpers/issues/15) | Der Repository-Check läuft in GitHub Actions. Unity-Abnahme ist als lokaler Lauf dokumentiert. |

Für die finale Abnahme gilt der gemeinsame Lauf auf derselben Build-Version. [Testmatrix](../PRODUCT-TEST-MATRIX.md)
