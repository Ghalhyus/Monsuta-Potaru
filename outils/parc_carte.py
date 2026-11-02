# Moteur commun des générateurs de cartes du parc (generer-parc.py, generer-plage.py, generer-volcan.py) avec le pack Cute
# Fantasy : jeux de tuiles incorporés (et leurs tuiles animées), calques, formes (bruit, taches, nettoyage), bordures
# automatiques, objets posés, poches enfermées, écriture des .tmx pour Tiled et aperçus en PNG. Chaque générateur garde
# ce qui fait son paysage (formes, objets, ce qui cache la partie pas encore achetée) et l'ordre de ses tirages : relancé
# sans changement, il redonne les mêmes cartes.
import math
import os
import random
from PIL import Image


# --- Formes ----------------------------------------------------------------------------------
def bruit(n, amplitude, periode, graine):
    r = random.Random(graine)
    a, b, c = r.random() * 6.3, r.random() * 6.3, r.random() * 6.3
    return [amplitude * (math.sin(i / periode + a) * 0.6 + math.sin(i / (periode * 0.45) + b) * 0.3
                         + math.sin(i / (periode * 0.21) + c) * 0.1) for i in range(n)]


def voisins8(cellules):
    return {(x + dx, y + dy) for x, y in cellules for dx in (-1, 0, 1) for dy in (-1, 0, 1)}


def couvertes(cellules):
    """Cellules couvertes par un carré 2 × 2 entièrement dans la forme."""
    return {(x, y) for x, y in cellules
            if any(all((x + dx + i, y + dy + j) in cellules for i in (0, 1) for j in (0, 1))
                   for dx in (0, -1) for dy in (0, -1))}


def combler_diagonales(gardees):
    """Une diagonale isolée (deux carrés qui ne se touchent que par un coin) : on comble."""
    avant = list(gardees)  # copie : les cases comblées s'ajoutent pendant le parcours
    for x, y in avant:
        for dx, dy in ((1, 1), (1, -1)):
            if (x + dx, y + dy) in gardees and (x + dx, y) not in gardees and (x, y + dy) not in gardees:
                gardees.add((x + dx, y))


def nettoyer(cellules):
    """Garde les cellules couvertes par un carré 2 × 2 entièrement dans la forme (bordures sans trous)."""
    cellules = set(cellules)
    while True:
        gardees = couvertes(cellules)
        combler_diagonales(gardees)
        if gardees == cellules:
            return cellules
        cellules = gardees


def plus_grand_morceau(cellules):
    restantes, meilleur = set(cellules), set()
    while restantes:
        pile, morceau = [restantes.pop()], set()
        while pile:
            x, y = pile.pop()
            morceau.add((x, y))
            for v in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if v in restantes:
                    restantes.remove(v)
                    pile.append(v)
        meilleur = max(meilleur, morceau, key=len)
    return meilleur


def morceaux_de(cellules):
    """Morceaux d'une forme (cases reliées par un côté)."""
    restantes, liste = set(cellules), []
    while restantes:
        pile, morceau = [restantes.pop()], set()
        while pile:
            x, y = pile.pop()
            morceau.add((x, y))
            for v in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if v in restantes:
                    restantes.remove(v)
                    pile.append(v)
        liste.append(morceau)
    return liste


def bordure(forme, x, y, coins, interieur):
    """Tuile de bordure de la case (x, y) de la forme, ou None au milieu. coins : HG H HD G D BG B BD ; interieur : coins
    rentrants SE SO NE NO."""
    hg, h, hd, g, d, bg, b, bd = coins
    ise, iso, ine, ino = interieur
    n, s = (x, y - 1) in forme, (x, y + 1) in forme
    o, e = (x - 1, y) in forme, (x + 1, y) in forme
    # Premier cas qui s'applique : coins, côtés, puis coins rentrants
    cas = ((not (n or o), hg), (not (n or e), hd), (not (s or o), bg), (not (s or e), bd),
           (not n, h), (not s, b), (not o, g), (not e, d),
           ((x + 1, y + 1) not in forme, ise), ((x - 1, y + 1) not in forme, iso),
           ((x + 1, y - 1) not in forme, ine), ((x - 1, y - 1) not in forme, ino))
    return next((tuile for vrai, tuile in cas if vrai), None)


# --- Cartes de chaque taille ----------------------------------------------------------------
def voisins_relies(x, y, libres):
    """Cases voisines de (x, y) où l'on passe, avec la règle de parc.js : en diagonale seulement si les deux côtés sont
    libres."""
    return [(x + dx, y + dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1)
            if dx == 0 or dy == 0 or ((x + dx, y) in libres and (x, y + dy) in libres)]


def relies(libres):
    """Cases libres reliées à la plus grande partie."""
    restantes, meilleur = set(libres), set()
    while restantes:
        pile, partie = [restantes.pop()], set()
        while pile:
            x, y = pile.pop()
            partie.add((x, y))
            for v in voisins_relies(x, y, libres):
                if v in restantes:
                    restantes.remove(v)
                    pile.append(v)
        meilleur = max(meilleur, partie, key=len)
    return meilleur


def dans(zone, x, y):
    zx, zy, zl, zh = zone
    return zx <= x < zx + zl and zy <= y < zy + zh


def tirage(x, y, n):
    """Tirage propre à la case (x, y), le même dans toutes les tailles ; n : son usage (1 arbre, 2 buisson...)."""
    return random.Random(x * 7919 + y * 104729 + n)


def ecrire(grilles, bloque_, tuiles):
    """Pose des tuiles (calque, x, y, tuile, bloquée) dans les grilles d'une taille."""
    for calque, x, y, tuile, bloquee in tuiles:
        grilles[calque][y][x] = tuile
        bloque_[y][x] = bloque_[y][x] or bloquee


def csv(grille):
    return ",\n".join(",".join(str(v) for v in ligne) for ligne in grille)


def rectangle(id_, nom, zone):
    x, y, l, h = zone
    return f'  <object id="{id_}" name="{nom}" x="{x * 16}" y="{y * 16}" width="{l * 16}" height="{h * 16}"/>'


class Carte:
    """Une carte en cours de dessin : jeux de tuiles, calques du parc entier, objets posés (gardés ensuite dans les
    tailles où ils tiennent), tirages."""

    def __init__(self, pack, largeur, hauteur, graine):
        self.pack, self.L, self.H = pack, largeur, hauteur
        self.rng = random.Random(graine)
        self.jeux = []  # (nom, chemin relatif au pack, largeur, hauteur)
        self.premier = {}
        self.gid_collision = 1  # dernier jeu : collisions.png (une tuile rouge), après tous les autres
        self.sol = [[0] * largeur for _ in range(hauteur)]
        self.decors = [[0] * largeur for _ in range(hauteur)]
        self.devant = [[0] * largeur for _ in range(hauteur)]
        self.bloque = [[False] * largeur for _ in range(hauteur)]
        self.occupe_decors, self.occupe_devant = set(), set()
        # Objets posés (arbres, rochers, fleurs...), pour les cartes de chaque taille : chacun est une liste de tuiles
        # (calque, x, y, tuile, bloquée), gardé seulement s'il est tout entier dans la zone de cette taille.
        self.objets = []
        # Réglés par le générateur : sol où un objet peut se poser, lisière où il n'en pose pas
        self.libre_sol = self.dedans
        self.lisiere = set()
        self.planches = None
        # Tuiles animées (animations de Tiled, jouées par parc.js) : jeu → {tuile : [(image, durée en ms)...]}
        self.animations = {}

    # --- Jeux de tuiles (incorporés à la carte) ---
    def jeu(self, nom, chemin):
        largeur, hauteur = Image.open(os.path.join(self.pack, chemin)).size
        self.jeux.append((nom, chemin, largeur, hauteur))
        self.premier[nom] = self.gid_collision
        self.gid_collision += (largeur // 16) * (hauteur // 16)

    def animer(self, nom, i, images):
        """La tuile i du jeu est animée : images = [(tuile du même jeu, durée en ms)...]."""
        self.animations.setdefault(nom, {})[i] = images

    def t(self, nom, i):
        return self.premier[nom] + i

    def colonnes(self, nom):
        return next(w // 16 for n, _, w, _ in self.jeux if n == nom)

    def dedans(self, x, y):
        return 0 <= x < self.L and 0 <= y < self.H

    def tache(self, cx, cy, rx, ry):
        return {(x, y) for y in range(self.H) for x in range(self.L) if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1}

    def bordures(self, forme, nom, coins, interieur, centre, cases=None):
        """Bordures automatiques de la forme dans le sol ; centre : tuile du milieu (fonction) ; cases : celles à dessiner
        (toute la forme par défaut)."""
        for x, y in forme if cases is None else cases:
            i = bordure(forme, x, y, coins, interieur)
            self.sol[y][x] = centre() if i is None else self.t(nom, i)

    # --- Objets ---
    def bloc(self, nom, gauche, haut_, largeur, hauteur, x, y, rang_devant=0, bloque_lignes=(), bloque_cols=None):
        """Tuiles d'un bloc d'une planche, dans la carte : (calque, x, y, tuile, bloquée). Lignes < rang_devant : « Devant »."""
        cols = self.colonnes(nom)
        return [("devant" if j < rang_devant else "decors", x + i, y + j, self.t(nom, (haut_ + j) * cols + gauche + i),
                 j in bloque_lignes and (bloque_cols is None or i in bloque_cols))
                for j in range(hauteur) for i in range(largeur) if self.dedans(x + i, y + j)]

    def place_libre(self, largeur, hauteur, x, y, rang_devant, marge):
        """Le bloc tient-il en (x, y) : dans la carte, sur des cases encore vides de son calque ; ses lignes du dessous (hors
        « Devant ») sur un sol libre hors de la lisière, à <marge> cases au moins des autres décors."""
        for j in range(hauteur):
            for i in range(largeur):
                cx, cy = x + i, y + j
                calque = self.occupe_devant if j < rang_devant else self.occupe_decors
                if not self.dedans(cx, cy) or (cx, cy) in calque:
                    return False
                if j >= rang_devant and (not self.libre_sol(cx, cy) or (cx, cy) in self.lisiere):
                    return False
        zone = {(x + i + dx, y + j + dy) for j in range(rang_devant, hauteur) for i in range(largeur)
                for dx in range(-marge, marge + 1) for dy in range(-marge, marge + 1)}
        return not any(c in self.occupe_decors for c in zone)

    def poser(self, nom, gauche, haut_, largeur, hauteur, x, y, rang_devant=0, bloque_lignes=(), bloque_cols=None,
              verifier=True, marge=0, enregistrer=True):
        """Pose un bloc de tuiles d'une planche. Les lignes < rang_devant vont dans « Devant »."""
        if verifier and not self.place_libre(largeur, hauteur, x, y, rang_devant, marge):
            return False
        tuiles = self.bloc(nom, gauche, haut_, largeur, hauteur, x, y, rang_devant, bloque_lignes, bloque_cols)
        for calque, cx, cy, tuile, bloquee in tuiles:
            (self.devant if calque == "devant" else self.decors)[cy][cx] = tuile
            (self.occupe_devant if calque == "devant" else self.occupe_decors).add((cx, cy))
            if bloquee:
                self.bloque[cy][cx] = True
        if enregistrer:
            self.objets.append({"tuiles": tuiles})
        return True

    def essayer(self, n, fonction):
        for _ in range(n * 40):
            if n <= 0:
                break
            x, y = self.rng.randrange(self.L), self.rng.randrange(self.H)
            if fonction(x, y):
                n -= 1

    def boucher_poches(self, zone, decors_, bloque_, remplissage):
        """Poches enfermées (entre la lisière, l'eau et les objets) : remplissage(x, y) y met un décor, personne n'y va.
        Rend les cases libres gardées."""
        zx, zy, zl, zh = zone
        libres = {(x, y) for y in range(zy, zy + zh) for x in range(zx, zx + zl) if not bloque_[y][x]}
        gardees = relies(libres)
        for x, y in libres - gardees:
            bloque_[y][x] = True
            if not decors_[y][x]:
                decors_[y][x] = remplissage(x, y)
        return gardees

    # --- Écriture des .tmx et aperçus ---
    def ecrire_carte(self, sortie, fichier, depart, zone, sol_, decors_, devant_, bloque_, autres=()):
        """Écrit le .tmx d'une taille. autres : calques de tuiles en plus, [(nom, grille)], après « Collisions »."""
        os.makedirs(sortie, exist_ok=True)
        Image.new("RGBA", (16, 16), (230, 40, 40, 255)).save(os.path.join(sortie, "collisions.png"))
        relatif = os.path.relpath(self.pack, sortie).replace("\\", "/")
        L, H = self.L, self.H
        collisions = [[self.gid_collision if bloque_[y][x] else 0 for x in range(L)] for y in range(H)]
        morceaux = [
            '<?xml version="1.0" encoding="UTF-8"?>',
            f'<map version="1.10" tiledversion="1.12.2" orientation="orthogonal" renderorder="right-down" width="{L}" '
            f'height="{H}" tilewidth="16" tileheight="16" infinite="0" nextlayerid="{6 + len(autres)}" nextobjectid="3">',
        ]
        for nom, chemin_, w, h in self.jeux:
            morceaux.extend([f' <tileset firstgid="{self.premier[nom]}" name="{nom}" tilewidth="16" tileheight="16" '
                             f'tilecount="{(w // 16) * (h // 16)}" columns="{w // 16}">',
                             f'  <image source="{os.path.normpath(relatif + "/" + chemin_).replace(os.sep, "/")}" '
                             f'width="{w}" height="{h}"/>'])
            for i, images in sorted(self.animations.get(nom, {}).items()):
                morceaux.extend([f'  <tile id="{i}">', '   <animation>']
                                + [f'    <frame tileid="{image}" duration="{duree}"/>' for image, duree in images]
                                + ['   </animation>', '  </tile>'])
            morceaux.append(' </tileset>')
        morceaux.extend([f' <tileset firstgid="{self.gid_collision}" name="collisions" tilewidth="16" tileheight="16" '
                         f'tilecount="1" columns="1">',
                         '  <image source="collisions.png" width="16" height="16"/>',
                         ' </tileset>'])
        for i, (nom, grille, extra) in enumerate([("Sol", sol_, ""), ("Décors", decors_, ""), ("Devant", devant_, ""),
                                                 ("Collisions", collisions, ' visible="0" opacity="0.45"')]
                                                + [(n, g, "") for n, g in autres], 1):
            morceaux.extend([f' <layer id="{i}" name="{nom}" width="{L}" height="{H}"{extra}>',
                             f'  <data encoding="csv">\n{csv(grille)}\n</data>',
                             ' </layer>'])
        # « Départ » : la vue de départ (hauteur affichée, centre) ; « Parc » : là où vivent les Pokémon
        morceaux.extend([f' <objectgroup id="{5 + len(autres)}" name="Zones">',
                         rectangle(1, "Départ", depart),
                         rectangle(2, "Parc", zone),
                         ' </objectgroup>',
                         '</map>'])
        with open(os.path.join(sortie, fichier), "w", encoding="utf-8") as f:
            f.write("\n".join(morceaux) + "\n")

    def tuile_de(self, g):
        """Image de la tuile de numéro g (numéro de la carte : premier numéro de son jeu + rang dans sa planche)."""
        if self.planches is None:
            self.planches = {nom: Image.open(os.path.join(self.pack, c)).convert("RGBA") for nom, c, _, _ in self.jeux}
        nom = max((n for n in self.premier if self.premier[n] <= g), key=lambda n: self.premier[n])
        i, cols = g - self.premier[nom], self.colonnes(nom)
        return self.planches[nom].crop(((i % cols) * 16, (i // cols) * 16, (i % cols) * 16 + 16, (i // cols) * 16 + 16))

    def apercu(self, dossier, nom, grilles_, bloque_):
        """Aperçus (pour vérifier, pas pour Tiled) : la carte, et la même avec les cases bloquées en rouge
        (<nom>.png, <nom>_collisions.png)."""
        image = Image.new("RGBA", (self.L * 16, self.H * 16), (0, 0, 0, 255))
        for grille in grilles_:
            for y in range(self.H):
                for x in range(self.L):
                    g = grille[y][x]
                    if g and g < self.gid_collision:
                        image.alpha_composite(self.tuile_de(g), (x * 16, y * 16))
        os.makedirs(dossier, exist_ok=True)
        image.save(os.path.join(dossier, f"{nom}.png"))
        for y in range(self.H):
            for x in range(self.L):
                if bloque_[y][x]:
                    image.alpha_composite(Image.new("RGBA", (16, 16), (230, 40, 40, 110)), (x * 16, y * 16))
        image.save(os.path.join(dossier, f"{nom}_collisions.png"))
