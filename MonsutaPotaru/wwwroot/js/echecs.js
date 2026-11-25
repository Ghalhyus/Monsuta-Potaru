// Échecs Pokémon (Chess.razor) : le plateau, dessiné en pixel art par ce module (aucune image : cases, bordure de pierre et
// lettres sont tracées pixel par pixel), et les pièces, sprites Donjon Mystère de SpriteCollab (Idle, Walk, Sleep, Attack,
// Hurt) à la même échelle que les cases (1 pixel de sprite = 1 pixel du plateau, agrandi sans flou). Le serveur décide de
// tout (ChessHub) : ce module signale les clics sur les cases (OnSquareClicked), affiche les marques que la page lui donne
// (highlight) et joue chaque coup (play) : la pièce marche jusqu'à sa case (le cavalier en L) ; si elle mange, elle
// s'arrête devant sa proie, attaque, la proie a mal puis rejoint les K.O. sur le côté (endormie) ; un pion au bout du
// plateau et le Pokémon K.O. qui le remplace échangent leurs places en marchant. Animations au repos coupées (préférence du
// joueur, setIdle) : les pièces du plateau restent sur la première image d'Idle, sauf la pièce sélectionnée, et les K.O.
// sur la première image de Sleep. Pièces adverses face à la pièce choisie : teintées (copies des planches recolorées) et
// petit bouclier, sans autre changement (faible : rouge, bouclier cassé ; qui résiste : bleu, bouclier) ; en contraste
// élevé (setContrast), des cadres.
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
// Le canvas est plus haut que le plateau : les grands Pokémon de la rangée du haut dépassent du cadre (mesuré le 2026-11-25
// sur les 128 Pokémon des équipes : jusqu'à 18 px au repos, 28 en attaquant, Golemastoc). L'agrandissement ne compte
// que le plateau (le garder grand d'abord) ; la hauteur qui reste dans la fenêtre va d'abord au-dessus du cadre, jusqu'à
// HEADROOM, puis centre le plateau, jusqu'à MAX_ROOM au-dessus et au-dessous (un écran très haut ne l'envoie pas en bas).
const HEADROOM = 28, MAX_ROOM = 64;

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
        seed = (seed + 0x6d2b79f5) >>> 0; // reste sur 32 bits
        let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
        t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
        return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
}

// Pièces adverses face à la pièce choisie (hors contraste élevé) : teinte des sprites, du plus sombre au plus clair d'après
// la luminosité de chaque pixel (trois paliers) ; ni animation ni demi-tour (retirés à la demande de l'utilisateur)
const FILTERS = {
    weak: [[92, 4, 12], [204, 66, 74], [255, 226, 226]], // rouge : elle leur est super efficace
    resist: [[0, 58, 92], [46, 148, 196], [224, 248, 255]], // bleu : elle ne peut pas les manger
};

// Petits boucliers, 9 × 10 pixels, en bas à droite de la case : bleu (il résiste), gris cassé (il est faible).
// O : contour clair, B : bord, F : fond, S : reflet, K : bord de la fêlure ; « . » : transparent (la fêlure est un vide)
const SHIELDS = {
    resist: {
        colors: { O: 0xf4f8f8, B: 0x2be0ff, F: 0x45f7f0, S: 0xeefcfb },
        rows: [".OOOOOOO.", "OBBBBBBBO", "OBFFFFSBO", "OBFFFSFBO", "OBFFSFFBO", "OBFSFFFBO", ".OBSFFBO.", ".OBFFFBO.", "..OBFBO..", "...OOO..."],
    },
    weak: {
        colors: { O: 0xf4f4f4, B: 0xb7b7b7, F: 0x717171, K: 0x222222 },
        rows: [".OOO.OOO.", "OBBB.KBBO", "OBFFK.FBO", "OBFFK.FBO", "OBFK.FFBO", "OBFK.FFBO", ".OBFK.BO.", ".OBF.KBO.", "..OB.BO..", "...O.O..."],
    },
};

const shade = ([r, g, b], d) => [r + d, g + d, b + d].map(v => Math.max(0, Math.min(255, v)));

/** Lance step(item, index) sur chaque élément, chacun après la fin du précédent ; s'arrête au premier refus. */
const inTurn = (items, step) => items.reduce((done, item, i) => done.then(() => step(item, i)), Promise.resolve());

/** Planches d'un Pokémon dans les textures de ce jeu de Phaser : une image par case de l'animation. */
function addTextures(s, sheet) {
    for (const anim of new Set(Object.values(sheet))) {
        if (s.textures.exists(anim.key)) continue;
        const texture = s.textures.addImage(anim.key, anim.image);
        for (let r = 0; r < anim.rows; r++) {
            for (let c = 0; c < anim.columns; c++) texture.add(r * anim.columns + c, 0, c * anim.fw, r * anim.fh, anim.fw, anim.fh);
        }
    }
}

/** Ombre sous les pieds d'un Pokémon. */
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

/** Change l'animation d'un Pokémon (repos, marche, sommeil…), reprise à sa première image. */
function setState(a, state) {
    if (a.state === state) return;
    Object.assign(a, { state, frame: 0, frameTime: 0 });
}

/** Animation en cours d'un Pokémon ; celle du repos à défaut. */
function animOf(a) {
    return a.anims?.[{ idle: "Idle", walk: "Walk", sleep: "Sleep", attack: "Attack", hurt: "Hurt" }[a.state]] ?? a.anims?.Idle;
}

/** Durée d'une animation jouée une fois (attaque ou blessure), en ms. */
function durationOf(a, state) {
    const anim = a.anims?.[state === "attack" ? "Attack" : "Hurt"];
    return anim ? anim.durations.reduce((t, d) => t + d, 0) : 400;
}

/** Panneaux des K.O. : fond sombre, une case creusée par place. */
function paintPanels(put, random) {
    for (const x0 of [0, WIDTH - PANEL]) {
        for (let y = 0; y < HEIGHT; y++) {
            for (let x = x0; x < x0 + PANEL; x++) put(x, y, panelColor(x, y, x0, random));
        }
    }
}

/** Couleur d'un pixel d'un panneau des K.O. (x0 : son bord gauche) : bord, case creusée (grain au hasard) ou fond. */
function panelColor(x, y, x0, random) {
    if (y === 0 || y === HEIGHT - 1 || x === x0 || x === x0 + PANEL - 1) return PANEL_EDGE;
    const inSlots = y >= BORDER && y < BORDER + 8 * SQ;
    const sx = (x - x0) % SQ, sy = (y - BORDER) % SQ;
    if (!inSlots || sx < 3 || sx >= SQ - 3 || sy < 3 || sy >= SQ - 3) return PANEL_BG;
    return shade(PANEL_SLOT, random() < 0.08 ? 4 : 0);
}

/** Cadre de pierre : briques de 5 pixels de haut, décalées d'une rangée à l'autre. */
function paintFrame(put, random) {
    for (let y = 0; y < FRAME; y++) {
        for (let x = BOARD_X; x < BOARD_X + FRAME; x++) put(x, y, frameColor(x - BOARD_X, y, random));
    }
}

/** Couleur d'un pixel du cadre (lx : depuis son bord gauche) ; le grain de la pierre est tiré même sous le bord. */
function frameColor(lx, y, random) {
    const brick = (lx + (Math.floor(y / 5) % 2) * 7) % 14;
    let color = STONE;
    if (y % 5 === 4 || brick === 13) color = MORTAR;
    else if (y % 5 === 0 || brick === 0) color = STONE_LIGHT;
    else if (random() < 0.18) color = shade(STONE, random() < 0.5 ? -9 : 7);
    const edge = lx === 0 || y === 0 || lx === FRAME - 1 || y === FRAME - 1;
    return edge ? PANEL_EDGE : color;
}

/** Cases : a1 foncée ; grain, puis un pixel de relief (clair en haut à gauche, sombre en bas à droite). */
function paintSquares(put, random, originOf) {
    for (let square = 0; square < 64; square++) {
        const base = ((square >> 3) + (square & 7)) % 2 === 0 ? DARK : LIGHT;
        const { x: x0, y: y0 } = originOf(square);
        for (let y = 0; y < SQ; y++) {
            for (let x = 0; x < SQ; x++) put(x0 + x, y0 + y, squareColor(x, y, base, random));
        }
    }
}

/** Couleur d'un pixel d'une case (x, y : dans la case) : grain au hasard, relief sur les bords. */
function squareColor(x, y, base, random) {
    const r = random();
    let color = base;
    if (r < 0.07) color = shade(base, -10);
    else if (r < 0.12) color = shade(base, 8);
    if (x === 0 || y === 0) color = shade(base, 16);
    if (x === SQ - 1 || y === SQ - 1) color = shade(base, -18);
    return color;
}

/** Lettres des colonnes (bas) et chiffres des rangées (gauche), dans le sens du joueur. */
function paintLabels(put, originOf) {
    for (let file = 0; file < 8; file++) {
        const { x } = originOf(file);
        paintGlyph(put, "ABCDEFGH"[file], x + SQ / 2 - 1, FRAME - BORDER + 2);
    }
    for (let rank = 0; rank < 8; rank++) {
        const { y } = originOf(rank * 8);
        paintGlyph(put, String(rank + 1), BOARD_X + 3, y + SQ / 2 - 3);
    }
}

/** Un caractère de 3 × 5 pixels, avec une ombre d'un pixel dessous. */
function paintGlyph(put, char, gx, gy) {
    const pattern = GLYPHS[char];
    for (let i = 0; i < 15; i++) {
        if (pattern[i] !== "#") continue;
        put(gx + (i % 3), gy + Math.floor(i / 3) + 1, shade(MORTAR, -20)); // ombre d'un pixel
        put(gx + (i % 3), gy + Math.floor(i / 3), LABEL);
    }
}

/** Case pleine d'une couleur (o : son coin haut gauche). */
function fillBox(g, o, color, alpha) {
    g.fillStyle(color, alpha);
    g.fillRect(o.x, o.y, SQ, SQ);
}

/** Cadre d'une case, de cette épaisseur. */
function fillFrame(g, o, color, alpha, width) {
    g.fillStyle(color, alpha);
    g.fillRect(o.x, o.y, SQ, width);
    g.fillRect(o.x, o.y + SQ - width, SQ, width);
    g.fillRect(o.x, o.y, width, SQ);
    g.fillRect(o.x + SQ - width, o.y, width, SQ);
}

/** Case où la pièce choisie peut aller : un point, ou quatre coins pour une prise (dorés si super efficace). */
function drawMoveMark(g, o, move) {
    if (!move.capture) {
        const cx = o.x + SQ / 2, cy = o.y + SQ / 2;
        g.fillStyle(0x1c2733, 0.5);
        g.fillRect(cx - 2, cy - 3, 4, 6);
        g.fillRect(cx - 3, cy - 2, 6, 4);
        return;
    }
    g.fillStyle(move.super ? 0xffd23c : 0xff5a4a, 1);
    for (const [cx, cy, sx, sy] of [[o.x, o.y, 1, 1], [o.x + SQ, o.y, -1, 1], [o.x, o.y + SQ, 1, -1], [o.x + SQ, o.y + SQ, -1, -1]]) {
        g.fillRect(sx > 0 ? cx : cx - 7, sy > 0 ? cy : cy - 2, 7, 2);
        g.fillRect(sx > 0 ? cx : cx - 2, sy > 0 ? cy : cy - 7, 2, 7);
    }
}

/** Grande croix rouge sur tout le Pokémon (bord sombre, cœur rouge vif). */
function drawCross(crosses, o) {
    for (const [color, size, inset] of [[0x4a0a0a, 4, 0], [0xff2e2e, 2, 1]]) {
        crosses.fillStyle(color, 1);
        for (let i = 0; i < 23; i++) {
            crosses.fillRect(o.x + 3 + i + inset, o.y + 3 + i + inset, size, size);
            crosses.fillRect(o.x + SQ - 7 - i + inset, o.y + 3 + i + inset, size, size);
        }
    }
}

/** Petit bouclier en bas à droite de la case (o : son coin haut gauche), pixel par pixel. */
function drawShield(g, o, kind) {
    const { colors, rows } = SHIELDS[kind];
    const x0 = o.x + SQ - rows[0].length - 1, y0 = o.y + SQ - rows.length - 1;
    rows.forEach((row, y) => [...row].forEach((c, x) => {
        if (!colors[c]) return;
        g.fillStyle(colors[c], 1);
        g.fillRect(x0 + x, y0 + y, 1, 1);
    }));
}

/** Couleur de la teinte pour une luminosité de 0 à 1 : entre les paliers sombre, moyen et clair. */
function filterColor(stops, lum) {
    const [from, to, t] = lum < 0.5 ? [stops[0], stops[1], lum * 2] : [stops[1], stops[2], lum * 2 - 1];
    return from.map((v, i) => Math.round(v + (to[i] - v) * t));
}

/** Avance l'animation de delta ms ; jouée une fois, elle reste sur sa dernière image. */
function advanceFrame(a, anim, delta) {
    a.frameTime += delta;
    while (a.frameTime >= anim.durations[a.frame % anim.durations.length]) {
        if (a.once && a.frame >= anim.durations.length - 1) { a.frameTime = 0; break; } // reste sur la dernière image
        a.frameTime -= anim.durations[a.frame % anim.durations.length];
        a.frame = (a.frame + 1) % anim.durations.length;
    }
}

/** Image (key : la planche, ou sa copie teintée), place et ombre du sprite d'un Pokémon. */
function placeSprite(a, anim, key) {
    if (anim) {
        const row = Math.min(a.state === "sleep" ? 0 : a.dir, anim.rows - 1);
        a.sprite.setTexture(key, row * anim.columns + a.frame % anim.columns);
    }
    a.sprite.setPosition(Math.round(a.x), Math.round(a.y - SPRITE_UP - (a.anims ? 0 : 3)))
        .setDepth(10 + a.y / 1000).setAlpha(a.alpha).setVisible(a.visible);
    a.shadow.setPosition(Math.round(a.x), Math.round(a.y)).setAlpha(a.alpha).setVisible(a.visible && a.state !== "sleep");
}

/**
 * Plateau dans <container> ; <component> (Chess.razor) est prévenu des clics. Options : flip (les noirs en bas : je joue
 * les noirs), units (toutes les pièces : id, mine, folder des sprites, name), position (Board : id et case ; WhiteKo,
 * BlackKo), timing (walk : durée de marche d'une case ; strike : attaque et K.O., en ms), idle (faux : pièces figées au
 * repos, sauf la sélectionnée), contrast (vrai : cadres vert et rouge au lieu des teintes, boucliers et animations).
 */
export async function create(container, component, options) {
    const flip = options?.flip === true;
    let idleAnimated = options?.idle !== false;
    let contrast = options?.contrast === true;
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
        let width = row.clientWidth - (Number.parseFloat(style.paddingLeft) || 0) - (Number.parseFloat(style.paddingRight) || 0);
        if (style.flexDirection === "row") {
            const others = [...row.children].filter(e => e !== container);
            for (const e of others) width -= e.getBoundingClientRect().width;
            width -= (Number.parseFloat(style.columnGap) || 0) * others.length;
        }
        return width;
    };
    const fit = () => {
        const ratio = window.devicePixelRatio || 1;
        const width = Math.max(1, availableWidth()) * ratio;
        let below = MARGIN;
        for (let e = container.parentElement; e && e !== document.body; e = e.parentElement) {
            const style = getComputedStyle(e);
            below += (Number.parseFloat(style.paddingBottom) || 0) + (Number.parseFloat(style.borderBottomWidth) || 0) + (Number.parseFloat(style.marginBottom) || 0);
        }
        const top = container.getBoundingClientRect().top + window.scrollY;
        const height = Math.max(HEIGHT, (window.innerHeight - top - below) * ratio);
        const k = Math.max(1, Math.floor(Math.min(width / WIDTH, height / HEIGHT)));
        // Hauteur du canvas en pixels du plateau (paire : la caméra se centre sur un pixel entier) ; room : ce qui reste
        let view = Math.min(HEIGHT + 2 * MAX_ROOM, Math.max(HEIGHT, Math.floor(height / k)));
        view -= view % 2;
        const room = view - HEIGHT;
        return { ratio, k, view, top: Math.min(room, Math.max(HEADROOM, Math.floor(room / 2))) };
    };
    let size = fit();

    let overlay; // marques, redessinées à chaque changement
    let crosses; // boucliers et croix rouges, par-dessus les Pokémon
    /**
     * Animation en cours : un coup trop long (onglet en arrière-plan, navigateur ralenti) est abandonné au bout de
     * MAX_PLAY, tout le monde est remis à sa place, et l'ancienne animation s'arrête à sa prochaine étape (CANCELLED) au
     * lieu de bouger les pièces du coup suivant. Déclarée avant le jeu de Phaser : tick() la lit.
     */
    let current = null;
    const actors = new Map(); // une pièce par numéro : sprite, ombre, position, animation
    let scene, ready;
    const started = new Promise(resolve => ready = resolve);
    const game = new Phaser.Game({
        type: Phaser.AUTO,
        parent: container,
        width: WIDTH * size.k,
        height: size.view * size.k,
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
        game.scale.resize(WIDTH * size.k, size.view * size.k);
        game.scale.setZoom(1 / size.ratio);
        const camera = scene.cameras.main;
        camera.setSize(WIDTH * size.k, size.view * size.k);
        camera.setZoom(size.k);
        camera.centerOn(WIDTH / 2, size.view / 2 - size.top); // le canvas montre de -top à view - top
    }
    const onResize = () => {
        const next = fit();
        if (next.k === size.k && next.ratio === size.ratio && next.view === size.view && next.top === size.top) return;
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
        crosses = s.add.graphics().setDepth(50);
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
        // bouger depuis, et le CSS peut réduire le canvas) ; la caméra montre le plateau et la place au-dessus (size.top).
        const squareOf = p => {
            const r = game.canvas.getBoundingClientRect();
            const e = p.event;
            if (!e || !r.width || !r.height) return null;
            return squareAt((e.clientX - r.left) / r.width * WIDTH, (e.clientY - r.top) / r.height * size.view - size.top);
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
        const random = seeded(20261122); // tirés dans le même ordre : panneaux, cadre, cases
        paintPanels(put, random);
        paintFrame(put, random);
        paintSquares(put, random, originOf);
        paintLabels(put, originOf);
        ctx.putImageData(image, 0, 0);
        texture.refresh();
        s.add.image(0, 0, "echiquier").setOrigin(0, 0).setDepth(0);
    }

    // ---------- Marques : dernier coup, roi en échec, pièce choisie, cases permises, pièces faibles ou qui résistent ----------
    function drawMarks() {
        if (!overlay) return;
        const g = overlay;
        g.clear();
        crosses.clear();
        const box = (square, color, alpha) => fillBox(g, originOf(square), color, alpha);
        const frame = (square, color, alpha, width) => fillFrame(g, originOf(square), color, alpha, width);
        for (const square of marks.last ?? []) box(square, 0xffe066, 0.3);
        if (Number.isInteger(marks.check)) box(marks.check, 0xff3030, 0.4);
        if (Number.isInteger(hover)) frame(hover, 0xffffff, 0.45, 1);
        if (Number.isInteger(marks.selected)) frame(marks.selected, 0xffe14d, 1, 2);
        // Pièces adverses face à la pièce choisie. Contraste élevé : cadre vert, elle leur est super efficace ; rouge, elle ne
        // peut pas les manger. Sinon un petit bouclier, par-dessus le sprite (sa teinte et son animation : tick)
        if (contrast) {
            for (const square of marks.weak ?? []) frame(square, 0x3cd65a, 1, 2);
            for (const square of marks.resist ?? []) frame(square, 0xff3b3b, 1, 2);
        } else {
            for (const square of marks.weak ?? []) drawShield(crosses, originOf(square), "weak");
            for (const square of marks.resist ?? []) drawShield(crosses, originOf(square), "resist");
        }
        // Cases permises : un point ; prise : quatre coins (dorés si super efficace : la pièce rejouera)
        for (const move of marks.moves ?? []) drawMoveMark(g, originOf(move.to), move);
        // Pièce atteinte mais que son type empêche de manger : en contraste élevé, une grande croix rouge sur tout le Pokémon,
        // par-dessus son sprite ; sinon rien de plus (elle est déjà bleue, avec son bouclier)
        if (contrast) for (const square of marks.blocked ?? []) drawCross(crosses, originOf(square));
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

    /** Pièce sur la case sélectionnée (marks.selected), d'après la dernière position connue ; null sans sélection. */
    function selectedId() {
        if (!Number.isInteger(marks.selected)) return null;
        return (position.board ?? []).find(b => b.square === marks.selected)?.id ?? null;
    }

    /**
     * Pièces adverses face à la pièce choisie, numéro → "weak" (elle leur est super efficace) ou "resist" (elle ne peut pas
     * les manger) ; aucune en contraste élevé (des cadres) ni pendant un coup.
     */
    function matchupKinds() {
        const kinds = new Map();
        if (contrast || current) return kinds;
        const at = new Map((position.board ?? []).map(b => [b.square, b.id]));
        for (const kind of ["weak", "resist"]) {
            for (const square of marks[kind] ?? []) if (at.has(square)) kinds.set(at.get(square), kind);
        }
        return kinds;
    }

    /** Planche teintée (copie recolorée d'après la luminosité de chaque pixel, transparence gardée), faite une fois. */
    function tinted(anim, kind) {
        const key = `${anim.key}~${kind}`;
        if (scene.textures.exists(key)) return key;
        const { width, height } = anim.image;
        const texture = scene.textures.createCanvas(key, width, height);
        const ctx = texture.getContext();
        ctx.drawImage(anim.image, 0, 0);
        const image = ctx.getImageData(0, 0, width, height), d = image.data;
        for (let i = 0; i < d.length; i += 4) {
            if (d[i + 3] === 0) continue;
            [d[i], d[i + 1], d[i + 2]] = filterColor(FILTERS[kind], (0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2]) / 255);
        }
        ctx.putImageData(image, 0, 0);
        for (let r = 0; r < anim.rows; r++) {
            for (let c = 0; c < anim.columns; c++) texture.add(r * anim.columns + c, 0, c * anim.fw, r * anim.fh, anim.fw, anim.fh);
        }
        texture.refresh();
        return key;
    }

    function tick(delta) {
        const chosen = selectedId();
        const kinds = matchupKinds();
        for (const a of actors.values()) {
            a.filter = kinds.get(a.id) ?? null; // teinte seule : il ne bouge ni ne se tourne
            const anim = animOf(a);
            const frozen = !idleAnimated && (a.state === "idle" || a.state === "sleep") && a.id !== chosen;
            if (anim && frozen) {
                // Figé au repos (sur le plateau, ou endormi chez les K.O.) : la première image (sélectionné, il
                // repartira du début de l'animation)
                a.frame = 0;
                a.frameTime = 0;
            } else if (anim) {
                advanceFrame(a, anim, delta);
            }
            placeSprite(a, anim, anim && a.filter ? tinted(anim, a.filter) : anim?.key);
        }
    }

    const wait = ms => new Promise(resolve => scene.time.delayedCall(ms, resolve));

    /** Marche par ces points (un segment après l'autre), à la vitesse de la marche d'une case. */
    async function walk(a, points) {
        a.visible = true;
        await inTurn(points, point => {
            const dx = point.x - a.x, dy = point.y - a.y;
            const length = Math.hypot(dx, dy);
            if (length < 0.5) return undefined;
            a.dir = directionOf(dx, dy);
            a.once = false;
            setState(a, "walk");
            return new Promise(resolve => scene.tweens.add({
                targets: a, x: point.x, y: point.y, duration: length / SQ * walkMs, ease: "Linear", onComplete: resolve,
            }));
        });
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
        let y2 = y1;
        if (slotRow > row) y2 = line(slotRow);
        else if (slotRow < row) y2 = line(slotRow + 1);
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

    const CANCELLED = Symbol("annulé");

    /**
     * Prise : le mangeur s'approche de sa proie, l'attaque, elle est blessée, puis elle part chez les K.O. (le mangeur
     * s'installe dès qu'elle a quitté la case) ou s'efface (le roi mangé). go : chaque étape s'arrête si le coup est abandonné.
     */
    async function capture(step, mover, path, go) {
        const after = step.after, to = step.move.to;
        const victim = actors.get(step.capturedId);
        const target = footOf(step.capturedSquare);
        const before = path.length > 1 ? path.at(-2) : { x: mover.x, y: mover.y };
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
    }

    /** Anime un coup (ChessStep), puis met tout le monde à sa place exacte. */
    async function play(step, run) {
        if (!scene || disposed) return;
        const after = step.after;
        const go = async promise => {
            const result = await promise;
            if (current !== run || disposed) throw CANCELLED;
            return result;
        };
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
            if (step.capturedId !== null && step.capturedId !== undefined) await capture(step, mover, path, go);
            else await go(walk(mover, path));
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
        let back = Promise.resolve();
        if (revived && from >= 0) back = walk(revived, [...koRoute(square, revived.mine, from).reverse().slice(1), footOf(square)]);
        else if (revived) back = walk(revived, [footOf(square)]);
        await Promise.all([toKo(pawn, square, after), back]);
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
        /**
         * Marques : selected, moves ({ to, capture, super }), blocked (croix, en contraste élevé), weak (vert, ou rouge et bouclier cassé),
         * resist (rouge, ou bleu et bouclier), last, check.
         */
        highlight(next) { marks = next ?? {}; drawMarks(); },
        /** Toutes les pièces à leur place, sans animation. */
        setPosition(p) { if (p) sync(p); },
        /** Animations au repos (Idle en boucle) ou pièces figées, sauf la sélectionnée. */
        setIdle(animated) { idleAnimated = animated !== false; },
        /** Contraste élevé : cadres vert et rouge ; sinon teintes, boucliers et animations. */
        setContrast(on) { contrast = on === true; drawMarks(); },
        dispose() {
            disposed = true;
            observer.disconnect();
            window.removeEventListener("resize", onResize);
            game.destroy(true);
        },
    };
}
