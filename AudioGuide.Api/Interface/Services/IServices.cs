using AudioGuide.Api.DTOs.Access;
using AudioGuide.Api.DTOs.Ai;
using AudioGuide.Api.DTOs.Auth;
using AudioGuide.Api.DTOs.Chat;
using AudioGuide.Api.DTOs.Common;
using AudioGuide.Api.DTOs.Content;
using AudioGuide.Api.DTOs.Knowledge;
using AudioGuide.Api.DTOs.Language;
using AudioGuide.Api.DTOs.Localization;
using AudioGuide.Api.DTOs.Poi;
using AudioGuide.Api.DTOs.Stats;
using AudioGuide.Api.DTOs.Tour;
using AudioGuide.Api.Models;

namespace AudioGuide.Api.Interface.Services
{
    /*
     * Giao diện lớp 2 - Business Logic. Controller chỉ gọi các interface này,
     * không biết bên dưới dùng database, dịch máy hay AI nào.
     */
    public interface ILanguageService
    {
        string SourceLang { get; }
        string FallbackLang { get; }
        Task<List<LanguageDto>> GetActiveAsync();
        Task<List<LanguageAdminDto>> GetAllAsync();
        Task<Language> GetActiveEntityAsync(string code);
        Task<string> ResolveCodeAsync(string? code);
        Task<LanguageAdminDto> SetActiveAsync(string code, bool isActive);
    }

    public interface ILocalizationService
    {
        Task<List<PoiLocalization>> LocalizePoiAsync(int poiId, IReadOnlyCollection<string>? langs = null, bool force = false,
            CancellationToken ct = default);
        Task<PoiLocalization> EnsureAsync(int poiId, string lang);
        Task<int> SchedulePoiAsync(int poiId, IReadOnlyCollection<string>? langs = null, bool force = false);
        Task<int> ScheduleAllAsync(bool force = false);
        Task MarkOutdatedAsync(int poiId);
        Task UpdateManualAsync(int poiId, string lang, ManualLocalizationDto dto);
        Task<int> DeleteForPoiAsync(int poiId);
        Task DeleteOneAsync(int poiId, string lang);
        Task<List<LocalizationOverviewDto>> OverviewAsync(int poiId);
        Task<int> RequestLanguageAsync(string lang);
        Task<int> RecoverUnfinishedAsync();
        Task LocalizeTourAsync(int tourId, IReadOnlyCollection<string>? langs = null, CancellationToken ct = default);
        void ScheduleTour(int tourId, IReadOnlyCollection<string>? langs = null);
    }

    public interface IContentService
    {
        Task<List<LocalizedPoiDto>> ListPoisAsync(string lang);
        Task<LocalizedPoiDto> GetPoiAsync(int poiId, string lang);
        Task<List<LocalizedPoiDto>> NearbyAsync(double lat, double lng, double radiusM, string lang);
        Task<List<LocalizedTourDto>> ListToursAsync(string lang);
        Task<string> ComputeVersionAsync(string lang);
        Task<ContentBundleDto> BuildBundleAsync(string lang);
    }

    public interface IPoiService
    {
        Task<PagedResult<PoiDto>> SearchAsync(string? keyword, bool? isActive, int page, int size);
        Task<PoiDto> GetAsync(int id);
        Task<PoiDto> CreateAsync(CreatePoiDto dto);
        Task<PoiDto> UpdateAsync(int id, UpdatePoiDto dto);
        Task DeleteAsync(int id);
    }

    public interface IPoiInsightsService
    {
        Task<PoiInsightsDto> GetAsync(int poiId);
    }

    public interface ITourService
    {
        Task<List<TourDto>> GetAllAsync();
        Task<TourDto> GetAsync(int id);
        Task<TourDto> CreateAsync(CreateTourDto dto);
        Task<TourDto> UpdateAsync(int id, UpdateTourDto dto);
        Task DeleteAsync(int id);
        Task<RecommendedRouteDto> RecommendAsync(RecommendRequestDto dto);
        Task<TourDistanceDto> GetDistanceAsync(int id);
    }

    public interface IJwtService
    {
        Task<(string Token, DateTime ExpiresAt)> CreateAdminTokenAsync(ApplicationUser user);
        string CreateVisitorToken(int sessionId, DateTime expiresAt);
    }

    public interface IAuthService
    {
        Task<LoginResultDto> LoginAsync(LoginDto dto);
        Task<UserDto> GetUserAsync(string userId);
        Task ChangePasswordAsync(string userId, ChangePasswordDto dto);
        Task ChangePasswordByLoginAsync(ChangePasswordByLoginDto dto);
        Task<List<UserDto>> ListUsersAsync();
        Task<UserDto> CreateUserAsync(CreateUserDto dto);
        Task<UserDto> UpdateUserAsync(string id, UpdateUserDto dto, string actorId);
    }

    public interface IAccessService
    {
        AccessInfoDto GetInfo();
        Task<AccessSession> CreateSessionAsync(AccessMethod method, AccessStatus status, string? createdById = null, string? note = null);
        Task<List<AccessSessionDto>> CreateCashCodesAsync(string staffId, int quantity, string? note);
        Task<AccessTokenDto> RedeemAsync(string code);
        Task<AccessSession> VerifySessionAsync(int sessionId);
        Task<PagedResult<AccessSessionDto>> SearchAsync(AccessStatus? status, int page, int size);
        Task<AccessSessionDto> RevokeAsync(int sessionId);
    }

    public interface IPaymentService
    {
        Task<OnlinePaymentDto> StartOnlinePaymentAsync();
        Task<(Payment Payment, AccessSession Session)> GetPaymentAsync(int paymentId);
        Task<Payment> HandleWebhookAsync(WebhookRequestDto dto, string signature);
        Task<Payment> CompleteMockPaymentAsync(int paymentId, bool success);
    }

    public interface IKnowledgeService
    {
        Task<List<ArticleDto>> GetAllAsync();
        Task<ArticleDto> CreateAsync(CreateArticleDto dto);
        Task<ArticleDto> UpdateAsync(int id, UpdateArticleDto dto);
        Task DeleteAsync(int id);
    }

    public interface IChatService
    {
        Task<ChatAnswerDto> AskAsync(string question, string lang, int? sessionId);
    }

    public interface IStatsService
    {
        Task RecordEventAsync(EventRequestDto dto, int? sessionId);
        Task<List<ChatLogDto>> RecentChatsAsync(int limit);
        Task<StatsDto> OverviewAsync();
        Task<List<CodesByDayDto>> CodesByDayAsync(DateOnly? from, DateOnly? to);
    }

    public interface IUiTextService
    {
        Task<UiStringsDto> GetStringsAsync(string lang);
    }

    public interface IMediaService
    {
        Task<(byte[] Data, string ContentType)> GetAsync(string key);
    }

    public interface IAiAssistantService
    {
        Task<EnhanceResultDto> EnhanceDescriptionAsync(EnhanceRequestDto dto);
    }
}
