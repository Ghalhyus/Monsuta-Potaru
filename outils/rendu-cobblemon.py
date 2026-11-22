# -*- coding: utf-8 -*-
"""Images du living dex All the Mons : rendu des modèles 3D du modpack (Bedrock, comme dans Minecraft).

Pour les espèces et formes propres au modpack, qui n'ont pas d'image 2D (Piglich, Creepyon, Stari Patrickyu, coupes de
Couafarel et motifs de Prismillon de Cobblemon). Lit les fichiers d'une instance All the Mons installée (mods du
dossier « mods », texture du Creeper dans le jar de Minecraft 1.21.1) et écrit des PNG de 256 × 256 dans
MonsutaPotaru/wwwroot/minecraft/pokemon/ (versionnés : le serveur n'a pas l'instance).

    python outils/rendu-cobblemon.py "C:/Users/<moi>/curseforge/minecraft/Instances/All the Mons - ATMons"

Sans dépendance (Python seul) : lecture et écriture des PNG avec zlib, rendu par tampon de profondeur. Vue de
trois quarts face, lumière simple par face. Pose de repos du modèle (pas d'animation).
"""
import json
import math
import os
import struct
import sys
import zipfile
import zlib

SORTIE = os.path.join(os.path.dirname(__file__), "..", "MonsutaPotaru", "wwwroot", "minecraft", "pokemon")
TAILLE = 256

# (fichier de sortie, jar, modèle, texture, calques) ; « minecraft: » : jar de Minecraft
COBBLEMON = "Cobblemon-neoforge-1.8.4+1.21.1.jar"
ATM = "allthemons-1.1.1.jar"
FURFROU = "assets/cobblemon/textures/pokemon/0676_furfrou/{0}/furfrou_{0}.png"
VIVILLON = "assets/cobblemon/textures/pokemon/0666_vivillon/"
IMAGES = [
    ("piglich.png", ATM, "assets/allthemons/bedrock/pokemon/models/99001_pigilch/piglich.geo.json",
     (ATM, "assets/allthemons/textures/pokemon/piglich.png"), [(ATM, "assets/allthemons/textures/pokemon/piglich_crown.png")]),
    ("creepyon.png", ATM, "assets/allthemons/bedrock/pokemon/models/99003_creepyon/creepyon.geo.json",
     ("minecraft:", "assets/minecraft/textures/entity/creeper/creeper.png"), []),
    ("120-patrickyu.png", ATM, "assets/cobblemon/bedrock/pokemon/models/0120_staryu/staryu_atm.geo.json",
     (ATM, "assets/allthemons/textures/pokemon/staryu_patrick.png"), []),
] + [
    (f"676-{coupe}.png", COBBLEMON, f"assets/cobblemon/bedrock/pokemon/models/0676_furfrou/furfrou_{coupe}.geo.json",
     (COBBLEMON, FURFROU.format(coupe)), [])
    for coupe in ("matcha", "lavender", "cinnamon", "crusader", "rocker", "mourner")
] + [
    (f"666-{motif}.png", COBBLEMON, "assets/cobblemon/bedrock/pokemon/models/0666_vivillon/vivillon.geo.json",
     (COBBLEMON, VIVILLON + "vivillon.png"), [(COBBLEMON, VIVILLON + f"vivillon_wings_{motif}.png")])
    for motif in ("inferno", "void", "forsaken")
]


# ---------- PNG ----------

def lire_png(data):
    """Pixels RGBA (liste de lignes de tuples) d'un PNG non entrelacé, 8 bits ou palette."""
    assert data[:8] == b"\x89PNG\r\n\x1a\n", "pas un PNG"
    pos, idat, palette, trns = 8, b"", None, None
    while pos < len(data):
        longueur, genre = struct.unpack(">I4s", data[pos:pos + 8])
        corps = data[pos + 8:pos + 8 + longueur]
        if genre == b"IHDR":
            largeur, hauteur, profondeur, couleur, _, _, entrelace = struct.unpack(">IIBBBBB", corps)
            assert entrelace == 0, "PNG entrelacé"
        elif genre == b"PLTE":
            palette = [tuple(corps[i:i + 3]) for i in range(0, len(corps), 3)]
        elif genre == b"tRNS":
            trns = corps
        elif genre == b"IDAT":
            idat += corps
        pos += 12 + longueur
    canaux = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[couleur]
    bits = canaux * profondeur
    pas = max(1, bits // 8)
    ligne = (largeur * bits + 7) // 8
    brut = zlib.decompress(idat)
    lignes, precedente, i = [], bytearray(ligne), 0
    for _ in range(hauteur):
        filtre, cur = brut[i], bytearray(brut[i + 1:i + 1 + ligne])
        i += 1 + ligne
        for x in range(ligne):
            a = cur[x - pas] if x >= pas else 0
            b = precedente[x]
            c = precedente[x - pas] if x >= pas else 0
            if filtre == 1:
                cur[x] = (cur[x] + a) & 255
            elif filtre == 2:
                cur[x] = (cur[x] + b) & 255
            elif filtre == 3:
                cur[x] = (cur[x] + (a + b) // 2) & 255
            elif filtre == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                cur[x] = (cur[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        lignes.append(bytes(cur))
        precedente = cur
    pixels = []
    for brute in lignes:
        rangee = []
        for x in range(largeur):
            if profondeur < 8:
                octet = brute[x * profondeur // 8]
                decalage = 8 - profondeur - (x * profondeur) % 8
                v = (octet >> decalage) & ((1 << profondeur) - 1)
                valeurs = [v]
            else:
                valeurs = list(brute[x * pas:(x + 1) * pas]) if profondeur == 8 else \
                    [brute[x * pas + k * 2] for k in range(canaux)]
            if couleur == 6:
                rangee.append(tuple(valeurs))
            elif couleur == 2:
                rangee.append((*valeurs, 255))
            elif couleur == 3:
                alpha = trns[valeurs[0]] if trns and valeurs[0] < len(trns) else 255
                rangee.append((*palette[valeurs[0]], alpha))
            elif couleur == 4:
                rangee.append((valeurs[0], valeurs[0], valeurs[0], valeurs[1]))
            else:
                rangee.append((valeurs[0], valeurs[0], valeurs[0], 255))
        pixels.append(rangee)
    return pixels


def ecrire_png(chemin, pixels):
    hauteur, largeur = len(pixels), len(pixels[0])
    brut = b"".join(b"\x00" + bytes(c for p in rangee for c in p) for rangee in pixels)

    def bloc(genre, corps):
        return struct.pack(">I", len(corps)) + genre + corps + struct.pack(">I", zlib.crc32(genre + corps) & 0xFFFFFFFF)

    with open(chemin, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + bloc(b"IHDR", struct.pack(">IIBBBBB", largeur, hauteur, 8, 6, 0, 0, 0))
                + bloc(b"IDAT", zlib.compress(brut, 9)) + bloc(b"IEND", b""))


# ---------- Géométrie ----------

def rot_x(a):
    c, s = math.cos(a), math.sin(a)
    return [[1, 0, 0], [0, c, -s], [0, s, c]]


def rot_y(a):
    c, s = math.cos(a), math.sin(a)
    return [[c, 0, s], [0, 1, 0], [-s, 0, c]]


def rot_z(a):
    c, s = math.cos(a), math.sin(a)
    return [[c, -s, 0], [s, c, 0], [0, 0, 1]]


def mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]


def app(m, v):
    return [sum(m[i][k] * v[k] for k in range(3)) for i in range(3)]


def rotation(r):
    """Rotation d'un os ou d'un cube Bedrock (degrés), comme Blockbench : x et y inversés, ordre ZYX."""
    rx, ry, rz = (math.radians(v) for v in (r or (0, 0, 0)))
    return mul(rot_z(rz), mul(rot_y(-ry), rot_x(-rx)))


def transforme(point, pivot, matrice):
    p = [point[i] - pivot[i] for i in range(3)]
    q = app(matrice, p)
    return [q[i] + pivot[i] for i in range(3)]


def faces_cube(cube, tw, th, miroir_os):
    """Quatre coins (en coordonnées Blockbench, x inversé) et rectangle de texture de chaque face d'un cube."""
    ox, oy, oz = cube["origin"]
    sx, sy, sz = cube["size"]
    gonfle = cube.get("inflate", 0)
    x0, x1 = -(ox + sx) - gonfle, -ox + gonfle
    y0, y1 = oy - gonfle, oy + sy + gonfle
    z0, z1 = oz - gonfle, oz + sz + gonfle
    uv = cube.get("uv", [0, 0])
    miroir = cube.get("mirror", miroir_os)
    faces = {}
    if isinstance(uv, dict):
        for nom, f in uv.items():
            u, v = f["uv"]
            du, dv = f.get("uv_size", [0, 0])
            faces[nom] = (u, v, u + du, v + dv)
    else:
        u, v = uv
        w, h, d = math.floor(sx), math.floor(sy), math.floor(sz)
        faces = {
            "east": (u, v + d, u + d, v + d + h), "north": (u + d, v + d, u + d + w, v + d + h),
            "west": (u + d + w, v + d, u + d + w + d, v + d + h), "south": (u + d + w + d, v + d, u + 2 * d + 2 * w, v + d + h),
            "up": (u + d + w, v + d, u + d, v), "down": (u + d + 2 * w, v, u + d + w, v + d),
        }
        if miroir:
            faces["east"], faces["west"] = faces["west"], faces["east"]
            faces = {k: (r[2], r[1], r[0], r[3]) for k, r in faces.items()}
    coins = {  # haut-gauche, haut-droit, bas-droit, bas-gauche, vus de l'extérieur
        "north": [(x1, y1, z0), (x0, y1, z0), (x0, y0, z0), (x1, y0, z0)],
        "south": [(x0, y1, z1), (x1, y1, z1), (x1, y0, z1), (x0, y0, z1)],
        "east": [(x0, y1, z0), (x0, y1, z1), (x0, y0, z1), (x0, y0, z0)],
        "west": [(x1, y1, z1), (x1, y1, z0), (x1, y0, z0), (x1, y0, z1)],
        "up": [(x1, y1, z1), (x0, y1, z1), (x0, y1, z0), (x1, y1, z0)],
        "down": [(x1, y0, z0), (x0, y0, z0), (x0, y0, z1), (x1, y0, z1)],
    }
    return [(coins[n], faces[n], n) for n in coins if n in faces]


def triangles(geo, tw, th):
    """Triangles du modèle (sommets, coordonnées de texture, face) dans la pose de repos."""
    os_par_nom = {b["name"]: b for b in geo["bones"]}

    def chaine(os_):
        while os_ is not None:
            yield os_
            os_ = os_par_nom.get(os_.get("parent"))

    sortie = []
    for os_ in geo["bones"]:
        for cube in os_.get("cubes", []):
            for coins, (u0, v0, u1, v1), nom in faces_cube(cube, tw, th, os_.get("mirror", False)):
                pts = [list(c) for c in coins]
                if "rotation" in cube:
                    pivot = cube.get("pivot", [0, 0, 0])
                    m = rotation(cube["rotation"])
                    pts = [transforme(p, [-pivot[0], pivot[1], pivot[2]], m) for p in pts]
                for o in chaine(os_):
                    if o.get("rotation"):
                        pivot = o.get("pivot", [0, 0, 0])
                        m = rotation(o["rotation"])
                        pts = [transforme(p, [-pivot[0], pivot[1], pivot[2]], m) for p in pts]
                uvs = [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]
                sortie.append(([pts[0], pts[1], pts[2]], [uvs[0], uvs[1], uvs[2]], nom))
                sortie.append(([pts[0], pts[2], pts[3]], [uvs[0], uvs[2], uvs[3]], nom))
    return sortie


def rendu(geo, textures):
    """Image RGBA du modèle, de trois quarts face, cadrée au centre."""
    tw, th = geo["description"]["texture_width"], geo["description"]["texture_height"]
    tris = triangles(geo, tw, th)
    vue = mul(rot_x(math.radians(-15)), rot_y(math.radians(225)))  # de face (vers +z), un peu de côté et d'en haut
    proj = [[app(vue, p) for p in pts] for pts, _, _ in tris]
    xs = [p[0] for t in proj for p in t]
    ys = [p[1] for t in proj for p in t]
    etendue = max(max(xs) - min(xs), max(ys) - min(ys)) or 1
    echelle = (TAILLE - 16) / etendue
    cx, cy = (max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2
    image = [[(0, 0, 0, 0)] * TAILLE for _ in range(TAILLE)]
    profondeur = [[-1e9] * TAILLE for _ in range(TAILLE)]
    lumiere = {"up": 1.0, "down": 0.55, "north": 0.8, "south": 0.8, "east": 0.65, "west": 0.65}
    for (pts, uvs, nom), p3 in zip(tris, proj):
        ecran = [((p[0] - cx) * echelle + TAILLE / 2, TAILLE / 2 - (p[1] - cy) * echelle, p[2]) for p in p3]
        (x0, y0, z0), (x1, y1, z1), (x2, y2, z2) = ecran
        aire = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
        if abs(aire) < 1e-9:
            continue
        for y in range(max(0, int(min(y0, y1, y2))), min(TAILLE, int(max(y0, y1, y2)) + 1)):
            for x in range(max(0, int(min(x0, x1, x2))), min(TAILLE, int(max(x0, x1, x2)) + 1)):
                px, py = x + 0.5, y + 0.5
                w0 = ((x1 - px) * (y2 - py) - (x2 - px) * (y1 - py)) / aire
                w1 = ((x2 - px) * (y0 - py) - (x0 - px) * (y2 - py)) / aire
                w2 = 1 - w0 - w1
                if w0 < 0 or w1 < 0 or w2 < 0:
                    continue
                z = w0 * z0 + w1 * z1 + w2 * z2
                if z <= profondeur[y][x]:
                    continue
                u = w0 * uvs[0][0] + w1 * uvs[1][0] + w2 * uvs[2][0]
                v = w0 * uvs[0][1] + w1 * uvs[1][1] + w2 * uvs[2][1]
                couleur = None
                for tex in textures:  # calques par-dessus la texture
                    th_, tw_ = len(tex), len(tex[0])
                    tx = min(tw_ - 1, max(0, int(u * tw_ / tw)))
                    ty = min(th_ - 1, max(0, int(v * th_ / th)))
                    c = tex[ty][tx]
                    if c[3] >= 128:
                        couleur = c
                if couleur is None:
                    continue
                k = lumiere[nom]
                image[y][x] = (int(couleur[0] * k), int(couleur[1] * k), int(couleur[2] * k), 255)
                profondeur[y][x] = z
    return image


def main():
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    instance = sys.argv[1]
    minecraft = os.path.join(instance, "..", "..", "Install", "versions", "1.21.1", "1.21.1.jar")
    os.makedirs(SORTIE, exist_ok=True)
    jars = {}

    def lire(jar, chemin):
        nom = minecraft if jar == "minecraft:" else os.path.join(instance, "mods", jar)
        if nom not in jars:
            jars[nom] = zipfile.ZipFile(nom)
        return jars[nom].read(chemin)

    for fichier, jar, modele, texture, calques in IMAGES:
        geo = json.loads(lire(jar, modele))["minecraft:geometry"][0]
        textures = [lire_png(lire(*texture))] + [lire_png(lire(*c)) for c in calques]
        ecrire_png(os.path.join(SORTIE, fichier), rendu(geo, textures))
        print(fichier)


if __name__ == "__main__":
    main()
