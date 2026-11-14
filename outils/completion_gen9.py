"""
Génère MonsutaPotaru/Services/Completion/Gen9CompletionData.cs : Écarlate et Violet (DLC compris : Le Masque Turquoise,
Le Disque Indigo).

    python outils/completion_gen9.py <dossier> MonsutaPotaru/Services/Completion/Gen9CompletionData.cs

<dossier> contient :
- flags_gen9sv_en.txt : liste de FlagsEditorEX (https://github.com/fattard/FlagsEditorEXPlugin, flagslist/) : objets au
  sol (« Field Item Flags » : clés de 64 bits, liste du bloc 0x2482AD60), dresseurs (« Trainer Flags » : clés de 64 bits,
  listes des blocs 0xF018C4AC et 0x28E475DE), drapeaux (« Event Flags » : un bloc booléen par drapeau, clé de 32 bits) et
  variables (« Event Work » : un bloc numérique chacune ; les badges y sont : rang d'obtention, 0 tant qu'il manque) ;
- text_sv_00000_en.txt et text_sv_00000_fr.txt : lieux de PKHeX (PKHeX.Core/Resources/text/locations/gen9) ;
- text_Items_en.txt et text_Items_fr.txt : noms des objets de PKHeX (Resources/text/items) ;
- text_Species_en.txt et text_Species_fr.txt : noms des espèces de PKHeX (Resources/text/other/en, fr) ;
- suivi_completion_pokemon_ecarlate_violet.csv : check-list de l'utilisateur (d'après Pokébip et Eternia ; sur son Drive :
  completion_ecarlate_violet.csv, renommée) : histoire, CT et leurs lieux, légendaires, échanges, Pokédex (les espèces
  de la catégorie « Pokédex », présentes dans le jeu).

Pas de désassemblage pour la Switch : comme en 8e génération, histoire, rencontres et quêtes sont choisies ici d'après
la check-list, avec les drapeaux nommés de FlagsEditorEX. Choix de l'utilisateur (2026-11-14) : DLC toujours comptés, à
leur lieu ; Pokédex = les 692 espèces présentes dans le jeu ; dresseurs, objets au sol, CT par recette débloquée, quêtes
annexes qui laissent une trace.

Dresseurs : FlagsEditorEX ne donne ni leur lieu ni leur équipe ; le lieu vient de leur nom interne
(« area09_trainer_… » : la zone 9, dont le lieu est celui de la plupart des objets « itemball_area09_… » ; « _dlc1 » :
Septentria, objets « itemball_su1_areaNN » ; « _dlc2 » : Terra-Dôme, objets « su2_areaNN ») ; le texte est la classe,
traduite ici à la main (CLASSES), et le nom anglais de FlagsEditorEX.
"""
import csv
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from completion_gen3 import anchored_rank  # noqa: E402

MAX_ITEM = 2557

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
        sections[current].append((int(p[0], 16), p[1], p[2].strip().replace('’', "'"), p[3].strip(), p[4].strip(),
                                  p[6].strip()))
    return sections


class Ctx:
    def __init__(self, root):
        self.sections = load_flags(os.path.join(root, 'flags_gen9sv_en.txt'))
        # Nom interne → condition : drapeau (bloc booléen « X: »), variable (bloc numérique « XV:…:1 »), dresseur (« TR: »)
        self.cond, self.work, self.trainer = {}, {}, {}
        for flag, kind, loc, sub, desc, name in self.sections['Event Flags']:
            if name:
                self.cond.setdefault(name, f'X:{flag & 0xFFFFFFFF:08X}')
        for flag, kind, loc, sub, desc, name in self.sections['Event Work']:
            if name:
                self.work.setdefault(name, f'{flag & 0xFFFFFFFF:08X}')
        for flag, kind, loc, sub, desc, name in self.sections['Trainer Flags']:
            if name:
                self.trainer.setdefault(name, f'TR:{flag:016X}')
        self.items_en = lines_of(os.path.join(root, 'text_Items_en.txt'))
        self.item_ids = {}
        for n, name in enumerate(self.items_en[:MAX_ITEM + 1]):
            self.item_ids.setdefault(name.replace('’', "'"), []).append(n)
        self.species_en = {n: i for i, n in enumerate(lines_of(os.path.join(root, 'text_Species_en.txt')))}
        self.species_fr = {n: i for i, n in enumerate(lines_of(os.path.join(root, 'text_Species_fr.txt')))}
        en = lines_of(os.path.join(root, 'text_sv_00000_en.txt'))
        fr = lines_of(os.path.join(root, 'text_sv_00000_fr.txt'))
        self.place_fr = {}
        for e, f in zip(en, fr):
            e, f = e.replace('’', "'"), f.replace('’', "'")
            if e and e not in self.place_fr:
                self.place_fr[e] = f

    def f(self, *names):
        for n in names:
            if n not in self.cond:
                raise SystemExit(f'Drapeau inconnu : {n}')
        return '+'.join(self.cond[n] for n in names)

    def a(self, *names):
        return '|'.join(self.f(n) for n in names)

    def badge(self, name):
        """Variable « Index for received Badge … » : rang d'obtention du badge, 0 tant qu'il manque."""
        if name not in self.work:
            raise SystemExit(f'Variable inconnue : {name}')
        return f'XV:{self.work[name]}:1'

    def tr(self, *names):
        for n in names:
            if n not in self.trainer:
                raise SystemExit(f'Dresseur inconnu : {n}')
        return '|'.join(self.trainer[n] for n in names)

    def item(self, name):
        name = ITEM_FIXES.get(name, name)
        ids = self.item_ids.get(name)
        if not ids:
            UNKNOWN.add(f'objet {name}')
            return 0
        return ids[-1]  # un nom repris d'une génération précédente : le numéro le plus récent

    def species(self, name, french=True):
        table = self.species_fr if french else self.species_en
        if name not in table:
            UNKNOWN.add(f'espèce {name}')
            return 0
        return table[name]

    def place(self, loc):
        """Lieu français d'un lieu anglais de FlagsEditorEX."""
        loc = PLACE_EN.get(loc, loc)
        if loc not in self.place_fr:
            UNKNOWN.add(f'lieu {loc}')
            return loc
        return self.place_fr[loc]


# Noms anglais de FlagsEditorEX → noms anglais de PKHeX
PLACE_EN = {'Glaseado': 'Glaseado Mountain'}
ITEM_FIXES = {'Punching Gloves': 'Punching Glove'}

# Sous-lieux de FlagsEditorEX : (lieu de PKHeX à la place du lieu, précision) ; None : rien à ajouter
SUBPLACES = {
    '(cave)': (None, 'grotte'), '(core)': (None, 'cœur'), '(alcove)': (None, 'renfoncement'),
    '(Crystal Pool cave)': (None, 'grotte du Lac Cristallin'),
    '[needs to send PKMN inside the rock]': (None, 'envoyer un Pokémon dans le rocher'),
    'Colonnade Hollow': (None, 'grotte aux colonnes'),
}


def split_place(c, loc, sub):
    """(lieu, précision) d'un objet : au Terra-Dôme, le biome (« Coastal Biome (cave) ») ou le poste devient le lieu."""
    sub = sub.strip()
    if not sub:
        return c.place(loc), None
    m = re.fullmatch(r'(.+?)(?: (\(cave\)))?', sub)
    if m.group(1) in c.place_fr and m.group(1) not in SUBPLACES:
        return c.place(m.group(1)), 'grotte' if m.group(2) else None
    if sub in SUBPLACES:
        place, detail = SUBPLACES[sub]
        return c.place(place or loc), detail
    UNKNOWN.add(f'sous-lieu {sub}')
    return c.place(loc), None


def item_parts(c, text):
    """« Ultra Ball (x3) » → (numéro, 3)."""
    m = re.fullmatch(r'(.+?)(?: ?\(x ?(\d+)\))?', text.strip())
    return c.item(m.group(1).strip()), int(m.group(2) or 1)


# ---------------------------------------------------------------------------------------------------------------------
# Histoire (check-list de l'utilisateur ; seulement les étapes qui laissent une trace dans la sauvegarde)

def story(c, version):
    scarlet = version == 'SL'
    legend = 'Koraidon' if scarlet else 'Miraidon'
    academy = 'Académie Orange' if scarlet else 'Académie Raisin'
    clear = c.f('FSYS_SCENARIO_GAME_CLEAR')
    su1 = c.f('FSYS_SCENARIO_GAME_CLEAR_SU1')
    su2 = c.f('FSYS_SCENARIO_GAME_CLEAR_SU2')
    return [
        ('Cuchalaga', None, 'Choisir son premier Pokémon', 'D:906|D:909|D:912'),
        ('Cuchalaga', 'plage', "Recevoir l'appli Pokédex (combat contre Menzi)", c.f('FSYS_YMAP_TOPMENU_UNLOCK_00')),
        ('Mesaledo', None, f'Pouvoir chevaucher {legend}', c.f('FSYS_RIDE_ENABLE')),
        # La Voie du Maître : les huit Arènes (rang d'obtention de chaque badge)
        ('Sevaro', 'Arène', 'Badge Insecte (Éra)', c.badge('WEVT_GYM_MUSHI_CLEAR')),
        # Un parfum de légende : les Pokémon Dominants, et le pouvoir que chacun donne à la monture
        ('Zone Sud nº 3', 'Roche-Falaise', 'Battre le Dominant Craparoi', c.badge('WEVT_NUSHI_IWA_CLEAR')),
        ('Zone Sud nº 3', None, f'{legend} : pouvoir Accélération', c.f('FSYS_RIDE_DASH_ENABLE')),
        ('Cuencia', 'Arène', 'Badge Plante (Colza)', c.badge('WEVT_GYM_KUSA_CLEAR')),
        ('Zone Ouest nº 1', 'Vaste-Ciel', 'Battre le Dominant Lestombaile', c.badge('WEVT_NUSHI_HIKOU_CLEAR')),
        ('Zone Ouest nº 1', None, f'{legend} : pouvoir Nage', c.f('FSYS_RIDE_SWIM_ENABLE')),
        # Objectif Stardust : les cinq groupes de la Team Star
        ('Repaire du Groupe Segin', None, 'Battre Brome (Groupe Segin, Ténèbres)', c.badge('WEVT_DAN_AKU_CLEAR')),
        ('Levalendura', 'Arène', 'Badge Électrik (Mashynn)', c.badge('WEVT_GYM_DENKI_CLEAR')),
        ('Repaire du Groupe Schedar', None, 'Battre Meloco (Groupe Schedar, Feu)', c.badge('WEVT_DAN_HONOO_CLEAR')),
        # FlagsEditorEX inverse les lieux de ces deux variables : HAGANE (Acier) est Ferdeter, JIMEN (Sol) le Désert
        ('Zone Est nº 3', 'Acier-Tranchée', 'Battre le Dominant Ferdeter', c.badge('WEVT_NUSHI_HAGANE_CLEAR')),
        ('Zone Est nº 3', None, f'{legend} : pouvoir Saut', c.f('FSYS_RIDE_HIJUMP_ENABLE')),
        ('Jarramanca', 'Arène', 'Badge Eau (Kombu)', c.badge('WEVT_GYM_MIZU_CLEAR')),
        ('Repaire du Groupe Tsih', None, 'Battre Erio (Groupe Tsih, Poison)', c.badge('WEVT_DAN_DOKU_CLEAR')),
        ('Mezclamora', 'Arène', 'Badge Normal (Okuba)', c.badge('WEVT_GYM_NORMAL_CLEAR')),
        ('Frigao', 'Arène', 'Badge Spectre (Laïm)', c.badge('WEVT_GYM_GHOST_CLEAR')),
        ('Désert Rôtissable', 'Tremble-Terre',
         'Battre le Dominant Fort-Ivoire' if scarlet else 'Battre le Dominant Roue-de-Fer', c.badge('WEVT_NUSHI_JIMEN_CLEAR')),
        ('Désert Rôtissable', None, f'{legend} : pouvoir Planer', c.f('FSYS_RIDE_GLIDE_ENABLE')),
        ('Alforneira', 'Arène', 'Badge Psy (Tully)', c.badge('WEVT_GYM_ESPER_CLEAR')),
        ('Mont Nappé', 'Arène', 'Badge Glace (Grusha)', c.badge('WEVT_GYM_KOORI_CLEAR')),
        ('Repaire du Groupe Ruchbah', None, 'Battre Ortiga (Groupe Ruchbah, Fée)', c.badge('WEVT_DAN_FAIRY_CLEAR')),
        ('Lac Asrol', None, 'Battre le Dominant Oyacata, puis Nigirigon', c.badge('WEVT_NUSHI_DRAGON_CLEAR')),
        ('Lac Asrol', None, f'{legend} : pouvoir Escalade', c.f('FSYS_RIDE_CLIMB_ENABLE')),
        ('Repaire du Groupe Caph', None, 'Battre Nèflie (Groupe Caph, Combat)', c.badge('WEVT_DAN_KAKUTOU_CLEAR')),
        # La Ligue : l'entretien de Cayenn, puis les combats (lancés par script : leur trace n'est pas sûre, l'histoire
        # finie les fait d'office)
        ('Ligue Pokémon', None, "Réussir l'entretien de l'épreuve de maîtrise", c.f('FEVT_CHAMP_PASS_1ST_INTERVIEW')),
        ('Ligue Pokémon', None, 'Battre Cayenn (Conseil 4, Sol)', f"{c.tr('e4_jimen_01')}|{clear}"),
        ('Ligue Pokémon', None, 'Battre Popi (Conseil 4, Acier)', f"{c.tr('e4_hagane_01')}|{clear}"),
        ('Ligue Pokémon', None, 'Battre Okuba (Conseil 4, Vol)', f"{c.tr('e4_hikou_01')}|{clear}"),
        ('Ligue Pokémon', None, 'Battre Hassa (Conseil 4, Dragon)', f"{c.tr('e4_dragon_01')}|{clear}"),
        ('Ligue Pokémon', None, 'Devenir Maître : battre Alisma, Maîtresse en Chef', f"{c.tr('chairperson_01')}|{clear}"),
        (academy, None, 'Battre Cassiopée (Pania), cheffe de la Team Star', f"{c.tr('botan_01')}|{clear}"),
        ('Zone Zéro', None, "Désactiver les verrous des Postes d'Observation", f"{c.f('FEVT_ATLANTIS_BASE_A3_EVENT_END')}|{clear}"),
        ('Zone Zéro', 'Laboratoire Zéro',
         "Finir l'histoire : battre Olim (IA) et le Koraidon du Laboratoire" if scarlet
         else "Finir l'histoire : battre Turum (IA) et le Miraidon du Laboratoire", clear),
        # Le Masque Turquoise
        ('Jaderaude', None, 'Arriver à Septentria (Le Masque Turquoise)', c.f('FSYS_YMAP_SU1MAP_CHANGE')),
        ('Jaderaude', None, 'Recevoir le Pokédex de Septentria', c.f('FSYS_DLC1_POKEDEX_ADD')),
        ('Lande de la Quiétude', None, 'Battre Félicanis', c.badge('WEVT_S1_INU_CLEAR_NUM')),
        ('Plaine des Glycines', None, 'Battre Fortusimia', c.badge('WEVT_S1_SARU_CLEAR_NUM')),
        ('Mont Strueux', None, 'Battre Favianos', c.badge('WEVT_S1_KIZI_CLEAR_NUM')),
        ('Antre Interdit', None, 'Capturer Ogerpon', 'D:1017'),
        ('Jaderaude', None, "Finir l'histoire du Masque Turquoise", su1),
        # Le Disque Indigo
        ('Institut Myrtille', None, "Arriver à l'Institut Myrtille (Le Disque Indigo)", c.f('FSYS_YMAP_S2_MAPCHANGE_ENABLE')),
        ('Institut Myrtille', None, 'Recevoir le Pokédex de Myrtille', c.f('FSYS_DLC2_POKEDEX_ADD')),
        ('Institut Myrtille', None, 'Battre Taro (Conseil 4 de la Ligue Myrtille, Fée)', f"{c.tr('fairy4_02_02')}|{su2}"),
        ('Institut Myrtille', None, 'Battre Rubépin (Conseil 4 de la Ligue Myrtille, Feu)', f"{c.tr('hono4_02_01')}|{su2}"),
        ('Institut Myrtille', None, 'Battre Irido (Conseil 4 de la Ligue Myrtille, Dragon)', f"{c.tr('dragon4_02_01')}|{su2}"),
        ('Institut Myrtille', None, 'Battre Nérine (Conseil 4 de la Ligue Myrtille, Acier)', f"{c.tr('hagane4_02_01')}|{su2}"),
        ('Institut Myrtille', None, 'Battre Kassis, Maître de la Ligue Myrtille', f"{c.tr('brother_02_01')}|{su2}"),
        ('Abîme Zéro', None, 'Capturer Terapagos', 'D:1024'),
        ('Abîme Zéro', None, "Finir l'histoire du Disque Indigo", su2),
        ('Terra-Dôme', 'Nérine', f'{legend} : pouvoir Vol', c.f('FSYS_RIDE_FLIGHT_ENABLE')),
        ('Parc Adorami', 'épilogue « La nuit des kibi »', 'Capturer Pêchaminus', 'D:1025'),
    ]


# ---------------------------------------------------------------------------------------------------------------------
# Lecture de la check-list

def checklist(path):
    with open(path, encoding='utf-8-sig') as fh:
        return list(csv.DictReader(fh, delimiter=';'))


VERSION_NAMES = {'SL': 'Écarlate', 'VL': 'Violet'}


def for_version(row, version):
    v = (row.get('Version') or '').strip().rstrip('*')
    return v in ('', 'Les deux', '—') or v == VERSION_NAMES[version]


def pokedex(rows):
    """Espèces de la catégorie « Pokédex » (présentes dans le jeu), dans l'ordre national."""
    out = []
    for row in rows:
        if row['Catégorie'] == 'Pokédex':
            out.append(int(re.match(r'#(\d+)', row['Objectif']).group(1)))
    return sorted(set(out))


def machines(rows):
    """CT de la check-list : numéro → lieu (texte de la check-list)."""
    out = {}
    for row in rows:
        if row['Catégorie'] == 'CT':
            out[int(re.match(r'CT(\d+)', row['Objectif'].strip()).group(1))] = row['Lieu'].strip()
    return out


# ---------------------------------------------------------------------------------------------------------------------
# Objets

def field_items(c):
    """Objets au sol : (lieu, précision, condition, objet, quantité, CT) ; CT : son numéro (comptée par sa recette)."""
    out = []
    for flag, kind, loc, sub, desc, name in c.sections['Field Item Flags']:
        text = re.sub(r' item ball disappeared$', '', desc)
        place, detail = split_place(c, loc, sub)
        tm = re.fullmatch(r'TM(\d+)(?: \(.*\))?', text)
        if tm:
            out.append((place, detail, None, 0, 0, int(tm.group(1))))
            continue
        item, count = item_parts(c, text)
        out.append((place, detail, f'FI:{flag:016X}', item, count, 0))
    return out


def gifts(c):
    """Cadeaux d'objets (représentants de la Ligue, Clan des Monstres) ; les CT offertes comptent par leur recette."""
    out = []
    for flag, kind, loc, sub, desc, name in c.sections['Event Flags']:
        if kind != 'ITEM GIFT':
            continue
        m = re.fullmatch(r'Received (.+?) from (.+?) for defeating (.+)', desc)
        if not m:
            UNKNOWN.add(f'cadeau {desc}')
            continue
        if m.group(1).startswith('TM'):
            continue
        item, count = item_parts(c, m.group(1))
        if m.group(2) == 'Muramasa':
            n = re.match(r'(\d+)', m.group(3)).group(1)
            place, detail = 'Jaderaude', f'Muramasa, {n} membre{"s" if n != "1" else ""} du Clan des Monstres battu{"s" if n != "1" else ""}'
        else:
            n = re.search(r'(\d+) trainers', m.group(3)).group(1)
            place, detail = c.place(loc), f'représentant de la Ligue, {n} dresseurs battus'
        out.append((place, detail, c.f(name), item, count))
    return out


# ---------------------------------------------------------------------------------------------------------------------
# Dresseurs

# Classes de FlagsEditorEX → (masculin, féminin, genre inconnu), traduites à la main ; le genre vient du nom interne
# (« _f », « girl » ; « _m », « boy »), jamais du prénom : inconnu, la forme épicène « Musicien(ne) »
CLASSES = {
    'Student': ('Élève',) * 3, 'Hiker': ('Montagnard', 'Montagnarde', 'Montagnard(e)'), 'Black Belt': ('Karatéka',) * 3,
    'Backpacker': ('Routard', 'Routarde', 'Routard(e)'),
    'Dragon Tamer': ('Dompteur de dragons', 'Dompteuse de dragons', 'Dompteur(se) de dragons'),
    'Courier': ('Livreur', 'Livreuse', 'Livreur(se)'),
    'Office Worker': ('Employé de bureau', 'Employée de bureau', 'Employé(e) de bureau'),
    'Poké Maniac': ('Pokémaniac',) * 3, 'Youngster': ('Gamin', 'Gamine', 'Gamin(e)'),
    'Musician': ('Musicien', 'Musicienne', 'Musicien(ne)'), 'Festival Boy': ('Enfant de la fête',) * 3,
    'Festival Girl': ('Enfant de la fête',) * 3, 'Worker': ('Ouvrier', 'Ouvrière', 'Ouvrier(ère)'),
    'Scientist': ('Scientifique',) * 3, 'Model': ('Mannequin',) * 3, 'Artist': ('Artiste',) * 3,
    'Cabbie': ('Chauffeur de taxi', 'Chauffeuse de taxi', 'Chauffeur(se) de taxi'),
    'Cook': ('Cuisinier', 'Cuisinière', 'Cuisinier(ère)'), 'Waitress': ('Serveuse',) * 3, 'Waiter': ('Serveur',) * 3,
    'Janitor': ("Agent d'entretien", "Agente d'entretien", "Agent(e) d'entretien"), 'Delinquent': ('Voyou',) * 3,
    'Ruffian': ('Loubard', 'Loubarde', 'Loubard(e)'), 'League Club': ('Membre du Club de la Ligue',) * 3,
    'Kitakami Ogre Clan': ('Clan des Monstres :',) * 3,
    'Gym Trainer': ("Dresseur de l'Arène", "Dresseuse de l'Arène", "Dresseur(se) de l'Arène"),
}
GYMS = {'mushi': ('Sevaro', 'WEVT_GYM_MUSHI_CLEAR'), 'kusa': ('Cuencia', 'WEVT_GYM_KUSA_CLEAR'),
        'denki': ('Levalendura', 'WEVT_GYM_DENKI_CLEAR'), 'mizu': ('Jarramanca', 'WEVT_GYM_MIZU_CLEAR'),
        'normal': ('Mezclamora', 'WEVT_GYM_NORMAL_CLEAR'), 'ghost': ('Frigao', 'WEVT_GYM_GHOST_CLEAR'),
        'esper': ('Alforneira', 'WEVT_GYM_ESPER_CLEAR'), 'koori': ('Mont Nappé', 'WEVT_GYM_KOORI_CLEAR')}


def trainer_label(desc, name):
    text = desc.removeprefix('Battled ')
    for cls in sorted(CLASSES, key=len, reverse=True):
        if text.startswith(cls + ' '):
            if re.search(r'_f(?:_|$)|girl|_ol(?:_|$)', name):
                form = 1
            elif re.search(r'_m(?:_|$)|boy|businessman', name):
                form = 0
            else:
                form = 2
            return f'{CLASSES[cls][form]} {text[len(cls) + 1:]}'
    UNKNOWN.add(f'classe {text}')
    return text


def area_places(c):
    """Zone interne → lieu : celui de la plupart des objets au sol de cette zone (Paldea, Septentria, Terra-Dôme)."""
    votes = {}
    for flag, kind, loc, sub, desc, name in c.sections['Field Item Flags']:
        m = re.match(r'(?:itemball_(?:rare_)?(su1_)?area(\d+)|su2_area(\d+))_', name)
        if not m:
            continue
        key = ('dlc2', int(m.group(3))) if m.group(3) else ('dlc1' if m.group(1) else 'paldea', int(m.group(2)))
        place, _ = split_place(c, loc, sub)
        votes.setdefault(key, {}).setdefault(place, 0)
        votes[key][place] += 1
    return {k: max(v.items(), key=lambda t: t[1])[0] for k, v in votes.items()}


def trainers(c):
    """Dresseurs : (lieu, précision, condition, texte)."""
    places = area_places(c)
    out = []
    for flag, kind, loc, sub, desc, name in c.sections['Trainer Flags']:
        cond = f'TR:{flag:016X}'
        gym = re.fullmatch(r'gym_([a-z]+)_\d+', name)
        if gym and desc:  # dresseurs d'Arène : à battre avant le champion (son badge les fait d'office)
            town, badge = GYMS[gym.group(1)]
            out.append((town, 'Arène', f'{cond}|{c.badge(badge)}', trainer_label(desc, name)))
            continue
        if kind != 'TRAINER BATTLE':
            continue  # scénario, revanches (« _strong »), dresseurs sans description
        m = re.match(r'area(\d+)(?:_(dlc[12]))?_trainer_', name)
        if m:
            place = places.get((m.group(2) or 'paldea', int(m.group(1))))
            if place is None:
                UNKNOWN.add(f'zone de dresseur {name}')
                continue
            out.append((place, None, cond, trainer_label(desc, name)))
        elif name.startswith('sp_trainer_'):
            out.append(('Jaderaude', 'Clan des Monstres', cond, trainer_label(desc, name)))
        else:
            UNKNOWN.add(f'dresseur {name}')
    return out


# ---------------------------------------------------------------------------------------------------------------------
# Rencontres et quêtes

def encounters(c, rows, version):
    scarlet = version == 'SL'
    u = [
        ('Zone Zéro', 'après l\'histoire', 1007 if scarlet else 1008, c.f('FEVT_SUB_018_CAPTURED'),
         'Koraidon (deuxième)' if scarlet else 'Miraidon (deuxième)'),
        # Les Pokémon Dominants, capturables après leur combat
        ('Zone Sud nº 3', 'Dominant', 950, c.f('NUSHI_IWA_AFTER_PERMANENT'), None),
        ('Zone Ouest nº 1', 'Dominant', 962, c.f('NUSHI_HIKO_AFTER_PERMANENT'), None),
        ('Zone Est nº 3', 'Dominant', 968, c.f('NUSHI_HAGANE_AFTER_PERMANENT'), None),
        ('Désert Rôtissable', 'Dominant', 984 if scarlet else 990, c.f('NUSHI_JIMEN_AFTER_PERMANENT'), None),
        ('Lac Asrol', 'Dominant', 978, c.f('NUSHI_DRAGON_AFTER_PERMANENT'), None),
        # Le Masque Turquoise : les Adorables
        ('Lande de la Quiétude', None, 1014, c.f('FEVT_S1_SUB_011_CAPTURED'), None),
        ('Plaine des Glycines', None, 1015, c.f('FEVT_S1_SUB_012_CAPTURED'), None),
        ('Mont Strueux', None, 1016, c.f('FEVT_S1_SUB_016_CAPTURED'), None),
    ]
    # Légendaires de la check-list (pas ceux de l'histoire : Koraidon, Miraidon, Ogerpon, Terapagos, Pêchaminus ; pas
    # ceux du dessus) : par le Pokédex. Les Fléaux : à leur sanctuaire (leurs pieux : quêtes)
    done = {1007, 1008, 1017, 1024, 1025, 1014, 1015, 1016}
    for row in rows:
        if row['Catégorie'] != 'Légendaire / fixe' or not for_version(row, version):
            continue
        text = row['Objectif'].strip()
        m = re.match(r"(?:Faire évoluer )?(.+?)(?: \(2e exemplaire\))?(?: \(?niv\..*| en .+| via .+|$)", text)
        name = m.group(1).strip()
        if name.startswith(('Montrer', 'Obtenir')):
            continue  # pas un Pokémon (Fléaux montrés à Mora, Friandises de Jeffry Andise)
        if text.startswith('Faire évoluer'):
            name = re.sub(r'^.* en ', '', text)
        name = {'Ursaking Lune Vermeille – aider Lithia': 'Ursaking'}.get(name, name)
        species = c.species(name)
        if species in done:
            continue
        done.add(species)
        zone, detail = legend_place(row['Lieu'])
        label = 'Ursaking Lune Vermeille' if species == 901 else None
        u.append((zone, detail, species, f'D:{species}', label))
    # Échanges : les trois de Paldea ont un drapeau ; ceux du Club de la Ligue (Disque Indigo), aucun : laissés de côté
    for name, zone, got, give, label in (('FEVT_SUB_044_TRADE_A', 'Sevaro', 872, 669, None),
                                         ('FEVT_SUB_044_TRADE_B', 'Jarramanca', 194, 194, 'Axoloto (contre Axoloto de Paldea)'),
                                         ('FEVT_SUB_044_TRADE_C', 'Levalendura', 93, 871, None)):
        u.append((zone, 'échange', got, c.f(name), label or ('give', give)))
    return u


# Lieux des légendaires de la check-list (texte libre) : un lieu d'ORDER cité (le dernier membre d'un « A – B » qui en
# cite un), le reste du texte en précision ; les Fléaux à leur sanctuaire
LEGEND_FIXES = {'phare de Cuchalaga': 'Cuchalaga', 'Cascade Furie': 'Zone Nord nº 2', 'Roc Onaissance': 'Zone Ouest nº 1',
                'Sanctuaire du Sol Corrompu': 'Forêt Toastée', 'Étang des Glycines': 'Plaine des Glycines',
                "grotte d'Alforneira": "Grotte d'Alforneira", 'base du Groupe Segin': 'Zone Ouest nº 1',
                "l'île la plus au nord-est de Paldea": "Mer de l'Est de Paldea"}


def numbered(text):
    """« Zone Sud n°4 », « Poste d'Observation n°3 » : l'ordinal de PKHeX, « nº 4 »."""
    return re.sub(r'n°\s*(\d)', lambda m: f'nº {m.group(1)}', text)


def legend_place(text):
    text = numbered(text.strip())
    if text.startswith('Sanctuaire du '):
        m = re.fullmatch(r'(Sanctuaire du [^,(]+?)(?:, (.+)| \((.+)\))', text)
        return m.group(1), m.group(2) or m.group(3)
    parts = text.split(' – ')
    for k in range(len(parts) - 1, -1, -1):
        norm = parts[k]
        for a_, b_ in LEGEND_FIXES.items():
            norm = norm.replace(a_, b_)
        found = [(norm.find(z), -len(z), z) for z in ORDER if z in norm]
        if not found:
            continue
        zone = min(found)[2]
        # La précision : le texte sans le lieu retenu (en tête, ou entre parenthèses), les membres qui suivent compris
        own = re.sub(rf'^{re.escape(zone)}(?:, | )?', '', parts[k]).replace(f'({zone})', '').strip(' ,')
        own = re.sub(r'^\((.*)\)$', lambda m: m.group(1), own)
        others = [p for i, p in enumerate(parts) if i > k]
        detail = ', '.join(p for p in [own] + others if p and p != 'Septentria')
        if not detail:
            return zone, None
        if not any(detail.startswith(z) for z in ORDER):
            detail = detail[0].lower() + detail[1:]
        return zone, detail
    UNKNOWN.add(f'lieu de légendaire {text}')
    return text, None


def quests(c):
    q = []
    stakes = [('014', 'Dinglu'), ('015', 'Baojian'), ('016', 'Chongjian'), ('017', 'Yuyu')]
    for sub, legend in stakes:
        for n in range(1, 9):
            q.append(('Pieux des Fléaux', None, f'Pieu de {legend} n° {n}', c.f(f'FEVT_SUB_{sub}_KUI_{n:02d}_RELEASE')))
    leaders = [('mushi', 'Sevaro', 'Éra'), ('kusa', 'Cuencia', 'Colza'), ('denki', 'Levalendura', 'Mashynn'),
               ('mizu', 'Jarramanca', 'Kombu'), ('normal', 'Mezclamora', 'Okuba'), ('ghost', 'Frigao', 'Laïm'),
               ('esper', 'Alforneira', 'Tully'), ('koori', 'Mont Nappé', 'Grusha')]
    for gym, town, leader in leaders:
        q.append(("Revanches d'Arène", town, f"Revanche contre {leader} (visite d'inspection)", c.tr(f'gym_{gym}_leader_02')))
    for squad, boss in (('aku', 'Brome'), ('hono', 'Meloco'), ('doku', 'Erio'), ('fairy', 'Ortiga'), ('kakutou', 'Nèflie')):
        q.append(('Revanches de la Team Star', 'repaires de la Team Star', f'Revanche contre {boss}', c.tr(f'dan_{squad}_boss_02')))
    q.append(('Revanches de la Team Star', 'repaires de la Team Star', 'Revanche contre Pania', c.tr('botan_02')))
    for n in range(3, 7):
        q.append(('Raids Téracristal', None, f'Débloquer les raids {n}★', c.f(f'FSYS_RAID_DIFFICTLTY{n}_RELEASE')))
    for flag, course in (('NORMAL', 'normal'), ('HARD', 'difficile'), ('VERY_HARD', 'très difficile')):
        q.append(('Glisse du Mont Nappé', 'Mont Nappé', f'Réussir le parcours {course}', c.f(f'FEVT_SUB_GYM_KOORI_{flag}_CLER')))
    q.append(('Mordudor', None, 'Réunir 999 pièces et faire évoluer Mordudor en Gromago', 'D:1000'))
    for flag, label in (('OniballoonSave_isCleardEasy', 'mode facile'), ('OniballoonSave_isCleardNormal', 'mode normal'),
                        ('OniballoonSave_isCleardHard', 'mode difficile')):
        q.append(('Chasse aux Monstres', None, f'Réussir le {label}', c.f(flag)))
    for n in range(1, 4):
        q.append(("Quête de Billy et O'Nare", None, f"Quête de Billy et O'Nare : partie {n}",
                  c.f({1: 'FEVT_S1_SUB_003_TALKED', 2: 'FEVT_S1_SUB_006_TALKED', 3: 'FEVT_S1_SUB_009_TALKED'}[n])))
    for flag, label in (('FSYS_POKEDEX_SYOUJOU_EVENT', 'Diplôme du Pokédex de Paldea'),
                        ('FSYS_DLC1_POKEDEX_SYOUJOU_EVENT', 'Diplôme du Pokédex de Septentria'),
                        ('FSYS_DLC2_POKEDEX_SYOUJOU_EVENT', 'Diplôme du Pokédex de Myrtille')):
        q.append(('Diplômes du Pokédex', None, label, c.f(flag)))
    # Coachs spéciaux du Club de la Ligue (Disque Indigo) : chaque invité venu au club
    for flag, kind, loc, sub, desc, name in c.sections['Event Flags']:
        m = re.fullmatch(r'KUnlockedSpecialCoach([A-Z]\w*)', desc)
        if m:
            q.append(('Club de la Ligue', 'Institut Myrtille', f'Coach spécial : {COACHES[m.group(1)]}', c.f(name)))
    return q


COACHES = {'Penny': 'Pania', 'Poppy': 'Popi', 'Rika': 'Cayenn', 'Geeta': 'Alisma', 'Arven': 'Pepper', 'Nemona': 'Menzi',
           'Hassel': 'Hassa', 'Dendra': 'Élige', 'Jacq': 'Jacq', 'Clavell': 'Clavel', 'Miriam': 'Mimosa', 'Raifort': 'Mora',
           'Saguaro': 'Saguaro', 'Salvatore': 'Salvio', 'Tyme': 'Thaïm'}


# ---------------------------------------------------------------------------------------------------------------------

def build(c, version, rows, order, cur):
    out = []

    def add(section, zone, det, done, label=None, item=0, tm=0, species=0, count=0, give=0, sort=0):
        if section == 'Quests':
            rank = 0  # groupées par activité, dans l'ordre de la liste
        else:
            rank = anchored_rank(zone, order, ANCHORS)
            if rank is None:
                MISSING_PLACES.add(zone)
                rank = 10_000
        out.append(dict(section=section, zone=zone, detail=det, done=done, label=label, item=item, tm=tm,
                        species=species, count=count, give=give, sort=(rank, zone, sort)))

    S, T, I, U, Q = 'Story', 'Trainers', 'Items', 'Encounters', 'Quests'
    for i, (zone, det, label, cond) in enumerate(cur['story']):
        add(S, zone, det, cond, label, sort=i)
    for i, (zone, det, cond, label) in enumerate(cur['trainers']):
        add(T, zone, det, cond, label, sort=i)
    tm_places = {}
    for i, (zone, det, cond, item, count, tm) in enumerate(cur['items']):
        if tm:
            tm_places.setdefault(tm, (zone, det))
            continue
        add(I, zone, det, cond, item=item, count=count if count > 1 else 0, sort=i)
    for i, (zone, det, cond, item, count) in enumerate(cur['gifts']):
        add(I, zone, det, cond, item=item, count=count if count > 1 else 0, sort=50_000 + i)
    # CT : faite dès que sa recette est débloquée (au sol, offerte, achetée : la CT ramassée la débloque) ; lieu : son
    # objet au sol s'il y en a un, sinon la check-list (Machine à CT)
    for number, where in sorted(cur['machines'].items()):
        zone, det = tm_places.get(number, ('Machine à CT', where[0].lower() + where[1:] if where else None))
        add(I, zone, det, c.f(f'FSYS_UI_WAZA_MACHINE_RELEASE_{number:03d}'), tm=number, sort=100_000 + number)
    for i, (zone, det, species, cond, label) in enumerate(cur['encounters']):
        give = 0
        if isinstance(label, tuple):
            give, label = label[1], None
        add(U, zone, det, cond, label, species=species, give=give, sort=i)
    for i, (zone, det, label, cond) in enumerate(cur['quests']):
        add(Q, zone, det, cond, label, sort=i)

    section_order = {s: n for n, s in enumerate((S, T, I, U, Q))}
    out.sort(key=lambda r: (section_order[r['section']],) + ((r['sort'][2],) if r['section'] in (S, U, Q) else r['sort']))
    return out


def cs(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"' if s is not None else 'null'


def emit(games, dex, out):
    lines = ['// <auto-generated>',
             "// Généré par outils/completion_gen9.py d'après la liste de drapeaux de FlagsEditorEX et la check-list de",
             "// l'utilisateur : ne pas modifier à la main, changer le script et le relancer.",
             '// </auto-generated>',
             '',
             'namespace MonsutaPotaru.Services;',
             '',
             'public static class Gen9CompletionData',
             '{',
             '    /// <summary>Pokédex d\'Écarlate / Violet : les espèces présentes dans le jeu (Paldea, Septentria, Myrtille et',
             '    /// légendaires des Friandises ; check-list de l\'utilisateur), dans l\'ordre national.</summary>',
             '    public static readonly int[] Pokedex =',
             '    [']
    for i in range(0, len(dex), 20):
        lines.append('        ' + ', '.join(str(n) for n in dex[i:i + 20]) + ',')
    lines += ['    ];', '']
    for name, doc, rows in games:
        lines.append(f'    /// <summary>Étapes de {doc} (le Pokédex est ajouté à part : <see cref="Pokedex"/>).</summary>')
        lines.append(f'    public static readonly CompletionRow[] {name} =')
        lines.append('    [')
        for r in rows:
            a_ = [f'CompletionSection.{r["section"]}', cs(r['zone']), cs(r['detail']), cs(r['done'])]
            n = [f'Label: {cs(r["label"])}'] if r['label'] else []
            for key, field in (('item', 'Item'), ('tm', 'Tm'), ('count', 'Count'), ('species', 'Species'), ('give', 'Give')):
                if r[key]:
                    n.append(f'{field}: {r[key]}')
            lines.append(f'        new({", ".join(a_ + n)}),')
        lines.append('    ];')
        lines.append('')
    lines[-1] = '}'
    with open(out, 'w', encoding='utf-8', newline='\n') as fh:
        fh.write('﻿' + '\n'.join(lines) + '\n')


# Lieux dans l'ordre de l'aventure (la route conseillée : Arènes, Dominants et Team Star mêlés), puis Septentria, puis
# la Myrtille ; un lieu absent arrête le générateur (sauf s'il a un voisin dans ANCHORS)
ORDER = [
    'Cuchalaga', 'Caverne de la Crique', 'Route de Cuchalaga', 'Zone Sud nº 1', 'Plato Real', 'Mesaledo',
    'Académie Orange', 'Académie Raisin', 'Zone Sud nº 2', 'Sevaro', 'Zone Sud nº 4', 'Zone Sud nº 3', 'Cuencia',
    'Zone Ouest nº 1', 'Repaire du Groupe Segin', 'Zone Est nº 1', 'Levalendura', 'Zone Est nº 2',
    'Repaire du Groupe Schedar', 'Zone Est nº 3', 'Pinchoria', 'Zone Ouest nº 2', 'Désert Rôtissable', 'Jarramanca',
    'Porto Marinada', 'Zone Ouest nº 3', 'Bosquet Tagué', 'Repaire du Groupe Tsih', 'Mezclamora', 'Tunnel Mezcla-Pincho',
    'Mont Nappé', 'Frigao', 'Zone Sud nº 5', 'Zone Sud nº 6', 'Alforneira', "Grotte d'Alforneira", 'Zone Nord nº 3',
    'Repaire du Groupe Ruchbah', 'Lac Asrol', 'Forêt Toastée', 'Zone Nord nº 1', 'Zone Nord nº 2',
    'Repaire du Groupe Caph', 'Mer du Sud de Paldea', "Mer de l'Ouest de Paldea", "Mer de l'Est de Paldea",
    'Mer du Nord de Paldea', 'Ligue Pokémon', 'Cratère de Paldea', 'Zone Zéro',
    # Le Masque Turquoise
    'Jaderaude', 'Route de Septentria', 'Coteau Pommes', 'Sentier de la Liesse', 'Rives du Confluent', 'Parc Adorami',
    'Septemplion', 'Plaine des Glycines', 'Lande de la Quiétude', 'Mont Strueux', 'Gueule du Monstre',
    'Grotte Douche Froide', 'Lac Cristallin', 'Forêt Ternelle', 'Gorge de la Corne', 'Val Infernal',
    'Terres Vierges de Septentria', 'Antre Interdit',
    # Le Disque Indigo
    'Institut Myrtille', 'Terra-Dôme', 'Poste Central', 'Zone Savane', 'Poste Savane', 'Zone Côtière', 'Poste Côtier',
    'Zone Canyon', 'Poste Canyon', 'Zone Polaire', 'Poste Polaire', 'Caverne Électrolithe', 'Dédale des Torches',
    'Abîme Zéro', 'Machine à CT',
]
# Lieux absents d'ORDER : rangés avec un voisin (sanctuaires des Fléaux : rencontres, rangées dans l'ordre de la table)
ANCHORS = {'Sanctuaire du Bois Mourant': 'Ligue Pokémon', 'Sanctuaire du Sol Corrompu': 'Ligue Pokémon',
           'Sanctuaire du Feu Ravageur': 'Ligue Pokémon', 'Sanctuaire du Froid Cruel': 'Ligue Pokémon'}


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    root, out = sys.argv[1:3]
    rows = checklist(os.path.join(root, 'suivi_completion_pokemon_ecarlate_violet.csv'))
    c = Ctx(root)
    items, gift_items, trainer_rows, tms = field_items(c), gifts(c), trainers(c), machines(rows)
    games = []
    for name, version, doc in (('Scarlet', 'SL', 'Pokémon Écarlate'), ('Violet', 'VL', 'Pokémon Violet')):
        cur = dict(story=story(c, version), trainers=trainer_rows, items=items, gifts=gift_items, machines=tms,
                   encounters=encounters(c, rows, version), quests=quests(c))
        games.append((name, doc, build(c, version, rows, ORDER, cur)))
    if UNKNOWN:
        raise SystemExit('À traduire : ' + ', '.join(sorted(UNKNOWN)))
    if MISSING_PLACES:
        raise SystemExit('Lieux sans rang (ORDER, ANCHORS) : ' + ', '.join(sorted(MISSING_PLACES)))
    dex = pokedex(rows)
    emit(games, dex, out)
    print('Pokédex', len(dex))
    for name, _, rs in games:
        counts = {}
        for r in rs:
            counts[r['section']] = counts.get(r['section'], 0) + 1
        print(name, counts)
