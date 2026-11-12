"""Lecture des désassemblages pokeruby / pokeemerald / pokefirered pour outils/completion_gen3.py (complétion de 3e génération)."""
import glob
import json
import os
import re


def c_constants(paths):
    """Constantes de fichiers C : « #define NOM expression » et énumérations (« enum { A, B = 5, C } »), évaluées."""
    raw = {}
    for path in paths:
        src = open(path, encoding='utf-8', errors='replace').read()
        src = re.sub(r'/\*.*?\*/', '', src, flags=re.S)
        src = re.sub(r'//[^\n]*', '', src)
        for m in re.finditer(r'^\s*#define\s+(\w+)\s+([^\n]+)$', src, re.M):
            raw[m.group(1)] = m.group(2).strip()
        for m in re.finditer(r'\benum\s*\w*\s*\{(.*?)\}', src, re.S):
            value = '0'
            step = 0
            for item in m.group(1).split(','):
                item = item.strip()
                if not item:
                    continue
                if '=' in item:
                    name, value = [x.strip() for x in item.split('=', 1)]
                    step = 0
                else:
                    name = item
                raw.setdefault(name, f'({value}) + {step}')
                step += 1
    values = {}

    def ev(name, depth=0):
        if name in values:
            return values[name]
        if depth > 60 or name not in raw:
            raise KeyError(name)
        expr = raw[name]
        expr = re.sub(r'\b(0x[0-9A-Fa-f]+|\d+)[uUlL]*\b', r'\1', expr)

        def repl(m):
            return str(ev(m.group(0), depth + 1))

        try:
            text = re.sub(r'\b[A-Za-z_]\w*\b', repl, expr)
            values[name] = int(eval(text, {'__builtins__': {}}))  # expressions entières de l'en-tête (+, -, *, <<, ())
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
    """Étiquette d'un script (« Route102_EventScript_ItemPotion:: ») → son texte, jusqu'à la suivante."""
    parts = re.split(r'^(\w+)::?', src, flags=re.M)
    return dict(zip(parts[1::2], parts[2::2]))


def extract(root):
    R = lambda *p: os.path.join(root, *p)
    inc = R('include', 'constants')
    k = c_constants([R('include', 'constants', f) for f in ('flags.h', 'vars.h', 'opponents.h', 'items.h', 'species.h',
                                                          'moves.h', 'trainers.h', 'pokedex.h') if os.path.exists(os.path.join(inc, f))])
    # Numéro interne de l'espèce (Arcko : 277) → numéro national (252), par le nom (SPECIES_X, NATIONAL_DEX_X)
    national = {v: k['NATIONAL_DEX_' + n[8:]] for n, v in k.items() if n.startswith('SPECIES_') and 'NATIONAL_DEX_' + n[8:] in k}
    trainer_start = k.get('TRAINER_FLAGS_START', k.get('TRAINER_FLAG_START'))

    # Scripts : toutes les étiquettes de toutes les cartes et des scripts communs
    scripts = {}
    for f in glob.glob(R('data', '**', '*.inc'), recursive=True) + glob.glob(R('data', '*.s')):
        scripts.update(blocks(open(f, encoding='utf-8', errors='replace').read()))

    # Drapeaux levés au début d'une partie (objets cachés jusqu'à un événement)
    initial = set()
    for label, text in scripts.items():
        if 'ResetAllMapFlags' in label or label.endswith('EventScript_ResetAllMapFlags'):
            initial |= set(re.findall(r'setflag\s+(FLAG_\w+)', text))

    # Équipes : TRAINER_X → étiquette de son équipe → (niveau, espèce interne)
    tfile = next(p for p in (R('src', 'data', 'trainers.h'), R('src', 'data', 'trainers_en.h')) if os.path.exists(p))
    tsrc = open(tfile, encoding='utf-8', errors='replace').read()
    party_of, class_of = {}, {}
    for m in re.finditer(r'\[(TRAINER_\w+)\]\s*=\s*\{(.*?)\n\s*\},', tsrc, re.S):
        body = m.group(2)
        p = re.search(r'(?:sParty_|gTrainerParty_)(\w+)', body)
        c = re.search(r'\.trainerClass\s*=\s*(\w+)', body)
        if p:
            party_of[m.group(1)] = p.group(0)
        if c:
            class_of[m.group(1)] = c.group(1)
    psrc = open(R('src', 'data', 'trainer_parties.h'), encoding='utf-8', errors='replace').read()
    parties = {}
    # Sans « ; » dans le corps : une équipe vide sur une ligne ({DUMMY_TRAINER_MON};) ne déborde pas sur la suivante
    for m in re.finditer(r'\b((?:sParty_|gTrainerParty_)\w+)\[\]\s*=\s*\{([^;]*)\};', psrc):
        # Chaque Pokémon : son niveau, puis son espèce (les capacités, entre accolades, viennent après)
        team = [(int(lvl), national[k[sp]]) for lvl, sp in
                re.findall(r'\.(?:lvl|level)\s*=\s*(\d+)[^}]*?\.species\s*=\s*(SPECIES_\w+)', m.group(2))]
        parties[m.group(1)] = team

    # Rouge Feu / Vert Feuille : objets cachés qui réapparaissent au hasard (leur drapeau change sans cesse) : pas comptés
    rfile = R('src', 'renewable_hidden_items.c')
    renewable = set(re.findall(r'FLAG_HIDDEN_ITEM_\w+', open(rfile, encoding='utf-8').read())) if os.path.exists(rfile) else set()
    ground, hidden, trainers, gifts = [], [], [], []
    seen_trainers = set()
    mapsec = {}
    for f in sorted(glob.glob(R('data', 'maps', '*', 'map.json'))):
        m = json.load(open(f, encoding='utf-8'))
        name = m['name']
        mapsec[name] = m.get('region_map_section')
        for o in m.get('object_events', []):
            script = scripts.get(o.get('script', ''), '')
            flag = o.get('flag', '0')
            if 'ITEM_BALL' in o.get('graphics_id', ''):
                it = re.search(r'finditem\s+(ITEM_\w+)(?:,\s*(\d+))?', script)
                if it and flag in k:
                    ground.append(dict(map=name, item=it.group(1), count=int(it.group(2) or 1), flag=flag))
                continue
            for t in re.finditer(r'trainerbattle_\w+\s+(TRAINER_\w+)', script):
                if t.group(1) in k and t.group(1) not in seen_trainers:
                    seen_trainers.add(t.group(1))
                    trainers.append(dict(map=name, id=t.group(1), cls=class_of.get(t.group(1)), hide=None if flag == '0' else flag,
                                         team=parties.get(party_of.get(t.group(1)), [])))
        for b in m.get('bg_events', []):
            if b.get('type') == 'hidden_item' and b.get('flag') in k and b['flag'] not in renewable:
                hidden.append(dict(map=name, item=b['item'], flag=b['flag']))
        sfile = os.path.join(os.path.dirname(f), 'scripts.inc')
        if not os.path.exists(sfile):
            continue
        src = open(sfile, encoding='utf-8', errors='replace').read()
        for label, text in blocks(src).items():
            # Dresseurs sans objet dresseur (champions, rival, Team Magma / Aqua dans un script)
            for t in re.finditer(r'trainerbattle_\w+\s+(TRAINER_\w+)', text):
                if t.group(1) in k and t.group(1) not in seen_trainers:
                    seen_trainers.add(t.group(1))
                    trainers.append(dict(map=name, id=t.group(1), cls=class_of.get(t.group(1)), hide=None,
                                         team=parties.get(party_of.get(t.group(1)), [])))
            lines = text.split('\n')
            for i, line in enumerate(lines):
                g = re.match(r'\s*(?:giveitem|giveitem_std|giveitem_msg|additem)\b.*?\b(ITEM_\w+)(?:,\s*(\d+))?', line)
                if not g:
                    continue
                after = [re.match(r'\s*setflag\s+(FLAG_\w+)', l) for l in lines[i + 1:i + 8]]
                after = [x.group(1) for x in after if x]
                before = re.findall(r'(?:goto_if_set|checkflag|call_if_set)\s+(FLAG_\w+)', '\n'.join(lines[:i]))
                flag = after[0] if after else (before[0] if before else None)
                if flag and flag in k:
                    gifts.append(dict(map=name, item=g.group(1), count=int(g.group(2) or 1), flag=flag))
    sections = [x['id'] for x in json.load(open(R('src', 'data', 'region_map', 'region_map_sections.json'), encoding='utf-8'))['map_sections']] \
        if os.path.exists(R('src', 'data', 'region_map', 'region_map_sections.json')) else None
    return dict(k=k, national=national, trainer_start=trainer_start, initial=initial, ground=ground, hidden=hidden, trainers=trainers,
                gifts=gifts, mapsec=mapsec, sections=sections)


if __name__ == '__main__':
    import sys
    sys.stdout.reconfigure(encoding='utf-8')
    for g in ('pokeruby', 'pokeemerald', 'pokefirered'):
        d = extract(os.path.join(sys.argv[1], g))
        print(g, 'début dresseurs', d['trainer_start'], 'objets', len(d['ground']), 'cachés', len(d['hidden']),
              'dresseurs', len(d['trainers']), 'sans équipe', sum(1 for t in d['trainers'] if not t['team']),
              'cadeaux', len(d['gifts']), 'au départ', len(d['initial']), 'sections', None if d['sections'] is None else len(d['sections']))
