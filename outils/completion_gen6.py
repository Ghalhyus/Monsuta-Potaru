"""
Génère MonsutaPotaru/Services/Completion/Gen6CompletionData.cs : X, Y, Rubis Oméga et Saphir Alpha.

    python outils/completion_gen6.py <dossier> <ROM de X ou Y> <ROM de Rubis Oméga ou Saphir Alpha> MonsutaPotaru/Services/Completion/Gen6CompletionData.cs

<ROM de …> : un .3ds déchiffré (ceux de l'utilisateur, dans son dossier de jeux 3DS : lui demander le chemin), ou le
dossier de sa RomFS extraite par Azahar / Citra ; n'importe quelle langue d'Europe (les textes français y sont). X et Y
ont les mêmes dresseurs (vérifié) ; Saphir Alpha : les dresseurs de Rubis Oméga, sbires Aqua et Magma échangés
(vérifié sur rubis-omega.sav : aucun sbire Aqua battu dans une partie finie). Lecture : completion_gen6_rom.py.

<dossier> contient :
- flags_gen6xy_en.txt et flags_gen6oras_en.txt : listes de FlagsEditorEX
  (https://github.com/fattard/FlagsEditorEXPlugin, flagslist/) : drapeaux nommés (noms internes du jeu), objets au sol,
  objets cachés et cadeaux (avec leur lieu en anglais) ;
- text_xy_00000_en.txt et text_xy_00000_fr.txt : lieux de la 6e génération de PKHeX
  (PKHeX.Core/Resources/text/locations/gen6, X / Y et Rubis Oméga / Saphir Alpha dans la même liste) ;
- text_Items_en.txt et text_Items_fr.txt : noms des objets de PKHeX (Resources/text/items) ;
- text_SuperTraining_fr.txt : noms des épreuves du Super Training de PKHeX (Resources/text/other/fr) ;
- suivi_completion_pokemon_x_y.csv et suivi_completion_pokemon_rubis_omega_saphir_alpha.csv : check-lists de
  l'utilisateur (d'après Pokébip) : CT / CS et leurs lieux, Méga-Gemmes, distributions.
À côté de ce script : completion_gen6_ordre.json, les lieux des étapes « Histoire » de ces check-lists, dans leur ordre.

Pas de désassemblage pour la 3DS, et les scripts compilés du jeu ne se lisent pas de façon fiable : histoire, rencontres et
quêtes sont choisies ici d'après les check-lists, avec les drapeaux et variables nommés de FlagsEditorEX, les badges et les
objets rares du sac. Dresseurs : lus dans les ROM (équipes, lieux, drapeaux), plus ceux que FlagsEditorEX situe sans
que la ROM le montre (Dresseurs Aériens, dresseurs d'Illumis, Pat & Joe).
"""
import csv
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from completion_gen3 import anchored_rank  # noqa: E402
from completion_gen6_rom import TRAINER_FLAGS, read_trainers  # noqa: E402

LOCAL_FLAGS = 0x60  # en dessous : propres à la carte
MAX_ITEM = 775  # dernier objet de Rubis Oméga / Saphir Alpha (Flûte Éon)

# Lieux absents des étapes d'histoire des check-lists : rangés avec un lieu voisin (en chaîne si le voisin manque aussi) ;
# une route ou un chenal se range d'office près de son numéro.
ANCHORS = {
    # X / Y
    'Bourg Croquis': 'Quarellis', 'Route 1': 'Quarellis', 'Forêt de Neuvartault': 'Route 3', 'Route 6': 'Palais Chaydeuvre',
    'Château de Combat': 'Route 7', 'Bourg Pokéflûte': 'Route 7', 'Ville Pokéflûte': 'Route 7', 'Route 9': 'Route 8',
    'Cave Connecterre': 'Route 8', 'Roche-sur-Gliffe': 'Route 8', 'Route 10': "Cromlac'h", 'Route 11': 'Grotte Miroitante',
    'Aquarium de Roche-sur-Gliffe': 'Roche-sur-Gliffe', 'Baie Azur': 'Route 12', "Palais du Lac": 'Route 12',
    'Route 15': 'Romant-sous-Bois', 'Romant-sous-Bois': 'Arène de Romant-sous-Bois', 'Route 16': 'Romant-sous-Bois',
    'Route 17': 'La Frescale', 'Caverne Gelée': 'La Frescale', 'Mozheim': 'Route 18', 'Route 18': 'Flusselles',
    'Route 20': 'Route 19', 'Village Pokémon': 'Route 20', 'Auffrac-les-Congères': "Arène d'Auffrac-les-Congères",
    'Route 21': 'Route Victoire', 'Route 22': 'Bourg Croquis', 'Antre Néréen': 'Batisques', 'Grotte Coda': 'Route Victoire',
    'Grotte Inconnue': 'Village Pokémon', 'Château Rillou': 'Route 7', 'Port Tempères': 'Arène de Port Tempères',
    'Centrale de Kalos': 'Route 13', 'Usine de Poké Balls': 'Route 15', 'Tour Maîtrise': 'Yantreizh',
    # Rubis Oméga / Saphir Alpha
    'Bourg-en-Vol': 'Route 101', 'Rosyères': 'Route 113', 'Chemin Ardent': 'Route 112', 'Sentier Sinuroc': 'Mont Chimnée',
    'Vergazon': 'Tunnel Mérazon', 'Parc Safari': 'Route 121', 'Grotte Tréfonds': 'Chenal 125', 'Pacifiville': 'Chenal 131',
    'Planque Magma': 'Planque Aqua/Magma', 'Planque Aqua': 'Planque Aqua/Magma', 'Lavandia Sea': 'Route 110',
    'Rosaville': 'Route 113', 'Désert': 'Route 111', 'Chemin Mystère': 'Route 111', 'Vermilava': 'Arène de Vermilava',
    'Clémenti-Ville': 'Arène de Clémenti-Ville', 'Cimetronelle': 'Arène de Cimetronelle', 'Atalanopolis':
    "Arène d'Atalanopolis", 'Lavandia Mer': 'Lavandia', 'Pilier Céleste': 'Route 131', 'Grotte Fondmer': 'Caverne Fondmer',
    'Ruines Désert': 'Route 111', 'Bateau Abandonné': 'Chenal 108', 'Musée Océano': 'Musée Océanographique',
    'Fort de Vanitas': 'Fort-Vanitas', 'Porte de P. Tempères': 'Port Tempères', 'Porte de R. s/Gliffe': 'Roche-sur-Gliffe',
    'Le Marina': 'Chenal 108', 'Maison des Pièges': 'Route 110', 'Éternara': 'Ligue Pokémon',
    # Mirages (après l'Épisode Delta)
    'Forêt Mirage': 'Hoenn', 'Grotte Mirage': 'Hoenn', 'Mont Mirage': 'Hoenn', 'Île Mirage': 'Hoenn',
    'Gouffre Informe': 'Hoenn', 'Grotte Zénith': 'Hoenn', "Île de l'Arc": 'Hoenn',
}

# Lieux de FlagsEditorEX absents de la liste de PKHeX (ou à préciser) : nom anglais de PKHeX, ou nom français direct (« = »)
PLACE_EN = {'Power Plant': 'Kalos Power Plant', 'PokeBall Factory': 'Poké Ball Factory', 'Lysandre Lab': 'Lysandre Labs',
            'Weather Institute': 'Weather Institute', 'Desert': 'Route 111'}
# Planques : une seule version (OR : Magma, AS : Aqua) ; la planque commune prend le nom de la version
HIDEOUTS = {'Team Magma / Aqua Hideout': None, 'Team Aqua Hideout 2': 'AS', 'Team Aqua hideout': 'AS',
            'Team Magma hideout': 'OR', 'Team Magma Hideout 2': 'OR'}

SUBPLACES = {
    'Gym': 'Arène', 'Poké Center': 'Centre Pokémon', 'Underwater': "sous l'eau", 'House': 'maison', 'Fossil Lab': 'Labo Fossile',
    "Sycamore's Lab": 'Labo Platane', 'Hotel': 'hôtel', 'Hotel Room': 'hôtel', 'Café Woof': 'Café Ouaf', 'Station': 'gare',
    "Trainer's School": 'École des Dresseurs', 'Office Building': 'immeuble', 'Ranch House': 'ranch', 'Courtyard': 'jardins',
    'Berry Fields': 'Champs de Baies', 'Summit': 'sommet', 'Seaside Cycling Road': 'Piste Cyclable', 'Seashore House':
    'Maison de la Plage', 'Cabin': 'cabine', 'Deck': 'pont', 'Corridor': 'couloir', 'Storage': 'réserve',
    'Exterior': 'extérieur', 'Inner Room': 'salle intérieure', 'Ice Room': 'salle de glace', 'Lower Room': 'salle basse',
    'Submarine Room': 'salle du sous-marin', 'Stairs Room': 'salle des escaliers', 'Lysandre Cafe': 'Café Lysandre',
    'Big Chamber': 'grande salle', 'Small Chamber': 'petite salle', 'Entrance': 'entrée', 'Inside': 'intérieur',
    'Outside': 'extérieur', 'Inner Cave': 'grotte intérieure', 'Back Room': 'arrière-salle', 'Main Area': 'salle principale',
    "Archie's / Maxie's Room": 'salle du chef', "Tabitha's / Matt's Room": "salle de l'admin", 'Area': 'zone', 'Room': 'salle',
}

# Noms anglais de FlagsEditorEX qui diffèrent de ceux de PKHeX
ITEM_FIXES = {'Shoothe Bell': 'Soothe Bell', 'Venosock': 'Venoshock', 'U-Turn': 'U-turn', 'Parlyz Heal': 'Paralyze Heal',
              'Pokeball': 'Poké Ball', 'Dousing Machine': 'Dowsing Machine', 'Pretty Wing': 'Pretty Feather'}

UNKNOWN = set()


def load_flags(path):
    """Drapeau → (type, lieu, sous-lieu, description, nom interne) ; variables (section « Event Work ») à part."""
    text = open(path, encoding='utf-8').read()
    flags_part, work_part = text.split('//\tEvent Work')
    flags, works = {}, {}
    for part, out in ((flags_part, flags), (work_part, works)):
        for line in part.splitlines():
            p = line.lstrip('﻿').split('\t')
            if not p[0].startswith('0x') or len(p) < 7:
                continue
            out[int(p[0], 16)] = (p[1], p[2], p[3], p[4], p[6] if len(p) > 6 else '')
    return flags, works


class Ctx:
    def __init__(self, flags, works, items_en, items_fr, en_places, fr_places, version):
        self.flags, self.works, self.version = flags, works, version
        self.num = {}
        for flag, (_, _, _, _, name) in flags.items():
            if name:
                self.num.setdefault(name, flag)
        self.wnum = {name: n for n, (_, _, _, _, name) in works.items() if name}
        self.items_en, self.items_fr = items_en, items_fr
        self.item_id = {}  # un nom repris d'une génération précédente (Clé Hangar, Passe Bateau) : le numéro de la 6e
        for n, name in enumerate(items_en[:MAX_ITEM + 1]):
            self.item_id[name] = n
        self.place_fr = {}
        for en, fr in zip(en_places, fr_places):
            if en and en not in self.place_fr:
                self.place_fr[en] = fr

    def f(self, *names):
        for n in names:
            if n not in self.num:
                raise SystemExit(f'Drapeau inconnu : {n}')
        return '+'.join(f'E:{self.num[n]}' for n in names)

    def a(self, *names):
        return '|'.join(self.f(n) for n in names)

    def w(self, name, value):
        if name not in self.wnum:
            raise SystemExit(f'Variable inconnue : {name}')
        return f'W:{self.wnum[name]}:{value}'

    def item(self, name):
        name = ITEM_FIXES.get(name, name)
        if name not in self.item_id:
            UNKNOWN.add(f'objet {name}')  # le générateur s'arrête à la fin, avec la liste complète
            return 0
        return self.item_id[name]

    def k(self, *names):
        return '|'.join(f'K:{self.item(n)}' for n in names)

    def place(self, loc):
        """Lieu français d'un lieu de FlagsEditorEX (« Lumiose City (South Boulevard) » → « Illumis »)."""
        if loc in HIDEOUTS:
            return 'Planque Magma' if self.version == 'OR' else 'Planque Aqua'
        base = re.sub(r'\s*\(.*\)$', '', loc)
        base = PLACE_EN.get(base, base)
        if base not in self.place_fr:
            UNKNOWN.add(f'lieu {loc}')
            return base
        return self.place_fr[base].replace('’', "'")  # PKHeX : apostrophe typographique


def floor(token):
    m = re.fullmatch(r'B(\d)F', token)
    if m:
        return f'sous-sol {m.group(1)}'
    m = re.fullmatch(r'(\d+)F', token)
    if m:
        n = int(m.group(1)) - 1
        return 'rez-de-chaussée' if n == 0 else '1er étage' if n == 1 else f'{n}e étage'
    return None


def subplace(sub):
    """Précision française d'un sous-lieu de FlagsEditorEX (« Gym 2F » → « Arène », « B1F » → « sous-sol 1 »)."""
    sub = re.sub(r'^\(.*?\)\s*', '', sub).strip()
    sub = re.sub(r'\s*\(.*\)$', '', sub)
    if not sub:
        return None
    if f := floor(sub):
        return f
    words = sub.split(' ')
    if floor(words[0]):  # « 1F Inner Cave », « B1F Back Room »
        rest = ' '.join(words[1:])
        return f'{floor(words[0])}, {SUBPLACES[rest]}' if rest in SUBPLACES else floor(words[0])
    if words[-1] and floor(words[-1]):
        head = ' '.join(words[:-1])
        if head == 'Gym':
            return 'Arène'
        return SUBPLACES.get(head, None) and f'{SUBPLACES[head]}, {floor(words[-1])}'
    if re.fullmatch(r'[\w\s]+ \d', sub):
        return SUBPLACES.get(sub.rsplit(' ', 1)[0])
    if re.fullmatch(r'\d', sub):
        return None
    if sub in SUBPLACES:
        return SUBPLACES[sub]
    if sub.startswith('Trick House'):
        return 'Maison Piège'
    UNKNOWN.add(f'sous-lieu {sub}')
    return None


def item_parts(c, text):
    """« Luxury Ball (x5) » → [(numéro, 5)] ; « A / B » : un choix ; « A and B » : les deux."""
    text = re.sub(r'^the ', '', text.strip())
    parts = []
    for piece in re.split(r', | and | / ', text):
        m = re.fullmatch(r'(.+?)(?: \(x(\d+)\))?', piece.strip())
        name, count = m.group(1), int(m.group(2) or 1)
        tm = re.fullmatch(r'(TM|HM)(\d+) \(.*\)', name)
        parts.append((c.item(f'{tm.group(1)}{int(tm.group(2)):02d}' if tm else name), count))
    return parts, ' / ' in text


def tm_number(c, item):
    name = c.items_en[item]
    m = re.fullmatch(r'(TM|HM)(\d+)', name)
    return int(m.group(2)) + (100 if m.group(1) == 'HM' else 0) if m else 0


# ---------------------------------------------------------------------------------------------------------------------
# Étapes choisies à la main (check-lists de l'utilisateur)

def badge(n, name, leader, city):
    return (city, 'Arène', f'Badge {name} ({leader})', f'B:{n}')


def xy_story(c, version):
    legend = 'Xerneas' if version == 'X' else 'Yveltal'
    return [
        ('Quarellis', None, 'Choisir son premier Pokémon', 'D:650|D:653|D:656'),
        ('Route 3', None, "Recevoir l'ABC Aventure", c.w('WK_SCENE_R03', 2)),
        ('Neuvartault', None, 'Recevoir les Rollers', c.k('Roller Skates')),
        badge(0, 'Coléoptère', 'Violette', 'Neuvartault'),
        ('Neuvartault', None, 'Recevoir le Multi Exp (Alexia)', c.k('Exp. Share')),
        ('Route 4', None, 'Recevoir la CT27 Retour (Dexio)', c.w('WK_SCENE_C02G0101', 1)),
        ('Illumis (labo)', None, 'Combattre le Prof. Platane et choisir un starter de Kanto', c.w('WK_SYS_SECOND_POKETYPE', 3)),
        ('Route 5', None, 'Battre Tierno', c.w('WK_SCENE_R05', 2)),
        ('Palais Chaydeuvre', None, 'Retrouver Couafarel et recevoir la Poké Flûte', c.k('Poké Flute')),
        ('Route 7', None, 'Réveiller Ronflex', c.f('FV_R07_KABI_EV')),
        ('Route 7', None, 'Combat multi contre Tierno et Trovato', c.w('WK_SCENE_R07', 14)),
        ('Grotte Étincelante', None, 'Chasser la Team Flare de la Grotte Étincelante', c.w('WK_SCENE_D03R0102', 4)),
        ('Route 8', None, 'Recevoir la Canne', c.k('Old Rod')),
        ('Relifac-le-Haut', None, 'Recevoir la Bicyclette', 'K:450|K:713'),
        badge(1, 'Mur', 'Lino', 'Relifac-le-Haut'),
        ('Relifac-le-Haut', None, 'Recevoir la CS04 Force', c.k('HM04')),
        ("Cromlac'h", None, 'Battre Cornélia', c.w('WK_SCENE_T04', 4)),
        badge(2, 'Lutte', 'Cornélia', 'Yantreizh'),
        ('Tour Maîtrise', None, 'Recevoir le Méga-Anneau', c.k('Mega Ring')),
        ('Yantreizh', None, 'Recevoir la CS03 Surf', c.k('HM03')),
        ('Port Tempères', None, 'Recevoir la Super Canne', c.k('Good Rod')),
        ('Port Tempères', None, 'Recevoir la CS02 Vol (Prof. Platane)', c.k('HM02')),
        badge(3, 'Végétal', 'Amaro', 'Port Tempères'),
        ('Route 13', None, 'Trouver le Passe Centrale', c.k('Power Plant Pass') + '|' + c.w('WK_SCENE_D12R0103', 3)),
        ('Centrale de Kalos', None, 'Chasser la Team Flare de la Centrale', c.w('WK_SCENE_D12R0103', 3)),
        badge(4, 'Tension', 'Lem', 'Illumis'),
        badge(5, 'Nymphe', 'Valériane', 'Romant-sous-Bois'),
        badge(6, 'Psychisme', 'Astera', 'Flusselles'),
        ('Labos Lysandre', None, 'Recevoir la Clé Ascenseur', c.k('Elevator Key')),
        ('Repaire Team Flare', None, f'Capturer {legend}', 'D:716' if version == 'X' else 'D:717'),
        badge(7, 'Iceberg', 'Urup', 'Auffrac-les-Congères'),
        ('Route 19', None, 'Recevoir la CS05 Cascade', c.k('HM05')),
        # Conseil 4 : drapeau levé par le script de chaque salle (lu dans la ROM : salle 251 → 0x177…), sans doute remis à
        # zéro pour les revanches : la Ligue déjà gagnée compte aussi
        ('Ligue Pokémon', 'Chambre du Brasier', 'Battre Malva (Conseil 4)', c.a('FE_PLR0302_BIGFOUR2_01', 'SYS_FLAG_GAME_CLEAR')),
        ('Ligue Pokémon', 'Chambre du Barrage', 'Battre Narcisse (Conseil 4)', c.a('FE_PLR0402_BIGFOUR4_01', 'SYS_FLAG_GAME_CLEAR')),
        ('Ligue Pokémon', "Chambre de l'Alliage", 'Battre Thyméo (Conseil 4)', c.a('FE_PLR0502_BIGFOUR1_01', 'SYS_FLAG_GAME_CLEAR')),
        ('Ligue Pokémon', 'Chambre de la Guivre', 'Battre Dracéna (Conseil 4)', c.a('FE_PLR0602_BIGFOUR3_01', 'SYS_FLAG_GAME_CLEAR')),
        ('Ligue Pokémon', None, 'Battre la Maîtresse Dianthéa et entrer au Panthéon', c.f('SYS_FLAG_GAME_CLEAR')),
        ('Gare du TMV (Illumis)', None, 'Recevoir le Pokédex National et le Passe TMV', c.k('TMV Pass')),
        ('Labo Platane', None, 'Recevoir le PokéRadar', c.k('Poké Radar')),
        ('Flusselles (cadran solaire)', None, 'Faire améliorer le Méga-Anneau (Méga-Gemmes cachées)',
         c.f('FE_FLDITEM_MEGASTONE_UNLOCK')),
    ]


def oras_story(c, version):
    omega = version == 'OR'
    team = 'Team Magma' if omega else 'Team Aqua'
    return [
        ('Route 101', None, 'Choisir son premier Pokémon', 'D:252|D:255|D:258'),
        ('Bourg-en-Vol (labo)', None, 'Recevoir le Pokédex', c.f('SYS_FLAG_ZUKAN_GET')),
        ('Bourg-en-Vol (labo)', None, 'Recevoir le Navi-Dex', c.f('SYS_FLAG_NAVI_ZUKAN_SET')),
        ('Clémenti-Ville', None, 'Aider Timmy à capturer un Tarsal (Navi-Fun)', c.f('SYS_FLAG_NAVI_PLAY_SET')),
        ('Bois Clémenti', None, 'Recevoir le Multi Exp', c.k('Exp. Share')),
        ('Mérouville', None, 'Recevoir la CS01 Coupe', c.k('HM01')),
        badge(0, 'Roche', 'Roxanne', 'Mérouville'),
        ('Mérouville', None, 'Recevoir le Navi-Show', c.f('SYS_FLAG_NAVI_TV_SET')),
        ('Myokara', None, 'Recevoir la Canne', c.k('Old Rod')),
        badge(1, 'Poing', 'Bastien', 'Myokara'),
        ('Poivressel', None, 'Recevoir le Passe Concours', c.k('Contest Pass')),
        ('Route 110', None, "Recevoir le Cherch'Objet", c.k('Dowsing Machine')),
        ('Lavandia', None, 'Recevoir un vélo (Cross ou Course)', c.k('Mach Bike', 'Acro Bike')),
        ('Lavandia', None, 'Recevoir la CS06 Éclate-Roc', c.k('HM06')),
        badge(2, 'Dynamo', 'Voltère', 'Lavandia'),
        ('Route 112', None, 'Recevoir la CS04 Force', c.k('HM04')),
        ('Mont Chimnée', None, 'Recevoir les Lunettes Sable', c.k('Go-Goggles')),
        badge(3, 'Chaleur', 'Adriane', 'Vermilava'),
        # Clémenti-Ville est déjà un lieu du début (Timmy) : l'Arène à part, sinon le badge y serait regroupé
        ('Arène de Clémenti-Ville', None, 'Badge Balance (Norman)', 'B:4'),
        ('Arène de Clémenti-Ville', None, 'Recevoir la CS03 Surf', c.k('HM03')),
        ('Île du Sud', None, 'Recevoir ' + ('Latios' if omega else 'Latias') + ' et le Méga-Bracelet',
         c.k('Mega Bracelet')),
        ('Route 119', None, 'Recevoir la CS02 Vol', c.k('HM02')),
        ('Route 120', None, 'Recevoir le Devon Scope', c.k('Devon Scope')),
        badge(5, 'Plume', 'Alizée', 'Cimetronelle'),
        ('Planque Aqua/Magma', None, f'Explorer la planque de la {team}', c.k('Magma Suit' if omega else 'Aqua Suit')),
        badge(6, 'Esprit', 'Lévy et Tatia', 'Algatia'),
        ('Algatia', None, 'Recevoir la CS07 Plongée', c.k('HM07')),
        ('Grotte Origine', None, 'Capturer ' + ('Primo-Groudon' if omega else 'Primo-Kyogre'),
         c.f('SYS_FLAG_CAPTURE_GURAADON' if omega else 'SYS_FLAG_CAPTURE_KAIOOGA')),
        ('Grotte Origine', None, 'Recevoir la Flûte Éon', c.k('Eon Flute')),
        badge(7, 'Pluie', 'Marc', 'Atalanopolis'),
        ('Arène d\'Atalanopolis', None, 'Recevoir la CS05 Cascade', c.k('HM05')),
        ('Ligue Pokémon', None, 'Battre le Maître Pierre Rochard et entrer au Panthéon', c.f('SYS_FLAG_GAME_CLEAR')),
        ('Hoenn', None, "Commencer l'Épisode Delta", c.f('SYS_FLAG_DELTA_SET')),
        ('Pilier Céleste', None, 'Capturer Rayquaza', c.f('SYS_FLAG_CAPTURE_REKKUUZA') + '|D:384'),
        ('Espace', None, 'Capturer Deoxys', c.f('SYS_FLAG_CAPTURE_DEOKISISU')),
        ('Atoll de Combat', None, 'Recevoir le Passe Bateau (Norman)', 'K:736'),
        ('Sanctuaire (Chenal 134)', None, 'Ouvrir les grottes du Trio des Golems', c.f('SYS_FLAG_RELEASE_D104D116D117')),
    ]


# Rencontres : (lieu, précision, numéro national, condition, texte)
def xy_encounters(c, version):
    x = version == 'X'
    return [
        ('Repaire Team Flare', None, 716 if x else 717, 'D:716' if x else 'D:717', None),
        ('Grotte Coda', None, 718, c.f('FE_EV_POKE03_CAPTURE'), None),
        ('Grotte Inconnue', None, 150, c.f('FE_MYUUTUU_CAPTURE'), None),
        ('Kalos', 'errant, selon le starter', 144, 'D:144|D:145|D:146', 'Artikodin, Électhor ou Sulfura'),
        ('Route 7', None, 143, c.f('FE_R07_KABIGON_01'), None),
        ('Illumis (labo)', 'starter de Kanto', 1, c.w('WK_SYS_SECOND_POKETYPE', 3), 'Bulbizarre, Salamèche ou Carapuce'),
        ('Tour Maîtrise', 'Cornélia', 448, 'D:448', None),
        ('Route 12', 'cadeau', 131, 'D:131', None),
        ('Roche-sur-Gliffe', 'fossile', 696, 'D:696|D:698', 'Ptyranidur ou Amagara'),
        ('Roche-sur-Gliffe', 'Vieil Ambre', 142, 'D:142', None),
        ('Roche-sur-Gliffe', 'fossile', 138 if not x else 345,
         'D:138|D:140' if not x else 'D:345|D:347', 'Amonita ou Kabuto' if not x else 'Lilia ou Anorith'),
        ('Roche-sur-Gliffe', 'fossile', 408, 'D:408|D:410', 'Kranidos ou Dinoclier'),
        ('Roche-sur-Gliffe', 'fossile', 564, 'D:564|D:566', 'Carapagos ou Arkéapti'),
        ('Hôtels de Kalos', 'échange (Montagnard)', 129, c.w('WK_SCENE_HOTEL_GUEST_01', 5), 'Magicarpe (contre Léviator)'),
        ('Hôtels de Kalos', 'échange (Soubrette)', 133, c.w('WK_SCENE_HOTEL_GUEST_04', 5), 'Évoli'),
        # Pas les Pokémon reçus par cadeau mystère (Diancie, Hoopa, Volcanion) : retirés (choix de l'utilisateur,
        # 2026-11-13) ; ils restent dans le Pokédex (échange, Banque)
    ]


def oras_encounters(c, version):
    omega = version == 'OR'

    def cap(name):
        return c.f(f'SYS_FLAG_CAPTURE_{name}')

    lati, other = ('RATHIOSU', 'RATHIASU') if omega else ('RATHIASU', 'RATHIOSU')
    u = [
        ('Île du Sud', 'cadeau', 381 if omega else 380, cap(lati), None),
        ('Île du Sud', 'Flûte Éon', 380 if omega else 381, cap(other), None),
        ('Grotte Origine', None, 383 if omega else 382, cap('GURAADON' if omega else 'KAIOOGA'), None),
        ('Pilier Céleste', None, 384, cap('REKKUUZA') + '|D:384', None),
        ('Espace', None, 386, cap('DEOKISISU'), None),
        ('Ruines Désert', None, 377, cap('REZIROKKU'), None),
        ("Grotte de l'Îlot", None, 378, cap('REZIAISU'), None),
        ('Tombeau Antique', None, 379, cap('REZISUTIRU'), None),
        ("Grotte de l'Îlot", None, 486, cap('REZIGIGASU'), None),
        ('Lavandia Sea', None, 250 if omega else 249, cap('HOUOU' if omega else 'RUGIA'), None),
        ('Forêt Impénétrable', 'mirage', 243, cap('RAIKOU'), None),
        ('Forêt Impénétrable', 'mirage', 244, cap('ENTEI'), None),
        ('Forêt Impénétrable', 'mirage', 245, cap('SUIKUN'), None),
        ('Grotte Oubliée', 'mirage', 480, cap('YUKUSII'), None),
        ('Grotte Oubliée', 'mirage', 481, cap('EMURITTO'), None),
        ('Grotte Oubliée', 'mirage', 482, cap('AGUNOMU'), None),
        ('Faille', 'au-dessus de Myokara', 484 if omega else 483, cap('PARUKIA' if omega else 'DHIARUGA'), None),
        ('Faille', 'au-dessus de Myokara', 487, cap('GIRATHINA'), None),
        ('Grotte Zénith', 'mirage', 485, cap('HIIDORAN'), None),
        ("Île de l'Arc", 'mirage', 488, cap('KURESERIA'), None),
        ('Grotte Évanescente', 'mirage', 643 if omega else 644, cap('RESIRAMU' if omega else 'ZEKUROMU'), None),
        ('Gouffre Informe', 'mirage', 646, cap('KYUREMU'), None),
        ('Plaine sans Nom', 'mirage', 638, cap('KOBARUON'), None),
        ('Plaine sans Nom', 'mirage', 639, cap('TERAKION'), None),
        ('Plaine sans Nom', 'mirage', 640, cap('BIRIZION'), None),
        ('Grand Envol', 'nuages noirs', 641 if omega else 642, cap('TORUNEROSU' if omega else 'BORUTOROSU'), None),
        ('Grand Envol', 'nuages noirs', 645, cap('RANDOROSU'), None),
        ('Lavandia Sea', None, 442, c.f('FE_D115R0109_CAP_MIKARUGE_01'), None),
    ]
    for flag in ('FV_D114R0102_BIRIRIDAMA01', 'FV_D114R0102_BIRIRIDAMA02', 'FV_D114R0102_BIRIRIDAMA03',
                 'FV_D114R0102_BIRIRIDAMA04'):
        u.append(('New Lavandia', 'déguisé en objet', 100, c.f(flag), None))
    for flag, where in (('FV_R120A_STOPPER_01', 'pont'), ('FV_R119A_STOPPER_04', 'Route 119'),
                        ('FV_R120A_STOPPER_04', 'escalier'), ('FV_R120A_STOPPER_05', 'hautes herbes'),
                        ('FV_T104_STOPPER_01', 'Vermilava'), ('FV_C107R0601_STOPPER_01', 'Centre Spatial')):
        zone = {'Route 119': 'Route 119', 'Vermilava': 'Vermilava', 'Centre Spatial': 'Algatia'}.get(where, 'Route 120')
        u.append((zone, f'invisible ({where})' if zone == 'Route 120' else 'invisible', 352, c.f(flag), None))
    hideout = ('Planque Magma', 'FV_D120R0106_POKE_01', 'FV_D120R0106_POKE_02') if omega else \
        ('Planque Aqua', 'FV_D109R0106_POKE_01', 'FV_D109R0106_POKE_02')
    for flag in hideout[1:]:
        u.append((hideout[0], 'déguisé en objet', 101, c.f(flag), None))
    u += [
        ('Poivressel', 'premier concours', 25, c.w('WK_SCENE_OKIPIKA', 2), 'Pikachu Cosplayeur'),
        ('Vermilava', 'œuf', 360, 'D:360', None),
        ('Vermilava', 'œuf', 175, 'D:175', None),
        ('Centre Météo', 'cadeau', 351, 'D:351', None),
        ('Bourg-en-Vol', 'starter de Johto', 152, c.w('WK_SCENE_T103', 3), 'Germignon, Héricendre ou Kaiminus'),
        ('Bourg-en-Vol', "starter d'Unys", 495, c.w('WK_SCENE_T103', 6), 'Vipélierre, Gruikui ou Moustillon'),
        ('Bourg-en-Vol', 'starter de Sinnoh', 387, c.w('WK_SCENE_T103', 9), 'Tortipouss, Ouisticram ou Tiplouf'),
        ('Algatia', 'cadeau', 374, 'D:374', None),
        ('Désert', 'fossile', 345, 'D:345|D:347', 'Lilia ou Anorith'),
        # Pas les Pokémon reçus par cadeau mystère (Mew, Celebi, Jirachi… de la check-list) : retirés (choix de
        # l'utilisateur, 2026-11-13) ; ils restent dans le Pokédex
    ]
    return u


# Quêtes annexes : (lieu, précision, texte, condition)
MAISON = [('Solo', 'SINGLE'), ('Duo', 'DOUBLE'), ('Trio', 'TRIPLE'), ('Rotatif', 'ROTATION'), ('Multi', 'MULTI')]


def maison(c, where):
    """Maison de Combat : la Châtelaine battue au 20e combat (le mode Super s'ouvre), puis au 50e en mode Super (statuette)."""
    q = []
    for mode, key in MAISON:
        q.append((where, None, f'Combat {mode} : battre la Châtelaine (20e victoire)', c.f(f'FE_BTLHOUSE_CLEAR_{key}')))
        q.append((where, None, f'Combat Super {mode} : battre la Châtelaine (50e victoire)', c.f(f'FE_BTLHOUSE_MONUMENT_{key}')))
    return q


# Château de Combat (X / Y) : ses nobles lèvent leur drapeau de dresseur (vérifié sur y.sav : 20 battus, levés), mais
# ne sont sur aucune carte ni dans aucun script de zone (le Château les choisit selon le jour et le titre du joueur)
NOBLES = {'Baron', 'Baronne', 'Vicomte', 'Vicomtesse', 'Comte', 'Comtesse', 'Marquis', 'Marquise', 'Duc', 'Duchesse',
          'Grande-Duchesse'}


def chateau(rom):
    """Chaque noble du Château de Combat une fois ; même titre et même nom (Marquise Violette, deux équipes) : l'un ou
    l'autre, faute de savoir si les deux viennent."""
    trainers, _ = rom
    groups = {}
    for n, t in sorted(trainers.items()):
        if t['cls'] in NOBLES and t['team'] and not t['places']:
            groups.setdefault(f"{t['cls']} {t['name']}", []).append(n)
    return [('Château de Combat', None, label, '|'.join(f'E:{TRAINER_FLAGS + n}' for n in ns))
            for label, ns in groups.items()]


def super_training(stages):
    """Les 30 épreuves du Super Training de la cartouche (écran tactile, pas un lieu ; meilleur temps enregistré : épreuve
    réussie)."""
    return [('Super Training', None, name.rstrip('!').rstrip(), f'ST:{n}') for n, name in enumerate(stages[:30])]


CONTESTS = [(7, 'Remporter un premier concours de rang Normal'), (9, 'Remporter un premier concours de rang Super'),
            (11, 'Remporter un premier concours de rang Hyper'), (13, 'Remporter un premier concours de rang Master'),
            (15, 'Remporter le rang Master dans 4 catégories'), (17, 'Remporter le rang Master dans les 5 catégories'),
            (19, 'Battre Lisia'), (21, 'Battre Marc et Lisia')]


def contests(c):
    return [('Salles Concours Live', None, label, c.w('WK_SCENE_CONTEST', v)) for v, label in CONTESTS]


# Méga-Gemmes (check-lists de l'utilisateur) : (lieu, précision, nom anglais, versions) ; comptées par possession (sac ou
# tenue par un Pokémon) : la plupart n'ont pas de drapeau nommé. Les Méga-Gemmes déjà reçues en cadeau avec un drapeau
# (Pharampite, Ptéraïte de X / Y) n'y sont pas. Exclues : Latiasite et Latiosite de X / Y (échange depuis Rubis Oméga /
# Saphir Alpha).
XY_MEGA = [
    ('Illumis', 'Prof. Platane ou Joyau d\'Illumis', 'Venusaurite', None),
    ('Illumis', 'Prof. Platane ou Joyau d\'Illumis', 'Charizardite X', 'X'),
    ('Illumis', 'Prof. Platane ou Joyau d\'Illumis', 'Charizardite Y', 'Y'),
    ('Illumis', 'Prof. Platane ou Joyau d\'Illumis', 'Blastoisinite', None),
    ('Tour Maîtrise', 'avec Lucario', 'Lucarionite', None),
    ('Romant-sous-Bois', None, 'Gengarite', None),
    ('Caverne Gelée', None, 'Abomasite', None),
    ('Illumis', 'Tarsal de Dianthéa', 'Gardevoirite', None),
    ('Batisques', 'rival', 'Absolite', None),
    ('Grotte Inconnue', None, 'Mewtwonite X', 'X'),
    ('Grotte Inconnue', None, 'Mewtwonite Y', 'Y'),
    ('Grotte Miroitante', '20h-21h', 'Alakazite', None),
    ('Grotte Étincelante', '20h-21h', 'Kangaskhanite', None),
    ('Forêt de Neuvartault', '20h-21h', 'Pinsirite', 'X'),
    ('Forêt de Neuvartault', '20h-21h', 'Heracronite', 'Y'),
    ('Mozheim', '20h-21h', 'Gyaradosite', None),
    ('Caverne Gelée', '20h-21h', 'Scizorite', None),
    ('Route 16', '20h-21h', 'Manectite', 'X'),
    ('Route 16', '20h-21h', 'Houndoominite', 'Y'),
    ('Relifac-le-Haut', 'Arène, 20h-21h', 'Tyranitarite', 'X'),
    ('Relifac-le-Haut', 'Arène, 20h-21h', 'Aggronite', 'Y'),
    ('Fort-Vanitas', '20h-21h', 'Mawilite', None),
    ('Romant-sous-Bois', '20h-21h', 'Medichamite', None),
    ('Route 22', 'Chambre Néant', 'Banettite', None),
    ('Route Victoire', '20h-21h', 'Garchompite', None),
    ('Kalos', 'distribution', 'Blazikenite', None),
]
ORAS_MEGA = [
    ('Nénucrique', None, 'Altarianite', None),
    ('Atoll de Combat', None, 'Cameruptite', None),
    ('Lavandia Sea', None, 'Beedrillite', None),
    ('Site Météore', 'après Delta', 'Salamencite', None),
    ('Hoenn', 'distribution (avec Diancie)', 'Diancite', None),
    ('Grotte Tréfonds', None, 'Slowbronite', None),
    ('Autéquia', 'après Delta', 'Galladite', None),
    ('Route 114', 'Pierre Rochard ou 1 500 P', 'Sceptilite', None),
    ('Route 114', 'Pierre Rochard ou 1 500 P', 'Swampertite', None),
    ('Route 120', 'Pierre Rochard (Poussifeu)', 'Blazikenite', None),
    ('Île du Sud', None, 'Latiasite', None),
    ('Île du Sud', None, 'Latiosite', None),
    ('Lavandia', None, 'Lopunnite', None),
    ('Ligue Pokémon', 'Pierre Rochard, 2e victoire', 'Metagrossite', None),
    ('Atoll de Combat', 'Beladonis', 'Audinite', None),
    ('Grotte Tréfonds', 'marée basse', 'Glalitite', None),
    ('Mérouville', 'Pierre Insolite', 'Pidgeotite', None),
    ('Atoll de Combat', None, 'Sharpedonite', None),
    ('Grotte Granite', 'Vélo Course', 'Steelixite', None),
    ('Atalanopolis', None, 'Sablenite', None),
    ('Poivressel', None, 'Alakazite', None),
    ('Route 110', 'Piste Cyclable', 'Manectite', None),
    ('Vergazon', None, 'Mawilite', None),
    ('Tunnel Mérazon', None, 'Aggronite', None),
    ('Vermilava', None, 'Houndoominite', None),
    ('Grotte Zénith', None, 'Charizardite Y', None),
    ('Mont Mémoria', None, 'Medichamite', None),
    ('Mont Mémoria', None, 'Banettite', None),
    ('Route 123', None, 'Gyaradosite', None),
    ('Chenal 127', None, 'Heracronite', None),
    ('Chenal 124', None, 'Pinsirite', None),
    ('Mérouville', 'Delta', 'Aerodactylite', None),
    ('Ligue Pokémon', None, 'Mewtwonite Y', None),
    ('Pacifiville', None, 'Kangaskhanite', None),
    ('New Lavandia', None, 'Ampharosite', None),
    ('Atoll de Combat', None, 'Gengarite', None),
]


# ---------------------------------------------------------------------------------------------------------------------

def checklist_machines(path):
    """CT / CS de la check-list : numéro (CT01 = 1… CT100 = 100, CS01 = 101…) → lieu."""
    out = {}
    with open(path, encoding='utf-8-sig') as fh:
        for row in csv.DictReader(fh, delimiter=';'):
            if row['Catégorie'] != 'CT/CS':
                continue
            m = re.fullmatch(r'(CT|CS)(\d+)', row['ID'])
            out[int(m.group(2)) + (100 if m.group(1) == 'CS' else 0)] = row['Lieu']
    return out


def clean_place(text):
    """Lieu d'une check-list : « Boutique de Yantreizh (50 000 P) » → (« Yantreizh », « Boutique, 50 000 P »)."""
    m = re.fullmatch(r'(.+?)(?: \((.+)\))?', text.strip())
    place, extra = m.group(1), m.group(2)
    shop = re.fullmatch(r"Boutique (?:de |d'|du )(.+)", place)
    if shop:
        place = shop.group(1)
        extra = 'Boutique' + (f', {extra}' if extra else '')
    if place.startswith('Maison de Combat'):
        place, extra = 'Batisques', 'Maison de Combat' + (f', {extra}' if extra else '')
    place = place.replace(' Sud', '')
    return place, extra


# ---------------------------------------------------------------------------------------------------------------------
# Dresseurs (ROM)

# Dans l'histoire (badges, Panthéon), pas dans les dresseurs ; leurs combats sont lancés par un script
LEADERS = {'Champion', 'Championne', 'Champions', 'Conseil 4', 'Maître'}
# Rubis Oméga : on combat la Team Magma, Saphir Alpha la Team Aqua (la ROM a les deux, l'autre équipe reste cachée)
OTHER_TEAM = {'OR': 'Team Aqua', 'AS': 'Team Magma'}


def trainer_rows(c, version, rom, places_fr, checklist):
    """Dresseurs de la version : (lieu, précision, condition, équipe, numéro), un par drapeau."""
    trainers, gyms = rom
    rematch = {flag - TRAINER_FLAGS for flag, (kind, _, _, desc, _) in c.flags.items()
               if kind == 'TRAINER BATTLE' and 'Rematch' in desc}

    def wanted(n):
        t = trainers[n]
        return (t['team'] and t['cls'] not in LEADERS and n not in rematch
                and not (OTHER_TEAM.get(version) and OTHER_TEAM[version] in t['cls']))

    def zone_of(zone, place):
        name = places_fr[place].replace('’', "'")  # PKHeX : apostrophe typographique
        if zone not in gyms:
            return name, None
        for gym in (f'Arène de {name}', f"Arène d'{name}"):
            if gym in checklist:  # rangé avec son badge (Arène de Clémenti-Ville : 5e badge)
                return gym, None
        return name, 'Arène'

    out = []
    for n, t in sorted(trainers.items()):
        if not t['places'] or not wanted(n):
            continue
        zone, place, _ = min(t['places'], key=lambda p: (p[2] != 'carte', p[0]))  # le personnage, sinon le script
        out.append(zone_of(zone, place) + (n,))
    # Dresseurs que FlagsEditorEX situe sans que la ROM les montre (Dresseurs Aériens, dresseurs d'Illumis…)
    found = {n for *_, n in out}
    for flag, (kind, loc, sub, desc, name) in sorted(c.flags.items()):
        n = flag - TRAINER_FLAGS
        if kind == 'TRAINER BATTLE' and loc and n in trainers and n not in found and wanted(n):
            out.append((c.place(loc), subplace(sub), n))
    return [(zone, det, f'E:{TRAINER_FLAGS + n}', ','.join(f'{s}:{lv}' for s, lv in trainers[n]['team']), n)
            for zone, det, n in out]


def build(c, version, checklist, cur):
    rows = []

    def order(zone, strict):
        rank = anchored_rank(zone, checklist, ANCHORS)
        if rank is None and strict:
            MISSING_PLACES.add(f'{zone} ({version})')
        return 10_000 if rank is None else rank

    def add(section, zone, det, done, label=None, item=0, tm=0, species=0, count=0, sort=0, team=None):
        rows.append(dict(section=section, zone=zone, detail=det, done=done, label=label, item=item, tm=tm,
                         species=species, count=count, team=team,
                         sort=(order(zone, section in ('Trainers', 'Items')), zone, sort)))

    S, D, I, U, Q = 'Story', 'Trainers', 'Items', 'Encounters', 'Quests'
    for i, (zone, det, label, cond) in enumerate(cur['story']):
        add(S, zone, det, cond, label, sort=i)
    for zone, det, cond, team, n in cur['trainers']:
        add(D, zone, det, cond, team=team, sort=n)

    # ---------- Objets : au sol, cachés, cadeaux décrits ----------
    seen_items = set()  # CT / CS et Méga-Gemmes déjà comptées par un drapeau
    for flag, (kind, loc, sub, desc, name) in sorted(c.flags.items()):
        if kind not in ('FIELD ITEM', 'HIDDEN ITEM', 'ITEM GIFT') or not desc or flag < LOCAL_FLAGS:
            continue
        if loc in HIDEOUTS and HIDEOUTS[loc] not in (None, version):
            continue  # planque de l'autre version
        if '(recurring)' in desc or desc.startswith('[Daily]') or 'Diploma' in desc:
            continue  # objets cachés qui reviennent, cadeaux quotidiens, diplômes du Pokédex
        if kind == 'FIELD ITEM':
            text, extra, k = desc.replace(' item ball disappeared', ''), None, 0
        elif kind == 'HIDDEN ITEM':
            text, extra, k = desc.removeprefix('Found '), 'caché', 1
        else:
            m = re.fullmatch(r'Received (.+?) from .*', desc)
            if not m:
                continue  # échanges d'objets (« Exchanged… »)
            text, extra, k = m.group(1), 'cadeau', 2
        parts, choice = item_parts(c, text)
        det = ', '.join(p for p in (subplace(sub), extra) if p) or None
        zone = c.place(loc)
        if len(parts) == 1:
            value, count = parts[0]
            tm = tm_number(c, value)
            seen_items.add(value)
            add(I, zone, det, f'E:{flag}', item=0 if tm else value, tm=tm, count=count if count > 1 else 0,
                sort=k * 100_000 + flag)
        else:
            sep = ' ou ' if choice else ' et '
            label = sep.join(c.items_fr[v] + (f' ×{n}' if n > 1 else '') for v, n in parts)
            add(I, zone, det, f'E:{flag}', label, sort=k * 100_000 + flag)

    # CT et CS sans drapeau (cadeaux de personnages, boutiques, Maison de Combat) : possédées (elles ne se jettent pas)
    for number, where in sorted(cur['machines'].items()):
        item = c.item(f'TM{number:02d}' if number <= 100 else f'HM{number - 100:02d}')
        if item in seen_items:
            continue
        place, extra = clean_place(where)
        add(I, place, extra, f'K:{item}', tm=number, sort=300_000 + number)

    # Méga-Gemmes : possédées (sac ou tenue par un Pokémon)
    for i, (place, extra, name, only) in enumerate(cur['mega']):
        if only not in (None, version):
            continue
        item = c.item(name)
        if item in seen_items:
            continue
        add(I, place, ', '.join(p for p in (extra, 'Méga-Gemme') if p), f'H:{item}', item=item, sort=400_000 + i)

    # ---------- Rencontres et quêtes ----------
    for i, (zone, det, species, cond, label) in enumerate(cur['encounters']):
        add(U, zone, det, cond, label, species=species, sort=i)
    for i, (zone, det, label, cond) in enumerate(cur['quests']):
        add(Q, zone, det, cond, label, sort=i)

    section_order = {s: n for n, s in enumerate((S, D, I, U, Q))}
    rows.sort(key=lambda r: (section_order[r['section']],) + (r['sort'] if r['section'] in (D, I) else (r['sort'][2],)))
    return rows


MISSING_PLACES = set()


def cs(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"' if s is not None else 'null'


def emit(games, out):
    lines = ['// <auto-generated>',
             "// Généré par outils/completion_gen6.py d'après les listes de drapeaux de FlagsEditorEX, les check-lists de",
             "// l'utilisateur et les ROM (dresseurs) : ne pas modifier à la main, changer le script et le relancer.",
             '// </auto-generated>',
             '',
             'namespace MonsutaPotaru.Services;',
             '',
             'public static class Gen6CompletionData',
             '{',
             '']
    for name, doc, rows in games:
        lines.append(f'    /// <summary>Étapes de {doc} (le Pokédex national est ajouté à part : les 721).</summary>')
        lines.append(f'    public static readonly CompletionRow[] {name} =')
        lines.append('    [')
        for r in rows:
            a_ = [f'CompletionSection.{r["section"]}', cs(r['zone']), cs(r['detail']), cs(r['done'])]
            n = [f'Label: {cs(r["label"])}'] if r['label'] else []
            if r['team']:
                n.append(f'Team: {cs(r["team"])}')
            for key, field in (('item', 'Item'), ('tm', 'Tm'), ('count', 'Count'), ('species', 'Species')):
                if r[key]:
                    n.append(f'{field}: {r[key]}')
            lines.append(f'        new({", ".join(a_ + n)}),')
        lines.append('    ];')
        lines.append('')
    lines[-1] = '}'
    with open(out, 'w', encoding='utf-8', newline='\n') as fh:
        fh.write('﻿' + '\n'.join(lines) + '\n')


def lines_of(path):
    return [line.rstrip('\n').lstrip('﻿') for line in open(path, encoding='utf-8')]


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    root, rom_xy, rom_oras, out = sys.argv[1:5]
    roms = {'xy': read_trainers(rom_xy, 'xy'), 'oras': read_trainers(rom_oras, 'oras')}
    here = os.path.dirname(os.path.abspath(__file__))
    order = json.load(open(os.path.join(here, 'completion_gen6_ordre.json'), encoding='utf-8'))
    items_en = lines_of(os.path.join(root, 'text_Items_en.txt'))
    items_fr = lines_of(os.path.join(root, 'text_Items_fr.txt'))
    places_en = lines_of(os.path.join(root, 'text_xy_00000_en.txt'))
    places_fr = lines_of(os.path.join(root, 'text_xy_00000_fr.txt'))
    stages = lines_of(os.path.join(root, 'text_SuperTraining_fr.txt'))
    games = []
    for flags_file, csv_file, key, versions in (
            ('flags_gen6xy_en.txt', 'suivi_completion_pokemon_x_y.csv', 'xy', (('X', 'X', 'Pokémon X'), ('Y', 'Y', 'Pokémon Y'))),
            ('flags_gen6oras_en.txt', 'suivi_completion_pokemon_rubis_omega_saphir_alpha.csv', 'oras',
             (('OmegaRuby', 'OR', 'Pokémon Rubis Oméga'), ('AlphaSapphire', 'AS', 'Pokémon Saphir Alpha')))):
        flags, works = load_flags(os.path.join(root, flags_file))
        machines = checklist_machines(os.path.join(root, csv_file))
        for name, version, doc in versions:
            c = Ctx(flags, works, items_en, items_fr, places_en, places_fr, version)
            trainers = trainer_rows(c, version, roms[key], places_fr, order[key])
            if key == 'xy':
                cur = dict(story=xy_story(c, version), encounters=xy_encounters(c, version), mega=XY_MEGA,
                           quests=maison(c, 'Maison de Combat') + chateau(roms['xy']) + super_training(stages),
                           machines=machines,
                           trainers=trainers)
            else:
                cur = dict(story=oras_story(c, version), encounters=oras_encounters(c, version), mega=ORAS_MEGA,
                           quests=maison(c, 'Maison de Combat') + super_training(stages) + contests(c),
                           machines=machines, trainers=trainers)
            games.append((name, doc, build(c, version, order[key], cur)))
    if UNKNOWN:
        raise SystemExit('À traduire (ITEM_FIXES, PLACE_EN, SUBPLACES) : ' + ', '.join(sorted(UNKNOWN)))
    if MISSING_PLACES:
        raise SystemExit('Lieux absents de la check-list (leur donner un voisin dans ANCHORS) : ' + ', '.join(sorted(MISSING_PLACES)))
    emit(games, out)
    for name, _, rows in games:
        counts = {}
        for r in rows:
            counts[r['section']] = counts.get(r['section'], 0) + 1
        print(name, counts)
