using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Access;
using FluentValidation;

namespace AudioGuide.Api.Validators.Access
{
    public class RedeemRequestValidator : AbstractValidator<RedeemRequestDto>
    {
        public RedeemRequestValidator()
        {
            RuleFor(x => x.Code).NotEmpty().WithMessage("Vui lòng nhập mã truy cập")
                .Length(4, 12).WithMessage("Mã truy cập từ 4 đến 12 ký tự");
        }
    }

    public class StartOnlineRequestValidator : AbstractValidator<StartOnlineRequestDto>
    {
        public StartOnlineRequestValidator()
        {
            RuleFor(x => x.DiscountPercent).Must(p => BusinessConstants.DiscountPercents.Contains(p))
                .WithMessage("Mức giảm giá không hợp lệ");
        }
    }

    public class CashCodeRequestValidator : AbstractValidator<CashCodeRequestDto>
    {
        public CashCodeRequestValidator()
        {
            RuleFor(x => x.Quantity).InclusiveBetween(1, 50).WithMessage("Số lượng mã từ 1 đến 50");
            RuleFor(x => x.Note).MaximumLength(200).WithMessage("Ghi chú tối đa 200 ký tự");
        }
    }

    public class WebhookRequestValidator : AbstractValidator<WebhookRequestDto>
    {
        public WebhookRequestValidator()
        {
            RuleFor(x => x.PaymentId).GreaterThan(0);
            RuleFor(x => x.Status).Must(s => s is "success" or "failed").WithMessage("Status phải là success hoặc failed");
        }
    }
}
