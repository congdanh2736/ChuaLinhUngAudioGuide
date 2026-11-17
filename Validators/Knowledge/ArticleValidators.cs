using AudioGuide.Api.DTOs.Knowledge;
using FluentValidation;

namespace AudioGuide.Api.Validators.Knowledge
{
    public class CreateArticleValidator : AbstractValidator<CreateArticleDto>
    {
        public CreateArticleValidator()
        {
            RuleFor(x => x.Title).NotEmpty().WithMessage("Vui lòng nhập tiêu đề").MaximumLength(200).WithMessage("Tiêu đề tối đa 200 ký tự");
            RuleFor(x => x.Content).NotEmpty().WithMessage("Vui lòng nhập nội dung")
                .MaximumLength(10000).WithMessage("Nội dung tối đa 10.000 ký tự");
        }
    }

    public class UpdateArticleValidator : AbstractValidator<UpdateArticleDto>
    {
        public UpdateArticleValidator()
        {
            RuleFor(x => x.Title!).NotEmpty().MaximumLength(200).When(x => x.Title != null).WithMessage("Tiêu đề từ 1 đến 200 ký tự");
            RuleFor(x => x.Content!).NotEmpty().MaximumLength(10000).When(x => x.Content != null)
                .WithMessage("Nội dung từ 1 đến 10.000 ký tự");
        }
    }
}
