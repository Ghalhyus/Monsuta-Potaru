# Génère les cartes du donjon abandonné pour Tiled (Donjon_1-1.tmx à Donjon_1-4.tmx, une par taille achetée) avec
# l'extension Dungeons du pack Cute Fantasy : des salles et des couloirs de pierre creusés dans la roche, des bassins d'égout
# et leurs ponts de bois, des piliers brisés, des tombes, des pots, des caisses, des toiles, des os et de l'or oublié, des
# torches et des candélabres (calque d'objets « Lumières » : la pénombre de parc.js), et le calque « Collisions » qui va
# avec. Tirages fixés : relancé sans changement, il redonne les mêmes cartes.
# Choix de l'utilisateur (2026-11-07) : un souterrain délaissé, des salles reliées par des couloirs (chaque agrandissement
# ouvre de nouvelles salles), des canaux d'égout avec de petits ponts, la pénombre et des torches ; tous les Pokémon y vont.
# Planches (Dungeon_1, aucun exemple avec le pack ; lues le 2026-11-07) : la roche vue de dessus n'a pas de tuile (case
# transparente sur la couleur de fond de la carte, FOND, comme la grotte) ; autour de chaque salle, le dessus du mur (pavés
# sombres, ANNEAU) dans les cases de roche qui la touchent ; sous le bord du haut, la face du mur (FACE, deux rangées) puis
# l'ombre sur le dallage, toutes bloquées.
# Ce qui n'est pas encore acheté est de la roche : chaque salle et chaque couloir appartient à une taille (PIECES), et n'est
# montré que tout entier.
# Ensuite, pour chacune : python outils/carte-parc.py <Donjon_1-1.tmx>. Aperçus (donjon-1.png, donjon-1_collisions.png...)
# dans le dossier de sortie, ou dans le troisième. Moteur commun avec les autres parcs : parc_carte.py.
# Usage (Python 3 avec Pillow) :
#   python outils/generer-donjon.py <dossier Cute_Fantasy du pack> <dossier de sortie> [dossier des aperçus]
import math
import random
import sys
from parc_carte import Carte, dans, relies, tirage

PACK, SORTIE = sys.argv[1], sys.argv[2]
L, H = 80, 60
# Zone de chaque taille (x, y, largeur, hauteur, en tuiles) : 20, 40, 60 puis 80 Pokémon (ParkService), comme le volcan
DEPART = (28, 20, 24, 20)
ZONES = [DEPART, (20, 15, 40, 30), (12, 10, 56, 40), (0, 0, L, H)]
carte = Carte(PACK, L, H, 37)
jeu, t, dedans = carte.jeu, carte.t, carte.dedans
TOUT = {(x, y) for y in range(H) for x in range(L)}

# --- Jeux de tuiles (incorporés à la carte) -------------------------------------------------
DONJON = "../Cute_Fantasy_Dungeons/"
jeu("donjon", DONJON + "Dungeon_1/Dungeon_1.png")
jeu("egout", DONJON + "Dungeon_1/Dungeon_1_Sewer_Tileset.png")
jeu("piliers", DONJON + "Dungeon_1/Dungeon_1_Pillars.png")
jeu("pics", DONJON + "Dungeon_1/Floor_spikes_1.png")
jeu("objets", DONJON + "Objects/Dungeon_Objects.png")
jeu("or", DONJON + "Objects/Gold_Piles.png")
jeu("coffre", DONJON + "Objects/Chest_anim.png")
jeu("pont", DONJON + "Objects/Small_Wooden_Bridge.png")
jeu("bouche", DONJON + "Objects/Sewer.png")
jeu("torche", "Outdoor decoration/Big_Torch_Anim.png")
# Torche : la flamme (première ligne, 8 images) est animée, le manche et le pied ne bougent pas ; pics du sol (deuxième
# ligne, sans dallage dessous) : ils sortent et rentrent
carte.animer("torche", 0, [(i, 110) for i in range(8)])
carte.animer("pics", 8, [(8, 1400)] + [(8 + i, 90) for i in range(1, 8)] + [(15, 900)] + [(8 + i, 90) for i in range(6, 0, -1)])

FOND = "#16141f"  # la roche vue de dessus (le noir des joints des pavés)
ROCHE = 3  # tuile transparente de Dungeon_1 : la roche (couleur de fond), de type donjon pour carte-parc.py
# Dessus du mur dans la case de roche qui touche la salle : la salle au sud (bord du haut), au nord, à l'est, à l'ouest,
# puis seulement en diagonale (coins de la salle)
ANNEAU = {"s": 5, "n": 31, "e": 19, "o": 17, "se": 6, "so": 4, "ne": 32, "no": 30}
FACE = (82, 108)  # face du mur sous le bord du haut : deux rangées (+ 0 bout gauche, + 1 milieu, + 2 bout droit)
OMBRE = 62  # dallage sous la face du mur, assombri en haut (+ 0, 1, 2 comme la face)
DALLAGE = 88  # dallage 3 × 3 (88-90, 101-103, 114-116), répété


# --- Le plan : salles et couloirs, chacun à sa taille -------------------------------------------
# (taille, x0, y0, x1, y1) compris. Deux pièces qui ne se touchent pas ont au moins deux cases de roche entre elles (le
# dessus du mur de chacune) ; un couloir touche les deux salles qu'il relie. Un couloir horizontal ou une galerie a cinq
# rangées (deux de face de mur et l'ombre à son pied, bloquées, puis deux où l'on marche).
PIECES = [
    # Vue de départ : la grande salle
    (1, 29, 21, 50, 38),
    # Taille 2 : deux longues salles à l'ouest et à l'est, et leurs couloirs vers la grande salle
    (2, 21, 17, 26, 42), (2, 27, 26, 28, 30),
    (2, 53, 17, 58, 42), (2, 51, 25, 52, 29),
    # Taille 3 : quatre salles aux coins (deux cryptes au nord, deux salles d'égout au sud), deux galeries au nord et au sud
    # (un plan sans symétrie a été essayé le 2026-11-07, puis refusé : « j'aime moins »)
    (3, 12, 11, 18, 24), (3, 19, 17, 20, 21),
    (3, 12, 29, 18, 48), (3, 19, 35, 20, 39),
    (3, 61, 11, 67, 24), (3, 59, 17, 60, 21),
    (3, 61, 29, 67, 48), (3, 59, 35, 60, 39),
    (3, 22, 10, 57, 14), (3, 23, 15, 25, 16), (3, 54, 15, 56, 16),
    (3, 22, 45, 57, 49), (3, 23, 43, 25, 44), (3, 54, 43, 56, 44),
    # Carte entière : les grandes salles des bords, qui se rejoignent dans les coins (une galerie tout autour : les coins
    # pleins de roche perdaient de la place, 2026-11-07)
    (4, 4, 8, 9, 51), (4, 10, 17, 11, 21), (4, 10, 35, 11, 39),
    (4, 70, 8, 75, 51), (4, 68, 17, 69, 21), (4, 68, 35, 69, 39),
    (4, 4, 3, 75, 7), (4, 38, 8, 40, 9),
    (4, 4, 52, 75, 56), (4, 38, 50, 40, 51),
]
# Bassins d'égout (taille, x0, y0, x1, y1 de l'eau) : un rebord de pierre d'une case autour, où l'on marche ; un pont de
# bois (5 cases) à la ligne <pont>
BASSINS = [(3, 14, 34, 16, 44, 39), (3, 63, 34, 65, 44, 39), (1, 35, 30, 44, 31, None)]


def rectangle(x0, y0, x1, y1):
    return {(x, y) for x in range(x0, x1 + 1) for y in range(y0, y1 + 1)}


PLAN = [rectangle(*p[1:]) for p in PIECES]
for i, a in enumerate(PLAN):
    for j, b in enumerate(PLAN[:i]):
        touche = any((x + dx, y + dy) in b for x, y in a for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
        proche = any((x + dx, y + dy) in b for x, y in a for dx in range(-2, 3) for dy in range(-2, 3))
        if a & b or (proche and not touche):
            sys.exit(f"Pièces {PIECES[i]} et {PIECES[j]} : se chevauchent, ou trop proches sans se toucher")


def forme_de(n):
    """Salles et couloirs montrés à la taille n."""
    return set().union(*(c for p, c in zip(PIECES, PLAN) if p[0] <= n))


# --- Murs : dessus (dans la roche) et face (sous le bord du haut) -------------------------------
COTES = (("s", (0, 1)), ("n", (0, -1)), ("e", (1, 0)), ("o", (-1, 0)), ("se", (1, 1)), ("so", (-1, 1)), ("ne", (1, -1)),
         ("no", (-1, -1)))


def dessus_des_murs(forme):
    """Dessus du mur de chaque case de roche voisine de la forme {case : tuile}."""
    dessus = {}
    for x, y in TOUT - forme:
        cote = next((c for c, (dx, dy) in COTES if (x + dx, y + dy) in forme), None)
        if cote:
            dessus[(x, y)] = ANNEAU[cote]
    return dessus


def colonne_de_face(haut, x, y):
    """Colonne de la planche pour la face du mur : 0 au bout gauche, 2 au bout droit, 1 entre les deux."""
    if (x - 1, y) not in haut:
        return 0
    return 2 if (x + 1, y) not in haut else 1


def murs(forme):
    """Dessus du mur de chaque case de roche voisine de la forme {case : tuile} ; face et ombre {case : tuile} ; les cases
    de face (bloquées)."""
    dessus = dessus_des_murs(forme)
    haut = {(x, y) for x, y in forme if (x, y - 1) not in forme}
    face, faces = {}, set()
    for x, y in haut:
        col = colonne_de_face(haut, x, y)
        for j, ligne in enumerate(FACE):
            if (x, y + j) in forme:
                face[(x, y + j)] = ligne + col
                faces.add((x, y + j))
        # L'ombre au pied du mur : à l'œil, le bas du mur (bloquée aussi : les Pokémon y marchaient, 2026-11-07)
        if (x, y + len(FACE)) in forme and (x, y + len(FACE)) not in faces:
            face.setdefault((x, y + len(FACE)), OMBRE + col)
            faces.add((x, y + len(FACE)))
    return dessus, face, faces


FORME_ENTIERE = forme_de(4)
_, _, FACES_ENTIERES = murs(FORME_ENTIERE)
# Le dallage où l'on pose les objets : hors des faces de mur à toutes les tailles (une face peut disparaître quand un
# couloir s'ouvre au-dessus, jamais apparaître sur du dallage)
FACES_TOUJOURS = set().union(*(murs(forme_de(n))[2] for n in range(1, 5)))


# --- Bassins ---------------------------------------------------------------------------------
def bassin(x0, y0, x1, y1):
    """Tuiles du bassin d'eau x0..x1 × y0..y1 et de son rebord (une case autour) : {case : tuile d'Egout}, l'eau."""
    tuiles = {}
    for x in range(x0 - 1, x1 + 2):
        for y in range(y0 - 1, y1 + 2):
            tuiles[(x, y)] = rang_du_bassin(y, y0, y1) * 13 + 2 + rang_du_bassin(x, x0, x1)
    return tuiles, rectangle(x0, y0, x1, y1)


def rang_du_bassin(v, v0, v1):
    """Colonne (ou ligne) de la planche d'Egout : 0 le rebord avant, 1 le premier rang d'eau, 2 le milieu, 3 le dernier
    rang, 4 le rebord après."""
    if v == v0 - 1:
        return 0
    if v == v1 + 1:
        return 4
    if v == v0:
        return 1
    return 3 if v == v1 else 2


bassins = []
for taille_, x0, y0, x1, y1, ligne_pont in BASSINS:
    tuiles, eau = bassin(x0, y0, x1, y1)
    if not set(tuiles) <= forme_de(taille_) - FACES_TOUJOURS:
        sys.exit(f"Bassin {(x0, y0)} : pas tout entier sur le dallage de sa salle")
    pont = {(x0 - 1 + i, ligne_pont): i for i in range(5)} if ligne_pont is not None else {}
    bassins.append({"taille": taille_, "tuiles": tuiles, "eau": eau, "pont": pont})
EAU = set().union(*(b["eau"] for b in bassins))
REBORDS = set().union(*(set(b["tuiles"]) for b in bassins))


# --- Objets ----------------------------------------------------------------------------------
# Chaque objet : {"taille", "tuiles": [(calque, x, y, tuile, bloquée)], "lumiere"?}. Posés sur le dallage d'une pièce de
# leur taille (ses cases étaient de la roche avant : un objet ne prend jamais une case libre d'une taille plus petite).
objets = []
occupees = set()  # cases prises par un objet (et leurs voisines pour ceux qui veulent de la place)


def piece_de(x, y):
    return next((p for p, c in zip(PIECES, PLAN) if (x, y) in c), None)


DALLE = FORME_ENTIERE - FACES_TOUJOURS - REBORDS  # dallage libre où poser un objet


def poser(taille_, nom, gauche, haut_, largeur, hauteur, x, y, devant=0, bloque=(), marge=1, lumiere=None):
    """Pose un bloc de la planche si ses lignes du bas (hors « Devant ») sont sur le dallage d'une pièce de cette taille,
    loin des autres objets ; False sinon. Ce qui bloque a du dallage tout autour : jamais dans une galerie de deux rangées où
    l'on marche (un objet y coupait le passage, et la suite devenait une poche de gravats)."""
    cases = [(x + i, y + j) for j in range(devant, hauteur) for i in range(largeur)]
    if not all(c in DALLE and piece_de(*c)[0] == taille_ for c in cases):
        return False
    bloquees = {(x + i, y + j) for j in bloque for i in range(largeur)}
    if any((bx + dx, by + dy) not in DALLE and (bx + dx, by + dy) not in bloquees
           for bx, by in bloquees for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
        return False
    if any((cx + dx, cy + dy) in occupees for cx, cy in cases for dx in range(-marge, marge + 1) for dy in range(-marge, marge + 1)):
        return False
    cols = carte.colonnes(nom)
    tuiles = [("devant" if j < devant else "decors", x + i, y + j, t(nom, (haut_ + j) * cols + gauche + i), j in bloque)
              for j in range(hauteur) for i in range(largeur)]
    occupees.update((cx, cy) for _, cx, cy, _, _ in tuiles)
    objets.append({"taille": taille_, "tuiles": tuiles, **({"lumiere": lumiere} if lumiere else {})})
    return True


def essayer(taille_, n, fonction, rng):
    cases = sorted(c for c in DALLE if piece_de(*c)[0] == taille_)
    for _ in range(n * 40):
        if n <= 0:
            break
        if fonction(*rng.choice(cases)):
            n -= 1


LUMIERE_TORCHE, LUMIERE_BOUGIES = (88, "#ffb35c"), (60, "#ffd27a")
PORTEE = 7  # aucune case de dallage à plus de PORTEE cases d'une lumière (candélabres)


def poser_torches(taille_, bords):
    """Torches contre le mur du haut (flamme et manche sur la face, le pied sur l'ombre), une toutes les huit cases
    environ, sur les bords du haut apparus à cette taille."""
    for x, y in bords:
        if x % 8 == 3 and all((x + dx, y + dy) not in occupees for dx in (-1, 0, 1) for dy in (0, 1, 2)):
            tuiles = [("decors", x, y + j, t("torche", j * 8), j < 2) for j in range(3)]
            occupees.update((x, y + j) for j in range(3))
            objets.append({"taille": taille_, "tuiles": tuiles,
                           "lumiere": (x * 16 + 8, y * 16 + 6, *LUMIERE_TORCHE)})


def poser_toiles(taille_, forme, bords, rng):
    """Toiles d'araignée dans les coins du haut des salles (sur la face du mur)."""
    for x, y in bords:
        if ((x - 1, y) not in forme or (x + 1, y) not in forme) and tirage(x, y, 11).random() < 0.6 and (x, y) not in occupees:
            tuiles = [("decors", x, y, t("objets", rng.choice((7, 25))), False)]
            occupees.add((x, y))
            objets.append({"taille": taille_, "tuiles": tuiles})


def poser_salles(taille_, rng):
    """Piliers en rangées dans la grande salle (taille 1), cryptes (taille 3), or et coffres oubliés (taille 4)."""
    # Piliers (1 × 3 : chapiteau et fût dans « Devant », base bloquée), surtout brisés : en rangées dans la grande salle
    if taille_ == 1:
        for x in range(32, 49, 4):
            for y in (24, 33):
                poser(1, "piliers", rng.choice((0, 1, 1, 2, 2)), 0, 1, 3, x, y, devant=2, bloque=(2,), marge=0)
    essayer(taille_, {1: 0, 2: 0, 3: 5, 4: 6}[taille_],
            lambda x, y: poser(taille_, "piliers", rng.choice((1, 2)), 0, 1, 3, x, y, devant=2, bloque=(2,)), rng)
    # Cryptes (les salles du nord de la taille 3) : tombes en rangées ; statue d'ange au fond
    if taille_ == 3:
        for x0 in (13, 62):
            for x in (x0, x0 + 2, x0 + 4):
                for y in (15, 19):
                    poser(3, "objets", rng.choice((3, 4)), 4, 1, 2, x, y, devant=1, bloque=(1,), marge=0)
            poser(3, "objets", 0, 4, 3, 2, x0 + 1, 22, devant=1, bloque=(1,), marge=0)
    # Or et coffres oubliés (les grandes salles des bords, à la carte entière)
    if taille_ == 4:
        essayer(4, 3, lambda x, y: poser(4, "or", rng.choice((0, 2)), 0, 2, 2, x, y, devant=1, bloque=(1,)), rng)
        essayer(4, 3, lambda x, y: poser(4, "coffre", 0, 1, 2, 1, x, y, bloque=(0,)), rng)
        essayer(4, 4, lambda x, y: poser(4, "or", rng.randrange(4), 2, 1, 1, x, y, marge=0), rng)


def poser_debris(taille_, rng):
    """Pots, caisses, armoires renversées (bloqués) ; os et gravats (on y marche) ; pics du sol (animés, bloqués). Peu
    (« trop d'objets au sol », 2026-11-07) ; moins encore à la taille 2, qui laisse juste 14 cases libres par Pokémon."""
    n = {1: 1, 2: 1, 3: 2, 4: 3}[taille_]
    essayer(taille_, 2 * n, lambda x, y: poser(taille_, "objets", *rng.choice(((5, 1), (5, 2), (5, 5), (6, 5), (0, 3), (1, 0))),
                                               1, 1, x, y, bloque=(0,)), rng)
    essayer(taille_, n, lambda x, y: poser(taille_, "objets", 3, 0, 1, 2, x, y, devant=1, bloque=(1,)), rng)
    essayer(taille_, 2 * n, lambda x, y: poser(taille_, "objets", *rng.choice(((2, 0), (4, 1), (3, 2), (4, 2), (2, 3))),
                                               1, 1, x, y, marge=0), rng)
    essayer(taille_, 0 if taille_ == 2 else n, lambda x, y: all(poser(taille_, "pics", 0, 1, 1, 1, x + i, y, bloque=(0,), marge=0) for i in range(2)), rng)


def distance_a_la_lumiere(c, lumieres):
    """Distance du centre de la case c à la lumière la plus proche (99 sans lumière)."""
    return min((math.dist((c[0] + 0.5, c[1] + 0.5), lu) for lu in lumieres), default=99)


def poser_candelabres(taille_, forme):
    """Candélabres (1 × 2, la flamme dans « Devant ») jusqu'à ce qu'aucune case de dallage de cette taille ne soit à plus
    de PORTEE cases d'une lumière."""
    lumieres = [(o["lumiere"][0] / 16, o["lumiere"][1] / 16) for o in objets if "lumiere" in o]
    reste = sorted(c for c in DALLE if c in forme and piece_de(*c)[0] == taille_)
    while reste:
        loin = max(reste, key=lambda c: (distance_a_la_lumiere(c, lumieres), c))
        if distance_a_la_lumiere(loin, lumieres) <= PORTEE:
            break
        x, y = loin
        if poser(taille_, "objets", 2, 1, 1, 2, x, y - 1, devant=1, bloque=(1,), marge=1,
                 lumiere=(x * 16 + 8, y * 16 - 4, *LUMIERE_BOUGIES)):
            lumieres.append((x + 0.5, y - 0.25))
        else:
            reste.remove(loin)


def placer_objets(taille_):
    """Objets apparus à cette taille, posés dans l'ordre (le tirage de chaque taille en dépend)."""
    rng = random.Random(100 + taille_)
    forme = forme_de(taille_)
    _, _, faces = murs(forme)
    bords = sorted((x, y) for x, y in forme if (x, y - 1) not in forme and (x, y) in faces
                   and piece_de(x, y)[0] == taille_ and (x, y + 2) in forme)
    poser_torches(taille_, bords)
    poser_toiles(taille_, forme, bords, rng)
    poser_salles(taille_, rng)
    poser_debris(taille_, rng)
    poser_candelabres(taille_, forme)


for taille_ in range(1, 5):
    placer_objets(taille_)


# --- Carte de chaque taille --------------------------------------------------------------------
def gravats(x, y):
    """Gravats (bloqués) : les poches de dallage enfermées."""
    return t("objets", tirage(x, y, 2).choice((21, 22, 29)))


def taille(n, libres_avant):
    """Carte de la taille n : sol, décors, feuillages, cases bloquées, cases libres, eau libre et lumières."""
    zone = ZONES[n - 1]
    forme = forme_de(n)
    if forme - set().union(*(rectangle(z[0], z[1], z[0] + z[2] - 1, z[1] + z[3] - 1) for z in ZONES[n - 1:n])):
        sys.exit(f"Taille {n} : une pièce déborde de la zone")
    dessus, face, faces = murs(forme)
    sol_ = [[t("donjon", ROCHE)] * L for _ in range(H)]
    decors_ = [[0] * L for _ in range(H)]
    devant_ = [[0] * L for _ in range(H)]
    bloque_ = [[(x, y) not in forme or (x, y) in faces for x in range(L)] for y in range(H)]
    for (x, y), i in dessus.items():
        decors_[y][x] = t("donjon", i)
    for x, y in forme:
        sol_[y][x] = t("donjon", face.get((x, y), DALLAGE + x % 3 + 13 * (y % 3)))
    poser_bassins(n, sol_, decors_, bloque_)
    lumieres = poser_objets(n, {"decors": decors_, "devant": devant_}, bloque_)
    # Poches enfermées (entre les objets) : des gravats, personne n'y va
    gardees = carte.boucher_poches(zone, decors_, bloque_, gravats)
    perdues = libres_avant - gardees
    if perdues:
        sys.exit(f"Taille {n} : cases libres de la taille d'avant bloquées : {sorted(perdues)[:10]}")
    return sol_, decors_, devant_, bloque_, gardees, eau_libre(n, zone, forme, decors_, gardees), lumieres


def poser_bassins(n, sol_, decors_, bloque_):
    """Bassins de la taille n : l'eau bloquée (les nageurs y vont), le rebord où l'on marche, le pont."""
    for b in bassins:
        if b["taille"] > n:
            continue
        for (x, y), i in b["tuiles"].items():
            sol_[y][x] = t("egout", i)
            bloque_[y][x] = (x, y) in b["eau"]
        for (x, y), i in b["pont"].items():
            decors_[y][x] = t("pont", i)
            bloque_[y][x] = False


def poser_objets(n, grilles, bloque_):
    """Objets de la taille n et des plus petites ; rend leurs lumières."""
    lumieres = []
    for objet_ in objets:
        if objet_["taille"] > n:
            continue
        for calque, x, y, tuile, bloquee in objet_["tuiles"]:
            grilles[calque][y][x] = tuile
            bloque_[y][x] = bloque_[y][x] or bloquee
        if "lumiere" in objet_:
            lumieres.append(objet_["lumiere"])
    return lumieres


def eau_libre(n, zone, forme, decors_, gardees):
    """Eau des bassins de la taille n où l'on nage, reliée au rebord où l'on marche."""
    eau = {(x, y) for x, y in EAU & forme if not decors_[y][x] and dans(zone, x, y) and any(b["taille"] <= n and (x, y) in b["eau"] for b in bassins)}
    if eau and not eau.issubset(relies(eau | gardees)):
        sys.exit(f"Taille {n} : de l'eau d'un bassin n'est pas reliée au rebord où l'on marche")
    return eau


# --- Écriture des .tmx -----------------------------------------------------------------------
APERCUS = sys.argv[3] if len(sys.argv) > 3 else SORTIE
libres = set()
for n, zone in enumerate(ZONES, 1):
    sol_, decors_, devant_, bloque_, libres, eau, lumieres = taille(n, libres)
    carte.ecrire_carte(SORTIE, f"Donjon_1-{n}.tmx", DEPART, zone, sol_, decors_, devant_, bloque_, fond=FOND,
                       lumieres=lumieres)
    carte.apercu(APERCUS, f"donjon-{n}", (sol_, decors_, devant_), bloque_, fond=(0x16, 0x14, 0x1f))
    zx, zy, zl, zh = zone
    print(f"Donjon_1-{n}.tmx : zone {zl} × {zh}, eau {len(eau)} cases, {len(libres)} cases libres "
          f"({len(libres) / (20 * n):.1f} par Pokémon), {len(lumieres)} lumières")
