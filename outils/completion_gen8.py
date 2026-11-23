"""
Génère MonsutaPotaru/Services/Completion/Gen8CompletionData.cs : Épée, Bouclier, Diamant Étincelant, Perle Scintillante
et Légendes Pokémon : Arceus.

    python outils/completion_gen8.py <dossier> MonsutaPotaru/Services/Completion/Gen8CompletionData.cs

<dossier> contient :
- flags_gen8swsh_en.txt, flags_gen8bdsp_en.txt et flags_gen8la_en.txt : listes de FlagsEditorEX
  (https://github.com/fattard/FlagsEditorEXPlugin, flagslist/) : drapeaux nommés (noms internes du jeu ; Épée / Bouclier
  et Légendes Arceus : clés de 64 bits, dont la sauvegarde garde les 32 bits du bas), objets au sol, objets cachés,
  Taupiqueur d'Alola, rencontres (avec leur lieu en anglais) ;
- text_swsh_00000_en.txt, text_swsh_00000_fr.txt, text_bdsp_00000_en.txt, text_bdsp_00000_fr.txt, text_la_00000_en.txt
  et text_la_00000_fr.txt : lieux de PKHeX (PKHeX.Core/Resources/text/locations/gen8, gen8b, gen8a) ;
- text_Items_en.txt et text_Items_fr.txt : noms des objets de PKHeX (Resources/text/items) ;
- text_Species_en.txt et text_Species_fr.txt : noms des espèces de PKHeX (Resources/text/other/en, fr) ;
- UgItemUtil.cs (PKHeX.Core/Saves/Substructures/Gen8/BS) et text_ug_item_fr.txt
  (Resources/text/game/underground_bdsp) : les statues des Grands Souterrains de DÉ/PS ;
- suivi_completion_pokemon_epee_bouclier.csv, suivi_completion_pokemon_diamant_etincelant_perle_scintillante.csv et
  suivi_completion_pokemon_legendes_arceus.csv : check-lists de l'utilisateur (d'après Pokébip) : histoire (et l'ordre
  des lieux), CT / DT et leurs lieux, légendaires, cadeaux, échanges.

Pas de désassemblage pour la Switch : comme en 7e génération, histoire, rencontres et quêtes sont choisies ici d'après
les check-lists, avec les drapeaux nommés de FlagsEditorEX et les objets rares du sac. Pas de dresseurs (choix de
l'utilisateur, 2026-11-13 : FlagsEditorEX ne donne pas leur lieu). Le Pokédex est ajouté par le code
(Gen8Completion) : Pokémon présents dans Épée / Bouclier (Galar, Isolarmure, Couronneige), national de 493 en DÉ/PS,
Hisui en Légendes Arceus (trois étapes par espèce).
"""
import csv
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from completion_gen3 import anchored_rank, split_on, split_paren, strip_paren  # noqa: E402

# Textes répétés (lieux, libellés)
AIRE_DE_COMBAT = 'Aire de Combat'
ARENE_DE_CHARBOURG = 'Arène de Charbourg'
ARENE_DE_FRIMAPIC = 'Arène de Frimapic'
ARENE_DE_SKIFFORD = 'Arène de Skifford'
BAIE_DE_LUDESTER = 'Baie de Ludester'
CATEGORIE = 'Catégorie'
CHOISIR_SON_PREMIER_POKEMON = 'Choisir son premier Pokémon'
CONTREFORT_COURONNE = 'Contrefort Couronné'
COTE_LAZULI = 'Côte Lazuli'
DOJO_DE_LA_MAITRISE = 'Dojo de la Maîtrise'
GRAND_ANTRE_DYNAMAX = 'Grand Antre Dynamax'
HAMEAU_GELE = 'Hameau Gelé'
INTERIEUR = 'intérieur'
LEGENDAIRE_FIXE = 'Légendaire / Fixe'
LIGUE_POKEMON = 'Ligue Pokémon'
MARAIS_CARMIN = 'Marais Carmin'
PARC_ROSA_RUGOSA = 'Parc Rosa Rugosa'
PLAINES_OBSIDIENNES = 'Plaines Obsidiennes'
REPOS_DU_GEANT = 'Repos du Géant'
RUSTI_CITE = 'Rusti-Cité'
SENTIER_DE_LA_GARE = 'Sentier de la Gare'
SIEGE_GROUPE_GALAXIE = 'Siège Groupe Galaxie'
STADE_DE_CORRIFEY = 'Stade de Corrifey'
STADE_DE_GREENBURY = 'Stade de Greenbury'
STADE_DE_KICKENHAM = 'Stade de Kickenham'
STADE_DE_MOTORBY = 'Stade de Motorby'
STADE_DE_SKIFFORD = 'Stade de Skifford'
TEMPLE_COURONNE = 'Temple Couronne'
TEMPLE_DE_SINNOH = 'Temple de Sinnoh'
TERRES_IMMACULEES = 'Terres Immaculées'
TERRES_SAUVAGES = 'Terres Sauvages'
TOUR_DE_COMBAT = 'Tour de Combat'

MAX_ITEM = {'swsh': 1607, 'bdsp': 1822, 'la': 1828}

UNKNOWN = set()
MISSING_PLACES = set()


def lines_of(path):
    return [line.rstrip('\n').lstrip('﻿') for line in open(path, encoding='utf-8')]


def load_flags(path):
    """Sections de la liste de FlagsEditorEX : nom de section → [(clé, type, lieu, sous-lieu, description, nom)]."""
    sections, current = {}, None
    for line in lines_of(path):
        p = line.split('\t')
        if p[0] == '//' and len(p) > 1:
            current = p[1].strip()
            sections[current] = []
            continue
        if not p[0].startswith('0x') or current is None:
            continue
        p += [''] * (7 - len(p))
        sections[current].append((int(p[0], 16), p[1], p[2].strip(), p[3].strip(), p[4].strip(), p[6].strip()))
    return sections


class Ctx:
    def __init__(self, root, key, version):
        self.key, self.version = key, version
        self.sections = load_flags(os.path.join(root, f'flags_gen8{key}_en.txt'))
        # Nom interne → condition (Épée / Bouclier, Arceus : bloc booléen de la sauvegarde ; DÉ/PS : drapeau ou drapeau
        # système de FlagWork8b)
        self.cond = {}
        for section, entries in self.sections.items():
            for flag, kind, loc, sub, desc, name in entries:
                if not name or name in self.cond:
                    continue
                if key == 'bdsp':
                    if section == 'Event Flags':
                        self.cond[name] = f'E:{flag}'
                    elif section == 'Sys Flags':
                        self.cond[name] = f'SF:{flag}'
                elif section == 'Event Flags':
                    self.cond[name] = f'X:{flag & 0xFFFFFFFF:08X}'
        self.items_en = lines_of(os.path.join(root, 'text_Items_en.txt'))
        self.items_fr = lines_of(os.path.join(root, 'text_Items_fr.txt'))
        self.item_ids = {}
        for n, name in enumerate(self.items_en[:MAX_ITEM[key] + 1]):
            self.item_ids.setdefault(name.replace('’', "'"), []).append(n)
        self.item_fr_id = {}
        for n, name in enumerate(self.items_fr[:MAX_ITEM[key] + 1]):
            self.item_fr_id.setdefault(name, n)
        self.species_en = {n: i for i, n in enumerate(lines_of(os.path.join(root, 'text_Species_en.txt')))}
        self.species_fr = {n: i for i, n in enumerate(lines_of(os.path.join(root, 'text_Species_fr.txt')))}
        prefix = {'swsh': 'text_swsh_00000', 'bdsp': 'text_bdsp_00000', 'la': 'text_la_00000'}[key]
        en = lines_of(os.path.join(root, f'{prefix}_en.txt'))
        fr = lines_of(os.path.join(root, f'{prefix}_fr.txt'))
        self.place_fr = {}
        self.places = set()
        for e, f in zip(en, fr):
            e, f = e.replace('’', "'"), f.replace('’', "'")
            if e and e not in self.place_fr:
                self.place_fr[e] = f
            if f:
                self.places.add(f)
            m = re.fullmatch(r'Grand Underground \((.+)\)', e)
            if m:  # grottes des Grands Souterrains : sous-lieux de FlagsEditorEX
                inner = re.fullmatch(r'Grands Souterrains \((.+)\)', f)
                SUBPLACES[m.group(1)] = inner.group(1) if inner else f

    def f(self, *names):
        for n in names:
            if n not in self.cond:
                raise SystemExit(f'Drapeau inconnu ({self.key}) : {n}')
        return '+'.join(self.cond[n] for n in names)

    def a(self, *names):
        return '|'.join(self.f(n) for n in names)

    def item(self, name):
        name = ITEM_FIXES.get(name, name)
        ids = self.item_ids.get(name)
        if not ids:
            UNKNOWN.add(f'objet {name} ({self.key})')  # le générateur s'arrête à la fin, avec la liste complète
            return 0
        return ITEM_CHOICE.get((self.key, name), ids[0])

    def item_fr(self, name):
        if name not in self.item_fr_id:
            UNKNOWN.add(f'objet français {name}')
            return 0
        return self.item_fr_id[name]

    def k(self, *names):
        """Objet rare dans le sac (tous les numéros d'un nom : la Moticyclette en a plusieurs)."""
        out = []
        for n in names:
            ids = self.item_ids.get(n)
            if not ids:
                UNKNOWN.add(f'objet {n} ({self.key})')
                continue
            out += [f'K:{i}' for i in ids]
        return '|'.join(out)

    def species_name(self, number):
        return next(n for n, i in self.species_fr.items() if i == number)

    def species(self, name, french=True):
        table = self.species_fr if french else self.species_en
        if name not in table:
            UNKNOWN.add(f'espèce {name}')
            return 0
        return table[name]

    def place(self, loc):
        """Lieu français d'un lieu anglais de FlagsEditorEX."""
        if loc in PLACE_FR:
            return PLACE_FR[loc]
        base = loc.replace('’', "'")
        base = PLACE_EN.get((self.key, base), PLACE_EN.get(base, base))
        if base not in self.place_fr:
            base2 = strip_paren(base)
            base2 = PLACE_EN.get((self.key, base2), PLACE_EN.get(base2, base2))
            if base2 not in self.place_fr:
                UNKNOWN.add(f'lieu {loc} ({self.key})')
                return base
            base = base2
        return self.place_fr[base]


# Noms anglais de FlagsEditorEX → noms anglais de PKHeX (ou de la check-list)
PLACE_EN = {
    "Brawler's Cave": "Brawlers' Cave", 'Floarama Meadow': 'Floaroma Meadow', 'Solaceon Ruin': 'Solaceon Ruins',
}
# Lieux de FlagsEditorEX absents des listes de PKHeX : nom français direct
PLACE_FR = {'Lost Tower': 'Tour Perdue', 'Pokémon Mansion': 'Manoir Pokémon'}
ITEM_FIXES = {'Pokeball': 'Poké Ball', 'Shny Stone': 'Shiny Stone', 'Snow Ball': 'Snowball', 'Parlyz Heal': 'Paralyze Heal', 'Heart Scales': 'Heart Scale',
              'X Sp. Attack': 'X Sp. Atk', 'X Sp. Defense': 'X Sp. Def'}
# Noms d'objets à plusieurs numéros : celui du jeu
ITEM_CHOICE = {}


def floor(token):
    m = re.fullmatch(r'B(\d)F', token)
    if m:
        return f'sous-sol {m.group(1)}'
    m = re.fullmatch(r'(\d+)F', token)
    if m:
        n = int(m.group(1)) - 1
        if n == 0:
            return 'rez-de-chaussée'
        return '1er étage' if n == 1 else f'{n}e étage'
    return None


# Sous-lieux de FlagsEditorEX : précision française (None : rien à ajouter)
SUBPLACES = {
    '1': None, '2': None, 'Area 1': 'zone 1', 'Area 2': 'zone 2', 'Area 3': 'zone 3', 'Area 4': 'zone 4', 'Area 5': 'zone 5',
    'Area 6': 'zone 6', 'Back 2': 'fond', 'Dried': 'asséché', 'East Room': 'salle est', 'West Room': 'salle ouest',
    'Exterior': 'extérieur', 'Tower Exterior': 'extérieur de la tour', 'Galactic Warehouse': 'entrepôt Galaxie',
    'House': 'maison', 'Inside': INTERIEUR, 'Interior 1': INTERIEUR, 'Interior 2': INTERIEUR, 'Lower tier': 'en bas',
    'Upper Tier': 'en haut', 'Office': 'bureau', 'Outside': 'extérieur', 'Room': 'salle', 'Underpass': 'passage souterrain',
    'Circhester Bay': BAIE_DE_LUDESTER, 'Outer Spikemuth': 'Abords de Smashings', 'Steamdrift Way': 'Sentier des Sources',
}


def subplace(sub):
    sub = sub.strip()
    if not sub:
        return None
    if f := floor(sub):
        return f
    m = re.fullmatch(r'(B?\d+F)[:,]? (.+)', sub)
    if m:
        return ', '.join(p for p in (floor(m.group(1)), subplace(m.group(2))) if p)
    if sub in SUBPLACES:
        return SUBPLACES[sub]
    UNKNOWN.add(f'sous-lieu {sub}')
    return None


def item_parts(c, text):
    """« Ultra Ball (x3) » → (numéro, 3) ; « TM27 (Low Sweep) » → (numéro de la CT27, 1)."""
    text = text.strip()
    m = re.fullmatch(r'(.+?)(?: ?\(x ?(\d+)\))?$', text)
    name, count = m.group(1).strip(), int(m.group(2) or 1)
    tm = re.fullmatch(r'(TM|TR)(\d+)(?: \(.*\))?', name)
    if tm:
        return c.item(f'{tm.group(1)}{int(tm.group(2)):02d}'), count
    return c.item(name), count


def machine(c, item):
    """Numéro de la table pour une CT ou une DT : Épée / Bouclier CT00 → 1 … CT99 → 100, DT00 → 101 … ; DÉ/PS CT01 → 1."""
    m = re.fullmatch(r'(TM|TR)(\d+)', c.items_en[item])
    if not m:
        return 0
    n = int(m.group(2))
    if c.key == 'swsh':
        return n + 1 if m.group(1) == 'TM' else n + 101
    return n


# ---------------------------------------------------------------------------------------------------------------------
# Étapes choisies à la main (check-lists de l'utilisateur) : (lieu, précision, texte, condition)

def swsh_story(c, version):
    sword = version == 'SW'
    return [
        ('Paddoxton', None, CHOISIR_SON_PREMIER_POKEMON, 'D:810|D:813|D:816'),
        ('Route 2', 'labo de Magnolia', 'Recevoir le Poignet Dynamax', c.k('Dynamax Band')),
        ('Brasswick', None, 'Recevoir le Kit de Camping', c.k('Camping Gear')),
        ('Motorby', None, 'Recevoir la Moticyclette', c.k('Rotom Bike')),
        (STADE_DE_GREENBURY, None, "Mission d'Arène : rassembler les Moumouton", c.f('FE_GC_KUSA_CLEAR')),
        (STADE_DE_GREENBURY, None, 'Badge Plante (Percy)', 'B:0'),
        (STADE_DE_SKIFFORD, None, "Mission d'Arène : le labyrinthe des cascades", c.f('FE_GC_MIZU_CLEAR')),
        (STADE_DE_SKIFFORD, None, 'Badge Eau (Donna)', 'B:1'),
        (STADE_DE_MOTORBY, None, "Mission d'Arène : 5 points (captures et K.O.)", c.f('FE_GC_HONO_CLEAR')),
        (STADE_DE_MOTORBY, None, 'Badge Feu (Kabu)', 'B:2'),
        ("Stade d'Old Chister", None, 'Badge Combat (Faïza)' if sword else 'Badge Spectre (Alistair)', 'B:3'),
        (STADE_DE_CORRIFEY, None, "Mission d'Arène : le quiz", c.f('FE_GC_FAIRY_CLEAR')),
        (STADE_DE_CORRIFEY, None, 'Badge Fée (Sally)', 'B:4'),
        ('Stade de Ludester', None, 'Badge Roche (Chaz)' if sword else 'Badge Glace (Lona)', 'B:5'),
        (BAIE_DE_LUDESTER, None, 'Moticyclette aquatique', c.f('FSYS_FLAG_NAMINORI_OPEN')),
        ('Smashings', None, 'Badge Ténèbres (Peterson)', 'B:6'),
        (STADE_DE_KICKENHAM, None, "Mission d'Arène : combats Duo sous la météo", c.f('FE_GC_DORAGON_CLEAR')),
        (STADE_DE_KICKENHAM, None, 'Badge Dragon (Roy)', 'B:7'),
        ('Sommet de la tour', None, 'Capturer Éthernatos', 'D:890'),
        ('Stade de Winscor', None, 'Devenir Maître de la Ligue (Match Ultime contre Tarak)', c.f('FSYS_GAME_CLEAR')),
        # « Kickenham » et pas « Stade de Kickenham » : la page regroupe par lieu (sinon avec le badge de Roy, avant Éthernatos)
        ('Kickenham', 'toit du stade', 'Capturer Zacian' if sword else 'Capturer Zamazenta', 'D:888' if sword else 'D:889'),
        # Isolarmure
        ('Plaine Salutation', None, "Arriver sur l'Isolarmure", c.f('FSYS_ARRIVE_WR0201')),
        (DOJO_DE_LA_MAITRISE, None, 'Recevoir Wushours (Mustar)', 'D:891'),
        ('Tour des Ténèbres', 'ou Tour de l\'Eau', 'Terminer une des Tours des Deux Poings (Shifours)', 'D:892'),
        (DOJO_DE_LA_MAITRISE, None, "Finir l'histoire de l'Isolarmure (combat final contre Mustar)",
         c.f('FSYS_R1_GAME_CLEAR')),
        # Couronneige
        (HAMEAU_GELE, None, 'Arriver sur les Terres Couronneige', c.f('FSYS_ARRIVE_WR0301')),
        (GRAND_ANTRE_DYNAMAX, None, 'Première Expédition Dynamax', c.f('FSYS_CHIKA_FIRST')),
        (REPOS_DU_GEANT, None, 'Quête des Colosses légendaires (Regigigas)', 'D:486'),
        (TEMPLE_COURONNE, None, 'Capturer Sylveroy', 'D:898'),
        (HAMEAU_GELE, None, 'Finir l\'histoire des Terres Couronneige (Dhilan)', c.f('FSYS_R2_GAME_CLEAR')),
    ]


def bdsp_story(c, version):
    diamond = version == 'BD'
    badges = [(ARENE_DE_CHARBOURG, 'Badge Charbon (Pierrick)'), ('Arène de Vestigion', 'Badge Forêt (Flo)'),
              ('Arène de Voilaroc', 'Badge Pavé (Mélina)'), ('Arène de Verchamps', 'Badge Palustre (Lovis)'),
              ("Arène d'Unionpolis", 'Badge Relique (Kiméra)'), ('Arène de Joliberges', 'Badge Mine (Charles)'),
              (ARENE_DE_FRIMAPIC, 'Badge Glaçon (Gladys)'), ('Arène de Rivamar', 'Badge Phare (Tanguy)')]
    b = [(zone, None, label, f'SF:{124 + n}') for n, (zone, label) in enumerate(badges)]
    return [
        ('Lac Vérité', None, CHOISIR_SON_PREMIER_POKEMON, 'D:387|D:390|D:393'),
        ('Littorella', None, 'Recevoir le Pokédex', c.f('FE_ZUKAN_GET')),
        b[0],
        b[1],
        ('Vestigion', None, 'Recevoir la Bicyclette', c.f('FE_CYCLE_GET')),
        b[2],
        b[3],
        b[4],
        b[5],
        b[6],
        ('QG Team Galaxie', None, 'Infiltrer le QG Galaxie (Clé Galaxie)', c.k('Galactic Key')),
        ('Colonnes Lances', None, 'Capturer Dialga' if diamond else 'Capturer Palkia', c.f('FE_D05R0114_SPPOKE_GET')),
        b[7],
        (LIGUE_POKEMON, None, 'Battre Aaron (Conseil 4)', c.f('FE_BIGFOUR1_WIN') + '|' + c.f('SYS_FLAG_GAME_CLEAR')),
        (LIGUE_POKEMON, None, 'Battre Terry (Conseil 4)', c.f('FE_BIGFOUR2_WIN') + '|' + c.f('SYS_FLAG_GAME_CLEAR')),
        (LIGUE_POKEMON, None, 'Battre Adrien (Conseil 4)', c.f('FE_BIGFOUR3_WIN') + '|' + c.f('SYS_FLAG_GAME_CLEAR')),
        (LIGUE_POKEMON, None, 'Battre Lucio (Conseil 4)', c.f('FE_BIGFOUR4_WIN') + '|' + c.f('SYS_FLAG_GAME_CLEAR')),
        (LIGUE_POKEMON, None, 'Devenir Maître de la Ligue (Cynthia)', c.f('SYS_FLAG_GAME_CLEAR')),
        ('Littorella', None, 'Obtenir le Pokédex National', 'N'),
        (AIRE_DE_COMBAT, None, "Arriver à l'Aire de Combat", c.f('OLD_FLAG_ARRIVE_C11')),
    ]


def la_story(c):
    return [
        (RUSTI_CITE, None, CHOISIR_SON_PREMIER_POKEMON, 'D:722|D:155|D:501'),
        ('Arène du Grand Arbre', None, 'Apaiser le Monarque Hachécateur', c.f('FSYS_NS_01_CLEARED')),
        (PLAINES_OBSIDIENNES, None, 'Monture Cerbyllin', c.f('FSYS_RIDE_OPEN_01')),
        (MARAIS_CARMIN, None, 'Monture Ursaking', c.f('FSYS_RIDE_OPEN_03')),
        ('Arène du Théâtre', None, 'Apaiser la Monarque Fragilady', c.f('FSYS_NS_02_CLEARED')),
        (COTE_LAZULI, None, 'Monture Paragruel', c.f('FSYS_RIDE_OPEN_05')),
        ('Arène de la Lave', None, 'Apaiser le Monarque Arcanin', c.f('FSYS_NS_03_CLEARED')),
        (CONTREFORT_COURONNE, None, 'Monture Farfurex', c.f('FSYS_RIDE_OPEN_07')),
        ('Arène de la Lune', None, 'Apaiser le Monarque Électrode', c.f('FSYS_NS_04_CLEARED')),
        ("Arène de l'Iceberg", None, 'Apaiser le Monarque Séracrawl', c.f('FSYS_NS_05_CLEARED')),
        (TEMPLE_DE_SINNOH, None, 'Capturer Dialga ou Palkia', 'D:483|D:484'),
        (TEMPLE_DE_SINNOH, None, 'Apaiser Dialga Originel', c.f('FSYS_NS_D_CLEARED')),
        (TEMPLE_DE_SINNOH, None, 'Apaiser Palkia Originel', c.f('FSYS_NS_P_CLEARED')),
        (TEMPLE_DE_SINNOH, None, 'Capturer Dialga et Palkia', 'D:483+D:484'),
        (SIEGE_GROUPE_GALAXIE, None, 'Compléter le Pokédex de Hisui', c.f('FEVE_EV110100_END')),
        (TEMPLE_DE_SINNOH, None, 'Capturer Arceus', 'D:493'),
    ]


# Pokémon reçus par cadeau mystère : pas dans les rencontres (choix de l'utilisateur, 2026-11-13), toujours dans le
# Pokédex. DÉ/PS : Mew (Poké Ball Plus, comme en Let's Go), Darkrai, Shaymin et Arceus (objets de cadeau mystère).
# Jirachi de DÉ/PS (sauvegarde d'Épée / Bouclier) et Darkrai / Shaymin d'Arceus (sauvegardes de DÉ/PS et d'Épée /
# Bouclier) : débloqués par une sauvegarde d'un autre jeu, gardés.
MYSTERY_GIFT = {'bdsp': {151, 491, 492, 493}, 'swsh': set(), 'la': set()}


# ---------------------------------------------------------------------------------------------------------------------
# Lecture des check-lists

def checklist(path):
    with open(path, encoding='utf-8-sig') as fh:
        return list(csv.DictReader(fh, delimiter=';'))


VERSION_NAMES = {'SW': 'Épée', 'SH': 'Bouclier', 'BD': 'Diamant Étincelant', 'SP': 'Perle Scintillante', 'LA': ''}


def for_version(row, version):
    v = (row.get('Version') or '').strip()
    return not v or v == VERSION_NAMES[version]


def outside_parentheses(text, sep):
    """Position du premier <sep> hors des parenthèses, -1 sinon."""
    depth = 0
    for i, ch in enumerate(text):
        depth += ch == '('
        depth -= ch == ')'
        if depth == 0 and text.startswith(sep, i):
            return i
    return -1


def clean_zone(text):
    """« Kickenham (toit du stade) » → (« Kickenham », « toit du stade ») ; « Marais Carmin – Plaine d'Or »,
    « Lac Courage (Marais Carmin), caverne », « Lac Vérité puis errant » : le premier lieu, le reste en précision."""
    text = text.strip().replace('’', "'")
    rest = []
    for sep in (' – ', ', ', ' puis '):
        cut = outside_parentheses(text, sep)
        if cut >= 0:
            text, more = text[:cut], text[cut + len(sep):]
            rest.insert(0, more if sep != ' puis ' else f'puis {more}')
    place, extra = split_paren(text)
    place = place.strip()
    if ' / ' in place:
        place, other = place.split(' / ', 1)
        rest.append(f'ou {other}')
    extra = ', '.join(p for p in [extra] + rest if p) or None
    return ZONE_FIX.get(place, place), extra


ZONE_FIX = {
    'Labo de Brasswick': 'Brasswick', 'Hôtel de Motorby': 'Motorby', 'Mine de Galar n°2': 'Mine de Galar nº 2',
    'Shehroz Tower': 'Kickenham',
    'Dojo Maîtrise': DOJO_DE_LA_MAITRISE, 'Terrain de Combat du Dojo': DOJO_DE_LA_MAITRISE,
    GRAND_ANTRE_DYNAMAX: GRAND_ANTRE_DYNAMAX, 'Siège du Groupe Galaxie': SIEGE_GROUPE_GALAXIE,
    'Littorella (labo)': 'Littorella', ARENE_DE_CHARBOURG: ARENE_DE_CHARBOURG,
}


def story_order(rows, extra=()):
    """Lieux des étapes « Histoire » de la check-list, dans l'ordre (premier mot de chaque lieu)."""
    out = []
    for row in rows:
        if row[CATEGORIE] != 'Histoire':
            continue
        place, _ = clean_zone(row['Lieu'])
        if place not in out:
            out.append(place)
    for p in extra:
        if p not in out:
            out.append(p)
    return out


# ---------------------------------------------------------------------------------------------------------------------

def build(c, version, order, anchors, cur):
    rows = []

    def add(section, zone, det, done, label=None, item=0, tm=0, species=0, count=0, give=0, form=0, sort=0):
        if section == 'Quests':
            rank = 0  # groupées par activité, dans l'ordre de la liste
        else:
            rank = anchored_rank(zone, order, anchors)
            if rank is None:
                MISSING_PLACES.add(f'{zone} ({version})')
                rank = 10_000
        rows.append({'section': section, 'zone': zone, 'detail': det, 'done': done, 'label': label, 'item': item, 'tm': tm,
                         'species': species, 'count': count, 'give': give, 'form': form, 'sort': (rank, zone, sort)})

    S, I, U, Q = 'Story', 'Items', 'Encounters', 'Quests'
    for i, (zone, det, label, cond) in enumerate(cur['story']):
        add(S, zone, det, cond, label, sort=i)

    # ---------- Objets : au sol, cachés ----------
    seen = set()
    for i, (kind, cond, loc, sub, desc) in enumerate(cur['items']):
        if kind == 'FIELD ITEM':
            text, extra, k = re.sub(r' item ball disappeared$', '', desc).removeprefix('Found '), None, 0
        else:
            text, extra, k = desc.removeprefix('Found '), 'caché', 1
        only = re.search(r' \((Sw|Sh|BD|SP)\)$', text)
        if only:  # objet d'une seule version
            if only.group(1).upper() != version:
                continue
            text = text[:only.start()]
        if text.startswith('('):
            continue  # « (fossil spot) » : fossile au hasard
        value, count = item_parts(c, text)
        tm = machine(c, value)
        if tm:
            seen.add(value)
        det = ', '.join(p for p in (subplace(sub), extra) if p) or None
        add(I, c.place(loc), det, cond, item=0 if tm else value, tm=tm, count=count if count > 1 else 0,
            sort=k * 100_000 + i)

    # CT (et DT d'Épée / Bouclier) sans drapeau (boutiques, cadeaux, raids) : possédées (les CT ne se jettent pas ; une DT
    # apprise disparaît du sac : choix de l'utilisateur, 2026-11-13)
    for number, (name, where) in sorted(cur['machines'].items()):
        item = c.item(name)
        if item in seen:
            continue
        place, extra = shop_place(where, c.key)
        add(I, place, extra, f'K:{item}', tm=machine(c, item), sort=300_000 + number)

    # ---------- Rencontres et quêtes ----------
    for i, (zone, det, species, cond, label) in enumerate(cur['encounters']):
        give, form = 0, 0
        if isinstance(label, tuple):
            give, label = label[1], None
        add(U, zone, det, cond, label, species=species, give=give, form=form, sort=i)
    for i, (zone, det, label, cond) in enumerate(cur['quests']):
        add(Q, zone, det, cond, label, sort=i)

    section_order = {s: n for n, s in enumerate((S, I, U, Q))}
    rows.sort(key=lambda r: (section_order[r['section']],) + ((r['sort'][2],) if r['section'] in (S, U, Q) else r['sort']))
    return rows


def shop_place(text, key):
    """Lieu d'une CT de la check-list : « Kickenham (Centre Pokémon Est) », « Raids ; Marchands (2000 W) »…"""
    text = text.strip()
    if ';' in text or text.startswith('Raids'):
        first = text.split(';')[0].strip()
        if first.startswith(('Raids', 'Marchands')):
            return REGION[key], text.replace(' ; ', ', ').replace('Raids', 'raids').replace('Marchands', 'marchands')
        place, extra = clean_zone(first)
        return place, ', '.join(p for p in (extra, 'ou ' + text.split(';', 1)[1].strip()) if p)
    m = re.fullmatch(r"Arène (?:de |d'|d’)(.+)", text)
    if m and key == 'swsh':
        return f'Stade de {m.group(1)}' if m.group(1)[0] not in 'AEIOUÉ' else f"Stade d'{m.group(1)}", None
    if text.startswith('Boutique de la Tour de Combat'):
        return TOUR_DE_COMBAT, 'boutique'
    return clean_zone(text)


REGION = {'swsh': TERRES_SAUVAGES, 'bdsp': 'Sinnoh', 'la': 'Hisui'}


def checklist_machines(rows):
    """CT (et DT) de la check-list : rang → (nom anglais de l'objet, lieu)."""
    out = {}
    for row in rows:
        if row[CATEGORIE] not in ('CT', 'DT'):
            continue
        m = re.match(r'(CT|DT)(\d+)', row['Objectif'].strip())
        kind, n = m.group(1), int(m.group(2))
        name = f'TM{n:02d}' if kind == 'CT' else f'TR{n:02d}'
        out[(0 if kind == 'CT' else 1000) + n] = (name, row['Lieu'])
    return out


def field_items(c):
    """Objets au sol et cachés de FlagsEditorEX : (type, condition, lieu, sous-lieu, description)."""
    out = []
    for section, entries in c.sections.items():
        for flag, kind, loc, sub, desc, name in entries:
            if kind not in ('FIELD ITEM', 'HIDDEN ITEM') or not loc or not desc:
                continue
            if desc.startswith('[') or '(recurring)' in desc or 'random' in desc or 'Daily' in desc:
                continue
            if c.key == 'swsh':
                cond = f'HI:{flag}' if section == 'Hidden Items Flags' else f'X:{flag & 0xFFFFFFFF:08X}'
            else:
                cond = f'E:{flag}'
            out.append((kind, cond, loc, sub, desc))
    return out


def capture_flags(c):
    """Rencontres de FlagsEditorEX (« KCapturedRegirock », « (BD) Captured Raikou ») : espèce → (condition, version)."""
    out = {}
    for section, entries in c.sections.items():
        for flag, kind, loc, sub, desc, name in entries:
            m = re.fullmatch(r'(?:\((BD|SP)\) )?K?Captured ?(.+)', desc)
            if kind != 'STATIC ENCOUNTER' or not m or name.startswith('z_'):
                continue  # « z_… » : apparition d'un Pokémon sur la carte (Cobaltium…), pas sa capture : par le Pokédex
            raw = m.group(2)
            raw = {'GalarianMoltres': 'Moltres', 'GalarianZapdos': 'Zapdos', 'GalarianArticuno': 'Articuno',
                   'HoOh': 'Ho-Oh', 'TapuKoko': 'Tapu Koko', 'TapuLele': 'Tapu Lele', 'TapuBulu': 'Tapu Bulu',
                   'TapuFini': 'Tapu Fini', 'Dialga/Palkia': None}.get(raw, raw)
            if raw is None:
                continue
            galar = m.group(2).startswith('Galarian')
            species = c.species(raw, french=False)
            out[(species, galar)] = (c.cond.get(name) or (f'E:{flag}' if c.key == 'bdsp' else f'X:{flag & 0xFFFFFFFF:08X}'),
                                     m.group(1))
    return out


# ---------------------------------------------------------------------------------------------------------------------
# Épée / Bouclier

def swsh_encounters(c, rows, version):
    caps = capture_flags(c)
    u = []
    gifts = [
        ('Route 5', 'Garderie, cadeau', 848, c.f('FE_SUB_005_CLEAR'), None),
        (SENTIER_DE_LA_GARE, 'sauvegarde de Let\'s Go, Pikachu', 25, c.f('FE_SUB_037_PIKACHU_CLEAR'), 'Pikachu Gigamax'),
        (SENTIER_DE_LA_GARE, 'sauvegarde de Let\'s Go, Évoli', 133, c.f('FE_SUB_037_EEVEE_CLEAR'), 'Évoli Gigamax'),
        ('Route 6', 'fossiles', 880, 'D:880|D:881|D:882|D:883', 'Galvagon, Galvagla, Hydragon ou Hydragla'),
        # Les drapeaux « z_… » de FlagsEditorEX (« KReceivedGiftCharmander »…) font apparaître ou disparaître un objet de la
        # carte : celui de Salamèche est levé dans epee.sav (3 badges), bien avant le cadeau. Ces cadeaux passent par le
        # Pokédex (aucun de ces Pokémon ne se capture à l'état sauvage).
        ('Paddoxton', 'chambre de Tarak, cadeau', 4, 'D:4', None),
        (TOUR_DE_COMBAT, 'cadeau', 772, 'D:772', None),
        (DOJO_DE_LA_MAITRISE, 'cadeau', 1, c.a('FE_R1_HUSHIGIDANE_GET', 'FE_R1_ZENIGAME_GET'), 'Bulbizarre ou Carapuce Gigamax'),
        (HAMEAU_GELE, 'cadeau', 789, 'D:789', None),
        (GRAND_ANTRE_DYNAMAX, 'cadeau', 803, 'D:803', None),
    ]
    u += gifts
    # Échanges : FSYS_NPC_TRADE_01 à 11 dans l'ordre des lignes de la check-list (E001 à E011 ; ordre supposé, aucune
    # description dans FlagsEditorEX) ; ceux de l'Isolarmure (position variable, sans drapeau) sont laissés de côté
    trades = [r for r in rows if r[CATEGORIE] == 'Échange interne']
    for n, row in enumerate(trades[:11], 1):
        if not for_version(row, version):
            continue
        got, give = (species_of(c, s) for s in split_on(row['Objectif'].strip().removeprefix('Recevoir '), ' contre '))
        zone, extra = clean_zone(row['Lieu'])
        u.append((zone, ', '.join(p for p in (extra, 'échange') if p), got, c.f(f'FSYS_NPC_TRADE_{n:02d}'), ('give', give)))
    # Légendaires de la check-list (sauf ceux de l'histoire)
    story = {888, 889, 890, 891, 892, 898, 486}
    for row in rows:
        if row[CATEGORIE] != LEGENDAIRE_FIXE or not for_version(row, version):
            continue
        name = re.sub(r'^Capturer / obtenir ', '', row['Objectif'].strip())
        galar = 'de Galar' in name
        kanto = '(forme de Kanto)' in name
        name = re.sub(r' \(.*\)$', '', name).replace(' de Galar', '')
        if ' ou ' in name:
            continue  # Blizzeval ou Spectreval : ci-dessous
        species = c.species(name.split(' (')[0])
        if species in story or species in (789, 803, 772):
            continue
        zone, extra = clean_zone(row['Lieu'])
        cond, _ = caps.get((species, galar), (None, None))
        if species in (144, 145, 146) and not galar and not kanto:
            continue
        label = f'{name} de Galar' if galar else None
        u.append((zone, extra, species, cond or f'D:{species}', label))
    u.append((TEMPLE_COURONNE, None, 896, 'D:896|D:897', 'Blizzeval ou Spectreval'))
    return u


def species_of(c, text):
    """« Miaouss (Kanto) », « Noadkoko d'Alola (Fouille) », « Tutafeh de Galar » → numéro national."""
    text = re.sub(r' \([^)\n]*\)', '', text).strip()
    text = re.sub(r" (?:de Galar|d'Alola|de Hisui)$", '', text)
    return c.species(text)


def diglett(c):
    q = []
    n = 0
    for section, entries in c.sections.items():
        for flag, kind, loc, sub, desc, name in entries:
            if kind == 'COLLECTABLE' and desc == 'Found Diglett':
                n += 1
                q.append(("Taupiqueur d'Alola", c.place(loc), f"Taupiqueur d'Alola n° {n}", f'X:{flag & 0xFFFFFFFF:08X}'))
    return q


# ---------------------------------------------------------------------------------------------------------------------
# Diamant Étincelant / Perle Scintillante

def bdsp_encounters(c, rows, version):
    caps = capture_flags(c)
    u = []
    for row in rows:
        cat = row[CATEGORIE]
        if cat not in (LEGENDAIRE_FIXE, 'Pokémon offert', 'Pokémon fixe') or not for_version(row, version):
            continue
        text = row['Objectif'].strip()
        zone, extra = clean_zone(row['Lieu'])
        if cat == LEGENDAIRE_FIXE:
            name = re.sub(r'^Capturer / obtenir ', '', text)
            if name.startswith('Giratina forme Originelle'):
                u.append((zone, extra, 487, c.f('FE_D10R0901POKE_01'), 'Giratina (Salle Distorsion)'))
                continue
            species = c.species(name)
            if species in (483, 484) or species in MYSTERY_GIFT['bdsp']:
                continue
            cond, only = caps.get((species, False), (None, None))
            if only and only != version:
                continue
            u.append((zone, extra, species, cond or f'D:{species}', None))
        elif cat == 'Pokémon fixe':
            m = re.fullmatch(r'Rencontre : (.+?) niv\. \d+', text)
            species = c.species(m.group(1))
            if species == 479:
                cond = c.f('FE_D25R0106_SPPOKE_CLEAR')
            else:
                cond = f'D:{species}'
            u.append((zone, extra, species, cond, None))
        else:
            if text.startswith('Starter'):
                continue
            if text.startswith('Œuf de '):
                species = c.species(text.removeprefix('Œuf de '))
                u.append((zone, extra, species, f'D:{species}', text))
            elif text.startswith('Fossiles :'):
                u.append((zone, extra, 138, 'D:138|D:140|D:142|D:345|D:347', 'Amonita, Kabuto, Ptéra, Lilia ou Anorith'))
            else:
                name = re.sub(r' niv\. \d+$| \(.*\)$', '', text)
                species = c.species(name)
                u.append((zone, extra, species, f'D:{species}', None))
    flags = ['FE_C03R0201_POKE_CHG', 'FE_C04R0301_POKE_CHG']
    trades = [r for r in rows if r[CATEGORIE] == 'Échange interne']
    trade_flags = [name for _, entries in c.sections.items() for (_, kind, _, _, _, name) in entries if kind == 'IN-GAME TRADE']
    for n, row in enumerate(trades):
        got, give = (species_of(c, s) for s in split_on(row['Objectif'].strip().removeprefix('Recevoir '), ' contre '))
        zone, extra = clean_zone(row['Lieu'])
        cond = c.f(trade_flags[n]) if n < len(trade_flags) else f'D:{got}'
        u.append((zone, ', '.join(p for p in (extra, 'échange') if p), got, cond, ('give', give)))
    del flags
    return u


def statues(root):
    """Statues des Grands Souterrains : (numéro de l'objet des souterrains, nom français)."""
    src = open(os.path.join(root, 'UgItemUtil.cs'), encoding='utf-8').read()
    names = lines_of(os.path.join(root, 'text_ug_item_fr.txt'))
    out = []
    for m in re.finditer(r'new\(\s*(\d+),\s*(-?\d+),\s*(-?\d+),\s*(-?\d+),\s*(-?\d+)\)', src):
        ug, item, sphere, _, statue = (int(x) for x in m.groups())  # 4e : socle
        if statue > 0 and item <= 0 and sphere <= 0:
            out.append((ug, names[ug] if ug < len(names) else f'Statue {ug}'))
    return out


def bdsp_quests(c, root):
    q = [
        (TOUR_DE_COMBAT, None, 'Battre le Magnat de la Tour', c.f('FE_TOWER_MASTER_WINED')),
        (TOUR_DE_COMBAT, None, 'Atteindre le rang 10 de la classe Expert', 'BTM:10'),
    ]
    for flag, name in (('STYLE', 'Sang-Froid'), ('BEAUTIFUL', 'Beauté'), ('CUTE', 'Grâce'), ('CLEVER', 'Intelligence'),
                       ('STRONG', 'Robustesse')):
        q.append(('Concours', 'Unionpolis, Salle de Concours', f'Super Show Concours {name} : rang Master',
                  c.f(f'SYS_FLAG_CON_{flag}_MASTER')))
    letters = [chr(ord('A') + i) for i in range(26)] + ['!', '?']
    for n, letter in enumerate(letters):
        q.append(('Zarbi', 'Ruines de Bonville', f'Zarbi {letter}', f'UF:{n}'))
    for ug, name in statues(root):
        q.append(('Statues des Grands Souterrains', None, name, f'UG:{ug}'))
    return q


# ---------------------------------------------------------------------------------------------------------------------
# Légendes Arceus

WISPS = [(RUSTI_CITE, '8B18ADE5', 7), (PLAINES_OBSIDIENNES, '8B18AC32', 20), (MARAIS_CARMIN, '8B18AA7F', 20),
         (COTE_LAZULI, '8B18A8CC', 20), (CONTREFORT_COURONNE, '8B18A719', 20), (TERRES_IMMACULEES, '8B18A566', 20)]
EXPEDITION_RANK = '50FE632A'
LA_REGIONS = [PLAINES_OBSIDIENNES, MARAIS_CARMIN, COTE_LAZULI, CONTREFORT_COURONNE, TERRES_IMMACULEES]


def la_encounters(c, rows):
    u = []
    for row in rows:
        if row[CATEGORIE] != 'Légendaire / Fabuleux':
            continue
        name = re.sub(r' \(niv\..*\)$', '', row['Objectif'].strip())
        species = c.species(name)
        if species in (483, 484, 493):
            continue  # dans l'histoire
        zone, extra = clean_zone(row['Lieu'])
        for region in LA_REGIONS:
            if extra and region in extra:
                rest = extra.replace(f'({region})', '').replace(region, '').replace(' ,', ',').strip(', ')
                extra = ', '.join(p for p in (zone, rest) if p)
                zone = region
                break
        u.append((zone, extra, species, f'D:{species}', None))
    a, b, d = c.cond['FEVE_SUB199_GET_KUSA'], c.cond['FEVE_SUB199_GET_HONOO'], c.cond['FEVE_SUB199_GET_MIZU']
    u += [
        (SIEGE_GROUPE_GALAXIE, 'Lavande, après Arceus', 722, f'{a}+{b}|{a}+{d}|{b}+{d}', 'Les deux autres starters'),
        ('Vallée Enneigée', 'Keoni, cadeau', 37, c.f('FEVE_POKE_SUB092_GET'), "Goupix d'Alola"),
    ]
    return u


def la_quests(rows):
    q = []
    for zone, key, count in WISPS:
        for n in range(1, count + 1):
            q.append(('Flammes Étranges', zone, f'Flamme Étrange n° {n} ({zone})', f'XV:{key}:{n}'))
    unown = [r for r in rows if re.fullmatch(r'Zarbi [A-Z!?]', r['Objectif'].strip())]
    letters = [chr(ord('A') + i) for i in range(26)] + ['!', '?']
    places = {r['Objectif'].strip()[-1]: r['Lieu'] for r in unown}
    for n, letter in enumerate(letters):
        where = places.get(letter)
        where = where.replace(' – ', ', ').replace('votre maison', 'ta maison') if where else None  # l'interface tutoie
        q.append(('Zarbi', where, f'Zarbi {letter}', f'LF:201:{n}'))
    for n in range(1, 11):
        q.append(('Rangs du Groupe Galaxie', SIEGE_GROUPE_GALAXIE, 'Rang Stellaire' if n == 10 else f'Rang {n}★',
                  f'XV:{EXPEDITION_RANK}:{n}'))
    return q


# ---------------------------------------------------------------------------------------------------------------------

def cs(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"' if s is not None else 'null'


def emit(games, out):
    lines = ['// <auto-generated>',
             "// Généré par outils/completion_gen8.py d'après les listes de drapeaux de FlagsEditorEX et les check-lists de",
             "// l'utilisateur : ne pas modifier à la main, changer le script et le relancer.",
             '// </auto-generated>',
             '',
             'namespace MonsutaPotaru.Services;',
             '',
             'public static class Gen8CompletionData',
             '{',
             '']
    for name, doc, dex, rows in games:
        lines.extend([
            f'    /// <summary>Étapes de {doc} (le Pokédex est ajouté à part : {dex}).</summary>',
            f'    public static readonly CompletionRow[] {name} =',
            '    [',
        ])
        for r in rows:
            a_ = [f'CompletionSection.{r["section"]}', cs(r['zone']), cs(r['detail']), cs(r['done'])]
            n = [f'Label: {cs(r["label"])}'] if r['label'] else []
            for key, field in (('item', 'Item'), ('tm', 'Tm'), ('count', 'Count'), ('species', 'Species'), ('form', 'Form'),
                               ('give', 'Give')):
                if r[key]:
                    n.append(f'{field}: {r[key]}')
            lines.append(f'        new({", ".join(a_ + n)}),')
        lines.append('    ];')
        lines.append('')
    lines[-1] = '}'
    with open(out, 'w', encoding='utf-8', newline='\n') as fh:
        fh.write('﻿' + '\n'.join(lines) + '\n')


# Lieux absents des étapes « Histoire » des check-lists : rangés avec un lieu voisin (en chaîne) ; une route se range
# d'office près de son numéro.
ANCHORS = {
    'swsh': {
        'Forêt de Sleepwood': 'Paddoxton', 'Route 1': 'Paddoxton', 'Gare de Brasswick': 'Brasswick',
        STADE_DE_MOTORBY: 'Motorby', 'Gare de Motorby': 'Motorby', 'Abords de Motorby': 'Motorby',
        'Route 3': 'Mine de Galar', 'Route 4': 'Mine de Galar', 'Greenbury': 'Arène de Greenbury',
        STADE_DE_GREENBURY: 'Arène de Greenbury', 'Skifford': ARENE_DE_SKIFFORD, STADE_DE_SKIFFORD: ARENE_DE_SKIFFORD,
        'Gare de Skifford': ARENE_DE_SKIFFORD, 'Gare de Kickenham': 'Kickenham', STADE_DE_KICKENHAM: 'Arène de Kickenham',
        'Salle du réacteur': 'Kickenham', 'Sommet de la tour': 'Kickenham', "Stade d'Old Chister": "Arène d'Old Chister",
        STADE_DE_CORRIFEY: 'Arène de Corrifey', 'Route 8': 'Route 7', 'Sentier des Sources': 'Route 8',
        'Route 9': BAIE_DE_LUDESTER, 'Tunnel de la Route 9': 'Route 9', 'Abords de Smashings': 'Smashings',
        'Stade de Ludester': 'Arène de Ludester', 'Arène de Smashings': 'Smashings', 'Route 10': 'Winscor',
        'Gare de White Hill': 'Route 10', 'Gare de Winscor': 'Winscor', 'Siège de la Ligue': 'Winscor',
        TOUR_DE_COMBAT: 'Winscor', SENTIER_DE_LA_GARE: 'Gare des Terres Sauvages',
        'Plaine Verdoyante': TERRES_SAUVAGES, 'Bois de Clairjour': TERRES_SAUVAGES, 'Tour en ruines': TERRES_SAUVAGES,
        'Lac Coupenotte Est': TERRES_SAUVAGES, 'Lac Coupenotte Ouest': TERRES_SAUVAGES, 'Œil du Lac': TERRES_SAUVAGES,
        'Lac Milobellus Sud': TERRES_SAUVAGES, 'Siège du Géant': TERRES_SAUVAGES,
        'Lac Milobellus Nord': TERRES_SAUVAGES, 'Berges de Motorby': TERRES_SAUVAGES,
        'Prairie Entre-Ponts': TERRES_SAUVAGES, 'Plaine Rocheuse': TERRES_SAUVAGES, 'Fosse des Sables': TERRES_SAUVAGES,
        'Miroir du Géant': TERRES_SAUVAGES, 'Plateau de Kickenham': TERRES_SAUVAGES, 'Coiffe du Géant': TERRES_SAUVAGES,
        'Lac Ouragan': TERRES_SAUVAGES, 'Gare Terres Sauvages': TERRES_SAUVAGES, TERRES_SAUVAGES: 'Gare des Terres Sauvages',
        'Plaine Salutation': 'Isolarmure', 'Forêt Flexion': 'Isolarmure', "Plage de l'Épreuve": 'Isolarmure',
        'Grotte du Pugilat': 'Isolarmure', "Route de l'Épreuve": 'Isolarmure', 'Caverne Pugnacité': 'Isolarmure',
        'Lagune Circulaire': 'Isolarmure', 'Plaine des Efforts': 'Isolarmure', 'Sables de Kudsac': 'Isolarmure',
        'Mer Eau-Hisse': 'Isolarmure', "Mer Align'Îlots": 'Isolarmure', "Mer Isol'Îlots": 'Isolarmure',
        'Mer Calméole': 'Îlot Calméole', DOJO_DE_LA_MAITRISE: 'Dojo Maîtrise', 'Tour des Ténèbres': 'Tours des Deux Poings',
        "Tour de l'Eau": 'Tours des Deux Poings', "Gare d'Isolarmure": 'Isolarmure', 'Lande Boldair': 'Isolarmure',
        "Voie des Étirements": 'Isolarmure',
        'Plateau Beau-Gant': 'Couronneige', 'Gare de Couronneige': 'Couronneige', 'Plaine Granfroy': 'Couronneige',
        'Cimetière Ancien': 'Couronneige', 'Galeries Monte-Pic': 'Couronneige', 'Sentier Blanche-Cime': 'Couronneige',
        'Empreinte du Géant': 'Couronneige', 'Cavernes Gronde-Mer': 'Couronneige', 'Mer Banquise': 'Couronneige',
        'Lac Poké Ball': 'Couronneige', 'Grotte du Lac': 'Couronneige', 'Ruines Pic Roche': REPOS_DU_GEANT,
        'Ruines Iceberg': REPOS_DU_GEANT, 'Ruines de Fer': REPOS_DU_GEANT, 'Ruines du Choix': REPOS_DU_GEANT,
        'Pente Enneigée': REPOS_DU_GEANT, 'Croisée Trois-Voies': REPOS_DU_GEANT, TEMPLE_COURONNE: HAMEAU_GELE,
        'Galar': GRAND_ANTRE_DYNAMAX, 'Centre Pokémon Sud de Motorby': 'Motorby', 'Marché de Skifford': 'Skifford',
        "Toit d'Old Chister": "Arène d'Old Chister", 'Stade de Smashings': 'Smashings', HAMEAU_GELE: 'Couronneige',
        REPOS_DU_GEANT: 'Couronneige', GRAND_ANTRE_DYNAMAX: 'Couronneige', 'Butte du Dynarbre': 'Couronneige',
    },
    'bdsp': {
        'Aire de Détente': AIRE_DE_COMBAT, 'Aire de Survie': AIRE_DE_COMBAT, 'Parc des Combats': AIRE_DE_COMBAT,
        'Chemin Rocheux': 'Floraville', 'Pré de Floraville': 'Floraville', 'Grands Souterrains': 'Vestigion',
        'Grotte des Égarés': 'Route 207', 'Mont Couronné': 'Route 207', 'Grotte du Retour': LIGUE_POKEMON,
        'Temple de Frimapic': LIGUE_POKEMON, 'Grotte du Ruinophile': 'Rive du Lac Courage', 'Lac Savoir': ARENE_DE_FRIMAPIC,
        'Rive du Lac Savoir': ARENE_DE_FRIMAPIC, 'Manoir Pokémon': 'Route 212', 'Mine de Charbourg': ARENE_DE_CHARBOURG,
        'Musée Minier de Charbourg': ARENE_DE_CHARBOURG, 'QG Team Galaxie': 'QG Galaxie', 'Rive du Lac Vérité': 'Lac Vérité',
        'Ruines de Bonville': 'Bonville', 'Square Paisible': 'Unionpolis', 'Île Pleine Lune': PARC_ROSA_RUGOSA,
        'Île Nouvellune': PARC_ROSA_RUGOSA, 'Paradis Fleuri': PARC_ROSA_RUGOSA, 'Charbourg': ARENE_DE_CHARBOURG,
        'Sinnoh': LIGUE_POKEMON,
    },
    'la': {
        'Champ Flora': PLAINES_OBSIDIENNES, 'Île Rosa Rugosa': PLAINES_OBSIDIENNES, 'Creux du Cap': COTE_LAZULI,
        'Main du Sable': COTE_LAZULI, 'Grotte du Retour': COTE_LAZULI, "Grotte de l'Île Crache-Feu": COTE_LAZULI,
        'Marais Carlate': MARAIS_CARMIN, MARAIS_CARMIN: 'Bivouac du Marais', 'Vallée Enneigée': TERRES_IMMACULEES,
        CONTREFORT_COURONNE: 'Grotte des Égarés', 'Lac Savoir': TERRES_IMMACULEES, SIEGE_GROUPE_GALAXIE: RUSTI_CITE,
        'Hisui': TEMPLE_DE_SINNOH,
    },
}


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    root, out = sys.argv[1:3]
    games = []
    for key, csv_file, versions in (
            ('swsh', 'suivi_completion_pokemon_epee_bouclier.csv',
             (('Sword', 'SW', 'Pokémon Épée'), ('Shield', 'SH', 'Pokémon Bouclier'))),
            ('bdsp', 'suivi_completion_pokemon_diamant_etincelant_perle_scintillante.csv',
             (('BrilliantDiamond', 'BD', 'Pokémon Diamant Étincelant'), ('ShiningPearl', 'SP', 'Pokémon Perle Scintillante'))),
            ('la', 'suivi_completion_pokemon_legendes_arceus.csv', (('LegendsArceus', 'LA', 'Légendes Pokémon : Arceus'),))):
        rows = checklist(os.path.join(root, csv_file))
        machines = checklist_machines(rows)
        for name, version, doc in versions:
            c = Ctx(root, key, version)
            if key == 'swsh':
                cur = {'story': swsh_story(c, version), 'items': field_items(c), 'machines': machines,
                           'encounters': swsh_encounters(c, rows, version), 'quests': diglett(c)}
                dex = 'les Pokémon des Pokédex de Galar, de l\'Isolarmure et de Couronneige'
            elif key == 'bdsp':
                cur = {'story': bdsp_story(c, version), 'items': field_items(c), 'machines': machines,
                           'encounters': bdsp_encounters(c, rows, version), 'quests': bdsp_quests(c, root)}
                dex = 'les 493'
            else:
                cur = {'story': la_story(c), 'items': [], 'machines': {},
                           'encounters': la_encounters(c, rows), 'quests': la_quests(rows)}
                dex = 'les 242 de Hisui, trois étapes chacun'
            order = story_order(rows, extra=[REGION[key]])
            games.append((name, doc, dex, build(c, version, order, ANCHORS[key], cur)))
    if UNKNOWN:
        raise SystemExit('À traduire (ITEM_FIXES, PLACE_EN, SUBPLACES) : ' + ', '.join(sorted(UNKNOWN)))
    if MISSING_PLACES:
        raise SystemExit('Lieux sans rang (ANCHORS) : ' + ', '.join(sorted(MISSING_PLACES)))
    emit(games, out)
    for name, _, _, rows in games:
        counts = {}
        for r in rows:
            counts[r['section']] = counts.get(r['section'], 0) + 1
        print(name, counts)
