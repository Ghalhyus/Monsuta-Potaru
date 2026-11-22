"""
Lecture d'une ROM de Nintendo DS de la 5e génération (Pokémon Noir / Blanc) pour outils/completion_gen5.py : système de
fichiers de la cartouche (FNT / FAT), archives NARC, textes du jeu (chiffrés), scripts des cartes (commandes de
16 bits, arguments d'après les macros de include/asm/field_script.inc du désassemblage pokebw2, que Noir / Blanc partage
pour les commandes de base).

Formats : en-tête de la cartouche (GBATEK), NARC (BTAF / BTNF / GMIF), textes de la 5e génération (clé de 16 bits qui
tourne de 3 bits à chaque caractère, en partant du dernier), documentés par https://ds-pokemon-hacking.github.io et
pokebw2 (docs/data.md, docs/scripts.md).
"""
import re
import struct


class Rom:
    """Fichiers d'une cartouche DS, par chemin (« a/0/5/7 »)."""

    def __init__(self, path):
        with open(path, 'rb') as fh:
            self.data = fh.read()
        d = self.data
        self.code = d[0x0C:0x10].decode('ascii')  # IRBF : Noir, français
        fnt_off, fnt_len, fat_off, fat_len = struct.unpack_from('<IIII', d, 0x40)
        self.fat = [struct.unpack_from('<II', d, fat_off + 8 * i) for i in range(fat_len // 8)]
        self.paths = {}
        self._dir(d, fnt_off, 0xF000, '')

    def _dir(self, d, fnt, dir_id, prefix):
        entry = fnt + (dir_id & 0xFFF) * 8
        sub_off, first_file, _ = struct.unpack_from('<IHH', d, entry)
        pos = fnt + sub_off
        file_id = first_file
        while True:
            length = d[pos]
            pos += 1
            if length == 0:
                break
            name = d[pos:pos + (length & 0x7F)].decode('latin-1')
            pos += length & 0x7F
            if length & 0x80:
                sub = struct.unpack_from('<H', d, pos)[0]
                pos += 2
                self._dir(d, fnt, sub, prefix + name + '/')
            else:
                self.paths[prefix + name] = file_id
                file_id += 1

    def file(self, path):
        start, end = self.fat[self.paths[path]]
        return self.data[start:end]

    def narc(self, path):
        return narc(self.file(path))


def narc(data):
    """Membres d'une archive NARC."""
    assert data[:4] == b'NARC', 'pas une archive NARC'
    pos = struct.unpack_from('<H', data, 0x0C)[0]
    assert data[pos:pos + 4] == b'BTAF'
    size, count = struct.unpack_from('<IH', data, pos + 4)
    entries = [struct.unpack_from('<II', data, pos + 12 + 8 * i) for i in range(count)]
    pos += size
    assert data[pos:pos + 4] == b'BTNF'
    pos += struct.unpack_from('<I', data, pos + 4)[0]
    assert data[pos:pos + 4] == b'GMIF'
    base = pos + 8
    return [data[base + a:base + b] for a, b in entries]


# ---------------------------------------------------------------------------------------------------------------------
# Textes

def texts(data):
    """Lignes d'un fichier de textes de la 5e génération (première section), déchiffrées."""
    sections, lines, _, _ = struct.unpack_from('<HHII', data, 0)
    sec = struct.unpack_from('<I', data, 12)[0]
    out = []
    for i in range(lines):
        off, n, _ = struct.unpack_from('<IHH', data, sec + 4 + 8 * i)
        chars = list(struct.unpack_from(f'<{n}H', data, sec + off))
        key = chars[-1] ^ 0xFFFF
        for j in range(n - 1, -1, -1):
            chars[j] ^= key
            key = ((key >> 3) | (key << 13)) & 0xFFFF
        s = []
        j = 0
        while j < n:
            c = chars[j]
            if c == 0xFFFF:
                break
            if c == 0xF000:  # commande (nom du joueur, couleur…) : son type, ses paramètres
                count = chars[j + 2] if j + 2 < n else 0
                s.append('[…]')
                j += 3 + count
                continue
            if c == 0xFFFE:
                s.append('\n')
            else:
                s.append(chr(c))
            j += 1
        out.append(''.join(s))
    return out


# ---------------------------------------------------------------------------------------------------------------------
# Scripts

def commands(inc_path):
    """Commandes des scripts d'après les macros de pokebw2 : numéro → (nom, [(taille, saut ?)…])."""
    text = open(inc_path, encoding='utf-8').read()
    table = {}
    for m in re.finditer(r'\.macro (\w+)([^\n]*)\n((?:(?!\.endm).*\n)*?)\s*\.endm', text):
        name, body = m.group(1), m.group(3)
        cmd = re.search(r'FieldCommand (\d+)', body)
        if not cmd:
            continue
        args = []
        for a in re.finditer(r'\.(byte|2byte|4byte) ([^\n]*)', body):
            size = {'byte': 1, '2byte': 2, '4byte': 4}[a.group(1)]
            args.append((size, '- (.' in a.group(2)))
        table[int(cmd.group(1))] = (name, args)
    return table


ENDS = {'VMHalt', 'VMEnd', 'VMReturn', 'VMJump'}


def script_entries(data):
    """Début de chaque script d'un fichier (décalages relatifs jusqu'au marqueur 0xFD13)."""
    entries = []
    pos = 0
    while pos + 4 <= len(data):
        if struct.unpack_from('<H', data, pos)[0] == 0xFD13:
            break
        rel = struct.unpack_from('<i', data, pos)[0]
        entries.append(pos + 4 + rel)
        pos += 4
    return entries


def disassemble(data, table):
    """Instructions atteintes depuis les scripts du fichier : {décalage: (nom, [arguments], suivant)}, en suivant les
    sauts ; une commande inconnue arrête la branche (commandes d'extensions, numéros de 1000 et plus)."""
    seen = {}
    todo = list(script_entries(data))
    while todo:
        pos = todo.pop()
        while 0 <= pos < len(data) - 1 and pos not in seen:
            op = struct.unpack_from('<H', data, pos)[0]
            if op not in table:
                break
            name, args = table[op]
            p = pos + 2
            values = []
            for size, rel in args:
                if p + size > len(data):
                    values = None
                    break
                v = int.from_bytes(data[p:p + size], 'little', signed=rel)
                p += size
                if rel:
                    v = p + v
                    todo.append(v)
                values.append(v)
            if values is None:
                break
            seen[pos] = (name, values, p)
            if name in ENDS:
                break
            pos = p
    return seen


def flow(seen):
    """Instructions dans l'ordre des décalages : [(décalage, nom, arguments)]."""
    return [(pos, name, values) for pos, (name, values, _) in sorted(seen.items())]
