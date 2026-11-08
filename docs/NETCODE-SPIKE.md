# Netzcode-Kandidatenprüfung für S1-C

Stand: 2026-11-08
Projekt-Editor: Unity 6000.6.8f1 (Revision 237b6593792b)
Issue: [#3](https://github.com/maxloibl/SantasLittleHelpers/issues/3)

## Entscheidung

**Konkreter Blocker für #7 / S2-C: Netcode for GameObjects (NGO) 2.7.0 ist ein passender Kandidat, aber für dieses Projekt nicht freigegeben.** Die Dokumentation ordnet NGO den GameObject/MonoBehaviour-Workflows zu; das passt zum First-Person-Controller und den Rigidbody-Geschenken des MVP. Unitys offizielle Paketübersicht für Unity 6000.0 führt 2.7.0 jedoch unter Pre-release. Das ist ein experimenteller Stand, kein Nachweis für Kompatibilität oder Laufzeitverhalten in 6000.6.8f1.

Zwei headless Editor-Läufe scheiterten vor dem Projektstart und vor Paketauflösung. Der erste meldete `[Licensing::IpcConnector] Connection to channel LicenseClient-MaxLo refused` und `Licensing is not yet initialized.` Im zweiten erreichte Unity den Licensing Client und löste eine Unity-Personal-Lizenz auf, meldete aber `Access token is unavailable; failed to update` und beendete sich mit Exit-Code 1. Ein erneuter Lauf mit `-nographics` endete ebenfalls mit Exit-Code 1 an derselben Stelle. Das eigene Log enthält danach weder Import- noch Paketauflösungs-Ausgaben. Damit konnte ich weder NGO auflösen noch einen Wegwerfprototyp in einer isolierten Szene ausführen. Es gibt folglich **keine Laufzeitprüfung** und keine Evidenz für Host/Client-Sichtbarkeit, hostautoritatives Rigidbody-Verhalten oder Client-Abbruch.

**Folgerung:** #7 / S2-C kann NGO 2.7.0 als Versuchskandidat übernehmen, wenn Editor/Lizenz-Initialisierung funktioniert. Vor Paketfreigabe muss ein eigener, isolierter Lauf die untenstehenden Kriterien erfüllen. Falls Pre-release-Pakete nicht akzeptiert werden, bleibt NGO 2.7.0 ein Blocker und #7 muss eine andere Option mit verifizierter Unity-6000.6-Kompatibilität festlegen. Unity Transport wird von NGO als darunterliegende Transportebene genutzt; die exakte transitive Version wurde nicht ermittelt, da die Paketauflösung nicht startete.

## Offizielle Paketinfos

- [Unity 6.0 Paketübersicht: Netcode for GameObjects](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.netcode.gameobjects.html): NGO ist ein High-Level-Netcode-SDK für GameObject/MonoBehaviour-Workflows. Die Tabelle listet `com.unity.netcode.gameobjects` 2.7.0 als Pre-release für Unity 6000.0.
- [Unity 6.0 Paketübersicht: Unity Transport](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.transport.html): Transport ist die Low-Level-Netzwerk- und Transportebene. Die Paketübersicht beschreibt die Unity-6.0-Verfügbarkeit.
- Diese Übersichten gelten ausdrücklich für 6000.0. Für 6000.6.8f1 ist die Kompatibilität hier nicht belegt.

## Versuch und Reproduzierbarkeit

Untersuchte Basis: Commit `5835246`, Branch `issue/3-netzcode-prototyp`. Der Projektstand nennt exakt `6000.6.8f1 (237b6593792b)`.

1. Projekt im angegebenen Editor öffnen: `C:\6000.6.8f1\Editor\Unity.exe -projectPath <Projektordner>`.
2. Wenn der Editor samt Lizenz initialisiert ist, in einer ausschließlich für den Versuch angelegten Szene NGO `2.7.0` über den Package Manager installieren. Manifest und Lockfile nur in diesem Wegwerfversuch ändern.
3. In dieser Szene einen `NetworkManager` mit Unity Transport, zwei Network-Player-Objekten mit `NetworkObject` und `NetworkTransform` sowie ein gemeinsames Rigidbody-Objekt mit `NetworkObject`, `NetworkRigidbody` und `NetworkTransform` anlegen. Das Rigidbody läuft serverseitig/hostautoritativ; den Host steuert es per einfacher Eingabe.
4. Host und Client in zwei lokalen Instanzen starten. Beide Spieler bewegen und prüfen, dass ihre Positionen auf beiden Instanzen sichtbar sind. Das Rigidbody durch den Host bewegen und die replizierte Position vergleichen.
5. Client normal schließen und protokollieren, ob sein Player-Objekt entfernt wird und was mit dem Rigidbody geschieht. Danach abrupten Client-Abbruch gesondert prüfen.

Schritte 2–5 wurden wegen des Editor-Abbruchs bei der Licensing-Initialisierung nicht ausgeführt. Die angegebene Komponentenliste und Testabfolge sind ein vorgeschlagener Reproduktionsplan, keine beobachteten Ergebnisse.

## Grenzen und offene Kriterien

- Kein isoliertes Unity-Testscene-Asset oder Prototyp wurde erstellt.
- Keine Pakete wurden in den finalen Projektdateien hinterlegt; NGO-Installationsversuch scheiterte vor Paketauflösung. Es gibt daher keine validierte vollständige Paketversionsliste.
- Zwei Instanzen, gegenseitige Positionssichtbarkeit, hostgesteuertes Rigidbody-Verhalten sowie normales und abruptes Trennen sind ungetestet.
- Keine Aussage zu LAN außerhalb des lokalen Rechners, Latenz, Paketverlust, Lobby/Matchmaking, Sicherheit, Ownership-Übergabe oder Builds.
- Für S2-C sollte ein Reviewer mit funktionierendem Unity-Lizenzclient den isolierten Versuch ausführen, Screenshots/Logs oder ein kurzes Protokoll sichern und Paketauflösung sowie Client-Bereinigung bestätigen, bevor das Paket in `Packages/` aufgenommen wird.
