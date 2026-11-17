namespace AudioGuide.Api.DTOs.Knowledge
{
    public class ArticleDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateArticleDto
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public bool IsActive { get; set; } = true;
    }

    public class UpdateArticleDto
    {
        public string? Title { get; set; }
        public string? Content { get; set; }
        public List<string>? Tags { get; set; }
        public bool? IsActive { get; set; }
    }
}
