# Génère les cartes de la montagne pour Tiled (Montagne_1-1.tmx à Montagne_1-4.tmx, une par taille achetée) avec le pack
# Cute Fantasy : une vallée d'herbe traversée d'ouest en est par une rivière (trois ponts, dont un dans la vue de départ), des massifs
# aux flancs de pierre sur deux étages (des plateaux, et des sommets posés dessus), des sapins, des rochers et des nids sur
# les sommets, et la forêt de sapins qui cache ce qui n'est pas encore acheté. Tirages fixés : relancé sans changement, il
# redonne les mêmes cartes.
# Choix de l'utilisateur (2026-11-07) : tous les Pokémon y vivent, seuls les volants (type Vol) montent sur la montagne ;
# montagnes vertes à flancs de pierre, deux étages sans escalier ; une rivière qui traverse. Tous marchent dans la vallée
# (le pont relie ses deux rives) ; les volants survolent aussi la rivière et les flancs, et se posent sur les plateaux et
# les sommets : calques « Sommets » (où un volant se pose) et « Survol » (où il passe sans
# s'arrêter : rebords et flancs), lus par parc.js ; ces cases sont bloquées dans « Collisions » (personne n'y marche).
# Ensuite, pour chacune : python outils/carte-parc.py <Montagne_1-1.tmx>. Aperçus (montagne-1.png,
# montagne-1_collisions.png...) dans le dossier de sortie, ou dans le troisième. Moteur commun : parc_carte.py.
# Usage (Python 3 avec Pillow) :
#   python outils/generer-montagne.py <dossier Cute_Fantasy du pack> <dossier de sortie> [dossier des aperçus]
import sys
from parc_carte import Carte, bordure, bruit, dans, ecrire, nettoyer, tirage, voisins8, voisins_relies

PACK, SORTIE = sys.argv[1], sys.argv[2]
L, H = 80, 60
# Zone de chaque taille (x, y, largeur, hauteur, en tuiles) : 20, 40, 60 puis 80 Pokémon (ParkService). Celles du volcan.
DEPART = (28, 20, 24, 20)
ZONES = [DEPART, (20, 15, 40, 30), (12, 10, 56, 40), (0, 0, L, H)]
carte = Carte(PACK, L, H, 17)
rng, jeu, t, dedans = carte.rng, carte.jeu, carte.t, carte.dedans
bloc, poser, essayer = carte.bloc, carte.poser, carte.essayer
sol, decors, devant, objets = carte.sol, carte.decors, carte.devant, carte.objets
occupe_decors = carte.occupe_decors

# --- Jeux de tuiles (incorporés à la carte) -------------------------------------------------
# Une herbe par étage (les jeux de couleurs vont par numéro : Grass_Tiles_n, Grass_n_Middle et Water_Tile_n ont la même
# herbe) : la vallée en vert bleuté (n° 4), les plateaux en vert (n° 1), les sommets en vert jaune (n° 3, des alpages)
HERBES = {"vallee": 4, "plateau": 1, "sommet": 3}
for nom, n in HERBES.items():
    jeu(nom, f"Tiles/Grass/Grass_Tiles_{n}.png")
    jeu(nom + "_unie", f"Tiles/Grass/Grass_{n}_Middle.png")
jeu("eau", "Tiles/Water/Water_Tile_4.png")
jeu("eau_unie", "Tiles/Water/Water_Middle.png")
jeu("eau_details", "Tiles/Water/Water_Decoration.png")
jeu("pont", "Tiles/Bridge/Bridge_Wood.png")
jeu("decors", "Outdoor decoration/Outdoor_Decor.png")
jeu("fleurs", "Outdoor decoration/Flowers.png")
jeu("nids", "Outdoor decoration/Nests.png")
jeu("sapin", "Trees/Medium_Spruce_Tree.png")
jeu("grand_sapin", "Trees/Big_Spruce_tree.png")
jeu("petit_sapin", "Trees/Small_Spruce_Tree.png")


def herbe_de(etage_, x, y):
    """Herbe de l'étage en (x, y) : unie, parfois des touffes (tirage propre à la case)."""
    r = tirage(x, y, 7)
    return t(etage_, r.choice((149, 150, 151))) if r.random() < 0.12 else t(etage_ + "_unie", 0)


# --- Massifs : plateaux à flancs de pierre (Grass_Tiles) -----------------------------------
# Le bord d'un plateau passe au milieu des cases : une case prend sa tuile d'après ses quatre coins (NO, NE, SO, SE ; un
# coin est sur le plateau si les quatre cases autour sont dans la forme). Rebords : la tour de pierre de la planche
# (colonnes 11-13 : coins, haut et côtés) et l'anneau de pierre (83-148 : coins rentrants du haut, 115 et 117, même
# rebord). Le bord du bas porte le flanc (tour, lignes 2 à 4) : <faces> lignes sous le plateau, celle du milieu répétée.
# La planche n'a pas de coin rentrant avec un flanc : chaque colonne d'un plateau descend jusqu'à la même dernière ligne
# (bas plat ; le haut, lui, est libre). Les tuiles d'un étage sont celles de sa planche d'herbe (rebord de son herbe) ;
# leur partie transparente montre le sol de l'étage du dessous.
REBORD = {(0, 0, 0, 1): 11, (0, 0, 1, 0): 13, (0, 0, 1, 1): 12, (0, 1, 0, 1): 27, (1, 0, 1, 0): 29,
          (1, 0, 1, 1): 115, (0, 1, 1, 1): 117}
FLANC = {(0, 1, 0, 0): (43, 59, 75), (1, 1, 0, 0): (44, 60, 76), (1, 0, 0, 0): (45, 61, 77)}  # haut, milieu, pied


def coins_de(forme, x, y):
    coin = lambda a, b: all((a + i, b + j) in forme for i in (-1, 0) for j in (-1, 0))
    return int(coin(x, y)), int(coin(x + 1, y)), int(coin(x, y + 1)), int(coin(x + 1, y + 1))


def plateau(x0, x1, haut, bas, graine, relief=1.6):
    """Forme d'un plateau des colonnes x0 à x1 : chaque colonne de son haut (bruit, épaules arrondies près des bords)
    jusqu'à la ligne <bas>."""
    r = bruit(L, relief, 2.2, graine)
    cellules = set()
    for x in range(x0, x1 + 1):
        epaule = max(0, 2 - min(x - x0, x1 - x))
        h = min(bas - 2, round(haut + r[x] + epaule * 0.9))
        cellules |= {(x, y) for y in range(h, bas + 1)}
    return nettoyer(cellules)


def etage(forme, faces, nom):
    """Tuiles d'un étage (planche <nom>) : {case : tuile de la carte}, l'emprise (rebords, dessus et flanc), les cases où
    l'on se pose (trois coins au moins sur le plateau) et celles du dessus entier (quatre coins : son herbe dans « Sol »)."""
    tuiles, pose, plein = {}, set(), set()
    for x, y in forme:
        k = coins_de(forme, x, y)
        if k == (1, 1, 1, 1):
            plein.add((x, y))
        elif k in FLANC:
            haut_, milieu, pied = FLANC[k]
            tuiles[(x, y)] = t(nom, haut_)
            for j in range(1, faces + 1):
                tuiles[(x, y + j)] = t(nom, milieu if j < faces else pied)
        elif k in REBORD:
            tuiles[(x, y)] = t(nom, REBORD[k])
        elif sum(k):
            sys.exit(f"Plateau : coins {k} sans tuile en {(x, y)}")
        if sum(k) >= 3:
            pose.add((x, y))
    return tuiles, set(forme) | set(tuiles), pose, plein


# Chaque massif : un plateau (x0, x1, haut, bas, lignes de flanc, graine), parfois un sommet posé dessus. Hors de la forêt
# de bordure (elle les couperait : trous dans les rebords), à deux cases au moins de la rivière. Un massif n'est montré
# que tout entier : à une taille où il déborde de la zone, la forêt le cache (2026-11-07, « les montagnes qui ne sont pas
# entières » dans le parc 3) ; chacun tient donc tout entier dans la zone de la taille où il apparaît (« taille », vérifié).
MASSIFS = [
    # Vue de départ, à l'est : un plateau et son sommet
    {"taille": 1, "plateau": (40, 47, 19, 29, 3, 1), "sommet": (42, 45, 23, 25, 2, 2)},
    # Anneau de la taille 2 : au nord-ouest et au nord-est de la vue de départ
    {"taille": 2, "plateau": (22, 27, 19, 27, 3, 3)},
    {"taille": 2, "plateau": (52, 57, 20, 27, 3, 4)},
    # Au sud de la rivière (taille 3 : ils débordent de la zone de la taille 2)
    {"taille": 3, "plateau": (22, 27, 42, 46, 3, 5)},
    {"taille": 3, "plateau": (52, 57, 42, 46, 3, 6)},
    # Les quatre massifs des coins de la taille 3 (avec un sommet au nord : au sud, la rivière laisse trop peu de place)
    {"taille": 3, "plateau": (12, 19, 10, 22, 4, 11), "sommet": (14, 17, 13, 17, 3, 12)},
    {"taille": 3, "plateau": (60, 67, 11, 21, 4, 24), "sommet": (62, 65, 15, 17, 2, 14)},
    {"taille": 3, "plateau": (12, 19, 38, 46, 3, 15)},
    {"taille": 3, "plateau": (60, 67, 40, 46, 3, 17)},
]
massifs = []
tuiles_massifs = {}  # case → tuile de « Décors », pour tous les massifs
sol_etages = {}  # case → herbe de l'étage (dessus entier d'un plateau ou d'un sommet)
for m in MASSIFS:
    x0, x1, haut, bas, faces, graine = m["plateau"]
    forme = plateau(x0, x1, haut, bas, graine)
    tuiles, emprise, pose, plein = etage(forme, faces, "plateau")
    sols = dict.fromkeys(plein, "plateau")
    survol = emprise - pose
    if "sommet" in m:
        sx0, sx1, shaut, sbas, sfaces, sgraine = m["sommet"]
        forme2 = plateau(sx0, sx1, shaut, sbas, sgraine, relief=1.0)
        tuiles2, emprise2, pose2, plein2 = etage(forme2, sfaces, "sommet")
        # Le sommet tient sur le dessus du plateau, avec une case d'herbe tout autour
        if not voisins8(emprise2).issubset(plein):
            sys.exit(f"Sommet {m['sommet']} : déborde du dessus de son plateau : {sorted(voisins8(emprise2) - plein)[:8]}")
        tuiles.update(tuiles2)
        sols.update(dict.fromkeys(plein2, "sommet"))
        pose = (pose - emprise2) | pose2
        survol = (survol | emprise2) - pose
    if set(tuiles) & set(tuiles_massifs) or voisins8(voisins8(emprise)) & set().union(*(m_["emprise"] for m_ in massifs)):
        sys.exit(f"Massif {m['plateau']} : trop près d'un autre")
    tuiles_massifs.update(tuiles)
    sol_etages.update(sols)
    massifs.append({"emprise": emprise, "pose": pose, "survol": survol, "tuiles": tuiles, "taille": m["taille"]})
emprises = set().union(*(m["emprise"] for m in massifs))
poses = set().union(*(m["pose"] for m in massifs))
survols = set().union(*(m["survol"] for m in massifs))

# --- Rivière : d'ouest en est, 4 ou 5 cases de large, qui serpente entre les massifs --------
# Prolongée hors de la carte (pas de berge le long des bords : elle entre et sort par la bordure, et la forêt qui cache ce
# qui n'est pas acheté ne la couvre jamais : sinon, aux petites tailles, « un lac bizarre », 2026-11-07). Trois ponts
# (demande de l'utilisateur) : celui de la vue de départ, et un à l'ouest et à l'est, qui apparaissent à la taille 3 ; la
# rivière est droite sous chacun (colonnes x à x + 2). Gardée à deux cases au moins des massifs : à chaque colonne, entre
# le bas des massifs du nord et le haut de ceux du sud.
PONTS = [15, 36, 62]  # colonne de gauche de chaque pont
COURS = [(0, 30), (12, 32), (22, 34), (30, 34.6), (37, 35), (44, 36), (52, 35.6), (60, 32.8), (70, 30.5), (L - 1, 30)]


def cours(x):
    for (xa, ya), (xb, yb) in zip(COURS, COURS[1:]):
        if xa <= x <= xb:
            return ya + (yb - ya) * (x - xa) / (xb - xa)
    return COURS[0][1] if x < 0 else COURS[-1][1]


ondule, largeur = bruit(L + 4, 1.6, 3.2, 21), bruit(L + 4, 1, 2.5, 22)


def pont_de(x):
    """Le pont dont la rivière passe droite en colonne x (une case de plus de chaque côté), ou None."""
    return next((p for p in PONTS if p - 1 <= x <= p + 3), None)



def couloir(x, large):
    """Lignes permises pour le haut de la rivière en colonne x (<large> cases de large) : à trois lignes au moins des
    massifs proches, au nord comme au sud."""
    proches = [c for c in emprises if abs(c[0] - x) <= 2]
    haut_ = max([y + 3 for _, y in proches if y < COURS_MILIEU] + [0])
    bas_ = min([y - 3 - (large - 1) for _, y in proches if y >= COURS_MILIEU] + [H])
    return haut_, bas_


COURS_MILIEU = 33  # ligne qui sépare les massifs du nord de ceux du sud
riviere_hors = set()
for x in range(-2, L + 2):
    pont_x = pont_de(x)
    large = 4 if pont_x is not None or largeur[x + 2] < 0.45 else 5
    if pont_x is not None:
        bornes = [couloir(c, 4) for c in range(pont_x - 1, pont_x + 4)]
        haut_, bas_ = max(h for h, _ in bornes), min(b for _, b in bornes)
        y0 = cours(pont_x + 1)
    else:
        haut_, bas_ = couloir(x, large)
        y0 = cours(x) + ondule[x + 2]
    if haut_ > bas_:
        sys.exit(f"Rivière : pas de place entre les massifs en colonne {x}")
    y0 = min(bas_, max(haut_, round(y0)))
    riviere_hors |= {(x, y0 + k) for k in range(large)}
riviere_hors = nettoyer(riviere_hors)
eau = {c for c in riviere_hors if dedans(*c)}

# --- Forêt de bordure (carte entière) : comme la forêt du parc 1, 2 à 3 cases de sapins à l'œil ---
# La rivière la traverse (elle entre et sort par les bords de la carte)
haut_b, bas_b = bruit(L, 2.2, 6, 31), bruit(L, 2.2, 6, 32)
gauche_b, droite_b = bruit(H, 2.2, 6, 33), bruit(H, 2.2, 6, 34)
foret = {(x, y) for y in range(H) for x in range(L)
         if y < 6 + haut_b[x] or y >= H - 5 + bas_b[x] or x < 5 + gauche_b[y] or x >= L - 5 + droite_b[y]}
# Bosquets de sapins dans la vallée (au nord, au sud), loin de la rivière et des massifs
foret |= carte.tache(33, 11, 4.5, 2.6) | carte.tache(49, 9.5, 4, 2.4) | carte.tache(39, 51, 4.5, 2.2)
foret -= voisins8(eau)
carte.lisiere = set(foret)
for m in massifs:
    if voisins8(m["emprise"]) & foret:
        sys.exit(f"Massif {min(m['emprise'])} : dans la forêt de bordure : {sorted(voisins8(m['emprise']) & foret)[:8]}")


# --- Sol : herbe de chaque étage, rivière à bordures automatiques ----------------------------
for y in range(H):
    for x in range(L):
        sol[y][x] = herbe_de(sol_etages.get((x, y), "vallee"), x, y)
herbe = [ligne[:] for ligne in sol]
EAU = ("eau", (0, 1, 2, 3, 5, 6, 7, 8), (9, 10, 12, 13))


def milieu_eau(x, y):
    r = tirage(x, y, 6)
    return t("eau_details", r.randrange(3)) if r.random() < 0.06 else t("eau_unie", 0)


for x, y in eau:
    i = bordure(riviere_hors, x, y, *EAU[1:])
    sol[y][x] = t("eau", i) if i is not None else milieu_eau(x, y)
occupe_decors.update(tuiles_massifs)


# --- Objets ------------------------------------------------------------------------------
def dans_depart(x, y, marge=0):
    dx, dy, dl, dh = DEPART
    return dx - marge <= x < dx + dl + marge and dy - marge <= y < dy + dh + marge


vallee = {(x, y) for y in range(H) for x in range(L)} - emprises - voisins8(eau)
sur_sommet = set(poses)
carte.libre_sol = lambda x, y: (x, y) in vallee or (x, y) in sur_sommet

# Ponts de bois (nord-sud) sur la rivière : planches sur la colonne du milieu (on y marche), poteaux sur les deux berges
# (bloqués). Chacun n'est posé qu'à la taille où il est tout entier dans la zone (là où ses poteaux étaient encore cachés)
for pont_x in PONTS:
    lignes_eau = sorted(y for x, y in eau if x == pont_x + 1)
    haut_pont, bas_pont = lignes_eau[0] - 1, lignes_eau[-1] + 1
    if any((pont_x + i, y) not in eau for i in range(3) for y in range(haut_pont + 1, bas_pont)):
        sys.exit(f"Pont {pont_x} : la rivière n'est pas droite sous le pont")
    pont = []
    for y in range(haut_pont, bas_pont + 1):
        rang = 1  # les planches du milieu
        if y == haut_pont:
            rang = 0
        elif y == bas_pont:
            rang = 2
        for i in range(3):
            tuile = (12, 13, 14, 21, 22, 23, 30, 31, 32)[rang * 3 + i]
            if rang == 1 and i != 1:
                continue  # tuiles 21 et 23 vides
            pont.append(("decors", pont_x + i, y, t("pont", tuile), i != 1))
    for _, x, y, _, _ in pont:
        occupe_decors.add((x, y))
    objets.append({"tuiles": pont, "debloque": {(pont_x + 1, y) for y in range(haut_pont, bas_pont + 1)}, "pont": True})
    occupe_decors |= {(pont_x + 1, haut_pont - 1), (pont_x + 1, bas_pont + 1)}  # rien devant les deux bouts

# Sapins de la vallée (le grand : feuillage sur 4 lignes ; le moyen : 2), petits sapins sur les sommets
grand_sapin = lambda x, y: poser("grand_sapin", 5, 0, 2, 5, x, y, rang_devant=4, bloque_lignes=(4,), marge=1)
sapin = lambda x, y: poser("sapin", 2, 0, 2, 3, x, y, rang_devant=2, bloque_lignes=(2,), marge=1)
petit_sapin = lambda x, y: poser("petit_sapin", 2, 2, 2, 2, x, y, rang_devant=1, bloque_lignes=(1,))
en_vallee = lambda x, y, l, h: all((x + i, y + j) in vallee for i in range(l) for j in range(h))
en_haut = lambda x, y, l, h: all((x + i, y + j) in sur_sommet for i in range(l) for j in range(h))
essayer(8, lambda x, y: not dans_depart(x, y + 4, 2) and en_vallee(x, y + 4, 2, 1) and grand_sapin(x, y))
essayer(30, lambda x, y: not dans_depart(x, y + 2, 1) and en_vallee(x, y + 2, 2, 1) and sapin(x, y))
essayer(3, lambda x, y: dans_depart(x, y + 2) and en_vallee(x, y + 2, 2, 1) and sapin(x, y))
essayer(16, lambda x, y: en_haut(x, y + 1, 2, 1) and petit_sapin(x, y))

# Gros rochers (2 × 2) au pied des flancs et dans la vallée
essayer(10, lambda x, y: not dans_depart(x, y, 1) and en_vallee(x, y, 2, 2)
        and poser("decors", rng.choice((4, 6)), 11, 2, 2, x, y, bloque_lignes=(0, 1), marge=1))
essayer(1, lambda x, y: dans_depart(x, y) and en_vallee(x, y, 2, 2)
        and poser("decors", rng.choice((4, 6)), 11, 2, 2, x, y, bloque_lignes=(0, 1), marge=1))


def objet(nom, liste, n, ou, bloquant=False, marge=0):
    """n objets d'une case (tuiles de <liste>) là où ou(x, y)."""
    def un(x, y):
        if not ou(x, y) or not poser(nom, 0, 0, 1, 1, x, y, verifier=True, marge=marge):
            return False
        decors[y][x] = t(nom, rng.choice(liste))
        carte.bloque[y][x] = bloquant
        objets[-1]["tuiles"] = [("decors", x, y, decors[y][x], bloquant)]
        return True
    essayer(n, un)


ROCHERS = [40, 41, 45, 46, 57, 58, 59]
BUISSONS = [50, 84, 86, 87, 88]
SOUCHES = [55, 81, 83]
PLANTES = [24, 49, 71, 78, 79, 80, 89]
PETITES_FLEURS = [0, 1, 2, 3, 4, 5, 9, 10, 11, 12, 13, 14, 18, 19, 20, 21, 22, 23]
FLEURS = [r * 10 + c for r in range(10) for c in range(5)]
NIDS = [0, 1, 3, 4, 6, 7, 9, 10]  # nids vides ou avec des œufs (pas les dorés)
valle = lambda x, y: (x, y) in vallee
haut_ = lambda x, y: (x, y) in sur_sommet
objet("decors", ROCHERS, 18, lambda x, y: valle(x, y) and not dans_depart(x, y), bloquant=True, marge=1)
objet("decors", ROCHERS, 3, lambda x, y: valle(x, y) and dans_depart(x, y), bloquant=True, marge=1)
objet("decors", ROCHERS, 14, haut_, bloquant=True, marge=1)
objet("decors", BUISSONS, 14, lambda x, y: valle(x, y) and not dans_depart(x, y), bloquant=True, marge=1)
objet("decors", SOUCHES, 4, lambda x, y: valle(x, y) and not dans_depart(x, y), bloquant=True, marge=1)
objet("nids", NIDS, 14, haut_, marge=1)
objet("decors", PLANTES, 30, valle)
objet("decors", PETITES_FLEURS, 50, valle)
objet("decors", PETITES_FLEURS, 30, haut_)
objet("fleurs", FLEURS, 40, lambda x, y: valle(x, y) or haut_(x, y))


# --- Carte de chaque taille : la zone, de la forêt de sapins tout autour ---------------------
# Lisière irrégulière : la forêt entre de 0 à 2 cases dans la zone (moins que l'écart entre deux zones)
LISIERE = [[max(0, min(2, round(0.8 + v))) for v in bruit(max(L, H), 1.3, 2.5, graine)] for graine in (41, 42, 43, 44)]


def foret_de(zone):
    """Forêt d'une taille : la bordure de la carte, et tout ce qui est hors de la zone (lisière irrégulière). La lisière
    contourne les objets et les massifs qui tiennent dans la zone ; un massif qui n'y tient pas est caché tout entier
    (jamais montré coupé : demande de l'utilisateur, 2026-11-07)."""
    zx, zy, zl, zh = zone
    haut__, bas__, gauche__, droite__ = LISIERE
    foret_ = foret | {(x, y) for y in range(H) for x in range(L)
                      if not (zx + gauche__[y] <= x < zx + zl - droite__[y] and zy + haut__[x] <= y < zy + zh - bas__[x])}
    for objet_ in objets:
        cellules = {(x, y) for _, x, y, _, _ in objet_["tuiles"]}
        if all(dans(zone, x, y) for x, y in cellules) and not cellules & foret:
            foret_ -= cellules
    for m in massifs:
        if all(dans(zone, x, y) for x, y in m["emprise"]) and not m["emprise"] & foret:
            foret_ -= {c for c in voisins8(m["emprise"]) if dans(zone, *c)}
        else:
            foret_ |= m["emprise"]
    return foret_ - eau  # la rivière traverse la forêt jusqu'aux bords de la carte


def sol_de(foret_):
    """Sol d'une taille : celui du parc entier (la rivière comprise) ; sous la forêt, l'herbe de la vallée (pas de plateau
    caché qui se devine sous les sapins)."""
    sol_ = [ligne[:] for ligne in sol]
    for x, y in foret_:
        if (x, y) in sol_etages:
            sol_[y][x] = herbe_de("vallee", x, y)
    return sol_


def arbre_de_foret(x, y):
    return "sapin" if tirage(x, y, 1).random() < 0.85 else "petit_sapin"


def planter_arbres(foret_, grilles, bloque_):
    """Forêt de sapins en quinconce : un sapin dont les deux cases du tronc sont dans la forêt, sans tuile de massif, et
    dont le feuillage ne tombe pas sur un massif montré (la rangée de la lisière cachait le pied des flancs : retirée à la
    demande de l'utilisateur, 2026-11-07 ; ses buissons restent)."""
    boisee = lambda x, y: not dedans(x, y) or ((x, y) in foret_ and not grilles["decors"][y][x])
    sur_un_massif = lambda x, y: any((x + i, y + j) in emprises and (x + i, y + j) not in foret_ for i in (0, 1) for j in (0, 1))
    for y in range(-2, H, 2):
        for x in range(-1 + (y // 2) % 2, L, 2):
            if boisee(x, min(y + 2, H - 1)) and boisee(x + 1, min(y + 2, H - 1)) and not sur_un_massif(x, y):
                if arbre_de_foret(x, y) == "sapin":
                    ecrire(grilles, bloque_, bloc("sapin", 2, 0, 2, 3, x, y, rang_devant=2, bloque_lignes=(2,)))
                else:
                    ecrire(grilles, bloque_, bloc("petit_sapin", 2, 2, 2, 2, x, y + 1, rang_devant=1, bloque_lignes=(1,)))


def planter_sous_bois(foret_, origine, grilles, bloque_):
    """Ailleurs dans la forêt, des buissons sombres (sous les feuillages seulement dans la forêt d'origine)."""
    decors_, devant_ = grilles["decors"], grilles["devant"]
    for x, y in foret_:
        r = tirage(x, y, 2)
        if (x, y) in origine:
            if not decors_[y][x] and not devant_[y][x]:
                continue  # bord de la bordure sans arbre : de l'herbe où l'on marche
            bloque_[y][x] = True
        if not decors_[y][x] and (not devant_[y][x] or r.random() < 0.85):
            decors_[y][x] = t("decors", r.choice((86, 87, 88)))


def buisson(x, y):
    return t("decors", tirage(x, y, 2).choice((86, 87, 88)))


def relier(depart, passage):
    """Cases de <passage> reliées à <depart> (même règle de diagonale que parc.js)."""
    relies_, pile = {depart}, [depart]
    while pile:
        x, y = pile.pop()
        for v in voisins_relies(x, y, passage):
            if v in passage and v not in relies_:
                relies_.add(v)
                pile.append(v)
    return relies_


def poser_objets(grilles, bloque_, foret_, gardees):
    """Objets du parc entier, s'ils sont tout entiers dans la zone, sans chevaucher la forêt ni bloquer une case libre
    d'avant (<gardees>). Rend les cases qu'ils bloquent."""
    bloques_objets = set()
    for objet_ in objets:
        tuiles = objet_["tuiles"]
        if any((x, y) in foret_ or grilles[calque][y][x] or (bloquee and (x, y) in gardees) for calque, x, y, _, bloquee in tuiles):
            continue
        ecrire(grilles, bloque_, tuiles)
        bloques_objets |= {(x, y) for _, x, y, _, bloquee in tuiles if bloquee}
        for x, y in objet_.get("debloque", ()):
            bloque_[y][x] = False
    return bloques_objets


def terre_a_pied(cases, decors_, bloque_):
    """Tous les Pokémon marchent dans la vallée : la terre libre doit être reliée à pied (la rivière se traverse par le
    pont) ; ce qui ne l'est pas : des buissons, personne n'y va. Rend la case de départ et la terre reliée."""
    terre = {c for c in cases if not bloque_[c[1]][c[0]]}
    depart = next(c for c in sorted(terre) if dans(DEPART, *c))
    terre_reliee = relier(depart, terre)
    for x, y in terre - terre_reliee:
        bloque_[y][x] = True
        if not decors_[y][x]:
            decors_[y][x] = buisson(x, y)
    return depart, terre_reliee


def taille(zone, libres_avant, sommets_avant):
    """Carte d'une taille : sol, décors, feuillages, cases bloquées, et pour les volants les cases où se poser (terre,
    sommets) et celles qu'ils survolent. Ce qu'une taille montre, la suivante le montre encore (rien n'est repris)."""
    foret_ = foret_de(zone)
    for m in massifs:
        if m["taille"] <= ZONES.index(zone) + 1 and m["emprise"] & foret_:
            sys.exit(f"Zone {zone} : massif {min(m['emprise'])} pas montré tout entier (il déborde de la zone)")
    eau_ = eau  # toute la rivière, à chaque taille (bloquée hors de la zone comme dedans)
    sol_ = sol_de(foret_)
    decors_ = [[0] * L for _ in range(H)]
    devant_ = [[0] * L for _ in range(H)]
    origine = {(x, y) for x, y in foret_ & foret if dans(zone, x, y)}
    bloque_ = [[((x, y) in foret_ and (x, y) not in origine) or (x, y) in eau_ or (x, y) in emprises
                for x in range(L)] for y in range(H)]
    grilles = {"decors": decors_, "devant": devant_}
    # Les massifs, sauf sous la forêt (coupés par la lisière)
    for (x, y), tuile in tuiles_massifs.items():
        if (x, y) not in foret_:
            decors_[y][x] = tuile
    planter_arbres(foret_, grilles, bloque_)
    planter_sous_bois(foret_, origine, grilles, bloque_)
    bloques_objets = poser_objets(grilles, bloque_, foret_, libres_avant | sommets_avant)
    zx, zy, zl, zh = zone
    cases = {(x, y) for y in range(zy, zy + zh) for x in range(zx, zx + zl)}
    depart, terre = terre_a_pied(cases, decors_, bloque_)
    # Seuls les volants montent : où ils se posent (les sommets) et où ils passent (rebords, flancs, eau libre) ; un sommet
    # qu'ils n'atteignent pas depuis la terre n'en est plus un
    sommets_ = {c for c in poses & cases if c not in foret_ and c not in bloques_objets}
    survol_ = {c for c in survols & cases if c not in foret_}
    eau_libre = {(x, y) for x, y in eau_ & cases if not decors_[y][x] and not devant_[y][x]}
    sommets_ &= relier(depart, terre | sommets_ | survol_ | eau_libre)
    perdues = (libres_avant - terre) | (sommets_avant - sommets_)
    if perdues:
        sys.exit(f"Zone {zone} : cases libres de la taille d'avant perdues : {sorted(perdues)[:10]}")
    sommets_grille = [[carte.gid_collision if (x, y) in sommets_ else 0 for x in range(L)] for y in range(H)]
    survol_grille = [[carte.gid_collision if (x, y) in survol_ else 0 for x in range(L)] for y in range(H)]
    return sol_, decors_, devant_, bloque_, terre, sommets_, survol_, sommets_grille, survol_grille


# --- Écriture des .tmx -----------------------------------------------------------------------
APERCUS = sys.argv[3] if len(sys.argv) > 3 else SORTIE
libres, sommets = set(), set()
for n, zone in enumerate(ZONES, 1):
    sol_, decors_, devant_, bloque_, libres, sommets, survol_, sg, vg = taille(zone, libres, sommets)
    carte.ecrire_carte(SORTIE, f"Montagne_1-{n}.tmx", DEPART, zone, sol_, decors_, devant_, bloque_,
                       [("Sommets", sg), ("Survol", vg)])
    carte.apercu(APERCUS, f"montagne-{n}", (sol_, decors_, devant_), bloque_)
    zx, zy, zl, zh = zone
    poses_ = len(libres) + len(sommets)
    print(f"Montagne_1-{n}.tmx : zone {zl} × {zh}, {len(libres)} cases de terre, {len(sommets)} sur les sommets, "
          f"{len(survol_)} survolées ({poses_ / (20 * n):.1f} cases où se poser par Pokémon)")
