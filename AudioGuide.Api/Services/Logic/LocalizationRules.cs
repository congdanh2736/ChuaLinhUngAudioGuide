using AudioGuide.Api.Models;

namespace AudioGuide.Api.Services.Logic
{
    // Quy tắc "bản dịch còn dùng được không" - hàm thuần, dùng chung cho LocalizationService và ContentService
    public static class LocalizationRules
    {
        // Bản dịch dùng được: có đủ chữ và khớp nội dung gốc hiện tại (admin sửa mô tả -> bản cũ tự hết hiệu lực)
        public static bool IsFresh(PoiLocalization? loc, Poi poi)
            => loc != null
               && !string.IsNullOrEmpty(loc.Name)
               && !string.IsNullOrEmpty(loc.Description)
               && loc.SourceHash == TextTools.SourceHash(poi.Name, poi.Description);

        // Phần "lưu ý" có hash riêng: sửa lưu ý thì chỉ dịch lại lưu ý, không dịch lại mô tả + audio
        public static bool TipsFresh(PoiLocalization? loc, Poi poi)
        {
            if (string.IsNullOrEmpty(poi.Tips)) return true;
            return loc != null && !string.IsNullOrEmpty(loc.Tips) && loc.TipsHash == TextTools.TipsHash(poi.Tips);
        }

        public static bool IsComplete(PoiLocalization? loc, Poi poi) => IsFresh(loc, poi) && TipsFresh(loc, poi);
    }
}
