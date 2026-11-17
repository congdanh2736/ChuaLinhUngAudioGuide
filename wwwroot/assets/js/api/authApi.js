import { adminHttp as http } from "./adminClient.js";

// API tài khoản: AuthController (/api/auth) + UserController (/api/users)
export const authApi = {
    login: (userName, password) => http.post("/auth/login", { userName, password }),
    me: () => http.get("/auth/me"),
    changePassword: (oldPassword, newPassword) => http.post("/auth/change-password", { oldPassword, newPassword })
};

export const userApi = {
    getAll: () => http.get("/users"),
    create: data => http.post("/users", data),
    update: (id, data) => http.patch(`/users/${id}`, data)
};
