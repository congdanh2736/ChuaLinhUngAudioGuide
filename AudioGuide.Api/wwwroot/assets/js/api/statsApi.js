import { adminHttp as http } from "./adminClient.js";

// API thống kê: StatsController (/api/stats)
export const statsApi = {
    overview: () => http.get("/stats"),
    chatLogs: (limit = 20) => http.get("/stats/chat-logs", { limit }),
    codesByDay: (from, to) => http.get("/stats/codes-by-day", { from, to })
};
