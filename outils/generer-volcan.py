# Génère les cartes du volcan pour Tiled (Volcan_1-1.tmx à Volcan_1-4.tmx, une par taille achetée) avec le pack Cute
# Fantasy Volcano : un plateau de roche volcanique où des cascades de lave (animées) nourrissent des coulées qui finissent
# en mares, un pont de pierre, des rochers, des arbres et des plantes de braise, des bulles de lave (animées, calque
# « Bulles »), et le calque « Collisions » qui va avec. Tirages fixés : relancé sans changement, il redonne les mêmes
# cartes.
# Tout est de type « Volcan » (choix de l'utilisateur, 2026-11-01 : le parc des types Feu, Roche et Dragon ; voir
# ParkHabitats), 17 à 31 % de lave selon la zone. La lave est bloquée ; les types Feu y vont de temps en temps, les
# volants la survolent (parc.js : la lave libre, ce sont les cases de lave bloquées sans objet posé dessus : toute autre
# case bloquée porte un décor, le script le vérifie).
# Ce qui n'est pas encore acheté est un champ de gros rochers ; la lave qui en sort est coupée à la lisière.
# Ensuite, pour chacune : python outils/carte-parc.py <Volcan_1-1.tmx>. Aperçus (volcan-1.png, volcan-1_collisions.png...)
# dans le dossier de sortie, ou dans le troisième. Moteur commun avec le parc 1 et la plage : parc_carte.py.
# Usage (Python 3 avec Pillow) :
#   python outils/generer-volcan.py <dossier Cute_Fantasy du pack> <dossier de sortie> [dossier des aperçus]
import math
import sys
from parc_carte import Carte, bordure, bruit, dans, ecrire, morceaux_de, nettoyer, tirage, voisins8

PACK, SORTIE = sys.argv[1], sys.argv[2]
L, H = 80, 60
# Zone de chaque taille (x, y, largeur, hauteur, en tuiles) : 20, 40, 60 puis 80 Pokémon (ParkService). Celles du parc 1.
DEPART = (28, 20, 24, 20)
ZONES = [DEPART, (20, 15, 40, 30), (12, 10, 56, 40), (0, 0, L, H)]
carte = Carte(PACK, L, H, 13)
rng, jeu, t, dedans, tache = carte.rng, carte.jeu, carte.t, carte.dedans, carte.tache
bloc, poser, essayer = carte.bloc, carte.poser, carte.essayer
decors, devant, objets = carte.decors, carte.devant, carte.objets

# --- Jeux de tuiles (incorporés à la carte) -------------------------------------------------
VOLCAN = "../Cute_Fantasy_Volcano/"
jeu("volcan", VOLCAN + "Tiles/Volcano_Tiles.png")
jeu("cascade", VOLCAN + "Tiles/Volcano_Lavafall.png")
jeu("bulle", VOLCAN + "Tiles/Volcano_lava_buble.png")
jeu("pont", VOLCAN + "Tiles/Volcano_Bridge.png")
jeu("rochers", VOLCAN + "Volcano_Props/Volcano_Rocks.png")
jeu("plantes", VOLCAN + "Volcano_Props/Volcano_Plants.png")

# Sol : roche craquelée (tuiles qui se raccordent toutes ; les plus lisses d'abord), roche fendue de lave près des coulées
SOL_LISSE = [61, 3, 1, 59, 119, 60]
SOL_CRAQUELE = [32, 2, 30, 118, 89, 88, 117, 31]
SOL_BRULANT = [4, 6, 62, 64, 121, 122, 120, 91, 92]
# Bords de la lave (la lave est la forme, la roche autour) : coins HG H HD G D BG B BD, coins rentrants SE SO NE NO
LAVE = ("volcan", (181, 182, 183, 210, 212, 239, 240, 241), (184, 185, 213, 214))
LAVE_UNIE = 211
LAVE_CROUTE = [186, 187, 188, 215, 216, 217, 244, 245, 246]  # lave avec des morceaux de croûte

# Cascade de lave : 3 × 5 tuiles, 18 images côte à côte (Volcano_Lavafall : 54 colonnes) ; ligne 0, la lave qui arrive
# d'en haut, lignes 1 à 3, la chute, ligne 4, le bouillonnement au pied (relié à la coulée)
CASCADE_COLONNES, CASCADE_IMAGES, CASCADE_DUREE = 54, 18, 110
for r in range(5):
    for c in range(3):
        carte.animer("cascade", r * CASCADE_COLONNES + c,
                     [(r * CASCADE_COLONNES + f * 3 + c, CASCADE_DUREE) for f in range(CASCADE_IMAGES)])
# Bulle : une petite boursouflure qui attend, puis gonfle et éclate (parc.js décale chaque bulle : pas toutes à la fois)
carte.animer("bulle", 0, [(0, 2600)] + [(i, 90) for i in range(1, 11)])

# --- Formes : cascades, coulées, mares ---------------------------------------------------------
# Une cascade : (x, y) du coin haut gauche. Règle de l'utilisateur (2026-11-01) : une cascade a toujours quelque chose en
# amont et en aval (« ça doit être logique ») : un lac au-dessus d'elle, qui déborde, et en bas une coulée de 3 cases de
# large qui finit dans une mare.
# Modèle de l'utilisateur (Volcan_1-4.tmx retouché dans Tiled, 2026-11-01) : un grand lac en haut du volcan alimente
# trois cascades (à gauche, dans la vue de départ, à droite) ; quelques mares seules ailleurs.
CASCADES = [(32, 25), (16, 15), (60, 15)]  # départ, gauche, droite
cascades = [{(x + i, y + j) for i in range(3) for j in range(5)} for x, y in CASCADES]


def coulee(x, y0, y1, graine, droites=()):
    """Coulée de 3 cases de large de la ligne y0 à y1 (exclue), qui serpente doucement à partir de x ; droite (en x) sur
    les lignes <droites> (sous un pont)."""
    ecart = bruit(H, 1.6, 4, graine)
    cellules = set()
    for y in range(y0, y1):
        # Au départ (sous la cascade), la coulée reste dans l'axe, puis serpente peu à peu
        cx = x if y in droites else x + round(ecart[y] * min(1, (y - y0) / 4))
        cellules |= {(cx + i, y) for i in range(3)}
    return cellules


def lac(cascade, rx, graine):
    """Lac au-dessus d'une cascade (4 lignes environ), centré sur elle ; ses deux dernières lignes touchent le haut de la
    cascade sur toute sa largeur."""
    x, y = CASCADES[cascade]
    return mare(x + 1.5, y - 2, rx, 2.1, graine) | {(x + i, y - j) for i in range(3) for j in (1, 2)}


def mare(cx, cy, rx, ry, graine, bord=0.3):
    """Mare aux bords irréguliers : une ellipse dont le rayon varie avec l'angle (de ± <bord>, en part du rayon)."""
    r = bruit(64, bord, 2.2, graine)
    return {(x, y) for y in range(H) for x in range(L)
            if math.hypot((x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry)
            <= 1 + r[int((math.atan2(y + 0.5 - cy, x + 0.5 - cx) + math.pi) / (2 * math.pi) * 63)]}


def bandes(lignes):
    """Forme donnée ligne par ligne : {y: (x0, x1), ...}, de x0 à x1 compris (contours repris d'une carte retouchée)."""
    return {(x, y) for y, (x0, x1) in lignes.items() for x in range(x0, x1 + 1)}


# Le grand lac, tel que l'utilisateur l'a peint dans Tiled : une large nappe (lignes 9 à 18) qui s'évase vers la cascade
# de la vue de départ, avec les lacs des trois cascades à ses bouts
GRAND_LAC = bandes({9: (27, 50), 10: (24, 59), 11: (14, 66), 12: (13, 66), 13: (13, 66), 14: (13, 64), 15: (19, 58),
                    16: (22, 57), 17: (25, 56), 18: (27, 47), 19: (28, 38), 20: (29, 37), 21: (29, 37), 22: (29, 36),
                    23: (30, 36), 24: (30, 34)}) | lac(0, 4.5, 21) | lac(1, 4, 23) | lac(2, 4, 24)
# Les ponts (x de la première case, ligne du tablier), chacun sur la coulée d'une cascade, qui y est droite sur 4 lignes :
# celui de la vue de départ, et les deux que l'utilisateur a posés dans Tiled (2026-11-01) sur les coulées de gauche et de
# droite. Le tablier va d'un bord de roche à l'autre (x à x + 4) ; la coulée passe dessous (x + 1 à x + 3).
PONTS = [(31, 32), (15, 28), (59, 25)]


def sous_pont(n):
    """Lignes où la coulée passe sous le pont n : droite, sous la rambarde, le tablier, les arches et les remous."""
    return range(PONTS[n][1] - 1, PONTS[n][1] + 3)


def bouts_de_pont(n):
    """Cases de roche aux deux bouts du pont n (sous les poteaux et le tablier) : pas de lave."""
    x, y = PONTS[n]
    return {(x + i, y + j) for i in (0, 4) for j in range(-1, 3)}


# Chaque groupe : le grand lac avec ses cascades, leurs coulées et leurs mares, ou une mare seule. Un groupe tout entier
# dans une zone y est contourné par la lisière (comme les objets) ; sinon, sa lave est coupée à la lisière (le grand lac
# déborde de toutes les zones sauf la dernière : on en voit le bas dès la vue de départ, au-dessus de sa cascade).
groupes = [
    {"cascades": [0, 1, 2], "lave": GRAND_LAC
        | coulee(32, 30, 35, 1, droites=sous_pont(0))
        | bandes({35: (29, 37), 36: (29, 39), 37: (29, 39), 38: (29, 39), 39: (31, 39), 40: (33, 38), 41: (33, 38)})
        | coulee(16, 20, 41, 7, droites=sous_pont(1))
        | bandes({41: (16, 24), 42: (13, 24), 43: (6, 24), 44: (5, 24), 45: (5, 22), 46: (6, 21), 47: (6, 17),
                  48: (6, 9)})
        | coulee(60, 20, 28, 9, droites=sous_pont(2)) | mare(62.5, 31, 3.8, 5, 10)},
    {"lave": mare(72, 21, 2.6, 5.5, 15)},
    {"lave": mare(70, 47, 4.5, 3, 13)},
    {"lave": mare(44, 51.5, 4.8, 2.2, 14)},
]
for groupe in groupes:
    groupe["cascades"] = set().union(*(cascades[i] for i in groupe.get("cascades", [])))
    groupe["lave"] -= groupe["cascades"] | set().union(*(bouts_de_pont(n) for n in range(len(PONTS))))

# Bordure d'origine (carte entière) : des rochers serrés tout autour, 2 à 3 cases à l'œil (règle de l'utilisateur) ;
# EPAISSEUR comme la plage (un gros rocher demande deux cases de profondeur)
EPAISSEUR = 4.4
haut, gauche, droite, bas = (bruit(n, 0.9, 5, g) for n, g in ((L, 41), (H, 42), (H, 43), (L, 44)))
bordure_carte = {(x, y) for y in range(H) for x in range(L)
                 if y < EPAISSEUR + haut[x] or x < EPAISSEUR + gauche[y] or x >= L - EPAISSEUR + droite[y]
                 or y >= H - EPAISSEUR + bas[x]}
carte.lisiere = set(bordure_carte)
cascades_toutes = set().union(*cascades)
lave = nettoyer(set().union(*(g["lave"] for g in groupes)) - bordure_carte)
for groupe in groupes:
    groupe["lave"] &= lave
    groupe["cellules"] = groupe["lave"] | groupe["cascades"]
    # Lacs, cascades, coulées et mares d'un seul tenant : chaque coulée finit bien dans sa mare
    if groupe["cascades"] and len(morceaux_de(groupe["cellules"])) != 1:
        sys.exit("Le grand lac, ses cascades, leurs coulées et leurs mares ne sont pas d'un seul tenant")


def forme_de(lave_, cascades_):
    """Forme des bords de la lave : la lave et les cascades (la source et la coulée se raccordent à la cascade)."""
    return lave_ | cascades_


# --- Sol : roche, lave et bords, tiré case par case (le même dans toutes les tailles) ----------
def sol_de(lave_, cascades_):
    forme = forme_de(lave_, cascades_)
    pres = voisins8(voisins8(lave_))  # à deux cases de la lave : la roche est parfois fendue de lave
    sol_ = [[0] * L for _ in range(H)]
    for y in range(H):
        for x in range(L):
            r = tirage(x, y, 3)
            if (x, y) in lave_:
                i = bordure(forme, x, y, *LAVE[1:])
                sol_[y][x] = t("volcan", i if i is not None else
                               (r.choice(LAVE_CROUTE) if r.random() < 0.07 else LAVE_UNIE))
            elif (x, y) in pres and r.random() < 0.22:
                sol_[y][x] = t("volcan", r.choice(SOL_BRULANT))
            else:
                sol_[y][x] = t("volcan", r.choice(SOL_LISSE) if r.random() < 0.72 else r.choice(SOL_CRAQUELE))
    return sol_


# --- Objets : cascades, pont, arbres, rochers, plantes ---------------------------------------
def libre_sol(x, y):
    return (x, y) not in lave and (x, y) not in cascades_toutes


carte.libre_sol = libre_sol
for x, y in voisins8(cascades_toutes):  # rien de posé contre une cascade
    carte.occupe_decors.add((x, y))
for x, y in CASCADES:  # ni dans le lac juste au-dessus : rien ne barre l'écoulement
    carte.occupe_decors |= {(x + i, y - j) for i in range(-1, 4) for j in (1, 2, 3)}

# Cascades : toutes leurs cases bloquées
for (x, y), cellules in zip(CASCADES, cascades):
    tuiles = [("decors", x + i, y + j, t("cascade", j * CASCADE_COLONNES + i), True) for j in range(5) for i in range(3)]
    for _, cx, cy, tuile, _ in tuiles:
        decors[cy][cx] = tuile
    objets.append({"tuiles": tuiles, "cascade": cellules})

# Ponts de pierre sur les coulées (Volcano_Bridge, colonnes 3 à 7 : les poteaux sont à cheval sur deux cases) : rambarde,
# tablier où l'on marche (ses cases de lave débloquées), arches, bouillonnement au pied des piles. Rien de posé devant
# les deux bouts du tablier (on doit pouvoir y monter).
for x0, y0 in PONTS:
    if any((x0 + i, y0) not in lave for i in (1, 2, 3)) or any((x0 + i, y0) in lave for i in (0, 4)):
        sys.exit(f"Pont {(x0, y0)} : la coulée n'est pas sous son tablier, ou un bout est dans la lave")
    # Bloquées : la lave sous la rambarde, les arches et les remous ; aux deux bouts, les poteaux (à cheval sur deux cases)
    # laissent passer : un bout de pont dans la zone d'une taille plus petite n'y reprend aucune case libre
    pont = [("decors", x0 + i, y0 - 1 + j, t("pont", j * 8 + 3 + i), j != 1 and 0 < i < 4)
            for j in range(4) for i in range(5) if j != 1 or 0 < i < 4]  # tuiles 11 et 15 vides
    for _, x, y, tuile, _ in pont:
        carte.occupe_decors.add((x, y))
        decors[y][x] = tuile
    carte.occupe_decors |= {(x0 - 1, y0 + j) for j in (-1, 0, 1)} | {(x0 + 5, y0 + j) for j in (-1, 0, 1)}
    objets.append({"tuiles": pont, "debloque": {(x0 + i, y0) for i in range(5)}})


def dans_depart(x, y, marge=0):
    dx, dy, dl, dh = DEPART
    return dx - marge <= x < dx + dl + marge and dy - marge <= y < dy + dh + marge


def loin_de_la_lave(x, y, largeur, hauteur):
    """Le bloc et les cases autour, sans lave ni cascade (un feuillage ne déborde pas sur la lave)."""
    return not any(libre_sol(x + i, y + j) is False for i in range(-1, largeur + 1) for j in range(-1, hauteur + 1))


# Arbres de braise : le petit (2 × 3) et le grand (3 × 3) ; feuillage dans « Devant », tronc bloqué
petit_arbre = lambda x, y: loin_de_la_lave(x, y, 2, 3) and poser("plantes", 0, 0, 2, 3, x, y, rang_devant=2,
                                                                  bloque_lignes=(2,), bloque_cols=(0,), marge=1)
grand_arbre = lambda x, y: loin_de_la_lave(x, y, 3, 3) and poser("plantes", 2, 0, 3, 3, x, y, rang_devant=2,
                                                                  bloque_lignes=(2,), bloque_cols=(1,), marge=1)
essayer(10, lambda x, y: not dans_depart(x + 1, y + 2, 1) and grand_arbre(x, y))
essayer(10, lambda x, y: not dans_depart(x, y + 2, 1) and petit_arbre(x, y))
essayer(1, lambda x, y: dans_depart(x + 1, y + 2) and grand_arbre(x, y))
essayer(2, lambda x, y: dans_depart(x, y + 2) and petit_arbre(x, y))

# Gros rochers (2 × 2) : amas, butte, tour de basalte ; colonnes de basalte et cristaux (1 × 2)
GROS_ROCHERS = [(1, 5), (0, 7), (2, 7), (4, 7)]  # (colonne, ligne du coin haut gauche) dans Volcano_Rocks
HAUTS_ROCHERS = [(0, 1), (1, 1), (2, 1), (4, 1), (0, 5), (3, 5)]
PETITS_ROCHERS = [0, 1, 2, 3, 22, 23]
essayer(12, lambda x, y: not dans_depart(x, y, 1)
        and poser("rochers", *rng.choice(GROS_ROCHERS), 2, 2, x, y, bloque_lignes=(1,), marge=1))
essayer(2, lambda x, y: dans_depart(x, y) and poser("rochers", *rng.choice(GROS_ROCHERS), 2, 2, x, y, bloque_lignes=(1,), marge=1))
essayer(14, lambda x, y: poser("rochers", *rng.choice(HAUTS_ROCHERS), 1, 2, x, y, bloque_lignes=(1,), marge=1))


def objet(nom, liste, n, zone=lambda x, y: True, bloquant=False, marge=0, sur=None):
    """n objets d'une case (tuiles de <liste>) ; sur(x, y) : où les poser (sinon, la roche libre)."""
    def un(x, y):
        if not zone(x, y):
            return False
        if sur is not None:
            if not sur(x, y) or (x, y) in carte.occupe_decors:
                return False
            decors[y][x] = t(nom, rng.choice(liste))
            carte.occupe_decors.add((x, y))
            objets.append({"tuiles": [("decors", x, y, decors[y][x], bloquant)]})
            return True
        return poser(nom, 0, 0, 1, 1, x, y, verifier=True, marge=marge) and remplacer(x, y)

    def remplacer(x, y):
        decors[y][x] = t(nom, rng.choice(liste))
        carte.bloque[y][x] = bloquant
        objets[-1]["tuiles"] = [("decors", x, y, decors[y][x], bloquant)]
        return True
    essayer(n, un)


# Petits rochers (bloqués), buissons de braise et fleurs de cendre (on y marche)
objet("rochers", PETITS_ROCHERS, 22, lambda x, y: not dans_depart(x, y), bloquant=True, marge=1)
objet("rochers", PETITS_ROCHERS, 4, lambda x, y: dans_depart(x, y), bloquant=True, marge=1)
objet("plantes", [18, 19, 20, 21, 22, 24, 25, 26, 30, 31, 32], 40, marge=1)
objet("plantes", [36, 37, 38, 39, 40, 41], 40)


# Rochers dans la lave (au milieu, loin des bords), avec leur lueur rouge
def lave_profonde(x, y):
    return all((x + dx, y + dy) in lave for dx in (-1, 0, 1) for dy in (-1, 0, 1))


objet("rochers", [4, 5, 6, 7], 14, sur=lave_profonde)
essayer(4, lambda x, y: all(lave_profonde(x + i, y + j) and (x + i, y + j) not in carte.occupe_decors
                            for i in (0, 1) for j in (0, 1))
        and poser("rochers", 5, 5, 2, 2, x, y, bloque_lignes=(0, 1), verifier=False))

# --- Carte de chaque taille : la zone, un champ de rochers tout autour -----------------------
# Lisière irrégulière : 0 à 2 cases dans la zone (moins que l'écart entre deux zones)
LISIERE = [[max(0, min(2, round(0.8 + v))) for v in bruit(max(L, H), 1.3, 2.5, graine)] for graine in (51, 52, 53, 54)]


def dans_la_zone(groupe, zone):
    return all(dans(zone, x, y) for x, y in groupe["cellules"])


def cache_de(zone):
    """Cases cachées d'une taille : la bordure de la carte, et tout ce qui est hors de la zone (lisière irrégulière), sauf
    les objets et les groupes de lave (cascade, coulée, mare) qui tiennent dans la zone (la lisière les contourne)."""
    zx, zy, zl, zh = zone
    haut_, bas_, gauche_, droite_ = LISIERE
    cache = bordure_carte | {(x, y) for y in range(H) for x in range(L)
                             if not (zx + gauche_[y] <= x < zx + zl - droite_[y] and zy + haut_[x] <= y < zy + zh - bas_[x])}
    for objet_ in objets:
        cellules = {(x, y) for _, x, y, _, _ in objet_["tuiles"]}
        if all(dans(zone, x, y) for x, y in cellules) and not cellules & bordure_carte:
            cache -= cellules
    for groupe in groupes:
        if dans_la_zone(groupe, zone):
            cache -= {c for c in voisins8(groupe["cellules"]) if dans(zone, *c)} - bordure_carte
    return cache


def planter_rochers(cache, lave_, grilles, bloque_):
    """Champ de rochers en quinconce : un gros rocher (2 × 2) dont les quatre cases sont de la roche cachée."""
    for y in range(-1, H, 2):
        for x in range(-1 + (y // 2) % 2, L, 2):
            cases = [(x + i, y + j) for i in (0, 1) for j in (0, 1)]
            if all(not dedans(*c) or (c in cache and c not in lave_) for c in cases):
                gauche_, haut_ = tirage(x, y, 4).choice(GROS_ROCHERS)
                ecrire(grilles, bloque_, bloc("rochers", gauche_, haut_, 2, 2, x, y, bloque_lignes=(0, 1)))


def petit_rocher(x, y):
    return t("rochers", tirage(x, y, 2).choice(PETITS_ROCHERS))


def planter_bordure(cache, grilles, bloque_):
    """Le reste de ce qui est caché : un petit rocher sur chaque case, bloquée. Le bord de la bordure d'origine dans la
    zone, sans rocher, reste de la roche où l'on marche."""
    decors_, devant_ = grilles["decors"], grilles["devant"]
    for x, y in cache:
        if (x, y) in bordure_carte and not decors_[y][x] and not devant_[y][x]:
            continue
        bloque_[y][x] = True
        if not decors_[y][x] and not devant_[y][x]:
            decors_[y][x] = petit_rocher(x, y)


def poser_objets(cache, libres_avant, grilles, bloque_):
    """Objets du parc entier, s'ils sont tout entiers dans la zone, sans chevaucher ce qui est caché ni bloquer une case
    libre de la taille d'avant. Le pont débloque son tablier (la lave dessous). Rend les cascades posées."""
    posees = set()
    for objet_ in objets:
        tuiles = objet_["tuiles"]
        if any((x, y) in cache or grilles[calque][y][x] or (bloquee and (x, y) in libres_avant)
               for calque, x, y, _, bloquee in tuiles):
            continue
        ecrire(grilles, bloque_, tuiles)
        for x, y in objet_.get("debloque", ()):
            bloque_[y][x] = False
        posees |= objet_.get("cascade", set())
    return posees


def taille(zone, libres_avant):
    """Carte d'une taille : sol, décors, feuillages, bulles, cases bloquées et cases libres. libres_avant : cases libres de
    la taille d'avant, qui doivent le rester (un agrandissement ne reprend rien)."""
    cache = cache_de(zone)
    # Cascades montrées : tout entières visibles
    cascades_ = set().union(*(c for c in cascades if not c & cache))
    # La lave coupée à la lisière (sans morceau de moins de 2 × 2)
    lave_ = nettoyer(lave - cache)
    decors_ = [[0] * L for _ in range(H)]
    devant_ = [[0] * L for _ in range(H)]
    bulles_ = [[0] * L for _ in range(H)]
    bloque_ = [[(x, y) in lave_ for x in range(L)] for y in range(H)]
    grilles = {"decors": decors_, "devant": devant_}
    # La lave retirée au bord de la coupe (le nettoyage) : un rocher, bloqué (elle redevient de la lave plus tard : une case
    # libre ne doit pas être reprise)
    for x, y in lave - lave_ - cache:
        bloque_[y][x] = True
        decors_[y][x] = petit_rocher(x, y)
    planter_rochers(cache, lave_, grilles, bloque_)
    planter_bordure(cache, grilles, bloque_)
    posees = poser_objets(cache, libres_avant, grilles, bloque_)
    if posees != cascades_:
        sys.exit(f"Zone {zone} : cascade visible mais pas posée : {sorted(cascades_ - posees)[:5]}")
    for x, y in CASCADES:
        amont = all((x + i, y - j) in lave_ and not decors_[y - j][x + i] for i in range(3) for j in (1, 2))
        aval = all((x + i, y + 5) in lave_ for i in range(3))
        if (x, y) in cascades_ and not (amont and aval):
            sys.exit(f"Zone {zone} : cascade {(x, y)} sans lac au-dessus ou sans coulée en dessous")
    sol_ = sol_de(lave_, cascades_)
    # Bulles sur la lave libre, loin des bords (calque « Bulles », hors des décors : la lave reste libre pour parc.js)
    forme = forme_de(lave_, cascades_)
    for x, y in lave_:
        if bordure(forme, x, y, *LAVE[1:]) is None and not decors_[y][x] and tirage(x, y, 5).random() < 0.15:
            bulles_[y][x] = t("bulle", 0)
    # Poches de roche enfermées (entre la lisière, la lave et les objets) : des rochers, personne n'y va
    gardees = carte.boucher_poches(zone, decors_, bloque_, petit_rocher)
    perdues = libres_avant - gardees
    if perdues:
        sys.exit(f"Zone {zone} : cases libres de la taille d'avant bloquées : {sorted(perdues)[:10]}")
    # parc.js prend toute case bloquée sans objet pour de la lave libre : il n'y en a pas d'autre
    nues = [(x, y) for y in range(H) for x in range(L)
            if bloque_[y][x] and (x, y) not in lave_ and not decors_[y][x] and not devant_[y][x]]
    if nues:
        sys.exit(f"Zone {zone} : cases bloquées sans décor hors de la lave : {nues[:10]}")
    return sol_, decors_, devant_, bulles_, bloque_, gardees, lave_


# --- Écriture des .tmx -----------------------------------------------------------------------
APERCUS = sys.argv[3] if len(sys.argv) > 3 else SORTIE
libres = set()
for n, zone in enumerate(ZONES, 1):
    sol_, decors_, devant_, bulles_, bloque_, libres, lave_ = taille(zone, libres)
    carte.ecrire_carte(SORTIE, f"Volcan_1-{n}.tmx", DEPART, zone, sol_, decors_, devant_, bloque_, [("Bulles", bulles_)])
    carte.apercu(APERCUS, f"volcan-{n}", (sol_, decors_, bulles_, devant_), bloque_)
    zx, zy, zl, zh = zone
    cases = [(x, y) for y in range(zy, zy + zh) for x in range(zx, zx + zl)]
    lave_zone = sum(1 for c in cases if c in lave_ and not decors_[c[1]][c[0]])
    print(f"Volcan_1-{n}.tmx : zone {zl} × {zh}, lave {100 * sum(1 for c in cases if c in lave_) / len(cases):.1f} % "
          f"(libre : {lave_zone} cases), {len(libres)} cases libres ({len(libres) / (20 * n):.1f} par Pokémon)")
