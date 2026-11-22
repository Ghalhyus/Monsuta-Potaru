"""
Génère MonsutaPotaru/Services/Cobblemon/AllTheMonsCompletionData.cs : les quêtes (FTB Quests) et les succès
(advancements) du modpack All the Mons, pour la complétion All the Mons.

    python outils/completion_allthemons.py "<instance All the Mons>" MonsutaPotaru/Services/Cobblemon/AllTheMonsCompletionData.cs

<instance> : le dossier de l'instance CurseForge (« .../curseforge/minecraft/Instances/All the Mons - ATMons »). Le jar
de Minecraft et ses traductions sont cherchés à côté, dans « .../curseforge/minecraft/Install ». Node.js est nécessaire
(succès créés par un script kubejs du modpack, exécuté avec des fonctions simulées).

Lu dans l'instance :
- quêtes : config/ftbquests/quests (chapitres, groupes, traductions fr_fr puis en_us) ;
- succès : data/<mod>/advancement(s)/**.json des jars des mods, du jar de Minecraft, des datapacks et de kubejs, plus
  ceux du script kubejs/server_scripts/modpack/advancements.js. Un succès compte s'il s'affiche dans l'écran des progrès :
  il a un « display » et remonte à une racine qui en a un (les recettes, sans racine affichée, sont écartées), et ses
  conditions NeoForge sont remplies (mod présent).

Titres seulement (jamais les descriptions : les quêtes d'All the Mods sont « Tous droits réservés ») : en français
quand le modpack ou le mod les traduit, sinon en anglais. Une quête sans titre prend celui de sa première tâche, comme
dans le jeu (nom de l'objet, du monstre, du succès, de la structure...).
"""
import glob
import io
import json
import os
import re
import subprocess
import sys
import zipfile

# ---------- SNBT (format des fichiers de FTB Quests) ----------

TOKEN = re.compile(r'\s*(?:(?P<str>"(?:[^"\\]|\\.)*")|(?P<punct>[{}\[\]:,;])|(?P<atom>[^\s{}\[\]:,;"]+))')


def snbt_tokens(text):
    pos, n = 0, len(text)
    while pos < n:
        m = TOKEN.match(text, pos)
        if not m:
            if text[pos:].strip() == '':
                break
            raise ValueError(f'SNBT illisible à {pos} : {text[pos:pos + 30]!r}')
        pos = m.end()
        if m.group('str') is not None:
            yield 's', re.sub(r'\\(.)', lambda x: {'n': '\n', 't': '\t'}.get(x.group(1), x.group(1)), m.group('str')[1:-1])
        elif m.group('punct') is not None:
            yield 'p', m.group('punct')
        elif m.group('atom') is not None:
            yield 'a', m.group('atom')


def snbt_atom(a):
    if a in ('true', 'false'):
        return a == 'true'
    if re.fullmatch(r'-?\d+[LbBsS]?', a):
        return int(a.rstrip('LbBsS'))
    if re.fullmatch(r'-?\d*\.?\d+(?:[eE][-+]?\d+)?[dDfF]?', a):
        return float(a.rstrip('dDfF'))
    return a


def snbt(text):
    toks = list(snbt_tokens(text))
    i = 0

    def value():
        nonlocal i
        k, v = toks[i]
        i += 1
        if (k, v) == ('p', '{'):
            d = {}
            while True:
                k2, v2 = toks[i]
                if (k2, v2) == ('p', '}'):
                    i += 1
                    return d
                if (k2, v2) == ('p', ','):
                    i += 1
                    continue
                i += 1
                if toks[i] != ('p', ':'):
                    raise ValueError(f'« : » attendu après {v2}')
                i += 1
                d[v2] = value()
        if (k, v) == ('p', '['):
            items = []
            if toks[i][0] == 'a' and toks[i][1] in ('I', 'L', 'B') and toks[i + 1] == ('p', ';'):
                i += 2
            while True:
                k2, v2 = toks[i]
                if (k2, v2) == ('p', ']'):
                    i += 1
                    return items
                if (k2, v2) == ('p', ','):
                    i += 1
                    continue
                items.append(value())
        return v if k == 's' else snbt_atom(v)

    return value()


def read_snbt(path):
    with open(path, encoding='utf-8') as f:
        return snbt(f.read())


# ---------- Textes ----------

def clean(text):
    """Retire les codes de mise en forme de Minecraft (&6, §l) et les échappements de FTB Quests (\\&)."""
    text = re.sub(r'(?<!\\)[&§][0-9a-fk-orA-FK-OR]', '', text)
    text = text.replace('\\&', '&')
    return re.sub(r'\s+', ' ', text).strip()


def cs(text):
    return '"' + text.replace('\\', '\\\\').replace('"', '\\"') + '"'


class Lang:
    """Traductions des mods (fr_fr, puis en_us)."""

    def __init__(self):
        self.fr, self.en = {}, {}

    def add(self, code, data):
        (self.fr if code == 'fr_fr' else self.en).update({k: v for k, v in data.items() if isinstance(v, str)})

    def get(self, key):
        return self.fr.get(key) or self.en.get(key)

    def component(self, comp):
        """Composant de texte (« {"translate": ...} », « {"text": ...} » ou chaîne)."""
        if isinstance(comp, str):
            return comp
        if isinstance(comp, dict):
            if 'translate' in comp:
                return self.get(comp['translate']) or comp.get('fallback') or comp['translate']
            if 'text' in comp:
                return comp['text'] + ''.join(self.component(e) for e in comp.get('extra', []))
        if isinstance(comp, list):
            return ''.join(self.component(e) for e in comp)
        return ''

    def name(self, kind, ident):
        """Nom d'un objet, d'un bloc, d'un monstre, d'un biome... (« item.ae2.certus_quartz_crystal »)."""
        ns, _, path = ident.partition(':')
        for k in kind:
            text = self.get(f'{k}.{ns}.{path.replace("/", ".")}')
            if text:
                return text
        return None


def pretty(ident):
    """« cobblemon:fossils/prehistoric_birch_tree » → « Prehistoric Birch Tree » (nom sans traduction)."""
    path = ident.split(':')[-1].split('/')[-1]
    return ' '.join(w.capitalize() for w in re.split(r'[_\-]+', path) if w)


# ---------- Lecture de l'instance ----------

def read_jar_langs(lang, jar):
    try:
        with zipfile.ZipFile(jar) as z:
            for n in z.namelist():
                m = re.search(r'assets/[^/]+/lang/(fr_fr|en_us)\.json$', n)
                if m:
                    try:
                        lang.add(m.group(1), json.loads(z.read(n).decode('utf-8-sig')))
                    except ValueError:
                        pass
    except zipfile.BadZipFile:
        pass


def minecraft_files(instance):
    install = os.path.join(os.path.dirname(os.path.dirname(instance)), 'Install')
    jar = os.path.join(install, 'versions', '1.21.1', '1.21.1.jar')
    with open(os.path.join(install, 'versions', '1.21.1', '1.21.1.json'), encoding='utf-8') as f:
        index_id = json.load(f)['assetIndex']['id']
    with open(os.path.join(install, 'assets', 'indexes', index_id + '.json'), encoding='utf-8') as f:
        objects = json.load(f)['objects']
    h = objects['minecraft/lang/fr_fr.json']['hash']
    return jar, os.path.join(install, 'assets', 'objects', h[:2], h)


def mod_ids(jar):
    """Identifiants des mods d'un jar (META-INF/neoforge.mods.toml), jars imbriqués compris."""
    ids = set()

    def scan(z):
        for n in z.namelist():
            if n.endswith('mods.toml'):
                ids.update(re.findall(r'modId\s*=\s*"([^"]+)"', z.read(n).decode('utf-8', 'replace')))
            elif n.endswith('.jar'):
                try:
                    with zipfile.ZipFile(io.BytesIO(z.read(n))) as inner:
                        scan(inner)
                except zipfile.BadZipFile:
                    pass

    try:
        with zipfile.ZipFile(jar) as z:
            scan(z)
    except zipfile.BadZipFile:
        pass
    return ids


ADVANCEMENT = re.compile(r'(?:^|/)data/([^/]+)/advancements?/(.+)\.json$')


def read_advancements(instance, minecraft_jar):
    found = {}

    def add(name, read):
        m = ADVANCEMENT.search(name.replace('\\', '/'))
        if not m:
            return
        try:
            data = json.loads(read().decode('utf-8-sig'))
        except ValueError:
            return
        found[f'{m.group(1)}:{m.group(2)}'] = data  # chargé plus tard : remplace (datapacks et kubejs après les mods)

    def scan_zip(z):
        for n in z.namelist():
            if '/advancement' in n and n.endswith('.json'):
                add(n, lambda n=n: z.read(n))
            elif n.endswith('.jar'):
                try:
                    with zipfile.ZipFile(io.BytesIO(z.read(n))) as inner:
                        scan_zip(inner)
                except zipfile.BadZipFile:
                    pass

    def scan_dir(root):
        for f in glob.glob(os.path.join(root, '**', '*.json'), recursive=True):
            rel = os.path.relpath(f, root).replace('\\', '/')
            if '/advancement' in '/' + rel:
                add('data/' + rel if not rel.startswith('data/') else rel, lambda f=f: open(f, 'rb').read())

    sources = [minecraft_jar] + sorted(glob.glob(os.path.join(instance, 'mods', '*.jar')))
    sources += sorted(glob.glob(os.path.join(instance, 'datapacks', '*.zip')))
    for source in sources:
        try:
            with zipfile.ZipFile(source) as z:
                scan_zip(z)
        except zipfile.BadZipFile:
            print('illisible :', source, file=sys.stderr)
    for d in sorted(glob.glob(os.path.join(instance, 'global_packs', '*', '*'))):
        if os.path.isdir(d):
            scan_dir(d)
    scan_dir(os.path.join(instance, 'kubejs', 'data'))
    found.update(kubejs_advancements(instance))
    return found


# Fonctions de kubejs simulées : seuls les appels à event.json(...) de ServerEvents.generateData sont gardés.
KUBEJS_RUNNER = r'''
const fs = require("fs");
const source = fs.readFileSync(process.argv[1], "utf8").replace(/^\s*import .*$/gm, "");
const created = {};
const handlers = [];
const any = new Proxy(function () {}, { get: () => any, apply: () => any, construct: () => any });
const ServerEvents = { generateData: (when, f) => handlers.push(f), basicCommand: () => {} };
const PlayerEvents = new Proxy({}, { get: () => () => {} });
const Java = { loadClass: () => any };
const Utils = new Proxy({ newMap: () => Object.defineProperty({}, "put", { value(k, v) { this[k] = v; } }), newList: () => [] }, { get: (t, k) => t[k] ?? any });
new Function("ServerEvents", "PlayerEvents", "Java", "Utils", "NBT", "Item", source)(ServerEvents, PlayerEvents, Java, Utils, any, any);
for (const f of handlers) f({ json: (id, data) => { created[id] = data; } });
process.stdout.write(JSON.stringify(created));
'''


def kubejs_advancements(instance):
    script = os.path.join(instance, 'kubejs', 'server_scripts', 'modpack', 'advancements.js')
    if not os.path.exists(script):
        return {}
    out = subprocess.run(['node', '-e', KUBEJS_RUNNER, script], capture_output=True, text=True, encoding='utf-8', check=False)
    if out.returncode != 0:
        sys.exit(f'Script kubejs des succès : {out.stderr}')
    result = {}
    for ident, data in json.loads(out.stdout).items():
        ns, _, path = ident.partition(':')
        result[f'{ns}:{path.removeprefix("advancement/").removeprefix("advancements/")}'] = data
    return result


def condition_ok(cond, mods):
    """Conditions de chargement de NeoForge ; une condition inconnue (réglage d'un mod) compte comme remplie."""
    kind = cond.get('type', '')
    if kind == 'neoforge:mod_loaded':
        return cond.get('modid') in mods
    if kind == 'neoforge:not':
        return not condition_ok(cond.get('value', {}), mods)
    if kind == 'neoforge:and':
        return all(condition_ok(c, mods) for c in cond.get('values', []))
    if kind == 'neoforge:or':
        return any(condition_ok(c, mods) for c in cond.get('values', []))
    if kind == 'neoforge:false':
        return False
    return True


# ---------- Quêtes ----------

def task_title(task, lang):
    """Titre par défaut d'une tâche, comme FTB Quests l'affiche quand la quête n'a pas de titre."""
    kind = task.get('type', '')
    if kind == 'item':
        item = task.get('item')
        ident = item.get('id') if isinstance(item, dict) else item
        if not ident:
            return None
        if ident == 'ftbfiltersystem:smart_filter':
            flt = (item.get('components') or {}).get('ftbfiltersystem:filter', '') if isinstance(item, dict) else ''
            m = re.search(r'item_tag\(([^)]+)\)', flt)
            if m:
                return lang.get('tag.item.' + m.group(1).replace(':', '.').replace('/', '.')) or f'#{m.group(1)}'
            m = re.search(r'item\(([^)]+)\)', flt)
            if m:
                return lang.name(['item', 'block'], m.group(1)) or pretty(m.group(1))
            return None
        name = lang.name(['item', 'block'], ident) or pretty(ident)
        count = task.get('count', 1)
        return f'{name} ×{count}' if isinstance(count, int) and count > 1 else name
    if kind == 'kill':
        entity = task.get('entity', '')
        name = lang.name(['entity'], entity) or pretty(entity)
        count = task.get('value', 1)
        return f'{name} ×{count}' if isinstance(count, int) and count > 1 else name
    if kind == 'advancement':
        return None  # remplacé par le titre du succès, plus bas
    if kind in ('structure', 'dimension', 'biome'):
        ident = task.get(kind, '')
        if ident.startswith('#'):
            return pretty(ident[1:])
        keys = {'biome': ['biome'], 'dimension': ['dimension'], 'structure': ['structure']}[kind]
        return lang.name(keys, ident) or pretty(ident)
    return None


def is_license_note(quest):
    """
    Quête « Tous droits réservés » d'All the Mods, une par chapitre : invisible, optionnelle, seulement des cases à
    cocher ; elle ne s'affiche qu'en mode édition (« si vous voyez ceci, vous êtes en mode Edition ») : aucun joueur ne
    peut la faire. Écartée (sinon le 100 % serait impossible). Les vraies quêtes secrètes (invisibles jusqu'à ce qu'elles
    soient faites : un objet, un succès) restent.
    """
    tasks = quest.get('tasks', [])
    return bool(quest.get('invisible') and quest.get('optional') and tasks and all(t.get('type') == 'checkmark' for t in tasks))


def read_quests(instance, lang_items, advancement_titles):
    root = os.path.join(instance, 'config', 'ftbquests', 'quests')
    qlang = Lang()
    for code in ('en_us', 'fr_fr'):
        path = os.path.join(root, 'lang', code + '.snbt')
        if os.path.exists(path):
            qlang.add(code, read_snbt(path))
        for f in glob.glob(os.path.join(root, 'lang', code, '**', '*'), recursive=True):
            if os.path.isfile(f):
                qlang.add(code, read_snbt(f))

    groups = [g['id'] for g in read_snbt(os.path.join(root, 'chapter_groups.snbt'))['chapter_groups']]
    chapters = [read_snbt(f) for f in glob.glob(os.path.join(root, 'chapters', '*.snbt'))]
    # Ordre du livre : chapitres sans groupe d'abord, puis les groupes dans leur ordre ; dans un groupe, order_index.
    chapters.sort(key=lambda c: (groups.index(c['group']) + 1 if c.get('group') in groups else 0, c.get('order_index', 0)))

    result_chapters, result_quests = [], []
    untitled = {}
    for c in chapters:
        title = clean(qlang.get(f'chapter.{c["id"]}.title') or c.get('filename', c['id']))
        group = clean(qlang.get(f'chapter_group.{c["group"]}.title') or '') if c.get('group') in groups else ''
        index = len(result_chapters)
        result_chapters.append((group, title))
        quests = sorted(c.get('quests', []), key=lambda q: (q.get('y', 0), q.get('x', 0)))
        for q in quests:
            if is_license_note(q):
                continue
            label = qlang.get(f'quest.{q["id"]}.title')
            tasks = q.get('tasks', [])
            if not label and tasks:
                first = tasks[0]
                label = qlang.get(f'task.{first["id"]}.title')
                if not label and first.get('type') == 'advancement':
                    label = advancement_titles.get(first.get('advancement', ''))
                if not label:
                    label = task_title(first, lang_items)
                if not label:
                    untitled[first.get('type')] = untitled.get(first.get('type'), 0) + 1
            label = clean(label or 'Quête sans titre')
            hidden = bool(q.get('invisible'))
            result_quests.append((q['id'], index, label, hidden))
    if untitled:
        print('quêtes sans titre trouvé (par type de tâche) :', untitled, file=sys.stderr)
    return result_chapters, result_quests


# ---------- Écriture ----------

def main():
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(1)
    instance, output = sys.argv[1], sys.argv[2]
    minecraft_jar, minecraft_fr = minecraft_files(instance)

    lang = Lang()
    for jar in [minecraft_jar] + sorted(glob.glob(os.path.join(instance, 'mods', '*.jar'))):
        read_jar_langs(lang, jar)
    with open(minecraft_fr, encoding='utf-8') as f:
        lang.add('fr_fr', json.load(f))
    for d in glob.glob(os.path.join(instance, 'kubejs', 'assets', '*', 'lang')):
        for code in ('en_us', 'fr_fr'):
            p = os.path.join(d, code + '.json')
            if os.path.exists(p):
                with open(p, encoding='utf-8-sig') as f:
                    lang.add(code, json.load(f))

    mods = {'minecraft', 'neoforge'}
    for jar in glob.glob(os.path.join(instance, 'mods', '*.jar')):
        mods |= mod_ids(jar)

    raw = read_advancements(instance, minecraft_jar)
    shown = {k: v for k, v in raw.items() if isinstance(v, dict) and isinstance(v.get('display'), dict)
             and all(condition_ok(c, mods) for c in v.get('neoforge:conditions', []))}

    def root_of(k, depth=0):
        parent = shown[k].get('parent')
        if parent is None:
            return k
        if parent not in shown or depth > 100:
            return None
        return root_of(parent, depth + 1)

    roots = {k: root_of(k) for k in shown}
    visible = {k for k, r in roots.items() if r}
    titles = {k: clean(lang.component(v['display'].get('title', ''))) or pretty(k) for k, v in shown.items()}

    # Onglets : ceux de Minecraft dans l'ordre du jeu, puis les autres par titre ; dans un onglet, l'arbre en profondeur.
    vanilla = ['minecraft:story/root', 'minecraft:nether/root', 'minecraft:end/root', 'minecraft:adventure/root',
               'minecraft:husbandry/root']
    tab_roots = sorted({roots[k] for k in visible},
                       key=lambda r: (vanilla.index(r) if r in vanilla else len(vanilla), titles[r].lower()))
    children = {}
    for k in sorted(visible):
        if shown[k].get('parent'):
            children.setdefault(shown[k]['parent'], []).append(k)
    advancements = []
    for t, r in enumerate(tab_roots):
        stack = [r]
        while stack:
            k = stack.pop()
            advancements.append((k, t, titles[k], bool(shown[k]['display'].get('hidden'))))
            stack.extend(reversed(children.get(k, [])))

    chapters, quests = read_quests(instance, lang, titles)

    with open(os.path.join(instance, 'manifest.json'), encoding='utf-8') as f:
        version = json.load(f).get('version', '?')

    lines = [
        '﻿// <auto-generated>',
        f'// Généré par outils/completion_allthemons.py d\'après le modpack All the Mons {version} (quêtes FTB Quests, succès',
        '// des mods) : ne pas modifier à la main, changer le script et le relancer. Titres seulement : les quêtes d\'All the',
        '// Mods sont « Tous droits réservés ».',
        '// </auto-generated>',
        '',
        'namespace MonsutaPotaru.Services;',
        '',
        'public static class AllTheMonsCompletionData',
        '{',
        f'    /// <summary>Version du modpack relevée.</summary>',
        f'    public const string ModpackVersion = {cs(version)};',
        '',
        '    /// <summary>Chapitres du livre de quêtes, dans son ordre : groupe (vide pour les premiers) et titre.</summary>',
        '    public static readonly AllTheMonsChapter[] Chapters =',
        '    [',
    ]
    lines += [f'        new({cs(g)}, {cs(t)}),' for g, t in chapters]
    lines += ['    ];', '',
              '    /// <summary>Quêtes : identifiant de FTB Quests, chapitre (rang dans <see cref="Chapters"/>), titre, cachée.</summary>',
              '    public static readonly AllTheMonsQuest[] Quests =', '    [']
    lines += [f'        new({cs(i)}, {c}, {cs(t)}{", true" if h else ""}),' for i, c, t, h in quests]
    lines += ['    ];', '',
              '    /// <summary>Onglets de l\'écran des progrès : titre de leur succès racine.</summary>',
              '    public static readonly string[] Tabs =', '    [']
    lines += [f'        {cs(titles[r])},' for r in tab_roots]
    lines += ['    ];', '',
              '    /// <summary>Succès : identifiant, onglet (rang dans <see cref="Tabs"/>), titre, caché.</summary>',
              '    public static readonly AllTheMonsAdvancement[] Advancements =', '    [']
    lines += [f'        new({cs(k)}, {t}, {cs(title)}{", true" if h else ""}),' for k, t, title, h in advancements]
    lines += ['    ];', '}', '']
    with open(output, 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(lines))
    fr_titles = sum(1 for k, *_ in advancements
                    if isinstance(shown[k]['display'].get('title'), dict) and shown[k]['display']['title'].get('translate') in lang.fr)
    print(f'{len(chapters)} chapitres, {len(quests)} quêtes ; {len(tab_roots)} onglets, {len(advancements)} succès '
          f'({fr_titles} titres en français).')


if __name__ == '__main__':
    main()
