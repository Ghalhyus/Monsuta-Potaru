"""
Lecture d'une ROM de 3DS de la 6e génération (X / Y, Rubis Oméga / Saphir Alpha) pour outils/completion_gen6.py :
les dresseurs (classe et nom français, équipe), les zones où on les combat et les zones des arènes.

La ROM est un .3ds **déchiffré** (en-tête NCSD, partition NCCH sans chiffrement), ou le dossier de sa RomFS extraite
(Azahar / Citra : « Dump RomFS »). Formats, d'après pk3DS (https://github.com/kwsch/pk3DS) et 3dbrew :
- RomFS (niveau 3 de l'IVFC) : répertoires et fichiers « a/0/1/2 » ; archives GARC (CRAG / OTAF / BTAF / BMIF) ;
- zones (X / Y : a/0/1/2, Rubis Oméga / Saphir Alpha : a/0/1/3) : chaque membre est compressé en LZ11 et contient un
  conteneur « ZO » : en-tête de la zone (56 octets : lieu au décalage 28, sur 9 bits, numéro de la liste des lieux de
  PKHeX ; musique au décalage 8, 17 = celle des arènes), entités (décors de 20 octets, personnages de 48 octets, dont le
  script au décalage 10) et script de la zone (Pawn / AMX, instructions compressées par octets de 7 bits) ;
- dresseurs : données (X / Y a/0/3/8, Rubis Oméga / Saphir Alpha a/0/3/6), équipes (a/0/4/0, a/0/3/8), textes du jeu
  (X / Y a/0/7/5, Rubis Oméga / Saphir Alpha a/0/7/4 : la langue française ; fichiers 20 / 21 et 21 / 22 : classes et
  noms des dresseurs), chiffrés comme en 5e génération.

Où se combat un dresseur, et son drapeau (0x6CC + numéro, comme les « TRID_ » de FlagsEditorEX) :
- un personnage de la zone dont le script est 3000 + numéro (5000 + numéro : le second d'un duo) : le script commun des
  dresseurs lève le drapeau (vérifié sur rubis-omega.sav) ;
- un script de zone qui lève lui-même le drapeau : une fonction du script ajoute 0x6CC à son argument et appelle la
  fonction native qui lève un drapeau (hachage 0x13413496 ; 0x133daa6a le teste, 0xaccbbbd7 l'efface). Les dresseurs
  d'arène de Rubis Oméga, les familles de la Route 111… Un combat lancé par un script sans cet appel (rival, champions de
  X / Y, Lysandre, Team Flare) ne lève pas de drapeau.
Les combats lancés par script (natives 0x179b019f et 0x0b144ec1, directement ou par une petite fonction) servent à
reconnaître le combat d'un champion.
"""
import os
import struct

TRAINER_FLAGS = 0x6CC  # drapeau du dresseur 0 (TRID_NULL de FlagsEditorEX), le même dans les quatre jeux

GAMES = {
    'xy': dict(zones='a/0/1/2', trdata='a/0/3/8', trpoke='a/0/4/0', text='a/0/7/5', classes=20, names=21, oras=False),
    'oras': dict(zones='a/0/1/3', trdata='a/0/3/6', trpoke='a/0/3/8', text='a/0/7/4', classes=21, names=22, oras=True),
}

SET_FLAG = 0x13413496
BATTLES = {0x179b019f, 0x0b144ec1}  # combats (Rubis Oméga / Saphir Alpha ; les deux paires)
GYM_MUSIC = 17

# Instructions AMX (compressées : un mot de 32 bits ; « push.c » garde sa valeur dans les 16 bits du haut)
PROC, RETN, CALL, SYSREQ = 0x2E, 0x30, 0x31, 0x87
PUSH_C = 0xBC  # push.c n : (n << 16) | 0xBC
PUSH_ARG0 = 0x000C00BE  # push.s [fp+12] : premier argument de la fonction
LOAD_ARG0 = 0x000C00A4  # load.s.pri [fp+12]
ADD_TRAINER_FLAGS = (TRAINER_FLAGS << 16) | 0xC5  # add.c 0x6CC


class RomFS:
    """Fichiers de la RomFS d'un .3ds déchiffré, ou d'un dossier de RomFS extraite."""

    def __init__(self, path):
        self.folder = path if os.path.isdir(path) else None
        if self.folder:
            return
        self.fh = open(path, 'rb')
        f = self.fh
        f.seek(0x100)
        base = 0
        if f.read(4) == b'NCSD':  # cartouche : la partition 0 est le jeu
            f.seek(0x120)
            base = struct.unpack('<I', f.read(4))[0] * 0x200
        f.seek(base + 0x100)
        if f.read(4) != b'NCCH':
            raise SystemExit(f'{path} : pas une ROM de 3DS')
        f.seek(base + 0x188)
        if not f.read(8)[7] & 0x4:
            raise SystemExit(f'{path} : ROM chiffrée (il faut un .3ds déchiffré, ou la RomFS extraite par Azahar / Citra)')
        f.seek(base + 0x1B0)
        romfs = base + struct.unpack('<I', f.read(4))[0] * 0x200
        f.seek(romfs)
        ivfc = f.read(0x60)
        assert ivfc[:4] == b'IVFC', 'RomFS illisible'
        master = struct.unpack_from('<I', ivfc, 0x08)[0]
        block = 1 << struct.unpack_from('<I', ivfc, 0x4C)[0]
        self.level3 = romfs + -(-(0x60 + master) // block) * block
        f.seek(self.level3)
        h = struct.unpack('<10I', f.read(0x28))
        f.seek(self.level3 + h[3])
        self.dirs = f.read(h[4])
        f.seek(self.level3 + h[7])
        self.file_meta = f.read(h[8])
        self.data = self.level3 + h[9]
        self.files = {}
        self._walk(0, '')

    def _walk(self, d, path):
        _, _, child, file, _, size = struct.unpack_from('<6I', self.dirs, d)
        name = self.dirs[d + 0x18:d + 0x18 + size].decode('utf-16le')
        here = f'{path}/{name}' if name else path
        while file != 0xFFFFFFFF:
            _, sibling, offset, length, _, size = struct.unpack_from('<IIQQII', self.file_meta, file)
            fname = self.file_meta[file + 0x20:file + 0x20 + size].decode('utf-16le')
            self.files[f'{here}/{fname}'.lstrip('/')] = (offset, length)
            file = sibling
        while child != 0xFFFFFFFF:
            self._walk(child, here)
            child = struct.unpack_from('<I', self.dirs, child + 4)[0]

    def read(self, name):
        if self.folder:
            with open(os.path.join(self.folder, *name.split('/')), 'rb') as fh:
                return fh.read()
        offset, length = self.files[name]
        self.fh.seek(self.data + offset)
        return self.fh.read(length)


def garc(data):
    """Membres d'une archive GARC (le premier sous-fichier de chacun)."""
    assert data[:4] == b'CRAG', 'pas une archive GARC'
    header = struct.unpack_from('<I', data, 4)[0]
    start = struct.unpack_from('<I', data, 0x10)[0]
    fato_size, count = struct.unpack_from('<IH', data, header + 4)
    fatb = header + fato_size
    out = []
    for i in range(count):
        entry = fatb + 12 + struct.unpack_from('<I', data, header + 12 + 4 * i)[0]
        bits = struct.unpack_from('<I', data, entry)[0]
        assert bits, 'membre vide'
        begin, end, _ = struct.unpack_from('<III', data, entry + 4)
        out.append(data[start + begin:start + end])
    return out


def lz11(data):
    """Décompression LZ11 (Nintendo)."""
    if data[0] != 0x11:
        return data
    size = data[1] | data[2] << 8 | data[3] << 16
    p = 4
    if size == 0:
        size = struct.unpack_from('<I', data, 4)[0]
        p = 8
    out = bytearray()
    while len(out) < size:
        flags = data[p]
        p += 1
        for bit in range(8):
            if len(out) >= size:
                break
            if not flags & (0x80 >> bit):
                out.append(data[p])
                p += 1
                continue
            b1 = data[p]
            if b1 >> 4 == 0:
                length = ((b1 & 0xF) << 4 | data[p + 1] >> 4) + 0x11
                disp = ((data[p + 1] & 0xF) << 8 | data[p + 2]) + 1
                p += 3
            elif b1 >> 4 == 1:
                length = ((b1 & 0xF) << 12 | data[p + 1] << 4 | data[p + 2] >> 4) + 0x111
                disp = ((data[p + 2] & 0xF) << 8 | data[p + 3]) + 1
                p += 4
            else:
                length = (b1 >> 4) + 1
                disp = ((b1 & 0xF) << 8 | data[p + 1]) + 1
                p += 2
            for _ in range(length):
                out.append(out[-disp])
    return bytes(out)


def container(data):
    """Sous-fichiers d'un conteneur à deux lettres (« ZO ») : u16 nombre, puis les décalages."""
    count = struct.unpack_from('<H', data, 2)[0]
    offsets = struct.unpack_from(f'<{count + 1}I', data, 4)
    return data[:2], [data[offsets[i]:offsets[i + 1]] for i in range(count)]


def texts(data):
    """Lignes d'un fichier de textes de la 6e génération (clé 0x7C89, +0x2983 par ligne, tournée de 3 bits par caractère)."""
    _, lines, _, _, section = struct.unpack_from('<HHIII', data, 0)
    out = []
    key = 0x7C89
    for i in range(lines):
        offset, length, _ = struct.unpack_from('<IHH', data, section + 4 + 8 * i)
        k = key
        chars = []
        for j in range(length):
            c = struct.unpack_from('<H', data, section + offset + 2 * j)[0] ^ k
            k = (k << 3 | k >> 13) & 0xFFFF
            if c == 0:
                break
            if c == 0x10:  # variable (nom du joueur…) : ligne sans nom
                chars = ['{v}']
                break
            chars.append(chr(c))
        out.append(''.join(chars))
        key = (key + 0x2983) & 0xFFFF
    return out


def amx(script):
    """Instructions et fonctions natives (hachages de leurs noms) d'un script de zone ; ([], []) s'il est vide."""
    if len(script) < 0x38 or struct.unpack_from('<H', script, 4)[0] != 0xF1E0:
        return [], []
    size = struct.unpack_from('<I', script, 0)[0]
    cod, dat = struct.unpack_from('<II', script, 12)
    natives, libraries = struct.unpack_from('<II', script, 36)
    hashes = [struct.unpack_from('<I', script, o + 4)[0] for o in range(natives, libraries, 8)]
    code, value, first = [], 0, True
    count = (dat - cod) // 4
    for b in script[cod:size]:
        if len(code) >= count:
            break
        v = b & 0x7F
        if first:
            value = v - 0x80 if v & 0x40 else v  # signe porté par le bit 6 du premier octet
        else:
            value = value << 7 | v
        if b & 0x80:
            first = False
            continue
        code.append(value & 0xFFFFFFFF)
        first = True
    return code, hashes


def _signed(v):
    return v - (1 << 32) if v & 0x80000000 else v


def _pushed(word):
    """Valeur d'un « push.c » compressé, sinon None."""
    return word >> 16 if word & 0xFFFF == PUSH_C else None


def _functions(code, hashes):
    """Début de chaque fonction → (natives appelées, instructions jusqu'au premier retour)."""
    out = {}
    for start, w in enumerate(code):
        if w != PROC:
            continue
        body, natives, k = [], [], start + 1
        while k < len(code) - 1 and code[k] not in (PROC, RETN):
            if code[k] == SYSREQ and code[k + 1] < len(hashes):
                natives.append(hashes[code[k + 1]])
                k += 3
                continue
            body.append(code[k])
            k += 1
        out[start] = (natives, body)
    return out


def _calls(code):
    """Appels de fonctions locales avec leurs arguments constants : (adresse, [arguments]) ; None si pas constant."""
    for i in range(2, len(code) - 1):
        if code[i] != CALL or _pushed(code[i - 1]) is None:
            continue
        nargs = _pushed(code[i - 1]) // 4
        args = [_pushed(code[i - 2 - k]) if i - 2 - k >= 0 else None for k in range(nargs)]
        yield i + _signed(code[i + 1]) // 4, args


def zone_trainers(code, hashes):
    """Dresseurs d'un script de zone : (dont le script lève le drapeau, combattus)."""
    functions = _functions(code, hashes)
    setters = {s for s, (natives, body) in functions.items() if SET_FLAG in natives and ADD_TRAINER_FLAGS in body}
    fighters = {s for s, (natives, body) in functions.items()
                if BATTLES & set(natives) and (PUSH_ARG0 in body or LOAD_ARG0 in body)}
    flagged, fought = set(), set()
    for target, args in _calls(code):
        if target in setters and args and args[0] is not None:
            flagged.add(args[0])
        if target in fighters and args:
            fought.update(a for a in args[:3] if a)
    for i in range(1, len(code) - 1):  # combat lancé directement
        if code[i] == SYSREQ and code[i + 1] < len(hashes) and hashes[code[i + 1]] in BATTLES:
            if (v := _pushed(code[i - 1])) is not None:
                fought.add(v)
    return flagged, fought


def read_trainers(path, kind):
    """
    Dresseurs d'une ROM : {numéro: dict(cls, name, team=[(espèce, niveau)], places=[(zone, lieu, source)])},
    source « carte » (personnage de la zone) ou « script » (le script lève le drapeau) ; et les zones des arènes :
    {zone: (lieu, dresseurs combattus dans la zone)}.
    """
    g = GAMES[kind]
    rom = RomFS(path)
    text = garc(rom.read(g['text']))
    classes, names = texts(text[g['classes']]), texts(text[g['names']])
    if 'Montagnard' not in classes:
        raise SystemExit(f'{path} : textes français introuvables ({g["text"]})')
    data, parties = garc(rom.read(g['trdata'])), garc(rom.read(g['trpoke']))
    trainers = {}
    for n in range(1, len(names)):
        d, p = data[n], parties[n]
        fmt = d[0]
        cls = struct.unpack_from('<H', d, 2)[0] if g['oras'] else d[1]
        size = 8 + (2 if fmt & 2 else 0) + (8 if fmt & 1 else 0)
        team = [(struct.unpack_from('<H', p, k + 4)[0], struct.unpack_from('<H', p, k + 2)[0])
                for k in range(0, len(p) - size + 1, size)]
        trainers[n] = dict(cls=classes[cls], name=names[n], team=team, places=[])
    gyms = {}
    for zone, raw in enumerate(garc(rom.read(g['zones']))):
        z = lz11(raw)
        if z[:2] != b'ZO':
            continue  # le dernier membre : tous les en-têtes à la suite
        _, (header, entities, script, *_) = container(z)
        place = struct.unpack_from('<H', header, 28)[0] & 0x1FF
        furniture, npcs = entities[4], entities[5]
        for k in range(npcs):
            s = struct.unpack_from('<H', entities, 12 + 20 * furniture + 48 * k + 10)[0]
            n = s - 3000 if 3000 < s < 4000 else s - 5000 if 5000 < s < 6000 else 0
            if n in trainers:
                trainers[n]['places'].append((zone, place, 'carte'))
        flagged, fought = zone_trainers(*amx(script))
        for n in sorted(flagged):
            if n in trainers:
                trainers[n]['places'].append((zone, place, 'script'))
        if struct.unpack_from('<H', header, 8)[0] == GYM_MUSIC:
            gyms[zone] = (place, {n for n in fought if n in trainers})
    return trainers, gyms
