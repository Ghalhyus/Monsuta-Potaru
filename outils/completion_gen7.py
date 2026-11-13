"""
Génère MonsutaPotaru/Services/Completion/Gen7CompletionData.cs : Soleil, Lune, Ultra-Soleil, Ultra-Lune et Let's Go,
Pikachu / Évoli.

    python outils/completion_gen7.py <dossier> MonsutaPotaru/Services/Completion/Gen7CompletionData.cs

<dossier> contient :
- flags_gen7sm_en.txt, flags_gen7usum_en.txt et flags_gen7blgpe_en.txt : listes de FlagsEditorEX
  (https://github.com/fattard/FlagsEditorEXPlugin, flagslist/) : drapeaux nommés (noms internes du jeu), objets au sol,
  objets cachés, cadeaux, rencontres (avec leur lieu en anglais) ;
- text_sm_00000_en.txt, text_sm_00000_fr.txt, text_gg_00000_en.txt et text_gg_00000_fr.txt : lieux de la 7e génération
  de PKHeX (PKHeX.Core/Resources/text/locations/gen7 ; Ultra-Soleil / Ultra-Lune dans la liste de Soleil / Lune) ;
- text_Items_en.txt et text_Items_fr.txt : noms des objets de PKHeX (Resources/text/items) ;
- text_Species_en.txt et text_Species_fr.txt : noms des espèces de PKHeX (Resources/text/other/en, fr) ;
- SAV_ZygardeCell.cs : l'éditeur des cellules Zygarde de PKHeX (PKHeX.WinForms/Subforms/Save Editors/Gen7) : le lieu de
  chaque cellule, dans l'ordre de la sauvegarde ;
- suivi_completion_pokemon_soleil_lune.csv, suivi_completion_pokemon_ultra_soleil_ultra_lune.csv et
  suivi_completion_pokemon_lets_go.csv : check-lists de l'utilisateur (d'après Pokébip) : CT et leurs lieux,
  légendaires, Ultra-Chimères, Scanner des Îles, Experts de Let's Go.
À côté de ce script : completion_gen7_ordre.json, les lieux dans l'ordre de l'aventure (un lieu absent arrête le script).

Pas de désassemblage pour la 3DS ni pour la Switch : comme en 6e génération, histoire, rencontres et quêtes sont choisies
ici d'après les check-lists, avec les drapeaux nommés de FlagsEditorEX et les objets rares du sac (cristaux Z des
épreuves). Pas encore de dresseurs (il faut les ROM : équipes, noms, lieux).
"""
import csv
import json
import os
import re
import sys

MAX_ITEM = {'sm': 959, 'usum': 959, 'gg': 1057}
# Cristaux Z rangés dans le sac (le jeu donne au Pokémon une copie : le numéro « tenu », 776 et suivants)
ZKEY = list(range(807, 836)) + list(range(927, 933))

# Lieux de FlagsEditorEX et de PKHeX (cellules) absents des listes de lieux de PKHeX : nom anglais de PKHeX
PLACE_EN = {
    'Verdant Cave': 'Verdant Cavern', "Hau'oli City (Shopping)": "Hau'oli City", "Hau'oli City (Marina)": "Hau'oli City",
    'Hano Resort': 'Hano Grand Resort', 'Hano Resort Lobby': 'Hano Grand Resort',
    'Konikoni City Lighthouse': 'Konikoni City', 'Aether Foundation B2F': 'Aether Paradise',
    'Aether Foundation 1F': 'Aether Paradise', 'Aether House': 'Route 15', 'Malie City (Outer Cape)': 'Malie City',
    'Secluded Shore': 'Route 13', 'Lake of Sunne / Moone': 'Lake of the Sunne', 'Heahea Beach': 'Heahea Beach',
    "Team Rocket's Castle": 'Team Rocket’s Castle', 'Ultra Plant': 'Ultra Plant', 'Digglett\'sTunnel': "Diglett's Tunnel",
    'Ten Carat Hill': 'Ten Carat Hill', 'Lush Jungel': 'Lush Jungle', 'Ula\'Ula Meadow': "Ula'ula Meadow",
    'Mount Hokulani': 'Mount Hokulani', 'Vermilion': 'Vermilion City', 'Power Plant': 'Power Plant',
    'Pokémon Mansion': 'Pokémon Mansion',
}

ITEM_FIXES = {'Leach Life': 'Leech Life', 'Flamathrower': 'Flamethrower', 'Thunder Waver': 'Thunder Wave',
              'Pretty Wing': 'Pretty Feather', 'Parlyz Heal': 'Paralyze Heal', 'Pokeball': 'Poké Ball',
              'Health Wing': 'Health Feather', 'Swift Wing': 'Swift Feather', 'Heart Scales': 'Heart Scale',
              'Ultra Balls': 'Ultra Ball', 'X Sp. Attack': 'X Sp. Atk', 'X Sp. Defense': 'X Sp. Def'}
# Cristaux Z « tenus » (le jeu en donne une copie au Pokémon) : jamais dans le sac
HELD_Z = set(range(776, 795)) | set(range(798, 807)) | {836} | set(range(921, 927))

UNKNOWN = set()
MISSING_PLACES = set()


def lines_of(path):
    return [line.rstrip('\n').lstrip('﻿') for line in open(path, encoding='utf-8')]


def load_flags(path):
    """Drapeau → (type, lieu, sous-lieu, description, nom interne)."""
    text = open(path, encoding='utf-8').read()
    flags_part = text.split('//\tEvent Work')[0]
    flags = {}
    for line in flags_part.splitlines():
        p = line.lstrip('﻿').split('\t')
        if not p[0].startswith('0x') or len(p) < 5:
            continue
        flags[int(p[0], 16)] = (p[1], p[2], p[3], p[4], p[6] if len(p) > 6 else '')
    return flags


class Ctx:
    def __init__(self, root, key, version):
        self.key, self.version = key, version
        flags_file = {'sm': 'flags_gen7sm_en.txt', 'usum': 'flags_gen7usum_en.txt', 'gg': 'flags_gen7blgpe_en.txt'}[key]
        self.flags = load_flags(os.path.join(root, flags_file))
        self.num = {}
        for flag, (_, _, _, _, name) in sorted(self.flags.items()):
            if name:
                self.num.setdefault(name, flag)
        self.items_en = lines_of(os.path.join(root, 'text_Items_en.txt'))
        self.items_fr = lines_of(os.path.join(root, 'text_Items_fr.txt'))
        self.item_id, self.item_fr_id = {}, {}
        for n, name in enumerate(self.items_en[:MAX_ITEM[key] + 1]):
            if n not in HELD_Z:
                self.item_id[name] = n
        for n, name in enumerate(self.items_fr[:MAX_ITEM[key] + 1]):
            if n not in HELD_Z:
                self.item_fr_id[name] = n
        self.species_en = {n: i for i, n in enumerate(lines_of(os.path.join(root, 'text_Species_en.txt')))}
        self.species_fr = {n: i for i, n in enumerate(lines_of(os.path.join(root, 'text_Species_fr.txt')))}
        prefix = 'text_gg_00000' if key == 'gg' else 'text_sm_00000'
        en = lines_of(os.path.join(root, f'{prefix}_en.txt'))
        fr = lines_of(os.path.join(root, f'{prefix}_fr.txt'))
        self.place_fr = {}
        for e, f in zip(en, fr):
            e, f = e.replace('’', "'"), f.replace('’', "'")
            if e and e not in self.place_fr:
                self.place_fr[e] = f

    def f(self, *names):
        for n in names:
            if n not in self.num:
                raise SystemExit(f'Drapeau inconnu ({self.key}) : {n}')
        return '+'.join(f'E:{self.num[n]}' for n in names)

    def a(self, *names):
        return '|'.join(self.f(n) for n in names)

    def item(self, name):
        name = ITEM_FIXES.get(name, name)
        if name not in self.item_id:
            UNKNOWN.add(f'objet {name}')  # le générateur s'arrête à la fin, avec la liste complète
            return 0
        return self.item_id[name]

    def item_fr(self, name):
        if name not in self.item_fr_id:
            UNKNOWN.add(f'objet français {name}')
            return 0
        return self.item_fr_id[name]

    def k(self, *names):
        return '|'.join(f'K:{self.item(n)}' for n in names)

    def species_name(self, number):
        return next(n for n, i in self.species_fr.items() if i == number)

    def species(self, name, french=True):
        table = self.species_fr if french else self.species_en
        if name not in table:
            UNKNOWN.add(f'espèce {name}')
            return 0
        return table[name]

    def place(self, loc):
        """Lieu français d'un lieu anglais (FlagsEditorEX, cellules de PKHeX)."""
        base = loc.replace('’', "'")
        base = PLACE_EN.get(base, base).replace('’', "'")
        if base not in self.place_fr:
            base2 = re.sub(r'\s*\(.*\)$', '', base)
            base2 = PLACE_EN.get(base2, base2).replace('’', "'")
            if base2 not in self.place_fr:
                UNKNOWN.add(f'lieu {loc}')
                return base
            base = base2
        return self.place_fr[base]


def floor(token):
    m = re.fullmatch(r'B(\d)F', token)
    if m:
        return f'sous-sol {m.group(1)}'
    m = re.fullmatch(r'(\d+)F', token)
    if m:
        n = int(m.group(1)) - 1
        return 'rez-de-chaussée' if n == 0 else '1er étage' if n == 1 else f'{n}e étage'
    return None


# Sous-lieux de FlagsEditorEX : précision française (None : rien à ajouter)
SUBPLACES = {
    "Hau'oli Outskirts": None, 'Trial Site': "zone de l'Épreuve", "Totem's Den": 'repaire du Dominant', 'Cave': 'grotte',
    'Farthest Hollow': 'Caldeira Toutofon', 'Outside': 'extérieur', 'Inside': 'intérieur', 'Gym': 'Arène',
    'Museum': 'musée', 'House': 'maison', 'Gate': 'portail', 'PokéCenter': 'Centre Pokémon', 'Lab': 'laboratoire',
    'Water': "sur l'eau", 'Harbor': 'port', 'Port': 'port', 'Fan Club': 'Club des Fans', 'Dojo': 'Dojo',
    'Condominiums Rooftop': 'toit de la Résidence', "Mr. Psychic's House": 'maison de M. Psy', 'Bike Maniac House':
    'maison du fan de vélos', 'Safari Zones Warden\'s House': 'maison du Gardien', 'Department Store 3F': 'Centre Commercial',
    'Underground': 'sous-sol', 'Room': 'salle', 'Rooftop': 'toit', 'Basement': 'sous-sol', 'Summit': 'sommet',
    'Grass': None, 'Hotel': 'hôtel', 'Pokémon Center': 'Centre Pokémon', 'Entrance': 'entrée', 'Back': 'fond',
    'Interior': 'intérieur', 'Plateau': 'plateau', 'Beach': 'plage', 'Lighthouse': 'phare', 'Ship': 'bateau',
    'Shopping District': 'zone commerciale', 'Beachfront': 'plage', 'Marina': 'port', 'Lobby': 'hall',
    'Main Building': 'bâtiment principal', 'Conservation Area': 'zone de préservation', 'Mountainside': 'flanc',
    'Hokulani Observatory': 'Observatoire', 'Abandoned Site': 'Site désaffecté', 'Courtyard': 'cour',
    'Shady House': 'Manoir Chelou', 'Ruins': 'ruines', 'Canyon': 'canyon', 'Ultra Space': 'Ultra-Dimension',
    'Front': 'devant', 'Top': 'sommet', 'Islet': 'îlot', 'Fossil Restoration Center': 'Laboratoire des Fossiles',
    'Heahea City': None, 'Altar of the Sunne / Moone': 'autel', 'Tapu Village': None, 'Poké Mart': 'Boutique',
    'Police Station': 'commissariat', 'Hapu\'s Room': 'chambre de Paulie', "Olivia's Room": "chambre d'Alyxia",
    'Home': 'maison du joueur', "Porch in Hala's House": 'maison de Pectorius', 'Inner': 'intérieur',
    'Dimensional Research Lab': 'C.R.I.', 'Aether Base': 'Base Æther', 'Ruins of Hope': None,
    'Docks': 'quais', 'Lab Area': 'laboratoire', 'Crater': 'cratère', 'Kitchen': 'cuisine', 'Outer Cape': 'Cap Passiloin',
    'Plank Bridge': 'pont suspendu', 'Roof': 'toit', 'Steelix Ship': 'bateau Steelix', 'Tide Song Hotel': 'Hôtel des Embruns',
    "Trainer's School": 'École de Dresseurs', 'Underground Path': 'souterrain',
}


def subplace(sub):
    sub = sub.strip()
    if not sub:
        return None
    m = re.fullmatch(r'(B?\d+F): (.+)', sub)  # « B1F: Docks »
    if m:
        return ', '.join(p for p in (floor(m.group(1)), subplace(m.group(2))) if p)
    m = re.fullmatch(r'(.+?) \((B?\d+F|[\w-]+)\)', sub)  # « Tide Song Hotel (3F) », « Underground Path (East-West) »
    if m:
        return ', '.join(p for p in (subplace(m.group(1)), floor(m.group(2))) if p)
    if f := floor(sub):
        return f
    words = sub.split(' ')
    if floor(words[-1]):
        head = ' '.join(words[:-1])
        if head in SUBPLACES:
            return ', '.join(p for p in (SUBPLACES[head], floor(words[-1])) if p)
    if floor(words[0]):
        rest = ' '.join(words[1:])
        if rest in SUBPLACES:
            return ', '.join(p for p in (floor(words[0]), SUBPLACES[rest]) if p)
    if sub in SUBPLACES:
        return SUBPLACES[sub]
    UNKNOWN.add(f'sous-lieu {sub}')
    return None


def item_parts(c, text):
    """« Ultra Ball (x3) » → [(numéro, 3)] ; « TM83 (Infestation) » → [(numéro de la CT83, 1)]."""
    text = text.strip()
    m = re.fullmatch(r'(.+?)(?: \(x(\d+)\))?', text)
    name, count = m.group(1), int(m.group(2) or 1)
    tm = re.fullmatch(r'TM(\d+) \(.*\)', name)
    return c.item(f'TM{int(tm.group(1)):02d}' if tm else name), count


def tm_number(c, item):
    m = re.fullmatch(r'TM(\d+)', c.items_en[item])
    return int(m.group(1)) if m else 0


# ---------------------------------------------------------------------------------------------------------------------
# Étapes choisies à la main (check-lists de l'utilisateur) : (lieu, précision, texte, condition)

def sm_story(c, version):
    sun = version == 'SN'
    return [
        ("Lili'i", None, 'Choisir son premier Pokémon', 'D:722|D:725|D:728'),
        ("Lili'i", None, 'Recevoir le Pokédex', c.f('SYS_FLAG_ZUKAN_GET')),
        ("Lili'i", None, 'Recevoir le Bracelet Z (festival)', c.k('Z-Ring')),
        ('Grotte Verdoyante', None, "Épreuve d'Althéo", c.k('Normalium Z')),
        ("Lili'i", None, 'Grande Épreuve de Mele-Mele (Pectorius)', c.k('Fightinium Z') + '|PS:1'),
        ("Lili'i", None, "Recevoir l'Appel-Monture (Tauros)", c.f('SYS_FLAG_KENTAROSU_RIDE_OPEN')),
        ("Ho'ohale", None, 'Recevoir la Boîte Zygarde (Dexio et Sina)', c.k('Zygarde Cube')),
        ('Ranch Ohana', None, 'Poké Monture Mastouffe', c.f('SYS_FLAG_MUURANDO_RIDE_OPEN')),
        ('Colline Clapotis', None, 'Poké Monture Lokhlass', c.f('SYS_FLAG_RAPURASU_RIDE_OPEN')),
        ('Colline Clapotis', None, 'Épreuve de Néphie', c.k('Waterium Z')),
        ('Colline Clapotis', None, 'Recevoir la Canne', c.k('Fishing Rod')),
        ('Parc Volcanique', None, 'Épreuve de Kiawe', c.k('Firium Z')),
        ('Parc Volcanique', None, 'Poké Monture Dracaufeu', c.f('SYS_FLAG_RIZAADON_RIDE_OPEN')),
        ('Jungle Sombrefeuille', None, 'Épreuve de Barbara', c.k('Grassium Z')),
        ("Ruines de l'Éveil", None, "Grande Épreuve d'Akala (Alyxia)", c.k('Rockium Z') + '|PS:2'),
        ('Observatoire', None, 'Épreuve de Chrys', c.k('Electrium Z')),
        ('Route 12', None, 'Poké Monture Bourrinos', c.f('SYS_FLAG_ROBA2_RIDE_OPEN')),
        ('Bradley Prix', None, 'Épreuve de Margie', c.k('Ghostium Z')),
        ('Route 15', None, 'Poké Monture Sharpedo', c.f('SYS_FLAG_SAMEHADAA_RIDE_OPEN')),
        ('Manoir Chelou', None, 'Battre Guzma au Manoir Chelou', c.k('Buginium Z')),
        ('Malié', None, "Grande Épreuve d'Ula-Ula (Danh)", c.k('Darkinium Z') + '|PS:3'),
        ('Paradis Æther', None, 'Battre Elsa-Mina et recevoir la Flûte du Soleil' if sun
         else 'Battre Elsa-Mina et recevoir la Flûte de la Lune', c.k('Sun Flute' if sun else 'Moon Flute')),
        ('Vieille Route', None, 'Poké Monture Mackogneur', c.f('SYS_FLAG_KAIRIKII_RIDE_OPEN')),
        ('Île Noadkoko', None, 'Recevoir la Flûte de la Lune' if sun else 'Recevoir la Flûte du Soleil',
         c.k('Moon Flute' if sun else 'Sun Flute')),
        ('Grand Canyon de Poni', None, 'Grande Épreuve de Poni (Paulie)', c.k('Groundium Z') + '|PS:4'),
        ('Grand Canyon de Poni', None, 'Épreuve du Grand Canyon (Ékaïser Dominant)', c.k('Dragonium Z')),
        ('Autel du Soleil' if sun else 'Autel de la Lune', None, 'Capturer Solgaleo' if sun else 'Capturer Lunala',
         'D:791' if sun else 'D:792'),
        ('Ligue Pokémon', None, "Devenir Maître de la Ligue Pokémon d'Alola", c.f('SYS_FLAG_GAME_CLEAR')),
    ]


def usum_story(c, version):
    us = version == 'US'
    return [
        ('Route 1', None, 'Choisir son premier Pokémon', 'D:722|D:725|D:728'),
        ("Lili'i", None, 'Recevoir le Pokédex', c.f('SYS_FLAG_ZUKAN_GET')),
        ("Lili'i", None, 'Recevoir le Super Bracelet Z (festival)', c.k('Z-Power Ring')),
        ('Grotte Verdoyante', None, "Épreuve d'Althéo", c.k('Normalium Z')),
        ("Lili'i", None, 'Grande Épreuve de Mele-Mele (Pectorius)', c.k('Fightinium Z') + '|PS:1'),
        ("Lili'i", None, "Recevoir l'Appel-Monture (Tauros)", c.f('SYS_FLAG_KENTAROSU_RIDE_OPEN')),
        ('Plage Big Wave', None, 'Apprendre le Surf Démanta', c.f('FE_SURF_TUTORIAL_CLEAR')),
        ('Ranch Ohana', None, 'Poké Monture Mastouffe', c.f('SYS_FLAG_MUURANDO_RIDE_OPEN')),
        ('Colline Clapotis', None, 'Poké Monture Lokhlass', c.f('SYS_FLAG_RAPURASU_RIDE_OPEN')),
        ('Colline Clapotis', None, 'Épreuve de Néphie', c.k('Waterium Z')),
        ('Colline Clapotis', None, 'Recevoir la Canne', c.k('Fishing Rod')),
        ('Parc Volcanique', None, 'Épreuve de Kiawe', c.k('Firium Z')),
        ('Parc Volcanique', None, 'Poké Monture Dracaufeu', c.f('SYS_FLAG_RIZAADON_RIDE_OPEN')),
        ('Jungle Sombrefeuille', None, 'Épreuve de Barbara', c.k('Grassium Z')),
        ("Ruines de l'Éveil", None, "Grande Épreuve d'Akala (Alyxia)", c.k('Rockium Z') + '|PS:2'),
        ('Mont Hokulani', None, 'Épreuve de Chrys', c.k('Electrium Z')),
        ('Route 12', None, 'Poké Monture Bourrinos', c.f('SYS_FLAG_ROBA2_RIDE_OPEN')),
        ('Bradley Prix', None, 'Épreuve de Margie', c.k('Ghostium Z')),
        ('Route 15', None, 'Poké Monture Sharpedo', c.f('SYS_FLAG_SAMEHADAA_RIDE_OPEN')),
        ('Manoir Chelou', None, 'Battre Guzma au Manoir Chelou', c.k('Buginium Z')),
        ('Malié', None, "Grande Épreuve d'Ula-Ula (Danh)", c.k('Darkinium Z') + '|PS:3'),
        ('Paradis Æther', None, 'Battre Elsa-Mina et recevoir la Flûte de la Lune' if us
         else 'Battre Elsa-Mina et recevoir la Flûte du Soleil', c.k('Moon Flute' if us else 'Sun Flute')),
        ('Vieille Route', None, 'Poké Monture Mackogneur', c.f('SYS_FLAG_KAIRIKII_RIDE_OPEN')),
        ('Grand Canyon de Poni', None, 'Épreuve du Grand Canyon (Ékaïser Dominant)', c.k('Dragonium Z')),
        ('Autel du Soleil' if us else 'Autel de la Lune', None, 'Capturer Solgaleo' if us else 'Capturer Lunala',
         'D:791' if us else 'D:792'),
        ('Alola', None, 'Compléter la Fleur 7 Couleurs', c.k('Rainbow Flower')),
        ('Village Flottant', None, "Épreuve d'Oléa (Rubombelle Dominant)", c.k('Fairium Z')),
        ('Île Noadkoko', None, 'Grande Épreuve de Poni (Paulie)', c.k('Groundium Z') + '|PS:4'),
        ('Mont Lanakila', None, 'Capturer Necrozma', c.f('SYS_FLAG_CAPTURE_NECROZMA')),
        ('Ligue Pokémon', None, "Devenir le premier Maître de la Ligue Pokémon d'Alola", c.f('SYS_FLAG_GAME_CLEAR')),
    ]


def badge(c, flag, name, leader, city):
    return (city, 'Arène', f'Badge {name} ({leader})', c.f(f'FSYS_GYM_CLEAR_{flag}'))


def gg_story(c, version):
    pikachu = version == 'GP'
    return [
        ('Bourg Palette', None, 'Recevoir son partenaire (' + ('Pikachu' if pikachu else 'Évoli') + ')',
         'D:25' if pikachu else 'D:133'),
        badge(c, 'ROCK', 'Roche', 'Pierre', 'Argenta'),
        badge(c, 'WATAER', 'Cascade', 'Ondine', 'Azuria'),
        ("L'Océane", None, 'Technique Secrète Tranchage', c.f('FSYS_HIDEN_OPEN_IAIGIRI')),
        badge(c, 'VOLT', 'Foudre', 'Major Bob', 'Carmin sur Mer'),
        ('Route 2', None, 'Technique Secrète Lumière', c.f('FSYS_HIDEN_OPEN_HURASSYU')),
        ('Repaire Rocket', None, 'Battre Giovanni et recevoir le Scope Sylphe', c.k('Silph Scope')),
        ('Céladopole', None, 'Technique Secrète Envol', c.f('FSYS_HIDEN_OPEN_SORAWOTOBU')),
        badge(c, 'GRASS', 'Prisme', 'Erika', 'Céladopole'),
        ('Tour Pokémon', None, 'Délivrer M. Fuji et recevoir la Poké Flûte', c.k('Poké Flute')),
        ('Parmanie', None, 'Technique Secrète Navigation', c.f('FSYS_HIDEN_OPEN_NAMINORI')),
        ('Parmanie', None, 'Technique Secrète Main Forte', c.f('FSYS_HIDEN_OPEN_KAIRIKI')),
        badge(c, 'POISON', 'Âme', 'Koga', 'Parmanie'),
        badge(c, 'ESPER', 'Marais', 'Morgane', 'Safrania'),
        badge(c, 'FIRE', 'Volcan', 'Auguste', "Cramois'Île"),
        badge(c, 'GROUND', 'Terre', 'Giovanni', 'Jadielle'),
        ('Plateau Indigo', None, 'Devenir Maître de la Ligue Pokémon', c.f('FSYS_GAME_CLEAR')),
    ]


# Cristaux Z hors épreuves (celles de l'histoire) : (lieu, précision, nom anglais, version) ; comptés par possession
SM_Z = [
    ('Konikoni', 'Électacle appris à Pikachu', 'Pikanium Z', None),
    ('Observatoire', 'Molène', 'Steelium Z', None),
    ('Parc de Malié', 'starter, ou Tili après la Ligue', 'Decidium Z', None),
    ('Parc de Malié', 'starter, ou Tili après la Ligue', 'Incinium Z', None),
    ('Parc de Malié', 'starter, ou Tili après la Ligue', 'Primarium Z', None),
    ('Désert Haina', None, 'Psychium Z', None),
    ('Village Flottant', "montrer un Raichu d'Alola", 'Aloraichium Z', None),
    ('Grand Canyon de Poni', 'Apocyne', 'Poisonium Z', None),
    ('Grand Canyon de Poni', 'Oléa', 'Fairium Z', None),
    ('Mont Lanakila', None, 'Icium Z', None),
    ('Ruines du Conflit', 'après Tokorico', 'Tapunium Z', None),
    ('Colline Dicarat', 'Poussée Mackogneur', 'Flyinium Z', None),
    ('Rue du Dôme Royal', 'Bradley Prix, experts Évoli', 'Eevium Z', None),
    ('Alola', 'distribution', 'Snorlium Z', None),
    ('Alola', 'distribution', 'Marshadium Z', None),
    ('Alola', 'distribution', 'Mewnium Z', None),
    ('Alola', 'distribution', 'Pikashunium Z', None),
]
USUM_Z = [
    ('Colline Dicarat', 'Charge Tauros', 'Flyinium Z', None),
    ('Village Flottant', "montrer un Raichu d'Alola", 'Aloraichium Z', None),
    ("Lili'i", 'starter, ou Pectorius après Necrozma', 'Decidium Z', None),
    ("Lili'i", 'starter, ou Pectorius après Necrozma', 'Incinium Z', None),
    ("Lili'i", 'starter, ou Pectorius après Necrozma', 'Primarium Z', None),
    ('Mont Lanakila', None, 'Icium Z', None),
    ('Grotte de la Plaine', None, 'Kommonium Z', None),
    ('Rue du Dôme Royal', 'Bradley Prix', 'Eevium Z', None),
    ('Malié', 'Tili', 'Lycanium Z', None),
    ('Mont Hokulani', 'Molène', 'Steelium Z', None),
    ('Bradley Prix', 'nuit, après Danh', 'Mimikium Z', None),
    ('Plaine des Pikachu', 'Pikachu Casquette', 'Pikashunium Z', None),
    ('Club Hano-Hano', 'Monsieur Chu', 'Pikanium Z', None),
    ('Désert Haina', None, 'Psychium Z', None),
    ("Ho'ohale", 'Hôtel des Embruns, montrer Ronflex', 'Snorlium Z', None),
    ("Ho'ohale", 'Hôtel des Embruns, montrer Mew', 'Mewnium Z', None),
    ("Ho'ohale", 'Hôtel des Embruns, montrer Marshadow', 'Marshadium Z', None),
    ('Ruines du Conflit', 'un Toko vaincu ou capturé', 'Tapunium Z', None),
    ('Vieille Route', 'Apocyne', 'Poisonium Z', None),
    ('Mont Lanakila', 'Necrozma', 'Ultranecrozium Z', None),
    ('Sentier de Mahalo', 'Solgaleo', 'Solganium Z', 'US'),
    ('Autel de la Lune', 'Cosmog du monde inversé', 'Lunalium Z', 'US'),
    ('Sentier de Mahalo', 'Lunala', 'Lunalium Z', 'UM'),
    ('Autel du Soleil', 'Cosmog du monde inversé', 'Solganium Z', 'UM'),
]
GG_MEGA = [
    ('Bourg Palette', 'Blue, labo du Prof. Chen', 'Venusaurite'),
    ('Bourg Palette', 'Blue, labo du Prof. Chen', 'Charizardite X'),
    ('Bourg Palette', 'Blue, labo du Prof. Chen', 'Charizardite Y'),
    ('Bourg Palette', 'Blue, labo du Prof. Chen', 'Blastoisinite'),
    ('Plateau Indigo', 'vente', 'Beedrillite'),
    ('Plateau Indigo', 'vente', 'Pidgeotite'),
    ('Plateau Indigo', 'vente', 'Alakazite'),
    ('Plateau Indigo', 'vente', 'Slowbronite'),
    ('Plateau Indigo', 'vente', 'Gengarite'),
    ('Plateau Indigo', 'vente', 'Kangaskhanite'),
    ('Plateau Indigo', 'vente', 'Pinsirite'),
    ('Plateau Indigo', 'vente', 'Gyaradosite'),
    ('Plateau Indigo', 'vente', 'Aerodactylite'),
    ('Caverne Azurée', 'après Green', 'Mewtwonite X'),
    ('Caverne Azurée', 'après Green', 'Mewtwonite Y'),
]


# Pokémon reçus par cadeau mystère (Magearna par code QR, Marshadow, Zeraora ; le Pikachu Casquette Partenaire d'Ultra,
# par code QR, n'est pas dans usum_encounters) : pas dans les rencontres (choix de l'utilisateur, 2026-11-13), toujours
# dans le Pokédex. Let's Go : pas non plus Mew de la Poké Ball Plus (même choix) ; Meltan et Melmetal (Pokémon GO)
# restent.
MYSTERY_GIFT = {801, 802, 807}


# Rencontres choisies à la main : (lieu, précision, numéro national, condition, texte)
def sm_encounters(c, version):
    sun = version == 'SN'
    u = [
        ('Ranch Ohana', 'Œuf, Garderie', 133, c.f('FE_SUB059_END'), 'Œuf d\'Évoli'),
        ('Route 8', 'fossile', 408 if sun else 410, 'D:408' if sun else 'D:410', None),
        ('Route 8', 'fossile', 564 if sun else 566, 'D:564' if sun else 'D:566', None),
        ('Village Flottant', 'cadeau', 142, c.f('FE_SUB017_END'), None),
        ('Route 15', 'Foyer Æther, cadeau', 137, c.f('FE_SUB078_END'), None),
    ]
    # Échanges : (drapeau, lieu, reçu, donné)
    for flag, zone, got, give in (('FE_FIELD_TRADE_01', 'Route 2', 66, 21), ('FE_FIELD_TRADE_02', 'Route 5', 761, 506),
                                  ('FE_FIELD_TRADE_03', 'Konikoni', 61, 41), ('FE_FIELD_TRADE_04', 'Malié', 440, 674),
                                  ('FE_FIELD_TRADE_05', 'Village Toko', 75, 93),
                                  ('FE_FIELD_TRADE_06', 'Village Flottant', 762, 210),
                                  ('FE_FIELD_TRADE_07', 'Chemin du Défi', 663, 760)):
        u.append((zone, 'échange', got, c.f(flag), ('give', give)))
    return u


def usum_encounters(c, version):
    u = [
        ('Ranch Ohana', 'Œuf, Garderie', 133, c.f('FE_SUB059_END'), 'Œuf d\'Évoli'),
        ('Village Flottant', 'cadeau', 142, c.f('FE_SUB017_END'), None),
        ('Route 15', 'Foyer Æther, cadeau', 137, c.f('FE_SUB078_END'), None),
        ("Ho'ohale", 'Surf Club, records des 4 plages', 25, c.f('FE_SUB313_END'), 'Pikachu Surfeur'),
        ('Ekaeka', 'Centre Commercial', 760, c.f('FE_SUB342_END'), None),
        ('Route 14', 'îlot', 592, c.f('FE_SUB402_END'), None),
        ('Château Rocket', 'faux objet', 101, c.f('FV_F0801D0207_MARUMAIN'), None),
        ('Château Rocket', 'faux objet', 101, c.f('FV_F0801D0217_MARUMAIN'), None),
        ('Château Rocket', 'faux objet', 101, c.f('FV_F0801D0106_MARUMAIN'), None),
    ]
    for flag, zone, got, give in (('FE_FIELD_TRADE_01', 'Route 2', 701, 21), ('FE_FIELD_TRADE_02', 'Route 5', 714, 506),
                                  ('FE_FIELD_TRADE_03', 'Rue du Dôme Royal', 339, 72),
                                  ('FE_FIELD_TRADE_04', 'Route 8', 24, 732),
                                  ('FE_FIELD_TRADE_05', 'Village Toko', 708, 708),
                                  ('FE_FIELD_TRADE_06', 'Village Flottant', 422, 210),
                                  ('FE_FIELD_TRADE_07', 'Chemin du Défi', 128, 760)):
        u.append((zone, 'échange', got, c.f(flag), ('give', give)))
    return u


def gg_encounters(c, version):
    pikachu = version == 'GP'
    u = [
        ('Route 4', 'Centre Pokémon, 500 ₽', 129, c.f('FE_SUB_02_CLEAR'), None),
        ('Azuria', 'cadeau', 1, c.f('FE_SUB_37_CLEAR'), None),
        ('Route 24', 'cadeau', 4, c.f('FE_SUB_38_CLEAR'), None),
        ('Carmin sur Mer', 'cadeau', 7, c.f('FE_SUB_39_CLEAR'), None),
        ('Safrania', 'Dojo', 106, c.f('FE_SUB_23_CLEAR'), 'Kicklee ou Tygnon'),
        ('Sylphe SARL', 'cadeau', 131, c.f('FE_SUB_08_CLEAR'), None),
        ('Safrania', 'cadeau', 137, c.f('FE_SUB_60_CLEAR'), None),
        ("Cramois'Île", 'fossile', 138, 'D:138|D:140', 'Amonita ou Kabuto'),
        ("Cramois'Île", 'Vieil Ambre', 142, 'D:142', None),
        ('Route 12', None, 143, c.f('FE_CAPTURE_KABIGON_ROAD012'), None),
        ('Route 16', None, 143, c.f('FE_CAPTURE_KABIGON_ROAD016'), None),
    ]
    for n in range(1, 5):
        u.append(('Centrale abandonnée', 'faux objet', 101, c.f(f'r010r0101_marumain_0{n}'), None))
    u += [
        ('Îles Écume', None, 144, c.f('FE_SUB_49_CLEAR'), None),
        ('Centrale abandonnée', None, 145, c.f('FE_SUB_48_CLEAR'), None),
        ('Route Victoire', None, 146, c.f('FE_SUB_47_CLEAR'), None),
        ('Caverne Azurée', None, 150, c.f('FE_SUB_50_CLEAR'), None),
        ('Kanto', 'Pokémon GO (GO Park)', 808, 'D:808', None),
        ('Kanto', 'Pokémon GO (GO Park)', 809, 'D:809', None),
    ]
    # Échanges contre une forme d'Alola (8 par version) : drapeaux FE_FIELD_TRADE_FIRST_01 à 08, ordre supposé (celui du
    # voyage), aucune description dans FlagsEditorEX
    trades = [('Azuria', 19, 19), ('Carmin sur Mer', 74, 74), ('Lavanville', 50, 50),
              ('Céladopole', 27, 27) if pikachu else ('Céladopole', 37, 37), ('Safrania', 26, 26), ('Parmanie', 105, 105),
              ("Cramois'Île", 88, 88) if pikachu else ("Cramois'Île", 52, 52), ('Plateau Indigo', 103, 103)]
    for n, (zone, got, give) in enumerate(trades, 1):
        name = c.species_name(got)
        u.append((zone, 'échange', got, c.f(f'FE_FIELD_TRADE_FIRST_{n:02d}'), ('alola', f"{name} d'Alola (contre {name})")))
    return u


# ---------------------------------------------------------------------------------------------------------------------
# Lecture des check-lists

def checklist(path):
    with open(path, encoding='utf-8-sig') as fh:
        return list(csv.DictReader(fh, delimiter=';'))


VERSIONS = {'SN': 'Soleil', 'MN': 'Lune', 'US': 'Ultra-Soleil', 'UM': 'Ultra-Lune', 'GP': "Let's Go Pikachu",
            'GE': "Let's Go Évoli"}


def for_version(row, version):
    v = row['Version']
    return v.startswith('Les deux') or v.startswith('Soleil/Lune') or v == VERSIONS[version]


# Lieux des check-lists → (lieu de la table, précision)
ZONE_FIX = {
    'Base Æther': ('Route 16', 'Base Æther'), 'Foyer Æther': ('Route 15', 'Foyer Æther'),
    'Base Æther (Route 16)': ('Route 16', 'Base Æther'), 'Foyer Æther (Route 15)': ('Route 15', 'Foyer Æther'),
    'Lac du Halo Solaire / Lunaire': ('Lac du Halo Solaire', None), 'Laboratoire d\'Euphorbe': ('Abords d\'Ekaeka', 'labo'),
    'Lac du Halo Lunaire/Solaire': ('Lac du Halo Lunaire', None),
    'Vieille Route / Paradis Æther': ('Vieille Route', None), 'Centre Commercial d\'Ekaeka': ('Ekaeka', 'Centre Commercial'),
    'Sentier de Mahalo': ('Sentier de Mahalo', None), 'Grand Canyon': ('Grand Canyon de Poni', None),
    'Jardin d\'Ula-Ula': ('Jardin d\'Ula-Ula', None),
    'École des Dresseurs': ('Route 1', 'École de Dresseurs'), 'École de Dresseurs': ('Route 1', 'École de Dresseurs'),
    'Lac du Halo Lunaire / Lac du Halo Solaire': ('Lac du Halo Lunaire', None),
    'Embarcadère de Malié': ('Malié', 'embarcadère'), 'Kokohio – Manoir Chelou': ('Manoir Chelou', None),
    'Grotte Taupiqueur': ('Grotte Taupiqueur', None), 'Souterrain': ('Route 5', 'Souterrain'),
    'Dojo de Safrania': ('Safrania', 'Dojo'), 'Arène': ('Arène', None), 'Ligue Pokémon': ('Plateau Indigo', None),
    'Sylphe SARL étage 10': ('Sylphe SARL', '10e étage'), 'Sylphe SARL étage 3': ('Sylphe SARL', '3e étage'),
    'Sylphe SARL RdC': ('Sylphe SARL', 'rez-de-chaussée'), 'Caldeira Toutofon (Colline Dicarat)': ('Caldeira Toutofon', None),
}


def clean_zone(text, key):
    """« Paradis Æther (Réserve, 1er étage) » → (« Paradis Æther », « Réserve, 1er étage ») ; « — » → la région."""
    text = text.strip()
    if text in ('—', ''):
        return ('Kanto' if key == 'gg' else 'Alola'), 'distribution'
    fixes = ZONE_FIX if key == 'gg' else {k: v for k, v in ZONE_FIX.items() if k != 'Ligue Pokémon'}
    if text in fixes:
        return fixes[text]
    m = re.fullmatch(r'(.+?)(?: \((.+)\))?', text)
    place, extra = m.group(1), m.group(2)
    if ', ' in place:  # « Malié, Bibliothèque »
        place, more = place.split(', ', 1)
        extra = ', '.join(p for p in (more, extra) if p)
    if ' / ' in place:
        place, other = place.split(' / ', 1)
        extra = ', '.join(p for p in (extra, f'ou {other}') if p)
    if place in ZONE_FIX:
        place, more = ZONE_FIX[place]
        extra = ', '.join(p for p in (more, extra) if p)
    return place, extra


def shop_place(text, key):
    """Lieu d'une CT de la check-list : « Boutique du Village Flottant » → (« Village Flottant », « Boutique »)."""
    text = re.sub(r'^(?:à l\'|à |au |aux |par .+? (?:à l\'|à |au |aux ))', '', text.strip())
    m = re.fullmatch(r"(?:Boutique|Magasin) (?:de CT |CT )?(?:de la |de |du |d'|d’)(.+)", text)
    if m:
        place, extra = m.group(1), 'Boutique de CT' if 'CT' in text.split(m.group(1))[0] else 'Boutique'
        return place, extra
    m = re.fullmatch(r"Arène (?:de |d')(.+)", text)
    if m:
        return m.group(1), 'Arène'
    return clean_zone(text, key)


# ---------------------------------------------------------------------------------------------------------------------

def zygarde_cells(root):
    """Lieux anglais des cellules Zygarde de PKHeX : Soleil / Lune (95), Ultra-Soleil / Ultra-Lune (100)."""
    src = open(os.path.join(root, 'SAV_ZygardeCell.cs'), encoding='utf-8').read()
    out = {}
    for name, key in (('locationsSM', 'sm'), ('locationsUSUM', 'usum')):
        body = re.search(name + r' =\s*\[(.*?)\];', src, re.S).group(1)
        out[key] = re.findall(r'"((?:[^"\\]|\\.)*)"', body)
    return out


TIME = {'Day': 'jour', 'Night': 'nuit'}


def cell_place(c, loc):
    """« Route 1 - Trainer School (Night) » → (« Route 1 », « nuit »)."""
    time = None
    m = re.search(r'\((Day|Night)\)$', loc)
    if m:
        time = TIME[m.group(1)]
        loc = loc[:m.start()].strip()
    base = loc.split(' - ')[0].strip()
    return c.place(base), time


def build(c, version, order, cur):
    rows = []

    def add(section, zone, det, done, label=None, item=0, tm=0, species=0, count=0, give=0, form=0, sort=0):
        if section == 'Quests':
            rank = 0  # groupées par activité, dans l'ordre de la liste
        elif zone not in order:
            MISSING_PLACES.add(f'{zone} ({version})')
            rank = 10_000
        else:
            rank = order.index(zone)
        rows.append(dict(section=section, zone=zone, detail=det, done=done, label=label, item=item, tm=tm,
                         species=species, count=count, give=give, form=form, sort=(rank, zone, sort)))

    S, I, U, Q = 'Story', 'Items', 'Encounters', 'Quests'
    story_items = set()
    for i, (zone, det, label, cond) in enumerate(cur['story']):
        add(S, zone, det, cond, label, sort=i)
        story_items.update(int(a[2:]) for a in re.split(r'[|+]', cond) if a.startswith('K:'))

    # ---------- Objets : au sol, cachés, cadeaux décrits (Let's Go) ----------
    seen = set()
    for flag, (kind, loc, sub, desc, name) in sorted(c.flags.items()):
        if kind not in ('FIELD ITEM', 'HIDDEN ITEM', 'ITEM GIFT') or not desc or not loc:
            continue
        if desc.startswith('[') or '(recurring)' in desc or 'Daily' in desc or 'random' in desc:
            continue  # inutilisés, objets cachés qui reviennent, cadeaux quotidiens
        if kind == 'FIELD ITEM':
            text, extra, k = desc.replace(' item ball disappeared', '').removeprefix('Found '), None, 0
        elif kind == 'HIDDEN ITEM':
            text, extra, k = desc.removeprefix('Found ').removeprefix('Found '), 'caché', 1  # « Found Found » (Soleil / Lune)
        else:
            m = re.fullmatch(r'(.+?) from .*', desc)
            if not m or ' Set' in m.group(1) or ' / ' in m.group(1):
                continue  # tenues (Let's Go), cadeaux sans objet
            text, extra, k = m.group(1), 'cadeau', 2
        value, count = item_parts(c, text)
        tm = tm_number(c, value)
        seen.add(value)
        det = ', '.join(p for p in (subplace(sub), extra) if p) or None
        add(I, c.place(loc), det, f'E:{flag}', item=0 if tm else value, tm=tm, count=count if count > 1 else 0,
            sort=k * 100_000 + flag)

    # CT sans drapeau (cadeaux de personnages, boutiques) : possédées (elles ne se jettent pas)
    for number, where in sorted(cur['machines'].items()):
        item = c.item(f'TM{number:02d}')
        if item in seen:
            continue
        place, extra = shop_place(where, c.key)
        add(I, place, extra, f'K:{item}', tm=number, sort=300_000 + number)

    # Cristaux Z hors épreuves de l'histoire, Méga-Gemmes de Let's Go : possédés
    for i, (place, extra, name, only) in enumerate(cur['z']):
        if only not in (None, version):
            continue
        item = c.item(name)
        if item in story_items or item in seen:
            continue
        seen.add(item)
        add(I, place, ', '.join(p for p in (extra, 'cristal Z') if p), f'K:{item}', item=item, sort=400_000 + i)
    for i, (place, extra, name) in enumerate(cur.get('mega', [])):
        item = c.item(name)
        add(I, place, ', '.join(p for p in (extra, 'Méga-Gemme') if p), f'K:{item}', item=item, sort=400_000 + i)

    # ---------- Rencontres et quêtes ----------
    for i, (zone, det, species, cond, label) in enumerate(cur['encounters']):
        give, form = 0, 0
        if isinstance(label, tuple):
            kind, value = label
            if kind == 'give':
                give, label = value, None
            else:  # forme d'Alola (Let's Go)
                form, label = 1, value
        add(U, zone, det, cond, label, species=species, give=give, form=form, sort=i)
    for i, (zone, det, label, cond) in enumerate(cur['quests']):
        add(Q, zone, det, cond, label, sort=i)

    section_order = {s: n for n, s in enumerate((S, I, U, Q))}
    rows.sort(key=lambda r: (section_order[r['section']],) + ((r['sort'][2],) if r['section'] in (S, U, Q) else r['sort']))
    return rows


def checklist_encounters(c, rows, version, skip, flags):
    """Légendaires et Ultra-Chimères de la check-list : capturés (drapeau de capture de FlagsEditorEX s'il y en a un,
    sinon le Pokédex). <skip> : espèces déjà dans l'histoire ; <flags> : numéro national → condition."""
    u = []
    for row in rows:
        if row['Catégorie'] not in ('Légendaire', 'Ultra-Chimère') or not for_version(row, version):
            continue
        name = re.sub(r'^(?:Obtenir|Capturer) ', '', row['Objectif'])
        name = re.sub(r'\s*\(.*\)$', '', name)
        species = c.species(name)
        if species in skip or any(e[2] == species for e in u):
            continue
        zone, extra = clean_zone(row['Lieu'], c.key)
        if species == 789:  # Cosmog : le lac de la version (Soleil / Lune), celui du monde inversé (Ultra)
            zone = 'Lac du Halo Solaire' if version in ('SN', 'UM') else 'Lac du Halo Lunaire'
            extra = 'monde inversé' if c.key == 'usum' else None
        u.append((zone, extra, species, flags.get(species, f'D:{species}'), None))
    return u


def capture_flags(c):
    """Drapeaux « … Captured » de FlagsEditorEX (Ultra-Soleil / Ultra-Lune) : numéro national → condition."""
    out = {}
    for flag, (kind, _, _, desc, name) in c.flags.items():
        m = re.fullmatch(r'(?:\((?:US|UM)\) )?(.+?) Captured', desc)
        if kind == 'STATIC ENCOUNTER' and m and name.startswith('SYS_FLAG_CAPTURE_'):
            out[c.species(m.group(1), french=False)] = f'E:{flag}'
    return out


def island_scan(c, rows, version):
    """Scanner des Îles : chaque Pokémon capturé (le Pokédex : rien d'autre ne le garde)."""
    q = []
    for row in rows:
        if row['Catégorie'] != 'Scanner des Îles':
            continue
        m = re.fullmatch(r'Capturer (.+?) via le Scanner des Îles', row['Objectif'])
        species = c.species(m.group(1))
        zone = re.sub(r'\s*\(.*\)$', '', row['Lieu'])
        zone, extra = clean_zone(zone, c.key)
        day = row['Détails / Récompense'].split(',')[0].lower()
        q.append(('Scanner des Îles', ', '.join(p for p in (zone, extra, day) if p), f'Capturer {m.group(1)}',
                  f'D:{species}'))
    return q


TREE = [('Solo', 0), ('Duo', 1), ('Multi', 2)]


def battle_tree():
    q = []
    for mode, n in TREE:
        q.append(('Arbre de Combat', None, f'Combat {mode} : 20 victoires (Red ou Blue au 20e combat)', f'BT:{n}:0:20'))
    for mode, n in TREE:
        q.append(('Arbre de Combat', None, f'Combat Super {mode} : 50 victoires', f'BT:{n}:1:50'))
    return q


SM_CORES = [('Vieille Route', 'chambre de Paulie'), ("Lili'i", 'maison de Pectorius'), ('Route 17', 'commissariat'),
            ('Abords d\'Ekaeka', 'maison du joueur'), ('Konikoni', "chambre d'Alyxia")]


def zygarde(c, cells):
    q = [('Zygarde', 'Route 16, Base Æther', 'Recevoir la Boîte Zygarde', c.k('Zygarde Cube'))] if c.key == 'usum' else []
    for n in range(1, 6):
        where = ', '.join(SM_CORES[n - 1]) if c.key == 'sm' else None
        q.append(('Zygarde', where, f'Cœur de Zygarde n° {n}', c.f(f'SYS_FLAG_ZIGARUDE_CORE{n}_GET')))
    for i, loc in enumerate(cells):
        zone, time = cell_place(c, loc)
        q.append(('Zygarde', ', '.join(p for p in (zone, time) if p), f'Cellule Zygarde n° {i + 1}', f'ZC:{i}'))
    return q


def masters(c, rows):
    """Experts de Let's Go : titre débloqué (titre n : Pokémon n du Pokédex de Let's Go, Meltan et Melmetal en 152 et 153)."""
    q = []
    for n, row in enumerate((r for r in rows if r['Catégorie'] == 'Expert'), 1):
        zone, extra = clean_zone(row['Lieu'], 'gg')
        q.append(('Experts Pokémon', ', '.join(p for p in (zone, extra) if p), row['Objectif'].strip(), f'TF:{n}'))
    if len(q) != 153:
        raise SystemExit(f'Experts : {len(q)} au lieu de 153')
    q += [
        ('Experts Pokémon', 'Céladopole, Bureau Game Freak', 'Obtenir le titre de Grand Maître', 'TF:154'),
        ('Après la Ligue', 'Plateau Indigo, entrée', 'Battre Red (titre de Maître de Combat)',
         c.f('FE_EV0905_BTL_RED') + '|TF:155'),
        ('Après la Ligue', 'Repaire Rocket', 'Battre Amos', c.f('FE_SUB_44_CLEAR')),
        ('Après la Ligue', 'Caverne Azurée', 'Battre Green', c.f('FE_SUB_45_CLEAR')),
        ('Après la Ligue', 'Route 17', 'Combat final contre Jessie et James', c.f('FE_SUB_57_CLEAR')),
    ]
    return q


def checklist_machines(rows):
    """CT de la check-list : numéro → lieu."""
    out = {}
    for row in rows:
        if row['Catégorie'] != 'CT':
            continue
        m = re.fullmatch(r'CT0*(\d+)', row['ID'])
        out[int(m.group(1))] = row['Lieu']
    return out


def cs(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"' if s is not None else 'null'


def emit(games, out):
    lines = ['// <auto-generated>',
             "// Généré par outils/completion_gen7.py d'après les listes de drapeaux de FlagsEditorEX et les check-lists de",
             "// l'utilisateur : ne pas modifier à la main, changer le script et le relancer.",
             '// </auto-generated>',
             '',
             'namespace MonsutaPotaru.Services;',
             '',
             'public static class Gen7CompletionData',
             '{',
             '']
    for name, doc, dex, rows in games:
        lines.append(f'    /// <summary>Étapes de {doc} (le Pokédex est ajouté à part : {dex}).</summary>')
        lines.append(f'    public static readonly CompletionRow[] {name} =')
        lines.append('    [')
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


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    root, out = sys.argv[1:3]
    here = os.path.dirname(os.path.abspath(__file__))
    order = json.load(open(os.path.join(here, 'completion_gen7_ordre.json'), encoding='utf-8'))
    cells = zygarde_cells(root)
    games = []
    for key, csv_file, versions in (
            ('sm', 'suivi_completion_pokemon_soleil_lune.csv',
             (('Sun', 'SN', 'Pokémon Soleil'), ('Moon', 'MN', 'Pokémon Lune'))),
            ('usum', 'suivi_completion_pokemon_ultra_soleil_ultra_lune.csv',
             (('UltraSun', 'US', 'Pokémon Ultra-Soleil'), ('UltraMoon', 'UM', 'Pokémon Ultra-Lune'))),
            ('gg', 'suivi_completion_pokemon_lets_go.csv',
             (('LetsGoPikachu', 'GP', "Pokémon Let's Go, Pikachu"), ('LetsGoEevee', 'GE', "Pokémon Let's Go, Évoli")))):
        rows = checklist(os.path.join(root, csv_file))
        machines = checklist_machines(rows)
        for name, version, doc in versions:
            c = Ctx(root, key, version)
            if key == 'sm':
                gifts = {789: c.f('FE_SUB140_END'), 772: c.f('FE_SUB202_END'), 801: c.f('SYS_FLAG_MAGIANA_CLEAR')}
                enc = checklist_encounters(c, rows, version, {791, 792} | MYSTERY_GIFT, gifts) + sm_encounters(c, version)
                cur = dict(story=sm_story(c, version), encounters=enc, z=SM_Z, machines=machines,
                           quests=battle_tree() + zygarde(c, cells['sm']) + island_scan(c, rows, version))
                dex = 'les 802'
            elif key == 'usum':
                gifts = capture_flags(c)
                gifts.update({789: c.f('FE_SUB140_END'), 772: c.f('FE_VICKE_NULL_GET'),
                              801: c.f('SYS_FLAG_MAGIANA_CLEAR'), 803: c.f('FE_CAPTURE_UBVENOM')})
                enc = checklist_encounters(c, rows, version, {791, 792, 800} | MYSTERY_GIFT, gifts) + usum_encounters(c, version)
                cur = dict(story=usum_story(c, version), encounters=enc, z=USUM_Z, machines=machines,
                           quests=battle_tree() + zygarde(c, cells['usum']) + island_scan(c, rows, version))
                dex = 'les 807'
            else:
                cur = dict(story=gg_story(c, version), encounters=gg_encounters(c, version), z=[], mega=GG_MEGA,
                           machines=machines, quests=masters(c, rows))
                dex = 'les 151 de Kanto, Meltan et Melmetal'
            games.append((name, doc, dex, build(c, version, order[key], cur)))
    if UNKNOWN:
        raise SystemExit('À traduire (ITEM_FIXES, PLACE_EN, SUBPLACES) : ' + ', '.join(sorted(UNKNOWN)))
    if MISSING_PLACES:
        raise SystemExit('Lieux absents de completion_gen7_ordre.json : ' + ', '.join(sorted(MISSING_PLACES)))
    emit(games, out)
    for name, _, _, rows in games:
        counts = {}
        for r in rows:
            counts[r['section']] = counts.get(r['section'], 0) + 1
        print(name, counts)
