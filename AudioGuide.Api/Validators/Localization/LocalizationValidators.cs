using AudioGuide.Api.DTOs.Localization;
using FluentValidation;

namespace AudioGuide.Api.Validators.Localization
{
    public class ManualLocalizationValidator : AbstractValidator<ManualLocalizationDto>
    {
        public ManualLocalizationValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Vui lòng nhập tên").MaximumLength(200).WithMessage("Tên tối đa 200 ký tự");
            RuleFor(x => x.Description).NotEmpty().WithMessage("Vui lòng nhập nội dung")
                .MaximumLength(10000).WithMessage("Nội dung tối đa 10.000 ký tự");
        }
    }
}
