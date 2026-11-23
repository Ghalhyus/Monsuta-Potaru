"""
Génère MonsutaPotaru/Services/Completion/Gen1CompletionData.cs d'après les désassemblages pokered et pokeyellow (pret).

    python outils/completion_gen1.py <dossier contenant pokered et pokeyellow> <fichier .cs à écrire>

Les deux dépôts : git clone --depth 1 https://github.com/pret/pokered.git (et pokeyellow). Les lieux en français, l'ordre
de l'aventure et les étapes choisies à la main (histoire, cadeaux, rencontres) sont dans ce script ; le reste (dresseurs,
objets au sol, objets cachés, échanges) est lu dans les dépôts.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from completion_gen1_extract import extract  # noqa: E402

# Textes répétés (lieux, libellés)
ARENE = 'Arène'
BOURG_PALETTE = 'Bourg Palette'
CARMIN_SUR_MER = 'Carmin sur Mer'
CAVERNE_AZUREE = 'Caverne Azurée'
CELADOPOLE = 'Céladopole'
CRAMOIS_ILE = "Cramois'Île"
ILES_ECUME = 'Îles Écume'
JESSIE_ET_JAMES = 'Jessie et James'
LABO_POKEMON = 'Labo Pokémon'
MAISON_DU_PECHEUR = 'maison du pêcheur'
MANOIR_POKEMON = 'Manoir Pokémon'
MONT_SELENITE = 'Mont Sélénite'
N_1ER_ETAGE = '1er étage'
N_2E_ETAGE = '2e étage'
N_6E_ETAGE = '6e étage'
PARC_SAFARI = 'Parc Safari'
POSTE_DE_GARDE = 'poste de garde'
REPAIRE_ROCKET = 'Repaire Rocket'
REZ_DE_CHAUSSEE = 'rez-de-chaussée'
ROUTE_11 = 'Route 11'
ROUTE_12 = 'Route 12'
ROUTE_15 = 'Route 15'
ROUTE_16 = 'Route 16'
ROUTE_18 = 'Route 18'
ROUTE_2 = 'Route 2'
ROUTE_22 = 'Route 22'
ROUTE_24 = 'Route 24'
ROUTE_25 = 'Route 25'
ROUTE_4 = 'Route 4'
ROUTE_VICTOIRE = 'Route Victoire'
SOUS_SOL_1 = 'sous-sol 1'
SOUS_SOL_2 = 'sous-sol 2'
SOUS_SOL_4 = 'sous-sol 4'
SYLPHE_SARL = 'Sylphe SARL'
S_S_ANNE = 'S.S. Anne'
TOUR_POKEMON = 'Tour Pokémon'

# Octets de la sauvegarde (version internationale) : début des données principales 0x25A3 = wPokedexOwned.
HIDDEN_ITEMS = 0x299C   # wObtainedHiddenItemsFlags
HIDDEN_COINS = 0x29AA   # wObtainedHiddenCoinsFlags
STATUS_FLAGS1 = 0x29D4  # wStatusFlags1 : cannes (bits 3, 4, 5)
STATUS_FLAGS4 = 0x29DA  # wStatusFlags4 : Lokhlass de la Sylphe SARL (bit 0)
TRADES = 0x29E3         # wCompletedInGameTradeFlags

# Lieux : clé normalisée (sans « _ », en majuscules) → (zone, précision). L'ordre de ZONES est celui de l'aventure.
ZONES = [
    BOURG_PALETTE, 'Route 1', 'Jadielle', ROUTE_22, ROUTE_2, 'Forêt de Jade', 'Argenta', 'Route 3', MONT_SELENITE,
    ROUTE_4, 'Azuria', ROUTE_24, ROUTE_25, 'Route 5', 'Souterrain', 'Route 6', CARMIN_SUR_MER, S_S_ANNE,
    ROUTE_11, 'Cave Taupiqueur', 'Route 9', 'Route 10', 'Grotte', 'Lavanville', TOUR_POKEMON, 'Route 8', 'Route 7',
    CELADOPOLE, REPAIRE_ROCKET, ROUTE_16, 'Route 17', ROUTE_18, ROUTE_12, 'Route 13', 'Route 14', ROUTE_15,
    'Parmanie', PARC_SAFARI, 'Safrania', SYLPHE_SARL, 'Route 19', 'Route 20', ILES_ECUME, CRAMOIS_ILE,
    MANOIR_POKEMON, 'Route 21', 'Centrale', 'Route 23', ROUTE_VICTOIRE, 'Plateau Indigo', CAVERNE_AZUREE,
]

PLACES = {
    'PALLETTOWN': (BOURG_PALETTE, None), 'OAKSLAB': (BOURG_PALETTE, 'Labo du Prof. Chen'),
    'BLUESHOUSE': (BOURG_PALETTE, 'maison du rival'),
    'ROUTE1': ('Route 1', None), 'VIRIDIANCITY': ('Jadielle', None), 'VIRIDIANGYM': ('Jadielle', ARENE),
    'VIRIDIANMART': ('Jadielle', 'Boutique'), 'ROUTE22': (ROUTE_22, None), 'ROUTE2': (ROUTE_2, None),
    'ROUTE2GATE': (ROUTE_2, POSTE_DE_GARDE), 'VIRIDIANFOREST': ('Forêt de Jade', None),
    'PEWTERGYM': ('Argenta', ARENE), 'MUSEUM1F': ('Argenta', 'Musée'), 'ROUTE3': ('Route 3', None),
    'MTMOON1F': (MONT_SELENITE, REZ_DE_CHAUSSEE), 'MTMOONB1F': (MONT_SELENITE, SOUS_SOL_1),
    'MTMOONB2F': (MONT_SELENITE, SOUS_SOL_2), 'MTMOONPOKECENTER': (ROUTE_4, 'Centre Pokémon'),
    'ROUTE4': (ROUTE_4, None), 'CERULEANCITY': ('Azuria', None), 'CERULEANGYM': ('Azuria', ARENE),
    'CERULEANMELANIESHOUSE': ('Azuria', 'maison de Mélanie'), 'BIKESHOP': ('Azuria', 'Magasin de vélos'),
    'ROUTE24': (ROUTE_24, None), 'ROUTE25': (ROUTE_25, None), 'BILLSHOUSE': (ROUTE_25, 'Cottage de Léo'),
    'UNDERGROUNDPATHNORTHSOUTH': ('Souterrain', 'nord-sud'), 'UNDERGROUNDPATHWESTEAST': ('Souterrain', 'est-ouest'),
    'ROUTE6': ('Route 6', None), 'VERMILIONCITY': (CARMIN_SUR_MER, None), 'VERMILIONGYM': (CARMIN_SUR_MER, ARENE),
    'POKEMONFANCLUB': (CARMIN_SUR_MER, 'Club des fans'),
    'SSANNE1FROOMS': (S_S_ANNE, 'cabines du pont 1'), 'SSANNE2FROOMS': (S_S_ANNE, 'cabines du pont 2'),
    'SSANNEB1FROOMS': (S_S_ANNE, 'cabines de la cale'), 'SSANNEBOW': (S_S_ANNE, 'proue'),
    'SSANNEKITCHEN': (S_S_ANNE, 'cuisines'), 'SSANNECAPTAINSROOM': (S_S_ANNE, 'cabine du capitaine'),
    'ROUTE11': (ROUTE_11, None), 'ROUTE11GATE2F': (ROUTE_11, POSTE_DE_GARDE), 'ROUTE9': ('Route 9', None),
    'ROUTE10': ('Route 10', None), 'ROCKTUNNEL1F': ('Grotte', REZ_DE_CHAUSSEE), 'ROCKTUNNELB1F': ('Grotte', SOUS_SOL_1),
    'MRFUJISHOUSE': ('Lavanville', 'maison de M. Fuji'),
    'POKEMONTOWER3F': (TOUR_POKEMON, N_2E_ETAGE), 'POKEMONTOWER4F': (TOUR_POKEMON, '3e étage'),
    'POKEMONTOWER5F': (TOUR_POKEMON, '4e étage'), 'POKEMONTOWER6F': (TOUR_POKEMON, '5e étage'),
    'POKEMONTOWER7F': (TOUR_POKEMON, N_6E_ETAGE), 'ROUTE8': ('Route 8', None),
    'CELADONCITY': (CELADOPOLE, None), 'CELADONGYM': (CELADOPOLE, ARENE),
    'CELADONMART3F': (CELADOPOLE, 'Centre commercial'), 'CELADONMARTROOF': (CELADOPOLE, 'toit du Centre commercial'),
    'CELADONDINER': (CELADOPOLE, 'restaurant'), 'GAMECORNER': (CELADOPOLE, 'Casino'),
    'CELADONMANSIONROOFHOUSE': (CELADOPOLE, 'toit du Manoir'),
    'ROCKETHIDEOUTB1F': (REPAIRE_ROCKET, SOUS_SOL_1), 'ROCKETHIDEOUTB2F': (REPAIRE_ROCKET, SOUS_SOL_2),
    'ROCKETHIDEOUTB3F': (REPAIRE_ROCKET, 'sous-sol 3'), 'ROCKETHIDEOUTB4F': (REPAIRE_ROCKET, SOUS_SOL_4),
    'ROUTE16': (ROUTE_16, None), 'ROUTE16FLYHOUSE': (ROUTE_16, 'maison'), 'ROUTE17': ('Route 17', None),
    'ROUTE18': (ROUTE_18, None), 'ROUTE18GATE2F': (ROUTE_18, POSTE_DE_GARDE),
    'ROUTE12': (ROUTE_12, None), 'ROUTE12GATE2F': (ROUTE_12, POSTE_DE_GARDE), 'ROUTE13': ('Route 13', None),
    'ROUTE14': ('Route 14', None), 'ROUTE15': (ROUTE_15, None), 'ROUTE15GATE2F': (ROUTE_15, POSTE_DE_GARDE),
    'FUCHSIAGYM': ('Parmanie', ARENE), 'WARDENSHOUSE': ('Parmanie', 'maison du Gardien'),
    'SAFARIZONEGATE': (PARC_SAFARI, 'entrée'), 'SAFARIZONECENTER': (PARC_SAFARI, 'centre'),
    'SAFARIZONEEAST': (PARC_SAFARI, 'est'), 'SAFARIZONENORTH': (PARC_SAFARI, 'nord'),
    'SAFARIZONEWEST': (PARC_SAFARI, 'ouest'), 'SAFARIZONESECRETHOUSE': (PARC_SAFARI, 'maison secrète'),
    'SAFFRONGYM': ('Safrania', ARENE), 'FIGHTINGDOJO': ('Safrania', 'Dojo'),
    'COPYCATSHOUSE2F': ('Safrania', 'maison de Copieuse'), 'MRPSYCHICSHOUSE': ('Safrania', 'maison de M. Psy'),
    'SILPHCO2F': (SYLPHE_SARL, N_1ER_ETAGE), 'SILPHCO3F': (SYLPHE_SARL, N_2E_ETAGE), 'SILPHCO4F': (SYLPHE_SARL, '3e étage'),
    'SILPHCO5F': (SYLPHE_SARL, '4e étage'), 'SILPHCO6F': (SYLPHE_SARL, '5e étage'), 'SILPHCO7F': (SYLPHE_SARL, N_6E_ETAGE),
    'SILPHCO8F': (SYLPHE_SARL, '7e étage'), 'SILPHCO9F': (SYLPHE_SARL, '8e étage'), 'SILPHCO10F': (SYLPHE_SARL, '9e étage'),
    'SILPHCO11F': (SYLPHE_SARL, '10e étage'),
    'ROUTE19': ('Route 19', None), 'ROUTE20': ('Route 20', None), 'SEAFOAMISLANDSB2F': (ILES_ECUME, SOUS_SOL_2),
    'SEAFOAMISLANDSB3F': (ILES_ECUME, 'sous-sol 3'), 'SEAFOAMISLANDSB4F': (ILES_ECUME, SOUS_SOL_4),
    'CINNABARGYM': (CRAMOIS_ILE, ARENE), 'CINNABARLABMETRONOMEROOM': (CRAMOIS_ILE, LABO_POKEMON),
    'CINNABARLABFOSSILROOM': (CRAMOIS_ILE, LABO_POKEMON), 'CINNABARLABTRADEROOM': (CRAMOIS_ILE, LABO_POKEMON),
    'POKEMONMANSION1F': (MANOIR_POKEMON, REZ_DE_CHAUSSEE), 'POKEMONMANSION2F': (MANOIR_POKEMON, N_1ER_ETAGE),
    'POKEMONMANSION3F': (MANOIR_POKEMON, N_2E_ETAGE), 'POKEMONMANSIONB1F': (MANOIR_POKEMON, 'sous-sol'),
    'ROUTE21': ('Route 21', None), 'POWERPLANT': ('Centrale', None), 'ROUTE23': ('Route 23', None),
    'VICTORYROAD1F': (ROUTE_VICTOIRE, REZ_DE_CHAUSSEE), 'VICTORYROAD2F': (ROUTE_VICTOIRE, N_1ER_ETAGE),
    'VICTORYROAD3F': (ROUTE_VICTOIRE, N_2E_ETAGE),
    'CERULEANCAVE1F': (CAVERNE_AZUREE, REZ_DE_CHAUSSEE), 'CERULEANCAVE2F': (CAVERNE_AZUREE, N_1ER_ETAGE),
    'CERULEANCAVEB1F': (CAVERNE_AZUREE, 'sous-sol'),
    'VERMILIONTRADEHOUSE': (CARMIN_SUR_MER, 'maison'), 'CERULEANTRADEHOUSE': ('Azuria', 'maison'),
    'ROUTE2TRADEHOUSE': (ROUTE_2, 'maison'), 'UNDERGROUNDPATHROUTE5': ('Route 5', 'entrée du Souterrain'),
    'VERMILIONOLDRODHOUSE': (CARMIN_SUR_MER, MAISON_DU_PECHEUR),
    'FUCHSIAGOODRODHOUSE': ('Parmanie', MAISON_DU_PECHEUR), 'ROUTE12SUPERRODHOUSE': (ROUTE_12, MAISON_DU_PECHEUR),
}

# Cartes inaccessibles en jouant : leurs objets ne comptent pas.
UNUSED = {'UNUSEDMAP6F'}
# Salles du Conseil 4 : leurs drapeaux sont remis à zéro après le Panthéon (le Panthéon compte dans l'Histoire).
ELITE_FOUR = {'LORELEISROOM', 'BRUNOSROOM', 'AGATHASROOM', 'LANCESROOM'}


def key(name):
    """« MT_MOON_B2F », « MtMoonB2F », « BillsHouse_2 » → « MTMOONB2F »."""
    if name != name.upper():  # nom de fichier de script : « BillsHouse_2 » est la suite de « BillsHouse » (Jaune)
        name = re.sub(r'_2$', '', name)
    return name.replace('_', '').upper()


def place(name):
    k = key(name)
    if k not in PLACES:
        raise SystemExit(f'Lieu sans nom français : {name} ({k})')
    return PLACES[k]


def cs(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"' if s is not None else 'null'


class Rows:
    def __init__(self):
        self.entries = []

    def add(self, section, zone, detail, done, label=None, missed=None, team=None, item=0, coins=0, species=0, give=0, order=None):
        if zone not in ZONES:
            raise SystemExit(f'Zone inconnue : {zone}')
        self.entries.append({'section': section, 'zone': zone, 'detail': detail, 'done': done, 'label': label, 'missed': missed, 'team': team,
                              'item': item, 'coins': coins, 'species': species, 'give': give, 'order': len(self.entries) if order is None else order})


def build(d, yellow):
    ev = d['events']
    dex = d['dex']
    items = d['items']

    def event_cond(name):
        if name not in ev:
            raise SystemExit(f'Drapeau inconnu dans {"Jaune" if yellow else "Rouge / Bleu"} : {name}')
        return f'E:{ev[name]}'

    def toggle_cond(name):
        return f'T:{d["toggles"].index(name)}'

    def bit_cond(offset, bit):
        return f'R:{offset + bit // 8}:{bit % 8}'

    def sp(name):
        return dex['DEX_' + name]

    rows = Rows()
    S, _, D, I, U = 'Story', 'Pokedex', 'Trainers', 'Items', 'Encounters'

    # ---------- Histoire ----------
    badges = [('Badge Roche', 'Pierre', 'Argenta'), ('Badge Cascade', 'Ondine', 'Azuria'),
              ('Badge Foudre', 'Major Bob', CARMIN_SUR_MER), ('Badge Prisme', 'Érika', CELADOPOLE),
              ('Badge Âme', 'Koga', 'Parmanie'), ('Badge Marais', 'Morgane', 'Safrania'),
              ('Badge Volcan', 'Auguste', CRAMOIS_ILE), ('Badge Terre', 'Giovanni', 'Jadielle')]
    rows.add(S, BOURG_PALETTE, None, event_cond('EVENT_GOT_STARTER'), 'Recevoir son premier Pokémon du Prof. Chen')
    rows.add(S, BOURG_PALETTE, None, event_cond('EVENT_OAK_GOT_PARCEL'), 'Apporter le colis de Jadielle au Prof. Chen')
    rows.add(S, BOURG_PALETTE, None, event_cond('EVENT_GOT_POKEDEX'), 'Recevoir le Pokédex')
    story = [(0, None), (1, None), (None, (ROUTE_25, 'Aider Léo au Cottage de Léo', event_cond('EVENT_GOT_SS_TICKET'))), (2, None),
             (None, (REPAIRE_ROCKET, 'Vaincre Giovanni au Repaire Rocket', event_cond('EVENT_BEAT_ROCKET_HIDEOUT_GIOVANNI'))),
             (None, (TOUR_POKEMON, 'Apaiser l\'esprit d\'Ossatueur', event_cond('EVENT_BEAT_GHOST_MAROWAK'))),
             (None, (TOUR_POKEMON, 'Délivrer M. Fuji', event_cond('EVENT_RESCUED_MR_FUJI'))), (3, None), (4, None),
             (None, (SYLPHE_SARL, 'Libérer la Sylphe SARL (Giovanni vaincu)', event_cond('EVENT_BEAT_SILPH_CO_GIOVANNI'))),
             (5, None), (6, None), (7, None),
             (None, ('Plateau Indigo', 'Vaincre la Ligue Pokémon et entrer au Panthéon', 'F'))]
    for badge, extra in story:
        if badge is not None:
            name, leader, city = badges[badge]
            rows.add(S, city, ARENE, f'B:{badge}', f'{name} ({leader})')
        else:
            rows.add(S, extra[0], None, extra[2], extra[1])
    if yellow:
        rows.add(S, BOURG_PALETTE, None, 'P', 'Amitié de Pikachu au maximum')

    # ---------- Dresseurs ----------
    rival = [(None, BOURG_PALETTE, 'Labo du Prof. Chen', event_cond('EVENT_BATTLED_RIVAL_IN_OAKS_LAB'), None),
             (None, ROUTE_22, 'premier combat', event_cond('EVENT_BEAT_ROUTE22_RIVAL_1ST_BATTLE'), event_cond('EVENT_BEAT_BROCK')),
             (None, 'Azuria', None, event_cond('EVENT_BEAT_CERULEAN_RIVAL'), None),
             # Le S.S. Anne n'a pas de drapeau pour ce combat : il barre l'escalier de la cabine du capitaine (CS01).
             (None, S_S_ANNE, 'pont 2', event_cond('EVENT_GOT_HM01'), None),
             (None, TOUR_POKEMON, N_1ER_ETAGE, event_cond('EVENT_BEAT_POKEMON_TOWER_RIVAL'), None),
             (None, SYLPHE_SARL, N_6E_ETAGE, event_cond('EVENT_BEAT_SILPH_CO_RIVAL'), None),
             (None, ROUTE_22, 'second combat', event_cond('EVENT_BEAT_ROUTE22_RIVAL_2ND_BATTLE'), None)]
    trainer_rows = []
    for _, zone, detail, done, missed in rival:
        trainer_rows.append((ZONES.index(zone), 0, {'zone': zone, 'detail': detail, 'done': done, 'label': 'Rival', 'missed': missed}))
    specials = [('Azuria', None, 'EVENT_BEAT_CERULEAN_ROCKET_THIEF', 'Voleur de la Team Rocket'),
                (ROUTE_24, 'Pont Pépite', 'EVENT_BEAT_ROUTE24_ROCKET', 'Sbire de la Team Rocket'),
                (MONT_SELENITE, SOUS_SOL_2, 'EVENT_BEAT_MT_MOON_EXIT_SUPER_NERD', 'Gardien des fossiles'),
                ('Safrania', 'Dojo', 'EVENT_BEAT_KARATE_MASTER', 'Maître Karaté')]
    if yellow:
        specials += [(MONT_SELENITE, SOUS_SOL_2, 'EVENT_BEAT_MT_MOON_3_JESSIE_JAMES', JESSIE_ET_JAMES),
                     (REPAIRE_ROCKET, SOUS_SOL_4, 'EVENT_BEAT_ROCKET_HIDEOUT_4_JESSIE_JAMES', JESSIE_ET_JAMES),
                     (TOUR_POKEMON, N_6E_ETAGE, 'EVENT_BEAT_POKEMONTOWER_7_JESSIE_JAMES', JESSIE_ET_JAMES),
                     (SYLPHE_SARL, '10e étage', 'EVENT_BEAT_SILPH_CO_11F_JESSIE_JAMES', JESSIE_ET_JAMES)]
    for zone, detail, name, label in specials:
        trainer_rows.append((ZONES.index(zone), ev[name], {'zone': zone, 'detail': detail, 'done': event_cond(name), 'label': label, 'missed': None}))
    statics = []
    for t in d['trainers']:
        k = key(t['file'])
        if k in ELITE_FOUR:
            continue
        if t.get('species'):
            statics.append(t)
            continue
        zone, detail = place(t['file'])
        missed = event_cond('EVENT_BEAT_SILPH_CO_GIOVANNI') if t['event'].startswith('EVENT_BEAT_SILPH_CO_') else None
        trainer_rows.append((ZONES.index(zone), t['flag'], {'zone': zone, 'detail': detail, 'done': f'E:{t["flag"]}',
                                                               'team': ','.join(f'{sp(s)}:{lv}' for lv, s in t['team']),
                                                               'missed': missed}))
    # Arène de Cramois'Île : pas d'en-tête de dresseur (quiz), ses dresseurs dans l'ordre des objets.
    for i, t in enumerate(d['cinnabar']):
        name = f'EVENT_BEAT_CINNABAR_GYM_TRAINER_{i}'
        trainer_rows.append((ZONES.index(CRAMOIS_ILE), ev[name], {'zone': CRAMOIS_ILE, 'detail': ARENE, 'done': event_cond(name),
                                                                        'team': ','.join(f'{sp(s)}:{lv}' for lv, s in t),
                                                                        'missed': None}))
    for z, flag, r in sorted(trainer_rows, key=lambda x: (x[0], x[1])):
        rows.add(D, r['zone'], r['detail'], r['done'], r.get('label'), missed=r['missed'], team=r.get('team'))

    # ---------- Objets ----------
    item_rows = []
    for g in d['ground']:
        if key(g['map']) in UNUSED:
            continue
        zone, detail = place(g['map'])
        done = toggle_cond(g['toggle'])
        if g['initial'] == 'OFF':  # n'apparaît qu'après un événement (Scope Sylphe, Clé Ascenseur)
            done += '+' + (event_cond('EVENT_BEAT_ROCKET_HIDEOUT_GIOVANNI') if g['item'] == 'SILPH_SCOPE'
                           else event_cond('EVENT_ROCKET_DROPPED_LIFT_KEY'))
        item_rows.append((ZONES.index(zone), 0, g['flag'], {'zone': zone, 'detail': detail, 'done': done, 'item': g['itemId']}))
    for h in d['hidden']:
        if key(h['map']) in UNUSED:
            continue
        zone, detail = place(h['map'])
        item_rows.append((ZONES.index(zone), 1, h['flag'], {'zone': zone, 'detail': ', '.join(x for x in (detail, 'caché') if x),
                                                                'done': bit_cond(HIDDEN_ITEMS, h['flag']), 'item': h['itemId']}))
    for c in d['coins']:
        zone, detail = place(c['map'])
        item_rows.append((ZONES.index(zone), 1, c['flag'], {'zone': zone, 'detail': ', '.join(x for x in (detail, 'cachés') if x),
                                                                'done': bit_cond(HIDDEN_COINS, c['flag']), 'coins': c['coins']}))
    gifts = d['gifts']
    for name, (script, extra) in gifts.items():
        zone, detail = place(script)
        m = re.match(r'EVENT_GOT_(TM|HM)(\d+)', name)
        coins = 0
        item = 0
        label = None
        if m:
            item = items[f'{m.group(1)}_{d["tm_moves"][m.group(1)][int(m.group(2)) - 1]}']
        elif name in ('EVENT_GOT_10_COINS', 'EVENT_GOT_20_COINS', 'EVENT_GOT_20_COINS_2'):
            coins = 10 if '10' in name else 20
        elif name == 'EVENT_GOT_POTION_SAMPLE':
            item = items['POTION']
        elif name == 'EVENT_GOT_POKEBALLS_FROM_OAK':
            item = items['POKE_BALL']
            label = 'Poké Balls du Prof. Chen'
        else:
            item = items[name[len('EVENT_GOT_'):]]
        item_rows.append((ZONES.index(zone), 2, ev[name], {'zone': zone, 'detail': ', '.join(x for x in (detail, 'cadeau') if x),
                                                               'done': event_cond(name), 'item': item, 'coins': coins, 'label': label}))
    # Fossile Dôme ou Nautile : un seul des deux, au choix.
    item_rows.append((ZONES.index(MONT_SELENITE), 2, 0, {'zone': MONT_SELENITE, 'detail': 'sous-sol 2, au choix',
                                                               'done': event_cond('EVENT_GOT_DOME_FOSSIL') + '|' + event_cond('EVENT_GOT_HELIX_FOSSIL'),
                                                               'label': 'Fossile Dôme ou Nautile'}))
    for bit, item, house in ((3, 'OLD_ROD', 'VermilionOldRodHouse'), (4, 'GOOD_ROD', 'FuchsiaGoodRodHouse'),
                             (5, 'SUPER_ROD', 'Route12SuperRodHouse')):
        zone, detail = place(house)
        item_rows.append((ZONES.index(zone), 2, 0, {'zone': zone, 'detail': detail + ', cadeau', 'done': bit_cond(STATUS_FLAGS1, bit),
                                                        'item': items[item]}))
    for z, kind, flag, r in sorted(item_rows, key=lambda x: (x[0], x[1], x[2])):
        rows.add(I, r['zone'], r['detail'], r['done'], r.get('label'), item=r.get('item', 0), coins=r.get('coins', 0))

    # ---------- Rencontres uniques ----------
    enc = []
    for t in statics:
        zone, detail = place(t['file'])
        enc.append((ZONES.index(zone), t['flag'], {'zone': zone, 'detail': detail, 'done': f'E:{t["flag"]}', 'species': sp(t['species']),
                                                       'label': None}))
    enc.extend([
        (ZONES.index(ROUTE_12), 0, {'zone': ROUTE_12, 'detail': None, 'done': event_cond('EVENT_BEAT_ROUTE12_SNORLAX'),
                                                'species': sp('SNORLAX'), 'label': None}),
        (ZONES.index(ROUTE_16), 0, {'zone': ROUTE_16, 'detail': None, 'done': event_cond('EVENT_BEAT_ROUTE16_SNORLAX'),
                                                'species': sp('SNORLAX'), 'label': None}),
        (ZONES.index(ROUTE_4), 1, {'zone': ROUTE_4, 'detail': 'Centre Pokémon, acheté', 'done': event_cond('EVENT_BOUGHT_MAGIKARP'),
                                               'species': sp('MAGIKARP'), 'label': None}),
        (ZONES.index(CELADOPOLE), 1, {'zone': CELADOPOLE, 'detail': 'toit du Manoir, cadeau',
                                                  'done': toggle_cond('TOGGLE_CELADON_MANSION_EEVEE_GIFT'), 'species': sp('EEVEE'), 'label': None}),
        (ZONES.index('Safrania'), 1, {'zone': 'Safrania', 'detail': 'Dojo, au choix',
                                                'done': event_cond('EVENT_GOT_HITMONLEE') + '|' + event_cond('EVENT_GOT_HITMONCHAN'),
                                                'species': sp('HITMONLEE'), 'label': 'Kicklee ou Tygnon'}),
        (ZONES.index(SYLPHE_SARL), 1, {'zone': SYLPHE_SARL, 'detail': '6e étage, cadeau', 'done': bit_cond(STATUS_FLAGS4, 0),
                                                   'species': sp('LAPRAS'), 'label': None}),
    ])
    if yellow:
        enc.extend([
            (ZONES.index('Azuria'), 1, {'zone': 'Azuria', 'detail': 'maison de Mélanie, cadeau',
                                                  'done': event_cond('EVENT_GOT_BULBASAUR_IN_CERULEAN'), 'species': sp('BULBASAUR'), 'label': None}),
            (ZONES.index(ROUTE_24), 1, {'zone': ROUTE_24, 'detail': 'cadeau', 'done': event_cond('EVENT_GOT_CHARMANDER_ON_ROUTE_24'),
                                                    'species': sp('CHARMANDER'), 'label': None}),
            (ZONES.index(CARMIN_SUR_MER), 1, {'zone': CARMIN_SUR_MER, 'detail': 'cadeau de l\'Agente Jenny',
                                                          'done': event_cond('EVENT_GOT_SQUIRTLE_FROM_OFFICER_JENNY'),
                                                          'species': sp('SQUIRTLE'), 'label': None}),
        ])
    for t in d['trades']:
        m = re.match(r'used in (\w+)', t['where'])
        if not m:
            continue
        zone, detail = place(m.group(1))
        enc.append((ZONES.index(zone), 2, {'zone': zone, 'detail': ', '.join(x for x in (detail, 'échange') if x),
                                              'done': bit_cond(TRADES, t['index']), 'species': sp(t['get']), 'label': None,
                                              'give': sp(t['give'])}))
    for z, o, r in sorted(enc, key=lambda x: (x[0], x[1])):
        rows.add(U, r['zone'], r['detail'], r['done'], r['label'], species=r['species'], give=r.get('give', 0))
    return rows.entries


def emit(rows_rb, rows_y, tm_moves, out):
    lines = ['// <auto-generated>',
             '// Généré par outils/completion_gen1.py d\'après les désassemblages pokered et pokeyellow (pret) : ne pas modifier',
             '// à la main, changer le script et le relancer.',
             '// </auto-generated>',
             '',
             'namespace MonsutaPotaru.Services;',
             '',
             'public static class Gen1CompletionData',
             '{',
             '    /// <summary>Capacité de chaque CT (1 à 50), puis de chaque CS (1 à 5) : numéros nationaux des capacités.</summary>',
             '    public static readonly ushort[] TmMoves = [' + ', '.join(str(m) for m in tm_moves) + '];',
             '']
    for name, doc, rows in (('RedBlue', 'Rouge et Bleu', rows_rb), ('Yellow', 'Jaune', rows_y)):
        lines.extend([
            f'    /// <summary>Étapes de {doc} (le Pokédex est ajouté à part : les 151).</summary>',
            f'    public static readonly CompletionRow[] {name} =',
            '    [',
        ])
        for r in rows:
            args = [f'CompletionSection.{r["section"]}', cs(r['zone']), cs(r['detail']), cs(r['done'])]
            named = []
            if r['label']:
                named.append(f'Label: {cs(r["label"])}')
            if r['missed']:
                named.append(f'Missed: {cs(r["missed"])}')
            if r['team']:
                named.append(f'Team: {cs(r["team"])}')
            if r['item']:
                named.append(f'Item: {r["item"]}')
            if r['coins']:
                named.append(f'Coins: {r["coins"]}')
            if r['species']:
                named.append(f'Species: {r["species"]}')
            if r['give']:
                named.append(f'Give: {r["give"]}')
            lines.append(f'        new({", ".join(args + named)}),')
        lines.append('    ];')
        lines.append('')
    lines[-1] = '}'
    with open(out, 'w', encoding='utf-8', newline='\n') as f:
        f.write('﻿' + '\n'.join(lines) + '\n')


if __name__ == '__main__':
    root, out = sys.argv[1], sys.argv[2]
    rb = extract(os.path.join(root, 'pokered'))
    y = extract(os.path.join(root, 'pokeyellow'))
    rows_rb = build(rb, False)
    rows_y = build(y, True)
    moves = [rb['moves'][m] for kind in ('tm', 'hm') for k, m in rb['tms'] if k == kind]
    emit(rows_rb, rows_y, moves, out)
    for name, rows in (('Rouge / Bleu', rows_rb), ('Jaune', rows_y)):
        counts = {}
        for r in rows:
            counts[r['section']] = counts.get(r['section'], 0) + 1
        print(name, counts)
