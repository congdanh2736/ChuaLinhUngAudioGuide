using AudioGuide.Api.DTOs.Ai;
using FluentValidation;

namespace AudioGuide.Api.Validators.Ai
{
    public class EnhanceRequestValidator : AbstractValidator<EnhanceRequestDto>
    {
        public EnhanceRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithMessage("Tên từ 1 đến 200 ký tự");
            RuleFor(x => x.Description).Length(10, 5000).WithMessage("Mô tả từ 10 đến 5000 ký tự");
        }
    }
}
