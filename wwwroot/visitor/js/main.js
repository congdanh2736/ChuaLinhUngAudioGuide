/*
 * main.js - điểm khởi động app du khách.
 * Luồng: mua vé / nhập mã -> nhận token -> tải TOÀN BỘ dữ liệu 1 lần (bundle) -> bản đồ + GPS
 *        -> tới gần điểm nào thì tự phát thuyết minh (geofence, xem mapView.js).
 */
import { visitorApi } from "../../assets/js/api/visitorApi.js";
import { STORAGE_KEYS } from "../../assets/js/core/config.js";
import { escapeHtml, storage } from "../../assets/js/core/utils.js";
import { state, saveToken, $, showMsg } from "./state.js";
import { loadStrings, t, applyI18n } from "./i18n.js";
import { initMap, renderMarkers, startGps, renderGpsStatus, invalidateMapSize } from "./mapView.js";
import { initPanel, refreshPanel, hidePanel } from "./poiPanel.js";
import { renderList, initPlaces } from "./placesView.js";
import { renderTours, initRoute, clearRoute } from "./routeView.js";
import { initChat, greet } from "./chatView.js";

let pollId = 0;
let gpsStarted = false;

// ===== Mua vé / nhập mã =====
function showGate(message = "") {
    $("app").classList.add("hidden");
    $("gate").classList.remove("hidden");
    $("gateMsg").textContent = message;
}

// Token hết hạn / bị thu hồi -> quay lại màn hình nhập mã
export function onUnauthorized() {
    saveToken(null);
    hidePanel();
    showGate(t("expired"));
}

async function loadAccessInfo() {
    try {
        const info = await visitorApi.accessInfo();
        $("price").textContent = info.priceVnd.toLocaleString("vi-VN") + " đ";
        $("hours").textContent = info.accessHours;
        return info;
    } catch {
        return null;
    }
}

// POST /api/access/token -> AccessService.RedeemAsync: mã hợp lệ -> kích hoạt phiên + cấp token du khách
async function redeem(code, attempt = 1) {
    $("gateMsg").textContent = t("processing");
    try {
        const data = await visitorApi.redeem(code);
        saveToken(data.token);
        history.replaceState(null, "", "/");
        await startApp();
    } catch (err) {
        // Thanh toán online: kết quả từ cổng có thể về chậm -> thử lại vài lần
        if (err.code === "PAYMENT_PENDING" && attempt < 6) {
            $("gateMsg").textContent = t("paymentPending");
            setTimeout(() => redeem(code, attempt + 1), 2000);
            return;
        }
        $("gateMsg").textContent = err.message;
    }
}

// ===== Tải dữ liệu: tải hết 1 lần, lần sau gửi ETag, không đổi thì server trả 304 =====
async function loadBundle() {
    const cacheKey = "bundle_" + state.lang;
    const cached = storage.get(cacheKey);
    try {
        const response = await visitorApi.bundle(state.lang, cached?.etag);
        state.pendingTranslations = Number(response.headers.get("X-Translation-Pending") || 0);
        if (response.status === 304 && cached) {
            state.bundle = cached.data;
        } else {
            state.bundle = await response.json();
            storage.set(cacheKey, { etag: response.headers.get("ETag"), data: state.bundle });
        }
    } catch (err) {
        if (err.status === 401) {
            onUnauthorized();
            throw err;
        }
        if (!cached) throw err;
        state.bundle = cached.data; // mất mạng -> dùng bản đã lưu
        showMsg("Offline");
    }
    state.languages = state.bundle.languages;
    fillLanguageSelects();
    renderMarkers();
    renderList();
    renderTours();
    refreshPanel();
}

// Server đang dịch ở nền -> 4 giây tải lại 1 lần cho tới khi xong (tối đa 2 phút)
async function waitForTranslations() {
    const myId = ++pollId;
    if (!state.pendingTranslations) return;
    showMsg(t("translating"), 0);
    for (let i = 0; i < 30 && state.pendingTranslations > 0; i++) {
        await new Promise(r => setTimeout(r, 4000));
        if (myId !== pollId) return; // đã đổi ngôn ngữ khác
        try {
            await loadBundle();
        } catch {
            return;
        }
    }
    if (myId !== pollId) return;
    showMsg(state.pendingTranslations ? t("fallbackNote") + " English / Tiếng Việt" : t("translated"));
}

// ===== Ngôn ngữ =====
function fillLanguageSelects() {
    const html = state.languages.map(l => `<option value="${escapeHtml(l.code)}">${escapeHtml(l.nativeName)}</option>`).join("");
    document.querySelectorAll(".lang-select").forEach(sel => {
        sel.innerHTML = html;
        sel.value = state.lang;
    });
}

function applyLanguage() {
    applyI18n();
    renderGpsStatus();
    renderList();
    renderTours();
}

async function changeLang(code) {
    if (code === state.lang) return;
    state.lang = code;
    storage.set(STORAGE_KEYS.LANG, code);
    document.querySelectorAll(".lang-select").forEach(sel => (sel.value = code));
    hidePanel();
    clearRoute();
    const inApp = state.token && !$("app").classList.contains("hidden");
    if (inApp) showMsg(t("processing"), 0);
    await loadStrings(code);         // chữ trên nút / tab
    if (state.lang !== code) return;
    applyLanguage();
    if (inApp) {
        await loadBundle();          // server tự xếp hàng dịch những điểm còn thiếu
        waitForTranslations();
        if (!state.pendingTranslations) showMsg(t("translated"));
    }
}

// ===== Tab =====
function initTabs() {
    document.querySelectorAll(".v-tabs button").forEach(btn => {
        btn.addEventListener("click", () => {
            document.querySelectorAll(".v-tabs button").forEach(b => b.classList.toggle("is-active", b === btn));
            document.querySelectorAll(".v-view").forEach(v => v.classList.toggle("is-active", v.id === "view-" + btn.dataset.view));
            if (btn.dataset.view === "map") invalidateMapSize();
        });
    });
}

async function startApp() {
    $("gate").classList.add("hidden");
    $("app").classList.remove("hidden");
    initMap();
    invalidateMapSize();
    try {
        await loadBundle();
    } catch (err) {
        if (err.status !== 401) showMsg(err.message);
        return;
    }
    waitForTranslations();
    greet();
    if (!gpsStarted) {
        gpsStarted = true;
        startGps();
    }
}

async function init() {
    initTabs();
    initPanel();
    initPlaces();
    initRoute();
    initChat();
    $("chkAutoplay").checked = state.autoplay;
    $("chkAutoplay").addEventListener("change", e => {
        state.autoplay = e.target.checked;
        storage.set(STORAGE_KEYS.AUTOPLAY, state.autoplay);
    });
    document.querySelectorAll(".lang-select").forEach(sel => sel.addEventListener("change", () => changeLang(sel.value)));
    $("redeemForm").addEventListener("submit", e => {
        e.preventDefault();
        const code = $("codeInput").value.trim().toUpperCase();
        if (code.length >= 4) redeem(code);
    });
    // POST /api/access/online -> tạo giao dịch -> chuyển sang trang cổng thanh toán (giả lập)
    $("btnPayOnline").addEventListener("click", async () => {
        try {
            const data = await visitorApi.startOnlinePayment();
            location.href = data.paymentUrl;
        } catch (err) {
            $("gateMsg").textContent = err.message;
        }
    });

    await loadStrings(state.lang);
    applyI18n();
    try {
        state.languages = await visitorApi.languages();
    } catch {
        state.languages = [{ code: "vi", nativeName: "Tiếng Việt" }, { code: "en", nativeName: "English" }];
    }
    fillLanguageSelects();

    const info = await loadAccessInfo();
    const params = new URLSearchParams(location.search);
    // Cổng thanh toán chuyển về kèm mã -> tự đổi mã lấy token
    if (params.get("payment") === "success" && params.get("code")) {
        showGate();
        $("codeInput").value = params.get("code");
        return redeem(params.get("code"));
    }
    if (params.get("payment") === "failed") {
        history.replaceState(null, "", "/");
        return showGate(t("paymentFailed"));
    }
    if (state.token || (info && !info.accessRequired)) return startApp();
    showGate();
}

init();
