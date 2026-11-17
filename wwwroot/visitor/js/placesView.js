/*
 * placesView.js - tab "Danh sách": các điểm, có GPS thì xếp gần -> xa.
 */
import { escapeHtml } from "../../assets/js/core/utils.js";
import { state, $, langName } from "./state.js";
import { t } from "./i18n.js";
import { distance } from "./mapView.js";
import { openPoi } from "./poiPanel.js";

export function renderList() {
    if (!state.bundle) return;
    const items = state.bundle.pois.map((p, i) => ({
        p, i, d: state.position ? distance(state.position.lat, state.position.lng, p.latitude, p.longitude) : null
    }));
    if (state.position) items.sort((a, b) => a.d - b.d);
    $("poiList").innerHTML = items.map(({ p, i, d }) => {
        const info = [];
        if (d !== null) info.push(Math.round(d) + " " + t("meters"));
        info.push(p.audioUrl ? t("hasAudio") : t("noAudio"));
        if (p.isFallback) info.push(langName(p.servedLang));
        return `<li data-id="${p.id}">${i + 1}. <b>${escapeHtml(p.name)}</b><br><span class="text-sm text-muted">${escapeHtml(info.join(" · "))}</span></li>`;
    }).join("");
}

export function initPlaces() {
    $("poiList").addEventListener("click", e => {
        const li = e.target.closest("li[data-id]");
        if (li) openPoi(state.bundle.pois.find(p => p.id === Number(li.dataset.id)));
    });
}
