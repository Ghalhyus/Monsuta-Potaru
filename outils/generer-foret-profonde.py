# Génère les cartes de la forêt profonde pour Tiled (Foret_profonde_1-1.tmx à Foret_profonde_1-4.tmx, une par taille
# achetée) avec le pack Cute Fantasy et son extension ShroomLands : un labyrinthe végétal de clairières reliées par des
# sentiers sinueux entre des murs d'arbres serrés, sans aucun point d'eau ; champignons, souches, rochers moussus, touffes
# d'herbe animées et quelques fleurs ; une pénombre verte (calque d'objets « Lumières » de parc.js) éclaircie par un puits de
# lumière au milieu de chaque clairière et par des champignons lumineux ; et le calque « Collisions » qui va avec. Tirages
# fixés : relancé sans changement, il redonne les mêmes cartes.
# Choix de l'utilisateur (2026-11-21) : des murs d'arbres serrés (chênes et sapins en quinconce, grands arbres, buissons et
# herbes hautes au pied), des clairières reliées par des sentiers avec des culs-de-sac et des boucles, assez de place pour
# se balader (14 cases libres par Pokémon), la pénombre et ses puits de lumière ; tous les Pokémon y vont.
# Ce qui n'est pas encore acheté est de la forêt : chaque clairière et chaque sentier appartient à une taille (PIECES), et
# n'est montré que tout entier.
# Ensuite, pour chacune : python outils/carte-parc.py <Foret_profonde_1-1.tmx>. Aperçus (foret-profonde-1.png,
# foret-profonde-1_collisions.png...) dans le dossier de sortie, ou dans le troisième. Moteur commun : parc_carte.py.
# Usage (Python 3 avec Pillow) :
#   python outils/generer-foret-profonde.py <dossier Cute_Fantasy du pack> <dossier de sortie> [dossier des aperçus]
import math
import random
import sys
from parc_carte import Carte, bordure, dans, nettoyer, relies, tirage

PACK, SORTIE = sys.argv[1], sys.argv[2]
L, H = 80, 60
# Zone de chaque taille (x, y, largeur, hauteur, en tuiles) : 20, 40, 60 puis 80 Pokémon (ParkService), comme le volcan
DEPART = (28, 20, 24, 20)
ZONES = [DEPART, (20, 15, 40, 30), (12, 10, 56, 40), (0, 0, L, H)]
# Ce qui s'ouvre à la carte entière reste à distance du bord : la bordure de forêt (règle de l'utilisateur, 2 à 3 cases à
# l'œil) ; en haut, une case de plus (les feuillages montent de deux cases au-dessus des troncs)
BORDURE = (5, 7, 5, 5)  # gauche, haut, droite, bas
carte = Carte(PACK, L, H, 53)
jeu, t, dedans = carte.jeu, carte.t, carte.dedans
SHROOM = "../Cute_Fantasy_ShroomLands/"

# --- Jeux de tuiles (incorporés à la carte) -------------------------------------------------
jeu("herbe", "Tiles/Grass/Grass_Tiles_1.png")
jeu("herbe_unie", "Tiles/Grass/Grass_1_Middle.png")
jeu("herbes_hautes", SHROOM + "Tiles/ShroomLands_Tall_Grass_Green_Tiles.png")
jeu("decors", "Outdoor decoration/Outdoor_Decor.png")
jeu("fleurs", "Outdoor decoration/Flowers.png")
jeu("chene", "Trees/Medium_Oak_Tree.png")
jeu("sapin", "Trees/Medium_Spruce_Tree.png")
jeu("grand_chene", "Trees/Big_Oak_Tree.png")
jeu("grand_sapin", "Trees/Big_Spruce_tree.png")
jeu("champi_rouge", SHROOM + "Props/Mushrooms_Red.png")
jeu("champi_brun", SHROOM + "Props/Mushrooms_Brown.png")
jeu("champi_bleu", SHROOM + "Props/Mushrooms_Blue.png")
jeu("rochers", SHROOM + "Props/Shroomlands_Rocks.png")
# Touffes d'herbe (8 images d'une tuile) : chaque tuile de la planche commence à une image différente (les touffes ne
# bougent pas toutes ensemble)
TOUFFES = ("touffe1", "touffe2", "touffe3")
for i, nom in enumerate(TOUFFES, 1):
    jeu(nom, f"Outdoor decoration/Outdoor_Decor_Animations/Grass_Animations/Grass_{i}_Anim.png")
    for k in range(8):
        carte.animer(nom, k, [((k + j) % 8, 150) for j in range(8)])

HERBE = "#3e8948"  # Grass_1_Middle : la couleur de fond, sous le bord transparent des herbes hautes
VOILE = ("#0a1f12", 0.5)  # la pénombre sous les arbres, verte
SOLEIL = "#fff1b0"  # puits de lumière des clairières
LUEUR = "#5fd3ff"  # champignons lumineux
# Herbes hautes (ShroomLands, le bord passe à l'intérieur des cases, dehors transparent) : coins et côtés HG H HD G D BG B
# BD, coins rentrants SE SO NE NO, milieux
HERBES_HAUTES = ((0, 1, 2, 5, 7, 10, 11, 12), (3, 4, 8, 9))
MILIEUX = (6, 6, 6, 21)


# --- Le plan : clairières et sentiers, chacun à sa taille ------------------------------------------
# Clairière : (taille, "c", centre x, centre y, rayon x, rayon y) ; sentier : (taille, "s", [points], largeur). Un sentier
# commence et finit dans ce qui est déjà ouvert (ou dans une clairière de sa taille).
PIECES = [
    # Vue de départ : deux clairières reliées par deux sentiers, autour d'un bosquet (une boucle)
    (1, "c", 35.5, 26.5, 6.5, 5.2),
    (1, "c", 44.5, 33.5, 6.4, 5),
    (1, "c", 47.5, 24, 2.6, 2.4),
    (1, "s", [(40, 24), (46, 23), (49, 27), (48, 30)], 3),
    (1, "s", [(32, 29), (32, 35), (37, 37), (41, 35)], 3),
    (1, "c", 31.5, 36, 2.5, 2),
    # Taille 2
    (2, "c", 25.5, 19.5, 4.5, 3.3),
    (2, "s", [(30, 25), (26, 25), (24, 21)], 3),
    (2, "s", [(23, 21), (22, 29), (25, 34), (24, 38)], 2),
    (2, "c", 25.5, 40, 3.5, 2.8),
    (2, "s", [(28, 41), (34, 42), (38, 41)], 2),  # cul-de-sac du sud (rejoint à la taille 3)
    (2, "c", 53, 19.5, 4.5, 3.2),
    (2, "s", [(47, 23), (50, 21)], 3),
    (2, "c", 55, 35, 3.4, 4.2),
    (2, "s", [(51, 34), (54, 34)], 3),
    (2, "s", [(57, 22), (57, 29), (56, 32)], 2),
    (2, "s", [(37, 22), (38, 19), (43, 18)], 2),  # cul-de-sac du nord
    (2, "c", 45, 18.5, 2.5, 1.8),
    # Taille 3
    (3, "c", 17, 28, 3.5, 5),
    (3, "s", [(22, 30), (19, 29)], 3),
    (3, "c", 17.5, 15.5, 3, 2.5),
    (3, "s", [(23, 18), (19, 16)], 2),
    (3, "c", 30, 46, 5, 2),
    (3, "s", [(31, 42), (30, 45)], 2),
    (3, "s", [(38, 41), (44, 45), (52, 46), (58, 45)], 3),
    (3, "c", 60, 45, 4, 2.5),
    (3, "s", [(56, 38), (59, 43)], 2),
    (3, "c", 62.5, 15, 3, 2.5),
    (3, "s", [(57, 19), (60, 15)], 2),
    (3, "c", 63, 29, 3, 4),
    (3, "s", [(58, 27), (61, 28)], 2),
    (3, "s", [(64, 33), (64, 40)], 2),  # cul-de-sac de l'est
    (3, "s", [(45, 17), (48, 12), (55, 12), (54, 16)], 2),
    (3, "c", 39, 13, 3.5, 2),
    (3, "s", [(38, 18), (39, 14)], 2),
    # Carte entière
    (4, "c", 10, 41, 3, 5),
    (4, "s", [(16, 32), (11, 37)], 2),
    (4, "c", 9.5, 16, 3, 3),
    (4, "s", [(15, 15), (11, 16)], 2),
    (4, "c", 28, 10, 5, 2),
    (4, "s", [(24, 17), (27, 11)], 2),
    (4, "c", 48, 9, 4, 1.8),
    (4, "s", [(50, 12), (49, 10)], 2),
    (4, "c", 70, 12, 3, 3),
    (4, "s", [(65, 15), (68, 13)], 2),
    (4, "c", 70.5, 30, 3, 6),
    (4, "s", [(66, 29), (69, 30)], 2),
    (4, "c", 69.5, 48.5, 3.5, 2.5),
    (4, "s", [(64, 46), (67, 48)], 2),
    (4, "c", 47, 51.5, 5, 2),
    (4, "s", [(48, 46), (48, 50)], 2),
    (4, "c", 15, 50, 4, 2.5),
    (4, "s", [(26, 47), (19, 50)], 2),
]


def clairiere(cx, cy, rx, ry, graine):
    """Cases d'une clairière : un ovale au bord irrégulier."""
    r = random.Random(graine)
    a, b, c = (r.random() * 6.3 for _ in range(3))
    cases = set()
    for y in range(int(cy - ry) - 3, int(cy + ry) + 4):
        for x in range(int(cx - rx) - 3, int(cx + rx) + 4):
            dx, dy = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
            angle = math.atan2(dy, dx)
            bord = 1 + 0.08 * math.sin(2 * angle + a) + 0.06 * math.sin(3 * angle + b) + 0.04 * math.sin(5 * angle + c)
            if dx * dx + dy * dy <= bord * bord:
                cases.add((x, y))
    return cases


def sentier(points, largeur):
    """Cases d'un sentier de <largeur> cases qui passe par les points (cases)."""
    r, decalage = largeur / 2, 0.5 if largeur % 2 else 0
    cases = set()
    for (x0, y0), (x1, y1) in zip(points, points[1:]):
        n = max(1, int(math.dist((x0, y0), (x1, y1)) * 4))
        for k in range(n + 1):
            px, py = x0 + (x1 - x0) * k / n + decalage, y0 + (y1 - y0) * k / n + decalage
            for y in range(int(py - r) - 1, int(py + r) + 2):
                for x in range(int(px - r) - 1, int(px + r) + 2):
                    if (x + 0.5 - px) ** 2 + (y + 0.5 - py) ** 2 <= r * r + 0.01:
                        cases.add((x, y))
    return cases


def hors_zone(taille_, cases):
    """Cases qui sortent de la zone de la taille, moins une case de forêt (à la carte entière : moins la bordure)."""
    zx, zy, zl, zh = ZONES[taille_ - 1]
    gauche, haut, droite, bas = BORDURE if taille_ == 4 else (1, 1, 1, 1)
    return [c for c in cases if not (zx + gauche <= c[0] < zx + zl - droite and zy + haut <= c[1] < zy + zh - bas)]


# « Il faut un peu plus de place » (2026-11-21) : chaque clairière grandit de 15 % au plus (autant que sa zone le permet),
# les sentiers ont 3 cases de large au moins
AGRANDIR, LARGEUR_MIN = 1.15, 3


def piece(i, p):
    if p[1] == "s":
        return sentier(p[2], max(p[3], LARGEUR_MIN))
    cx, cy, rx, ry = p[2:]
    for k in range(16):
        echelle = max(1, AGRANDIR - 0.01 * k)
        cases = clairiere(cx, cy, rx * echelle, ry * echelle, graine=i)
        if not hors_zone(p[0], cases) or echelle == 1:
            return cases
    return cases


PLAN = [piece(i, p) for i, p in enumerate(PIECES)]
for p, cases in zip(PIECES, PLAN):
    hors = hors_zone(p[0], cases)
    if hors:
        sys.exit(f"Pièce {p[:2]} {p[2]} : déborde de la zone de sa taille, {sorted(hors)[:6]}")
CLAIRIERES = set().union(*(c for p, c in zip(PIECES, PLAN) if p[1] == "c"))


def murs_minces(ouvert, zone):
    """Cases de forêt de la zone entre deux cases ouvertes (à gauche et à droite, ou en haut et en bas) : un mur d'une case
    ne tient ni arbre ni buisson lisible, il est ouvert."""
    return {(x, y) for x, y in set(ouvert_voisins(ouvert)) - ouvert if dans(zone, x, y)
            and (((x - 1, y) in ouvert and (x + 1, y) in ouvert) or ((x, y - 1) in ouvert and (x, y + 1) in ouvert))}


def ouvert_voisins(ouvert):
    return {(x + dx, y + dy) for x, y in ouvert for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))}


# « Il faut ajouter de l'espace en réduisant les gros morceaux de bordures » (2026-11-22) : un bloc de forêt plus épais
# que MUR_MAX cases (dans la zone, ou entre l'ouvert et le bord de la zone) recule, une rangée à la fois, jusqu'à ne plus
# l'être. Les murs du labyrinthe gardent 2 à MUR_MAX cases ; la bordure de la carte et la case de forêt au bord de chaque
# zone restent.
MUR_MAX = 8


def disque(r):
    return [(dx, dy) for dx in range(-6, 7) for dy in range(-6, 7) if dx * dx + dy * dy <= r * r]


# Épaisseur permise selon l'endroit (± 1 case, lentement) : le bord de ce qui recule n'est pas une ligne droite
_a, _b = random.Random(91).random() * 6.3, random.Random(92).random() * 6.3
RAYON = {(x, y): round(2 * (MUR_MAX / 2 + math.sin(x / 3.1 + _a) * math.cos(y / 2.7 + _b))) / 2
         for y in range(H) for x in range(L)}
DISQUES = {r: disque(r) for r in set(RAYON.values())}


def amincir(ouvert, n):
    """Ouvre, de proche en proche depuis l'ouvert, les cases de forêt de la zone de la taille n près d'un bloc trop épais :
    une case de la zone dont tout le disque (diamètre MUR_MAX ± 2, hors de la zone et de la carte compris) est en forêt."""
    zx, zy, zl, zh = ZONES[n - 1]
    permises = {(x, y) for y in range(zy, zy + zh) for x in range(zx, zx + zl) if not hors_zone(n, [(x, y)])}
    ouvert = set(ouvert)
    while True:
        epais = [c for c in permises - ouvert if all((c[0] + dx, c[1] + dy) not in ouvert for dx, dy in DISQUES[RAYON[c]])]
        pres = {(x + dx, y + dy) for x, y in epais for dx, dy in DISQUES[RAYON[(x, y)]]}
        ajout = {c for c in (ouvert_voisins(ouvert) & permises) - ouvert if c in pres}
        if not ajout:
            return ouvert
        ouvert |= ajout


# Ce qui est ouvert à chaque taille (clairières et sentiers de cette taille et des précédentes, la forêt trop épaisse
# amincie, sans mur d'une case)
OUVERT = {}
for n in range(1, 5):
    ouvert = set(OUVERT.get(n - 1, set())).union(*(c for p, c in zip(PIECES, PLAN) if p[0] == n))
    ouvert = amincir(ouvert, n)
    while True:
        minces = murs_minces(ouvert, ZONES[n - 1])
        if not minces:
            break
        ouvert |= minces
    ouvert = nettoyer(ouvert) | OUVERT.get(n - 1, set())
    if relies(ouvert) != ouvert:
        sys.exit(f"Taille {n} : clairières et sentiers pas tous reliés ({len(ouvert - relies(ouvert))} cases à part)")
    OUVERT[n] = ouvert
NOUVEAU = {n: OUVERT[n] - OUVERT.get(n - 1, set()) for n in range(1, 5)}
TOUT = {(x, y) for y in range(H) for x in range(L)}
DEHORS = {(x, y) for y in range(-1, H + 1) for x in range(-1, L + 1)} - TOUT  # la forêt continue hors de la carte


# --- Arbres -----------------------------------------------------------------------------------
ARBRES = ["sapin"] * 5 + ["chene"] * 4
# Grands chênes et grands sapins (4 × 5, tronc sur la ligne 3, colonnes 1 et 2) au milieu des bois : tout entiers dans la
# forêt de la carte entière, donc à toutes les tailles
FORET_ENTIERE = TOUT - OUVERT[4]
GRANDS = []
r = random.Random(7)
pris = set()
for _ in range(4000):
    x, y = r.randrange(-1, L - 3), r.randrange(-2, H - 4)
    cases = {(x + i, y + j) for i in range(4) for j in range(5)}
    proches = {(cx + dx, cy + dy) for cx, cy in cases for dx in (-1, 0, 1) for dy in (-1, 0, 1)}
    if all(c in FORET_ENTIERE or c not in TOUT for c in proches) and not cases & pris and any(c in TOUT for c in cases):
        GRANDS.append((r.choice(("grand_chene", "grand_sapin", "grand_sapin")), x, y))
        pris |= cases
    if len(GRANDS) >= 40:
        break
CASES_GRANDS = pris


def arbres(n):
    """Arbres de la taille n : [(tuiles, cases)]. Forêt en quinconce, un arbre dont les deux cases du tronc sont dans la
    forêt ; son feuillage peut déborder sur une clairière (on passe derrière), jamais sur un sentier."""
    ouvert = OUVERT[n]
    foret = lambda c: c not in ouvert
    liste = []
    for nom, x, y in GRANDS:
        liste.append(carte.bloc(nom, 4, 0, 4, 5, x, y, rang_devant=3, bloque_lignes=(3, 4), bloque_cols=(0, 1, 2, 3)))
    for y in range(-2, H, 2):
        for x in range(-1 + (y // 2) % 2, L, 2):
            tronc = [(x, y + 2), (x + 1, y + 2)]
            feuillage = [(x + i, y + j) for i in (0, 1) for j in (0, 1)]
            if not all(foret(c) for c in tronc) or any(c in CASES_GRANDS for c in tronc + feuillage):
                continue
            if any(not foret(c) and c not in CLAIRIERES for c in feuillage):
                continue
            liste.append(carte.bloc(tirage(x, y, 1).choice(ARBRES), 2, 0, 2, 3, x, y, rang_devant=2, bloque_lignes=(2,)))
    return liste


ARBRES_DE = {n: arbres(n) for n in range(1, 5)}
SOUS_ARBRES = {n: {(x, y) for tuiles in ARBRES_DE[n] for _, x, y, _, _ in tuiles} for n in range(1, 5)}


# --- Objets ----------------------------------------------------------------------------------
# Chaque objet : {"taille", "tuiles": [(calque, x, y, tuile, bloquée)], "lumiere"?}. Posés sur les cases ouvertes à leur taille
# (jamais sur une case libre d'une taille plus petite), hors des arbres de cette taille.
objets = []
occupees = set()


def poser(n, nom, gauche, haut_, largeur, hauteur, x, y, devant=0, bloque=(), marge=1, clairiere_=False, lumiere=None):
    """Pose un bloc de la planche sur les cases nouvelles de la taille n ; False si ça ne tient pas. Ce qui bloque a des cases
    ouvertes tout autour (jamais en travers d'un sentier) et seulement dans une clairière."""
    cases = [(x + i, y + j) for j in range(hauteur) for i in range(largeur)]
    if any(c not in NOUVEAU[n] or c in SOUS_ARBRES[n] for c in cases):
        return False
    bloquees = {(x + i, y + j) for j in bloque for i in range(largeur)}
    if (bloquees or clairiere_) and any(c not in CLAIRIERES for c in cases):
        return False
    if any((bx + dx, by + dy) not in OUVERT[n] or (bx + dx, by + dy) in occupees
           for bx, by in bloquees for dx in (-1, 0, 1) for dy in (-1, 0, 1) if (bx + dx, by + dy) not in bloquees):
        return False
    if any((cx + dx, cy + dy) in occupees for cx, cy in cases for dx in range(-marge, marge + 1) for dy in range(-marge, marge + 1)):
        return False
    cols = carte.colonnes(nom)
    tuiles = [("devant" if j < devant else "decors", x + i, y + j, t(nom, (haut_ + j) * cols + gauche + i), j in bloque)
              for j in range(hauteur) for i in range(largeur)]
    occupees.update(cases)
    objets.append({"taille": n, "tuiles": tuiles, **({"lumiere": lumiere} if lumiere else {})})
    return True


def tuile(n, nom, i, x, y, marge=0, **options):
    """Pose la tuile i de la planche (un objet d'une case)."""
    cols = carte.colonnes(nom)
    return poser(n, nom, i % cols, i // cols, 1, 1, x, y, marge=marge, **options)


def essayer(n, nombre, fonction, rng):
    cases = sorted(NOUVEAU[n])
    for _ in range(nombre * 60):
        if nombre <= 0:
            break
        if fonction(*rng.choice(cases)):
            nombre -= 1


CHAMPIGNONS = ("champi_rouge", "champi_brun")
PETITS_CHAMPIGNONS = (32, 33, 40, 41)  # une tuile chacun, dans les planches de ShroomLands
CHAMPIGNONS_DECOR = (15, 16, 17, 25, 26, 27, 28, 29, 36, 37, 38)  # Outdoor_Decor
SOUCHES = (55, 81, 83)  # Outdoor_Decor, une tuile
ROCHERS = ((1, 0), (3, 0), (1, 2), (3, 2), (7, 0), (7, 2))  # rochers moussus 2 × 2 de ShroomLands
FLEURS = [r_ * 10 + c_ for r_ in range(10) for c_ in range(5)]
LUMIERE_SOLEIL, LUMIERE_CHAMPIGNON, LUMIERE_PETIT = SOLEIL, (44, LUEUR), (30, LUEUR)
PORTEE = 8  # une case de sentier à plus de PORTEE cases de toute lumière reçoit un champignon lumineux


def soleils(n):
    """Puits de lumière des clairières de la taille n."""
    return [(round(p[2] * 16), round(p[3] * 16), round(16 * (p[4] + p[5]) / 2 * 1.05), SOLEIL)
            for p in PIECES if p[1] == "c" and p[0] == n]


for n in range(1, 5):
    rng = random.Random(300 + n)
    # Peu d'objets qui bloquent dans les tailles 1 et 2 : elles laissent juste 14 cases libres par Pokémon
    k = {1: 1, 2: 1, 3: 2, 4: 2}[n]
    # Grands champignons (2 × 3, le chapeau dans « Devant », le pied bloqué) et moyens (2 × 2), dans les clairières ; les
    # bleus luisent
    essayer(n, k, lambda x, y: poser(n, rng.choice(CHAMPIGNONS), rng.choice((0, 4)), 0, 2, 3, x, y, devant=2, bloque=(2,)), rng)
    essayer(n, k, lambda x, y: poser(n, "champi_bleu", 0, 0, 2, 3, x, y, devant=2, bloque=(2,),
                                     lumiere=(x * 16 + 16, y * 16 + 16, *LUMIERE_CHAMPIGNON)), rng)
    essayer(n, k, lambda x, y: poser(n, rng.choice(CHAMPIGNONS + ("champi_bleu",)), rng.choice((2, 6)), 1, 2, 2, x, y,
                                     devant=1, bloque=(1,)), rng)
    # Rochers moussus (2 × 2, bloqués), souches (bloquées)
    essayer(n, k, lambda x, y: poser(n, "rochers", *rng.choice(ROCHERS), 2, 2, x, y, bloque=(0, 1)), rng)
    essayer(n, k, lambda x, y: tuile(n, "decors", rng.choice(SOUCHES), x, y, bloque=(0,), marge=1), rng)
    essayer(n, k - 1, lambda x, y: poser(n, "grand_chene", 1, 3, 2, 1, x, y, bloque=(0,)), rng)  # souche du grand chêne
    # Petits champignons, touffes d'herbe animées (on y marche) ; quelques fleurs dans les clairières
    essayer(n, 6 * k, lambda x, y: tuile(n, rng.choice(CHAMPIGNONS), rng.choice(PETITS_CHAMPIGNONS), x, y), rng)
    essayer(n, 6 * k, lambda x, y: tuile(n, "decors", rng.choice(CHAMPIGNONS_DECOR), x, y), rng)
    essayer(n, 14 * k, lambda x, y: tuile(n, rng.choice(TOUFFES), rng.randrange(8), x, y), rng)
    essayer(n, 3 * k, lambda x, y: tuile(n, "fleurs", rng.choice(FLEURS), x, y, clairiere_=True), rng)
    # Champignons lumineux (une tuile, bleus) sur les sentiers loin de toute lumière
    lumieres = [(lx / 16, ly / 16) for m in range(1, n + 1) for lx, ly, _, _ in soleils(m)]
    lumieres += [(o["lumiere"][0] / 16, o["lumiere"][1] / 16) for o in objets if "lumiere" in o]
    reste = sorted(NOUVEAU[n] - SOUS_ARBRES[n])
    while reste:
        distance = lambda c: min((math.dist((c[0] + 0.5, c[1] + 0.5), lu) for lu in lumieres), default=99)
        loin = max(reste, key=lambda c: (distance(c), c))
        if distance(loin) <= PORTEE:
            break
        x, y = loin
        if tuile(n, "champi_bleu", tirage(x, y, 5).choice(PETITS_CHAMPIGNONS), x, y,
                 lumiere=(x * 16 + 8, y * 16 + 8, *LUMIERE_PETIT)):
            lumieres.append((x + 0.5, y + 0.5))
        else:
            reste.remove(loin)


# --- Carte de chaque taille --------------------------------------------------------------------
def sol_de(n):
    """Sol de la taille n : l'herbe (quelques tuiles marquées, les mêmes à chaque taille) ; sous la forêt, des herbes hautes
    (bord transparent sur la couleur de fond, l'herbe)."""
    sol_ = [[t("herbe", tirage(x, y, 3).choice((149, 150, 151))) if tirage(x, y, 4).random() < 0.1 else t("herbe_unie", 0)
             for x in range(L)] for y in range(H)]
    hautes = nettoyer((TOUT - OUVERT[n]) | DEHORS)
    coins, interieur = HERBES_HAUTES
    for x, y in hautes & TOUT:
        i = bordure(hautes, x, y, coins, interieur)
        sol_[y][x] = t("herbes_hautes", tirage(x, y, 6).choice(MILIEUX) if i is None else i)
    return sol_


def buisson(x, y):
    return t("decors", tirage(x, y, 2).choice((86, 87, 88)))


def taille(n, libres_avant):
    """Carte de la taille n : sol, décors, feuillages, cases bloquées, cases libres et lumières."""
    zone = ZONES[n - 1]
    ouvert = OUVERT[n]
    sol_ = sol_de(n)
    decors_ = [[0] * L for _ in range(H)]
    devant_ = [[0] * L for _ in range(H)]
    bloque_ = [[(x, y) not in ouvert for x in range(L)] for y in range(H)]
    grilles = {"decors": decors_, "devant": devant_}
    for tuiles in ARBRES_DE[n]:
        for calque, x, y, i, bloquee in tuiles:
            grilles[calque][y][x] = i
            bloque_[y][x] = bloque_[y][x] or bloquee
    # Sous-bois : une haie de buissons au bord de la forêt ; plus loin, entre les arbres, l'ombre des herbes hautes
    bord = {(x + dx, y + dy) for x, y in ouvert for dx in (-1, 0, 1) for dy in (-1, 0, 1)}
    for x, y in (TOUT - ouvert) & bord:
        if not decors_[y][x] and (not devant_[y][x] or tirage(x, y, 7).random() < 0.85):
            decors_[y][x] = buisson(x, y)
    lumieres = [lu for m in range(1, n + 1) for lu in soleils(m)]
    for objet in objets:
        if objet["taille"] > n:
            continue
        for calque, x, y, i, bloquee in objet["tuiles"]:
            grilles[calque][y][x] = i
            bloque_[y][x] = bloque_[y][x] or bloquee
        if "lumiere" in objet:
            lumieres.append(objet["lumiere"])
    # Poches enfermées (entre les objets) : des buissons, personne n'y va
    gardees = carte.boucher_poches(zone, decors_, bloque_, buisson)
    perdues = libres_avant - gardees
    if perdues:
        sys.exit(f"Taille {n} : cases libres de la taille d'avant bloquées : {sorted(perdues)[:10]}")
    return sol_, decors_, devant_, bloque_, gardees, lumieres


# --- Écriture des .tmx -----------------------------------------------------------------------
APERCUS = sys.argv[3] if len(sys.argv) > 3 else SORTIE
libres = set()
for n, zone in enumerate(ZONES, 1):
    sol_, decors_, devant_, bloque_, libres, lumieres = taille(n, libres)
    carte.ecrire_carte(SORTIE, f"Foret_profonde_1-{n}.tmx", DEPART, zone, sol_, decors_, devant_, bloque_, fond=HERBE,
                       lumieres=lumieres, voile=VOILE)
    carte.apercu(APERCUS, f"foret-profonde-{n}", (sol_, decors_, devant_), bloque_, fond=(0x3e, 0x89, 0x48))
    zx, zy, zl, zh = zone
    print(f"Foret_profonde_1-{n}.tmx : zone {zl} × {zh}, {len(libres)} cases libres ({len(libres) / (20 * n):.1f} par "
          f"Pokémon), {len(lumieres)} lumières, {sum(1 for o in objets if o['taille'] == n)} objets ajoutés")
