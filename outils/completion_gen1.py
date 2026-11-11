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

# Octets de la sauvegarde (version internationale) : début des données principales 0x25A3 = wPokedexOwned.
HIDDEN_ITEMS = 0x299C   # wObtainedHiddenItemsFlags
HIDDEN_COINS = 0x29AA   # wObtainedHiddenCoinsFlags
STATUS_FLAGS1 = 0x29D4  # wStatusFlags1 : cannes (bits 3, 4, 5)
STATUS_FLAGS4 = 0x29DA  # wStatusFlags4 : Lokhlass de la Sylphe SARL (bit 0)
TRADES = 0x29E3         # wCompletedInGameTradeFlags

# Lieux : clé normalisée (sans « _ », en majuscules) → (zone, précision). L'ordre de ZONES est celui de l'aventure.
ZONES = [
    'Bourg Palette', 'Route 1', 'Jadielle', 'Route 22', 'Route 2', 'Forêt de Jade', 'Argenta', 'Route 3', 'Mont Sélénite',
    'Route 4', 'Azuria', 'Route 24', 'Route 25', 'Route 5', 'Souterrain', 'Route 6', 'Carmin sur Mer', 'S.S. Anne',
    'Route 11', 'Cave Taupiqueur', 'Route 9', 'Route 10', 'Grotte', 'Lavanville', 'Tour Pokémon', 'Route 8', 'Route 7',
    'Céladopole', 'Repaire Rocket', 'Route 16', 'Route 17', 'Route 18', 'Route 12', 'Route 13', 'Route 14', 'Route 15',
    'Parmanie', 'Parc Safari', 'Safrania', 'Sylphe SARL', 'Route 19', 'Route 20', 'Îles Écume', 'Cramois\'Île',
    'Manoir Pokémon', 'Route 21', 'Centrale', 'Route 23', 'Route Victoire', 'Plateau Indigo', 'Caverne Azurée',
]

PLACES = {
    'PALLETTOWN': ('Bourg Palette', None), 'OAKSLAB': ('Bourg Palette', 'Labo du Prof. Chen'),
    'BLUESHOUSE': ('Bourg Palette', 'maison du rival'),
    'ROUTE1': ('Route 1', None), 'VIRIDIANCITY': ('Jadielle', None), 'VIRIDIANGYM': ('Jadielle', 'Arène'),
    'VIRIDIANMART': ('Jadielle', 'Boutique'), 'ROUTE22': ('Route 22', None), 'ROUTE2': ('Route 2', None),
    'ROUTE2GATE': ('Route 2', 'poste de garde'), 'VIRIDIANFOREST': ('Forêt de Jade', None),
    'PEWTERGYM': ('Argenta', 'Arène'), 'MUSEUM1F': ('Argenta', 'Musée'), 'ROUTE3': ('Route 3', None),
    'MTMOON1F': ('Mont Sélénite', 'rez-de-chaussée'), 'MTMOONB1F': ('Mont Sélénite', 'sous-sol 1'),
    'MTMOONB2F': ('Mont Sélénite', 'sous-sol 2'), 'MTMOONPOKECENTER': ('Route 4', 'Centre Pokémon'),
    'ROUTE4': ('Route 4', None), 'CERULEANCITY': ('Azuria', None), 'CERULEANGYM': ('Azuria', 'Arène'),
    'CERULEANMELANIESHOUSE': ('Azuria', 'maison de Mélanie'), 'BIKESHOP': ('Azuria', 'Magasin de vélos'),
    'ROUTE24': ('Route 24', None), 'ROUTE25': ('Route 25', None), 'BILLSHOUSE': ('Route 25', 'Cottage de Léo'),
    'UNDERGROUNDPATHNORTHSOUTH': ('Souterrain', 'nord-sud'), 'UNDERGROUNDPATHWESTEAST': ('Souterrain', 'est-ouest'),
    'ROUTE6': ('Route 6', None), 'VERMILIONCITY': ('Carmin sur Mer', None), 'VERMILIONGYM': ('Carmin sur Mer', 'Arène'),
    'POKEMONFANCLUB': ('Carmin sur Mer', 'Club des fans'),
    'SSANNE1FROOMS': ('S.S. Anne', 'cabines du pont 1'), 'SSANNE2FROOMS': ('S.S. Anne', 'cabines du pont 2'),
    'SSANNEB1FROOMS': ('S.S. Anne', 'cabines de la cale'), 'SSANNEBOW': ('S.S. Anne', 'proue'),
    'SSANNEKITCHEN': ('S.S. Anne', 'cuisines'), 'SSANNECAPTAINSROOM': ('S.S. Anne', 'cabine du capitaine'),
    'ROUTE11': ('Route 11', None), 'ROUTE11GATE2F': ('Route 11', 'poste de garde'), 'ROUTE9': ('Route 9', None),
    'ROUTE10': ('Route 10', None), 'ROCKTUNNEL1F': ('Grotte', 'rez-de-chaussée'), 'ROCKTUNNELB1F': ('Grotte', 'sous-sol 1'),
    'MRFUJISHOUSE': ('Lavanville', 'maison de M. Fuji'),
    'POKEMONTOWER3F': ('Tour Pokémon', '2e étage'), 'POKEMONTOWER4F': ('Tour Pokémon', '3e étage'),
    'POKEMONTOWER5F': ('Tour Pokémon', '4e étage'), 'POKEMONTOWER6F': ('Tour Pokémon', '5e étage'),
    'POKEMONTOWER7F': ('Tour Pokémon', '6e étage'), 'ROUTE8': ('Route 8', None),
    'CELADONCITY': ('Céladopole', None), 'CELADONGYM': ('Céladopole', 'Arène'),
    'CELADONMART3F': ('Céladopole', 'Centre commercial'), 'CELADONMARTROOF': ('Céladopole', 'toit du Centre commercial'),
    'CELADONDINER': ('Céladopole', 'restaurant'), 'GAMECORNER': ('Céladopole', 'Casino'),
    'CELADONMANSIONROOFHOUSE': ('Céladopole', 'toit du Manoir'),
    'ROCKETHIDEOUTB1F': ('Repaire Rocket', 'sous-sol 1'), 'ROCKETHIDEOUTB2F': ('Repaire Rocket', 'sous-sol 2'),
    'ROCKETHIDEOUTB3F': ('Repaire Rocket', 'sous-sol 3'), 'ROCKETHIDEOUTB4F': ('Repaire Rocket', 'sous-sol 4'),
    'ROUTE16': ('Route 16', None), 'ROUTE16FLYHOUSE': ('Route 16', 'maison'), 'ROUTE17': ('Route 17', None),
    'ROUTE18': ('Route 18', None), 'ROUTE18GATE2F': ('Route 18', 'poste de garde'),
    'ROUTE12': ('Route 12', None), 'ROUTE12GATE2F': ('Route 12', 'poste de garde'), 'ROUTE13': ('Route 13', None),
    'ROUTE14': ('Route 14', None), 'ROUTE15': ('Route 15', None), 'ROUTE15GATE2F': ('Route 15', 'poste de garde'),
    'FUCHSIAGYM': ('Parmanie', 'Arène'), 'WARDENSHOUSE': ('Parmanie', 'maison du Gardien'),
    'SAFARIZONEGATE': ('Parc Safari', 'entrée'), 'SAFARIZONECENTER': ('Parc Safari', 'centre'),
    'SAFARIZONEEAST': ('Parc Safari', 'est'), 'SAFARIZONENORTH': ('Parc Safari', 'nord'),
    'SAFARIZONEWEST': ('Parc Safari', 'ouest'), 'SAFARIZONESECRETHOUSE': ('Parc Safari', 'maison secrète'),
    'SAFFRONGYM': ('Safrania', 'Arène'), 'FIGHTINGDOJO': ('Safrania', 'Dojo'),
    'COPYCATSHOUSE2F': ('Safrania', 'maison de Copieuse'), 'MRPSYCHICSHOUSE': ('Safrania', 'maison de M. Psy'),
    'SILPHCO2F': ('Sylphe SARL', '1er étage'), 'SILPHCO3F': ('Sylphe SARL', '2e étage'), 'SILPHCO4F': ('Sylphe SARL', '3e étage'),
    'SILPHCO5F': ('Sylphe SARL', '4e étage'), 'SILPHCO6F': ('Sylphe SARL', '5e étage'), 'SILPHCO7F': ('Sylphe SARL', '6e étage'),
    'SILPHCO8F': ('Sylphe SARL', '7e étage'), 'SILPHCO9F': ('Sylphe SARL', '8e étage'), 'SILPHCO10F': ('Sylphe SARL', '9e étage'),
    'SILPHCO11F': ('Sylphe SARL', '10e étage'),
    'ROUTE19': ('Route 19', None), 'ROUTE20': ('Route 20', None), 'SEAFOAMISLANDSB2F': ('Îles Écume', 'sous-sol 2'),
    'SEAFOAMISLANDSB3F': ('Îles Écume', 'sous-sol 3'), 'SEAFOAMISLANDSB4F': ('Îles Écume', 'sous-sol 4'),
    'CINNABARGYM': ('Cramois\'Île', 'Arène'), 'CINNABARLABMETRONOMEROOM': ('Cramois\'Île', 'Labo Pokémon'),
    'CINNABARLABFOSSILROOM': ('Cramois\'Île', 'Labo Pokémon'), 'CINNABARLABTRADEROOM': ('Cramois\'Île', 'Labo Pokémon'),
    'POKEMONMANSION1F': ('Manoir Pokémon', 'rez-de-chaussée'), 'POKEMONMANSION2F': ('Manoir Pokémon', '1er étage'),
    'POKEMONMANSION3F': ('Manoir Pokémon', '2e étage'), 'POKEMONMANSIONB1F': ('Manoir Pokémon', 'sous-sol'),
    'ROUTE21': ('Route 21', None), 'POWERPLANT': ('Centrale', None), 'ROUTE23': ('Route 23', None),
    'VICTORYROAD1F': ('Route Victoire', 'rez-de-chaussée'), 'VICTORYROAD2F': ('Route Victoire', '1er étage'),
    'VICTORYROAD3F': ('Route Victoire', '2e étage'),
    'CERULEANCAVE1F': ('Caverne Azurée', 'rez-de-chaussée'), 'CERULEANCAVE2F': ('Caverne Azurée', '1er étage'),
    'CERULEANCAVEB1F': ('Caverne Azurée', 'sous-sol'),
    'VERMILIONTRADEHOUSE': ('Carmin sur Mer', 'maison'), 'CERULEANTRADEHOUSE': ('Azuria', 'maison'),
    'ROUTE2TRADEHOUSE': ('Route 2', 'maison'), 'UNDERGROUNDPATHROUTE5': ('Route 5', 'entrée du Souterrain'),
    'VERMILIONOLDRODHOUSE': ('Carmin sur Mer', 'maison du pêcheur'),
    'FUCHSIAGOODRODHOUSE': ('Parmanie', 'maison du pêcheur'), 'ROUTE12SUPERRODHOUSE': ('Route 12', 'maison du pêcheur'),
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
        self.rows = []

    def add(self, section, zone, detail, done, label=None, missed=None, team=None, item=0, coins=0, species=0, give=0, order=None):
        if zone not in ZONES:
            raise SystemExit(f'Zone inconnue : {zone}')
        self.rows.append(dict(section=section, zone=zone, detail=detail, done=done, label=label, missed=missed, team=team,
                              item=item, coins=coins, species=species, give=give, order=len(self.rows) if order is None else order))


def build(d, yellow):
    ev = d['events']
    dex = d['dex']
    items = d['items']

    def E(name):
        if name not in ev:
            raise SystemExit(f'Drapeau inconnu dans {"Jaune" if yellow else "Rouge / Bleu"} : {name}')
        return f'E:{ev[name]}'

    def T(name):
        return f'T:{d["toggles"].index(name)}'

    def R(offset, bit):
        return f'R:{offset + bit // 8}:{bit % 8}'

    def sp(name):
        return dex['DEX_' + name]

    rows = Rows()
    S, P, D, I, U = 'Story', 'Pokedex', 'Trainers', 'Items', 'Encounters'

    # ---------- Histoire ----------
    badges = [('Badge Roche', 'Pierre', 'Argenta'), ('Badge Cascade', 'Ondine', 'Azuria'),
              ('Badge Foudre', 'Major Bob', 'Carmin sur Mer'), ('Badge Prisme', 'Érika', 'Céladopole'),
              ('Badge Âme', 'Koga', 'Parmanie'), ('Badge Marais', 'Morgane', 'Safrania'),
              ('Badge Volcan', 'Auguste', 'Cramois\'Île'), ('Badge Terre', 'Giovanni', 'Jadielle')]
    rows.add(S, 'Bourg Palette', None, E('EVENT_GOT_STARTER'), 'Recevoir son premier Pokémon du Prof. Chen')
    rows.add(S, 'Bourg Palette', None, E('EVENT_OAK_GOT_PARCEL'), 'Apporter le colis de Jadielle au Prof. Chen')
    rows.add(S, 'Bourg Palette', None, E('EVENT_GOT_POKEDEX'), 'Recevoir le Pokédex')
    story = [(0, None), (1, None), (None, ('Route 25', 'Aider Léo au Cottage de Léo', E('EVENT_GOT_SS_TICKET'))), (2, None),
             (None, ('Repaire Rocket', 'Vaincre Giovanni au Repaire Rocket', E('EVENT_BEAT_ROCKET_HIDEOUT_GIOVANNI'))),
             (None, ('Tour Pokémon', 'Apaiser l\'esprit d\'Ossatueur', E('EVENT_BEAT_GHOST_MAROWAK'))),
             (None, ('Tour Pokémon', 'Délivrer M. Fuji', E('EVENT_RESCUED_MR_FUJI'))), (3, None), (4, None),
             (None, ('Sylphe SARL', 'Libérer la Sylphe SARL (Giovanni vaincu)', E('EVENT_BEAT_SILPH_CO_GIOVANNI'))),
             (5, None), (6, None), (7, None),
             (None, ('Plateau Indigo', 'Vaincre la Ligue Pokémon et entrer au Panthéon', 'F'))]
    for badge, extra in story:
        if badge is not None:
            name, leader, city = badges[badge]
            rows.add(S, city, 'Arène', f'B:{badge}', f'{name} ({leader})')
        else:
            rows.add(S, extra[0], None, extra[2], extra[1])
    if yellow:
        rows.add(S, 'Bourg Palette', None, 'P', 'Amitié de Pikachu au maximum')

    # ---------- Dresseurs ----------
    rival = [(None, 'Bourg Palette', 'Labo du Prof. Chen', E('EVENT_BATTLED_RIVAL_IN_OAKS_LAB'), None),
             (None, 'Route 22', 'premier combat', E('EVENT_BEAT_ROUTE22_RIVAL_1ST_BATTLE'), E('EVENT_BEAT_BROCK')),
             (None, 'Azuria', None, E('EVENT_BEAT_CERULEAN_RIVAL'), None),
             # Le S.S. Anne n'a pas de drapeau pour ce combat : il barre l'escalier de la cabine du capitaine (CS01).
             (None, 'S.S. Anne', 'pont 2', E('EVENT_GOT_HM01'), None),
             (None, 'Tour Pokémon', '1er étage', E('EVENT_BEAT_POKEMON_TOWER_RIVAL'), None),
             (None, 'Sylphe SARL', '6e étage', E('EVENT_BEAT_SILPH_CO_RIVAL'), None),
             (None, 'Route 22', 'second combat', E('EVENT_BEAT_ROUTE22_RIVAL_2ND_BATTLE'), None)]
    trainer_rows = []
    for _, zone, detail, done, missed in rival:
        trainer_rows.append((ZONES.index(zone), 0, dict(zone=zone, detail=detail, done=done, label='Rival', missed=missed)))
    specials = [('Azuria', None, 'EVENT_BEAT_CERULEAN_ROCKET_THIEF', 'Voleur de la Team Rocket'),
                ('Route 24', 'Pont Pépite', 'EVENT_BEAT_ROUTE24_ROCKET', 'Sbire de la Team Rocket'),
                ('Mont Sélénite', 'sous-sol 2', 'EVENT_BEAT_MT_MOON_EXIT_SUPER_NERD', 'Gardien des fossiles'),
                ('Safrania', 'Dojo', 'EVENT_BEAT_KARATE_MASTER', 'Maître Karaté')]
    if yellow:
        specials += [('Mont Sélénite', 'sous-sol 2', 'EVENT_BEAT_MT_MOON_3_JESSIE_JAMES', 'Jessie et James'),
                     ('Repaire Rocket', 'sous-sol 4', 'EVENT_BEAT_ROCKET_HIDEOUT_4_JESSIE_JAMES', 'Jessie et James'),
                     ('Tour Pokémon', '6e étage', 'EVENT_BEAT_POKEMONTOWER_7_JESSIE_JAMES', 'Jessie et James'),
                     ('Sylphe SARL', '10e étage', 'EVENT_BEAT_SILPH_CO_11F_JESSIE_JAMES', 'Jessie et James')]
    for zone, detail, name, label in specials:
        trainer_rows.append((ZONES.index(zone), ev[name], dict(zone=zone, detail=detail, done=E(name), label=label, missed=None)))
    statics = []
    for t in d['trainers']:
        k = key(t['file'])
        if k in ELITE_FOUR:
            continue
        if t.get('species'):
            statics.append(t)
            continue
        zone, detail = place(t['file'])
        missed = E('EVENT_BEAT_SILPH_CO_GIOVANNI') if t['event'].startswith('EVENT_BEAT_SILPH_CO_') else None
        trainer_rows.append((ZONES.index(zone), t['flag'], dict(zone=zone, detail=detail, done=f'E:{t["flag"]}',
                                                               team=','.join(f'{sp(s)}:{lv}' for lv, s in t['team']),
                                                               missed=missed)))
    # Arène de Cramois'Île : pas d'en-tête de dresseur (quiz), ses dresseurs dans l'ordre des objets.
    for i, t in enumerate(d['cinnabar']):
        name = f'EVENT_BEAT_CINNABAR_GYM_TRAINER_{i}'
        trainer_rows.append((ZONES.index('Cramois\'Île'), ev[name], dict(zone='Cramois\'Île', detail='Arène', done=E(name),
                                                                        team=','.join(f'{sp(s)}:{lv}' for lv, s in t),
                                                                        missed=None)))
    for z, flag, r in sorted(trainer_rows, key=lambda x: (x[0], x[1])):
        rows.add(D, r['zone'], r['detail'], r['done'], r.get('label'), missed=r['missed'], team=r.get('team'))

    # ---------- Objets ----------
    item_rows = []
    for g in d['ground']:
        if key(g['map']) in UNUSED:
            continue
        zone, detail = place(g['map'])
        done = T(g['toggle'])
        if g['initial'] == 'OFF':  # n'apparaît qu'après un événement (Scope Sylphe, Clé Ascenseur)
            done += '+' + (E('EVENT_BEAT_ROCKET_HIDEOUT_GIOVANNI') if g['item'] == 'SILPH_SCOPE'
                           else E('EVENT_ROCKET_DROPPED_LIFT_KEY'))
        item_rows.append((ZONES.index(zone), 0, g['flag'], dict(zone=zone, detail=detail, done=done, item=g['itemId'])))
    for h in d['hidden']:
        if key(h['map']) in UNUSED:
            continue
        zone, detail = place(h['map'])
        item_rows.append((ZONES.index(zone), 1, h['flag'], dict(zone=zone, detail=', '.join(x for x in (detail, 'caché') if x),
                                                                done=R(HIDDEN_ITEMS, h['flag']), item=h['itemId'])))
    for c in d['coins']:
        zone, detail = place(c['map'])
        item_rows.append((ZONES.index(zone), 1, c['flag'], dict(zone=zone, detail=', '.join(x for x in (detail, 'cachés') if x),
                                                                done=R(HIDDEN_COINS, c['flag']), coins=c['coins'])))
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
        item_rows.append((ZONES.index(zone), 2, ev[name], dict(zone=zone, detail=', '.join(x for x in (detail, 'cadeau') if x),
                                                               done=E(name), item=item, coins=coins, label=label)))
    # Fossile Dôme ou Nautile : un seul des deux, au choix.
    item_rows.append((ZONES.index('Mont Sélénite'), 2, 0, dict(zone='Mont Sélénite', detail='sous-sol 2, au choix',
                                                               done=E('EVENT_GOT_DOME_FOSSIL') + '|' + E('EVENT_GOT_HELIX_FOSSIL'),
                                                               label='Fossile Dôme ou Nautile')))
    for bit, item, house in ((3, 'OLD_ROD', 'VermilionOldRodHouse'), (4, 'GOOD_ROD', 'FuchsiaGoodRodHouse'),
                             (5, 'SUPER_ROD', 'Route12SuperRodHouse')):
        zone, detail = place(house)
        item_rows.append((ZONES.index(zone), 2, 0, dict(zone=zone, detail=detail + ', cadeau', done=R(STATUS_FLAGS1, bit),
                                                        item=items[item])))
    for z, kind, flag, r in sorted(item_rows, key=lambda x: (x[0], x[1], x[2])):
        rows.add(I, r['zone'], r['detail'], r['done'], r.get('label'), item=r.get('item', 0), coins=r.get('coins', 0))

    # ---------- Rencontres uniques ----------
    enc = []
    for t in statics:
        zone, detail = place(t['file'])
        enc.append((ZONES.index(zone), t['flag'], dict(zone=zone, detail=detail, done=f'E:{t["flag"]}', species=sp(t['species']),
                                                       label=None)))
    enc.append((ZONES.index('Route 12'), 0, dict(zone='Route 12', detail=None, done=E('EVENT_BEAT_ROUTE12_SNORLAX'),
                                                species=sp('SNORLAX'), label=None)))
    enc.append((ZONES.index('Route 16'), 0, dict(zone='Route 16', detail=None, done=E('EVENT_BEAT_ROUTE16_SNORLAX'),
                                                species=sp('SNORLAX'), label=None)))
    enc.append((ZONES.index('Route 4'), 1, dict(zone='Route 4', detail='Centre Pokémon, acheté', done=E('EVENT_BOUGHT_MAGIKARP'),
                                               species=sp('MAGIKARP'), label=None)))
    enc.append((ZONES.index('Céladopole'), 1, dict(zone='Céladopole', detail='toit du Manoir, cadeau',
                                                  done=T('TOGGLE_CELADON_MANSION_EEVEE_GIFT'), species=sp('EEVEE'), label=None)))
    enc.append((ZONES.index('Safrania'), 1, dict(zone='Safrania', detail='Dojo, au choix',
                                                done=E('EVENT_GOT_HITMONLEE') + '|' + E('EVENT_GOT_HITMONCHAN'),
                                                species=sp('HITMONLEE'), label='Kicklee ou Tygnon')))
    enc.append((ZONES.index('Sylphe SARL'), 1, dict(zone='Sylphe SARL', detail='6e étage, cadeau', done=R(STATUS_FLAGS4, 0),
                                                   species=sp('LAPRAS'), label=None)))
    if yellow:
        enc.append((ZONES.index('Azuria'), 1, dict(zone='Azuria', detail='maison de Mélanie, cadeau',
                                                  done=E('EVENT_GOT_BULBASAUR_IN_CERULEAN'), species=sp('BULBASAUR'), label=None)))
        enc.append((ZONES.index('Route 24'), 1, dict(zone='Route 24', detail='cadeau', done=E('EVENT_GOT_CHARMANDER_ON_ROUTE_24'),
                                                    species=sp('CHARMANDER'), label=None)))
        enc.append((ZONES.index('Carmin sur Mer'), 1, dict(zone='Carmin sur Mer', detail='cadeau de l\'Agente Jenny',
                                                          done=E('EVENT_GOT_SQUIRTLE_FROM_OFFICER_JENNY'),
                                                          species=sp('SQUIRTLE'), label=None)))
    for t in d['trades']:
        m = re.match(r'used in (\w+)', t['where'])
        if not m:
            continue
        zone, detail = place(m.group(1))
        enc.append((ZONES.index(zone), 2, dict(zone=zone, detail=', '.join(x for x in (detail, 'échange') if x),
                                              done=R(TRADES, t['index']), species=sp(t['get']), label=None,
                                              give=sp(t['give']))))
    for z, o, r in sorted(enc, key=lambda x: (x[0], x[1])):
        rows.add(U, r['zone'], r['detail'], r['done'], r['label'], species=r['species'], give=r.get('give', 0))
    return rows.rows


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
        lines.append(f'    /// <summary>Étapes de {doc} (le Pokédex est ajouté à part : les 151).</summary>')
        lines.append(f'    public static readonly Gen1Row[] {name} =')
        lines.append('    [')
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
