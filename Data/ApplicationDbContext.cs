using AudioGuide.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AudioGuide.Api.Data
{
    /*
     * Kết nối SQL Server bằng Entity Framework Core (Code First).
     * Các lớp trong Models/ là "bản thiết kế" bảng; hàm OnModelCreating mô tả khóa chính, khóa ngoại,
     * ràng buộc UNIQUE, độ dài cột... Lệnh Add-Migration / Update-Database (hoặc lúc chạy app) tạo bảng tương ứng.
     *
     * QUY ƯỚC KHÓA NGOẠI: hầu hết là ON DELETE NO ACTION (DeleteBehavior.NoAction)
     * => bản ghi "cha" đang được bản ghi "con" tham chiếu thì KHÔNG XÓA ĐƯỢC, kể cả xóa thẳng trong SSMS
     *    (SQL Server báo lỗi 547). Vd: còn bản dịch thì không xóa được điểm tham quan.
     * Ngoại lệ: bảng con "thuộc sở hữu" của bảng cha (khung giờ mở cửa của POI, danh sách điểm và bản dịch
     *    của lộ trình) dùng CASCADE - xóa cha thì con đi theo.
     */
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // Danh mục nội dung
        public DbSet<Language> Languages => Set<Language>();
        public DbSet<Poi> Pois => Set<Poi>();
        public DbSet<PoiOpeningHour> PoiOpeningHours => Set<PoiOpeningHour>();
        public DbSet<PoiLocalization> PoiLocalizations => Set<PoiLocalization>();
        public DbSet<Tour> Tours => Set<Tour>();
        public DbSet<TourPoi> TourPois => Set<TourPoi>();
        public DbSet<TourTranslation> TourTranslations => Set<TourTranslation>();

        // Quyền truy cập, thanh toán
        public DbSet<AccessSession> AccessSessions => Set<AccessSession>();
        public DbSet<Payment> Payments => Set<Payment>();

        // Chatbot, thống kê
        public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();
        public DbSet<ChatLog> ChatLogs => Set<ChatLog>();
        public DbSet<VisitEvent> VisitEvents => Set<VisitEvent>();

        // Bảng kỹ thuật
        public DbSet<UiTranslation> UiTranslations => Set<UiTranslation>();
        public DbSet<ChatCacheEntry> ChatCache => Set<ChatCacheEntry>();
        public DbSet<MediaFile> MediaFiles => Set<MediaFile>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder); // các bảng của Identity (AspNetUsers, AspNetRoles, ...)

            // ============ TÀI KHOẢN ============
            builder.Entity<ApplicationUser>(e =>
            {
                e.Property(x => x.FullName).IsRequired().HasMaxLength(255);
            });

            // ============ NGÔN NGỮ ============
            builder.Entity<Language>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Code).IsRequired().HasMaxLength(16);
                e.HasIndex(x => x.Code).IsUnique();
                e.Property(x => x.Name).IsRequired().HasMaxLength(100);
                e.Property(x => x.NativeName).IsRequired().HasMaxLength(100);
                e.Property(x => x.TranslatorCode).IsRequired().HasMaxLength(16);
                e.Property(x => x.TtsVoice).IsRequired().HasMaxLength(100);
            });

            // ============ ĐIỂM THAM QUAN ============
            builder.Entity<Poi>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Code).IsRequired().HasMaxLength(64);
                e.HasIndex(x => x.Code).IsUnique();
                e.Property(x => x.Name).IsRequired().HasMaxLength(255);
                e.Property(x => x.Description).IsRequired();
                e.Property(x => x.Category).IsRequired().HasMaxLength(50);
                e.Property(x => x.ThumbnailUrl).HasMaxLength(1000);
                e.Property(x => x.Tips).IsRequired();
                e.HasIndex(x => new { x.IsActive, x.SortOrder });
            });

            builder.Entity<PoiOpeningHour>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Open).IsRequired().HasMaxLength(5);
                e.Property(x => x.Close).IsRequired().HasMaxLength(5);
                e.HasOne(x => x.Poi)
                    .WithMany(p => p.OpeningHours)
                    .HasForeignKey(x => x.PoiId)
                    .OnDelete(DeleteBehavior.Cascade); // khung giờ thuộc sở hữu của POI
            });

            builder.Entity<PoiLocalization>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Lang).IsRequired().HasMaxLength(16);
                e.Property(x => x.Name).HasMaxLength(255);
                e.Property(x => x.AudioUrl).HasMaxLength(500);
                e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
                e.Property(x => x.TranslatedBy).HasConversion<string>().HasMaxLength(20);
                e.Property(x => x.SourceHash).HasMaxLength(64);
                e.Property(x => x.TipsHash).HasMaxLength(64);
                e.HasIndex(x => new { x.PoiId, x.Lang }).IsUnique(); // mỗi POI chỉ có 1 bản dịch / ngôn ngữ
                e.HasIndex(x => x.Status);

                e.HasOne(x => x.Poi)
                    .WithMany(p => p.Localizations)
                    .HasForeignKey(x => x.PoiId)
                    .OnDelete(DeleteBehavior.NoAction); // còn bản dịch -> không xóa được POI

                e.HasOne(x => x.Language)
                    .WithMany(l => l.PoiLocalizations)
                    .HasForeignKey(x => x.Lang)
                    .HasPrincipalKey(l => l.Code) // khóa ngoại trỏ tới cột Code (duy nhất) thay vì Id
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // ============ LỘ TRÌNH ============
            builder.Entity<Tour>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Code).IsRequired().HasMaxLength(64);
                e.HasIndex(x => x.Code).IsUnique();
                e.Property(x => x.Name).IsRequired().HasMaxLength(255);
            });

            builder.Entity<TourPoi>(e =>
            {
                e.HasKey(x => new { x.TourId, x.Position });
                e.HasOne(x => x.Tour)
                    .WithMany(t => t.TourPois)
                    .HasForeignKey(x => x.TourId)
                    .OnDelete(DeleteBehavior.Cascade); // xóa lộ trình -> xóa luôn danh sách điểm của nó
                e.HasOne(x => x.Poi)
                    .WithMany(p => p.TourPois)
                    .HasForeignKey(x => x.PoiId)
                    .OnDelete(DeleteBehavior.NoAction); // POI đang nằm trong lộ trình -> không xóa thẳng được
            });

            builder.Entity<TourTranslation>(e =>
            {
                e.HasKey(x => new { x.TourId, x.Lang });
                e.Property(x => x.Lang).HasMaxLength(16);
                e.Property(x => x.Name).IsRequired().HasMaxLength(255);
                e.HasOne(x => x.Tour)
                    .WithMany(t => t.Translations)
                    .HasForeignKey(x => x.TourId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Language)
                    .WithMany(l => l.TourTranslations)
                    .HasForeignKey(x => x.Lang)
                    .HasPrincipalKey(l => l.Code)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // ============ QUYỀN TRUY CẬP, THANH TOÁN ============
            builder.Entity<AccessSession>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Code).IsRequired().HasMaxLength(64);
                e.HasIndex(x => x.Code).IsUnique();
                e.Property(x => x.Method).HasConversion<string>().HasMaxLength(20);
                e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
                e.Property(x => x.Currency).IsRequired().HasMaxLength(8);
                e.Property(x => x.Note).HasMaxLength(500);
                e.HasIndex(x => new { x.Status, x.CreatedAt });
                e.HasOne(x => x.CreatedBy)
                    .WithMany(u => u.CreatedSessions)
                    .HasForeignKey(x => x.CreatedById)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<Payment>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Provider).IsRequired().HasMaxLength(50);
                e.Property(x => x.Currency).IsRequired().HasMaxLength(8);
                e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
                e.Property(x => x.ProviderRef).HasMaxLength(255);
                e.HasOne(x => x.Session)
                    .WithMany(s => s.Payments)
                    .HasForeignKey(x => x.SessionId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // ============ CHATBOT, THỐNG KÊ ============
            builder.Entity<KnowledgeArticle>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Title).IsRequired().HasMaxLength(255);
                e.Property(x => x.Content).IsRequired();
            });

            builder.Entity<ChatLog>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Lang).IsRequired().HasMaxLength(16);
                e.Property(x => x.Question).IsRequired();
                e.Property(x => x.Answer).IsRequired();
                e.HasIndex(x => new { x.SessionId, x.CreatedAt });
                e.HasOne(x => x.Session)
                    .WithMany(s => s.ChatLogs)
                    .HasForeignKey(x => x.SessionId)
                    .OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.Language)
                    .WithMany()
                    .HasForeignKey(x => x.Lang)
                    .HasPrincipalKey(l => l.Code)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<VisitEvent>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Lang).IsRequired().HasMaxLength(16);
                e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
                e.HasIndex(x => new { x.PoiId, x.CreatedAt });
                e.HasOne(x => x.Session)
                    .WithMany(s => s.VisitEvents)
                    .HasForeignKey(x => x.SessionId)
                    .OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.Poi)
                    .WithMany(p => p.VisitEvents)
                    .HasForeignKey(x => x.PoiId)
                    .OnDelete(DeleteBehavior.NoAction); // POI đã có lượt tham quan -> chỉ nên ẩn, không xóa
            });

            // ============ BẢNG KỸ THUẬT ============
            builder.Entity<UiTranslation>(e =>
            {
                e.HasKey(x => x.Lang);
                e.Property(x => x.Lang).HasMaxLength(16);
                e.Property(x => x.StringsJson).IsRequired();
                e.Property(x => x.SourceHash).IsRequired().HasMaxLength(64);
                e.HasOne(x => x.Language)
                    .WithMany()
                    .HasForeignKey(x => x.Lang)
                    .HasPrincipalKey(l => l.Code)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<ChatCacheEntry>(e =>
            {
                e.ToTable("ChatCache");
                e.HasKey(x => x.Key);
                e.Property(x => x.Key).HasMaxLength(64);
                e.Property(x => x.AnswerJson).IsRequired();
                e.HasIndex(x => x.ExpiresAt);
            });

            builder.Entity<MediaFile>(e =>
            {
                e.HasKey(x => x.Key);
                e.Property(x => x.Key).HasMaxLength(255);
                e.Property(x => x.Data).IsRequired(); // VARBINARY(MAX)
                e.Property(x => x.ContentType).IsRequired().HasMaxLength(100);
            });

            /*
             * Mọi cột thời gian đều lưu giờ UTC. SQL Server (DATETIME2) không lưu múi giờ nên khi đọc ra,
             * DateTime có Kind = Unspecified -> JSON trả về thiếu chữ "Z" và trình duyệt hiểu nhầm là giờ máy.
             * Bộ chuyển đổi dưới đây gắn lại Kind = Utc mỗi khi đọc.
             */
            var utc = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
                v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            var utcNullable = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
                v => v == null ? v : (v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime()),
                v => v == null ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc));
            foreach (var entity in builder.Model.GetEntityTypes())
            {
                foreach (var property in entity.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime)) property.SetValueConverter(utc);
                    else if (property.ClrType == typeof(DateTime?)) property.SetValueConverter(utcNullable);
                }
            }
        }
    }
}
