/*
 * Trang Tổng quan: GET /api/stats -> StatsController -> StatsService (đếm số liệu từ nhiều Repository).
 */
import { initAdminLayout } from "./layout.js";
import { statsApi } from "../../assets/js/api/statsApi.js";
import { localizationApi } from "../../assets/js/api/poiApi.js";
import { renderTable } from "../../assets/js/components/table.js";
import { confirmDialog } from "../../assets/js/components/modal.js";
import { toast } from "../../assets/js/components/toast.js";
import { escapeHtml, formatMoney, formatNumber, formatDateTime, localizationBadge, accessBadge } from "../../assets/js/core/utils.js";

const $ = id => document.getElementById(id);

function statCard(label, value, note = "") {
    return `<div class="stat-card"><div class="stat-label">${escapeHtml(label)}</div>
        <div class="stat-value">${escapeHtml(value)}</div>${note ? `<div class="text-muted text-sm">${escapeHtml(note)}</div>` : ""}</div>`;
}

function statusRows(counts, badge) {
    const entries = Object.entries(counts ?? {});
    if (!entries.length) return '<p class="text-muted">Chưa có dữ liệu.</p>';
    const total = entries.reduce((sum, [, n]) => sum + n, 0);
    return entries.map(([status, n]) => `
        <div class="status-row"><div style="flex:1">${badge(status)}
            <div class="bar-track"><div class="bar-fill" style="width:${Math.round(n * 100 / total)}%"></div></div></div>
            <b style="margin-left:16px">${formatNumber(n)}</b></div>`).join("");
}

// "2026-11-20" -> "T6, 20/11/2026"
function formatDay(isoDate) {
    const [y, m, d] = isoDate.split("-").map(Number);
    const weekday = ["CN", "T2", "T3", "T4", "T5", "T6", "T7"][new Date(y, m - 1, d).getDay()];
    return `${weekday}, ${String(d).padStart(2, "0")}/${String(m).padStart(2, "0")}/${y}`;
}

// Bảng số mã truy cập được tạo theo ngày: GET /api/stats/codes-by-day
async function loadCodesByDay() {
    const from = $("codesFrom").value || undefined;
    const to = $("codesTo").value || undefined;
    const rows = await statsApi.codesByDay(from, to);
    // Lần đầu (chưa chọn ngày) -> điền khoảng mặc định server trả về vào 2 ô ngày
    if (rows.length) {
        $("codesTo").value = rows[0].date;
        $("codesFrom").value = rows[rows.length - 1].date;
    }
    const sum = key => rows.reduce((total, r) => total + r[key], 0);
    const total = { isTotal: true, total: sum("total"), cash: sum("cash"), online: sum("online"), activated: sum("activated") };
    $("codesByDaySummary").textContent = `${formatNumber(total.total)} mã trong ${rows.length} ngày`;

    const num = key => r => r.isTotal ? `<b>${formatNumber(r[key])}</b>` : (r[key] ? formatNumber(r[key]) : '<span class="text-muted">0</span>');
    renderTable($("codesByDay"), {
        columns: [
            { key: "date", title: "Ngày", render: r => r.isTotal ? "<b>Tổng cộng</b>" : formatDay(r.date) },
            { key: "total", title: "Tổng số mã", align: "right", render: num("total") },
            { key: "cash", title: "Tiền mặt (quầy)", align: "right", render: num("cash") },
            { key: "online", title: "Online", align: "right", render: num("online") },
            { key: "activated", title: "Đã kích hoạt", align: "right", render: num("activated") }
        ],
        rows: [total, ...rows],
        emptyText: "Chưa có dữ liệu."
    });
}

async function load() {
    const [s, logs] = await Promise.all([statsApi.overview(), statsApi.chatLogs(10)]);
    $("statGrid").innerHTML = [
        statCard("Điểm đang hiển thị", `${s.activePois}/${s.totalPois}`),
        statCard("Ngôn ngữ đang bật", s.activeLanguages),
        statCard("Độ phủ bản dịch", `${s.localizationCoveragePercent}%`),
        statCard("Doanh thu vé", formatMoney(s.revenueVnd)),
        statCard("Câu hỏi chatbot", formatNumber(s.totalChats), `AI: ${s.aiProvider}`),
        statCard("Lượt tương tác", formatNumber(s.totalEvents)),
        statCard("Tác vụ nền đang chạy", s.backgroundTasks, "dịch + tạo audio")
    ].join("");
    $("coverageText").textContent = `${s.localizationCoveragePercent}% đã sẵn sàng`;
    $("locStatus").innerHTML = statusRows(s.localizationStatus, localizationBadge);
    $("sessionStatus").innerHTML = statusRows(s.accessSessions, accessBadge);
    $("eventsByLang").innerHTML = Object.entries(s.eventsByLang).map(([lang, n]) => `<span class="badge badge-secondary">${escapeHtml(lang)}: ${formatNumber(n)}</span>`).join(" ")
        || '<p class="text-muted">Chưa có dữ liệu.</p>';
    renderTable($("topPois"), {
        columns: [{ key: "name", title: "Điểm tham quan" }, { key: "count", title: "Lượt", align: "right", render: r => formatNumber(r.count) }],
        rows: s.topPois,
        emptyText: "Chưa có lượt tham quan nào."
    });
    renderTable($("chatLogs"), {
        columns: [
            { key: "createdAt", title: "Thời gian", width: "150px", render: r => formatDateTime(r.createdAt) },
            { key: "lang", title: "NN", width: "50px" },
            { key: "question", title: "Câu hỏi" },
            { key: "answer", title: "Trả lời", render: r => `<span class="text-sm">${escapeHtml(r.answer.slice(0, 180))}</span>` },
            { key: "usedLlm", title: "Nguồn", render: r => r.usedLlm ? '<span class="badge badge-primary">AI</span>' : '<span class="badge badge-secondary">Trích đoạn</span>' }
        ],
        rows: logs,
        emptyText: "Chưa có câu hỏi nào."
    });
}

if (await initAdminLayout({ title: "Tổng quan" })) {
    $("btnReload").addEventListener("click", () => {
        load().catch(err => toast.error(err.message));
        loadCodesByDay().catch(err => toast.error(err.message));
    });
    $("codesByDayForm").addEventListener("submit", event => {
        event.preventDefault();
        loadCodesByDay().catch(err => toast.error(err.message));
    });
    $("btnLocalizeAll")?.addEventListener("click", async () => {
        const ok = await confirmDialog({ title: "Dịch toàn bộ", message: "Xếp hàng dịch + tạo audio cho mọi điểm và lộ trình còn thiếu?\nQuá trình chạy nền, có thể mất vài phút.", confirmText: "Bắt đầu" });
        if (!ok) return;
        try {
            const r = await localizationApi.localizeAll(false);
            toast.success(`Đã xếp hàng ${r.queued} điểm tham quan.`);
        } catch (err) {
            toast.error(err.message);
        }
    });
    load().catch(err => toast.error(err.message));
    loadCodesByDay().catch(err => toast.error(err.message));
}
