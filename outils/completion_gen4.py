"""
Génère MonsutaPotaru/Services/Completion/Gen4CompletionData.cs d'après les désassemblages pokediamond, pokeplatinum et
pokeheartgold (pret).

    python outils/completion_gen4.py <dossier> MonsutaPotaru/Services/Completion/Gen4CompletionData.cs

<dossier> contient les clones de https://github.com/pret/pokediamond, pokeplatinum et pokeheartgold (git clone --depth 1).
À côté de ce script : completion_gen4_lieux.json, les noms français des lieux de la 4e génération (liste de PKHeX,
Resources/text/locations/gen4/text_hgss_00000_fr.txt) ; completion_gen4_ordre.json, les lieux des check-lists de
l'utilisateur dans leur ordre (celui de l'aventure). Les étapes de l'histoire, des rencontres et des quêtes sont choisies
ici d'après ces check-lists ; dresseurs, objets, objets cachés et cadeaux sont lus dans les dépôts.

Diamant / Perle n'est pas désassemblé : completion_gen4_extract.extract_dp lit ses fichiers binaires. Ses drapeaux n'ont
pas de nom : ceux de l'histoire reprennent le numéro du drapeau de Platine de même nom, vérifié dans les scripts de
Diamant / Perle (le script de la même carte doit le lever ou le tester), voir dp_flag.
"""
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from completion_gen4_extract import extract_dp, extract_hgss, extract_pt, dp_find, OP_SETFLAG, OP_CHECKFLAG  # noqa: E402
from completion_gen3 import anchored_rank  # noqa: E402

# Lieux absents des étapes d'histoire des check-lists (completion_gen4_ordre.json) : rangés avec un lieu voisin (même
# ville, même route), en chaîne si ce voisin manque aussi ; une route ou un chenal se range d'office près de son numéro.
ANCHORS = {
    'Aire de Combat': 'Ligue Pokémon', 'Mont Abrupt': 'Aire de Combat', 'Féli-Télé': 'Féli-Cité',
    'Temple Frimapic': 'Frimapic', 'Vieux Château': 'Forêt Vestigion', 'Île de Fer': 'Joliberges',
    'Grand Marais': 'Verchamps',
    'Atrium de Combat': 'Oliville', 'Caverne Azurée': 'Azuria', 'Caves Jumelles': 'Route 32', 'Grotte': 'Route 10',
    'Grotte Argentée': 'Mont Argenté', 'Mont Creuset': 'Route 42', 'Parc Naturel': 'Route 35',
    'Plateau Indigo': 'Route Victoire',
    # Routes de Kanto d'HG / SS : sinon rangées par leur numéro près des routes de Johto (25 près de 30)
    'Route 2': 'Jadielle', 'Route 3': 'Argenta', 'Route 4': 'Azuria', 'Route 5': 'Azuria', 'Route 6': 'Carmin sur Mer',
    'Route 7': 'Céladopole', 'Route 8': 'Safrania', 'Route 9': 'Azuria', 'Route 10': 'Azuria', 'Route 11': 'Carmin sur Mer',
    'Route 12': 'Parmanie', 'Route 13': 'Parmanie', 'Route 14': 'Parmanie', 'Route 15': 'Parmanie', 'Route 16': 'Céladopole',
    'Route 17': 'Céladopole', 'Route 18': 'Parmanie', 'Chenal 19': 'Parmanie', 'Chenal 20': 'Parmanie',
    'Chenal 21': "Cramois'Île", 'Route 22': 'Jadielle', 'Route 24': 'Azuria', 'Route 25': 'Azuria',
    'Route 26': 'Plateau Indigo', 'Route 27': 'Plateau Indigo', 'Route 28': 'Mont Argenté',
    'Boutique de Vélo': 'Vestigion', 'Bâtiment Galaxie de Vestigion': 'Vestigion', 'Café': 'Route 210',
    'Centre Commercial de Voilaroc': 'Voilaroc', 'Fleuriste': 'Floraville', 'Grand Lac': 'Route 213',
    'Grotte Ruinemaniac': 'Route 214', 'Restaurant': 'Rive Lac Courage', 'Rive Lac Vérité': 'Lac Vérité',
    'École de Dresseurs': 'Féli-Cité', 'Parc des Combats': 'Aire de Combat', 'Aire de Détente': 'Aire de Combat',
    'Aire de Survie': 'Aire de Combat', 'Forge Fuego': 'Route 205', 'Grotte Revêche': 'Route 206',
    'Manoir Pokémon': 'Route 212', 'Rive Lac Savoir': 'Lac Savoir', 'Square Paisible': 'Unionpolis',
    'Tunnel Ruinemaniac': 'Route 214',
    'Dôme Pokéathlon': 'Route 36', 'Portail Falaise': 'Route 47', 'Portail Safari': 'Route 48', 'Sentier Carillon': 'Rosalia',
    'Antre Noir': 'Route 31', 'Bourg Geon': 'Bourg Géon', 'Chutes Tohjo': 'Route 27', 'Forêt de Jade': 'Jadielle',
    'Grotte Falaise': 'Route 47',
}

# Classes de dresseurs comptées ailleurs (badges, Ligue, histoire) ; combats scénarisés sans drapeau de dresseur.
STORY_CLASSES = re.compile(r'(LEADER|ELITE_FOUR|CHAMPION|COMMANDER|GALACTIC_BOSS|EXECUTIVE|ROCKET_BOSS|PKMN_TRAINER_RED)')
VARS_START = 0x4000

# Noms de PKHeX abrégés par le jeu (place limitée) : rendus en entier.
PLACE_FIXES = {
    'Endroit mystér.': 'Endroit mystérieux', 'Les Eoliennes': 'Les Éoliennes', 'Ile Pleine Lune': 'Île Pleine Lune',
    'Grotte Ruineman.': 'Grotte Ruinemaniac', 'Tunnel Ruineman.': 'Tunnel Ruinemaniac', 'Ile de Fer': 'Île de Fer',
    'Ile Nouvellune': 'Île Nouvellune', 'Ecole Dresseurs': 'École de Dresseurs', 'Bâtiment Etrange': 'Bâtiment Étrange',
    'C. Com. Voilaroc': 'Centre Commercial de Voilaroc', 'Bibl. Joliberges': 'Bibliothèque de Joliberges',
    'Salle MOTISMA': 'Salle Motisma', 'Team G Vestigion': 'Bâtiment Galaxie de Vestigion', 'Ecorcia': 'Écorcia',
    'Ebènelle': 'Ébènelle', 'Cramois’Ile': "Cramois'Île", 'Cave TAUPIQUEUR': 'Cave Taupiqueur', 'Iles Ecume': 'Îles Écume',
    'Tour CHETIFLOR': 'Tour Chétiflor', 'Ruines d’Alpha': "Ruines d'Alpha", 'Puits RAMOLOSS': 'Puits Ramoloss',
    "Tourb’Iles": "Tourb'Îles", 'Atrium Combat': 'Atrium de Combat',
}


def place(places, index):
    name = places[index]
    return PLACE_FIXES.get(name, name)


def floor(token):
    m = re.fullmatch(r'(b?)(\d+)f', token)
    if not m:
        return None
    n = int(m.group(2))
    if m.group(1):
        return f'sous-sol {n}'
    return 'rez-de-chaussée' if n == 1 else ('1er étage' if n == 2 else f'{n - 1}e étage')


def detail(map_name):
    """Précision tirée du nom de la carte (« oreburgh_city_gym » → « Arène », « T22GYM0101 » → « Arène »)."""
    low = map_name.lower()
    if re.fullmatch(r'[a-z]\d+[a-z]*\d*', low):  # HG/SS : T22GYM0101, R32PC0101, D36R0101
        if 'gym' in low:
            return 'Arène'
        if re.search(r'pc\d', low):
            return 'Centre Pokémon'
        if re.search(r'fs\d', low):
            return 'Boutique'
        return None
    tokens = low.split('_')
    if 'gym' in tokens:
        return 'Arène'
    if any(t in ('pokecenter', 'pokemon_center') for t in tokens) or 'pokecenter' in low or 'pokemon_center' in low:
        return 'Centre Pokémon'
    if 'department' in tokens:
        f = floor(tokens[-1])
        return 'Centre commercial' + (f', {f}' if f else '')
    if 'mart' in tokens or 'pokemart' in tokens:
        return 'Boutique'
    if any(t.endswith('house') for t in tokens):
        return 'maison'
    return floor(tokens[-1])


def cs(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"' if s is not None else 'null'


def build(d, game, places, checklist, cur):
    k = d['k']
    rows = []
    initial = {f if isinstance(f, int) else k.get(f) for f in d['initial']}

    def num(flag):
        return flag if isinstance(flag, int) else k[flag]

    def zone_of(map_name):
        idx = d['mapsec'].get(map_name)
        if idx is None or idx >= len(places):
            raise SystemExit(f'Lieu inconnu pour {map_name} ({game})')
        return place(places, idx)

    def order(zone, strict):
        """Rang du lieu (completion_gen3.anchored_rank) ; dresseurs et objets (strict) : chaque lieu doit en avoir un."""
        rank = anchored_rank(zone, checklist, ANCHORS)
        if rank is None and strict:
            raise SystemExit(f'Lieu absent de la check-list de {game} : {zone} (lui donner un voisin dans ANCHORS)')
        return 10_000 if rank is None else rank

    def add(section, zone, det, done, label=None, missed=None, team=None, item=0, tm=0, species=0, form=0, count=0, sort=0):
        rows.append(dict(section=section, zone=zone, detail=det, done=done, label=label, missed=missed, team=team, item=item,
                         tm=tm, species=species, form=form, count=count,
                         sort=(order(zone, section in ('Trainers', 'Items')), zone, sort)))

    S, D, I, U, Q = 'Story', 'Trainers', 'Items', 'Encounters', 'Quests'
    for i, (zone, det, label, cond) in enumerate(cur['story']):
        add(S, zone, det, cond, label, sort=i)

    # ---------- Dresseurs : seuls ceux qui ont un drapeau (combat de vue, ou drapeau levé par un script) ----------
    flagged = cur['trainer_flags']  # identifiants de dresseurs dont un script lève le drapeau
    groups = {}
    for t in d['trainers']:
        if t['cls'] and STORY_CLASSES.search(t['cls']):
            continue
        if not t.get('object') and t['id'] not in flagged:
            continue  # combat scénarisé (rival…) : aucun drapeau
        key = (t['map'], 'RIVAL' if 'RIVAL' in (t['cls'] or '') else t['id'])
        groups.setdefault(key, []).append(t)
    for (map_name, _), ts in groups.items():
        t = ts[0]
        done = '|'.join(f'E:{d["trainer_start"] + x["num"]}' for x in ts)
        hide = t['hide']
        missed = None
        if hide is not None and not (isinstance(hide, str) and re.match(r'FLAG_(MAP_LOCAL|TEMP|UNK|MAPTEMP|NOTHING)', hide)):
            # Raté : caché après le départ et jamais réaffiché (pas un dresseur qui ne vient qu'à certaines heures)
            if num(hide) not in initial and num(hide) not in d['cleared']:
                missed = f'E:{num(hide)}'
        team = ','.join(f'{sp}:{lv}' for lv, sp in t['team']) or None
        label = 'Rival' if len(ts) > 1 else (None if team else 'Dresseur')
        add(D, zone_of(map_name), detail(map_name), done, label, missed=missed, team=team if len(ts) == 1 else None,
            sort=d['trainer_start'] + t['num'])

    # ---------- Objets ----------
    tm_of = {}
    for name, value in k.items():
        m = re.fullmatch(r'ITEM_(TM|HM)(\d\d)', name)
        if m:
            tm_of[value] = int(m.group(2)) + (92 if m.group(1) == 'HM' else 0)
    seen = set()

    def item(x, kind, extra):
        flag = num(x['flag'])
        # Drapeaux 1 à 64 : propres à la carte, remis à zéro en la quittant (0 : aucun) ; au-delà de daily_start : du jour
        if flag <= 64 or flag in seen or flag in initial or x['item'] not in k or flag >= cur['daily_start']:
            return
        if isinstance(x['flag'], str) and re.match(r'FLAG_(HIDE|MAP_LOCAL|TEMP|UNK|MAPTEMP|DAILY)', x['flag']) and kind == 2:
            return  # cadeau suivi d'un drapeau qui cache un personnage : pas une trace sûre du cadeau
        seen.add(flag)
        det = ', '.join(p for p in (detail(x['map']), extra) if p) or None
        value = k[x['item']]
        add(I, zone_of(x['map']), det, f'E:{flag}', item=0 if value in tm_of else value, tm=tm_of.get(value, 0),
            count=x['count'] if x['count'] > 1 else 0, sort=kind * 100_000 + flag)

    for x in d['ground']:
        item(x, 0, None)
    for x in d['hidden']:
        item(x, 1, 'caché')
    for x in d['gifts']:
        if x['item'] not in cur['gift_exclude']:
            item(x, 2, 'cadeau')

    # ---------- Rencontres et quêtes ----------
    for i, (zone, det, species, cond, label) in enumerate(cur['encounters']):
        add(U, zone, det, cond, label, species=k[species], sort=i)
    for i, (zone, det, label, cond, species, form) in enumerate(cur['quests']):
        add(Q, zone, det, cond, label, species=k[species] if species else 0, form=form, sort=i)

    section_order = {s: n for n, s in enumerate((S, D, I, U, Q))}
    rows.sort(key=lambda r: (section_order[r['section']],) + ((r['sort'][2],) if r['section'] in (S, U, Q) else r['sort']))
    return rows


# ---------------------------------------------------------------------------------------------------------------------
# Étapes choisies à la main

class Conds:
    """Conditions écrites avec les noms des drapeaux du jeu : E:n (drapeau), W:n:v (variable n ≥ v), B:n (badge n),
    N (Pokédex National), D:n (Pokémon capturé), Z:n (forme n de Zarbi au Pokédex), M:espèce:forme (forme au Pokédex)."""

    def __init__(self, k, flag_number=None):
        self.k = k
        self.flag_number = flag_number or (lambda name: k[name])

    def f(self, *names):
        """Tous ces drapeaux levés."""
        for n in names:
            if n not in self.k and not n.startswith('#'):
                raise SystemExit(f'Drapeau inconnu : {n}')
        return '+'.join(f'E:{self.flag_number(n)}' for n in names)

    def a(self, *names):
        """Un de ces drapeaux levé."""
        return '|'.join(self.f(n) for n in names)

    def w(self, var, value):
        return f'W:{self.k[var] - VARS_START}:{value}'

    def t(self, trainer, start):
        return f'E:{start + self.k[trainer]}'


SINNOH_BADGES = {0: ('Charbon', 'Pierrick', 'Charbourg'), 1: ('Forêt', 'Flo', 'Vestigion'), 2: ('Pavé', 'Mélina', 'Voilaroc'),
                 3: ('Palustre', 'Lovis', 'Verchamps'), 4: ('Relique', 'Kiméra', 'Unionpolis'), 5: ('Mine', 'Charles', 'Joliberges'),
                 6: ('Glaçon', 'Gladys', 'Frimapic'), 7: ('Phare', 'Tanguy', 'Rivamar')}


def sinnoh_badge(n):
    name, leader, city = SINNOH_BADGES[n]
    return (city, 'Arène', f'Badge {name} ({leader})', f'B:{n}')


def sinnoh_story(c, platinum):
    """Histoire de Diamant / Perle (platinum=False) et de Platine : noms des drapeaux de Platine (numéros vérifiés en
    Diamant / Perle par dp_flag)."""
    s = [('Littorella', 'Laboratoire du Prof. Sorbier', 'Recevoir le Pokédex', c.f('FLAG_HAS_POKEDEX')),
         ('Féli-Cité', None, 'Recevoir la Pokémontre (trouver les trois clowns)', c.f('FLAG_RECEIVED_POKETCH')),
         sinnoh_badge(0)]
    if platinum:
        s.append(('Pré Floraville', None, 'Chasser la Team Galaxie du Pré Floraville', c.f('FLAG_DEFEATED_FLOAROMA_MEADOW_GRUNTS')))
    s += [('Les Éoliennes', None, 'Battre Mars aux Éoliennes', c.f('FLAG_HIDE_ROUTE_205_SOUTH_GRUNTS')),
          ('Forêt Vestigion', None, 'Traverser la forêt avec Sara', c.f('FLAG_TRAVELED_WITH_CHERYL')),
          sinnoh_badge(1),
          ('Vestigion', 'Bâtiment Galaxie', 'Chasser la Team Galaxie du bâtiment de Vestigion', c.f('FLAG_TEAM_GALACTIC_LEFT_ETERNA_BUILDING')),
          ('Vestigion', 'Boutique de Vélo', 'Recevoir la Bicyclette', c.f('FLAG_RECEIVED_BICYCLE'))]
    order = [4, 2, 3] if platinum else [2, 3, 4]  # Platine : Kiméra avant Mélina et Lovis
    for n in order:
        s.append(sinnoh_badge(n))
        if n == 2:
            s.append(('Voilaroc', 'Entrepôt Galaxie', "Ouvrir l'entrepôt Galaxie (Clé Stockage)", c.f('FLAG_USED_STORAGE_KEY')))
    s += [('Route 210', None, 'Soigner les Psykokwak avec la Potionsecret', c.f('FLAG_USED_SECRETPOTION')),
          sinnoh_badge(5),
          ('Lac Courage', None, 'Battre Saturne au Lac Courage', c.f('FLAG_DEFEATED_COMMANDER_SATURN_VALOR_CAVERN')),
          ('Lac Vérité', None, 'Battre Mars au Lac Vérité', c.f('FLAG_TEAM_GALACTIC_LEFT_LAKE_VERITY')),
          sinnoh_badge(6),
          ('QG Galaxie', None, 'Battre Saturne et libérer les Pokémon des lacs', c.f('FLAG_DEFEATED_GALACTIC_HQ_CONTROL_ROOM_SATURN'))]
    if platinum:
        s.append(('Monde Distorsion', None, 'Battre Hélio dans le Monde Distorsion',
                  c.w('VAR_DISTORTION_WORLD_PROGRESS', 13)))
    s.append(sinnoh_badge(7))
    for who, flag in (('Aaron', 'AARON'), ('Terry', 'BERTHA'), ('Adrien', 'FLINT'), ('Lucio', 'LUCIAN')):
        s.append(('Ligue Pokémon', None, f'Battre {who} (Conseil 4)', c.a(f'FLAG_DEFEATED_{flag}', 'FLAG_GAME_COMPLETED')))
    s += [('Ligue Pokémon', None, 'Battre la Maître Cynthia et entrer au Panthéon', c.f('FLAG_GAME_COMPLETED')),
          ('Littorella', None, 'Recevoir le Pokédex National', 'N')]
    if platinum:  # Diamant / Perle : pas de drapeau connu pour le premier voyage
        s += [('Aire de Combat', None, "Prendre le bateau pour l'Aire de Combat", c.f('FLAG_SAILED_TO_BATTLE_ZONE')),
              ('Mont Abrupt', None, 'Faire arrêter Pluton au Mont Abrupt', c.f('FLAG_ARRESTED_CHARON_STARK_MOUNTAIN'))]
    return s


def sinnoh_encounters(c, platinum):
    if platinum:
        u = [('Monde Distorsion', None, 'SPECIES_GIRATINA', c.f('FLAG_CAUGHT_GIRATINA'), None),
             ('Colonnes Lances', 'après la Ligue', 'SPECIES_DIALGA', c.f('FLAG_CAUGHT_DIALGA'), None),
             ('Colonnes Lances', 'après la Ligue', 'SPECIES_PALKIA', c.f('FLAG_CAUGHT_PALKIA'), None)]
    else:
        # Diamant : Dialga ; Perle : Palkia (un seul drapeau pour les deux)
        u = [('Colonnes Lances', None, 'SPECIES_DIALGA', c.f('FLAG_CAUGHT_DIALGA'), 'Dialga (Diamant) ou Palkia (Perle)'),
             ('Grotte Retour', None, 'SPECIES_GIRATINA', c.f('FLAG_CAUGHT_GIRATINA'), None)]
    u += [('Lac Savoir', None, 'SPECIES_UXIE', c.f('FLAG_CAUGHT_UXIE'), None),
          ('Lac Courage', None, 'SPECIES_AZELF', c.f('FLAG_CAUGHT_AZELF'), None),
          ('Lac Vérité', 'errant dans Sinnoh', 'SPECIES_MESPRIT', 'D:481', None),
          ('Île Pleine Lune', 'errant dans Sinnoh', 'SPECIES_CRESSELIA', 'D:488', None),
          ('Mont Abrupt', None, 'SPECIES_HEATRAN', c.f('FLAG_CAUGHT_HEATRAN'), None),
          ('Temple Frimapic', None, 'SPECIES_REGIGIGAS', c.f('FLAG_CAUGHT_REGIGIGAS'), None),
          ('Vieux Château', None, 'SPECIES_ROTOM', c.f('FLAG_CAUGHT_OLD_CHATEAU_ROTOM'), None),
          ('Les Éoliennes', 'le vendredi', 'SPECIES_DRIFLOON', 'D:425', None),
          ('Route 209', 'Tour Perdue, Clé de Voûte', 'SPECIES_SPIRITOMB', 'D:442', None)]
    if platinum:
        u += [('Ruines de Fer', 'avec Regigigas', 'SPECIES_REGISTEEL', 'D:379', None),
              ('Ruines Iceberg', 'avec Regigigas', 'SPECIES_REGICE', 'D:378', None),
              ('Ruines Pic Roche', 'avec Regigigas', 'SPECIES_REGIROCK', 'D:377', None),
              ('Sinnoh', 'errant, après la Ligue', 'SPECIES_ARTICUNO', 'D:144', None),
              ('Sinnoh', 'errant, après la Ligue', 'SPECIES_ZAPDOS', 'D:145', None),
              ('Sinnoh', 'errant, après la Ligue', 'SPECIES_MOLTRES', 'D:146', None)]
    # Distributions (choix de l'utilisateur : les vraies seulement ; pas la Flûte Azur). La Carte Membre et la Lettre
    # n'ont été distribuées que pour Platine.
    if platinum:
        u += [('Île Nouvellune', 'Carte Membre', 'SPECIES_DARKRAI', c.f('FLAG_CAUGHT_DARKRAI'), None),
              ('Paradis Fleuri', 'Lettre', 'SPECIES_SHAYMIN', c.f('FLAG_CAUGHT_SHAYMIN'), None)]
    # Cadeaux et échanges
    if platinum:
        u += [('Vestigion', 'œuf, Cynthia', 'SPECIES_TOGEPI', 'D:175', None),
              ('Unionpolis', 'cadeau', 'SPECIES_EEVEE', c.f('FLAG_RECEIVED_HEARTHOME_CITY_NORTHWEST_HOUSE_EEVEE'), None),
              ('Voilaroc', 'cadeau', 'SPECIES_PORYGON', c.f('FLAG_RECEIVED_VEILSTONE_CITY_NORTHEAST_HOUSE_PORYGON'), None)]
    else:
        u += [('Unionpolis', 'cadeau', 'SPECIES_EEVEE', c.f('FLAG_RECEIVED_HEARTHOME_CITY_NORTHWEST_HOUSE_EEVEE'), None),
              ('Route 209', 'œuf', 'SPECIES_HAPPINY', 'D:440', None)]
    u += [('Île de Fer', 'œuf, Armand', 'SPECIES_RIOLU', c.f('FLAG_RECEIVED_RIOLU_EGG_FROM_RILEY'), None),
          ('Charbourg', 'échange', 'SPECIES_ABRA', c.f('FLAG_TRADED_FOR_KAZZA_ABRA'), None),
          ('Vestigion', 'échange', 'SPECIES_CHATOT', c.f('FLAG_TRADED_FOR_CHARAP_CHATOT'), None),
          ('Frimapic', 'échange', 'SPECIES_HAUNTER', c.f('FLAG_TRADED_FOR_GASPAR_HAUNTER'), None),
          ('Chenal 226', 'échange', 'SPECIES_MAGIKARP', c.f('FLAG_TRADED_FOR_FOPPA_MAGIKARP'), None)]
    return u


UNOWN = [chr(65 + n) for n in range(26)] + ['!', '?']


def sinnoh_quests(c, platinum):
    q = [('Unionpolis', 'Salle Concours', f'Gagner le concours {cat} de rang Master', c.f(f'FLAG_CONTEST_MASTER_{flag}'), None, 0)
         for cat, flag in (('Sang-Froid', 'COOL'), ('Beauté', 'BEAUTY'), ('Grâce', 'CUTE'), ('Intelligence', 'SMART'),
                           ('Robustesse', 'TOUGH'))]
    if platinum:
        for n, form in enumerate(('Chaleur', 'Lavage', 'Froid', 'Hélice', 'Tonte'), 1):
            q.append(('Vestigion', 'Bâtiment Galaxie', f'Motisma forme {form} au Pokédex', f'M:479:{n}', 'SPECIES_ROTOM', n))
        q += [('Monde Distorsion', 'Orbe Platiné', 'Giratina Forme Originelle au Pokédex', 'M:487:1', 'SPECIES_GIRATINA', 1),
              ('Floraville', 'Gracidée', 'Shaymin Forme Céleste au Pokédex', 'M:492:1', 'SPECIES_SHAYMIN', 1)]
    q += [('Ruines Bonville', None, f'Zarbi {u} au Pokédex', f'Z:{n}', 'SPECIES_UNOWN', n) for n, u in enumerate(UNOWN)]
    return q


JOHTO_BADGES = [('Zéphyr', 'Albert', 'Mauville'), ('Essaim', 'Hector', 'Écorcia'), ('Plaine', 'Blanche', 'Doublonville'),
                ('Brume', 'Mortimer', 'Rosalia'), ('Choc', 'Chuck', 'Irisia'), ('Minéral', 'Jasmine', 'Oliville'),
                ('Glacier', 'Frédo', 'Acajou'), ('Lever', 'Sandra', 'Ébènelle'),
                ('Roche', 'Pierre', 'Argenta'), ('Cascade', 'Ondine', 'Azuria'), ('Foudre', 'Major Bob', 'Carmin sur Mer'),
                ('Prisme', 'Érika', 'Céladopole'), ('Âme', 'Jeannine', 'Parmanie'), ('Marais', 'Morgane', 'Safrania'),
                ('Volcan', 'Auguste', "Îles Écume"), ('Terre', 'Blue', 'Jadielle')]


def hgss_badge(n):
    name, leader, city = JOHTO_BADGES[n]
    return (city, 'Arène', f'Badge {name} ({leader})', f'B:{n}')


def hgss_story(c):
    s = [('Bourg Geon', 'Labo du Prof. Orme', 'Recevoir son premier Pokémon', c.f('FLAG_GOT_STARTER')),
         ('Ville Griotte', None, 'Recevoir le Pokédex', c.f('FLAG_GOT_POKEDEX')),
         hgss_badge(0),
         ('Écorcia', 'Puits Ramoloss', 'Chasser la Team Rocket du Puits Ramoloss', c.f('FLAG_BEAT_AZALEA_ROCKETS')),
         hgss_badge(1), hgss_badge(2), hgss_badge(3),
         ('Oliville', 'Phare', 'Rapporter la Potionsecret pour Ampharos', c.f('FLAG_GOT_SECRETPOTION')),
         hgss_badge(4), hgss_badge(5),
         ('Acajou', 'Repaire Rocket', 'Démanteler le Repaire Rocket', c.f('FLAG_ROCKET_HIDEOUT_CLEARED')),
         hgss_badge(6),
         ('Doublonville', 'Tour Radio', 'Libérer la Tour Radio', c.f('FLAG_BEAT_RADIO_TOWER_ROCKETS')),
         hgss_badge(7)]
    for who, flag in (('Clément', 'WILL'), ('Koga', 'KOGA'), ('Aldo', 'BRUNO'), ('Marion', 'KAREN')):
        s.append(('Plateau Indigo', 'Ligue Pokémon', f'Battre {who} (Conseil 4)', c.a(f'FLAG_DEFEATED_{flag}', 'FLAG_GAME_CLEAR')))
    s += [('Plateau Indigo', 'Ligue Pokémon', 'Battre le Maître Peter et entrer au Panthéon', c.f('FLAG_GAME_CLEAR')),
          ('Bourg Geon', 'Labo du Prof. Orme', 'Recevoir le Passe Bateau', c.f('FLAG_GOT_SS_TICKET_FROM_ELM')),
          ('Centrale', None, 'Rétablir le courant de la Centrale', c.f('FLAG_RESTORED_POWER'))]
    s += [hgss_badge(n) for n in range(8, 16)]
    s += [('Bourg Palette', None, 'Recevoir le Pokédex National', 'N'),
          ('Bourg Palette', 'Prof. Chen', "Obtenir l'accès au Mont Argenté", c.f('FLAG_UNLOCKED_MT_SILVER')),
          ('Mont Argenté', None, 'Battre Red', c.f('FLAG_UNK_25F'))]
    return s


def hgss_encounters(c):
    u = [('Tour Carillon', None, 'SPECIES_HO_OH', c.f('FLAG_CAUGHT_HO_OH'), 'Ho-Oh (HeartGold)'),
         ('Tourb\'Îles', None, 'SPECIES_LUGIA', c.f('FLAG_CAUGHT_LUGIA'), 'Lugia (SoulSilver)'),
         ('Tour Chétiflor', None, 'SPECIES_SUICUNE', c.f('FLAG_CAUGHT_SUICUNE'), None),
         ('Johto', 'errant', 'SPECIES_RAIKOU', 'D:243', None),
         ('Johto', 'errant', 'SPECIES_ENTEI', 'D:244', None),
         ('Route 36', None, 'SPECIES_SUDOWOODO', c.f('FLAG_CAUGHT_SUDOWOODO'), None),
         ('Lac Colère', None, 'SPECIES_GYARADOS', c.f('FLAG_CAUGHT_RED_GYARADOS'), 'Léviator rouge'),
         ('Route 11', None, 'SPECIES_SNORLAX', c.f('FLAG_CAUGHT_SNORLAX'), None),
         ('Centrale', None, 'SPECIES_ZAPDOS', c.f('FLAG_CAUGHT_ZAPDOS'), None),
         ('Îles Écume', None, 'SPECIES_ARTICUNO', c.f('FLAG_CAUGHT_ARTICUNO'), None),
         ('Mont Argenté', None, 'SPECIES_MOLTRES', c.f('FLAG_CAUGHT_MOLTRES'), None),
         ('Caverne Azurée', None, 'SPECIES_MEWTWO', c.f('FLAG_CAUGHT_MEWTWO'), None),
         ('Grotte Falaise', 'Orbe Bleu / Orbe Rouge', 'SPECIES_KYOGRE', c.a('FLAG_CAUGHT_KYOGRE', 'FLAG_CAUGHT_GROUDON'),
          'Kyogre (SoulSilver) ou Groudon (HeartGold)'),
         ('Tour Enfouie', 'Orbe Jade', 'SPECIES_RAYQUAZA', c.f('FLAG_CAUGHT_RAYQUAZA'), None),
         ('Kanto', 'errant, après la Ligue', 'SPECIES_LATIAS', 'D:380|D:381', 'Latias (HeartGold) ou Latios (SoulSilver)'),
         # Distributions (les vraies) : GS Ball (Celebi), Arceus (Ruines Sinjoh)
         ('Bois aux Chênes', 'GS Ball', 'SPECIES_CELEBI', 'D:251', None),
         ('Ruines Sinjoh', 'avec Arceus', 'SPECIES_DIALGA', 'D:483|D:484|D:487', 'Dialga, Palkia ou Giratina'),
         # Cadeaux
         ('Mauville', 'œuf, assistant du Prof. Orme', 'SPECIES_TOGEPI', c.f('FLAG_GOT_EGG_FROM_ELMS_ASSISTANT'), None),
         ('Doublonville', 'Léo', 'SPECIES_EEVEE', c.f('FLAG_GOT_EEVEE_FROM_BILL'), None),
         ('Irisia', 'prêt', 'SPECIES_SHUCKLE', c.f('FLAG_GOT_LOAN_SHUCKLE'), None),
         ('Mont Creuset', 'Karatéka', 'SPECIES_TYROGUE', c.f('FLAG_GOT_TYROGUE_FROM_KARATE_KING'), None),
         ('Antre du Dragon', 'Doyen', 'SPECIES_DRATINI', c.a('FLAG_GOT_DRATINI_FROM_MASTER_JUST_NOW', 'FLAG_GOT_DRATINI_FROM_MASTER_LONG_AGO'), None),
         ('Mauville', 'œuf, Centre Pokémon', 'SPECIES_MAREEP',
          c.a('FLAG_GOT_MAREEP_EGG_FROM_PRIMO', 'FLAG_GOT_WOOPER_EGG_FROM_PRIMO', 'FLAG_GOT_SLUGMA_EGG_FROM_PRIMO'),
          'Œuf de Wattouat, Axoloto ou Limagma'),
         ('Safrania', 'Sylphe SARL, Pierre Rochard', 'SPECIES_TREECKO', c.f('FLAG_GOT_HOENN_STARTER_FROM_STEVEN'),
          'Arcko, Poussifeu ou Gobou'),
         # Échanges
         ('Mauville', 'échange', 'SPECIES_ONIX', c.f('FLAG_TRADE_VIOLET_CITY_BELLSPROUT_ONIX'), None),
         ('Doublonville', 'échange', 'SPECIES_MACHOP', c.f('FLAG_TRADE_GOLDENROD_CITY_DROWZEE_MACHOP'), None),
         ('Oliville', 'échange', 'SPECIES_VOLTORB', c.f('FLAG_TRADE_OLIVINE_CITY_KRABBY_VOLTORB'), None),
         ('Ébènelle', 'échange', 'SPECIES_DODRIO', c.f('FLAG_TRADE_BLACKTHORN_CITY_DRAGONAIR_DODRIO'), None),
         ('Argenta', 'échange', 'SPECIES_XATU', c.f('FLAG_TRADE_PEWTER_CITY_HAUNTER_XATU'), None),
         ('Centrale', 'échange', 'SPECIES_MAGNETON', c.f('FLAG_TRADE_POWER_PLANT_DUGTRIO_MAGNETON'), None),
         ('Carmin sur Mer', 'échange, Major Bob', 'SPECIES_PIKACHU', c.f('FLAG_TRADE_LT_SURGE_PIKACHU'), None),
         ('Argenta', 'échange, Pierre', 'SPECIES_RHYHORN', c.f('FLAG_TRADE_BROCK_BONSLY_RHYHORN'), None),
         ('Oliville', 'échange, Jasmine', 'SPECIES_STEELIX', c.f('FLAG_TRADE_JASMINE_STEELIX'), None),
         ('Safrania', 'échange, Pierre Rochard', 'SPECIES_BELDUM', c.f('FLAG_TRADE_STEVEN_FORRETRESS_BELDUM'), None)]
    return u


def hgss_quests(c):
    q = [("Ruines d'Alpha", 'puzzle', f'Résoudre le puzzle {name}', c.f(f'FLAG_SYS_ALPH_PUZZLE_{flag}'), None, 0)
         for name, flag in (('Kabuto', 'KABUTO'), ('Ptéra', 'AERODACTYL'), ('Amonita', 'OMANYTE'), ('Ho-Oh', 'HO_OH'))]
    q += [("Ruines d'Alpha", 'salle cachée', f'Ouvrir la salle cachée ({how})', c.f(f'FLAG_OPENED_ALPH_{flag}_SECRET_ROOM'), None, 0)
          for how, flag in (('Corde Sortie', 'ESCAPE_ROPE'), ('Flash', 'FLASH'), ('Pierre Eau', 'WATER_STONE'), ('Ho-Oh', 'HO_OH'))]
    q.append(("Ruines d'Alpha", 'Centre de Recherche', 'Recevoir le Carnet Zarbi', c.f('FLAG_GOT_UNOWN_REPORT'), None, 0))
    q += [("Ruines d'Alpha", None, f'Zarbi {u} au Pokédex', f'Z:{n}', 'SPECIES_UNOWN', n) for n, u in enumerate(UNOWN)]
    q.append(('Zone de Combat', None, 'Obtenir les quatre Photos des Meneurs', c.f('FLAG_GOT_ALL_FOUR_FRONTIER_PRINTS'), None, 0))
    return q


# ---------------------------------------------------------------------------------------------------------------------

def dp_flag_resolver(pt_k, dp_scripts):
    """Drapeau de Diamant / Perle d'après le nom de Platine : même numéro, à condition qu'un script de Diamant / Perle le
    lève ou le teste (sinon le numéro a changé : arrêt, à corriger ici)."""
    used = set()
    for data in dp_scripts.values():
        used |= {v[0] for _, v in dp_find(data, OP_SETFLAG, None)}
        used |= {v[0] for _, v in dp_find(data, OP_CHECKFLAG, None)}
    def number(name):
        if name in DP_FLAGS:
            return DP_FLAGS[name]
        n = pt_k[name]
        if n not in used and n < 2400:
            MISSING.append(f'{name} ({n})')
        return n
    return number


# Drapeaux de Diamant / Perle dont le numéro diffère de Platine (liste de PKHeX, flags_dp : « Dialga/Palkia Capturé »)
# et scripts binaires : premier SetFlag après le combat contre Mars aux Éoliennes, dresseur 295, carte VALLEY_WINDWORKS_INTERIOR)
DP_FLAGS = {'FLAG_CAUGHT_DIALGA': 351, 'FLAG_CAUGHT_PALKIA': 351, 'FLAG_HIDE_ROUTE_205_SOUTH_GRUNTS': 452}
MISSING = []


def emit(games, tm_moves, out):
    lines = ['// <auto-generated>',
             "// Généré par outils/completion_gen4.py d'après les désassemblages pokediamond, pokeplatinum et pokeheartgold (pret) : ne",
             '// pas modifier à la main, changer le script et le relancer.',
             '// </auto-generated>',
             '',
             'namespace MonsutaPotaru.Services;',
             '',
             'public static class Gen4CompletionData',
             '{',
             '    /// <summary>Capacité de chaque CT (1 à 92), puis de chaque CS (1 à 8) : numéros des capacités.</summary>',
             '    public static readonly ushort[] TmMoves = [' + ', '.join(str(m) for m in tm_moves) + '];',
             '']
    for name, doc, rows in games:
        lines.append(f'    /// <summary>Étapes de {doc} (le Pokédex national est ajouté à part : les 493).</summary>')
        lines.append(f'    public static readonly CompletionRow[] {name} =')
        lines.append('    [')
        for r in rows:
            a_ = [f'CompletionSection.{r["section"]}', cs(r['zone']), cs(r['detail']), cs(r['done'])]
            n = []
            for key, field in (('label', 'Label'), ('missed', 'Missed'), ('team', 'Team')):
                if r[key]:
                    n.append(f'{field}: {cs(r[key])}')
            for key, field in (('item', 'Item'), ('tm', 'Tm'), ('count', 'Count'), ('species', 'Species'), ('form', 'Form')):
                if r[key]:
                    n.append(f'{field}: {r[key]}')
            lines.append(f'        new({", ".join(a_ + n)}),')
        lines.append('    ];')
        lines.append('')
    lines[-1] = '}'
    with open(out, 'w', encoding='utf-8', newline='\n') as fh:
        fh.write('﻿' + '\n'.join(lines) + '\n')


def trainer_flags_set(src_texts):
    return set(re.findall(r'SetTrainerFlag\s+(TRAINER_\w+)', '\n'.join(src_texts)))


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    root, out = sys.argv[1:3]
    here = os.path.dirname(os.path.abspath(__file__))
    places = json.load(open(os.path.join(here, 'completion_gen4_lieux.json'), encoding='utf-8'))
    lieux = json.load(open(os.path.join(here, 'completion_gen4_ordre.json'), encoding='utf-8'))
    pt = extract_pt(os.path.join(root, 'pokeplatinum'))
    hg = extract_hgss(os.path.join(root, 'pokeheartgold'))
    dp = extract_dp(os.path.join(root, 'pokediamond'), pt['k'])

    import glob
    pt_srcs = [open(f, encoding='utf-8').read() for f in glob.glob(os.path.join(root, 'pokeplatinum', 'res', 'field', '**', '*.s'), recursive=True)]
    hg_srcs = [open(f, encoding='utf-8').read() for f in glob.glob(os.path.join(root, 'pokeheartgold', 'files', 'fielddata', 'script', 'scr_seq', '*.s'))]
    dp_trainer_flags = set()
    for data in dp['scripts'].values():
        dp_trainer_flags |= {v[0] for _, v in dp_find(data, 0x23, None)}
    dp_names = {v: n for n, v in dp['k'].items() if n.startswith('TRAINER_')}

    cp = Conds(pt['k'])
    ch = Conds(hg['k'])
    cd = Conds(pt['k'], dp_flag_resolver(pt['k'], dp['scripts']))
    cur = {
        'pokeplatinum': dict(story=sinnoh_story(cp, True), encounters=sinnoh_encounters(cp, True), quests=sinnoh_quests(cp, True),
                             trainer_flags=trainer_flags_set(pt_srcs), daily_start=pt['k']['DAILY_FLAGS_START'], gift_exclude=set()),
        'pokeheartgold': dict(story=hgss_story(ch), encounters=hgss_encounters(ch), quests=hgss_quests(ch),
                              trainer_flags=trainer_flags_set(hg_srcs), daily_start=2400, gift_exclude=set()),
        'pokediamond': dict(story=sinnoh_story(cd, False), encounters=sinnoh_encounters(cd, False), quests=sinnoh_quests(cd, False),
                            trainer_flags={dp_names[n] for n in dp_trainer_flags if n in dp_names}, daily_start=2400, gift_exclude=set()),
    }
    if MISSING:
        raise SystemExit('Drapeaux de Platine absents des scripts de Diamant / Perle : ' + ', '.join(MISSING))
    games = [('DiamondPearl', 'Diamant et Perle', build(dp, 'pokediamond', places, lieux['pokediamond'], cur['pokediamond'])),
             ('Platinum', 'Platine', build(pt, 'pokeplatinum', places, lieux['pokeplatinum'], cur['pokeplatinum'])),
             ('HeartGoldSoulSilver', 'HeartGold et SoulSilver', build(hg, 'pokeheartgold', places, lieux['pokeheartgold'], cur['pokeheartgold']))]
    tm_moves = [pt['k'][m] for m in pt['tm_moves']]
    emit(games, tm_moves, out)
    for name, _, rows in games:
        counts = {}
        for r in rows:
            counts[r['section']] = counts.get(r['section'], 0) + 1
        print(name, counts, 'ratés possibles', sum(1 for r in rows if r['missed']))
