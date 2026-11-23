// Animation d'échange (Shared/TradeAnimation.razor), à la manière de la 4e génération : envoi (le Pokémon rentre dans sa
// Poké Ball, qui s'envole), voyage, arrivée (l'autre Poké Ball descend et s'ouvre), puis chaque évolution par échange
// (silhouette blanche qui passe d'une forme à l'autre de plus en plus vite, éclair, nouvelle forme). Les textes viennent
// de la page (data-ligne) et s'écrivent lettre à lettre. Musiques : fichiers de la bande originale de HeartGold /
// SoulSilver (images/sons, hors Git), donnés par la page (data-musique-*) seulement s'ils sont sur le serveur : un
// fichier absent ne joue rien. « Passer » : la page retire l'animation et appelle stop(). Le son coupé est retenu par
// le navigateur (localStorage).

const SOUND_KEY = "echange-son";
const STOPPED = new Error("Animation arrêtée"); // rejet attendu quand on passe l'animation
const LETTER = 28; // millisecondes par lettre
const HOLD = 1100; // temps de lecture après une phrase
const SILHOUETTE = "brightness(0) invert(1) drop-shadow(0 0 6px rgba(255, 255, 255, .9))";
const VOLUME = .6;
const FANFARE_MAX = 8000; // au plus, attente de la fin de la fanfare (fichier qui ne se termine pas)

/** Lance step(item, index) sur chaque élément, chacun après la fin du précédent ; s'arrête au premier refus. */
const inTurn = (items, step) => items.reduce((done, item, i) => done.then(() => step(item, i)), Promise.resolve());

const sessions = new WeakMap(); // racine de l'animation → { abort, music }

/** Son coupé par le joueur ? */
export function muted() {
    try {
        return localStorage.getItem(SOUND_KEY) === "off";
    } catch {
        return false;
    }
}

/** Coupe ou remet le son (et le retient pour les prochains échanges). */
export function setMuted(root, value) {
    try {
        if (value) localStorage.setItem(SOUND_KEY, "off");
        else localStorage.removeItem(SOUND_KEY);
    } catch {
        // stockage refusé : le réglage ne vaut que pour cette animation
    }
    sessions.get(root)?.music.setMuted(value);
}

/** Joue l'animation dans root ; dotnet.Ended() à la fin. */
export async function play(root, dotnet) {
    stop(root);
    const abort = new AbortController();
    const music = new Music(root, muted());
    sessions.set(root, { abort, music });
    try {
        await run(root, abort.signal, music);
        await dotnet.invokeMethodAsync("Ended");
    } catch (e) {
        if (e !== STOPPED) throw e;
    }
}

/** Arrête l'animation et le son (« Passer », page quittée). */
export function stop(root) {
    const session = sessions.get(root);
    if (!session) return;
    sessions.delete(root);
    session.abort.abort();
    session.music.close();
}

// ---------- Déroulé ----------

async function run(root, signal, music) {
    const $ = selector => root.querySelector(selector);
    const lines = key => [...root.querySelectorAll(`[data-ligne="${key}"]`)].map(l => l.textContent.trim());
    const screen = $(".trade-show-screen");
    const text = $(".trade-show-text");
    const ball = $(".trade-show-ball");
    const flash = $(".trade-show-flash");
    const sent = $('[data-acteur="envoye"]');
    const received = $('[data-acteur="recu"]');

    const wait = ms => new Promise((resolve, reject) => {
        if (signal.aborted) return reject(STOPPED);
        const timer = setTimeout(resolve, ms);
        signal.addEventListener("abort", () => { clearTimeout(timer); reject(STOPPED); }, { once: true });
    });
    const animate = (element, keyframes, options) => {
        if (signal.aborted) return Promise.reject(STOPPED);
        const animation = element.animate(keyframes, { fill: "forwards", ...options });
        signal.addEventListener("abort", () => animation.cancel(), { once: true });
        return animation.finished.catch(() => { throw STOPPED; });
    };
    const say = async line => {
        text.textContent = "";
        text.classList.add("trade-show-text-on");
        await inTurn(Array.from({ length: line.length }), (_, i) => {
            text.textContent = line.slice(0, i + 1);
            return wait(LETTER);
        });
        await wait(HOLD);
    };
    const sayAll = part => inTurn(lines(part), say);
    const show = (element, visible = true) => element.classList.toggle("trade-show-visible", visible);

    // 1. Envoi : le Pokémon apparaît, dit au revoir, rentre dans sa Poké Ball, qui s'envole.
    music.play(music.trade);
    show(sent);
    await animate(sent, [{ opacity: 0, transform: "scale(.6)" }, { opacity: 1, transform: "scale(1)" }], { duration: 500, easing: "ease-out" });
    await sayAll("envoi");
    await animate(sent, [{ filter: "none" }, { filter: SILHOUETTE }], { duration: 350 });
    show(ball);
    await Promise.all([
        animate(sent, [{ transform: "scale(1)", opacity: 1 }, { transform: "scale(0)", opacity: 0 }], { duration: 450, easing: "ease-in" }),
        animate(ball, [{ transform: "scale(0)" }, { transform: "scale(1.15)" }, { transform: "scale(1)" }], { duration: 450 }),
    ]);
    show(sent, false);
    await animate(ball, [{ rotate: "0deg" }, { rotate: "-18deg" }, { rotate: "14deg" }, { rotate: "0deg" }], { duration: 450 });
    text.classList.remove("trade-show-text-on");
    await animate(ball, [{ transform: "translateY(0) scale(1)" }, { transform: "translateY(-420%) scale(.7)" }], { duration: 650, easing: "cubic-bezier(.5, 0, .9, .5)" });

    // 2. Voyage : un temps sans rien (le fond ne bouge pas), puis la Poké Ball de l'autre arrive d'en haut.
    await wait(1600);
    await animate(ball, [{ transform: "translateY(-420%) scale(.7)" }, { transform: "translateY(0) scale(1)" }], { duration: 700, easing: "cubic-bezier(.2, .6, .4, 1)" });
    await animate(ball, [{ transform: "translateY(0)" }, { transform: "translateY(-40%)" }, { transform: "translateY(0)" },
        { transform: "translateY(-15%)" }, { transform: "translateY(0)" }], { duration: 700, easing: "ease-in-out" });
    await wait(250);

    // 3. Arrivée : la Poké Ball s'ouvre dans un éclair, le Pokémon reçu sort en silhouette puis prend ses couleurs.
    await animate(flash, [{ opacity: 0 }, { opacity: .85 }], { duration: 160 });
    show(ball, false);
    show(received);
    received.style.filter = SILHOUETTE;
    await Promise.all([
        animate(flash, [{ opacity: .85 }, { opacity: 0 }], { duration: 450 }),
        animate(received, [{ transform: "scale(0)" }, { transform: "scale(1.1)" }, { transform: "scale(1)" }], { duration: 500, easing: "ease-out" }),
    ]);
    await animate(received, [{ filter: SILHOUETTE }, { filter: "none" }], { duration: 400 });
    received.style.filter = "";
    await sayAll("arrivee");

    // 4. Évolutions par échange ; sans évolution, le thème s'éteint doucement.
    const evolutions = [];
    for (let actor = $('[data-acteur="evolution-0"]'); actor; actor = $(`[data-acteur="evolution-${evolutions.length}"]`))
        evolutions.push(actor);
    if (evolutions.length === 0) music.fadeOut(music.trade, 1.5);
    await inTurn(evolutions, evolve);
    text.classList.add("trade-show-text-on");

    async function evolve(actor, i) {
        const before = actor.querySelector('[data-forme="avant"]');
        const after = actor.querySelector('[data-forme="apres"]');
        // Le Pokémon déjà à l'écran laisse la place à celui qui évolue (l'envoyé, en échange interne, revient d'abord).
        [sent, received, ...root.querySelectorAll('[data-acteur^="evolution-"]')].forEach(a => show(a, false));
        before.style.display = "";
        after.style.display = "none";
        before.style.filter = after.style.filter = "";
        show(actor);
        // La musique de l'échange laisse la place à celle de l'évolution, reprise du début à chaque évolution.
        music.fadeOut(music.trade, .5);
        music.play(music.evolution);
        await animate(actor, [{ opacity: 0 }, { opacity: 1 }], { duration: 300 });
        await sayAll(`evolution-${i}-debut`);

        screen.classList.add("trade-show-evolving");
        text.classList.remove("trade-show-text-on");
        await animate(before, [{ filter: "none" }, { filter: SILHOUETTE }], { duration: 900 });
        before.style.filter = after.style.filter = SILHOUETTE;

        // Les deux silhouettes alternent, de plus en plus vite.
        const changes = [];
        for (let d = 620; d > 45; d *= .82) changes.push(d);
        for (let n = 0; n < 14; n++) changes.push(45);
        await inTurn(changes, (d, n) => {
            const showAfter = n % 2 === 0; // la nouvelle forme d'abord, puis l'ancienne, et ainsi de suite
            before.style.display = showAfter ? "none" : "";
            after.style.display = showAfter ? "" : "none";
            const shown = showAfter ? after : before;
            shown.animate([{ transform: "scale(.92)" }, { transform: "scale(1)" }], { duration: Math.min(d, 200) });
            return wait(d);
        });

        // Éclair, nouvelle forme.
        await animate(flash, [{ opacity: 0 }, { opacity: 1 }], { duration: 250 });
        before.style.display = "none";
        after.style.display = "";
        after.style.filter = "";
        screen.classList.remove("trade-show-evolving");
        music.fadeOut(music.evolution, .2);
        const fanfare = music.fanfare();
        await Promise.all([
            animate(flash, [{ opacity: 1 }, { opacity: 0 }], { duration: 900, easing: "ease-out" }),
            animate(after, [{ transform: "scale(1.12)" }, { transform: "scale(1)" }], { duration: 900, easing: "ease-out" }),
        ]);
        await sayAll(`evolution-${i}-fin`);
        await Promise.race([fanfare, wait(FANFARE_MAX)]); // le message reste jusqu'à la fin de la fanfare
        await wait(400);
    }
}

// ---------- Musiques (fichiers) ----------

class Music {
    /** Les fichiers que la page donne (data-musique-*) ; ceux qui manquent restent null et ne jouent rien. */
    constructor(root, muted) {
        this.muted = muted;
        this.fades = new Map(); // audio → minuterie du fondu en cours
        const load = (url, loop) => {
            if (!url) return null;
            const audio = new Audio(url);
            audio.preload = "auto";
            audio.loop = loop;
            audio.muted = muted;
            return audio;
        };
        this.trade = load(root.dataset.musiqueEchange, true);
        this.evolution = load(root.dataset.musiqueEvolution, true);
        this.congratulations = load(root.dataset.musiqueFelicitations, false);
    }

    all() {
        return [this.trade, this.evolution, this.congratulations].filter(Boolean);
    }

    setMuted(value) {
        this.muted = value;
        this.all().forEach(audio => audio.muted = value);
    }

    /** Joue un fichier depuis son début ; promesse : true s'il a démarré (le navigateur peut refuser le son). */
    play(audio) {
        if (!audio) return Promise.resolve(false);
        this.stopFade(audio);
        audio.currentTime = 0;
        audio.volume = VOLUME;
        audio.muted = this.muted;
        return audio.play().then(() => true, () => false);
    }

    /** Baisse le son jusqu'au silence en `seconds` secondes, puis l'arrête. */
    fadeOut(audio, seconds) {
        if (!audio || audio.paused) return;
        this.stopFade(audio);
        const start = audio.volume;
        const t0 = performance.now();
        const timer = setInterval(() => {
            const k = (performance.now() - t0) / (seconds * 1000);
            if (k < 1) {
                audio.volume = start * (1 - k);
                return;
            }
            this.stopFade(audio);
            audio.pause();
            audio.volume = VOLUME;
        }, 40);
        this.fades.set(audio, timer);
    }

    stopFade(audio) {
        clearInterval(this.fades.get(audio));
        this.fades.delete(audio);
    }

    /** Fanfare de la nouvelle forme ; promesse résolue à sa fin (tout de suite sans fichier, ou si le son est refusé). */
    fanfare() {
        const audio = this.congratulations;
        if (!audio) return Promise.resolve();
        return new Promise(resolve => {
            audio.addEventListener("ended", resolve, { once: true });
            this.play(audio).then(started => { if (!started) resolve(); });
        });
    }

    close() {
        for (const audio of this.all()) {
            this.stopFade(audio);
            audio.pause();
            audio.removeAttribute("src");
            audio.load(); // libère le fichier
        }
    }
}
