/*
 * mapView.js - bản đồ (Leaflet + OpenStreetMap), GPS và GEOFENCE.
 *
 * Geofence = "hàng rào ảo" quanh mỗi điểm (bán kính TriggerRadiusM do admin đặt).
 * Cứ 3 giây kiểm tra 1 lần: khoảng cách (Haversine) từ vị trí hiện tại tới điểm <= bán kính
 * và ở trong vùng >= 3 giây (tránh GPS nhảy) -> tự mở + phát thuyết minh. 1 điểm đã phát thì 5 phút sau mới tự phát lại.
 */
import { DEFAULT_CENTER, MAP_TILE_URL } from "../../assets/js/core/config.js";
import { state, $, showMsg } from "./state.js";
import { t } from "./i18n.js";
import { openPoi, prepareAndPlay, needsLocalization, isAudioPlaying, sendEvent } from "./poiPanel.js";
import { renderList } from "./placesView.js";

const DEBOUNCE_MS = 3000;          // ở trong vùng >= 3 giây mới tính là đã tới
const COOLDOWN_MS = 5 * 60 * 1000; // 1 điểm đã phát thì 5 phút sau mới tự phát lại
const CHECK_EVERY_MS = 3000;

let map = null;
let poiLayer = null;
let userMarker = null;
let gpsStatus = null;
const enteredAt = {};  // poiId -> lúc bắt đầu vào vùng
const lastPlayed = {}; // poiId -> lúc phát gần nhất

// Khoảng cách 2 tọa độ (mét) - công thức Haversine, giống Services/Logic/Geo.cs ở backend
export function distance(lat1, lng1, lat2, lng2) {
    const R = 6371000, rad = Math.PI / 180;
    const dLat = (lat2 - lat1) * rad, dLng = (lng2 - lng1) * rad;
    const a = Math.sin(dLat / 2) ** 2 + Math.cos(lat1 * rad) * Math.cos(lat2 * rad) * Math.sin(dLng / 2) ** 2;
    return 2 * R * Math.asin(Math.sqrt(a));
}

export function getMap() {
    return map;
}

export function initMap() {
    if (map) return;
    map = L.map("map").setView(DEFAULT_CENTER, 18);
    L.tileLayer(MAP_TILE_URL, { maxZoom: 20, maxNativeZoom: 19, attribution: "© OpenStreetMap" }).addTo(map);
    poiLayer = L.layerGroup().addTo(map);
    map.on("click", e => {
        if (state.simulate) updatePosition(e.latlng.lat, e.latlng.lng, "sim");
    });
    $("btnLocate").addEventListener("click", () => {
        if (state.position) map.setView([state.position.lat, state.position.lng], 19);
        else showMsg(t("locating"));
    });
    $("chkSimulate").addEventListener("change", e => {
        state.simulate = e.target.checked;
        if (state.simulate) showMsg(t("simulateOn"));
    });
}

export function invalidateMapSize() {
    setTimeout(() => map?.invalidateSize(), 50);
}

export function renderMarkers() {
    poiLayer.clearLayers();
    const bounds = [];
    state.bundle.pois.forEach((poi, i) => {
        const ll = [poi.latitude, poi.longitude];
        bounds.push(ll);
        L.circle(ll, { radius: poi.triggerRadiusM, color: "#9a3412", weight: 1, fillOpacity: 0.1 }).addTo(poiLayer);
        const icon = L.divIcon({ className: "poi-num", html: String(i + 1), iconSize: [22, 22] });
        L.marker(ll, { icon, title: poi.name }).on("click", () => openPoi(poi)).addTo(poiLayer);
    });
    if (bounds.length && !state.position) map.fitBounds(bounds, { padding: [30, 30], maxZoom: 18 });
}

// ===== GPS =====
export function startGps() {
    if (!navigator.geolocation) {
        setGpsStatus("gpsDenied");
        return;
    }
    setGpsStatus("locating");
    navigator.geolocation.watchPosition(
        pos => { if (!state.simulate) updatePosition(pos.coords.latitude, pos.coords.longitude, "gps", pos.coords.accuracy); },
        () => { if (!state.position) setGpsStatus("gpsDenied"); },
        { enableHighAccuracy: true, maximumAge: 5000, timeout: 15000 }
    );
    setInterval(checkGeofence, CHECK_EVERY_MS);
}

function updatePosition(lat, lng, source, accuracy) {
    state.position = { lat, lng };
    if (!userMarker) {
        userMarker = L.circleMarker([lat, lng], { radius: 7, color: "#fff", weight: 2, fillColor: "#2b7bb9", fillOpacity: 1 }).addTo(map);
    } else {
        userMarker.setLatLng([lat, lng]);
    }
    if (source === "sim") setGpsStatus("simulateOn");
    else setGpsStatus(accuracy ? "gpsAccuracy" : null, accuracy);
    checkGeofence();
    renderList();
}

function setGpsStatus(key, accuracy) {
    gpsStatus = key ? { key, accuracy } : null;
    renderGpsStatus();
}

export function renderGpsStatus() {
    let text = "";
    if (gpsStatus) {
        text = t(gpsStatus.key);
        if (gpsStatus.key === "gpsAccuracy") text += " " + Math.round(gpsStatus.accuracy) + " " + t("meters");
    }
    $("gpsStatus").textContent = text;
}

// ===== Geofence =====
function checkGeofence() {
    if (!state.position || !state.bundle) return;
    const now = Date.now();
    const inside = [];
    for (const poi of state.bundle.pois) {
        const d = distance(state.position.lat, state.position.lng, poi.latitude, poi.longitude);
        if (d <= poi.triggerRadiusM) {
            if (!enteredAt[poi.id]) enteredAt[poi.id] = now;
            inside.push({ poi, d });
        } else {
            delete enteredAt[poi.id];
        }
    }
    // Nhiều vùng chồng nhau: điểm ưu tiên cao hơn trước, bằng nhau thì điểm gần hơn
    inside.sort((a, b) => b.poi.priority - a.poi.priority || a.d - b.d);
    for (const { poi } of inside) {
        const stayedLongEnough = state.simulate || now - enteredAt[poi.id] >= DEBOUNCE_MS;
        const notRecent = !lastPlayed[poi.id] || now - lastPlayed[poi.id] > COOLDOWN_MS;
        if (stayedLongEnough && notRecent && !isAudioPlaying() && !state.preparingPoiId) {
            lastPlayed[poi.id] = now;
            sendEvent(poi.id, "GeofenceEnter");
            showMsg(t("near") + ": " + poi.name);
            // Chưa có bản dịch đúng ngôn ngữ / chưa có audio -> nhờ server dịch ngay rồi mới phát
            if (state.autoplay && needsLocalization(poi)) prepareAndPlay(poi);
            else openPoi(poi, { autoplay: state.autoplay });
            break;
        }
    }
}
