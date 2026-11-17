/*
 * ============================================================================
 *  pagination.js  -  PHÂN TRANG
 * ============================================================================
 *
 *  Khi danh sách có 200 khách hàng, không ai muốn thấy cả 200 dòng một lúc.
 *  Phân trang = chia danh sách thành từng "trang", mỗi trang vài dòng (ví dụ 10),
 *  phía dưới có các nút  ‹ 1 2 3 … 20 ›  để chuyển trang.
 *
 *  Nhiều API trả về CẢ danh sách một lần, nên ta tự cắt trang ở
 *  phía trình duyệt. File này có 2 hàm:
 *
 *  1) paginate(danhSach, trangSo, soDongMoiTrang)  -  CẮT lấy các dòng của 1 trang
 *
 *      const result = paginate(customers, 2, 10);
 *      // result.items      -> 10 khách hàng của trang 2 (từ người thứ 11 đến 20)
 *      // result.page       -> 2
 *      // result.totalPages -> tổng số trang
 *      // result.total      -> tổng số khách hàng
 *
 *  2) renderPagination(oChua, { page, totalPages, total, pageSize, onChange })
 *     VẼ thanh nút chuyển trang vào một thẻ div. Bấm nút -> gọi onChange(trangMoi).
 *
 *  Thường thì bạn KHÔNG cần gọi 2 hàm này trực tiếp: table.js (createDataTable)
 *  đã dùng sẵn. Chỉ gọi tay khi tự vẽ danh sách kiểu khác (ví dụ dạng lưới thẻ).
 * ============================================================================
 */

import { PAGE_SIZE } from "../core/config.js";
import { formatNumber } from "../core/utils.js";

/**
 * Cắt lấy các phần tử thuộc một trang.
 *
 * Ví dụ 25 phần tử, mỗi trang 10:
 *   trang 1 -> phần tử 0..9,  trang 2 -> 10..19,  trang 3 -> 20..24
 * Công thức: bắt đầu từ (trang - 1) * soDong, lấy soDong phần tử.
 *
 * Nếu trang yêu cầu lớn hơn số trang có thật (ví dụ vừa xóa dòng cuối của
 * trang cuối), tự lùi về trang cuối cùng.
 */
export function paginate(items = [], page = 1, pageSize = PAGE_SIZE) {
    const total = items.length;
    // Math.ceil = làm tròn LÊN: 25 / 10 = 2.5 -> 3 trang. Ít nhất luôn có 1 trang.
    const totalPages = Math.max(1, Math.ceil(total / pageSize));
    // Giữ số trang trong khoảng 1..totalPages
    const safePage = Math.min(Math.max(1, page), totalPages);

    const start = (safePage - 1) * pageSize;
    return {
        items: items.slice(start, start + pageSize), // slice = cắt một đoạn của mảng
        page: safePage,
        totalPages,
        total,
        pageSize,
        from: total === 0 ? 0 : start + 1,             // đang xem từ dòng thứ mấy
        to: Math.min(start + pageSize, total)          // đến dòng thứ mấy
    };
}

/**
 * Tính ra danh sách các nút số trang cần hiện.
 * Nhiều trang quá thì không hiện hết mà dùng "…" cho gọn, ví dụ đang ở trang 7/20:
 *     1 … 6 7 8 … 20
 * Luôn hiện: trang đầu, trang cuối, trang hiện tại và 1 trang mỗi bên.
 */
function getPageNumbers(page, totalPages) {
    if (totalPages <= 7) {
        // Ít trang thì hiện hết: [1, 2, 3, 4, 5]
        return Array.from({ length: totalPages }, (_, i) => i + 1);
    }

    // Mảng chứa cả số (1, 2, 3) lẫn chữ ("…") -> ghi chú cho VS Code biết điều đó
    /** @type {Array<number|string>} */
    const pages = [1];
    const start = Math.max(2, page - 1);
    const end = Math.min(totalPages - 1, page + 1);

    if (start > 2) pages.push("…");
    for (let p = start; p <= end; p++) pages.push(p);
    if (end < totalPages - 1) pages.push("…");

    pages.push(totalPages);
    return pages;
}

/**
 * Vẽ thanh phân trang.
 *
 * @param {HTMLElement|null} container - thẻ div sẽ chứa thanh phân trang
 * @param {object} [state]
 * @param {number} [state.page]       - trang đang xem
 * @param {number} [state.totalPages] - tổng số trang
 * @param {number} [state.total]     - tổng số dòng (để hiện "Hiển thị 11–20 / 57")
 * @param {number} [state.from]      - dòng bắt đầu
 * @param {number} [state.to]        - dòng kết thúc
 * @param {(page: number) => void} [state.onChange] - hàm được gọi khi bấm sang trang khác, nhận số trang mới
 *
 * (Ghi chú: các dòng @param không chỉ để người đọc - VS Code cũng đọc chúng để biết
 *  kiểu dữ liệu. Dấu [ ] quanh tên = không bắt buộc; "A|null" = kiểu A hoặc null.)
 *
 * Mẹo: có thể đưa thẳng kết quả của paginate() vào, chỉ cần thêm onChange:
 *     renderPagination(div, { ...paginate(list, page), onChange: p => { page = p; draw(); } });
 */
export function renderPagination(container, { page = 1, totalPages = 1, total, from, to, onChange } = {}) {
    if (!container) return;

    // Không có dữ liệu thì không cần thanh phân trang
    if (!total) {
        container.innerHTML = "";
        return;
    }

    const info = total !== undefined
        ? `<div class="pagination-info">Hiển thị ${formatNumber(from)}–${formatNumber(to)} / ${formatNumber(total)}</div>`
        : "<div></div>";

    // Chỉ có 1 trang thì chỉ hiện dòng thông tin, không cần các nút
    let buttons = "";
    if (totalPages > 1) {
        const numbers = getPageNumbers(page, totalPages).map(p =>
            p === "…"
                ? `<span class="page-ellipsis">…</span>`
                : `<button type="button" class="page-btn ${p === page ? "is-active" : ""}" data-page="${p}"
                        ${p === page ? 'aria-current="page"' : ""}>${p}</button>`
        ).join("");

        buttons = `
            <div class="pagination-pages">
                <button type="button" class="page-btn" data-page="${page - 1}" ${page <= 1 ? "disabled" : ""} aria-label="Trang trước">‹</button>
                ${numbers}
                <button type="button" class="page-btn" data-page="${page + 1}" ${page >= totalPages ? "disabled" : ""} aria-label="Trang sau">›</button>
            </div>`;
    }

    container.className = "pagination";
    container.innerHTML = info + buttons;

    // Gắn sự kiện bấm. Dùng "onclick =" (gán đè) thay vì addEventListener để mỗi lần
    // vẽ lại không bị gắn thêm một lần nữa (gắn nhiều lần -> bấm 1 lần chạy nhiều lần).
    container.onclick = event => {
        // event.target = thứ bị bấm. Ghi chú @type để VS Code biết đó là thẻ HTML (có hàm closest).
        const target = /** @type {HTMLElement} */ (event.target);
        const button = /** @type {HTMLButtonElement|null} */ (target.closest("button[data-page]"));
        if (!button || button.disabled) return;
        const newPage = Number(button.dataset.page);
        if (newPage !== page && typeof onChange === "function") onChange(newPage);
    };
}
