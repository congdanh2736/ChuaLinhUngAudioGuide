using AudioGuide.Api.Common;
using AudioGuide.Api.Models;

namespace AudioGuide.Api.Services.Logic
{
    public record OpenStatus(string State, string? ClosesAt = null, int? MinutesLeft = null,
        string? OpensAt = null, int? OpensDay = null, int? OpensInDays = null);

    public record PopularTimes(int SampleSize, Dictionary<int, int[]>? ByDay);

    /*
     * Nghiệp vụ "thông tin chi tiết" của điểm tham quan - giống thẻ địa điểm trên Google Maps:
     * giờ mở cửa, đang mở/đóng cửa, khung giờ đông khách.
     * Toàn bộ là HÀM THUẦN (giờ hiện tại được truyền vào) -> test được với mọi thời điểm.
     */
    public static class PoiDetailsLogic
    {
        public static int ToMinutes(string hhmm)
        {
            var parts = hhmm.Split(':');
            return int.Parse(parts[0]) * 60 + int.Parse(parts[1]);
        }

        public static string FormatMinutes(int total) => $"{total / 60:00}:{total % 60:00}";

        // Ngày trong tuần kiểu Việt Nam: 0 = Thứ Hai ... 6 = Chủ nhật (DayOfWeek của .NET: 0 = Chủ nhật)
        public static int WeekdayIndex(DateTime date) => ((int)date.DayOfWeek + 6) % 7;

        public static List<(int Start, int End)> PeriodsForDay(IEnumerable<PoiOpeningHour> hours, int day)
            => hours.Where(h => h.Day == day)
                .Select(h => (ToMinutes(h.Open), ToMinutes(h.Close)))
                .OrderBy(p => p.Item1)
                .ToList();

        // Trả về mô tả lỗi nếu có 2 khung giờ chồng nhau trong cùng ngày, không lỗi thì null
        public static string? FindOverlap(IReadOnlyCollection<PoiOpeningHour> hours)
        {
            for (var day = 0; day < 7; day++)
            {
                var periods = PeriodsForDay(hours, day);
                for (var i = 1; i < periods.Count; i++)
                    if (periods[i].Start < periods[i - 1].End)
                        return $"{BusinessConstants.DayNamesVi[day]} có 2 khung giờ chồng nhau";
            }
            return null;
        }

        /*
         * Trạng thái mở cửa tại thời điểm nowLocal (giờ địa phương của chùa):
         * Open | ClosingSoon | Closed | OpeningSoon | Unknown (chưa nhập giờ).
         */
        public static OpenStatus GetOpenStatus(IReadOnlyCollection<PoiOpeningHour> hours, DateTime nowLocal)
        {
            if (hours.Count == 0) return new OpenStatus("Unknown");
            var today = WeekdayIndex(nowLocal);
            var minute = nowLocal.Hour * 60 + nowLocal.Minute;

            foreach (var (start, end) in PeriodsForDay(hours, today))
            {
                if (start <= minute && minute < end)
                {
                    var left = end - minute;
                    return new OpenStatus(left <= BusinessConstants.ClosingSoonMinutes ? "ClosingSoon" : "Open",
                        ClosesAt: FormatMinutes(end), MinutesLeft: left);
                }
            }

            // Đang đóng cửa -> tìm lần mở kế tiếp (hôm nay sau giờ hiện tại, rồi các ngày sau)
            for (var offset = 0; offset < 8; offset++)
            {
                var day = (today + offset) % 7;
                foreach (var (start, _) in PeriodsForDay(hours, day))
                {
                    if (offset == 0 && start <= minute) continue;
                    var wait = offset * 24 * 60 + start - minute;
                    return new OpenStatus(wait <= BusinessConstants.OpeningSoonMinutes ? "OpeningSoon" : "Closed",
                        OpensAt: FormatMinutes(start), OpensDay: day, OpensInDays: offset);
                }
            }
            return new OpenStatus("Unknown");
        }

        /*
         * "Khung giờ đông khách" từ dữ liệu THẬT (lượt xem / nghe / đi qua điểm).
         * Trả về mức 0-100 cho từng giờ của từng ngày (100 = giờ đông nhất tuần). Ít dữ liệu quá thì ByDay = null.
         */
        public static PopularTimes GetPopularTimes(IEnumerable<DateTime> eventTimesUtc, int utcOffsetHours)
        {
            var counts = new int[7, 24];
            var total = 0;
            foreach (var moment in eventTimesUtc)
            {
                var local = DateTime.SpecifyKind(moment, DateTimeKind.Utc).AddHours(utcOffsetHours);
                counts[WeekdayIndex(local), local.Hour]++;
                total++;
            }
            if (total < BusinessConstants.PopularMinEvents) return new PopularTimes(total, null);
            var peak = counts.Cast<int>().Max();
            var byDay = new Dictionary<int, int[]>();
            for (var d = 0; d < 7; d++)
                byDay[d] = Enumerable.Range(0, 24).Select(h => (int)Math.Round(counts[d, h] * 100.0 / peak, MidpointRounding.ToEven)).ToArray();
            return new PopularTimes(total, byDay);
        }

        // "Thứ Hai - Chủ nhật: 06:00-11:30, 13:30-21:00" - gộp các ngày liền nhau có cùng giờ (dùng cho chatbot)
        public static string DescribeHoursVi(IReadOnlyCollection<PoiOpeningHour> hours)
        {
            if (hours.Count == 0) return "chưa có thông tin";
            var byDay = Enumerable.Range(0, 7)
                .Select(d => string.Join(", ", PeriodsForDay(hours, d).Select(p => $"{FormatMinutes(p.Start)}-{FormatMinutes(p.End)}")))
                .ToList();
            var groups = new List<(int First, int Last, string Text)>();
            for (var day = 0; day < 7; day++)
            {
                if (groups.Count > 0 && groups[^1].Text == byDay[day])
                    groups[^1] = (groups[^1].First, day, byDay[day]);
                else
                    groups.Add((day, day, byDay[day]));
            }
            return string.Join("; ", groups.Select(g =>
            {
                var days = g.First == g.Last
                    ? BusinessConstants.DayNamesVi[g.First]
                    : $"{BusinessConstants.DayNamesVi[g.First]} - {BusinessConstants.DayNamesVi[g.Last]}";
                return $"{days}: {(g.Text.Length == 0 ? "đóng cửa" : g.Text)}";
            }));
        }

        // Thông tin chi tiết dạng văn bản tiếng Việt để đưa vào tài liệu cho chatbot
        public static string DescribeDetailsVi(Poi poi)
        {
            var lines = new List<string> { $"Giờ mở cửa: {DescribeHoursVi(poi.OpeningHours.ToList())}." };
            var fee = poi.EntryFeeVnd == 0 ? "miễn phí" : poi.EntryFeeVnd.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", ".") + " đồng";
            lines.Add($"Vé vào cửa: {fee}.");
            if (poi.VisitMinutes.HasValue) lines.Add($"Thời gian tham quan gợi ý: khoảng {poi.VisitMinutes} phút.");
            if (poi.Amenities.Count > 0)
                lines.Add("Tiện ích / quy định: " + string.Join(", ",
                    poi.Amenities.Select(a => BusinessConstants.Amenities.GetValueOrDefault(a, a))) + ".");
            if (!string.IsNullOrEmpty(poi.Tips)) lines.Add($"Lưu ý: {poi.Tips}");
            return string.Join(" ", lines);
        }
    }
}
