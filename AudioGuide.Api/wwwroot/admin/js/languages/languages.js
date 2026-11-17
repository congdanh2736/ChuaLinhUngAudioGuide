/*
 * Ngôn ngữ: GET /api/languages/all, PATCH /api/languages/{code} -> LanguageService.SetActiveAsync
 * (không cho tắt ngôn ngữ gốc "vi" và ngôn ngữ dự phòng "en").
 */
import { initAdminLayout } from "../layout.js";
import { languageApi } from "../../../assets/js/api/languageApi.js";
import { renderTable } from "../../../assets/js/components/table.js";
import { toast } from "../../../assets/js/components/toast.js";
import { isAdmin } from "../../../assets/js/core/auth.js";
import { escapeHtml } from "../../../assets/js/core/utils.js";

const container = document.getElementById("tableContainer");

async function load() {
    try {
        const rows = await languageApi.getAll();
        renderTable(container, {
            columns: [
                { key: "code", title: "Mã", render: r => `<b>${escapeHtml(r.code)}</b>` },
                { key: "nativeName", title: "Tên", render: r => `${escapeHtml(r.nativeName)} <span class="text-muted text-sm">${escapeHtml(r.name)}</span>` },
                { key: "translatorCode", title: "Mã dịch máy", render: r => `<code>${escapeHtml(r.translatorCode)}</code>` },
                { key: "ttsVoice", title: "Giọng đọc (Edge-TTS)", render: r => `<span class="text-sm">${escapeHtml(r.ttsVoice)}</span>` },
                {
                    key: "isActive", title: "Bật", align: "center", render: r =>
                        `<input type="checkbox" data-code="${escapeHtml(r.code)}" ${r.isActive ? "checked" : ""} ${isAdmin() ? "" : "disabled"}>`
                }
            ],
            rows
        });
    } catch (err) {
        container.innerHTML = `<p class="card-body text-danger">${escapeHtml(err.message)}</p>`;
    }
}

container.addEventListener("change", async e => {
    const box = e.target.closest("input[data-code]");
    if (!box) return;
    try {
        await languageApi.toggle(box.dataset.code, box.checked);
        toast.success("Đã cập nhật.");
    } catch (err) {
        box.checked = !box.checked; // trả lại như cũ
        toast.error(err.message);
    }
});

if (await initAdminLayout({ title: "Ngôn ngữ" })) load();
