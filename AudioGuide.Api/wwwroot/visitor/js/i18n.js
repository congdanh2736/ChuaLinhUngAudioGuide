/*
 * i18n.js - chữ trên giao diện (nút, tab, thông báo) theo ngôn ngữ đang chọn.
 * Lấy từ GET /api/ui-strings/{lang}: tiếng Việt / Anh có sẵn, ngôn ngữ khác server dịch máy 1 lần rồi lưu DB.
 * Lưu thêm vào localStorage để lần sau hiện ngay, kể cả khi mất mạng.
 */
import { visitorApi } from "../../assets/js/api/visitorApi.js";
import { storage } from "../../assets/js/core/utils.js";
import { state } from "./state.js";

const strings = {}; // lang -> {key: text}

export async function loadStrings(lang) {
    const cached = storage.get("ui_" + lang);
    if (cached) strings[lang] = cached;
    try {
        const data = await visitorApi.uiStrings(lang);
        if (!data.isFallback || lang === "en") {
            strings[lang] = data.strings;
            storage.set("ui_" + lang, data.strings);
        } else if (!strings.en) {
            strings.en = data.strings; // server trả tạm bản tiếng Anh
        }
    } catch { /* mất mạng -> dùng bản đã lưu */ }
    if (!strings.en && lang !== "en") await loadStrings("en");
}

export function t(key) {
    return strings[state.lang]?.[key] ?? strings.en?.[key] ?? key;
}

// Gán chữ cho mọi phần tử có thuộc tính data-i18n="key"
export function applyI18n() {
    document.documentElement.lang = state.lang;
    document.querySelectorAll("[data-i18n]").forEach(el => (el.textContent = t(el.dataset.i18n)));
    const chatInput = document.getElementById("chatInput");
    if (chatInput) chatInput.placeholder = t("chatPlaceholder");
}
