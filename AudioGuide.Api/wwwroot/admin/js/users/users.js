/*
 * Tài khoản quản trị (chỉ Admin): UserController (/api/users) -> AuthService -> UserManager của ASP.NET Identity.
 */
import { initAdminLayout } from "../layout.js";
import { userApi } from "../../../assets/js/api/authApi.js";
import { renderTable } from "../../../assets/js/components/table.js";
import { formModal, confirmDialog } from "../../../assets/js/components/modal.js";
import { toast } from "../../../assets/js/components/toast.js";
import { getUser } from "../../../assets/js/core/auth.js";
import { ROLE_LABELS } from "../../../assets/js/core/config.js";
import { escapeHtml, activeBadge, formatDateTime } from "../../../assets/js/core/utils.js";

const container = document.getElementById("tableContainer");
const ROLE_OPTIONS = [{ value: "Staff", label: ROLE_LABELS.Staff }, { value: "Admin", label: ROLE_LABELS.Admin }];

async function load() {
    try {
        const me = getUser();
        renderTable(container, {
            columns: [
                { key: "userName", title: "Tên đăng nhập", render: r => `<b>${escapeHtml(r.userName)}</b>` },
                { key: "fullName", title: "Họ tên" },
                { key: "role", title: "Vai trò", render: r => `<span class="badge badge-${r.role === "Admin" ? "primary" : "secondary"}">${escapeHtml(ROLE_LABELS[r.role] ?? r.role)}</span>` },
                { key: "isActive", title: "Trạng thái", render: r => activeBadge(r.isActive, "Hoạt động", "Đã khóa") },
                { key: "lastLoginAt", title: "Đăng nhập gần nhất", render: r => `<span class="text-sm">${formatDateTime(r.lastLoginAt)}</span>` }
            ],
            rows: await userApi.getAll(),
            actions: [
                { label: "Sửa", onClick: r => openEdit(r) },
                { label: "Khóa", danger: true, visible: r => r.isActive && r.id !== me?.id, onClick: r => toggle(r, false) },
                { label: "Mở khóa", visible: r => !r.isActive, onClick: r => toggle(r, true) }
            ]
        });
    } catch (err) {
        container.innerHTML = `<p class="card-body text-danger">${escapeHtml(err.message)}</p>`;
    }
}

function openCreate() {
    formModal({
        title: "Thêm tài khoản",
        columns: 2,
        fields: [
            { name: "userName", label: "Tên đăng nhập", required: true, placeholder: "vd: nhanvien01" },
            { name: "password", label: "Mật khẩu (tối thiểu 8 ký tự)", type: "password", required: true },
            { name: "fullName", label: "Họ tên", required: true },
            { name: "role", label: "Vai trò", type: "select", options: ROLE_OPTIONS }
        ],
        values: { role: "Staff" },
        submitText: "Tạo",
        onSubmit: async data => {
            await userApi.create(data);
            toast.success("Đã tạo tài khoản.");
            await load();
        }
    });
}

function openEdit(row) {
    formModal({
        title: `Sửa tài khoản ${row.userName}`,
        fields: [
            { name: "fullName", label: "Họ tên", required: true },
            { name: "role", label: "Vai trò", type: "select", options: ROLE_OPTIONS },
            { name: "password", label: "Đặt lại mật khẩu (để trống nếu không đổi)", type: "password" }
        ],
        values: { fullName: row.fullName, role: row.role },
        onSubmit: async data => {
            if (!data.password) delete data.password;
            await userApi.update(row.id, data);
            toast.success("Đã lưu.");
            await load();
        }
    });
}

async function toggle(row, isActive) {
    if (!isActive) {
        const ok = await confirmDialog({ title: "Khóa tài khoản", message: `Khóa tài khoản ${row.userName}? Người này sẽ bị đăng xuất ngay.`, confirmText: "Khóa", danger: true });
        if (!ok) return;
    }
    try {
        await userApi.update(row.id, { isActive });
        toast.success(isActive ? "Đã mở khóa." : "Đã khóa.");
        await load();
    } catch (err) {
        toast.error(err.message);
    }
}

if (await initAdminLayout({ title: "Tài khoản", adminOnly: true })) {
    document.getElementById("btnAdd").addEventListener("click", openCreate);
    load();
}
