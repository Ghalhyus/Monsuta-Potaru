// Modèle du parc, sans affichage : la carte (cases libres, eau et lave libres, sommets, chemins), le hasard de la
// simulation, le caractère et l'humeur d'un Pokémon, la nuit. Importé par parc.js, qui l'anime et le dessine avec Phaser ;
// rien ici ne dépend du navigateur (ni Phaser, ni document) : les tests le lancent avec Node (MonsutaPotaru.Tests/js).

// Taille d'une case de la carte, en pixels de la carte.
export const TILE = 16;
// Cases où l'on peut aller (masques de Ground.walkable) : LAND, la terre où l'on marche ; WATER, l'eau libre (sans rocher ni
// objet posé) et le bord qui la touche (écume de la plage), pour les nageurs et les volants ; LAVA, la lave libre (de même),
// pour les types Feu et les volants ; SUMMIT, le dessus des plateaux et des sommets de la montagne (calque « Sommets »), où
// seul un volant se pose ; AIR, leurs rebords et leurs flancs (calque « Survol »), qu'il survole sans s'y arrêter
export const LAND = 1, WATER = 2, LAVA = 4, LIQUID = WATER | LAVA, SUMMIT = 8, AIR = 16;
// Nageur : part de ses destinations (et de ses arrivées) dans l'eau, au moins (Ground.waterShare)
const WATER_SHARE = 2 / 3;

/** Tirage à graine (mulberry32) : la même suite de nombres dans tous les navigateurs. */
export function seeded(seed) {
    let a = seed >>> 0;
    return () => {
        a = (a + 0x6D2B79F5) >>> 0;
        let t = a;
        t = Math.imul(t ^ (t >>> 15), t | 1);
        t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
        return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
}
/** Nombre au hasard entre <min> et <max>, tiré par <rand>. */
export const between = (rand, min, max) => min + rand() * (max - min);
// Racine exacte partout (Math.hypot peut différer d'un navigateur à l'autre au dernier chiffre : simulation commune)
export const hypot = (x, y) => Math.sqrt(x * x + y * y);
export const clamp = (v, min, max) => Math.max(min, Math.min(max, v));
export const distance = (a, b) => hypot(a.x - b.x, a.y - b.y);

/** Humeurs, de la plus basse à la plus haute (ParkPokemon.Mood). */
export const MOODS = ["sad", "normal", "happy"];
/**
 * Humeur <mood> abaissée de <levels> niveaux (malus « attaqué »), jamais plus bas que « sad » ; une humeur inconnue compte
 * comme « normal ». Même règle que ParkHabitats.WithMalus côté serveur.
 */
export function loweredMood(mood, levels) {
    const rank = MOODS.indexOf(mood);
    return MOODS[Math.max(0, (rank < 0 ? 1 : rank) - levels)];
}

/** Heure de la nuit : 0 le jour, 1 la nuit (22 h – 6 h), progressif le soir (19 h – 22 h) et à l'aube (6 h – 8 h). */
export function nightLevel(date = new Date()) {
    const h = date.getHours() + date.getMinutes() / 60;
    if (h >= 22 || h < 6) return 1;
    if (h >= 19) return (h - 19) / 3;
    if (h < 8) return 1 - (h - 6) / 2;
    return 0;
}

/**
 * Caractère tiré de la nature (index PKHeX : statistique augmentée = n / 5, diminuée = n % 5 ; 2 = Vitesse, 4 = Déf. Spé.),
 * avec un peu de hasard (<rand>).
 */
export function personalityOf(nature, rand) {
    const up = Math.floor(nature / 5), down = nature % 5, neutral = up === down;
    return {
        activity: clamp(1 + (neutral ? 0 : (up === 2) * 0.3 - (down === 2) * 0.3) + between(rand, -0.1, 0.1), 0.6, 1.4),
        sleepy: clamp((neutral ? 0 : (down === 2) * 0.12 - (up === 2) * 0.08) + between(rand, -0.05, 0.05), -0.1, 0.2),
        sociable: clamp(0.55 + (neutral ? 0 : (up === 4) * 0.25 - (down === 4) * 0.2) + between(rand, -0.1, 0.1), 0.2, 0.95),
        curious: between(rand, 0.3, 1),
    };
}

/**
 * Terrain de la carte : cases où l'on peut marcher (dans la zone « Parc », sans tuile du calque « Collisions »), et
 * chemins entre deux points (A* sur les cases, en diagonale seulement si les deux côtés sont libres, puis lissé en
 * lignes droites). Sans carte : un pré de 24 × 20 cases, sans obstacle.
 */
export class Ground {
    /** <rand> : le tirage au hasard de la simulation (Math.random, ou seeded au cache-cache) pour randomSpot. */
    constructor(data, rand = Math.random) {
        this.data = data;
        this.rand = rand;
        this.columns = data?.width ?? 24;
        this.lines = data?.height ?? 20;
        const zones = data?.layers.find(l => l.name === "Zones")?.objects ?? [];
        const tiles = zone => zone && { x: Math.round(zone.x / TILE), y: Math.round(zone.y / TILE), w: Math.round(zone.width / TILE), h: Math.round(zone.height / TILE) };
        // Vue de départ (sa hauteur remplit l'écran) ; partie de la carte où vivent les Pokémon (le reste est assombri)
        this.start = tiles(zones.find(o => o.name === "Départ")) ?? { x: 0, y: 0, w: this.columns, h: this.lines };
        this.area = tiles(zones.find(o => o.name === "Parc")) ?? this.start;
        // Type de chaque case (« habitats » de la carte, écrit par outils/carte-parc.py : un chiffre par case, 0 aucun,
        // 1 sable, 2 cave, 3 falaise, 4 végétation, 5 eau, 6 donjon, 7 volcan ; comme Services/ParkHabitats.cs)
        this.habitats = new Uint8Array(this.columns * this.lines);
        (data?.habitats ?? []).forEach((row, y) => {
            for (let x = 0; x < this.columns && x < row.length && y < this.lines; x++) {
                const h = row.codePointAt(x) - 48;
                if (h >= 0 && h <= 7) this.habitats[y * this.columns + x] = h;
            }
        });
        const layer = name => data?.layers.find(l => l.name === name && l.type === "tilelayer")?.data ?? [];
        const collisions = layer("Collisions");
        this.walkable = new Uint8Array(this.columns * this.lines); // LAND, WATER, LAVA, SUMMIT, AIR, ou 0 : bloquée pour tous
        this.free = []; // cases de terre libres, pour tirer un endroit au hasard
        this.summits = []; // dessus des plateaux et des sommets (montagne), de même
        // Montagne : cases bloquées dans « Collisions » (personne n'y marche) mais où un volant se pose, ou qu'il survole
        const summits = layer("Sommets"), air = layer("Survol");
        const { x: sx, y: sy, w: sw, h: sh } = this.area;
        for (let y = sy; y < sy + sh; y++) {
            for (let x = sx; x < sx + sw; x++) {
                const i = y * this.columns + x;
                if (x < 0 || y < 0 || x >= this.columns || y >= this.lines) continue;
                if (!collisions[i]) {
                    this.walkable[i] = LAND;
                    this.free.push(i);
                } else if (summits[i]) {
                    this.walkable[i] = SUMMIT;
                    this.summits.push(i);
                } else if (air[i]) this.walkable[i] = AIR;
            }
        }
        const decors = layer("Décors"), front = layer("Devant");
        this.water = this.openLiquid(collisions, decors, front, 5, WATER); // cases d'eau libre, au hasard aussi
        this.lava = this.openLiquid(collisions, decors, front, 7, LAVA); // cases de lave libre
        this.liquid = [...this.water, ...this.lava]; // survol d'un volant
        // Recherche de chemin : tableaux gardés d'une recherche à l'autre (pas de déchets à chaque décision)
        const n = this.columns * this.lines;
        this.cost = new Float64Array(n);
        this.previous = new Int32Array(n);
        this.seen = new Uint32Array(n); // numéro de la recherche qui a vu la case (évite de tout remettre à zéro)
        this.closed = new Uint32Array(n);
        // Cases à explorer : tas binaire (la plus prometteuse d'abord), une case peut y être plusieurs fois
        this.heap = new Int32Array(8 * n + 1);
        this.heapScore = new Float64Array(8 * n + 1);
        this.search = 0;
        this.mask = LAND; // cases permises de la recherche en cours (path)
    }

    /**
     * Eau libre (<bit> WATER dans walkable, <habitat> 5) ou lave libre (LAVA, 7) : les cases de ce type bloquées sans objet
     * posé dessus (rocher, nénuphar, récif, pont ; les bulles de lave sont dans un autre calque), et les cases bloquées sans
     * objet qui les touchent (écume au bord du sable : pour entrer dans l'eau et en sortir). Seulement ce qui est relié à
     * la terre où l'on marche (pas de mare enfermée entre des rochers) ; sans terre (l'océan, vue de départ), à la case de
     * ce type la plus proche du centre de la vue de départ. Rend ces cases.
     */
    openLiquid(collisions, decors, front, habitat, bit) {
        const bounds = this.areaBounds();
        const bare = i => collisions[i] && !decors[i] && !front[i]; // bloquée, sans objet
        const open = this.liquidTiles(bounds, bare, habitat);
        // Reliée à la terre : de proche en proche (par les côtés) depuis les cases où l'on marche
        const cells = [], queue = [...this.free];
        if (!queue.length) {
            const first = this.liquidNearStart(open);
            if (first < 0) return cells;
            this.walkable[first] = bit;
            cells.push(first);
            queue.push(first);
        }
        return cells.concat(this.spreadLiquid(queue, open, bounds, bit));
    }

    /** Zone « Parc » ramenée à la carte (x0 ≤ x < x1, y0 ≤ y < y1), et le test « dans la zone ». */
    areaBounds() {
        const { x: sx, y: sy, w: sw, h: sh } = this.area;
        const x0 = Math.max(0, sx), y0 = Math.max(0, sy), x1 = Math.min(this.columns, sx + sw), y1 = Math.min(this.lines, sy + sh);
        return { x0, y0, x1, y1, inside: (x, y) => x >= x0 && y >= y0 && x < x1 && y < y1 };
    }

    /** Cases du liquide dans la zone : 1 le liquide (bloqué, sans objet), 2 son bord bloqué sans objet, 0 le reste. */
    liquidTiles({ x0, y0, x1, y1, inside }, bare, habitat) {
        const { columns } = this;
        const open = new Uint8Array(columns * this.lines);
        for (let y = y0; y < y1; y++)
            for (let x = x0; x < x1; x++)
                if (bare(y * columns + x) && this.habitats[y * columns + x] === habitat) open[y * columns + x] = 1;
        const touchesLiquid = (x, y) => {
            for (let dy = -1; dy <= 1; dy++)
                for (let dx = -1; dx <= 1; dx++)
                    if (inside(x + dx, y + dy) && open[(y + dy) * columns + x + dx] === 1) return true;
            return false;
        };
        for (let y = y0; y < y1; y++)
            for (let x = x0; x < x1; x++)
                if (!open[y * columns + x] && bare(y * columns + x) && !this.walkable[y * columns + x] && touchesLiquid(x, y))
                    open[y * columns + x] = 2; // le bord (pas déjà pris par l'autre liquide)
        return open;
    }

    /** Sans terre (l'océan) : la case de liquide la plus proche du centre de la vue de départ (-1 s'il n'y en a pas). */
    liquidNearStart(open) {
        const { columns } = this;
        const cx = this.start.x + this.start.w / 2, cy = this.start.y + this.start.h / 2;
        let first = -1, best = Infinity;
        for (let i = 0; i < open.length; i++) {
            const d = (i % columns + 0.5 - cx) ** 2 + (Math.floor(i / columns) + 0.5 - cy) ** 2;
            if (open[i] === 1 && !this.walkable[i] && d < best) { first = i; best = d; }
        }
        return first;
    }

    /** Ouvre (<bit>) les cases de liquide reliées par les côtés à celles de <queue>, de proche en proche. Rend ces cases. */
    spreadLiquid(queue, open, { inside }, bit) {
        const { columns } = this, cells = [], seen = new Uint8Array(columns * this.lines);
        for (const i of queue) seen[i] = 1;
        while (queue.length) {
            const i = queue.pop(), x = i % columns, y = Math.floor(i / columns);
            for (const [nx, ny] of [[x + 1, y], [x - 1, y], [x, y + 1], [x, y - 1]]) {
                const j = ny * columns + nx;
                if (!inside(nx, ny) || seen[j] || !open[j] || this.walkable[j]) continue;
                seen[j] = 1;
                this.walkable[j] = bit;
                cells.push(j);
                queue.push(j);
            }
        }
        return cells;
    }

    get width() { return this.columns * TILE; }
    get height() { return this.lines * TILE; }

    /** Case où l'on peut aller avec <mask> (LAND, WATER, LAVA, SUMMIT, AIR, ou plusieurs). */
    tileFree(tx, ty, mask = LAND) { return tx >= 0 && ty >= 0 && tx < this.columns && ty < this.lines && (this.walkable[ty * this.columns + tx] & mask) !== 0; }
    isFree(x, y, mask = LAND) { return this.tileFree(Math.floor(x / TILE), Math.floor(y / TILE), mask); }
    /** Type de la case (cases de la carte) ; hors de la carte : 0 (aucun). */
    habitatOfTile(tx, ty) { return tx >= 0 && ty >= 0 && tx < this.columns && ty < this.lines ? this.habitats[ty * this.columns + tx] : 0; }
    /** Type de la case sous le point (pixels de la carte). */
    habitatAt(x, y) { return this.habitatOfTile(Math.floor(x / TILE), Math.floor(y / TILE)); }
    /** Sous le point : WATER (eau libre), LAVA (lave libre), ou 0 (la terre, une case bloquée, hors de la carte). */
    liquidAt(x, y) {
        const tx = Math.floor(x / TILE), ty = Math.floor(y / TILE);
        return tx >= 0 && ty >= 0 && tx < this.columns && ty < this.lines ? this.walkable[ty * this.columns + tx] & LIQUID : 0;
    }
    /** Sous le point, une case qu'un volant ne fait que survoler (eau, lave, rebord ou flanc de la montagne) : il y vole haut. */
    flightAt(x, y) {
        const tx = Math.floor(x / TILE), ty = Math.floor(y / TILE);
        return tx >= 0 && ty >= 0 && tx < this.columns && ty < this.lines && (this.walkable[ty * this.columns + tx] & (LIQUID | AIR)) !== 0;
    }
    /**
     * Part des promenades (et des arrivées) d'un nageur dans l'eau : WATER_SHARE, ou plus quand la terre est rare (la part
     * de l'eau parmi les cases libres) : à l'océan, quelques îlots de 8 à 68 cases attiraient un tiers des promenades, et
     * tous les nageurs s'y entassaient. Sans terre : toujours l'eau.
     */
    get waterShare() {
        const all = this.water.length + this.free.length;
        return all ? Math.max(WATER_SHARE, this.water.length / all) : 1;
    }
    center(i) { return { x: (i % this.columns) * TILE + TILE / 2, y: Math.floor(i / this.columns) * TILE + TILE / 2 }; }

    /**
     * Endroit libre au hasard (un peu décalé du centre de sa case), sur la terre, ou dans l'eau libre (WATER), la lave libre
     * (LAVA), l'une ou l'autre (LIQUID), ou sur un sommet (SUMMIT) s'il y en a.
     */
    randomSpot(where = LAND) {
        const wanted = { [WATER]: this.water, [LAVA]: this.lava, [LIQUID]: this.liquid, [SUMMIT]: this.summits }[where] ?? this.free;
        const fallback = this.free.length ? this.free : this.liquid; // sans terre (océan) : l'eau
        const cells = wanted.length ? wanted : fallback;
        if (!cells.length) return { x: this.width / 2, y: this.height / 2 };
        const c = this.center(cells[Math.floor(this.rand() * cells.length)]);
        return { x: c.x + between(this.rand, -4, 4), y: c.y + between(this.rand, -4, 4) };
    }

    /** Le point lui-même s'il est libre (avec <mask>), sinon le centre de la case libre la plus proche. */
    nearestFree(x, y, mask = LAND) {
        if (this.isFree(x, y, mask)) return { x, y };
        let best = -1, bestDistance = Infinity;
        const look = cells => {
            for (const i of cells) {
                const cx = (i % this.columns) * TILE + TILE / 2, cy = Math.floor(i / this.columns) * TILE + TILE / 2;
                const d = (cx - x) ** 2 + (cy - y) ** 2;
                if (d < bestDistance) { best = i; bestDistance = d; }
            }
        };
        if (mask & LAND) look(this.free);
        if (mask & WATER) look(this.water);
        if (mask & LAVA) look(this.lava);
        if (mask & SUMMIT) look(this.summits);
        return best < 0 ? { x, y } : this.center(best);
    }

    /** Ligne droite sans obstacle pour <mask> (vérifiée tous les 4 pixels). */
    lineFree(a, b, mask = LAND) {
        const steps = Math.ceil(distance(a, b) / 4);
        for (let s = 1; s <= steps; s++) {
            if (!this.isFree(a.x + (b.x - a.x) * s / steps, a.y + (b.y - a.y) * s / steps, mask)) return false;
        }
        return true;
    }

    /** Points de passage de <from> à <to> (sans <from>) par les cases de <mask>, ou null s'il n'y a pas de chemin. */
    path(from, to, mask = LAND) {
        this.mask = mask; // pour canStep et smooth
        if (this.lineFree(from, to, mask)) return [to];
        const start = this.nearestFree(from.x, from.y, mask);
        const startTile = Math.floor(start.y / TILE) * this.columns + Math.floor(start.x / TILE);
        const goalTile = Math.floor(to.y / TILE) * this.columns + Math.floor(to.x / TILE);
        if (!this.explore(startTile, goalTile)) return null;
        const tiles = [];
        for (let t = goalTile; t !== startTile; t = this.previous[t]) tiles.unshift(t);
        return this.smooth([start, ...tiles.slice(0, -1).map(t => this.center(t)), to]);
    }

    /** Recherche A* de <startTile> à <goalTile> : vrai si elle aboutit (chemin dans previous, depuis l'arrivée). */
    explore(startTile, goalTile) {
        const gx = goalTile % this.columns, gy = Math.floor(goalTile / this.columns);
        const columns = this.columns, cost = this.cost, previous = this.previous, seen = this.seen, closed = this.closed;
        const heap = this.heap, score = this.heapScore;
        let size = 0;
        const push = (tile, value) => {
            let i = size++;
            while (i > 0) {
                const parent = (i - 1) >> 1;
                if (score[parent] <= value) break;
                heap[i] = heap[parent]; score[i] = score[parent]; i = parent;
            }
            heap[i] = tile; score[i] = value;
        };
        const pop = () => {
            const top = heap[0], tile = heap[--size], value = score[size];
            let i = 0;
            for (let child = 1; child < size; child = 2 * i + 1) {
                if (child + 1 < size && score[child + 1] < score[child]) child++;
                if (score[child] >= value) break;
                heap[i] = heap[child]; score[i] = score[child]; i = child;
            }
            heap[i] = tile; score[i] = value;
            return top;
        };
        const estimate = i => {
            const dx = Math.abs(i % columns - gx), dy = Math.abs(Math.floor(i / columns) - gy);
            return cost[i] + Math.max(dx, dy) + (Math.SQRT2 - 1) * Math.min(dx, dy);
        };
        const search = ++this.search;
        let current = startTile;
        const visit = (next, step) => {
            const c = cost[current] + step;
            if (closed[next] === search || (seen[next] === search && c >= cost[next])) return;
            seen[next] = search;
            cost[next] = c;
            previous[next] = current;
            push(next, estimate(next));
        };
        cost[startTile] = 0;
        seen[startTile] = search;
        push(startTile, estimate(startTile));
        while (size) {
            current = pop();
            if (closed[current] === search) continue; // déjà exploré par un chemin plus court
            if (current === goalTile) return true;
            closed[current] = search;
            this.forEachNeighbour(current, visit);
        }
        return false;
    }

    /** Appelle visit(case, coût du pas) pour chaque case voisine où l'on peut aller. */
    forEachNeighbour(tile, visit) {
        const columns = this.columns, cx = tile % columns, cy = Math.floor(tile / columns);
        for (let dy = -1; dy <= 1; dy++) {
            for (let dx = -1; dx <= 1; dx++) {
                if (this.canStep(cx, cy, dx, dy)) visit((cy + dy) * columns + cx + dx, dx && dy ? Math.SQRT2 : 1);
            }
        }
    }

    /** Pas de (cx, cy) vers la case voisine (cx + dx, cy + dy) : libre, et sans couper de coin en diagonale. */
    canStep(cx, cy, dx, dy) {
        const mask = this.mask;
        if ((!dx && !dy) || !this.tileFree(cx + dx, cy + dy, mask)) return false;
        return !dx || !dy || (this.tileFree(cx + dx, cy, mask) && this.tileFree(cx, cy + dy, mask));
    }

    /** Lissage : du point actuel, aller tout droit tant qu'on voit le point de passage suivant. */
    smooth(points) {
        const result = [];
        for (let i = 0; i < points.length - 1;) {
            let j = i + 1;
            while (j + 1 < points.length && this.lineFree(points[i], points[j + 1], this.mask)) j++;
            result.push(points[j]);
            i = j;
        }
        return result;
    }
}
