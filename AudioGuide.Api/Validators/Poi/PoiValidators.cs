using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Poi;
using FluentValidation;

namespace AudioGuide.Api.Validators.Poi
{
    public static class PoiRules
    {
        public const string CodePattern = @"^[A-Za-z0-9_\-]{2,50}$";
        public const string OpenPattern = @"^([01]\d|2[0-3]):[0-5]\d$";
        public const string ClosePattern = @"^(([01]\d|2[0-3]):[0-5]\d|24:00)$";
    }

    // 1 khung giờ: ngày 0-6, giờ dạng HH:mm, giờ đóng phải sau giờ mở
    public class OpeningHourValidator : AbstractValidator<OpeningHourDto>
    {
        public OpeningHourValidator()
        {
            RuleFor(x => x.Day).InclusiveBetween(0, 6).WithMessage("Ngày phải từ 0 (Thứ Hai) đến 6 (Chủ nhật)");
            RuleFor(x => x.Open).Matches(PoiRules.OpenPattern).WithMessage("Giờ mở cửa phải dạng HH:mm, vd 06:00");
            RuleFor(x => x.Close).Matches(PoiRules.ClosePattern).WithMessage("Giờ đóng cửa phải dạng HH:mm, vd 21:00");
            // "HH:mm" cùng độ dài -> so sánh chuỗi = so sánh giờ
            RuleFor(x => x).Must(x => string.CompareOrdinal(x.Close, x.Open) > 0)
                .WithName("Close").WithMessage("Giờ đóng cửa phải sau giờ mở cửa");
        }
    }

    public class CreatePoiValidator : AbstractValidator<CreatePoiDto>
    {
        public CreatePoiValidator()
        {
            RuleFor(x => x.Code).NotEmpty().WithMessage("Vui lòng nhập mã điểm")
                .Matches(PoiRules.CodePattern).WithMessage("Mã gồm 2-50 ký tự chữ không dấu, số, _ hoặc -");
            RuleFor(x => x.Name).NotEmpty().WithMessage("Vui lòng nhập tên").MaximumLength(200).WithMessage("Tên tối đa 200 ký tự");
            RuleFor(x => x.Description).NotEmpty().WithMessage("Vui lòng nhập nội dung thuyết minh")
                .MaximumLength(10000).WithMessage("Nội dung tối đa 10.000 ký tự");
            RuleFor(x => x.Category).Must(c => BusinessConstants.Categories.Contains(c)).WithMessage("Loại điểm không hợp lệ");
            RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).WithMessage("Vĩ độ phải từ -90 đến 90");
            RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).WithMessage("Kinh độ phải từ -180 đến 180");
            RuleFor(x => x.TriggerRadiusM).InclusiveBetween(5, 500).WithMessage("Bán kính kích hoạt từ 5 đến 500 m");
            RuleFor(x => x.Priority).InclusiveBetween(0, 100).WithMessage("Mức ưu tiên từ 0 đến 100");
            RuleFor(x => x.ImageUrls).Must(l => l.Count <= 8).WithMessage("Tối đa 8 ảnh");
            RuleFor(x => x.OpeningHours).Must(l => l.Count <= 50).WithMessage("Tối đa 50 khung giờ");
            RuleForEach(x => x.OpeningHours).SetValidator(new OpeningHourValidator());
            RuleFor(x => x.EntryFeeVnd).InclusiveBetween(0, 10_000_000).WithMessage("Giá vé từ 0 đến 10.000.000 đ");
            RuleFor(x => x.VisitMinutes!.Value).InclusiveBetween(1, 600).When(x => x.VisitMinutes.HasValue)
                .WithMessage("Thời gian tham quan từ 1 đến 600 phút");
            RuleFor(x => x.Amenities).Must(l => l.Count <= 20).WithMessage("Tối đa 20 tiện ích");
            RuleFor(x => x.Tips).MaximumLength(2000).WithMessage("Lưu ý tối đa 2000 ký tự");
        }
    }

    public class UpdatePoiValidator : AbstractValidator<UpdatePoiDto>
    {
        public UpdatePoiValidator()
        {
            RuleFor(x => x.Code!).Matches(PoiRules.CodePattern).When(x => x.Code != null)
                .WithMessage("Mã gồm 2-50 ký tự chữ không dấu, số, _ hoặc -");
            RuleFor(x => x.Name!).NotEmpty().MaximumLength(200).When(x => x.Name != null).WithMessage("Tên từ 1 đến 200 ký tự");
            RuleFor(x => x.Description!).NotEmpty().MaximumLength(10000).When(x => x.Description != null)
                .WithMessage("Nội dung từ 1 đến 10.000 ký tự");
            RuleFor(x => x.Category!).Must(c => BusinessConstants.Categories.Contains(c)).When(x => x.Category != null)
                .WithMessage("Loại điểm không hợp lệ");
            RuleFor(x => x.Latitude!.Value).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue).WithMessage("Vĩ độ phải từ -90 đến 90");
            RuleFor(x => x.Longitude!.Value).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue).WithMessage("Kinh độ phải từ -180 đến 180");
            RuleFor(x => x.TriggerRadiusM!.Value).InclusiveBetween(5, 500).When(x => x.TriggerRadiusM.HasValue)
                .WithMessage("Bán kính kích hoạt từ 5 đến 500 m");
            RuleFor(x => x.Priority!.Value).InclusiveBetween(0, 100).When(x => x.Priority.HasValue).WithMessage("Mức ưu tiên từ 0 đến 100");
            RuleFor(x => x.ImageUrls!).Must(l => l.Count <= 8).When(x => x.ImageUrls != null).WithMessage("Tối đa 8 ảnh");
            RuleForEach(x => x.OpeningHours).SetValidator(new OpeningHourValidator()).When(x => x.OpeningHours != null);
            RuleFor(x => x.EntryFeeVnd!.Value).InclusiveBetween(0, 10_000_000).When(x => x.EntryFeeVnd.HasValue)
                .WithMessage("Giá vé từ 0 đến 10.000.000 đ");
            RuleFor(x => x.VisitMinutes!.Value).InclusiveBetween(1, 600).When(x => x.VisitMinutes.HasValue)
                .WithMessage("Thời gian tham quan từ 1 đến 600 phút");
            RuleFor(x => x.Tips!).MaximumLength(2000).When(x => x.Tips != null).WithMessage("Lưu ý tối đa 2000 ký tự");
        }
    }
}
