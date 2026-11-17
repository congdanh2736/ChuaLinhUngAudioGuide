import { adminHttp as http } from "./adminClient.js";

// API ngôn ngữ: LanguageController (/api/languages)
export const languageApi = {
    getAll: () => http.get("/languages/all"),
    toggle: (code, isActive) => http.patch(`/languages/${code}`, { isActive })
};
