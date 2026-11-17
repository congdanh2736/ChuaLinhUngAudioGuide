import { adminHttp as http } from "./adminClient.js";

// API bài viết kiến thức cho chatbot: KnowledgeController (/api/knowledge)
export const knowledgeApi = {
    getAll: () => http.get("/knowledge"),
    create: data => http.post("/knowledge", data),
    update: (id, data) => http.put(`/knowledge/${id}`, data),
    remove: id => http.del(`/knowledge/${id}`)
};
