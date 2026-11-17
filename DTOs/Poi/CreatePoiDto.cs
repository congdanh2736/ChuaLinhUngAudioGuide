namespace AudioGuide.Api.DTOs.Poi
{
    public class CreatePoiDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = "landmark";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int TriggerRadiusM { get; set; } = 30;
        public int Priority { get; set; }
        public string? ThumbnailUrl { get; set; }
        public List<string> ImageUrls { get; set; } = new();
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public List<OpeningHourDto> OpeningHours { get; set; } = new();
        public int EntryFeeVnd { get; set; }
        public int? VisitMinutes { get; set; }
        public List<string> Amenities { get; set; } = new();
        public string Tips { get; set; } = string.Empty;
    }
}
