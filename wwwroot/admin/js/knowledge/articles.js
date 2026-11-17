/*
 * Bài viết kiến thức: KnowledgeController (/api/knowledge) -> KnowledgeService -> KnowledgeRepository.
 * Trang dùng formModal (form chung, FluentValidation trả lỗi thì hiện dưới từng ô).
 */
import { initAdminLayout } from "../layout.js";
import { knowledgeApi } from "../../../assets/js/api/knowledgeApi.js";
import { createDataTable } from "../../../assets/js/components/table.js";
import { formModal, confirmDialog } from "../../../assets/js/components/modal.js";
import { toast } from "../../../assets/js/components/toast.js";
import { isAdmin } from "../../../assets/js/core/auth.js";
import { escapeHtml, activeBadge, debounce } from "../../../assets/js/core/utils.js";

const $ = id => document.getElementById(id);

const table = createDataTable({
    container: $("tableContainer"),
    paginationContainer: $("paginationContainer"),
    columns: [
        { key: "title", title: "Tiêu đề", render: r => `<b>${escapeHtml(r.title)}</b><div class="text-muted text-sm">${r.tags.map(escapeHtml).join(", ")}</div>` },
        { key: "content", title: "Nội dung", render: r => `<span class="text-sm">${escapeHtml(r.content.length > 220 ? r.content.slice(0, 220) + "…" : r.content)}</span>` },
        { key: "isActive", title: "Trạng thái", render: r => activeBadge(r.isActive, "Dùng", "Tắt") }
    ],
    searchFields: ["title", "content"],
    emptyText: "Chưa có bài viết nào.",
    actions: [
        { label: "Sửa", visible: () => isAdmin(), onClick: row => openForm(row) },
        { label: "Xóa", danger: true, visible: () => isAdmin(), onClick: row => removeRow(row) }
    ]
});

async function load({ keepPage = false } = {}) {
    table.setLoading();
    try {
        table.setRows(await knowledgeApi.getAll(), { keepPage });
    } catch (err) {
        table.setError(err.message);
    }
}

function openForm(row = null) {
    formModal({
        title: row ? "Sửa bài viết" : "Thêm bài viết",
        size: "lg",
        fields: [
            { name: "title", label: "Tiêu đề", required: true },
            { name: "content", label: "Nội dung", type: "textarea", required: true },
            { name: "tags", label: "Từ khóa (phân cách bằng dấu phẩy)" },
            { name: "isActive", type: "checkbox", checkboxLabel: "Dùng cho chatbot" }
        ],
        values: row ? { ...row, tags: row.tags.join(", ") } : { isActive: true },
        onSubmit: async data => {
            const body = { ...data, tags: (data.tags || "").split(",").map(s => s.trim()).filter(Boolean) };
            if (row) await knowledgeApi.update(row.id, body);
            else await knowledgeApi.create(body);
            toast.success("Đã lưu bài viết.");
            await load({ keepPage: !!row });
        }
    });
}

async function removeRow(row) {
    const ok = await confirmDialog({ title: "Xóa bài viết", message: `Xóa "${row.title}"?`, confirmText: "Xóa", danger: true });
    if (!ok) return;
    try {
        await knowledgeApi.remove(row.id);
        toast.success("Đã xóa.");
        await load({ keepPage: true });
    } catch (err) {
        toast.error(err.message);
    }
}

if (await initAdminLayout({ title: "Kiến thức chatbot" })) {
    $("btnAdd")?.addEventListener("click", () => openForm());
    $("btnReload").addEventListener("click", () => load({ keepPage: true }));
    const onSearch = debounce(text => table.search(text), 300);
    $("searchInput").addEventListener("input", e => onSearch(e.target.value));
    load();
}
