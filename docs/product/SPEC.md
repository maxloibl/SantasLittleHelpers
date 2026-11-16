# Produktspezifikation

Santa’s Little Helpers ist ein kooperatives First-Person-Weihnachtsspiel für eine bis vier Personen. Die Crew startet in einer eingerichteten Winterhütte, liefert Geschenke im DeliveryHouse und kehrt zum Schlitten zurück. Der Bewohner reagiert auf Sicht und Geräusche. Gefangene Helfer verlieren die Bewegung und können von freien Mitspielern befreit werden.

## Spielablauf

| Phase | Solo | Host und Gäste |
| --- | --- | --- |
| Home | Spielstand anlegen, auswählen, laden, umbenennen oder löschen. Solo starten. | Host lädt seinen Slot und öffnet die Hütte. Gäste geben direkte IPv4-Adresse und UDP-Port ein. |
| CozyCabin | Der eigene Spieler erscheint am ersten Hüttenanker und startet die Mission über das Hüttenmenü. | Host und Gäste erhalten unterschiedliche Hüttenplätze. Der Host startet mit mindestens einem bereitstehenden Gast, sobald die verbundenen Spieler platziert sind. Plätze werden beim Verlassen wieder frei. |
| DeliveryHouse | Geschenk aufnehmen, zur Lieferzone bringen und zum Schlitten zurückkehren. | Der Host steuert Rundenregeln und Abschluss. Leichte Geschenke können einzeln getragen werden. Schwere Geschenke unterstützen zwei gleichzeitige Griffe. Gäste können im laufenden Spiel befreien und mittragen. |
| Rückkehr | Erfolgreiche Runde wird im gewählten Slot gespeichert. Verlassen während der Runde bringt keinen Fortschritt. | Nur der Host speichert den Abschluss. Gäste kehren nach Abschluss oder Verbindungsverlust zu Home zurück. Joins werden nach Freigabe der Mission abgewiesen. |

## Bedienung und Einstellungen

- W/A/S/D bewegt, die Maus steuert den Blick. Shift sprintet, Strg oder C duckt, Leertaste springt.
- E nimmt ein Geschenk auf, legt es ab oder befreit einen nahen Mitspieler. R startet eine beendete Runde erneut, wo das jeweilige Rundenmenü diese Aktion anbietet.
- Esc gibt die Maus frei. Im Hüttenmenü gibt es separate Aktionen für den Missionsstart und die Rückkehr zum Startbildschirm. Der Status der Eingabekorrektur steht im [Sprintstand](../delivery/STATUS.md).
- Einstellungen für Lautstärke, Mausempfindlichkeit und Fenstermodus lassen sich lokal vorab anwenden, übernehmen, verwerfen oder auf Standardwerte setzen.

## Produktgrenzen

- Windows x64 mit Unity 6 und URP ist das gebaute Ziel. Multiplayer nutzt Netcode for GameObjects und direkten IPv4/UDP-Beitritt. Standardport ist 7777.
- Die Sitzung umfasst maximal vier Personen einschließlich Host. Öffentliche Spielsuche, Relay, IPv6, NAT-Traversal, Cloud-Saves und Hostmigration liegen außerhalb des aktuellen Produktumfangs.
- Slot-Dateien gehören dem Solo-Spieler beziehungsweise dem Host. Der Abschluss einer Runden-ID darf den Fortschritt nur einmal erhöhen. Ein Gastbeitritt benötigt keinen Slot.
- Die Modelle verwenden ausgewählte Asset-Packs und spielgerechte Stilvarianten. Das DeliveryHouse enthält weiterhin Blockout-Umgebung. Figur-, Geschenk-, Ziel- und Schlittenmodelle sind integriert. [Art-Pipeline](../art/ASSET-PIPELINE.md)

Technische Details und Fehlercodes stehen in [Architektur](../engineering/ARCHITECTURE.md) und [Produktverträge v1](../PRODUCT-CONTRACTS.md).
