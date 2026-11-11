# Phân công file & quy trình commit – nhóm 3 người

Phân công dựa trên bảng UC của nhóm, kết hợp với chia theo lớp kiến trúc (Danh – Presentation, Tài – Business Logic, Phát – Data Access + CI/CD) để mỗi người sở hữu trọn vẹn các file của UC mình phụ trách. Khối lượng tính theo dung lượng mã nguồn (không tính `docs/`, `__pycache__`).

| Thành viên | UC phụ trách | Khối lượng (≈) |
|---|---|---|
| **Trương Công Danh** | UC01, UC03, UC04, UC05, UC09, UC10, UC19 | ~158 KB |
| **Võ Thành Tài** | UC06, UC07, UC08, UC11, UC14, UC15 | ~154 KB |
| **Nguyễn Lê Tấn Phát** | UC02, UC12, UC13, UC16, UC17, UC18, UC20 | ~158 KB |

---

## 1. Danh sách file của từng người

### Trương Công Danh – Lớp API + giao diện du khách + xác thực
| File | UC |
|---|---|
| `app/main.py` | khởi tạo app, gắn router |
| `app/core/security.py` | UC03, UC09 (JWT, bcrypt) |
| `app/api/deps.py` | UC03 `get_visitor`, UC09 `get_current_admin`, UC19 `require_admin_role` |
| `app/api/errors.py`, `app/api/v1/router.py`, `app/api/__init__.py`, `app/api/v1/__init__.py`, `app/api/v1/endpoints/__init__.py`, `app/api/schemas/__init__.py` | xử lý lỗi tập trung, router |
| `app/api/schemas/common.py`, `content.py`, `admin.py` | schema request/response |
| `app/api/v1/endpoints/access.py` | UC01 `/ui-strings`, UC03 `/access/token` |
| `app/api/v1/endpoints/content.py` | UC01 bundle ETag/304 |
| `app/api/v1/endpoints/admin.py` | UC09, UC10, UC19 + các endpoint quản trị |
| `app/services/auth_service.py` | UC09, UC19 |
| `app/services/ui_text_service.py`, `app/db/ui_strings.py` | UC01 |
| `frontend/index.html`, `frontend/css/app.css` | giao diện du khách |
| `frontend/js/api.js`, `frontend/js/i18n.js` | UC01 |
| `frontend/js/app.js` | UC04 (geofence, GPS, chống nhiễu 3 giây), UC05 |
| `frontend/admin/index.html`, `frontend/admin/admin.css` | UC09, UC10 (trang đăng nhập) |
| `tests/test_api/` (cả thư mục), `tests/test_core.py`, `tests/test_frontend_sync.py` | test lớp API |

### Võ Thành Tài – Lớp Service (nghiệp vụ chính)
| File | UC |
|---|---|
| `app/services/content_service.py`, `poi_details.py`, `poi_insights_service.py` | UC06 (chuỗi dự phòng ngôn ngữ, đang mở/đóng, đông khách) |
| `app/services/tour_service.py`, `geo.py` | UC07 (`recommend`, khoảng cách) |
| `app/services/chat_service.py`, `retrieval.py`, `app/integrations/llm.py`, `app/check_llm.py` | UC08 (phạm vi, lịch sử, cache, dự phòng BM25) |
| `app/services/access_service.py` | UC11 (vòng đời mã paid → active → expired) |
| `app/services/poi_service.py`, `ai_assistant_service.py` | UC14 |
| `app/services/localization_service.py`, `task_runner.py` | UC15 (`source_hash`, giữ bản sửa tay, chạy nền) |
| `app/services/__init__.py` | |
| `tests/test_services/` (cả thư mục), `tests/test_integrations/test_llm.py` | test lớp Service |

### Nguyễn Lê Tấn Phát – Lớp Data Access + tích hợp + CI/CD + trang quản trị
| File | UC |
|---|---|
| `app/core/config.py`, `exceptions.py`, `logging.py`, `app/core/__init__.py`, `app/__init__.py` | cấu hình chung |
| `app/models/domain.py`, `views.py`, `app/models/__init__.py` | mô hình dữ liệu |
| `app/db/mongo.py`, `indexes.py`, `seed.py`, `seed_data.py`, `reset.py`, `demo_events.py`, `app/db/__init__.py` | UC18 (16 ngôn ngữ, seed) |
| `app/repositories/base.py`, `repositories.py`, `__init__.py` | UC12 (lọc, phân trang), UC13 (aggregate), UC16 (gỡ POI khỏi lộ trình) |
| `app/integrations/payment_gateway.py`, `translator.py`, `tts.py`, `media_storage.py`, `__init__.py` | UC02 (HMAC) |
| `app/services/payment_service.py` | UC02 (webhook idempotent) |
| `app/services/stats_service.py` | UC13 |
| `app/services/knowledge_service.py` | UC17 |
| `app/services/language_service.py`, `media_service.py` | UC18 |
| `app/api/v1/endpoints/mock_gateway.py`, `media.py` | UC02, phát audio |
| `frontend/admin/admin.js` | UC12, UC13, UC16, UC17, UC18, UC20 (trang quản trị) |
| `.github/` (workflows CI/CD, PR template), `Dockerfile`, `docker-compose.yml`, `.dockerignore`, `requirements*.txt`, `pyproject.toml`, `.env.example`, `.gitignore` | CI/CD |
| `tests/conftest.py`, `tests/fakes.py`, `tests/__init__.py`, `tests/test_repositories/`, `tests/test_integrations/test_translator.py`, `tests/test_integrations/__init__.py`, `tests/test_e2e.py` | test lớp Data Access + E2E |

### Tài liệu (`docs/`, `README.md`)
| Người | Tài liệu |
|---|---|
| Danh | `README.md`, `docs/01-kien-truc-3-lop.md`, báo cáo mục I, III |
| Tài | `docs/03-use-case.md`, `docs/10-dac-ta-use-case.md`, `docs/Dac-ta-Use-Casee.docx`, báo cáo mục II.1, II.2, II.3 |
| Phát | `docs/02-erd.md`, `docs/RDM.png`, `docs/use-case/`, báo cáo mục II.4, DFD (`docs/bao-cao/dfd-*`) |

> File dùng chung nhiều UC (`admin.js`, `app.js`, `admin.py`) giao cho **một chủ sở hữu**; người khác muốn sửa thì mở Pull Request để chủ file review.

---

## 2. Cách A – Dựng lại lịch sử commit theo phân công

Dùng khi muốn lịch sử Git thể hiện mỗi người commit phần của mình. Làm trên **nhánh mới**, không ghi đè `main` cho tới khi xong.

**Chuẩn bị:**
- Danh tạo tag giữ lịch sử cũ (chạy trong thư mục project hiện tại):
  ```bash
  git tag backup-truoc-chia-commit
  git push origin backup-truoc-chia-commit
  ```
- Danh gửi thư mục `_commit_zips/` (16 file zip + `THU-TU-COMMIT.txt`) cho Tài và Phát. Mỗi zip ứng với một vòng và giữ nguyên đường dẫn thư mục bên trong.
- **Không làm trong thư mục project đang có đủ code**, vì `git add .` sẽ đưa luôn mọi file vào commit. Mỗi người làm trong một **bản clone mới**.

**Vòng 01 (Phát) – tạo nhánh trống `rebuild`:**
```bash
git clone https://github.com/congdanh2736/audio-guide.git audio-guide-rebuild
cd audio-guide-rebuild
git checkout --orphan rebuild
git rm -rf . -q                      # chỉ xóa trong bản clone mới này, repo gốc không ảnh hưởng
# giải nén vong-01-Phat.zip vào thư mục audio-guide-rebuild
git add .
git commit -m "chore: khởi tạo cấu hình và mô hình dữ liệu"
git push -u origin rebuild
```

**Thứ tự commit** (sắp theo phụ thuộc giữa các lớp, để sau mỗi vòng code vẫn import được):

| Vòng | Người | Nội dung | Gợi ý commit message |
|---|---|---|---|
| 1 | Phát | `.gitignore`, `requirements*.txt`, `pyproject.toml`, `.env.example`, `app/__init__.py`, `app/core/config.py`, `exceptions.py`, `logging.py`, `app/models/` | `chore: khởi tạo cấu hình và mô hình dữ liệu` |
| 2 | Danh | `app/core/security.py` | `feat(core): JWT, băm mật khẩu bcrypt` |
| 3 | Phát | `app/db/`, `app/repositories/`, `app/integrations/` (trừ `llm.py`) | `feat(data): kết nối MongoDB, repository, tích hợp dịch máy/TTS/thanh toán` |
| 4 | Tài | `app/services/` (các file của Tài), `app/integrations/llm.py`, `app/check_llm.py` | `feat(services): nghiệp vụ POI, bản dịch, lộ trình, chatbot, mã truy cập` |
| 5 | Phát | `app/services/payment_service.py`, `stats_service.py`, `knowledge_service.py`, `language_service.py`, `media_service.py` | `feat(services): thanh toán, thống kê, kiến thức, ngôn ngữ` |
| 6 | Danh | `app/services/auth_service.py`, `ui_text_service.py`, `app/db/ui_strings.py`, `app/api/`, `app/main.py` (trừ `mock_gateway.py`, `media.py`) | `feat(api): endpoint, schema, phân quyền, xử lý lỗi tập trung` |
| 7 | Phát | `app/api/v1/endpoints/mock_gateway.py`, `media.py` | `feat(api): cổng thanh toán giả lập, phát file audio` |
| 8 | Danh | `frontend/` (trừ `admin/admin.js`) | `feat(frontend): app du khách, đa ngôn ngữ, geofence` |
| 9 | Phát | `frontend/admin/admin.js` | `feat(admin): trang quản trị` |
| 10 | Phát | `tests/conftest.py`, `fakes.py`, `__init__.py`, `test_repositories/`, `test_integrations/test_translator.py`, `test_e2e.py` | `test: repository, tích hợp, E2E` |
| 11 | Tài | `tests/test_services/`, `tests/test_integrations/test_llm.py` | `test(services): kiểm thử nghiệp vụ` |
| 12 | Danh | `tests/test_api/`, `test_core.py`, `test_frontend_sync.py` | `test(api): kiểm thử endpoint` |
| 13 | Phát | `.github/`, `Dockerfile`, `docker-compose.yml`, `.dockerignore` | `ci: GitHub Actions, Docker, deploy Render` |
| 14 | cả 3 | `README.md`, `docs/` theo bảng tài liệu ở trên | `docs: ...` |

**Các vòng tiếp theo** – người thực hiện làm trên máy mình, bằng tài khoản GitHub của mình:
```bash
# lần đầu tham gia:
git clone -b rebuild https://github.com/congdanh2736/audio-guide.git audio-guide-rebuild
cd audio-guide-rebuild
# những lần sau: cd audio-guide-rebuild && git pull origin rebuild

# giải nén zip của vòng mình vào thư mục audio-guide-rebuild (chọn ghi đè nếu được hỏi)
git status                           # chỉ thấy đúng các file của vòng này là được
git add .
git commit -m "<message trong THU-TU-COMMIT.txt>"
git push origin rebuild
```
Phải đợi người làm vòng trước push xong mới `git pull` rồi làm vòng của mình.
Kiểm tra trước khi commit: `git config user.name` và `git config user.email` phải là tài khoản của chính mình.

**Khi xong vòng 14:** chạy `pytest` trên nhánh `rebuild`. Nếu tất cả pass, mở Pull Request `rebuild → main` để cả nhóm review rồi merge (hoặc đặt `rebuild` làm nhánh mặc định trên GitHub). CI ở các vòng giữa có thể báo đỏ vì code chưa đủ; chỉ cần lần cuối xanh.

---

## 3. Cách B – Giữ lịch sử, chia việc từ nay

Giữ nguyên lịch sử hiện tại. Từ giờ mỗi người làm trên nhánh riêng (`feature/<tên>-<việc>`), mở Pull Request và người khác review. Đây là các việc thật đang tồn đọng, đã chia theo phần sở hữu:

| Người | Việc cần làm |
|---|---|
| Danh | Thêm nút "Xóa bản dịch" vào luồng xóa POI trên giao diện và hiển thị lỗi 409 rõ ràng; sửa `README.md` (link `docs/05-ci-cd.md` đang hỏng); thống nhất số UC giữa `docs/03-use-case.md` và `docs/10-dac-ta-use-case.md` |
| Tài | Sửa race condition: tác vụ dịch nền ghi lại bản dịch sau khi POI bị xóa (kiểm tra POI còn tồn tại trước `upsert` trong `_localize_one`); sửa công thức "độ phủ bản dịch" chỉ đếm ngôn ngữ đang bật; vẽ lại sơ đồ hoạt động UC14, UC15 |
| Phát | Thêm hàm xóa file audio mồ côi trong `media_storage.py`; viết `docs/05-ci-cd.md`; sửa 3 lỗi `ruff` (thiếu dòng trống cuối file) để CI xanh |

Mỗi người nên **đọc kỹ và chạy thử phần code mình sở hữu**, vì giảng viên thường hỏi từng thành viên về đúng UC người đó phụ trách.
