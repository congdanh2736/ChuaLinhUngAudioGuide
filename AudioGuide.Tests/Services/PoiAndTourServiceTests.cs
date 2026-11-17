using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Poi;
using AudioGuide.Api.DTOs.Tour;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Interface.Services;
using AudioGuide.Api.Models;
using AudioGuide.Api.Services;
using Moq;

namespace AudioGuide.Tests.Services
{
    public class PoiServiceTests
    {
        private readonly Mock<IPoiRepository> _pois = new();
        private readonly Mock<IPoiLocalizationRepository> _locs = new();
        private readonly Mock<ITourRepository> _tours = new();
        private readonly Mock<IVisitEventRepository> _events = new();
        private readonly Mock<ILocalizationService> _localization = new();

        private PoiService Service() => new(_pois.Object, _locs.Object, _tours.Object, _events.Object, _localization.Object, TestHelpers.Mapper);

        [Fact]
        public async Task Delete_WithLocalizations_Conflict()
        {
            _pois.Setup(r => r.GetWithDetailsAsync(1)).ReturnsAsync(TestHelpers.Poi());
            _locs.Setup(r => r.CountForPoiAsync(1)).ReturnsAsync(3);
            var ex = await Assert.ThrowsAsync<ConflictException>(() => Service().DeleteAsync(1));
            Assert.Equal("POI_HAS_LOCALIZATIONS", ex.Code);
            _pois.Verify(r => r.DeleteAsync(It.IsAny<Poi>()), Times.Never);
        }

        [Fact]
        public async Task Delete_WithVisitEvents_Conflict()
        {
            _pois.Setup(r => r.GetWithDetailsAsync(1)).ReturnsAsync(TestHelpers.Poi());
            _events.Setup(r => r.CountForPoiAsync(1)).ReturnsAsync(10);
            var ex = await Assert.ThrowsAsync<ConflictException>(() => Service().DeleteAsync(1));
            Assert.Equal("POI_HAS_VISIT_EVENTS", ex.Code);
        }

        [Fact]
        public async Task Delete_Clean_RemovesFromToursThenDeletes()
        {
            var poi = TestHelpers.Poi();
            _pois.Setup(r => r.GetWithDetailsAsync(1)).ReturnsAsync(poi);
            await Service().DeleteAsync(1);
            _tours.Verify(r => r.RemovePoiEverywhereAsync(1), Times.Once);
            _pois.Verify(r => r.DeleteAsync(poi), Times.Once);
        }

        [Fact]
        public async Task Create_DuplicateCode_Conflict()
        {
            _pois.Setup(r => r.GetByCodeAsync("CONG")).ReturnsAsync(TestHelpers.Poi());
            var ex = await Assert.ThrowsAsync<ConflictException>(() => Service().CreateAsync(new CreatePoiDto { Code = "cong", Name = "x", Description = "y" }));
            Assert.Equal("POI_CODE_EXISTS", ex.Code);
        }

        [Fact]
        public async Task Create_OverlappingHours_Rejected()
        {
            var dto = new CreatePoiDto
            {
                Code = "NEW", Name = "x", Description = "y",
                OpeningHours = new() { new() { Day = 0, Open = "06:00", Close = "12:00" }, new() { Day = 0, Open = "11:00", Close = "18:00" } }
            };
            var ex = await Assert.ThrowsAsync<BusinessException>(() => Service().CreateAsync(dto));
            Assert.Equal("OPENING_HOURS_OVERLAP", ex.Code);
        }

        [Fact]
        public async Task Create_SchedulesLocalization()
        {
            var dto = new CreatePoiDto { Code = "new_poi", Name = "Điểm mới", Description = "Mô tả", Amenities = new() { "parking", "parking" } };
            var result = await Service().CreateAsync(dto);
            Assert.Equal("NEW_POI", result.Code);
            Assert.Equal(new[] { "parking" }, result.Amenities);
            _localization.Verify(l => l.SchedulePoiAsync(It.IsAny<int>(), null, false), Times.Once);
        }

        [Fact]
        public async Task Update_DescriptionChanged_MarksOutdatedAndRetranslates()
        {
            _pois.Setup(r => r.GetWithDetailsAsync(1)).ReturnsAsync(TestHelpers.Poi());
            await Service().UpdateAsync(1, new UpdatePoiDto { Description = "Nội dung mới" });
            _localization.Verify(l => l.MarkOutdatedAsync(1), Times.Once);
            _localization.Verify(l => l.SchedulePoiAsync(1, null, false), Times.Once);
        }

        [Fact]
        public async Task Update_OnlyCoordinates_NoRetranslation()
        {
            _pois.Setup(r => r.GetWithDetailsAsync(1)).ReturnsAsync(TestHelpers.Poi());
            await Service().UpdateAsync(1, new UpdatePoiDto { Latitude = 16.2 });
            _localization.Verify(l => l.SchedulePoiAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<bool>()), Times.Never);
        }
    }

    public class TourServiceTests
    {
        private readonly Mock<ITourRepository> _tours = new();
        private readonly Mock<IPoiRepository> _pois = new();
        private readonly Mock<IContentService> _content = new();
        private readonly Mock<ILocalizationService> _localization = new();

        private TourService Service() => new(_tours.Object, _pois.Object, _content.Object, _localization.Object, TestHelpers.Mapper);

        [Fact]
        public async Task Create_DuplicatePoi_Rejected()
        {
            var ex = await Assert.ThrowsAsync<BusinessException>(() =>
                Service().CreateAsync(new CreateTourDto { Code = "T1", Name = "Tour", PoiIds = new() { 1, 1 } }));
            Assert.Equal("DUPLICATE_POI_IN_TOUR", ex.Code);
        }

        [Fact]
        public async Task Create_UnknownPoi_Rejected()
        {
            _pois.Setup(r => r.GetMissingIdsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new List<int> { 99 });
            var ex = await Assert.ThrowsAsync<BusinessException>(() =>
                Service().CreateAsync(new CreateTourDto { Code = "T1", Name = "Tour", PoiIds = new() { 99 } }));
            Assert.Equal("POI_NOT_FOUND", ex.Code);
        }

        [Fact]
        public async Task Recommend_OrdersByNearestNeighbor_AndSumsDistance()
        {
            _content.Setup(c => c.ListPoisAsync("vi")).ReturnsAsync(new List<LocalizedPoiDto>
            {
                new() { Id = 3, Name = "C", Latitude = 16.1020, Longitude = 108.2790, Description = "mot hai ba", Priority = 1 },
                new() { Id = 1, Name = "A", Latitude = 16.1003, Longitude = 108.2779, Description = "mot hai ba", Priority = 3 },
                new() { Id = 2, Name = "B", Latitude = 16.1010, Longitude = 108.2785, Description = "mot hai ba", Priority = 2 }
            });
            var route = await Service().RecommendAsync(new RecommendRequestDto { Latitude = 16.1000, Longitude = 108.2775, Lang = "vi" });

            Assert.Equal(new[] { 1, 2, 3 }, route.Stops.Select(s => s.Poi.Id));
            Assert.Equal(Math.Round(route.Stops.Sum(s => s.WalkDistanceM), 1), route.TotalDistanceM);
        }

        [Fact]
        public async Task Update_TextChanged_ClearsTranslationsAndReschedules()
        {
            var tour = new Tour { Id = 5, Code = "T", Name = "Cũ", Translations = { new TourTranslation { Lang = "en", Name = "Old" } } };
            _tours.Setup(r => r.GetWithDetailsAsync(5)).ReturnsAsync(tour);
            await Service().UpdateAsync(5, new UpdateTourDto { Name = "Mới" });
            Assert.Empty(tour.Translations);
            _localization.Verify(l => l.ScheduleTour(5, null), Times.Once);
        }

        [Fact]
        public async Task Update_PoiOrder_ReusesRowsByPosition()
        {
            var tour = new Tour { Id = 5, Code = "T", Name = "Tour" };
            tour.TourPois.Add(new TourPoi { TourId = 5, Position = 0, PoiId = 1 });
            tour.TourPois.Add(new TourPoi { TourId = 5, Position = 1, PoiId = 2 });
            tour.TourPois.Add(new TourPoi { TourId = 5, Position = 2, PoiId = 3 });
            _tours.Setup(r => r.GetWithDetailsAsync(5)).ReturnsAsync(tour);
            _pois.Setup(r => r.GetMissingIdsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new List<int>());

            var result = await Service().UpdateAsync(5, new UpdateTourDto { PoiIds = new() { 3, 1 } });

            Assert.Equal(new[] { 3, 1 }, result.PoiIds);
            Assert.Equal(2, tour.TourPois.Count);
        }
    }
}
