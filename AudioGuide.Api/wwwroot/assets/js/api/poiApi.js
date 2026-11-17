import { adminHttp as http } from "./adminClient.js";

// API quản trị điểm tham quan: PoiController (/api/pois) + LocalizationController (/api/pois/{id}/...)
export const poiApi = {
    search: ({ q, isActive, page = 1, size = 100 } = {}) => http.get("/pois", { q, isActive, page, size }),
    getById: id => http.get(`/pois/${id}`),
    create: data => http.post("/pois", data),
    update: (id, data) => http.put(`/pois/${id}`, data),
    remove: id => http.del(`/pois/${id}`)
};

export const localizationApi = {
    overview: poiId => http.get(`/pois/${poiId}/localizations`),
    localize: (poiId, languages = null, force = false) => http.post(`/pois/${poiId}/localize`, { languages, force }),
    updateManual: (poiId, lang, data) => http.put(`/pois/${poiId}/localizations/${lang}`, data),
    removeOne: (poiId, lang) => http.del(`/pois/${poiId}/localizations/${lang}`),
    removeAll: poiId => http.del(`/pois/${poiId}/localizations`),
    localizeAll: (force = false) => http.post("/localizations/localize-all", { force })
};

export const aiApi = {
    enhanceDescription: (name, description) => http.post("/ai/enhance-description", { name, description })
};
