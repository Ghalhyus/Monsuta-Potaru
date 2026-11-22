"""
Lecture d'une ROM de 3DS de la 7e génération (Soleil, Lune, Ultra-Soleil, Ultra-Lune) pour outils/completion_gen7.py :
les dresseurs (classe et nom français, équipe) et le lieu où on les combat : dresseurs des routes et combats lancés par
un script.

La ROM est un .3ds **déchiffré**, ou le dossier de sa RomFS extraite (lecture : completion_gen6_rom.RomFS). Formats
(trouvés du 2026-11-18 au 20 sur Soleil, Lune et Ultra-Soleil ; les numéros d'archives sont les mêmes dans les trois,
sauf les aires de Lune) :
- zones (a/0/7/7, membre 0, LZ11) : 0x54 octets par zone, lieu au décalage 0x1C (u32, liste des lieux de PKHeX :
  text_sm_00000) ; 295 zones en Soleil / Lune, 380 en Ultra ;
- mondes (a/0/9/1) : un conteneur « WD » par monde, son sous-fichier 0 donne (u32 aux décalages 4, 8, 12) la liste des
  aires puis des paires u16 (zone, aire) ; dans une aire, l'ordre des paires est celui de ses zones ;
- aires (a/0/8/2 ; Lune : a/0/8/3) : 11 membres par aire, dont « ED » (événements), « ZS » (un script AMX par zone) et
  « ZI » (un script d'initialisation par zone) ; « ED » est un conteneur de blocs (EP, EM, EB, ES, EA, ET…), chacun un
  conteneur d'un sous-bloc par zone ;
- **dresseurs des routes** : le premier bloc « ET » de l'aire : u32 nombre, puis des enregistrements de 84 octets (u32
  type 7, position…, numéro d'événement au mot 11, numéro de script au mot 13 = 1000 + numéro du dresseur). Le moteur
  (FieldRo.cro) copie ces deux mots quand le dresseur voit le joueur, et la native TrainerIDGet retire la base 1000 ;
- **combats lancés par un script** (rival, Team Skull, Fondation Æther, Dexio et Sina…) :
  - scripts d'événements : a/0/9/2 (AMX, un fichier par script) ; numéros de script → fichier : table de tranches
    (Soleil / Lune a/1/3/9, Ultra a/1/4/3, membre 0 ; 10 octets, u16 : premier numéro, dernier, genre, ?, fichier ;
    1000–4999 = trainer.amx, d'où la base des dresseurs des routes) ;
  - combat : la native 0x549eec84 (0, dresseur, partenaire, …), appelée directement ou par de petites fonctions locales
    qui lui passent leurs paramètres ; dresseur constant, ou rangé dans une variable locale par la fonction (rival : un
    numéro selon le Pokémon de départ, trois numéros qui se suivent) ;
  - lieu : les zones qui citent un numéro de la tranche du script (u32 des blocs « ED », constantes des scripts « ZS »
    et « ZI ») ; un script cité par un autre script hérite de ses lieux s'il n'en a pas. Quelques scripts n'ont aucun
    lieu (le premier combat contre Tili, Elsa-Mina, Giovanni…) ;
  - vérifié sur ultra-soleil.sav : les 17 combats retrouvés parmi les 27 battus sont tous à Mele-Mele ou au début
    d'Akala ; 10 dresseurs battus restent introuvables (Chaz, Pierrot, Séraphine… : mécanisme inconnu) ;
- dresseurs : données (Soleil / Lune a/1/0/5, Ultra a/1/0/6 ; 20 octets, classe à l'octet 0), équipes (a/1/0/6,
  a/1/0/7 ; 32 octets par Pokémon : niveau u16 au décalage 14, espèce u16 au 16), textes français (a/0/3/3, fichiers
  105 / 106 : noms et classes ; Ultra : 110 / 111 ; chiffrés comme en 6e génération).
Drapeau du dresseur n : Soleil / Lune 0x81C + n, Ultra 0xBDC + n (vérifié sur ultra-soleil.sav : les 52 dresseurs
battus, rival, Doyen et Capitaine compris, ont leur drapeau levé).
Détails et méthode de recherche : docs/claude/completion-gen7-dresseurs.md.
"""
import bisect
import struct

from completion_gen6_rom import RomFS, amx, garc, lz11, texts

GAMES = {
    'sm': dict(trdata='a/1/0/5', trpoke='a/1/0/6', names=105, classes=106, flags=0x81C, scripts='a/1/3/9'),
    'usum': dict(trdata='a/1/0/6', trpoke='a/1/0/7', names=110, classes=111, flags=0xBDC, scripts='a/1/4/3'),
}
SCRIPT_BASE = 1000  # numéro de script d'un dresseur des routes = 1000 + numéro
ET_RECORD = 84
EVENT_IDS = 5000  # numéros des scripts d'événements cités par les zones : à partir de 5000 (avant : trainer.amx)

# Instructions AMX (compressées : un mot de 32 bits ; « push.c » et « push.s » gardent leur valeur dans les 16 bits du haut)
PROC, CALL, SYSREQ, CONST_PRI, PUSH_C32, CONST_S = 0x2E, 0x31, 0x87, 0x27, 0x8A, 0x9D
PUSH_C, PUSH_S, STOR_S_PRI = 0xBC, 0xBE, 0xB1
BATTLE = 0x549eec84  # native de combat : (0, dresseur, partenaire, …)


def _unpack(member):
    return lz11(member) if member[:1] == b'\x11' else member


def _container(data):
    """Sous-fichiers d'un conteneur à deux lettres : u16 nombre, puis les décalages."""
    count = struct.unpack_from('<H', data, 2)[0]
    offsets = struct.unpack_from(f'<{count + 1}I', data, 4)
    return [data[offsets[i]:offsets[i + 1]] for i in range(count)]


def _signed(v):
    return v - (1 << 32) if v & 0x80000000 else v


def _constants(code):
    """Constantes poussées par un script (push.c, push.c sur 32 bits)."""
    for k, w in enumerate(code):
        if w & 0xFFFF == PUSH_C:
            yield w >> 16
        elif w == PUSH_C32 and k + 1 < len(code):
            yield code[k + 1]


def _argument(w):
    """Argument poussé : ('c', constante), ('p', numéro du paramètre), ('l', décalage d'une variable locale) ou None."""
    if w & 0xFFFF == PUSH_C:
        return 'c', w >> 16
    if w & 0xFFFF == PUSH_S:
        offset = w >> 16
        if offset >= 0x8000:
            return 'l', offset - 0x10000
        if offset >= 12:
            return 'p', (offset - 12) // 4
    return None


def _pushed(code, k, count):
    """Les <count> arguments poussés juste avant l'instruction k, le premier d'abord."""
    return [_argument(code[b]) for b in range(k - 1, max(-1, k - 1 - count), -1)]


def script_battles(code, hashes, count):
    """
    Combats d'un script AMX : ensembles de numéros de dresseurs (de 1 à count - 1), un par combat ; plusieurs numéros :
    les variantes d'un même combat (numéro rangé dans une variable locale selon le Pokémon de départ ou la version). La
    native de combat est appelée directement ou par des fonctions locales qui lui passent leurs paramètres.
    """
    procs = [i for i, w in enumerate(code) if w == PROC]
    wrappers, found = {}, set()  # début de fonction → ceux de ses paramètres qui sont des dresseurs

    def function(k):
        i = bisect.bisect_right(procs, k) - 1
        if i < 0:
            return 0, len(code)
        return procs[i], procs[i + 1] if i + 1 < len(procs) else len(code)

    def take(k, args, positions):
        for p in positions:
            if p >= len(args) or not args[p]:
                continue
            kind, v = args[p]
            if kind == 'c' and 0 < v < count:
                found.add(frozenset([v]))
            elif kind == 'p':
                wrappers.setdefault(function(k)[0], set()).add(v)
            elif kind == 'l':
                start, end = function(k)
                variants = set()
                for q in range(start, end - 2):
                    if code[q] == CONST_S and _signed(code[q + 1]) == v and 0 < code[q + 2] < count:
                        variants.add(code[q + 2])
                    if (code[q] == CONST_PRI and code[q + 2] == ((v & 0xFFFF) << 16 | STOR_S_PRI)
                            and 0 < code[q + 1] < count):
                        variants.add(code[q + 1])
                if variants:
                    found.add(frozenset(variants))

    for _ in range(4):  # fonctions qui appellent des fonctions qui appellent la native…
        for k in range(1, len(code) - 2):
            if code[k] == SYSREQ and code[k + 1] < len(hashes) and hashes[code[k + 1]] == BATTLE:
                take(k, _pushed(code, k, code[k + 2] // 4), (1, 2))
            elif code[k] == CALL and code[k - 1] & 0xFFFF == PUSH_C:  # push.c <octets d'arguments> ; call
                target = k + _signed(code[k + 1]) // 4
                if target in wrappers:
                    take(k - 1, _pushed(code, k - 1, (code[k - 1] >> 16) // 4), wrappers[target])
    return found


def read_trainers(path, kind):
    """
    Dresseurs d'une ROM : {numéro: dict(cls, name, team=[(espèce, niveau)], places=[lieu], script=None)}, lieu = numéro
    de la liste de PKHeX. places : dresseur des routes. script : combat lancé par un script, dict(places={lieu},
    group=numéro) ; places vide : lieu inconnu ; group : plus petit numéro des variantes du même combat (selon le Pokémon
    de départ).
    """
    g = GAMES[kind]
    rom = RomFS(path)
    text = garc(rom.read('a/0/3/3'))
    classes, names = texts(text[g['classes']]), texts(text[g['names']])
    if 'Montagnard' not in classes:
        raise SystemExit(f'{path} : textes français introuvables (a/0/3/3)')
    data, parties = garc(rom.read(g['trdata'])), garc(rom.read(g['trpoke']))
    trainers = {}
    for n in range(1, len(data)):
        d, p = data[n], parties[n]  # pas compressés (la classe 17 commence par l'octet 0x11 de LZ11)
        team = [(struct.unpack_from('<H', p, k + 16)[0], struct.unpack_from('<H', p, k + 14)[0])
                for k in range(0, len(p) - 31, 32)]
        trainers[n] = dict(cls=classes[d[0]], name=names[n] if n < len(names) else '', team=team, places=[], script=None)

    table = garc(rom.read(g['scripts']))[0]
    ranges = [r for r in sorted(struct.unpack_from('<5H', table, 10 * k) for k in range(len(table) // 10)) if r[0]]
    if ranges[0][:2] != (1000, 2999) or ranges[0][4] != 0:
        raise SystemExit(f"{path} : table des numéros de script inattendue ({g['scripts']})")
    starts = [r[0] for r in ranges]

    def script_file(v):
        i = bisect.bisect_right(starts, v) - 1
        return ranges[i][4] if v >= EVENT_IDS and i >= 0 and ranges[i][0] <= v <= ranges[i][1] else None

    events = garc(rom.read('a/0/9/2'))
    if max(r[4] for r in ranges) >= len(events):
        raise SystemExit(f'{path} : {len(events)} scripts dans a/0/9/2, la table des numéros en attend plus')
    event_places = {}  # script d'événement → lieux des zones qui citent un de ses numéros
    zone_battles = {}  # dresseur → lieux (combat lancé par un script de zone)

    zones_raw = _unpack(garc(rom.read('a/0/7/7'))[0])
    zone_place = [struct.unpack_from('<I', zones_raw, i * 0x54 + 0x1C)[0] for i in range(len(zones_raw) // 0x54)]
    area_zones = {}
    for member in garc(rom.read('a/0/9/1')):
        world = _container(_unpack(member))[0]
        _, pairs, end = struct.unpack_from('<III', world, 4)
        for k in range(pairs, end, 4):
            zone, area = struct.unpack_from('<HH', world, k)
            area_zones.setdefault(area, []).append(zone)
    areas = garc(rom.read('a/0/8/2') or rom.read('a/0/8/3'))  # Lune : a/0/8/3
    for a in range(len(areas) // 11):
        files = {}
        for k in range(11):
            x = _unpack(areas[11 * a + k])
            files.setdefault(x[:2], x)
        if b'ED' not in files or a not in area_zones:
            continue
        zones = area_zones[a]
        if len(_container(files[b'ZS'])) != len(zones):  # un script par zone : contrôle de l'ordre des zones
            raise SystemExit(f'aire {a} : le nombre de scripts ne suit pas ses {len(zones)} zones')
        for block in _container(files[b'ED']):
            for j, sub in enumerate(_container(block)[:len(zones)]):
                for o in range(0, len(sub) - 3, 4):
                    if (f := script_file(struct.unpack_from('<I', sub, o)[0])) is not None:
                        event_places.setdefault(f, set()).add(zone_place[zones[j]])
        for tag in (b'ZS', b'ZI'):
            if tag not in files:
                continue
            for j, script in enumerate(_container(files[tag])[:len(zones)]):
                code, hashes = amx(script)
                for v in _constants(code):
                    if (f := script_file(v)) is not None:
                        event_places.setdefault(f, set()).add(zone_place[zones[j]])
                for battle in script_battles(code, hashes, len(data)):
                    zone_battles.setdefault(battle, set()).add(zone_place[zones[j]])
        et = next(b for b in _container(files[b'ED']) if b[:2] == b'ET')
        for j, block in enumerate(_container(et)):
            if len(block) < 4:
                continue
            for r in range(struct.unpack_from('<I', block, 0)[0]):
                words = struct.unpack_from('<21I', block, 4 + ET_RECORD * r)
                if words[0] != 7:
                    raise SystemExit(f'aire {a}, zone {j} : enregistrement ET de type {words[0]}')
                n = words[13] - SCRIPT_BASE
                if n not in trainers:
                    raise SystemExit(f'aire {a}, zone {j} : script {words[13]}, pas un dresseur')
                place = zone_place[zones[j]]
                if place not in trainers[n]['places']:
                    trainers[n]['places'].append(place)

    battles, launches = {}, {}  # script → dresseurs combattus ; script → scripts qu'il cite
    for f, member in enumerate(events):
        code, hashes = amx(member if member[4:6] == b'\xe0\xf1' else _unpack(member))  # AMX brut (sa taille peut commencer par 0x11)
        battles[f] = script_battles(code, hashes, len(data))
        launches[f] = {c for v in _constants(code) if (c := script_file(v)) is not None and c != f}
    for _ in range(5):  # un script cité par un autre prend ses lieux
        for f, called in launches.items():
            for c in called:
                if not event_places.get(c) and event_places.get(f):
                    event_places[c] = set(event_places[f])
    fights = [(battle, places) for battle, places in zone_battles.items()]
    fights += [(battle, event_places.get(f, set())) for f in sorted(battles) for battle in sorted(battles[f], key=min)]
    for battle, places in fights:
        members = sorted(n for n in battle if trainers[n]['team'])
        if not members:
            continue
        known = [trainers[n]['script']['group'] for n in members if trainers[n]['script']]
        group = min(known) if known else members[0]
        for n in members:
            if trainers[n]['script'] is None:
                trainers[n]['script'] = dict(places=set(), group=group)
            trainers[n]['script']['places'] |= places
    return trainers
