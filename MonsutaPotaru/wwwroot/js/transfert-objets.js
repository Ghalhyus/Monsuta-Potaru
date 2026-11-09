// Transfert d'inventaire (Pages/InventoryTransfer.razor) : glisser un objet d'une colonne (une sauvegarde) et le lâcher sur
// l'autre. Événements « pointeur » : à la souris, le glisser commence après quelques pixels ; au doigt, après un appui long
// (sans bouger), pour que la liste défile toujours normalement. Le serveur décide du reste (Drop : combien, et si l'objet
// peut aller dans l'autre sauvegarde) ; ici, seulement l'image qui suit le pointeur, la colonne d'arrivée en surbrillance et
// le défilement de la page près de ses bords (colonnes l'une sous l'autre sur un téléphone).

const MOUSE_THRESHOLD = 6; // pixels à parcourir à la souris avant de glisser (un simple clic ne glisse pas)
const LONG_PRESS = 350; // millisecondes d'appui au doigt avant de glisser
const TOUCH_TOLERANCE = 10; // pixels de tremblement permis pendant l'appui long (au-delà : la liste défile)
const EDGE = 60; // pixels près du haut ou du bas de la fenêtre où la page défile toute seule
const SCROLL_STEP = 14;

/** Branche le glisser-déposer sur la page (root) ; dotnet reçoit Drop(colonne de départ, numéro de l'objet). */
export function init(root, dotnet) {
    let pressed = null; // appui en cours sur un objet : { item, side, itemId, x, y, pointerId, touch, timer }
    let drag = null; // glisser en cours : { ghost, dx, dy, x, y, over }
    let scrolling = 0; // requestAnimationFrame du défilement automatique

    const columnAt = (x, y) => document.elementFromPoint(x, y)?.closest?.(".transfer-column[data-side]") ?? null;
    const otherColumns = side => root.querySelectorAll(`.transfer-column[data-side]:not([data-side="${side}"])`);

    function begin() {
        const { item, x, y } = pressed;
        const box = item.getBoundingClientRect();
        // Image qui suit le pointeur : la vignette sans son compteur, avec le nombre envoyé.
        const ghost = item.cloneNode(true);
        ghost.querySelector(".transfer-actions")?.remove();
        ghost.classList.add("transfer-ghost");
        ghost.removeAttribute("title");
        const count = item.querySelector("input[type=number]")?.value;
        if (count) {
            const badge = document.createElement("span");
            badge.className = "badge bg-primary";
            badge.textContent = `→ ${count}`;
            ghost.append(badge);
        }
        ghost.style.width = `${box.width}px`;
        document.body.append(ghost);
        drag = { ghost, dx: x - box.left, dy: y - box.top, x, y, over: null };
        item.classList.add("transfer-item-dragged");
        root.classList.add("transfer-dragging");
        otherColumns(pressed.side).forEach(c => c.classList.add("transfer-drop-target"));
        place(x, y);
        if (pressed.touch) navigator.vibrate?.(15);
        scrolling = requestAnimationFrame(autoScroll);
    }

    function place(x, y) {
        drag.x = x;
        drag.y = y;
        drag.ghost.style.transform = `translate(${x - drag.dx}px, ${y - drag.dy}px)`;
        const column = columnAt(x, y);
        const over = column && column.dataset.side !== pressed.side ? column : null;
        if (over !== drag.over) {
            drag.over?.classList.remove("transfer-drop-hover");
            over?.classList.add("transfer-drop-hover");
            drag.over = over;
        }
    }

    function autoScroll() {
        if (!drag) return;
        if (drag.y < EDGE) window.scrollBy(0, -SCROLL_STEP);
        else if (drag.y > window.innerHeight - EDGE) window.scrollBy(0, SCROLL_STEP);
        place(drag.x, drag.y); // la colonne sous le pointeur a pu changer en défilant
        scrolling = requestAnimationFrame(autoScroll);
    }

    /** Fin de l'appui : lâché sur l'autre colonne (drop), ou abandonné. */
    function end(drop) {
        if (pressed?.timer) clearTimeout(pressed.timer);
        if (drag) {
            cancelAnimationFrame(scrolling);
            drag.ghost.remove();
            drag.over?.classList.remove("transfer-drop-hover");
            pressed.item.classList.remove("transfer-item-dragged");
            root.classList.remove("transfer-dragging");
            root.querySelectorAll(".transfer-drop-target").forEach(c => c.classList.remove("transfer-drop-target"));
            if (drop && drag.over) dotnet.invokeMethodAsync("Drop", pressed.side, pressed.itemId);
        }
        pressed = null;
        drag = null;
    }

    function onPointerDown(e) {
        if (pressed || (e.pointerType === "mouse" && e.button !== 0)) return;
        const item = e.target.closest(".transfer-item[data-draggable=true]");
        // Les boutons et le champ du compteur gardent leur rôle.
        if (!item || !root.contains(item) || e.target.closest("button, input, select, a")) return;
        const column = item.closest(".transfer-column[data-side]");
        if (!column) return;
        const touch = e.pointerType !== "mouse";
        pressed = { item, side: column.dataset.side, itemId: Number(item.dataset.item), x: e.clientX, y: e.clientY, pointerId: e.pointerId, touch, timer: 0 };
        if (touch) pressed.timer = setTimeout(() => { if (pressed && !drag) begin(); }, LONG_PRESS);
        else e.preventDefault(); // pas de sélection de texte
    }

    function onPointerMove(e) {
        if (!pressed || e.pointerId !== pressed.pointerId) return;
        if (drag) {
            e.preventDefault();
            place(e.clientX, e.clientY);
            return;
        }
        const distance = Math.hypot(e.clientX - pressed.x, e.clientY - pressed.y);
        if (pressed.touch) {
            if (distance > TOUCH_TOLERANCE) end(false); // le doigt fait défiler la liste
        } else if (distance > MOUSE_THRESHOLD) {
            begin();
            place(e.clientX, e.clientY);
        }
    }

    const onPointerUp = e => { if (pressed && e.pointerId === pressed.pointerId) end(true); };
    const onPointerCancel = e => { if (pressed && e.pointerId === pressed.pointerId) end(false); };
    // Au doigt, pendant le glisser, la page ne défile pas (elle défile toute seule près des bords).
    const onTouchMove = e => { if (drag) e.preventDefault(); };
    // Pas de menu de l'appui long, ni de glisser des images par le navigateur.
    const onContextMenu = e => { if (pressed || drag) e.preventDefault(); };
    const onDragStart = e => { if (e.target instanceof Element && e.target.closest(".transfer-item")) e.preventDefault(); };
    const onKeyDown = e => { if (e.key === "Escape" && drag) end(false); };

    root.addEventListener("pointerdown", onPointerDown);
    root.addEventListener("dragstart", onDragStart);
    document.addEventListener("pointermove", onPointerMove, { passive: false });
    document.addEventListener("pointerup", onPointerUp);
    document.addEventListener("pointercancel", onPointerCancel);
    document.addEventListener("touchmove", onTouchMove, { passive: false });
    document.addEventListener("contextmenu", onContextMenu);
    document.addEventListener("keydown", onKeyDown);

    return {
        dispose() {
            end(false);
            root.removeEventListener("pointerdown", onPointerDown);
            root.removeEventListener("dragstart", onDragStart);
            document.removeEventListener("pointermove", onPointerMove);
            document.removeEventListener("pointerup", onPointerUp);
            document.removeEventListener("pointercancel", onPointerCancel);
            document.removeEventListener("touchmove", onTouchMove);
            document.removeEventListener("contextmenu", onContextMenu);
            document.removeEventListener("keydown", onKeyDown);
        },
    };
}
