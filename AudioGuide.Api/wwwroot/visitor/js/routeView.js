/*
 * routeView.js - tab "Lộ trình": POST /api/content/tours/recommend
 * -> TourService.RecommendAsync: sắp thứ tự "láng giềng gần nhất" + tính quãng đường bằng Haversine.
 */
import { visitorApi } from "../../assets/js/api/visitorApi.js";
import { DEFAULT_CENTER } from "../../assets/js/core/config.js";
import { escapeHtml } from "../../assets/js/core/utils.js";
import { state, $, showMsg } from "./state.js";
import { t } from "./i18n.js";
import { getMap } from "./mapView.js";
import { openPoi } from "./poiPanel.js";

let routeLine = null;

export function renderTours() {
    if (!state.bundle) return;
    $("tourSelect").innerHTML = `<option value="">${escapeHtml(t("autoPick"))}</option>`
        + state.bundle.tours.map(tour => `<option value="${tour.id}">${escapeHtml(tour.name)} (${tour.estimatedMinutes} ${escapeHtml(t("minutes"))})</option>`).join("");
}

export function clearRoute() {
    $("routeSummary").textContent = "";
    $("routeList").innerHTML = "";
    routeLine?.remove();
    routeLine = null;
}

async function recommend() {
    // Chưa có GPS thì lấy điểm đầu tiên (cổng chùa) làm điểm xuất phát
    const first = state.bundle.pois[0];
    const start = state.position || { lat: first ? first.latitude : DEFAULT_CENTER[0], lng: first ? first.longitude : DEFAULT_CENTER[1] };
    const tourId = $("tourSelect").value ? Number($("tourSelect").value) : null;
    try {
        const data = await visitorApi.recommend({ latitude: start.lat, longitude: start.lng, lang: state.lang, tourId });
        $("routeSummary").textContent = `${t("total")}: ${Math.round(data.totalDistanceM)} ${t("meters")}, ~${Math.round(data.totalMinutes)} ${t("minutes")}`;
        $("routeList").innerHTML = data.stops.map(s =>
            `<li data-id="${s.poi.id}"><b>${escapeHtml(s.poi.name)}</b><br><span class="text-sm text-muted">${escapeHtml(t("walk"))} ${Math.round(s.walkDistanceM)} ${escapeHtml(t("meters"))} (${s.walkMinutes} ${escapeHtml(t("minutes"))}), ${escapeHtml(t("listen"))} ${s.listenMinutes} ${escapeHtml(t("minutes"))}</span></li>`).join("");
        routeLine?.remove();
        const pts = [[start.lat, start.lng], ...data.stops.map(s => [s.poi.latitude, s.poi.longitude])];
        routeLine = L.polyline(pts, { color: "#2b7bb9", weight: 3 }).addTo(getMap());
    } catch (err) {
        showMsg(err.message);
    }
}

export function initRoute() {
    $("btnRecommend").addEventListener("click", recommend);
    $("routeList").addEventListener("click", e => {
        const li = e.target.closest("li[data-id]");
        if (li) openPoi(state.bundle.pois.find(p => p.id === Number(li.dataset.id)));
    });
}
