════════════════════════════════════════════════════════════
 KKBlocks – farbige Bausteine für Kerbal Konstructs (KSP 1.12.x)
════════════════════════════════════════════════════════════

Fügt Kerbal Konstructs (KK) neue Statiks hinzu: 6 Formen
(Würfel, Rampe, Pyramide, Kugel, Zylinder, Kuppel) in 7 Größen
(0,5 / 1 / 2 / 4 / 8 / 16 / 32 m) mit 12 Farben, die im Spiel
jederzeit geändert werden können.

VORAUSSETZUNG
-------------
Kerbal Konstructs muss installiert sein. Dieser Ordner liegt
richtig unter:  GameData/KKBlocks/

BENUTZUNG
---------
1. Irgendwo im Flug (z. B. am KSC) den KK-Editor öffnen:
   KK-Symbol in der Toolbar  oder  STRG + K.
2. "Spawn New Static" – im Modellfilter die Kategorie
   "Bausteine" wählen und ein Objekt anklicken (z. B.
   "KK Rampe 2 m"), dann auf den Boden klicken, wo es
   stehen soll.
3. Das platzierte Objekt auswählen (TAB = Objekt unterm Mauszeiger
   greifen). Im Instance-Editor:
     • "Selected Variant" → Farbe ändern (Weiss, Rot, Blau, …)
     • "Model Scale"      → Größe ändern (1 = Originalgröße).
       Das Objekt wächst dabei vom Boden nach oben, denn der
       Ursprung liegt immer unten in der Mitte.
4. "Save" drücken – KK speichert die Platzierung nach
   GameData/KerbalKonstructs/NewInstances/.

Tipps:
• Mit "Increment" (z. B. 1) + WASD / Q,E / PageUp,PageDown lässt
  sich das Objekt exakt verschieben und stapeln.
• "Snap to Terrain" setzt das Objekt genau auf den Boden.
• Die Rampe dreht man am besten mit der Rotation im Instance-
  Editor so, dass die Schräge in die gewünschte Richtung zeigt.
• Unter Basis lohnt es sich, vorher mit einem KK-MapDecal das
  Gelände einebnen zu lassen.

DIE FORMEN
----------
• Baustein  – klassischer Würfel, ideal zum Stapeln.
• Rampe     – Würfel mit 45°-Schräge, befahrbar (Rover/Fahrwerke
              fahren hoch und runter). Die hohe Wand zeigt nach
              hinten (-Z), die Schräge nach vorne (+Z).
• Pyramide  – quadratische Basis, Spitze zeigt nach oben.
• Kugel     – rollt (Kugel-Collider), perfekt für Deko-Kugeln
              oder rolling-Torii.
• Zylinder  – Röhre mit exakt flachen Deckeln, gut als Silo/Turm.
• Kuppel    – Halbkugel, sitzt direkt auf dem Boden; mit der
              Basis nach unten, gut für Radome/Gewächshäuser.

Neu platzierte Objekte stehen mit "unten Mitte" am Boden –
zusammen mit "Snap to Terrain" und Model Scale lassen sich so
ohne Fummelei Türme und Rampen bauen.

TECHNIK-DETAIL COLLIDER
-----------------------
• Würfel: BoxCollider (wie bisher).
• Rampe, Pyramide, Zylinder, Kuppel: konvexer MeshCollider –
  genau der Typ, den auch hunderte Stock-Teile nutzen.
• Kugel: SphereCollider – ideal zum Rollen.
Alle Objekte sind also "solide": Fahrzeuge können drauf fahren,
Kerbale drauf laufen.

EIGENE GRÖSSEN
--------------
Ordner Generator öffnen und regenerate.bat starten. In der .bat
steht oben die Größenliste:
    set SIZES=0.5,1,2,4,8,16,32
Anpassen (Meter, kommasepariert), speichern, ausführen → es
werden ALLE Formen (Würfel + 5 neue) in der neuen Größe erzeugt.

Für eine neue Größe noch je Form einen STATIC-Block in
KKBlocks.cfg kopieren und anpassen, z. B. für 64 m:
    name = KKBlock64        /  mesh = KKBlock_64.mu
    name = KKBlockRamp64    /  mesh = KKBlockRamp_64.mu
    title = KK Baustein 64 m  usw.

EIGENE FARBEN
-------------
In KKBlocks.cfg einen VARIANT-Block kopieren und anpassen:

    VARIANT
    {
        name = Pink
        TextureSet
        {
            transforms = Any
            color = 0.95,0.30,0.60,1
        }
    }

color = R,G,B,A mit Werten von 0 bis 1 (0.5 = 50 % Helligkeit).
Danach KSP neu starten.

TECHNISCHES
-----------
• Die .mu-Dateien sind programmatisch erzeugt (Generator/MuTool.cs,
  wird von regenerate.bat mit dem .NET-csc.exe kompiliert;
  Format-Referenz: taniwha/io_object_mu).
• Alle Meshes: 1 Submesh + 1 Material (KSP/Diffuse mit _Color-
  Property) – dadurch greift der Variant-Farbwechsel von KK
  (renderer.material.color). Faces zeigen mit der im .mu-Format
  üblichen Windung nach außen (cross(v1-v0, v2-v0) = Normale).
• Material-Properties entsprechen exakt Squad-Stock-Werten
  (_TemperatureColor = 0,0,0,0), damit Rendering-Mods wie
  Deferred das Objekt nicht orange einfärben.
• Winding + Collider aller erzeugten Dateien werden beim
  Generieren automatisch geprüft ("MuTool.exe check").
• Getestet gegen KK 1.12.3 (Fork KSP-RO) unter KSP 1.12.x.

ABHÄNGIGKEITEN
--------------
Benötigt werden nur:
• Kerbal Space Program 1.12.0 – 1.12.5 (getestet auf 1.12.5)
• Kerbal Konstructs 1.12.3 oder neuer (liefert Statiks und den Editor)

KKBlocks bringt keinen Plugin-Code mit und braucht kein ModuleManager,
keine Textur-Packs und keine anderen Mods. Scatterer, EVE, TUFX oder
Deferred sind optional und ändern nichts an den Bausteinen. Die Mod ist
save-kompatibel und kann jederzeit installiert oder entfernt werden;
platzierte Instanzen bleiben in den KK-Saves erhalten.

Hinweis: Die praktische Obergrenze setzt Kerbal Konstructs selbst – nutze
eine KK-Version, die deine KSP-Patchversion unterstützt. Mit KK 1.12.3+
bist du auf allen KSP 1.12.0–1.12.5-Installationen abgedeckt.

HILFE, UPDATES UND DISCORD
--------------------------
Community, Fragen und Release-News: https://discord.gg/GEjdeqJE6g
(Lynx Development)

LIZENZ
------
MIT – siehe LICENSE. Verwendung, Änderung und Weitergabe (auch in
Mod-Packs) sind erlaubt, solange Copyright- und Lizenz-Hinweis erhalten
bleiben.
