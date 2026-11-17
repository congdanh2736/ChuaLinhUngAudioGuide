/*
 * http.js - hàm gọi API dùng chung (bọc fetch).
 * - Tự gắn token vào header Authorization.
 * - Backend báo lỗi dạng {message, code, errors} -> ném ApiError để trang hiển thị.
 *
 * Mỗi "khu vực" dùng 1 token khác nhau: trang quản trị dùng token admin,
 * app du khách dùng token du khách -> truyền tokenKind khi tạo client.
 */
import { API_BASE, STORAGE_KEYS } from "./config.js";

export class ApiError extends Error {
    constructor(message, status = 0, code = null, errors = null, data = null) {
        super(message);
        this.name = "ApiError";
        this.status = status;   // mã HTTP: 400, 401, 404, 409...
        this.code = code;       // mã lỗi của backend: POI_NOT_FOUND, PAYMENT_PENDING...
        this.errors = errors;   // lỗi theo từng ô nhập (FluentValidation)
        this.data = data;
    }
}

function buildUrl(path, params) {
    let url = path.startsWith("http") || path.startsWith("/media") ? path : API_BASE + path;
    if (params) {
        const search = new URLSearchParams();
        for (const [key, value] of Object.entries(params)) {
            if (value === undefined || value === null || value === "") continue;
            search.append(key, value);
        }
        const query = search.toString();
        if (query) url += (url.includes("?") ? "&" : "?") + query;
    }
    return url;
}

function defaultMessageFor(status) {
    switch (status) {
        case 400: return "Dữ liệu gửi lên không hợp lệ.";
        case 401: return "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.";
        case 403: return "Bạn không có quyền thực hiện thao tác này.";
        case 404: return "Không tìm thấy dữ liệu.";
        case 409: return "Dữ liệu bị trùng hoặc đang được dùng ở nơi khác.";
        case 429: return "Bạn thao tác quá nhiều, vui lòng thử lại sau.";
        case 503: return "Dịch vụ tạm thời không phản hồi, vui lòng thử lại sau.";
        default: return `Có lỗi xảy ra (mã ${status}).`;
    }
}

/*
 * Tạo 1 "client" gọi API.
 *   tokenKey   : khóa localStorage chứa token cần gắn vào request
 *   onUnauthorized: hàm chạy khi server trả 401 (vd chuyển về trang đăng nhập)
 */
export function createClient({ tokenKey = STORAGE_KEYS.ADMIN_TOKEN, onUnauthorized = null } = {}) {
    async function request(method, path, { body, params, headers = {}, raw = false } = {}) {
        const allHeaders = { Accept: "application/json", ...headers };
        let token = null;
        try { token = localStorage.getItem(tokenKey); } catch { /* chế độ riêng tư */ }
        if (token) allHeaders.Authorization = `Bearer ${token}`;
        let payload;
        if (body !== undefined) {
            allHeaders["Content-Type"] = "application/json";
            payload = JSON.stringify(body);
        }

        let response;
        try {
            response = await fetch(buildUrl(path, params), { method, headers: allHeaders, body: payload });
        } catch {
            throw new ApiError("Không kết nối được tới máy chủ. Kiểm tra mạng hoặc server đã chạy chưa.", 0, "NETWORK_ERROR");
        }
        // raw = true: trả nguyên Response (cần đọc header ETag / trạng thái 304)
        if (raw && (response.ok || response.status === 304)) return response;

        const text = await response.text();
        let data = null;
        if (text) {
            try { data = JSON.parse(text); } catch { data = text; }
        }
        if (response.ok) return data;

        if (response.status === 401 && token && typeof onUnauthorized === "function") onUnauthorized(data);
        const message = (data && typeof data === "object" && data.message) || (typeof data === "string" && data) || defaultMessageFor(response.status);
        throw new ApiError(message, response.status, data?.code ?? null, data?.errors ?? null, data);
    }

    return {
        request,
        get: (path, params) => request("GET", path, { params }),
        post: (path, body, params) => request("POST", path, { body, params }),
        put: (path, body) => request("PUT", path, { body }),
        patch: (path, body) => request("PATCH", path, { body }),
        del: (path) => request("DELETE", path)
    };
}
