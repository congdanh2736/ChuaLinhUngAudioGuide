/*
 * Quản trị lộ trình: TourController (/api/tours) -> TourService (kiểm tra điểm trùng / không tồn tại)
 * -> TourRepository (bảng Tours + TourPois có thứ tự + TourTranslations).
 */
import { initAdminLayout } from "../layout.js";
import { tourApi } from "../../../assets/js/api/tourApi.js";
import { poiApi } from "../../../assets/js/api/poiApi.js";
import { createDataTable } from "../../../assets/js/components/table.js";
import { openModal, confirmDialog } from "../../../assets/js/components/modal.js";
import { toast } from "../../../assets/js/components/toast.js";
import { isAdmin } from "../../../assets/js/core/auth.js";
import { escapeHtml, activeBadge, debounce, setLoading, formatNumber } from "../../../assets/js/core/utils.js";

const $ = id => document.getElementById(id);
let pois = [];
const poiName = id => pois.find(p => p.id === id)?.name ?? `#${id}`;

const table = createDataTable({
    container: $("tableContainer"),
    paginationContainer: $("paginationContainer"),
    columns: [
        { key: "code", title: "Mã", render: r => `<code>${escapeHtml(r.code)}</code>` },
        { key: "name", title: "Tên", render: r => `<b>${escapeHtml(r.name)}</b><div class="text-muted text-sm">${escapeHtml(r.description ?? "")}</div>` },
        { key: "poiIds", title: "Các điểm", render: r => `<span class="text-sm">${r.poiIds.map((id, i) => `${i + 1}. ${escapeHtml(poiName(id))}`).join("<br>")}</span>` },
        { key: "estimatedMinutes", title: "Thời gian", align: "right", render: r => `${r.estimatedMinutes} phút` },
        { key: "translatedLanguages", title: "Đã dịch", align: "right", render: r => `${r.translatedLanguages.length} NN` },
        { key: "isActive", title: "Trạng thái", render: r => activeBadge(r.isActive) }
    ],
    searchFields: ["name", "code"],
    emptyText: "Chưa có lộ trình nào.",
    actions: [
        { label: "Quãng đường", onClick: row => showDistance(row) },
        { label: "Sửa", visible: () => isAdmin(), onClick: row => openForm(row) },
        { label: "Xóa", danger: true, visible: () => isAdmin(), onClick: row => removeRow(row) }
    ]
});

async function load({ keepPage = false } = {}) {
    table.setLoading();
    try {
        const [tours, page] = await Promise.all([tourApi.getAll(), poiApi.search({ size: 100 })]);
        pois = page.items;
        table.setRows(tours, { keepPage });
    } catch (err) {
        table.setError(err.message);
    }
}

// Tổng quãng đường khi đi đúng thứ tự các điểm (backend tính bằng công thức Haversine)
async function showDistance(row) {
    try {
        const d = await tourApi.distance(row.id);
        toast.info(`${row.name}: ${d.stops} điểm, tổng ${formatNumber(Math.round(d.totalDistanceM))} m (đường chim bay).`, 6000);
    } catch (err) {
        toast.error(err.message);
    }
}

function openForm(tour = null) {
    const t = tour ?? { code: "", name: "", description: "", poiIds: [], estimatedMinutes: 30, isActive: true };
    let selected = [...t.poiIds];
    const content = document.createElement("div");
    content.innerHTML = `
        <div class="alert alert-danger mb-4" data-error></div>
        <div class="form-grid">
            <div class="form-group"><label class="form-label">Mã <span class="required">*</span></label><input class="input" name="code" value="${escapeHtml(t.code)}" required></div>
            <div class="form-group"><label class="form-label">Thời gian ước tính (phút)</label><input class="input" type="number" min="1" max="600" name="estimatedMinutes" value="${t.estimatedMinutes}"></div>
            <div class="form-group span-2"><label class="form-label">Tên <span class="required">*</span></label><input class="input" name="name" value="${escapeHtml(t.name)}" required></div>
            <div class="form-group span-2"><label class="form-label">Mô tả</label><input class="input" name="description" value="${escapeHtml(t.description ?? "")}"></div>
        </div>
        <div class="form-label">Thứ tự các điểm (bấm để thêm theo thứ tự đi)</div>
        <div class="chip-list" data-selected></div>
        <div class="chip-list">${pois.map(p => `<button type="button" class="btn btn-secondary btn-sm" data-add="${p.id}">+ ${escapeHtml(p.name)}</button>`).join("")}</div>
        <label class="checkbox mt-4"><input type="checkbox" name="isActive" ${t.isActive ? "checked" : ""}> <span>Hiển thị cho du khách</span></label>`;
    const footer = document.createElement("div");
    footer.style.display = "contents";
    footer.innerHTML = `<button type="button" class="btn btn-secondary" data-close>Hủy</button><button type="submit" class="btn btn-primary">Lưu</button>`;
    const modal = openModal({ title: tour ? `Sửa lộ trình ${tour.code}` : "Thêm lộ trình", content, footer, size: "lg", wrapInForm: true });

    const drawSelected = () => {
        content.querySelector("[data-selected]").innerHTML = selected.length
            ? selected.map((id, i) => `<span class="chip">${i + 1}. ${escapeHtml(poiName(id))} <button type="button" data-remove="${i}" aria-label="Bỏ">×</button></span>`).join("")
            : '<span class="text-muted">Chưa chọn điểm nào</span>';
    };
    content.addEventListener("click", e => {
        const add = e.target.closest("[data-add]");
        const remove = e.target.closest("[data-remove]");
        if (add && !selected.includes(Number(add.dataset.add))) selected.push(Number(add.dataset.add));
        if (remove) selected.splice(Number(remove.dataset.remove), 1);
        if (add || remove) drawSelected();
    });
    drawSelected();

    modal.form.addEventListener("submit", async e => {
        e.preventDefault();
        const f = modal.form;
        const body = {
            code: f.code.value.trim(),
            name: f.name.value.trim(),
            description: f.description.value.trim(),
            estimatedMinutes: Number(f.estimatedMinutes.value) || 30,
            isActive: f.isActive.checked,
            poiIds: selected
        };
        const button = f.querySelector('button[type="submit"]');
        setLoading(button, true, "Đang lưu...");
        try {
            if (tour) await tourApi.update(tour.id, body);
            else await tourApi.create(body);
            toast.success("Đã lưu lộ trình.");
            modal.close();
            await load({ keepPage: true });
        } catch (err) {
            content.querySelector("[data-error]").textContent = err.errors ? Object.values(err.errors).flat().join("\n") : err.message;
        } finally {
            setLoading(button, false);
        }
    });
}

async function removeRow(row) {
    const ok = await confirmDialog({ title: "Xóa lộ trình", message: `Xóa lộ trình "${row.name}"?`, confirmText: "Xóa", danger: true });
    if (!ok) return;
    try {
        await tourApi.remove(row.id);
        toast.success("Đã xóa.");
        await load({ keepPage: true });
    } catch (err) {
        toast.error(err.message);
    }
}

if (await initAdminLayout({ title: "Lộ trình" })) {
    $("btnAdd")?.addEventListener("click", () => openForm());
    $("btnReload").addEventListener("click", () => load({ keepPage: true }));
    const onSearch = debounce(text => table.search(text), 300);
    $("searchInput").addEventListener("input", e => onSearch(e.target.value));
    load();
}
