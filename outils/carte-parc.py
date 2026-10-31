"""
Carte du « Parc » : prend une carte faite dans Tiled avec le pack Cute Fantasy et la prépare pour le site.

- La carte (numéros de tuiles, calques, zone de départ) est écrite dans MonsutaPotaru/wwwroot/parc/<nom>.json, dans Git :
  elle ne contient aucun dessin du pack.
- Les planches du pack qu'elle utilise sont copiées dans MonsutaPotaru/wwwroot/images/parc/tuiles/, hors de Git comme les
  autres images : la licence du pack interdit de le redistribuer, même modifié. deploy\\fond-parc.cmd les envoie sur le
  serveur.

Ce que la carte doit avoir (README, « Parc ») : vue de dessus (orthogonale), tuiles de 16 px, taille fixe (pas
« infinie »), jeux de tuiles incorporés à la carte, calques de tuiles « Sol », « Décors », « Devant » (feuillages,
au-dessus de leur tronc) et « Collisions » (une tuile quelconque = case où l'on ne passe pas), et un calque d'objets « Zones » avec un
rectangle « Départ » (vue de départ : sa hauteur remplit l'écran) et un rectangle « Parc » qui le contient : la partie de la
carte où vivent les Pokémon (le reste est assombri). Sans « Parc », c'est « Départ ».

Types de cases (feuille « Habitat » de l'utilisateur, pour le bonheur des Pokémon) : déduits des planches (HABITAT_RULES)
et écrits dans « habitats » : une chaîne par ligne de la carte, un chiffre par case (0 aucun, 1 sable, 2 cave, 3 falaise,
4 végétation, 5 eau, 6 donjon, 7 volcan ; même ordre que Services/ParkHabitats.cs). Une case prend le type de sa tuile
de « Sol », que « Décors » remplace seulement par une planche de sol (les objets, arbres et fleurs gardent le type du
dessous) ; « Devant » ne compte pas. Une planche sans règle arrête le script : lui ajouter une ligne.

Lancement (Python 3 seul) :
  python outils/carte-parc.py <carte .tmx ou .tmj> [--nom parc-1-1] [--tiled <chemin de tiled.exe>]
Un .tmx est d'abord exporté en JSON par Tiled lui-même (en ligne de commande). Nom par défaut : celui du fichier en
minuscules, « _ » remplacés par « - » (Parc_1-2.tmx → parc-1-2.json : parc n° 1, taille 2).
"""
import json
import os
import re
import shutil
import subprocess
import sys

args = sys.argv[1:]
def option(name, default):
    if name in args:
        i = args.index(name)
        value = args[i + 1]
        del args[i:i + 2]
        return value
    return default
NAME = option("--nom", None)
TILED = option("--tiled", shutil.which("tiled") or r"C:\Program Files\Tiled\tiled.exe")
if len(args) != 1:
    sys.exit("Usage : python outils/carte-parc.py <carte .tmx ou .tmj> [--nom parc-1-1] [--tiled <chemin de tiled.exe>]")

SOURCE = os.path.abspath(args[0])
NAME = NAME or os.path.splitext(os.path.basename(SOURCE))[0].lower().replace("_", "-")
WWWROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "MonsutaPotaru", "wwwroot")
MAP_OUT = os.path.join(WWWROOT, "parc", NAME + ".json")
TILES_OUT = os.path.join(WWWROOT, "images", "parc", "tuiles")
LAYERS = ["Sol", "Décors", "Devant", "Collisions"]

# ---------- Types de cases : la première règle dont le motif se trouve dans le chemin de la planche (« / » partout) ----------
HABITATS = ["Aucun", "Sable", "Cave", "Falaise", "Végétation", "Eau", "Donjon", "Volcan"]
DECOR = None  # objet posé sur le sol : la case garde le type de la tuile du dessous
# Planches d'herbe à bords (16 × 10 tuiles) : herbe, sauf le chemin de terre (bas à gauche) et les flancs des hauteurs
GRASS_TILES = {**{i: "Aucun" for i in (80, 81, 82, 96, 97, 98, 112, 113, 114, 128, 129, 144, 145)},
               **{r * 16 + c: "Falaise" for r in (2, 3, 4, 6, 7, 8) for c in range(8, 16)},
               **{i: "Aucun" for i in (6, 7, 22, 23, 86, 87, 102, 103)}}  # escaliers des hauteurs
HABITAT_RULES = [
    (r"Cute_Fantasy/Tiles/Grass/Grass_Tiles_\d\.png$", ("Végétation", GRASS_TILES)),
    (r"Cute_Fantasy/Tiles/Grass/Path_", "Aucun"),
    (r"Cute_Fantasy/Tiles/Grass/", "Végétation"),
    (r"Cute_Fantasy/Tiles/Hedge_", "Végétation"),
    (r"Cute_Fantasy/Tiles/(Water|Waterfall)/", "Eau"),
    (r"Cute_Fantasy/Tiles/Cliff/", "Falaise"),
    (r"Cute_Fantasy/Tiles/Cave/Cave_Water", "Eau"),
    (r"Cute_Fantasy/Tiles/Cave/", "Cave"),
    (r"Cute_Fantasy/Tiles/Beach/", "Sable"),
    (r"Cute_Fantasy/Tiles/(Bridge/|Cobble_Road/|Pavement_|Wooden_Deck_)", "Aucun"),
    (r"Cute_Fantasy/Tiles/Picnic_", DECOR),
    (r"Cute_Fantasy/(Outdoor decoration|Trees|Buildings|Crops|Animals|Other)/", DECOR),
    (r"Cute_Fantasy_Desert/Tiles/Desert_(Water|Cliff_Waterfall)", "Eau"),
    (r"Cute_Fantasy_Desert/Tiles/Desert_Cliff", "Falaise"),
    (r"Cute_Fantasy_Desert/Tiles/Desert_Grass", "Végétation"),
    (r"Cute_Fantasy_Desert/Tiles/Desert_Bridge", "Aucun"),
    (r"Cute_Fantasy_Desert/Tiles/", "Sable"),
    (r"Cute_Fantasy_Desert/", DECOR),
    (r"Cute_Fantasy_Volcano/Tiles/Volcano_Bridge", "Aucun"),
    (r"Cute_Fantasy_Volcano/", "Volcan"),
    (r"Cute_Fantasy_Dungeons/Objects/", DECOR),
    (r"Cute_Fantasy_Dungeons/", "Donjon"),
    (r"Cute_Fantasy_ShroomLands/Tiles/.*Waterfall", "Eau"),
    (r"Cute_Fantasy_ShroomLands/Tiles/.*Cliff", "Falaise"),
    (r"Cute_Fantasy_ShroomLands/Tiles/", "Végétation"),
]
HABITAT_LAYERS = ["Sol", "Décors"]  # dans l'ordre : une tuile de sol de « Décors » remplace celle de « Sol »


def habitat_rule(path):
    """Règle de la planche (chemin complet) : type, (type, {tuile: type}), DECOR ; False si aucune règle."""
    path = path.replace("\\", "/")
    return next((rule for pattern, rule in HABITAT_RULES if re.search(pattern, path)), False)


# ---------- Lecture (un .tmx passe d'abord par l'export JSON de Tiled) ----------
if SOURCE.lower().endswith(".tmx"):
    if not os.path.exists(TILED):
        sys.exit(f"Tiled introuvable ({TILED}) : l'indiquer avec --tiled, ou exporter la carte en JSON depuis Tiled "
                 "(Fichier → Exporter sous… → .tmj).")
    # L'export s'écrit à côté de la carte : les chemins des images restent relatifs à son dossier
    exported = os.path.join(os.path.dirname(SOURCE), f".{NAME}-export.tmj")
    result = subprocess.run([TILED, "--embed-tilesets", "--export-map", "json", SOURCE, exported])
    if result.returncode != 0 or not os.path.exists(exported):
        sys.exit("Tiled n'a pas pu exporter la carte (voir les messages ci-dessus).")
    with open(exported, encoding="utf-8") as f:
        data = json.load(f)
    os.remove(exported)
else:
    with open(SOURCE, encoding="utf-8") as f:
        data = json.load(f)
base = os.path.dirname(SOURCE)

# ---------- Vérifications : un message clair plutôt qu'un parc qui ne s'affiche pas ----------
errors = []
if data.get("orientation") != "orthogonal":
    errors.append("la carte doit être orthogonale (vue de dessus)")
if data.get("tilewidth") != 16 or data.get("tileheight") != 16:
    errors.append("les tuiles doivent faire 16 × 16 pixels")
if data.get("infinite"):
    errors.append("la carte doit avoir une taille fixe (Carte → Propriétés de la carte : décocher « Infinie »)")
layers = {layer["name"]: layer for layer in data.get("layers", [])}
for name in LAYERS:
    if layers.get(name, {}).get("type") != "tilelayer":
        errors.append(f"calque de tuiles « {name} » absent")
for name, layer in layers.items():
    if layer.get("type") == "tilelayer" and layer.get("encoding", "csv") != "csv" and not isinstance(layer.get("data"), list):
        errors.append(f"calque « {name} » : format de calque de tuiles « CSV » attendu (pas de compression)")
zones = layers.get("Zones", {}).get("objects", [])
start = next((o for o in zones if o.get("name") == "Départ"), None)
if start is None:
    errors.append("rectangle « Départ » absent (calque d'objets « Zones »)")
park = next((o for o in zones if o.get("name") == "Parc"), start)
if start is not None and park is not None and not (
        park["x"] <= start["x"] and park["y"] <= start["y"] and start["x"] + start["width"] <= park["x"] + park["width"]
        and start["y"] + start["height"] <= park["y"] + park["height"]):
    errors.append("le rectangle « Parc » doit contenir « Départ »")
for tileset in data.get("tilesets", []):
    if "source" in tileset:
        errors.append(f"jeu de tuiles « {tileset['source']} » enregistré à part : l'incorporer à la carte "
                      "(bouton « Incorporer le jeu de tuiles » du panneau Jeux de Tuiles)")
    elif "image" not in tileset:
        errors.append(f"jeu de tuiles « {tileset.get('name')} » : une seule image par jeu (pas de « collection d'images »)")
if errors:
    sys.exit("Carte refusée :\n- " + "\n- ".join(errors))

# ---------- Planches : copiées sous leur nom de fichier (le même dans toutes les cartes) ----------
os.makedirs(TILES_OUT, exist_ok=True)
copied = {}
for tileset in data["tilesets"]:
    image = os.path.normpath(os.path.join(base, tileset["image"]))
    if not os.path.exists(image):
        sys.exit(f"Planche introuvable : {image}")
    file_name = os.path.basename(image)
    if copied.setdefault(file_name, image) != image:
        sys.exit(f"Deux planches différentes s'appellent « {file_name} » : renommer l'une des deux.")
    shutil.copyfile(image, os.path.join(TILES_OUT, file_name))
    tileset["image"] = file_name  # le parc la cherche dans images/parc/tuiles/
    tileset["_habitat"] = habitat_rule(image)

# ---------- Types de cases ----------
FLIP_BITS = 0x1FFFFFFF  # bits de retournement des tuiles de Tiled
tilesets = sorted(data["tilesets"], key=lambda t: t["firstgid"])
width, height = data["width"], data["height"]
cells = [0] * (width * height)
missing = set()
for name in HABITAT_LAYERS:
    for i, gid in enumerate(layers[name]["data"]):
        gid &= FLIP_BITS
        if not gid:
            continue
        tileset = next(t for t in reversed(tilesets) if t["firstgid"] <= gid)
        rule = tileset["_habitat"]
        if rule is False:
            missing.add(tileset["image"])
            continue
        if isinstance(rule, tuple):
            rule = rule[1].get(gid - tileset["firstgid"], rule[0])
        if rule is not DECOR:
            cells[i] = HABITATS.index(rule)
for tileset in data["tilesets"]:
    del tileset["_habitat"]
if missing:
    sys.exit("Planches sans type de case (ajouter une règle à HABITAT_RULES dans outils/carte-parc.py) : "
             + ", ".join(sorted(missing)))
data["habitats"] = ["".join(str(c) for c in cells[y * width:(y + 1) * width]) for y in range(height)]

# ---------- Carte : seulement ce que le parc lit, sur une ligne ----------
os.makedirs(os.path.dirname(MAP_OUT), exist_ok=True)
with open(MAP_OUT, "w", encoding="utf-8", newline="\n") as f:
    json.dump(data, f, ensure_ascii=False, separators=(",", ":"))
    f.write("\n")
print(f"Carte écrite : {os.path.normpath(MAP_OUT)} ({data['width']} × {data['height']} tuiles, départ "
      f"{int(start['width']) // 16} × {int(start['height']) // 16}, parc {int(park['width']) // 16} × {int(park['height']) // 16})")
counts = {HABITATS[h]: cells.count(h) for h in range(len(HABITATS)) if cells.count(h)}
print("Types de cases : " + ", ".join(f"{name} {n}" for name, n in counts.items()))
print(f"{len(copied)} planches copiées dans {os.path.normpath(TILES_OUT)} (à envoyer sur le serveur : deploy\\fond-parc.cmd)")
