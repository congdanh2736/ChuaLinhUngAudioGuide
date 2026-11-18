using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using AudioGuide.Api.BackgroundJobs;
using AudioGuide.Api.Common;
using AudioGuide.Api.Data;
using AudioGuide.Api.Integrations;
using AudioGuide.Api.Interface.Integrations;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Interface.Services;
using AudioGuide.Api.Middleware;
using AudioGuide.Api.Models;
using AudioGuide.Api.Repositories;
using AudioGuide.Api.Services;
using AudioGuide.Api.Validators.Poi;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

/*
 * ĐIỂM KHỞI ĐỘNG ỨNG DỤNG - "lắp ráp" 3 lớp với nhau bằng Dependency Injection.
 *
 * Kiến trúc 3 lớp (luồng gọi MỘT CHIỀU, không đi ngược, không nhảy cóc):
 *
 *   [Lớp 1 - Presentation]  Controllers/         nhận HTTP request, validate (FluentValidation), trả JSON
 *            │                                    + wwwroot/ (giao diện HTML/CSS/JS gọi API)
 *            ▼
 *   [Lớp 2 - Business]      Services/            quy tắc nghiệp vụ, đổi Entity <-> DTO (AutoMapper)
 *            │
 *            ▼
 *   [Lớp 3 - Data Access]   Repositories/        SQL Server qua Entity Framework Core
 *                           Integrations/        dịch máy, Edge-TTS, AI tạo sinh, cổng thanh toán, lưu audio
 */
var builder = WebApplication.CreateBuilder(args);

// Không in từng câu SQL của EF Core ra console (câu SQL lỗi vẫn được in)
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

//--------------------------------------------------------------[CẤU HÌNH ĐỌC TỪ appsettings.json]-----------------------------------------------//
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<AccessSettings>(builder.Configuration.GetSection("Access"));
builder.Services.Configure<PaymentSettings>(builder.Configuration.GetSection("Payment"));
builder.Services.Configure<LocalizationSettings>(builder.Configuration.GetSection("Localization"));
builder.Services.Configure<TranslatorSettings>(builder.Configuration.GetSection("Translator"));
builder.Services.Configure<TtsSettings>(builder.Configuration.GetSection("Tts"));
builder.Services.Configure<LlmSettings>(builder.Configuration.GetSection("Llm"));
builder.Services.Configure<ChatSettings>(builder.Configuration.GetSection("Chat"));
builder.Services.Configure<SiteSettings>(builder.Configuration.GetSection("Site"));
builder.Services.Configure<SeedSettings>(builder.Configuration.GetSection("Seed"));
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();

//--------------------------------------------------------------[CORS: CHO FRONTEND DEPLOY RIÊNG (VERCEL) GỌI API]--------------------------------//
/*
 * Frontend chạy chung địa chỉ với backend thì không cần CORS.
 * Frontend deploy ở domain khác (Vercel) thì phải khai báo domain đó trong Cors:AllowedOrigins.
 * API dùng token ở header Authorization (không dùng cookie) nên không cần AllowCredentials.
 */
var corsOrigins = builder.Configuration.GetSection("Cors").Get<CorsSettings>()?.AllowedOrigins ?? Array.Empty<string>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .SetIsOriginAllowed(origin => corsOrigins.Any(allowed => OriginMatches(origin, allowed)))
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("ETag")));   // app du khách đọc header ETag của /api/content/bundle

static bool OriginMatches(string origin, string allowed)
{
    allowed = allowed.Trim().TrimEnd('/');
    if (string.IsNullOrEmpty(allowed)) return false;
    if (!allowed.Contains("://*.")) return string.Equals(origin, allowed, StringComparison.OrdinalIgnoreCase);
    // "https://*.vercel.app" -> scheme phải khớp, host kết thúc bằng ".vercel.app"
    var scheme = allowed[..allowed.IndexOf("://", StringComparison.Ordinal)];
    var suffix = allowed[(allowed.IndexOf("://*", StringComparison.Ordinal) + 4)..];
    return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && uri.Scheme.Equals(scheme, StringComparison.OrdinalIgnoreCase)
        && uri.Host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
}

//--------------------------------------------------------------[DATABASE: SQL SERVER + EF CORE]-------------------------------------------------//
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

//--------------------------------------------------------------[IDENTITY: TÀI KHOẢN QUẢN TRỊ]-------------------------------------------------//
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 6;
        options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._";
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

//--------------------------------------------------------------[XÁC THỰC JWT]------------------------------------------------------------------//
/*
 * Mọi request đều được đọc token (nếu có) ở header Authorization: Bearer <token>.
 * - [Authorize(Roles = "Admin")] / RoleGroups.Staff: API quản trị
 * - [VisitorAccess]: API du khách (token du khách hoặc token quản trị đều được)
 */
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.Name,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            // Token quản trị: tài khoản đã bị khóa / xóa thì token cũ cũng hết hiệu lực ngay
            OnTokenValidated = async ctx =>
            {
                var principal = ctx.Principal!;
                if (!principal.IsInRole(nameof(AppRole.Admin)) && !principal.IsInRole(nameof(AppRole.Staff))) return;
                var userManager = ctx.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await userManager.FindByIdAsync(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
                if (user == null || !user.IsActive) ctx.Fail("Tài khoản không còn hiệu lực");
            },
            // Trả lỗi 401 / 403 dạng JSON giống các lỗi khác (frontend đọc message)
            OnChallenge = async ctx =>
            {
                ctx.HandleResponse();
                var expired = ctx.AuthenticateFailure is SecurityTokenExpiredException;
                await ExceptionMiddleware.WriteErrorAsync(ctx.HttpContext, StatusCodes.Status401Unauthorized,
                    expired ? "Phiên đăng nhập đã hết hạn" : "Vui lòng đăng nhập", expired ? "TOKEN_EXPIRED" : "NOT_AUTHENTICATED");
            },
            OnForbidden = ctx => ExceptionMiddleware.WriteErrorAsync(ctx.HttpContext, StatusCodes.Status403Forbidden,
                "Chức năng này chỉ dành cho quản trị viên", "ADMIN_ONLY")
        };
    });
builder.Services.AddAuthorization();

//--------------------------------------------------------------[CONTROLLER + JSON + VALIDATION]-----------------------------------------------//
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// FluentValidation: dữ liệu sai -> trả 400 kèm lỗi từng ô, không vào tới Controller
builder.Services.AddValidatorsFromAssemblyContaining<CreatePoiValidator>();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = ctx =>
    {
        var errors = ctx.ModelState.Where(kv => kv.Value!.Errors.Count > 0)
            .ToDictionary(
                kv => string.IsNullOrEmpty(kv.Key) ? "body" : char.ToLowerInvariant(kv.Key[0]) + kv.Key[1..],
                kv => kv.Value!.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Giá trị không hợp lệ" : e.ErrorMessage).ToArray());
        return new BadRequestObjectResult(new { message = "Dữ liệu gửi lên không hợp lệ", code = "VALIDATION_ERROR", errors });
    };
});
// AutoMapper: quét các Profile trong thư mục Mappings
builder.Services.AddAutoMapper(cfg => { }, typeof(Program).Assembly);

//--------------------------------------------------------------[SWAGGER: TÀI LIỆU API]-----------------------------------------------------------//
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Thuyết minh Chùa Linh Ứng API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Dán token (không cần chữ Bearer)",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer", document), new List<string>() }
    });
});

//--------------------------------------------------------------[ĐĂNG KÝ REPOSITORY (LỚP 3)]-------------------------------------------------//
builder.Services.AddScoped<ILanguageRepository, LanguageRepository>();
builder.Services.AddScoped<IPoiRepository, PoiRepository>();
builder.Services.AddScoped<IPoiLocalizationRepository, PoiLocalizationRepository>();
builder.Services.AddScoped<ITourRepository, TourRepository>();
builder.Services.AddScoped<IAccessSessionRepository, AccessSessionRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IKnowledgeRepository, KnowledgeRepository>();
builder.Services.AddScoped<IChatLogRepository, ChatLogRepository>();
builder.Services.AddScoped<IVisitEventRepository, VisitEventRepository>();
builder.Services.AddScoped<IUiTranslationRepository, UiTranslationRepository>();
builder.Services.AddScoped<IChatCacheRepository, ChatCacheRepository>();
builder.Services.AddScoped<IMediaFileRepository, MediaFileRepository>();

//--------------------------------------------------------------[ĐĂNG KÝ DỊCH VỤ NGOÀI (LỚP 3)]-----------------------------------------------//
builder.Services.AddHttpClient("translator", c => c.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddHttpClient("llm");
builder.Services.AddSingleton<ITranslator>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<TranslatorSettings>>().Value;
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("translator");
    var logger = sp.GetRequiredService<ILogger<FallbackTranslator>>();
    return settings.Provider.ToLowerInvariant() switch
    {
        // Google -> MyMemory: dịch vụ này bị chặn thì tự chuyển sang dịch vụ kế tiếp
        "google" or "auto" => new FallbackTranslator(new INamedTranslator[]
        {
            new GoogleApiTranslator(http), new MyMemoryTranslator(http, settings.MyMemoryEmail)
        }, logger),
        "mymemory" => new FallbackTranslator(new INamedTranslator[] { new MyMemoryTranslator(http, settings.MyMemoryEmail) }, logger),
        _ => new DisabledTranslator()
    };
});
builder.Services.AddHttpClient("tts", c => c.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddSingleton<ITtsProvider>(sp =>
{
    // edge = Edge-TTS, lỗi thì tự chuyển sang Google | google = chỉ dùng Google | none = tắt (app đọc bằng giọng trình duyệt)
    var provider = sp.GetRequiredService<IOptions<TtsSettings>>().Value.Provider.ToLowerInvariant();
    var google = new GoogleTranslateTts(sp.GetRequiredService<IHttpClientFactory>().CreateClient("tts"),
        sp.GetRequiredService<ILogger<GoogleTranslateTts>>());
    var fallbackLogger = sp.GetRequiredService<ILogger<FallbackTts>>();
    return provider switch
    {
        "edge" or "auto" => new FallbackTts(new (string, ITtsProvider)[]
        {
            ("edge", new EdgeTtsClient(sp.GetRequiredService<ILogger<EdgeTtsClient>>())),
            ("google", google)
        }, fallbackLogger),
        "google" => google,
        _ => new DisabledTts()
    };
});
builder.Services.AddSingleton<ILlmClient>(sp => new OpenAiCompatibleLlmClient(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("llm"),
    sp.GetRequiredService<IOptions<LlmSettings>>(),
    sp.GetRequiredService<ILogger<OpenAiCompatibleLlmClient>>()));
builder.Services.AddSingleton<IPaymentGateway, MockPaymentGateway>();
builder.Services.AddScoped<IMediaStorage, DatabaseMediaStorage>();

//--------------------------------------------------------------[ĐĂNG KÝ SERVICE (LỚP 2)]-------------------------------------------------------//
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
builder.Services.AddHostedService<BackgroundTaskWorker>();

builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ILanguageService, LanguageService>();
builder.Services.AddScoped<ILocalizationService, LocalizationService>();
builder.Services.AddScoped<IContentService, ContentService>();
builder.Services.AddScoped<IPoiService, PoiService>();
builder.Services.AddScoped<IPoiInsightsService, PoiInsightsService>();
builder.Services.AddScoped<ITourService, TourService>();
builder.Services.AddScoped<IAccessService, AccessService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IKnowledgeService, KnowledgeService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<IUiTextService, UiTextService>();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddScoped<IAiAssistantService, AiAssistantService>();

//--------------------------------------------------------------[KHỞI ĐỘNG]--------------------------------------------------------------------//
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<ApplicationDbContext>();
    try
    {
        // Tạo database + bảng theo các Migration (chưa có thì tạo, có rồi thì chỉ áp dụng migration mới)
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical("Không kết nối được SQL Server: {Message}\n"
            + "  - Kiểm tra chuỗi kết nối ConnectionStrings:DefaultConnection trong appsettings.json\n"
            + "  - Máy có Visual Studio: dùng Server=(localdb)\\MSSQLLocalDB\n"
            + "  - SQL Server Express: Server=localhost\\SQLEXPRESS", ex.Message);
        return;
    }
    await DataSeeder.SeedAsync(services, app.Logger);
    // Server từng bị tắt khi đang dịch -> chạy tiếp các bản dịch còn dở
    await services.GetRequiredService<ILocalizationService>().RecoverUnfinishedAsync();
    app.Logger.LogInformation("Chatbot AI: {Llm}", services.GetRequiredService<ILlmClient>().Describe);
}

// Middleware xử lý lỗi đặt ĐẦU TIÊN để bắt được lỗi của mọi bước phía sau
app.UseMiddleware<ExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

// Giao diện HTML/CSS/JS trong wwwroot: "/" = app du khách, "/admin/" = trang quản trị
var webRootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (Directory.Exists(webRootPath))
{
    var webRootFiles = new PhysicalFileProvider(webRootPath);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = webRootFiles });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = webRootFiles,
        // Trình duyệt luôn hỏi lại server file có đổi chưa (không đổi thì 304, rất nhanh) -> sửa file F5 là thấy
        OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache"
    });
}
else
{
    app.Logger.LogWarning("Không tìm thấy thư mục wwwroot tại {Path}. Giao diện web sẽ không mở được.", webRootPath);
}

app.UseCors();           // phải đứng trước Authentication để request preflight (OPTIONS) không bị chặn
app.UseAuthentication(); // đọc token
app.UseAuthorization();  // kiểm tra vai trò
app.MapControllers();

app.Run();

// Cho phép project test tạo server thử (WebApplicationFactory)
public partial class Program { }
