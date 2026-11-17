/*
 * chatView.js - tab "Hỏi đáp": POST /api/chat -> ChatService (AI tạo sinh + tài liệu của chùa, hoặc trích đoạn BM25).
 */
import { visitorApi } from "../../assets/js/api/visitorApi.js";
import { escapeHtml } from "../../assets/js/core/utils.js";
import { state, $ } from "./state.js";
import { t } from "./i18n.js";

function addBubble(kind, html) {
    const p = document.createElement("p");
    p.className = `bubble ${kind}`;
    p.innerHTML = html;
    $("chatLog").appendChild(p);
    $("chatLog").scrollTop = $("chatLog").scrollHeight;
    return p;
}

export function greet() {
    if (!$("chatLog").children.length) addBubble("bot", escapeHtml(t("chatHello")));
}

export function initChat() {
    $("chatForm").addEventListener("submit", async e => {
        e.preventDefault();
        const question = $("chatInput").value.trim();
        if (question.length < 2) return;
        $("chatInput").value = "";
        addBubble("me", escapeHtml(question));
        const bubble = addBubble("bot", "...");
        try {
            const data = await visitorApi.chat(question, state.lang);
            let html = escapeHtml(data.answer);
            if (data.sources.length) html += `<span class="src">${escapeHtml(t("sources"))}: ${data.sources.map(s => escapeHtml(s.title)).join(", ")}</span>`;
            bubble.innerHTML = html;
        } catch (err) {
            bubble.innerHTML = `<span class="text-danger">${escapeHtml(err.message)}</span>`;
        }
    });
}
