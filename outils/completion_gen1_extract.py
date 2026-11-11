"""Lecture des désassemblages pokered / pokeyellow pour outils/completion_gen1.py (complétion de 1re génération)."""
import os, re, glob, json, sys


def consts(path):
    v = 0
    out = {}
    in_macro = False
    for line in open(path, encoding='utf-8'):
        line = line.split(';')[0].strip()
        if not line:
            continue
        # Corps des macros (« MACRO add_tm … ENDM ») : leurs « const » ne comptent qu'à l'appel.
        if line.startswith('MACRO'):
            in_macro = True
        if in_macro:
            in_macro = line != 'ENDM'
            continue
        p = re.split(r'[\s,]+', line)
        if p[0] == 'const_def':
            v = (int(p[1][1:], 16) if p[1].startswith('$') else int(p[1])) if len(p) > 1 else 0
        elif p[0] in ('const', 'const_export'):
            out[p[1]] = v
            v += 1
        elif p[0] == 'const_skip':
            v += int(p[1]) if len(p) > 1 else 1
        elif p[0] == 'const_next':
            v = int(p[1][1:], 16) if p[1].startswith('$') else int(p[1])
        elif p[0] == 'add_hm':
            out['HM_' + p[1]] = v
            v += 1
        elif p[0] == 'add_tm':
            out['TM_' + p[1]] = v
            v += 1
    return out


def extract(root):
    R = lambda *p: os.path.join(root, *p)
    events = consts(R('constants', 'event_constants.asm'))
    items = consts(R('constants', 'item_constants.asm'))
    moves = consts(R('constants', 'move_constants.asm'))
    tms = []
    for line in open(R('constants', 'item_constants.asm'), encoding='utf-8'):
        m = re.match(r'\s*add_(tm|hm)\s+(\w+)', line)
        if m:
            tms.append((m.group(1), m.group(2)))
    toggles = []
    for line in open(R('constants', 'toggle_constants.asm'), encoding='utf-8'):
        m = re.match(r'const\s+(TOGGLE_\w+)', line.split(';')[0].strip())
        if m:
            toggles.append(m.group(1))
    # Objets : constante -> (fichier, object_event)
    objconst = {}
    files = {}
    for f in glob.glob(R('data', 'maps', 'objects', '*.asm')):
        names = []
        objs = []
        for line in open(f, encoding='utf-8'):
            s = line.split(';')[0].strip()
            m = re.match(r'const_export\s+(\w+)', s)
            if m:
                names.append(m.group(1))
            m = re.match(r'object_event\s+(.*)', s)
            if m:
                objs.append([a.strip() for a in m.group(1).split(',')])
        base = os.path.basename(f)[:-4]
        files[base] = objs
        for i, n in enumerate(names):
            if i < len(objs):
                objconst[n] = (base, objs[i])
    states = []
    tmap = None
    for line in open(R('data', 'maps', 'toggleable_objects.asm'), encoding='utf-8'):
        s = line.split(';')[0].strip()
        m = re.match(r'toggleable_objects_for\s+(\w+)', s)
        if m:
            tmap = m.group(1)
            continue
        m = re.match(r'toggle_object_state\s+(\w+),\s*(\w+)', s)
        if m:
            states.append((tmap, m.group(1), m.group(2)))
    ground = []
    for i, (mp, oc, st) in enumerate(states):
        f, obj = objconst.get(oc, (None, None))
        if obj and len(obj) >= 7 and obj[2] == 'SPRITE_POKE_BALL' and obj[6] in items:
            ground.append(dict(flag=i, map=mp, toggle=toggles[i], item=obj[6], itemId=items[obj[6]], initial=st))
    blocks = {}
    mp = None
    for line in open(R('data', 'events', 'hidden_events.asm'), encoding='utf-8'):
        s = line.split(';')[0].strip()
        m = re.match(r'hidden_events_for\s+(\w+)', s)
        if m:
            mp = m.group(1)
            continue
        m = re.match(r'hidden_event\s+(\d+),\s*(\d+),\s*(Hidden\w+),\s*(.+)', s)
        if m:
            blocks[(mp, int(m.group(1)), int(m.group(2)))] = (m.group(3), m.group(4).strip())

    def coords(fname, macro):
        out = []
        for line in open(R('data', 'events', fname), encoding='utf-8'):
            m = re.match(r'\s*' + macro + r'\s+(\w+),\s*(\d+),\s*(\d+)', line.split(';')[0])
            if m:
                out.append((m.group(1), int(m.group(2)), int(m.group(3))))
        return out

    hidden = []
    for i, (mp, x, y) in enumerate(coords('hidden_item_coords.asm', 'hidden_item')):
        kind, arg = blocks[(mp, x, y)]
        hidden.append(dict(flag=i, map=mp, item=arg, itemId=items[arg]))
    coins = []
    for i, (mp, x, y) in enumerate(coords('hidden_coins.asm', 'hidden_coin')):
        kind, arg = blocks[(mp, x, y)]
        coins.append(dict(flag=i, map=mp, coins=int(arg.split('+')[1])))
    parties = {}
    cls = None
    for line in open(R('data', 'trainers', 'parties.asm'), encoding='utf-8'):
        s = line.split(';')[0].strip()
        m = re.match(r'(\w+)Data:$', s)
        if m:
            cls = m.group(1)
            parties[cls] = []
            continue
        m = re.match(r'db\s+(.*)', s)
        if m and cls:
            a = [x.strip() for x in m.group(1).split(',')][:-1]
            if a[0] == '$FF':
                team = [(int(a[i]), a[i + 1]) for i in range(1, len(a), 2)]
            else:
                team = [(int(a[0]), sp) for sp in a[1:]]
            parties[cls].append(team)

    def cls_data(opp):
        key = ''.join(w.capitalize() for w in re.sub(r'_TR$', '', opp[4:]).split('_'))  # OPP_PSYCHIC_TR : PsychicData
        for k in parties:
            if k.lower() == key.lower():
                return k
        return None

    species = consts(R('constants', 'pokemon_constants.asm'))
    trainers = []
    for f in sorted(glob.glob(R('scripts', '*.asm'))):
        src = open(f, encoding='utf-8').read()
        headers = dict(re.findall(r'^(\w+TrainerHeader\w*):\s*\n\s*trainer\s+(EVENT_\w+)', src, re.M))
        if not headers:
            continue
        text_to_header = {}
        # Bloc de chaque étiquette, jusqu'à la suivante (« Route3Youngster1Text: … ld hl, Route3TrainerHeader0 »).
        parts = re.split(r'^(\w+):', src, flags=re.M)
        for label, body in zip(parts[1::2], parts[2::2]):
            h = re.search(r'ld hl, (\w+TrainerHeader\w*)', body)
            if label.endswith('Text') and h:
                text_to_header[label] = h.group(1)
        textconst = dict((b, a) for a, b in re.findall(r'dw_const\s+(\w+),\s*(TEXT_\w+)', src))
        base = os.path.basename(f)[:-4]
        found = set()
        for o in files.get(base, []):
            if len(o) >= 8 and o[6] in species and o[5] in textconst:
                h = text_to_header.get(textconst[o[5]])
                if h:
                    ev = headers[h]
                    trainers.append(dict(event=ev, flag=events[ev], file=base, species=o[6], level=int(o[7]), team=None))
                    found.add(ev)
                continue
            if len(o) >= 8 and o[6].startswith('OPP_') and o[5] in textconst:
                h = text_to_header.get(textconst[o[5]])
                if not h:
                    continue
                ev = headers[h]
                k = cls_data(o[6])
                n = int(o[7])
                team = parties[k][n - 1] if k and n <= len(parties[k]) else None
                trainers.append(dict(event=ev, flag=events[ev], file=base, cls=o[6], n=n, team=team))
                found.add(ev)
        for h, ev in headers.items():
            if ev not in found:
                trainers.append(dict(event=ev, flag=events[ev], file=base, cls=None, n=None, team=None))
    trades = []
    for line in open(R('data', 'events', 'trades.asm'), encoding='utf-8'):
        m = re.match(r'\s*npctrade\s+(\w+),\s*(\w+),\s*\w+,\s*"(\w+)"\s*;\s*(.*)', line)
        if m:
            trades.append(dict(index=len(trades), give=m.group(1), get=m.group(2), nick=m.group(3), where=m.group(4)))
    dex = consts(R('constants', 'pokedex_constants.asm'))
    # Arène de Cramois'Île : ses dresseurs (quiz, sans en-tête) dans l'ordre des objets, après Auguste.
    cinnabar = []
    for o in files['CinnabarGym']:
        if len(o) >= 8 and o[6].startswith('OPP_') and o[6] != 'OPP_BLAINE':
            cinnabar.append(parties[cls_data(o[6])][int(o[7]) - 1])
    # Cadeaux : le premier script qui lève chaque drapeau EVENT_GOT_.
    skip = {'EVENT_GOT_STARTER', 'EVENT_GOT_POKEDEX', 'EVENT_GOT_OAKS_PARCEL', 'EVENT_GOT_SS_TICKET', 'EVENT_GOT_DOME_FOSSIL',
            'EVENT_GOT_HELIX_FOSSIL', 'EVENT_GOT_HITMONLEE', 'EVENT_GOT_HITMONCHAN', 'EVENT_GOT_BULBASAUR_IN_CERULEAN',
            'EVENT_GOT_CHARMANDER_ON_ROUTE_24', 'EVENT_GOT_SQUIRTLE_FROM_OFFICER_JENNY'}
    gifts = {}
    for f in sorted(glob.glob(R('scripts', '*.asm'))):
        src = open(f, encoding='utf-8').read()
        for m in re.finditer(r'(?:SetEvent|SetEventReuseHL|CheckAndSetEvent|SetEvents)\s+([\w, ]+)', src):
            for name in re.findall(r'EVENT_GOT_\w+', m.group(1)):
                if name not in skip and name not in gifts:
                    gifts[name] = (os.path.basename(f)[:-4], None)
    gifts = dict(sorted(gifts.items(), key=lambda kv: events[kv[0]]))
    tm_moves = {'TM': [m for k, m in tms if k == 'tm'], 'HM': [m for k, m in tms if k == 'hm']}
    return dict(cinnabar=cinnabar, gifts=gifts, tm_moves=tm_moves,events=events, ground=ground, hidden=hidden, coins=coins, trainers=trainers, trades=trades,
                tms=tms, moves=moves, items=items, dex=dex, toggles=toggles, states=states, objfiles=sorted(files))


if __name__ == '__main__':
    out = {g: extract(os.path.join(sys.argv[1], g)) for g in ('pokered', 'pokeyellow')}
    json.dump(out, open('data.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    for g, d in out.items():
        print(g, 'ground', len(d['ground']), 'hidden', len(d['hidden']), 'coins', len(d['coins']),
              'trainers', len(d['trainers']), 'noobj', [t['event'] for t in d['trainers'] if t.get('cls') is None and not t.get('species')],
              'trades', len(d['trades']))
