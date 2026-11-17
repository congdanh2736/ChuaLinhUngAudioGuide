import { adminHttp as http } from "./adminClient.js";

// API quản trị lộ trình: TourController (/api/tours)
export const tourApi = {
    getAll: () => http.get("/tours"),
    getById: id => http.get(`/tours/${id}`),
    distance: id => http.get(`/tours/${id}/distance`),
    create: data => http.post("/tours", data),
    update: (id, data) => http.put(`/tours/${id}`, data),
    remove: id => http.del(`/tours/${id}`)
};
