# Génère les cartes de la plage pour Tiled (Plage_1-1.tmx à Plage_1-4.tmx, une par taille achetée) avec le pack Cute
# Fantasy (+ Desert pour les palmiers) : une côte, le sable au nord et la mer au sud, bords automatiques, criques, pointes,
# îlots, ponton et barque, palmiers, rochers, et le calque « Collisions » qui va avec. Tirages fixés : relancé sans
# changement, il redonne les mêmes cartes.
# Environ 65 % de mer et 35 % de sable dans la zone des tailles 2 à 4 (choix de l'utilisateur : les types Eau y sont
# heureux, Feu, Roche, Acier, Électrik, Sol et Fée malheureux ; voir ParkHabitats) : la côte passe vers le tiers haut de
# chaque zone ; la taille 1 est la vue de départ, moitié plage moitié mer (52,5 % de mer). La mer est bloquée : ~8 cases
# de sable libres par Pokémon aux tailles 1 et 2 (zones choisies par l'utilisateur dans Tiled le 2026-11-01, les types Eau
# nagent en plus dans la mer). Ce qui n'est pas encore acheté est caché par une cocoteraie dense côté plage et des
# rochers serrés côté mer.
# Le bord du sable (écume) est bloqué : un Pokémon n'y aurait pas les pieds au sec.
# Ensuite, pour chacune : python outils/carte-parc.py <Plage_1-1.tmx>. Aperçus (plage-1.png, plage-1_collisions.png...)
# dans le dossier de sortie, ou dans le troisième. Moteur commun avec le parc 1 : parc_carte.py.
# Usage (Python 3 avec Pillow) :
#   python outils/generer-plage.py <dossier Cute_Fantasy du pack> <dossier de sortie> [dossier des aperçus]
import sys
from parc_carte import Carte, bordure, bruit, dans, ecrire, morceaux_de, nettoyer, plus_grand_morceau, tirage, voisins8

PACK, SORTIE = sys.argv[1], sys.argv[2]
L, H = 80, 60
# Zone de chaque taille (x, y, largeur, hauteur, en tuiles) : 20, 40, 60 puis 80 Pokémon (ParkService). La côte est vers
# la ligne 22 : les zones 2 et 3 en ont environ un tiers au-dessus (le sable), deux tiers au-dessous (la mer). La zone 1
# est la vue de départ (mêmes tailles que le parc 1 : 24 × 20, puis 40 × 31 et 56 × 43).
DEPART = (28, 10, 24, 20)  # vue de départ (même hauteur que le parc 1 : même zoom), moitié plage, moitié mer
ZONES = [DEPART, (20, 10, 40, 31), (12, 7, 56, 43), (0, 0, L, H)]
COTE = 20
carte = Carte(PACK, L, H, 11)
rng, jeu, t, dedans, tache = carte.rng, carte.jeu, carte.t, carte.dedans, carte.tache
bloc, poser, essayer = carte.bloc, carte.poser, carte.essayer
decors, devant, objets = carte.decors, carte.devant, carte.objets

# --- Jeux de tuiles (incorporés à la carte) -------------------------------------------------
jeu("plage", "Tiles/Beach/Beach_Tiles.png")
jeu("sable_details", "Tiles/Beach/Beach_Decor_Tiles.png")
jeu("eau_unie", "Tiles/Water/Water_Middle.png")
jeu("eau_details", "Tiles/Water/Water_Decoration.png")
jeu("decors", "Outdoor decoration/Outdoor_Decor.png")
jeu("ponton", "Tiles/Wooden_Deck_Tiles.png")
jeu("barque", "Outdoor decoration/Boat.png")
jeu("palmier", "../Cute_Fantasy_Desert/Props/Palm_Tree_2.png")
jeu("grand_palmier", "../Cute_Fantasy_Desert/Props/Palm_Tree_1.png")
jeu("palmes", "../Cute_Fantasy_Desert/Props/Fallen_Palm_Leaves.png")
jeu("herbes_dune", "../Cute_Fantasy_Desert/Props/Desert_Grass_Props.png")
jeu("rochers_sable", "../Cute_Fantasy_Desert/Props/Desert_Rocks.png")

# Bords du sable (le sable est la forme, la mer autour) : coins HG H HD G D BG B BD, coins rentrants SE SO NE NO
PLAGE = ("plage", (0, 1, 2, 30, 32, 60, 61, 62), (3, 4, 33, 34))

# --- Formes : côte, criques, pointes, îlots -------------------------------------------------
# Côte en grandes courbes (anses et pointes arrondies), avec de petites vagues par-dessus
grandes, petites = bruit(L, 3.2, 9, 31), bruit(L, 0.8, 3, 32)
sable = {(x, y) for x in range(L) for y in range(H) if y < COTE + grandes[x] + petites[x]}
sable -= tache(-2, 21, 7, 9) | tache(82, 20, 7, 8)  # anses aux deux bouts (moins de sable dans la dernière taille)
# Îlots (bloqués : on n'y va pas), chacun tout entier dans une zone et hors de la précédente
ILOTS = [tache(51, 33, 3, 2.5), tache(16, 38, 3, 2.6), tache(62, 43, 3.4, 2.6), tache(38, 46, 3, 2.4),
         tache(9, 50, 3, 2.5), tache(70, 52, 3.2, 2.5), tache(26, 53, 3, 2.4), tache(52, 52, 3.2, 2.4)]
for ilot in ILOTS:
    sable |= ilot


def hors_carte(cellules):
    """Cases hors de la carte qui prolongent la forme (le sable continue au-delà du bord : pas de bordure le long du bord)."""
    return {(x + dx, y + dy) for x, y in cellules for dx in (-1, 0, 1) for dy in (-1, 0, 1)
            if not dedans(x + dx, y + dy) and (min(max(x + dx, 0), L - 1), min(max(y + dy, 0), H - 1)) in cellules}


def nettoyer_cote(sable_):
    """Sable et mer sans morceau de moins de 2 × 2 (bordures sans trous), l'un puis l'autre jusqu'à ce que rien ne change."""
    toutes = {(x, y) for x in range(L) for y in range(H)}
    while True:
        avant = sable_
        sable_ = {c for c in nettoyer(sable_ | hors_carte(sable_)) if dedans(*c)}
        eau_ = {c for c in nettoyer((toutes - sable_) | hors_carte(toutes - sable_)) if dedans(*c)}
        sable_ = toutes - eau_
        if sable_ == avant:
            return sable_


sable = nettoyer_cote(sable)
terre = plus_grand_morceau(sable)  # la côte ; le reste : les îlots
ilots = [m for m in morceaux_de(sable - terre)]

# Bordure d'origine (carte entière) : cocoteraie le long du haut et des côtés à terre, rochers le long des bords en mer.
# EPAISSEUR : 3,5 à 5,3 cases cachées, pour deux rangées de palmiers et 2 à 3 cases de rochers au moins à la dernière
# taille (demande de l'utilisateur, 2026-11-01 ; 2,4 avant : une seule rangée, avec des trous)
EPAISSEUR = 4.4
haut, gauche, droite, bas = (bruit(n, 0.9, 5, g) for n, g in ((L, 41), (H, 42), (H, 43), (L, 44)))
bordure_carte = {(x, y) for y in range(H) for x in range(L)
                 if y < EPAISSEUR + haut[x] or x < EPAISSEUR + gauche[y] or x >= L - EPAISSEUR + droite[y]
                 or y >= H - EPAISSEUR + bas[x]}
carte.lisiere = lisiere = set(bordure_carte)


# --- Sol : sable, mer et écume, tiré case par case (le même dans toutes les tailles) -----------
def sol_de(sable_):
    """Sol d'une taille : le sable donné, la mer ailleurs. Rend aussi le bord du sable (écume, bloqué)."""
    forme = sable_ | hors_carte(sable_)
    sol_, bord = [[0] * L for _ in range(H)], set()
    for y in range(H):
        for x in range(L):
            r = tirage(x, y, 3)
            if (x, y) not in sable_:
                sol_[y][x] = t("eau_details", r.randrange(3)) if r.random() < 0.04 else t("eau_unie", 0)
                continue
            i = bordure(forme, x, y, *PLAGE[1:])
            if i is not None:
                sol_[y][x] = t("plage", i)
                bord.add((x, y))
            else:
                sol_[y][x] = t("sable_details", r.randrange(4)) if r.random() < 0.1 else t("plage", 31)
    return sol_, bord


sol, ecume = sol_de(sable)
for y in range(H):
    for x in range(L):
        carte.sol[y][x] = sol[y][x]


# --- Objets : ponton, barque, palmiers, rochers, palmes, herbes ------------------------------
def libre_sol(x, y):
    return (x, y) in terre and (x, y) not in ecume


carte.libre_sol = libre_sol


def dans_depart(x, y, marge=0):
    dx, dy, dl, dh = DEPART
    return dx - marge <= x < dx + dl + marge and dy - marge <= y < dy + dh + marge


# Ponton qui part du sable et avance dans la mer, barque amarrée à côté. Ses planches sont à cheval sur deux colonnes
# (moitié droite de l'une, moitié gauche de l'autre) : on marche sur les deux ; les poteaux du bout sont bloqués.
PONTON_X = 40
debut = max(y for y in range(H) if (PONTON_X, y) in terre and (PONTON_X + 1, y) in terre and
            (PONTON_X, y) not in ecume and (PONTON_X + 1, y) not in ecume)
fin = debut + 7
tuiles = []
for y in range(debut, fin + 1):
    gauche_, droite_ = (28, 29) if y == fin else ((0, 1) if (y - debut) % 2 == 0 else (5, 6))
    tuiles += [("decors", PONTON_X, y, t("ponton", gauche_), y == fin), ("decors", PONTON_X + 1, y, t("ponton", droite_), y == fin)]
for _, x, y, tuile, _ in tuiles:
    carte.occupe_decors.add((x, y))
    decors[y][x] = tuile
objets.append({"tuiles": tuiles, "debloque": {(x, y) for _, x, y, _, b in tuiles if not b}})
poser("barque", 0, 1, 3, 2, PONTON_X + 2, fin - 3, verifier=False)

# Grands palmiers et palmiers isolés (peu dans la vue de départ, pour laisser de la place)
grand_palmier = lambda x, y: poser("grand_palmier", 3, 0, 3, 4, x, y, rang_devant=3, bloque_lignes=(3,), bloque_cols=(1,), marge=1)
palmier = lambda x, y: poser("palmier", 2, 0, 2, 3, x, y, rang_devant=2, bloque_lignes=(2,), bloque_cols=(0,), marge=1)
essayer(8, lambda x, y: not dans_depart(x + 1, y + 3, 1) and grand_palmier(x, y))
essayer(2, lambda x, y: dans_depart(x + 1, y + 3) and grand_palmier(x, y))
essayer(16, lambda x, y: not dans_depart(x, y + 2, 1) and palmier(x, y))
essayer(3, lambda x, y: dans_depart(x, y + 2) and palmier(x, y))

# Un palmier sur chaque îlot, au plus près de son centre
for ilot in ilots:
    cx, cy = sum(x for x, _ in ilot) / len(ilot), sum(y for _, y in ilot) / len(ilot)
    for x, y in sorted(ilot, key=lambda c: (c[0] + 0.5 - cx) ** 2 + (c[1] + 2 - cy) ** 2):
        if (x, y + 2) in ilot and (x + 1, y + 2) in ilot and all((x + i, y + j) not in carte.occupe_decors
                                                                 for i in (0, 1) for j in (0, 1, 2)):
            poser("palmier", 2, 0, 2, 3, x, y, rang_devant=2, verifier=False)
            break


def objet(nom, liste, n, zone=lambda x, y: True, bloquant=False, marge=0):
    def un(x, y):
        if not zone(x, y):
            return False
        return poser(nom, 0, 0, 1, 1, x, y, verifier=True, marge=marge) and remplacer(x, y)

    def remplacer(x, y):
        decors[y][x] = t(nom, rng.choice(liste))
        carte.bloque[y][x] = bloquant
        objets[-1]["tuiles"] = [("decors", x, y, decors[y][x], bloquant)]
        return True
    essayer(n, un)


# Rochers sur le sable (bloqués), palmes tombées (2 × 2, on y marche), touffes d'herbe des dunes
objet("rochers_sable", [0, 1, 4, 13, 16], 10, lambda x, y: not dans_depart(x, y), bloquant=True, marge=1)
objet("rochers_sable", [0, 1, 4], 2, lambda x, y: dans_depart(x, y), bloquant=True, marge=1)
essayer(8, lambda x, y: poser("palmes", 0, 0, 2, 2, x, y, marge=0))
objet("herbes_dune", [0, 1, 2], 45, lambda x, y: y < COTE - 3)

# Rochers dans la mer (loin du bord et des îlots)
ROCHERS_EAU = [42, 43, 51, 52, 60, 61]
PETITS_RECIFS = [44, 53, 62, 42, 61]  # rochers d'un seul morceau, les plus gros d'abord
GROS_ROCHERS = [(4, 11), (6, 11), (4, 13)]  # gros rochers de 2 × 2 (colonne, ligne du coin haut gauche)
pleine_mer = [(x, y) for y in range(H) for x in range(L)
              if all(dedans(x + dx, y + dy) and (x + dx, y + dy) not in sable for dx in (-2, 0, 2) for dy in (-2, 0, 2))
              and (x, y) not in bordure_carte and (x, y) not in carte.occupe_decors]
for x, y in rng.sample(pleine_mer, 30):
    if not any((x + dx, y + dy) in carte.occupe_decors for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
        decors[y][x] = t("decors", rng.choice(ROCHERS_EAU))
        carte.occupe_decors.add((x, y))
        objets.append({"tuiles": [("decors", x, y, decors[y][x], False)]})

# Quelques gros rochers en pleine mer
essayer(8, lambda x, y: all((x + i, y + j) in pleine_mer and (x + i, y + j) not in carte.occupe_decors
                            for i in (-1, 0, 1, 2) for j in (-1, 0, 1, 2))
        and poser("decors", *rng.choice(GROS_ROCHERS), 2, 2, x, y, bloque_lignes=(0, 1), verifier=False))

# --- Carte de chaque taille : la zone, une cocoteraie et des rochers tout autour ---------------
# Lisière irrégulière : 0 à 2 cases dans la zone (moins que l'écart entre deux zones)
LISIERE = [[max(0, min(2, round(0.8 + v))) for v in bruit(max(L, H), 1.3, 2.5, graine)] for graine in (51, 52, 53, 54)]


def cache_de(zone):
    """Cases cachées d'une taille : la bordure de la carte, et tout ce qui est hors de la zone (lisière irrégulière), sauf
    les objets et les îlots qui tiennent dans la zone (la lisière les contourne)."""
    zx, zy, zl, zh = zone
    haut_, bas_, gauche_, droite_ = LISIERE
    cache = bordure_carte | {(x, y) for y in range(H) for x in range(L)
                             if not (zx + gauche_[y] <= x < zx + zl - droite_[y] and zy + haut_[x] <= y < zy + zh - bas_[x])}
    for objet_ in objets:
        cellules = {(x, y) for _, x, y, _, _ in objet_["tuiles"]}
        if all(dans(zone, x, y) for x, y in cellules) and not cellules & bordure_carte:
            cache -= cellules
    for ilot in ilots:
        if all(dans(zone, x, y) for x, y in ilot):
            cache -= {c for c in voisins8(ilot) if dans(zone, *c)} - bordure_carte
    return cache


def palmier_de(x, y):
    """Palmier de la cocoteraie en (x, y) : le droit (2 × 3) ou le penché (2 × 2, posé une case plus bas)."""
    return tirage(x, y, 1).random() < 0.75


def planter_cocoteraie(cache, terre_, grilles, bloque_):
    """Cocoteraie en quinconce : un palmier dont les deux cases du pied sont du sable caché, hors de l'écume."""
    for y in range(-2, H, 2):
        for x in range(-1 + (y // 2) % 2, L, 2):
            pied = [(x, min(y + 2, H - 1)), (x + 1, min(y + 2, H - 1))]
            if all(not dedans(*c) or (c in cache and c in terre_) for c in pied):
                if palmier_de(x, y):
                    tuiles = bloc("palmier", 2, 0, 2, 3, x, y, rang_devant=2, bloque_lignes=(2,))
                else:
                    tuiles = bloc("palmier", 4, 0, 2, 2, x, y + 1, rang_devant=1, bloque_lignes=(1,))
                ecrire(grilles, bloque_, tuiles)


def planter_recif(cache, sable_, grilles, bloque_):
    """Récif en quinconce dans la mer cachée : un gros rocher (2 × 2) dont les quatre cases sont de la mer cachée."""
    for y in range(-1, H, 2):
        for x in range(-1 + (y // 2) % 2, L, 2):
            cases = [(x + i, y + j) for i in (0, 1) for j in (0, 1)]
            if all(not dedans(*c) or (c in cache and c not in sable_) for c in cases):
                gauche_, haut_ = tirage(x, y, 4).choice(GROS_ROCHERS)
                ecrire(grilles, bloque_, bloc("decors", gauche_, haut_, 2, 2, x, y, bloque_lignes=(0, 1)))


def planter_bordure(cache, terre_, grilles, bloque_):
    """Le reste de ce qui est caché : des buissons sous les palmiers, des rochers serrés dans la mer et sur l'écume. Le
    bord de la bordure d'origine dans la zone, sans palmier, reste du sable où l'on marche."""
    decors_, devant_ = grilles["decors"], grilles["devant"]
    for x, y in cache:
        r = tirage(x, y, 2)
        if (x, y) in terre_:
            if (x, y) in bordure_carte and not decors_[y][x] and not devant_[y][x]:
                continue
            bloque_[y][x] = True
            if not decors_[y][x] and (not devant_[y][x] or r.random() < 0.85):
                decors_[y][x] = t("decors", r.choice((86, 87, 88)))
        else:
            bloque_[y][x] = True
            if not decors_[y][x] and r.random() < 0.7:
                decors_[y][x] = t("decors", r.choice(PETITS_RECIFS))


def poser_objets(cache, libres_avant, grilles, bloque_):
    """Objets du parc entier, s'ils sont tout entiers dans la zone, sans chevaucher ce qui est caché ni bloquer une case
    libre de la taille d'avant. Le ponton débloque ses cases (la mer et l'écume dessous)."""
    for objet_ in objets:
        tuiles = objet_["tuiles"]
        if any((x, y) in cache or grilles[calque][y][x] or (bloquee and (x, y) in libres_avant)
               for calque, x, y, _, bloquee in tuiles):
            continue
        ecrire(grilles, bloque_, tuiles)
        for x, y in objet_.get("debloque", ()):
            bloque_[y][x] = False


def taille(zone, libres_avant):
    """Carte d'une taille : sol, décors, feuillages, cases bloquées et cases libres. libres_avant : cases libres de la taille
    d'avant, qui doivent le rester (un agrandissement ne reprend rien)."""
    cache = cache_de(zone)
    # Les îlots hors de la zone disparaissent (rien de visible de ce qu'il reste à débloquer)
    sable_ = terre | {c for ilot in ilots if all(dans(zone, x, y) for x, y in ilot) for c in ilot}
    sol_, ecume_ = sol_de(sable_)
    decors_ = [[0] * L for _ in range(H)]
    devant_ = [[0] * L for _ in range(H)]
    bloque_ = [[(x, y) not in sable_ or (x, y) in ecume_ or (x, y) in sable_ - terre for x in range(L)] for y in range(H)]
    grilles = {"decors": decors_, "devant": devant_}
    terre_ = terre - ecume_
    planter_cocoteraie(cache, terre_, grilles, bloque_)
    planter_recif(cache, sable_, grilles, bloque_)
    planter_bordure(cache, terre_, grilles, bloque_)
    poser_objets(cache, libres_avant, grilles, bloque_)
    # Poches de sable enfermées (entre la lisière, l'écume et les objets) : des buissons, personne n'y va
    gardees = carte.boucher_poches(zone, decors_, bloque_, lambda x, y: t("decors", tirage(x, y, 2).choice((86, 87, 88))))
    perdues = libres_avant - gardees
    if perdues:
        sys.exit(f"Zone {zone} : cases libres de la taille d'avant bloquées : {sorted(perdues)[:10]}")
    return sol_, decors_, devant_, bloque_, gardees, sable_


# --- Écriture des .tmx -----------------------------------------------------------------------
APERCUS = sys.argv[3] if len(sys.argv) > 3 else SORTIE
libres = set()
for n, zone in enumerate(ZONES, 1):
    sol_, decors_, devant_, bloque_, libres, sable_ = taille(zone, libres)
    carte.ecrire_carte(SORTIE, f"Plage_1-{n}.tmx", DEPART, zone, sol_, decors_, devant_, bloque_)
    carte.apercu(APERCUS, f"plage-{n}", (sol_, decors_, devant_), bloque_)
    # Types de cases comme carte-parc.py : le ponton (planche de sol dans « Décors ») n'est ni sable ni mer
    zx, zy, zl, zh = zone
    planches = [t("ponton", 0), t("barque", 0)]
    ponton = {(x, y) for y in range(H) for x in range(L) if planches[0] <= decors_[y][x] < planches[1]}
    cases = [(x, y) for y in range(zy, zy + zh) for x in range(zx, zx + zl)]
    sable_zone = sum(1 for c in cases if c in sable_ and c not in ponton)
    eau_zone = sum(1 for c in cases if c not in sable_ and c not in ponton)
    print(f"Plage_1-{n}.tmx : zone {zl} × {zh}, sable {100 * sable_zone / len(cases):.1f} %, "
          f"mer {100 * eau_zone / len(cases):.1f} %, {len(libres)} cases libres ({len(libres) / (20 * n):.1f} par Pokémon)")
