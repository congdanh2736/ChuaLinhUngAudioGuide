/*
 * auth.js - đăng nhập / đăng xuất / phân quyền phía trang quản trị.
 * Token admin lưu ở localStorage; mỗi lần gọi API, http.js tự gắn vào header.
 */
import { STORAGE_KEYS, ROLES, PAGES } from "./config.js";

export function saveLogin(result) {
    localStorage.setItem(STORAGE_KEYS.ADMIN_TOKEN, result.token);
    localStorage.setItem(STORAGE_KEYS.ADMIN_USER, JSON.stringify(result.user));
}

export function getToken() {
    return localStorage.getItem(STORAGE_KEYS.ADMIN_TOKEN);
}

export function getUser() {
    try {
        return JSON.parse(localStorage.getItem(STORAGE_KEYS.ADMIN_USER) || "null");
    } catch {
        return null;
    }
}

export function clearLogin() {
    localStorage.removeItem(STORAGE_KEYS.ADMIN_TOKEN);
    localStorage.removeItem(STORAGE_KEYS.ADMIN_USER);
}

// Đọc hạn dùng ghi trong token JWT (phần giữa, mã hóa base64) để biết đã hết hạn chưa mà không cần gọi server
export function isTokenExpired(token = getToken()) {
    if (!token) return true;
    try {
        const payload = JSON.parse(atob(token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")));
        return payload.exp ? payload.exp < Date.now() / 1000 : false;
    } catch {
        return true;
    }
}

export function isLoggedIn() {
    if (isTokenExpired()) {
        clearLogin();
        return false;
    }
    return true;
}

export function isAdmin() {
    return isLoggedIn() && getUser()?.role === ROLES.ADMIN;
}

export function redirectToLogin() {
    if (window.location.pathname === PAGES.ADMIN_LOGIN) return;
    const returnUrl = encodeURIComponent(window.location.pathname + window.location.search);
    window.location.replace(`${PAGES.ADMIN_LOGIN}?returnUrl=${returnUrl}`);
}

export function redirectAfterLogin() {
    const returnUrl = new URLSearchParams(window.location.search).get("returnUrl");
    const safe = returnUrl && returnUrl.startsWith("/admin") && !returnUrl.startsWith("//");
    window.location.replace(safe ? returnUrl : PAGES.ADMIN_HOME);
}

export function logout() {
    clearLogin();
    window.location.replace(PAGES.ADMIN_LOGIN);
}

// Trang nào cũng gọi: chưa đăng nhập -> về trang đăng nhập
export function requireLogin() {
    if (!isLoggedIn()) {
        redirectToLogin();
        return false;
    }
    return true;
}

// Trang chỉ dành cho Admin (vd quản lý tài khoản)
export function requireAdmin() {
    if (!requireLogin()) return false;
    if (!isAdmin()) {
        alert("Trang này chỉ dành cho quản trị viên.");
        window.location.replace(PAGES.ADMIN_HOME);
        return false;
    }
    return true;
}
