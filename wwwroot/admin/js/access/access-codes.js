/*
 * Mã truy cập:
 *   POST /api/access/cash-codes   -> AccessService.CreateCashCodesAsync (phiên Paid, mã 6 ký tự ngẫu nhiên)
 *   GET  /api/access/sessions     -> phân trang phía server (Skip / Take)
 *   POST /api/access/sessions/{id}/revoke -> thu hồi, token du khách hết hiệu lực ngay
 */
import { initAdminLayout } from "../layout.js";
import { accessApi } from "../../../assets/js/api/accessApi.js";
import { renderTable } from "../../../assets/js/components/table.js";
import { renderPagination } from "../../../assets/js/components/pagination.js";
import { confirmDialog } from "../../../assets/js/components/modal.js";
import { toast } from "../../../assets/js/components/toast.js";
import { isAdmin } from "../../../assets/js/core/auth.js";
import { ACCESS_METHOD_LABELS } from "../../../assets/js/core/config.js";
import { escapeHtml, accessBadge, formatMoney, formatDateTime, setLoading } from "../../../assets/js/core/utils.js";

const $ = id => document.getElementById(id);
const PAGE_SIZE = 20;
let page = 1;

async function load() {
    try {
        const data = await accessApi.sessions({ status: $("statusFilter").value || undefined, page, size: PAGE_SIZE });
        renderTable($("tableContainer"), {
            columns: [
                { key: "code", title: "Mã", render: r => `<code>${escapeHtml(r.code)}</code>` },
                { key: "method", title: "Hình thức", render: r => escapeHtml(ACCESS_METHOD_LABELS[r.method] ?? r.method) },
                { key: "status", title: "Trạng thái", render: r => accessBadge(r.status) },
                { key: "amount", title: "Số tiền", align: "right", render: r => formatMoney(r.amount) },
                { key: "createdAt", title: "Tạo lúc", render: r => `<span class="text-sm">${formatDateTime(r.createdAt)}</span>` },
                { key: "accessExpiresAt", title: "Hết hạn dùng", render: r => `<span class="text-sm">${formatDateTime(r.accessExpiresAt)}</span>` },
                { key: "createdByName", title: "Nhân viên", render: r => escapeHtml(r.createdByName ?? "—") },
                { key: "note", title: "Ghi chú", render: r => `<span class="text-sm">${escapeHtml(r.note ?? "")}</span>` }
            ],
            rows: data.items,
            emptyText: "Chưa có phiên truy cập nào.",
            actions: [{
                label: "Thu hồi", danger: true,
                visible: r => isAdmin() && ["Pending", "Paid", "Active"].includes(r.status),
                onClick: r => revoke(r)
            }]
        });
        const totalPages = Math.max(1, Math.ceil(data.total / data.size));
        renderPagination($("pager"), {
            page, totalPages, total: data.total,
            from: data.total ? (page - 1) * data.size + 1 : 0,
            to: Math.min(page * data.size, data.total),
            onChange: p => { page = p; load(); }
        });
    } catch (err) {
        toast.error(err.message);
    }
}

async function revoke(row) {
    const ok = await confirmDialog({ title: "Thu hồi quyền truy cập", message: `Thu hồi mã ${row.code}? Du khách đang dùng sẽ bị đăng xuất.`, confirmText: "Thu hồi", danger: true });
    if (!ok) return;
    try {
        await accessApi.revoke(row.id);
        toast.success("Đã thu hồi.");
        await load();
    } catch (err) {
        toast.error(err.message);
    }
}

if (await initAdminLayout({ title: "Mã truy cập" })) {
    $("cashForm").addEventListener("submit", async e => {
        e.preventDefault();
        const button = $("btnCreate");
        setLoading(button, true, "Đang tạo...");
        try {
            const codes = await accessApi.createCashCodes(Number($("quantity").value) || 1, $("note").value.trim() || null);
            $("newCodes").innerHTML = codes.map(c => `<span class="code">${escapeHtml(c.code)}</span>`).join("");
            toast.success("Đưa mã cho khách nhập vào ứng dụng.");
            page = 1;
            await load();
        } catch (err) {
            toast.error(err.message);
        } finally {
            setLoading(button, false);
        }
    });
    $("statusFilter").addEventListener("change", () => { page = 1; load(); });
    load();
}
