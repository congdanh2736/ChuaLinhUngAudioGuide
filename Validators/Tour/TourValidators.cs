using AudioGuide.Api.DTOs.Tour;
using AudioGuide.Api.Validators.Poi;
using FluentValidation;

namespace AudioGuide.Api.Validators.Tour
{
    public class CreateTourValidator : AbstractValidator<CreateTourDto>
    {
        public CreateTourValidator()
        {
            RuleFor(x => x.Code).NotEmpty().WithMessage("Vui lòng nhập mã lộ trình")
                .Matches(PoiRules.CodePattern).WithMessage("Mã gồm 2-50 ký tự chữ không dấu, số, _ hoặc -");
            RuleFor(x => x.Name).NotEmpty().WithMessage("Vui lòng nhập tên").MaximumLength(200).WithMessage("Tên tối đa 200 ký tự");
            RuleFor(x => x.Description).MaximumLength(2000).WithMessage("Mô tả tối đa 2000 ký tự");
            RuleFor(x => x.EstimatedMinutes).InclusiveBetween(1, 600).WithMessage("Thời gian ước tính từ 1 đến 600 phút");
        }
    }

    public class UpdateTourValidator : AbstractValidator<UpdateTourDto>
    {
        public UpdateTourValidator()
        {
            RuleFor(x => x.Code!).Matches(PoiRules.CodePattern).When(x => x.Code != null)
                .WithMessage("Mã gồm 2-50 ký tự chữ không dấu, số, _ hoặc -");
            RuleFor(x => x.Name!).NotEmpty().MaximumLength(200).When(x => x.Name != null).WithMessage("Tên từ 1 đến 200 ký tự");
            RuleFor(x => x.Description!).MaximumLength(2000).When(x => x.Description != null).WithMessage("Mô tả tối đa 2000 ký tự");
            RuleFor(x => x.EstimatedMinutes!.Value).InclusiveBetween(1, 600).When(x => x.EstimatedMinutes.HasValue)
                .WithMessage("Thời gian ước tính từ 1 đến 600 phút");
        }
    }

    public class RecommendRequestValidator : AbstractValidator<RecommendRequestDto>
    {
        public RecommendRequestValidator()
        {
            RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).WithMessage("Vĩ độ phải từ -90 đến 90");
            RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).WithMessage("Kinh độ phải từ -180 đến 180");
            RuleFor(x => x.Lang).NotEmpty().MaximumLength(16);
            RuleFor(x => x.MaxStops!.Value).InclusiveBetween(1, 30).When(x => x.MaxStops.HasValue)
                .WithMessage("Số điểm tối đa từ 1 đến 30");
        }
    }
}
