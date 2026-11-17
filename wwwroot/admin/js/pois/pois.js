/*
 * Danh sách điểm tham quan: GET /api/pois -> PoiController.Search -> PoiService.SearchAsync -> PoiRepository.SearchAsync
 */
import { initAdminLayout } from "../layout.js";
import { poiApi } from "../../../assets/js/api/poiApi.js";
import { createDataTable } from "../../../assets/js/components/table.js";
import { confirmDialog } from "../../../assets/js/components/modal.js";
import { toast } from "../../../assets/js/components/toast.js";
import { isAdmin } from "../../../assets/js/core/auth.js";
import { CATEGORY_LABELS } from "../../../assets/js/core/config.js";
import { escapeHtml, activeBadge, debounce } from "../../../assets/js/core/utils.js";

const $ = id => document.getElementById(id);

const table = createDataTable({
    container: $("tableContainer"),
    paginationContainer: $("paginationContainer"),
    columns: [
        { key: "sortOrder", title: "#", width: "50px" },
        { key: "code", title: "Mã", render: r => `<code>${escapeHtml(r.code)}</code>` },
        { key: "name", title: "Tên", render: r => `<span class="fw-bold">${escapeHtml(r.name)}</span>` },
        { key: "category", title: "Loại", render: r => escapeHtml(CATEGORY_LABELS[r.category] ?? r.category) },
        { key: "latitude", title: "Tọa độ", render: r => `<span class="text-sm">${r.latitude.toFixed(5)}, ${r.longitude.toFixed(5)}</span>` },
        { key: "triggerRadiusM", title: "Bán kính", align: "right", render: r => `${r.triggerRadiusM} m` },
        { key: "priority", title: "Ưu tiên", align: "right" },
        { key: "isActive", title: "Trạng thái", render: r => activeBadge(r.isActive) }
    ],
    searchFields: ["name", "code"],
    emptyText: "Chưa có điểm tham quan nào.",
    actions: [
        { label: "Bản dịch", onClick: row => (window.location.href = `/admin/pois/localizations.html?id=${row.id}`) },
        { label: "Sửa", visible: () => isAdmin(), onClick: row => (window.location.href = `/admin/pois/poi-form.html?id=${row.id}`) },
        { label: "Xóa", danger: true, visible: () => isAdmin(), onClick: row => removeRow(row) }
    ]
});

async function load({ keepPage = false } = {}) {
    table.setLoading();
    try {
        const value = $("activeFilter").value;
        const page = await poiApi.search({ isActive: value === "" ? undefined : value, size: 100 });
        table.setRows(page.items, { keepPage });
    } catch (err) {
        table.setError(err.message);
    }
}

async function removeRow(row) {
    const ok = await confirmDialog({
        title: "Xóa điểm tham quan",
        message: `Xóa "${row.name}"?\nChỉ xóa được khi điểm không còn bản dịch nào và chưa có lượt tham quan (khóa ngoại).`,
        confirmText: "Xóa",
        danger: true
    });
    if (!ok) return;
    try {
        await poiApi.remove(row.id);
        toast.success("Đã xóa.");
        await load({ keepPage: true });
    } catch (err) {
        toast.error(err.message);
    }
}

if (await initAdminLayout({ title: "Điểm tham quan" })) {
    const onSearch = debounce(text => table.search(text), 300);
    $("searchInput").addEventListener("input", e => onSearch(e.target.value));
    $("activeFilter").addEventListener("change", () => load());
    $("btnReload").addEventListener("click", () => load({ keepPage: true }));
    load();
}
