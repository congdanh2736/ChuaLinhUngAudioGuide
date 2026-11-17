/*
 * layout.js - khung chung của mọi trang quản trị: menu bên trái (sidebar), thanh trên (topbar),
 * kiểm tra đăng nhập, ẩn menu chỉ dành cho Admin, nút đổi mật khẩu / đăng xuất.
 *
 * Cách dùng trong 1 trang:
 *     if (await initAdminLayout({ title: "Điểm tham quan" })) { ...code của trang... }
 */
import { requireLogin, requireAdmin, isAdmin, getUser, logout } from "../../assets/js/core/auth.js";
import { ROLE_LABELS } from "../../assets/js/core/config.js";
import { confirmDialog, formModal } from "../../assets/js/components/modal.js";
import { toast } from "../../assets/js/components/toast.js";
import { authApi } from "../../assets/js/api/authApi.js";

const APP_NAME = "Quản trị Chùa Linh Ứng";

async function loadPartial(url) {
    const response = await fetch(url);
    if (!response.ok) throw new Error(`Không tải được ${url} (mã ${response.status})`);
    return response.text();
}

export async function initAdminLayout({ title = "", adminOnly = false } = {}) {
    const allowed = adminOnly ? requireAdmin() : requireLogin();
    if (!allowed) return false;

    try {
        const [sidebarHtml, topbarHtml] = await Promise.all([
            loadPartial("/admin/partials/sidebar.html"),
            loadPartial("/admin/partials/topbar.html")
        ]);
        document.getElementById("sidebar").innerHTML = sidebarHtml;
        document.getElementById("topbar").innerHTML = topbarHtml;
    } catch (err) {
        console.error("[layout]", err);
    }

    // Nhân viên (Staff) không thấy các mục chỉ dành cho Admin
    if (!isAdmin()) document.querySelectorAll('[data-role="admin"]').forEach(el => el.remove());

    highlightActiveMenu();
    fillUserInfo();
    setupButtons();
    setupSidebarToggle();
    setPageTitle(title);
    document.querySelector(".admin-shell")?.classList.add("is-ready");
    return true;
}

export function setPageTitle(title) {
    const el = document.querySelector("[data-page-title]");
    if (el) el.textContent = title;
    document.title = title ? `${title} | ${APP_NAME}` : APP_NAME;
}

function highlightActiveMenu() {
    const path = window.location.pathname;
    document.querySelectorAll("#sidebar .nav-link").forEach(link => {
        const linkPath = new URL(link.getAttribute("href") ?? "", window.location.origin).pathname;
        // Trang con (vd pois/poi-form.html, pois/localizations.html) vẫn sáng mục cha cùng thư mục
        const folder = p => p.substring(0, p.lastIndexOf("/") + 1);
        const sameFolder = !["/", "/admin/"].includes(folder(linkPath)) && folder(linkPath) === folder(path);
        const active = linkPath === path || sameFolder;
        link.classList.toggle("is-active", active);
        if (active) link.setAttribute("aria-current", "page");
    });
}

function fillUserInfo() {
    const user = getUser();
    const name = user?.fullName || user?.userName || "Người dùng";
    const words = name.trim().split(/\s+/);
    setText("[data-user-name]", name);
    setText("[data-user-role]", ROLE_LABELS[user?.role] ?? user?.role ?? "");
    setText("[data-user-avatar]", (words[words.length - 1] || "?").charAt(0).toUpperCase());
}

function setText(selector, text) {
    const el = document.querySelector(selector);
    if (el) el.textContent = text;
}

function setupButtons() {
    document.querySelector("[data-logout]")?.addEventListener("click", async () => {
        const ok = await confirmDialog({ title: "Đăng xuất", message: "Bạn có muốn đăng xuất khỏi trang quản trị?", confirmText: "Đăng xuất" });
        if (ok) logout();
    });
    document.querySelector("[data-change-password]")?.addEventListener("click", () => {
        formModal({
            title: "Đổi mật khẩu",
            fields: [
                { name: "oldPassword", label: "Mật khẩu hiện tại", type: "password", required: true },
                { name: "newPassword", label: "Mật khẩu mới (tối thiểu 8 ký tự)", type: "password", required: true }
            ],
            submitText: "Đổi mật khẩu",
            onSubmit: async data => {
                await authApi.changePassword(data.oldPassword, data.newPassword);
                toast.success("Đã đổi mật khẩu.");
            }
        });
    });
}

function setupSidebarToggle() {
    let overlay = document.querySelector(".sidebar-overlay");
    if (!overlay) {
        overlay = document.createElement("div");
        overlay.className = "sidebar-overlay";
        document.body.appendChild(overlay);
    }
    const close = () => document.body.classList.remove("sidebar-open");
    document.querySelector("[data-sidebar-toggle]")?.addEventListener("click", () => document.body.classList.toggle("sidebar-open"));
    overlay.addEventListener("click", close);
    document.querySelectorAll("#sidebar .nav-link").forEach(link => link.addEventListener("click", close));
}
