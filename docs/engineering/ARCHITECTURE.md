# Laufzeitarchitektur

## Einstieg und Szenen

Die [globale Szenenliste](../../ProjectSettings/EditorBuildSettings.asset) und der [explizite Windows-Build](../../Assets/Game/Delivery/Editor/WindowsPlayerBuild.cs) enthalten Home, CozyCabin, DeliveryHouse und MovementSandbox. Home ist der normale Einstieg. Die Legacy-Option öffnet DeliveryHouse direkt.

| Verantwortung | Einstieg |
| --- | --- |
| Slot-Auswahl, Settings, Join-Eingabe, Statusmeldungen | [HomeMenuController](../../Assets/Game/Home/HomeMenuController.cs) und [HomeMenuModel](../../Assets/Game/Home/HomeMenuModel.cs) |
| Szenenwechsel, Rückkehr und Speichern des Missionsabschlusses | [HomeLaunchBootstrap](../../Assets/Game/Bootstrap/HomeLaunchBootstrap.cs) |
| Sitzungslebenszyklus, Join-Fehler und Netzmanager | [SessionService](../../Assets/Game/Networking/Session/SessionService.cs) |
| Hüttenplätze, Bereitschaft und Join-Gate | [CabinPhaseController](../../Assets/Game/Cabin/Runtime/CabinPhaseController.cs) und [Prefab-Katalog](../../Assets/Game/Cabin/Runtime/CabinNetworkPrefabCatalog.cs) |
| Solo-Beginn und autoritative Runde | [CoopDeliveryEntry](../../Assets/Game/Delivery/CoopDeliveryEntry.cs) und [CoopNetworkRound](../../Assets/Game/Delivery/CoopNetworkRound.cs) |
| Bewegung, Kamera, Sichtbarkeit und Animation | [FirstPersonController](../../Assets/Game/Player/FirstPersonController.cs), [DeliveryPlayerOwnership](../../Assets/Game/Delivery/DeliveryPlayerOwnership.cs), [DeliveryCharacterVisual](../../Assets/Game/Delivery/DeliveryCharacterVisual.cs) |
| Gameplay-Prefabs und skalierte Visual-Wrapper | [CoopDeliverySetupBuilder](../../Assets/Game/Delivery/Editor/CoopDeliverySetupBuilder.cs) |

## Übergang in die Mission

1. Home validiert den Slot für Solo/Host beziehungsweise die IPv4 für Join. Jeder Prozess öffnet CozyCabin. Solo platziert seinen lokalen Spieler ohne NetworkManager.
2. Host und Gast starten den SessionService aus der Hütte. Der Katalog registriert die Netzwerk-Prefabs vor dem Sitzungsstart. Der SessionService besitzt den einzigen Manager für Hütte und Mission.
3. Die Hüttenautorität vergibt einen freien Anker und bestätigt die Platzierung. Der Host schließt beim Freigeben den Join-Eingang. NGO lädt DeliveryHouse synchron im Single-Modus. Nach Abschluss des Szenenwechsels startet der Host die autoritative Runde.
4. Nach Abschluss speichert der Bootstrap für Solo/Host über die aktive Slot- und Runden-ID. Der Sitzungsdienst räumt den Manager vor der Rückkehr auf. Ein fehlgeschlagener Commit kann während dieser Anwendungssitzung vom Startbildschirm aus wiederholt werden.

Die additive Hütte/Mission-Vorladung in [Produktverträge v1](../PRODUCT-CONTRACTS.md) war ein isoliert geprüfter Entwurf. Die Produktionsintegration verwendet seit [PR #71](https://github.com/maxloibl/SantasLittleHelpers/pull/71) den synchronen Single-Wechsel. Fehler- und Timeout-Erwartungen aus dem Entwurf bleiben bis zum jeweiligen Nachweis Anforderungen.

## Lokale Daten

- [SaveSlotStore](../../Assets/Game/Savegames/SaveSlotStore.cs) verwaltet unter `Application.persistentDataPath/save-slots` die Slot-Dateien `slot-<guid>.slotsave`. Das Schema und das Commit-Ledger liegen beim Store. Laufende Runden werden über eine Runden-ID identifiziert.
- [LocalSettingsStore](../../Assets/Game/Settings/LocalSettingsStore.cs) liest und schreibt `Application.persistentDataPath/settings.json`. Geräte-Einstellungen und Spielfortschritt haben eigene Lebenszyklen.
- `--product-smoke=<fall>` aktiviert beim Test die Steuerung des echten Produktflusses. `--profile=<absoluter Pfad>` isoliert dabei die Slot-Daten. Normale Spielstarts verwenden das lokale Standardprofil. Einstellungen haben einen eigenen Smoke-Pfad.

## Änderungen am Produktfluss

Prefab-GUIDs, Colliders, Greifpunkte, NetworkObjects und Kamera-Ownership bleiben beim Austausch von Visuals erhalten. Animator-Wrapper verwenden In-place-Clips. Die Figur des lokalen Besitzers bleibt aus der eigenen First-Person-Ansicht ausgeblendet. Szenen- und Prefab-Änderungen werden einem Issue exklusiv zugeteilt. [Art-Pipeline](../art/ASSET-PIPELINE.md) · [Entwicklungsablauf](WORKFLOW.md)
