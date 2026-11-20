using System.Data;
using System.Data.Common;
using AudioGuide.Api.Data;
using AudioGuide.Api.Models;
using AudioGuide.Api.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AudioGuide.Tests.Infrastructure
{
    /*
     * Kiểm tra mọi câu truy vấn LINQ của Repository dịch được sang SQL của SQL Server
     * mà KHÔNG cần SQL Server thật: interceptor chặn việc mở kết nối / chạy lệnh và trả về kết quả rỗng.
     * Câu LINQ nào EF không dịch được ("could not be translated") -> test đỏ.
     */
    public class RepositoryQueryTranslationTests
    {
        private sealed class FakeDatabaseInterceptor : DbConnectionInterceptor, IDbCommandInterceptor
        {
            public List<string> Sql { get; } = new();

            public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
                InterceptionResult result, CancellationToken cancellationToken = default) => new(InterceptionResult.Suppress());

            public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
                => InterceptionResult.Suppress();

            public ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
                InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            {
                Sql.Add(command.CommandText);
                return new(InterceptionResult<DbDataReader>.SuppressWithResult(new DataTable().CreateDataReader()));
            }

            public ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
                InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                Sql.Add(command.CommandText);
                return new(InterceptionResult<int>.SuppressWithResult(0));
            }

            public ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData,
                InterceptionResult<object> result, CancellationToken cancellationToken = default)
            {
                Sql.Add(command.CommandText);
                return new(InterceptionResult<object>.SuppressWithResult(0));
            }
        }

        private static (ApplicationDbContext Db, FakeDatabaseInterceptor Interceptor) NewContext()
        {
            var interceptor = new FakeDatabaseInterceptor();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer("Server=fake;Database=fake;Trusted_Connection=True;")
                .AddInterceptors(interceptor)
                .Options;
            return (new ApplicationDbContext(options), interceptor);
        }

        // Chạy 1 câu truy vấn: chỉ chấp nhận lỗi do kết quả rỗng giả, không chấp nhận lỗi dịch LINQ -> SQL
        private static async Task Run(Func<Task> query)
        {
            try
            {
                await query();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Sequence contains no elements")
                                                       || ex.Message.Contains("Nullable object must have a value")
                                                       || ex.Message.Contains("doesn't have as many fields"))
            {
            }
        }

        [Fact]
        public async Task AllRepositoryQueries_TranslateToSql()
        {
            var (db, interceptor) = NewContext();
            var languages = new LanguageRepository(db);
            var pois = new PoiRepository(db);
            var locs = new PoiLocalizationRepository(db);
            var tours = new TourRepository(db);
            var sessions = new AccessSessionRepository(db);
            var knowledge = new KnowledgeRepository(db);
            var chats = new ChatLogRepository(db);
            var events = new VisitEventRepository(db);
            var ui = new UiTranslationRepository(db);
            var cache = new ChatCacheRepository(db);
            var media = new MediaFileRepository(db);
            var now = DateTime.UtcNow;

            var queries = new List<Func<Task>>
            {
                () => sessions.CountCreatedByDayAsync(now.AddDays(-30), now, 7),
                () => languages.GetByCodeAsync("vi"), () => languages.ListAsync(true), () => languages.CountActiveAsync(),
                () => pois.GetByCodeAsync("A"), () => pois.GetWithDetailsAsync(1), () => pois.ListActiveAsync(), () => pois.ListAllAsync(),
                () => pois.SearchAsync("cổng", true, 0, 20), () => pois.GetMissingIdsAsync(new[] { 1, 2 }), () => pois.CountActiveAsync(),
                () => pois.LatestUpdateAsync(), () => pois.CountAsync(),
                () => locs.GetAsync(1, "en"), () => locs.ListForPoiAsync(1), () => locs.ListForLangsAsync(new[] { "en", "vi" }, new[] { 1, 2 }),
                () => locs.ListByStatusAsync(LocalizationStatus.Pending, LocalizationStatus.Processing),
                () => locs.MarkStatusForPoiAsync(1, LocalizationStatus.Outdated), () => locs.CountForPoiAsync(1),
                () => locs.DeleteForPoiAsync(1), () => locs.DeleteOneAsync(1, "en"), () => locs.CountByStatusAsync(), () => locs.LatestUpdateAsync(),
                () => tours.GetByCodeAsync("T"), () => tours.GetWithDetailsAsync(1), () => tours.ListWithDetailsAsync(true),
                () => tours.RemovePoiEverywhereAsync(1), () => tours.LatestUpdateAsync(),
                () => sessions.GetByCodeAsync("ABC123"), () => sessions.CodeExistsAsync("ABC123"),
                () => sessions.SearchAsync(AccessStatus.Paid, 0, 20), () => sessions.SearchAsync(null, 0, 20),
                () => sessions.MarkExpiredAsync(now), () => sessions.CountByStatusAsync(), () => sessions.TotalRevenueAsync(),
                () => knowledge.ListAsync(true),
                () => chats.CountSinceAsync(1, now), () => chats.RecentForSessionAsync(1, 4), () => chats.RecentAsync(10),
                () => events.CountForPoiAsync(1), () => events.TopPoisAsync(5), () => events.TimesForPoiAsync(1, now), () => events.CountByLangAsync(),
                () => ui.GetAsync("ja"), () => cache.GetAsync("vi:gio mo cua"), () => media.ExistsAsync("audio/x.mp3"), () => media.GetAsync("audio/x.mp3")
            };
            foreach (var query in queries) await Run(query);

            Assert.True(interceptor.Sql.Count >= queries.Count - 2);
            Assert.All(interceptor.Sql, sql => Assert.False(string.IsNullOrWhiteSpace(sql)));
        }
    }
}
