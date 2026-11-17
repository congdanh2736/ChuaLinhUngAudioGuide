/*
 * ============================================================================
 *  modal.js  -  HỘP THOẠI NỔI GIỮA MÀN HÌNH
 * ============================================================================
 *
 *  "Modal" là hộp thoại hiện đè lên giữa trang, phía sau có lớp nền tối.
 *  File này có 3 hàm, từ dễ đến linh hoạt:
 *
 *  1) confirmDialog(...)  -  HỎI CÓ / KHÔNG (thay cho confirm() của trình duyệt)
 *
 *      const ok = await confirmDialog({
 *          title: "Xóa nhân viên",
 *          message: "Bạn có chắc muốn xóa nhân viên này?",
 *          confirmText: "Xóa",
 *          danger: true            // nút xác nhận màu đỏ
 *      });
 *      if (!ok) return;            // người dùng bấm Hủy
 *
 *  2) formModal(...)  -  HỘP THOẠI CÓ FORM để THÊM / SỬA (dùng nhiều nhất)
 *     Chỉ cần mô tả các ô nhập, file này tự vẽ form, tự gom dữ liệu,
 *     tự hiện lỗi từ server dưới đúng ô bị sai.
 *
 *      formModal({
 *          title: "Thêm danh mục",
 *          fields: [
 *              { name: "name", label: "Tên danh mục", required: true },
 *              { name: "parentCategoryId", label: "Danh mục cha", type: "select",
 *                valueType: "number", options: [{ value: 1, label: "Áo" }] }
 *          ],
 *          values: {},                         // dữ liệu điền sẵn (khi SỬA)
 *          submitText: "Lưu",
 *          onSubmit: async data => {           // data = { name: "...", parentCategoryId: 1 }
 *              await post("/Category", data);  // lỗi ở đây -> tự hiện trong hộp thoại
 *              toast.success("Đã thêm!");
 *              reload();
 *          }                                   // chạy xong không lỗi -> hộp thoại tự đóng
 *      });
 *
 *  3) openModal(...)  -  HỘP THOẠI TRỐNG, tự đưa nội dung HTML vào
 *     (dùng khi cần hiển thị gì đó đặc biệt, ví dụ xem chi tiết).
 *
 *      const modal = openModal({ title: "Chi tiết", content: "<p>...</p>" });
 *      modal.close();  // đóng bằng code
 *
 *  Người dùng đóng hộp thoại bằng: nút ✕, nút Hủy, phím Esc, hoặc bấm ra
 *  vùng tối bên ngoài.
 * ============================================================================
 */

import { escapeHtml, setLoading } from "../core/utils.js";

/* ============================================================================
 *  PHẦN 1: HỘP THOẠI CƠ BẢN
 * ========================================================================== */

/**
 * Mở một hộp thoại.
 *
 * @param {object} [options]
 * @param {string} [options.title]        - tiêu đề
 * @param {string|Node} [options.content] - nội dung: chuỗi HTML hoặc một phần tử đã tạo sẵn
 * @param {string|Node|null} [options.footer] - phần chân (thường chứa các nút). Bỏ trống = không có chân.
 * @param {"sm"|"md"|"lg"} [options.size] - cỡ hộp thoại: nhỏ / vừa (mặc định) / lớn
 * @param {Function} [options.onClose]    - hàm chạy SAU KHI hộp thoại đóng
 * @param {boolean} [options.wrapInForm]  - bọc thân + chân trong thẻ <form> (formModal dùng)
 *
 * (Ghi chú: các dòng @param không chỉ để người đọc - VS Code cũng đọc chúng để biết
 *  kiểu dữ liệu. Dấu [ ] quanh tên = không bắt buộc; "A|null" = kiểu A hoặc null.)
 *
 * @returns {{ element, body, footer, form, close }} - các phần của hộp thoại để dùng tiếp:
 *          element = hộp trắng, body = phần thân, close() = hàm đóng hộp thoại.
 *
 * LƯU Ý: nếu "content" là chuỗi HTML có chứa dữ liệu người dùng nhập,
 * hãy bọc dữ liệu đó qua escapeHtml() trước (xem giải thích trong utils.js).
 */
export function openModal({ title = "", content = "", footer = null, size = "md", onClose, wrapInForm = false } = {}) {
    // ---- Lớp nền tối phủ toàn màn hình ----
    const backdrop = document.createElement("div");
    backdrop.className = "modal-backdrop";

    // ---- Hộp trắng ở giữa ----
    const modal = document.createElement("div");
    modal.className = `modal modal-${size}`;
    // role="dialog" + aria-modal: báo cho phần mềm đọc màn hình biết đây là hộp thoại
    modal.setAttribute("role", "dialog");
    modal.setAttribute("aria-modal", "true");

    // ---- Đầu hộp thoại: tiêu đề + nút ✕ ----
    const header = document.createElement("div");
    header.className = "modal-header";
    header.innerHTML = `
        <h2 class="modal-title">${escapeHtml(title)}</h2>
        <button type="button" class="modal-close" aria-label="Đóng">&times;</button>
    `;

    // ---- Thân hộp thoại ----
    const body = document.createElement("div");
    body.className = "modal-body";
    fillContent(body, content);

    // ---- Chân hộp thoại (có thể không có) ----
    let footerElement = null;
    if (footer) {
        footerElement = document.createElement("div");
        footerElement.className = "modal-footer";
        fillContent(footerElement, footer);
    }

    // ---- Ghép các phần lại ----
    // Nếu wrapInForm = true: thân và chân nằm trong <form>, để nút "Lưu" (type=submit)
    // ở chân và các ô nhập ở thân thuộc CÙNG một form.
    let form = null;
    modal.appendChild(header);
    if (wrapInForm) {
        form = document.createElement("form");
        form.noValidate = false; // vẫn để trình duyệt tự kiểm tra ô "required", email...
        form.append(body);
        if (footerElement) form.append(footerElement);
        modal.appendChild(form);
    } else {
        modal.append(body);
        if (footerElement) modal.append(footerElement);
    }
    backdrop.appendChild(modal);

    // ---- Các cách đóng hộp thoại ----
    let closed = false;
    function close() {
        if (closed) return;     // tránh đóng 2 lần
        closed = true;
        document.removeEventListener("keydown", onKeyDown);
        backdrop.remove();
        // Chỉ mở khóa cuộn trang khi không còn hộp thoại nào khác đang mở
        if (!document.querySelector(".modal-backdrop")) {
            document.body.classList.remove("modal-open");
        }
        // Trả con trỏ về chỗ cũ (ví dụ nút "Sửa" vừa bấm) cho tiện dùng bàn phím
        previouslyFocused?.focus?.();
        if (typeof onClose === "function") onClose();
    }

    // Bấm phím Esc -> đóng (trừ khi hộp thoại đang bận xử lý)
    function onKeyDown(event) {
        if (event.key === "Escape" && !modal.dataset.busy) close();
    }

    // Lấy nút ✕. Chú thích @type để VS Code biết chắc đây là một nút <button>
    // (vừa tạo ở trên nên chắc chắn có) -> không báo "possibly null".
    const closeButton = /** @type {HTMLButtonElement} */ (header.querySelector(".modal-close"));

    closeButton.addEventListener("click", () => {
        if (!modal.dataset.busy) close();
    });

    // Bấm vào vùng tối bên ngoài -> đóng. Kiểm tra event.target === backdrop để
    // chắc chắn là bấm vào VÙNG TỐI chứ không phải bấm vào bên trong hộp trắng.
    backdrop.addEventListener("mousedown", event => {
        if (event.target === backdrop && !modal.dataset.busy) close();
    });

    // Bất kỳ nút nào có thuộc tính data-close (ví dụ nút "Hủy") cũng đóng hộp thoại
    // (event.target là "thứ bị bấm"; ghi chú @type để VS Code biết đó là một thẻ HTML, có hàm closest)
    modal.addEventListener("click", event => {
        const target = /** @type {HTMLElement} */ (event.target);
        if (target.closest("[data-close]") && !modal.dataset.busy) close();
    });

    // ---- Hiện lên ----
    // Nhớ thẻ đang được chọn trước khi mở hộp thoại (có thể không có -> null)
    const previouslyFocused = /** @type {HTMLElement|null} */ (document.activeElement);
    document.addEventListener("keydown", onKeyDown);
    document.body.classList.add("modal-open"); // khóa cuộn trang phía sau
    document.body.appendChild(backdrop);

    // Đặt con trỏ vào ô nhập đầu tiên (nếu có), không thì vào nút ✕
    const firstInput = /** @type {HTMLElement|null} */ (modal.querySelector("input:not([type=hidden]), select, textarea"));
    (firstInput ?? closeButton).focus();

    return { element: modal, body, footer: footerElement, form, close };
}

/** Đưa nội dung vào một phần tử: chuỗi -> gán innerHTML, phần tử -> gắn vào. */
function fillContent(target, content) {
    if (content instanceof Node) {
        target.appendChild(content);
    } else if (content !== null && content !== undefined) {
        target.innerHTML = content;
    }
}

/* ============================================================================
 *  PHẦN 2: HỘP THOẠI XÁC NHẬN CÓ / KHÔNG
 * ========================================================================== */

/**
 * Hỏi người dùng một câu, chờ họ bấm nút.
 *
 * Hàm trả về một "Promise" - hiểu đơn giản là "lời hứa sẽ có kết quả sau".
 * Dùng "await" để đợi kết quả: true = đồng ý, false = hủy / đóng.
 *
 * @param {object} [options]
 * @param {string} [options.title]
 * @param {string} [options.message]    - câu hỏi (chữ thường, không phải HTML)
 * @param {string} [options.confirmText] - chữ trên nút đồng ý
 * @param {string} [options.cancelText]  - chữ trên nút hủy
 * @param {boolean} [options.danger]     - true = nút đồng ý màu đỏ (dùng khi xóa)
 */
export function confirmDialog({
    title = "Xác nhận",
    message = "Bạn có chắc chắn muốn thực hiện thao tác này?",
    confirmText = "Đồng ý",
    cancelText = "Hủy",
    danger = false
} = {}) {
    return new Promise(resolve => {
        let answer = false; // mặc định là "không đồng ý" (khi bấm ✕, Esc, bấm ra ngoài)

        const footer = document.createElement("div");
        footer.style.display = "contents"; // phần tử bọc "vô hình", chỉ để chứa 2 nút
        footer.innerHTML = `
            <button type="button" class="btn btn-secondary" data-close>${escapeHtml(cancelText)}</button>
            <button type="button" class="btn ${danger ? "btn-danger" : "btn-primary"}" data-confirm>${escapeHtml(confirmText)}</button>
        `;

        const content = document.createElement("p");
        content.style.whiteSpace = "pre-line";
        content.textContent = message;

        const modal = openModal({
            title,
            content,
            footer,
            size: "sm",
            onClose: () => resolve(answer) // đóng hộp thoại -> trả kết quả
        });

        // Bấm nút đồng ý: ghi nhận true rồi đóng
        modal.element.querySelector("[data-confirm]").addEventListener("click", () => {
            answer = true;
            modal.close();
        });

        // Với hộp thoại xác nhận, đặt con trỏ sẵn vào nút đồng ý
        modal.element.querySelector("[data-confirm]").focus();
    });
}

/* ============================================================================
 *  PHẦN 3: HỘP THOẠI CÓ FORM (THÊM / SỬA)
 * ========================================================================== */

/**
 * Mô tả một ô nhập (field) - mỗi phần tử trong mảng "fields" có dạng:
 * {
 *   name:        "firstName",   // BẮT BUỘC - tên gửi lên API (phải giống tên thuộc tính bên backend, viết kiểu camelCase)
 *   label:       "Tên",         // chữ hiện phía trên ô
 *   type:        "text",        // text | email | password | number | date | datetime-local |
 *                               // textarea | select | checkbox | hidden   (mặc định: text)
 *   required:    true,          // bắt buộc nhập (hiện dấu * đỏ, trình duyệt tự chặn nếu bỏ trống)
 *   placeholder: "Nhập tên...", // chữ mờ gợi ý bên trong ô
 *   hint:        "Tối đa 50 ký tự", // dòng gợi ý nhỏ bên dưới ô
 *   options:     [{ value: 1, label: "Áo" }], // chỉ dùng cho select
 *   emptyOption: "-- Không chọn --",          // dòng trống đầu tiên của select (bỏ trống = không có)
 *   valueType:   "number",      // select chứa id số -> đổi giá trị thành số khi gửi
 *   min, max, step,             // giới hạn cho ô số / ngày
 *   disabled:    true,          // khóa không cho sửa (vẫn hiển thị)
 *   fullWidth:   true           // khi form 2 cột (columns: 2) thì ô này chiếm cả hàng
 * }
 */

/**
 * Mở hộp thoại có form.
 *
 * @param {object} [options]
 * @param {string} [options.title]
 * @param {any[]}  [options.fields]     - danh sách ô nhập (xem mô tả ở trên)
 * @param {any}    [options.values]     - dữ liệu điền sẵn, ví dụ dữ liệu cũ khi Sửa
 * @param {(data: any, modal: any) => any} [options.onSubmit] - hàm async nhận (data, modal). Chạy xong không lỗi -> tự đóng.
 *                                        Muốn GIỮ hộp thoại mở thì cho hàm trả về false.
 * @param {string} [options.submitText] - chữ trên nút lưu
 * @param {string} [options.cancelText] - chữ trên nút hủy
 * @param {number} [options.columns]    - 1 hoặc 2 cột
 * @param {"sm"|"md"|"lg"} [options.size]
 * @param {string} [options.description] - đoạn mô tả ngắn phía trên form
 *
 * @returns cùng kiểu với openModal (có thêm modal.form)
 */
export function formModal({
    title,
    fields = [],
    values = {},
    onSubmit,
    submitText = "Lưu",
    cancelText = "Hủy",
    columns = 1,
    size = "md",
    description = ""
} = {}) {
    // ---- Vẽ phần thân: hộp báo lỗi chung + các ô nhập ----
    const content = document.createElement("div");

    if (description) {
        const p = document.createElement("p");
        p.className = "text-muted mb-4";
        p.textContent = description;
        content.appendChild(p);
    }

    // Hộp báo lỗi chung (lỗi không thuộc ô nào, ví dụ "Email đã tồn tại")
    const alertBox = document.createElement("div");
    alertBox.className = "alert alert-danger mb-4";
    alertBox.setAttribute("role", "alert");
    content.appendChild(alertBox);

    const grid = document.createElement("div");
    grid.className = columns === 2 ? "form-grid" : "";
    grid.innerHTML = fields.map(field => renderField(field, values[field.name], columns)).join("");
    content.appendChild(grid);

    // ---- Chân: nút Hủy + nút Lưu ----
    const footer = document.createElement("div");
    footer.style.display = "contents";
    footer.innerHTML = `
        <button type="button" class="btn btn-secondary" data-close>${escapeHtml(cancelText)}</button>
        <button type="submit" class="btn btn-primary">${escapeHtml(submitText)}</button>
    `;

    const modal = openModal({ title, content, footer, size, wrapInForm: true });
    const form = modal.form;
    const submitButton = form.querySelector('button[type="submit"]');

    // Gõ lại vào ô bị lỗi -> xóa viền đỏ và chữ lỗi của ô đó
    form.addEventListener("input", event => clearFieldError(event.target));

    // ---- Khi bấm "Lưu" (hoặc nhấn Enter trong ô nhập) ----
    form.addEventListener("submit", async event => {
        // Mặc định form sẽ tải lại cả trang khi gửi -> chặn lại, để JS tự xử lý
        event.preventDefault();

        clearAllErrors(form, alertBox);
        const data = collectValues(form, fields);

        // Đánh dấu "đang bận": khóa nút, không cho đóng hộp thoại giữa chừng
        modal.element.dataset.busy = "1";
        setLoading(submitButton, true, "Đang lưu...");

        try {
            const result = await onSubmit?.(data, modal);
            delete modal.element.dataset.busy;
            if (result !== false) modal.close(); // thành công -> đóng
        } catch (err) {
            // Có lỗi (thường là ApiError từ http.js) -> hiện lỗi, KHÔNG đóng hộp thoại
            showErrors(form, alertBox, err);
        } finally {
            // "finally" luôn chạy dù thành công hay lỗi -> mở khóa nút
            delete modal.element.dataset.busy;
            setLoading(submitButton, false);
        }
    });

    return modal;
}

/* ---------------------- Các hàm phụ của formModal ---------------------- */

/** Tạo HTML cho một ô nhập dựa trên mô tả "field". */
function renderField(field, value, columns) {
    const type = field.type ?? "text";
    const id = `field-${field.name}`;
    const name = escapeHtml(field.name);
    const required = field.required ? "required" : "";
    const disabled = field.disabled ? "disabled" : "";
    const placeholder = field.placeholder ? `placeholder="${escapeHtml(field.placeholder)}"` : "";
    // Thêm min / max / step nếu có
    const limits = ["min", "max", "step"]
        .filter(key => field[key] !== undefined)
        .map(key => `${key}="${escapeHtml(field[key])}"`)
        .join(" ");

    // Ô ẩn: không có nhãn, chỉ giữ giá trị (ví dụ id)
    if (type === "hidden") {
        return `<input type="hidden" name="${name}" value="${escapeHtml(value ?? "")}">`;
    }

    let control;
    if (type === "textarea") {
        control = `<textarea class="textarea" id="${id}" name="${name}" ${required} ${disabled} ${placeholder}>${escapeHtml(value ?? "")}</textarea>`;
    } else if (type === "select") {
        const empty = field.emptyOption !== undefined
            ? `<option value="">${escapeHtml(field.emptyOption)}</option>`
            : "";
        const options = (field.options ?? []).map(option => {
            // So sánh dạng chữ để 1 và "1" vẫn được coi là giống nhau
            const selected = String(option.value) === String(value ?? "") ? "selected" : "";
            return `<option value="${escapeHtml(option.value)}" ${selected}>${escapeHtml(option.label)}</option>`;
        }).join("");
        control = `<select class="select" id="${id}" name="${name}" ${required} ${disabled}>${empty}${options}</select>`;
    } else if (type === "checkbox") {
        const checked = value ? "checked" : "";
        control = `
            <label class="checkbox">
                <input type="checkbox" id="${id}" name="${name}" ${checked} ${disabled}>
                <span>${escapeHtml(field.checkboxLabel ?? field.label ?? "")}</span>
            </label>`;
    } else {
        // text, email, password, number, date, datetime-local...
        control = `<input class="input" type="${escapeHtml(type)}" id="${id}" name="${name}"
                    value="${escapeHtml(formatInputValue(value, type))}" ${required} ${disabled} ${placeholder} ${limits}
                    ${type === "password" ? 'autocomplete="new-password"' : ""}>`;
    }

    // Checkbox đã có chữ bên cạnh, nên không cần nhãn phía trên
    const label = type === "checkbox"
        ? ""
        : `<label class="form-label" for="${id}">${escapeHtml(field.label ?? field.name)}${field.required ? ' <span class="required">*</span>' : ""}</label>`;

    const hint = field.hint ? `<div class="form-hint">${escapeHtml(field.hint)}</div>` : "";
    const spanClass = columns === 2 && (field.fullWidth || type === "textarea") ? "span-2" : "";

    return `
        <div class="form-group ${spanClass}" data-field="${name}">
            ${label}
            ${control}
            ${hint}
            <div class="form-error"></div>
        </div>`;
}

/**
 * Ô ngày cần đúng định dạng: "yyyy-MM-dd" (date) hoặc "yyyy-MM-ddTHH:mm" (datetime-local).
 * Backend trả về dạng dài "2026-10-13T07:30:00" nên phải cắt bớt cho vừa.
 */
function formatInputValue(value, type) {
    if (value === null || value === undefined) return "";
    if (type === "date") return String(value).slice(0, 10);
    if (type === "datetime-local") return String(value).slice(0, 16);
    return value;
}

/**
 * Gom giá trị các ô thành object để gửi lên API, đồng thời đổi đúng kiểu dữ liệu:
 *   - ô số (number) / select có valueType "number": đổi sang số; bỏ trống -> null
 *   - checkbox: true / false
 *   - select bỏ trống: null (ví dụ "Danh mục cha: không chọn")
 *   - ô chữ: xóa khoảng trắng thừa ở 2 đầu
 * Ô bị khóa (disabled) thì KHÔNG gửi lên.
 */
function collectValues(form, fields) {
    const data = {};
    for (const field of fields) {
        const element = form.elements[field.name];
        if (!element || element.disabled) continue;

        const type = field.type ?? "text";
        if (type === "checkbox") {
            data[field.name] = element.checked;
            continue;
        }

        const raw = typeof element.value === "string" ? element.value.trim() : element.value;

        if (type === "number" || field.valueType === "number") {
            data[field.name] = raw === "" ? null : Number(raw);
        } else if (type === "select" && raw === "") {
            data[field.name] = null;
        } else {
            data[field.name] = raw;
        }
    }
    return data;
}

/** Xóa lỗi của một ô (bỏ viền đỏ, xóa chữ lỗi). */
function clearFieldError(input) {
    const group = input?.closest?.(".form-group");
    if (!group) return;
    input.classList.remove("is-invalid");
    const errorBox = group.querySelector(".form-error");
    if (errorBox) errorBox.textContent = "";
}

/** Xóa toàn bộ lỗi trong form trước mỗi lần gửi. */
function clearAllErrors(form, alertBox) {
    alertBox.textContent = "";
    form.querySelectorAll(".is-invalid").forEach(el => el.classList.remove("is-invalid"));
    form.querySelectorAll(".form-error").forEach(el => (el.textContent = ""));
}

/**
 * Hiện lỗi trả về từ server.
 *
 * Nếu server trả lỗi theo từng ô (err.errors, từ FluentValidation), ví dụ:
 *     { FirstName: ["Tên không được để trống"], Email: ["Email không hợp lệ"] }
 * thì ta tìm ô có name tương ứng để hiện lỗi ngay dưới ô đó.
 * Tên bên server viết hoa chữ đầu (FirstName), bên form viết thường (firstName)
 * -> so sánh sau khi đổi hết về chữ thường.
 * Lỗi nào không tìm được ô tương ứng thì gom vào hộp báo lỗi chung ở trên.
 */
function showErrors(form, alertBox, err) {
    const leftover = [];

    if (err?.errors && typeof err.errors === "object") {
        for (const [key, messages] of Object.entries(err.errors)) {
            const list = Array.isArray(messages) ? messages : [messages];
            const group = [...form.querySelectorAll(".form-group[data-field]")]
                .find(g => g.dataset.field.toLowerCase() === key.toLowerCase());

            if (group) {
                group.querySelector(".input, .select, .textarea")?.classList.add("is-invalid");
                group.querySelector(".form-error").textContent = list.join(" ");
            } else {
                leftover.push(...list);
            }
        }
        // Tất cả lỗi đều đã nằm dưới từng ô -> hộp chung chỉ cần câu nhắc ngắn
        alertBox.textContent = leftover.length ? leftover.join("\n") : "Vui lòng kiểm tra lại các ô được đánh dấu.";
    } else {
        alertBox.textContent = err?.message ?? "Có lỗi xảy ra, vui lòng thử lại.";
    }

    // Cuộn hộp thoại lên đầu để người dùng thấy lỗi
    alertBox.scrollIntoView({ block: "nearest" });
}
