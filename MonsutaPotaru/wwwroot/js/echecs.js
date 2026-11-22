// Échecs Pokémon (Chess.razor) : le plateau, dessiné en pixel art par ce module (aucune image : cases, bordure de pierre et
// lettres sont tracées pixel par pixel), et les pièces, sprites Donjon Mystère de SpriteCollab (Idle, Walk, Sleep, Attack,
// Hurt) à la même échelle que les cases (1 pixel de sprite = 1 pixel du plateau, agrandi sans flou). Le serveur décide de
// tout (ChessHub) : ce module signale les clics sur les cases (OnSquareClicked), affiche les marques que la page lui donne
// (highlight) et joue chaque coup (play) : la pièce marche jusqu'à sa case (le cavalier en L) ; si elle mange, elle
// s'arrête devant sa proie, attaque, la proie a mal puis rejoint les K.O. sur le côté (endormie) ; un pion au bout du
// plateau et le Pokémon K.O. qui le remplace échangent leurs places en marchant. Animations au repos coupées (préférence du
// joueur, setIdle) : les pièces du plateau restent sur la première image d'Idle, sauf la pièce sélectionnée, et les K.O.
// sur la première image de Sleep.
import * as Phaser from "../lib/phaser/phaser.esm.min.js";
import { loadSheets } from "./parc.js";

// Géométrie, en pixels du plateau : les K.O. de l'adversaire à gauche, les miens à droite (deux colonnes de huit cases)
const SQ = 32, BORDER = 10, GAP = 8, KO_ROWS = 8, KO_COLS = 2;
const PANEL = KO_COLS * SQ;
const FRAME = 8 * SQ + 2 * BORDER;
const WIDTH = PANEL + GAP + FRAME + GAP + PANEL;
const HEIGHT = FRAME;
const BOARD_X = PANEL + GAP; // bord gauche du cadre de pierre
const MARGIN = 8; // pixels CSS gardés libres sous le plateau (en plus des marges de la page) : rien ne défile

// Couleurs (r, g, b)
const LIGHT = [232, 214, 168], DARK = [143, 100, 63];
const STONE = [104, 100, 116], MORTAR = [64, 61, 74], STONE_LIGHT = [136, 132, 148];
const PANEL_BG = [43, 47, 58], PANEL_SLOT = [52, 57, 71], PANEL_EDGE = [30, 33, 41];
const LABEL = [232, 226, 240];

const FOOT = 8; // les pieds d'un Pokémon : sous le centre de sa case
const SPRITE_UP = 6; // SpriteCollab centre le dessin, les pieds 4 à 10 px sous le centre (comme au parc)
const APPROACH = 18; // distance où il s'arrête devant sa proie
const MAX_PLAY = 15000; // durée maximale de l'animation d'un coup (ms) : jusqu'à ~10 s avec la marche d'un K.O.
const FADE = 260;
const UP = 4, DOWN = 0;
const directionOf = (dx, dy) => (((Math.round((90 - Math.atan2(dy, dx) * 180 / Math.PI) / 45)) % 8) + 8) % 8;

// Lettres et chiffres des bords, 3 × 5 pixels (les cinq lignes de trois pixels à la suite)
const GLYPHS = {
    A: ".#.#.#####.##.#", B: "##.#.###.#.###.", C: ".###..#..#...##", D: "##.#.##.##.###.", E: "####..##.#..###",
    F: "####..##.#..#..", G: ".###..#.##.#.##", H: "#.##.#####.##.#", 1: ".#.##..#..#.###", 2: "##...#.#.#..###",
    3: "##...#.#...###.", 4: "#.##.####..#..#", 5: "####..##...###.", 6: ".###..####.####", 7: "###..#.#..#..#.",
    8: "####.#####.####",
};

/** Générateur pseudo-aléatoire à graine (le grain des cases est le même à chaque affichage). */
function seeded(seed) {
    return () => {
        seed = (seed + 0x6d2b79f5) | 0;
        let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
        t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
        return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
}

const shade = ([r, g, b], d) => [r + d, g + d, b + d].map(v => Math.max(0, Math.min(255, v)));

/**
 * Plateau dans <container> ; <component> (Chess.razor) est prévenu des clics. Options : flip (les noirs en bas : je joue
 * les noirs), units (toutes les pièces : id, mine, folder des sprites, name), position (Board : id et case ; WhiteKo,
 * BlackKo), timing (walk : durée de marche d'une case ; strike : attaque et K.O., en ms), idle (faux : pièces figées au
 * repos, sauf la sélectionnée).
 */
export async function create(container, component, options) {
    const flip = options?.flip === true;
    let idleAnimated = options?.idle !== false;
    const walkMs = Number(options?.timing?.walk) || 350;
    const units = new Map((options?.units ?? []).map(u => [u.id, u]));
    let position = options?.position ?? { board: [], whiteKo: [], blackKo: [] };
    let marks = {};
    let hover = null;
    let disposed = false;

    // Sprites de chaque pièce (une planche par dossier, partagée avec le parc)
    const anims = new Map();
    await Promise.all([...units.values()].map(async u => {
        try { anims.set(u.id, await loadSheets(u.folder)); } catch { /* sprites absents : un rond à la place */ }
    }));
    if (disposed) return null;

    // ---------- Cases ----------
    const cellOf = square => {
        const rank = square >> 3, file = square & 7;
        return flip ? { col: 7 - file, row: rank } : { col: file, row: 7 - rank };
    };
    const originOf = square => {
        const { col, row } = cellOf(square);
        return { x: BOARD_X + BORDER + col * SQ, y: BORDER + row * SQ };
    };
    const footOf = square => { const o = originOf(square); return { x: o.x + SQ / 2, y: o.y + SQ / 2 + FOOT }; };
    const squareAt = (x, y) => {
        const col = Math.floor((x - BOARD_X - BORDER) / SQ), row = Math.floor((y - BORDER) / SQ);
        if (col < 0 || col > 7 || row < 0 || row > 7) return null;
        const file = flip ? 7 - col : col, rank = flip ? row : 7 - row;
        return rank * 8 + file;
    };
    /** Place d'un K.O. : les miens à droite, ceux de l'adversaire à gauche, de haut en bas. */
    const koFoot = (mine, index) => {
        const x0 = mine ? WIDTH - PANEL : 0;
        const col = Math.floor(index / KO_ROWS) % KO_COLS, row = index % KO_ROWS;
        return { x: x0 + col * SQ + SQ / 2, y: BORDER + row * SQ + SQ / 2 + FOOT };
    };
    const myKo = p => (flip ? p.blackKo : p.whiteKo) ?? [];
    const theirKo = p => (flip ? p.whiteKo : p.blackKo) ?? [];

    // ---------- Taille : un nombre entier de pixels d'écran par pixel du plateau ----------
    /**
     * Le plus grand agrandissement qui tient dans la largeur du conteneur et dans la hauteur de la fenêtre sous le haut du
     * plateau, moins ce que la page met dessous (marges et bordures des blocs qui l'entourent) : la partie se joue sans
     * défiler, quels que soient le thème et ce qu'il y a au-dessus.
     */
    /**
     * Largeur permise : celle de la rangée (.chess-play) moins la colonne d'à côté ; le conteneur, lui, a la largeur du
     * canvas (la colonne reste collée au plateau). Colonnes l'une sur l'autre (écran étroit) : toute la rangée.
     */
    const availableWidth = () => {
        const row = container.parentElement;
        if (!row) return container.clientWidth;
        const style = getComputedStyle(row);
        let width = row.clientWidth - (parseFloat(style.paddingLeft) || 0) - (parseFloat(style.paddingRight) || 0);
        if (style.flexDirection === "row") {
            const others = [...row.children].filter(e => e !== container);
            for (const e of others) width -= e.getBoundingClientRect().width;
            width -= (parseFloat(style.columnGap) || 0) * others.length;
        }
        return width;
    };
    const fit = () => {
        const ratio = window.devicePixelRatio || 1;
        const width = Math.max(1, availableWidth()) * ratio;
        let below = MARGIN;
        for (let e = container.parentElement; e && e !== document.body; e = e.parentElement) {
            const style = getComputedStyle(e);
            below += (parseFloat(style.paddingBottom) || 0) + (parseFloat(style.borderBottomWidth) || 0) + (parseFloat(style.marginBottom) || 0);
        }
        const top = container.getBoundingClientRect().top + window.scrollY;
        const height = Math.max(HEIGHT, (window.innerHeight - top - below) * ratio);
        return { ratio, k: Math.max(1, Math.floor(Math.min(width / WIDTH, height / HEIGHT))) };
    };
    let size = fit();

    let overlay; // marques, redessinées à chaque changement
    const actors = new Map(); // une pièce par numéro : sprite, ombre, position, animation
    let scene, ready;
    const started = new Promise(resolve => ready = resolve);
    const game = new Phaser.Game({
        type: Phaser.AUTO,
        parent: container,
        width: WIDTH * size.k,
        height: HEIGHT * size.k,
        transparent: true,
        pixelArt: true,
        roundPixels: true,
        banner: false,
        audio: { noAudio: true },
        scale: { mode: Phaser.Scale.NONE, zoom: 1 / size.ratio },
        scene: {
            create() { build(this); ready(); },
            update(_, delta) { tick(delta); },
        },
    });
    await started;

    // Fonctions déclarées (hissées) : Phaser appelle build() et tick() avant que la suite de create() ne s'exécute
    function applySize() {
        game.scale.resize(WIDTH * size.k, HEIGHT * size.k);
        game.scale.setZoom(1 / size.ratio);
        const camera = scene.cameras.main;
        camera.setSize(WIDTH * size.k, HEIGHT * size.k);
        camera.setZoom(size.k);
        camera.centerOn(WIDTH / 2, HEIGHT / 2);
    }
    const onResize = () => {
        const next = fit();
        if (next.k === size.k && next.ratio === size.ratio) return;
        size = next;
        applySize();
    };
    // La largeur du conteneur, et tout ce qui déplace le plateau (un message au-dessus, la colonne posée dessus sur un
    // écran étroit) : un nouvel agrandissement seulement s'il change
    const observer = new ResizeObserver(onResize);
    observer.observe(container.parentElement ?? container);
    observer.observe(document.body);
    window.addEventListener("resize", onResize);

    // ---------- Décor ----------
    function build(s) {
        scene = s;
        game.canvas.classList.add("chess-canvas");
        drawBoard(s);
        overlay = s.add.graphics().setDepth(1);
        for (const u of units.values()) {
            const a = { id: u.id, mine: u.mine, name: u.name, anims: anims.get(u.id), x: 0, y: 0, dir: u.mine ? UP : DOWN, state: "idle", frame: 0, frameTime: 0, alpha: 1, once: false };
            if (a.anims) addTextures(s, a.anims);
            a.shadow = s.add.image(0, 0, shadowTexture(s)).setDepth(2);
            a.sprite = a.anims ? s.add.image(0, 0, a.anims.Idle.key, 0) : s.add.image(0, 0, placeholder(s, u.mine));
            actors.set(u.id, a);
        }
        applySize();
        sync(position);
        drawMarks();

        // Case sous le pointeur d'après la place du canvas au moment même (Phaser garde celle du démarrage : la page a pu
        // bouger depuis, et le CSS peut réduire le canvas) ; la caméra montre tout le plateau, rien d'autre.
        const squareOf = p => {
            const r = game.canvas.getBoundingClientRect();
            const e = p.event;
            if (!e || !r.width || !r.height) return null;
            return squareAt((e.clientX - r.left) / r.width * WIDTH, (e.clientY - r.top) / r.height * HEIGHT);
        };
        s.input.on("pointerdown", p => {
            const square = squareOf(p);
            if (square !== null && !disposed) component.invokeMethodAsync("OnSquareClicked", square).catch(() => { });
        });
        s.input.on("pointermove", p => {
            const square = squareOf(p);
            if (square === hover) return;
            hover = square;
            game.canvas.style.cursor = square === null ? "default" : "pointer";
            drawMarks();
        });
        s.input.on("pointerout", () => { hover = null; drawMarks(); });
    }

    /** Le plateau entier dans une texture : panneaux des K.O., cadre de pierre et ses lettres, cases à grain. */
    function drawBoard(s) {
        const texture = s.textures.createCanvas("echiquier", WIDTH, HEIGHT);
        const ctx = texture.getContext();
        const image = ctx.createImageData(WIDTH, HEIGHT);
        const put = (x, y, [r, g, b]) => {
            const i = (y * WIDTH + x) * 4;
            image.data[i] = r; image.data[i + 1] = g; image.data[i + 2] = b; image.data[i + 3] = 255;
        };
        const random = seeded(20261122);

        // Panneaux des K.O. : fond sombre, une case creusée par place
        for (const x0 of [0, WIDTH - PANEL]) {
            for (let y = 0; y < HEIGHT; y++) {
                for (let x = x0; x < x0 + PANEL; x++) {
                    const edge = y === 0 || y === HEIGHT - 1 || x === x0 || x === x0 + PANEL - 1;
                    const inSlots = y >= BORDER && y < BORDER + 8 * SQ;
                    const sx = (x - x0) % SQ, sy = (y - BORDER) % SQ;
                    const slot = inSlots && sx >= 3 && sx < SQ - 3 && sy >= 3 && sy < SQ - 3;
                    put(x, y, edge ? PANEL_EDGE : slot ? shade(PANEL_SLOT, random() < 0.08 ? 4 : 0) : PANEL_BG);
                }
            }
        }

        // Cadre de pierre : briques de 5 pixels de haut, décalées d'une rangée à l'autre
        for (let y = 0; y < FRAME; y++) {
            for (let x = BOARD_X; x < BOARD_X + FRAME; x++) {
                const lx = x - BOARD_X;
                const course = Math.floor(y / 5);
                const brick = (lx + (course % 2) * 7) % 14;
                let color = y % 5 === 4 || brick === 13 ? MORTAR : y % 5 === 0 || brick === 0 ? STONE_LIGHT : STONE;
                if (color === STONE && random() < 0.18) color = shade(STONE, random() < 0.5 ? -9 : 7);
                if (lx === 0 || y === 0 || lx === FRAME - 1 || y === FRAME - 1) color = PANEL_EDGE;
                put(x, y, color);
            }
        }

        // Cases : a1 foncée ; grain, puis un pixel de relief (clair en haut à gauche, sombre en bas à droite)
        for (let square = 0; square < 64; square++) {
            const base = ((square >> 3) + (square & 7)) % 2 === 0 ? DARK : LIGHT;
            const { x: x0, y: y0 } = originOf(square);
            for (let y = 0; y < SQ; y++) {
                for (let x = 0; x < SQ; x++) {
                    let color = base;
                    const r = random();
                    if (r < 0.07) color = shade(base, -10);
                    else if (r < 0.12) color = shade(base, 8);
                    if (x === 0 || y === 0) color = shade(base, 16);
                    if (x === SQ - 1 || y === SQ - 1) color = shade(base, -18);
                    put(x0 + x, y0 + y, color);
                }
            }
        }

        // Lettres des colonnes (bas) et chiffres des rangées (gauche), dans le sens du joueur
        const glyph = (char, gx, gy) => {
            const pattern = GLYPHS[char];
            for (let i = 0; i < 15; i++) {
                if (pattern[i] !== "#") continue;
                put(gx + (i % 3), gy + Math.floor(i / 3) + 1, shade(MORTAR, -20)); // ombre d'un pixel
                put(gx + (i % 3), gy + Math.floor(i / 3), LABEL);
            }
        };
        for (let file = 0; file < 8; file++) {
            const { x } = originOf(file);
            glyph("ABCDEFGH"[file], x + SQ / 2 - 1, FRAME - BORDER + 2);
        }
        for (let rank = 0; rank < 8; rank++) {
            const { y } = originOf(rank * 8);
            glyph(String(rank + 1), BOARD_X + 3, y + SQ / 2 - 3);
        }
        ctx.putImageData(image, 0, 0);
        texture.refresh();
        s.add.image(0, 0, "echiquier").setOrigin(0, 0).setDepth(0);
    }

    /** Planches d'un Pokémon dans les textures de ce jeu de Phaser : une image par case de l'animation. */
    function addTextures(s, sheet) {
        for (const anim of new Set(Object.values(sheet))) {
            if (s.textures.exists(anim.key)) continue;
            const texture = s.textures.addImage(anim.key, anim.image);
            for (let r = 0; r < anim.rows; r++)
                for (let c = 0; c < anim.columns; c++) texture.add(r * anim.columns + c, 0, c * anim.fw, r * anim.fh, anim.fw, anim.fh);
        }
    }

    function shadowTexture(s) {
        if (!s.textures.exists("ombre-echecs")) {
            const t = s.textures.createCanvas("ombre-echecs", 16, 5), c = t.getContext();
            c.fillStyle = "rgba(0,0,0,0.32)";
            c.fillRect(3, 0, 10, 5);
            c.fillRect(1, 1, 14, 3);
            t.refresh();
        }
        return "ombre-echecs";
    }

    /** Sprites absents (images pas récupérées) : un rond de la couleur du camp. */
    function placeholder(s, mine) {
        const key = mine ? "pion-moi" : "pion-autre";
        if (!s.textures.exists(key)) {
            const t = s.textures.createCanvas(key, 18, 18), c = t.getContext();
            c.fillStyle = mine ? "#f4f1ea" : "#2a2633";
            c.fillRect(4, 1, 10, 16);
            c.fillRect(1, 4, 16, 10);
            c.fillRect(2, 2, 14, 14);
            t.refresh();
        }
        return key;
    }

    // ---------- Marques : dernier coup, roi en échec, pièce choisie, cases permises, pièces qui résistent ----------
    function drawMarks() {
        if (!overlay) return;
        const g = overlay;
        g.clear();
        const box = (square, color, alpha) => {
            const o = originOf(square);
            g.fillStyle(color, alpha);
            g.fillRect(o.x, o.y, SQ, SQ);
        };
        const frame = (square, color, alpha, width) => {
            const o = originOf(square);
            g.fillStyle(color, alpha);
            g.fillRect(o.x, o.y, SQ, width);
            g.fillRect(o.x, o.y + SQ - width, SQ, width);
            g.fillRect(o.x, o.y, width, SQ);
            g.fillRect(o.x + SQ - width, o.y, width, SQ);
        };
        for (const square of marks.last ?? []) box(square, 0xffe066, 0.3);
        if (Number.isInteger(marks.check)) box(marks.check, 0xff3030, 0.4);
        if (Number.isInteger(hover)) frame(hover, 0xffffff, 0.45, 1);
        if (Number.isInteger(marks.selected)) frame(marks.selected, 0xffe14d, 1, 2);
        for (const move of marks.moves ?? []) {
            const o = originOf(move.to);
            if (!move.capture) {
                const cx = o.x + SQ / 2, cy = o.y + SQ / 2;
                g.fillStyle(0x1c2733, 0.5);
                g.fillRect(cx - 2, cy - 3, 4, 6);
                g.fillRect(cx - 3, cy - 2, 6, 4);
                continue;
            }
            // Prise : quatre coins (dorés si super efficace : la pièce rejouera)
            g.fillStyle(move.super ? 0xffd23c : 0xff5a4a, 1);
            for (const [cx, cy, sx, sy] of [[o.x, o.y, 1, 1], [o.x + SQ, o.y, -1, 1], [o.x, o.y + SQ, 1, -1], [o.x + SQ, o.y + SQ, -1, -1]]) {
                g.fillRect(sx > 0 ? cx : cx - 7, sy > 0 ? cy : cy - 2, 7, 2);
                g.fillRect(sx > 0 ? cx : cx - 2, sy > 0 ? cy : cy - 7, 2, 7);
            }
        }
        // Pièce qui résiste : une croix grise dans le coin, la case un peu assombrie
        for (const square of marks.blocked ?? []) {
            box(square, 0x000000, 0.18);
            const o = originOf(square);
            g.fillStyle(0x3a3a44, 0.95);
            for (let i = 0; i < 6; i++) {
                g.fillRect(o.x + SQ - 9 + i, o.y + 3 + i, 2, 1);
                g.fillRect(o.x + SQ - 4 - i, o.y + 3 + i, 2, 1);
            }
        }
    }

    // ---------- Pièces ----------
    /** Tout le monde à sa place, sans animation : sur sa case (tourné vers l'adversaire), ou endormi chez les K.O. */
    function sync(p) {
        position = p;
        const placed = new Map((p.board ?? []).map(b => [b.id, b.square]));
        const mineKo = myKo(p), otherKo = theirKo(p);
        for (const a of actors.values()) {
            scene.tweens.killTweensOf(a);
            let spot, state = "idle";
            if (placed.has(a.id)) spot = footOf(placed.get(a.id));
            else if (mineKo.includes(a.id)) { spot = koFoot(true, mineKo.indexOf(a.id)); state = "sleep"; }
            else if (otherKo.includes(a.id)) { spot = koFoot(false, otherKo.indexOf(a.id)); state = "sleep"; }
            a.visible = spot !== undefined;
            if (spot) Object.assign(a, spot);
            a.alpha = 1;
            a.dir = a.mine ? UP : DOWN;
            setState(a, state);
        }
    }

    function setState(a, state) {
        if (a.state === state) return;
        Object.assign(a, { state, frame: 0, frameTime: 0 });
    }

    function animOf(a) {
        return a.anims?.[{ idle: "Idle", walk: "Walk", sleep: "Sleep", attack: "Attack", hurt: "Hurt" }[a.state]] ?? a.anims?.Idle;
    }

    function durationOf(a, state) {
        const anim = a.anims?.[state === "attack" ? "Attack" : "Hurt"];
        return anim ? anim.durations.reduce((t, d) => t + d, 0) : 400;
    }

    /** Pièce sur la case sélectionnée (marks.selected), d'après la dernière position connue ; null sans sélection. */
    function selectedId() {
        if (!Number.isInteger(marks.selected)) return null;
        return (position.board ?? []).find(b => b.square === marks.selected)?.id ?? null;
    }

    function tick(delta) {
        const chosen = idleAnimated ? null : selectedId();
        for (const a of actors.values()) {
            const anim = animOf(a);
            if (anim) {
                if (!idleAnimated && (a.state === "idle" || a.state === "sleep") && a.id !== chosen) {
                    // Figé au repos (sur le plateau, ou endormi chez les K.O.) : la première image (sélectionné, il
                    // repartira du début de l'animation)
                    a.frame = 0;
                    a.frameTime = 0;
                } else {
                    a.frameTime += delta;
                    while (a.frameTime >= anim.durations[a.frame % anim.durations.length]) {
                        if (a.once && a.frame >= anim.durations.length - 1) { a.frameTime = 0; break; } // reste sur la dernière image
                        a.frameTime -= anim.durations[a.frame % anim.durations.length];
                        a.frame = (a.frame + 1) % anim.durations.length;
                    }
                }
                const row = Math.min(a.state === "sleep" ? 0 : a.dir, anim.rows - 1);
                a.sprite.setTexture(anim.key, row * anim.columns + a.frame % anim.columns);
            }
            a.sprite.setPosition(Math.round(a.x), Math.round(a.y - SPRITE_UP - (a.anims ? 0 : 3)))
                .setDepth(10 + a.y / 1000).setAlpha(a.alpha).setVisible(a.visible);
            a.shadow.setPosition(Math.round(a.x), Math.round(a.y)).setAlpha(a.alpha).setVisible(a.visible && a.state !== "sleep");
        }
    }

    const wait = ms => new Promise(resolve => scene.time.delayedCall(ms, resolve));

    /** Marche par ces points (un segment après l'autre), à la vitesse de la marche d'une case. */
    async function walk(a, points) {
        a.visible = true;
        for (const point of points) {
            const dx = point.x - a.x, dy = point.y - a.y;
            const length = Math.hypot(dx, dy);
            if (length < 0.5) continue;
            a.dir = directionOf(dx, dy);
            a.once = false;
            setState(a, "walk");
            await new Promise(resolve => scene.tweens.add({
                targets: a, x: point.x, y: point.y, duration: length / SQ * walkMs, ease: "Linear", onComplete: resolve,
            }));
        }
        setState(a, "idle");
    }

    /** Joue une fois une animation (attaque, blessure), sur place. */
    async function once(a, state) {
        a.once = true;
        setState(a, state);
        a.frame = 0;
        await wait(durationOf(a, state));
        a.once = false;
    }

    const fade = (a, to) => new Promise(resolve => scene.tweens.add({ targets: a, alpha: to, duration: FADE, onComplete: resolve }));
    const face = (a, b) => { a.dir = directionOf(b.x - a.x, b.y - a.y); };

    /** Chemin d'une case à une autre : en ligne droite, ou en L pour le cavalier (deux cases, puis une). */
    function pathOf(from, to) {
        const end = footOf(to);
        const dr = (to >> 3) - (from >> 3), df = (to & 7) - (from & 7);
        if (dr === 0 || df === 0 || Math.abs(dr) === Math.abs(df)) return [end];
        const start = footOf(from);
        // Les deux cases d'abord (dans le sens le plus long), puis la dernière à angle droit
        const corner = Math.abs(dr) > Math.abs(df) ? { x: start.x, y: end.y } : { x: end.x, y: start.y };
        return [corner, end];
    }

    /** Place d'un Pokémon chez les K.O. de son camp dans cette position (-1 : il n'y est pas). */
    const koIndex = (a, p) => (a.mine ? myKo(p) : theirKo(p)).indexOf(a.id);

    /**
     * Chemin d'une case à une place des K.O., entre les cases (choix de l'utilisateur, 2026-11-22) : une demi-case vers
     * la ligne entre deux rangées (du côté de sa place), cette ligne jusqu'à l'espace entre le plateau et le panneau des
     * K.O., cet espace jusqu'à la ligne voisine de sa place, puis sa place. Il ne passe ainsi sur aucun autre Pokémon.
     * Même calcul côté serveur (ChessTiming.KoRoutePixels), pour le temps du coup.
     */
    function koRoute(square, mine, index) {
        const start = footOf(square), slot = koFoot(mine, index);
        const row = cellOf(square).row, slotRow = index % KO_ROWS;
        const line = k => BORDER + k * SQ + FOOT; // ligne entre les rangées k - 1 et k (à hauteur des pieds)
        const y1 = line(slotRow >= row ? row + 1 : row);
        const y2 = slotRow > row ? line(slotRow) : slotRow < row ? line(slotRow + 1) : y1;
        const gapX = mine ? BOARD_X + FRAME + GAP / 2 : PANEL + GAP / 2;
        return [{ x: start.x, y: y1 }, { x: gapX, y: y1 }, { x: gapX, y: y2 }, { x: slot.x, y: y2 }, slot];
    }

    /** Le Pokémon K.O. marche de sa case à sa place (d'après la position d'après le coup), puis s'y endort. */
    async function toKo(a, square, after) {
        const index = koIndex(a, after);
        if (index < 0) return;
        await walk(a, koRoute(square, a.mine, index));
        setState(a, "sleep");
    }

    /**
     * Animation en cours : un coup trop long (onglet en arrière-plan, navigateur ralenti) est abandonné au bout de
     * MAX_PLAY, tout le monde est remis à sa place, et l'ancienne animation s'arrête à sa prochaine étape (CANCELLED) au
     * lieu de bouger les pièces du coup suivant.
     */
    let current = null;
    const CANCELLED = Symbol("annulé");

    /** Anime un coup (ChessStep), puis met tout le monde à sa place exacte. */
    async function play(step, run) {
        if (!scene || disposed) return;
        const after = step.after;
        const go = async promise => { const result = await promise; if (current !== run || disposed) throw CANCELLED; return result; };
        try {
            const mover = actors.get(step.unitId);
            if (step.move.isPass || !mover) {
                await go(wait(300));
                return;
            }
            const from = step.move.from, to = step.move.to;
            if (step.move.isSwap) { // pion déjà au bout : il part chez les K.O., le Pokémon revenu prend sa case
                await go(swap(mover, actors.get(step.revivedId), to, after));
                return;
            }
            const path = pathOf(from, to);
            if (step.capturedId !== null && step.capturedId !== undefined) {
                const victim = actors.get(step.capturedId);
                const target = footOf(step.capturedSquare);
                const before = path.length > 1 ? path[path.length - 2] : { x: mover.x, y: mover.y };
                const dx = target.x - before.x, dy = target.y - before.y, length = Math.hypot(dx, dy) || 1;
                const stop = length > APPROACH ? { x: target.x - dx / length * APPROACH, y: target.y - dy / length * APPROACH } : before;
                await go(walk(mover, [...path.slice(0, -1), stop]));
                face(mover, victim);
                face(victim, mover);
                const attack = once(mover, "attack");
                await go(wait(durationOf(mover, "attack") * 0.4));
                await go(once(victim, "hurt"));
                await go(attack);
                if (koIndex(victim, after) >= 0) {
                    // Le K.O. part vers sa place ; dès qu'il a quitté la case (une demi-case), le mangeur s'y installe
                    const leaving = toKo(victim, step.capturedSquare, after);
                    await go(wait(walkMs / 2));
                    await go(Promise.all([leaving, walk(mover, [footOf(to)])]));
                } else {
                    await go(fade(victim, 0)); // le roi mangé : la partie est finie, il n'a pas de place chez les K.O.
                    victim.visible = false;
                    await go(walk(mover, [footOf(to)]));
                }
            } else {
                await go(walk(mover, path));
            }
            if (step.rookId !== null && step.rookId !== undefined) await go(walk(actors.get(step.rookId), [footOf(step.rookTo)]));
            if (step.revivedId !== null && step.revivedId !== undefined) await go(swap(mover, actors.get(step.revivedId), to, after));
        } catch (e) {
            if (e !== CANCELLED) throw e;
        } finally {
            if (!disposed && current === run) sync(after);
        }
    }

    /**
     * Le pion et le Pokémon K.O. échangent leurs places, en marchant en même temps, entre les cases (koRoute) : le pion
     * vers les K.O., le revenu de sa place (dans la position d'avant le coup) à la case.
     */
    async function swap(pawn, revived, square, after) {
        const from = revived ? koIndex(revived, position) : -1;
        await Promise.all([
            toKo(pawn, square, after),
            revived && from >= 0 ? walk(revived, [...koRoute(square, revived.mine, from).reverse().slice(1), footOf(square)])
                : revived ? walk(revived, [footOf(square)]) : Promise.resolve(),
        ]);
    }

    return {
        /** Un coup à animer ; la promesse se termine à la fin de l'animation (10 s au plus : la page ne reste pas bloquée). */
        play(step) {
            const run = {};
            current = run;
            return Promise.race([play(step, run), new Promise(resolve => setTimeout(resolve, MAX_PLAY))]).finally(() => {
                if (current === run && !disposed) { current = null; sync(step.after); } // fini, ou abandonné
            });
        },
        /** Marques : selected, moves ({ to, capture, super }), blocked, last, check. */
        highlight(next) { marks = next ?? {}; drawMarks(); },
        /** Toutes les pièces à leur place, sans animation. */
        setPosition(p) { if (p) sync(p); },
        /** Animations au repos (Idle en boucle) ou pièces figées, sauf la sélectionnée. */
        setIdle(animated) { idleAnimated = animated !== false; },
        dispose() {
            disposed = true;
            observer.disconnect();
            window.removeEventListener("resize", onResize);
            game.destroy(true);
        },
    };
}
