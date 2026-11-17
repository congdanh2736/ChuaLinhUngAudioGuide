using AudioGuide.Api.BackgroundJobs;
using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Localization;
using AudioGuide.Api.Interface.Integrations;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Interface.Services;
using AudioGuide.Api.Models;
using AudioGuide.Api.Services.Logic;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AudioGuide.Api.Services
{
    /*
     * Nghiệp vụ ĐA NGÔN NGỮ - trái tim của đề tài.
     *
     * Pipeline cho mỗi điểm x mỗi ngôn ngữ:
     *   Nội dung gốc (vi) -> dịch máy -> văn bản đích -> Edge-TTS -> file mp3 -> lưu PoiLocalizations
     *
     * Quy tắc:
     * - SourceHash = MD5(tên + mô tả gốc). Bản dịch chỉ "còn hiệu lực" khi hash khớp nội dung gốc hiện tại.
     * - Bản admin sửa tay (Manual) không bị dịch máy ghi đè, trừ khi force = true.
     * - Lỗi 1 ngôn ngữ không làm hỏng các ngôn ngữ khác.
     * - Việc dịch chạy NỀN (BackgroundTaskQueue), API trả kết quả ngay.
     */
    public class LocalizationService : ILocalizationService
    {
        private readonly IPoiRepository _poiRepository;
        private readonly IPoiLocalizationRepository _localizationRepository;
        private readonly ILanguageRepository _languageRepository;
        private readonly ITourRepository _tourRepository;
        private readonly ITranslator _translator;
        private readonly ITtsProvider _tts;
        private readonly IMediaStorage _storage;
        private readonly IBackgroundTaskQueue _queue;
        private readonly IMemoryCache _cache;
        private readonly ILogger<LocalizationService> _logger;
        private readonly string _sourceLang;

        public LocalizationService(
            IPoiRepository poiRepository,
            IPoiLocalizationRepository localizationRepository,
            ILanguageRepository languageRepository,
            ITourRepository tourRepository,
            ITranslator translator,
            ITtsProvider tts,
            IMediaStorage storage,
            IBackgroundTaskQueue queue,
            IMemoryCache cache,
            IOptions<LocalizationSettings> options,
            ILogger<LocalizationService> logger)
        {
            _poiRepository = poiRepository;
            _localizationRepository = localizationRepository;
            _languageRepository = languageRepository;
            _tourRepository = tourRepository;
            _translator = translator;
            _tts = tts;
            _storage = storage;
            _queue = queue;
            _cache = cache;
            _logger = logger;
            _sourceLang = options.Value.SourceLanguage;
        }

        // ------------------------------------------------------------------ hỗ trợ
        private async Task<Poi> GetPoiAsync(int poiId)
            => await _poiRepository.GetByIdAsync(poiId) ?? throw new NotFoundException("Không tìm thấy điểm tham quan", "POI_NOT_FOUND");

        private async Task<List<Language>> TargetLanguagesAsync(IReadOnlyCollection<string>? langs)
        {
            var active = await _languageRepository.ListAsync(activeOnly: true);
            if (langs == null || langs.Count == 0) return active;
            var unknown = langs.Except(active.Select(l => l.Code)).OrderBy(x => x).ToList();
            if (unknown.Count > 0)
                throw new BusinessException($"Ngôn ngữ không hỗ trợ hoặc đang tắt: {string.Join(", ", unknown)}", "LANGUAGE_NOT_SUPPORTED");
            return active.Where(l => langs.Contains(l.Code)).ToList();
        }

        private async Task<string> SourceTranslatorCodeAsync()
            => (await _languageRepository.GetByCodeAsync(_sourceLang))?.TranslatorCode ?? _sourceLang;

        // Tạo (hoặc dùng lại) file mp3. Trả về (url, lỗi). Lỗi audio không làm mất bản dịch chữ.
        private async Task<(string? Url, string? Error)> MakeAudioAsync(string text, string voice, CancellationToken ct)
        {
            var key = _storage.AudioKey(text, voice);
            if (await _storage.ExistsAsync(key)) return (_storage.UrlFor(key), null);
            try
            {
                var data = await _tts.SynthesizeAsync(text, voice, ct);
                return (await _storage.SaveAsync(key, data), null);
            }
            catch (ExternalServiceException ex)
            {
                return (null, ex.Message);
            }
        }

        // ------------------------------------------------------------------ pipeline (chạy đồng bộ)
        public async Task<List<PoiLocalization>> LocalizePoiAsync(int poiId, IReadOnlyCollection<string>? langs = null, bool force = false,
            CancellationToken ct = default)
        {
            var poi = await GetPoiAsync(poiId);
            var languages = await TargetLanguagesAsync(langs);
            var sourceCode = await SourceTranslatorCodeAsync();
            var results = new List<PoiLocalization>();
            foreach (var language in languages)
                results.Add(await LocalizeOneAsync(poi, language, sourceCode, force, ct));
            return results;
        }

        private async Task<PoiLocalization> LocalizeOneAsync(Poi poi, Language language, string sourceCode, bool force, CancellationToken ct)
        {
            var currentHash = TextTools.SourceHash(poi.Name, poi.Description);
            var existing = await _localizationRepository.GetAsync(poi.Id, language.Code);

            // Tên + mô tả đã dịch đúng nội dung hiện tại và đã có audio -> không cần dịch lại phần này
            var textReady = !force && LocalizationRules.IsFresh(existing, poi) && !string.IsNullOrEmpty(existing!.AudioUrl);
            if (textReady)
            {
                if (LocalizationRules.TipsFresh(existing, poi))
                {
                    if (existing!.Status == LocalizationStatus.Ready) return existing;
                    return await _localizationRepository.UpsertAsync(poi.Id, language.Code, l => l.Status = LocalizationStatus.Ready);
                }
                // Chỉ thiếu phần lưu ý (vd admin vừa sửa lưu ý) -> dịch riêng phần đó, giữ nguyên mô tả + audio
                try
                {
                    var (tips, tipsHash) = await TranslatedTipsAsync(poi, language, sourceCode, ct);
                    return await _localizationRepository.UpsertAsync(poi.Id, language.Code, l =>
                    {
                        l.Status = LocalizationStatus.Ready;
                        l.Tips = tips;
                        l.TipsHash = tipsHash;
                    });
                }
                catch (ExternalServiceException ex)
                {
                    _logger.LogWarning("Dịch lưu ý {Poi} sang {Lang} lỗi: {Message}", poi.Code, language.Code, ex.Message);
                    return await _localizationRepository.UpsertAsync(poi.Id, language.Code, l => l.Status = LocalizationStatus.Ready);
                }
            }

            await _localizationRepository.UpsertAsync(poi.Id, language.Code, l => l.Status = LocalizationStatus.Processing);

            // 1) Lấy văn bản theo ngôn ngữ đích
            var keepManual = !force && existing != null && existing.TranslatedBy == TranslatedBy.Manual
                             && LocalizationRules.IsFresh(existing, poi);
            string name, description;
            TranslatedBy by;
            string? tipsText, tipsHashValue;
            try
            {
                if (language.Code == _sourceLang)
                {
                    (name, description, by) = (poi.Name, poi.Description, TranslatedBy.Source);
                }
                else if (keepManual)
                {
                    (name, description, by) = (existing!.Name!, existing.Description!, TranslatedBy.Manual);
                }
                else
                {
                    name = await _translator.TranslateAsync(poi.Name, language.TranslatorCode, sourceCode, ct);
                    description = await _translator.TranslateAsync(poi.Description, language.TranslatorCode, sourceCode, ct);
                    by = TranslatedBy.Machine;
                }
                (tipsText, tipsHashValue) = await TranslatedTipsAsync(poi, language, sourceCode, ct);
            }
            catch (ExternalServiceException ex)
            {
                // Giữ nguyên bản dịch cũ (còn hợp lệ thì app vẫn dùng được), chỉ ghi nhận lỗi
                return await _localizationRepository.UpsertAsync(poi.Id, language.Code, l =>
                {
                    l.Status = LocalizationStatus.Failed;
                    l.Error = ex.Message;
                });
            }

            // 2) Tạo audio - chỉ đọc tên + mô tả, không đọc phần lưu ý
            var (audioUrl, audioError) = await MakeAudioAsync($"{name}. {description}", language.TtsVoice, ct);
            return await _localizationRepository.UpsertAsync(poi.Id, language.Code, l =>
            {
                l.Name = name;
                l.Description = description;
                l.AudioUrl = audioUrl;
                l.Status = LocalizationStatus.Ready;
                l.TranslatedBy = by;
                l.SourceHash = currentHash;
                l.Error = audioError;
                l.Tips = tipsText;
                l.TipsHash = tipsHashValue;
            });
        }

        // Bản dịch phần "lưu ý" + hash bản gốc. Điểm không có lưu ý thì xóa bản dịch cũ.
        private async Task<(string? Tips, string? TipsHash)> TranslatedTipsAsync(Poi poi, Language language, string sourceCode, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(poi.Tips)) return (null, null);
            var text = language.Code == _sourceLang
                ? poi.Tips
                : await _translator.TranslateAsync(poi.Tips, language.TranslatorCode, sourceCode, ct);
            return (text, TextTools.TipsHash(poi.Tips));
        }

        // ------------------------------------------------------------------ xếp hàng chạy nền
        private void EnqueueLocalize(int poiId, IReadOnlyCollection<string>? langs, bool force, string name)
        {
            var langsCopy = langs?.ToList();
            _queue.Enqueue(name, (sp, ct) =>
                sp.GetRequiredService<ILocalizationService>().LocalizePoiAsync(poiId, langsCopy, force, ct));
        }

        // Xếp hàng dịch cho 1 điểm. Trả về số ngôn ngữ được xếp hàng.
        public async Task<int> SchedulePoiAsync(int poiId, IReadOnlyCollection<string>? langs = null, bool force = false)
        {
            await GetPoiAsync(poiId);
            var languages = await TargetLanguagesAsync(langs);
            foreach (var language in languages)
                await _localizationRepository.UpsertAsync(poiId, language.Code, l => l.Status = LocalizationStatus.Pending);
            var codes = languages.Select(l => l.Code).ToList();
            EnqueueLocalize(poiId, codes, force, $"localize:{poiId}");
            return codes.Count;
        }

        public async Task<int> ScheduleAllAsync(bool force = false)
        {
            var pois = await _poiRepository.ListAllAsync();
            foreach (var poi in pois)
                await SchedulePoiAsync(poi.Id, null, force);
            foreach (var tour in await _tourRepository.ListWithDetailsAsync(activeOnly: false))
                ScheduleTour(tour.Id);
            return pois.Count;
        }

        public Task MarkOutdatedAsync(int poiId)
            => _localizationRepository.MarkStatusForPoiAsync(poiId, LocalizationStatus.Outdated);

        // ------------------------------------------------------------------ dịch ngay / sửa tay
        // Du khách cần ngay 1 ngôn ngữ chưa có -> dịch đồng bộ rồi trả về
        public async Task<PoiLocalization> EnsureAsync(int poiId, string lang)
        {
            var poi = await GetPoiAsync(poiId);
            if (!poi.IsActive) throw new NotFoundException("Không tìm thấy điểm tham quan", "POI_NOT_FOUND");
            var existing = await _localizationRepository.GetAsync(poiId, lang);
            if (LocalizationRules.IsComplete(existing, poi) && !string.IsNullOrEmpty(existing!.AudioUrl)) return existing;
            return (await LocalizePoiAsync(poiId, new[] { lang }))[0];
        }

        public async Task UpdateManualAsync(int poiId, string lang, ManualLocalizationDto dto)
        {
            var poi = await GetPoiAsync(poiId);
            var language = (await TargetLanguagesAsync(new[] { lang }))[0];
            if (lang == _sourceLang)
                throw new BusinessException("Muốn sửa tiếng Việt hãy sửa trực tiếp nội dung điểm tham quan", "EDIT_SOURCE_VIA_POI");
            var existing = await _localizationRepository.GetAsync(poiId, lang);
            var audioUrl = existing?.AudioUrl;
            string? error = null;
            if (dto.RegenerateAudio)
                (audioUrl, error) = await MakeAudioAsync($"{dto.Name}. {dto.Description}", language.TtsVoice, CancellationToken.None);
            await _localizationRepository.UpsertAsync(poiId, lang, l =>
            {
                l.Name = dto.Name;
                l.Description = dto.Description;
                l.AudioUrl = audioUrl;
                l.Status = LocalizationStatus.Ready;
                l.TranslatedBy = TranslatedBy.Manual;
                l.SourceHash = TextTools.SourceHash(poi.Name, poi.Description);
                l.Error = error;
            });
        }

        // Xóa mọi bản dịch của 1 điểm (bước bắt buộc trước khi xóa điểm)
        public Task<int> DeleteForPoiAsync(int poiId) => _localizationRepository.DeleteForPoiAsync(poiId);

        // Không cho xóa khi bản dịch đang chờ / đang xử lý: tác vụ nền chạy xong sẽ ghi lại bản vừa xóa
        public async Task DeleteOneAsync(int poiId, string lang)
        {
            var loc = await _localizationRepository.GetAsync(poiId, lang)
                      ?? throw new NotFoundException($"Điểm không có bản dịch '{lang}'", "LOCALIZATION_NOT_FOUND");
            if (loc.Status is LocalizationStatus.Pending or LocalizationStatus.Processing)
                throw new ConflictException($"Bản dịch '{lang}' đang được xử lý, hãy đợi xong rồi xóa", "LOCALIZATION_BUSY");
            await _localizationRepository.DeleteOneAsync(poiId, lang);
        }

        // Bảng trạng thái dịch của 1 điểm cho mọi ngôn ngữ đang bật (trang quản trị)
        public async Task<List<LocalizationOverviewDto>> OverviewAsync(int poiId)
        {
            var poi = await GetPoiAsync(poiId);
            var languages = await _languageRepository.ListAsync(activeOnly: true);
            var byLang = (await _localizationRepository.ListForPoiAsync(poiId)).ToDictionary(l => l.Lang);
            return languages.Select(language =>
            {
                var loc = byLang.GetValueOrDefault(language.Code);
                return new LocalizationOverviewDto
                {
                    Lang = language.Code,
                    LanguageName = language.NativeName,
                    Status = loc?.Status.ToString() ?? "Missing",
                    IsFresh = LocalizationRules.IsFresh(loc, poi),
                    TranslatedBy = loc?.TranslatedBy.ToString(),
                    HasAudio = !string.IsNullOrEmpty(loc?.AudioUrl),
                    AudioUrl = loc?.AudioUrl,
                    Name = loc?.Name,
                    Description = loc?.Description,
                    Error = loc?.Error
                };
            }).ToList();
        }

        // ------------------------------------------------------------------ du khách chọn ngôn ngữ
        /*
         * Khách vừa chọn 1 ngôn ngữ: xếp hàng dịch NGAY (chạy nền) mọi điểm / lộ trình còn thiếu bản dịch.
         * Không bắt khách chờ - app hiển thị bản dự phòng rồi tự cập nhật. Trả về số điểm đang chờ dịch.
         */
        public async Task<int> RequestLanguageAsync(string lang)
        {
            if (lang == _sourceLang) return 0;
            var language = await _languageRepository.GetByCodeAsync(lang);
            if (language == null || !language.IsActive) return 0;

            var pois = await _poiRepository.ListActiveAsync();
            var locs = (await _localizationRepository.ListForLangsAsync(new[] { lang }, pois.Select(p => p.Id)))
                .ToDictionary(l => l.PoiId);
            var now = DateTime.UtcNow;
            var pending = 0;
            foreach (var poi in pois)
            {
                var loc = locs.GetValueOrDefault(poi.Id);
                if (LocalizationRules.IsComplete(loc, poi)) continue;
                var inProgress = loc != null && loc.Status is LocalizationStatus.Pending or LocalizationStatus.Processing;
                if (inProgress && loc!.UpdatedAt > now - BusinessConstants.StaleAfter)
                {
                    pending++; // đang được dịch rồi, không xếp hàng lần nữa
                    continue;
                }
                if (loc != null && loc.Status == LocalizationStatus.Failed && loc.UpdatedAt > now - BusinessConstants.FailedRetryAfter)
                    continue; // vừa lỗi xong (vd mất mạng) -> chờ một lúc rồi mới thử lại
                await _localizationRepository.UpsertAsync(poi.Id, lang, l => l.Status = LocalizationStatus.Pending);
                EnqueueLocalize(poi.Id, new[] { lang }, false, $"localize:{poi.Id}:{lang}");
                pending++;
            }

            // Lộ trình: app tải lại vài lần trong lúc chờ -> mỗi lộ trình + ngôn ngữ chỉ xếp hàng 1 lần / 2 phút
            foreach (var tour in await _tourRepository.ListWithDetailsAsync(activeOnly: true))
            {
                if (tour.Translations.Any(t => t.Lang == lang)) continue;
                var cacheKey = $"tour-translate:{tour.Id}:{lang}";
                if (_cache.TryGetValue(cacheKey, out _)) continue;
                _cache.Set(cacheKey, true, BusinessConstants.TourRequestInterval);
                ScheduleTour(tour.Id, new[] { lang });
            }
            return pending;
        }

        // Lúc khởi động: server bị tắt khi đang dịch -> chạy lại các bản dịch kẹt ở Pending / Processing
        public async Task<int> RecoverUnfinishedAsync()
        {
            var unfinished = await _localizationRepository.ListByStatusAsync(LocalizationStatus.Pending, LocalizationStatus.Processing);
            var activeCodes = (await _languageRepository.ListAsync(activeOnly: true)).Select(l => l.Code).ToHashSet();
            var recovered = 0;
            foreach (var group in unfinished.GroupBy(l => l.PoiId))
            {
                var langs = group.Select(l => l.Lang).Where(activeCodes.Contains).ToList();
                if (langs.Count == 0 || await _poiRepository.GetByIdAsync(group.Key) == null) continue;
                EnqueueLocalize(group.Key, langs, false, $"recover:{group.Key}");
                recovered++;
            }
            if (recovered > 0) _logger.LogInformation("Khôi phục {Count} điểm có bản dịch đang dở sau khi khởi động lại", recovered);
            return recovered;
        }

        // ------------------------------------------------------------------ lộ trình
        public async Task LocalizeTourAsync(int tourId, IReadOnlyCollection<string>? langs = null, CancellationToken ct = default)
        {
            var tour = await _tourRepository.GetWithDetailsAsync(tourId);
            if (tour == null) return;
            var sourceCode = await SourceTranslatorCodeAsync();
            foreach (var language in await _languageRepository.ListAsync(activeOnly: true))
            {
                if (language.Code == _sourceLang || (langs != null && langs.Count > 0 && !langs.Contains(language.Code))) continue;
                try
                {
                    var name = await _translator.TranslateAsync(tour.Name, language.TranslatorCode, sourceCode, ct);
                    var description = string.IsNullOrEmpty(tour.Description)
                        ? null
                        : await _translator.TranslateAsync(tour.Description, language.TranslatorCode, sourceCode, ct);
                    var row = tour.Translations.FirstOrDefault(t => t.Lang == language.Code);
                    if (row == null)
                    {
                        row = new TourTranslation { TourId = tour.Id, Lang = language.Code };
                        tour.Translations.Add(row);
                    }
                    row.Name = name;
                    row.Description = description;
                }
                catch (ExternalServiceException)
                {
                    // thiếu bản dịch -> app tự dùng ngôn ngữ dự phòng
                }
            }
            tour.UpdatedAt = DateTime.UtcNow; // đổi phiên bản dữ liệu -> app du khách tải lại bản dịch mới
            await _tourRepository.UpdateAsync(tour);
        }

        public void ScheduleTour(int tourId, IReadOnlyCollection<string>? langs = null)
        {
            var langsCopy = langs?.ToList();
            _queue.Enqueue($"localize-tour:{tourId}", (sp, ct) =>
                sp.GetRequiredService<ILocalizationService>().LocalizeTourAsync(tourId, langsCopy, ct));
        }
    }
}
