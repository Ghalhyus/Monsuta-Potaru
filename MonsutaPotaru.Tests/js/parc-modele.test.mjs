// Tests du modèle du parc (wwwroot/js/parc-modele.js), sans navigateur : lancés par Node (node --test), depuis
// ParcJsTests.cs (dotnet test). Les cartes lues sont les vraies (wwwroot/parc/{carte}-{taille}.json).
import { test, describe } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import {
    TILE, LAND, WATER, LAVA, LIQUID, SUMMIT, AIR, Ground, seeded, between, hypot, clamp, distance, MOODS, loweredMood,
    nightLevel, personalityOf,
} from "../../MonsutaPotaru/wwwroot/js/parc-modele.js";

const MAPS = ["parc-1", "plage-1", "volcan-1", "desert-1", "ocean-1", "grotte-1", "montagne-1", "donjon-1"];
const SIZES = [1, 2, 3, 4];
const mapFile = (map, size) => fileURLToPath(new URL(`../../MonsutaPotaru/wwwroot/parc/${map}-${size}.json`, import.meta.url));
const cache = new Map();
/** Carte lue une fois (comme parc.js la reçoit de fetch). */
function load(map, size) {
    const key = `${map}-${size}`;
    if (!cache.has(key)) cache.set(key, JSON.parse(readFileSync(mapFile(map, size), "utf8")));
    return cache.get(key);
}
const layer = (data, name) => data.layers.find(l => l.name === name && l.type === "tilelayer")?.data ?? [];
/** Les cases (indices) de <ground> où walkable vaut exactement <bit>. */
const cellsOf = (ground, bit) => [...ground.walkable.keys()].filter(i => ground.walkable[i] === bit);
/** Façons de se déplacer de parc.js (Pokemon.pass) : où l'on passe. */
const PASS = { marcheur: LAND, nageur: LAND | WATER, feu: LAND | LAVA, volant: LAND | LIQUID | SUMMIT | AIR };

describe("hasard et calculs", () => {
    test("même graine, même suite de nombres, entre 0 et 1", () => {
        const a = seeded(424242), b = seeded(424242), c = seeded(424243);
        const first = Array.from({ length: 1000 }, a), second = Array.from({ length: 1000 }, b);
        assert.deepEqual(first, second);
        assert.notDeepEqual(Array.from({ length: 10 }, c), first.slice(0, 10));
        assert.ok(first.every(v => v >= 0 && v < 1));
        // Valeurs de mulberry32 : les changer, c'est changer ce que voient les joueurs du cache-cache pendant un déploiement
        assert.deepEqual(first.slice(0, 3), [0.1287079993635416, 0.23631658218801022, 0.18376533640548587]);
    });

    test("between, hypot (racine exacte), clamp, distance", () => {
        assert.equal(between(() => 0, -4, 4), -4);
        assert.equal(between(() => 0.5, -4, 4), 0);
        assert.equal(hypot(3, 4), 5);
        assert.equal(hypot(0.1, 0.2), Math.sqrt(0.1 * 0.1 + 0.2 * 0.2));
        assert.equal(distance({ x: 1, y: 1 }, { x: 4, y: 5 }), 5);
        assert.deepEqual([clamp(-1, 0, 1), clamp(0.5, 0, 1), clamp(2, 0, 1)], [0, 0.5, 1]);
    });
});

describe("humeur, nuit, caractère", () => {
    test("le malus abaisse l'humeur, jamais sous « sad » (comme ParkHabitats.WithMalus)", () => {
        // Mêmes cas que ParkServiceTests.Malus_LowersTheMood_NeverBelowSad
        for (const [mood, levels, expected] of [["happy", 1, "normal"], ["happy", 2, "sad"], ["normal", 1, "sad"],
            ["normal", 2, "sad"], ["sad", 1, "sad"], ["happy", 0, "happy"]])
            assert.equal(loweredMood(mood, levels), expected, `${mood} − ${levels}`);
        assert.deepEqual(MOODS, ["sad", "normal", "happy"]);
        assert.equal(loweredMood("inconnue", 0), "normal"); // humeur inconnue : neutre
        assert.equal(loweredMood("inconnue", 1), "sad");
    });

    test("nuit : 0 le jour, 1 de 22 h à 6 h, progressive le soir et à l'aube", () => {
        const at = (h, m = 0) => nightLevel(new Date(2026, 10, 26, h, m));
        assert.deepEqual([at(12), at(8), at(18, 59)], [0, 0, 0]);
        assert.deepEqual([at(22), at(23, 30), at(0), at(3), at(5, 59)], [1, 1, 1, 1, 1]);
        assert.equal(at(19), 0);
        assert.equal(at(20, 30), 0.5);
        assert.equal(at(7), 0.5);
        assert.equal(at(6), 1);
    });

    test("caractère tiré de la nature (Vitesse : activité et sommeil ; Déf. Spé. : sociabilité)", () => {
        const middle = () => 0.5; // le hasard au milieu : aucun écart
        const round = p => Object.fromEntries(Object.entries(p).map(([k, v]) => [k, Math.round(v * 1000) / 1000]));
        assert.deepEqual(round(personalityOf(0, middle)), { activity: 1, sleepy: 0, sociable: 0.55, curious: 0.65 }); // Hardi : neutre
        assert.deepEqual(round(personalityOf(10, middle)), { activity: 1.3, sleepy: -0.08, sociable: 0.55, curious: 0.65 }); // Timide : + Vitesse
        assert.deepEqual(round(personalityOf(2, middle)), { activity: 0.7, sleepy: 0.12, sociable: 0.55, curious: 0.65 }); // Brave : − Vitesse
        assert.equal(round(personalityOf(20, middle)).sociable, 0.8); // Calme : + Déf. Spé.
        assert.equal(round(personalityOf(14, middle)).sociable, 0.35); // Naïf : − Déf. Spé.
        // Toutes les natures, hasard aux deux bouts : toujours dans les bornes
        for (let nature = 0; nature < 25; nature++)
            for (const r of [() => 0, () => 0.999999]) {
                const p = personalityOf(nature, r);
                assert.ok(p.activity >= 0.6 && p.activity <= 1.4 && p.sleepy >= -0.1 && p.sleepy <= 0.2
                    && p.sociable >= 0.2 && p.sociable <= 0.95 && p.curious >= 0.3 && p.curious < 1, `nature ${nature}`);
            }
    });
});

describe("sans carte : un pré sans obstacle", () => {
    const ground = new Ground(null, () => 0.5);
    test("24 × 20 cases, toutes libres, chemin en ligne droite", () => {
        assert.deepEqual([ground.columns, ground.lines, ground.width, ground.height], [24, 20, 24 * TILE, 20 * TILE]);
        assert.equal(ground.free.length, 24 * 20);
        assert.deepEqual([ground.water, ground.lava, ground.summits], [[], [], []]);
        assert.deepEqual(ground.path({ x: 10, y: 10 }, { x: 300, y: 200 }), [{ x: 300, y: 200 }]);
        assert.equal(ground.waterShare, 2 / 3);
        assert.equal(ground.habitatAt(10, 10), 0);
    });

    test("hors de la carte : rien n'est libre, aucun type", () => {
        assert.equal(ground.tileFree(-1, 0), false);
        assert.equal(ground.tileFree(24, 0), false);
        assert.equal(ground.tileFree(0, 20), false);
        assert.equal(ground.isFree(-0.1, 5), false);
        assert.deepEqual([ground.habitatOfTile(-1, 0), ground.liquidAt(-5, -5), ground.flightAt(1000, 0)], [0, 0, false]);
        // Le point libre le plus proche d'un point dehors : une case du bord
        const near = ground.nearestFree(-40, 8);
        assert.deepEqual(near, { x: TILE / 2, y: TILE / 2 });
    });

    test("endroit au hasard : le tirage donné, rien d'autre", () => {
        const a = new Ground(null, seeded(7)), b = new Ground(null, seeded(7));
        const spots = g => Array.from({ length: 50 }, () => g.randomSpot());
        assert.deepEqual(spots(a), spots(b));
        // Décalage d'au plus 4 pixels du centre d'une case
        for (const s of spots(new Ground(null, seeded(8)))) {
            const cx = Math.floor(s.x / TILE) * TILE + TILE / 2, cy = Math.floor(s.y / TILE) * TILE + TILE / 2;
            assert.ok(Math.abs(s.x - cx) <= 4 && Math.abs(s.y - cy) <= 4);
        }
        // Sans eau, un nageur qui veut l'eau reste sur la terre
        assert.ok(a.isFree(...Object.values(a.randomSpot(WATER))));
    });
});

describe("cartes des parcs", () => {
    for (const map of MAPS)
        for (const size of SIZES)
            describe(`${map}-${size}`, () => {
                const data = load(map, size);
                const ground = new Ground(data, seeded(size));
                const collisions = layer(data, "Collisions"), decors = layer(data, "Décors"), front = layer(data, "Devant");
                const { x0, y0, x1, y1, inside } = ground.areaBounds();
                const xy = i => [i % ground.columns, Math.floor(i / ground.columns)];

                test("chaque case libre est dans la zone « Parc », sans collision ; les autres cases en sont dehors", () => {
                    for (let i = 0; i < ground.walkable.length; i++) {
                        const [x, y] = xy(i);
                        if (ground.walkable[i] === LAND) assert.ok(inside(x, y) && !collisions[i], `terre en ${x}, ${y}`);
                        else if (ground.walkable[i]) assert.ok(inside(x, y) && collisions[i], `case ${ground.walkable[i]} en ${x}, ${y}`);
                        else if (inside(x, y)) assert.ok(collisions[i], `case libre oubliée en ${x}, ${y}`);
                    }
                    assert.deepEqual(ground.free, cellsOf(ground, LAND));
                    assert.ok(x0 >= 0 && y0 >= 0 && x1 <= ground.columns && y1 <= ground.lines);
                });

                test("eau et lave libres : sans objet, de leur type ou au bord, reliées à la terre", () => {
                    for (const [cells, bit, habitat] of [[ground.water, WATER, 5], [ground.lava, LAVA, 7]]) {
                        assert.deepEqual([...cells].sort((a, b) => a - b), cellsOf(ground, bit));
                        const set = new Set(cells);
                        for (const i of cells) {
                            const [x, y] = xy(i);
                            assert.ok(!decors[i] && !front[i], `objet sur le liquide en ${x}, ${y}`);
                            // Le liquide lui-même, ou son bord (écume) : une case de ce type tout autour
                            let typed = ground.habitats[i] === habitat;
                            for (let dy = -1; dy <= 1 && !typed; dy++)
                                for (let dx = -1; dx <= 1 && !typed; dx++)
                                    typed = inside(x + dx, y + dy) && ground.habitats[(y + dy) * ground.columns + x + dx] === habitat;
                            assert.ok(typed, `case ${bit} sans ${habitat} autour, en ${x}, ${y}`);
                            // Reliée par un côté à la terre ou au même liquide (pas de flaque isolée), sauf la première case
                            // de l'océan, sans terre, d'où tout part
                            const linked = [[1, 0], [-1, 0], [0, 1], [0, -1]].some(([dx, dy]) => {
                                const j = (y + dy) * ground.columns + x + dx;
                                return inside(x + dx, y + dy) && (ground.walkable[j] === LAND || set.has(j));
                            });
                            assert.ok(linked || ground.free.length === 0, `case ${bit} isolée en ${x}, ${y}`);
                        }
                    }
                    assert.deepEqual(ground.liquid, [...ground.water, ...ground.lava]);
                });

                test("sommets et survol (montagne) : seulement des cases bloquées pour la marche", () => {
                    const summits = layer(data, "Sommets"), air = layer(data, "Survol");
                    for (const i of ground.summits) assert.ok(summits[i] && collisions[i]);
                    for (const i of cellsOf(ground, AIR)) assert.ok(air[i] && collisions[i] && !summits[i]);
                    assert.equal(ground.summits.length > 0, map === "montagne-1");
                });

                // Les lignes droites du lissage sont vérifiées tous les 4 pixels (lineFree) : elles peuvent effleurer le coin
                // d'une case interdite (mesuré le 2026-11-26 : 5 % des morceaux, 1,2 pixel au plus), la marche de parc.js
                // glissant de toute façon le long. Jamais plus loin : <GRAZE> pixels au plus dans une case interdite.
                const GRAZE = 2;
                const depthIn = (x, y) => {
                    const tx = Math.floor(x / TILE) * TILE, ty = Math.floor(y / TILE) * TILE;
                    return Math.min(x - tx, tx + TILE - x, y - ty, ty + TILE - y);
                };
                test("chemins : jamais dans une case interdite (un coin effleuré au plus), pour chaque façon de se déplacer", () => {
                    const pick = seeded(1000 + size);
                    // À l'océan, seuls les nageurs vivent (ParkLandscapes.Refusal) : ses îlots ne sont reliés que par l'eau
                    const movers = map === "ocean-1" ? { nageur: PASS.nageur } : PASS;
                    for (const [who, mask] of Object.entries(movers)) {
                        const reach = who === "volant" ? LAND | SUMMIT : mask;
                        for (let n = 0; n < 15; n++) {
                            const from = ground.nearestFree(between(pick, 0, ground.width), between(pick, 0, ground.height), reach);
                            const to = ground.nearestFree(between(pick, 0, ground.width), between(pick, 0, ground.height), reach);
                            if (!ground.isFree(from.x, from.y, mask)) continue; // aucune case de ce genre (pas de terre à l'océan, taille 1)
                            const path = ground.path(from, to, mask);
                            assert.ok(path, `${who} : pas de chemin de ${JSON.stringify(from)} à ${JSON.stringify(to)}`);
                            assert.deepEqual(path.at(-1), to);
                            let at = from;
                            for (const point of path) {
                                assert.ok(ground.isFree(point.x, point.y, mask), `${who} : s'arrête sur une case interdite`);
                                assert.ok(ground.lineFree(at, point, mask));
                                for (let s = 0; s <= 1; s += 1 / 64) {
                                    const x = at.x + (point.x - at.x) * s, y = at.y + (point.y - at.y) * s;
                                    assert.ok(ground.isFree(x, y, mask) || depthIn(x, y) <= GRAZE,
                                        `${who} : traverse ${Math.floor(x / TILE)}, ${Math.floor(y / TILE)}`);
                                }
                                at = point;
                            }
                        }
                    }
                });

                test("endroits au hasard : sur une case du genre demandé", () => {
                    for (const [where, ok] of [[LAND, LAND], [WATER, WATER], [LAVA, LAVA], [LIQUID, LIQUID], [SUMMIT, SUMMIT]]) {
                        const wanted = { [LAND]: ground.free, [WATER]: ground.water, [LAVA]: ground.lava, [LIQUID]: ground.liquid,
                            [SUMMIT]: ground.summits }[where];
                        for (let n = 0; n < 20; n++) {
                            const spot = ground.randomSpot(where);
                            // Rien de ce genre : la terre, ou à défaut (l'océan sans îlot) l'eau
                            const expected = wanted.length ? ok : ground.free.length ? LAND : LIQUID;
                            assert.ok(ground.isFree(spot.x, spot.y, expected), `${where} : ${JSON.stringify(spot)}`);
                        }
                    }
                });
            });

    test("océan : sans terre dans la vue de départ, l'eau part de son centre ; les nageurs y passent leur temps", () => {
        const ground = new Ground(load("ocean-1", 1));
        assert.equal(ground.free.length, 0);
        assert.ok(ground.water.length > 0);
        assert.equal(ground.waterShare, 1);
        const big = new Ground(load("ocean-1", 4));
        assert.ok(big.free.length > 0 && big.waterShare > 2 / 3);
        assert.ok(big.liquidAt(...Object.values(big.center(big.water[0]))) === WATER);
    });

    test("plage : l'écume (le bord de la mer, de type sable) s'ouvre aux nageurs ; deux tiers de leurs promenades dans l'eau", () => {
        const ground = new Ground(load("plage-1", 2));
        assert.ok(ground.water.length > 0 && ground.free.length > 0);
        const foam = ground.water.filter(i => ground.habitats[i] !== 5);
        assert.ok(foam.length > 0, "pas d'écume ouverte : les nageurs n'entreraient pas dans la mer");
        assert.ok(foam.every(i => ground.habitats[i] === 1)); // du sable
        // Chaque nageur entre dans la mer depuis la plage : un chemin de la terre à l'eau
        const sea = ground.center(ground.water.find(i => ground.habitats[i] === 5));
        assert.ok(ground.path(ground.center(ground.free[0]), sea, LAND | WATER));
        assert.equal(ground.waterShare, Math.max(2 / 3, ground.water.length / (ground.water.length + ground.free.length)));
    });

    test("volcan : de la lave libre pour les types Feu ; le volant la survole", () => {
        const ground = new Ground(load("volcan-1", 1));
        assert.ok(ground.lava.length > 0);
        const lava = ground.center(ground.lava[0]);
        assert.equal(ground.liquidAt(lava.x, lava.y), LAVA);
        assert.ok(ground.flightAt(lava.x, lava.y));
        assert.ok(!ground.isFree(lava.x, lava.y, LAND));
        assert.equal(ground.habitatAt(lava.x, lava.y), 7);
    });

    test("montagne : un volant atteint chaque sommet depuis la vallée", () => {
        const ground = new Ground(load("montagne-1", 4));
        const start = ground.center(ground.free[0]);
        for (const i of ground.summits) {
            const summit = ground.center(i);
            assert.ok(ground.path(start, summit, PASS.volant), `sommet ${i % ground.columns}, ${Math.floor(i / ground.columns)}`);
        }
        assert.equal(ground.path(start, ground.center(ground.summits[0]), LAND), null); // à pied : non
    });
});
