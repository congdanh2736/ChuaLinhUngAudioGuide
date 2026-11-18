/*
 * config.js - các giá trị dùng chung cho cả frontend (không làm gì, chỉ cất hằng số).
 * Muốn đổi địa chỉ API hay tên khóa lưu trình duyệt thì sửa ở đây.
 */

/*
 * Địa chỉ backend.
 * - Mở frontend từ chính backend (localhost, IP trong mạng LAN, *.runasp.net) -> "" = cùng địa chỉ, gọi /api như cũ.
 * - Mở frontend từ nơi khác (Vercel...) -> gọi sang backend trên MonsterASP (backend đã bật CORS cho domain đó).
 */
const PROD_BACKEND_URL = "https://audioguide.runasp.net";
const host = window.location.hostname;
const servedByBackend = ["localhost", "127.0.0.1"].includes(host)
    || /^(10|192\.168|172\.(1[6-9]|2\d|3[01]))\./.test(host)
    || host.endsWith(".runasp.net");
export const BACKEND_URL = servedByBackend ? "" : PROD_BACKEND_URL;

// Mọi API của backend .NET đều bắt đầu bằng /api
export const API_BASE = BACKEND_URL + "/api";

// Đường dẫn tương đối do backend trả về (/media/..., /mock-gateway/...) -> URL đầy đủ tới backend
export const backendUrl = path => (!path || /^https?:\/\//.test(path) ? path : BACKEND_URL + path);

// Tên khóa lưu trong localStorage của trình duyệt
export const STORAGE_KEYS = Object.freeze({
    ADMIN_TOKEN: "ag_admin_token",     // token đăng nhập trang quản trị
    ADMIN_USER: "ag_admin_user",       // thông tin tài khoản quản trị
    VISITOR_TOKEN: "ag_visitor_token", // token du khách (sau khi đổi mã truy cập)
    LANG: "ag_lang",
    AUTOPLAY: "ag_autoplay"
});

// Vai trò tài khoản quản trị (trùng tên vai trò trong bảng AspNetRoles)
export const ROLES = Object.freeze({ ADMIN: "Admin", STAFF: "Staff" });
export const ROLE_LABELS = Object.freeze({ Admin: "Quản trị viên", Staff: "Nhân viên" });

export const PAGES = Object.freeze({
    ADMIN_LOGIN: "/admin/login.html",
    ADMIN_HOME: "/admin/dashboard.html",
    VISITOR_HOME: "/"
});

export const PAGE_SIZE = 10;

// Trạng thái bản dịch -> nhãn + màu badge
export const LOCALIZATION_STATUS_LABELS = Object.freeze({
    Pending: { text: "Chờ dịch", color: "warning" },
    Processing: { text: "Đang dịch", color: "info" },
    Ready: { text: "Sẵn sàng", color: "success" },
    Failed: { text: "Lỗi", color: "danger" },
    Outdated: { text: "Cũ", color: "secondary" },
    Missing: { text: "Chưa có", color: "secondary" }
});

// Trạng thái phiên truy cập
export const ACCESS_STATUS_LABELS = Object.freeze({
    Pending: { text: "Chờ thanh toán", color: "warning" },
    Paid: { text: "Đã thanh toán", color: "info" },
    Active: { text: "Đang dùng", color: "success" },
    Expired: { text: "Hết hạn", color: "secondary" },
    Cancelled: { text: "Đã hủy", color: "danger" }
});

export const ACCESS_METHOD_LABELS = Object.freeze({ Cash: "Tiền mặt", Online: "Online" });

export const TRANSLATED_BY_LABELS = Object.freeze({ Source: "Bản gốc", Machine: "Dịch máy", Manual: "Sửa tay" });

// Loại điểm tham quan (khớp BusinessConstants.Categories ở backend)
export const CATEGORY_LABELS = Object.freeze({
    landmark: "Điểm tham quan",
    statue: "Tượng Phật",
    hall: "Điện thờ",
    gate: "Cổng chùa",
    garden: "Vườn",
    viewpoint: "Điểm ngắm cảnh",
    service: "Khu dịch vụ"
});

// Mã tiện ích -> nhãn (khớp BusinessConstants.Amenities ở backend)
export const AMENITY_LABELS = Object.freeze({
    wheelchair: "Lối đi cho xe lăn",
    stairs: "Nhiều bậc thang",
    parking: "Bãi gửi xe",
    restroom: "Nhà vệ sinh",
    drinking_water: "Nước uống",
    shade: "Có bóng mát, ghế nghỉ",
    photo_ok: "Được chụp ảnh",
    no_photo_ceremony: "Không chụp ảnh khi hành lễ",
    shoes_off: "Bỏ giày dép trước khi vào",
    quiet_zone: "Giữ yên lặng",
    dress_code: "Trang phục kín đáo"
});

export const DAY_NAMES = Object.freeze(["Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy", "Chủ nhật"]);

// Vị trí mặc định trên bản đồ (Chùa Linh Ứng Bãi Bụt)
export const DEFAULT_CENTER = Object.freeze([16.1003, 108.2777]);
export const MAP_TILE_URL = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";
