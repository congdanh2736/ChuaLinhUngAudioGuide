using AudioGuide.Api.DTOs.Chat;
using FluentValidation;

namespace AudioGuide.Api.Validators.Chat
{
    public class ChatRequestValidator : AbstractValidator<ChatRequestDto>
    {
        public ChatRequestValidator()
        {
            RuleFor(x => x.Question).NotEmpty().WithMessage("Vui lòng nhập câu hỏi")
                .Length(2, 500).WithMessage("Câu hỏi từ 2 đến 500 ký tự");
            RuleFor(x => x.Lang).NotEmpty().MaximumLength(16);
        }
    }
}
