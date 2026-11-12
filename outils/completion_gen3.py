"""
Génère MonsutaPotaru/Services/Completion/Gen3CompletionData.cs d'après les désassemblages pokeruby, pokeemerald et
pokefirered (pret).

    python outils/completion_gen3.py <dossier> MonsutaPotaru/Services/Completion/Gen3CompletionData.cs

<dossier> contient les clones de https://github.com/pret/pokeruby, pokeemerald et pokefirered (git clone --depth 1).
À côté de ce script : completion_gen3_lieux.json, les noms français des sections de carte dans l'ordre du jeu (liste de
PKHeX, Resources/text/locations/gen3/text_rsefrlg_00000_fr.txt) ; completion_gen3_ordre.json, les lieux des check-lists
de l'utilisateur dans leur ordre (celui de l'aventure). Les étapes de l'histoire, des rencontres et des quêtes sont
choisies ici d'après ces check-lists ; dresseurs, objets, objets cachés et cadeaux sont lus dans les dépôts.
"""
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from completion_gen3_extract import extract  # noqa: E402

# Classes de dresseurs comptées ailleurs : champions d'arène (badges), Conseil 4 et Maître (Ligue).
STORY_CLASSES = {'TRAINER_CLASS_LEADER', 'TRAINER_CLASS_ELITE_FOUR', 'TRAINER_CLASS_CHAMPION'}
# Un même combat sous plusieurs identifiants, selon le Pokémon de départ ou le personnage choisi : une seule étape.
VARIANTS = r'_(TREECKO|TORCHIC|MUDKIP|BULBASAUR|CHARMANDER|SQUIRTLE)$'


def clean(name, game):
    """Nom PKHeX d'une section, sans la précision de jeu (« Route Victoire (RSE) » → « Route Victoire »)."""
    if '/' in name:  # « Tour de Combat (RS) / Zone de Combat (E) »
        name = name.split('/')[1 if game == 'pokeemerald' else 0].strip()
    return re.sub(r'\s*\((RSE|RS|E|RFVF|Kanto|inutilisé)\)$', '', name).strip()


def floor(code):
    m = re.fullmatch(r'(B?)(\d+)F', code)
    if not m:
        return None
    n = int(m.group(2))
    if m.group(1):
        return f'sous-sol {n}'
    return 'rez-de-chaussée' if n == 1 else ('1er étage' if n == 2 else f'{n - 1}e étage')


def detail(map_name):
    """Précision tirée du nom de la carte après le premier « _ » (« RustboroCity_Gym » → « Arène »)."""
    parts = map_name.split('_')[1:]
    if not parts:
        return None
    rest = '_'.join(parts)
    if 'Gym' in parts[0]:
        return 'Arène'
    if parts[0].startswith('PokemonCenter'):
        return 'Centre Pokémon'
    if parts[0] in ('Mart', 'DepartmentStore') or 'Mart' in parts[0]:
        return 'Boutique' if parts[0] != 'DepartmentStore' else f'Centre commercial{", " + floor(parts[-1]) if floor(parts[-1]) else ""}'
    f = floor(parts[-1])
    if 'House' in rest:
        return 'maison'
    if f and len(parts) == 1:
        return f
    if f:
        return f
    if re.match(r'(B?\d+)R$', parts[-1]):
        return 'salle'
    return None


def cs(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"' if s is not None else 'null'


def build(d, game, places, checklist):
    k = d['k']
    rows = []

    def F(name):
        if name not in k:
            raise SystemExit(f'Drapeau inconnu dans {game} : {name}')
        return f'E:{k[name]}'

    def any_of(*names):
        return '|'.join(F(n) for n in names)

    def zone_of(map_name):
        sec = d['mapsec'].get(map_name)
        idx = d['sections'].index(sec) if d['sections'] and sec in d['sections'] else None
        if idx is None:  # pokeruby : MAPSEC_ dans l'ordre de PKHeX (même numérotation)
            idx = SECTIONS.index(sec) if sec in SECTIONS else None
        if idx is None or idx >= len(places):
            raise SystemExit(f'Section inconnue pour {map_name} : {sec}')
        return clean(places[idx], game)

    # Ordre de l'aventure : première apparition de la zone dans la check-list de l'utilisateur
    def order(zone):
        for i, lieu in enumerate(checklist):
            if lieu.startswith(zone) or zone in lieu:
                return i
        return 10_000

    def add(section, zone, det, done, label=None, missed=None, team=None, item=0, tm=0, species=0, give=0, count=0, sort=0):
        rows.append(dict(section=section, zone=zone, detail=det, done=done, label=label, missed=missed, team=team, item=item,
                         tm=tm, species=species, give=give, count=count, sort=(order(zone), sort)))

    S, D, I, U, Q = 'Story', 'Trainers', 'Items', 'Encounters', 'Quests'
    cur = CURATED[game]

    for i, (zone, det, label, cond) in enumerate(cur['story']):
        add(S, zone, det, cond(F, any_of), label, sort=i)

    # ---------- Dresseurs ----------
    groups = {}
    for t in d['trainers']:
        if t['cls'] in STORY_CLASSES:
            continue
        key = (t['map'], re.sub(VARIANTS, '', re.sub(r'_(BRENDAN|MAY)_', '_RIVAL_', t['id'])))
        groups.setdefault(key, []).append(t)
    for (map_name, _), ts in groups.items():
        t = ts[0]
        zone = zone_of(map_name)
        done = '|'.join(f'E:{d["trainer_start"] + k[x["id"]]}' for x in ts)
        hide = t['hide']
        # Raté : visible au départ, caché ensuite sans avoir été battu. Pas pour un drapeau sans nom (pokeruby : FLAG_0x31C)
        # ni pour un drapeau temporaire (remis à zéro à chaque carte).
        missed = F(hide) if hide and hide not in d['initial'] and hide in k and not re.match(r'FLAG_(0x|TEMP|UNUSED)', hide) else None
        team = ','.join(f'{sp}:{lv}' for lv, sp in t['team']) or None
        label = None if team else 'Dresseur'
        if len(ts) > 1:
            label = 'Rival'
        add(D, zone, detail(map_name), done, label, missed=missed, team=team if len(ts) == 1 else None,
            sort=d['trainer_start'] + k[t['id']])

    # ---------- Objets ----------
    tm_of = {}
    for name, value in k.items():
        m = re.match(r'ITEM_(TM|HM)(\d\d)', name)
        if m:
            tm_of[value] = int(m.group(2)) + (50 if m.group(1) == 'HM' else 0)
    seen = set()

    def item(x, kind, extra):
        flag = x['flag']
        if flag in seen or flag not in k or x['item'] not in k:
            return
        seen.add(flag)
        zone = zone_of(x['map'])
        det = ', '.join(p for p in (detail(x['map']), extra) if p) or None
        value = k[x['item']]
        add(I, zone, det, F(flag), item=0 if value in tm_of else value, tm=tm_of.get(value, 0),
            count=x.get('count', 1) if x.get('count', 1) > 1 else 0, sort=kind * 100_000 + k[flag])

    for x in d['ground']:
        item(x, 0, None)
    for x in d['hidden']:
        item(x, 1, 'caché')
    for x in d['gifts']:
        # Cadeaux quotidiens (une baie par jour, drapeau remis à zéro chaque jour) : pas une étape
        if 'DAILY' not in x['flag']:
            item(x, 2, 'cadeau')

    # ---------- Rencontres et quêtes ----------
    for i, (zone, det, species, cond, label) in enumerate(cur['encounters']):
        add(U, zone, det, cond(F, any_of), label, species=d['national'][k[species]], sort=i)
    for i, (zone, det, label, cond) in enumerate(cur['quests']):
        add(Q, zone, det, cond(F, any_of), label, sort=i)

    section_order = {s: n for n, s in enumerate((S, D, I, U, Q))}
    # Histoire, rencontres et quêtes : dans l'ordre choisi ici ; dresseurs et objets : par lieu, dans l'ordre de l'aventure
    rows.sort(key=lambda r: (section_order[r['section']],) + ((r['sort'][1],) if r['section'] in (S, U, Q) else r['sort']))
    return rows


# Sections de carte de Rubis / Saphir (le dépôt pokeruby n'a pas de region_map_sections.json) : la même numérotation que
# PKHeX et Émeraude pour Hoenn, lue dans include/constants/region_map_sections.h si présent, sinon reprise d'Émeraude.
SECTIONS = []


def f(name):
    return lambda F, A: F(name)


def a(*names):
    return lambda F, A: A(*names)


def raw(text):
    return lambda F, A: text


def badge(n, leader, city, game):
    names = {'kanto': ['Roche', 'Cascade', 'Foudre', 'Prisme', 'Âme', 'Marais', 'Volcan', 'Terre'],
             'hoenn': ['Roche', 'Poing', 'Dynamo', 'Chaleur', 'Balancier', 'Plume', 'Esprit', 'Pluie']}
    region = 'kanto' if game == 'pokefirered' else 'hoenn'
    return (city, 'Arène', f'Badge {names[region][n - 1]} ({leader})', f(f'FLAG_BADGE0{n}_GET'))


def hoenn_story(game):
    e = game == 'pokeemerald'
    team = 'Magma' if e else 'Magma / Aqua'
    s = [
        ('Route 101', None, 'Sauver le Prof. Seko et recevoir son premier Pokémon', f('FLAG_RESCUED_BIRCH')),
        ('Route 103', None, 'Premier combat contre le rival', f('FLAG_DEFEATED_RIVAL_ROUTE103')),
        ('Bourg-en-Vol', None, 'Recevoir le Pokédex', f('FLAG_SYS_POKEDEX_GET')),
        badge(1, 'Roxanne', 'Mérouville', game),
        ('Mérouville', None, 'Rapporter le Pack Devon', f('FLAG_RETURNED_DEVON_GOODS')),
        badge(2, 'Bastien', 'Village Myokara', game),
        ('Grotte Granite', None, 'Remettre la Lettre à Pierre Rochard', f('FLAG_DELIVERED_STEVEN_LETTER')),
        ('Poivressel', None, 'Remettre le Pack Devon au Capitaine Poupe', f('FLAG_DELIVERED_DEVON_GOODS')),
        badge(3, 'Voltère', 'Lavandia', game),
        ('Tunnel Mérazon', None, 'Libérer le passage du Tunnel Mérazon', f('FLAG_RUSTURF_TUNNEL_OPENED')),
        ('Mont Chimnée', None, f'Vaincre la Team {team} au Mont Chimnée', f('FLAG_DEFEATED_EVIL_TEAM_MT_CHIMNEY')),
        badge(4, 'Adriane', 'Vermilava', game),
        badge(5, 'Norman', 'Clémenti-Ville', game),
        ('Route 119', 'Institut Météo', "Chasser la Team de l'Institut Météo", f('FLAG_RECEIVED_CASTFORM')),
        badge(6, 'Alizée', 'Cimetronelle', game),
    ]
    if e:
        s.append(('Algatia', 'Centre Spatial', 'Défendre le Centre Spatial', f('FLAG_DEFEATED_MAGMA_SPACE_CENTER')))
    s += [
        badge(7, 'Lévy et Tatia', 'Algatia', game),
        ('Caverne Fondmer', None, 'Atteindre le fond de la Caverne Fondmer',
         f('FLAG_KYOGRE_ESCAPED_SEAFLOOR_CAVERN' if e else 'FLAG_LEGEND_ESCAPED_SEAFLOOR_CAVERN')),
        ('Atalanopolis', None, 'Calmer Kyogre et Groudon' if e else 'Apaiser le Pokémon légendaire',
         f('FLAG_SOOTOPOLIS_ARCHIE_MAXIE_LEAVE' if e else 'FLAG_LEGENDARY_BATTLE_COMPLETED')),
        badge(8, 'Marc' if e else 'Juan', 'Atalanopolis', game),
        ('Mont Mémoria', None, "Recevoir l'Orbe des anciens du Mont Mémoria", f('FLAG_RECEIVED_RED_OR_BLUE_ORB')),
        ('Route Victoire', None, 'Battre Timmy', f('FLAG_DEFEATED_WALLY_VICTORY_ROAD')),
    ]
    for who, flag in (('Damien', 'SIDNEY' if e else 'SYDNEY'), ('Spectra', 'PHOEBE'), ('Glacia', 'GLACIA'), ('Aragon', 'DRAKE')):
        # Le jeu efface ces drapeaux pour les revanches : la Ligue gagnée une fois suffit.
        s.append(('Éternara', 'Ligue Pokémon', f'Battre {who} (Conseil 4)', a(f'FLAG_DEFEATED_ELITE_4_{flag}', 'FLAG_SYS_GAME_CLEAR')))
    s += [('Éternara', 'Ligue Pokémon', f'Battre le Maître {"Marc" if e else "Pierre"} et entrer au Panthéon', f('FLAG_SYS_GAME_CLEAR')),
          ('Bourg-en-Vol', None, 'Recevoir le Pokédex National', f('FLAG_SYS_NATIONAL_DEX'))]
    if e:
        s += [('Site Météore', None, 'Battre Pierre Rochard', f('FLAG_DEFEATED_METEOR_FALLS_STEVEN')),
              ('Zone de Combat', None, 'Recevoir le Passe Zone', f('FLAG_SYS_FRONTIER_PASS'))]
    return s


def hoenn_encounters(game):
    e = game == 'pokeemerald'
    ruby = game == 'pokeruby'
    u = []
    if e:
        u += [('Grotte Marine', None, 'SPECIES_KYOGRE', f('FLAG_DEFEATED_KYOGRE'), None),
              ('Grotte Terra', None, 'SPECIES_GROUDON', f('FLAG_DEFEATED_GROUDON'), None),
              ('Pilier Céleste', None, 'SPECIES_RAYQUAZA', f('FLAG_DEFEATED_RAYQUAZA'), None),
              ('Ruines Désert', None, 'SPECIES_REGIROCK', f('FLAG_DEFEATED_REGIROCK'), None),
              ('Grotte Island', None, 'SPECIES_REGICE', f('FLAG_DEFEATED_REGICE'), None),
              ('Tombeau Antique', None, 'SPECIES_REGISTEEL', f('FLAG_DEFEATED_REGISTEEL'), None)]
        u += [('New Lavandia', None, 'SPECIES_VOLTORB', f(f'FLAG_DEFEATED_VOLTORB_{n}_NEW_MAUVILLE'), None) for n in (1, 2, 3)]
        u += [('Planque Aqua', None, 'SPECIES_ELECTRODE', f(f'FLAG_DEFEATED_ELECTRODE_{n}_AQUA_HIDEOUT'), None) for n in (1, 2)]
    else:
        # Rubis / Saphir : pas de drapeau « vaincu » ; l'objet du Pokémon est caché une fois le combat passé.
        u += [('Grotte Origine', None, 'SPECIES_GROUDON' if ruby else 'SPECIES_KYOGRE', f('FLAG_HIDE_LEGEND_MON_CAVE_OF_ORIGIN'), None),
              ('Pilier Céleste', None, 'SPECIES_RAYQUAZA', f('FLAG_HIDE_RAYQUAZA'), None),
              ('Ruines Désert', None, 'SPECIES_REGIROCK', f('FLAG_HIDE_REGIROCK'), None),
              ('Grotte Island', None, 'SPECIES_REGICE', f('FLAG_HIDE_REGICE'), None),
              ('Tombeau Antique', None, 'SPECIES_REGISTEEL', f('FLAG_HIDE_REGISTEEL'), None)]
        u += [('New Lavandia', None, 'SPECIES_VOLTORB', f(f'FLAG_HIDE_VOLTORB_{n}_NEW_MAUVILLE'), None) for n in (1, 2, 3)]
        u += [('Planque Magma' if ruby else 'Planque Aqua', None, 'SPECIES_ELECTRODE', f(f'FLAG_HIDE_ELECTRODE_{n}_HIDEOUT'), None)
              for n in (1, 2)]
    u += [('Route 120', 'errant dans Hoenn', 'SPECIES_LATIAS', raw('D:380|D:381'), 'Latias ou Latios (errant)'),
          ('Île du Sud', 'Ticket Éon', 'SPECIES_LATIOS', raw('D:380+D:381'), 'Latias et Latios'),
          ('Route 119', 'Institut Météo, cadeau', 'SPECIES_CASTFORM', f('FLAG_RECEIVED_CASTFORM'), None),
          ('Vermilava', 'œuf', 'SPECIES_WYNAUT', f('FLAG_RECEIVED_LAVARIDGE_EGG'), None),
          ('Mérouville', 'Devon SARL, fossile', 'SPECIES_LILEEP',
           f('FLAG_RECEIVED_REVIVED_FOSSIL_MON' if e else 'FLAG_RECEIVED_FOSSIL_MON'), 'Lilia ou Anorith'),
          ('Algatia', 'maison de Pierre Rochard, cadeau', 'SPECIES_BELDUM', f('FLAG_RECEIVED_BELDUM'), None),
          ('Mérouville', 'maison, échange', 'SPECIES_SEEDOT', f('FLAG_RUSTBORO_NPC_TRADE_COMPLETED'), None),
          ('Cimetronelle', 'maison, échange', 'SPECIES_PLUSLE', f('FLAG_FORTREE_NPC_TRADE_COMPLETED'), None),
          ('Pacifiville', 'maison, échange', 'SPECIES_HORSEA', f('FLAG_PACIFIDLOG_NPC_TRADE_COMPLETED'), None)]
    if e:
        u += [('Zone de Combat', None, 'SPECIES_SUDOWOODO', f('FLAG_DEFEATED_SUDOWOODO'), None),
              ('Zone de Combat', 'maison, échange', 'SPECIES_MEOWTH', f('FLAG_BATTLE_FRONTIER_TRADE_DONE'), None),
              ('Île Lointaine', 'Vieille Carte', 'SPECIES_MEW', a('FLAG_DEFEATED_MEW', 'FLAG_CAUGHT_MEW'), None),
              ('Roc Nombri', 'Ticket Mystique', 'SPECIES_LUGIA', a('FLAG_DEFEATED_LUGIA', 'FLAG_CAUGHT_LUGIA'), None),
              ('Roc Nombri', 'Ticket Mystique', 'SPECIES_HO_OH', a('FLAG_DEFEATED_HO_OH', 'FLAG_CAUGHT_HO_OH'), None),
              ('Île Aurore', 'Ticket Aurora', 'SPECIES_DEOXYS', f('FLAG_DEFEATED_DEOXYS'), None)]
    return u


def hoenn_quests(game):
    e = game == 'pokeemerald'
    q = [('Nénucrique', 'Musée', f'Exposer le tableau {cat} (concours de rang Master)', f(f'FLAG_{flag}_PAINTING_MADE'))
         for cat, flag in (('Sang-Froid', 'COOL'), ('Beauté', 'BEAUTY'), ('Grâce', 'CUTE'), ('Intelligence', 'SMART'), ('Robustesse', 'TOUGH'))]
    for berry, flag in (('Kiwan', 'SPELON'), ('Palma', 'PAMTRE'), ('Myrte', 'WATMEL'), ('Stekpa', 'DURIN'), ('Durin', 'BELUE')):
        q.append(('Route 123', 'femme du Maître des Baies', f'Recevoir la Baie {berry}', f(f'FLAG_RECEIVED_{flag}_BERRY')))
    if e:
        for fac, name, sym in (('TOWER', 'Tour de Combat', 'Capacité'), ('DOME', 'Dôme de Combat', 'Tactique'),
                               ('PALACE', 'Palace de Combat', 'Esprit'), ('ARENA', 'Dojo de Combat', 'Cran'),
                               ('FACTORY', 'Usine de Combat', 'Savoir'), ('PIKE', 'Reptile de Combat', 'Chance'),
                               ('PYRAMID', 'Pyramide de Combat', 'Bravoure')):
            q.append(('Zone de Combat', name, f"Symbole {sym} d'Argent", f(f'FLAG_SYS_{fac}_SILVER')))
            q.append(('Zone de Combat', name, f"Symbole {sym} d'Or", f(f'FLAG_SYS_{fac}_GOLD')))
        for move, flag, zone in (('Blabla Dodo', 'SLEEP_TALK', 'Cimetronelle'), ('Clonage', 'SUBSTITUTE', 'Nénucrique'),
                                 ('Copie', 'MIMIC', 'Vermilava'), ('Damoclès', 'DOUBLE_EDGE', 'Atalanopolis'),
                                 ('Dynamopoing', 'DYNAMICPUNCH', 'Algatia'), ('Explosion', 'EXPLOSION', 'Pacifiville'),
                                 ('Métronome', 'METRONOME', 'Autéquia'), ('Roulade', 'ROLLOUT', 'Lavandia'),
                                 ('Taillade', 'FURY_CUTTER', 'Vergazon'), ('Vantardise', 'SWAGGER', 'Poivressel')):
            q.append((zone, 'Maître des capacités', f'Apprendre {move}', f(f'FLAG_MOVE_TUTOR_TAUGHT_{flag}')))
    return q


def frlg_story():
    g = 'pokefirered'
    return [
        ('Bourg Palette', 'Labo du Prof. Chen', 'Recevoir son premier Pokémon', f('FLAG_SYS_POKEMON_GET')),
        ('Bourg Palette', 'Labo du Prof. Chen', 'Recevoir le Pokédex', f('FLAG_SYS_POKEDEX_GET')),
        badge(1, 'Pierre', 'Argenta', g), badge(2, 'Ondine', 'Azuria', g),
        ('Route 25', None, 'Aider Léo', f('FLAG_HELPED_BILL_IN_SEA_COTTAGE')),
        badge(3, 'Major Bob', 'Carmin sur Mer', g),
        ('Repaire Rocket', None, 'Vaincre Giovanni au Repaire Rocket', f('FLAG_HIDE_HIDEOUT_GIOVANNI')),
        badge(4, 'Érika', 'Céladopole', g),
        ('Tour Pokémon', None, 'Délivrer M. Fuji', f('FLAG_RESCUED_MR_FUJI')),
        ('Sylphe SARL', None, 'Libérer la Sylphe SARL', f('FLAG_GOT_MASTER_BALL_FROM_SILPH')),
        badge(5, 'Koga', 'Parmanie', g), badge(6, 'Morgane', 'Safrania', g), badge(7, 'Auguste', "Cramois'Île", g),
        badge(8, 'Giovanni', 'Jadielle', g),
        ('Ligue Pokémon', None, 'Vaincre la Ligue et entrer au Panthéon', f('FLAG_SYS_GAME_CLEAR')),
        ('Île 3', 'Bois Baies', 'Sauver Lostelle', f('FLAG_RESCUED_LOSTELLE')),
        ('Mont Braise', None, 'Récupérer le Rubis', f('FLAG_GOT_RUBY')),
        ('Île 5', 'Entrepôt', "Vaincre la Team Rocket de l'Entrepôt", f('FLAG_DEFEATED_ROCKETS_IN_WAREHOUSE')),
        ('Île 1', None, 'Rendre le Saphir à Ciléo', f('FLAG_RECOVERED_SAPPHIRE')),
        ('Bourg Palette', None, 'Recevoir le Pokédex National', f('FLAG_SYS_NATIONAL_DEX')),
    ]


def frlg_encounters():
    u = [('Îles Écume', None, 'SPECIES_ARTICUNO', f('FLAG_FOUGHT_ARTICUNO'), None),
         ('Centrale', None, 'SPECIES_ZAPDOS', f('FLAG_FOUGHT_ZAPDOS'), None),
         ('Mont Braise', None, 'SPECIES_MOLTRES', f('FLAG_FOUGHT_MOLTRES'), None),
         ('Caverne Azurée', None, 'SPECIES_MEWTWO', f('FLAG_FOUGHT_MEWTWO'), None),
         ('Route 12', None, 'SPECIES_SNORLAX', f('FLAG_HIDE_ROUTE_12_SNORLAX'), None),
         ('Route 16', None, 'SPECIES_SNORLAX', f('FLAG_HIDE_ROUTE_16_SNORLAX'), None),
         ('Centrale', None, 'SPECIES_ELECTRODE', f('FLAG_FOUGHT_POWER_PLANT_ELECTRODE_1'), None),
         ('Centrale', None, 'SPECIES_ELECTRODE', f('FLAG_FOUGHT_POWER_PLANT_ELECTRODE_2'), None),
         # Un seul chien légendaire erre, celui qui suit le Pokémon de départ : compté par le Pokédex.
         ('Route 1', 'errant dans Kanto', 'SPECIES_ENTEI', raw('D:243|D:244|D:245'), 'Chien légendaire errant (selon ton starter)'),
         ('Route 4', 'Centre Pokémon, acheté', 'SPECIES_MAGIKARP', f('FLAG_BOUGHT_MAGIKARP'), None),
         ('Céladopole', 'Manoir, cadeau', 'SPECIES_EEVEE', f('FLAG_GOT_EEVEE'), None),
         ('Safrania', 'Dojo, au choix', 'SPECIES_HITMONLEE', f('FLAG_GOT_HITMON_FROM_DOJO'), 'Kicklee ou Tygnon'),
         ('Sylphe SARL', 'cadeau', 'SPECIES_LAPRAS', f('FLAG_GOT_LAPRAS_FROM_SILPH'), None),
         ("Cramois'Île", 'Labo Pokémon, fossile', 'SPECIES_OMANYTE', a('FLAG_REVIVED_DOME', 'FLAG_REVIVED_HELIX'), 'Amonita ou Kabuto'),
         ("Cramois'Île", 'Labo Pokémon, Vieil Ambre', 'SPECIES_AERODACTYL', f('FLAG_REVIVED_AMBER'), None),
         ('Île 5', "Labyrinthe d'O, œuf", 'SPECIES_TOGEPI', f('FLAG_GOT_TOGEPI_EGG'), None),
         ('Roc Nombri', 'Ticket Mystique', 'SPECIES_LUGIA', f('FLAG_FOUGHT_LUGIA'), None),
         ('Roc Nombri', 'Ticket Mystique', 'SPECIES_HO_OH', f('FLAG_FOUGHT_HO_OH'), None),
         ('Île Aurore', 'Ticket Aurora', 'SPECIES_DEOXYS', f('FLAG_FOUGHT_DEOXYS'), None)]
    for zone, det, sp, flag in (('Route 2', 'maison, échange', 'SPECIES_MR_MIME', 'MIMIEN'), ('Azuria', 'maison, échange', 'SPECIES_JYNX', 'ZYNX'),
                                ('Route 5', 'souterrain, échange', 'SPECIES_NIDORAN_F', 'MS_NIDO'),
                                ('Carmin sur Mer', 'maison, échange', 'SPECIES_FARFETCHD', 'CH_DING'),
                                ('Route 11', 'poste de garde, échange', 'SPECIES_NIDORINA', 'NINA'),
                                ('Route 18', 'poste de garde, échange', 'SPECIES_LICKITUNG', 'MARC'),
                                ("Cramois'Île", 'Labo Pokémon, échange', 'SPECIES_ELECTRODE', 'ESPHERE'),
                                ("Cramois'Île", 'Labo Pokémon, échange', 'SPECIES_TANGELA', 'TANGENY'),
                                ("Cramois'Île", 'Labo Pokémon, échange', 'SPECIES_SEEL', 'SEELOR')):
        u.append((zone, det, sp, f(f'FLAG_DID_{flag}_TRADE'), None))
    return u


def frlg_quests():
    q = []
    for move, flag, zone in (('Ultimapoing', 'MEGA_PUNCH', 'Route 4'), ('Ultimawashi', 'MEGA_KICK', 'Route 4'),
                             ('Frappe Atlas', 'SEISMIC_TOSS', 'Argenta'), ('Dévorêve', 'DREAM_EATER', 'Jadielle'),
                             ('Éboulement', 'ROCK_SLIDE', 'Grotte'), ('Riposte', 'COUNTER', 'Céladopole'),
                             ('Copie', 'MIMIC', 'Safrania'), ('Cage-Éclair', 'THUNDER_WAVE', 'Sylphe SARL'),
                             ('Clonage', 'SUBSTITUTE', 'Parmanie'), ('E-Coque', 'SOFT_BOILED', 'Céladopole'),
                             ('Métronome', 'METRONOME', "Cramois'Île"), ('Damoclès', 'DOUBLE_EDGE', 'Route Victoire'),
                             ('Explosion', 'EXPLOSION', 'Île 1'), ('Plaquage', 'BODY_SLAM', 'Île 4'),
                             ('Danse-Lames', 'SWORDS_DANCE', 'Île 7')):
        q.append((zone, 'Maître des capacités', f'Apprendre {move}', f(f'FLAG_TUTOR_{flag}')))
    q.append(('Cap Falaise', 'Maître des capacités', "Apprendre l'attaque ultime à son Pokémon de départ",
              a('FLAG_TUTOR_FRENZY_PLANT', 'FLAG_TUTOR_BLAST_BURN', 'FLAG_TUTOR_HYDRO_CANNON')))
    return q


CURATED = {}


def setup(games):
    for g in games:
        if g == 'pokefirered':
            CURATED[g] = dict(story=frlg_story(), encounters=frlg_encounters(), quests=frlg_quests())
        else:
            CURATED[g] = dict(story=hoenn_story(g), encounters=hoenn_encounters(g), quests=hoenn_quests(g))


def emit(games, tm_moves, out):
    lines = ['// <auto-generated>',
             "// Généré par outils/completion_gen3.py d'après les désassemblages pokeruby, pokeemerald et pokefirered (pret) : ne pas",
             '// modifier à la main, changer le script et le relancer.',
             '// </auto-generated>',
             '',
             'namespace MonsutaPotaru.Services;',
             '',
             'public static class Gen3CompletionData',
             '{',
             '    /// <summary>Capacité de chaque CT (1 à 50), puis de chaque CS (1 à 8) : numéros nationaux des capacités.</summary>',
             '    public static readonly ushort[] TmMoves = [' + ', '.join(str(m) for m in tm_moves) + '];',
             '']
    for name, doc, rows in games:
        lines.append(f'    /// <summary>Étapes de {doc} (le Pokédex national est ajouté à part : les 386).</summary>')
        lines.append(f'    public static readonly CompletionRow[] {name} =')
        lines.append('    [')
        for r in rows:
            a_ = [f'CompletionSection.{r["section"]}', cs(r['zone']), cs(r['detail']), cs(r['done'])]
            n = []
            for key, field in (('label', 'Label'), ('missed', 'Missed'), ('team', 'Team')):
                if r[key]:
                    n.append(f'{field}: {cs(r[key])}')
            for key, field in (('item', 'Item'), ('tm', 'Tm'), ('count', 'Count'), ('species', 'Species'), ('give', 'Give')):
                if r[key]:
                    n.append(f'{field}: {r[key]}')
            lines.append(f'        new({", ".join(a_ + n)}),')
        lines.append('    ];')
        lines.append('')
    lines[-1] = '}'
    with open(out, 'w', encoding='utf-8', newline='\n') as fh:
        fh.write('﻿' + '\n'.join(lines) + '\n')


if __name__ == '__main__':
    root, out = sys.argv[1:3]
    here = os.path.dirname(os.path.abspath(__file__))
    places = json.load(open(os.path.join(here, 'completion_gen3_lieux.json'), encoding='utf-8'))
    lieux = json.load(open(os.path.join(here, 'completion_gen3_ordre.json'), encoding='utf-8'))
    data = {g: extract(os.path.join(root, g)) for g in ('pokeruby', 'pokeemerald', 'pokefirered')}
    SECTIONS.extend(data['pokeemerald']['sections'])
    setup(data)
    games = [('RubySapphire', 'Rubis et Saphir', build(data['pokeruby'], 'pokeruby', places, lieux['pokeruby'])),
             ('Emerald', 'Émeraude', build(data['pokeemerald'], 'pokeemerald', places, lieux['pokeemerald'])),
             ('FireRedLeafGreen', 'Rouge Feu et Vert Feuille', build(data['pokefirered'], 'pokefirered', places, lieux['pokefirered']))]
    # Capacités des CT et CS : les mêmes dans les trois jeux, nommées dans les constantes de pokeruby (ITEM_TM01_FOCUS_PUNCH)
    rk = data['pokeruby']['k']
    named = sorted((v, m.group(1), int(m.group(2)), m.group(3)) for n, v in rk.items()
                   if (m := re.fullmatch(r'ITEM_(TM|HM)(\d\d)_(\w+)', n)))
    ek = data['pokeemerald']['k']
    by_key = {n[5:].replace('_', ''): v for n, v in ek.items() if n.startswith('MOVE_')}  # « SOLARBEAM » = « SOLAR_BEAM »
    moves = [by_key[mv.replace('_', '')] for _, kind, _, mv in sorted(named, key=lambda x: (x[1] == 'HM', x[2]))]
    emit(games, moves, out)
    for name, _, rows in games:
        counts = {}
        for r in rows:
            counts[r['section']] = counts.get(r['section'], 0) + 1
        print(name, counts, 'ratés possibles', sum(1 for r in rows if r['missed']))
