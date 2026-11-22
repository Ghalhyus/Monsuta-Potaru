# Génère les cartes du désert pour Tiled (Desert_1-1.tmx à Desert_1-4.tmx, une par taille achetée) avec le pack Cute
# Fantasy Desert : des dunes, un plateau au fond (falaise de 3 cases, escaliers) qui porte un lac, une cascade (animée) qui
# en tombe jusqu'à une oasis (herbe, palmiers), des cactus, des plantes, des rochers et des acacias, et le calque
# « Collisions » qui va avec. Tirages fixés : relancé sans changement, il redonne les mêmes cartes.
# Choix de l'utilisateur (2026-11-02) : dunes, plateau et oasis, ~70 % de sable (le parc des types Sol ; Feu, Roche...
# n'aiment pas le sable d'après la feuille « Habitat »), cactus, plantes, rochers et acacias, teinte 1 (sable doré) ; ce
# qui n'est pas encore acheté est un champ de cactus et de rochers serrés ; les zones du volcan.
# L'eau et son bord de sable (l'écume) sont bloqués ; les types Eau y nagent (parc.js : eau libre).
# Ensuite, pour chacune : python outils/carte-parc.py <Desert_1-1.tmx>. Aperçus (desert-1.png, desert-1_collisions.png...)
# dans le dossier de sortie, ou dans le troisième. Moteur commun avec les autres parcs : parc_carte.py.
# Usage (Python 3 avec Pillow) :
#   python outils/generer-desert.py <dossier Cute_Fantasy du pack> <dossier de sortie> [dossier des aperçus]
import math
import sys
from parc_carte import Carte, bordure, bruit, dans, ecrire, morceaux_de, nettoyer, tirage, voisins8

PACK, SORTIE = sys.argv[1], sys.argv[2]
L, H = 80, 60
# Zone de chaque taille (x, y, largeur, hauteur, en tuiles) : 20, 40, 60 puis 80 Pokémon (ParkService). Celles du volcan.
DEPART = (28, 20, 24, 20)
ZONES = [DEPART, (20, 15, 40, 30), (12, 10, 56, 40), (0, 0, L, H)]
carte = Carte(PACK, L, H, 17)
rng, jeu, t, dedans = carte.rng, carte.jeu, carte.t, carte.dedans
bloc, poser, essayer = carte.bloc, carte.poser, carte.essayer
decors, objets = carte.decors, carte.objets

# --- Jeux de tuiles (incorporés à la carte) -------------------------------------------------
DESERT = "../Cute_Fantasy_Desert/"
jeu("falaise", DESERT + "Tiles/Desert_Cliff_Tiles_1.png")  # flancs, escaliers, et le sable uni (en bas à droite)
jeu("plage", DESERT + "Tiles/Desert_Beach_Tiles_1.png")
jeu("eau", DESERT + "Tiles/Desert_Water_Tiles_1.png")
jeu("cascade", DESERT + "Tiles/Desert_Cliff_Waterfall_1.png")
jeu("herbe", DESERT + "Tiles/Desert_Grass.png")
jeu("cactus", DESERT + "Props/Cactus.png")
jeu("rochers", DESERT + "Props/Desert_Rocks.png")
jeu("acacia", DESERT + "Props/Acacia_Tree.png")
jeu("palmier", DESERT + "Props/Palm_Tree_2.png")
jeu("grand_palmier", DESERT + "Props/Palm_Tree_1.png")
jeu("palmes", DESERT + "Props/Fallen_Palm_Leaves.png")
jeu("herbes", DESERT + "Props/Desert_Grass_Props.png")
jeu("buisson_sec", DESERT + "Props/Dead_bush.png")
jeu("fougere", DESERT + "Props/Desert_Fern.png")
for n in (1, 2, 3):
    jeu(f"herbe_vent{n}", DESERT + f"Props/Outdoor_Decor_Animations/Desert_Grass_{n}_Anim.png")
    carte.animer(f"herbe_vent{n}", 0, [(i, 160) for i in range(8)])  # une touffe qui ondule au vent

# Sable : la tuile unie de Desert_Cliff_Tiles (128), parfois une à peine marquée (les ronds de dunes, en carrés, faisaient
# des taches : retirés)
SABLE_UNI, SABLE_MARQUE = 128, [139, 140]
# Bords du sable autour de l'eau (le sable est la forme, l'eau autour) : coins HG H HD G D BG B BD, coins rentrants
# SE SO NE NO ; l'eau, elle, est unie
PLAGE = ("plage", (0, 1, 2, 5, 7, 10, 11, 12), (3, 4, 8, 9))
EAU_UNIE = 10
# Herbe (Desert_Grass) : son bord passe au milieu des cases ; une tuile selon les coins herbus (NO, NE, SO, SE), comme sur
# la planche : 11 toute herbe, 0 à 8 l'herbe autour d'un trou, 9 10 12 13 un seul coin
HERBE = {(1, 1, 1, 1): 11, (0, 0, 0, 1): 9, (0, 0, 1, 0): 10, (0, 1, 0, 0): 12, (1, 0, 0, 0): 13,
         (1, 1, 1, 0): 0, (1, 1, 0, 0): 1, (1, 1, 0, 1): 2, (1, 0, 1, 0): 3, (0, 1, 0, 1): 5, (1, 0, 1, 1): 6,
         (0, 0, 1, 1): 7, (0, 1, 1, 1): 8, (1, 0, 0, 1): 9, (0, 1, 1, 0): 10}  # (deux coins opposés : un seul gardé)
HERBE_PLEINE = 11

# Cascade (Desert_Cliff_Waterfall_1) : 3 × 6 tuiles, 6 images côte à côte (18 colonnes) ; lignes 0 et 1, le ruisseau
# entre deux pointes de roche, lignes 2 à 4, la chute, ligne 5, le pied dans l'eau. Seule la chute le long du flanc est
# gardée (demandes de l'utilisateur, 2026-11-03) : les pointes, posées sur le plateau, faisaient des bords cassés avec
# l'écume du ruisseau, et les lignes 4 et 5 ont des cailloux au pied de la chute (la ligne 3 est répétée en bas).
CASCADE_COLONNES, CASCADE_IMAGES, CASCADE_DUREE = 18, 6, 120
CASCADE_LIGNES = [2, 3, 3]  # ligne de la planche pour chaque ligne de la cascade, sur les 3 lignes du flanc
for r in sorted(set(CASCADE_LIGNES)):
    for c in range(3):
        carte.animer("cascade", r * CASCADE_COLONNES + c,
                     [(r * CASCADE_COLONNES + f * 3 + c, CASCADE_DUREE) for f in range(CASCADE_IMAGES)])

# --- Plateau à deux étages (modèle de l'utilisateur, Desert_1-4_modele.tmx, 2026-11-03) ------------
# Desert_Cliff_Tiles : le rebord est dessiné dans les cases autour du dessus (14 15 16 en haut, 27 et 29 sur les côtés,
# le dessus lui-même est transparent : le sable du sol), le flanc sous la dernière ligne du dessus (40 41 42, puis 53 54
# 55 et 66 67 68 pour un flanc plus haut ; colonne du milieu répétée). Escaliers de 3 cases de large (22-24, 35-37, 48-50)
# à la place du flanc.
# Le dessus : flanc de 3 lignes au milieu (escaliers et cascade), d'une seule ligne ailleurs. Autour, une marche plus
# basse : ses rebords à une case de ceux du dessus sur les côtés (sa surface y est le rebord du dessus), et au sud, de part
# et d'autre du milieu, une ligne de sable puis son propre flanc d'une ligne. Le haut est commun aux deux étages.
PLATEAU = (21, 8, 38, 18)  # x, y, largeur, hauteur du dessus
PX, PY, PL, PH = PLATEAU
FLANC_Y = PY + PH  # première ligne du flanc
MILIEU = (31, 49)  # colonnes du flanc de 3 lignes (de x0 à x1 exclu)
ESCALIERS = [31, 46]  # x de la première colonne : de part et d'autre de la cascade, dans la vue de départ
CASCADE = (38, FLANC_Y)  # coin haut gauche : la chute sur les 3 lignes du flanc
CX, CY = CASCADE
GX0, GX1 = PX - 2, PX + PL + 1  # rebords de la marche, à gauche et à droite
M0, M1 = MILIEU

dessus = {(x, y) for x in range(PX, PX + PL) for y in range(PY, PY + PH)}
falaise = {(GX0, PY - 1): 14, (GX1, PY - 1): 16}  # case → tuile (rebords, flancs, escaliers)
falaise.update({(x, PY - 1): 15 for x in range(GX0 + 1, GX1)})
for y in range(PY, FLANC_Y + 2):
    falaise[(GX0, y)], falaise[(GX1, y)] = 27, 29
for y in range(PY, FLANC_Y):
    falaise[(PX - 1, y)], falaise[(PX + PL, y)] = 27, 29
falaise[(PX - 1, FLANC_Y)], falaise[(PX + PL, FLANC_Y)] = 40, 42
falaise.update({(x, FLANC_Y): 41 for x in range(PX, PX + PL)})
for j, m in ((1, 54), (2, 67)):
    falaise.update({(x, FLANC_Y + j): m for x in range(M0, M1)})
cotes = list(range(GX0 + 1, M0 - 1)) + list(range(M1 + 1, GX1))  # la marche au sud, de part et d'autre du milieu
falaise[(M0 - 1, FLANC_Y + 1)], falaise[(M1, FLANC_Y + 1)] = 29, 27
falaise[(GX0, FLANC_Y + 2)], falaise[(M0 - 1, FLANC_Y + 2)], falaise[(M1, FLANC_Y + 2)], falaise[(GX1, FLANC_Y + 2)] = 40, 42, 40, 42
falaise.update({(x, FLANC_Y + 2): 41 for x in cotes})
marche = {(x, FLANC_Y + 1) for x in cotes}  # sable de la marche au sud : on n'y monte pas
escaliers = {(x0 + i, FLANC_Y + j): 22 + 13 * j + i for x0 in ESCALIERS for i in range(3) for j in range(3)}
falaise.update(escaliers)
cascade = {(CX + i, CY + j): r * CASCADE_COLONNES + i for i in range(3) for j, r in enumerate(CASCADE_LIGNES)}
for c in cascade:
    falaise.pop(c, None)


# --- Formes : lac, oasis, herbe ----------------------------------------------------------------
def bandes(lignes):
    """Forme donnée ligne par ligne : {y: (x0, x1), ...}, de x0 à x1 compris."""
    return {(x, y) for y, (x0, x1) in lignes.items() for x in range(x0, x1 + 1)}


def tache(cx, cy, rx, ry, graine, bord=0.12):
    """Tache aux bords irréguliers : une ellipse dont le rayon varie avec l'angle (de ± <bord>, en part du rayon)."""
    r = bruit(64, bord, 4, graine)
    return {(x, y) for y in range(H) for x in range(L)
            if math.hypot((x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry)
            <= 1 + r[int((math.atan2(y + 0.5 - cy, x + 0.5 - cx) + math.pi) / (2 * math.pi) * 63)]}


# Un seul point d'eau (demande de l'utilisateur, 2026-11-03) : le lac du plateau, qui se resserre en un ruisseau de 3
# cases et tombe en cascade dans l'oasis, au pied du flanc. Règle de l'utilisateur (2026-11-01) : une cascade a toujours
# quelque chose en amont et en aval.
LAC = tache(39.5, 15.5, 7, 3.6, 21) | bandes({19: (37, 42), 20: (37, 41), 21: (37, 41), 22: (38, 40), 23: (38, 40)})
LAC &= {c for c in dessus if c not in voisins8(voisins8(set(falaise)))}
# Juste au-dessus et juste au-dessous de la chute, l'eau passe par une seule case (l'écume des berges, dans les cases
# voisines, fait le reste : la largeur de l'eau de la chute, ~1,5 case) ; posée après le nettoyage (qui retire tout
# morceau d'eau de moins de 2 cases de large)
GOULOTS = {(CX + 1, CY - 1), (CX + 1, CY - 2), (CX + 1, CY + len(CASCADE_LIGNES)), (CX + 1, CY + len(CASCADE_LIGNES) + 1)}
OASIS = bandes({FLANC_Y + 3: (35, 43)}) | tache(39.5, 32.4, 5.6, 3.4, 22, bord=0.1)
# Herbe : seulement autour de l'eau (le reste du désert reste aride), un anneau jusqu'à l'écume ; largeur en cases (et
# son écart, selon l'angle)
HERBE_OASIS, HERBE_LAC = (4.3, 0.9), (3.4, 0.7)

# Bordure d'origine (carte entière) : des cactus et des rochers serrés tout autour, 2 à 3 cases à l'œil (règle de
# l'utilisateur) ; EPAISSEUR comme la plage et le volcan (un cactus demande deux cases de profondeur)
EPAISSEUR = 4.4
haut, gauche, droite, bas = (bruit(n, 0.9, 5, g) for n, g in ((L, 41), (H, 42), (H, 43), (L, 44)))
bordure_carte = {(x, y) for y in range(H) for x in range(L)
                 if y < EPAISSEUR + haut[x] or x < EPAISSEUR + gauche[y] or x >= L - EPAISSEUR + droite[y]
                 or y >= H - EPAISSEUR + bas[x]}
carte.lisiere = set(bordure_carte)



def nettoyer_eau(eau_):
    """Eau et sable sans morceau de moins de 2 × 2 (bords sans trous), l'un puis l'autre jusqu'à ce que rien ne change ;
    la cascade compte comme du sable (le ruisseau et l'oasis s'y raccordent)."""
    toutes = {(x, y) for x in range(-1, L + 1) for y in range(-1, H + 1)}
    while True:
        avant = eau_
        sable_ = nettoyer(toutes - eau_) | set(cascade)
        eau_ = nettoyer(toutes - sable_)
        if eau_ == avant:
            return eau_


eau = nettoyer_eau(LAC | OASIS)
eau -= {(CX, CY + len(CASCADE_LIGNES)), (CX + 2, CY + len(CASCADE_LIGNES))}
eau |= GOULOTS
# Corrections de l'utilisateur dans Tiled (Desert_1-4_modele2.tmx puis Desert_1-4_modele3.tmx, 2026-11-03), reprises
# telles quelles : la planche des falaises et les bords de plage ne se raccordent pas d'eux-mêmes.
# - Une corniche de sable au pied du flanc, de part et d'autre de la chute, bordée côté oasis d'une rangée de berge
#   (Desert_Beach_Tiles : bord du bas, coins de part et d'autre de la sortie de la chute) ; à ses deux bouts, du sable uni.
corniche = {(x, y) for x, y in eau if y == FLANC_Y + 3 and not CX <= x < CX + 3}
gauche_c = min(x for x, y in corniche if x < CX) - 1
droite_c = max(x for x, y in corniche if x > CX + 2) + 1
bouts = {(gauche_c, FLANC_Y + 3), (droite_c, FLANC_Y + 3)}
B_, BG_, BD_ = PLAGE[1][6], PLAGE[1][5], PLAGE[1][7]
berge_basse = {(x, FLANC_Y + 4): B_ for x in range(gauche_c, droite_c + 1) if x != CX + 1}
berge_basse[(CX, FLANC_Y + 4)], berge_basse[(CX + 2, FLANC_Y + 4)] = BD_, BG_
eau_avant_berge = eau - corniche  # l'herbe autour de l'oasis est mesurée depuis l'eau d'avant la berge (dessin validé)
eau -= corniche | set(berge_basse)
# - Les deux berges de la sortie de la chute : un simple bord, côté eau.
BERGES = {(CX, CY + len(CASCADE_LIGNES)): PLAGE[1][4], (CX + 2, CY + len(CASCADE_LIGNES)): PLAGE[1][3]}
BERGES.update(berge_basse)
# - Au-dessus de la chute, de part et d'autre du goulot : les tuiles de la cascade du bout du ruisseau (pointes de roche,
#   dans « Sol »), un coin du petit rebord par-dessus (« Devant ») ; et une tuile de la chute (3e image) sur la pointe de
#   droite du haut du flanc (« Devant »).
SOL_FORCE = {(CX, CY - 2): ("cascade", 1), (CX + 2, CY - 2): ("cascade", 1),
             (CX, CY - 1): ("cascade", 18), (CX + 2, CY - 1): ("cascade", 20)}
DEVANT_FORCE = {(CX, CY - 2): ("falaise", 81), (CX + 2, CY - 2): ("falaise", 79), (CX + 2, CY): ("cascade", 44)}
# - Le flanc continue derrière les deux bouts de la marche, contre le milieu (dans « Sol », sous les rebords).
for j, m in enumerate((41, 54, 67)):
    SOL_FORCE[(M0 - 1, FLANC_Y + j)] = SOL_FORCE[(M1, FLANC_Y + j)] = ("falaise", m)
rebords_eau = {}
sable_uni = corniche | bouts  # ni écume ni herbe
if len(morceaux_de(eau | set(cascade))) != 1 or any((x, y + 2) not in eau for x, y in corniche):
    sys.exit("Le lac, la cascade et l'oasis ne sont pas d'un seul tenant, ou la corniche n'est pas au bord de l'oasis")


def anneau(eau_, largeur, ecart, graine):
    """Cases à <largeur> (± <ecart> selon l'angle autour de l'eau) au plus de l'eau, hors de l'eau."""
    cx, cy = sum(x for x, _ in eau_) / len(eau_), sum(y for _, y in eau_) / len(eau_)
    r = bruit(64, ecart, 4, graine)
    proches = voisins8(eau_)
    for _ in range(int(largeur + ecart) + 1):
        proches = voisins8(proches)
    return {(x, y) for x, y in proches - eau_
            if min(math.hypot(x - ex, y - ey) for ex, ey in eau_)
            <= largeur + r[int((math.atan2(y - cy, x - cx) + math.pi) / (2 * math.pi) * 63)]}


# L'herbe va jusqu'à l'écume (comprise : son bord passe au milieu de la case, l'écume reste visible côté eau)
eau_lac, eau_oasis = {c for c in eau if c in dessus}, {c for c in eau_avant_berge if c not in dessus}
herbe = nettoyer(((anneau(eau_oasis, *HERBE_OASIS, 23) - dessus) | (anneau(eau_lac, *HERBE_LAC, 26) & dessus))
                 - eau - set(falaise) - set(BERGES) - marche - sable_uni - voisins8(set(cascade)) - bordure_carte)
# Un groupe tout entier dans une zone y est contourné par la lisière (comme les objets) ; sinon, son eau est coupée à la
# lisière
herbe = set().union(*(m for m in morceaux_de(herbe) if len(m) >= 12))  # sans petits bouts isolés
groupes = [{"eau": eau, "herbe": herbe, "cellules": eau | set(cascade) | herbe | corniche}]
falaise.update(rebords_eau)  # dessinés et bloqués comme les rebords du plateau
morceaux_herbe = morceaux_de(herbe)  # l'herbe du lac et celle de l'oasis


def herbe_de(herbe_):
    """Tuiles de l'herbe : {case: tuile de Desert_Grass} (le bord de l'herbe passe au milieu des cases)."""
    def coin(x, y):  # sommet en haut à gauche de la case (x, y) : herbe si les quatre cases autour en sont
        return all((x + i, y + j) in herbe_ for i in (-1, 0) for j in (-1, 0))
    tuiles = {}
    for x, y in herbe_:
        coins = (coin(x, y), coin(x + 1, y), coin(x, y + 1), coin(x + 1, y + 1))
        if any(coins):
            tuiles[(x, y)] = HERBE[tuple(int(c) for c in coins)]
    return tuiles


bord_herbe = {c for c, i in herbe_de(herbe).items() if i != HERBE_PLEINE}


# --- Sol : sable, eau, écume et herbe, tiré case par case (le même dans toutes les tailles) -----
def tuile_hors_de_l_eau(x, y, forme, herbes, ecume):
    """Sol d'une case hors de l'eau : la plage (de l'écume, bloquée), l'herbe, sinon du sable (parfois marqué). Rend
    (tuile, écume ?)."""
    r = tirage(x, y, 3)
    i = None if (x, y) in sable_uni else BERGES.get((x, y), bordure(forme, x, y, *PLAGE[1:]))
    if i is not None:
        return t("plage", i), True
    if herbes.get((x, y)) == HERBE_PLEINE and (x, y) not in ecume:
        return t("herbe", HERBE_PLEINE), False
    return t("falaise", r.choice(SABLE_MARQUE) if r.random() < 0.05 else SABLE_UNI), False


def sol_de(eau_, herbe_):
    """Sol d'une taille et ses bords : rend (sol, écume (bloquée), tuiles du bord de l'herbe (dans « Décors »))."""
    forme = {(x, y) for x in range(-1, L + 1) for y in range(-1, H + 1)} - eau_  # le sable, et au-delà du bord
    herbes = herbe_de(herbe_)
    sol_, ecume = [[0] * L for _ in range(H)], set()
    for y in range(H):
        for x in range(L):
            if (x, y) in eau_:
                sol_[y][x] = t("eau", EAU_UNIE)
                continue
            sol_[y][x], est_ecume = tuile_hors_de_l_eau(x, y, forme, herbes, ecume)
            if est_ecume:
                ecume.add((x, y))
    for (x, y), (nom, i) in SOL_FORCE.items():
        sol_[y][x] = t(nom, i)
    bords = {c: t("herbe", i) for c, i in herbes.items() if i != HERBE_PLEINE}
    return sol_, ecume, bords


# --- Objets : palmiers, acacias, cactus, rochers, plantes --------------------------------------
ecume_toute = voisins8(eau)


def libre_sol(x, y):
    return ((x, y) not in eau and (x, y) not in ecume_toute and (x, y) not in falaise and (x, y) not in cascade
            and (x, y) not in bord_herbe and (x, y) not in marche)


carte.libre_sol = libre_sol
carte.occupe_decors |= set(falaise) | set(cascade) | bord_herbe | voisins8(set(cascade))
carte.occupe_decors |= sable_uni  # la corniche et le sable à côté des pointes de la cascade restent nus
carte.occupe_decors |= {(CX + i, CY - j) for i in range(-1, 4) for j in (1, 2, 3)}  # rien ne barre le ruisseau
for x0 in ESCALIERS:  # rien devant ni derrière les escaliers : on doit pouvoir y monter
    carte.occupe_decors |= {(x0 + i, FLANC_Y + j) for i in range(-1, 4) for j in (-2, -1, 3, 4)}

# Cascade : toutes ses cases bloquées
tuiles = [("decors", x, y, t("cascade", i), True) for (x, y), i in cascade.items()]
for _, x, y, tuile, _ in tuiles:
    decors[y][x] = tuile
objets.append({"tuiles": tuiles, "cascade": set(cascade)})


def dans_depart(x, y, marge=0):
    dx, dy, dl, dh = DEPART
    return dx - marge <= x < dx + dl + marge and dy - marge <= y < dy + dh + marge


pres_oasis = voisins8(voisins8(herbe | eau)) - eau
# Palmiers de l'oasis : le grand (3 × 4) et le petit (2 × 3) ; palmes dans « Devant », pied bloqué
grand_palmier = lambda x, y: poser("grand_palmier", 3, 0, 3, 4, x, y, rang_devant=3, bloque_lignes=(3,),
                                   bloque_cols=(1,), marge=1)
palmier = lambda x, y: poser("palmier", 2, 0, 2, 3, x, y, rang_devant=2, bloque_lignes=(2,), bloque_cols=(0,), marge=1)
essayer(14, lambda x, y: (x + 1, y + 3) in pres_oasis and grand_palmier(x, y))
essayer(14, lambda x, y: (x, y + 2) in pres_oasis and palmier(x, y))
# Acacias (5 × 4 avec l'ombre, ou 5 × 3), feuillage dans « Devant », tronc bloqué ; un seul dans la vue de départ
acacia = lambda x, y: (poser("acacia", 5, 0, 5, 4, x, y, rang_devant=2, bloque_lignes=(2,), bloque_cols=(2,), marge=1)
                       if tirage(x, y, 7).random() < 0.5 else
                       poser("acacia", 10, 0, 5, 3, x, y, rang_devant=2, bloque_lignes=(2,), bloque_cols=(2,), marge=1))
essayer(8, lambda x, y: not dans_depart(x + 2, y + 2, 2) and (x + 2, y + 2) not in pres_oasis and acacia(x, y))
essayer(1, lambda x, y: dans_depart(x + 2, y + 2) and (x + 2, y + 2) not in pres_oasis and acacia(x, y))


# Grands cactus (2 × 2 : à cheval sur deux colonnes de la planche), haut dans « Devant », pied bloqué sur ses deux cases :
# 5 formes, sans fleur ou en fleur (roses, orange, rouges)
def grand_cactus(x, y):
    r = tirage(x, y, 8)
    return poser("cactus", 2 * r.randrange(5), 2 * r.randrange(8), 2, 2, x, y, rang_devant=1, bloque_lignes=(1,), marge=1)


essayer(24, lambda x, y: not dans_depart(x, y, 1) and (x, y + 1) not in pres_oasis and grand_cactus(x, y))
essayer(3, lambda x, y: dans_depart(x, y) and (x, y + 1) not in pres_oasis and grand_cactus(x, y))
# Le haut de la carte (le plateau et au-dessus), que la vue de départ ne montre pas : plus de végétation (demande de
# l'utilisateur, 2026-11-03)
en_haut = lambda y: y < FLANC_Y
essayer(5, lambda x, y: en_haut(y + 2) and (x + 2, y + 2) not in pres_oasis and acacia(x, y))
essayer(14, lambda x, y: en_haut(y + 1) and not dans_depart(x, y, 1) and (x, y + 1) not in pres_oasis and grand_cactus(x, y))
essayer(6, lambda x, y: en_haut(y + 1) and poser("cactus", rng.choice((10, 12)), rng.choice((8, 10, 12)), 2, 2, x, y,
                                                 rang_devant=1, bloque_lignes=(1,), marge=1))
# Buissons fleuris (2 × 2), on ne passe pas dessus
essayer(6, lambda x, y: poser("cactus", rng.choice((10, 12)), rng.choice((8, 10, 12)), 2, 2, x, y, rang_devant=1,
                              bloque_lignes=(1,), marge=1))
# Gros rochers (2 × 2)
essayer(8, lambda x, y: not dans_depart(x, y, 1) and poser("rochers", 2, 0, 2, 2, x, y, bloque_lignes=(1,), marge=1))
essayer(1, lambda x, y: dans_depart(x, y) and poser("rochers", 2, 0, 2, 2, x, y, bloque_lignes=(1,), marge=1))


def objet(nom, liste, n, zone=lambda x, y: True, bloquant=False, marge=0, sur=None):
    """n objets d'une case (tuiles de <liste>) ; sur(x, y) : où les poser (sinon, le sable libre)."""
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
        if not poser(nom, 0, 0, 1, 1, x, y, verifier=True, marge=marge):
            return False
        decors[y][x] = t(nom, rng.choice(liste))
        carte.bloque[y][x] = bloquant
        objets[-1]["tuiles"] = [("decors", x, y, decors[y][x], bloquant)]
        return True
    essayer(n, un)


PETITS_CACTUS = [10, 11, 24, 25, 38, 39, 52, 53]
PETITS_ROCHERS = [0, 1, 4, 12, 13, 16]
objet("cactus", PETITS_CACTUS, 16, lambda x, y: not dans_depart(x, y), bloquant=True, marge=1)
objet("cactus", PETITS_CACTUS, 3, dans_depart, bloquant=True, marge=1)
objet("rochers", PETITS_ROCHERS, 18, lambda x, y: not dans_depart(x, y), bloquant=True, marge=1)
objet("rochers", PETITS_ROCHERS, 3, dans_depart, bloquant=True, marge=1)
# Herbes sèches, touffes qui ondulent au vent, buissons secs, fougères (on y marche)
objet("herbes", [0, 1, 2], 40, marge=1)
for n in (1, 2, 3):
    objet(f"herbe_vent{n}", [0], 12)
objet("buisson_sec", [0, 1], 12, marge=1)
objet("cactus", PETITS_CACTUS, 12, lambda x, y: en_haut(y) and not dans_depart(x, y), bloquant=True, marge=1)
objet("herbes", [0, 1, 2], 20, lambda x, y: en_haut(y), marge=1)
objet("buisson_sec", [0, 1], 8, lambda x, y: en_haut(y), marge=1)
objet("fougere", [0], 10, lambda x, y: (x, y) in pres_oasis)
essayer(5, lambda x, y: (x, y) in pres_oasis and poser("palmes", 0, 0, 2, 2, x, y))


# Dans l'eau : quelques rochers (avec leurs remous), loin des bords
def eau_profonde(x, y):
    return all((x + dx, y + dy) in eau for dx in (-1, 0, 1) for dy in (-1, 0, 1))


objet("rochers", [5, 6, 9, 17, 18], 2, sur=eau_profonde)

# --- Carte de chaque taille : la zone, un champ de cactus et de rochers tout autour --------------
# Lisière irrégulière : 0 à 2 cases dans la zone (moins que l'écart entre deux zones)
LISIERE = [[max(0, min(2, round(0.8 + v))) for v in bruit(max(L, H), 1.3, 2.5, graine)] for graine in (51, 52, 53, 54)]


def cache_de(zone):
    """Cases cachées d'une taille : la bordure de la carte, et tout ce qui est hors de la zone (lisière irrégulière), sauf
    les objets et l'oasis (avec son lac et sa cascade) qui tiennent dans la zone (la lisière les contourne)."""
    zx, zy, zl, zh = zone
    haut_, bas_, gauche_, droite_ = LISIERE
    cache = bordure_carte | {(x, y) for y in range(H) for x in range(L)
                             if not (zx + gauche_[y] <= x < zx + zl - droite_[y] and zy + haut_[x] <= y < zy + zh - bas_[x])}
    for objet_ in objets:
        cellules = {(x, y) for _, x, y, _, _ in objet_["tuiles"]}
        if all(dans(zone, x, y) for x, y in cellules) and not cellules & bordure_carte:
            cache -= cellules
    for groupe in groupes:
        if all(dans(zone, x, y) for x, y in groupe["cellules"]):
            cache -= {c for c in voisins8(groupe["cellules"]) if dans(zone, *c)} - bordure_carte
    return cache


def petit_decor(x, y):
    """Petit cactus ou petit rocher (une case)."""
    r = tirage(x, y, 2)
    return t("cactus", r.choice(PETITS_CACTUS)) if r.random() < 0.5 else t("rochers", r.choice(PETITS_ROCHERS))


def planter_cactus(cache, sable_, grilles, bloque_):
    """Champ en quinconce : un grand cactus (parfois un gros rocher) dont les deux cases du pied sont du sable caché."""
    for y in range(-1, H, 2):
        for x in range(-1 + (y // 2) % 2, L, 2):
            if place_de_cactus(x, y, cache, sable_, grilles) and (tuiles := cactus_ou_rocher(x, y, grilles)):
                ecrire(grilles, bloque_, tuiles)


def place_de_cactus(x, y, cache, sable_, grilles):
    """Les deux cases du pied (x, y + 1 et la suivante) sont du sable caché sans décor, et le haut n'a rien devant."""
    pied = [(x, y + 1), (x + 1, y + 1)]
    haut_ = [(x, y), (x + 1, y)]
    return (all(not dedans(*c) or (c in cache and c in sable_ and not grilles["decors"][c[1]][c[0]]) for c in pied)
            and all(not dedans(*c) or not grilles["devant"][c[1]][c[0]] for c in haut_))


def cactus_ou_rocher(x, y, grilles):
    """Un grand cactus (parfois un gros rocher, s'il ne couvre aucun décor) : ses tuiles, ou None."""
    r = tirage(x, y, 4)
    if r.random() < 0.8:
        return bloc("cactus", 2 * r.randrange(5), 2 * r.randrange(8), 2, 2, x, y, rang_devant=1, bloque_lignes=(1,))
    haut_ = [(x, y), (x + 1, y)]
    if any(c in grilles["decors"] and grilles["decors"][c[1]][c[0]] for c in haut_ if dedans(*c)):
        return None
    return bloc("rochers", 2, 0, 2, 2, x, y, bloque_lignes=(0, 1))


def planter_bordure(cache, grilles, bloque_):
    """Le reste de ce qui est caché : un petit cactus ou un petit rocher, bloqué. Le bord de la bordure d'origine dans la
    zone, sans rien, reste du sable où l'on marche ; le flanc et le rebord du plateau restent tels quels."""
    decors_, devant_ = grilles["decors"], grilles["devant"]
    for x, y in cache:
        if (x, y) in bordure_carte and not decors_[y][x] and not devant_[y][x]:
            continue
        bloque_[y][x] = True
        if not decors_[y][x] and (not devant_[y][x] or tirage(x, y, 9).random() < 0.85):
            decors_[y][x] = petit_decor(x, y)


def poser_objets(cache, libres_avant, grilles, bloque_):
    """Objets du parc entier, s'ils sont tout entiers dans la zone, sans chevaucher ce qui est caché ni bloquer une case
    libre de la taille d'avant. Rend les cases de la cascade, si elle est posée."""
    posees = set()
    for objet_ in objets:
        tuiles = objet_["tuiles"]
        cellules = {(x, y) for _, x, y, _, _ in tuiles}
        if cellules & cache and not cellules & bordure_carte and not objet_.get("cascade"):
            bloquer_pied_visible(tuiles, cache, grilles, bloque_)
        elif not any((x, y) in cache or grilles[calque][y][x] or (bloquee and (x, y) in libres_avant)
                     for calque, x, y, _, bloquee in tuiles):
            ecrire(grilles, bloque_, tuiles)
            posees |= objet_.get("cascade", set())
    return posees


def bloquer_pied_visible(tuiles, cache, grilles, bloque_):
    """Objet coupé par la lisière : son pied visible porte un petit décor, bloqué, pour que l'objet puisse apparaître tout
    entier à la taille suivante (une case libre ne doit pas être reprise)."""
    for _, x, y, _, bloquee in tuiles:
        if bloquee and (x, y) not in cache:
            bloque_[y][x] = True
            if not grilles["decors"][y][x]:
                grilles["decors"][y][x] = petit_decor(x, y)


def dessiner_plateau(cache, bords_herbe, decors_, bloque_):
    """Plateau (rebord, flanc, escaliers) : toujours dessiné, bloqué sauf les escaliers montrés ; la marche du plateau et
    la corniche de l'oasis (on n'y va pas, rien n'y est posé) ; le bord de l'herbe."""
    for (x, y), i in falaise.items():
        decors_[y][x] = t("falaise", i)
        bloque_[y][x] = (x, y) not in escaliers or (x, y) in cache
    for x, y in marche | corniche:
        bloque_[y][x] = True
    for (x, y), tuile in bords_herbe.items():
        decors_[y][x] = tuile


def bloquer_bord_de_l_eau(cache, eau_, ecume_, decors_, bloque_):
    """L'eau retirée au bord de la coupe (le nettoyage) : un rocher, bloqué (elle redevient de l'eau plus tard : une case
    libre ne doit pas être reprise). Le sable au bord de l'eau cachée aussi : il deviendra de l'écume (bloquée) quand
    l'eau sera montrée."""
    for x, y in (eau - eau_ - cache) | (voisins8(eau - eau_) - cache - eau_ - ecume_ - set(falaise) - set(cascade)):
        bloque_[y][x] = True
        if not decors_[y][x]:
            decors_[y][x] = t("rochers", tirage(x, y, 2).choice(PETITS_ROCHERS))


def verifier_cascade(zone, posees, eau_, decors_):
    """La cascade est posée, avec le lac au-dessus et de l'eau en dessous."""
    if posees != set(cascade):
        sys.exit(f"Zone {zone} : cascade pas posée")
    amont = all((CX + 1, CY - j) in eau_ and not decors_[CY - j][CX + 1] for j in (1, 2))
    aval = (CX + 1, CY + len(CASCADE_LIGNES)) in eau_
    if not (amont and aval):
        sys.exit(f"Zone {zone} : cascade sans lac au-dessus ou sans eau en dessous")


def taille(zone, libres_avant):
    """Carte d'une taille : sol, décors, feuillages, cases bloquées et cases libres. libres_avant : cases libres de la
    taille d'avant, qui doivent le rester (un agrandissement ne reprend rien)."""
    cache = cache_de(zone)
    if set(cascade) & cache:
        sys.exit(f"Zone {zone} : la cascade doit être visible à toutes les tailles")
    eau_ = nettoyer(eau - GOULOTS - cache) | (GOULOTS - cache)  # l'eau coupée à la lisière (sans morceau de moins de 2 × 2)
    # Un morceau d'herbe coupé à la lisière n'est montré que s'il en reste l'essentiel (pas de bande d'herbe le long de la
    # lisière) ; une case de sable entre l'herbe et ce qui est caché
    herbe_ = set().union(*(m - voisins8(cache) for m in morceaux_herbe if len(m - cache) >= 0.6 * len(m)))
    sol_, ecume_, bords_herbe = sol_de(eau_, herbe_)
    decors_ = [[0] * L for _ in range(H)]
    devant_ = [[0] * L for _ in range(H)]
    bloque_ = [[(x, y) in eau_ or (x, y) in ecume_ for x in range(L)] for y in range(H)]
    grilles = {"decors": decors_, "devant": devant_}
    dessiner_plateau(cache, bords_herbe, decors_, bloque_)
    bloquer_bord_de_l_eau(cache, eau_, ecume_, decors_, bloque_)
    sable_ = {(x, y) for y in range(H) for x in range(L)} - eau_ - ecume_ - set(falaise) - set(cascade)
    planter_cactus(cache, sable_, grilles, bloque_)
    planter_bordure(cache, grilles, bloque_)
    posees = poser_objets(cache, libres_avant, grilles, bloque_)
    for (x, y), (nom, i) in DEVANT_FORCE.items():
        if (x, y) not in cache:
            devant_[y][x] = t(nom, i)
    verifier_cascade(zone, posees, eau_, decors_)
    # Poches de sable enfermées (entre la lisière, l'eau, le plateau et les objets) : un petit décor, personne n'y va
    gardees = carte.boucher_poches(zone, decors_, bloque_, petit_decor)
    perdues = libres_avant - gardees
    if perdues:
        sys.exit(f"Zone {zone} : cases libres de la taille d'avant bloquées : {sorted(perdues)[:10]}")
    return sol_, decors_, devant_, bloque_, gardees, eau_, ecume_, herbe_


# --- Écriture des .tmx -----------------------------------------------------------------------
# --seulement <taille> : cette taille seule (essais d'une retouche, sans vérifier les tailles d'avant)
SEULEMENT = int(sys.argv[sys.argv.index("--seulement") + 1]) if "--seulement" in sys.argv else None
APERCUS = sys.argv[3] if len(sys.argv) > 3 and not sys.argv[3].startswith("--") else SORTIE
libres = set()
for n, zone in enumerate(ZONES, 1):
    if SEULEMENT is not None and n != SEULEMENT:
        continue
    sol_, decors_, devant_, bloque_, libres, eau_, ecume_, herbe_ = taille(zone, libres)
    carte.ecrire_carte(SORTIE, f"Desert_1-{n}.tmx", DEPART, zone, sol_, decors_, devant_, bloque_)
    carte.apercu(APERCUS, f"desert-{n}", (sol_, decors_, devant_), bloque_)
    # Types de cases comme carte-parc.py : eau (et cascade), herbe, falaise (rebord et flanc), aucun (escaliers), sable
    zx, zy, zl, zh = zone
    cases = [(x, y) for y in range(zy, zy + zh) for x in range(zx, zx + zl)]
    premier_herbe = t("herbe", 0)
    compte = {"eau": 0, "herbe": 0, "falaise": 0, "aucun": 0}
    for x, y in cases:
        if (x, y) in eau_ or (x, y) in cascade:
            compte["eau"] += 1
        elif (x, y) in escaliers:
            compte["aucun"] += 1
        elif (x, y) in falaise and (x, y) not in rebords_eau:
            compte["falaise"] += 1
        elif premier_herbe <= sol_[y][x] < premier_herbe + 15 or premier_herbe <= decors_[y][x] < premier_herbe + 15:
            compte["herbe"] += 1
    sable_zone = len(cases) - sum(compte.values())
    print(f"Desert_1-{n}.tmx : zone {zl} × {zh}, sable {100 * sable_zone / len(cases):.1f} %, "
          + ", ".join(f"{k} {100 * v / len(cases):.1f} %" for k, v in compte.items())
          + f", {len(libres)} cases libres ({len(libres) / (20 * n):.1f} par Pokémon)")
