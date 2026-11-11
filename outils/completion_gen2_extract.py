"""Lecture des désassemblages pokegold / pokecrystal pour outils/completion_gen2.py (complétion de 2e génération)."""
import glob
import os
import re

from completion_gen1_extract import consts


def blocks(src):
    """Étiquette → son texte, jusqu'à l'étiquette suivante (« Route30Antidote: itemball ANTIDOTE »)."""
    parts = re.split(r'^(\.?\w+):+', src, flags=re.M)
    return list(zip(parts[1::2], parts[2::2]))


def args(line):
    return [a.strip() for a in line.split(';')[0].split(',')]


def extract(root):
    R = lambda *p: os.path.join(root, *p)
    events = consts(R('constants', 'event_flags.asm'))
    items = consts(R('constants', 'item_constants.asm'))
    moves = consts(R('constants', 'move_constants.asm'))
    dex = consts(R('constants', 'pokemon_constants.asm'))  # en 2e génération, numéro interne = numéro national
    tms = []
    for line in open(R('constants', 'item_constants.asm'), encoding='utf-8'):
        m = re.match(r'\s*add_(tm|hm)\s+(\w+)', line)
        if m:
            tms.append((m.group(1), m.group(2)))

    # Drapeaux levés au début de la partie (objets cachés jusqu'à un événement)
    std = open(R('engine', 'events', 'std_scripts.asm'), encoding='utf-8').read()
    init_block = dict(blocks(std)).get('InitializeEventsScript', '')
    initial = set(re.findall(r'setevent (EVENT_\w+)', init_block))

    # Équipes des dresseurs : classe → liste de ses identifiants (JOEY1…) ; groupe de la classe → équipes dans l'ordre
    classes = []
    ids = {}
    cur = None
    for line in open(R('constants', 'trainer_constants.asm'), encoding='utf-8'):
        s = line.split(';')[0].strip()
        m = re.match(r'trainerclass\s+(\w+)', s)
        if m:
            cur = m.group(1)
            classes.append(cur)
            ids[cur] = []
            continue
        m = re.match(r'const\s+(\w+)', s)
        if m and cur:
            ids[cur].append(m.group(1))
    groups = re.findall(r'dw (\w+Group)', open(R('data', 'trainers', 'party_pointers.asm'), encoding='utf-8').read())
    group_of = dict(zip(classes[1:], groups))  # TRAINER_NONE n'a pas de groupe
    parties = {}
    grp = None
    team = None
    for line in open(R('data', 'trainers', 'parties.asm'), encoding='utf-8'):
        s = line.split(';')[0].strip()
        m = re.match(r'(\w+Group):$', s)
        if m:
            grp = m.group(1)
            parties[grp] = []
            continue
        if not grp or not s.startswith('db '):
            continue
        a = args(s[3:])
        if a[0].startswith('"'):
            team = []
            parties[grp].append(team)
        elif a[0] == '-1':
            team = None
        elif team is not None:
            team.append((int(a[0]), a[1]))

    def party(cls, tid):
        try:
            return parties[group_of[cls]][ids[cls].index(tid)]
        except (KeyError, ValueError, IndexError):
            return None

    ground, hidden, trainers, gifts, statics = [], [], [], [], []
    trades_at = {}
    for f in sorted(glob.glob(R('maps', '*.asm'))):
        name = os.path.basename(f)[:-4]
        src = open(f, encoding='utf-8').read()
        labels = blocks(src)
        body = dict(labels)
        # Objets de la carte : type, étiquette, drapeau qui les cache
        for m in re.finditer(r'^\s*object_event\s+(.*)$', src, re.M):
            a = args(m.group(1))
            if len(a) < 13:
                continue
            kind, label, flag = a[9], a[11], a[12]
            text = body.get(label, '')
            if kind == 'OBJECTTYPE_ITEMBALL':
                it = re.search(r'itemball\s+(\w+)(?:,\s*(\d+))?', text)
                if it and flag in events:
                    ground.append(dict(map=name, item=it.group(1), count=int(it.group(2) or 1), flag=flag,
                                       initial=flag in initial))
            elif kind == 'OBJECTTYPE_TRAINER':
                tr = re.search(r'trainer\s+(\w+),\s*(\w+),\s*(EVENT_\w+)', text)
                if tr:
                    trainers.append(dict(map=name, cls=tr.group(1), id=tr.group(2), flag=tr.group(3),
                                         hide=None if flag == '-1' else flag, team=party(tr.group(1), tr.group(2))))
        for label, text in labels:
            for it in re.finditer(r'hiddenitem\s+(\w+),\s*(EVENT_\w+)', text):
                hidden.append(dict(map=name, item=it.group(1), flag=it.group(2)))
            # Cadeaux : un objet donné, puis le drapeau levé juste après (ou vérifié avant, dans le même bloc)
            lines = text.split('\n')
            for i, line in enumerate(lines):
                g = re.match(r'\s*(?:verbosegiveitem|giveitem)\s+(\w+)', line)
                if not g:
                    continue
                after = [re.match(r'\s*setevent\s+(EVENT_\w+)', l) for l in lines[i + 1:i + 8]]
                after = [x.group(1) for x in after if x]
                before = re.findall(r'checkevent\s+(EVENT_\w+)', '\n'.join(lines[:i]))
                ev = after[0] if after else (before[0] if before else None)
                if ev:
                    gifts.append(dict(map=name, item=g.group(1), flag=ev, label=label))
            for t in re.finditer(r'^\s*trade\s+(NPC_TRADE_\w+)', text, re.M):
                trades_at.setdefault(t.group(1), name)
    npc = consts(R('constants', 'npc_trade_constants.asm'))
    trades = []
    for line in open(R('data', 'events', 'npc_trades.asm'), encoding='utf-8'):
        m = re.match(r'\s*npctrade\s+(.*)', line)
        if m:
            a = args(m.group(1))
            trades.append(dict(give=a[1], get=a[2]))
    for k, v in npc.items():
        if k.startswith('NPC_TRADE_') and v < len(trades):
            trades[v]['index'] = v
            trades[v]['map'] = trades_at.get(k)
    # Adresses des octets de « scène » de chaque carte (wXxxSceneID), comptées depuis wEventFlags vers l'arrière.
    lines = open(R('ram', 'wram.asm'), encoding='utf-8').read().split('\n')
    start = [k for k, l in enumerate(lines) if l.startswith('wTradeFlags::')][0]
    sizes = []
    for l in lines[start:]:
        s = l.split(';')[0].strip()
        if s.startswith('wEventFlags::'):
            break
        label = re.match(r'(w\w+)::', s)
        if 'flag_array NUM_NPC_TRADES' in s:
            sizes.append((label.group(1), 1))
        elif re.search(r'\bds (\d+)', s):
            sizes.append((None, int(re.search(r'\bds (\d+)', s).group(1))))
        elif re.search(r'::\s*db\b', s):
            sizes.append((label.group(1), 1))
    total = sum(n for _, n in sizes)
    before_events = {}
    pos = 0
    for label, n in sizes:
        if label:
            before_events[label] = total - pos  # octets entre cette étiquette et wEventFlags
        pos += n
    return dict(before_events=before_events, events=events, items=items, moves=moves, dex=dex, tms=tms, initial=initial, ground=ground,
                hidden=hidden, trainers=trainers, gifts=gifts, trades=trades)


if __name__ == '__main__':
    import sys
    for g in ('pokegold', 'pokecrystal'):
        d = extract(os.path.join(sys.argv[1], g))
        print(g, 'objets', len(d['ground']), 'cachés', len(d['hidden']), 'dresseurs', len(d['trainers']),
              'sans équipe', [t['map'] + ':' + t['id'] for t in d['trainers'] if not t['team']],
              'cadeaux', len(d['gifts']), 'échanges', [(t.get('index'), t['get'], t.get('map')) for t in d['trades']])
        print('  objets cachés au départ', [(x['map'], x['item']) for x in d['ground'] if x['initial']])
