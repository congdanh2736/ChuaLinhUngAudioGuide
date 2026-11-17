using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Knowledge;
using AudioGuide.Api.Interface.Repositories;
using AudioGuide.Api.Interface.Services;
using AudioGuide.Api.Models;
using AutoMapper;

namespace AudioGuide.Api.Services
{
    // Quản lý bài viết kiến thức (nguồn tài liệu cho chatbot)
    public class KnowledgeService : IKnowledgeService
    {
        private readonly IKnowledgeRepository _repository;
        private readonly IMapper _mapper;

        public KnowledgeService(IKnowledgeRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public async Task<List<ArticleDto>> GetAllAsync()
            => _mapper.Map<List<ArticleDto>>(await _repository.ListAsync(activeOnly: false));

        private async Task<KnowledgeArticle> GetEntityAsync(int id)
            => await _repository.GetByIdAsync(id) ?? throw new NotFoundException("Không tìm thấy bài viết", "ARTICLE_NOT_FOUND");

        private static List<string> CleanTags(IEnumerable<string> tags)
            => tags.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct().ToList();

        public async Task<ArticleDto> CreateAsync(CreateArticleDto dto)
        {
            var article = _mapper.Map<KnowledgeArticle>(dto);
            article.Tags = CleanTags(dto.Tags);
            article.CreatedAt = article.UpdatedAt = DateTime.UtcNow;
            await _repository.AddAsync(article);
            return _mapper.Map<ArticleDto>(article);
        }

        public async Task<ArticleDto> UpdateAsync(int id, UpdateArticleDto dto)
        {
            var article = await GetEntityAsync(id);
            if (dto.Title != null) article.Title = dto.Title;
            if (dto.Content != null) article.Content = dto.Content;
            if (dto.Tags != null) article.Tags = CleanTags(dto.Tags);
            if (dto.IsActive.HasValue) article.IsActive = dto.IsActive.Value;
            article.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(article);
            return _mapper.Map<ArticleDto>(article);
        }

        public async Task DeleteAsync(int id) => await _repository.DeleteAsync(await GetEntityAsync(id));
    }
}
