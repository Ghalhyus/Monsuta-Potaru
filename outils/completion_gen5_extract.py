"""
Extraction des données de la 5e génération pour outils/completion_gen5.py.

- Noir 2 / Blanc 2 : désassemblage pokebw2 (https://github.com/fuddlesworth/pokebw2) : listes de constantes
  (generated/*.txt), en-têtes des zones (res/field/zones), événements des cartes (res/field/events : personnages,
  objets au sol, objets cachés), scripts (res/field/scripts/*.s), dresseurs (res/trainers/data).
- Noir / Blanc : pas de désassemblage ; la ROM de l'utilisateur (completion_gen5_rom.Rom), mêmes formats : en-têtes des
  zones a/0/1/2 (48 octets), événements a/1/2/5, scripts a/0/5/7, dresseurs a/0/9/2 et a/0/9/3, noms des lieux (textes
  système, fichier 89).
- Les deux : liste des drapeaux de FlagsEditorEX (https://github.com/fattard/FlagsEditorEXPlugin,
  flagslist/flags_gen5bw_en.txt et flags_gen5b2w2_en.txt) pour le contenu des objets au sol et des objets cachés
  (l'objet est dans un script commun, celui-ci donne son nom anglais, rapproché des constantes ITEM_ de pokebw2).

Numéros communs : un dresseur a le script 3000 + son numéro (5000 + : second dresseur d'un duo), son drapeau est
TRAINER_FLAGS + son numéro ; un objet au sol est un personnage au script 7000 + n, son drapeau d'apparition est celui de
l'objet ; un objet caché est un décor au script 8000 + n, de drapeau HIDDEN_FLAGS + n.
"""
import json
import os
import re
import struct

from completion_gen5_rom import Rom, commands, disassemble, flow, texts

# Premier drapeau des dresseurs et des objets cachés (FlagsEditorEX ; pokebw2 : generated/flags.txt)
TRAINER_FLAGS = {'bw': 0x58C, 'b2w2': 0x5F0}
HIDDEN_FLAGS = {'bw': 0x384, 'b2w2': 0xB04}
# Scripts communs qui donnent l'objet de la variable 0x8000 (quantité : 0x8001)
GIVE_ITEM = range(2800, 2812)


def enum_list(path):
    """Constantes d'une liste de pokebw2 (« NOM = valeur », puis les suivantes à la suite)."""
    k = {}
    n = -1
    for line in open(path, encoding='utf-8'):
        line = line.split('#')[0].strip()
        if not line:
            continue
        m = re.fullmatch(r'(\w+)\s*=\s*(\w+)', line)
        if m:
            n = int(m.group(2), 0)
            k[m.group(1)] = n
        else:
            n += 1
            k[line] = n
    return k


def fattard(path):
    """Drapeaux nommés de FlagsEditorEX : numéro → (catégorie, lieu, précision, description, nom)."""
    out = {}
    for line in open(path, encoding='utf-8'):
        if line.startswith('//'):
            if 'Event Work' in line:
                break
            continue
        p = line.rstrip('\n').split('\t')
        if len(p) < 7 or not p[0].startswith('0x'):
            continue
        out[int(p[0], 16)] = (p[1], p[2], p[3], p[4], p[6])
    return out


def item_key(name):
    """« TM53 (Energy Ball) » → ITEM_TM53 ; « Poké Ball » → ITEM_POKE_BALL."""
    m = re.match(r'(TM|HM)(\d+)', name)
    if m:
        return f'ITEM_{m.group(1)}{int(m.group(2)):02d}'
    s = name.replace('é', 'e').replace('É', 'E').replace("'", '').replace('.', '').replace('-', '_').replace(' ', '_')
    return 'ITEM_' + re.sub(r'_+', '_', s).upper()


ITEM_NAME_FIXES = {
    # Noms de FlagsEditorEX (ceux du jeu en anglais) qui ne suivent pas les constantes de pokebw2 (abrégés du jeu) ; un
    # nom qui ne diffère que par les « _ » se retrouve tout seul (ITEM_THUNDER_STONE → ITEM_THUNDERSTONE)
    'ITEM_PARALYZE_HEAL': 'ITEM_PARLYZ_HEAL', 'ITEM_X_SP_ATK': 'ITEM_X_SPECIAL', 'ITEM_X_DEFENSE': 'ITEM_X_DEFEND',
}


def field_items(fl):
    """Objets au sol et cachés de FlagsEditorEX : {drapeau: [(nom anglais, version ou None)]} ; un objet propre à une
    version : « Grip Claw (B2) » ; deux objets selon la version : « Protein (W2) / Iron (B2) »."""
    ground, hidden = {}, {}
    for flag, (cat, where, _, desc, _) in fl.items():
        if cat not in ('FIELD ITEM', 'HIDDEN ITEM'):
            continue
        place_version = re.search(r'\((B|W|B2|W2)\)$', where)  # « White Forest (W) »
        name = re.sub(r'^Found ', '', re.sub(r' item ball disappeared$', '', desc))
        if 'recurring' in name.lower():
            continue  # objet caché qui revient : pas une étape
        variants = []
        for part in name.split(' / '):
            m = re.search(r'\s*\((B|W|B2|W2)\)$', part)
            variants.append((part[:m.start()], m.group(1)) if m else (part, place_version and place_version.group(1)))
        (ground if cat == 'FIELD ITEM' else hidden)[flag] = variants
    return ground, hidden


def items_of(names, k):
    """[(numéro de l'objet, version)] d'après les noms anglais de FlagsEditorEX (« Never-Melt Ice »)."""
    out = {}
    unknown = []
    for flag, variants in names.items():
        out[flag] = []
        for name, version in variants:
            key = item_key(name)
            key = ITEM_NAME_FIXES.get(key, key)
            if key not in k:
                alt = key.replace('_', '')
                cands = [c for c in k if c.startswith('ITEM_') and c.replace('_', '') == alt]
                if len(cands) != 1:
                    unknown.append(f'{name} ({key}, {flag:#x})')
                    continue
                key = cands[0]
            out[flag].append((k[key], version))
    if unknown:
        raise SystemExit('Objets inconnus : ' + ', '.join(unknown))
    return out


# ---------------------------------------------------------------------------------------------------------------------

def gifts(zone_flows):
    """Cadeaux : « WorkSet 0x8000, objet » (quantité dans 0x8001) puis l'appel d'un script commun qui le donne, et le
    premier FlagSet qui suit dans le même bloc. {zone: [(objet, quantité, drapeau)]}."""
    out = {}
    for zone, fl in zone_flows.items():
        fl = [x for x in fl if x[0] != 'LABEL']
        for i, (name, args) in enumerate(fl):
            if name != 'RTCallGlobal' or args[0] not in GIVE_ITEM:
                continue
            item = count = None
            for name2, args2 in reversed(fl[max(0, i - 8):i]):
                if name2 in ('WorkSet', 'WorkSetConst') and args2[0] == 0x8000 and item is None and isinstance(args2[1], int):
                    item = args2[1]
                if name2 in ('WorkSet', 'WorkSetConst') and args2[0] == 0x8001 and count is None and isinstance(args2[1], int):
                    count = args2[1]
            if not item or item >= 0x4000:
                continue
            for name2, args2 in fl[i + 1:i + 12]:
                if name2 == 'FlagSet':
                    out.setdefault(zone, []).append((item, count or 1, args2[0]))
                    break
                if name2 in ('VMHalt', 'VMEnd', 'VMReturn', 'VMJump', 'RTCallGlobal'):
                    break
    return out


VERSION_GETTERS = ('GameGetVersion', 'GetVersion')
STOPS = ('VMJump', 'VMReturn', 'VMHalt', 'VMEnd', 'VMJumpIf')


def version_flags(zone_flows):
    """Drapeaux levés selon la version (personnages d'une seule version) : « GameGetVersion v », puis « VMStackPush v,
    VMStackPushConst N, VMStackCmp CMP_EQ, VMJumpIf CMP_STACK, L » : les FlagSet qui suivent le saut sont ceux des
    autres versions, ceux de L ceux de la version N. {drapeau: (True si version N, N)}."""
    out = {}
    for fl in zone_flows.values():
        labels = {a[0]: i for i, (n, a) in enumerate(fl) if n == 'LABEL'}
        version_vars = {a[0] for n, a in fl if n in VERSION_GETTERS and a}
        for i, (n, a) in enumerate(fl):
            if n != 'VMJumpIf' or i < 4:
                continue
            seq = [x for x in fl[max(0, i - 6):i] if x[0] != 'LABEL'][-3:]
            if len(seq) < 3 or seq[0][0] != 'VMStackPush' or seq[0][1][0] not in version_vars:
                continue
            if seq[1][0] != 'VMStackPushConst' or seq[2][0] != 'VMStackCmp' or seq[2][1][0] not in (1, 'CMP_EQ'):
                continue
            version = seq[1][1][0]
            branches = [(False, i + 1)]
            if a[-1] in labels:
                branches.append((True, labels[a[-1]] + 1))
            for equal, start in branches:
                for name, args in fl[start:start + 20]:
                    if name in STOPS:
                        break
                    if name == 'FlagSet' and isinstance(args[0], int):
                        out[args[0]] = (equal, version)
    return out


def flag_uses(zone_flows):
    """Drapeaux levés, effacés et testés par les scripts."""
    sets, resets, tests = set(), set(), set()
    for fl in zone_flows.values():
        for name, args in fl:
            if not args or not isinstance(args[0], int):
                continue
            if name == 'FlagSet':
                sets.add(args[0])
            elif name == 'FlagReset':
                resets.add(args[0])
            elif name in ('FlagGet', 'VMStackPushFlag'):
                tests.add(args[0])
    return sets, resets, tests


def fallback(found, items, fl, english):
    """Objets que les cartes ne montrent pas (la Forêt Blanche de Blanc dans la ROM de Noir, objets posés par un script) :
    lieu de FlagsEditorEX, traduit par la liste des lieux de PKHeX (anglais → français)."""
    for flag, variants in items.items():
        if flag not in found:
            where = re.sub(r'\s*\((B|W|B2|W2)\)$', '', fl[flag][1])
            if where not in english:
                raise SystemExit(f'Lieu inconnu : {where} (drapeau {flag:#x})')
            found[flag] = dict(zone=None, place=english[where], items=variants)


def result(game, places, zones, trainers, ground, hidden, zone_flows, k, fl, npc_flags):
    return dict(game=game, npc_flags=npc_flags, places=places, zones=zones, trainers=trainers, ground=ground, hidden=hidden,
                gifts=gifts(zone_flows), flows=zone_flows, version_flags=version_flags(zone_flows), uses=flag_uses(zone_flows), k=k, fattard=fl)


# ---------------------------------------------------------------------------------------------------------------------
# Noir 2 / Blanc 2 : pokebw2

def extract_b2w2(root, fattard_path, places_fr, english):
    g = os.path.join(root, 'generated')
    k = {}
    for name in ('flags', 'vars', 'items', 'species', 'trainers', 'zones', 'places', 'moves'):
        k.update(enum_list(os.path.join(g, name + '.txt')))
    # Drapeaux des dresseurs : EVENT_FLAG_TRAINER_X = TRAINER_FLAGS + numéro du dresseur
    order = json.load(open(os.path.join(root, 'res', 'field', 'events', 'order.json'), encoding='utf-8'))
    places = {}
    zones = {}
    zone_flows = {}
    npc_flags = {}  # drapeau d'apparition d'un personnage : sa zone
    trainers = []
    ground = {}
    hidden = {}
    fl = fattard(fattard_path)
    gnames, hnames = field_items(fl)
    gitems = items_of(gnames, k)
    hitems = items_of(hnames, k)
    tnames = {v: n for n, v in enum_list(os.path.join(g, 'trainers.txt')).items()}
    for zid, zname in enumerate(order):
        if zname is None:
            continue
        low = zname[len('ZONE_'):].lower()
        zpath = os.path.join(root, 'res', 'field', 'zones', low + '.json')
        if not os.path.exists(zpath):
            continue
        z = json.load(open(zpath, encoding='utf-8'))
        place = places_fr[k[z['place']]]
        zones[zid] = dict(name=zname, place=place)
        spath = os.path.join(root, 'res', 'field', 'scripts', z['scripts'][len('SCRIPTS_'):].lower() + '.s')
        if os.path.exists(spath):
            zone_flows[zid] = parse_s(open(spath, encoding='utf-8').read(), k)
        epath = os.path.join(root, 'res', 'field', 'events', low + '.json')
        if not os.path.exists(epath):
            continue
        ev = json.load(open(epath, encoding='utf-8'))
        for n in ev.get('npcs', []):
            s = n['script']
            flag = value(n['flag'], k)
            if isinstance(flag, int) and flag:
                npc_flags.setdefault(flag, zid)
            if 3000 <= s < 6000 and 'TRAINER' in n['type']:
                tid = s - 3000 if s < 5000 else s - 5000
                trainers.append(dict(zone=zid, tid=tid, hide=flag, **party_b2w2(root, tnames[tid], k)))
            elif 7000 <= s < 8000 and flag in gitems:
                ground[flag] = dict(zone=zid, place=zones[zid]['place'], items=gitems[flag])
        for b in ev.get('bg_events', []):
            s = b['script']
            if 8000 <= s < 9000:
                flag = HIDDEN_FLAGS['b2w2'] + s - 8000
                if flag in hitems:
                    hidden[flag] = dict(zone=zid, place=zones[zid]['place'], items=hitems[flag])
    fallback(ground, gitems, fl, english)
    fallback(hidden, hitems, fl, english)
    return result('b2w2', places, zones, trainers, ground, hidden, zone_flows, k, fl, npc_flags)


def value(token, k):
    if isinstance(token, int):
        return token
    token = token.strip()
    if re.fullmatch(r'-?(0x[0-9a-fA-F]+|\d+)', token):
        return int(token, 0)
    m = re.fullmatch(r'EVENT_FLAG_TRAINER_(\w+)', token)
    if m and 'TRAINER_' + m.group(1) in k:
        return TRAINER_FLAGS['b2w2'] + k['TRAINER_' + m.group(1)]
    return k.get(token, token)


def parse_s(text, k):
    """Instructions d'un script de pokebw2, dans l'ordre du fichier : [(nom, [arguments])] ; une étiquette donne
    ('LABEL', [nom])."""
    out = []
    for line in text.splitlines():
        line = line.split('//')[0].strip()
        if re.fullmatch(r'\w+:', line):
            out.append(('LABEL', [line[:-1]]))
            continue
        m = re.fullmatch(r'([A-Z]\w*)(?:\s+(.*))?', line)
        if not m:
            continue
        args = [value(a, k) for a in m.group(2).split(',')] if m.group(2) else []
        out.append((m.group(1), args))
    return out


def party_b2w2(root, name, k):
    path = os.path.join(root, 'res', 'trainers', 'data', name[len('TRAINER_'):].lower() + '.json')
    t = json.load(open(path, encoding='utf-8'))
    return dict(cls=t['class'], name=name, team=[(k[p['species']], p['level']) for p in t['party']])


# ---------------------------------------------------------------------------------------------------------------------
# Noir / Blanc : ROM

def extract_bw(rom_path, inc_path, fattard_path, k, english):
    rom = Rom(rom_path)
    table = commands(inc_path)
    headers = rom.narc('a/0/1/2')[0]
    names = texts(rom.narc('a/0/0/2')[89])
    events = rom.narc('a/1/2/5')
    scripts = rom.narc('a/0/5/7')
    trdata = rom.narc('a/0/9/2')
    trpoke = rom.narc('a/0/9/3')
    fl = fattard(fattard_path)
    gnames, hnames = field_items(fl)
    gitems = items_of(gnames, k)
    hitems = items_of(hnames, k)
    zones, zone_flows, trainers, ground, hidden, npc_flags = {}, {}, [], {}, {}, {}
    script_files = {}
    for zid in range(len(headers) // 48):
        h = struct.unpack_from('<24H', headers, zid * 48)
        zones[zid] = dict(name=f'Z{zid}', place=names[h[13] & 0x3FF])
        if h[3] not in script_files:
            script_files[h[3]] = [x for pos, n, v in flow(disassemble(scripts[h[3]], table)) for x in (('LABEL', [pos]), (n, v))]
        zone_flows[zid] = script_files[h[3]]
        e = events[zid] if zid < len(events) else b''
        if len(e) < 8:
            continue
        n_bg, n_npc = e[4], e[5]
        for i in range(n_bg):
            s = struct.unpack_from('<H', e, 8 + i * 20)[0]
            if 8000 <= s < 9000:
                flag = HIDDEN_FLAGS['bw'] + s - 8000
                if flag in hitems:
                    hidden[flag] = dict(zone=zid, place=zones[zid]['place'], items=hitems[flag])
        base = 8 + n_bg * 20
        for i in range(n_npc):
            _, _, _, kind, flag, s = struct.unpack_from('<6H', e, base + i * 36)
            if flag:
                npc_flags.setdefault(flag, zid)
            if 3000 <= s < 6000 and kind != 0:
                tid = s - 3000 if s < 5000 else s - 5000
                trainers.append(dict(zone=zid, tid=tid, hide=flag, **party_bw(trdata[tid], trpoke[tid], tid)))
            elif 7000 <= s < 8000 and flag in gitems:
                ground[flag] = dict(zone=zid, place=zones[zid]['place'], items=gitems[flag])
    # Des zones partagent un fichier de scripts : chaque fichier ne compte qu'une fois (la première zone)
    seen = set()
    for zid in list(zone_flows):
        h = struct.unpack_from('<24H', headers, zid * 48)
        if h[3] in seen:
            zone_flows[zid] = []
        seen.add(h[3])
    fallback(ground, gitems, fl, english)
    fallback(hidden, hitems, fl, english)
    return result('bw', names, zones, trainers, ground, hidden, zone_flows, k, fl, npc_flags)


def party_bw(data, poke, tid):
    """Fiche d'un dresseur de la 5e génération : format (bit 0 : capacités, bit 1 : objets), classe, type de combat,
    nombre de Pokémon ; chaque Pokémon : difficulté, sexe et talent, niveau (16 bits), espèce, forme (+ objet,
    + 4 capacités)."""
    fmt, cls, _, count = data[:4]
    size = 8 + (2 if fmt & 2 else 0) + (8 if fmt & 1 else 0)
    team = []
    for i in range(count):
        _, _, level, species, _ = struct.unpack_from('<BBHHH', poke, i * size)
        team.append((species, level))
    return dict(cls=cls, name=f'TRID_{tid}', team=team)
