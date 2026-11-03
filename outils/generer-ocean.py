# Génère les cartes de l'océan pour Tiled (Ocean_1-1.tmx à Ocean_1-4.tmx, une par taille achetée) avec le pack Cute
# Fantasy : la pleine mer, sans plage, trois îlots d'herbe bordés de pierre (on y marche), des rochers animés dans l'eau,
# et le calque « Collisions » qui va avec. Tirages fixés : relancé sans changement, il redonne les mêmes cartes.
# Choix de l'utilisateur (2026-11-03) : un océan, seuls les Pokémon qui nagent peuvent y vivre (ParkLandscapes) ; trois
# îlots seulement, aucun dans la vue de départ (toute en eau), pas bloqués (les nageurs y sortent de l'eau) ; pas de remous
# d'écume ; ce qui n'est pas encore acheté est caché par un récif de rochers serrés, qui fait aussi la bordure de la carte
# entière.
# Le bord des îlots (Water_Stone_Tile_1) passe au milieu des cases : une case prend sa tuile d'après ses quatre coins ;
# avec trois coins d'herbe ou plus, on y marche (type végétation, règle de carte-parc.py), sinon c'est de l'eau.
# Ensuite, pour chacune : python outils/carte-parc.py <Ocean_1-1.tmx>. Aperçus (ocean-1.png, ocean-1_collisions.png...)
# dans le dossier de sortie, ou dans le troisième. Moteur commun avec les autres parcs : parc_carte.py.
# Usage (Python 3 avec Pillow) :
#   python outils/generer-ocean.py <dossier Cute_Fantasy du pack> <dossier de sortie> [dossier des aperçus]
import math
import sys
from parc_carte import Carte, bruit, dans, ecrire, morceaux_de, nettoyer, relies, tirage, voisins8

PACK, SORTIE = sys.argv[1], sys.argv[2]
L, H = 80, 60
# Zone de chaque taille (x, y, largeur, hauteur, en tuiles) : 20, 40, 60 puis 80 Pokémon (ParkService), comme le volcan
DEPART = (28, 20, 24, 20)
ZONES = [DEPART, (20, 15, 40, 30), (12, 10, 56, 40), (0, 0, L, H)]
carte = Carte(PACK, L, H, 23)
rng, jeu, t, dedans = carte.rng, carte.jeu, carte.t, carte.dedans
bloc, objets = carte.bloc, carte.objets

# --- Jeux de tuiles (incorporés à la carte) -------------------------------------------------
jeu("eau_unie", "Tiles/Water/Water_Middle.png")
jeu("eau_details", "Tiles/Water/Water_Decoration.png")
jeu("rivage", "Tiles/Water/Water_Stone_Tile_1.png")
jeu("herbe_unie", "Tiles/Grass/Grass_1_Middle.png")
jeu("herbe", "Tiles/Grass/Grass_Tiles_1.png")
jeu("fleurs", "Outdoor decoration/Flowers.png")
jeu("decors", "Outdoor decoration/Outdoor_Decor.png")
# Rochers dans l'eau, animés (8 images d'une tuile, le remous autour) : dôme, pointu, anguleux, plat, amas
ROCHERS_ANIMES = ["Rock_10", "Rock_15", "Rock_3", "Rock_4", "Rock_5", "Rock_9"]
for nom in ROCHERS_ANIMES:
    jeu(nom, f"Outdoor decoration/Outdoor_Decor_Animations/Water_Decor_Animations/Water_Rocks/{nom}_Water_Anim.png")
    carte.animer(nom, 0, [(i, 150) for i in range(8)])

# Bord des îlots : tuile selon les coins d'herbe (NO, NE, SO, SE), comme l'herbe du désert ; None : toute herbe
RIVAGE = {(1, 1, 1, 1): None, (0, 0, 0, 1): 9, (0, 0, 1, 0): 10, (0, 1, 0, 0): 12, (1, 0, 0, 0): 13,
          (1, 1, 1, 0): 0, (1, 1, 0, 0): 1, (1, 1, 0, 1): 2, (1, 0, 1, 0): 3, (0, 1, 0, 1): 5, (1, 0, 1, 1): 6,
          (0, 0, 1, 1): 7, (0, 1, 1, 1): 8, (1, 0, 0, 1): 9, (0, 1, 1, 0): 10}  # (deux coins opposés : un seul gardé)
GROS_ROCHERS = [(4, 11), (6, 11), (4, 13)]  # récif : gros rochers de 2 × 2 d'Outdoor_Decor (colonne, ligne du coin)
PETITS_RECIFS = [44, 53, 62, 42, 61]  # rochers d'un seul morceau, comme ceux de la plage
FLEURS = [r * 10 + c for r in range(10) for c in range(5)]

# Bordure d'origine (carte entière) : un récif tout autour, 2 à 3 cases à l'œil (règle de l'utilisateur), même
# EPAISSEUR que les autres parcs
EPAISSEUR = 4.4
haut, gauche, droite, bas = (bruit(n, 0.9, 5, g) for n, g in ((L, 41), (H, 42), (H, 43), (L, 44)))
bordure_carte = {(x, y) for y in range(H) for x in range(L)
                 if y < EPAISSEUR + haut[x] or x < EPAISSEUR + gauche[y] or x >= L - EPAISSEUR + droite[y]
                 or y >= H - EPAISSEUR + bas[x]}
carte.lisiere = set(bordure_carte)


# --- Îlots ----------------------------------------------------------------------------------
def coins_de(forme):
    """Coins d'herbe (NO, NE, SO, SE) de chaque case de la forme : un sommet est de l'herbe si les quatre cases autour sont
    dans la forme (le bord passe au milieu des cases)."""
    def coin(x, y):
        return all((x + i, y + j) in forme for i in (-1, 0) for j in (-1, 0))
    return {(x, y): (coin(x, y), coin(x + 1, y), coin(x, y + 1), coin(x + 1, y + 1)) for x, y in forme}


def terre_de(forme):
    """Cases où l'on marche : trois coins d'herbe au moins."""
    return {c for c, coins in coins_de(forme).items() if sum(coins) >= 3}


def ilot(cx, cy, rx, ry, graine):
    """Îlot arrondi centré en (cx, cy) : un ovale aux bords à peine irréguliers, et un ou deux lobes qui le débordent."""
    r = tirage(graine, 0, 8)
    ovales = [(cx, cy, rx, ry)]
    for _ in range(r.choice((1, 2))):
        a = r.uniform(0, 2 * math.pi)
        ovales.append((cx + math.cos(a) * rx * 0.7, cy + math.sin(a) * ry * 0.7, rx * r.uniform(0.45, 0.6),
                       ry * r.uniform(0.5, 0.7)))
    cellules = set()
    for n, (ox, oy, orx, ory) in enumerate(ovales):
        b = bruit(64, 0.12, 5, graine * 10 + n)
        for y in range(int(oy - ory) - 2, int(oy + ory) + 3):
            for x in range(int(ox - orx) - 2, int(ox + orx) + 3):
                u, v = (x + 0.5 - ox) / orx, (y + 0.5 - oy) / ory
                k = 1 + b[int((math.atan2(v, u) + math.pi) / (2 * math.pi) * 63)]
                if u * u + v * v <= k * k:
                    cellules.add((x, y))
    return nettoyer(cellules)


def proches(cellules, n):
    for _ in range(n):
        cellules = voisins8(cellules)
    return cellules


# Trois îlots (centre, rayons) placés à la main, hors de la vue de départ (toute en eau) : à gauche (taille 2), en haut à
# droite et en bas (taille 3). Un îlot qui déborde de la zone est coupé à la lisière (il se dévoile en entier à
# l'agrandissement suivant). 3 cases d'eau au moins entre deux îlots (on nage autour), 2 entre un îlot et la bordure.
ILOTS_PLACES = [(21.5, 30, 3.4, 4.8), (61, 14, 4.6, 3.2), (45, 48.5, 5, 3)]
ILOTS = []
for i, place in enumerate(ILOTS_PLACES):
    # Lobes tirés de nouveau (autre graine) tant que l'îlot touche un voisin ou la bordure
    for essai in range(30):
        forme = ilot(*place, 61 + i + 1000 * essai)
        if not proches(forme, 3) & set().union(set(), *ILOTS) and not proches(forme, 2) & bordure_carte:
            ILOTS.append(forme)
            break
    else:
        sys.exit(f"Îlot {place} : trop près d'un autre ou de la bordure")
TOUS_ILOTS = set().union(*ILOTS)

# --- Objets : rochers animés et fleurs -----------------------------------------------------------
pleine_mer = {(x, y) for y in range(H) for x in range(L)} - proches(TOUS_ILOTS, 2) - bordure_carte
pres_de_la_mer = set()


def rocher(x, y):
    if (x, y) not in pleine_mer or proches({(x, y)}, 2) & pres_de_la_mer:
        return False
    nom = rng.choice(ROCHERS_ANIMES)
    objets.append({"tuiles": [("decors", x, y, t(nom, 0), True)], "mer": True})
    pres_de_la_mer.add((x, y))
    return True


carte.essayer(70, rocher)
# Trois rochers sur dix retirés (demande de l'utilisateur, 2026-11-03), dans chaque anneau entre deux zones (la vue de
# départ comprise) : les autres gardent leur place
for n, zone in enumerate(ZONES):
    rochers = [o for o in objets if o.get("mer") and dans(zone, *o["tuiles"][0][1:3])
               and not (n and dans(ZONES[n - 1], *o["tuiles"][0][1:3]))]
    for o in sorted(rochers, key=lambda o: tirage(o["tuiles"][0][1], o["tuiles"][0][2], 7).random())[:round(len(rochers) * 0.3)]:
        objets.remove(o)
# Fleurs sur l'herbe pleine (on y marche), une sur huit environ
pleine = {c for c, coins in coins_de(TOUS_ILOTS).items() if all(coins)}
for x, y in sorted(pleine):
    if tirage(x, y, 5).random() < 0.12:
        objets.append({"tuiles": [("decors", x, y, t("fleurs", tirage(x, y, 6).choice(FLEURS)), False)], "herbe": True})


# --- Carte de chaque taille : la zone, un récif tout autour -----------------------------------
# Lisière irrégulière : 0 à 2 cases dans la zone (moins que l'écart entre deux zones)
LISIERE = [[max(0, min(2, round(0.8 + v))) for v in bruit(max(L, H), 1.3, 2.5, graine)] for graine in (51, 52, 53, 54)]


def cache_de(zone):
    """Cases cachées d'une taille : la bordure de la carte, et tout ce qui est hors de la zone (lisière irrégulière), sauf
    les îlots qui tiennent dans la zone (la lisière les contourne) ; un îlot qui la déborde est coupé."""
    zx, zy, zl, zh = zone
    haut_, bas_, gauche_, droite_ = LISIERE
    cache = bordure_carte | {(x, y) for y in range(H) for x in range(L)
                             if not (zx + gauche_[y] <= x < zx + zl - droite_[y] and zy + haut_[x] <= y < zy + zh - bas_[x])}
    for ilot_ in ILOTS:
        if all(dans(zone, x, y) for x, y in ilot_):
            cache -= {c for c in voisins8(ilot_) if dans(zone, *c)} - bordure_carte
    return cache


def taille(zone, libres_avant):
    """Carte d'une taille : sol, décors, feuillages, cases bloquées et cases libres (la terre des îlots). libres_avant :
    cases libres de la taille d'avant, qui doivent le rester (un agrandissement ne reprend rien)."""
    cache = cache_de(zone)
    # Îlots visibles : coupés à la lisière, sans les bouts où l'on ne marche presque pas
    forme = set()
    for ilot_ in ILOTS:
        for morceau in morceaux_de(nettoyer(ilot_ - cache)):
            if len(terre_de(morceau)) >= 4:
                forme |= morceau
    coins = coins_de(forme)
    terre = {c for c, k in coins.items() if sum(k) >= 3}
    sol_ = [[0] * L for _ in range(H)]
    decors_ = [[0] * L for _ in range(H)]
    devant_ = [[0] * L for _ in range(H)]
    bloque_ = [[(x, y) not in terre for x in range(L)] for y in range(H)]
    for y in range(H):
        for x in range(L):
            r = tirage(x, y, 3)
            if (x, y) in forme:
                i = RIVAGE[tuple(int(k) for k in coins[(x, y)])]
                sol_[y][x] = (t("rivage", i) if i is not None
                              else t("herbe", r.choice((149, 150, 151))) if r.random() < 0.12 else t("herbe_unie", 0))
            else:
                sol_[y][x] = t("eau_details", r.randrange(3)) if r.random() < 0.04 else t("eau_unie", 0)
    grilles = {"decors": decors_, "devant": devant_}
    # Récif en quinconce dans la mer cachée : un gros rocher (2 × 2) dont les quatre cases sont cachées, puis un petit
    # rocher sur chaque case cachée qui reste (aucune case bloquée sans décor : parc.js la prendrait pour de l'eau libre)
    for y in range(-1, H, 2):
        for x in range(-1 + (y // 2) % 2, L, 2):
            cases = [(x + i, y + j) for i in (0, 1) for j in (0, 1)]
            if all(not dedans(*c) or c in cache for c in cases):
                gauche_, haut_ = tirage(x, y, 4).choice(GROS_ROCHERS)
                ecrire(grilles, bloque_, bloc("decors", gauche_, haut_, 2, 2, x, y, bloque_lignes=(0, 1)))
    for x, y in cache:
        bloque_[y][x] = True
        if not decors_[y][x]:
            decors_[y][x] = t("decors", tirage(x, y, 2).choice(PETITS_RECIFS))
    # Rochers animés et fleurs tout entiers dans la zone, hors de ce qui est caché
    for objet_ in objets:
        tuiles = objet_["tuiles"]
        if any((x, y) in cache or not dans(zone, x, y) or grilles[calque][y][x] for calque, x, y, _, _ in tuiles):
            continue
        if objet_.get("herbe") and not all(all(coins.get((x, y), ())) for _, x, y, _, _ in tuiles):
            continue
        ecrire(grilles, bloque_, tuiles)
    perdues = libres_avant - terre
    if perdues:
        sys.exit(f"Zone {zone} : cases libres de la taille d'avant bloquées : {sorted(perdues)[:10]}")
    # Toute la terre doit être reliée par la mer libre (sans rocher) : les nageurs vont d'un îlot à l'autre (sans terre,
    # parc.js part de l'eau au centre de la vue de départ)
    zx, zy, zl, zh = zone
    mer = {(x, y) for y in range(zy, zy + zh) for x in range(zx, zx + zl) if bloque_[y][x] and not decors_[y][x]}
    dx, dy, dl, dh = DEPART
    if not terre <= relies(terre | mer) or (dx + dl // 2, dy + dh // 2) not in relies(terre | mer):
        sys.exit(f"Zone {zone} : un îlot, ou le centre de la vue de départ, n'est pas relié au reste par la mer libre")
    return sol_, decors_, devant_, bloque_, terre, mer


# --- Écriture des .tmx -----------------------------------------------------------------------
APERCUS = sys.argv[3] if len(sys.argv) > 3 else SORTIE
libres = set()
for n, zone in enumerate(ZONES, 1):
    sol_, decors_, devant_, bloque_, libres, mer = taille(zone, libres)
    carte.ecrire_carte(SORTIE, f"Ocean_1-{n}.tmx", DEPART, zone, sol_, decors_, devant_, bloque_)
    carte.apercu(APERCUS, f"ocean-{n}", (sol_, decors_, devant_), bloque_)
    zx, zy, zl, zh = zone
    total = zl * zh
    print(f"Ocean_1-{n}.tmx : zone {zl} × {zh}, terre {100 * len(libres) / total:.1f} %, eau {100 - 100 * len(libres) / total:.1f} % "
          f"(libre : {len(mer)} cases), {len(libres)} cases de terre, {(len(libres) + len(mer)) / (20 * n):.1f} cases "
          f"libres par Pokémon avec l'eau")
