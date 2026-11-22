"""
Génère MonsutaPotaru/Services/Completion/Gen5CompletionData.cs : Noir, Blanc, Noir 2 et Blanc 2.

    python outils/completion_gen5.py <dossier> <rom Noire> <rom Blanche 2> MonsutaPotaru/Services/Completion/Gen5CompletionData.cs

<dossier> contient :
- pokebw2/ : clone de https://github.com/fuddlesworth/pokebw2 (git clone --depth 1 ; generated, res/field,
  res/trainers/data et include/asm suffisent) ;
- flags_gen5bw_en.txt et flags_gen5b2w2_en.txt : listes de FlagsEditorEX
  (https://github.com/fattard/FlagsEditorEXPlugin, FlagsEditorEXPlugin/Resources/flagslist ou flagslist/) ;
- text_bw2_00000_fr.txt et text_bw2_00000_en.txt : lieux de la 5e génération de PKHeX
  (PKHeX.Core/Resources/text/locations/gen5) ; text_Items_fr.txt : noms des objets de PKHeX (Resources/text/items).
<rom Blanche 2> : la cartouche française de Pokémon Version Blanche 2 de l'utilisateur : noms français des médailles
(textes système, fichier 83) et des parures du Music-Hall (fichier 93, les mêmes que dans Noire / Blanche).
<rom Noire> : la cartouche Pokémon Version Noire de l'utilisateur (Noir / Blanc n'a pas de désassemblage ; les objets et
dresseurs propres à Blanc, la Forêt Blanche, viennent de FlagsEditorEX). À côté de ce script :
completion_gen5_ordre.json, les lieux des étapes « Histoire » des check-lists de l'utilisateur, dans leur ordre.

Les étapes de l'histoire, des rencontres et des quêtes sont choisies ici d'après ces check-lists, avec les drapeaux nommés
de FlagsEditorEX (noms internes du jeu : SYS_FLAG_BIGFOUR_GHOSTWIN…) ; dresseurs, objets et cadeaux sont lus dans pokebw2
et la ROM.
"""
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from completion_gen5_extract import extract_b2w2, extract_bw  # noqa: E402
from completion_gen3 import anchored_rank  # noqa: E402
from completion_gen5_rom import Rom, texts  # noqa: E402

DAILY_FLAGS = range(0xAA0, 0xB04)  # remis à zéro chaque jour (« TMFLG ») ; en dessous de 0x60 : propres à la carte
LOCAL_FLAGS = 0x60

# Lieux absents des étapes d'histoire des check-lists : rangés avec un lieu voisin (en chaîne si le voisin manque aussi) ;
# une route ou un chenal se range d'office près de son numéro.
ANCHORS = {
    'Porte Volucité': 'Volucité', 'Porte Méanville': 'Méanville', 'Porte Janusia': 'Janusia', 'Porte Maillard': 'Maillard',
    'Porte Noire': 'Ville Noire / Forêt Blanche', 'Porte Blanche': 'Ville Noire / Forêt Blanche',
    'Ville Noire': 'Ville Noire / Forêt Blanche', 'Forêt Blanche': 'Ville Noire / Forêt Blanche',
    'Porte de Pont': 'Pont Sagiciel', 'Porte de Route': 'Route 4', 'Engrenage du Métro': 'Méanville',
    'Métro de Combat': 'Méanville', 'Music-Hall': 'Méanville', "L'Étoile d'Unys": 'Volucité', 'Centre Commercial': 'Route 9',
    'Route 9': 'Janusia', 'Route 7': 'Tour des Cieux', 'Route 11': 'Route Victoire', 'Route 12': 'Route 13',
    'Route 14': 'Route 13', 'Route 16': 'Route 5', 'Route 17': 'Route 18', 'Route 6': 'Parsemille', 'Route 1': 'Renouet',
    'Désert Délassant': 'Château Enfoui', 'Antre de Dracolosse': 'Route 9', 'Grotte Parsemille': 'Route 6',
    'Vaguelone': 'Route 13', 'Baie Vaguelone': 'Vaguelone', 'Ruines des Abysses': 'Baie Vaguelone',
    'Grotte Cyclopéenne': 'Route 13', 'Autel Abondance': 'Route 14', 'Bois des Illusions': 'Route 16',
    "Pont de l'Inconnu": 'Route 15 / Poké Fret', 'Route 15': 'Route 15 / Poké Fret', 'Poké Fret': 'Route 15 / Poké Fret',
    'Île Liberté': 'Volucité', 'Entrelasque': 'Route 13', 'Pont du Hameau': 'Pont Ferroviaire',
    "Antre d'Entraînement": 'Route 9', 'Espace Guide': 'Volucité', 'Labo P2': 'Route 17', 'Palais de N': 'Ligue Pokémon', 'Château de N': 'Ligue Pokémon',
    'Tourbière Flocombe': 'Flocombe', 'Rotombourg': 'Méanville', 'Pont Yoneuve': 'Port Yoneuve', 'Pont Sagiciel': 'Volucité',
    'Hangar Frigorifique': 'Port Yoneuve', 'Tour Dragospire': 'Flocombe',
    # Noir 2 / Blanc 2
    'Route 19': 'Pavonnay', 'Ranch': "Ranch d'Amaillide", 'Z.I. d\'Ondes-sur-Mer': 'Ondes-sur-Mer (port)',
    'Ondes-sur-Mer': 'Ondes-sur-Mer (port)', 'Égouts de Volucité': 'Égouts de Volucité', 'Aquatube': 'Route 4',
    'Galerie Concorde': 'Route 4', 'Réserve Naturelle': 'Parsemille', 'Arpentières': 'Mont Renenvers',
    'Grotte des Esprits': 'Route 20', 'Caverne des Esprits': 'Route 20', 'Papeloa': 'Arène de Papeloa', 'Chenal 21': 'Papeloa',
    'Ruines Enfouies': 'Route Bardane / Ruines Enfouies', 'Route Bardane': 'Route Bardane / Ruines Enfouies',
    'Gratte-Ciel Noir': 'Ville Noire / Forêt Blanche', 'Arbre Creux Blanc': 'Ville Noire / Forêt Blanche',
    'Maison Étrange': 'Route 16', "Manoir de l'Étrange": 'Route 16', 'Arabelle': 'Ogoesse', 'Renouet': 'Ogoesse',
    'Maillard': "Forêt d'Empoigne", 'Château Enfoui': 'Route 4', 'Chemin Enfoui': 'Égouts de Volucité', 'Grotte Littorale': 'Grotte Littorale', 'Opelucid': 'Janusia',
}

# Badges : bit du badge dans la sauvegarde, nom, champion, ville (ordre du jeu)
BW_BADGES = [('Triple', 'Rachid, Armando ou Noa', 'Ogoesse'), ('Basique', 'Aloé', 'Maillard'), ('Élytre', 'Artie', 'Volucité'),
             ('Volt', 'Inezia', 'Méanville'), ('Sismique', 'Bardane', 'Port Yoneuve'), ('Jet', 'Carolina', 'Parsemille'),
             ('Stalactite', 'Zhu', 'Flocombe'), ('Mythe', 'Watson ou Iris', 'Janusia')]
B2W2_BADGES = [('Basique', 'Tcheren', 'Pavonnay'), ('Toxique', 'Strykna', 'Ondes-sur-Mer'), ('Élytre', 'Artie', 'Volucité'),
               ('Volt', 'Inezia', 'Méanville'), ('Sismique', 'Bardane', 'Port Yoneuve'), ('Jet', 'Carolina', 'Parsemille'),
               ('Mythe', 'Watson', 'Janusia'), ('Vague', 'Amana', 'Papeloa')]


def badge(badges, n):
    name, leader, city = badges[n]
    return (city, 'Arène', f'Badge {name} ({leader})', f'B:{n}')


class Conds:
    """Conditions écrites avec les noms des drapeaux de FlagsEditorEX (noms internes du jeu) ; K:objet possédé (sac),
    B:n badge, D:n Pokémon capturé, N Pokédex National."""

    def __init__(self, fl, k):
        self.num = {}
        for flag, (_, _, _, _, name) in fl.items():
            if name:
                self.num.setdefault(name, flag)
        self.k = k

    def f(self, *names):
        for n in names:
            if n not in self.num:
                raise SystemExit(f'Drapeau inconnu : {n}')
        return '+'.join(f'E:{self.num[n]}' for n in names)

    def a(self, *names):
        return '|'.join(self.f(n) for n in names)

    def item(self, *items):
        return '|'.join(f'K:{self.k[i]}' for i in items)


# ---------------------------------------------------------------------------------------------------------------------
# Étapes choisies à la main (check-lists de l'utilisateur)

def bw_story(c, version):
    dragon = 'Galet Blanc' if version == 'B' else 'Galet Noir'
    s = [('Renouet', None, 'Choisir son premier Pokémon', c.f('SYS_FLAG_FIRST_POKE_GET')),
         ('Renouet', None, 'Recevoir le Pokédex', c.f('SYS_FLAG_ZUKAN_GET')),
         ('Vestiges du Rêve', None, 'Recevoir le C-Gear', c.f('FE_CGEAR_GET')),
         badge(BW_BADGES, 0),
         ('Ogoesse', None, 'Recevoir la CS01 Coupe', c.item('ITEM_HM01')),
         badge(BW_BADGES, 1), badge(BW_BADGES, 2),
         ('Méanville', None, 'Recevoir la Bicyclette', c.item('ITEM_BICYCLE')),
         ('Méanville', None, 'Recevoir la CS04 Force', c.item('ITEM_HM04')),
         ('Méanville', 'Music-Hall', 'Recevoir la Boîte Parure', c.item('ITEM_PROP_CASE')),
         badge(BW_BADGES, 3),
         ('Hangar Frigorifique', None, 'Chasser la Team Plasma du Hangar Frigorifique',
          c.f('FE_D05R0201_PLASMA_01', 'FE_D05R0201_PLASMA_02', 'FE_D05R0201_PLASMA_03', 'FE_D05R0201_PLASMA_04')),
         badge(BW_BADGES, 4),
         ('Port Yoneuve', None, 'Recevoir la CS02 Vol (Bianca)', c.item('ITEM_HM02')),
         badge(BW_BADGES, 5),
         ('Mont Foré', None, 'Recevoir la CS03 Surf', c.item('ITEM_HM03')),
         badge(BW_BADGES, 6),
         ('Maillard', None, f"Recevoir le {dragon} d'Aloé",
          c.item('ITEM_LIGHT_STONE' if version == 'B' else 'ITEM_DARK_STONE') + '|' + c.f('FE_PACKAGE_POKE_GET')),
         badge(BW_BADGES, 7)]
    for who, flag in (('Anis', 'GHOST'), ('Pieris', 'EVIL'), ('Kunz', 'FIGHT'), ('Percila', 'ESP')):
        s.append(('Ligue Pokémon', None, f'Battre {who} (Conseil 4)', c.a(f'SYS_FLAG_BIGFOUR_{flag}WIN', 'SYS_FLAG_GAME_CLEAR')))
    dragon_species = 'Reshiram' if version == 'B' else 'Zekrom'
    s += [('Château de N', None, f'Capturer {dragon_species}', c.f('FE_PACKAGE_POKE_GET')),
          ('Château de N', None, 'Battre N et Ghetis', c.f('SYS_FLAG_GAME_CLEAR')),
          ('Renouet', None, 'Recevoir le Pokédex National', 'N'),
          ('Renouet', None, 'Recevoir la Méga Canne', c.item('ITEM_SUPER_ROD')),
          ('Route 18', None, 'Recevoir la CS05 Cascade', c.item('ITEM_HM05')),
          ('Vaguelone', None, 'Recevoir la CS06 Plongée', c.item('ITEM_HM06')),
          ('Ligue Pokémon', None, 'Battre le Maître Goyah', c.f('SYS_FLAG_CHAMPION_WIN'))]
    return s


def b2w2_story(c, version):
    s = [('Pavonnay', None, 'Choisir son premier Pokémon', c.f('SYS_FLAG_FIRST_POKE_GET')),
         ('Pavonnay', None, 'Recevoir le Pokédex', c.f('SYS_FLAG_ZUKAN_GET')),
         ('Amaillide', None, 'Recevoir la Boîte Médailles', c.item('ITEM_MEDAL_BOX')),
         badge(B2W2_BADGES, 0),
         ('Pavonnay', None, 'Recevoir le C-Gear', c.f('FE_CGEAR_GET')),
         badge(B2W2_BADGES, 1),
         ('Ondes-sur-Mer (port)', None, 'Recevoir la CS01 Coupe', c.item('ITEM_HM01')),
         ('Volucité (Quai Prime)', None, 'Recevoir la Bicyclette', c.item('ITEM_BICYCLE')),
         ('Égouts de Volucité', None, 'Recevoir la CS04 Force', c.item('ITEM_HM04')),
         badge(B2W2_BADGES, 2),
         ('Volucité', None, "Recevoir le Cherch'Objet", c.item('ITEM_DOWSING_MCHN')),
         badge(B2W2_BADGES, 3),
         ('Route 5', None, 'Recevoir la CS02 Vol (Bianca)', c.item('ITEM_HM02')),
         badge(B2W2_BADGES, 4),
         ('Route 6 (Bureau d\'Étude des Saisons)', None, 'Recevoir la CS03 Surf', c.f('SYS_FLAG_NAMINORI_GET')),
         badge(B2W2_BADGES, 5), badge(B2W2_BADGES, 6), badge(B2W2_BADGES, 7),
         ('Route Victoire', None, 'Recevoir la CS05 Cascade (N)', c.item('ITEM_HM05'))]
    for who, flag in (('Anis', 'GHOST'), ('Pieris', 'EVIL'), ('Percila', 'ESP'), ('Kunz', 'FIGHT')):
        s.append(('Ligue Pokémon', None, f'Battre {who} (Conseil 4)', c.a(f'SYS_FLAG_BIGFOUR_{flag}WIN', 'SYS_FLAG_GAME_CLEAR')))
    s += [('Ligue Pokémon', None, 'Battre la Maîtresse Iris et entrer au Panthéon', c.f('SYS_FLAG_GAME_CLEAR')),
          ('Pavonnay', None, 'Recevoir le Pokédex National', 'N'),
          ('Renouet', None, 'Recevoir la Méga Canne', c.f('SYS_FLAG_SUGOI_TSURI_GET')),
          ('Vaguelone', None, 'Recevoir la CS06 Plongée', c.item('ITEM_HM06')),
          ('Grotte Cyclopéenne', None, 'Recevoir le Pointeau ADN', c.item('ITEM_DNA_SPLICERS_FUSE', 'ITEM_DNA_SPLICERS_SEPARATE'))]
    return s


def bw_encounters(c, version):
    black = version == 'B'
    u = [('Château de N', None, 'SPECIES_RESHIRAM' if black else 'SPECIES_ZEKROM', c.f('FE_PACKAGE_POKE_GET'), None),
         ('Grotte Parsemille', None, 'SPECIES_COBALION', c.f('FE_LEGEND1_GET'), None),
         ('Route Victoire', None, 'SPECIES_TERRAKION', c.f('FE_LEGEND2_GET'), None),
         ("Forêt d'Empoigne", None, 'SPECIES_VIRIZION', c.f('FE_LEGEND3_GET'), None),
         ('Unys', 'errant', 'SPECIES_TORNADUS' if black else 'SPECIES_THUNDURUS', 'D:641' if black else 'D:642', None),
         ('Autel Abondance', None, 'SPECIES_LANDORUS', c.f('FE_R14R0201_LEG_GET'), None),
         ('Grotte Cyclopéenne', None, 'SPECIES_KYUREM', c.f('FE_D10R0401_POKE10_GET'), None),
         ('Château Enfoui', None, 'SPECIES_VOLCARONA', c.f('FE_D04R2301_POKE13_GET'), None),
         ('Désert Délassant', 'Bonbon Rage', 'SPECIES_DARMANITAN', 'D:555', None),
         ('Vestiges du Rêve', 'le vendredi', 'SPECIES_MUSHARNA', 'D:518', None),
         # Distribution réelle : le Pass Liberté (Victini)
         ('Île Liberté', 'Pass Liberté', 'SPECIES_VICTINI', 'D:494', None),
         ('Château Enfoui', 'fossile', 'SPECIES_TIRTOUGA', 'D:564|D:566', 'Carapagos ou Arkéapti'),
         ('Route 18', 'œuf', 'SPECIES_LARVESTA', c.f('FE_R18R0101_POKE_GET'), None),
         ("Pont de l'Inconnu", 'achat', 'SPECIES_MAGIKARP', c.f('FE_H05_BUY_POKE'), None),
         # Noir : Doudouvet donné contre Chlorobule ; Blanc : l'inverse (check-list)
         ('Maillard', 'échange', 'SPECIES_PETILIL' if black else 'SPECIES_COTTONEE', c.f('FE_C02R0301_POKETRADE'), None),
         ('Port Yoneuve', 'échange', 'SPECIES_BASCULIN', c.f('FE_C05R0201_POKETRADE_01'), None),
         ('Route 7', 'échange', 'SPECIES_EMOLGA', c.f('FE_R07R0201_TRADE_01'), None),
         ('Route 15', 'échange', 'SPECIES_ROTOM', c.f('FE_R15R0401_TRADE_01'), None)]
    return u + field_mons(c)


def b2w2_encounters(c, version):
    black = version == 'B2'
    u = [('Tour Dragospire', None, 'SPECIES_ZEKROM' if black else 'SPECIES_RESHIRAM', c.f('FE_D08R0901_LEGEND_GET_01'), None),
         ('Grotte Cyclopéenne', None, 'SPECIES_KYUREM', c.f('FE_D10R0501_LEGEND_GET_01'), None),
         ('Route 13', None, 'SPECIES_COBALION', c.f('FE_R13_POKE4_GET_01'), None),
         ('Route 22', None, 'SPECIES_TERRAKION', c.f('FE_R22_POKE5_GET_01'), None),
         ('Route 11', None, 'SPECIES_VIRIZION', c.f('FE_R11_POKE6_GET_01'), None),
         ('Vestiges du Rêve', None, 'SPECIES_LATIOS' if black else 'SPECIES_LATIAS', c.f('FE_D01R0101_RATI2_GET_01'), None),
         ("Pont de l'Inconnu", None, 'SPECIES_CRESSELIA', c.f('FE_H05_POKE488_GET_01'), None),
         ('Mont Renenvers', None, 'SPECIES_HEATRAN', c.f('FE_D24_POKE485_GET_01'), None),
         ('Ruines Enfouies', None, 'SPECIES_REGIROCK', c.f('FE_D29R0401_POKE2_GET_01'), None),
         ('Ruines Enfouies', None, 'SPECIES_REGISTEEL' if black else 'SPECIES_REGICE',
          c.f('FE_D29R0401_POKE3_GET_01' if black else 'FE_D29R0401_POKE1_GET_01'), None),
         ('Mont Foré', None, 'SPECIES_REGIGIGAS', c.f('FE_D07R0601_POKE4_GET_01'), None),
         ('Château Enfoui', None, 'SPECIES_VOLCARONA', c.f('FE_D04R2301_POKE13_GET_01'), None),
         ('Réserve Naturelle', 'chromatique', 'SPECIES_HAXORUS', c.f('FE_D35R0101_POKE_GET_01'), None),
         ('Route 4', 'le jeudi' if black else 'le lundi', 'SPECIES_MANDIBUZZ' if black else 'SPECIES_BRAVIARY',
          'D:630' if black else 'D:628', None),
         ('Baie Vaguelone', 'le lundi' if black else 'le jeudi', 'SPECIES_JELLICENT', 'D:593', None),
         ('Grotte Littorale', None, 'SPECIES_CRUSTLE', 'D:558', None),
         ('Port Yoneuve', 'Sage Carmine', 'SPECIES_ZORUA', c.f('FE_C05R0501_POKE22_GET'), None),
         ('Route 6', "Bureau d'Étude des Saisons", 'SPECIES_DEERLING', c.f('FE_R06R0101_POKE_GET_01'), None),
         ('Volucité', "labo d'Oryse", 'SPECIES_EEVEE', c.f('FE_C03R1103_POKE_GET'), None),
         ('Maillard', 'œuf', 'SPECIES_HAPPINY', c.f('FE_C02R0701_POKE_GET'), None),
         ('Maillard', 'fossile, Aloé', 'SPECIES_TIRTOUGA', 'D:564|D:566', 'Carapagos ou Arkéapti'),
         ('Amaillide', 'Goyah, chromatique', 'SPECIES_GIBLE' if black else 'SPECIES_DRATINI', 'D:443' if black else 'D:147', None),
         ("Pont de l'Inconnu", 'achat', 'SPECIES_MAGIKARP', c.f('FE_H05_BUY_POKE'), None),
         ('Route 4', 'échange', 'SPECIES_PETILIL' if black else 'SPECIES_COTTONEE', c.f('FE_R04_POKETRADE_01'), None),
         ('Route 7', 'échange', 'SPECIES_GIGALITH', c.f('FE_R07R0201_TRADE_01'), None),
         ('Route 15', 'échange', 'SPECIES_ROTOM', c.f('FE_R15R0401_TRADE_01'), None),
         ('Papeloa', 'échange', 'SPECIES_TANGROWTH', c.f('FE_C14R0201_TRADE_01'), None),
         ('Arabelle', 'échange', 'SPECIES_AMBIPOM', c.f('FE_T02R0101_POKETRADE_01'), None),
         ('Arabelle', 'échange', 'SPECIES_ALAKAZAM', c.f('FE_T02R0101_POKETRADE_02'), None)]
    return u + field_mons(c)


def field_mons(c):
    """Trompignon et Gaulet déguisés en objets (FV_FLDMONS_n : caché une fois capturé ou battu). Lieu : la zone du
    personnage qui a ce drapeau ; espèce : celle de FlagsEditorEX, sinon celle des check-lists (Trompignon sur les Routes 6
    et 7, Gaulet ailleurs)."""
    out = []
    for flag, (_, _, _, desc, name) in sorted(c.fl.items()):
        if not name.startswith('FV_FLDMONS_') or flag not in c.d['npc_flags']:
            continue
        place = c.d['zones'][c.d['npc_flags'][flag]]['place']
        foongus = 'Foongus' in desc if desc else place in ('Route 6', 'Route 7')
        out.append((place, 'déguisé en objet', 'SPECIES_FOONGUS' if foongus else 'SPECIES_AMOONGUSS', f'E:{flag}', None))
    return out


def subway(prefix=''):
    """Métro de Combat : Chefs battus en mode normal (le mode Super s'ouvre) et en mode Super (49e combat : série d'au
    moins 49 victoires)."""
    q = []
    for name, key in (('Simple', 'Single'), ('Double', 'Double'), ('Multi', 'Multi')):
        q.append(('Métro de Combat', name, f'Battre les Chefs du Métro (Ligne {name})', f'SB:Super{key}:1', None, 0))
        record = f'SuperMultiNPCRecord' if key == 'Multi' else f'Super{key}Record'
        q.append(('Métro de Combat', name, f'Battre les Chefs du Métro (Ligne Super {name})', f'SB:{record}:49', None, 0))
    return q


PWT = [('Port Yoneuve', 18, 'Tournoi de Port Yoneuve'), ('Unys', 19, 'Tournoi Champions d\'Unys'),
       ('Kanto', 20, 'Tournoi Champions de Kanto'), ('Johto', 21, 'Tournoi Champions de Johto'),
       ('Hoenn', 22, 'Tournoi Champions de Hoenn'), ('Sinnoh', 23, 'Tournoi Champions de Sinnoh'),
       ('Monde', 24, 'Tournoi Champions du Monde'), ('Maîtres', 17, 'Tournoi des Maîtres'),
       ('Location', 25, 'Tournoi Location'), ('Location Expert', 26, 'Tournoi Location Expert'),
       ('Mix', 27, 'Tournoi Mix'), ('Mix Expert', 28, 'Tournoi Mix Expert')]
TYPES = ['Normal', 'Combat', 'Vol', 'Poison', 'Sol', 'Roche', 'Insecte', 'Spectre', 'Acier', 'Feu', 'Eau', 'Plante',
         'Électrik', 'Psy', 'Glace', 'Dragon', 'Ténèbres']


# Catégories des médailles (MedalList5.GetMedalType de PKHeX : numéro de la première de chaque catégorie suivante)
MEDAL_TYPES = [(7, 'Spéciales'), (105, 'Aventure'), (161, 'Combat'), (236, 'Divertissement'), (255, 'Défi')]


def props_quests(props):
    """Parures du Music-Hall (100, un bit chacune dans la sauvegarde : Musical5.GetHasProp de PKHeX)."""
    return [('Music-Hall', 'parure', name, f'PR:{n}', None, 0) for n, name in enumerate(props)]


def b2w2_quests(medals, props):
    q = subway()
    q += [('PWT', None, f'Remporter le {name}', f'PW:{rid}', None, 0) for _, rid, name in PWT]
    q += [('PWT', None, f'Remporter le Tournoi par Type Expert ({t})', f'PW:{n}', None, 0) for n, t in enumerate(TYPES)]
    # 254 médailles : la 255e du fichier (« Médaille Aide ») n'en est pas une
    q += [('Médailles', next(cat for limit, cat in MEDAL_TYPES if n < limit), name, f'MD:{n}', None, 0)
          for n, name in enumerate(medals[:254])]
    return q + props_quests(props)


# ---------------------------------------------------------------------------------------------------------------------

def detail(zone_name):
    """Précision tirée du nom de la zone de pokebw2 (« ZONE_VIRBANK_CITY_GYM » → « Arène »)."""
    if not zone_name or not zone_name.startswith('ZONE_'):
        return None
    tokens = zone_name.split('_')
    if 'GYM' in tokens:
        return 'Arène'
    if 'CENTER' in tokens:
        return 'Centre Pokémon'
    return None


def cs(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"' if s is not None else 'null'


def build(d, version, checklist, cur):
    k = d['k']
    rows = []
    uses_set, uses_reset, _ = d['uses']

    def order(zone, strict):
        rank = anchored_rank(zone, checklist, ANCHORS)
        if rank is None and strict:
            MISSING_PLACES.add(f'{zone} ({d["game"]})')
        return 10_000 if rank is None else rank

    def add(section, zone, det, done, label=None, missed=None, team=None, item=0, tm=0, species=0, count=0, sort=0):
        if section in ('Trainers', 'Items') and version not in EXCLUSIVE_PLACES.get(zone, (version,)):
            return  # Ville Noire, Forêt Blanche… : dans une seule version
        rows.append(dict(section=section, zone=zone, detail=det, done=done, label=label, missed=missed, team=team, item=item,
                         tm=tm, species=species, count=count,
                         sort=(order(zone, section in ('Trainers', 'Items')), zone, sort)))

    S, D, I, U, Q = 'Story', 'Trainers', 'Items', 'Encounters', 'Quests'
    for i, (zone, det, label, cond) in enumerate(cur['story']):
        add(S, zone, det, cond, label, sort=i)

    # ---------- Dresseurs : ceux qui ont un personnage sur la carte ----------
    groups = {}
    for t in d['trainers']:
        groups.setdefault(t['tid'], []).append(t)  # un dresseur sur plusieurs zones d'un même lieu : une étape
    code = VERSION_CODES[version]
    for tid, ts in groups.items():
        t = ts[0]
        zid = t['zone']
        hide = t['hide']
        if hide in d['version_flags']:
            equal, v = d['version_flags'][hide]
            if (code == v) == equal:
                continue  # caché dans cette version : dresseur de l'autre version
            missed = None
        else:
            missed = f'E:{hide}' if hide >= LOCAL_FLAGS and hide in uses_set and hide not in uses_reset else None
        team = ','.join(f'{sp}:{lv}' for sp, lv in t['team']) or None
        z = d['zones'][zid]
        add(D, z['place'], detail(z['name']), f'E:{cur["trainer_flags"] + tid}', None if team else 'Dresseur',
            missed=missed, team=team, sort=tid)

    # ---------- Objets ----------
    tm_of = {}
    for name, value in k.items():
        m = re.fullmatch(r'ITEM_(TM|HM)(\d\d)', name)
        if m:
            tm_of[value] = int(m.group(2)) + (95 if m.group(1) == 'HM' else 0)
    seen = set()

    def pick(variants):
        for item, v in variants:
            if v is None or v == version:
                return item
        return None

    def item(flag, value, count, zone_id, place, kind, extra):
        if flag < LOCAL_FLAGS or flag in DAILY_FLAGS or flag in seen:
            return
        seen.add(flag)
        det = ', '.join(p for p in ((detail(d['zones'][zone_id]['name']) if zone_id is not None else None), extra) if p) or None
        if isinstance(value, str):  # choix exclusif (fossiles) : son texte
            add(I, place, det, f'E:{flag}', value, sort=kind * 100_000 + flag)
            return
        add(I, place, det, f'E:{flag}', item=0 if value in tm_of else value, tm=tm_of.get(value, 0),
            count=count if 1 < count < 0x4000 else 0, sort=kind * 100_000 + flag)

    for flag, x in sorted(d['ground'].items()):
        value = pick(x['items'])
        if value is not None:
            item(flag, value, 1, x['zone'], x['place'], 0, None)
    for flag, x in sorted(d['hidden'].items()):
        value = pick(x['items'])
        if value is not None:
            item(flag, value, 1, x['zone'], x['place'], 1, 'caché')
    by_flag = {}
    for zid, gs in d['gifts'].items():
        for value, count, flag in gs:
            if isinstance(flag, int):
                by_flag.setdefault(flag, (zid, []))[1].append((value, count))
    for flag, (zid, gs) in sorted(by_flag.items()):
        values = list(dict.fromkeys(v for v, _ in gs))
        if any(cur['item_key'](v).startswith(('ITEM_GRAM', 'ITEM_XTRANSCEIVER')) for v in values):
            continue  # lettres à porter (Route 13), rendues ; Vokit (selon le héros), dans l'histoire
        if flag in d['ground'] or flag in d['hidden'] or flag in cur['gift_exclude']:
            continue
        value = values[0] if len(values) == 1 else ' ou '.join(cur['item_name'](v) for v in values)
        item(flag, value, gs[0][1], zid, d['zones'][zid]['place'], 2, 'cadeau')

    # ---------- Rencontres et quêtes ----------
    for i, (zone, det, species, cond, label) in enumerate(cur['encounters']):
        add(U, zone, det, cond, label, species=k[species], sort=i)
    for i, (zone, det, label, cond, species, _) in enumerate(cur['quests']):
        add(Q, zone, det, cond, label, species=k[species] if species else 0, sort=i)

    section_order = {s: n for n, s in enumerate((S, D, I, U, Q))}
    rows.sort(key=lambda r: (section_order[r['section']],) + ((r['sort'][2],) if r['section'] in (S, U, Q) else r['sort']))
    return rows


MISSING_PLACES = set()
# Lieux d'une seule version (la ROM de Noir a aussi la Forêt Blanche, inaccessible)
EXCLUSIVE_PLACES = {'Ville Noire': ('B', 'B2'), 'Forêt Blanche': ('W', 'W2'), 'Gratte-Ciel Noir': ('B2',),
                    'Arbre Creux Blanc': ('W2',)}
VERSION_CODES = {'B': 21, 'W': 20, 'B2': 23, 'W2': 22}  # GameVersion de PKHeX


def emit(games, out):
    lines = ['// <auto-generated>',
             "// Généré par outils/completion_gen5.py d'après le désassemblage pokebw2, la ROM de Pokémon Version Noire et les",
             '// listes de drapeaux de FlagsEditorEX : ne pas modifier à la main, changer le script et le relancer.',
             '// </auto-generated>',
             '',
             'namespace MonsutaPotaru.Services;',
             '',
             'public static class Gen5CompletionData',
             '{',
             '']
    for name, doc, rows in games:
        lines.append(f'    /// <summary>Étapes de {doc} (le Pokédex national est ajouté à part : les 649).</summary>')
        lines.append(f'    public static readonly CompletionRow[] {name} =')
        lines.append('    [')
        for r in rows:
            a_ = [f'CompletionSection.{r["section"]}', cs(r['zone']), cs(r['detail']), cs(r['done'])]
            n = []
            for key, field in (('label', 'Label'), ('missed', 'Missed'), ('team', 'Team')):
                if r[key]:
                    n.append(f'{field}: {cs(r[key])}')
            for key, field in (('item', 'Item'), ('tm', 'Tm'), ('count', 'Count'), ('species', 'Species')):
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
    root, rom, rom_w2, out = sys.argv[1:5]
    here = os.path.dirname(os.path.abspath(__file__))
    lieux = json.load(open(os.path.join(here, 'completion_gen5_ordre.json'), encoding='utf-8'))
    places = [line.rstrip('\n') for line in open(os.path.join(root, 'text_bw2_00000_fr.txt'), encoding='utf-8')]
    english = dict(zip((line.rstrip('\n') for line in open(os.path.join(root, 'text_bw2_00000_en.txt'), encoding='utf-8')), places))
    b2 = extract_b2w2(os.path.join(root, 'pokebw2'), os.path.join(root, 'flags_gen5b2w2_en.txt'), places, english)
    bw = extract_bw(rom, os.path.join(root, 'pokebw2', 'include', 'asm', 'field_script.inc'),
                    os.path.join(root, 'flags_gen5bw_en.txt'), b2['k'], english)
    k = b2['k']
    fr_items = [line.rstrip('\n') for line in open(os.path.join(root, 'text_Items_fr.txt'), encoding='utf-8')]
    item_keys = {v: n for n, v in k.items() if n.startswith('ITEM_')}
    system = Rom(rom_w2).narc('a/0/0/2')
    medals = texts(system[83])
    props = texts(system[93])[3:103]

    def item_name(v):
        return fr_items[v]

    games = []
    for d, versions, story, enc, trainer_flags in (
            (bw, (('Black', 'Noire', 'B'), ('White', 'Blanche', 'W')), bw_story, bw_encounters, 0x58C),
            (b2, (('Black2', 'Noire 2', 'B2'), ('White2', 'Blanche 2', 'W2')), b2w2_story, b2w2_encounters, 0x5F0)):
        c = Conds(d['fattard'], k)
        c.fl = d['fattard']
        c.english = english
        c.d = d
        for name, doc, v in versions:
            cur = dict(story=story(c, v), encounters=enc(c, v),
                       quests=subway() + props_quests(props) if d['game'] == 'bw' else b2w2_quests(medals, props),
                       trainer_flags=trainer_flags, gift_exclude=set(), item_name=item_name,
                       item_key=lambda v: item_keys[v])
            games.append((name, f'Pokémon {doc}', build(d, v, lieux[d['game']], cur)))
    if MISSING_PLACES:
        raise SystemExit('Lieux absents de la check-list (leur donner un voisin dans ANCHORS) : ' + ', '.join(sorted(MISSING_PLACES)))
    emit(games, out)
    for name, _, rows in games:
        counts = {}
        for r in rows:
            counts[r['section']] = counts.get(r['section'], 0) + 1
        print(name, counts, 'ratés possibles', sum(1 for r in rows if r['missed']))
