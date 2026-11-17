using AudioGuide.Api.DTOs.Content;
using FluentValidation;

namespace AudioGuide.Api.Validators.Content
{
    public class EventRequestValidator : AbstractValidator<EventRequestDto>
    {
        public EventRequestValidator()
        {
            RuleFor(x => x.PoiId).GreaterThan(0).WithMessage("Thiếu mã điểm tham quan");
            RuleFor(x => x.Type).IsInEnum().WithMessage("Loại sự kiện không hợp lệ");
            RuleFor(x => x.Lang).NotEmpty().MaximumLength(16);
        }
    }
}
