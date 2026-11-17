/*
 * poiPanel.js - bảng thông tin 1 điểm: tên, nội dung, audio, giờ mở cửa, vé, tiện ích, khung giờ đông khách.
 *   Audio: file mp3 do server tạo (Edge-TTS). Chưa có audio -> đọc bằng giọng máy của trình duyệt (speechSynthesis).
 *   Trạng thái mở cửa + đông khách: GET /api/content/pois/{id}/insights (đổi theo giờ nên không nằm trong bundle).
 */
import { visitorApi } from "../../assets/js/api/visitorApi.js";
import { escapeHtml } from "../../assets/js/core/utils.js";
import { state, $, showMsg, langName } from "./state.js";
import { t } from "./i18n.js";
import { onUnauthorized } from "./main.js";

const SPEECH_LANG = {
    vi: "vi-VN", en: "en-US", zh: "zh-CN", ja: "ja-JP", ko: "ko-KR", fr: "fr-FR", de: "de-DE", es: "es-ES",
    ru: "ru-RU", th: "th-TH", it: "it-IT", pt: "pt-BR", id: "id-ID", ms: "ms-MY", hi: "hi-IN", ar: "ar-SA"
};
const audio = $("audio");
let playEventSent = false;
let insights = null;    // trạng thái mở cửa + đông khách của điểm đang mở
let popularDay = null;  // ngày đang xem trong biểu đồ đông khách

export function sendEvent(poiId, type) {
    if (!state.token) return;
    visitorApi.event(poiId, type, state.lang).catch(() => { /* thống kê lỗi không ảnh hưởng du khách */ });
}

export function isAudioPlaying() {
    return !audio.paused;
}

// Điểm chưa có bản dịch đúng ngôn ngữ khách chọn (hoặc chưa có audio)
export function needsLocalization(poi) {
    return poi.isFallback || !poi.audioUrl;
}

export function stopAudio() {
    audio.pause();
    if (window.speechSynthesis) speechSynthesis.cancel();
}

function speak(poi) {
    if (!window.speechSynthesis) return;
    speechSynthesis.cancel();
    const u = new SpeechSynthesisUtterance(poi.name + ". " + poi.description);
    u.lang = SPEECH_LANG[poi.servedLang] || poi.servedLang;
    speechSynthesis.speak(u);
}

function replaceInBundle(poi) {
    const idx = state.bundle.pois.findIndex(p => p.id === poi.id);
    if (idx >= 0) state.bundle.pois[idx] = poi;
    try { localStorage.removeItem("bundle_" + state.lang); } catch { /* bỏ qua */ }
}

const panelOpenFor = poiId => state.currentPoi?.id === poiId && !$("poiPanel").classList.contains("hidden");

export function openPoi(poi, opts = {}) {
    state.currentPoi = poi;
    playEventSent = false;
    stopAudio();
    $("poiTitle").textContent = poi.name;
    $("poiDesc").textContent = poi.description;
    if (!opts.keepInsights || !insights || insights.poiId !== poi.id) {
        insights = null;
        popularDay = null;
        loadInsights(poi.id);
    }
    renderDetails(poi);

    const note = $("poiNote");
    note.classList.toggle("hidden", !poi.isFallback);
    if (poi.isFallback) note.textContent = t("fallbackNote") + " " + langName(poi.servedLang);

    const actions = $("poiActions");
    actions.innerHTML = "";
    if (needsLocalization(poi)) {
        const btn = document.createElement("button");
        btn.type = "button";
        btn.className = "btn btn-secondary btn-sm";
        btn.textContent = t("makeAudio");
        btn.onclick = () => translateNow(poi, btn);
        actions.appendChild(btn);
    }
    const speakBtn = document.createElement("button");
    speakBtn.type = "button";
    speakBtn.className = "btn btn-secondary btn-sm";
    speakBtn.textContent = t("speak");
    speakBtn.onclick = () => speak(poi);
    actions.appendChild(speakBtn);

    if (poi.audioUrl) {
        audio.src = poi.audioUrl;
        audio.classList.remove("hidden");
    } else {
        audio.removeAttribute("src");
        audio.classList.add("hidden");
    }
    $("poiPanel").classList.remove("hidden");
    if (!opts.silent) sendEvent(poi.id, "View");
    if (opts.autoplay) {
        if (poi.audioUrl) audio.play().catch(() => { /* trình duyệt chặn tự phát -> khách bấm nút phát */ });
        else speak(poi);
    }
}

// Bấm "Dịch và tạo audio": server dịch ngay điểm này rồi trả về
async function translateNow(poi, btn) {
    btn.disabled = true;
    btn.textContent = t("processing");
    try {
        const fresh = await visitorApi.ensureLocalization(poi.id, state.lang);
        replaceInBundle(fresh);
        openPoi(fresh, { autoplay: true });
    } catch (err) {
        if (err.status === 401) return onUnauthorized();
        showMsg(err.message);
        btn.disabled = false;
        btn.textContent = t("makeAudio");
    }
}

// Tới gần 1 điểm chưa có bản dịch: dịch + tạo audio NGAY rồi mới phát (thay vì phát tiếng Anh)
export async function prepareAndPlay(poi) {
    state.preparingPoiId = poi.id;
    const requestLang = state.lang;
    openPoi(poi, { autoplay: false });
    showMsg(t("translating"), 0);
    try {
        const fresh = await visitorApi.ensureLocalization(poi.id, requestLang);
        if (requestLang !== state.lang) return; // khách đã đổi ngôn ngữ trong lúc chờ
        replaceInBundle(fresh);
        showMsg(t("translated"));
        if (panelOpenFor(poi.id)) openPoi(fresh, { autoplay: true, silent: true, keepInsights: true });
    } catch (err) {
        if (err.status === 401) return onUnauthorized();
        showMsg(t("fallbackNote") + " " + langName(poi.servedLang));
        if (panelOpenFor(poi.id)) openPoi(poi, { autoplay: true, silent: true, keepInsights: true });
    } finally {
        if (state.preparingPoiId === poi.id) state.preparingPoiId = null;
    }
}

// ===== Thông tin chi tiết (giống thẻ địa điểm Google Maps) =====
// Tên thứ trong tuần theo ngôn ngữ đang chọn: dùng Intl của trình duyệt (2026-10-05 là Thứ Hai -> day 0)
function dayName(day) {
    try {
        return new Intl.DateTimeFormat(state.lang, { weekday: "long" }).format(new Date(2026, 9, 5 + day));
    } catch {
        return String(day + 1);
    }
}

function money(vnd) {
    if (!vnd) return t("free");
    try {
        return new Intl.NumberFormat(state.lang, { style: "currency", currency: "VND", maximumFractionDigits: 0 }).format(vnd);
    } catch {
        return vnd + " VND";
    }
}

const periodsOf = (poi, day) => poi.openingHours.filter(p => p.day === day).map(p => p.open + "–" + p.close);

async function loadInsights(poiId) {
    try {
        const data = await visitorApi.insights(poiId);
        if (state.currentPoi?.id !== poiId) return; // khách đã mở điểm khác
        insights = data;
        if (popularDay === null) popularDay = data.today;
        renderDetails(state.currentPoi);
    } catch { /* mất mạng -> vẫn hiện giờ mở cửa từ bundle */ }
}

// "Đang mở cửa · Đóng cửa lúc 21:00"
function statusHtml() {
    const s = insights?.openStatus;
    if (!s || s.state === "Unknown") return "";
    const open = s.state === "Open" || s.state === "ClosingSoon";
    const label = { Open: "statusOpen", ClosingSoon: "statusClosingSoon", Closed: "statusClosed", OpeningSoon: "statusOpeningSoon" }[s.state];
    let more;
    if (open) more = t("closesAt") + " " + s.closesAt;
    else {
        const when = s.opensInDays === 0 ? "" : s.opensInDays === 1 ? t("tomorrow") + " " : dayName(s.opensDay) + " ";
        more = t("opensAt") + " " + when + s.opensAt;
    }
    const cls = s.state === "Open" ? "st-open" : open || s.state === "OpeningSoon" ? "st-warn" : "st-closed";
    return `<b class="${cls}">${escapeHtml(t(label))}</b> · ${escapeHtml(more)}`;
}

function popularHtml() {
    if (!insights) return "";
    const pt = insights.popularTimes;
    if (!pt.byDay) return `<span class="text-muted text-sm">${escapeHtml(t("notEnoughData"))}</span>`;
    let html = '<select class="select" id="popularDay" style="width:auto">';
    for (let d = 0; d < 7; d++)
        html += `<option value="${d}" ${d === popularDay ? "selected" : ""}>${escapeHtml(d === insights.today ? t("today") : dayName(d))}</option>`;
    html += "</select>";
    if (insights.busyNow !== null && popularDay === insights.today) {
        const level = insights.busyNow >= 70 ? "busyHigh" : insights.busyNow >= 35 ? "busyMedium" : "busyLow";
        html += ` <span class="text-sm text-muted">${escapeHtml(t("busyNow") + ": " + t(level))}</span>`;
    }
    const nowHour = parseInt(insights.nowLocal.slice(11, 13), 10);
    html += '<div class="bars">';
    for (let h = 5; h <= 22; h++) {
        const v = pt.byDay[popularDay][h];
        const now = popularDay === insights.today && h === nowHour ? " now" : "";
        html += `<div class="bar${now}" title="${h}h: ${v}%"><i style="height:${Math.max(v, 2)}%"></i><span>${h % 3 === 0 ? h : ""}</span></div>`;
    }
    return html + "</div>";
}

function renderDetails(poi) {
    const status = statusHtml();
    $("poiMeta").innerHTML = escapeHtml(t("cat_" + poi.category)) + (status ? " · " + status : "");

    const rows = [];
    if (poi.openingHours.length) {
        const today = insights ? insights.today : (new Date().getDay() + 6) % 7;
        let week = "";
        for (let d = 0; d < 7; d++) {
            const list = periodsOf(poi, d);
            week += `<tr${d === today ? ' class="today"' : ""}><td>${escapeHtml(dayName(d))}</td><td>${list.length ? list.map(escapeHtml).join("<br>") : escapeHtml(t("closedAllDay"))}</td></tr>`;
        }
        const todayText = periodsOf(poi, today).join(", ") || t("closedAllDay");
        rows.push([t("hoursLabel"), `${escapeHtml(t("today") + ": " + todayText)}<details><summary>${escapeHtml(t("showWeek"))}</summary><table class="v-week">${week}</table></details>`]);
    } else {
        rows.push([t("hoursLabel"), escapeHtml(t("hoursUnknown"))]);
    }
    rows.push([t("feeLabel"), escapeHtml(money(poi.entryFeeVnd))]);
    if (poi.visitMinutes) rows.push([t("visitLabel"), escapeHtml(`${t("about")} ${poi.visitMinutes} ${t("minutes")}`)]);
    if (poi.amenities.length)
        rows.push([t("amenitiesLabel"), `<ul class="v-amen">${poi.amenities.map(a => `<li>${escapeHtml(t("amenity_" + a))}</li>`).join("")}</ul>`]);
    if (poi.tips) {
        const note = poi.tipsLang && poi.tipsLang !== state.lang ? ` <span class="text-sm text-muted">(${escapeHtml(langName(poi.tipsLang))})</span>` : "";
        rows.push([t("tipsLabel"), escapeHtml(poi.tips) + note]);
    }
    rows.push([t("popularLabel"), popularHtml() || '<span class="text-muted">...</span>']);

    const maps = `https://www.google.com/maps/dir/?api=1&travelmode=walking&destination=${poi.latitude},${poi.longitude}`;
    $("poiDetails").innerHTML = `<h4>${escapeHtml(t("detailsTitle"))}</h4><table class="v-info">`
        + rows.map(([k, v]) => `<tr><th>${escapeHtml(k)}</th><td>${v}</td></tr>`).join("") + "</table>"
        + `<p class="mt-4"><a href="${maps}" target="_blank" rel="noopener">${escapeHtml(t("directions"))}</a></p>`;

    const sel = $("popularDay");
    if (sel) sel.onchange = e => { popularDay = Number(e.target.value); renderDetails(state.currentPoi); };
}

// Bảng đang mở mà bản dịch vừa có -> cập nhật chữ (không cắt audio đang phát)
export function refreshPanel() {
    if (!state.currentPoi || $("poiPanel").classList.contains("hidden") || !audio.paused) return;
    const fresh = state.bundle.pois.find(p => p.id === state.currentPoi.id);
    if (!fresh) return;
    const changed = fresh.servedLang !== state.currentPoi.servedLang || fresh.tips !== state.currentPoi.tips
        || JSON.stringify(fresh.openingHours) !== JSON.stringify(state.currentPoi.openingHours);
    if (changed) openPoi(fresh, { silent: true, keepInsights: true });
}

export function initPanel() {
    audio.addEventListener("play", () => {
        if (state.currentPoi && !playEventSent) {
            playEventSent = true;
            sendEvent(state.currentPoi.id, "AudioPlay");
        }
    });
    $("btnPoiClose").addEventListener("click", () => {
        stopAudio();
        $("poiPanel").classList.add("hidden");
    });
    // Trạng thái mở cửa đổi theo giờ -> bảng đang mở thì 1 phút hỏi lại server 1 lần
    setInterval(() => {
        if (state.currentPoi && !$("poiPanel").classList.contains("hidden")) loadInsights(state.currentPoi.id);
    }, 60000);
}

export function hidePanel() {
    stopAudio();
    $("poiPanel").classList.add("hidden");
}
