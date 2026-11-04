# Génère les cartes de la grotte pour Tiled (Grotte_1-1.tmx à Grotte_1-4.tmx, une par taille achetée) avec le pack Cute
# Fantasy : une grande caverne au sol de terre, des piliers de roche, deux lacs souterrains bordés de pierres, des cristaux,
# des stalagmites, des rochers et des torches (animées), et le calque « Collisions » qui va avec. Tirages fixés : relancé
# sans changement, il redonne les mêmes cartes.
# Choix de l'utilisateur (2026-11-03) : une grotte naturelle (pas de rails ni de wagonnets), un lac souterrain qui grandit
# avec les agrandissements, la pénombre (calque d'objets « Lumières » : torches et cristaux, que parc.js éclaire), des
# cristaux et minerais, des stalagmites et des rochers, des torches.
# Parois (Cave_Walls, aucun exemple fourni avec le pack ; lu le 2026-11-03) : la roche vue de dessus est sombre et n'a pas de
# tuile unie : ses cases restent transparentes (tuile 3) sur la couleur de fond de la carte (FOND, que parc.js reprend).
# Une salle (la forme, SALLE) est bordée d'un anneau de pierres qui passe au milieu des cases de son bord, et sous son bord
# du haut descend un mur de deux rangées (MURS). Un lac (BASSIN) : le même anneau, le sol autour, l'eau dessous, et sous son
# bord du haut le pied du mur dans l'eau.
# Ce qui n'est pas encore acheté est de la roche : la salle de chaque taille s'arrête juste hors de la zone (son anneau et son
# mur du haut sont dans la lisière), la zone elle-même reste libre.
# Ensuite, pour chacune : python outils/carte-parc.py <Grotte_1-1.tmx>. Aperçus (grotte-1.png, grotte-1_collisions.png...)
# dans le dossier de sortie, ou dans le troisième. Moteur commun avec les autres parcs : parc_carte.py.
# Usage (Python 3 avec Pillow) :
#   python outils/generer-grotte.py <dossier Cute_Fantasy du pack> <dossier de sortie> [dossier des aperçus]
import math
import sys
from parc_carte import Carte, bordure, bruit, dans, morceaux_de, nettoyer, plus_grand_morceau, relies, tirage

PACK, SORTIE = sys.argv[1], sys.argv[2]
L, H = 80, 60
# Zone de chaque taille (x, y, largeur, hauteur, en tuiles) : 20, 40, 60 puis 80 Pokémon (ParkService), comme le volcan
DEPART = (28, 20, 24, 20)
ZONES = [DEPART, (20, 15, 40, 30), (12, 10, 56, 40), (0, 0, L, H)]
carte = Carte(PACK, L, H, 31)
rng, jeu, t, dedans = carte.rng, carte.jeu, carte.t, carte.dedans
poser, essayer, objets = carte.poser, carte.essayer, carte.objets
TOUT = {(x, y) for y in range(H) for x in range(L)}

# --- Jeux de tuiles (incorporés à la carte) -------------------------------------------------
jeu("murs", "Tiles/Cave/Cave_Walls.png")
jeu("sol", "Tiles/Cave/Cave_Floor_Middle.png")
jeu("sol_1", "Tiles/Cave/Cave_Floor_1.png")
jeu("sol_2", "Tiles/Cave/Cave_Floor_2.png")
jeu("sol_details", "Tiles/Cave/Cave_Floor_Decoration.png")
jeu("eau", "Tiles/Cave/Cave_Water.png")
jeu("pierres", "Outdoor decoration/Cave_Decorations.png")
jeu("minerais", "Outdoor decoration/Ores.png")
jeu("torche", "Outdoor decoration/Big_Torch_Anim.png")
# Torche : la flamme (première ligne, 8 images) est animée, le manche (ligne 2) et son pied (ligne 3) ne bougent pas
carte.animer("torche", 0, [(i, 110) for i in range(8)])

FOND = "#391f21"  # la roche vue de dessus (couleur des parois de Cave_Walls)
NOIR = 3  # tuile transparente de Cave_Walls : la roche (couleur de fond), de type cave pour carte-parc.py
# Bords (coins HG H HD G D BG B BD, coins rentrants SE SO NE NO) : d'une salle dans la roche (sombre autour), d'un lac
# creusé dans le sol (sol autour) ; le milieu de l'anneau est transparent (le sol ou l'eau dessous)
SALLE = ((4, 5, 6, 11, 13, 18, 19, 20), (25, 26, 32, 33))
BASSIN = ((0, 1, 2, 7, 9, 14, 15, 16), (21, 22, 28, 29))
MURS = [(42, 49), (43, 50), (46, 53), (47, 54)]  # mur sous le bord du haut d'une salle : (rangée 1, rangée 2)
EAU, PIED_DANS_L_EAU = 6, 32  # Cave_Water : eau unie, pied du mur dans l'eau (sous le bord du haut d'un lac)

# --- Formes : la caverne, ses piliers, les lacs ------------------------------------------------
# Bordure d'origine (carte entière) : la roche tout autour, 2 à 3 cases à l'œil (règle de l'utilisateur ; l'anneau de
# pierres en prend une demie, et le mur du haut deux rangées de plus)
EPAISSEUR = 3.6
haut, gauche, droite, bas = (bruit(n, 1.0, 5, g) for n, g in ((L, 41), (H, 42), (H, 43), (L, 44)))
bordure_carte = {(x, y) for y in range(H) for x in range(L)
                 if y < EPAISSEUR + haut[x] or x < EPAISSEUR + gauche[y] or x >= L - EPAISSEUR + droite[y]
                 or y >= H - EPAISSEUR + bas[x]}


def tache(cx, cy, rx, ry, graine, bord=0.25):
    """Tache aux bords irréguliers : une ellipse dont le rayon varie avec l'angle (de ± <bord>, en part du rayon)."""
    r = bruit(64, bord, 2.2, graine)
    return {(x, y) for x, y in TOUT
            if math.hypot((x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry)
            <= 1 + r[int((math.atan2(y + 0.5 - cy, x + 0.5 - cx) + math.pi) / (2 * math.pi) * 63)]}


# Piliers de roche dans la caverne (centre, rayons), aucun dans la vue de départ : deux autour d'elle (taille 2), trois plus
# loin (taille 3), trois près des bords (taille 4)
PILIERS = [(23.5, 25, 2.2, 3), (56.5, 19.5, 2.4, 2.2),
           (40, 12, 3.4, 1.9), (65.5, 24, 2, 3), (16.5, 38, 2.4, 2.6),
           (8, 26, 2.2, 3), (71, 44, 2.4, 2.8), (23.5, 52, 3.2, 1.8)]
piliers = set().union(*(tache(*p, 71 + i) for i, p in enumerate(PILIERS)))
# Lacs : le grand, en bas à droite (un bras le long de la vue de départ, à droite, et une large nappe sous elle), qu'on
# découvre à la taille 2 et qui s'étend aux tailles 3 et 4 ; le petit, en haut à gauche, à la taille 3
LACS = [tache(45, 49.4, 13.5, 4.3, 81, 0.15) | tache(58.5, 38.5, 4.6, 8.5, 82, 0.15) | tache(62, 49.5, 6.5, 4.2, 83, 0.15),
        tache(15, 15, 5.6, 4, 84, 0.15)]


def large(forme):
    """Les cases de la forme couvertes par un carré 3 × 3 tout entier dedans, sans trou (pas de langue d'eau de deux cases
    de large, ni d'île de sol enfermée)."""
    forme = {(x + i, y + j) for x, y in forme if all((x + a, y + b) in forme for a in range(3) for b in range(3))
             for i in range(3) for j in range(3)}
    return nettoyer(TOUT - plus_grand_morceau(TOUT - forme))


LAC = large(set().union(*LACS))


def adoucir(salle):
    """La salle et la roche sans morceau de moins de 2 × 2 (pas de pilier ni de couloir d'une case : l'anneau de pierres
    ne s'y dessine pas)."""
    while True:
        avant = salle
        salle = TOUT - nettoyer(TOUT - nettoyer(salle))
        if salle == avant:
            return salle


CAVERNE = adoucir(TOUT - bordure_carte - piliers)
if LAC - CAVERNE:
    sys.exit("Un lac déborde de la caverne")


# --- Bords d'une salle (anneau, mur du haut) et d'un lac ------------------------------------------
def bords(forme, jeu_):
    """Tuile de bord de chaque case du bord de la forme : {case : tuile}."""
    coins, interieur = jeu_
    return {c: i for c in forme if (i := bordure(forme, *c, coins, interieur)) is not None}


def murs_de(salle, anneau):
    """Mur sous le bord du haut de la salle : {case : rangée (0 ou 1)}, deux rangées hors de l'anneau."""
    murs = {}
    for x, y in anneau:
        if (x, y - 1) not in salle:
            for j in (1, 2):
                if (x, y + j) in salle and (x, y + j) not in anneau:
                    murs[(x, y + j)] = j - 1
    return murs


# Le sol de la caverne entière (hors des anneaux, des murs et des lacs) : là où se posent les objets
anneau_entier = bords(CAVERNE, SALLE)
sol_entier = CAVERNE - set(anneau_entier) - set(murs_de(CAVERNE, anneau_entier))
if LAC - sol_entier:
    sys.exit("Un lac touche une paroi (son anneau ou son mur)")
sol_entier -= LAC
bord_des_lacs = set(bords(LAC, BASSIN))  # pierres autour des lacs entiers : on y marche à toutes les tailles


def libre_sol(x, y):
    return (x, y) in sol_entier


carte.libre_sol = libre_sol


def dans_depart(x, y, marge=0):
    dx, dy, dl, dh = DEPART
    return dx - marge <= x < dx + dl + marge and dy - marge <= y < dy + dh + marge


# --- Objets : cristaux, torches, stalagmites, rochers ------------------------------------------
# Cristaux (Ores : colonnes 1 à 4, l'amas, le gros, le petit et la pointe ; les colonnes 5 à 7 sont des objets ramassés) en
# bouquets de 2 à 4 de la même couleur, éclairés de leur couleur : (ligne de la planche, couleur de la lumière)
CRISTAUX = [(4, "#4fc8ff"), (7, "#d77bff"), (3, "#74e07a"), (6, "#ff6f6f")]
LUMIERE_CRISTAL, LUMIERE_TORCHE = 56, 88  # rayons, en pixels de la carte


def bouquet(x, y, zone=lambda x, y: True):
    """Bouquet de cristaux autour de (x, y) : 2 à 4 cases voisines, chacune un cristal bloquant."""
    ligne, teinte = rng.choice(CRISTAUX)
    cases = [(x, y)] + rng.sample([(x + 1, y), (x, y + 1), (x + 1, y + 1), (x - 1, y)], rng.randint(1, 3))
    if not all(zone(*c) and carte.place_libre(1, 1, *c, 0, 0) for c in cases):
        return False
    if any((cx + dx, cy + dy) in carte.occupe_decors for cx, cy in cases for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
        return False
    tuiles = [("decors", cx, cy, t("minerais", ligne * 8 + rng.randint(1, 4)), True) for cx, cy in cases]
    for _, cx, cy, tuile, _ in tuiles:
        carte.decors[cy][cx] = tuile
        carte.occupe_decors.add((cx, cy))
    mx, my = sum(c[0] for c in cases) / len(cases), sum(c[1] for c in cases) / len(cases)
    objets.append({"tuiles": tuiles, "lumiere": (round(mx * 16 + 8), round(my * 16 + 8), LUMIERE_CRISTAL, teinte)})
    return True


# Les torches sont posées plus loin, sur une grille (TORCHES). Leur ancien tirage au hasard n'est là que pour les objets
# ci-dessous : ils gardent leur place, ces cases sont rendues une fois tout tiré.
def place_de_l_ancienne_torche(x, y):
    return poser("torche", 0, 0, 1, 3, x, y, bloque_lignes=(0, 1), marge=2, enregistrer=False)


occupees_avant = set(carte.occupe_decors)
essayer(3, lambda x, y: dans_depart(x, y, -2) and place_de_l_ancienne_torche(x, y))
essayer(14, lambda x, y: not dans_depart(x, y, 1) and place_de_l_ancienne_torche(x, y))
anciennes_torches = carte.occupe_decors - occupees_avant
essayer(2, lambda x, y: dans_depart(x, y, -1) and bouquet(x, y, dans_depart))
essayer(14, lambda x, y: not dans_depart(x, y, 1) and bouquet(x, y))


def un_objet(nom, liste, x, y, marge):
    if not poser(nom, 0, 0, 1, 1, x, y, bloque_lignes=(0,), marge=marge):
        return False
    tuile = t(nom, rng.choice(liste))
    carte.decors[y][x] = tuile
    objets[-1]["tuiles"] = [("decors", x, y, tuile, True)]
    return True


# Stalagmites (Cave_Decorations, ligne 2), parfois en groupe de deux ou trois ; rochers moussus (ligne 1)
STALAGMITES, ROCHERS = [4, 5, 6, 7], [0, 1, 2, 3]
essayer(5, lambda x, y: dans_depart(x, y) and un_objet("pierres", STALAGMITES, x, y, 1))
essayer(40, lambda x, y: not dans_depart(x, y) and un_objet("pierres", STALAGMITES, x, y, 1))
essayer(12, lambda x, y: not dans_depart(x, y) and un_objet("pierres", STALAGMITES, x, y, 0))
essayer(3, lambda x, y: dans_depart(x, y) and un_objet("pierres", ROCHERS, x, y, 1))
essayer(20, lambda x, y: not dans_depart(x, y) and un_objet("pierres", ROCHERS, x, y, 1))
carte.occupe_decors -= anciennes_torches


def eboulis(x, y):
    """Petit rocher ou stalagmite (bloqué) : le sol encore pris par un lac caché, les poches enfermées."""
    r = tirage(x, y, 2)
    return t("pierres", r.choice([2, 3, 6, 7]))


# --- Carte de chaque taille : la salle de la zone, la roche autour -------------------------------
def ecart(graine, plus):
    """Écart de la salle hors de la zone, de 0 à <plus> cases, case par case."""
    return [max(0, min(plus, round(plus / 2 + v))) for v in bruit(max(L, H), 1.1, 2.5, graine)]


ECARTS = [ecart(51, 1), ecart(52, 2), ecart(53, 2), ecart(54, 2)]  # haut, bas, gauche, droite


def salle_de(zone):
    """La caverne visible à cette taille : la zone, son anneau de pierres une case dehors (le mur du haut, deux de plus),
    avec un écart irrégulier."""
    if zone == ZONES[-1]:
        return CAVERNE
    zx, zy, zl, zh = zone
    h_, b_, g_, d_ = ECARTS
    vue = {(x, y) for x, y in TOUT
           if zx - 1 - g_[y] <= x <= zx + zl + d_[y] and zy - 3 - h_[x] <= y <= zy + zh + b_[x]}
    return adoucir(CAVERNE & vue)


# Torches réparties régulièrement (demande de l'utilisateur, 2026-11-04 : au hasard, elles se collaient et laissaient des
# coins sans lumière) : une grille en quinconce, calée sur la vue de départ (trois torches), PAS_TORCHES cases entre deux
# torches d'une rangée et entre deux rangées. Chacune sur la place libre la plus proche de son nœud (à RAYON_TORCHE cases au
# plus) ; un nœud sur un lac ou un pilier n'a pas de torche. Jamais deux torches à moins de ECART_TORCHES cases.
PAS_TORCHES, RAYON_TORCHE, ECART_TORCHES, PORTEE_TORCHE = (15, 12), 4, 9, 14
SOLS = []  # par taille : sa zone et son sol, hors de l'anneau et du mur
for zone_ in ZONES:
    salle_ = salle_de(zone_)
    anneau_ = bords(salle_, SALLE)
    SOLS.append((zone_, salle_ - set(anneau_) - set(murs_de(salle_, anneau_))))
TORCHES = []


def place_de_torche(x, y):
    """La torche (flamme en (x, y), manche et pied dessous) tient ici, loin des autres torches ; à chaque taille, elle est
    posée tout entière, ou ne prend aucune case où l'on marche (sinon, un agrandissement ne pourrait plus la poser)."""
    cases = [(x, y + j) for j in range(3)]
    return (carte.place_libre(1, 3, x, y, 0, 1)
            and all(math.dist((x, y), autre) >= ECART_TORCHES for autre in TORCHES)
            and all(set(cases) <= sol or not any(dans(zone_, *c) and c in sol for c in cases[:2]) for zone_, sol in SOLS))


def torche_vers(colonne, ligne, sol=TOUT):
    """Pose une torche sur la place libre la plus proche de ce point, tout entière sur <sol> ; False s'il n'y en a pas."""
    proches = sorted(((x, y) for x in range(colonne - RAYON_TORCHE, colonne + RAYON_TORCHE + 1)
                      for y in range(ligne - RAYON_TORCHE, ligne + RAYON_TORCHE + 1)),
                     key=lambda c: (math.dist(c, (colonne, ligne)), c))
    place = next((c for c in proches if place_de_torche(*c) and all((c[0], c[1] + j) in sol for j in range(3))), None)
    if place is None:
        return False
    x, y = place
    # Flamme et manche bloqués, on marche sur le pied
    poser("torche", 0, 0, 1, 3, x, y, bloque_lignes=(0, 1), verifier=False)
    objets[-1]["lumiere"] = (x * 16 + 8, y * 16 + 6, LUMIERE_TORCHE, "#ffb35c")
    TORCHES.append(place)
    return True


for rang, ligne in enumerate(range(0, H, PAS_TORCHES[1])):
    for colonne in range(3 + (PAS_TORCHES[0] // 2 if rang % 2 else 0), L, PAS_TORCHES[0]):
        torche_vers(colonne, ligne)
# Ce que la grille laisse sans lumière à une taille (derrière un lac, ou une torche encore cachée) : une torche de plus sur le
# sol de la zone le plus loin de celles qu'on y voit, jusqu'à ce qu'il n'y en ait plus à plus de PORTEE_TORCHE cases
for zone_, sol in SOLS:
    reste = {c for c in sol - LAC if dans(zone_, *c)}
    while True:
        vues = [c for c in TORCHES if all((c[0], c[1] + j) in sol for j in range(3))]
        loin = max(reste, key=lambda c: (min(math.dist(c, torche_) for torche_ in vues), c))
        if min(math.dist(loin, torche_) for torche_ in vues) <= PORTEE_TORCHE:
            break
        if not torche_vers(*loin, sol):
            reste.remove(loin)


def sol_de(x, y):
    """Sol de terre tiré case par case (le même à toutes les tailles) : surtout uni, des fissures par endroits."""
    r = tirage(x, y, 3)
    if r.random() < 0.05:
        return t("sol_details", r.randrange(3))
    return t("sol", 0)


def fissures(sol_, libre):
    """Plaques de fissures (Cave_Floor_1 et 2 : 3 × 3 qui s'estompent sur les bords ; leurs 2 × 2 serrées, aux bords droits,
    font des carrés), posées seulement sur du sol libre tout entier."""
    for y in range(0, H, 3):
        for x in range(0, L, 3):
            r = tirage(x, y, 9)
            if r.random() > 0.2:
                continue
            nom, px, py = r.choice(("sol_1", "sol_2")), x + r.randrange(2), y + r.randrange(2)
            if all((px + i, py + j) in libre for i in range(3) for j in range(3)):
                for i in range(3):
                    for j in range(3):
                        sol_[py + j][px + i] = t(nom, (2 + j) * 3 + i)


def taille(zone, libres_avant):
    """Carte d'une taille : sol, décors, cases bloquées, cases libres, eau libre et lumières. libres_avant : cases libres de
    la taille d'avant, qui doivent le rester (un agrandissement ne reprend rien)."""
    salle = salle_de(zone)
    anneau = bords(salle, SALLE)
    murs = murs_de(salle, anneau)
    sol_libre = salle - set(anneau) - set(murs)
    # Lacs : coupés à la salle (sans morceau de moins de 2 × 2) ; le sol d'un lac encore caché porte un éboulis
    lac = set().union(set(), *(m for m in morceaux_de(large(LAC & sol_libre)) if len(m) >= 30))
    bord_lac = bords(lac, BASSIN)
    pied = {(x, y + 1) for x, y in bord_lac if (x, y - 1) not in lac and (x, y + 1) in lac and (x, y + 1) not in bord_lac}
    sol_ = [[t("murs", NOIR)] * L for _ in range(H)]
    decors_ = [[0] * L for _ in range(H)]
    devant_ = [[0] * L for _ in range(H)]
    bloque_ = [[(x, y) not in salle for x in range(L)] for y in range(H)]
    for (x, y), i in anneau.items():
        sol_[y][x], decors_[y][x], bloque_[y][x] = t("sol", 0), t("murs", i), True
    for (x, y), j in murs.items():
        sol_[y][x], bloque_[y][x] = t("murs", tirage(x, 0, 6).choice(MURS)[j]), True
    for x, y in sol_libre:
        sol_[y][x] = sol_de(x, y)
    for x, y in lac:
        sol_[y][x] = t("eau", PIED_DANS_L_EAU if (x, y) in pied else EAU)
        bloque_[y][x] = (x, y) not in bord_lac
    # Pierres du bord : on y marche, sauf là où le lac s'étendra plus tard (elles redeviennent de l'eau)
    for (x, y), i in bord_lac.items():
        decors_[y][x] = t("murs", i)
        bloque_[y][x] = (x, y) not in bord_des_lacs
    for x, y in (LAC & sol_libre) - lac:
        decors_[y][x], bloque_[y][x] = eboulis(x, y), True
    sol_terre = sol_libre - lac
    fissures(sol_, {c for c in sol_terre if not decors_[c[1]][c[0]]})
    # Objets tout entiers sur le sol libre, sans bloquer une case libre de la taille d'avant
    grilles = {"decors": decors_, "devant": devant_}
    lumieres = []
    for objet_ in objets:
        tuiles = objet_["tuiles"]
        if any((x, y) not in sol_terre or grilles[calque][y][x] or (bloquee and (x, y) in libres_avant)
               for calque, x, y, _, bloquee in tuiles):
            continue
        for calque, x, y, tuile, bloquee in tuiles:
            grilles[calque][y][x] = tuile
            bloque_[y][x] = bloque_[y][x] or bloquee
        if "lumiere" in objet_:
            lumieres.append(objet_["lumiere"])
    # Poches enfermées (entre les piliers, le lac et les objets) : un éboulis, personne n'y va
    gardees = carte.boucher_poches(zone, decors_, bloque_, eboulis)
    perdues = libres_avant - gardees
    if perdues:
        sys.exit(f"Zone {zone} : cases libres de la taille d'avant bloquées : {sorted(perdues)[:10]}")
    # L'eau libre (bloquée, sans objet) doit toucher les pierres du bord où l'on marche : les nageurs y entrent (parc.js)
    zx, zy, zl, zh = zone
    eau = {(x, y) for x, y in lac if bloque_[y][x] and not decors_[y][x] and dans(zone, x, y)}
    if eau and not eau <= relies(eau | gardees):
        sys.exit(f"Zone {zone} : de l'eau du lac n'est pas reliée aux pierres du bord où l'on marche")
    return sol_, decors_, devant_, bloque_, gardees, eau, lumieres


# --- Écriture des .tmx -----------------------------------------------------------------------
APERCUS = sys.argv[3] if len(sys.argv) > 3 else SORTIE
libres = set()
for n, zone in enumerate(ZONES, 1):
    sol_, decors_, devant_, bloque_, libres, eau, lumieres = taille(zone, libres)
    carte.ecrire_carte(SORTIE, f"Grotte_1-{n}.tmx", DEPART, zone, sol_, decors_, devant_, bloque_, fond=FOND,
                       lumieres=lumieres)
    carte.apercu(APERCUS, f"grotte-{n}", (sol_, decors_, devant_), bloque_, fond=(0x39, 0x1f, 0x21))
    zx, zy, zl, zh = zone
    total = zl * zh
    print(f"Grotte_1-{n}.tmx : zone {zl} × {zh}, eau {100 * len(eau) / total:.1f} %, {len(libres)} cases libres "
          f"({len(libres) / (20 * n):.1f} par Pokémon), {len(lumieres)} lumières")
