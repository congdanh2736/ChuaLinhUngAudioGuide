using AudioGuide.Api.Interface.Repositories.Base;
using AudioGuide.Api.Models;

namespace AudioGuide.Api.Interface.Repositories
{
    /*
     * Giao diện (interface) các repository.
     * Service chỉ biết tới interface -> khi test có thể thay bằng repository giả (Moq) mà không cần database.
     */
    public interface ILanguageRepository : IRepository<Language>
    {
        Task<Language?> GetByCodeAsync(string code);
        Task<List<Language>> ListAsync(bool activeOnly);
        Task<int> CountActiveAsync();
    }

    public interface IPoiRepository : IRepository<Poi>
    {
        Task<Poi?> GetByCodeAsync(string code);
        Task<Poi?> GetWithDetailsAsync(int id);                 // kèm khung giờ mở cửa (có theo dõi để sửa)
        Task<List<Poi>> ListActiveAsync();                     // điểm đang hiển thị, kèm khung giờ
        Task<List<Poi>> ListAllAsync();
        Task<(List<Poi> Items, int Total)> SearchAsync(string? keyword, bool? isActive, int skip, int take);
        Task<List<int>> GetMissingIdsAsync(IEnumerable<int> ids); // các id KHÔNG tồn tại
        Task<int> CountActiveAsync();
        Task<DateTime?> LatestUpdateAsync();
    }

    public interface IPoiLocalizationRepository : IRepository<PoiLocalization>
    {
        Task<PoiLocalization?> GetAsync(int poiId, string lang);
        Task<List<PoiLocalization>> ListForPoiAsync(int poiId);
        Task<List<PoiLocalization>> ListForLangsAsync(IEnumerable<string> langs, IEnumerable<int>? poiIds = null);
        Task<List<PoiLocalization>> ListByStatusAsync(params LocalizationStatus[] statuses);
        Task<PoiLocalization> UpsertAsync(int poiId, string lang, Action<PoiLocalization> apply);
        Task<int> MarkStatusForPoiAsync(int poiId, LocalizationStatus status);
        Task<int> CountForPoiAsync(int poiId);
        Task<int> DeleteForPoiAsync(int poiId);
        Task<bool> DeleteOneAsync(int poiId, string lang);
        Task<Dictionary<string, int>> CountByStatusAsync();
        Task<DateTime?> LatestUpdateAsync();
    }

    public interface ITourRepository : IRepository<Tour>
    {
        Task<Tour?> GetByCodeAsync(string code);
        Task<Tour?> GetWithDetailsAsync(int id);       // kèm danh sách điểm + bản dịch (có theo dõi để sửa)
        Task<List<Tour>> ListWithDetailsAsync(bool activeOnly);
        Task<int> RemovePoiEverywhereAsync(int poiId);  // gỡ 1 POI khỏi mọi lộ trình, trả về số lộ trình bị sửa
        Task<DateTime?> LatestUpdateAsync();
    }

    public interface IAccessSessionRepository : IRepository<AccessSession>
    {
        Task<AccessSession?> GetByCodeAsync(string code);
        Task<bool> CodeExistsAsync(string code);
        Task<(List<AccessSession> Items, int Total)> SearchAsync(AccessStatus? status, int skip, int take);
        Task<int> MarkExpiredAsync(DateTime now);
        Task<Dictionary<string, int>> CountByStatusAsync();
        Task<long> TotalRevenueAsync();
    }

    public interface IPaymentRepository : IRepository<Payment>
    {
    }

    public interface IKnowledgeRepository : IRepository<KnowledgeArticle>
    {
        Task<List<KnowledgeArticle>> ListAsync(bool activeOnly);
    }

    public interface IChatLogRepository : IRepository<ChatLog>
    {
        Task<int> CountSinceAsync(int sessionId, DateTime since);
        Task<List<ChatLog>> RecentForSessionAsync(int sessionId, int limit); // từ cũ đến mới
        Task<List<ChatLog>> RecentAsync(int limit);
    }

    public interface IVisitEventRepository : IRepository<VisitEvent>
    {
        Task<int> CountForPoiAsync(int poiId);
        Task<List<(int PoiId, int Count)>> TopPoisAsync(int limit);
        Task<List<DateTime>> TimesForPoiAsync(int poiId, DateTime since);
        Task<Dictionary<string, int>> CountByLangAsync();
    }

    public interface IUiTranslationRepository
    {
        Task<UiTranslation?> GetAsync(string lang);
        Task SaveAsync(string lang, string stringsJson, string sourceHash);
    }

    public interface IChatCacheRepository
    {
        Task<string?> GetAsync(string key);
        Task SetAsync(string key, string answerJson, int ttlSeconds);
    }

    public interface IMediaFileRepository
    {
        Task<bool> ExistsAsync(string key);
        Task<MediaFile?> GetAsync(string key);
        Task SaveAsync(string key, byte[] data, string contentType);
    }
}
