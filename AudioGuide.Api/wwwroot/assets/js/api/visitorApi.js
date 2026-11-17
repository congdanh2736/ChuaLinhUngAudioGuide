/*
 * API cho app du khách: dùng token du khách (đổi từ mã truy cập).
 * ContentController (/api/content), ChatController (/api/chat), AccessController (/api/access), UiStringsController.
 */
import { createClient } from "../core/http.js";
import { STORAGE_KEYS } from "../core/config.js";

export const visitorHttp = createClient({ tokenKey: STORAGE_KEYS.VISITOR_TOKEN });

export const visitorApi = {
    languages: () => visitorHttp.get("/languages"),
    uiStrings: lang => visitorHttp.get(`/ui-strings/${lang}`),

    accessInfo: () => visitorHttp.get("/access/info"),
    startOnlinePayment: () => visitorHttp.post("/access/online"),
    redeem: code => visitorHttp.post("/access/token", { code }),

    // Gửi kèm ETag cũ: dữ liệu không đổi thì server trả 304 (raw = true để đọc header)
    bundle: (lang, etag) => visitorHttp.request("GET", "/content/bundle", {
        params: { lang },
        headers: etag ? { "If-None-Match": etag } : {},
        raw: true
    }),
    insights: poiId => visitorHttp.get(`/content/pois/${poiId}/insights`),
    ensureLocalization: (poiId, lang) => visitorHttp.post(`/content/pois/${poiId}/localizations/${lang}/ensure`),
    recommend: body => visitorHttp.post("/content/tours/recommend", body),
    event: (poiId, type, lang) => visitorHttp.post("/content/events", { poiId, type, lang }),
    chat: (question, lang) => visitorHttp.post("/chat", { question, lang })
};
