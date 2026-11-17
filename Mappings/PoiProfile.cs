using AudioGuide.Api.DTOs.Poi;
using AudioGuide.Api.Models;
using AutoMapper;

namespace AudioGuide.Api.Mappings
{
    // Quy tắc chuyển đổi Entity <-> DTO cho điểm tham quan (AutoMapper tự chép các trường cùng tên)
    public class PoiProfile : Profile
    {
        public PoiProfile()
        {
            CreateMap<PoiOpeningHour, OpeningHourDto>();
            CreateMap<OpeningHourDto, PoiOpeningHour>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.PoiId, o => o.Ignore())
                .ForMember(d => d.Poi, o => o.Ignore());

            CreateMap<Poi, PoiDto>()
                .ForMember(d => d.OpeningHours, o => o.MapFrom(s => s.OpeningHours.OrderBy(h => h.Day).ThenBy(h => h.Open)));

            // Phần chung của bản dành cho du khách; tên / mô tả / audio theo ngôn ngữ do ContentService điền
            CreateMap<Poi, LocalizedPoiDto>()
                .ForMember(d => d.OpeningHours, o => o.MapFrom(s => s.OpeningHours.OrderBy(h => h.Day).ThenBy(h => h.Open)))
                .ForMember(d => d.AudioUrl, o => o.Ignore())
                .ForMember(d => d.RequestedLang, o => o.Ignore())
                .ForMember(d => d.ServedLang, o => o.Ignore())
                .ForMember(d => d.IsFallback, o => o.Ignore())
                .ForMember(d => d.DistanceM, o => o.Ignore())
                .ForMember(d => d.TipsLang, o => o.Ignore());

            // Tạo mới: khung giờ, mã, thời gian do PoiService xử lý riêng (chuẩn hóa + kiểm tra chồng giờ)
            CreateMap<CreatePoiDto, Poi>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.OpeningHours, o => o.Ignore())
                .ForMember(d => d.Localizations, o => o.Ignore())
                .ForMember(d => d.TourPois, o => o.Ignore())
                .ForMember(d => d.VisitEvents, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore());
        }
    }
}
