# Thuyết minh tự động đa ngôn ngữ – Chùa Linh Ứng (.NET)

Bản ASP.NET Core (.NET 10) của đồ án, viết lại từ bản Python/FastAPI. Cấu trúc theo kiểu **ClothesStore.Api**: mô hình 3 lớp, Entity Framework Core (Code First), luồng dữ liệu **Repository → Service → Controller**, trả JSON cho frontend (HTML/JS thuần trong `wwwroot`).

## Chạy thử

Yêu cầu: Visual Studio 2026 (hoặc .NET SDK 10) và SQL Server LocalDB (cài kèm Visual Studio, workload *ASP.NET and web development*).

1. Mở `AudioGuide.slnx` bằng Visual Studio, chọn project `AudioGuide.Api`, nhấn **F5**.
   Hoặc dùng dòng lệnh: `cd AudioGuide.Api` rồi `dotnet run`.
2. Lần chạy đầu, app tự **tạo database `AudioGuideDb`** (chạy migration) và nạp dữ liệu mẫu: 16 ngôn ngữ, 6 điểm tham quan, 2 lộ trình, 7 bài kiến thức. Nó cũng tạo tài khoản quản trị **admin / admin123**. Hãy đổi mật khẩu này sau khi đăng nhập.
3. Mở trình duyệt:
   - `http://localhost:5080/`: app du khách (mua vé hoặc nhập mã → bản đồ, GPS, tự phát thuyết minh)
   - `http://localhost:5080/admin/`: trang quản trị
   - `http://localhost:5080/swagger`: tài liệu và thử API

Muốn dùng SQL Server khác LocalDB thì sửa `ConnectionStrings:DefaultConnection` trong `appsettings.json`.

Để máy khác trong cùng Wi-Fi truy cập được: `dotnet run --urls http://0.0.0.0:5080`, rồi mở `http://<IP-máy-bạn>:5080`.

### Lấy mã truy cập để thử app du khách

- **Mua online (giả lập):** bấm "Thanh toán online" → trang cổng thanh toán giả lập → "Thanh toán thành công" → app tự nhận mã.
- **Tiền mặt:** đăng nhập admin → *Mã truy cập* → tạo mã → nhập mã đó ở app du khách.

### Khóa AI (chatbot, viết lại mô tả) – KHÔNG commit khóa thật

Chatbot vẫn chạy khi không có khóa: nó trả lời bằng cách trích câu từ bài kiến thức (BM25). Muốn bật LLM thì đặt khóa bằng **User Secrets**, vì cách này không ghi khóa vào file trong repo:

```
cd AudioGuide.Api
dotnet user-secrets set "Llm:ApiKey" "<khóa của bạn>"
dotnet user-secrets set "Llm:Provider" "openrouter"   # hoặc gemini / groq
```

Trong Visual Studio: chuột phải project → *Manage User Secrets*.

## Chạy test

```
dotnet test
```

Có 76 test (xUnit + Moq). Chúng kiểm tra:
- công thức Haversine, sắp xếp lộ trình, BM25, giờ mở cửa;
- service, với repository được giả lập bằng Moq;
- AutoMapper, model EF khớp migration, và việc mọi câu LINQ của repository dịch được sang SQL.

Phần test không cần SQL Server.

## Cấu trúc project

```
AudioGuide.Api/
├── Controllers/        Lớp trình bày: nhận request, gọi Service, trả JSON (không chứa nghiệp vụ)
├── DTOs/               Dữ liệu vào/ra API (không trả thẳng Entity)
├── Validators/         FluentValidation: kiểm tra dữ liệu gửi lên → 400 VALIDATION_ERROR
├── Mappings/           AutoMapper: Entity ⇄ DTO
├── Services/           Lớp nghiệp vụ (Business)
│   └── Logic/          Hàm thuần: Haversine, BM25, giờ mở cửa, quy tắc bản dịch
├── Interface/          Interface của Repository / Service / Integration (để DI và test bằng Moq)
├── Repositories/       Lớp truy cập dữ liệu: chỉ làm việc với EF Core
├── Data/               ApplicationDbContext, DataSeeder, SeedData/*.json
├── Models/             Entity (bảng trong database)
├── Migrations/         Migration EF Core
├── Integrations/       Dịch (Google/MyMemory), Edge-TTS, LLM, cổng thanh toán giả lập, lưu file
├── BackgroundJobs/     Hàng đợi việc nền (dịch + tạo audio không làm treo request)
├── Filters/            [VisitorAccess]: kiểm tra token du khách
├── Middleware/         ExceptionMiddleware: lỗi → JSON {message, code}
├── Common/             Exception nghiệp vụ, Role, cấu hình, hằng số
└── wwwroot/            Frontend: app du khách (index.html) + trang quản trị (admin/)
AudioGuide.Tests/       xUnit + Moq
```

Ví dụ luồng **tạo điểm tham quan**:
1. `POST /api/pois` → `PoiController`.
2. `CreatePoiValidator` kiểm tra dữ liệu.
3. `PoiService.CreateAsync` xử lý nghiệp vụ: chuẩn hóa giờ mở cửa và tiện ích, kiểm tra mã trùng.
4. `PoiRepository` dùng EF Core để ghi SQL Server.
5. AutoMapper đổi kết quả sang `PoiDto`, và controller trả `201`.

## Phân công theo lớp

| Thành viên | Phần phụ trách |
|---|---|
| Tài | **Presentation**: `Controllers/`, `DTOs/`, `wwwroot/` (app du khách + trang quản trị), Swagger |
| Phát | **Business**: `Services/`, `Validators/`, `Mappings/`, `Integrations/`, `BackgroundJobs/` |
| Danh | **Data Access + CI/CD**: `Models/`, `Data/`, `Repositories/`, `Migrations/`, test, pipeline |

## Danh sách API chính

| Nhóm | Endpoint | Quyền |
|---|---|---|
| Đăng nhập | `POST /api/auth/login`, `GET /api/auth/me`, `POST /api/auth/change-password` | Admin/Staff |
| Người dùng | `GET/POST /api/users`, `PATCH /api/users/{id}` | Admin |
| Ngôn ngữ | `GET /api/languages`, `GET /api/languages/all`, `PATCH /api/languages/{code}` | công khai / Staff / Admin |
| Điểm tham quan | `GET/POST /api/pois`, `GET/PUT/DELETE /api/pois/{id}` | Staff xem, Admin sửa |
| Bản dịch | `GET /api/pois/{id}/localizations`, `POST /api/pois/{id}/localize`, `PUT/DELETE /api/pois/{id}/localizations/{lang}`, `POST /api/localizations/localize-all` | Staff / Admin |
| Lộ trình | `GET/POST /api/tours`, `GET/PUT/DELETE /api/tours/{id}`, `GET /api/tours/{id}/distance` | Staff / Admin |
| Kiến thức | `GET/POST /api/knowledge`, `PUT/DELETE /api/knowledge/{id}` | Staff / Admin |
| Thống kê, AI | `GET /api/stats`, `GET /api/stats/chat-logs`, `POST /api/ai/enhance-description` | Staff / Admin |
| Vé / truy cập | `GET /api/access/info`, `POST /api/access/online`, `POST /api/access/token`, `GET /api/access/me`, `POST /api/access/cash-codes`, `GET /api/access/sessions`, `POST /api/access/sessions/{id}/revoke` | du khách / Staff / Admin |
| Thanh toán | `POST /api/payments/webhook` (header `X-Signature`), `/mock-gateway/{id}` | cổng thanh toán |
| Nội dung du khách | `GET /api/content/bundle` (ETag/304), `/api/content/pois`, `/pois/nearby`, `/pois/{id}`, `/pois/{id}/insights`, `POST /pois/{id}/localizations/{lang}/ensure`, `/tours`, `POST /tours/recommend`, `POST /events` | token du khách |
| Chatbot | `POST /api/chat` | token du khách |
| Chữ giao diện | `GET /api/ui-strings/{lang}` | công khai |
| Khác | `GET /media/{key}`, `GET /health`, `GET /health/ready` | công khai |

Lỗi luôn có dạng `{ "message": "...", "code": "..." }`. Ví dụ:
- `NOT_FOUND`
- `VALIDATION_ERROR`, kèm thêm `errors`
- `ACCESS_REQUIRED`
- `PAYMENT_PENDING`
- `REFERENCED_BY_OTHER_DATA`

## Ghi chú

- Thuyết minh audio dùng Edge-TTS. Nếu không gọi được dịch vụ này, app du khách tự đọc bằng giọng của trình duyệt.
- Không commit khóa API thật, file `.env` hay `appsettings.Local.json`. Hãy dùng User Secrets như hướng dẫn ở trên.
