"""
Lecture des désassemblages pokeplatinum, pokeheartgold et pokediamond pour outils/completion_gen4.py (complétion de
4e génération). Chaque lecteur rend le même dictionnaire :

    k         constantes (nom → valeur : drapeaux, objets, espèces, dresseurs…)
    trainers  dresseurs : carte, identifiant, classe, drapeau qui le cache, équipe [(niveau, espèce)], numéro
    ground    objets au sol : carte, objet, quantité, drapeau
    hidden    objets cachés : carte, objet, quantité, drapeau
    gifts     cadeaux : carte, objet, quantité, drapeau levé après le don
    mapsec    carte → numéro de lieu (celui des listes de lieux de PKHeX)
    initial   drapeaux levés au début d'une partie
    trainer_start  premier drapeau des dresseurs (+ numéro du dresseur)
    tm_moves  capacité de chaque CT, puis de chaque CS (noms MOVE_X)

Platine et HeartGold / SoulSilver sont désassemblés en texte ; Diamant / Perle ne l'est pas : ses événements de carte
et ses scripts sont lus dans les fichiers binaires du jeu (files/fielddata/…/narc_XXXX.bin), voir extract_dp.
"""
import glob
import json
import os
import re
import struct


# ---------------------------------------------------------------------------------------------------------------------
# Constantes

def enum_txt(path, values):
    """Fichier « generated/*.txt » de pokeplatinum : une constante par ligne, « NOM » ou « NOM = expression », numérotées
    comme une énumération C."""
    current = 0
    for line in open(path, encoding='utf-8'):
        line = re.sub(r'//.*|#.*', '', line).strip()
        if not line:
            continue
        if '=' in line:
            name, expr = [x.strip() for x in line.split('=', 1)]
            expr = re.sub(r'\b[A-Za-z_]\w*\b', lambda m: str(values[m.group(0)]), expr)
            current = int(eval(expr, {'__builtins__': {}}))
        else:
            name = line
        values[name] = current
        current += 1
    return values


def c_defines(paths, values=None):
    """« #define NOM expression » de fichiers C, évalués (expressions entières simples)."""
    raw = {}
    for path in paths:
        src = open(path, encoding='utf-8', errors='replace').read()
        src = re.sub(r'/\*.*?\*/', '', src, flags=re.S)
        src = re.sub(r'//[^\n]*', '', src)
        for m in re.finditer(r'^[ \t]*#define[ \t]+(\w+)[ \t]+([^\n]+)$', src, re.M):
            raw[m.group(1)] = m.group(2).strip()
    values = {} if values is None else values

    def ev(name, depth=0):
        if name in values:
            return values[name]
        if depth > 40 or name not in raw:
            raise KeyError(name)
        expr = re.sub(r'\b(0x[0-9A-Fa-f]+|\d+)[uUlL]*\b', r'\1', raw[name])
        try:
            text = re.sub(r'\b[A-Za-z_]\w*\b', lambda m: str(ev(m.group(0), depth + 1)), expr)
            values[name] = int(eval(text, {'__builtins__': {}}))
        except Exception:
            raise KeyError(name)
        return values[name]

    for name in list(raw):
        try:
            ev(name)
        except KeyError:
            pass
    return values


def blocks(src):
    """Étiquette d'un script (« Route201_Potion: ») → son texte, jusqu'à la suivante."""
    parts = re.split(r'^(\w+):', src, flags=re.M)
    return dict(zip(parts[1::2], parts[2::2]))


def gifts_in(lines, k, set_flag, before_flag, item_re, map_name, out):
    """Cadeaux d'un script : l'objet donné (« SetVar VAR_0x8004, ITEM_X » puis la quantité), puis le drapeau levé juste
    après (sinon celui qu'on vérifie avant)."""
    for i, line in enumerate(lines):
        g = re.match(item_re, line)
        if not g:
            continue
        item = g.group(1)
        qty = 1
        nxt = '\n'.join(lines[i + 1:i + 3])
        q = re.search(r'x8005,\s*(\d+)', nxt) or re.search(re.escape(item) + r',\s*(\d+)', line)
        if q:
            qty = int(q.group(1))
        after = [re.match(set_flag, x) for x in lines[i + 1:i + 9]]
        after = [x.group(1) for x in after if x]
        before = re.findall(before_flag, '\n'.join(lines[max(0, i - 40):i]))
        flag = after[0] if after else (before[-1] if before else None)
        if flag and flag in k and item in k:
            out.append(dict(map=map_name, item=item, count=qty, flag=flag))


# ---------------------------------------------------------------------------------------------------------------------
# Platine

def extract_pt(root):
    R = lambda *p: os.path.join(root, *p)
    k = {}
    for name in ('vars_flags', 'trainers', 'items', 'species', 'moves', 'trainer_classes'):
        enum_txt(R('generated', f'{name}.txt'), k)

    # Lieux : le nom de la carte (header.json) est un texte de location_names.json, dont le rang est le numéro du lieu
    loc_ids = [m['id'] for m in json.load(open(R('res', 'text', 'location_names.json'), encoding='utf-8'))['messages']]

    # Équipes et classes des dresseurs : res/trainers/data/<identifiant en minuscules>.json
    parties, classes = {}, {}
    for f in glob.glob(R('res', 'trainers', 'data', '*.json')):
        t = json.load(open(f, encoding='utf-8'))
        tid = 'TRAINER_' + os.path.basename(f)[:-5].upper()
        classes[tid] = t['class']
        parties[tid] = [(p['level'], k[p['species']]) for p in t['party'] if p['species'] in k]

    # Objets au sol : VisibleItems_X → objet et quantité (res/field/scripts/visible_items.s)
    visible = {}
    for label, text in blocks(open(R('res', 'field', 'scripts', 'visible_items.s'), encoding='utf-8').read()).items():
        it = re.search(r'VAR_0x8008,\s*(ITEM_\w+)', text)
        qt = re.search(r'VAR_0x8009,\s*(\d+)', text)
        if it:
            visible[label] = (it.group(1), int(qt.group(1)) if qt else 1)

    # Objets cachés : drapeau → objet et quantité (include/data/field/hidden_items.h)
    hidden_of = {m.group(3): (m.group(1), int(m.group(2))) for m in re.finditer(
        r'HIDDEN_ITEM_ENTRY\((ITEM_\w+),\s*(\d+),\s*\d+,\s*(FLAG_\w+)\)',
        open(R('include', 'data', 'field', 'hidden_items.h'), encoding='utf-8').read())}

    initial = set(re.findall(r'SetFlag\s+(FLAG_\w+)', open(R('res', 'field', 'scripts', 'init_new_game.s'), encoding='utf-8').read()))
    # Drapeaux qu'un script efface : le personnage peut réapparaître (selon l'heure, le jour…)
    cleared = set()
    for f in glob.glob(R('res', 'field', '**', '*.s'), recursive=True):
        cleared |= set(re.findall(r'ClearFlag\s+(FLAG_\w+)', open(f, encoding='utf-8').read()))

    trainers, ground, hidden, gifts, mapsec = [], [], [], [], {}
    seen = set()

    def trainer(map_name, tid, hide, obj=True):
        if tid in k and tid not in seen and tid != 'TRAINER_NONE':
            seen.add(tid)
            trainers.append(dict(map=map_name, id=tid, cls=classes.get(tid), hide=hide, team=parties.get(tid, []), num=k[tid], object=obj))

    for d in sorted(glob.glob(R('res', 'field', 'maps', '*', 'header.json'))):
        d = os.path.dirname(d)
        name = os.path.basename(d)
        header = json.load(open(os.path.join(d, 'header.json'), encoding='utf-8'))
        if header.get('locationName') in loc_ids:
            mapsec[name] = loc_ids.index(header['locationName'])
        efile = os.path.join(d, 'events.json')  # certaines cartes (Monde Distorsion) n'en ont pas
        ev = json.load(open(efile, encoding='utf-8')) if os.path.exists(efile) else {}
        for o in ev.get('object_events', []):
            flag = o.get('hidden_flag', '0')
            hide = None if flag in ('0', 0) else flag
            if o.get('script', '') in visible:
                item, qty = visible[o['script']]
                if hide in k:
                    ground.append(dict(map=name, item=item, count=qty, flag=hide))
            elif 'trainer' in o:
                trainer(name, o['trainer']['id'], hide)
        for b in ev.get('bg_events', []):
            if 'hidden_item' in b and b['hidden_item'] in hidden_of and b['hidden_item'] in k:
                item, qty = hidden_of[b['hidden_item']]
                hidden.append(dict(map=name, item=item, count=qty, flag=b['hidden_item']))
        sfile = os.path.join(d, 'scripts.s')
        if not os.path.exists(sfile):
            continue
        src = open(sfile, encoding='utf-8').read()
        for t in re.findall(r'(?:StartTrainerBattle|StartTagBattle|StartFirstBattle)\s+(TRAINER_\w+)(?:,\s*(TRAINER_\w+))?', src):
            for tid in t:
                if tid:
                    trainer(name, tid, None, False)
        for text in blocks(src).values():
            gifts_in(text.split('\n'), k, r'\s*SetFlag\s+(FLAG_\w+)', r'GoToIfSet\s+(FLAG_\w+)',
                     r'\s*SetVar\s+VAR_0x8004,\s*(ITEM_\w+)', name, gifts)

    tm_moves = []
    for kind, count in (('tm', 92), ('hm', 8)):
        for n in range(1, count + 1):
            tm_moves.append(json.load(open(R('res', 'items', 'data', f'{kind}{n:02}.json'), encoding='utf-8'))['teachesMove'])
    return dict(k=k, trainers=trainers, ground=ground, hidden=hidden, gifts=gifts, mapsec=mapsec, initial=initial,
                trainer_start=k['TRAINER_DEFEATED_FLAGS_START'], tm_moves=tm_moves, cleared={k[f] for f in cleared if f in k})


# ---------------------------------------------------------------------------------------------------------------------
# HeartGold / SoulSilver

def extract_hgss(root):
    R = lambda *p: os.path.join(root, *p)
    inc = R('include', 'constants')
    k = c_defines([os.path.join(inc, f) for f in ('flags.h', 'vars.h', 'items.h', 'species.h', 'moves.h', 'trainers.h',
                                                   'trainer_class.h', 'std_script.h', 'hidden_items.h', 'map_sections.h')])
    by_value = {}
    for name, value in k.items():
        if name.startswith('TRAINER_') and not name.startswith('TRAINER_FLAG'):
            by_value.setdefault(value, name)

    # Équipes : trainers.json, une entrée par numéro de dresseur
    parties, classes = {}, {}
    for num, t in enumerate(json.load(open(R('files', 'poketool', 'trainer', 'trainers.json'), encoding='utf-8'))['trainers']):
        tid = by_value.get(num)
        if tid:
            classes[tid] = t['class']
            parties[tid] = [(p['level'], k[p['species']]) for p in t['party'] if p['species'] in k]

    # Lieu de chaque carte : son fichier d'événements (« 030_R29 ») dans la table des cartes
    mapsec = {}
    for m in re.finditer(r'\.eventsBank\s*=\s*NARC_zone_event_\d+_(\w+?)_bin,\s*\.mapsec\s*=\s*(MAPSEC_\w+)',
                         open(R('src', 'data', 'map_headers.h'), encoding='utf-8').read()):
        mapsec.setdefault(m.group(1), k[m.group(2)])

    # Objets au sol : script 7000 + n → scr_seq_0141_n (« SetVar VAR_SPECIAL_x8008, numéro de l'objet »)
    items_by_value = {v: n for n, v in k.items() if n.startswith('ITEM_')}
    balls = {}
    for label, text in blocks(open(R('files', 'fielddata', 'script', 'scr_seq', 'scr_seq_0141.s'), encoding='utf-8').read()).items():
        m = re.fullmatch(r'scr_seq_0141_(\d+)', label)
        it = re.search(r'x8008,\s*(\d+)', text)
        qt = re.search(r'x8009,\s*(\d+)', text)
        if m and it:
            balls[k['_std_item_ball'] + int(m.group(1))] = (items_by_value[int(it.group(1))], int(qt.group(1)) if qt else 1)

    # Objets cachés : sHiddenItemParam = {objet, quantité, …, numéro} ; drapeau FLAG_HIDDENITEM_X (numéro HIDDENITEM_X)
    hidden_param = {}
    for m in re.finditer(r'\{\s*(ITEM_\w+),\s*(\d+),\s*\d+,\s*\d+,\s*(\d+)\s*\}',
                         open(R('src', 'data', 'fieldmap', 'hidden_items.h'), encoding='utf-8').read()):
        hidden_param[int(m.group(3))] = (m.group(1), int(m.group(2)))

    initial = set(re.findall(r'SetFlag\s+(FLAG_\w+)', open(R('files', 'fielddata', 'script', 'scr_seq', 'scr_seq_0149.s'),
                                                         encoding='utf-8').read()))
    cleared = set()  # drapeaux qu'un script efface : le personnage peut réapparaître
    for f in glob.glob(R('files', 'fielddata', 'script', 'scr_seq', '*.s')):
        cleared |= set(re.findall(r'ClearFlag\s+(FLAG_\w+)', open(f, encoding='utf-8').read()))

    trainers, ground, hidden, gifts = [], [], [], []
    seen = set()

    def trainer(map_name, tid, hide, obj=True):
        if tid in k and tid not in seen and tid != 'TRAINER_NONE':
            seen.add(tid)
            trainers.append(dict(map=map_name, id=tid, cls=classes.get(tid), hide=hide, team=parties.get(tid, []), num=k[tid], object=obj))

    for f in sorted(glob.glob(R('files', 'fielddata', 'eventdata', 'zone_event', '*.json'))):
        name = re.sub(r'^\d+_', '', os.path.basename(f)[:-5])
        ev = json.load(open(f, encoding='utf-8'))
        for o in ev.get('objects', []):
            script = str(o.get('scriptId', ''))
            flag = o.get('eventFlag', 'FLAG_NOTHING')
            hide = None if flag in ('FLAG_NOTHING', 0, '0') else flag
            t = re.fullmatch(r'std_trainer\((TRAINER_\w+)\)', script)
            if t:
                trainer(name, t.group(1), hide)
            elif script.startswith('std_itemball_') and k.get(script) in balls and hide in k:
                item, qty = balls[k[script]]
                ground.append(dict(map=name, item=item, count=qty, flag=hide))
        for b in ev.get('bgs', []):
            script = str(b.get('scriptId', ''))
            if script.startswith('std_hiddenitem_'):
                key = script[len('std_'):].upper()  # HIDDENITEM_R29_R30_T21_NUGGET
                if key in k and k[key] in hidden_param and 'FLAG_' + key in k:
                    item, qty = hidden_param[k[key]]
                    hidden.append(dict(map=name, item=item, count=qty, flag='FLAG_' + key))

    for f in sorted(glob.glob(R('files', 'fielddata', 'script', 'scr_seq', 'scr_seq_*_*.s'))):
        name = re.match(r'scr_seq_\d+_(\w+)\.s', os.path.basename(f)).group(1)
        src = open(f, encoding='utf-8').read()
        for tid in re.findall(r'TrainerBattle\s+(TRAINER_\w+)', src):
            trainer(name, tid, None, False)
        for text in blocks(src).values():
            gifts_in(text.split('\n'), k, r'\s*SetFlag\s+(FLAG_\w+)', r'GoToIfSet\s+(FLAG_\w+)',
                     r'\s*(?:SetVar\s+VAR_SPECIAL_x8004,|GoToIfNoItemSpace)\s*(ITEM_\w+)', name, gifts)

    # Capacités des CT et CS : les mêmes que Platine (lues là-bas)
    return dict(k=k, trainers=trainers, ground=ground, hidden=hidden, gifts=gifts, mapsec=mapsec, initial=initial,
                trainer_start=k['TRAINER_FLAG_BASE'], tm_moves=None, cleared={k[f] for f in cleared if f in k})


# ---------------------------------------------------------------------------------------------------------------------
# Diamant / Perle : fichiers binaires du jeu

DP_HIDDEN_FLAGS_START = 730    # drapeau d'un objet caché = 730 + son numéro (script 8000 + numéro ; arm9, sub_02039694)
DP_TRAINER_FLAGS_START = 0x550  # drapeau d'un dresseur = 0x550 + son numéro (TrainerFieldSystem_FlagCheck)
# Commandes de script (numéros communs à Diamant / Perle et Platine)
OP_SETFLAG, OP_CHECKFLAG, OP_SETVAR, OP_GIVEITEM, OP_TRAINERBATTLE = 0x1E, 0x20, 0x28, 0x7B, 0xE5


def dp_scripts(data):
    """Scripts d'un fichier compilé : décalages (u32, relatifs à la fin de chacun) jusqu'au marqueur 0xFD13."""
    starts, pos = [], 0
    while pos + 2 <= len(data) and struct.unpack_from('<H', data, pos)[0] != 0xFD13:
        starts.append(pos + 4 + struct.unpack_from('<i', data, pos)[0])
        pos += 4
    return starts


def dp_events(data):
    """Événements d'une carte : décors (20 octets), objets (32 octets) ; les passages et déclencheurs ne servent pas."""
    pos = 0
    bgs, objs = [], []
    n, = struct.unpack_from('<I', data, pos)
    pos += 4
    for _ in range(n):
        script, typ = struct.unpack_from('<HH', data, pos)
        bgs.append(dict(script=script, type=typ))
        pos += 20
    n, = struct.unpack_from('<I', data, pos)
    pos += 4
    for _ in range(n):
        f = struct.unpack_from('<HHHHHHhHHHhhHHi', data, pos)
        objs.append(dict(gfx=f[1], ttype=f[3], flag=f[4], script=f[5]))
        pos += 32
    return bgs, objs


def dp_find(data, op, *args):
    """Positions d'une commande (u16) suivie de ces arguments (u16, None = n'importe lequel)."""
    found = []
    pat = struct.pack('<H', op)
    i = data.find(pat)
    while i >= 0:
        ok = i + 2 + 2 * len(args) <= len(data)
        values = list(struct.unpack_from('<' + 'H' * len(args), data, i + 2)) if ok else []
        if ok and all(a is None or a == v for a, v in zip(args, values)):
            found.append((i, values))
        i = data.find(pat, i + 1)
    return found


def extract_dp(root, pt):
    """Diamant / Perle. <pt> : les constantes de Platine (mêmes numéros d'objets, d'espèces, de capacités et de
    dresseurs en 4e génération)."""
    R = lambda *p: os.path.join(root, *p)
    k = c_defines([R('include', 'constants', f) for f in ('map_sections.h', 'trainers.h')])
    for name, value in pt.items():
        if name.startswith(('ITEM_', 'SPECIES_', 'MOVE_')):
            k.setdefault(name, value)
    items_by_value = {v: n for n, v in k.items() if n.startswith('ITEM_')}
    trainer_names = {v: n for n, v in k.items() if n.startswith('TRAINER_')}

    def narc(kind, n):
        sub = {'scr': ('script', 'scr_seq_release'), 'ev': ('eventdata', 'zone_event_release')}[kind]
        return open(R('files', 'fielddata', *sub, f'narc_{n:04}.bin'), 'rb').read()

    # Cartes : fichier de scripts, fichier d'événements et lieu (arm9/src/map_header.c)
    headers = []
    for m in re.finditer(r'\{([^{}]*NARC_zone_event_release_narc_(\d+)_bin[^{}]*)\},\s*//\s*MAP_(\w+)',
                         open(R('arm9', 'src', 'map_header.c'), encoding='utf-8').read()):
        scr = re.findall(r'NARC_scr_seq_release_narc_(\d+)_bin', m.group(1))
        sec = re.search(r'(MAPSEC_\w+)', m.group(1))
        headers.append(dict(map=m.group(3), scripts=int(scr[0]), events=int(m.group(2)),
                            mapsec=k.get(sec.group(1)) if sec else None))

    # Dresseurs : files/poketool/trainer/trdata.json (numéro, classe, équipe)
    parties, classes = {}, {}
    for t in json.load(open(R('files', 'poketool', 'trainer', 'trdata.json'), encoding='utf-8'))['trdata']:
        tid = trainer_names.get(t['index'])
        if tid:
            classes[tid] = t['class']
            parties[tid] = [(p['level'], k[p['species']]) for p in t['party'] if p['species'] in k]

    # Objets au sol : script 7000 + n → n-ième script du fichier des objets (« SetVar 0x8008, objet ; SetVar 0x8009, quantité »)
    ball_file = narc('scr', 370)
    balls = {}
    for n, start in enumerate(dp_scripts(ball_file)):
        it = dp_find(ball_file[start:start + 16], OP_SETVAR, 0x8008, None)
        qt = dp_find(ball_file[start:start + 16], OP_SETVAR, 0x8009, None)
        if it and it[0][0] == 0:
            balls[7000 + n] = (items_by_value.get(it[0][1][1]), qt[0][1][1] if qt else 1)

    # Objets cachés : table de l'arm9 (UNK_020F2DB4) {objet u16, quantité u8, portée u8, 0 u16, numéro u16}
    table = re.search(r'UNK_020F2DB4:[^\n]*\n((?:\s*\.(?:short|byte|word)[^\n]*\n)+)',
                      open(R('arm9', 'asm', 'unk_02038C78.s'), encoding='utf-8').read()).group(1)
    raw = bytearray()
    for line in table.split('\n'):
        mm = re.match(r'\s*\.(short|byte|word)\s+(.*)', line)
        if mm:
            size = {'short': 2, 'byte': 1, 'word': 4}[mm.group(1)]
            for v in mm.group(2).split(','):
                raw += int(v.split(';')[0].strip(), 0).to_bytes(size, 'little')
    hidden_param = {}
    for i in range(0, len(raw) - 7, 8):
        item, qty, _, _, index = struct.unpack_from('<HBBHH', raw, i)
        hidden_param[index] = (items_by_value.get(item), qty)

    # Drapeaux levés au début d'une partie : le fichier de scripts qui en lève le plus ; drapeaux qu'un script efface
    # (un personnage qui réapparaît, selon l'heure par exemple)
    initial, best, cleared = set(), 0, set()
    for f in glob.glob(R('files', 'fielddata', 'script', 'scr_seq_release', '*.bin')):
        data = open(f, 'rb').read()
        flags = {v[0] for _, v in dp_find(data, OP_SETFLAG, None)}
        cleared |= {v[0] for _, v in dp_find(data, OP_SETFLAG + 1, None)}  # ClearFlag (0x1F)
        if len(flags) > best:
            best, initial = len(flags), flags

    trainers, ground, hidden, gifts, mapsec, scripts_of = [], [], [], [], {}, {}
    seen = set()

    def trainer(map_name, num, hide, obj=True):
        tid = trainer_names.get(num)
        if tid and tid not in seen:
            seen.add(tid)
            trainers.append(dict(map=map_name, id=tid, cls=classes.get(tid), hide=hide, team=parties.get(tid, []), num=num, object=obj))

    for h in headers:
        name = h['map']
        if h['mapsec'] is not None:
            mapsec.setdefault(name, h['mapsec'])
        bgs, objs = dp_events(narc('ev', h['events']))
        for o in objs:
            s, hide = o['script'], o['flag'] or None
            if 3000 <= s < 5000:
                trainer(name, s - 3000 + 1, hide)
            elif 5000 <= s < 7000:
                trainer(name, s - 5000 + 1, hide)
            elif 7000 <= s < 8000 and s in balls and balls[s][0] and hide:
                item, qty = balls[s]
                ground.append(dict(map=name, item=item, count=qty, flag=hide))
        for b in bgs:
            if 8000 <= b['script'] < 8800 and b['script'] - 8000 in hidden_param:
                item, qty = hidden_param[b['script'] - 8000]
                if item:
                    hidden.append(dict(map=name, item=item, count=qty, flag=DP_HIDDEN_FLAGS_START + b['script'] - 8000))
        data = narc('scr', h['scripts'])
        scripts_of.setdefault(name, data)
        for _, (num, _) in dp_find(data, OP_TRAINERBATTLE, None, None):
            if 0 < num < 1000:
                trainer(name, num, None, False)
        # Cadeaux : « SetVar 0x8004, objet » (puis la quantité dans 0x8005), puis le premier SetFlag qui suit
        for pos, (_, item) in dp_find(data, OP_SETVAR, 0x8004, None):
            if item not in items_by_value or item == 0:
                continue
            qty = dp_find(data[pos + 6:pos + 12], OP_SETVAR, 0x8005, None)
            after = dp_find(data[pos:pos + 60], OP_SETFLAG, None)
            if after:
                gifts.append(dict(map=name, item=items_by_value[item], count=qty[0][1][1] if qty else 1, flag=after[0][1][0]))
    return dict(k=k, trainers=trainers, ground=ground, hidden=hidden, gifts=gifts, mapsec=mapsec, initial=initial,
                trainer_start=DP_TRAINER_FLAGS_START, tm_moves=None, scripts=scripts_of, cleared=cleared)
