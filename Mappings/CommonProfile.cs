using AudioGuide.Api.DTOs.Access;
using AudioGuide.Api.DTOs.Auth;
using AudioGuide.Api.DTOs.Chat;
using AudioGuide.Api.DTOs.Knowledge;
using AudioGuide.Api.DTOs.Language;
using AudioGuide.Api.Models;
using AutoMapper;

namespace AudioGuide.Api.Mappings
{
    public class LanguageProfile : Profile
    {
        public LanguageProfile()
        {
            CreateMap<Language, LanguageDto>();
            CreateMap<Language, LanguageAdminDto>();
        }
    }

    public class AccessProfile : Profile
    {
        public AccessProfile()
        {
            CreateMap<AccessSession, AccessSessionDto>()
                .ForMember(d => d.Method, o => o.MapFrom(s => s.Method.ToString()))
                .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
                .ForMember(d => d.CreatedByName, o => o.MapFrom(s => s.CreatedBy != null ? s.CreatedBy.FullName : null));
        }
    }

    public class KnowledgeProfile : Profile
    {
        public KnowledgeProfile()
        {
            CreateMap<KnowledgeArticle, ArticleDto>();
            CreateMap<CreateArticleDto, KnowledgeArticle>()
                .ForMember(d => d.Id, o => o.Ignore())
                .ForMember(d => d.CreatedAt, o => o.Ignore())
                .ForMember(d => d.UpdatedAt, o => o.Ignore());
        }
    }

    public class ChatProfile : Profile
    {
        public ChatProfile()
        {
            CreateMap<ChatLog, ChatLogDto>();
        }
    }

    public class UserProfile : Profile
    {
        public UserProfile()
        {
            // Vai trò nằm ở bảng khác (AspNetUserRoles) -> AuthService điền sau
            CreateMap<ApplicationUser, UserDto>()
                .ForMember(d => d.UserName, o => o.MapFrom(s => s.UserName ?? ""))
                .ForMember(d => d.Role, o => o.Ignore());
        }
    }
}
