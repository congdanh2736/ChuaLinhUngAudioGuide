using AudioGuide.Api.Common;
using AudioGuide.Api.DTOs.Auth;
using FluentValidation;

namespace AudioGuide.Api.Validators.Auth
{
    /*
     * Kiểm tra dữ liệu gửi lên (FluentValidation). Sai -> ASP.NET tự trả 400 kèm danh sách lỗi theo từng ô,
     * request không vào tới Controller / Service.
     */
    public class LoginValidator : AbstractValidator<LoginDto>
    {
        public LoginValidator()
        {
            RuleFor(x => x.UserName).NotEmpty().WithMessage("Vui lòng nhập tên đăng nhập")
                .MaximumLength(50).WithMessage("Tên đăng nhập tối đa 50 ký tự");
            RuleFor(x => x.Password).NotEmpty().WithMessage("Vui lòng nhập mật khẩu")
                .MaximumLength(100).WithMessage("Mật khẩu tối đa 100 ký tự");
        }
    }

    public class ChangePasswordValidator : AbstractValidator<ChangePasswordDto>
    {
        public ChangePasswordValidator()
        {
            RuleFor(x => x.OldPassword).NotEmpty().WithMessage("Vui lòng nhập mật khẩu hiện tại");
            RuleFor(x => x.NewPassword).NotEmpty().WithMessage("Vui lòng nhập mật khẩu mới")
                .MinimumLength(8).WithMessage("Mật khẩu mới tối thiểu 8 ký tự")
                .MaximumLength(100).WithMessage("Mật khẩu tối đa 100 ký tự")
                .NotEqual(x => x.OldPassword).WithMessage("Mật khẩu mới phải khác mật khẩu cũ");
        }
    }

    public class CreateUserValidator : AbstractValidator<CreateUserDto>
    {
        public CreateUserValidator()
        {
            RuleFor(x => x.UserName).NotEmpty().WithMessage("Vui lòng nhập tên đăng nhập")
                .Length(3, 50).WithMessage("Tên đăng nhập từ 3 đến 50 ký tự")
                .Matches(@"^[a-zA-Z0-9_.]+$").WithMessage("Tên đăng nhập chỉ gồm chữ không dấu, số, dấu _ và .");
            RuleFor(x => x.Password).NotEmpty().WithMessage("Vui lòng nhập mật khẩu")
                .MinimumLength(8).WithMessage("Mật khẩu tối thiểu 8 ký tự")
                .MaximumLength(100).WithMessage("Mật khẩu tối đa 100 ký tự");
            RuleFor(x => x.FullName).NotEmpty().WithMessage("Vui lòng nhập họ tên")
                .MaximumLength(100).WithMessage("Họ tên tối đa 100 ký tự");
            RuleFor(x => x.Role).Must(r => Enum.TryParse<AppRole>(r, true, out _))
                .WithMessage("Vai trò phải là Admin hoặc Staff");
        }
    }

    public class UpdateUserValidator : AbstractValidator<UpdateUserDto>
    {
        public UpdateUserValidator()
        {
            RuleFor(x => x.FullName!).NotEmpty().MaximumLength(100).When(x => x.FullName != null)
                .WithMessage("Họ tên từ 1 đến 100 ký tự");
            RuleFor(x => x.Role).Must(r => Enum.TryParse<AppRole>(r, true, out _)).When(x => x.Role != null)
                .WithMessage("Vai trò phải là Admin hoặc Staff");
            RuleFor(x => x.Password!).MinimumLength(8).MaximumLength(100).When(x => !string.IsNullOrEmpty(x.Password))
                .WithMessage("Mật khẩu từ 8 đến 100 ký tự");
        }
    }
}
