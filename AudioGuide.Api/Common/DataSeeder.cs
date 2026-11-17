using System.Reflection;
using System.Text.Json;
using AudioGuide.Api.Data;
using AudioGuide.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AudioGuide.Api.Common
{
    /*
     * Khởi tạo dữ liệu ban đầu lúc chạy ứng dụng:
     *  1. Vai trò Admin / Staff (bảng AspNetRoles)
     *  2. Tài khoản admin đầu tiên nếu chưa có tài khoản nào (mặc định admin / admin123 - đổi trong appsettings)
     *  3. 16 ngôn ngữ (thêm nếu thiếu, không ghi đè trạng thái bật/tắt admin đã chỉnh)
     *  4. Dữ liệu MẪU của Chùa Linh Ứng (6 điểm, 2 lộ trình, 7 bài viết) - CHỈ khi chưa có điểm nào
     * Dữ liệu mẫu nằm ở Data/SeedData/*.json, được nhúng vào file .dll lúc build.
     */
    public static class DataSeeder
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private record LanguageSeed(string Code, string Name, string NativeName, string TranslatorCode, string TtsVoice, int SortOrder);
        private record HourSeed(int Day, string Open, string Close);
        private record PoiSeed(string Code, string Name, string Description, string Category, double Latitude, double Longitude,
            int TriggerRadiusM, int Priority, int SortOrder, string? ThumbnailUrl, List<HourSeed> OpeningHours, int EntryFeeVnd,
            int? VisitMinutes, List<string> Amenities, string Tips);
        private record TourSeed(string Code, string Name, string? Description, int EstimatedMinutes, List<string> PoiCodes);
        private record ArticleSeed(string Title, string Content, List<string> Tags);

        private static T Load<T>(string name)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SeedData." + name)
                               ?? throw new InvalidOperationException($"Thiếu file dữ liệu nhúng SeedData.{name}");
            return JsonSerializer.Deserialize<T>(stream, Json)!;
        }

        public static async Task SeedAsync(IServiceProvider services, ILogger logger)
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var settings = services.GetRequiredService<IOptions<SeedSettings>>().Value;

            // 1. Vai trò
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in Enum.GetNames<AppRole>())
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));

            // 2. Admin đầu tiên
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            if (!await userManager.Users.AnyAsync())
            {
                var admin = new ApplicationUser
                {
                    UserName = settings.AdminUserName,
                    FullName = "Quản trị viên",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(admin, settings.AdminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, nameof(AppRole.Admin));
                    logger.LogInformation("Đã tạo tài khoản quản trị đầu tiên: {User}", settings.AdminUserName);
                }
                else
                {
                    logger.LogError("Không tạo được admin: {Errors}", string.Join(" ", result.Errors.Select(e => e.Description)));
                }
            }

            // 3. Ngôn ngữ
            foreach (var seed in Load<List<LanguageSeed>>("Languages.json"))
            {
                var language = await db.Languages.FirstOrDefaultAsync(l => l.Code == seed.Code);
                if (language == null)
                {
                    language = new Language { Code = seed.Code, IsActive = true };
                    db.Languages.Add(language);
                }
                language.Name = seed.Name;
                language.NativeName = seed.NativeName;
                language.TranslatorCode = seed.TranslatorCode;
                language.TtsVoice = seed.TtsVoice;
                language.SortOrder = seed.SortOrder;
            }
            await db.SaveChangesAsync();

            // 4. Dữ liệu mẫu (chỉ 1 lần, khi chưa có điểm tham quan nào)
            if (!settings.SampleData || await db.Pois.AnyAsync()) return;
            var now = DateTime.UtcNow;
            var codeToPoi = new Dictionary<string, Poi>();
            foreach (var seed in Load<List<PoiSeed>>("Pois.json"))
            {
                var poi = new Poi
                {
                    Code = seed.Code, Name = seed.Name, Description = seed.Description, Category = seed.Category,
                    Latitude = seed.Latitude, Longitude = seed.Longitude, TriggerRadiusM = seed.TriggerRadiusM,
                    Priority = seed.Priority, SortOrder = seed.SortOrder, ThumbnailUrl = seed.ThumbnailUrl,
                    EntryFeeVnd = seed.EntryFeeVnd, VisitMinutes = seed.VisitMinutes, Amenities = seed.Amenities,
                    Tips = seed.Tips ?? "", IsActive = true, CreatedAt = now, UpdatedAt = now,
                    OpeningHours = seed.OpeningHours.Select(h => new PoiOpeningHour { Day = h.Day, Open = h.Open, Close = h.Close }).ToList()
                };
                db.Pois.Add(poi);
                codeToPoi[poi.Code] = poi;
            }
            await db.SaveChangesAsync();

            var tours = Load<List<TourSeed>>("Tours.json");
            foreach (var seed in tours)
            {
                db.Tours.Add(new Tour
                {
                    Code = seed.Code, Name = seed.Name, Description = seed.Description, EstimatedMinutes = seed.EstimatedMinutes,
                    IsActive = true, CreatedAt = now, UpdatedAt = now,
                    TourPois = seed.PoiCodes.Select((code, i) => new TourPoi { Position = i, PoiId = codeToPoi[code].Id }).ToList()
                });
            }
            var articles = Load<List<ArticleSeed>>("Articles.json");
            foreach (var seed in articles)
            {
                db.KnowledgeArticles.Add(new KnowledgeArticle
                {
                    Title = seed.Title, Content = seed.Content, Tags = seed.Tags, IsActive = true, CreatedAt = now, UpdatedAt = now
                });
            }
            await db.SaveChangesAsync();
            logger.LogInformation("Đã thêm dữ liệu mẫu: {Pois} điểm, {Tours} lộ trình, {Articles} bài viết",
                codeToPoi.Count, tours.Count, articles.Count);
        }
    }
}
