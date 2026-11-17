/*
 * ============================================================================
 *  table.js  -  VẼ BẢNG DỮ LIỆU
 * ============================================================================
 *
 *  Gần như trang admin nào cũng có một bảng: danh sách khách hàng, đơn hàng,
 *  sản phẩm... Thay vì mỗi trang tự viết vòng lặp tạo <tr><td>, ta chỉ cần
 *  MÔ TẢ bảng có những cột nào, nút nào - file này vẽ giúp.
 *
 *  Có 2 cách dùng:
 *
 *  ── CÁCH 1 (khuyên dùng): createDataTable - bảng "trọn gói" ──────────────
 *     Có sẵn: tìm kiếm + phân trang + báo "đang tải" / "không có dữ liệu".
 *
 *      const table = createDataTable({
 *          container: document.getElementById("tableContainer"),
 *          paginationContainer: document.getElementById("paginationContainer"),
 *          columns: [
 *              { key: "id", title: "Mã", width: "70px" },
 *              { key: "email", title: "Email" },
 *              { key: "totalAmount", title: "Tổng tiền", align: "right",
 *                render: row => formatMoney(row.totalAmount) },   // tự định dạng ô
 *              { key: "status", title: "Trạng thái",
 *                render: row => orderStatusBadge(row.status) }
 *          ],
 *          actions: [
 *              { label: "Sửa", onClick: row => openEdit(row) },
 *              { label: "Xóa", danger: true, onClick: row => remove(row) },
 *              { label: "Duyệt", visible: row => row.status === "Pending", onClick: ... }
 *          ],
 *          searchFields: ["email", "fullName"],   // tìm kiếm theo những cột nào
 *      });
 *
 *      table.setLoading();                // hiện "Đang tải..."
 *      table.setRows(await get("/Order")); // đổ dữ liệu vào, tự vẽ + phân trang
 *      table.search("nguyen");            // lọc theo từ khóa (gắn vào ô tìm kiếm)
 *      table.setError("Lỗi tải dữ liệu"); // hiện dòng báo lỗi
 *
 *  ── CÁCH 2: renderTable - chỉ vẽ bảng, không tìm kiếm / phân trang ───────
 *      renderTable(container, { columns, rows, actions });
 *
 *  AN TOÀN: giá trị ô bình thường được tự động escapeHtml (xem utils.js).
 *  Nhưng nếu bạn tự viết "render", thì phần chữ lấy từ dữ liệu bên trong
 *  render phải tự bọc escapeHtml(...) nhé. Các hàm formatMoney, formatDate,
 *  orderStatusBadge... trả về chữ an toàn sẵn rồi.
 * ============================================================================
 */

import { PAGE_SIZE } from "../core/config.js";
import { escapeHtml } from "../core/utils.js";
import { paginate, renderPagination } from "./pagination.js";

/* ============================================================================
 *  PHẦN 1: CÁC HÀM PHỤ
 * ========================================================================== */

/**
 * Lấy giá trị theo "đường dẫn" có dấu chấm.
 * Ví dụ row = { store: { name: "CN Quận 1" } }
 *   getValue(row, "store.name") -> "CN Quận 1"
 * Nhờ vậy cột có thể ghi key: "store.name" mà không cần viết render.
 */
export function getValue(row, key) {
    if (!key) return undefined;
    return key.split(".").reduce((obj, part) => (obj == null ? undefined : obj[part]), row);
}

/**
 * Bỏ dấu tiếng Việt và đổi về chữ thường, để tìm kiếm dễ hơn:
 *   "Nguyễn Văn Đức" -> "nguyen van duc"
 * Gõ "duc" hay "Đức" đều tìm ra.
 *
 * normalize("NFD") tách chữ có dấu thành "chữ gốc + dấu" (ví dụ "ễ" -> "e" + dấu),
 * rồi replace xóa phần dấu đi. Riêng chữ "đ" không tách được nên đổi tay.
 */
export function normalizeText(value) {
    return String(value ?? "")
        .normalize("NFD")
        .replace(/[̀-ͯ]/g, "")
        .replace(/đ/g, "d")
        .replace(/Đ/g, "D")
        .toLowerCase()
        .trim();
}

/** Tạo thuộc tính class / style cho một ô theo mô tả cột. */
function cellAttributes(column) {
    const classes = [];
    if (column.align === "right") classes.push("col-right");
    if (column.align === "center") classes.push("col-center");
    if (column.className) classes.push(column.className);
    const classAttr = classes.length ? ` class="${classes.join(" ")}"` : "";
    const styleAttr = column.width ? ` style="width:${escapeHtml(column.width)}"` : "";
    return classAttr + styleAttr;
}

/* ============================================================================
 *  PHẦN 2: VẼ BẢNG
 * ========================================================================== */

/**
 * Vẽ một dòng thông báo chiếm cả chiều ngang bảng (Đang tải / Không có dữ liệu / Lỗi).
 * Vẫn vẽ hàng tiêu đề cột để bảng không bị "nhảy" khi dữ liệu về.
 */
export function renderTableMessage(container, columns = [], message = "", { hasActions = false, isError = false } = {}) {
    const colspan = columns.length + (hasActions ? 1 : 0) || 1;
    const header = columns.map(col => `<th${cellAttributes(col)}>${escapeHtml(col.title ?? "")}</th>`).join("")
        + (hasActions ? `<th class="table-actions"></th>` : "");
    container.innerHTML = `
        <div class="table-wrapper">
            <table class="table">
                <thead><tr>${header}</tr></thead>
                <tbody>
                    <tr><td class="table-message ${isError ? "text-danger" : ""}" colspan="${colspan}">${message}</td></tr>
                </tbody>
            </table>
        </div>`;
}

/**
 * Vẽ bảng từ dữ liệu.
 *
 * @param {HTMLElement|null} container - thẻ div sẽ chứa bảng
 * @param {object} [options]
 * @param {any[]} [options.columns] - danh sách cột:
 *        { key, title, render?(row, index), align?: "right"|"center", width?, className? }
 * @param {any[]} [options.rows]    - danh sách dữ liệu (mảng các object)
 * @param {any[]} [options.actions] - danh sách nút ở cuối mỗi dòng:
 *        { label, onClick(row), danger?, visible?(row), title?, className? }
 * @param {string} [options.emptyText] - chữ hiện khi không có dòng nào
 *
 * (Ghi chú: các dòng @param không chỉ để người đọc - VS Code cũng đọc chúng để biết
 *  kiểu dữ liệu. Dấu [ ] quanh tên = không bắt buộc; "A|null" = kiểu A hoặc null.)
 */
export function renderTable(container, { columns = [], rows = [], actions = [], emptyText = "Không có dữ liệu." } = {}) {
    if (!container) return;
    const hasActions = actions.length > 0;

    if (!rows.length) {
        renderTableMessage(container, columns, escapeHtml(emptyText), { hasActions });
        container.onclick = null;
        return;
    }

    // ---- Hàng tiêu đề ----
    const headerHtml = columns
        .map(col => `<th${cellAttributes(col)}>${escapeHtml(col.title ?? "")}</th>`)
        .join("") + (hasActions ? `<th class="table-actions">Thao tác</th>` : "");

    // ---- Các dòng dữ liệu ----
    const bodyHtml = rows.map((row, rowIndex) => {
        const cells = columns.map(col => {
            // Có render thì dùng render (tự định dạng), không thì lấy giá trị và escape
            let content;
            if (typeof col.render === "function") {
                content = col.render(row, rowIndex);
            } else {
                const value = getValue(row, col.key);
                content = value === null || value === undefined || value === "" ? "—" : escapeHtml(value);
            }
            return `<td${cellAttributes(col)}>${content ?? ""}</td>`;
        }).join("");

        // Các nút thao tác. Mỗi nút ghi lại "dòng số mấy" (data-row) và "nút số mấy" (data-action)
        // để khi bấm còn biết là bấm nút nào của dòng nào.
        let actionCell = "";
        if (hasActions) {
            const buttons = actions.map((action, actionIndex) => {
                if (typeof action.visible === "function" && !action.visible(row)) return "";
                const classes = ["btn", "btn-ghost", "btn-sm"];
                if (action.danger) classes.push("btn-danger-text");
                if (action.className) classes.push(action.className);
                const title = action.title ? ` title="${escapeHtml(action.title)}"` : "";
                return `<button type="button" class="${classes.join(" ")}" data-row="${rowIndex}" data-action="${actionIndex}"${title}>${escapeHtml(action.label)}</button>`;
            }).join("");
            actionCell = `<td class="table-actions">${buttons}</td>`;
        }

        return `<tr>${cells}${actionCell}</tr>`;
    }).join("");

    container.innerHTML = `
        <div class="table-wrapper">
            <table class="table">
                <thead><tr>${headerHtml}</tr></thead>
                <tbody>${bodyHtml}</tbody>
            </table>
        </div>`;

    // ---- Bắt sự kiện bấm nút ----
    // Thay vì gắn sự kiện cho TỪNG nút (100 dòng x 3 nút = 300 lần), ta gắn MỘT lần
    // cho cả bảng, rồi xem người dùng bấm trúng nút nào (gọi là "ủy quyền sự kiện").
    container.onclick = event => {
        // event.target = thứ bị bấm. Ghi chú @type để VS Code biết đó là thẻ HTML (có hàm closest).
        const target = /** @type {HTMLElement} */ (event.target);
        const button = /** @type {HTMLButtonElement|null} */ (target.closest("button[data-action]"));
        if (!button || !container.contains(button)) return;
        const row = rows[Number(button.dataset.row)];
        const action = actions[Number(button.dataset.action)];
        action?.onClick?.(row, button);
    };
}

/* ============================================================================
 *  PHẦN 3: BẢNG TRỌN GÓI (tìm kiếm + phân trang)
 * ========================================================================== */

/**
 * Tạo bảng có tìm kiếm và phân trang.
 *
 * @param {object} [options]
 * @param {HTMLElement|null} [options.container]           - nơi vẽ bảng
 * @param {HTMLElement|null} [options.paginationContainer] - nơi vẽ thanh phân trang (bỏ trống = không phân trang)
 * @param {any[]} [options.columns]
 * @param {any[]} [options.actions]
 * @param {string[]|null} [options.searchFields] - các key dùng để tìm kiếm.
 *        Bỏ trống = tìm trên tất cả các cột có key.
 * @param {number} [options.pageSize]   - số dòng mỗi trang (mặc định PAGE_SIZE trong config.js)
 * @param {string} [options.emptyText]  - chữ khi danh sách trống
 * @param {((a: any, b: any) => number)|null} [options.sortBy] - hàm sắp xếp, ví dụ (a, b) => b.id - a.id (mới nhất lên đầu)
 *
 * (Ghi chú: các dòng @param không chỉ để người đọc - VS Code cũng đọc chúng để biết
 *  kiểu dữ liệu. Dấu [ ] quanh tên = không bắt buộc; "A|null" = kiểu A hoặc null.)
 *
 * @returns một object điều khiển bảng: setRows, setLoading, setError, search, setFilter, refresh, getRows
 */
export function createDataTable({
    container,
    paginationContainer = null,
    columns = [],
    actions = [],
    searchFields = null,
    pageSize = PAGE_SIZE,
    emptyText = "Không có dữ liệu.",
    sortBy = null
} = {}) {
    // Không tìm thấy chỗ để vẽ bảng (thường do gõ sai id trong HTML) -> báo lỗi rõ ràng
    // ra Console (F12) ngay từ đầu, thay vì để lỗi khó hiểu xảy ra ở chỗ khác.
    if (!container) {
        throw new Error("createDataTable: không tìm thấy thẻ chứa bảng (container). Kiểm tra lại id trong HTML.");
    }
    // Sau dòng kiểm tra trên thì chắc chắn có thẻ -> cất vào hằng "box" để dùng bên dưới
    // (VS Code nhờ vậy cũng biết box không thể là null).
    const box = container;

    // "Trạng thái" bên trong bảng - các biến này được giữ lại giữa các lần vẽ
    /** @type {any[]} */
    let allRows = [];          // toàn bộ dữ liệu (ghi chú any[] = mảng chứa dữ liệu kiểu gì cũng được)
    let keyword = "";          // từ khóa đang tìm (đã bỏ dấu)
    /** @type {((row: any) => boolean)|null} */
    let extraFilter = null;    // bộ lọc thêm (ví dụ lọc theo trạng thái)
    let page = 1;              // trang đang xem

    const fields = searchFields ?? columns.map(c => c.key).filter(Boolean);

    /** Lọc dữ liệu theo từ khóa + bộ lọc thêm. */
    function getFilteredRows() {
        let rows = allRows;

        if (typeof extraFilter === "function") {
            rows = rows.filter(extraFilter);
        }

        if (keyword) {
            rows = rows.filter(row =>
                // Giữ lại dòng nếu CÓ ÍT NHẤT MỘT ô chứa từ khóa
                fields.some(field => normalizeText(getValue(row, field)).includes(keyword))
            );
        }

        return rows;
    }

    /** Vẽ lại bảng + thanh phân trang theo trạng thái hiện tại. */
    function draw() {
        const filtered = getFilteredRows();

        // Có ô tìm kiếm mà không ra kết quả -> báo khác với "chưa có dữ liệu"
        const message = keyword && allRows.length ? "Không tìm thấy kết quả phù hợp." : emptyText;

        if (paginationContainer) {
            const result = paginate(filtered, page, pageSize);
            page = result.page; // paginate có thể đã lùi trang (ví dụ vừa xóa dòng cuối)
            renderTable(box, { columns, rows: result.items, actions, emptyText: message });
            renderPagination(paginationContainer, {
                ...result,
                onChange: newPage => {
                    page = newPage;
                    draw();
                    // Cuộn lên đầu bảng cho người dùng thấy trang mới
                    box.scrollIntoView({ behavior: "smooth", block: "start" });
                }
            });
        } else {
            renderTable(box, { columns, rows: filtered, actions, emptyText: message });
        }
    }

    return {
        /**
         * Đổ dữ liệu mới vào bảng và vẽ lại.
         * @param {any[]} rows
         * @param {{ keepPage?: boolean }} [opts] - { keepPage: true } để ở nguyên trang hiện tại
         *        (dùng sau khi Sửa/Xóa, để không bị nhảy về trang 1)
         */
        setRows(rows, { keepPage = false } = {}) {
            allRows = Array.isArray(rows) ? [...rows] : [];
            if (typeof sortBy === "function") allRows.sort(sortBy);
            if (!keepPage) page = 1;
            draw();
        },

        /** Hiện dòng "Đang tải..." (gọi trước khi gọi API). */
        setLoading(text = "Đang tải dữ liệu...") {
            renderTableMessage(box, columns, `<span class="spinner"></span> ${escapeHtml(text)}`, { hasActions: actions.length > 0 });
            if (paginationContainer) paginationContainer.innerHTML = "";
        },

        /** Hiện dòng báo lỗi màu đỏ (gọi khi API lỗi). */
        setError(message = "Không tải được dữ liệu.") {
            renderTableMessage(box, columns, escapeHtml(message), { hasActions: actions.length > 0, isError: true });
            if (paginationContainer) paginationContainer.innerHTML = "";
        },

        /** Tìm kiếm theo từ khóa. Gắn vào ô tìm kiếm (nên kèm debounce trong utils.js). */
        search(text) {
            keyword = normalizeText(text);
            page = 1;
            draw();
        },

        /**
         * Thêm bộ lọc riêng. Ví dụ chỉ hiện đơn "Pending":
         *     table.setFilter(row => row.status === "Pending");
         *     table.setFilter(null);   // bỏ lọc
         */
        setFilter(fn) {
            extraFilter = fn;
            page = 1;
            draw();
        },

        /** Vẽ lại mà không đổi dữ liệu. */
        refresh: draw,

        /** Lấy lại toàn bộ dữ liệu đang giữ (chưa lọc). */
        getRows: () => [...allRows]
    };
}
