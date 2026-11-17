/*
 * Client gọi API cho trang quản trị: gắn token admin, hết hạn (401) thì quay về trang đăng nhập.
 */
import { createClient } from "../core/http.js";
import { STORAGE_KEYS } from "../core/config.js";
import { clearLogin, redirectToLogin } from "../core/auth.js";

export const adminHttp = createClient({
    tokenKey: STORAGE_KEYS.ADMIN_TOKEN,
    onUnauthorized: () => {
        clearLogin();
        redirectToLogin();
    }
});
