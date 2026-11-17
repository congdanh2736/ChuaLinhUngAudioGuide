/*
 * ============================================================================
 *  toast.js  -  THÔNG BÁO NHỎ Ở GÓC MÀN HÌNH
 * ============================================================================
 *
 *  "Toast" là cái hộp thông báo nhỏ hiện lên ở góc trên bên phải, vài giây
 *  sau tự biến mất (giống lát bánh mì bật lên từ máy nướng - nên gọi là toast).
 *  Dùng thay cho alert() vì alert() bắt người dùng phải bấm OK mới làm tiếp được.
 *
 *  Cách dùng:
 *      import { toast } from "/assets/js/components/toast.js";
 *
 *      toast.success("Đã lưu khách hàng!");
 *      toast.error("Không tìm thấy đơn hàng.");
 *      toast.warning("Sản phẩm sắp hết hàng.");
 *      toast.info("Đang tải dữ liệu...");
 *
 *      // Thông báo lỗi từ API: chỉ cần đưa err.message vào
 *      try { ... } catch (err) { toast.error(err.message); }
 *
 *  Kiểu dáng nằm trong components.css (các class .toast, .toast-success...).
 * ============================================================================
 */

// Ký hiệu nhỏ hiện bên trái mỗi loại thông báo
const ICONS = {
    success: "✓",
    error: "✕",
    warning: "!",
    info: "i"
};

// Thời gian tự biến mất (mili-giây). Lỗi để lâu hơn cho người dùng kịp đọc.
const DURATION = {
    success: 3000,
    info: 3000,
    warning: 4500,
    error: 6000
};

/**
 * Lấy "cái khay" chứa các thông báo. Chưa có thì tạo mới và gắn vào cuối <body>.
 * Tất cả thông báo đều nằm trong khay này nên sẽ xếp chồng gọn gàng.
 */
function getContainer() {
    let container = document.querySelector(".toast-container");
    if (!container) {
        container = document.createElement("div");
        container.className = "toast-container";
        // aria-live: báo cho phần mềm đọc màn hình (người khiếm thị) đọc thông báo mới
        container.setAttribute("aria-live", "polite");
        document.body.appendChild(container);
    }
    return container;
}

/**
 * Cho một thông báo biến mất: thêm class để nó mờ dần,
 * đợi hiệu ứng chạy xong (200ms) rồi mới gỡ hẳn khỏi trang.
 */
function removeToast(element) {
    if (!element.isConnected) return; // đã bị gỡ rồi thì thôi
    element.classList.add("is-leaving");
    setTimeout(() => element.remove(), 200);
}

/**
 * Hàm gốc: tạo và hiện một thông báo.
 *
 * @param {string} message - nội dung thông báo
 * @param {"success"|"error"|"warning"|"info"} type - loại thông báo
 * @param {number} [duration] - bao lâu thì tự tắt (ms). Truyền 0 = không tự tắt.
 */
export function showToast(message, type = "info", duration = DURATION[type] ?? 3000) {
    const element = document.createElement("div");
    element.className = `toast toast-${type}`;
    element.setAttribute("role", type === "error" ? "alert" : "status");

    // Tạo từng phần bằng createElement + textContent (KHÔNG dùng innerHTML cho
    // nội dung), để dù message có chứa ký tự < > lạ cũng chỉ hiện như chữ thường.
    const icon = document.createElement("span");
    icon.className = "toast-icon";
    icon.textContent = ICONS[type] ?? ICONS.info;

    const text = document.createElement("div");
    text.className = "toast-message";
    text.textContent = message ?? "";

    const closeButton = document.createElement("button");
    closeButton.type = "button";
    closeButton.className = "toast-close";
    closeButton.setAttribute("aria-label", "Đóng thông báo");
    closeButton.textContent = "×";
    closeButton.addEventListener("click", () => removeToast(element));

    element.append(icon, text, closeButton);
    getContainer().appendChild(element);

    // Hẹn giờ tự tắt
    if (duration > 0) {
        setTimeout(() => removeToast(element), duration);
    }

    return element;
}

/**
 * Các cách gọi tắt cho gọn: toast.success(...), toast.error(...)...
 * Object.freeze để không ai lỡ tay ghi đè các hàm này.
 */
export const toast = Object.freeze({
    success: (message, duration) => showToast(message, "success", duration),
    error: (message, duration) => showToast(message, "error", duration),
    warning: (message, duration) => showToast(message, "warning", duration),
    info: (message, duration) => showToast(message, "info", duration)
});
