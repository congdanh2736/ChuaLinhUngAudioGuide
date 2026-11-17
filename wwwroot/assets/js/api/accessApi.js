import { adminHttp as http } from "./adminClient.js";

// API mã truy cập phía nhân viên: AccessController (/api/access)
export const accessApi = {
    createCashCodes: (quantity, note) => http.post("/access/cash-codes", { quantity, note }),
    sessions: ({ status, page = 1, size = 20 } = {}) => http.get("/access/sessions", { status, page, size }),
    revoke: id => http.post(`/access/sessions/${id}/revoke`)
};
