using AudioGuide.Api.DTOs.Tour;
using AudioGuide.Api.Models;
using AutoMapper;

namespace AudioGuide.Api.Mappings
{
    public class TourProfile : Profile
    {
        public TourProfile()
        {
            CreateMap<Tour, TourDto>()
                .ForMember(d => d.PoiIds, o => o.MapFrom(s => s.TourPois.OrderBy(tp => tp.Position).Select(tp => tp.PoiId)))
                .ForMember(d => d.TranslatedLanguages, o => o.MapFrom(s => s.Translations.Select(t => t.Lang).OrderBy(l => l)));
        }
    }
}
