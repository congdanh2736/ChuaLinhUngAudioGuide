/*
 * state.js - "bộ nhớ chung" của app du khách: các module khác đọc / ghi chung các biến ở đây.
 */
import { STORAGE_KEYS } from "../../assets/js/core/config.js";
import { storage } from "../../assets/js/core/utils.js";

function readToken() {
    try { return localStorage.getItem(STORAGE_KEYS.VISITOR_TOKEN); } catch { return null; }
}

const browserLang = (navigator.language || "vi").slice(0, 2) === "vi" ? "vi" : "en";

export const state = {
    token: readToken(),                                  // token du khách (đổi từ mã truy cập)
    lang: storage.get(STORAGE_KEYS.LANG) || browserLang, // ngôn ngữ đang chọn
    languages: [],                                       // các ngôn ngữ đang bật
    bundle: null,                                        // toàn bộ dữ liệu: điểm, lộ trình (tải 1 lần)
    pendingTranslations: 0,                              // số điểm server đang dịch dở
    position: null,                                      // {lat, lng} vị trí hiện tại
    simulate: false,                                     // đang giả lập GPS (bấm bản đồ để đặt vị trí)
    autoplay: storage.get(STORAGE_KEYS.AUTOPLAY, true),  // tới gần điểm thì tự phát
    currentPoi: null,                                    // điểm đang mở trong bảng thông tin
    preparingPoiId: null                                 // đang dịch ngay cho điểm này
};

export function saveToken(token) {
    state.token = token;
    try {
        if (token) localStorage.setItem(STORAGE_KEYS.VISITOR_TOKEN, token);
        else localStorage.removeItem(STORAGE_KEYS.VISITOR_TOKEN);
    } catch { /* chế độ riêng tư */ }
}

export const $ = id => document.getElementById(id);

// Thanh thông báo dưới tab. ms = 0 thì không tự ẩn
let msgTimer = null;
export function showMsg(text, ms = 4000) {
    const bar = $("msgBar");
    bar.textContent = text;
    bar.classList.remove("hidden");
    clearTimeout(msgTimer);
    if (ms > 0) msgTimer = setTimeout(() => bar.classList.add("hidden"), ms);
}

export function langName(code) {
    return state.languages.find(l => l.code === code)?.nativeName ?? code;
}
