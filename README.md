# Thuyết Minh Tự Động Đa Ngôn Ngữ — Chùa Linh Ứng (Backend 3 lớp + CI/CD)

Đồ án môn **Công nghệ phần mềm** — đề tài *"Thuyết minh tự động đa ngôn ngữ"*, **yêu cầu 2: BE Dev theo hướng 3 lớp bao gồm CI/CD**.

Du khách quét QR ở cổng chùa → mua quyền truy cập (online hoặc tiền mặt) → mở bản đồ, đi tới điểm nào thì app **tự phát thuyết minh** bằng ngôn ngữ của họ (16 ngôn ngữ), có **chatbot** hỏi đáp và **gợi ý lộ trình**. Ban quản lý dùng **dashboard** để nhập nội dung tiếng Việt — hệ thống tự dịch và tạo audio.

| Thành phần | Công nghệ |
|---|---|
| Backend | Python 3.11+ (đã kiểm thử trên 3.11, 3.12, 3.14) · FastAPI · Pydantic v2 |
| Database | Microsoft SQL Server 2019/2022 (SQLAlchemy 2.0 async + ODBC) — có khóa ngoại, cài trên máy hoặc Docker |
| Đa ngôn ngữ | deep-translator (Google Translate, dự phòng MyMemory) · Edge-TTS (giọng đọc Microsoft, miễn phí) |
| Chatbot | AI tạo sinh qua OpenRouter (cũng hỗ trợ Gemini / Groq) + RAG trên tài liệu của chùa, chỉ trả lời trong phạm vi Chùa Linh Ứng |
| Frontend | HTML/CSS/JavaScript thuần · Leaflet (bản đồ) |
| CI/CD | GitHub Actions · Docker · GHCR · Render |

## Thành viên

| Thành viên | Phụ trách |
|---|---|
| **Võ Thành Tài** | Lớp 1 — Presentation: `app/api/`, `frontend/` (app du khách), `tests/test_api/` |
| **Nguyễn Lê Tấn Phát** | Lớp 2 — Business Logic: `app/services/`, `tests/test_services/` |
| **Trương Công Danh** | Lớp 3 — Data Access + CI/CD: `app/db/`, `app/repositories/`, `app/integrations/`, `frontend/admin/admin.js`, `.github/workflows/` |

---

## Kiến trúc 3 lớp

```
                ┌──────────────────────────────────────────────┐
 Browser/App ──▶│ LỚP 1 · PRESENTATION   app/api/              │  nhận request, validate (Pydantic),
                │   routers · schemas · deps · errors           │  phân quyền, trả JSON chuẩn
                └───────────────────────┬──────────────────────┘
                                        ▼  chỉ gọi Service
                ┌──────────────────────────────────────────────┐
                │ LỚP 2 · BUSINESS LOGIC  app/services/        │  quy tắc nghiệp vụ: fallback ngôn ngữ,
                │   poi · localization · content · tour ·       │  vòng đời mã truy cập, lộ trình, RAG...
                │   access · payment · chat · auth · stats      │
                └───────────────────────┬──────────────────────┘
                                        ▼  chỉ gọi Repository / Integration
                ┌──────────────────────────────────────────────┐
                │ LỚP 3 · DATA ACCESS                           │
                │   app/repositories/  → SQL Server (15 bảng)   │
                │   app/integrations/  → Dịch máy, TTS, LLM,    │
                │                        cổng thanh toán, file  │
                └──────────────────────────────────────────────┘
```

**Quy tắc vàng:** luồng gọi **một chiều** `api → services → repositories`. Router không chứa nghiệp vụ, không chạm DB. Service không biết HTTP (ném `NotFoundError`, lớp API mới đổi thành 404). Chi tiết: [docs/01-kien-truc-3-lop.md](docs/01-kien-truc-3-lop.md).

Riêng **geofence** (theo dõi GPS, ở trong vùng ≥ 3 giây thì tự phát thuyết minh) chạy trên trình duyệt, trong `frontend/js/app.js`; backend chỉ ghi nhận lượt ghé thăm qua `POST /api/v1/events`.

## Cài đặt & chạy

### Bước 1 — Cài SQL Server (chọn 1 trong 2 cách)

**Cách A — Cài trên Windows (khuyên dùng khi code hằng ngày)**
1. Tải **SQL Server 2022 Express** (miễn phí) tại https://www.microsoft.com/sql-server/sql-server-downloads → chọn *Basic*.
   Cài xong ghi lại tên instance, thường là `localhost\SQLEXPRESS`.
2. Cài **ODBC Driver 18 for SQL Server**: https://learn.microsoft.com/sql/connect/odbc/download-odbc-driver-for-sql-server
   (máy đã có ODBC Driver 17 thì dùng luôn, nhớ sửa `driver=ODBC+Driver+17+for+SQL+Server` trong `.env`).
3. Nên cài **SQL Server Management Studio (SSMS)** để xem dữ liệu, sơ đồ quan hệ (*Database Diagrams*) và thử xóa dữ liệu.
4. Kiểm tra: mở `services.msc` → thấy **SQL Server (SQLEXPRESS)** ở trạng thái *Running*.

**Cách A2 — SQL Server LocalDB (máy đã cài Visual Studio thường có sẵn):** không cần cài thêm, không cần service.
Đặt `DATABASE_URL=mssql+aioodbc://@(localdb)\MSSQLLocalDB/audio_guide?driver=ODBC+Driver+18+for+SQL+Server&TrustServerCertificate=yes`;
SSMS kết nối server `(localdb)\MSSQLLocalDB` (Windows Authentication). Kiểm tra có LocalDB: `sqllocaldb info`.

**Cách B — Docker:** `docker compose up --build` (chạy cả SQL Server 2022 + backend, mở http://localhost:8000).
Chỉ muốn SQL Server trong Docker: `docker compose up -d sqlserver` rồi đặt trong `.env`:
`DATABASE_URL=mssql+aioodbc://sa:YourStrong!Passw0rd@localhost:14330/audio_guide?driver=ODBC+Driver+18+for+SQL+Server&TrustServerCertificate=yes`
(cổng 14330 để không đụng SQL Server đã cài trên máy; SSMS kết nối `localhost,14330`, user `sa`).

> Không cần tự tạo database: lần chạy đầu, ứng dụng tự tạo database `audio_guide` và toàn bộ bảng + khóa ngoại (**Code First**,
> khai báo ở `app/db/tables.py`). Script SQL tương đương: `docs/database/schema-sqlserver.sql`.
> Mật khẩu có ký tự đặc biệt như `@ : / ? #` thì phải mã hóa URL trong `DATABASE_URL` (vd `@` → `%40`).

### Bước 2 — Chạy backend

```bash
# Tạo + kích hoạt môi trường ảo
python -m venv .venv
# Windows:  .venv\Scripts\activate        macOS/Linux:  source .venv/bin/activate

# Cài thư viện (mọi thư viện đều có bản cài sẵn, không cần Visual C++/Rust)
pip install -r requirements-dev.txt

# Tạo file cấu hình
copy .env.example .env        # macOS/Linux: cp .env.example .env
# Mở .env, sửa DATABASE_URL cho đúng SQL Server trên máy (xem Bước 1):
#   SQL Server Express:  mssql+aioodbc://@localhost\SQLEXPRESS/audio_guide?driver=ODBC+Driver+18+for+SQL+Server&TrustServerCertificate=yes
#   LocalDB:             mssql+aioodbc://@(localdb)\MSSQLLocalDB/audio_guide?driver=ODBC+Driver+18+for+SQL+Server&TrustServerCertificate=yes

# Chạy server
uvicorn app.main:app --reload
```

Thấy dòng `Đã kết nối cơ sở dữ liệu: mssql+aioodbc://...` là thành công. Nếu SQL Server chưa chạy hoặc sai tên driver,
server dừng ngay và in hướng dẫn khắc phục.

| Địa chỉ | Nội dung |
|---|---|
| http://localhost:8000 | App du khách (mở bằng DevTools chế độ mobile; điện thoại xem lưu ý GPS bên dưới) |
| http://localhost:8000/admin/ | Dashboard quản trị — `admin` / `admin123` |
| http://localhost:8000/docs | Swagger — thử trực tiếp các API |

Lần đầu chạy, hệ thống tự tạo 16 ngôn ngữ, tài khoản admin và **dữ liệu mẫu** (6 điểm tham quan, 2 lộ trình, 7 bài viết cho chatbot) — **chỉ một lần**, các lần sau dữ liệu được giữ nguyên.

> **Mở bằng điện thoại thật:** trình duyệt chỉ cho lấy vị trí GPS trên `https://` hoặc `localhost`. Vào bằng `http://192.168.x.x:8000` thì GPS bị chặn, dùng ô **Giả lập GPS** thay thế. Một số trình duyệt (nhất là Safari trên iPhone) có thể chặn tự phát âm thanh; khi đó bấm nút phát trong bảng chi tiết.

### Thông tin chi tiết từng điểm (tương tự Google Maps)

Mỗi điểm tham quan có: **giờ mở cửa theo từng ngày** (nhiều khung/ngày, vd nghỉ trưa), trạng thái **Đang mở cửa / Sắp đóng cửa / Đã đóng cửa**, vé vào cửa, thời gian tham quan gợi ý, tiện ích & quy định, lưu ý (tự dịch 16 ngôn ngữ), biểu đồ **khung giờ đông khách**, nút **Chỉ đường**. Admin nhập ở form sửa điểm tham quan.

- Database tạo từ bản cũ? Lần khởi động sau, hệ thống **tự bổ sung** thông tin mẫu cho 6 điểm mẫu (không đụng điểm admin đã nhập).
- Biểu đồ đông khách tính từ lượt ghé thăm thật. Muốn demo khi chưa có khách: `python -m app.db.demo_events` (tạo 4 tuần dữ liệu **giả lập**), xóa bằng `python -m app.db.demo_events --clear`.

> Mẹo: Không ở chùa? Trong app tick ô **Giả lập GPS** rồi bấm vào vòng tròn của một điểm trên bản đồ — trong vài giây app tự mở và phát thuyết minh (ô **Tự phát** phải đang bật). Với GPS thật, phải ở trong vùng liên tục ≥ 3 giây mới phát, để tránh GPS nhảy.

### Bật chatbot AI tạo sinh

Nhóm dùng **OpenRouter** (`.env.example` đã đặt sẵn `LLM_PROVIDER=openrouter` và một model miễn phí):

1. Vào https://openrouter.ai → đăng nhập → **Keys** → **Create Key** (key dạng `sk-or-...`).
2. Dán vào file `.env`: `LLM_API_KEY=<key vừa tạo>` (không dấu nháy, không khoảng trắng) rồi khởi động lại server.
3. Log khởi động có dòng `Chatbot dùng AI tạo sinh: openrouter / <tên model>` là đã bật. Đổi model bằng biến `LLM_MODEL` (tên model lấy trên trang Models của OpenRouter; model có đuôi `:free` là miễn phí nhưng có thể bị giới hạn số lượt).

Muốn dùng nhà cung cấp khác: đặt `LLM_PROVIDER=gemini` (key tại https://aistudio.google.com/apikey) hoặc `LLM_PROVIDER=groq`, và **xóa trống `LLM_MODEL`** để dùng model mặc định của nhà cung cấp đó.

Chatbot trả lời tự nhiên mọi câu hỏi về Chùa Linh Ứng (lịch sử, kiến trúc, Phật giáo, tham quan, đường đi...), nhớ ngữ cảnh vài câu trước, ưu tiên tài liệu do admin nhập, và **từ chối lịch sự** câu hỏi ngoài phạm vi. Không có key hoặc AI lỗi thì chatbot vẫn chạy ở chế độ trích đoạn tài liệu (tìm kiếm BM25).

### Dữ liệu được lưu ở đâu?

Mọi thứ nằm trong **SQL Server** (database `audio_guide`) — tắt server, khởi động lại đều không mất:

| Dữ liệu | Bảng |
|---|---|
| Điểm tham quan, bản dịch, lộ trình, ngôn ngữ | `pois`, `poi_localizations`, `tours`, `tour_pois`, `tour_translations`, `languages` |
| **File audio mp3** | `media_files`, cột `VARBINARY(MAX)` (đặt `MEDIA_STORAGE=local` nếu muốn lưu ra thư mục) |
| Tài khoản, mã truy cập, thanh toán | `admin_users`, `access_sessions`, `payments` |
| Chatbot: kiến thức, lịch sử, **cache câu trả lời** (tự hết hạn) | `knowledge_articles`, `chat_logs`, `chat_cache` |
| Nhãn giao diện đã dịch, thống kê | `ui_translations`, `visit_events` |

Server tắt khi đang dịch? Lần khởi động sau sẽ **tự chạy tiếp** các bản dịch còn dở.

Muốn xóa sạch để demo lại từ đầu: `python -m app.db.reset --yes`

### Ràng buộc khóa ngoại — dữ liệu đang được tham chiếu thì không xóa được

13 khóa ngoại, tất cả `ON DELETE NO ACTION` (xem [docs/02-erd.md](docs/02-erd.md)). Thử trong SSMS:

```sql
USE audio_guide;
DELETE FROM pois WHERE code = N'TUONG_QUAN_AM';      -- lỗi 547: còn bản dịch / lượt tham quan / lộ trình trỏ tới
DELETE FROM languages WHERE code = N'en';            -- lỗi 547: còn bản dịch tiếng Anh
```

Trong ứng dụng, xóa POI còn bản dịch → HTTP 409 kèm hướng dẫn (xóa bản dịch trước; POI đã có lượt tham quan thì ẩn đi).

## Kiểm thử

```bash
pytest                              # toàn bộ test (không cần SQL Server: dùng SQLite file tạm, BẬT khóa ngoại)
pytest tests/test_api               # lớp 1 - API (mock Service)
pytest tests/test_services          # lớp 2 - nghiệp vụ
pytest tests/test_repositories      # lớp 3 - truy vấn DB + ràng buộc khóa ngoại
pytest tests/test_e2e.py            # tích hợp đầu-cuối theo kịch bản PRD
# Chạy toàn bộ test trên SQL Server thật (CI làm vậy) - dùng 1 database RIÊNG cho test vì test xóa sạch bảng:
#   Windows: $env:TEST_DATABASE_URL="mssql+aioodbc://@localhost\SQLEXPRESS/audio_guide_test?driver=ODBC+Driver+18+for+SQL+Server&TrustServerCertificate=yes"; pytest
pytest --cov                        # đo độ phủ (219 test, hiện ~90%)
ruff check . && ruff format --check .   # kiểm tra style
```

## CI/CD

```
push / PR ─▶ CI: lint ─▶ unit test (Py 3.11 + 3.12, coverage ≥ 80%) ─▶ build Docker + smoke test
                     └─▶ integration test trên SQL Server 2022 thật ┘
merge main ─▶ CD: build image ─▶ push ghcr.io ─▶ Render deploy hook ─▶ kiểm tra /health/ready
```

**Hiện trạng:** CI chạy đủ 4 job trên mỗi lần push / Pull Request. CD đã build và đẩy image lên GHCR khi CI trên `main` xanh; **nhóm chưa thuê server**, nên bước gọi Render và kiểm tra `/health/ready` tự bỏ qua (chưa có secret `RENDER_DEPLOY_HOOK_URL`, `APP_URL`).

Muốn deploy thật: tạo Web Service trên Render từ `Dockerfile` (image đã cài sẵn ODBC Driver 18), đặt `DATABASE_URL` trỏ tới một SQL Server trên cloud (vd Azure SQL Database), `APP_ENV=production`, `JWT_SECRET`, `FIRST_ADMIN_PASSWORD`, `PAYMENT_WEBHOOK_SECRET`; rồi thêm 2 secret trên vào GitHub.

## Cấu trúc thư mục

```
app/
├── main.py                 # khởi tạo app, gắn router, khởi động/seed dữ liệu
├── core/                   # cấu hình, bảo mật (JWT, bcrypt, HMAC), ngoại lệ nghiệp vụ
├── models/                 # domain model (ERD) + view model của Service
├── api/          ◀ LỚP 1   # deps.py (DI), errors.py (xử lý lỗi tập trung), schemas/, v1/endpoints/
├── services/     ◀ LỚP 2   # nghiệp vụ
├── repositories/ ◀ LỚP 3   # truy cập SQL Server (SQLAlchemy)
├── integrations/ ◀ LỚP 3   # dịch máy, TTS, LLM, cổng thanh toán, lưu file
└── db/                     # tables.py (lược đồ + khóa ngoại), database.py (kết nối), dữ liệu khởi tạo
frontend/                   # app du khách + admin/ (HTML/CSS/JS thuần)
tests/                      # test_api/ · test_services/ · test_repositories/ · test_e2e.py
docs/                       # kiến trúc, ERD, use case, đặc tả use case, database/ (sơ đồ + script SQL), bao-cao/
.github/workflows/          # ci.yml · cd.yml
```

## Tài liệu

1. [Kiến trúc 3 lớp](docs/01-kien-truc-3-lop.md)
2. [ERD — thiết kế dữ liệu](docs/02-erd.md) · sơ đồ [docs/database/erd-sqlserver.png](docs/database/erd-sqlserver.png) · script [docs/database/schema-sqlserver.sql](docs/database/schema-sqlserver.sql)
3. [Use Case](docs/03-use-case.md)
4. [Đặc tả Use Case](docs/10-dac-ta-use-case.md)
5. Danh sách API: chạy server rồi mở http://localhost:8000/docs (Swagger, tự sinh từ code)
6. Báo cáo, DFD, biểu đồ tuần tự: thư mục [docs/bao-cao/](docs/bao-cao/)

## Lưu ý

- **Dữ liệu mẫu** (tọa độ, nội dung, giờ mở cửa) chỉ để minh họa — cần đo tọa độ thật tại chùa và đối chiếu nội dung với tài liệu chính thức trước khi demo.
- **Thanh toán** dùng cổng giả lập (không trừ tiền thật) thay cho Payoo trong PRD.
- Dịch máy, Edge-TTS và chatbot AI cần Internet; mất mạng thì app tự lùi về tiếng Anh/tiếng Việt, giọng đọc của trình duyệt và chế độ trích đoạn tài liệu.
