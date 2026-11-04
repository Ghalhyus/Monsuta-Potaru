// Parc : les Pokémon s'y promènent avec les sprites Donjon Mystère de SpriteCollab (animations Idle, Walk et Sleep), sur
// une carte faite dans Tiled avec le pack Cute Fantasy (outils/carte-parc.py), affichée par Phaser (moteur de jeu 2D).
// Tout se passe dans le navigateur ; le serveur ne donne que la liste des Pokémon (Park.razor) et est prévenu d'un clic sur
// l'un d'eux.
//
// Comportement : chaque Pokémon a une énergie (baisse en marchant, remonte en dormant : il s'endort quand il est fatigué,
// plus tôt la nuit), une intention (explorer un endroit, rejoindre un Pokémon de sa famille, observer autour de lui, aller
// dormir près d'un congénère), un caractère tiré de sa nature (plus ou moins actif, dormeur, sociable) et de la curiosité
// pour le pointeur de la souris. Il contourne les obstacles (calque « Collisions » de la carte) et reste dans la zone
// « Parc » (la taille achetée : forêt dense autour, assombrie) ; on la parcourt en faisant glisser la vue. Les types Eau
// nagent dans l'eau libre (surtout là), les types Feu vont de temps en temps dans la lave libre, les types Vol survolent
// l'une et l'autre mais se posent sur la terre ferme (ParkPokemon.Moves). Les tuiles animées de la carte (cascades de lave,
// bulles, torches) sont jouées par-dessus la carte dessinée ; une grotte est dans la pénombre, éclairée par ses torches et
// ses cristaux (calque « Lumières »).
import * as Phaser from "../lib/phaser/phaser.esm.min.js";

// Carte : coordonnées du parc en pixels de la carte (16 par tuile). La hauteur affichée est celle de la zone « Départ »
// (320 pixels), à un nombre entier de pixels d'écran par pixel de carte (dessin net). Une carte par paysage (« parc-1 »,
// « plage-1 », « volcan-1 », « grotte-1 »... : ParkLandscapes.MapName) et par taille du parc (1 à 4, agrandissements achetés + 1, ParkService), toutes de
// la même taille pour un paysage : seule la zone « Parc » et ce qui la cache autour changent.
const MAP_URL = (map, size) => `parc/${map}-${size}.json`;
const VIEW_MARGIN = 6; // cases de forêt qu'on peut voir au-delà de la zone « Parc » en faisant glisser la vue
const TILES_URL = "images/parc/tuiles/"; // planches du pack, hors de Git (deploy/fond-parc.cmd)
const TILE = 16;
const GRASS = 0x33984b; // couleur de l'herbe, sans les planches (absentes, ou pas encore chargées)
const OUTSIDE = "#132a1c"; // autour de la carte, quand on a dézoomé plus large qu'elle
// Icônes des boutons sur la carte (Bootstrap Icons : dash-lg, plus-lg, fullscreen, fullscreen-exit)
const svg = path => `<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="currentColor" viewBox="0 0 16 16" aria-hidden="true"><path fill-rule="evenodd" d="${path}"/></svg>`;
const ICONS = {
    minus: svg("M2 8a.5.5 0 0 1 .5-.5h11a.5.5 0 0 1 0 1h-11A.5.5 0 0 1 2 8"),
    plus: svg("M8 2a.5.5 0 0 1 .5.5v5h5a.5.5 0 0 1 0 1h-5v5a.5.5 0 0 1-1 0v-5h-5a.5.5 0 0 1 0-1h5v-5A.5.5 0 0 1 8 2"),
    expand: svg("M1.5 1a.5.5 0 0 0-.5.5v4a.5.5 0 0 1-1 0v-4A1.5 1.5 0 0 1 1.5 0h4a.5.5 0 0 1 0 1zM10 .5a.5.5 0 0 1 .5-.5h4A1.5 1.5 0 0 1 16 1.5v4a.5.5 0 0 1-1 0v-4a.5.5 0 0 0-.5-.5h-4a.5.5 0 0 1-.5-.5M.5 10a.5.5 0 0 1 .5.5v4a.5.5 0 0 0 .5.5h4a.5.5 0 0 1 0 1h-4A1.5 1.5 0 0 1 0 14.5v-4a.5.5 0 0 1 .5-.5m15 0a.5.5 0 0 1 .5.5v4a1.5 1.5 0 0 1-1.5 1.5h-4a.5.5 0 0 1 0-1h4a.5.5 0 0 0 .5-.5v-4a.5.5 0 0 1 .5-.5"),
    shrink: svg("M5.5 0a.5.5 0 0 1 .5.5v4A1.5 1.5 0 0 1 4.5 6h-4a.5.5 0 0 1 0-1h4a.5.5 0 0 0 .5-.5v-4a.5.5 0 0 1 .5-.5m5 0a.5.5 0 0 1 .5.5v4a.5.5 0 0 0 .5.5h4a.5.5 0 0 1 0 1h-4A1.5 1.5 0 0 1 10 4.5v-4a.5.5 0 0 1 .5-.5M0 10.5a.5.5 0 0 1 .5-.5h4A1.5 1.5 0 0 1 6 11.5v4a.5.5 0 0 1-1 0v-4a.5.5 0 0 0-.5-.5h-4a.5.5 0 0 1-.5-.5m10 1a1.5 1.5 0 0 1 1.5-1.5h4a.5.5 0 0 1 0 1h-4a.5.5 0 0 0-.5.5v4a.5.5 0 0 1-1 0z"),
};
// Hors de la zone « Parc » : de plus en plus sombre, une marche par case à partir de la deuxième (la lisière reste claire)
const FOG = { color: 0x0b1222, alpha: 0.18, steps: 4 };
// Profondeur d'affichage d'un Pokémon ou d'un feuillage, selon la ligne de ses pieds (de son tronc) : les plus bas devant.
// Entre 2 et 3 : au-dessus de la carte (0) et des ombres (1,9), sous le brouillard (3) et la nuit (4).
const depthOf = y => 2 + y / 10000;
const NIGHT = { color: 0x101848, alpha: 0.38 };
// Pénombre d'une grotte (calque d'objets « Lumières » de la carte : torches, cristaux) : un voile sombre sur tout, devant les
// Pokémon et sous le brouillard, éclairci autour de chaque lumière (clear : part du voile retirée au centre) qui y ajoute un
// peu de sa couleur (glow) ; dessinée une seule fois
const SHADE = { color: 0x0a0510, alpha: 0.55, depth: 2.9, clear: 0.9, glow: 0.3 };
const HABITAT_DEPTH = 3.2; // types de cases (mode debug) : devant le brouillard, sous un Pokémon soulevé et la nuit
const HABITAT_ALPHA = 0.45;
const ANIMATED_DEPTH = 0.5; // tuiles animées : sur la carte (0), sous les ombres (1,9)
// Taille d'un pixel de sprite par rapport à un pixel de la carte (deux tiers : ×2 quand la carte est à ×3), arrondie à un
// nombre entier de pixels d'écran (dessin net), jamais moins de 2 pixels d'écran tant que la carte en a autant.
const SPRITE_SIZE = 2 / 3;
const WALK_SPEED = 27; // vitesse de marche (pixels de la carte par seconde), selon le caractère
const WALK_ANIMATION_SPEED = 30; // vitesse à laquelle l'animation de marche joue à son rythme normal (pieds sans glisser),
                                 // pour des sprites à la taille des pixels de la carte (plus petits : foulée plus courte)
const DRAG_THRESHOLD = 6; // pixels (CSS) avant qu'un clic devienne un glissement de la vue (ou soulève un Pokémon)
const LONG_PRESS = 300; // ms d'appui sans bouger, au doigt, pour soulever un Pokémon (sinon le doigt fait glisser la vue)
const LIFT = 8; // hauteur (pixels de la carte) d'un Pokémon soulevé au-dessus de son ombre
const HELD_DEPTH = 3.5; // un Pokémon soulevé passe devant tout (feuillages, brouillard), sous la nuit (4)
const EDGE_SCROLL = { margin: 48, speed: 420 }; // en le portant près du bord (pixels CSS) : la vue défile (pixels CSS / s)
const ANIMATIONS = ["Idle", "Walk", "Sleep", "Charge", "Attack", "Hurt"];
const OPTIONAL = new Set(["Charge", "Attack", "Hurt"]); // planche absente (images d'avant, sprite incomplet) : Idle à la place
// Selon l'état du Pokémon ; « attack » et « hurt » sont joués une fois (play)
const ANIMATION_OF = { idle: "Idle", walk: "Walk", sleep: "Sleep", held: "Charge", attack: "Attack", hurt: "Hurt" };
const ONCE = new Set(["attack", "hurt"]);
// Pokémon malheureux (habitats du parc : Services/ParkHabitats.cs) : de temps en temps, il attaque un Pokémon proche
// (secondes entre deux attaques ; sans cible à portée, il réessaie un peu plus tard) et boude (Hurt joué seul, immobile)
const SAD = { attackEvery: [45, 90], retry: [5, 10], reach: 128, sulkEvery: [30, 50] };
const STRIKE_RANGE = 26; // distance (pixels de la carte) d'où il frappe
const FLEE = { speed: 2, distance: [96, 160] }; // le Pokémon frappé s'enfuit plus vite, à 6 à 10 cases de l'attaquant
// Énergie (par seconde) : 3 à 5 minutes d'activité le jour avant une sieste d'environ une minute ; la nuit, ils
// s'endorment plus tôt et récupèrent lentement (longues nuits).
const ENERGY = { walk: 0.004, idle: 0.0012, sleepDay: 0.012, sleepNight: 0.004 };
// Cases où l'on peut aller (masques de Ground.walkable) : LAND, la terre où l'on marche ; WATER, l'eau libre (sans rocher ni
// objet posé) et le bord qui la touche (écume de la plage), pour les nageurs et les volants ; LAVA, la lave libre (de même),
// pour les types Feu et les volants
const LAND = 1, WATER = 2, LAVA = 4, LIQUID = WATER | LAVA;
// Nageur : part de ses destinations dans l'eau ; dans l'eau, le sprite descend de <sink> pixels de sprite et n'est plus
// dessiné sous la surface (la ligne de ses pieds) ; des remous à la place de l'ombre
const SWIM = { waterShare: 2 / 3, sink: 4 };
// Type Feu : part de ses promenades dans la lave, « de temps en temps », bien moins que les nageurs dans l'eau (choix de
// l'utilisateur, 2026-11-01) ; il n'y dort pas et arrive au parc sur la terre ferme. Dans la lave, il s'enfonce comme un
// nageur (SWIM.sink), avec des remous dorés.
const LAVA_BATH = { share: 1 / 4 };
const FLY_HEIGHT = 10; // hauteur (pixels de la carte) d'un volant au-dessus de l'eau, son ombre restant sur l'eau
const FLY_OVER = 0.3; // volant : part de ses promenades qui passent au-dessus de l'eau (un tour, puis il se pose à terre)

// Lignes des planches de SpriteCollab : bas, bas-droite, droite, haut-droite, haut, haut-gauche, gauche, bas-gauche.
const FRONT = 0;
const directionOf = (dx, dy) => (((Math.round((90 - Math.atan2(dy, dx) * 180 / Math.PI) / 45)) % 8) + 8) % 8;
const vectorOf = dir => { const a = (90 - dir * 45) * Math.PI / 180; return [Math.cos(a), Math.sin(a)]; };

const random = (min, max) => min + Math.random() * (max - min);
const chance = (perSecond, dt) => Math.random() < perSecond * dt; // probabilité par seconde, ramenée au pas de temps
const clamp = (v, min, max) => Math.max(min, Math.min(max, v));
const distance = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);

/** Animations d'un dossier de sprites (AnimData.xml + planches), chargées une fois par dossier. */
const sheets = new Map();
function loadSheets(folder) {
    if (!sheets.has(folder)) {
        sheets.set(folder, (async () => {
            // La liste des animations dans l'adresse : une copie gardée par le navigateur d'avant l'ajout d'une animation
            // (sans elle) ne sert plus
            const response = await fetch(`${folder}AnimData.xml?animations=${ANIMATIONS.join(",")}`);
            if (!response.ok) throw new Error(`Sprites absents : ${folder}`);
            const xml = new DOMParser().parseFromString(await response.text(), "text/xml");
            const defs = new Map([...xml.querySelectorAll("Anim")].map(a => [a.querySelector("Name").textContent, a]));
            const loaded = await Promise.all(ANIMATIONS.map(name => loadAnimation(folder, defs, name))); // planches en parallèle
            const result = {};
            ANIMATIONS.forEach((name, i) => { if (loaded[i]) result[name] = loaded[i]; });
            result.Idle ??= result.Walk; // au cas où une planche manquerait
            if (!result.Idle) throw new Error(`Ni Idle ni Walk : ${folder}`); // ce Pokémon n'apparaît pas (setPokemon)
            result.Walk ??= result.Idle;
            result.Sleep ??= result.Idle;
            result.Charge ??= result.Idle; // soulevé
            result.Attack ??= result.Idle;
            result.Hurt ??= result.Idle;
            return result;
        })());
    }
    return sheets.get(folder);
}

/** Une animation d'un dossier de sprites (définitions de AnimData.xml), ou null si elle manque. */
async function loadAnimation(folder, defs, name) {
    let def = defs.get(name);
    while (def?.querySelector("CopyOf")) def = defs.get(def.querySelector("CopyOf").textContent); // animation copiée d'une autre
    if (!def) return null;
    const source = def.querySelector("Name").textContent;
    const image = new Image();
    image.src = `${folder}${source}-Anim.png`;
    try {
        await image.decode();
    } catch (e) {
        if (OPTIONAL.has(name)) return null;
        throw e;
    }
    const fw = +def.querySelector("FrameWidth").textContent, fh = +def.querySelector("FrameHeight").textContent;
    return {
        image, fw, fh,
        key: image.src, // texture de Phaser
        columns: Math.max(1, Math.round(image.width / fw)),
        rows: Math.max(1, Math.round(image.height / fh)),
        durations: [...def.querySelectorAll("Duration")].map(d => +d.textContent * 1000 / 60), // images à 60 par seconde
    };
}

/** Heure de la nuit : 0 le jour, 1 la nuit (22 h – 6 h), progressif le soir (19 h – 22 h) et à l'aube (6 h – 8 h). */
function nightLevel(date = new Date()) {
    const h = date.getHours() + date.getMinutes() / 60;
    if (h >= 22 || h < 6) return 1;
    if (h >= 19) return (h - 19) / 3;
    if (h < 8) return 1 - (h - 6) / 2;
    return 0;
}

/** Caractère tiré de la nature (index PKHeX : statistique augmentée = n / 5, diminuée = n % 5 ; 2 = Vitesse, 4 = Déf. Spé.). */
function personalityOf(nature) {
    const up = Math.floor(nature / 5), down = nature % 5, neutral = up === down;
    return {
        activity: clamp(1 + (neutral ? 0 : (up === 2) * 0.3 - (down === 2) * 0.3) + random(-0.1, 0.1), 0.6, 1.4),
        sleepy: clamp((neutral ? 0 : (down === 2) * 0.12 - (up === 2) * 0.08) + random(-0.05, 0.05), -0.1, 0.2),
        sociable: clamp(0.55 + (neutral ? 0 : (up === 4) * 0.25 - (down === 4) * 0.2) + random(-0.1, 0.1), 0.2, 0.95),
        curious: random(0.3, 1),
    };
}

/**
 * Terrain de la carte : cases où l'on peut marcher (dans la zone « Parc », sans tuile du calque « Collisions »), et
 * chemins entre deux points (A* sur les cases, en diagonale seulement si les deux côtés sont libres, puis lissé en
 * lignes droites). Sans carte : un pré de 24 × 20 cases, sans obstacle.
 */
class Ground {
    constructor(data) {
        this.data = data;
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
                const h = row.charCodeAt(x) - 48;
                if (h >= 0 && h <= 7) this.habitats[y * this.columns + x] = h;
            }
        });
        const layer = name => data?.layers.find(l => l.name === name && l.type === "tilelayer")?.data ?? [];
        const collisions = layer("Collisions");
        this.walkable = new Uint8Array(this.columns * this.lines); // LAND, WATER, ou 0 : bloquée pour tous
        this.free = []; // cases de terre libres, pour tirer un endroit au hasard
        const { x: sx, y: sy, w: sw, h: sh } = this.area;
        for (let y = sy; y < sy + sh; y++) {
            for (let x = sx; x < sx + sw; x++) {
                const i = y * this.columns + x;
                if (x < 0 || y < 0 || x >= this.columns || y >= this.lines || collisions[i]) continue;
                this.walkable[i] = LAND;
                this.free.push(i);
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
        const { columns } = this, { x: sx, y: sy, w: sw, h: sh } = this.area;
        const x0 = Math.max(0, sx), y0 = Math.max(0, sy), x1 = Math.min(columns, sx + sw), y1 = Math.min(this.lines, sy + sh);
        const inArea = (x, y) => x >= x0 && y >= y0 && x < x1 && y < y1;
        const bare = i => collisions[i] && !decors[i] && !front[i]; // bloquée, sans objet
        const open = new Uint8Array(columns * this.lines);
        for (let y = y0; y < y1; y++)
            for (let x = x0; x < x1; x++)
                if (bare(y * columns + x) && this.habitats[y * columns + x] === habitat) open[y * columns + x] = 1;
        const touchesWater = (x, y) => {
            for (let dy = -1; dy <= 1; dy++)
                for (let dx = -1; dx <= 1; dx++)
                    if (inArea(x + dx, y + dy) && open[(y + dy) * columns + x + dx] === 1) return true;
            return false;
        };
        for (let y = y0; y < y1; y++)
            for (let x = x0; x < x1; x++)
                if (!open[y * columns + x] && bare(y * columns + x) && !this.walkable[y * columns + x] && touchesWater(x, y))
                    open[y * columns + x] = 2; // le bord (pas déjà pris par l'autre liquide)
        // Reliée à la terre : de proche en proche (par les côtés) depuis les cases où l'on marche
        const cells = [], queue = [...this.free], seen = new Uint8Array(columns * this.lines);
        if (!queue.length) {
            const cx = this.start.x + this.start.w / 2, cy = this.start.y + this.start.h / 2;
            let first = -1, best = Infinity;
            for (let i = 0; i < open.length; i++) {
                const d = (i % columns + 0.5 - cx) ** 2 + (Math.floor(i / columns) + 0.5 - cy) ** 2;
                if (open[i] === 1 && !this.walkable[i] && d < best) { first = i; best = d; }
            }
            if (first < 0) return cells;
            this.walkable[first] = bit;
            cells.push(first);
            queue.push(first);
        }
        for (const i of queue) seen[i] = 1;
        while (queue.length) {
            const i = queue.pop(), x = i % columns, y = Math.floor(i / columns);
            for (const [nx, ny] of [[x + 1, y], [x - 1, y], [x, y + 1], [x, y - 1]]) {
                const j = ny * columns + nx;
                if (!inArea(nx, ny) || seen[j] || !open[j] || this.walkable[j]) continue;
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

    /** Case où l'on peut aller avec <mask> (LAND, WATER, LAVA, ou plusieurs). */
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
    center(i) { return { x: (i % this.columns) * TILE + TILE / 2, y: Math.floor(i / this.columns) * TILE + TILE / 2 }; }

    /**
     * Endroit libre au hasard (un peu décalé du centre de sa case), sur la terre, ou dans l'eau libre (WATER), la lave libre
     * (LAVA) ou l'une ou l'autre (LIQUID) s'il y en a.
     */
    randomSpot(where = LAND) {
        const liquid = where === WATER ? this.water : where === LAVA ? this.lava : where === LIQUID ? this.liquid : this.free;
        const cells = liquid.length ? liquid : this.free.length ? this.free : this.liquid; // sans terre (océan) : l'eau
        if (!cells.length) return { x: this.width / 2, y: this.height / 2 };
        const c = this.center(cells[Math.floor(Math.random() * cells.length)]);
        return { x: c.x + random(-4, 4), y: c.y + random(-4, 4) };
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

class Pokemon {
    constructor(info, anims, ground, x, y) {
        Object.assign(this, { id: info.id, family: info.family, species: info.species, nickname: info.nickname, anims, ground, x, y });
        this.personality = personalityOf(info.nature);
        this.dir = Math.floor(random(0, 8));
        this.state = "idle";
        this.timer = random(0.5, 3);
        this.lookTimer = random(1, 3);
        this.energy = random(0.35, 1);
        this.frame = 0;
        this.frameTime = 0;
        this.target = null;
        this.path = [];
        this.intent = null;
        this.progress = { time: 0, distance: Infinity };
        this.zzz = 0;
        this.vx = 0; // vitesse actuelle : accélère, tourne et freine progressivement
        this.vy = 0;
        this.mood = info.mood ?? "normal"; // « sad » : attaque les autres et boude de temps en temps
        this.nextAttack = random(...SAD.attackEvery);
        this.nextSulk = random(...SAD.sulkEvery);
        this.prey = null; // Pokémon qu'il va attaquer
        this.hit = null; // frappé : { by, delay } (il a mal un instant après le début de l'attaque)
        this.then = null; // après une animation jouée une fois
        this.moves = info.moves ?? null; // « swim » (Eau), « lava » (Feu), « swim-lava », « fly » (Vol) ou null (ParkPokemon.Moves)
        this.wet = 0; // nageur (ou dans la lave) : 0 sur terre, 1 dans l'eau (progressif en y entrant)
        this.liquid = WATER; // où il est (ou était) plongé : WATER ou LAVA (couleur des remous)
        this.altitude = 0; // volant : hauteur au-dessus de l'eau (pixels de la carte, progressive)
    }

    get anim() { return this.anims[ANIMATION_OF[this.state]]; }
    get swims() { return this.moves === "swim" || this.moves === "swim-lava"; }
    get bathes() { return this.moves === "lava" || this.moves === "swim-lava"; }
    /** Cases où il peut passer : l'eau libre pour un nageur, la lave pour un type Feu, les deux pour un volant. */
    get pass() { return this.moves === "fly" ? LAND | LIQUID : this.reach; }
    /** Cases où il peut s'arrêter : un volant se pose toujours sur la terre ferme. */
    get reach() { return LAND | (this.swims ? WATER : 0) | (this.bathes ? LAVA : 0); }
    /** Où il peut dormir : pas dans la lave. */
    get bed() { return LAND | (this.swims ? WATER : 0); }
    /** Où il part se promener : un nageur va surtout dans l'eau (toujours, sans terre), un type Feu de temps en temps dans la lave. */
    destination() {
        if (this.swims && this.ground.water.length && (!this.ground.free.length || Math.random() < SWIM.waterShare)) return WATER;
        if (this.bathes && this.ground.lava.length && Math.random() < LAVA_BATH.share) return LAVA;
        return LAND;
    }

    setState(state) {
        if (state === this.state) return;
        this.state = state;
        this.frame = 0;
        this.frameTime = 0;
    }

    /**
     * Part vers un endroit (le plus proche où il peut s'arrêter ; <reach> : où il peut arriver, pour un survol), en
     * contournant les obstacles.
     */
    walkTo(x, y, intent, reach = this.reach) {
        const target = this.ground.nearestFree(x, y, reach);
        const path = this.ground.path(this, target, this.pass);
        if (!path) { this.rest(random(1, 2)); return; }
        this.target = target;
        this.path = path;
        this.intent = intent;
        this.progress = { time: 0, distance: this.remaining() };
        this.setState("walk");
    }

    /** Distance qu'il reste à parcourir, en suivant le chemin. */
    remaining() {
        let total = 0, from = { x: this.x, y: this.y };
        for (const point of this.path) { total += distance(from, point); from = point; }
        return total;
    }

    rest(seconds) {
        this.vx = this.vy = 0;
        this.target = null;
        this.path = [];
        this.intent = null;
        this.timer = seconds;
        this.setState("idle");
    }

    sleep() {
        this.vx = this.vy = 0;
        this.target = null;
        this.path = [];
        this.intent = null;
        this.setState("sleep");
        this.wakeAt = random(0.85, 1);
    }

    face(other) { this.dir = directionOf(other.x - this.x, other.y - this.y); }

    /** Joue une fois une animation (« attack », « hurt »), sur place, puis <then> (sinon il se repose). */
    play(state, then) {
        Object.assign(this, { vx: 0, vy: 0, target: null, path: [], intent: null, resume: null, then: then ?? null });
        this.setState(state);
        this.frame = 0;
        this.frameTime = 0;
        this.timer = Math.max(0.6, this.anim.durations.reduce((a, b) => a + b, 0) / 1000);
    }

    /** Avance l'animation ; la marche joue au rythme de la vitesse (les pieds ne glissent pas, ne patinent pas). */
    advanceFrame(dtMs, size) {
        const anim = this.anim;
        const rate = this.state === "walk" ? clamp(Math.hypot(this.vx, this.vy) / (WALK_ANIMATION_SPEED * size), 0.35, 1.4) : 1;
        this.frameTime += dtMs * rate;
        while (this.frameTime >= anim.durations[this.frame % anim.durations.length]) {
            if (ONCE.has(this.state) && this.frame >= anim.durations.length - 1) { this.frameTime = 0; break; } // reste sur la dernière image
            this.frameTime -= anim.durations[this.frame % anim.durations.length];
            this.frame = (this.frame + 1) % anim.durations.length;
        }
    }
}

/**
 * Parc dans <container> ; <component> (Park.razor) est prévenu d'un clic sur un Pokémon. Options : canMove (le
 * propriétaire peut soulever ses Pokémon et les poser ailleurs ; rien n'est enregistré), map (début du nom des cartes de
 * son paysage : « parc-1 », « plage-1 »), size (taille du parc, de 1 à 4 : sa carte), fullscreen (élément mis en plein
 * écran, qui contient <container> et les fenêtres de la page, comme la fiche d'un Pokémon ; sans lui, <container>).
 */
export async function create(container, component, options) {
    const canMove = options?.canMove === true;
    const fullscreenTarget = options?.fullscreen ?? container; // plein écran : lui, pour que la fiche d'un Pokémon s'y affiche aussi
    const size = Math.max(1, Math.floor(Number(options?.size) || 1));
    const mapName = /^[a-z0-9-]+$/.test(options?.map ?? "") ? options.map : "parc-1";
    let data = null;
    try { data = await (await fetch(MAP_URL(mapName, size))).json(); }
    catch { /* carte illisible : un pré sans obstacle */ }
    const ground = new Ground(data);
    const viewW = ground.start.w * TILE, viewH = ground.start.h * TILE; // la zone « Départ » remplit la hauteur

    let pokemon = [];
    let mouse = null; // position du pointeur dans le parc, null hors du parc
    let hovered = null;
    let drag = null; // glissement de la vue en cours
    let k = 1; // pixels d'écran par pixel de la carte (zoom de la caméra)
    let spriteK = 2; // pixels d'écran par pixel de sprite
    let spriteScale = SPRITE_SIZE; // pixels de la carte par pixel de sprite
    let scene = null, camera = null, night = null, label = null, labelText = null, marker = null;
    let held = null; // Pokémon soulevé : { p, from (sa place), dx, dy (écart au pointeur), pointer }
    let habitatLayer = null; // types de cases affichés (mode debug), dessinés une fois dans une image
    let clock = 0; // secondes écoulées (remous)
    const animated = []; // tuiles animées de la carte : { image, animation, offset, index } (animatedTiles)
    let animationClock = 0; // millisecondes écoulées (tuiles animées)

    // ---------- Taille : toute la largeur du conteneur ; en hauteur, un nombre entier de fois la zone « Départ » en
    // pixels d'écran (chaque pixel de la carte et des sprites couvre autant de pixels d'écran : dessin net), le plus grand
    // qui tient dans la hauteur visible sous le parc (sur un téléphone : pas plus que la zone entière en largeur). En plein
    // écran : tout l'écran, et le plus grand zoom où la zone « Départ » tient en hauteur ----------
    const isFullscreen = () => document.fullscreenElement === fullscreenTarget;
    const measure = () => {
        const ratio = window.devicePixelRatio || 1;
        if (isFullscreen()) {
            const height = Math.round(container.clientHeight * ratio);
            return { ratio, zoom: Math.max(1, Math.floor(height / viewH)), width: Math.round(container.clientWidth * ratio), height };
        }
        const width = container.clientWidth || viewW;
        const top = container.getBoundingClientRect().top + window.scrollY;
        const visible = Math.max(240, window.innerHeight - top - 24) * ratio;
        const fitWidth = Math.round(width * ratio / viewW); // zone à peu près entière en largeur
        const zoom = Math.max(1, Math.round(240 * ratio / viewH), Math.min(Math.floor(visible / viewH), fitWidth));
        return { ratio, zoom, width: Math.round(width * ratio), height: zoom * viewH };
    };
    let maxZoom = 1; // zoom par défaut, et le plus grand permis (on dézoome de là jusqu'à ×1)
    const applyZoom = zoom => {
        k = zoom;
        spriteK = Math.max(Math.min(k, 2), Math.round(k * SPRITE_SIZE));
        spriteScale = spriteK / k;
        if (!scene) return;
        camera.setZoom(k);
        // Limites de la vue : la zone « Parc » et un peu de forêt autour (dans la carte), ou plus large et centrée quand la
        // vue l'est (la forêt continue jusqu'au bord de la carte)
        const vw = camera.width / k, vh = camera.height / k;
        const bx = Math.max(0, (ground.area.x - VIEW_MARGIN) * TILE), by = Math.max(0, (ground.area.y - VIEW_MARGIN) * TILE);
        const bw = Math.min(ground.width, (ground.area.x + ground.area.w + VIEW_MARGIN) * TILE) - bx;
        const bh = Math.min(ground.height, (ground.area.y + ground.area.h + VIEW_MARGIN) * TILE) - by;
        const cw = Math.max(bw, vw), ch = Math.max(bh, vh);
        camera.setBounds(bx + (bw - cw) / 2, by + (bh - ch) / 2, cw, ch);
        for (const p of pokemon) for (const text of p.texts) text.setResolution(k);
        labelText.setResolution(k);
        zoomOutButton.disabled = k <= 1;
        zoomInButton.disabled = k >= maxZoom;
    };
    /** Centre de la vue, en pixels de la carte (camera.midPoint n'est mis à jour qu'au dessin). */
    const viewCenter = () => ({ x: camera.scrollX + camera.width / 2, y: camera.scrollY + camera.height / 2 });
    /** Change le zoom (un nombre entier, de ×1 au zoom par défaut) en gardant fixe le point (en pixels du canvas) visé. */
    function zoomTo(zoom, sx = camera.width / 2, sy = camera.height / 2) {
        zoom = clamp(zoom, 1, maxZoom);
        if (zoom === k) return;
        const center = viewCenter();
        const wx = center.x + (sx - camera.width / 2) / k, wy = center.y + (sy - camera.height / 2) / k;
        applyZoom(zoom);
        camera.centerOn(wx - (sx - camera.width / 2) / k, wy - (sy - camera.height / 2) / k);
    }
    const resize = () => {
        if (!scene) return;
        const size = measure();
        const center = viewCenter();
        game.scale.setZoom(1 / size.ratio); // canvas en pixels physiques, affiché en pixels CSS
        game.scale.resize(size.width, size.height);
        if (size.zoom !== maxZoom) { maxZoom = size.zoom; applyZoom(maxZoom); } // autre taille (plein écran...) : zoom par défaut
        else applyZoom(clamp(k, 1, maxZoom));
        camera.centerOn(center.x, center.y);
    };
    const initial = measure();
    maxZoom = initial.zoom;
    applyZoom(initial.zoom);

    // ---------- Boutons sur la carte : dézoomer, zoomer, plein écran ----------
    const controls = document.createElement("div");
    controls.className = "park-controls";
    const button = (title, icon, action) => {
        const b = document.createElement("button");
        b.type = "button";
        b.title = title;
        b.setAttribute("aria-label", title);
        b.innerHTML = icon;
        b.addEventListener("click", action);
        controls.appendChild(b);
        return b;
    };
    const zoomOutButton = button("Dézoomer", ICONS.minus, () => zoomTo(k - 1));
    const zoomInButton = button("Zoomer", ICONS.plus, () => zoomTo(k + 1));
    const fullscreenButton = button("Plein écran", ICONS.expand, () => {
        if (isFullscreen()) document.exitFullscreen().catch(() => { });
        else fullscreenTarget.requestFullscreen().catch(() => { });
    });
    fullscreenButton.hidden = !document.fullscreenEnabled; // iPhone : pas de plein écran pour une page
    container.appendChild(controls);
    const onFullscreen = () => {
        const full = isFullscreen();
        fullscreenButton.innerHTML = full ? ICONS.shrink : ICONS.expand;
        fullscreenButton.title = full ? "Quitter le plein écran (Échap)" : "Plein écran";
        fullscreenButton.setAttribute("aria-label", fullscreenButton.title);
        resize();
    };
    document.addEventListener("fullscreenchange", onFullscreen);
    // Molette sur le parc : zoom autour du pointeur, en plein écran ou non (la page ne défile pas tant que la souris est
    // sur le parc)
    let wheel = 0;
    const onWheel = e => {
        if (!scene) return;
        e.preventDefault();
        wheel += e.deltaMode === 0 ? e.deltaY : Math.sign(e.deltaY) * 100; // pavé tactile : petits pas cumulés
        if (Math.abs(wheel) < 100) return;
        const r = game.canvas.getBoundingClientRect();
        zoomTo(k - Math.sign(wheel), (e.clientX - r.left) * game.canvas.width / r.width, (e.clientY - r.top) * game.canvas.height / r.height);
        wheel = 0;
    };

    let ready;
    const started = new Promise(resolve => ready = resolve);
    const game = new Phaser.Game({
        type: Phaser.AUTO,
        parent: container,
        width: initial.width,
        height: initial.height,
        backgroundColor: OUTSIDE,
        pixelArt: true,
        roundPixels: true,
        banner: false,
        audio: { noAudio: true },
        scale: { mode: Phaser.Scale.NONE, zoom: 1 / initial.ratio },
        input: { mouse: { preventDefaultWheel: false }, activePointers: 2 }, // molette : voir onWheel ; deux doigts : pincement
        scene: {
            preload() {
                for (const tileset of data?.tilesets ?? []) this.load.image("tuiles:" + tileset.image, TILES_URL + tileset.image);
                this.load.on("loaderror", () => { }); // planches absentes (images pas récupérées) : de l'herbe unie
            },
            create() { build(this); ready(); },
            update(_, delta) { tick(delta); },
        },
    });

    // ---------- Décor : carte, brouillard hors de la zone « Parc », nuit ----------
    function build(s) {
        scene = s;
        camera = s.cameras.main;
        game.canvas.classList.add("park-canvas");
        game.canvas.addEventListener("wheel", onWheel, { passive: false });
        // Sous la carte : sa couleur de fond (la roche d'une grotte, là où le sol est transparent), ou de l'herbe (planches absentes)
        const background = data?.backgroundcolor ? parseInt(data.backgroundcolor.slice(-6), 16) : GRASS;
        s.add.rectangle(0, 0, ground.width, ground.height, background).setOrigin(0).setDepth(-1);
        // La carte ne bouge pas : ses calques sont dessinés une seule fois, dans une image sous les Pokémon (sol, décors), des
        // morceaux de feuillages triés avec eux (foliage) et une image devant tout (brouillard). Des calques de tuiles
        // redessinés à chaque image créaient des dizaines de Ko de déchets par image : petites saccades au passage du
        // ramasse-miettes.
        const below = s.add.renderTexture(0, 0, ground.width, ground.height).setOrigin(0).setDepth(0);
        const above = s.add.renderTexture(0, 0, ground.width, ground.height).setOrigin(0).setDepth(3);
        if (data) {
            s.cache.tilemap.add("parc", { format: Phaser.Tilemaps.Formats.TILED_JSON, data });
            const map = s.make.tilemap({ key: "parc" });
            const tilesets = data.tilesets
                .filter(t => s.textures.exists("tuiles:" + t.image))
                .map(t => map.addTilesetImage(t.name, "tuiles:" + t.image))
                .filter(Boolean);
            for (const name of ["Sol", "Décors", "Devant"]) {
                if (!tilesets.length || map.getLayerIndex(name) === null) continue;
                const layer = map.createLayer(name, tilesets, 0, 0);
                if (name === "Devant") foliage(s, layer);
                else below.draw(layer);
                layer.destroy();
            }
            animatedTiles(s);
        }
        const fog = s.add.graphics().fillStyle(FOG.color, FOG.alpha);
        const fill = (x, y, w, h) => { if (w > 0 && h > 0) fog.fillRect(x, y, w, h); }; // rien hors de la carte (taille 4 : toute la carte)
        for (let step = 1; step <= FOG.steps; step++) { // marches superposées : plus sombre à chaque case
            const { x, y, w, h } = ground.area;
            const [zx, zy, zw, zh] = [(x - step) * TILE, (y - step) * TILE, (w + 2 * step) * TILE, (h + 2 * step) * TILE];
            fill(0, 0, ground.width, zy);
            fill(0, zy + zh, ground.width, ground.height - zy - zh);
            fill(0, zy, zx, zh);
            fill(zx + zw, zy, ground.width - zx - zw, zh);
        }
        above.draw(fog);
        fog.destroy();
        shade(s);
        night =s.add.rectangle(0, 0, ground.width, ground.height, NIGHT.color).setOrigin(0).setDepth(4).setVisible(false);
        label = s.add.graphics().setDepth(6).setVisible(false);
        labelText = s.add.text(0, 0, "", { fontFamily: "sans-serif", fontStyle: "bold", fontSize: "8px", color: "#ffffff", resolution: k })
            .setDepth(6.1).setVisible(false);
        marker = s.add.graphics().setDepth(HELD_DEPTH - 0.1).setVisible(false); // case visée par un Pokémon soulevé

        camera.roundPixels = true;
        applyZoom(k); // zoom et limites de la vue
        const start = ground.start;
        camera.centerOn((start.x + start.w / 2) * TILE, (start.y + start.h / 2) * TILE); // vue de départ : la zone « Départ »
        listen(s);
    }

    /**
     * Pénombre d'une grotte : si la carte a un calque d'objets « Lumières » (un point par lumière, propriétés « rayon » en
     * pixels de la carte et « couleur »), un voile sombre dessiné une fois dans une image, où chaque lumière efface un
     * dégradé rond, et une image de ce dégradé, teinte de sa couleur, ajoutée par-dessus (rien de recalculé ensuite).
     */
    function shade(s) {
        const lights = data?.layers.find(l => l.name === "Lumières" && l.type === "objectgroup")?.objects;
        if (!lights) return;
        if (!s.textures.exists("lumiere")) { // dégradé rond, opaque au centre (une texture par jeu de Phaser)
            const size = 128, texture = s.textures.createCanvas("lumiere", size, size), context = texture.getContext();
            const gradient = context.createRadialGradient(size / 2, size / 2, 0, size / 2, size / 2, size / 2);
            gradient.addColorStop(0, "rgba(255, 255, 255, 1)");
            gradient.addColorStop(0.45, "rgba(255, 255, 255, 0.6)");
            gradient.addColorStop(1, "rgba(255, 255, 255, 0)");
            context.fillStyle = gradient;
            context.fillRect(0, 0, size, size);
            texture.refresh();
        }
        const veil = s.add.renderTexture(0, 0, ground.width, ground.height).setOrigin(0).setDepth(SHADE.depth);
        veil.fill(SHADE.color, SHADE.alpha);
        const stamp = s.make.image({ key: "lumiere", add: false });
        for (const light of lights) {
            const property = name => light.properties?.find(p => p.name === name)?.value;
            const radius = property("rayon") ?? 64, color = parseInt(String(property("couleur") ?? "#ffffff").slice(-6), 16);
            stamp.setPosition(light.x, light.y).setDisplaySize(2 * radius, 2 * radius).setAlpha(SHADE.clear);
            veil.erase(stamp);
            s.add.image(light.x, light.y, "lumiere").setDisplaySize(2 * radius, 2 * radius).setTint(color)
                .setAlpha(SHADE.glow).setBlendMode(Phaser.BlendModes.ADD).setDepth(SHADE.depth + 0.01);
        }
        stamp.destroy();
    }

    /**
     * Tuiles animées de la carte (animations de Tiled : cascades de lave, bulles) : une image par tuile, sur la carte déjà
     * dessinée, dont on ne change l'image qu'à son tour (animate : rien de créé à chaque image). Les bulles (calque
     * « Bulles », jamais dessiné dans la carte) partent chacune à un moment différent : elles n'éclatent pas toutes ensemble.
     */
    function animatedTiles(s) {
        const animations = new Map(); // numéro de tuile de la carte → { key, frames, ends (fin de chaque image, ms), total }
        for (const set of data.tilesets) {
            if (!set.tiles?.some(t => t.animation?.length) || !s.textures.exists("tuiles:" + set.image)) continue;
            const texture = s.textures.get("tuiles:" + set.image);
            const frame = id => {
                const name = "tuile" + id;
                if (!texture.has(name)) texture.add(name, 0, (id % set.columns) * TILE, Math.floor(id / set.columns) * TILE, TILE, TILE);
                return texture.get(name);
            };
            for (const tile of set.tiles) {
                if (!tile.animation?.length) continue;
                let total = 0;
                const ends = tile.animation.map(f => total += f.duration);
                animations.set(set.firstgid + tile.id, { key: "tuiles:" + set.image, frames: tile.animation.map(f => frame(f.tileid)), ends, total });
            }
        }
        if (!animations.size) return;
        for (const [name, apart] of [["Sol", false], ["Décors", false], ["Bulles", true]]) {
            const tiles = data.layers.find(l => l.name === name && l.type === "tilelayer")?.data ?? [];
            tiles.forEach((gid, i) => {
                const animation = animations.get(gid & 0x1fffffff); // sans les bits de retournement de Tiled
                if (!animation) return;
                const image = s.add.image((i % ground.columns) * TILE, Math.floor(i / ground.columns) * TILE, animation.key, animation.frames[0])
                    .setOrigin(0).setDepth(ANIMATED_DEPTH);
                animated.push({ image, animation, offset: apart ? Math.random() * animation.total : 0, index: 0 });
            });
        }
    }

    /** Fait avancer les tuiles animées de <dt> secondes : une image change seulement quand son tour vient. */
    function animate(dt) {
        animationClock = (animationClock + dt * 1000) % 3600000;
        for (const a of animated) {
            const { ends, total, frames } = a.animation;
            const t = (animationClock + a.offset) % total;
            let index = 0;
            while (ends[index] <= t) index++;
            if (index !== a.index) {
                a.index = index;
                a.image.setFrame(frames[index]);
            }
        }
    }

    /**
     * Feuillages (calque « Devant »), triés avec les Pokémon : chaque tuile appartient à l'arbre dont le tronc est la
     * première case sous elle qui bloque ou n'a plus de feuillage. Un Pokémon aux pieds plus hauts que le milieu de ce
     * tronc passe derrière l'arbre, plus bas devant. Les feuillages d'une même ligne de troncs, proches les uns des autres,
     * forment un morceau ; tous sont dessinés une seule fois, dans une même texture (une image par morceau).
     */
    function foliage(s, layer) {
        const leaves = layer.layer.data; // tuiles, [ligne][colonne]
        const pieces = foliagePieces(trunkRows(leaves));
        if (!pieces.length) return;
        const { width, height } = packPieces(pieces);
        const texture = s.textures.addDynamicTexture("feuillages", width, height);
        layer.setSkipCull(true); // dessiné ailleurs qu'à sa place : ne pas écarter les tuiles hors de la vue
        layer.forEachTile(t => { t.visible = false; });
        pieces.forEach((p, i) => {
            for (const c of p.cells) leaves[c.y][c.x].visible = true;
            texture.draw(layer, p.ax - p.x0 * TILE, p.ay - p.y0 * TILE);
            for (const c of p.cells) leaves[c.y][c.x].visible = false;
            texture.add("f" + i, 0, p.ax, p.ay, p.w, p.h);
            s.add.image(p.x0 * TILE, p.y0 * TILE, "feuillages", "f" + i).setOrigin(0)
                .setDepth(depthOf(p.trunk * TILE + TILE / 2 + 0.5)); // + 0,5 : jamais à égalité avec un Pokémon
        });
    }

    /** Cases de feuillage, par ligne du tronc de leur arbre. */
    function trunkRows(leaves) {
        const { columns, lines } = ground;
        const blocked = data.layers.find(l => l.name === "Collisions" && l.type === "tilelayer")?.data ?? [];
        const has = (x, y) => leaves[y][x].index !== -1;
        const rows = new Map(); // ligne du tronc → cases de feuillage
        for (let y = 0; y < lines; y++) {
            for (let x = 0; x < columns; x++) {
                if (!has(x, y)) continue;
                let trunk = y;
                while (trunk < lines && has(x, trunk) && !blocked[trunk * columns + x]) trunk++;
                if (!rows.has(trunk)) rows.set(trunk, []);
                rows.get(trunk).push({ x, y });
            }
        }
        return rows;
    }

    /** Morceaux de feuillage : les cases d'une même ligne de troncs, proches les unes des autres. */
    function foliagePieces(rows) {
        const pieces = [];
        for (const [trunk, cells] of rows) {
            let piece = null;
            for (const c of cells.sort((a, b) => a.x - b.x)) {
                if (!piece || c.x - piece.x1 > 3) {
                    piece = { trunk, cells: [], x0: c.x, x1: c.x, y0: c.y, y1: c.y };
                    pieces.push(piece);
                }
                piece.cells.push(c);
                piece.x1 = c.x;
                piece.y0 = Math.min(piece.y0, c.y);
                piece.y1 = Math.max(piece.y1, c.y);
            }
        }
        return pieces;
    }

    /**
     * Rangement des morceaux dans la texture (place de chacun : ax, ay) : par étagères, les plus hauts d'abord, un peu
     * d'écart (pas de bavure d'un morceau à l'autre). Rend la taille de la texture.
     */
    function packPieces(pieces) {
        const GAP = 2;
        for (const p of pieces) [p.w, p.h] = [(p.x1 - p.x0 + 1) * TILE, (p.y1 - p.y0 + 1) * TILE];
        const width = Math.max(1024, ...pieces.map(p => p.w));
        let px = 0, py = 0, shelf = 0;
        for (const p of [...pieces].sort((a, b) => b.h - a.h)) {
            if (px + p.w > width) [px, py, shelf] = [0, py + shelf + GAP, 0];
            [p.ax, p.ay] = [px, py];
            px += p.w + GAP;
            shelf = Math.max(shelf, p.h);
        }
        return { width, height: py + shelf };
    }

    // ---------- Souris : glisser pour explorer, curiosité, nom au survol, clic ----------
    /** Pokémon sous le pointeur (le plus en avant d'abord). */
    const at = point => [...pokemon].sort((a, b) => b.y - a.y).find(p => {
        const a = p.anim, w = a.fw * spriteScale / 2, h = a.fh * spriteScale, y = p.y - p.altitude; // un volant : plus haut
        return Math.abs(point.x - p.x) < w * 0.7 && point.y < y + 4 && point.y > y - h * 0.75;
    });
    const cursor = () => {
        let style = "grab";
        if (held || drag?.moved) style = "grabbing";
        else if (hovered) style = "pointer";
        game.canvas.style.cursor = style;
    };

    // ---------- Pokémon soulevé (propriétaire) : suit le pointeur, posé seulement sur une case où il peut marcher ----------
    const spot = new Phaser.Math.Vector2(); // gardé : la position suit le pointeur à chaque image, sans déchets

    function pickUp(p, pointer) {
        camera.getWorldPoint(pointer.x, pointer.y, spot);
        held = { p, from: { x: p.x, y: p.y }, dx: p.x - spot.x, dy: p.y - spot.y, pointer };
        Object.assign(p, { vx: 0, vy: 0, target: null, path: [], intent: null, resume: null, dir: FRONT, prey: null, hit: null, then: null });
        p.setState("held"); // un dormeur se réveille (une attaque prévue n'a pas lieu)
        drag = null;
        hovered = null;
        cursor();
    }

    /** Sous le pointeur, même quand la vue défile ou change de zoom. */
    function carry() {
        camera.getWorldPoint(held.pointer.x, held.pointer.y, spot);
        held.p.x = clamp(spot.x + held.dx, 0, ground.width - 1);
        held.p.y = clamp(spot.y + held.dy, 0, ground.height - 1);
    }

    /**
     * Posé là où il est s'il peut s'y arrêter (dans l'eau : un nageur seulement), sinon (ou <back>) remis à sa place ; il
     * reste un moment immobile.
     */
    function putDown(back = false) {
        const { p, from } = held;
        held = null;
        if (back || !ground.isFree(p.x, p.y, p.reach)) Object.assign(p, from);
        p.dir = FRONT;
        p.rest(random(2, 4));
        cursor();
    }

    /** Près du bord de la vue, la vue défile vers lui (de plus en plus vite en s'en approchant). */
    function edgeScroll(dt) {
        const ratio = window.devicePixelRatio || 1, margin = EDGE_SCROLL.margin * ratio;
        const push = (v, size) => {
            if (v < margin) return -(1 - v / margin);
            if (v > size - margin) return (v - size + margin) / margin;
            return 0;
        };
        const vx = clamp(push(held.pointer.x, camera.width), -1, 1), vy = clamp(push(held.pointer.y, camera.height), -1, 1);
        if (!vx && !vy) return;
        const speed = EDGE_SCROLL.speed * ratio / k * dt; // pixels de la carte
        camera.setScroll(camera.scrollX + vx * speed, camera.scrollY + vy * speed);
    }

    /** Cadre de la case visée : clair si on peut l'y poser, rouge sinon (redessiné seulement quand ça change). */
    function drawMarker() {
        marker.setVisible(!!held);
        if (!held) return;
        const p = held.p, ok = ground.isFree(p.x, p.y, p.reach);
        if (marker.ok !== ok) {
            marker.ok = ok;
            const color = ok ? 0xffffff : 0xff4d4d;
            marker.clear().fillStyle(color, 0.18).fillRect(0, 0, TILE, TILE)
                .lineStyle(1, color, 0.9).strokeRect(0.5, 0.5, TILE - 1, TILE - 1);
        }
        marker.setPosition(Math.floor(p.x / TILE) * TILE, Math.floor(p.y / TILE) * TILE);
    }

    function listen(s) {
        const worldOf = pointer => camera.getWorldPoint(pointer.x, pointer.y);
        // Pincement à deux doigts : un cran de zoom chaque fois que l'écart change d'un tiers environ
        const fingers = () => [s.input.pointer1, s.input.pointer2];
        const pinching = () => fingers().every(f => f.isDown);
        const spread = () => { const [a, b] = fingers(); return Math.hypot(a.x - b.x, a.y - b.y); };
        let pinch = null;
        s.input.on("pointerdown", pointer => {
            if (pinching()) { // deux doigts : pincement (un Pokémon porté retourne à sa place)
                if (held) putDown(true);
                drag = null;
                pinch = { spread: spread() };
                return;
            }
            if (held) return;
            // Sur un Pokémon (propriétaire) : à la souris, il est soulevé dès qu'on bouge ; au doigt, après un appui long
            // (tick), un glissement avant fait défiler la vue
            const target = canMove ? at(worldOf(pointer)) ?? null : null;
            drag = { x: pointer.x, y: pointer.y, scrollX: camera.scrollX, scrollY: camera.scrollY, moved: false,
                target, touch: pointer.wasTouch, time: performance.now(), pointer };
        });
        const pinchMove = () => {
            if (!pinching()) return;
            const [a, b] = fingers(), now = spread();
            if (now > pinch.spread * 1.35 || now < pinch.spread / 1.35) {
                zoomTo(k + (now > pinch.spread ? 1 : -1), (a.x + b.x) / 2, (a.y + b.y) / 2);
                pinch.spread = now;
            }
        };
        // Bouton appuyé : au-delà du seuil, le Pokémon visé est soulevé ou la vue défile
        const dragMove = pointer => {
            const dx = pointer.x - drag.x, dy = pointer.y - drag.y;
            if (!drag.moved && Math.hypot(dx, dy) > DRAG_THRESHOLD * (window.devicePixelRatio || 1)) {
                if (drag.target && !drag.touch) pickUp(drag.target, pointer);
                else drag.moved = true;
            }
            if (drag?.moved) camera.setScroll(drag.scrollX - dx / k, drag.scrollY - dy / k);
        };
        s.input.on("pointermove", pointer => {
            if (pinch) {
                pinchMove();
                return;
            }
            if (drag && pointer.isDown) dragMove(pointer);
            if (held) carry();
            const point = worldOf(pointer);
            mouse = { x: point.x, y: point.y };
            hovered = held || drag?.moved ? null : at(mouse);
            cursor();
        });
        const release = pointer => {
            if (pinch) { // un doigt levé : fin du pincement (sans clic ni glissement)
                if (!pinching()) pinch = null;
                drag = null;
                mouse = hovered = null;
                return;
            }
            if (held) {
                carry();
                putDown();
                if (pointer.wasTouch) mouse = hovered = null;
                return;
            }
            const click = drag && !drag.moved;
            drag = null;
            if (click) {
                const p = at(worldOf(pointer));
                if (p) {
                    p.dir = FRONT; // se tourne vers toi
                    p.rest(4);
                    component.invokeMethodAsync("OnPokemonClicked", p.id);
                }
            }
            if (pointer.wasTouch) mouse = hovered = null; // doigt levé : plus de pointeur à regarder
            cursor();
        };
        s.input.on("pointerup", release);
        s.input.on("pointerupoutside", release); // relâché hors du parc après un glissement
        s.input.on("gameout", () => { mouse = hovered = null; drag = null; cursor(); });
    }

    // ---------- Comportement ----------
    const friendsOf = p => pokemon.filter(o => o !== p && o.family === p.family);

    // ---------- Attaques d'un Pokémon malheureux ----------
    /**
     * Pokémon que <p> peut attaquer, le plus proche : éveillé ou endormi, ni porté, ni déjà frappé ou en fuite, là où <p>
     * peut s'arrêter (pas dans l'eau, sauf pour un nageur, ni dans la lave, sauf pour un type Feu) ; sauf <any> (mode debug), pas de sa famille et à portée.
     */
    const preyOf = (p, any) => pokemon
        .filter(o => o !== p && o.state !== "held" && o.state !== "hurt" && o.intent !== "flee" && !o.hit
            && ground.isFree(o.x, o.y, p.reach) && (any || (o.family !== p.family && distance(p, o) < SAD.reach)))
        .sort((a, b) => distance(p, a) - distance(p, b))[0];

    /** Part attaquer <prey> (il marche jusqu'à lui). */
    function charge(p, prey) {
        Object.assign(p, { prey, tries: 0, then: null, hit: null });
        approach(p);
    }

    /** Marche jusqu'à côté de sa proie, du côté où il arrive. */
    function approach(p) {
        const angle = Math.atan2(p.y - p.prey.y, p.x - p.prey.x);
        p.walkTo(p.prey.x + Math.cos(angle) * 18, p.prey.y + Math.sin(angle) * 12, "attack");
    }

    /** Arrivé : il frappe si sa proie est assez près, sinon la suit encore un peu (elle a pu bouger), puis abandonne. */
    function strike(p) {
        const prey = p.prey;
        if (!prey || !pokemon.includes(prey) || prey.state === "held" || distance(p, prey) > SAD.reach * 1.5) {
            p.prey = null;
            p.rest(random(1, 3));
            return;
        }
        if (distance(p, prey) > STRIKE_RANGE) {
            if (++p.tries > 3) { p.prey = null; p.rest(random(1, 3)); }
            else approach(p);
            return;
        }
        p.prey = null;
        p.face(prey);
        p.play("attack");
        prey.hit = { by: p, delay: p.timer * 0.5 }; // il a mal au milieu de l'attaque
    }

    /** Frappé par <by> : réveillé s'il dormait, il a mal (Hurt), puis s'enfuit loin de lui. */
    function hurt(p, by) {
        p.face(by);
        p.play("hurt", () => flee(p, by));
    }

    /** S'enfuit à l'opposé de <from> (à peu près), vers l'endroit libre le plus éloigné de lui parmi quelques essais. */
    function flee(p, from) {
        const angle = Math.atan2(p.y - from.y, p.x - from.x);
        let best = null;
        for (let tries = 0; tries < 8; tries++) {
            const a = angle + random(-0.7, 0.7), d = random(...FLEE.distance);
            const spot = ground.nearestFree(p.x + Math.cos(a) * d, p.y + Math.sin(a) * d, p.reach);
            if (!best || distance(spot, from) > distance(best, from)) best = spot;
        }
        p.walkTo(best.x, best.y, "flee");
    }

    /** Endroit au hasard sur la terre, dans l'eau ou la lave libre (randomSpot), ni trop près ni trop loin de <p>. */
    function spotNear(p, where) {
        let spot = ground.randomSpot(where);
        for (let tries = 0; tries < 12 && !(distance(p, spot) > 40 && distance(p, spot) < 160); tries++) spot = ground.randomSpot(where);
        return spot;
    }

    /** Nouvelle intention quand le Pokémon a fini ce qu'il faisait. */
    function decide(p, night) {
        const { activity, sleepy, sociable, curious } = p.personality;
        const friends = friendsOf(p);
        if (!ground.isFree(p.x, p.y, p.reach)) {
            // Volant arrêté au-dessus de l'eau ou de la lave (clic, chemin barré) : il va d'abord se poser sur la terre ferme.
            const land = ground.nearestFree(p.x, p.y, p.reach);
            p.walkTo(land.x, land.y, "explore");
            return;
        }
        if (p.energy < 0.22 + night * 0.4 + sleepy) {
            // Fatigué : il va dormir près d'un congénère qui dort, sinon sur place ; dans la lave, il en sort d'abord.
            const sleeper = friends.filter(f => f.state === "sleep").sort((a, b) => distance(p, a) - distance(p, b))[0];
            if (sleeper && Math.random() < sociable && distance(p, sleeper) > 20) {
                const side = p.x < sleeper.x ? -1 : 1;
                p.walkTo(sleeper.x + side * random(16, 23), sleeper.y + random(-5, 5), "sleep", p.bed);
            } else if (!ground.isFree(p.x, p.y, p.bed)) {
                const bed = ground.nearestFree(p.x, p.y, p.bed);
                p.walkTo(bed.x, bed.y, "sleep", p.bed);
            } else {
                p.sleep();
            }
            return;
        }
        if (p.mood === "sad" && p.nextAttack <= 0) {
            // Malheureux : de temps en temps, il s'en prend à un Pokémon proche (pas de sa famille), même endormi.
            const prey = preyOf(p, false);
            if (prey) {
                p.nextAttack = random(...SAD.attackEvery);
                charge(p, prey);
                return;
            }
            p.nextAttack = random(...SAD.retry);
        }
        if (mouse && distance(p, mouse) < 133 && Math.random() < 0.6 * curious) {
            // Curieux : il vient voir le pointeur de près, puis le regarde.
            if (distance(p, mouse) > 30) {
                const angle = Math.atan2(p.y - mouse.y, p.x - mouse.x);
                p.walkTo(mouse.x + Math.cos(angle) * 21, mouse.y + Math.sin(angle) * 16, "curious");
            } else {
                p.face(mouse);
                p.rest(random(2, 5));
            }
            return;
        }
        const roll = Math.random();
        const nearest = friends.sort((a, b) => distance(p, a) - distance(p, b))[0];
        if (nearest && distance(p, nearest) < 47) {
            // Avec un congénère : il reste un moment avec lui (se repose face à lui, ou flâne autour).
            if (roll < 0.45 + 0.35 * sociable) {
                if (Math.random() < 0.55) {
                    p.face(nearest);
                    p.rest(random(3, 8) / activity);
                } else {
                    const angle = random(0, Math.PI * 2);
                    p.walkTo(nearest.x + Math.cos(angle) * random(17, 33), nearest.y + Math.sin(angle) * random(11, 23), "stroll");
                }
                return;
            }
        } else if (friends.length && roll < 0.1 + 0.45 * sociable) {
            // Rendre visite à un Pokémon de sa famille, de préférence proche.
            const friend = friends[Math.floor(Math.random() ** 2 * friends.length)];
            const angle = random(0, Math.PI * 2);
            p.walkTo(friend.x + Math.cos(angle) * 20, friend.y + Math.sin(angle) * 13, "visit");
            return;
        }
        if (p.moves === "fly" && ground.liquid.length && Math.random() < 0.55 * FLY_OVER) {
            // Volant : un tour au-dessus de l'eau ou de la lave (il n'y reste pas : arrivé, il repart vers la terre, update).
            const spot = spotNear(p, LIQUID);
            p.walkTo(spot.x, spot.y, "soar", LIQUID);
        } else if (Math.random() < 0.55) {
            // Explorer un endroit du parc, ni trop près ni trop loin (un nageur : surtout dans l'eau ; un type Feu : parfois
            // dans la lave).
            const spot = spotNear(p, p.destination());
            p.walkTo(spot.x, spot.y, "explore");
        } else {
            // Rester là à regarder autour de soi.
            p.rest(random(2, 6) / activity);
        }
    }

    function update(p, dt, night) {
        const { activity, curious } = p.personality;
        if (p.state === "held") return; // porté : suit le pointeur
        if (p.hit && (p.hit.delay -= dt) <= 0) { // frappé (même endormi) : il a mal, puis s'enfuit
            const by = p.hit.by;
            p.hit = null;
            hurt(p, by);
            return;
        }
        if (ONCE.has(p.state)) { // animation jouée une fois (attaque, douleur), puis la suite
            p.timer -= dt;
            if (p.timer <= 0) {
                const then = p.then;
                p.then = null;
                if (then) then();
                else p.rest(random(1, 3));
            }
            return;
        }
        if (p.mood === "sad" && p.state !== "sleep") { p.nextAttack -= dt; p.nextSulk -= dt; }
        if (p.state === "sleep") {
            p.energy = Math.min(1, p.energy + dt * (night > 0.5 ? ENERGY.sleepNight : ENERGY.sleepDay));
            p.zzz += dt;
            if (p.energy >= p.wakeAt && (night < 0.5 || chance(0.002, dt))) { // la nuit, rares réveils
                p.dir = FRONT;
                p.rest(random(1.5, 3)); // se réveille, regarde devant lui un moment
            }
            return;
        }

        // Curiosité : un Pokémon éveillé près du pointeur se tourne vers lui, et parfois s'en approche.
        const nearMouse = mouse && distance(p, mouse) < 93;
        if (nearMouse && p.state === "idle") {
            p.face(mouse);
            if (chance(0.12 * curious, dt) && distance(p, mouse) > 27)
                p.walkTo(mouse.x + random(-13, 13), mouse.y + random(10, 20), "curious");
        }

        if (p.state === "idle") {
            p.energy = Math.max(0, p.energy - dt * ENERGY.idle);
            p.timer -= dt;
            p.lookTimer -= dt;
            if (p.lookTimer <= 0 && !nearMouse) {
                // Il regarde autour de lui : un quart de tour, ou de nouveau devant lui.
                p.dir = Math.random() < 0.25 ? FRONT : (p.dir + (Math.random() < 0.5 ? 1 : 7)) % 8;
                p.lookTimer = random(1.2, 3.5);
            }
            if (p.mood === "sad" && p.nextSulk <= 0) { // malheureux : il boude un instant
                p.nextSulk = random(...SAD.sulkEvery);
                p.play("hurt");
                return;
            }
            if (p.timer <= 0) decide(p, night);
            return;
        }

        // Marche vers le prochain point de passage, en s'écartant des autres.
        p.energy = Math.max(0, p.energy - dt * ENERGY.walk * activity);
        while (p.path.length > 1 && distance(p, p.path[0]) < 4) p.path.shift();
        const remaining = p.remaining();
        // Ralentit en approchant de sa cible (pas d'arrêt net).
        const pace = p.intent === "sleep" ? 0.7 : p.intent === "flee" ? FLEE.speed : p.intent === "attack" ? 1.3 : 1;
        const speed = WALK_SPEED * activity * pace * clamp(remaining / 12, 0.35, 1);
        if (remaining < 1.7) {
            if (p.intent === "attack") strike(p);
            else if (p.intent === "sleep") p.sleep();
            else if (p.intent === "soar") { // au bout de son tour au-dessus de l'eau : il va se poser, sans s'arrêter
                const spot = spotNear(p, LAND);
                p.walkTo(spot.x, spot.y, "explore");
            }
            else {
                if (p.intent === "curious" && mouse) p.face(mouse);
                if (p.intent === "visit" || p.intent === "stroll") {
                    // Arrivé chez un congénère : ils se regardent.
                    const friend = friendsOf(p).sort((a, b) => distance(p, a) - distance(p, b))[0];
                    if (friend) { p.face(friend); if (friend.state === "idle") friend.face(p); }
                }
                p.rest(random(1.5, 5) / activity);
            }
            return;
        }
        const next = p.path[0];
        let dx = next.x - p.x, dy = next.y - p.y;
        const toNext = Math.hypot(dx, dy) || 1;
        dx /= toNext; dy /= toNext;
        for (const o of pokemon) {
            const d = distance(p, o);
            if (o !== p && d < 15 && d > 0) { dx += (p.x - o.x) / d * 0.6; dy += (p.y - o.y) / d * 0.6; }
        }
        const norm = Math.hypot(dx, dy) || 1;
        // La vitesse rejoint progressivement celle voulue : virages arrondis, démarrages en douceur.
        const ease = Math.min(1, dt * 6);
        p.vx += (dx / norm * speed - p.vx) * ease;
        p.vy += (dy / norm * speed - p.vy) * ease;
        // Jamais sur une case bloquée : contre un obstacle, il glisse le long (ou s'arrête).
        const nx = p.x + p.vx * dt, ny = p.y + p.vy * dt;
        const pass = p.pass;
        if (ground.isFree(nx, ny, pass)) { p.x = nx; p.y = ny; }
        else if (ground.isFree(nx, p.y, pass)) { p.x = nx; p.vy = 0; }
        else if (ground.isFree(p.x, ny, pass)) { p.y = ny; p.vx = 0; }
        else { p.vx = p.vy = 0; }
        // Regarde où il va, et ne change de direction que franchement (pas de tremblement entre deux directions voisines).
        const moving = Math.hypot(p.vx, p.vy);
        if (moving > 3) {
            const [fx, fy] = vectorOf(p.dir);
            if ((fx * p.vx + fy * p.vy) / moving < 0.85) p.dir = directionOf(p.vx, p.vy);
        }
        // Petites pauses en chemin, et abandon si quelqu'un lui barre la route.
        p.progress.time += dt;
        if (p.progress.time > 2) {
            if (p.progress.distance - remaining < 7) { p.rest(random(1, 2)); return; }
            p.progress = { time: 0, distance: remaining };
        }
        // Petite pause en chemin (pas pour un volant au-dessus de l'eau : il ne s'y arrête pas)
        if (p.intent !== "sleep" && p.intent !== "flee" && remaining > 27 && chance(0.04, dt) && ground.isFree(p.x, p.y, p.reach)) {
            const target = p.target, intent = p.intent;
            p.rest(random(0.4, 1.2));
            p.timer = random(0.4, 1.2);
            p.resume = { target, intent };
        }
    }

    // ---------- Dessin (objets de Phaser mis à jour à chaque image) ----------
    /** Planches d'un Pokémon dans les textures de Phaser : une image par case de l'animation. */
    function addTextures(anims) {
        for (const anim of new Set(Object.values(anims))) {
            if (scene.textures.exists(anim.key)) continue;
            const texture = scene.textures.addImage(anim.key, anim.image);
            for (let r = 0; r < anim.rows; r++)
                for (let c = 0; c < anim.columns; c++) texture.add(r * anim.columns + c, 0, c * anim.fw, r * anim.fh, anim.fw, anim.fh);
        }
    }

    /**
     * Ombre au sol d'une planche, dessinée une fois (adoucie) en pixels d'écran : pas d'ellipse recalculée à chaque image.
     * Refaite si la taille des sprites change (fenêtre redimensionnée), ou dans un nouveau parc (planches gardées par le
     * module, textures propres à chaque jeu de Phaser : sans, la texture « manquante » de Phaser, un carré noir).
     */
    function shadowOf(anim) {
        if (anim.shadowK !== spriteK || !scene.textures.exists(anim.shadow)) {
            const w = Math.max(2, Math.round(Math.min(anim.fw, 32) * spriteK * 0.56)), h = 6 * spriteK;
            anim.shadow = `ombre:${w}x${h}`;
            anim.shadowK = spriteK;
            if (!scene.textures.exists(anim.shadow)) {
                const texture = scene.textures.createCanvas(anim.shadow, w, h);
                const context = texture.getContext();
                context.fillStyle = "rgba(0, 0, 0, 0.18)";
                context.beginPath();
                context.ellipse(w / 2, h / 2, w / 2, h / 2, 0, 0, Math.PI * 2);
                context.fill();
                texture.refresh();
            }
        }
        return anim.shadow;
    }

    /**
     * Remous autour d'un nageur (à la place de son ombre) : deux ellipses claires (dorées dans la lave), dessinées une fois
     * comme l'ombre.
     */
    function rippleOf(anim, lava) {
        const name = lava ? "rippleLava" : "ripple";
        if (anim[name + "K"] !== spriteK || !scene.textures.exists(anim[name])) {
            const w = Math.max(4, Math.round(Math.min(anim.fw, 32) * spriteK * 0.7)), h = 7 * spriteK;
            anim[name] = `${lava ? "remous-lave" : "remous"}:${w}x${h}`;
            anim[name + "K"] = spriteK;
            if (!scene.textures.exists(anim[name])) {
                const texture = scene.textures.createCanvas(anim[name], w, h);
                const context = texture.getContext();
                context.lineWidth = spriteK;
                for (const [scale, alpha] of [[1, 0.35], [0.62, 0.6]]) {
                    context.strokeStyle = lava ? `rgba(255, 226, 120, ${alpha + 0.2})` : `rgba(255, 255, 255, ${alpha})`;
                    context.beginPath();
                    context.ellipse(w / 2, h / 2, (w / 2 - spriteK / 2) * scale, (h / 2 - spriteK / 2) * scale, 0, 0, Math.PI * 2);
                    context.stroke();
                }
                texture.refresh();
            }
        }
        return anim[name];
    }

    const snap = v => Math.round(v * k) / k; // au pixel d'écran près : déplacement continu, sans bonds d'un pixel de carte

    function draw(level) {
        night.setVisible(level > 0);
        if (level > 0) night.setFillStyle(NIGHT.color, NIGHT.alpha * level);
        for (const p of pokemon) {
            const anim = p.anim, s = spriteScale;
            // Ombre au sol (remous dans l'eau), puis l'image : SpriteCollab centre le Pokémon dans chaque image, les pieds un
            // peu sous le centre.
            if (p.wet > 0.5) {
                const wave = Math.sin(clock * 2.6 + p.id);
                p.shadow.setTexture(rippleOf(anim, p.liquid === LAVA)).setScale((1 + 0.06 * wave) / k).setAlpha(0.75 + 0.25 * wave);
            } else {
                p.shadow.setTexture(shadowOf(anim)).setScale((1 - p.altitude / (FLY_HEIGHT * 3)) / k).setAlpha(1);
            }
            p.shadow.setPosition(snap(p.x), snap(p.y));
            const row = Math.min(p.state === "sleep" ? 0 : p.dir, anim.rows - 1);
            const lift = (p.state === "held" ? LIFT : 0) + p.altitude;
            p.sprite.setTexture(anim.key, row * anim.columns + p.frame % anim.columns)
                .setScale(s)
                .setPosition(snap(p.x), snap(p.y - 6 * s - lift + p.wet * SWIM.sink * s));
            // Dans l'eau ou la lave : rien sous la surface (la ligne de ses pieds, une fois descendu de SWIM.sink)
            if (p.wet > 0.02) {
                p.sprite.setCrop(0, 0, anim.fw, Math.round(anim.fh - p.wet * (anim.fh / 2 - 6 + SWIM.sink)));
                p.cropped = true;
            } else if (p.cropped) {
                p.sprite.setCrop();
                p.cropped = false;
            }
            // Les plus bas passent devant (tri seulement s'ils changent de ligne) ; soulevé : devant tout
            const depth = lift ? HELD_DEPTH : depthOf(Math.round(p.y));
            if (p.sprite.depth !== depth) p.sprite.setDepth(depth);
            drawZzz(p, anim.fh * s);
        }
        drawMarker();
        drawName(hovered);
    }

    function drawZzz(p, h) {
        const [small, big] = p.texts;
        const sleeping = p.state === "sleep";
        small.setVisible(sleeping);
        big.setVisible(sleeping && p.zzz % 2.4 > 1.2);
        if (!sleeping) return;
        const t = p.zzz % 2.4, alpha = Math.min(1, 2.4 - t) * 0.85;
        small.setPosition(snap(p.x + 5 + t * 4), snap(p.y - h * 0.35 - t * 7 - 8)).setAlpha(alpha);
        big.setPosition(snap(p.x + 9 + (t - 1.2) * 4), snap(p.y - h * 0.35 - (t - 1.2) * 7 - 12)).setAlpha(alpha);
    }

    let labelFor = null; // Pokémon dont le nom est affiché (fond redessiné seulement quand il change)
    function drawName(p) {
        label.setVisible(!!p);
        labelText.setVisible(!!p);
        if (!p) { labelFor = null; return; }
        if (labelFor !== p) {
            labelFor = p;
            labelText.setText(p.nickname);
            label.clear().fillStyle(0x000000, 0.6).fillRoundedRect(0, 0, labelText.width + 7, 11, 3);
        }
        const width = labelText.width + 7, h = p.anim.fh * spriteScale;
        const x = snap(clamp(p.x - width / 2, 1, ground.width - width - 1)), y = snap(Math.max(1, p.y - p.altitude - h * 0.75 - 12));
        label.setPosition(x, y);
        labelText.setPosition(x + 3.5, y + 1);
    }

    // ---------- Boucle ----------
    function step(dt, level) {
        clock += dt;
        const ease = Math.min(1, dt * 5);
        for (const p of pokemon) {
            if (p.state === "idle" && p.resume && p.timer <= dt) { // reprend sa marche après une petite pause
                const { target, intent } = p.resume;
                p.resume = null;
                p.walkTo(target.x, target.y, intent);
            }
            update(p, dt, level);
            p.advanceFrame(dt * 1000, spriteScale);
            // Dans l'eau ou la lave (ou au-dessus), pas porté : le nageur s'y enfonce, le volant monte, en douceur
            if (p.moves) {
                const under = p.state !== "held" ? ground.liquidAt(p.x, p.y) : 0;
                if (p.moves === "fly") p.altitude += ((under ? FLY_HEIGHT : 0) - p.altitude) * ease;
                else {
                    if (under) p.liquid = under;
                    p.wet += ((under ? 1 : 0) - p.wet) * ease;
                }
            }
        }
    }

    let level = nightLevel(), levelAge = 0;
    function tick(delta) {
        const dt = Math.min(0.1, delta / 1000); // onglet en arrière-plan : pas de bond au retour
        levelAge += dt;
        if (levelAge > 1) { level = nightLevel(); levelAge = 0; } // l'heure, une fois par seconde
        if (drag?.target && drag.touch && !drag.moved && drag.pointer.isDown && performance.now() - drag.time >= LONG_PRESS)
            pickUp(drag.target, drag.pointer); // appui long sur un Pokémon
        if (held) { edgeScroll(dt); carry(); }
        step(dt, level);
        animate(dt);
        draw(level);
    }

    await started;
    const observer = new ResizeObserver(resize);
    observer.observe(container);
    window.addEventListener("resize", resize);
    resize();

    const removeFromScene = p => {
        if (held?.p === p) held = null;
        if (drag?.target === p) drag.target = null;
        if (hovered === p) hovered = null;
        p.sprite.destroy(); p.shadow.destroy(); for (const text of p.texts) text.destroy();
    };
    let lists = 0; // numéro de la dernière liste reçue (setPokemon)

    return {
        /** Pokémon du parc : ceux qui restent gardent leur place, les nouveaux arrivent à un endroit libre. */
        async setPokemon(list) {
            const call = ++lists;
            const kept = pokemon.filter(p => list.some(i => i.id === p.id));
            for (const p of kept) {
                const info = list.find(i => i.id === p.id);
                p.mood = info.mood ?? "normal";
                p.moves = info.moves ?? null;
            }
            for (const p of pokemon) if (!kept.includes(p)) removeFromScene(p);
            // Retirés de la liste dessinée avant d'attendre les planches des nouveaux : dessiner un Pokémon détruit lève une
            // erreur qui arrête la boucle de Phaser (parc figé, vu sous Firefox en retirant et ajoutant d'un coup).
            pokemon = kept;
            const arriving = list.filter(i => !kept.some(p => p.id === i.id));
            const loaded = await Promise.all(arriving.map(async info => {
                try { return { info, anims: await loadSheets(info.spriteFolder) }; }
                catch { return null; } // sprites absents (images pas récupérées) : ce Pokémon n'apparaît pas
            }));
            if (call !== lists) return; // une liste plus récente est arrivée pendant le chargement : c'est elle qui compte
            const placed = [...pokemon];
            for (const item of loaded.filter(l => l)) {
                const swims = item.info.moves === "swim" || item.info.moves === "swim-lava";
                const where = swims && Math.random() < SWIM.waterShare ? WATER : LAND; // nageur : surtout dans l'eau ; Feu : à terre
                let spot = ground.randomSpot(where);
                for (let tries = 0; tries < 30 && !placed.every(o => distance(o, spot) > 27); tries++) spot = ground.randomSpot(where);
                const p = new Pokemon(item.info, item.anims, ground, spot.x, spot.y);
                addTextures(p.anims);
                p.sprite = scene.add.image(0, 0, p.anim.key, 0);
                p.shadow = scene.add.image(0, 0, shadowOf(p.anim)).setDepth(1.9);
                p.texts = ["z", "Z"].map(z => scene.add.text(0, 0, z, { fontFamily: "sans-serif", fontStyle: "bold", fontSize: "7px",
                    color: "#ffffff", resolution: k }).setDepth(6).setVisible(false));
                placed.push(p);
            }
            pokemon = placed;
            if (hovered && !pokemon.includes(hovered)) hovered = null;
        },
        /** Type de la case sous le point (pixels de la carte) : 0 aucun, 1 sable ... 7 volcan (Services/ParkHabitats.cs). */
        habitatAt(x, y) { return ground.habitatAt(x, y); },
        /**
         * Mode debug : colore chaque case selon son type (colors[type] : couleur « #rrggbb », « transparent » pour rien) ; null
         * cache les couleurs. Dessiné une seule fois, dans une image comme la carte.
         */
        showHabitats(colors) {
            habitatLayer?.destroy();
            habitatLayer = null;
            if (!colors) return;
            const shapes = scene.add.graphics();
            for (let ty = 0; ty < ground.lines; ty++) {
                for (let tx = 0; tx < ground.columns; tx++) {
                    const css = colors[ground.habitatOfTile(tx, ty)];
                    if (!css?.startsWith("#")) continue; // « transparent » : aucun type
                    shapes.fillStyle(Phaser.Display.Color.HexStringToColor(css).color, HABITAT_ALPHA).fillRect(tx * TILE, ty * TILE, TILE, TILE);
                }
            }
            habitatLayer = scene.add.renderTexture(0, 0, ground.width, ground.height).setOrigin(0).setDepth(HABITAT_DEPTH);
            habitatLayer.draw(shapes);
            shapes.destroy();
        },
        /**
         * Pour les essais : fait vivre le parc <seconds> secondes d'un coup (sans dessin), à l'heure <hour> (sinon l'heure
         * actuelle), et rend l'état de chaque Pokémon.
         */
        simulate(seconds, hour) {
            const level = hour === undefined ? nightLevel() : nightLevel(new Date(2000, 0, 1, Math.floor(hour), (hour % 1) * 60));
            for (let t = 0; t < seconds; t += 1 / 30) step(1 / 30, level);
            draw(level);
            return pokemon.map(p => ({ id: p.id, species: p.species, mood: p.mood, moves: p.moves, state: p.state, intent: p.intent,
                x: Math.round(p.x), y: Math.round(p.y), energy: Math.round(p.energy * 100) / 100,
                free: ground.isFree(p.x, p.y, p.pass), // jamais sur une case qui lui est interdite
                water: ground.liquidAt(p.x, p.y) === WATER, // dans l'eau (nageur) ou au-dessus (volant)
                lava: ground.liquidAt(p.x, p.y) === LAVA })); // dans la lave (Feu) ou au-dessus (volant)
        },
        /** Mode debug : ce Pokémon attaque le plus proche, quel que soit son bonheur ; false s'il n'y en a pas. */
        attack(id) {
            const p = pokemon.find(o => o.id === id);
            const prey = p && p.state !== "held" ? preyOf(p, true) : null;
            if (!prey) return false;
            charge(p, prey);
            return true;
        },
        /** Pour les essais : pose un Pokémon à un endroit (pixels de la carte), immobile un long moment. */
        place(id, x, y) {
            const p = pokemon.find(o => o.id === id);
            if (!p) return false;
            Object.assign(p, { x, y, vx: 0, vy: 0, target: null, path: [], intent: null, timer: 600 });
            p.setState("idle");
            draw(nightLevel());
            return true;
        },
        /** Pour les essais : la caméra (zone affichée, zoom), les zones « Départ » et « Parc » (en cases), en pixels de la carte. */
        view() {
            // Calculée ici : camera.worldView n'est mise à jour qu'au dessin (rare quand l'onglet est caché)
            const width = camera.width / k, height = camera.height / k;
            const x = camera.scrollX + (camera.width - width) / 2, y = camera.scrollY + (camera.height - height) / 2;
            return { x, y, width, height, zoom: k, spriteK, start: ground.start, area: ground.area, size,
                map: { width: ground.width, height: ground.height } };
        },
        dispose() {
            observer.disconnect();
            window.removeEventListener("resize", resize);
            document.removeEventListener("fullscreenchange", onFullscreen);
            if (isFullscreen()) document.exitFullscreen().catch(() => { });
            controls.remove();
            game.destroy(true);
        },
    };
}
