## Mô tả
<!-- PR này làm gì? Closes #<số issue> -->

## Lớp bị ảnh hưởng
- [x] Lớp 1 — API (`app/api/`)
- [x] Lớp 2 — Service (`app/services/`)
- [x] Lớp 3 — Repository / Integration
- [x] Frontend / Docs / CI

## Checklist
- [x] Router không chứa nghiệp vụ, không gọi Repository trực tiếp
- [x] Service không import `fastapi`
- [x] Đã viết/cập nhật test; `pytest` chạy xanh
- [x] `ruff check . && ruff format --check .` không lỗi
- [x] Không commit `.env`, API key, mật khẩu
