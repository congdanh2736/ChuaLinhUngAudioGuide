using System.Security.Claims;
using AudioGuide.Api.Common;
using AudioGuide.Api.Interface.Services;
using AudioGuide.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace AudioGuide.Api.Filters
{
    // Thông tin người đang dùng app du khách (Controller lấy qua HttpContext.GetVisitor())
    public record VisitorContext(int? SessionId, string? Status = null, DateTime? AccessExpiresAt = null, bool IsAdminPreview = false);

    /*
     * Cổng kiểm soát truy cập cho các API của du khách: gắn [VisitorAccess] lên Controller / action.
     * - Token du khách hợp lệ + phiên còn hoạt động (admin chưa thu hồi) -> cho qua.
     * - Token admin / nhân viên -> cho qua để xem thử app.
     * - Không có token: Access:Required = false (demo) thì cho qua ẩn danh, ngược lại chặn 401 ACCESS_REQUIRED.
     */
    public class VisitorAccessAttribute : TypeFilterAttribute
    {
        public VisitorAccessAttribute() : base(typeof(VisitorAccessFilter)) { }
    }

    public class VisitorAccessFilter : IAsyncActionFilter
    {
        public const string ItemKey = "Visitor";
        private readonly IAccessService _accessService;
        private readonly AccessSettings _settings;

        public VisitorAccessFilter(IAccessService accessService, IOptions<AccessSettings> options)
        {
            _accessService = accessService;
            _settings = options.Value;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;
            VisitorContext visitor;
            if (user.IsInRole(RoleGroups.Visitor) && int.TryParse(user.FindFirstValue(JwtService.SessionClaim), out var sessionId))
            {
                var session = await _accessService.VerifySessionAsync(sessionId);
                visitor = new VisitorContext(session.Id, session.Status.ToString(), session.AccessExpiresAt);
            }
            else if (user.IsInRole(nameof(AppRole.Admin)) || user.IsInRole(nameof(AppRole.Staff)))
            {
                visitor = new VisitorContext(null, IsAdminPreview: true);
            }
            else if (_settings.Required)
            {
                throw new UnauthorizedException("Bạn cần mua quyền truy cập để sử dụng", "ACCESS_REQUIRED");
            }
            else
            {
                visitor = new VisitorContext(null);
            }
            context.HttpContext.Items[ItemKey] = visitor;
            await next();
        }
    }

    public static class VisitorHttpContextExtensions
    {
        public static VisitorContext GetVisitor(this HttpContext context)
            => context.Items[VisitorAccessFilter.ItemKey] as VisitorContext ?? new VisitorContext(null);
    }
}
