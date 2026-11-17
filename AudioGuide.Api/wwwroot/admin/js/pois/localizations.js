/*
 * Bản dịch + audio của 1 điểm.
 *   GET    /api/pois/{id}/localizations         -> LocalizationService.OverviewAsync
 *   POST   /api/pois/{id}/localize              -> xếp hàng dịch nền (BackgroundTaskQueue)
 *   PUT    /api/pois/{id}/localizations/{lang}  -> sửa tay (không bị dịch máy ghi đè)
 *   DELETE /api/pois/{id}/localizations[/{lang}]
 * Đang có bản dịch chờ / đang xử lý -> trang tự tải lại mỗi 3 giây.
 */
import { initAdminLayout, setPageTitle } from "../layout.js";
import { poiApi, localizationApi } from "../../../assets/js/api/poiApi.js";
import { renderTable } from "../../../assets/js/components/table.js";
import { confirmDialog, formModal } from "../../../assets/js/components/modal.js";
import { toast } from "../../../assets/js/components/toast.js";
import { isAdmin } from "../../../assets/js/core/auth.js";
import { TRANSLATED_BY_LABELS } from "../../../assets/js/core/config.js";
import { escapeHtml, getIdFromUrl, localizationBadge } from "../../../assets/js/core/utils.js";

const $ = id => document.getElementById(id);
const poiId = getIdFromUrl();
let poi = null;
let timer = null;

async function load() {
    clearTimeout(timer);
    let rows;
    try {
        rows = await localizationApi.overview(poiId);
    } catch (err) {
        $("tableContainer").innerHTML = `<p class="card-body text-danger">${escapeHtml(err.message)}</p>`;
        return;
    }
    const busy = rows.some(r => r.status === "Pending" || r.status === "Processing");
    $("busyText").textContent = busy ? "Đang dịch / tạo audio ở nền, trang tự cập nhật..." : `${rows.filter(r => r.status === "Ready" && r.isFresh).length}/${rows.length} ngôn ngữ đã sẵn sàng`;
    renderTable($("tableContainer"), {
        columns: [
            { key: "lang", title: "Ngôn ngữ", render: r => `<b>${escapeHtml(r.lang)}</b> <span class="text-muted text-sm">${escapeHtml(r.languageName)}</span>` },
            {
                key: "status", title: "Trạng thái", render: r => localizationBadge(r.status)
                    + (r.status === "Ready" && !r.isFresh ? ' <span class="badge badge-warning">nội dung cũ</span>' : "")
                    + (r.error ? `<div class="text-danger text-sm">${escapeHtml(r.error)}</div>` : "")
            },
            { key: "translatedBy", title: "Nguồn", render: r => escapeHtml(TRANSLATED_BY_LABELS[r.translatedBy] ?? "—") },
            { key: "name", title: "Tên", render: r => `<span class="text-sm">${escapeHtml(r.name ?? "—")}</span>` },
            { key: "audioUrl", title: "Audio", render: r => r.audioUrl ? `<audio controls preload="none" src="${escapeHtml(r.audioUrl)}"></audio>` : '<span class="text-muted">—</span>' }
        ],
        rows,
        actions: [
            { label: "Sửa tay", visible: r => isAdmin() && r.lang !== "vi", onClick: r => manualEdit(r) },
            { label: "Dịch lại", visible: () => isAdmin(), onClick: r => queue([r.lang], true) },
            { label: "Xóa", danger: true, visible: r => isAdmin() && r.status !== "Missing", onClick: r => removeOne(r) }
        ]
    });
    if (busy) timer = setTimeout(load, 3000);
}

async function queue(languages, force) {
    try {
        const r = await localizationApi.localize(poiId, languages, force);
        toast.success(`Đã xếp hàng ${r.queued} ngôn ngữ.`);
        await load();
    } catch (err) {
        toast.error(err.message);
    }
}

function manualEdit(row) {
    formModal({
        title: `Sửa tay bản dịch "${row.lang}"`,
        description: `Bản sửa tay không bị dịch máy ghi đè. Gốc tiếng Việt: ${poi?.name ?? ""}`,
        size: "lg",
        fields: [
            { name: "name", label: `Tên (${row.lang})`, required: true },
            { name: "description", label: `Nội dung (${row.lang})`, type: "textarea", required: true },
            { name: "regenerateAudio", type: "checkbox", checkboxLabel: "Tạo lại audio" }
        ],
        values: { name: row.name ?? "", description: row.description ?? "", regenerateAudio: true },
        onSubmit: async data => {
            await localizationApi.updateManual(poiId, row.lang, data);
            toast.success("Đã lưu bản dịch.");
            await load();
        }
    });
}

async function removeOne(row) {
    const note = row.lang === "vi" ? "\nĐây là bản gốc tiếng Việt: du khách chọn tiếng Việt sẽ không có audio cho tới khi dịch lại." : "";
    const ok = await confirmDialog({ title: "Xóa bản dịch", message: `Xóa bản dịch "${row.lang}"?${note}`, confirmText: "Xóa", danger: true });
    if (!ok) return;
    try {
        await localizationApi.removeOne(poiId, row.lang);
        toast.success(`Đã xóa bản dịch ${row.lang}.`);
        await load();
    } catch (err) {
        toast.error(err.message);
    }
}

if (await initAdminLayout({ title: "Bản dịch và audio" })) {
    if (!poiId) {
        window.location.replace("/admin/pois/pois.html");
    } else {
        try {
            poi = await poiApi.getById(poiId);
            $("pageHeading").textContent = `Bản dịch: ${poi.name}`;
            setPageTitle(`Bản dịch: ${poi.name}`);
        } catch (err) {
            toast.error(err.message);
        }
        $("btnMissing")?.addEventListener("click", () => queue(null, false));
        $("btnForce")?.addEventListener("click", async () => {
            if (await confirmDialog({ title: "Dịch lại toàn bộ", message: "Ghi đè cả các bản admin đã sửa tay?", confirmText: "Ghi đè", danger: true }))
                queue(null, true);
        });
        $("btnDeleteAll")?.addEventListener("click", async () => {
            const ok = await confirmDialog({
                title: "Xóa toàn bộ bản dịch",
                message: `Xóa TẤT CẢ bản dịch của "${poi?.name}"?\n(Bước bắt buộc trước khi xóa điểm - khóa ngoại không cho xóa điểm khi còn bản dịch.)`,
                confirmText: "Xóa hết",
                danger: true
            });
            if (!ok) return;
            try {
                const r = await localizationApi.removeAll(poiId);
                toast.success(`Đã xóa ${r.deleted} bản dịch.`);
                await load();
            } catch (err) {
                toast.error(err.message);
            }
        });
        load();
    }
}
