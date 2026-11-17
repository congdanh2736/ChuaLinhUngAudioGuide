using AudioGuide.Api.Models;
using AudioGuide.Api.Services.Logic;

namespace AudioGuide.Tests.Logic
{
    // "Đang mở / đóng cửa" và "khung giờ đông khách" - thời điểm được truyền vào nên test được mọi giờ
    public class PoiDetailsLogicTests
    {
        // Mở cửa cả tuần 06:00-11:30 và 13:30-21:00
        private static List<PoiOpeningHour> Hours() => Enumerable.Range(0, 7)
            .SelectMany(d => new[]
            {
                new PoiOpeningHour { Day = d, Open = "06:00", Close = "11:30" },
                new PoiOpeningHour { Day = d, Open = "13:30", Close = "21:00" }
            }).ToList();

        // 2026-11-16 là Thứ Hai
        private static DateTime Monday(int hour, int minute) => new(2026, 11, 16, hour, minute, 0);

        [Fact]
        public void WeekdayIndex_MondayIsZero_SundayIsSix()
        {
            Assert.Equal(0, PoiDetailsLogic.WeekdayIndex(Monday(8, 0)));
            Assert.Equal(6, PoiDetailsLogic.WeekdayIndex(new DateTime(2026, 11, 22)));
        }

        [Fact]
        public void OpenStatus_InsidePeriod_IsOpen()
        {
            var status = PoiDetailsLogic.GetOpenStatus(Hours(), Monday(8, 0));
            Assert.Equal("Open", status.State);
            Assert.Equal("11:30", status.ClosesAt);
        }

        [Fact]
        public void OpenStatus_LastHour_IsClosingSoon()
            => Assert.Equal("ClosingSoon", PoiDetailsLogic.GetOpenStatus(Hours(), Monday(11, 0)).State);

        [Fact]
        public void OpenStatus_LunchBreak_ReportsNextOpening()
        {
            var status = PoiDetailsLogic.GetOpenStatus(Hours(), Monday(12, 0));
            Assert.Equal("Closed", status.State);
            Assert.Equal("13:30", status.OpensAt);
            Assert.Equal(0, status.OpensInDays);
        }

        [Fact]
        public void OpenStatus_LateNight_OpensTomorrow()
        {
            var status = PoiDetailsLogic.GetOpenStatus(Hours(), Monday(23, 0));
            Assert.Equal("06:00", status.OpensAt);
            Assert.Equal(1, status.OpensDay);
            Assert.Equal(1, status.OpensInDays);
        }

        [Fact]
        public void OpenStatus_NoHours_IsUnknown()
            => Assert.Equal("Unknown", PoiDetailsLogic.GetOpenStatus(new List<PoiOpeningHour>(), Monday(8, 0)).State);

        [Fact]
        public void FindOverlap_DetectsOverlappingPeriods()
        {
            var hours = new List<PoiOpeningHour>
            {
                new() { Day = 2, Open = "06:00", Close = "12:00" },
                new() { Day = 2, Open = "11:00", Close = "18:00" }
            };
            Assert.Equal("Thứ Tư có 2 khung giờ chồng nhau", PoiDetailsLogic.FindOverlap(hours));
            Assert.Null(PoiDetailsLogic.FindOverlap(Hours()));
        }

        [Fact]
        public void PopularTimes_NotEnoughData_ReturnsNull()
        {
            var result = PoiDetailsLogic.GetPopularTimes(Enumerable.Repeat(DateTime.UtcNow, 5), 7);
            Assert.Equal(5, result.SampleSize);
            Assert.Null(result.ByDay);
        }

        [Fact]
        public void PopularTimes_PeakHourIs100_UsesSiteTimezone()
        {
            // 01:00 UTC = 08:00 giờ Việt Nam, Thứ Hai
            var events = Enumerable.Repeat(new DateTime(2026, 11, 16, 1, 0, 0, DateTimeKind.Utc), 30);
            var result = PoiDetailsLogic.GetPopularTimes(events, 7);
            Assert.Equal(100, result.ByDay![0][8]);
            Assert.Equal(0, result.ByDay[0][9]);
        }

        [Fact]
        public void DescribeHoursVi_GroupsSameDays()
            => Assert.Equal("Thứ Hai - Chủ nhật: 06:00-11:30, 13:30-21:00", PoiDetailsLogic.DescribeHoursVi(Hours()));
    }
}
