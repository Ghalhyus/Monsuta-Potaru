"""
Génère MonsutaPotaru/Services/Completion/Gen2CompletionData.cs d'après les désassemblages pokegold et pokecrystal (pret).

    python outils/completion_gen2.py <dossier> MonsutaPotaru/Services/Completion/Gen2CompletionData.cs

<dossier> contient les clones de https://github.com/pret/pokegold et https://github.com/pret/pokecrystal
(git clone --depth 1). Les étapes de l'histoire, des rencontres et des quêtes viennent des check-lists de l'utilisateur
(Or / Argent et Cristal, d'après Pokébip) et sont choisies à la main ici ; dresseurs, objets, objets cachés, cadeaux et
échanges sont lus dans les dépôts. Les lieux en français et l'ordre de l'aventure sont dans ce script.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from completion_gen2_extract import extract  # noqa: E402

# Textes répétés (lieux, libellés)
ANTRE_DU_DRAGON = 'Antre du Dragon'
BOIS_AUX_CHENES = 'Bois aux Chênes'
BOURG_GEON = 'Bourg Geon'
BOURG_PALETTE = 'Bourg Palette'
CARMIN_SUR_MER = 'Carmin sur Mer'
CAVES_JUMELLES = 'Caves Jumelles'
CELADOPOLE = 'Céladopole'
EBENELLE = 'Ébènelle'
ECORCIA = 'Écorcia'
ERRANT_DANS_JOHTO = 'errant dans Johto'
ILES_ECUME = 'Îles Écume'
LABO_DU_PROF_ORME = 'Labo du Prof. Orme'
LAC_COLERE = 'Lac Colère'
L_AQUARIA = "L'Aquaria"
MAISON_DE_COPIEUSE = 'maison de Copieuse'
MONT_ARGENTE = 'Mont Argenté'
MONT_CREUSET = 'Mont Creuset'
MONT_SELENITE = 'Mont Sélénite'
PHARE_D_OLIVILLE = "Phare d'Oliville"
PLATEAU_INDIGO = 'Plateau Indigo'
POSTE_DE_GARDE = 'poste de garde'
PUITS_RAMOLOSS = 'Puits Ramoloss'
RANCH_MEUMEU = 'Ranch Meumeu'
REPAIRE_ROCKET = 'Repaire Rocket'
ROUTE_25 = 'Route 25'
ROUTE_29 = 'Route 29'
ROUTE_30 = 'Route 30'
ROUTE_36 = 'Route 36'
ROUTE_VICTOIRE = 'Route Victoire'
RUINES_D_ALPHA = "Ruines d'Alpha"
SOUS_SOL_2 = 'sous-sol 2'
TOURB_ILES = "Tourb'Îles"
TOUR_CENDREE = 'Tour Cendrée'
TOUR_CHETIFLOR = 'Tour Chétiflor'
TOUR_FERRAILLE = 'Tour Ferraille'
TOUR_RADIO = 'Tour Radio'
VILLE_GRIOTTE = 'Ville Griotte'

# Octets de la sauvegarde (versions internationales), d'après PKHeX (SAV2Offsets) et les WRAM des désassemblages.
OFFSETS = {
    'pokegold': {'badges': 0x23E4, 'events': 0x261F, 'unown': 0x2A8C},
    'pokecrystal': {'badges': 0x23E5, 'events': 0x2600, 'unown': 0x2A67},
}

ZONES = [
    BOURG_GEON, ROUTE_29, VILLE_GRIOTTE, ROUTE_30, 'Route 31', 'Mauville', TOUR_CHETIFLOR, 'Route 32',
    RUINES_D_ALPHA, CAVES_JUMELLES, 'Route 33', ECORCIA, PUITS_RAMOLOSS, BOIS_AUX_CHENES, 'Route 34',
    'Doublonville', 'Route 35', 'Parc Naturel', ROUTE_36, 'Route 37', 'Rosalia', TOUR_CENDREE, TOUR_FERRAILLE,
    'Route 38', 'Route 39', 'Oliville', PHARE_D_OLIVILLE, 'Route 40', 'Tour de Combat', 'Route 41', TOURB_ILES,
    'Irisia', 'Route 42', MONT_CREUSET, 'Acajou', 'Route 43', LAC_COLERE, REPAIRE_ROCKET, 'Route 44',
    'Route de Glace', EBENELLE, ANTRE_DU_DRAGON, 'Route 45', 'Route 46', 'Antre Noir', 'Route 27', 'Chutes Tohjo',
    'Route 26', ROUTE_VICTOIRE, PLATEAU_INDIGO, L_AQUARIA, CARMIN_SUR_MER, 'Cave Taupiqueur', 'Route 6',
    'Souterrain', 'Route 5', 'Safrania', 'Route 7', 'Route 8', 'Lavanville', 'Route 11', 'Route 12', 'Route 13',
    'Route 14', 'Route 15', 'Parmanie', 'Route 16', 'Route 17', 'Route 18', CELADOPOLE, 'Route 19', 'Route 20',
    ILES_ECUME, "Cramois'Île", 'Route 21', BOURG_PALETTE, 'Route 1', 'Jadielle', 'Route 22', 'Route 2', 'Argenta',
    'Route 3', MONT_SELENITE, 'Route 4', 'Azuria', 'Route 24', ROUTE_25, 'Route 9', 'Route 10', 'Centrale',
    'Grotte', 'Route 28', MONT_ARGENTE,
]

# Préfixe du nom de la carte → zone (le plus long qui convient).
PREFIXES = {
    'NewBarkTown': BOURG_GEON, 'ElmsLab': BOURG_GEON, 'ElmsHouse': BOURG_GEON, 'PlayersHouse': BOURG_GEON,
    'PlayersNeighborsHouse': BOURG_GEON, 'Cherrygrove': VILLE_GRIOTTE, 'GuideGentsHouse': VILLE_GRIOTTE,
    'MrPokemonsHouse': ROUTE_30, 'Violet': 'Mauville', 'EarlsPokemonAcademy': 'Mauville', 'SproutTower': TOUR_CHETIFLOR,
    'RuinsOfAlph': RUINES_D_ALPHA, 'UnionCave': CAVES_JUMELLES, 'Azalea': ECORCIA, 'KurtsHouse': ECORCIA,
    'CharcoalKiln': ECORCIA, 'SlowpokeWell': PUITS_RAMOLOSS, 'IlexForest': BOIS_AUX_CHENES,
    'DayCare': 'Route 34', 'Goldenrod': 'Doublonville', 'RadioTower': 'Doublonville', 'NationalPark': 'Parc Naturel',
    'Ecruteak': 'Rosalia', 'DanceTheater': 'Rosalia', 'WiseTriosRoom': 'Rosalia', 'BurnedTower': TOUR_CENDREE,
    'TinTower': TOUR_FERRAILLE, 'Olivine': 'Oliville', 'OlivineLighthouse': PHARE_D_OLIVILLE,
    'BattleTower': 'Tour de Combat', 'WhirlIsland': TOURB_ILES, 'Cianwood': 'Irisia', 'ManiasHouse': 'Irisia',
    'PokeSeersHouse': 'Irisia', 'MountMortar': MONT_CREUSET, 'Mahogany': 'Acajou', 'LakeOfRage': LAC_COLERE,
    'TeamRocketBase': REPAIRE_ROCKET, 'IcePath': 'Route de Glace', 'Blackthorn': EBENELLE,
    'MoveDeletersHouse': EBENELLE, 'DragonsDen': ANTRE_DU_DRAGON, 'DragonShrine': ANTRE_DU_DRAGON,
    'DarkCave': 'Antre Noir', 'TohjoFalls': 'Chutes Tohjo', 'VictoryRoad': ROUTE_VICTOIRE,
    'IndigoPlateau': PLATEAU_INDIGO, 'WillsRoom': PLATEAU_INDIGO, 'KogasRoom': PLATEAU_INDIGO,
    'BrunosRoom': PLATEAU_INDIGO, 'KarensRoom': PLATEAU_INDIGO, 'LancesRoom': PLATEAU_INDIGO,
    'HallOfFame': PLATEAU_INDIGO, 'FastShip': L_AQUARIA, 'Vermilion': CARMIN_SUR_MER,
    'PokemonFanClub': CARMIN_SUR_MER, 'DiglettsCave': 'Cave Taupiqueur', 'UndergroundPath': 'Souterrain',
    'Saffron': 'Safrania', 'CopycatsHouse': 'Safrania', 'MrPsychicsHouse': 'Safrania', 'FightingDojo': 'Safrania',
    'SilphCo': 'Safrania', 'Lavender': 'Lavanville', 'LavRadioTower': 'Lavanville', 'SoulHouse': 'Lavanville',
    'MrFujisHouse': 'Lavanville', 'Fuchsia': 'Parmanie', 'SafariZone': 'Parmanie', 'Celadon': CELADOPOLE,
    'SeafoamGym': ILES_ECUME, 'Cinnabar': "Cramois'Île", 'Pallet': BOURG_PALETTE, 'OaksLab': BOURG_PALETTE,
    'RedsHouse': BOURG_PALETTE, 'BluesHouse': BOURG_PALETTE, 'Viridian': 'Jadielle', 'TrainerHouse': 'Jadielle',
    'Pewter': 'Argenta', 'MountMoon': MONT_SELENITE, 'Cerulean': 'Azuria', 'BillsHouse': ROUTE_25,
    'BillsFamilysHouse': 'Doublonville', 'BillsOlderSistersHouse': 'Lavanville', 'PowerPlant': 'Centrale',
    'RockTunnel': 'Grotte', 'SilverCave': MONT_ARGENTE, 'DayOfWeekSiblingsHouse': 'Route 26',
}

# Précision du lieu pour une carte précise, quand la règle générale ne suffit pas.
DETAILS = {
    'ElmsLab': LABO_DU_PROF_ORME, 'ElmsHouse': 'maison du Prof. Orme', 'PlayersHouse1F': 'maison', 'PlayersHouse2F': 'chambre',
    'PlayersNeighborsHouse': 'maison des voisins', 'MrPokemonsHouse': 'maison de M. Pokémon', 'KurtsHouse': 'maison de Fargas',
    'CharcoalKiln': 'four à charbon', 'DanceTheater': 'Théâtre', 'WiseTriosRoom': 'salle des Sages',
    'EarlsPokemonAcademy': 'École Pokémon', 'BillsHouse': 'Cottage de Léo', 'BillsFamilysHouse': 'famille de Léo',
    'BillsOlderSistersHouse': 'sœur de Léo', 'FightingDojo': 'Dojo', 'MrPsychicsHouse': 'maison de M. Psy',
    'CopycatsHouse1F': MAISON_DE_COPIEUSE, 'CopycatsHouse2F': MAISON_DE_COPIEUSE, 'SilphCo1F': 'Sylphe SARL',
    'PokemonFanClub': 'Club des fans', 'GoldenrodBikeShop': 'Cycles à Gogo', 'GoldenrodFlowerShop': 'fleuriste',
    'GoldenrodGameCorner': 'Casino', 'CeladonGameCorner': 'Casino', 'CeladonCafe': 'café', 'OlivineCafe': 'café',
    'CianwoodPharmacy': 'pharmacie', 'MahoganyMart1F': 'boutique', 'Route39Barn': RANCH_MEUMEU, 'Route39Farmhouse': RANCH_MEUMEU,
    'LavRadioTower1F': TOUR_RADIO, 'SoulHouse': 'Maison des Âmes', 'MrFujisHouse': 'maison de M. Fuji',
    'TrainerHouse1F': 'Maison des Dresseurs', 'TrainerHouseB1F': 'Maison des Dresseurs', 'DayOfWeekSiblingsHouse': 'maison de la Fratrie',
    'GoldenrodUnderground': 'souterrain', 'GoldenrodUndergroundWarehouse': 'entrepôt du souterrain',
    'GoldenrodUndergroundSwitchRoomEntrances': 'souterrain', 'DragonShrine': 'sanctuaire', 'DayCare': 'pension',
    'SilverCaveItemRooms': 'salles du fond', 'SafariZoneWardensHome': 'maison du Gardien', 'PowerPlant': None,
    'IlexForestAzaleaGate': POSTE_DE_GARDE, 'Route34IlexForestGate': POSTE_DE_GARDE, 'LakeOfRageHiddenPowerHouse': 'maison',
    'LakeOfRageMagikarpHouse': 'maison', 'GoldenrodDeptStoreRoof': 'toit du Centre commercial',
    'CeladonMansionRoof': 'toit du Manoir', 'CeladonMansionRoofHouse': 'toit du Manoir', 'TinTowerRoof': 'toit',
    'WhirlIslandLugiaChamber': 'grotte de Lugia', 'SilverCaveOutside': 'dehors', 'MountMortar1FOutside': 'dehors',
    'NationalParkBugContest': None, 'VermilionPort': 'port', 'OlivinePort': 'port', 'FastShipCabins_NNW_NNE_NE': 'cabines',
    'FastShipCabins_SE_SSE_CaptainsCabin': 'cabines', 'FastShipCabins_SW_SSW_NW': 'cabines',
    'DarkCaveVioletEntrance': 'côté Mauville', 'DarkCaveBlackthornEntrance': 'côté Ébènelle',
    'IcePathB2FMahoganySide': SOUS_SOL_2, 'IcePathB2FBlackthornSide': SOUS_SOL_2, 'Route10North': None, 'Route10South': None,
    'MountMoonSquare': 'place', 'MountMoonGiftShop': 'boutique', 'CeruleanPoliceStation': 'commissariat',
    'BattleTower1F': None, 'BattleTowerOutside': None, 'EcruteakTinTowerEntrance': 'entrée de la Tour Ferraille',
    'RuinsOfAlphResearchCenter': 'centre de recherche', 'GoldenrodDeptStoreElevator': 'ascenseur',
}

# Bâtiments à étages : leur nom devant l'étage (« Tour Radio, 1er étage »).
BUILDINGS = {'RadioTower': TOUR_RADIO, 'CeladonMansion': 'Manoir', 'SilphCo': 'Sylphe SARL', 'LavRadioTower': TOUR_RADIO}


def floor(code):
    """« 1F » → « rez-de-chaussée », « 3F » → « 2e étage », « B2F » → « sous-sol 2 »."""
    m = re.fullmatch(r'(B?)(\d+)F', code)
    if not m:
        return None
    n = int(m.group(2))
    if m.group(1):
        return f'sous-sol {n}'
    if n == 1:
        return 'rez-de-chaussée'
    return '1er étage' if n == 2 else f'{n - 1}e étage'


def zone_prefix(name):
    """Préfixe de zone le plus long de cette carte (vide pour une route)."""
    return max((p for p in PREFIXES if name.startswith(p)), key=len, default='')


def place(name):
    """Carte → (zone, précision)."""
    zone = None
    m = re.match(r'Route(\d+)', name)
    if m:
        zone, rest = f'Route {m.group(1)}', name[len(m.group(0)):]
    for p in sorted(PREFIXES, key=len, reverse=True):
        if name.startswith(p) and (zone is None or len(p) > len(m.group(0))):
            zone, rest = PREFIXES[p], name[len(p):]
            break
    if zone is None:
        raise SystemExit(f'Carte sans zone : {name}')
    if name in DETAILS:
        return zone, DETAILS[name]
    rest = re.sub(r'(Town|City|Island)$', '', rest)
    if rest in ('', 'Outside', 'North', 'South', 'Inside'):
        return zone, None
    for b, label in BUILDINGS.items():
        if name.startswith(b) and floor(name[len(b):]):
            return zone, f'{label}, {floor(name[len(b):])}'
        if name.startswith(zone_prefix(name)) and rest.startswith(b) and floor(rest[len(b):]):
            return zone, f'{label}, {floor(rest[len(b):])}'
    rules = [
        (r'Gym(\dF)?$', 'Arène'), (r'Pokecenter1F$', 'Centre Pokémon'), (r'Mart$', 'Boutique'),
        (r'DeptStore(B?\d+F)$', lambda f: f'Centre commercial, {floor(f)}'), (r'MagnetTrainStation$', 'gare'),
        (r'Lighthouse(\d+F)$', lambda f: floor(f)), (r'Gate$', POSTE_DE_GARDE), (r'.*House$', 'maison'),
        (r'^(B?\d+F)$', lambda f: floor(f)), (r'(B?\d+F)$', lambda f: floor(f)),
        (r'(B?\d+F)Inside$', lambda f: floor(f)), (r'Room(\d)$', lambda n: f'salle {n}'),
        (r'.*(Chamber|Room)$', 'salle'), (r'Barn|Farmhouse', 'ferme'), (r'Entrance$', 'entrée'), (r'Rater$', 'maison'),
        (r'^(NE|NW|SE|SW|Cave)$', 'grotte'),
    ]
    for pattern, label in rules:
        mm = re.search(pattern, rest)
        if mm:
            return zone, label(mm.group(1)) if callable(label) else label
    raise SystemExit(f'Précision inconnue pour {name} ({zone}, « {rest} »)')


def cs(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"' if s is not None else 'null'


def build(d, game):
    crystal = game == 'pokecrystal'
    ev, items, dex = d['events'], d['items'], d['dex']
    off = OFFSETS[game]
    rows = []

    def event_cond(name):
        if name not in ev:
            raise SystemExit(f'Drapeau inconnu dans {game} : {name}')
        return f'E:{ev[name]}'

    def bit_cond(offset, bit):
        return f'R:{offset + bit // 8}:{bit % 8}'

    def wram(label):
        """Adresse dans la sauvegarde d'un octet de la WRAM situé avant wEventFlags (échanges, scènes)."""
        return off['events'] - d['before_events'][label]

    def scene(label, value):
        """La scène de la carte a atteint cette valeur (« S:octet:valeur ») : le combat qui la fait avancer a eu lieu."""
        return f'S:{wram(label)}:{value}'

    def sp(name):
        return dex[name]

    def add(section, zone, detail, done, label=None, missed=None, team=None, item=0, tm=0, species=0, give=0, count=0, form=0):
        if zone not in ZONES:
            raise SystemExit(f'Zone inconnue : {zone}')
        rows.append({'section': section, 'zone': zone, 'detail': detail, 'done': done, 'label': label, 'missed': missed, 'team': team,
                         'item': item, 'tm': tm, 'species': species, 'give': give, 'count': count, 'form': form})

    S, D, I, U, Q = 'Story', 'Trainers', 'Items', 'Encounters', 'Quests'

    # ---------- Histoire ----------
    johto = [('Badge Zéphyr', 'Albert', 'Mauville'), ('Badge Essaim', 'Hector', ECORCIA),
             ('Badge Plaine', 'Blanche', 'Doublonville'), ('Badge Brume', 'Mortimer', 'Rosalia'),
             ('Badge Minéral', 'Jasmine', 'Oliville'), ('Badge Choc', 'Chuck', 'Irisia'),
             ('Badge Glacier', 'Fredo', 'Acajou'), ('Badge Lever', 'Sandra', EBENELLE)]
    kanto = [('Badge Roche', 'Pierre', 'Argenta'), ('Badge Cascade', 'Ondine', 'Azuria'),
             ('Badge Foudre', 'Major Bob', CARMIN_SUR_MER), ('Badge Prisme', 'Érika', CELADOPOLE),
             ('Badge Âme', 'Jeannine', 'Parmanie'), ('Badge Marais', 'Morgane', 'Safrania'),
             ('Badge Volcan', 'Auguste', ILES_ECUME), ('Badge Terre', 'Blue', 'Jadielle')]

    def badge(region, n):
        name, leader, city = (johto if region == 0 else kanto)[n]
        add(S, city, 'Arène', bit_cond(off['badges'] + region, n), f'{name} ({leader})')

    def story(zone, label, done, detail=None):
        add(S, zone, detail, done, label)

    story(BOURG_GEON, 'Recevoir son premier Pokémon du Prof. Orme', event_cond('EVENT_GOT_A_POKEMON_FROM_ELM'), LABO_DU_PROF_ORME)
    story(ROUTE_30, "Récupérer l'Œuf Mystère chez M. Pokémon (et le Pokédex)", event_cond('EVENT_GOT_MYSTERY_EGG_FROM_MR_POKEMON'))
    story(BOURG_GEON, "Remettre l'Œuf Mystère au Prof. Orme", event_cond('EVENT_GAVE_MYSTERY_EGG_TO_ELM'), LABO_DU_PROF_ORME)
    story(TOUR_CHETIFLOR, "Battre les Sages jusqu'au sommet", event_cond('EVENT_BEAT_SAGE_LI'))
    badge(0, 0)
    story(PUITS_RAMOLOSS, 'Chasser la Team Rocket du Puits Ramoloss', event_cond('EVENT_CLEARED_SLOWPOKE_WELL'))
    badge(0, 1)
    story(BOIS_AUX_CHENES, "Ramener le Canarticho de l'apprenti", event_cond('EVENT_HERDED_FARFETCHD'))
    badge(0, 2)
    story(TOUR_CENDREE, 'Réveiller Raikou, Entei et Suicune', event_cond('EVENT_RELEASED_THE_BEASTS'))
    badge(0, 3)
    if crystal:
        story('Irisia', "Voir Suicune s'enfuir et battre Eusine", event_cond('EVENT_FOUGHT_EUSINE'))
    badge(0, 5)
    story(PHARE_D_OLIVILLE, 'Soigner Pharamp avec la Potionsecret', event_cond('EVENT_JASMINE_RETURNED_TO_GYM'))
    badge(0, 4)
    story(REPAIRE_ROCKET, "Vaincre la Team Rocket au repaire d'Acajou", event_cond('EVENT_CLEARED_ROCKET_HIDEOUT'))
    badge(0, 6)
    story('Doublonville', 'Libérer la Tour Radio de la Team Rocket', event_cond('EVENT_CLEARED_RADIO_TOWER'), TOUR_RADIO)
    badge(0, 7)
    for who, flag in (('Clément', 'EVENT_BEAT_ELITE_4_WILL'), ('Koga', 'EVENT_BEAT_ELITE_4_KOGA'),
                      ('Aldo', 'EVENT_BEAT_ELITE_4_BRUNO'), ('Marion', 'EVENT_BEAT_ELITE_4_KAREN')):
        # Le jeu efface ces drapeaux pour les revanches : la Ligue gagnée une fois suffit.
        story(PLATEAU_INDIGO, f'Battre {who} (Conseil 4)', event_cond(flag) + '|' + event_cond('EVENT_BEAT_ELITE_FOUR'))
    story(PLATEAU_INDIGO, 'Battre le Maître Peter et entrer au Panthéon', event_cond('EVENT_BEAT_ELITE_FOUR'))
    story('Centrale', 'Rapporter la Partie Mach. et rétablir le courant', event_cond('EVENT_RESTORED_POWER_TO_KANTO'))
    for n in range(8):
        badge(1, n)
    story(BOURG_PALETTE, 'Parler au Prof. Chen : accès au Mont Argenté', event_cond('EVENT_TALKED_TO_OAK_IN_KANTO'), 'Labo du Prof. Chen')
    # Red n'a pas de drapeau à lui : il quitte le Mont Argenté (drapeau levé), et le Panthéon le fait revenir.
    story(MONT_ARGENTE, 'Battre Red', event_cond('EVENT_RED_IN_MT_SILVER') + '+' + event_cond('EVENT_TALKED_TO_OAK_IN_KANTO'))

    # ---------- Dresseurs ----------
    rows_t = []
    # Combats contre le rival : aucun drapeau durable (son objet est caché avant comme après) ; chacun barre la route
    # vers l'étape donnée, qui sert de preuve.
    # Tour Cendrée et Route Victoire : la scène de la carte passe de « combat contre le rival » (0) à la suite.
    rivals = [(VILLE_GRIOTTE, None, event_cond('EVENT_GAVE_MYSTERY_EGG_TO_ELM')), (ECORCIA, None, event_cond('EVENT_GOT_HM01_CUT')),
              (TOUR_CENDREE, None, scene('wBurnedTower1FSceneID', 1)),
              ('Doublonville', 'souterrain', event_cond('EVENT_RECEIVED_CARD_KEY')),
              (ROUTE_VICTOIRE, None, scene('wVictoryRoadSceneID', 1)), (MONT_SELENITE, None, event_cond('EVENT_BEAT_RIVAL_IN_MT_MOON'))]
    for zone, detail, done in rivals:
        rows_t.append((ZONES.index(zone), 0, {'zone': zone, 'detail': detail, 'done': done, 'label': 'Rival', 'missed': None, 'team': None}))
    radio = event_cond('EVENT_CLEARED_RADIO_TOWER')
    for t in d['trainers']:
        zone, detail = place(t['map'])
        hide = t['hide']
        missed = None
        if hide == 'EVENT_RADIO_TOWER_ROCKET_TAKEOVER':
            missed = event_cond(hide) + '+' + radio  # partis avec la Team Rocket
        elif hide and hide not in d['initial']:
            missed = event_cond(hide)  # visibles au départ, partis ensuite (Puits Ramoloss, repaire, premier voyage de L'Aquaria)
        team = ','.join(f'{sp(s)}:{lv}' for lv, s in t['team'])
        rows_t.append((ZONES.index(zone), ev[t['flag']], {'zone': zone, 'detail': detail, 'done': event_cond(t['flag']), 'label': None,
                                                             'missed': missed, 'team': team}))
    seen = set()
    for z, f, r in sorted(rows_t, key=lambda x: (x[0], x[1])):
        if r['done'] in seen and r['label'] is None:
            continue  # Jumelles : un seul combat pour deux objets
        seen.add(r['done'])
        add(D, r['zone'], r['detail'], r['done'], r['label'], missed=r['missed'], team=r['team'])

    # ---------- Objets ----------
    tm_names = {f'TM_{m}': i + 1 for i, m in enumerate(m for k, m in d['tms'] if k == 'tm')}
    tm_names.update({f'HM_{m}': 51 + i for i, m in enumerate(m for k, m in d['tms'] if k == 'hm')})
    skip_gifts = {'MYSTERY_EGG'}  # Œuf Mystère : rendu au Prof. Orme
    # GS Ball (Cristal, distribution comptée : choix de l'utilisateur) : celle du Centre Pokémon de Doublonville ;
    # Fargas la rend après l'avoir examinée (même objet, pas une deuxième étape).
    gs_ball = 'EVENT_GOT_GS_BALL_FROM_GOLDENROD_POKEMON_CENTER'
    fix_flags = {'RAINBOW_WING': 'EVENT_GOT_RAINBOW_WING', 'PP_UP': None}  # premier drapeau trouvé faux ; Kenji : pas de drapeau
    rows_i = []
    done_flags = set()

    def item_row(kind, x, detail_extra):
        flag = x['flag']
        if x['item'] in fix_flags and kind == 2:
            flag = fix_flags[x['item']]
            if flag is None:
                return
        if flag in done_flags:
            return
        done_flags.add(flag)
        zone, detail = place(x['map'])
        detail = ', '.join(p for p in (detail, detail_extra) if p)
        tm = tm_names.get(x['item'], 0)
        rows_i.append((ZONES.index(zone), kind, ev[flag], {'zone': zone, 'detail': detail or None, 'done': event_cond(flag),
                                                              'item': 0 if tm else items[x['item']], 'tm': tm,
                                                              'count': x.get('count', 1)}))

    for x in d['ground']:
        item_row(0, x, None)
    for x in d['hidden']:
        item_row(1, x, 'caché')
    for x in d['gifts']:
        if x['item'] == 'GS_BALL' and x['flag'] != gs_ball:
            continue
        if x['item'] not in skip_gifts:
            item_row(2, x, 'distribution' if x['item'] == 'GS_BALL' else 'cadeau')
    for z, k, f, r in sorted(rows_i, key=lambda x: (x[0], x[1], x[2])):
        add(I, r['zone'], r['detail'], r['done'], item=r['item'], tm=r['tm'], count=r['count'] if r['count'] > 1 else 0)

    # ---------- Rencontres uniques ----------
    rows_u = []

    def enc(zone, detail, done, species, label=None, give=0, order=0):
        rows_u.append((ZONES.index(zone), order, {'zone': zone, 'detail': detail, 'done': done, 'species': species, 'label': label, 'give': give}))

    if crystal:
        enc(TOUR_FERRAILLE, None, event_cond('EVENT_FOUGHT_SUICUNE'), sp('SUICUNE'))
    else:
        enc(ROUTE_29, ERRANT_DANS_JOHTO, 'D:245', sp('SUICUNE'))
    enc(ROUTE_29, ERRANT_DANS_JOHTO, 'D:243', sp('RAIKOU'))
    enc(ROUTE_29, ERRANT_DANS_JOHTO, 'D:244', sp('ENTEI'))
    enc(TOUR_FERRAILLE, 'toit', event_cond('EVENT_FOUGHT_HO_OH'), sp('HO_OH'))
    enc(TOURB_ILES, 'grotte de Lugia', event_cond('EVENT_FOUGHT_LUGIA'), sp('LUGIA'))
    enc(ROUTE_36, None, event_cond('EVENT_FOUGHT_SUDOWOODO'), sp('SUDOWOODO'))
    enc(LAC_COLERE, 'chromatique', event_cond('EVENT_LAKE_OF_RAGE_RED_GYARADOS'), sp('GYARADOS'), 'Léviator rouge')
    for n in (1, 2, 3):
        enc(REPAIRE_ROCKET, SOUS_SOL_2, event_cond(f'EVENT_TEAM_ROCKET_BASE_B2F_ELECTRODE_{n}'), sp('ELECTRODE'), order=n)
    enc(CARMIN_SUR_MER, 'devant la Cave Taupiqueur', event_cond('EVENT_FOUGHT_SNORLAX'), sp('SNORLAX'))
    # Lokhlass revient chaque vendredi (drapeau remis à zéro) : compté par le Pokédex.
    enc(CAVES_JUMELLES, 'sous-sol 2, le vendredi', 'D:131', sp('LAPRAS'))
    enc('Mauville', 'Centre Pokémon, œuf', event_cond('EVENT_GOT_TOGEPI_EGG_FROM_ELMS_AIDE'), sp('TOGEPI'), "Togepi (œuf de l'assistant)")
    enc('Doublonville', 'poste de garde, cadeau', event_cond('EVENT_GOT_KENYA'), sp('SPEAROW'))
    enc('Doublonville', 'famille de Léo, cadeau', event_cond('EVENT_GOT_EEVEE'), sp('EEVEE'))
    enc('Irisia', 'maison, cadeau', event_cond('EVENT_GOT_SHUCKIE'), sp('SHUCKLE'))
    enc(MONT_CREUSET, 'sous-sol 1, cadeau', event_cond('EVENT_GOT_TYROGUE_FROM_KIYO'), sp('TYROGUE'))
    if crystal:
        enc(ANTRE_DU_DRAGON, 'sanctuaire, cadeau', event_cond('EVENT_GOT_DRATINI'), sp('DRATINI'))
        # Celebi de la GS Ball : aucun drapeau durable (le script les remet à zéro) : compté par le Pokédex.
        enc(BOIS_AUX_CHENES, 'autel, GS Ball', 'D:251', sp('CELEBI'))
    for t in d['trades']:
        zone, detail = place(t['map'])
        enc(zone, ', '.join(p for p in (detail, 'échange') if p), bit_cond(wram('wTradeFlags'), t['index']), sp(t['get']),
            give=sp(t['give']), order=10 + t['index'])
    for z, o, r in sorted(rows_u, key=lambda x: (x[0], x[1])):
        add(U, r['zone'], r['detail'], r['done'], r['label'], species=r['species'], give=r['give'])

    # ---------- Quêtes annexes ----------
    for name, flag in (('Kabuto', 'EVENT_SOLVED_KABUTO_PUZZLE'), ('Ptéra', 'EVENT_SOLVED_AERODACTYL_PUZZLE'),
                       ('Ho-Oh', 'EVENT_SOLVED_HO_OH_PUZZLE'), ('Amonita', 'EVENT_SOLVED_OMANYTE_PUZZLE')):
        add(Q, RUINES_D_ALPHA, None, event_cond(flag), f'Résoudre le puzzle {name}')
    for n in range(26):
        add(Q, RUINES_D_ALPHA, None, f'U:{off["unown"]}:{n}', f'Capturer Zarbi {chr(65 + n)}', species=sp('UNOWN'), form=n)
    siblings = [('Lucie (lundi)', 'EVENT_GOT_SHARP_BEAK_FROM_MONICA', 'Route 40'), ('Marie (mardi)', 'EVENT_GOT_PINK_BOW_FROM_TUSCANY', ROUTE_29),
                ('Homer (mercredi)', 'EVENT_GOT_BLACKBELT_FROM_WESLEY', LAC_COLERE), ('Jérôme (jeudi)', 'EVENT_GOT_HARD_STONE_FROM_ARTHUR', ROUTE_36),
                ('Vanessa (vendredi)', 'EVENT_GOT_POISON_BARB_FROM_FRIEDA', 'Route 32'), ('Samuel (samedi)', 'EVENT_GOT_SPELL_TAG_FROM_SANTOS', EBENELLE),
                ('Dimitri (dimanche)', 'EVENT_GOT_MAGNET_FROM_SUNNY', 'Route 37')]
    for who, flag, zone in siblings:
        add(Q, zone, 'Fratrie des sept jours', event_cond(flag), f'Rencontrer {who}')
    for color, apricorn in (('Rouge', 'RED'), ('Bleu', 'BLU'), ('Jaune', 'YLW'), ('Vert', 'GRN'), ('Blanc', 'WHT'),
                            ('Noir', 'BLK'), ('Rose', 'PNK')):
        add(Q, ECORCIA, 'Fargas', event_cond(f'EVENT_GAVE_KURT_{apricorn}_APRICORN'), f'Faire une Ball avec un Noigrume {color}')
    # L'Écaillerouge contre le Multi Exp n'a pas de drapeau (M. Pokémon regarde le sac) : laissé de côté.
    quests = [('Route 39', "Soigner l'Écrémeuh malade", 'EVENT_HEALED_MOOMOO', RANCH_MEUMEU),
              (ROUTE_25, "Montrer les 5 Pokémon au grand-père de Léo", 'EVENT_GOT_THUNDERSTONE_FROM_BILLS_GRANDPA', 'Cottage de Léo'),
              (ROUTE_25, 'Battre les dresseurs du pont', 'EVENT_CLEARED_NUGGET_BRIDGE', None),
              (CARMIN_SUR_MER, 'Retrouver la Poupée de Copieuse', 'EVENT_GOT_LOST_ITEM_FROM_FAN_CLUB', 'Club des fans'),
              ('Safrania', 'Rendre la poupée à Copieuse (Passe du Train Magnet)', 'EVENT_GOT_PASS_FROM_COPYCAT', MAISON_DE_COPIEUSE),
              (L_AQUARIA, 'Retrouver la petite-fille du vieil homme', 'EVENT_GOT_METAL_COAT_FROM_GRANDPA_ON_SS_AQUA', None)]
    for zone, label, flag, detail in quests:
        if flag in ev:
            add(Q, zone, detail, event_cond(flag), label)
        else:
            print(f'{game} : quête sans drapeau, laissée de côté : {label} ({flag})')
    return rows


def emit(games, tm_moves, out):
    lines = ['// <auto-generated>',
             "// Généré par outils/completion_gen2.py d'après les désassemblages pokegold et pokecrystal (pret) : ne pas modifier",
             '// à la main, changer le script et le relancer.',
             '// </auto-generated>',
             '',
             'namespace MonsutaPotaru.Services;',
             '',
             'public static class Gen2CompletionData',
             '{',
             '    /// <summary>Capacité de chaque CT (1 à 50), puis de chaque CS (1 à 7) : numéros nationaux des capacités.</summary>',
             '    public static readonly ushort[] TmMoves = [' + ', '.join(str(m) for m in tm_moves) + '];',
             '']
    for name, doc, rows in games:
        lines.extend([
            f'    /// <summary>Étapes de {doc} (le Pokédex est ajouté à part : les 251).</summary>',
            f'    public static readonly CompletionRow[] {name} =',
            '    [',
        ])
        for r in rows:
            a = [f'CompletionSection.{r["section"]}', cs(r['zone']), cs(r['detail']), cs(r['done'])]
            n = []
            if r['label']:
                n.append(f'Label: {cs(r["label"])}')
            if r['missed']:
                n.append(f'Missed: {cs(r["missed"])}')
            if r['team']:
                n.append(f'Team: {cs(r["team"])}')
            if r['item']:
                n.append(f'Item: {r["item"]}')
            if r['tm']:
                n.append(f'Tm: {r["tm"]}')
            if r['count']:
                n.append(f'Count: {r["count"]}')
            if r['form']:
                n.append(f'Form: {r["form"]}')
            if r['species']:
                n.append(f'Species: {r["species"]}')
            if r['give']:
                n.append(f'Give: {r["give"]}')
            lines.append(f'        new({", ".join(a + n)}),')
        lines.append('    ];')
        lines.append('')
    lines[-1] = '}'
    with open(out, 'w', encoding='utf-8', newline='\n') as f:
        f.write('﻿' + '\n'.join(lines) + '\n')


if __name__ == '__main__':
    root, out = sys.argv[1], sys.argv[2]
    gold = extract(os.path.join(root, 'pokegold'))
    crystal = extract(os.path.join(root, 'pokecrystal'))
    games = [('GoldSilver', 'Or et Argent', build(gold, 'pokegold')), ('Crystal', 'Cristal', build(crystal, 'pokecrystal'))]
    moves = [crystal['moves'][m] for kind in ('tm', 'hm') for k, m in crystal['tms'] if k == kind]
    emit(games, moves, out)
    for name, _, rows in games:
        counts = {}
        for r in rows:
            counts[r['section']] = counts.get(r['section'], 0) + 1
        print(name, counts, 'ratés possibles', sum(1 for r in rows if r['missed']))
