/*
 * utils.js - các hàm tiện ích nhỏ dùng nhiều nơi.
 */
import { LOCALIZATION_STATUS_LABELS, ACCESS_STATUS_LABELS } from "./config.js";

export function formatMoney(value) {
    return new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND", maximumFractionDigits: 0 }).format(Number(value) || 0);
}

export function formatNumber(value) {
    return new Intl.NumberFormat("vi-VN").format(Number(value) || 0);
}

// Backend trả giờ UTC (có chữ Z). Thiếu múi giờ thì coi là UTC.
export function toDate(value) {
    if (!value) return null;
    if (value instanceof Date) return value;
    let text = String(value);
    if (text.includes("T") && !/([zZ]|[+-]\d{2}:?\d{2})$/.test(text)) text += "Z";
    const date = new Date(text);
    return isNaN(date.getTime()) ? null : date;
}

export function formatDateTime(value) {
    const date = toDate(value);
    if (!date) return "—";
    return date.toLocaleString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit", hour12: false });
}

export function getQueryParam(name) {
    return new URLSearchParams(window.location.search).get(name);
}

export function getIdFromUrl(name = "id") {
    const value = Number(getQueryParam(name));
    return Number.isInteger(value) && value > 0 ? value : null;
}

// Chống chèn mã độc (XSS): luôn escape chữ do người dùng nhập trước khi đưa vào innerHTML
export function escapeHtml(value) {
    if (value === null || value === undefined) return "";
    return String(value)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

export function statusBadge(status, labels) {
    const info = labels?.[status] ?? { text: status || "—", color: "secondary" };
    return `<span class="badge badge-${info.color}">${escapeHtml(info.text)}</span>`;
}
export const localizationBadge = status => statusBadge(status, LOCALIZATION_STATUS_LABELS);
export const accessBadge = status => statusBadge(status, ACCESS_STATUS_LABELS);
export const activeBadge = (isActive, on = "Hiển thị", off = "Ẩn") =>
    `<span class="badge badge-${isActive ? "success" : "secondary"}">${escapeHtml(isActive ? on : off)}</span>`;

export function debounce(fn, delay = 400) {
    let timer;
    return (...args) => {
        clearTimeout(timer);
        timer = setTimeout(() => fn(...args), delay);
    };
}

export function setLoading(button, isLoading, loadingText = "Đang xử lý...") {
    if (!button) return;
    if (isLoading) {
        button.dataset.originalText = button.textContent;
        button.textContent = loadingText;
        button.disabled = true;
    } else {
        button.textContent = button.dataset.originalText ?? button.textContent;
        button.disabled = false;
    }
}

// Đọc/ghi localStorage an toàn (chế độ riêng tư có thể chặn)
export const storage = {
    get(key, fallback = null) {
        try {
            const value = localStorage.getItem(key);
            return value === null ? fallback : JSON.parse(value);
        } catch {
            return fallback;
        }
    },
    set(key, value) {
        try { localStorage.setItem(key, JSON.stringify(value)); } catch { /* hết dung lượng */ }
    },
    remove(key) {
        try { localStorage.removeItem(key); } catch { /* bỏ qua */ }
    }
};
